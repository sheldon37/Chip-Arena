import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {SpecId,CONFIG as C,v,len} from '../src/shared/config.js';
import {ballisticStep,ballisticPitch} from '../src/shared/projectiles.js';
await initPhysics();
function shotArena(spec:SpecId='pistol',stacks=0){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b')!;
 a.spec=spec;a.chips={velocity:stacks};a.stats=statsFor(a.chips);s.phase='fight';
 for(const prop of [...s.props.values()])s.removeProp(prop.id);
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(-40,1,30),true);
 s.addProp({id:'test-floor',pos:v(0,-.75,0),size:v(240,1.5,240),material:'foundation'});s.world.step();s.spawnProjectile(a);
 const shot=[...s.projectiles.values()][0];shot.pos=v(0,1.6,0);shot.vel=v(len(shot.vel),0,0);return {s,a,b,shot};
}
test('ballistic integration gives constant acceleration and is independent of tick partition',()=>{
 let velocity=v(20,2,0),position=v();for(let n=0;n<60;n++){const step=ballisticStep(velocity,12,1/60);position=v(position.x+step.displacement.x,position.y+step.displacement.y,0);velocity=step.velocity;}
 assert.ok(Math.abs(position.x-20)<1e-7);assert.ok(Math.abs(position.y+4)<1e-7);assert.ok(Math.abs(velocity.y+10)<1e-7);
 const one=ballisticStep(v(20,2,0),12,1);assert.ok(Math.abs(one.displacement.y-position.y)<1e-7);
});
test('every gun and spell lands from gravity; one speed Chip extends horizontal travel by approximately 25%',()=>{
 const results=[];
 for(const w of Object.values(WEAPONS).filter(w=>w.class!=='melee'&&!w.devOnly)){
  const distances=[];for(const stacks of [0,1]){const {s,shot}=shotArena(w.id,stacks);for(let n=0;n<180&&s.projectiles.has(shot.id);n++)s.stepProjectiles(1/60);
   const impact=s.events.find(e=>e.type==='impact');assert.ok(impact,`${w.id} must hit the floor`);assert.ok(Math.abs(impact.pos.y-shot.radius)<.02,`${w.id} impact y=${impact.pos.y}, radius=${shot.radius}, pos=${JSON.stringify(impact.pos)}`);assert.ok(shot.life>1,'the floor, not expiry, must stop the shot');distances.push(impact.pos.x);s.dispose();}
  assert.ok(Math.abs(distances[1]/distances[0]-1.25)<.01,`${w.id}: ${distances}`);results.push({weapon:w.id,baseGroundRangeM:+distances[0].toFixed(2),oneCoilGroundRangeM:+distances[1].toFixed(2)});
 }
 console.log('Flat shots from 1.6 m above level ground:',JSON.stringify(results));
});
test('one Velocity Coil reduces drop by 36% at the same horizontal distance',()=>{
 const drops=[];for(const stacks of [0,1]){const {s,shot}=shotArena('pistol',stacks);shot.pos.y=4;const duration=10/shot.vel.x;for(let left=duration;left>1e-8;){const dt=Math.min(1/60,left);s.stepProjectiles(dt);left-=dt;}drops.push(4-shot.pos.y);assert.ok(Math.abs(shot.pos.x-10)<1e-6);s.dispose();}
 assert.ok(Math.abs(drops[1]/drops[0]-.64)<1e-6);
});
test('a fast falling projectile hits a player using the authoritative sweep',()=>{
 const {s,b,shot}=shotArena();b.body.setTranslation(v(0,1,1),true);s.world.step();shot.pos=v(0,2,-5);shot.vel=v(0,-20,240);for(let n=0;n<4;n++)s.stepProjectiles(1/60);
 assert.equal(b.hp,65.5);assert.ok(!s.projectiles.has(shot.id));s.dispose();
});
test('ricochets retain gravity, fall again, and remain bounded',()=>{
 const {s,shot}=shotArena();shot.bounces=3;shot.vel=v(10,-5,0);let bounced=false;
 for(let n=0;n<60&&s.projectiles.has(shot.id);n++){s.stepProjectiles(1/60);if(shot.bounces<3&&shot.vel.y>0){bounced=true;break;}}
 assert.ok(bounced);assert.equal(shot.gravity,WEAPONS.pistol.gravity);const vy=shot.vel.y;s.stepProjectiles(1/60);assert.ok(shot.vel.y<vy);
 for(let n=0;n<240;n++)s.stepProjectiles(1/60);assert.equal(s.projectiles.size,0);s.dispose();
});
test('split projectiles inherit gravity and cannot pass through the intact struck cover',()=>{
 const {s,a,shot}=shotArena('pistol');a.stats=statsFor({splitter:3});const cover=s.addProp({id:'cover',pos:v(3,1.5,0),size:v(.3,3,6),material:'metal'})!;s.world.step();
 for(let n=0;n<8&&!s.events.some(e=>e.type==='impact');n++)s.stepProjectiles(1/60);
 const splits=[...s.projectiles.values()].filter(p=>!p.direct);assert.equal(splits.length,5);assert.ok(splits.every(p=>p.gravity===shot.gravity&&!p.hit.has(cover.id)));
 for(let n=0;n<20;n++)s.stepProjectiles(1/60);assert.equal(s.projectiles.size,0);assert.ok(cover.hp>0);s.dispose();
});
test('homing still falls without an acquired target and bot arc aim respects projectile speed',()=>{
 const {s,shot}=shotArena('fire');shot.homing=105;s.stepProjectiles(1/60);assert.ok(shot.vel.y<0);s.dispose();
 const base=ballisticPitch(10,0,24,12),boost=ballisticPitch(10,0,30,12);assert.ok(base>boost&&boost>0);
 const flight=10/(24*Math.cos(base));assert.ok(Math.abs(24*Math.sin(base)*flight-.5*12*flight*flight)<1e-6);
 assert.ok(Number.isFinite(ballisticPitch(1000,100,15,5)));
});
