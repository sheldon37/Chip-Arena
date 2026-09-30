import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {foot,hand,braceWeapon,supportHand} from './rig-ik.js';
import {smoothMotion as ease} from './motion-quality.js';
import {WEAPON_WEIGHT} from '../shared/souls-combat.js';
import {WEAPONS} from '../shared/content.js';

/** Damped impulse response of a struck joint chain: 0 at the impact with its fastest rise there, exactly 1 at `tp`
 * (a ~6% overshoot peaks near .79 tp), then an opposite follow-through lobe scaled by `rebound`, decaying with `z`. */
export function hitImpulse(t:number,tp:number,z:number,rebound=1){
 if(t<=0)return 0;const x=t/tp,v=Math.exp(z*(1-x))*Math.sin(Math.PI/2*x);return x>2?v*rebound:v;
}
const e=new T.Euler(),q=new T.Quaternion(),grip=new T.Vector3(),palm=new T.Vector3(),SIDES=[true,false] as const;
/** Small additive rotations on whatever the weapon layer posed: the arm in its parent (chest) frame, elbow and wrist locally. */
const nudge=(node:T.Object3D,x:number,y:number,z:number,local=false)=>{q.setFromEuler(e.set(x,y,z));if(local)node.quaternion.multiply(q);else node.quaternion.premultiply(q);};
/** Impact drives the pelvis first; the chest follows 16 ms later, the head 33 ms and the arms 45 ms later, each with its own
 * damping (the head and arms whip, the hips stay heavy). Arms add a weight-scaled delta to the held weapon pose, and the
 * wrist counter-rotates so blades lag instead of flipping. A light hit without stun is a .35 recoil of the body with a
 * stronger head snap and, for guns and staves, an aim-arm kick so it still reads (a melee swing's arms are left alone). Every chain is exactly 0 at the authoritative end of the stun;
 * presentation never extends the lockout. */
export function hitReactionMotion(r:CharacterRig,p:PlayerView,time:number,stunned=false){return p.hitReaction?react(r,p,p.hitReaction,time,stunned):false;}
type Reaction=NonNullable<PlayerView['hitReaction']>;
function react(r:CharacterRig,p:PlayerView,h:Reaction,time:number,stunned:boolean){
 const until=stunned?Math.max(h.until,p.hitStunUntil||0):h.until;if(time<h.at||time>=until)return false;
 const duration=Math.max(.04,until-h.at),age=time-h.at,u=age/duration,tp=Math.min(.095,duration*.26),end=1-ease((u-.72)/.28),scale=stunned?1:.35,d=h.direction;
 if(h.kind==='parry'){parryMotion(r,p,age,u,tp,end,h.turn);return true;}
 const pelvis=hitImpulse(age,tp,.55,.6)*end*scale,chest=hitImpulse(age-.016,tp,.45,.6)*1.1*end*scale,head=hitImpulse(age-.033,tp,.25,.45)*1.25*end*(stunned?1:.6),arms=hitImpulse(age-.045,tp,.2,.5)*end*(stunned?1:WEAPONS[p.spec].class==='melee'?0:.55);
 const strength=pelvis*h.strength,torso=chest*h.strength,low=h.zone==='legs',headHit=h.zone==='head',side=h.point.x>=0?1:-1;
 // A two-handed hold (greatsword, shield, long guns) keeps its support hand on the grip through the recoil.
 const support=r.weapon.getObjectByName('support-grip');let twoHand=false;
 if(support&&r.weapon.visible){r.group.updateMatrixWorld(true);twoHand=support.getWorldPosition(grip).distanceTo(r.leftHand.getWorldPosition(palm))<.06;}
 r.body.position.x+=d.x*.05*strength;r.body.position.z+=d.z*.05*strength;r.body.position.y-=(low?.16:.07)*strength;
 r.body.rotation.x+=d.z*(low?.12:headHit?.22:.32)*strength;r.body.rotation.z-=d.x*.25*strength;r.body.rotation.y+=h.turn*strength;
 r.chest.rotation.y+=h.turn*.65*torso;r.chest.rotation.x+=d.z*(low?.06:.15)*torso;
 r.head.rotation.x+=d.z*(headHit?.48:.12)*head*h.strength;r.head.rotation.z-=d.x*(headHit?.4:.1)*head*h.strength;
 // Arms lag the torso (they swing against its pitch and flare out); the struck arm is knocked back straight. The wrists
 // counter-rotate against the whole torso-and-arm pitch, so a held blade keeps most of its world angle (heavier: more).
 const weight=WEAPON_WEIGHT[p.spec]??5,heavy=1/(1+weight*.08),a=arms*h.strength*heavy,inertia=Math.min(.92,.55+.03*weight);
 const trunk=d.z*(low?.12:headHit?.22:.32)*strength+d.z*(low?.06:.15)*torso;
 for(const left of SIDES){
  const sign=left?1:-1,struck=h.zone===(left?'left-arm':'right-arm'),arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,wrist=left?r.leftHand:r.rightHand;
  const dx=d.z*.45*a+(struck?.5*a:0)-(stunned?0:.1*a),dz=sign*(.22+(struck?.3:0))*a-d.x*.15*a,bend=struck?.35*a:-.3*a;
  nudge(arm,dx,0,dz);nudge(elbow,bend,0,0,true);nudge(wrist,-(trunk+dx+bend)*inertia,0,0,true);
 }
 if(twoHand)supportHand(r,support!,1);
 if(stunned){
  // The feet stay planted (the knees take the body drop) and a small catch step follows the push.
  const plant=Math.min(1,ease(age/.05)*end),catchStep=ease((u-.10)/.35),lift=Math.sin(Math.PI*catchStep)*.085;
  foot(r,true,new T.Vector3(.18+d.x*.10,-.795+(side>0?lift:0),.05+d.z*.13*catchStep),h.turn*.5,0,plant);
  foot(r,false,new T.Vector3(-.18+d.x*.10,-.795+(side<0?lift:0),-.05+d.z*.13*catchStep),h.turn*.5,0,plant);
 }
 return true;
}
/** A blocked melee swing: the weapon is thrown high and wide on the impulse, then hangs outside the opened guard.
 * The greatsword keeps both hands on its grip and is knocked back over the shoulder. */
function parryMotion(r:CharacterRig,p:PlayerView,age:number,u:number,tp:number,end:number,turn:number){
 const rise=(t:number)=>t/tp<.786?hitImpulse(t,tp,.55)/1.062:1,w=rise(age)*end,limb=Math.min(1,rise(age-.025)*end),slump=ease((u-.18)/.25),rebound=1-.55*slump,heavy=p.spec==='greatsword';
 r.body.position.y-=.12*w;r.body.position.z-=.065*(heavy?1.25:1)*w;
 r.body.rotation.x+=(-.30*rebound+.16*slump)*w;r.body.rotation.y+=turn*w;r.chest.rotation.y-=.22*w;r.head.rotation.x+=(-.28*rebound+.20*slump)*w;
 if(heavy){holdWeapon(r.weapon);braceWeapon(r,new T.Vector3(-.1-.02*slump,.45-.25*slump,.35-.05*slump),new T.Euler(-1.75+.55*slump,-.4+.1*slump,-.3+.1*slump),limb);if(limb>=.001)easeWeapon(r.weapon,limb);}
 else{
  parryHand(r,false,new T.Vector3(-.46,.38-.38*slump,.03),new T.Euler(-1.7+.8*slump,-.8,-.55),limb);
  parryHand(r,true,new T.Vector3(.43,.08-.18*slump,.10),new T.Euler(-.4,.4,.5),limb);
 }
 foot(r,true,new T.Vector3(.19,-.795,.10),.14,0,Math.min(1,w));
 foot(r,false,new T.Vector3(-.22,-.795+Math.sin(Math.PI*ease(u/.35))*.075,-.18*ease(u/.35)),-.3,0,Math.min(1,w));
}
const heldWrist=new T.Quaternion(),desired=new T.Quaternion(),parentQ=new T.Quaternion(),heldWeapon=new T.Quaternion(),heldWeaponPos=new T.Vector3(),solvedQ=new T.Quaternion(),solvedPos=new T.Vector3();
/** hand() and braceWeapon() snap the weapon to the grip transform at any weight; ease it from the held transform instead. */
const holdWeapon=(weapon:T.Object3D)=>{heldWeapon.copy(weapon.quaternion);heldWeaponPos.copy(weapon.position);};
const easeWeapon=(weapon:T.Object3D,weight:number)=>{if(weight>=1)return;solvedQ.copy(weapon.quaternion);solvedPos.copy(weapon.position);weapon.quaternion.copy(heldWeapon).slerp(solvedQ,weight);weapon.position.lerpVectors(heldWeaponPos,solvedPos,weight);};
/** hand() blends the wrist in its local frame while the forearm is still turning, where carry to parry passes 180 degrees
 * and the blade flips 80-100 degrees in one frame at mid weight. Blend the wrist's world orientation instead (carry to parry
 * is under 90 degrees there), and skip the solve entirely at weight 0. */
function parryHand(r:CharacterRig,left:boolean,target:T.Vector3,orientation:T.Euler,weight:number){
 if(weight<=0)return;const wrist=left?r.leftHand:r.rightHand,weapon=left?r.offWeapon:r.weapon;
 r.group.updateMatrixWorld(true);wrist.getWorldQuaternion(heldWrist);holdWeapon(weapon);
 hand(r,left,target,orientation,weight);
 r.chest.getWorldQuaternion(desired).multiply(q.setFromEuler(orientation));heldWrist.slerp(desired,weight);
 wrist.quaternion.copy(wrist.parent!.getWorldQuaternion(parentQ).invert().multiply(heldWrist));easeWeapon(weapon,weight);
}
const flinches=new WeakMap<CharacterRig,{until:number;at:number}>(),flinchHit:Reaction={kind:'hit',at:0,until:0,yaw:0,direction:{x:0,y:0,z:-1},point:{x:0,y:.1,z:.2},zone:'torso',turn:0,strength:.6};
/** Hitstun without an authoritative hitReaction (a directionless stun): the same impulse reaction as a torso hit from the
 * front (strength .6), starting on the first frame this rig shows the stun and ending exactly at hitStunUntil. The start is
 * client-only and restarts when the stun changes or time runs backward. */
export function fallbackFlinch(r:CharacterRig,p:PlayerView,time:number){
 const until=p.hitStunUntil||0;let s=flinches.get(r);if(!s){s={until,at:time};flinches.set(r,s);}else if(s.until!==until||time<s.at||time-s.at>until-s.at+.35){s.until=until;s.at=time;}
 flinchHit.at=s.at;flinchHit.until=until;flinchHit.yaw=p.yaw;react(r,p,flinchHit,time,true);
}
