/**
 * Siren visuals façade: the only module the systems branch imports for Siren presentation. Owned by the visuals
 * branch (src/client/siren/**). The signatures here are the contract; see docs/SIREN.md for where each call is wired.
 */
import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import type {PlayerView,Snapshot,GameEvent} from '../../shared/simulation.js';
import type {KillEffectFactory} from '../death-effects.js';
import {SIREN} from '../../shared/siren-contract.js';
import {buildSiren,sirenParts} from './model.js';
import {animateSirenRig} from './motion.js';
import {SirenFx,markBeeps} from './fx.js';
import {sirenCrush,sirenDissipate} from './kills.js';
import {SirenIntroScene} from './intro.js';
import {WardZeroVisuals} from './stage.js';
import {sirenViewModel} from './first-person.js';

export {markBeeps};

export function isSirenRig(rig:{group:T.Object3D}){return !!sirenParts(rig);}

/** Turns a standard rig into the Siren apparition. Idempotent. Hides weapons and cosmetics (Siren overrides the
 * player's look). The renderer must rebuild the rig when a player's spec changes to or from 'siren'. */
export function sirenify(rig:CharacterRig){buildSiren(rig);}

/** Call right after animateCharacter for a Siren (same arguments, plus the snapshot). Overrides the pose. */
export function animateSiren(rig:CharacterRig,p:PlayerView,state:Snapshot,time:number,dt:number,pitch:number){animateSirenRig(rig,p,state,time,dt,pitch);}

/** First person: call after poseFirstPersonView when the local player is the Siren (claws / burning palms). */
export function sirenFirstPerson(view:CharacterRig,p:PlayerView,time:number,dt:number){sirenViewModel(view,p,time,dt);}

/** Every Siren projectile, fire patch, eruption, rain, mark, slash and grab presentation. */
export class SirenEffects {
 private fx:SirenFx;
 constructor(scene:T.Scene){this.fx=new SirenFx(scene);}
 /** True for projectiles this class renders; the renderer must skip its default mesh and trail for them. */
 owns(shot:object){return this.fx.owns(shot);}
 update(state:Snapshot,time:number,rigs:ReadonlyMap<string,CharacterRig>,quality:number,camera?:T.Camera){this.fx.update(state,time,rigs,quality,camera);}
 event(e:GameEvent,time:number,quality?:number){this.fx.event(e,time,quality);}
 clear(){this.fx.clear();}
}

/** The dev-siren intro scene. While active it owns the intro scene's lights, background, fog and camera. */
export class SirenIntroStage {
 private scene:SirenIntroScene;
 constructor(scene:T.Scene){this.scene=new SirenIntroScene(scene);}
 get group(){return this.scene.group;}
 setActive(on:boolean){this.scene.setActive(on);}
 update(age:number,rig:CharacterRig,camera:T.PerspectiveCamera,quality:number){this.scene.update(age,rig,camera,quality);}
}

/** Ward Zero stage dressing and its music-synced animation (map 'siren'). */
export class SirenStageVisuals {
 private ward:WardZeroVisuals;
 constructor(scene:T.Scene){this.ward=new WardZeroVisuals(scene);}
 get group(){return this.ward.group;}
 setActive(on:boolean){this.ward.setActive(on);}
 update(state:Snapshot,time:number,quality:number,camera?:T.Camera){this.ward.update(state,time,quality,camera);}
}

/** Death effects: register in death-effects.ts KILL_EFFECTS. */
export const SIREN_KILL_EFFECTS:Record<typeof SIREN.deaths[keyof typeof SIREN.deaths],KillEffectFactory>={
 [SIREN.deaths.crush]:sirenCrush,
 [SIREN.deaths.dissipate]:sirenDissipate,
};
