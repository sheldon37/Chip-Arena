// Personal chip screen (design §7.6, §5). A self-managed overlay root (#pve-chips), like PlayerBoard: the cards are written
// once per pick/offers key, and the attacked state toggles in place, so card animations never restart under fire.
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import type {Chip} from '../../shared/content.js';
import {cardChip} from '../../shared/pve/money-chips.js';
import {pveOf,type PvePick} from '../../shared/pve/contract.js';
import {chipCard} from '../chip-card.js';
import {draftPilot,draftTray} from '../draft-presentation.js';
import {draftRows,draftPerRow} from '../draft-timeline.js';
import {icon} from '../icons.js';
import {escapeHtml,sourceLabel,tierColor} from './hud.js';

export interface ChipScreenModel{pick:PvePick;queued:number;offers:Chip[];copies:Record<string,number>;openAt:number;pilot?:PlayerView}
/** The open pick of `me`, or null. */
export function chipScreenModel(state:Snapshot,me:string):ChipScreenModel|null{
 const pve=pveOf(state),row=pve?.party[me];if(!row?.open)return null;
 const pick=row.picks.find(p=>p.id===row.open);if(!pick)return null;
 const self=state.players.find(p=>p.id===me);
 return {pick,queued:Math.max(0,row.picks.length-1),offers:(pick.offers??[]).map(cardChip).filter((c):c is Chip=>!!c),copies:self?.chips??{},openAt:row.openAt??state.time,pilot:self};
}
/** Shown while my pick is open in a live fight and no cinematic covers the screen (§7.5: hidden during every cinematic). */
export function chipScreenVisible(state:Snapshot,me:string,locallyClosed=false){
 const pve=pveOf(state);return !!pve&&!locallyClosed&&state.phase==='fight'&&!pve.cinematic&&!!chipScreenModel(state,me);
}
/** Presentation-only adapter for the existing PvP pilot stage. Never send this view to the simulation, input,
 * sound or network: the real snapshot stays in fight, with the same personal open pick and vulnerability. */
export function chipDraftSnapshot(state:Snapshot,me:string):Snapshot|undefined{
 if(!chipScreenVisible(state,me))return;const m=chipScreenModel(state,me)!;if(!m.pilot)return;
 return {...state,phase:'draft',draft:{player:me,turn:1,total:1,started:m.openAt,readyAt:m.openAt},
  players:state.players.map(p=>p.id===me?{...p,offers:m.offers.map(c=>c.id)}:p)};
}
export const alarmHtml=(attacked:boolean)=>attacked?`<div class="pve-alarm" role="alert">${icon('skull')}<strong>WARNING — YOU ARE UNDER ATTACK</strong><span>Choose later to defend yourself</span></div>`:'';
/** Full markup (pure). Cards use `data-pve-chip`, never `data-chip`, so the draft click handler ignores them. */
export function chipScreenHtml(m:ChipScreenModel,attacked=false,elapsed=0,width=1280,height=720){
 const rainbow=m.pick.tier==='rainbow';
 const tray=draftTray(m.offers,(c,i)=>`<div class="draft-slot pve-card-slot" data-slot="${escapeHtml(c.id)}"><kbd>${i+1}</kbd>${chipCard(c,m.copies[c.id]||0,{index:i,elapsed,attr:'data-pve-chip'})}</div>`,width,height);
 const cards=tray.html||`<div class="pve-chip-rolling">${icon('cpu')}<span>Rolling your offers…</span></div>`;
 return `<section class="pve-chip-screen ${rainbow?'rainbow ':''}draft-screen is-own" data-pve-pick="${escapeHtml(m.pick.id)}" data-attacked="${attacked?1:0}" data-count="${m.offers.length}" data-rows="${tray.rows}" style="--tier:${tierColor(m.pick.tier)}" role="dialog" aria-label="Choose a chip"><div class="pve-chip-vignette" aria-hidden="true"></div><header class="draft-top"><div class="draft-heading"><div class="eyebrow">${escapeHtml(sourceLabel(m.pick.source,m.pick.tier))}</div><h1>Make your next move</h1><p>Choose one Chip. This choice is yours alone.</p></div><div class="draft-status pve-chip-meta">${m.queued?`<span class="pve-chip-queue">${m.queued} more waiting</span>`:''}<span>You are vulnerable while choosing</span></div></header><div class="pve-alarm-slot">${alarmHtml(attacked)}</div><div class="draft-tray" role="group" aria-label="Chip offers">${cards}</div>${m.pilot?draftPilot(m.pilot,'Your pick'):''}<footer class="pve-chip-foot"><span class="fine">Press <kbd>1</kbd>–<kbd>${Math.max(1,Math.min(9,m.offers.length))}</kbd> or click a chip</span><button class="btn pve-later" data-action="pve-later">${icon('clock-3')}Choose later <kbd>C</kbd><kbd>Esc</kbd></button></footer></section>`;
}

export interface ChipScreenHandlers{pick(pick:string,chip:string):void;later():void}
/** The overlay. update() is cheap: markup is rewritten only when the pick, its offers or my copies change. */
export class PveChipScreen{
 private key='';visible=false;
 constructor(private root:HTMLElement,private handlers:ChipScreenHandlers){
  root.className='pve-chips hidden';
  root.addEventListener('click',e=>{const t=(e.target as HTMLElement).closest<HTMLElement>('button');if(!t||!root.contains(t))return;
   const screen=t.closest<HTMLElement>('[data-pve-pick]');
   if(t.dataset.pveChip&&screen?.dataset.pvePick){e.stopPropagation();handlers.pick(screen.dataset.pvePick,t.dataset.pveChip);}
   else if(t.dataset.action==='pve-later'){e.stopPropagation();handlers.later();}});
 }
 /** Card n (1-based) of the open screen, for the 1–9 keys. */
 chipAt(n:number){const b=this.root.querySelectorAll<HTMLElement>('[data-pve-chip]')[n-1],s=this.root.querySelector<HTMLElement>('[data-pve-pick]');return b&&s?{pick:s.dataset.pvePick!,chip:b.dataset.pveChip!}:null;}
 update(state:Snapshot,me:string,visible:boolean,attacked:boolean){
  const m=visible?chipScreenModel(state,me):null;this.visible=!!m;this.root.classList.toggle('hidden',!m);
  if(!m){if(this.key){this.key='';this.root.innerHTML='';}return;}
  const width=typeof innerWidth==='number'?innerWidth:1280,height=typeof innerHeight==='number'?innerHeight:720;
  const key=JSON.stringify([m.pick.id,m.pick.offers,m.queued,m.offers.map(c=>m.copies[c.id]||0),m.pilot?.name,m.pilot?.color,draftRows(m.offers.length,draftPerRow(width,height)).length]);
  if(key!==this.key){this.key=key;this.root.innerHTML=chipScreenHtml(m,attacked,Math.max(0,state.time-m.openAt),width,height);}
  const screen=this.root.querySelector<HTMLElement>('.pve-chip-screen');
  if(screen&&screen.dataset.attacked!==(attacked?'1':'0')){screen.dataset.attacked=attacked?'1':'0';const slot=this.root.querySelector('.pve-alarm-slot');if(slot)slot.innerHTML=alarmHtml(attacked);}
 }
}
