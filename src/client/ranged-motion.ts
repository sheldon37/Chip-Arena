import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,statsFor,type Weapon} from '../shared/content.js';
import {clamp} from '../shared/config.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import {braceWeapon,hand} from './rig-ik.js';
import {poseCurve} from './pose-curves.js';
import {smoothMotion} from './motion-quality.js';
type Pose=ReturnType<typeof attackPose>;
/*
 * Gun handling (presentation only). Every pose derives from authoritative timestamps (lastShot, reloadTime, guarding,
 * blockTime) plus client-only per-rig springs that start on target, clamp dt to .05, reset on backward time or a jump
 * over .35 s, and use hashed timestamps instead of randomness.
 *  - aim:    rifle and machine gun are shouldered (butt in a right-shoulder pocket, torso bladed and taking part of the
 *            pitch, cheek toward the stock); the pistol is an isosceles two-hand grip. Both are solved with braceWeapon.
 *  - travel: the running arm pump stays (every class swings its arms); the weapon itself is held in a stable chest-space
 *            carry orientation instead of windmilling with the forearm.
 *  - reload: tuck and cant, the support hand strips the magazine (it drops on a gravity arc), fetches a new one from the
 *            belt, seats it with a palm slap and charges the weapon; the whole phrase fades in and out of the aim pose.
 *  - recoil: a rise-and-settle response per shot (a spring for the machine gun), spread over grip, muzzle, torso and head.
 *  - guard:  a critically damped guard weight blends into a port-arms barrier (long guns) or a compact cover (pistol).
 */
const _q=new T.Quaternion(),_q2=new T.Quaternion(),_q3=new T.Quaternion(),_qc=new T.Quaternion(),_qd=new T.Quaternion(),_qg=new T.Quaternion(),_qf=new T.Quaternion(),_qi=new T.Quaternion(),_qw=new T.Quaternion();
const _v=new T.Vector3(),_v2=new T.Vector3(),_t=new T.Vector3(),_eu=new T.Euler(),_eu2=new T.Euler(),_lh=new T.Vector3();
const X=new T.Vector3(1,0,0);
/** Deterministic [0,1) hash of a timestamp (no randomness in pose code). */
export const hashTime=(x:number)=>{const s=Math.sin(x*127.1+311.7)*43758.5453;return s-Math.floor(s);};
/** Semi-implicit damped spring, sub-stepped (<=1/240 s) so a stiff spring stays stable at 30 Hz. Writes [x,v] into `out`. */
export function springStep(x:number,v:number,target:number,omega:number,zeta:number,dt:number,out:[number,number]){
 const n=Math.max(1,Math.ceil(dt*240)),h=dt/n;for(let i=0;i<n;i++){v+=(-(x-target)*omega*omega-2*zeta*omega*v)*h;x+=v*h;}out[0]=x;out[1]=v;return out;
}
const _s:[number,number]=[0,0];
/** Per-rig clock: the step since the last sample clamped to .05; `fresh` on the first sample, backward time or a jump over .35 s. */
export function clockStep(c:{t:number}|undefined,time:number){const step=c?time-c.t:-1;const fresh=!c||step<0||step>.35;return {fresh,dt:fresh?0:Math.min(step,.05)};}
interface RangedState{t:number;spec:string;kick:number;kickV:number;shot:number;headKick:number;guard:number;guardV:number}
const states=new WeakMap<CharacterRig,RangedState>();
/** Reload duration exactly as the simulation sets it: max(.2, w.reload * statsFor(chips).reloadSpeed), memoised per chips. */
const speedByChips=new WeakMap<object,number>(),speedByKey=new Map<string,number>();
export function reloadDuration(p:PlayerView,w:Weapon=WEAPONS[p.spec]){
 const chips=p.chips as Record<string,number>|undefined;if(!chips)return Math.max(.2,w.reload);
 let m=speedByChips.get(chips);if(m===undefined){const key=Object.keys(chips).sort().map(k=>k+':'+chips[k]).join();m=speedByKey.get(key);if(m===undefined){m=statsFor(chips).reloadSpeed;if(speedByKey.size<256)speedByKey.set(key,m);}speedByChips.set(chips,m);}
 return Math.max(.2,w.reload*m);
}
/** Reload phase u (0 at the authoritative reload start, 1 at its end) and the aim<->reload fade envelope. */
export function reloadPhase(p:PlayerView,w:Weapon=WEAPONS[p.spec]){
 if(!(p.reloadTime>0))return {u:0,envelope:0,elapsed:0,duration:0};
 const duration=reloadDuration(p,w),elapsed=Math.max(0,duration-p.reloadTime);
 return {u:clamp(elapsed/duration,0,1),envelope:smoothMotion(elapsed/.14)*smoothMotion(p.reloadTime/.18),elapsed,duration};
}
/** Held-part offsets (magazine, pistol slide) are restored every sample before any layer moves them, so a cancelled or
 * interrupted reload (round reset, hitstun, knockdown, spec change) never leaves a part floating. */
interface Moved{node:T.Object3D;pos:T.Vector3;quat:T.Quaternion}
const touched=new WeakMap<object,Moved[]>();
function touch(r:CharacterRig,node:T.Object3D){let list=touched.get(r);if(!list){list=[];touched.set(r,list);}if(!list.some(m=>m.node===node))list.push({node,pos:node.userData.restPos??=node.position.clone(),quat:node.userData.restQuat??=node.quaternion.clone()});return node;}
export function restoreHeldParts(r:object){const list=touched.get(r);if(!list||!list.length)return;for(const m of list){m.node.position.copy(m.pos);m.node.quaternion.copy(m.quat);m.node.visible=true;}list.length=0;}
/** Turn a held weapon toward a chest-space orientation: `wristShare` of the correction goes to the wrist, the rest to the
 * weapon node, blended by `weight`. */
export function orientHeld(r:CharacterRig,wrist:T.Object3D,weapon:T.Object3D,chestOrientation:T.Quaternion,weight:number,wristShare=.5){
 if(weight<=.001)return;
 const chestInv=r.chest.getWorldQuaternion(_qi).invert(),cw=weapon.getWorldQuaternion(_q).premultiply(chestInv);
 const final=_qf.copy(cw).slerp(chestOrientation,clamp(weight,0,1));
 const delta=_q2.copy(cw).invert().premultiply(final);_qg.identity().slerp(delta,wristShare);
 const socketChest=weapon.parent!.getWorldQuaternion(_q3).premultiply(chestInv);
 const parentChest=wrist.parent!.getWorldQuaternion(_qw).premultiply(chestInv),wristChest=_qd.copy(parentChest).multiply(wrist.quaternion).premultiply(_qg);
 wrist.quaternion.copy(parentChest.invert().multiply(wristChest));
 weapon.quaternion.copy(socketChest.premultiply(_qg).invert().multiply(final));
}
/** Chest orientation relative to the rig group (the aim frame: yaw is the group's). */
function chestInGroup(r:CharacterRig,out:T.Quaternion){r.chest.getWorldQuaternion(out);return out.premultiply(r.group.getWorldQuaternion(_q).invert());}
/** Default butt position (weapon space) when a long gun has no 'stock-butt' marker. */
const BUTT_FALLBACK=new T.Vector3(0,-.04,-.30);
/** Right-shoulder pocket (chest space): the front face of the ribcage inside the right shoulder. */
export const POCKET=new T.Vector3(-.11,.245,.155);
/** Live tuning of the shouldered hold (exported for the numeric probes; presentation only). */
export const RANGED_TUNE={rifleBlade:.75,mgBlade:.75,rightProtract:.9,leftProtract:.6};
/** Isosceles pistol: pivot between the shoulders and the grip's offset along the aim (weapon space). */
const PISTOL_PIVOT=new T.Vector3(-.035,.215,0),PISTOL_REACH=new T.Vector3(0,.075,.37);
/** Belt pouch the support hand fetches a fresh magazine from (chest space). */
const POUCH=new T.Vector3(.15,-.33,.07);
function weaponPoint(r:CharacterRig,local:T.Vector3,out:T.Vector3){r.weapon.updateWorldMatrix(true,false);out.copy(local).applyMatrix4(r.weapon.matrixWorld);r.chest.updateWorldMatrix(true,false);return r.chest.worldToLocal(out);}
/** Swing during travel; settle into the aiming stance when stopping, strafing, backpedalling, guarding or firing.
 * Called for EVERY class at the same point of sampleCharacter, because melee and magic also decay aimWeight. */
export function updateAimWeight(r:CharacterRig,p:PlayerView,pose:Pose,w:Weapon,dt:number){
 let aimTarget=0;
 if(w.class==='ranged'){
  // Combat strafe and backpedal keep the gun up; only running forward lowers it into the travel carry.
  const speed=Math.hypot(p.vel.x,p.vel.z),ahead=speed>1?(p.vel.x*Math.sin(p.yaw)+p.vel.z*Math.cos(p.yaw))/speed:1;
  aimTarget=pose.shotAge<Math.max(.38,w.interval+.15)||p.reloadTime>0||r.motion<.18||ahead<.6||p.guarding||p.blockTime>0?1:0;
 }
 // Critically damped (starts at rest, so raising the gun out of a run eases in); stiffer when a shot fires from a low carry.
 const s=aimSprings.get(r),h=clamp(dt,0,.05),omega=w.class==='ranged'&&aimTarget>r.aimWeight&&pose.shotAge<.1&&r.aimWeight<.6?32:24;
 if(!s){aimSprings.set(r,{v:0});r.aimWeight+=(aimTarget-r.aimWeight)*(1-Math.exp(-h*24));return;}
 springStep(r.aimWeight,s.v,aimTarget,omega,1,h,_s);r.aimWeight=clamp(_s[0],0,1);s.v=r.aimWeight===_s[0]?_s[1]:0;
}
const aimSprings=new WeakMap<CharacterRig,{v:number}>();
/** Chest-space carry orientation while running: rifle/MG barrel forward-down across the body, pistol muzzle-down low-ready. */
function carryOrientation(spec:string,stride:number,out:T.Quaternion){
 const long=spec!=='pistol',s=Math.sin(stride),c=Math.cos(2*stride);
 return out.setFromEuler(_eu2.set(long?.3+c*.05:.62+c*.04,long?.38+s*.06:.18+s*.05,long?-.35:-.12));
}
const TUCK_T=[0,.12,.8,.95,1],TUCK_V=[0,1,1,0,0];
/** Ranged aim, recoil, isosceles pistol, shouldered long guns, reload choreography, guard and the travel carry. */
export function rangedLayer(r:CharacterRig,p:PlayerView,pose:Pose,time:number,dt:number,pitch:number){
 const w=WEAPONS[p.spec],spec=p.spec,long=spec!=='pistol',mg=spec==='machinegun';
 restoreHeldParts(r);
 let s=states.get(r);const clock=clockStep(s,time);
 if(!s){s={t:time,spec,kick:0,kickV:0,shot:-1e9,headKick:0,guard:0,guardV:0};states.set(r,s);}
 const fresh=clock.fresh||s.spec!==spec,step=clock.dt;s.t=time;s.spec=spec;
 // Recoil: a ~30 ms rise, then a damped return (the pistol overshoots ~10%, the rifle settles critically); machine-gun
 // shots feed a spring so bursts (and shots merged into one 20 Hz snapshot) build a steady shake instead of a buzz.
 const shotAt=p.lastShot??p.lastAttack,shotAge=time-shotAt;
 const single=(age:number)=>{if(age<0||age>.8)return 0;if(age<.03)return Math.sin(Math.PI/2*age/.03);const u=age-.03;if(spec==='pistol'){const o=24,z=.6,wd=o*Math.sqrt(1-z*z);return Math.exp(-z*o*u)*(Math.cos(wd*u)+z*o/wd*Math.sin(wd*u));}const o=18;return (1+o*u)*Math.exp(-o*u);};
 let kick:number;
 if(mg){
  if(fresh){s.kick=single(shotAge)*.55;s.kickV=0;s.shot=shotAt;}
  else{if(shotAt>s.shot+1e-6&&shotAge>=0&&shotAge<.2){s.kickV+=.35*28;s.shot=shotAt;}springStep(s.kick,s.kickV,0,28,.55,step,_s);s.kick=_s[0];s.kickV=_s[1];}
  kick=clamp(s.kick,-.4,1.4)*1.15;
 }else{kick=single(shotAge)*(spec==='rifle'?1.7:1);s.shot=shotAt;}
 const headKick=fresh?kick:s.headKick;s.headKick=kick;
 // Guard weight: critically damped, ~70 ms in and ~150 ms out.
 const guarding=p.guarding||p.blockTime>0?1:0;
 if(fresh){s.guard=guarding;s.guardV=0;}else{springStep(s.guard,s.guardV,guarding,guarding>s.guard?24:16,1,step,_s);s.guard=clamp(_s[0],0,1);s.guardV=_s[1];}
 const g=s.guard,a=r.aimWeight,hold=a+(1-a)*g;
 const reload=reloadPhase(p,w),e=reload.envelope*(1-g),u=reload.u,tuck=poseCurve(TUCK_T,TUCK_V,u)*e;
 const firing=mg?smoothMotion(1-shotAge/.3)*a:0;
 // Torso: cancel the travel lean and roll, blade behind a long gun and take part of the pitch (the brace keeps the rest).
 const blade=(spec==='rifle'?RANGED_TUNE.rifleBlade:mg?RANGED_TUNE.mgBlade:.04)*a*(1-g);
 r.spine.rotation.x=T.MathUtils.lerp(r.spine.rotation.x,-r.body.rotation.x,hold)-pitch*.22*a+firing*.08+g*.12;
 r.chest.rotation.z=T.MathUtils.lerp(r.chest.rotation.z,-r.body.rotation.z-r.pelvis.rotation.z,hold);
 r.chest.rotation.x=T.MathUtils.lerp(r.chest.rotation.x,0,hold)-pitch*.18*a-kick*.05*a+tuck*.06;
 r.spine.rotation.y=T.MathUtils.lerp(r.spine.rotation.y,-blade*.45,hold);
 r.chest.rotation.y=T.MathUtils.lerp(r.chest.rotation.y,-blade*.55,hold)+tuck*.2;
 r.neck.rotation.x-=pitch*.10*a;
 // Head: counter the blade, keep the previous world pitch, and (rifle; lighter on the MG) settle the cheek toward the stock.
 const weld=(spec==='rifle'?1:mg?.55:0)*a*(1-g)*(1-e);
 r.head.rotation.x+=pitch*.5*a+weld*.12+headKick*.05*a+g*.2+tuck*.25;
 r.head.rotation.y+=blade+weld*.06;
 r.head.rotation.z=T.MathUtils.lerp(r.head.rotation.z,long?0:-.08,hold)+weld*.06;r.neck.rotation.z+=weld*.12;r.neck.rotation.x+=weld*.15;
 r.leftShoulder.rotation.y-=(long?RANGED_TUNE.leftProtract:.15)*hold;r.rightShoulder.rotation.y+=(long?RANGED_TUNE.rightProtract:0)*hold;
 r.rightShoulder.rotation.y-=kick*.08*a;
 // The aimed weapon in the group frame (barrel along the aim, recoil rise, hashed MG yaw/roll), then in chest space.
 const jitter=mg?clamp(s.kick,0,1.2):0,jy=(hashTime(shotAt)-.5)*.06*jitter,jr=(hashTime(shotAt+1.7)-.5)*.08*jitter;
 _qg.setFromEuler(_eu.set(-pitch-kick*.13,jy,jr,'YXZ'));
 const aimQ=chestInGroup(r,_qc).invert().multiply(_qg);
 // Brace target (weapon origin = wrist): shouldered long guns hang from the pocket by the butt; the pistol from the pivot.
 if(long)_t.copy(r.weapon.getObjectByName('stock-butt')?.position??BUTT_FALLBACK).applyQuaternion(aimQ).negate().add(POCKET);
 else _t.copy(PISTOL_REACH).applyQuaternion(aimQ).add(PISTOL_PIVOT);
 _t.addScaledVector(_v.set(0,0,1).applyQuaternion(aimQ),-kick*(long?.045:.035));
 // Guard: long guns become a diagonal port-arms barrier (muzzle up-left); the pistol comes to a two-hand high ready at the sternum, muzzle up.
 if(g>.001){aimQ.slerp(_q3.setFromEuler(_eu2.set(long?-1.05:-1.25,long?.55:.1,long?.75:0,'YXZ')),g);_t.lerp(_v2.set(long?-.1:-.06,long?.14:.2,long?.3:.24),g);}
 // Reload: tuck and cant the gun toward the chest (the pistol comes inboard and up), hold, then ease back to the aim.
 if(e>0){
  const charge=poseCurve([0,.78,.83,.89,.94,1],[0,0,1,1,0,0],u)*e;
  aimQ.multiply(_q3.setFromEuler(_eu2.set(-.25*tuck,long?.1*tuck:-.25*tuck,.5*tuck))).multiply(_q3.setFromAxisAngle(X,-.08*charge));
  _t.add(_v2.set(long?.05:.09,long?-.02:-.04,long?-.06:-.12).multiplyScalar(tuck));
 }
 r.weapon.rotation.x=T.MathUtils.lerp(.15,1.5,a);
 braceWeapon(r,_t,_eu.setFromQuaternion(aimQ),hold);
 // Travel carry: the arms keep pumping, but the weapon holds a stable chest-space orientation instead of windmilling.
 if(hold<.999)orientHeld(r,r.rightHand,r.weapon,carryOrientation(spec,r.stride,_q3),(1-hold)*smoothMotion(r.motion/.35),.55);
 if(e>0)reloadHands(r,spec,u,e,reload.elapsed,reload.duration,aimQ);
}
/** Left-hand IK that leaves the (hidden) off-hand weapon socket alone. */
const _op=new T.Vector3(),_oq=new T.Quaternion();
function supportReach(r:CharacterRig,target:T.Vector3,orientation:T.Euler,weight:number){_op.copy(r.offWeapon.position);_oq.copy(r.offWeapon.quaternion);hand(r,true,target,orientation,weight);r.offWeapon.position.copy(_op);r.offWeapon.quaternion.copy(_oq);}
/** Weapon-space key points for the support hand: magwell (wrist below the magazine) and the charge (bolt knob, top cover, slide). */
const MAGWELL:Record<string,T.Vector3>={pistol:new T.Vector3(0,-.30,-.07),rifle:new T.Vector3(0,-.27,.16),machinegun:new T.Vector3(.02,-.27,.2)};
const CHARGE:Record<string,[T.Vector3,T.Vector3]>={pistol:[new T.Vector3(0,.12,-.03),new T.Vector3(0,.12,-.08)],rifle:[new T.Vector3(-.12,.06,-.02),new T.Vector3(-.12,.06,-.09)],machinegun:[new T.Vector3(0,.24,.02),new T.Vector3(0,.16,.02)]};
const _k=[new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3()];
const HAND_T=[0,.1,.2,.3,.42,.5,.6,.68,.71,.74,.83,.89,.97,1];
/** Support-hand reload choreography over the normalised phase u, faded by the envelope e. */
function reloadHands(r:CharacterRig,spec:string,u:number,e:number,elapsed:number,duration:number,aimQ:T.Quaternion){
 const grip=r.weapon.getObjectByName('support-grip'),mag=r.weapon.getObjectByName('magazine');
 const well=MAGWELL[spec]??MAGWELL.rifle,[c0,c1]=CHARGE[spec]??CHARGE.rifle;
 const start=weaponPoint(r,grip?grip.position:_v.set(0,-.05,.1),_k[0]),wellP=weaponPoint(r,well,_k[1]),strip=weaponPoint(r,_v.copy(well).add(_v2.set(0,-.13,-.02)),_k[2]);
 const slap=weaponPoint(r,_v.copy(well).add(_v2.set(0,-.05,0)),_k[3]),ch0=weaponPoint(r,c0,_k[4]),ch1=weaponPoint(r,c1,_k[5]);
 const pts=[start,start,wellP,strip,POUCH,POUCH,strip,wellP,slap,wellP,ch0,ch1,start,start];
 _lh.set(poseCurve(HAND_T,pts.map(q=>q.x),u),poseCurve(HAND_T,pts.map(q=>q.y),u),poseCurve(HAND_T,pts.map(q=>q.z),u));
 const reach=poseCurve([0,.08,.14,.94,1],[0,0,1,1,0],u)*e;
 if(reach>.001){_q.copy(aimQ);if(u>.34&&u<.6)_q.multiply(_q2.setFromAxisAngle(X,.9*poseCurve([.34,.42,.52,.6],[0,1,1,0],u)));supportReach(r,_lh,_eu2.setFromQuaternion(_q),reach);}
 // Magazine: rides the hand out, drops on a gravity arc (hidden after .35 s), reappears in the hand at the pouch and seats.
 if(mag){
  touch(r,mag);const rest=mag.userData.restPos as T.Vector3,dropU=.3;
  if(u>=.2&&u<dropU){const out=poseCurve([.2,.3],[0,1],u);mag.position.copy(rest).add(_v2.set(0,-.13*out,-.02*out));}
  else if(u>=dropU&&u<.5){
   const fall=Math.max(0,elapsed-dropU*duration);
   if(fall>.35)mag.visible=false;
   else{mag.position.copy(rest).add(_v2.set(-.18*fall,-.13-4.9*fall*fall-.6*fall,-.02));mag.quaternion.setFromEuler(_eu2.set(fall*5,0,fall*3));}
  }else if(u>=.5&&u<.68){
   // Carried at the palm (converted into weapon space), then seated on the socket origin.
   r.leftHand.updateWorldMatrix(true,false);_v.set(0,-.1,.02).applyMatrix4(r.leftHand.matrixWorld);r.weapon.worldToLocal(_v);
   _v.sub(_v2.set(0,.08,0).add(well).sub(rest));
   mag.position.lerpVectors(_v,rest,smoothMotion((u-.6)/.08));
  }
 }
 // Charge: the pistol slide racks 4 cm while the hand pulls it; the long guns' hand does the work (bolt pull, top-cover slap).
 if(spec==='pistol'){const slide=r.weapon.getObjectByName('pistol-slide');if(slide){touch(r,slide);slide.position.z=(slide.userData.restPos as T.Vector3).z-.04*poseCurve([0,.83,.87,.9,.94,1],[0,0,1,1,0,0],u)*e;}}
}
/** Non-melee guard hook: ranged guards blend inside rangedLayer and magic guards inside magicLayer (spring weights). */
export function nonMeleeGuard(_r:CharacterRig,_p:PlayerView){}
/** Renderer-side timed-block arm override for non-melee classes: superseded by the blended guards (kept as a no-op hook). */
export function rangedBlockArms(_r:{block:T.Object3D;leftArm:T.Object3D;rightArm:T.Object3D},_w:{class:string}){}
