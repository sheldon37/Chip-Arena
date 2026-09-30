import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {presentedYaw} from '../src/client/motion-quality.js';
import {probeBase,probeRig} from './helpers/motion-probe.js';

// The renderer does not feed the rig the way the lab and the shared probe do (root = p.vel integrated at the render rate,
// monotonic clock). These tests replay the real feeds: a jittery local animation clock that steps backward, a fixed 60 Hz
// sim drawn at 144 Hz, remote players interpolated behind the newest p.vel, and a local player whose p.vel/p.yaw are stale
// server values while the root is predicted and the rig yaw follows the camera.
await initPhysics();
type Frame={x:number;z:number;yaw:number;vx:number;vz:number;pyaw:number;anim:number};
type Replay={plantedSpeeds:number[];median:number;p90:number;max:number;maxLegStep:number;backsteps:number};
const LEGS=['leftLeg','leftKnee','leftFoot','rightLeg','rightKnee','rightFoot','body','pelvis'] as const;
const pct=(xs:number[],q:number)=>{const s=[...xs].sort((a,b)=>a-b);return s.length?s[Math.min(s.length-1,Math.floor(s.length*q))]:0;};
/** Renders `feed` at `hz` from t=1 for `seconds` (measured after `settle` s) and reports planted-ankle world speeds
 * (ankle within 1 cm of the floor on consecutive frames) and the largest per-frame leg/body joint rotation. */
function replay(hz:number,seconds:number,feed:(t:number)=>Frame,{spec='sword',settle=.5,rig=probeRig(spec)}:{spec?:string;settle?:number;rig?:ReturnType<typeof probeRig>}={}):Replay{
 const base=probeBase(spec),prev=[new T.Vector3(),new T.Vector3()],was=[false,false],speeds:number[]=[],pq=LEGS.map(()=>new T.Quaternion()),q=new T.Quaternion();
 let maxLegStep=0,backsteps=0,last=-Infinity;
 for(let i=0;i<=Math.round(hz*seconds);i++){
  const t=1+i/hz,f=feed(t),measured=i>0&&t>=1+settle;if(f.anim<last)backsteps++;last=f.anim;
  const p={...animationPreviewPlayer({...base},'idle',f.anim),hitStunUntil:-1e9,vel:{x:f.vx,y:0,z:f.vz},yaw:f.pyaw,animationTime:f.anim} as PlayerView;
  rig.group.position.set(f.x,0,f.z);rig.group.rotation.y=f.yaw;animateCharacter(rig,p,f.anim,1/hz,0);rig.group.updateMatrixWorld(true);
  LEGS.forEach((b,k)=>{rig[b].getWorldQuaternion(q);if(measured)maxLegStep=Math.max(maxLegStep,q.angleTo(pq[k])*180/Math.PI);pq[k].copy(q);});
  [rig.leftFoot,rig.rightFoot].forEach((foot,k)=>{const a=foot.getWorldPosition(new T.Vector3()),planted=a.y<-.785;if(measured&&planted&&was[k])speeds.push(Math.hypot(a.x-prev[k].x,a.z-prev[k].z)*hz);was[k]=planted;prev[k].copy(a);});
 }
 return {plantedSpeeds:speeds,median:pct(speeds,.5),p90:pct(speeds,.9),max:pct(speeds,1),maxLegStep,backsteps};
}
const show=(r:Replay)=>`planted median ${r.median.toFixed(2)} p90 ${r.p90.toFixed(2)} max ${r.max.toFixed(2)} m/s, leg step ${r.maxLegStep.toFixed(1)} deg/frame, ${r.backsteps} backward clock steps`;
/** A 60 Hz sim accelerating at 35 m/s^2 (config) toward want(t) = [heading, speed]. */
function simulate(want:(t:number)=>[number,number],seconds=8){
 const out:{t:number;x:number;z:number;vx:number;vz:number;yaw:number}[]=[];let x=0,z=0,vx=0,vz=0;
 for(let i=0;i<=60*seconds;i++){const t=i/60,[yaw,s]=want(t);out.push({t,x,z,vx,vz,yaw});const wx=Math.sin(yaw)*s,wz=Math.cos(yaw)*s,dx=wx-vx,dz=wz-vz,d=Math.hypot(dx,dz),a=35/60;
  if(d>a){vx+=dx/d*a;vz+=dz/d*a;}else{vx=wx;vz=wz;}x+=vx/60;z+=vz/60;}
 const at=(t:number)=>{const i=Math.min(out.length-2,Math.max(0,Math.floor(t*60))),u=t*60-i,a=out[i],b=out[i+1];return {x:a.x+(b.x-a.x)*u,z:a.z+(b.z-a.z)*u};};
 return {ticks:out,at};
}
const hash=(k:number)=>{let h=2166136261^k;h=Math.imul(h^(h>>>13),16777619);h^=h>>>15;return ((h>>>0)%1000)/1000;};
/** The local player's clock (snapshots.ts): newest 20 Hz snapshot time + clamp(now - arrival, 0, .08), with per-packet
 * arrival delay `latency` + up to `jitter` s, so it steps back when a snapshot lands late. Returns [clock, snapshot index]. */
function localClock(t:number,latency:number,jitter:number):[number,number]{
 const arrive=(k:number)=>k*.05+latency+hash(k)*jitter;let k=Math.floor((t-latency)/.05)+1;while(arrive(k)>t)k--;
 return [k*.05+Math.min(Math.max(0,t-arrive(k)),.08),k];
}

test('a jittery local animation clock (backward steps of 5-30 ms) holds the gait: no leg snaps and no slip spikes',()=>{
 for(const hz of [60,144]){
  const run=(anim:(t:number)=>number)=>replay(hz,3,t=>({x:0,z:6.8*t,yaw:0,vx:0,vz:6.8,pyaw:0,anim:anim(t)}));
  const clean=run(t=>t),jittery=run(t=>localClock(t,.03,.03)[0]+.03);
  assert.ok(jittery.backsteps>=4,`${hz} Hz: the feed steps the clock backward (${jittery.backsteps})`);
  // Before: every backward step re-seated the feet and reset the springs (75-90 deg/frame leg snaps, .16-.2 m slip).
  assert.ok(jittery.maxLegStep<=clean.maxLegStep+.5,`${hz} Hz jittery ${show(jittery)} vs clean ${show(clean)}`);
  assert.ok(jittery.max<=clean.max+.3&&jittery.p90<=clean.p90+.1,`${hz} Hz planted feet: jittery ${show(jittery)} vs clean ${show(clean)}`);
 }
 // The presented yaw keeps easing through a small backward step instead of snapping to the new target.
 const rig={},p={...probeBase('sword'),hitReaction:{kind:'hit',at:.5,until:.74,yaw:1.2,direction:{x:0,y:0,z:1},point:{x:0,y:0,z:0},zone:'torso',turn:0,strength:1},hitStunUntil:1} as PlayerView;
 const yaws=[.98,1,1.03,1.01,1.05].map(t=>presentedYaw(rig,p,t,0));
 assert.ok(Math.abs(yaws[3]-yaws[2])<.02,`no snap on a 20 ms step back: ${yaws.map(y=>y.toFixed(3)).join(' ')}`);
 assert.ok(yaws[4]<yaws[2]&&yaws[4]>0,`the ease continues afterwards: ${yaws.map(y=>y.toFixed(3)).join(' ')}`);
 // A real rewind (new round, > .1 s back) still starts clean.
 assert.equal(presentedYaw(rig,p,.2,0),0);
});

test('planted feet lock to the displayed root when a fixed 60 Hz sim is drawn at 144 Hz',()=>{
 for(const [hz,sim] of [[144,60],[60,60],[120,60]] as const){
  const r=replay(hz,3,t=>{const s=Math.floor(t*sim+1e-9)/sim;return {x:0,z:6.8*s,yaw:0,vx:0,vz:6.8,pyaw:0,anim:s};});
  const still=r.plantedSpeeds.filter(v=>v/hz<=.005).length/r.plantedSpeeds.length;
  // Before: the planted feet integrated p.vel*dt at the render rate against a root that moves in 11 cm sim steps (4.7 cm
  // median world step per frame at 144 Hz). Only the touchdown frame itself still rides the root's sim step.
  assert.ok(r.median/hz<=.001&&still>=.85,`${hz} Hz render / ${sim} Hz sim: ${(still*100).toFixed(0)}% still, ${show(r)}`);
 }
});

test('remote players: planted feet stay put although p.vel leads the interpolated root by 0.1-0.2 s',()=>{
 // A/D strafe jukes every .5 s; display interpolates 20 Hz snapshots `delay` s behind; p.vel is the NEWEST snapshot's.
 const sim=simulate(t=>[Math.PI/2,Math.floor(t/.5)%2?-6.8:6.8]);
 for(const hz of [60,144])for(const delay of [.1,.2]){
  const r=replay(hz,4,t=>{const c=t-delay,a=Math.floor(c*20)/20,sa=sim.ticks[Math.round(a*60)],sb=sim.ticks[Math.round((a+.05)*60)],u=(c-a)/.05,n=sim.ticks[Math.floor(t*20)*3];
   return {x:sa.x+(sb.x-sa.x)*u,z:sa.z+(sb.z-sa.z)*u,yaw:0,vx:n.vx,vz:n.vz,pyaw:0,anim:c};});
  // Before: median 2.3 m/s (5.8 at .2 s), p90 4.1-7.6 m/s: the feet skated after every direction change.
  assert.ok(r.median<=.2&&r.p90<=.6,`${hz} Hz delay ${delay}: ${show(r)}`);
 }
});

test('local player: stale snapshot p.vel/p.yaw while the camera turns the rig and the predicted root runs',()=>{
 // Turn left at 1.6 rad/s, sweep back right, stop and restart; the server view is 60 ms old at 20 Hz.
 const cam=(t:number)=>t<2?0:t<3?(t-2)*1.6:t<4?1.6-(t-3)*2.4:-.8,sim=simulate(t=>[cam(t),t>4.6&&t<5.2?0:6.8]);
 for(const [hz,cap] of [[60,32],[144,20]] as const){
  const r=replay(hz,5,t=>{const [anim,k]=localClock(t,.06,0),n=sim.ticks[k*3],pos=sim.at(t);return {x:pos.x,z:pos.z,yaw:cam(t),vx:n.vx,vz:n.vz,pyaw:n.yaw,anim};});
  // Before: p90 2.3 m/s planted-foot speed and 40 deg/frame leg steps (feet moved along the stale p.yaw frame).
  assert.ok(r.median<=.2&&r.p90<=.6,`${hz} Hz: ${show(r)}`);
  assert.ok(r.maxLegStep<=cap,`${hz} Hz leg steps: ${show(r)}`);
 }
});

test('a teleport re-seats the feet, and a static root with a velocity still runs on a treadmill',()=>{
 const rig=probeRig('sword');let away=0;
 replay(60,1,t=>({x:0,z:6.8*t,yaw:0,vx:0,vz:6.8,pyaw:0,anim:t}),{rig,settle:0});
 // One frame after a 5 m jump (respawn, correction): no foot is left behind at the old spot.
 const p={...animationPreviewPlayer({...probeBase('sword')},'idle',2+1/60),hitStunUntil:-1e9,vel:{x:0,y:0,z:6.8}} as PlayerView;
 rig.group.position.set(5,0,6.8*(2+1/60));animateCharacter(rig,p,2+1/60,1/60,0);rig.group.updateMatrixWorld(true);
 // Locked to the old spot, both legs would stretch 5 m sideways (ankles pulled ~.3 m off their track toward -x).
 for(const f of [rig.leftFoot,rig.rightFoot]){const a=rig.group.worldToLocal(f.getWorldPosition(new T.Vector3()));away=Math.max(away,Math.abs(a.x));}
 assert.ok(away<.2,`feet stay on their track right after a 5 m jump (${away.toFixed(2)} m sideways)`);
 // Previews and the lab without root travel keep the root still: the planted feet slide back at the ground speed.
 const tread=probeRig('sword'),back:number[]=[];let last:number|undefined;
 replay(60,2,t=>({x:0,z:0,yaw:0,vx:0,vz:6.8,pyaw:0,anim:t}),{rig:tread,settle:0});
 for(let i=0;i<60;i++){const t=3+i/60,base=probeBase('sword'),p={...animationPreviewPlayer({...base},'idle',t),hitStunUntil:-1e9,vel:{x:0,y:0,z:6.8}} as PlayerView;
  animateCharacter(tread,p,t,1/60,0);tread.group.updateMatrixWorld(true);const a=tread.leftFoot.getWorldPosition(new T.Vector3());if(a.y<-.785&&last!==undefined)back.push((a.z-last)*60);last=a.y<-.785?a.z:undefined;}
 const med=pct(back,.5);assert.ok(back.length>3&&med<-4.5&&med>-9,`treadmill planted-foot speed ${med.toFixed(2)} m/s (${back.length} frames)`);
});
