// PvE machines and interaction geometry (design §4.2, §4.3, §4.6; S3). Pure helpers over the F layout data: what an F press
// reaches, which respawn pad to use, and whether an add is near. The Economy owns the state and the flow.
import type {Vec} from '../config.js';
import type {AddRef,PvePickupView} from './contract.js';
import {PVE_MACHINES,PVE_LAYOUT,type PveMachine} from './layout.js';
import {PRICES} from './balance.js';

const flat=(a:{x:number;z:number},b:{x:number;z:number})=>Math.hypot(a.x-b.x,a.z-b.z);
/** Adds that count as present for safety checks (auto-open, respawn pads): standing in the hall and alive. */
export const activeAdd=(a:AddRef)=>a.state==='active'||a.state==='crawl'||a.state==='stagger';
/** True when an active add stands within `radius` of `pos` (floor plane). */
export function addNear(adds:readonly AddRef[],pos:{x:number;z:number},radius:number){return adds.some(a=>activeAdd(a)&&flat(a.pos,pos)<=radius);}

export type InteractTarget=
 |{kind:'pickup';pickup:PvePickupView;d:number}
 |{kind:'revive';target:string;d:number}
 |{kind:'machine';machine:PveMachine;d:number};
/** §4.3 priority: own grabbable pickup within PRICES.grabRadius, then a downed teammate within reviveRadius, then a machine
 *  front within interactRadius; the nearest wins inside each class. */
export function interactTarget(pos:Vec,owner:string,time:number,pickups:readonly PvePickupView[],downed:readonly {id:string;pos:Vec}[]):InteractTarget|null{
 let best:InteractTarget|null=null;
 for(const k of pickups){if(k.owner!==owner||time<k.born-1e-9)continue;const d=flat(k.pos,pos);if(d<=PRICES.grabRadius&&(!best||d<best.d))best={kind:'pickup',pickup:k,d};}
 if(best)return best;
 for(const t of downed){const d=flat(t.pos,pos);if(d<=PRICES.reviveRadius&&(!best||d<best.d))best={kind:'revive',target:t.id,d};}
 if(best)return best;
 for(const m of PVE_MACHINES){const d=flat(m.front,pos);if(d<=PRICES.interactRadius&&(!best||d<best.d))best={kind:'machine',machine:m,d};}
 return best;
}
/** §4.6 respawn pad: the pad nearest `from` with no active add within `clear` m; if none qualifies, the pad farthest from every add. */
export function respawnPad(from:{x:number;z:number},adds:readonly AddRef[],clear:number,taken:readonly {x:number;z:number}[]=[]):{x:number;z:number}{
 const pads=PVE_LAYOUT.pads,live=adds.filter(activeAdd);
 const free=pads.filter(p=>!addNear(live,p,clear)),pool=free.filter(p=>!taken.some(t=>flat(t,p)<.5));
 const choose=pool.length?pool:free;
 if(choose.length)return choose.reduce((a,b)=>flat(b,from)<flat(a,from)?b:a);
 const room=(p:{x:number;z:number})=>live.reduce((m,a)=>Math.min(m,flat(a.pos,p)),Infinity);
 return pads.reduce((a,b)=>room(b)>room(a)?b:a);
}
export const machineById=(m:number)=>PVE_MACHINES[m];
