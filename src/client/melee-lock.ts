// Melee lock-on (owner 2026-10-04): Tab locks the nearest enemy in view, a Tab tap cycles to the next one, holding Tab
// (LOCK_ON.holdRelease) releases; the lock is gone when the target dies, disappears or leaves range. Client only: the
// steered yaw/pitch are the pilot's ordinary input, so the server, prediction and golden traces are unchanged. Pure.
import type {Snapshot,PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {sameTeam} from '../shared/teams.js';
import {decodeAdd,BOSS_PLAYER_ID} from '../shared/pve/contract.js';
import {VIEW} from '../shared/view.js';
import {SLENDER} from '../shared/leet-content.js';
import {type Vec,v,clamp,forward} from '../shared/config.js';

export const LOCK_ON={acquireRange:16,breakRange:24,cone:50*Math.PI/180,holdRelease:.4,turnRate:14,pitchLimit:1.1,lift:.2,pitchBias:.06} as const;
export type LockKind='player'|'add'|'minion'|'slender';
export interface LockTarget{key:string;kind:LockKind;id:string;point:Vec;name?:string}
const TAU=Math.PI*2,wrap=(a:number)=>a-TAU*Math.round(a/TAU);

/** Lock-on is offered only to a living melee pilot in a fight who controls their own body (Siren: melee mode only). */
export function lockEligible(state:Snapshot,id:string):boolean{
 const me=state.players.find(p=>p.id===id);
 return !!me&&state.phase==='fight'&&me.hp>0&&me.connected&&WEAPONS[me.spec]?.class==='melee'&&!(me.spec==='siren'&&me.siren?.mode==='ranged')&&!me.leet?.controlling&&!me.leet?.controlledBy&&!state.pve?.hold;
}
/** Every enemy body a melee swing can hit, with the point the camera should aim at. Allies never (friendly fire or not). */
export function lockTargets(state:Snapshot,id:string):LockTarget[]{
 const me=state.players.find(p=>p.id===id),mode=state.options.gameMode,out:LockTarget[]=[];
 const enemyOwner=(owner:string)=>owner!==id&&!sameTeam(me,state.players.find(p=>p.id===owner),mode);
 for(const p of state.players){
  if(p.id===id||!p.connected||p.hp<=0||sameTeam(me,p,mode))continue;
  if(p.id===BOSS_PLAYER_ID&&state.pve?.bossView?.hidden)continue;
  const k=p.siren?.boost?.scale??1;out.push({key:'p:'+p.id,kind:'player',id:p.id,point:v(p.pos.x,p.pos.y+LOCK_ON.lift*k,p.pos.z),name:p.name});
 }
 const pve=state.pve;
 if(pve)for(const t of pve.adds){const a=decodeAdd(t,pve.slots);if(a.hp<=0||!(a.state==='active'||a.state==='crawl'||a.state==='stagger'))continue;
  out.push({key:'a:'+a.id,kind:'add',id:a.id,point:v(a.pos.x,a.pos.y+(a.state==='crawl'?0:LOCK_ON.lift),a.pos.z)});}
 for(const m of state.minions??[])if(m.hp>0&&enemyOwner(m.owner))out.push({key:'m:'+m.id,kind:'minion',id:m.id,point:v(m.pos.x,m.pos.y+LOCK_ON.lift,m.pos.z)});
 for(const f of state.leetFields??[])if(f.kind==='tree'&&f.walker&&f.slender&&(f.hp??1)>0&&enemyOwner(f.owner))
  out.push({key:'m:'+f.slender,kind:'slender',id:f.slender,point:v(f.walker.x,f.walker.y+SLENDER.center+.25,f.walker.z)});
 return out;
}
export function aimAt(eye:Vec,point:Vec){const dx=point.x-eye.x,dy=point.y-eye.y,dz=point.z-eye.z,flat=Math.hypot(dx,dz);
 return {yaw:Math.atan2(dx,dz),pitch:clamp(Math.atan2(dy,flat)-LOCK_ON.pitchBias,-LOCK_ON.pitchLimit,LOCK_ON.pitchLimit),distance:Math.hypot(flat,dy)};}
const eyeOf=(pos:Vec)=>v(pos.x,pos.y+VIEW.eyeHeight,pos.z);
/** Nearest valid target in view: inside the cone around the look direction and the acquire range; centre first, then near. */
export function acquire(list:LockTarget[],pos:Vec,yaw:number,pitch:number):LockTarget|undefined{
 const eye=eyeOf(pos),look=forward(yaw,pitch);let best:LockTarget|undefined,score=Infinity;
 for(const t of list){const d=v(t.point.x-eye.x,t.point.y-eye.y,t.point.z-eye.z),l=Math.hypot(d.x,d.y,d.z);if(l>LOCK_ON.acquireRange||l<1e-3)continue;
  const angle=Math.acos(clamp((d.x*look.x+d.y*look.y+d.z*look.z)/l,-1,1));if(angle>LOCK_ON.cone)continue;
  const s=angle/LOCK_ON.cone+l/LOCK_ON.acquireRange;if(s<score||s===score&&t.key<(best?.key??'')){score=s;best=t;}}
 return best;
}
/** Tab tap while locked: the next target clockwise (to the right on screen) around the pilot, within acquire range; wraps. */
export function cycle(list:LockTarget[],pos:Vec,current:string):LockTarget|undefined{
 const eye=eyeOf(pos),cur=list.find(t=>t.key===current);if(!cur)return undefined;const from=aimAt(eye,cur.point).yaw;let best:LockTarget|undefined,gap=Infinity;
 for(const t of list){if(t.key===current)continue;const a=aimAt(eye,t.point);if(a.distance>LOCK_ON.acquireRange)continue;
  // Turning right decreases yaw (mouse: yaw -= movementX), so "next to the right" is the smallest positive (from - yaw).
  let g=((from-a.yaw)%TAU+TAU)%TAU;if(g<1e-6)g=TAU;if(g<gap||g===gap&&t.key<(best?.key??'')){gap=g;best=t;}}
 return best??cur;
}
/** Exponential approach along the shortest arc; frame-rate independent. */
export function steer(yaw:number,pitch:number,pos:Vec,point:Vec,dt:number){const a=aimAt(eyeOf(pos),point),k=1-Math.exp(-LOCK_ON.turnRate*Math.max(0,dt));
 return {yaw:yaw+wrap(a.yaw-yaw)*k,pitch:pitch+(a.pitch-pitch)*k,distance:a.distance};}

/** Tab state machine. press/release from key events (non-repeat), tick each frame. */
export class LockOn{
 key:string|undefined;private downAt:number|undefined;private acquiredThisPress=false;private released=false;
 get active(){return this.key!==undefined;}
 clear(){this.key=undefined;this.downAt=undefined;}
 /** Window blur / alt-tab: forget a Tab press in progress (it neither cycles nor releases later). */
 cancelPress(){this.downAt=undefined;}
 press(now:number,state:Snapshot,id:string,pos:Vec,yaw:number,pitch:number){this.downAt=now;this.released=false;this.acquiredThisPress=false;
  if(!this.key){this.key=acquire(lockTargets(state,id),pos,yaw,pitch)?.key;this.acquiredThisPress=true;}}
 release(now:number,state:Snapshot,id:string,pos:Vec){if(this.downAt===undefined)return;const held=(now-this.downAt)/1000;this.downAt=undefined;
  if(this.released||this.acquiredThisPress||!this.key)return;
  // A hold the frame loop has not seen yet (low frame rate) still releases on key-up; a tap cycles.
  if(held>=LOCK_ON.holdRelease){this.released=true;this.key=undefined;return;}this.key=cycle(lockTargets(state,id),pos,this.key)?.key;}
 /** Returns the live target (or undefined after any release condition). */
 tick(now:number,state:Snapshot,id:string,pos:Vec):LockTarget|undefined{
  if(this.downAt!==undefined&&!this.acquiredThisPress&&!this.released&&this.key&&(now-this.downAt)/1000>=LOCK_ON.holdRelease){this.released=true;this.key=undefined;}
  if(!this.key)return undefined;if(!lockEligible(state,id)){this.key=undefined;return undefined;}
  const t=lockTargets(state,id).find(t=>t.key===this.key);if(!t){this.key=undefined;return undefined;}// dead, removed, hidden, ally, disconnected
  const e=eyeOf(pos);if(Math.hypot(t.point.x-e.x,t.point.y-e.y,t.point.z-e.z)>LOCK_ON.breakRange){this.key=undefined;return undefined;}
  return t;
 }
}
