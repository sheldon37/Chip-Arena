// Menus: the home Boss Test pill and the simplified online room (ready summary + remembered Advanced Options fold).
import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {readySummary,readyStatus,roomAdvanced,roomAdvancedOpen,rememberRoomAdvanced,ROOM_ADVANCED_KEY} from '../src/client/room-options.js';
import {bossPill,pveLobbyBody} from '../src/client/pve/menu.js';
import {PARTY_BOT_PREFIX} from '../src/shared/pve/contract.js';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';

const memory=()=>{const m=new Map<string,string>();return {getItem:(k:string)=>m.get(k)??null,setItem:(k:string,v:string)=>void m.set(k,v),m};};

test('ready summary and status read at a glance',()=>{
 assert.match(readySummary(2,3),/<strong>2\/3<\/strong>READY/);assert.match(readySummary(2,3),/data-all-ready="0"/);assert.match(readySummary(3,3),/data-all-ready="1"/);
 assert.match(readySummary(0,0),/data-all-ready="0"/,'an empty room is not "all ready"');
 assert.match(readyStatus(1,3),/Waiting for 2 pilots/);assert.match(readyStatus(2,3),/Waiting for 1 pilot</);assert.match(readyStatus(3,3),/Everyone is ready/);
 assert.match(readyStatus(1,2,'pilot','The run starts when every pilot is ready.'),/The run starts/);
});

test('Advanced Options is collapsed by default, remembers its state and survives blocked storage',()=>{
 const store=memory();assert.equal(roomAdvancedOpen(store),false);
 rememberRoomAdvanced(true,store);assert.equal(store.m.get(ROOM_ADVANCED_KEY),'1');assert.equal(roomAdvancedOpen(store),true);
 rememberRoomAdvanced(false,store);assert.equal(roomAdvancedOpen(store),false);
 const broken={getItem:()=>{throw new Error('blocked');},setItem:()=>{throw new Error('blocked');}};
 assert.equal(roomAdvancedOpen(broken),false);assert.doesNotThrow(()=>rememberRoomAdvanced(true,broken));assert.equal(roomAdvancedOpen(undefined),false);
 assert.doesNotThrow(()=>roomAdvancedOpen(),'no localStorage in Node: the default store degrades to closed');
 const closed=roomAdvanced('<p>x</p>',false,'First to 5 <sets>'),open=roomAdvanced('<p>x</p>',true);
 assert.match(closed,/^<details class="lobby-advanced room-advanced" data-room-advanced >/);assert.match(open,/data-room-advanced open>/);
 assert.match(closed,/<summary><span>Advanced Options<\/span><small>First to 5 &lt;sets&gt;<\/small><\/summary><p>x<\/p><\/details>$/);
});

test('home: Boss Test is a labelled glossy pill with a skull, outside the main button list',()=>{
 const pill=bossPill();assert.match(pill,/^<button class="boss-pill" data-action="test-boss" aria-label="Boss test: fight the Siren">/);
 assert.match(pill,/<svg viewBox="0 0 48 48" aria-hidden="true"/);assert.match(pill,/<strong>BOSS TEST<\/strong>/);assert.equal((pill.match(/class="boss-pill-eye"/g)||[]).length,2);
 const main=readFileSync(new URL('../src/client/main.ts',import.meta.url),'utf8'),home=main.slice(main.indexOf("if(screen==='home'){html="),main.indexOf("else if(screen==='practice')"));
 assert.ok(!/data-action="test-boss"|btn\('test-boss'/.test(home.slice(home.indexOf('menu-list'),home.indexOf('</section>'))),'not inside the menu list');
 const css=readFileSync(new URL('../src/client/pve/pve.css',import.meta.url),'utf8');
 assert.match(css,/\.boss-pill\{[^}]*border-radius:999px/);assert.match(css,/\.boss-pill:focus-visible\{outline:/);assert.match(css,/@media\(max-width:580px\)\{\.boss-pill\{align-self:stretch/);
});

test('PvP room: essentials up front, rules/bots/connection under Advanced Options, settings ids and host gating unchanged',()=>{
 const main=readFileSync(new URL('../src/client/main.ts',import.meta.url),'utf8');
 const lobby=main.slice(main.indexOf(" else if(screen==='lobby'){"),main.indexOf("'PRIVATE ROOM');}"));
 assert.ok(lobby.length>0);
 assert.match(lobby,/\$\{readySummary\(ready,players\.length\)\}/);assert.match(lobby,/btn\('ready',me\?\.ready\?'Cancel Ready':'Ready'/);
 const advanced=lobby.indexOf('${roomAdvanced(`');assert.ok(advanced>0);
 assert.ok(lobby.indexOf("matchOptions(true,'essential')")<advanced,'mode and arena stay visible');
 for(const part of ["matchOptions(true,'advanced')",'botDraftOption(','botLoadouts(','id="invite-link"',"btn('network-report'"])assert.ok(lobby.indexOf(part)>advanced,part+' is under Advanced Options');
 assert.match(lobby,/host\?botLoadouts\(/,'bot loadouts stay host-only');
 assert.ok(lobby.indexOf("btn('copy-code'")<advanced,'the room code and copy buttons stay visible');
 // The practice/create screens keep the full block in its original order; the room splits the same pieces.
 assert.match(main,/part==='essential'\?mode\+arena:part==='advanced'\?ff\+sets\+rounds\+random\+botRow\+fine:mode\+ff\+sets\+rounds\+arena\+random\+botRow\+fine/);
 for(const id of ['match-mode','friendly-fire','sets-to-win','rounds-per-set','arena-map','random-map','bot-count','bot-difficulty'])assert.match(main,new RegExp(`id="${id}"[^>]*\\$\\{disabled\\}`),id+' keeps its id and owner-only disabled state');
 assert.match(main,/ui\.addEventListener\('toggle',e=>\{const t=e\.target;if\(t instanceof HTMLDetailsElement&&t\.hasAttribute\('data-room-advanced'\)\)rememberRoomAdvanced\(t\.open\);\},true\);/);
});

const player=(id:string,o:Partial<PlayerView>={})=>({id,name:id,class:'ranged',spec:'pistol',color:'#ff6b35',ready:false,bot:id.startsWith(PARTY_BOT_PREFIX)?'normal':null,connected:true,team:'red',cosmetics:{},...o}) as unknown as PlayerView;
test('PvE room: party ready count counts bots, invite stays visible, connection details fold away',()=>{
 const players=[player('me',{ready:true}),player('p2'),player(PARTY_BOT_PREFIX+'1')];
 const s={players,options:{pve:{boss:'siren'}},pve:{helloed:['me','p2']}} as unknown as Snapshot;
 const base={me:'me',owner:'me',code:'ABCD',codeLabel:'ROOM CODE',codeReady:true,invite:'https://x',local:false,classOptions:'',players};
 const body=pveLobbyBody(s,{...base,advancedOpen:false});
 assert.match(body,/<strong>2\/3<\/strong>READY/);assert.match(body,/Waiting for 1 pilot</);assert.match(body,/data-action="copy-code"/);
 assert.ok(body.indexOf('data-action="copy-code"')<body.indexOf('data-room-advanced'),'code outside the fold');
 assert.ok(body.indexOf('id="invite-link"')>body.indexOf('data-room-advanced')&&body.indexOf('data-action="network-report"')>body.indexOf('data-room-advanced'));
 assert.match(pveLobbyBody(s,{...base,advancedOpen:true}),/data-room-advanced open>/);
 const local=pveLobbyBody(s,{...base,local:true});assert.ok(!local.includes('data-room-advanced')&&!local.includes('copy-code'),'offline run: no invite and no fold');
});
