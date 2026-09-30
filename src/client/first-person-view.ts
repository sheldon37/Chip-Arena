import type * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {animateCharacter,type AnimatedRig} from './character-animation.js';
/** First-person view model: animates the camera-attached rig, then places the view root (moved verbatim from the renderer, same order).
 * The renderer hides the rig's head, legs and torso meshes once at construction and marks it with markFirstPersonRig. */
export function poseFirstPersonView(firstRig:AnimatedRig,viewRoot:T.Object3D,self:PlayerView,time:number,dt:number,scoped:boolean){
 animateCharacter(firstRig,self,time,dt,0);
 viewRoot.position.set((self.spec==='ansatsuken'||self.spec==='emperor')?0:.24,-.48,-.20);viewRoot.rotation.set(0,Math.PI,0);viewRoot.scale.setScalar(.78);viewRoot.visible=!scoped;
}
