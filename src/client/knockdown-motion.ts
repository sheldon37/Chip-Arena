import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import type {Knockdown} from '../shared/recovery.js';
import {foot} from './rig-ik.js';
export type KnockdownPhase='air'|'land'|'legs-up'|'flat'|'roll'|'rise';
/** Seconds after landedAt that the landing (hips, then arms and head settling) lasts before the body lies flat. */
export const KNOCKDOWN_LANDING=.2;
/** The knockdown sub-phase at `time`, purely from the authoritative timestamps (landedAt, pickupUntil, rollAt, rollUntil). */
export function knockdownPhase(k:Knockdown,time:number):KnockdownPhase{
 if(k.landedAt===undefined||time<k.landedAt)return 'air';
 if(k.rollUntil!==undefined&&time>=k.rollUntil)return 'rise';
 if(k.rollAt!==undefined&&time>=k.rollAt)return 'roll';
 if(!k.pickupUsed&&time<(k.pickupUntil??0))return 'legs-up';
 return time<k.landedAt+KNOCKDOWN_LANDING?'land':'flat';
}
const sat=(x:number)=>x<0?0:x>1?1:x,sm=(x:number)=>{x=sat(x);return x*x*x*(x*(x*6-15)+10);},outCubic=(x:number)=>1-(1-sat(x))**3,inQuad=(x:number)=>sat(x)**2,bump=(x:number)=>x<=0||x>=1?0:Math.sin(Math.PI*x);
const wrap=(a:number)=>Math.atan2(Math.sin(a),Math.cos(a));
type Joint='pelvis'|'spine'|'chest'|'neck'|'head'|'leftArm'|'rightArm'|'leftElbow'|'rightElbow'|'leftHand'|'rightHand'|'leftLeg'|'rightLeg'|'leftKnee'|'rightKnee'|'leftFoot'|'rightFoot'|'leftToe'|'rightToe';
const joints:Joint[]=['pelvis','spine','chest','neck','head','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe'];
type Pose=Partial<Record<Joint,[number,number,number]>>;
const upper:Joint[]=['pelvis','spine','chest','neck'],arms:Joint[]=['leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand'],legs:Joint[]=['leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe'],head:Joint[]=['head'];
/** Lying on the back (the body root is exactly -PI/2): arms spread on the floor, legs long, feet relaxed outward. */
const FLAT:Pose={pelvis:[0,0,0],spine:[-.04,0,0],chest:[0,0,0],neck:[.05,0,0],head:[.14,0,0],leftArm:[-.08,0,.95],rightArm:[-.08,0,-.95],leftElbow:[-.32,0,0],rightElbow:[-.38,0,0],leftHand:[0,0,.1],rightHand:[0,0,-.1],leftLeg:[-.06,0,.16],rightLeg:[-.1,0,-.13],leftKnee:[.14,0,0],rightKnee:[.22,0,0],leftFoot:[.5,.28,0],rightFoot:[.45,-.3,0],leftToe:[0,0,0],rightToe:[0,0,0]};
/** The legs-up pickup window: knees drawn over the hips. */
const LEGS_UP:Pose={leftLeg:[-1.15,0,.12],rightLeg:[-.9,0,-.12],leftKnee:[1.25,0,0],rightKnee:[1.1,0,0],leftFoot:[.3,0,0],rightFoot:[.3,0,0]};
/** Tucked backward roll: chin in, hands by the ears, knees to the chest. */
const TUCK:Pose={pelvis:[0,0,0],spine:[.3,0,0],chest:[.28,0,0],neck:[.2,0,0],head:[.45,0,0],leftArm:[-2.3,0,.45],rightArm:[-1.25,0,-.15],leftElbow:[-1.9,0,0],rightElbow:[-2.0,0,0],leftHand:[.3,0,0],rightHand:[.3,0,0],leftLeg:[-1.95,0,.1],rightLeg:[-1.8,0,-.1],leftKnee:[2.2,0,0],rightKnee:[2.1,0,0],leftFoot:[.45,0,0],rightFoot:[.45,0,0],leftToe:[0,0,0],rightToe:[0,0,0]};
/** The roll opens into a crouch: lead foot planted forward, rear knee low on its toes, free hand down for balance.
 * Leg angles are rig-ik foot() solutions for the crouch body (lean .3, root -.32) with the feet at LEAD and REAR. */
const CROUCH:Pose={pelvis:[0,0,0],spine:[.1,0,0],chest:[.06,0,0],neck:[-.05,0,0],head:[-.1,0,0],leftArm:[-.95,0,.22],rightArm:[-1.05,0,-.12],leftElbow:[-.35,0,0],rightElbow:[-1.45,0,0],leftHand:[0,0,0],rightHand:[0,0,0],leftLeg:[-2.083,.019,-.011],rightLeg:[-.092,.007,-.152],leftKnee:[1.607,.072,.069],rightKnee:[1.842,.049,.037],leftFoot:[.173,-.068,-.04],rightFoot:[-2.545,-.042,.114],leftToe:[0,0,0],rightToe:[.225,0,0]};
const LEAD=new T.Vector3(.14,-.795,.2),REAR=new T.Vector3(-.15,-.73,-.36),q=new T.Quaternion(),e=new T.Euler(),stand=joints.map(()=>new T.Quaternion()),standBody=new T.Quaternion(),standPos=new T.Vector3(),target=new T.Vector3(),yawAxis=new T.Vector3(0,1,0),lean=new T.Quaternion();
const set=(r:CharacterRig,p:Pose,only?:Joint[])=>{for(const j of only??joints){const v=p[j];if(v)r[j].quaternion.setFromEuler(e.set(v[0],v[1],v[2]));}};
const mix=(r:CharacterRig,p:Pose,w:number,only?:Joint[])=>{if(w<=0)return;w=Math.min(1,w);for(const j of only??joints){const v=p[j];if(v)r[j].quaternion.slerp(q.setFromEuler(e.set(v[0],v[1],v[2])),w);}};
const restore=(r:CharacterRig,w:number,only:Joint[])=>{if(w<=0)return;for(const j of only)r[j].quaternion.slerp(stand[joints.indexOf(j)],Math.min(1,w));};
/** Airborne limbs: they trail the vertical velocity (hang while rising, lift while falling) with a damped, staggered flail. */
function airPose(age:number,vy:number):Pose{
 const lift=T.MathUtils.clamp(-vy*.07,-.45,.6)*sm(age/.14),decay=Math.exp(-age*2)*.22,f=(phase:number)=>Math.sin(age*9+phase)*decay;
 return {pelvis:[0,0,0],spine:[.1,0,0],chest:[.08,0,-.05],neck:[0,0,0],head:[.34*age/.09*Math.exp(1-age/.09)-.18*sm(age/.45),0,.05],
  leftArm:[-.55-.45*lift+f(0),0,.7+.35*lift+f(.6)*.6],rightArm:[-.45-.4*lift+f(.55),0,-.78-.32*lift-f(1.2)*.6],leftElbow:[-.45-.35*lift+f(1.6)*.5,0,0],rightElbow:[-.55-.3*lift+f(2.1)*.5,0,0],leftHand:[.25,0,.15],rightHand:[.3,0,-.15],
  leftLeg:[-.6-.25*lift+f(.8),0,.14],rightLeg:[.2-.2*lift-f(1.4),0,-.12],leftKnee:[.8+.3*lift+f(2.4)*.5,0,0],rightKnee:[.35+.35*lift-f(2.9)*.5,0,0],leftFoot:[.4,0,0],rightFoot:[.35,0,0],leftToe:[0,0,0],rightToe:[0,0,0]};
}
/** Roll heading offset: eased in while tucking (longer for bigger turns), unwound from late in the roll through the rise so
 * the fighter stands facing the aim again. */
const rollHeading=(psi:number,u:number,time:number,from:number,until:number)=>psi*sm(u/(.25+.45*Math.abs(psi)/Math.PI))*(1-sm((time-from)/Math.max(.01,until-from)));
const airTilt=(age:number)=>-1.3*outCubic(age/.5);
/** While falling, the tilted body sinks inside the upright capsule (never below its base), so the landing only finishes a fall
 * that is already under way instead of teleporting .64 m. The landing estimates its impact speed from the flight time. */
const GRAVITY=23,airDrop=(vy:number)=>-.64*sm((-vy-.5)/12);
/** The last airborne sample per rig (vertical speed and body drop), so the landing continues exactly that fall. Client-only;
 * ignored unless it belongs to the same knockdown and ends at most .2 s before landedAt. */
const airMemory=new WeakMap<CharacterRig,{at:number;time:number;vy:number;drop:number}>();
/** The roll tuck anticipates rollAt by 80 ms (limbs first, torso and head curl as the somersault starts). */
const tuck=(r:CharacterRig,time:number,rollAt:number)=>{const w=sm((time-rollAt+.08)/.2);mix(r,TUCK,w,legs);mix(r,TUCK,sm((time-rollAt+.16)/.28),arms);mix(r,TUCK,w*(.6+.4*sm((time-rollAt)/.1)),[...upper,...head]);};
/** How far the weapon hand lets the blade fall (added to the held .5 rad): loose in the air, lying along the arm on the floor,
 * gripped again for the roll and back to the carry by the end of the rise. Continuous across every sub-phase. */
function weaponRelax(k:Knockdown,time:number,age:number){
 const air=(a:number)=>.7*sm(a/.3);if(k.landedAt===undefined||time<k.landedAt)return air(age);
 const landed=air(k.landedAt-k.at),floor=landed+(1.5-landed)*sm((time-k.landedAt)/.15);if(k.rollAt===undefined)return floor;
 // During the roll the blade counter-rotates against the somersault (it stays clear of the floor) and arrives back at the
 // carry angle (a full turn) as the roll opens into the crouch.
 const span=Math.max(.01,(k.rollUntil??k.rollAt+.4)-k.rollAt),rolled=floor+(2*Math.PI-floor)*rollProgress((time-k.rollAt)/span);
 return rolled;
}
/** Somersault progress over the roll: slow while tucking and while opening into the crouch. */
const rollProgress=(u:number)=>{u=sat(u);return u-.9*Math.sin(2*Math.PI*u)/(2*Math.PI);};
/** The roll pitches the lying body (-PI/2) over its head into the crouch lean (.3 - 2 PI). */
const ROLL_END_LEAN=.3,ROLL_PITCH=ROLL_END_LEAN-2*Math.PI+Math.PI/2;
/** Knockdown presentation: an eased fall, a weighted landing, the legs-up pickup, a backward roll that opens into a crouch
 * and a hips-first rise. Pure function of time and the authoritative timestamps (at, landedAt, pickupUntil, rollAt,
 * rollUntil, until); the body is exactly flat (-PI/2) from landedAt+.2 until the roll. Caller guarantees p.knockdown&&time<p.knockdown.until. */
export function knockdownMotion(r:CharacterRig,p:PlayerView,time:number){
 const k=p.knockdown!,phase=knockdownPhase(k,time),age=Math.max(0,time-k.at);
 // Locomotion already posed this frame: it is the standing pose the rise returns to.
 joints.forEach((j,i)=>stand[i].copy(r[j].quaternion));standBody.copy(r.body.quaternion);standPos.copy(r.body.position);
 const grip=weaponRelax(k,time,age);r.weapon.rotation.set(.5+grip,0,0);r.offWeapon.rotation.set(.55+grip,0,0);
 if(phase==='air'){
  const w=sm(age/.12),drop=airDrop(p.vel.y)*w;mix(r,airPose(age,p.vel.y),w);airMemory.set(r,{at:k.at,time,vy:p.vel.y,drop});
  r.body.rotation.set(airTilt(age),0,Math.sin(age*5)*.12*sm(age/.2));r.body.position.set(standPos.x*(1-w),standPos.y*(1-w)+drop,0);return;
 }
 const landed=k.landedAt!,lt=time-landed,landAge=Math.max(0,landed-k.at),tilt=airTilt(landAge);
 const pickup=!k.pickupUsed&&(k.pickupUntil??landed)>landed,legsUp=pickup?sm(lt/.12)*(1-sm((time-k.pickupUntil!)/.12)):0;
 if(phase==='land'||phase==='legs-up'&&lt<KNOCKDOWN_LANDING){
  // Hips hit first (the fall continues from the last airborne frame under gravity, then a small rebound),
  // the arms slap 30 ms later and the head 50 ms later.
  const m=airMemory.get(r),seen=m&&m.at===k.at&&m.time<=landed+1e-6&&landed-m.time<.2,vy=seen?m.vy:-GRAVITY*landAge/2;
  set(r,airPose(landAge,vy));
  const drop=seen?m.drop:airDrop(vy)*sm(landAge/.12),fall=Math.min(.16,Math.max(.05,Math.sqrt(2*Math.abs(-.64-drop)/35))),hips=inQuad(lt/fall),bounce=bump((lt-fall)/(KNOCKDOWN_LANDING-fall))*.025*Math.min(1,(KNOCKDOWN_LANDING-fall)/.12);
  mix(r,FLAT,sm(lt/.09),[...upper,...legs]);mix(r,FLAT,sm((lt-.03)/.09),arms);mix(r,FLAT,sm((lt-.05)/.1),head);
  // Arms and head overshoot into the floor contact once, then settle.
  r.leftArm.rotation.z+=bump((lt-.07)/.12)*.12;r.rightArm.rotation.z-=bump((lt-.07)/.12)*.12;r.head.rotation.x-=bump((lt-.09)/.1)*.12;
  mix(r,LEGS_UP,legsUp,legs);
  const turn=Math.min(.18,.1+.08*Math.abs(-Math.PI/2-tilt)/(Math.PI/2)),x=lt<turn?tilt+(-Math.PI/2-tilt)*inQuad(lt/turn):-Math.PI/2+bump((lt-turn)/(KNOCKDOWN_LANDING-turn))*.035*Math.min(1,(KNOCKDOWN_LANDING-turn)/.1);
  r.body.rotation.set(x,0,Math.sin(landAge*5)*.12*(1-sm(lt/.1)));r.body.position.set(0,drop+(-.64-drop)*hips+bounce,0);return;
 }
 if(phase==='legs-up'||phase==='flat'){
  set(r,FLAT);mix(r,LEGS_UP,legsUp,legs);
  if(k.rollAt!==undefined)tuck(r,time,k.rollAt);
  r.chest.rotation.x+=Math.sin((time-landed)*2.6)*.012;
  r.body.rotation.set(-Math.PI/2,0,0);r.body.position.set(0,-.64,0);return;
 }
 const dx=k.rollX,dz=k.rollZ,psi=dx===undefined||dz===undefined||Math.hypot(dx,dz)<1e-4?0:wrap(Math.atan2(dx,dz)+Math.PI-p.yaw);
 const rollAt=k.rollAt!,rollUntil=k.rollUntil!,span=Math.max(.01,rollUntil-rollAt),endLean=ROLL_END_LEAN,endY=-.32;
 if(phase==='roll'){
  // Head-first backward roll along the recovery direction: tuck, somersault over the shoulders, open into a crouch.
  const u=sat((time-rollAt)/span),s=rollProgress(u),theta=-Math.PI/2+ROLL_PITCH*s;
  set(r,FLAT);tuck(r,time,rollAt);const open=sm((u-.45)/.55);mix(r,CROUCH,open);
  const yaw=rollHeading(psi,u,time,rollAt+.72*span,k.until);r.body.quaternion.setFromEuler(e.set(theta,yaw,0,'YXZ'));r.body.position.set(0,-.64+(endY+.64)*sm(s)+.38*bump(s/.85)+.07*bump((s-.6)/.4),0);
  return;
 }
 // Rise: hips drive up first, the torso straightens after them and the head follows about 40 ms later.
 const v=sat((time-rollUntil)/Math.max(.01,k.until-rollUntil)),hips=1-(1-v)**2,torso=sm((v-.1)/.9),yaw=rollHeading(psi,1,time,rollAt+.72*span,k.until);
 set(r,CROUCH);restore(r,torso,[...upper,...arms]);restore(r,sm((v-.2)/.8),head);
 lean.setFromEuler(e.set(endLean,0,0)).slerp(standBody,torso);r.body.quaternion.setFromAxisAngle(yawAxis,yaw).multiply(lean);
 r.body.position.set(standPos.x*hips,endY+(standPos.y-endY)*hips,0);
 const plant=1-sm((v-.72)/.28);if(plant>0){restore(r,1,legs);stepUp(r,yaw,v,plant);}else restore(r,1,legs);
}
/** Feet planted in the crouch that ends the roll (rig-group space, turned with the roll heading). */
const saved=[new T.Quaternion(),new T.Quaternion(),new T.Quaternion(),new T.Quaternion()],heading=new T.Quaternion(),groupQ=new T.Quaternion();
/** rig-ik foot() solved fully, then blended from the incoming leg by `weight` (foot() alone re-solves the knee even at a
 * small weight, which flips an inverted leg). */
function plant(r:CharacterRig,left:boolean,at:T.Vector3,yaw:number,heel:number,weight:number){
 if(weight<=0)return;const chain=left?[r.leftLeg,r.leftKnee,r.leftFoot,r.leftToe]:[r.rightLeg,r.rightKnee,r.rightFoot,r.rightToe];
 chain.forEach((n,i)=>saved[i].copy(n.quaternion));foot(r,left,at,yaw,heel,1);if(weight<1)chain.forEach((n,i)=>n.quaternion.slerpQuaternions(saved[i],n.quaternion.clone(),weight));
}
/** foot() aims knees along the rig group's forward. While the rolled body still carries a heading offset, solve the legs in a
 * frame turned by that heading (the body's world transform is unchanged), so knees bend over the toes, never backward. */
function withHeading(r:CharacterRig,yaw:number,solve:()=>void){
 if(Math.abs(yaw)<1e-6){solve();return;}
 groupQ.copy(r.group.quaternion);heading.setFromAxisAngle(yawAxis,yaw);r.group.quaternion.multiply(heading);heading.invert();r.body.quaternion.premultiply(heading);r.body.position.applyQuaternion(heading);
 try{solve();}finally{heading.invert();r.body.quaternion.premultiply(heading);r.body.position.applyQuaternion(heading);r.group.quaternion.copy(groupQ);r.group.updateMatrixWorld(true);}
}
/** The rise brings the rear foot up beside the lead foot with a short lifted step, then hands the legs back to locomotion. */
function stepUp(r:CharacterRig,yaw:number,v:number,weight:number){
 const step=sm(v/.85);
 withHeading(r,yaw,()=>{
  target.set(T.MathUtils.lerp(LEAD.x,.13,step),-.795,T.MathUtils.lerp(LEAD.z,0,step));plant(r,true,target,0,0,weight);
  target.set(T.MathUtils.lerp(REAR.x,-.13,step),Math.max(-.795,T.MathUtils.lerp(REAR.y,-.795,step)+bump(step)*.07),T.MathUtils.lerp(REAR.z,0,step));plant(r,false,target,0,-.5*(1-step),weight);
 });
}
