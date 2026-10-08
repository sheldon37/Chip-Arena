// PveVisuals facade (design §8.1). FROZEN by the foundation. The renderer calls it every frame; it is a no-op while the
// snapshot carries no `pve`. HordeView (S5) draws adds, corpses, gore and the crowd; WorldView (S6) draws machines,
// pickups, entries, holograms, beacons, recruits and the grown boss. Every presentation module is a pure function of
// (view, state.time) plus bounded, dt-clamped smoothing that resets on time jumps (design §2.7).
import * as T from 'three';
import type {Vec} from '../../shared/config.js';
import type {GameEvent,PlayerView,Snapshot} from '../../shared/simulation.js';
import {pveOf,type PveView} from '../../shared/pve/contract.js';
import {HordeView} from './horde-view.js';
import {WorldView} from './world-view.js';

/** Structural subset of the renderer's Rig that PvE presentation may touch. */
export interface PveRigRef {group:T.Group; head:T.Group; rightArm:T.Group; weapon:T.Group}
/** Everything the renderer hands to PvE presentation each frame (renderer.ts builds it in place). */
export interface PveVisualsContext {camera:T.PerspectiveCamera; me:string; dt:number; quality:number; compatibility:boolean;
 replay:boolean; rigs:ReadonlyMap<string,PveRigRef>; burst:(pos:Vec,color:number,count:number,power:number)=>void}
/** The boss rig stays hidden until bossView.foundAt + this (longer than the 350 ms maximum interpolation delay, §2.6). */
export const BOSS_FOUND_HIDE_SECONDS=.4;

export class PveVisuals {
 horde:HordeView;world:WorldView;
 private active=false;
 constructor(readonly scene:T.Scene){this.horde=new HordeView(scene);this.world=new WorldView(scene);}
 update(state:Snapshot,ctx:PveVisualsContext){
  const view=pveOf(state);
  if(!view){if(this.active)this.clear();return;}
  this.active=true;this.horde.update(state,view,ctx);this.world.update(state,view,ctx);
 }
 /** false hides the rig: the hidden boss (and until foundAt+0.4), a recruit before its creation time; otherwise WorldView
  *  decides (scale, choosing pose). Called by the renderer after the rig animation, only while the rig is visible. */
 decoratePlayer(rig:PveRigRef,p:PlayerView,state:Snapshot):boolean{
  const view=pveOf(state);if(!view)return true;
  if(!playerShown(p,view,state.time))return false;
  return this.world.decoratePlayer(rig,p,state,view);
 }
 event(e:GameEvent,state:Snapshot){const view=pveOf(state);if(!view)return;this.horde.event(e,state,view);this.world.event(e,state,view);}
 metrics():{fullRigs:number;nearImpostors:number;adds:number}{return this.horde.metrics();}
 clear(){this.active=false;this.horde.clear();this.world.clear();}
}
/** Pure visibility rule shared by the facade and tests. */
export function playerShown(p:Pick<PlayerView,'id'>,view:PveView,time:number){
 const b=view.bossView;
 if(b&&p.id===b.id&&(b.hidden||b.foundAt!==undefined&&time<b.foundAt+BOSS_FOUND_HIDE_SECONDS))return false;
 const recruit=view.recruits.find(r=>r.id===p.id);if(recruit&&time<recruit.at)return false;
 return true;
}
