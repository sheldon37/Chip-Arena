import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
const clamp=(n:number)=>T.MathUtils.clamp(n,0,1),ease=(n:number)=>{n=clamp(n);return n*n*(3-2*n);};
/** Staggered karate guard: planted bent knees, forward left fist, rear fist at the cheek. */
export function fighterGuard(r:CharacterRig,time:number,weight=1){
 const bounce=Math.sin(time*7)*.018,blend=(o:T.Object3D,x:number,y:number,z:number)=>o.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 r.body.position.y-=.07*weight;r.body.position.y+=bounce*weight;r.body.rotation.y=-.24*weight;
 blend(r.pelvis,-.05,-.18,0);blend(r.spine,0,.12,0);blend(r.chest,0,.22,0);blend(r.head,0,.10,0);
 blend(r.leftLeg,-.28,.08,.14);blend(r.rightLeg,.19,-.08,-.22);blend(r.leftKnee,.4,0,0);blend(r.rightKnee,.24,0,0);
 blend(r.leftFoot,-.12,0,0);blend(r.rightFoot,-.32,0,0);
 blend(r.leftArm,-.91+bounce*2,-.18,.12);blend(r.leftElbow,-1.16,0,0);blend(r.rightArm,-.58+bounce*2,.16,-.2);blend(r.rightElbow,-1.98,0,0);
 blend(r.leftHand,.06,0,.13);blend(r.rightHand,.02,0,-.10);
}
export function fighterParry(r:CharacterRig,time:number){
 r.body.position.y=-.13;r.body.rotation.set(0,-.32,0);r.pelvis.rotation.set(0,-.15,0);r.chest.rotation.set(0,.22,0);
 r.leftLeg.rotation.set(-.12,0,.45);r.rightLeg.rotation.set(.05,0,-.45);r.leftKnee.rotation.x=.58;r.rightKnee.rotation.x=.58;
 r.leftFoot.rotation.x=r.rightFoot.rotation.x=-.42;r.leftArm.rotation.set(-1.45,-.18,.18);r.leftElbow.rotation.x=-.12;r.leftHand.rotation.set(-.95,0,-.12);
 r.rightArm.rotation.set(.14,.12,-.16);r.rightElbow.rotation.x=-1.6;r.rightHand.rotation.set(0,0,-.16);r.offWeapon.scale.setScalar(1);const fist=r.offWeapon.getObjectByName('fighter-fist'),palm=r.offWeapon.getObjectByName('fighter-palm');if(fist)fist.visible=false;if(palm)palm.visible=true;
}
export function animateAnsatsuken(r:CharacterRig,p:PlayerView,time:number){
 const speed=Math.hypot(p.vel.x,p.vel.z);fighterGuard(r,time,1-clamp(speed/9)*.35);r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);r.weapon.visible=r.offWeapon.visible=true;
 const fist=r.offWeapon.getObjectByName('fighter-fist'),palm=r.offWeapon.getObjectByName('fighter-palm');if(fist)fist.visible=true;if(palm)palm.visible=false;
 const a=p.arcade;if(p.blockTime>0||time-(a?.parryAt??-10)<.22){fighterParry(r,time);return;}if(!a?.action||time>a.until+.1)return;const age=Math.max(0,time-a.at),hitAge=Math.max(0,time-p.lastAttack);
 if(a.action==='combo'){
  const rate=p.attackRate||1,first=.12/rate,gap=(a.total===10?.115:.25)/rate;
  // Anticipation leads the authority's strike; each impact has a readable hold and recoil.
  const index=Math.min(a.total-1,Math.max(0,Math.floor((age-first+.055/rate)/gap))),impactAt=a.at+first+index*gap;
  const phase=(time-impactAt)*rate,u=phase<0?ease((phase+.12)/.12):1-ease((phase-.045)/.17),last=index===a.total-1;
  if(last){const chamber=phase<0?ease((phase+.14)/.1):0;r.body.rotation.x=-.19*u;r.body.rotation.y=-.26*u;r.pelvis.rotation.y=-.22*u;r.rightLeg.rotation.set(-2.7*u-.85*chamber,.04,-.12*u);r.rightKnee.rotation.x=.12*u+1.45*chamber;r.rightFoot.rotation.x=-.22*u;r.leftLeg.rotation.x=.06;r.leftKnee.rotation.x=.18;r.leftArm.rotation.set(-1.1,0,.25);r.rightArm.rotation.set(-.7,0,-.35);r.leftElbow.rotation.x=-1.65;r.rightElbow.rotation.x=-1.7;r.chest.rotation.y=.24*u;r.body.position.z=.16*u;}
  else{const left=index%2===1,arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,sign=left?1:-1;r.body.rotation.y=.28*sign*u;r.pelvis.rotation.y=.32*sign*u;r.chest.rotation.y=.48*sign*u;r.head.rotation.y=-.3*sign*u;arm.rotation.set(-1.45,-.12*sign,.05*sign);elbow.rotation.x=-1.35*(1-u)-.06;r.body.position.z=.15*u;r.body.position.y-=.06*u;r.leftKnee.rotation.x+=.2*u;r.rightKnee.rotation.x+=.12*u;}
 }else if(a.action==='tatsu'){
  const end=ease((a.until-time)/.18);r.body.rotation.y+=age*Math.PI*9;r.body.rotation.x=-.1;r.body.position.y+=.16*end;r.rightLeg.rotation.set(-.12,0,-1.47*end);r.rightKnee.rotation.x=.13;r.rightFoot.rotation.x=.1;r.leftLeg.rotation.x=-.15;r.leftKnee.rotation.x=.5;r.leftArm.rotation.set(-.75,0,.65);r.rightArm.rotation.set(-.75,0,-.65);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.3;
 }else if(a.action==='shoryuken'){
  const u=ease((age-.035)/.13),settle=ease((a.until-time)/.22),turn=ease((age-.10)/Math.max(.3,a.until-a.at-.18));r.body.rotation.y+=Math.PI*2*turn;r.body.position.y-=.16*(1-u);r.body.rotation.x=-.15*settle;r.rightArm.rotation.set(-3.08*u,.1,-.12);r.rightElbow.rotation.x=-1.7*(1-u)-.10;r.rightLeg.rotation.x=-1.25*settle;r.rightKnee.rotation.x=1.8*settle;r.rightFoot.rotation.x=-.5*settle;r.leftLeg.rotation.x=.23;r.leftKnee.rotation.x=.28;r.leftArm.rotation.x=-.25;r.leftElbow.rotation.x=-1.95;
 }else{
  const charge=a.action==='charge',release=charge?0:ease(age/.12),hold=charge?1:1-ease((age-.2)/.28);
  r.pelvis.rotation.y=-.35*(1-release)*hold;r.chest.rotation.y=-.55*(1-release)*hold;r.body.rotation.x=.12*release*hold;
  r.leftArm.rotation.set(-.9-.6*release,-.65*(1-release),-.2);r.rightArm.rotation.set(-.68-.82*release,.50*(1-release),.2);
  r.leftElbow.rotation.x=-1.3*(1-release)-.16;r.rightElbow.rotation.x=-1.5*(1-release)-.16;r.leftHand.rotation.set(.25,0,-.4);r.rightHand.rotation.set(.25,0,.4);
  r.leftLeg.rotation.x=-.35;r.rightLeg.rotation.x=.3;r.leftKnee.rotation.x=.48;r.rightKnee.rotation.x=.3;r.body.position.z=.14*release*hold;
 }
}
