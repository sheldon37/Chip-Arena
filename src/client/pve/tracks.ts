// PvE presentation tracks (design §7.3, §5.5). Pure classes fed with snapshots: no DOM, no clocks. Every value they hand to
// markup is stable across 10 Hz redraws (hud-morph keeps elements whose class and data-* match), so CSS animations never
// restart or double-count time.
import type {Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,PVE_EVENT,PVE_TIMING,decodeAdd,pveOf,type AddAttack,type PveView} from '../../shared/pve/contract.js';

/** Seconds a `+$10` popup stays on screen. */
export const MONEY_POPUP_SECONDS=1.6;
/** Seconds the chip screen keeps blinking after a hit (§5.5). */
export const ATTACKED_WINDOW=1.2;

export interface MoneyPopup{
 /** Stable identity: `e<event id>` for event popups, `d<n>` for total-delta fallbacks. */ id:string;
 amount:number; at:number;
 /** Negative CSS delay fixed when the popup was first seen (so a late popup starts mid-flight, and redraws keep it). */ delay:number;
 reason?:string}

/** Money gains for one player. Event popups come from `pve-kill` (the killer's reward; the economy's own `kill` grant is
 *  silent) and `pve-cash`; spending (`pve-buy`, a paid `pve-revive`) shows as a red `−$`. When the event ring dropped
 *  events between snapshots, the unexplained part of the authoritative total's increase becomes one fallback popup. */
export class MoneyTrack{
 popups:MoneyPopup[]=[];private last=0;private generation=-1;private time=-1;private money:number|undefined;private synthetic=0;
 clear(){this.popups=[];this.last=0;this.generation=-1;this.time=-1;this.money=undefined;}
 update(state:Snapshot,me:string):MoneyPopup[]{
  const pve=pveOf(state);if(!pve){this.clear();return this.popups;}
  if(state.time<this.time-.25||this.generation!==(state.generation||0))this.clear();
  this.generation=state.generation||0;this.time=state.time;
  let gained=0,spent=0;
  const push=(id:string,amount:number,at:number,reason?:string)=>{const age=state.time-at;if(age>=MONEY_POPUP_SECONDS||amount===0)return;
   this.popups.push({id,amount,at,delay:-Math.max(0,age),reason});};
  for(const e of state.events){
   if(e.id<=this.last)continue;this.last=e.id;const amount=Math.round(Number(e.amount)||0);
   if(e.type===PVE_EVENT.kill&&e.source===me&&amount>0){gained+=amount;push('e'+e.id,amount,e.time,'kill');}
   else if(e.type===PVE_EVENT.cash&&e.target===me&&amount!==0){if(amount>0)gained+=amount;else spent-=amount;push('e'+e.id,amount,e.time,e.message);}
   else if(e.type===PVE_EVENT.buy&&e.target===me&&amount>0){spent+=amount;push('e'+e.id,-amount,e.time,e.message);}
   else if(e.type===PVE_EVENT.revive&&e.source===me&&amount>0){spent+=amount;push('e'+e.id,-amount,e.time,'revive');}
  }
  const row=pve.party[me];
  if(row){if(this.money!==undefined){const residual=Math.round(row.money-this.money+spent-gained);if(residual>=1)push('d'+(++this.synthetic),residual,state.time,'total');}this.money=row.money;}
  this.popups=this.popups.filter(p=>state.time-p.at<MONEY_POPUP_SECONDS&&state.time-p.at>=-.5).slice(-6);
  return this.popups;
 }
}

/** Windup length (attack start → damage) for an add attack. */
export const windupSeconds=(attack:AddAttack)=>attack==='smash'?PVE_TIMING.smashHit:attack==='lunge'?PVE_TIMING.lungeHit:PVE_TIMING.swipeHit;
/** True while an add whose target slot is `me` is winding up (attackAt ≤ time < attackAt+hit), or the boss's move aims at me. */
export function threatenedBy(state:Snapshot,me:string,pve:PveView|undefined=pveOf(state)):'add'|'boss'|null{
 if(!pve)return null;
 for(const t of pve.adds){const a=decodeAdd(t,pve.slots);if(a.target!==me||a.attackAt===undefined||a.state==='dead'||a.state==='entering')continue;
  if(state.time>=a.attackAt&&state.time<a.attackAt+windupSeconds(a.attack))return 'add';}
 const move=state.players.find(p=>p.id===BOSS_PLAYER_ID)?.siren?.move;
 if(move&&move.target===me&&state.time>=move.at&&state.time<move.hitAt)return 'boss';
 return null;
}
export type AttackedSource='hurt'|'hit'|'add'|'boss';
/** The chip screen's "under attack" signal (§5.5): the authoritative `hurtAt`, a local `hit` event on me, or an early
 *  warning from a windup that targets me. Stays on for ATTACKED_WINDOW after the latest hurt or hit. */
export class AttackedTrack{
 private last=0;private generation=-1;private time=-1;private hitAt=-1e9;
 clear(){this.last=0;this.generation=-1;this.time=-1;this.hitAt=-1e9;}
 update(state:Snapshot,me:string):{attacked:boolean;source:AttackedSource|null}{
  if(state.time<this.time-.25||this.generation!==(state.generation||0))this.clear();
  this.generation=state.generation||0;this.time=state.time;
  for(const e of state.events){if(e.id<=this.last)continue;this.last=e.id;if(e.type==='hit'&&e.target===me&&(e.amount||0)>0)this.hitAt=Math.max(this.hitAt,e.time);}
  const pve=pveOf(state),hurtAt=pve?.party[me]?.hurtAt;
  if(hurtAt!==undefined&&state.time-hurtAt<ATTACKED_WINDOW&&state.time>=hurtAt)return {attacked:true,source:'hurt'};
  if(state.time-this.hitAt<ATTACKED_WINDOW&&state.time>=this.hitAt)return {attacked:true,source:'hit'};
  const threat=threatenedBy(state,me,pve);
  return threat?{attacked:true,source:threat}:{attacked:false,source:null};
 }
}

/** Memoised negative animation delays: the first sighting of `key` fixes `-(time-at)`; later redraws reuse it, so a
 *  banner or popup that is already playing is never re-keyed. Resets on a rewind or a new generation. */
export class DelayMemo{
 private delays=new Map<string,number>();private time=-1;private generation=-1;
 clear(){this.delays.clear();this.time=-1;this.generation=-1;}
 frame(time:number,generation=0){if(time<this.time-.25||generation!==this.generation)this.delays.clear();this.time=time;this.generation=generation;
  if(this.delays.size>64)for(const k of [...this.delays.keys()].slice(0,32))this.delays.delete(k);}
 delay(key:string,at:number,time=this.time){let d=this.delays.get(key);if(d===undefined){d=-Math.max(0,time-at);this.delays.set(key,d);}return d;}
}
