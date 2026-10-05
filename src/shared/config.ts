export const CONFIG = {
  maxPlayers: 8, simulationHz: 60, snapshotHz: 20, wins: 5, roundsPerSet: 2, countdown: 3, resultSeconds: 5,
  selectionSeconds: 18, disconnectSeconds: 12, maxRoundSeconds: 100,
  speed: 6.8, acceleration: 35, jumpSpeed: 8.5, gravity: -23, dashSpeed: 15,
  dashDuration: 0.18, dashCooldown: 2.8, externalDrag: 3.1,
  maxExternalSpeed: 70,
  playerMass: 65, playerRadius: .42, playerHalfHeight: .43,
  minimumImpactSpeed: 3.6, safeEnergy: 45, damageScale: .035,
  maximumDamagePerImpact: 65, impactPairCooldown: .4,
  fragmentWindow: .35, fragmentDamageCap: 75, attributionSeconds: 7,
  manaRegen: 12, staminaRegen: 24, staminaRegenDelay: .45,
  shieldDrain: 33, shieldImpactCost: .9,
  blockDuration: .30, blockCooldown: 4, blockMaximumDuration: .65, blockMinimumCooldown: 1.5,
  blockCooldownChip: .85, blockWindowChip: .08, blockReflectDamageChip: 1.15, blockReflectSpeedChip: 1.10,
  blockPulseRadius: 3, blockPulseDamageChip: 6, blockPulseImpulseChip: 360,
  maxReflections: 3, maxReflectedDamage: 100, maxReflectedSpeed: 120,
  volleyDamage: .75, volleyFanDegrees: 7,
  followupDamage: .45, followupCost: 2, followupGap: .08,
  projectileLifetime: 3, projectileMaxTravel: 160, splitLifetime: 1.2, splitMaxTravel: 18,
  homingRange: 16, homingConeDegrees: 50,
  maxBodies: 360, maxProjectiles: 100, maxEvents: 80,
  maxInputRate: 100, staleInputSeconds: .3, maxRooms: 24,
} as const;
export const MATERIALS = {
  wood: {color:0xc78342, mass:8, durability:34, friction:.7},
  stone: {color:0x8a929e, mass:26, durability:85, friction:.85},
  metal: {color:0x426b79, mass:44, durability:150, friction:.55},
  foundation: {color:0x314651, mass:0, durability:Infinity, friction:.8},
};
export type Material = keyof typeof MATERIALS;
/** DEV is a selection category; weapons retain their combat/resource class. */
export type ClassId='ranged'|'magic'|'melee'|'dev';
export type SpecId='siren'|'emperor'|'ansatsuken'|'leet'|'pistol'|'rifle'|'machinegun'|'fire'|'water'|'lightning'|'darkness'|'sword'|'daggers'|'shield'|'greatsword';
export type Vec = {x:number;y:number;z:number};
export const v=(x=0,y=0,z=0):Vec=>({x,y,z});
export const add=(a:Vec,b:Vec):Vec=>v(a.x+b.x,a.y+b.y,a.z+b.z);
export const sub=(a:Vec,b:Vec):Vec=>v(a.x-b.x,a.y-b.y,a.z-b.z);
export const mul=(a:Vec,b:number):Vec=>v(a.x*b,a.y*b,a.z*b);
export const dot=(a:Vec,b:Vec)=>a.x*b.x+a.y*b.y+a.z*b.z;
export const len=(a:Vec)=>Math.hypot(a.x,a.y,a.z);
export const norm=(a:Vec)=>mul(a,1/(len(a)||1));
export const cross=(a:Vec,b:Vec)=>v(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
export const clamp=(n:number,a:number,b:number)=>Math.max(a,Math.min(b,n));
export const forward=(yaw:number,pitch=0):Vec=>v(Math.sin(yaw)*Math.cos(pitch),Math.sin(pitch),Math.cos(yaw)*Math.cos(pitch));
export interface Input {/** Q held (ADS or spell); sent only to capable hosts. */ability?:boolean;/** Held sprint (double-tap W, kept while W is held); sent only to sprint-capable hosts. */sprint?:boolean;/** Sent with a roll press by clients that roll-cancel (2026-10-04): older clients never send it, so the host keeps their swings. */rollCancel?:boolean;interact?:boolean;sirenToggle?:boolean;sirenAbility?:1|2|3|4;electric?:boolean;hadoken?:boolean;uppercut?:boolean;camera?:'first'|'third';seq:number; x:number;z:number;yaw:number;pitch:number;attack:boolean;block?:boolean;secondary:boolean;jump:boolean;dash:boolean;reload:boolean;}
export const idleInput=():Input=>({seq:0,x:0,z:0,yaw:0,pitch:0,attack:false,ability:false,electric:false,hadoken:false,uppercut:false,block:false,secondary:false,jump:false,dash:false,reload:false});
export type Difficulty='dummy'|'easy'|'normal'|'hard';
