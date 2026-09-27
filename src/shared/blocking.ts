import {CONFIG} from './config.js';
import type {Stats} from './content.js';
export const blockCooldown=(stats:Stats)=>Math.max(CONFIG.blockMinimumCooldown,CONFIG.blockCooldown*stats.blockCooldown);
export const blockDuration=(stats:Stats)=>Math.max(.1,Math.min(CONFIG.blockMaximumDuration,CONFIG.blockDuration+stats.blockWindow));
