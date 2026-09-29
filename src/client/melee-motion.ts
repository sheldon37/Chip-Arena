import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {braceWeapon} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {smoothMotion as ease} from './motion-quality.js';
import {poseCurve} from './pose-curves.js';
import type {attackPose} from './character-animation.js';
const keys=[0,.65,1,1.45,2,3];
/** Passing keys preserve angular velocity; anticipation and recovery carry the body's weight. */
export function meleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,time:number){
 const special=p.meleeAction,age=special?time-special.at:0,heavy=p.spec==='greatsword',dagger=p.spec==='daggers',shield=p.spec==='shield';
 r.weapon.visible=!p.weaponAway&&!(special?.released&&special.kind==='dagger-throw'&&time<special.until-.12);
 if(special&&time<special.until){specialMotion(r,p,time);return;}
 if(!a.active){const carry=ease((r.motion-.10)/.65);r.weapon.rotation.x=T.MathUtils.lerp(.5,.15,carry);if(dagger)r.offWeapon.rotation.x=r.weapon.rotation.x;return;}
 const phase=a.age<a.windup?a.age/a.windup:a.strike<1?1+a.strike:2+(1-a.recovery),weight=ease(a.age/Math.min(.06,a.windup))*a.recovery;
 const curve=(values:number[])=>poseCurve(keys,values,phase),left=dagger&&(p.attackIndex||1)%2===0,sign=left?-1:1,combo=((p.attackIndex||1)-1)%3;
 const torque=curve([0,-.48,-.7,.18,.95,.04])*(shield?.65:heavy?.85:1)*sign;
 r.pelvis.rotation.y+=torque*.38;r.spine.rotation.y+=torque*.48;r.chest.rotation.y+=torque*.35;r.head.rotation.y-=torque*.52;
 r.body.position.z+=curve([0,-.055,-.08,.10,.18,0]);r.body.position.y-=curve([0,.035,.075,.055,.11,0])*(heavy?1.4:1);
 r.body.rotation.x+=curve([0,-.04,-.12,.05,.21,0]);r.leftShoulder.rotation.y+=torque*.16;r.rightShoulder.rotation.y-=torque*.20;
 const joint=(node:T.Object3D,x:number,y=0,z=0)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 // Opposing feet drive the hips; lead knee accepts the impact, rear heel releases.
 const lead=left?r.rightLeg:r.leftLeg,rear=left?r.leftLeg:r.rightLeg,leadKnee=left?r.rightKnee:r.leftKnee,rearKnee=left?r.leftKnee:r.rightKnee;
 joint(lead,curve([-.10,-.2,-.36,-.52,-.65,-.1]),0,sign*.10);joint(leadKnee,curve([.2,.3,.45,.68,.8,.2]));joint(rear,curve([.12,.22,.28,.35,.42,.12]),0,-sign*.09);joint(rearKnee,.28);(left?r.leftFoot:r.rightFoot).rotation.x+=curve([0,.1,.16,.35,.42,0]);
 if(heavy){
  const overhead=(p.attackIndex||1)%2===1;
  const target=new T.Vector3(curve(overhead?[-.06,-.18,-.14,-.1,.08,-.06]:[-.06,-.28,-.3,-.05,.25,-.06]),curve(overhead?[.10,.36,.53,.32,-.19,.1]:[.1,.22,.30,.04,-.12,.1]),curve([.24,.06,-.01,.35,.4,.24]));
  braceWeapon(r,target,new T.Euler(curve(overhead?[-.2,-1.3,-1.9,-.6,.62,-.2]:[-.2,-.6,-.8,.2,.4,-.2]),curve(overhead?[.1,-.4,-.3,.12,.15,.1]:[.1,-1.3,-1.5,0,1.35,.1]),curve([0,-.2,-.3,-.12,.15,0])),weight);
 }else if(shield){
  joint(r.rightArm,curve([-.5,-.4,-.35,-1.1,-1.5,-.5]),curve([0,-.4,-.6,-.1,.25,0]),-.25);joint(r.rightElbow,curve([-1,-1.2,-1.4,-.68,-.22,-1]));joint(r.rightHand,0,curve([0,-.15,-.3,.05,.15,0]));r.weapon.rotation.set(1.65,0,0);
  joint(r.leftArm,curve([-.4,-.6,-.7,.1,.5,-.4]),.2,.35);joint(r.leftElbow,-1.15);r.chest.rotation.x+=curve([0,-.08,-.13,.1,.22,0]);
 }else if(dagger){
  const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,wrist=left?r.leftHand:r.rightHand,other=left?r.rightArm:r.leftArm;
  joint(arm,curve([-.65,-1.2,-1.55,-1.2,-.42,-.65]),curve([0,-.9,-1.1,.15,1.18,0])*sign,curve([-.18,-.65,-.75,-.28,.12,-.18])*sign);
  joint(elbow,curve([-1,-1.3,-1.45,-.5,-.32,-1]));joint(wrist,curve([0,-.1,-.2,.14,.35,0]),curve([0,-.35,-.4,.1,.32,0])*sign,-.2*sign);
  joint(other,curve([-.7,-.45,-.2,-.75,-1.1,-.7]),-.35*sign,.32*sign);joint(left?r.rightElbow:r.leftElbow,-1.35);
  r.weapon.rotation.set(1.48,0,left?-.15:.15);r.offWeapon.rotation.set(1.48,0,left?-.15:.15);
 }else{
  const reverse=combo===1?-1:1,overhead=combo===2;
  joint(r.rightArm,curve(overhead?[-.65,-1.6,-2.3,-1.5,-.35,-.65]:[-.65,-1,-1.25,-1.05,-.65,-.65]),curve([0,-.95,-1.15,.05,1.05,0])*reverse,curve([-.15,-.6,-.8,-.25,.2,-.15]));
  joint(r.rightElbow,curve([-1,-1.35,-1.55,-.52,-.22,-1]));joint(r.rightHand,curve([0,-.25,-.35,.1,.35,0]),curve([0,-.2,-.4,.1,.5,0])*reverse,curve([0,-.15,-.3,.1,.25,0]));r.weapon.rotation.set(1.5,0,0);
  joint(r.leftArm,curve([-.4,-.1,.12,-.2,-.65,-.4]),curve([.1,.3,.4,-.2,-.6,.1]),.38);joint(r.leftElbow,curve([-.7,-.55,-.5,-1,-1.3,-.7]));
 }
 if(p.chips.hurricane)r.body.rotation.y+=a.strike*Math.PI*2*a.recovery;
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
