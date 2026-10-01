import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {WEAPONS,statsFor,eligibleChips,CHIPS} from '../src/shared/content.js';
import {CONFIG as C,idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
import {committed,rolling,rollInvulnerable,rollProfile,usesRoll,STAGGER_ESCAPE_SECONDS} from '../src/shared/souls-combat.js';
import {Predictor} from '../src/client/prediction.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {buildWeapon} from '../src/client/weapon-visuals.js';
import {SnapshotBuffer} from '../src/client/snapshots.js';
await initPhysics();
const standard:SpecId[]=['daggers','sword','shield','greatsword'];
function arena(spec:SpecId='sword'){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','A',WEAPONS[spec].class,null,'#80bdff',{},true)!,b=s.addPlayer('b','B')!;
 a.spec=spec;s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(100,1,100),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(30,.9,30),true);b.hp=b.stats.hp=10000;
 for(let i=0;i<20;i++)s.step();return {s,a,b};
}
function input(s:Simulation,p:Player,patch:Partial<Input>={}){s.setInput(p.id,{...idleInput(),seq:p.input.seq+1,...patch});}
function advance(s:Simulation,seconds:number){for(let i=0;i<Math.ceil(seconds*60);i++)s.step();}
test('standard weapon costs scale with weight and failed attacks spend nothing',()=>{
 const costs=[];for(const spec of standard){const {s,a}=arena(spec);try{const before=a.stamina;s.primary(a);costs.push(before-a.stamina);assert.equal(before-a.stamina,WEAPONS[spec].cost);assert.ok(committed(a,s.time));assert.equal(a.move.z,0);s.primary(a);assert.equal(a.stamina,before-WEAPONS[spec].cost);s.resetRound();s.phase='fight';a.stamina=WEAPONS[spec].cost-.1;s.primary(a);assert.equal(a.swing,null);assert.equal(a.attackCommitment,undefined);}finally{s.dispose();}}
 assert.deepEqual(costs,[16,30,36,44]);
});
test('WASD stops through attack recovery while the authored step advances the body',()=>{
 const {s,a}=arena();try{input(s,a,{z:1});advance(s,.3);const start={...a.body.translation()};s.primary(a);input(s,a,{x:1,z:1,jump:true,dash:true});assert.equal(s.beginBlock(a),false);while(committed(a,s.time)){s.step();if(committed(a,s.time)){assert.equal(a.move.x,0);assert.equal(a.move.z,0);assert.equal(a.roll,undefined);assert.ok(a.body.translation().y<1.05);}}
 assert.ok(Math.abs(a.body.translation().x-start.x)<.02);assert.ok(Math.abs(a.body.translation().z-start.z-.65)<.03);s.step();assert.ok(a.move.x>0);assert.ok(a.move.z>0);}finally{s.dispose();}
});
test('melee hitstun cannot be refreshed by repeated hits or another standard weapon',()=>{
 const {s,a,b}=arena('daggers');try{b.body.setTranslation(v(0,.9,1),true);const hit=()=>{const before=b.hp;s.directHit(a,b,b.body.translation(),v(0,0,1),1,0,'hit-'+s.time);s.standardMelee.primaryImpact(a,b,before);};hit();const stun=b.hitStunUntil!,immune=b.staggerImmuneUntil!;assert.ok(stun>s.time);assert.ok(immune-stun>=STAGGER_ESCAPE_SECONDS-1e-8);s.time+=.08;a.spec='greatsword';hit();assert.equal(b.hitStunUntil,stun);s.time=stun+.03;hit();assert.equal(b.hitStunUntil,stun);input(s,b,{x:1});s.step();assert.ok(b.move.x>0,'escape movement is available');s.time=immune+.01;hit();assert.ok(b.hitStunUntil!>s.time);}finally{s.dispose();}
});
test('normal shield hits keep knockdown, and cannot restart it during recovery',()=>{
 const {s,a,b}=arena('shield');try{const hp=b.hp;s.directHit(a,b,b.body.translation(),v(),1,0,'first');s.standardMelee.primaryImpact(a,b,hp);assert.ok(b.knockdown);const at=b.knockdown.at;for(let n=0;n<5;n++){s.time+=.03;const before=b.hp;s.directHit(a,b,b.body.translation(),v(),1,0,'repeat');s.standardMelee.primaryImpact(a,b,before);}assert.equal(b.knockdown.at,at);assert.ok((b.knockdown.juggleHits||0)<=2);}finally{s.dispose();}
});
test('all classes roll from stamina; light weapons travel farther and faster',()=>{
 let light=0,heavy=0;for(const spec of Object.keys(WEAPONS) as SpecId[]){if(!usesRoll(spec))continue;const {s,a}=arena(spec);try{const profile=rollProfile(spec,a.stats),start={...a.body.translation()},mana=a.mana,ammo=a.ammo;input(s,a,{dash:true,z:1});s.step();assert.ok(a.roll,spec);assert.equal(a.stamina,100-profile.cost);assert.equal(a.mana,mana);assert.equal(a.ammo,ammo);const end=a.roll!.until;while(s.time+1/60<end){input(s,a,{x:1,z:1,attack:true,secondary:true,jump:true,block:true});s.step();assert.equal(a.move.x,0);assert.equal(a.swing,null);assert.equal(a.blockTime,0);}s.time=end-1/120;s.updatePlayer(a,1/120);s.physics(1/120);const distance=a.body.translation().z-start.z;assert.ok(Math.abs(distance-profile.distance)<.06,`${spec}: ${distance} vs ${profile.distance}`);assert.ok(Math.abs(a.body.translation().x-start.x)<.01);if(spec==='daggers')light=profile.distance/profile.duration;if(spec==='greatsword')heavy=profile.distance/profile.duration;}finally{s.dispose();}}
 assert.ok(light>heavy*1.7);
});
test('roll startup and recovery take damage; only its middle avoids attacks and their procs',()=>{
 const {s,a,b}=arena('pistol');try{input(s,a,{dash:true});s.step();const roll={...a.roll!};for(const [offset,protectedHit] of [[.02,false],[.14,true],[.34,false]] as const){a.hp=100;a.roll={...roll};s.time=roll.at+offset;const dealt=s.damage(a,5,b.id,'direct',v(),0);assert.equal(dealt,protectedHit?0:5);}
 a.roll={...roll};s.time=roll.at+.14;a.hp=100;s.directHit(b,a,a.body.translation(),v(),10,100,'blocked-by-roll');assert.equal(a.hp,100);assert.equal(b.hits,0);assert.equal(a.ext.z,0);assert.equal(s.damage(a,3,b.id,'burn'),3,'existing burning keeps ticking');a.roll={...roll};assert.equal(s.damage(a,3,null,'overtime'),3);a.roll={...roll};s.damage(a,100000,null,'fall');assert.equal(a.hp,0);}finally{s.dispose();}
});
test('roll requires a fresh press, ground, enough stamina, and recovery before regeneration',()=>{
 const {s,a}=arena('fire');try{a.stamina=1;input(s,a,{dash:true});s.step();assert.equal(a.roll,undefined);a.stamina=100;input(s,a);s.step();a.grounded=false;input(s,a,{dash:true});s.beginRoll(a);assert.equal(a.roll,undefined);a.grounded=true;input(s,a);s.beginRoll(a);input(s,a,{dash:true});s.step();const spent=a.stamina,at=a.roll!.at;advance(s,.6);assert.equal(a.stamina,spent);advance(s,.5);assert.ok(a.stamina>spent);assert.equal(a.roll!.at,at,'holding Shift never repeats a roll');input(s,a);s.step();input(s,a,{dash:true});s.step();assert.ok(a.roll!.at>at);}finally{s.dispose();}
});
test('roll chips stack within partial-invincibility caps and only roll users draft them',()=>{
 assert.equal(new Set(CHIPS.map(c=>c.id)).size,CHIPS.length);const chips={'long-roll':3,'quick-tuck':3,'efficient-roll':3,'ghost-timing':2,'tumble-engine':1,'reserve-cell':3},stats=statsFor(chips),base=rollProfile('daggers',statsFor({})),up=rollProfile('daggers',stats);
 assert.ok(up.distance>base.distance);assert.ok(up.duration<base.duration);assert.ok(up.cost<base.cost);assert.ok(up.iframeStart>0);assert.ok(up.iframeSeconds/up.duration<=.6+1e-8);assert.ok(up.duration-up.iframeStart-up.iframeSeconds>=.09-1e-8);assert.equal(stats.maxStamina,175);
 for(const spec of Object.keys(WEAPONS) as SpecId[]){const pool=eligibleChips(spec,{});assert.equal(pool.some(c=>c.id==='long-roll'),usesRoll(spec));assert.ok(pool.some(c=>c.id==='endurance'));}
});
test('Arcade keeps Demon Step and developer primary hits charge per step without locking mobility',()=>{
 for(const spec of ['ansatsuken','emperor'] as SpecId[]){const {s,a}=arena(spec);try{input(s,a,{z:1});s.primary(a);assert.equal(a.attackCommitment,undefined);assert.equal(a.stamina,spec==='ansatsuken'?96:97);s.step();assert.ok(a.move.z>0);if(spec==='ansatsuken'){input(s,a,{dash:true});s.step();assert.equal(a.roll,undefined);assert.ok(a.dashTime>0);assert.ok(s.events.some(e=>e.type==='demon-step'));}}finally{s.dispose();}}
});
test('roll direction, stamina authorization and committed attacks reconcile with the shared physics',()=>{
 for(const spec of ['sword','pistol'] as SpecId[]){const {s,a}=arena(spec),predictor=new Predictor();try{let snap=s.snapshot(),me=snap.players[0];predictor.reconcile(snap,me);for(let n=1;n<=75;n++){const i={...idleInput(),seq:n,x:n<40?0:1,z:1,dash:n===1,attack:spec==='sword'&&n===55};predictor.step(i,1/60,me);s.setInput(a.id,i);s.step();const delta=new T.Vector3().copy(predictor.pos).distanceTo(a.body.translation());assert.ok(delta<.035,`${spec} tick ${n}: ${delta}`);if(n%6===0){snap=JSON.parse(JSON.stringify(s.snapshot()));me=snap.players[0];predictor.reconcile(snap,me);}}}finally{predictor.dispose();s.dispose();}}
});
test('round reset clears movement locks, roll immunity and press state',()=>{
 const {s,a}=arena();try{input(s,a,{dash:true});s.step();s.resetRound();assert.equal(a.roll,undefined);assert.equal(a.rollHeld,false);assert.equal(a.attackCommitment,undefined);assert.equal(a.staggerImmuneUntil,0);assert.equal(a.stamina,100);}finally{s.dispose();}
});
test('roll poses tuck knees and elbows, keep all bone lengths finite and complete a full turn',()=>{
 const {s,a}=arena('sword');try{input(s,a,{dash:true,z:1});s.step();const p=s.snapshot().players[0],r=createCharacterRig(a.color);buildWeapon(r.weapon,'sword');const rolls=[];for(const u of [.05,.2,.4,.6,.8,.95]){animateCharacter(r,p,p.roll!.at+u*(p.roll!.until-p.roll!.at),1/60);r.group.updateMatrixWorld(true);r.group.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));if(u===.4){assert.ok(r.leftKnee.rotation.x>1.8);assert.ok(r.leftElbow.rotation.x< -1.3);}rolls.push(r.body.quaternion.clone());}assert.ok(rolls[0].angleTo(rolls[2])>1);}finally{s.dispose();}
});
test('remote roll playback selects the roll on the interpolation clock for every class',()=>{
 const {s,a,b}=arena('pistol');try{b.spec='fire';const first=s.snapshot();input(s,b,{dash:true});s.step();const second=s.snapshot(),buffer=new SnapshotBuffer();first.time=1;first.tick=60;second.time=1.15;second.tick=69;second.players[1].roll={...second.players[1].roll!,at:1.1,until:1.6,invulnerableAt:1.175,invulnerableUntil:1.355};buffer.push(first,1000);buffer.push(second,1150);const frame=buffer.sample(second,a.id,1150);assert.ok(frame.time<1.1);assert.equal(frame.players[1].roll,undefined);}finally{s.dispose();}
});
