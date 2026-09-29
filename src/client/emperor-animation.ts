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
/** Absolute authoritative hit times preserve the same launch and spin for all observers. */
export function animateJuggle(r:CharacterRig,p:PlayerView,time:number){
 const j=p.juggle!;const age=Math.max(0,time-j.lastHit),kick=Math.exp(-age*7),spin=j.spinUntil>time;
 r.body.position.y=.03;r.body.rotation.set(-.7-kick*.55,spin?(j.spinUntil-time)*9:Math.sin((time-j.at)*4)*.18,Math.sin(time*8)*.10);
 r.spine.rotation.set(.24,0,0);r.chest.rotation.set(.22,0,.13*kick);r.head.rotation.set(-.32,0,.08);
 r.leftArm.rotation.set(-.45,0,.65+.2*kick);r.rightArm.rotation.set(.25,0,-.75);r.leftElbow.rotation.x=-.75;r.rightElbow.rotation.x=-.4;
 r.leftLeg.rotation.set(-.85-.25*kick,0,.22);r.rightLeg.rotation.set(-.4,0,-.25);r.leftKnee.rotation.x=1.3;r.rightKnee.rotation.x=.85+.2*Math.sin(time*9);r.leftFoot.rotation.x=r.rightFoot.rotation.x=-.2;
}
export function animateEmperor(r:CharacterRig,p:PlayerView,time:number){
 const speed=Math.hypot(p.vel.x,p.vel.z),lower=[r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot],base=lower.map(n=>n.quaternion.clone()),body=r.body.position.clone(),travel=sat(r.motion*1.4);emperorGuard(r,time);
 if(p.grounded&&!p.waveTime){lower.forEach((n,i)=>n.quaternion.slerp(base[i],travel));r.body.position.lerp(body,travel*.8);r.pelvis.rotation.y+=Math.sin(r.stride)*.07*travel;r.chest.rotation.y-=Math.sin(r.stride)*.05*travel;}
 r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 if(p.blockTime>0){r.leftArm.rotation.set(-1.3,0,.08);r.rightArm.rotation.set(-1.2,0,-.08);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.55;return;}
 const a=p.emperor;if(!a?.action&&p.grounded){const gait=Math.sin(r.stride)*travel;r.rightArm.rotation.x+=gait*.55;r.leftArm.rotation.x-=gait*.55;r.rightElbow.rotation.x+=gait*.12;r.leftElbow.rotation.x-=gait*.12;}if(!a?.action||time>a.until)return;const age=Math.max(0,time-a.at),duration=a.until-a.at;
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
