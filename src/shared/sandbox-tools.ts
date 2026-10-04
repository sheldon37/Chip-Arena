// Ward Zero vents outside PvE (PvP map and DEV sandbox, owner 2026-10-04): the PvE vent pits on a fixed cycle, no adds, no
// boss. DEV sandbox tools (options.sandbox only, debug commands): chip search, dummy cast, safe mode, round hold, forced
// vent falls, hit and summon-death previews. All authoritative and through the ordinary simulation paths.
import {v,clamp,norm,sub,type SpecId,type Difficulty} from './config.js';
import {CHIPS,RARITIES,WEAPONS,eligibleChips,statsFor,magazineCapacity,type Chip} from './content.js';
import {canUse} from './access.js';
import {mapId} from './maps.js';
import {sirenTaken} from './lobby-presentation.js';
import {SIREN_VENTS} from './siren-stage-layout.js';
import {VentPits,VENT_PIT} from './pve/vent-pits.js';
import {PveSystem} from './pve/system.js';
import {PVE_TIMING,type PveEntryView} from './pve/contract.js';
import type {Player,Simulation} from './simulation.js';

export type ChipSort='default'|'rarity'|'rarity-desc'|'name';
/** Catalog rarity order: by draft weight (common first), the weightless special rarities last. */
const rank=(c:Chip)=>-RARITIES[c.rarity].weight;
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
 const W=WARD_VENTS;return SIREN_VENTS.map((_,e)=>{const f=forced?.get(e),o=e*W.step,a=f!==undefined&&t<f+W.open+W.tail?f:o+(Math.floor((t-o-W.open-W.tail)/W.period)+1)*W.period;return {e,openAt:a,closeAt:a+W.open,mode:'dive' as const};});
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
/** The dummy's weapon for a chip: its own when the chip fits, else the first fitting weapon (non-DEV first). */
export const castSpec=(c:Chip,cur:SpecId)=>fits(c,cur)?cur:(Object.keys(WEAPONS) as SpecId[]).sort((a,b)=>+!!WEAPONS[a].devOnly-+!!WEAPONS[b].devOnly).find(s=>fits(c,s));

export class SandboxTools{
 /** Safe mode: humans keep 1 HP against every damage path. hold: the round does not resolve before this time. */
 safe=false;hold=0;every=0;next=0;cast?:{id:string;until:number;rest:Difficulty};
 constructor(readonly s:Simulation){}
 dummy(p:Player){for(const q of this.s.players.values())if(q!==p&&q.bot)return q;}
 spare(p:Player){return this.safe&&!p.bot?Math.min(1,p.hp):0;}
 holding(){return this.s.time<this.hold||!!this.s.vents?.busy;}
 /** The dummy fights as a hard bot for 4 s (its normal AI: approach, attack, E and Q), then stands again. */
 arm(d:Player){this.cast={id:d.id,until:this.s.time+4,rest:this.cast?.rest??d.bot!};d.bot='hard';d.botThink=0;}
 step(){const s=this.s,c=this.cast;
  if(c&&s.time>=c.until){this.cast=undefined;const d=s.players.get(c.id);if(d)d.bot=c.rest;}
  if(this.every&&s.time>=this.next){this.next=s.time+this.every;const d=s.players.get(c?.id??'')??[...s.players.values()].find(q=>q.bot);if(d&&d.hp>0)this.arm(d);}
  if(s.vents?.busy)this.hold=Math.max(this.hold,s.time+3);
 }
 /** Sandbox debug actions; undefined for actions this class does not own. */
 command(c:any,p:Player):boolean|undefined{
  const s=this.s,d=this.dummy(p),target=c.target==='self'?p:d;
  switch(c.action){
   case 'dummy-cast':{if(!d)return false;if(c.stop){this.every=0;if(this.cast)this.cast.until=0;return true;}
    // A chip (the dummy takes a weapon it fits), or no chip and a weapon to spar with.
    const chip=CHIPS.find(x=>x.id===c.id),spec:SpecId|undefined=chip?castSpec(chip,d.spec):c.id===undefined&&Object.hasOwn(WEAPONS,c.spec)?c.spec:undefined;
    if(!spec||WEAPONS[spec].devOnly&&!canUse(p,'weapon:'+spec)||spec==='siren'&&sirenTaken(s.players.values(),d.id))return false;
    d.chips={};s.respec(d,spec);if(chip)d.chips[chip.id]=1;d.stats=statsFor(d.chips,d.spec);d.hp=d.stats.hp;d.mana=d.stats.maxMana;d.stamina=d.stats.maxStamina;d.ammo=magazineCapacity(d.spec,d.stats);
    this.safe=c.safe===true;const n=Number(c.every)||0;this.every=n>0?clamp(n,2,60):0;this.next=s.time+this.every;this.arm(d);return true;}
   case 'safe':this.safe=c.on===true;return true;
   case 'map':{const id=mapId(c.id);if(id!==c.id)return false;s.options.map=id;s.normalArena=undefined;s.resetSandboxOutcome();s.resetRound();s.phase='fight';s.fightStart=s.time;return true;}
   case 'vent-fall':{if(s.options.map!=='siren')this.command({action:'map',id:'siren'},p);const w=ward(s);if(!target||target.hp<=0||!w)return false;
    w.splatAt=clamp(Number(c.delay)||VENT_PIT.splatAt,1,VENT_PIT.splatAt);const at=target.body.translation(),dist=(e:number)=>Math.hypot(SIREN_VENTS[e].x-at.x,SIREN_VENTS[e].z-at.z);
    let e=0;for(let i=1;i<SIREN_VENTS.length;i++)if(dist(i)<dist(e))e=i;
    // Open that vent now and stand the target on it: the ordinary pit check takes the fall on the next tick.
    w.forced.set(e,s.time-PVE_TIMING.vent.open);target.body.setTranslation(PveSystem.prototype.standAt(SIREN_VENTS[e].x,SIREN_VENTS[e].z,target),true);target.body.setLinvel(v(),true);return true;}
   case 'down':{const by=c.by==='dummy'?d:p;if(!target||!by||target===by||target.hp<=0||s.phase!=='fight')return false;
    this.hold=s.time+clamp(Number(c.hold)||0,0,15);const safe=this.safe;this.safe=false;s.damage(target,1e5,by.id,'preview',norm(sub(target.body.translation(),by.body.translation())),160);this.safe=safe;s.resolveRound();return true;}
   case 'hit':{const by=target===p?d:p;if(!target||!by||s.phase!=='fight')return false;const dir=norm(sub(target.body.translation(),by.body.translation()));
    s.damage(target,c.heavy?18:4,by.id,'preview',dir,c.heavy?900:120,{...target.body.translation(),y:target.body.translation().y+.3});return true;}
   case 'summons-die':for(const m of [...s.chaos.minions.values()])if(m.owner!==p.id)s.chaos.hitMinion(m,1e5,p.id,300,norm(sub(m.body.translation(),p.body.translation())),{point:m.body.translation(),kind:'energy'});return true;
  }
  return undefined;
 }
}
