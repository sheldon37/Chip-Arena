// Owner request 2026-10-04: Siren adds take hits while they come in (climbing out of a vent, walking out of a door or
// cell, re-emerging from vent travel) as soon as they are in sight; hidden adds (pending, behind a closed door or grate,
// underground) stay untouchable. Kills on the way in release the entry, credit once and let the wave finish.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,idleInput,type SpecId,type Vec} from '../src/shared/config.js';
import {statsFor,WEAPONS,magazineCapacity} from '../src/shared/content.js';
import {PVE_EVENT,PVE_TIMING,decodeAdd} from '../src/shared/pve/contract.js';
import {ADD_KINDS,ECONOMY} from '../src/shared/pve/balance.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {Horde} from '../src/shared/pve/horde.js';
import {Director} from '../src/shared/pve/director.js';
import {Economy} from '../src/shared/pve/economy.js';
import {entryExposedAt,ventClimbOffset,VENT_EXPOSED_AGE} from '../src/shared/pve/entering.js';
import type {PveAdd} from '../src/shared/pve/add-ai.js';
import type {Minion} from '../src/shared/chaos.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

function hordeSim(ids=['h1']){
 const {factory,made}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:7}});
  for(const id of ids){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
  s.phase='fight';s.fightStart=s.time;
  return {s,made,h:s.pve!.horde as Horde};
 });
}
const place=(p:Player,at:{x:number;z:number},face:{x:number;y?:number;z:number})=>{
 p.body.setTranslation(v(at.x,.9,at.z),true);p.body.setLinvel(v(),true);p.ext=v();
 const dx=face.x-at.x,dz=face.z-at.z,dy=(face.y??.9)-(.9+.5);p.input={...p.input,yaw:Math.atan2(dx,dz),pitch:Math.atan2(dy,Math.hypot(dx,dz))};
};
const arm=(p:Player,spec:SpecId)=>{p.spec=spec;p.class=WEAPONS[spec].class;p.chips={};p.stats=statsFor({},spec);p.stats.hp=p.hp=1e6;p.stamina=p.stats.maxStamina;p.mana=p.stats.maxMana;p.ammo=magazineCapacity(spec,p.stats);};
const sched=(h:Horde)=>(h as unknown as {sched:{queueLength(e:number):number}}).sched;
const body=(s:Simulation,a:PveAdd)=>{const m=s.chaos.minions.get(a.id);return m&&m.body.isEnabled()?m.body.translation():undefined;};
/** A minion handle for an add that has none registered (pending, hidden): hits must still be refused. */
const ghost=(a:PveAdd)=>({id:a.id,kind:'siren-add',owner:'horde'}) as unknown as Minion;
/** Hidden: no registered body, refused by hit() and status(), and an AoE on its spot deals nothing. */
function assertHidden(s:Simulation,h:Horde,a:PveAdd,at:Vec,why:string){
 const hp=a.hp,m=s.chaos.minions.get(a.id);
 assert.ok(!m||!m.body.isEnabled(),`${why}: no enabled body`);
 assert.equal(h.hit(m??ghost(a),500,'h1',600,v(0,0,1),{kind:'blast'}),0,`${why}: hit() refused`);
 assert.equal(h.status(m??ghost(a),'h1',{burn:5}),false,`${why}: status refused`);
 const r=s.explode(s.players.get('h1')!,v(at.x,at.y+.5,at.z),3,500,0,'blast',false);
 assert.ok(!(r.adds??[]).includes(a.id),`${why}: AoE refused`);assert.equal(a.hp,hp,`${why}: HP unchanged`);
}
/** The hall-side point `d` metres past the entry's exit along its walk/climb direction. */
const ahead=(e:number,d:number)=>{const en=PVE_ENTRIES[e];return {x:en.exit.x+Math.sin(en.yaw)*d,z:en.exit.z+Math.cos(en.yaw)*d};};

for(const [e,label] of [[12,'door'],[8,'cell'],[7,'vent']] as const)
 test(`${label} entry: hidden until in sight, then shot, slashed and blasted on the way in, and killed cleanly`,()=>{
  const {s,h,made}=hordeSim(['h1','h2']);try{
   const gun=s.players.get('h1')!,blade=s.players.get('h2')!;arm(gun,'pistol');arm(blade,'sword');
   place(gun,{x:0,z:0},{x:0,z:1});place(blade,{x:2,z:0},{x:0,z:1});s.step();
   const n=h.spawn('shambler',{entry:e})!,a=h.record(n)!,en=PVE_ENTRIES[e];assert.equal(a.phase,'entering');
   a.hp=a.maxHp=1e4;const exposed=entryExposedAt(en.kind,a.walkAt,a.activeAt);
   assert.ok(exposed>s.time&&exposed<a.activeAt,'first add through a closed entry is hidden at first');
   if(en.kind==='vent')assert.ok(Math.abs(exposed-(a.spawnAt+VENT_EXPOSED_AGE))<1e-9,'vent: in sight once the grate is open');
   else assert.equal(exposed,a.walkAt,'door/cell: in sight once the opening is open and the walk starts');
   // Hidden: behind the closed leaves / teeth, or under the grate.
   while(!body(s,a)&&a.phase==='entering'){assert.ok(s.time<exposed+1e-6,`${label}: in sight on time`);assertHidden(s,h,a,a.pos,`${label} before exposure`);s.step();}
   assert.equal(a.phase,'entering');assert.ok(s.time>=exposed-1e-9,'not before exposure');
   // The hitbox is where the client draws the add (door/cell: the walk; vent: the climb out of the pit at the exit).
   const check=()=>{const b=body(s,a)!;
    if(en.kind==='vent'){const o=ventClimbOffset(s.time,a.activeAt);assert.ok(Math.abs(b.x-(en.exit.x+Math.sin(en.yaw)*o.z))<1e-4&&Math.abs(b.z-(en.exit.z+Math.cos(en.yaw)*o.z))<1e-4,'vent hitbox xz');assert.ok(Math.abs(b.y-(.9+o.y))<.06,`vent hitbox y ${b.y}`);}
    else{const view=decodeAdd(s.snapshot().pve!.adds.find(t=>t[0]===n)!,[]);assert.ok(Math.hypot(b.x-view.pos.x,b.z-view.pos.z)<.02,'door/cell hitbox on the drawn (snapshot) path');
     const along=((b.x-en.from.x)*(en.exit.x-en.from.x)+(b.z-en.from.z)*(en.exit.z-en.from.z))/((en.exit.x-en.from.x)**2+(en.exit.z-en.from.z)**2);assert.ok(along>=-1e-6&&along<=1+1e-6,'on the entry segment');
     const off=Math.abs((b.x-en.from.x)*(en.exit.z-en.from.z)-(b.z-en.from.z)*(en.exit.x-en.from.x));assert.ok(off<1e-3,'not pushed off the path');}};
   check();
   // Gun from the hall, blade close in front, then an AoE: all while it is still entering.
   const target=()=>{const b=body(s,a)!;return {x:b.x,y:b.y+.2,z:b.z};};
   place(gun,ahead(e,3),target());let seq=gun.input.seq,hp=a.hp,shot=false,slashed=false,blasted=false;
   for(let k=0;k<20&&a.phase==='entering'&&!shot;k++){place(gun,ahead(e,3),target());s.setInput('h1',{...idleInput(),seq:++seq,yaw:gun.input.yaw,pitch:gun.input.pitch,attack:k===0});gun.inputAt=s.time;s.step();if(a.phase==='entering'&&a.hp<hp){shot=true;hp=a.hp;check();}}
   assert.ok(shot,`${label}: pistol shot hit while entering`);
   let seq2=blade.input.seq;
   for(let k=0;k<40&&a.phase==='entering'&&!slashed;k++){{const t=target();place(blade,{x:t.x+Math.sin(en.yaw)*1.3,z:t.z+Math.cos(en.yaw)*1.3},t);}s.setInput('h2',{...idleInput(),seq:++seq2,yaw:blade.input.yaw,pitch:0,attack:k<2});blade.inputAt=s.time;s.step();if(a.phase==='entering'&&a.hp<hp){slashed=true;hp=a.hp;check();}}
   assert.ok(slashed,`${label}: sword hit while entering`);
   assert.equal(a.phase,'entering');const b=body(s,a)!;
   const r=s.explode(gun,v(b.x,b.y+.6,b.z),2.5,300,900,'blast',false);blasted=r.minions>0&&a.hp<hp;
   assert.ok(blasted,`${label}: AoE hit while entering`);assert.deepEqual(a.ext,{x:0,z:0},'no knockback while entering');
   s.step();if(a.phase==='entering')check();
   // Statuses tick on the way in.
   assert.equal(s.minionStatus('h1',s.chaos.minions.get(a.id)!,{burn:6}),true);const before=a.hp;for(let k=0;k<20&&a.phase==='entering';k++)s.step();
   if(a.phase==='entering')assert.ok(a.hp<before,'burn ticks while entering');
   assert.equal(a.phase,'entering',`${label}: still entering for the kill`);
   // The kill.
   a.hp=1;const kb=body(s,a)!,from=s.events.at(-1)?.id??0;
   s.explode(gun,v(kb.x,kb.y+.6,kb.z),2.5,300,0,'blast',false);
   assert.equal(h.record(n),undefined,'dead');assert.equal(s.chaos.minions.has(a.id),false,'body removed');
   assert.equal(h.stats.killed,1);assert.equal(h.stats.activated,0);assert.equal(h.alive()+h.queued(),0,'director counts drop');
   assert.equal(sched(h).queueLength(e),0,'entry slot released');assert.equal(h.corpseCount(),1);
   for(let k=0;k<200;k++)s.step();
   assert.equal(h.stats.activated,0,'nothing activates afterwards');assert.equal(h.stats.killed,1,'killed once');
   const kills=s.events.filter(x=>x.id>from&&x.type===PVE_EVENT.kill&&x.target===a.id);assert.equal(kills.length,1,'one kill event');assert.equal(kills[0].source,'h1');
   assert.equal(made.economy!.kills.filter(d=>d.n===n).length,1,'credited once');assert.equal(made.economy!.kills[0].killer,'h1');
   if(en.kind==='vent'){const k=kills[0].pos;assert.ok(Math.hypot(k.x-en.exit.x,k.z-en.exit.z)<1e-6,'vent corpse lies where the climb ends, not over the pit');}
   // The entry still works: the next add comes through and activates.
   place(gun,{x:0,z:0},{x:0,z:1});place(blade,{x:2,z:0},{x:0,z:1});
   const m=h.spawn('shambler',{entry:e});assert.ok(m!==null);for(let k=0;k<300&&h.record(m!)?.phase!=='active';k++)s.step();assert.equal(h.record(m!)?.phase,'active','entry not blocked');
  }finally{s.dispose();}
 });

test('pending adds and vent travellers underground cannot be hit; a re-emerging add can once it is over the rim',()=>{
 const {s,h}=hordeSim();try{
  const p=s.players.get('h1')!;p.hp=p.stats.hp=1e7;
  // Pending: the requested door is too close to a living player, so the add waits without an entry.
  const en=PVE_ENTRIES[13];place(p,en.exit,{x:0,z:0});s.step();
  const n=h.spawn('shambler',{entry:13})!,a=h.record(n)!;assert.equal(a.phase,'pending');
  for(let k=0;k<5;k++){assertHidden(s,h,a,v(en.exit.x,.9,en.exit.z),'pending');s.step();assert.equal(a.phase,'pending');}
  // Vent travel (as in pve-horde 'vent travel'): a far target makes adds dive.
  place(p,{x:0,z:-24},{x:0,z:0});s.bossSong={track:'black-siren-protocol',startedAt:s.time};
  for(let k=0;k<6;k++)h.spawn('shambler',{entry:5});for(let k=0;k<4;k++)h.spawn('shambler',{entry:1});
  let underground=0,emerged=false;
  for(let i=0;i<60*40&&!emerged;i++){s.step();place(p,{x:0,z:-24},{x:0,z:0});
   for(const ref of h.adds()){const r=h.record(ref.n);if(!r)continue;
    if(r.phase==='vent'||r.phase==='entering'&&r.travel&&!body(s,r)){underground++;assertHidden(s,h,r,r.pos,'vent travel');}
    else if(r.phase==='entering'&&r.travel&&body(s,r)){
     assert.ok(s.time>=r.activeAt-(PVE_TIMING.vent.climb-VENT_EXPOSED_AGE)-1e-9,'re-emerging add in sight only over the rim');
     const alive=h.alive(),m=s.chaos.minions.get(r.id)!;r.hp=1;
     assert.ok(h.hit(m,50,'h1',0,v(0,0,1),{kind:'blast'})>0,'re-emerging add takes hits');
     assert.equal(h.record(ref.n),undefined);assert.equal(h.alive(),alive-1,'traveller leaves the alive count');emerged=true;}}
  }
  assert.ok(underground>0,'saw an add underground');assert.ok(emerged,'saw a re-emerging add');
 }finally{s.dispose();}
});

test('wave 1 completes when every add is killed on its way in; money and kill credit once per add',()=>{
 const {factory}=fakeParts({horde:h=>new Horde(h),director:h=>new Director(h),economy:h=>new Economy(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7}}));
 try{
  s.addPlayer('h1','h1','ranged');s.pve!.command('h1',{action:'hello',v:1},false);s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
  const p=s.players.get('h1')!;p.hp=p.stats.hp=1e7;const h=s.pve!.horde as Horde,pve=s.pve!;
  const deaths:{n:number;kind:keyof typeof ADD_KINDS;killer:string|null;headless:boolean}[]=[];const orig=pve.onAddKilled.bind(pve);
  pve.onAddKilled=d=>{deaths.push(d);orig(d);};
  // Kill money only (the wave-clear bonus is paid to everyone separately).
  const econ=pve.economy as Economy,grant=econ.grant.bind(econ);let killMoney=0;
  econ.grant=(id,amount,reason)=>{if(id==='h1'&&(reason==='kill'||reason==='head'))killMoney+=Math.round(amount);grant(id,amount,reason);};
  let i=0;for(;i<2000&&pve.view().stage!=='wave';i++)s.step();
  assert.equal(pve.view().stage,'wave');const wave=pve.view().wave,reward=pve.director.modifiers().reward;
  for(i=0;i<60*120&&pve.view().stage==='wave'&&pve.view().wave===wave;i++){
   for(const ref of h.adds()){const r=h.record(ref.n);if(!r||r.phase!=='entering')continue;const b=body(s,r);if(!b)continue;
    s.explode(p,v(b.x,b.y+.6,b.z),1.5,1e6,0,'blast',false);assert.equal(h.record(ref.n),undefined,'killed on the way in');}
   s.step();
  }
  assert.notEqual(pve.view().stage==='wave'&&pve.view().wave===wave,true,'wave finished');
  assert.equal(h.stats.activated,0,'every add died entering');assert.equal(h.stats.killed,deaths.length);assert.ok(deaths.length>=10,`kills ${deaths.length}`);
  assert.equal(new Set(deaths.map(d=>d.n)).size,deaths.length,'no add killed twice');assert.ok(deaths.every(d=>d.killer==='h1'));
  const expected=deaths.reduce((t,d)=>t+Math.round(ADD_KINDS[d.kind].reward*reward)+(d.headless?ECONOMY.headBonus:0),0);
  assert.equal(killMoney,expected,'money: one kill reward (and head bonus) per add');
  assert.equal(h.alive()+h.queued(),0);
  for(let e=0;e<PVE_ENTRIES.length;e++)assert.equal(sched(h).queueLength(e),0,`entry ${e} released`);
 }finally{s.dispose();}
});

test('client: an add that lost a leg on the way in is still drawn on its entry path',async()=>{
 const {enteringView}=await import('../src/client/pve/horde-view.js');
 assert.equal(enteringView({state:'entering',entry:3}),true);assert.equal(enteringView({state:'crawl',entry:3}),true);
 assert.equal(enteringView({state:'crawl',entry:-1}),false);assert.equal(enteringView({state:'vent',entry:3}),false);assert.equal(enteringView({state:'dead',entry:-1}),false);
});
