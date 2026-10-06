import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v,idleInput} from '../src/shared/config.js';
import {sirenStageHazards} from '../src/shared/siren-stage-layout.js';
import {sirenSuperArmor} from '../src/shared/siren-motion.js';
import {SIREN_SLOTS} from '../src/shared/siren-contract.js';
await initPhysics();
function arena(){const s=new Simulation({sandbox:true});const boss=s.addPlayer('boss','Boss','dev',null,'#fff',{},true)!,enemy=s.addPlayer('enemy','Enemy','dev',null,'#fff',{},true)!;
 boss.spec='siren';enemy.spec='emperor';s.resetRound();s.phase='fight';s.time=1;
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 boss.body.setTranslation(v(0,1,0),true);enemy.body.setTranslation(v(0,1,2),true);boss.grounded=enemy.grounded=true;boss.input=idleInput();s.world.step();return {s,boss,enemy};}
test('all eight numbered abilities armor their complete committed window, excluding primary and lingering effects',()=>{
 for(const mode of ['melee','ranged'] as const)for(const id of [...SIREN_SLOTS[mode].abilities,SIREN_SLOTS[mode].primary]){
  const {s,boss}=arena();try{s.siren.state(boss).mode=mode;assert.equal(s.siren.start(boss,id),true,id);const move=boss.siren!.move!;const expected=id!=='swipe'&&id!=='hellfire';
   assert.equal(s.siren.armored(boss),expected,id);assert.equal(sirenSuperArmor(s.snapshot().players[0],move.until-1e-6),expected);
   assert.equal(sirenSuperArmor(s.snapshot().players[0],move.at-1e-6),false);s.time=move.until;assert.equal(s.siren.armored(boss),false);
  }finally{s.dispose();}
 }
});
test('armor takes full damage and DOT while suppressing physical recoil, visual flinch, stagger and launches',()=>{
 const {s,boss,enemy}=arena();try{assert.ok(s.siren.start(boss,'rend'));const move=boss.siren!.move,hp=boss.hp;
  assert.equal(s.damage(boss,20,enemy.id,'direct',v(0,0,-1),500,boss.body.translation(),true),20);
  assert.equal(boss.hp,hp-20);assert.deepEqual(boss.ext,v());assert.equal(boss.hitReaction,undefined);
  s.standardMelee.stun(boss,.5);assert.equal(s.standardMelee.stagger(boss,.5),false);assert.equal(s.standardMelee.launch(boss,enemy,8),false);
  s.arcade.confirmHit(enemy,boss,20,'punch',.5,'HIT');s.arcade.knockDown(boss);s.emperor.confirm(enemy,boss,20,true);
  assert.equal(boss.hitStunUntil,0);assert.equal(boss.knockdown,undefined);assert.equal(boss.juggle,undefined);assert.deepEqual(boss.ext,v());
  assert.equal(s.damage(boss,5,enemy.id,'burn'),5);assert.equal(boss.hp,hp-25);assert.equal(boss.siren!.move,move);
  s.siren.stepPlayer(boss,1/60);assert.equal(boss.siren!.move,move,'hit does not cancel ability');
  enemy.chips={uppercut:1,'grappling-shot':1,'coin-toss':1,trampoline:1};
  s.chaos.direct(enemy,boss,boss.body.translation(),v(0,0,1),'armor-chips');s.chaos.jump(enemy);s.vacuum(enemy,enemy.body.translation(),1000);
  assert.deepEqual(boss.ext,v(),'chip launch, grapple, trampoline and vacuum cannot move an armored caster');
  s.chaos.field(enemy,'black-hole',v(0,1,1),5,2);s.chaos.step(1/60);assert.deepEqual(boss.ext,v());
  const songTime=Array.from({length:2400},(_,i)=>i/10).find(t=>sirenStageHazards(t).some(h=>h.active))!;
  const vent=sirenStageHazards(songTime).find(h=>h.active)!;s.options.map='siren';s.bossSong={track:'black-siren-protocol',startedAt:s.time-songTime};
  boss.body.setTranslation(v(vent.pos.x,.9,vent.pos.z),true);const beforeVent=boss.hp;s.stageHazards.step();
  assert.ok(boss.hp<beforeVent,'stage hazard still damages Siren');assert.deepEqual(boss.ext,v(),'stage hazard cannot launch armored Siren');

 }finally{s.dispose();}
});
test('blocked armored melee does no damage but cannot rebound-stun its caster',()=>{
 const {s,boss,enemy}=arena();try{s.siren.start(boss,'rend');enemy.blockTime=.3;const hp=enemy.hp;
  assert.equal(s.siren.hit(boss,enemy,22,'rend',true),0);assert.equal(enemy.hp,hp);assert.equal(boss.hitStunUntil,0);assert.equal(boss.hitReaction,undefined);assert.deepEqual(boss.ext,v());
 }finally{s.dispose();}
});
test('armor ends on cancel, death, disconnect, reset and spec change; ordinary hit reactions still work',()=>{
 for(const action of ['cancel','death','disconnect','reset','spec']){const {s,boss,enemy}=arena();try{s.siren.start(boss,'slam');
  if(action==='cancel')s.siren.cancel(boss);if(action==='death')s.damage(boss,boss.hp+1,enemy.id,'direct',v(0,0,1),100);
  if(action==='disconnect')s.disconnect(boss.id);if(action==='reset')s.resetRound();if(action==='spec')boss.spec='pistol';
  assert.equal(s.siren.armored(boss),false,action);if(action==='death'){assert.equal(boss.hp,0);assert.ok(boss.death);assert.equal(boss.siren?.move,undefined);}
 }finally{s.dispose();}}
 const {s,boss,enemy}=arena();try{s.damage(boss,20,enemy.id,'direct',v(0,0,-1),500,boss.body.translation());assert.ok(boss.ext.z<0);assert.ok(boss.hitReaction);assert.equal(s.standardMelee.stagger(boss,.3),true);}finally{s.dispose();}
});
test('Last Laugh keeps armor during its extended hold while lethal damage releases the victim',()=>{
 const {s,boss,enemy}=arena();try{s.siren.start(boss,'laugh');const move=boss.siren!.move!;move.caught=enemy.id;move.until=s.time+5;
  s.siren.grabs.push({owner:boss.id,victim:enemy.id,at:s.time,crushAt:s.time+3.2,releaseAt:move.until});s.time+=3;
  assert.ok(s.siren.armored(boss));s.damage(boss,boss.hp+1,enemy.id,'direct');assert.equal(s.siren.grabs.length,0);assert.equal(boss.hp,0);
 }finally{s.dispose();}
});
