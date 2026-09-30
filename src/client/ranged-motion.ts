import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,type Weapon} from '../shared/content.js';
import {clamp} from '../shared/config.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import {braceWeapon} from './rig-ik.js';
import {blendRotation} from './pose-math.js';
type Pose=ReturnType<typeof attackPose>;
/** Swing during travel; settle into the usual aiming stance when stopping or firing.
 * Called for EVERY class at the same point of sampleCharacter, because melee and magic also decay aimWeight. */
export function updateAimWeight(r:CharacterRig,p:PlayerView,pose:Pose,w:Weapon,dt:number){
 const aimTarget=w.class==='ranged'?(pose.shotAge<Math.max(.38,w.interval+.15)||p.reloadTime>0||r.motion<.18?1:0):0;r.aimWeight+=(aimTarget-r.aimWeight)*(1-Math.exp(-dt*24));
}
/** Ranged aim, recoil, pistol support arm, reload and long-gun brace layer (moved verbatim from sampleCharacter). */
export function rangedLayer(r:CharacterRig,p:PlayerView,pose:Pose,time:number,dt:number,pitch:number){
 const w=WEAPONS[p.spec];
 const a=r.aimWeight,kick=pose.recoil*(p.spec==='rifle'?1.7:p.spec==='machinegun'?.65:1);
 r.spine.rotation.x=T.MathUtils.lerp(r.spine.rotation.x,-r.body.rotation.x,a);r.chest.rotation.z=T.MathUtils.lerp(r.chest.rotation.z,-r.body.rotation.z-r.pelvis.rotation.z,a);
 blendRotation(r.rightArm,-1.0-pitch*.6-kick*.28,0,-.04,a);blendRotation(r.rightElbow,-.5+kick*.18,0,0,a);blendRotation(r.rightHand,-pitch*.4-kick*.12,0,0,a);
 r.weapon.rotation.x=T.MathUtils.lerp(.15,1.5,a);r.chest.rotation.x=T.MathUtils.lerp(r.chest.rotation.x,-kick*.12,a);r.spine.rotation.y=T.MathUtils.lerp(r.spine.rotation.y,-.15,a);r.head.rotation.z=T.MathUtils.lerp(r.head.rotation.z,-.08,a);
 if(p.spec==='pistol'){blendRotation(r.leftArm,-.6-pitch*.5,-.4,-.4,a*.75);blendRotation(r.leftElbow,-1.25,0,0,a*.75);}
 if(p.reloadTime>0){const u=clamp(1-p.reloadTime/w.reload,0,1),reach=Math.sin(Math.PI*clamp(u/.7,0,1));r.rightArm.rotation.set(-.65, .25, -.3);r.rightElbow.rotation.x=-.8;r.rightHand.rotation.z=-.28;r.weapon.rotation.x=1.45;r.leftArm.rotation.set(-.4-reach*.85,-.45,-.5);r.leftElbow.rotation.x=-.7-reach*.65;r.leftHand.rotation.z=reach*.8;r.chest.rotation.y=.25*pose.reload;r.head.rotation.x=.15*pose.reload;const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19-Math.sin(Math.PI*clamp((u-.16)/.52,0,1))*.3;}
 else {const mag=r.weapon.getObjectByName('magazine');if(mag)mag.position.y=-.19;if(p.spec!=='pistol')braceWeapon(r,new T.Vector3(-.02,.12,.16-kick*.045),new T.Euler(-pitch-kick*.13,.12,0),a);}
}
/** Non-melee guard override, applied after the class layers while guarding or blocking. */
export function nonMeleeGuard(r:CharacterRig,p:PlayerView){
 if(WEAPONS[p.spec].class!=='melee'&&(p.guarding||p.blockTime>0)){r.spine.rotation.y=0;r.chest.rotation.set(-.06,0,0);r.leftArm.rotation.set(-1.4,-.25,.35);r.rightArm.rotation.set(-1.3,.25,-.35);r.leftElbow.rotation.x=-.8;r.rightElbow.rotation.x=-.8;r.leftHand.rotation.set(0,0,.25);r.rightHand.rotation.set(0,0,-.25);r.weapon.rotation.x=1.9;}
}
/** Renderer-side timed-block arm override for non-melee classes (moved verbatim from the renderer). */
export function rangedBlockArms(r:{block:T.Object3D;leftArm:T.Object3D;rightArm:T.Object3D},w:{class:string}){if(r.block.visible&&w.class!=='melee'){r.leftArm.rotation.x=-1.7;r.rightArm.rotation.x=-1.5;}}
