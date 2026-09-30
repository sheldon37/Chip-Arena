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

function specialMotion(r:CharacterRig,p:PlayerView,time:number){
 const a=p.meleeAction!,age=time-a.at,wind=Math.max(.025,a.releaseAt-a.at),drive=ease((age-wind*.55)/(wind*.55)),coil=ease(age/(wind*.65))*(1-drive),recovery=a.kind==='shield-throw'?1-ease((time-a.releaseAt-.16)/.25):a.kind==='wolf-spin'?1:1-ease((time-a.releaseAt-.1)/Math.max(.1,a.until-a.releaseAt-.1)),weight=ease(age/.055)*recovery;
 const blend=(node:T.Object3D,x:number,y=0,z=0)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
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
 const torque=(-.65*coil+.95*drive)*recovery;r.pelvis.rotation.y+=torque*.45;r.spine.rotation.y+=torque*.5;r.chest.rotation.y+=torque*.35;r.head.rotation.y-=torque*.65;
 r.body.position.z+=(-.08*coil+.16*drive)*weight;r.body.position.y-=.07*coil*weight;r.body.rotation.x+=(-.08*coil+.16*drive)*weight;
 blend(r.leftLeg,-.3-.25*drive);blend(r.leftKnee,.42+.25*drive);blend(r.rightLeg,.15+.2*drive);blend(r.rightKnee,.32);
 if(a.kind==='sword-launch'){
  blend(r.rightArm,-.1-2.3*drive,-.65*coil+.1*drive,-.2-.15*coil);blend(r.rightElbow,-1.0+.65*drive);blend(r.rightHand,-.15+.35*drive,0,-.12);r.weapon.rotation.set(1.45,0,0);
  r.body.position.y+=(-.16*coil+.13*drive)*weight;r.body.rotation.x-=.23*drive*weight;blend(r.leftArm,.3-.9*drive,.1,.4);blend(r.leftElbow,-.85);r.rightFoot.rotation.x+=.3*drive*weight;
 }else{
  const shield=a.kind==='shield-throw';
  blend(r.rightArm,shield?-.3-.08*coil-1.1*drive:-1.25-.3*coil-.10*drive,(-1.15*coil+1.1*drive)*(shield?1:.7),-.45-(shield?.05:.25)*coil);blend(r.rightElbow,-(shield?1.1:1.7)*coil-.25*drive-.15);blend(r.rightHand,-.15*coil+.3*drive,-.35*coil+.5*drive,shield?.15:-.3);
  r.weapon.rotation.set(shield?1.65:1.5,0,shield?.5*coil:0);blend(r.leftArm,-.3-.65*drive,.4-.9*drive,.4);blend(r.leftElbow,-1.1);r.offWeapon.rotation.set(1.5,0,0);
 }
}
