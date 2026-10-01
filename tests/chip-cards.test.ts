import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {CHIPS,RARITIES,WEAPONS,eligibleChips,offerChips,statsFor} from '../src/shared/content.js';
import {SIREN_CHIPS,SIREN_CHIP_ODDS} from '../src/shared/siren-chips.js';
import {SIREN_COMBAT} from '../src/shared/siren-content.js';
import {chipDescription} from '../src/shared/chip-description.js';
import {chipStackEffects} from '../src/shared/chip-inspection.js';
import {CHIP_ICONS} from '../src/client/icon-map.js';
import {ICON_PATHS} from '../src/client/icons.generated.js';
import {chipCard} from '../src/client/chip-card.js';
import {selectedChip,brokenChip} from '../src/client/draft-presentation.js';

const chip=(id:string)=>CHIPS.find(c=>c.id===id)!;
const seeded=(seed:number)=>()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/4294967296);

test('three Siren Chips: their own rarity, plain descriptions and icons, offered to the Siren only',()=>{
 assert.equal(SIREN_CHIPS.length,3);assert.equal(RARITIES.siren.weight,0);
 for(const c of SIREN_CHIPS){
  assert.ok(CHIPS.includes(c),c.id);assert.equal(c.rarity,'siren');assert.deepEqual(c.tags,['siren']);assert.equal(c.max,1);assert.ok(c.effects.length>0);
  assert.equal(chipDescription(c),c.description);assert.ok(!/\d+(?:\.\d+)?\s*[×x]|[×]\d/.test(c.description),c.id);
  assert.ok(ICON_PATHS[CHIP_ICONS[c.id]],c.id);
  for(const w of Object.values(WEAPONS))assert.equal(eligibleChips(w.id,{}).includes(c),w.id==='siren',`${c.id} for ${w.id}`);
 }
});

test('Siren Chips change stats the Siren honours',()=>{
 assert.equal(statsFor({'siren-crown':1},'siren').hp,SIREN_COMBAT.hp+3000);
 const hide=statsFor({'siren-hide':1},'siren');assert.equal(hide.thorns,.3);assert.equal(hide.knockbackTaken,.5);
 const brk=statsFor({'siren-break':1},'siren'),base=statsFor({},'siren');
 assert.ok(Math.abs(brk.damage/base.damage-1.25)<1e-9);assert.ok(Math.abs(brk.speed/base.speed-1.12)<1e-9);assert.ok(Math.abs(brk.damageTaken/base.damageTaken-1.15)<1e-9);
 assert.deepEqual(chipStackEffects(chip('siren-hide'),1).map(r=>r.label),['Damage returned to attackers','Received knockback']);
});

test('Siren drafts guarantee two Siren Chips while they last, like the other exclusive kits',()=>{
 const random=seeded(77);
 for(let i=0;i<300;i++){const offers=offerChips('siren',{},random,6);assert.equal(new Set(offers).size,6);assert.ok(offers.slice(0,SIREN_CHIP_ODDS.guaranteedExclusives).every(id=>id.startsWith('siren-')));}
 const owned=Object.fromEntries(SIREN_CHIPS.map(c=>[c.id,1]));assert.equal(offerChips('siren',owned,random,6).filter(id=>id.startsWith('siren-')).length,0);
 delete owned['siren-break'];assert.equal(offerChips('siren',owned,random,6)[0],'siren-break');
 for(const spec of ['emperor','ansatsuken','pistol','sword'] as const)for(let i=0;i<50;i++)assert.ok(!offerChips(spec,{},random,12).some(id=>id.startsWith('siren-')),spec);
});

test('package cards: rank pips on ordinary tiers, stack slots for short stacks, safe glitch text',()=>{
 for(const [r,rank] of Object.entries({common:1,uncommon:2,rare:3,epic:4,legendary:5,mythic:6})){const html=chipCard(CHIPS.find(c=>c.rarity===r)!);assert.match(html,new RegExp(`--rank:${rank};`));assert.match(html,/tier-pips/);}
 for(const r of ['mishima','ansatsuken','l44t','siren']){const html=chipCard(CHIPS.find(c=>c.rarity===r)!);assert.match(html,/--rank:0;/);assert.ok(!html.includes('tier-pips'),r);}
 assert.ok(!chipCard(chip('vital')).includes('stack-slots'),'twelve copies is text only');assert.match(chipCard(chip('skirmisher'),1),/--max:3;--have:1;[^"]*"[^>]*>[\s\S]*stack-slots/);
 assert.match(chipCard(chip('skirmisher'),1,{chosen:true}),/--have:2;/);
 const siren=chipCard(chip('siren-hide'));assert.match(siren,/siren-aura/);assert.match(siren,/data-glitch="Black Flame Hide"/);assert.match(siren,/--rarity:#b7a8ff/);
 assert.equal((siren.match(/<i style=/g)||[]).length,16,'ember particles only');assert.ok(!/ id="/.test(siren),'no element ids repeated across cards');
 assert.ok(!chipCard(chip('vital')).includes('data-glitch'));
});

test('Siren selection: its own reveal on the shared draft timing; shards keep only the static package',()=>{
 const html=selectedChip(chip('siren-crown'),1,'<b>x</b>',.5);
 assert.match(html,/siren-selection/);assert.match(html,/siren-cinema/);assert.match(html,/siren-stare/);assert.match(html,/THE GRIN WIDENS/);assert.match(html,/--selection-duration:3\.2s/);assert.ok(!html.includes('<b>x</b>'));
 assert.ok(!selectedChip(chip('vital'),1,'P',0).includes('siren-cinema'));
 assert.match(brokenChip(chip('siren-crown'),0,1,.2),/chip-shard/);
 const css=readFileSync(new URL('../src/client/chip-cards.css',import.meta.url),'utf8');
 assert.match(css,/\.chip-shard \.chip-particles,\.chip-shard \.siren-flames/);
 const reduced=[...css.matchAll(/@media\(prefers-reduced-motion:reduce\)\{([\s\S]*?)\}\s*\n?$/gm)].map(m=>m[1]).join(' ');
 for(const k of ['.siren-tongue','.siren-tear','.rarity-siren strong:before','.emperor-aura:before','.draft-reveal.rarity-siren','.siren-stare'])assert.ok(reduced.includes(k),k+' has a reduced-motion fallback');
 assert.ok(readFileSync(new URL('../src/client/main.ts',import.meta.url),'utf8').includes("import './chip-cards.css';"));
});
