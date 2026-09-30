import {rolling,meleeRecovery} from '../shared/souls-combat.js';
import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {smoothMotion} from './motion-quality.js';
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe','weapon','offWeapon'] as const;
type StoredPose={q:T.Quaternion[];position:T.Vector3};
type Transition={key:string;time:number;at:number;duration:number;last:StoredPose;offset?:StoredPose};
const transitions=new WeakMap<CharacterRig,Transition>();
/** Preserve the incoming pose, decay its offset, and keep sampling the live attack underneath.
 * This is a transition correction, not an all-frame low-pass that would delay hit poses. */
export function finishPose(r:CharacterRig,p:PlayerView,time:number){
 const standard=['sword','daggers','greatsword','shield'].includes(p.spec),rate=p.attackRate||1,windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 const attackDuration=standard?windup+window+meleeRecovery(p.spec)/rate:.8/rate;
 const action=p.juggle?'juggle':p.knockdown?'down':(p.hitStunUntil||0)>time?'flinch':rolling(p,time)?'roll':(p.guarding||p.blockTime>0)?'block':p.emperor?.action&&time<p.emperor.until?p.emperor.action:p.arcade?.action&&time<p.arcade.until?p.arcade.action:p.meleeAction&&time<p.meleeAction.until?p.meleeAction.kind:WEAPONS[p.spec].class==='melee'&&time-p.lastAttack<attackDuration?'attack-'+p.attackIndex:'base';
 const reaction=p.hitReaction&&time>=p.hitReaction.at&&time<Math.max(p.hitReaction.until,p.hitStunUntil||0)?p.hitReaction:undefined;
 const key=p.spec+':'+action+':'+p.grounded+(reaction?':impact-'+reaction.at+':'+reaction.kind:''),current:StoredPose={q:bones.map(k=>r[k].quaternion.clone()),position:r.body.position.clone()};let s=transitions.get(r);
 if(!s||time<s.time||time-s.time>.35||action==='juggle'||action==='down'){s={key,time,at:time,duration:0,last:current};transitions.set(r,s);return;}
 if(key!==s.key){s.offset={q:current.q.map((q,i)=>q.clone().invert().multiply(s!.last.q[i])),position:s.last.position.clone().sub(current.position)};s.key=key;s.at=time;s.duration=reaction?Math.min(.075,Math.max(.02,(Math.max(reaction.until,p.hitStunUntil||0)-time)*.28)):action==='base'?.13:.055;}
 if(s.offset){const u=smoothMotion((time-s.at)/s.duration),identity=new T.Quaternion();for(let i=0;i<bones.length;i++)r[bones[i]].quaternion.multiply(s.offset.q[i].clone().slerp(identity,u));r.body.position.addScaledVector(s.offset.position,1-u);if(u>=1)s.offset=undefined;}
 s.time=time;s.last={q:bones.map(k=>r[k].quaternion.clone()),position:r.body.position.clone()};
}
