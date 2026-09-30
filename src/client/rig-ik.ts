import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {markLegsAuthored} from './pose-flags.js';
/** Analytic limb IK shared by locomotion actions, melee, blocks, reactions, intros and lobby poses (character-rig.ts and
 * primary-melee-motion.ts re-export these names). foot() also marks the rig's legs as authored for this sample.
 *
 * Limbs are solved in their parent's local space from local transforms (no full-rig world matrix updates for hands and
 * feet). Arms are built from an orthonormal basis (the bone direction plus the bend direction projected perpendicular to
 * it) with the elbow a pure hinge, so no hand target can twist the upper arm through a shortest-arc singularity (an arm
 * pointing straight up) and flip the elbow in one frame; reach is clamped so the elbow never folds flat into the shoulder.
 * Legs keep a little bend (reach clamp) and lift their knee pole as they reach forward, so a lunging leg never flips its
 * knee plane. The solve is stateless (the same targets always give the same limb, however the rig got there).
 * The weapon's grip transform blends by the same weight as the arm instead of snapping at any weight. */
// Rig contract lengths (RIG_LENGTHS in character-rig.ts; literal here because character-rig re-exports this module).
const UPPER=.27,FORE=.25,THIGH=.25,ARM_MIN=.20,ARM_MAX=.515,SUPPORT_MIN=.10,LEG_MIN=.1,LEG_MAX=.485;
/** The grip offset that puts the weapon origin on the wrist joint (cancels the hand socket's (0,-.065,.055)). */
const GRIP=new T.Vector3(0,.065,-.055),IDENTITY=new T.Quaternion(),X_AXIS=new T.Vector3(1,0,0),DOWN=new T.Vector3(0,-1,0);
const _s=new T.Vector3(),_e=new T.Vector3(),_d=new T.Vector3(),_p=new T.Vector3(),_u=new T.Vector3(),_w=new T.Vector3(),_x=new T.Vector3(),_y=new T.Vector3(),_k=new T.Vector3(),_f=new T.Vector3(),_m=new T.Matrix4();
const _q=new T.Quaternion(),_q2=new T.Quaternion(),_q3=new T.Quaternion(),_cq=new T.Quaternion(),_upper=new T.Quaternion(),_lower=new T.Quaternion(),_eu=new T.Euler();
/** Bend direction perpendicular to the unit limb direction `dir` from the unit pole `p` (overwritten and returned). Stateless:
 * the same targets always give the same limb, however the rig got there (poses stay pure functions of time). */
function bendDirection(dir:T.Vector3,p:T.Vector3){
 p.addScaledVector(dir,-p.dot(dir));
 if(p.lengthSq()<1e-10){p.set(0,0,1).addScaledVector(dir,-dir.z);if(p.lengthSq()<1e-8)p.set(1,0,0).addScaledVector(dir,-dir.x);}
 return p.normalize();
}
/** Rotation from `node`'s frame to `root`'s frame (local quaternions only; rig joints are unscaled). */
function chainQuat(node:T.Object3D,root:T.Object3D,out:T.Quaternion){out.identity();for(let n:T.Object3D|null=node;n&&n!==root;n=n.parent)out.premultiply(n.quaternion);return out;}
/** `node`'s origin in `root`'s frame (local transforms only). */
function chainPoint(node:T.Object3D,root:T.Object3D,out:T.Vector3){out.set(0,0,0);for(let n:T.Object3D|null=node;n&&n!==root;n=n.parent)out.applyQuaternion(n.quaternion).add(n.position);return out;}
/** Two-bone arm in its clavicle's space: `end` is the hand target there, (px,py,pz) the elbow pole. Blends the upper arm and
 * elbow toward the solution by `weight`. */
function solveArm(r:CharacterRig,left:boolean,end:T.Vector3,px:number,py:number,pz:number,weight:number,min:number){
 const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow;
 _s.copy(arm.position);_d.subVectors(end,_s);const raw=_d.length();if(raw<1e-6)_d.set(0,-1,0);else _d.divideScalar(raw);const d=T.MathUtils.clamp(raw,min,ARM_MAX);
 const along=(UPPER*UPPER-FORE*FORE+d*d)/(2*d),height=Math.sqrt(Math.max(0,UPPER*UPPER-along*along)),p=bendDirection(_d,_p.set(px,py,pz).normalize());
 const cos=along/UPPER,sin=height/UPPER;
 // Upper bone u (shoulder to elbow), the fold direction w (perpendicular to u, toward the hand) and the hinge x = y cross w.
 _u.copy(_d).multiplyScalar(cos).addScaledVector(p,sin);_w.copy(_d).multiplyScalar(sin).addScaledVector(p,-cos);
 _y.copy(_u).negate();_x.crossVectors(_y,_w);_upper.setFromRotationMatrix(_m.makeBasis(_x,_y,_w));
 // Forearm from the elbow to the reached point; the elbow folds it toward +z of the upper arm (a negative x rotation).
 _f.copy(_d).multiplyScalar(d).addScaledVector(_u,-UPPER);_lower.setFromAxisAngle(X_AXIS,-Math.atan2(_f.dot(_w),_f.dot(_u)));
 if(weight>=1){arm.quaternion.copy(_upper);elbow.quaternion.copy(_lower);}else{arm.quaternion.slerp(_upper,weight);elbow.quaternion.slerp(_lower,weight);}
}
/** Blend the weapon's grip transform (origin on the wrist, no extra rotation) by the arm's weight. */
function gripWeapon(weapon:T.Object3D,weight:number){if(weight>=1){weapon.position.copy(GRIP);weapon.quaternion.identity();}else if(weight>0){weapon.position.lerp(GRIP,weight);weapon.quaternion.slerp(IDENTITY,weight);}}
/** Wrist to a chest-space orientation, relative to the (possibly partly blended) arm chain. */
function orientWrist(r:CharacterRig,left:boolean,orientation:T.Quaternion,weight:number){
 const wrist=left?r.leftHand:r.rightHand;chainQuat(wrist.parent!,r.chest,_q3).invert().multiply(orientation);if(weight>=1)wrist.quaternion.copy(_q3);else wrist.quaternion.slerp(_q3,weight);
}
/** Chest-space point into a clavicle's space. */
function chestToClavicle(r:CharacterRig,left:boolean,target:T.Vector3,out:T.Vector3){const clavicle=(left?r.leftArm:r.rightArm).parent!;return out.copy(target).sub(clavicle.position).applyQuaternion(_q.copy(clavicle.quaternion).invert());}

/** Analytic two-link arm IK. Targets come from weapon sockets, never per-item frame offsets. */
export function supportHand(r:CharacterRig,target:T.Object3D,weight:number){
 if(weight<=.001)return;const clavicle=r.leftArm.parent!;
 target.getWorldPosition(_e);clavicle.updateWorldMatrix(true,false);clavicle.worldToLocal(_e);
 solveArm(r,true,_e,1,-.8,-.25,weight,SUPPORT_MIN);
 r.leftHand.parent!.getWorldQuaternion(_q2).invert();r.leftHand.quaternion.slerp(target.getWorldQuaternion(_cq).premultiply(_q2),weight);
}

/** Drive the main grip in chest space, solve both arms, then preserve the desired weapon orientation. Below weight .001
 * the incoming pose (and weapon transform) is untouched; above it the weapon grip blends by the same weight. */
export function braceWeapon(r:CharacterRig,target:T.Vector3,rotation:T.Euler,weight:number,pole?:T.Vector3){
 if(weight<.001)return;
 solveArm(r,false,chestToClavicle(r,false,target,_e),pole?pole.x:-1,pole?pole.y:-.7,pole?pole.z:-.2,weight,ARM_MIN);orientWrist(r,false,_cq.setFromEuler(rotation),weight);
 gripWeapon(r.weapon,weight);const grip=r.weapon.getObjectByName('support-grip');if(grip)supportHand(r,grip,weight);
}
/** Analytic limb placement keeps the planted foot on the floor while the pelvis turns and drops. Below full weight the
 * ankle blends from `from` (rig-group space; default: where the incoming leg puts it under the current body, which moves
 * with any body offset a layer already applied) toward the target. The knee bends toward the
 * rig's forward with a lift that keeps a forward-reaching leg's bend plane well defined; the thigh is built from a basis and
 * the knee is a pure hinge. */
export function foot(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,heel:number,weight:number,from?:T.Vector3){
 markLegsAuthored(r);
 const hip=left?r.leftLeg:r.rightLeg,knee=left?r.leftKnee:r.rightKnee,ankle=left?r.leftFoot:r.rightFoot,toe=left?r.leftToe:r.rightToe;
 // Pelvis frame in rig-group space (_q: pelvis to group, _k: pelvis origin).
 chainQuat(r.pelvis,r.group,_q);chainPoint(r.pelvis,r.group,_k);_q2.copy(_q).invert();
 const placed=(from?_e.copy(from):chainPoint(ankle,r.group,_e)).lerp(target,weight);placed.y=Math.max(-.795,placed.y);
 placed.sub(_k).applyQuaternion(_q2);_s.copy(hip.position);_d.subVectors(placed,_s);const raw=_d.length();if(raw<1e-6)_d.set(0,-1,0);else _d.divideScalar(raw);const d=T.MathUtils.clamp(raw,LEG_MIN,LEG_MAX);
 // Knee pole: the rig's forward, lifted as the leg reaches forward (a lunge), so the knee bends up and forward instead of
 // flipping its bend plane about a pole the leg nearly points along. A leg under the hips bends exactly as before.
 const reach=_w.copy(_d).applyQuaternion(_q).z;_p.set(0,.6*T.MathUtils.smoothstep(reach,.2,.65),1).applyQuaternion(_q2);
 const bend=bendDirection(_d,_p),half=d/2,out=Math.sqrt(Math.max(0,THIGH*THIGH-half*half));
 // The thigh takes the shortest arc from hanging (legs never point up, so it is never singular) and the knee the shortest
 // arc to the shin; the ground pass re-solves layer-placed legs as clean hinges keeping the same knee.
 _u.copy(_d).multiplyScalar(half).addScaledVector(bend,out).normalize();hip.quaternion.setFromUnitVectors(DOWN,_u);
 _f.copy(_d).multiplyScalar(d).addScaledVector(_u,-THIGH).normalize().applyQuaternion(_q3.copy(hip.quaternion).invert());knee.quaternion.setFromUnitVectors(DOWN,_f);
 // Foot to the desired rig-group heading/heel (Euler XYZ), relative to the solved leg.
 ankle.quaternion.slerp(_q3.copy(_q).multiply(hip.quaternion).multiply(knee.quaternion).invert().multiply(_cq.setFromEuler(_eu.set(heel,yaw,0))),weight);toe.rotation.x=-heel*.45*weight;
}
/** Independent hand targets let the blades describe arcs while the shoulders and elbows fold. `pole` (optional, the
 * clavicle's frame) is the direction the elbow bends toward; a phrase that sweeps the hand across the default pole's
 * singular directions passes its own, keyed with the hand path. */
export function hand(r:CharacterRig,left:boolean,target:T.Vector3,orientation:T.Euler,weight:number,pole?:T.Vector3){
 solveArm(r,left,chestToClavicle(r,left,target,_e),pole?pole.x:left?1:-1,pole?pole.y:-.65,pole?pole.z:-.3,weight,ARM_MIN);orientWrist(r,left,_cq.setFromEuler(orientation),weight);gripWeapon(left?r.offWeapon:r.weapon,weight);
}
