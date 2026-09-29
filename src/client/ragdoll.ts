import {RagdollVfx,ragdollForce} from './ragdoll-vfx.js';
import type {RagdollStyle} from '../shared/ragdoll-styles.js';
import * as T from 'three';
import RAPIER from '@dimforge/rapier3d-compat';
import type {KillEffectFactory} from './death-effects.js';
import {ARENA} from '../shared/arena.js';
/** Cosmetic-only articulated bodies in a private, disposable world. Never adds server bodies or damage. */
export const ragdollVariant=(style:RagdollStyle='ragdoll'):KillEffectFactory=>(scene,rig,p,death,quality,budget,state)=>{
 const world=new RAPIER.World({x:0,y:-18,z:0});world.timestep=1/60;const group=new T.Group();group.name='ragdoll';scene.add(group);rig.updateMatrixWorld(true);const ownMaterials:T.Material[]=[];
 const names=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'];
 const roots=names.map(name=>rig.getObjectByName(name)).filter((o):o is T.Object3D=>!!o),rootSet=new Set(roots),parts=new Map<T.Object3D,{body:RAPIER.RigidBody;mesh:T.Group}>();
 // Match the current silhouette (including cosmetics), while severing only the animation hierarchy.
 for(const root of roots.slice(0,budget)){
  const mesh=new T.Group(),pos=root.getWorldPosition(new T.Vector3()),rotation=root.getWorldQuaternion(new T.Quaternion()),inverse=new T.Matrix4().compose(pos,rotation,new T.Vector3(1,1,1)).invert();mesh.name=root.name;mesh.position.copy(pos);mesh.quaternion.copy(rotation);group.add(mesh);
  const collect=(node:T.Object3D)=>{if(node!==root&&rootSet.has(node))return;if(node instanceof T.Mesh&&node.visible){const copy=new T.Mesh(node.geometry,node.material);if(style!=='ragdoll'){copy.material=(node.material as T.Material).clone();ownMaterials.push(copy.material);if(style==='ragdoll-soul'){copy.material.transparent=true;copy.material.depthWrite=false;}}copy.name=node.name;const transform=inverse.clone().multiply(node.matrixWorld);transform.decompose(copy.position,copy.quaternion,copy.scale);mesh.add(copy);}if(node.visible)for(const child of node.children)collect(child);};collect(root);
  const bounds=new T.Box3();for(const child of mesh.children){const m=child as T.Mesh;m.geometry.computeBoundingBox();m.updateMatrix();bounds.union(m.geometry.boundingBox!.clone().applyMatrix4(m.matrix));}
  if(bounds.isEmpty())bounds.set(new T.Vector3(-.07,-.07,-.07),new T.Vector3(.07,.07,.07));const size=bounds.getSize(new T.Vector3()).multiplyScalar(.5).clamp(new T.Vector3(.05,.05,.05),new T.Vector3(.4,.45,.4)),center=bounds.getCenter(new T.Vector3());
  const body=world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().setTranslation(pos.x,pos.y,pos.z).setRotation(rotation).setLinearDamping(.35).setAngularDamping(.6).setCcdEnabled(true));world.createCollider(RAPIER.ColliderDesc.cuboid(size.x,size.y,size.z).setTranslation(center.x,center.y,center.z).setDensity(2).setFriction(.7).setRestitution(style==='ragdoll-pinball'?.82:.15).setCollisionGroups(0x00020001),body);parts.set(root,{body,mesh});
 }
 for(const [root,part]of parts){let parent=root.parent;while(parent&&!parts.has(parent))parent=parent.parent;if(!parent)continue;const other=parts.get(parent)!,point=root.getWorldPosition(new T.Vector3()),anchor=point.clone().sub(other.mesh.position).applyQuaternion(other.mesh.quaternion.clone().invert());const joint=world.createImpulseJoint(RAPIER.JointData.spherical(anchor,{x:0,y:0,z:0}),other.body,part.body,true);joint.setContactsEnabled(false);}
 const floor=RAPIER.ColliderDesc.cuboid(ARENA.width/2,.5,ARENA.depth/2).setTranslation(0,-.5,0).setCollisionGroups(0x00010002).setFriction(.8);world.createCollider(floor);
 // Nearby snapshot geometry catches limbs on the same cover the players see. Budget stays bounded.
 for(const prop of (state?.props||[]).filter(x=>x.size.y<20&&x.size.x<60&&Math.hypot(x.pos.x-death.pos.x,x.pos.z-death.pos.z)<9).slice(0,32)){const desc=RAPIER.ColliderDesc.cuboid(prop.size.x/2,prop.size.y/2,prop.size.z/2).setTranslation(prop.pos.x,prop.pos.y,prop.pos.z).setCollisionGroups(0x00010002);if(prop.rot)desc.setRotation(prop.rot);world.createCollider(desc);}
 const dir=new T.Vector3(death.direction.x,death.direction.y,death.direction.z);if(dir.lengthSq()<.001)dir.set(Math.sin(p.yaw),.1,Math.cos(p.yaw));dir.normalize();const point=new T.Vector3((death.hitPoint||death.pos).x,(death.hitPoint||death.pos).y,(death.hitPoint||death.pos).z);
 let seed=death.seed;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};const strength=death.melee?8+Math.min(7,(death.force||0)/100):2.2+Math.min(2,(death.force||0)/160),inherited=new T.Vector3(death.velocity.x,death.velocity.y,death.velocity.z).clampLength(0,5).multiplyScalar(.3);
 for(const {body,mesh}of parts.values()){const distance=mesh.position.distanceTo(point),local=1+Math.exp(-distance*4)*.9,velocity=dir.clone().multiplyScalar(strength*local).add(inherited);velocity.y+=death.melee?3.8:1;body.setLinvel(velocity,true);const lever=point.clone().sub(mesh.position),spin=lever.cross(dir).multiplyScalar(death.melee?15:5);spin.add(new T.Vector3(random()-.5,random()-.5,random()-.5).multiplyScalar(death.melee?5:1.5)).clampLength(0,18);body.setAngvel(spin,true);}
 const origin=new T.Vector3(death.pos.x,death.pos.y,death.pos.z),vfx=style==='ragdoll'?undefined:new RagdollVfx(style,death.seed,Math.max(0,budget-parts.size),quality);if(vfx)scene.add(vfx.root);const bodies=[...parts.values()];let elapsed=0,disposed=false;
 return {count:parts.size+(vfx?.count||0),update(age){if(disposed)return;const target=Math.min(5,Math.max(0,age));while(elapsed+1/60<=target+1e-6){ragdollForce(style,bodies,elapsed,origin);world.step();elapsed+=1/60;}const fade=Math.min(1,Math.max(0,(5-target)/.65));for(const {body,mesh}of parts.values()){mesh.position.copy(body.translation());mesh.quaternion.copy(body.rotation());mesh.scale.setScalar(fade);}const center=parts.get(roots[0])?.mesh.position||origin;vfx?.update(target,center,bodies);},dispose(){if(disposed)return;disposed=true;group.removeFromParent();world.free();parts.clear();vfx?.dispose();ownMaterials.forEach(m=>m.dispose());}};
};

export const ragdoll=ragdollVariant();
