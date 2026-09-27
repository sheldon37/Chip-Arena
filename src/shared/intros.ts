import type {Snapshot} from './simulation.js';

export const INTRO_STYLES={
 'neon-drop':'Neon Drop',
 'vector-slide':'Vector Slide',
 'signal-spin':'Signal Spin',
 'arc-salute':'Arc Salute',
 'pulse-wave':'Pulse Wave',
} as const;
export type IntroStyle=keyof typeof INTRO_STYLES;
export const INTRO={seconds:3.6,nameAt:2.25} as const;
export interface IntroSequence {started:number;players:string[];}
/** One authority-owned timeline. Joining a snapshot mid-intro never restarts it. */
export function introFrame(state:Pick<Snapshot,'phase'|'intro'|'time'|'players'>){
 if(state.phase!=='intro'||!state.intro?.players.length)return null;
 const elapsed=Math.max(0,state.time-state.intro.started),index=Math.min(state.intro.players.length-1,Math.floor(elapsed/INTRO.seconds));
 const player=state.players.find(p=>p.id===state.intro!.players[index]);if(!player)return null;
 const age=Math.min(INTRO.seconds,elapsed-index*INTRO.seconds);
 return {player,index,total:state.intro.players.length,age,nameVisible:age>=INTRO.nameAt,style:player.cosmetics?.intro||'neon-drop' as IntroStyle};
}
