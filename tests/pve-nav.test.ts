// PvE horde navigation and entry scheduling (design §3.2–3.3, stream S1): grids, flow fields, entries, rate independence.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {arenaBlocks} from '../src/shared/arena.js';
import {sirenStageHazards} from '../src/shared/siren-stage-layout.js';
import {machineBlocks,PVE_ENTRIES,PVE_LAYOUT} from '../src/shared/pve/layout.js';
import {PVE_TIMING} from '../src/shared/pve/contract.js';
import {HORDE} from '../src/shared/pve/balance.js';
import {PveRng} from '../src/shared/pve/rng.js';
import {buildNavGrid,buildFlowField,cellOf,cellCentre,descendCell,fieldDistance,lineClear,blocksWalking,NAV_INF,HordeNav} from '../src/shared/pve/nav.js';
import {EntryScheduler,SpawnBudget,chooseEntry,entryTimeline,ventHazard} from '../src/shared/pve/entries.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const BLOCKS=[...arenaBlocks('siren'),...machineBlocks()];

test('nav grids are deterministic; every entry exit reaches every player pad and descent never enters a blocked cell',()=>{
 const a=buildNavGrid(BLOCKS,{radius:HORDE.nav.partyRadius}),b=buildNavGrid(BLOCKS,{radius:HORDE.nav.partyRadius});
 assert.deepEqual(a.blocked,b.blocked);assert.equal(a.w*a.d,3584);
 const boss1=buildNavGrid(BLOCKS,{radius:HORDE.nav.bossRadius}),boss2=buildNavGrid(BLOCKS,{radius:HORDE.nav.bossRadius});assert.deepEqual(boss1.blocked,boss2.blocked);
 const f1=buildFlowField(a,cellOf(a,0,0)),f2=buildFlowField(a,cellOf(a,0,0));assert.deepEqual(f1,f2);
 for(const pad of PVE_LAYOUT.pads){
  const goal=cellOf(a,pad.x,pad.z),dist=buildFlowField(a,goal);
  for(const en of PVE_ENTRIES){
   let c=cellOf(a,en.exit.x,en.exit.z);assert.equal(a.blocked[c],0,`${en.id} exit cell free`);
   assert.ok(Number.isFinite(fieldDistance(a,dist,en.exit.x,en.exit.z)),`${en.id} reaches pad ${pad.x},${pad.z}`);
   for(let i=0;i<500&&c!==goal;i++){const n=descendCell(a,dist,c);assert.ok(n>=0,'descent continues');assert.equal(a.blocked[n],0,'never a blocked cell');assert.ok(dist[n]<dist[c]);c=n;}
   assert.equal(c,goal,`${en.id} descends to the pad`);
  }
 }
 // Descent from every free cell toward the hall centre never enters a blocked cell.
 const centre=buildFlowField(a,cellOf(a,0,0));
 for(let c=0;c<a.blocked.length;c++){if(a.blocked[c]||centre[c]>=NAV_INF)continue;const n=descendCell(a,centre,c);if(n>=0)assert.equal(a.blocked[n],0);}
});

test('the boss grid never routes through a cell narrower than 1.26 m',()=>{
 const g=buildNavGrid(BLOCKS,{radius:HORDE.nav.bossRadius}),walls=BLOCKS.filter(blocksWalking);
 // Every free boss cell's whole square keeps at least the boss radius (0.63 m, i.e. a 1.26 m wide capsule) from every block.
 for(let c=0;c<g.blocked.length;c++){if(g.blocked[c])continue;const p=cellCentre(g,c);
  for(const b of walls){const yaw=b.rotation?2*Math.atan2(b.rotation.y,b.rotation.w):0,co=Math.cos(yaw),si=Math.sin(yaw),dx=p.x-b.pos.x,dz=p.z-b.pos.z;
   const lx=Math.abs(co*dx-si*dz)-b.size.x/2,lz=Math.abs(si*dx+co*dz)-b.size.z/2,gap=Math.hypot(Math.max(0,lx),Math.max(0,lz));
   // The cell's farthest corner is ≤ 0.71 m from its centre; the rule inflates each block by radius + half a cell per axis.
   assert.ok(Math.max(lx,lz)>=HORDE.nav.bossRadius+.5-1e-6||gap>=HORDE.nav.bossRadius,`cell ${c} too close to ${b.id}`);}
 }
 // Descent on the boss grid only visits free boss cells (so never a sub-1.26 m gap).
 const goal=cellOf(g,0,-21),dist=buildFlowField(g,goal);
 for(const en of PVE_ENTRIES){let c=cellOf(g,en.exit.x,en.exit.z);if(g.blocked[c]){continue;}for(let i=0;i<500&&c!==goal;i++){const n=descendCell(g,dist,c);if(n<0)break;assert.equal(g.blocked[n],0);c=n;}}
});

test('grid line of sight and the HordeNav field budget',()=>{
 const g=buildNavGrid(BLOCKS,{radius:HORDE.nav.partyRadius});
 assert.equal(lineClear(g,{x:0,z:-10},{x:0,z:-12}),true);
 assert.equal(lineClear(g,{x:-30,z:0},{x:30,z:0}),false,'the hall middle has columns/pillars');
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 withPveParts(factory,()=>{const s=new Simulation({pve:{boss:'siren',seed:3}});try{
  for(const id of ['h1','h2','h3'])s.addPlayer(id);
  const nav=(s.pve!.horde as Horde).nav;assert.ok(nav.ready());
  const before=nav.builds;nav.direction(v(10,1,10),'h1');nav.direction(v(10,1,10),'h2');nav.direction(v(10,1,10),'h3');
  assert.equal(nav.builds-before,HordeNav.buildsPerTick,'at most 2 field builds per tick');
  assert.equal(nav.direction(v(10,1,10),'h3'),null,'over budget: no field yet');
  assert.ok(Number.isFinite(nav.distance(v(10,1,10),'h3')),'distance falls back to straight line');
 }finally{s.dispose();}});
});

test('removing a stone prop rebuilds the grids within 0.5 s',()=>{
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 withPveParts(factory,()=>{const s=new Simulation({pve:{boss:'siren',seed:3}});try{
  s.addPlayer('h1');s.phase='fight';s.fightStart=s.time;
  const nav=(s.pve!.horde as Horde).nav,stone=arenaBlocks('siren').find(b=>b.material==='stone'&&!b.dynamic)!;
  const g0=nav.grid('party')!,c=cellOf(g0,stone.pos.x,stone.pos.z);assert.equal(g0.blocked[c],1);
  s.step();s.removeProp(stone.id);const at=s.time;
  while(nav.grid('party')!.blocked[c]&&s.time<at+1)s.step();
  assert.equal(nav.grid('party')!.blocked[c],0);assert.equal(nav.grid('boss')!.blocked[cellOf(g0,stone.pos.x,stone.pos.z)],0);
  assert.ok(s.time-at<=.5+1/60+1e-9,`rebuilt after ${s.time-at}s`);
 }finally{s.dispose();}});
});

test('entry timelines: vent 1.95 s, door .6+1.2, cell .5+1.0; followers start gap later and skip the opening; close after the last exit',()=>{
 assert.equal(entryTimeline('vent',true).active,1.95);assert.equal(entryTimeline('door',true).active,PVE_TIMING.door.open+PVE_TIMING.door.walk);
 assert.equal(entryTimeline('cell',true).active,PVE_TIMING.cell.open+PVE_TIMING.cell.walk);
 const sch=new EntryScheduler(),vent=0,door=12;
 const a=sch.schedule(vent,1,10),b=sch.schedule(vent,2,10.1);
 assert.equal(a.spawnAt,10);assert.equal(a.activeAt,10+1.95);assert.equal(a.first,true);
 assert.equal(b.first,false);assert.ok(Math.abs(b.spawnAt-(10+HORDE.entry.gap.vent))<1e-9);assert.ok(Math.abs(b.activeAt-(b.spawnAt+1.95-PVE_TIMING.vent.push))<1e-9,'skips the push');
 assert.ok(Math.abs(sch.slots[vent].closeAt-(b.activeAt+HORDE.entry.closeAfter))<1e-9);
 const d=sch.schedule(door,3,20);assert.equal(d.walkAt,20+.6);assert.ok(Math.abs(d.activeAt-21.8)<1e-9);
 const d2=sch.schedule(door,4,20);assert.ok(Math.abs(d2.walkAt-20.7)<1e-9);assert.ok(Math.abs(d2.activeAt-21.9)<1e-9);
 assert.equal(sch.views(17).length,2);assert.equal(sch.views(22).length,1);assert.equal(sch.views(40).length,0);
});

test('entry choice: no entry within 5 m of a party member, queues ≤ 3, vents skip hazard windows',()=>{
 const sch=new EntryScheduler(),rng=new PveRng(5);
 const party=[{x:PVE_ENTRIES[12].exit.x,z:PVE_ENTRIES[12].exit.z+1}];
 for(let i=0;i<200;i++){const e=chooseEntry(sch,{time:0,party,wave:true,possession:false},rng);const ex=PVE_ENTRIES[e].exit;assert.ok(Math.hypot(ex.x-party[0].x,ex.z-party[0].z)>=5);}
 for(let n=0;n<3;n++)sch.schedule(5,n,0);
 for(let i=0;i<100;i++)assert.notEqual(chooseEntry(sch,{time:0,party:[],wave:false,possession:false},rng),5);
 // Find a song time with a live vent hazard; that vent is never chosen then.
 let found=-1,hazardT=0;for(let t=0;t<200&&found<0;t+=.05){const hz=sirenStageHazards(t);const i=hz.findIndex(h=>h.active||h.warning>0);if(i>=0){found=i;hazardT=t;}}
 assert.ok(found>=0,'the Black Siren song has vent hazards');assert.equal(ventHazard(found,hazardT,0),true);
 const fresh=new EntryScheduler();
 for(let i=0;i<300;i++){const e=chooseEntry(fresh,{time:hazardT,party:[],songStart:0,wave:false,possession:true,kinds:['vent']},rng);if(e>=0)assert.equal(ventHazard(e,hazardT,0),false);}
});

/** Pure scheduler run: flow budget + entry choice + activation, at a given dt. */
function scheduleRun(dt:number,seconds:number){
 const sch=new EntryScheduler(),budget=new SpawnBudget(),rng=new PveRng(77),party=[{x:0,z:0}],spawns:number[]=[],actives:number[]=[];
 const entering:{n:number;e:number;at:number}[]=[];let n=0,pending=0;
 for(let t=0;t<seconds-1e-9;t+=dt){
  const time=+t.toFixed(9);
  const release=budget.step(dt,1.5,Math.max(0,20-entering.length-pending),40-n-pending);pending+=release;
  while(pending>0){const e=chooseEntry(sch,{time,party,wave:true,possession:false},rng);if(e<0)break;pending--;n++;const s=sch.schedule(e,n,time);spawns.push(s.spawnAt);entering.push({n,e,at:s.activeAt});}
  for(const x of [...entering])if(time>=x.at-1e-9){actives.push(time);sch.leave(x.e,x.n);entering.splice(entering.indexOf(x),1);}
 }
 // Activations in the last step before the cut-off may land on either side of it: compare those before 19.5 s.
 return {spawns,actives:actives.filter(t=>t<seconds-.5),total:n};
}
test('rate independence: spawn times and totals match at dt 1/30, 1/60 and 1/120 over 20 s',()=>{
 const runs=[1/30,1/60,1/120].map(dt=>({dt,...scheduleRun(dt,20)}));
 for(const r of runs){assert.equal(r.total,runs[0].total);assert.equal(r.spawns.length,runs[0].spawns.length);assert.equal(r.actives.length,runs[0].actives.length);}
 for(const r of runs)for(let i=0;i<r.spawns.length;i++){
  assert.ok(Math.abs(r.spawns[i]-runs[0].spawns[i])<=1/30+1e-9,`spawn ${i} at ${r.dt}`);
 }
 for(const r of runs)for(let i=0;i<r.actives.length;i++)assert.ok(Math.abs(r.actives[i]-runs[0].actives[i])<=1/30+1e-9,`activation ${i} at ${r.dt}`);
 assert.ok(runs[0].total>=25,'about 1.5/s for 20 s');
});
