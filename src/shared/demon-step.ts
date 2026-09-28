import {canUse} from './access.js';
/** Shared by server movement and client replay; normal dash cooldown/chips still apply. */
export const DEMON_STEP={speed:22,seconds:.30,afterimageSeconds:.28,sampleSeconds:.045} as const;
export function demonStepUser(p:{spec:string;developer?:boolean;unlocks?:string[]}){return p.spec==='ansatsuken'&&canUse(p,'weapon:ansatsuken');}
