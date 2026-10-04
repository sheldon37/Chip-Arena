// Interact prompts above the crosshair (design §7.3, §4.3). Pure client derivation from the fixed layout, positions and the
// authoritative party rows; the authority decides what F actually does.
import type {Snapshot} from '../../shared/simulation.js';
import type {PveView} from '../../shared/pve/contract.js';
import {PRICES,RECRUIT} from '../../shared/pve/balance.js';
import {PVE_MACHINES,machinesPresent} from '../../shared/pve/layout.js';
import {inVentShaft} from '../../shared/pve/vent-pits.js';
import {escapeHtml,formatCash,tierName} from './hud.js';

export interface PvePrompt{kind:'take'|'revive'|'chip'|'recruit'|'med';key:string;text:string;afford:boolean;note?:string}
const flat=(a:{x:number;z:number},b:{x:number;z:number})=>Math.hypot(a.x-b.x,a.z-b.z);

/** The single prompt for me, by priority: my pickup, a downed teammate (one F press revives at once), the nearest machine. */
export function promptFor(state:Snapshot,pve:PveView,me:string):PvePrompt|null{
 const self=state.players.find(p=>p.id===me),row=pve.party[me];
 if(!self||!row||row.kind==='bot'||self.hp<=0||pve.hold||state.phase!=='fight')return null;
 const pickup=pve.pickups.filter(k=>k.owner===me&&k.born<=state.time&&flat(k.pos,self.pos)<=PRICES.grabRadius).sort((a,b)=>flat(a.pos,self.pos)-flat(b.pos,self.pos))[0];
 if(pickup)return {kind:'take',key:'take-'+pickup.id,text:`Take ${tierName(pickup.tier)} chip`,afford:true};
 for(const id of Object.keys(pve.party)){if(id===me)continue;const p=state.players.find(x=>x.id===id),r=pve.party[id];
  if(!p||p.hp>0||!r.connected||inVentShaft(p.death,state.time))continue;const at=p.death?.pos??p.pos;
  if(flat(at,self.pos)<=PRICES.reviveRadius)return {kind:'revive',key:'revive-'+id,text:`Revive ${p.name} ${formatCash(r.reviveCost)}`,afford:row.money>=r.reviveCost};}
 if(!machinesPresent(state))return null;
 const m=PVE_MACHINES.filter(m=>flat(m.front,self.pos)<=PRICES.interactRadius).sort((a,b)=>flat(a.front,self.pos)-flat(b.front,self.pos))[0];
 if(!m)return null;
 if(m.kind==='chip')return {kind:'chip',key:'chip-'+m.m,text:`Buy chip ${formatCash(row.chipPrice)}`,afford:row.money>=row.chipPrice};
 if(m.kind==='recruit')return {kind:'recruit',key:'recruit-'+m.m,text:`Recruit ${formatCash(RECRUIT.price)}`,afford:row.money>=RECRUIT.price};
 const cooldown=row.healReadyAt-state.time;
 const note=row.money<PRICES.heal?undefined:cooldown>0?'Cooling down':self.hp>=self.maxHp?'Full health':undefined;
 return {kind:'med',key:'med-'+m.m,text:`Heal ${formatCash(PRICES.heal)}${cooldown>0?` (${Math.ceil(cooldown)}s)`:''}`,afford:row.money>=PRICES.heal&&cooldown<=0&&self.hp<self.maxHp,note};
}
/** Markup: `F · Buy chip $175`, `F · Revive Ana $150`. */
export function promptHud(p:PvePrompt|null){
 if(!p)return '';
 return `<div class="pve-prompt" data-prompt="${p.kind}" data-afford="${p.afford?1:0}" role="status"><kbd>F</kbd><span>${escapeHtml(p.text)}</span>${p.afford?'':`<em>${escapeHtml(p.note??'Not enough money')}</em>`}</div>`;
}
