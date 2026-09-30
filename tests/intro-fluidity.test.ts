import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {WEAPON_INTROS,type WeaponIntro} from '../src/shared/weapon-intros.js';
import {INTRO_STYLES,type IntroStyle,introDuration,introNameAt} from '../src/shared/intros.js';
import {poseIntro,introCamera,INTRO_HIT} from '../src/client/intro-choreography.js';
import {keysFor} from '../src/client/weapon-intro-motion.js';
import {introCurve} from '../src/client/intro-curves.js';
import {WeaponIntroEffects} from '../src/client/weapon-intro-effects.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {probeMotion,probeRig,probeBase,describeProbe} from './helpers/motion-probe.js';
await initPhysics();
const weaponIntros=Object.keys(WEAPON_INTROS) as WeaponIntro[];
/** Ankles are measured against the world origin (poseIntro places the root itself): planted means within 1 cm of the floor. */
const FLOOR_PLANT=.085+.01;
const probeIntro=(style:IntroStyle,spec:string,hz=120,keepFrames=false)=>probeMotion({spec,player:(_t,b)=>b,animate:(r,_p,t)=>poseIntro(r,style,t),from:.02,to:introDuration(style)-.02,warm:0,hz,plantY:FLOOR_PLANT,rig:probeRig(spec),keepFrames});

test('weapon intro keys follow each weapon hit and keep the original 1.8 s timeline',()=>{
 assert.deepEqual(keysFor(1.8).map(k=>+k.toFixed(6)),[0,.5,1.05,1.45,1.8,2.12,2.65,3.2,4.8]);
 for(const style of weaponIntros){const hit=WEAPON_INTROS[style].hit,keys=keysFor(hit);assert.equal(keys[4],hit,style);assert.ok(keys.every((k,i)=>i===0||k>keys[i-1]),style);assert.equal(keys[7],introNameAt(style));assert.equal(keys[8],introDuration(style));}
});

test('intro curves: pass keys keep moving, impacts arrive travelling and ring out, drive keys carry speed into contact',()=>{
 const times=[0,1,2,3],values=[0,1,0,0],v=(t:number,o:object)=>introCurve(times,values,t,o);
 // A default reversal is a dead stop; a pass key keeps velocity through the neighbourhood of the key.
 const speed=(o:object,t:number)=>Math.abs(v(t+1e-3,o)-v(t-1e-3,o))/2e-3;
 const rise=[0,1,2,3],ramp=[0,1,2,2];
 assert.ok(Math.abs(introCurve(rise,ramp,1+1e-3,{pass:[1]})-introCurve(rise,ramp,1-1e-3,{pass:[1]}))>1e-3);
 assert.ok(speed({},1)<1e-2);
 // Impact: arrives at 1.3x the incoming slope, then settles back to the key (small, decaying ring-out).
 const impact={impact:[1]};assert.ok(Math.abs(v(1-1e-3,impact)-v(1-2e-3,impact))/1e-3>1.1,'arrives moving');
 const ring=[1.02,1.05,1.1,1.2,1.4].map(t=>v(t,impact)-v(t,{}));assert.ok(Math.max(...ring.map(Math.abs))<.06&&Math.abs(ring[4])<.005,String(ring));
 // Drive: the contact tangent carries speed without overshooting the follow-through key.
 const strike=[0,.35,.67],swing=[-1.6,.8,2];for(let t=.35;t<=.67;t+=.01)assert.ok(introCurve(strike,swing,t,{drive:[1]})<=2+1e-9);
 assert.ok(Math.abs(introCurve(strike,swing,.35+1e-3,{drive:[1]})-introCurve(strike,swing,.35-1e-3,{drive:[1]}))/2e-3>6);
 // Pure function of time: the same query always gives the same value.
 assert.equal(v(1.37,impact),v(1.37,impact));
});

test('weapon intros keep planted feet on the floor, have no IK pops and are stable across frame rates',()=>{
 for(const style of weaponIntros){const spec=WEAPON_INTROS[style].spec;
  const fast=probeIntro(style,spec,120),slow=probeIntro(style,spec,30);
  assert.ok(fast.maxFootSlip<.05,`${style} planted feet slide: ${describeProbe(fast)}`);
  assert.ok(fast.feet.left.plantedFrames>100&&fast.feet.right.plantedFrames>100,`${style} feet are planted most of the time: ${describeProbe(fast)}`);
  // No single-frame flip: at 120 Hz no bone turns faster than 60 rad/s (a snap keeps its size as the rate rises).
  assert.ok(fast.maxJointSpeedDegPerSec<60*180/Math.PI,`${style} joint pop: ${describeProbe(fast)}`);
  assert.ok(fast.maxJointStepDeg<slow.maxJointStepDeg+1e-6,`${style} per-frame change shrinks with the frame rate: ${describeProbe(fast)} vs ${describeProbe(slow)}`);
  assert.ok(fast.minAnkleY>FLOOR_PLANT-.02,`${style} feet never sink into the floor: ${describeProbe(fast)}`);
 }
});

test('the weapon is travelling at its hit mark (body mechanics timed to each weapon)',()=>{
 for(const style of weaponIntros){const spec=WEAPON_INTROS[style].spec,hit=WEAPON_INTROS[style].hit,report=probeIntro(style,spec,60,true),frames=report.samples!;
  const speeds=frames.slice(1).map((f,i)=>({t:f.t,v:f.tip&&frames[i].tip?f.tip.distanceTo(frames[i].tip!)*60:0}));
  const peak=Math.max(...speeds.map(s=>s.v)),around=Math.max(...speeds.filter(s=>Math.abs(s.t-hit)<=.12).map(s=>s.v));
  assert.ok(around>=.7*peak,`${style}: ${around.toFixed(2)} m/s at the hit vs ${peak.toFixed(2)} m/s peak`);
 }
});

test('the hero hold is alive but subtle, and every intro stays a pure function of age',()=>{
 for(const style of Object.keys(INTRO_STYLES) as IntroStyle[]){const r=createCharacterRig('#44ddff'),nameAt=introNameAt(style),end=introDuration(style);
  const sample=(t:number)=>{poseIntro(r,style,t);r.group.updateMatrixWorld(true);return [r.chest,r.head,r.pelvis,r.leftHand,r.rightHand].map(o=>o.getWorldQuaternion(new T.Quaternion()));};
  const a=sample(nameAt+.4),b=sample(end-.3),moved=Math.max(...a.map((q,i)=>q.angleTo(b[i])));
  if(!style.startsWith('dev-'))assert.ok(moved>.004,`${style} hero hold is frozen (${moved})`);
  // Late joins and rewinds: a sample after visiting other ages equals a direct sample.
  const probe=[.3,1.1,nameAt-.2,end-.4];for(const t of probe){poseIntro(r,style,t);r.group.updateMatrixWorld(true);const direct=r.rightHand.matrixWorld.clone();poseIntro(r,style,end*.73);poseIntro(r,style,.05);poseIntro(r,style,t);r.group.updateMatrixWorld(true);assert.deepEqual(r.rightHand.matrixWorld.elements,direct.elements,`${style} at ${t}`);}
 }
});

test('weapon-intro camera frames the action tightly and never shows the fighter smaller than 40% of frame height at the hit',()=>{
 for(const style of weaponIntros){const r=createCharacterRig('#44ddff'),camera=new T.PerspectiveCamera(40,16/9,.1,80),hit=WEAPON_INTROS[style].hit;
  for(const t of [hit-.3,hit,hit+.3]){poseIntro(r,style,t);introCamera(camera,style,t,r.group.position,16/9);r.group.updateMatrixWorld(true);camera.updateMatrixWorld(true);
   const head=r.head.getWorldPosition(new T.Vector3()).add(new T.Vector3(0,.2,0)).project(camera),feet=r.leftFoot.getWorldPosition(new T.Vector3()).project(camera);
   assert.ok((head.y-feet.y)/2>.4,`${style} @${t}: subject ${(head.y-feet.y)/2} of frame height`);assert.ok(Math.abs(head.x)<.9&&head.y<1,`${style} @${t}: head out of frame`);}
 }
});

test('generic intros: arc-salute walks on planted feet and neon-drop lands still travelling',()=>{
 const walk=probeIntro('arc-salute','sword',120);assert.ok(walk.maxFootSlip<.05,describeProbe(walk));
 const r=createCharacterRig('#44ddff');poseIntro(r,'neon-drop',INTRO_HIT['neon-drop']-.02);const before=r.group.position.y;poseIntro(r,'neon-drop',INTRO_HIT['neon-drop']-.01);assert.ok((before-r.group.position.y)/.01>4,'falls fast into the landing');
 poseIntro(r,'neon-drop',INTRO_HIT['neon-drop']+.1);const dip=r.body.position.y;poseIntro(r,'neon-drop',INTRO_HIT['neon-drop']+.6);assert.ok(dip<r.body.position.y-.05,'the legs absorb the landing');
});

test('dagger afterimages trail the choreography in time and leave the fighter posed at the present age',()=>{
 const r=probeRig('daggers'),style:WeaponIntro='weapon-daggers',fx=new WeaponIntroEffects(style,r),age=1.6;void probeBase;
 poseIntro(r,style,age);fx.update(age,r,1);r.group.updateMatrixWorld(true);const present=r.rightHand.getWorldPosition(new T.Vector3());
 const check=createCharacterRig('#ff6b35');poseIntro(check,style,age);check.group.updateMatrixWorld(true);assert.ok(present.distanceTo(check.rightHand.getWorldPosition(new T.Vector3()))<1e-9);
 const ghosts=fx.root.children.filter(o=>o.name==='phantom-afterimage');assert.equal(ghosts.length,3);
 ghosts.forEach((ghost,i)=>{poseIntro(check,style,age-(i+1)*.065);assert.ok(ghost.position.distanceTo(check.group.position)<1e-9);assert.ok(ghost.quaternion.angleTo(check.group.quaternion)<1e-6);});
 fx.dispose();
});
