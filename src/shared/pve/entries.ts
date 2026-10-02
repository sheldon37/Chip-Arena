// PvE entries (design §3.3, stream S1): door, cell and vent queues with the shared PVE_TIMING timelines, the seeded entry
// choice and the rate-independent spawn budget. Pure: no Rapier, no Simulation, no clocks; time is passed in.
import {sirenStageHazards} from '../siren-stage-layout.js';
import type {EntryKind,PveEntryView} from './contract.js';
import {PVE_TIMING} from './contract.js';
import {HORDE} from './balance.js';
import {PVE_ENTRIES,type PveEntry} from './layout.js';

/** Timeline of one add through an entry, relative to its own spawnAt. `walk` is when the walk (door, cell) starts;
 *  `active` is when the body is created. The first add through a closed entry pays the push / leaf / gate opening;
 *  followers through an entry that is already open skip it (§3.3). */
export function entryTimeline(kind:EntryKind,first:boolean):{walk:number;walkFor:number;active:number}{
 if(kind==='vent'){const lead=first?0:PVE_TIMING.vent.push;return {walk:0,walkFor:0,active:PVE_TIMING.vent.climb-lead};}
 const t=kind==='door'?PVE_TIMING.door:PVE_TIMING.cell,open=first?t.open:0;
 return {walk:open,walkFor:t.walk,active:open+t.walk};
}
export const entryGap=(kind:EntryKind)=>HORDE.entry.gap[kind];

export interface EntrySlot{e:number;openAt:number;closeAt:number;lastStart:number;queue:number[];mode:'spawn'|'dive';used:boolean}
export interface Scheduled{e:number;spawnAt:number;walkAt:number;activeAt:number;first:boolean}

/** Per-entry FIFO queues and open/close times. */
export class EntryScheduler{
 slots:EntrySlot[]=[];
 constructor(readonly entries:readonly PveEntry[]=PVE_ENTRIES){this.reset();}
 reset(){this.slots=this.entries.map(en=>({e:en.e,openAt:0,closeAt:-Infinity,lastStart:-Infinity,queue:[],mode:'spawn' as const,used:false}));}
 isOpen(e:number,time:number){const s=this.slots[e];return !!s&&s.used&&time<s.closeAt;}
 queueLength(e:number){return this.slots[e]?.queue.length??0;}
 /** Schedules add n through entry e at `time`. Opens a closed entry (openAt=time); followers start `gap` after the
  *  previous start and skip the opening. The entry closes HORDE.entry.closeAfter after the last scheduled exit. */
 schedule(e:number,n:number,time:number):Scheduled{
  const s=this.slots[e],kind=this.entries[e].kind,open=this.isOpen(e,time);
  let start=time,first=true;
  if(open){start=Math.max(time,s.lastStart+entryGap(kind));first=false;}
  else{s.openAt=time;s.mode='spawn';s.used=true;}
  const t=entryTimeline(kind,first);
  s.lastStart=start;s.queue.push(n);s.closeAt=Math.max(s.closeAt,start+t.active+HORDE.entry.closeAfter);
  return {e,spawnAt:start,walkAt:start+t.walk,activeAt:start+t.active,first};
 }
 /** A vent opened from below by a diving add (mode 'dive'): the grate is pulled up for PVE_TIMING.vent.dive. */
 dive(e:number,time:number){
  const s=this.slots[e];if(!s)return;
  if(!this.isOpen(e,time)){s.openAt=time;s.mode='dive';s.used=true;}
  s.closeAt=Math.max(s.closeAt,time+PVE_TIMING.vent.dive+HORDE.entry.closeAfter);
 }
 /** The add left the queue (activated or removed). */
 leave(e:number,n:number){const s=this.slots[e];if(!s)return;const i=s.queue.indexOf(n);if(i>=0)s.queue.splice(i,1);}
 shift(dt:number){for(const s of this.slots){s.openAt+=dt;s.closeAt+=dt;s.lastStart+=dt;}}
 /** Entries active in the last 4 s (contract PveEntryView). */
 views(time:number):PveEntryView[]{
  const out:PveEntryView[]=[];
  for(const s of this.slots)if(s.used&&s.closeAt>time-4)out.push({e:s.e,openAt:r2(s.openAt),closeAt:r2(s.closeAt),mode:s.mode});
  return out;
 }
}
const r2=(n:number)=>Math.round(n*100)/100;

/** True when vent `e` (0–7) shows a hazard warning or is active at any time in [time, time+span] (sampled every 0.25 s).
 *  songStart is the boss song's start (sim.bossSong.startedAt); without a song there are no vent hazards. */
export function ventHazard(e:number,time:number,songStart:number|undefined,span=PVE_TIMING.vent.climb){
 if(songStart===undefined||e<0||e>7)return false;
 for(let t=0;t<=span+1e-9;t+=.25){const h=sirenStageHazards(time+t-songStart)[e];if(h&&(h.warning>0||h.active))return true;}
 return false;
}

export interface EntryChoiceContext{
 time:number;
 /** Living party members' positions (x,z). */ party:readonly {x:number;z:number}[];
 /** Boss song start time, or undefined. */ songStart?:number;
 wave:boolean; possession:boolean;
 /** Restrict to these entry kinds (default all). */ kinds?:readonly EntryKind[];
}
/** Weight of entry `en` for a spawn, or 0 when it is not eligible (§3.3 a–c plus the distance weights). */
export function entryWeight(en:PveEntry,sched:EntryScheduler,ctx:EntryChoiceContext):number{
 if(ctx.kinds&&!ctx.kinds.includes(en.kind))return 0;
 if(sched.queueLength(en.e)>=HORDE.entry.queue)return 0;
 let near=Infinity;for(const p of ctx.party)near=Math.min(near,Math.hypot(p.x-en.exit.x,p.z-en.exit.z));
 if(near<HORDE.entry.minPlayerDistance)return 0;
 if(en.kind==='vent'&&ventHazard(en.e,ctx.time,ctx.songStart))return 0;
 let w=near<10?.4:near<=30?1:.6;
 if(ctx.wave&&en.kind!=='vent')w*=HORDE.entry.doorWeightWaves;
 if(ctx.possession&&en.kind==='vent')w*=HORDE.entry.ventWeightPossession;
 return w;
}
/** Seeded weighted pick over eligible entries (fixed index order), or -1 when none is eligible (the request waits). */
export function chooseEntry(sched:EntryScheduler,ctx:EntryChoiceContext,rng:{next():number}):number{
 const weights=sched.entries.map(en=>entryWeight(en,sched,ctx)),total=weights.reduce((a,b)=>a+b,0);
 if(total<=0)return -1;
 let roll=rng.next()*total;
 for(let e=0;e<weights.length;e++){if(weights[e]<=0)continue;roll-=weights[e];if(roll<0)return e;}
 for(let e=weights.length-1;e>=0;e--)if(weights[e]>0)return e;
 return -1;
}

/** Rate-independent spawn pacing: budget += rate·dt releases whole adds; while blocked the budget holds at 1. */
export class SpawnBudget{
 acc=0;released=0;
 reset(){this.acc=0;this.released=0;}
 /** Advances by dt at `rate` adds per second and returns how many adds may be released now (≤ room). */
 step(dt:number,rate:number,room:number,left:number):number{
  if(!(rate>0)||left===0){this.acc=Math.min(this.acc,1);return 0;}
  this.acc+=rate*dt;
  let n=Math.floor(this.acc+1e-9);
  if(left>0)n=Math.min(n,left);
  n=Math.max(0,Math.min(n,room));
  this.acc-=n;if(n===0||room<=n)this.acc=Math.min(this.acc,1);   // blocked: hold at most one pending release
  this.released+=n;return n;
 }
}
