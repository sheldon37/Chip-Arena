import {bodyPartVelocity} from '../shared/body-ragdoll.js';
import {GibberVfx} from './gibber-vfx.js';
import {gibPartAt} from '../shared/body-hit.js';
import {RagdollVfx,ragdollForce} from './ragdoll-vfx.js';
import type {RagdollStyle} from '../shared/ragdoll-styles.js';
import * as T from 'three';
import RAPIER from '@dimforge/rapier3d-compat';
import type {KillEffectFactory} from './death-effects.js';
import {ARENA} from '../shared/arena.js';
import {copyVoxelPart} from './voxel-kit.js';
/** Cosmetic-only articulated bodies in a private, disposable world. Never adds server bodies or damage. */
export interface RagdollOptions {gib?:boolean;anchor?:()=>{position:T.Vector3;rotation:T.Quaternion};}
/** The effect ragdollVariant returns also exposes its part group (children named after the rig joints they copy), so a
 * knockdown ragdoll can hand its final pose back to the live rig. */
export type RagdollEffect=ReturnType<KillEffectFactory>&{root:T.Group};
/** Hinge joints (revolute about the joint's x axis) and their bend limits in rig convention: elbows flex negative, knees
 * positive, the neck nods within a small range. Shoulders and hips stay spherical. */
const HINGES:Record<string,[number,number]>={'left-elbow':[-2.6,.05],'right-elbow':[-2.6,.05],'left-knee':[-.05,2.6],'right-knee':[-.05,2.6],head:[-.55,.65]};
const COSMETIC_SOCKETS=new Set(['head-cosmetic','chest-cosmetic','back-socket']),wrapAngle=(a:number)=>Math.atan2(Math.sin(a),Math.cos(a));
/** Brief muscle tone: hinges hold their death bend and relax over .3 s, so the body buckles instead of going limp at once.
 * Limbs damp their spin more than the trunk; the slightly lower linear damping keeps the mean fling distance of the
 * (comedic) launch the same as before the hinges and weapon colliders were added (measured over specs, seeds and forces). */
const TONE_SECONDS=.3,TONE_STIFFNESS=90,TONE_DAMPING=9,LIMB_DAMPING=1.1,LINEAR_DAMPING=.27;
type CoverCopy={mesh:T.Mesh;position:T.Vector3;quaternion:T.Quaternion};
type ShownHinge={parent:T.Group;child:T.Group;rest:number;lo:number;hi:number;covers:CoverCopy[]};
const X_AXIS=new T.Vector3(1,0,0),_rel=new T.Quaternion(),_twist=new T.Quaternion();
/** Shown hinge pose, after the part meshes copy their bodies: Rapier's impulse limits are soft, so a hard impact (a long
 * weapon collider levering the forearm into the floor) can push a hinge past its limit for a few frames; the displayed child
 * keeps its swing but its bend is clamped to the joint's limits, so no rendered elbow, knee or neck ever over-bends (the
 * bodies are untouched). The knee and elbow covers then turn half of the bend change since death about the pivot, as
 * settleJointCovers does on the live rig, instead of freezing at the death angle. A pure function of the body state. */
function showHinge(h:ShownHinge){
 _rel.copy(h.parent.quaternion).invert().multiply(h.child.quaternion);if(_rel.w<0)_rel.set(-_rel.x,-_rel.y,-_rel.z,-_rel.w);
 const twist=2*Math.atan2(_rel.x,_rel.w),shown=Math.min(h.hi,Math.max(h.lo,twist));
 if(shown!==twist)h.child.quaternion.copy(h.parent.quaternion).multiply(_rel.multiply(_twist.setFromAxisAngle(X_AXIS,-twist))).multiply(_twist.setFromAxisAngle(X_AXIS,shown));
 if(h.covers.length){_twist.setFromAxisAngle(X_AXIS,-.5*(shown-h.rest));for(const c of h.covers){c.mesh.quaternion.copy(_twist).multiply(c.quaternion);c.mesh.position.copy(c.position).applyQuaternion(_twist);}}
}
export const ragdollVariant=(style:RagdollStyle='ragdoll',options:RagdollOptions={}):KillEffectFactory=>(scene,rig,p,death,quality,budget,state)=>{
 const world=new RAPIER.World({x:0,y:-18,z:0});world.timestep=1/60;const group=new T.Group();group.name=options.gib?'gibber-ragdoll':options.anchor?'knockdown-ragdoll':'ragdoll';scene.add(group);rig.updateMatrixWorld(true);const ownMaterials:T.Material[]=[];
 const names=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'];
 const roots=names.map(name=>rig.getObjectByName(name)).filter((o):o is T.Object3D=>!!o),rootSet=new Set(roots),parts=new Map<T.Object3D,{body:RAPIER.RigidBody;mesh:T.Group}>();
 // Match the current silhouette (including cosmetics), while severing only the animation hierarchy. Colliders come from the
 // body meshes only: weapons get their own light collider, cosmetics none, so a held weapon cannot prop up an arm.
 const frames=new Map<T.Object3D,T.Quaternion>(),coverCopies=new Map<T.Object3D,CoverCopy[]>();
 for(const root of roots.slice(0,budget)){
  const pos=root.getWorldPosition(new T.Vector3()),joint=root.getWorldQuaternion(new T.Quaternion()),hinge=HINGES[root.name];let rotation=joint;
  // Hinged joints (elbows, knees, neck) get a body frame twisted only about the joint's x axis relative to the parent part, so
  // the revolute joint starts exactly at rest; the meshes are expressed in that frame and look identical.
  let parentPart=root.parent;while(parentPart&&!rootSet.has(parentPart))parentPart=parentPart.parent;const parentFrame=parentPart&&frames.get(parentPart);
  if(hinge&&parentFrame){const rel=parentFrame.clone().invert().multiply(joint),twist=wrapAngle(2*Math.atan2(rel.x,rel.w));rotation=parentFrame.clone().multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),twist));}
  frames.set(root,rotation);
  const mesh=new T.Group(),inverse=new T.Matrix4().compose(pos,rotation,new T.Vector3(1,1,1)).invert();mesh.name=root.name;mesh.position.copy(pos);mesh.quaternion.copy(rotation);mesh.userData.jointOffset=rotation.clone().invert().multiply(joint);group.add(mesh);
  const bodyBounds=new T.Box3(),weaponBounds=new T.Box3(),box=new T.Box3();
  const collect=(node:T.Object3D,kind:'body'|'weapon'|'cosmetic')=>{if(node!==root&&rootSet.has(node))return;const zone=kind!=='body'?kind:node.name==='weapon-socket'||node.name==='offhand-weapon-socket'?'weapon':COSMETIC_SOCKETS.has(node.name)?'cosmetic':'body';
   if(node instanceof T.Mesh&&node.visible){const copy=copyVoxelPart(node);if(style!=='ragdoll'){copy.material=(node.material as T.Material).clone();ownMaterials.push(copy.material);if(style==='ragdoll-soul'){copy.material.transparent=true;copy.material.depthWrite=false;}}copy.name=node.name;const transform=inverse.clone().multiply(node.matrixWorld);transform.decompose(copy.position,copy.quaternion,copy.scale);mesh.add(copy);if(node.userData.jointCover){let list=coverCopies.get(root);if(!list)coverCopies.set(root,list=[]);list.push({mesh:copy,position:copy.position.clone(),quaternion:copy.quaternion.clone()});}
    if(zone!=='cosmetic'){node.geometry.computeBoundingBox();copy.updateMatrix();box.copy(node.geometry.boundingBox!).applyMatrix4(copy.matrix);(zone==='weapon'?weaponBounds:bodyBounds).union(box);}}
   if(node.visible)for(const child of node.children)collect(child,zone);};collect(root,'body');
  if(bodyBounds.isEmpty())bodyBounds.set(new T.Vector3(-.07,-.07,-.07),new T.Vector3(.07,.07,.07));const size=bodyBounds.getSize(new T.Vector3()).multiplyScalar(.5).clamp(new T.Vector3(.05,.05,.05),new T.Vector3(.4,.45,.4)),center=bodyBounds.getCenter(new T.Vector3());
  const limb=root.name!=='pelvis'&&root.name!=='spine';
  const body=world.createRigidBody((options.anchor&&root===roots[0]?RAPIER.RigidBodyDesc.kinematicPositionBased():RAPIER.RigidBodyDesc.dynamic()).setTranslation(pos.x,pos.y,pos.z).setRotation(rotation).setLinearDamping(LINEAR_DAMPING).setAngularDamping(limb?LIMB_DAMPING:.6).setCcdEnabled(true));world.createCollider(RAPIER.ColliderDesc.cuboid(size.x,size.y,size.z).setTranslation(center.x,center.y,center.z).setDensity(2).setFriction(.7).setRestitution(style==='ragdoll-pinball'?.82:.15).setCollisionGroups(0x00020001),body);
  if(!weaponBounds.isEmpty()){const w=weaponBounds.getSize(new T.Vector3()).multiplyScalar(.5).clamp(new T.Vector3(.02,.02,.02),new T.Vector3(.6,.6,.6)),c=weaponBounds.getCenter(new T.Vector3());world.createCollider(RAPIER.ColliderDesc.cuboid(w.x,w.y,w.z).setTranslation(c.x,c.y,c.z).setDensity(.25).setFriction(.45).setRestitution(.15).setCollisionGroups(0x00020001),body);}
  parts.set(root,{body,mesh});
 }
 const sever=options.gib?(death.gibPart||gibPartAt(death.pos,death.yaw,death.hitPoint||death.pos)):undefined;group.userData.gibPart=sever;let wound:{part:{body:RAPIER.RigidBody;mesh:T.Group};point:T.Vector3;normal:T.Vector3}|undefined;const hinges:{joint:RAPIER.RevoluteImpulseJoint;rest:number}[]=[],shown:ShownHinge[]=[];const woundGeometry=new T.CircleGeometry(.09,8),woundMaterial=new T.MeshLambertMaterial({color:0x8e071d,side:T.DoubleSide});
 for(const [root,part]of parts){let parent=root.parent;while(parent&&!parts.has(parent))parent=parent.parent;if(!parent)continue;const other=parts.get(parent)!,point=root.getWorldPosition(new T.Vector3()),anchor=point.clone().sub(other.mesh.position).applyQuaternion(other.mesh.quaternion.clone().invert());if(root.name===sever){const normal=point.clone().sub(other.mesh.position).normalize().applyQuaternion(other.mesh.quaternion.clone().invert());wound={part:other,point:anchor,normal};if(budget-parts.size>=2)for(const [mesh,position,direction]of [[other.mesh,anchor,normal],[part.mesh,new T.Vector3(),normal.clone().negate()]] as const){const cap=new T.Mesh(woundGeometry,woundMaterial);cap.name='gibber-wound';cap.position.copy(position);cap.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),direction);mesh.add(cap);}continue;}const hinge=HINGES[root.name];if(hinge){const rest=wrapAngle(2*Math.atan2(other.mesh.quaternion.clone().invert().multiply(part.mesh.quaternion).x,other.mesh.quaternion.clone().invert().multiply(part.mesh.quaternion).w)),joint=world.createImpulseJoint(RAPIER.JointData.revolute(anchor,{x:0,y:0,z:0},{x:1,y:0,z:0}),other.body,part.body,true) as RAPIER.RevoluteImpulseJoint;joint.setContactsEnabled(false);const lo=Math.min(hinge[0],rest-.02),hi=Math.max(hinge[1],rest+.02);joint.setLimits(lo,hi);hinges.push({joint,rest});shown.push({parent:other.mesh,child:part.mesh,rest,lo,hi,covers:coverCopies.get(root)||[]});continue;}const joint=world.createImpulseJoint(RAPIER.JointData.spherical(anchor,{x:0,y:0,z:0}),other.body,part.body,true);joint.setContactsEnabled(false);}
 const floor=RAPIER.ColliderDesc.cuboid(ARENA.width/2,.5,ARENA.depth/2).setTranslation(0,-.5,0).setCollisionGroups(0x00010002).setFriction(.8);world.createCollider(floor);
 // Nearby snapshot geometry catches limbs on the same cover the players see. Budget stays bounded.
 for(const prop of (state?.props||[]).filter(x=>x.size.y<20&&x.size.x<60&&Math.hypot(x.pos.x-death.pos.x,x.pos.z-death.pos.z)<9).slice(0,32)){const desc=RAPIER.ColliderDesc.cuboid(prop.size.x/2,prop.size.y/2,prop.size.z/2).setTranslation(prop.pos.x,prop.pos.y,prop.pos.z).setCollisionGroups(0x00010002);if(prop.rot)desc.setRotation(prop.rot);world.createCollider(desc);}
 const dir=new T.Vector3(death.direction.x,death.direction.y,death.direction.z);if(dir.lengthSq()<.001)dir.set(Math.sin(p.yaw),.1,Math.cos(p.yaw));dir.normalize();const point=new T.Vector3((death.hitPoint||death.pos).x,(death.hitPoint||death.pos).y,(death.hitPoint||death.pos).z);
 let seed=death.seed;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};const strength=death.melee?8+Math.min(7,(death.force||0)/100):2.2+Math.min(2,(death.force||0)/160),inherited=new T.Vector3(death.velocity.x,death.velocity.y,death.velocity.z).clampLength(0,5).multiplyScalar(.3);
 for(const {body,mesh}of parts.values()){if(body.isKinematic())continue;const distance=mesh.position.distanceTo(point),local=1+Math.exp(-distance*4)*.9,velocity=dir.clone().multiplyScalar(strength*local).add(inherited);if(death.effect==='impact-ragdoll'&&death.motion)velocity.copy(bodyPartVelocity(death.motion,death.wound,mesh.name,death.direction));else velocity.y+=death.melee?3.8:1;body.setLinvel(velocity,true);const lever=point.clone().sub(mesh.position),spin=lever.cross(dir).multiplyScalar(death.melee?15:5);spin.add(new T.Vector3(random()-.5,random()-.5,random()-.5).multiplyScalar(death.melee?5:1.5)).clampLength(0,18);body.setAngvel(spin,true);if(mesh.name===sever&&death.effect!=='impact-ragdoll'){body.setLinvel(velocity.multiplyScalar(1.65).add(new T.Vector3(0,5,0)),true);body.setAngvel({x:11,y:8,z:-9},true);}}
 const origin=new T.Vector3(death.pos.x,death.pos.y,death.pos.z),vfx=style==='ragdoll'?undefined:new RagdollVfx(style,death.seed,Math.max(0,budget-parts.size),quality);if(vfx)scene.add(vfx.root);const blood=options.gib?new GibberVfx(death.seed,Math.max(0,budget-parts.size-2),quality):undefined;if(blood)scene.add(blood.root);const bodies=[...parts.values()];let elapsed=0,disposed=false;
 const effect:RagdollEffect={root:group,count:parts.size+(vfx?.count||0)+(blood?.count||0)+(wound&&budget-parts.size>=2?2:0),update(age){if(disposed)return;const target=Math.min(5,Math.max(0,age));while(elapsed+1/60<=target+1e-6){ragdollForce(style,bodies,elapsed,origin);if(elapsed<=TONE_SECONDS+1e-6){const tone=Math.max(0,1-elapsed/TONE_SECONDS);for(const h of hinges)h.joint.configureMotorPosition(h.rest,TONE_STIFFNESS*tone,TONE_DAMPING*tone);}if(options.anchor){const target=options.anchor(),pelvis=parts.get(roots[0])!.body;pelvis.setNextKinematicTranslation(target.position);pelvis.setNextKinematicRotation(target.rotation);}world.step();if(blood&&wound){const q=new T.Quaternion().copy(wound.part.body.rotation()),point=wound.point.clone().applyQuaternion(q).add(new T.Vector3().copy(wound.part.body.translation()));blood.step(elapsed,point,wound.normal.clone().applyQuaternion(q));}elapsed+=1/60;}const fade=Math.min(1,Math.max(0,(5-target)/.65)),sink=(1-fade)*(1-fade);for(const {body,mesh}of parts.values()){mesh.position.copy(body.translation());mesh.position.y-=.35*sink;mesh.quaternion.copy(body.rotation());mesh.scale.setScalar(.85+.15*fade);}for(const h of shown)showHinge(h);const center=parts.get(roots[0])?.mesh.position||origin;vfx?.update(target,center,bodies);blood?.update(target);},dispose(){if(disposed)return;disposed=true;group.removeFromParent();world.free();parts.clear();vfx?.dispose();blood?.dispose();woundGeometry.dispose();woundMaterial.dispose();ownMaterials.forEach(m=>m.dispose());}};
 return effect;
};

export const ragdoll=ragdollVariant();
