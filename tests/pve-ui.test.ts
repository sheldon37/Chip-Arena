// S4 PvE client UI: pure HUD builders, tracks, prompts, the Siren reveal, CSS and main.ts source pins (design §7, §11 S4).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import type {PlayerView,Snapshot,GameEvent} from '../src/shared/simulation.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,encodeAdd,type PveView} from '../src/shared/pve/contract.js';
import {pveFixture,fixtureAdd,fixtureBossPlayer,fixtureBossHp} from '../src/shared/pve/fixtures.js';
import {PVE_MACHINES} from '../src/shared/pve/layout.js';
import {SIREN_BOSS_CHIPS} from '../src/shared/pve/bosses.js';
import {moneyHud,waveHeading,bossBar,partyHud,chipLaterIcon,bannerFor,pveBanner,downLine,possessionArrow,formatCash,formatGain,clock,bossIntroName} from '../src/client/pve/hud.js';
import {MoneyTrack,AttackedTrack,DelayMemo,ATTACKED_WINDOW,threatenedBy} from '../src/client/pve/tracks.js';
import {promptFor,promptHud} from '../src/client/pve/prompts.js';
import {sirenRevealHtml,revealWindow} from '../src/client/pve/siren-reveal.js';
import {testBossBody,bossPrefs,pveLobbyBody} from '../src/client/pve/menu.js';

const ME='host',IDS=[ME,'p2',PARTY_BOT_PREFIX+'1','p3'];
function player(id:string,o:Partial<PlayerView>={}):PlayerView{
 return {id,name:id.toUpperCase(),color:'#ff6b35',class:'ranged',spec:'pistol',pos:{x:0,y:.9,z:0},vel:{x:0,y:0,z:0},yaw:0,pitch:0,hp:100,maxHp:100,mana:0,maxMana:0,ammo:7,maxAmmo:7,
  stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:0,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,
  chips:{},ready:true,selected:true,offers:[],connected:true,bot:id.startsWith(PARTY_BOT_PREFIX)?'normal':null,ack:0,grounded:true,lastAttack:0,lastSecondary:0,reach:1,windup:0,statuses:[],...o} as PlayerView;
}
function snap(pve:PveView,o:Partial<Snapshot>&{time?:number}={}):Snapshot{
 const players=o.players??Object.keys(pve.party).map(id=>player(id,{hp:pve.party[id].downAt!==undefined?0:100}));
 return {time:o.time??100,tick:0,phase:'fight',remaining:0,round:1,setNumber:1,setWinner:null,winner:null,result:'',props:[],projectiles:[],events:o.events??[],impact:null,
  metrics:{bodies:0,projectiles:0,tickMs:0},options:{pve:{boss:'siren',seed:1}} as Snapshot['options'],generation:o.generation??0,...o,players,pve} as Snapshot;
}
const ev=(id:number,type:string,o:Partial<GameEvent>={}):GameEvent=>({id,type,time:100,pos:{x:0,y:0,z:0},...o});
const view=(name:Parameters<typeof pveFixture>[0],time=100)=>pveFixture(name,{time,ids:IDS});

test('HUD builders escape names and keep stable data-* identities across redraws',()=>{
 const v=view('party'),s=snap(v);s.players[0].name='<img src=x onerror=alert(1)>';
 const a=partyHud(s,v,ME);assert.match(a,/&lt;img src=x/);assert.ok(!a.includes('<img'));
 const b=partyHud({...s,time:101.3,players:s.players.map(p=>({...p,hp:p.hp>0?p.hp-12:0}))},v,ME);
 const ids=(h:string)=>[...h.matchAll(/<[a-z]+ ([^>]*)>/g)].map(m=>(m[1].match(/(class|data-[a-z-]+)="[^"]*"/g)||[]).join(' ')).join('\n');
 assert.equal(ids(a),ids(b),'only text and style change between redraws');
 assert.match(a,/data-member="host"/);assert.match(a,/data-down="1"/);
 assert.equal(formatCash(1250),'$1,250');assert.equal(formatGain(10),'+$10');assert.equal(formatGain(-150),'−$150');assert.equal(clock(12.2),'0:13');
});

test('party rows: dead timer counts from downAt, self-revive countdown, BOT rows carry no money or revive price',()=>{
 const v=view('party'),s=snap(v);
 const dead=partyHud(s,v,ME).split('data-member="p2"')[1].split('data-member=')[0];
 assert.match(dead,/DOWN · Revive \$100 · 0:03/,'time since down (downAt = time - 3)');assert.match(dead,/Respawns at wave clear/);
 assert.match(partyHud({...s,time:112},v,ME),/DOWN · Revive \$100 · 0:15/);
 const sv=view('self-revive'),ss=snap(sv);assert.match(partyHud(ss,sv,ME),/SELF-REVIVE 0:02/);
 const bot=partyHud(s,v,ME).split(`data-member="${PARTY_BOT_PREFIX}1"`)[1].split('data-member=')[0];
 assert.match(bot,/BOT/);assert.ok(!bot.includes('$'),'party bots show no money');
 assert.equal(moneyHud(v,PARTY_BOT_PREFIX+'1'),'','no money counter for a party bot');
 const choosing=partyHud(s,v,ME).split('data-member="p3"')[1];assert.match(choosing,/data-choosing="1"|CHOOSING/);
 const dv=view('dead'),ds=snap(dv);assert.match(partyHud(ds,dv,ME),/REVIVING HOST|REVIVING P/,'a channelling member shows the revive');
 const boss=view('boss-2'),bs=snap(boss);assert.match(partyHud({...bs,players:bs.players.map(p=>p.id==='p2'?{...p,hp:0}:p)},{...boss,party:{...boss.party,p2:{...boss.party.p2,downAt:95}}},ME),/Respawns next phase/);
});

test('MoneyTrack: popups keyed by event id, spend shown, fallback on dropped events, reset on generation and rewind',()=>{
 const t=new MoneyTrack(),v=view('wave');
 const kill=ev(10,'pve-kill',{source:ME,amount:10}),cash=ev(11,'pve-cash',{target:ME,amount:5,message:'head'}),other=ev(12,'pve-kill',{source:'p2',amount:35});
 let p=t.update(snap(v,{events:[kill,cash,other]}),ME);
 assert.deepEqual(p.map(x=>x.id),['e10','e11']);assert.deepEqual(p.map(x=>x.amount),[10,5]);
 p=t.update(snap(v,{time:100.5,events:[kill,cash,other]}),ME);assert.equal(p.length,2,'events are consumed once');assert.equal(p[0].delay,0,'delay fixed at first sight');
 const money=v.party[ME].money;
 p=t.update(snap({...v,party:{...v.party,[ME]:{...v.party[ME],money:money+40}}},{time:100.6,events:[]}),ME);
 assert.ok(p.some(x=>x.id.startsWith('d')&&x.amount===40),'unexplained total increase becomes one fallback popup');
 p=t.update(snap({...v,party:{...v.party,[ME]:{...v.party[ME],money:money-110}}},{time:100.7,events:[ev(13,'pve-buy',{target:ME,amount:150,message:'chip'})]}),ME);
 assert.ok(p.some(x=>x.id==='e13'&&x.amount===-150));assert.ok(!p.some(x=>x.id.startsWith('d')&&x.at===100.7),'a matching spend is not a fallback gain');
 const spent={...v,party:{...v.party,[ME]:{...v.party[ME],money:money-110}}};p=t.update(snap(spent,{time:103,events:[]}),ME);assert.equal(p.length,0,'popups expire');
 t.update(snap(v,{time:103,events:[kill]}),ME);p=t.update(snap(v,{time:100,generation:1,events:[kill]}),ME);assert.deepEqual(p.map(x=>x.id),['e10'],'new generation resets the cursor');
 p=t.update(snap(v,{time:90,generation:1,events:[ev(10,'pve-kill',{source:ME,amount:10,time:90})]}),ME);assert.deepEqual(p.map(x=>x.id),['e10'],'rewind resets');
 const html=moneyHud(v,ME,p);assert.match(html,/data-gain="e10"/);assert.match(html,/\+\$10/);assert.match(html,/1,250/);
});

test('AttackedTrack: 1.2 s window from hurtAt or a hit, and an early warning during an add windup on me',()=>{
 const t=new AttackedTrack(),v=view('choosing');
 assert.equal(t.update(snap(v),ME).attacked,false);
 const hurt={...v,party:{...v.party,[ME]:{...v.party[ME],hurtAt:100}}};
 assert.equal(t.update(snap(hurt,{time:100.5}),ME).source,'hurt');assert.equal(t.update(snap(hurt,{time:100+ATTACKED_WINDOW+.01}),ME).attacked,false);
 const t2=new AttackedTrack();assert.equal(t2.update(snap(v,{time:200,events:[ev(5,'hit',{target:ME,amount:7,time:200})]}),ME).source,'hit');
 assert.equal(t2.update(snap(v,{time:201.3,events:[]}),ME).attacked,false);
 const windup=(attackAt:number,target=ME)=>({...v,adds:[encodeAdd(fixtureAdd({n:1,target,attackAt,x:1}),v.slots)]});
 const t3=new AttackedTrack();
 assert.equal(t3.update(snap(windup(300),{time:300.1}),ME).source,'add','swipe windup targeting me (0.34 s)');
 assert.equal(t3.update(snap(windup(300),{time:300.5}),ME).attacked,false,'after the hit time');
 assert.equal(t3.update(snap(windup(300,'p2'),{time:300.1}),ME).attacked,false,'windup on someone else');
 const boss=snap(view('boss-1'),{time:400});const b=fixtureBossPlayer(boss.players[0],1,{time:400});b.siren={...b.siren!,move:{id:'mark',seq:1,at:399.8,hitAt:400.4,until:401,target:ME}};
 assert.equal(threatenedBy({...boss,players:[...boss.players,b]},ME),'boss');
});

test('wave heading, boss bar (notches, floor lock, hidden, mini chips) and banners',()=>{
 assert.match(waveHeading(view('wave'),100),/WAVE 1 \/ 3[\s\S]*9 <span>left/);assert.match(waveHeading(view('intermission'),100),/Intermission <span>0:09/);assert.match(waveHeading(view('opening'),100),/Opening/);
 const v=view('boss-2'),s=snap(v);assert.equal(bossBar(s,v),'','no bar without a boss player');
 const b=fixtureBossPlayer(s.players[0],2,{time:100,h:4});const bar=bossBar({...s,players:[...s.players,b]},v);
 assert.match(bar,/THE SIREN/);assert.match(bar,/HIDDEN — FIND HIM/);assert.equal((bar.match(/class="pve-notch/g)||[]).length,3);
 assert.equal((bar.match(/class="pve-boss-chip on"/g)||[]).length,2);assert.equal((bar.match(/>\?<\/span>/g)||[]).length,2);
 const v1=view('boss-1'),b1=fixtureBossPlayer(s.players[0],1,{time:100,h:4});b1.hp=v1.bossView!.floor;
 assert.match(bossBar({...snap(v1),players:[...snap(v1).players,b1]},v1),/data-locked="1"/);
 assert.equal(fixtureBossHp(1,4)>v1.bossView!.floor,true);
 const spawned=view('boss-1');spawned.stageAt=99;assert.equal(bannerFor(snap(spawned),spawned,ME)?.title,'Siren has Spawned');
 assert.equal(bannerFor(snap(view('wave'),{time:100}),{...view('wave'),stageAt:99},ME)?.title,'WAVE 1');
 const deny=bannerFor(snap(view('boss-3'),{events:[ev(3,'pve-deny',{target:ME,message:'money'})]}),view('boss-3'),ME);assert.equal(deny?.title,'NOT ENOUGH MONEY');
 const html=pveBanner(deny,-.2);assert.match(html,/data-banner="deny-3"/);assert.match(html,/--banner-delay:-0.200s/);
 const memo=new DelayMemo();memo.frame(100);assert.equal(memo.delay('k',99.5),-.5);memo.frame(101);assert.equal(memo.delay('k',99.5),-.5,'memoised');memo.frame(50);assert.equal(memo.delay('k',49.9,50).toFixed(2),'-0.10','rewind clears');
 assert.match(downLine(view('dead'),ME,100),/A teammate can revive you · you respawn when the wave is cleared/);
});

test('chip-later icon, possession arrow and interact prompts',()=>{
 const v=view('wave');assert.match(chipLaterIcon(v,ME,100),/data-action="pve-chips"[\s\S]*<b>2<\/b>[\s\S]*<kbd>C<\/kbd>/);assert.match(chipLaterIcon(v,ME,100),/data-tier="epic"/);
 assert.equal(chipLaterIcon(view('choosing'),ME,100),'','hidden while the screen is open');
 const pv=view('possession'),ps=snap(pv,{time:pv.bossView!.possessedAt!+35.5});assert.match(possessionArrow(ps,pv,ME,0),/pve-possess-arrow/);assert.equal(possessionArrow({...ps,time:pv.bossView!.possessedAt!+30},pv,ME,0),'');
 const m=PVE_MACHINES[0],s=snap(v,{players:[player(ME,{pos:{x:m.front.x,y:.9,z:m.front.z}})]});
 const p=promptFor(s,v,ME)!;assert.equal(p.kind,'chip');assert.match(promptHud(p),/<kbd>F<\/kbd><span>Buy chip \$200<\/span>/);
 const poor={...v,party:{...v.party,[ME]:{...v.party[ME],money:20}}};assert.match(promptHud(promptFor(s,poor,ME)),/data-afford="0"[\s\S]*Not enough money/);
 const med=PVE_MACHINES[6],sm=snap(v,{players:[player(ME,{pos:{x:med.front.x,y:.9,z:med.front.z},hp:40})]});assert.match(promptHud(promptFor(sm,v,ME)),/Heal \$60/);
 const dv=view('party'),dp=snap(dv);dp.players[0].pos={x:5,y:.9,z:5};dp.players[1]={...dp.players[1],pos:{x:5.5,y:.9,z:5},hp:0};assert.match(promptHud(promptFor(dp,dv,ME)),/Hold F[\s\S]*Revive P2 \$100/);
 assert.equal(promptFor(snap({...v,hold:true},{players:s.players}),{...v,hold:true},ME),null,'no prompt during holds');
});

test('Siren reveal: smile, then 4 slots with ??? beyond revealed and the booting current card, timed from cinematic.at',()=>{
 const v=view('reveal-2'),c=v.cinematic!;
 assert.equal(revealWindow(v,c.at+.5),'smile');assert.equal(revealWindow(v,c.at+2),'tray');assert.equal(revealWindow(v,c.until-.2),'fade');assert.equal(revealWindow(v,c.until+.1),null);
 const smile=sirenRevealHtml(v,c.at+.5);assert.match(smile,/class="siren-selection pve-smile" style="--selection-duration:1.40s;--selection-delay:-0.500s"/);assert.match(smile,/siren-stare/);
 const tray=sirenRevealHtml(v,c.at+2);
 assert.equal((tray.match(/class="pve-reveal-slot/g)||[]).length,4);assert.equal((tray.match(/>\?\?\?</g)||[]).length,2,'slots ≥ revealed are ???');
 assert.match(tray,/pve-reveal-slot pve-boot" data-slot="1"/);assert.match(tray,/draft-reveal/);assert.match(tray,/--reveal-delay:-0\.6/,'boot delay from smileUntil');
 assert.match(tray,/Find me\.\.\./);assert.match(tray,/Minion spawns are increased until he is found\./);assert.ok(!/ data-chip=/.test(tray),'boss cards never use data-chip');
 const four=sirenRevealHtml(view('reveal-4'),view('reveal-4').cinematic!.at+2);assert.equal((four.match(/>\?\?\?</g)||[]).length,0);
 for(const chip of SIREN_BOSS_CHIPS)assert.ok(four.includes(chip.name.replace("'",'&#39;')),chip.name);
 assert.equal(sirenRevealHtml(view('boss-1'),100),'','no reveal outside the cinematic');
 assert.match(bossIntroName('The Siren'),/class="siren-title" data-glitch="THE SIREN"/);assert.equal((bossIntroName('The Siren').match(/<i style="--i:/g)||[]).length,9);
});

test('menu: picker with Host online, party bots and Start at the Siren; PvE lobby hides PvP rows and marks stale clients',()=>{
 const body=testBossBody({classOptions:'',prefs:bossPrefs({bossHostOnline:true,bossPartyBots:2}),hosting:'desktop'});
 assert.match(body,/data-boss="siren"/);assert.match(body,/The Siren/);assert.match(body,/id="boss-host-online" checked/);assert.match(body,/<option value="2" selected>/);
 assert.match(body,/id="boss-skip-waves"/);assert.match(body,/data-action="start-boss"/);assert.match(body,/desktop app hosts/);
 assert.deepEqual(bossPrefs({bossPartyBots:9}),{bossHostOnline:false,bossPartyBots:0,bossSkipWaves:false,bossAutoOpenChips:true});
 const v=view('lobby'),s=snap({...v,helloed:[ME]},{phase:'lobby',players:[player(ME),player('p2',{name:'<b>x'}),player(PARTY_BOT_PREFIX+'1')]});
 const lobby=pveLobbyBody(s,{me:ME,owner:ME,code:'ABCD',codeLabel:'ROOM CODE',codeReady:true,invite:'https://x',local:false,classOptions:'',players:s.players});
 assert.match(lobby,/Needs the latest update/);assert.equal((lobby.match(/data-pilot-preview=/g)||[]).length,3);assert.match(lobby,/&lt;b&gt;x/);
 assert.ok(!/id="match-mode"|id="sets-to-win"|id="arena-map"|id="bot-count"/.test(lobby),'no PvP settings');
 assert.match(lobby,/data-action="ready"/);assert.match(lobby,/data-action="copy-code"/);
 assert.ok(!pveLobbyBody(s,{me:ME,owner:ME,code:'',codeLabel:'',codeReady:true,invite:'',local:true,classOptions:'',players:s.players}).includes('copy-code'),'offline lobby has no invite');
});

const src=(f:string)=>readFileSync(new URL('../src/client/'+f,import.meta.url),'utf8');
test('main.ts pins: Test Boss on home, boss intro keeps pointer lock, PvE idle input, one hud.innerHTML, pve-chips skipped by the global click',()=>{
 const main=src('main.ts');
 assert.match(main,/btn\('test-boss','Test Boss <span class="key">04<\/span>'\)/);
 assert.match(main,/if\(document\.pointerLockElement&&!intro\.boss\)document\.exitPointerLock\(\);return;/,'the boss intro does not leave pointer lock');
 assert.match(main,/function input\(\):Input\{if\(pveIdle\(\)\)return \{\.\.\.idleInput\(\)/);
 assert.match(main,/function pveIdle\(\)\{[^}]*pve\.hold\|\|!!chipScreen\?\.visible\|\|!!pve\.party\[id\]\?\.channel/,'idle during holds, the open screen and a revive channel');
 assert.equal((main.match(/hud\.innerHTML=/g)||[]).length,1);assert.match(main,/t\.closest\('#pve-chips'\)/);
 assert.match(main,/'test-boss'\]\.includes\(screen\)\)html=`<div class="pilot-lobby-layout">/);
 assert.match(main,/screen!=='create'&&screen!=='test-boss'\)return;/,'the desktop create guard accepts the picker');
 assert.match(main,/if\(type==='pve'\)\{if\(mode==='online'\)send\(pveNonce\.wrap/,'PvE commands travel as {type:chip,pve} with a nonce');
 assert.match(main,/!state\?\.pve\?\.hold\)\{yaw-=/,'no mouse look during holds');
});

test('pve.css: every animated selector has a reduced-motion rule; the HUD layout moves the feed and notice; no legacy teal',()=>{
 const css=src('pve/pve.css');assert.ok(!css.includes('#97f0d0'));
 const reduced=[...css.matchAll(/@media\(prefers-reduced-motion:reduce\)\{([\s\S]*?)\n\}/g)].map(m=>m[1]).join(' ');assert.ok(reduced.length>0);
 const plain=css.replace(/@media\(prefers-reduced-motion:reduce\)\{[\s\S]*?\n\}/g,'').replace(/@keyframes[^{]*\{(?:[^{}]*\{[^}]*\})*[^}]*\}/g,'');
 const missing:string[]=[];
 for(const m of plain.matchAll(/([^{}]+)\{([^{}]*)\}/g)){if(!/animation:(?!none)/.test(m[2]))continue;
  for(const sel of m[1].split(',').map(s=>s.trim().replace(/^@media[^{]*/,'')))if(sel&&!reduced.includes(sel))missing.push(sel);}
 assert.deepEqual(missing,[],'animated selectors without a reduced-motion rule');
 assert.match(css,/body\.pve \.death-announcement\{top:110px\}/);assert.match(css,/body\.pve \.notice\{top:150px\}/);
 assert.match(css,/\.menu-list \.btn:nth-child\(4\)/);
});
