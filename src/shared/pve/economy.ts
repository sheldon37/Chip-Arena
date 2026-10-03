// PvE economy (design §4, §5; stream S3): the money ledger and rewards, the F-key interactions (pickups, revives, chip,
// recruit and med machines), personal chip picks (lazy offers, commands, live install), forced idle while choosing or
// channelling, paid revives, free respawns, the solo self-revive and party-bot picks. Deterministic: every draw comes from
// host.rng('economy'); every schedule is an absolute sim time, so results do not depend on the step size.
import type {Input,Vec} from '../config.js';
import type {Player} from '../simulation.js';
import {ADD_PREFIX,BOSS_PLAYER_ID,HORDE_ID,PVE_EVENT,
 type AddDeath,type DropTier,type EconomyApi,type PickSource,type PveChannel,type PveCommand,type PveHost,type PveMachineUse,
 type PvePartyView,type PvePick,type PvePickupView} from './contract.js';
import {ADD_KINDS,DROP_ODDS,ECONOMY,PICKS,PRICES,PVE,chipPrice,revivePrice} from './balance.js';
import {PVE_MACHINES,type PveMachine} from './layout.js';
import {interactTarget,respawnPad} from './machines.js';
import {applyChipsLive,bestOffer,chipAllowed,offerCount,rollOffers,stillEligible} from './picks.js';

/** An empty party row (the seed's view of every member). S3 may reuse it for initial rows. */
export function emptyPartyRow(kind:PvePartyView['kind'],connected:boolean):PvePartyView{
 return {kind,connected,money:0,earned:0,spent:0,kills:0,assists:0,heads:0,addDamage:0,bossDamage:0,revives:0,revived:0,deaths:0,
  chipsBought:0,recruitsHired:0,picks:[],selfRevives:0,reviveCost:revivePrice(0),chipPrice:chipPrice(0),healReadyAt:0};
}
/** Money reasons whose event another part emits (the horde's pve-kill carries the killer's own reward, design §3.6). */
const SILENT_REASONS=new Set(['kill']);
const EPS=1e-9;
/** Weighted key pick in insertion order from a [0,1) draw (same rule as PveRng.weighted). */
export function weightedPick<K extends string>(next:()=>number,table:Partial<Record<K,number>>):K{
 const keys=(Object.keys(table) as K[]).filter(k=>(table[k]??0)>0);let roll=next()*keys.reduce((n,k)=>n+table[k]!,0);
 for(const k of keys){roll-=table[k]!;if(roll<0)return k;}return keys[keys.length-1];
}
/** Forced idle that keeps the sequence, look direction and camera (§5.3). */
const idle=(b:Input):Input=>({seq:b.seq,x:0,z:0,yaw:b.yaw,pitch:b.pitch,attack:false,electric:false,hadoken:false,uppercut:false,block:false,
 secondary:false,jump:false,dash:false,reload:false,...(b.camera?{camera:b.camera}:{})});

interface Row{
 id:string;money:number;earned:number;spent:number;chipsBought:number;recruitsHired:number;picks:PvePick[];open?:string;openAt?:number;
 hurtAt?:number;channel?:PveChannel;downAt?:number;selfRevives:number;selfReviveAt?:number;healAt?:number;healUntil?:number;healReadyAt:number;
 healPulses:number;thinkUntil?:number;pendingStats:boolean;bossCum:number;bossPaid:number;
}
interface PendingHire{owner:string;at:number;m:number;price:number}

export class Economy implements EconomyApi{
 /** D18 option (PICKS.moveWhileChoosing): keep x/z while choosing. Never while channelling. */
 moveWhileChoosing:boolean=PICKS.moveWhileChoosing;
 private rows=new Map<string,Row>();
 private pickups:PvePickupView[]=[];
 private uses:PveMachineUse[]=[];
 private hires:PendingHire[]=[];
 private interact=new Map<string,boolean>();private prevInteract=new Map<string,boolean>();
 private pickSerial=0;private pickupSerial=0;
 constructor(readonly host:PveHost){}
 private get sim(){return this.host.sim;}
 private get time(){return this.host.sim.time;}
 private draw(){return this.host.rng('economy').next();}
 private player(id:string){return this.sim.players.get(id);}
 private isMemberId(id:string){const p=this.player(id);return !!p&&(this.host.isHuman(p)||this.host.isPartyBot(p));}
 private isHumanId(id:string){const p=this.player(id);return !!p&&this.host.isHuman(p);}
 private row(id:string):Row{let r=this.rows.get(id);if(!r){r={id,money:0,earned:0,spent:0,chipsBought:0,recruitsHired:0,picks:[],selfRevives:0,healReadyAt:0,healPulses:4,pendingStats:false,bossCum:0,bossPaid:0};this.rows.set(id,r);}return r;}
 private emit(type:string,id:string,extra:object){const p=this.player(id);this.host.emit(type,p?p.body.translation():{x:0,y:0,z:0},{target:id,...extra});}
 private deny(id:string,message:string,m?:number){this.emit(PVE_EVENT.deny,id,{message});if(m!==undefined)this.use(m,id,'deny');}
 private use(m:number,by:string,result:PveMachineUse['result']){this.uses.push({m,at:this.time,by,result});if(this.uses.length>16)this.uses.shift();}
 private stage(){return this.host.director.view().stage;}

 // ---- money ----
 /** Start money for humans, the solo self-revive token, and an open `start` pick for every connected member (§5.4). */
 onFightStart(){
  for(const p of this.host.members()){
   const r=this.row(p.id);
   if(this.host.isHuman(p)){r.money=ECONOMY.startMoney;r.earned=ECONOMY.startMoney;r.selfRevives=this.host.solo?PVE.soloSelfRevives:0;}
   if(p.connected)this.openPick(p,this.grantPick(p.id,'common','start'));
  }
 }
 /** Kill rewards (§4.1): the killer (a recruit pays its hirer 50%, a chaos minion its owner), head bonus, assists, possessed kill.
  *  d.assists are the attackers that dealt ≥ ECONOMY.assistMinFraction of the add's max HP (filtered by the horde). */
 onAddKilled(d:AddDeath){
  const base=Math.round(ADD_KINDS[d.kind].reward*this.host.director.modifiers().reward),head=d.headless?ECONOMY.headBonus:0;
  let killer=d.killer;
  const minion=killer?this.sim.chaos.minions.get(killer):undefined;if(minion&&minion.kind!=='siren-add')killer=minion.owner;
  if(killer&&(killer===HORDE_ID||killer===BOSS_PLAYER_ID||killer.startsWith(ADD_PREFIX)))killer=null;
  let paid:string|null=null;
  const kp=killer?this.player(killer):undefined;
  if(kp&&this.host.isRecruit(kp)){
   const hirer=this.host.recruits.views().find(r=>r.id===kp.id)?.owner;
   if(hirer&&this.isHumanId(hirer)){this.grant(hirer,Math.round((base+head)*ECONOMY.recruitKillShare),'recruit');paid=hirer;}
  }else if(kp&&this.host.isHuman(kp)){this.grant(kp.id,base,'kill');if(head)this.grant(kp.id,head,'head');paid=kp.id;}
  const assist=Math.round(base*ECONOMY.assistShare);
  for(const id of new Set(d.assists))if(id!==killer&&id!==paid&&this.isHumanId(id))this.grant(id,assist,'assist');
  if(d.possessed)for(const p of this.host.humans())this.grant(p.id,p.id===paid?ECONOMY.possessedKill:ECONOMY.possessedOthers,'possessed');
 }
 /** $4 per cumulative 1% of the boss's max HP dealt (the facade already credits recruits to their hirer). */
 onBossDamage(source:string,amount:number,maxHp:number){
  if(!this.isHumanId(source)||!(amount>0)||!(maxHp>0))return;const r=this.row(source);r.bossCum+=amount;
  const steps=Math.floor(r.bossCum/maxHp*100+EPS);if(steps>r.bossPaid){const n=steps-r.bossPaid;r.bossPaid=steps;this.grant(source,n*ECONOMY.bossPerPercent,'boss');}
 }
 /** Humans only: party bots never gain money. */
 grant(id:string,amount:number,reason:string){
  if(!this.isHumanId(id)||!Number.isFinite(amount))return;const a=Math.round(amount);if(!a)return;const r=this.row(id);
  r.money=Math.max(0,r.money+a);if(a>0)r.earned+=a;
  if(!SILENT_REASONS.has(reason))this.emit(PVE_EVENT.cash,id,{amount:a,message:reason});
 }
 /** Every human in the room, dead, alive or disconnected (wave clear, thresholds). */
 grantAll(amount:number,reason:string){for(const p of this.host.humans())this.grant(p.id,amount,reason);}
 money(id:string){return this.rows.get(id)?.money??0;}
 private charge(r:Row,price:number){r.money-=price;r.spent+=price;}

 // ---- picks ----
 grantPick(id:string,tier:DropTier,source:PickSource):string|null{
  if(!this.isMemberId(id))return null;const pick:PvePick={id:'pick-'+(++this.pickSerial),tier,source,at:this.time};
  this.row(id).picks.push(pick);return pick.id;
 }
 private roll(p:Player,pick:PvePick){pick.offers=rollOffers(p,pick.tier,()=>this.draw(),offerCount(pick.tier,this.sim.options.draftChoices));}
 /** Opens a pick; offers are rolled on the first open and then frozen (D17). Party bots start thinking. */
 private openPick(p:Player,pickId:string|null|undefined){
  const r=this.row(p.id),pick=r.picks.find(k=>k.id===pickId);if(!pick)return false;
  if(!pick.offers)this.roll(p,pick);
  r.open=pick.id;r.openAt=this.time;
  if(this.host.isPartyBot(p)){const [lo,hi]=PICKS.botThink;r.thinkUntil=this.time+lo+(hi-lo)*this.draw();}
  return true;
 }
 private closePick(r:Row){r.open=undefined;r.openAt=undefined;r.thinkUntil=undefined;}
 /** Installs `chip` from the open pick (already an offer). An ineligible chip re-rolls the offers instead (false). */
 private choose(p:Player,r:Row,pick:PvePick,chip:string){
  if(!stillEligible(p,chip)){this.roll(p,pick);this.deny(p.id,'offers-refreshed');return false;}
  p.chips[chip]=(p.chips[chip]||0)+1;
  if(p.hp>0)applyChipsLive(p);else r.pendingStats=true;
  this.sim.emit('install',p.body.translation(),{source:p.id,message:chip});
  r.picks=r.picks.filter(k=>k!==pick);this.closePick(r);
  const next=r.picks[0];if(next)this.openPick(p,next.id);
  return true;
 }
 command(id:string,cmd:PveCommand){
  const p=this.player(id);if(!p||!this.host.isHuman(p))return false;const r=this.row(id);
  switch(cmd.action){
   case 'open':{if(this.host.director.holding())return false;const pick=cmd.pick===undefined?r.picks[0]:r.picks.find(k=>k.id===cmd.pick);return !!pick&&this.openPick(p,pick.id);}
   case 'close':{if(!r.open)return false;this.closePick(r);return true;}
   case 'pick':{
    if(r.open!==cmd.pick)return false;const pick=r.picks.find(k=>k.id===cmd.pick);
    if(!pick?.offers?.includes(cmd.chip)||!chipAllowed(this.sim,p,cmd.chip))return false;
    this.choose(p,r,pick,cmd.chip);return true;
   }
   // Retired: taken chips always wait as pending picks (C / the icon). Still accepted from published clients.
   case 'prefs':return true;
   default:return false;
  }
 }
 /** Records the F state for edge detection; forced idle while channelling or choosing (x/z kept with moveWhileChoosing). */
 inputFor(p:Player,base?:Input):Input|undefined{
  const b=base??p.input;this.interact.set(p.id,b.interact===true);
  const r=this.rows.get(p.id);if(!r)return undefined;
  if(r.channel)return idle(b);
  if(r.open){const i=idle(b);if(this.moveWhileChoosing){i.x=b.x;i.z=b.z;}return i;}
  return undefined;
 }
 choosing(p:Player){return !!this.rows.get(p.id)?.open;}
 weaponAway(p:Player){return this.choosing(p);}

 // ---- damage, death, revive ----
 onDamage(p:Player,actual:number,_source:string|null){
  if(!(actual>0))return;const r=this.rows.get(p.id);if(!r?.open)return;
  r.hurtAt=this.time;if(this.host.isPartyBot(p))r.thinkUntil=this.time;   // a bot under attack picks at once
 }
 onDeath(p:Player){
  const r=this.rows.get(p.id);if(!r)return;
  this.closePick(r);r.channel=undefined;r.downAt=this.time;r.hurtAt=undefined;
  if(this.host.isHuman(p)&&this.host.solo&&r.selfRevives>0&&this.stage()!=='ended')r.selfReviveAt=this.time+PVE.selfReviveDelay;
 }
 /** From Simulation.revivePlayer (every revive path): picks made while dead apply now; once-keys reset (D37). */
 onRevive(p:Player){
  const r=this.rows.get(p.id);
  if(r){r.downAt=undefined;r.selfReviveAt=undefined;if(r.pendingStats){r.pendingStats=false;applyChipsLive(p);}}
  this.sim.chaos.forget(p.id+':exit');this.sim.chaos.forget(p.id+':panic');
 }
 private revive(p:Player,pos:{x:number;z:number},event:string,extra:object){
  this.sim.revivePlayer(p,PVE.respawnHpFraction,this.host.standAt(pos.x,pos.z,p),event,extra);
  this.host.setUntouchable(p.id,this.time+PVE.reviveShield);
 }
 /** D7b: every dead, connected human and party bot back at 50% HP on a safe pad. Wave clears also give each party bot a free common pick. */
 respawnAll(reason:'wave'|'phase'){
  const adds=this.host.horde.adds(),taken:{x:number;z:number}[]=[];
  for(const p of this.host.members()){
   if(p.hp>0||!p.connected)continue;
   const from=p.death?.pos??p.body.translation(),pad=respawnPad(from,adds,PVE.respawnClearRadius,taken);taken.push(pad);
   this.revive(p,pad,PVE_EVENT.respawn,{message:reason});
  }
  // D37: once-per-round chip keys reset for the whole party at every wave clear and boss phase (revives reset them in onRevive).
  for(const p of this.host.party()){this.sim.chaos.forget(p.id+':exit');this.sim.chaos.forget(p.id+':panic');}
  if(reason==='wave')for(const b of this.host.partyBots())this.grantPick(b.id,'common','wave');
 }
 pendingRevive(){for(const r of this.rows.values())if(r.selfReviveAt!==undefined&&(this.player(r.id)?.hp??1)<=0)return true;return false;}
 pendingHires(){return this.hires.length;}
 /** Pending hires of one owner (the per-player recruit cap counts them). */
 pendingHiresOf(owner:string){return this.hires.filter(h=>h.owner===owner).length;}

 // ---- per tick ----
 step(_dt:number){
  if(this.sim.phase!=='fight')return;
  if(this.stage()==='ended'){for(const r of this.rows.values()){r.picks=[];this.closePick(r);r.channel=undefined;r.selfReviveAt=undefined;}return;}
  const t=this.time;
  for(const h of [...this.hires])if(t>=h.at-EPS){this.hires.splice(this.hires.indexOf(h),1);this.completeHire(h);}
  for(const r of [...this.rows.values()]){
   const p=this.player(r.id);if(!p)continue;
   while(r.healAt!==undefined&&r.healPulses<4&&t>=r.healAt+PRICES.healSeconds/4*(r.healPulses+1)-EPS){r.healPulses++;if(p.hp>0)this.sim.heal(p,p.stats.hp*PRICES.healFraction/4,p.id);}
   if(r.selfReviveAt!==undefined&&t>=r.selfReviveAt-EPS){
    r.selfReviveAt=undefined;
    if(p.hp<=0&&p.connected&&r.selfRevives>0){r.selfRevives--;this.revive(p,p.death?.pos??p.body.translation(),PVE_EVENT.revive,{message:'self'});}
   }
   if(this.host.isPartyBot(p))this.stepBot(p,r);
  }
  for(const p of this.host.humans())this.stepInteract(p,this.row(p.id));
  while(this.uses.length&&t-this.uses[0].at>4)this.uses.shift();
 }
 /** §4.7: open the next pick at once, think, then take the highest-rarity offer. */
 private stepBot(p:Player,r:Row){
  if(!r.open){if(r.picks.length)this.openPick(p,r.picks[0].id);return;}
  if(r.thinkUntil!==undefined&&this.time<r.thinkUntil-EPS)return;
  const pick=r.picks.find(k=>k.id===r.open);if(!pick){this.closePick(r);return;}
  for(let tries=0;tries<2;tries++){
   const chip=bestOffer(pick.offers??[]);if(!chip||!chipAllowed(this.sim,p,chip))break;
   if(this.choose(p,r,pick,chip))return;
  }
  r.picks=r.picks.filter(k=>k!==pick);this.closePick(r);
 }
 private downed(except:string){
  const out:{id:string;pos:Vec}[]=[];
  for(const q of this.host.members())if(q.id!==except&&q.hp<=0&&q.connected&&q.death)out.push({id:q.id,pos:q.death.pos});
  return out;
 }
 private stepInteract(p:Player,r:Row){
  const cur=this.interact.get(p.id)===true,pressed=cur&&!this.prevInteract.get(p.id);this.prevInteract.set(p.id,cur);
  if(r.channel){this.stepChannel(p,r,cur);return;}
  if(!pressed||p.hp<=0||r.open||!p.connected)return;
  const target=interactTarget(p.body.translation(),p.id,this.time,this.pickups,this.downed(p.id));if(!target)return;
  if(target.kind==='pickup')this.grab(p,r,target.pickup);
  else if(target.kind==='revive'){
   if(r.money<revivePrice(this.host.stats(target.target).revived)){this.deny(p.id,'money');return;}
   r.channel={kind:'revive',target:target.target,at:this.time,until:this.time+PRICES.reviveSeconds};
  }else this.tap(p,r,target.machine);
 }
 /** D8: cancels on release, leaving the radius, the reviver's death or the target getting up; charges on completion only. */
 private stepChannel(p:Player,r:Row,held:boolean){
  const c=r.channel!,t=this.player(c.target),pos=p.body.translation();
  const ok=held&&p.hp>0&&p.connected&&!!t&&t.hp<=0&&!!t.death&&Math.hypot(t.death.pos.x-pos.x,t.death.pos.z-pos.z)<=PRICES.reviveRadius;
  if(!ok){r.channel=undefined;return;}
  if(this.time<c.until-EPS)return;
  r.channel=undefined;
  const price=revivePrice(this.host.stats(t.id).revived);if(r.money<price){this.deny(p.id,'money');return;}
  this.charge(r,price);this.host.stats(p.id).revives++;this.host.stats(t.id).revived++;
  this.revive(t,t.death!.pos,PVE_EVENT.revive,{source:p.id,amount:price});
 }
 private grab(p:Player,r:Row,k:PvePickupView){
  // Owner playtest: a taken chip never opens the chip screen; it waits as a pending pick (C / the CHIPS icon) until chosen.
  this.pickups=this.pickups.filter(x=>x!==k);this.grantPick(p.id,k.tier,'dispenser');
  this.emit(PVE_EVENT.grab,p.id,{message:k.tier,source:k.id});
 }
 private tap(p:Player,r:Row,m:PveMachine){
  if(m.kind==='chip'){
   const price=chipPrice(r.chipsBought);
   if(r.money<price)return this.deny(p.id,'money',m.m);
   if(this.pickups.filter(k=>k.owner===p.id).length>=PICKS.maxUngrabbed)return this.deny(p.id,'pickups',m.m);
   this.charge(r,price);r.chipsBought++;this.use(m.m,p.id,'chip');
   const tier=weightedPick(()=>this.draw(),DROP_ODDS);
   this.pickups.push({id:'pk'+(++this.pickupSerial),owner:p.id,tier,pos:{x:m.dispense.x,y:.9,z:m.dispense.z},born:this.time+PRICES.dispenseDelay,m:m.m});
   this.emit(PVE_EVENT.buy,p.id,{message:'chip',amount:price});
  }else if(m.kind==='recruit'){
   const price=PRICES.recruit;
   if(r.money<price)return this.deny(p.id,'money',m.m);
   const can=this.host.recruits.canHire(p.id);if(can!=='ok')return this.deny(p.id,can,m.m);
   this.charge(r,price);this.use(m.m,p.id,'recruit');this.hires.push({owner:p.id,at:this.time+PRICES.recruitDelay,m:m.m,price});
   this.emit(PVE_EVENT.buy,p.id,{message:'recruit',amount:price});
  }else{
   if(p.hp>=p.stats.hp-EPS)return this.deny(p.id,'full',m.m);
   if(this.time<r.healReadyAt-EPS)return this.deny(p.id,'cooldown',m.m);
   const price=PRICES.heal;if(r.money<price)return this.deny(p.id,'money',m.m);
   this.charge(r,price);this.use(m.m,p.id,'med');r.healReadyAt=this.time+PRICES.healCooldown;r.healAt=this.time;r.healUntil=this.time+PRICES.healSeconds;r.healPulses=0;
   this.emit(PVE_EVENT.buy,p.id,{message:'heal',amount:price});
  }
 }
 /** The pod pops the recruit; a failed hire (seat taken meanwhile) is refunded. */
 private completeHire(h:PendingHire){
  const machine=PVE_MACHINES[h.m],r=this.rows.get(h.owner);
  const id=machine&&this.player(h.owner)?this.host.recruits.hire(h.owner,this.host.standAt(machine.dispense.x,machine.dispense.z),machine.yaw):null;
  if(id){if(r)r.recruitsHired++;return;}
  if(r){r.money+=h.price;r.spent-=h.price;this.emit(PVE_EVENT.cash,h.owner,{amount:h.price,message:'refund'});}
 }

 // ---- holds and re-keying ----
 /** Hold start: channels cancel, heals complete at once and party DoTs clear. */
 onHold(active:boolean){
  if(!active)return;
  for(const r of this.rows.values()){
   r.channel=undefined;const p=this.player(r.id);
   if(p&&r.healAt!==undefined&&r.healPulses<4){const left=4-r.healPulses;r.healPulses=4;if(p.hp>0)this.sim.heal(p,p.stats.hp*PRICES.healFraction/4*left,p.id);}
  }
  for(const p of this.host.party()){p.burns.clear();p.poisons.clear();}
 }
 /** Every hold tick: absolute timers move by dt (D21). */
 shift(dt:number){
  for(const k of this.pickups)k.born+=dt;
  for(const h of this.hires)h.at+=dt;
  for(const u of this.uses)u.at+=dt;
  for(const r of this.rows.values()){
   r.healReadyAt+=dt;if(r.healAt!==undefined)r.healAt+=dt;if(r.healUntil!==undefined)r.healUntil+=dt;
   if(r.selfReviveAt!==undefined)r.selfReviveAt+=dt;if(r.thinkUntil!==undefined)r.thinkUntil+=dt;
   if(r.channel){r.channel.at+=dt;r.channel.until+=dt;}
  }
 }
 /** Rejoin (§9.3): money, picks, pickups, channels and pending hires move to the new id. */
 rekey(from:string,to:string){
  const r=this.rows.get(from);if(r){this.rows.delete(from);r.id=to;this.rows.set(to,r);}
  for(const k of this.pickups)if(k.owner===from)k.owner=to;
  for(const h of this.hires)if(h.owner===from)h.owner=to;
  for(const u of this.uses)if(u.by===from)u.by=to;
  for(const x of this.rows.values())if(x.channel?.target===from)x.channel.target=to;
  for(const map of [this.interact,this.prevInteract]){const v=map.get(from);map.delete(from);if(v!==undefined)map.set(to,v);}
 }

 // ---- views ----
 partyView(id:string):PvePartyView{
  const p=this.player(id),r=this.rows.get(id),s=this.host.stats(id),row=emptyPartyRow(p&&this.host.isPartyBot(p)?'bot':'human',!!p?.connected);
  Object.assign(row,{kills:s.kills,assists:s.assists,heads:s.heads,addDamage:s.addDamage,bossDamage:s.bossDamage,revives:s.revives,revived:s.revived,deaths:s.deaths,
   reviveCost:revivePrice(s.revived)});
  if(!r)return row;
  Object.assign(row,{money:r.money,earned:r.earned,spent:r.spent,chipsBought:r.chipsBought,recruitsHired:r.recruitsHired,selfRevives:r.selfRevives,
   chipPrice:chipPrice(r.chipsBought),healReadyAt:r.healReadyAt,picks:r.picks.map(k=>({...k,...(k.offers?{offers:[...k.offers]}:{})}))});
  const opt=row as unknown as Record<string,unknown>;
  for(const key of ['open','openAt','hurtAt','downAt','selfReviveAt','healAt','healUntil'] as const)if(r[key]!==undefined)opt[key]=r[key];
  if(r.channel)row.channel={...r.channel};
  return row;
 }
 machinesView():PveMachineUse[]{return this.uses.map(u=>({...u}));}
 pickupsView():PvePickupView[]{return this.pickups.map(k=>({...k,pos:{...k.pos}}));}
 reset(){this.rows.clear();this.pickups=[];this.uses=[];this.hires=[];this.interact.clear();this.prevInteract.clear();this.pickSerial=0;this.pickupSerial=0;}
 /** D29 `money:n` adds n to every human. */
 debug(op:string){
  const m=/^money:(-?\d{1,7})$/.exec(op);if(!m)return false;
  for(const p of this.host.humans()){const r=this.row(p.id);r.money=Math.max(0,r.money+Number(m[1]));}
  return true;
 }
}
