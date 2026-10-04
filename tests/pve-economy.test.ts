// PvE S3 economy: rewards, prices, deny cases, pickups, revive/respawn/self-revive, holds, rekey, determinism and rate
// independence (design §4, §11 S3). Real Economy and Recruits; fake horde (real add bodies) and fake director.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import type {Minion} from '../src/shared/chaos.js';
import {PVE_EVENT,type PveOptions} from '../src/shared/pve/contract.js';
import {ADD_KINDS,ECONOMY,PVE,PRICES,chipPrice,revivePrice} from '../src/shared/pve/balance.js';
import {PVE_MACHINES,PVE_LAYOUT} from '../src/shared/pve/layout.js';
import {Economy} from '../src/shared/pve/economy.js';
import {Recruits} from '../src/shared/pve/recruits.js';
import {fakeParts,withPveParts,type FakeDirector,type FakeHorde} from './helpers/pve-fakes.js';
await initPhysics();

interface World{s:Simulation;econ:Economy;rec:Recruits;horde:FakeHorde;director:FakeDirector}
function world(o:Partial<PveOptions>={},humans:string[]=['h1'],start=true):World{
 let econ!:Economy,rec!:Recruits;
 const {factory,made}=fakeParts({economy:h=>(econ=new Economy(h)),recruits:h=>(rec=new Recruits(h))});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,...o}}));
 for(const id of humans){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;
 if(start)s.pve!.onFightStart();
 return {s,econ,rec,horde:made.horde!,director:made.director!};
}
const P=(s:Simulation,id:string)=>s.players.get(id)!;
const place=(p:Player,x:number,z:number)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);};
/** Holds or releases F for the next tick (fresh input so the stale-input rule never idles it). */
const HELD=new WeakMap<Simulation,Set<string>>();
const held=(s:Simulation)=>{let h=HELD.get(s);if(!h){h=new Set();HELD.set(s,h);}return h;};
const press=(s:Simulation,p:Player,on:boolean)=>{if(on)held(s).add(p.id);else held(s).delete(p.id);};
/** Steps with fresh human inputs (the stale-input rule never idles them); F stays held for pressed players, like a client. */
const tick=(s:Simulation,n=1,dt?:number)=>{for(let i=0;i<n;i++){for(const p of s.players.values())if(!p.bot){p.input={...p.input,interact:held(s).has(p.id),seq:p.input.seq+1};p.inputAt=s.time;}s.step(dt);}};
/** Press F for one tick, then release for one tick. */
function tapF(s:Simulation,p:Player){press(s,p,true);tick(s);press(s,p,false);tick(s);}
const events=(s:Simulation,type:string)=>s.events.filter(e=>e.type===type);
const closeAll=(w:World)=>{for(const p of w.s.players.values())if(w.econ.choosing(p))w.s.pve!.command(p.id,{action:'close'},false);};
const kill=(s:Simulation,p:Player)=>{s.damage(p,1e6,null,'environment');assert.ok(p.hp<=0&&p.death);};
const front=(m:number)=>PVE_MACHINES[m].front;

test('rewards: per kind, head bonus, assists, recruit share, chaos-minion owner, possessed kill, boss %; party bots earn nothing',()=>{
 const w=world({partyBots:1},['h1','h2']);const {s,econ,rec}=w;try{
  const host=s.pve!,bot='party-bot-1';
  const S=ECONOMY.startMoney,sh=ADD_KINDS.shambler.reward,ru=ADD_KINDS.runner.reward,br=ADD_KINDS.brute.reward,hb=ECONOMY.headBonus;
  const as=Math.round(sh*ECONOMY.assistShare);
  assert.equal(econ.money('h1'),S);assert.equal(econ.money('h2'),S);assert.equal(econ.money(bot),0);
  const dead=(o:object)=>host.onAddKilled({n:1,kind:'shambler',pos:v(),killer:'h1',assists:[],headless:false,possessed:false,cause:'damage',...o});
  dead({});assert.equal(econ.money('h1'),S+sh);
  dead({kind:'runner'});assert.equal(econ.money('h1'),S+sh+ru);
  dead({kind:'brute'});assert.equal(econ.money('h1'),S+sh+ru+br);
  dead({headless:true});assert.equal(econ.money('h1'),S+2*sh+ru+br+hb,'kill + head bonus');
  dead({assists:['h2','h1',bot]});assert.equal(econ.money('h2'),S+as,'assist 40% of the reward');const h1m=S+3*sh+ru+br+hb;assert.equal(econ.money('h1'),h1m);
  // A recruit's kill pays its hirer 50%.
  const r=rec.hire('h1',host.standAt(0,0),0)!;assert.ok(r);
  dead({killer:r,kind:'brute'});assert.equal(econ.money('h1'),h1m+Math.round(br*ECONOMY.recruitKillShare));
  // A player-owned chaos minion pays its owner.
  s.chaos.minions.set('chaos-zombie-1',{id:'chaos-zombie-1',owner:'h2',kind:'zombie'} as unknown as Minion);
  dead({killer:'chaos-zombie-1'});assert.equal(econ.money('h2'),S+as+sh);s.chaos.minions.delete('chaos-zombie-1');
  // Party bots: kills, assists, grants and boss damage never pay.
  dead({killer:bot,assists:[bot]});econ.grant(bot,500,'test');econ.onBossDamage(bot,9999,6000);assert.equal(econ.money(bot),0);
  assert.equal(econ.partyView(bot).kind,'bot');
  // Possessed kill: killer and others get the possessed bonus (the killer also the normal reward).
  const before1=econ.money('h1'),before2=econ.money('h2');dead({possessed:true});
  assert.equal(econ.money('h1')-before1,ECONOMY.possessedKill+sh);assert.equal(econ.money('h2')-before2,ECONOMY.possessedOthers);
  // Boss: ECONOMY.bossPerPercent per cumulative 1% of max HP.
  const bp=ECONOMY.bossPerPercent;
  const b=econ.money('h2');econ.onBossDamage('h2',59,6000);assert.equal(econ.money('h2'),b);econ.onBossDamage('h2',1,6000);assert.equal(econ.money('h2'),b+bp);
  econ.onBossDamage('h2',600,6000);assert.equal(econ.money('h2'),b+11*bp);
  // Wave bonus reaches dead humans too.
  kill(s,P(s,'h2'));const d2=econ.money('h2');econ.grantAll(60,'wave');assert.equal(econ.money('h2'),d2+60);
  assert.ok(events(s,PVE_EVENT.cash).some(e=>e.target==='h2'&&e.amount===60&&e.message==='wave'));
  assert.ok(!events(s,PVE_EVENT.cash).some(e=>e.message==='kill'),'the horde emits pve-kill for the killer share');
  // Reward modifiers from the director scale the base reward.
  w.director.mods.reward=2;const m=econ.money('h1');dead({});assert.equal(econ.money('h1')-m,2*ADD_KINDS.shambler.reward);
 }finally{s.dispose();}
});

test('owner money retune (2026-10-03): income about 2.5x, prices about 1.2x',()=>{
 assert.deepEqual([ADD_KINDS.shambler.reward,ADD_KINDS.runner.reward,ADD_KINDS.brute.reward,ECONOMY.headBonus],[25,20,80,10]);
 assert.deepEqual([ECONOMY.startMoney,ECONOMY.waveClearBase,ECONOMY.waveClearPerWave,ECONOMY.bossThresholdBonus,ECONOMY.bossPerPercent],[150,100,50,250,10]);
 assert.deepEqual([ECONOMY.possessedKill,ECONOMY.possessedOthers,PRICES.recruit,PRICES.heal],[300,100,300,75]);
});

test('prices escalate (chip 175..350, revive 150..400); heal cooldown 12 s; every deny charges nothing and emits pve-deny',()=>{
 assert.deepEqual([0,1,2,3,4,5,6,7,8].map(chipPrice),[175,200,225,250,275,300,325,350,350]);
 assert.deepEqual([0,1,2,3,4,5,6].map(revivePrice),[150,200,250,300,350,400,400]);
 const w=world({},['h1','h2']);const {s,econ}=w;try{
  closeAll(w);const h1=P(s,'h1'),spent:number[]=[];
  s.pve!.command('h1',{action:'debug',op:'money:5000'},true);   // debug is off in this room: refused
  assert.equal(econ.money('h1'),ECONOMY.startMoney);econ.grant('h1',5000,'test');
  const f=front(0);place(h1,f.x,f.z);
  for(let i=0;i<8;i++){const m=econ.money('h1');tapF(s,h1);spent.push(m-econ.money('h1'));
   // grab the pickup once it is born so the 3-ungrabbed cap never applies. Chips now land CHIP_DROP_DISTANCE out from
   // the machine (owner 2026-10-03), so walk to the chip to take it, then return to the machine.
   tick(s,40);const k=econ.pickupsView().find(x=>x.owner==='h1')!;place(h1,k.pos.x,k.pos.z);tapF(s,h1);closeAll(w);place(h1,f.x,f.z);}
  assert.deepEqual(spent,[175,200,225,250,275,300,325,350]);assert.equal(econ.partyView('h1').chipsBought,8);
  // 3 ungrabbed pickups.
  for(let i=0;i<3;i++){tapF(s,h1);}
  const m3=econ.money('h1');tapF(s,h1);assert.equal(econ.money('h1'),m3);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'pickups');
  assert.equal(econ.pickupsView().filter(k=>k.owner==='h1').length,3);
  // Med: full HP, then cooldown, then money.
  const med=front(6);place(h1,med.x,med.z);tick(s);
  const full=econ.money('h1');tapF(s,h1);assert.equal(econ.money('h1'),full);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'full');
  h1.hp=40;tapF(s,h1);assert.equal(econ.money('h1'),full-PRICES.heal);const at=s.time;
  tick(s,60*3);assert.ok(Math.abs(h1.hp-90)<1e-6,`heal 50% over 2 s: ${h1.hp}`);
  h1.hp=40;tapF(s,h1);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'cooldown');assert.equal(econ.money('h1'),full-PRICES.heal);
  assert.ok(Math.abs(econ.partyView('h1').healReadyAt-(at-1/60+PRICES.healCooldown))<.05);
  while(s.time<econ.partyView('h1').healReadyAt+.02)tick(s);tapF(s,h1);assert.equal(econ.money('h1'),full-2*PRICES.heal,'ready again after 12 s');
  // No money: chip, recruit, med.
  const h2=P(s,'h2');econ.grant('h2',-10000,'test');assert.equal(econ.money('h2'),0);
  for(const m of [0,4,7]){const fm=front(m);place(h2,fm.x,fm.z);h2.hp=50;tapF(s,h2);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'money',`machine ${m}`);}
  assert.equal(econ.money('h2'),0);assert.equal(econ.partyView('h2').spent,0);
  assert.ok(econ.machinesView().some(u=>u.result==='deny'&&u.by==='h2'));
 }finally{s.dispose();}
});

test('recruit machine: seats full (pending hire counted) and per-player limit deny without charge; a hire pops after 0.8 s',()=>{
 const w=world({},['h1','h2']);const {s,econ,rec}=w;try{
  closeAll(w);econ.grant('h1',5000,'t');econ.grant('h2',5000,'t');
  // Filler seats: 2 humans + 4 fillers = 6, boss seat reserved → 1 free seat.
  for(let k=90;k<94;k++)assert.ok(s.addPlayer('recruit-'+k,'filler','ranged','normal'));
  const h1=P(s,'h1'),h2=P(s,'h2'),f=front(4);place(h1,f.x,f.z);place(h2,f.x+.3,f.z);
  tapF(s,h1);assert.equal(econ.pendingHires(),1);assert.equal(econ.money('h1'),ECONOMY.startMoney+5000-PRICES.recruit);
  const m2=econ.money('h2');tapF(s,h2);assert.equal(econ.money('h2'),m2,'no charge');assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'seats');
  tick(s,50);assert.equal(econ.pendingHires(),0);assert.equal(rec.views().length,1);assert.equal(econ.partyView('h1').recruitsHired,1);
  assert.ok(events(s,PVE_EVENT.recruit).some(e=>e.source==='h1'));
  const r=rec.views()[0];assert.equal(r.owner,'h1');assert.equal(r.m,4);
  // Free the fillers: per-player cap 2 → third is 'limit'.
  for(let k=90;k<94;k++)s.removePlayer('recruit-'+k);
  place(h1,f.x,f.z);tapF(s,h1);tick(s,50);assert.equal(rec.views().filter(x=>x.owner==='h1').length,2);
  const m1=econ.money('h1');tapF(s,h1);assert.equal(econ.money('h1'),m1);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'limit');
  // Total cap 3 (h2 hires one, then a pending second hire would exceed).
  place(h2,f.x,f.z);tapF(s,h2);assert.equal(econ.pendingHires(),1);const mm=econ.money('h2');tapF(s,h2);assert.equal(econ.money('h2'),mm);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'limit');
 }finally{s.dispose();}
});

test('pickups: owner-only, only after born; a taken chip waits as a pending pick and never opens the chip screen',()=>{
 const w=world({},['h1','h2']);const {s,econ,horde}=w;try{
  closeAll(w);econ.grant('h1',chipPrice(0)-ECONOMY.startMoney,'t');const h1=P(s,'h1'),h2=P(s,'h2'),f=front(0);place(h1,f.x,f.z);
  tapF(s,h1);const k=econ.pickupsView()[0];assert.ok(k&&k.owner==='h1');assert.ok(k.born>s.time-.1);
  tapF(s,h1);assert.equal(econ.pickupsView().length,1,'not born yet: the press reaches the machine (no money)');
  assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'money');
  place(h2,k.pos.x,k.pos.z);econ.grant('h2',-ECONOMY.startMoney,'t');tick(s,40);tapF(s,h2);assert.equal(econ.pickupsView().length,1,'only the owner takes it');
  place(h1,k.pos.x,k.pos.z);tapF(s,h1);assert.equal(econ.pickupsView().length,0);
  const row=econ.partyView('h1');assert.equal(row.picks.length,2,'the start pick (Choose later) and the grabbed one');assert.equal(row.picks[1].source,'dispenser');assert.equal(row.open,undefined,'a taken chip never opens the chip screen');
  assert.ok(events(s,PVE_EVENT.grab).some(e=>e.target==='h1'));
  // Retired auto-open: neither the old prefs (published clients still send it) nor a clear area opens it.
  assert.equal(s.pve!.command('h1',{action:'prefs',autoOpen:true},false),true);econ.grant('h1',chipPrice(1),'t');place(h1,f.x,f.z);tapF(s,h1);tick(s,40);
  place(h1,econ.pickupsView()[0].pos.x,econ.pickupsView()[0].pos.z);tapF(s,h1);assert.equal(econ.partyView('h1').open,undefined);assert.equal(econ.partyView('h1').picks.length,3);
  assert.ok(!econ.choosing(h1),'free to move: nothing forced idle');
  assert.equal(s.pve!.command('h1',{action:'open'},false),true);assert.equal(econ.partyView('h1').open,row.picks[0].id,'C opens the oldest pending pick');
 }finally{s.dispose();}
});

test('paid revive: one F press within reach revives at once (owner playtest, no hold); deny without money; 50% HP with a shield; fresh death id',()=>{
 const w=world({},['h1','h2']);const {s,econ}=w;try{
  closeAll(w);const h1=P(s,'h1'),h2=P(s,'h2');place(h2,0,-8);tick(s);kill(s,h2);const firstDeath=h2.death!.id;
  const spot=h2.death!.pos,price=revivePrice(0);
  // Out of reach: the press does nothing.
  econ.grant('h1',price-econ.money('h1'),'t');place(h1,spot.x+PRICES.reviveRadius+.5,spot.z);tick(s);tapF(s,h1);assert.ok(h2.hp<=0);assert.equal(econ.money('h1'),price);
  // Not affordable: denied with 'money', nothing charged, still down.
  econ.grant('h1',-1,'t');place(h1,spot.x+1,spot.z);tick(s);const denies=events(s,PVE_EVENT.deny).length;tapF(s,h1);
  assert.ok(h2.hp<=0);assert.equal(econ.money('h1'),price-1);assert.equal(events(s,PVE_EVENT.deny).length,denies+1);assert.equal(events(s,PVE_EVENT.deny).at(-1)?.message,'money');
  // Affordable: the press tick itself revives and charges; the reviver's input is never forced idle.
  econ.grant('h1',1,'t');press(s,h1,true);h1.input={...h1.input,attack:true,x:1};tick(s);
  assert.ok(h2.hp>0,'revived on the press tick');assert.equal(h1.input.attack,true);assert.equal(h1.input.x,1);
  // Holding F on afterwards (and a second press) does nothing more.
  tick(s,30);press(s,h1,false);tick(s);
  assert.equal(econ.money('h1'),0);assert.equal(h2.hp,h2.stats.hp*.5);assert.equal(s.pve!.untouchable(h2),true);assert.equal(econ.partyView('h1').channel,undefined);
  assert.equal(s.pve!.stats('h1').revives,1);assert.equal(s.pve!.stats('h2').revived,1);assert.equal(econ.partyView('h2').reviveCost,revivePrice(1));
  assert.ok(events(s,PVE_EVENT.revive).some(e=>e.target==='h2'&&e.source==='h1'));
  tick(s,100);assert.equal(s.pve!.untouchable(h2),false);kill(s,h2);assert.notEqual(h2.death!.id,firstDeath);
 }finally{s.dispose();}
});

test('respawnAll revives dead connected members on a pad with no add within 4 m; once-keys reset (second-chance fires again)',()=>{
 const w=world({partyBots:1},['h1','h2']);const {s,econ,horde}=w;try{
  closeAll(w);const h1=P(s,'h1'),h2=P(s,'h2'),bot=P(s,'party-bot-1');
  // second-chance once-key used before the respawn.
  h1.chips['second-chance']=1;s.damage(h1,h1.hp*.8,null,'environment');assert.ok((s.chaos as unknown as {used:Set<string>}).used.has('h1:exit'));
  // 2026-10-03 rework (intended): Emergency Exit also grants 1.2 s immunity, which would absorb the scripted kills below.
  (s.chaos as unknown as {shields:Map<string,number>}).shields.clear();
  const pad0=PVE_LAYOUT.pads[0];place(h1,pad0.x,pad0.z);place(h2,pad0.x+.5,pad0.z);tick(s);
  kill(s,h1);kill(s,h2);kill(s,bot);h2.connected=false;
  horde.addAt({x:pad0.x+1,z:pad0.z});
  econ.respawnAll('wave');
  assert.ok(h1.hp>0&&bot.hp>0);assert.ok(h2.hp<=0,'disconnected humans stay down');
  for(const p of [h1,bot]){const pos=p.body.translation();assert.ok(PVE_LAYOUT.pads.some(q=>Math.hypot(q.x-pos.x,q.z-pos.z)<1e-6),'on a pad');
   for(const a of horde.adds())assert.ok(Math.hypot(a.pos.x-pos.x,a.pos.z-pos.z)>4);assert.equal(p.hp,p.stats.hp*PVE.respawnHpFraction);assert.equal(s.pve!.untouchable(p),true);}
  assert.ok(!(s.chaos as unknown as {used:Set<string>}).used.has('h1:exit'),'second-chance can fire again');
  // Living members' once-keys reset too (D37), without reviving them.
  const used=(s.chaos as unknown as {used:Set<string>}).used;used.add('h1:panic');econ.respawnAll('phase');assert.ok(!used.has('h1:panic'));
  assert.equal(events(s,PVE_EVENT.respawn).filter(e=>e.message==='phase').length,0,'nobody was down');
  assert.equal(events(s,PVE_EVENT.respawn).filter(e=>e.message==='wave').length,2);
  assert.equal(econ.partyView('party-bot-1').picks.filter(k=>k.source==='wave'&&k.tier==='common').length,1,'party bot wave pick');
 }finally{s.dispose();}
});

test('solo self-revive: 5 s after the first death, not after the second; pendingRevive() in between',()=>{
 const w=world({},['h1']);const {s,econ}=w;try{
  closeAll(w);assert.equal(s.pve!.solo,true);const h1=P(s,'h1');assert.equal(econ.partyView('h1').selfRevives,1);
  tick(s);kill(s,h1);const died=s.time;assert.equal(econ.pendingRevive(),true);
  while(h1.hp<=0&&s.time<died+10)tick(s);
  assert.ok(Math.abs(s.time-died-PVE.selfReviveDelay)<=1/60+1e-6);assert.equal(econ.pendingRevive(),false);
  assert.ok(events(s,PVE_EVENT.revive).some(e=>e.target==='h1'&&e.message==='self'));assert.equal(econ.partyView('h1').selfRevives,0);
  tick(s,100);kill(s,h1);assert.equal(econ.pendingRevive(),false);tick(s,60*7);assert.ok(h1.hp<=0,'no second self-revive');
 }finally{s.dispose();}
 const two=world({},['h1','h2']);try{assert.equal(two.s.pve!.solo,false);assert.equal(two.econ.partyView('h1').selfRevives,0);}finally{two.s.dispose();}
});

test('holds: heals complete at once, timers shift by the hold time',()=>{
 const w=world({},['h1','h2']);const {s,econ,director}=w;try{
  closeAll(w);econ.grant('h1',1000,'t');const h1=P(s,'h1');
  const f=front(0);place(h1,f.x,f.z);tapF(s,h1);const born=econ.pickupsView()[0].born;
  const med=front(6);place(h1,med.x,med.z);h1.hp=20;tapF(s,h1);tick(s,10);
  director.hold=true;s.pve!.notifyHold(true);assert.ok(Math.abs(h1.hp-70)<1e-6,`heal completed ${h1.hp}`);
  const ready=econ.partyView('h1').healReadyAt;for(let i=0;i<120;i++)s.step();
  director.hold=false;s.pve!.notifyHold(false);
  assert.ok(Math.abs(econ.pickupsView()[0].born-born-2)<1e-6);assert.ok(Math.abs(econ.partyView('h1').healReadyAt-ready-2)<1e-6);
 }finally{s.dispose();}
});

test('rekey moves money, picks, pickups and recruit owners to the new id',()=>{
 const w=world({},['h1','h2']);const {s,econ,rec}=w;try{
  closeAll(w);econ.grant('h2',1000,'t');const h2=P(s,'h2'),h1=P(s,'h1');
  const f=front(0);place(h2,f.x,f.z);tapF(s,h2);rec.hire('h2',s.pve!.standAt(0,0),0);econ.grantPick('h2','rare','dispenser');
  place(h1,0,-8);tick(s);kill(s,h1);
  const money=econ.money('h2'),picks=econ.partyView('h2').picks.length;
  h1.connected=false;
  s.pve!.adoptSeat('h2','h2b');
  assert.equal(econ.money('h2b'),money);assert.equal(econ.partyView('h2b').picks.length,picks);assert.equal(econ.money('h2'),0);
  assert.ok(econ.pickupsView().every(k=>k.owner==='h2b'));assert.ok(rec.views().every(r=>r.owner==='h2b'));
 }finally{s.dispose();}
});

function scripted(seed:number,dt:number){
 const w=world({seed},['h1','h2']);const {s,econ}=w;const out:{money:number[];offers:string[][];heals:number[];reviveAt:number;selfAt:number}={money:[],offers:[],heals:[],reviveAt:-1,selfAt:-1};
 try{
  for(const id of ['h1','h2'])out.offers.push(econ.partyView(id).picks[0].offers!);closeAll(w);
  econ.grant('h1',2000,'t');const h1=P(s,'h1'),h2=P(s,'h2');
  const f=front(1);place(h1,f.x,f.z);
  for(let i=0;i<3;i++){tapF(s,h1);const n=Math.ceil(.8/dt);for(let k=0;k<n;k++)tick(s,1,dt);tapF(s,h1);closeAll(w);}
  for(const k of econ.partyView('h1').picks)if(k.offers)out.offers.push(k.offers);
  const med=front(6);place(h1,med.x,med.z);h1.hp=10;const t0=s.time;press(s,h1,true);tick(s,1,dt);press(s,h1,false);
  const healStart=econ.partyView('h1').healAt!;for(let i=0;s.time<t0+3;i++){const hp=h1.hp;tick(s,1,dt);if(h1.hp>hp)out.heals.push(+(s.time-healStart).toFixed(6));}
  place(h2,0,-8);tick(s,1,dt);kill(s,h2);place(h1,h2.death!.pos.x+1,h2.death!.pos.z);press(s,h1,true);tick(s,1,dt);press(s,h1,false);out.reviveAt=h2.hp>0?0:-1;
  out.money=[econ.money('h1'),econ.money('h2')];
 }finally{s.dispose();}
 return out;
}
test('determinism (same seed → same money and offers) and rate independence of heal pulses and the instant revive (dt 1/30, 1/60, 1/120)',()=>{
 const a=scripted(11,1/60),b=scripted(11,1/60),c=scripted(12,1/60);
 assert.deepEqual(a.money,b.money);assert.deepEqual(a.offers,b.offers);assert.notDeepEqual(a.offers,c.offers);
 for(const dt of [1/30,1/120]){const r=scripted(11,dt);
  assert.equal(r.heals.length,4);for(let i=0;i<4;i++)assert.ok(Math.abs(r.heals[i]-a.heals[i])<=1/30+1e-6,`heal pulse ${i} at ${dt}`);
  assert.equal(a.reviveAt,0,'revived on the press tick');assert.equal(r.reviveAt,a.reviveAt);assert.deepEqual(r.money,a.money);}
 // Self-revive time.
 for(const dt of [1/30,1/60,1/120]){const w=world({},['h1']);try{closeAll(w);const h1=P(w.s,'h1');kill(w.s,h1);const d=w.s.time;while(h1.hp<=0&&w.s.time<d+8)tick(w.s,1,dt);
  assert.ok(Math.abs(w.s.time-d-PVE.selfReviveDelay)<=dt+1e-6,`self revive at ${dt}`);}finally{w.s.dispose();}}
});
