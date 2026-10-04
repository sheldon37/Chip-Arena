// "Find me..." (Siren phase 2) on the real PvE parts: a wipe during the hunt is a plain defeat (the Siren is not "found"
// by the defeat's killAll), and a debug phase skip releases the possessed add (no possession cap or ×3 HP afterwards).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput} from '../src/shared/config.js';
import {HORDE,PVE_SIREN} from '../src/shared/pve/balance.js';
import type {Horde} from '../src/shared/pve/horde.js';
await initPhysics();

function hunt(){
 const s=new Simulation({pve:{boss:'siren',seed:1,skipWaves:true,debug:true}});
 s.addPlayer('h1','H1','ranged');s.pve!.command('h1',{action:'hello',v:1},false);s.setReady('h1',true);
 for(let i=0;i<600&&s.phase!=='specialization';i++)s.step();
 s.selectSpec('h1','rifle');
 for(let i=0;i<6000&&s.phase!=='fight';i++)s.step();
 const pve=s.pve!,horde=pve.horde as Horde;let seq=0;
 const tick=()=>{const row=pve.view().party.h1;if(row?.open){const pick=row.picks.find(k=>k.id===row.open);if(pick?.offers?.length)pve.command('h1',{action:'pick',pick:pick.id,chip:pick.offers[0]},false);}
  s.queueInputs('h1',[{...idleInput(),seq:++seq}],`${s.setNumber}:${s.round}:${s.generation}`);s.step();};
 const until=(f:()=>boolean,max:number)=>{for(let i=0;i<max&&!f();i++)tick();return f();};
 pve.debug('god:on');
 assert.ok(until(()=>pve.view().stage==='boss'&&!pve.view().hold&&horde.alive()>=3,60*80),'boss stage with adds');
 assert.ok(pve.debug('boss-phase:2'));
 assert.ok(until(()=>pve.view().bossView?.phase===2&&!pve.view().hold&&horde.possessed()!==undefined,60*20),'the hunt is on');
 return {s,pve,horde,tick,until};
}

test('a wipe during the hunt is a plain defeat: the Siren stays hidden at its HP, no "found", no possessed reward',()=>{
 const {s,pve,horde,until}=hunt();try{
  until(()=>false,60*4);assert.equal(horde.cap(),Math.min(HORDE.maxAlivePossession,18),'solo hunt cap');
  const boss=s.players.get(pve.view().bossView!.id)!,hp=boss.hp;assert.equal(pve.hidden(boss),true);
  const seen:string[]=[],emit=s.emit.bind(s);(s as unknown as {emit:typeof emit}).emit=((type:string,...rest:unknown[])=>{seen.push(type);return (emit as (...a:unknown[])=>unknown)(type,...rest);}) as typeof emit;
  const econ=pve.economy as unknown as {grant(id:string,amount:number,reason:string):void},grant=econ.grant.bind(econ),possessedGrants:number[]=[];
  econ.grant=(id,amount,reason)=>{if(reason==='possessed')possessedGrants.push(amount);grant(id,amount,reason);};
  pve.debug('god:off');const me=s.players.get('h1')!;s.damage(me,me.hp+1e5,null,'test');
  assert.ok(until(()=>!!pve.view().outcome,60*90),'the run ends');assert.equal(pve.view().outcome,'defeat');
  assert.ok(!seen.includes('pve-found'),'not found');assert.ok(!seen.some(t=>t==='pve-phase'),'no chip-3 reveal');
  assert.deepEqual(possessedGrants,[],'no possessed-kill money for the defeat burn');
  assert.equal(boss.hp,hp,'the Siren keeps its HP');assert.equal(pve.view().bossView?.foundAt,undefined);
 }finally{s.dispose();}
});

test('a debug phase skip past the hunt releases the possessed add: usual cap, ×3 HP undone, no possession in the view',()=>{
 const {s,pve,horde,until}=hunt();try{
  const n=horde.possessed()!,rec=horde.record(n)!,max=rec.maxHp;
  assert.ok(pve.debug('boss-phase:3'));assert.ok(until(()=>!pve.view().hold,60*20));
  assert.equal(pve.view().bossView?.phase,3);assert.equal(horde.possessed(),undefined,'released');
  assert.equal(pve.view().bossView?.possessed,undefined);assert.ok(horde.cap()<=HORDE.maxAlive,'phase-3 cap');
  if(horde.record(n))assert.ok(Math.abs(horde.record(n)!.maxHp-max/PVE_SIREN.possession.hpScale)<1e-6,'×3 HP undone');
  for(let i=0;i<60;i++)horde.spawn('shambler');assert.ok(horde.alive()+horde.queued()<=HORDE.maxAlive,'debug spawns stop at the usual cap');
 }finally{s.dispose();}
});
