import * as T from 'three';
import {baseLayout,currentLayout,retagVoxelPart,type VoxelLayout} from '../../voxel-kit.js';
import {LIMB} from '../../../shared/pve/contract.js';
import {corruptedPart,type PartRequest} from './layouts.js';
import {categoryOf} from './model.js';
import type {SirenMinion} from './model.js';

/**
 * Runtime limb loss (PvE gibs, design §8.3). The authority sends a LIMB mask; the rig hides the severed joint groups and
 * retags the part left at the cut as a torn stump (the same `stump` condition the seeded missing hands use). Everything is
 * a pure function of the mask: applying an older mask (a kill-cam rewind) restores the limbs exactly.
 *
 * | Loss  | Hidden                                   | Stump part             |
 * |-------|------------------------------------------|------------------------|
 * | head  | the head joint (head, crown, jaw)        | neck-joint (torn top)  |
 * | arm   | the elbow joint and everything below it  | the upper arm          |
 * | fore  | the forearm and the wrist (the hand)     | the elbow joint        |
 * | leg   | the knee joint and everything below it   | the thigh              |
 * | shin  | the shin, the calf and the ankle (foot)  | the knee joint         |
 */
interface Cut {groups:string[];meshes:string[];stump:string;variant:number}
const side=(s:'left'|'right',arm:boolean,whole:boolean):Cut=>arm
 ?whole?{groups:[s+'-elbow'],meshes:[],stump:s+'-upper-arm',variant:0}:{groups:[s+'-wrist'],meshes:[s+'-forearm'],stump:s+'-elbow-joint',variant:0}
 :whole?{groups:[s+'-knee'],meshes:[],stump:s+'-thigh',variant:0}:{groups:[s+'-ankle'],meshes:[s+'-shin',s+'-calf'],stump:s+'-knee-joint',variant:0};
/** The cuts in LIMB bit order. A whole limb wins over its lower half (its stump is higher up). */
export const LIMB_CUTS:readonly {bit:number;cut:Cut;covers?:number}[]=[
 {bit:LIMB.head,cut:{groups:['head'],meshes:[],stump:'neck-joint',variant:1}},
 {bit:LIMB.lArm,cut:side('left',true,true),covers:LIMB.lFore},{bit:LIMB.rArm,cut:side('right',true,true),covers:LIMB.rFore},
 {bit:LIMB.lFore,cut:side('left',true,false)},{bit:LIMB.rFore,cut:side('right',true,false)},
 {bit:LIMB.lLeg,cut:side('left',false,true),covers:LIMB.lShin},{bit:LIMB.rLeg,cut:side('right',false,true),covers:LIMB.rShin},
 {bit:LIMB.lShin,cut:side('left',false,false)},{bit:LIMB.rShin,cut:side('right',false,false)},
];
/** Every stump layout runtime loss can ask for: [part name, variant] (the prewarm builds them for each rot level). */
export const LOSS_STUMPS:readonly [string,number][]=LIMB_CUTS.map(c=>[c.cut.stump,c.cut.variant]);

interface LimbState {mask:number;nodes:Map<string,T.Object3D>;restore:Map<T.Mesh,{visible:boolean;layout?:VoxelLayout}>;stumps:T.Object3D[]}
const states=new WeakMap<SirenMinion,LimbState>();
function stateOf(m:SirenMinion){
 let st=states.get(m);if(st)return st;const nodes=new Map<string,T.Object3D>();m.rig.group.traverse(o=>{if(o.name&&!nodes.has(o.name))nodes.set(o.name,o);});
 st={mask:0,nodes,restore:new Map(),stumps:[]};states.set(m,st);return st;
}
/** The cuts a mask shows (a whole limb hides its lower cut). */
export function activeCuts(mask:number){return LIMB_CUTS.filter(c=>(mask&c.bit)&&!LIMB_CUTS.some(o=>o.covers===c.bit&&(mask&o.bit)));}

/** Shows the rig with exactly the limbs of `mask` severed. Cheap when the mask is unchanged. */
export function applyLostLimbs(m:SirenMinion,mask:number){
 mask&=0x1FF;const st=states.get(m);if(!st&&!mask)return;const s=st??stateOf(m);if(s.mask===mask)return;
 // Restore everything the last mask changed, then apply the new one.
 for(const [mesh,r] of s.restore){mesh.visible=r.visible;if(r.layout&&mesh instanceof T.InstancedMesh&&currentLayout(mesh)!==r.layout)retagVoxelPart(mesh,r.layout);}
 for(const g of [...LIMB_CUTS.flatMap(c=>c.cut.groups)])s.nodes.get(g)!.visible=true;
 s.restore.clear();s.stumps=[];s.mask=mask;
 for(const {cut} of activeCuts(mask)){
  for(const g of cut.groups)s.nodes.get(g)!.visible=false;
  for(const name of cut.meshes){const mesh=s.nodes.get(name) as T.Mesh|undefined;if(!mesh)continue;if(!s.restore.has(mesh))s.restore.set(mesh,{visible:mesh.visible});mesh.visible=false;}
  const stump=s.nodes.get(cut.stump) as T.Mesh|undefined;if(!(stump instanceof T.InstancedMesh))continue;
  if(!s.restore.has(stump))s.restore.set(stump,{visible:stump.visible,layout:currentLayout(stump)});
  stump.visible=true;const category=categoryOf(cut.stump),base=baseLayout(stump);if(!category||!base)continue;
  const req:PartRequest={name:cut.stump,category,size:stump.scale.clone(),base,condition:'stump',variant:cut.variant,rot:m.look.rot,face:'blank'};
  retagVoxelPart(stump,corruptedPart(req).layout);s.stumps.push(stump);
 }
 // A tongue on a hidden joint goes with it (they are its children); the scrap goes with the right hand.
}
/** The current stump parts (black flame drips from them; gore.ts reads their world positions). */
export function stumpsOf(m:SirenMinion):readonly T.Object3D[]{return states.get(m)?.stumps??[];}
