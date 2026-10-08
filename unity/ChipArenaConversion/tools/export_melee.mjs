// Evaluate pinned original functions/expressions. This is not a PhysX or Rapier test.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {stripTypeScriptTypes} from 'node:module';
import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const lock=JSON.parse(fs.readFileSync(path.join(root,'tools/source-lock.json'),'utf8'));
const paths=['src/shared/config.ts','src/shared/souls-combat.ts','src/shared/blocking.ts','src/shared/standard-melee.ts','src/shared/simulation.ts','src/shared/movement.ts'];
const files=paths.map(p=>{const receipt=lock.files.find(f=>f.path===p);if(!receipt)throw Error('Missing source receipt '+p);const b=fs.readFileSync(path.join(source,p));if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==receipt.git_blob_sha)throw Error('Source mismatch '+p);return receipt;});
const read=p=>fs.readFileSync(path.join(source,'src/shared',p),'utf8');
const content=JSON.parse(fs.readFileSync(path.join(root,'Assets/Conversion/Resources/GameContent.json'),'utf8'));
const stripImports=s=>s.replace(/^import .*;\n/gm,'');
const config=read('config.ts'),souls=read('souls-combat.ts'),blocking=read('blocking.ts'),sim=read('simulation.ts'),movement=read('movement.ts');
// Extract the actual melee sweep expression and target filter without constructing Simulation/Rapier.
const angle=sim.slice(sim.indexOf('const progress=clamp((this.time-swing.at)'),sim.indexOf('// A moving capsule-like'));
const filter=sim.slice(sim.indexOf('const d=sub(target.body.translation(),origin),dist=len(d)'),sim.indexOf('if(!this.lineOfSight(origin,target.body.translation(),p.id,target.id))continue;',sim.indexOf('const d=sub(target.body.translation(),origin),dist=len(d)')));
if(!angle.includes('Math.min(Math.PI*2')||!filter.endsWith('continue;'))throw Error('Original sweep extraction changed');
const pair=read('standard-melee.ts').split('\n').find(l=>l.startsWith('export function daggerPairStun('));
const external=movement.slice(movement.indexOf(' const lift=clamp(state.ext.y'),movement.indexOf(' let vy=body.linvel().y'));
const mechanics=`const C=CONFIG;const WEAPONS=${JSON.stringify(Object.fromEntries(content.weapons.map(w=>[w.id,w])))};
export function sweep(spec,index,time,at,until,yaw,arc,range,delta,size){const p={spec,attackIndex:index,stats:{meleeArc:arc}},w=WEAPONS[spec],swing={at,until,yaw};${angle.replaceAll('this.time','time')}const dir=forward(angle),origin=v(),target={stats:{},body:{translation:()=>delta}};${filter.replace("extra='stats'in target?.45:Math.min(.8,len(target.size)*.3)",'extra=size').replace('continue;','return {angle,hit:false};')}return {angle,hit:true};}
export function externalFixture(ext,move,dt){const state={ext:{...ext},move:{...move},dashTime:0},stats={};const roll=undefined;${external}return {remaining:state.ext,lift,controlled};}`;
const module=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(config+stripImports(souls)+stripImports(blocking)+pair+mechanics,{mode:'transform'})).toString('base64'));
const stats=Object.fromEntries(content.baseStats.map(x=>[x.name,x.value]));const swings=[],blocks=[],externals=[];
for(const spec of ['sword','daggers','greatsword','shield'])for(const rate of [.5,1,1.5,3])for(const index of [1,2,3,4]){
 const w=content.weapons.find(w=>w.id===spec),at=2,yaw=.7,wind=w.windup/rate,active=.16/rate,a=module.attackCommitment(spec,at,wind,active,rate,w.interval/rate);a.yaw=yaw;
 const samples=[];for(const age of [-.01,wind*.3,wind+.001,wind+active*.5,wind+active-.001,wind+active+.01,a.until-at+.1]){
  const time=Math.fround(at+age),step=module.attackStep(a,time,1/60),out=module.sweep(spec,index,time,at+wind,at+wind+active,yaw,1,w.range,{x:0,y:0,z:1},.45);
  const contacts=[];for(const delta of [{x:0,y:0,z:1},{x:1,y:0,z:1},{x:-1,y:0,z:1},{x:0,y:0,z:-1},{x:0,y:3,z:1},{x:0,y:0,z:20}])for(const extra of [.45,.8]){const hit=time>=at+wind&&time<=at+wind+active&&module.sweep(spec,index,time,at+wind,at+wind+active,yaw,1,w.range,delta,extra).hit;contacts.push({delta,extra,hit});}
  samples.push({time,step,before:module.beforeStrike(a,time),angle:out.angle,contacts});
 }
 swings.push({spec,rate,index,at,yaw,hitAt:at+wind,hitUntil:at+wind+active,until:a.until,stepAt:a.stepAt,stepUntil:a.stepUntil,distance:a.distance,stun:spec==='daggers'?module.daggerPairStun(rate):module.meleeHitStun(spec),samples});
}
for(const damage of [0,23,30,100,1000])for(const impulse of [0,610,10000])for(const ready of [0,.2,2])for(const travel of [0,.7])blocks.push({damage,impulse,ready,travel,stun:module.meleeBlockStun(damage,impulse),punish:module.meleeBlockPunish(damage,ready,travel)});
for(const ext of [{x:5,y:4,z:0},{x:100,y:30,z:-100},{x:0,y:-40,z:0}])for(const move of [{x:-6.8,y:0,z:0},{x:0,y:0,z:6.8},{x:0,y:0,z:0}])externals.push({ext,move,...module.externalFixture(ext,move,1/60)});
const windows=[-1,0,.08,5].map(window=>({window,duration:module.blockDuration({...stats,blockWindow:window})}));
fs.writeFileSync(path.join(root,'Tests/melee-parity.json'),JSON.stringify({commit:lock.commit,swings,blocks,externals,windows})+'\n');
fs.writeFileSync(path.join(root,'tools/melee-provenance.json'),JSON.stringify({commit:lock.commit,files},null,2)+'\n');
console.log(JSON.stringify({swings:swings.length,samples:swings.reduce((n,s)=>n+s.samples.length,0),contacts:swings.reduce((n,s)=>n+s.samples.reduce((k,x)=>k+x.contacts.length,0),0),blocks:blocks.length,externals:externals.length,files:files.length}));
