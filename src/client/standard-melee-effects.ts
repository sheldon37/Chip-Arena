import {DAGGER_THROW} from '../shared/standard-melee.js';
import * as T from 'three';
import type {Snapshot,GameEvent} from '../shared/simulation.js';
import {buildWeapon,enchantWeapon} from './weapon-visuals.js';
import {recordMeleeHit} from './block-impacts.js';
interface Impact {at:number;root:T.Group;ring:T.Mesh<T.RingGeometry,T.MeshBasicMaterial>;dust:T.Points<T.BufferGeometry,T.PointsMaterial>;stones:T.Mesh[];}
/** A landed standard-melee hit's impact flash (presentation only): a flat shock ring and a few sparks thrown from the contact
 * point, sized by the weapon's weight, so a greatsword connects with a heavy thump and a dagger with a small nick. Shared
 * geometry, one material per flash, gone after FLASH_LIFE seconds. */
interface Flash {at:number;weight:number;root:T.Group;ring:T.Mesh;sparks:T.Mesh[];material:T.MeshBasicMaterial;ringMaterial:T.MeshBasicMaterial;}
const HIT_WEIGHT:Partial<Record<string,number>>={greatsword:1,shield:.8,sword:.6,daggers:.35},FLASH_LIFE=.28;
const FLASH_RING=new T.RingGeometry(.55,.7,24),SPARK=new T.BoxGeometry(.035,.035,.32);
/** Dedicated meshes retain the equipped weapon's silhouette during flight. */
export class StandardMeleeEffects {
 private shots=new Map<string,T.Group>();private impacts:Impact[]=[];private flashes:Flash[]=[];private specs=new Map<string,string>();
 constructor(private scene:T.Scene){}
 update(s:Snapshot){
  const shots=s.projectiles.filter(p=>p.melee);for(const[id,g]of this.shots)if(!shots.some(p=>p.id===id)){g.removeFromParent();this.shots.delete(id);}
  for(const p of shots){let group=this.shots.get(p.id);if(!group){group=new T.Group();const spin=new T.Group(),weapon=new T.Group();buildWeapon(weapon,p.spec);weapon.position.z=p.melee!.kind==='dagger'?-.44:0;spin.add(weapon);group.add(spin);group.name='thrown-'+p.melee!.kind;this.shots.set(p.id,group);this.scene.add(group);}group.position.set(p.pos.x,p.pos.y,p.pos.z);group.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(p.vel.x,p.vel.y,p.vel.z).normalize());const spin=group.children[0] as T.Group,weapon=spin.children[0] as T.Group;
   const age=Math.max(0,s.time-(p.melee!.at??0));spin.rotation.set(p.melee!.kind==='shield'?Math.PI/2:age*DAGGER_THROW.spinSpeed,0,p.melee!.kind==='shield'?age*24:0);enchantWeapon(weapon,p.elements,s.time,p.mods||{});
  }
  this.specs.clear();for(const p of s.players)this.specs.set(p.id,p.spec);
  for(const f of [...this.flashes]){const age=s.time-f.at;if(age<0||age>FLASH_LIFE){this.removeFlash(f);continue;}const u=age/FLASH_LIFE,out=1-(1-u)**3;
   f.ring.scale.setScalar((.25+out*1.1)*(.6+.6*f.weight));f.ringMaterial.opacity=.7*(1-u)**2;f.material.opacity=(1-u)**1.5;
   f.sparks.forEach((spark,i)=>{const a=i*2.39996+f.at*7,d=(.15+out*(.6+.7*f.weight))*(.8+.1*(i%3));const x=Math.cos(a)*d,y=Math.sin(a*1.7)*.25*d+.1*out,z=Math.sin(a)*d;spark.position.set(x,y,z);spark.rotation.set(-Math.atan2(y,Math.hypot(x,z)),Math.atan2(x,z),0);spark.scale.set(1,1,.3+1.2*(1-u));});}
  for(const impact of [...this.impacts]){const age=s.time-impact.at;if(age<0||age>1.1){impact.root.removeFromParent();impact.root.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.Points){o.geometry.dispose();(o.material as T.Material).dispose();}});this.impacts.splice(this.impacts.indexOf(impact),1);continue;}
   impact.ring.scale.setScalar(.2+age*3);impact.ring.material.opacity=Math.max(0,.55-age);impact.dust.material.opacity=Math.max(0,.46-age*.4);impact.dust.scale.setScalar(.4+age*1.6);impact.dust.position.y=age*.35;impact.stones.forEach((stone,i)=>{const a=i*2.39996,speed=1.7+(i%3)*.6;stone.position.set(Math.cos(a)*speed*age,Math.max(0,(1.8+i%3*.45)*age-4*age*age),Math.sin(a)*speed*age);stone.rotation.set(age*(i+2),0,age*3);stone.scale.setScalar(.075*(1-age/1.1));});
  }
 }
 event(e:GameEvent){recordMeleeHit(e);if(e.type==='hit')this.flash(e);if(e.type!=='melee-slam')return;const root=new T.Group();root.position.set(e.pos.x,e.pos.y+.03,e.pos.z);root.name='wolf-landing-impact';const ring=new T.Mesh(new T.RingGeometry(.9,1,40),new T.MeshBasicMaterial({color:0xe9d4b2,transparent:true,opacity:.6,side:T.DoubleSide,depthWrite:false}));ring.rotation.x=-Math.PI/2;root.add(ring);
  const points=[];for(let i=0;i<40;i++){const a=i*2.39996,r=.7+i%5*.25;points.push(Math.cos(a)*r,(i%4)*.05,Math.sin(a)*r);}const dust=new T.Points(new T.BufferGeometry().setAttribute('position',new T.Float32BufferAttribute(points,3)),new T.PointsMaterial({color:0xb6a895,size:.3,transparent:true,opacity:.45,depthWrite:false}));root.add(dust);const stones=Array.from({length:10},()=>new T.Mesh(new T.IcosahedronGeometry(1,0),new T.MeshLambertMaterial({color:0x8b8880})));stones.forEach(o=>root.add(o));this.scene.add(root);this.impacts.push({at:e.time,root,ring,dust,stones});
 }
 private flash(e:GameEvent){
  const weight=e.source?HIT_WEIGHT[this.specs.get(e.source)??'']:undefined;if(weight===undefined||e.message==='burn'||e.message==='poison')return;
  if(this.flashes.length>=8)this.removeFlash(this.flashes[0]);
  const root=new T.Group();root.name='melee-hit-flash';root.position.set(e.pos.x,e.pos.y,e.pos.z);
  const ringMaterial=new T.MeshBasicMaterial({color:0xfff1d6,transparent:true,opacity:.7,side:T.DoubleSide,depthWrite:false}),material=new T.MeshBasicMaterial({color:0xffd9a0,transparent:true,depthWrite:false});
  const ring=new T.Mesh(FLASH_RING,ringMaterial);ring.rotation.set(Math.PI/2*.6,e.time*3%Math.PI,0);root.add(ring);
  const sparks=Array.from({length:3+Math.round(4*weight)},()=>{const m=new T.Mesh(SPARK,material);root.add(m);return m;});
  this.scene.add(root);this.flashes.push({at:e.time,weight,root,ring,sparks,material,ringMaterial});
 }
 private removeFlash(f:Flash){f.root.removeFromParent();f.material.dispose();f.ringMaterial.dispose();this.flashes.splice(this.flashes.indexOf(f),1);}
}
