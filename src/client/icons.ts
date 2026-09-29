import {ICON_PATHS} from './icons.generated.js';
import {CHIP_ICONS,CLASS_ICONS,SPEC_ICONS,ACTION_ICONS} from './icon-map.js';
/** Decorative icons accompany existing visible labels / accessible names. */
export function icon(name:string){return `<svg class="game-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${ICON_PATHS[name]||ICON_PATHS.cpu}</svg>`;}
export function chipIcon(id:string){const base=icon(CHIP_ICONS[id]||'cpu');return id==='necronomicon'?`<span class="icon-composite">${base}<i>${icon('skull')}</i></span>`:base;}
export const classIcon=(id:string)=>icon(CLASS_ICONS[id]||'cpu');
export const specIcon=(id:string)=>icon(SPEC_ICONS[id]||'cpu');
export const actionIcon=(id:string)=>ACTION_ICONS[id]?icon(ACTION_ICONS[id]):'';
