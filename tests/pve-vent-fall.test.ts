// PvE vent pits (owner 2026-10-04, src/shared/pve/vent-pits.ts): an open Ward Zero vent is a pit. Falls are an instant
// 'vent-fall' kill with the usual attacker credit; the body lands with a splat at a clear centre spot 10 s later and only
// then can be revived; wave/phase respawns during the shaft time skip the splat; adds and the boss never fall.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,type Input} from '../src/shared/config.js';
import {SIREN_VENTS,sirenStageBlocks} from '../src/shared/siren-stage-layout.js';
import {BOSS_PLAYER_ID,PVE_EVENT,type PveEntryView,type PveHost,type PveOptions} from '../src/shared/pve/contract.js';
import {PRICES,revivePrice} from '../src/shared/pve/balance.js';
import {Economy} from '../src/shared/pve/economy.js';
import {Horde} from '../src/shared/pve/horde.js';
import {VENT_PIT,entryOpenAmount,inVentShaft,splatSpot,ventAt} from '../src/shared/pve/vent-pits.js';
import {FakeHorde,fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

/** FakeHorde with scripted entry views (the vent open/close schedule the client animates from). */
class VentHorde extends FakeHorde{
 entries:PveEntryView[]=[];
 views(slots:readonly string[]){return {...super.views(slots),entries:this.entries};}
}
interface World{s:Simulation;econ:Economy;horde:VentHorde}
function world(humans=['h1','h2'],o:Partial<PveOptions>={}):World{
 let econ!:Economy,horde!:VentHorde;
 const {factory}=fakeParts({economy:h=>(econ=new Economy(h)),horde:(h:PveHost)=>(horde=new VentHorde(h))});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,...o}}));
 for(const id of humans){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
 for(const p of s.players.values())if(econ.choosing(p))s.pve!.command(p.id,{action:'close'},false);
 return {s,econ,horde};
}
const P=(s:Simulation,id:string)=>s.players.get(id)!;
const place=(p:Player,x:number,z:number,y=.9)=>{p.body.setTranslation(v(x,y,z),true);p.body.setLinvel(v(),true);};
const HELD=new WeakMap<Simulation,Set<string>>();
const held=(s:Simulation)=>{let h=HELD.get(s);if(!h){h=new Set();HELD.set(s,h);}return h;};
const tick=(s:Simulation,n=1)=>{for(let i=0;i<n;i++){for(const p of s.players.values())if(!p.bot){p.input={...p.input,x:0,z:0,interact:held(s).has(p.id),seq:p.input.seq+1};p.inputAt=s.time;}s.step();}};
function tapF(s:Simulation,p:Player){held(s).add(p.id);tick(s);held(s).delete(p.id);tick(s);}
const until=(s:Simulation,t:number,each?:()=>void)=>{while(s.time<t-1e-9){tick(s);each?.();}};
const events=(s:Simulation,type:string)=>s.events.filter(e=>e.type===type);
const V0=SIREN_VENTS[0];
/** Vent 0 fully open from now for `open` seconds (a diving add's grate). */
const openVent=(w:World,e=0,open=30)=>{w.horde.entries=[{e,openAt:w.s.time-2,closeAt:w.s.time+open,mode:'dive'}];};
function spawnBoss(s:Simulation,x:number,z:number){s.pve!.spawningBoss=true;const boss=s.addPlayer(BOSS_PLAYER_ID,'The Siren','melee','normal','#b7a8ff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']})!;s.pve!.spawningBoss=false;boss.stats.hp=boss.hp=1e7;place(boss,x,z);return boss;}
function blocked(x:number,z:number){return sirenStageBlocks().some(b=>b.pos.y+b.size.y/2>.05&&b.pos.y-b.size.y/2<2.5&&b.size.x<60&&Math.abs(x-b.pos.x)<b.size.x/2+.4&&Math.abs(z-b.pos.z)<b.size.z/2+.4);}

test('a player walking onto an open vent falls in: instant vent-fall kill crediting the recent attacker, landing scheduled 10 s later',()=>{
 const w=world();const {s}=w;try{
  const h1=P(s,'h1'),boss=spawnBoss(s,0,20);place(h1,V0.x+3,V0.z);openVent(w);tick(s,5);assert.ok(h1.hp>0,'beside the vent: alive');
  s.damage(h1,5,BOSS_PLAYER_ID,'direct');tick(s,30);
  // Walk in: step by step across the rim into the hole.
  let fellAt=NaN;for(let k=0;k<40&&h1.hp>0;k++){const x=V0.x+3-k*.1;place(h1,x,V0.z);tick(s);if(h1.hp<=0)fellAt=s.time;}
  assert.ok(h1.hp<=0&&h1.death,'fell');assert.ok(Math.abs(h1.death!.pos.x-V0.x)<=VENT_PIT.fall+1e-6,'only once over the hole');
  const d=h1.death!;assert.equal(d.kind,'vent-fall');assert.equal(d.effect,'vent-fall');assert.equal(d.source,BOSS_PLAYER_ID,'recent attacker credited');
  assert.equal(d.announcement?.killer,boss.name);assert.ok(Math.abs(d.landAt!-(d.at+VENT_PIT.splatAt))<1e-9);assert.ok(Math.abs(d.at-fellAt)<1e-9);
  assert.ok(events(s,'death').some(e=>e.target==='h1'&&e.message==='vent-fall'&&e.source===BOSS_PLAYER_ID));
  assert.ok(events(s,'pve-vent-fall').some(e=>e.target==='h1'));assert.equal(s.pve!.stats('h1').deaths,1);
  assert.equal(inVentShaft(d,s.time),true);
 }finally{s.dispose();}
});

test('closed vents stay solid floor; a vent is a pit only from the lethal open amount on; a jump over clears it',()=>{
 const w=world(['h1']);const {s}=w;try{
  const h1=P(s,'h1');place(h1,V0.x,V0.z);tick(s,90);assert.ok(h1.hp>0,'closed vent: solid');
  assert.ok(Math.abs(h1.body.translation().y-s.pve!.standAt(V0.x,V0.z,h1).y)<.2,'standing on it');
  // Just starting to open (a climbing add's push, then the lid swing): alive until the open amount reaches VENT_PIT.lethal.
  w.horde.entries=[{e:0,openAt:s.time,closeAt:s.time+5,mode:'spawn'}];const view=w.horde.entries[0];
  let died=NaN;for(let i=0;i<90&&h1.hp>0;i++){place(h1,V0.x,V0.z);tick(s);if(h1.hp<=0)died=s.time;}
  assert.ok(h1.hp<=0);assert.ok(entryOpenAmount('vent',view,died)>=VENT_PIT.lethal&&entryOpenAmount('vent',view,died-1/60)<VENT_PIT.lethal,'dies on the first lethal tick');
 }finally{s.dispose();}
 const w2=world(['h1']);try{
  const h1=P(w2.s,'h1');openVent(w2);place(h1,V0.x,V0.z,3.2);w2.s.step();assert.ok(h1.hp>0,'airborne over the pit: no fall');
 }finally{w2.s.dispose();}
});

test('the splat: at exactly death+10 s the body lands at a clear spot at the middle (nudged off the boss); revivable only after it',()=>{
 const w=world();const {s,econ}=w;try{
  const h1=P(s,'h1'),h2=P(s,'h2');place(h2,V0.x+1.6,V0.z);openVent(w);place(h1,V0.x,V0.z);tick(s);assert.ok(h1.hp<=0);
  const fall=h1.death!,landAt=fall.landAt!,price=revivePrice(0);econ.grant('h2',price*3,'t');const money=econ.money('h2');
  // In the shaft: F next to the vent does nothing; no beacon target.
  tapF(s,h2);assert.ok(h1.hp<=0);assert.equal(econ.money('h2'),money);
  until(s,landAt-VENT_PIT.drop-.05);assert.equal(h1.death!.id,fall.id,'still the shaft record');
  tick(s,4);const splat=h1.death!;assert.equal(splat.effect,'vent-splat');assert.equal(splat.kind,'vent-fall');assert.notEqual(splat.id,fall.id);
  assert.ok(Math.abs(splat.at-(landAt-VENT_PIT.drop))<1e-9&&Math.abs(splat.at+VENT_PIT.drop-(fall.at+10))<1e-9,'lands at exactly death + 10 s');
  assert.deepEqual({x:splat.pos.x,z:splat.pos.z},{x:0,z:0},'hall centre is clear');assert.equal(splat.source,fall.source);
  assert.ok(events(s,'pve-vent-splat').some(e=>e.target==='h1'));assert.equal(events(s,'death').filter(e=>e.target==='h1').length,1,'one death in the feed');
  // In the air above the spot: still not revivable.
  place(h2,.8,0);tapF(s,h2);if(s.time<landAt-1e-9)assert.ok(h1.hp<=0);
  until(s,landAt+.05);assert.equal(inVentShaft(h1.death,s.time),false);assert.ok(h1.hp<=0);
  tapF(s,h2);assert.ok(h1.hp>0,'instant F revive works on the landed body');assert.equal(econ.money('h2'),money-price);
  assert.ok(Math.hypot(h1.body.translation().x,h1.body.translation().z)<.3,'stands up where it landed');
  assert.ok(events(s,PVE_EVENT.revive).some(e=>e.target==='h1'&&e.source==='h2'));assert.equal(econ.partyView('h1').upAt!==undefined,true);
 }finally{s.dispose();}
 // The landing spot avoids the boss, pillars, props, machines and vents and other bodies; deterministic.
 const a=splatSpot({boss:{x:0,z:0,r:1}}),b=splatSpot({boss:{x:0,z:0,r:1}});assert.deepEqual(a,b);
 assert.ok(Math.hypot(a.x,a.z)>=1+VENT_PIT.land.boss-1e-9&&Math.hypot(a.x,a.z)<5,'nearest clear point off the boss');
 assert.equal(blocked(a.x,a.z),false);assert.equal(ventAt(a,1.9),-1);
 const c=splatSpot({bodies:[{x:0,z:0}]});assert.ok(Math.hypot(c.x,c.z)>=VENT_PIT.land.body);
 for(const boss of [{x:1,z:1,r:1.5},{x:-2,z:0,r:1},{x:0,z:3,r:1}]){const q=splatSpot({boss});assert.equal(blocked(q.x,q.z),false);assert.ok(Math.hypot(q.x-boss.x,q.z-boss.z)>=boss.r+VENT_PIT.land.boss-1e-9);}
});

test('the splat avoids a boss standing in the middle (live sim)',()=>{
 const w=world(['h1','h2']);const {s}=w;try{
  const boss=spawnBoss(s,0,0);openVent(w);const h1=P(s,'h1');place(h1,V0.x,V0.z);tick(s);assert.ok(h1.hp<=0);
  until(s,h1.death!.landAt!-VENT_PIT.drop+.02,()=>place(boss,0,0));
  const d=h1.death!;assert.equal(d.effect,'vent-splat');assert.ok(Math.hypot(d.pos.x,d.pos.z)>=2.4,'nudged off the boss');assert.equal(blocked(d.pos.x,d.pos.z),false);
 }finally{s.dispose();}
});

test('adds and the boss never fall into an open vent; with the real horde, a climbing add opens the pit and a player still falls',()=>{
 const w=world(['h1']);const {s}=w;try{
  const boss=spawnBoss(s,0,0);openVent(w);const add=w.horde.addAt({x:V0.x,z:V0.z});const hp=add.hp;
  for(let i=0;i<60;i++){place(boss,SIREN_VENTS[0].x,SIREN_VENTS[0].z);tick(s);}
  assert.ok(boss.hp>0&&!boss.death,'boss stays up');assert.ok(s.chaos.minions.has(add.id)&&add.hp===hp,'add unharmed');
  assert.equal(events(s,'pve-vent-fall').length,0);
 }finally{s.dispose();}
 // Real Horde (the EntryScheduler the client animates from): an add spawned through vent 2 opens it.
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 const s2=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7}}));try{
  s2.addPlayer('h1','h1','ranged');s2.pve!.command('h1',{action:'hello',v:1},false);s2.phase='fight';s2.fightStart=s2.time;
  const h1=P(s2,'h1'),V=SIREN_VENTS[2];h1.stats.hp=h1.hp=1e6;place(h1,V.x+6,V.z+6);tick(s2);(s2.pve!.horde as Horde).spawn('shambler',{entry:2});
  const view=()=>s2.pve!.horde.views(s2.pve!.slots()).entries.find(e=>e.e===2)!;
  for(let i=0;i<200&&h1.hp>0;i++){place(h1,V.x,V.z);tick(s2);}
  assert.ok(h1.hp<=0&&h1.death?.kind==='vent-fall');assert.ok(entryOpenAmount('vent',view(),h1.death!.at)>=VENT_PIT.lethal);
 }finally{s2.dispose();}
});

test('respawn interplay: a wave respawn during the shaft time revives normally and skips the splat; the solo self-revive waits for the landing',()=>{
 const w=world();const {s,econ}=w;try{
  const h1=P(s,'h1');openVent(w);place(h1,V0.x,V0.z);tick(s);const d=h1.death!;w.horde.entries=[];
  until(s,d.at+3);econ.respawnAll('wave');assert.ok(h1.hp>0,'respawned on a pad');const pad=h1.body.translation();
  assert.equal(ventAt(pad,1.5),-1,'not back over the vent');
  until(s,d.landAt!+1);assert.equal(events(s,'pve-vent-splat').length,0,'no splat for a living player');assert.ok(h1.hp>0&&!h1.death);
 }finally{s.dispose();}
 const w2=world(['h1']);try{
  const s=w2.s,h1=P(s,'h1');assert.equal(s.pve!.solo,true);openVent(w2);place(h1,V0.x,V0.z);tick(s);const d=h1.death!;w2.horde.entries=[];
  const self=w2.econ.partyView('h1').selfReviveAt!;assert.ok(self<d.landAt!,'the timer would fire in the shaft');
  until(s,self+1);assert.ok(h1.hp<=0,'no self-revive in the shaft');
  until(s,d.landAt!+.1);assert.ok(h1.hp>0,'self-revives once landed');
  assert.ok(Math.hypot(h1.body.translation().x,h1.body.translation().z)<.3,'at the landing spot');
  assert.ok(events(s,PVE_EVENT.revive).some(e=>e.target==='h1'&&e.message==='self'));
 }finally{w2.s.dispose();}
});

test('party bots and recruits steer around an open vent; inputs away from vents are untouched',()=>{
 const w=world(['h1']);const {s}=w;try{
  const h1=P(s,'h1'),vents=s.pve!.vents;place(h1,V0.x+2.6,V0.z);tick(s);openVent(w);
  const toward={...h1.input,x:-1,z:0} as Input,out=vents.steer(h1,toward);
  assert.ok(out.x>-.05,'no longer heading into the pit');assert.ok(Math.hypot(out.x,out.z)>.5,'still moving (around it)');
  const away={...h1.input,x:1,z:0} as Input;assert.equal(vents.steer(h1,away),away);
  w.horde.entries=[];s.step();assert.equal(vents.steer(h1,toward),toward,'closed vent: walk over it');
 }finally{s.dispose();}
 void PRICES;
});
