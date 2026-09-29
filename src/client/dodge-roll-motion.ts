import * as T from 'three';
import {clamp} from '../shared/config.js';
import {rolling} from '../shared/souls-combat.js';
import type {PlayerView} from '../shared/simulation.js';
import type {CharacterRig} from './character-rig.js';
import {smoothMotion as ease} from './motion-quality.js';

/** Anticipation, tucked shoulder roll, foot plant, then recovery. No camera rotation. */
export function dodgeRollMotion(r:CharacterRig,p:PlayerView,time:number){
 if(!rolling(p,time))return false;
 const roll=p.roll!,u=clamp((time-roll.at)/(roll.until-roll.at),0,1),enter=ease(u/.18),leave=1-ease((u-.70)/.30),tuck=enter*leave;
 const rotation=ease((u-.12)/.66)*Math.PI*2,turn=Math.atan2(roll.direction.x,roll.direction.z)-p.yaw;
 r.body.rotation.set(0,turn,0);r.body.rotateX(rotation);r.body.rotateZ(-.12*Math.sin(Math.PI*u));
 r.body.position.set(0,-.25*tuck+Math.sin(Math.PI*u)*.09+.23*tuck*Math.sin(rotation/2)**2+.20*tuck*(1-tuck),0);
 r.pelvis.rotation.set(-.18*tuck,0,0);r.spine.rotation.set(.28*tuck,0,0);r.chest.rotation.set(.12*tuck,0,0);r.neck.rotation.set(.15*tuck,0,0);r.head.rotation.set(.35*tuck,0,0);
 r.leftShoulder.rotation.set(0,0,.08);r.rightShoulder.rotation.set(0,0,-.08);
 r.leftArm.rotation.set(-1.15*tuck,0,.30*tuck);r.rightArm.rotation.set(-1.05*tuck,0,-.24*tuck);
 r.leftElbow.rotation.set(-1.55*tuck,0,0);r.rightElbow.rotation.set(-1.5*tuck,0,0);
 r.leftHand.rotation.set(.2*tuck,0,0);r.rightHand.rotation.set(.2*tuck,0,0);
 r.leftLeg.rotation.set(-1.55*tuck,0,.12*tuck);r.rightLeg.rotation.set(-1.35*tuck,0,-.12*tuck);
 r.leftKnee.rotation.set(2.05*tuck,0,0);r.rightKnee.rotation.set(1.90*tuck,0,0);
 r.leftFoot.rotation.set(-.5*tuck,0,0);r.rightFoot.rotation.set(-.55*tuck,0,0);r.leftToe.rotation.set(0,0,0);r.rightToe.rotation.set(0,0,0);
 // Weapons remain attached to their hand sockets while the torso tumbles.
 r.weapon.rotation.set(.35,0,0);r.offWeapon.rotation.set(.35,0,0);
 r.group.updateMatrixWorld(true);
 for(const [hand,side] of [[r.rightHand,-1],[r.leftHand,1]] as const){
  const desired=r.group.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(new T.Euler(0,turn+side*Math.PI/2,0))).multiply(new T.Quaternion().setFromEuler(new T.Euler(-.35,0,0)));
  hand.quaternion.slerp(hand.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(desired),tuck);
 }
 return true;
}
