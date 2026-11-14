// MoCap Online "ZOMBIE PRO" pack source profile for the mocap converter (development tool; never imported by the game or
// the build). The pack is a licensed Unity asset: its FBX and .meta files are never committed; the converter reads an
// extracted copy. This module makes a pack file look like a Mixamo file to mixamo-retarget.mjs:
// - the MotusMan_v55 rig already uses Mixamo's bone names (Hips, Spine1, LeftUpLeg, LeftHandIndex1, ...) without the
//   "mixamorig" prefix: every node (and its animation tracks) gets the prefix the retarget reads;
// - the exports are Y-up in centimetres (no stand-up turn; the retarget only uses ratios of its own lengths);
// - the rest pose is built from the Unity avatar skeleton in the file's .meta (the FBX defaults are the clip's first
//   frame), Unity's left-handed metres converted to three's right-handed centimetres. The FBX nests a same-named,
//   identity node under each bone: only the outer one takes the skeleton's transform.
import fs from 'node:fs';
import * as T from 'three';
import {loadFbx} from './mixamo-retarget.mjs';

/** The MotusMan bones the retarget reads (with their fingers' first joints). */
const NAMES=new Set(['Hips','Spine','Spine1','Spine2','Neck','Head',...['Left','Right'].flatMap(s=>['Shoulder','Arm','ForeArm','Hand','UpLeg','Leg','Foot','ToeBase',
 ...['Thumb','Index','Middle','Ring','Pinky'].flatMap(f=>[1,2,3].map(i=>'Hand'+f+i))].map(b=>s+b))]);
const renamed=n=>NAMES.has(n)?'mixamorig'+n:n;

/** Pack file → the retarget's view of it: bones and tracks renamed (Y-up; checked). */
export function loadZombieFbx(file){
 const obj=loadFbx(file);
 obj.traverse(n=>{n.name=renamed(n.name);});
 for(const clip of obj.animations)for(const t of clip.tracks){const i=t.name.indexOf('.');t.name=renamed(t.name.slice(0,i))+t.name.slice(i);}
 obj.updateMatrixWorld(true);
 const hips=obj.getObjectByName('mixamorigHips'),head=obj.getObjectByName('mixamorigHead');
 if(!hips||!head)throw new Error(`${file}: not a MotusMan rig`);
 const up=head.getWorldPosition(new T.Vector3()).sub(hips.getWorldPosition(new T.Vector3()));
 if(up.y<.7*up.length())throw new Error(`${file}: expected a Y-up export (head over hips)`);
 return {obj,clip:obj.animations[0]};
}

/** The Unity avatar skeleton of a pack file's .meta (identical in every file of the pack), metres. */
export function zombieSkeleton(file){
 const meta=fs.readFileSync(file+'.meta','utf8'),sk=meta.slice(meta.indexOf('    skeleton:'),meta.indexOf('    armTwist'));
 const re=/- name: (.+)\n\s+parentName:.*\n\s+position: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}\n\s+rotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}/g;
 const S={};let m;while((m=re.exec(sk))){const [px,py,pz,qx,qy,qz,qw]=m.slice(2).map(Number);S[m[1]]={p:[px,py,pz],q:[qx,qy,qz,qw]};}
 if(!S.Hips||!S.LeftUpLeg)throw new Error(`${file}.meta: no avatar skeleton`);
 return S;
}

/** Rest pose for calibrate(): the file's own node tree under Root, each outer bone's local transform from the avatar
 * skeleton (Unity left-handed metres → three right-handed centimetres: position (-x, y, z) x 100, rotation (x, -y, -z, w)). */
export function zombieRestPose(file){
 const S=zombieSkeleton(file),{obj}=loadZombieFbx(file),root=new T.Group();
 const raw=n=>n.startsWith('mixamorig')?n.slice(9):n;
 const copy=(src,parent,parentName)=>{const g=new T.Object3D();g.name=src.name;const name=raw(src.name),e=name!==parentName?S[name]:null;
  if(e){g.position.set(-e.p[0]*100,e.p[1]*100,e.p[2]*100);g.quaternion.set(e.q[0],-e.q[1],-e.q[2],e.q[3]);}
  parent.add(g);for(const c of src.children)copy(c,g,name);};
 const top=obj.getObjectByName('Root');if(!top)throw new Error(`${file}: no Root node`);
 copy(top,root,null);root.updateMatrixWorld(true);
 return root;
}

/** A clip's frame range and loop flag from its .meta (30 fps frames). */
export function zombieRange(file){
 const meta=fs.readFileSync(file+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
