import {test} from 'node:test';import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {prepareMotion} from '../src/shared/movement.js';
import {stageHazards,inLava,LAVA_SURFACE} from '../src/shared/stage-hazards.js';
import {MAPS} from '../src/shared/maps.js';import {v,idleInput} from '../src/shared/config.js';
await initPhysics();
test('lava is a real pit: fall, float, swim, jump onto bank; bridges stay solid',()=>{
 const s=new Simulation({map:'lavaworks',sandbox:true});try{const p=s.addPlayer('p')!;p.body.setTranslation(v(5,3,7),true);
 const tick=(x=0,z=0,jump=false)=>{prepareMotion(s.world,p.body,p.collider,p,{...idleInput(),x,z,jump},{...p.stats,map:s.options.map},1/60);s.world.step();};
 for(let i=0;i<180;i++)tick();let pos=p.body.translation();assert.ok(inLava('lavaworks',pos));assert.ok(Math.abs(pos.y-(LAVA_SURFACE+.15))<.1,JSON.stringify(pos));
 const before=pos.z;for(let i=0;i<25;i++)tick(0,1);assert.ok(p.body.translation().z>before+1);
 for(let i=0;i<60;i++)tick(-1,0,i===0);pos=p.body.translation();assert.ok(pos.x<2.5,JSON.stringify(pos));assert.ok(pos.y>.8,JSON.stringify(pos));assert.ok(!inLava('lavaworks',pos));
 p.move=v();p.ext=v();p.body.setTranslation(v(5,4,0),true);p.body.setLinvel(v(),true);for(let i=0;i<120;i++)tick();assert.ok(p.body.translation().y>2.5);assert.equal(inLava('lavaworks',p.body.translation()),false);
 }finally{s.dispose();}
});
test('spikes extend and retract progressively; eruption plays once per cycle, saw emits a distinct impact',()=>{
 const h=(t:number)=>stageHazards('spikevault',t)[0];assert.equal(h(3.5).extension,0);assert.ok(h(3.69).extension!>0&&h(3.69).extension!<1);assert.equal(h(4).extension,1);assert.ok(h(4.9).extension!<1);
 const s=new Simulation({map:'spikevault'});try{s.phase='fight';s.time=3.59;s.stageHazards.step();s.time=3.61;s.stageHazards.step();s.stageHazards.step();assert.equal(s.events.filter(e=>e.type==='stage-spikes-rise').length,1);s.time=8.61;s.stageHazards.step();assert.equal(s.events.filter(e=>e.type==='stage-spikes-rise'&&e.pos.x===-8&&e.pos.z===-5).length,2);}finally{s.dispose();}
 const saw=new Simulation({map:'sawmill'});try{const p=saw.addPlayer('p')!;saw.phase='fight';saw.time=4;const h=stageHazards('sawmill',4)[0];p.body.setTranslation(v(h.x,h.y,h.z),true);saw.stageHazards.step();saw.stageHazards.step();assert.equal(saw.events.filter(e=>e.type==='stage-saw-hit').length,1);}finally{saw.dispose();}
});
test('Random resolves on the authority and snapshots contain a real map',()=>{const old=Math.random;try{for(const [random,index]of [[0,0],[.999,MAPS.length-1]]){Math.random=()=>random;const s=new Simulation({map:'random'});try{assert.equal(s.options.map,MAPS[index].id);assert.equal(s.options.randomStage,true);s.configureLobby({botCount:0});assert.equal(s.options.map,MAPS[index].id);assert.equal(s.snapshot().options.map,MAPS[index].id);s.configureLobby({map:'moonlit'});assert.equal(s.options.map,'moonlit');assert.equal(s.options.randomStage,false);}finally{s.dispose();}}}finally{Math.random=old;}});
test('lava swimming and escape match client prediction during snapshot gaps',async()=>{
 const {Predictor}=await import('../src/client/prediction.js');const s=new Simulation({map:'lavaworks',sandbox:true}),prediction=new Predictor();try{const p=s.addPlayer('p')!;s.addPlayer('q');s.phase='fight';p.body.setTranslation(v(5,-.95,7),true);p.body.setLinvel(v(),true);s.world.propagateModifiedBodyPositionsToColliders();const snapshot=s.snapshot(),me=snapshot.players[0];prediction.reconcile(snapshot,me);
 for(let i=0;i<85;i++){const input={...idleInput(),seq:i+1,x:i>=25?-1:0,z:i<25?1:0,jump:i===25};p.input={...input};p.inputAt=s.time;s.step(1/60);prediction.step(input,1/60,me);const actual=p.body.translation();assert.ok(Math.hypot(prediction.pos.x-actual.x,prediction.pos.y-actual.y,prediction.pos.z-actual.z)<.06,`tick ${i}: ${JSON.stringify(actual)} / ${JSON.stringify(prediction.pos)}`);}
 }finally{prediction.dispose();s.dispose();}
});
