import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {WEAPONS,eligibleChips,statsFor,magazineCapacity} from '../src/shared/content.js';
import {CONFIG as C,SpecId,v,len,dot,norm,idleInput} from '../src/shared/config.js';
import {steerVelocity} from '../src/shared/projectiles.js';
await initPhysics();
const near=(a:number,b:number)=>assert.ok(Math.abs(a-b)<1e-6,`${a} ≠ ${b}`);
function arena(spec:SpecId='pistol',chips:Record<string,number>={}){
 const s=new Simulation({sandbox:true});const a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b')!;
 a.spec=spec;a.chips=chips;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])if(p.material!=='foundation')s.removeProp(p.id);
 a.body.setTranslation(v(-3,1,-7),true);b.body.setTranslation(v(3,1,-7),true);a.input.yaw=Math.PI/2;s.world.step();return {s,a,b};
}
test('every weapon receives only useful projectile, resource and attack-rate modifiers',()=>{
 for(const w of Object.values(WEAPONS)){
  const ids=new Set(eligibleChips(w.id,{}).map(c=>c.id));
  for(const id of ['multishot','ricochet','velocity','homing'])assert.equal(ids.has(id),w.class!=='melee'&&!(w.id==='leet'&&id==='homing'),`${w.id}: ${id}`);
  assert.equal(ids.has('echo-strike'),w.class==='melee'&&w.id!=='ansatsuken'&&w.id!=='emperor');
  for(const [cls,resource,rate]of [['ranged','magazine','fire-rate'],['magic','mana','cast-rate'],['melee','stamina','swing-rate']]){
   assert.equal(ids.has(resource),w.class===cls);assert.equal(ids.has(rate),w.class===cls);
  }
 }
 assert.equal(statsFor({multishot:99}).extraProjectiles,3);assert.equal(statsFor({'echo-strike':99}).extraStrikes,2);
 assert.equal(statsFor({homing:99}).homing,105);
});
test('all guns consume ammo, stop at empty, and reload the expanded magazine',()=>{
 for(const spec of ['pistol','rifle','machinegun'] as SpecId[]){const {s,a}=arena(spec,{magazine:2});const cap=magazineCapacity(spec,a.stats);
  assert.ok(cap>WEAPONS[spec].ammo);assert.equal(a.ammo,cap);
  for(let n=0;n<cap;n++){a.cooldown=0;s.primary(a);s.projectiles.clear();}
  assert.equal(a.ammo,0);a.cooldown=0;s.primary(a);assert.equal(s.projectiles.size,0);assert.equal(a.reloadTime,WEAPONS[spec].reload);
  for(let n=0;n<Math.ceil(WEAPONS[spec].reload*60)+1;n++){s.time+=1/60;s.updatePlayer(a,1/60);}
  assert.equal(a.ammo,cap);assert.ok(!('heat' in a));assert.ok(!('overheated' in a));s.dispose();
 }
});
test('machine-gun secondary spends six ammo and schedules exactly six authoritative shots',()=>{
 const {s,a}=arena('machinegun');s.secondary(a);assert.equal(a.ammo,42);assert.equal(a.burst,6);
 for(let n=0;n<40;n++){s.time+=1/60;s.updatePlayer(a,1/60);}
 assert.equal(a.ammo,42);assert.equal(a.burst,0);assert.equal(s.projectiles.size,6);s.dispose();
});
test('parallel shots share one volley cost and proc identity, deal real hits, and obey the budget',()=>{
 for(const spec of ['pistol','fire'] as SpecId[]){const {s,a,b}=arena(spec,{multishot:2});b.body.setTranslation(v(-1.5,1,-7),true);s.world.step();
  const ammo=a.ammo,mana=a.mana;s.primary(a);if(a.cast){s.time=a.cast.at;s.updatePlayer(a,0);}assert.equal(s.projectiles.size,3);
  assert.equal(a.ammo,ammo-(WEAPONS[spec].ammo?1:0));assert.equal(a.mana,mana-WEAPONS[spec].cost);
  assert.equal(new Set([...s.projectiles.values()].map(p=>p.attackId)).size,1);
  for(const p of s.projectiles.values())near(p.damage,WEAPONS[spec].damage*.75);
  for(let n=0;n<10;n++)s.stepProjectiles(1/60);
  assert.ok(b.hp<100-WEAPONS[spec].damage,'multiple direct hits must deal more than a single base shot');s.dispose();
 }
 const {s,a}=arena('pistol',{multishot:3});for(let n=0;n<50;n++)s.spawnProjectile(a);assert.equal(s.projectiles.size,C.maxProjectiles);s.dispose();
});
test('echo strikes hit once per sweep, charge stamina once, and retain recovery',()=>{
 const {s,a,b}=arena('sword',{'echo-strike':2});b.body.setTranslation(v(-1.5,1,-7),true);s.world.step();
 s.primary(a);assert.equal(a.stamina,100-WEAPONS.sword.cost-2*C.followupCost);assert.ok(a.cooldown>WEAPONS.sword.interval);
 for(let n=0;n<80;n++){s.time=n/120;s.melee(a);s.melee(a);}
 // Third sweep occurs after 0.62 seconds and completes by 0.78.
 for(let n=80;n<110;n++){s.time=n/120;s.melee(a);s.melee(a);}
 near(b.hp,43);assert.equal(a.stamina,100-WEAPONS.sword.cost-2*C.followupCost);assert.equal(a.swing,null);s.dispose();
});
test('mana and stamina upgrades set real caps; insufficient resources block combat and regenerate',()=>{
 const magic=arena('fire',{mana:2});assert.equal(magic.a.mana,150);magic.a.mana=7;magic.s.primary(magic.a);assert.equal(magic.s.projectiles.size,0);
 magic.s.time+=1;magic.s.updatePlayer(magic.a,1);near(magic.a.mana,19);magic.s.primary(magic.a);near(magic.a.mana,11);magic.s.dispose();
 const {s,a}=arena('sword',{stamina:2});assert.equal(a.stamina,150);a.stamina=13;s.primary(a);assert.equal(a.swing,null);assert.equal(a.mana,100);
 a.staminaAt=s.time;s.time+=.2;s.updatePlayer(a,.2);near(a.stamina,13);s.time+=.5;s.updatePlayer(a,.5);near(a.stamina,25);s.primary(a);assert.equal(a.swing,null);s.time+=.5;s.updatePlayer(a,.5);s.primary(a);near(a.stamina,37-WEAPONS.sword.cost);s.dispose();
});
test('shield throw spends stamina upfront and does not enable the retired held guard',()=>{
 const {s,a}=arena('shield');a.input={...idleInput(),seq:1,yaw:Math.PI/2,secondary:true};a.inputAt=s.time;s.updatePlayer(a,.1);assert.equal(a.guarding,false);near(a.stamina,100-WEAPONS.shield.secondaryCost!);assert.equal(a.meleeAction?.kind,'shield-throw');
 s.damage(a,20,'b','direct',v(-1,0,0));near(a.hp,80);a.stamina=0;s.updatePlayer(a,.01);assert.equal(a.guarding,false);s.dispose();
});
test('class-specific rate modifiers reduce actual cooldown and melee windup with explicit stacking',()=>{
 for(const [spec,chip]of [['pistol','fire-rate'],['fire','cast-rate'],['sword','swing-rate']] as [SpecId,string][]){const {s,a}=arena(spec,{[chip]:2,overclock:1});s.primary(a);near(a.cooldown,WEAPONS[spec].interval/(1.15**2*1.18));if(a.swing)near(a.swing.at-s.time,WEAPONS[spec].windup/(1.15**2*1.18));s.dispose();}
});
test('projectile speed stacks, homing turning preserves speed, and travel remains bounded',()=>{
 const {s,a}=arena('pistol',{velocity:2,homing:2});s.spawnProjectile(a);const shot=[...s.projectiles.values()][0];near(len(shot.vel),52*1.25**2);near(shot.distanceLeft,C.projectileMaxTravel);
 const turn=steerVelocity(v(0,0,40),v(1,0,1),35*Math.PI/180/60);near(len(turn),40);near(Math.acos(dot(norm(turn),v(0,0,1))),35*Math.PI/180/60);
 s.players.get('b')!.body.setTranslation(v(20,1,20),true);s.world.step();shot.pos=v(-20,5,-20);shot.vel=v(0,0,len(shot.vel));const start=shot.pos.z;
 for(let n=0;n<240;n++)s.stepProjectiles(1/60);assert.ok(shot.pos.z-start<=C.projectileMaxTravel+1e-6);assert.equal(s.projectiles.size,0);s.dispose();
});
test('homing bends toward visible opponents, respects cover, and does not transfer to split fragments',()=>{
 const {s,a,b}=arena('pistol',{homing:3,splitter:1});a.body.setTranslation(v(0,1,-8),true);b.body.setTranslation(v(2,1,-2),true);a.input.yaw=0;s.world.step();s.spawnProjectile(a);
 const shot=[...s.projectiles.values()][0];shot.pos=v(0,1.1,-7);shot.vel=v(0,0,20);s.stepProjectiles(1/60);assert.ok(shot.vel.x>0);
 const wall=s.addProp({id:'homing-wall',pos:v(0,1.5,-4),size:v(7,3,.5),material:'stone'})!;s.world.step();shot.pos=v(0,1.1,-7);shot.vel=v(0,0,20);s.stepProjectiles(1/60);near(shot.vel.x,0);
 const fragments=[];for(let n=0;n<12;n++){s.stepProjectiles(1/60);fragments.push(...[...s.projectiles.values()].filter(p=>!p.direct));}assert.equal(b.hp,100);assert.ok(wall.hp<wall.maxHp);
 assert.ok(fragments.length>0);assert.ok(fragments.every(p=>p.homing===0&&p.bounces===0&&p.effectsUsed));s.dispose();
});
test('round reset restores expanded pools and clears temporary combo and burst state',()=>{
 const {s,a}=arena('machinegun',{magazine:2,mana:2,stamina:2});s.secondary(a);a.stamina=3;a.mana=2;a.staminaAt=10;s.resetRound();const view=s.snapshot().players[0];
 assert.equal(view.ammo,72);assert.equal(view.maxAmmo,72);assert.equal(view.mana,150);assert.equal(view.maxMana,150);assert.equal(view.stamina,150);assert.equal(view.maxStamina,150);assert.equal(a.burst,0);assert.equal(a.swing,null);assert.equal(a.reloadTime,0);s.dispose();
});
