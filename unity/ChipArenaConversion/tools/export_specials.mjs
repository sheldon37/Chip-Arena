// Pure source functions and StandardMelee.start with a data-only adapter; no Rapier/PhysX claim.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {stripTypeScriptTypes} from 'node:module';
import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const lock=JSON.parse(fs.readFileSync(path.join(root,'tools/source-lock.json'),'utf8'));
const extra=[{path:'src/client/melee-motion.ts',git_blob_sha:'6c7ea0a5da4118897b52139686885a5383c63c26'},{path:'src/client/knockdown-motion.ts',git_blob_sha:'7f628d9537a28f434cc0da97f987c6fb883dc998'}];
const paths=['config','souls-combat','standard-melee','recovery','projectiles','ceiling-field','arena'].map(p=>'src/shared/'+p+'.ts').concat(extra.map(x=>x.path));
const files=paths.map(p=>{const receipt=[...lock.files,...extra].find(f=>f.path===p);if(!receipt)throw Error('Missing receipt '+p);const b=fs.readFileSync(path.join(source,p));if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==receipt.git_blob_sha)throw Error('Source mismatch '+p);return {...receipt,size:b.length};});
const read=p=>fs.readFileSync(path.join(source,p),'utf8'),shared=p=>read('src/shared/'+p+'.ts'),stripImports=s=>s.replace(/^import .*;\n/gm,'');
const content=JSON.parse(fs.readFileSync(path.join(root,'Assets/Conversion/Resources/GameContent.json'),'utf8'));
const standard=shared('standard-melee'),down=read('src/client/knockdown-motion.ts'),motion=read('src/client/melee-motion.ts');
const phase=down.slice(down.indexOf('export const KNOCKDOWN_LANDING='),down.indexOf('const sat='));
const smooth=down.split('\n').find(l=>l.startsWith('const sat='));
const envelope=motion.slice(motion.indexOf(' const a=p.meleeAction!,age='),motion.indexOf(' const blend='));
const filter=standard.slice(standard.indexOf('   if(len(d)>range+extra'),standard.indexOf('   const hp=target.hp'));
if(!filter.includes("kind==='launch'?.45:.15")||!envelope.includes('drift=ease'))throw Error('Source extraction changed');
const arena=shared('arena').split('\n').find(l=>l.startsWith('export const ARENA='));
const adapter=`const C=CONFIG,WEAPONS=${JSON.stringify(Object.fromEntries(content.weapons.map(w=>[w.id,w])))};
export function startFixture(spec,rate,resource,grounded,busy,committedAction,projectiles){const s={time:2,projectiles:{size:projectiles},uid:()=> 'fixture',spendResource:(p,cost)=>{if(resource<cost)return false;resource-=cost;return true;},emit:()=>{}};const p={spec,stats:{attackRate:rate},input:{yaw:.7},grounded,body:{translation:()=>v()},meleeAction:busy?{until:3}:undefined,attackCommitment:committedAction?{until:3}:undefined};new StandardMelee(s).start(p);return {started:!!p.meleeAction?.kind,resource,release:p.meleeAction?.releaseAt??0,until:p.meleeAction?.until??0,cooldown:p.cooldown??0};}
export function strikeFixture(d,range,extra,yaw,kind){const f=forward(yaw),s={lineOfSight:()=>true},p={id:0},target={id:1,body:{translation:()=>d}},origin=v();${filter.replace('continue;','return false;')}return true;}
const ease=sm;export function envelopeFixture(action,time){const p={meleeAction:action};${envelope}return {coil,drive:d,recovery,weight};}`;
const code=shared('config')+stripImports(shared('souls-combat'))+stripImports(shared('recovery'))+stripImports(shared('projectiles'))+arena+stripImports(shared('ceiling-field'))+stripImports(standard)+phase+smooth+adapter;
const mod=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(code,{mode:'transform'})).toString('base64'));
const starts=[],strikes=[],recoveries=[],flights=[],ceilings=[],envelopes=[];
for(const spec of ['sword','daggers','greatsword','shield'])for(const rate of [.5,1,1.5,3])for(const gate of ['ok','resource','air','busy','commit','cap']){
 const resource=gate==='resource'?0:100,grounded=gate!=='air',busy=gate==='busy',committed=gate==='commit',projectiles=gate==='cap'?100:0;
 starts.push({spec,rate,resource,grounded,busy,committed,projectiles,...Object.fromEntries(Object.entries(mod.startFixture(spec,rate,resource,grounded,busy,committed,projectiles)).map(([k,v])=>[k==='resource'?'remaining':k,v]))});
}
for(const kind of ['launch','spin-direct','slam'])for(const yaw of [0,.7,-2])for(const range of [1.75,2.5,3.7])for(const delta of [{x:0,y:0,z:1},{x:1,y:0,z:1},{x:-1,y:0,z:-1},{x:0,y:3,z:1},{x:0,y:0,z:8},{x:0,y:1,z:0}])for(const extra of [.45,.8])strikes.push({kind,yaw,range,delta,extra,hit:mod.strikeFixture(delta,range,extra,yaw,kind)});
for(const used of [false,true])for(const move of [{x:0,y:0,z:0},{x:.1,y:0,z:0},{x:1,y:0,z:-1}])for(const yaw of [0,.7,Math.PI]){
 const k={at:1,until:6,pickupUsed:used};mod.landRecovery(k,2);const samples=[];
 for(const time of [1.99,2.001,2.19,2.219,2.221,2.399,2.401,2.6,2.799,2.801,2.999,3.001]){
  const m=mod.recoveryMotion(k,time,{...move,yaw});samples.push({time,pickup:mod.pickupOpen(k,time),protected:mod.downProtected(k,time),phase:mod.knockdownPhase(k,time),motion:m??{x:0,y:0,z:0}});
 }
 recoveries.push({used,move,yaw,landed:k.landedAt,pickup:k.pickupUntil,roll:k.rollAt,rollEnd:k.rollUntil,until:k.until,samples});
}
for(const velocity of [{x:0,y:12,z:30},{x:3,y:-5,z:7},{x:0,y:0,z:24}])for(const gravity of [0,9,18,36])for(const dt of [1/60,.005,.03])flights.push({initial:velocity,gravity,dt,...mod.ballisticStep(velocity,gravity,dt)});
for(const position of [{x:0,y:23.9,z:0},{x:0,y:24.5,z:0},{x:33,y:24,z:0},{x:0,y:20,z:0}])for(const delta of [{x:0,y:1,z:0},{x:0,y:-1,z:0},{x:1,y:0,z:0}])for(const radius of [.095,.28]){const hit=mod.ceilingContact(position,delta,radius);ceilings.push({position,delta,radius,hit:!!hit,fraction:hit?.fraction??0,center:hit?.center??{x:0,y:0,z:0}});}
for(const spec of ['sword','daggers','shield','greatsword'])for(const rate of [.5,1,3]){
 const info=mod.MELEE_SPECIALS[spec],a={kind:info.kind,at:2,releaseAt:2+info.windup/rate,until:2+(spec==='greatsword'?3.5:(info.windup+info.recover)/rate)};
 for(const age of [0,.025,.08,.15,.25,.35,.5,.8,1]){const time=2+age;envelopes.push({spec,rate,time,...mod.envelopeFixture(a,time)});}
}
fs.writeFileSync(path.join(root,'Tests/specials-parity.json'),JSON.stringify({commit:lock.commit,starts,strikes,recoveries,flights,ceilings,envelopes})+'\n');
fs.writeFileSync(path.join(root,'tools/specials-provenance.json'),JSON.stringify({commit:lock.commit,files},null,2)+'\n');
console.log(JSON.stringify(Object.fromEntries(Object.entries({starts,strikes,recoveries,flights,ceilings,envelopes}).map(([k,v])=>[k,v.length]))));
