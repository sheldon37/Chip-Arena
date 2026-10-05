// Explosive "RPG Character Mecanim Animation Pack" source profile for the mocap converter (development tool; never
// imported by the game or the build). The pack is a licensed Unity asset: its FBX files are never committed; the
// converters read an extracted copy. This module makes a pack file look like a Mixamo file to mixamo-retarget.mjs:
// - loads FBX 7.3 without the Mixamo footer trim (loadFbx picks that by version);
// - stands up the Z-up exports (GlobalSettings UpAxis 2: three's FBXLoader keeps their Motion node turned +90° about X);
// - renames the 3ds Max biped bones (and their animation tracks) to the Mixamo names the retarget reads;
// - builds the rest pose from the Unity avatar skeleton in the file's .meta (the FBX defaults are the clip's first
//   frame, a combat crouch, not a bind pose): a clean T-pose, Unity's left-handed frame converted to three's.
import fs from 'node:fs';
import * as T from 'three';
import {loadFbx} from './mixamo-retarget.mjs';

/** Pack bone → Mixamo name (mixamorig prefix added on rename). Fingers: 0 thumb, 1 index, 2 middle, 3 ring, 4 little. */
const SIDES={L:'Left',R:'Right'},FINGERS=['Thumb','Index','Middle','Ring','Pinky'];
export const BONE_MAP=(()=>{
 const m={B_Pelvis:'Hips',B_Spine:'Spine',B_Spine1:'Spine1',B_Spine2:'Spine2',B_Neck:'Neck',B_Head:'Head'};
 for(const [s,side] of Object.entries(SIDES)){
  Object.assign(m,{[`B_${s}_Clavicle`]:side+'Shoulder',[`B_${s}_UpperArm`]:side+'Arm',[`B_${s}_Forearm`]:side+'ForeArm',[`B_${s}_Hand`]:side+'Hand',
   [`B_${s}_Thigh`]:side+'UpLeg',[`B_${s}_Calf`]:side+'Leg',[`B_${s}_Foot`]:side+'Foot',[`B_${s}_Toe0`]:side+'ToeBase'});
  FINGERS.forEach((f,i)=>{m[`B_${s}_Finger${i}`]=`${side}Hand${f}1`;m[`B_${s}_Finger${i}1`]=`${side}Hand${f}2`;m[`B_${s}_Finger${i}2`]=`${side}Hand${f}3`;});
 }
 return m;
})();
const renamed=n=>BONE_MAP[n]?'mixamorig'+BONE_MAP[n]:n;

/** Pack file → the retarget's view of it: bones and tracks renamed, Z-up exports stood up (the Motion node is the world
 * identity at frame 0 in every Y-up file; a Z-up file's is +90° about X). */
export function loadPackFbx(file){
 const obj=loadFbx(file);
 obj.traverse(n=>{n.name=renamed(n.name);});
 for(const clip of obj.animations)for(const t of clip.tracks){const i=t.name.indexOf('.');t.name=renamed(t.name.slice(0,i))+t.name.slice(i);}
 obj.updateMatrixWorld(true);
 const motion=obj.getObjectByName('Motion'),up=new T.Vector3(0,1,0).applyQuaternion(motion.getWorldQuaternion(new T.Quaternion()));
 if(up.z>.7){obj.quaternion.premultiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),-Math.PI/2));obj.updateMatrixWorld(true);}
 else if(up.y<.7)throw new Error(`${file}: unexpected Motion orientation (up ${up.toArray().map(x=>x.toFixed(2))})`);
 return {obj,clip:obj.animations[0]};
}

/** The Unity avatar skeleton of a pack file's .meta (identical in every file of the pack). */
export function metaSkeleton(file){
 const meta=fs.readFileSync(file+'.meta','utf8'),sk=meta.slice(meta.indexOf('    skeleton:'),meta.indexOf('    armTwist'));
 const re=/- name: (.+)\n\s+parentName:.*\n\s+position: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}\n\s+rotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}/g;
 const S={};let m;while((m=re.exec(sk))){const [px,py,pz,qx,qy,qz,qw]=m.slice(2).map(Number);S[m[1]]={p:[px,py,pz],q:[qx,qy,qz,qw]};}
 if(!S.B_Pelvis||!S.Motion)throw new Error(`${file}.meta: no avatar skeleton`);
 return S;
}

/** Rest pose (T-pose) for calibrate(): the pack file's own node tree under Motion, every node's local transform taken from
 * the avatar skeleton (Unity left-handed → three right-handed: position (-x, y, z), rotation (x, -y, -z, w)), renamed. */
export function packRestPose(file){
 const S=metaSkeleton(file),{obj}=loadPackFbx(file),root=new T.Group();
 const raw=n=>Object.keys(BONE_MAP).find(k=>'mixamorig'+BONE_MAP[k]===n)??n;
 const copy=(src,parent)=>{const g=new T.Object3D();g.name=src.name;const e=S[raw(src.name)];
  if(e){g.position.set(-e.p[0],e.p[1],e.p[2]);g.quaternion.set(e.q[0],-e.q[1],-e.q[2],e.q[3]);}
  parent.add(g);for(const c of src.children)copy(c,g);};
 copy(obj.getObjectByName('Motion'),root);root.updateMatrixWorld(true);
 return root;
}

/** The authored impact markers of a pack clip: the .meta clip events named `name`, in source seconds (Unity stores them
 * normalized to the clip's frame range). */
export function packEvents(file,name='Hit'){
 let meta;try{meta=fs.readFileSync(file+'.meta','utf8');}catch{return [];}
 const first=+(meta.match(/\n\s+firstFrame: (.*)/)?.[1]??0),last=+(meta.match(/\n\s+lastFrame: (.*)/)?.[1]??0),span=(last-first)/30;
 return [...meta.matchAll(/- time: ([\d.eE+-]+)\n\s+functionName: (\S*)/g)].filter(x=>x[2]===name).map(x=>+(first/30+Number(x[1])*span).toFixed(4));
}
