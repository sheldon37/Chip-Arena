import {test} from 'node:test';
import assert from 'node:assert/strict';
import {v,idleInput,add,type SpecId} from '../src/shared/config.js';
import {Simulation,initPhysics,type Projectile} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {cosmetics,canUseKillEffect,DEFAULT_COSMETICS} from '../src/shared/cosmetics.js';
import {projectileBodyImpact,meleeBodyImpact,resolveBodyWound,deathMotion,MAX_BODY_WOUNDS,type BodyImpact,type BodyWound} from '../src/shared/body-damage.js';
import {bodyPartVelocity,bodyDeathPhase} from '../src/shared/body-ragdoll.js';
await initPhysics();
function wound(spec:SpecId='pistol',speed=52,lethal=true,point=v(0,.65,0),extra:Partial<BodyImpact>={}) {
  const impact={...projectileBodyImpact({spec,vel:v(0,0,speed),radius:.045}),...extra};
  return resolveBodyWound({id:'test',at:1,position:v(),yaw:0,point,direction:v(0,0,1),impact,damage:impact.referenceDamage,lethal});
}
function game(){
  const s=new Simulation({sandbox:true,skipIntros:true});
  const a=s.addPlayer('a','Shooter')!,b=s.addPlayer('b','Target')!;
  s.phase='fight';s.time=1;
  for(const p of [...s.props.values()])s.removeProp(p.id);
  a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,3),true);
  a.input=idleInput();b.input={...idleInput(),yaw:Math.PI};b.grounded=true;
  s.world.step();a.body.setLinvel(v(),true);b.body.setLinvel(v(),true);
  return {s,a,b};
}
test('ordinary bullet punctures; sniper opens a cavity; faster heavy bullet severs the struck head',()=>{
  assert.equal(wound().kind,'puncture');assert.equal(wound('rifle',150).kind,'cavity');
  const fast=wound('rifle',270);assert.equal(fast.kind,'sever');assert.equal(fast.destroyedPart,'head');
  assert.equal(wound('rifle',270,false).destroyedPart,undefined);
  assert.equal(wound('rifle',270,true,v(0,.1,0)).part,'spine');
  assert.equal(wound('rifle',270,true,v(0,.1,0)).kind,'cavity');
});
test('energy varies with mass and speed squared; momentum is independent and linear in speed',()=>{
  const a=wound('pistol',50),b=wound('pistol',100),c=wound('pistol',50,true,v(0,.65,0),{mass:.04});
  assert.equal(b.energy,a.energy*4);assert.equal(b.momentum,a.momentum*2);assert.equal(c.energy,a.energy*2);
});
test('cutting, thrusting and blunt impacts produce different lethal outcomes',()=>{
  const hit=(i:BodyImpact)=>wound('pistol',50,true,v(.3,.2,0),i);
  assert.equal(hit(meleeBodyImpact('sword')).kind,'sever');
  assert.equal(hit({...meleeBodyImpact('sword'),kind:'pierce'}).kind,'puncture');
  assert.equal(hit(meleeBodyImpact('shield')).kind,'crush');
  assert.equal(hit({...meleeBodyImpact('shield'),speed:1}).kind,'bruise');
});
test('local wound coordinates rotate with the victim and verified headshots override capsule shoulders',()=>{
  const impact=meleeBodyImpact('sword');
  for(const yaw of [0,.8,Math.PI]){
    const w=resolveBodyWound({id:'x',at:0,position:v(),yaw,point:v(.3*Math.cos(yaw),.2,-.3*Math.sin(yaw)),direction:v(0,0,1),impact,damage:30,lethal:true});
    assert.equal(w.part,'left-shoulder');assert.ok(Math.abs(w.point.x-.3)<1e-7);
  }
  const w=resolveBodyWound({id:'x',at:0,position:v(),yaw:0,point:v(0,.3,0),direction:v(0,0,1),impact:projectileBodyImpact({spec:'rifle',vel:v(0,0,150),radius:.045}),damage:87,lethal:true,headshot:true});
  assert.equal(w.part,'head');assert.ok(w.point.y>=.48);
});
test('finite bounded data even with degenerate internal impact inputs',()=>{
  const w=wound('rifle',Infinity,true,v(0,.65,0),{mass:Infinity,radius:NaN,transfer:NaN});
  for(const x of [w.radius,w.energy,w.momentum,w.speed,w.severity,...Object.values(w.point)])assert.ok(Number.isFinite(x));
});
test('kneel then forward fall only for upright grounded fast bullet head kills',()=>{
  const w=wound('rifle',150),base={wound:w,grounded:true,incapacitated:false,yaw:1,velocity:v()};
  const m=deathMotion(base);assert.equal(m.kind,'kneel-fall');
  assert.equal(bodyDeathPhase(m,.1).phase,'kneel');assert.equal(bodyDeathPhase(m,.5).phase,'fall');assert.equal(bodyDeathPhase(m,1).phase,'ragdoll');
  for(const x of [{grounded:false},{incapacitated:true},{velocity:v(10,0,0)},{wound:wound()},{wound:undefined}])assert.equal(deathMotion({...base,...x}).kind,'ragdoll');
  assert.equal(deathMotion({...base,wound:wound('rifle',270)}).kind,'kneel-fall');
});
test('ragdoll inherits knockback once; only detached part receives bounded extra impulse',()=>{
  const w=wound('rifle',270),m=deathMotion({wound:w,grounded:true,incapacitated:false,yaw:0,velocity:v(0,0,4)});
  assert.deepEqual(bodyPartVelocity(m,w,'spine',v(0,0,1)),v(0,0,4));
  const head=bodyPartVelocity(m,w,'head',v(0,0,1));assert.ok(head.z>4&&head.z<=14);
});
test('public impact ragdoll is default; explicit cosmetics and DEV authorization survive',()=>{
  assert.equal(DEFAULT_COSMETICS.killEffect,'impact-ragdoll');assert.equal(cosmetics({}).killEffect,'impact-ragdoll');
  assert.ok(canUseKillEffect(false,'impact-ragdoll'));
  assert.equal(cosmetics({killEffect:'dev-gibber'},false).killEffect,'impact-ragdoll');
  assert.equal(cosmetics({killEffect:'dev-gibber'},true).killEffect,'dev-gibber');
  assert.equal(cosmetics({killEffect:'confetti'}).killEffect,'confetti');
  assert.equal(cosmetics({killEffect:'voxel-burst'}).killEffect,'voxel-burst');
});
test('lethal potential damage is measured before remaining HP clips it',()=>{
  const values:number[]=[];
  for(const hp of [1,50]){const {s,a,b}=game();try{
    b.hp=hp;const i=projectileBodyImpact({spec:'rifle',vel:v(0,0,150),radius:.045});
    s.damage(b,87,a.id,'direct',v(0,0,1),350,add(b.body.translation(),v(0,.65,0)),false,false,true,[],i);
    assert.equal(b.hp,0);assert.equal(b.death?.effect,'impact-ragdoll');assert.equal(b.death?.wound?.kind,'cavity');
    assert.equal(b.death?.motion?.kind,'kneel-fall');values.push(b.death!.wound!.energy);
    const id=b.death!.id;s.damage(b,1000,a.id,'direct');assert.equal(b.death!.id,id);
  }finally{s.dispose();}}assert.equal(values[0],values[1]);
});
test('damage mitigation reduces wound energy without changing HP rules',()=>{
  const {s,a,b}=game();try{
    b.stats.damageTaken=.2;const i=projectileBodyImpact({spec:'rifle',vel:v(0,0,150),radius:.045});
    s.damage(b,58,a.id,'direct',v(0,0,1),0,add(b.body.translation(),v(0,.65,0)),false,false,false,[],i);
    assert.equal(b.hp,88.4);assert.equal(b.wounds?.[0].kind,'puncture');assert.equal(b.wounds?.[0].energy,36);
  }finally{s.dispose();}
});
test('blocked and allied hits create no wounds; nonlethal hits never remove a limb',()=>{
  for(const mode of ['block','ally','hit'] as const){const {s,a,b}=game();try{
    a.spec='sword';a.class='melee';if(mode==='block')b.blockTime=.5;if(mode==='ally'){s.options.gameMode='teams';a.team=b.team='red';}
    s.directHit(a,b,add(b.body.translation(),v(.3,.2,0)),v(0,0,1),30,300,'cut');
    if(mode==='hit'){assert.equal(b.hp,70);assert.equal(b.wounds?.[0].kind,'cut');assert.equal(b.wounds?.[0].destroyedPart,undefined);}
    else{assert.equal(b.hp,100);assert.equal(b.wounds,undefined);}
  }finally{s.dispose();}}
});
test('real projectile collisions retain rifle trauma after reflection by a sword user',()=>{
  for(const reflected of [false,true]){const {s,a,b}=game();try{
    a.spec=reflected?'sword':'rifle';a.class=reflected?'melee':'ranged';b.hp=1;
    const shot:Projectile={id:'shot',owner:a.id,spec:'rifle',pos:v(0,1.64,1),vel:v(0,0,150),damage:58,impulse:350,radius:.045,life:3,distanceLeft:100,gravity:0,direct:!reflected,reflections:reflected?1:0,attackId:'a',bounces:0,pierces:0,hit:new Set(),effectsUsed:false,homing:0};
    s.projectiles.set(shot.id,shot);s.stepProjectiles(.03);
    assert.equal(b.hp,0);assert.equal(b.death?.wound?.trauma,'bullet');assert.equal(b.death?.wound?.part,'head');assert.equal(b.death?.wound?.kind,'cavity');
  }finally{s.dispose();}}
});
test('thrown dagger and thrown shield keep piercing/crushing semantics',()=>{
  assert.equal(projectileBodyImpact({spec:'daggers',vel:v(0,0,30),radius:.095,melee:{kind:'dagger'}}).kind,'pierce');
  assert.equal(projectileBodyImpact({spec:'shield',vel:v(0,0,24),radius:.28,melee:{kind:'shield'}}).kind,'blunt');
});
test('damage over time does not invent a fresh bullet wound from last attacker',()=>{
  const {s,a,b}=game();try{a.spec='rifle';b.hp=1;s.damage(b,10,a.id,'burn');assert.equal(b.death?.wound,undefined);assert.equal(b.death?.gibPart,undefined);assert.equal(b.death?.motion?.kind,'ragdoll');}finally{s.dispose();}
});
test('wound history is bounded, snapshot copies are isolated, and heal/round reset clear it',()=>{
  const {s,a,b}=game();try{
    b.hp=1000;const i=meleeBodyImpact('sword');
    for(let n=0;n<20;n++)s.damage(b,1,a.id,'direct',v(0,0,1),0,b.body.translation(),true,false,false,[],i);
    assert.equal(b.wounds?.length,MAX_BODY_WOUNDS);assert.equal(new Set(b.wounds!.map(w=>w.id)).size,MAX_BODY_WOUNDS);
    const snap=s.snapshot(),view=snap.players.find(p=>p.id==='b')!;view.wounds![0].point.x=99;assert.notEqual(b.wounds![0].point.x,99);
    s.debug({action:'heal'},'a');assert.equal(b.wounds,undefined);
    s.damage(b,1,a.id,'direct',v(0,0,1),0,b.body.translation(),true,false,false,[],i);s.resetRound();assert.equal(b.wounds,undefined);
  }finally{s.dispose();}
});
test('death contract survives JSON and cloned snapshots without authority mutation',()=>{
  const {s,a,b}=game();try{b.hp=1;a.spec='greatsword';a.class='melee';s.directHit(a,b,add(b.body.translation(),v(.3,.2,0)),v(0,0,1),52,560,'kill');
    const snapshot=s.snapshot(),d=snapshot.players.find(p=>p.id==='b')!.death!;
    assert.equal(d.gibPart,'right-shoulder');assert.equal(d.wound?.kind,'sever');
    assert.deepEqual(JSON.parse(JSON.stringify(d.wound)),JSON.parse(JSON.stringify(b.death!.wound)));
    d.wound!.direction.x=99;d.motion!.velocity.x=99;
    assert.notEqual(b.death!.wound!.direction.x,99);assert.notEqual(b.death!.motion!.velocity.x,99);
  }finally{s.dispose();}
});
