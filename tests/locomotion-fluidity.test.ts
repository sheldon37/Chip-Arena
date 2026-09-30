import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {attackCommitment} from '../src/shared/souls-combat.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {markFirstPersonRig} from '../src/client/pose-flags.js';
import {GAIT,cycleLength} from '../src/client/motion-quality.js';
import {stanceFor} from '../src/client/stances.js';
import {PROBE_RATES,probeMotion,probeRates,probeRig,describeProbe,type ProbeBone} from './helpers/motion-probe.js';

await initPhysics();
type Rig=ReturnType<typeof probeRig>;
const vel=(x:number,z:number)=>({vel:{x,y:0,z}});
const wrap=(a:number)=>a-Math.PI*2*Math.round(a/(Math.PI*2));
const worldYaw=(o:T.Object3D)=>{const f=new T.Vector3(0,0,1).applyQuaternion(o.getWorldQuaternion(new T.Quaternion()));return Math.atan2(f.x,f.z);};
const world=(o:T.Object3D)=>o.getWorldPosition(new T.Vector3());
/** Largest per-frame world rotation (deg) of the named bones inside [from,to]. */
function boneSteps(spec:string,hz:number,from:number,to:number,patch:(t:number)=>Partial<PlayerView>,bones:ProbeBone[],mode='idle'){
 const prev=new Map<string,T.Quaternion>(),max=new Map<string,number>();
 probeMotion({spec,mode,from:from-.2,to,hz,travel:true,patch,animate:(r,p,t,dt)=>{animateCharacter(r,p,t,dt,p.pitch);r.group.updateMatrixWorld(true);
  for(const b of bones){const q=r[b].getWorldQuaternion(new T.Quaternion());const last=prev.get(b);if(last&&t>=from)max.set(b,Math.max(max.get(b)??0,q.angleTo(last)*180/Math.PI));prev.set(b,q);}}});
 return max;
}

test('planted feet stay put on the floor in every gait at 30, 60 and 120 Hz',()=>{
 const gaits:[string,string,number,number][]=[['sword','idle',0,6.8],['pistol','idle',0,6.8],['sword','idle',0,2.8],['sword','block',0,3.06],['greatsword','block',0,3.06],['sword','idle',6.8,0],['pistol','idle',0,-6.8],['sword','idle',4.8,4.8]];
 for(const [spec,mode,x,z] of gaits){const speed=Math.hypot(x,z);
  for(const [hz,r] of Object.entries(probeRates({spec,mode,from:1,to:3,travel:true,patch:()=>vel(x,z)}))){
   const why=`${spec} ${mode} v=(${x},${z}) ${describeProbe(r)}`;
   assert.ok(r.feet.left.plantedFrames>0&&r.feet.right.plantedFrames>0,'both feet plant: '+why);
   // Baseline: planted ankles moved at 75-91% of body speed.
   for(const f of [r.feet.left,r.feet.right])assert.ok(f.medianPlantedSpeed<=.2*speed,`median planted-foot speed ${f.medianPlantedSpeed.toFixed(2)} at ${hz} Hz: `+why);
   assert.ok(r.maxFootSlip<=.06,'planted slip: '+why);
   // Baseline guard-walk ankles sank to -.946.
   assert.ok(r.minAnkleY>=-.8,'ankles stay on the floor: '+why);
  }
 }
});

test('cadence follows the ground speed (about 6.5 steps per second at a run)',()=>{
 assert.ok(Math.abs(6.8/cycleLength(6.8)*2-6.5)<.25);
 assert.ok(cycleLength(15)>cycleLength(6.8)&&cycleLength(15)<=GAIT.dashCycle+1e-9);
 for(const hz of PROBE_RATES){const r=probeRig('sword'),base=r.stride;probeMotion({spec:'sword',mode:'idle',from:1,to:2,hz,rig:r,patch:()=>vel(0,6.8)});
  const steps=(r.stride-base)/Math.PI;assert.ok(steps>12&&steps<14.5,`${steps.toFixed(2)} steps over 2 s at ${hz} Hz`);}
});

test('stopping takes a settling step into the class stance instead of sliding both feet together',()=>{
 const patch=(t:number)=>vel(0,t<2?6.8:Math.max(0,6.8-35*(t-2)));
 for(const hz of PROBE_RATES){const r=probeRig('sword'),report=probeMotion({spec:'sword',mode:'idle',from:1.8,to:3.3,hz,rig:r,travel:true,patch});
  assert.ok(report.maxFootSlip<=.05,describeProbe(report));assert.ok(r.motion<.001,'locomotion blend settles');
  r.group.updateMatrixWorld(true);const stance=stanceFor('sword'),lead=r.group.worldToLocal(world(r.leftFoot)),rear=r.group.worldToLocal(world(r.rightFoot));
  assert.ok(Math.abs(lead.z-stance.lead.z)<.06&&Math.abs(rear.z-stance.rear.z)<.06,`settled into the staggered stance at ${hz} Hz: lead z ${lead.z.toFixed(3)} rear z ${rear.z.toFixed(3)}`);
  for(const a of [lead,rear])assert.ok(Math.abs(a.y+.795)<.008,'settled ankles on the floor');
 }
});

test('dash lean, dash bursts and attacks out of a run have no hard pops',()=>{
 const dash=(t:number)=>({...vel(0,t>=2&&t<2.18?15:6.8),dashTime:t>=2&&t<2.18?2.18-t:0});
 // Baseline: the binary dash lean popped the body ~9.8 deg in one frame at the start and end.
 const body=boneSteps('sword',60,1.95,2.4,dash,['body','pelvis']);assert.ok(body.get('body')!<4,`body ${body.get('body')!.toFixed(1)} deg/frame`);
 // Continuous motion shrinks per frame as the rate rises; a single-frame snap keeps its size.
 const legs:ProbeBone[]=['leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','body','pelvis'];
 const cut=(t:number)=>t<2?vel(0,6.8):{...vel(0,6.8),lastAttack:2,attackIndex:1,swingAt:2.12,swingUntil:2.28,attackCommitment:attackCommitment('sword',2,.12,.16,1,.28)};
 for(const [name,patch] of [['dash',dash],['attack out of a run',cut]] as const){
  const a=boneSteps('sword',60,1.98,2.35,patch,legs),b=boneSteps('sword',120,1.98,2.35,patch,legs);
  for(const bone of legs)assert.ok(b.get(bone)!<Math.max(6,.8*a.get(bone)!),`${name}: ${bone} ${a.get(bone)!.toFixed(1)} deg/frame at 60 Hz vs ${b.get(bone)!.toFixed(1)} at 120 Hz`);
 }
});

test('jumps read push-off, apex and descent, then land into a knee-bend dip with planted feet',()=>{
 for(const [name,speed] of [['standing',0],['running',6.8]] as const){
  const r=probeRig('sword'),poses=new Map<number,T.Quaternion[]>();let before=0,dip=0,late=0;
  probeMotion({spec:'sword',mode:'idle',from:1,to:2.9,hz:60,rig:r,patch:t=>{const a=t-1.5;return a<0||a>.739?vel(0,speed):{grounded:false,vel:{x:0,y:8.5-23*a,z:speed}};},animate:(rig,p,t,dt)=>{
   animateCharacter(rig,p,t,dt,p.pitch);rig.group.updateMatrixWorld(true);const hip=world(rig.pelvis).y-rig.group.position.y;
   for(const at of [1.55,1.87,2.17])if(Math.abs(t-at)<1e-6+.5/60)poses.set(at,[rig.leftLeg,rig.leftKnee,rig.rightLeg,rig.rightKnee].map(b=>b.quaternion.clone()));
   if(t>1.3&&t<1.45)before=Math.max(before,hip);if(t>2.24&&t<2.6)dip=Math.max(dip,before-hip);
   if(t>2.24+.14)for(const f of [rig.leftFoot,rig.rightFoot])late=Math.min(late,world(f).y-rig.group.position.y);}});
  const [push,apex,fall]=[1.55,1.87,2.17].map(at=>poses.get(at)!),diff=(a:T.Quaternion[],b:T.Quaternion[])=>Math.max(...a.map((q,i)=>q.angleTo(b[i])));
  assert.ok(diff(push,apex)>.25&&diff(apex,fall)>.25&&diff(push,fall)>.25,`${name}: distinct air poses ${diff(push,apex).toFixed(2)} ${diff(apex,fall).toFixed(2)} ${diff(push,fall).toFixed(2)}`);
  assert.ok(dip>=.04,`${name}: landing dip ${dip.toFixed(3)} m`);
  assert.ok(late>=-.8,`${name}: ankles back on the floor after the landing blend (${late.toFixed(3)})`);
 }
});

test('turning in place keeps the feet planted, lags the hips, then steps round; the gun stays on aim',()=>{
 const flick=(t:number)=>({yaw:t<1.5?0:t<1.6?-(t-1.5)/.1*1.745:-1.745});
 for(const spec of ['greatsword','rifle'])for(const hz of PROBE_RATES){
  const r=probeRig(spec);let startL:T.Vector3|undefined,startR:T.Vector3|undefined,held=0,lag=0,maxGrip=0;const lifted=[0,0];
  probeMotion({spec,mode:'idle',from:1,to:2.8,hz,rig:r,patch:flick,animate:(rig,p,t,dt)=>{
   animateCharacter(rig,p,t,dt,p.pitch);rig.group.updateMatrixWorld(true);const l=world(rig.leftFoot),rr=world(rig.rightFoot);
   if(t<1.5){startL=l;startR=rr;}
   else if(t<1.63&&l.distanceTo(startL!)<.005&&rr.distanceTo(startR!)<.005)held++;
   if(t>1.58&&t<1.62)lag=Math.max(lag,Math.abs(wrap(worldYaw(rig.pelvis)-rig.group.rotation.y)));
   if(t>1.5)[l,rr].forEach((a,i)=>{if(a.y-rig.group.position.y>-.775)lifted[i]=1;});
   const grip=rig.weapon.getObjectByName('support-grip');if(spec==='rifle'&&grip&&t>=1.2)maxGrip=Math.max(maxGrip,world(rig.leftHand).distanceTo(world(grip)));}});
  assert.ok(held>=2,`${spec} ${hz} Hz: feet held in the world for ${held} frames through the flick`);
  assert.ok(lag>.3,`${spec} ${hz} Hz: hips lag the flick by ${lag.toFixed(2)} rad`);
  assert.deepEqual(lifted,[1,1],`${spec} ${hz} Hz: both feet step round`);
  const stance=stanceFor(spec);r.group.updateMatrixWorld(true);
  for(const [foot,toe] of [[r.leftFoot,stance.lead.toeOut],[r.rightFoot,-stance.rear.toeOut]] as const)assert.ok(Math.abs(wrap(worldYaw(foot)-r.group.rotation.y-toe))<.3,`${spec} ${hz} Hz: feet face the new aim`);
  assert.ok(Math.abs(wrap(worldYaw(r.pelvis)-r.group.rotation.y-stance.pelvisYaw))<.2,`${spec} ${hz} Hz: hips return over the feet`);
  if(spec==='rifle')assert.ok(maxGrip<.03,`support grip error ${maxGrip.toFixed(3)} while the hips lag`);
 }
});

test('the dodge roll never passes through the zero pose and exits into a planted crouch',()=>{
 const roll={at:1.5,until:2,invulnerableAt:1.58,invulnerableUntil:1.84,direction:{x:1,y:0,z:0},distance:3.2};
 for(const hz of PROBE_RATES){let minKnee=Infinity,exitAnkle=-Infinity;
  const report=probeMotion({spec:'sword',mode:'idle',from:1,to:2.4,hz,travel:true,patch:()=>({roll}),animate:(r,p,t,dt)=>{animateCharacter(r,p,t,dt,p.pitch);r.group.updateMatrixWorld(true);
   const u=(t-roll.at)/(roll.until-roll.at);if(u>=.72&&u<1){minKnee=Math.min(minKnee,r.leftKnee.quaternion.angleTo(new T.Quaternion()),r.rightKnee.quaternion.angleTo(new T.Quaternion()));}
   if(u>=.9&&u<1)for(const f of [r.leftFoot,r.rightFoot])exitAnkle=Math.max(exitAnkle,Math.abs(world(f).y-r.group.position.y+.795));}});
  assert.ok(minKnee>.35,`${hz} Hz: knees stay bent through the exit (${minKnee.toFixed(2)})`);
  assert.ok(exitAnkle<.02,`${hz} Hz: exit feet planted (${exitAnkle.toFixed(3)})`);
  // The roll -> base transition blend (pose-transitions.ts) can dip a crouched ankle ~1.5 cm for one frame at 30 Hz.
  assert.ok(report.minAnkleY>=(hz<60?-.815:-.8),describeProbe(report));
 }
});

test('idle is a living staggered stance: weight shifts over planted feet',()=>{
 const r=probeRig('sword'),xs:number[]=[];
 const report=probeMotion({spec:'sword',mode:'idle',from:1,to:7,hz:30,rig:r,animate:(rig,p,t,dt)=>{animateCharacter(rig,p,t,dt,p.pitch);if(t>=1)xs.push(rig.body.position.x);}});
 assert.ok(report.maxFootSlip<1e-9,describeProbe(report));
 assert.ok(Math.max(...xs)-Math.min(...xs)>.015,'hips shift weight');
 r.group.updateMatrixWorld(true);const lead=r.group.worldToLocal(world(r.leftFoot)),rear=r.group.worldToLocal(world(r.rightFoot));
 assert.ok(lead.z-rear.z>.12,'staggered feet');
 assert.ok(r.leftKnee.quaternion.angleTo(new T.Quaternion())>.15,'soft knees');
});

test('the first-person view model sways gently instead of pumping its arms',()=>{
 const fp=probeRig('sword'),tp=probeRig('sword');markFirstPersonRig(fp);const range=(r:Rig)=>{const xs:number[]=[];probeMotion({spec:'sword',mode:'run',from:1,to:2,hz:60,rig:r,animate:(rig,p,t,dt)=>{animateCharacter(rig,p,t,dt,0);if(t>=1)xs.push(rig.rightArm.rotation.x);}});return Math.max(...xs)-Math.min(...xs);};
 const a=range(fp),b=range(tp);assert.ok(b>1.2&&a<.3*b,`first person ${a.toFixed(2)} rad vs third person ${b.toFixed(2)} rad`);
 assert.ok(fp.stride>0,'the view-model stride still advances (view bob reads it)');
});
