// PvE Tab party board (owner feedback 2026-10-03): no rounds or sets in a boss run; per-member live stats from the snapshot's
// party rows; chip and weapon inspection still works; the PvP board is unchanged; the authority exposes the run start.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,RECRUIT_PREFIX,type PveView} from '../src/shared/pve/contract.js';
import {pveFixture} from '../src/shared/pve/fixtures.js';
import {boardKey,boardLive,boardRun,pveBoardHtml,runClock,runHeading,PVE_BOARD_COLUMNS} from '../src/client/pve/board.js';

// ---- a minimal DOM: enough for PlayerBoard (root, tooltip, slot lookups by data attribute) ----
class El{
 className='';id='';role='';dataset:Record<string,string>={};style:Record<string,string>={};private html='';writes=0;private slots=new Map<string,El>();
 private cls=new Set<string>();
 classList={toggle:(c:string,on?:boolean)=>{const v=on??!this.cls.has(c);if(v)this.cls.add(c);else this.cls.delete(c);return v;},add:(c:string)=>{this.cls.add(c);},remove:(c:string)=>{this.cls.delete(c);},contains:(c:string)=>this.cls.has(c)};
 get innerHTML(){return this.html;}
 set innerHTML(v:string){this.html=v;this.writes++;this.slots.clear();}
 append(){}addEventListener(){}getBoundingClientRect(){return {left:0,right:0,top:0,bottom:0,width:0,height:0};}
 private slot(key:string,make:()=>El){let e=this.slots.get(key);if(!e){e=make();this.slots.set(key,e);}return e;}
 querySelector(sel:string):El|null{
  if(sel==='[data-board-run]')return this.html.includes('data-board-run')?this.slot('run',()=>new El()):null;
  const m=sel.match(/^\[data-board-health="(.+)"\]$/);if(m)return this.html.includes(`data-board-health="${m[1]}"`)?this.slot('h:'+m[1],()=>new El()):null;
  return null;
 }
 querySelectorAll(sel:string):El[]{
  if(sel!=='[data-board-live]')return [];
  return [...this.html.matchAll(/data-board-live="([^"]*)"/g)].map(m=>this.slot('l:'+m[1],()=>{const e=new El();e.dataset.boardLive=m[1];return e;}));
 }
}
(globalThis as unknown as {document:unknown}).document={createElement:()=>new El()};
const {PlayerBoard}=await import('../src/client/player-board.js');
const board=()=>{const root=new El();const b=new PlayerBoard(root as unknown as HTMLDivElement,()=>{});return {root,b};};

// ---- fixtures ----
const ME='host',IDS=[ME,'p2',PARTY_BOT_PREFIX+'1','p3'];
function player(id:string,o:Partial<PlayerView>={}):PlayerView{
 return {id,name:id.toUpperCase(),color:'#ff6b35',class:'ranged',spec:'pistol',pos:{x:0,y:.9,z:0},vel:{x:0,y:0,z:0},yaw:0,pitch:0,hp:100,maxHp:100,mana:0,maxMana:0,ammo:7,maxAmmo:7,
  stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:0,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:2,roundWins:1,
  chips:{},ready:true,selected:true,offers:[],connected:true,bot:id.startsWith(PARTY_BOT_PREFIX)?'normal':null,ack:0,grounded:true,lastAttack:0,lastSecondary:0,reach:1,windup:0,statuses:[],...o} as PlayerView;
}
function snap(pve:PveView|undefined,players:PlayerView[],time=100):Snapshot{
 return {time,tick:0,phase:'fight',remaining:0,round:2,setNumber:1,setWinner:null,winner:null,result:'',props:[],projectiles:[],events:[],impact:null,
  metrics:{bodies:0,projectiles:0,tickMs:0},options:{roundsPerSet:3,wins:3,...(pve?{pve:{boss:'siren',seed:1}}:{})} as unknown as Snapshot['options'],generation:0,players,...(pve?{pve}:{})} as Snapshot;
}
/** Economy fixture with distinct stats per member, a downed p3 and recruits that exist as players. */
function partyState(o:{time?:number}={}){
 const time=o.time??100,v=structuredClone(pveFixture('economy',{time,ids:IDS}));
 const stats=[[41,12,1,9,2,3120,880,640,2210],[17,3,3,4,0,1490,410,85,1180],[28,6,0,6,1,2280,1320,0,0],[9,1,2,2,1,760,0,1410,1630]];
 IDS.forEach((id,i)=>{const r=v.party[id],[kills,heads,deaths,assists,revives,addDamage,bossDamage,money,earned]=stats[i];
  Object.assign(r,{kills,heads,deaths,assists,revives,addDamage,bossDamage,...(r.kind==='bot'?{}:{money,earned})});});
 v.party.p3.downAt=time-21;
 const players=[player(ME,{hp:52,chips:{vital:2,ricochet:1}}),player('p2',{name:'Kestrel',spec:'sword',class:'melee',color:'#6ec3ff'}),player(PARTY_BOT_PREFIX+'1',{name:'Party Bot',chips:{piercing:1}}),
  player('p3',{name:'<b>Juno</b>',hp:0,spec:'fire',class:'magic'}),...v.recruits.map(r=>player(r.id,{name:r.role,hp:r.role==='tank'?0:60}))];
 return {v,s:snap(v,players,time)};
}

test('PvE board: no rounds or sets; run heading, party totals and every member stat column from the party rows',()=>{
 const {v,s}=partyState(),html=pveBoardHtml(s,v,ME);
 const text=html.replace(/<[^>]*>/g,' ');for(const banned of [/round/i,/\bsets?\b/i,/First to/i,/score/i])assert.doesNotMatch(text,banned);
 assert.match(html,/<h2>Wave 1 \/ 3<\/h2>/);assert.match(html,/9 adds left/);assert.match(html,/Run <b>1:35<\/b>/,'startedAt = time − 95');
 assert.equal((html.match(/data-member="/g)||[]).length,4,'one row per party member (humans and party bots)');
 assert.ok(!html.includes(`data-member="${RECRUIT_PREFIX}`),'recruits are nested, not rows');
 for(const c of PVE_BOARD_COLUMNS)assert.ok(html.includes(`<span>${c}</span>`),c);
 const live=(id:string)=>boardLive(s,v,id),cell=(h:string,k:string)=>h.match(new RegExp(`data-stat="${k}"[^>]*><span>${k}</span><b>(.*?)</b></div>`))![1];
 const me=live(ME);
 assert.equal(cell(me,'Kills'),'41<small>12 heads</small>');assert.equal(cell(me,'Deaths'),'1');assert.equal(cell(me,'Assists'),'9');assert.equal(cell(me,'Revives'),'2');
 assert.equal(cell(me,'Add dmg'),'3,120');assert.equal(cell(me,'Boss dmg'),'880');assert.equal(cell(me,'Money'),'$640');assert.equal(cell(me,'Earned'),'$2,210');
 assert.match(me,/data-state="alive"/);assert.match(me,/52<small> \/ 100<\/small>/);
 assert.match(me,/data-role="dps" data-down="0"/);assert.match(me,/data-role="tank" data-down="1"/,'a downed recruit is marked');
 const bot=live(PARTY_BOT_PREFIX+'1');assert.equal(cell(bot,'Money'),'—');assert.equal(cell(bot,'Earned'),'—');assert.equal(cell(bot,'Kills'),'28<small>6 heads</small>');
 const down=live('p3');assert.match(down,/data-state="down"/);assert.match(down,/Down <small>0:21<\/small>/);assert.equal(cell(down,'Kills'),'9<small>1 head</small>');
 const run=boardRun(s,v);assert.match(run,/Kills<\/dt><dd>95</);assert.match(run,/Deaths<\/dt><dd>6</);assert.match(run,/Earned<\/dt><dd>\$5,020</,'bots add no money');
 assert.match(run,/Damage<\/dt><dd>10,260</);
 // Identity and inspection hooks.
 assert.match(html,/YOU<\/small>/);assert.match(html,/class="bot">BOT/);assert.ok(html.includes('&lt;b&gt;Juno&lt;/b&gt;')&&!html.includes('<b>Juno'),'names are escaped');
 assert.match(html,/data-board-chip="spec" data-player="host"/);assert.match(html,/data-board-chip="vital" data-player="host"[^>]*aria-label="Vital Core, 2 stacks/);
 assert.match(html,/data-board-close/);
});

test('PvE board heading: intermission, boss phase with health, frozen run time and outcome',()=>{
 const {v,s}=partyState();
 assert.equal(runHeading(s,{...v,stage:'intermission',wave:2,waveLeft:0}).title,'Intermission');
 assert.equal(runHeading(s,{...v,stage:'opening',wave:0}).title,'Opening');
 const boss=player(BOSS_PLAYER_ID,{hp:4200,maxHp:12000}),bs=snap(v,[...s.players,boss]);
 const h=runHeading(bs,{...v,stage:'boss',wave:3,bossView:{id:BOSS_PLAYER_ID,phase:3,floor:1000}});
 assert.match(h.title,/Phase 3 \/ 4/);assert.equal(h.detail,'Boss health 35%');
 const ended={...v,stage:'ended' as const,outcome:'victory' as const,endedAt:90};
 const e=runHeading({...s,time:400},ended);assert.equal(e.title,'Victory');assert.equal(e.time,85,'clock stops at endedAt');
 assert.equal(runHeading(s,{...v,startedAt:undefined}).time,undefined);assert.doesNotMatch(boardRun(s,{...v,startedAt:undefined}),/Run </);
 assert.equal(runClock(65.9),'1:05');assert.equal(runClock(3725),'1:02:05');
});

test('PlayerBoard in PvE: static markup once, live cells swapped in place, chip changes rebuild',()=>{
 const {root,b}=board(),{v,s}=partyState();
 b.update(s,ME,true);assert.equal(root.writes,1);assert.ok(!root.classList.contains('hidden'));assert.match(root.innerHTML,/pveb-panel/);
 const slot=root.querySelectorAll('[data-board-live]').find(e=>e.dataset.boardLive===ME)!,runSlot=root.querySelector('[data-board-run]')!;
 const v2=structuredClone(v);v2.party[ME].kills=42;v2.party[ME].money=700;
 b.update(snap(v2,s.players,101),ME,true);
 assert.equal(root.writes,1,'live stats do not rebuild the board (chip hover keeps working)');
 assert.match(slot.innerHTML,/<span>Kills<\/span><b>42</);assert.match(slot.innerHTML,/\$700/);assert.match(runSlot.innerHTML,/Run <b>1:36<\/b>/);
 const w=slot.writes;b.update(snap(v2,s.players,101),ME,true);assert.equal(slot.writes,w,'unchanged cells are not rewritten');
 b.update(snap(v2,s.players.map(p=>p.id===ME?{...p,chips:{...p.chips,piercing:1}}:p),101),ME,true);assert.equal(root.writes,2,'installing a chip rebuilds');
 b.update(s,ME,false);assert.ok(root.classList.contains('hidden'));
 assert.notEqual(boardKey(s,v,ME),boardKey(s,v,'p2'));
});

// The PvP board markup (style attributes stripped, so chip-card colour work elsewhere does not move it) recorded from the
// unmodified board at 6fd525c.
// Re-pinned 2026-10-04: the eyebrow now reads 'HOLD TAB OR ESC' (Escape shows the board; melee Tab is lock-on).
const PVP_BOARD_SHA='f683d83e2f9ada1aaa26e1a20670d8e3dc8579e445492c7886b55b1694983224';
export function pvpMarkup(make:()=>{root:El;b:{update(s:Snapshot,me:string,v:boolean):void}}){
 const {root,b}=make(),players=[player(ME,{chips:{vital:2,ricochet:1}}),player('rival',{name:'Rival',spec:'sword',class:'melee',color:'#6ec3ff',bot:'hard',hp:0}),player('away',{connected:false})];
 b.update(snap(undefined,players),ME,true);return root.innerHTML.replace(/ style="[^"]*"/g,'');
}
test('PvP board is unchanged (sets, rounds, score) and never takes the PvE path',()=>{
 const html=pvpMarkup(board);
 assert.match(html,/Set 1 · Round 2 · First to 3 sets/);assert.match(html,/1\/3 rounds this set/);assert.match(html,/<small>SETS<\/small>/);
 assert.doesNotMatch(html,/pveb-/);
 assert.equal(createHash('sha256').update(html).digest('hex'),PVP_BOARD_SHA);
});

test('authority: the PvE view carries the run start from fight start; PvP snapshots carry no pve',async()=>{
 await initPhysics();
 const sim=new Simulation({pve:{boss:'siren',seed:7,debug:true}});sim.addPlayer('h1','H1','ranged');sim.pve!.command('h1',{action:'hello',v:1,key:'ab'.repeat(16)},false);
 assert.equal(sim.pve!.view().startedAt,undefined,'no run clock before the fight');
 sim.setReady('h1',true);
 for(let i=0;i<6000&&sim.phase!=='fight';i++){if(sim.phase==='specialization')sim.selectSpec('h1','pistol');sim.step();}
 assert.equal(sim.phase,'fight');const start=sim.pve!.view().startedAt;assert.ok(start!==undefined&&start<=sim.time);
 for(let i=0;i<30;i++)sim.step();assert.equal(sim.pve!.view().startedAt,start,'stable while the run goes on');
 const pvp=new Simulation({});pvp.addPlayer('a','A','ranged');assert.equal('pve' in pvp.snapshot(),false);
});
