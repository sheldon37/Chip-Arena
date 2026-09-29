import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {foot,hand} from './primary-melee-motion.js';
import {smoothMotion as ease} from './motion-quality.js';

/** Rebound, exposed hold, then foot recovery occupy the authoritative stun window. */
export function hitReactionMotion(r:CharacterRig,p:PlayerView,time:number,stunned=false){
 const h=p.hitReaction;if(!h)return false;const until=stunned?Math.max(h.until,p.hitStunUntil||0):h.until;if(time<h.at||time>=until)return false;
 const age=time-h.at,u=age/Math.max(.04,until-h.at),settle=1-ease((u-.64)/.36),kick=ease(age/Math.min(.055,(until-h.at)*.18)),w=kick*settle*(stunned?1:.35),d=h.direction;
 if(h.kind==='parry'){
  const slump=ease((u-.18)/.25),rebound=1-.55*slump;
  r.body.position.y-=.12*w;r.body.position.z-=.065*w;
  r.body.rotation.x+=(-.30*rebound+.16*slump)*w;r.body.rotation.y+=h.turn*w;r.chest.rotation.y-=.22*w;r.head.rotation.x+=(-.28*rebound+.20*slump)*w;
  // Weapon hand is thrown high and wide, then hangs outside the opened guard.
  hand(r,false,new T.Vector3(-.46,.38-.38*slump,.03),new T.Euler(-1.7+.8*slump,-.8,-.55),w);
  hand(r,true,new T.Vector3(.43,.08-.18*slump,.10),new T.Euler(-.4,.4,.5),w);
  foot(r,true,new T.Vector3(.19,-.795,.10),.14,0,w);
  foot(r,false,new T.Vector3(-.22,-.795+Math.sin(Math.PI*ease(u/.35))*.075,-.18*ease(u/.35)),-.3,0,w);
  return true;
 }
 const strength=w*h.strength,head=h.zone==='head',low=h.zone==='legs',side=h.point.x>=0?1:-1;
 r.body.position.x+=d.x*.05*strength;r.body.position.z+=d.z*.05*strength;r.body.position.y-=(low?.16:.07)*strength;
 r.body.rotation.x+=d.z*(low?.12:head?.22:.32)*strength;r.body.rotation.z-=d.x*.25*strength;r.body.rotation.y+=h.turn*strength;
 r.chest.rotation.y+=h.turn*.65*strength;r.chest.rotation.x+=d.z*(low?.06:.15)*strength;
 r.head.rotation.x+=d.z*(head?.48:.12)*strength;r.head.rotation.z-=d.x*(head?.4:.1)*strength;
 if(stunned){
  r.leftArm.rotation.set(h.zone==='left-arm'?.2:-.35,0,.35+(h.zone==='left-arm'?.35*strength:0));r.rightArm.rotation.set(h.zone==='right-arm'?.2:-.35,0,-.35-(h.zone==='right-arm'?.35*strength:0));r.leftElbow.rotation.x=h.zone==='left-arm'?-.2:-.65;r.rightElbow.rotation.x=h.zone==='right-arm'?-.2:-.65;
  const catchStep=ease(u/.4),lift=Math.sin(Math.PI*catchStep)*.065;
  foot(r,true,new T.Vector3(.18+d.x*.10,-.795+(side>0?lift:0),.05+d.z*.13*catchStep),h.turn*.5,0,w);
  foot(r,false,new T.Vector3(-.18+d.x*.10,-.795+(side<0?lift:0),-.05+d.z*.13*catchStep),h.turn*.5,0,w);
 }
 return true;
}
