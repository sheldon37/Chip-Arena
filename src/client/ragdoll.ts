import {bodyPartVelocity} from '../shared/body-ragdoll.js';
import {GibberVfx,SEVER_PROFILE,CRUSH_PROFILE} from './gibber-vfx.js';
import {gibPartAt} from '../shared/body-hit.js';
import {RagdollVfx,ragdollForce} from './ragdoll-vfx.js';
import type {RagdollStyle} from '../shared/ragdoll-styles.js';
import * as T from 'three';
import RAPIER from '@dimforge/rapier3d-compat';
import type {KillEffectFactory} from './death-effects.js';
import {ARENA} from '../shared/arena.js';
import {copyVoxelPart,releaseVoxelLayout} from './voxel-kit.js';
import {woundCap,faceAlong,boneSign,CAP_MESH,CrushDebris,type DebrisSource,type DebrisObject} from './body-trauma.js';
import {KneelFall,partBoxes,type CollapsePart} from './body-collapse.js';
/** Cosmetic-only articulated bodies in a private, disposable world. Never adds server bodies or damage. */
export interface RagdollOptions {gib?:boolean;anchor?:()=>{position:T.Vector3;rotation:T.Quaternion};
 /** The public impact ragdoll (docs/BODY-DAMAGE-HANDOFF.md): consumes the authority's fatal wound and death motion. A
  * severed part detaches with matching voxel caps, a crushed part breaks into bounded fragments, and the gated
  * fast-headshot death kneels and falls forward before the ragdoll takes over. Nothing here chooses an outcome. */
 impact?:boolean;}
/** The effect ragdollVariant returns also exposes its part group (children named after the rig joints they copy), so a
 * knockdown ragdoll can hand its final pose back to the live rig. */
export type RagdollEffect=ReturnType<KillEffectFactory>&{root:T.Group};
/** Hinge joints (revolute about the joint's x axis) and their bend limits in rig convention: elbows flex negative, knees
 * positive, the neck nods within a small range. Shoulders and hips stay spherical. */
const HINGES:Record<string,[number,number]>={'left-elbow':[-2.6,.05],'right-elbow':[-2.6,.05],'left-knee':[-.05,2.6],'right-knee':[-.05,2.6],head:[-.55,.65]};
const COSMETIC_SOCKETS=new Set(['head-cosmetic','chest-cosmetic','back-socket']),WEAPON_SOCKETS=new Set(['weapon-socket','offhand-weapon-socket']),wrapAngle=(a:number)=>Math.atan2(Math.sin(a),Math.cos(a));
/** Brief muscle tone: hinges hold their death bend and relax over .3 s, so the body buckles instead of going limp at once.
 * Limbs damp their spin more than the trunk; the slightly lower linear damping keeps the mean fling distance of the
 * (comedic) launch the same as before the hinges and weapon colliders were added (measured over specs, seeds and forces). */
const TONE_SECONDS=.3,TONE_STIFFNESS=90,TONE_DAMPING=9,LIMB_DAMPING=1.1,LINEAR_DAMPING=.27;
type CoverCopy={mesh:T.Mesh;position:T.Vector3;quaternion:T.Quaternion};
type ShownHinge={parent:T.Group;child:T.Group;rest:number;lo:number;hi:number;covers:CoverCopy[]};
const X_AXIS=new T.Vector3(1,0,0),ONE=new T.Vector3(1,1,1),_rel=new T.Quaternion(),_twist=new T.Quaternion();
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
/** A copy of a socket's visible meshes (hat, held weapon) at the socket's world transform: a loose crush fragment. */
function socketCopy(socket:T.Object3D){
 const g=new T.Group();g.name='crush-'+socket.name;socket.matrixWorld.decompose(g.position,g.quaternion,g.scale);const inverse=socket.matrixWorld.clone().invert();
 const walk=(o:T.Object3D)=>{if(!o.visible)return;if(o instanceof T.Mesh){const copy=copyVoxelPart(o);copy.name=o.name;inverse.clone().multiply(o.matrixWorld).decompose(copy.position,copy.quaternion,copy.scale);g.add(copy);}for(const c of o.children)walk(c);};
 walk(socket);return g.children.length?g:undefined;
}
export const ragdollVariant=(style:RagdollStyle='ragdoll',options:RagdollOptions={}):KillEffectFactory=>(scene,rig,p,death,quality,budget,state)=>{
 const world=new RAPIER.World({x:0,y:-18,z:0});world.timestep=1/60;const group=new T.Group();group.name=options.gib?'gibber-ragdoll':options.anchor?'knockdown-ragdoll':'ragdoll';scene.add(group);rig.updateMatrixWorld(true);const ownMaterials:T.Material[]=[];
 const names=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'];
 const roots=names.map(name=>rig.getObjectByName(name)).filter((o):o is T.Object3D=>!!o),rootSet=new Set(roots),parts=new Map<T.Object3D,{body:RAPIER.RigidBody;mesh:T.Group}>();
 // The authority's fatal wound (impact ragdoll only): the part it severed or crushed. A destroyed root owns its subtree.
 const fatal=options.impact?death.wound:undefined,destroyedName=fatal?.destroyedPart&&(fatal.kind==='sever'||fatal.kind==='crush')?fatal.destroyedPart:undefined;
 const detachedRoot=destroyedName?rig.getObjectByName(destroyedName):undefined,crushRoot=fatal?.kind==='crush'?detachedRoot:undefined;
 const inDetached=(o:T.Object3D)=>{if(!detachedRoot)return false;for(let a:T.Object3D|null=o;a;a=a.parent)if(a===detachedRoot)return true;return false;};
 // Match the current silhouette (including cosmetics), while severing only the animation hierarchy. Colliders come from the
 // body meshes only: weapons get their own light collider, cosmetics none, so a held weapon cannot prop up an arm.
 const frames=new Map<T.Object3D,T.Quaternion>(),coverCopies=new Map<T.Object3D,CoverCopy[]>(),ankleCopies=new Map<T.Object3D,CoverCopy[]>(),ankles=new Map<T.Object3D,{pivot:T.Vector3;frame:T.Quaternion}>(),bodyInverses=new Map<T.Object3D,T.Matrix4[]>();
 const debrisSources:DebrisSource[]=[],debrisLoose:DebrisObject[]=[];
 for(const root of roots.slice(0,budget)){
  const pos=root.getWorldPosition(new T.Vector3()),joint=root.getWorldQuaternion(new T.Quaternion()),hinge=HINGES[root.name];let rotation=joint;
  // Hinged joints (elbows, knees, neck) get a body frame twisted only about the joint's x axis relative to the parent part, so
  // the revolute joint starts exactly at rest; the meshes are expressed in that frame and look identical.
  let parentPart=root.parent;while(parentPart&&!rootSet.has(parentPart))parentPart=parentPart.parent;const parentFrame=parentPart&&frames.get(parentPart);
  if(hinge&&parentFrame){const rel=parentFrame.clone().invert().multiply(joint),twist=wrapAngle(2*Math.atan2(rel.x,rel.w));rotation=parentFrame.clone().multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),twist));}
  frames.set(root,rotation);
  const inverses:T.Matrix4[]=[];bodyInverses.set(root,inverses);
  // A crushed part gets no body: its meshes become bounded fragments, a hat or weapon it carried flies loose.
  if(root===crushRoot){const walk=(node:T.Object3D)=>{if(node!==root&&rootSet.has(node)||!node.visible)return;if(COSMETIC_SOCKETS.has(node.name)||WEAPON_SOCKETS.has(node.name)){const loose=socketCopy(node);if(loose)debrisLoose.push({object:loose});return;}
    if(node instanceof T.Mesh&&!node.userData.bodyWound){if(!node.userData.detail)inverses.push(node.matrixWorld.clone().invert());const color=(node.material as T.MeshLambertMaterial).color;debrisSources.push({matrix:node.matrixWorld.clone(),color:color?color.clone():new T.Color(p.cosmetics?.bodyColor||0xdce5e8)});}
    for(const child of node.children)walk(child);};walk(root);continue;}
  const mesh=new T.Group(),inverse=new T.Matrix4().compose(pos,rotation,new T.Vector3(1,1,1)).invert();mesh.name=root.name;mesh.position.copy(pos);mesh.quaternion.copy(rotation);mesh.userData.jointOffset=rotation.clone().invert().multiply(joint);group.add(mesh);
  const bodyBounds=new T.Box3(),weaponBounds=new T.Box3(),box=new T.Box3();
  const collect=(node:T.Object3D,kind:'body'|'weapon'|'cosmetic',underAnkle=false)=>{if(node!==root&&rootSet.has(node))return;const zone=kind!=='body'?kind:WEAPON_SOCKETS.has(node.name)?'weapon':COSMETIC_SOCKETS.has(node.name)?'cosmetic':'body',ankle=underAnkle||node.name.endsWith('-ankle');
   if(node instanceof T.Mesh&&node.visible){const copy=copyVoxelPart(node);if(style!=='ragdoll'){copy.material=(node.material as T.Material).clone();ownMaterials.push(copy.material);if(style==='ragdoll-soul'){copy.material.transparent=true;copy.material.depthWrite=false;}}copy.name=node.name;const transform=inverse.clone().multiply(node.matrixWorld);transform.decompose(copy.position,copy.quaternion,copy.scale);mesh.add(copy);if(node.userData.jointCover){let list=coverCopies.get(root);if(!list)coverCopies.set(root,list=[]);list.push({mesh:copy,position:copy.position.clone(),quaternion:copy.quaternion.clone()});}
    // Living wounds ride along as their own cells; they never shape a collider.
    if(node.userData.bodyWound)copy.userData.bodyWound=true;else if(zone!=='cosmetic'){node.geometry.computeBoundingBox();copy.updateMatrix();box.copy(node.geometry.boundingBox!).applyMatrix4(copy.matrix);(zone==='weapon'?weaponBounds:bodyBounds).union(box);}
    if(zone!=='body')copy.userData.collapseSkip=true;else if(!node.userData.bodyWound&&!node.userData.detail)inverses.push(node.matrixWorld.clone().invert());
    // Feet (and anything else below the ankle) can pivot visually during a scripted collapse; the body stays one part.
    if(ankle){copy.userData.ankle=true;let list=ankleCopies.get(root);if(!list)ankleCopies.set(root,list=[]);list.push({mesh:copy,position:copy.position.clone(),quaternion:copy.quaternion.clone()});}}
   if(node.visible)for(const child of node.children)collect(child,zone,ankle);};collect(root,'body');
  const ankleJoint=root.name.endsWith('-knee')?rig.getObjectByName(root.name.replace(/-knee$/,'-ankle')):undefined;
  if(ankleJoint)ankles.set(root,{pivot:ankleJoint.getWorldPosition(new T.Vector3()).applyMatrix4(inverse),frame:rotation.clone().invert().multiply(ankleJoint.getWorldQuaternion(new T.Quaternion()))});
  if(bodyBounds.isEmpty())bodyBounds.set(new T.Vector3(-.07,-.07,-.07),new T.Vector3(.07,.07,.07));const size=bodyBounds.getSize(new T.Vector3()).multiplyScalar(.5).clamp(new T.Vector3(.05,.05,.05),new T.Vector3(.4,.45,.4)),center=bodyBounds.getCenter(new T.Vector3());
  const limb=root.name!=='pelvis'&&root.name!=='spine';
  const body=world.createRigidBody((options.anchor&&root===roots[0]?RAPIER.RigidBodyDesc.kinematicPositionBased():RAPIER.RigidBodyDesc.dynamic()).setTranslation(pos.x,pos.y,pos.z).setRotation(rotation).setLinearDamping(LINEAR_DAMPING).setAngularDamping(limb?LIMB_DAMPING:.6).setCcdEnabled(true));world.createCollider(RAPIER.ColliderDesc.cuboid(size.x,size.y,size.z).setTranslation(center.x,center.y,center.z).setDensity(2).setFriction(.7).setRestitution(style==='ragdoll-pinball'?.82:.15).setCollisionGroups(0x00020001),body);
  if(!weaponBounds.isEmpty()){const w=weaponBounds.getSize(new T.Vector3()).multiplyScalar(.5).clamp(new T.Vector3(.02,.02,.02),new T.Vector3(.6,.6,.6)),c=weaponBounds.getCenter(new T.Vector3());world.createCollider(RAPIER.ColliderDesc.cuboid(w.x,w.y,w.z).setTranslation(c.x,c.y,c.z).setDensity(.25).setFriction(.45).setRestitution(.15).setCollisionGroups(0x00020001),body);}
  parts.set(root,{body,mesh});
 }
 const sever=options.gib?(death.gibPart||gibPartAt(death.pos,death.yaw,death.hitPoint||death.pos)):fatal?.kind==='sever'?destroyedName:undefined;group.userData.gibPart=sever??(crushRoot?destroyedName:undefined);
 let wound:{part:{body:RAPIER.RigidBody;mesh:T.Group};point:T.Vector3;normal:T.Vector3}|undefined;const hinges:{joint:RAPIER.RevoluteImpulseJoint;rest:number;name:string}[]=[],shown:ShownHinge[]=[],caps:T.InstancedMesh[]=[],anchors=new Map<string,{parent:string;anchor:T.Vector3}>();
 // Exposed cross-sections where a part came off: a clean blade/bullet cut or a ragged crushed stump, sized like the
 // thinner side of the joint and set flush with the end of each part's geometry. They ride on the part bodies.
 let capBudget=Math.max(0,budget-parts.size);const low=quality<.5;
 const addCap=(root:T.Object3D,on:{mesh:T.Group},side:'parent'|'child',kind:'clean'|'ragged',owner:T.Object3D)=>{
  if(capBudget<1)return undefined;capBudget--;
  const jq=root.getWorldQuaternion(new T.Quaternion()),jp=root.getWorldPosition(new T.Vector3()),sign=boneSign(root.name),bone=new T.Vector3(0,sign,0).applyQuaternion(jq),out=side==='parent'?1:-1;
  const section=rig.getObjectByName(CAP_MESH[root.name]||''),scale=section?section.getWorldScale(new T.Vector3()):new T.Vector3(.16,.16,.16),material=section instanceof T.Mesh?section.material as T.MeshLambertMaterial:undefined;
  const cap=woundCap({width:scale.x*.92,depth:scale.z*.92,kind,seed:(death.seed^Math.imul(caps.length+1,0x9e3779b1))>>>0,shell:material?.color?material.color.clone():new T.Color(p.cosmetics?.bodyColor||0xdce5e8),bone:root.name==='spine'?'back':'centre',low});
  const s=faceAlong(jp,bone,bodyInverses.get(owner)||[],side),position=jp.addScaledVector(bone,s-out*.012),rotation=jq.multiply(new T.Quaternion().setFromAxisAngle(X_AXIS,sign*out>0?0:Math.PI));
  new T.Matrix4().compose(on.mesh.position,on.mesh.quaternion,ONE).invert().multiply(new T.Matrix4().compose(position,rotation,ONE)).decompose(cap.position,cap.quaternion,cap.scale);
  on.mesh.add(cap);caps.push(cap);return {point:cap.position.clone(),normal:new T.Vector3(0,1,0).applyQuaternion(cap.quaternion)};
 };
 for(const [root,part]of parts){
  // Direct children of a crushed part are thrown free, each with a ragged end where it was attached.
  let nearest=root.parent;while(nearest&&!rootSet.has(nearest))nearest=nearest.parent;if(crushRoot&&nearest===crushRoot){addCap(root,part,'child','ragged',root);continue;}
  let parent=root.parent;while(parent&&!parts.has(parent))parent=parent.parent;if(!parent)continue;const other=parts.get(parent)!,point=root.getWorldPosition(new T.Vector3()),anchor=point.clone().sub(other.mesh.position).applyQuaternion(other.mesh.quaternion.clone().invert());
  if(root.name===sever){const normal=point.clone().sub(other.mesh.position).normalize().applyQuaternion(other.mesh.quaternion.clone().invert());wound={part:other,point:anchor,normal};
   if(capBudget>=2){const stump=addCap(root,other,'parent','clean',parent);addCap(root,part,'child','clean',root);if(stump&&options.impact)wound={part:other,...stump};}continue;}
  anchors.set(root.name,{parent:other.mesh.name,anchor:anchor.clone()});
  const hinge=HINGES[root.name];if(hinge){const rest=wrapAngle(2*Math.atan2(other.mesh.quaternion.clone().invert().multiply(part.mesh.quaternion).x,other.mesh.quaternion.clone().invert().multiply(part.mesh.quaternion).w)),joint=world.createImpulseJoint(RAPIER.JointData.revolute(anchor,{x:0,y:0,z:0},{x:1,y:0,z:0}),other.body,part.body,true) as RAPIER.RevoluteImpulseJoint;joint.setContactsEnabled(false);const lo=Math.min(hinge[0],rest-.02),hi=Math.max(hinge[1],rest+.02);joint.setLimits(lo,hi);hinges.push({joint,rest,name:root.name});shown.push({parent:other.mesh,child:part.mesh,rest,lo,hi,covers:coverCopies.get(root)||[]});continue;}const joint=world.createImpulseJoint(RAPIER.JointData.spherical(anchor,{x:0,y:0,z:0}),other.body,part.body,true);joint.setContactsEnabled(false);}
 // The stump a crushed part leaves on its parent.
 if(crushRoot){let parent=crushRoot.parent;while(parent&&!parts.has(parent))parent=parent.parent;const other=parent&&parts.get(parent);if(parent&&other){const point=crushRoot.getWorldPosition(new T.Vector3()),inverse=other.mesh.quaternion.clone().invert();
  wound={part:other,point:point.clone().sub(other.mesh.position).applyQuaternion(inverse),normal:point.clone().sub(other.mesh.position).normalize().applyQuaternion(inverse)};const stump=addCap(crushRoot,other,'parent','ragged',parent);if(stump)wound={part:other,...stump};}}
 const floor=RAPIER.ColliderDesc.cuboid(ARENA.width/2,.5,ARENA.depth/2).setTranslation(0,-.5,0).setCollisionGroups(0x00010002).setFriction(.8);world.createCollider(floor);
 // Nearby snapshot geometry catches limbs on the same cover the players see. Budget stays bounded.
 const nearby=(state?.props||[]).filter(x=>x.size.y<20&&x.size.x<60&&Math.hypot(x.pos.x-death.pos.x,x.pos.z-death.pos.z)<9).slice(0,32);
 for(const prop of nearby){const desc=RAPIER.ColliderDesc.cuboid(prop.size.x/2,prop.size.y/2,prop.size.z/2).setTranslation(prop.pos.x,prop.pos.y,prop.pos.z).setCollisionGroups(0x00010002);if(prop.rot)desc.setRotation(prop.rot);world.createCollider(desc);}
 const dir=new T.Vector3(death.direction.x,death.direction.y,death.direction.z);if(dir.lengthSq()<.001)dir.set(Math.sin(p.yaw),.1,Math.cos(p.yaw));dir.normalize();const point=new T.Vector3((death.hitPoint||death.pos).x,(death.hitPoint||death.pos).y,(death.hitPoint||death.pos).z);
 let seed=death.seed;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};const strength=death.melee?8+Math.min(7,(death.force||0)/100):2.2+Math.min(2,(death.force||0)/160),inherited=new T.Vector3(death.velocity.x,death.velocity.y,death.velocity.z).clampLength(0,5).multiplyScalar(.3);
 // Impact ragdoll: the authority's resulting velocity once, plus the helper's bounded kick for the whole detached subtree.
 // Legacy cosmetics deliberately fling and spin every part. An impact death must not inherit that independent torque:
 // even a nearly stationary melee victim received up to 18 rad/s on untouched limbs. Contacts/joints (or the collapse's
 // pose velocities) now supply its angular motion. Old snapshots without motion retain the cosmetic fallback.
 const impactMotion=options.impact?death.motion:undefined;
 for(const [root,{body,mesh}]of parts){
  if(body.isKinematic())continue;
  if(impactMotion){body.setLinvel(bodyPartVelocity(impactMotion,death.wound,destroyedName&&inDetached(root)?destroyedName:mesh.name,death.direction),true);body.setAngvel({x:0,y:0,z:0},true);continue;}
  const distance=mesh.position.distanceTo(point),local=1+Math.exp(-distance*4)*.9,velocity=dir.clone().multiplyScalar(strength*local).add(inherited);velocity.y+=death.melee?3.8:1;body.setLinvel(velocity,true);
  const lever=point.clone().sub(mesh.position),spin=lever.cross(dir).multiplyScalar(death.melee?15:5);spin.add(new T.Vector3(random()-.5,random()-.5,random()-.5).multiplyScalar(death.melee?5:1.5)).clampLength(0,18);body.setAngvel(spin,true);
  if(mesh.name===sever&&death.effect!=='impact-ragdoll'){body.setLinvel(velocity.multiplyScalar(1.65).add(new T.Vector3(0,5,0)),true);body.setAngvel({x:11,y:8,z:-9},true);}
 }
 // The authority-selected fast-headshot death: knees, then a forward fall, then these same bodies take over.
 const byName=new Map([...parts].map(([root,part])=>[root.name,part]));let collapse:KneelFall|undefined;
 if(options.impact&&death.motion?.kind==='kneel-fall'){const list:CollapsePart[]=[...parts].map(([root,{mesh}])=>({name:root.name,parent:anchors.get(root.name)?.parent,position:mesh.position.clone(),quaternion:mesh.quaternion.clone(),anchor:anchors.get(root.name)?.anchor,free:root!==roots[0]&&!anchors.has(root.name),boxes:partBoxes(mesh),ankle:ankles.get(root)}));
  if(KneelFall.supports(list))collapse=new KneelFall(list,death.motion,{ground:death.pos.y-.85,shot:death.wound?.direction,severity:death.wound?.severity,props:nearby});}
 const toneStart=collapse?collapse.release:0;let restsSet=!collapse,releaseFeet:Map<string,{ankle?:T.Quaternion}>|undefined;
 const drive=(t:number)=>collapse!.each(t,(name,pose,linear,angular)=>{const part=byName.get(name);if(!part)return;part.body.setTranslation(pose.position,true);part.body.setRotation(pose.quaternion,true);part.body.setLinvel(linear,true);part.body.setAngvel(angular,true);});
 const origin=new T.Vector3(death.pos.x,death.pos.y,death.pos.z),vfx=style==='ragdoll'?undefined:new RagdollVfx(style,death.seed,Math.max(0,budget-parts.size),quality);if(vfx)scene.add(vfx.root);
 // Crush fragments share the remaining budget with the spray.
 const remaining=Math.max(0,budget-parts.size-caps.length),detachedVelocity=crushRoot&&death.motion?bodyPartVelocity(death.motion,death.wound,destroyedName!,death.direction):death.velocity;
 const debris=crushRoot&&(debrisSources.length||debrisLoose.length)?new CrushDebris({sources:debrisSources,loose:debrisLoose,origin:crushRoot.getWorldPosition(new T.Vector3()),direction:dir,inherited:new T.Vector3(detachedVelocity.x,detachedVelocity.y,detachedVelocity.z),kick:2,severity:fatal?.severity??1,seed:death.seed,limit:Math.min(low?16:40,Math.floor(remaining*.45)),floor:death.pos.y-.85}):undefined;if(debris)scene.add(debris.root);
 const sprayBudget=options.gib?Math.max(0,budget-parts.size-2):Math.max(0,remaining-(debris?.count||0));
 const blood=options.gib?new GibberVfx(death.seed,sprayBudget,quality):wound&&options.impact?new GibberVfx(death.seed,sprayBudget,quality,{...(crushRoot?CRUSH_PROFILE:SEVER_PROFILE),floor:death.pos.y-.85}):undefined;if(blood)scene.add(blood.root);const bodies=[...parts.values()];let elapsed=0,disposed=false,released=!collapse;
 const effect:RagdollEffect={root:group,count:parts.size+(vfx?.count||0)+(blood?.count||0)+caps.length+(debris?.count||0),update(age){if(disposed)return;const target=Math.min(5,Math.max(0,age));while(elapsed+1/60<=target+1e-6){if(collapse&&elapsed<collapse.release-1e-6)drive(elapsed);ragdollForce(style,bodies,elapsed,origin);
   // Tone holds the bend at the start of the ragdoll: at death, or where the collapse hands over.
   if(!restsSet&&elapsed>=toneStart-1e-6){for(const h of hinges)h.rest=collapse!.hinge(toneStart,h.name);restsSet=true;}const toneAge=elapsed-toneStart;if(toneAge>=-1e-6&&toneAge<=TONE_SECONDS+1e-6){const tone=Math.max(0,1-toneAge/TONE_SECONDS);for(const h of hinges)h.joint.configureMotorPosition(h.rest,TONE_STIFFNESS*tone,TONE_DAMPING*tone);}
   if(options.anchor){const target=options.anchor(),pelvis=parts.get(roots[0])!.body;pelvis.setNextKinematicTranslation(target.position);pelvis.setNextKinematicRotation(target.rotation);}world.step();
   // The displayed scripted pose ignores solver corrections while it owns the body. At the release boundary copy that
   // exact pose AND its velocities once, rather than reveal the previous driven step's corrected bodies (a ~10 cm arm
   // pop with real Rapier). Detached parts are not driven; subsequent steps are entirely physical.
   if(!released&&elapsed+1/60>=collapse!.release-1e-6){drive(collapse!.release);released=true;}
   if(blood&&wound){const q=new T.Quaternion().copy(wound.part.body.rotation()),point=wound.point.clone().applyQuaternion(q).add(new T.Vector3().copy(wound.part.body.translation()));blood.step(elapsed,point,wound.normal.clone().applyQuaternion(q));}elapsed+=1/60;}
  const fade=Math.min(1,Math.max(0,(5-target)/.65)),sink=(1-fade)*(1-fade),pose=collapse&&target<collapse.release-1e-6?collapse.pose(target):undefined;
  for(const [root,{body,mesh}]of parts){const scripted=pose?.get(root.name);if(scripted){mesh.position.copy(scripted.position);mesh.quaternion.copy(scripted.quaternion);}else{mesh.position.copy(body.translation());mesh.quaternion.copy(body.rotation());}mesh.position.y-=.35*sink;mesh.scale.setScalar(.85+.15*fade);}
  // Feet keep the ankle angle the collapse reached (it holds at the release angle afterwards).
  if(collapse){const feet=pose||(releaseFeet??=new Map([...collapse.pose(collapse.release)].map(([name,entry])=>[name,{ankle:entry.ankle?.clone()}])));for(const [root,list]of ankleCopies){const turn=feet.get(root.name)?.ankle,pivot=ankles.get(root)?.pivot;if(!turn||!pivot)continue;for(const c of list){c.mesh.quaternion.copy(turn).multiply(c.quaternion);c.mesh.position.copy(c.position).sub(pivot).applyQuaternion(turn).add(pivot);}}}
  for(const h of shown)showHinge(h);const center=parts.get(roots[0])?.mesh.position||origin;vfx?.update(target,center,bodies);blood?.update(target);debris?.update(target);},
  dispose(){if(disposed)return;disposed=true;group.traverse(o=>releaseVoxelLayout(o));group.removeFromParent();for(const cap of caps)cap.dispose();world.free();parts.clear();vfx?.dispose();blood?.dispose();debris?.dispose();ownMaterials.forEach(m=>m.dispose());}};
 return effect;
};

export const ragdoll=ragdollVariant();
