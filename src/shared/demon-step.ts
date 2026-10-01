import {canUse} from './access.js';
/** Shared by server movement and client replay; normal dash cooldown/chips still apply. */
export const DEMON_STEP={speed:22,seconds:.30,iframeStart:.05,iframeSeconds:.20,afterimageSeconds:.28,sampleSeconds:.045} as const;
export function demonStepUser(p:{spec:string;developer?:boolean;unlocks?:string[]}){return p.spec==='ansatsuken'&&canUse(p,'weapon:ansatsuken');}
/** Only a live, authorized, active Demon Step protects against new hits. Its 50 ms startup and last 50 ms are vulnerable.
 * Use the existing authoritative dash timestamp, not the afterimage lifetime; canceled dashes lose protection immediately. */
export function demonStepInvulnerable(p:{spec:string;developer?:boolean;unlocks?:string[];hp:number;dashTime?:number;arcade?:{demonStepAt?:number}}|undefined,time:number){
 if(!p||!demonStepUser(p)||p.hp<=0||(p.dashTime||0)<=0||p.arcade?.demonStepAt===undefined)return false;
 const start=p.arcade.demonStepAt+DEMON_STEP.iframeStart;
 return time>=start&&time<start+DEMON_STEP.iframeSeconds;
}
