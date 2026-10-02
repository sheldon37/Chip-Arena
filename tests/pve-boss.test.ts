// PvE director, Siren boss (design §6, §11 S2): spawn, floors and hard caps, holds, Last Laugh, possession, grow,
// no-cooldown and the fall rescue. Real Director, real Siren and real damage(); fake horde, economy and recruits.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {CONFIG as C,v} from '../src/shared/config.js';
import {BOSS_PLAYER_ID,RECRUIT_PREFIX,type PveOptions} from '../src/shared/pve/contract.js';
import {Director} from '../src/shared/pve/director.js';
import {PVE,PVE_SIREN,bossHp,damageScale} from '../src/shared/pve/balance.js';
import {SIREN_INTRO} from '../src/shared/siren-contract.js';
import {fakeParts,withPveParts,type FakeEconomy,type FakeHorde,type FakeRecruits} from './helpers/pve-fakes.js';
await initPhysics();

const KEY='0123456789abcdef0123456789abcdef';
type Rig={s:Simulation;d:Director;horde:FakeHorde;economy:FakeEconomy;recruits:FakeRecruits};
/** A PvE sim in the fight phase with a real Director (fake other parts) and `humans` humans with a hello. */
function rig(o:Partial<PveOptions>={},humans=1,before?:(s:Simulation)=>void):Rig{
 const {factory,made}=fakeParts({director:h=>new Director(h)});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:11,skipWaves:true,...o}}));
 for(let i=1;i<=humans;i++){s.addPlayer('h'+i,'P'+i,'ranged');assert.equal(s.pve!.command('h'+i,{action:'hello',v:1,key:KEY},false),true);}
 before?.(s);
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();
 return {s,d:s.pve!.director as Director,horde:made.horde!,economy:made.economy!,recruits:made.recruits!};
}
const view=(s:Simulation)=>s.pve!.view();
function stepUntil(s:Simulation,done:()=>boolean,max=4000){for(let i=0;i<max;i++){if(done())return i;s.step();}assert.fail('condition not reached');}
/** Steps to the boss stage (no hold) with god mode on, so the AI Siren cannot end the run. */
function toBoss(r:Rig){r.s.pve!.debug('god:on');r.d.debug('skip-to-boss');stepUntil(r.s,()=>view(r.s).stage==='boss'&&!view(r.s).hold);return r.s.players.get(BOSS_PLAYER_ID)!;}
const place=(p:Player,x:number,z:number,yaw=0)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw,pitch:0};};
const hit=(s:Simulation,p:Player,amount:number)=>s.damage(p,amount,'h1','direct');

test('boss spawn: authorized Siren, team, HP, intro, boss song and spawn clearance',()=>{
 const r=rig();const {s}=r;try{
  s.pve!.debug('god:on');const h=s.players.get('h1')!;
  r.d.debug('skip-to-boss');const v1=view(s);assert.equal(v1.cinematic?.kind,'reveal');assert.equal(v1.cinematic?.chip,1);assert.equal(v1.hold,true);
  assert.ok(Math.abs(v1.cinematic!.until-v1.cinematic!.at-5)<1e-9,'reveal 5 s');
  // Stand on the spawn: the clearance push moves the human out to 4 m.
  place(h,PVE_SIREN.spawn.x+.5,PVE_SIREN.spawn.z-.5);
  stepUntil(s,()=>s.players.has(BOSS_PLAYER_ID));
  const boss=s.players.get(BOSS_PLAYER_ID)!;
  assert.equal(s.siren.active(boss),true);assert.equal(boss.team,'purple');assert.equal(boss.stats.hp,bossHp(1));assert.equal(boss.hp,bossHp(1));
  assert.equal(boss.bot,'normal');
  assert.equal(s.intro?.boss,true);assert.deepEqual(s.intro?.players,[BOSS_PLAYER_ID]);
  const hp=h.body.translation();assert.ok(Math.hypot(hp.x-PVE_SIREN.spawn.x,hp.z-PVE_SIREN.spawn.z)>=PVE_SIREN.spawnClear-1e-6,'pushed out');
  const bp=boss.body.translation();assert.ok(Math.abs(bp.x-PVE_SIREN.spawn.x)<.05&&Math.abs(bp.z-PVE_SIREN.spawn.z)<.05);
  assert.equal(view(s).cinematic?.kind,'boss-intro');
  s.step();assert.equal(s.bossSong?.startedAt,s.intro!.started);assert.equal(s.snapshot().intro?.boss,true);
  const until=view(s).cinematic!.until;assert.ok(Math.abs(until-view(s).cinematic!.at-(SIREN_INTRO.duration+PVE.bossIntroExtra))<1e-9);
  stepUntil(s,()=>!view(s).hold);
  assert.equal(s.intro,undefined);const bv=view(s).bossView!;assert.equal(view(s).stage,'boss');assert.equal(bv.phase,1);assert.equal(bv.floor,.75*bossHp(1));
  assert.ok(Math.abs(s.time-until)<=1/60+1e-9);
  assert.ok(r.horde.flows.at(-1)&&r.horde.flows.at(-1)!.budget===-1,'boss trickle flow');
 }finally{s.dispose();}
});

test('boss spawn: 4 humans (one disconnected) and 3 recruits; forced null seat dismisses the newest recruit with a refund',()=>{
 const addRecruits=(s:Simulation)=>{for(let k=1;k<=3;k++)assert.ok(s.addPlayer(RECRUIT_PREFIX+k,'R'+k,'ranged','normal'));};
 const r=rig({},4,addRecruits);const {s}=r;try{
  s.players.get('h4')!.connected=false;assert.equal(s.players.size,7);
  s.pve!.debug('god:on');r.d.debug('skip-to-boss');stepUntil(s,()=>s.players.has(BOSS_PLAYER_ID));
  assert.equal(s.players.size,8);assert.equal(s.players.get(BOSS_PLAYER_ID)!.stats.hp,bossHp(3),'h counts connected humans');
 }finally{s.dispose();}
 const q=rig({},2,addRecruits);try{
  const s=q.s;q.recruits.recruitViews=[1,2,3].map(k=>({id:RECRUIT_PREFIX+k,owner:k===3?'h2':'h1',role:'dps' as const,at:0,m:4}));
  const real=s.addPlayer.bind(s);let calls=0;
  (s as {addPlayer:typeof s.addPlayer}).addPlayer=(...a:Parameters<typeof s.addPlayer>)=>{if(a[0]===BOSS_PLAYER_ID&&calls++===0)return null;return real(...a);};
  s.pve!.debug('god:on');q.d.debug('skip-to-boss');stepUntil(s,()=>s.players.has(BOSS_PLAYER_ID)||view(s).stage==='ended');
  assert.ok(s.players.has(BOSS_PLAYER_ID));assert.equal(s.players.has(RECRUIT_PREFIX+3),false);assert.deepEqual(q.recruits.dismissed,[RECRUIT_PREFIX+3]);
  assert.deepEqual(q.economy.grants.filter(g=>g.reason==='refund'),[{id:'h2',amount:250,reason:'refund'}]);
 }finally{q.s.dispose();}
 // Both attempts fail: the run ends as a defeat and never soft-locks.
 const z=rig();try{
  (z.s as {addPlayer:typeof z.s.addPlayer}).addPlayer=()=>null;z.s.pve!.debug('god:on');z.d.debug('skip-to-boss');
  stepUntil(z.s,()=>z.s.phase==='match',2000);assert.equal(z.s.pve!.outcome,'defeat');assert.equal(z.s.result,'Boss could not spawn');
 }finally{z.s.dispose();}
});

test('hard caps: 10,000-damage hits never take the boss below 75% (or 25%) before the transition ends; floors move after the hold',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r),max=boss.stats.hp;
  hit(s,boss,10000);assert.ok(boss.hp>=.75*max-1e-6&&boss.hp<=.75*max+1e-6,`hp ${boss.hp}`);
  s.step();assert.equal(view(s).hold,true);assert.equal(view(s).cinematic?.chip,2);
  for(let i=0;i<60;i++){hit(s,boss,10000);s.step();assert.ok(boss.hp>=.75*max-1e-6);}
  assert.equal(view(s).bossView!.floor,.75*max,'floor unchanged during the hold');
  stepUntil(s,()=>!view(s).hold);assert.equal(view(s).bossView!.floor,.5*max);assert.equal(view(s).bossView!.hidden,true);
  assert.equal(hit(s,boss,10000),0,'hidden boss is untouchable');
  // Phase 3 (debug jump) and the 25% cap.
  assert.equal(r.d.debug('boss-phase:3'),true);stepUntil(s,()=>!view(s).hold);
  assert.equal(view(s).bossView!.phase,3);assert.equal(view(s).bossView!.floor,.25*max);
  hit(s,boss,10000);assert.ok(Math.abs(boss.hp-.25*max)<1e-6);s.step();assert.equal(view(s).cinematic?.chip,4);
  for(let i=0;i<30;i++){hit(s,boss,10000);s.step();assert.ok(boss.hp>=.25*max-1e-6);}
  stepUntil(s,()=>!view(s).hold);assert.equal(view(s).bossView!.phase,4);assert.equal(view(s).bossView!.floor,0);
  assert.deepEqual(r.economy.respawns,['phase','phase','phase'],'respawnAll(phase) at the end of the chip 2, 3 and 4 reveals');
  assert.equal(r.economy.grants.filter(g=>g.reason==='boss-threshold').length,3);
 }finally{s.dispose();}
});

test('holds shift the boss ability clock: cooldowns keep their remaining time and nothing becomes ready on release',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r),st=s.siren.state(boss);boss.bot=null;
  st.ready={rend:s.time+3,slam:s.time+.5,rush:s.time-1};boss.cooldown=.4;
  hit(s,boss,10000);s.step();assert.equal(view(s).hold,true);
  const t0=s.time,rem={rend:st.ready.rend!-t0,slam:st.ready.slam!-t0,rush:st.ready.rush!-t0},cd=boss.cooldown;
  stepUntil(s,()=>!view(s).hold);
  assert.ok(Math.abs(st.ready.rend!-s.time-rem.rend)<1e-9);assert.ok(Math.abs(st.ready.slam!-s.time-rem.slam)<1e-9);
  assert.ok(st.ready.slam!>s.time,'slam (not ready at hold start) is not ready on release');assert.ok(st.ready.rush!<=s.time);
  assert.equal(boss.cooldown,cd,'move lock frozen');
  // Holds also notify the parts and null the flow.
  assert.deepEqual(r.horde.holds.slice(-2),[true,false]);assert.ok(r.horde.flows.includes(null));
 }finally{s.dispose();}
});

test('Last Laugh: a grab is released at hold start; in phase 4 a solo human survives one crush; laugh re-casts after the move (D35)',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r),h=s.players.get('h1')!;boss.bot=null;s.pve!.debug('god:off');
  // A grab in progress when the 75% threshold is reached.
  s.siren.grabs.push({owner:boss.id,victim:h.id,at:s.time,crushAt:s.time+5,releaseAt:s.time+6});
  hit(s,boss,10000);s.step();assert.equal(view(s).hold,true);assert.equal(s.siren.grabs.length,0,'no grab survives a hold');
  assert.equal(r.d.debug('boss-phase:4'),false,'no debug jump during a hold');
  stepUntil(s,()=>!view(s).hold);assert.equal(r.d.debug('boss-phase:4'),true);stepUntil(s,()=>!view(s).hold);
  assert.equal(view(s).bossView!.phase,4);assert.deepEqual(boss.siren!.boost?.minRecast,{laugh:0});
  // Real laugh: boss facing the human 1 m away; step the Siren directly.
  h.hp=h.stats.hp;place(boss,0,0,0);place(h,0,1);s.world.step();s.siren.cancel(boss);boss.cooldown=0;boss.siren!.mode='melee';boss.siren!.ready={};
  const at=s.time;assert.equal(s.siren.start(boss,'laugh'),true);assert.equal(boss.siren!.ready!.laugh,at,'no laugh floor in phase 4');
  assert.equal(s.siren.start(boss,'laugh'),false,'the move itself gates a re-cast');
  let crushed=false;for(let i=0;i<600&&!crushed;i++){s.time+=1/60;s.siren.stepPlayer(boss,1/60);s.siren.step(1/60);crushed=h.hp<h.stats.hp;}
  assert.ok(crushed,'crush landed');assert.ok(h.hp>0,`survives one crush (hp ${h.hp})`);
  assert.ok(Math.abs(h.hp-(h.stats.hp-PVE_SIREN.laughCrush*h.stats.hp*damageScale(1)*h.stats.damageTaken))<1e-6,`crush = 60% × max HP × damageScale (hp ${h.hp})`);
  boss.cooldown=0;s.siren.cancel(boss);assert.equal(s.siren.start(boss,'laugh'),true,'ready again once the move ended');
 }finally{s.dispose();}
});

test('possession: hidden untouchable boss, possessed add, ×1.8/×1.6 flow; the kill sets 50% and starts chip 3 on the same tick',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r),max=boss.stats.hp,h=s.players.get('h1')!;
  const add=r.horde.addAt({x:6,z:-4});const n=Number(add.id.slice(4));
  hit(s,boss,10000);stepUntil(s,()=>view(s).bossView?.hidden===true&&!view(s).hold);
  assert.equal(s.pve!.hidden(boss),true);assert.equal(boss.body.isEnabled(),false);assert.equal(s.pve!.untouchable(boss),true);
  assert.equal(r.horde.possessed(),n);assert.equal(view(s).bossView!.possessed,n);assert.equal(typeof view(s).bossView!.possessedAt,'number');
  assert.ok(s.events.some(e=>e.type==='pve-possess'&&e.target==='add-'+n));
  const flow=r.horde.flows.at(-1)!;assert.ok(Math.abs(flow.rate-PVE_SIREN.trickle.rate*1.8)<1e-9);assert.equal(flow.maxAlive,Math.round(6*1.6));
  // Not a botInput target, no hazard damage, no siren readiness.
  const target=s.botInput(h);void target;assert.equal(s.siren.ready(boss),false);assert.equal(boss.connected,false);
  const enemies=[...s.players.values()].filter(x=>s.enemy(h.id,x.id)&&x.connected&&x.hp>0);assert.equal(enemies.length,0,'botInput has no target');
  boss.ext=v(3,0,1);s.step();
  r.horde.kill(n,'h1');
  assert.ok(Math.abs(boss.hp-.5*max)<1e-9);assert.equal(view(s).hold,true);assert.equal(view(s).cinematic?.chip,3);
  assert.equal(s.pve!.hidden(boss),false);assert.equal(boss.connected,true);assert.equal(typeof view(s).bossView!.foundAt,'number');
  const bp=boss.body.translation();assert.ok(Math.abs(bp.x-6)<1e-6&&Math.abs(bp.z+4)<1e-6);assert.deepEqual({...boss.ext},{x:0,y:0,z:0});
  assert.ok(bp.y>=C.playerHalfHeight+C.playerRadius-.01,'standing, not in the floor');
  stepUntil(s,()=>!view(s).hold);assert.equal(view(s).bossView!.phase,3);
  const after=r.horde.flows.at(-1)!;assert.ok(Math.abs(after.rate-PVE_SIREN.trickle.rate)<1e-9,'normal trickle after found');
 }finally{s.dispose();}
});

test('grow: boost, collider .63/.645, grounded after 30 ticks, slam radius 5.4',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r);r.d.debug('boss-phase:3');stepUntil(s,()=>!view(s).hold);
  const b=boss.siren!.boost!;assert.equal(b.scale,1.5);assert.equal(b.reach,1.5);assert.equal(b.cooldown,1);
  assert.ok(Math.abs(boss.collider.radius()-.63)<1e-6);assert.ok(Math.abs(boss.collider.halfHeight()-.645)<1e-6);
  boss.bot=null;for(let i=0;i<30;i++)s.step();assert.equal(boss.grounded,true);
  s.siren.cancel(boss);boss.cooldown=0;boss.siren!.mode='melee';boss.siren!.ready={};assert.equal(s.siren.start(boss,'slam'),true);
  for(let i=0;i<120&&!s.events.some(e=>e.type==='siren-slam');i++){s.time+=1/60;s.siren.stepPlayer(boss,1/60);}
  const slam=s.events.find(e=>e.type==='siren-slam');assert.ok(slam);assert.ok(Math.abs(slam!.radius!-5.4)<1e-9);
 }finally{s.dispose();}
});

test('no cooldown: ready.rend===at, re-cast only after the move; 20 s of bot play keeps ≤2 rains and ≤4 eruptions',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r),h=s.players.get('h1')!;r.d.debug('boss-phase:4');stepUntil(s,()=>!view(s).hold);
  s.siren.cancel(boss);boss.cooldown=0;boss.siren!.mode='melee';const at=s.time;
  boss.bot=null;assert.equal(s.siren.start(boss,'rend'),true);assert.equal(boss.siren!.ready!.rend,at);
  assert.equal(s.siren.start(boss,'rend'),false);const lock=boss.cooldown;assert.ok(lock>0);
  boss.bot='normal';place(h,0,8);place(boss,0,0,Math.PI);
  let rains=0,erupts=0,casts=0;const seen=new Set<number>();
  for(let i=0;i<1200;i++){s.step();rains=Math.max(rains,s.siren.rains.filter(x=>x.owner===boss.id).length);erupts=Math.max(erupts,s.siren.eruptions.filter(x=>x.owner===boss.id).length);
   const m=boss.siren?.move;if(m&&!seen.has(m.seq)){seen.add(m.seq);casts++;}}
  assert.ok(rains<=2,`rains ${rains}`);assert.ok(erupts<=4,`eruptions ${erupts}`);assert.ok(casts>5,`the bot keeps casting (${casts})`);
  assert.equal(s.pve!.outcome,undefined);
 }finally{s.dispose();}
});

test('boss fall: below y −6 the boss returns to its spawn on the next tick with unchanged HP (phase 1 and phase 4)',()=>{
 const r=rig();const {s}=r;try{
  const boss=toBoss(r);boss.bot=null;
  const check=()=>{const hp=boss.hp;boss.body.setTranslation(v(3,-7,3),true);s.step();const p=boss.body.translation();
   assert.ok(Math.hypot(p.x-PVE_SIREN.spawn.x,p.z-PVE_SIREN.spawn.z)<.2,`at spawn ${p.x},${p.z}`);assert.equal(boss.hp,hp);};
  check();r.d.debug('boss-phase:4');stepUntil(s,()=>!view(s).hold);check();
 }finally{s.dispose();}
});
