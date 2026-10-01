/**
 * Siren visuals façade: the only module the systems branch imports for Siren presentation. Owned by the visuals
 * branch (src/client/siren/**). The signatures here are the contract; the implementations behind them grow on
 * claude/visual-revision-siren. See docs/SIREN.md for where each call is wired.
 */
import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import type {PlayerView,Snapshot,GameEvent} from '../../shared/simulation.js';
import type {KillEffectFactory} from '../death-effects.js';
import {ragdoll,ragdollVariant} from '../ragdoll.js';
import {SIREN,SIREN_HOVER,sirenShotOf,sirenWorldOf} from '../../shared/siren-contract.js';

const SIREN_KEY='sirenApparition';
const ink=new T.MeshLambertMaterial({color:0x08070b,emissive:0x0d0a14});
const glow=new T.MeshBasicMaterial({color:0xf4f1ff,toneMapped:false});
const box=new T.BoxGeometry(1,1,1);

export function isSirenRig(rig:{group:T.Object3D}){return !!rig.group.userData[SIREN_KEY];}

/** Turns a standard rig into the Siren apparition. Idempotent. Hides weapons and cosmetics (Siren overrides the
 * player's look). The renderer must rebuild the rig when a player's spec changes to or from 'siren'. */
export function sirenify(rig:CharacterRig){
 if(isSirenRig(rig))return;rig.group.userData[SIREN_KEY]=true;
 rig.group.traverse(o=>{if(o instanceof T.Mesh)o.material=ink;});
 for(const s of [rig.sockets.head,rig.sockets.chest,rig.sockets.back,rig.weapon,rig.offWeapon])s.visible=false;
 for(const x of [-.075,.075]){const eye=new T.Mesh(box,glow);eye.name='siren-eye';eye.position.set(x,.2,.165);eye.scale.set(.085,.06,.01);rig.head.add(eye);}
 const grin=new T.Mesh(box,glow);grin.name='siren-grin';grin.position.set(0,.08,.165);grin.scale.set(.2,.03,.01);rig.head.add(grin);
}

/** Call right after animateCharacter for a Siren (same arguments, plus the snapshot). Overrides the pose. */
export function animateSiren(rig:CharacterRig,p:PlayerView,state:Snapshot,time:number,_dt:number,_pitch:number){
 void p;void state;
 rig.body.position.y=SIREN_HOVER.height+Math.sin(time*Math.PI*2*SIREN_HOVER.bobHz)*SIREN_HOVER.bob;
 rig.head.rotation.z=.55;
}

/** First person: call after poseFirstPersonView when the local player is the Siren. */
export function sirenFirstPerson(view:CharacterRig,_p:PlayerView,_time:number,_dt:number){sirenify(view);}

/** Every Siren projectile, fire patch, eruption, rain, mark and grab presentation. */
export class SirenEffects {
 private group=new T.Group();private shots=new Map<string,T.Mesh>();private patches=new Map<string,T.Mesh>();
 private shotGeometry=new T.SphereGeometry(1,10,8);private patchGeometry=new T.CircleGeometry(1,24);
 private shotMaterial=new T.MeshBasicMaterial({color:0x0a0810});private patchMaterial=new T.MeshBasicMaterial({color:0x050407,transparent:true,opacity:.8,depthWrite:false});
 constructor(scene:T.Scene){this.group.name='siren-effects';scene.add(this.group);}
 /** True for projectiles this class renders; the renderer must skip its default mesh for them. */
 owns(shot:object){return !!sirenShotOf(shot);}
 update(state:Snapshot,_time:number,_rigs:ReadonlyMap<string,CharacterRig>,_quality:number,_camera?:T.Camera){
  const live=new Set<string>();
  for(const shot of state.projectiles){const s=sirenShotOf(shot);if(!s)continue;live.add(shot.id);let m=this.shots.get(shot.id);if(!m){m=new T.Mesh(this.shotGeometry,this.shotMaterial);this.group.add(m);this.shots.set(shot.id,m);}m.position.set(shot.pos.x,shot.pos.y,shot.pos.z);m.scale.setScalar(Math.max(.12,shot.radius));}
  for(const [id,m] of this.shots)if(!live.has(id)){this.group.remove(m);this.shots.delete(id);}
  const world=sirenWorldOf(state),patches=new Set<string>();
  for(const f of world?.patches??[]){patches.add(f.id);let m=this.patches.get(f.id);if(!m){m=new T.Mesh(this.patchGeometry,this.patchMaterial);m.rotation.x=-Math.PI/2;this.group.add(m);this.patches.set(f.id,m);}m.position.set(f.pos.x,f.pos.y+.03,f.pos.z);m.scale.setScalar(f.radius);}
  for(const [id,m] of this.patches)if(!patches.has(id)){this.group.remove(m);this.patches.delete(id);}
 }
 event(_e:GameEvent,_time:number,_quality?:number){}
 clear(){for(const m of [...this.shots.values(),...this.patches.values()])this.group.remove(m);this.shots.clear();this.patches.clear();}
}

/** The dev-siren intro scene. While active it owns the intro scene's lights, background, fog and camera. */
export class SirenIntroStage {
 readonly group=new T.Group();private active=false;private saved=new Map<T.Light,number>();
 private bulb=new T.PointLight(0xd9e4ff,0,7);
 constructor(private scene:T.Scene){this.group.name='siren-intro';this.group.visible=false;this.bulb.position.set(0,2.2,.9);this.group.add(this.bulb);scene.add(this.group);}
 setActive(on:boolean){if(on===this.active)return;this.active=on;this.group.visible=on;if(on)this.scene.traverse(o=>{if(o instanceof T.Light&&!this.isOwn(o)){this.saved.set(o,o.intensity);}});else{for(const [l,i] of this.saved)l.intensity=i;this.saved.clear();}}
 private isOwn(o:T.Object3D){for(let x:T.Object3D|null=o;x;x=x.parent)if(x===this.group)return true;return false;}
 update(age:number,rig:CharacterRig,camera:T.PerspectiveCamera,_quality:number){
  if(!this.active)this.setActive(true);for(const l of this.saved.keys())l.intensity=0;
  sirenify(rig);this.bulb.intensity=age<.6?0:age<5.6?(Math.sin(age*37)>.2?6:1.2):0;
  camera.position.set(0,.55,1.55);camera.lookAt(0,.45,0);
 }
}

/** Ward Zero stage dressing and its music-synced animation (map 'siren'). */
export class SirenStageVisuals {
 readonly group=new T.Group();
 constructor(scene:T.Scene){this.group.name='siren-stage';this.group.visible=false;scene.add(this.group);}
 setActive(on:boolean){this.group.visible=on;}
 update(_state:Snapshot,_time:number,_quality:number,_camera?:T.Camera){}
}

/** Death effects: register in death-effects.ts KILL_EFFECTS. */
export const SIREN_KILL_EFFECTS:Record<typeof SIREN.deaths[keyof typeof SIREN.deaths],KillEffectFactory>={
 [SIREN.deaths.crush]:ragdollVariant('ragdoll',{gib:true}),
 [SIREN.deaths.dissipate]:ragdoll,
};
