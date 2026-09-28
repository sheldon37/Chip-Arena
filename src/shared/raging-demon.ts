import type {PlayerView} from './simulation.js';
export const RAGING_DEMON={contact:.72,symbol:1.28,reveal:2.25,end:3.1} as const;
/** Only the defeated player's own screen is covered. Absolute age supports late snapshots. */
export function demonScreen(p:PlayerView|undefined,time:number){if(!p||p.hp>0||p.death?.effect!=='dev-raging-demon')return null;const age=time-p.death.at;if(age<RAGING_DEMON.contact||age>=RAGING_DEMON.end)return null;return {age,symbol:age>=RAGING_DEMON.symbol&&age<RAGING_DEMON.reveal+.25,opacity:age<=RAGING_DEMON.reveal?1:Math.max(0,1-(age-RAGING_DEMON.reveal)/(RAGING_DEMON.end-RAGING_DEMON.reveal))};}
