import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {attackCommitment,attackTravel} from '../src/shared/souls-combat.js';
import {animateCharacter,animationPreviewPlayer,attackPose} from '../src/client/character-animation.js';
import {type CharacterRig} from '../src/client/character-rig.js';
import {hand} from '../src/client/rig-ik.js';
import {meleeReadiness,meleeGuard,GUARD_HOLD,GUARD_FADE} from '../src/client/melee-guard.js';
import {blockRaise,blockRecoil} from '../src/client/melee-block-motion.js';
import {MeleeTrails} from '../src/client/melee-trails.js';
import {recordBlockHit,clearBlockImpacts} from '../src/client/block-impacts.js';
import type {ProjectileTrails} from '../src/client/projectile-trails.js';
import {probeBase,probeRig,weaponTip} from './helpers/motion-probe.js';
import {MOCAP} from '../src/client/mocap/mocap-layer.js';
// The greatsword's third-person rig now plays the captured set (tests/greatsword-mocap.test.ts guards it); this suite keeps
// guarding the procedural melee layers, which still pose the first-person view model and the other weapons.
MOCAP.enabled=false;

await initPhysics();
const CUTS:[string,number][]=[['sword',1],['sword',2],['sword',3],['daggers',1],['daggers',2],['daggers',3],['daggers',4],['shield',1],['shield',2],['greatsword',1],['greatsword',2]];
const LIMBS=['leftElbow','rightElbow','leftKnee','rightKnee','leftArm','rightArm','leftLeg','rightLeg'] as const;
/** A grounded standing player that attacks at t=2 with the synthesized commitment (the rig root follows its travel). */
function attackScenario(spec:string,index:number){
 const base={...probeBase(spec),grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,w=WEAPONS[spec as keyof typeof WEAPONS],lastAttack=2,c=attackCommitment(base.spec,lastAttack,w.windup,.16,1,w.windup+.16);
 const attack={...base,attackIndex:index,lastAttack,swingAt:lastAttack+w.windup,swingUntil:lastAttack+w.windup+.16,attackCommitment:c} as PlayerView,idle={...base,lastAttack:-10,swingAt:undefined,swingUntil:undefined,attackCommitment:undefined} as PlayerView;
 return {attack,idle,c,lastAttack,end:c.until};
}
/** Steps the scenario at `hz` and calls `each` after every frame with the rig posed in world space. */
function run(spec:string,index:number,hz:number,each:(r:CharacterRig,t:number,p:PlayerView)=>void,from=1.7,extra=.3){
 const s=attackScenario(spec,index),r=probeRig(spec);
 for(let i=0;;i++){const t=from+i/hz;if(t>s.end+extra)break;const p=t<s.lastAttack?s.idle:s.attack;r.group.position.set(0,0,t<s.lastAttack?0:attackTravel(s.c,t));r.group.rotation.y=0;animateCharacter(r,p,t,1/hz);r.group.updateMatrixWorld(true);each(r,t,p);}
 return s;
}
const world=(r:CharacterRig,bone:typeof LIMBS[number])=>r[bone].getWorldQuaternion(new T.Quaternion());

test('elbows and knees never flip: every cut is continuous at 60 and 120 Hz and far below the old single-frame snaps',()=>{
 for(const [spec,index] of CUTS){
  const worst:number[]=[];
  for(const hz of [60,120]){let prev:T.Quaternion[]|undefined,step=0;
   run(spec,index,hz,(r,t)=>{const q=LIMBS.map(b=>world(r,b));if(prev&&t>=1.95)q.forEach((x,k)=>{step=Math.max(step,x.angleTo(prev![k]));});prev=q;});worst.push(step);}
  // A snap keeps its size per frame as the rate doubles; continuous motion halves (baseline: a 644 rad/s elbow flip).
  assert.ok(worst[1]<=worst[0]*.7+.01,`${spec} ${index}: ${worst[0].toFixed(3)} rad/frame at 60 Hz vs ${worst[1].toFixed(3)} at 120 Hz`);
  assert.ok(worst[1]*120<110,`${spec} ${index}: ${(worst[1]*120).toFixed(0)} rad/s`);
 }
});

test('the blade carries its momentum through swingUntil and returns without retracing the cut',()=>{
 for(const [spec,index] of [['sword',1],['sword',2],['daggers',1],['greatsword',1]] as [string,number][]){
  const w=WEAPONS[spec as keyof typeof WEAPONS],speeds:{t:number;v:number}[]=[];let prev:T.Vector3|undefined;const hz=240;
  const s=run(spec,index,hz,(r,t,p)=>{const tip=weaponTip(r,p).sub(r.group.position);if(prev&&t>2)speeds.push({t:t-2,v:tip.distanceTo(prev)*hz});prev=tip;},1.9,.05);
  const at=(x:number)=>speeds.find(v=>v.t>=x)!.v,active=speeds.filter(v=>v.t>=w.windup&&v.t<=w.windup+.16),peak=Math.max(...active.map(v=>v.v));
  assert.ok(at(w.windup+.16)>=.18*peak,`${spec} ${index}: ${at(w.windup+.16).toFixed(1)} m/s at swingUntil vs peak ${peak.toFixed(1)} (baseline 0)`);
  // Daggers recover in .15 s, so their snap back to guard may be nearly as quick as the slash, never quicker.
  const recovery=Math.max(...speeds.filter(v=>v.t>w.windup+.16+.03&&v.t<s.end-2).map(v=>v.v));assert.ok(recovery<=(spec==='daggers'?.9:.6)*peak,`${spec} ${index}: recovery ${recovery.toFixed(1)} vs strike ${peak.toFixed(1)}`);
  assert.ok(at(.001)<5,`${spec} ${index}: no weapon pop at the first attack frame (${at(.001).toFixed(1)} m/s)`);
 }
});

test('the head never rolls with the torso and the neck stays proud through every cut',()=>{
 for(const [spec,index] of CUTS){let roll=0,tilt=0;
  run(spec,index,60,(r,t)=>{if(t<2)return;const e=new T.Euler().setFromQuaternion(r.head.getWorldQuaternion(new T.Quaternion()),'YXZ');roll=Math.max(roll,Math.abs(e.z));
   const d=r.head.getWorldPosition(new T.Vector3()).sub(r.neck.getWorldPosition(new T.Vector3())).normalize();tilt=Math.max(tilt,Math.acos(Math.min(1,d.y)));},1.9,0);
  assert.ok(roll<=.2,`${spec} ${index}: head roll ${roll.toFixed(2)} (baseline .61-.96)`);assert.ok(tilt<=.45,`${spec} ${index}: neck tilt ${tilt.toFixed(2)} (baseline .62-.89)`);
 }
});

test('planted feet hold their floor spot under the authored root travel; steps move feet only while lifted',()=>{
 for(const [spec,index] of CUTS){const down:(T.Vector3|undefined)[]=[undefined,undefined],slip=[0,0];
  run(spec,index,120,(r,t)=>{if(t<1.98)return;[r.leftFoot,r.rightFoot].forEach((f,k)=>{const a=f.getWorldPosition(new T.Vector3());if(a.y<-.785){if(!down[k])down[k]=a.clone();slip[k]=Math.max(slip[k],Math.hypot(a.x-down[k]!.x,a.z-down[k]!.z));}else down[k]=undefined;});});
  assert.ok(Math.max(...slip)<.02,`${spec} ${index}: planted slip ${slip.map(x=>(x*1000).toFixed(1)).join('/')} mm (baseline 105-500 mm)`);
 }
});

test('running carries the sword and greatsword forward-down (Dark Souls sprint) instead of windmilling them overhead or trailing them back',()=>{
 for(const spec of ['sword','greatsword']){
  const base=probeBase(spec),r=probeRig(spec);let above=0,frames=0,maxStep=0,prev:T.Quaternion|undefined;
  for(let n=0;n<90;n++){const t=n/60,p={...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9} as PlayerView;animateCharacter(r,p,t,1/60);r.group.updateMatrixWorld(true);if(n<40)continue;
   const grip=r.weapon.getWorldPosition(new T.Vector3()),tip=r.weapon.getObjectByName('blade-tip')!.getWorldPosition(new T.Vector3()),q=r.weapon.getWorldQuaternion(new T.Quaternion());
   frames++;if(tip.y-grip.y>.9||tip.y>grip.y||tip.z<grip.z+.3)above++;if(prev)maxStep=Math.max(maxStep,q.angleTo(prev));prev=q;assert.ok(tip.y>-.84,`${spec} tip above the floor`);}
  assert.equal(above,0,`${spec}: blade overhead, raised or behind the grip in ${above}/${frames} run frames (baseline: vertical every stride)`);
  assert.ok(maxStep*180/Math.PI<12,`${spec}: weapon turns ${(maxStep*180/Math.PI).toFixed(1)} deg in one frame`);
 }
});

test('the carry fade is rate independent and the guard is combat readiness from authoritative timestamps',()=>{
 const angles=[30,60,120].map(hz=>{const base=probeBase('sword'),r=probeRig('sword');let t=0;for(let n=0;n<=hz*.6;n++){t=n/hz;animateCharacter(r,{...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9} as PlayerView,t,1/hz);}r.group.updateMatrixWorld(true);return r.weapon.getWorldQuaternion(new T.Quaternion());});
 assert.ok(angles[0].angleTo(angles[2])<.08&&angles[1].angleTo(angles[2])<.05,'the eased carry lands on the same hold at 30, 60 and 120 Hz');
 const p={...probeBase('sword'),lastAttack:-10,lastBlock:-10,meleeAction:undefined,swingAt:undefined,swingUntil:undefined} as PlayerView;
 assert.equal(meleeReadiness(p,3),0,'no recent action: relaxed');
 const hit={...p,lastAttack:2,swingAt:2.14,swingUntil:2.3};assert.equal(meleeReadiness(hit,2.1),1);assert.equal(meleeReadiness(hit,2.6+GUARD_HOLD),1);
 assert.ok(meleeReadiness(hit,2.6+GUARD_HOLD+GUARD_FADE*.5)>0&&meleeReadiness(hit,2.6+GUARD_HOLD+GUARD_FADE*.5)<1);assert.equal(meleeReadiness(hit,2.6+GUARD_HOLD+GUARD_FADE+.01),0);
 assert.equal(meleeReadiness(hit,1.9),0,'a rewind before the attack shows no guard');assert.equal(meleeReadiness({...p,lastBlock:5},5.2),1,'a timed block raises readiness');
 for(const spec of ['sword','daggers','shield','greatsword'])assert.ok(meleeGuard(spec),spec+' guard');assert.equal(meleeGuard('pistol'),undefined);
 // A guard shows on the idle right after an attack, and relaxes once readiness lapses.
 const r=probeRig('sword'),after={...animationPreviewPlayer(probeBase('sword'),'idle',3),hitStunUntil:-1e9,lastAttack:2,swingAt:2.14,swingUntil:2.3} as PlayerView;
 animateCharacter(r,after,3,1/60);r.group.updateMatrixWorld(true);const tip=r.weapon.getObjectByName('blade-tip')!.getWorldPosition(new T.Vector3()),grip=r.weapon.getWorldPosition(new T.Vector3());
 assert.ok(tip.y>grip.y+.3&&tip.z>grip.z+.3,'combat-ready guard: blade forward and up');
});

test('arm IK is stateless and keeps the elbow hinge clean even with the hand straight overhead',()=>{
 for(const target of [[-.3,.55,.05],[-.3,.62,0],[-.35,.2,.02],[-.2,-.1,.3]] as [number,number,number][]){
  const a=probeRig('sword'),b=probeRig('sword'),e=new T.Euler(-1,.2,.1);
  hand(b,false,new T.Vector3(.2,-.1,.3),e,1);hand(b,false,new T.Vector3(...target),e,1);hand(a,false,new T.Vector3(...target),e,1);
  assert.ok(a.rightArm.quaternion.angleTo(b.rightArm.quaternion)<1e-6&&a.rightElbow.quaternion.angleTo(b.rightElbow.quaternion)<1e-6,'same target, same limb');
  const hinge=new T.Euler().setFromQuaternion(a.rightElbow.quaternion);assert.ok(Math.abs(hinge.y)<1e-6&&Math.abs(hinge.z)<1e-6&&hinge.x<=1e-9,'the elbow is a pure flexion hinge');
  a.group.updateMatrixWorld(true);const s=a.rightArm.getWorldPosition(new T.Vector3()),w=a.rightHand.getWorldPosition(new T.Vector3());assert.ok(s.distanceTo(w)>=.199,'the hand never folds into the shoulder');
 }
});

test('a living block rises with a small overshoot, settles, and gives under a recorded block impact',()=>{
 assert.equal(blockRaise(0),0);assert.ok(blockRaise(.06)>=.78);assert.ok(Math.max(...[.07,.08,.09,.1,.11].map(blockRaise))<=1.06);assert.equal(blockRaise(.13),1);
 for(let t=0;t<.3;t+=.005)assert.ok(Number.isFinite(blockRecoil(t,false))&&blockRecoil(t,true)<=1.001);assert.equal(blockRecoil(.3,true),0);
 clearBlockImpacts();const base={...probeBase('sword'),id:'blocker',hitStunUntil:-1e9,lastAttack:-10,guarding:true,blockTime:0} as PlayerView,r=probeRig('sword');
 const grip=(t:number)=>{animateCharacter(r,base,t,1/60);r.group.updateMatrixWorld(true);return r.chest.worldToLocal(r.weapon.getWorldPosition(new T.Vector3())).z;};
 let before=0;for(let t=3;t<3.5;t+=1/60)before=grip(t);recordBlockHit({type:'blockHit',time:3.5,pos:{x:0,y:1,z:1},source:'blocker',target:'a'},3.5);
 let shoved=before;for(let t=3.5;t<3.6;t+=1/60)shoved=Math.min(shoved,grip(t));let settled=0;for(let t=3.6;t<3.9;t+=1/60)settled=grip(t);
 assert.ok(before-shoved>.02,`the weapon gives toward the chest (${(before-shoved).toFixed(3)} m)`);assert.ok(Math.abs(settled-before)<.01,'and returns to the guard');clearBlockImpacts();
});

test('swept blade trails are finite, bounded, follow the setting and dispose after their lifetime',()=>{
 const scene=new T.Scene(),calls:string[]=[],stub={sample:(id:string)=>{calls.push(id);}} as unknown as ProjectileTrails,trails=new MeleeTrails(stub),r=probeRig('greatsword');scene.add(r.group);
 const s=attackScenario('greatsword',1);
 // Legacy path (a renderer that does not pass the setting): the tip ribbon goes through ProjectileTrails.
 for(let t=2;t<2.7;t+=1/60){const pose=animateCharacter(r,s.attack,t,1/60);trails.sample(r,s.attack,pose,t);trails.update(1/60,t);}
 assert.ok(calls.length>0&&trails.size===0,'legacy tip samples only');
 trails.update(1/60,2.7,true,1);let seen=0;
 for(let t=2;t<2.8;t+=1/60){const pose=animateCharacter(r,s.attack,t,1/60);trails.sample(r,s.attack,pose,t);trails.update(1/60,t,true,1);
  for(const m of trails.meshes()){assert.equal(m.parent,scene);const pos=m.geometry.getAttribute('position') as T.BufferAttribute,count=m.geometry.drawRange.count;if(count>0)seen++;
   for(let i=0;i<pos.count;i++){const v=new T.Vector3().fromBufferAttribute(pos,i);assert.ok(Number.isFinite(v.x+v.y+v.z)&&v.length()<4,'bounded, finite ribbon');}}
  assert.ok(trails.size<=16);}
 assert.ok(seen>3,'the sweep drew a ribbon');assert.ok(attackPose(s.attack,2.8).active);
 trails.update(1/60,3.2,true,1);assert.equal(trails.size,0,'ribbons retire after their lifetime');assert.ok(scene.children.every(c=>c.name!=='melee-sweep'),'and leave the scene');
 for(let t=2.4;t<2.5;t+=1/60){const pose=animateCharacter(r,s.attack,t,1/60);trails.sample(r,s.attack,pose,t);trails.update(1/60,t,true,.3);}
 trails.update(1/60,2.5,false);assert.equal(trails.size,0,'turning trails off clears them');trails.dispose();assert.ok(scene.children.every(c=>c.name!=='melee-sweep'));
});
