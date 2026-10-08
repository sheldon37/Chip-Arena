import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {fighterGuard,demonStepPose} from './ansatsuken-animation.js';
import {RAGING_DEMON as D,demonBeat} from '../shared/raging-demon.js';
export const smooth=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
function reset(r:CharacterRig){for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=false;}
/** Two straights, body hook, knee, backfist, high kick, rising blow, driving finisher. */
export function poseDemonAttack(r:CharacterRig,age:number){reset(r);fighterGuard(r,0);if(age<D.combo){demonStepPose(r,0);return;}
 if(age>=D.symbol){r.body.rotation.y=Math.PI;r.leftArm.rotation.set(.15,0,.22);r.rightArm.rotation.set(.15,0,-.22);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.35;r.head.rotation.x=.1;return;}
 const upcoming=D.hits.findIndex(at=>age<at),index=upcoming>=0&&D.hits[upcoming]-age<.095?upcoming:Math.max(0,demonBeat(age).index),phase=age-D.hits[index],u=phase<0?smooth((phase+.095)/.095):1-smooth((phase-.04)/.14),side=index%2?1:-1;
 r.pelvis.rotation.y=.4*side*u;r.chest.rotation.y=.62*side*u;r.body.rotation.y=.28*side*u;r.head.rotation.y=-.25*side*u;r.body.position.z=.14*u;
 if(index===3||index===5){r.rightLeg.rotation.set(-(index===5?2.55:1.65)*u,0,-.14);r.rightKnee.rotation.x=index===5?.15:1.7;r.body.rotation.x=-.22*u;r.leftArm.rotation.x=-.9;r.rightElbow.rotation.x=-1.75;}
 else if(index===6){r.rightArm.rotation.set(-.8-2.2*u,.1,-.12);r.rightElbow.rotation.x=-1.6+1.35*u;r.rightLeg.rotation.x=-.9*u;r.rightKnee.rotation.x=1.4*u;r.body.rotation.x=-.1*u;}
 else if(index===7){r.leftArm.rotation.set(-1.5,0,.05);r.rightArm.rotation.set(-1.5,0,-.05);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.4*(1-u)-.03;r.body.rotation.x=.22*u;r.body.position.z=.25*u;}
 else{const arm=index%2?r.leftArm:r.rightArm,elbow=index%2?r.leftElbow:r.rightElbow;arm.rotation.set(-1.5,index===2?.65*u:index===4?-.7*u:0,.1*side);elbow.rotation.x=-1.45*(1-u)-(index===2?.8:.05);r.body.position.y-=index===2?.12*u:0;}
}
export function poseDemonVictim(r:CharacterRig,age:number){reset(r);const beat=demonBeat(age),hit=beat.index>=0&&beat.age<.20?Math.sin(Math.min(1,beat.age/.20)*Math.PI):0;
 if(age<D.hits[D.hits.length-1]){r.body.rotation.set(-.18-hit*.3,0,hit*(beat.index%2?.2:-.2));r.head.rotation.x=-.25*hit;r.leftArm.rotation.set(-.35,0,.35);r.rightArm.rotation.set(-.4,0,-.35);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.55;r.leftLeg.rotation.x=-.12;r.rightKnee.rotation.x=.25;}
 else{const fall=smooth((age-D.hits[D.hits.length-1])/.4);r.body.rotation.x=-Math.PI/2*fall;r.body.position.y=-.64*fall;r.body.position.z=-.1*fall;r.leftArm.rotation.z=.65*fall;r.rightArm.rotation.z=-.65*fall;r.leftLeg.rotation.z=.13*fall;r.rightLeg.rotation.z=-.13*fall;}
}
