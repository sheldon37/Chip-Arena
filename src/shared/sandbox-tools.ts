// Ward Zero vents outside PvE (PvP map and DEV sandbox, owner 2026-10-04): the PvE vent pits on a fixed cycle, no adds, no
// boss. DEV sandbox tools (options.sandbox only, debug commands): chip search, dummy cast, safe mode, round hold, forced
// vent falls, hit and summon-death previews. All authoritative and through the ordinary simulation paths.
import {v,clamp,norm,sub,add,idleInput,CONFIG as C,type SpecId,type Difficulty,type Input} from './config.js';
import {kitFor} from './abilities.js';
import {ballisticPitch} from './projectiles.js';
import {ARCADE} from './arcade-content.js';
import {CHIPS,WEAPONS,eligibleChips,statsFor,magazineCapacity,type Chip,type Rarity} from './content.js';
import {canUse} from './access.js';
import {mapId} from './maps.js';
import {sirenTaken} from './lobby-presentation.js';
import {SIREN_VENTS} from './siren-stage-layout.js';
import {VentPits,VENT_PIT,inVentShaft} from './pve/vent-pits.js';
import {PveSystem} from './pve/system.js';
import type {PveEntryView} from './pve/contract.js';
import {playerSpawn} from './arena.js';
import type {Player,Simulation} from './simulation.js';

export type ChipSort='default'|'rarity'|'rarity-desc'|'name';
/** Rarity tiers, lowest first: the six draft tiers, then the DEV-kit exclusives in catalog order (owner 2026-10-04: the
 *  sandbox list sorts strictly by tier). RARITIES' own key order stays as it is: draft rolls walk it. */
export const RARITY_ORDER=['common','uncommon','rare','epic','legendary','mythic','mishima','ansatsuken','l44t','siren'] as const satisfies readonly Rarity[];
export const rarityRank=(r:Rarity)=>{const i=(RARITY_ORDER as readonly Rarity[]).indexOf(r);return i<0?RARITY_ORDER.length:i;};
const rank=(c:Chip)=>rarityRank(c.rarity);
/** Live chip search (every term in name, id or description, case-insensitive) and sort. Pure. */
export function findChips(list:readonly Chip[],query='',sort:ChipSort='default'){
 const terms=query.toLowerCase().split(/\s+/).filter(Boolean),out=list.filter(c=>{const s=(c.name+' '+c.id+' '+c.description).toLowerCase();return terms.every(t=>s.includes(t));});
 if(sort==='name')out.sort((a,b)=>a.name.localeCompare(b.name));
 else if(sort!=='default'){const d=sort==='rarity'?1:-1;out.sort((a,b)=>d*(rank(a)-rank(b))||a.name.localeCompare(b.name));}
 return out;
}

/** Vent e opens at e·step + k·period for `open` s; pure in time. `forced`: vent → an extra opening start. */
export const WARD_VENTS={period:12,open:4.5,step:1.5,tail:1} as const;
export function wardEntries(t:number,forced?:Map<number,number>):PveEntryView[]{
 const W=WARD_VENTS,ahead=VENT_PIT.lead+VENT_PIT.warn;return SIREN_VENTS.map((_,e)=>{const f=forced?.get(e),o=e*W.step;let a=o+(Math.floor((t-o-W.open-W.tail)/W.period)+1)*W.period;
  // A forced opening shows from its warning on; one forced while the vent was closing waits out that close (prev); afterwards
  // the cycle resumes with the first opening that warns after it, so a vent never appears already open.
  if(f!==undefined){const end=f+W.open+W.tail,prev=f-ahead-W.tail-W.open;
   if(t>=f-ahead&&t<end)a=f;else if(t>=prev&&t<f-ahead)a=prev;else if(t>=end&&a-ahead<end)a=o+Math.ceil((end+ahead-o)/W.period)*W.period;}
  return {e,openAt:a,closeAt:a+W.open,mode:'dive' as const};});
}
/** Every pilot can fall; only the sandbox brings the body back down (PvP has no revive). */
export class WardVents extends VentPits{
 readonly forced:Map<number,number>;
 constructor(s:Simulation){const forced=new Map<number,number>(),no=()=>false;
  super({sim:s,horde:{views:()=>({entries:wardEntries(s.time,forced)})},slots:()=>[],isBoss:no,hidden:no,isRecruit:no,isMember:()=>true,isHuman:(p:Player)=>!p.bot,godMode:false,
   members:()=>[...s.players.values()],boss:()=>undefined,standAt:PveSystem.prototype.standAt} as unknown as PveSystem);
  this.forced=forced;this.lands=!!s.options.sandbox;}
 reset(){super.reset();this.forced.clear();}
}
/** Ward Zero chosen as the map (not forced by a Siren pilot) outside PvE. */
export const ward=(s:Simulation)=>!s.pve&&s.options.map==='siren'&&!s.normalArena?s.vents??=new WardVents(s):undefined;

const fits=(c:Chip,s:SpecId)=>eligibleChips(s,{}).some(x=>x.id===c.id);
/** The dummy's weapon for a chip: its own when the chip fits (a Siren dummy keeps the boss kit only for Siren chips, so
 *  the sandbox leaves Ward Zero again), else the first fitting weapon (non-DEV first). */
export const castSpec=(c:Chip,cur:SpecId)=>fits(c,cur)&&(cur!=='siren'||c.rarity==='siren')?cur:(Object.keys(WEAPONS) as SpecId[]).sort((a,b)=>+!!WEAPONS[a].devOnly-+!!WEAPONS[b].devOnly).find(s=>fits(c,s));

/** Dummy cast (keypress), owner 2026-10-04: instead of fighting with its AI for 4 s ('ai'), the dummy turns to the pilot who
 *  cast it and presses one key, as soon as it is able to act (L44T Scare Tactics' Slenderman is on E, Siren's moves on 1-4). */
export type CastKey='ai'|'e'|'q'|'block'|'dash'|'jump'|'1'|'2'|'3'|'4'|'mode';
export const CAST_KEYS:readonly CastKey[]=['ai','e','q','block','dash','jump','1','2','3','4','mode'];
/** The input a key sets for a weapon (the same fields the pilot's own keys set), or undefined when it does nothing for it. */
export function keyInput(spec:SpecId,key:CastKey):Partial<Input>|undefined{
 const siren=spec==='siren',kit=kitFor(spec);
 switch(key){
  case 'ai':return undefined;
  case 'e':return siren?undefined:{secondary:true};
  case 'q':return kit?.q.live||kit?.ads||spec==='emperor'?{ability:true,electric:true}:undefined;
  case 'block':return {block:true};case 'dash':return {dash:true};case 'jump':return {jump:true};
  case 'mode':return siren?{sirenToggle:true}:undefined;
  default:{const n=Number(key) as 1|2|3|4;return siren?{sirenAbility:n}:spec==='ansatsuken'&&n===1?{hadoken:true}:spec==='ansatsuken'&&n===2?{uppercut:true}:undefined;}
 }
}
/** How long a key stays down: a tap, or long enough for a charge (Shinku Hadoken), an aim-down-sights look or a guard window. */
export function keyHold(p:Pick<Player,'spec'|'chips'>,key:CastKey,asked?:unknown){
 const n=Number(asked);if(asked!==undefined&&asked!==''&&Number.isFinite(n))return clamp(n,0,3);
 return key==='1'&&p.spec==='ansatsuken'&&p.chips['arcade-shinku']?ARCADE.chargeSeconds+.1:key==='q'&&kitFor(p.spec)?.ads?1.2:key==='block'?.6:0;
}

/** Sandbox bot respawn (owner 2026-10-04): killing the dummy (any sandbox bot) does not end the round; it stands up again on
 *  its pad this many seconds after its death. The default matches the death presentation (ANIMATION.deathSeconds), so kill
 *  effects finish; 0 turns it off (the old round end). Humans keep the round flow: a human death still ends the round. */
export const SANDBOX_RESPAWN={default:5,min:1,max:30} as const;
export const respawnDelay=(n:unknown)=>{const x=Number(n);return !Number.isFinite(x)||x<=0?0:clamp(x,SANDBOX_RESPAWN.min,SANDBOX_RESPAWN.max);};
/** Sandbox settings the panel shows (snapshot `sandbox`, sandbox rooms only). */
export interface SandboxView{respawn:number;safe:boolean;every:number}

export class SandboxTools{
 /** Safe mode: humans keep 1 HP against every damage path. hold: the round does not resolve before this time. */
 safe=false;hold=0;every=0;next=0;cast?:{id:string;until:number;rest:Difficulty};respawn:number=SANDBOX_RESPAWN.default;
 /** Keypress mode: the key, who asked (the dummy faces them; Slenderman stalks them) and the pending press. */
 key:CastKey='ai';by='';dummyId='';hold0?:unknown;press?:{id:string;target:string;key:CastKey;hold:number;start?:number;deadline:number;at:number};
 constructor(readonly s:Simulation){}
 dummy(p:Player){for(const q of this.s.players.values())if(q!==p&&q.bot)return q;}
 spare(p:Player){return this.safe&&!p.bot?Math.min(1,p.hp):0;}
 /** A dead bot that will stand up again (it counts as alive for the round). */
 respawns(p:Player){return this.respawn>0&&!!p.bot&&p.connected&&!this.s.pve;}
 view():SandboxView{return {respawn:this.respawn,safe:this.safe,every:this.every};}
 /** Queue one press of the current key on the dummy, aimed at the pilot who asked. */
 private queue(d:Player){this.press={id:d.id,target:this.by,key:this.key,hold:keyHold(d,this.key,this.hold0),deadline:this.s.time+.75,at:this.s.time};}
 /** Cooldowns and resources back to full, so a repeat really recasts (sandbox only; Scare Tactics alone has a 12 s cooldown). */
 /** Once-per-round chips (Emergency Exit, Do Not Press, Round Two, CTRL+Z) are ready again, as after a round reset. */
 private rearm(p:Player){const s=this.s;s.chaos.forget(p.id+':exit');s.chaos.forget(p.id+':panic');s.leet.forget(p.id);s.arcade.forget(p.id);}
 private refresh(d:Player){this.rearm(d);this.s.abilities.reset(d);d.cooldown=d.secondaryCd=d.dashCd=d.blockCd=0;if(d.arcade)d.arcade.hadokenCd=d.arcade.uppercutCd=0;if(d.siren)d.siren.ready={};
  d.mana=d.stats.maxMana;d.stamina=d.stats.maxStamina;}
 private ready(p:Player,key:CastKey){const s=this.s,ground=key==='jump'||key==='dash'||key==='e'&&p.spec==='emperor';
  return p.cooldown<=0&&!p.swing&&!p.cast&&(p.burst??0)<=0&&!s.abilities.busy(p)&&!(p.spec==='siren'&&s.siren.busy(p))&&(p.blockTime??0)<=0&&(!ground||p.grounded);}
 /** Face the target the way a cast aims: point casts at the feet, ballistic ones lobbed, the rest straight at the chest. */
 private aim(p:Player,t:Player,key:CastKey){const eye=add(p.body.translation(),v(0,.5,0)),to=t.body.translation(),kit=kitFor(p.spec),def=key==='e'?kit?.e:key==='q'?kit?.q:undefined,bot=def?.bot;
  const at=bot?.aim==='point'?v(to.x,to.y-(C.playerHalfHeight+C.playerRadius-.05),to.z):v(to.x,to.y,to.z),d=sub(at,eye),h=Math.hypot(d.x,d.z);
  return {yaw:Math.atan2(d.x,d.z),pitch:bot?.aim==='ballistic'?clamp(ballisticPitch(h,d.y,def!.speed||30,def!.gravity||0),-1.1,1.1):clamp(Math.atan2(d.y,h),-1.1,1.1)};}
 /** The dummy's input while a keypress is queued or held: still, facing the target, every other action released. */
 input(p:Player,i:Input):Input{const pr=this.press;if(!pr||pr.id!==p.id)return i;const s=this.s,t=s.players.get(pr.target);
  if(!t||p.hp<=0){this.press=undefined;return i;}
  const out:Input={...i,x:0,z:0,attack:false,secondary:false,ability:false,electric:false,hadoken:false,uppercut:false,block:false,dash:false,jump:false,reload:false,sirenToggle:false,sirenAbility:undefined,camera:'first',...this.aim(p,t,pr.key)};
  if(pr.start===undefined&&(this.ready(p,pr.key)||s.time>=pr.deadline))pr.start=s.time;
  if(pr.start!==undefined){if(s.time<pr.start+Math.max(pr.hold,1e-6))Object.assign(out,keyInput(p.spec,pr.key));if(s.time>=pr.start+pr.hold+.8)this.press=undefined;}
  return out;}
 private padOf(p:Player){return [...this.s.players.values()].indexOf(p);}
 /** Back on its own pad (the pad resetRound gives it), restored like a round start; chips and weapon are kept. */
 private revive(p:Player,pad:number){const s=this.s,pos=playerSpawn(pad,true);
  s.revivePlayer(p,1,pos,'sandbox-respawn');this.rearm(p);p.siren=undefined;p.stats=statsFor(p.chips,p.spec);s.abilities.reset(p);p.hp=p.stats.hp;p.mana=p.stats.maxMana;p.stamina=p.stats.maxStamina;p.staminaAt=-10;
  p.ammo=magazineCapacity(p.spec,p.stats);p.cooldown=p.secondaryCd=p.dashCd=p.dashTime=p.reloadTime=p.parryTime=0;p.guarding=false;p.blockTime=p.blockCd=0;p.blockHeld=p.blockProcUsed=false;p.lastBlock=-10;
  p.waveTime=p.electricStepTime=0;p.attackIndex=0;p.hits=0;p.lastShot=p.lastSecondary=p.lastAttack=-10;p.input={...idleInput(),seq:p.input.seq,yaw:Math.atan2(-pos.x,-pos.z)};p.body.setAngvel(v(),true);}
 holding(){return this.s.time<this.hold||!!this.s.vents?.busy;}
 /** The dummy fights as a hard bot for 4 s (its normal AI: approach, attack, E and Q), then stands again. */
 arm(d:Player){this.cast={id:d.id,until:this.s.time+4,rest:this.cast?.rest??d.bot!};d.bot='hard';d.botThink=0;}
 step(){const s=this.s,c=this.cast;
  if(c&&s.time>=c.until){this.cast=undefined;const d=s.players.get(c.id);if(d)d.bot=c.rest;}
  if(this.every&&s.time>=this.next){this.next=s.time+this.every;const d=s.players.get(c?.id??'')??[...s.players.values()].find(q=>q.bot);if(d&&d.hp>0){if(this.key==='ai')this.arm(d);else{this.refresh(d);this.queue(d);}}}
  // Slenderman summoned by the dummy's keypress stalks the pilot who asked for it, even with other bots nearer.
  const asker=s.players.get(this.by);if(this.key!=='ai'&&asker&&asker.hp>0&&asker.connected&&!asker.leet?.scare)for(const f of s.leet.fields.values())if(f.kind==='tree'&&f.owner===this.dummyId)f.target=asker.id;
  if(s.vents?.busy)this.hold=Math.max(this.hold,s.time+3);
  // Respawns wait for animation holds and for a vent-fall body to land (the splat resets death.at).
  // (A shaft body lands only while the ward vents run; if a Siren pilot paused them, it stands up anyway.)
  for(const p of s.players.values())if(p.hp<=0&&p.death&&this.respawns(p)&&!(inVentShaft(p.death,s.time)&&s.vents&&ward(s))&&s.time>=this.hold&&s.time>=p.death.at+this.respawn)this.revive(p,this.padOf(p));
 }
 /** Sandbox debug actions; undefined for actions this class does not own. */
 command(c:any,p:Player):boolean|undefined{
  const s=this.s,d=this.dummy(p),target=c.target==='self'?p:d;
  switch(c.action){
   case 'respawn':{if(c.seconds===undefined||c.seconds===''||!Number.isFinite(Number(c.seconds)))return false;this.respawn=respawnDelay(c.seconds);return true;}
   case 'dummy-cast':{if(!d)return false;if(c.stop){this.every=0;if(this.cast)this.cast.until=0;return true;}
    // A chip (the dummy takes a weapon it fits), or no chip and a weapon to spar with.
    const chip=CHIPS.find(x=>x.id===c.id),spec:SpecId|undefined=chip?castSpec(chip,d.spec):c.id===undefined&&Object.hasOwn(WEAPONS,c.spec)?c.spec:undefined;
    if(!spec||WEAPONS[spec].devOnly&&!canUse(p,'weapon:'+spec)||spec==='siren'&&sirenTaken(s.players.values(),d.id))return false;
    const key:CastKey=CAST_KEYS.includes(c.key)?c.key:'ai';if(key!=='ai'&&!keyInput(spec,key))return false;
    if(d.hp<=0)this.revive(d,this.padOf(d));
    // A DEV kit checks its own pilot's access: the dummy gets the cast weapon only (like DEV bots), never the DEV role.
    d.unlocks=WEAPONS[spec].devOnly?['weapon:'+spec]:[];d.chips={};this.rearm(d);s.respec(d,spec);if(chip)d.chips[chip.id]=1;d.stats=statsFor(d.chips,d.spec);d.hp=d.stats.hp;d.mana=d.stats.maxMana;d.stamina=d.stats.maxStamina;d.ammo=magazineCapacity(d.spec,d.stats);
    this.safe=c.safe===true;const n=Number(c.every)||0;this.every=n>0?clamp(n,2,60):0;this.next=s.time+this.every;this.key=key;this.by=p.id;this.hold0=c.hold;this.dummyId=d.id;
    if(key==='ai'){this.press=undefined;this.arm(d);}else{if(this.cast)this.cast.until=0;this.refresh(d);this.queue(d);}return true;}
   case 'safe':this.safe=c.on===true;return true;
   case 'map':{const id=mapId(c.id);if(id!==c.id)return false;s.options.map=id;s.normalArena=undefined;s.resetSandboxOutcome();s.resetRound();s.phase='fight';s.fightStart=s.time;return true;}
   case 'vent-fall':{if(s.options.map!=='siren')this.command({action:'map',id:'siren'},p);const w=ward(s);if(!target||target.hp<=0||!w)return false;
    w.splatAt=clamp(Number(c.delay)||VENT_PIT.splatAt,1,VENT_PIT.splatAt);const at=target.body.translation(),dist=(e:number)=>Math.hypot(SIREN_VENTS[e].x-at.x,SIREN_VENTS[e].z-at.z);
    let e=0;for(let i=1;i<SIREN_VENTS.length;i++)if(dist(i)<dist(e))e=i;
    // Open that vent for real and stand the target on it (owner 2026-10-04: the opening must show before the fall): unless it
    // is already warning or open, it warns now (or once a closing lid is down), its lid swings VENT_PIT.warn s later and the
    // pit check takes the fall once lethal.
    const cur=wardEntries(s.time,w.forced)[e];if(!(s.time>=cur.openAt-VENT_PIT.lead-VENT_PIT.warn&&s.time<cur.closeAt))w.forced.set(e,(s.time>=cur.closeAt?cur.closeAt+WARD_VENTS.tail:s.time)+VENT_PIT.warn+VENT_PIT.lead);target.body.setTranslation(PveSystem.prototype.standAt(SIREN_VENTS[e].x,SIREN_VENTS[e].z,target),true);target.body.setLinvel(v(),true);return true;}
   case 'down':{const by=c.by==='dummy'?d:p;if(!target||!by||target===by||target.hp<=0||s.phase!=='fight')return false;
    this.hold=s.time+clamp(Number(c.hold)||0,0,15);const safe=this.safe;this.safe=false;s.damage(target,1e5,by.id,'preview',norm(sub(target.body.translation(),by.body.translation())),160);this.safe=safe;s.resolveRound();return true;}
   case 'hit':{const by=target===p?d:p;if(!target||!by||s.phase!=='fight')return false;const dir=norm(sub(target.body.translation(),by.body.translation()));
    s.damage(target,c.heavy?18:4,by.id,'preview',dir,c.heavy?900:120,{...target.body.translation(),y:target.body.translation().y+.3});return true;}
   case 'summons-die':for(const m of [...s.chaos.minions.values()])if(m.owner!==p.id)s.chaos.hitMinion(m,1e5,p.id,300,norm(sub(m.body.translation(),p.body.translation())),{point:m.body.translation(),kind:'energy'});return true;
  }
  return undefined;
 }
}
