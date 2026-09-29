import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {braceWeapon} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {smoothMotion as ease} from './motion-quality.js';
import type {attackPose} from './character-animation.js';
/** Weapon-specific full-body phrases. Contact stays aligned to the authority's sweep. */
export function meleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>){
 const heavy=p.spec==='greatsword',dagger=p.spec==='daggers',shield=p.spec==='shield',left=dagger&&(p.attackIndex||1)%2===0,sign=left?-1:1;
 if(!a.active){const carry=ease((r.motion-.10)/.65);r.weapon.rotation.x=T.MathUtils.lerp(.35,.15,carry);if(dagger){r.leftElbow.rotation.x-=.2*(1-carry);r.offWeapon.rotation.x=r.weapon.rotation.x;}return;}
 const wind=ease(a.age/a.windup),strike=ease(a.strike),settle=a.recovery,weight=ease(a.age/Math.min(.065,a.windup))*settle;
 const coil=wind*(1-strike),drive=strike*settle,torque=(-.68*coil+.88*drive)*sign;
 r.pelvis.rotation.y+=torque*(heavy?.30:.21);r.spine.rotation.y+=torque*(heavy?.55:.42);r.chest.rotation.y+=torque*.38;r.head.rotation.y-=torque*.42;
 r.body.position.z+=(-.045*coil+(shield?.22:heavy?.13:.09)*drive)*weight;r.body.position.y-=(.055*coil+.03*drive)*weight;r.body.rotation.x+=(-.045*coil+(heavy?.18:.10)*drive)*weight;
 r.leftKnee.rotation.x+=.15*weight;r.rightKnee.rotation.x+=.21*weight;r.leftLeg.rotation.x-=.10*weight;r.rightLeg.rotation.x-=.10*weight;
 r.leftShoulder.rotation.y+=.12*torque;r.rightShoulder.rotation.y-=.15*torque;
 const blend=(node:T.Object3D,x:number,y:number,z:number)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 if(shield){blend(r.rightArm,-.7-.85*strike,-.12,-.18);blend(r.rightElbow,-1.55+1.35*strike,0,0);blend(r.rightHand,-.08*strike,0,-.08);r.weapon.rotation.x=1.6;blend(r.leftArm,.15+.55*strike,0,.38);blend(r.leftElbow,-.7,0,0);r.chest.rotation.x+=.08*drive;}
 else if(dagger){const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,wrist=left?r.leftHand:r.rightHand,trail=left?r.rightArm:r.leftArm;
  blend(arm,-.55-.93*strike,(-.48*coil+.32*drive)*sign,-.22*sign);blend(elbow,-1.6+1.38*strike,0,0);blend(wrist,.12*strike,-.2*sign*strike,-.18*sign);blend(trail,-.5+.25*strike,-.2*sign,.32*sign);
  (left?r.rightElbow:r.leftElbow).rotation.x=-1.35;r.weapon.rotation.x=left?.55:1.42;r.offWeapon.rotation.x=left?1.42:.55;
 }else if(heavy){
  // Raised two-handed guard folds into a diagonal cut; the off hand stays on its socket.
  const target=new T.Vector3(-.05-.18*coil+.13*drive,.1+.38*coil-.08*drive,.2+.11*drive);
  braceWeapon(r,target,new T.Euler(-1.35*coil+.58*drive,-.6*coil+.72*drive,-.25*coil+.24*drive),weight);
 }else{blend(r.rightArm,-.75-.78*coil+.3*drive,-.72*coil+.80*drive,-.25-.32*coil);blend(r.rightElbow,-.65-.8*coil+.35*drive,0,0);blend(r.rightHand,.2*drive,-.25*coil+.26*drive,-.2*coil+.2*drive);r.weapon.rotation.x=1.4+.25*coil;blend(r.leftArm,.15+.42*drive,-.15,.35+.12*coil);blend(r.leftElbow,-.7-.22*coil,0,0);}
 if(p.chips.hurricane)r.body.rotation.y+=a.strike*Math.PI*2*settle;
}
