import * as T from 'three';
import {punchString} from './fighter-motion.js';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
const sat=(x:number)=>T.MathUtils.clamp(x,0,1),ease=(x:number)=>{x=sat(x);return x*x*x*(x*(x*6-15)+10);};
export function emperorGuard(r:CharacterRig,time:number){
 r.body.position.y=-.045+Math.sin(time*2.3)*.008;r.spine.rotation.set(Math.sin(time*2.3)*.008,0,Math.sin(time*.9)*.01);r.body.rotation.set(.04,-.3,0);r.pelvis.rotation.set(0,-.12,0);r.chest.rotation.set(0,.28,0);r.head.rotation.set(0,.12,0);
 r.leftArm.rotation.set(-.8,-.15,.08);r.leftElbow.rotation.set(-1.4,0,0);r.rightArm.rotation.set(-.55,.12,-.18);r.rightElbow.rotation.set(-1.95,0,0);
 r.leftLeg.rotation.set(-.23,0,.14);r.rightLeg.rotation.set(.2,0,-.18);r.leftKnee.rotation.x=.3;r.rightKnee.rotation.x=.2;r.leftFoot.rotation.x=-.1;r.rightFoot.rotation.x=-.25;
}
/** Seconds a Mishima-screw spin lasts (shared/emperor.ts sets spinUntil = hit + .65). */
const SCREW_SPIN=.65;
/** Absolute authoritative hit times preserve the same launch and spin for all observers. Each hit kicks the body with an
 * impulse that rises over 65 ms (no single-frame jolt); a screw spin accumulates one whole turn from 0 at its hit and
 * decelerates, so it neither pops in nor out; limbs trail the vertical velocity and flail with staggered, decaying phases. */
export function animateJuggle(r:CharacterRig,p:PlayerView,time:number){
 const j=p.juggle!,age=Math.max(0,time-j.lastHit),pulse=(a:number)=>a<=0||a>2?0:a/.065*Math.exp(1-a/.065),kick=pulse(age)+pulse(time-previousHit(r,j,time)),start=j.spinUntil-SCREW_SPIN,spinU=j.spinUntil>time&&time>=start?sat((time-start)/Math.max(.05,j.spinUntil-start)):0,spin=Math.PI*2*ease(spinU);
 const air=time-j.at,lift=T.MathUtils.clamp(-p.vel.y*.06,-.35,.45),decay=.05+Math.exp(-air*1.5)*.17,flail=(phase:number)=>Math.sin(air*9+phase)*decay;
 const incoming=JUGGLE_BONES.map((b,i)=>held[i].copy(r[b].quaternion)),launch=ease((time-j.at)/.16),rise=r.body.position.y;
 r.body.position.y=.03;r.body.rotation.set(-.7-kick*.45,Math.sin((time-j.at)*4)*.18+(spinU>0?spin:0),Math.sin(time*8)*.10);
 r.spine.rotation.set(.24-.08*kick,0,0);r.chest.rotation.set(.22,0,.13*kick);r.head.rotation.set(-.32+.3*kick*Math.exp(-age*6),0,.08);
 r.leftArm.rotation.set(-.45-.3*lift+flail(0),0,.65+.2*kick+.2*lift+flail(.6)*.6);r.rightArm.rotation.set(.25-.25*lift+flail(.55),0,-.75-.15*lift-flail(1.2)*.6);r.leftElbow.rotation.x=-.75+flail(1.6)*.5;r.rightElbow.rotation.x=-.4+flail(2.1)*.5;
 r.leftLeg.rotation.set(-.85-.25*kick-.2*lift+flail(.8),0,.22);r.rightLeg.rotation.set(-.4-.15*lift-flail(1.4),0,-.25);r.leftKnee.rotation.x=1.3+.2*lift+flail(2.4)*.4;r.rightKnee.rotation.x=.85+.2*Math.sin(time*9)+.25*lift;r.leftFoot.rotation.x=r.rightFoot.rotation.x=-.2;
 // The launch grows out of whatever pose the hit caught (finishPose then only has to absorb the weapon layer).
 if(launch<1){JUGGLE_BONES.forEach((b,i)=>r[b].quaternion.slerpQuaternions(incoming[i],r[b].quaternion.clone(),launch));r.body.position.y=rise+(.03-rise)*launch;}
}
const hitMemory=new WeakMap<CharacterRig,{at:number;last:number;previous:number}>();
/** The hit before j.lastHit (client-only memory), so a new hit's kick adds to the previous one instead of resetting it. */
function previousHit(r:CharacterRig,j:NonNullable<PlayerView['juggle']>,time:number){
 let m=hitMemory.get(r);if(!m||m.at!==j.at||time<m.last||j.lastHit<m.last){m={at:j.at,last:j.lastHit,previous:-Infinity};hitMemory.set(r,m);}
 else if(j.lastHit>m.last){m.previous=m.last;m.last=j.lastHit;}
 return m.previous;
}
const JUGGLE_BONES=['body','spine','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as const,held=JUGGLE_BONES.map(()=>new T.Quaternion());
export function animateEmperor(r:CharacterRig,p:PlayerView,time:number){
 const upper=[r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand],upperBase=upper.map(n=>n.quaternion.clone());
 const lower=[r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot],base=lower.map(n=>n.quaternion.clone()),body=r.body.position.clone(),travel=sat(r.motion*1.4);emperorGuard(r,time);
 if(p.grounded&&!p.waveTime){lower.forEach((n,i)=>n.quaternion.slerp(base[i],travel));r.body.position.lerp(body,travel*.8);r.pelvis.rotation.y+=Math.sin(r.stride)*.07*travel;r.chest.rotation.y-=Math.sin(r.stride)*.05*travel;}
 r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 if(p.blockTime>0){r.leftArm.rotation.set(-1.3,0,.08);r.rightArm.rotation.set(-1.2,0,-.08);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.55;return;}
 const a=p.emperor;if((!a?.action||time>=a.until)&&p.grounded&&!p.guarding){const carry=ease((r.motion-.10)/.65);upper.forEach((n,i)=>n.quaternion.slerp(upperBase[i],carry));r.body.position.lerp(body,carry);}if(!a?.action||time>=a.until)return;const age=Math.max(0,time-a.at),duration=a.until-a.at;
 if(a.action==='wave'){
  const u=sat(age/.18),dip=Math.sin(u*Math.PI);r.body.position.y=-.08-.28*dip;r.body.position.z=.12*dip;r.body.rotation.x=.28*dip;r.leftLeg.rotation.x=-.75*dip;r.leftKnee.rotation.x=1.45*dip;r.rightLeg.rotation.x=.35*dip;r.rightKnee.rotation.x=.75*dip;r.leftFoot.rotation.x=-.65*dip;r.rightFoot.rotation.x=-.6*dip;r.rightArm.rotation.x=-.4;r.leftArm.rotation.x=-1;
 }else if(a.action==='electric'){
  // The planted crouch-dash unwinds into a bent-elbow hook at the authority's hit frame.
  const u=sat(age/duration),coil=ease(u/.115)*(1-ease((u-.115)/.135)),drive=ease((u-.115)/.135),hold=1-ease((u-.48)/.52),power=drive*hold;
  r.body.position.y=-.23*coil+.025*power;r.body.position.z=.09*coil+.17*power;
  r.body.rotation.set(.24*coil-.09*power,-.3-.42*coil+.5*power,-.06*power);r.pelvis.rotation.y=-.12-.2*coil+.25*power;r.chest.rotation.y=.28-.35*coil+.18*power;r.head.rotation.set(-.08*power,.12-.4*power,0);
  r.rightArm.rotation.set(-.55+.45*coil-1.25*power,.12+.32*power,-.18-.22*power);r.rightElbow.rotation.x=-1.95+.85*coil+.73*power;r.rightHand.rotation.y=-.4*power;r.rightShoulder.rotation.y=.22*power-.1*coil;r.spine.rotation.y=.14*power-.12*coil;r.leftShoulder.rotation.y=-.10*power;
  r.leftArm.rotation.set(-.8+.35*power,-.15,.08+.16*power);r.leftElbow.rotation.x=-1.4-.35*power;
  r.leftLeg.rotation.set(-.23-.4*coil-.12*power,0,.14);r.leftKnee.rotation.x=.3+.7*coil+.12*power;
  r.rightLeg.rotation.set(.2-.2*coil+.18*power,0,-.18);r.rightKnee.rotation.x=.2+.75*coil;r.leftFoot.rotation.x=-.1;r.rightFoot.rotation.x=-.25-.15*power;

 }else{
  const rate=p.attackRate||1;punchString(r,age*rate,.075,.17,a.total,true,'emperor');
 }
}
