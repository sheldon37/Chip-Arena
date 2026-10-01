import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,sanitizeInput} from '../src/shared/simulation.js';
import {idleInput,v} from '../src/shared/config.js';
import {availableClasses,preferredWeapon,weaponInClass,botWeapon} from '../src/shared/lobby-presentation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {effectiveIntro,introDuration} from '../src/shared/intros.js';
import {SIREN_TIMING as T} from '../src/shared/siren-contract.js';
import {ROTATION_MAPS} from '../src/shared/maps.js';
await initPhysics();

function arena(){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','Siren','dev',null,'#ffffff',{},true)!,b=s.addPlayer('b','Target','melee')!,c=s.addPlayer('c','Observer')!;
 a.spec='siren';s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);
 s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,2),true);c.body.setTranslation(v(20,1,20),true);a.input=idleInput();s.world.step();
 const tick=(seconds:number)=>{for(let i=0;i<Math.ceil(seconds*60);i++){for(const p of s.players.values())p.inputAt=s.time;s.step();}};
 const settle=()=>{tick(.15);s.events=[];};settle();
 return {s,a,b,c,tick};
}

test('DEV category requires verified DEV, preserves item grants and old saved weapons',()=>{
 assert.deepEqual(availableClasses(false),['ranged','magic','melee']);assert.equal(availableClasses(true).at(-1),'dev');
 assert.equal(preferredWeapon('siren','dev',false),'pistol');assert.equal(preferredWeapon('siren','dev',true),'siren');
 for(const spec of ['emperor','ansatsuken','leet'] as const)assert.equal(weaponInClass(spec,'dev',true),true);
 const grant={developer:false,unlocks:['weapon:siren']};assert.equal(preferredWeapon('siren','melee',grant),'siren');assert.equal(weaponInClass('siren','dev',grant),false);
 assert.equal(botWeapon('siren'),'random');
 const s=new Simulation();try{const p=s.addPlayer('p','Guest','dev')!;assert.equal(p.class,'ranged');assert.equal(s.setClass('p','dev'),false);}finally{s.dispose();}
});

test('one connected Siren across selection, debug and saved preferences; disconnect frees the slot',()=>{
 const s=new Simulation({sandbox:true});try{
  const a=s.addPlayer('a','A','dev',null,'#ffffff',{preferredSpec:'siren'},true)!,b=s.addPlayer('b','B','dev',null,'#ffffff',{preferredSpec:'siren'},true)!;
  assert.equal(a.spec,'siren');assert.notEqual(b.spec,'siren');s.phase='specialization';
  assert.equal(s.selectSpec('b','siren'),false);assert.equal(s.debug({action:'spec',id:'siren'},'b'),false);
  s.disconnect('a');assert.equal(s.selectSpec('b','siren'),true);assert.equal(b.spec,'siren');
 }finally{s.dispose();}
});

test('host command path rejects forged DEV class and duplicate Siren',()=>{
 const r=new HostedRoom('ABCDEF',{name:'A',class:'dev',color:'#ffffff'},false,()=>{},6,true);
 try{r.join({name:'B',class:'dev'},true,'b');r.join({name:'Guest',class:'dev',developer:true},false,'g');assert.equal(r.sim.players.get('g')!.class,'ranged');
  r.sim.phase='specialization';assert.equal(r.receive('host',{type:'spec',id:'siren'}),true);assert.equal(r.receive('b',{type:'spec',id:'siren'}),false);assert.equal(r.receive('g',{type:'spec',id:'siren'}),false);
 }finally{r.dispose();}
});

test('Siren intro is forced last; song starts on its entrance and survives rounds/sets',()=>{
 const s=new Simulation({map:'neon',gameMode:'teams',randomMapEachSet:true});try{
  const a=s.addPlayer('a','Boss','dev',null,'#ffffff',{overrideWeaponIntro:true,intro:'pulse-wave'},true)!,b=s.addPlayer('b')!;a.spec='siren';
  s.beginIntros();assert.deepEqual(s.intro!.players,['b','a']);assert.equal(effectiveIntro(a),'dev-siren');assert.equal(s.options.map,'siren');assert.equal(s.options.gameMode,'ffa');
  s.updateBossSong();assert.equal(s.bossSong,undefined);s.time=introDuration(effectiveIntro(b));s.updateBossSong();const epoch=s.bossSong!.startedAt;assert.equal(epoch,s.time);
  s.resetRound();s.countdown();assert.equal(s.bossSong!.startedAt,epoch);s.beginNextSet();assert.equal(s.bossSong!.startedAt,epoch);assert.equal(s.options.map,'siren');
  assert.equal(s.snapshot().bossSong!.startedAt,epoch);s.returnLobby();assert.equal(s.bossSong,undefined);assert.equal(s.options.map,'neon');assert.equal(s.options.gameMode,'teams');
 }finally{s.dispose();}
});

test('skipped intros start the song at countdown; ordinary matches keep their map and intro order',()=>{
 const s=new Simulation({skipIntros:true}),normal=new Simulation({map:'neon'});try{
  s.addPlayer('a','Boss','dev',null,'#ffffff',{preferredSpec:'siren'},true);s.addPlayer('b');s.beginIntros();assert.equal(s.phase,'countdown');assert.equal(s.bossSong?.startedAt,s.time);
  normal.addPlayer('z');normal.addPlayer('a');normal.beginIntros();assert.deepEqual(normal.intro!.players,['z','a']);assert.equal(normal.options.map,'neon');assert.equal(normal.snapshot().siren,undefined);assert.equal(normal.bossSong,undefined);
  assert.ok(ROTATION_MAPS.every(m=>String(m.id)!=='siren'));normal.configureLobby({map:'siren'});assert.notEqual(normal.options.map,'siren');
 }finally{s.dispose();normal.dispose();}
});

test('input sanitization and edge-triggered mode/abilities retain cooldowns across toggles',()=>{
 assert.equal(sanitizeInput({...idleInput(),sirenToggle:'true',sirenAbility:5})!.sirenToggle,false);assert.equal(sanitizeInput({...idleInput(),sirenAbility:'1'})!.sirenAbility,undefined);
 const {s,a,tick}=arena();try{a.input.sirenToggle=true;tick(.8);assert.equal(a.siren!.mode,'ranged');assert.equal(s.events.filter(e=>e.type==='siren-mode').length,1);
  a.input.sirenToggle=false;tick(.05);a.input.sirenAbility=2;tick(1.2);assert.equal(s.events.filter(e=>e.type==='siren-cast'&&e.message==='eruption').length,1);const ready=a.siren!.ready!.eruption;
  a.input.sirenAbility=undefined;a.input.sirenToggle=true;tick(.4);assert.equal(a.siren!.mode,'melee');assert.equal(a.siren!.ready!.eruption,ready);
 }finally{s.dispose();}
});

test('claw swipe hits once after windup and alternates hands; rending claw knocks down',()=>{
 const {s,a,b,tick}=arena();try{assert.equal(s.siren.primary(a),true);tick(.08);assert.equal(b.hp,100);tick(.4);assert.equal(b.hp,91);assert.equal(s.events.find(e=>e.type==='siren-swipe')!.amount,1);
  assert.equal(s.siren.primary(a),true);tick(.5);assert.equal(b.hp,82);assert.equal(s.events.filter(e=>e.type==='siren-swipe').at(-1)!.amount,-1);
  assert.equal(s.siren.start(a,'rend'),true);tick(T.rend.windup+.04);assert.equal(b.hp,60);assert.ok(b.knockdown);
 }finally{s.dispose();}
});

test('slam launches; eruption warns for a second before dealing damage',()=>{
 for(const id of ['slam','eruption'] as const){const {s,a,b,tick}=arena();try{
  if(id==='eruption')s.siren.state(a).mode='ranged';assert.equal(s.siren.start(a,id),true);if(id==='eruption')a.siren!.move!.aim=v(0,0,2);
  tick(id==='slam'?.3:.5);assert.equal(b.hp,100);if(id==='eruption'){assert.equal(s.snapshot().siren!.eruptions.length,1);tick(.6);assert.equal(b.hp,100);tick(.3);}else tick(.12);
  assert.equal(b.hp,id==='slam'?84:72);assert.ok(b.juggle);assert.ok(b.body.linvel().y>0||b.ext.y>0);
 }finally{s.dispose();}}
});

test('Last Laugh roots, catches and kills with a head gib; killing the owner releases the victim',()=>{
 for(const killOwner of [false,true]){const {s,a,b,tick}=arena();try{
  assert.equal(s.siren.start(a,'laugh'),true);a.input.x=1;const x=a.body.translation().x;tick(1.8);assert.ok(Math.abs(a.body.translation().x-x)<.02);assert.equal(s.siren.grabs.length,0);
  tick(.5);assert.equal(s.siren.grabs.length,1);assert.equal(b.hp,100);b.input.attack=true;assert.equal(s.beginBlock(b),false);assert.equal(s.beginRoll(b),false);
  if(killOwner){s.damage(a,a.hp+1,b.id,'test');assert.equal(s.siren.grabs.length,0);assert.equal(b.hp,100);assert.equal(b.body.gravityScale(),1);}
  else{tick(3.3);assert.equal(b.hp,0);assert.equal(b.death!.effect,'siren-crush');assert.equal(b.death!.gibPart,'head');assert.equal(b.death!.source,'a');}
 }finally{s.dispose();}}
});

test('Last Laugh whiffs through invulnerability and honors recovery',()=>{
 const {s,a,b,tick}=arena();try{s.siren.start(a,'laugh');tick(1.9);b.roll={at:s.time,until:s.time+2,invulnerableAt:s.time,invulnerableUntil:s.time+2,direction:v(),distance:0};tick(.6);
  assert.equal(s.siren.grabs.length,0);assert.equal(b.hp,100);assert.ok(s.events.some(e=>e.type==='siren-whiff'));assert.equal(s.siren.primary(a),false);
 }finally{s.dispose();}
});

test('hellfire patches are capped and burn only inside; reset clears all world effects',()=>{
 const {s,a,b,tick}=arena();try{for(let n=0;n<15;n++)s.siren.patch(a,v(n?20:0,0,2));assert.equal(s.siren.patches.length,12);assert.ok(s.siren.patches.every(f=>f.pos.x===20));s.siren.patch(a,v(0,0,2));tick(.3);assert.ok(b.hp<100);
  b.body.setTranslation(v(10,1,0),true);const hp=b.hp;tick(.5);assert.equal(b.hp,hp);s.resetRound();assert.equal(s.siren.patches.length,0);
 }finally{s.dispose();}
});

test('rain is seeded and consists of individually telegraphed projectiles',()=>{
 const one=arena(),two=arena();try{for(const {s,a} of [one,two]){s.siren.state(a).mode='ranged';s.siren.start(a,'rain');a.siren!.move!.aim=v(8,0,8);}
  one.tick(.75);two.tick(.75);const shots=(s:Simulation)=>[...s.projectiles.values()].filter(p=>p.siren?.kind==='drop');assert.ok(shots(one.s).length);assert.deepEqual(shots(one.s).map(p=>p.siren!.land),shots(two.s).map(p=>p.siren!.land));assert.ok(shots(one.s).every(p=>p.siren!.landAt!>one.s.time));
 }finally{one.s.dispose();two.s.dispose();}
});

test('Marked requires visible target, stays unique and destroys props but stops at foundation',()=>{
 const {s,a,b,tick}=arena();try{b.body.setTranslation(v(0,1,12),true);s.world.step();s.siren.state(a).mode='ranged';assert.equal(s.siren.start(a,'mark'),true);tick(2.5);const seeker=[...s.projectiles.values()].find(p=>p.siren?.kind==='seeker')!;assert.ok(seeker);assert.equal(Math.round(Math.hypot(seeker.vel.x,seeker.vel.y,seeker.vel.z)),3);
  tick(.6);a.siren!.ready!.mark=0;assert.equal(s.siren.start(a,'mark'),false);
  seeker.pos=v(0,1,4);s.addProp({id:'wood-test',pos:v(0,1,5),size:v(1,2,.3),material:'wood'});s.addProp({id:'cover-test',pos:v(0,1,7),size:v(2,3,.5),material:'foundation'});s.world.step();
  tick(1.2);assert.equal(s.props.has('wood-test'),false);assert.equal(s.props.has('cover-test'),true);assert.equal(s.projectiles.has(seeker.id),false);assert.equal(b.hp,100);assert.ok(s.events.some(e=>e.type==='siren-seeker-out'));
 }finally{s.dispose();}
});

test('Siren death dissipates and snapshot copies do not mutate the authority',()=>{
 const {s,a,b}=arena();try{s.siren.start(a,'rend');const snap=s.snapshot();snap.players.find(p=>p.id==='a')!.siren!.move!.hitAt=1000;assert.notEqual(a.siren!.move!.hitAt,1000);s.damage(a,a.hp+1,b.id,'test');assert.equal(a.death!.effect,'siren-dissipate');}finally{s.dispose();}
});

test('Shadow Rush hits once on its path, lays fire, and cannot pass foundation cover',()=>{
 const {s,a,b,tick}=arena();try{
  s.addProp({id:'rush-wall',pos:v(0,1,4),size:v(4,3,.4),material:'foundation'});s.world.step();assert.equal(s.siren.start(a,'rush'),true);tick(.7);
  assert.equal(s.events.filter(e=>e.type==='siren-hit'&&e.target==='b'&&e.message==='rush').length,1);assert.ok(b.hp<=86);assert.ok(s.siren.patches.some(p=>p.kind==='trail'));assert.ok(a.body.translation().z<3.5);
 }finally{s.dispose();}
});

test('Damnation uses an arcing shot and falloff; rolling targets avoid projectile contact',()=>{
 const {s,a,b,c}=arena();try{
  b.body.setTranslation(v(0,1,2),true);c.body.setTranslation(v(2,1,2),true);s.world.step();
  const shot=s.siren.spawn(a,'damnation',{},v(0,1,1.2))!;assert.equal(shot.gravity,8);shot.vel=v(0,0,13);s.siren.stepShot(shot,.1);assert.equal(s.projectiles.has(shot.id),false);assert.ok(b.hp<100);assert.ok(c.hp>b.hp&&c.hp<100);
  b.hp=100;b.roll={at:s.time,until:s.time+1,invulnerableAt:s.time,invulnerableUntil:s.time+1,direction:v(),distance:0};const bolt=s.siren.spawn(a,'hellfire',{},v(0,1,1.2))!;bolt.vel=v(0,0,20);s.siren.stepShot(bolt,.1);assert.equal(b.hp,100);assert.ok(s.projectiles.has(bolt.id));
 }finally{s.dispose();}
});
