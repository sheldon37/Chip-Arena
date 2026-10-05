import {inLava,LAVA_SURFACE} from './stage-hazards.js';
import {rollVelocity,type RollState} from './souls-combat.js';
import RAPIER from '@dimforge/rapier3d-compat';
import {CONFIG as C,Input,Vec,v,add,mul,len,norm,forward,clamp,sub} from './config.js';
/** Identical locomotion integration for authority and client replay. Gravity and
 * contact response belong to Rapier, not a separate client character controller. */
export interface Motion {electricStepTime?:number;waveTime?:number;move:Vec;ext:Vec;dashCd:number;dashTime:number;grounded:boolean;}
/** Forward sprint (owner 2026-10-04: double-tap W). speed multiplies run speed; forwardDot is the cos of the widest
 * travel angle from the aim that still sprints (W+A/D diagonals sprint, strafes and backpedals do not). No stamina cost. */
export const SPRINT={speed:1.35,forwardDot:.6,doubleTapMs:300} as const;
/** The held sprint flag while travelling forward of the aim (shared by authority and prediction). */
export function sprintIntent(input:Input){if(input.sprint!==true||input.attack||input.block||input.ability)return false;const l=Math.hypot(input.x,input.z);if(l<.5)return false;const f=forward(input.yaw);return (input.x*f.x+input.z*f.z)/l>=SPRINT.forwardDot;}
export interface MotionRules {/** Sprint speed multiplier when allowed this tick (undefined: no sprint). */sprint?:number;time?:number;roll?:RollState;locomotionLock?:boolean;attackStep?:Vec;map?:unknown;recoveryRoll?:Vec;electricStepSpeed?:number;dashSpeed?:number;dashDuration?:number;groundedHint?:boolean;waveSpeed?:number;maxExternalSpeed?:number;maxExternalLift?:number;groundGroups?:number;groundRay?:number;speed:number;dashCooldown:number;knockbackTaken:number;jumpPower?:number;playerGravity?:number;}
export function prepareMotion(world:RAPIER.World,body:RAPIER.RigidBody,collider:RAPIER.Collider,state:Motion,input:Input,stats:MotionRules,dt:number,guarding=false,swinging=false,stunned=false){
 state.dashCd=Math.max(0,state.dashCd-dt);state.dashTime=Math.max(0,state.dashTime-dt);
 body.setGravityScale(stats.playerGravity??1,true);const pos=body.translation();state.grounded=stats.groundedHint??!!world.castRay(new RAPIER.Ray(pos,v(0,-1,0)),stats.groundRay??C.playerHalfHeight+C.playerRadius+.16,true,undefined,stats.groundGroups,collider);
 const swimming=inLava(stats.map,pos);if(swimming){if(body.linvel().y<=2)body.setGravityScale(0,true);state.grounded=false;}
 state.waveTime=Math.max(0,(state.waveTime||0)-dt);if(stunned){state.electricStepTime=0;state.move=v();state.dashTime=0;state.waveTime=0;}
 const roll=!stunned&&stats.roll&&(stats.time??0)>=stats.roll.at&&(stats.time??0)<stats.roll.until?stats.roll:undefined;
 const locked=stats.locomotionLock||!!roll;
 if(locked){state.move=v();input={...input,x:0,z:0,jump:false,dash:false,secondary:false};}
 let direction=stunned?v():v(input.x,0,input.z);if(len(direction)>1)direction=norm(direction);
 const waved=!locked&&!stunned&&!guarding&&!!stats.waveSpeed&&input.secondary&&state.grounded&&state.waveTime<=0;
 if(waved){state.waveTime=.18;state.dashTime=.18;state.ext=add(mul(forward(input.yaw),stats.waveSpeed!),v(0,state.ext.y,0));}
 const dashed=!locked&&!waved&&!stunned&&input.dash&&state.dashCd<=0;
 if(dashed){state.dashCd=Math.max(.5,C.dashCooldown*stats.dashCooldown);state.dashTime=stats.dashDuration??C.dashDuration;state.ext=add(state.ext,mul(len(direction)>.1?norm(direction):forward(input.yaw),stats.dashSpeed??C.dashSpeed));}
 const sprinting=!locked&&!stunned&&!guarding&&!swinging&&!swimming&&!!stats.sprint&&sprintIntent(input);
 const speed=C.speed*stats.speed*(swimming?.6:1)*(guarding?.45:1)*(swinging?.85:1)*(sprinting?stats.sprint!:1),desired=mul(direction,speed);
 const approach=(a:number,b:number)=>a+clamp(b-a,-C.acceleration*dt,C.acceleration*dt);
 state.move=v(approach(state.move.x,desired.x),0,approach(state.move.z,desired.z));
 // External Y is a one-shot kick, not an acceleration spread over many frames.
 const lift=clamp(state.ext.y,-(stats.maxExternalLift??24),stats.maxExternalLift??24);state.ext.y=0;
 if(roll||state.dashTime<=0)state.ext=mul(state.ext,Math.exp(-C.externalDrag*dt));
 const externalSpeed=Math.hypot(state.ext.x,state.ext.z);
 const limit=stats.maxExternalSpeed??C.maxExternalSpeed;if(externalSpeed>limit)state.ext=mul(state.ext,limit/externalSpeed);
 // Counter-steering softens a launch without instantly cancelling it; lateral input stays responsive.
 let controlled=state.move;
 if(state.dashTime<=0&&externalSpeed>.01){const away=norm(state.ext),against=Math.min(0,state.move.x*away.x+state.move.z*away.z),cancel=against*(1-1/(1+(externalSpeed/4)**2));controlled=add(state.move,mul(away,-cancel));}
 let vy=body.linvel().y;const jumped=!stunned&&input.jump&&(state.grounded||swimming&&vy<2);
 if(swimming&&vy<=2)vy+=clamp((LAVA_SURFACE+.15-pos.y)*12-vy*4,-35,35)*dt;
 if(jumped){vy=(swimming?10:C.jumpSpeed)*(stats.jumpPower??1);state.grounded=false;}
 const electricStep=!stunned&&!guarding&&state.grounded&&(state.electricStepTime||0)>1e-6?mul(forward(input.yaw),stats.electricStepSpeed||0):v();
 state.electricStepTime=Math.max(0,(state.electricStepTime||0)-dt);
 if(stats.recoveryRoll){state.move=v();state.ext=v();body.setLinvel(v(stats.recoveryRoll.x,vy,stats.recoveryRoll.z),true);return {dashed:false,jumped:false,waved:false,sprinting:false};}
 const root=roll?rollVelocity(roll,stats.time??0,dt):!stunned&&state.grounded?stats.attackStep||v():v();
 if(roll)state.dashTime=Math.max(0,roll.until-(stats.time??0));
 body.setLinvel(v(controlled.x+state.ext.x+electricStep.x+root.x,vy+lift,controlled.z+state.ext.z+electricStep.z+root.z),true);
 return {dashed,jumped,waved,sprinting};
}
export function finishMotion(state:Motion,before:Vec,after:Vec,knockbackTaken:number,dt:number){
 const delta=sub(after,before);state.ext.x+=delta.x*knockbackTaken;state.ext.z+=delta.z*knockbackTaken;state.ext.y*=Math.exp(-5*dt);
}
