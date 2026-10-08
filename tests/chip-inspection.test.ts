import {DRAFT_REVEAL_SECONDS} from '../src/shared/draft-settings.js';
import {test} from 'node:test';import assert from 'node:assert/strict';
import {CHIPS,offerChips,eligibleChips,statsFor} from '../src/shared/content.js';
import {chipStackEffects} from '../src/shared/chip-inspection.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
await initPhysics();
const summary=(id:string,n:number)=>chipStackEffects(CHIPS.find(c=>c.id===id)!,n);
test('board stack summaries show additive values, multiplicative tradeoffs, fixed triggers and caps',()=>{
 assert.deepEqual(summary('vital',3),[{label:'Maximum health',value:'+75'}]);
 assert.deepEqual(summary('overclock',2),[{label:'Attack rate',value:'×1.392'},{label:'Damage per hit',value:'×0.846'}]);
 assert.deepEqual(summary('static',3),[{label:'Direct hits per discharge',value:'4'},{label:'Discharge damage',value:'+21'}]);
 assert.equal(summary('magazine',2)[0].value,'+50%');assert.equal(summary('ember',2)[0].value,'+6');assert.equal(summary('poison',2)[0].value,'+5');assert.equal(summary('poison',99)[0].value,'+15');assert.deepEqual(summary('vital',0),[]);
});
test('owned Chips remain eligible until their cap; a repeated authoritative draft installs the second copy',()=>{
 const s=new Simulation({skipIntros:true,countdown:.01,resultSeconds:.01});const a=s.addPlayer('a')!,b=s.addPlayer('b')!;s.start();s.selectSpec('a','pistol');s.selectSpec('b','pistol');s.step();b.chips.vital=1;b.stats=statsFor(b.chips);
 assert.ok(offerChips('pistol',b.chips,()=>0).includes('vital'));assert.ok(eligibleChips('pistol',{vital:1}).some(c=>c.id==='vital'));assert.ok(!eligibleChips('pistol',{vital:12}).some(c=>c.id==='vital'));
 for(let n=0;n<2;n++){b.hp=0;s.resolveRound();s.step();if(n===0)s.step();}assert.equal(s.phase,'draft');b.offers=offerChips(b.spec,b.chips,()=>0);s.time=s.draft!.readyAt;assert.ok(s.selectChip('b','vital'));s.time+=DRAFT_REVEAL_SECONDS+.01;s.advance();assert.equal(b.chips.vital,2);assert.equal(b.stats.hp,150);assert.equal(b.hp,150);assert.equal(a.score,1);assert.equal(s.snapshot().players[1].chips.vital,2);s.dispose();
});
