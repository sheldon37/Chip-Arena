import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {createCharacterRig,recolorCharacterRig,setRigDetail,settleJointCovers} from '../src/client/character-rig.js';
import {buildWeapon} from '../src/client/weapon-visuals.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {ARMOR_SLOTS,DEFAULT_ARMOR,armorBuilder} from '../src/client/voxel-armor.js';
import {PLAIN_LAYOUT,type VoxelLayout} from '../src/client/voxel-kit.js';
import {PROBE_RATES,probeMotion,probeRig} from './helpers/motion-probe.js';

await initPhysics();
const meshes=(o:T.Object3D)=>{const list:T.Mesh[]=[];o.traverse(x=>{if(x instanceof T.Mesh)list.push(x);});return list;};
const voxels=(o:T.Object3D)=>meshes(o).filter((m):m is T.InstancedMesh=>m instanceof T.InstancedMesh&&!!m.userData.voxel);
const tris=(o:T.Object3D)=>meshes(o).reduce((n,m)=>n+12*(m instanceof T.InstancedMesh?m.count:1),0);
/** Bounds of a voxel part's instances in the part's own unit-cube space (the plain box is [-.5,.5]^3). */
const unitBounds=(m:T.InstancedMesh)=>{const box=new T.Box3(),b=new T.Box3(),mat=new T.Matrix4();for(let i=0;i<m.count;i++){m.getMatrixAt(i,mat);box.union(b.set(new T.Vector3(-.5,-.5,-.5),new T.Vector3(.5,.5,.5)).applyMatrix4(mat));}return box;};

test('voxel parts are the old boxes to every consumer: shared cube, one mesh each, same names, scales and materials',()=>{
 const a=createCharacterRig('#70e1d0','#e5e7eb',true),b=createCharacterRig('#ff6b35','#1b1d22',true),plain=createCharacterRig('#70e1d0');
 const cube=meshes(a.group)[0].geometry;
 for(const r of [a,b,plain])for(const m of meshes(r.group)){assert.equal(m.geometry,cube,m.name+' uses the shared cube');assert.equal(m.geometry.type,'BoxGeometry');}
 assert.ok(meshes(a.group).length<=48&&meshes(plain.group).length<=45,'mesh budget');
 const list=voxels(a.group);assert.ok(list.length>=30,'most body parts are voxel sculpted: '+list.length);
 for(const name of ['blank-head','ribcage','sternum','left-shoulder-cap','right-fingers','left-foot','armor-back-light'])assert.ok(a.group.getObjectByName(name) instanceof T.InstancedMesh,name+' is voxel');
 // Layouts are built once and shared by every rig, whatever the colours.
 for(const m of list){const other=b.group.getObjectByName(m.name) as T.InstancedMesh;assert.equal(other.instanceMatrix,m.instanceMatrix,m.name+' shares its layout');assert.ok(m.count>1||m.name==='armor-shin-light',m.name+' has voxel detail');}
 // Scale and position stay the part box (death voxels, ragdoll bounds and the SVG path read the plain box).
 const ribcage=a.group.getObjectByName('ribcage')!;assert.ok(ribcage.scale.equals(new T.Vector3(.53,.30,.30))&&Math.abs(ribcage.position.y-.10)<1e-9);
 // Recolouring swaps materials only.
 const before=(a.group.getObjectByName('ribcage') as T.InstancedMesh).instanceMatrix;recolorCharacterRig(a,'#e2453c','#3a7bd5');assert.equal((a.group.getObjectByName('ribcage') as T.InstancedMesh).instanceMatrix,before);
 assert.ok(tris(a.group)<4000,'triangles per rig '+tris(a.group));
});

test('voxel geometry is finite and stays on the part: inside its box apart from thin proud plates; the head box is exact',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true);
 for(const m of voxels(r.group)){const box=unitBounds(m),s=m.scale;
  for(const v of [box.min,box.max])assert.ok(Number.isFinite(v.x+v.y+v.z),m.name+' finite');
  // Proud detail is at most ~2 cm beyond the plain box on any side (plates, flares, glow insets), except the glow strips
  // and the sternum core, which carry seams across their part's frame by design.
  if(m.name.startsWith('armor-')||m.name==='sternum')continue;
  for(const [k,size] of [['x',s.x],['y',s.y],['z',s.z]] as const){assert.ok((box.max[k]-.5)*size<=.021&&(-.5-box.min[k])*size<=.021,`${m.name} ${k} stays on the part (${((box.max[k]-.5)*size).toFixed(3)}, ${((-.5-box.min[k])*size).toFixed(3)})`);}
 }
 r.group.updateMatrixWorld(true);const head=new T.Box3().setFromObject(r.group.getObjectByName('blank-head')!);
 assert.ok(Math.abs(head.max.y-head.min.y-.34)<1e-6&&Math.abs(head.getCenter(new T.Vector3()).y-.64)<1e-6,'the head keeps the authoritative headshot box');
 // Soles stay where the boxes were, so planted feet do not sink or float.
 for(const n of ['left-foot','right-heel']){const m=r.group.getObjectByName(n) as T.InstancedMesh;assert.ok(Math.abs(unitBounds(m).min.y+.5)<1e-6,n+' sole on the old bottom');}
});

test('low detail turns every voxel part back into its plain box and restores the sculpt',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true),list=voxels(r.group),layouts=list.map(m=>m.userData.voxel as VoxelLayout);
 setRigDetail(r,true);for(const m of list){assert.equal(m.instanceMatrix,PLAIN_LAYOUT.matrix,m.name+' plain');assert.equal(m.count,1);}
 assert.ok(meshes(r.group).filter(m=>m.userData.detail).every(m=>!m.visible),'detail meshes hidden');
 setRigDetail(r,false);list.forEach((m,i)=>{assert.equal(m.instanceMatrix,layouts[i].matrix,m.name+' sculpt restored');assert.equal(m.count,layouts[i].count);});
});

test('armor slots describe every sculpted part, and the default variant builds each one',()=>{
 const slotted=new Set(Object.values(ARMOR_SLOTS).flat());
 for(const key of Object.keys(DEFAULT_ARMOR))assert.ok(slotted.has(key),key+' belongs to a slot');
 for(const key of slotted)assert.ok(DEFAULT_ARMOR[key],key+' has a default builder');
 const r=createCharacterRig('#70e1d0',undefined,true);for(const m of voxels(r.group))assert.ok(armorBuilder(m.name),m.name+' comes from the armor slots');
 assert.equal(armorBuilder('left-forearm'),DEFAULT_ARMOR.forearm);assert.equal(armorBuilder('beanie'),undefined,'hats and props are never sculpted by accident');
});

test('the rig contract names survive, and covers still bisect the hinges on voxel parts at every rate',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true);
 for(const n of ['motion-root','pelvis','spine','chest','neck','head','weapon-socket','offhand-weapon-socket','head-cosmetic','chest-cosmetic','back-socket',...['left','right'].flatMap(s=>['clavicle','shoulder','elbow','wrist','hand-socket','hip','knee','ankle','toe'].map(j=>s+'-'+j))])assert.ok(r.group.getObjectByName(n),n);
 assert.equal(r.group.name,'blank-mannequin-v2');
 for(const hz of PROBE_RATES){const rig=probeRig('greatsword',true);probeMotion({spec:'greatsword',mode:'run',from:1,to:1.4,hz,rig,animate:(x,p,t,dt)=>{animateCharacter(x,p,t,dt,p.pitch);settleJointCovers(x);const cap=x.group.getObjectByName('left-kneecap') as T.InstancedMesh;assert.ok(cap.count>1&&Math.abs(cap.rotation.x+x.leftKnee.rotation.x*.5)<1e-9);}});}
});

test('voxel weapons keep one mesh per part and stay inside their boxes',()=>{
 for(const spec of ['sword','greatsword','daggers','shield','pistol','rifle','machinegun','fire','water','lightning','darkness']){
  const a=new T.Group(),b=new T.Group();buildWeapon(a,spec);buildWeapon(b,spec);const va=voxels(a),vb=voxels(b);
  assert.ok(va.length>=1,spec+' has voxel parts');va.forEach((m,i)=>assert.equal(vb[i].instanceMatrix,m.instanceMatrix,spec+' layouts shared'));
  for(const m of va){const box=unitBounds(m);for(const k of ['x','y','z'] as const)assert.ok((box.max[k]-.5)*m.scale[k]<=.021&&(-.5-box.min[k])*m.scale[k]<=.021,spec+' '+k);}
 }
});
