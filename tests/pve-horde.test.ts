// PvE horde (design §3, stream S1): entries, attacks, lifecycle, holds, events, possession, vent travel, rekey, determinism.
// The real Horde runs against the foundation's fake director, economy and recruits (tests/helpers/pve-fakes.ts).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {sirenStageHazards} from '../src/shared/siren-stage-layout.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,PVE_TIMING,decodeAdd,type PveOptions} from '../src/shared/pve/contract.js';
import {HORDE,PVE_SIREN} from '../src/shared/pve/balance.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {Horde} from '../src/shared/pve/horde.js';
import {ventHazard} from '../src/shared/pve/entries.js';
import {entryExposedAt} from '../src/shared/pve/entering.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

function hordeSim(o:Partial<PveOptions>={},ids=['h1']){
 const {factory,made}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:7,...o}});
  for(const id of ids){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
  s.phase='fight';s.fightStart=s.time;
  return {s,made,h:s.pve!.horde as Horde};
 });
}
const place=(p:Player,x:number,z:number,yaw=0)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw,pitch:0};};
const steps=(s:Simulation,n:number)=>{for(let i=0;i<n;i++)s.step();};
const tough=(p:Player,hp=1e7)=>{p.hp=p.stats.hp=hp;};
function untilActive(s:Simulation,h:Horde,n:number,max=600){for(let i=0;i<max&&h.record(n)?.phase!=='active';i++)s.step();assert.equal(h.record(n)?.phase,'active');return h.record(n)!;}
function spawnBoss(s:Simulation,x:number,z:number){s.pve!.spawningBoss=true;const b=s.addPlayer(BOSS_PLAYER_ID,'The Siren','melee','normal','#b7a8ff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']})!;s.pve!.spawningBoss=false;place(b,x,z);return b;}

test('entries: inert until activeAt (vent, door, cell), walk from behind the opening, followers gap later, entries close after the last exit',()=>{
 const {s,h}=hordeSim();try{
  place(s.players.get('h1')!,0,0);tough(s.players.get('h1')!);s.step();
  const t0=s.time;
  const vent=h.spawn('shambler',{entry:4})!,vent2=h.spawn('shambler',{entry:4})!,door=h.spawn('runner',{entry:12})!,cell=h.spawn('brute',{entry:8})!;
  for(const n of [vent,vent2,door,cell])assert.equal(h.record(n)!.phase,'entering');
  const rv=h.record(vent)!,rv2=h.record(vent2)!,rd=h.record(door)!,rc=h.record(cell)!;
  assert.ok(Math.abs(rv.activeAt-(t0+1.95))<1e-9);assert.ok(Math.abs(rd.activeAt-(t0+.6+1.2))<1e-9);assert.ok(Math.abs(rc.activeAt-(t0+.5+1.0))<1e-9);
  assert.ok(Math.abs(rv2.spawnAt-(rv.spawnAt+HORDE.entry.gap.vent))<1e-9,'second add through the open vent starts gap later');
  assert.ok(Math.abs(rv2.activeAt-(rv2.spawnAt+PVE_TIMING.vent.climb-PVE_TIMING.vent.push))<1e-9,'and skips the push');
  const en=PVE_ENTRIES[12],ec=PVE_ENTRIES[8];
  assert.ok(Math.abs(rd.pos.z-en.from.z)<1e-6&&Math.abs(en.from.z)>Math.abs(en.exit.z),'door add starts at from, behind the exit');
  assert.ok(Math.abs(rc.pos.x)>Math.abs(ec.gate!.x),'cell add starts behind the gate');
  let lastDoorZ=rd.pos.z;const activeTimes=new Map<number,number>();
  for(let i=0;i<240;i++){
   s.step();
   for(const n of [vent,vent2,door,cell]){const r=h.record(n)!;const inChaos=s.chaos.minions.has(r.id);
    // 2026-10-04: an entering add registers its (contact-free) body once it is in sight, so weapons can hit it.
    if(r.phase==='entering'){assert.equal(inChaos,s.time>=entryExposedAt(PVE_ENTRIES[r.entry].kind,r.walkAt,r.activeAt),'entering add is in chaos.minions only once in sight');assert.ok(s.time<r.activeAt);}
    else if(!activeTimes.has(n)){activeTimes.set(n,s.time);assert.equal(inChaos,true);}}
   if(h.record(door)!.phase==='entering'){const z=h.record(door)!.pos.z;assert.ok(Math.abs(z)<=Math.abs(lastDoorZ)+1e-9);lastDoorZ=z;}
  }
  for(const [n,at] of activeTimes){const r=h.record(n);assert.ok(at>=(r?.spawnAt??0));}
  assert.ok(Math.abs(activeTimes.get(vent)!-(t0+1.95))<=1/60+1e-9);assert.ok(Math.abs(activeTimes.get(door)!-(t0+1.8))<=1/60+1e-9);assert.ok(Math.abs(activeTimes.get(cell)!-(t0+1.5))<=1/60+1e-9);
  const views=s.snapshot().pve!.entries,v4=views.find(e=>e.e===4)!;
  assert.ok(Math.abs(v4.closeAt-(t0+HORDE.entry.gap.vent+1.6+HORDE.entry.closeAfter))<.011,'vent closes closeAfter after the last exit');assert.equal(v4.mode,'spawn');
 }finally{s.dispose();}
});

test('entries: no spawn within 5 m of a living party member; vents skip hazard windows of the boss song',()=>{
 const {s,h}=hordeSim();try{
  const p=s.players.get('h1')!;tough(p);s.pve!.debug('god:on');   // god: untargetable, so adds stay near their exits
  const ex=PVE_ENTRIES[12].exit;place(p,ex.x,ex.z+2);s.step();
  s.bossSong={track:'black-siren-protocol',startedAt:s.time};
  h.setFlow({rate:6,maxAlive:40,mix:{shambler:1},budget:60});
  const seen=new Set<number>();
  for(let i=0;i<60*12;i++){
   s.step();place(p,ex.x,ex.z+2);
   for(const a of h.adds()){const r=h.record(a.n)!;if(seen.has(a.n)||r.phase!=='entering')continue;seen.add(a.n);
    const en=PVE_ENTRIES[r.entry];assert.ok(Math.hypot(en.exit.x-ex.x,en.exit.z-ex.z-2)>=5,'not near the party');
    if(en.kind==='vent'){const hz=sirenStageHazards(r.spawnAt-s.bossSong!.startedAt)[r.entry];assert.ok(!hz||!hz.active&&hz.warning===0,`vent ${r.entry} hazard at spawn`);assert.equal(ventHazard(r.entry,r.spawnAt,s.bossSong!.startedAt),false);}}
  }
  assert.ok(seen.size>=40,`spawned ${seen.size}`);
 }finally{s.dispose();}
});

test('attacks: damage lands at attackAt+hit, block staggers, roll avoids, boss team untouched, target slot exported',()=>{
 const {s,h}=hordeSim({partyBots:1});try{
  const p=s.players.get('h1')!,bot=s.players.get(PARTY_BOT_PREFIX+'1')!;tough(p);tough(bot);place(bot,20,20);
  const boss=spawnBoss(s,-12,-15);tough(boss);
  const ex=PVE_ENTRIES[12].exit;place(p,ex.x,ex.z+6);s.step();
  const n=h.spawn('shambler',{entry:12})!;const r=untilActive(s,h,n);
  // Windup → damage at attackAt+hit (±1 tick), never earlier.
  let hp=p.hp,landed=-1,attackAt=0;
  for(let i=0;i<600&&landed<0;i++){s.step();place(p,ex.x,ex.z+6);if(r.attackAt>0&&!r.struck)attackAt=r.attackAt;if(p.hp<hp){landed=s.time;}hp=p.hp;}
  assert.ok(landed>0,'the swipe landed');assert.ok(landed>=attackAt+PVE_TIMING.swipeHit-1e-9,'never earlier');assert.ok(landed<=attackAt+PVE_TIMING.swipeHit+1/60+1e-9,'within one tick');
  assert.equal(r.target,'h1');
  const snap=s.snapshot(),tuple=snap.pve!.adds.find(t=>t[0]===n)!;assert.equal(decodeAdd(tuple,snap.pve!.slots).target,'h1','decoded target slot');
  // Block: no damage, the add staggers.
  hp=p.hp;let blocked=false;
  for(let i=0;i<240&&!blocked;i++){p.blockTime=.5;s.step();place(p,ex.x,ex.z+6);if(r.staggerUntil>s.time+.3)blocked=true;}
  assert.ok(blocked,'blocked strike staggers the add');assert.equal(p.hp,hp,'blocked: no damage');p.blockTime=0;
  // Roll i-frames avoid the strike.
  for(let i=0;i<120;i++){p.roll={at:s.time,until:s.time+1,invulnerableAt:s.time-.1,invulnerableUntil:s.time+1,direction:v(),distance:0};s.step();place(p,ex.x,ex.z+6);}
  assert.equal(p.hp,hp,'roll i-frames avoid');p.roll=undefined;
  assert.equal(boss.hp,boss.stats.hp,'adds never damage the boss');
  assert.ok(h.adds().every(a=>a.target!==BOSS_PLAYER_ID));
 }finally{s.dispose();}
});

test('targets: humans, party bots and recruits are targets; the tank taunt shifts the target',()=>{
 const {s,h,made}=hordeSim({partyBots:1});try{
  const p=s.players.get('h1')!,bot=s.players.get(PARTY_BOT_PREFIX+'1')!;tough(p);tough(bot);
  const rec=s.addPlayer('recruit-1','Tank','melee','normal')!;tough(rec);
  const ex=PVE_ENTRIES[12].exit;
  place(p,30,20);place(bot,ex.x,ex.z+6);place(rec,-28,20);s.step();
  const a=h.spawn('shambler',{entry:12})!;untilActive(s,h,a);steps(s,60);
  assert.equal(h.record(a)!.target,bot.id,'party bot is a target');
  place(bot,30,22);place(rec,ex.x,ex.z+6);steps(s,90);assert.equal(h.record(a)!.target,rec.id,'recruit is a target');
  place(rec,-28,20);place(p,ex.x+1,ex.z+5);steps(s,90);assert.equal(h.record(a)!.target,'h1','human is a target');
  // Taunt: the recruit is 3 m farther but taunting.
  place(p,ex.x,ex.z+5);place(rec,ex.x+3,ex.z+7);made.recruits!.taunting.set('t',rec.id);steps(s,90);
  assert.equal(h.record(a)!.target,rec.id,'tank taunt pulls the add');
 }finally{s.dispose();}
});

test('while an add is possessed the hall holds up to maxAlivePossession (192), relay rooms 112; back to 40 once it dies',()=>{
 assert.equal(HORDE.maxAlivePossession,192);
 for(const relay of [false,true]){
  const {s,h}=hordeSim(relay?{relay:true}:{});try{
   const p=s.players.get('h1')!;tough(p);s.pve!.debug('god:on');place(p,0,0);
   h.setFlow({rate:40,maxAlive:300,mix:{shambler:1},budget:-1});
   let peak=0;for(let i=0;i<60*20;i++){s.step();peak=Math.max(peak,h.alive()+h.queued());}
   assert.equal(peak,relay?HORDE.maxAliveRelay:HORDE.maxAlive,'no possession: the usual cap');
   const n=h.possess()!;assert.equal(h.possessed(),n);
   for(let i=0;i<60*20;i++){s.step();peak=Math.max(peak,h.alive()+h.queued());}
   assert.equal(peak,relay?HORDE.maxAlivePossessionRelay:HORDE.maxAlivePossession,'possessed: the possession cap');
   s.chaos.hitMinion(s.chaos.minions.get(h.get(n)!.id)!,1e7,'h1');steps(s,2);assert.equal(h.possessed(),undefined);assert.equal(h.cap(),relay?HORDE.maxAliveRelay:HORDE.maxAlive,'found: the usual cap again');
  }finally{s.dispose();}
 }
});

test('lifecycle: corpse kept 3.1 s, maxAlive 40 (28 relay), killAll burns every add',()=>{
 for(const relay of [false,true]){
  const {s,h,made}=hordeSim(relay?{relay:true}:{});try{
   const p=s.players.get('h1')!;tough(p);s.pve!.debug('god:on');place(p,0,0);
   h.setFlow({rate:40,maxAlive:100,mix:{shambler:1},budget:-1});
   let peak=0;for(let i=0;i<60*14;i++){s.step();peak=Math.max(peak,h.alive()+h.queued());}
   const cap=relay?HORDE.maxAliveRelay:HORDE.maxAlive;assert.equal(peak,cap,`cap ${cap}`);
   if(!relay){
    const one=h.adds().find(a=>a.state==='active')!;s.chaos.hitMinion(s.chaos.minions.get(one.id)!,1e4,'h1');const at=s.time;
    const has=()=>s.snapshot().pve!.adds.some(t=>t[0]===one.n&&t[13]>0);
    assert.ok(has());steps(s,Math.floor(3.0*60));assert.ok(has(),'corpse still there at 3.0 s');steps(s,12);assert.ok(!has(),'corpse gone after 3.1 s');assert.ok(s.time-at>=3.1);
   }
   const before=made.director!.kills.length,active=h.alive();h.killAll('boss-dead');
   assert.equal(h.alive(),0);assert.equal(h.queued(),0);assert.equal(made.director!.kills.length-before,active);
   assert.ok(made.director!.kills.slice(before).every(k=>k.cause==='burn'&&k.killer===null));
   steps(s,30);assert.ok(s.chaos.minions.size===0);
  }finally{s.dispose();}
 }
});

test('holds: queued adds keep their spacing, at most one activation per entry per tick, no pre-hold windup strikes',()=>{
 const {s,h,made}=hordeSim();try{
  const p=s.players.get('h1')!;tough(p);const ex=PVE_ENTRIES[12].exit;place(p,ex.x,ex.z+5);s.step();
  // An add already winding up when the hold starts (it comes out of door 12 while the runners queue at door 13).
  const w=h.spawn('shambler',{entry:12})!;for(let i=0;i<150;i++){s.step();place(p,ex.x,ex.z+5);}
  const ns=[0,1,2].map(()=>h.spawn('runner',{entry:13})!);const sched=ns.map(n=>h.record(n)!.activeAt);
  const act=new Map<number,number>();
  const tick=()=>{s.step();place(p,ex.x,ex.z+5);for(const n of ns)if(!act.has(n)&&h.record(n)?.phase==='active')act.set(n,s.time);};
  while(!act.has(ns[0]))tick();
  const rw=h.record(w)!;
  for(let i=0;i<300&&!(rw.phase==='active'&&rw.attackAt>0&&!rw.struck);i++)tick();
  assert.ok(rw.attackAt>0&&!rw.struck,'winding up');assert.ok(act.size>=1&&act.size<3,'hold starts mid-queue');const heldAt=act.size;
  const hp=p.hp;
  // Hold starts mid-queue for 5 s.
  made.director!.hold=true;s.pve!.notifyHold(true);
  for(let i=0;i<300;i++)tick();
  assert.equal(act.size,heldAt,'nothing activates during the hold');assert.equal(p.hp,hp,'no damage during the hold');
  made.director!.hold=false;s.pve!.notifyHold(false);
  const release=s.time;
  for(let i=0;i<240;i++){tick();if(s.time<=release+1/60+1e-9)assert.ok([...act.values()].filter(t=>Math.abs(t-s.time)<1e-9).length<=1);}
  const t=ns.map(n=>act.get(n)!);
  for(const i of [1,2])assert.ok(Math.abs((t[i]-t[i-1])-(sched[i]-sched[i-1])-(i===heldAt?5:0))<=1/60+1e-9,`spacing ${i}`);
  assert.ok(t[heldAt]-t[heldAt-1]>=5,'the hold delayed the rest');
  assert.ok(rw.attackAt===0||rw.attackAt>release-1e-9,'the pre-hold windup was cancelled');
 }finally{s.dispose();}
});

test('events: 30 adds in a fire field for 5 s keep ≤ 25 PvE/minion events per snapshot and every kill reaches a snapshot',()=>{
 const {s,h,made}=hordeSim();try{
  const p=s.players.get('h1')!;tough(p);s.pve!.debug('god:on');place(p,0,0);
  h.setFlow({rate:30,maxAlive:30,mix:{shambler:1},budget:30});
  for(let i=0;i<60*12&&h.alive()<30;i++)s.step();
  assert.equal(h.alive(),30);
  s.chaos.field(p,'fire',v(0,0,0),60,5.2,15);
  const isPve=(t:string)=>t.startsWith('pve-')||t==='minionHit';let last=s.events.at(-1)?.id??0,maxNew=0;const killIds=new Set<number>();const before=made.director!.kills.length;
  for(let i=0;i<60*6;i++){s.step();if(i%3===2){const snap=s.snapshot();const fresh=snap.events.filter(e=>e.id>last&&isPve(e.type));maxNew=Math.max(maxNew,fresh.length);
   for(const e of snap.events)if(e.type==='pve-kill')killIds.add(e.id);last=snap.events.at(-1)?.id??last;}}
  const kills=made.director!.kills.length-before;
  assert.ok(kills>=25,`${kills} adds burned`);assert.ok(maxNew<=25,`max new PvE events in one snapshot: ${maxNew}`);
  assert.equal(killIds.size,kills,'every death event reached a snapshot');
 }finally{s.dispose();}
});

test('possession: deterministic choice, ×3 HP, never attacks, keeps 8–14 m, forced chase at 60 s',()=>{
 const run=()=>{const {s,h}=hordeSim();
  const p=s.players.get('h1')!;tough(p);place(p,0,0);
  for(const e of [12,13,14,15,8,9])h.spawn('shambler',{entry:e});
  steps(s,150);const n=h.possess()!;return {s,h,p,n};};
 const a=run(),b=run();try{
  assert.equal(a.n,b.n,'deterministic');
  const r=a.h.record(a.n)!;assert.equal(r.maxHp,60*PVE_SIREN.possession.hpScale);assert.equal(r.hp,r.maxHp);assert.equal(a.h.possessed(),a.n);
  for(const add of a.h.adds())if(add.n!==a.n)a.s.chaos.hitMinion(a.s.chaos.minions.get(add.id)!,1e4,'h1');
  const s=a.s;let attacked=false,minD=Infinity,maxD=0,inBand=false;
  for(let i=0;i<60*58;i++){s.step();place(a.p,0,0);if(r.attackAt>0)attacked=true;
   const q=r.minion!.body.translation(),d=Math.hypot(q.x,q.z);if(d<=14.05)inBand=true;   // it walks in from its starting spot first
   if(inBand){minD=Math.min(minD,d);maxD=Math.max(maxD,d);}}
  assert.ok(inBand,'reached the keep-away band');
  assert.equal(attacked,false,'never attacks');
  assert.ok(minD>=7.4&&maxD<=14.8,`keep-away band ${minD.toFixed(2)}..${maxD.toFixed(2)}`);
  for(let i=0;i<60*12;i++){s.step();place(a.p,0,0);if(r.attackAt>0)attacked=true;}
  const q=r.minion!.body.translation();assert.ok(Math.hypot(q.x,q.z)<3,'forced chase after 60 s');assert.equal(attacked,false);
  // No active add: possess() queues a possessed shambler at the farthest door.
  const {s:t,h:th}=hordeSim();try{place(t.players.get('h1')!,-18,-20);t.step();const m=th.possess()!;const rm=th.record(m)!;
   assert.equal(rm.phase,'entering');assert.equal(rm.possessed,true);assert.equal(PVE_ENTRIES[rm.entry].id,'door-se');assert.equal(th.possessed(),m);
   assert.ok(t.snapshot().pve!.adds.some(x=>x[0]===m),'reported in the view');}finally{t.dispose();}
 }finally{a.s.dispose();b.s.dispose();}
});

test('vent travel: a far target makes adds dive, at most 2 at once, never at a hazard-active vent',()=>{
 const {s,h}=hordeSim();try{
  const p=s.players.get('h1')!;tough(p);place(p,0,-24);s.step();s.bossSong={track:'black-siren-protocol',startedAt:s.time};
  const song=s.bossSong.startedAt,dives=new Set<string>(),emerge=new Set<string>();let peak=0;
  for(let k=0;k<6;k++)h.spawn('shambler',{entry:5});for(let k=0;k<4;k++)h.spawn('shambler',{entry:1});for(let k=0;k<3;k++)h.spawn('shambler',{entry:7});
  for(let i=0;i<60*40;i++){s.step();place(p,0,-24);
   let travel=0;for(const a of h.adds()){const r=h.record(a.n)!;if(r.travel)travel++;
    if(r.phase==='vent'&&!dives.has(`${a.n}@${r.diveAt}`)){dives.add(`${a.n}@${r.diveAt}`);assert.equal(ventHazard(r.entry,r.diveAt,song,PVE_TIMING.vent.dive),false,'dive vent not hazardous');}
    if(r.travel&&r.phase==='entering'&&!emerge.has(`${a.n}@${r.spawnAt}`)){emerge.add(`${a.n}@${r.spawnAt}`);assert.equal(ventHazard(r.entry,r.spawnAt,song),false,'emerge vent not hazardous');}}
   peak=Math.max(peak,travel);if(i%60===0)for(const a of h.adds())s.chaos.minions.get(a.id)&&0;
  }
  assert.ok(dives.size>=1,`dives ${dives.size}`);assert.ok(peak<=HORDE.ventTravel.maxConcurrent,`peak travellers ${peak}`);assert.ok(emerge.size>=1);
 }finally{s.dispose();}
});

test('rekey: targets and assists follow the adopted seat',()=>{
 const {s,h,made}=hordeSim({},['h1','h2']);try{
  const a=s.players.get('h1')!,b=s.players.get('h2')!;tough(a);tough(b);const ex=PVE_ENTRIES[12].exit;place(a,ex.x,ex.z+5);place(b,10,10);s.step();
  const n=h.spawn('shambler',{entry:12})!;untilActive(s,h,n);steps(s,60);assert.equal(h.record(n)!.target,'h1');
  s.chaos.hitMinion(s.chaos.minions.get(h.record(n)!.id)!,25,'h1');
  a.connected=false;assert.equal(s.pve!.adoptSeat('h1','h1b'),true);
  assert.equal(h.record(n)!.target,'h1b');assert.ok(h.record(n)!.dealt.has('h1b'));
  s.chaos.hitMinion(s.chaos.minions.get(h.record(n)!.id)!,1e4,'h2');
  const k=made.director!.kills.at(-1)!;assert.equal(k.killer,'h2');assert.deepEqual(k.assists,['h1b']);
 }finally{s.dispose();}
});

test('determinism: same seed and inputs give identical pve.adds for 1,800 ticks; another seed differs',()=>{
 const trace=(seed:number)=>{const {s,h}=hordeSim({seed},['h1','h2']);try{
  const a=s.players.get('h1')!,b=s.players.get('h2')!;tough(a);tough(b);place(a,0,-10);place(b,4,12);
  h.setFlow({rate:2,maxAlive:24,mix:{shambler:.5,runner:.3,brute:.2},budget:-1});const out:string[]=[];
  for(let i=0;i<1800;i++){a.input={...a.input,x:Math.sin(i/90)>0?1:-1,z:0,seq:i};s.step();out.push(JSON.stringify(s.snapshot().pve!.adds));}
  return out;}finally{s.dispose();}};
 const x=trace(11),y=trace(11),z=trace(12);
 for(let i=0;i<x.length;i++)assert.equal(x[i],y[i],`tick ${i}`);
 assert.notDeepEqual(x,z,'a different seed changes the spawns');
 assert.ok(JSON.parse(x.at(-1)!).length>=6,'adds were spawned');
});
