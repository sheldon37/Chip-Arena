import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import {createCharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {buildWeapon} from './weapon-visuals.js';
import {fighterGuard} from './ansatsuken-animation.js';
import {heavenMark} from './ansatsuken-intro.js';
import {RAGING_DEMON as D} from '../shared/raging-demon.js';
const ease=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
/** Batched attacker silhouettes glide into the victim. Cosmetic only; no duplicate actors or hits. */
export const ragingDemon:KillEffectFactory=(scene,_original,p,d,quality,budget)=>{
 const root=new T.Group();root.name='raging-demon';scene.add(root);root.position.set(d.pos.x,d.pos.y,d.pos.z);
 const victim=createCharacterRig(p.color,p.cosmetics?.bodyColor);dressRig(victim,p.cosmetics);root.add(victim.group);victim.group.rotation.y=d.yaw;victim.weapon.visible=victim.offWeapon.visible=false;
 const a=d.attacker,source=createCharacterRig(a?.color||'#ff274c',a?.cosmetics.bodyColor);dressRig(source,a?.cosmetics);if(a?.spec)buildWeapon(source.weapon,a.spec);fighterGuard(source,0);source.leftArm.rotation.x=-1.25;source.leftElbow.rotation.x=-.2;source.group.updateMatrixWorld(true);
 const boxes:T.Matrix4[]=[];source.group.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type==='BoxGeometry')boxes.push(o.matrixWorld.clone());});
 const geometry=new T.BoxGeometry(1,1,1),ghosts:T.InstancedMesh[]=[],materials:T.MeshBasicMaterial[]=[];
 const start=a?new T.Vector3(a.pos.x-d.pos.x,a.pos.y-d.pos.y,a.pos.z-d.pos.z):new T.Vector3(-4,0,0);if(start.length()<2)start.set(-Math.sin(d.yaw)*3,0,-Math.cos(d.yaw)*3);start.clampLength(0,12);
 const yaw=Math.atan2(-start.x,-start.z);for(let i=0;i<Math.min(Math.max(0,budget-1),quality<.5?1:3);i++){const material=new T.MeshBasicMaterial({color:i===0?0xff2c53:0x691933,transparent:true,opacity:.7-i*.16,depthWrite:false,toneMapped:false});materials.push(material);const ghost=new T.InstancedMesh(geometry,material,boxes.length);ghost.name='demon-afterimage';boxes.forEach((m,j)=>ghost.setMatrixAt(j,m));ghost.rotation.y=yaw;ghost.frustumCulled=false;root.add(ghost);ghosts.push(ghost);}
 const mark=heavenMark();mark.name='demon-finish-mark';mark.scale.setScalar(3);mark.position.set(0,1.1,0);root.add(mark);
 const ownedGeometry=new Set<T.BufferGeometry>(),ownedMaterials=new Set<T.Material>(materials);for(const rig of [victim,source])rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry'){ownedGeometry.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material])ownedMaterials.add(m);}});mark.traverse(o=>{if(o instanceof T.Mesh){ownedGeometry.add(o.geometry);ownedMaterials.add(o.material as T.Material);}});
 return {count:ghosts.length+1,update(age){
  ghosts.forEach((g,i)=>{const u=ease((age-i*.055)/D.contact);g.position.copy(start).multiplyScalar(1-u);g.position.y+=Math.sin(u*Math.PI)*.08;g.visible=age<D.reveal;materials[i].opacity=(.7-i*.17)*(1-ease((age-D.contact)/.35));});
  const fall=ease((age-D.contact)/.28);victim.body.rotation.x=-Math.PI/2*fall;victim.body.position.y=-.64*fall;victim.body.position.z=-.10*fall;victim.leftArm.rotation.z=.65*fall;victim.rightArm.rotation.z=-.65*fall;victim.leftLeg.rotation.z=.13*fall;victim.rightLeg.rotation.z=-.13*fall;
  // Settle even an aerial victim onto the arena floor as the blackout lifts.
  root.position.y=T.MathUtils.lerp(d.pos.y,.88,ease((age-D.contact)/.7));mark.visible=age>=D.symbol&&age<D.reveal;mark.rotation.y=d.yaw+Math.PI;mark.scale.setScalar(2.2+.7*ease((age-D.symbol)/.3));
  root.scale.setScalar(1-ease((age-4.3)/.7));
 },dispose(){root.removeFromParent();geometry.dispose();ownedGeometry.forEach(g=>g.dispose());ownedMaterials.forEach(m=>m.dispose());}};
};
