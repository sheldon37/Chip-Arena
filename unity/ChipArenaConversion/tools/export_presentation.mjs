// Build-time only: evaluate pinned original pure functions, never the C# implementation.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {stripTypeScriptTypes} from 'node:module';
import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const provenance=JSON.parse(fs.readFileSync(path.join(root,'tools/presentation-provenance.json'),'utf8'));
for(const f of provenance.files){const b=fs.readFileSync(path.join(source,f.path));if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.git_blob_sha)throw Error('Source blob mismatch: '+f.path);}
const read=p=>fs.readFileSync(path.join(source,p),'utf8');
const content=JSON.parse(fs.readFileSync(path.join(root,'Assets/Conversion/Resources/GameContent.json'),'utf8'));
let code=read('src/client/magic-motion.ts').split('interface MagicState')[0].replace(/^import .*;\n/gm,'');
// Function has an inner semicolon, so take the complete source line.
const smoothLine=read('src/client/motion-quality.ts').split('\n').find(x=>x.startsWith('export const smoothMotion='));
const ranged=read('src/client/ranged-motion.ts');
const abilityPhrase=ranged.slice(ranged.indexOf('export function abilityPhrase('),ranged.indexOf('/** Rocket recoil envelope'));
const abilities=read('src/shared/abilities.ts');
const helpers=abilities.slice(abilities.indexOf('export const castTime='),abilities.indexOf('export const adsProfile='));
const bindings=`const clamp=(x,lo,hi)=>Math.max(lo,Math.min(hi,x));const defs=${JSON.stringify(content.abilities)};const kitFor=s=>Object.fromEntries(defs.filter(d=>d.spec===s).map(d=>[d.slot,d]));const abilityActive=(s,slot)=>!!kitFor(s)[slot]?.live;`;
const module=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(bindings+smoothLine+helpers+abilityPhrase+code,{mode:'transform'})).toString('base64'));
// Quantize input timestamps to the native float clock before evaluating the original double-precision functions.
const cases=[];const windup={fire:.16,water:.12,lightning:.065,darkness:.22};
for(const [spec,base] of Object.entries(windup))for(const rate of [.5,1,1.5,3])for(const pitch of [-.8,0,.8]){
 const release=base/rate,m={windup:release*.6,releaseAt:release};
 for(const age of [0,release*.3,release*.6,release,release+.05/rate,release+.25/rate,release+.46,release+.75])for(const streak of [false,true]){
  const p={spec,attackRate:rate,lastAttack:2,lastShot:age>=release?2+release:streak?1.8:-100};p.lastShot=Math.fround(p.lastShot);const time=Math.fround(2+age);const out=new Float64Array(13);const weight=module.castPose(p,time,m,pitch,out);
  cases.push({element:Object.keys(windup).indexOf(spec)+1,rate,pitch,time,attack:2,shot:p.lastShot,ability:null,abilityAt:-100,releaseAt:0,first:false,weight,pose:[...out]});
 }
 for(const d of content.abilities.filter(d=>d.spec===spec))for(const first of [false,true]){
  const releaseAt=module.castTime(d.windup,{attackRate:rate});
  for(const age of [releaseAt*.3,releaseAt,releaseAt+.05/rate,releaseAt+.4/rate,releaseAt+.55/rate+.25]){
   const p={spec,attackRate:rate,lastAttack:-100,lastShot:-100,lastAbility:d.slot==='q'?2:-100,lastSecondary:d.slot==='e'?2:-100,chips:{}};const time=Math.fround(2+age);const out=new Float64Array(13),weight=module.abilityCastPose(p,time,pitch,first,out);
   cases.push({element:Object.keys(windup).indexOf(spec)+1,rate,pitch,time,attack:-100,shot:-100,ability:d.id,abilityAt:2,releaseAt:2+releaseAt,first,weight,pose:[...out]});
  }
 }
}
const stats=Object.fromEntries(content.baseStats.map(x=>[x.name,x.value]));const rules=[];
for(const d of content.abilities.filter(d=>d.kind!=='ads'))for(const variation of [{},{attackRate:.01,abilityCooldown:.1,abilityRadius:4,abilityCharges:7},{attackRate:5,damage:9,abilityPower:5,abilityCost:.5,resourceCost:1.3,arcaneSurge:.3},{attackRate:.5,damage:.04,abilityCooldown:5,abilityRadius:.1,abilityCharges:-1}]){
 const s={...stats,...variation};rules.push({spec:d.spec,slot:d.slot,stats:Object.entries(s).map(([name,value])=>({name,value})),windup:module.castTime(d.windup,s),cooldown:module.abilityCooldown(d,s),cost:module.abilityCost(d,s)*s.resourceCost,radius:module.abilityRadius(d,s),scale:module.abilityScale(s,true),echo:module.abilityScale(s,true,true),charges:module.maxCharges(s)});
}
fs.writeFileSync(path.join(root,'Tests/presentation-parity.json'),JSON.stringify({commit:provenance.commit,cases,rules})+'\n');
console.log(JSON.stringify({poses:cases.length,ruleCases:rules.length,filesVerified:provenance.files.length}));
