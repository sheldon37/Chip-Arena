// Damage / health chips, green-red chip-card stat lines and PvE-only money chips (owner request 2026-10-03).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {CHIPS,WEAPONS,eligibleChips,offerChips,statsFor,type Chip} from '../src/shared/content.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput,v,type SpecId} from '../src/shared/config.js';
import {DAMAGE_CHIPS,HEALTH_CHIPS,POWER_CHIPS} from '../src/shared/power-chips.js';
import {chipStatLines,effectLine} from '../src/shared/chip-stats.js';
import {chipCard} from '../src/client/chip-card.js';
import {chipScreenHtml} from '../src/client/pve/chip-screen.js';
import {CHIP_ICONS} from '../src/client/icon-map.js';
import {ICON_PATHS} from '../src/client/icons.generated.js';
import {PVE_MONEY_CHIPS,MONEY_OFFER_CHANCE,cardChip,moneyWithChips,rollMoneyChip} from '../src/shared/pve/money-chips.js';
import {withMoneyOffer,rollOffers,bestOffer} from '../src/shared/pve/picks.js';
import {mulberry32} from '../src/shared/pve/rng.js';
import {Economy} from '../src/shared/pve/economy.js';
import {Recruits} from '../src/shared/pve/recruits.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();
const near=(a:number,b:number,eps=1e-6)=>assert.ok(Math.abs(a-b)<eps,`${a} vs ${b}`);

function arena(spec:SpecId='pistol',chips:Record<string,number>={}){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b')!;a.spec=spec;a.chips=chips;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,8),true);a.input={...idleInput(),camera:'first'};s.world.step();return {s,a,b};
}

test('damage and health chips: a good portion across rarities, unique, eligible, with real stat effects and icons',()=>{
 assert.ok(DAMAGE_CHIPS.length>=10&&DAMAGE_CHIPS.length<=16);assert.ok(HEALTH_CHIPS.length>=10&&HEALTH_CHIPS.length<=16);
 const ids=new Set(CHIPS.map(c=>c.id));assert.equal(ids.size,CHIPS.length,'unique ids');
 for(const c of POWER_CHIPS){
  assert.ok(CHIPS.includes(c),c.id);assert.ok(c.effects.length>0&&!c.mechanic,c.id);
  assert.ok(Object.values(WEAPONS).some(w=>eligibleChips(w.id,{}).includes(c)),c.id);
  assert.ok(ICON_PATHS[CHIP_ICONS[c.id]],c.id);
  // Every effect changes the stats a pilot actually fights with.
  const one=statsFor({[c.id]:1},'pistol'),base=statsFor({},'pistol');
  for(const f of c.effects)assert.notEqual(one[f.stat],base[f.stat],`${c.id}.${f.stat}`);
 }
 for(const r of ['common','uncommon','rare','epic','legendary'] as const){assert.ok(DAMAGE_CHIPS.some(c=>c.rarity===r),'damage '+r);assert.ok(HEALTH_CHIPS.some(c=>c.rarity===r),'health '+r);}
 // Trade-off chips carry negatives.
 assert.ok(POWER_CHIPS.filter(c=>chipStatLines(c).some(l=>l.tone==='bad')).length>=10);
 // Weapon-class damage chips stay with their class.
 assert.ok(eligibleChips('pistol',{}).some(c=>c.id==='gunsmith')&&!eligibleChips('sword',{}).some(c=>c.id==='gunsmith'));
 assert.ok(eligibleChips('sword',{}).some(c=>c.id==='whetstone')&&!eligibleChips('fire',{}).some(c=>c.id==='whetstone'));
 assert.ok(eligibleChips('fire',{}).some(c=>c.id==='overcharge')&&!eligibleChips('rifle',{}).some(c=>c.id==='overcharge'));
 // PvP drafts can offer them.
 let x=3;const rnd=()=>((x=(Math.imul(x,1664525)+1013904223)>>>0)/4294967296);const seen=new Set<string>();
 for(let i=0;i<400;i++)for(const id of offerChips('pistol',{},rnd,6))seen.add(id);
 assert.ok(POWER_CHIPS.filter(c=>eligibleChips('pistol',{}).includes(c)).every(c=>seen.has(c.id)));
});

test('damage chips change real projectile and melee damage, crits, executes and damage taken',()=>{
 for(const [chips,mult] of [[{'power-cell':2},1.08**2],[{gunsmith:1},1.12],[{annihilator:1},1.75],[{apex:1},1.5]] as const){
  const {s,a}=arena('pistol',{...chips});s.spawnProjectile(a);near([...s.projectiles.values()][0].damage,23*mult,1e-6);s.dispose();
 }
 // Data-driven (abilities F): MAGIC raises the fire bolt damage, so the Overcharge multiplier is checked against WEAPONS.
 {const {s,a}=arena('fire',{overcharge:1});s.spawnProjectile(a);near([...s.projectiles.values()][0].damage,WEAPONS.fire.damage*1.45);near(a.stats.resourceCost,1.4);s.dispose();}
 {const {s,a,b}=arena('pistol',{finisher:1});b.hp=20;s.directHit(a,b,b.body.translation(),v(0,0,1),10,0,'x');near(b.hp,20-10*1.25);s.dispose();}
 {const {s,a,b}=arena('pistol',{'point-blank':1});b.body.setTranslation(v(0,1,3),true);s.world.step();s.directHit(a,b,b.body.translation(),v(0,0,1),10,0,'x');near(b.hp,100-13);s.dispose();}
 {const {s,a}=arena('pistol',{berserker:1});s.damage(a,10,'b','direct');near(a.hp,100-11.5);s.dispose();}
 {const {s,a}=arena('pistol',{'iron-will':2,hardened:1});s.damage(a,10,'b','direct');near(a.hp,100-10*.95*.95*.92);s.dispose();}
 near(statsFor({'keen-edge':2}).critical,.24);near(statsFor({'titan-edge':1},'sword').damage,1.4);
});

test('health chips change real maximum health, regeneration and life steal',()=>{
 {const {s,a}=arena('pistol',{'colossus-heart':1});assert.equal(a.stats.hp,200);assert.equal(a.hp,200);s.dispose();}
 {const {s,a}=arena('pistol',{stout:2,'heart-cell':1});near(a.stats.hp,(100+70)*1.12);s.dispose();}
 {const {s,a}=arena('sword',{warden:2});near(a.stats.hp,190);s.dispose();}
 {const {s,a}=arena('pistol',{'troll-blood':1});a.hp=50;a.lastDamage=-10;s.time=0;s.updatePlayer(a,1);near(a.hp,56);s.dispose();}
 {const {s,a,b}=arena('pistol',{'blood-pact':1});assert.equal(a.stats.hp,85);a.hp=40;s.directHit(a,b,b.body.translation(),v(0,0,1),20,0,'x');near(a.hp,44);s.dispose();}
 {const {s,a,b}=arena('pistol',{'vampiric-rounds':2});a.hp=40;s.directHit(a,b,b.body.translation(),v(0,0,1),25,0,'x');near(a.hp,44);s.dispose();}
 near(statsFor({'phoenix-core':1}).hp,140);near(statsFor({'giant-blood':2}).hp,156.25);
});

test('stat lines: description first, then green positives and red negatives derived from data',()=>{
 const colossus=CHIPS.find(c=>c.id==='colossus-heart')!;
 assert.deepEqual(chipStatLines(colossus),[{text:'+100% Health',tone:'good'},{text:'-12% Move speed',tone:'bad'},{text:'-12% Damage',tone:'bad'}]);
 const html=chipCard(colossus);
 assert.ok(html.indexOf('<p>Adds a huge amount of health.</p>')<html.indexOf('chip-stats'),'description first');
 assert.ok(html.includes('<li class="chip-stat good">+100% Health</li>'));assert.ok(html.includes('<li class="chip-stat bad">-12% Damage</li>'));
 assert.ok(html.includes('aria-label="Colossus Heart, Legendary. Adds a huge amount of health. +100% Health, -12% Move speed (penalty), -12% Damage (penalty)."'));
 // Old chips get lines too; lower-is-better stats flip colour.
 const line=(id:string)=>chipStatLines(CHIPS.find(c=>c.id===id)!);
 assert.deepEqual(line('vital'),[{text:'+25 Health',tone:'good'}]);
 assert.deepEqual(line('frame'),[{text:'-12% Damage taken',tone:'good'},{text:'-6% Move speed',tone:'bad'}]);
 assert.deepEqual(line('drum'),[{text:'+60% Magazine size',tone:'good'},{text:'+20% Reload time',tone:'bad'}]);
 assert.deepEqual(line('glass'),[{text:'+140% Damage',tone:'good'},{text:'-55% Health',tone:'bad'}]);
 assert.equal(effectLine({stat:'pong',op:'set',value:1}),null);
 // Pure mechanic chips print no list; every catalogue stat chip prints at least one line.
 assert.ok(!chipCard(CHIPS.find(c=>c.id==='necronomicon')!).includes('chip-stats'));
 for(const c of CHIPS.filter(c=>!c.mechanic&&!['pong','boomerang','zigzag'].includes(c.id)))assert.ok(chipStatLines(c).length>0,c.id);
});

test('money chips are PvE-only: never in CHIPS, PvP offers or eligibility; their cards say so',()=>{
 assert.ok(PVE_MONEY_CHIPS.length>=5);assert.ok(new Set(PVE_MONEY_CHIPS.map(c=>c.rarity)).size>=5);
 const ids=new Set(PVE_MONEY_CHIPS.map(c=>c.id));
 for(const c of PVE_MONEY_CHIPS){assert.ok(!CHIPS.some(x=>x.id===c.id));assert.ok(ICON_PATHS[CHIP_ICONS[c.id]],c.id);assert.equal(cardChip(c.id),c);
  const html=chipCard(c);assert.ok(html.includes('BOSS FIGHT ONLY'));assert.ok(html.includes('class="chip-stat good"'));}
 let x=11;const rnd=()=>((x=(Math.imul(x,1664525)+1013904223)>>>0)/4294967296);
 for(const w of Object.values(WEAPONS)){assert.ok(!eligibleChips(w.id,{}).some(c=>ids.has(c.id)));for(let i=0;i<60;i++)assert.ok(!offerChips(w.id,{},rnd,12).some(id=>ids.has(id)),w.id);}
 // rollOffers (catalogue) never returns them either; only withMoneyOffer adds one, and never to rainbow picks.
 const next=mulberry32(4);for(let i=0;i<200;i++)assert.ok(!rollOffers({spec:'pistol',chips:{}},'rare',next,6).some(id=>ids.has(id)));
 for(let i=0;i<200;i++){const base=['a','b','c','d','e','f'];assert.deepEqual(withMoneyOffer({chips:{}},'rainbow',next,base,6),base);}
 let added=0;for(let i=0;i<2000;i++){const out=withMoneyOffer({chips:{}},'common',next,['a','b','c','d','e','f'],6);assert.equal(out.length,6);if(ids.has(out[5]))added++;}
 assert.ok(Math.abs(added/2000-MONEY_OFFER_CHANCE)<.04,String(added));
 assert.equal(rollMoneyChip(Object.fromEntries(PVE_MONEY_CHIPS.map(c=>[c.id,c.max])),'epic',()=>0),undefined,'capped chips are not offered');
 assert.equal(bestOffer(['bounty-scanner','vital']),'vital','party bots skip money chips');
});

test('money chips scale PvE earnings through the economy grant path',()=>{
 assert.equal(moneyWithChips({'bounty-scanner':2},'kill',20),26);
 assert.equal(moneyWithChips({'bounty-scanner':2},'refund',250),250);
 assert.equal(moneyWithChips({'payday-loop':1},'wave-clear',60),85);
 assert.equal(moneyWithChips({headhunter:2},'head',5),17);
 assert.equal(moneyWithChips({'midas-core':1,'gold-rush':1},'wave-clear',60),(60+40)*1.8);
 assert.equal(moneyWithChips({'boss-contract':5},'boss',4),4*1.6,'copies cap at max');
 assert.equal(moneyWithChips({},'kill',20),20);
 let econ!:Economy;
 const ids=new Set(PVE_MONEY_CHIPS.map(c=>c.id));
 const {factory}=fakeParts({economy:h=>(econ=new Economy(h)),recruits:h=>new Recruits(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7}}));
 try{
  for(const id of ['h1','h2']){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
  s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
  s.players.get('h1')!.chips={'gold-rush':1};
  const m1=econ.money('h1'),m2=econ.money('h2');
  econ.grantAll(100,'wave-clear');econ.grant('h1',50,'refund');
  assert.equal(econ.money('h1')-m1,130+50);assert.equal(econ.money('h2')-m2,100);
  // A money pick through the economy installs the chip without touching combat stats.
  // Human picks roll a money offer in their last slot about 30% of the time (seeded economy stream).
  let pick='',money='',tries=0;
  for(;tries<40&&!money;tries++){pick=econ.grantPick('h2','epic','dispenser')!;s.pve!.command('h2',{action:'open',pick},false);
   const offers=econ.partyView('h2').picks.find(k=>k.id===pick)!.offers!;assert.equal(offers.length,6);money=offers.find(id=>ids.has(id))??'';}
  assert.ok(money,'a money chip was offered');
  const h2=s.players.get('h2')!,hp=h2.stats.hp;
  assert.equal(s.pve!.command('h2',{action:'pick',pick,chip:money},false),true);
  assert.equal(h2.chips[money],1);assert.equal(h2.stats.hp,hp);
  h2.chips={headhunter:1};const before=econ.money('h2');econ.grant('h2',5,'head');assert.equal(econ.money('h2')-before,11);
  // The PvE chip screen renders the money card with its green line.
  const html=chipScreenHtml({pick:{id:pick,tier:'epic',source:'dispenser',at:0,offers:['headhunter']},queued:0,offers:[cardChip('headhunter') as Chip],copies:{},openAt:0});
  assert.ok(html.includes('data-pve-chip="headhunter"')&&html.includes('+$6 per headshot kill'));
 }finally{s.dispose();}
});
