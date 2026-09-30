import * as T from 'three';
import {RIG_LENGTHS,type CharacterRig} from './character-rig.js';
import {markLegsAuthored} from './pose-flags.js';
/** Analytic limb IK shared by locomotion actions, melee, blocks, reactions, intros and lobby poses.
 * Moved verbatim from character-rig.ts (supportHand, braceWeapon) and primary-melee-motion.ts (foot, hand);
 * both modules re-export these names. foot() also marks the rig's legs as authored for this sample. */
const down=new T.Vector3(0,-1,0);
/** Analytic two-link arm IK. Targets come from weapon sockets, never per-item frame offsets. */
export function supportHand(r:CharacterRig,target:T.Object3D,weight:number){
 if(weight<=.001)return;r.group.updateMatrixWorld(true);
 const parent=r.leftArm.parent!,start=r.leftArm.position.clone(),end=parent.worldToLocal(target.getWorldPosition(new T.Vector3()));
 const delta=end.clone().sub(start),d=T.MathUtils.clamp(delta.length(),.045,RIG_LENGTHS.upperArm+RIG_LENGTHS.forearm-.002),dir=delta.normalize();
 end.copy(start).addScaledVector(dir,d);
 const a=RIG_LENGTHS.upperArm,b=RIG_LENGTHS.forearm,along=(a*a-b*b+d*d)/(2*d),height=Math.sqrt(Math.max(0,a*a-along*along));
 const pole=new T.Vector3(1,-.8,-.25);pole.addScaledVector(dir,-pole.dot(dir)).normalize();const elbow=start.clone().addScaledVector(dir,along).addScaledVector(pole,height);
 const upper=new T.Quaternion().setFromUnitVectors(down,elbow.clone().sub(start).normalize());const lower=new T.Quaternion().setFromUnitVectors(down,end.clone().sub(elbow).normalize().applyQuaternion(upper.clone().invert()));
 r.leftArm.quaternion.slerp(upper,weight);r.leftElbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);
 const wrist=target.getWorldQuaternion(new T.Quaternion()).premultiply(r.leftHand.parent!.getWorldQuaternion(new T.Quaternion()).invert());r.leftHand.quaternion.slerp(wrist,weight);
}

/** Drive the main grip in chest space, solve both arms, then preserve the desired weapon orientation. */
export function braceWeapon(r:CharacterRig,target:T.Vector3,rotation:T.Euler,weight:number){
 if(weight<.001)return;r.group.updateMatrixWorld(true);
 const parent=r.rightArm.parent!,start=r.rightArm.position.clone(),end=parent.worldToLocal(r.chest.localToWorld(target.clone())),delta=end.clone().sub(start),d=T.MathUtils.clamp(delta.length(),.045,RIG_LENGTHS.upperArm+RIG_LENGTHS.forearm-.002),dir=delta.normalize();end.copy(start).addScaledVector(dir,d);
 const a=RIG_LENGTHS.upperArm,b=RIG_LENGTHS.forearm,along=(a*a-b*b+d*d)/(2*d),height=Math.sqrt(Math.max(0,a*a-along*along)),pole=new T.Vector3(-1,-.7,-.2);pole.addScaledVector(dir,-pole.dot(dir)).normalize();const elbow=start.clone().addScaledVector(dir,along).addScaledVector(pole,height),upper=new T.Quaternion().setFromUnitVectors(down,elbow.clone().sub(start).normalize()),lower=new T.Quaternion().setFromUnitVectors(down,end.clone().sub(elbow).normalize().applyQuaternion(upper.clone().invert()));
 r.rightArm.quaternion.slerp(upper,weight);r.rightElbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);
 const orientation=r.chest.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(rotation)),wrist=r.rightHand.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(orientation);r.rightHand.quaternion.slerp(wrist,weight);
 r.weapon.position.set(0,.065,-.055);r.weapon.rotation.set(0,0,0);r.group.updateMatrixWorld(true);const grip=r.weapon.getObjectByName('support-grip');if(grip)supportHand(r,grip,weight);
}
/** Analytic limb placement keeps the planted foot on the floor while the pelvis turns and drops. */
export function foot(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,heel:number,weight:number){
 markLegsAuthored(r);
 const hip=left?r.leftLeg:r.rightLeg,knee=left?r.leftKnee:r.rightKnee,ankle=left?r.leftFoot:r.rightFoot,toe=left?r.leftToe:r.rightToe;
 r.group.updateMatrixWorld(true);const placed=r.group.worldToLocal(ankle.getWorldPosition(new T.Vector3())).lerp(target,weight);placed.y=Math.max(-.795,placed.y);const parent=hip.parent!,start=hip.position.clone(),end=parent.worldToLocal(r.group.localToWorld(placed)),delta=end.sub(start),d=T.MathUtils.clamp(delta.length(),.045,.497),dir=delta.normalize();
 const pole=new T.Vector3(0,0,1).applyQuaternion(r.group.getWorldQuaternion(new T.Quaternion())).applyQuaternion(parent.getWorldQuaternion(new T.Quaternion()).invert());pole.addScaledVector(dir,-pole.dot(dir)).normalize();
 const bend=dir.clone().multiplyScalar(d/2).addScaledVector(pole,Math.sqrt(Math.max(0,.25*.25-d*d/4))),upper=new T.Quaternion().setFromUnitVectors(down,bend.clone().normalize()),lower=new T.Quaternion().setFromUnitVectors(down,dir.clone().multiplyScalar(d).sub(bend).normalize().applyQuaternion(upper.clone().invert()));
 hip.quaternion.copy(upper);knee.quaternion.copy(lower);r.group.updateMatrixWorld(true);
 const desired=r.group.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(new T.Euler(heel,yaw,0)));ankle.quaternion.slerp(ankle.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(desired),weight);toe.rotation.x=-heel*.45*weight;
}
/** Independent hand targets let the blades describe arcs while the shoulders and elbows fold. */
export function hand(r:CharacterRig,left:boolean,target:T.Vector3,orientation:T.Euler,weight:number){
 const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,wrist=left?r.leftHand:r.rightHand,weapon=left?r.offWeapon:r.weapon;
 r.group.updateMatrixWorld(true);const parent=arm.parent!,start=arm.position.clone(),end=parent.worldToLocal(r.chest.localToWorld(target.clone())),delta=end.sub(start),d=T.MathUtils.clamp(delta.length(),.045,.518),dir=delta.normalize(),along=(.27*.27-.25*.25+d*d)/(2*d),pole=new T.Vector3(left?1:-1,-.65,-.3);pole.addScaledVector(dir,-pole.dot(dir)).normalize();
 const bend=dir.clone().multiplyScalar(along).addScaledVector(pole,Math.sqrt(Math.max(0,.27*.27-along*along))),upper=new T.Quaternion().setFromUnitVectors(down,bend.clone().normalize()),lower=new T.Quaternion().setFromUnitVectors(down,dir.clone().multiplyScalar(d).sub(bend).normalize().applyQuaternion(upper.clone().invert()));
 arm.quaternion.slerp(upper,weight);elbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);const q=r.chest.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(orientation));wrist.quaternion.slerp(wrist.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(q),weight);
 weapon.position.set(0,.065,-.055);weapon.rotation.set(0,0,0);
}
