import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {effectiveIntro,introDuration,introFrame} from '../src/shared/intros.js';
await initPhysics();

test('ordinary and non-boss DEV bots have no intro slots or waiting time',()=>{
 const s=new Simulation();try{
  s.addPlayer('human');s.addPlayer('ordinary','Bot','ranged','hard');
  s.addPlayer('dev','DEV bot','melee','hard','#ffffff',{},true);
  s.players.get('dev')!.spec='emperor';
  s.addPlayer('gone');s.players.get('gone')!.connected=false;
  s.beginIntros();assert.deepEqual(s.intro?.players,['human']);
  assert.equal(s.phaseEnd-s.time,introDuration(effectiveIntro(s.players.get('human'))));
  assert.equal(introFrame(s.snapshot())?.total,1);
  s.time=s.phaseEnd;s.advance();assert.equal(s.phase,'countdown');
 }finally{s.dispose();}
});
test('Siren bot enters after humans and boss music starts at its filtered slot',()=>{
 const s=new Simulation();try{
  s.addPlayer('boss','Siren','magic','hard','#ffffff',{},true);s.players.get('boss')!.spec='siren';
  s.addPlayer('ordinary','Bot','ranged','hard');s.addPlayer('human');
  s.beginIntros();assert.deepEqual(s.intro?.players,['human','boss']);
  const humanDuration=introDuration(effectiveIntro(s.players.get('human')));
  assert.equal(s.phaseEnd-s.time,humanDuration+introDuration('dev-siren'));
  s.updateBossSong();assert.equal(s.bossSong,undefined);
  s.time+=humanDuration;s.updateBossSong();
  assert.equal(introFrame(s.snapshot())?.player.id,'boss');assert.equal(s.snapshot().bossSong?.startedAt,s.time);
  const copy=structuredClone(s.snapshot());assert.deepEqual(introFrame(copy),introFrame(s.snapshot()));
 }finally{s.dispose();}
});
test('empty bot-only sequence goes directly to countdown; global skip still includes bosses',()=>{
 const s=new Simulation();try{
  s.addPlayer('ordinary','Bot','ranged','easy');s.beginIntros();
  assert.equal(s.phase,'countdown');assert.equal(s.intro,undefined);
  s.addPlayer('boss','Siren','magic','hard','#ffffff',{},true);s.players.get('boss')!.spec='siren';
  s.beginIntros();assert.deepEqual(s.snapshot().intro?.players,['boss']);
  s.options.skipIntros=true;s.beginIntros();assert.equal(s.phase,'countdown');assert.equal(s.intro,undefined);
  s.options.skipIntros=false;s.options.sandbox=true;s.beginIntros();assert.equal(s.phase,'countdown');
 }finally{s.dispose();}
});
