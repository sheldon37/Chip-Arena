// Magic WEAPONS rows and Q/E kits (design §6.2-§6.4); leaf module. Behaviour lives in magic-abilities.ts.
import type {Weapon} from './content.js';
import type {AbilityKit} from './abilities.js';
export const MAGIC_WEAPON:Record<'fire'|'water'|'lightning'|'darkness',Weapon>={
 fire:{id:'fire',name:'Fire',class:'magic',description:'24-damage firebolts, 1.5 m blast and 4 burn DPS for 2 s. Q: Flame Lance. E: Meteor Strike.',damage:24,interval:.5,speed:34,gravity:9,range:42,impulse:210,ammo:0,reload:0,cost:8,spread:.006,windup:0,arc:0,secondary:'meteor-strike',secondaryCooldown:11,color:0xff7d3e,burn:4,blast:1.5},
 water:{id:'water',name:'Water',class:'magic',description:'20-damage fast water globes Soak (slow) on a direct hit. Q: Tidal Wave. E: Geyser Wall.',damage:20,interval:.38,speed:42,gravity:10,range:40,impulse:340,ammo:0,reload:0,cost:6,spread:.01,windup:0,arc:0,secondary:'geyser-wall',secondaryCooldown:11,color:0x65d7ed,shotRadius:.16},
 lightning:{id:'lightning',name:'Lightning',class:'magic',description:'15-damage rapid bolts chain 6 damage to the nearest enemy. Q: Blink Strike. E: Ball Lightning.',damage:15,interval:.27,speed:85,gravity:30,range:45,impulse:75,ammo:0,reload:0,cost:5,spread:.006,windup:0,arc:0,secondary:'ball-lightning',secondaryCooldown:12,color:0xc2a3ff,chain:6},
 darkness:{id:'darkness',name:'Darkness',class:'magic',description:'34-damage heavy orbs restore 20% of direct damage. Q: Shadow Shackles. E: Black Hole.',damage:34,interval:.66,speed:26,gravity:3,range:36,impulse:140,ammo:0,reload:0,cost:10,spread:0,windup:0,arc:0,secondary:'singularity',secondaryCooldown:14,color:0xad78d2,drain:.2,shotRadius:.2}
};
export const MAGIC_KITS:Record<'fire'|'water'|'lightning'|'darkness',AbilityKit>={
 fire:{
  q:{id:'flame-lance',slot:'q',name:'Flame Lance',short:'LANCE',text:'A fast piercing flame javelin: 30 damage and a strong burn.',kind:'shot',fx:'flame-lance',cooldown:5,cost:16,windup:.12,lockout:.3,range:60,damage:30,burn:8,impulse:260,speed:70,gravity:4,shotRadius:.2,pierce:2,live:true,bot:{min:4,max:40,aim:'ballistic'}},
  e:{id:'meteor-strike',slot:'e',name:'Meteor Strike',short:'METEOR',text:'Call a meteor on the aimed ground: 70 damage in 4.5 m, then burning ground.',kind:'strike',fx:'meteor-strike',cooldown:11,cost:35,windup:.25,lockout:.4,range:45,damage:70,radius:4.5,impulse:1100,fuse:.9,closeFuse:.3,duration:3,live:true,bot:{min:6,max:40,aim:'point',lead:.6,desired:9}}},
 water:{
  q:{id:'tidal-wave',slot:'q',name:'Tidal Wave',short:'WAVE',text:'Send a wave that knocks enemies back and slows them.',kind:'wave',fx:'tidal-wave',cooldown:6.5,cost:18,windup:.15,lockout:.3,range:16.8,damage:22,impulse:950,speed:24,gravity:0,shotRadius:1,duration:.7,speedFactor:.6,slowFor:1.5,live:true,bot:{min:1,max:12,aim:'direct',priorityBelow:6}},
  e:{id:'geyser-wall',slot:'e',name:'Geyser Wall',short:'GEYSER',text:'Three geysers erupt in sequence across the aimed point: knock-up and slow.',kind:'line',fx:'geyser-wall',cooldown:11,cost:32,windup:.25,lockout:.35,range:28,damage:54,radius:2.5,impulse:1100,fuse:.45,closeFuse:.3,speedFactor:.6,slowFor:1,live:true,bot:{min:6,max:26,aim:'point',lead:.5,desired:9}}},
 lightning:{
  q:{id:'blink-strike',slot:'q',name:'Blink Strike',short:'BLINK',text:'Teleport up to 9 m, shocking enemies along the path.',kind:'blink',fx:'blink-strike',cooldown:6,cost:16,windup:.05,lockout:.2,range:9,damage:24,radius:2.4,live:true},
  e:{id:'ball-lightning',slot:'e',name:'Ball Lightning',short:'BALL',text:'A slow orb that zaps nearby enemies, then bursts.',kind:'orb',fx:'ball-lightning',cooldown:12,cost:38,windup:.25,lockout:.35,range:33,damage:40,radius:3.5,speed:11,gravity:0,shotRadius:.45,duration:3,tick:.3,live:true,bot:{min:5,max:25,aim:'direct',desired:9}}},
 darkness:{
  q:{id:'shadow-shackles',slot:'q',name:'Shadow Shackles',short:'SHACKLES',text:'A dark bolt that shackles (strong slow) and heals you.',kind:'shot',fx:'shackles',cooldown:7,cost:18,windup:.1,lockout:.3,range:40,damage:20,impulse:0,speed:48,gravity:3,shotRadius:.3,speedFactor:.4,slowFor:1.2,live:true,bot:{min:4,max:30,aim:'ballistic'}},
  e:{id:'singularity',slot:'e',name:'Black Hole',short:'HOLE',text:'A vortex pulls enemies in, then collapses.',kind:'zone',fx:'singularity',cooldown:14,cost:40,windup:.3,lockout:.4,range:35,damage:55,radius:6,duration:2.2,force:16,tick:8,live:true,bot:{min:6,max:32,aim:'point',lead:.3,desired:9}}},
};
