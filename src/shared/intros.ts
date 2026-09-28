import type {Snapshot} from './simulation.js';

export const INTRO_STYLES={
 'dev-ansatsuken':'DEV · Rain Warrior',
 'dev-matrix':'DEV · L33T Throne',
 'neon-drop':'Neon Drop',
 'vector-slide':'Vector Slide',
 'signal-spin':'Signal Spin',
 'arc-salute':'Arc Salute',
 'pulse-wave':'Pulse Wave',
} as const;
export type IntroStyle=keyof typeof INTRO_STYLES;
export const INTRO={seconds:4.8,nameAt:3.2} as const;
export const introDuration=(style?:string)=>style==='dev-matrix'?8:style==='dev-ansatsuken'?7.2:INTRO.seconds;
export const introNameAt=(style?:string)=>style==='dev-matrix'?6.25:style==='dev-ansatsuken'?5.45:INTRO.nameAt;
export interface IntroSequence {started:number;players:string[];}
/** One authority-owned timeline. Joining a snapshot mid-intro never restarts it. */
export function introFrame(state:Pick<Snapshot,'phase'|'intro'|'time'|'players'>){
 if(state.phase!=='intro'||!state.intro?.players.length)return null;
 let elapsed=Math.max(0,state.time-state.intro.started),index=0;
 for(;index<state.intro.players.length-1;index++){const p=state.players.find(p=>p.id===state.intro!.players[index]);const duration=introDuration(p?.cosmetics?.intro);if(elapsed<duration)break;elapsed-=duration;}
 const player=state.players.find(p=>p.id===state.intro!.players[index]);if(!player)return null;
 const style=player.cosmetics?.intro||'neon-drop',duration=introDuration(style),nameAt=introNameAt(style),age=Math.min(duration,elapsed);
 return {player,index,total:state.intro.players.length,age,nameAt,duration,nameVisible:age>=nameAt,style:style as IntroStyle};
}
