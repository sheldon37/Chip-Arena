// DEV sandbox tools and Ward Zero outside PvE (src/shared/sandbox-tools.ts).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {CHIPS,CLASS_INFO,RARITIES,type Rarity} from '../src/shared/content.js';
import {SIREN_VENTS} from '../src/shared/siren-stage-layout.js';
import {MAPS} from '../src/shared/maps.js';
import {findChips,wardEntries,RARITY_ORDER,rarityRank,SANDBOX_RESPAWN} from '../src/shared/sandbox-tools.js';
import {playerSpawn} from '../src/shared/arena.js';
import {VENT_PIT,ventOpenAmounts,wardVentDrawn,wardVentWarning} from '../src/shared/pve/vent-pits.js';
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
 // Strict tier order (owner 2026-10-04): the six draft tiers, then each DEV exclusive as its own block (they used to tie and interleave).
 assert.deepEqual([...RARITY_ORDER].sort(),Object.keys(RARITIES).sort(),'every rarity has a place in the order');
 const r=(c:{rarity:Rarity})=>rarityRank(c.rarity),up=findChips(CHIPS,'','rarity'),down=findChips(CHIPS,'','rarity-desc');
 assert.deepEqual(up.map(r),up.map(r).sort((a,b)=>a-b));assert.deepEqual(down.map(r),down.map(r).sort((a,b)=>b-a));
 const runs=(l:typeof CHIPS)=>l.filter((c,i)=>i===0||c.rarity!==l[i-1].rarity).length;
 assert.equal(runs(up),new Set(CHIPS.map(c=>c.rarity)).size,'one contiguous block per rarity');assert.equal(runs(down),runs(up));
 assert.deepEqual(up.slice(-3).map(c=>c.rarity),['siren','siren','siren']);assert.equal(down[0].rarity,'siren');
 for(const l of [up,down])for(let i=1;i<l.length;i++)if(l[i].rarity===l[i-1].rarity)assert.ok(l[i-1].name.localeCompare(l[i].name)<=0,'A-Z inside a tier');
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
  // The forced vent warns, swings open (drawn ahead of the pit) and only then takes the fall (owner 2026-10-04).
  const me=s.players.get('local')!,at=me.body.translation(),e=SIREN_VENTS.map((w,i)=>[Math.hypot(w.x-at.x,w.z-at.z),i]).sort((a,b)=>a[0]-b[0])[0][1];
  const before=s.vents!.forced.get(e);assert.equal(s.debug({action:'vent-fall',target:'self',delay:2},'local'),true);const forced=s.vents!.forced.get(e)!==before,t0=s.time;
  let warned=0,drawn=-1;for(let i=0;i<60*2.5&&!me.death;i++){tick(s);const en=s.snapshot().vents!.find(x=>x.e===e);if(wardVentWarning(en,s.time)>0)warned+=1/60;if(me.death)drawn=wardVentDrawn(en,s.time);}
  assert.equal(me.death?.effect,'vent-fall');assert.ok(drawn>.99,`the lid is drawn fully open when the pit takes the fall (${drawn.toFixed(2)})`);
  if(forced){assert.ok(warned>VENT_PIT.warn-.05,`warned ${warned.toFixed(2)} s`);assert.ok(me.death!.at-t0>VENT_PIT.warn+VENT_PIT.lead,'alive through the warning and the swing');}
  assert.ok(Math.abs(me.death!.landAt!-me.death!.at-2)<1e-6);assert.equal(s.phase,'fight','held while in the shaft');
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

test('a killed sandbox dummy stands up on its own pad after the respawn delay; the round goes on',()=>{
 const s=sandbox();try{const me=s.players.get('local')!,d=s.players.get('bot')!,gen=s.generation,round=s.round;
  assert.deepEqual(s.snapshot().sandbox,{respawn:SANDBOX_RESPAWN.default,safe:false,every:0});
  assert.ok(s.debug({action:'death-preview'},'local'));assert.ok(d.hp<=0&&d.death);const id=d.death!.id;assert.equal(s.phase,'fight','no round end');
  tick(s,Math.round(60*(SANDBOX_RESPAWN.default-.25)));assert.ok(d.hp<=0,'still down before the delay');assert.equal(s.phase,'fight');assert.equal(me.roundWins,0);
  tick(s,30);assert.equal(d.hp,d.stats.hp);assert.equal(d.death,undefined);assert.equal(d.mana,d.stats.maxMana);assert.equal(d.cooldown,0);
  assert.equal(s.phase,'fight');assert.equal(s.generation,gen,'no round reset');assert.equal(s.round,round);
  const pad=playerSpawn(1,true),p=d.body.translation();assert.ok(Math.hypot(p.x-pad.x,p.z-pad.z)<.6,'on its pad');assert.ok(s.events.some(e=>e.type==='sandbox-respawn'&&e.target==='bot'));
  assert.ok(s.debug({action:'death-preview'},'local'));const again=d.death as {id:string}|undefined;assert.ok(again&&again.id!==id,'a second kill is a new death');assert.equal(s.phase,'fight');
 }finally{s.dispose();}
});

test('respawn delay: bounded, 0 restores the old round end, and a human death still ends the round with the dummy as winner',()=>{
 const s=sandbox();try{
  for(const [n,want] of [[3,3],[100,30],[.2,1],[0,0]] as const){assert.equal(s.debug({action:'respawn',seconds:n},'local'),true);assert.equal(s.snapshot().sandbox!.respawn,want);}
  for(const bad of ['abc','',undefined])assert.equal(s.debug({action:'respawn',seconds:bad},'local'),false);
  assert.ok(s.debug({action:'death-preview'},'local'));assert.equal(s.phase,'result','0 = off');assert.equal(s.players.get('local')!.roundWins,1);
 }finally{s.dispose();}
 const t=sandbox();try{assert.ok(t.debug({action:'death-preview'},'local'));tick(t);assert.equal(t.phase,'fight');
  assert.ok(t.debug({action:'down',target:'self',by:'dummy'},'local'));assert.equal(t.phase,'result');assert.equal(t.winner,'bot');
 }finally{t.dispose();}
 const pvp=new Simulation({gameMode:'ffa'});try{assert.equal(pvp.snapshot().sandbox,undefined,'sandbox rooms only');}finally{pvp.dispose();}
});

test('a sandbox respawn waits for animation holds and for a vent-fall body to land',()=>{
 const s=sandbox();try{const d=s.players.get('bot')!;
  assert.ok(s.debug({action:'down',target:'opponent',hold:8},'local'));const at=d.death!.at;
  while(d.hp<=0&&s.time<at+12)tick(s);assert.ok(s.time>=at+8-1e-9,`held: back at ${(s.time-at).toFixed(2)} s`);
  s.debug({action:'map',id:'siren'},'local');assert.ok(s.debug({action:'vent-fall',target:'opponent',delay:2},'local'));
  while(!d.death&&s.time<60)tick(s);assert.equal(d.death?.effect,'vent-fall');
  let last=d.death!;while(d.hp<=0&&s.time<80){last=d.death??last;tick(s);}
  assert.ok(d.hp>0,'back up');assert.equal(last.effect,'vent-splat','it landed before standing up');assert.ok(s.time>=last.at+SANDBOX_RESPAWN.default-1e-9,'the full delay after the landing');
 }finally{s.dispose();}
});

// Dummy cast (keypress), owner 2026-10-04: the dummy faces the pilot who asked and presses one key.
const eventsAfter=(s:Simulation,from:number)=>s.events.filter(e=>e.time>=from).map(e=>e.type+(e.message?':'+e.message:''));
test('dummy cast (keypress E) with Scare Tactics: the dummy gets the L33T kit (not the DEV role), Slenderman comes for the pilot who asked',()=>{
 const s=sandbox(DEV);try{tick(s,10);const me=s.players.get('local')!,d=s.players.get('bot')!,t0=s.time;
  assert.equal(s.debug({action:'dummy-cast',id:'l44t-scare',key:'e',safe:true},'local'),true);
  assert.deepEqual(d.unlocks,['weapon:leet']);assert.equal(d.developer,false);assert.equal(d.spec,'leet');assert.equal(s.leet.active(d,'scare'),true);
  // A second bot standing much nearer the tree does not steal the stalk.
  s.addPlayer('bot2','Decoy','melee','dummy','#ccc');const decoy=s.players.get('bot2')!;decoy.selected=true;decoy.hp=decoy.stats.hp;
  tick(s,6);const tree=[...s.leet.fields.values()].find(f=>f.kind==='tree'&&f.owner==='bot');assert.ok(tree,'E planted the tree');place(decoy,tree!.pos.x+.8,tree!.pos.z);
  tick(s,90);assert.ok(eventsAfter(s,t0).includes('summon:slender'),'Slenderman emerged');assert.equal(tree!.target,'local');
  for(let i=0;i<60*8&&!me.leet?.scare&&me.hp>1;i++)tick(s);for(let i=0;i<60*4;i++)tick(s);assert.equal(me.hp,1,'safe mode: the jumpscare knocks out to 1 HP');
 }finally{s.dispose();}
});

test('dummy cast keys press the right ability, aimed at the pilot; keys a weapon has no use for, and DEV kits without access, are refused',()=>{
 const run=(chip:string,key:string,want:string|RegExp,dev=true,ticks=90)=>{const s=sandbox(dev?DEV:false);try{tick(s,10);const t0=s.time;const ok=s.debug({action:'dummy-cast',id:chip,key},'local');
  if(ok)tick(s,ticks);return {ok,events:eventsAfter(s,t0),s};}finally{s.dispose();}};
 const hit=(r:{events:string[]},want:string|RegExp)=>r.events.some(e=>typeof want==='string'?e===want||e.startsWith(want):want.test(e));
 for(const [chip,key,want] of [['vital','e','ability:meteor-strike'],['necronomicon','e','summon'],['mishima-godfist','q','emperor-electric'],['arcade-rush','1',/HADOKEN|hadoken/],['blast-dash','dash','explosion:dashBlast']] as const){
  const r=run(chip,key,want);assert.equal(r.ok,true,chip+' '+key);assert.ok(hit(r,want),`${chip} ${key}: ${r.events.slice(0,12).join(', ')}`);}
 assert.equal(run('vital','3','').ok,false,'3 does nothing for a mage');
 assert.equal(run('l44t-scare','e','',false).ok,false,'DEV kit without access');
 const outside=new Simulation({gameMode:'ffa'});try{outside.addPlayer('local','Me','ranged',null,'#fff',undefined,DEV);outside.addPlayer('bot','D','magic','dummy','#eee');assert.equal(outside.debug({action:'dummy-cast',id:'vital',key:'e'},'local'),false);}finally{outside.dispose();}
});

test('keypress repeats recast on the timer (cooldowns refreshed in the sandbox) and Stop ends them; AI mode now drives DEV kits too',()=>{
 const s=sandbox(DEV);try{tick(s,10);const t0=s.time;
  assert.ok(s.debug({action:'dummy-cast',id:'l44t-time',key:'e',every:3,safe:true},'local'));tick(s,60*7);
  assert.ok(eventsAfter(s,t0).filter(e=>e==='leet-freeze').length>=2,'Admin Pause cast again on the repeat');
  assert.ok(s.debug({action:'dummy-cast',stop:true},'local'));const t1=s.time;tick(s,60*4);assert.equal(eventsAfter(s,t1).filter(e=>e==='leet-freeze').length,0);
  const t2=s.time;assert.ok(s.debug({action:'dummy-cast',id:'l44t-scare',safe:true},'local'));tick(s,60*4);
  assert.ok([...s.leet.fields.values()].some(f=>f.kind==='tree'&&f.owner==='bot')||eventsAfter(s,t2).includes('summon:slender'),'the AI fight uses the granted L33T kit');
 }finally{s.dispose();}
});

test('a respawn re-arms once-per-round chips; with two humans a downed human still decides the round',()=>{
 const s=sandbox();try{const chaos=s.chaos as unknown as {once(k:string):boolean};assert.equal(chaos.once('bot:exit'),true);assert.equal(chaos.once('bot:exit'),false,'used this round');
  assert.ok(s.debug({action:'death-preview'},'local'));while(s.players.get('bot')!.hp<=0&&s.time<20)tick(s);assert.equal(chaos.once('bot:exit'),true,'Emergency Exit is ready again after the respawn');
 }finally{s.dispose();}
 const t=sandbox();try{t.addPlayer('b2','Second','melee',null,'#ddd');const b2=t.players.get('b2')!;b2.selected=true;tick(t);
  assert.ok(t.debug({action:'death-preview'},'local'));tick(t);assert.equal(t.phase,'fight','only the dummy is down');
  t.damage(b2,1e5,'local','preview',v(0,0,1),0);t.resolveRound();assert.equal(t.phase,'result','the human duel is decided while the dummy waits');assert.equal(t.winner,'local');
 }finally{t.dispose();}
});

test('a Siren dummy cast takes the sandbox to Ward Zero; casting anything else brings the arena back (and stops her song)',()=>{
 const s=sandbox(DEV);try{tick(s,5);assert.equal(s.options.map,'foundry');
  assert.ok(s.debug({action:'dummy-cast',id:'siren-crown',safe:true},'local'));assert.equal(s.options.map,'siren');tick(s,30);assert.ok(s.bossSong,'her song plays');
  assert.ok(s.debug({action:'dummy-cast',id:'vital',safe:true},'local'));assert.equal(s.options.map,'foundry','arena restored');assert.equal(s.bossSong,undefined);assert.equal(s.normalArena,undefined);
  assert.ok(s.debug({action:'vent-fall',target:'opponent',delay:2},'local'),'the Ward Zero vent test works again');
 }finally{s.dispose();}
});
