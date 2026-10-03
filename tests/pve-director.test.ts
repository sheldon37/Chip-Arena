// PvE director flow (design §2.4, §6.8, §11 S2): stages, wave budgets and clear rule, intermissions, respawns, holds,
// outcome, retry, debug ops, boss pathing, determinism and rate independence. Real Director; fake horde, economy, recruits.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput,v} from '../src/shared/config.js';
import {BOSS_PLAYER_ID,type PveOptions} from '../src/shared/pve/contract.js';
import {Director} from '../src/shared/pve/director.js';
import {WaveSchedule,bossFlow,waveBudget,waveFlow} from '../src/shared/pve/waves.js';
import {HORDE,PVE,WAVES,bossStageScale,partyFactor,waveScale} from '../src/shared/pve/balance.js';
import {SIREN_INTRO} from '../src/shared/siren-contract.js';
import {fakeParts,withPveParts,type FakeEconomy,type FakeHorde,type FakeRecruits} from './helpers/pve-fakes.js';
await initPhysics();

const KEY='0123456789abcdef0123456789abcdef';
const DT=1/60;
type Rig={s:Simulation;d:Director;horde:FakeHorde;economy:FakeEconomy;recruits:FakeRecruits};
function rig(o:Partial<PveOptions>={},humans=1):Rig{
 const {factory,made}=fakeParts({director:h=>new Director(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:5,...o}}));
 for(let i=1;i<=humans;i++){s.addPlayer('h'+i,'P'+i,'ranged');assert.equal(s.pve!.command('h'+i,{action:'hello',v:1,key:KEY},false),true);}
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
 return {s,d:s.pve!.director as Director,horde:made.horde!,economy:made.economy!,recruits:made.recruits!};
}
const view=(s:Simulation)=>s.pve!.view();
const label=(s:Simulation)=>{const w=view(s);return w.cinematic?`${w.cinematic.kind}${w.cinematic.chip??''}`:w.stage==='wave'?`wave${w.wave}`:w.stage;};
/** Kills `perTick` adds of the running wave each tick (spawned through the fake horde), so waves drain deterministically. */
function drain(r:Rig,perTick=1){if(view(r.s).stage!=='wave')return;for(let k=0;k<perTick&&view(r.s).waveLeft>0;k++){const m=r.horde.addAt({x:10,z:-10});r.horde.kill(Number(m.id.slice(4)),'h1');}}
/** Steps until `done` and records every label change with its time. */
function run(r:Rig,done:()=>boolean,max=20000,kill=1){
 const log:{label:string;at:number}[]=[{label:label(r.s),at:r.s.time}];
 for(let i=0;i<max&&!done();i++){if(kill)drain(r,kill);r.s.step();const l=label(r.s);if(l!==log.at(-1)!.label)log.push({label:l,at:r.s.time});}
 assert.ok(done(),'condition reached');return log;
}

test('wave budgets and flows follow partyFactor (party bots counted) and the HORDE cap',()=>{
 for(const h of [1,2,3,4])for(let w=1;w<=3;w++){
  const spec=WAVES[w-1],f=waveFlow(w,h);
  assert.equal(waveBudget(w,h),Math.round(spec.count*partyFactor(h)));assert.equal(f.budget,waveBudget(w,h));
  assert.ok(Math.abs(f.rate-spec.rate*(1+.35*(h-1)))<1e-9);assert.equal(f.maxAlive,Math.min(HORDE.maxAlive,spec.maxAlive+5*(h-1)));
  assert.ok(waveFlow(w,h,{count:1},true).maxAlive<=HORDE.maxAliveRelay);
 }
 assert.equal(waveBudget(3,4),62,'D6: W3 for 4 members is 62 adds');
 assert.deepEqual([1,2,3,4].map(h=>bossFlow(1,h,false).maxAlive),[6,10,14,18]);
 assert.deepEqual([1,2,3,4].map(h=>+bossFlow(1,h,false).rate.toFixed(6)),[.6,1,1.4,1.8]);
 // In a sim, h counts party bots: 1 human + 3 party bots runs W1 with 39 adds.
 const r=rig({partyBots:3});try{
  assert.equal(r.s.pve!.partySize(),4);r.s.pve!.debug('god:on');r.d.debug('skip-wave');
  assert.equal(view(r.s).stage,'wave');assert.equal(view(r.s).waveTotal,waveBudget(1,4));assert.equal(view(r.s).waveTotal,39);
  assert.deepEqual(r.horde.flows.at(-1),waveFlow(1,4));
  assert.deepEqual(r.d.modifiers(),{...waveScale(1)});
 }finally{r.s.dispose();}
});

test('flow: opening → W1 → I → W2 → I → W3 → I(8 s) → reveal 1 → boss-intro → boss, with balance times and one respawn per clear',()=>{
 const r=rig();const {s}=r;try{
  s.pve!.debug('god:on');r.economy.openPick('h1');   // the human is choosing: the opening runs its full 10 s
  const log=run(r,()=>view(s).stage==='boss'&&!view(s).hold);
  assert.deepEqual(log.map(e=>e.label),['opening','wave1','intermission','wave2','intermission','wave3','intermission','reveal1','boss-intro','boss']);
  const at=(l:string,k=0)=>log.filter(e=>e.label===l)[k].at,near=(a:number,b:number)=>assert.ok(Math.abs(a-b)<=DT+1e-9,`${a} vs ${b}`);
  near(at('wave1')-log[0].at,PVE.openingSeconds);
  near(at('wave2')-at('intermission',0),PVE.intermissionSeconds);near(at('wave3')-at('intermission',1),PVE.intermissionSeconds);
  near(at('reveal1')-at('intermission',2),PVE.lastIntermissionSeconds);
  near(at('boss-intro')-at('reveal1'),PVE.reveal.smile+PVE.reveal.card+PVE.reveal.settle);
  near(at('boss')-at('boss-intro'),SIREN_INTRO.duration+PVE.bossIntroExtra);
  // Clear rule: ≤2 left for 3 s. One add is killed per tick, so each wave clears 3 s after its count reaches 2.
  for(const w of [1,2,3]){const len=at('intermission',w-1)-at('wave'+w);near(len,(waveBudget(w,1)-PVE.waveClearRemainder)*DT+PVE.waveClearHold);}
  assert.deepEqual(r.economy.respawns,['wave','wave','wave'],'respawnAll(wave) once per clear');
  assert.deepEqual(r.economy.grants.filter(g=>g.reason==='wave-clear').map(g=>g.amount),[60,80,100]);
  assert.equal(r.horde.flows.filter(f=>f&&f.budget>0).length,3);assert.equal(r.horde.flows.at(-1)!.budget,-1,'boss trickle');
  assert.deepEqual(r.d.modifiers(),bossStageScale());
  assert.ok(s.events.some(e=>e.type==='pve-wave'&&e.message==='clear'&&e.amount===3));
  assert.equal(view(s).track.revealed,1);
 }finally{s.dispose();}
});

test('opening ends 3 s after every human resolved the start pick (at least 4 s); skipWaves goes straight to the reveal',()=>{
 const r=rig();const {s}=r;try{
  r.economy.openPick('h1');for(let i=0;i<120;i++)s.step();r.economy.closePick('h1');const resolved=s.time;
  const log=run(r,()=>view(s).stage==='wave',2000,0);assert.ok(Math.abs(log.at(-1)!.at-(resolved+DT+PVE.openingEarlySeconds))<=DT+1e-9);
 }finally{s.dispose();}
 const q=rig();try{const log=run(q,()=>view(q.s).stage==='wave',2000,0);assert.ok(Math.abs(log.at(-1)!.at-PVE.openingMinSeconds-log[0].at)<=DT+1e-9,'minimum 4 s');}finally{q.s.dispose();}
 const k=rig({skipWaves:true});try{
  const log=run(k,()=>view(k.s).cinematic?.kind==='reveal',2000,0);assert.deepEqual(log.map(e=>e.label),['opening','reveal1']);
  assert.equal(k.economy.respawns.length,0);
 }finally{k.s.dispose();}
});

test('waveMaxSeconds forces the clear; waves keep counting carried-over adds',()=>{
 const r=rig();const {s}=r;try{
  s.pve!.debug('god:on');r.d.debug('skip-wave');const start=s.time;
  const log=run(r,()=>view(s).stage==='intermission',20000,0);
  assert.ok(Math.abs(log.at(-1)!.at-start-PVE.waveMaxSeconds)<=DT+1e-9);assert.deepEqual(r.economy.respawns,['wave']);
  // Two stragglers carried into W2 count toward its total left.
  r.horde.addAt({x:5,z:5});r.horde.addAt({x:6,z:5});r.d.debug('skip-wave');
  assert.equal(view(s).wave,2);assert.equal(view(s).waveLeft,waveBudget(2,1)+2);
 }finally{s.dispose();}
});

test('defeat: everyone down with no pending self-revive (disconnected counts as dead); never while a self-revive is pending',()=>{
 const r=rig({},2);const {s}=r;try{
  const [a,b]=[s.players.get('h1')!,s.players.get('h2')!];
  b.connected=false;s.damage(a,1000,null,'direct');assert.equal(a.hp,0);
  r.economy.pending=true;for(let i=0;i<30;i++)s.step();assert.equal(view(s).cinematic,undefined,'self-revive pending');
  r.economy.pending=false;s.step();assert.equal(view(s).cinematic?.kind,'outro');assert.equal(view(s).cinematic?.outcome,'defeat');
  const until=view(s).cinematic!.until;assert.ok(Math.abs(until-view(s).cinematic!.at-PVE.outroDefeatSeconds)<1e-9);
  run(r,()=>s.phase==='match',400,0);
  assert.equal(s.pve!.outcome,'defeat');assert.equal(view(s).outcome,'defeat');assert.equal(s.winner,null);assert.ok(Math.abs(s.time-until)<=DT+1e-9);
  assert.deepEqual(r.horde.killAllCauses,['defeat']);assert.ok(view(s).party.h1,'results rows present');
  // Retry: a fresh run with a new run seed.
  const seed=s.pve!.runSeed;assert.equal(s.rematch(),true);assert.notEqual(s.pve!.runSeed,seed);assert.equal(view(s).stage,'opening');assert.equal(view(s).outcome,undefined);
  assert.equal(s.players.has(BOSS_PLAYER_ID),false);
 }finally{s.dispose();}
});

test('victory: boss death → outro (4 s) → match with results; party bots alone prevent defeat',()=>{
 const r=rig({skipWaves:true,partyBots:1});const {s}=r;try{
  s.pve!.debug('god:on');run(r,()=>view(s).stage==='boss'&&!view(s).hold,4000,0);
  // The human is down but a party bot lives: no defeat.
  s.pve!.debug('god:off');const h=s.players.get('h1')!;s.damage(h,1e6,null,'direct');for(let i=0;i<10;i++)s.step();assert.equal(view(s).cinematic,undefined);
  r.d.debug('boss-phase:4');run(r,()=>!view(s).hold,1000,0);
  const boss=s.players.get(BOSS_PLAYER_ID)!;s.damage(boss,1e9,'h1','direct');assert.equal(boss.hp,0);
  assert.equal(view(s).cinematic?.kind,'outro');assert.equal(view(s).cinematic?.outcome,'victory');assert.deepEqual(r.horde.killAllCauses,['boss-dead']);
  const until=view(s).cinematic!.until;run(r,()=>s.phase==='match',600,0);
  assert.ok(Math.abs(s.time-until)<=DT+1e-9);assert.equal(s.pve!.outcome,'victory');assert.equal(s.winner,'h1');assert.equal(s.result,'The Siren is silenced');
  assert.equal(view(s).stage,'ended');assert.ok(view(s).party.h1&&view(s).party['party-bot-1']);
 }finally{s.dispose();}
});

test('boss pathing: without a straight line the move follows nav.direction (boss grid once grown); idle while hidden',()=>{
 const r=rig({skipWaves:true});const {s}=r;try{
  s.pve!.debug('god:on');run(r,()=>view(s).stage==='boss'&&!view(s).hold,4000,0);
  const boss=s.players.get(BOSS_PLAYER_ID)!,nav=r.horde.nav as unknown as {clear:(a:unknown,b:unknown,g?:string)=>boolean;direction:(f:unknown,t:string,g?:string)=>{x:number;z:number}|null};
  const base={...idleInput(),x:1,z:0,seq:3,yaw:.5};
  assert.deepEqual(r.d.bossInput(boss,base),base,'line of sight: the bot input is kept');
  const grids:string[]=[];nav.clear=(_a,_b,g)=>{grids.push(g??'');return false;};nav.direction=(_f,t,g)=>{grids.push('dir:'+t+':'+g);return {x:0,z:-1};};
  const out=r.d.bossInput(boss,base);assert.equal(out.x,0);assert.equal(out.z,-1);assert.equal(out.yaw,.5);assert.deepEqual(grids,['party','dir:h1:party']);
  r.d.debug('boss-phase:3');run(r,()=>!view(s).hold,1000,0);grids.length=0;r.d.bossInput(boss,base);assert.deepEqual(grids,['boss','dir:h1:boss']);
  s.pve!.setHidden(boss,true);const idle=r.d.bossInput(boss,base);assert.equal(idle.x,0);assert.equal(idle.z,0);assert.equal(idle.attack,false);s.pve!.setHidden(boss,false);
 }finally{s.dispose();}
});

test('debug ops: skip-wave, skip-to-boss and boss-phase:n; refused when not applicable',()=>{
 const r=rig();const {s}=r;try{
  s.pve!.debug('god:on');
  assert.equal(r.d.debug('boss-phase:2'),false);assert.equal(r.d.debug('nope'),false);
  assert.equal(r.d.debug('skip-wave'),true);assert.equal(view(s).stage,'wave');assert.equal(r.d.debug('skip-wave'),true);assert.equal(view(s).stage,'intermission');
  assert.equal(r.d.debug('skip-to-boss'),true);assert.equal(view(s).cinematic?.chip,1);assert.equal(r.d.debug('skip-to-boss'),false);
  run(r,()=>view(s).stage==='boss'&&!view(s).hold,4000,0);
  assert.equal(r.d.debug('boss-phase:1'),false);assert.equal(r.d.debug('boss-phase:4'),true);assert.equal(view(s).cinematic?.chip,4);assert.equal(view(s).track.revealed,3);
  run(r,()=>!view(s).hold,1000,0);assert.equal(view(s).bossView!.phase,4);
  const boss=s.players.get(BOSS_PLAYER_ID)!;assert.equal(boss.siren!.boost!.scale,1.5,'chip 3 applied on the way');assert.equal(boss.siren!.boost!.cooldown,0);
 }finally{s.dispose();}
});

test('determinism: the same seed gives the same stage timeline',()=>{
 const timeline=()=>{const r=rig({seed:99});try{r.s.pve!.debug('god:on');return JSON.stringify(run(r,()=>view(r.s).stage==='boss'&&!view(r.s).hold,20000,2));}finally{r.s.dispose();}};
 assert.equal(timeline(),timeline());
});

test('rate independence: WaveSchedule at 30/60/120 Hz gives the same transition times within one step',()=>{
 const trace=(dt:number)=>{
  const w=new WaveSchedule(3,false);w.start(0);const ev:{kind:string;at:number}[]=[];let killedFrom=0,total=0;
  for(let t=dt;t<200;t+=dt){
   // The start pick resolves at 2.5 s; each wave drains at 1.7 adds per second from its start.
   const remaining=w.stage==='wave'?Math.max(0,total-Math.floor((t-killedFrom)*1.7)):0;
   for(const e of w.step({time:t,remaining,resolvedAt:t>=2.5?2.5:undefined})){ev.push({kind:e.kind,at:e.at});if(e.kind==='wave-start'){killedFrom=e.at;total=waveBudget(e.wave,1);}}
   if(w.stage==='done')break;
  }
  return ev;
 };
 const a=trace(1/30),b=trace(1/60),c=trace(1/120);
 assert.deepEqual(a.map(e=>e.kind),['wave-start','wave-clear','wave-start','wave-clear','wave-start','wave-clear','boss']);
 for(const x of [b,c]){assert.equal(x.length,a.length);x.forEach((e,i)=>{assert.equal(e.kind,a[i].kind);assert.ok(Math.abs(e.at-a[i].at)<=1/30+1e-9,`${e.kind} ${e.at} vs ${a[i].at}`);});}
 assert.ok(Math.abs(a[0].at-5.5)<=1/30+1e-9,'opening: resolved at 2.5 s + 3 s');
 void v;
});
