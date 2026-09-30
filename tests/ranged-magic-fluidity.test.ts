import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {poseFirstPersonView} from '../src/client/first-person-view.js';
import {markFirstPersonRig} from '../src/client/pose-flags.js';
import {POCKET,reloadPhase} from '../src/client/ranged-motion.js';
import {probeBase,probeMotion,probeRig} from './helpers/motion-probe.js';

// Ranged/magic presentation fluidity (ranged-magic workstream). Presentation only: every scenario drives existing
// PlayerView fields (reloadTime, guarding, lastAttack/lastShot, vel, pitch) through the real rig.
await initPhysics();
const GUNS=['pistol','rifle','machinegun'] as const,MAGIC=['fire','water','lightning','darkness'] as const;
const idle=(spec:string,t:number,patch:Partial<PlayerView>={})=>({...animationPreviewPlayer(probeBase(spec),'idle',t),hitStunUntil:-1e9,...patch}) as PlayerView;

test('reload fades in and out of the aim without a pose pop, and the magazine always comes back',()=>{
 for(const spec of GUNS){
  const R=WEAPONS[spec].reload,patch=(t:number)=>({reloadTime:t>=1&&t<1+R?1+R-t:0});
  const start=probeMotion({spec,mode:'idle',from:.95,to:1.25,patch}),end=probeMotion({spec,mode:'idle',from:1+R-.25,to:1+R+.15,patch});
  assert.ok(start.maxJointStepDeg<15,`${spec} reload start ${start.maxJointStepDeg.toFixed(1)} deg/frame on ${start.maxJointStepBone}`);
  assert.ok(end.maxJointStepDeg<15,`${spec} reload end ${end.maxJointStepDeg.toFixed(1)} deg/frame on ${end.maxJointStepBone}`);
  // Mid-reload the magazine is off its socket; after the reload, and after a cancelled reload, it is back in place.
  const r=probeRig(spec),mag=r.weapon.getObjectByName('magazine')!,rest=mag.position.clone();let moved=false;
  for(let i=0;i<=Math.round((R+.4)*60);i++){const t=1+i/60,p=idle(spec,t,{reloadTime:t<1+R?1+R-t:0});animateCharacter(r,p,t,1/60);const u=reloadPhase(p).u;if(u>.22&&u<.6&&mag.position.distanceTo(rest)>.05)moved=true;}
  assert.ok(moved,spec+' magazine leaves the gun during the reload');
  assert.ok(mag.position.distanceTo(rest)<1e-9&&mag.visible,spec+' magazine restored after the reload');
  const c=probeRig(spec),cm=c.weapon.getObjectByName('magazine')!;
  for(let i=0;i<60;i++){const t=1+i/60;animateCharacter(c,idle(spec,t,{reloadTime:t<1.5?R-(t-1):0}),t,1/60);}
  assert.ok(cm.position.distanceTo(rest)<1e-9&&cm.visible,spec+' magazine restored when the reload is cut short');
 }
});

test('the carried magazine rides in the support hand while it is fetched and seated',()=>{
 for(const spec of GUNS){
  const R=WEAPONS[spec].reload,r=probeRig(spec),mag=r.weapon.getObjectByName('magazine')!;let worst=0,n=0;
  for(let i=0;i<=Math.round(R*60);i++){const t=1+i/60,p=idle(spec,t,{reloadTime:1+R-t});animateCharacter(r,p,t,1/60);const u=reloadPhase(p).u;
   if(u>.52&&u<.6){r.group.updateMatrixWorld(true);worst=Math.max(worst,r.leftHand.getWorldPosition(new T.Vector3()).distanceTo((mag.children[0]??mag).getWorldPosition(new T.Vector3())));n++;}}
  assert.ok(n>0&&worst<.2,`${spec} magazine to support hand ${worst.toFixed(3)} m`);
 }
});

test('guard entry and exit are blended (no single-frame snap into the guard)',()=>{
 for(const spec of [...GUNS,...MAGIC]){
  const rep=probeMotion({spec,mode:'idle',from:.95,to:1.8,patch:(t:number)=>({guarding:t>=1&&t<1.4})});
  assert.ok(rep.maxJointStepDeg<(WEAPONS[spec].class==='magic'?20:15),`${spec} guard ${rep.maxJointStepDeg.toFixed(1)} deg/frame on ${rep.maxJointStepBone}`);
 }
});

test('magic recasts carry on from the previous phrase: per-frame steps shrink with the frame rate (no snaps)',()=>{
 for(const spec of MAGIC){
  const I=WEAPONS[spec].interval,casts=[1,1+I,1+2*I,1+3*I],cw=probeBase(spec)&&(animationPreviewPlayer(probeBase(spec),'attack',0).lastShot!-animationPreviewPlayer(probeBase(spec),'attack',0).lastAttack);
  const patch=(t:number)=>{const l=[...casts].reverse().find(c=>c<=t),rel=[...casts].reverse().find(c=>c+cw<=t);return l===undefined?{}:{lastAttack:l,lastShot:rel===undefined?-10:rel+cw};};
  const at60=probeMotion({spec,hz:60,mode:'idle',from:.95,to:1+4*I+.8,patch}),at120=probeMotion({spec,hz:120,mode:'idle',from:.95,to:1+4*I+.8,patch});
  assert.ok(at120.maxJointStepDeg<.72*at60.maxJointStepDeg,`${spec} recast steps ${at60.maxJointStepDeg.toFixed(1)} @60 vs ${at120.maxJointStepDeg.toFixed(1)} @120`);
 }
});

test('running keeps the arm pump but the gun holds a stable carry (no vertical or backward barrel)',()=>{
 for(const spec of GUNS){
  const r=probeRig(spec),base=probeBase(spec);let minPitch=Infinity,maxPitch=-Infinity,minZ=Infinity;
  for(let n=0;n<120;n++){const t=n/60;animateCharacter(r,{...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9},t,1/60);if(n<40)continue;r.group.updateMatrixWorld(true);
   const d=new T.Vector3(0,0,1).applyQuaternion(r.weapon.getWorldQuaternion(new T.Quaternion()));minPitch=Math.min(minPitch,Math.asin(d.y));maxPitch=Math.max(maxPitch,Math.asin(d.y));minZ=Math.min(minZ,d.z);}
  assert.ok(minPitch>-60*Math.PI/180&&maxPitch<15*Math.PI/180,`${spec} barrel pitch ${(minPitch*57.3).toFixed(0)}..${(maxPitch*57.3).toFixed(0)} deg`);
  assert.ok(minZ>.3,`${spec} barrel never points back (min z ${minZ.toFixed(2)})`);
 }
});

test('rifle and machine gun are shouldered: butt in the pocket, support hand on the grip, barrel on the aim',()=>{
 for(const spec of ['rifle','machinegun'])for(const speed of [0,6.8])for(const pitch of [-.7,0,.7]){
  const r=probeRig(spec);for(let n=0;n<40;n++){const t=2+n/60;animateCharacter(r,idle(spec,t,{pitch,vel:{x:0,y:0,z:speed},lastShot:t-.3,lastAttack:t-.3}),t,1/60,pitch);}
  r.group.updateMatrixWorld(true);
  const butt=r.chest.worldToLocal(r.weapon.getObjectByName('stock-butt')!.getWorldPosition(new T.Vector3()));
  assert.ok(butt.distanceTo(POCKET)<.05,`${spec} butt ${butt.distanceTo(POCKET).toFixed(3)} m from the shoulder pocket (v=${speed}, pitch ${pitch})`);
  const grip=r.leftHand.getWorldPosition(new T.Vector3()).distanceTo(r.weapon.getObjectByName('support-grip')!.getWorldPosition(new T.Vector3()));
  assert.ok(grip<.03,`${spec} support grip ${grip.toFixed(3)}`);
  const d=new T.Vector3(0,0,1).applyQuaternion(r.weapon.getWorldQuaternion(new T.Quaternion()));
  assert.ok(Math.abs(Math.asin(d.y)-pitch)<.08,`${spec} barrel pitch ${Math.asin(d.y).toFixed(2)} vs aim ${pitch}`);
 }
 // Isosceles pistol: both hands on the gun.
 const r=probeRig('pistol');for(let n=0;n<40;n++){const t=2+n/60;animateCharacter(r,idle('pistol',t),t,1/60);}
 r.group.updateMatrixWorld(true);assert.ok(r.aimWeight>.99);
 assert.ok(r.leftHand.getWorldPosition(new T.Vector3()).distanceTo(r.weapon.getObjectByName('support-grip')!.getWorldPosition(new T.Vector3()))<.03,'pistol support hand on the grip');
});

test('gun and cast layers are deterministic and consistent across 30/60/120 Hz',()=>{
 const scenario=(hz:number)=>{const r=probeRig('machinegun');for(let i=0;i<=hz*1.5;i++){const t=1+i/hz,n=Math.floor((t-1.1)/.085),l=t<1.1?-10:1.1+n*.085;animateCharacter(r,idle('machinegun',t,{lastShot:l,lastAttack:l,guarding:t>1.3&&t<1.9}),t,1/hz);}r.group.updateMatrixWorld(true);return r.weapon.getWorldQuaternion(new T.Quaternion());};
 assert.ok(scenario(60).angleTo(scenario(60))<1e-9,'same inputs, same pose');
 assert.ok(scenario(30).angleTo(scenario(120))<.05,'frame-rate independent');
});

test('first-person melee keeps the striking hand on screen',()=>{
 for(const spec of ['greatsword','daggers','sword','shield']){
  const cam=new T.PerspectiveCamera(75,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
  const base=probeBase(spec);let n=0,on=0;
  for(let i=0;i<=150;i++){const t=i/60;poseFirstPersonView(r,root,{...animationPreviewPlayer(base,'attack',t),hitStunUntil:-1e9},t,1/60,false);if(i<30)continue;cam.updateMatrixWorld(true);
   const h=r.rightHand.getWorldPosition(new T.Vector3()).project(cam);n++;if(Math.abs(h.x)<=1&&Math.abs(h.y)<=1)on++;}
  assert.ok(on/n>=.8,`${spec} right hand on screen ${(100*on/n).toFixed(0)}%`);
 }
});

test('first-person gun barrels point along the camera aim, idle and firing',()=>{
 for(const spec of GUNS)for(const mode of ['idle','attack'] as const){
  const cam=new T.PerspectiveCamera(75,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
  const muzzle=r.weapon.getObjectByName('muzzle');assert.ok(muzzle,`${spec} has a muzzle marker`);
  const a=new T.Vector3(),b=new T.Vector3();let worstYaw=0,sum=0,n=0;
  for(let i=0;i<=120;i++){const t=1+i/60;poseFirstPersonView(r,root,{...animationPreviewPlayer(probeBase(spec),mode,t),hitStunUntil:-1e9},t,1/60,false);if(i<30)continue;cam.updateMatrixWorld(true);
   muzzle!.getWorldPosition(a);r.weapon.getWorldPosition(b);const d=a.sub(b).normalize();
   // Yaw across the view is the defect axis (every frame); the recoil rise is vertical and intended, so the total angle is bounded on average.
   worstYaw=Math.max(worstYaw,Math.abs(Math.atan2(d.x,-d.z))*180/Math.PI);sum+=Math.acos(Math.max(-1,Math.min(1,-d.z)))*180/Math.PI;n++;}
  assert.ok(worstYaw<5,`${spec} ${mode}: first-person barrel yawed ${worstYaw.toFixed(1)} deg across the view`);
  assert.ok(sum/n<5,`${spec} ${mode}: first-person barrel ${(sum/n).toFixed(1)} deg off camera forward on average`);
 }
});
