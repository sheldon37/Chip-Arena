// PvE money Chips (owner request 2026-10-03: "add some chips in PVE modes (For now, boss fight) to increase money gain").
// They are NOT in CHIPS: PvP drafts, eligibleChips and statsFor never see them. The PvE personal pick offers them
// (picks.ts withMoneyOffer, called from Economy.roll) and Economy.grant applies them through moneyWithChips.
import {CHIPS,RARITIES,type Chip,type Rarity} from '../content.js';
import type {DropTier} from './contract.js';
import {OFFER_WEIGHTS} from './balance.js';

/** Money reasons that count as earnings (refunds and start money never scale). */
export const KILL_REASONS=['kill','assist','recruit','possessed'] as const;
export const BOSS_REASONS=['boss','boss-threshold'] as const;
export const EARN_REASONS=[...KILL_REASONS,'head','wave-clear',...BOSS_REASONS] as const;
export interface MoneyEffect{
 /** Fractional bonus per copy (0.15 = +15%) on these reasons. */
 bonus?:number;reasons?:readonly string[];
 /** Flat dollars per copy added to each grant with this reason (before percentage bonuses). */
 flat?:Partial<Record<string,number>>;
}
export interface MoneyChip extends Chip{money:MoneyEffect}
const good=(text:string)=>({text,tone:'good' as const});
const chip=(id:string,rarity:Rarity,name:string,description:string,max:number,stats:string[],money:MoneyEffect):MoneyChip=>
 ({id,rarity,name,description,tags:['pve-money'],max,icon:'$',effects:[],mechanic:'pve-money',stats:stats.map(good),money});

export const PVE_MONEY_CHIPS:MoneyChip[]=[
 chip('bounty-scanner','common','Bounty Scanner','Kills and assists pay out more.',4,['+15% Kill money'],{bonus:.15,reasons:KILL_REASONS}),
 chip('payday-loop','uncommon','Payday Loop','Every cleared wave pays a bonus.',3,['+$25 per wave clear'],{flat:{'wave-clear':25}}),
 chip('headhunter','uncommon','Headhunter Contract','Headshot kills pay a bounty.',3,['+$6 per headshot kill'],{flat:{head:6}}),
 chip('boss-contract','rare','Boss Contract','Damaging the boss pays more.',2,['+30% Boss money'],{bonus:.3,reasons:BOSS_REASONS}),
 chip('gold-rush','epic','Gold Rush','All money you earn is increased.',1,['+30% All money earned'],{bonus:.3,reasons:EARN_REASONS}),
 chip('midas-core','legendary','Midas Core','Everything you touch pays out.',1,['+50% All money earned','+$40 per wave clear'],{bonus:.5,reasons:EARN_REASONS,flat:{'wave-clear':40}}),
];
export const MONEY_CHIP_BY_ID=new Map(PVE_MONEY_CHIPS.map(c=>[c.id,c]));
export const isMoneyChip=(id:string)=>MONEY_CHIP_BY_ID.has(id);
/** Every chip a pilot can own and a card can show: the catalogue, then the PvE money chips. */
export const CARD_CHIPS:readonly Chip[]=[...CHIPS,...PVE_MONEY_CHIPS];
const CARD_BY_ID=new Map(CARD_CHIPS.map(c=>[c.id,c]));
export const cardChip=(id:string):Chip|undefined=>CARD_BY_ID.get(id);

/** A grant after p's money Chips: (amount + flat) × (1 + Σ bonus). Only positive earnings with an earning reason change. */
export function moneyWithChips(chips:Record<string,number>|undefined,reason:string,amount:number){
 if(!chips||!(amount>0)||!(EARN_REASONS as readonly string[]).includes(reason))return amount;
 let flat=0,bonus=0;
 for(const c of PVE_MONEY_CHIPS){const n=Math.min(c.max,Math.max(0,Math.floor(chips[c.id]||0)));if(!n)continue;
  flat+=(c.money.flat?.[reason]??0)*n;if(c.money.bonus&&c.money.reasons?.includes(reason))bonus+=c.money.bonus*n;}
 return (amount+flat)*(1+bonus);
}

/** Chance that an ordinary (non-rainbow) PvE pick offers one money Chip in its last slot. */
export const MONEY_OFFER_CHANCE=.3;
/** Money Chips p can still install (below their copy cap). */
export const eligibleMoneyChips=(chips:Record<string,number>)=>PVE_MONEY_CHIPS.filter(c=>(chips[c.id]||0)<c.max);
/** Rolls a money Chip for a pick of `tier` (rarity by the tier's OFFER_WEIGHTS row; empty tiers renormalize), or undefined. */
export function rollMoneyChip(chips:Record<string,number>,tier:DropTier,next:()=>number):MoneyChip|undefined{
 if(tier==='rainbow'||next()>=MONEY_OFFER_CHANCE)return undefined;
 const pool=eligibleMoneyChips(chips),row=OFFER_WEIGHTS[tier];if(!pool.length)return undefined;
 const tiers=(Object.keys(RARITIES) as Rarity[]).filter(r=>(row[r]??0)>0&&pool.some(c=>c.rarity===r));
 if(!tiers.length)return pool[Math.min(pool.length-1,Math.floor(next()*pool.length))];
 let roll=next()*tiers.reduce((n,r)=>n+(row[r]??0),0),pick=tiers[tiers.length-1];
 for(const r of tiers){roll-=row[r]??0;if(roll<0){pick=r;break;}}
 const choices=pool.filter(c=>c.rarity===pick);return choices[Math.min(choices.length-1,Math.floor(next()*choices.length))];
}
