// PvE add AI pieces (design §3.4–3.5, stream S1): the add record, attack profiles from kind and limb state, target
// scoring, separation and possession keep-away. Deterministic; the horde drives these once per tick in Map order.
import type {Vec} from '../config.js';
import type {Minion} from '../chaos.js';
import {PVE_TIMING,type AddAttack,type AddKind,type HordeModifiers,type LimbName} from './contract.js';
import {GIB,HORDE,PVE_SIREN,type AddKindStats} from './balance.js';
import {limbState} from './gibs.js';
import type {DotState} from '../elements.js';

/** Internal phase: pending (waiting for an entry), entering (no body), active (body, AI), vent (diving / hidden). */
export type AddPhase='pending'|'entering'|'active'|'vent';
export interface PveAdd{
 n:number;id:string;kind:AddKind;stats:AddKindStats;mods:HordeModifiers;phase:AddPhase;
 hp:number;maxHp:number;parts:Partial<Record<LimbName,number>>;lost:number;crawl:boolean;limp:boolean;headless:boolean;
 attack:AddAttack;side:1|-1;attackAt:number;strikeAt:number;struck:boolean;recoverUntil:number;staggerUntil:number;
 atkDamage:number;atkReach:number;atkCone:number;
 target?:string;thinkAt:number;ext:{x:number;z:number};
 entry:number;requestEntry?:number;spawnAt:number;walkAt:number;walkFor:number;activeAt:number;
 diveAt:number;ventUntil:number;ventCooldown:number;travel:boolean;
 hitAt:number;hitYaw:number;dealt:Map<string,number>;lastHitBy?:string;lastDamageAt:number;
 possessed:boolean;possessedAt:number;
 /** Body-less position (pending, entering, vent) and the facing yaw. */ pos:Vec;yaw:number;
 minion?:Minion;
 progressAt:number;progressDist:number;nudgeUntil:number;
 fieldSum:number;fieldAt:number;fieldBy?:string;
 /** Weapon statuses (2026-10-04): burn/poison DoTs per source (elements.ts rules) and a movement slow. */
 burns?:Map<string,DotState>;poisons?:Map<string,DotState>;slowUntil?:number;slowFactor?:number;
 /** Direct hits waiting for an event token (coalesced instead of dropped). */
 pend?:Map<string,{amount:number;point:Vec;at:number;headshot?:boolean}>;
}
export interface AttackProfile{attack:AddAttack;damage:number;reach:number;start:number;cone:number;hit:number;lunge:number;cycle:number;impulse:number}
/** §3.5 table plus the limb rules: one arm ×0.65, armless bite (lunge ×0.35, 1.2 m), crawling ×0.6 low swipe (1.3 m). */
export function attackProfile(a:Pick<PveAdd,'stats'|'lost'|'crawl'|'mods'>):AttackProfile{
 const st=a.stats,ls=limbState(a.lost),rate=Math.max(.05,a.mods.attackRate),cycle=st.cycle/rate,impulse=st.impulse;
 if(a.crawl)return {attack:'swipe',damage:st.damage*GIB.crawlDamage*a.mods.damage,reach:GIB.crawlReach,start:GIB.crawlReach-.3,cone:.35,hit:PVE_TIMING.swipeHit,lunge:.15,cycle,impulse:impulse*.5};
 if(ls.armless)return {attack:'lunge',damage:st.damage*GIB.biteDamage*a.mods.damage,reach:GIB.biteReach,start:GIB.biteReach-.25,cone:.5,hit:PVE_TIMING.lungeHit,lunge:.45,cycle,impulse:impulse*.5};
 const hit=st.attack==='smash'?PVE_TIMING.smashHit:st.attack==='lunge'?PVE_TIMING.lungeHit:PVE_TIMING.swipeHit;
 return {attack:st.attack,damage:st.damage*(ls.oneArm?GIB.oneArmDamage:1)*a.mods.damage,reach:st.reach,start:st.startRange,cone:st.attack==='lunge'?.5:.35,hit,lunge:st.lunge,cycle,impulse};
}
/** Swiping side: alternate, but never with a lost arm (side 1 = right arm, -1 = left arm; contract flag bit 16). */
export function nextSide(previous:1|-1,lost:number):1|-1{
 const ls=limbState(lost);let side:1|-1=previous===1?-1:1;
 if(side===1&&ls.rArm)side=-1;else if(side===-1&&ls.lArm)side=1;return side;
}
/** Movement speed (m/s) before modifiers: run within frenzy range or below 50% HP; crawl ×0.35, limp ×0.55. */
export function addSpeed(a:Pick<PveAdd,'stats'|'hp'|'maxHp'|'crawl'|'limp'|'mods'>,targetDistance:number){
 const run=targetDistance<=HORDE.frenzyRange||a.hp<a.maxHp*.5;
 return (run?a.stats.run:a.stats.walk)*a.mods.speed*(a.crawl?GIB.crawlSpeed:a.limp?GIB.limpSpeed:1);
}
/** §3.4 score (lower wins). */
export function targetScore(navDistance:number,chasers:number,taunted:boolean,current:boolean){
 return navDistance+3*Math.max(0,chasers-6)-(taunted?6:0)+(current?-2:0);
}
/** Uniform spatial hash (1.5 m buckets) used for separation. */
export class SeparationHash{
 private buckets=new Map<number,PveAdd[]>();
 private key=(x:number,z:number)=>Math.floor(x/1.5)*4096+Math.floor(z/1.5);
 clear(){this.buckets.clear();}
 add(a:PveAdd,p:Vec){const k=this.key(p.x,p.z);let b=this.buckets.get(k);if(!b){b=[];this.buckets.set(k,b);}b.push(a);}
 /** Separation velocity: neighbours closer than HORDE.separation push by (sep−d)·separationPush; at most 6 neighbours. */
 push(a:PveAdd,p:Vec,posOf:(b:PveAdd)=>Vec):{x:number;z:number}{
  let sx=0,sz=0,count=0;const hx=Math.floor(p.x/1.5),hz=Math.floor(p.z/1.5);
  for(let ox=-1;ox<=1;ox++)for(let oz=-1;oz<=1;oz++){const list=this.buckets.get((hx+ox)*4096+hz+oz);if(!list)continue;
   for(const b of list){if(b===a||count>=6)continue;const q=posOf(b),ex=p.x-q.x,ez=p.z-q.z,e=Math.hypot(ex,ez);
    if(e<HORDE.separation){count++;if(e<1e-4){const s=a.n<b.n?1:-1;sx+=s*HORDE.separation*HORDE.separationPush;continue;}
     sx+=ex/e*(HORDE.separation-e)*HORDE.separationPush;sz+=ez/e*(HORDE.separation-e)*HORDE.separationPush;}}}
  return {x:sx,z:sz};
 }
}
/** Possession keep-away mode at age (seconds since possession) and distance to the nearest human (§3.4). */
export function possessedMode(age:number,distance:number):'chase'|'stare'|'away'|'toward'|'hold'{
 const p=PVE_SIREN.possession;
 if(age>=p.forceRevealAfter)return 'chase';
 if(age%p.stareEvery>=p.stareEvery-p.stareFor)return 'stare';
 if(distance<p.keepAway[0])return 'away';
 if(distance>p.keepAway[1])return 'toward';
 return 'hold';
}
