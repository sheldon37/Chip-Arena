import * as T from 'three';
import type RAPIER from '@dimforge/rapier3d-compat';
import type {RagdollStyle} from '../shared/ragdoll-styles.js';
export type RagdollPart={body:RAPIER.RigidBody;mesh:T.Group};
const colors:Record<string,number>={'ragdoll-ember':0xff762d,'ragdoll-frost':0x9feaff,'ragdoll-static':0xffe667,'ragdoll-gravity':0xa47eff,'ragdoll-pinball':0x70ffc9,'ragdoll-fireworks':0xffa1e2,'ragdoll-soul':0x95dfed,'ragdoll-petals':0xff98c4,'dev-singularity':0xd7b4ff};
/** Fixed-step cosmetic forces preserve observer/late-snapshot consistency. */
export function ragdollForce(style:RagdollStyle,parts:RagdollPart[],time:number,origin:T.Vector3){
 for(let i=0;i<parts.length;i++){const {body}=parts[i],pos=body.translation(),velocity=body.linvel();
  if(style==='ragdoll-frost'&&time>=1.1){body.setGravityScale(0,true);body.setLinvel({x:0,y:0,z:0},true);body.setAngvel({x:0,y:0,z:0},true);}
  if(style==='ragdoll-static'&&[.35,.7,1.05].some(t=>time>=t&&time<t+1/60)){body.setLinvel({x:velocity.x+(i%2?2:-2),y:3.5,z:velocity.z},true);body.setAngvel({x:4*(i%2?1:-1),y:6,z:2},true);}
  if(style==='ragdoll-pinball'&&[.8,1.65].some(t=>time>=t&&time<t+1/60))body.setLinvel({x:(i%2?1:-1)*1.4,y:7,z:velocity.z*.5},true);
  if(style==='ragdoll-fireworks'&&time>=.45&&time<.45+1/60)body.setLinvel({x:velocity.x*.25,y:10,z:velocity.z*.25},true);
  if(style==='ragdoll-soul'){body.setGravityScale(.13,true);if(time>.8)body.setLinvel({x:velocity.x*.985,y:Math.min(2,velocity.y+.08),z:velocity.z*.985},true);}
  if(style==='ragdoll-gravity'&&time>.3){body.setGravityScale(0,true);const dx=origin.x-pos.x,dz=origin.z-pos.z;body.setLinvel({x:dx*2.8-dz*2,y:(origin.y+1.2-pos.y)*3,z:dz*2.8+dx*2},true);}
  if(style==='dev-singularity'&&time>.22){body.setGravityScale(0,true);const dx=origin.x-pos.x,dz=origin.z-pos.z;body.setLinvel({x:dx*3-dz*2.6,y:(origin.y+2.4-pos.y)*3,z:dz*3+dx*2.6},true);body.setAngvel({x:.8,y:3,z:.6},true);}
 }
}
export class RagdollVfx{
 readonly root=new T.Group();readonly count:number;private points:T.Points<T.BufferGeometry,T.PointsMaterial>;private rings:T.Mesh<T.TorusGeometry,T.MeshBasicMaterial>[]=[];private total:number;private core?:T.Mesh<T.SphereGeometry,T.MeshBasicMaterial>;
 constructor(private style:RagdollStyle,private seed:number,budget:number,quality:number){this.root.name=style+'-fx';this.total=Math.max(0,Math.min(quality<.5?6:16,budget-(style==='dev-singularity'?4:3)));this.count=this.total+Math.min(style==='dev-singularity'?4:3,Math.max(0,budget-this.total));const positions=new Float32Array(this.total*3),tints=new Float32Array(this.total*3);const color=new T.Color(colors[style]);for(let i=0;i<this.total;i++){const c=['ragdoll-fireworks','ragdoll-pinball'].includes(style)?new T.Color().setHSL(i/Math.max(1,this.total),.9,.68):color;tints.set(c.toArray(),i*3);}const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.BufferAttribute(positions,3));geometry.setAttribute('color',new T.BufferAttribute(tints,3));this.points=new T.Points(geometry,new T.PointsMaterial({color:0xffffff,vertexColors:true,size:style==='ragdoll-petals'?.10:.055,transparent:true,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false}));this.points.frustumCulled=false;this.root.add(this.points);if(style==='dev-singularity'&&budget>=4){this.core=new T.Mesh(new T.SphereGeometry(1,20,14),new T.MeshBasicMaterial({color:0x080011,toneMapped:false}));this.root.add(this.core);}
  for(let i=0;i<this.count-this.total-(this.core?1:0);i++){const ring=new T.Mesh(new T.TorusGeometry(1,i===0?.025:.01,4,48),new T.MeshBasicMaterial({color:i===0?0xffffff:colors[style],transparent:true,opacity:0,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false}));this.root.add(ring);this.rings.push(ring);}
 }
 update(age:number,center:T.Vector3,parts:RagdollPart[]){const kind=this.style;let burst=kind==='ragdoll-frost'?1.7:kind==='ragdoll-fireworks'?1.6:kind==='dev-singularity'?2.4:kind==='ragdoll-gravity'?1.8:.6;
  const t=Math.max(0,age-burst),finish=['ragdoll-frost','ragdoll-fireworks','dev-singularity','ragdoll-gravity'].includes(kind),collapse=finish?Math.max(0,1-t*5):1;
  for(const {mesh}of parts){if(finish)mesh.scale.multiplyScalar(collapse);if(kind==='ragdoll-soul'||kind==='ragdoll-frost')mesh.traverse(o=>{if(o instanceof T.Mesh){const m=o.material as T.MeshLambertMaterial;if(kind==='ragdoll-soul'){m.opacity=Math.max(0,.72-age*.12);m.color.set(0x91dcec);}else if(age>1.1){m.color.set(0xbdefff);m.emissive?.set(0x244655);}}});}
  const attr=this.points.geometry.getAttribute('position') as T.BufferAttribute;for(let i=0;i<this.total;i++){const angle=i*2.39996+this.seed*.001,rad=.45+(i%5)*.08,phase=(age*(kind==='ragdoll-ember'?1.2:.7)+i/this.total)%1;let x=0,y=0,z=0;
   if(kind==='ragdoll-ember'){x=Math.cos(angle)*rad*phase;y=phase*2;z=Math.sin(angle)*rad*phase;}
   else if(kind==='ragdoll-soul'){x=Math.cos(angle+age*2)*rad;y=phase*2.5;z=Math.sin(angle+age*2)*rad;}
   else if(kind==='ragdoll-gravity'||kind==='dev-singularity'){const radius=age<burst?(kind==='dev-singularity'?1.7:1.1)*(1-phase):t*5;x=Math.cos(angle-age*7)*radius;z=Math.sin(angle-age*7)*radius;y=Math.sin(angle*2)*radius*.6;}
   else if(kind==='ragdoll-static'){const part=parts[i%parts.length];if(part){const p=part.mesh.position;x=p.x-center.x+Math.sin(age*90+i)*.12;y=p.y-center.y;z=p.z-center.z+Math.cos(age*80+i)*.12;}}
   else{const travel=finish?t:phase;x=Math.cos(angle)*travel*(kind==='ragdoll-fireworks'?4:1.7);z=Math.sin(angle)*travel*(kind==='ragdoll-fireworks'?4:1.7);y=(kind==='ragdoll-petals'?Math.sin(angle+age*2)*.15:travel*(1+(i%4)*.4)-travel*travel*2);}
   attr.setXYZ(i,center.x+x,center.y+y,center.z+z);
  }attr.needsUpdate=true;this.points.material.opacity=finish?age<burst?.45:Math.max(0,1-t*.7):Math.min(1,(5-age)/.7)*.85;
  if(this.core){this.core.position.copy(center);this.core.scale.setScalar(age<burst?Math.min(.65,Math.max(0,age-.5)*.45):Math.max(0,.65-t*3));}
  this.rings.forEach((ring,i)=>{ring.position.copy(center);let scale=.4,opacity=0;
   if(kind==='dev-singularity'){scale=(age<burst?1.15+Math.sin(age*3+i)*.12:1.2+t*5)*(1+i*.15);ring.rotation.set(age*(i+1)*.4,age*(i%2?-.7:.5),age+i);opacity=age<burst?.65:Math.max(0,1-t*1.8);}
   else if(kind==='ragdoll-gravity'){scale=Math.max(.03,1.1-age*.3);ring.rotation.set(Math.PI/2+i*.65,age*1.4,0);opacity=age<burst?.6:Math.max(0,1-t*4);}
   else if(kind==='ragdoll-static'){ring.rotation.set(age*4+i,age*6+i,0);scale=.65+i*.14;opacity=(Math.sin(age*35+i)> .65?.7:.06)*Math.max(0,1-age/3);}
   else if(kind==='ragdoll-pinball'){const pulse=(age+.15*i)%.85;ring.rotation.x=Math.PI/2;scale=.35+pulse*2;opacity=Math.max(0,1-pulse/.65)*.6;}
   else if(finish){ring.rotation.set(i*Math.PI/3,age*.3,0);scale=.3+t*3;opacity=age>=burst?Math.max(0,1-t*2):0;}
   ring.scale.setScalar(scale);ring.material.opacity=opacity;ring.visible=opacity>.001;
  });
 }
 dispose(){this.root.removeFromParent();this.points.geometry.dispose();this.points.material.dispose();if(this.core){this.core.geometry.dispose();this.core.material.dispose();}for(const ring of this.rings){ring.geometry.dispose();ring.material.dispose();}}
}
