// Ability HUD contract (design §3.15; FOUNDATION minimal markup, PRESENTATION styles it). Pure functions of the snapshot.
import {abilityClient,kitFor,abilityActive,abilityCooldown,abilityCost,maxCharges,type HostCaps} from '../shared/abilities.js';
import {statsFor,WEAPONS} from '../shared/content.js';
import type {PlayerView} from '../shared/simulation.js';
export interface AbilitySlotView{key:'Q'|'E';name:string;short:string;mode:'hold'|'press';state:'ready'|'cooldown'|'active'|'casting'|'nomana';
 remaining:number;max:number;fraction:number;cost?:number;affordable:boolean;charges?:{n:number;max:number}}
/** Q and E cards for a non-DEV ranged/magic pilot on a capable host; [] otherwise (main.ts keeps the legacy E card). */
export function abilitySlots(me:PlayerView,time:number,caps:HostCaps|null|undefined):AbilitySlotView[]{
 if(!abilityClient(caps,me.spec))return [];const kit=kitFor(me.spec)!,stats=statsFor(me.chips,me.spec),w=WEAPONS[me.spec],magic=w.class==='magic',out:AbilitySlotView[]=[];
 const casting=(slot:'q'|'e')=>me.abilityCastAt!==undefined&&me.abilityCastAt>time&&(slot==='q'?(me.lastAbility??-10)>=me.lastSecondary:me.lastSecondary>(me.lastAbility??-10));
 const card=(key:'Q'|'E',name:string,short:string,remaining:number,max:number,cost:number,charges?:{n:number;max:number}):AbilitySlotView=>{
  cost*=stats.resourceCost;const affordable=!magic||me.mana>=cost,slot=key==='Q'?'q':'e',empty=charges?charges.n<=0:remaining>0;
  return {key,name,short,mode:'press',state:casting(slot)?'casting':!affordable?'nomana':empty&&remaining>0?'cooldown':'ready',remaining,max,fraction:max>0?Math.max(0,Math.min(1,1-remaining/max)):1,...(cost?{cost}:{}),affordable,...(charges?{charges}:{})};};
 if(kit.q.kind==='ads'&&kit.ads)out.push({key:'Q',name:kit.q.name,short:kit.q.short,mode:'hold',state:me.adsSince!==undefined?'active':'ready',remaining:0,max:0,fraction:1,affordable:true});
 else if(abilityActive(me.spec,'q'))out.push(card('Q',kit.q.name,kit.q.short,me.abilityCd??0,abilityCooldown(kit.q,stats),abilityCost(kit.q,stats)));
 if(me.chips.necronomicon&&magic)out.push(card('E','Raise the Dead','RAISE THE DEAD',me.secondaryCd,10,35));
 else if(abilityActive(me.spec,'e')){const max=maxCharges(stats);out.push(card('E',kit.e.name,kit.e.short,me.secondaryCd,abilityCooldown(kit.e,stats),abilityCost(kit.e,stats),max>1?{n:me.eCharges??max,max}:undefined));}
 else out.push(card('E','Special','SPECIAL',me.secondaryCd,w.secondaryCooldown,0));
 return out;
}
/** `.cooldown.ability` cards: `--p` is the recharge fraction and `data-state` the slot state. '' when there are no slots. */
export function abilitySlotsHtml(slots:AbilitySlotView[]){return slots.map(s=>{const label=s.state==='active'?'AIMING':s.state==='casting'?'CASTING':s.state==='nomana'?'NO MANA':s.state==='cooldown'?s.remaining.toFixed(1)+'s':'READY';
 return `<div class="cooldown ability" data-slot="${s.key}" data-state="${s.state}" style="--p:${s.fraction.toFixed(3)}"><small>${s.key} · ${s.short}</small><strong class="${s.state==='ready'||s.state==='active'?'ready':''}">${label}${s.charges?` <b>${s.charges.n}/${s.charges.max}</b>`:''}</strong>${s.cost?`<small>${Math.round(s.cost)} mana</small>`:''}</div>`;}).join('');}
