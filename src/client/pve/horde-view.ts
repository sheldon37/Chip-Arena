// SEED created by the foundation; owned by stream S5 (HORDE VISUALS) after the foundation merges. Draws nothing.
// S5 replaces the body (design §8.2-§8.5); keep the class name, constructor and method signatures: PveVisuals calls them.
import type * as T from 'three';
import type {GameEvent,Snapshot} from '../../shared/simulation.js';
import type {PveView} from '../../shared/pve/contract.js';
import type {PveVisualsContext} from './visuals.js';

export class HordeView {
 constructor(readonly scene:T.Scene){}
 /** Every frame while state.pve exists. */
 update(_state:Snapshot,_view:PveView,_ctx:PveVisualsContext){}
 /** Every new snapshot event (pve-kill, pve-gib, minionHit, …). */
 event(_e:GameEvent,_state:Snapshot,_view:PveView){}
 /** getMetrics() fields (§7.8). */
 metrics(){return {fullRigs:0,nearImpostors:0,adds:0};}
 /** Remove everything from the scene (PvE ended or snapshot without pve). */
 clear(){}
}
