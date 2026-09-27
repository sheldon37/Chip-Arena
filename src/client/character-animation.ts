import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {clamp} from '../shared/config.js';

export const ANIMATION={blendSpeed:14,stridePerMetre:2.65,runLean:.16,deathSeconds:5,deathVoxels:100,reducedDeathVoxels:40,maxDeathVoxels:240} as const;
export interface AnimatedRig {body:T.Group;head:T.Group;leftArm:T.Group;rightArm:T.Group;leftElbow:T.Group;rightElbow:T.Group;leftLeg:T.Group;rightLeg:T.Group;leftKnee:T.Group;rightKnee:T.Group;weapon:T.Group;stride:number;motion:number;}
export function attackPose(p:PlayerView,time:number){
 const w=WEAPONS[p.spec],rate=p.attackRate||1,age=Math.max(0,time-p.lastAttack),windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 const active=w.class==='melee'&&age<windup+window+.25/rate;
 const load=active&&age<windup?Math.sin(clamp(age/windup,0,1)*Math.PI/2):0;
 const strike=active&&age>=windup?clamp((age-windup)/Math.max(.025,window),0,1):0;
 const recovery=active&&age>=windup+window?1-clamp((age-windup-window)/(.25/rate),0,1):1;
 const swing=active?(age<windup?-.9*load:(-.9+strike*2.5)*recovery):0;
 const shotAge=time-(p.lastShot??p.lastAttack),recoil=shotAge>=0?Math.exp(-shotAge*22):0;
 const cast=w.class==='magic'&&age<.7/rate?Math.sin(clamp(age/(.7/rate),0,1)*Math.PI):0;
 const reload=p.reloadTime>0?Math.sin(clamp(1-p.reloadTime/Math.max(.1,w.reload),0,1)*Math.PI):0;
 return {load,strike,swing,recoil,cast,reload,active};
}

/** Presentation consumes authoritative attack times; it never changes combat timing. */
export function animateCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){
 const w=WEAPONS[p.spec],speed=Math.hypot(p.vel.x,p.vel.z),target=p.grounded?clamp(speed/6.8,0,1):0;
 r.motion+=(target-r.motion)*(1-Math.exp(-ANIMATION.blendSpeed*dt));r.stride+=Math.min(speed,12)*dt*ANIMATION.stridePerMetre;
 const gait=Math.sin(r.stride)*.78*r.motion,run=r.motion,pose=attackPose(p,time),air=p.grounded?0:clamp(Math.abs(p.vel.y)/5,0,1);
 r.body.position.z=0;
 r.body.position.y=Math.abs(Math.cos(r.stride))*.045*run+Math.sin(time*2.3)*.008*(1-run);
 r.body.rotation.set(run*ANIMATION.runLean+(p.dashTime? .25:0),pose.swing*-.16,Math.sin(r.stride)*.035*run);
 r.head.rotation.set(-pitch*.55-r.body.rotation.x*.5,-pose.swing*.1,0);
 r.leftLeg.rotation.set(gait-.32*air,0,0);r.rightLeg.rotation.set(-gait-.32*air,0,0);
 r.leftKnee.rotation.x=Math.max(0,-gait)*.9+.65*air;r.rightKnee.rotation.x=Math.max(0,gait)*.9+.65*air;
 r.leftArm.rotation.set(-gait*.6,0,.07);r.rightArm.rotation.set(-.2+gait*.28,0,-.06);
 r.leftElbow.rotation.x=-.22;r.rightElbow.rotation.x=-.22;
 r.weapon.position.set(0,-.23,.12);r.weapon.rotation.set(.12,0,0);r.weapon.scale.set(1,1,w.class==='melee'?p.reach/w.range:1);
 if(w.class==='ranged'){
  // Both arms brace the gun; the muzzle remains forward in the standard hand socket.
  r.rightArm.rotation.set(-1.05-pitch*.45-pose.recoil*.2,0,-.05);r.rightElbow.rotation.x=-.28+pose.recoil*.08;
  r.leftArm.rotation.set(-.88-pitch*.4,-.4,-.35);r.leftElbow.rotation.x=-.8;
  r.weapon.rotation.x=1.3+pose.recoil*.1;r.weapon.position.z=.1-pose.recoil*.05;
  if(p.reloadTime>0){r.rightArm.rotation.x=-.55;r.rightArm.rotation.z=-.22-pose.reload*.3;r.leftArm.rotation.set(-.55-pose.reload*.7,-.2,-.4);r.leftElbow.rotation.x=-1.2*pose.reload;r.body.rotation.y=.12;}
 }else if(w.class==='magic'){
  r.rightArm.rotation.set(-.7-pose.cast*.55-pitch*.4,pose.cast*.2,-.12);r.rightElbow.rotation.x=-.25-pose.cast*.35;r.weapon.rotation.x=.95+pose.cast*.7;
  r.leftArm.rotation.set(-.3-pose.cast*1.05,-pose.cast*.5,.18+pose.cast*.4);r.leftElbow.rotation.x=-.25-pose.cast*.65;
  r.body.rotation.y=pose.cast*-.2;
 }else{
  const heavy=p.spec==='greatsword',dagger=p.spec==='daggers',shield=p.spec==='shield';
  r.rightArm.rotation.set(-.55-pose.load*(heavy?1.45:.7),pose.swing*(dagger?.75:1),-.18-pose.load*.35);
  r.rightElbow.rotation.x=-.25-pose.load*.7;r.weapon.rotation.x=.8+pose.load*.8;
  r.leftArm.rotation.x=-gait*.55-pose.load*.55;r.leftElbow.rotation.x=-.3;
  if(heavy){r.leftArm.rotation.set(r.rightArm.rotation.x+.1,pose.swing*.6,-.5);r.leftElbow.rotation.x=-.65;r.body.rotation.x+=pose.strike*.14;}
  if(shield){r.rightArm.rotation.set(-.8-pose.strike*.5,0,-.2);r.weapon.rotation.x=1.05;r.weapon.position.z=.1+pose.strike*.18;r.body.position.z=pose.strike*.08;}else r.body.position.z=0;
 }
 if(p.guarding||p.blockTime>0){r.leftArm.rotation.set(-1.4,-.25,.25);r.rightArm.rotation.set(-1.2,.25,-.25);r.leftElbow.rotation.x=-.7;r.rightElbow.rotation.x=-.7;r.weapon.rotation.x=1.5;}
 return pose;
}
