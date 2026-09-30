import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {createCharacterRig,setRigDetail} from '../src/client/character-rig.js';
import {PLAIN_LAYOUT,VoxelPart,copyVoxelPart,type VoxelLayout} from '../src/client/voxel-kit.js';

await initPhysics();
const voxels=(o:T.Object3D)=>{const list:VoxelPart[]=[];o.traverse(x=>{if(x instanceof VoxelPart)list.push(x);});return list;};
/** What the WebGL renderer does right before drawing a mesh. */
const draw=(m:T.Mesh)=>m.onBeforeRender(null as never,null as never,null as never,m.geometry,m.material as T.Material,null as never);

test('see-through consumers (hologram, ghost cinematic, phantom afterimages) draw each voxel part as its plain box',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true),list=voxels(r.group),sculpt=list.map(m=>m.instanceMatrix);
 assert.ok(list.length>=30);
 // The death-announcement hologram: one transparent DoubleSide material without depth writes, edges from the cube.
 const holo=new T.MeshBasicMaterial({color:0x62dcff,transparent:true,opacity:.6,depthWrite:false,side:T.DoubleSide});
 // The switch happens on assignment, before any frame (the renderer uploads instance buffers before onBeforeRender).
 for(const m of list){m.material=holo;assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix,m.name+' plain under the hologram');assert.equal(m.count,1);}
 // Back to an opaque material: the shared sculpt returns.
 const solid=new T.MeshLambertMaterial();list.forEach((m,i)=>{m.material=solid;draw(m);assert.equal(m.instanceMatrix,sculpt[i],m.name+' sculpt again');});
 // A transparent flag flipped later is caught by the per-draw safety net.
 const late=new T.MeshLambertMaterial();for(const m of list){m.material=late;}late.transparent=true;for(const m of list){draw(m);assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix);}
 for(const m of list)m.material=solid;
 // Low detail still wins over an opaque material.
 setRigDetail(r,true);for(const m of list){draw(m);assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix);}
 setRigDetail(r,false);list.forEach((m,i)=>{draw(m);assert.equal(m.instanceMatrix,sculpt[i]);});
});

test('clones (weapon-intro phantoms) stay voxel parts, share the layouts and carry no serialized instance arrays',()=>{
 const r=createCharacterRig('#ff6b35','#1b1d22',true),ghost=r.group.clone(true),source=voxels(r.group),copies=voxels(ghost);
 assert.equal(copies.length,source.length);
 for(const m of copies){assert.ok(JSON.stringify(m.userData).length<200,m.name+' userData stays small');m.material=new T.MeshBasicMaterial({transparent:true,opacity:.16,depthWrite:false});draw(m);assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix,m.name+' phantom is plain');}
 copies.forEach((m,i)=>{m.material=new T.MeshBasicMaterial();draw(m);assert.equal(m.instanceMatrix,source[i].instanceMatrix,m.name+' shares the layout');});
 // Detail switches work on clones too (no serialized attribute is ever installed as instanceMatrix).
 setRigDetail({group:ghost} as never,true);for(const m of copies)assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix);
 setRigDetail({group:ghost} as never,false);copies.forEach((m,i)=>assert.equal(m.instanceMatrix,source[i].instanceMatrix));
 // Cloning a low-detail rig never writes into the shared layouts.
 const counts=source.map(m=>m.instanceMatrix.array),lengths=counts.map(a=>a.length);setRigDetail(r,true);const again=r.group.clone(true);
 for(const m of voxels(again))assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix);setRigDetail(r,false);
 source.forEach((m,i)=>{assert.equal(m.instanceMatrix.array,counts[i],m.name+' layout array untouched');assert.equal(m.instanceMatrix.array.length,lengths[i]);});
 assert.equal(PLAIN_LAYOUT.matrix.array.length,16);
});

test('copyVoxelPart keeps the sculpt for ragdoll pieces and falls back to a plain mesh',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true),[part]=voxels(r.group),material=new T.MeshLambertMaterial();
 const copy=copyVoxelPart(part,material);assert.ok(copy instanceof VoxelPart);assert.equal(copy.geometry,part.geometry);assert.equal(copy.material,material);
 assert.equal((copy as VoxelPart).instanceMatrix,(part.userData.voxel as VoxelLayout).matrix);
 const plain=new T.Mesh(part.geometry,material),other=copyVoxelPart(plain);assert.ok(!(other instanceof T.InstancedMesh));assert.equal(other.material,material);
});
