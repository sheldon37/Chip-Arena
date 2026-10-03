// PvE S3 personal picks: lazy offers by drop tier, rainbow, re-validation, commands, live install, forced idle while
// choosing, hurtAt (design §4.4, §5, §11 S3). Real Economy and Recruits with the fake horde and director.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {idleInput,v,type SpecId} from '../src/shared/config.js';
import {CHIPS,WEAPONS,eligibleChips,statsFor,type Rarity} from '../src/shared/content.js';
import {mulberry32} from '../src/shared/pve/rng.js';
import {ORDINARY_TIERS,OFFER_WEIGHTS} from '../src/shared/pve/balance.js';
import {type DropTier,type PveOptions} from '../src/shared/pve/contract.js';
import {Economy} from '../src/shared/pve/economy.js';
import {Recruits} from '../src/shared/pve/recruits.js';
import {applyChipsLive,bestOffer,offerWeights,rollOffers} from '../src/shared/pve/picks.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const RARITY=new Map(CHIPS.map(c=>[c.id,c.rarity]));
function world(o:Partial<PveOptions>={},humans:string[]=['h1']){
 let econ!:Economy;
 const {factory}=fakeParts({economy:h=>(econ=new Economy(h)),recruits:h=>new Recruits(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,...o}}));
 for(const id of humans){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
 return {s,econ};
}
const P=(s:Simulation,id:string)=>s.players.get(id)!;
const cmd=(s:Simulation,id:string,c:object)=>s.pve!.command(id,c,false);

test('start picks open at once with offers; other picks carry no offers until opened (D17)',()=>{
 const {s,econ}=world({},['h1','h2']);try{
  for(const id of ['h1','h2']){const row=econ.partyView(id);assert.equal(row.picks.length,1);assert.equal(row.picks[0].source,'start');assert.equal(row.open,row.picks[0].id);
   assert.equal(row.picks[0].offers?.length,6);assert.ok(econ.choosing(P(s,id)));}
  const snap=s.snapshot();assert.equal(snap.players.find(p=>p.id==='h1')?.weaponAway,true,'weapon hidden while choosing');
  const id=econ.grantPick('h1','epic','dispenser')!;assert.equal(econ.partyView('h1').picks.find(k=>k.id===id)?.offers,undefined);
  assert.equal(cmd(s,'h1',{action:'open',pick:id,n:1}),true);const row=econ.partyView('h1');assert.equal(row.open,id);assert.equal(row.picks.find(k=>k.id===id)?.offers?.length,6);
  const offers=row.picks.find(k=>k.id===id)!.offers!;cmd(s,'h1',{action:'close',n:2});cmd(s,'h1',{action:'open',pick:id,n:3});
  assert.deepEqual(econ.partyView('h1').picks.find(k=>k.id===id)!.offers,offers,'frozen once rolled');
 }finally{s.dispose();}
});

test('commands: validation, Choose later keeps the pick, the next pending pick opens, install is emitted',()=>{
 const {s,econ}=world();try{
  const h1=P(s,'h1'),start=econ.partyView('h1').open!;const second=econ.grantPick('h1','rare','dispenser')!;
  assert.equal(cmd(s,'h1',{action:'pick',pick:second,chip:'vital'}),false,'not open');
  assert.equal(cmd(s,'h1',{action:'pick',pick:start,chip:'no-such-chip'}),false,'not an offer');
  assert.equal(cmd(s,'h1',{action:'pick',pick:'pick-99',chip:'vital'}),false,'wrong pick');
  assert.equal(cmd(s,'h1',{action:'open',pick:'pick-99'}),false);
  assert.equal(cmd(s,'h1',{action:'close'}),true);assert.equal(cmd(s,'h1',{action:'close'}),false,'nothing open');
  assert.equal(econ.partyView('h1').picks.length,2,'Choose later keeps it');assert.equal(econ.choosing(h1),false);
  assert.equal(cmd(s,'h1',{action:'open'}),true);assert.equal(econ.partyView('h1').open,start,'default: the oldest pick');
  const chip=econ.partyView('h1').picks[0].offers![0];
  assert.equal(cmd(s,'h1',{action:'pick',pick:start,chip}),true);assert.equal(h1.chips[chip],1);
  assert.ok(s.events.some(e=>e.type==='install'&&e.source==='h1'&&e.message===chip));
  const row=econ.partyView('h1');assert.equal(row.picks.length,1);assert.equal(row.open,second,'next pending pick opens');assert.ok(row.picks[0].offers);
  // Bots and unknown ids are refused by the facade; humans only.
  assert.equal(s.pve!.command('nobody',{action:'open'},false),false);
  assert.equal(cmd(s,'h1',{action:'prefs',autoOpen:false}),true);
 }finally{s.dispose();}
});

test('offers exclude a chip an earlier pick maxed; picking a newly ineligible chip re-rolls instead of applying',()=>{
 const {s,econ}=world();try{
  const h1=P(s,'h1');
  for(let round=0;round<6;round++){
   const pickId=econ.partyView('h1').open!;const offers=econ.partyView('h1').picks.find(k=>k.id===pickId)!.offers!;
   const chip=offers[0],max=CHIPS.find(c=>c.id===chip)!.max;h1.chips[chip]=max-1;
   econ.grantPick('h1',(['common','rare','epic','legendary','mythic','uncommon'] as const)[round],'dispenser');
   assert.equal(cmd(s,'h1',{action:'pick',pick:pickId,chip}),true);assert.equal(h1.chips[chip],max);
   const next=econ.partyView('h1').picks.find(k=>k.id===econ.partyView('h1').open)!;assert.ok(!next.offers!.includes(chip),'maxed chip never offered again');
  }
  // Re-validation: a chip maxed after the offers were rolled.
  const pickId=econ.partyView('h1').open!,offers=[...econ.partyView('h1').picks.find(k=>k.id===pickId)!.offers!];
  const chip=offers[0],max=CHIPS.find(c=>c.id===chip)!.max;h1.chips[chip]=max;
  const before=JSON.stringify(h1.chips);
  assert.equal(cmd(s,'h1',{action:'pick',pick:pickId,chip}),true);
  assert.equal(JSON.stringify(h1.chips),before,'not applied');assert.equal(econ.partyView('h1').open,pickId,'screen stays open');
  const fresh=econ.partyView('h1').picks.find(k=>k.id===pickId)!.offers!;assert.ok(!fresh.includes(chip));
  assert.ok(s.events.some(e=>e.type==='pve-deny'&&e.target==='h1'&&e.message==='offers-refreshed'));
 }finally{s.dispose();}
});

test('offer-tier distribution per drop tier over 10,000 seeded rolls is within ±2 pp of OFFER_WEIGHTS',()=>{
 const p={spec:'pistol' as SpecId,chips:{}};
 for(const tier of ORDINARY_TIERS){
  const next=mulberry32(1000+ORDINARY_TIERS.indexOf(tier)),counts:Partial<Record<Rarity,number>>={},N=10000;
  for(let i=0;i<N;i++){const [id]=rollOffers(p,tier,next,1);const r=RARITY.get(id)!;counts[r]=(counts[r]??0)+1;}
  const row=OFFER_WEIGHTS[tier],total=Object.values(row).reduce((a,b)=>a+(b??0),0);
  for(const r of ORDINARY_TIERS){const want=100*(row[r]??0)/total,got=100*(counts[r]??0)/N;assert.ok(Math.abs(got-want)<=2,`${tier}→${r}: ${got.toFixed(2)} vs ${want.toFixed(2)}`);}
  assert.equal(Object.keys(counts).filter(r=>!(ORDINARY_TIERS as readonly string[]).includes(r)).length,0);
 }
 // The leet kit keeps its own tier at the row's legendary weight.
 assert.equal(offerWeights('rare','leet').l44t,OFFER_WEIGHTS.rare.legendary);assert.equal(offerWeights('rare','pistol').l44t,undefined);
});

test('rainbow: 8 mythic for ranged and magic kits, 7 mythic + 1 legendary for melee; never siren chips in any tier',()=>{
 const kits=Object.values(WEAPONS).filter(w=>!w.devOnly);
 for(const w of kits){
  const offers=rollOffers({spec:w.id,chips:{}},'rainbow',mulberry32(5),8),r=offers.map(id=>RARITY.get(id));
  assert.equal(offers.length,8,w.id);
  if(w.class==='melee'){assert.equal(r.filter(x=>x==='mythic').length,7,w.id);assert.equal(r.filter(x=>x==='legendary').length,1,w.id);}
  else assert.equal(r.filter(x=>x==='mythic').length,8,w.id);
 }
 const next=mulberry32(77);
 for(const w of kits)for(const tier of [...ORDINARY_TIERS,'rainbow'] as DropTier[])for(let i=0;i<20;i++)
  for(const id of rollOffers({spec:w.id,chips:{}},tier,next,8)){const c=CHIPS.find(x=>x.id===id)!;assert.notEqual(c.rarity,'siren');assert.ok(!c.tags.includes('siren-boss'));}
 // A rainbow pick through the economy offers 8.
 const {s,econ}=world();try{const id=econ.grantPick('h1','rainbow','dispenser')!;cmd(s,'h1',{action:'open',pick:id});assert.equal(econ.partyView('h1').picks.find(k=>k.id===id)!.offers!.length,8);}finally{s.dispose();}
});

test('applyChipsLive keeps damage taken; a dead player can pick and the stats apply on revive',()=>{
 const {s,econ}=world();try{
  const h1=P(s,'h1');h1.hp=40;assert.equal(h1.stats.hp,100);
  h1.chips.vital=1;applyChipsLive(h1);assert.equal(h1.stats.hp,125);assert.equal(h1.hp,65,'40 + Δ25');
  h1.chips.vital=0;applyChipsLive(h1);assert.equal(h1.hp,65,'a lower max keeps hp under the cap');assert.equal(h1.stats.hp,100);
  // Dead: pick now, stats on revive.
  s.damage(h1,1e6,null,'environment');assert.ok(h1.hp<=0);assert.equal(econ.partyView('h1').open,undefined,'death closes the screen');
  assert.equal(cmd(s,'h1',{action:'open'}),true,'a dead player may open');
  const pick=econ.partyView('h1').open!;const offers=econ.partyView('h1').picks[0].offers!;
  const vital=offers.includes('vital');const chip=vital?'vital':offers[0];
  assert.equal(cmd(s,'h1',{action:'pick',pick,chip}),true);assert.equal(h1.chips[chip],1);
  const statsBefore=h1.stats.hp;assert.equal(statsBefore,100,'stats wait for the revive');
  s.revivePlayer(h1,.5,s.pve!.standAt(0,0,h1));
  assert.deepEqual(h1.stats,statsFor(h1.chips,h1.spec),'stats applied on revive');
 }finally{s.dispose();}
});

test('choosing forces neutral attack, block, dash, secondary, jump and x/z (x/z kept with moveWhileChoosing); hurtAt on hits',()=>{
 const {s,econ}=world();try{
  const h1=P(s,'h1');assert.ok(econ.choosing(h1));
  const busy={...idleInput(),seq:9,yaw:1.2,pitch:.3,x:1,z:-1,attack:true,block:true,dash:true,secondary:true,jump:true,reload:true,interact:true,ability:true,camera:'third' as const};
  const i=s.pve!.filterInput(h1,busy);
  for(const k of ['attack','block','dash','secondary','jump','reload','ability'] as const)assert.equal(i[k],false,k);
  assert.equal(i.x,0);assert.equal(i.z,0);assert.equal(i.seq,9);assert.equal(i.yaw,1.2);assert.equal(i.pitch,.3);assert.equal(i.camera,'third');
  econ.moveWhileChoosing=true;const j=s.pve!.filterInput(h1,busy);assert.equal(j.x,1);assert.equal(j.z,-1);assert.equal(j.attack,false);assert.equal(j.ability,false);econ.moveWhileChoosing=false;
  assert.equal(econ.partyView('h1').hurtAt,undefined);s.step();s.damage(h1,5,null,'environment');assert.equal(econ.partyView('h1').hurtAt,s.time);
  cmd(s,'h1',{action:'close'});assert.equal(s.pve!.filterInput(h1,busy).attack,true,'control returns after Choose later');
 }finally{s.dispose();}
});

test('bestOffer takes the highest rarity (ties: first)',()=>{
 const byRarity=(r:Rarity)=>CHIPS.find(c=>c.rarity===r&&eligibleChips('pistol',{}).includes(c))!.id;
 assert.equal(bestOffer([byRarity('common'),byRarity('epic'),byRarity('rare')]),byRarity('epic'));
 const two=CHIPS.filter(c=>c.rarity==='rare').slice(0,2).map(c=>c.id);assert.equal(bestOffer(two),two[0]);
 assert.equal(bestOffer([]),undefined);void v;
});
