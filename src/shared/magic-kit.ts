// Magic Q/E kit data (ranged/magic Q+E overhaul, design §3.1, §6). Leaf module: types only plus nothing at runtime.
// MAGIC_WEAPON holds the fire/water/lightning/darkness WEAPONS rows moved verbatim from content.ts (current values).
// FOUNDATION ships the kit rows with the design numbers but no bot data and no `live` flag: the legacy E stays in charge
// until the MAGIC stream lands the handlers (magic-abilities.ts) and sets `live`.
import type {Weapon} from './content.js';
import type {AbilityKit} from './abilities.js';
export const MAGIC_WEAPON:Record<'fire'|'water'|'lightning'|'darkness',Weapon>={
 fire:{id:'fire',name:'Fire',class:'magic',description:'22-damage firebolts, 1.5 m blast and 4 burn DPS for 2 s. E secondary: explosive fireball (25 mana).',damage:22,interval:.56,speed:24,gravity:12,range:42,impulse:210,ammo:0,reload:0,cost:8,spread:.006,windup:0,arc:0,secondary:'fireball',secondaryCooldown:3.4,color:0xff7d3e,burn:4,blast:1.5},
 water:{id:'water',name:'Water',class:'magic',description:'18-damage fast water globes. E secondary: wide pressure wave pushes players and props (22 mana).',damage:18,interval:.4,speed:31,gravity:16,range:40,impulse:340,ammo:0,reload:0,cost:6,spread:.01,windup:0,arc:0,secondary:'push',secondaryCooldown:3,color:0x65d7ed},
 lightning:{id:'lightning',name:'Lightning',class:'magic',description:'12-damage rapid bolts. Chains 6 damage to one nearby target. E secondary: close electrical nova (24 mana).',damage:12,interval:.27,speed:85,gravity:40,range:45,impulse:75,ammo:0,reload:0,cost:5,spread:.006,windup:0,arc:0,secondary:'nova',secondaryCooldown:3.5,color:0xc2a3ff,chain:6},
 darkness:{id:'darkness',name:'Darkness',class:'magic',description:'27-damage slow orbs restore 15% of direct damage. E secondary: short-range pull (20 mana).',damage:27,interval:.7,speed:15,gravity:5,range:36,impulse:140,ammo:0,reload:0,cost:10,spread:0,windup:0,arc:0,secondary:'pull',secondaryCooldown:3.8,color:0xad78d2,drain:.15}
};
export const MAGIC_KITS:Record<'fire'|'water'|'lightning'|'darkness',AbilityKit>={
 fire:{
  q:{id:'flame-lance',slot:'q',name:'Flame Lance',short:'LANCE',text:'A fast piercing flame javelin: 30 damage and a strong burn.',kind:'shot',fx:'flame-lance',cooldown:5,cost:16,windup:.12,lockout:.3,range:60,damage:30,burn:8,impulse:260,speed:70,gravity:4,shotRadius:.2,pierce:2},
  e:{id:'meteor-strike',slot:'e',name:'Meteor Strike',short:'METEOR',text:'Call a meteor on the aimed ground: 70 damage in 4.5 m, then burning ground.',kind:'strike',fx:'meteor-strike',cooldown:11,cost:35,windup:.25,lockout:.4,range:45,damage:70,radius:4.5,impulse:1100,fuse:.9,closeFuse:.3,duration:3}},
 water:{
  q:{id:'tidal-wave',slot:'q',name:'Tidal Wave',short:'WAVE',text:'Send a wave that knocks enemies back and slows them.',kind:'wave',fx:'tidal-wave',cooldown:6.5,cost:18,windup:.15,lockout:.3,range:16.8,damage:22,impulse:950,speed:24,gravity:0,shotRadius:1,duration:.7,speedFactor:.6,slowFor:1.5},
  e:{id:'geyser-wall',slot:'e',name:'Geyser Wall',short:'GEYSER',text:'Three geysers erupt in sequence across the aimed point: knock-up and slow.',kind:'line',fx:'geyser-wall',cooldown:11,cost:32,windup:.25,lockout:.35,range:28,damage:32,radius:2.5,impulse:1100,fuse:.45,closeFuse:.3,speedFactor:.6,slowFor:1}},
 lightning:{
  q:{id:'blink-strike',slot:'q',name:'Blink Strike',short:'BLINK',text:'Teleport up to 9 m, shocking enemies along the path.',kind:'blink',fx:'blink-strike',cooldown:6,cost:16,windup:.05,lockout:.2,range:9,damage:24,radius:2.4},
  e:{id:'ball-lightning',slot:'e',name:'Ball Lightning',short:'BALL',text:'A slow orb that zaps nearby enemies, then bursts.',kind:'orb',fx:'ball-lightning',cooldown:12,cost:38,windup:.25,lockout:.35,range:33,damage:40,radius:3.5,speed:11,gravity:0,shotRadius:.45,duration:3,tick:.3}},
 darkness:{
  q:{id:'shadow-shackles',slot:'q',name:'Shadow Shackles',short:'SHACKLES',text:'A dark bolt that shackles (strong slow) and heals you.',kind:'shot',fx:'shackles',cooldown:7,cost:18,windup:.1,lockout:.3,range:40,damage:20,impulse:0,speed:48,gravity:3,shotRadius:.3,speedFactor:.4,slowFor:1.2},
  e:{id:'singularity',slot:'e',name:'Black Hole',short:'HOLE',text:'A vortex pulls enemies in, then collapses.',kind:'zone',fx:'singularity',cooldown:14,cost:40,windup:.3,lockout:.4,range:35,damage:55,radius:6,duration:2.2,force:18,tick:8}},
};
