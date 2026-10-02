// PvE S3 party bots: start pick opens at once, seeded 2-6 s think (at once when hit), highest-rarity choice, wave picks,
// no money, no buying or reviving, the exported partyBotInput brain (design §4.7, §11 S3).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {idleInput,v} from '../src/shared/config.js';
import {type PveOptions} from '../src/shared/pve/contract.js';
import {PICKS} from '../src/shared/pve/balance.js';
import {PVE_MACHINES} from '../src/shared/pve/layout.js';
import {Economy} from '../src/shared/pve/economy.js';
import {Recruits} from '../src/shared/pve/recruits.js';
import {partyBotInput} from '../src/shared/pve/recruit-ai.js';
import {bestOffer} from '../src/shared/pve/picks.js';
import {fakeParts,withPveParts,type FakeHorde} from './helpers/pve-fakes.js';
await initPhysics();

function world(o:Partial<PveOptions>={}){
 let econ!:Economy,rec!:Recruits;
 const {factory,made}=fakeParts({economy:h=>(econ=new Economy(h)),recruits:h=>(rec=new Recruits(h))});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,partyBots:2,...o}}));
 s.addPlayer('h1','h1','ranged');s.pve!.command('h1',{action:'hello',v:1},false);
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();s.pve!.command('h1',{action:'close'},false);
 return {s,econ,rec,horde:made.horde as FakeHorde,bots:s.pve!.partyBots()};
}
const place=(p:Player,x:number,z:number)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);};
const tick=(s:Simulation,n=1)=>{for(let i=0;i<n;i++){for(const p of s.players.values())if(!p.bot)p.inputAt=s.time;s.step();}};
/** Steps until every bot installed its open pick; returns the install time (from the open) and the chip per bot. */
function runPicks(s:Simulation,econ:Economy,bots:Player[],limit=8){
 const open=new Map(bots.map(b=>{const row=econ.partyView(b.id),k=row.picks.find(x=>x.id===row.open)!;return [b.id,{at:row.openAt!,offers:k.offers!}];}));
 const out=new Map<string,{after:number;chip:string;offers:string[]}>();const t0=s.time;
 while(out.size<bots.length&&s.time<t0+limit){tick(s);for(const e of s.events)if(e.type==='install'&&open.has(e.source??'')&&!out.has(e.source!)){const o=open.get(e.source!)!;out.set(e.source!,{after:e.time-o.at,chip:e.message!,offers:o.offers});}}
 return out;
}

test('party bots open the start pick at once, think a seeded 2-6 s with idle input, then take the highest-rarity offer',()=>{
 const {s,econ,rec,bots}=world();try{
  assert.equal(bots.length,2);
  for(const b of bots){const row=econ.partyView(b.id);assert.equal(row.kind,'bot');assert.equal(row.money,0);assert.ok(row.open);assert.equal(row.picks[0].source,'start');
   assert.equal(row.openAt,s.time);assert.ok(econ.choosing(b));
   const i=rec.inputFor(b,{...idleInput(),seq:4,attack:true,x:1});assert.equal(i!.attack,false);assert.equal(i!.x,0);}
  assert.equal(s.snapshot().players.find(p=>p.id===bots[0].id)?.weaponAway,true,'hologram pose: weapon hidden');
  const picks=runPicks(s,econ,bots);
  for(const b of bots){const r=picks.get(b.id)!;assert.ok(r,'picked');
   assert.ok(r.after>=PICKS.botThink[0]-1e-6&&r.after<=PICKS.botThink[1]+1/60+1e-6,`think ${r.after}`);
   assert.equal(r.chip,bestOffer(r.offers));assert.equal(b.chips[r.chip],1);assert.equal(econ.choosing(b),false);}
  // Same seed, same think times and choices.
  const again=world();try{const p2=runPicks(again.s,again.econ,again.bots);for(const b of bots)assert.deepEqual(p2.get(b.id),picks.get(b.id));}finally{again.s.dispose();}
 }finally{s.dispose();}
});

test('a bot hit while choosing picks at once; wave clears give a free common pick',()=>{
 const {s,econ,bots}=world();try{
  const b=bots[0];tick(s);s.damage(b,5,null,'environment');assert.equal(econ.partyView(b.id).hurtAt,s.time);
  tick(s);assert.ok(s.events.some(e=>e.type==='install'&&e.source===b.id),'picked on the next tick');assert.ok(s.time<1);
  econ.respawnAll('wave');const row=econ.partyView(b.id);const wave=row.picks.find(k=>k.source==='wave')!;assert.equal(wave.tier,'common');
  tick(s);assert.equal(econ.partyView(b.id).open,wave.id,'opens it at once');
  econ.respawnAll('phase');assert.equal(econ.partyView(b.id).picks.filter(k=>k.source==='wave').length,1,'no pick for a phase respawn');
 }finally{s.dispose();}
});

test('party bots never gain money, buy or revive',()=>{
 const {s,econ,bots}=world();try{
  const b=bots[0],h1=s.players.get('h1')!;
  econ.grant(b.id,1000,'test');econ.grantAll(100,'wave-clear');assert.equal(econ.money(b.id),0);assert.equal(econ.money('h1'),150);
  const f=PVE_MACHINES[0].front;place(b,f.x,f.z);
  place(h1,0,-8);tick(s);s.damage(h1,1e6,null,'environment');
  for(let i=0;i<120;i++){b.input={...b.input,interact:true};tick(s);}
  const row=econ.partyView(b.id);assert.equal(row.spent,0);assert.equal(row.chipsBought,0);assert.equal(row.channel,undefined);
  assert.equal(econ.pickupsView().length,0);assert.ok(h1.hp<=0);
  for(let i=0;i<30;i++){const inp=partyBotInput(s.pve!,b);assert.ok(!inp.interact);}
 }finally{s.dispose();}
});

test('partyBotInput: the dps brain leashed 12 m to the nearest living human; attacks a visible add',()=>{
 const {s,econ,horde,bots}=world();try{
  const b=bots[0],h1=s.players.get('h1')!;for(const x of bots)if(econ.choosing(x))runPicks(s,econ,[x]);
  place(h1,0,10);place(b,0,-6);tick(s);
  const far=partyBotInput(s.pve!,b);assert.ok(far.z>.7,`back toward the human: ${far.z}`);
  place(h1,0,1);place(b,0,-2);horde.addAt({x:0,z:-9},'brute',{hp:5000});tick(s,3);
  let attacked=false,closed=false;for(let i=0;i<120&&!attacked;i++){const inp=partyBotInput(s.pve!,b);attacked||=inp.attack;closed||=inp.z<-.3;tick(s);}
  assert.ok(attacked,`${b.spec} attacks the add (closing in: ${closed})`);
  s.damage(h1,1e6,null,'environment');const alone=partyBotInput(s.pve!,b);assert.ok(Number.isFinite(alone.x),'no human alive: no leash');
 }finally{s.dispose();}
});
