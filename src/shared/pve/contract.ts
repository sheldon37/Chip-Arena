// PvE contract (design Appendix A). FROZEN by the foundation: streams S1-S6 code against these types and helpers and
// request changes through the integrator (S7). Everything here is deterministic and free of clocks and Math.random.
import type {Vec,Input} from '../config.js';
import type {Rarity} from '../content.js';
import type {TeamId} from '../teams.js';
import type {Player,Simulation,Options} from '../simulation.js';
import type {Minion} from '../chaos.js';

export const PVE_CONTRACT=1 as const;
export type PveBossId='siren';
/** Authority-side room options (Simulation.options.pve). Built only by pveOption() in a router, or directly by tests. */
export interface PveOptions{boss:PveBossId; seed:number; skipWaves?:boolean; partyBots?:0|1|2|3; debug?:boolean; relay?:boolean; local?:boolean}
/** Values only the authority decides (D29, D30). */
export interface PveOptionContext{seed:number; debug?:boolean; relay?:boolean; local?:boolean}
/** Reads boss, skipWaves and partyBots from raw client data; seed, debug, relay and local come only from ctx.
 *  Returns undefined unless raw is a PvE request (an object with boss:'siren'). Optional flags are present only when true. */
export function pveOption(raw:unknown,ctx:PveOptionContext):PveOptions|undefined{
 if(!raw||typeof raw!=='object')return undefined;const r=raw as {boss?:unknown;skipWaves?:unknown;partyBots?:unknown};
 if(r.boss!=='siren')return undefined;
 const bots=typeof r.partyBots==='number'&&Number.isFinite(r.partyBots)?Math.max(0,Math.min(3,Math.floor(r.partyBots))) as 0|1|2|3:0;
 const seed=Number.isFinite(ctx.seed)?ctx.seed>>>0:0;
 return {boss:'siren',seed,...(r.skipWaves===true?{skipWaves:true}:{}),...(bots?{partyBots:bots}:{}),...(ctx.debug===true?{debug:true}:{}),
  ...(ctx.relay===true?{relay:true}:{}),...(ctx.local===true?{local:true}:{})};
}
/** Applied by the Simulation constructor when options.pve is set (edit map #5). */
export const PVE_FORCED_OPTIONS:Partial<Options>={map:'siren',gameMode:'teams',friendlyFire:false,bothDraft:false,
 skipBotDrafts:true,randomMapEachSet:false,randomStage:false,sandbox:false};

export const PVE_TEAMS={party:'blue',boss:'purple'} as const satisfies Record<string,TeamId>;
export const HORDE_ID='pve-horde', HORDE_NAME='Siren minion', BOSS_PLAYER_ID='pve-boss', RECRUIT_PREFIX='recruit-',
 PARTY_BOT_PREFIX='party-bot-', ADD_PREFIX='add-';
/** Add ids are ADD_PREFIX+n. teamRef() is consulted only after players.get(id) misses, so player ids never collide. */
export const PVE_GROUPS={player:(0x0002<<16)|0xFFFB, add:(0x0004<<16)|0x0001} as const;
/** Shared timings: S1 (authority), S5 (poses) and S6 (lids, leaves, pods) all read these. */
export const PVE_TIMING={swipeHit:.34, lungeHit:.34, smashHit:.43, corpse:3.1,
 vent:{push:.35, open:.45, climb:1.95, dive:.9}, door:{open:.6, walk:1.2}, cell:{open:.5, walk:1.0},
 recruitStep:.6} as const;

export type AddKind='shambler'|'runner'|'brute';
export type AddAttack='swipe'|'lunge'|'smash';
export type AddState='entering'|'active'|'crawl'|'stagger'|'vent'|'dead';
export type EntryKind='vent'|'door'|'cell';
export const LIMB={head:1,lArm:2,rArm:4,lFore:8,rFore:16,lLeg:32,rLeg:64,lShin:128,rShin:256} as const;
export type LimbName=keyof typeof LIMB;
export type OrdinaryRarity=Extract<Rarity,'common'|'uncommon'|'rare'|'epic'|'legendary'|'mythic'>;
export type DropTier=OrdinaryRarity|'rainbow';
export type MachineKind='chip'|'recruit'|'med';
export type RecruitRole='dps'|'tank'|'medic';
export type PveMemberKind='human'|'bot';
export type PickSource='start'|'dispenser'|'wave';
export type PveStage='opening'|'wave'|'intermission'|'boss'|'ended';
export type CinematicKind='reveal'|'boss-intro'|'outro';
export type PveOutcome='victory'|'defeat';
/** Codec orders (flag values). Never reorder: the wire carries the index. */
export const ADD_KINDS_ORDER:readonly AddKind[]=['shambler','runner','brute'];
export const ADD_STATES_ORDER:readonly AddState[]=['entering','active','crawl','stagger','vent','dead'];
export const ADD_ATTACKS_ORDER:readonly AddAttack[]=['swipe','lunge','smash'];

export interface AddHitInfo{point?:Vec; kind?:'bullet'|'cut'|'pierce'|'blunt'|'blast'|'energy'|'field'; radius?:number; headshot?:boolean; melee?:boolean}
export interface PveCinematic{kind:CinematicKind; at:number; until:number; chip?:1|2|3|4; smileUntil?:number; outcome?:PveOutcome}
export interface PveBossView{id:string; phase:1|2|3|4; floor:number; hidden?:boolean; possessed?:number; possessedAt?:number; foundAt?:number}
export interface PvePick{id:string; tier:DropTier; source:PickSource; at:number; offers?:string[] /* present once opened (D17) */}
export interface PveChannel{kind:'revive'; target:string; at:number; until:number}
export interface PvePartyView{kind:PveMemberKind; connected:boolean; money:number; earned:number; spent:number; kills:number;
 assists:number; heads:number; addDamage:number; bossDamage:number; revives:number; revived:number; deaths:number;
 chipsBought:number; recruitsHired:number; picks:PvePick[]; open?:string; openAt?:number; hurtAt?:number;
 channel?:PveChannel; downAt?:number; selfRevives:number; selfReviveAt?:number; reviveCost:number; chipPrice:number;
 healAt?:number; healUntil?:number; healReadyAt:number}
export interface PveMachineUse{m:number; at:number; by:string; result:MachineKind|'deny'}
export interface PvePickupView{id:string; owner:string; tier:DropTier; pos:Vec; born:number; m:number}
export interface PveRecruitView{id:string; owner:string; role:RecruitRole; at:number /* creation; step-out over PVE_TIMING.recruitStep */; m:number; tauntUntil?:number}
export interface PveEntryView{e:number; openAt:number; closeAt:number; mode:'spawn'|'dive'}
export type PveAddTuple=[n:number,x:number,y:number,z:number,yaw:number,vx:number,vz:number,hp:number,flags:number,
 spawnAt:number,attackAt:number,hitAt:number,hitYaw:number,deathAt:number,diveAt:number];
export interface PveAddView{n:number; id:string; pos:Vec; yaw:number; vel:{x:number;z:number}; hp:number /*0..1*/;
 kind:AddKind; state:AddState; attack:AddAttack; side:1|-1; lost:number; entry:number /*-1 none*/; headless:boolean;
 target?:string; spawnAt?:number; attackAt?:number; hitAt?:number; hitYaw?:number; deathAt?:number; diveAt?:number}

const r2=(n:number)=>Math.round((Number.isFinite(n)?n:0)*100)/100, r1=(n:number)=>Math.round((Number.isFinite(n)?n:0)*10)/10;
const idx=<T>(list:readonly T[],value:T)=>Math.max(0,list.indexOf(value));
/** flags bits: 0-8 lost (LIMB), 9-10 kind, 11-13 state, 14-15 attack, 16 side (1 = left, side -1), 17-21 entry+1,
 *  22 headless, 23-25 the target's slot+1 in `slots` (§3.7). Optional times encode as 0 when absent. */
export function encodeAdd(a:PveAddView,slots:readonly string[]):PveAddTuple{
 const slot=a.target===undefined?-1:slots.indexOf(a.target),entry=Math.max(-1,Math.min(30,Math.floor(a.entry)));
 const flags=(a.lost&0x1FF)|idx(ADD_KINDS_ORDER,a.kind)<<9|idx(ADD_STATES_ORDER,a.state)<<11|idx(ADD_ATTACKS_ORDER,a.attack)<<14|
  (a.side===-1?1:0)<<16|(entry+1)<<17|(a.headless?1:0)<<22|(slot>=0&&slot<7?slot+1:0)<<23;
 return [a.n,r2(a.pos.x),r2(a.pos.y),r2(a.pos.z),r2(a.yaw),r1(a.vel.x),r1(a.vel.z),Math.max(0,Math.min(100,Math.round(a.hp*100))),flags,
  r2(a.spawnAt??0),r2(a.attackAt??0),r2(a.hitAt??0),r1(a.hitYaw??0),r2(a.deathAt??0),r2(a.diveAt??0)];
}
export function decodeAdd(t:PveAddTuple,slots:readonly string[]):PveAddView{
 const f=t[8],slot=(f>>>23&7)-1,opt=(n:number)=>n?n:undefined;
 const view:PveAddView={n:t[0],id:ADD_PREFIX+t[0],pos:{x:t[1],y:t[2],z:t[3]},yaw:t[4],vel:{x:t[5],z:t[6]},hp:t[7]/100,
  kind:ADD_KINDS_ORDER[f>>>9&3]??'shambler',state:ADD_STATES_ORDER[f>>>11&7]??'active',attack:ADD_ATTACKS_ORDER[f>>>14&3]??'swipe',
  side:(f>>>16&1)?-1:1,lost:f&0x1FF,entry:(f>>>17&31)-1,headless:!!(f>>>22&1)};
 if(slot>=0&&slots[slot]!==undefined)view.target=slots[slot];
 const spawnAt=opt(t[9]),attackAt=opt(t[10]),hitAt=opt(t[11]),deathAt=opt(t[13]),diveAt=opt(t[14]);
 if(spawnAt!==undefined)view.spawnAt=spawnAt;if(attackAt!==undefined)view.attackAt=attackAt;if(hitAt!==undefined){view.hitAt=hitAt;view.hitYaw=t[12];}
 if(deathAt!==undefined)view.deathAt=deathAt;if(diveAt!==undefined)view.diveAt=diveAt;
 return view;
}

export interface PveView{v:1; boss:PveBossId; stage:PveStage; stageAt:number; stageUntil?:number; wave:number; waves:number;
 waveTotal:number; waveLeft:number; hold:boolean; cinematic?:PveCinematic; track:{revealed:0|1|2|3|4; at:number};
 bossView?:PveBossView; slots:string[]; helloed:string[] /* player ids that sent a hello (lobby badge) */; adds:PveAddTuple[];
 entries:PveEntryView[]; machines:PveMachineUse[]; pickups:PvePickupView[]; party:Record<string,PvePartyView>;
 recruits:PveRecruitView[]; outcome?:PveOutcome; endedAt?:number; startedAt?:number /* fight start (run clock) */}
export const pveOf=(state:object):PveView|undefined=>(state as {pve?:PveView}).pve;
/** Lerp adds present in a and b by n (pos by f, plus vel·extra; yaw by shortest angle); everything else from current. */
export function interpolatePve(a:PveView|undefined,b:PveView|undefined,current:PveView|undefined,f:number,extra:number):PveView|undefined{
 if(!current||!a||!b)return current;
 const from=new Map(a.adds.map(t=>[t[0],t])),to=new Map(b.adds.map(t=>[t[0],t]));
 const adds=current.adds.map(t=>{const x=from.get(t[0]),y=to.get(t[0]);if(!x||!y)return t;
  const out=[...t] as PveAddTuple;for(const k of [1,2,3] as const)out[k]=x[k]+(y[k]-x[k])*f;out[1]+=y[5]*extra;out[3]+=y[6]*extra;
  out[4]=x[4]+Math.atan2(Math.sin(y[4]-x[4]),Math.cos(y[4]-x[4]))*f;return out;});
 return {...current,adds};
}

export type PveCommand=
 |{action:'hello';v:number;key?:string;n?:number}
 |{action:'open';pick?:string;n?:number}
 |{action:'close';n?:number}
 |{action:'pick';pick:string;chip:string;n?:number}
 |{action:'prefs';autoOpen:boolean;n?:number}
 |{action:'debug';op:string;n?:number};
const shortString=(s:unknown,max:number):s is string=>typeof s==='string'&&s.length>0&&s.length<=max;
/** key /^[0-9a-f]{32,64}$/; op ≤ 40 chars; pick and chip ≤ 64 chars; n a safe integer. Returns a fresh object with only
 *  the known fields, or null. */
export function sanitizePveCommand(raw:unknown):PveCommand|null{
 if(!raw||typeof raw!=='object'||Array.isArray(raw))return null;const r=raw as Record<string,unknown>;
 if(r.n!==undefined&&!Number.isSafeInteger(r.n))return null;const n=r.n===undefined?{}:{n:r.n as number};
 switch(r.action){
  case 'hello':{if(!Number.isSafeInteger(r.v))return null;if(r.key!==undefined&&(typeof r.key!=='string'||!/^[0-9a-f]{32,64}$/.test(r.key)))return null;
   return {action:'hello',v:r.v as number,...(r.key!==undefined?{key:r.key as string}:{}),...n};}
  case 'open':{if(r.pick!==undefined&&!shortString(r.pick,64))return null;return {action:'open',...(r.pick!==undefined?{pick:r.pick as string}:{}),...n};}
  case 'close':return {action:'close',...n};
  case 'pick':{if(!shortString(r.pick,64)||!shortString(r.chip,64))return null;return {action:'pick',pick:r.pick,chip:r.chip,...n};}
  case 'prefs':{if(typeof r.autoOpen!=='boolean')return null;return {action:'prefs',autoOpen:r.autoOpen,...n};}
  case 'debug':{if(!shortString(r.op,40))return null;return {action:'debug',op:r.op,...n};}
  default:return null;
 }
}
/** Wire form on every transport (D32): {type:'chip', pve:PveCommand}. 'chip' is already a relay guest type. */
export const PVE_WIRE='chip' as const;
export const isPveWire=(msg:unknown):msg is {type:'chip';pve:object}=>
 !!msg&&typeof msg==='object'&&(msg as {type?:unknown}).type===PVE_WIRE&&typeof (msg as {pve?:unknown}).pve==='object'&&(msg as {pve?:unknown}).pve!==null;
export const PVE_EVENT={kill:'pve-kill',gib:'pve-gib',cash:'pve-cash',deny:'pve-deny',buy:'pve-buy',grab:'pve-grab',
 revive:'pve-revive',respawn:'pve-respawn',rejoin:'pve-rejoin',recruit:'pve-recruit',possess:'pve-possess',
 found:'pve-found',wave:'pve-wave',phase:'pve-phase'} as const;

export interface HordeModifiers{hp:number; damage:number; speed:number; attackRate:number; reward:number; count:number}
export interface HordeFlow{rate:number; maxAlive:number; mix:Partial<Record<AddKind,number>>; budget:number /* −1 = endless */}
export interface AddRef{n:number; id:string; pos:Vec; kind:AddKind; state:AddState; hp:number; maxHp:number; target?:string; attackAt?:number}
export interface AddDeath{n:number; kind:AddKind; pos:Vec; killer:string|null; assists:string[]; headless:boolean; possessed:boolean; cause:'damage'|'head'|'burn'|'fall'}
export interface PveStats{addDamage:number; bossDamage:number; kills:number; assists:number; heads:number; revives:number; revived:number; deaths:number}
export type NavGrid='party'|'boss';
export interface PveNav{ready():boolean; direction(from:Vec,targetId:string,grid?:NavGrid):{x:number;z:number}|null;
 distance(from:Vec,targetId:string,grid?:NavGrid):number; clear(a:Vec,b:Vec,grid?:NavGrid):boolean; dirty():void}

/** What every part may use. Implemented by PveSystem (system.ts). */
export interface PveHost{
 readonly sim:Simulation; readonly options:PveOptions; readonly runSeed:number; readonly runIndex:number;
 readonly horde:HordeApi; readonly director:DirectorApi; readonly economy:EconomyApi; readonly recruits:RecruitsApi;
 rng(label:'horde'|'director'|'economy'|'recruits'):{next():number};
 humans():Player[]; partyBots():Player[]; members():Player[] /* humans + party bots */; party():Player[] /* + recruits */;
 boss():Player|undefined; partySize():number /* h */; slots():readonly string[];
 spawningBoss:boolean;
 /** Exactly one connected human and no party bots at fight start (solo self-revive, D7c). */
 readonly solo:boolean;
 isHuman(p:Player):boolean; isPartyBot(p:Player):boolean; isRecruit(p:Player):boolean; isBoss(p:Player):boolean;
 stats(id:string):PveStats; emit(type:string,pos:Vec,extra?:object):void;
 setHidden(p:Player,hidden:boolean):void; hidden(p:Player):boolean;
 setUntouchable(id:string,until:number):void;
 /** Hidden, revive-shielded (setUntouchable) or debug god: adds, recruits and the boss should not target or hit them. */
 untouchable(p:Player):boolean;
 standAt(x:number,z:number,p?:Player):Vec;   // standing capsule centre over (x,z) for p's collider (default: player size)
 notifyHold(active:boolean):void;           // director only
 finish(outcome:PveOutcome):void;            // director only
 onAddKilled(d:AddDeath):void;               // horde only; fans out to economy, ledger, director
}
export interface HordeApi{
 readonly nav:PveNav;
 alive():number; queued():number; adds():AddRef[]; get(n:number):AddRef|undefined;
 setFlow(flow:HordeFlow|null):void; spawn(kind:AddKind,opts?:{entry?:number;possessed?:boolean}):number|null;
 possess():number|null; possessed():number|undefined;
 hit(m:Minion,damage:number,attacker:string,impulse:number,dir:Vec,hit?:AddHitInfo):void;
 assistMinion(m:Minion,owner:Player,dt:number):void;
 step(dt:number):void; freeze():void; onHold(active:boolean):void; shift(dt:number):void;
 killAll(cause:'boss-dead'|'defeat'):void; rekey(from:string,to:string):void;
 views(slots:readonly string[]):{adds:PveAddTuple[];entries:PveEntryView[]};
 reset():void; debug(op:string):boolean;
}
export interface DirectorApi{
 onFightStart():void; step(dt:number):void; stepHold(dt:number):void; holding():boolean;
 hpFloor(p:Player):number|undefined; bossInput(p:Player,base:Input):Input; modifiers():HordeModifiers;
 onAddKilled(d:AddDeath):void; onDeath(p:Player):void; onDamage(p:Player,actual:number,source:string|null):void;
 view():Pick<PveView,'stage'|'stageAt'|'stageUntil'|'wave'|'waves'|'waveTotal'|'waveLeft'|'hold'|'cinematic'|'track'|'bossView'|'outcome'|'endedAt'|'startedAt'>;
 reset():void; debug(op:string):boolean;
}
export interface EconomyApi{
 onFightStart():void; onAddKilled(d:AddDeath):void; onBossDamage(source:string,amount:number,maxHp:number):void;
 grant(id:string,amount:number,reason:string):void; grantAll(amount:number,reason:string):void; money(id:string):number;
 grantPick(id:string,tier:DropTier,source:PickSource):string|null; command(id:string,cmd:PveCommand):boolean;
 /** Forced input for a human (choosing, channelling); `base` is the player's input after the queue pop (edge detection, e.g. interact release). */
 inputFor(p:Player,base?:Input):Input|undefined; choosing(p:Player):boolean; weaponAway(p:Player):boolean;
 onDamage(p:Player,actual:number,source:string|null):void; onDeath(p:Player):void; onRevive(p:Player):void;
 respawnAll(reason:'wave'|'phase'):void; pendingRevive():boolean; pendingHires():number;
 /** Hires paid by `owner` that have not popped out yet (S7 from S3; optional so fakes stay valid). */ pendingHiresOf?(owner:string):number;
 step(dt:number):void; onHold(active:boolean):void; shift(dt:number):void; rekey(from:string,to:string):void;
 partyView(id:string):PvePartyView; machinesView():PveMachineUse[]; pickupsView():PvePickupView[];
 reset():void; debug(op:string):boolean;
}
export interface RecruitsApi{
 canHire(owner:string):'ok'|'seats'|'limit'; hire(owner:string,at:Vec,yaw:number):string|null;
 /** Recruits by role; party bots through partyBotInput. */
 inputFor(p:Player,base?:Input):Input|undefined; weaponAway(p:Player):boolean; isRecruit(id:string):boolean; tauntNear(pos:Vec):boolean;
 /** The id of the taunting tank nearest pos within RECRUIT.tauntRadius, or null (adds use it for the −6 m score). */
 tauntTarget(pos:Vec):string|null;
 /** The newest living-or-dying recruit id (hire order), or null. */
 newest():string|null;
 /** Removes recruit `id` now (sim.removePlayer) and returns its hirer, or null if unknown. The caller refunds (S2's boss-seat fallback: economy.grant(owner,RECRUIT.price,'refund')). */
 dismiss(id:string):{owner:string}|null;
 step(dt:number):void; onDeath(p:Player):void; onHold(active:boolean):void; shift(dt:number):void; rekey(from:string,to:string):void;
 views():PveRecruitView[]; reset():void;
}
/** Test seam: PveSystem.testParts (static). Never stored in Simulation.options (options are sent in snapshots). */
export interface PvePartsFactory{horde?:(h:PveHost)=>HordeApi; director?:(h:PveHost)=>DirectorApi;
 economy?:(h:PveHost)=>EconomyApi; recruits?:(h:PveHost)=>RecruitsApi}
