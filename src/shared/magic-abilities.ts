// Magic Q/E handlers and primary hit effects (design §6). FOUNDATION ships the empty registry and the current lightning
// chain verbatim (players only, props as targets); MAGIC registers the spells, the chain fix, Soak and the school chips.
// Runtime imports: config, projectiles, content, elements, abilities only.
import type {AbilityHandler} from './ability-system.js';
import type {Simulation,Player} from './simulation.js';
import type {Minion} from './chaos.js';
import {WEAPONS} from './content.js';
import {type Vec,v,sub,len} from './config.js';
export const MAGIC_HANDLERS:Record<string,AbilityHandler>={};
/** Magic primary direct-hit hook: today's lightning chain arcs to one nearby prop after a player hit; no-op for minions. */
export function magicPrimaryHit(sim:Simulation,p:Player,target:Player|Minion,point:Vec,_actual:number):void{
 if(!('stats'in target))return;const w=WEAPONS[p.spec];
 if(w.chain){for(const other of sim.props.values())if(other.material!=='foundation'&&len(sub(other.body.translation(),point))<3&&sim.lineOfSight(point,other.body.translation(),target.id,other.id)){const t=other.body.translation(),arcEnd=v(t.x,t.y,t.z);sim.damageProp(other,w.chain,v(),p.id,arcEnd);sim.emit('arc',point,{from:arcEnd,color:w.color});break;}}
}
