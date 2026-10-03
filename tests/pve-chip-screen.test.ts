// S4 personal chip screen (design §7.6, §5): pure markup, visibility and the attacked warning.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';
import {PARTY_BOT_PREFIX,type PveView} from '../src/shared/pve/contract.js';
import {pveFixture,fixtureCinematic} from '../src/shared/pve/fixtures.js';
import {CHIPS} from '../src/shared/content.js';
import {chipScreenModel,chipScreenVisible,chipScreenHtml,alarmHtml} from '../src/client/pve/chip-screen.js';
import {chipCard} from '../src/client/chip-card.js';

const ME='host',IDS=[ME,'p2',PARTY_BOT_PREFIX+'1','p3'];
const player=(id:string,o:Partial<PlayerView>={})=>({id,name:id,color:'#ff6b35',class:'ranged',spec:'pistol',pos:{x:0,y:.9,z:0},hp:100,maxHp:100,chips:{vital:2},bot:null,connected:true,...o}) as PlayerView;
const snap=(pve:PveView,o:Partial<Snapshot>={})=>({time:100,phase:'fight',players:[player(ME)],events:[],options:{pve:{boss:'siren',seed:1}},pve,...o}) as unknown as Snapshot;
const choosing=()=>pveFixture('choosing',{time:100,ids:IDS});

test('the open pick drives the model: offers in order, queue count, copies owned',()=>{
 const m=chipScreenModel(snap(choosing()),ME)!;
 assert.equal(m.pick.id,'pick-2');assert.equal(m.queued,1);assert.deepEqual(m.offers.map(c=>c.id),['vital','ricochet','piercing','splitter','vacuum','pong']);assert.equal(m.copies.vital,2);
 assert.equal(chipScreenModel(snap(pveFixture('wave',{time:100,ids:IDS})),ME),null,'no open pick, no screen');
});

test('cards use data-pve-chip (never data-chip), one per offer, with Choose later and number keys',()=>{
 const m=chipScreenModel(snap(choosing()),ME)!,html=chipScreenHtml(m,false,0);
 assert.equal((html.match(/data-pve-chip="/g)||[]).length,m.offers.length,'card count equals offers');
 assert.ok(!/ data-chip=/.test(html),'never data-chip: the global draft click handler must not see these cards');
 assert.match(html,/data-action="pve-later"[^>]*>[\s\S]*Choose later/);assert.match(html,/<kbd>C<\/kbd><kbd>Esc<\/kbd>/);
 assert.match(html,/data-pve-pick="pick-2"/);assert.match(html,/<kbd>1<\/kbd>[\s\S]*<kbd>6<\/kbd>/);
 assert.match(html,/DISPENSED · EPIC/);assert.match(html,/1 more waiting/);
 const rolling=chipScreenHtml({...m,offers:[]},false,0);assert.match(rolling,/Rolling your offers/);assert.ok(!rolling.includes('data-pve-chip='));
 const start=chipScreenHtml({...m,pick:{...m.pick,source:'start',tier:'common'},queued:0},false,0);assert.match(start,/BEGINNING CHIP/);assert.ok(!start.includes('more waiting'));
 assert.match(chipScreenHtml({...m,pick:{...m.pick,tier:'rainbow'}},false,0),/pve-chip-screen rainbow[\s\S]*RAINBOW · 8 MAX-RARITY CHOICES/);
});

test('the chip-card attr option leaves the default card byte-identical',()=>{
 for(const c of CHIPS.slice(0,40)){const plain=chipCard(c,1,{index:2,elapsed:.5});assert.equal(chipCard(c,1,{index:2,elapsed:.5,attr:undefined}),plain);
  assert.equal(chipCard(c,1,{index:2,elapsed:.5,attr:'data-pve-chip'}),plain.replace(` data-chip="${c.id}"`,` data-pve-chip="${c.id}"`));}
});

test('the warning appears iff attacked',()=>{
 const m=chipScreenModel(snap(choosing()),ME)!;
 const calm=chipScreenHtml(m,false,0),hit=chipScreenHtml(m,true,0);
 assert.ok(!calm.includes('WARNING'));assert.match(calm,/data-attacked="0"/);
 assert.match(hit,/data-attacked="1"/);assert.match(hit,/class="pve-alarm" role="alert"[\s\S]*WARNING — YOU ARE UNDER ATTACK/);
 assert.equal(alarmHtml(false),'');assert.match(alarmHtml(true),/UNDER ATTACK/);
});

test('hidden during every cinematic, outside the fight, after Choose later and without an open pick',()=>{
 const v=choosing();assert.equal(chipScreenVisible(snap(v),ME),true);
 for(const kind of ['reveal','boss-intro','outro'] as const)assert.equal(chipScreenVisible(snap({...v,cinematic:fixtureCinematic(kind,{time:100})}),ME),false,kind);
 assert.equal(chipScreenVisible(snap(v,{phase:'match'}),ME),false);assert.equal(chipScreenVisible(snap(v),ME,true),false,'locally closed');
 assert.equal(chipScreenVisible(snap(v),'p2'),false,'only the chooser sees it');
 const {pve:_,...pvp}=snap(v) as Snapshot&{pve?:PveView};assert.equal(chipScreenVisible(pvp as Snapshot,ME),false,'never in PvP');
});
