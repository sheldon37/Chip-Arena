import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {foot,STANDING_REACH} from './rig-ik.js';
import {smooth} from './intro-curves.js';
/** World-planted footwork for the hand-authored (developer) intros, the same model as the weapon intros: a foot keeps
 * its world anchor while the root moves and turns, and a step travels on a lift arc with toe-off and heel strike. A step
 * with h=0 is a pivot on the ball of the foot (heel raised, toes swivel, the ankle stays on its spot). Pure functions of
 * time: anchors are constants or come from the caller's own pure schedule. */
export interface Plant{x:number;z:number;yaw:number}
export interface IntroStep extends Plant{lift:number;land:number;h:number}
export interface FootPose extends Plant{y:number;heel:number;weight:number}
const FLOOR=-.795,LEG_REACH=.485;
const _hip=new T.Vector3(),_targets=[new T.Vector3(),new T.Vector3()];
/** Residual toe-up after a heel strike at `landed`, flattening over .14 s. */
const strike=(t:number,landed:number)=>landed<0||t>=landed+.14?0:-.22*(1-smooth((t-landed)/.14));
const wrap=(a:number)=>Math.atan2(Math.sin(a),Math.cos(a));
export function footAt(t:number,start:Plant,steps:readonly IntroStep[]):FootPose{
 let anchor:Plant=start,heel=0,lastLand=-1;
 for(const step of steps){
  if(t>=step.land){anchor=step;if(step.h>0)lastLand=step.land;continue;}
  if(t>=step.lift){const u=(t-step.lift)/(step.land-step.lift),e=smooth(u),turn=wrap(step.yaw-anchor.yaw);
   if(step.h<=0)return {x:anchor.x+(step.x-anchor.x)*e,z:anchor.z+(step.z-anchor.z)*e,yaw:anchor.yaw+turn*e,y:0,heel:.32*Math.sin(Math.PI*u)+strike(t,lastLand),weight:1};
   const from=.3+strike(step.lift,lastLand);
   return {x:anchor.x+(step.x-anchor.x)*e,z:anchor.z+(step.z-anchor.z)*e,yaw:anchor.yaw+turn*e,y:4*step.h*u*(1-u),heel:from+(-.22-from)*smooth(u*1.15),weight:u<.5?1-smooth(u/.3):smooth((u-.7)/.3)};}
  if(step.h>0&&t>step.lift-.1)heel=.3*smooth((t-step.lift+.1)/.1);
  break;}
 heel+=strike(t,lastLand);
 return {x:anchor.x,z:anchor.z,yaw:anchor.yaw,y:0,heel,weight:1};
}
/** Plant both feet for a root at (x,z,yaw) in world space: the hips shift toward the supporting foot (scaled by `shift`),
 * the pelvis drops just enough that every loaded leg reaches its anchor (never a knee locked straight), then both legs are
 * solved onto their targets. Call after the body, pelvis and spine are posed. Returns the applied hip shift. */
export function plantFeet(r:CharacterRig,feet:readonly [FootPose,FootPose],root:{x:number;z:number;yaw:number},shift=.4){
 const c=Math.cos(root.yaw),s=Math.sin(root.yaw);
 feet.forEach((f,i)=>{const dx=f.x-root.x,dz=f.z-root.z;_targets[i].set(dx*c-dz*s,FLOOR+f.y+(f.heel>0?.08*Math.sin(f.heel):.05*Math.sin(-f.heel)),dx*s+dz*c);});
 const total=feet[0].weight+feet[1].weight,support=(_targets[0].x*feet[0].weight+_targets[1].x*feet[1].weight)/Math.max(.01,total),hips=T.MathUtils.clamp(support*shift,-.07,.07);
 r.body.position.x+=hips;r.pelvis.rotation.z+=hips*.7;r.chest.rotation.z-=hips*.8;
 r.body.updateMatrix();r.pelvis.updateMatrix();let deficit=-1;
 feet.forEach((f,i)=>{const leg=i===0?r.leftLeg:r.rightLeg,p=_targets[i];_hip.copy(leg.position).applyMatrix4(r.pelvis.matrix).applyMatrix4(r.body.matrix);const dh=Math.hypot(p.x-_hip.x,p.z-_hip.z),allowed=Math.sqrt(Math.max(0,(LEG_REACH-.005)**2-dh*dh));deficit=Math.max(deficit,f.weight*(_hip.y-(p.y+allowed)));});
 r.body.position.y-=Math.min(.32,(deficit+Math.sqrt(deficit*deficit+.0009))/2);
 feet.forEach((f,i)=>foot(r,i===0,_targets[i],f.yaw-root.yaw,f.heel,1,undefined,STANDING_REACH));
 return hips;
}
/** Group-local stance offset (x,z,yaw) to a world plant for a root at (x,z,yaw). */
export function plantOf(root:{x:number;z:number;yaw:number},local:Plant):Plant{
 const c=Math.cos(root.yaw),s=Math.sin(root.yaw);return {x:root.x+local.x*c+local.z*s,z:root.z-local.x*s+local.z*c,yaw:root.yaw+local.yaw};
}
