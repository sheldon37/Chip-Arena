import * as T from 'three';
import {punchString,donkeyKick,fighterReady,beginFighterLegs,applyFighterLegs,editFoot,holdGaze} from './fighter-motion.js';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
const clamp=(n:number)=>T.MathUtils.clamp(n,0,1),ease=(n:number)=>{n=clamp(n);return n*n*n*(n*(n*6-15)+10);};
/** Staggered karate guard: planted bent knees, forward left fist, rear fist at the cheek, breathing on a slow, deep bounce
 * (the fists ride it a beat late). `legs` poses the legs directly (intros, cinematics); in play they stand on the gait's
 * planted stance (stances.ts) and the ground pass bends the knees under the bounce. */
export function fighterGuard(r:CharacterRig,time:number,weight=1,legs=true){
 const beat=time*7.2,bounce=(Math.sin(beat)+.25*Math.sin(2*beat-.7))*.013,late=Math.sin(beat-.8)*.013,blend=(o:T.Object3D,x:number,y:number,z:number)=>o.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 r.body.position.y-=.07*weight;r.body.position.y+=bounce*weight;r.body.rotation.y=-.24*weight;
 blend(r.pelvis,-.05,-.18,0);blend(r.spine,0,.12,0);blend(r.chest,0,.22,0);blend(r.head,0,.10,0);
 if(legs){blend(r.leftLeg,-.28,.08,.14);blend(r.rightLeg,.19,-.08,-.22);blend(r.leftKnee,.4,0,0);blend(r.rightKnee,.24,0,0);
  blend(r.leftFoot,-.12,0,0);blend(r.rightFoot,-.32,0,0);}
 blend(r.leftArm,-.91+late*2.4,-.18,.12);blend(r.leftElbow,-1.16-late*1.5,0,0);blend(r.rightArm,-.58+late*2,.16,-.2);blend(r.rightElbow,-1.98,0,0);
 blend(r.leftHand,.06,0,.13);blend(r.rightHand,.02,0,-.10);
}
/** Parry: the hips drop into a braced crouch over the planted stance (in play the gait's feet hold and the knees bend). */
export function fighterParry(r:CharacterRig,time:number,legs=true){
 r.body.position.y=-.13;r.body.rotation.set(0,-.32,0);r.pelvis.rotation.set(0,-.15,0);r.chest.rotation.set(0,.22,0);
 if(legs){r.leftLeg.rotation.set(-.12,0,.45);r.rightLeg.rotation.set(.05,0,-.45);r.leftKnee.rotation.x=.58;r.rightKnee.rotation.x=.58;
 r.leftFoot.rotation.x=r.rightFoot.rotation.x=-.42;}r.leftArm.rotation.set(-1.45,-.18,.18);r.leftElbow.rotation.x=-.12;r.leftHand.rotation.set(-.95,0,-.12);
 r.rightArm.rotation.set(.14,.12,-.16);r.rightElbow.rotation.x=-1.6;r.rightHand.rotation.set(0,0,-.16);r.offWeapon.scale.setScalar(1);const fist=r.offWeapon.getObjectByName('fighter-fist'),palm=r.offWeapon.getObjectByName('fighter-palm');if(fist)fist.visible=false;if(palm)palm.visible=true;
}
/** The gliding stance from the supplied reference, with no running cycle. */
export function demonStepPose(r:CharacterRig,time:number,legs=true){
 r.body.position.set(0,-.04,0);r.body.rotation.set(.04,-.3,0);r.pelvis.rotation.set(0,-.12,0);r.chest.rotation.set(0,.2,0);r.head.rotation.set(0,.1,0);
 r.leftArm.rotation.set(-1.35,-.2,-.18);r.rightArm.rotation.set(-1.35,.2,.18);r.leftElbow.rotation.set(-1.1,0,-.25);r.rightElbow.rotation.set(-1.1,0,.25);
 if(legs){r.leftLeg.rotation.set(-1.12,0,.08);r.leftKnee.rotation.set(1.9,0,0);r.rightLeg.rotation.set(.1,0,-.12);r.rightKnee.rotation.set(.2,0,0);r.leftFoot.rotation.x=-.45;r.rightFoot.rotation.x=-.2;}r.body.position.y+=Math.sin(time*9)*.006;
}
/** In play, the gliding Demon Step blends in and out over its authoritative window: the lead knee tucks high, the rear foot
 * skims on its toes (the root glides at dash speed, so nothing is planted), and no leg ever dips below the floor. */
const DEMON_BONES=['body','pelvis','spine','chest','neck','head','leftArm','rightArm','leftElbow','rightElbow'] as const,demonHeld=DEMON_BONES.map(()=>new T.Quaternion()),demonBody=new T.Vector3();
function demonStepLayer(r:CharacterRig,time:number,since:number){
 const w=ease(since/.05)*(1-ease((since-.24)/.06));if(w<=0)return;
 DEMON_BONES.forEach((b,i)=>demonHeld[i].copy(r[b].quaternion));demonBody.copy(r.body.position);demonStepPose(r,time,false);
 DEMON_BONES.forEach((b,i)=>r[b].quaternion.slerpQuaternions(demonHeld[i],r[b].quaternion.clone(),w));r.body.position.lerpVectors(demonBody,r.body.position.clone(),w);
 const lead=editFoot(true),rear=editFoot(false);lead.to.set(.11,-.62,.10);lead.tw=w;lead.heel+=.37*w;rear.to.set(-.13,-.775,-.2);rear.tw=w;rear.heel+=.3*w;
}
/** In play: the fighter layers over the gait, then one ground-anchored leg solve (fighter-motion applyFighterLegs). */
export function animateAnsatsuken(r:CharacterRig,p:PlayerView,time:number){beginFighterLegs();ansatsukenLayers(r,p,time);applyFighterLegs(r);}
function ansatsukenLayers(r:CharacterRig,p:PlayerView,time:number){
 const upper=[r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand],upperBase=upper.map(n=>n.quaternion.clone());
 const body=r.body.position.clone(),travel=clamp(r.motion*1.4);fighterGuard(r,time,1-travel*.15,false);if(p.grounded){r.body.position.lerp(body,travel*.85);r.pelvis.rotation.y+=Math.sin(r.stride)*.07*travel;r.chest.rotation.y-=Math.sin(r.stride)*.1*travel;}r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 const fist=r.offWeapon.getObjectByName('fighter-fist'),palm=r.offWeapon.getObjectByName('fighter-palm');if(fist)fist.visible=true;if(palm)palm.visible=false;
 const a=p.arcade;if((!a?.action||time>=a.until)&&p.grounded&&p.blockTime<=0&&!p.guarding){const carry=ease((r.motion-.10)/.65);upper.forEach((n,i)=>n.quaternion.slerp(upperBase[i],carry));r.body.position.lerp(body,carry);}
 // Recover straight into the rear fist's chamber; Demon Step and parry release the hold.
 fighterReady(r,p,time,a,'combo','karate',ease((r.motion-.10)/.65));if(p.dashTime&&a?.demonStepAt!==undefined&&time>=a.demonStepAt&&time-a.demonStepAt<.30){demonStepLayer(r,time,time-a.demonStepAt);holdGaze(r,.6);return;}if(p.blockTime>0||time-(a?.parryAt??-10)<.22){fighterParry(r,time,false);holdGaze(r,.8);return;}if(!a?.action||time>a.until){holdGaze(r,.8);return;}const age=Math.max(0,time-a.at),hitAge=Math.max(0,time-p.lastAttack);
 if(a.action==='combo'){
  const rate=p.attackRate||1,gap=a.total===10?.115:.25,first=.12,lastAt=first+(a.total-1)*gap;
  punchString(r,age*rate,first,gap,a.total-1,false,'karate');
  donkeyKick(r,age*rate-lastAt,Math.min(.16,gap*.68),Math.max(.06,(a.until-a.at)*rate-lastAt));
 }else if(a.action==='tatsu'){
  const end=ease((a.until-time)/.18);r.body.rotation.y+=age*Math.PI*9;r.body.rotation.x=-.1;r.body.position.y+=.16*end;r.rightLeg.rotation.set(-.12,0,-1.47*end);r.rightKnee.rotation.x=.13;r.rightFoot.rotation.x=.1;r.leftLeg.rotation.x=-.15;r.leftKnee.rotation.x=.5;r.leftArm.rotation.set(-.75,0,.65);r.rightArm.rotation.set(-.75,0,-.65);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.3;
 }else if(a.action==='shoryuken'){
  const u=ease((age-.035)/.13),settle=ease((a.until-time)/.22),turn=ease((age-.10)/Math.max(.3,a.until-a.at-.18));r.body.rotation.y+=Math.PI*2*turn;r.body.position.y-=.16*(1-u);r.body.rotation.x=-.15*settle;r.rightArm.rotation.set(-3.08*u,.1,-.12);r.rightElbow.rotation.x=-1.7*(1-u)-.10;const knee=settle*ease(age/.12);r.rightLeg.rotation.x=-1.25*knee;r.rightKnee.rotation.x=1.8*knee;r.rightFoot.rotation.x=-.5*knee;r.leftLeg.rotation.x=.23;r.leftKnee.rotation.x=.28;r.leftArm.rotation.x=-.25;r.leftElbow.rotation.x=-1.95;
 }else{
  const charge=a.action==='charge',release=charge?0:ease(age/.12),hold=charge?1:1-ease((age-.2)/.28);
  r.pelvis.rotation.y=-.35*(1-release)*hold;r.chest.rotation.y=-.55*(1-release)*hold;r.body.rotation.x=.12*release*hold;
  r.leftArm.rotation.set(-.9-.6*release,-.65*(1-release),-.2);r.rightArm.rotation.set(-.68-.82*release,.50*(1-release),.2);
  r.leftElbow.rotation.x=-1.3*(1-release)-.16;r.rightElbow.rotation.x=-1.5*(1-release)-.16;r.leftHand.rotation.set(.25,0,-.4);r.rightHand.rotation.set(.25,0,.4);
  r.body.position.z=.14*release*hold;r.body.position.y-=.03*(charge?1:hold);editFoot(false).heel+=.3*release*hold*(1-travel);
 }
 if(a.action!=='tatsu'&&a.action!=='shoryuken')holdGaze(r,.8);
}
