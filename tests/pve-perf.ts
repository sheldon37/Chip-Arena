// PvE horde performance (design §3.9, stream S1). Not part of npm test: `npm run test:pve-perf`.
// 40 adds chasing 4 invulnerable party members for 600 ticks: average sim.step ≤ 4 ms and PvE share ≤ 2.5 ms;
// snapshot p95 raw JSON ≤ 64 KB with a full event ring, 40 adds, 16 corpses and 4 open picks (fake economy).
import assert from 'node:assert/strict';
import {performance} from 'node:perf_hooks';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v,CONFIG} from '../src/shared/config.js';
import {PVE_LAYOUT} from '../src/shared/pve/layout.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const {factory,made}=fakeParts({horde:h=>new Horde(h)});
const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:2026}}));
try{
 const ids=['h1','h2','h3','h4'];
 for(const id of ids){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;
 const h=s.pve!.horde as Horde,pads=[0,1,4,5].map(i=>PVE_LAYOUT.pads[i]);
 const hold=()=>ids.forEach((id,i)=>{const p=s.players.get(id)!;p.hp=p.stats.hp=1e9;p.body.setTranslation(v(pads[i].x,.9,pads[i].z),true);p.body.setLinvel(v(),true);});
 hold();
 h.setFlow({rate:40,maxAlive:40,mix:{shambler:.55,runner:.3,brute:.15},budget:-1});
 for(let i=0;i<60*20&&h.alive()<40;i++){s.step();hold();}
 assert.equal(h.alive(),40,'40 adds alive');
 // Timed run.
 let hordeMs=0,afterMs=0;const step=h.step.bind(h),after=s.pve!.stepAfterPhysics.bind(s.pve!);
 h.step=(dt:number)=>{const t=performance.now();step(dt);hordeMs+=performance.now()-t;};
 s.pve!.stepAfterPhysics=(dt:number)=>{const t=performance.now();after(dt);afterMs+=performance.now()-t;};
 const ticks:number[]=[];
 for(let i=0;i<600;i++){hold();const t=performance.now();s.step();ticks.push(performance.now()-t);}
 h.step=step;s.pve!.stepAfterPhysics=after;
 ticks.sort((a,b)=>a-b);
 const avg=ticks.reduce((a,b)=>a+b,0)/ticks.length,p99=ticks[Math.floor(ticks.length*.99)],pveAvg=(hordeMs+afterMs)/600;
 // Snapshot size: 16 corpses (+16 replacements entering), 4 open picks, a full event ring.
 for(const id of ids)made.economy!.openPick(id);
 for(const a of h.adds().filter(a=>a.state==='active').slice(0,16))s.chaos.hitMinion(s.chaos.minions.get(a.id)!,1e6,'h1');
 for(let k=0;k<16;k++)h.spawn('shambler');
 const sizes:number[]=[];let adds=0;
 for(let i=0;i<60;i++){hold();s.step();while(s.events.length<CONFIG.maxEvents)s.emit('minionHit',v(1.2345,.9876,5.4321),{source:'h1',target:'add-1',amount:12.345,color:0x0b0a0d});
  const snap=s.snapshot();adds=Math.max(adds,snap.pve!.adds.length);sizes.push(JSON.stringify(snap).length);}
 sizes.sort((a,b)=>a-b);const p95=sizes[Math.floor(sizes.length*.95)];
 const tuple=JSON.stringify(s.snapshot().pve!.adds[0]).length;
 const report={avgStepMs:+avg.toFixed(3),p99StepMs:+p99.toFixed(3),pveShareMs:+pveAvg.toFixed(3),hordeMs:+(hordeMs/600).toFixed(3),
  fieldBuilds:h.nav.builds,snapshotP95Bytes:p95,maxAddTuples:adds,addTupleBytes:tuple,eventsDropped:h.stats.eventsDropped};
 console.log(JSON.stringify(report));
 assert.ok(avg<=4,`average sim.step ${avg.toFixed(2)} ms > 4 ms`);
 assert.ok(pveAvg<=2.5,`PvE share ${pveAvg.toFixed(2)} ms > 2.5 ms`);
 assert.ok(adds>=56,`snapshot carried ${adds} add tuples`);
 assert.ok(p95<=64*1024,`snapshot p95 ${p95} B > 64 KB`);
 console.log('pve-perf: ok');
}finally{s.dispose();}
