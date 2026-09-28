import type {Chip} from './content.js';
const chip=(id:string,name:string,description:string,icon:string):Chip=>({id:'mishima-'+id,name,description,icon,rarity:'mishima',tags:['emperor'],max:1,effects:[],mechanic:'mishima-'+id});
export const MISHIMA_CHIPS:Chip[]=[
 chip('godfist','Perfect Electric','Electric hooks recover much faster. Keep the lightning chain alive.','ϟ'),
 chip('twin','Twin Thunder','Each electric hook leaves a second lightning strike at the impact point.','Ⅱ'),
 chip('screw','Screwdriver','Every third airborne hit spins the enemy sideways and deals extra damage.','⟳'),
 chip('bound','Bound Breaker','The first landing in each juggle bounces the enemy back up and deals damage.','↟'),
 chip('magnet','Magnetic Fists','Electric hooks steer airborne enemies into your next punch while still carrying them back.','⌁'),
 chip('wall','Wall Tyrant','Electric hooks carry enemies farther. Their first wall collision adds a damaging wall bounce.','▥'),
 chip('armor','Iron Wavedash','Take half damage while wavedashing. You can still be interrupted and knocked back.','◇'),
 chip('trail','Storm Footwork','Wavedashes leave short-lived lightning that shocks enemies crossing your path.','≋'),
 chip('pressure','Pressure String','Your two jabs become a three-punch string with a heavier final hit.','Ⅲ'),
 chip('counter','Counter Voltage','Electric hooks deal greatly increased damage when they catch an enemy attacking.','!'),
 chip('rage','Emperor’s Rage','Below one-third health, your punches and electric hooks deal greatly increased damage.','♜'),
 chip('bloodline','Bloodline Surge','Confirmed electric hits restore a little health and stamina.','♥'),
];
export const EMPEROR={exclusiveChance:.85,guaranteedExclusives:2,chipRevealSeconds:4.8,electricDamage:8,electricStepSeconds:.10,electricStepSpeed:6,electricPush:3,electricPushGrowth:4.5,electricPushMax:36,echoDamage:3,electricRecovery:.40,electricHit:.10,waveSeconds:.18,waveSpeed:18,juggleSeconds:1.25,comboMemory:1.35} as const;
export const MISHIMA_INCOMPATIBLE=new Set(['echo-strike','hurricane']);
