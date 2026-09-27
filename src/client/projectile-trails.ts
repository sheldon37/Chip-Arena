import * as T from 'three';
import {Vec} from '../shared/config.js';
/** Small, depth-tested ribbons through observed authoritative positions. */
export class ProjectileTrails {
 private traces=new Map<string,{mesh:T.Mesh;points:{pos:T.Vector3;at:number}[];seen:number;width:number}>();
 readonly lifetime=.18;readonly maxPoints=12;readonly budget=120;
 constructor(private scene:T.Scene){}
 clear(){for(const t of this.traces.values()){this.scene.remove(t.mesh);t.mesh.geometry.dispose();(t.mesh.material as T.Material).dispose();}this.traces.clear();}
 sample(id:string,pos:Vec,color:number,now:number,width=1){
  let t=this.traces.get(id);
  if(!t){if(this.traces.size>=this.budget)return;
   const geometry=new T.BufferGeometry(),positions=new Float32Array((this.maxPoints-1)*12),colors=new Float32Array(positions.length),indices=[];
   for(let n=0;n<this.maxPoints-1;n++){const i=n*4;indices.push(i,i+1,i+2,i+2,i+1,i+3);}
   geometry.setAttribute('position',new T.BufferAttribute(positions,3).setUsage(T.DynamicDrawUsage));geometry.setAttribute('color',new T.BufferAttribute(colors,3).setUsage(T.DynamicDrawUsage));geometry.setIndex(indices);geometry.setDrawRange(0,0);
   const mesh=new T.Mesh(geometry,new T.MeshBasicMaterial({color,vertexColors:true,transparent:true,opacity:.85,depthWrite:false,side:T.DoubleSide}));mesh.frustumCulled=false;this.scene.add(mesh);t={mesh,points:[],seen:now,width};this.traces.set(id,t);
  }
  const point=new T.Vector3(pos.x,pos.y,pos.z),last=t.points.at(-1);if(!last||last.pos.distanceToSquared(point)>.0025)t.points.push({pos:point,at:now});else last.at=now;
  t.seen=now;t.width=width;t.points=t.points.filter(p=>now-p.at<this.lifetime).slice(-this.maxPoints);
 }
 render(camera:T.Camera,now:number){
  const fallback=new T.Vector3(1,0,0).applyQuaternion(camera.quaternion);
  for(const[id,t]of this.traces){if(now-t.seen>this.lifetime){this.scene.remove(t.mesh);t.mesh.geometry.dispose();(t.mesh.material as T.Material).dispose();this.traces.delete(id);continue;}
   const position=t.mesh.geometry.getAttribute('position') as T.BufferAttribute,color=t.mesh.geometry.getAttribute('color') as T.BufferAttribute;let count=0;
   for(let n=1;n<t.points.length;n++){const a=t.points[n-1],b=t.points[n],middle=a.pos.clone().add(b.pos).multiplyScalar(.5),direction=b.pos.clone().sub(a.pos),side=direction.clone().cross(camera.position.clone().sub(middle));if(side.lengthSq()<1e-8)side.copy(fallback);
    side.normalize().multiplyScalar((.012+camera.position.distanceTo(middle)*.00065)*t.width);
    const vertices=[a.pos.clone().add(side),a.pos.clone().sub(side),b.pos.clone().add(side),b.pos.clone().sub(side)];
    for(let j=0;j<4;j++){const index=count*4+j,p=vertices[j],age=now-(j<2?a.at:b.at),fade=Math.max(0,1-age/this.lifetime);position.setXYZ(index,p.x,p.y,p.z);color.setXYZ(index,fade,fade,fade);}count++;
   }
   position.needsUpdate=true;color.needsUpdate=true;t.mesh.geometry.setDrawRange(0,count*6);(t.mesh.material as T.MeshBasicMaterial).opacity=.85*Math.max(0,1-(now-t.seen)/this.lifetime);
  }
 }
 get count(){return this.traces.size;}
}
