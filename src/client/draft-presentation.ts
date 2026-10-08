import {EMPEROR} from '../shared/emperor-content.js';
import {emperorCinematic} from './emperor-card.js';
import {ARCADE} from '../shared/arcade-content.js';
import {fightingCinematic} from './ansatsuken-card.js';
import {matrixRain} from './matrix-effects.js';
import {sirenCinematic} from './siren-card.js';
import {LEET} from '../shared/leet-content.js';
import {CHIPS,type Chip} from '../shared/content.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
import {DRAFT_REVEAL_SECONDS} from '../shared/draft-settings.js';
import {chipCard,rarityStyle} from './chip-card.js';
import {DRAFT_GRAB,draftRows,draftPerRow} from './draft-timeline.js';
const escape=(s:string)=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const cuts=['polygon(0 0,52% 0,37% 43%,0 32%)','polygon(52% 0,100% 0,100% 38%,37% 43%)','polygon(0 32%,37% 43%,48% 71%,0 78%)','polygon(37% 43%,100% 38%,100% 68%,48% 71%)','polygon(0 78%,48% 71%,61% 100%,0 100%)','polygon(48% 71%,100% 68%,100% 100%,61% 100%)'];
export function brokenChip(c:Chip,copies:number,index:number,elapsed:number){
 const card=chipCard(c,copies,{disabled:true});
 return `<div class="chip-shatter" aria-hidden="true" inert style="${rarityStyle(c)};--break-delay:${index*.035-elapsed}s"><div class="shatter-size">${card}</div>${cuts.map((cut,n)=>`<div class="chip-shard" style="clip-path:${cut};--dx:${(n%2?1:-1)*(45+index*5)}px;--dy:${(n-2.5)*44}px;--spin:${(n%2?1:-1)*(15+n*7)}deg">${card}</div>`).join('')}</div>`;
}
/** Server reveal hold for a chosen chip (matches Simulation.advance). */
export const revealSeconds=(c:Chip)=>c.rarity==='mishima'?EMPEROR.chipRevealSeconds:c.rarity==='l44t'?LEET.chipRevealSeconds:c.rarity==='ansatsuken'?ARCADE.chipRevealSeconds:DRAFT_REVEAL_SECONDS;
const seconds=(n:number)=>`${+n.toFixed(3)}s`;
/** `offset` starts the spotlight that many seconds after the pick (the 3D grab plays first) and shortens it so it still
 * ends with the server reveal. */
export function selectedChip(c:Chip,copies:number,picker:string,elapsed:number,offset=0){
 const timing=offset?`--selection-delay:${seconds(offset-elapsed)};--selection-duration:${seconds(revealSeconds(c)-offset)}`:`--selection-delay:${-elapsed}s;--selection-duration:${revealSeconds(c)}s`;
 return `<div class="selection-spotlight ${c.rarity==='mishima'?'emperor-selection':c.rarity==='l44t'?'matrix-selection':c.rarity==='ansatsuken'?'ansatsuken-selection':c.rarity==='siren'?'siren-selection':''}" role="status" aria-live="polite" style="${rarityStyle(c)};${timing}">${c.rarity==='mishima'?emperorCinematic():c.rarity==='l44t'?matrixRain(28):c.rarity==='ansatsuken'?fightingCinematic():c.rarity==='siren'?sirenCinematic():''}<div class="selection-focus"><div class="selection-owner">${escape(picker)} <span>INSTALLED</span></div>${chipCard(c,Math.max(0,copies-1),{disabled:true,chosen:true})}<div class="selection-confirm">${c.rarity==='mishima'?'POWER WITHOUT LIMIT':c.rarity==='l44t'?'ROOT ACCESS GRANTED':c.rarity==='ansatsuken'?'THE WARRIOR AWAKENS':c.rarity==='siren'?'THE GRIN WIDENS':'UPGRADE SECURED'} <span>×${copies}</span></div></div></div>`;
}

/** True once the pilot holds the chosen chip and the details spotlight should be shown (sim clock, any frame rate). */
export const draftDetailsOpen=(state:Snapshot,time=state.time)=>!!state.draft?.chosen&&time-(state.draft.chosenAt??time)>=DRAFT_GRAB.details-.02;
const safeColor=(value:string|undefined)=>value&&/^#[0-9a-f]{3}([0-9a-f]{3})?$/i.test(value)?value:'#ff6b35';
/** Shared offer layout for PvP and personal PvE picks; callers keep their own buttons and selection rules. */
export function draftTray<T>(offers:T[],slot:(offer:T,index:number)=>string,width=1280,height=720){
 const rows=draftRows(offers.length,draftPerRow(width,height));let start=0;
 const html=rows.map(n=>{const row=offers.slice(start,start+n).map((offer,k)=>slot(offer,start+k)).join('');start+=n;return `<div class="draft-row" style="--cols:${n}">${row}</div>`;}).join('');
 return {rows:rows.length,html};
}
export function draftPilot(p:Pick<PlayerView,'name'|'color'>,label:string){
 return `<div class="draft-pilot" style="--pilot:${safeColor(p.color)}"><i aria-hidden="true"></i><strong>${escape(p.name)}</strong><span>${escape(label)}</span></div>`;
}
/**
 * Full-screen draft over the 3D pilot stage: heading and turn order on top, the picker's nameplate, and the offers as a
 * horizontal tray (one row up to six, two rows above that). Cards stay real `<button data-chip>` elements, so the click
 * path, disabled gate and accessible names are unchanged. After the pick the chosen card stays in its slot for the grab,
 * the others shatter, and the details spotlight starts once the chip is in the pilot's hand.
 */
/** `clock` is the presentation time the 3D grab uses (the interpolated time online); the ready gate keeps the newest
 * snapshot so the cards enable exactly when the server will accept a pick. */
export function draftScreen(state:Snapshot,local:string,width=1280,height=720,clock=state.time){
 const d=state.draft,picker=state.players.find(p=>p.id===d?.player),own=!!picker&&picker.id===local,chosen=d?.chosen?CHIPS.find(c=>c.id===d.chosen):undefined;
 const elapsed=Math.max(0,clock-(d?.chosenAt??clock)),offers=picker?.offers||[];
 const title=chosen?'Upgrade secured':own?'Make your next move':`${escape(picker?.name||'Opponent')} is choosing`;
 const hint=chosen?'':own?'Choose one Chip. Everyone can see this draft.':'Watch the draft. All players see the same choices and the selected upgrade.';
 const turns=d?`<div class="draft-turns">${state.players.map(p=>`<span class="${p.id===d.player?'current':''}"><i style="background:${safeColor(p.color)}"></i>${escape(p.name)}${p.id===d.player?(chosen?' · INSTALLED':' · PICKING'):p.selected?' · READY':' · UP NEXT'}</span>`).join('')}<strong>TURN ${d.turn} / ${d.total}</strong></div>`:'';
 const slot=(chipId:string,index:number)=>{const c=CHIPS.find(c=>c.id===chipId);if(!c||!picker)return '';const copies=picker.chips[chipId]||0;
  if(chosen)return chipId===chosen.id?`<div class="draft-slot taken" data-slot="${escape(chipId)}">${chipCard(c,Math.max(0,copies-1),{disabled:true})}</div>`:`<div class="draft-slot" data-slot="${escape(chipId)}">${brokenChip(c,copies,index,elapsed)}</div>`;
  return `<div class="draft-slot" data-slot="${escape(chipId)}">${chipCard(c,copies,{index,elapsed:state.time-(d?.started||0),disabled:!own||state.time<(d?.readyAt||0)})}</div>`;};
 const tray=draftTray(offers,slot,width,height);
 return `<section class="draft-screen ${chosen?'is-chosen':''} ${own?'is-own':''}" data-rows="${tray.rows}" aria-label="Chip draft"><header class="draft-top"><div class="draft-heading"><div class="eyebrow">CHIP DRAFT / SET ${state.setNumber}</div><h1>${title}</h1>${hint?`<p>${hint}</p>`:''}</div><div class="draft-status">${turns}${chosen?'':`<div class="timer" id="timer">${Math.ceil(state.remaining)} seconds</div>`}</div></header>`
  +(picker?draftPilot(picker,chosen?'Installing':own?'Your pick':'Picking'):'')
  +`<div class="draft-tray" role="group" aria-label="Chip offers">${tray.html}</div></section>`
  +(chosen&&picker&&draftDetailsOpen(state,clock)?selectedChip(chosen,picker.chips[chosen.id],picker.name,elapsed,DRAFT_GRAB.details):'');
}
