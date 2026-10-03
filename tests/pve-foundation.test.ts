// PvE foundation: sim-level acceptance (design §11 F, criteria 2-9 and 12) with the seed parts and the fake parts.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import RAPIER from '@dimforge/rapier3d-compat';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {CONFIG as C,idleInput,v,type Input} from '../src/shared/config.js';
import {offerChips,CHIPS} from '../src/shared/content.js';
import {POWER_CHIPS} from '../src/shared/power-chips.js';
import {introFrame} from '../src/shared/intros.js';
import {sirenMotion} from '../src/shared/siren-motion.js';
import {SIREN_TIMING} from '../src/shared/siren-contract.js';
import {SIREN_COMBAT} from '../src/shared/siren-content.js';
import {sirenStageHazards,SIREN_VENTS} from '../src/shared/siren-stage-layout.js';
import {prepareMotion} from '../src/shared/movement.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {BOSS_PLAYER_ID,HORDE_ID,PARTY_BOT_PREFIX,PVE_GROUPS,pveOption,type PveOptions} from '../src/shared/pve/contract.js';
import {PveSystem} from '../src/shared/pve/system.js';
import {PVE,PVE_SIREN,damageScale} from '../src/shared/pve/balance.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const KEY='0123456789abcdef0123456789abcdef';
const pve=(o:Partial<PveOptions>={}):PveOptions=>({boss:'siren',seed:7,...o});
function hello(s:Simulation,id:string,key=KEY){assert.equal(s.pve!.command(id,{action:'hello',v:1,key},false),true);}
/** A PvE sim in the fight phase with one human (and optionally the boss) placed explicitly. */
function fightSim(o:Partial<PveOptions>={},boss=false){
 const s=new Simulation({pve:pve(o)});s.addPlayer('h1','Ana','ranged');hello(s,'h1');
 if(boss)spawnBoss(s);
 s.phase='fight';s.fightStart=s.time;return s;
}
function spawnBoss(s:Simulation,at={x:0,z:0}){
 s.pve!.spawningBoss=true;const b=s.addPlayer(BOSS_PLAYER_ID,'The Siren','melee','normal','#b7a8ff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']});s.pve!.spawningBoss=false;
 assert.ok(b);assert.equal(b.spec,'siren');assert.equal(b.team,'purple');b.body.setTranslation(v(at.x,.9,at.z),true);return b;
}
const place=(p:Player,x:number,z:number,yaw=0)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw,pitch:0};};
const epoch=(s:Simulation)=>`${s.setNumber}:${s.round}:${s.generation}`;

test('PvE options are forced, a solo human with a hello reaches the fight and runs 3,600 ticks (acceptance 2)',()=>{
 const s=new Simulation({pve:pve()});try{
  assert.equal(s.options.map,'siren');assert.equal(s.options.gameMode,'teams');assert.equal(s.options.friendlyFire,false);assert.equal(s.options.sandbox,false);
  assert.ok(s.props.has('pve-machine-0')&&s.props.has('pve-machine-7'),'machine colliders');
  s.addPlayer('h1','Ana','ranged');
  assert.equal(s.setReady('h1',true),false,'no start without a hello');hello(s,'h1');
  assert.equal(s.setReady('h1',true),true);assert.equal(s.phase,'specialization');
  s.selectSpec('h1','pistol');
  for(let i=0;i<4000&&(s.phase as string)!=='fight';i++)s.step();
  assert.equal(s.phase as string,'fight');
  // Integration (S7): the real horde attacks an idle human, so keep the run alive with debug god; the real director
  // reaches 'wave' after the opening.
  s.pve!.debug('god:on');
  for(let i=0;i<3600;i++)s.step();
  assert.equal(s.phase,'fight');const snap=s.snapshot();assert.equal(snap.pve?.v,1);assert.ok(['opening','wave'].includes(snap.pve!.stage),snap.pve?.stage);assert.deepEqual(snap.pve?.helloed,['h1']);
  assert.ok(JSON.stringify(snap).length<200000);
  assert.deepEqual(pveOption({boss:'siren',seed:1,debug:true},{seed:9}),{boss:'siren',seed:9});
  const before=s.pve!.runSeed;s.pve!.reset();const one=s.pve!.runSeed;s.pve!.reset();assert.notEqual(one,before);assert.notEqual(s.pve!.runSeed,one);
  assert.notEqual(s.pve!.rng('horde').next(),s.pve!.rng('director').next());
 }finally{s.dispose();}
});
test('seats and handshake: party cap, party bots, bot eviction, hello-gated ready; PvP unchanged (acceptance 3)',()=>{
 const s=new Simulation({pve:pve({partyBots:3})});try{
  assert.equal(s.joinable(),true);
  s.addPlayer('h1');assert.deepEqual([...s.players.keys()],['h1',PARTY_BOT_PREFIX+'1',PARTY_BOT_PREFIX+'2',PARTY_BOT_PREFIX+'3']);
  assert.ok(s.pve!.partyBots().every(p=>p.ready&&p.team==='blue'&&p.spec!=='siren'&&p.bot==='normal'));
  s.addPlayer('h2');assert.ok(!s.players.has(PARTY_BOT_PREFIX+'3'),'newest party bot evicted');assert.ok(s.players.has(PARTY_BOT_PREFIX+'2'));
  s.addPlayer('h3');s.addPlayer('h4');assert.equal(s.pve!.partyBots().length,0);
  assert.equal(s.joinable(),false);assert.equal(s.addPlayer('h5'),null,'5th human refused');
  assert.equal(s.addPlayer('bot-x','Bot','ranged','hard'),null,'no foreign bots');
  assert.equal(s.addPlayer(BOSS_PLAYER_ID),null,'boss seat only while spawning');
  assert.equal(s.configureLobby({botCount:3}),false);assert.equal(s.setTeam('h1','red'),false);
  assert.deepEqual(s.pve!.slots(),['h1',PARTY_BOT_PREFIX+'1',PARTY_BOT_PREFIX+'2','h2','h3','h4'].filter(id=>s.players.has(id)));
 }finally{s.dispose();}
 // Humans may not take the Siren in PvE (D23).
 const t=new Simulation({pve:pve()});try{const p=t.addPlayer('dev','Dev','melee',null,'#fff',{preferredSpec:'siren'},true)!;assert.notEqual(p.spec,'siren');hello(t,'dev');t.setReady('dev',true);assert.equal(t.selectSpec('dev','siren'),false);}finally{t.dispose();}
 // HostedRoom (relay/worker host): ready without a hello is an error, fatal for guests.
 const sent:{target:string;message:any}[]=[];
 const room=new HostedRoom('ABCDEF',{name:'Host',class:'ranged',color:'#ffffff'},false,(target,message)=>sent.push({target,message}),6,false,false,undefined,{pve:{boss:'siren'}});
 try{
  assert.ok(room.sim.pve);assert.equal(room.sim.pve.options.relay,true);assert.equal(room.sim.pve.options.debug,undefined);
  assert.equal(room.join({name:'Guest',class:'melee'},false,'guest'),true);
  assert.equal(room.receive('guest',{type:'ready',ready:true}),false);
  assert.deepEqual(sent.at(-1),{target:'guest',message:{type:'error',fatal:true,message:'Update the game to join Boss Test rooms.'}});
  assert.equal(room.receive('host',{type:'ready',ready:true}),false);assert.equal(sent.at(-1)!.message.fatal,false);
  assert.equal(room.receive('guest',{type:'chip',pve:{action:'hello',v:1,key:KEY,n:1}}),true);assert.equal(room.sim.pve.helloed('guest'),true);
  assert.equal(room.receive('host',{type:'chip',pve:{action:'hello',v:1}}),true);
  assert.equal(room.receive('guest',{type:'ready',ready:true}),true);assert.equal(room.receive('host',{type:'ready',ready:true}),true);assert.equal(room.sim.phase,'specialization');
 }finally{room.dispose();}
 // PvP join and ready are unchanged.
 const pvp:{target:string;message:any}[]=[];const plain=new HostedRoom('ABCDEG',{name:'Host',class:'ranged',color:'#ffffff'},false,(target,message)=>pvp.push({target,message}));
 try{assert.equal(plain.sim.pve,undefined);assert.equal(plain.join({name:'Guest',class:'melee'},false,'guest'),true);assert.equal(plain.receive('guest',{type:'ready',ready:true}),true);assert.equal(plain.receive('host',{type:'ready',ready:true}),true);assert.equal(plain.sim.phase,'specialization');
  assert.ok(!pvp.some(m=>m.message.type==='error'));assert.equal(plain.receive('guest',{type:'chip',pve:{action:'hello',v:1}}),false);}finally{plain.dispose();}
});
test('damageRoom clamps damage() to the director floor, including debris and huge hits (acceptance 4)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim({},true);try{
  const boss=s.players.get(BOSS_PLAYER_ID)!,d=made.director!;boss.stats={...boss.stats,hp:1000};boss.hp=1000;d.floor=750;
  s.damage(boss,100,'h1','direct');assert.equal(boss.hp,1000-100*boss.stats.damageTaken);
  s.damage(boss,10000,'h1','direct');assert.equal(boss.hp,750);assert.equal(s.damage(boss,50,'h1','direct'),0);assert.equal(boss.hp,750);
  boss.hp=800;boss.stats={...boss.stats,debrisTaken:2.5};s.damage(boss,100,'h1','debris');assert.ok(Math.abs(boss.hp-750)<1e-9,`debris clamp ${boss.hp}`);
  d.floor=undefined;s.damage(boss,10,'h1','direct');assert.ok(boss.hp<750);
  assert.equal(s.pve!.stats('h1').bossDamage>0,true);assert.ok(made.economy!.bossDamage.length>0);
 }finally{s.dispose();}});
});
test('untouchable stops damage(), Siren hits, Siren projectiles and the laugh catch (acceptance 4, 6)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim({},true);try{
  const h=s.players.get('h1')!,boss=s.players.get(BOSS_PLAYER_ID)!;
  s.pve!.debug('god:on');assert.equal(s.pve!.untouchable(h),true);
  assert.equal(s.damage(h,30,BOSS_PLAYER_ID,'direct'),0);assert.equal(s.siren.hit(boss,h,30,'swipe'),0);assert.equal(h.hp,h.stats.hp);
  // Siren projectile toward the untouchable human passes through.
  place(boss,0,0,0);place(h,0,4);s.world.step();
  const shoot=()=>{const shot=s.siren.spawn(boss,'hellfire')!;for(let i=0;i<120&&s.projectiles.has(shot.id);i++)s.siren.stepShot(shot,1/60);};
  shoot();assert.equal(h.hp,h.stats.hp,'god blocks projectiles');
  s.pve!.debug('god:off');s.pve!.setUntouchable('h1',s.time+5);assert.equal(s.damage(h,30,BOSS_PLAYER_ID,'direct'),0);
  s.pve!.setUntouchable('h1',s.time-1);shoot();assert.ok(h.hp<h.stats.hp,'visible human is hit');
  // Laugh catch: untouchable humans cannot be caught.
  const laugh=(untouchable:boolean)=>{h.hp=h.stats.hp;place(boss,0,0,0);place(h,0,1);s.siren.cancel(boss);boss.cooldown=0;boss.siren!.ready={};boss.siren!.mode='melee';
   s.pve!.setUntouchable('h1',untouchable?s.time+30:s.time-1);assert.equal(s.siren.start(boss,'laugh'),true);
   for(let i=0;i<200&&!s.siren.grabs.length;i++){s.time+=1/60;s.siren.stepPlayer(boss,1/60);}return s.siren.grabs.length>0;};
  assert.equal(laugh(true),false);assert.equal(laugh(false),true);
  void made;
 }finally{s.dispose();}});
});
test('hidden stops homing, stage hazards, chaos-zombie targeting and siren.ready (acceptance 4, 5)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim({},true);try{
  const h=s.players.get('h1')!,boss=s.players.get(BOSS_PLAYER_ID)!;
  assert.equal(s.siren.ready(boss),true);s.pve!.setHidden(boss,true);assert.equal(s.siren.ready(boss),false);assert.equal(s.pve!.untouchable(boss),true);
  // Homing: a homing shot bends toward a visible enemy but not toward the hidden boss.
  const bend=(hidden:boolean)=>{s.pve!.setHidden(boss,hidden);s.projectiles.clear();place(h,0,0,0);place(boss,1.6,12);s.world.step();h.stats={...h.stats,homing:40};h.cooldown=0;h.ammo=10;h.reloadTime=0;
   s.spawnProjectile(h,1);const shot=[...s.projectiles.values()][0];const x0=shot.vel.x;s.stepProjectiles(1/60);return shot.vel.x-x0;};
  assert.ok(bend(false)>0,'bends toward the visible boss');assert.equal(bend(true),0,'ignores the hidden boss');
  // Stage hazards: a hidden player on an active vent is skipped.
  let t=0;while(!sirenStageHazards(t).some(x=>x.active)&&t<400)t+=.05;const vent=sirenStageHazards(t).find(x=>x.active)!;
  s.bossSong={track:'black-siren-protocol',startedAt:s.time-t};place(boss,vent.pos.x,vent.pos.z);s.events=[];s.stageHazards.step();
  assert.ok(!s.events.some(e=>e.target===BOSS_PLAYER_ID),'hidden boss untouched by vents');
  s.pve!.setHidden(boss,false);place(h,vent.pos.x,vent.pos.z);s.stageHazards.step();assert.ok(s.events.some(e=>e.target==='h1'),'visible player hit by the vent');
  assert.ok(SIREN_VENTS.some(x=>x.id===vent.id));
  // Chaos zombies: a party player's zombie with no visible enemy assists against adds.
  s.bossSong=undefined;place(boss,0,20);place(h,0,0);s.chaos.summon(h,1,'zombie');s.pve!.setHidden(boss,true);s.chaos.step(1/60);
  assert.equal(made.horde!.assists.length,1);assert.equal(made.horde!.assists[0].owner,'h1');
  s.pve!.setHidden(boss,false);s.chaos.step(1/60);assert.equal(made.horde!.assists.length,1,'visible boss is chased instead');
 }finally{s.dispose();}});
});
test('filterInput idles choosers and channellers keeping seq and look; holds freeze and shift every part (acceptance 4)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim();try{
  const h=s.players.get('h1')!,input:Input={...idleInput(),seq:5,x:1,z:-1,yaw:.7,pitch:.2,attack:true,jump:true,block:true,interact:true,camera:'first'};
  made.economy!.choosingIds.add('h1');assert.equal(s.queueInputs('h1',[input],epoch(s)),true);s.preparePlayer(h,1/60);
  assert.deepEqual(h.input,{...idleInput(),seq:5,yaw:.7,pitch:.2,camera:'first'});assert.equal(h.ack,5);
  made.economy!.choosingIds.clear();made.economy!.channellingIds.add('h1');s.queueInputs('h1',[{...input,seq:6}],epoch(s));s.preparePlayer(h,1/60);assert.equal(h.input.x,0);assert.equal(h.input.seq,6);
  made.economy!.channellingIds.clear();s.queueInputs('h1',[{...input,seq:7}],epoch(s));s.preparePlayer(h,1/60);assert.equal(h.input.x,1);assert.equal(h.input.interact,true,'interact reaches the authority');
  // Hold branch.
  place(h,2,2);for(let i=0;i<10;i++)s.step();s.spawnProjectile(h,1);const shot=[...s.projectiles.values()][0],shotPos={...shot.pos};
  h.body.setLinvel(v(6,0,0),true);made.director!.hold=true;const x0=h.body.translation().x,z0=h.body.translation().z;
  h.burns.set('x',{dps:5,until:s.time+3,next:s.time+.5});const burn={...h.burns.get('x')!};s.pve!.setUntouchable('h1',s.time+1);const shield=s.time+1;
  const boss=spawnBoss(s,{x:-3,z:-3});boss.body.setLinvel(v(0,0,5),true);const bz=boss.body.translation().z;
  for(let i=0;i<30;i++){s.queueInputs('h1',[{...input,seq:100+i}],epoch(s));s.step();}
  assert.ok(Math.abs(boss.body.translation().z-bz)<1e-6,'boss frozen too');
  assert.ok(Math.abs(h.burns.get('x')!.next-(burn.next+30/60))<1e-9&&Math.abs(h.burns.get('x')!.until-(burn.until+30/60))<1e-9,'DoT timers shifted');
  assert.equal(s.pve!.untouchable(h),true);s.pve!.setUntouchable('h1',shield+30/60);
  assert.ok(Math.abs(h.body.translation().x-x0)<1e-6&&Math.abs(h.body.translation().z-z0)<1e-6,'party frozen');
  assert.deepEqual(shot.pos,shotPos,'projectiles skipped');assert.equal(h.input.attack,false);
  for(const part of [made.horde!,made.economy!,made.recruits!])assert.equal(part.shifts.length,30);
  assert.equal(made.director!.holdSteps.length,30);const steps=made.director!.steps.length;made.director!.hold=false;s.step();assert.equal(made.director!.steps.length,steps+1);
  // No overtime in PvE.
  s.fightStart=s.time-C.maxRoundSeconds-50;const hp=h.hp;for(let i=0;i<20;i++)s.step();assert.ok(h.hp>=hp,'no overtime damage');assert.equal(s.phase,'fight');
 }finally{s.dispose();}});
});
test('no disconnect forfeit and no one-team round end in PvE (acceptance 4)',()=>{
 const s=fightSim();try{
  s.addPlayer('h2');s.phase='fight';s.disconnect('h2');assert.equal(s.players.get('h2')?.connected,false);
  s.time+=C.disconnectSeconds+1;s.step();assert.equal(s.phase,'fight');assert.equal(s.players.get('h2')!.hp,0);
  s.damage(s.players.get('h1')!,100000,null,'fall');for(let i=0;i<5;i++)s.step();assert.equal(s.phase,'fight','the director ends runs');
 }finally{s.dispose();}
});
test('boss intro during fight: introFrame and the boss song; PvP unchanged (acceptance 4)',()=>{
 const s=fightSim({},true);try{
  s.intro={started:s.time,players:[BOSS_PLAYER_ID],boss:true};s.bossSong=undefined;
  const frame=introFrame(s.snapshot());assert.equal(frame?.boss,true);assert.equal(frame?.player.id,BOSS_PLAYER_ID);assert.equal(frame?.style,'dev-siren');
  assert.equal(s.snapshot().intro?.boss,true);s.updateBossSong();assert.equal((s.bossSong as {startedAt:number}|undefined)?.startedAt,s.intro!.started);
 }finally{s.dispose();}
 const p=new Simulation();try{p.addPlayer('a');p.addPlayer('b');p.phase='fight';p.intro={started:p.time,players:['a']};assert.equal(introFrame(p.snapshot()),null);
  const q=new Simulation();try{q.addPlayer('a');q.addPlayer('b');q.beginIntros();assert.equal(introFrame(q.snapshot())?.boss,undefined);}finally{q.dispose();}}finally{p.dispose();}
});
test('friendliness matrix and projectile pass-through against a fake add (acceptance 5)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=new Simulation({pve:pve({partyBots:1})});try{
  s.addPlayer('h1');hello(s,'h1');spawnBoss(s);s.phase='fight';
  const bot=PARTY_BOT_PREFIX+'1';
  assert.equal(s.canHit('h1',HORDE_ID),true);assert.equal(s.canHit(bot,HORDE_ID),true);assert.equal(s.canHit(BOSS_PLAYER_ID,HORDE_ID),false);
  assert.equal(s.canHit('h1','add-3'),true);assert.equal(s.canHit(HORDE_ID,BOSS_PLAYER_ID),false);assert.equal(s.canHit(HORDE_ID,'h1'),true);assert.equal(s.canHit('h1',bot),false);
  const h=s.players.get('h1')!,boss=s.players.get(BOSS_PLAYER_ID)!;
  place(boss,0,0,0);place(h,0,6);const add=made.horde!.addAt({x:0,z:3});add.body.setBodyType(RAPIER.RigidBodyType.Fixed,true);s.world.step();
  // The Siren's shot passes the add and reaches the human.
  const shot=s.siren.spawn(boss,'hellfire')!;for(let i=0;i<120&&s.projectiles.has(shot.id);i++)s.siren.stepShot(shot,1/60);
  assert.ok(h.hp<h.stats.hp,'Siren shot reached the human');assert.equal(made.horde!.hits.length,0);
  // The human's shot hits the add through the real chaos.hitMinion path, with hit info.
  place(h,0,6,Math.PI);h.cooldown=0;h.ammo=10;h.reloadTime=0;s.world.step();s.spawnProjectile(h,1);for(let i=0;i<60&&s.projectiles.size;i++)s.stepProjectiles(1/60);
  assert.equal(made.horde!.hits.length,1);const hit=made.horde!.hits[0];assert.equal(hit.attacker,'h1');assert.ok(hit.hit?.point);assert.ok(['bullet','energy'].includes(hit.hit!.kind!));
  // Explosions pass blast hit info; the Siren cannot hurt adds through chaos.hitMinion.
  s.explode(h,{x:0,y:1,z:3},3,40,0,'blast');assert.equal(made.horde!.hits.at(-1)!.hit?.kind,'blast');assert.equal(made.horde!.hits.at(-1)!.hit?.radius,3);
  const count=made.horde!.hits.length;s.chaos.hitMinion(add,50,BOSS_PLAYER_ID);assert.equal(made.horde!.hits.length,count);
  // Kills fan out to the ledger, economy and director.
  s.chaos.hitMinion(add,1000,'h1');assert.equal(s.chaos.minions.has(add.id),false);assert.equal(s.pve!.stats('h1').kills,1);assert.equal(made.economy!.kills.length,1);assert.equal(made.director!.kills.length,1);
  assert.equal(s.chaos.views().length,0,'adds never appear in chaos minion views');
 }finally{s.dispose();}});
});
test('Siren boost: reach, cooldown, caps and the D35 laugh rules (acceptance 6)',()=>{
 const s=fightSim({},true);try{
  const boss=s.players.get(BOSS_PLAYER_ID)!,h=s.players.get('h1')!,a=s.siren.state(boss);
  const run=(id:'slam'|'rend'|'laugh'|'rain',mode:'melee'|'ranged'='melee')=>{s.siren.cancel(boss);boss.cooldown=0;a.ready={};a.mode=mode;assert.equal(s.siren.start(boss,id),true,id);return a.move!;};
  const fire=(m:{hitAt:number})=>{while(s.time<m.hitAt+1e-9){s.time+=1/60;s.siren.stepPlayer(boss,1/60);}};
  place(boss,0,0,0);place(h,0,20);s.world.step();
  s.events=[];fire(run('slam'));assert.equal(s.events.find(e=>e.type==='siren-slam')?.radius,SIREN_COMBAT.slam.radius);
  a.boost={scale:1,reach:1.5,cooldown:1,at:s.time};
  s.events=[];const slam=run('slam');assert.equal(slam.reach,1.5);fire(slam);assert.ok(Math.abs(s.events.find(e=>e.type==='siren-slam')!.radius!-5.4)<1e-9);
  const lm=run('laugh');assert.equal(lm.reach,1,'laugh lunge unboosted');
  const step=sirenMotion({...lm,dir:{x:0,y:0,z:1},endAt:lm.hitAt+SIREN_TIMING.laugh.lunge},lm.hitAt,1/60).step;
  assert.ok(Math.abs(step.z-SIREN_COMBAT.laugh.range/SIREN_TIMING.laugh.lunge)<1e-9,'lunge range still 7');
  // Catch radius grows to 1.575: a target at 1.5 m is caught only with the boost.
  const caught=(boost:boolean)=>{a.boost=boost?{scale:1,reach:1.5,cooldown:1,at:s.time}:undefined;h.hp=h.stats.hp;s.siren.cancel(h);place(boss,0,0,0);place(h,0,1.5);
   const m=run('laugh');m.reach=boost?1:undefined;for(let i=0;i<180&&!s.siren.grabs.length&&s.time<m.hitAt+.34;i++){s.time+=1/60;place(boss,0,0,0);s.siren.stepPlayer(boss,1/60);}const r=s.siren.grabs.length>0;s.siren.cancel(boss);return r;};
  assert.equal(caught(false),false);assert.equal(caught(true),true);
  // Cooldown 0 (owner D35: no laugh floor): every move is ready again at its start; only the move itself gates re-cast.
  a.boost={scale:1,reach:1,cooldown:0,at:s.time,minRecast:PVE_SIREN.play.minRecast,maxRains:2,maxEruptions:4};
  const rend=run('rend');assert.equal(a.ready!.rend,rend.at);assert.ok(boss.cooldown>0);
  const laugh=run('laugh');assert.equal(a.ready!.laugh,laugh.at);
  s.siren.rains=[{id:'r1',owner:BOSS_PLAYER_ID,center:v(),radius:7,at:s.time,until:s.time+10,next:s.time+10},{id:'r2',owner:BOSS_PLAYER_ID,center:v(),radius:7,at:s.time,until:s.time+10,next:s.time+10}];
  s.siren.cancel(boss);boss.cooldown=0;a.mode='ranged';assert.equal(s.siren.start(boss,'rain'),false,'rains capped at 2');
  s.siren.rains.pop();assert.equal(s.siren.start(boss,'rain'),true);
 }finally{s.dispose();}
});
test('PvE crush is a damage() hit scaled by party size; clearEffects keeps the Siren state (acceptance 6)',()=>{
 const s=fightSim({},true);try{
  const boss=s.players.get(BOSS_PLAYER_ID)!,h=s.players.get('h1')!,a=s.siren.state(boss);
  h.stats={...h.stats,hp:100,damageTaken:1};h.hp=100;place(boss,0,0,0);place(h,0,1);s.world.step();a.mode='melee';boss.cooldown=0;
  assert.equal(s.siren.start(boss,'laugh'),true);for(let i=0;i<200&&!s.siren.grabs.length;i++){s.time+=1/60;s.siren.stepPlayer(boss,1/60);}assert.equal(s.siren.grabs.length,1);
  const g=s.siren.grabs[0];s.time=g.crushAt;s.events=[];s.siren.step(1/60);
  const left=100-100*PVE_SIREN.laughCrush*damageScale(1);assert.ok(left>0&&left<100);assert.ok(Math.abs(h.hp-left)<1e-9,`crush leaves ${left} HP (${h.hp})`);assert.equal(h.death,undefined);assert.ok(s.events.some(e=>e.type==='siren-crush'));assert.equal(s.siren.grabs.length,0);
  // A second grab, then clearEffects releases it and keeps boss.siren.
  s.siren.cancel(boss);boss.cooldown=0;a.ready={};place(boss,0,0,0);place(h,0,1);assert.equal(s.siren.start(boss,'laugh'),true);
  for(let i=0;i<200&&!s.siren.grabs.length;i++){s.time+=1/60;s.siren.stepPlayer(boss,1/60);}assert.equal(s.siren.grabs.length,1);
  s.siren.patches.push({id:'f',owner:BOSS_PLAYER_ID,kind:'hellfire',pos:v(),radius:1,at:s.time,until:s.time+5});
  s.siren.clearEffects(boss);assert.equal(s.siren.grabs.length,0);assert.equal(s.siren.patches.length,0);assert.ok(boss.siren);assert.equal(boss.siren!.move,undefined);
 }finally{s.dispose();}
});
test('an add between the boss and a target changes neither aim() nor visibleTarget (acceptance 6)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim({},true);try{
  const boss=s.players.get(BOSS_PLAYER_ID)!,h=s.players.get('h1')!;place(boss,0,0,0);place(h,0,8);for(const p of [boss,h])p.body.setBodyType(RAPIER.RigidBodyType.Fixed,true);boss.input.camera='third';s.world.step();
  const vt=(s.siren as unknown as {visibleTarget(p:Player,r:number):Player|undefined});const aim0=s.siren.aim(boss),target0=vt.visibleTarget(boss,20)?.id;
  const add=made.horde!.addAt({x:0,z:4});add.body.setBodyType(RAPIER.RigidBodyType.Fixed,true);s.world.step();
  assert.deepEqual(s.siren.aim(boss),aim0);assert.equal(vt.visibleTarget(boss,20)?.id,target0);assert.equal(target0,'h1');
  assert.equal(s.lineOfSight(v(0,.9,0),v(0,.9,8),BOSS_PLAYER_ID,'h1'),false,'plain LOS still sees the add');
 }finally{s.dispose();}});
});
test('ground rays: standard capsules unchanged, groundRay grounds a 1.5x capsule, groundGroups ignore adds (acceptance 7)',()=>{
 const s=new Simulation({pve:pve()});try{
  const w=s.world,mk=(y:number,hh:number,r:number,groups?:number)=>{const body=w.createRigidBody(RAPIER.RigidBodyDesc.fixed().setTranslation(30,y,0));const c=RAPIER.ColliderDesc.capsule(hh,r);if(groups!==undefined)c.setCollisionGroups(groups);return {body,collider:w.createCollider(c,body)};};
  const grounded=(o:{body:RAPIER.RigidBody;collider:RAPIER.Collider},extra:{groundRay?:number;groundGroups?:number}={})=>{const st={move:v(),ext:v(),dashCd:0,dashTime:0,grounded:false};
   prepareMotion(w,o.body,o.collider,st,idleInput(),{speed:1,dashCooldown:1,knockbackTaken:1,...extra},1/60);return st.grounded;};
  const big=mk(.645+.63+.02,.645,.63);w.step();
  assert.equal(grounded(big),false,'standard ray too short for the grown capsule');assert.equal(grounded(big,{groundRay:(.43+.42)*1.5+.16}),true);
  const std=mk(.43+.42+.02,.43,.42);w.step();assert.equal(grounded(std),true);
  w.removeRigidBody(big.body);w.removeRigidBody(std.body);
  const add=mk(.9,.43,.42,PVE_GROUPS.add),human=mk(.9+.85+.85+.05,.43,.42,PVE_GROUPS.player);w.step();
  assert.equal(grounded(human),true,'without groups the human stands on the add');assert.equal(grounded(human,{groundGroups:PVE_GROUPS.player}),false);
  void add;const p=s.addPlayer('h1')!;assert.deepEqual(s.pve!.motion(p),{groundGroups:PVE_GROUPS.player});
 }finally{s.dispose();}
});
test('offerChips: default path identical to the base; weights replace tiers and exclusives can be skipped (acceptance 8)',()=>{
 const specs=['pistol','rifle','sword','daggers','fire','water','siren','emperor','leet','ansatsuken'] as const,out:string[][]=[];
 // The 2026-10-03 damage/health chips (power-chips.ts) grew the catalogue on purpose; the base comparison runs on the
 // base catalogue so it still proves the default code path is unchanged.
 const saved=[...CHIPS],added=new Set(POWER_CHIPS.map(c=>c.id));CHIPS.splice(0,CHIPS.length,...saved.filter(c=>!added.has(c.id)));
 try{
 for(let seed=1;seed<=50;seed++)for(const spec of specs)for(const chips of [{},{vital:1,ricochet:2}] as Record<string,number>[]){let x=seed*2654435761>>>0;const random=()=>{x=(Math.imul(x,1664525)+1013904223)>>>0;return x/4294967296;};out.push(offerChips(spec as never,chips,random,seed%2?6:8));}
 // Generated on the untouched base b6d2f58 with the same loop.
 assert.equal(createHash('sha256').update(JSON.stringify(out)).digest('hex').slice(0,24),'9816b7ffe84fda7dd350f305');
 }finally{CHIPS.splice(0,CHIPS.length,...saved);}
 let n=1;const random=()=>{n=(Math.imul(n,1664525)+1013904223)>>>0;return n/4294967296;};
 const rarity=(id:string)=>CHIPS.find(c=>c.id===id)!.rarity;
 const pistol=offerChips('pistol',{},random,8,{weights:{mythic:1},exclusives:false});assert.equal(pistol.length,8);assert.ok(pistol.every(id=>rarity(id)==='mythic'));
 const sword=offerChips('sword',{},random,8,{weights:{mythic:1},exclusives:false}).map(rarity).sort();assert.deepEqual(sword,['legendary',...Array(7).fill('mythic')]);
 for(let i=0;i<20;i++)assert.ok(offerChips('rifle',{},random,6,{weights:{common:1}}).every(id=>rarity(id)==='common'));
});
test('rejoin: rejoinSeat matches a disconnected human key and adoptSeat restores state and re-keys every part (acceptance 9)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim();try{
  const h=s.players.get('h1')!;h.chips={vital:1};h.hp=42;place(h,3,-4,1.2);s.pve!.stats('h1').kills=5;made.economy!.moneyBy.set('h1',300);
  assert.equal(s.pve!.rejoinSeat(KEY),undefined,'connected seats cannot be taken');
  s.disconnect('h1');h.input.yaw=1.2;assert.equal(s.pve!.rejoinSeat('f'.repeat(32)),undefined);assert.equal(s.pve!.rejoinSeat(KEY),'h1');
  assert.equal(s.pve!.adoptSeat('h1','h1b'),true);assert.equal(s.players.has('h1'),false);
  const p=s.players.get('h1b')!;assert.equal(p.connected,true);assert.equal(p.hp,42);assert.deepEqual(p.chips,{vital:1});assert.equal(p.team,'blue');assert.equal(p.name,'Ana');
  assert.ok(Math.abs(p.body.translation().x-3)<1e-6);assert.equal(p.input.yaw,1.2);assert.equal(s.pve!.helloed('h1b'),true);assert.equal(s.pve!.stats('h1b').kills,5);
  for(const part of [made.horde!,made.economy!,made.recruits!])assert.deepEqual(part.rekeys,[['h1','h1b']]);
  assert.ok(s.events.some(e=>e.type==='pve-rejoin'&&e.target==='h1b'));assert.deepEqual(s.pve!.slots(),['h1b']);
 }finally{s.dispose();}});
});
test('commands: hello first wins, debug needs options.debug and a trusted sender, others reach the economy for humans',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=new Simulation({pve:pve({debug:true,partyBots:1})});try{
  s.addPlayer('h1');assert.equal(s.pve!.command('h1',{action:'hello',v:1,key:KEY},false),true);s.pve!.command('h1',{action:'hello',v:1,key:'f'.repeat(32)},false);
  assert.equal(s.pve!.command('h1',{action:'debug',op:'god:on'},false),false);assert.equal(s.pve!.godMode,false);
  assert.equal(s.pve!.command('h1',{action:'debug',op:'god:on'},true),true);assert.equal(s.pve!.godMode,true);
  assert.equal(s.pve!.command('h1',{action:'debug',op:'fake-horde:x'},true),true);assert.deepEqual(made.horde!.debugOps.at(-1),'fake-horde:x');
  assert.equal(s.pve!.command('h1',{action:'open',n:2},false),true);assert.deepEqual(made.economy!.commands,[{id:'h1',cmd:{action:'open',n:2}}]);
  assert.equal(s.pve!.command(PARTY_BOT_PREFIX+'1',{action:'open'},false),false,'humans only');
  assert.equal(s.pve!.command('h1',{action:'bogus'},true),false);assert.equal(s.pve!.command('nobody',{action:'hello',v:1},true),false);
  s.phase='fight';s.pve!.setHidden(s.players.get('h1')!,false);s.pve!.onFightStart();s.pve!.setUntouchable('h1',0);assert.equal(s.pve!.solo,false,'a party bot means not solo');
  // Party bots idle until S3 provides a brain; a scripted brain is used when present.
  const bot=s.players.get(PARTY_BOT_PREFIX+'1')!;s.preparePlayer(bot,1/60);assert.equal(bot.input.attack,false);assert.equal(bot.input.x,0);
  made.recruits!.inputs.set(bot.id,p=>({...idleInput(),seq:p.input.seq,x:1}));s.preparePlayer(bot,1/60);assert.equal(bot.input.x,1);
 }finally{s.dispose();}
 const plain=new Simulation({pve:pve()});try{plain.addPlayer('h1');assert.equal(plain.pve!.command('h1',{action:'debug',op:'god:on'},true),false,'no debug without options.debug');}finally{plain.dispose();}});
});
test('reset: new run seed, boss and recruits removed, party bots kept, parts reset; rescueFall returns the boss',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim({partyBots:1},true);try{
  const boss=s.players.get(BOSS_PLAYER_ID)!;boss.body.setTranslation(v(5,-8,5),true);assert.equal(s.pve!.rescueFall(boss),true);
  assert.ok(Math.abs(boss.body.translation().z-PVE_SIREN.spawn.z)<1e-6);assert.equal(s.pve!.rescueFall(s.players.get('h1')!),false);
  s.pve!.setHidden(boss,true);const resets=made.horde!.resets,seed=s.pve!.runSeed;s.resetRound();
  assert.equal(s.players.has(BOSS_PLAYER_ID),false);assert.ok(s.players.has(PARTY_BOT_PREFIX+'1'));assert.notEqual(s.pve!.runSeed,seed);assert.equal(made.horde!.resets,resets+1);
  assert.ok(s.props.has('pve-machine-3'));
 }finally{s.dispose();}});
});
test('revivePlayer and removePlayer (PvE helpers)',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim();try{
  const h=s.players.get('h1')!;s.damage(h,100000,null,'fall');assert.ok(h.death);assert.deepEqual(made.economy!.deaths,['h1']);assert.equal(s.pve!.stats('h1').deaths,1);
  s.revivePlayer(h,.5,s.pve!.standAt(1,2,h));assert.equal(h.death,undefined);assert.equal(h.hp,h.stats.hp*.5);assert.deepEqual(made.economy!.revives,['h1']);
  assert.ok(s.events.some(e=>e.type==='pve-revive'&&e.target==='h1'));assert.ok(Math.abs(h.body.translation().y-(.43+.42+.05))<1e-6);
  assert.equal(s.removePlayer('h1'),true);assert.equal(s.players.size,0);assert.equal(s.removePlayer('h1'),false);
 }finally{s.dispose();}});
});
test('offline worker room: no guests, debug only from a developer host, pause stops stepping (acceptance 12)',async()=>{
 const sent:any[]=[];const dev=new HostedRoom('LOCAL1',{name:'Dev',class:'ranged',color:'#ffffff'},false,(t,m)=>sent.push({t,m}),6,true,false,undefined,{local:true,pve:{boss:'siren'}});
 try{
  assert.equal(dev.local,true);assert.equal(dev.sim.pve!.options.local,true);assert.equal(dev.sim.pve!.options.debug,true);assert.equal(dev.sim.pve!.options.relay,undefined);
  assert.equal(dev.join({name:'Guest',class:'ranged'},false,'guest'),false);
  assert.equal(dev.receive('host',{type:'chip',pve:{action:'debug',op:'god:on'}}),true);assert.equal(dev.sim.pve!.godMode,true);
  assert.equal(dev.sim.pve!.command('guest',{action:'debug',op:'god:off'},false),false);
 }finally{dev.dispose();}
 const user=new HostedRoom('LOCAL2',{name:'Ana',class:'ranged',color:'#ffffff'},false,()=>{},6,false,false,undefined,{local:true,pve:{boss:'siren'}});
 try{assert.equal(user.sim.pve!.options.debug,undefined);assert.equal(user.receive('host',{type:'chip',pve:{action:'debug',op:'god:on'}}),false);}finally{user.dispose();}
 // host-worker.ts with a stub worker scope.
 const g=globalThis as any,intervals:any[]=[],realSet=g.setInterval;const messages:any[]=[];
 g.self={postMessage:(m:any)=>messages.push(m)};g.setInterval=(...a:any[])=>{const h=realSet(...a);intervals.push(h);return h;};
 try{
  await import('../src/client/host-worker.js');
  const snapshots=()=>messages.filter(m=>m.message?.type==='snapshot').length,wait=(ms:number)=>new Promise(r=>setTimeout(r,ms));
  await g.self.onmessage({data:{type:'create',code:'LOCAL3',profile:{name:'Ana',class:'ranged'},bothDraft:false,draftChoices:6,skipIntros:true,settings:{local:true,pve:{boss:'siren'}}}});
  await wait(150);assert.ok(snapshots()>0,'stepping');
  await g.self.onmessage({data:{type:'pause',paused:true}});await wait(30);const paused=snapshots();await wait(200);assert.equal(snapshots(),paused,'paused');
  await g.self.onmessage({data:{type:'pause',paused:false}});await wait(150);assert.ok(snapshots()>paused,'resumed');
  await g.self.onmessage({data:{type:'create',code:'LOCAL4',profile:{name:'Ana',class:'ranged'},bothDraft:false,draftChoices:6,skipIntros:true,settings:{}}});
  await g.self.onmessage({data:{type:'pause',paused:true}});const before=snapshots();await wait(150);assert.ok(snapshots()>before,'non-local rooms ignore pause');
 }finally{for(const h of intervals)clearInterval(h);g.setInterval=realSet;delete g.self;}
});
test('party size counts connected humans and party bots; PVE caps',()=>{
 const s=new Simulation({pve:pve({partyBots:2})});try{s.addPlayer('h1');s.addPlayer('h2');assert.equal(s.pve!.partySize(),4);s.phase='fight';s.disconnect('h2');assert.equal(s.pve!.partySize(),3);assert.equal(PVE.maxPartyBots,3);}finally{s.dispose();}
});
test('contract extras: solo flag, base input to inputFor, FakeEconomy.openPick, recruit dismiss through the fake',()=>{
 const {factory,made}=fakeParts();withPveParts(factory,()=>{const s=fightSim();try{
  s.pve!.onFightStart();assert.equal(s.pve!.solo,true);const host=made.host!;assert.equal(host.solo,true);
  const h=s.players.get('h1')!;assert.equal(host.untouchable(h),false);
  const pick=made.economy!.openPick('h1');const row=s.snapshot().pve!.party.h1;assert.equal(row.open,pick);assert.ok(row.picks[0].offers!.length>0);
  s.queueInputs('h1',[{...idleInput(),seq:9,x:1,interact:true}],epoch(s));s.preparePlayer(h,1/60);
  assert.equal(made.economy!.baseInputs.at(-1)!.base!.interact,true,'raw base input reaches the economy');assert.equal(h.input.x,0);
  const r=s.addPlayer('recruit-1','dps · Rook','ranged','normal','#fff',{preferredSpec:'pistol'},{developer:false,unlocks:[]})!;assert.ok(r);
  made.recruits!.recruitViews=[{id:'recruit-1',owner:'h1',role:'dps',at:s.time,m:4}];assert.equal(made.recruits!.newest(),'recruit-1');
  assert.deepEqual(made.recruits!.dismiss('recruit-1'),{owner:'h1'});assert.equal(s.players.has('recruit-1'),false);assert.equal(made.recruits!.dismiss('recruit-1'),null);
 }finally{s.dispose();}});
});
