import {CONFIG} from './config.js';
export const DRAFT_REVEAL_SECONDS=3.2;
/** Validate settings at player/network boundaries; simulations may use shorter test clocks. */
export function selectionSeconds(value:unknown){return typeof value==='number'&&Number.isFinite(value)?Math.max(5,Math.min(120,Math.round(value))):CONFIG.selectionSeconds;}
