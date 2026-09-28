import {test} from 'node:test';import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';import {arenaBlocks} from '../src/shared/arena.js';import {HostedRoom} from '../src/shared/host-room.js';import {scoreCircles,matchScoreHud} from '../src/client/match-score.js';
await initPhysics();
function fixture(randomMapEachSet=true){const s=new Simulation({map:'moonlit',randomMapEachSet,skipIntros:true});s.addPlayer('a','Player A');s.addPlayer('b','Player B');s.start();s.selectSpec('a','pistol');s.selectSpec('b','pistol');return s;}
function winRound(s:Simulation){s.phase='fight';s.players.get('b')!.hp=0;s.resolveRound();}
test('map rotation waits for the next set, preserves builds/scores, avoids repeats and stays optional',()=>{
 for(const enabled of [false,true]){const s=fixture(enabled);try{
  winRound(s);s.time=s.phaseEnd;s.advance();assert.equal(s.options.map,'moonlit');assert.equal(s.round,2);
  winRound(s);assert.equal(s.players.get('a')!.score,1);assert.equal(s.options.map,'moonlit');s.time=s.phaseEnd;s.advance();assert.equal(s.phase,'draft');assert.equal(s.options.map,'moonlit');
  const d=s.draft!,p=s.players.get(d.player)!;s.time=d.readyAt;assert.ok(s.selectChip(p.id,p.offers[0]));const chip=d.chosen!;s.time+=10;s.advance();assert.equal(s.phase,'countdown');assert.equal(s.setNumber,2);assert.equal(p.chips[chip],1);assert.equal(s.players.get('a')!.score,1);assert.ok([...s.players.values()].every(p=>p.roundWins===0));
  if(enabled)assert.notEqual(s.options.map,'moonlit');else assert.equal(s.options.map,'moonlit');assert.deepEqual([...s.props.keys()],arenaBlocks(s.options.map).map(b=>b.id));
  for(let n=0;n<12;n++){const before:string|undefined=s.options.map;s.beginNextSet();assert.equal(s.options.map===before,!enabled);}
  const before:string|undefined=s.options.map;s.players.get('a')!.score=4;s.players.get('a')!.roundWins=1;winRound(s);s.time=s.phaseEnd;s.advance();assert.equal(s.phase,'match');assert.equal(s.options.map,before,'final match does not swap scenery');
 }finally{s.dispose();}}
});
test('map rotation is host-owned, strictly boolean and locked after ready',()=>{
 const room=new HostedRoom('ABCD2345',{name:'Host',class:'ranged',color:'#abcdef'},false,()=>{},6,false,true,5,{randomMapEachSet:true,map:'neon'});try{room.join({name:'Guest'});assert.equal(room.receive('guest',{type:'settings',randomMapEachSet:false}),false);assert.equal(room.sim.options.randomMapEachSet,true);room.receive('host',{type:'settings',randomMapEachSet:'true'});assert.equal(room.sim.options.randomMapEachSet,false);room.receive('host',{type:'settings',randomMapEachSet:true});room.receive('host',{type:'ready',ready:true});room.receive('guest',{type:'ready',ready:true});assert.equal(room.receive('host',{type:'settings',randomMapEachSet:false}),false);assert.equal(room.sim.snapshot().options.randomMapEachSet,true);}finally{room.dispose();}
});
test('score circles show half rounds and full sets without double-counting the set-winning round',()=>{
 const s=fixture();try{let snap=s.snapshot(),p=snap.players[0];assert.deepEqual(scoreCircles(snap,p),[0,0,0,0,0]);winRound(s);snap=s.snapshot();assert.deepEqual(scoreCircles(snap,snap.players[0]),[.5,0,0,0,0]);s.time=s.phaseEnd;s.advance();winRound(s);snap=s.snapshot();assert.deepEqual(scoreCircles(snap,snap.players[0]),[1,0,0,0,0]);snap.phase='draft';assert.deepEqual(scoreCircles(snap,snap.players[0]),[1,0,0,0,0]);s.beginNextSet();s.players.get('a')!.roundWins=1;snap=s.snapshot();assert.deepEqual(scoreCircles(snap,snap.players[0]),[1,.5,0,0,0]);s.players.get('a')!.score=5;s.setWinner='a';snap=s.snapshot();assert.deepEqual(scoreCircles(snap,snap.players[0]),[1,1,1,1,1]);}finally{s.dispose();}
});
test('eight-player score panel precedes alive status, includes own and eliminated players, and escapes names',()=>{
 const s=new Simulation();try{for(let i=0;i<8;i++)s.addPlayer(String(i),'Player '+i);const snap=s.snapshot();snap.players[0].name='<img onerror="bad">';snap.players[0].roundWins=1;snap.players[7].hp=0;snap.players[6].connected=false;const html=matchScoreHud(snap,'0');assert.equal((html.match(/class="score-pilot /g)||[]).length,8);assert.equal((html.match(/class="set-circle /g)||[]).length,40);assert.ok(html.indexOf('set-scoreboard')<html.indexOf('alive-roster'));assert.match(html,/6 \/ 8 ALIVE/);assert.match(html,/Player 7/);assert.match(html,/partial/);assert.match(html,/&lt;img/);assert.doesNotMatch(html,/<img/);}finally{s.dispose();}
});
