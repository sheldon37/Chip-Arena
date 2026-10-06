import type {GameEvent} from '../shared/simulation.js';
import type {Vec} from '../shared/config.js';
/** Client-only record of the latest successful block per defender, fed from 'blockHit' events.
 * Verified in simulation.successfulBlock: event.source is the DEFENDER id and event.target is the attacker id.
 * Presentation only: never serialized or fed into simulation, prediction or input. melee-block-motion reads it for the block recoil. */
export type BlockImpact={at:number;point:Vec;dir?:Vec;attacker?:string;kind?:string};
const impacts=new Map<string,BlockImpact>();
/** Record a 'blockHit' event (other event types are ignored). at is the authoritative event time.
 * defenderPos, when known, gives dir: the unit horizontal direction from the defender to the impact point. */
export function recordBlockHit(event:Pick<GameEvent,'type'|'time'|'pos'|'source'|'target'|'message'>,time:number,defenderPos?:Vec){
 if(event.type!=='blockHit'||!event.source)return;
 for(const [id,e] of impacts)if(time-e.at>2||e.at-time>2)impacts.delete(id);
 let dir:Vec|undefined;if(defenderPos){const x=event.pos.x-defenderPos.x,z=event.pos.z-defenderPos.z,l=Math.hypot(x,z);if(l>1e-4)dir={x:x/l,y:0,z:z/l};}
 impacts.set(event.source,{at:event.time,point:{x:event.pos.x,y:event.pos.y,z:event.pos.z},dir,attacker:event.target,kind:event.message});
}
/** The latest block impact on this defender if it happened within [time-window, time]; rewinds before it return undefined. */
export function blockImpact(id:string,time:number,window=.4):BlockImpact|undefined{
 const e=impacts.get(id);if(!e)return undefined;const age=time-e.at;return age>=0&&age<=window?e:undefined;
}
export function clearBlockImpacts(){impacts.clear();hits.clear();}
/** Client-only record of each attacker's latest landed hit ('hit' events: source is the attacker, time the authoritative hit
 * time), for the attacker's presentation-only hit-stop (primary-melee-motion). Damage-over-time ticks are ignored, and a
 * cleave's later victims keep the first contact. Never serialized or fed into simulation, prediction or input. */
const hits=new Map<string,number>();
export function recordMeleeHit(event:Pick<GameEvent,'type'|'time'|'source'|'message'>){
 if(event.type!=='hit'||!event.source||event.message==='burn'||event.message==='poison')return;
 for(const [id,at] of hits)if(Math.abs(event.time-at)>2)hits.delete(id);
 const prev=hits.get(event.source);if(prev!==undefined&&event.time>=prev&&event.time-prev<.2)return;hits.set(event.source,event.time);
}
/** The attacker's latest recorded hit time if it lies within [time-window, time]; a rewind before it returns undefined. */
export function meleeHitAt(id:string,time:number,window=.4){const at=hits.get(id);return at!==undefined&&time>=at&&time-at<=window?at:undefined;}
