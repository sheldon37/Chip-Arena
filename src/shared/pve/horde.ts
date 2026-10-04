// PvE horde (design §3, stream S1): Siren adds as shared-authority minions. Spawning through door, cell and vent entries,
// flow-field steering, swipe/lunge/smash attacks through sim.damage, limb pools and gibs, corpses, vent travel,
// possession, holds as time shifts, assists for player-owned chaos minions (D38) and the compact add view.
// Deterministic: draws only from host.rng('horde'); every timer is an absolute sim time; iteration in Map order.
import RAPIER from '@dimforge/rapier3d-compat';
import {v,type Vec} from '../config.js';
import type {Minion} from '../chaos.js';
import type {Player} from '../simulation.js';
import {arenaBlocks} from '../arena.js';
import {SIREN_VENTS} from '../siren-stage-layout.js';
import {ADD_KINDS_ORDER,ADD_PREFIX,HORDE_ID,PVE_EVENT,PVE_GROUPS,PVE_TIMING,encodeAdd,
 type AddDeath,type AddHitInfo,type AddKind,type AddRef,type AddState,type HordeApi,type HordeFlow,type PveAddTuple,
 type PveAddView,type PveEntryView,type PveHost} from './contract.js';
import {ADD_KINDS,ECONOMY,HORDE,IDENTITY_MODS,PVE_SIREN} from './balance.js';
import {machineBlocks,PVE_ENTRIES} from './layout.js';
import {HordeNav} from './nav.js';
import {EntryScheduler,SpawnBudget,chooseEntry,entryWeight,ventHazard,type EntryChoiceContext} from './entries.js';
import {PART_LIMB,SEVER_BITS,classifyAddHit,jointWorld,limbState,partLost,partMultiplier,severAt,type HitPart} from './gibs.js';
import {SeparationHash,addSpeed,attackProfile,nextSide,possessedMode,targetScore,type PveAdd} from './add-ai.js';

/** Standing capsule centre height of an upright add (capsule .43/.42 on the y=0 floor). */
export const ADD_STAND_Y=HORDE.collider.halfHeight+HORDE.collider.radius+.05;
const CRAWL_DROP=(HORDE.collider.halfHeight+HORDE.collider.radius)-(HORDE.collider.crawlHalfHeight+HORDE.collider.crawlRadius);
/** Horde events per tick (with 3 ticks per 20 Hz snapshot: at most 24 new horde events per snapshot). */
export const HORDE_EVENTS_PER_TICK=8;
const HIT_EVENT_COLOR=0x0b0a0d;
interface Corpse{n:number;view:PveAddView;until:number}
interface Outgoing{type:string;pos:Vec;extra:object;keep:boolean;at:number}
const flat=(a:{x:number;z:number},b:{x:number;z:number})=>Math.hypot(a.x-b.x,a.z-b.z);

export class Horde implements HordeApi{
 readonly nav:HordeNav;
 private list=new Map<number,PveAdd>();private byId=new Map<string,PveAdd>();
 private corpses:Corpse[]=[];private serial=0;private flow:HordeFlow|null=null;private budget=new SpawnBudget();private flowReleased=0;
 private sched=new EntryScheduler();private outbox:Outgoing[]=[];private buckets=new Map<string,{tokens:number;at:number}>();
 private assists=new Map<string,{target?:string;at:number}>();private hash=new SeparationHash();private stepCtx?:EntryChoiceContext;
 /** Counters for tests and the perf script. */
 stats={spawned:0,activated:0,killed:0,dives:0,eventsDropped:0};
 constructor(readonly host:PveHost){this.nav=new HordeNav(host.sim);}
 private get sim(){return this.host.sim;}
 private get time(){return this.host.sim.time;}
 private rng(){return this.host.rng('horde');}
 // ---------------------------------------------------------------- queries
 private count(pred:(a:PveAdd)=>boolean){let n=0;for(const a of this.list.values())if(pred(a))n++;return n;}
 /** Adds with a body, or travelling through a vent. */
 alive(){return this.count(a=>a.phase==='active'||a.phase==='vent'||a.phase==='entering'&&a.travel);}
 /** Adds waiting for an entry or entering for the first time. */
 queued(){return this.count(a=>a.phase==='pending'||a.phase==='entering'&&!a.travel);}
 private entering(){return this.count(a=>a.phase==='entering'||a.phase==='pending');}
 /** The current alive cap: HORDE.maxAlive (maxAliveRelay on relay rooms), lowered by the flow's maxAlive. */
 cap(){const hard=this.host.options.relay?HORDE.maxAliveRelay:HORDE.maxAlive;return this.flow?Math.min(hard,this.flow.maxAlive):hard;}
 private posOf(a:PveAdd):Vec{return a.phase==='active'&&a.minion?a.minion.body.translation():a.pos;}
 private stateOf(a:PveAdd):AddState{
  if(a.phase==='pending'||a.phase==='entering')return 'entering';if(a.phase==='vent')return 'vent';
  return a.crawl?'crawl':this.time<a.staggerUntil?'stagger':'active';
 }
 private ref(a:PveAdd):AddRef{return {n:a.n,id:a.id,pos:{...this.posOf(a)},kind:a.kind,state:this.stateOf(a),hp:a.hp,maxHp:a.maxHp,target:a.target,attackAt:a.attackAt||undefined};}
 adds(){return [...this.list.values()].map(a=>this.ref(a));}
 get(n:number){const a=this.list.get(n);return a?this.ref(a):undefined;}
 /** The internal record (tests and tools). */
 record(n:number){return this.list.get(n);}
 corpseCount(){return this.corpses.length;}
 possessed(){for(const a of this.list.values())if(a.possessed)return a.n;return undefined;}
 // ---------------------------------------------------------------- spawning
 setFlow(flow:HordeFlow|null){this.flow=flow?{...flow,mix:{...flow.mix}}:null;this.budget.reset();this.flowReleased=0;}
 private context():EntryChoiceContext{
  if(this.stepCtx&&this.stepCtx.time===this.time)return this.stepCtx;
  const party=this.host.party().filter(p=>p.hp>0).map(p=>{const t=p.body.translation();return {x:t.x,z:t.z};});
  this.stepCtx={time:this.time,party,songStart:this.sim.bossSong?.startedAt,wave:this.host.director.view().stage==='wave',possession:this.possessed()!==undefined};
  return this.stepCtx;
 }
 spawn(kind:AddKind,opts:{entry?:number;possessed?:boolean}={}):number|null{
  if(!ADD_KINDS_ORDER.includes(kind))return null;
  if(this.alive()+this.queued()>=(this.host.options.relay?HORDE.maxAliveRelay:HORDE.maxAlive))return null;
  if(opts.entry!==undefined&&!(Number.isInteger(opts.entry)&&opts.entry>=0&&opts.entry<PVE_ENTRIES.length))return null;
  const a=this.create(kind,opts.possessed===true);if(opts.entry!==undefined)a.requestEntry=opts.entry;
  this.place(a);return a.n;
 }
 private create(kind:AddKind,possessed:boolean):PveAdd{
  const n=++this.serial,stats=ADD_KINDS[kind],mods=this.host.director.modifiers(),t=this.time;
  const maxHp=stats.hp*mods.hp*(possessed?PVE_SIREN.possession.hpScale:1);
  const a:PveAdd={n,id:ADD_PREFIX+n,kind,stats,mods:{...IDENTITY_MODS,...mods},phase:'pending',hp:maxHp,maxHp,parts:{},lost:0,crawl:false,limp:false,headless:false,
   attack:stats.attack,side:1,attackAt:0,strikeAt:Infinity,struck:true,recoverUntil:t,staggerUntil:t,atkDamage:0,atkReach:0,atkCone:0,
   thinkAt:t,ext:{x:0,z:0},entry:-1,spawnAt:t,walkAt:t,walkFor:0,activeAt:Infinity,diveAt:0,ventUntil:0,ventCooldown:t,travel:false,
   hitAt:0,hitYaw:0,dealt:new Map(),lastDamageAt:-Infinity,possessed,possessedAt:possessed?t:0,pos:v(0,ADD_STAND_Y,0),yaw:0,
   progressAt:t,progressDist:Infinity,nudgeUntil:0,fieldSum:0,fieldAt:-Infinity};
  this.list.set(n,a);this.byId.set(a.id,a);this.stats.spawned++;return a;
 }
 /** Assigns a pending add to an entry (seeded choice unless one was requested); stays pending when none is eligible. */
 private place(a:PveAdd){
  const ctx=this.context();let e=-1;
  if(a.requestEntry!==undefined){if(entryWeight(PVE_ENTRIES[a.requestEntry],this.sched,ctx)>0)e=a.requestEntry;}
  else e=chooseEntry(this.sched,ctx,this.rng());
  if(e<0)return false;
  this.enter(a,e);return true;
 }
 private enter(a:PveAdd,e:number){
  const en=PVE_ENTRIES[e],s=this.sched.schedule(e,a.n,this.time);
  a.phase='entering';a.entry=e;a.spawnAt=s.spawnAt;a.walkAt=s.walkAt;a.activeAt=s.activeAt;a.walkFor=en.kind==='vent'?0:s.activeAt-s.walkAt;
  a.pos=v(en.from.x,ADD_STAND_Y,en.from.z);a.yaw=en.yaw;
 }
 private activate(a:PveAdd){
  const s=this.sim,en=PVE_ENTRIES[a.entry],y=ADD_STAND_Y,c=HORDE.collider;
  if(a.minion){const m=a.minion;m.body.setTranslation(v(en.exit.x,y,en.exit.z),true);m.body.setLinvel(v(),true);m.body.setEnabled(true);m.collider.setEnabled(true);}
  else{
   const body=s.world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().setTranslation(en.exit.x,y,en.exit.z).lockRotations());
   const collider=s.world.createCollider(RAPIER.ColliderDesc.capsule(c.halfHeight,c.radius).setMass(c.mass).setFriction(0).setFrictionCombineRule(RAPIER.CoefficientCombineRule.Min).setCollisionGroups(PVE_GROUPS.add),body);
   const m:Minion={id:a.id,owner:HORDE_ID,kind:'siren-add',pos:v(en.exit.x,y,en.exit.z),yaw:en.yaw,hp:a.hp,maxHp:a.maxHp,speed:a.stats.walk,until:1e9,lastAttack:-10,nextAttack:1e9,body,collider};
   a.minion=m;s.chaos.minions.set(a.id,m);s.colliderOwners.set(collider.handle,a.id);
  }
  this.sched.leave(a.entry,a.n);
  a.phase='active';a.travel=false;a.yaw=en.yaw;a.entry=-1;a.thinkAt=this.time+(a.n%12)*.0625;a.recoverUntil=this.time;a.progressAt=this.time;a.progressDist=Infinity;
  this.stats.activated++;
 }
 // ---------------------------------------------------------------- per tick
 step(dt:number){
  const t=this.time;this.stepCtx=undefined;this.nav.poll(t);
  // 1. flow releases (rate-independent budget)
  if(this.flow){
   const left=this.flow.budget<0?-1:Math.max(0,this.flow.budget-this.flowReleased);
   const room=Math.max(0,Math.min(this.cap()-this.alive()-this.queued(),HORDE.maxEntering-this.entering()));
   const n=this.budget.step(dt,this.flow.rate,room,left);
   for(let i=0;i<n;i++){this.flowReleased++;this.create(this.pickKind(this.flow.mix),false);}
  }
  // 2. pending → entries (FIFO); 3. entering → walk and activation; 4. vent travellers re-emerge
  for(const a of [...this.list.values()]){
   if(a.phase==='pending')this.place(a);
   if(a.phase==='entering'){
    if(t>=a.activeAt)this.activate(a);
    else if(a.walkFor>0){const en=PVE_ENTRIES[a.entry],f=Math.max(0,Math.min(1,(t-a.walkAt)/a.walkFor));a.pos=v(en.from.x+(en.exit.x-en.from.x)*f,ADD_STAND_Y,en.from.z+(en.exit.z-en.from.z)*f);}
   }
   else if(a.phase==='vent'&&t>=a.ventUntil)this.reemerge(a);
  }
  // 5. active adds
  const players=this.candidates(),chasers=new Map<string,number>();
  this.hash.clear();
  for(const a of this.list.values())if(a.phase==='active'&&a.minion){this.hash.add(a,a.minion.body.translation());if(a.target)chasers.set(a.target,(chasers.get(a.target)??0)+1);}
  for(const a of [...this.list.values()])if(a.phase==='active'&&a.minion&&this.list.has(a.n))this.think(a,dt,players,chasers);
  // 6. corpses
  if(this.corpses.length)this.corpses=this.corpses.filter(c=>c.until>t);
  // 7. budgeted events
  this.flush();
 }
 private pickKind(mix:HordeFlow['mix']):AddKind{
  const keys=ADD_KINDS_ORDER.filter(k=>(mix[k]??0)>0);if(!keys.length)return 'shambler';
  let roll=this.rng().next()*keys.reduce((n,k)=>n+(mix[k]??0),0);for(const k of keys){roll-=mix[k]??0;if(roll<0)return k;}return keys[keys.length-1];
 }
 /** Living, connected party players the horde may target (humans, party bots, recruits; never the boss). */
 private candidates():Player[]{return this.host.party().filter(p=>this.valid(p));}
 private valid(p:Player|undefined):p is Player{return !!p&&p.connected&&p.hp>0&&!this.host.untouchable(p)&&!this.host.isBoss(p)&&this.sim.enemy(HORDE_ID,p.id);}
 private think(a:PveAdd,dt:number,players:Player[],chasers:Map<string,number>){
  const s=this.sim,t=this.time,m=a.minion!,p=m.body.translation();
  if(p.y<-6){this.kill(a,null,'fall');return;}
  let target=a.target?s.players.get(a.target):undefined;
  if(!this.valid(target)){if(a.target){chasers.set(a.target,Math.max(0,(chasers.get(a.target)??1)-1));a.target=undefined;}target=undefined;a.thinkAt=Math.min(a.thinkAt,t);}
  if(t>=a.thinkAt){
   a.thinkAt=t+HORDE.thinkInterval;
   const before=a.target;target=a.possessed?this.nearestHuman(p):this.chooseTarget(a,p,players,chasers);
   a.target=target?.id;
   if(before!==a.target){if(before)chasers.set(before,Math.max(0,(chasers.get(before)??1)-1));if(a.target)chasers.set(a.target,(chasers.get(a.target)??0)+1);}
   if(target&&this.ventTravel(a,p,target))return;
  }
  // Strike (exactly once, at the first tick at or after strikeAt).
  if(a.attackAt>0&&!a.struck&&t>=a.strikeAt){a.struck=true;this.strike(a);if(!this.list.has(a.n))return;}
  // Start an attack.
  const tp=target?.body.translation(),dist=tp?flat(p,tp):Infinity;
  if(target&&tp&&!a.possessed&&a.struck&&t>=a.recoverUntil&&t>=a.staggerUntil){
   const prof=attackProfile(a);
   if(dist<=prof.start&&Math.abs(tp.y-p.y)<1.6){
    a.attack=prof.attack;a.side=nextSide(a.side,a.lost);a.attackAt=t;a.strikeAt=t+prof.hit;a.struck=false;a.recoverUntil=t+Math.max(prof.cycle,prof.hit+.05);
    a.atkDamage=prof.damage;a.atkReach=prof.reach;a.atkCone=prof.cone;a.yaw=Math.atan2(tp.x-p.x,tp.z-p.z);
   }
  }
  // Velocity.
  let vx=0,vz=0;const decay=Math.exp(-HORDE.extDecay*dt);a.ext.x*=decay;a.ext.z*=decay;
  if(t<a.staggerUntil){/* staggered: knockback only */}
  else if(a.attackAt>0&&!a.struck){
   if(tp)a.yaw=Math.atan2(tp.x-p.x,tp.z-p.z);
   const prof=attackProfile(a),sp=prof.lunge/Math.max(.05,prof.hit);
   if(dist>.8){vx=Math.sin(a.yaw)*sp;vz=Math.cos(a.yaw)*sp;}
  }
  else if(t<a.recoverUntil&&a.attackAt>0){/* recovering */}
  else if(target&&tp){
   const mv=a.possessed?this.possessedMove(a,p,target,tp,dist):this.chaseMove(a,p,target,tp,dist);
   vx=mv.x;vz=mv.z;
  }
  if(vx||vz){const sep=this.hash.push(a,p,b=>this.posOf(b));vx+=sep.x;vz+=sep.z;}
  vx+=a.ext.x;vz+=a.ext.z;
  const vel=m.body.linvel();m.body.setLinvel(v(vx,vel.y,vz),true);
  m.pos={x:p.x,y:p.y,z:p.z};m.yaw=a.yaw;
 }
 private chooseTarget(a:PveAdd,p:Vec,players:Player[],chasers:Map<string,number>):Player|undefined{
  if(!players.length)return undefined;
  const taunt=this.host.recruits.tauntTarget(p);
  let best:Player|undefined,score=Infinity;
  for(const q of players){
   const own=a.target===q.id,c=(chasers.get(q.id)??0)-(own?1:0);
   const sc=targetScore(this.nav.distance(p,q.id,'party'),c,taunt===q.id,own);
   if(sc<score){score=sc;best=q;}
  }
  return best;
 }
 private nearestHuman(p:Vec):Player|undefined{
  let best:Player|undefined,d=Infinity;
  for(const q of this.host.humans()){if(!this.valid(q))continue;const e=flat(p,q.body.translation());if(e<d){d=e;best=q;}}
  return best;
 }
 /** Straight within 6 m with grid line of sight, else the flow field; straight when the field is not ready. */
 private steer(p:Vec,target:Player,tp:Vec,dist:number):{x:number;z:number}{
  let dir:{x:number;z:number}|null=null;
  if(!(dist<=6&&this.nav.clear(p,tp,'party')))dir=this.nav.direction(p,target.id,'party');
  if(!dir){const l=dist||1;dir={x:(tp.x-p.x)/l,z:(tp.z-p.z)/l};}
  return dir;
 }
 private chaseMove(a:PveAdd,p:Vec,target:Player,tp:Vec,dist:number){
  const t=this.time;
  if(dist<=HORDE.stopDistance){a.yaw=Math.atan2(tp.x-p.x,tp.z-p.z);a.progressAt=t;a.progressDist=dist;return {x:0,z:0};}
  let dir=this.steer(p,target,tp,dist);
  // Stuck nudge: less than 5 cm of progress in 0.5 s while chasing adds a lateral bias by n parity.
  if(t>=a.progressAt+.5){if(a.progressDist-dist<.05)a.nudgeUntil=t+.5;a.progressAt=t;a.progressDist=dist;}
  if(t<a.nudgeUntil){const s=a.n%2?1:-1,lx=dir.z*s,lz=-dir.x*s,l=Math.hypot(dir.x+lx*.8,dir.z+lz*.8)||1;dir={x:(dir.x+lx*.8)/l,z:(dir.z+lz*.8)/l};}
  const speed=addSpeed(a,dist);a.yaw=Math.atan2(dir.x,dir.z);
  return {x:dir.x*speed,z:dir.z*speed};
 }
 private possessedMove(a:PveAdd,p:Vec,target:Player,tp:Vec,dist:number){
  const mode=possessedMode(this.time-a.possessedAt,dist),face=Math.atan2(tp.x-p.x,tp.z-p.z);
  if(mode==='chase')return this.chaseMove(a,p,target,tp,dist);
  if(mode==='stare'||mode==='hold'){a.yaw=face;return {x:0,z:0};}
  const speed=addSpeed(a,Infinity);
  if(mode==='toward'){const d=this.steer(p,target,tp,dist);a.yaw=Math.atan2(d.x,d.z);return {x:d.x*speed,z:d.z*speed};}
  let d=this.nav.direction(p,target.id,'party');
  d=d?{x:-d.x,z:-d.z}:{x:(p.x-tp.x)/(dist||1),z:(p.z-tp.z)/(dist||1)};
  a.yaw=Math.atan2(d.x,d.z);return {x:d.x*speed,z:d.z*speed};
 }
 // ---------------------------------------------------------------- attacks
 private strike(a:PveAdd){
  const s=this.sim,m=a.minion;if(!m||a.hp<=0||this.time<a.staggerUntil)return;
  const p=m.body.translation(),fx=Math.sin(a.yaw),fz=Math.cos(a.yaw),eye=v(p.x,p.y+(a.crawl?.1:.45),p.z),impulse=attackProfile(a).impulse;
  const hits:Player[]=[];
  if(a.attack==='smash'&&!a.crawl){
   const cx=p.x+fx*1.2,cz=p.z+fz*1.2,r=a.stats.smashRadius??1.6;
   for(const q of this.candidates()){const qp=q.body.translation();if(Math.hypot(qp.x-cx,qp.z-cz)<=r&&Math.abs(qp.y-p.y)<1.8&&s.lineOfSight(eye,qp,a.id,q.id,true))hits.push(q);}
  }else{
   const q=a.target?s.players.get(a.target):undefined;if(!this.valid(q))return;
   const qp=q.body.translation(),dx=qp.x-p.x,dz=qp.z-p.z,d=Math.hypot(dx,dz);
   if(d>a.atkReach||Math.abs(qp.y-p.y)>1.8)return;
   if(d>1e-3&&(fx*dx+fz*dz)/d<a.atkCone)return;
   if(!s.lineOfSight(eye,qp,a.id,q.id,true))return;
   hits.push(q);
  }
  for(const q of hits){
   const qp=q.body.translation(),dx=qp.x-p.x,dz=qp.z-p.z,l=Math.hypot(dx,dz)||1,dir=v(dx/l,0,dz/l);
   if(q.blockTime>0){
    s.successfulBlock(q,'minion',v(qp.x-dir.x*.4,qp.y+.2,qp.z-dir.z*.4),HORDE_ID,a.atkDamage,dir,impulse);
    a.staggerUntil=this.time+.6;a.ext={x:-dir.x*3,z:-dir.z*3};
   }else{
    const point=v(qp.x-dir.x*.35,qp.y+(a.crawl?-.55:a.attack==='smash'?.35:.15),qp.z-dir.z*.35);
    s.damage(q,a.atkDamage,HORDE_ID,'minion',dir,impulse,point,true,false);
   }
  }
 }
 /** Cancels a windup that has not struck yet. */
 private cancelAttack(a:PveAdd){if(a.attackAt>0&&!a.struck){a.struck=true;a.attackAt=0;a.strikeAt=Infinity;a.recoverUntil=this.time;}}
 // ---------------------------------------------------------------- vent travel
 private ventTravel(a:PveAdd,p:Vec,target:Player):boolean{
  const t=this.time,vt=HORDE.ventTravel;
  if(!vt.enabled||a.crawl||a.attackAt>0&&!a.struck||t<a.ventCooldown||t-a.lastDamageAt<2)return false;
  if(a.possessed&&t-a.possessedAt<PVE_SIREN.possession.noVentFor)return false;
  const vent=SIREN_VENTS.findIndex(e=>flat(p,e)<=vt.nearVent);if(vent<0)return false;
  if(this.nav.distance(p,target.id,'party')<=vt.minPathDistance)return false;
  if(this.count(b=>b.travel)>=vt.maxConcurrent)return false;
  if(ventHazard(vent,t,this.sim.bossSong?.startedAt,PVE_TIMING.vent.dive))return false;
  if(this.rng().next()>=vt.chance)return false;
  // Dive: the body is disabled but stays registered (chaos.clear removes it); hits are ignored outside 'active'.
  const m=a.minion!;this.cancelAttack(a);
  a.phase='vent';a.travel=true;a.diveAt=t;a.ventUntil=t+PVE_TIMING.vent.dive+vt.hidden;a.ventCooldown=t+vt.cooldown;a.entry=vent;
  a.pos=v(SIREN_VENTS[vent].x,ADD_STAND_Y,SIREN_VENTS[vent].z);a.yaw=Math.atan2(-a.pos.x,-a.pos.z);a.ext={x:0,z:0};
  m.body.setLinvel(v(),true);m.body.setEnabled(false);m.collider.setEnabled(false);
  this.sched.dive(vent,t);this.stats.dives++;return true;
 }
 /** After dive+hidden: queue at the open-able vent closest (path) to the target, hazard filter applied; else wait. */
 private reemerge(a:PveAdd){
  const t=this.time,song=this.sim.bossSong?.startedAt,target=a.target?this.sim.players.get(a.target):undefined;
  let best=-1,score=Infinity;
  for(let e=0;e<SIREN_VENTS.length;e++){
   if(this.sched.queueLength(e)>=HORDE.entry.queue||ventHazard(e,t,song))continue;
   const at=v(SIREN_VENTS[e].x,ADD_STAND_Y,SIREN_VENTS[e].z),sc=target?this.nav.distance(at,target.id,'party'):e;
   if(sc<score){score=sc;best=e;}
  }
  if(best>=0)this.enter(a,best);
 }
 // ---------------------------------------------------------------- hits, gibs, death
 /** Returns the actual damage dealt. */
 hit(m:Minion,damage:number,attacker:string,impulse:number,dir:Vec,hit?:AddHitInfo):number{
  const a=this.byId.get(m.id),t=this.time;if(!a||a.phase!=='active'||a.hp<=0||!a.minion)return 0;
  if(!Number.isFinite(damage)||damage<=0)return 0;
  const pos=a.minion.body.translation(),body={pos,yaw:a.yaw,crawl:a.crawl,lost:a.lost,maxHp:a.maxHp};
  const shares=classifyAddHit(body,damage,hit,this.rng()),severed:Exclude<HitPart,'torso'>[]=[];let total=0;
  for(const share of shares){
   const part:HitPart=partLost(a.lost,share.part)?'torso':share.part,dmg=share.damage*partMultiplier(part),actual=Math.min(a.hp,dmg);
   a.hp-=actual;total+=actual;
   if(part!=='torso'){
    const limb=PART_LIMB[part],pool=(a.parts[limb]??0)+actual;a.parts[limb]=pool;
    if(pool>=severAt(part,a.maxHp,a.stats.limbFrac,a.stats.headFrac)&&!partLost(a.lost,part)){a.lost|=SEVER_BITS[limb];severed.push(part);}
   }
  }
  a.minion.hp=a.hp;a.minion.maxHp=a.maxHp;a.lastDamageAt=t;a.lastHitBy=attacker;
  if(total>0)a.dealt.set(attacker,(a.dealt.get(attacker)??0)+total);
  this.ledger(attacker,total);
  // Consequences of new losses.
  const ls=limbState(a.lost);a.headless=ls.headless;a.limp=ls.limp;
  if(ls.crawl&&!a.crawl){
   a.crawl=true;this.cancelAttack(a);const c=HORDE.collider,col=a.minion.collider;col.setHalfHeight(c.crawlHalfHeight);col.setRadius(c.crawlRadius);
   a.minion.body.setTranslation(v(pos.x,pos.y-CRAWL_DROP,pos.z),true);
  }
  if(severed.length&&a.attackAt>0&&!a.struck){const prof=attackProfile(a);a.atkDamage=prof.damage;a.atkReach=prof.reach;}
  for(const part of severed)this.queue(PVE_EVENT.gib,jointWorld({pos,yaw:a.yaw,crawl:a.crawl},part),{target:a.id,message:PART_LIMB[part],from:{x:dir.x,y:dir.y,z:dir.z},source:attacker},true);
  // Stagger, knockback and the hit view.
  if(total>=a.maxHp*.25||impulse>=400){a.staggerUntil=Math.max(a.staggerUntil,t+a.stats.stagger);this.cancelAttack(a);}
  const k=Math.min(HORDE.extMax,impulse/HORDE.collider.mass*.5);a.ext.x+=dir.x*k;a.ext.z+=dir.z*k;
  const el=Math.hypot(a.ext.x,a.ext.z);if(el>HORDE.extMax){a.ext.x*=HORDE.extMax/el;a.ext.z*=HORDE.extMax/el;}
  if(Math.hypot(dir.x,dir.z)>1e-6){a.hitAt=t;a.hitYaw=Math.atan2(dir.x,dir.z);}
  this.hitEvent(a,attacker,total,hit,pos);
  if(a.hp<=1e-9||a.headless)this.kill(a,attacker,a.headless?'head':'damage');
  return total;
 }
 /** Add damage for the ledger: party members directly, recruits for their hirer at the recruit share. */
 private ledger(attacker:string,amount:number){
  if(amount<=0)return;const p=this.sim.players.get(attacker);if(!p)return;
  if(this.host.isRecruit(p)){const owner=this.host.recruits.views().find(r=>r.id===attacker)?.owner;if(owner)this.host.stats(owner).addDamage+=amount*ECONOMY.recruitKillShare;return;}
  if(this.host.isHuman(p)||this.host.isPartyBot(p))this.host.stats(attacker).addDamage+=amount;
 }
 /** Hit events: fields and DoTs aggregate per add per 0.25 s; direct hits use a 20/s token bucket per attacker. */
 private hitEvent(a:PveAdd,attacker:string,amount:number,hit:AddHitInfo|undefined,pos:Vec){
  if(amount<=0)return;const t=this.time;
  if(!hit||hit.kind==='field'){
   a.fieldSum+=amount;a.fieldBy=attacker;
   if(t-a.fieldAt>=HORDE.aoeEventInterval){a.fieldAt=t;this.queue('minionHit',pos,{source:attacker,target:a.id,amount:a.fieldSum,color:HIT_EVENT_COLOR},false);a.fieldSum=0;}
   return;
  }
  const rate:number=HORDE.hitEventsPerSecond;let b=this.buckets.get(attacker);if(!b){b={tokens:rate,at:t};this.buckets.set(attacker,b);}
  b.tokens=Math.min(rate,b.tokens+(t-b.at)*rate);b.at=t;
  if(b.tokens>=1){b.tokens--;this.queue('minionHit',hit.point??pos,{source:attacker,target:a.id,amount,color:HIT_EVENT_COLOR,...(hit.headshot?{headshot:true}:{})},false);}
  else this.stats.eventsDropped++;
 }
 private queue(type:string,pos:Vec,extra:object,keep:boolean){this.outbox.push({type,pos:{x:pos.x,y:pos.y,z:pos.z},extra,keep,at:this.time});}
 /** Emits at most HORDE_EVENTS_PER_TICK horde events per tick: deaths and gibs first (never dropped, carried over), then
  *  hits younger than 0.3 s. Protects the 80-event ring (config maxEvents) at 20 Hz snapshots. */
 private flush(){
  if(!this.outbox.length)return;const t=this.time;let budget=HORDE_EVENTS_PER_TICK;const rest:Outgoing[]=[];
  for(const e of this.outbox)if(e.keep){if(budget>0){budget--;this.sim.emit(e.type,e.pos,e.extra);}else rest.push(e);}
  for(const e of this.outbox)if(!e.keep){if(t-e.at>.3){this.stats.eventsDropped++;continue;}if(budget>0){budget--;this.sim.emit(e.type,e.pos,e.extra);}else rest.push(e);}
  this.outbox=rest;
 }
 private viewOf(a:PveAdd,state:AddState):PveAddView{
  const p=this.posOf(a),vel=a.phase==='active'&&a.minion?a.minion.body.linvel():{x:0,z:0};
  const view:PveAddView={n:a.n,id:a.id,pos:{x:p.x,y:p.y,z:p.z},yaw:a.yaw,vel:{x:vel.x,z:vel.z},hp:a.maxHp>0?Math.max(0,a.hp/a.maxHp):0,kind:a.kind,state,
   attack:a.attack,side:a.side,lost:a.lost,entry:a.phase==='active'?-1:a.entry,headless:a.headless,spawnAt:a.spawnAt};
  if(a.target)view.target=a.target;
  if(a.attackAt>0)view.attackAt=a.attackAt;if(a.hitAt>0){view.hitAt=a.hitAt;view.hitYaw=a.hitYaw;}if(a.diveAt>0&&(a.travel||a.phase==='vent'))view.diveAt=a.diveAt;
  return view;
 }
 private kill(a:PveAdd,attacker:string|null,cause:AddDeath['cause'],uncapped=false){
  if(!this.list.has(a.n))return;const s=this.sim,t=this.time,pos={...this.posOf(a)};
  a.hp=0;
  const view=this.viewOf(a,'dead');view.deathAt=t;view.entry=-1;view.vel={x:0,z:0};delete view.target;
  if(a.minion){if(s.chaos.minions.get(a.id)===a.minion)s.chaos.removeMinion(a.minion,false);else{s.colliderOwners.delete(a.minion.collider.handle);s.world.removeRigidBody(a.minion.body);}}
  // S7: the body is gone; this tick's separation hash may still hold the add, so drop the stale body handle (Rapier panics on it).
  a.pos=v(pos.x,pos.y,pos.z);a.minion=undefined;
  if(a.phase==='entering'||a.phase==='pending')this.sched.leave(a.entry,a.n);
  this.list.delete(a.n);this.byId.delete(a.id);this.stats.killed++;
  this.corpses.push({n:a.n,view,until:t+HORDE.corpseSeconds});
  if(!uncapped)while(this.corpses.length>HORDE.maxCorpses)this.corpses.shift();
  const q=attacker?s.players.get(attacker):undefined,killer=q&&!this.host.isBoss(q)?q.id:null;
  const assists=[...a.dealt].filter(([id,d])=>id!==killer&&d>=a.maxHp*ECONOMY.assistMinFraction&&s.players.has(id)).map(([id])=>id);
  this.host.onAddKilled({n:a.n,kind:a.kind,pos,killer,assists,headless:a.headless,possessed:a.possessed,cause});
  this.queue(PVE_EVENT.kill,pos,{source:killer,target:a.id,amount:cause==='burn'?0:Math.round(a.stats.reward*a.mods.reward),message:a.kind},true);
 }
 // ---------------------------------------------------------------- possession
 /** §6.5: the active, non-crawling add farthest from every human, seeded among the top 5 (×3 HP, no attacks, keep-away);
  *  with none active, a fresh shambler through the farthest door, possessed on arrival. Returns its n. */
 possess():number|null{
  const cur=this.possessed();if(cur!==undefined)return cur;
  const humans=this.host.humans().filter(p=>p.connected&&p.hp>0).map(p=>p.body.translation());
  const far=(p:{x:number;z:number})=>humans.length?Math.min(...humans.map(h=>flat(p,h))):0;
  const pool=[...this.list.values()].filter(a=>a.phase==='active'&&!a.crawl&&a.minion).map(a=>({a,d:far(a.minion!.body.translation())}))
   .sort((x,y)=>y.d-x.d||x.a.n-y.a.n).slice(0,5);
  if(pool.length){
   const a=pool[Math.min(pool.length-1,Math.floor(this.rng().next()*pool.length))].a;
   a.possessed=true;a.possessedAt=this.time;a.maxHp*=PVE_SIREN.possession.hpScale;a.hp=a.maxHp;a.parts={};
   if(a.minion){a.minion.hp=a.hp;a.minion.maxHp=a.maxHp;}this.cancelAttack(a);a.thinkAt=this.time;return a.n;
  }
  const doors=PVE_ENTRIES.filter(e=>e.kind==='door').map(e=>({e:e.e,d:far(e.exit)})).sort((x,y)=>y.d-x.d||x.e-y.e);
  const door=doors.find(d=>this.sched.queueLength(d.e)<HORDE.entry.queue)??doors[0];
  const a=this.create('shambler',true);this.enter(a,door.e);return a.n;
 }
 // ---------------------------------------------------------------- assists (D38)
 /** A party player's chaos zombie or toy with no enemy player: chase the nearest active add within 20 m and hit it
  *  through chaos.hitMinion (credited to the owner). */
 assistMinion(m:Minion,owner:Player,_dt:number){
  const s=this.sim,t=this.time,as=HORDE.assist;let st=this.assists.get(m.id);
  if(!st){st={at:-Infinity};this.assists.set(m.id,st);if(this.assists.size>64)for(const id of [...this.assists.keys()])if(!s.chaos.minions.has(id))this.assists.delete(id);}
  const p=m.body.translation();let add=st.target?this.byId.get(st.target):undefined;
  if(!add||add.phase!=='active'||t>=st.at+as.retarget){
   st.at=t;add=undefined;let best:number=as.range;
   for(const a of this.list.values()){if(a.phase!=='active'||!a.minion)continue;const d=flat(p,a.minion.body.translation());if(d<best){best=d;add=a;}}
   st.target=add?.id;
  }
  const vel=m.body.linvel();
  if(!add?.minion){m.body.setLinvel(v(0,vel.y,0),true);return;}
  const ap=add.minion.body.translation(),dx=ap.x-p.x,dz=ap.z-p.z,d=Math.hypot(dx,dz)||1,dir=v(dx/d,0,dz/d);
  m.yaw=Math.atan2(dir.x,dir.z);
  m.body.setLinvel(v(d>as.stop?dir.x*m.speed:0,vel.y,d>as.stop?dir.z*m.speed:0),true);
  if(d<as.reach&&t>=m.nextAttack){m.lastAttack=t;m.nextAttack=t+as.interval;s.chaos.hitMinion(add.minion,(m.kind==='toy'?as.damage.toy:as.damage.zombie)*owner.stats.damage,owner.id,as.impulse,dir);}
 }
 // ---------------------------------------------------------------- holds, end, rekey
 /** Hold start: cancel windups, stop horizontal motion and knockback. */
 freeze(){
  for(const a of this.list.values()){this.cancelAttack(a);a.ext={x:0,z:0};
   if(a.phase==='active'&&a.minion){const vel=a.minion.body.linvel();a.minion.body.setLinvel(v(0,vel.y,0),true);}}
 }
 onHold(active:boolean){if(active)this.freeze();}
 /** Adds dt to every absolute horde timer (D21): nothing advances during a hold and nothing bursts on release. */
 shift(dt:number){
  for(const a of this.list.values()){
   a.spawnAt+=dt;a.walkAt+=dt;a.activeAt+=dt;if(a.attackAt>0)a.attackAt+=dt;a.strikeAt+=dt;a.recoverUntil+=dt;a.staggerUntil+=dt;a.thinkAt+=dt;
   if(a.diveAt>0)a.diveAt+=dt;a.ventUntil+=dt;a.ventCooldown+=dt;if(a.hitAt>0)a.hitAt+=dt;a.lastDamageAt+=dt;if(a.possessed)a.possessedAt+=dt;
   a.progressAt+=dt;a.nudgeUntil+=dt;a.fieldAt+=dt;
  }
  for(const c of this.corpses){c.until+=dt;const w=c.view;if(w.deathAt)w.deathAt+=dt;if(w.hitAt)w.hitAt+=dt;if(w.spawnAt)w.spawnAt+=dt;if(w.attackAt)w.attackAt+=dt;if(w.diveAt)w.diveAt+=dt;}
  for(const b of this.buckets.values())b.at+=dt;for(const st of this.assists.values())st.at+=dt;for(const e of this.outbox)e.at+=dt;
  this.sched.shift(dt);this.nav.shift(dt);
 }
 /** Every add dies (cause 'burn', no killer, no corpse cap); queued and first-time entering adds are dropped silently. */
 killAll(_cause:'boss-dead'|'defeat'){
  this.flow=null;this.budget.reset();
  for(const a of [...this.list.values()]){
   if(a.phase==='pending'||a.phase==='entering'&&!a.minion){this.sched.leave(a.entry,a.n);this.list.delete(a.n);this.byId.delete(a.id);continue;}
   this.kill(a,null,'burn',true);
  }
 }
 rekey(from:string,to:string){
  for(const a of this.list.values()){
   if(a.target===from)a.target=to;if(a.lastHitBy===from)a.lastHitBy=to;if(a.fieldBy===from)a.fieldBy=to;
   const d=a.dealt.get(from);if(d!==undefined){a.dealt.delete(from);a.dealt.set(to,(a.dealt.get(to)??0)+d);}
  }
  const b=this.buckets.get(from);if(b){this.buckets.delete(from);this.buckets.set(to,b);}
  this.nav.forget(from);
 }
 views(slots:readonly string[]):{adds:PveAddTuple[];entries:PveEntryView[]}{
  const adds:PveAddTuple[]=[];
  for(const a of this.list.values())if(a.phase!=='pending')adds.push(encodeAdd(this.viewOf(a,this.stateOf(a)),slots));
  for(const c of this.corpses)adds.push(encodeAdd(c.view,slots));
  return {adds,entries:this.sched.views(this.time)};
 }
 reset(){
  // resetArena's chaos.clear() already removed every registered add body; never touch old bodies here.
  this.list.clear();this.byId.clear();this.corpses=[];this.serial=0;this.flow=null;this.budget.reset();this.flowReleased=0;
  this.sched.reset();this.outbox=[];this.buckets.clear();this.assists.clear();this.stepCtx=undefined;
  this.stats={spawned:0,activated:0,killed:0,dives:0,eventsDropped:0};
  this.nav.setBlocks([...arenaBlocks('siren'),...machineBlocks()]);
 }
 debug(op:string){
  if(op==='kill-adds'){for(const a of [...this.list.values()])if(a.phase==='active'||a.phase==='vent'||a.travel)this.kill(a,null,'damage');return true;}
  if(op.startsWith('spawn:')){const kind=op.slice(6) as AddKind;return ADD_KINDS_ORDER.includes(kind)&&this.spawn(kind)!==null;}
  return false;
 }
}
