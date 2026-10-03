// Structured stat lines for chip cards (owner request 2026-10-03): the prose description stays first, then short lines
// such as "+100% Health" (good, green) and "-12% Damage" (bad, red). Lines are derived from a Chip's declarative effects
// (per copy) unless the Chip carries explicit `stats` (PvE money chips, which have no stat effects).
import {BASE_STATS,type Chip,type ChipStatLine,type Stats} from './content.js';

/** Stats where a smaller value helps the pilot (cooldowns, costs, damage received, reload duration). */
const LOWER_IS_BETTER=new Set<keyof Stats>(['damageTaken','knockbackTaken','dashCooldown','debrisTaken','reloadSpeed','resourceCost','blockCooldown','rollCost','abilityCooldown','abilityCost']);
/** Stats whose direction is a matter of taste: printed without a green/red judgement. */
const NEUTRAL=new Set<keyof Stats>(['projectileGravity','playerGravity','recoil','fanDegrees']);
/** Additive stats whose value is a fraction (0.3 = +30%). */
const FRACTION=new Set<keyof Stats>(['lifeSteal','execute','closeDamage','farDamage','backstab','critical','thorns','reach','demolition','ammoCapacity','bounceRetention','lastRound','qEmpower','manaSurge','abilityLeech','arcaneSurge']);
const LABEL:Partial<Record<keyof Stats,string>>={hp:'Health',damage:'Damage',attackRate:'Attack speed',speed:'Move speed',damageTaken:'Damage taken',
 knockbackTaken:'Knockback taken',dashCooldown:'Dodge cooldown',impulse:'Knockback dealt',demolition:'Structure damage',debrisTaken:'Debris damage taken',
 ammoCapacity:'Magazine size',maxMana:'Max mana',maxStamina:'Max stamina',projectileSpeed:'Projectile speed',blockCooldown:'Block cooldown',
 blockWindow:'Block window',reflectDamage:'Reflected damage',reflectSpeed:'Reflected speed',blockForce:'Block knockback',manaRegen:'Mana regen',
 staminaRegen:'Stamina regen',resourceCost:'Ability cost',reloadSpeed:'Reload time',projectileGravity:'Projectile drop',projectileSize:'Projectile size',
 jumpPower:'Jump height',playerGravity:'Gravity',reach:'Melee reach',meleeArc:'Swing width',fanDegrees:'Spread',bounceRetention:'Bounce damage kept',
 lifeSteal:'Life steal',execute:'Damage to enemies below 30% health',closeDamage:'Close-range damage',farDamage:'Long-range damage',backstab:'Backstab damage',
 critical:'Critical chance',thorns:'Damage reflected',lastRound:'Last-round damage',regeneration:'Health / s regen',reloadHeal:'Health per reload',
 blockHeal:'Health per Block',blockAmmo:'Ammo per Block',dashBlast:'Dodge blast damage',recoil:'Recoil',vacuum:'Hit pull',bounces:'Bounce',
 split:'Split level',pierces:'Pierce',burn:'Burn DPS',poison:'Poison DPS',staticDamage:'Discharge damage',shrapnel:'Blast damage',
 extraProjectiles:'Projectile',extraStrikes:'Follow-up strike',homing:'Homing °/s',blockPulseDamage:'Pulse damage',blockPulseImpulse:'Pulse knockback',
 rollDistance:'Roll distance',rollSpeed:'Roll speed',rollCost:'Roll cost',rollWindow:'Roll invulnerability',
 abilityCooldown:'Ability cooldown',abilityPower:'Ability damage',abilityRadius:'Ability radius',abilityCost:'Spell mana cost',abilityCharges:'E charge',
 qEmpower:'Next bolts after Q',manaSurge:'Ability damage as mana',abilityLeech:'Ability life steal',arcaneSurge:'Ability damage at high mana'};
const SECONDS=new Set<keyof Stats>(['blockWindow','rollWindow']);
/** Switch-like stats (Pong, Boomerang, ...): the description already explains them. */
const SWITCH=new Set<keyof Stats>(['pong','boomerang','zigzag','chaos','spellEcho']);

const num=(n:number)=>{const a=Math.abs(n);return Number(a>=10?Math.round(a):a>=1?a.toFixed(1):a>=.1?a.toFixed(2):a.toFixed(3)).toString();};
const pct=(n:number)=>{const a=Math.abs(n*100);return `${Number(a>=10?Math.round(a):a.toFixed(1))}%`;};
const label=(stat:keyof Stats)=>LABEL[stat]??stat.replace(/([A-Z])/g,' $1').replace(/^./,c=>c.toUpperCase());
const tone=(stat:keyof Stats,up:boolean):ChipStatLine['tone']=>NEUTRAL.has(stat)?'neutral':up!==LOWER_IS_BETTER.has(stat)?'good':'bad';

/** One effect as a card line, or null when it has no useful number (switches, zero changes). */
export function effectLine(f:Chip['effects'][number]):ChipStatLine|null{
 const {stat,op,value}=f;if(SWITCH.has(stat))return null;
 if(stat==='staticEvery')return op==='set'?{text:`Discharge every ${num(value)} hits`,tone:'good'}:null;
 const base=BASE_STATS[stat];
 if(stat==='vacuum')return value>0?{text:'Hits pull enemies inward',tone:'good'}:null;
 if(op==='set'&&stat==='fanDegrees')return {text:`${num(value)}° ${label(stat)}`,tone:'neutral'};
 if(op==='set'){if(!base)return null;return effectLine({stat,op:'mul',value:value/base});}
 if(op==='mul'){const d=value-1;if(Math.abs(d)<1e-9)return null;return {text:`${d>0?'+':'-'}${pct(d)} ${label(stat)}`,tone:tone(stat,d>0)};}
 if(!value)return null;
 const up=value>0,sign=up?'+':'-';
 if(FRACTION.has(stat))return {text:`${sign}${pct(value)} ${label(stat)}`,tone:tone(stat,up)};
 if(stat==='fanDegrees')return {text:`${sign}${num(value)}° ${label(stat)}`,tone:tone(stat,up)};
 if(SECONDS.has(stat))return {text:`${sign}${num(value)} s ${label(stat)}`,tone:tone(stat,up)};
 const plural=['bounces','pierces','extraProjectiles','extraStrikes','abilityCharges'].includes(stat)&&Math.abs(value)!==1?'s':'';
 return {text:`${sign}${num(value)} ${label(stat)}${plural}`,tone:tone(stat,up)};
}
/** Card lines for a Chip, positives first (stable within each group). Per installed copy. */
export function chipStatLines(c:Chip):ChipStatLine[]{
 if(c.stats)return c.stats;
 const lines=c.effects.map(effectLine).filter((l):l is ChipStatLine=>!!l);
 const order={good:0,neutral:1,bad:2} as const;
 return lines.map((l,i)=>({l,i})).sort((a,b)=>order[a.l.tone]-order[b.l.tone]||a.i-b.i).map(x=>x.l);
}
