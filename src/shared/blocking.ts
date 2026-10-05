import {CONFIG} from './config.js';
import type {Stats} from './content.js';
export const FIRST_BLOCK_CHIPS=['supernova','block-pulse','block-medic','block-loader','mirror-mine','freezer-block','graveyard-shift','corpse-party','poltergeist'] as const;
export const firstBlockChips=(chips:Record<string,number>)=>Object.fromEntries(FIRST_BLOCK_CHIPS.filter(id=>chips[id]>0).map(id=>[id,chips[id]]));
export const blockCooldown=(stats:Stats)=>Math.max(CONFIG.blockMinimumCooldown,CONFIG.blockCooldown*stats.blockCooldown);
export const blockDuration=(stats:Stats)=>Math.max(.1,Math.min(CONFIG.blockMaximumDuration,CONFIG.blockDuration+stats.blockWindow));
/** Incoming damage and physical impact determine recovery; attack speed cannot erase a block punish. */
export const meleeBlockStun=(damage:number,impulse=0)=>Math.max(.25,Math.min(.95,.18+Math.max(0,damage)*.008+Math.max(0,impulse)*.00025));
/** A human's timed melee block is a riposte (owner 2026-10-04: "make blocking melee worth it ... enough to get a hit or 2"):
 * the attacker stays stunned until `window` s (plus `perDamage` per blocked point) after the blocker's own first strike could
 * land (`ready`: its attack cooldown left plus its windup), capped; never shorter than meleeBlockStun. */
export const BLOCK_PUNISH={window:.5,perDamage:.004,cap:1.4} as const;
export const meleeBlockPunish=(damage:number,ready:number)=>Math.min(BLOCK_PUNISH.cap,Math.max(0,ready)+BLOCK_PUNISH.window+Math.max(0,damage)*BLOCK_PUNISH.perDamage);
