import RAPIER from '@dimforge/rapier3d-compat';
import {CONFIG as C,Input,Vec,v,add,mul,len,norm,forward,clamp,sub} from './config.js';
/** Identical locomotion integration for authority and client replay. Gravity and
 * contact response belong to Rapier, not a separate client character controller. */
export interface Motion {move:Vec;ext:Vec;dashCd:number;dashTime:number;grounded:boolean;}
export interface MotionRules {speed:number;dashCooldown:number;knockbackTaken:number;jumpPower?:number;playerGravity?:number;}
export function prepareMotion(world:RAPIER.World,body:RAPIER.RigidBody,collider:RAPIER.Collider,state:Motion,input:Input,stats:MotionRules,dt:number,guarding=false,swinging=false){
 state.dashCd=Math.max(0,state.dashCd-dt);state.dashTime=Math.max(0,state.dashTime-dt);
 body.setGravityScale(stats.playerGravity??1,true);const pos=body.translation();state.grounded=!!world.castRay(new RAPIER.Ray(pos,v(0,-1,0)),C.playerHalfHeight+C.playerRadius+.16,true,undefined,undefined,collider);
 let direction=v(input.x,0,input.z);if(len(direction)>1)direction=norm(direction);
 const dashed=input.dash&&state.dashCd<=0;
 if(dashed){state.dashCd=Math.max(.5,C.dashCooldown*stats.dashCooldown);state.dashTime=C.dashDuration;state.ext=add(state.ext,mul(len(direction)>.1?norm(direction):forward(input.yaw),C.dashSpeed));}
 const speed=C.speed*stats.speed*(guarding?.45:1)*(swinging?.85:1),desired=mul(direction,speed);
 const approach=(a:number,b:number)=>a+clamp(b-a,-C.acceleration*dt,C.acceleration*dt);
 state.move=v(approach(state.move.x,desired.x),0,approach(state.move.z,desired.z));
 // External Y is a one-shot kick, not an acceleration spread over many frames.
 const lift=clamp(state.ext.y,-24,24);state.ext.y=0;
 if(state.dashTime<=0)state.ext=mul(state.ext,Math.exp(-C.externalDrag*dt));
 const externalSpeed=Math.hypot(state.ext.x,state.ext.z);
 if(externalSpeed>C.maxExternalSpeed)state.ext=mul(state.ext,C.maxExternalSpeed/externalSpeed);
 // Counter-steering softens a launch without instantly cancelling it; lateral input stays responsive.
 let controlled=state.move;
 if(state.dashTime<=0&&externalSpeed>.01){const away=norm(state.ext),against=Math.min(0,state.move.x*away.x+state.move.z*away.z),cancel=against*(1-1/(1+(externalSpeed/4)**2));controlled=add(state.move,mul(away,-cancel));}
 let vy=body.linvel().y;const jumped=input.jump&&state.grounded;
 if(jumped){vy=C.jumpSpeed*(stats.jumpPower??1);state.grounded=false;}
 body.setLinvel(v(controlled.x+state.ext.x,vy+lift,controlled.z+state.ext.z),true);
 return {dashed,jumped};
}
export function finishMotion(state:Motion,before:Vec,after:Vec,knockbackTaken:number,dt:number){
 const delta=sub(after,before);state.ext.x+=delta.x*knockbackTaken;state.ext.z+=delta.z*knockbackTaken;state.ext.y*=Math.exp(-5*dt);
}
