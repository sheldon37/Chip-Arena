// Fake PvE parts and the PveSystem.testParts seam (design §2.1). Owned by the foundation; streams reuse these fakes to test
// their part against the others without waiting for them. Every fake implements its contract interface fully, records
// calls and returns scriptable values.
import RAPIER from '@dimforge/rapier3d-compat';
import type {Player,Simulation} from '../../src/shared/simulation.js';
import type {Minion} from '../../src/shared/chaos.js';
import {v,type Input,type Vec} from '../../src/shared/config.js';
import {PveSystem,idleKeepLook} from '../../src/shared/pve/system.js';
import {emptyPartyRow} from '../../src/shared/pve/economy.js';
import {IDENTITY_MODS,ADD_KINDS,PVE} from '../../src/shared/pve/balance.js';
import {ADD_PREFIX,HORDE_ID,PVE_GROUPS,RECRUIT_PREFIX,encodeAdd,
 type AddDeath,type AddHitInfo,type AddKind,type AddRef,type DirectorApi,type DropTier,type EconomyApi,type HordeApi,type HordeFlow,
 type HordeModifiers,type PickSource,type PveAddTuple,type PveCommand,type PveEntryView,type PveHost,type PveMachineUse,type PveNav,
 type PvePartsFactory,type PvePartyView,type PvePickupView,type PveRecruitView,type PveView,type RecruitsApi} from '../../src/shared/pve/contract.js';

/** Runs fn with PveSystem.testParts set to factory and always restores the previous value (also for async fn). */
export function withPveParts<T>(factory:PvePartsFactory,fn:()=>T):T{
 const previous=PveSystem.testParts;PveSystem.testParts=factory;
 let async=false;
 try{const out=fn();if(out&&typeof (out as {then?:unknown}).then==='function'){async=true;return (out as unknown as Promise<unknown>).finally(()=>{PveSystem.testParts=previous;}) as T;}return out;}
 finally{if(!async)PveSystem.testParts=previous;}
}
/** Builds every part from fresh fakes; returns the factory and a holder that receives the instances once constructed. */
export function fakeParts(overrides:Partial<{horde:(h:PveHost)=>HordeApi;director:(h:PveHost)=>DirectorApi;economy:(h:PveHost)=>EconomyApi;recruits:(h:PveHost)=>RecruitsApi}>={}){
 const made:{horde?:FakeHorde;director?:FakeDirector;economy?:FakeEconomy;recruits?:FakeRecruits;host?:PveHost}={};
 const factory:PvePartsFactory={
  horde:overrides.horde??(h=>(made.host=h,made.horde=new FakeHorde(h))),
  director:overrides.director??(h=>(made.director=new FakeDirector(h))),
  economy:overrides.economy??(h=>(made.economy=new FakeEconomy(h))),
  recruits:overrides.recruits??(h=>(made.recruits=new FakeRecruits(h))),
 };
 return {factory,made};
}

/** Straight-line nav: direction toward the target player's (x,z), flat distance, always clear. */
export class StraightNav implements PveNav{
 dirtyCalls=0;
 constructor(readonly sim:Simulation){}
 ready(){return true;}
 private target(id:string){return this.sim.players.get(id)?.body.translation();}
 direction(from:Vec,targetId:string){const t=this.target(targetId);if(!t)return null;const dx=t.x-from.x,dz=t.z-from.z,d=Math.hypot(dx,dz);return d<1e-6?null:{x:dx/d,z:dz/d};}
 distance(from:Vec,targetId:string){const t=this.target(targetId);return t?Math.hypot(t.x-from.x,t.z-from.z):Infinity;}
 clear(){return true;}
 dirty(){this.dirtyCalls++;}
}

interface FakeAdd{n:number;kind:AddKind;minion:Minion;maxHp:number;target?:string;attackAt?:number;possessed:boolean}
/** Registers REAL Rapier add bodies (kind 'siren-add', owner HORDE_ID, add collision groups) in chaos.minions, so every
 *  existing damage path (projectiles, melee, explosions, fields) reaches them through chaos.hitMinion → PveSystem.hitAdd →
 *  this.hit. hit() applies the damage, records it and kills at 0 HP (host.onAddKilled). */
export class FakeHorde implements HordeApi{
 readonly nav:StraightNav;
 hits:{n:number;damage:number;attacker:string;impulse:number;dir:Vec;hit?:AddHitInfo}[]=[];
 assists:{minion:string;owner:string;dt:number}[]=[];shifts:number[]=[];holds:boolean[]=[];flows:(HordeFlow|null)[]=[];
 steps=0;resets=0;freezes=0;rekeys:[string,string][]=[];debugOps:string[]=[];killAllCauses:string[]=[];
 /** Scripted add count override for alive() (null: real count). */ aliveOverride:number|null=null;
 possessedN?:number;
 private list=new Map<number,FakeAdd>();private serial=0;
 constructor(readonly host:PveHost){this.nav=new StraightNav(host.sim);}
 /** Creates a real add body standing at pos (capsule centre y defaults to the standing height). */
 addAt(pos:{x:number;y?:number;z:number},kind:AddKind='shambler',opts:{hp?:number;target?:string;possessed?:boolean}={}){
  const s=this.host.sim,n=++this.serial,id=ADD_PREFIX+n,hp=opts.hp??ADD_KINDS[kind].hp,y=pos.y??.9;
  const body=s.world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().setTranslation(pos.x,y,pos.z).lockRotations().setCanSleep(false));
  const collider=s.world.createCollider(RAPIER.ColliderDesc.capsule(.43,.42).setMass(40).setFriction(0).setCollisionGroups(PVE_GROUPS.add),body);
  const minion:Minion={id,owner:HORDE_ID,kind:'siren-add',pos:{x:pos.x,y,z:pos.z},yaw:0,hp,maxHp:hp,speed:0,until:1e9,lastAttack:-10,nextAttack:1e9,body,collider};
  s.chaos.minions.set(id,minion);s.colliderOwners.set(collider.handle,id);
  this.list.set(n,{n,kind,minion,maxHp:hp,target:opts.target,possessed:opts.possessed===true});if(opts.possessed)this.possessedN=n;
  return minion;
 }
 kill(n:number,killer:string|null,cause:AddDeath['cause']='damage'){
  const a=this.list.get(n);if(!a)return;const pos={...a.minion.body.translation()};
  this.host.sim.chaos.removeMinion(a.minion,false);this.list.delete(n);
  this.host.onAddKilled({n,kind:a.kind,pos,killer,assists:[],headless:false,possessed:a.possessed,cause});
 }
 alive(){return this.aliveOverride??this.list.size;}
 queued(){return 0;}
 adds():AddRef[]{return [...this.list.values()].map(a=>({n:a.n,id:a.minion.id,pos:{...a.minion.body.translation()},kind:a.kind,state:'active',hp:a.minion.hp,maxHp:a.maxHp,target:a.target,attackAt:a.attackAt}));}
 get(n:number){return this.adds().find(a=>a.n===n);}
 setFlow(flow:HordeFlow|null){this.flows.push(flow);}
 spawn(kind:AddKind,opts?:{entry?:number;possessed?:boolean}){const p=this.host.party()[0]?.body.translation()??v();return Number(this.addAt({x:p.x+3,z:p.z+3},kind,{possessed:opts?.possessed}).id.slice(ADD_PREFIX.length));}
 possess(){const first=[...this.list.values()][0];if(!first)return null;first.possessed=true;this.possessedN=first.n;return first.n;}
 possessed(){return this.possessedN;}
 hit(m:Minion,damage:number,attacker:string,impulse:number,dir:Vec,hit?:AddHitInfo){
  const n=Number(m.id.slice(ADD_PREFIX.length));this.hits.push({n,damage,attacker,impulse,dir:{...dir},hit:hit?{...hit}:undefined});
  m.hp-=damage;if(m.hp<=0)this.kill(n,attacker);
 }
 assistMinion(m:Minion,owner:Player,dt:number){this.assists.push({minion:m.id,owner:owner.id,dt});}
 step(_dt:number){this.steps++;}
 freeze(){this.freezes++;}
 onHold(active:boolean){this.holds.push(active);}
 shift(dt:number){this.shifts.push(dt);}
 killAll(cause:'boss-dead'|'defeat'|'wave-clear'){this.killAllCauses.push(cause);for(const n of [...this.list.keys()])this.kill(n,null,'burn');}
 rekey(from:string,to:string){this.rekeys.push([from,to]);for(const a of this.list.values())if(a.target===from)a.target=to;}
 views(slots:readonly string[]):{adds:PveAddTuple[];entries:PveEntryView[]}{
  return {adds:[...this.list.values()].map(a=>{const p=a.minion.body.translation();return encodeAdd({n:a.n,id:a.minion.id,pos:{...p},yaw:0,vel:{x:0,z:0},hp:Math.max(0,a.minion.hp/a.maxHp),
   kind:a.kind,state:'active',attack:ADD_KINDS[a.kind].attack,side:1,lost:0,entry:-1,headless:false,target:a.target,attackAt:a.attackAt},slots);}),entries:[]};
 }
 reset(){this.resets++;this.list.clear();this.possessedN=undefined;}
 debug(op:string){this.debugOps.push(op);return op.startsWith('fake-horde');}
}

/** Scriptable director: stage, boss floor, hold and boss input; records notifications. */
export class FakeDirector implements DirectorApi{
 stage:PveView['stage']='opening';hold=false;
 /** HP floor for the boss (undefined: no floor). */ floor:number|undefined;
 /** Optional per-player floor override. */ floorFor?:(p:Player)=>number|undefined;
 bossInputFn?:(p:Player,base:Input)=>Input;
 mods:HordeModifiers={...IDENTITY_MODS};
 fightStarts=0;steps:number[]=[];holdSteps:number[]=[];resets=0;debugOps:string[]=[];
 deaths:string[]=[];kills:AddDeath[]=[];damages:{id:string;actual:number;source:string|null}[]=[];
 extraView:Partial<ReturnType<DirectorApi['view']>>={};
 constructor(readonly host:PveHost){}
 onFightStart(){this.fightStarts++;}
 step(dt:number){this.steps.push(dt);}
 stepHold(dt:number){this.holdSteps.push(dt);}
 holding(){return this.hold;}
 hpFloor(p:Player){if(this.floorFor)return this.floorFor(p);return this.host.isBoss(p)?this.floor:undefined;}
 bossInput(p:Player,base:Input){return this.bossInputFn?this.bossInputFn(p,base):base;}
 modifiers(){return {...this.mods};}
 onAddKilled(d:AddDeath){this.kills.push(d);}
 onDeath(p:Player){this.deaths.push(p.id);}
 onDamage(p:Player,actual:number,source:string|null){this.damages.push({id:p.id,actual,source});}
 view():ReturnType<DirectorApi['view']>{return {stage:this.stage,stageAt:0,wave:0,waves:PVE.wavesBeforeBoss,waveTotal:0,waveLeft:0,hold:this.hold,track:{revealed:0,at:0},...this.extraView};}
 reset(){this.resets++;}
 debug(op:string){this.debugOps.push(op);return op.startsWith('fake-director');}
}

/** Recording economy. Players in `choosing` or `channelling` get idle input that keeps seq, yaw, pitch and camera. */
export class FakeEconomy implements EconomyApi{
 choosingIds=new Set<string>();channellingIds=new Set<string>();moneyBy=new Map<string,number>();
 commands:{id:string;cmd:PveCommand}[]=[];damages:{id:string;actual:number;source:string|null}[]=[];deaths:string[]=[];revives:string[]=[];
 bossDamage:{source:string;amount:number;maxHp:number}[]=[];kills:AddDeath[]=[];shifts:number[]=[];holds:boolean[]=[];steps:number[]=[];
 rekeys:[string,string][]=[];respawns:string[]=[];grants:{id:string;amount:number;reason:string}[]=[];picks:{id:string;tier:DropTier;source:PickSource}[]=[];
 fightStarts=0;resets=0;debugOps:string[]=[];pending=false;commandResult=true;
 constructor(readonly host:PveHost){}
 onFightStart(){this.fightStarts++;}
 onAddKilled(d:AddDeath){this.kills.push(d);}
 onBossDamage(source:string,amount:number,maxHp:number){this.bossDamage.push({source,amount,maxHp});}
 grant(id:string,amount:number,reason:string){this.grants.push({id,amount,reason});this.moneyBy.set(id,this.money(id)+amount);}
 grantAll(amount:number,reason:string){for(const p of this.host.humans())this.grant(p.id,amount,reason);}
 money(id:string){return this.moneyBy.get(id)??0;}
 grantPick(id:string,tier:DropTier,source:PickSource){this.picks.push({id,tier,source});return 'pick-'+this.picks.length;}
 command(id:string,cmd:PveCommand){this.commands.push({id,cmd});return this.commandResult;}
 baseInputs:{id:string;base?:Input}[]=[];
 inputFor(p:Player,base?:Input){this.baseInputs.push({id:p.id,base});return this.choosingIds.has(p.id)||this.channellingIds.has(p.id)?idleKeepLook(base??p.input):undefined;}
 /** Marks `id` as choosing an opened pick (open, openAt, offers in partyView) and returns the pick id. */
 openPick(id:string,offers:string[]=['vital','ricochet','piercing']){this.choosingIds.add(id);this.openPicks.set(id,{id:'pick-'+(this.openPicks.size+1),offers,at:this.host.sim.time});return this.openPicks.get(id)!.id;}
 closePick(id:string){this.choosingIds.delete(id);this.openPicks.delete(id);}
 openPicks=new Map<string,{id:string;offers:string[];at:number}>();
 choosing(p:Player){return this.choosingIds.has(p.id);}
 weaponAway(p:Player){return this.choosingIds.has(p.id);}
 onDamage(p:Player,actual:number,source:string|null){this.damages.push({id:p.id,actual,source});}
 onDeath(p:Player){this.deaths.push(p.id);}
 onRevive(p:Player){this.revives.push(p.id);}
 respawnAll(reason:'wave'|'phase'){this.respawns.push(reason);}
 pendingRevive(){return this.pending;}
 pendingHires(){return 0;}
 step(dt:number){this.steps.push(dt);}
 onHold(active:boolean){this.holds.push(active);}
 shift(dt:number){this.shifts.push(dt);}
 rekey(from:string,to:string){this.rekeys.push([from,to]);}
 partyView(id:string):PvePartyView{const p=this.host.sim.players.get(id),o=this.openPicks.get(id),row={...emptyPartyRow(p&&this.host.isPartyBot(p)?'bot':'human',!!p?.connected),money:this.money(id)};
  if(o){row.picks=[{id:o.id,tier:'common',source:'dispenser',at:o.at,offers:o.offers}];row.open=o.id;row.openAt=o.at;}return row;}
 machinesView():PveMachineUse[]{return [];}
 pickupsView():PvePickupView[]{return [];}
 reset(){this.resets++;}
 debug(op:string){this.debugOps.push(op);return op.startsWith('fake-economy');}
}

/** Recording recruits part with scriptable brains (inputs) and views. */
export class FakeRecruits implements RecruitsApi{
 inputs=new Map<string,(p:Player)=>Input>();recruitViews:PveRecruitView[]=[];
 deaths:string[]=[];shifts:number[]=[];holds:boolean[]=[];steps:number[]=[];rekeys:[string,string][]=[];resets=0;hires:{owner:string;at:Vec;yaw:number}[]=[];
 canHireResult:'ok'|'seats'|'limit'='limit';
 constructor(readonly host:PveHost){}
 canHire(_owner:string){return this.canHireResult;}
 hire(owner:string,at:Vec,yaw:number){this.hires.push({owner,at:{...at},yaw});return null;}
 inputFor(p:Player,_base?:Input){return this.inputs.get(p.id)?.(p);}
 taunting=new Map<string,string>();dismissed:string[]=[];
 tauntTarget(_pos:Vec){return [...this.taunting.values()][0]??null;}
 newest(){const ids=[...this.host.sim.players.keys()].filter(id=>id.startsWith(RECRUIT_PREFIX));return ids.at(-1)??null;}
 /** Removes the recruit player for real; the owner comes from recruitViews (default 'unknown'). */
 dismiss(id:string){if(!this.host.sim.players.has(id))return null;this.dismissed.push(id);this.host.sim.removePlayer(id);return {owner:this.recruitViews.find(r=>r.id===id)?.owner??'unknown'};}
 weaponAway(_p:Player){return false;}
 isRecruit(id:string){return id.startsWith(RECRUIT_PREFIX);}
 tauntNear(_pos:Vec){return false;}
 step(dt:number){this.steps.push(dt);}
 onDeath(p:Player){this.deaths.push(p.id);}
 onHold(active:boolean){this.holds.push(active);}
 shift(dt:number){this.shifts.push(dt);}
 rekey(from:string,to:string){this.rekeys.push([from,to]);}
 views(){return this.recruitViews;}
 reset(){this.resets++;}
}
