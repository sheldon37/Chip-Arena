import * as T from 'three';
import type {Snapshot,GameEvent} from '../shared/simulation.js';
import {buildWeapon,enchantWeapon} from './weapon-visuals.js';
interface Impact {at:number;root:T.Group;ring:T.Mesh<T.RingGeometry,T.MeshBasicMaterial>;dust:T.Points<T.BufferGeometry,T.PointsMaterial>;stones:T.Mesh[];}
/** Dedicated meshes retain the equipped weapon's silhouette during flight. */
export class StandardMeleeEffects {
 private shots=new Map<string,T.Group>();private impacts:Impact[]=[];
 constructor(private scene:T.Scene){}
 update(s:Snapshot){
  const shots=s.projectiles.filter(p=>p.melee);for(const[id,g]of this.shots)if(!shots.some(p=>p.id===id)){g.removeFromParent();this.shots.delete(id);}
  for(const p of shots){let group=this.shots.get(p.id);if(!group){group=new T.Group();const weapon=new T.Group();buildWeapon(weapon,p.spec);group.add(weapon);group.name='thrown-'+p.melee!.kind;this.shots.set(p.id,group);this.scene.add(group);}group.position.set(p.pos.x,p.pos.y,p.pos.z);group.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(p.vel.x,p.vel.y,p.vel.z).normalize());const weapon=group.children[0] as T.Group;
   weapon.rotation.set(p.melee!.kind==='shield'?Math.PI/2:0,0,p.melee!.kind==='shield'?s.time*24:0);enchantWeapon(weapon,p.elements,s.time,p.mods||{});
  }
  for(const impact of [...this.impacts]){const age=s.time-impact.at;if(age<0||age>1.1){impact.root.removeFromParent();impact.root.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.Points){o.geometry.dispose();(o.material as T.Material).dispose();}});this.impacts.splice(this.impacts.indexOf(impact),1);continue;}
   impact.ring.scale.setScalar(.2+age*3);impact.ring.material.opacity=Math.max(0,.55-age);impact.dust.material.opacity=Math.max(0,.46-age*.4);impact.dust.scale.setScalar(.4+age*1.6);impact.dust.position.y=age*.35;impact.stones.forEach((stone,i)=>{const a=i*2.39996,speed=1.7+(i%3)*.6;stone.position.set(Math.cos(a)*speed*age,Math.max(0,(1.8+i%3*.45)*age-4*age*age),Math.sin(a)*speed*age);stone.rotation.set(age*(i+2),0,age*3);stone.scale.setScalar(.075*(1-age/1.1));});
  }
 }
 event(e:GameEvent){if(e.type!=='melee-slam')return;const root=new T.Group();root.position.set(e.pos.x,e.pos.y+.03,e.pos.z);root.name='wolf-landing-impact';const ring=new T.Mesh(new T.RingGeometry(.9,1,40),new T.MeshBasicMaterial({color:0xe9d4b2,transparent:true,opacity:.6,side:T.DoubleSide,depthWrite:false}));ring.rotation.x=-Math.PI/2;root.add(ring);
  const points=[];for(let i=0;i<40;i++){const a=i*2.39996,r=.7+i%5*.25;points.push(Math.cos(a)*r,(i%4)*.05,Math.sin(a)*r);}const dust=new T.Points(new T.BufferGeometry().setAttribute('position',new T.Float32BufferAttribute(points,3)),new T.PointsMaterial({color:0xb6a895,size:.3,transparent:true,opacity:.45,depthWrite:false}));root.add(dust);const stones=Array.from({length:10},()=>new T.Mesh(new T.IcosahedronGeometry(1,0),new T.MeshLambertMaterial({color:0x8b8880})));stones.forEach(o=>root.add(o));this.scene.add(root);this.impacts.push({at:e.time,root,ring,dust,stones});
 }
}
