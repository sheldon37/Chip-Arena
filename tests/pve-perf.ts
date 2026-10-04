// PvE horde performance (design §3.9, stream S1). Not part of npm test: `npm run test:pve-perf`.
// HORDE.maxAlivePossession (48) adds, one possessed ("Find me...", the largest field), chasing 4 invulnerable party members
// for 600 ticks: average sim.step ≤ 4 ms and PvE share ≤ 2.5 ms; snapshot p95 raw JSON ≤ 64 KB with a full event ring,
// 48 adds, 16 corpses and 4 open picks (fake economy).
// Scenario `abilities` (ability overhaul, design §16.2 I): 40 adds (no possession) against a darkness, a water and a lightning
// member re-casting Black Hole, Geyser Wall and Ball Lightning every second (charges and mana restored: far above the real
// 11-14 s cooldowns) and a machine gun with Rally Pong holding fire. Same step and snapshot budgets, with 20 Hz snapshots
// (every 3 ticks, as the server ships them) and no `ability`/`ability-cast`/`explosion` event evicted from the event ring
// before the next snapshot.
import assert from 'node:assert/strict';
import {performance} from 'node:perf_hooks';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v,idleInput,CONFIG} from '../src/shared/config.js';
import {statsFor} from '../src/shared/content.js';
import {cameraPosition} from '../src/shared/view.js';
import {PVE_LAYOUT} from '../src/shared/pve/layout.js';
import {HORDE} from '../src/shared/pve/balance.js';
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
 const N=HORDE.maxAlivePossession;
 h.setFlow({rate:40,maxAlive:N,mix:{shambler:.55,runner:.3,brute:.15},budget:-1});
 for(let i=0;i<60*20&&!h.adds().some(a=>a.state==='active');i++){s.step();hold();}
 assert.notEqual(h.possess(),null,'an add is possessed');
 for(let i=0;i<60*20&&h.alive()<N;i++){s.step();hold();}
 assert.equal(h.alive(),N,`${N} adds alive`);
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
 for(const a of h.adds().filter(a=>a.state==='active'&&a.n!==h.possessed()).slice(0,16))s.chaos.hitMinion(s.chaos.minions.get(a.id)!,1e6,'h1');
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
 assert.ok(adds>=N+16,`snapshot carried ${adds} add tuples`);
 assert.ok(p95<=64*1024,`snapshot p95 ${p95} B > 64 KB`);
 console.log('pve-perf: ok');
}finally{s.dispose();}

// ---- scenario: abilities ----
{
const {factory}=fakeParts({horde:h=>new Horde(h)});
const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:2026}}));
try{
 const kit:Record<string,string>={h1:'darkness',h2:'water',h3:'lightning',h4:'machinegun'},ids=Object.keys(kit);
 for(const id of ids){s.addPlayer(id,id,kit[id]==='machinegun'?'ranged':'magic');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;
 for(const id of ids){const p=s.players.get(id)!;p.spec=kit[id] as never;p.chips=id==='h4'?{pong:1}:{};p.stats=statsFor(p.chips,p.spec);}
 const h=s.pve!.horde as Horde,pads=[0,1,4,5].map(i=>PVE_LAYOUT.pads[i]);let seq=0;
 const hold=()=>ids.forEach((id,i)=>{const p=s.players.get(id)!;p.hp=p.stats.hp=1e9;p.mana=p.stats.maxMana;p.body.setTranslation(v(pads[i].x,.9,pads[i].z),true);p.body.setLinvel(v(),true);});
 /** Camera aim at the add nearest to the centroid of the adds (feet for point casts, chest for the gun and the orb). */
 const aim=(id:string)=>{const p=s.players.get(id)!,adds=h.adds().filter(a=>a.state==='active').map(a=>s.chaos.minions.get(a.id)).filter(Boolean);if(!adds.length)return {yaw:p.input.yaw,pitch:p.input.pitch};
  const c=adds.reduce((o,m)=>{const t=m!.body.translation();return v(o.x+t.x/adds.length,0,o.z+t.z/adds.length);},v()),m=adds.sort((a,b)=>{const x=a!.body.translation(),y=b!.body.translation();return Math.hypot(x.x-c.x,x.z-c.z)-Math.hypot(y.x-c.x,y.z-c.z);})[0]!.body.translation();
  const to=v(m.x,id==='h1'||id==='h2'?.02:.8,m.z),pos=p.body.translation();let yaw=Math.atan2(to.x-pos.x,to.z-pos.z),pitch=0;for(let k=0;k<4;k++){const cam=cameraPosition(pos,yaw,pitch,'first');pitch=Math.atan2(to.y-cam.y,Math.hypot(to.x-cam.x,to.z-cam.z));}return {yaw,pitch};};
 let tick=0;const inputs=()=>{for(const id of ids){const p=s.players.get(id)!,a=aim(id),gun=id==='h4';s.setInput(id,{...idleInput(),seq:++seq,...a,camera:'first',attack:gun,reload:gun&&p.ammo===0});p.inputAt=s.time;
  if(!gun&&tick%60===ids.indexOf(id)*15){p.eCharges=Math.max(1,p.eCharges);p.input={...p.input,...a,camera:'first'};s.abilities.press(p,'e');}}};
 hold();
 h.setFlow({rate:40,maxAlive:40,mix:{shambler:.55,runner:.3,brute:.15},budget:-1});
 for(let i=0;i<60*20&&h.alive()<40;i++){s.step();hold();}
 assert.equal(h.alive(),40,'40 adds alive');
 const KEEP=new Set(['ability','ability-cast','explosion']),pending:{id:number;type:string}[]=[],emitted:Record<string,number>={},dropped:Record<string,number>={};
 const emit=s.emit.bind(s);s.emit=((type:string,pos:Parameters<typeof emit>[1],extra?:Parameters<typeof emit>[2])=>{emit(type,pos,extra);if(KEEP.has(type)){pending.push({id:s.events.at(-1)!.id,type});emitted[type]=(emitted[type]||0)+1;}}) as typeof s.emit;
 const ticks:number[]=[],sizes:number[]=[];let casts=0;const cast=s.abilities.press.bind(s.abilities);s.abilities.press=((...a:Parameters<typeof cast>)=>{const r=cast(...a);if(r==='cast')casts++;return r;}) as typeof cast;
 for(tick=0;tick<600;tick++){hold();inputs();const t=performance.now();s.step();ticks.push(performance.now()-t);
  if(tick%3===2){const first=s.events[0]?.id??Infinity;for(const e of pending.splice(0))if(e.id<first)dropped[e.type]=(dropped[e.type]||0)+1;sizes.push(JSON.stringify(s.snapshot()).length);}}
 ticks.sort((a,b)=>a-b);sizes.sort((a,b)=>a-b);
 const avg=ticks.reduce((a,b)=>a+b,0)/ticks.length,p99=ticks[Math.floor(ticks.length*.99)],p95=sizes[Math.floor(sizes.length*.95)];
 const balls=[...s.projectiles.values()].filter(x=>x.pong).length,fields=[...s.chaos.fields.values()].length;
 const report={scenario:'abilities',avgStepMs:+avg.toFixed(3),p99StepMs:+p99.toFixed(3),snapshotP95Bytes:p95,casts,emitted,abilityEventsDropped:dropped,hordeEventsDropped:h.stats.eventsDropped,pongBallsAtEnd:balls,fieldsAtEnd:fields,addsAlive:h.alive()};
 console.log(JSON.stringify(report));
 assert.ok(casts>=25,`only ${casts} E casts`);assert.ok((emitted.explosion||0)>0,'ability blasts happened');
 assert.ok(avg<=4,`abilities: average sim.step ${avg.toFixed(2)} ms > 4 ms`);
 assert.ok(p95<=64*1024,`abilities: snapshot p95 ${p95} B > 64 KB`);
 assert.deepEqual(dropped,{},'no ability/explosion event evicted before its snapshot');
 console.log('pve-perf abilities: ok');
}finally{s.dispose();}
}
