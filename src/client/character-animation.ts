import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
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
 const cast=w.class==='magic'&&age<.7/rate?Math.sin(clamp(age/(.7/rate),0,1)*Math.PI):0;
 const reload=p.reloadTime>0?Math.sin(clamp(1-p.reloadTime/Math.max(.1,w.reload),0,1)*Math.PI):0;
 return {load,strike,swing,recoil,cast,reload,active,age,shotAge,windup,window,recovery};
}
function blendRotation(node:T.Object3D,x:number,y:number,z:number,weight:number){node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),clamp(weight,0,1));}
/** Distance-driven locomotion under action layers. Authoritative attack timestamps keep the hit phase aligned. */
export function animateCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){
 const w=WEAPONS[p.spec],speed=Math.hypot(p.vel.x,p.vel.z),target=p.grounded?clamp(speed/6.8,0,1.25):0;
 r.motion+=(target-r.motion)*(1-Math.exp(-ANIMATION.blendSpeed*dt));r.stride+=Math.min(speed,15)*dt*ANIMATION.stridePerMetre;
 const run=r.motion,gait=Math.sin(r.stride),opposite=-gait,pose=attackPose(p,time),forward=(p.vel.x*Math.sin(p.yaw)+p.vel.z*Math.cos(p.yaw))/Math.max(.1,speed),strafe=(p.vel.x*Math.cos(p.yaw)-p.vel.z*Math.sin(p.yaw))/Math.max(.1,speed),travel=forward<-.2?-1:1;
 if(p.grounded&&!r.wasGrounded)r.land=.10;r.wasGrounded=p.grounded;r.land*=Math.exp(-dt*15);r.groundWeight+=(Number(p.grounded)-r.groundWeight)*(1-Math.exp(-dt*12));const air=1-r.groundWeight;
 r.body.position.set(0,Math.abs(Math.cos(r.stride))*.04*run+Math.sin(time*2.3)*.006*(1-Math.min(run,1))-r.land,0);r.body.rotation.set(run*.13*forward+(p.dashTime?.22:0),0,-strafe*run*.08);
 r.pelvis.rotation.set(0,gait*.13*run,-gait*.055*run);r.spine.rotation.set(run*.07, -gait*.12*run,0);r.chest.rotation.set(0,-gait*.11*run,0);r.neck.rotation.set(0,0,0);r.head.rotation.set(-pitch*.55-r.body.rotation.x*.6,gait*.08*run,0);
 r.leftLeg.rotation.set(gait*.86*run*travel-.45*air,0,strafe*.23*run);r.rightLeg.rotation.set(opposite*.86*run*travel+.15*air,0,strafe*.23*run);
 r.leftKnee.rotation.set(Math.max(0,-gait)*1.1*run+.8*air,0,0);r.rightKnee.rotation.set(Math.max(0,gait)*1.1*run+.35*air,0,0);
 r.leftFoot.rotation.set(-r.leftLeg.rotation.x*.45-r.leftKnee.rotation.x*.65,0,0);r.rightFoot.rotation.set(-r.rightLeg.rotation.x*.45-r.rightKnee.rotation.x*.65,0,0);r.leftToe.rotation.x=Math.max(0,gait)*.35*run;r.rightToe.rotation.x=Math.max(0,-gait)*.35*run;
 r.leftShoulder.rotation.set(0,0,.025);r.rightShoulder.rotation.set(0,0,-.025);
 // Unarmed travel remains visible with every class: large opposing shoulder swings, elbows and wrist lag.
 r.leftArm.rotation.set(-gait*.95*run-.12,0,.10+air*.25);r.rightArm.rotation.set(gait*.9*run-.16,0,-.10-air*.25);
 r.leftElbow.rotation.set(-.22-Math.max(0,gait)*.5*run,0,0);r.rightElbow.rotation.set(-.25-Math.max(0,-gait)*.5*run,0,0);
 r.leftHand.rotation.set(Math.sin(r.stride-.5)*.13*run,0,.06);r.rightHand.rotation.set(-Math.sin(r.stride-.5)*.13*run,0,-.06);
 r.weapon.position.set(0,0,0);r.weapon.rotation.set(.5,0,0);r.weapon.scale.set(1,1,w.class==='melee'?p.reach/w.range:1);r.offWeapon.rotation.set(.55,0,0);r.offWeapon.scale.copy(r.weapon.scale);
 const aimTarget=w.class==='ranged'?(pose.shotAge<Math.min(.9,w.interval+.25)||p.reloadTime>0?1:run>.15?0:1):0;r.aimWeight+=(aimTarget-r.aimWeight)*(1-Math.exp(-dt*24));
 if(w.class==='ranged'){
  const a=r.aimWeight,kick=pose.recoil*(p.spec==='rifle'?1.7:p.spec==='machinegun'?.65:1);
  blendRotation(r.rightArm,-1.0-pitch*.6-kick*.28,0,-.04,a);blendRotation(r.rightElbow,-.5+kick*.18,0,0,a);blendRotation(r.rightHand,-pitch*.4-kick*.12,0,0,a);
  r.weapon.rotation.x=T.MathUtils.lerp(.5,1.5,a);r.chest.rotation.x=-kick*.12*a;r.spine.rotation.y=-.15*a;r.head.rotation.z=-.08*a;
  if(p.spec==='pistol'){blendRotation(r.leftArm,-.6-pitch*.5,-.4,-.4,a*.75);blendRotation(r.leftElbow,-1.25,0,0,a*.75);}
  if(p.reloadTime>0){const u=clamp(1-p.reloadTime/w.reload,0,1),reach=Math.sin(Math.PI*clamp(u/.7,0,1));r.rightArm.rotation.set(-.65, .25, -.3);r.rightElbow.rotation.x=-.8;r.rightHand.rotation.z=-.28;r.weapon.rotation.x=1.45;r.leftArm.rotation.set(-.4-reach*.85,-.45,-.5);r.leftElbow.rotation.x=-.7-reach*.65;r.leftHand.rotation.z=reach*.8;r.chest.rotation.y=.25*pose.reload;r.head.rotation.x=.15*pose.reload;const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19-Math.sin(Math.PI*clamp((u-.16)/.52,0,1))*.3;}
  else {const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19;if(p.spec!=='pistol')braceWeapon(r,new T.Vector3(-.02,.12,.16-kick*.045),new T.Euler(-pitch-kick*.13,.12,0),a);}
 }else if(w.class==='magic'){
  const casting=Math.max(pose.cast,Math.exp(-pose.age*13)),c=clamp(casting,0,1),water=p.spec==='water',dark=p.spec==='darkness',lightning=p.spec==='lightning';
  if(pose.age<.8/(p.attackRate||1)){
   r.spine.rotation.y=-c*(dark?.4:.25);r.chest.rotation.y=c*(water?.4:.18);r.body.position.z=c*.075;r.body.rotation.x+=c*.09;
   blendRotation(r.rightArm,-1.25-pitch*.5-c*(lightning?.4:.2),c*(water?-.45:.18),-c*.2,c);blendRotation(r.rightElbow,-.3-c*.3,0,0,c);r.weapon.rotation.x=.9+c*.7;
   blendRotation(r.leftArm,-.5-c*(dark?.65:1.0),-c*(water?.75:.35),c*(dark?.75:.38),c);r.leftElbow.rotation.x=-.25-c*(lightning?.25:.7);r.leftHand.rotation.set(-c*.3,c*.7,c*.45);r.rightHand.rotation.y=c*(dark?-.5:.2);r.head.rotation.y=c*.18;
  }
 }else{
  const heavy=p.spec==='greatsword',dagger=p.spec==='daggers',shield=p.spec==='shield';
  if(pose.active){const wind=pose.load,hit=smooth(pose.strike),recover=pose.recovery,action=pose.age<pose.windup?wind:recover,sw=(pose.age<pose.windup?-wind:-1+2.3*hit)*recover;
   r.pelvis.rotation.y=-sw*(heavy?.24:.16);r.spine.rotation.y=sw*(heavy?.54:.37);r.chest.rotation.y=sw*.34;r.head.rotation.y=-sw*.3;r.body.rotation.x+=(shield?hit*.18:heavy?hit*.21:hit*.07)*recover;r.body.position.z=(shield?.18:.10)*hit*recover;
   r.leftLeg.rotation.x-=action*.17;r.rightLeg.rotation.x+=action*.22;r.leftKnee.rotation.x+=action*.25;r.rightKnee.rotation.x+=action*.32;
   if(shield){r.rightArm.rotation.set(-.85-hit*.5,-.08,-.17);r.rightElbow.rotation.x=-.95+hit*.8;r.rightHand.rotation.set(0,0,0);r.weapon.rotation.x=1.7;r.leftArm.rotation.set(.25+hit*.6,0,.25);}
   else if(dagger){r.rightArm.rotation.set(-.9-hit*.3,sw*.5,-.22);r.rightElbow.rotation.x=-1.35+hit*1.15;r.weapon.rotation.x=1.4;r.leftArm.rotation.set(-.8+hit*.3,-sw*.5,.22);r.leftElbow.rotation.x=-.3-hit*.9;r.offWeapon.rotation.x=1.15;r.rightHand.rotation.y=-sw*.25;}
   else{r.rightArm.rotation.set(-.95-action*(heavy?1.05:.45)+hit*.7,sw*(heavy?.5:.8),-.15-action*.45);r.rightElbow.rotation.x=-.6-wind*.6+hit*.25;r.weapon.rotation.x=1.4+wind*.3;r.rightHand.rotation.set(hit*.28,sw*.18,sw*.18);r.leftArm.rotation.set(.3+sw*.4,0,.35+action*.2);r.leftElbow.rotation.x=-.6;if(heavy){braceWeapon(r,new T.Vector3(-.03,.12+wind*.32,.22+hit*.07),new T.Euler(-wind*1.35+hit*.5,sw*.55,sw*.3),action);}}
  }else {r.weapon.rotation.x=.3;if(dagger){r.leftArm.rotation.x-=.15;r.offWeapon.rotation.x=.3;}}
 }
 if(p.guarding||p.blockTime>0){r.spine.rotation.y=0;r.chest.rotation.set(-.06,0,0);r.leftArm.rotation.set(-1.4,-.25,.35);r.rightArm.rotation.set(-1.3,.25,-.35);r.leftElbow.rotation.x=-.8;r.rightElbow.rotation.x=-.8;r.leftHand.rotation.set(0,0,.25);r.rightHand.rotation.set(0,0,-.25);r.weapon.rotation.x=1.9;}
 return pose;
}

/** Sandbox-only presentation sampler: never writes simulation, attacks, inputs, or health. */
export function animationPreviewPlayer(p:PlayerView,mode:string,time:number):PlayerView{
 const w=WEAPONS[p.spec],cycle=w.class==='melee'?Math.max(.75,w.interval+.45):w.class==='magic'?1.25:1.6,age=time%cycle;
 const out:PlayerView={...p,vel:{x:0,y:0,z:0},grounded:true,lastAttack:-10,lastShot:-10,blockTime:0,guarding:false,reloadTime:0,swingAt:undefined,swingUntil:undefined,attackRate:1,dashTime:0};
 if(mode==='walk'||mode==='run'){out.vel={x:Math.sin(p.yaw)*(mode==='walk'?2.8:6.8),y:0,z:Math.cos(p.yaw)*(mode==='walk'?2.8:6.8)};}
 if(mode==='attack'){out.lastAttack=time-age;out.lastShot=out.lastAttack;out.swingAt=out.lastAttack+w.windup;out.swingUntil=out.swingAt+.16;}
 if(mode==='reload')out.reloadTime=w.reload?Math.max(.01,w.reload-time%w.reload):0;
 if(mode==='block')out.blockTime=.3;
 if(mode==='jump'){out.grounded=false;out.vel.y=4;}
 return out;
}
