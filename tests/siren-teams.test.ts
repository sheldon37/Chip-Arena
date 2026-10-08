import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {TEAMS} from '../src/shared/teams.js';
import {idleInput,v} from '../src/shared/config.js';
await initPhysics();

for(const source of ['saved-player','selected-player','bot'] as const){
 test(`red squad versus blue Siren preserves team rules through the match (${source})`,()=>{
  const s=new Simulation({gameMode:'teams',map:'neon',skipIntros:true,roundsPerSet:1,randomMapEachSet:true});
  try{
   const botBoss=source==='bot';
   s.addPlayer('host','DEV',botBoss?'ranged':'dev',null,'#ffffff',source==='saved-player'?{preferredSpec:'siren'}:{},true);
   s.configureLobby({botCount:3,botWeapons:botBoss?['siren','pistol','fire']:['pistol','fire','sword'],botTeams:botBoss?['blue','red','red']:['red','red','red']},'host');
   s.setTeam('host',botBoss?'red':'blue');
   const teams=[...s.players.values()].map(p=>[p.id,p.team]);
   const check=()=>{
    const snap=s.snapshot();assert.equal(snap.options.gameMode,'teams');
    assert.deepEqual(snap.players.map(p=>[p.id,p.team]),teams);
    for(const p of snap.players)assert.equal(p.color,TEAMS[p.team!].color);
   };
   s.setReady('host',true);check();
   const select=()=>{assert.equal(s.selectSpec('host',botBoss?'pistol':'siren'),true);s.advance();};
   select();check();assert.equal(s.options.map,'siren');assert.equal(s.phase,'countdown');
   s.time=s.phaseEnd;s.advance();assert.equal(s.phase,'fight');
   const boss=[...s.players.values()].find(p=>p.spec==='siren')!,red=[...s.players.values()].filter(p=>p.team==='red');
   assert.equal(s.damage(red[1],10,red[0].id,'direct'),0,'red teammates cannot damage each other');
   assert.equal(s.damage(boss,10,red[0].id,'direct'),10,'red can damage blue Siren');
   s.damage(boss,boss.hp+1,red[0].id,'direct');s.resolveRound();
   assert.equal(s.phase,'result');assert.match(s.result,/Red team wins/);
   assert.ok(red.every(p=>p.score===1));assert.equal(boss.score,0);
   s.resetRound();s.countdown();check();s.beginNextSet();check();assert.equal(s.options.map,'siren');
   s.phase='match';assert.equal(s.rematch(),true);check();select();check();
   s.returnLobby();check();assert.equal(s.options.map,'neon');
  }finally{s.dispose();}
 });
}

test('hosted team assignments survive bot rebuilds and Siren specialization',()=>{
 const room=new HostedRoom('TEAM2345',{name:'Host',class:'dev',color:'#ffffff'},false,()=>{},6,true,true,20,{gameMode:'teams'});
 try{
  room.join({name:'Ally',class:'ranged'},false,'guest');
  assert.ok(room.receive('host',{type:'settings',botCount:2,botWeapons:['siren','pistol']}));
  for(const p of room.sim.players.values())assert.ok(room.receive('host',{type:'team',target:p.id,team:p.spec==='siren'?'blue':'red'}));
  assert.ok(room.receive('host',{type:'settings',roundsPerSet:2}));
  assert.deepEqual(room.sim.options.botTeams,['blue','red']);
  assert.ok(room.receive('host',{type:'ready',ready:true}));assert.ok(room.receive('guest',{type:'ready',ready:true}));
  assert.ok(room.receive('host',{type:'spec',id:'emperor'}));assert.ok(room.receive('guest',{type:'spec',id:'pistol'}));room.sim.advance();
  const snap=room.sim.snapshot();assert.equal(snap.phase,'countdown');assert.equal(snap.options.gameMode,'teams');assert.equal(snap.options.map,'siren');
  for(const p of snap.players)assert.equal(p.team,p.spec==='siren'?'blue':'red');
 }finally{room.dispose();}
});

test('Siren keeps FFA and the configured friendly-fire option',()=>{
 for(const mode of ['ffa','teams'] as const)for(const friendlyFire of [false,true]){
  const s=new Simulation({gameMode:mode,friendlyFire,skipIntros:true});
  try{
   s.addPlayer('boss','Siren','dev',null,'#ffffff',{preferredSpec:'siren'},true);
   const a=s.addPlayer('a')!,b=s.addPlayer('b')!;s.setTeam('boss','blue');s.setTeam('a','red');s.setTeam('b','red');
   s.beginIntros();assert.equal(s.options.gameMode,mode);assert.equal(s.options.friendlyFire,friendlyFire);
   s.phase='fight';assert.equal(s.damage(b,10,a.id,'direct'),mode==='ffa'||friendlyFire?10:0);
  }finally{s.dispose();}
 }
});

test('Siren team bots and Marked target enemies while allied Siren hits are ignored',()=>{
 const s=new Simulation({gameMode:'teams',skipIntros:true});
 try{
  const boss=s.addPlayer('boss','Siren','dev',null,'#ffffff',{preferredSpec:'siren'},true)!,ally=s.addPlayer('ally')!,enemy=s.addPlayer('enemy','Red','ranged','hard')!,redAlly=s.addPlayer('red-ally')!;
  s.setTeam(boss.id,'blue');s.setTeam(ally.id,'blue');s.setTeam(enemy.id,'red');s.setTeam(redAlly.id,'red');s.beginIntros();s.phase='fight';
  for(const p of [...s.props.values()])s.removeProp(p.id);
  boss.body.setTranslation(v(0,1,0),true);ally.body.setTranslation(v(.7,1,2),true);enemy.body.setTranslation(v(0,1,8),true);redAlly.body.setTranslation(v(2,1,8),true);
  s.world.propagateModifiedBodyPositionsToColliders();s.random=()=>.5;
  assert.ok(Math.abs(s.botInput(enemy).yaw)>2.5,'red bot aims toward blue opponents, not its nearby red ally');
  assert.equal(s.siren.hit(boss,ally,10,'swipe',true),0);assert.equal(s.siren.hit(boss,enemy,10,'swipe',true),10);
  boss.input=idleInput();s.siren.state(boss).mode='ranged';assert.equal(s.siren.start(boss,'mark'),true);assert.equal(s.siren.marks[0].target,enemy.id);
 }finally{s.dispose();}
});
