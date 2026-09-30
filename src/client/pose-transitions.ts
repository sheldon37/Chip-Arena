import {rolling,meleeRecovery} from '../shared/souls-combat.js';
import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {smoothMotion} from './motion-quality.js';
import {knockdownPhase} from './knockdown-motion.js';
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe','weapon','offWeapon'] as const;
export type TransitionBone=typeof bones[number];
/** The joints finishPose preserves and decays; seedTransition accepts a quaternion array in this order. */
export const TRANSITION_BONES:readonly TransitionBone[]=bones;
type StoredPose={q:T.Quaternion[];position:T.Vector3;weapon:T.Vector3;offWeapon:T.Vector3};
type Transition={id:string;key:string;action:string;time:number;prevTime:number;at:number;duration:number;last:StoredPose;prev:StoredPose;hasPrev:boolean;offset:StoredPose;active:boolean};
const transitions=new WeakMap<CharacterRig,Transition>(),seeds=new WeakMap<CharacterRig,{pose:StoredPose;duration:number}>();
const stored=():StoredPose=>({q:bones.map(()=>new T.Quaternion()),position:new T.Vector3(),weapon:new T.Vector3(),offWeapon:new T.Vector3()});
const capture=(r:CharacterRig,out:StoredPose)=>{for(let i=0;i<bones.length;i++)out.q[i].copy(r[bones[i]].quaternion);out.position.copy(r.body.position);out.weapon.copy(r.weapon.position);out.offWeapon.copy(r.offWeapon.position);return out;};
const identity=new T.Quaternion(),scratch=new T.Quaternion(),current=stored(),projected=stored();
const standardSpec=(spec:string)=>['sword','daggers','greatsword','shield'].includes(spec);
/** Knockdown and juggle sub-phases are continuous poses: their key changes blend from the extrapolated incoming pose,
 * so the offset holds only a real discontinuity, never one frozen frame of motion. */
const reactionFamily=(action:string)=>action==='juggle'||action.startsWith('down:');
/** The pose family the sampled rig is in. Juggle and knockdown count only inside their ACTIVE windows (a snapshot can carry
 * the field past `until`), exactly as sampleCharacter picks the pose. */
export function transitionAction(p:PlayerView,time:number){
 const rate=p.attackRate||1,windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 const attackDuration=standardSpec(p.spec)?windup+window+meleeRecovery(p.spec)/rate:.8/rate,w=WEAPONS[p.spec];
 return p.juggle&&time<p.juggle.until?'juggle':p.knockdown&&time<p.knockdown.until?'down:'+knockdownPhase(p.knockdown,time):(p.hitStunUntil||0)>time?'flinch':rolling(p,time)?'roll':(p.guarding||p.blockTime>0)?(w.class==='melee'?'block':'guard'):p.emperor?.action&&time<p.emperor.until?p.emperor.action:p.arcade?.action&&time<p.arcade.until?p.arcade.action:p.meleeAction&&time<p.meleeAction.until?p.meleeAction.kind:w.class==='melee'&&time-p.lastAttack<attackDuration?'attack-'+p.attackIndex:w.class==='ranged'&&p.reloadTime>0?'reload':'base';
}
/** Transition length for a key change. Attack entries always end before the contact frame (swingAt). */
export function transitionDuration(p:PlayerView,time:number,action:string,previous:string,reaction?:PlayerView['hitReaction']){
 if(reaction)return Math.min(.075,Math.max(.02,(Math.max(reaction.until,p.hitStunUntil||0)-time)*.28));
 if(reactionFamily(action)){
  let d=action!=='juggle'&&action!=='down:air'&&reactionFamily(previous)?.08:.09;
  // The lying body is exact from landedAt+.25 (it reads as fully down; fighter-presentation pins -PI/2).
  if(action==='down:flat'&&p.knockdown?.landedAt!==undefined)d=Math.min(d,p.knockdown.landedAt+.25-time);
  return d;
 }
 const heavy=p.spec==='greatsword';
 if(action.startsWith('attack-')){const windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup);let d=Math.min(.55*windup,.09);if(p.swingAt!==undefined)d=Math.min(d,p.swingAt-time);return d;}
 if(action==='base')return previous.startsWith('attack-')&&standardSpec(p.spec)?(heavy?.28:.22):.13;
 if(action==='block')return heavy?.11:.09;
 if(action==='guard'||action==='reload')return .08;
 return .055;
}
/** Hand an externally produced pose (a knockdown ragdoll, a cinematic) to the rig: the next finishPose blends from it over
 * `duration` seconds. `localQuats` are rig-local joint rotations, either an array in TRANSITION_BONES order or a map by
 * bone name; missing joints keep the rig's current rotation. `bodyPos` is the motion-root (r.body) local position. */
export function seedTransition(r:CharacterRig,localQuats:ArrayLike<T.Quaternion|undefined>|Partial<Record<TransitionBone,T.Quaternion>>,bodyPos:T.Vector3|undefined,duration:number){
 const pose=capture(r,stored()),list=localQuats as ArrayLike<T.Quaternion|undefined>,map=localQuats as Partial<Record<TransitionBone,T.Quaternion>>,indexed=typeof list.length==='number';
 for(let i=0;i<bones.length;i++){const q=indexed?list[i]:map[bones[i]];if(q)pose.q[i].copy(q);}
 if(bodyPos)pose.position.copy(bodyPos);seeds.set(r,{pose,duration:Math.max(0,duration)});
}
/** Preserve the incoming pose, decay its offset, and keep sampling the live attack underneath.
 * This is a transition correction, not an all-frame low-pass that would delay hit poses. It starts fresh (no blend) on a
 * rig's first sample, when time runs backward or jumps by more than .35 s, and when the rig starts showing another player. */
export function finishPose(r:CharacterRig,p:PlayerView,time:number){
 const action=transitionAction(p,time);
 const reaction=p.hitReaction&&time>=p.hitReaction.at&&time<Math.max(p.hitReaction.until,p.hitStunUntil||0)?p.hitReaction:undefined;
 // Grounded is not part of the key: jumps and landings are animated continuously by locomotion.
 const key=p.spec+':'+action+(reaction?':impact-'+reaction.at+':'+reaction.kind:'');let s=transitions.get(r);const seed=seeds.get(r);seeds.delete(r);
 if(!s){s={id:p.id,key,action,time,prevTime:time,at:time,duration:0,last:capture(r,stored()),prev:stored(),hasPrev:false,offset:stored(),active:false};transitions.set(r,s);if(!seed)return;}
 else if(time<s.time||time-s.time>.35||p.id!==s.id){s.id=p.id;s.key=key;s.action=action;s.time=s.prevTime=s.at=time;s.active=false;s.hasPrev=false;capture(r,s.last);return;}
 capture(r,current);let incoming:StoredPose|undefined,duration=0;
 if(seed){incoming=seed.pose;duration=seed.duration;}
 else if(key!==s.key){
  duration=transitionDuration(p,time,action,s.action,reaction);incoming=s.last;
  if(!reaction&&reactionFamily(s.action)&&s.hasPrev&&s.time>s.prevTime){
   // Continue the last frame's joint velocities to this frame's time, then keep only the remaining discontinuity.
   const ratio=T.MathUtils.clamp((time-s.time)/(s.time-s.prevTime),0,2);
   for(let i=0;i<bones.length;i++)projected.q[i].copy(s.prev.q[i]).invert().premultiply(s.last.q[i]).slerp(identity,1-ratio).multiply(s.last.q[i]);
   for(const k of ['position','weapon','offWeapon'] as const)projected[k].copy(s.last[k]).sub(s.prev[k]).multiplyScalar(ratio).add(s.last[k]);
   incoming=projected;
  }
 }
 if(incoming){
  s.key=key;s.action=action;s.at=time;s.duration=duration;s.active=duration>1e-4;
  if(s.active){for(let i=0;i<bones.length;i++)s.offset.q[i].copy(current.q[i]).invert().multiply(incoming.q[i]);for(const k of ['position','weapon','offWeapon'] as const)s.offset[k].copy(incoming[k]).sub(current[k]);}
 }
 if(s.active){
  const u=smoothMotion((time-s.at)/s.duration);
  // A finished transition leaves the sampled pose untouched (exact Euler values survive, e.g. the flat knockdown).
  if(u>=1)s.active=false;
  else{for(let i=0;i<bones.length;i++)r[bones[i]].quaternion.multiply(scratch.copy(s.offset.q[i]).slerp(identity,u));r.body.position.addScaledVector(s.offset.position,1-u);r.weapon.position.addScaledVector(s.offset.weapon,1-u);r.offWeapon.position.addScaledVector(s.offset.offWeapon,1-u);}
 }
 const spare=s.prev;s.prev=s.last;s.last=capture(r,spare);s.hasPrev=true;s.prevTime=s.time;s.time=time;
}
