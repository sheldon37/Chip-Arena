import {chipIcon} from './icons.js';
import {emperorAura} from './emperor-card.js';
import {fightingAura} from './ansatsuken-card.js';
import {matrixRain} from './matrix-effects.js';
import {sirenAura} from './siren-card.js';
import {chipDescription} from '../shared/chip-description.js';
import {Chip,RARITIES,type Rarity} from '../shared/content.js';
export const rarityStyle=(c:Chip)=>`--rarity:${RARITIES[c.rarity].color}`;
/** Rank pips for the ordinary tiers; exclusive tiers carry their own package instead. */
const RANK:Partial<Record<Rarity,number>>={common:1,uncommon:2,rare:3,epic:4,legendary:5,mythic:6};
const PARTICLES:Partial<Record<Rarity,number>>={mishima:18,ansatsuken:20,l44t:24,siren:16,mythic:14,legendary:6};
const escape=(s:string)=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const aura=(c:Chip)=>c.rarity==='mishima'?emperorAura():c.rarity==='l44t'?matrixRain(8):c.rarity==='ansatsuken'?fightingAura():c.rarity==='siren'?sirenAura():'';
/**
 * A Chip drawn as a processor package: metal legs, a raised heat spreader holding the lit die (icon), grade and name,
 * and the effect printed on the substrate. Stack slots show copies owned and the one this install adds.
 */
export function chipCard(c:Chip,copies=0,options:{index?:number;elapsed?:number;disabled?:boolean;chosen?:boolean;/** PvE screens use their own attribute so the global draft click handler never sees them. */attr?:string}={}){
 const tier=RARITIES[c.rarity],rank=RANK[c.rarity]??0,particles=PARTICLES[c.rarity]??0,max=Math.min(12,c.max),have=Math.min(max,options.chosen?copies+1:copies);
 return `<button class="chip circuit-chip rarity-${c.rarity} ${options.index!==undefined?'draft-reveal':''} ${options.chosen?'draft-chosen':''}" style="${rarityStyle(c)};--rank:${rank};--max:${max};--have:${have};--reveal-delay:${(options.index||0)*.22-(options.elapsed||0)}s" ${options.disabled?'disabled':''} ${options.attr??'data-chip'}="${c.id}" aria-label="${escape(c.name)}, ${tier.name}. ${escape(chipDescription(c))}">${aura(c)}<span class="chip-traces" aria-hidden="true"></span><span class="chip-particles" aria-hidden="true">${Array.from({length:particles},(_,n)=>`<i style="--n:${n};--x:${(n*37+11)%100}%;--delay:${-n*.31}s"></i>`).join('')}</span><span class="circuit-header"><span class="chip-icon" aria-hidden="true">${chipIcon(c.id)}</span><span class="chip-title"><span class="rarity-label">${tier.name}${rank?'<span class="tier-pips" aria-hidden="true"></span>':''}</span><strong${c.rarity==='siren'?` data-glitch="${escape(c.name)}"`:''}>${c.name}</strong></span></span><p>${chipDescription(c)}</p><span class="chip-footer"><span>${c.tags[0]==='projectile'?'GUNS + MAGIC':c.tags[0]==='all'?'ALL WEAPONS':c.tags[0].toUpperCase()}</span><span class="chip-stack">${c.max<=6?'<span class="stack-slots" aria-hidden="true"></span>':''}${options.chosen?`×${copies+1} INSTALLED`:copies?`×${copies} → ×${copies+1}`:'INSTALL'} / ${c.max}</span></span><span class="chip-notch" aria-hidden="true"></span></button>`;
}
