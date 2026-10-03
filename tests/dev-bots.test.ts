import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {botWeapon} from '../src/shared/lobby-presentation.js';
import {botLoadouts} from '../src/client/lobby-ui.js';
import {SIREN_SLOTS} from '../src/shared/siren-contract.js';
import {kitFor,abilityActive} from '../src/shared/abilities.js';
import {v,type SpecId,type Difficulty} from '../src/shared/config.js';
await initPhysics();

test('DEV bot choices are hidden from guests and item-granted accounts, and stale selections become random',()=>{
 for(const access of [false,{developer:false,unlocks:['weapon:siren','weapon:emperor']}]){
  const html=botLoadouts(2,['siren','emperor'],access);
  for(const w of Object.values(WEAPONS).filter(w=>w.devOnly)){assert.equal(botWeapon(w.id,access),'random');assert.ok(!html.includes(`value="${w.id}"`));}
  assert.equal((html.match(/value="random" selected/g)||[]).length,2);
 }
 const html=botLoadouts(2,['siren','random'],true);
 for(const w of Object.values(WEAPONS).filter(w=>w.devOnly))assert.ok(html.includes(`value="${w.id}"`));
 assert.ok(html.includes('<optgroup label="???">'));
 assert.equal((html.match(/value="siren"[^>]*disabled/g)||[]).length,1,'other slots cannot choose a second Siren');
});

test('only the verified configuring DEV may grant bots their selected weapon; humans and bots gain no DEV role',()=>{
 for(const developer of [false,true]){
  const room=new HostedRoom('BOTS2345',{name:'Host',class:'ranged',color:'#ffffff'},false,()=>{},6,developer);
  try{
   room.join({name:'DEV guest',class:'ranged'},true,'dev');
   assert.equal(room.receive('dev',{type:'settings',botCount:1,botWeapons:['siren']}),false);
   assert.equal(room.receive('host',{type:'settings',botCount:4,botWeapons:['siren','emperor','ansatsuken','leet'],developer:true,actorId:'dev'}),true);
   const bots=[...room.sim.players.values()].filter(p=>p.bot);
   if(developer){
    assert.deepEqual(bots.map(p=>p.spec),['siren','emperor','ansatsuken','leet']);
    for(const p of bots){assert.equal(p.developer,false);assert.deepEqual(p.unlocks,['weapon:'+p.spec]);assert.equal(p.accountId,undefined);}
    assert.equal(bots[0].hp,15000);
    room.receive('host',{type:'settings',wins:3});assert.deepEqual([...room.sim.players.values()].filter(p=>p.bot).map(p=>p.spec),bots.map(p=>p.spec));
   }else assert.ok(bots.every(p=>!WEAPONS[p.spec].devOnly&&!p.unlocks.length));
   assert.equal(room.sim.players.get('host')!.developer,developer);
   assert.deepEqual(room.sim.players.get('host')!.unlocks,[]);
  }finally{room.dispose();}
 }
});

test('Siren bot obeys the shared slot limit and keeps its weapon and HP through rounds and rematches',()=>{
 const s=new Simulation({skipIntros:true});try{
  const host=s.addPlayer('host','DEV','dev',null,'#ffffff',{},true)!;
  s.configureLobby({botCount:2,botWeapons:['siren','siren']},'host');
  assert.deepEqual(s.options.botWeapons,['siren','random']);assert.equal([...s.players.values()].filter(p=>p.spec==='siren').length,1);
  s.setReady('host',true);assert.equal(s.selectSpec('host','siren'),false);s.selectSpec('host','emperor');s.advance();assert.equal(s.phase,'countdown');
  assert.equal(s.players.get('bot')!.hp,15000);s.resetRound();assert.equal(s.players.get('bot')!.hp,15000);
  s.phase='match';s.rematch();s.selectSpec('host','emperor');s.advance();assert.equal(s.players.get('bot')!.spec,'siren');assert.equal(s.players.get('bot')!.hp,15000);
  s.returnLobby();s.configureLobby({botCount:0},'host');host.cosmetics.preferredSpec='siren';s.setClass('host','dev');
  s.configureLobby({botCount:1,botWeapons:['siren']},'host');assert.equal(host.spec,'siren');assert.notEqual(s.players.get('bot')!.spec,'siren');
  host.developer=false;s.configureLobby({botWeapons:['emperor']},'host');assert.ok(!WEAPONS[s.players.get('bot')!.spec].devOnly);
 }finally{s.dispose();}
});

test('15,000 Siren base HP applies to selection, saved loadouts, Chip rebuilds and sandbox spec changes',()=>{
 assert.equal(statsFor({},'siren').hp,15000);assert.equal(statsFor({vital:2},'siren').hp,15050);assert.equal(statsFor({fortress:1},'siren').hp,27000);assert.equal(statsFor({}).hp,100);
 const s=new Simulation({sandbox:true});try{
  const p=s.addPlayer('host','DEV','dev',null,'#ffffff',{preferredSpec:'siren'},true)!;s.addPlayer('other');
  assert.equal(p.hp,15000);assert.equal(s.snapshot().players[0].maxHp,15000);
  s.phase='specialization';assert.equal(s.selectSpec('host','emperor'),true);assert.equal(p.hp,100);
  s.phase='fight';assert.equal(s.debug({action:'spec',id:'siren'},'host'),true);assert.equal(p.hp,15000);
  assert.equal(s.debug({action:'chip',id:'vital'},'host'),true);assert.equal(p.hp,15025);s.resetRound();assert.equal(p.hp,15025);
  s.phase='fight';p.hp=p.stats.hp/2;assert.equal(s.debug({action:'spec',id:'sword'},'host'),true);assert.equal(p.stats.hp,125);assert.equal(p.hp,125,'leaving Ward Zero resets the arena and refills health');
  assert.equal(s.debug({action:'spec',id:'siren'},'host'),true);assert.equal(p.hp,15025);
 }finally{s.dispose();}
});

function arena(spec:SpecId,distance:number,difficulty:Difficulty='normal'){
 const s=new Simulation({skipIntros:true,sandbox:true});s.addPlayer('host','DEV','ranged',null,'#ffffff',{},true);
 s.configureLobby({botCount:1,botWeapons:[spec],botDifficulty:difficulty},'host');
 s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 const bot=s.players.get('bot')!,target=s.players.get('host')!;bot.body.setTranslation(v(0,.9,0),true);target.body.setTranslation(v(0,.9,distance),true);target.hp=target.stats.hp=100000;
 s.world.propagateModifiedBodyPositionsToColliders();s.random=()=>0;
 return {s,bot,target,tick:(frames:number)=>{for(let n=0;n<frames;n++)s.step();}};
}

// Data-driven (ranged/magic Q+E overhaul, design §14): a live ability kit is tested at the midpoint of its E bot row
// (≤ 20 m) within 120 ticks; legacy E rows keep today's distances and 90 ticks.
const LEGACY_E:Partial<Record<SpecId,number>>={pistol:10,rifle:12,machinegun:10,fire:12,water:4,lightning:3.5,darkness:4,sword:2,daggers:10,shield:10,greatsword:3};
const liveE=(spec:SpecId)=>{const e=kitFor(spec)?.e;return e?.live&&e.bot?e:undefined;};
test('every ordinary bot weapon executes its secondary through combat, including easy bots',()=>{
 for(const spec of Object.keys(LEGACY_E) as SpecId[]){
  const e=liveE(spec),distance=e?Math.min(20,(e.bot!.min+e.bot!.max)/2):LEGACY_E[spec]!;
  const {s,bot,tick}=arena(spec,distance,'easy');try{tick(e?120:90);assert.ok(bot.lastSecondary>=0,spec+' never used E');assert.ok(bot.secondaryCd>0||!!bot.abilityCast,spec+' did not start the normal cooldown');}finally{s.dispose();}
 }
});
test('ability bots use Q: guns hold ADS inside their Q bot range, magic casts its Q spell',()=>{
 for(const spec of Object.keys(LEGACY_E) as SpecId[]){const q=kitFor(spec)?.q;if(!q?.bot||!abilityActive(spec,'q'))continue;
  const {s,bot,tick}=arena(spec,Math.min(20,(q.bot.min+q.bot.max)/2),'hard');try{tick(120);if(q.kind==='ads')assert.equal(bot.input.ability,true,spec+' never aimed');else assert.ok(bot.lastAbility>=0,spec+' never cast Q');}finally{s.dispose();}}
});

test('DEV bots execute electric, wavedash, Hadoken, uppercut, Tatsumaki and Root Command',()=>{
 const cases:[SpecId,number,string][]=[['emperor',2,'emperor-electric'],['emperor',7,'emperor-wave'],['ansatsuken',12,'HADOKEN'],['ansatsuken',2,'SHORYUKEN'],['ansatsuken',4,'TATSUMAKI'],['leet',4,'root']];
 for(const [spec,distance,event] of cases){const {s,bot,tick}=arena(spec,distance);try{
  tick(110);assert.ok(event==='root'?bot.lastSecondary>=0:s.events.some(e=>e.source===bot.id&&(e.type===event||e.message===event)),event+' did not execute');
 }finally{s.dispose();}}
});

test('close-range casters approach for their abilities instead of hovering outside the effect radius',()=>{
 // Derived (design §14): specs whose E is not a live ability kit and that still use a close-range legacy special.
 const close=(Object.keys(WEAPONS) as SpecId[]).filter(spec=>['push','pull','nova','root'].includes(WEAPONS[spec].secondary)&&!abilityActive(spec,'e'));
 assert.ok(close.includes('leet'));
 for(const spec of close){
  const {s,bot,tick}=arena(spec,12);try{tick(300);assert.ok(bot.lastSecondary>=0,spec+' stayed outside ability range');}finally{s.dispose();}
 }
});

test('Siren bot switches modes and executes each ability through its normal cooldown and windup',()=>{
 for(const mode of ['melee','ranged'] as const)for(const ability of SIREN_SLOTS[mode].abilities){
  const {s,bot,tick}=arena('siren',mode==='melee'?2:12);try{
   const state=s.siren.state(bot);state.mode=mode;for(const id of SIREN_SLOTS[mode].abilities)state.ready![id]=id===ability?0:1000;
   tick(5);assert.ok((state.ready?.[ability]||0)>s.time,ability+' cooldown never started');assert.equal(state.move?.id,ability);
   tick(150);assert.ok(bot.lastAttack>=0);if(ability==='laugh')assert.ok(s.events.some(e=>e.type==='siren-catch'),'Last Laugh bot never caught its victim');
  }finally{s.dispose();}
 }
 const {s,bot,tick}=arena('siren',12);try{tick(5);assert.equal(bot.siren?.mode,'ranged');assert.equal(s.events.filter(e=>e.type==='siren-mode').length,1);}finally{s.dispose();}
});

test('bot abilities respect recovery, resources, visibility, cooldowns and dummy behavior',()=>{
 const {s,bot,tick}=arena('ansatsuken',12);try{
  bot.stamina=0;bot.stats.staminaRegen=0;tick(50);assert.ok(!s.events.some(e=>e.type==='arcade-move'));
  bot.stamina=100;bot.cooldown=2;tick(30);assert.ok(!s.events.some(e=>e.type==='arcade-move'));tick(110);assert.ok(s.events.some(e=>e.type==='arcade-move'));
 }finally{s.dispose();}
 const blocked=arena('fire',12);try{blocked.s.addProp({id:'wall',pos:v(0,2,6),size:v(60,5,1),material:'foundation'});blocked.s.world.step();blocked.tick(50);assert.equal(blocked.bot.lastSecondary,-10);}finally{blocked.s.dispose();}
 const dummy=arena('siren',2,'dummy');try{dummy.tick(150);assert.equal(dummy.bot.lastAttack,-10);assert.equal(dummy.bot.lastSecondary,-10);}finally{dummy.s.dispose();}
});
