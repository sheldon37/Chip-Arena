import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {fighterReady} from '../src/client/fighter-motion.js';
import {probeBase,probeRig} from './helpers/motion-probe.js';

await initPhysics();
type Spec='emperor'|'ansatsuken';
const length={emperor:.385,ansatsuken:.84};
function player(spec:Spec,at=1):PlayerView{
 const base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:0,animationTime:undefined,lastAttack:at,lastSecondary:-10,lastBlock:-10};
 return {...base,...(spec==='emperor'?{emperor:{action:'jab',at,until:at+length[spec],index:1,total:2,hits:0,lastHit:-10,comboDamage:0,electricAt:-10}}:
  {arcade:{action:'combo',at,until:at+length[spec],index:2,total:3,charge:0,uppercutCd:0,hadokenCd:0,hits:0,lastHit:-10,comboDamage:0,parryUntil:0,parryAt:-10}})} as PlayerView;
}
function clear(p:PlayerView):PlayerView{return {...p,emperor:p.emperor?{...p.emperor,action:undefined}:undefined,arcade:p.arcade?{...p.arcade,action:undefined}:undefined};}
const weight=(r:ReturnType<typeof probeRig>,p:PlayerView,t:number)=>fighterReady(r,p,t,p.spec==='emperor'?p.emperor:p.arcade,p.spec==='emperor'?'jab':'combo',p.spec==='emperor'?'emperor':'karate',0);

for(const spec of ['emperor','ansatsuken'] as const){
 test(`${spec}: recovery reaches the next chamber without an idle dip; cleared snapshots keep it for 2.4 seconds`,()=>{
  const p=player(spec),end=1+length[spec],r=probeRig(spec);
  animateCharacter(r,p,1,1/60);
  assert.ok(weight(r,p,end-.001)>.99,'the last recovery frame is already chambered');
  assert.equal(weight(r,clear(p),end),1,'clearing the action must not drop the chamber');
  assert.equal(weight(r,clear(p),end+2.3),1,'same hold duration as normal melee');
  assert.ok(weight(r,clear(p),end+2.8)>.4&&weight(r,clear(p),end+2.8)<.6,'timeout fades');
  assert.equal(weight(r,clear(p),end+3.21),0,'relaxed after timeout');
 });

 test(`${spec}: a held chamber flows into the next string at normal and chip-modified attack rates`,()=>{
  for(const rate of [.7,1,2]){
   const p=player(spec),key=spec==='emperor'?'emperor':'arcade';p.attackRate=rate;
   p[key]!.until=1+length[spec]/rate;const end=p[key]!.until,t=end+.4,r=probeRig(spec);
   weight(r,p,end-.01);assert.equal(weight(r,clear(p),t-.01),1);
   const next=player(spec,t);next.attackRate=rate;next[key]!.until=t+length[spec]/rate;
   assert.equal(weight(r,next,t),1,'new string inherits the displayed chamber');
   assert.ok(weight(r,next,t+.05)>.4,'first coil eases out of that chamber');
  }
 });

 test(`${spec}: a hit during recovery, a block, and a dodge permanently release the old hold`,()=>{
  const p=player(spec),end=1+length[spec];
  const hit={kind:'hit',at:end-.04,until:end+.20,zone:'torso',turn:.2,strength:.5,yaw:0,direction:{x:0,y:0,z:1},point:{x:0,y:1,z:0}} as const;
  const roll={at:end+.2,until:end+.7,invulnerableAt:end+.275,invulnerableUntil:end+.455,direction:{x:0,y:0,z:1},distance:3.2};
  const exits:[string,number,number,(p:PlayerView)=>PlayerView][]=[
   ['hit before string end',hit.at,hit.until,q=>({...q,hitReaction:hit,hitStunUntil:hit.until})],
   ['block',end+.2,end+.45,q=>({...q,blockTime:.25,lastBlock:end+.2})],
   spec==='emperor'?['roll',roll.at,roll.until,q=>({...q,roll})]:
    ['Demon Step',end+.2,end+.5,q=>({...q,dashTime:.3,arcade:{...q.arcade!,demonStepAt:end+.2}})],
  ];
  for(const [name,start,stop,patch] of exits){
   const r=probeRig(spec);
   for(let i=0;i<=Math.ceil((stop+.4)*120);i++){
    const t=i/120;let q=t>=end?clear(p):p;
    if(t>=start&&t<stop)q=patch(q);
    // Exit fields intentionally disappear after the action, as can happen on later snapshots.
    animateCharacter(r,q,t,1/120);
   }
   assert.equal(weight(r,clear(p),stop+.5),0,`${name}: old hold must not reappear`);
   const next=player(spec,stop+.6);weight(r,next,stop+.6);
   assert.equal(weight(r,clear(next),stop+.6+length[spec]+.2),1,'a new string can hold again');
  }
 });

 test(`${spec}: canceled, rewound, dead, or reused rigs cannot inherit an old combo`,()=>{
  const p=player(spec),t=1+length[spec]+.3;
  for(const variant of ['cancel','rewind','death','identity'] as const){
   const r=probeRig(spec);weight(r,p,t);
   let q=clear(p),now=t+.01;
   if(variant==='cancel'){q=structuredClone(q);(q.emperor??q.arcade)!.until=0;}
   if(variant==='rewind')now=t-.1;
   if(variant==='death')q={...q,hp:0};
   if(variant==='identity')q={...q,id:'another-player'};
   assert.equal(weight(r,q,now),0,variant);
  }
 });
}

test('both dev styles visibly hold the chamber at the end of a live string',()=>{
 for(const spec of ['emperor','ansatsuken'] as const){
  const p=player(spec),end=1+length[spec],r=probeRig(spec),guard=probeRig(spec);
  for(let i=0;i<=Math.ceil(end*120);i++){
   const t=i/120;animateCharacter(r,t>=end?clear(p):p,t,1/120);
   animateCharacter(guard,{...p,lastAttack:-10,emperor:undefined,arcade:undefined},t,1/120);
  }
  const arm=spec==='emperor'?'leftElbow':'rightArm';
  assert.ok(r[arm].getWorldQuaternion(new T.Quaternion()).angleTo(guard[arm].getWorldQuaternion(new T.Quaternion()))>.15,`${spec}: no idle-guard detour`);
 }
});
