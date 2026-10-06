import * as T from 'three';
import type {Vec} from '../shared/config.js';
type Burst={position:T.Vector3;velocity:T.Vector3;scale:number;life:number;color:T.Color};
/** All short-lived voxel impact sparks use one GPU draw call and shared geometry/material. */
export class BurstPool {
 mesh:T.InstancedMesh;private items:Burst[]=[];private dummy=new T.Object3D();
 private readonly initialCapacity:number;
 constructor(scene:T.Scene,public capacity=180){this.initialCapacity=capacity;this.mesh=new T.InstancedMesh(new T.BoxGeometry(1,1,1),new T.MeshBasicMaterial({color:0xffffff,toneMapped:false}),capacity);this.mesh.instanceMatrix.setUsage(T.DynamicDrawUsage);this.mesh.count=0;this.mesh.frustumCulled=false;this.mesh.name='pooled-impact-sparks';scene.add(this.mesh);}
 /** Grow for a busy volley; every spark keeps its original lifetime. */
 spawn(pos:Vec,color:number,count:number,power:number,quality:number){for(let i=0;i<count*quality;i++){if(this.items.length>=this.capacity)this.resize(this.capacity*2);this.items.push({position:new T.Vector3(pos.x,pos.y,pos.z),velocity:new T.Vector3((Math.random()-.5)*power,Math.random()*power,(Math.random()-.5)*power),scale:.035+Math.random()*.065,life:.25+Math.random()*.5,color:new T.Color(color)});}}
 private resize(capacity:number){
  const old=this.mesh,next=new T.InstancedMesh(old.geometry,old.material,capacity);next.instanceMatrix.setUsage(T.DynamicDrawUsage);
  next.instanceMatrix.array.set(old.instanceMatrix.array.subarray(0,old.count*16));
  if(old.instanceColor){next.setColorAt(0,new T.Color());next.instanceColor!.array.set(old.instanceColor.array.subarray(0,old.count*3));}
  next.count=old.count;next.name=old.name;next.frustumCulled=false;old.parent?.add(next);old.removeFromParent();old.dispose();this.mesh=next;this.capacity=capacity;
 }
 update(dt:number){for(let i=this.items.length-1;i>=0;i--){const p=this.items[i];p.life-=dt;if(p.life<=0){this.items.splice(i,1);continue;}p.position.addScaledVector(p.velocity,dt);p.velocity.y-=dt*5;p.scale*=Math.max(.93,1-dt*1.2);}this.mesh.count=this.items.length;for(let i=0;i<this.items.length;i++){const p=this.items[i];this.dummy.position.copy(p.position);this.dummy.scale.setScalar(p.scale);this.dummy.updateMatrix();this.mesh.setMatrixAt(i,this.dummy.matrix);this.mesh.setColorAt(i,p.color);}this.mesh.instanceMatrix.needsUpdate=true;if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;}
 clear(){this.items=[];this.mesh.count=0;if(this.capacity!==this.initialCapacity)this.resize(this.initialCapacity);}
 get count(){return this.items.length;}
}
