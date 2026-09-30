import * as T from 'three';
import {ragdollVariant,type RagdollEffect} from './ragdoll.js';
import type {CharacterRig} from './character-rig.js';
import type {Snapshot,PlayerView} from '../shared/simulation.js';
import {seedTransition,TRANSITION_BONES} from './pose-transitions.js';
/** Ragdoll part (rig joint name) to rig bone, in hierarchy order so parents are posed before their children. */
const PARTS:[string,'pelvis'|'spine'|'head'|'leftArm'|'leftElbow'|'rightArm'|'rightElbow'|'leftLeg'|'leftKnee'|'rightLeg'|'rightKnee'][]=[['pelvis','pelvis'],['spine','spine'],['head','head'],['left-shoulder','leftArm'],['left-elbow','leftElbow'],['right-shoulder','rightArm'],['right-elbow','rightElbow'],['left-hip','leftLeg'],['left-knee','leftKnee'],['right-hip','rightLeg'],['right-knee','rightKnee']];
const parent=new T.Quaternion(),world=new T.Quaternion();
/** Limbs fly along the knockback (horizontal velocity, else the external push), falling back to away from the facing. */
function launchDirection(p:PlayerView){
 for(const v of [p.vel,p.external])if(v&&Math.hypot(v.x,v.z)>.5){const l=Math.hypot(v.x,v.z);return {x:v.x/l,y:.2,z:v.z/l};}
 return {x:-Math.sin(p.yaw),y:.2,z:-Math.cos(p.yaw)};
}
/** Put the rig in the ragdoll's final pose (rig-local joint rotations), then let finishPose blend it into the recovery roll. */
function handBack(r:CharacterRig,effect:RagdollEffect){
 const root=effect.root;root.updateMatrixWorld(true);r.group.updateMatrixWorld(true);
 for(const [name,bone] of PARTS){const part=root.getObjectByName(name);if(!part||bone==='pelvis')continue;const joint=r[bone];
  joint.quaternion.copy(joint.parent!.getWorldQuaternion(parent).invert().multiply(part.getWorldQuaternion(world)));joint.updateMatrixWorld(true);}
 seedTransition(r,TRANSITION_BONES.map(b=>r[b].quaternion.clone()),r.body.position,.16);
}
/** The capsule remains authoritative; articulated limbs follow its pelvis until the recovery roll, then hand their pose back. */
export class KnockdownRagdolls{
 private active=new Map<string,{at:number;effect:RagdollEffect}>();private epoch='';
 constructor(private scene:T.Scene){}
 update(state:Snapshot,rigs:Map<string,CharacterRig>,hiddenId?:string){const epoch=`${state.generation}:${state.round}:${state.setNumber}`;if(epoch!==this.epoch){this.clear();this.epoch=epoch;}
  const keep=new Set<string>();for(const p of state.players){const k=p.knockdown,r=rigs.get(p.id),time=p.animationTime??state.time;if(!r||p.id===hiddenId||p.hp<=0||!k?.ragdoll||time>=k.until||k.rollAt!==undefined&&time>=k.rollAt||!['fight','result'].includes(state.phase))continue;keep.add(p.id);let entry=this.active.get(p.id);if(entry&&entry.at!==k.at){entry.effect.dispose();this.active.delete(p.id);entry=undefined;}
   if(!entry){r.group.updateMatrixWorld(true);const effect=ragdollVariant('ragdoll',{anchor:()=>{r.group.updateMatrixWorld(true);return {position:r.pelvis.getWorldPosition(new T.Vector3()),rotation:r.pelvis.getWorldQuaternion(new T.Quaternion())};}})(this.scene,r.group,p,{id:'down-'+p.id,at:k.at,effect:'ragdoll',seed:Math.round(k.at*60),pos:p.pos,velocity:p.vel,direction:launchDirection(p),source:k.source||null,kind:'knockdown',yaw:p.yaw},1,11,state) as RagdollEffect;entry={at:k.at,effect};this.active.set(p.id,entry);}
   r.group.visible=false;entry.effect.update(Math.max(0,time-k.at));
  }for(const[id,e]of this.active)if(!keep.has(id)){
   // Handing back at the recovery roll: the rig shows the ragdoll's pose this frame and blends into the roll from it.
   const p=state.players.find(q=>q.id===id),r=rigs.get(id),k=p?.knockdown,time=p?(p.animationTime??state.time):0;
   if(p&&r&&k?.ragdoll&&k.at===e.at&&p.hp>0&&k.rollAt!==undefined&&time>=k.rollAt&&time<k.until)handBack(r,e.effect);
   e.effect.dispose();this.active.delete(id);}
 }
 clear(){for(const e of this.active.values())e.effect.dispose();this.active.clear();}
}
