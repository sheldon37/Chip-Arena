// The PvE director (design §2.4, §6; stream S2): opening, waves and intermissions (waves.ts), held cinematics, the Siren
// boss (boss.ts) with its fixed chip track, phase floors, possession, grow, no-cooldown, victory and defeat.
// Deterministic: absolute sim times only, no clocks or Math.random. Keep the class name and constructor: the facade builds
// `new Director(host)`.
import type {Input} from '../config.js';
import type {Player} from '../simulation.js';
import {ADD_PREFIX,PVE_EVENT,type AddDeath,type DirectorApi,type HordeModifiers,type PveBossView,type PveCinematic,type PveHost,
 type PveOutcome,type PveStage} from './contract.js';
import {ECONOMY,PVE,PVE_SIREN,bossStageScale,combineMods,waveScale} from './balance.js';
import {SIREN_INTRO} from '../siren-contract.js';
import {BOSSES} from './bosses.js';
import {PVE_LAYOUT} from './layout.js';
import {WaveSchedule,bossFlow,waveBudget,waveFlow,type WaveEvent} from './waves.js';
import {bossInput,grow,play,revealAt,spawnBoss} from './boss.js';

type Chip=1|2|3|4;
type Phase=1|2|3|4;
const REVEAL_SECONDS=PVE.reveal.smile+PVE.reveal.card+PVE.reveal.settle;
/** damage() clamps to hp−floor; floating point can leave hp a hair above the floor (docs/PVE.md). */
const FLOOR_EPSILON=1e-6;
const ORIGIN={x:0,y:0,z:0};

export class Director implements DirectorApi{
 private schedule=new WaveSchedule(PVE.wavesBeforeBoss,false);
 private stage:PveStage='opening';private stageAt=0;private stageUntil?:number;
 private wave=0;private waveTotal=0;private waveCarry=0;private waveKilled=0;
 private resolvedAt?:number;
 private cinematic?:PveCinematic;private hold=false;
 private track:{revealed:0|1|2|3|4;at:number}={revealed:0,at:0};
 private bossId?:string;private maxHp=0;private phase:Phase=1;private floor=0;private shown=false;
 private hiddenBoss=false;private possessed?:number;private possessedAt?:number;private foundAt?:number;private possessionAt=0;
 private outcome?:PveOutcome;private endedAt?:number;private pendingOutcome?:PveOutcome;
 private started=false;
 /** Set when the boss could not spawn (the run then ends as a defeat with this result). */
 spawnError?:string;
 constructor(readonly host:PveHost){}

 // ---- lifecycle ----
 onFightStart(){
  const t=this.host.sim.time;this.reset();this.started=true;
  this.schedule=new WaveSchedule(PVE.wavesBeforeBoss,this.host.options.skipWaves===true);this.schedule.start(t);
  this.stage='opening';this.stageAt=t;this.stageUntil=this.schedule.stageUntil;
 }
 reset(){
  this.schedule=new WaveSchedule(PVE.wavesBeforeBoss,false);this.stage='opening';this.stageAt=0;this.stageUntil=undefined;
  this.wave=0;this.waveTotal=0;this.waveCarry=0;this.waveKilled=0;this.resolvedAt=undefined;this.cinematic=undefined;this.hold=false;
  this.track={revealed:0,at:0};this.bossId=undefined;this.maxHp=0;this.phase=1;this.floor=0;this.shown=false;
  this.hiddenBoss=false;this.possessed=undefined;this.possessedAt=undefined;this.foundAt=undefined;this.possessionAt=0;
  this.outcome=undefined;this.endedAt=undefined;this.pendingOutcome=undefined;this.spawnError=undefined;this.started=false;
 }

 // ---- per tick ----
 /** After physics (facade stepAfterPhysics, last): stages, floors, possession and defeat. */
 step(_dt:number){
  const s=this.host.sim;if(!this.started||this.outcome||this.hold||s.phase!=='fight')return;
  if(this.stage==='opening'||this.stage==='wave'||this.stage==='intermission')this.stepWaves();
  else if(this.stage==='boss')this.stepBoss();
  if(!this.hold&&!this.outcome&&this.defeated())this.startOutro('defeat');
 }
 /** Hold tick (D21): the boss's ability-ready times move with the clock, then the cinematic timeline advances. */
 stepHold(dt:number){
  const s=this.host.sim,c=this.cinematic;if(!c)return;
  const ready=this.boss()?.siren?.ready;if(ready)for(const id of Object.keys(ready) as (keyof typeof ready)[])ready[id]=(ready[id]??0)+dt;
  if(c.kind==='reveal'&&c.chip&&this.track.revealed<c.chip&&s.time>=(c.smileUntil??c.at))this.track={revealed:c.chip,at:s.time};
  if(s.time>=c.until)this.endCinematic(c);
 }
 holding(){return this.hold;}

 private stepWaves(){
  const s=this.host.sim;
  if(this.stage==='opening'&&this.resolvedAt===undefined&&this.allResolved())this.resolvedAt=s.time;
  for(const e of this.schedule.step({time:s.time,remaining:this.remaining(),resolvedAt:this.resolvedAt}))this.onWaveEvent(e);
  if(this.stage==='opening')this.stageUntil=this.schedule.stageUntil;
 }
 private onWaveEvent(e:WaveEvent){
  const h=this.host;
  if(e.kind==='wave-start'){
   this.wave=e.wave;this.stage='wave';this.stageAt=e.at;this.stageUntil=undefined;
   const mods=this.modifiers();this.waveTotal=waveBudget(e.wave,h.partySize(),mods);this.waveKilled=0;this.waveCarry=h.horde.alive()+h.horde.queued();
   h.horde.setFlow(waveFlow(e.wave,h.partySize(),mods,h.options.relay===true));
   h.emit(PVE_EVENT.wave,ORIGIN,{amount:e.wave,message:'start'});
  }else if(e.kind==='wave-clear'){
   this.stage='intermission';this.stageAt=e.at;this.stageUntil=this.schedule.stageUntil;
   h.horde.setFlow(null);
   h.economy.grantAll(ECONOMY.waveClearBase+ECONOMY.waveClearPerWave*e.wave,'wave-clear');
   if(PVE.respawnOnWaveClear)h.economy.respawnAll('wave');
   h.emit(PVE_EVENT.wave,ORIGIN,{amount:e.wave,message:'clear'});
  }else{
   h.horde.setFlow(null);this.stageUntil=undefined;this.startReveal(1);
  }
 }
 private stepBoss(){
  const boss=this.boss();if(!boss)return;
  if(this.phase===2&&this.hiddenBoss){
   if(this.possessed===undefined){
    const n=this.host.horde.possessed();if(n!==undefined)this.markPossessed(n);
    // Safeguard: no add could ever be possessed (no horde): the Siren shows itself where it vanished.
    else if(this.host.sim.time>=this.possessionAt+PVE_SIREN.possession.forceRevealAfter)
     this.found({n:-1,kind:'shambler',pos:{...boss.body.translation()},killer:null,assists:[],headless:false,possessed:true,cause:'damage'});
   }
   return;
  }
  if((this.phase===1||this.phase===3)&&boss.hp>0&&boss.hp<=this.floor+FLOOR_EPSILON)this.startReveal((this.phase+1) as Chip);
 }

 // ---- holds and cinematics ----
 private beginHold(){
  if(!this.hold){this.hold=true;this.host.horde.setFlow(null);this.host.notifyHold(true);}
  this.clearBossEffects();
 }
 private endHold(){this.hold=false;this.cinematic=undefined;this.host.notifyHold(false);this.applyFlow();}
 /** clearBossEffects: releases the boss's grabs and drops its hazards and projectiles (keeps p.siren). */
 private clearBossEffects(){const b=this.boss();if(b)this.host.sim.siren.clearEffects(b);}
 private startReveal(chip:Chip){
  const at=this.host.sim.time;
  this.cinematic={kind:'reveal',chip,at,smileUntil:at+PVE.reveal.smile,until:at+REVEAL_SECONDS};
  this.beginHold();
  this.host.emit(PVE_EVENT.phase,this.boss()?.body.translation()??ORIGIN,{amount:chip,message:BOSSES[this.host.options.boss].track[chip-1]?.id});
 }
 private startOutro(outcome:PveOutcome){
  const at=this.host.sim.time;this.pendingOutcome=outcome;
  this.cinematic={kind:'outro',outcome,at,until:at+(outcome==='victory'?PVE.outroVictorySeconds:PVE.outroDefeatSeconds)};
  this.beginHold();
 }
 private endCinematic(c:PveCinematic){
  const h=this.host,s=h.sim;
  if(c.kind==='outro'){this.finish(c.outcome??'defeat');return;}
  if(c.kind==='reveal'&&c.chip===1){
   const out=spawnBoss(h);
   if(!out.boss){this.spawnError=out.error;this.finish('defeat');s.result=out.error;return;}
   const boss=out.boss;this.bossId=boss.id;this.maxHp=boss.stats.hp;this.phase=1;this.floor=PVE_SIREN.thresholds[0]*this.maxHp;
   s.intro={started:s.time,players:[boss.id],boss:true};
   this.cinematic={kind:'boss-intro',at:s.time,until:s.time+SIREN_INTRO.duration+PVE.bossIntroExtra};
   return;
  }
  if(c.kind==='boss-intro'){
   s.intro=undefined;this.stage='boss';this.stageAt=s.time;this.stageUntil=undefined;this.shown=true;this.endHold();return;
  }
  if(c.kind==='reveal'&&c.chip)this.endTransition(c.chip);
 }
 /** End of the chip 2/3/4 reveal: the chip's mechanic, the next floor, the threshold bonus and the phase respawn (§6.4). */
 private endTransition(chip:Chip){
  const h=this.host,s=h.sim,boss=this.boss();
  if(boss){
   const mech=BOSSES[h.options.boss].track[chip-1]?.mechanic;
   if(mech==='possess')this.startPossession(boss);
   else if(mech==='grow')grow(boss,s.time);
   else if(mech==='nocd')play(boss,s.time);
  }
  this.phase=chip;this.floor=chip<4?PVE_SIREN.thresholds[chip-1]*this.maxHp:0;
  h.economy.grantAll(ECONOMY.bossThresholdBonus,'boss-threshold');
  if(PVE.respawnOnPhase)h.economy.respawnAll('phase');
  this.endHold();
 }

 // ---- possession (§6.5) ----
 private startPossession(boss:Player){
  const h=this.host;h.sim.siren.clearEffects(boss);
  // Hidden: body and collider off (facade). Not connected either, so Simulation.botInput and chaos zombies skip it.
  h.setHidden(boss,true);boss.connected=false;this.hiddenBoss=true;this.possessed=undefined;
  this.possessionAt=h.sim.time;
  // With no active add, the horde queues a possessed shambler itself (§6.5); stepBoss polls for it.
  const n=h.horde.possess()??h.horde.possessed();
  if(n!==undefined&&n!==null)this.markPossessed(n);
 }
 private markPossessed(n:number){
  const h=this.host;this.possessed=n;this.possessedAt=h.sim.time;
  h.emit(PVE_EVENT.possess,this.boss()?.body.translation()??ORIGIN,{target:ADD_PREFIX+n});
 }
 private unhide(boss:Player){if(!this.hiddenBoss)return;boss.connected=true;this.host.setHidden(boss,false);this.hiddenBoss=false;}
 /** The possessed add died: the boss is found at 50% HP over it and the chip-3 reveal starts on the same tick. */
 private found(d:AddDeath){
  const h=this.host,s=h.sim,boss=this.boss();if(!boss)return;
  // An add that fell out of the world gives the boss back at its spawn.
  const sp=PVE_LAYOUT.bossSpawn,at=d.cause==='fall'?{x:sp.x,y:1,z:sp.z}:d.pos;
  boss.connected=true;boss.hp=.5*this.maxHp;revealAt(h,boss,at);this.hiddenBoss=false;this.foundAt=s.time;
  if(this.possessed===undefined&&d.n>=0)this.possessed=d.n;
  h.emit(PVE_EVENT.found,boss.body.translation(),{...(d.killer?{target:d.killer}:{}),amount:d.n});
  this.startReveal(3);
 }

 // ---- outcome (§6.8, D7) ----
 /** No living human (disconnected counts as dead) or party bot, and no pending self-revive. Recruits never count. */
 private defeated(){
  const h=this.host;
  return !h.members().some(p=>p.hp>0&&(h.isPartyBot(p)||p.connected))&&!h.economy.pendingRevive();
 }
 private finish(outcome:PveOutcome){
  const h=this.host,s=h.sim;
  if(outcome==='defeat')h.horde.killAll('defeat');
  s.intro=undefined;this.hold=false;this.cinematic=undefined;h.horde.setFlow(null);
  this.outcome=outcome;this.endedAt=s.time;this.stage='ended';this.stageAt=s.time;this.stageUntil=undefined;
  h.finish(outcome);
 }

 // ---- hooks ----
 hpFloor(p:Player):number|undefined{return this.host.isBoss(p)&&this.bossId===p.id?this.floor:undefined;}
 bossInput(p:Player,base:Input):Input{return bossInput(this.host,p,base);}
 /** §3.10: the stage scale (waveScale(w) during waves, bossStageScale() once the boss is coming) × every revealed chip's empower. */
 modifiers():HordeModifiers{
  const empower=BOSSES[this.host.options.boss].track.slice(0,this.track.revealed).map(c=>c.empower??{});
  const bossStage=this.stage==='boss'||this.stage==='ended'||this.bossId!==undefined||this.schedule.stage==='done';
  return combineMods(bossStage?bossStageScale():waveScale(Math.max(1,this.wave)),...empower);
 }
 onAddKilled(d:AddDeath){
  if(this.stage==='wave')this.waveKilled++;
  if(d.possessed&&this.stage==='boss'&&this.phase===2&&this.hiddenBoss&&!this.outcome)this.found(d);
 }
 onDeath(p:Player){
  if(this.host.isBoss(p)&&p.id===this.bossId&&!this.outcome&&!this.pendingOutcome){this.host.horde.killAll('boss-dead');this.startOutro('victory');}
 }
 onDamage(_p:Player,_actual:number,_source:string|null){}

 // ---- view ----
 view():ReturnType<DirectorApi['view']>{
  const out:ReturnType<DirectorApi['view']>={stage:this.stage,stageAt:this.stageAt,wave:this.wave,waves:PVE.wavesBeforeBoss,
   waveTotal:this.waveTotal,waveLeft:this.stage==='wave'?this.remaining():0,hold:this.hold,track:{...this.track}};
  if(this.stageUntil!==undefined)out.stageUntil=this.stageUntil;
  if(this.cinematic)out.cinematic={...this.cinematic};
  const bv=this.bossView();if(bv)out.bossView=bv;
  if(this.outcome){out.outcome=this.outcome;out.endedAt=this.endedAt;}
  return out;
 }
 private bossView():PveBossView|undefined{
  if(!this.bossId||!this.shown)return undefined;
  const v:PveBossView={id:this.bossId,phase:this.phase,floor:this.floor};
  if(this.hiddenBoss)v.hidden=true;
  if(this.possessed!==undefined)v.possessed=this.possessed;
  if(this.possessedAt!==undefined)v.possessedAt=this.possessedAt;
  if(this.foundAt!==undefined)v.foundAt=this.foundAt;
  return v;
 }

 // ---- debug (D29; the facade gates on options.debug and a trusted sender) ----
 debug(op:string){
  const s=this.host.sim;if(!this.started||this.outcome||s.phase!=='fight')return false;
  if(op==='skip-wave'){
   if(this.hold||this.schedule.stage==='done')return false;
   const e=this.schedule.skip(s.time);if(!e)return false;this.onWaveEvent(e);return true;
  }
  if(op==='skip-to-boss'){
   if(this.hold||this.schedule.stage==='done')return false;
   this.onWaveEvent(this.schedule.finish(s.time));return true;
  }
  const m=/^boss-phase:([2-4])$/.exec(op);
  if(m){
   const n=Number(m[1]) as Chip,boss=this.boss();
   if(this.stage!=='boss'||this.hold||!boss||n<=this.phase)return false;
   // Apply the earlier chips at once (possession is skipped), then reveal chip n normally, with its hold, from its threshold.
   for(let k=this.phase+1;k<n;k++){if(k===3){this.unhide(boss);grow(boss,s.time);}this.phase=k as Phase;}
   if(n===3)this.unhide(boss);
   boss.hp=Math.min(boss.hp,PVE_SIREN.thresholds[n-2]*this.maxHp);this.floor=boss.hp;
   this.track={revealed:(n-1) as 1|2|3,at:s.time};
   this.startReveal(n);return true;
  }
  return false;
 }

 // ---- helpers ----
 private boss(){return this.bossId?this.host.sim.players.get(this.bossId):undefined;}
 /** The boss-stage trickle (null outside the boss stage and during holds, which set it to null themselves). */
 private applyFlow(){
  const h=this.host;if(this.stage!=='boss'||this.outcome)return;
  h.horde.setFlow(bossFlow(this.phase,h.partySize(),this.phase===2&&this.hiddenBoss,h.options.relay===true));
 }
 /** Adds of the running wave still to kill: its budget plus the adds carried over, minus kills (never below the field). */
 private remaining(){
  const h=this.host,field=h.horde.alive()+h.horde.queued();
  return Math.max(field,this.waveTotal+this.waveCarry-this.waveKilled);
 }
 /** Every connected human resolved the start pick: not choosing, and no start pick that was never opened. */
 private allResolved(){
  const h=this.host;
  return h.humans().filter(p=>p.connected).every(p=>!h.economy.choosing(p)&&!h.economy.partyView(p.id).picks.some(k=>k.source==='start'&&!k.offers));
 }
}
