// Owner report 2026-10-03: "Life steal seems to not be working (Tested in boss mode)" (design §19.3). Every direct player hit
// on a PvE add now heals by the ACTUAL damage dealt (after limb and HP clamps): projectiles, melee swings and specials,
// thrown weapons, darkness drain, Vampire Dash, coin-toss heads and direct ability hits (chip life steal only). The real
// Horde runs against the foundation's fake director/economy/recruits; PvP stays identical (tests/pvp-golden.test.ts).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,idleInput,type Input,type SpecId} from '../src/shared/config.js';
import {statsFor,WEAPONS,magazineCapacity} from '../src/shared/content.js';
import {BOSS_PLAYER_ID} from '../src/shared/pve/contract.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const near=(a:number,b:number,eps=1e-6,msg='')=>assert.ok(Math.abs(a-b)<=eps,`${msg} ${a} vs ${b}`);
/** A PvE room with one party member `h1` (spec + chips, hurt so heals are not capped) and one active add 2.2 m ahead. */
function room(spec:SpecId,chips:Record<string,number>={},distance=2.2){
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:7}});s.addPlayer('h1','h1','ranged');s.pve!.command('h1',{action:'hello',v:1},false);s.phase='fight';s.fightStart=s.time;
  const p=s.players.get('h1')!,h=s.pve!.horde as Horde;p.spec=spec;p.class=WEAPONS[spec].class;p.chips=chips;p.stats=statsFor(chips,spec);p.stats.hp=5000;p.hp=1000;p.mana=p.stats.maxMana;p.stamina=p.stats.maxStamina;p.ammo=magazineCapacity(spec,p.stats);
  place(p,0,0);const n=h.spawn('shambler',{entry:4})!;for(let i=0;i<600&&h.record(n)?.phase!=='active';i++){p.inputAt=s.time;s.step();}
  const add=h.record(n)!;assert.equal(add.phase,'active');add.hp=add.maxHp=4000;add.minion!.hp=add.minion!.maxHp=4000;
  return {s,p,h,add,park:()=>{place(p,0,0);add.minion!.body.setTranslation(v(0,1,distance),true);add.minion!.body.setLinvel(v(),true);s.world.step();p.grounded=true;}};
 });
}
const place=(p:Player,x:number,z:number)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw:0,pitch:0};p.ext=v();};
const heals=(s:Simulation,from:number)=>s.events.filter(e=>e.type==='heal'&&e.target==='h1'&&e.id>from).reduce((n,e)=>n+(e.amount||0),0);
const lastId=(s:Simulation)=>s.events.at(-1)?.id??0;
/** Drives h1 for n ticks with `input`; returns {healed, dealt} (heal events vs the add's HP loss). */
function drive(r:ReturnType<typeof room>,n:number,input:Partial<Input>|((k:number)=>Partial<Input>)){
 const {s,p,add}=r,from=lastId(s),hp0=add.hp;let seq=p.input.seq;
 for(let k=0;k<n;k++){const i=typeof input==='function'?input(k):input;s.setInput('h1',{...idleInput(),seq:++seq,yaw:0,pitch:p.input.pitch,...i});p.inputAt=s.time;s.step();}
 return {healed:heals(s,from),dealt:hp0-add.hp};
}
const aimAtAdd=(r:ReturnType<typeof room>)=>{const a=r.add.minion!.body.translation(),e=r.p.body.translation();return Math.atan2(a.y-.1-(e.y+.5),Math.hypot(a.x-e.x,a.z-e.z));};

test('Vampiric Rounds pistol shots heal by the actual damage dealt to an add',()=>{
 const r=room('pistol',{'vampiric-rounds':1},6);try{r.park();const pitch=aimAtAdd(r);const out=drive(r,30,k=>({attack:k<2,pitch,camera:'first'}));
  assert.ok(out.dealt>0,'the shot hit the add');near(out.healed,out.dealt*.08,1e-6,'heal = actual × .08');}finally{r.s.dispose();}
});
test('sword life steal heals on normal swings and on the rising-slash special against adds',()=>{
 const r=room('sword',{leech:2});try{r.park();const swing=drive(r,40,k=>({attack:k<2}));assert.ok(swing.dealt>0,'swing hit the add');near(swing.healed,swing.dealt*.24,1e-6,'swing');
  r.park();r.p.cooldown=0;r.p.secondaryCd=0;r.p.stamina=r.p.stats.maxStamina;r.s.time+=2;const slash=drive(r,80,k=>({secondary:k<2}));assert.ok(slash.dealt>0,'rising slash hit the add');near(slash.healed,slash.dealt*.24,1e-6,'special');
 }finally{r.s.dispose();}
});
test('a thrown dagger heals by its actual damage on an add',()=>{
 const r=room('daggers',{leech:1},5);try{r.park();const pitch=aimAtAdd(r),out=drive(r,60,k=>({secondary:k<2,pitch}));assert.ok(out.dealt>0,'dagger hit the add');near(out.healed,out.dealt*.12,1e-6);}finally{r.s.dispose();}
});
test('darkness drain heals 15 % of the actual damage on adds (weapon drain)',()=>{
 const r=room('darkness',{},5);try{r.park();const pitch=aimAtAdd(r)+.06,out=drive(r,70,k=>({attack:k<2,pitch,camera:'first'}));assert.ok(out.dealt>0,'orb hit the add');near(out.healed,out.dealt*.15,1e-6);}finally{r.s.dispose();}
});
test('Vampire Dash drains 12 from an add within 3 m and heals by the actual damage',()=>{
 const r=room('pistol',{'vampire-dash':1},2);try{r.park();const out=drive(r,3,k=>({dash:k===0,x:1}));near(out.dealt,12,1e-6,'drained');near(out.healed,12,1e-6);}finally{r.s.dispose();}
});
test('coin-toss heads heals 6 on add hits with the same RNG draw as on players',()=>{
 const r=room('pistol',{'coin-toss':1},6);try{let heads=0,coins=0;for(let shot=0;shot<10;shot++){r.park();r.p.cooldown=0;r.p.ammo=7;const from=lastId(r.s);const pitch=aimAtAdd(r),out=drive(r,20,k=>({attack:k<1,pitch,camera:'first'}));
  const coin=r.s.events.filter(e=>e.type==='coin'&&e.id>from).length;coins+=coin;if(out.healed>0){heads++;near(out.healed,6,1e-9,'heads');}assert.ok(out.dealt>0);}
  assert.equal(coins,10,'one flip per hit');assert.ok(heads>0&&heads<10,`heads ${heads}/10`);}finally{r.s.dispose();}
});
test('a direct ability hit heals by chip life steal only (not the darkness drain); blasts never heal',()=>{
 const r=room('darkness',{'vampiric-rounds':1},4);try{r.park();const m=r.add.minion!,hp0=r.add.hp,from=lastId(r.s);
  const actual=r.s.abilities.hit(r.p,m,30,v(0,0,1),0,m.body.translation(),{direct:true});near(hp0-r.add.hp,actual,1e-9);near(heals(r.s,from),actual*.08,1e-9);
  const from2=lastId(r.s);r.s.abilities.blast(r.p,m.body.translation(),3,30,0,'singularity');assert.equal(heals(r.s,from2),0);
 }finally{r.s.dispose();}
});
test('overkill on a nearly dead add heals only the actual damage',()=>{
 const r=room('pistol',{vampire:1},6);try{r.park();r.add.hp=3;r.add.minion!.hp=3;const pitch=aimAtAdd(r),out=drive(r,30,k=>({attack:k<2,pitch,camera:'first'}));near(out.dealt,3,1e-9);near(out.healed,3*.5,1e-9,'Vampire King 50 % of 3');}finally{r.s.dispose();}
});
test('hitting the Siren boss heals as before (directHit player path)',()=>{
 const r=room('pistol',{'vampiric-rounds':1},40);try{const s=r.s;s.pve!.spawningBoss=true;const b=s.addPlayer(BOSS_PLAYER_ID,'The Siren','melee','normal','#b7a8ff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']})!;s.pve!.spawningBoss=false;
  place(r.p,0,0);b.body.setTranslation(v(0,.9,6),true);s.world.step();const from=lastId(s),hp0=b.hp;const e=r.p.body.translation(),t=b.body.translation(),pitch=Math.atan2(t.y-(e.y+.5),6);
  let seq=r.p.input.seq;for(let k=0;k<20;k++){s.setInput('h1',{...idleInput(),seq:++seq,attack:k<1,pitch,camera:'first'});r.p.inputAt=s.time;b.input={...idleInput(),seq:k};s.step();}
  const dealt=hp0-b.hp;assert.ok(dealt>0,'boss hit');near(heals(s,from),dealt*.08,1e-6);
 }finally{r.s.dispose();}
});
