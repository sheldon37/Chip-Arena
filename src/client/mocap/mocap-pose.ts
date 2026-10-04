import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import {braceWeapon,hand} from '../rig-ik.js';
import {CH} from './mocap-clip.js';
import {clearBlade} from './blade-clearance.js';
/** Poses the voxel rig from one sampled mocap row (mocap-clip.ts) at `weight` over whatever the layers below posed.
 * Torso, clavicles and legs take the retargeted joint rotations (the ground pass then re-plants the legs on the floor);
 * the arms go through the rig's own IK: the main hand to the retargeted grip with the blade along the captured blade
 * and edge directions, the support hand on the weapon's support grip while the capture holds the sword two-handed,
 * otherwise on its own captured target. Root travel is stripped (the server owns movement); `travel` (0..1) would admit
 * the clip's horizontal hips offset, and no current state uses it.
 * `legs` (0..1, of weight) poses the hips, knees and ankles; `root` (0..1) the heading, hips height and pelvis (locomotion
 * passes 0: the gait owns them); `arms:false` leaves both arms and the weapon to the layers below. */
/** `clear`: blade clearance (blade-clearance.ts): 'full' turns the blade and moves the grip; 'turn' (fast cuts, where a grip
 * shift would whip the elbows) only turns the blade. */
export interface MocapPoseOptions {weight:number;travel?:number;legs?:number;root?:number;arms?:boolean;clear?:'full'|'turn'}
const _q=new T.Quaternion(),_v=new T.Vector3(),_lp=new T.Vector3(),_cq=new T.Quaternion(),_a=new T.Vector3(),_b=new T.Vector3(),_c=new T.Vector3(),_m=new T.Matrix4(),_e=new T.Euler(),_p=new T.Vector3();
const X=new T.Vector3(1,0,0),Y=new T.Vector3(0,1,0),_yq=new T.Quaternion();
function rot(f:Float32Array,i:number,out:T.Quaternion){_v.set(f[i],f[i+1],f[i+2]);const a=_v.length();return a<1e-9?out.identity():out.setFromAxisAngle(_v.divideScalar(a),a);}
function set(node:T.Object3D,q:T.Quaternion,w:number){if(w>=1)node.quaternion.copy(q);else node.quaternion.slerp(q,w);}
/** Chest-space blade (into `_a`) and edge (into `_b`, perpendicular) from a row. */
function bladeAxes(f:Float32Array,z:number,x:number){
 _a.set(f[z],f[z+1],f[z+2]).normalize();_b.set(f[x],f[x+1],f[x+2]);_b.addScaledVector(_a,-_b.dot(_a));if(_b.lengthSq()<1e-8)_b.set(1,0,0).addScaledVector(_a,-_a.x);_b.normalize();
}
/** Chest-space basis (blade `_a` along +z, edge `_b` along +x) as the wrist's chest-space Euler. */
function basisEuler(out:T.Euler){_c.crossVectors(_a,_b);return out.setFromRotationMatrix(_m.makeBasis(_b,_c,_a));}
/** The weapon's extent along its blade from the grip (pommel, blade end), in the weapon's current length scale. */
const extents=new WeakMap<T.Object3D,number>();
function bladeEnd(weapon:T.Object3D){let z=extents.get(weapon);if(z===undefined){z=(weapon.getObjectByName('blade-tip')?.position.z??1.4)-.08;extents.set(weapon,z);}return z*weapon.scale.z;}
function basisEulerYZ(f:Float32Array,z:number,y:number,out:T.Euler){
 _a.set(f[z],f[z+1],f[z+2]).normalize();_c.set(f[y],f[y+1],f[y+2]);_c.addScaledVector(_a,-_c.dot(_a));if(_c.lengthSq()<1e-8)_c.set(0,1,0);_c.normalize();
 _b.crossVectors(_c,_a);return out.setFromRotationMatrix(_m.makeBasis(_b,_c,_a));
}
/** rig-ik's default elbow poles (chest space ≈ clavicle space). */
const POLE_R=new T.Vector3(-1,-.7,-.2).normalize(),POLE_L=new T.Vector3(1,-.8,-.25).normalize(),_d=new T.Vector3();
/** The clip's elbow pole for a hand target, into the clavicle's frame (rig-ik solves arms there). The converter already
 * solved it continuous and perpendicular to the rig's reach; interpolation and blending can tilt it, so it is re-taken
 * perpendicular to this frame's reach (the default pole if nothing is left). Stateless: the same row gives the same arm. */
function pole(r:CharacterRig,left:boolean,target:T.Vector3,f:Float32Array,i:number,out:T.Vector3){
 _d.set(target.x-(left?.345:-.345),target.y-.18,target.z).normalize();out.set(f[i],f[i+1],f[i+2]);out.addScaledVector(_d,-out.dot(_d));
 if(out.lengthSq()<.01)out.copy(left?POLE_L:POLE_R);
 return out.normalize().applyQuaternion(_cq.copy((left?r.leftShoulder:r.rightShoulder).quaternion).invert());
}
export function applyMocapPose(r:CharacterRig,f:Float32Array,o:MocapPoseOptions){
 const w=o.weight;if(w<=.001)return;const root=(o.root??1)*w,legs=(o.legs??1)*w;
 // Hips height rides the motion root (relative to the standing gait height); travel only as admitted.
 // The heading turns the pelvis (with the legs under it), so layered clips blend it like any joint rotation.
 if(root>.001){r.body.position.y+=(f[CH.by]-.025-r.body.position.y)*root;set(r.pelvis,_yq.setFromAxisAngle(Y,f[CH.yaw]).multiply(rot(f,CH['pelvis.x'],_q)),root);}
 if(o.travel){r.body.position.x+=f[CH.bx]*o.travel*w;r.body.position.z+=f[CH.bz]*o.travel*w;}
 set(r.spine,rot(f,CH['spine.x'],_q),w);set(r.chest,rot(f,CH['chest.x'],_q),w);
 set(r.neck,rot(f,CH['neck.x'],_q),w);set(r.head,rot(f,CH['head.x'],_q),w);
 set(r.leftShoulder,rot(f,CH['lclav.x'],_q),w);set(r.rightShoulder,rot(f,CH['rclav.x'],_q),w);
 if(legs>.001){
  set(r.leftLeg,rot(f,CH['lhip.x'],_q),legs);set(r.rightLeg,rot(f,CH['rhip.x'],_q),legs);
  set(r.leftKnee,_q.setFromAxisAngle(X,Math.max(0,f[CH.lknee])),legs);set(r.rightKnee,_q.setFromAxisAngle(X,Math.max(0,f[CH.rknee])),legs);
  set(r.leftFoot,rot(f,CH['lankle.x'],_q),legs);set(r.rightFoot,rot(f,CH['rankle.x'],_q),legs);
 }
 if(o.arms===false)return;
 _p.set(f[CH['grip.x']],f[CH['grip.y']],f[CH['grip.z']]);bladeAxes(f,CH['blade.x'],CH['edge.x']);
 // The voxel shoulders, chest and head are bigger than the captured human's: keep the blade (and hilt) clear of them.
 clearBlade(r,_p,_a,_b,-.27*r.weapon.scale.z,bladeEnd(r.weapon),o.clear!=='turn');basisEuler(_e);
 braceWeapon(r,_p,_e,w,pole(r,false,_p,f,CH['rpole.x'],_v));
 // The support hand leaves the grip only where the capture lets go of it.
 const free=(1-Math.min(1,Math.max(0,f[CH.twohand])))*w;
 if(free>.001){_p.set(f[CH['lhand.x']],f[CH['lhand.y']],f[CH['lhand.z']]);basisEulerYZ(f,CH['lfwd.x'],CH['lup.x'],_e);hand(r,true,_p,_e,free,pole(r,true,_p,f,CH['lpole.x'],_lp));}
}
/** Linear blend of two sampled rows (neighbouring poses: rotation vectors and targets blend componentwise). */
export function blendRows(a:Float32Array,b:Float32Array,k:number,out:Float32Array){for(let i=0;i<out.length;i++)out[i]=a[i]+(b[i]-a[i])*k;return out;}
