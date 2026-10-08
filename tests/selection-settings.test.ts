import {test} from 'node:test';
import assert from 'node:assert/strict';
import {HostedRoom} from '../src/shared/host-room.js';
import {initPhysics} from '../src/shared/simulation.js';
import {DRAFT_REVEAL_SECONDS,selectionSeconds} from '../src/shared/draft-settings.js';
import {cosmetics,introText,DEFAULT_COSMETICS} from '../src/shared/cosmetics.js';
import {selectedChip} from '../src/client/draft-presentation.js';
import {CHIPS} from '../src/shared/content.js';
await initPhysics();
test('host timer reaches every snapshot, rejects guest mutations and leaves full reveal time after timeout',()=>{
 const r=new HostedRoom('ABCD2345',{name:'Host',class:'ranged',color:'#12ccff'},false,()=>{},6,false,false,45);
 for(let n=1;n<8;n++)assert.ok(r.join({name:'P'+n,cosmetics:{introText:'My entrance '+n}},false,'p'+n));
 assert.equal(r.sim.snapshot().options.selectionSeconds,45);assert.equal(r.receive('p1',{type:'options',selectionSeconds:120}),false);
 r.sim.start();assert.equal(r.sim.phaseEnd-r.sim.time,45);
 for(const p of r.sim.players.values()){p.offers=['vital'];p.selected=false;}r.sim.startDraft(['p1','p2']);const ready=r.sim.draft!.readyAt;
 r.sim.time=ready+44.99;r.sim.advance();assert.equal(r.sim.draft!.chosen,undefined);
 r.sim.time=ready+45;r.sim.advance();assert.equal(r.sim.draft!.chosen,'vital');const at=r.sim.time;
 assert.equal(r.sim.snapshot().players.find(p=>p.id==='p1')!.cosmetics?.introText,'My entrance 1');
 r.sim.time=at+DRAFT_REVEAL_SECONDS-.01;r.sim.advance();assert.equal(r.sim.draft!.player,'p1');
 r.sim.time=at+DRAFT_REVEAL_SECONDS+.01;r.sim.advance();assert.equal(r.sim.draft!.player,'p2');assert.ok(Math.abs(r.sim.phaseEnd-r.sim.draft!.readyAt-45)<1e-8);r.dispose();
 for(const [input,expected] of [[undefined,18],[NaN,18],[Infinity,18],['60',18],[-10,5],[0,5],[200,120],[30.6,31]])assert.equal(selectionSeconds(input),expected);
});
test('intro lines normalize safely and rendered player names remain plain text',()=>{
 assert.equal(introText(undefined),DEFAULT_COSMETICS.introText);assert.equal(introText(' \n '),DEFAULT_COSMETICS.introText);assert.equal(introText('  Hello\n  world\u202e '),'Hello world');
 assert.equal(Array.from(introText('🎉'.repeat(80))).length,64);assert.equal(cosmetics({introText:'Here comes trouble'}).introText,'Here comes trouble');
 const html=selectedChip(CHIPS[0],1,'<img src=x onerror=alert(1)>',1.5);assert.ok(!html.includes('<img'));assert.match(html,/&lt;img/);assert.match(html,/--selection-delay:-1.5s/);
});
