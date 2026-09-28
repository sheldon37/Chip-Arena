import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
const sat=(x:number)=>T.MathUtils.clamp(x,0,1),ease=(x:number)=>{x=sat(x);return x*x*(3-2*x);};
export function emperorGuard(r:CharacterRig,time:number){
 r.body.position.y=-.045+Math.sin(time*5)*.006;r.body.rotation.set(.04,-.3,0);r.pelvis.rotation.set(0,-.12,0);r.chest.rotation.set(0,.28,0);r.head.rotation.set(0,.12,0);
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
 const speed=Math.hypot(p.vel.x,p.vel.z),leg=[r.leftLeg.rotation.clone(),r.rightLeg.rotation.clone(),r.leftKnee.rotation.clone(),r.rightKnee.rotation.clone()];emperorGuard(r,time);
 if(speed>1&&p.grounded&&!p.waveTime){[r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee].forEach((n,i)=>n.rotation.copy(leg[i]));}
 r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 if(p.blockTime>0){r.leftArm.rotation.set(-1.3,0,.08);r.rightArm.rotation.set(-1.2,0,-.08);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.55;return;}
 const a=p.emperor;if(speed>1&&p.grounded&&!p.waveTime&&!a?.action){const gait=Math.sin(r.stride)*Math.min(1,speed/6.8);r.rightArm.rotation.x+=gait*.4;r.leftArm.rotation.x-=gait*.4;}if(!a?.action||time>a.until+.08)return;const age=Math.max(0,time-a.at),duration=a.until-a.at;
 if(a.action==='wave'){
  const u=sat(age/.18),dip=Math.sin(u*Math.PI);r.body.position.y=-.08-.28*dip;r.body.position.z=.12*dip;r.body.rotation.x=.28*dip;r.leftLeg.rotation.x=-.75*dip;r.leftKnee.rotation.x=1.45*dip;r.rightLeg.rotation.x=.35*dip;r.rightKnee.rotation.x=.75*dip;r.leftFoot.rotation.x=-.65*dip;r.rightFoot.rotation.x=-.6*dip;r.rightArm.rotation.x=-.4;r.leftArm.rotation.x=-1;
 }else if(a.action==='electric'){
  const u=sat(age/duration),drive=ease((u-.13)/.23),settle=1-ease((u-.62)/.38),coil=(1-drive)*Math.sin(Math.min(1,u/.13)*Math.PI/2);
  r.body.position.y=-.25*coil+.07*drive*settle;r.body.position.z=.16*drive*settle;r.body.rotation.set(.35*coil-.15*drive*settle,-.65+.92*drive*settle,0);r.pelvis.rotation.y=-.2+.4*drive*settle;r.chest.rotation.y=.1+.38*drive*settle;
  r.rightArm.rotation.set(-.35-2.4*drive*settle,.14,-.16);r.rightElbow.rotation.x=-1.5+.65*drive*settle;r.leftArm.rotation.set(-.8,0,.15);r.leftElbow.rotation.x=-1.65;
  r.leftLeg.rotation.x=-.4*coil;r.rightLeg.rotation.x=-.65*drive*settle;r.leftKnee.rotation.x=.35+.7*coil;r.rightKnee.rotation.x=.35+1.05*drive*settle;r.head.rotation.x=-.12*drive*settle;
 }else{
  const rate=p.attackRate||1,gap=.17/rate,index=Math.min(a.total-1,Math.max(0,Math.floor((age-.075/rate+.05/rate)/gap))),phase=(age-.075/rate-index*gap)*rate,u=phase<0?ease((phase+.08)/.08):1-ease((phase-.025)/.115),left=index%2===0,sign=left?1:-1,arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow;
  r.body.rotation.y=-.3+.42*sign*u;r.chest.rotation.y=.28+.25*sign*u;r.head.rotation.y=.12-.28*sign*u;r.body.position.z=.13*u;arm.rotation.set(-1.48,-.08*sign,.08*sign);elbow.rotation.x=-1.4*(1-u)-.03;r.rightKnee.rotation.x+=.12*u;
 }
}
