// Ranged/magic Q+E ability contract (design §3.2; frozen after FOUNDATION): data and pure helpers for the server, prediction
// and HUD. Never imports the handler files (no import cycle).
import {WEAPONS,abilityUser,type Stats} from './content.js';
import {GUN_KITS} from './gun-kit.js';
import {MAGIC_KITS} from './magic-kit.js';
import {pongBotBallistics} from './pong.js';
import type {Player} from './simulation.js';
import {clamp,add,mul,v,type Input,type SpecId,type Vec} from './config.js';
export {abilityUser} from './content.js';
export type AbilitySlot='q'|'e';
export type AbilitySpec='pistol'|'rifle'|'machinegun'|'fire'|'water'|'lightning'|'darkness';
export type AbilityKind='ads'|'shot'|'throw'|'mark'|'strike'|'line'|'zone'|'blink'|'wave'|'orb';
export interface AbilityBot{min:number;max:number;aim:'ballistic'|'point'|'direct'|'away';lead?:number;desired?:number;priorityBelow?:number}
export interface AbilityDef{
 id:string;slot:AbilitySlot;name:string;short:string;text:string;kind:AbilityKind;fx:string;
 cooldown:number;cost:number;windup:number;lockout:number;range:number;
 damage?:number;radius?:number;duration?:number;fuse?:number;speed?:number;gravity?:number;shotRadius?:number;
 impulse?:number;speedFactor?:number;slowFor?:number;pierce?:number;burn?:number;force?:number;tick?:number;
 /** Metres flown before a shot may blast. */arm?:number;
 /** Extra fuse when the point is within 4 m of the caster. */closeFuse?:number;
 bot?:AbilityBot;
 /** Handler landed; unset keeps the legacy E. */live?:boolean;
}
export interface AdsProfile{fov:number;spread:number;settle:number;move:number;sens:number;overlay:'scope'|'sight';kick:number;
 /** Primary interval × while aiming. */interval?:number;
 /** Spread per consecutive aimed shot, capped at hip spread. */bloom?:number}
export interface AbilityKit{q:AbilityDef;e:AbilityDef;ads?:AdsProfile}
export type AbilityAim={yaw:number;pitch:number;camera:'first'|'third'};
/** Player.abilityCast; `cast` groups one cast's fields. */
export interface AbilityCastState{slot:AbilitySlot;id:string;at:number;aim:AbilityAim;point?:Vec;cast:string}
/** Projectile.ability (§3.7): `fuse` is absolute time, `travel` metres flown. */
export interface AbilityShotTag{id:string;slot:AbilitySlot;cast:string;fuse?:number;travel:number;data?:Record<string,unknown>}
/** `welcome` caps (§3.12); absent on old hosts. */
export interface HostCaps{abilities?:number;/** Hold-to-aim host (2026-10-04): aimed abilities cast on key release. */aimHold?:number;
 /** Movement host (2026-10-04): double-tap-W sprint and roll-cancel of melee attacks. */move?:number}
export const HOST_CAPS:HostCaps={abilities:1,aimHold:1,move:1};
/** Aimed (ground/point-targeted) abilities: a tap casts on release, holding past HOLD_AIM_SECONDS shows the aim preview and the
 *  cast fires on release at the aimed point (owner 2026-10-04: "remove that aim-circle ... show if we hold the button"). */
export const HOLD_AIM:ReadonlySet<string>=new Set(['frag-grenade','meteor-strike','geyser-wall','singularity']);
export const HOLD_AIM_SECONDS=.2;
/** Gates every client ability feature (Q bit, ADS, HUD, hints). */
export const abilityClient=(caps:HostCaps|null|undefined,spec:SpecId|undefined)=>!!caps?.abilities&&!!spec&&abilityUser(spec);
const KITS:Partial<Record<SpecId,AbilityKit>>={...GUN_KITS,...MAGIC_KITS};
export function kitFor(spec:SpecId):AbilityKit|undefined{return abilityUser(spec)?KITS[spec]:undefined;}
export function abilityDef(spec:SpecId,slot:AbilitySlot):AbilityDef|undefined{return kitFor(spec)?.[slot];}
/** Data only: a gun Q with an ADS profile, or a `live` def. */
export function abilityActive(spec:SpecId,slot:AbilitySlot):boolean{const k=kitFor(spec);return !!k&&(slot==='q'&&k.q.kind==='ads'?!!k.ads:!!k[slot].live);}
/** Kit defs by id (`ads` maps to the pistol row). */
export const ABILITY_DEFS:Record<string,AbilityDef>=Object.fromEntries(Object.values(KITS).flatMap(k=>[k!.e,k!.q]).reverse().map(d=>[d.id,d]));
/** Windups and lockouts only. */
export const castTime=(t:number,s:Stats)=>t/Math.min(1.5,Math.max(.15,s.attackRate));
export const abilityCooldown=(d:AbilityDef,s:Stats)=>d.cooldown*clamp(s.abilityCooldown,.45,3);
/** × resourceCost inside spendResource. */
export const abilityCost=(d:AbilityDef,s:Stats)=>d.cost*s.abilityCost;
export const abilityRadius=(d:AbilityDef,s:Stats)=>(d.radius||0)*clamp(s.abilityRadius,.5,1.5);
export const abilityScale=(s:Stats,manaHigh:boolean,echo=false)=>Math.min(2.25,clamp(Math.sqrt(s.damage),.5,1.5)*Math.min(2,s.abilityPower*(manaHigh?1+s.arcaneSurge:1)))*(echo?.5:1);
export const maxCharges=(s:Stats)=>Math.min(3,1+Math.max(0,Math.floor(s.abilityCharges)));
export const adsProfile=(spec:SpecId)=>kitFor(spec)?.ads;
/** Walk factor while Q is held (prediction mirrors it). */
export function adsMove(spec:SpecId,input:Pick<Input,'ability'>):number{const a=adsProfile(spec);return a&&input.ability===true?a.move:1;}
export function adsSettle(p:{adsSince?:number},spec:SpecId,time:number):number{const a=adsProfile(spec);if(!a||p.adsSince===undefined)return 0;return a.settle>0?clamp((time-p.adsSince)/a.settle,0,1):1;}
/** Hip spread lerps to hip×profile.spread over the settle; bloom is capped at hip. */
export function adsSpread(p:{adsSince?:number;adsBloom?:{n:number;at:number}},spec:SpecId,time:number,hip:number):number{
 const a=adsProfile(spec);if(!a||p.adsSince===undefined)return hip;const t=adsSettle(p,spec,time);let s=hip+(hip*a.spread-hip)*t;
 if(a.bloom&&p.adsBloom&&time-p.adsBloom.at<=.25)s=Math.min(hip,s+a.bloom*p.adsBloom.n);return s;
}
export function shotBallistics(spec:SpecId,stats:Stats,owner?:Player):{speed:number;gravity:number}{const w=WEAPONS[spec];return stats.pong?pongBotBallistics(w,stats,owner):{speed:w.speed*stats.projectileSpeed,gravity:stats.boomerang?0:w.gravity};}
/** Crosshair point (server and client preview); `ground` projects an air point down ≤ 40 m; clamped to the arena. */
export function groundAimPoint(start:Vec,dir:Vec,range:number,ground:boolean,ray:(o:Vec,d:Vec,max:number)=>number|null):{point:Vec;clamped:boolean}{
 const hit=ray(start,dir,range);let point=add(start,mul(dir,hit??range)),clamped=hit===null;
 if(ground&&!(hit!==null&&ray(add(point,v(0,.05,0)),v(0,-1,0),.1)!==null)){const from=hit===null?point:add(point,mul(dir,-.05)),down=ray(from,v(0,-1,0),40);if(down!==null)point=v(from.x,from.y-down,from.z);}
 const x=clamp(point.x,-31,31),z=clamp(point.z,-27,27);if(x!==point.x||z!==point.z)clamped=true;return {point:v(x,point.y,z),clamped};
}
/** `range` counts from the pilot, the ray starts at the camera. */
export function abilityAimPoint(camera:Vec,pos:Vec,dir:Vec,range:number,ground:boolean,ray:(o:Vec,d:Vec,max:number)=>number|null){return groundAimPoint(camera,dir,range+Math.hypot(camera.x-pos.x,camera.y-pos.y,camera.z-pos.z),ground,ray);}
/** Bot aim lead per difficulty (abilityUser bots; skipped while 0). */
export const BOT_LEAD=[0,0,0];
/** §13.1 fx tag → ability id; marks ability-owned fields. */
export const FX_ABILITY:Record<string,string>={frag:'frag-grenade',rocket:'rocket','orbital-mark':'orbital-mark','flame-lance':'flame-lance','meteor-strike':'meteor-strike','tidal-wave':'tidal-wave','geyser-wall':'geyser-wall','blink-strike':'blink-strike','ball-lightning':'ball-lightning',shackles:'shadow-shackles',singularity:'singularity'};
export const ABILITY_FX=new Set<string>(Object.keys(FX_ABILITY));
