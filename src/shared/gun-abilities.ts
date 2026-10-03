// Gun Q/E handlers (design §5). Runtime imports: config, projectiles, content, elements, abilities only.
import type {AbilityHandler} from './ability-system.js';
import type {Simulation,Player} from './simulation.js';
import type {Minion} from './chaos.js';
import type {Vec} from './config.js';
export const GUN_HANDLERS:Record<string,AbilityHandler>={};
export function gunPrimaryHit(_sim:Simulation,_p:Player,_target:Player|Minion,_point:Vec,_actual:number):void{}
