import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import {smoothMotion} from './motion-quality.js';
import {blendRotation} from './pose-math.js';
/** Magic staff carry and the coil -> release -> settle casting phrase (moved verbatim from sampleCharacter). */
export function magicLayer(r:CharacterRig,p:PlayerView,pose:ReturnType<typeof attackPose>,time:number,dt:number,pitch:number){
 // Staff stays in the support hand. The free hand coils above the shoulder,
 // then drives through the target; pelvis and chest lead the follow-through.
 if(r.weapon.parent!==r.sockets.leftHand)r.sockets.leftHand.add(r.weapon);
 const m=pose.magic,c=m.weight,water=p.spec==='water',dark=p.spec==='darkness',lightning=p.spec==='lightning',u=m.drive,load=m.coil;
 const carry=1-smoothMotion((r.motion-.10)/.65);blendRotation(r.leftArm,-.24-pitch*.08,-.08,.16,carry);blendRotation(r.leftElbow,-.42,0,0,carry);r.leftHand.rotation.set(0,0,0);
 r.weapon.rotation.set(T.MathUtils.lerp(.15,-Math.PI/2-r.leftArm.rotation.x-r.leftElbow.rotation.x,m.active?1:carry),0,0);
 if(m.active){
  const twist=(m.age<m.windup?-.48*load:(-.48+.88*u)*m.settle)*(dark?1.15:water?1.1:1);
  r.pelvis.rotation.y+=twist*.35;r.spine.rotation.y+=twist*.65;r.chest.rotation.y+=twist*.48;r.head.rotation.y=-twist*.55;
  r.body.rotation.x+=(-.08*(1-u)+.17*u)*c;r.body.position.z=(-.065*(1-u)+.13*u)*c;
  r.rightShoulder.rotation.y=-.12*c;r.leftLeg.rotation.x-=.22*c;r.rightKnee.rotation.x+=.34*c;r.leftKnee.rotation.x+=.15*c;
  const arm=m.age<m.windup?-.3-(dark?1.65:1.35)*load:-(dark?1.95:1.65)+(lightning?.25:.18)*u-pitch*.65*u;
  const elbow=m.age<m.windup?-.3-1.65*load:-1.95+1.87*u;
  blendRotation(r.rightArm,arm,(water?-.5:.28)*(1-u),-(water?.65:.42)*(1-u)-.04*u,c);
  blendRotation(r.rightElbow,elbow,0,0,c);r.rightHand.rotation.set(-.18*u*c,(water?-.7:.45)*(1-u)*c,.45*(1-u)*c);
 }
}
