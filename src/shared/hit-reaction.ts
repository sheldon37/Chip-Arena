import {clamp,sub,v,type Vec} from './config.js';

/** Captured in the victim's facing frame so camera movement cannot reverse a recoil. */
export interface HitReaction {
 kind:'hit'|'parry';at:number;until:number;yaw:number;direction:Vec;point:Vec;
 zone:'head'|'torso'|'legs'|'left-arm'|'right-arm';turn:number;strength:number;
}
export function impactFacing(p:{input:{yaw:number};hitReaction?:HitReaction;hitStunUntil?:number;attackCommitment?:{yaw:number;until:number}},time:number){
 return p.hitReaction&&(p.hitStunUntil||0)>time?p.hitReaction.yaw:p.attackCommitment&&p.attackCommitment.until>time?p.attackCommitment.yaw:p.input.yaw;
}
export function hitReaction(pos:Vec,yaw:number,direction:Vec,point:Vec,time:number,damage:number,headshot=false):HitReaction {
 const local=(a:Vec)=>v(a.x*Math.cos(yaw)-a.z*Math.sin(yaw),a.y,a.x*Math.sin(yaw)+a.z*Math.cos(yaw));
 const hit=local(sub(point,pos)),force=local(direction),horizontal=Math.hypot(force.x,force.z);
 if(horizontal>.001){force.x/=horizontal;force.z/=horizontal;}
 const torque=hit.z*force.x-hit.x*force.z;
 return {kind:'hit',at:time,until:time+.24,yaw,direction:force,point:hit,zone:headshot||hit.y>.43?'head':hit.y<-.27?'legs':Math.abs(hit.x)>.24?(hit.x>0?'left-arm':'right-arm'):'torso',turn:clamp(torque*2.2+force.x*.18,-.7,.7),strength:clamp(.45+damage/70,.45,1)};
}
export function parryReaction(pos:Vec,yaw:number,direction:Vec,time:number,until:number):HitReaction {
 return {...hitReaction(pos,yaw,direction,pos,time,40),kind:'parry',until,turn:-.35};
}
