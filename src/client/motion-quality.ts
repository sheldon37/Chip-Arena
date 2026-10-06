import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {poseCurve} from './pose-curves.js';
import {legsAuthored,legsLayered,markLegsLayered,isFirstPersonRig} from './pose-flags.js';
import {stanceFor} from './stances.js';
import {committed,rolling} from '../shared/souls-combat.js';
const clamp=T.MathUtils.clamp,lerp=T.MathUtils.lerp,TAU=Math.PI*2,FLOOR=-.795,REACH=.485,LAND_REACH=.47;
/** Stateful presentation clocks reset only on a real rewind (> REWIND s back) or a jump forward (> .35 s). Smaller backward
 * steps are jitter (the local player's clock is newest snapshot + up to 80 ms extrapolation, so it steps back when a
 * snapshot lands late) and hold the state: the stateful clock stays at its latest value and dt keeps integrating. */
export const REWIND=.1;
export const timeJumped=(prev:number,time:number)=>time<prev-REWIND||time-prev>.35;
/** Root travel above this in one frame is a teleport (spawn, respawn, correction): the feet re-seat instead of locking. */
const TELEPORT=1.2,ROOT_VEL_RATE=20;
export const smoothMotion=(x:number)=>{x=clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);};
const wrapAngle=(a:number)=>a-TAU*Math.round(a/TAU),frac=(x:number)=>x-Math.floor(x);
const hermite=(p0:number,p1:number,m0:number,m1:number,s:number)=>{const s2=s*s,s3=s2*s;return (2*s3-3*s2+1)*p0+(s3-2*s2+s)*m0+(3*s2-2*s3)*p1+(s3-s2)*m1;};
/** Gait tuning (presentation only; ANIMATION.gait exposes the same object). Distances are metres in rig-group space,
 * where +z is the rig's forward, +x its left and planted ankles sit at y=-.795.
 *  cycle*       cycle length D (two steps) = clamp(cycleBase+cyclePerSpeed*speed, ..cycleMax) up to runSpeed, then growing to
 *               dashCycle at dashSpeed. Cadence = speed/D cycles/s: 2.8 m/s ~4.5 steps/s, 6.8 m/s ~6.5 steps/s.
 *  stance       planted ankle travel per step; the contact fraction is stance/D within [contactMin,contactMax], so a
 *               planted foot moves at exactly -velocity relative to the hips (no skating) while it is on the floor.
 *  stanceBias   the stance centre sits this far behind the hips at a sprint (land under the body, push off behind).
 *  settle*      cadence and lift of the steps taken in place (stopping, turning, stance changes).
 *  runLean/runCrouch/blendSpeed   forward lean (rad), extra knee bend while running (m), locomotion blend rate (1/s).
 *  maxCadence   cycles/s cap: dash bursts lengthen the stride (a bound) instead of pumping the legs faster.
 *  turnDeadZone planted feet may twist this far (rad) from the aim before the legs step round; travelYawMax clamps the
 *               hip turn toward the travel direction; legYawOmega is the hip-follow spring (rad/s, critically damped). */
export const GAIT={cycleBase:.62,cyclePerSpeed:.22,cycleMax:2.1,runSpeed:6.8,dashCycle:2.6,dashSpeed:15,stance:.42,stanceBias:.05,contactMin:.18,contactMax:.62,settleCadence:2.3,settleLift:.05,runLean:.17,runCrouch:.025,blendSpeed:13,turnDeadZone:.7,travelYawMax:.7,legYawOmega:10,maxCadence:3.7};
/** Gait cycle length (m per two steps) at a ground speed. */
export function cycleLength(speed:number){const g=GAIT;return speed<=g.runSpeed?clamp(g.cycleBase+g.cyclePerSpeed*speed,g.cycleBase,g.cycleMax):lerp(g.cycleMax,g.dashCycle,clamp((speed-g.runSpeed)/(g.dashSpeed-g.runSpeed),0,1));}
/** Per-foot gait state in rig-group space: x/y/z/yaw/pitch/toe is the live foot, f* the lift-off point, l* the landing
 * target and s* the last solved ankle target (what the ground pass re-solves to). */
type Foot={x:number;y:number;z:number;yaw:number;pitch:number;toe:number;planted:boolean;fx:number;fy:number;fz:number;fyaw:number;uLift:number;lx:number;lz:number;lyaw:number;sx:number;sy:number;sz:number;syaw:number;spitch:number};
type Travel={time:number;raw:number;px:number;pz:number;still:number;rvx:number;rvz:number;clock:number;forward:number;strafe:number;lean:number;bank:number;vx:number;vz:number;mvx:number;mvz:number;yaw:number;feet:[Foot,Foot];hipYaw:number;legYaw:number;legYawV:number;settling:boolean;airAt:number;airVy:number;airLead:number;onGround:boolean;phaseOffset:number;landAt:number;landDepth:number;dashLean:number;phase:number;armPrev:[number,number];hand:[number,number,number,number];legQ:T.Quaternion[];hips:[T.Vector3,T.Vector3];solvedBody:[T.Vector3,T.Quaternion,T.Quaternion];solved:boolean;id:string;fresh:boolean};
const travelState=new WeakMap<CharacterRig,Travel>();
const newFoot=(x:number,z:number,yaw:number):Foot=>({x,y:FLOOR,z,yaw,pitch:0,toe:0,planted:true,fx:x,fy:FLOOR,fz:z,fyaw:yaw,uLift:0,lx:x,lz:z,lyaw:yaw,sx:x,sy:FLOOR,sz:z,syaw:yaw,spitch:0});
/** Idle stance ankle target (group space) for the left/right foot from stances.ts. */
function idleFoot(spec:string,left:boolean){const st=stanceFor(spec),f=left===st.leadLeft?st.lead:st.rear;return {x:f.x,z:f.z,yaw:(left?1:-1)*f.toeOut};}
type IdleFoot={x:number;z:number;yaw:number};const _idle:[IdleFoot,IdleFoot]=[{x:0,z:0,yaw:0},{x:0,z:0,yaw:0}];
/** Both idle feet in module scratch (no per-frame allocation; read before the next call). */
function idleFeet(spec:string){const st=stanceFor(spec);for(let i=0;i<2;i++){const left=i===0,f=left===st.leadLeft?st.lead:st.rear,o=_idle[i];o.x=f.x;o.z=f.z;o.yaw=(left?1:-1)*f.toeOut;}return _idle;}
function hashPhase(id:string){let h=2166136261;for(let i=0;i<id.length;i++){h^=id.charCodeAt(i);h=Math.imul(h,16777619);}return ((h>>>0)%9973)/9973*TAU;}
// Module scratch objects (no per-frame allocation).
const UP=new T.Vector3(0,1,0),X_AXIS=new T.Vector3(1,0,0),_pq=new T.Quaternion(),_pp=new T.Vector3(),_hip=new T.Vector3(),_dir=new T.Vector3(),_pole=new T.Vector3(),_perp=new T.Vector3(),_fwd=new T.Vector3(),_knee=new T.Vector3(),_t=new T.Vector3(),_sh=new T.Vector3(),_x=new T.Vector3(),_y=new T.Vector3(),_z=new T.Vector3(),_m=new T.Matrix4(),_qh=new T.Quaternion(),_qi=new T.Quaternion(),_qf=new T.Quaternion(),_e=new T.Euler(),_target=new T.Vector3(),_air=new T.Vector3(),_q1=new T.Quaternion(),_q2=new T.Quaternion(),_q3=new T.Quaternion(),_q4=new T.Quaternion(),_ry=new T.Quaternion();
const _cap=[new T.Vector3(),new T.Vector3()],_capQ=[new T.Quaternion(),new T.Quaternion()],_capK=[new T.Vector3(),new T.Vector3()];
const makeLegs=(r:CharacterRig)=>[[r.leftLeg,r.leftKnee,r.leftFoot,r.leftToe],[r.rightLeg,r.rightKnee,r.rightFoot,r.rightToe]] as const;
const _legs=new WeakMap<CharacterRig,ReturnType<typeof makeLegs>>();
/** Hip, knee, ankle and toe joints of one leg (cached per rig). */
const legBones=(r:CharacterRig,left:boolean)=>{let l=_legs.get(r);if(!l){l=makeLegs(r);_legs.set(r,l);}return l[left?0:1];};
/** Pelvis orientation/position in rig-group space from the live body and pelvis joints. */
function pelvisFrame(r:CharacterRig){_pq.copy(r.body.quaternion).multiply(r.pelvis.quaternion);_pp.copy(r.pelvis.position).applyQuaternion(r.body.quaternion).add(r.body.position);}
/** Two-bone leg solve in rig-group space: the knee hinges toward the foot heading, so the knee joint is a pure x rotation
 * (no knee flips), and the foot takes a group-space heading/pitch (or an exact orientation when `orient` is given). */
function solveLeg(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,pitch:number,toe:number,orient?:T.Quaternion,knee0?:T.Vector3){
 const [hip,knee,ankle,toeJ]=legBones(r,left);pelvisFrame(r);_hip.copy(hip.position).applyQuaternion(_pq).add(_pp);
 _dir.subVectors(target,_hip);let d=_dir.length();if(d<1e-6){_dir.set(0,-1,0);d=.1;}else _dir.divideScalar(d);d=clamp(d,.1,.497);
 _fwd.set(0,0,1).applyQuaternion(_pq);if(knee0)_pole.subVectors(knee0,_hip);else _pole.set(Math.sin(yaw),0,Math.cos(yaw)).multiplyScalar(.7).addScaledVector(_fwd,.3);
 _perp.copy(_pole).addScaledVector(_dir,-_pole.dot(_dir));if(_perp.lengthSq()<1e-8)_perp.copy(_fwd).addScaledVector(_dir,-_fwd.dot(_dir));_perp.normalize();
 const half=d/2;_knee.copy(_hip).addScaledVector(_dir,half).addScaledVector(_perp,Math.sqrt(Math.max(0,.0625-half*half)));
 _t.subVectors(_knee,_hip).normalize();_y.copy(_t).negate();_z.copy(_perp).addScaledVector(_t,-_perp.dot(_t)).normalize();_x.crossVectors(_y,_z);
 _qh.setFromRotationMatrix(_m.makeBasis(_x,_y,_z));hip.quaternion.copy(_pq).invert().multiply(_qh);
 _sh.copy(_hip).addScaledVector(_dir,d).sub(_knee).applyQuaternion(_qi.copy(_qh).invert());knee.quaternion.setFromAxisAngle(X_AXIS,Math.atan2(-_sh.z,-_sh.y));
 if(orient)_qf.copy(orient);else _qf.setFromEuler(_e.set(pitch,yaw,0,'YXZ'));
 ankle.quaternion.copy(_qh).multiply(knee.quaternion).invert().multiply(_qf);if(!orient)toeJ.rotation.set(toe,0,0);
}
const _keep=[new T.Quaternion(),new T.Quaternion(),new T.Quaternion()],_kneeNow=new T.Vector3();
/** This sample's gait target for a foot in rig-group space (the ankle, heading, pitch and toe the ground pass holds), or
 * undefined before locomotion has run on the rig. The returned object is module scratch (read it before the next call). */
export type GaitFoot={x:number;y:number;z:number;yaw:number;pitch:number;toe:number;planted:boolean};
const _gaitFoot:GaitFoot={x:0,y:0,z:0,yaw:0,pitch:0,toe:0,planted:true};
export function gaitFoot(r:CharacterRig,left:boolean):GaitFoot|undefined{
 const s=travelState.get(r);if(!s)return undefined;const f=s.feet[left?0:1],o=_gaitFoot;
 o.x=f.sx;o.y=f.sy;o.z=f.sz;o.yaw=f.syaw;o.pitch=f.spitch;o.toe=f.toe;o.planted=f.planted;return o;
}
/** A leg layered on the gait (DEV fighting styles): solved onto a rig-group ankle target exactly as the gait solves its legs
 * (knee hinged toward the foot heading, floor-clamped by the caller), so a target equal to gaitFoot() reproduces the gait leg.
 * `orient` overrides the foot's group orientation; `pole` (a group-space direction) turns the knee plane away from the gait's
 * by `poleWeight`, so the knee never swaps planes in one frame. Marks the legs as layered for the ground pass. */
export function layerLeg(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,pitch:number,toe:number,orient?:T.Quaternion,pole?:T.Vector3,poleWeight=0){
 let knee:T.Vector3|undefined;
 if(pole&&poleWeight>0){
  const [hip]=legBones(r,left);pelvisFrame(r);_kneeNow.copy(hip.position).applyQuaternion(_pq).add(_pp);
  _fwd.set(0,0,1).applyQuaternion(_pq);_perp.set(Math.sin(yaw),0,Math.cos(yaw)).multiplyScalar(.7).addScaledVector(_fwd,.3).normalize();
  _perp.lerp(_t.copy(pole).normalize(),clamp(poleWeight,0,1));knee=_kneeNow.add(_perp);
 }
 solveLeg(r,left,target,yaw,pitch,toe,orient,knee);if(orient)legBones(r,left)[3].rotation.set(toe,0,0);markLegsLayered(r);
}
/** Blend a leg toward a planted rig-group ankle target (knee-hinged IK, flat foot with the given heading) by weight. */
export function plantLeg(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,weight:number,footWeight=weight){
 if(weight<=1e-4)return;const [hip,knee,ankle]=legBones(r,left);_keep[0].copy(hip.quaternion);_keep[1].copy(knee.quaternion);
 // The knee keeps bending the way it already points and only turns toward the foot heading as footWeight rises.
 readAnkle(r,left,_capK[0],_keep[2],_kneeNow);pelvisFrame(r);_hip.copy(hip.position).applyQuaternion(_pq).add(_pp);
 _fwd.set(0,0,1).applyQuaternion(_pq);_pole.set(Math.sin(yaw),0,Math.cos(yaw)).multiplyScalar(.7).addScaledVector(_fwd,.3).normalize();
 _kneeNow.sub(_hip).normalize().lerp(_pole,footWeight).add(_hip);
 solveLeg(r,left,target,yaw,0,0,undefined,_kneeNow);
 // The foot keeps its previous rig-group orientation until footWeight takes it to flat (so it never flips in one frame).
 if(footWeight<1){_qf.copy(_keep[2]).slerp(_qi.setFromEuler(_e.set(0,yaw,0,'YXZ')),footWeight);pelvisFrame(r);ankle.quaternion.copy(_pq).multiply(hip.quaternion).multiply(knee.quaternion).invert().multiply(_qf);}
 if(weight>=1)return;hip.quaternion.copy(_keep[0].slerp(hip.quaternion,weight));knee.quaternion.copy(_keep[1].slerp(knee.quaternion,weight));
}
const _hold=[new T.Vector3(),new T.Vector3()],_holdQ=[new T.Quaternion(),new T.Quaternion()],_holdK=[new T.Vector3(),new T.Vector3()],_holdLeg=[0,1,2,3].map(()=>new T.Quaternion()),_holdA=new T.Vector3(),_holdAq=new T.Quaternion();
/** Keep both ankles (position and foot orientation) where they are while `change` moves the body, pelvis or legs (a
 * finishPose blend over ground-planted legs): the legs are re-solved onto the captured ankles, floor-clamped, bending the
 * knee toward where the changed leg put it, and blended from the changed legs by `weight`. */
export function holdAnkles(r:CharacterRig,change:()=>void,weight=1){
 if(weight<=1e-4){change();return;}
 for(let i=0;i<2;i++)readAnkle(r,i===0,_hold[i],_holdQ[i]);change();
 for(let i=0;i<2;i++){const bones=legBones(r,i===0);readAnkle(r,i===0,_holdA,_holdAq,_holdK[i]);if(weight<1)bones.forEach((b,k)=>_holdLeg[k].copy(b.quaternion));_hold[i].y=Math.max(_hold[i].y,FLOOR);solveLeg(r,i===0,_hold[i],0,0,0,_holdAq,_holdK[i]);if(weight<1)bones.forEach((b,k)=>b.quaternion.copy(_holdLeg[k].slerp(b.quaternion,weight)));}
}
/** The ankle's rig-group position from the live joints. */
export function ankleInGroup(r:CharacterRig,left:boolean,out:T.Vector3){readAnkle(r,left,out,_keep[0]);return out;}
/** Current ankle position, foot orientation and (optionally) knee position in rig-group space, read from the joints. */
function readAnkle(r:CharacterRig,left:boolean,out:T.Vector3,q:T.Quaternion,kneeOut?:T.Vector3){
 const [hip,knee,ankle]=legBones(r,left);pelvisFrame(r);q.copy(_pq).multiply(hip.quaternion);out.copy(hip.position).applyQuaternion(_pq).add(_pp);
 _t.copy(knee.position).applyQuaternion(q);out.add(_t);if(kneeOut)kneeOut.copy(out);q.multiply(knee.quaternion);_t.copy(ankle.position).applyQuaternion(q);out.add(_t);q.multiply(ankle.quaternion);
}
/** The yaw the rig group faces without the local camera: death, active hit reaction, attack commitment, else the player's yaw. */
export function rigYaw(p:PlayerView,time:number){return p.death&&p.hp<=0?p.death.yaw:p.hitReaction&&(p.hitStunUntil||0)>time&&p.hitReaction.at<=time?p.hitReaction.yaw:committed(p,time)?p.attackCommitment!.yaw:p.yaw;}
type YawEase={source:string;from:number;at:number;last:number;time:number};const yawEase=new WeakMap<object,YawEase>();
/** Presented rig-group yaw: the authoritative (or local camera) yaw, eased out along the shortest arc for at most .12 s when
 * its SOURCE changes (death | hit | commit | roll | input). Exact inside commitments and swingAt..swingUntil. */
export function presentedYaw(r:object,p:PlayerView,time:number,input:number){
 const dead=!!(p.death&&p.hp<=0),hit=!dead&&!!p.hitReaction&&(p.hitStunUntil||0)>time&&p.hitReaction.at<=time,commit=!dead&&!hit&&committed(p,time),roll=!dead&&!hit&&!commit&&rolling(p,time);
 const source=dead?'death':hit?'hit':commit?'commit':roll?'roll':'input',target=dead?p.death!.yaw:hit?p.hitReaction!.yaw:commit?p.attackCommitment!.yaw:roll?p.yaw:input;
 let s=yawEase.get(r);if(!s||timeJumped(s.time,time)){s={source,from:target,at:-1e9,last:target,time};yawEase.set(r,s);}
 const now=Math.max(time,s.time);if(source!==s.source){s.source=source;s.from=s.last;s.at=now;}
 const strike=p.swingAt!==undefined&&p.swingUntil!==undefined&&time>=p.swingAt&&time<p.swingUntil,u=(now-s.at)/.12;let yaw=target;
 if(u<1&&!dead&&!commit&&!strike)yaw=s.from+wrapAngle(target-s.from)*(1-(1-u)**3);
 s.last=yaw;s.time=now;return yaw;
}
/** Landing compression: an under-damped spring impulse (omega 11, zeta .75) normalised to peak 1 at ~100 ms. */
function landingCurve(t:number){return t<0||t>.8?0:Math.exp(-8.25*t)*Math.sin(7.27*t)/.2915;}
/** Bent elbows pump past the hip and forward; opposite the same-side foot, lagging the legs slightly. */
function swingArm(r:CharacterRig,s:Travel,left:boolean,phase:number,run:number,air:number,amp:number,back:number,push:number,reach:number,dt:number,elbowAmp=1){
 const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,hand=left?r.leftHand:r.rightHand,sign=left?1:-1,i=left?0:1;
 const u=frac(phase/TAU+(left?0:.5)-.06),keys=[0,.17,.38,.55,.73,.90,1],weight=clamp(run,0,1);
 const shoulder=poseCurve(keys,[.66,.18,-.86,-.55,.18,.63,.66],u)*amp,bend=-1.3+(poseCurve(keys,[-1.05,-1.25,-1.42,-1.55,-1.16,-1.04,-1.05],u)+1.3)*elbowAmp-back*.3;
 const x=lerp(left?-.10:-.14,shoulder,weight)-push*.55-reach*.2;
 arm.rotation.set(x,0,sign*(.10+air*.18+reach*.2));elbow.rotation.set(lerp(-.22,bend,weight)-push*.35,0,0);
 // Hands trail the arm: a critically damped spring (implicit, rate independent) driven by the shoulder's angular velocity.
 const prev=s.armPrev[i],vel=dt>0&&prev===prev?(x-prev)/dt:0;s.armPrev[i]=x;const goal=clamp(vel*.035,-.3,.3),w=18;let h=s.hand[i*2],hv=s.hand[i*2+1];
 hv=(hv-(h-goal)*w*w*dt)/(1+2*w*dt+w*w*dt*dt);h+=hv*dt;s.hand[i*2]=h;s.hand[i*2+1]=hv;
 hand.rotation.set(h*weight+Math.sin(phase+(left?0:Math.PI)-.65)*.04*weight,0,sign*.05);
}
/** Distance-driven locomotion under the action layers. Planted feet are integrated against the ground velocity in
 * rig-group space, so they stay still on the floor while the hips travel over them; swing feet follow a cubic path to a
 * speed-matched landing target. `planted` (attack commitment or hitstun) freezes the stride and sets the swing foot down. */
export function locomotion(r:CharacterRig,p:PlayerView,time:number,dt:number,planted=false){
 const frameDt=Math.max(0,dt);dt=clamp(dt,0,.05);const fp=isFirstPersonRig(r),yawNow=r.group.rotation.y,gp=r.group.position;
 let s=travelState.get(r);
 // A rig that starts showing another player starts fresh, exactly as a new rig would: no feet, springs or idle phase carry over.
 if(s&&s.id!==p.id){r.motion=0;r.stride=0;r.groundWeight=1;r.land=0;}
 if(!s||timeJumped(s.time,time)||s.id!==p.id){const l=idleFoot(p.spec,true),rt=idleFoot(p.spec,false);s={time,raw:time,px:gp.x,pz:gp.z,still:1,rvx:p.vel.x,rvz:p.vel.z,clock:-1,forward:1,strafe:0,lean:0,bank:0,vx:0,vz:0,mvx:0,mvz:0,yaw:yawNow,feet:[newFoot(l.x,l.z,l.yaw),newFoot(rt.x,rt.z,rt.yaw)],hipYaw:0,legYaw:0,legYawV:0,settling:false,airAt:time,airVy:0,airLead:0,phaseOffset:0,onGround:p.grounded&&!(p.dashTime&&p.dashTime>0),landAt:-1e9,landDepth:0,dashLean:0,phase:p.id?hashPhase(p.id):0,armPrev:[NaN,NaN],hand:[0,0,0,0],legQ:Array.from({length:6},()=>new T.Quaternion()),hips:[new T.Vector3(.13,-.32,0),new T.Vector3(-.13,-.32,0)],solvedBody:[new T.Vector3(),new T.Quaternion(),new T.Quaternion()],solved:false,id:p.id,fresh:true};travelState.set(r,s);r.wasGrounded=p.grounded;}
 // A small backward step holds the stateful clock (see REWIND); authoritative phases below still read `time`.
 const st=s,now=Math.max(time,st.time),dNow=now-st.time;st.time=now;st.raw=time;
 // Root travel since the last frame (world). Planted feet lock to it, so they stay put however the root is fed (a fixed
 // 60 Hz sim under a faster display, interpolated remote players, the locally predicted player).
 const dxw=gp.x-st.px,dzw=gp.z-st.pz,moved=Math.hypot(dxw,dzw),jump=moved>TELEPORT;st.px=gp.x;st.pz=gp.z;
 // Planted feet and the hips' yaw stay fixed in the world when the rig group turns (a near-instant flip or a teleport re-seats them).
 const dyaw=wrapAngle(yawNow-st.yaw);st.yaw=yawNow;
 if(Math.abs(dyaw)>1.6||jump){const l=idleFoot(p.spec,true),rt=idleFoot(p.spec,false);st.feet=[newFoot(l.x,l.z,l.yaw),newFoot(rt.x,rt.z,rt.yaw)];}
 else if(dyaw!==0){st.legYaw=clamp(st.legYaw-dyaw,-GAIT.turnDeadZone-.1,GAIT.turnDeadZone+.1);const c=Math.cos(dyaw),sn=Math.sin(dyaw);for(const f of st.feet){let x=f.x,z=f.z;f.x=x*c-z*sn;f.z=x*sn+z*c;x=f.fx;z=f.fz;f.fx=x*c-z*sn;f.fz=x*sn+z*c;f.yaw-=dyaw;f.fyaw-=dyaw;}}
 // A moving root drives the gait with its own smoothed velocity (p.vel can lead it: remote interpolation, stale snapshots);
 // a root that has not moved for .3 s while p.vel says otherwise is a treadmill (previews, lab without root travel).
 // Commitment, hitstun and rolls (planted) keep the passed (zeroed) velocity: feet ride with the body the action moves.
 st.still=moved>1e-6?0:st.still+dt;const live=!fp&&!jump&&st.still<.3;
 // A root fed every frame (prediction, interpolation, the lab) is differentiated over the frame; a root that only moves when
 // the clock advances (the fixed 60 Hz offline sim under a faster display) over the clock, so the estimate never ripples.
 // Whichever explains the displacement clearly better (by 2x, above 2 mm) moves the `clock` score toward it.
 if(live){const eT=Math.hypot(dxw-st.rvx*dNow,dzw-st.rvz*dNow),eR=Math.hypot(dxw-st.rvx*frameDt,dzw-st.rvz*frameDt);if(Math.max(eT,eR)>.002&&(eT<.5*eR||eR<.5*eT))st.clock+=((eT<eR?1:-1)-st.clock)*.25;}
 const span=live&&st.clock>0?dNow:frameDt;
 if(!live){st.rvx=p.vel.x;st.rvz=p.vel.z;}else if(span>0){const k=1-Math.exp(-Math.min(span,.05)*ROOT_VEL_RATE);st.rvx+=(dxw/span-st.rvx)*k;st.rvz+=(dzw/span-st.rvz)*k;}
 const rooted=live&&!planted,wx=rooted?st.rvx:p.vel.x,wz=rooted?st.rvz:p.vel.z,cy=Math.cos(yawNow),sy=Math.sin(yawNow),vx=wx*cy-wz*sy,vz=wx*sy+wz*cy,speed=Math.hypot(vx,vz);
 // Ground travel under the planted feet this frame, in rig-group space (the rig group's own frame, not p.yaw's).
 const gdx=rooted?dxw*cy-dzw*sy:vx*dt,gdz=rooted?dxw*sy+dzw*cy:vz*dt;
 const blend=1-Math.exp(-dt*GAIT.blendSpeed),goal=p.grounded?clamp(speed/6.8,0,1.2):0;r.motion+=(goal-r.motion)*blend;
 if(speed>.1){st.forward+=(vz/speed-st.forward)*blend;st.strafe+=(vx/speed-st.strafe)*blend;}
 // Lean from the acceleration VECTOR in body space: forward acceleration pitches, lateral (turning) acceleration banks.
 // (Acceleration is sampled on the same span as the velocity estimate.)
 const dA=rooted?Math.min(span,.05):dt;if(dA>0){const ax=!planted?(vx-st.vx)/dA:0,az=!planted?(vz-st.vz)/dA:0,k9=1-Math.exp(-dA*9);st.vx=vx;st.vz=vz;
 st.lean+=(clamp(az*.008,-.1,.1)-st.lean)*k9;st.bank+=(clamp(-ax*.006,-.12,.12)-st.bank)*k9;}
 if(speed>.5&&!planted){st.mvx=vx;st.mvz=vz;}else{const k=Math.exp(-dt*6);st.mvx*=k;st.mvz*=k;}
 const dashing=!!(p.dashTime&&p.dashTime>0),dash=dashing?.15:0;st.dashLean+=(dash-st.dashLean)*(1-Math.exp(-dt*(dash>st.dashLean?18:10)));
 // Air phases (tracked per rig) and the landing spring. A dash burst is a short lunge: the legs leave the gait like a push-off.
 const onGround=p.grounded&&!dashing;
 if(!onGround&&st.onGround){st.airAt=now;st.airVy=0;const [lf,rf]=st.feet;st.airLead=!lf.planted?0:!rf.planted?1:frac(r.stride/TAU)<.5?1:0;}
 if(!p.grounded)st.airVy=Math.min(st.airVy,p.vel.y);
 const landing=onGround&&!st.onGround;st.onGround=onGround;
 if(p.grounded&&!r.wasGrounded){const airborne=now-st.airAt;st.landAt=now;st.landDepth=st.airVy<-2&&airborne>.08?clamp(-st.airVy*.012,.03,.12):0;st.airVy=0;}
 r.wasGrounded=p.grounded;r.groundWeight+=(Number(onGround)-r.groundWeight)*(1-Math.exp(-dt*(onGround?(r.wasGrounded&&now-st.landAt>.05?9:22):16)));const air=fp?0:1-r.groundWeight;
 const dip=st.landDepth*landingCurve(now-st.landAt);r.land=dip;
 // Cadence: stride length grows with speed, so a planted ankle's travel matches the ground (see GAIT).
 const run=r.motion,weight=Math.min(run,1),back=smoothMotion((-st.forward-.3)/.4)*clamp(speed/1.5,0,1),side=Math.abs(st.strafe)*clamp(speed/1.5,0,1);
 const D=cycleLength(speed)*lerp(1,.8,back),S=GAIT.stance*lerp(1,.7,back),contact=clamp(S/D,GAIT.contactMin,GAIT.contactMax);
 let rate=onGround&&speed>.05?Math.min(speed/D,GAIT.maxCadence):0;if(st.settling&&onGround&&!fp)rate=Math.max(rate,GAIT.settleCadence);if(planted)rate=0;
 let prevStride=r.stride;r.stride+=TAU*rate*dt;st.phaseOffset*=Math.exp(-dt*10);
 const sprint=smoothMotion((run-.32)/.65)*(1-back),moving=clamp(speed/2,0,1),heel=.1*weight,toeOff=(.22+.3*sprint)*moving;
 if(landing&&!fp){
  // Touchdown: the leading air foot starts its stance where it is; a running landing keeps striding with the other.
  const lead=st.airLead,old=r.stride;r.stride=Math.floor(r.stride/TAU)*TAU+(lead?Math.PI:0)+.001;prevStride=r.stride;st.phaseOffset=wrapAngle(st.phaseOffset+old-r.stride);
  st.feet.forEach((f,i)=>{f.x=f.fx=f.sx;f.z=f.fz=f.sz;f.y=f.fy=Math.max(FLOOR,f.sy);f.yaw=f.fyaw=f.syaw;f.pitch=0;f.toe=0;f.planted=i===lead||!(speed>=.25&&.5>=contact);f.uLift=.5;if(f.planted)f.y=FLOOR;});
 }
 // Hips turn toward the travel direction (backward travel keeps them facing the aim).
 let hipTravel=speed>1e-3?Math.atan2(vx,vz):0;if(Math.abs(hipTravel)>1.9)hipTravel=wrapAngle(hipTravel-Math.PI);hipTravel=clamp(hipTravel,-GAIT.travelYawMax,GAIT.travelYawMax)*clamp((speed-.1)/.6,0,1)*moving;
 const idle=idleFeet(p.spec),mean=(wrapAngle(st.feet[0].yaw-idle[0].yaw)+wrapAngle(st.feet[1].yaw-idle[1].yaw))/2,turn=clamp(mean,-GAIT.turnDeadZone,GAIT.turnDeadZone);
 const stance=stanceFor(p.spec);st.hipYaw=lerp(turn+stance.pelvisYaw*(p.spec==='rifle'?r.aimWeight:1),hipTravel,moving);
 let settling=false;
 if(!fp&&onGround)for(let i=0;i<2;i++){
  const f=st.feet[i],sx=i?-1:1,u=frac(r.stride/TAU+(i?.5:0)),pu=frac(prevStride/TAU+(i?.5:0)),wrapped=r.stride>prevStride&&u<pu-.5;
  // Landing target: the stance (idle) or the running track turned with the hips, plus half a stance along the velocity.
  const ch=Math.cos(hipTravel),shy=Math.sin(hipTravel),nx=sx*lerp(.125,.1,sprint),land=rate>1e-3&&speed>1e-3?(speed*contact/(2*rate)-GAIT.stanceBias*sprint)/speed:0;
  f.lx=lerp(idle[i].x,nx*ch,moving)+vx*land;f.lz=lerp(idle[i].z,-nx*shy,moving)+vz*land;
  // Landing stays within reach of the hip (a nominal running hip height keeps the clamp steady frame to frame).
  {const h=st.hips[i],reach=Math.sqrt(LAND_REACH*LAND_REACH-.44*.44),ex=f.lx-h.x,ez=f.lz-h.z,e=Math.hypot(ex,ez);if(e>reach){f.lx=h.x+ex*reach/e;f.lz=h.z+ez*reach/e;}}f.lyaw=lerp(idle[i].yaw,hipTravel+sx*.05,moving);
  // Settled: the planted foot matches its stance target rotated with the feet (turns inside the dead zone hold still).
  const c=Math.cos(turn),sn=Math.sin(turn),ix=idle[i].x*c+idle[i].z*sn,iz=-idle[i].x*sn+idle[i].z*c;
  const settled=f.planted&&Math.hypot(f.x-ix,f.z-iz)<.05&&Math.abs(wrapAngle(f.yaw-idle[i].yaw))<GAIT.turnDeadZone;
  if(f.planted){
   f.x-=gdx;f.z-=gdz;
   const hx=sx*.13*Math.cos(st.legYaw),hz=-sx*.13*Math.sin(st.legYaw),over=Math.hypot(f.x-hx,f.z-hz)>.4;
   if(!planted&&((u>=contact&&(speed>=.25||!settled))||over)){f.planted=false;f.fx=f.x;f.fy=f.y;f.fz=f.z;f.fyaw=f.yaw;f.uLift=Math.min(pu<contact&&u>=contact&&!wrapped?contact:u,.97);}
   else{const us=u<contact?u/contact:1,roll=poseCurve([0,.22,.55,1],[-heel,0,0,toeOff],us)*clamp(speed/1.5,0,1);f.pitch=roll;f.y=FLOOR+.09*Math.sin(Math.max(0,roll));f.toe=-Math.max(0,roll)*.85;}
  }
  if(!f.planted){
   if(planted){
    // Commitment or hitstun: the swing foot comes down within ~60 ms, with a short catch-step along the previous travel.
    const k=1-Math.exp(-dt*45),mv=Math.hypot(st.mvx,st.mvz),catchStep=mv>.5?Math.min(.12,mv*.018)/mv:0;
    f.x+=(f.fx+st.mvx*catchStep-f.x)*k;f.z+=(f.fz+st.mvz*catchStep-f.z)*k;f.y+=(FLOOR-f.y)*k;f.pitch*=1-k;f.toe*=1-k;
    if(f.y<FLOOR+.004){f.y=FLOOR;f.planted=true;}
   }else{
    const sw=wrapped?1:clamp((u-f.uLift)/Math.max(.03,1-f.uLift),0,1),dur=rate>1e-3?(1-f.uLift)/rate:.2;
    const k0=.3*(1-back*.6),k1=.7;f.x=hermite(f.fx,f.lx,-vx*dur*k0,-vx*dur*k1,sw);f.z=hermite(f.fz,f.lz,-vz*dur*k0,-vz*dur*k1,sw);
    // Heel fold behind the hip, then knee drive and reach (sprint); a low arc when walking, backpedalling or stepping in place.
    const fold=lerp(1,.45,side),start=Math.max(0,f.fy-FLOOR),sprintH=sw<.84?poseCurve([0,.12,.3,.56,.84],[start,start+.09*fold,.24*fold,.19*fold,.07],sw):.07*((1-sw)/.16)**1.3;
    const walkH=start*(1-sw)*(1-sw)+4*sw*(1-sw)*lerp(GAIT.settleLift,.085*lerp(1,.6,back),moving);
    // A crisp contact: the swinging foot clears the floor until the last 2% of the swing instead of skimming along it.
    f.y=FLOOR+Math.max(lerp(walkH,sprintH,sprint),.012*clamp((1-sw)*50,0,1)*clamp(speed/1.5,0,1));f.yaw=f.fyaw+wrapAngle(f.lyaw-f.fyaw)*smoothMotion(sw);
    f.pitch=poseCurve([0,.3,.72,1],[toeOff,toeOff+.12*sprint,.05,lerp(-heel,.22*weight,back)],sw)*clamp(speed/1.5+.001,0,1);f.toe=-Math.max(0,toeOff)*.85*(1-smoothMotion(sw/.35));
    if(sw>=1){const since=wrapped&&rate>1e-3?u/rate:0;f.planted=true;f.x=f.lx-vx*since;f.z=f.lz-vz*since;f.y=FLOOR;f.yaw=f.lyaw;}
   }
  }
  if(!f.planted||!settled&&speed<.25)settling=true;
 }
 st.settling=settling;
 // Body: bob lowest at mid-stance, lateral sway over the stance foot, lean about the ankles, living idle.
 // The upper body reads a continuous phase: re-seating the feet on touchdown never pops the torso or arms.
 const ph2=r.stride+st.phaseOffset;
 const idleW=1-clamp(run,0,1),ph=st.phase,shift=(Math.sin(now*TAU/4.3+ph)*.6+Math.sin(now*TAU/7.1+ph*1.7)*.4)*idleW,breath=Math.sin(now*TAU*.25+ph)*idleW;
 const moveW=fp?0:weight,bob=-(.008+.010*sprint)*moveW*Math.cos(2*ph2-TAU*contact),sway=Math.cos(ph2-Math.PI*contact)*.012*(1-sprint*.4)*moveW;
 const lean=fp?0:GAIT.runLean*weight*Math.max(0,st.forward)*(1-back)+.05*weight*back+st.lean+st.dashLean+dip*1.1;
 const vy=p.grounded?0:p.vel.y,airAge=now-st.airAt,push=air*Math.max(smoothMotion((vy-3)/3),1-smoothMotion(airAge/.12),st.dashLean/.15),descend=air*smoothMotion((-vy-2)/3),apex=Math.max(0,air-push-descend);
 // Rise out of a dodge roll's exit crouch (dodge-roll-motion ends at -.08) over ~.22 s after roll.until.
 const sinceRoll=p.roll&&time>=p.roll.until?time-p.roll.until:1,postRoll=fp?0:.08*(1-smoothMotion(sinceRoll/.22));
 r.body.position.set(fp?0:sway+shift*.018,-.025-GAIT.runCrouch*moveW+(stance.crouch-stance.kneeSoft*.08)*idleW+bob+breath*.004-dip-postRoll,fp?0:.295*Math.sin(lean));
 r.body.rotation.set(lean,0,fp?0:-st.strafe*weight*.08+st.bank);
 const twist=fp?0:weight*(1-back*.5),gait=Math.sin(ph2),pelvisRoll=Math.cos(ph2-Math.PI*contact)*.05*moveW+shift*.035;
 r.pelvis.rotation.set(0,gait*.14*twist,pelvisRoll);
 const spineYaw=-Math.sin(ph2-.35)*.10*twist,chestYaw=-Math.sin(ph2-.6)*.12*twist;
 r.spine.rotation.set(-breath*.006+stance.lean*.6*idleW,spineYaw,-pelvisRoll*.6);r.chest.rotation.set(breath*.02+stance.lean*.4*idleW,chestYaw,-pelvisRoll*.35-Math.cos(ph2)*.02*moveW);
 r.neck.rotation.set(-r.body.rotation.x*.25,0,0);
 r.head.rotation.set(-p.pitch*.55-r.body.rotation.x*.4,-(gait*.14*twist+spineYaw+chestYaw)+Math.sin(now*.6+ph)*.014*idleW,shift*.012-r.body.rotation.z*.5);
 // Legs: planted/swing feet on the floor, blended toward the vy-driven air pose.
 const lead=st.airLead;
 for(let i=0;i<2;i++){
  const f=st.feet[i],left=i===0,isLead=i===lead,sx=left?1:-1;
  if(fp){_target.set(sx*.13,FLOOR,0);solveLeg(r,left,_target,0,0,0);continue;}
  _target.set(f.x,f.y,f.z);let yaw=f.yaw,pitch=f.pitch,toe=f.toe;
  if(air>.001){
   // Hip-relative air targets: push-off (lead knee drives, trail leg extends back), apex tuck, descent reach.
   const [hip]=legBones(r,left),wsum=Math.max(1e-6,push+apex+descend);pelvisFrame(r);
   _air.set(-sx*.025,(push*(isLead?-.30:-.47)+apex*(isLead?-.31:-.42)+descend*(isLead?-.44:-.47))/wsum,(push*(isLead?.16:-.16)+apex*(isLead?.23:-.10)+descend*(isLead?.10:-.05))/wsum).add(hip.position).applyQuaternion(_pq).add(_pp);
   _target.lerp(_air,air);yaw*=1-air;pitch=lerp(pitch,(push*.55+apex*.25-descend*.08)/wsum,air);toe*=1-air;
  }
  if(p.grounded)_target.y=Math.max(_target.y,FLOOR);
  if(!f.planted&&air<.5){
   // A swinging leg never locks straight: keep its ankle inside ~96% reach of the hip (landing targets are clamped likewise).
   const [hip]=legBones(r,left);pelvisFrame(r);_hip.copy(hip.position).applyQuaternion(_pq).add(_pp);const d=_target.distanceTo(_hip);if(d>REACH)_target.sub(_hip).multiplyScalar(REACH/d).add(_hip);
  }
  solveLeg(r,left,_target,yaw,pitch,toe);st.hips[i].copy(_hip);f.sx=_target.x;f.sy=_target.y;f.sz=_target.z;f.syaw=yaw;f.spitch=pitch;
 }
 for(let k=0;k<6;k++)st.legQ[k].copy(legBones(r,k<3)[k%3].quaternion);st.solvedBody[0].copy(r.body.position);st.solvedBody[1].copy(r.body.quaternion);st.solvedBody[2].copy(r.pelvis.quaternion);st.solved=true;
 // Arms: pump opposite the legs (reduced for the first-person view model and backpedal), rise on push-off, reach on descent.
 const armAmp=(fp?.15:1)*lerp(1,.4,back);
 r.leftShoulder.rotation.set(-breath*.009,gait*.045*weight,.03+breath*.006);r.rightShoulder.rotation.set(-breath*.009,-gait*.045*weight,-.03-breath*.006);
 swingArm(r,st,true,ph2,run,air,armAmp,back,push,descend,dt,fp?.2:1);swingArm(r,st,false,ph2,run,air,armAmp,back,push,descend,dt,fp?.2:1);
 return {speed,run};
}
/** Ground pass, after every action layer and before finishPose. It turns the pelvis toward the feet/travel (the spine and
 * chest are counter-rotated exactly, so the chest, head, arms and weapon keep their pose), then re-solves the legs so the
 * ankles stay on their planted targets while the body drops, leans or twists (knees bend instead of feet sinking).
 * Legs an action layer placed (rig-ik foot() or direct writes) keep their ankles, and the gait state follows them. */
export function groundPass(r:CharacterRig,p:PlayerView,time:number,dt:number){
 const s=travelState.get(r);if(!s||!s.solved||s.raw!==time)return;s.solved=false;dt=clamp(dt,0,.05);
 const skip=isFirstPersonRig(r)||p.hp<=0||!!(p.juggle&&time<p.juggle.until)||!!(p.knockdown&&time<p.knockdown.until)||rolling(p,time);
 // Layered legs (offsets from this sample's gait targets) keep the hip follow and the gait's own foot state.
 const layered=legsLayered(r)&&!legsAuthored(r);let changed=legsAuthored(r)||layered;for(let k=0;k<6&&!changed;k++)changed=!legBones(r,k<3)[k%3].quaternion.equals(s.legQ[k]);
 if(skip){s.legYaw=0;s.legYawV=0;s.fresh=false;syncFeet(r,s,p);return;}
 // Critically damped hip follow (implicit, frame-rate independent); layers that own the legs let it relax to zero.
 // It starts on its target on the rig's first sample (a spawn or reset never twists the pelvis in from zero).
 const target=changed&&!layered?0:s.hipYaw,w=GAIT.legYawOmega;if(s.fresh){s.fresh=false;s.legYaw=target;s.legYawV=0;}else{s.legYawV=(s.legYawV-(s.legYaw-target)*w*w*dt)/(1+2*w*dt+w*w*dt*dt);s.legYaw+=s.legYawV*dt;}
 const L=s.legYaw;if(changed)for(let i=0;i<2;i++)readAnkle(r,i===0,_cap[i],_capQ[i],_capK[i]);
 if(Math.abs(L)>1e-5){
  const P=_q1.copy(r.pelvis.quaternion),S=_q2.copy(r.spine.quaternion),C=_q3.copy(r.chest.quaternion),Pi=_q4.copy(P).invert();
  r.pelvis.quaternion.copy(_ry.setFromAxisAngle(UP,L)).multiply(P);
  r.spine.quaternion.copy(Pi).multiply(_ry.setFromAxisAngle(UP,-.45*L)).multiply(P).multiply(S);
  r.chest.quaternion.copy(S).invert().multiply(Pi).multiply(_ry.setFromAxisAngle(UP,-.55*L)).multiply(P).multiply(S).multiply(C);
 }
 if(!p.grounded)return;
 // Layer-placed legs are always re-solved (never toggled): same ankle, foot orientation and knee direction, floor-clamped,
 // with the knee as a clean hinge, so the hip turn above cannot move them and no frame flips the twist convention.
 if(changed){for(let i=0;i<2;i++){const c=_cap[i];c.y=Math.max(c.y,FLOOR);solveLeg(r,i===0,c,0,0,0,_capQ[i],_capK[i]);}if(!layered)syncFeet(r,s,p,true);return;}
 // Untouched legs under an unmoved body and pelvis already sit on their targets.
 if(Math.abs(L)<=1e-5&&r.body.position.equals(s.solvedBody[0])&&r.body.quaternion.equals(s.solvedBody[1])&&r.pelvis.quaternion.equals(s.solvedBody[2]))return;
 for(let i=0;i<2;i++){const f=s.feet[i];_target.set(f.sx,f.sy,f.sz);solveLeg(r,i===0,_target,f.syaw,f.spitch,f.toe);}
}
/** The gait state follows ankles an action layer placed, so the gait resumes from them without a pop. */
function syncFeet(r:CharacterRig,s:Travel,p:PlayerView,captured=false){
 if(!p.grounded)return;
 for(let i=0;i<2;i++){
  const f=s.feet[i];if(!captured)readAnkle(r,i===0,_cap[i],_capQ[i]);const c=_cap[i];
  if(Math.hypot(c.x-f.sx,c.y-f.sy,c.z-f.sz)<.015)continue;
  _fwd.set(0,0,1).applyQuaternion(_capQ[i]);
  f.x=f.fx=f.sx=c.x;f.z=f.fz=f.sz=c.z;f.y=f.fy=f.sy=Math.max(FLOOR,c.y);f.yaw=f.fyaw=f.syaw=Math.atan2(_fwd.x,_fwd.z);f.pitch=f.spitch=Math.asin(clamp(-_fwd.y,-1,1));f.toe=0;
  f.planted=c.y<FLOOR+.015;if(!f.planted)f.uLift=Math.min(frac(r.stride/TAU+(i?.5:0)),.97);
 }
}
const lavaWeight=new WeakMap<object,{w:number;time:number}>();
/** Lava panic flail over the finished pose, ramped in and out per rig (~8/s) instead of a raw overwrite. */
export function lavaFlail(r:Pick<CharacterRig,'leftArm'|'rightArm'|'leftLeg'|'rightLeg'>,on:boolean,time:number,dt:number){
 let s=lavaWeight.get(r);if(!s||timeJumped(s.time,time)){s={w:on?1:0,time};lavaWeight.set(r,s);}
 s.w+=(Number(on)-s.w)*(1-Math.exp(-clamp(dt,0,.05)*8));s.time=Math.max(time,s.time);const w=s.w;if(w<1e-3)return;const k=Math.sin(time*7)*.45;
 r.leftArm.rotation.x=lerp(r.leftArm.rotation.x,-.7+k,w);r.rightArm.rotation.x=lerp(r.rightArm.rotation.x,-.7-k,w);r.leftLeg.rotation.x=lerp(r.leftLeg.rotation.x,k,w);r.rightLeg.rotation.x=lerp(r.rightLeg.rotation.x,-k,w);
}
export {finishPose} from './pose-transitions.js';
