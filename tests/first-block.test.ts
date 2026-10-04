import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,idleInput,type SpecId} from '../src/shared/config.js';
import {statsFor,WEAPONS} from '../src/shared/content.js';
import {FIRST_BLOCK_CHIPS} from '../src/shared/blocking.js';
import {ChipEffects} from '../src/client/chip-effects.js';
import {EventVfx} from '../src/client/event-vfx.js';
await initPhysics();
function arena(chip:string,spec:SpecId='pistol',distance=2){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','Shooter')!,b=s.addPlayer('b','Blocker')!;s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(30,1,30),material:'foundation'});
 a.spec=spec;a.class=WEAPONS[spec].class;a.body.setTranslation(v(0,1,4-distance),true);b.body.setTranslation(v(0,1,4),true);
 a.input={...idleInput(),camera:'first'};b.input={...idleInput(),yaw:Math.PI};b.chips={[chip]:1};b.stats=statsFor(b.chips);b.hp=50;b.ammo=0;
 const prop=s.addProp({id:'loose',pos:v(2,1,4),size:v(.3,.3,.3),material:'metal',dynamic:true})!;s.world.step();return {s,a,b,prop};
}
function bullet(s:Simulation,a:Player,b:Player){
 s.projectiles.clear();s.spawnProjectile(a);const shot=[...s.projectiles.values()][0];
 for(let n=0;n<60&&shot.owner!==b.id&&s.projectiles.has(shot.id);n++)s.stepProjectiles(1/60);
 assert.equal(shot.owner,b.id,'actual swept projectile must be blocked');
}
for(const spec of ['pistol','rifle','machinegun','fire'] as const)test(`all first-block chips trigger once on an actual ${spec} projectile, then reset on the next block`,()=>{
 for(const chip of FIRST_BLOCK_CHIPS){const {s,a,b,prop}=arena(chip,spec);try{
  assert.ok(s.beginBlock(b));assert.equal(s.events.filter(e=>e.type==='blockProc').length,0,'pressing Block alone does not trigger chips');bullet(s,a,b);
  assert.deepEqual(s.events.find(e=>e.type==='blockProc')?.mods,{[chip]:1});
  if(chip==='supernova'||chip==='block-pulse'){assert.ok(a.hp<100);assert.ok(a.ext.z<0);assert.equal(s.events.filter(e=>e.type==='blockPulse').length,1);}
  if(chip==='block-medic')assert.equal(b.hp,59);
  if(chip==='block-loader')assert.equal(b.ammo,1);
  if(chip==='mirror-mine')assert.equal([...s.chaos.fields.values()].filter(f=>f.kind==='mine').length,3);
  if(chip==='freezer-block')assert.equal([...s.chaos.fields.values()].filter(f=>f.kind==='time').length,1);
  if(chip==='graveyard-shift'||chip==='corpse-party')assert.equal(s.chaos.minions.size,1);
  if(chip==='poltergeist')assert.ok(prop.body.linvel().x>0);
  // 2026-10-03 rework (intended): Time Out now freezes the nearby shooter (and their shots) for 1.2 s; thaw it so the reset check can fire again.
  if(chip==='freezer-block'){assert.ok((a.leet?.frozenUntil||0)>s.time);a.leet!.frozenUntil=0;}
  const before={hp:b.hp,ammo:b.ammo,fields:s.chaos.fields.size,minions:s.chaos.minions.size};bullet(s,a,b);
  assert.equal(s.events.filter(e=>e.type==='blockProc').length,1);assert.deepEqual({hp:b.hp,ammo:b.ammo,fields:s.chaos.fields.size,minions:s.chaos.minions.size},before);
  b.blockTime=b.blockCd=0;assert.ok(s.beginBlock(b));bullet(s,a,b);assert.equal(s.events.filter(e=>e.type==='blockProc').length,2);
 }finally{s.dispose();}}
});
test('Supernova pulses around the blocker even when the shooter is out of blast range',()=>{
 const {s,a,b}=arena('supernova','rifle',12);s.beginBlock(b);bullet(s,a,b);assert.equal(a.hp,100);const pulse=s.events.find(e=>e.type==='blockPulse')!;
 assert.equal(pulse.radius,3);assert.equal(pulse.pos.z,b.body.translation().z);assert.equal(pulse.mods?.supernova,1);s.dispose();
});
test('block pulses hit nearby enemy summons without self-occlusion and still respect cover',()=>{
 for(const cover of [false,true]){const {s,a,b}=arena('supernova');try{
  s.chaos.summon(a,1,'zombie',v(0,1,4));const minion=[...s.chaos.minions.values()][0];minion.body.setTranslation(v(1.5,1,4),true);
  if(cover)s.addProp({id:'wall',pos:v(.75,1,4),size:v(.1,3,1),material:'foundation'});s.world.step();s.beginBlock(b);bullet(s,a,b);
  assert.equal(minion.hp,cover?minion.maxHp:minion.maxHp-45);
 }finally{s.dispose();}}
});
// INTEGRATION (design §19.2, chip VFX sweep): the flat chip-effects pulse rings this test pinned were replaced by pooled
// volumetric ChipBlasts blasts drawn by EventVfx. Same real block path; it now asserts no per-event ring meshes and a
// visible voxel blast that cleans up (the engine-path twin lives in chip-vfx-sweep.test.ts).
test('Supernova and Counter Pulse each render a visible blast and clean it up',()=>{
 for(const chip of ['supernova','block-pulse']){const {s,a,b}=arena(chip);s.beginBlock(b);bullet(s,a,b);const scene=new T.Scene(),effects=new ChipEffects(scene),e=s.events.find(e=>e.type==='blockPulse')!;effects.event(e,0);
  assert.equal(effects.pulses.length,0);assert.equal(scene.children.length,0,'no flat pulse rings');
  const vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:false})),n=scene.children.length;vfx.blastEvent(e);vfx.event(e,s.snapshot(),'a');assert.equal(scene.children.length,n,'pooled, no per-event meshes');
  assert.ok(vfx.chipBlasts.glow.count+vfx.chipBlasts.solid.count>0,chip);for(let i=0;i<120;i++)vfx.update(1/60);assert.equal(vfx.chipBlasts.glow.count+vfx.chipBlasts.solid.count,0,'cleaned up');s.dispose();
 }
});
