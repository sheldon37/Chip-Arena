// Ranged/magic Q+E ability contract (design §3.2, FOUNDATION; frozen after F, INTEGRATION only).
// Data and pure helpers shared by the server (AbilitySystem), prediction and the client HUD. This module never imports the
// handler files, so the client stays light and there is no import cycle.
import {WEAPONS,abilityUser,type Stats} from './content.js';
import {GUN_KITS} from './gun-kit.js';
import {MAGIC_KITS} from './magic-kit.js';
import {pongBotBallistics} from './pong.js';
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
 /** Metres of travel before a shot may blast (rocket 3). */arm?:number;
 /** Extra fuse when the cast point is within 4 m of the caster (ground-targeted E: .3). */closeFuse?:number;
 bot?:AbilityBot;
 /** Set by G/M in the kit row when the handler lands; unset means the legacy E stays in charge. */live?:boolean;
}
export interface AdsProfile{fov:number;spread:number;settle:number;move:number;sens:number;overlay:'scope'|'sight';kick:number;
 /** Primary interval × while aiming (pistol 1.12). */interval?:number;
 /** Spread added per consecutive aimed shot, capped at the hip spread (MG .002). */bloom?:number}
export interface AbilityKit{q:AbilityDef;e:AbilityDef;ads?:AdsProfile}
export type AbilityAim={yaw:number;pitch:number;camera:'first'|'third'};
/** Player.abilityCast: a pending windup (released at `at`). `cast` groups the fields of one cast (ability-field pool). */
export interface AbilityCastState{slot:AbilitySlot;id:string;at:number;aim:AbilityAim;point?:Vec;cast:string}
/** Projectile.ability: an ability shot (§3.7). `fuse` is an absolute time; `travel` the metres flown so far. */
export interface AbilityShotTag{id:string;slot:AbilitySlot;cast:string;fuse?:number;travel:number;data?:Record<string,unknown>}
/** Host capabilities advertised in `welcome` (§3.12). Absent on old hosts. */
export interface HostCaps{abilities?:number}
export const HOST_CAPS:HostCaps={abilities:1};
/** Every client-side ability feature (Q bit, ADS prediction/camera, ability HUD and hints) requires this. */
export const abilityClient=(caps:HostCaps|null|undefined,spec:SpecId|undefined)=>!!caps?.abilities&&!!spec&&abilityUser(spec);
const KITS:Partial<Record<SpecId,AbilityKit>>={...GUN_KITS,...MAGIC_KITS};
export function kitFor(spec:SpecId):AbilityKit|undefined{return abilityUser(spec)?KITS[spec]:undefined;}
export function abilityDef(spec:SpecId,slot:AbilitySlot):AbilityDef|undefined{return kitFor(spec)?.[slot];}
/** Data only: a gun Q with an ADS profile, or a `live` def (an F test asserts live defs and handlers match). */
export function abilityActive(spec:SpecId,slot:AbilitySlot):boolean{const k=kitFor(spec);return !!k&&(slot==='q'&&k.q.kind==='ads'?!!k.ads:!!k[slot].live);}
/** Every kit def by ability id (the shared `ads` id maps to the pistol row). */
export const ABILITY_DEFS:Record<string,AbilityDef>=Object.fromEntries(Object.values(KITS).flatMap(k=>[k!.e,k!.q]).reverse().map(d=>[d.id,d]));
/** Windups and lockouts only; fuses, telegraphs, durations and projectile lives never scale. */
export const castTime=(t:number,s:Stats)=>t/Math.min(1.5,Math.max(.15,s.attackRate));
export const abilityCooldown=(d:AbilityDef,s:Stats)=>d.cooldown*clamp(s.abilityCooldown,.45,3);
/** × resourceCost inside spendResource. */
export const abilityCost=(d:AbilityDef,s:Stats)=>d.cost*s.abilityCost;
export const abilityRadius=(d:AbilityDef,s:Stats)=>(d.radius||0)*clamp(s.abilityRadius,.5,1.5);
export const abilityScale=(s:Stats,manaHigh:boolean,echo=false)=>Math.min(2.25,clamp(Math.sqrt(s.damage),.5,1.5)*Math.min(2,s.abilityPower*(manaHigh?1+s.arcaneSurge:1)))*(echo?.5:1);
/** E charges: 1 + abilityCharges, at most 3. */
export const maxCharges=(s:Stats)=>Math.min(3,1+Math.max(0,Math.floor(s.abilityCharges)));
export const adsProfile=(spec:SpecId)=>kitFor(spec)?.ads;
/** Pure walk factor of the input: the profile's move while Q is held, else 1 (mirrored by prediction). */
export function adsMove(spec:SpecId,input:Pick<Input,'ability'>):number{const a=adsProfile(spec);return a&&input.ability===true?a.move:1;}
/** 0..1 settle progress since ADS started; 0 when not aiming. */
export function adsSettle(p:{adsSince?:number},spec:SpecId,time:number):number{const a=adsProfile(spec);if(!a||p.adsSince===undefined)return 0;return a.settle>0?clamp((time-p.adsSince)/a.settle,0,1):1;}
/** Primary spread: hip until aiming, then lerps to hip×profile.spread over the settle, plus MG bloom capped at the hip spread. */
export function adsSpread(p:{adsSince?:number;adsBloom?:{n:number;at:number}},spec:SpecId,time:number,hip:number):number{
 const a=adsProfile(spec);if(!a||p.adsSince===undefined)return hip;const t=adsSettle(p,spec,time);let s=hip+(hip*a.spread-hip)*t;
 if(a.bloom&&p.adsBloom&&time-p.adsBloom.at<=.25)s=Math.min(hip,s+a.bloom*p.adsBloom.n);return s;
}
/** Primary shot speed and gravity (bots aim with it). ADS never changes ballistics. */
export function shotBallistics(spec:SpecId,stats:Stats):{speed:number;gravity:number}{const w=WEAPONS[spec];return stats.pong?pongBotBallistics(w,stats):{speed:w.speed*stats.projectileSpeed,gravity:stats.boomerang?0:w.gravity};}
/** Crosshair point for point/ground abilities, shared by the server `aimPoint` and the client preview. `ray` returns the hit
 *  distance or null. With `ground`, a point in the air is projected down (≤ 40 m). Clamped to the arena (|x|≤31, |z|≤27). */
export function groundAimPoint(start:Vec,dir:Vec,range:number,ground:boolean,ray:(o:Vec,d:Vec,max:number)=>number|null):{point:Vec;clamped:boolean}{
 const hit=ray(start,dir,range);let point=add(start,mul(dir,hit??range)),clamped=hit===null;
 if(ground){const from=add(point,mul(dir,-.05)),down=ray(from,v(0,-1,0),40);if(down!==null)point=v(from.x,from.y-down,from.z);}
 const x=clamp(point.x,-31,31),z=clamp(point.z,-27,27);if(x!==point.x||z!==point.z)clamped=true;return {point:v(x,point.y,z),clamped};
}
/** The server `aimPoint` and the client ground preview: crosshair ray from the view camera; `range` counts from the pilot. */
export function abilityAimPoint(camera:Vec,pos:Vec,dir:Vec,range:number,ground:boolean,ray:(o:Vec,d:Vec,max:number)=>number|null){return groundAimPoint(camera,dir,range+Math.hypot(camera.x-pos.x,camera.y-pos.y,camera.z-pos.z),ground,ray);}
/** Bot aim lead per difficulty (I tunes); applied only to abilityUser bots and skipped entirely while 0. */
export const BOT_LEAD=[0,0,0];
/** §13.1 fx tags: identify ability-owned fields (their own pool) and pick the client blast style. */
export const FX_ABILITY:Record<string,string>={frag:'frag-grenade',rocket:'rocket','orbital-mark':'orbital-mark','flame-lance':'flame-lance','meteor-strike':'meteor-strike','tidal-wave':'tidal-wave','geyser-wall':'geyser-wall','blink-strike':'blink-strike','ball-lightning':'ball-lightning',shackles:'shadow-shackles',singularity:'singularity'};
export const ABILITY_FX=new Set<string>(Object.keys(FX_ABILITY));
/** Reserved contract ids (§17): school-chip mechanics and the Pong rally event. */
export const SCHOOL_MECHANICS=['cinder-field','undertow','storm-recall','abyssal-core'] as const;
export const PONG_RALLY={type:'ricochet',message:'rally'} as const;
