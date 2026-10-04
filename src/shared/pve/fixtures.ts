// Seeded PveView fixtures (design §2.1, §7.8). Presentation streams (S4 HUD and screens, S5 horde, S6 world), their labs
// and the app's DEV fixture injection build views from here, so nothing waits for the authority streams S1-S3.
// Pure and deterministic: the same (name, time, options) always gives the same view.
import type {PlayerView} from '../simulation.js';
import {ADD_PREFIX,BOSS_PLAYER_ID,LIMB,PARTY_BOT_PREFIX,PVE_TIMING,RECRUIT_PREFIX,encodeAdd,
 type AddAttack,type AddKind,type AddState,type DropTier,type PveAddTuple,type PveAddView,type PveBossView,type PveCinematic,
 type PveEntryView,type PveMachineUse,type PvePartyView,type PvePickupView,type PveRecruitView,type PveView} from './contract.js';
import {ADD_KINDS,PVE,PVE_SIREN,bossHp,chipPrice,revivePrice} from './balance.js';
import {PVE_ENTRIES,PVE_LAYOUT,PVE_MACHINES} from './layout.js';
import {PveRng} from './rng.js';

export interface FixtureOptions{
 /** Sim time the fixture is built for (default 100). Times inside the view are relative to it. */ time?:number;
 /** Seed for every random choice (default 1). */ seed?:number;
 /** Party player ids in slot order (default ['p1','p2','party-bot-1','p3']). The first id is "me" for party rows. */ ids?:string[];
 /** Party size used for boss HP (default ids.length). */ h?:number;
}
const DEFAULT_IDS=['p1','p2',PARTY_BOT_PREFIX+'1','p3'];
const opt=(o:FixtureOptions={})=>{const ids=o.ids?.length?o.ids:DEFAULT_IDS;return {time:o.time??100,seed:o.seed??1,ids,h:o.h??ids.length};};

// ---- adds ----
export interface FixtureAddSpec{n?:number;kind?:AddKind;state?:AddState;attack?:AddAttack;x?:number;z?:number;y?:number;yaw?:number;
 hp?:number;lost?:number;entry?:number;headless?:boolean;target?:string;side?:1|-1;vel?:{x:number;z:number};
 spawnAt?:number;attackAt?:number;hitAt?:number;hitYaw?:number;deathAt?:number;diveAt?:number}
/** One add view with sensible defaults (an active shambler at the origin facing north). */
export function fixtureAdd(s:FixtureAddSpec):PveAddView{
 const kind=s.kind??'shambler',n=s.n??1;
 return {n,id:ADD_PREFIX+n,pos:{x:s.x??0,y:s.y??(s.state==='crawl'?.45:.86),z:s.z??0},yaw:s.yaw??0,vel:s.vel??{x:0,z:0},hp:s.hp??1,kind,
  state:s.state??'active',attack:s.attack??ADD_KINDS[kind].attack,side:s.side??1,lost:s.lost??0,entry:s.entry??-1,headless:s.headless??false,
  target:s.target,spawnAt:s.spawnAt,attackAt:s.attackAt,hitAt:s.hitAt,hitYaw:s.hitYaw,deathAt:s.deathAt,diveAt:s.diveAt};
}
const LIMB_PATTERNS=[0,LIMB.lFore,LIMB.rArm,LIMB.lShin,LIMB.lLeg|LIMB.rLeg,LIMB.lArm|LIMB.rArm,LIMB.rFore|LIMB.lShin];
/** A ring of n adds around (cx,cz), facing the centre, seeded kinds, limb loss and attack phases. States cycle through
 *  `states` (default all active). Windups target `slots` in turn, so the attacked-blink path has data. */
export function fixtureHordeRing(n:number,o:FixtureOptions&{cx?:number;cz?:number;radius?:number;states?:AddState[];kinds?:AddKind[];slots?:string[];gore?:boolean}={}):PveAddView[]{
 const {time,seed,ids}=opt(o),rng=new PveRng(seed),slots=o.slots??ids,states=o.states??['active'],kinds=o.kinds??['shambler','shambler','runner','shambler','brute'];
 const out:PveAddView[]=[];
 for(let i=0;i<n;i++){
  const a=i/n*Math.PI*2,r=(o.radius??6)+rng.next()*1.5,x=(o.cx??0)+Math.sin(a)*r,z=(o.cz??0)+Math.cos(a)*r,state=states[i%states.length],kind=kinds[rng.int(kinds.length)];
  const winding=state==='active'&&i%3===0,lost=o.gore===false?0:LIMB_PATTERNS[rng.int(LIMB_PATTERNS.length)];
  out.push(fixtureAdd({n:i+1,kind,state:lost&(LIMB.lLeg|LIMB.rLeg)&&state==='active'?'crawl':state,x,z,yaw:Math.atan2(-(x-(o.cx??0)),-(z-(o.cz??0))),
   hp:state==='dead'?0:.25+rng.next()*.75,lost,side:i%2?-1:1,target:slots.length?slots[i%slots.length]:undefined,vel:{x:-Math.sin(a)*1.5,z:-Math.cos(a)*1.5},
   spawnAt:time-5-rng.next()*20,attackAt:winding?time-.1:undefined,hitAt:i%4===1?time-.2:undefined,hitYaw:i%4===1?a:undefined,
   deathAt:state==='dead'?time-1:undefined,diveAt:state==='vent'?time-.4:undefined,headless:state==='dead'&&i%2===0}));
 }
 return out;
}
/** Entering adds for each entry kind at an instant of its timeline (vents climbing, doors and cells walking out). */
export function fixtureEnteringAdds(o:FixtureOptions={}):{adds:PveAddView[];entries:PveEntryView[]}{
 const {time}=opt(o),adds:PveAddView[]=[],entries:PveEntryView[]=[];
 const pick=[0,5,8,11,12,15];let n=500;
 for(const e of pick){const entry=PVE_ENTRIES[e],age=entry.kind==='vent'?1.0:entry.kind==='door'?PVE_TIMING.door.open+.6:PVE_TIMING.cell.open+.5;
  const openAt=time-age,walk=entry.kind==='door'?PVE_TIMING.door:entry.kind==='cell'?PVE_TIMING.cell:undefined;
  const k=walk?Math.max(0,Math.min(1,(age-walk.open)/walk.walk)):0,x=entry.from.x+(entry.exit.x-entry.from.x)*k,z=entry.from.z+(entry.exit.z-entry.from.z)*k;
  entries.push({e,openAt,closeAt:openAt+(entry.kind==='vent'?PVE_TIMING.vent.climb:walk!.open+walk!.walk)+1.2,mode:'spawn'});
  adds.push(fixtureAdd({n:++n,state:'entering',entry:e,x,z,yaw:entry.yaw,spawnAt:openAt}));}
 // One add diving into a vent (vent travel).
 entries.push({e:3,openAt:time-.3,closeAt:time+2,mode:'dive'});adds.push(fixtureAdd({n:++n,state:'vent',entry:3,x:PVE_ENTRIES[3].from.x,z:PVE_ENTRIES[3].from.z,diveAt:time-.3}));
 return {adds,entries};
}

// ---- party ----
export type PartyRowKind='alive'|'dead'|'choosing'|'attacked'|'self-reviving'|'bot'|'healing'|'disconnected';
export function fixturePartyRow(kind:PartyRowKind,o:FixtureOptions&{money?:number}={}):PvePartyView{
 const {time}=opt(o),bot=kind==='bot';
 const row:PvePartyView={kind:bot?'bot':'human',connected:kind!=='disconnected',money:bot?0:o.money??1250,earned:bot?0:1630,spent:bot?0:380,kills:23,assists:7,heads:5,
  addDamage:1840,bossDamage:0,revives:1,revived:0,deaths:kind==='dead'||kind==='self-reviving'?1:0,chipsBought:bot?0:2,recruitsHired:bot?0:1,
  picks:[{id:'pick-1',tier:'common',source:'start',at:time-30},{id:'pick-2',tier:'epic',source:'dispenser',at:time-4}],
  selfRevives:kind==='self-reviving'?1:0,reviveCost:revivePrice(0),chipPrice:chipPrice(2),healReadyAt:0};
 if(kind==='choosing'||kind==='attacked'){row.open='pick-2';row.openAt=time-2;row.picks[1].offers=['vital','ricochet','piercing','splitter','vacuum','pong'];}
 if(kind==='attacked')row.hurtAt=time-.3;
 if(kind==='dead'||kind==='self-reviving')row.downAt=time-3;
 if(kind==='self-reviving')row.selfReviveAt=time+2;
 if(kind==='healing'){row.healAt=time-.5;row.healUntil=time+1.5;row.healReadyAt=time+11.5;}
 if(bot){row.picks=[];}
 return row;
}
/** Party rows for ids (default p1 alive and choosing-capable, p2 dead, party bot, p3 attacked while choosing). */
export function fixtureParty(o:FixtureOptions&{kinds?:PartyRowKind[]}={}):Record<string,PvePartyView>{
 const {ids}=opt(o),kinds=o.kinds??ids.map(id=>id.startsWith(PARTY_BOT_PREFIX)?'bot':'alive');const out:Record<string,PvePartyView>={};
 ids.forEach((id,i)=>{out[id]=fixturePartyRow(kinds[i]??'alive',o);});return out;
}

// ---- boss, cinematics, world ----
export function fixtureBossView(phase:1|2|3|4,o:FixtureOptions&{hidden?:boolean;possessed?:number;found?:boolean}={}):PveBossView{
 const {time,h}=opt(o),max=bossHp(h),floor=phase<4?PVE_SIREN.thresholds[phase-1]*max:0;
 const b:PveBossView={id:BOSS_PLAYER_ID,phase,floor};
 if(o.hidden||phase===2&&o.found!==true){b.hidden=true;b.possessed=o.possessed??7;b.possessedAt=time-20;}
 if(o.found){b.foundAt=time-1;}
 return b;
}
/** Boss HP a fixture boss should show for the phase (just above the floor, or 40% of the phase band). */
export function fixtureBossHp(phase:1|2|3|4,h=1){const max=bossHp(h),t=[1,...PVE_SIREN.thresholds,0];return Math.round(max*(t[phase]+(t[phase-1]-t[phase])*.4));}
export function fixtureCinematic(kind:PveCinematic['kind'],o:FixtureOptions&{chip?:1|2|3|4;outcome?:'victory'|'defeat';age?:number}={}):PveCinematic{
 const {time}=opt(o),age=o.age??1;const at=time-age;
 if(kind==='reveal')return {kind,at,until:at+PVE.reveal.smile+PVE.reveal.card+PVE.reveal.settle,chip:o.chip??1,smileUntil:at+PVE.reveal.smile};
 if(kind==='boss-intro')return {kind,at,until:at+7.27+PVE.bossIntroExtra};
 return {kind,at,until:at+(o.outcome==='defeat'?PVE.outroDefeatSeconds:PVE.outroVictorySeconds),outcome:o.outcome??'victory'};
}
export function fixtureMachines(o:FixtureOptions={}):PveMachineUse[]{const {time,ids}=opt(o);
 return [{m:0,at:time-.3,by:ids[0],result:'chip'},{m:4,at:time-.5,by:ids[0],result:'recruit'},{m:6,at:time-1,by:ids[1]??ids[0],result:'med'},{m:1,at:time-.2,by:ids[0],result:'deny'}];}
const TIERS:DropTier[]=['common','uncommon','rare','epic','legendary','mythic','rainbow'];
/** One pickup of every drop tier in front of the chip machines (the first still popping out). */
export function fixturePickups(o:FixtureOptions={}):PvePickupView[]{const {time,ids}=opt(o);
 return TIERS.map((tier,i)=>{const m=PVE_MACHINES[i%4],dx=(i>>2)*.6;return {id:'pk'+(i+1),owner:ids[i%ids.length],tier,pos:{x:m.chipDrop.x+dx,y:.9,z:m.chipDrop.z},born:i===0?time+.3:time-2,m:m.m};});}
export function fixtureRecruits(o:FixtureOptions={}):PveRecruitView[]{const {time,ids}=opt(o);
 return [{id:RECRUIT_PREFIX+'1',owner:ids[0],role:'dps',at:time-.3,m:4},{id:RECRUIT_PREFIX+'2',owner:ids[0],role:'tank',at:time-20,m:4,tauntUntil:time+.6},{id:RECRUIT_PREFIX+'3',owner:ids[1]??ids[0],role:'medic',at:time-40,m:5}];}

// ---- whole views ----
export function fixtureBase(o:FixtureOptions={}):PveView{
 const {time,ids}=opt(o);
 return {v:1,boss:'siren',stage:'wave',stageAt:time-20,startedAt:time-95,wave:1,waves:PVE.wavesBeforeBoss,waveTotal:14,waveLeft:9,hold:false,track:{revealed:0,at:0},
  slots:[...ids],helloed:ids.filter(id=>!id.startsWith(PARTY_BOT_PREFIX)),adds:[],entries:[],machines:[],pickups:[],party:fixtureParty(o),recruits:[]};
}
const enc=(adds:PveAddView[],slots:readonly string[]):PveAddTuple[]=>adds.map(a=>encodeAdd(a,slots));
/** Every named fixture. Each builder is pure in (options). */
export const PVE_FIXTURES={
 'lobby':(o?:FixtureOptions)=>({...fixtureBase(o),stage:'opening' as const,stageAt:0,wave:0,waveTotal:0,waveLeft:0}),
 'opening':(o?:FixtureOptions)=>{const v=fixtureBase(o),{time,ids}=opt(o);return {...v,stage:'opening' as const,stageAt:time-2,stageUntil:time+8,wave:0,waveTotal:0,waveLeft:0,
  party:fixtureParty({...o,kinds:ids.map((id,i)=>id.startsWith(PARTY_BOT_PREFIX)?'bot':i===0?'choosing':'alive')})};},
 'wave':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,adds:enc(fixtureHordeRing(12,{...o,radius:7}),v.slots)};},
 'intermission':(o?:FixtureOptions)=>{const v=fixtureBase(o),{time}=opt(o);return {...v,stage:'intermission' as const,stageAt:time-3,stageUntil:time+27,wave:1,waveLeft:0};},
 'horde-ring':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,adds:enc(fixtureHordeRing(24,{...o,radius:6,states:['active','active','stagger','crawl']}),v.slots)};},
 'horde-crowd':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,waveTotal:62,waveLeft:60,adds:enc([...fixtureHordeRing(20,{...o,radius:5}),...fixtureHordeRing(20,{...o,radius:12,seed:(o?.seed??1)+1}).map(a=>({...a,n:a.n+20,id:ADD_PREFIX+(a.n+20)})),...fixtureHordeRing(20,{...o,radius:22,seed:(o?.seed??1)+2}).map(a=>({...a,n:a.n+40,id:ADD_PREFIX+(a.n+40)}))],v.slots)};},
 'corpses':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,adds:enc(fixtureHordeRing(16,{...o,radius:5,states:['dead']}),v.slots)};},
 'entries':(o?:FixtureOptions)=>{const v=fixtureBase(o),e=fixtureEnteringAdds(o);return {...v,adds:enc(e.adds,v.slots),entries:e.entries};},
 'party':(o?:FixtureOptions)=>{const v=fixtureBase(o),{ids}=opt(o);const kinds:PartyRowKind[]=['alive','dead','bot','attacked'];return {...v,party:fixtureParty({...o,kinds:ids.map((id,i)=>id.startsWith(PARTY_BOT_PREFIX)?'bot':kinds[i]??'alive')})};},
 'choosing':(o?:FixtureOptions)=>{const v=fixtureBase(o),{ids}=opt(o);return {...v,party:fixtureParty({...o,kinds:ids.map((id,i)=>id.startsWith(PARTY_BOT_PREFIX)?'bot':i===0?'choosing':'alive')})};},
 'attacked':(o?:FixtureOptions)=>{const v=fixtureBase(o),{ids}=opt(o);return {...v,adds:enc(fixtureHordeRing(6,{...o,radius:2,slots:[ids[0]]}),v.slots),party:fixtureParty({...o,kinds:ids.map((id,i)=>id.startsWith(PARTY_BOT_PREFIX)?'bot':i===0?'attacked':'alive')})};},
 'dead':(o?:FixtureOptions)=>{const v=fixtureBase(o),{ids}=opt(o);return {...v,party:fixtureParty({...o,kinds:ids.map((id,i)=>id.startsWith(PARTY_BOT_PREFIX)?'bot':i===0?'dead':'alive')})};},
 'self-revive':(o?:FixtureOptions)=>{const v=fixtureBase({...o,ids:[opt(o).ids[0]]});return {...v,party:fixtureParty({...o,ids:[opt(o).ids[0]],kinds:['self-reviving']})};},
 'economy':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,machines:fixtureMachines(o),pickups:fixturePickups(o),recruits:fixtureRecruits(o)};},
 'reveal-1':(o?:FixtureOptions)=>fixtureRevealView(1,o),
 'reveal-2':(o?:FixtureOptions)=>fixtureRevealView(2,o),
 'reveal-3':(o?:FixtureOptions)=>fixtureRevealView(3,o),
 'reveal-4':(o?:FixtureOptions)=>fixtureRevealView(4,o),
 'boss-intro':(o?:FixtureOptions)=>{const v=fixtureBase(o);return {...v,stage:'wave' as const,wave:3,waveLeft:0,hold:true,cinematic:fixtureCinematic('boss-intro',o),track:{revealed:1 as const,at:opt(o).time-4}};},
 'boss-1':(o?:FixtureOptions)=>fixtureBossStage(1,o),
 'boss-2':(o?:FixtureOptions)=>fixtureBossStage(2,o),
 'boss-3':(o?:FixtureOptions)=>fixtureBossStage(3,o),
 'boss-4':(o?:FixtureOptions)=>fixtureBossStage(4,o),
 'possession':(o?:FixtureOptions)=>{const v=fixtureBossStage(2,o),adds=fixtureHordeRing(10,{...o,radius:12});adds[6]={...adds[6],state:'active',lost:0};
  return {...v,adds:enc(adds,v.slots),bossView:fixtureBossView(2,{...o,possessed:adds[6].n})};},
 'found':(o?:FixtureOptions)=>{const v=fixtureBossStage(3,o);return {...v,bossView:fixtureBossView(3,{...o,found:true})};},
 'outro-victory':(o?:FixtureOptions)=>{const v=fixtureBossStage(4,o);return {...v,hold:true,adds:[],cinematic:fixtureCinematic('outro',{...o,outcome:'victory'})};},
 'outro-defeat':(o?:FixtureOptions)=>{const v=fixtureBase(o),{ids}=opt(o);return {...v,hold:true,cinematic:fixtureCinematic('outro',{...o,outcome:'defeat'}),party:fixtureParty({...o,kinds:ids.map(id=>id.startsWith(PARTY_BOT_PREFIX)?'bot':'dead')})};},
 'results-victory':(o?:FixtureOptions)=>fixtureResults('victory',o),
 'results-defeat':(o?:FixtureOptions)=>fixtureResults('defeat',o),
} satisfies Record<string,(o?:FixtureOptions)=>PveView>;
export type PveFixtureName=keyof typeof PVE_FIXTURES;
export const PVE_FIXTURE_NAMES=Object.keys(PVE_FIXTURES) as PveFixtureName[];
/** Builds a named fixture. */
export function pveFixture(name:PveFixtureName,o?:FixtureOptions):PveView{return PVE_FIXTURES[name](o);}

export function fixtureRevealView(chip:1|2|3|4,o?:FixtureOptions):PveView{
 const v=chip===1?fixtureBase(o):fixtureBossStage(Math.max(1,chip-1) as 1|2|3,o),{time}=opt(o);
 return {...v,hold:true,cinematic:fixtureCinematic('reveal',{...o,chip,age:2}),track:{revealed:chip,at:time-.6}};
}
export function fixtureBossStage(phase:1|2|3|4,o?:FixtureOptions):PveView{
 const v=fixtureBase(o),{time}=opt(o);
 return {...v,stage:'boss',stageAt:time-30,wave:PVE.wavesBeforeBoss,waveTotal:0,waveLeft:0,track:{revealed:phase,at:time-25},bossView:fixtureBossView(phase,o),
  adds:enc(fixtureHordeRing(8,{...o,radius:9}),v.slots)};
}
export function fixtureResults(outcome:'victory'|'defeat',o?:FixtureOptions):PveView{
 const v=outcome==='victory'?fixtureBossStage(4,o):fixtureBase(o),{time,ids}=opt(o),party=fixtureParty(o);
 ids.forEach((id,i)=>{party[id]={...party[id],bossDamage:outcome==='victory'?[4200,2650,1810,1340][i%4]:0,kills:[41,33,27,19][i%4],picks:[]};});
 return {...v,stage:'ended',stageAt:time-4,hold:false,adds:[],cinematic:undefined,outcome,endedAt:time-4,party};
}
/** A boss PlayerView for fixtures, cloned from any real PlayerView (for example the local player's) so every required
 *  field exists. Phase 3 and 4 carry the grown boost. */
export function fixtureBossPlayer(template:PlayerView,phase:1|2|3|4,o?:FixtureOptions):PlayerView{
 const {time,h}=opt(o),b=PVE_LAYOUT.bossSpawn,max=bossHp(h);
 const boost=phase>=3?{scale:PVE_SIREN.beautiful.scale,reach:PVE_SIREN.beautiful.reach,cooldown:phase===4?0:1,at:time-10}:undefined;
 return {...template,id:BOSS_PLAYER_ID,name:'The Siren',spec:'siren',class:'melee',team:'purple',color:'#b7a8ff',bot:'hard',pos:{x:b.x,y:.9*(boost?.scale??1),z:b.z-6},yaw:b.yaw,
  hp:fixtureBossHp(phase,h),maxHp:max,death:undefined,siren:{mode:'melee',modeAt:time-5,ready:{},...(boost?{boost}:{})}} as PlayerView;
}
