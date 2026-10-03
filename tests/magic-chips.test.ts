// Magic chips (design §8) and the catalogue invariants that used to sit behind the failing 182-count pins
// (rarity-combat.test.ts:9, chaos.test.ts:7, core.test.ts:10): those pins stay as known failures (§9.3), so their later
// asserts never ran. They are re-asserted here over all CHIPS, with no count pin.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {CHIPS,WEAPONS,RARITIES,eligibleChips,offerChips,statsFor} from '../src/shared/content.js';
import {MAGIC_CHIPS} from '../src/shared/magic-chips.js';
import {POWER_CHIPS} from '../src/shared/power-chips.js';
import {chipStatLines} from '../src/shared/chip-stats.js';
import {chipDescription} from '../src/shared/chip-description.js';
import {chipCard} from '../src/client/chip-card.js';
import {CHIP_ICONS} from '../src/client/icon-map.js';
import {ICON_PATHS} from '../src/client/icons.generated.js';
import {abilityCooldown,abilityCost,abilityRadius,abilityScale,maxCharges,kitFor} from '../src/shared/abilities.js';
import type {SpecId} from '../src/shared/config.js';
const near=(a:number,b:number,eps=1e-9)=>assert.ok(Math.abs(a-b)<=eps,`${a} vs ${b}`);
const SPECS=Object.keys(WEAPONS) as SpecId[],SCHOOLS=['fire','water','lightning','darkness'] as const;
const ids=(spec:SpecId,chips:Record<string,number>={})=>new Set(eligibleChips(spec,chips).map(c=>c.id));
const chip=(id:string)=>CHIPS.find(c=>c.id===id)!;

/** id, rarity, max, tag (design §8 table). */
const TABLE:[string,string,number,string][]=[
 ['rune-clock','common',4,'spellcraft'],['spellweave','common',4,'spellcraft'],['wide-sigils','uncommon',3,'spellcraft'],
 ['thrifty-runes','uncommon',3,'spellcraft'],['swift-orbs','uncommon',3,'spellcraft'],['spellblade','rare',2,'spellcraft'],
 ['mana-surge','rare',2,'spellcraft'],['blood-rune','rare',2,'spellcraft'],['twin-sigil','epic',1,'spellcraft'],
 ['arcane-overflow','epic',1,'spellcraft'],['elemental-attunement','legendary',1,'spellcraft'],['spell-echo','mythic',1,'spellcraft'],
 ['cinder-field','rare',1,'spell-fire'],['undertow','rare',1,'spell-water'],['storm-recall','epic',1,'spell-lightning'],
 ['abyssal-core','epic',1,'spell-darkness'],
];

test('16 magic chips: ids, names, rarities, copies and tags; spread right after the damage/health chips',()=>{
 assert.equal(MAGIC_CHIPS.length,16);
 assert.deepEqual(MAGIC_CHIPS.map(c=>[c.id,c.rarity,c.max,c.tags.join()]),TABLE);
 const start=CHIPS.indexOf(POWER_CHIPS[POWER_CHIPS.length-1])+1;assert.deepEqual(CHIPS.slice(start,start+16),MAGIC_CHIPS);
 assert.equal(new Set(CHIPS.map(c=>c.id)).size,CHIPS.length,'unique ids');
 const others=CHIPS.filter(c=>!MAGIC_CHIPS.includes(c));
 for(const c of MAGIC_CHIPS){assert.ok(!others.some(o=>o.name===c.name),c.name);assert.ok(c.prose,c.id);assert.equal(c.tags.length,1);}
 // Taken names and ids stay with their chips (design §8).
 assert.equal(chip('black-hole').name,'Event Horizon');assert.ok(chip('meteor')&&chip('overcharge'));
 // School chips are pure mechanics (magic-abilities.ts reads ctx.p.chips[id]); the 12 ability chips are pure stats.
 for(const c of MAGIC_CHIPS)if(c.tags[0]==='spellcraft'){assert.ok(c.effects.length>0&&!c.mechanic,c.id);}else{assert.equal(c.mechanic,c.id);assert.deepEqual(c.effects,[]);}
});

test('eligibility: spellcraft for the four non-DEV schools, school chips for their school only; never L33T, guns or melee',()=>{
 const spellcraft=MAGIC_CHIPS.filter(c=>c.tags[0]==='spellcraft').map(c=>c.id),school:Record<string,string>={fire:'cinder-field',water:'undertow',lightning:'storm-recall',darkness:'abyssal-core'};
 for(const spec of SPECS){
  const got=MAGIC_CHIPS.map(c=>c.id).filter(id=>ids(spec).has(id));
  assert.deepEqual(got,spec in school?[...spellcraft,school[spec]]:[],spec);
 }
 for(const spec of ['leet','pistol','rifle','machinegun','sword','daggers','shield','greatsword','emperor','ansatsuken','siren'] as SpecId[])assert.ok(!MAGIC_CHIPS.some(c=>ids(spec).has(c.id)),spec);
 // Pool impact per school (design §8): common +2, uncommon +3, rare +3 (+1 fire/water), epic +2 (+1 lightning/darkness),
 // legendary +1, mythic +2 (Spell Echo, and Pong is now eligible for magic).
 for(const spec of SCHOOLS){
  const now=eligibleChips(spec,{}),count=(r:string,list=now)=>list.filter(c=>c.rarity===r).length,before=now.filter(c=>!MAGIC_CHIPS.includes(c)&&c.id!=='pong');
  const delta=Object.fromEntries(['common','uncommon','rare','epic','legendary','mythic'].map(r=>[r,count(r)-count(r,before)]));
  assert.deepEqual(delta,{common:2,uncommon:3,rare:spec==='fire'||spec==='water'?4:3,epic:spec==='lightning'||spec==='darkness'?3:2,legendary:1,mythic:2},spec);
 }
});

test('excludes are symmetric: Twin Sigil and Spell Echo are never offered together',()=>{
 for(const c of CHIPS)for(const other of c.excludes??[]){assert.ok(chip(other),`${c.id} excludes ${other}`);assert.ok(chip(other).excludes?.includes(c.id),`${other} excludes ${c.id}`);}
 assert.deepEqual(chip('twin-sigil').excludes,['spell-echo']);assert.deepEqual(chip('spell-echo').excludes,['twin-sigil']);
 for(const spec of SCHOOLS){
  assert.ok(ids(spec).has('spell-echo')&&ids(spec).has('twin-sigil'));
  assert.ok(!ids(spec,{'twin-sigil':1}).has('spell-echo'));assert.ok(!ids(spec,{'spell-echo':1}).has('twin-sigil'));
 }
 let x=5;const random=()=>((x=(Math.imul(x,1664525)+1013904223)>>>0)/4294967296);
 for(let i=0;i<300;i++){assert.ok(!offerChips('darkness',{'twin-sigil':1},random,12).includes('spell-echo'));assert.ok(!offerChips('fire',{'spell-echo':1},random,12).includes('twin-sigil'));}
});

test('cards: prose without ×N, green/red/neutral lines, existing icons',()=>{
 const g=(text:string)=>({text,tone:'good'}),b=(text:string)=>({text,tone:'bad'}),n=(text:string)=>({text,tone:'neutral'});
 const lines:Record<string,{text:string;tone:string}[]>={
  'rune-clock':[g('-10% Ability cooldown')],spellweave:[g('+10% Ability damage')],'wide-sigils':[g('+15% Ability radius'),b('-5% Ability damage')],
  'thrifty-runes':[g('-15% Spell mana cost')],'swift-orbs':[g('+18% Projectile speed'),n('-20% Projectile drop')],spellblade:[g('+35% Next bolts after Q')],
  'mana-surge':[g('+30% Ability damage as mana')],'blood-rune':[g('+20% Ability life steal')],
  'twin-sigil':[g('+1 E charge'),b('+15% Ability cooldown'),n('Cannot be combined with Spell Echo')],
  'arcane-overflow':[g('+40% Ability damage at high mana'),g('+20 Max mana')],
  'elemental-attunement':[g('+25% Ability damage'),g('-20% Ability cooldown'),g('+10% Ability radius'),b('+15% Mana cost')],
  'spell-echo':[g('E repeats after 0.8 s'),n('Echo deals 50% damage'),n('Cannot be combined with Twin Sigil')],
  'cinder-field':[g('+50% Meteor burn duration'),g('+50% Meteor burn radius')],undertow:[n('Tidal Wave pulls instead of pushes')],
  'storm-recall':[g('-2 s Blink cooldown per enemy hit, up to 4 s')],'abyssal-core':[g('+25% Black Hole radius'),g('+20% collapse damage')],
 };
 for(const c of MAGIC_CHIPS){
  assert.deepEqual(chipStatLines(c),lines[c.id],c.id);
  const text=chipDescription(c);assert.equal(text,c.description);assert.ok(text.length>20&&/^[A-Z].*\.$/.test(text),c.id);
  assert.ok(!/\d+(?:\.\d+)?\s*[×x]|[×]\d/.test(text),c.id);assert.ok(!text.includes('undefined'));
  assert.ok(ICON_PATHS[CHIP_ICONS[c.id]],c.id);
  const html=chipCard(c);assert.ok(html.includes('<svg')&&html.includes(`aria-label="${c.name}`),c.id);
  for(const l of lines[c.id])assert.ok(html.includes(`<li class="chip-stat ${l.tone}">${l.text}</li>`),`${c.id}: ${l.text}`);
 }
});

test('stats reach the ability helpers with the design caps; max-stacked Meteor ≤ 160 at the centre and ≤ 6.75 m',()=>{
 const fire=kitFor('fire')!,meteor=fire.e,s=(chips:Record<string,number>)=>statsFor(chips,'fire'),base=s({});
 near(abilityCooldown(meteor,s({'rune-clock':4})),meteor.cooldown*.9**4);near(abilityCooldown(meteor,s({'twin-sigil':1})),meteor.cooldown*1.15);
 near(abilityCost(meteor,s({'thrifty-runes':3})),meteor.cost*.85**3);
 near(abilityRadius(meteor,s({'wide-sigils':1})),meteor.radius!*1.15);near(abilityRadius(meteor,s({'wide-sigils':3})),meteor.radius!*1.5,1e-9);
 near(abilityScale(s({spellweave:4}),false),1.1**4);near(abilityScale(s({'wide-sigils':3}),false),.95**3);
 near(abilityScale(s({'arcane-overflow':1}),true),1.4);near(abilityScale(s({'arcane-overflow':1}),false),1);assert.equal(s({'arcane-overflow':1}).maxMana,base.maxMana+20);
 assert.equal(maxCharges(base),1);assert.equal(maxCharges(s({'twin-sigil':1})),2);
 near(s({spellblade:2}).qEmpower,.7);near(s({'mana-surge':2}).manaSurge,.6);near(s({'blood-rune':2}).abilityLeech,.4);assert.equal(s({'spell-echo':1}).spellEcho,1);
 near(abilityScale(s({'spell-echo':1}),false,true),.5);
 near(s({'swift-orbs':3}).projectileSpeed,1.18**3);near(s({'swift-orbs':3}).projectileGravity,.8**3);
 const ea=s({'elemental-attunement':1});near(ea.abilityPower,1.25);near(ea.abilityCooldown,.8);near(ea.abilityRadius,1.1);near(ea.resourceCost,1.15);
 // Copies beyond max are ignored.
 near(abilityCooldown(meteor,s({'rune-clock':9})),meteor.cooldown*.9**4);
 // Max stack: every spellcraft chip and every damage chip at max copies (Twin Sigil instead of Spell Echo makes no
 // difference to the scale); Arcane Overflow's high-mana bonus on.
 const all=Object.fromEntries([...MAGIC_CHIPS,...POWER_CHIPS].filter(c=>c.id!=='twin-sigil').map(c=>[c.id,c.max])),max=s(all);
 assert.ok(max.damage>2.25,'the damage chips alone would exceed the cap');
 const centre=meteor.damage!*abilityScale(max,true);assert.ok(centre<=160,`Meteor centre ${centre}`);near(abilityScale(max,true),2.25);
 assert.ok(abilityRadius(meteor,max)<=6.75+1e-9,`Meteor radius ${abilityRadius(meteor,max)}`);
});

test('every catalogue chip has a real effect, a valid rarity, an eligible weapon; Pong and boomerang eligibility',()=>{
 for(const c of CHIPS){
  assert.ok(c.effects.length>0||c.mechanic,c.id);assert.ok(c.rarity in RARITIES,c.id);assert.ok(c.max>=1,c.id);
  for(const f of c.effects)assert.ok(Number.isFinite(f.value),`${c.id}.${f.stat}`);
  assert.ok(SPECS.some(spec=>eligibleChips(spec,{}).includes(c)),c.id);
 }
 // Rally Pong v2 (design §9.2) is eligible for every non-DEV gun and magic weapon.
 for(const spec of SPECS)assert.equal(ids(spec).has('pong'),['pistol','rifle','machinegun',...SCHOOLS].includes(spec),spec);
 assert.ok(!ids('sword').has('boomerang'));
});
