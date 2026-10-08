import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import type {DeathPresentation,PlayerView,Snapshot} from '../shared/simulation.js';
import {createCharacterRig} from './character-rig.js';
import {cinematicVictim,DeathPoseCarry} from './cinematic-victim.js';
import {dressRig} from './cosmetics.js';
import {heavenMark} from './ansatsuken-intro.js';
import {disposeAuditorHalo} from './auditor-halo.js';
import {RAGING_DEMON as D,demonBeat} from '../shared/raging-demon.js';
import {boxPose,ghostMesh,setGhostPose} from './demon-afterimages.js';
import {poseDemonAttack,poseDemonVictim,smooth} from './demon-combo.js';
/** Cosmetic combo poses share the death clock; no extra attacks, damage or server actors. */
export function createRagingDemon(scene:T.Scene,p:PlayerView,d:DeathPresentation,quality:number,budget:number,cinema=false,original?:T.Object3D,state?:Snapshot){
 const root=new T.Group();root.name='raging-demon';scene.add(root);root.position.set(d.pos.x,d.pos.y,d.pos.z);
 const victim=cinematicVictim(p,state);dressRig(victim,p.cosmetics);const carry=new DeathPoseCarry(victim,cinema?undefined:original);root.add(victim.group);victim.group.rotation.y=d.yaw;victim.weapon.visible=victim.offWeapon.visible=false;
 const a=d.attacker,source=createCharacterRig(a?.color||'#ff274c',a?.cosmetics.bodyColor);dressRig(source,a?.cosmetics);
 const start=a?new T.Vector3(a.pos.x-d.pos.x,a.pos.y-d.pos.y,a.pos.z-d.pos.z):new T.Vector3(-4,0,0);if(start.length()<2)start.set(-Math.sin(d.yaw)*3,0,-Math.cos(d.yaw)*3);start.clampLength(0,12);
 const yaw=Math.atan2(-start.x,-start.z),stand=start.clone().setY(0).normalize().multiplyScalar(.72),ghosts:T.InstancedMesh[]=[];victim.group.rotation.y=yaw+Math.PI;
 for(let i=0;i<Math.min(Math.max(0,budget-1),quality<.5?2:4);i++){const g=ghostMesh(cinema?(i===0?0x10000f:0x541045):(i===0?0xc38cff:0x58359d),i===0?.9:.3);g.name='demon-afterimage';g.rotation.y=yaw;root.add(g);ghosts.push(g);}
 const ownedMaterials=new Set<T.Material>(),ownedGeometry=new Set<T.BufferGeometry>();if(cinema){disposeAuditorHalo(victim);const black=new T.MeshBasicMaterial({color:0x09000c});ownedMaterials.add(black);victim.group.traverse(o=>{if(o instanceof T.Mesh)o.material=black;});}
 const mark=heavenMark();mark.name='demon-finish-mark';mark.position.set(stand.x+Math.sin(yaw)*.20,.17,stand.z+Math.cos(yaw)*.20);mark.rotation.y=yaw;mark.scale.setScalar(1.1);root.add(mark);mark.traverse(o=>{if(o instanceof T.Mesh){ownedGeometry.add(o.geometry);ownedMaterials.add(o.material as T.Material);}});
 const sparkGeo=new T.SphereGeometry(1,10,6),sparkMat=new T.MeshBasicMaterial({color:0xffeaf2,transparent:true,opacity:0,depthWrite:false,toneMapped:false}),burst=new T.Mesh(sparkGeo,sparkMat);burst.name='demon-combo-impact';root.add(burst);ownedGeometry.add(sparkGeo);ownedMaterials.add(sparkMat);
 const streaks=new T.Group(),lineGeo=new T.BoxGeometry(1,1,1),lineMat=new T.MeshBasicMaterial({color:0xff376d,transparent:true,opacity:.85,depthWrite:false,toneMapped:false});ownedGeometry.add(lineGeo);ownedMaterials.add(lineMat);streaks.rotation.y=yaw;root.add(streaks);for(let i=0;i<12;i++){const mesh=new T.Mesh(lineGeo,lineMat);streaks.add(mesh);}
 return {count:ghosts.length+2,update(age:number){
  ghosts.forEach((g,i)=>{const t=Math.max(0,age-i*.042);poseDemonAttack(source,t);setGhostPose(g,boxPose(source.group));if(t<D.contact)g.position.copy(start).lerp(stand,smooth(t/D.contact));else g.position.copy(stand).add(new T.Vector3(Math.sin(yaw+Math.PI/2)*i*.035,0,Math.cos(yaw+Math.PI/2)*i*.035));g.visible=age<4.3&&(i===0||age<D.symbol);(g.material as T.MeshBasicMaterial).opacity=(i===0?(cinema?1:.88):.32-i*.045)*(1-smooth((age-4.1)/.4));});
  poseDemonVictim(victim,age);carry.apply(age);root.position.y=T.MathUtils.lerp(d.pos.y,.88,smooth((age-D.contact)/.6));
  const beat=demonBeat(age),impact=beat.index>=0&&beat.age<.18,force=impact?1-beat.age/.18:0;burst.visible=impact;burst.position.set(0,beat.index===2?-.12:beat.index===3?.04:beat.index===5?.5:.25,0);burst.scale.setScalar((beat.index===7?.18:.095)*(.4+force));sparkMat.opacity=force*.85;
  streaks.visible=impact;streaks.position.copy(burst.position);streaks.children.forEach((mesh,i)=>{const angle=i*Math.PI/6+(beat.index%2)*.2,r=.2+(1-force)*.7;mesh.position.set(Math.cos(angle)*r,Math.sin(angle)*r,0);mesh.rotation.set(0,0,angle);mesh.scale.set(.18+(1-force)*.3,.018*force,.02);});lineMat.opacity=force*.8;
  mark.visible=age>=D.symbol&&age<D.end;root.scale.setScalar(1-smooth((age-4.3)/.7));
 },dispose(){root.removeFromParent();for(const g of ghosts)(g.material as T.Material).dispose();disposeAuditorHalo(victim);disposeAuditorHalo(source);for(const rig of [victim,source])rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')ownedGeometry.add(o.geometry);});ownedGeometry.forEach(g=>g.dispose());ownedMaterials.forEach(m=>m.dispose());}};
}
export const ragingDemon:KillEffectFactory=(scene,original,p,d,quality,budget,state)=>createRagingDemon(scene,p,d,quality,budget,false,original,state);
