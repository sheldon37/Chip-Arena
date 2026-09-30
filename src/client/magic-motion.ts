import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {clamp} from '../shared/config.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import {smoothMotion} from './motion-quality.js';
import {blendRotation} from './pose-math.js';
import {hand} from './rig-ik.js';
import {orientHeld,springStep,clockStep} from './ranged-motion.js';
/*
 * Casting phrase (presentation only). spellPose timing is untouched: drive===1 exactly at the authoritative release
 * (releaseAt = castWindup), and the release frame is analytic (a pure function of time), so a fresh rig sampled once at
 * release shows the full free-hand extension. The phrase is a pure function of lastAttack/lastShot:
 *   start (rest, or wherever the previous cast of a streak was) -> coil (anticipation, torso wound up) -> drive (an
 *   accelerating curve, so the hand arrives with speed) -> follow-through past release (wrist flick, extra twist) ->
 *   ease-out settle into a 'ready' guard at the chest -> fade to rest once no cast followed for .45 s.
 * Element flavours: fire overhand hurl, water low circular sweep, lightning snap-point from ready, darkness claw
 * pull-back then a heavy palm thrust. The staff stays in the left hand, held vertical in chest space (it dips back on
 * the coil and tips toward the target at release). Feet stay planted: weight shifts through the body, not a leg slide.
 */
// Channels: arm x,y,z | elbow x | hand x,y,z | twist | body lean | body shift z | body drop
type Pose=Float64Array;
const N=11,mk=()=>new Float64Array(N);
const _a=mk(),_b=mk(),_c=mk(),_start=mk(),_out=mk(),_prev=mk();
function setPose(o:Pose,...v:number[]){for(let i=0;i<N;i++)o[i]=v[i]??0;return o;}
function mix(o:Pose,a:Pose,b:Pose,t:number){for(let i=0;i<N;i++)o[i]=a[i]+(b[i]-a[i])*t;return o;}
/** Ready guard between casts of a streak: palm forward at the chest, elbow folded. */
function ready(o:Pose,spec:string,pitch:number){
 // Lightning snaps from a pointing ready (arm already extended); the others hold the palm at the chest.
 if(spec==='lightning')return setPose(o,-1.35-pitch*.6,.12,-.12,-.45,-.1,.15,.15,.12,.05,.05,.02);
 return setPose(o,-1.0-pitch*.45,.15,-.25,-1.3,-.1,.3,.3,-.05,.02,.02,0);
}
/** Coil (end of anticipation), per element. */
function coil(o:Pose,spec:string,pitch:number){
 if(spec==='water')return setPose(o,-.55,-.75,-.95,-1.15,.2,-.7,.45,-.53,-.03,-.05,.035);
 if(spec==='lightning')return setPose(o,-1.2-pitch*.45,.3,-.3,-.95,.25,.35,.3,-.2,-.03,-.02,.015);
 if(spec==='darkness')return setPose(o,-1.35,.55,-.3,-2.25,.35,.2,.55,-.6,-.1,-.08,.04);
 return setPose(o,-2.3,.3,-.42,-1.75,.2,.45,.45,-.48,-.09,-.065,.03);
}
/** Release frame (full free-hand extension toward the aim). Keeps arm x<-1.2 and elbow>-.2 at pitch 0. */
function release(o:Pose,spec:string,pitch:number){
 const arm=-(spec==='darkness'?1.95:1.65)+(spec==='lightning'?.25:.18)-pitch*.65;
 return setPose(o,arm,spec==='water'?-.12:0,-.04,-.08,-.18,0,0,spec==='darkness'?.46:.4,.17,.13,.05);
}
/** Follow-through: the drive carries on past release, the wrist flicks and the torso keeps turning. */
function follow(o:Pose,spec:string,pitch:number){release(o,spec,pitch);o[0]+=spec==='darkness'?.08:.12;o[1]+=spec==='water'?.25:.05;o[3]-=.12;o[4]-=spec==='fire'?.5:.3;o[7]+=.1;o[8]+=.03;o[9]+=.02;o[10]+=.015;return o;}
/** Pure phrase pose at `age` since lastAttack; `start` is where the arm begins (rest weight is returned separately). */
function phrase(o:Pose,spec:string,pitch:number,age:number,windup:number,releaseAt:number,rate:number,start:Pose){
 const F=.1/rate,S=.3/rate;
 if(age<windup)return mix(o,start,coil(_a,spec,pitch),smoothMotion(age/Math.max(.01,windup)));
 if(age<releaseAt){const d=Math.pow(clamp((age-windup)/Math.max(.01,releaseAt-windup),0,1),1.4);return mix(o,coil(_a,spec,pitch),release(_b,spec,pitch),d);}
 if(age<releaseAt+F){const f=(age-releaseAt)/F;return mix(o,release(_a,spec,pitch),follow(_b,spec,pitch),f*(2-f));}
 return mix(o,follow(_a,spec,pitch),ready(_b,spec,pitch),smoothMotion((age-releaseAt-F)/S));
}
/** How long the ready guard is held after a release before the arm relaxes to rest (streak window). */
const STREAK=.45,RELAX=.3;
/** Right-arm cast pose and its weight over rest, a pure function of the authoritative timestamps. */
export function castPose(p:PlayerView,time:number,m:ReturnType<typeof attackPose>['magic'],pitch:number,out:Pose=_out){
 const rate=p.attackRate||1,spec=p.spec,age=time-p.lastAttack,shot=p.lastShot??-1e9;
 const released=shot>=p.lastAttack-1e-6&&shot<=time+1e-6;
 if(age<0||age>10)return 0;
 // Start pose: where the previous cast's phrase is at this cast's start (its ready guard once it has settled), weighted
 // by how much of that ready guard was still held; the coil ramps the rest of the weight in from rest.
 const hold=(since:number)=>1-smoothMotion((since-STREAK)/RELAX),ramp=age<m.windup?smoothMotion(age/Math.max(.01,m.windup)):1;
 let w:number;
 if(shot<p.lastAttack&&p.lastAttack-shot<STREAK+RELAX){const w0=hold(p.lastAttack-shot);phrase(_start,spec,pitch,p.lastAttack-(shot-m.releaseAt),m.windup,m.releaseAt,rate,ready(_c,spec,pitch));w=w0+(1-w0)*ramp;}
 else{ready(_start,spec,pitch);w=ramp;}
 phrase(out,spec,pitch,age,m.windup,m.releaseAt,rate,_start);
 // Once released, hold the ready guard through the streak window, then relax to rest.
 if(released&&age>m.releaseAt)w*=hold(time-shot);
 return w;
}
interface MagicState{t:number;guard:number;guardV:number}
const states=new WeakMap<CharacterRig,MagicState>();
const _q=new T.Quaternion(),_eu=new T.Euler(),_v=new T.Vector3(),_wp=new T.Vector3(),_wq=new T.Quaternion(),_wp2=new T.Vector3(),_gs:[number,number]=[0,0],_op=new T.Vector3(),_oq=new T.Quaternion();
/** Magic staff carry, the coil -> drive -> follow-through -> ready casting phrase, and the staff guard. */
export function magicLayer(r:CharacterRig,p:PlayerView,pose:ReturnType<typeof attackPose>,time:number,dt:number,pitch:number){
 if(r.weapon.parent!==r.sockets.leftHand)r.sockets.leftHand.add(r.weapon);
 const m=pose.magic,spec=p.spec;
 const carry=1-smoothMotion((r.motion-.10)/.65);blendRotation(r.leftArm,-.24-pitch*.08,-.08,.16,carry);blendRotation(r.leftElbow,-.42,0,0,carry);r.leftHand.rotation.set(0,0,0);
 r.weapon.rotation.set(.15,0,0);
 const c=p.hp>0?castPose(p,time,m,pitch,_out):0,o=_out;
 let tilt=.12;
 if(c>.001){
  const twist=o[7]*(spec==='darkness'?1.15:spec==='water'?1.1:1)*c;
  // Hip/shoulder separation: pelvis leads, chest follows, the head stays on the target.
  r.pelvis.rotation.y+=twist*.35;r.spine.rotation.y+=twist*.65;r.chest.rotation.y+=twist*.48;r.head.rotation.y=-twist*.55;
  // Weight transfer through the body (feet stay planted; the ground pass bends the knees).
  r.body.rotation.x+=o[8]*c;r.body.position.z+=o[9]*c;r.body.position.y-=o[10]*c;
  r.rightShoulder.rotation.y=-.12*c;
  blendRotation(r.rightArm,o[0],o[1],o[2],c);blendRotation(r.rightElbow,o[3],0,0,c);
  blendRotation(r.rightHand,o[4],o[5],o[6],c);
  // Staff: dips back on the coil, tips toward the target through the release.
  const age=time-p.lastAttack,coilW=age<m.windup?smoothMotion(age/Math.max(.01,m.windup)):1-smoothMotion((age-m.windup)/Math.max(.01,m.releaseAt-m.windup)),out=age>=m.windup?smoothMotion((age-m.windup)/Math.max(.01,m.releaseAt-m.windup))*(1-smoothMotion((age-m.releaseAt-.1)/.3)):0;
  tilt+=(-.2*coilW+.12*out)*c;
 }
 // Staff held upright in chest space (it no longer windmills with the carrying arm), with a small stride sway.
 const run=clamp(r.motion,0,1);
 orientHeld(r,r.leftHand,r.weapon,_q.setFromEuler(_eu.set(-Math.PI/2+tilt+Math.cos(2*r.stride)*.04*run,0,Math.sin(r.stride)*.08*run)),1,.35);
 magicGuard(r,p,time);
}
/** Staff guard: a critically damped weight (~100 ms in, ~150 ms out) brings the staff horizontal across the chest in both
 * hands (the right hand on the 'guard-grip' socket; without it the left hand keeps its hold). */
function magicGuard(r:CharacterRig,p:PlayerView,time:number){
 let s=states.get(r);const clock=clockStep(s,time),target=p.guarding||p.blockTime>0?1:0;
 if(!s){s={t:time,guard:target,guardV:0};states.set(r,s);}
 s.t=time;if(clock.fresh){s.guard=target;s.guardV=0;}else{springStep(s.guard,s.guardV,target,target>s.guard?17:14,1,clock.dt,_gs);s.guard=clamp(_gs[0],0,1);s.guardV=_gs[1];}
 const g=s.guard;if(g<=.001)return;
 r.spine.rotation.y*=1-g;r.chest.rotation.x+=.1*g;r.head.rotation.x+=.15*g;
 // Left hand out front at the left of the chest, the staff laid across toward the right.
 saveWeapon(r);hand(r,true,_v.set(.2,.22,.34),_eu.set(-1.5,0,-.2),g);restoreWeapon(r);
 orientHeld(r,r.leftHand,r.weapon,_q.setFromEuler(_eu.set(-.25,-Math.PI/2,0,'YXZ')),g,.3);
 const grip=r.weapon.getObjectByName('guard-grip');
 if(grip){grip.getWorldPosition(_wp);r.chest.updateWorldMatrix(true,false);r.chest.worldToLocal(_wp);saveWeapon(r);hand(r,false,_wp,_eu.set(-1.5,0,.2),g);restoreWeapon(r);}
}
function saveWeapon(r:CharacterRig){_op.copy(r.weapon.position);_oq.copy(r.weapon.quaternion);_wp2.copy(r.offWeapon.position);_wq.copy(r.offWeapon.quaternion);}
function restoreWeapon(r:CharacterRig){r.weapon.position.copy(_op);r.weapon.quaternion.copy(_oq);r.offWeapon.position.copy(_wp2);r.offWeapon.quaternion.copy(_wq);}
