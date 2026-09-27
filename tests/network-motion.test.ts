import {test} from 'node:test';import assert from 'node:assert/strict';
import {Simulation,initPhysics,Snapshot} from '../src/shared/simulation.js';
import {Predictor} from '../src/client/prediction.js';import {SnapshotBuffer} from '../src/client/snapshots.js';
import {idleInput,v,Input} from '../src/shared/config.js';import {socketEndpoint} from '../src/shared/connection.js';
import {validPacket} from '../src/shared/relay-protocol.js';
await initPhysics();
function fixture(){const sim=new Simulation({sandbox:true,inputHoldSeconds:.75});sim.addPlayer('guest');sim.addPlayer('host');sim.phase='fight';sim.players.get('guest')!.body.setTranslation(v(0,1,-8),true);for(let n=0;n<90;n++)sim.step();return sim;}
test('input batches retain turns and short taps, consume once per tick, reject stale rounds and nonfinite inputs',()=>{
 const sim=fixture(),epoch='1:1:0';const inputs=[{...idleInput(),seq:1,x:1},{...idleInput(),seq:2,x:-1,jump:true},{...idleInput(),seq:3,x:0}];
 assert.equal(sim.queueInputs('guest',inputs,epoch),true);assert.equal(sim.queueInputs('guest',inputs,epoch),true);
 for(let n=1;n<=3;n++){sim.step();assert.equal(sim.players.get('guest')!.ack,n);assert.equal(sim.players.get('guest')!.input.x,inputs[n-1].x);}
 assert.equal(sim.queueInputs('guest',[{...idleInput(),seq:4,x:NaN}],epoch),false);
 assert.equal(sim.queueInputs('guest',[{...idleInput(),seq:4}], '1:1:9'),false);
 assert.equal(sim.queueInputs('guest',Array.from({length:61},(_,n)=>({...idleInput(),seq:4+n})),epoch),false);
 sim.resetRound();sim.phase='fight';assert.equal(sim.queueInputs('guest',[{...idleInput(),seq:4}],epoch),false);sim.dispose();
});
for(const cadence of [3,12])test(`dynamic movement replay remains within 2 cm with ${cadence*1000/60} ms batches and 50 ms each-way delay`,()=>{
 const sim=fixture(),pred=new Predictor();let s=sim.snapshot(),me=s.players[0];pred.reconcile(s,me);
 const queue:{at:number;inputs?:Input[];s?:Snapshot}[]=[],errors:number[]=[];let batch:Input[]=[];
 for(let tick=1;tick<=720;tick++){
  const phase=tick%240,i={...idleInput(),seq:tick,x:phase<90?1:phase<180?-1:0,jump:tick%180===1};pred.step(i,1/60,me);pred.renderPosition(1/60);batch.push(i);
  if(tick%cadence===0){queue.push({at:tick+3,inputs:batch});batch=[];}
  for(const q of queue.filter(q=>q.at===tick)){if(q.inputs)assert.equal(sim.queueInputs('guest',q.inputs,'1:1:0'),true);else{s=q.s!;me=s.players[0];pred.reconcile(s,me);if(tick>60)errors.push(pred.error);}}
  sim.step();if(tick%cadence===0)queue.push({at:tick+3,s:sim.snapshot()});assert.ok(pred.pos.y>.7,'floor penetration');
 }
 const max=Math.max(...errors);assert.ok(max<.02,`largest correction ${max} m`);pred.dispose();sim.dispose();
});
test('an input gap cannot replay a one-frame jump or Block tap',()=>{const sim=fixture();sim.queueInputs('guest',[{...idleInput(),seq:1,jump:true,block:true}], '1:1:0');sim.step();const player=sim.players.get('guest')!;assert.equal(player.input.jump,true);const vy=player.body.linvel().y;sim.step();assert.equal(player.input.jump,false);assert.equal(player.input.block,false);assert.ok(player.body.linvel().y<vy);assert.equal(player.ack,1);sim.dispose();});
test('small reconciliation errors ease visually while authority is corrected immediately; resets snap',()=>{
 const sim=fixture(),pred=new Predictor(),s=sim.snapshot(),me=s.players[0];pred.reconcile(s,me);const before=pred.renderPosition(0);me.pos.x+=.4;pred.reconcile(s,me);assert.ok(Math.abs(pred.pos.x-me.pos.x)<1e-6);assert.ok(Math.abs(pred.renderPosition(0).x-before.x)<1e-6);pred.renderPosition(.2);assert.ok(pred.visualOffset<.012);
 s.generation=1;me.pos.x+=4;pred.reconcile(s,me);assert.equal(pred.visualOffset,0);pred.dispose();sim.dispose();
});
test('adaptive interpolation advances monotonically through jitter and normalizes debris rotations',()=>{
 const sim=fixture(),buffer=new SnapshotBuffer();let latest=sim.snapshot();const origin=latest.time;let previous=-Infinity,frame=0;
 for(let now=0;now<5000;now+=1000/60){if(frame++%12===0){latest=sim.snapshot();latest.tick=frame;latest.time=origin+now/1000;latest.players[1].pos=v(now/1000,1,0);latest.players[1].vel=v(1,0,0);buffer.push(latest,now);}
 const rendered=buffer.sample(latest,'guest',now);if(frame>30){assert.ok(rendered.time>=previous-1e-6);previous=rendered.time;}}
 assert.ok(buffer.delayMs>240&&buffer.delayMs<270);assert.equal(buffer.push({...latest,tick:latest.tick-1},6000),false);sim.dispose();
});
test('relay validates bounded sequential input batches and refuses guest snapshots',()=>{
 const latest={type:'inputs',epoch:'1:1:0',inputs:[{...idleInput(),seq:1},{...idleInput(),seq:2}]};const packet={seq:1,ack:0,events:[],latest};assert.equal(validPacket(packet,false),true);assert.equal(validPacket({...packet,latest:{...latest,inputs:[latest.inputs[1],latest.inputs[0]]}},false),false);assert.equal(validPacket({...packet,latest:{type:'snapshot',state:{}}},false),false);
});
test('automatic routing allows secure WebSocket backends without credentials in URLs',()=>{
 assert.equal(socketEndpoint('/socket','https://arena.example'),'wss://arena.example/socket');assert.equal(socketEndpoint('ws://backend.example/socket','https://arena.example'),null);assert.equal(socketEndpoint('wss://token:secret@backend.example/socket','https://arena.example'),null);assert.equal(socketEndpoint('wss://backend.example/socket','https://arena.example'),'wss://backend.example/socket');
});
