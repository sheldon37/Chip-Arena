import * as T from 'three';
import type {Snapshot,PlayerView} from '../shared/simulation.js';
import {createCharacterRig,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {animateCharacter} from './character-animation.js';
import {disposeAuditorHalo} from './auditor-halo.js';
export const DEATH_ANNOUNCEMENT={launch:1.05,duration:5.6,spacing:1.5};
export interface Announcement {id:string;at:number;player:PlayerView;text:string;}
/** Persistent death records survive dropped event packets. Simultaneous deaths get separate launches. */
export class DeathAnnouncementTrack {
 items:Announcement[]=[];private seen=new Map<string,number>();private lastTime=-1;private nextLaunch=-1;
 clear(){this.items=[];this.seen.clear();this.lastTime=-1;this.nextLaunch=-1;}
 update(s:Snapshot){
  if(s.time<this.lastTime||['lobby','intro','specialization'].includes(s.phase))this.clear();this.lastTime=s.time;
  for(const p of [...s.players].sort((a,b)=>(a.death?.at||0)-(b.death?.at||0)||String(a.death?.id).localeCompare(String(b.death?.id)))){
   const d=p.death;if(p.hp>0||!d||this.seen.has(d.id)||!['fight','result','match','countdown','draft'].includes(s.phase))continue;
   this.seen.set(d.id,d.at);if(s.time-d.at>2||s.time<d.at)continue;
   const at=Math.max(d.at,this.nextLaunch),name=d.announcement?.victim||p.name,killer=d.announcement?.killer??null;
   this.items.push({id:d.id,at,player:{...p,cosmetics:p.cosmetics?{...p.cosmetics}:undefined},text:killer?`${name} was killed by ${killer}.`:`${name} made an oopsie.`});this.nextLaunch=at+DEATH_ANNOUNCEMENT.spacing;
  }
  this.items=this.items.filter(a=>s.time-a.at<DEATH_ANNOUNCEMENT.duration);for(const[id,at]of this.seen)if(s.time-at>30)this.seen.delete(id);
  return this.items;
 }
}
interface Stage {root:T.Group;rig:CharacterRig;rocket:T.Mesh;trail:T.Line;stars:T.Points<T.BufferGeometry,T.PointsMaterial>;holo:T.Group;material:T.MeshBasicMaterial;lines:T.LineSegments;ring:T.Mesh;directions:T.Vector3[];}
export class DeathAnnouncements {
 readonly track=new DeathAnnouncementTrack();private stages=new Map<string,Stage>();private label?:HTMLDivElement;
 constructor(private scene:T.Scene){if(typeof document!=='undefined'&&document.createElement){this.label=document.createElement('div');this.label.className='death-announcement';this.label.setAttribute('role','status');this.label.setAttribute('aria-live','polite');this.label.hidden=true;document.body.append(this.label);}}
 private create(a:Announcement,camera:T.Camera):Stage{
  const root=new T.Group();root.name='death-firework-'+a.id;
  const forward=new T.Vector3();camera.getWorldDirection(forward);forward.y=0;if(forward.lengthSq()<.01)forward.set(0,0,-1);forward.normalize();
  const side=new T.Vector3(-forward.z,0,forward.x).multiplyScalar((a.player.death!.seed%3-1)*9);root.position.copy(camera.position).addScaledVector(forward,72).add(side);root.position.y=0;
  const material=new T.MeshBasicMaterial({color:0x91eaff,transparent:true,opacity:.75,depthWrite:false,toneMapped:false,fog:false,side:T.DoubleSide});
  const rocket=new T.Mesh(new T.SphereGeometry(.16,8,5),new T.MeshBasicMaterial({color:0xf0fbff,toneMapped:false,fog:false}));root.add(rocket);
  const trail=new T.Line(new T.BufferGeometry().setFromPoints([new T.Vector3(),new T.Vector3()]),new T.LineBasicMaterial({color:0x78dbff,transparent:true,opacity:.8,depthWrite:false,fog:false}));root.add(trail);
  const dirs=Array.from({length:72},(_,i)=>{const y=1-2*(i+.5)/72,phi=i*2.399963229728653;return new T.Vector3(Math.cos(phi)*Math.sqrt(1-y*y),y,Math.sin(phi)*Math.sqrt(1-y*y)).multiplyScalar(4+(i%7)*.55);});
  const stars=new T.Points(new T.BufferGeometry().setAttribute('position',new T.Float32BufferAttribute(new Float32Array(dirs.length*3),3)),new T.PointsMaterial({color:a.player.color,size:.20,transparent:true,opacity:1,depthWrite:false,toneMapped:false,fog:false}));root.add(stars);
  const holo=new T.Group(),rig=createCharacterRig(a.player.color,a.player.cosmetics?.bodyColor);dressRig(rig,a.player.cosmetics);rig.weapon.visible=rig.offWeapon.visible=false;
  rig.group.traverse(o=>{if(o instanceof T.Mesh){o.geometry=o.geometry.clone();o.material=material;o.castShadow=false;o.receiveShadow=false;}});rig.group.scale.setScalar(6.2);holo.add(rig.group);
  const scan:T.Vector3[]=[];for(let y=-5.5;y<=6.4;y+=.42)scan.push(new T.Vector3(-2.3,y,0),new T.Vector3(2.3,y,0));
  const lines=new T.LineSegments(new T.BufferGeometry().setFromPoints(scan),new T.LineBasicMaterial({color:0xc3f6ff,transparent:true,opacity:.23,depthWrite:false,fog:false}));holo.add(lines);
  const ring=new T.Mesh(new T.RingGeometry(3.1,3.22,48),material);ring.rotation.x=-Math.PI/2;ring.position.y=-5.4;holo.add(ring);holo.position.y=23;root.add(holo);this.scene.add(root);
  const stage={root,rig,rocket,trail,stars,holo,material,lines,ring,directions:dirs};this.stages.set(a.id,stage);return stage;
 }
 update(s:Snapshot,camera:T.Camera,visible:boolean){
  if(!visible){this.clear();return;}
  const active=this.track.update(s);for(const[id,stage]of this.stages)if(!active.some(a=>a.id===id)){this.disposeStage(stage);this.stages.delete(id);}
  let text='';for(const a of active){const age=s.time-a.at;if(age<0)continue;const stage=this.stages.get(a.id)||this.create(a,camera),u=Math.min(1,age/DEATH_ANNOUNCEMENT.launch),burst=age-DEATH_ANNOUNCEMENT.launch,fade=Math.min(1,Math.max(0,(DEATH_ANNOUNCEMENT.duration-age)/.8));
   stage.rocket.visible=stage.trail.visible=burst<0;const y=-14+37*(1-(1-u)**2);stage.rocket.position.set(Math.sin(u*8)*.25,y,0);const line=stage.trail.geometry.getAttribute('position') as T.BufferAttribute;line.setXYZ(0,stage.rocket.position.x,y,0);line.setXYZ(1,0,y-5*(1-u*.5),0);line.needsUpdate=true;
   stage.stars.visible=burst>=0&&burst<2.2;if(stage.stars.visible){const positions=stage.stars.geometry.getAttribute('position') as T.BufferAttribute;stage.directions.forEach((d,i)=>positions.setXYZ(i,d.x*burst,23+d.y*burst-burst*burst*1.8,d.z*burst));positions.needsUpdate=true;stage.stars.material.opacity=Math.max(0,1-burst/2.2);}
   stage.holo.visible=burst>=0;if(stage.holo.visible){const show=Math.min(1,burst/.22);stage.material.opacity=.63*fade*show*(.96+.04*Math.sin(age*29));stage.holo.scale.set(1,Math.max(.01,show),1);stage.holo.rotation.y=Math.atan2(camera.position.x-stage.root.position.x,camera.position.z-stage.root.position.z);stage.lines.position.z=.8;stage.lines.position.y=Math.sin(age*1.7)*.13;(stage.lines.material as T.LineBasicMaterial).opacity=.20*fade;animateCharacter(stage.rig,{...a.player,hp:100,vel:{x:0,y:0,z:0},grounded:true,death:undefined,meleeAction:undefined,emperor:undefined,arcade:undefined,juggle:undefined,knockdown:undefined,hitStunUntil:0,lastAttack:-100,lastSecondary:-100,lastShot:-100,blockTime:0,guarding:false,castAt:undefined},s.time,1/60,0);stage.rig.weapon.visible=stage.rig.offWeapon.visible=false;stage.rig.leftArm.rotation.set(-.12,0,.10);stage.rig.rightArm.rotation.set(-.12,0,-.10);if(age<DEATH_ANNOUNCEMENT.duration-.45)text=a.text;}
  }
  if(this.label){if(this.label.textContent!==text)this.label.textContent=text;this.label.hidden=!text;}
 }
 private disposeStage(s:Stage){disposeAuditorHalo(s.rig);s.root.removeFromParent();const materials=new Set<T.Material>();s.root.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.Line||o instanceof T.Points){o.geometry.dispose();for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);}});materials.forEach(m=>m.dispose());}
 clear(){this.track.clear();for(const stage of this.stages.values())this.disposeStage(stage);this.stages.clear();if(this.label)this.label.hidden=true;}
 dispose(){this.clear();this.label?.remove();}
}
