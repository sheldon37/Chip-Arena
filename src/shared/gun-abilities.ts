// Gun Q/E handlers (design §5). FOUNDATION ships the empty registry; GUNS registers frag-grenade, rocket and orbital-mark
// here and sets `live` on the kit rows (gun-kit.ts). Runtime imports: config, projectiles, content, elements, abilities only.
import type {AbilityHandler} from './ability-system.js';
import type {Simulation,Player} from './simulation.js';
import type {Minion} from './chaos.js';
import type {Vec} from './config.js';
export const GUN_HANDLERS:Record<string,AbilityHandler>={};
/** Gun primary direct-hit hook (players and minions). No effect in FOUNDATION. */
export function gunPrimaryHit(_sim:Simulation,_p:Player,_target:Player|Minion,_point:Vec,_actual:number):void{}
