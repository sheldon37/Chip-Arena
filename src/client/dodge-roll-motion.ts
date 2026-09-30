import * as T from 'three';
import {clamp} from '../shared/config.js';
import {rolling,rollVelocity} from '../shared/souls-combat.js';
import type {PlayerView} from '../shared/simulation.js';
import type {CharacterRig} from './character-rig.js';
import {smoothMotion as ease,plantLeg,ankleInGroup} from './motion-quality.js';

type Bone='pelvis'|'spine'|'chest'|'neck'|'head'|'leftShoulder'|'rightShoulder'|'leftArm'|'rightArm'|'leftElbow'|'rightElbow'|'leftHand'|'rightHand'|'leftLeg'|'rightLeg'|'leftKnee'|'rightKnee'|'leftFoot'|'rightFoot'|'leftToe'|'rightToe';
/** Fully tucked roll pose (local Euler x,y,z per bone); the roll blends the live locomotion pose toward it by `tuck`. */
const TUCK:[Bone,number,number,number][]=[['pelvis',-.18,0,0],['spine',.28,0,0],['chest',.12,0,0],['neck',.15,0,0],['head',.35,0,0],['leftShoulder',0,0,.08],['rightShoulder',0,0,-.08],['leftArm',-1.15,0,.30],['rightArm',-1.05,0,-.24],['leftElbow',-1.55,0,0],['rightElbow',-1.5,0,0],['leftHand',.2,0,0],['rightHand',.2,0,0],['leftLeg',-1.55,0,.12],['rightLeg',-1.35,0,-.12],['leftKnee',2.05,0,0],['rightKnee',1.90,0,0],['leftFoot',-.5,0,0],['rightFoot',-.55,0,0],['leftToe',0,0,0],['rightToe',0,0,0]];
const _a=new T.Vector3(),_t=new T.Vector3(),_q=new T.Quaternion(),_e=new T.Euler(),_live=new T.Quaternion(),_live2=new T.Vector3(),_v=new T.Vector3(),_w=new T.Quaternion(),_d=new T.Quaternion(),_p=new T.Quaternion(),X=new T.Vector3(1,0,0),Y=new T.Vector3(0,1,0),Z=new T.Vector3(0,0,1);
const wrap=(a:number)=>a-Math.PI*2*Math.round(a/(Math.PI*2));
/** Exit plants: [lead, group x, group z, foot yaw, phase start] (lead foot first, rear foot .04 later). */
const EXIT=[[true,.13,.13,.12,.72],[false,-.14,-.03,-.25,.76]] as const,_g=new T.Quaternion(),_h=new T.Quaternion(),_k=new T.Quaternion(),_ee=new T.Euler();

/** Anticipation, tucked shoulder roll, foot plant, then recovery. No camera rotation.
 * Every bone is blended from the live locomotion pose toward the tucked target by `tuck`, so entry and exit never pass
 * through the rig's zero pose; the heading turns in over the first 20% and pivots back over u .7-.92 above planted
 * feet (settled by .92 so coarse frame rates see a finished exit), and the roll exits into a staggered crouch that the next locomotion frames rise out of. Phase = roll.at..until. */
export function dodgeRollMotion(r:CharacterRig,p:PlayerView,time:number){
 if(!rolling(p,time))return false;
 const roll=p.roll!,u=clamp((time-roll.at)/(roll.until-roll.at),0,1),enter=ease(u/.18),leave=1-ease((u-.66)/.26),tuck=enter*leave;
 const rotation=ease((u-.12)/.66)*Math.PI*2,turn=wrap(Math.atan2(roll.direction.x,roll.direction.z)-p.yaw),heading=turn*ease(u/.2)*(1-ease((u-.7)/.22));
 const hold=ease(u/.12)*(1-ease((u-.74)/.18)),crouch=ease((u-.7)/.2);
 // Body: the live orientation eases onto the roll heading, the somersault and lean ride on top of it.
 _live.copy(r.body.quaternion);_live2.copy(r.body.position);
 r.body.quaternion.copy(_live).slerp(_d.setFromAxisAngle(Y,heading),hold).multiply(_w.setFromAxisAngle(X,rotation)).multiply(_p.setFromAxisAngle(Z,-.12*Math.sin(Math.PI*u)));
 r.body.position.set(0,-.25*tuck+Math.sin(Math.PI*u)*.09+.23*tuck*Math.sin(rotation/2)**2+.20*tuck*(1-tuck),0);
 r.body.position.lerpVectors(_live2,r.body.position,hold);r.body.position.y-=.08*crouch;
 for(const [bone,x,y,z] of TUCK)r[bone].quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),tuck);
 // Exit: lead foot, then rear foot, reach the floor ahead of the crouch spot by the roll travel still to come, so they land
 // still in the world as the authoritative roll decelerates; the ankle moves positionally from the tuck, never under the floor.
 if(u>.72){
  const left=roll.until-time,w=left>1e-4?rollVelocity(roll,time,left):{x:0,y:0,z:0},yaw=r.group.rotation.y,c=Math.cos(yaw),sn=Math.sin(yaw),gx=(w.x*c-w.z*sn)*left,gz=(w.x*sn+w.z*c)*left;
  for(const [lead,x,z,footYaw,start] of EXIT){
   const k=ease((u-start)/.14);if(k<=1e-4)continue;ankleInGroup(r,lead,_a);_t.set(x+gx,0,z+gz);const reach=Math.hypot(_t.x,_t.z);if(reach>.34)_t.multiplyScalar(.34/reach);_t.y=-.795+.05*(1-k);
   _a.lerp(_t,k);_a.y=Math.max(_a.y,-.795+(k<1?.012:0));plantLeg(r,lead,_a,footYaw,1,k);
  }
 }
 // Weapons remain attached to their hand sockets while the torso tumbles.
 r.weapon.rotation.set(.35,0,0);r.offWeapon.rotation.set(.35,0,0);
 r.group.updateMatrixWorld(true);
 for(const [hand,side] of [[r.rightHand,-1],[r.leftHand,1]] as const){
  const desired=r.group.getWorldQuaternion(_g).multiply(_h.setFromEuler(_ee.set(0,heading+side*Math.PI/2,0))).multiply(_h.setFromEuler(_ee.set(-.35,0,0)));
  hand.quaternion.slerp(hand.parent!.getWorldQuaternion(_k).invert().multiply(desired),tuck);
 }
 return true;
}
