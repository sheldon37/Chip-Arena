// CLazy "Runner Action Animation Pack" source profile for the mocap converter (development tool; never imported by the
// game or the build). The pack is a licensed Unity asset: its FBX and .meta files are never committed; the exporters read
// an extracted copy. It uses the Unreal mannequin skeleton (pelvis, spine_01..03, thigh_l, calf_l, foot_l, ...). This
// module makes a pack file look like a Mixamo file to mixamo-retarget.mjs, as explosive-pack.mjs does for the Explosive
// pack:
// - loads the FBX 7.5 file (Y-up, centimetres, the root at the identity);
// - renames the mannequin bones (and their animation tracks) to the Mixamo names the retarget reads;
// - builds the rest pose from the Unity avatar skeleton in the file's .meta (the FBX defaults are the clip's first frame,
//   not a bind pose): a clean T-pose, Unity's left-handed frame converted to three's, its Z-up root stood up and its
//   metres scaled to the file's centimetres.
import fs from 'node:fs';
import * as T from 'three';
import {loadFbx} from './mixamo-retarget.mjs';

/** Mannequin bone → Mixamo name (mixamorig prefix added on rename). Fingers: thumb, index, middle, ring, pinky. */
const SIDES={l:'Left',r:'Right'},FINGERS={thumb:'Thumb',index:'Index',middle:'Middle',ring:'Ring',pinky:'Pinky'};
export const CLAZY_BONE_MAP=(()=>{
 const m={pelvis:'Hips',spine_01:'Spine',spine_02:'Spine1',spine_03:'Spine2',neck_01:'Neck',head:'Head'};
 for(const [s,side] of Object.entries(SIDES)){
  Object.assign(m,{[`clavicle_${s}`]:side+'Shoulder',[`upperarm_${s}`]:side+'Arm',[`lowerarm_${s}`]:side+'ForeArm',[`hand_${s}`]:side+'Hand',
   [`thigh_${s}`]:side+'UpLeg',[`calf_${s}`]:side+'Leg',[`foot_${s}`]:side+'Foot',[`ball_${s}`]:side+'ToeBase'});
  for(const [f,name] of Object.entries(FINGERS))for(let k=1;k<=3;k++)m[`${f}_0${k}_${s}`]=`${side}Hand${name}${k}`;
 }
 return m;
})();
const renamed=n=>CLAZY_BONE_MAP[n]?'mixamorig'+CLAZY_BONE_MAP[n]:n;

/** Pack file → the retarget's view of it: bones and tracks renamed; the file must stand Y-up with the pelvis above the root. */
export function loadClazyFbx(file){
 const obj=loadFbx(file);
 obj.traverse(n=>{n.name=renamed(n.name);});
 for(const clip of obj.animations)for(const t of clip.tracks){const i=t.name.indexOf('.');t.name=renamed(t.name.slice(0,i))+t.name.slice(i);}
 obj.updateMatrixWorld(true);
 const root=obj.getObjectByName('root'),hips=obj.getObjectByName('mixamorigHips');
 if(!root||!hips)throw new Error(`${file}: not a CLazy mannequin file`);
 if(hips.getWorldPosition(new T.Vector3()).y<50)throw new Error(`${file}: unexpected orientation (the pelvis is not above the root)`);
 return {obj,clip:obj.animations[0]};
}

/** The Unity avatar skeleton of a pack file's .meta (every file of the pack carries the same T-pose). */
export function clazySkeleton(file){
 const meta=fs.readFileSync(file+'.meta','utf8'),sk=meta.slice(meta.indexOf('    skeleton:'),meta.indexOf('    armTwist'));
 const re=/- name: (.+)\n\s+parentName:.*\n\s+position: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}\n\s+rotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}/g;
 const S={};let m;while((m=re.exec(sk))){const [px,py,pz,qx,qy,qz,qw]=m.slice(2).map(Number);S[m[1]]={p:[px,py,pz],q:[qx,qy,qz,qw]};}
 if(!S.root||!S.pelvis)throw new Error(`${file}.meta: no avatar skeleton`);
 return S;
}

/** Rest pose (T-pose) for calibrate(): the file's bone tree under its root, every node's local transform taken from the avatar
 * skeleton (Unity left-handed → three right-handed: position (-x, y, z), rotation (x, -y, -z, w); metres → centimetres),
 * the root's own Unity rotation included (it stands the Z-up skeleton up), renamed. */
export function clazyRestPose(file){
 const S=clazySkeleton(file),{obj}=loadClazyFbx(file),scene=new T.Group();
 const raw=n=>Object.keys(CLAZY_BONE_MAP).find(k=>'mixamorig'+CLAZY_BONE_MAP[k]===n)??n;
 const copy=(src,parent)=>{const g=new T.Object3D();g.name=src.name;const e=S[raw(src.name)];
  if(e){g.position.set(-e.p[0]*100,e.p[1]*100,e.p[2]*100);g.quaternion.set(e.q[0],-e.q[1],-e.q[2],e.q[3]);}
  else{g.position.copy(src.position);g.quaternion.copy(src.quaternion);}
  parent.add(g);for(const c of src.children)if(c.isBone)copy(c,g);};
 copy(obj.getObjectByName('root'),scene);scene.updateMatrixWorld(true);
 const hips=scene.getObjectByName('mixamorigHips').getWorldPosition(new T.Vector3()),hand=scene.getObjectByName('mixamorigLeftHand').getWorldPosition(new T.Vector3());
 if(hips.y<50||Math.abs(hand.y-scene.getObjectByName('mixamorigLeftArm').getWorldPosition(new T.Vector3()).y)>5||hand.x<40)throw new Error(`${file}.meta: the avatar skeleton is not a standing T-pose`);
 return scene;
}

/** A clip's loop range from its .meta (pack frames at 30 Hz). */
export function clazyRange(file){
 const meta=fs.readFileSync(file+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
