import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {introDuration,introNameAt,type IntroStyle} from '../src/shared/intros.js';
import {poseIntro,introCamera} from '../src/client/intro-choreography.js';
import {probeMotion,probeRig,describeProbe} from './helpers/motion-probe.js';
await initPhysics();
/** The two developer fighting-style intros (Rain Warrior, Emperor's Oath). poseIntro places the root itself at y .88, so
 * the probe's root stays at the world origin and ankles are measured in world space: a standing ankle sits at .085
 * (rig -.795 under the .88 root); planted means within 1 cm of that. */
const DEV:[IntroStyle,string][]=[['dev-ansatsuken','ansatsuken'],['dev-emperor','emperor']];
const ROOT_Y=.88,STANDING=-.795,PLANT=ROOT_Y+STANDING+.01;
const probe=(style:IntroStyle,spec:string,hz:number)=>probeMotion({spec,player:(_t,b)=>b,animate:(r,_p,t)=>poseIntro(r,style,t),from:0,to:introDuration(style),warm:0,hz,plantY:PLANT,rig:probeRig(spec)});
const forward=(o:T.Object3D)=>new T.Vector3(0,0,1).applyQuaternion(o.getWorldQuaternion(new T.Quaternion())).setY(0).normalize();

test('developer intros keep planted feet locked on the floor and never sink', ()=>{
 for(const [style,spec] of DEV){const report=probe(style,spec,60);
  assert.ok(report.maxFootSlip<=.02,`${style} planted feet slide: ${describeProbe(report)}`);
  assert.ok(report.minAnkleY-ROOT_Y>=-.81,`${style} ankles sink below the floor: ${describeProbe(report)}`);
  assert.ok(report.feet.left.plantedFrames>200&&report.feet.right.plantedFrames>200,`${style} feet are planted most of the time: ${describeProbe(report)}`);
 }
});

test('developer intros keep each foot under its knee (no twisted or flipped legs)', ()=>{
 for(const [style,spec] of DEV){const r=probeRig(spec);let worst=0,at=0;
  for(let i=0;i<=introDuration(style)*60;i++){const t=i/60;poseIntro(r,style,t);r.group.updateMatrixWorld(true);
   for(const [ankle,knee] of [[r.leftFoot,r.leftKnee],[r.rightFoot,r.rightKnee]]){const twist=forward(ankle).angleTo(forward(knee));if(twist>worst){worst=twist;at=t;}}}
  assert.ok(worst<.8,`${style}: a foot points ${worst.toFixed(2)} rad away from its knee at ${at.toFixed(2)} s`);
 }
});

test('developer intros have no single-frame joint pops and move smoothly at every frame rate', ()=>{
 for(const [style,spec] of DEV){const slow=probe(style,spec,30),mid=probe(style,spec,60),fast=probe(style,spec,120);
  // A fast strike may turn a bone quickly, but never more than 14 deg in one 60 Hz frame.
  assert.ok(mid.maxJointStepDeg<14,`${style} joint pop: ${describeProbe(mid)}`);
  // A snap keeps its size as the rate rises; smooth motion's per-frame change shrinks with the frame time.
  assert.ok(fast.maxJointStepDeg<.6*slow.maxJointStepDeg,`${style} per-frame change does not shrink with the frame rate: ${describeProbe(fast)} vs ${describeProbe(slow)}`);
  assert.ok(fast.maxBodyStep<.6*slow.maxBodyStep+1e-4,`${style} motion root jumps: ${describeProbe(fast)} vs ${describeProbe(slow)}`);
 }
});

test('developer intro holds are alive: no stretch of half a second is frozen', ()=>{
 for(const [style,spec] of DEV){const r=probeRig(spec),bones=[r.pelvis,r.chest,r.head,r.leftHand,r.rightHand,r.leftKnee,r.rightKnee];
  const sample=(t:number)=>{poseIntro(r,style,t);r.group.updateMatrixWorld(true);return bones.map(b=>b.getWorldQuaternion(new T.Quaternion()));};
  for(let t=0;t+.5<=introDuration(style);t+=.25){const a=sample(t),b=sample(t+.5),moved=Math.max(...a.map((q,i)=>q.angleTo(b[i])))*180/Math.PI;assert.ok(moved>.5,`${style} is frozen from ${t.toFixed(2)} s (${moved.toFixed(2)} deg)`);}
 }
});

test('developer intros are pure functions of age: 30, 60 and 120 Hz playback agree at the same ages', ()=>{
 const BONES=['body','pelvis','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as const;
 for(const [style,spec] of DEV){const end=introDuration(style),runs=[30,60,120].map(hz=>{const r=probeRig(spec),poses=new Map<number,number[]>();
   for(let i=0;i<=Math.floor(end*hz);i++){const t=i/hz;poseIntro(r,style,t);r.group.updateMatrixWorld(true);if(i%(hz/30)===0)poses.set(Math.round(t*30),[...r.group.position.toArray(),...BONES.flatMap(b=>[...r[b].getWorldQuaternion(new T.Quaternion()).toArray(),...r[b].getWorldPosition(new T.Vector3()).toArray()])]);}
   return poses;});
  for(const [frame,pose] of runs[0])for(const other of runs.slice(1)){const diff=Math.max(...pose.map((v,i)=>Math.abs(v-other.get(frame)![i])));assert.ok(diff<1e-6,`${style} at ${(frame/30).toFixed(3)} s differs by ${diff} between frame rates`);}
 }
});

test("Emperor's Oath is shot from the front: the camera faces the fighter's chest in the hero shot and at the name", ()=>{
 const style:IntroStyle='dev-emperor',r=probeRig('emperor'),camera=new T.PerspectiveCamera(40,16/9,.1,80),nameAt=introNameAt(style),end=introDuration(style);
 const ages=[nameAt];for(let t=2.4;t<end;t+=.2)ages.push(t);
 for(const t of ages){poseIntro(r,style,t);const {shot}=introCamera(camera,style,t,r.group.position,16/9);r.group.updateMatrixWorld(true);
  if(t!==nameAt)assert.equal(shot,2,`hero shot at ${t.toFixed(2)} s`);
  const chest=r.chest.getWorldPosition(new T.Vector3()),toCamera=camera.position.clone().sub(chest).setY(0).normalize(),facing=new T.Vector3(Math.sin(r.group.rotation.y),0,Math.cos(r.group.rotation.y));
  assert.ok(toCamera.dot(facing)>0,`rig faces away from the camera at ${t.toFixed(2)} s`);
  assert.ok(toCamera.dot(forward(r.chest))>.94,`chest is ${(Math.acos(toCamera.dot(forward(r.chest)))*180/Math.PI).toFixed(0)} deg off the camera at ${t.toFixed(2)} s`);
  assert.ok(toCamera.dot(forward(r.head))>.9,`face is ${(Math.acos(toCamera.dot(forward(r.head)))*180/Math.PI).toFixed(0)} deg off the camera at ${t.toFixed(2)} s`);
 }
});
