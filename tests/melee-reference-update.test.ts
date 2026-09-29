import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {v,idleInput,type SpecId} from '../src/shared/config.js';
import {DAGGER_THROW} from '../src/shared/standard-melee.js';
import {attackTravel,STAGGER_ESCAPE_SECONDS} from '../src/shared/souls-combat.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {buildWeapon} from '../src/client/weapon-visuals.js';
import {StandardMeleeEffects} from '../src/client/standard-melee-effects.js';
import {SnapshotBuffer} from '../src/client/snapshots.js';
await initPhysics();
function arena(spec:SpecId='daggers',distance=1.6){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','Attacker','melee')!,b=s.addPlayer('b','Target')!;a.spec=spec;s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(80,1,80),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,distance),true);a.input=idleInput();b.input=idleInput();b.hp=b.stats.hp=1000;
 for(let i=0;i<15;i++)s.step();return {s,a,b};
}
function pose(p:PlayerView,time:number){const r=createCharacterRig('#79ccef');buildWeapon(r.weapon,p.spec);if(p.spec==='daggers')buildWeapon(r.offWeapon,p.spec,true);animateCharacter(r,p,time,1/60);r.group.updateMatrixWorld(true);return r;}
test('impact blends from the interrupted swing and repeated hits blend from the current recoil',()=>{
 const {s,a}=arena('sword',30);try{
  s.primary(a);const p=s.snapshot().players[0],at=p.lastAttack+.12,r=pose(p,at-.01);
  const nodes=[r.body,r.chest,r.head,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftLeg,r.rightLeg,r.weapon];
  const before=nodes.map(n=>n.quaternion.clone()),position=r.body.position.clone();
  const h={kind:'hit' as const,at,until:at+.5,yaw:0,direction:v(0,0,-1),point:v(.2,.1,0),zone:'torso' as const,turn:.45,strength:1};
  const struck={...p,lastAttack:-10,swingAt:undefined,swingUntil:undefined,attackCommitment:undefined,hitReaction:h,hitStunUntil:h.until};
  animateCharacter(r,struck,at,1/120);
  nodes.forEach((n,i)=>assert.ok(n.quaternion.angleTo(before[i])<1e-6,'no jump at contact'));assert.ok(r.body.position.distanceTo(position)<1e-8);
  for(let t=at+1/120;t<=at+.15;t+=1/120)animateCharacter(r,struck,t,1/120);
  assert.ok(r.body.quaternion.angleTo(before[0])>.2,'body visibly recoils');
  const prior=nodes.map(n=>n.quaternion.clone()),second={...struck,hitReaction:{...h,at:at+.16,direction:v(1,0,0),turn:-.35}};
  animateCharacter(r,second,at+.16,1/120);nodes.forEach((n,i)=>assert.ok(n.quaternion.angleTo(prior[i])<1e-6,'second hit starts from current recoil'));
  assert.equal(second.hitStunUntil,h.until,'visual reaction does not extend stun');
 }finally{s.dispose();}
});
test('hit recoil builds over multiple frames and settles before its existing deadline',()=>{
 const {s}=arena();try{
  const p=s.snapshot().players[0],h={kind:'hit' as const,at:2,until:2.5,yaw:0,direction:v(0,0,-1),point:v(0,.1,0),zone:'torso' as const,turn:0,strength:1};
  const struck={...p,lastAttack:-10,hitReaction:h,hitStunUntil:h.until};
  const early=pose(struck,2.015),middle=pose(struck,2.05),peak=pose(struck,2.095),end=pose(struck,2.499);
  assert.ok(Math.abs(early.body.rotation.x)<Math.abs(middle.body.rotation.x));assert.ok(Math.abs(middle.body.rotation.x)<Math.abs(peak.body.rotation.x));
  assert.ok(Math.abs(peak.body.rotation.x)>.3);assert.ok(Math.abs(end.body.rotation.x)<.01,'torso settles');
  const neutral=pose({...struck,hitReaction:undefined,hitStunUntil:0},2.499);assert.ok(end.rightArm.quaternion.angleTo(neutral.rightArm.quaternion)<.01,'arm settles instead of holding a fixed pose');
 }finally{s.dispose();}
});
test('two actual dagger swings land during one fixed stun at slow, normal and fast attack rates',()=>{
 for(const rate of [.75,1,1.6,2.5]){const {s,a,b}=arena();try{
  a.stats.attackRate=rate;const hits:number[]=[];let deadline=0,prior=b.hp;
  for(let i=0;i<150&&hits.length<2;i++){s.setInput(a.id,{...idleInput(),seq:i+1,attack:true});s.step();if(b.hp<prior){hits.push(s.time);if(hits.length===1)deadline=b.hitStunUntil!;else assert.equal(b.hitStunUntil,deadline,'second hit cannot extend stun');prior=b.hp;}}
  assert.equal(hits.length,2,`rate ${rate}`);assert.ok(hits[1]<deadline,`second hit precedes recovery at rate ${rate}`);assert.equal(100-a.stamina,32);assert.equal(WEAPONS.daggers.secondaryCost,30);
  const immune=b.staggerImmuneUntil!;assert.ok(immune-deadline>=STAGGER_ESCAPE_SECONDS-1e-8);
  for(let i=0;i<4;i++){s.time+=.01;const hp=b.hp;s.directHit(a,b,b.body.translation(),v(0,0,1),1,0,'repeat-'+i);s.standardMelee.primaryImpact(a,b,hp);assert.equal(b.hitStunUntil,deadline);assert.equal(b.staggerImmuneUntil,immune);}
  s.time=deadline+.01;s.setInput(a.id,{...idleInput(),seq:200});s.setInput(b.id,{...idleInput(),seq:200,x:1});s.step();assert.ok(b.move.x>0,'victim can move in escape window');
 }finally{s.dispose();}}
});
test('blocked melee cancels the swing and carries a parried recoil through its exact stun window',()=>{
 for(const spec of ['sword','daggers','greatsword','shield'] as SpecId[]){const {s,a,b}=arena(spec);try{
  s.primary(a);s.beginBlock(b);s.directHit(a,b,b.body.translation(),v(0,0,1),WEAPONS[spec].damage,WEAPONS[spec].impulse,'blocked');
  assert.equal(b.hp,1000);assert.equal(a.swing,null);assert.equal(a.attackCommitment,undefined);assert.equal(a.hitReaction?.kind,'parry');assert.equal(a.hitReaction!.until,a.hitStunUntil);
  const p=s.snapshot().players[0],h=p.hitReaction!,r=pose(p,h.at+(h.until-h.at)*.25),mid=pose(p,h.at+(h.until-h.at)*.55);
  assert.ok(r.rightHand.getWorldPosition(new T.Vector3()).y>mid.rightHand.getWorldPosition(new T.Vector3()).y+.08,'knocked-up weapon settles into exposed guard');
  assert.ok(Math.abs(mid.body.rotation.y)>.2);for(const f of [r.leftFoot,r.rightFoot])assert.ok(f.getWorldPosition(new T.Vector3()).y>-.81);
  assert.notEqual(p.hitReaction,a.hitReaction);p.hitReaction!.direction.x=99;assert.notEqual(a.hitReaction!.direction.x,99);
 }finally{s.dispose();}}
});
test('hit location and incoming direction determine body turn, head snap, arm recoil and leg buckle',()=>{
 const {s,a,b}=arena();try{const center=b.body.translation(),sample=(x:number,y:number,dir=v(0,0,-1))=>{
  s.damage(b,10,a.id,'direct',dir,0,v(center.x+x,center.y+y,center.z));s.standardMelee.stun(b,.4);return {view:s.snapshot().players[1],rig:pose(s.snapshot().players[1],s.time+.08)};
 };
 const left=sample(.18,.1),right=sample(-.18,.1);assert.ok(left.rig.body.rotation.y>0);assert.ok(right.rig.body.rotation.y<0);
 const front=sample(0,.1),back=sample(0,.1,v(0,0,1));assert.ok(front.rig.body.rotation.x<0);assert.ok(back.rig.body.rotation.x>0);
 const head=sample(0,.64),leg=sample(-.15,-.5),arm=sample(.36,.1);assert.equal(head.view.hitReaction!.zone,'head');assert.equal(leg.view.hitReaction!.zone,'legs');assert.equal(arm.view.hitReaction!.zone,'left-arm');
 assert.ok(Math.abs(head.rig.head.rotation.x)>Math.abs(front.rig.head.rotation.x)+.1);assert.ok(leg.rig.body.position.y<front.rig.body.position.y-.04);assert.ok(arm.rig.leftElbow.rotation.x>arm.rig.rightElbow.rotation.x);
 const h=b.hitReaction;s.damage(b,2,a.id,'burn');assert.equal(b.hitReaction,h,'damage over time does not restart impact');s.resetRound();assert.equal(b.hitReaction,undefined);
 }finally{s.dispose();}
});
test('remote reactions start on the render clock and are not shown from a future packet',()=>{
 const {s,a,b}=arena('sword');try{const first=s.snapshot();first.time=1;first.tick=60;s.time=1.1;s.damage(b,10,a.id,'direct',v(0,0,-1),0,b.body.translation());s.standardMelee.stun(b,.4);const next=s.snapshot();next.tick=66;const buffer=new SnapshotBuffer();buffer.push(first,1000);buffer.push(next,1100);
 let frame=buffer.sample(next,a.id,1100);assert.ok(frame.time<1.1);assert.equal(frame.players[1].hitReaction,undefined);assert.equal(frame.players[1].hitStunUntil,first.players[1].hitStunUntil);
 for(let now=1116;now<1300;now+=16)frame=buffer.sample(next,a.id,now);assert.ok(frame.time>=1.1);assert.equal(frame.players[1].hitReaction?.at,1.1);assert.equal(frame.players[1].hitStunUntil,b.hitStunUntil);
 }finally{s.dispose();}
});
test('dagger throw follows the same analytic gravity step as projectiles and visibly tumbles about its center',()=>{
 const {s,a,b}=arena('daggers',35);try{
  a.body.setTranslation(v(0,12,0),true);a.input.pitch=.1;a.stats.projectileGravity=1.4;s.secondary(a);s.time=a.meleeAction!.releaseAt;s.standardMelee.step(a);const shot=[...s.projectiles.values()][0];assert.equal(shot.gravity,DAGGER_THROW.gravity*1.4);
  const start={...shot.pos},initial={...shot.vel};for(let n=0;n<20;n++){s.time+=1/120;s.stepProjectiles(1/120);}const t=20/120;
  assert.ok(Math.abs(shot.pos.y-(start.y+initial.y*t-.5*shot.gravity*t*t))<1e-6);assert.ok(Math.abs(shot.pos.z-start.z-initial.z*t)<1e-6);assert.ok(shot.vel.y<initial.y);
  const scene=new T.Scene(),fx=new StandardMeleeEffects(scene),snap=s.snapshot();fx.update(snap);const mesh=scene.getObjectByName('thrown-dagger')!,spin=mesh.children[0],first=spin.quaternion.clone();snap.time+=.02;fx.update(snap);assert.ok(first.angleTo(spin.quaternion)>.5);assert.equal(spin.children[0].position.z,-.44);
  for(let n=0;n<300&&s.projectiles.has(shot.id);n++){s.time+=1/60;s.stepProjectiles(1/60);}assert.equal(s.projectiles.has(shot.id),false);assert.ok(Math.hypot(shot.pos.x-start.x,shot.pos.z-start.z)<=DAGGER_THROW.maxTravel+.01);assert.equal(b.hp,1000);fx.update(s.snapshot());assert.equal(scene.getObjectByName('thrown-dagger'),undefined);
 }finally{s.dispose();}
});
test('authored root travel does not drag the planted lead foot during contact and recovery',()=>{
 for(const spec of ['sword','daggers','shield','greatsword'] as SpecId[]){const {s,a}=arena(spec,30);try{s.primary(a);const p=s.snapshot().players[0],c=p.attackCommitment!,w=WEAPONS[spec],start=p.lastAttack+w.windup+.16*.65,end=p.lastAttack+w.windup+.16;
  const footAt=(time:number)=>{const r=pose(p,time);r.group.position.z=attackTravel(c,time);r.group.updateMatrixWorld(true);return r.leftFoot.getWorldPosition(new T.Vector3());};
  const first=footAt(start),last=footAt(end);assert.ok(first.distanceTo(last)<.025,`${spec} plant drift ${first.distanceTo(last)}`);
 }finally{s.dispose();}}
});
test('every combo keeps ankles and blade tips above the floor throughout attack and recovery',()=>{
 for(const spec of ['sword','daggers','shield','greatsword'] as SpecId[]){const {s,a}=arena(spec,30);try{s.primary(a);const base=s.snapshot().players[0],w=WEAPONS[spec],count=spec==='daggers'?4:spec==='sword'?3:2;
  for(let index=1;index<=count;index++)for(let age=0;age<base.attackCommitment!.until-base.lastAttack;age+=1/90){const r=pose({...base,attackIndex:index},base.lastAttack+age);for(const f of [r.leftFoot,r.rightFoot])assert.ok(f.getWorldPosition(new T.Vector3()).y>-.805,`${spec} ${index} foot at ${age}`);assert.ok(r.weapon.getObjectByName('blade-tip')!.getWorldPosition(new T.Vector3()).y>-.80,`${spec} ${index} blade at ${age}`);}
 }finally{s.dispose();}}
});
test('looking around during a committed attack cannot reverse the captured parry recoil',()=>{
 const {s,a,b}=arena('sword');try{s.primary(a);a.input.yaw=Math.PI;s.beginBlock(b);s.directHit(a,b,b.body.translation(),v(0,0,1),30,300,'parried');assert.equal(a.hitReaction!.yaw,0);assert.equal(a.input.yaw,Math.PI,'camera aim stays responsive');
 }finally{s.dispose();}
});
