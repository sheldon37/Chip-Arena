// Wave stages before the boss (design §2.4, §10.3; stream S2). Pure and rate-independent: every timer is an absolute sim time
// compared with >=, so the same inputs give the same transition times at any dt (within one step). The Director feeds it the
// sim time, the adds left in the current wave and the time every human resolved the start pick; it returns stage events.
import type {AddKind,HordeFlow,HordeModifiers} from './contract.js';
import {HORDE,MIX_BOSS,PVE,PVE_SIREN,WAVES,partyFactor} from './balance.js';

export type WaveStage='opening'|'wave'|'intermission'|'done';
export type WaveEvent=
 |{kind:'wave-start';wave:number;at:number}
 |{kind:'wave-clear';wave:number;at:number;forced:boolean}
 |{kind:'boss';at:number};
export interface WaveTick{
 /** Current sim time. */ time:number;
 /** Adds of the running wave not yet killed (queued, unreleased budget and alive). Ignored outside waves. */ remaining:number;
 /** The time every human resolved the start pick (opening early end), or undefined while someone has not. */ resolvedAt?:number;
}

/** The horde cap for the room (relay rooms keep fewer adds alive). */
export const hordeCap=(relay:boolean)=>relay?HORDE.maxAliveRelay:HORDE.maxAlive;
/** §10.3 wave budget: round(count·partyFactor(h)·mods.count), at least 1. */
export const waveBudget=(w:number,h:number,mods:Pick<HordeModifiers,'count'>={count:1})=>{
 const spec=WAVES[Math.max(0,Math.min(WAVES.length-1,w-1))];return Math.max(1,Math.round(spec.count*partyFactor(h)*mods.count));
};
/** §10.3 wave flow for wave w (1-based) and party size h. */
export function waveFlow(w:number,h:number,mods:Pick<HordeModifiers,'count'>={count:1},relay=false):HordeFlow{
 const spec=WAVES[Math.max(0,Math.min(WAVES.length-1,w-1))],m=Math.max(1,h)-1;
 return {rate:spec.rate*(1+.35*m),maxAlive:Math.min(hordeCap(relay),spec.maxAlive+5*m),mix:{...spec.mix},budget:waveBudget(w,h,mods)};
}
/** §6.4 boss-stage trickle: rate .6+.4(h−1) and alive 6+4(h−1), ×1.8 and ×1.6 while the Siren possesses an add. */
export function bossFlow(phase:1|2|3|4,h:number,possessing:boolean,relay=false):HordeFlow{
 const t=PVE_SIREN.trickle,m=Math.max(1,h)-1,kr=possessing?PVE_SIREN.possession.rate:1,ka=possessing?PVE_SIREN.possession.alive:1;
 return {rate:(t.rate+t.ratePerMember*m)*kr,maxAlive:Math.min(hordeCap(relay),Math.round((t.alive+t.alivePerMember*m)*ka)),
  mix:{...MIX_BOSS[phase]} as Partial<Record<AddKind,number>>,budget:-1};
}

/** Opening, waves and intermissions up to the boss. */
export class WaveSchedule{
 stage:WaveStage='opening';stageAt=0;stageUntil?:number;wave=0;
 /** Start of the current ≤waveClearRemainder streak in a wave. */ lowSince?:number;
 constructor(readonly waves:number=PVE.wavesBeforeBoss,readonly skipWaves=false){}
 /** Fight start: the opening stage begins. */
 start(time:number){this.stage='opening';this.stageAt=time;this.stageUntil=time+PVE.openingSeconds;this.wave=0;this.lowSince=undefined;}
 /** When the opening ends given the early-end time (3 s after every human resolved the start pick, at least 4 s in total). */
 openingEnd(resolvedAt?:number){
  const full=this.stageAt+PVE.openingSeconds;if(resolvedAt===undefined)return full;
  return Math.min(full,Math.max(this.stageAt+PVE.openingMinSeconds,resolvedAt+PVE.openingEarlySeconds));
 }
 /** Advances the schedule; returns the stage events of this tick (at most one transition per call). */
 step(t:WaveTick):WaveEvent[]{
  const out:WaveEvent[]=[];
  if(this.stage==='opening'){
   this.stageUntil=this.openingEnd(t.resolvedAt);
   if(t.time>=this.stageUntil)out.push(this.skipWaves||this.waves<=0?this.finish(t.time):this.startWave(1,t.time));
  }else if(this.stage==='wave'){
   if(t.remaining<=PVE.waveClearRemainder){this.lowSince??=t.time;}else this.lowSince=undefined;
   const quiet=this.lowSince!==undefined&&t.time>=this.lowSince+PVE.waveClearHold,forced=t.time>=this.stageAt+PVE.waveMaxSeconds;
   if(quiet||forced)out.push(this.clear(t.time,!quiet));
  }else if(this.stage==='intermission'&&this.stageUntil!==undefined&&t.time>=this.stageUntil){
   out.push(this.wave>=this.waves?this.finish(t.time):this.startWave(this.wave+1,t.time));
  }
  return out;
 }
 /** Ends the running wave now (wave clear) or, outside a wave, moves to the next stage (debug skip-wave). */
 skip(time:number):WaveEvent|undefined{
  if(this.stage==='opening')return this.skipWaves?this.finish(time):this.startWave(1,time);
  if(this.stage==='wave')return this.clear(time,true);
  if(this.stage==='intermission')return this.wave>=this.waves?this.finish(time):this.startWave(this.wave+1,time);
  return undefined;
 }
 /** Leaves the wave stages for the boss (debug skip-to-boss). */
 finish(time:number):WaveEvent{this.stage='done';this.stageAt=time;this.stageUntil=undefined;this.lowSince=undefined;return {kind:'boss',at:time};}
 private startWave(w:number,time:number):WaveEvent{this.stage='wave';this.wave=w;this.stageAt=time;this.stageUntil=undefined;this.lowSince=undefined;return {kind:'wave-start',wave:w,at:time};}
 private clear(time:number,forced:boolean):WaveEvent{
  const w=this.wave;this.stage='intermission';this.stageAt=time;this.lowSince=undefined;
  this.stageUntil=time+(w>=this.waves?PVE.lastIntermissionSeconds:PVE.intermissionSeconds);
  return {kind:'wave-clear',wave:w,at:time,forced};
 }
}
