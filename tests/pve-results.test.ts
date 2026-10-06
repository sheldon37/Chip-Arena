// S4 results (design §7.7, §6.8): portraits, chips and stats per party member, Retry and Return to Menu at the bottom.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';
import {PARTY_BOT_PREFIX,type PveView} from '../src/shared/pve/contract.js';
import {pveFixture,fixtureBossPlayer} from '../src/shared/pve/fixtures.js';
import {pveResults,resultPlayers} from '../src/client/pve/results.js';

const ME='host',IDS=[ME,'p2',PARTY_BOT_PREFIX+'1','p3'];
const player=(id:string,o:Partial<PlayerView>={})=>({id,name:id==='p2'?'<script>':id.toUpperCase(),color:'#6fd3ff',class:'ranged',spec:'pistol',pos:{x:0,y:.9,z:0},hp:100,maxHp:100,
 chips:id===ME?{vital:2,ricochet:1}:{},bot:id.startsWith(PARTY_BOT_PREFIX)?'normal':null,connected:true,cosmetics:undefined,...o}) as PlayerView;
const snap=(pve:PveView,extra:PlayerView[]=[])=>({time:100,phase:'match',players:[...IDS.map(id=>player(id)),...extra],events:[],options:{pve:{boss:'siren',seed:1}},pve,result:'',winner:ME}) as unknown as Snapshot;

test('victory: a portrait canvas per party member, every stat, chips, MVP and the bottom actions',()=>{
 const v=pveFixture('results-victory',{time:100,ids:IDS}),s=snap(v),r=pveResults(s,ME,{});
 assert.equal(r.title,'VICTORY');assert.match(r.body,/The Siren is silenced/);
 assert.equal((r.body.match(/data-pilot-preview="/g)||[]).length,4,'one portrait per party member');
 assert.deepEqual(resultPlayers(s).map(p=>p.id),IDS,'portraits follow the party order (main.ts menuPlayers)');
 for(const k of ['Damage','Boss damage','Kills','Assists','Revives','Deaths','Earned'])assert.ok(r.body.includes(`<dt>${k}</dt>`),k);
 assert.match(r.body,/<dt>Boss damage<\/dt><dd>4,200<\/dd>/);assert.match(r.body,/\(5 heads\)/);
 assert.match(r.body,/Vital Core[\s\S]*×2/);assert.match(r.body,/No chips installed/);
 assert.equal((r.body.match(/data-mvp="1"/g)||[]).length,1);assert.match(r.body,/data-member="host" data-mvp="1"/);
 assert.match(r.body,/&lt;script&gt;/);assert.ok(!r.body.includes('<script>'));
 const bot=r.body.split(`data-member="${PARTY_BOT_PREFIX}1"`)[1].split('data-member=')[0];assert.match(bot,/>BOT</);assert.ok(!bot.includes('<dt>Earned</dt>'),'party bots earn nothing');
 const actions=r.body.slice(r.body.lastIndexOf('<div class="actions pve-result-actions">'));
 assert.ok(r.body.trimEnd().endsWith('</div>')&&actions.length>0,'the actions div is the last child (sticky bottom-centre in .panel)');
 assert.match(actions,/data-action="retry"[^>]*>[\s\S]*Retry[\s\S]*data-action="leave"[^>]*>[\s\S]*Return to Menu/);
});

test('defeat: title, boss HP left, Retry votes; a lost host offers only Return to Menu',()=>{
 const v={...pveFixture('results-defeat',{time:100,ids:IDS}),bossView:{id:'pve-boss',phase:3 as const,floor:0}},boss=fixtureBossPlayer(player(ME),3,{time:100,h:4});boss.hp=3000;
 const r=pveResults(snap(v,[boss]),ME,{votes:1});
 assert.equal(r.title,'DEFEAT');assert.match(r.body,/The Siren smiles/);assert.match(r.body,/kept <strong>\d+%<\/strong> health \(3,000 \/ /);
 assert.equal((r.body.match(/data-pilot-preview="/g)||[]).length,4,'the boss is never a result card');
 assert.match(r.body,/Waiting for 2 more…/);
 const lost=pveResults(snap(v,[boss]),ME,{peerLost:true});assert.ok(!lost.body.includes('data-action="retry"'));assert.match(lost.body,/data-action="leave"/);
 const waves=pveResults(snap(pveFixture('results-defeat',{time:100,ids:IDS})),ME);assert.match(waves.body,/fell on wave 1 of 3/);
});
