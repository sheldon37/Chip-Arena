import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {WEAPONS} from '../src/shared/content.js';
import {meleeRecovery,attackCommitment,attackTravel} from '../src/shared/souls-combat.js';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {probeBase,probeRig} from './helpers/motion-probe.js';
import {CH,CHANNEL_COUNT,decodeClip,sampleClip,clipDuration,type MocapClip} from '../src/client/mocap/mocap-clip.js';
import {MOCAP_SETS,cutClip,cutTime} from '../src/client/mocap/mocap-layer.js';
import {GREATSWORD_CLIPS} from '../src/client/mocap/greatsword-clips.js';

await initPhysics();
const set=MOCAP_SETS.greatsword!;
const w=WEAPONS.greatsword,window=.16,recovery=meleeRecovery('greatsword');

test('greatsword mocap clip table decodes to finite rows', () => {
 for(const [name,clip] of Object.entries(GREATSWORD_CLIPS)){
  const d=decodeClip(clip);assert.equal(d.length,clip.frames*CHANNEL_COUNT,name);
  for(const x of d)assert.ok(Number.isFinite(x),name);
  if(clip.marks)for(let i=1;i<clip.marks.length;i++)assert.ok(clip.marks[i]>clip.marks[i-1]&&clip.marks[i]<=clipDuration(clip)+1e-9,name+' marks ascend inside the clip');
 }
 for(const k of ['idle','walk','run','block','blockHit','stagger'] as const)assert.ok(set[k],k);
 assert.equal(set.cuts!.length,2);
});

/** The blade's heading (rad, 0 = the rig's forward) and its upward component in the rig frame at clip time `t`. */
const _r=new Float32Array(CHANNEL_COUNT),_q=new T.Quaternion(),_j=new T.Quaternion(),_v=new T.Vector3();
function blade(clip:MocapClip,t:number){
 sampleClip(clip,t,_r);_q.setFromAxisAngle(_v.set(0,1,0),_r[CH.yaw]);
 for(const j of ['pelvis','spine','chest']){const i=CH[(j+'.x') as keyof typeof CH];_v.set(_r[i],_r[i+1],_r[i+2]);const a=_v.length();_q.multiply(a<1e-9?_j.identity():_j.setFromAxisAngle(_v.normalize(),a));}
 const b=_v.set(_r[CH['blade.x']],_r[CH['blade.y']],_r[CH['blade.z']]).applyQuaternion(_q);return {heading:Math.atan2(b.x,b.z),up:b.y};
}
test('every greatsword cut lands its captured contact on the authoritative hit window (within one frame)', () => {
 for(const rate of [1,1.35,.8]){
  const windup=w.windup/rate,win=window/rate,rec=recovery/rate;
  for(let index=1;index<=4;index++){
   const clip=cutClip(set,index),m=clip.marks!,frame=1/clip.fps;
   assert.ok(Math.abs(cutTime(clip,0,windup,win,rec)-m[0])<1e-9);
   assert.ok(Math.abs(cutTime(clip,windup,windup,win,rec)-m[1])<1e-9,'contact start on swingAt');
   assert.ok(Math.abs(cutTime(clip,windup+win,windup,win,rec)-m[2])<1e-9,'contact end on swingUntil');
   assert.ok(Math.abs(cutTime(clip,windup+win+rec,windup,win,rec)-m[3])<1e-9,'settled at the end of recovery');
   // Clip time never runs backward over the attack.
   let prev=-Infinity;for(let t=0;t<=windup+win+rec;t+=.005){const c=cutTime(clip,t,windup,win,rec);assert.ok(c>=prev-1e-9);prev=c;}
   // The captured cut crosses the rig's forward (the server's arc centre) inside the contact keys, within one source frame,
   // and is inside the arc (WEAPONS.greatsword.arc) for the whole window: the hit reads where and when the server hits.
   let cross=NaN;for(let t=m[0];t<m[3];t+=1/240){const a=blade(clip,t).heading,b=blade(clip,t+1/240).heading;if(Math.abs(a)<1.2&&Math.abs(b)<1.2&&Math.sign(a)!==Math.sign(b)){cross=t;break;}}
   assert.ok(cross>=m[1]-frame&&cross<=m[2]+frame,`index ${index}: blade crosses the front at ${cross} s, contact ${m[1]}..${m[2]}`);
   for(let t=m[1]+frame;t<=m[2]-frame;t+=1/120)assert.ok(Math.abs(blade(clip,t).heading)<w.arc/2+.15,`index ${index}: blade outside the arc at ${t.toFixed(3)}`);
  }
 }
});

test('greatsword mocap poses stay inside joint limits', () => {
 const row=new Float32Array(CHANNEL_COUNT);
 for(const [name,clip] of Object.entries(GREATSWORD_CLIPS))for(let t=0;t<=clipDuration(clip);t+=1/60){
  sampleClip(clip,t,row);
  for(const k of ['lknee','rknee'] as const)assert.ok(row[CH[k]]>-.15&&row[CH[k]]<2.7,`${name} ${k} ${row[CH[k]]}`);
  for(const j of ['pelvis','spine','chest','neck','head','lclav','rclav','lhip','rhip','lankle','rankle']){
   const i=CH[(j+'.x') as keyof typeof CH],a=Math.hypot(row[i],row[i+1],row[i+2]);
   const limit=j==='neck'||j==='head'||j.endsWith('clav')?1.3:j.endsWith('ankle')?1.4:j==='pelvis'?1.6:j.endsWith('hip')?2.4:1.2;
   assert.ok(a<limit,`${name} ${j} ${a.toFixed(2)} rad at ${t.toFixed(2)}`);
  }
  // The main grip stays near the arm's reach of its shoulder (chest space; rig-ik clamps the last few cm), blade and edge stay unit and perpendicular.
  const g=Math.hypot(row[CH['grip.x']]+.345,row[CH['grip.y']]-.18,row[CH['grip.z']]);assert.ok(g<.7,`${name} grip reach ${g.toFixed(2)}`);
  const b=[row[CH['blade.x']],row[CH['blade.y']],row[CH['blade.z']]],e=[row[CH['edge.x']],row[CH['edge.y']],row[CH['edge.z']]];
  assert.ok(Math.abs(Math.hypot(...b)-1)<.06&&Math.abs(Math.hypot(...e)-1)<.06,`${name} unit blade/edge`);
  assert.ok(Math.abs(b[0]*e[0]+b[1]*e[1]+b[2]*e[2])<.06,`${name} edge perpendicular to blade`);
 }
});

// Rig-level invariants of the captured greatsword on the real rig (the procedural layer's own suites run with MOCAP off).
const LIMBS=['leftElbow','rightElbow','leftKnee','rightKnee','leftArm','rightArm','leftLeg','rightLeg'] as const;
/** A combat-ready greatsword standing in its captured guard, cutting at t=2 (attack index, rate). */
function cut(index:number,rate:number,hz:number,each:(r:ReturnType<typeof probeRig>,t:number)=>void){
 const base={...probeBase('greatsword'),id:'mocap',grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,windup=w.windup/rate,win=window/rate;
 const c=attackCommitment('greatsword',2,windup,win,rate,w.interval/rate);
 const attack={...base,attackIndex:index,attackRate:rate,lastAttack:2,windup,swingAt:2+windup,swingUntil:2+windup+win,attackCommitment:c} as PlayerView,idle={...base,lastAttack:-10,lastBlock:1.2} as PlayerView;
 const r=probeRig('greatsword');
 for(let i=0;;i++){const t=1.7+i/hz;if(t>c.until+.3)break;r.group.position.set(0,0,t<2?0:attackTravel(c,t));animateCharacter(r,t<2?idle:attack,t,1/hz);r.group.updateMatrixWorld(true);each(r,t);}
}
test('captured greatsword cuts keep every limb continuous (no single-frame flips) at 60 and 120 Hz', () => {
 for(const rate of [.7,1,1.5])for(const index of [1,2]){
  const worst=[60,120].map(hz=>{let prev:T.Quaternion[]|undefined,step=0;cut(index,rate,hz,(r,t)=>{const q=LIMBS.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));if(prev&&t>=1.95)q.forEach((x,k)=>{step=Math.max(step,x.angleTo(prev![k]));});prev=q;});return step;});
  assert.ok(worst[1]<=worst[0]*.75+.01,`cut ${index} @${rate}x: ${worst[0].toFixed(3)} rad/frame at 60 Hz vs ${worst[1].toFixed(3)} at 120 Hz (a snap)`);
  assert.ok(worst[1]*120<110,`cut ${index} @${rate}x: ${(worst[1]*120).toFixed(0)} rad/s`);
 }
});
test('the captured guard and cuts keep the support hand on the hilt while the capture holds it two-handed', () => {
 const grip=new T.Vector3(),palm=new T.Vector3();
 for(const index of [1,2])cut(index,1,60,(r,t)=>{
  const support=r.weapon.getObjectByName('support-grip')!;support.getWorldPosition(grip);r.leftHand.getWorldPosition(palm);
  // The guard before the cut and the hoist (both captured two-handed) hold the hilt; the free hand may leave it later.
  if(t<2.3)assert.ok(grip.distanceTo(palm)<.09,`cut ${index} at ${t.toFixed(3)}: support hand ${grip.distanceTo(palm).toFixed(3)} m off the hilt`);
 });
});
