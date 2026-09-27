import {CONFIG} from './config.js';
import type {Stats} from './content.js';
export const FIRST_BLOCK_CHIPS=['supernova','block-pulse','block-medic','block-loader','mirror-mine','freezer-block','graveyard-shift','corpse-party','poltergeist'] as const;
export const firstBlockChips=(chips:Record<string,number>)=>Object.fromEntries(FIRST_BLOCK_CHIPS.filter(id=>chips[id]>0).map(id=>[id,chips[id]]));
export const blockCooldown=(stats:Stats)=>Math.max(CONFIG.blockMinimumCooldown,CONFIG.blockCooldown*stats.blockCooldown);
export const blockDuration=(stats:Stats)=>Math.max(.1,Math.min(CONFIG.blockMaximumDuration,CONFIG.blockDuration+stats.blockWindow));
