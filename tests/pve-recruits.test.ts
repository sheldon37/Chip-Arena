// PvE S3 recruits: roles, weapons (D40), stats, brains (medic D41, leash, taunt, block), heal ledger, real damage to adds,
// delayed removal (design §4.5, §11 S3). Real Economy and Recruits; fake horde (real add bodies) and fake director.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,type SpecId} from '../src/shared/config.js';
import {WEAPONS,statsFor,magazineCapacity} from '../src/shared/content.js';
import {PVE_EVENT,type PveOptions,type RecruitRole} from '../src/shared/pve/contract.js';
import {RECRUIT} from '../src/shared/pve/balance.js';
import {Economy} from '../src/shared/pve/economy.js';
import {RECRUIT_DPS_SPECS,Recruits} from '../src/shared/pve/recruits.js';
import {medicFocus,medicPulse,recruitInput} from '../src/shared/pve/recruit-ai.js';
import {fakeParts,withPveParts,type FakeHorde} from './helpers/pve-fakes.js';
await initPhysics();

function world(o:Partial<PveOptions>={},humans:string[]=['h1']){
 let econ!:Economy,rec!:Recruits;
 const {factory,made}=fakeParts({economy:h=>(econ=new Economy(h)),recruits:h=>(rec=new Recruits(h))});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,...o}}));
 for(const id of humans){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
 for(const id of humans)s.pve!.command(id,{action:'close'},false);
 return {s,econ,rec,horde:made.horde as FakeHorde};
}
const P=(s:Simulation,id:string)=>s.players.get(id)!;
const place=(p:Player,x:number,z:number)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);};
/** Steps with fresh inputs for humans (they idle in place). */
const tick=(s:Simulation,n=1)=>{for(let i=0;i<n;i++){for(const p of s.players.values())if(!p.bot)p.inputAt=s.time;s.step();}};
/** Hires (dismissing other rolls) until `role`; optionally swaps the weapon with the same stats override as a hire. */
function hireAs(s:Simulation,rec:Recruits,owner:string,role:RecruitRole,at={x:0,z:-6},spec?:SpecId){
 for(let i=0;i<200;i++){const id=rec.hire(owner,s.pve!.standAt(at.x,at.z),0)!;assert.ok(id);if(rec.role(id)===role){
  const p=P(s,id);if(spec&&p.spec!==spec){p.spec=spec;p.stats={...statsFor({},spec),hp:RECRUIT.hp[role]};p.hp=p.stats.hp;p.ammo=magazineCapacity(spec,p.stats);}return p;}
  rec.dismiss(id);}
 throw Error('no '+role);
}

test('hire: seeded role distribution over 1,000 hires; the DPS pool is exactly the 7 non-DEV ranged and melee specs',()=>{
 assert.deepEqual([...RECRUIT_DPS_SPECS].sort(),['daggers','greatsword','machinegun','pistol','rifle','shield','sword']);
 for(const id of RECRUIT_DPS_SPECS){assert.ok(!WEAPONS[id].devOnly);assert.ok(['ranged','melee'].includes(WEAPONS[id].class));}
 const roll=(seed:number)=>{const {s,rec}=world({seed});const roles:RecruitRole[]=[],specs=new Set<SpecId>();try{
  for(let i=0;i<1000;i++){const id=rec.hire('h1',s.pve!.standAt(0,-6),0)!;assert.ok(id);const r=rec.role(id)!;roles.push(r);const p=P(s,id);
   if(r==='dps')specs.add(p.spec);else assert.equal(p.spec,r==='tank'?'shield':'pistol');
   assert.equal(p.team,'blue');assert.equal(p.hp,RECRUIT.hp[r]);rec.dismiss(id);}
 }finally{s.dispose();}return {roles,specs};};
 const a=roll(3),b=roll(3);assert.deepEqual(a.roles,b.roles,'seeded');
 for(const role of ['dps','tank','medic'] as const){const f=a.roles.filter(r=>r===role).length/1000;assert.ok(Math.abs(f-RECRUIT.roles[role])<=.05,`${role} ${f}`);}
 assert.deepEqual([...a.specs].sort(),[...RECRUIT_DPS_SPECS].sort(),'every DPS weapon appears');
});

test('stats override: tank 250 HP with damageTaken .7, dps 100, medic 80; recruit event, view and weaponAway for medics',()=>{
 const {s,rec}=world();try{
  const tank=hireAs(s,rec,'h1','tank');assert.equal(tank.stats.hp,250);assert.equal(tank.hp,250);assert.equal(tank.stats.damageTaken,RECRUIT.tankDamageTaken);
  const before=tank.hp;s.step();s.damage(tank,100,null,'environment');assert.ok(Math.abs(before-tank.hp-70)<1e-6,'damageTaken .7');
  const medic=hireAs(s,rec,'h1','medic');assert.equal(medic.stats.hp,80);assert.equal(rec.weaponAway(medic),true);assert.equal(rec.weaponAway(tank),false);
  assert.equal(s.snapshot().players.find(p=>p.id===medic.id)?.weaponAway,true);
  const view=rec.views().find(r=>r.id===medic.id)!;assert.equal(view.owner,'h1');assert.equal(view.role,'medic');assert.ok(view.at<=s.time);
  assert.ok(s.events.some(e=>e.type===PVE_EVENT.recruit&&e.target===medic.id&&e.source==='h1'&&e.message==='medic'));
  assert.equal(P(s,medic.id).color,P(s,'h1').color,'owner colour');
 }finally{s.dispose();}
});

test('medic: never attacks, follows its hirer, switches only by the D41 rule, rolls from a wind-up',()=>{
 const {s,rec,horde}=world({partyBots:1},['h1','h2']);try{
  const h1=P(s,'h1'),h2=P(s,'h2'),bot=P(s,'party-bot-1');place(h1,0,-6);place(h2,6,-6);place(bot,-6,-6);
  const medic=hireAs(s,rec,'h1','medic',{x:0,z:-14});
  assert.equal(medicFocus(s.pve!,medic,'h1')?.id,'h1');
  h2.hp=30;assert.equal(medicFocus(s.pve!,medic,'h1')?.id,'h2','member below 35% while the hirer is above 70%');
  h1.hp=60;assert.equal(medicFocus(s.pve!,medic,'h1')?.id,'h1','hirer at 60%: stays');
  h1.hp=100;h2.hp=40;assert.equal(medicFocus(s.pve!,medic,'h1')?.id,'h1','switches back when the member is above 35%');
  h2.hp=100;
  // Far from the hirer: moves toward it; never attacks even with adds around.
  horde.addAt({x:1,z:-12});horde.addAt({x:-1,z:-12});
  let moved=0;for(let i=0;i<120;i++){const inp=rec.inputFor(medic)!;assert.equal(inp.attack,false);assert.equal(inp.secondary,false);if(inp.z>.3)moved++;tick(s);}
  assert.ok(moved>0,'heads toward the hirer');
  // Wind-up within 2 m → roll.
  const a=horde.addAt({x:medic.body.translation().x+1,z:medic.body.translation().z},'shambler',{target:medic.id});
  const ref=horde.adds().find(x=>x.id===a.id)!;(horde as unknown as {list:Map<number,{attackAt?:number}>}).list.get(ref.n)!.attackAt=s.time;
  assert.equal(rec.inputFor(medic)!.dash,true);
 }finally{s.dispose();}
});

test('heal ledger: 1/2/3 medics heal 3 / 4.5→cap 4 / cap 4 per pulse on a 100 HP target; ×0.6 under fire; pulses every 0.5 s',()=>{
 const {s,rec}=world();try{
  const h1=P(s,'h1');place(h1,0,-6);
  const medics=[0,1,2].map(i=>hireAs(s,rec,'h1','medic',{x:-2+2*i,z:-9}));
  for(const m of medics)place(m,m.body.translation().x,-9);tick(s,2);
  const pulse=(n:number,underFire=false)=>{h1.hp=50;h1.lastDamage=underFire?s.time:-10;return medicPulse(s.pve!,medics.slice(0,n)).get('h1');};
  assert.equal(pulse(1),3);assert.equal(pulse(2),4);assert.equal(pulse(3),4);
  assert.ok(Math.abs(pulse(1,true)!-1.8)<1e-9);
  // Through Recruits.step: pulses every 0.5 s.
  h1.hp=50;h1.lastDamage=-10;let heals=0;const t0=s.time;
  for(let i=0;i<60;i++){const hp=h1.hp;tick(s);h1.lastDamage=-10;if(h1.hp>hp)heals++;}
  assert.equal(heals,2,`two pulses in 1 s (${s.time-t0})`);
 }finally{s.dispose();}
});

test('recruits damage fake adds through real projectiles and melee; tank taunts and blocks a crowd',()=>{
 const {s,rec,horde}=world();try{
  const h1=P(s,'h1');place(h1,0,1);   // x=0 between z=-11 and 11 is open floor on Ward Zero
  const gun=hireAs(s,rec,'h1','dps',{x:0,z:-2},'pistol');place(gun,0,-2);
  const add=horde.addAt({x:0,z:-9},'brute',{hp:5000});
  for(let i=0;i<180&&!horde.hits.some(h=>h.attacker===gun.id);i++)tick(s);
  assert.ok(horde.hits.some(h=>h.attacker===gun.id&&h.damage>0),'pistol recruit hits the add with real projectiles');
  rec.dismiss(gun.id);
  const blade=hireAs(s,rec,'h1','dps',{x:0,z:-7.6},'sword');place(blade,0,-7.6);
  for(let i=0;i<180&&!horde.hits.some(h=>h.attacker===blade.id);i++)tick(s);
  assert.ok(horde.hits.some(h=>h.attacker===blade.id&&h.damage>0),'sword recruit hits the add in melee');
  rec.dismiss(blade.id);void add;
  const tank=hireAs(s,rec,'h1','tank',{x:2,z:-8});place(tank,2,-8);tick(s,2);
  const vt=rec.views().find(r=>r.id===tank.id)!;assert.ok(vt.tauntUntil!==undefined&&vt.tauntUntil>s.time,'taunting with an add within 8 m');
  assert.equal(rec.tauntTarget({x:0,y:0,z:-9}),tank.id);assert.equal(rec.tauntNear({x:0,y:0,z:-9}),true);assert.equal(rec.tauntTarget({x:30,y:0,z:20}),null);
  horde.addAt({x:3,z:-8.5});horde.addAt({x:2,z:-6.8});horde.addAt({x:1,z:-8});tick(s);
  assert.equal(rec.inputFor(tank)!.block,true,'≥3 adds within 2.5 m');
 }finally{s.dispose();}
});

test('leash: beyond 12 m from the hirer a recruit heads back; an orphaned recruit follows the nearest human',()=>{
 const {s,rec}=world({},['h1','h2']);try{
  const h1=P(s,'h1'),h2=P(s,'h2');place(h1,0,10);place(h2,-15,-15);
  const r=hireAs(s,rec,'h1','dps',{x:0,z:-10},'rifle');place(r,0,-10);tick(s);
  const i=recruitInput(s.pve!,r,'dps','h1');assert.ok(i.z>.7,`toward the hirer: ${i.x},${i.z}`);
  h1.connected=false;const j=recruitInput(s.pve!,r,'dps','h1');assert.ok(j.x<-.4&&j.z<0,`toward the nearest human: ${j.x},${j.z}`);
 }finally{s.dispose();}
});

test('a dead recruit is removed after 2.5 s and frees its seat; newest/dismiss; reset clears',()=>{
 const {s,econ,rec}=world();try{
  const r=hireAs(s,rec,'h1','dps');const size=s.players.size;
  assert.equal(rec.newest(),r.id);s.damage(r,1e6,null,'environment');assert.ok(r.hp<=0);
  tick(s,140);assert.ok(s.players.has(r.id),'still there before 2.5 s');tick(s,20);
  assert.ok(!s.players.has(r.id));assert.equal(s.players.size,size-1);assert.equal(rec.views().length,0);
  assert.equal(rec.canHire('h1'),'ok');void econ;
  const a=hireAs(s,rec,'h1','tank'),b=hireAs(s,rec,'h1','medic');assert.equal(rec.canHire('h1'),'limit','2 per player');
  assert.equal(rec.newest(),b.id);assert.deepEqual(rec.dismiss(b.id),{owner:'h1'});assert.equal(rec.dismiss('recruit-404'),null);
  assert.equal(rec.newest(),a.id);s.pve!.reset();assert.equal(rec.views().length,0);assert.ok(!s.players.has(a.id));
 }finally{s.dispose();}
});
