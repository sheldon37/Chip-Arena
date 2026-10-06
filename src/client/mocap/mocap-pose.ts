import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import {braceWeapon,hand,supportHand} from '../rig-ik.js';
import {CH} from './mocap-clip.js';
import {clearBlade,FLOOR_CLEAR} from './blade-clearance.js';
import {markMocapPosed} from '../pose-flags.js';
/** Poses the voxel rig from one sampled mocap row (mocap-clip.ts) at `weight` over whatever the layers below posed.
 * Torso, clavicles and legs take the retargeted joint rotations (the ground pass then re-plants the legs on the floor);
 * the arms go through the rig's own IK: the main hand to the retargeted grip with the blade along the captured blade
 * and edge directions, the support hand on the weapon's support grip while the capture holds the sword two-handed,
 * otherwise on its own captured target. Root travel is stripped (the server owns movement); `travel` (0..1) would admit
 * the clip's horizontal hips offset, and no current state uses it.
 * `legs` (0..1, of weight) poses the hips, knees and ankles; `root` (0..1) the heading, hips height and pelvis (locomotion
 * passes 0: the gait owns them); `arms:false` leaves both arms and the weapon to the layers below. */
/** `clear`: blade clearance (blade-clearance.ts): 'full' turns the blade and moves the grip; 'turn' (fast cuts, where a grip
 * shift would whip the elbows) only turns the blade. */
/** `oneHand`: a one-handed weapon's capture (sword, daggers, shield): the support hand never reaches for the weapon's
 * support grip, whatever the blend weight; the captured free hand poses it. `pommel` / `halfWidth`: the blade's extent
 * behind the grip (× the weapon's length scale) and slab half-width for the clearance (greatsword .27 / .09); clear 'none'
 * skips it (the shield is a plate, not a blade). `floor`: the clearance also keeps the blade above the floor plane. */
export interface MocapPoseOptions {weight:number;travel?:number;legs?:number;root?:number;arms?:boolean;clear?:'full'|'turn'|'none';oneHand?:boolean;floor?:boolean;pommel?:number;halfWidth?:number}
const _q=new T.Quaternion(),_v=new T.Vector3(),_lp=new T.Vector3(),_cq=new T.Quaternion(),_a=new T.Vector3(),_b=new T.Vector3(),_c=new T.Vector3(),_m=new T.Matrix4(),_e=new T.Euler(),_p=new T.Vector3();
const X=new T.Vector3(1,0,0),Y=new T.Vector3(0,1,0),_yq=new T.Quaternion();
function rot(f:Float32Array,i:number,out:T.Quaternion){_v.set(f[i],f[i+1],f[i+2]);const a=_v.length();return a<1e-9?out.identity():out.setFromAxisAngle(_v.divideScalar(a),a);}
function set(node:T.Object3D,q:T.Quaternion,w:number){if(w>=1)node.quaternion.copy(q);else node.quaternion.slerp(q,w);}
/** Chest-space blade (into `_a`) and edge (into `_b`, perpendicular) from a row. */
function bladeAxes(f:Float32Array,z:number,x:number){
 _a.set(f[z],f[z+1],f[z+2]).normalize();_b.set(f[x],f[x+1],f[x+2]);_b.addScaledVector(_a,-_b.dot(_a));if(_b.lengthSq()<1e-8)_b.set(1,0,0).addScaledVector(_a,-_a.x);_b.normalize();
}
/** Chest-space basis (blade `_a` along +z, edge `_b` along +x) as the wrist's chest-space Euler. */
function basisEuler(out:T.Euler){_c.crossVectors(_a,_b);return out.setFromRotationMatrix(_m.makeBasis(_b,_c,_a));}
/** The weapon's extent along its blade from the grip (pommel, blade end), in the weapon's current length scale; kept in the
 * weapon's userData, which buildWeapon resets (a respec on a reused rig measures the new weapon). */
function bladeEnd(weapon:T.Object3D){let z=weapon.userData.bladeEnd as number|undefined;if(z===undefined){z=(weapon.getObjectByName('blade-tip')?.position.z??1.4)-.08;weapon.userData.bladeEnd=z;}return z*weapon.scale.z;}
function basisEulerYZ(f:Float32Array,z:number,y:number,out:T.Euler){
 _a.set(f[z],f[z+1],f[z+2]).normalize();_c.set(f[y],f[y+1],f[y+2]);_c.addScaledVector(_a,-_c.dot(_a));if(_c.lengthSq()<1e-8)_c.set(0,1,0);_c.normalize();
 _b.crossVectors(_c,_a);return out.setFromRotationMatrix(_m.makeBasis(_b,_c,_a));
}
/** rig-ik's default elbow poles (chest space ≈ clavicle space). */
const POLE_R=new T.Vector3(-1,-.7,-.2).normalize(),POLE_L=new T.Vector3(1,-.8,-.25).normalize(),_d=new T.Vector3();
/** The clip's elbow pole for a hand target, into the clavicle's frame (rig-ik solves arms there). The converter already
 * solved it continuous and perpendicular to the rig's reach; interpolation and blending can tilt it, so it is re-taken
 * perpendicular to this frame's reach (the default pole if nothing is left). Stateless: the same row gives the same arm. */
function pole(r:CharacterRig,left:boolean,target:T.Vector3,f:Float32Array,i:number,out:T.Vector3){
 _d.set(target.x-(left?.345:-.345),target.y-.18,target.z).normalize();out.set(f[i],f[i+1],f[i+2]);out.addScaledVector(_d,-out.dot(_d));
 if(out.lengthSq()<.01)out.copy(left?POLE_L:POLE_R);
 return out.normalize().applyQuaternion(_cq.copy((left?r.leftShoulder:r.rightShoulder).quaternion).invert());
}
export function applyMocapPose(r:CharacterRig,f:Float32Array,o:MocapPoseOptions){
 const w=o.weight;if(w<=.001)return;markMocapPosed(r);const root=(o.root??1)*w,legs=(o.legs??1)*w;
 // Hips height rides the motion root (relative to the standing gait height); travel only as admitted.
 // The heading turns the pelvis (with the legs under it), so layered clips blend it like any joint rotation.
 if(root>.001){r.body.position.y+=(f[CH.by]-.025-r.body.position.y)*root;set(r.pelvis,_yq.setFromAxisAngle(Y,f[CH.yaw]).multiply(rot(f,CH['pelvis.x'],_q)),root);}
 if(o.travel){r.body.position.x+=f[CH.bx]*o.travel*w;r.body.position.z+=f[CH.bz]*o.travel*w;}
 set(r.spine,rot(f,CH['spine.x'],_q),w);set(r.chest,rot(f,CH['chest.x'],_q),w);
 set(r.neck,rot(f,CH['neck.x'],_q),w);set(r.head,rot(f,CH['head.x'],_q),w);
 set(r.leftShoulder,rot(f,CH['lclav.x'],_q),w);set(r.rightShoulder,rot(f,CH['rclav.x'],_q),w);
 if(legs>.001){
  set(r.leftLeg,rot(f,CH['lhip.x'],_q),legs);set(r.rightLeg,rot(f,CH['rhip.x'],_q),legs);
  set(r.leftKnee,_q.setFromAxisAngle(X,Math.max(0,f[CH.lknee])),legs);set(r.rightKnee,_q.setFromAxisAngle(X,Math.max(0,f[CH.rknee])),legs);
  set(r.leftFoot,rot(f,CH['lankle.x'],_q),legs);set(r.rightFoot,rot(f,CH['rankle.x'],_q),legs);
 }
 if(o.arms===false)return;
 _p.set(f[CH['grip.x']],f[CH['grip.y']],f[CH['grip.z']]);bladeAxes(f,CH['blade.x'],CH['edge.x']);
 // The voxel shoulders, chest and head are bigger than the captured human's: keep the blade (and hilt) clear of them.
 if(o.clear!=='none')clearBlade(r,_p,_a,_b,-(o.pommel??.27)*r.weapon.scale.z,bladeEnd(r.weapon),o.clear!=='turn',o.halfWidth,!!o.floor);basisEuler(_e);
 braceWeapon(r,_p,_e,w,pole(r,false,_p,f,CH['rpole.x'],_v),!o.oneHand);
 // The support hand leaves the grip only where the capture lets go of it.
 const free=(1-Math.min(1,Math.max(0,f[CH.twohand])))*w;
 if(free>.001){_p.set(f[CH['lhand.x']],f[CH['lhand.y']],f[CH['lhand.z']]);
  basisEulerYZ(f,CH['lfwd.x'],CH['lup.x'],_e);hand(r,true,_p,_e,free,pole(r,true,_p,f,CH['lpole.x'],_lp));}
}
/** Final floor guard (after every layer and transition blend): a blade tip below the floor plane turns its wrist up just
 * enough to bring the tip onto the plane, about the horizontal axis across the blade. Each pose clears the floor on its
 * own, but a blend between two of them (a reverse-grip guard into a cut, say) can swing the blade through straight down.
 * A blade within ~30° of vertical has no axis across it of its own (it would swap sides between frames), so it keeps the
 * one its weapon turned about before (eased over between 15° and 30°): the only per-weapon state in the mocap layers, a
 * presentation detail that never changes where a blade is above the floor. */
const _gt=new T.Vector3(),_gg=new T.Vector3(),_gd=new T.Vector3(),_ga=new T.Vector3(),_gn=new T.Vector3(),_gc=new T.Vector3(),_gq=new T.Quaternion(),_gp=new T.Quaternion(),_gr=new T.Quaternion(),UP=new T.Vector3(0,1,0);
const liftAxes=new WeakMap<T.Object3D,T.Vector3>(),VERT_IN=Math.sin(15*Math.PI/180),VERT_OUT=Math.sin(30*Math.PI/180);
export function keepBladesOffFloor(r:CharacterRig){
 r.group.updateMatrixWorld(true);const floor=r.group.getWorldPosition(_gt).y+FLOOR_CLEAR;
 for(const [weapon,wrist] of [[r.weapon,r.rightHand],[r.offWeapon,r.leftHand]] as const){
  const tip=weapon.visible?weapon.getObjectByName('blade-tip'):undefined;if(!tip)continue;
  tip.getWorldPosition(_gt);wrist.getWorldPosition(_gg);_gd.subVectors(_gt,_gg);const len=_gd.length();if(len<1e-4)continue;_gd.divideScalar(len);
  // The axis: its own across a slanted blade, the one it kept across a near-vertical one (taken perpendicular to it).
  let kept=liftAxes.get(weapon);if(!kept){kept=new T.Vector3(1,0,0);liftAxes.set(weapon,kept);}
  _gn.crossVectors(_gd,UP);const own=_gn.length(),u=T.MathUtils.smoothstep(own,VERT_IN,VERT_OUT);
  _ga.copy(kept).addScaledVector(_gd,-kept.dot(_gd));if(_ga.lengthSq()<1e-8)_ga.copy(_gn);_ga.normalize();
  // Leaning out again, the kept axis turns about the blade onto the blade's own (by the share u), never flipping over to it.
  if(u>0){_gn.divideScalar(Math.max(own,1e-9));const turn=Math.atan2(_gc.crossVectors(_ga,_gn).dot(_gd),_ga.dot(_gn));_ga.applyAxisAngle(_gd,turn*u).normalize();}
  kept.copy(_ga);
  if(_gt.y>=floor||_gg.y<=floor)continue;
  // The turn about that axis (positive, the way the axis was kept) that brings the tip onto the plane: the tip's height runs
  // len·R·cos(θ − φ) as it turns, φ taken in (0, 2π] so a near-vertical blade (φ ≈ π either side) turns continuously.
  const dy=_gd.y,uy=_gt.crossVectors(_ga,_gd).y,R=Math.hypot(dy,uy),want=(floor-_gg.y)/len;let phi=Math.atan2(uy,dy);if(phi<=0)phi+=2*Math.PI;
  const lift=Math.min(Math.PI,Math.max(0,phi-Math.acos(Math.max(-1,Math.min(1,want/Math.max(R,1e-6))))));if(lift<=0)continue;
  wrist.getWorldQuaternion(_gq);wrist.parent!.getWorldQuaternion(_gp);_gr.setFromAxisAngle(_ga,lift);
  wrist.quaternion.copy(_gp.invert().multiply(_gr.multiply(_gq)));wrist.updateMatrixWorld(true);
 }
}
/** Linear blend of two sampled rows (neighbouring poses: rotation vectors and targets blend componentwise). */
export function blendRows(a:Float32Array,b:Float32Array,k:number,out:Float32Array){for(let i=0;i<out.length;i++)out[i]=a[i]+(b[i]-a[i])*k;return out;}
/** Recover between the already fitted contact and guard poses. Clearing each intermediate raw blade direction can
 * choose a different way around the voxel body from one frame to the next. Fitting the endpoints first keeps the
 * regrip on one quaternion arc; the final floor pass still protects the blade tips. Feet are posed separately. */
const RECOVERY_BONES=['pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','weapon','offWeapon'] as const;
type RecoveryPose={q:T.Quaternion[];body:T.Vector3;wrists:T.Quaternion[];weapon:T.Vector3;offWeapon:T.Vector3};
const recoveryPoses=new WeakMap<CharacterRig,{key:string;from:RecoveryPose;to:RecoveryPose}>();
const recoveryQuat=new T.Quaternion(),recoveryParent=new T.Quaternion();
function chestQuat(n:T.Object3D,chest:T.Object3D,q:T.Quaternion){q.identity();for(let o:T.Object3D|null=n;o&&o!==chest;o=o.parent)q.premultiply(o.quaternion);return q;}
function recoveryPose(r:CharacterRig):RecoveryPose{return {q:RECOVERY_BONES.map(b=>r[b].quaternion.clone()),body:r.body.position.clone(),weapon:r.weapon.position.clone(),offWeapon:r.offWeapon.position.clone(),wrists:[r.rightHand,r.leftHand].map(h=>chestQuat(h,r.chest,new T.Quaternion()))};}
export function applyMocapRecovery(r:CharacterRig,contact:Float32Array,guard:Float32Array,k:number,o:MocapPoseOptions,key:string){
 let cache=recoveryPoses.get(r);
 if(cache?.key!==key){
  applyMocapPose(r,contact,{...o,weight:1,legs:0,clear:'turn'});keepBladesOffFloor(r);const from=recoveryPose(r);
  applyMocapPose(r,guard,{...o,weight:1,legs:0,clear:'full'});keepBladesOffFloor(r);cache={key,from,to:recoveryPose(r)};recoveryPoses.set(r,cache);
 }
 markMocapPosed(r);r.weapon.position.copy(cache.from.weapon).lerp(cache.to.weapon,k);r.offWeapon.position.copy(cache.from.offWeapon).lerp(cache.to.offWeapon,k);
 RECOVERY_BONES.forEach((b,i)=>r[b].quaternion.copy(cache!.from.q[i]).slerp(cache!.to.q[i],k));r.body.position.y=cache.from.body.y+(cache.to.body.y-cache.from.body.y)*k;
 // Wrist orientation is interpolated in chest space, then converted through the blended arm chain. Interpolating the
 // wrist locally as well as its parents adds the arm's turn twice and can swing reverse-grip blades through the floor.
 [r.rightHand,r.leftHand].forEach((h,i)=>{recoveryQuat.copy(cache!.from.wrists[i]).slerp(cache!.to.wrists[i],k);h.quaternion.copy(chestQuat(h.parent!,r.chest,recoveryParent).invert().multiply(recoveryQuat));
 });
 if(!o.oneHand){const grip=r.weapon.getObjectByName('support-grip');if(grip)supportHand(r,grip,1);}
}
/** The row's roll channels (the main blade's edge, the free wrist's up), which set the wrists' roll about the blades. */
const DIRS=[CH['edge.x'],CH['lup.x']],_da=new T.Vector3(),_db=new T.Vector3(),_dq=new T.Quaternion();
/** blendRows for two poses far apart (a cut's start and contact): the roll channels turn along the great circle between
 * them instead of blending componentwise, which passes near zero (and flips the wrist) when they point far apart; the
 * blades, poles and targets blend as blendRows does (the path the anticipation blend was tuned on). */
export function blendRowsTurning(a:Float32Array,b:Float32Array,k:number,out:Float32Array){
 for(const i of DIRS){_da.set(a[i],a[i+1],a[i+2]).normalize();_db.set(b[i],b[i+1],b[i+2]).normalize();
  _dq.setFromUnitVectors(_da,_db);_da.applyQuaternion(_q.identity().slerp(_dq,k));out[i]=_da.x;out[i+1]=_da.y;out[i+2]=_da.z;}
 for(let i=0;i<out.length;i++)if(!DIRS.some(d=>i>=d&&i<d+3))out[i]=a[i]+(b[i]-a[i])*k;
 return out;
}

/** Recovery-only wrist settling, measured relative to the chest so whole-body spins stay intact. Every active contact
 * frame is sampled exactly. Floor correction follows the limiter; the remembered pose is what was actually displayed. */
const wristHistory=new WeakMap<CharacterRig,{key:string;time:number;q:T.Quaternion[]}>();
const settleQ=new T.Quaternion(),settleTarget=new T.Quaternion(),settleParent=new T.Quaternion();
export function finishMocapRecovery(r:CharacterRig,time:number,recovering:boolean,rate:number,key:string){
 keepBladesOffFloor(r);
 let previous=wristHistory.get(r);const dt=previous?time-previous.time:0;let limited=false;
 if(recovering&&previous?.key===key&&dt>0&&dt<=.1){
  [r.rightHand,r.leftHand].forEach((h,i)=>{chestQuat(h,r.chest,settleQ);const angle=previous!.q[i].angleTo(settleQ),limit=18*rate*dt;
   if(angle>limit){limited=true;settleTarget.copy(settleQ);settleQ.copy(previous!.q[i]).slerp(settleTarget,limit/angle);h.quaternion.copy(chestQuat(h.parent!,r.chest,settleParent).invert().multiply(settleQ));}
  });
 }
 if(limited)keepBladesOffFloor(r);
 if(!previous||previous.key!==key){previous={key,time,q:[new T.Quaternion(),new T.Quaternion()]};wristHistory.set(r,previous);}
 previous.time=time;[r.rightHand,r.leftHand].forEach((h,i)=>chestQuat(h,r.chest,previous!.q[i]));
}
