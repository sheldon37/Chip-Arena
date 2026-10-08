// PvE vent pits (owner, 2026-10-04): an OPEN Ward Zero floor vent is a black pit. A party member or recruit (never an
// add, never the boss) whose capsule centre is over an open vent falls in: an instant environmental kill (death kind
// 'vent-fall', any recent attacker credited exactly as the ordinary 'fall' kill credits it). The body stays in the shaft
// (not revivable, no HELP bubble) until VENT_PIT.splatAt seconds after the death, when it lands with a cartoon splat at a
// clear spot near the middle of the hall and becomes an ordinary downed body (instant F revive, holographic regeneration).
// A wave/phase respawn during the shaft time follows the normal rules (the member respawns on a pad) and the splat is then
// skipped. Authoritative and deterministic: everything below is a function of sim time, the PvE entry schedule (the same
// PveEntryView open/close times the client animates the lids from) and positions. Closed vents are ordinary floor.
// Outside PvE the same pits run on a fixed cycle when Ward Zero is the chosen map (src/shared/sandbox-tools.ts).
import {v,type Input,type Vec} from '../config.js';
import type {Player} from '../simulation.js';
import type {ArenaBlock} from '../arena.js';
import {sirenStageBlocks,SIREN_VENTS} from '../siren-stage-layout.js';
import {PVE_TIMING,type EntryKind,type PveEntryView} from './contract.js';
import {machineBlocks} from './layout.js';
import type {PveSystem} from './system.js';

// ---- entry open amount (shared with the client renderer, src/client/siren/stage.ts) -------------------------------
/** Seconds each entry takes to close again after `closeAt`. */
export const ENTRY_CLOSE:Record<EntryKind,number>={vent:.4,door:.7,cell:.6};
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
/** When an entry starts to open: a climbing add's hands reach the grate at `openAt+vent.push`; a diving add pulls it at `openAt`. */
export function entryStart(kind:EntryKind,v:Pick<PveEntryView,'openAt'|'mode'>){return kind==='vent'&&v.mode==='spawn'?v.openAt+PVE_TIMING.vent.push:v.openAt;}
/** Open amount 0..1 of an entry at time t: o(t)=smooth((t−start)/T_open)·(1−smooth((t−closeAt)/T_close)). Pure in time. */
export function entryOpenAmount(kind:EntryKind,v:PveEntryView|undefined,t:number){
 if(!v)return 0;const open=kind==='vent'?PVE_TIMING.vent.open:kind==='door'?PVE_TIMING.door.open:PVE_TIMING.cell.open;
 return smooth((t-entryStart(kind,v))/open)*(1-smooth((t-v.closeAt)/ENTRY_CLOSE[kind]));
}

export const VENT_PIT={
 /** Side of the square opening under the grate (m); the renderer's pit matches it. */
 hole:2.1,
 /** A capsule centre within this of the vent centre on both floor axes is over the hole (the body visibly over the pit). */
 fall:.78,
 /** The vent is a pit from this open amount on (the lid half swung; the client's pit is fully dark from here). */
 lethal:.5,
 /** Feet within this of standing height: a jump over the pit clears it. */
 jumpClear:.35,
 /** Seconds from the fall to the splat (the body lands at death.at + splatAt). */
 splatAt:10,
 /** The landing fall from above lasts this long: the death record is swapped to 'vent-splat' at landAt − drop. */
 drop:.6,
 /** Landing-spot search around the hall centre: ring step (m), points per ring, rings, and clearances (m). */
 land:{step:1.25,points:8,rings:8,boss:2.4,body:1.1,block:.75,vent:1.9},
 /** Bot avoidance: look-ahead (m) and the extra margin around the hole. */
 avoid:{look:1.3,margin:.85},
 /** Ward Zero outside PvE (PvP and the DEV sandbox, owner 2026-10-04): snapshots carry each vent opening `warn` + `lead` s
  *  before it opens, so every client shows a warning (rim fill, black wisps, rattling lid) for `warn` s and then draws the
  *  lid swinging `lead` s ahead of the authority (and shutting `lead` s after it). An online view drawn up to ~lead s behind
  *  the server therefore never shows a shut grate over a lethal pit. Presentation only: falls use the authority's times. */
 warn:1,lead:.5,
} as const;
export const VENT_FALL_EFFECT='vent-fall',VENT_SPLAT_EFFECT='vent-splat',VENT_FALL_KIND='vent-fall';
type DeathLike={effect:string;at:number}|undefined|null;
/** True while a vent-fall body is still in the shaft or in the air above the landing spot: no revive, no beacon. */
export function inVentShaft(death:DeathLike,time:number):boolean{
 return !!death&&(death.effect===VENT_FALL_EFFECT||death.effect===VENT_SPLAT_EFFECT&&time<death.at+VENT_PIT.drop-1e-9);
}
/** Ward Zero outside PvE: how open a vent is DRAWN at time t (leads the authority on opening, trails it on closing). */
export function wardVentDrawn(v:PveEntryView|undefined,t:number){
 return v?entryOpenAmount('vent',{...v,openAt:v.openAt-VENT_PIT.lead,closeAt:v.closeAt+VENT_PIT.lead},t):0;
}
/** Ward Zero outside PvE: the warning before the drawn lid starts to move, 0..1 over VENT_PIT.warn s (0 otherwise). */
export function wardVentWarning(v:PveEntryView|undefined,t:number){
 if(!v)return 0;const start=v.openAt-VENT_PIT.lead;return t>=start||t<start-VENT_PIT.warn?0:smooth(1-(start-t)/VENT_PIT.warn);
}
/** The Ward Zero vent openings a snapshot carries: started ones and those within warn + lead of starting. */
export const wardVentsSent=(entries:readonly PveEntryView[],t:number)=>entries.filter(e=>t>=e.openAt-VENT_PIT.lead-VENT_PIT.warn);
/** Open amounts of the 8 vents (entries 0–7) at time t. */
export function ventOpenAmounts(entries:readonly PveEntryView[],t:number):number[]{
 const out=SIREN_VENTS.map(()=>0);for(const e of entries)if(e.e>=0&&e.e<out.length)out[e.e]=Math.max(out[e.e],entryOpenAmount('vent',e,t));return out;
}
/** The vent whose hole contains floor point pos (within `half` of its centre on both axes), or -1. */
export function ventAt(pos:{x:number;z:number},half:number=VENT_PIT.fall){return SIREN_VENTS.findIndex(w=>Math.abs(pos.x-w.x)<=half&&Math.abs(pos.z-w.z)<=half);}

/** Static obstacles of the PvE hall (stage blocks above the floor and the machines), computed once. */
let OBSTACLES:ArenaBlock[]|undefined;
const obstacles=()=>OBSTACLES??=[...sirenStageBlocks(),...machineBlocks()].filter(b=>b.pos.y+b.size.y/2>.05&&b.pos.y-b.size.y/2<2.5&&b.size.x<60);
/** A clear landing spot nearest the hall centre: off pillars, props, machines and vents, away from the boss and other
 *  kept bodies. Rings of VENT_PIT.land.points around (0,0), first clear point in fixed order (deterministic). */
export function splatSpot(avoid:{boss?:{x:number;z:number;r:number};bodies?:readonly {x:number;z:number}[]}={}):{x:number;z:number}{
 const L=VENT_PIT.land,clear=(x:number,z:number)=>{
  for(const b of obstacles()){const hx=b.size.x/2+L.block,hz=b.size.z/2+L.block;
   // Rotated machine boxes: use the larger half extent on both axes (conservative).
   const h=b.rotation?Math.max(hx,hz):undefined;if(Math.abs(x-b.pos.x)<(h??hx)&&Math.abs(z-b.pos.z)<(h??hz))return false;}
  if(ventAt({x,z},L.vent)>=0)return false;
  if(avoid.boss&&Math.hypot(x-avoid.boss.x,z-avoid.boss.z)<avoid.boss.r+L.boss)return false;
  for(const q of avoid.bodies??[])if(Math.hypot(x-q.x,z-q.z)<L.body)return false;
  return true;};
 if(clear(0,0))return {x:0,z:0};
 for(let ring=1;ring<=L.rings;ring++){const n=L.points*ring;for(let k=0;k<n;k++){const a=k/n*Math.PI*2,x=Math.round(Math.sin(a)*ring*L.step*100)/100,z=Math.round(Math.cos(a)*ring*L.step*100)/100;if(clear(x,z))return {x,z};}}
 return {x:0,z:0};
}

/** The authoritative part: falls, the timed splat and bot avoidance. Owned by PveSystem (only on the Siren map). */
export class VentPits{
 /** Bodies in the shaft: player id → the vent-fall death id and its landing time. */
 private pending=new Map<string,{death:string;landAt:number}>();
 private cache={tick:-1,open:[] as number[],soon:[] as boolean[]};
 /** Shaft seconds before the splat (the DEV sandbox may shorten it), and whether a fall `lands` at all (PvP: no). */
 splatAt:number=VENT_PIT.splatAt;lands=true;
 get busy(){return this.pending.size>0;}
 constructor(readonly host:PveSystem){}
 private get sim(){return this.host.sim;}
 private active(){const s=this.sim;return s.options.map==='siren'&&s.phase==='fight';}
 reset(){this.pending.clear();this.cache.tick=-1;}
 /** Director holds: every absolute timer moves with the hold. */
 shift(dt:number){for(const [id,q] of this.pending){q.landAt+=dt;const p=this.sim.players.get(id);if(p?.death?.id===q.death&&p.death.landAt!==undefined)p.death.landAt+=dt;}}
 /** Vent open amounts now, and whether each vent is open or about to open (its entry is scheduled and not yet shut). */
 private vents(){
  const s=this.sim;if(this.cache.tick===s.tick)return this.cache;
  const entries=this.host.horde.views(this.host.slots()).entries,t=s.time;
  this.cache={tick:s.tick,open:ventOpenAmounts(entries,t),soon:SIREN_VENTS.map((_,e)=>entries.some(x=>x.e===e&&t<x.closeAt+ENTRY_CLOSE.vent))};
  return this.cache;
 }
 /** Players that can fall: connected, alive, visible party members and recruits (not the boss, never adds). */
 private canFall(p:Player){return p.connected&&p.hp>0&&!this.host.isBoss(p)&&!this.host.hidden(p)&&!(this.host.godMode&&this.host.isHuman(p))&&(this.host.isMember(p)||this.host.isRecruit(p));}
 /** After physics: open-vent falls, then due splats. */
 step(){
  if(!this.active())return;const s=this.sim;
  for(const p of s.players.values()){
   if(!this.canFall(p))continue;const pos=p.body.translation(),e=ventAt(pos);if(e<0)continue;
   if(pos.y>this.host.standAt(pos.x,pos.z,p).y+VENT_PIT.jumpClear)continue;
   if(this.vents().open[e]<VENT_PIT.lethal-1e-9)continue;
   this.fall(p,e);
  }
  for(const [id,q] of [...this.pending]){
   const p=s.players.get(id);if(!p||p.hp>0||p.death?.id!==q.death){this.pending.delete(id);continue;}
   if(s.time>=q.landAt-VENT_PIT.drop-1e-9){this.pending.delete(id);this.splat(p,q.landAt);}
  }
 }
 private fall(p:Player,e:number){
  const s=this.sim,pos=p.body.translation(),down=v(0,-1,0);
  // The ordinary damage path first (kill feed, ledger, attribution of the last attacker within attributionSeconds); a pit is
  // not an attack, so roll i-frames, the revive shield and chip saves do not hold the floor up: the kill is then forced.
  s.damage(p,1e5,null,VENT_FALL_KIND,down,0,undefined,false,true);
  if(p.hp>0){p.hp=0;p.lastDamage=s.time;s.recordDeath(p,VENT_FALL_KIND,down);}
  if(!p.death)return;
  const w=SIREN_VENTS[e];
  p.death.effect=VENT_FALL_EFFECT;p.death.kind=VENT_FALL_KIND;p.death.landAt=s.time+this.splatAt;
  p.death.pos={x:pos.x,y:pos.y,z:pos.z};p.death.velocity={x:0,y:-1,z:0};p.death.direction=down;p.death.wound=undefined;p.death.gibPart=undefined;p.wounds=undefined;
  p.body.setLinvel(v(),true);p.ext=v();p.move=v();
  s.emit('pve-vent-fall',{x:w.x,y:0,z:w.z},{target:p.id,message:'vent-'+e});
  // Recruits are released by their own rules; only party members keep a body that comes back down.
  if(this.lands&&this.host.isMember(p))this.pending.set(p.id,{death:p.death.id,landAt:p.death.landAt});
 }
 private splat(p:Player,landAt:number){
  const s=this.sim,old=p.death!,boss=this.host.boss(),b=boss&&boss.hp>0&&!this.host.hidden(boss)?boss.body.translation():undefined;
  const bodies=this.host.members().filter(q=>q.id!==p.id&&q.hp<=0&&q.death&&!inVentShaft(q.death,s.time)).map(q=>q.death!.pos);
  const spot=splatSpot({boss:b?{x:b.x,z:b.z,r:boss!.siren?.boost?.scale??1}:undefined,bodies}),pos=this.host.standAt(spot.x,spot.z,p);
  p.death={...old,id:s.uid('death'),at:landAt-VENT_PIT.drop,effect:VENT_SPLAT_EFFECT,pos,velocity:{x:0,y:-22,z:0},direction:{x:0,y:-1,z:0},landAt,
   wound:undefined,motion:undefined,hitPoint:undefined,gibPart:undefined,attacker:undefined};
  p.body.setTranslation(pos,true);p.body.setLinvel(v(),true);p.ext=v();p.move=v();
  s.emit('pve-vent-splat',pos,{target:p.id,message:'SPLAT!'});
 }
 /** Party bots and recruits steer around vents that are open or about to open (look-ahead along their move). */
 steer(p:Player,i:Input):Input{
  if(!this.active()||p.hp<=0||!(i.x||i.z))return i;
  const pos=p.body.translation(),m=Math.hypot(i.x,i.z),dx=i.x/m,dz=i.z/m,A=VENT_PIT.avoid,half=VENT_PIT.hole/2+A.margin;
  let x=i.x,z=i.z;
  for(let e=0;e<SIREN_VENTS.length;e++){const w=SIREN_VENTS[e];
   const ax=pos.x+dx*A.look,az=pos.z+dz*A.look,near=Math.abs(ax-w.x)<half&&Math.abs(az-w.z)<half||Math.abs(pos.x-w.x)<half&&Math.abs(pos.z-w.z)<half;
   if(!near||!this.vents().soon[e])continue;
   let ox=pos.x-w.x,oz=pos.z-w.z;const ol=Math.hypot(ox,oz)||1;ox/=ol;oz/=ol;
   const into=-(dx*ox+dz*oz);if(into<=0&&Math.max(Math.abs(pos.x-w.x),Math.abs(pos.z-w.z))>=half)continue;
   // Drop the component toward the vent and slide along its edge (the side the move already leans to), plus a push out.
   const side=dx*-oz+dz*ox>=0?1:-1,tx=-oz*side,tz=ox*side;
   x=tx*m*.9+ox*m*.45;z=tz*m*.9+oz*m*.45;break;
  }
  if(x===i.x&&z===i.z)return i;
  const l=Math.hypot(x,z),k=l>1?1/l:1;return {...i,x:x*k,z:z*k};
 }
}
