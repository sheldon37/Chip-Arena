import type {PlayerView} from './simulation.js';
export const RAGING_DEMON={contact:.55,combo:.72,hits:[.80,1.02,1.24,1.46,1.69,1.94,2.21,2.56] as readonly number[],symbol:2.90,reveal:3.55,end:4.25} as const;
export function demonBeat(age:number){let index=-1;for(let i=0;i<RAGING_DEMON.hits.length;i++)if(age>=RAGING_DEMON.hits[i])index=i;return {index,age:index<0?0:age-RAGING_DEMON.hits[index]};}
/** Victim-only cinematic clock. Spectators see the same combo in the arena. */
export function demonScreen(p:PlayerView|undefined,time:number){if(!p||p.hp>0||p.death?.effect!=='dev-raging-demon')return null;const age=time-p.death.at;if(age<RAGING_DEMON.contact||age>=RAGING_DEMON.end)return null;return {age,symbol:age>=RAGING_DEMON.symbol&&age<RAGING_DEMON.reveal+.25,black:age<RAGING_DEMON.combo,opacity:age<=RAGING_DEMON.reveal?1:Math.max(0,1-(age-RAGING_DEMON.reveal)/(RAGING_DEMON.end-RAGING_DEMON.reveal))};}
