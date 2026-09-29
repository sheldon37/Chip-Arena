import * as T from 'three';
import {ragdollVariant} from './ragdoll.js';
import type {CharacterRig} from './character-rig.js';
import type {Snapshot} from '../shared/simulation.js';
import type {KillEffectFactory} from './death-effects.js';
/** The capsule remains authoritative; articulated limbs follow its pelvis until the recovery roll. */
export class KnockdownRagdolls{
 private active=new Map<string,{at:number;effect:ReturnType<KillEffectFactory>}>();private epoch='';
 constructor(private scene:T.Scene){}
 update(state:Snapshot,rigs:Map<string,CharacterRig>,hiddenId?:string){const epoch=`${state.generation}:${state.round}:${state.setNumber}`;if(epoch!==this.epoch){this.clear();this.epoch=epoch;}
  const keep=new Set<string>();for(const p of state.players){const k=p.knockdown,r=rigs.get(p.id),time=p.animationTime??state.time;if(!r||p.id===hiddenId||p.hp<=0||!k?.ragdoll||time>=k.until||k.rollAt!==undefined&&time>=k.rollAt||!['fight','result'].includes(state.phase))continue;keep.add(p.id);let entry=this.active.get(p.id);if(entry&&entry.at!==k.at){entry.effect.dispose();this.active.delete(p.id);entry=undefined;}
   if(!entry){r.group.updateMatrixWorld(true);const effect=ragdollVariant('ragdoll',{anchor:()=>{r.group.updateMatrixWorld(true);return {position:r.pelvis.getWorldPosition(new T.Vector3()),rotation:r.pelvis.getWorldQuaternion(new T.Quaternion())};}})(this.scene,r.group,p,{id:'down-'+p.id,at:k.at,effect:'ragdoll',seed:Math.round(k.at*60),pos:p.pos,velocity:p.vel,direction:{x:Math.sin(p.yaw),y:.2,z:Math.cos(p.yaw)},source:k.source||null,kind:'knockdown',yaw:p.yaw},1,11,state);entry={at:k.at,effect};this.active.set(p.id,entry);}
   r.group.visible=false;entry.effect.update(Math.max(0,time-k.at));
  }for(const[id,e]of this.active)if(!keep.has(id)){e.effect.dispose();this.active.delete(id);}
 }
 clear(){for(const e of this.active.values())e.effect.dispose();this.active.clear();}
}
