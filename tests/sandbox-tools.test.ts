// DEV sandbox tools and Ward Zero outside PvE (src/shared/sandbox-tools.ts).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {CHIPS,CLASS_INFO} from '../src/shared/content.js';
import {SIREN_VENTS} from '../src/shared/siren-stage-layout.js';
import {MAPS} from '../src/shared/maps.js';
import {findChips,wardEntries} from '../src/shared/sandbox-tools.js';
import {VENT_PIT,ventOpenAmounts} from '../src/shared/pve/vent-pits.js';
await initPhysics();

const DEV={developer:true,unlocks:[]};
function sandbox(dev:boolean|typeof DEV=false){const s=new Simulation({sandbox:true,gameMode:'ffa'});s.addPlayer('local','Me','ranged',null,'#fff',undefined,dev);s.addPlayer('bot','Dummy','magic','dummy','#eee');
 s.setReady('local',true);s.selectSpec('local',CLASS_INFO.ranged.default);s.selectSpec('bot',CLASS_INFO.magic.default);s.phase='fight';s.fightStart=s.time;return s;}
const tick=(s:Simulation,n=1)=>{for(let i=0;i<n;i++){const p=s.players.get('local');if(p){p.input={...p.input,seq:p.input.seq+1};p.inputAt=s.time;}s.step();}};
const place=(p:Player,x:number,z:number)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);};
const openFor=(s:Simulation,e:number)=>ventOpenAmounts(wardEntries(s.time,s.vents?.forced),s.time)[e];

test('chip search matches name, id and description case-insensitively; sorts by rarity both ways, name and default',()=>{
 assert.deepEqual(findChips(CHIPS,'VITAL core').map(c=>c.id),['vital']);
 assert.ok(findChips(CHIPS,'l44t-scare').some(c=>c.name.includes('Scare')));
 assert.ok(findChips(CHIPS,'slenderman').some(c=>c.id==='l44t-scare'),'description text');
 assert.deepEqual(findChips(CHIPS).map(c=>c.id),CHIPS.map(c=>c.id));
 const order=['common','uncommon','rare','epic','legendary','mythic'],r=(c:{rarity:string})=>order.indexOf(c.rarity)<0?9:order.indexOf(c.rarity);
 const up=findChips(CHIPS,'','rarity').map(r),down=findChips(CHIPS,'','rarity-desc').map(r);
 assert.deepEqual(up,[...up].sort((a,b)=>a-b));assert.deepEqual(down,[...down].sort((a,b)=>b-a));
 const names=findChips(CHIPS,'','name').map(c=>c.name);assert.deepEqual(names,[...names].sort((a,b)=>a.localeCompare(b)));
 assert.equal(findChips(CHIPS,'zzqx').length,0);
});

test('dummy cast is refused outside the sandbox, for unknown chips and for DEV-only chips without DEV access',()=>{
 const pvp=new Simulation();pvp.addPlayer('a');pvp.addPlayer('b','B','magic','dummy');
 assert.equal(pvp.debug({action:'dummy-cast',id:'ember'},'a'),false);
 const s=sandbox(),dev=sandbox(DEV);try{
  assert.equal(s.debug({action:'dummy-cast',id:'nope'},'local'),false);
  assert.equal(s.debug({action:'dummy-cast',id:'l44t-scare'},'local'),false);assert.equal(s.players.get('bot')!.spec,'fire');
  assert.equal(dev.debug({action:'dummy-cast',id:'l44t-scare'},'local'),true);assert.equal(dev.players.get('bot')!.spec,'leet');
 }finally{pvp.dispose();s.dispose();dev.dispose();}
});

test('dummy cast: the dummy installs the chip and attacks; the effect lands on the player (safe mode keeps 1 HP)',()=>{
 const s=sandbox();try{const me=s.players.get('local')!,d=s.players.get('bot')!;me.hp=me.stats.hp=40;
  assert.equal(s.debug({action:'dummy-cast',id:'ember',safe:true,every:5},'local'),true);
  assert.deepEqual(d.chips,{ember:1});assert.equal(d.bot,'hard');
  let burned=false;for(let i=0;i<60*9&&!burned;i++){tick(s);burned=me.burns.size>0;}
  assert.ok(burned,'burn applied to the player by the dummy');for(let i=0;i<60*5;i++)tick(s);assert.ok(me.hp>=1&&!me.death,'safe mode');
  s.debug({action:'dummy-cast',stop:true},'local');tick(s);assert.equal(d.bot,'dummy');
 }finally{s.dispose();}
});

test('Ward Zero in the sandbox: vents cycle, no adds or boss; a fall splats after the chosen delay (PvE keeps 10 s)',()=>{
 const s=sandbox();try{
  assert.equal(s.debug({action:'map',id:'siren'},'local'),true);assert.equal(s.options.map,'siren');assert.equal(s.pve,undefined);
  let opened=0;for(let i=0;i<60*13;i++){tick(s);if(openFor(s,0)>=VENT_PIT.lethal&&!opened)opened=s.time;}
  assert.ok(opened>0,'vent 0 opened on its cycle');
  assert.equal(s.chaos.minions.size,0);assert.equal(s.players.size,2);assert.equal(s.bossSong,undefined);
  const me=s.players.get('local')!;assert.equal(s.debug({action:'vent-fall',target:'self',delay:2},'local'),true);tick(s,2);
  assert.equal(me.death?.effect,'vent-fall');assert.ok(Math.abs(me.death!.landAt!-me.death!.at-2)<1e-6);assert.equal(s.phase,'fight','held while in the shaft');
  for(let i=0;i<60*2.2;i++)tick(s);assert.equal(me.death?.effect,'vent-splat');assert.ok(s.events.some(e=>e.type==='pve-vent-splat'));
  const pve=new Simulation({pve:{boss:'siren',seed:1}});assert.equal(pve.pve!.vents.splatAt,VENT_PIT.splatAt);assert.equal(VENT_PIT.splatAt,10);pve.dispose();
 }finally{s.dispose();}
});

test('a sandbox pilot walking onto an open Ward Zero vent falls in through the ordinary pit check',()=>{
 const s=sandbox();try{s.debug({action:'map',id:'siren'},'local');const me=s.players.get('local')!,w=SIREN_VENTS[2];
  while(openFor(s,2)<.9)tick(s);place(me,w.x,w.z);tick(s,3);assert.equal(me.hp,0);assert.equal(me.death?.effect,'vent-fall');
 }finally{s.dispose();}
});

test('Ward Zero is a PvP map: selectable, not in random rotation, vents cycle, falls credit the attacker, no splat, adds or boss',()=>{
 const s=new Simulation({map:'siren'}),lobby=new Simulation();try{
  lobby.configureLobby({map:'siren'});assert.equal(lobby.options.map,'siren');assert.ok(MAPS.every(m=>String(m.id)!=='siren'));
  s.addPlayer('a','A','ranged');s.addPlayer('b','B','ranged');s.addPlayer('c','C','ranged');s.phase='fight';s.fightStart=s.time;
  const a=s.players.get('a')!,b=s.players.get('b')!,w=SIREN_VENTS[1];
  while(openFor(s,1)<.9)s.step();assert.ok(s.snapshot().vents?.some(e=>e.e===1));
  s.damage(b,5,'a','direct');place(b,w.x,w.z);s.step();s.step();
  assert.equal(b.hp,0);assert.equal(b.death?.effect,'vent-fall');assert.equal(b.death?.announcement?.killer,a.name);
  for(let i=0;i<60*11;i++)s.step();assert.notEqual(b.death?.effect,'vent-splat');
  assert.equal(s.chaos.minions.size,0);assert.equal(s.pve,undefined);assert.equal(s.bossSong,undefined);assert.equal(s.players.size,3);
 }finally{s.dispose();lobby.dispose();}
});
