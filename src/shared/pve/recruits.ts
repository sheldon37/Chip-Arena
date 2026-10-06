// PvE recruits and party-bot brains (design §4.5, §4.7; stream S3): hiring with the seat formula and caps, roles and weapons
// (ranged and melee only, D40), the stats override, per-role input (recruit-ai.ts), the medic heal ledger, the tank taunt,
// delayed removal of dead recruits, holds and re-keying. Party bots are facade seats; this part only drives them.
import {CONFIG as C,idleInput,type Input,type SpecId,type Vec} from '../config.js';
import {WEAPONS,statsFor,magazineCapacity} from '../content.js';
import {BOT_WEAPONS} from '../lobby-presentation.js';
import type {Player} from '../simulation.js';
import {PVE_EVENT,RECRUIT_PREFIX,type PveHost,type PveRecruitView,type RecruitRole,type RecruitsApi} from './contract.js';
import {RECRUIT} from './balance.js';
import {PVE_MACHINES} from './layout.js';
import {activeAdd} from './machines.js';
import {weightedPick} from './economy.js';
import {medicPulse,partyBotInput,recruitInput} from './recruit-ai.js';

/** D40: the damage-dealer pool, non-DEV ranged and melee weapons (pistol, rifle, machinegun, sword, daggers, shield, greatsword). */
export const RECRUIT_DPS_SPECS:readonly SpecId[]=BOT_WEAPONS.filter(w=>(RECRUIT.dpsClasses as readonly string[]).includes(w.class)).map(w=>w.id);
export const RECRUIT_SPEC:Record<Exclude<RecruitRole,'dps'>,SpecId>={tank:'shield',medic:'pistol'};
const ROLE_NAME:Record<RecruitRole,string>={dps:'Gunner',tank:'Tank',medic:'Medic'};
const NAMES=['Vex','Rook','Patch','Mara','Juno','Ash','Kite','Moss','Nova','Bram'] as const;
const RECRUIT_MACHINES=PVE_MACHINES.filter(m=>m.kind==='recruit');
const flat=(a:{x:number;z:number},b:{x:number;z:number})=>Math.hypot(a.x-b.x,a.z-b.z);
const idleKeep=(i:Input):Input=>({...idleInput(),seq:i.seq,yaw:i.yaw,pitch:i.pitch,...(i.camera?{camera:i.camera}:{})});

interface Recruit{id:string;owner:string;role:RecruitRole;spec:SpecId;at:number;m:number;tauntUntil?:number;diedAt?:number}
/** Economy extension used for the per-player cap (pending hires of one owner); absent on fake economies. */

export class Recruits implements RecruitsApi{
 private list:Recruit[]=[];
 private serial=0;
 private nextPulse?:number;
 constructor(readonly host:PveHost){}
 private get sim(){return this.host.sim;}
 private alive(r:Recruit){return r.diedAt===undefined&&this.sim.players.has(r.id);}
 private get(id:string){return this.list.find(r=>r.id===id);}
 private freeSeats(){return C.maxPlayers-this.sim.players.size-(this.host.boss()?0:1)-this.host.economy.pendingHires();}
 /** D10: per player 2 and total 3 (pending hires count) → 'limit'; a free seat 8 − players − (boss ? 0 : 1) − pending ≤ 0 → 'seats'. */
 canHire(owner:string):'ok'|'seats'|'limit'{
  const e=this.host.economy,pending=e.pendingHires(),mine=e.pendingHiresOf?.(owner)??0;
  if(this.list.filter(r=>r.owner===owner).length+mine>=RECRUIT.perPlayer)return 'limit';
  if(this.list.length+pending>=RECRUIT.maxTotal)return 'limit';
  if(this.freeSeats()<=0)return 'seats';
  return 'ok';
 }
 /** Creates the recruit at `at` (a standing centre) facing `yaw`. Null when no seat is left (the economy refunds). */
 hire(owner:string,at:Vec,yaw:number):string|null{
  const s=this.sim,o=s.players.get(owner);
  if(this.list.length>=RECRUIT.maxTotal||this.freeSeats()<=0)return null;
  const rng=this.host.rng('recruits'),next=()=>rng.next(),role=weightedPick(next,RECRUIT.roles as Record<RecruitRole,number>);
  const spec=role==='dps'?RECRUIT_DPS_SPECS[Math.min(RECRUIT_DPS_SPECS.length-1,Math.floor(next()*RECRUIT_DPS_SPECS.length))]:RECRUIT_SPEC[role];
  const k=this.serial+1,id=RECRUIT_PREFIX+k,name=`${ROLE_NAME[role]} · ${NAMES[(k-1)%NAMES.length]}`;
  const p=s.addPlayer(id,name,WEAPONS[spec].class,'normal',o?.color??'#70e1d0',{preferredSpec:spec},{developer:false,unlocks:[]});
  if(!p)return null;
  this.serial=k;p.spec=spec;p.selected=true;
  p.stats={...statsFor({},spec),hp:RECRUIT.hp[role],...(role==='tank'?{damageTaken:RECRUIT.tankDamageTaken}:{})};
  p.hp=p.stats.hp;p.ammo=magazineCapacity(spec,p.stats);
  p.body.setTranslation(at,true);p.body.setLinvel({x:0,y:0,z:0},true);p.input={...p.input,yaw,pitch:0};
  const machine=RECRUIT_MACHINES.reduce((a,b)=>flat(b.dispense,at)<flat(a.dispense,at)?b:a);
  this.list.push({id,owner,role,spec,at:s.time,m:machine.m});
  this.host.emit(PVE_EVENT.recruit,at,{source:owner,target:id,message:role});
  return id;
 }
 /** Recruits by role; party bots through partyBotInput (idle while their pick screen is open). */
 inputFor(p:Player,base?:Input):Input|undefined{
  if(this.host.isPartyBot(p))return this.host.economy.choosing(p)?idleKeep(base??p.input):partyBotInput(this.host,p,base);
  const r=this.get(p.id);if(!r||!this.alive(r))return undefined;
  return recruitInput(this.host,p,r.role,r.owner,base);
 }
 /** Medics carry no weapon. */
 weaponAway(p:Player){return this.get(p.id)?.role==='medic';}
 isRecruit(id:string){return id.startsWith(RECRUIT_PREFIX);}
 private taunting(pos:Vec){
  const t=this.sim.time;
  return this.list.filter(r=>r.role==='tank'&&(r.tauntUntil??-Infinity)>t&&this.alive(r)).map(r=>({r,d:flat(this.sim.players.get(r.id)!.body.translation(),pos)}))
   .filter(x=>x.d<=RECRUIT.tauntRadius).sort((a,b)=>a.d-b.d);
 }
 tauntNear(pos:Vec){return this.taunting(pos).length>0;}
 tauntTarget(pos:Vec):string|null{return this.taunting(pos)[0]?.r.id??null;}
 newest():string|null{return this.list.at(-1)?.id??null;}
 dismiss(id:string):{owner:string}|null{const r=this.get(id);if(!r)return null;this.list=this.list.filter(x=>x!==r);this.sim.removePlayer(id);return {owner:r.owner};}
 /** Delayed removal of the dead (frees the seat), the tank taunt, and medic pulses every RECRUIT.medic.pulse s. */
 step(_dt:number){
  const s=this.sim,t=s.time;
  for(const r of [...this.list]){
   if(!s.players.has(r.id)){this.list=this.list.filter(x=>x!==r);continue;}
   if(r.diedAt!==undefined){if(t>=r.diedAt+RECRUIT.removeAfter-1e-9){this.list=this.list.filter(x=>x!==r);s.removePlayer(r.id);}continue;}
   if(r.role==='tank'){const pos=s.players.get(r.id)!.body.translation();if(this.host.horde.adds().some(a=>activeAdd(a)&&flat(a.pos,pos)<=RECRUIT.tauntRadius))r.tauntUntil=t+1;}
  }
  const medics=this.list.filter(r=>r.role==='medic'&&this.alive(r)).map(r=>s.players.get(r.id)!);
  if(!medics.length){this.nextPulse=undefined;return;}
  this.nextPulse??=t+RECRUIT.medic.pulse;
  while(t>=this.nextPulse-1e-9){medicPulse(this.host,medics);this.nextPulse+=RECRUIT.medic.pulse;}
 }
 onDeath(p:Player){const r=this.get(p.id);if(r&&r.diedAt===undefined)r.diedAt=this.sim.time;}
 onHold(_active:boolean){}
 shift(dt:number){for(const r of this.list){r.at+=dt;if(r.diedAt!==undefined)r.diedAt+=dt;if(r.tauntUntil!==undefined)r.tauntUntil+=dt;}if(this.nextPulse!==undefined)this.nextPulse+=dt;}
 rekey(from:string,to:string){for(const r of this.list)if(r.owner===from)r.owner=to;}
 views():PveRecruitView[]{
  const t=this.sim.time;
  return this.list.map(r=>({id:r.id,owner:r.owner,role:r.role,at:r.at,m:r.m,...(r.tauntUntil!==undefined&&r.tauntUntil>t?{tauntUntil:r.tauntUntil}:{})}));
 }
 /** Role of a recruit id (tests, balance harness). */
 role(id:string):RecruitRole|undefined{return this.get(id)?.role;}
 reset(){this.list=[];this.serial=0;this.nextPulse=undefined;}
}
