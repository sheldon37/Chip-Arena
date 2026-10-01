import * as T from 'three';
import {punchString,fighterReady,beginFighterLegs,applyFighterLegs,editFoot,holdGaze} from './fighter-motion.js';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
const sat=(x:number)=>T.MathUtils.clamp(x,0,1),ease=(x:number)=>{x=sat(x);return x*x*x*(x*(x*6-15)+10);};
/** Mishima guard: bladed, fists high, with a springy rhythm on the balls of the feet (the hips sink and lift, the chest and
 * fists ride the bounce a beat late, the head stays level). `legs` poses the legs directly (the intro stage); in play the
 * legs stand on the gait's planted stance (stances.ts) and the ground pass bends the knees under the bounce. */
export function emperorGuard(r:CharacterRig,time:number,legs=true){
 const beat=time*9.1,bounce=Math.sin(beat)+.3*Math.sin(2*beat-.8),late=Math.sin(beat-.9);
 r.body.position.y=-.05+.011*bounce;r.spine.rotation.set(.012*late,0,Math.sin(time*.9)*.01);r.body.rotation.set(.04+.01*late,-.3,0);r.pelvis.rotation.set(0,-.12,0);r.chest.rotation.set(-.008*late,.28,.006*Math.sin(beat*.5));r.head.rotation.set(-.01*late,.12,0);
 r.leftArm.rotation.set(-.8-.045*late,-.15,.08);r.leftElbow.rotation.set(-1.4+.04*late,0,0);r.rightArm.rotation.set(-.55-.035*Math.sin(beat-1.2),.12,-.18);r.rightElbow.rotation.set(-1.95,0,0);
 if(legs){r.leftLeg.rotation.set(-.23,0,.14);r.rightLeg.rotation.set(.2,0,-.18);r.leftKnee.rotation.x=.3;r.rightKnee.rotation.x=.2;r.leftFoot.rotation.x=-.1;r.rightFoot.rotation.x=-.25;}
}
/** Seconds a Mishima-screw spin lasts (shared/emperor.ts sets spinUntil = hit + .65). */
const SCREW_SPIN=.65;
/** Absolute authoritative hit times preserve the same launch and spin for all observers. Each hit kicks the body with an
 * impulse that rises over 65 ms (no single-frame jolt); a screw spin accumulates one whole turn from 0 at its hit and
 * decelerates, so it neither pops in nor out; limbs trail the vertical velocity and flail with staggered, decaying phases. */
export function animateJuggle(r:CharacterRig,p:PlayerView,time:number){
 const j=p.juggle!,age=Math.max(0,time-j.lastHit),pulse=(a:number)=>a<=0||a>2?0:a/.065*Math.exp(1-a/.065),kick=pulse(age)+pulse(time-previousHit(r,j,time)),start=j.spinUntil-SCREW_SPIN,spinU=j.spinUntil>time&&time>=start?sat((time-start)/Math.max(.05,j.spinUntil-start)):0,spin=Math.PI*2*ease(spinU);
 const air=time-j.at,lift=T.MathUtils.clamp(-p.vel.y*.06,-.35,.45),decay=.05+Math.exp(-air*1.5)*.17,flail=(phase:number)=>Math.sin(air*9+phase)*decay;
 JUGGLE_BONES.forEach((b,i)=>held[i].copy(r[b].quaternion));const launch=ease((time-j.at)/.16),rise=r.body.position.y;
 r.body.position.y=.03;r.body.rotation.set(-.7-kick*.45,Math.sin((time-j.at)*4)*.18+(spinU>0?spin:0),Math.sin(time*8)*.10);
 r.spine.rotation.set(.24-.08*kick,0,0);r.chest.rotation.set(.22,0,.13*kick);r.head.rotation.set(-.32+.3*kick*Math.exp(-age*6),0,.08);
 r.leftArm.rotation.set(-.45-.3*lift+flail(0),0,.65+.2*kick+.2*lift+flail(.6)*.6);r.rightArm.rotation.set(.25-.25*lift+flail(.55),0,-.75-.15*lift-flail(1.2)*.6);r.leftElbow.rotation.x=-.75+flail(1.6)*.5;r.rightElbow.rotation.x=-.4+flail(2.1)*.5;
 r.leftLeg.rotation.set(-.85-.25*kick-.2*lift+flail(.8),0,.22);r.rightLeg.rotation.set(-.4-.15*lift-flail(1.4),0,-.25);r.leftKnee.rotation.x=1.3+.2*lift+flail(2.4)*.4;r.rightKnee.rotation.x=.85+.2*Math.sin(time*9)+.25*lift;r.leftFoot.rotation.x=r.rightFoot.rotation.x=-.2;
 // The launch grows out of whatever pose the hit caught (finishPose then only has to absorb the weapon layer).
 if(launch<1){JUGGLE_BONES.forEach((b,i)=>r[b].quaternion.slerpQuaternions(held[i],launched.copy(r[b].quaternion),launch));r.body.position.y=rise+(.03-rise)*launch;}
}
const hitMemory=new WeakMap<CharacterRig,{at:number;last:number;previous:number}>();
/** The hit before j.lastHit (client-only memory), so a new hit's kick adds to the previous one instead of resetting it. */
function previousHit(r:CharacterRig,j:NonNullable<PlayerView['juggle']>,time:number){
 let m=hitMemory.get(r);if(!m||m.at!==j.at||time<m.last||j.lastHit<m.last){m={at:j.at,last:j.lastHit,previous:-Infinity};hitMemory.set(r,m);}
 else if(j.lastHit>m.last){m.previous=m.last;m.last=j.lastHit;}
 return m.previous;
}
const JUGGLE_BONES=['body','spine','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as const,held=JUGGLE_BONES.map(()=>new T.Quaternion()),launched=new T.Quaternion();
/** In play: the fighter layers over the gait, then one ground-anchored leg solve (fighter-motion applyFighterLegs). */
export function animateEmperor(r:CharacterRig,p:PlayerView,time:number){beginFighterLegs();emperorLayers(r,p,time);applyFighterLegs(r);}
function emperorLayers(r:CharacterRig,p:PlayerView,time:number){
 const upper=[r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand],upperBase=upper.map(n=>n.quaternion.clone());
 const body=r.body.position.clone(),travel=sat(r.motion*1.4);emperorGuard(r,time,false);
 if(p.grounded&&!p.waveTime){r.body.position.lerp(body,travel*.8);r.pelvis.rotation.y+=Math.sin(r.stride)*.07*travel;r.chest.rotation.y-=Math.sin(r.stride)*.05*travel;}
 r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 if(p.blockTime>0){r.leftArm.rotation.set(-1.3,0,.08);r.rightArm.rotation.set(-1.2,0,-.08);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.55;return;}
 const a=p.emperor;if((!a?.action||time>=a.until)&&p.grounded&&!p.guarding){const carry=ease((r.motion-.10)/.65);upper.forEach((n,i)=>n.quaternion.slerp(upperBase[i],carry));r.body.position.lerp(body,carry);}
 // Recover straight into the next jab's chamber; share standard melee's visual hold/timeout.
 fighterReady(r,p,time,a,'jab','emperor',ease((r.motion-.10)/.65));if(!a?.action||time>=a.until){holdGaze(r,.8);return;}const age=Math.max(0,time-a.at),duration=a.until-a.at;
 if(a.action==='wave'){
  // Wavedash: a low crouch-dash that skims the floor (the root glides; the lead foot reaches, the rear trails on its toes).
  const u=sat(age/.18),dip=Math.sin(u*Math.PI);r.body.position.y=-.08-.24*dip;r.body.position.z=.12*dip;r.body.rotation.x=.28*dip;r.rightArm.rotation.x=-.4;r.leftArm.rotation.x=-1;
  const skim=Math.sqrt(dip),lead=editFoot(true),rear=editFoot(false);lead.to.set(.16,-.772,.24);lead.tw=skim;rear.to.set(-.14,-.765,-.30);rear.tw=skim;rear.heel+=.55*skim;
 }else if(a.action==='electric'){
  // The planted crouch-dash unwinds into a bent-elbow hook at the authority's hit frame.
  const u=sat(age/duration),coil=ease(u/.115)*(1-ease((u-.115)/.135)),drive=ease((u-.115)/.135),hold=1-ease((u-.48)/.52),power=drive*hold;
  r.body.position.y=-.23*coil+.025*power;r.body.position.z=.09*coil+.17*power;
  r.body.rotation.set(.24*coil-.09*power,-.3-.42*coil+.5*power,-.06*power);r.pelvis.rotation.y=-.12-.2*coil+.25*power;r.chest.rotation.y=.28-.35*coil+.18*power;r.head.rotation.set(-.08*power,.12-.4*power,0);
  r.rightArm.rotation.set(-.55+.45*coil-1.25*power,.12+.32*power,-.18-.22*power);r.rightElbow.rotation.x=-1.95+.85*coil+.73*power;r.rightHand.rotation.y=-.4*power;r.rightShoulder.rotation.y=.22*power-.1*coil;r.spine.rotation.y=.14*power-.12*coil;r.leftShoulder.rotation.y=-.10*power;
  r.leftArm.rotation.set(-.8+.35*power,-.15,.08+.16*power);r.leftElbow.rotation.x=-1.4-.35*power;
  // The feet stay on the gait (planted, or stepping with the electric step's travel); the rear heel drives up into the hook.
  editFoot(false).heel+=.3*power*(1-travel);

 }else{
  const rate=p.attackRate||1;punchString(r,age*rate,.075,.17,a.singleHit?1:a.total,a.singleHit?a.index%2===0:true,'emperor',a.singleHit?Math.max(.02,duration*rate-.075-.016):Infinity);
 }
 holdGaze(r,a.action==='wave'?.5:.8);
}
