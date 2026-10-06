// PvE personal chip picks (design §4.4, §5; S3). Pure helpers: lazy offer rolls by drop tier, the pick guard, live chip
// install and the party-bot choice. Randomness comes only from the caller's seeded stream (never sim.random()).
import {CHIPS,RARITIES,eligibleChips,magazineCapacity,offerChips,statsFor,draftChoiceCount,type Rarity} from '../content.js';
import {canUse} from '../access.js';
import type {Player,Simulation} from '../simulation.js';
import type {DropTier} from './contract.js';
import {OFFER_WEIGHTS,PICKS} from './balance.js';
import {MONEY_CHIP_BY_ID,rollMoneyChip} from './money-chips.js';

/** Every chip a PvE pick can offer: the catalogue plus the PvE-only money chips (never in CHIPS, so never in PvP drafts). */
const CHIP_BY_ID=new Map([...CHIPS,...MONEY_CHIP_BY_ID.values()].map(c=>[c.id,c]));
/** Rarity order used to rank offers (higher index = rarer). Exotic kit tiers rank with mythic. */
const RANK:Partial<Record<Rarity,number>>={common:0,uncommon:1,rare:2,epic:3,legendary:4,mythic:5,l44t:5,ansatsuken:5,mishima:5};

/** Offer weights for a drop tier and kit: OFFER_WEIGHTS replaces RARITIES weights; the leet kit adds l44t at the row's legendary weight. */
export function offerWeights(tier:DropTier,spec:string):Partial<Record<Rarity,number>>{
 const row={...OFFER_WEIGHTS[tier]};
 if(spec==='leet'&&tier!=='rainbow')row.l44t=row.legendary??0;
 return row;
}
/** Offer count: options.draftChoices (default 6) or PICKS.rainbowCount for rainbow. */
export const offerCount=(tier:DropTier,draftChoices:unknown)=>tier==='rainbow'?PICKS.rainbowCount:draftChoiceCount(draftChoices);
/** Rolls the offers of a pick (D17: called on first open). Rainbow: mythic only, exclusives off (legendary fills an empty mythic pool). */
export function rollOffers(p:Pick<Player,'spec'|'chips'>,tier:DropTier,next:()=>number,count:number):string[]{
 const offers=tier==='rainbow'?offerChips(p.spec,p.chips,next,count,{weights:{mythic:1},exclusives:false})
  :offerChips(p.spec,p.chips,next,count,{weights:offerWeights(tier,p.spec)});
 // Siren chips are reserved for the boss (D23); they are never eligible for a human kit, this is a guard only.
 return offers.filter(id=>CHIP_BY_ID.get(id)?.rarity!=='siren');
}
/** PvE-only money chips (money-chips.ts): an ordinary pick may swap its last offer for one, drawn from `next` after the
 *  catalogue offers. Rainbow picks and picks with no free money chip are unchanged. */
export function withMoneyOffer(p:Pick<Player,'chips'>,tier:DropTier,next:()=>number,offers:string[],count:number):string[]{
 const money=rollMoneyChip(p.chips,tier,next);if(!money||offers.includes(money.id))return offers;
 const out=offers.slice(0,Math.max(0,count-1));out.push(money.id);return out;
}
/** The selectChip kit guards (simulation.ts selectChip) without the draft-phase checks; true when `chip` may be installed. */
export function chipAllowed(sim:Simulation,p:Player,chip:string){
 const c=CHIP_BY_ID.get(chip);if(!c||c.rarity==='siren'||c.tags.includes('siren-boss'))return false;
 if(chip.startsWith('mishima-')&&!sim.emperor.active(p))return false;
 if(chip.startsWith('arcade-')&&!sim.arcade.active(p))return false;
 if(c.rarity==='l44t'&&(!canUse(p,'weapon:leet')||p.spec!=='leet'))return false;
 return true;
}
/** True while `chip` is still eligible for p's current kit and chips (an earlier pick may have maxed it). */
export const stillEligible=(p:Player,chip:string)=>{const money=MONEY_CHIP_BY_ID.get(chip);return money?(p.chips[chip]||0)<money.max:eligibleChips(p.spec,p.chips).some(c=>c.id===chip);};
/** Live install (§5.2): new stats keep the damage already taken; resources are clamped to the new caps. Dead players keep hp 0. */
export function applyChipsLive(p:Player){
 const old=p.stats.hp;p.stats=statsFor(p.chips,p.spec);
 if(p.hp>0)p.hp=Math.min(p.stats.hp,p.hp+Math.max(0,p.stats.hp-old));
 p.mana=Math.min(p.mana,p.stats.maxMana);p.stamina=Math.min(p.stamina,p.stats.maxStamina);p.ammo=Math.min(p.ammo,magazineCapacity(p.spec,p.stats));
}
/** Party-bot choice: the highest-rarity offer (ties: first). Bots earn no money, so they skip money chips. */
export function bestOffer(offers:readonly string[]):string|undefined{
 let best:string|undefined,rank=-1;
 for(const id of offers){if(MONEY_CHIP_BY_ID.has(id))continue;const r=RANK[CHIP_BY_ID.get(id)?.rarity??'common']??0;if(r>rank){rank=r;best=id;}}
 return best;
}
export const chipRarity=(id:string):Rarity|undefined=>CHIP_BY_ID.get(id)?.rarity;
export const rarityColor=(r:Rarity)=>RARITIES[r].color;
