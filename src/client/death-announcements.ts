import * as T from 'three';
import type {Snapshot,PlayerView} from '../shared/simulation.js';
import {createCharacterRig,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {disposeAuditorHalo} from './auditor-halo.js';
export const DEATH_ANNOUNCEMENT={launch:1.05,duration:5.6,spacing:1.5};
export interface Announcement {id:string;at:number;slot:number;player:PlayerView;text:string;}
/** Persistent death records survive dropped event packets. Simultaneous deaths get separate launches. */
export class DeathAnnouncementTrack {
 items:Announcement[]=[];private seen=new Map<string,number>();private lastTime=-1;private nextLaunch=-1;private nextSlot=0;
 clear(){this.items=[];this.seen.clear();this.lastTime=-1;this.nextLaunch=-1;this.nextSlot=0;}
 update(s:Snapshot){
  if(s.time<this.lastTime||['lobby','intro','specialization'].includes(s.phase))this.clear();this.lastTime=s.time;
  for(const p of [...s.players].sort((a,b)=>(a.death?.at||0)-(b.death?.at||0)||String(a.death?.id).localeCompare(String(b.death?.id)))){
   const d=p.death;if(p.hp>0||!d||this.seen.has(d.id)||!['fight','result','match','countdown','draft'].includes(s.phase))continue;
   this.seen.set(d.id,d.at);if(s.time-d.at>2||s.time<d.at)continue;
   const at=Math.max(d.at,this.nextLaunch),name=d.announcement?.victim||p.name,killer=d.announcement?.killer??null;
   this.items.push({id:d.id,at,slot:this.nextSlot++%3,player:{...p,cosmetics:p.cosmetics?{...p.cosmetics}:undefined},text:killer?`${name} was killed by ${killer}.`:`${name} made an oopsie.`});this.nextLaunch=at+DEATH_ANNOUNCEMENT.spacing;
  }
  this.items=this.items.filter(a=>s.time-a.at<DEATH_ANNOUNCEMENT.duration);for(const[id,at]of this.seen)if(s.time-at>30)this.seen.delete(id);
  return this.items;
 }
}
/** Hologram reveal: opacity eases in; the vertical unfold overshoots about 6% and settles (easeOutBack). */
const revealEase=(x:number)=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);},revealBack=(x:number)=>{x=Math.min(1,Math.max(0,x))-1;return 1+2.3*x*x*x+1.3*x*x;};
/** Fixed world-space broadcast above the northern skyline: never follows the viewer or scope. */
export function skyBroadcastFrame(slot=0){
 return {position:new T.Vector3([0,-43,43][slot%3],72,-115),quaternion:new T.Quaternion(),scale:5.5};
}

interface Stage {root:T.Group;rig:CharacterRig;rocket:T.Mesh;trail:T.Line;stars:T.Points<T.BufferGeometry,T.PointsMaterial>;holo:T.Group;material:T.MeshBasicMaterial;lines:T.LineSegments;frame:T.LineSegments;directions:T.Vector3[];nameplate?:T.Mesh<T.PlaneGeometry,T.MeshBasicMaterial>;}
export class DeathAnnouncements {
 readonly track=new DeathAnnouncementTrack();private stages=new Map<string,Stage>();private label?:HTMLDivElement;
 constructor(private scene:T.Scene){if(typeof document!=='undefined'&&document.createElement){this.label=document.createElement('div');this.label.className='death-announcement';this.label.setAttribute('role','status');this.label.setAttribute('aria-live','polite');this.label.hidden=true;document.body.append(this.label);}}
 private create(a:Announcement):Stage{
  const root=new T.Group();root.name='death-firework-'+a.id;
  const material=new T.MeshBasicMaterial({color:0x62dcff,transparent:true,opacity:.6,depthTest:true,depthWrite:false,toneMapped:false,fog:false,side:T.DoubleSide});
  const lineMaterial=new T.LineBasicMaterial({color:0x8eedff,transparent:true,opacity:.6,depthTest:true,depthWrite:false,toneMapped:false,fog:false});
  const rocket=new T.Mesh(new T.SphereGeometry(.27,8,5),new T.MeshBasicMaterial({color:0xf0fbff,depthTest:true,depthWrite:false,toneMapped:false,fog:false}));root.add(rocket);
  const trail=new T.Line(new T.BufferGeometry().setFromPoints([new T.Vector3(),new T.Vector3()]),lineMaterial.clone());root.add(trail);
  const dirs=Array.from({length:72},(_,i)=>{const y=1-2*(i+.5)/72,phi=i*2.399963229728653;return new T.Vector3(Math.cos(phi)*Math.sqrt(1-y*y),y,Math.sin(phi)*Math.sqrt(1-y*y)).multiplyScalar(2+(i%7)*.22);});
  const stars=new T.Points(new T.BufferGeometry().setAttribute('position',new T.Float32BufferAttribute(new Float32Array(dirs.length*3),3)),new T.PointsMaterial({color:0xa6f4ff,size:.10,transparent:true,opacity:1,depthTest:true,depthWrite:false,toneMapped:false,fog:false}));root.add(stars);
  const holo=new T.Group(),rig=createCharacterRig(a.player.color,a.player.cosmetics?.bodyColor);holo.name='sky-portrait';dressRig(rig,a.player.cosmetics);rig.weapon.visible=rig.offWeapon.visible=false;
  // Crop to the player's actual head, hat and shoulders; blank mannequin faces stay blank.
  rig.leftLeg.visible=rig.rightLeg.visible=rig.leftArm.visible=rig.rightArm.visible=false;
  for(const name of ['hip-shell','waist-band','abdomen','abdomen-shell']){const part=rig.group.getObjectByName(name);if(part)part.visible=false;}
  rig.group.updateMatrixWorld(true);const bounds=new T.Box3();rig.group.traverseVisible(o=>{if(o instanceof T.Mesh){o.geometry.computeBoundingBox();bounds.union(o.geometry.boundingBox!.clone().applyMatrix4(o.matrixWorld));}});
  const size=bounds.getSize(new T.Vector3()),center=bounds.getCenter(new T.Vector3()),fit=Math.min(5.1/Math.max(.01,size.x),6.4/Math.max(.01,size.y));
  const edges:T.LineSegments[]=[];rig.group.traverse(o=>{if(o instanceof T.Mesh){o.geometry=o.geometry.clone();o.material=material;o.castShadow=false;o.receiveShadow=false;const edge=new T.LineSegments(new T.EdgesGeometry(o.geometry,24),lineMaterial);edges.push(edge);edge.userData.mesh=o;}});
  for(const edge of edges){(edge.userData.mesh as T.Mesh).add(edge);delete edge.userData.mesh;}
  rig.group.scale.setScalar(fit);rig.group.position.copy(center).multiplyScalar(-fit);rig.group.position.y+=.48;holo.add(rig.group);
  const scan:T.Vector3[]=[];for(let y=-2.85;y<=3.85;y+=.22)scan.push(new T.Vector3(-2.8,y,1.4),new T.Vector3(2.8,y,1.4));
  const lines=new T.LineSegments(new T.BufferGeometry().setFromPoints(scan),lineMaterial.clone());holo.add(lines);
  const brackets:T.Vector3[]=[];for(const x of [-1,1])for(const y of [-1,1]){const corner=new T.Vector3(x*3,y*3.65+.25,1.45);brackets.push(corner.clone().add(new T.Vector3(-x*.65,0,0)),corner,corner.clone(),corner.clone().add(new T.Vector3(0,-y*.9,0)));}
  for(const x of [-1,1])for(let i=0;i<9;i++)brackets.push(new T.Vector3(x*3.12,-2.8+i*.75,1.45),new T.Vector3(x*3.3,-2.8+i*.75,1.45));
  brackets.push(new T.Vector3(-2.5,-3.55,1.45),new T.Vector3(2.5,-3.55,1.45));
  const frame=new T.LineSegments(new T.BufferGeometry().setFromPoints(brackets),lineMaterial.clone());frame.name='portrait-frame';holo.add(frame);
  let nameplate:Stage['nameplate'];if(typeof document!=='undefined'&&document.createElement){const canvas=document.createElement('canvas');canvas.width=1024;canvas.height=224;const ctx=canvas.getContext('2d');if(ctx){ctx.clearRect(0,0,1024,224);ctx.fillStyle='#b8f4ff';ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='500 52px system-ui';const name=(a.player.death?.announcement?.victim||a.player.name).toUpperCase(),killer=a.player.death?.announcement?.killer;ctx.fillText(name,512,48,940);ctx.font='400 36px system-ui';ctx.fillText(killer?'was killed by':'made an oopsie.',512,118,940);if(killer){ctx.font='500 44px system-ui';ctx.fillText(killer,512,178,940);}const texture=new T.CanvasTexture(canvas);texture.colorSpace=T.SRGBColorSpace;nameplate=new T.Mesh(new T.PlaneGeometry(6.2,1.356),new T.MeshBasicMaterial({map:texture,transparent:true,depthTest:true,depthWrite:false,toneMapped:false,fog:false}));nameplate.name='portrait-name';nameplate.position.set(0,-4.26,1.5);holo.add(nameplate);}}
  root.add(holo);root.traverse(o=>{o.renderOrder=3;});lines.renderOrder=4;frame.renderOrder=5;if(nameplate)nameplate.renderOrder=6;this.scene.add(root);
  const stage={root,rig,rocket,trail,stars,holo,material,lines,frame,directions:dirs,nameplate};this.stages.set(a.id,stage);return stage;
 }
 update(s:Snapshot,_camera:T.Camera,visible:boolean){
  if(!visible){this.clear();return;}
  const active=this.track.update(s);for(const[id,stage]of this.stages)if(!active.some(a=>a.id===id)){this.disposeStage(stage);this.stages.delete(id);}
  const showing=active.filter(a=>s.time>=a.at).slice(-3),texts:string[]=[];
  for(const stage of this.stages.values())stage.root.visible=false;
  for(const a of showing){const age=s.time-a.at,stage=this.stages.get(a.id)||this.create(a),layout=skyBroadcastFrame(a.slot),u=Math.min(1,age/DEATH_ANNOUNCEMENT.launch),burst=age-DEATH_ANNOUNCEMENT.launch,fade=Math.min(1,Math.max(0,(DEATH_ANNOUNCEMENT.duration-age)/.8));
   stage.root.visible=true;stage.root.position.copy(layout.position);stage.root.quaternion.copy(layout.quaternion);stage.root.scale.setScalar(layout.scale);
   stage.rocket.visible=stage.trail.visible=burst<0;const y=-25*(1-u)**2;stage.rocket.position.set(Math.sin(u*8)*.12,y,0);const line=stage.trail.geometry.getAttribute('position') as T.BufferAttribute;line.setXYZ(0,stage.rocket.position.x,y,0);line.setXYZ(1,0,y-6*(1-u*.5),0);line.needsUpdate=true;
   stage.stars.material.size=.18*layout.scale;stage.stars.visible=burst>=0&&burst<2.2;if(stage.stars.visible){const positions=stage.stars.geometry.getAttribute('position') as T.BufferAttribute;stage.directions.forEach((d,i)=>positions.setXYZ(i,d.x*burst,d.y*burst-burst*burst*.4,d.z*burst));positions.needsUpdate=true;stage.stars.material.opacity=Math.max(0,1-burst/2.2);}
   stage.holo.visible=burst>=0;if(stage.holo.visible){const show=revealEase(burst/.26),flicker=.96+.04*Math.sin(age*29);stage.material.opacity=.55*fade*show*flicker;stage.holo.scale.set(1,Math.max(.01,revealBack(burst/.32)),1);stage.rig.head.rotation.y=Math.sin(age*.6)*.045;stage.rig.head.rotation.x=Math.sin(age*.85)*.018;stage.lines.position.y=Math.sin(age*1.7)*.09;(stage.lines.material as T.LineBasicMaterial).opacity=.12*fade*show;(stage.frame.material as T.LineBasicMaterial).opacity=.7*fade*show;if(stage.nameplate)stage.nameplate.material.opacity=fade*show;
    stage.rig.group.traverse(o=>{if(o instanceof T.LineSegments)(o.material as T.LineBasicMaterial).opacity=.36*fade*show;});
    if(age<DEATH_ANNOUNCEMENT.duration-.45)texts.push(a.text);
   }
  }
  if(this.label){const text=texts.join('\n');if(this.label.textContent!==text){this.label.textContent=text;
   // A new line restarts the entrance animation (styled by the .death-announcement.is-new rule).
   this.label.classList.remove('is-new');if(text){void this.label.offsetWidth;this.label.classList.add('is-new');}}this.label.hidden=!text;}
 }
 private disposeStage(s:Stage){disposeAuditorHalo(s.rig);s.root.removeFromParent();const materials=new Set<T.Material>();s.root.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.Line||o instanceof T.Points){o.geometry.dispose();for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);}});materials.forEach(m=>{if(m instanceof T.MeshBasicMaterial)m.map?.dispose();m.dispose();});}
 clear(){this.track.clear();for(const stage of this.stages.values())this.disposeStage(stage);this.stages.clear();if(this.label)this.label.hidden=true;}
 dispose(){this.clear();this.label?.remove();}
}
