import {locomotion,finishPose} from './motion-quality.js';
import {meleeMotion} from './melee-motion.js';
import {animateEmperor,animateJuggle} from './emperor-animation.js';
import {animateCosmetics} from './auditor-halo.js';
import {animateAnsatsuken} from './ansatsuken-animation.js';
import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,castWindup} from '../shared/content.js';
import {clamp} from '../shared/config.js';
import {CharacterRig,supportHand,braceWeapon} from './character-rig.js';

export const ANIMATION={blendSpeed:12,stridePerMetre:2.2,runLean:.20,deathSeconds:5,deathVoxels:120,reducedDeathVoxels:48,maxDeathVoxels:280} as const;
export type AnimatedRig=CharacterRig;
const smooth=(a:number)=>{a=clamp(a,0,1);return a*a*(3-2*a);};
export function attackPose(p:PlayerView,time:number){
 const w=WEAPONS[p.spec],rate=p.attackRate||1,age=Math.max(0,time-p.lastAttack),windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 const recoverySeconds=(p.spec==='greatsword'?.5:p.spec==='daggers'?.15:.3)/rate;
 const active=w.class==='melee'&&age<windup+window+recoverySeconds;
 const load=active&&age<windup?smooth(age/windup):0;
 const strike=active&&age>=windup?clamp((age-windup)/Math.max(.025,window),0,1):0;
 const recovery=active&&age>=windup+window?1-smooth((age-windup-window)/recoverySeconds):1;
 const swing=active?(age<windup?-.9*load:(-.9+strike*2.5)*recovery):0;
 const shotAge=time-(p.lastShot??p.lastAttack),recoil=shotAge>=0?Math.exp(-shotAge*(p.spec==='rifle'?13:22)):0;
 const magic=spellPose(p,time),cast=magic.weight;
 const reload=p.reloadTime>0?Math.sin(clamp(1-p.reloadTime/Math.max(.1,w.reload),0,1)*Math.PI):0;
 return {load,strike,swing,recoil,cast,reload,active,age,shotAge,windup,window,recovery,magic};
}
/** A coil → release → hold → settle phrase, aligned with authoritative spell release. */
export function spellPose(p:PlayerView,time:number){
 const rate=p.attackRate||1,age=Math.max(0,time-p.lastAttack),releaseAt=castWindup(p.spec,rate),windup=releaseAt*.6,release=releaseAt-windup,hold=.07/rate,recover=.26/rate;
 const active=WEAPONS[p.spec].class==='magic'&&age<releaseAt+hold+recover;
 const coil=active&&age<windup?smooth(age/Math.max(.01,windup)):0;
 const drive=active&&age>=windup?smooth((age-windup)/Math.max(.01,release)):0;
 const settle=active&&age>releaseAt+hold?1-smooth((age-releaseAt-hold)/recover):1;
 return {coil,drive,weight:active?(age<windup?coil:settle):0,active,windup,releaseAt,age,settle};
}
function blendRotation(node:T.Object3D,x:number,y:number,z:number,weight:number){node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),clamp(weight,0,1));}
/** Distance-driven locomotion under action layers. Authoritative attack timestamps keep the hit phase aligned. */
function sampleCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){
 animateCosmetics(r,time);
 const w=WEAPONS[p.spec],{run}=locomotion(r,{...p,pitch},time,dt),pose=attackPose(p,time);
 r.weapon.position.set(0,0,0);r.weapon.rotation.set(.5,0,0);r.weapon.scale.set(1,1,w.class==='melee'?p.reach/w.range:1);r.offWeapon.rotation.set(.55,0,0);r.offWeapon.scale.copy(r.weapon.scale);
 if(p.juggle&&time<p.juggle.until){animateJuggle(r,p,time);return pose;}
 if(p.knockdown&&time<p.knockdown.until){
  const k=p.knockdown,air=k.landedAt===undefined,age=time-k.at,legsUp=!air&&!k.pickupUsed&&time<(k.pickupUntil||0),rolling=!air&&k.rollAt!==undefined&&time>=k.rollAt&&time<(k.rollUntil||0),rolled=!air&&k.rollUntil!==undefined&&time>=k.rollUntil,rise=rolled?smooth((time-k.rollUntil!)/Math.max(.01,k.until-k.rollUntil!)):0,u=rolling?smooth((time-k.rollAt!)/Math.max(.01,k.rollUntil!-k.rollAt!)):0;
  r.body.rotation.set(air?-Math.min(1.3,.2+age*2):-Math.PI/2*(1-rise)-Math.PI*2*u,rolling||rolled?(Math.atan2(k.rollX??0,k.rollZ??-1)-p.yaw)*(1-rise):0,air?Math.sin(age*5)*.12:0);
  r.body.position.y=air?0:-.64*(1-rise)+(rolling?Math.sin(u*Math.PI)*.12:0);
  r.leftArm.rotation.set(rolling?-1.1:-.45,0,.7*(1-rise));r.rightArm.rotation.set(rolling?-1.1:-.45,0,-.7*(1-rise));r.leftElbow.rotation.x=r.rightElbow.rotation.x=rolling?-1.4:-.3;
  r.leftLeg.rotation.set(legsUp||rolling?-1.15:air?-.5:0,0,.12);r.rightLeg.rotation.set(legsUp||rolling?-.9:air?.3:0,0,-.12);r.leftKnee.rotation.x=legsUp||rolling?1.25:air?.65:.08+.85*rise*(1-rise);r.rightKnee.rotation.x=legsUp||rolling?1.1:air?.25:.08+.85*rise*(1-rise);r.head.rotation.x=air?-.3:rolling?.3:0;return pose;
 }
 if((p.hitStunUntil||0)>time){const flinch=Math.min(1,((p.hitStunUntil||0)-time)/.12);r.body.rotation.x=-.23*flinch;r.chest.rotation.y=.20*flinch;r.head.rotation.x=-.22;r.leftArm.rotation.set(-.5,0,.45);r.rightArm.rotation.set(-.5,0,-.45);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.8;return pose;}
 if(p.spec==='emperor'){animateEmperor(r,p,time);return pose;}
 if(p.spec==='ansatsuken'){animateAnsatsuken(r,p,time);return pose;}
 const aimTarget=w.class==='ranged'?(pose.shotAge<Math.min(.9,w.interval+.25)||p.reloadTime>0?1:run>.15?0:1):0;r.aimWeight+=(aimTarget-r.aimWeight)*(1-Math.exp(-dt*24));
 if(w.class!=='magic'&&r.weapon.parent!==r.sockets.rightHand)r.sockets.rightHand.add(r.weapon);
 if(w.class==='ranged'){
  const a=r.aimWeight,kick=pose.recoil*(p.spec==='rifle'?1.7:p.spec==='machinegun'?.65:1);
  blendRotation(r.rightArm,-1.0-pitch*.6-kick*.28,0,-.04,a);blendRotation(r.rightElbow,-.5+kick*.18,0,0,a);blendRotation(r.rightHand,-pitch*.4-kick*.12,0,0,a);
  r.weapon.rotation.x=T.MathUtils.lerp(.5,1.5,a);r.chest.rotation.x=-kick*.12*a;r.spine.rotation.y=-.15*a;r.head.rotation.z=-.08*a;
  if(p.spec==='pistol'){blendRotation(r.leftArm,-.6-pitch*.5,-.4,-.4,a*.75);blendRotation(r.leftElbow,-1.25,0,0,a*.75);}
  if(p.reloadTime>0){const u=clamp(1-p.reloadTime/w.reload,0,1),reach=Math.sin(Math.PI*clamp(u/.7,0,1));r.rightArm.rotation.set(-.65, .25, -.3);r.rightElbow.rotation.x=-.8;r.rightHand.rotation.z=-.28;r.weapon.rotation.x=1.45;r.leftArm.rotation.set(-.4-reach*.85,-.45,-.5);r.leftElbow.rotation.x=-.7-reach*.65;r.leftHand.rotation.z=reach*.8;r.chest.rotation.y=.25*pose.reload;r.head.rotation.x=.15*pose.reload;const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19-Math.sin(Math.PI*clamp((u-.16)/.52,0,1))*.3;}
  else {const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19;if(p.spec!=='pistol')braceWeapon(r,new T.Vector3(-.02,.12,.16-kick*.045),new T.Euler(-pitch-kick*.13,.12,0),a);}
 }else if(w.class==='magic'){
  // Staff stays in the support hand. The free hand coils above the shoulder,
  // then drives through the target; pelvis and chest lead the follow-through.
  if(r.weapon.parent!==r.sockets.leftHand)r.sockets.leftHand.add(r.weapon);
  const m=pose.magic,c=m.weight,water=p.spec==='water',dark=p.spec==='darkness',lightning=p.spec==='lightning',u=m.drive,load=m.coil;
  blendRotation(r.leftArm,-.24-pitch*.08,-.08,.16,.9);blendRotation(r.leftElbow,-.42,0,0,.9);r.leftHand.rotation.set(0,0,0);
  r.weapon.rotation.set(-Math.PI/2-r.leftArm.rotation.x-r.leftElbow.rotation.x,0,0);
  if(m.active){
   const twist=(m.age<m.windup?-.48*load:(-.48+.88*u)*m.settle)*(dark?1.15:water?1.1:1);
   r.pelvis.rotation.y+=twist*.35;r.spine.rotation.y+=twist*.65;r.chest.rotation.y+=twist*.48;r.head.rotation.y=-twist*.55;
   r.body.rotation.x+=(-.08*(1-u)+.17*u)*c;r.body.position.z=(-.065*(1-u)+.13*u)*c;
   r.rightShoulder.rotation.y=-.12*c;r.leftLeg.rotation.x-=.22*c;r.rightKnee.rotation.x+=.34*c;r.leftKnee.rotation.x+=.15*c;
   const arm=m.age<m.windup?-.3-(dark?1.65:1.35)*load:-(dark?1.95:1.65)+(lightning?.25:.18)*u-pitch*.65*u;
   const elbow=m.age<m.windup?-.3-1.65*load:-1.95+1.87*u;
   blendRotation(r.rightArm,arm,(water?-.5:.28)*(1-u),-(water?.65:.42)*(1-u)-.04*u,c);
   blendRotation(r.rightElbow,elbow,0,0,c);r.rightHand.rotation.set(-.18*u*c,(water?-.7:.45)*(1-u)*c,.45*(1-u)*c);
  }
 }else{meleeMotion(r,p,pose);}

 if(p.guarding||p.blockTime>0){r.spine.rotation.y=0;r.chest.rotation.set(-.06,0,0);r.leftArm.rotation.set(-1.4,-.25,.35);r.rightArm.rotation.set(-1.3,.25,-.35);r.leftElbow.rotation.x=-.8;r.rightElbow.rotation.x=-.8;r.leftHand.rotation.set(0,0,.25);r.rightHand.rotation.set(0,0,-.25);r.weapon.rotation.x=1.9;}
 return pose;
}

export function animateCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){const pose=sampleCharacter(r,p,time,dt,pitch);finishPose(r,p,time);return pose;}

/** Sandbox-only presentation sampler: never writes simulation, attacks, inputs, or health. */
export function animationPreviewPlayer(p:PlayerView,mode:string,time:number):PlayerView{
 const w=WEAPONS[p.spec],cycle=w.class==='melee'?Math.max(.75,w.interval+.45):w.class==='magic'?1.25:1.6,sampleAge=time%cycle;
 const out:PlayerView={...p,vel:{x:0,y:0,z:0},grounded:true,lastAttack:-10,lastShot:-10,blockTime:0,guarding:false,reloadTime:0,swingAt:undefined,swingUntil:undefined,attackRate:1,dashTime:0};
 if(mode==='walk'||mode==='run'){out.vel={x:Math.sin(p.yaw)*(mode==='walk'?2.8:6.8),y:0,z:Math.cos(p.yaw)*(mode==='walk'?2.8:6.8)};}
 if(['attack','windup','release','left-strike','right-strike'].includes(mode)){const age=mode==='windup'?(w.class==='magic'?castWindup(p.spec)*.58:Math.max(.01,w.windup*.92)):mode==='release'?(w.class==='magic'?castWindup(p.spec)+.025:w.windup+.10):mode.endsWith('strike')?w.windup+.10:sampleAge;out.attackIndex=mode==='left-strike'?2:mode==='right-strike'?1:Math.floor(time/cycle)+1;out.lastAttack=time-age;out.lastShot=out.lastAttack+castWindup(p.spec);out.swingAt=out.lastAttack+w.windup;out.swingUntil=out.swingAt+.16;}
 if(mode==='reload')out.reloadTime=w.reload?Math.max(.01,w.reload-time%w.reload):0;
 if(mode==='block')out.blockTime=.3;
 if(mode==='jump'){out.grounded=false;out.vel.y=4;}
 return out;
}
