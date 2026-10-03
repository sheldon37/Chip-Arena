// Gun Q/E kit data (ranged/magic Q+E overhaul, design §3.1, §4.1, §5). Leaf module: types only plus nothing at runtime.
// GUN_WEAPON holds the pistol/rifle/machine gun WEAPONS rows moved verbatim from content.ts (current values).
// FOUNDATION ships the kit rows with the design numbers but no bot data and no `live` flag: the legacy E stays in charge
// until the GUNS stream lands the handlers (gun-abilities.ts) and sets `live`.
import type {Weapon} from './content.js';
import type {AbilityKit,AdsProfile} from './abilities.js';
export const GUN_WEAPON:Record<'pistol'|'rifle'|'machinegun',Weapon>={
 pistol:{id:'pistol',name:'Pistol',class:'ranged',description:'Precise shots. 7-round magazine. E secondary: braced shot (2× damage, 2 ammo).',damage:23,interval:.42,speed:52,gravity:32,range:55,impulse:175,ammo:7,reload:1.1,cost:0,spread:.008,windup:0,arc:0,secondary:'power',secondaryCooldown:2.2,color:0xffd88d},
 rifle:{id:'rifle',name:'Rifle',class:'ranged',description:'Sniper: 58 damage, 3 rounds, 1.15 s between shots, 2.6 s reload. Fast 150 m/s shots retain gravity. Hold Q: 3× scope. E: 78-damage braced shot (2 ammo).',damage:58,interval:1.15,speed:150,gravity:12,range:140,impulse:350,ammo:3,reload:2.6,cost:0,spread:.0015,windup:0,arc:0,secondary:'snipe',secondaryCooldown:3.8,color:0xffe7aa},
 machinegun:{id:'machinegun',name:'Machine Gun',class:'ranged',description:'Rapid automatic fire, 48-round magazine, 2.4 s reload. E secondary: six-round suppression burst (6 ammo).',damage:8,interval:.085,speed:49,gravity:36,range:48,impulse:65,ammo:48,reload:2.4,cost:0,spread:.025,windup:0,arc:0,secondary:'burst',secondaryCooldown:3,color:0xffc269,burstCount:6}
};
/** Aim-down-sights profiles (§4.1, TUNE). ADS never changes projectile speed or gravity. */
export const ADS_PROFILES:Record<'pistol'|'rifle'|'machinegun',AdsProfile>={
 pistol:{fov:55,spread:.25,settle:.15,move:.75,sens:.68,overlay:'sight',kick:.5,interval:1.12},
 machinegun:{fov:52,spread:.4,settle:.2,move:.55,sens:.63,overlay:'sight',kick:.45,bloom:.002},
 rifle:{fov:28,spread:0,settle:.3,move:.5,sens:.36,overlay:'scope',kick:.6},
};
const ads=(name:string,short:string)=>({id:'ads',slot:'q',name,short,text:'Hold Q: aim down sights. Tighter shots after a short settle; slower walking.',kind:'ads',fx:'',cooldown:0,cost:0,windup:0,lockout:0,range:0} as const);
export const GUN_KITS:Record<'pistol'|'rifle'|'machinegun',AbilityKit>={
 pistol:{ads:ADS_PROFILES.pistol,q:ads('Aim','AIM'),
  e:{id:'frag-grenade',slot:'e',name:'Frag Grenade',short:'FRAG',text:'Throw a bouncing grenade that lands where you aim. 4.2 m blast after its fuse.',kind:'throw',fx:'frag',cooldown:8,cost:0,windup:.2,lockout:.3,range:24,damage:60,radius:4.2,impulse:1000,gravity:20,fuse:.6,shotRadius:.14}},
 machinegun:{ads:ADS_PROFILES.machinegun,q:ads('Aim','AIM'),
  e:{id:'rocket',slot:'e',name:'Underbarrel Rocket',short:'ROCKET',text:'Fire a rocket that explodes on contact and breaks cover. Arms after 3 m.',kind:'shot',fx:'rocket',cooldown:9,cost:0,windup:.12,lockout:.35,range:95,damage:55,radius:3.6,impulse:1300,speed:38,gravity:2,shotRadius:.2,arm:3}},
 rifle:{ads:ADS_PROFILES.rifle,q:ads('Scope','SCOPE'),
  e:{id:'orbital-mark',slot:'e',name:'Orbital Mark',short:'MARK',text:'Tag a target or point with a dart; an orbital strike tracks it and lands 1 s later.',kind:'mark',fx:'orbital-mark',cooldown:13,cost:0,windup:.15,lockout:.35,range:120,damage:85,radius:3,impulse:1400,speed:150,gravity:6,shotRadius:.08,fuse:1,duration:.8}},
};
