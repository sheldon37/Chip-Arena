import {fightingAura} from './ansatsuken-card.js';
import {matrixRain} from './matrix-effects.js';
import {chipDescription} from '../shared/chip-description.js';
import {Chip,RARITIES} from '../shared/content.js';
export const rarityStyle=(c:Chip)=>`--rarity:${RARITIES[c.rarity].color}`;
export function chipCard(c:Chip,copies=0,options:{index?:number;elapsed?:number;disabled?:boolean;chosen?:boolean}={}){
 const tier=RARITIES[c.rarity],particles=c.rarity==='ansatsuken'?20:c.rarity==='l44t'?24:c.rarity==='mythic'?14:c.rarity==='legendary'?6:0;
 return `<button class="chip circuit-chip rarity-${c.rarity} ${options.index!==undefined?'draft-reveal':''} ${options.chosen?'draft-chosen':''}" style="${rarityStyle(c)};--reveal-delay:${(options.index||0)*.22-(options.elapsed||0)}s" ${options.disabled?'disabled':''} data-chip="${c.id}" aria-label="${c.name}, ${tier.name}. ${chipDescription(c)}">${c.rarity==='l44t'?matrixRain(8):c.rarity==='ansatsuken'?fightingAura():''}<span class="chip-particles" aria-hidden="true">${Array.from({length:particles},(_,n)=>`<i style="--n:${n};--x:${(n*37+11)%100}%;--delay:${-n*.31}s"></i>`).join('')}</span><span class="circuit-header"><span class="chip-icon" aria-hidden="true">${c.icon}</span><span><span class="rarity-label">${tier.name}</span><strong>${c.name}</strong></span></span><p>${chipDescription(c)}</p><span class="chip-footer"><span>${c.tags[0]==='projectile'?'GUNS + MAGIC':c.tags[0]==='all'?'ALL WEAPONS':c.tags[0].toUpperCase()}</span><span>${options.chosen?`×${copies+1} INSTALLED`:copies?`×${copies} → ×${copies+1}`:'INSTALL'} / ${c.max}</span></span><span class="chip-notch" aria-hidden="true"></span></button>`;
}
