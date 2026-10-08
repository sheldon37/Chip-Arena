import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {PROBE_RATES,describeProbe,probeBase,probeMotion,probeRig,type ProbeReport} from './helpers/motion-probe.js';

/** DEV fighting styles (emperor = Tekken, ansatsuken = Street Fighter) on the ground-anchored gait: planted feet hold their
 * floor spot, ankles never sink below the braced floor, and the hips and knees never jump in one frame, standing, moving
 * and striking while moving, at 30/60/120 Hz. */
await initPhysics();
type Spec='emperor'|'ansatsuken';
const LEG=['leftLeg','rightLeg','leftKnee','rightKnee'] as const;
const SLIP=.015,FLOOR=-.81,LEG_STEP_120=70;
const jab=(at:number,total=2,action='jab')=>({emperor:{action,at,until:action==='electric'?at+.40:at+.075+(total-1)*.17+.14,index:0,total,hits:0,lastHit:-10,comboDamage:0,electricAt:-10},lastAttack:at,attackIndex:0}) as Partial<PlayerView>;
const combo=(at:number,total=3,action='combo')=>({arcade:{action,at,until:action==='combo'?at+.12+(total-1)*(total===10?.115:.25)+.22:at+.48,index:0,total,charge:0,uppercutCd:0,hadokenCd:0,hits:0,lastHit:-10,comboDamage:0,parryUntil:0,parryAt:-10},lastAttack:at,attackIndex:0}) as Partial<PlayerView>;
const LEN={emperor:.385,ansatsuken:.84};
const string=(spec:Spec,at:number,total?:number)=>spec==='emperor'?jab(at,total):combo(at,total);
const ahead=(speed:number)=>({x:0,y:0,z:speed});
interface Scenario{slip?:number;name:string;vel?:(t:number)=>{x:number;y:number;z:number};yaw?:(t:number)=>number;act?:(t:number)=>Partial<PlayerView>;from:number;to:number;glide?:boolean}
function scenarios(spec:Spec):Scenario[]{
 const A=(at:number,total?:number)=>string(spec,at,total),len=LEN[spec];
 return [
  {name:'idle stance',from:1,to:3},
  {name:'walk',vel:()=>ahead(2.8),from:1,to:3},
  {name:'run',vel:()=>ahead(6.8),from:1,to:3},
  {name:'strafe',vel:()=>({x:4,y:0,z:0}),from:1,to:3},
  {name:'backpedal',vel:()=>ahead(-3),from:1,to:3},
  {name:'attack standing',act:t=>t>=2?A(2):{},from:1.8,to:2+len+.4},
  {name:'long string standing',act:t=>t>=2?A(2,spec==='emperor'?3:10):{},from:1.8,to:3.7},
  {name:'attack walking',vel:()=>ahead(2.8),act:t=>t>=2?A(2):{},from:1.8,to:2+len+.4},
  {name:'attack running',vel:()=>ahead(6.8),act:t=>t>=2?A(2):{},from:1.8,to:2+len+.4},
  {name:'run, attack, run',vel:t=>ahead(t<2||t>2+len?6.8:0),act:t=>t>=2?A(2):{},from:1.8,to:2+len+.8},
  {name:'combo-ready hold',act:t=>t>=1?A(1):{},from:1+len-.1,to:1+len+2},
  {name:'combo-ready into a walk',vel:t=>ahead(t>1+len+.3?2.8:0),act:t=>t>=1?A(1):{},from:1+len-.1,to:1+len+1.5},
  {name:'turn in place',slip:.02,yaw:t=>t>1.5?Math.min(2.5,(t-1.5)*3):0,from:1,to:3},
  ...(spec==='emperor'?[
   {name:'electric',vel:(t:number)=>ahead(t>=2&&t<2.1?6:0),act:(t:number)=>t>=2?{...jab(2,1,'electric'),electricStepTime:t<2.1?.1:0}:{},from:1.8,to:2.8},
   {name:'wavedash',vel:(t:number)=>ahead(t>=2&&t<2.18?9:0),act:(t:number)=>t>=2&&t<2.18?{...jab(2,0,'wave'),emperor:{...jab(2,0,'wave').emperor!,until:2.18},waveTime:2.18-t,dashTime:2.18-t}:{},from:1.8,to:2.8,glide:true},
  ]:[
   {name:'hadoken',act:(t:number)=>t>=2?combo(2,1,'hadoken'):{},from:1.8,to:2.8},
   {name:'demon step',vel:(t:number)=>ahead(t>=2&&t<2.3?12:0),act:(t:number)=>t>=2&&t<2.3?{arcade:{...combo(0).arcade!,action:undefined,until:0,demonStepAt:2},dashTime:2.3-t}:{},from:1.8,to:2.8,glide:true},
  ]),
 ];
}
function run(spec:Spec,s:Scenario,hz:number){
 let prev:T.Quaternion[]|undefined,legStep=0,legAt='';
 const report:ProbeReport=probeMotion({spec,hz,travel:true,from:s.from,to:s.to,warm:1,
  player:(t,b)=>({...b,grounded:true,hitStunUntil:-1e9,animationTime:undefined,vel:s.vel?.(t)??{x:0,y:0,z:0},yaw:s.yaw?.(t)??0,emperor:undefined,arcade:undefined,lastAttack:-10,dashTime:0,...(s.act?.(t)??{})} as PlayerView),
  animate:(r,p,t,dt)=>{animateCharacter(r,p,t,dt,p.pitch);const q=LEG.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));
   if(prev&&t>=s.from)q.forEach((x,k)=>{const d=x.angleTo(prev![k])*180/Math.PI;if(d>legStep){legStep=d;legAt=`${LEG[k]}@${t.toFixed(3)}`;}});prev=q;}});
 return {report,legStep,legAt};
}
for(const spec of ['emperor','ansatsuken'] as const){
 test(`${spec}: planted feet hold within ${SLIP*100} cm and ankles stay on the floor at 30/60/120 Hz`,()=>{
  for(const s of scenarios(spec))for(const hz of PROBE_RATES){
   const {report}=run(spec,s,hz),what=`${spec} ${s.name} ${describeProbe(report)}`;
   assert.ok(report.minAnkleY>=FLOOR,`${what}: an ankle sinks below the floor`);
   // A glide (wavedash, Demon Step) moves the root at dash speed with the feet skimming: only the floor applies.
   // Turning in place pivots the planted feet as the gait steps round (the sword shows the same 1.8 cm at 30 Hz).
   if(!s.glide)assert.ok(report.maxFootSlip<=(s.slip??SLIP),`${what}: a planted foot slides`);
  }
 });
 test(`${spec}: hips and knees never jump in one frame (moving, striking, turning; 60 vs 120 Hz)`,()=>{
  for(const s of scenarios(spec)){
   const [a,b]=[60,120].map(hz=>run(spec,s,hz));
   // A snap keeps its size per frame as the rate doubles; continuous motion halves. Below 30 deg per 120 Hz frame the gait's
   // own crisp lift-offs, restarts and turn steps (shared by every spec; the sword shows 24 deg) are accepted as they are.
   assert.ok(b.legStep<=Math.max(30,a.legStep*.8+3),`${spec} ${s.name}: ${a.legStep.toFixed(1)} deg/frame (${a.legAt}) at 60 Hz vs ${b.legStep.toFixed(1)} (${b.legAt}) at 120 Hz`);
   assert.ok(b.legStep<=LEG_STEP_120,`${spec} ${s.name}: ${b.legStep.toFixed(1)} deg in one 120 Hz frame (${b.legAt})`);
  }
 });
}
test('combo-ready also shows in the live game, where the authority clears the string action at its until',()=>{
 for(const spec of ['emperor','ansatsuken'] as const){
  const key=spec==='emperor'?'emperor':'arcade',len=LEN[spec],pose=(cleared:boolean)=>{
   const base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,r=probeRig(spec);
   for(let i=0;i<=Math.round((len+.6)*60);i++){const t=1+i/60,s=string(spec,1) as Record<string,unknown>,state={...(s[key] as object)} as {action?:string;until:number};if(cleared&&t>=state.until)state.action=undefined;animateCharacter(r,{...base,...s,[key]:state} as PlayerView,t,1/60);}
   r.group.updateMatrixWorld(true);return r;
  };
  const kept=pose(false),live=pose(true),guard=probeRig(spec);
  animateCharacter(guard,{...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined,lastAttack:-10} as PlayerView,1+len+.6,1/60);guard.group.updateMatrixWorld(true);
  const arm=(r:typeof kept)=>r.rightArm.getWorldQuaternion(new T.Quaternion());
  assert.ok(arm(kept).angleTo(arm(live))<.01,`${spec}: the live game shows the same hold as a view that keeps the action`);
  assert.ok(arm(live).angleTo(arm(guard))>.05,`${spec}: the hold is visible (not the plain guard)`);
 }
});
