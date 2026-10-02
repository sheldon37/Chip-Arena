// SEED created by the foundation; owned by stream S6 (WORLD VISUALS) after the foundation merges. Draws nothing.
// S6 replaces the body (design §8.4, §8.6); keep the class name, constructor and method signatures: PveVisuals calls them.
import type * as T from 'three';
import type {GameEvent,PlayerView,Snapshot} from '../../shared/simulation.js';
import type {PveView} from '../../shared/pve/contract.js';
import type {PveRigRef,PveVisualsContext} from './visuals.js';

export class WorldView {
 constructor(readonly scene:T.Scene){}
 update(_state:Snapshot,_view:PveView,_ctx:PveVisualsContext){}
 /** Per player rig after its animation: scale (grown boss), choosing pose, weapon away. false hides the rig. */
 decoratePlayer(_rig:PveRigRef,_p:PlayerView,_state:Snapshot,_view:PveView){return true;}
 event(_e:GameEvent,_state:Snapshot,_view:PveView){}
 clear(){}
}
