// PvE boss look (design §6.5, §6.6, §8.6, stream S6): the grown Siren ("Am I beautiful?") scales 1 → boost.scale over
// one second from boost.at with its feet kept on the floor; possession ("Find me...") dissolves the boss into black flame
// that streaks to the possessed add; when it is found it bursts back out. Pure scale helpers are shared with
// siren/kills.ts (dissipate), siren/fx.ts (ground offsets) and siren/cutscene.ts (framing).
import * as T from 'three';
import type {GameEvent,PlayerView} from '../../shared/simulation.js';
import type {SirenBoost} from '../../shared/siren-contract.js';
import {sirenOf} from '../../shared/siren-contract.js';
import {decodeAdd,PVE_EVENT,type PveView} from '../../shared/pve/contract.js';
import {FlameField} from '../siren/flames.js';
import type {PveRigRef} from './visuals.js';

/** Seconds the growth takes; capsule half height + radius of a standard player (the rig origin to the feet). */
export const GROW_SECONDS=1,RIG_FOOT=.85,STREAK_SECONDS=1;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
/** The rig scale at time t: lerp(1, boost.scale, smooth((t − boost.at)/1)); 1 without a boost. */
export function bossVisualScale(boost:Pick<SirenBoost,'scale'|'at'>|undefined,t:number){return boost?1+(boost.scale-1)*smooth((t-boost.at)/GROW_SECONDS):1;}
/** The full (target) scale of a player's boost: what the simulation's capsule already uses. */
export const bossScaleOf=(p:object|undefined)=>p?sirenOf(p)?.boost?.scale??1:1;
/** Rig transform for a (possibly grown) Siren: scale about the capsule centre, and a y offset that keeps the feet on the
 *  floor while the visual scale lags the capsule (which grows at once). */
export function bossRigTransform(p:object,t:number){const b=sirenOf(p)?.boost,s=bossVisualScale(b,t);return {scale:s,dy:-((b?.scale??1)-s)*RIG_FOOT};}
export function applyBossLook(rig:PveRigRef,p:PlayerView,t:number){const {scale,dy}=bossRigTransform(p,t);rig.group.scale.setScalar(scale);rig.group.position.y+=dy;return scale;}

/** Streak position at time t from the boss (where it vanished) to the possessed add, over STREAK_SECONDS. */
export function streakPoint(from:{x:number;y:number;z:number},to:{x:number;y:number;z:number},at:number,t:number){
 const k=smooth((t-at)/STREAK_SECONDS);return {k,x:from.x+(to.x-from.x)*k,y:from.y+(to.y-from.y)*k+.9*Math.sin(Math.PI*k),z:from.z+(to.z-from.z)*k};
}
interface Streak{from:{x:number;y:number;z:number};n:number;at:number;acc:number;burst:boolean}
/** Black-flame presentation of possession, reveal and growth (one FlameField draw). */
export class BossLook {
 readonly fire=new FlameField(700);private streaks:Streak[]=[];private last=0;private growAcc=0;
 constructor(parent:T.Object3D){this.fire.mesh.name='pve-boss-flames';parent.add(this.fire.mesh);}
 event(e:GameEvent,view:PveView){
  const p=e.pos;if(!p)return;
  if(e.type===PVE_EVENT.possess){const n=Number(String(e.target??'').replace(/^\D+/,''))||view.bossView?.possessed||0;this.streaks.push({from:{...p},n,at:e.time,acc:0,burst:false});
   this.fire.burst(p.x,p.y,p.z,90,4.5,1.1,.16,'black',1.4,2);this.fire.burst(p.x,p.y+.6,p.z,24,3,.7,.06,'ember',1);}
  else if(e.type===PVE_EVENT.found){this.fire.burst(p.x,p.y,p.z,110,5,1.2,.17,'black',1.6,2);this.fire.burst(p.x,p.y+.8,p.z,30,4,.8,.06,'ember',1.2);this.fire.burst(p.x,p.y,p.z,30,6,.5,.08,'white',.6);}
 }
 update(players:readonly PlayerView[],view:PveView,t:number,quality:number){
  const dt=clamp(t-this.last,0,.05);this.last=t;const q=quality<.5?.5:1;
  for(let i=this.streaks.length-1;i>=0;i--){const s=this.streaks[i],tuple=view.adds.find(a=>a[0]===s.n),add=tuple?decodeAdd(tuple,view.slots):undefined;
   if(t<s.at-.5||t>s.at+STREAK_SECONDS+.2||!add){if(t>s.at+STREAK_SECONDS+.2||t<s.at-.5)this.streaks.splice(i,1);continue;}
   const to={x:add.pos.x,y:add.pos.y+.6,z:add.pos.z},pt=streakPoint(s.from,to,s.at,t);
   s.acc+=dt*q*160;while(s.acc>=1){s.acc-=1;this.fire.emit(pt.x+(Math.random()-.5)*.25,pt.y+(Math.random()-.5)*.25,pt.z+(Math.random()-.5)*.25,(Math.random()-.5)*.6,.4+Math.random()*.6,(Math.random()-.5)*.6,.5+Math.random()*.3,.1+Math.random()*.08,Math.random()<.08?'ember':Math.random()<.12?'white':'black',1,2);}
   if(pt.k>=1&&!s.burst){s.burst=true;this.fire.burst(to.x,to.y,to.z,40,2.5,.8,.12,'black',1.2);}}
  // Growth: black flame curls up the boss while it swells.
  for(const p of players){const b=sirenOf(p)?.boost;if(!b||p.hp<=0)continue;const k=(t-b.at)/GROW_SECONDS;if(k<0||k>1.2)continue;
   this.growAcc+=dt*q*120*(1-clamp(k-1,0,1)*5);while(this.growAcc>=1){this.growAcc-=1;const a=Math.random()*Math.PI*2,r=.5+.6*bossVisualScale(b,t);
    this.fire.emit(p.pos.x+Math.cos(a)*r,p.pos.y-RIG_FOOT*b.scale+Math.random()*.3,p.pos.z+Math.sin(a)*r,-Math.cos(a)*.4,2.5+Math.random()*2,-Math.sin(a)*.4,.7,.14,Math.random()<.1?'ember':'black',2,1.4);}}
  this.fire.update(dt);
 }
 clear(){this.streaks=[];this.fire.clear();}
}
