import test from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Projectile} from '../src/shared/simulation.js';
import {statsFor,WEAPONS} from '../src/shared/content.js';
import {idleInput,v,type SpecId} from '../src/shared/config.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();
const FIELDS={'black-hole':1,'acid-rain':1};
function arena(chips:Record<string,number>,spec:SpecId='pistol'){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b','B')!;
 a.spec=spec;a.chips=chips;a.stats=statsFor(chips,spec);a.input={...idleInput(),camera:'first'};s.phase='fight';
 for(const prop of [...s.props.values()])s.removeProp(prop.id);
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,6),true);b.hp=b.stats.hp=1e6;
 s.addProp({id:'floor',pos:v(0,-.5,0),size:v(80,1,80),material:'foundation'});s.world.step();return {s,a,b};
}
const aim=(q:Projectile)=>{q.pos=v(0,1.2,2);q.vel=v(0,0,50);q.gravity=0;q.impulse=0;q.chaos=undefined;q.mods={};};
const fly=(s:Simulation)=>{for(let n=0;n<20&&s.projectiles.size;n++)s.stepProjectiles(1/60);};
for(const [id,count] of [['multishot',2],['broadside',3],['buckshot',6],['torrent',4],['needle',3]] as const){
 test(`${id}: all ${count} projectiles create impact fields, while Shrapnel stays once per attack`,()=>{
  const {s,a,b}=arena({...FIELDS,[id]:1,shrapnel:1});try{
   b.body.setTranslation(v(0,1,30),true);s.addProp({id:'wall',pos:v(0,2,6),size:v(60,4,.5),material:'foundation'});s.world.step();
   s.spawnProjectile(a);assert.equal(s.projectiles.size,count);fly(s);
   assert.equal(s.chaos.fields.size,count*2);assert.equal(s.events.filter(e=>e.type==='explosion'&&e.message==='shrapnel').length,1);
   assert.equal(s.projectiles.size,0);
  }finally{s.dispose();}
 });
}
for(const kind of ['player','prop','summon'] as const)test(`Blade Runner carries impact Chips onto ${kind} targets`,()=>{
 const {s,a,b}=arena({...FIELDS,'sword-beam':1},'sword');try{
  if(kind!=='player')b.body.setTranslation(v(20,1,20),true);
  if(kind==='prop')s.addProp({id:'wall',pos:v(0,2,6),size:v(8,4,.5),material:'foundation'});
  if(kind==='summon'){s.chaos.summon(b,1,'zombie',v(0,1,6));[...s.chaos.minions.values()][0].body.setTranslation(v(0,1,6),true);}
  s.world.step();s.chaos.meleeStart(a);const q=[...s.projectiles.values()][0];assert.equal(q.chipImpact,true);aim(q);fly(s);
  assert.equal(s.chaos.fields.size,2);assert.equal(s.projectiles.size,0,'impact effects never create more blades');
 }finally{s.dispose();}
});
for(const modifier of ['orbitals','backfire','crossfire','helix','splitter'] as const)test(`${modifier} retains its explicit no-Chip/no-recursive-effects restriction`,()=>{
 const {s,a}=arena({...FIELDS,[modifier]:1});try{
  s.spawnProjectile(a);
  if(modifier==='splitter'){const q=[...s.projectiles.values()][0];aim(q);for(let i=0;i<10&&!q.effectsUsed;i++)s.stepProjectiles(1/60);}
  const extra=[...s.projectiles.values()].filter(q=>!q.direct);assert.ok(extra.length>0);s.projectiles.clear();s.chaos.fields.clear();
  for(const q of extra){q.hit.clear();aim(q);s.projectiles.set(q.id,q);}fly(s);assert.equal(s.chaos.fields.size,0);
 }finally{s.dispose();}
});
for(const splitter of [0,1])test(`Firework Factory fragments trigger impacts without re-splitting (Splitter ${splitter})`,()=>{
 const {s,a}=arena({...FIELDS,fireworks:1,splitter});try{
  s.spawnProjectile(a);const q=[...s.projectiles.values()][0];aim(q);
  for(let n=0;n<10&&!q.effectsUsed;n++)s.stepProjectiles(1/60);
  const fragments=[...s.projectiles.values()].filter(x=>!x.direct);assert.equal(fragments.length,5+splitter);
  assert.equal(fragments.filter(x=>x.chipImpact).length,5,'only the Firework-generated fragments allow impact Chips');
  s.chaos.fields.clear();for(const f of fragments){f.hit.clear();aim(f);}fly(s);
  assert.equal(s.chaos.fields.size,10);assert.equal(s.projectiles.size,0,'no recursive fragments');
 }finally{s.dispose();}
});
test('reflection disables the extra projectile impact permission',()=>{
 const {s,a,b}=arena({...FIELDS,'sword-beam':1},'sword');try{
  s.chaos.meleeStart(a);const q=[...s.projectiles.values()][0];aim(q);b.blockTime=1;
  for(let i=0;i<8&&!q.reflections;i++)s.stepProjectiles(1/60);
  assert.ok(q.reflections);assert.equal(q.chipImpact,false);assert.equal(s.chaos.fields.size,0);
 }finally{s.dispose();}
});
test('each added projectile applies its own impact fields to a real Siren add',()=>{
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7}}));try{
  const a=s.addPlayer('a','A','ranged')!;s.pve!.command('a',{action:'hello',v:1},false);s.phase='fight';a.hp=a.stats.hp=1e8;s.pve!.debug('god:on');
  a.chips={...FIELDS,multishot:2};a.stats={...statsFor(a.chips),hp:1e8};a.body.setTranslation(v(0,1,0),true);
  const h=s.pve!.horde as Horde,n=h.spawn('brute',{entry:12})!;
  for(let i=0;i<300&&h.record(n)?.phase!=='active';i++)s.step();
  const add=s.chaos.minions.get(h.get(n)!.id)!;add.body.setTranslation(v(0,1,6),true);a.body.setTranslation(v(0,1,0),true);s.world.step();
  s.spawnProjectile(a);for(const q of s.projectiles.values())aim(q);fly(s);assert.equal(s.chaos.fields.size,6);
 }finally{s.dispose();}
});
