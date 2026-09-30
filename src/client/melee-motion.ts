import {primaryMeleeMotion} from './primary-melee-motion.js';
import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {braceWeapon} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {smoothMotion as ease} from './motion-quality.js';
import type {attackPose} from './character-animation.js';
import {meleeReady} from './melee-guard.js';
export {meleeReady};
/** Passing keys preserve angular velocity; anticipation and recovery carry the body's weight. */
export function meleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,time:number){
 const special=p.meleeAction;
 r.weapon.visible=!p.weaponAway&&!(special?.released&&special.kind==='dagger-throw'&&time<special.until-.12);
 // Every melee layer sits on the combat-ready guard (idle) faded into the running carry, so entries and exits blend from it.
 if(special&&time<special.until){meleeReady(r,p,time);specialMotion(r,p,time);return;}
 if(!a.active){meleeReady(r,p,time);return;}
 if(a.age<Math.min(.065,a.windup))meleeReady(r,p,time);
 primaryMeleeMotion(r,p,a);
}

/** Special phrases (sword launcher, shield and dagger throws, wolf spin), driven by the authoritative at/releaseAt/until.
 * Past the release the pose keeps travelling (a slow ~12% drift beyond the throw) instead of freezing at full extension,
 * and it eases back to the guard from releaseAt+.06; the weapon's own hold blends by the same weight. */
function specialMotion(r:CharacterRig,p:PlayerView,time:number){
 const a=p.meleeAction!,age=time-a.at,wind=Math.max(.025,a.releaseAt-a.at),drive=ease((age-wind*.5)/(wind*.5)),coil=ease(age/(wind*.65))*(1-drive),drift=ease((age-wind)/.22)*.12;
 const settleFrom=a.releaseAt+.06,recovery=a.kind==='wolf-spin'?1:1-ease((time-settleFrom)/Math.max(.12,a.until-settleFrom)),weight=ease(age/.055)*recovery,d=drive+drift;
 const blend=(node:T.Object3D,x:number,y=0,z=0)=>node.quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),weight);
 if(a.kind==='wolf-spin'){
  const air=Math.max(0,time-a.releaseAt),land=a.landedAt===undefined?0:ease((time-a.landedAt)/.55),flight=ease(Math.min(1,air/.76)),squat=a.released?0:coil;
  // Full forward somersault around the hips, then planted two-handed landing.
  r.body.rotation.x=a.landedAt===undefined?flight*Math.PI*2:Math.PI*2+.24*(1-land);r.body.position.y-=squat*.18+(a.landedAt!==undefined?.25*(1-land):0);r.body.position.z+=a.landedAt!==undefined?.16*(1-land):0;
  r.pelvis.rotation.y=-.25*coil;r.spine.rotation.x=-.18*coil;r.chest.rotation.y=.16*coil;r.head.rotation.x=-.15;
  const tuck=a.released&&a.landedAt===undefined?Math.sin(Math.PI*flight):0;
  blend(r.leftLeg,-.65*squat-1.15*tuck-(1-land)*.2);blend(r.rightLeg,-.5*squat-.8*tuck);blend(r.leftKnee,.95*squat+1.55*tuck+(a.landedAt!==undefined?.7*(1-land):0));blend(r.rightKnee,.9*squat+1.4*tuck);
  const impact=a.landedAt!==undefined?1:Math.max(0,(flight-.72)/.28),settle=a.landedAt!==undefined?land:0;
  braceWeapon(r,new T.Vector3(-.06,.42*(1-impact)-.19*impact+.24*settle,.12+.28*impact),new T.Euler(-1.75*(1-impact)+.7*impact-.85*settle,0,-.08),ease(age/.06));return;
 }
 const torque=(-.65*coil+.95*d)*recovery;r.pelvis.rotation.y+=torque*.45;r.spine.rotation.y+=torque*.5;r.chest.rotation.y+=torque*.35;r.head.rotation.y-=torque*.65;
 r.body.position.z+=(-.08*coil+.16*d)*weight;r.body.position.y-=.07*coil*weight;r.body.rotation.x+=(-.08*coil+.16*d)*weight;
 blend(r.leftLeg,-.3-.25*d);blend(r.leftKnee,.42+.25*d);blend(r.rightLeg,.15+.2*d);blend(r.rightKnee,.32+(a.kind==='dagger-throw'?.18*d:0));
 if(a.kind==='sword-launch'){
  // Rising cut: heel lifts, the back arches and the chest opens, the arm finishes high with a soft elbow.
  blend(r.rightArm,-.1-2.3*d,-.65*coil+.1*d,-.2-.15*coil);blend(r.rightElbow,-1.0+.45*d);blend(r.rightHand,-.15+.35*d,0,-.12);r.weapon.quaternion.slerp(_q.setFromEuler(_e.set(1.45,0,0)),weight);
  r.body.position.y+=(-.16*coil+.13*d)*weight;r.body.rotation.x-=.23*d*weight;r.spine.rotation.x-=.12*d*weight;r.chest.rotation.x-=.06*d*weight;
  blend(r.leftArm,.3-.9*d,.1,.4);blend(r.leftElbow,-.85);blend(r.leftHand,.1,0,0);r.rightFoot.rotation.x+=.3*d*weight;r.leftFoot.rotation.x+=.35*d*weight;
 }else{
  // Throws: the throwing arm follows through across the body after the release; the dagger throw pushes off the rear toes.
  const shield=a.kind==='shield-throw',follow=ease((time-a.releaseAt)/.18)*(shield?.45:.3);
  blend(r.rightArm,shield?-.3-.08*coil-1.1*d:-1.25-.3*coil-.10*d,(-1.15*coil+1.1*d)*(shield?1:.7)+follow,-.45-(shield?.05:.25)*coil+follow*.4);blend(r.rightElbow,-(shield?1.1:1.7)*coil-.25*d-.15);blend(r.rightHand,-.15*coil+.3*d,-.35*coil+.5*d,shield?.15:-.3);
  r.weapon.quaternion.slerp(_q.setFromEuler(_e.set(shield?1.65:1.5,0,shield?.5*coil:0)),weight);blend(r.leftArm,-.3-.65*d,.4-.9*d,.4);blend(r.leftElbow,-1.1);blend(r.leftHand,.1,0,0);r.offWeapon.quaternion.slerp(_q.setFromEuler(_e.set(1.5,0,0)),weight);
  if(!shield){r.rightFoot.rotation.x+=.4*d*weight;r.body.position.z+=.04*d*weight;}
 }
}
const _q=new T.Quaternion(),_e=new T.Euler();
