import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {foot,hand} from './primary-melee-motion.js';
import {poseCurve} from './pose-curves.js';
import {smoothMotion as ease} from './motion-quality.js';

/** Impact drives the torso first, then head/arms lag, knees catch the weight and the guard recovers.
 * Every phase fits within the authoritative stun; presentation never extends the lockout. */
export function hitReactionMotion(r:CharacterRig,p:PlayerView,time:number,stunned=false){
 const h=p.hitReaction;if(!h)return false;const until=stunned?Math.max(h.until,p.hitStunUntil||0):h.until;if(time<h.at||time>=until)return false;
 const duration=Math.max(.04,until-h.at),age=time-h.at,u=age/duration;
 // A short, readable recoil with an overshoot and a smaller rebound, rather than a frozen target pose.
 const rise=Math.min(.095,duration*.26),peak=rise/duration;
 const envelope=(delay:number)=>{
  const t=Math.max(0,age-delay)/duration;
  return poseCurve([0,peak,Math.max(peak+.06,.40),.65,1],[0,1.12,.78,.68,0],t);
 };
 const w=envelope(0)*(stunned?1:.35),limb=envelope(Math.min(.025,duration*.06))*(stunned?1:.35)*(1-ease((u-.78)/.22)),headWeight=envelope(Math.min(.016,duration*.04))*(stunned?1:.35)*(1-ease((u-.83)/.17)),d=h.direction;
 const blend=(node:T.Object3D,x:number,y:number,z:number,weight:number)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),T.MathUtils.clamp(weight,0,1));
 if(h.kind==='parry'){
  const slump=ease((u-.18)/.25),rebound=1-.55*slump;
  r.body.position.y-=.12*w;r.body.position.z-=.065*w;
  r.body.rotation.x+=(-.30*rebound+.16*slump)*w;r.body.rotation.y+=h.turn*w;r.chest.rotation.y-=.22*w;r.head.rotation.x+=(-.28*rebound+.20*slump)*w;
  // Weapon hand is thrown high and wide, then hangs outside the opened guard.
  hand(r,false,new T.Vector3(-.46,.38-.38*slump,.03),new T.Euler(-1.7+.8*slump,-.8,-.55),Math.min(1,limb));
  hand(r,true,new T.Vector3(.43,.08-.18*slump,.10),new T.Euler(-.4,.4,.5),Math.min(1,limb));
  foot(r,true,new T.Vector3(.19,-.795,.10),.14,0,Math.min(1,w));
  foot(r,false,new T.Vector3(-.22,-.795+Math.sin(Math.PI*ease(u/.35))*.075,-.18*ease(u/.35)),-.3,0,Math.min(1,w));
  return true;
 }
 const strength=w*h.strength,head=h.zone==='head',low=h.zone==='legs',side=h.point.x>=0?1:-1;
 r.body.position.x+=d.x*.05*strength;r.body.position.z+=d.z*.05*strength;r.body.position.y-=(low?.16:.07)*strength;
 r.body.rotation.x+=d.z*(low?.12:head?.22:.32)*strength;r.body.rotation.z-=d.x*.25*strength;r.body.rotation.y+=h.turn*strength;
 r.chest.rotation.y+=h.turn*.65*strength;r.chest.rotation.x+=d.z*(low?.06:.15)*strength;
 r.head.rotation.x+=d.z*(head?.48:.12)*headWeight*h.strength;r.head.rotation.z-=d.x*(head?.4:.1)*headWeight*h.strength;
 if(stunned){
  const armWeight=Math.min(1,limb),slump=ease((u-.28)/.28);
  blend(r.leftArm,(h.zone==='left-arm'?.2:-.35)+.16*slump,0,.35+(h.zone==='left-arm'?.35*strength:0),armWeight);
  blend(r.rightArm,(h.zone==='right-arm'?.2:-.35)+.16*slump,0,-.35-(h.zone==='right-arm'?.35*strength:0),armWeight);
  blend(r.leftElbow,h.zone==='left-arm'?-.2:-.65,0,0,armWeight);blend(r.rightElbow,h.zone==='right-arm'?-.2:-.65,0,0,armWeight);
  blend(r.leftHand,-.18*d.z,.10*side,.12,armWeight);blend(r.rightHand,-.18*d.z,.10*side,-.12,armWeight);
  const catchStep=ease((u-.10)/.35),lift=Math.sin(Math.PI*catchStep)*.085;
  foot(r,true,new T.Vector3(.18+d.x*.10,-.795+(side>0?lift:0),.05+d.z*.13*catchStep),h.turn*.5,0,Math.min(1,w));
  foot(r,false,new T.Vector3(-.18+d.x*.10,-.795+(side<0?lift:0),-.05+d.z*.13*catchStep),h.turn*.5,0,Math.min(1,w));
 }
 return true;
}
/** Fallback hitstun flinch when no authoritative hitReaction covers the stun (moved verbatim from sampleCharacter). */
export function fallbackFlinch(r:CharacterRig,p:PlayerView,time:number){
 const flinch=Math.min(1,((p.hitStunUntil||0)-time)/.12);r.body.rotation.x=-.23*flinch;r.chest.rotation.y=.20*flinch;r.head.rotation.x=-.22;r.leftArm.rotation.set(-.5,0,.45);r.rightArm.rotation.set(-.5,0,-.45);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.8;
}
