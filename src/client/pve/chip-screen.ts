// Personal chip screen (design §7.6, §5). A self-managed overlay root (#pve-chips), like PlayerBoard: the cards are written
// once per pick/offers key, and the attacked state toggles in place, so card animations never restart under fire.
import type {Snapshot} from '../../shared/simulation.js';
import {CHIPS,type Chip} from '../../shared/content.js';
import {pveOf,type PvePick} from '../../shared/pve/contract.js';
import {chipCard} from '../chip-card.js';
import {icon} from '../icons.js';
import {escapeHtml,sourceLabel,tierColor} from './hud.js';

export interface ChipScreenModel{pick:PvePick;queued:number;offers:Chip[];copies:Record<string,number>;openAt:number}
/** The open pick of `me`, or null. */
export function chipScreenModel(state:Snapshot,me:string):ChipScreenModel|null{
 const pve=pveOf(state),row=pve?.party[me];if(!row?.open)return null;
 const pick=row.picks.find(p=>p.id===row.open);if(!pick)return null;
 const self=state.players.find(p=>p.id===me);
 return {pick,queued:Math.max(0,row.picks.length-1),offers:(pick.offers??[]).map(id=>CHIPS.find(c=>c.id===id)).filter((c):c is Chip=>!!c),copies:self?.chips??{},openAt:row.openAt??state.time};
}
/** Shown while my pick is open in a live fight and no cinematic covers the screen (§7.5: hidden during every cinematic). */
export function chipScreenVisible(state:Snapshot,me:string,locallyClosed=false){
 const pve=pveOf(state);return !!pve&&!locallyClosed&&state.phase==='fight'&&!pve.cinematic&&!!chipScreenModel(state,me);
}
export const alarmHtml=(attacked:boolean)=>attacked?`<div class="pve-alarm" role="alert">${icon('skull')}<strong>WARNING — YOU ARE UNDER ATTACK</strong><span>Choose later to defend yourself</span></div>`:'';
/** Full markup (pure). Cards use `data-pve-chip`, never `data-chip`, so the draft click handler ignores them. */
export function chipScreenHtml(m:ChipScreenModel,attacked=false,elapsed=0){
 const rainbow=m.pick.tier==='rainbow';
 const cards=m.offers.length?m.offers.map((c,i)=>`<div class="pve-card-slot"><kbd>${i+1}</kbd>${chipCard(c,m.copies[c.id]||0,{index:i,elapsed,attr:'data-pve-chip'})}</div>`).join('')
  :`<div class="pve-chip-rolling">${icon('cpu')}<span>Rolling your offers…</span></div>`;
 return `<section class="pve-chip-screen ${rainbow?'rainbow':''}" data-pve-pick="${escapeHtml(m.pick.id)}" data-attacked="${attacked?1:0}" data-count="${m.offers.length}" style="--tier:${tierColor(m.pick.tier)}" role="dialog" aria-label="Choose a chip"><div class="pve-chip-vignette" aria-hidden="true"></div><header class="pve-chip-head"><div><div class="eyebrow">${escapeHtml(sourceLabel(m.pick.source,m.pick.tier))}</div><h2>Choose a chip</h2></div><div class="pve-chip-meta">${m.queued?`<span class="pve-chip-queue">${m.queued} more waiting</span>`:''}<span>You are vulnerable while choosing</span></div></header><div class="pve-alarm-slot">${alarmHtml(attacked)}</div><div class="pve-chip-cards" data-count="${m.offers.length}">${cards}</div><footer class="pve-chip-foot"><span class="fine">Press <kbd>1</kbd>–<kbd>${Math.max(1,Math.min(9,m.offers.length))}</kbd> or click a chip</span><button class="btn pve-later" data-action="pve-later">${icon('clock-3')}Choose later <kbd>C</kbd><kbd>Esc</kbd></button></footer></section>`;
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
  const key=JSON.stringify([m.pick.id,m.pick.offers,m.queued,m.offers.map(c=>m.copies[c.id]||0)]);
  if(key!==this.key){this.key=key;this.root.innerHTML=chipScreenHtml(m,attacked,Math.max(0,state.time-m.openAt));}
  const screen=this.root.querySelector<HTMLElement>('.pve-chip-screen');
  if(screen&&screen.dataset.attacked!==(attacked?'1':'0')){screen.dataset.attacked=attacked?'1':'0';const slot=this.root.querySelector('.pve-alarm-slot');if(slot)slot.innerHTML=alarmHtml(attacked);}
 }
}
