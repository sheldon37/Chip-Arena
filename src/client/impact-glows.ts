import * as T from 'three';
import type {GameEvent,Snapshot} from '../shared/simulation.js';
import {glowTexture} from './projectile-glow.js';

type Mark={group:T.Group;halo:T.Mesh;ring:T.Mesh;born:number;target:string;local:T.Vector3;rotation:T.Quaternion;color:T.Color;};
/** Surface-aligned light decals: depth-tested, attached to moving cover, short lived. */
export class ImpactGlows {
 readonly marks:Mark[]=[];readonly lights:T.PointLight[]=[];
 private texture=glowTexture();private plane=new T.PlaneGeometry(1,1);private ring=new T.RingGeometry(.34,.37,6);
 private epoch='';
 constructor(private scene:T.Scene){for(let i=0;i<2;i++){const light=new T.PointLight(0xffffff,0,2.7,2);light.name='impact-light';scene.add(light);this.lights.push(light);}}
 event(e:GameEvent,time:number,props:Map<string,T.Group>,quality:number,compatibility=false){
  if(e.type!=='impact'||!e.material||!e.surface||!e.normal||!e.target||quality<=0)return;
  const prop=props.get(e.target);if(!prop)return;
  const normal=new T.Vector3(e.normal.x,e.normal.y,e.normal.z);if(normal.lengthSq()<.5)return;normal.normalize();
  const color=new T.Color(e.color??0x72eaff).lerp(new T.Color(0x95eaff),.25);
  const material=(opacity:number)=>new T.MeshBasicMaterial({color,map:compatibility?null:this.texture,transparent:true,opacity,blending:T.AdditiveBlending,depthWrite:false,depthTest:true,toneMapped:false,side:T.DoubleSide});
  const group=new T.Group();group.name='impact-glow';group.position.set(e.surface.x,e.surface.y,e.surface.z).addScaledVector(normal,.018);group.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),normal);
  const halo=new T.Mesh(this.plane,material(.9));void compatibility;const ring=new T.Mesh(this.ring,material(.95));ring.material.map=null;ring.visible=false;/* owner request 2026-10-04: no impact outline ring; the soft halo is the mark on every path */group.add(halo,ring);this.scene.add(group);
  prop.updateWorldMatrix(true,false);const local=prop.worldToLocal(group.position.clone()),rotation=prop.getWorldQuaternion(new T.Quaternion()).invert().multiply(group.quaternion);
  this.marks.push({group,halo,ring,born:time,target:e.target,local,rotation,color});
 }
 update(state:Pick<Snapshot,'generation'|'props'>,time:number,props:Map<string,T.Group>,quality:number,compatibility=false){
  const epoch=String(state.generation??0);if(this.epoch&&this.epoch!==epoch)this.clear();this.epoch=epoch;
  const ids=new Set(state.props.map(p=>p.id));
  for(let i=this.marks.length-1;i>=0;i--){const m=this.marks[i],age=time-m.born,prop=props.get(m.target);if(age>=.85||age<0||!ids.has(m.target)||!prop||quality<=0){this.remove(i);continue;}
   prop.updateWorldMatrix(true,false);m.group.position.copy(prop.localToWorld(m.local.clone()));m.group.quaternion.copy(prop.getWorldQuaternion(new T.Quaternion())).multiply(m.rotation);
   const fade=(1-age/.85)**2;m.halo.scale.setScalar(.7+age*.7);(m.halo.material as T.MeshBasicMaterial).opacity=fade*.85;m.ring.scale.setScalar(.4+age*1.1);(m.ring.material as T.MeshBasicMaterial).opacity=fade*.95;
  }
  this.lights.forEach((l,i)=>{l.intensity=0;const m=this.marks[this.marks.length-1-i];if(!m||compatibility||quality<.5)return;l.position.copy(m.group.position).add(new T.Vector3(0,0,.14).applyQuaternion(m.group.quaternion));l.color.copy(m.color);l.intensity=2.5*Math.max(0,1-(time-m.born)/.35)**2;});
 }
 private remove(i:number){const [m]=this.marks.splice(i,1);m.group.removeFromParent();(m.halo.material as T.Material).dispose();(m.ring.material as T.Material).dispose();}
 clear(){while(this.marks.length)this.remove(0);this.lights.forEach(l=>l.intensity=0);}
 dispose(){this.clear();this.lights.forEach(l=>l.removeFromParent());this.plane.dispose();this.ring.dispose();this.texture.dispose();}
}
