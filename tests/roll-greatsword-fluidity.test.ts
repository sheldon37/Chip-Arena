import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {attackCommitment,attackTravel,meleeRecovery} from '../src/shared/souls-combat.js';
import {animateCharacter,attackPose} from '../src/client/character-animation.js';
import {lowestShell,ROLL_FLOOR} from '../src/client/dodge-roll-motion.js';
import {comboReadyMotion,primaryMeleeMotion} from '../src/client/primary-melee-motion.js';
import {animateEmperor} from '../src/client/emperor-animation.js';
import {animateAnsatsuken} from '../src/client/ansatsuken-animation.js';
import {PROBE_RATES,probeBase,probeMotion,probeRig,weaponTip} from './helpers/motion-probe.js';

await initPhysics();
type Rig=Parameters<typeof lowestShell>[0];
const box=new T.Box3();
/** Lowest point (rig-group height) of the meshes whose names match `parts`. */
function lowest(r:Rig,parts:RegExp){let min=Infinity;r.body.traverse(o=>{if((o as T.Mesh).isMesh&&parts.test(o.name)){box.setFromObject(o);min=Math.min(min,box.min.y-r.group.position.y);}});return min;}
const LIMBS=/-(foot|heel|palm|fingers|thumb)$|^(blank-head|crown)$/;
const roll=(x:number,z:number,at=1.5,until=2)=>({at,until,invulnerableAt:at+.08,invulnerableUntil:at+.34,direction:{x,y:0,z},distance:3.2});
/** Samples of a roll at `hz`: u, the lowest body-shell point and the lowest foot/hand/head point relative to the sole plane. */
function sampleRoll(spec:string,x:number,z:number,hz:number){
 const R=roll(x,z),out:{t:number;u:number;shell:number;limb:number;body:number}[]=[];
 probeMotion({spec,mode:'idle',from:1.2,to:2.1,hz,travel:true,patch:()=>({roll:R}),animate:(r,p,t,dt)=>{animateCharacter(r,p,t,dt,p.pitch);r.group.updateMatrixWorld(true);
  const u=(t-R.at)/(R.until-R.at);if(u>=0&&u<1)out.push({t,u,shell:lowestShell(r)-ROLL_FLOOR,limb:lowest(r,LIMBS)-ROLL_FLOOR,body:r.body.position.y});}});
 return out;
}
const DIRS=[[0,1],[1,0],[-1,0],[0,-1],[.7,.7],[-.7,-.7]] as const;

test('the dodge roll hugs the floor: the tucked body touches it within 3 cm and no hand, foot or head goes through it',()=>{
 for(const spec of ['sword','greatsword'])for(const [x,z] of DIRS){
  const s=sampleRoll(spec,x,z,60),at=`${spec} roll (${x},${z})`;
  // Baseline (before the floor solve): the tucked body pivoted about the hips and floated up to 50 cm above the floor.
  for(const f of s.filter(f=>f.u>=.3&&f.u<=.62))assert.ok(Math.abs(f.shell)<.03,`${at} u=${f.u.toFixed(2)}: lowest body part ${(f.shell*100).toFixed(1)} cm from the floor`);
  for(const f of s){assert.ok(f.limb>-.015,`${at} u=${f.u.toFixed(2)}: a hand/foot/head ${(f.limb*100).toFixed(1)} cm under the floor`);assert.ok(f.shell>-.025,`${at} u=${f.u.toFixed(2)}: body ${(f.shell*100).toFixed(1)} cm under the floor`);}
  // The dive lowers the hips well below standing height before the tuck (a dip, not a hop).
  assert.ok(Math.min(...s.filter(f=>f.u<.3).map(f=>f.body))<-.2,`${at}: dips into the roll`);
 }
});

test('the roll is rate independent: the same authoritative phase gives the same contact at 30, 60 and 120 Hz',()=>{
 for(const [x,z] of [[0,1],[1,0],[0,-1]] as const){
  const runs=PROBE_RATES.map(hz=>new Map(sampleRoll('sword',x,z,hz).map(f=>[Math.round(f.t*120),f])));
  let compared=0;
  for(const [key,f] of runs[0]){const others=runs.slice(1).map(m=>m.get(key));if(others.some(o=>!o)||f.u<.1)continue;compared++;
   for(const o of others){assert.ok(Math.abs(o!.body-f.body)<.012,`(${x},${z}) u=${f.u.toFixed(2)}: body height ${f.body.toFixed(3)} vs ${o!.body.toFixed(3)}`);assert.ok(Math.abs(o!.shell-f.shell)<.012,`(${x},${z}) u=${f.u.toFixed(2)}: contact`);}}
  assert.ok(compared>8,'compared the shared 30 Hz frames');
 }
});

// ---- Greatsword kinetic chain and smooth arcs ----
const yawOf=(o:T.Object3D)=>{const f=new T.Vector3(0,0,1).applyQuaternion(o.getWorldQuaternion(new T.Quaternion()));return Math.atan2(f.x,f.z);};
/** A standing greatsword cut at t=2 (root follows the authored travel); calls `each` after every frame from the attack on. */
function greatswordCut(index:number,hz:number,each:(r:Rig,age:number,p:PlayerView)=>void){
 const spec='greatsword',base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,w=WEAPONS.greatsword,la=2,c=attackCommitment(base.spec,la,w.windup,.16,1,w.windup+.16);
 const idle={...base,lastAttack:-10,swingAt:undefined,swingUntil:undefined} as PlayerView,p={...base,attackIndex:index,lastAttack:la,swingAt:la+w.windup,swingUntil:la+w.windup+.16,attackCommitment:c} as PlayerView,r=probeRig(spec);
 for(let i=0;;i++){const t=1.9+i/hz;if(t>c.until)break;r.group.position.set(0,0,attackTravel(c,t));r.group.rotation.y=0;animateCharacter(r,t<la?idle:p,t,1/hz);r.group.updateMatrixWorld(true);if(t>=la)each(r,t-la,p);}
}
test('greatsword kinetic chain: the chest yaw trails the pelvis yaw by at least 40 ms through every cut',()=>{
 for(const index of [1,2,3]){const hz=240,P:number[]=[],C:number[]=[];greatswordCut(index,hz,r=>{P.push(yawOf(r.pelvis));C.push(yawOf(r.chest));});
  const vel=(a:number[])=>a.slice(1).map((x,i)=>x-a[i]),vp=vel(P),vc=vel(C);let best=0,err=Infinity;
  // The shift that best lines the chest's yaw velocity up with the pelvis's (baseline: 13 ms, hips and chest turned together).
  for(let s=-40;s<=40;s++){let e=0,n=0;for(let i=0;i<vp.length;i++){const j=i+s;if(j<0||j>=vc.length)continue;e+=(vc[j]-vp[i])**2;n++;}e/=n;if(e<err){err=e;best=s;}}
  assert.ok(best/hz>=.04,`cut ${index}: chest trails the pelvis by ${(best/hz*1000).toFixed(0)} ms`);
 }
});
test('greatsword blade tip follows smooth arcs: no velocity kinks (per-frame velocity jumps) from windup to recovery',()=>{
 for(const index of [1,2]){
  const jerk=[120,240].map(hz=>{const tips:T.Vector3[]=[];greatswordCut(index,hz,(r,age,p)=>{if(age>=.1)tips.push(weaponTip(r,p).sub(r.group.position));});
   const v=tips.slice(1).map((x,i)=>x.clone().sub(tips[i]).multiplyScalar(hz)),raw=v.slice(1).map((x,i)=>x.distanceTo(v[i])*hz),acc=raw.slice(2).map((a,i)=>(a+raw[i]+raw[i+1])/3);
   return Math.max(...acc.slice(1).map((a,i)=>Math.abs(a-acc[i])));});
  // A velocity kink is an acceleration spike that grows with the rate (also over a 3-frame average), so its frame-to-frame jump
  // roughly doubles from 120 to 240 Hz (baseline: the phase speed stepped at swingUntil and every key reversal stopped dead); a smooth arc's
  // stays level.
  assert.ok(jerk[1]<=1.3*jerk[0],`cut ${index}: acceleration jumps ${jerk[0].toFixed(0)} (120 Hz) vs ${jerk[1].toFixed(0)} (240 Hz)`);
 }
});

// ---- Combo-ready holds ----
const COMBO_BONES=['body','pelvis','spine','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','weapon','offWeapon'] as const;
/** Upper body: in the hold the legs belong to the ground pass (the feet are back on the guard stance). */
const UPPER=COMBO_BONES.filter(b=>!/Leg|Knee/.test(b));
const attackAt=(spec:string,at:number,index:number)=>{const w=WEAPONS[spec as 'sword'];return {attackIndex:index,lastAttack:at,swingAt:at+w.windup,swingUntil:at+w.windup+.16,attackCommitment:attackCommitment(spec as PlayerView['spec'],at,w.windup,.16,1,w.windup+.16),attackRate:1};};
const rest=(r:Rig,from:Rig)=>{const a:T.Object3D[]=[],b:T.Object3D[]=[];from.group.traverse(o=>a.push(o));r.group.traverse(o=>b.push(o));a.forEach((o,i)=>{b[i].position.copy(o.position);b[i].quaternion.copy(o.quaternion);});};
test('combo-ready: each cut ends in exactly the pose the next cut starts from',()=>{
 for(const spec of ['greatsword','sword','daggers','shield'])for(let index=1;index<=4;index++){
  const base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9} as PlayerView,w=WEAPONS[spec as 'sword'],end=1+w.windup+.16+meleeRecovery(spec as PlayerView['spec']),t=end+.4;
  const p={...base,...attackAt(spec,1,index)} as PlayerView,next={...base,...attackAt(spec,t,index+1)} as PlayerView,fresh=probeRig(spec);
  // The hold of cut N (at full weight) on one rig; on another, the same hold recorded, then cut N+1's first frame from rest.
  const held=probeRig(spec),entered=probeRig(spec);
  comboReadyMotion(held,p,attackPose(p,t-.01),t-.01,0);comboReadyMotion(entered,p,attackPose(p,t-.01),t-.01,0);rest(entered,fresh);
  primaryMeleeMotion(entered,next,attackPose(next,t));
  for(const r of [held,entered])r.group.updateMatrixWorld(true);
  let worst=0,at='';for(const b of UPPER){const d=held[b].getWorldQuaternion(new T.Quaternion()).angleTo(entered[b].getWorldQuaternion(new T.Quaternion()));if(d>worst){worst=d;at=b;}}
  assert.ok(worst<.035,`${spec} ${index} -> ${index+1}: ${at} differs by ${(worst*180/Math.PI).toFixed(1)} deg between the hold and the next cut's start`);
  assert.ok(weaponTip(held,p).distanceTo(weaponTip(entered,p))<.03,`${spec} ${index} -> ${index+1}: blade tip`);
  // The hold is a chamber, not the guard: the cut ends away from where a fresh cut starts.
  const guard=probeRig(spec);primaryMeleeMotion(guard,next,attackPose(next,t));guard.group.updateMatrixWorld(true);
  if(spec==='greatsword')assert.ok(weaponTip(guard,p).distanceTo(weaponTip(held,p))>.15,`${spec} ${index}: the hold is chambered for the next cut`);
 }
});
/** Largest per-frame world rotation (deg) of the combo bones over [from,to] at `hz`, for a view timeline. */
function steps(spec:string,hz:number,view:(t:number,b:PlayerView)=>PlayerView,from:number,to:number){
 const base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,r=probeRig(spec);let prev:T.Quaternion[]|undefined,max=0;
 for(let i=0;;i++){const t=from-1+i/hz;if(t>to)break;r.group.position.set(0,0,0);r.group.rotation.y=0;animateCharacter(r,view(t,base),t,1/hz);r.group.updateMatrixWorld(true);
  const q=COMBO_BONES.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));if(prev&&t>=from)q.forEach((x,k)=>{max=Math.max(max,x.angleTo(prev![k])*180/Math.PI);});prev=q;}
 return max;
}
test('combo-ready: the next cut, a roll and the timeout all leave the hold without single-frame snaps',()=>{
 for(const spec of ['greatsword','sword','daggers','shield']){
  const w=WEAPONS[spec as 'sword'],end=1+w.windup+.16+meleeRecovery(spec as PlayerView['spec']),t2=end+.5,R={at:end+.4,until:end+.9,invulnerableAt:end+.48,invulnerableUntil:end+.7,direction:{x:0,y:0,z:1},distance:3.2};
  const cases:[string,(t:number,b:PlayerView)=>PlayerView,number,number,number][]=[
   ['next cut',(t,b)=>({...b,...(t<t2?attackAt(spec,1,1):attackAt(spec,t2,2))}) as PlayerView,t2-.1,t2+.1,24],
   ['roll',(t,b)=>({...b,...attackAt(spec,1,1),roll:t>=R.at?R:undefined}) as PlayerView,R.at-.05,R.at+.1,40],
   ['after the roll',(t,b)=>({...b,...attackAt(spec,1,1),roll:t>=R.at?R:undefined}) as PlayerView,R.until-.03,R.until+.3,20],
   ['timeout',(t,b)=>({...b,...attackAt(spec,1,1)}) as PlayerView,end+2.3,end+3.4,5]];
  for(const [name,view,from,to,bound] of cases){
   const [s60,s120]=[60,120].map(hz=>steps(spec,hz,view,from,to));
   // A snap keeps its size per frame as the rate doubles; continuous motion halves.
   assert.ok(s120<=s60*.7+.3,`${spec} ${name}: ${s60.toFixed(1)} deg/frame at 60 Hz vs ${s120.toFixed(1)} at 120 Hz`);
   assert.ok(s120<=bound,`${spec} ${name}: ${s120.toFixed(1)} deg per 120 Hz frame`);
  }
 }
});

// ---- DEV fighting styles: combo-ready stance between strings ----
const DEV={emperor:{key:'emperor',action:'jab',len:.385,memory:1.35},ansatsuken:{key:'arcade',action:'combo',len:.84,memory:1.1}} as const;
const devString=(spec:'emperor'|'ansatsuken',at:number)=>{const d=DEV[spec];return {lastAttack:at,attackIndex:0,[d.key]:spec==='emperor'?{action:'jab',at,until:at+d.len,index:0,total:2,hits:0,lastHit:0,comboDamage:0,electricAt:-10}:{action:'combo',at,until:at+d.len,index:0,total:3,charge:0,uppercutCd:0,hadokenCd:0,hits:0,lastHit:0,comboDamage:0,parryUntil:0,parryAt:-10}} as Partial<PlayerView>;};
test('DEV styles: a string ends in a chambered fighting stance that the next string starts from',()=>{
 for(const spec of ['emperor','ansatsuken'] as const){
  const animate=spec==='emperor'?animateEmperor:animateAnsatsuken,base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9} as PlayerView,t=1+DEV[spec].len+.5;
  const prev={...base,...devString(spec,1)} as PlayerView,next={...base,...devString(spec,t)} as PlayerView,fresh=probeRig(spec),held=probeRig(spec),entered=probeRig(spec),guard=probeRig(spec);
  animate(held,prev,t-.001);animate(entered,prev,t-.002);rest(entered,fresh);animate(entered,next,t);animate(guard,{...base,[DEV[spec].key]:undefined} as PlayerView,t-.001);
  for(const r of [held,entered,guard])r.group.updateMatrixWorld(true);
  let worst=0,at='',chamber=0;for(const b of UPPER){const q=held[b].getWorldQuaternion(new T.Quaternion());const d=q.angleTo(entered[b].getWorldQuaternion(new T.Quaternion()));if(d>worst){worst=d;at=b;}chamber=Math.max(chamber,q.angleTo(guard[b].getWorldQuaternion(new T.Quaternion())));}
  assert.ok(worst<.035,`${spec}: ${at} differs by ${(worst*180/Math.PI).toFixed(1)} deg between the held stance and the next string's first frame`);
  assert.ok(chamber>.15,`${spec}: the held stance is chambered (${(chamber*180/Math.PI).toFixed(1)} deg from the plain guard)`);
 }
});
test('DEV styles: the next string, a roll, a hit and the combo-memory timeout leave the stance without snaps',()=>{
 for(const spec of ['emperor','ansatsuken'] as const){
  const end=1+DEV[spec].len,t2=end+.5,R={at:end+.4,until:end+.9,invulnerableAt:end+.48,invulnerableUntil:end+.7,direction:{x:0,y:0,z:1},distance:3.2};
  const hit={kind:'hit',at:end+.4,until:end+.64,zone:'torso',turn:.3,strength:.6,yaw:0,direction:{x:0,y:0,z:1},point:{x:0,y:1,z:.3}};
  const cases:[string,(t:number,b:PlayerView)=>PlayerView,number,number][]=[
   ['next string',(t,b)=>({...b,...devString(spec,t<t2?1:t2)}) as PlayerView,t2-.1,t2+.1],
   ['hit',(t,b)=>({...b,...devString(spec,1),hitReaction:t>=hit.at?hit:undefined,hitStunUntil:t>=hit.at?hit.until:-1e9}) as unknown as PlayerView,hit.at-.05,hit.at+.3],
   ['timeout',(t,b)=>({...b,...devString(spec,1)}) as PlayerView,end+DEV[spec].memory-.1,end+DEV[spec].memory+.5]];
  if(spec==='emperor')cases.push(['roll',(t,b)=>({...b,...devString(spec,1),roll:t>=R.at?R:undefined}) as PlayerView,R.at-.05,R.at+.1]);
  for(const [name,view,from,to] of cases){
   const [s60,s120]=[60,120].map(hz=>steps(spec,hz,view,from,to));
   assert.ok(s120<=s60*.7+.3,`${spec} ${name}: ${s60.toFixed(1)} deg/frame at 60 Hz vs ${s120.toFixed(1)} at 120 Hz`);
  }
 }
});
