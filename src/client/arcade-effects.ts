import * as T from 'three';
import type {GameEvent,Snapshot} from '../shared/simulation.js';
const starShape=new T.Shape();for(let i=0;i<24;i++){const a=i*Math.PI/12,r=i%2?.22:(i%6===0?1:.55+(i%5)*.07),x=Math.cos(a)*r,y=Math.sin(a)*r;i?starShape.lineTo(x,y):starShape.moveTo(x,y);}starShape.closePath();const star=new T.ShapeGeometry(starShape),crescent=new T.TorusGeometry(1,.024,3,36,Math.PI*1.55);
const cube=new T.BoxGeometry(1,1,1),ring=new T.TorusGeometry(1,.035,3,24),sphere=new T.SphereGeometry(1,12,8);
/** A fixed impact pool and bounded projectile shells. No full-screen flashes or simulation hit-stop. */
export class ArcadeEffects {
 private hits:{mesh:T.Mesh;at:number;angle:number;pos:T.Vector3;big:boolean;style:string;part:number}[]=[];private index=0;private shots=new Map<string,T.Group>();private styles=new Map<number,T.MeshBasicMaterial>();
 constructor(private scene:T.Scene){for(let i=0;i<96;i++){const m=new T.Mesh(cube,new T.MeshBasicMaterial({color:0xffdca4,transparent:true,opacity:0,depthWrite:false,toneMapped:false}));m.visible=false;scene.add(m);this.hits.push({mesh:m,at:-10,angle:0,pos:new T.Vector3(),big:false,style:'punch',part:0});}}
 mat(color:number){let m=this.styles.get(color);if(!m){m=new T.MeshBasicMaterial({color,transparent:true,opacity:.45,blending:T.AdditiveBlending,depthWrite:false,toneMapped:false});this.styles.set(color,m);}return m;}
 event(e:GameEvent,time:number,quality:number){if(!['arcade-hit','arcade-burst','arcade-echo','arcade-parry','arcade-land'].includes(e.type))return;const count=quality<.5?4:8;for(let i=0;i<count;i++){const h=this.hits[this.index++%this.hits.length];h.at=time;h.pos.set(e.pos.x,e.pos.y+.1,e.pos.z);h.angle=i*Math.PI*2/count+(e.id%7)*.23;h.style=e.type==='arcade-parry'?'parry':e.impact||'punch';h.big=e.message==='FINISH'||e.type==='arcade-burst';h.part=i;h.mesh.geometry=i<2?star:i<4?crescent:cube;const m=h.mesh.material as T.MeshBasicMaterial;m.color.set(i===0?0xe7fbff:i===1?0x168cff:0x80dfff);m.side=T.DoubleSide;h.mesh.renderOrder=i===0?3:2;}}
 update(state:Snapshot,time:number,camera:T.Camera,quality:number){for(const h of this.hits){const age=time-h.at;h.mesh.visible=age>=0&&age<.34;if(!h.mesh.visible)continue;const u=age/.34,size=(h.big?1.35:.8),m=h.mesh.material as T.MeshBasicMaterial;h.mesh.quaternion.copy(camera.quaternion);h.mesh.position.copy(h.pos);h.mesh.rotateZ(h.angle);
   if(h.part<2){const r=size*(h.part===0?.6:1)*(.95+u*.2)*(1-u*.65);h.mesh.scale.set(r,r,1);m.opacity=Math.max(0,1-u*(h.part===0?2.2:1.6));}
   else if(h.part<4){const r=size*(.25+u*.9)*(h.part===3?.78:1);h.mesh.scale.setScalar(r);h.mesh.rotateZ(u*2.6*(h.part===2?1:-1));m.opacity=Math.sin(Math.min(1,u*5)*Math.PI/2)*(1-u);}
   else{const r=size*(.15+u*.9);h.mesh.position.add(new T.Vector3(Math.cos(h.angle)*r,Math.sin(h.angle)*r,0).applyQuaternion(camera.quaternion));h.mesh.scale.set(size*(.25+u*.2),.025*(1-u),.02);m.opacity=1-u;}
  }
  const ids=new Set(state.projectiles.filter(p=>p.arcade).map(p=>p.id));for(const[id,g]of this.shots)if(!ids.has(id)){g.removeFromParent();this.shots.delete(id);}
  for(const p of state.projectiles){if(!p.arcade)continue;let g=this.shots.get(p.id);if(!g){g=new T.Group();const color=p.arcade.red?0xff542b:0x3fc4ff;for(let i=0;i<3;i++){const shell=new T.Mesh(i===0?sphere:ring,this.mat(color));shell.userData.i=i;g.add(shell);}this.shots.set(p.id,g);this.scene.add(g);}g.position.set(p.pos.x,p.pos.y,p.pos.z);g.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(p.vel.x,p.vel.y,p.vel.z).normalize());g.children.forEach((o,i)=>{o.visible=quality>=.5||i<2;const pulse=1+Math.sin(time*18+i)*.08;o.scale.setScalar(p.radius*(i===0?1.8:2.3)*pulse);if(i===0){o.scale.z*=2.1;o.position.z=-p.radius*.8;}else{o.position.z=-p.radius*(i*1.9);o.rotation.z=time*6*(i%2?1:-1);}});}
 }
 clear(){for(const h of this.hits){h.at=-10;h.mesh.visible=false;}for(const g of this.shots.values())g.removeFromParent();this.shots.clear();}
}
