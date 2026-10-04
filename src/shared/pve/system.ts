// PveSystem facade (design Appendix B). FROZEN by the foundation. The Simulation calls these hooks only when
// options.pve is set; in PvP `sim.pve` is undefined and none of this runs. Parts (Horde, Director, Economy, Recruits) are
// built from their seed files, or from PveSystem.testParts in tests (tests/helpers/pve-fakes.ts: withPveParts).
import type {Player,Simulation,Snapshot,GameEvent} from '../simulation.js';
import type {Minion} from '../chaos.js';
import {CONFIG as C,idleInput,v,add,sub,norm,len,type Input,type Vec,type SpecId} from '../config.js';
import {CLASS_INFO,WEAPONS,statsFor,magazineCapacity} from '../content.js';
import {BOT_WEAPONS} from '../lobby-presentation.js';
import type {ArenaBlock} from '../arena.js';
import {BOSS_PLAYER_ID,HORDE_ID,HORDE_NAME,PARTY_BOT_PREFIX,PVE_CONTRACT,PVE_EVENT,PVE_GROUPS,PVE_TEAMS,RECRUIT_PREFIX,sanitizePveCommand,
 type AddDeath,type AddHitInfo,type DirectorApi,type EconomyApi,type HordeApi,type PveHost,type PveOptions,type PveOutcome,
 type PvePartsFactory,type PveStats,type PveView,type RecruitsApi} from './contract.js';
import {PVE,PVE_SIREN,damageScale,RECRUIT} from './balance.js';
import {hash32,PveRng} from './rng.js';
import {machineBlocks,PVE_LAYOUT} from './layout.js';
import {Horde} from './horde.js';
import {Director} from './director.js';
import {Economy} from './economy.js';
import {Recruits} from './recruits.js';

type RngLabel='horde'|'director'|'economy'|'recruits';
const emptyStats=():PveStats=>({addDamage:0,bossDamage:0,kills:0,assists:0,heads:0,revives:0,revived:0,deaths:0});
const ADD_ID=/^add-\d+$/;
const PARTY_BOT_NAMES=['Comet','Circuit','Blitz'] as const, PARTY_BOT_COLORS=['#70e1d0','#e7ac78','#b591ef'] as const;
/** Non-DEV ranged and melee weapons (D40): party bots and recruit damage dealers draw from this list. */
export const PARTY_WEAPONS:readonly SpecId[]=BOT_WEAPONS.filter(w=>(RECRUIT.dpsClasses as readonly string[]).includes(w.class)).map(w=>w.id);
/** Idle input that keeps the sequence, look direction and camera (choosing, channelling, held, hidden). */
export const idleKeepLook=(i:Input):Input=>({...idleInput(),seq:i.seq,yaw:i.yaw,pitch:i.pitch,camera:i.camera});

export class PveSystem implements PveHost{
 /** Test-only seam: when set, the constructor builds the supplied parts instead of the seeds. Use withPveParts(). */
 static testParts?:PvePartsFactory;
 readonly options:PveOptions;
 runIndex=0;runSeed=0;spawningBoss=false;
 readonly horde:HordeApi;readonly director:DirectorApi;readonly economy:EconomyApi;readonly recruits:RecruitsApi;
 /** The run outcome once finish() ran (the director's view may also carry it). */
 outcome?:PveOutcome;endedAt?:number;
 private streams=new Map<RngLabel,PveRng>();
 private ledger=new Map<string,PveStats>();
 private hiddenIds=new Set<string>();
 private untouchableUntil=new Map<string,number>();
 private god=false;
 private hellos=new Map<string,{v:number;key?:string}>();
 private slotIds:string[]=[];
 private partyBotSerial=0;private partyBotsCreated=false;private creatingPartyBot=false;
 private adopting?:string;
 /** Exactly one human and no party bots at fight start (solo self-revive, D7c). Read by S3 through `solo`. */
 solo=false;
 constructor(readonly sim:Simulation,options:PveOptions){
  this.options=options;this.runSeed=hash32(options.seed>>>0,0);
  const f=PveSystem.testParts;
  this.horde=f?.horde?.(this)??new Horde(this);
  this.director=f?.director?.(this)??new Director(this);
  this.economy=f?.economy?.(this)??new Economy(this);
  this.recruits=f?.recruits?.(this)??new Recruits(this);
 }
 // ---- PveHost: queries ----
 rng(label:RngLabel){let s=this.streams.get(label);if(!s){s=new PveRng(hash32(this.runSeed,label));this.streams.set(label,s);}return s;}
 isBoss(p:Player){return p.id===BOSS_PLAYER_ID;}
 isRecruit(p:Player){return p.id.startsWith(RECRUIT_PREFIX);}
 isPartyBot(p:Player){return !!p.bot&&p.id.startsWith(PARTY_BOT_PREFIX);}
 isHuman(p:Player){return !p.bot&&!this.isBoss(p)&&!this.isRecruit(p);}
 isMember(p:Player){return this.isHuman(p)||this.isPartyBot(p);}
 humans(){return [...this.sim.players.values()].filter(p=>this.isHuman(p));}
 partyBots(){return [...this.sim.players.values()].filter(p=>this.isPartyBot(p));}
 members(){return [...this.sim.players.values()].filter(p=>this.isMember(p));}
 party(){return [...this.sim.players.values()].filter(p=>this.isMember(p)||this.isRecruit(p));}
 boss(){return this.sim.players.get(BOSS_PLAYER_ID);}
 /** h: connected humans plus party bots (when PVE.botsCountForScaling), at least 1. */
 partySize(){return Math.max(1,this.humans().filter(p=>p.connected).length+(PVE.botsCountForScaling?this.partyBots().length:0));}
 /** Party slot order (§3.7): humans and party bots by join order, then recruits by hire order; at most 7. */
 slots():readonly string[]{const ids=this.slotIds.filter(id=>this.sim.players.has(id)),rec=(id:string)=>id.startsWith(RECRUIT_PREFIX);return [...ids.filter(id=>!rec(id)),...ids.filter(rec)].slice(0,7);}
 stats(id:string){let s=this.ledger.get(id);if(!s){s=emptyStats();this.ledger.set(id,s);}return s;}
 emit(type:string,pos:Vec,extra:object={}){this.sim.emit(type,pos,extra as Partial<GameEvent>);}
 hidden(p:Player){return this.hiddenIds.has(p.id);}
 /** Hides or shows a player: hidden players are untouchable and untargetable, and their body and collider are disabled. */
 setHidden(p:Player,hidden:boolean){if(hidden)this.hiddenIds.add(p.id);else this.hiddenIds.delete(p.id);p.body.setEnabled(!hidden);p.collider.setEnabled(!hidden);}
 setUntouchable(id:string,until:number){this.untouchableUntil.set(id,until);}
 untouchable(p:Player){return this.hidden(p)||(this.untouchableUntil.get(p.id)??-Infinity)>this.sim.time||this.god&&this.isHuman(p);}
 standAt(x:number,z:number,p?:Player):Vec{const hh=p?p.collider.halfHeight():C.playerHalfHeight,r=p?p.collider.radius():C.playerRadius;return v(x,hh+r+.05,z);}
 // ---- PveHost: director-only and horde-only fan-outs ----
 notifyHold(active:boolean){this.horde.onHold(active);this.economy.onHold(active);this.recruits.onHold(active);}
 finish(outcome:PveOutcome){
  const s=this.sim;this.outcome=outcome;this.endedAt=s.time;s.phase='match';s.phaseEnd=0;
  s.winner=outcome==='victory'?this.humans()[0]?.id??null:null;s.setWinner=null;
  s.result=outcome==='victory'?'The Siren is silenced':'The party has fallen';
 }
 onAddKilled(d:AddDeath){
  if(d.killer){const k=this.stats(d.killer);k.kills++;if(d.headless)k.heads++;}
  for(const id of d.assists)this.stats(id).assists++;
  this.economy.onAddKilled(d);this.director.onAddKilled(d);
 }
 // ---- Simulation hooks (edit map) ----
 /** #19 Machine colliders. */
 blocks():ArenaBlock[]{return machineBlocks();}
 /** #18 New run: next run index and seed, boss and recruits removed, ledger and maps cleared, every part reset. Party bots stay. */
 reset(){
  this.runIndex++;this.runSeed=hash32(this.options.seed>>>0,this.runIndex);this.streams.clear();
  for(const p of [...this.sim.players.values()])if(this.isBoss(p)||this.isRecruit(p))this.sim.removePlayer(p.id);
  this.ledger.clear();this.hiddenIds.clear();this.untouchableUntil.clear();this.spawningBoss=false;this.outcome=undefined;this.endedAt=undefined;this.solo=false;
  this.horde.reset();this.director.reset();this.economy.reset();this.recruits.reset();
 }
 /** #7 Seats: the boss while spawningBoss, facade party bots, recruits, an adopting rejoin; a human only while humans<maxParty,
  *  evicting the newest party bot first when the party is full. */
 admit(id:string,bot:unknown){
  if(id===BOSS_PLAYER_ID)return this.spawningBoss;
  if(this.adopting===id)return true;
  if(id.startsWith(PARTY_BOT_PREFIX))return this.creatingPartyBot;
  if(id.startsWith(RECRUIT_PREFIX))return !!bot;
  if(bot)return false;
  const humans=this.humans().length,bots=this.partyBots();
  if(humans>=PVE.maxParty)return false;
  if(humans+bots.length>=PVE.maxParty){const newest=bots.at(-1);if(!newest)return false;this.sim.removePlayer(newest.id);}
  return true;
 }
 /** #8 Team, collision groups, Siren fallback for humans, slot; party bots after the first human in the lobby. */
 onPlayerAdded(p:Player){
  const boss=this.isBoss(p)&&this.spawningBoss;
  p.team=boss?PVE_TEAMS.boss:PVE_TEAMS.party;p.collider.setCollisionGroups(PVE_GROUPS.player);
  if(!boss&&p.spec==='siren'){p.spec=CLASS_INFO[p.class].default;p.stats=statsFor(p.chips,p.spec);p.hp=p.stats.hp;p.ammo=magazineCapacity(p.spec,p.stats);}
  if(!boss&&this.adopting!==p.id&&!this.slotIds.includes(p.id))this.slotIds.push(p.id);
  if(this.isHuman(p)&&this.adopting!==p.id&&this.sim.phase==='lobby'&&!this.partyBotsCreated){this.partyBotsCreated=true;this.createPartyBots();}
 }
 private createPartyBots(){
  const want=Math.min(PVE.maxPartyBots,this.options.partyBots??0);
  for(let k=0;k<want&&this.humans().length+this.partyBots().length<PVE.maxParty;k++){
   const n=++this.partyBotSerial,id=PARTY_BOT_PREFIX+n,rng=new PveRng(hash32(this.options.seed>>>0,'party-bot',n)),spec=rng.pick(PARTY_WEAPONS);
   this.creatingPartyBot=true;
   try{this.sim.addPlayer(id,`${PARTY_BOT_NAMES[(n-1)%3]} Bot`,WEAPONS[spec].class,'normal',PARTY_BOT_COLORS[(n-1)%3],{preferredSpec:spec},{developer:false,unlocks:[]});}
   finally{this.creatingPartyBot=false;}
  }
 }
 /** Router join check (Simulation.joinable): humans below the party cap. */
 canJoin(){return this.humans().length<PVE.maxParty;}
 /** #12 Start needs at least one connected human that sent a hello. */
 canStart(){return this.humans().some(p=>p.connected&&this.hellos.has(p.id));}
 helloed(id:string){return this.hellos.has(id);}
 /** §9.3: the old player id for a disconnected human whose hello key matches, only while fighting. */
 rejoinSeat(key:string):string|undefined{
  if(this.sim.phase!=='fight'||typeof key!=='string'||!key)return undefined;
  for(const p of this.humans())if(!p.connected&&this.hellos.get(p.id)?.key===key)return p.id;
  return undefined;
 }
 /** §9.3: move the old seat to a new connection id, keeping its state, and re-key every part. */
 adoptSeat(oldId:string,newId:string){
  const s=this.sim,old=s.players.get(oldId);if(!old||s.players.has(newId))return false;
  const keep={name:old.name,cls:old.class,spec:old.spec,chips:{...old.chips},cosmetics:old.cosmetics,color:old.color,
   access:{developer:old.developer,unlocks:[...old.unlocks],accountId:old.accountId,flags:[...old.flags]},hp:old.hp,death:old.death,
   pos:{...old.body.translation()},yaw:old.input.yaw,pitch:old.input.pitch,ammo:old.ammo,mana:old.mana,stamina:old.stamina,score:old.score,roundWins:old.roundWins};
  const slot=this.slotIds.indexOf(oldId),hello=this.hellos.get(oldId),hidden=this.hiddenIds.has(oldId),until=this.untouchableUntil.get(oldId),ledger=this.ledger.get(oldId);
  s.removePlayer(oldId);
  this.adopting=newId;let p:Player|null;
  try{p=s.addPlayer(newId,keep.name,keep.cls,null,keep.color,keep.cosmetics,keep.access);}finally{this.adopting=undefined;}
  if(!p)return false;
  p.spec=keep.spec;p.chips=keep.chips;p.stats=statsFor(p.chips,p.spec);p.hp=Math.min(keep.hp,p.stats.hp);p.death=keep.death;p.cosmetics=keep.cosmetics;
  p.ammo=keep.ammo;p.mana=keep.mana;p.stamina=keep.stamina;p.score=keep.score;p.roundWins=keep.roundWins;p.selected=true;p.ready=true;
  p.input={...p.input,yaw:keep.yaw,pitch:keep.pitch};p.body.setTranslation(keep.pos,true);p.body.setLinvel(v(),true);
  if(slot>=0)this.slotIds[slot]=newId;else this.slotIds.push(newId);
  this.hellos.delete(oldId);if(hello)this.hellos.set(newId,hello);
  this.hiddenIds.delete(oldId);if(hidden)this.setHidden(p,true);
  this.untouchableUntil.delete(oldId);if(until!==undefined)this.untouchableUntil.set(newId,until);
  this.ledger.delete(oldId);if(ledger)this.ledger.set(newId,ledger);
  this.horde.rekey(oldId,newId);this.economy.rekey(oldId,newId);this.recruits.rekey(oldId,newId);
  this.emit(PVE_EVENT.rejoin,p.body.translation(),{target:newId,message:keep.name});
  return true;
 }
 /** #36 countdown→fight. */
 onFightStart(){
  for(const p of this.members())this.stats(p.id);
  this.solo=this.humans().filter(p=>p.connected).length===1&&this.partyBots().length===0;
  this.economy.onFightStart();this.director.onFightStart();
 }
 /** #29 After the queue pop and botInput: holds idle everyone; recruits and party bots use their brains (idle when none);
  *  the boss idles while hidden, else the director refines it; humans get the economy's forced input (choosing, channelling). */
 filterInput(p:Player,i:Input):Input{
  if(this.director.holding())return idleKeepLook(i);
  if(this.isRecruit(p)||this.isPartyBot(p))return this.recruits.inputFor(p,i)??idleKeepLook(i);
  if(this.isBoss(p))return this.hidden(p)?idleKeepLook(i):this.director.bossInput(p,i);
  return this.economy.inputFor(p,i)??i;
 }
 /** #38 */
 holding(){return this.director.holding();}
 /** #38 Hold tick: the director's timeline; every part shifts its absolute timers; party horizontal motion stops. */
 stepHold(dt:number){
  this.director.stepHold(dt);this.horde.shift(dt);this.economy.shift(dt);this.recruits.shift(dt);this.sim.abilities.shift(dt);
  for(const p of this.sim.players.values()){if(!this.isMember(p)&&!this.isRecruit(p)&&!this.isBoss(p))continue;const vel=p.body.linvel();p.body.setLinvel(v(0,vel.y,0),true);p.move=v();p.ext=v();
   for(const dots of [p.burns,p.poisons])for(const d of dots.values()){d.next+=dt;d.until+=dt;}}
  for(const [id,until] of this.untouchableUntil)this.untouchableUntil.set(id,until+dt);
 }
 /** End of ChaosSystem.step: add AI before physics. */
 stepHorde(dt:number){this.horde.step(dt);}
 /** #39 After physics: economy, recruits, director (last). */
 stepAfterPhysics(dt:number){this.economy.step(dt);this.recruits.step(dt);this.director.step(dt);}
 /** #10 Faction refs for non-player ids: the horde, adds and the boss id are on the boss team. */
 teamRef(id:string|null|undefined){return id&&(id===HORDE_ID||id===BOSS_PLAYER_ID||ADD_ID.test(id))?{id,team:PVE_TEAMS.boss}:undefined;}
 /** #24 Remaining damage room above the director's HP floor (boss only). */
 damageRoom(p:Player){const floor=this.director.hpFloor(p);return floor===undefined?undefined:p.hp-floor;}
 /** #25 Kill-feed attribution for horde damage. */
 damager(source:string){return source===HORDE_ID||ADD_ID.test(source)?{id:HORDE_ID,name:HORDE_NAME}:undefined;}
 /** The hirer of a recruit (from the recruits view), else the id itself. */
 creditOf(id:string){return id.startsWith(RECRUIT_PREFIX)?this.recruits.views().find(r=>r.id===id)?.owner??id:id;}
 /** #26 Ledger boss damage, economy and director notifications. Runs before the death check. */
 onDamage(p:Player,actual:number,source:string|null,_kind:string){
  if(this.isBoss(p)&&source){const q=this.sim.players.get(source);if(q&&(this.isMember(q)||this.isRecruit(q))){const credit=this.creditOf(source);this.stats(credit).bossDamage+=actual;this.economy.onBossDamage(credit,actual,p.stats.hp);}}
  this.economy.onDamage(p,actual,source);this.director.onDamage(p,actual,source);
 }
 /** #27 After the death event. */
 onDeath(p:Player){if(this.isMember(p))this.stats(p.id).deaths++;this.economy.onDeath(p);this.recruits.onDeath(p);this.director.onDeath(p);}
 /** Called by Simulation.revivePlayer. */
 onRevive(p:Player){this.economy.onRevive(p);}
 /** chaos.hitMinion for kind 'siren-add'. */
 hitAdd(m:Minion,damage:number,attacker:string,impulse:number,dir:Vec,hit?:AddHitInfo):number{const actual:unknown=this.horde.hit(m,damage,attacker,impulse,dir,hit);return typeof actual==='number'?actual:0;}
 /** chaos.ts step loop: a party player's zombie or toy with no enemy player to chase (D38). */
 assistMinion(m:Minion,owner:Player,dt:number){if(this.isMember(owner)||this.isRecruit(owner))this.horde.assistMinion(m,owner,dt);}
 /** #33 Ground ray rules: humans never ground on adds; the grown boss grounds with its longer ray. */
 motion(p:Player):{groundGroups?:number;groundRay?:number}|undefined{
  const scale=this.isBoss(p)?p.siren?.boost?.scale??1:1;
  return scale!==1?{groundGroups:PVE_GROUPS.player,groundRay:(C.playerHalfHeight+C.playerRadius)*scale+.16}:{groundGroups:PVE_GROUPS.player};
 }
 /** #34 A falling boss returns to its spawn without damage (D27). */
 rescueFall(p:Player){
  if(!this.isBoss(p))return false;const b=PVE_LAYOUT.bossSpawn;
  p.body.setTranslation(this.standAt(b.x,b.z,p),true);p.body.setLinvel(v(),true);p.ext=v();p.move=v();return true;
 }
 /** siren.ts crush (D35): a heavy hit through damage(), so shields, god and the damage scale apply. */
 sirenCrush(boss:Player,victim:Player){
  const pos=victim.body.translation(),dir=norm(v(pos.x-boss.body.translation().x,0,pos.z-boss.body.translation().z));
  return this.sim.damage(victim,PVE_SIREN.laughCrush*victim.stats.hp*damageScale(this.partySize()),boss.id,'siren-crush',dir,0,add(pos,v(0,.65,0)),true,true);
 }
 weaponAway(p:Player){return this.economy.weaponAway(p)||this.recruits.weaponAway(p);}
 /** #42 Adds pve, intro.boss, personal colours (D25) and PvE weaponAway to a PvE snapshot. */
 decorate(s:Snapshot){
  s.pve=this.view();
  if(this.sim.intro?.boss&&s.intro)s.intro.boss=true;
  for(const view of s.players){const p=this.sim.players.get(view.id);if(!p)continue;view.color=p.color;if(this.weaponAway(p))view.weaponAway=true;}
 }
 /** Router entry for {type:'chip',pve}. hello is handled here; debug needs options.debug and a trusted sender; other
  *  actions go to the economy, humans only. Never emits an error message. */
 command(id:string,raw:unknown,trusted:boolean){
  const cmd=sanitizePveCommand(raw),p=this.sim.players.get(id);if(!cmd||!p)return false;
  if(cmd.action==='hello'){if(!this.hellos.has(id))this.hellos.set(id,{v:cmd.v,...(cmd.key?{key:cmd.key}:{})});return true;}
  if(cmd.action==='debug')return this.options.debug===true&&trusted&&this.debug(cmd.op);
  if(!this.isHuman(p))return false;
  return this.economy.command(id,cmd);
 }
 /** Debug ops (D29): god here; then director, economy, horde. */
 debug(op:string){
  if(op==='god:on'||op==='god:off'){this.god=op==='god:on';return true;}
  return this.director.debug(op)||this.economy.debug(op)||this.horde.debug(op);
 }
 get godMode(){return this.god;}
 /** The PvE snapshot view. */
 view():PveView{
  const d=this.director.view(),slots=[...this.slots()],h=this.horde.views(slots),party:Record<string,ReturnType<EconomyApi['partyView']>>={};
  for(const p of this.members())party[p.id]=this.economy.partyView(p.id);
  return {v:PVE_CONTRACT,boss:this.options.boss,...d,outcome:d.outcome??this.outcome,endedAt:d.endedAt??this.endedAt,slots,
   helloed:[...this.hellos.keys()].filter(id=>this.sim.players.has(id)),adds:h.adds,entries:h.entries,
   machines:this.economy.machinesView(),pickups:this.economy.pickupsView(),party,recruits:this.recruits.views()};
 }
 /** Distance on the floor plane (helper for parts). */
 static flat(a:Vec,b:Vec){return len(v(a.x-b.x,0,a.z-b.z));}
 /** Direction helper for parts. */
 static toward(a:Vec,b:Vec){return norm(sub(b,a));}
}
