import {SIREN_INTRO} from './siren-contract.js';
import {WEAPON_INTROS,weaponIntroFor,type WeaponIntro} from './weapon-intros.js';
import {canUse} from './access.js';
import type {PlayerView,Snapshot} from './simulation.js';

export const INTRO_STYLES={
 'dev-siren':'DEV · Black Siren Protocol',
 'dev-emperor':'DEV · Emperor’s Oath',
 'dev-ansatsuken':'DEV · Rain Warrior',
 'dev-matrix':'DEV · L33T Throne',
 ...Object.fromEntries(Object.entries(WEAPON_INTROS).map(([key,value])=>[key,value.name])) as Record<WeaponIntro,string>,
 'neon-drop':'Weapon Signature',
 'vector-slide':'Vector Slide',
 'signal-spin':'Signal Spin',
 'arc-salute':'Arc Salute',
 'pulse-wave':'Pulse Wave',
} as const;
export type IntroStyle=keyof typeof INTRO_STYLES;
export function effectiveIntro(p?:Pick<PlayerView,'spec'|'cosmetics'|'developer'|'unlocks'>):IntroStyle{
 if(p?.spec==='siren'&&canUse(p,'weapon:siren'))return 'dev-siren';
 const selected=p?.cosmetics?.intro,allowed=selected&&Object.hasOwn(INTRO_STYLES,selected)&&(!selected.startsWith('dev-')||canUse(p,'intro:'+selected));
 if(p?.cosmetics?.overrideWeaponIntro===true&&allowed)return selected;
 if(p?.spec==='emperor'&&canUse(p,'weapon:emperor'))return 'dev-emperor';
 if(p?.spec==='leet'&&canUse(p,'weapon:leet'))return 'dev-matrix';
 if(p?.spec==='ansatsuken'&&canUse(p,'weapon:ansatsuken'))return 'dev-ansatsuken';
 // Older unsanitized snapshots retain their selected custom intro.
 if(p?.cosmetics?.overrideWeaponIntro===undefined&&allowed&&selected!=='neon-drop')return selected;
 return p?weaponIntroFor(p.spec)||'neon-drop':'neon-drop';
}

export const INTRO={seconds:4.8,nameAt:3.2} as const;
export const introDuration=(style?:string)=>style==='dev-siren'?SIREN_INTRO.duration:style==='dev-emperor'?7.8:style==='dev-matrix'?8:style==='dev-ansatsuken'?7.2:INTRO.seconds;
export const introNameAt=(style?:string)=>style==='dev-siren'?SIREN_INTRO.nameAt:style==='dev-emperor'?5.95:style==='dev-matrix'?6.25:style==='dev-ansatsuken'?5.45:INTRO.nameAt;
export interface IntroSequence {started:number;players:string[];/** PvE: the mid-fight boss intro (phase stays 'fight'). */boss?:boolean;}
/** One authority-owned timeline. Joining a snapshot mid-intro never restarts it. */
export function introFrame(state:Pick<Snapshot,'phase'|'intro'|'time'|'players'>){
 if(state.phase!=='intro'&&!(state.phase==='fight'&&state.intro?.boss===true)||!state.intro?.players.length)return null;
 let elapsed=Math.max(0,state.time-state.intro.started),index=0;
 for(;index<state.intro.players.length-1;index++){const p=state.players.find(p=>p.id===state.intro!.players[index]);const duration=introDuration(effectiveIntro(p));if(elapsed<duration)break;elapsed-=duration;}
 const player=state.players.find(p=>p.id===state.intro!.players[index]);if(!player)return null;
 const style=effectiveIntro(player),duration=introDuration(style),nameAt=introNameAt(style),age=Math.min(duration,elapsed);
 return {player,index,total:state.intro.players.length,age,nameAt,duration,nameVisible:age>=nameAt,style:style as IntroStyle,...(state.intro.boss===true?{boss:true as const}:{})};
}
