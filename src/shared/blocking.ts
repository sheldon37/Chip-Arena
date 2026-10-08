import {CONFIG} from './config.js';
import type {Stats} from './content.js';
export const FIRST_BLOCK_CHIPS=['supernova','block-pulse','block-medic','block-loader','mirror-mine','freezer-block','graveyard-shift','corpse-party','poltergeist'] as const;
export const firstBlockChips=(chips:Record<string,number>)=>Object.fromEntries(FIRST_BLOCK_CHIPS.filter(id=>chips[id]>0).map(id=>[id,chips[id]]));
export const blockCooldown=(stats:Stats)=>Math.max(CONFIG.blockMinimumCooldown,CONFIG.blockCooldown*stats.blockCooldown);
export const blockDuration=(stats:Stats)=>Math.max(.1,Math.min(CONFIG.blockMaximumDuration,CONFIG.blockDuration+stats.blockWindow));
/** Incoming damage and physical impact determine recovery; attack speed cannot erase a block punish. */
export const meleeBlockStun=(damage:number,impulse=0)=>Math.max(.25,Math.min(.95,.18+Math.max(0,damage)*.008+Math.max(0,impulse)*.00025));
/** Riposte (owner 2026-10-04: "make blocking melee worth it ... enough to get a hit or 2"): when a human standard-melee
 * blocker stops a melee hit, the attacker stays stunned until `window` s (plus `perDamage` per blocked point) after the
 * blocker's own first strike could land (`ready`: its attack cooldown left plus its windup), capped, plus `travel` (the
 * time to walk back the extra rebound of block-force chips), and never shorter than meleeBlockStun. The blocker's counter ends that stun, so the counter's usual follow-up (Dagger pair, Shield knockdown,
 * Sword/Greatsword stagger) gives the second hit where the weapon has one. */
export const BLOCK_PUNISH={window:1,perDamage:.004,cap:1.4} as const;
export const meleeBlockPunish=(damage:number,ready:number,travel=0)=>Math.min(BLOCK_PUNISH.cap,Math.max(0,ready)+BLOCK_PUNISH.window+Math.max(0,damage)*BLOCK_PUNISH.perDamage)+Math.max(0,travel);
/** The Block window a riposte opened is still running: the blocker's attack, special or roll ends it early (breakRiposte). */
export const riposteOpen=(p:{riposteAt?:number;lastBlock?:number;blockTime:number})=>p.riposteAt!==undefined&&p.blockTime>0&&p.riposteAt>=(p.lastBlock??Infinity);
