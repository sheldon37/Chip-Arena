import * as T from 'three';
type Drop={pos:T.Vector3;vel:T.Vector3;life:number;size:number};
/** Fixed-step particles emit from the moving severed joint; one bounded instanced draw for the spray. */
export class GibberVfx{
 readonly root=new T.Group();readonly count:number;private drops:Drop[]=[];private cursor=0;private splatCursor=0;private spray:T.InstancedMesh;private pools:T.Mesh<T.CircleGeometry,T.MeshLambertMaterial>[]=[];private dummy=new T.Object3D();private pending=0;
 constructor(private seed:number,budget:number,quality:number){this.root.name='gibber-blood';const max=Math.min(quality<.5?55:150,Math.max(0,budget-10)),poolCount=Math.min(10,budget-max);this.count=max+poolCount;
  this.spray=new T.InstancedMesh(new T.SphereGeometry(1,5,3),new T.MeshLambertMaterial({color:0xc10b25}),max);this.spray.name='gibber-spray';this.spray.frustumCulled=false;this.root.add(this.spray);
  for(let i=0;i<max;i++){this.drops.push({pos:new T.Vector3(),vel:new T.Vector3(),life:0,size:0});this.dummy.scale.setScalar(0);this.dummy.updateMatrix();this.spray.setMatrixAt(i,this.dummy.matrix);this.spray.setColorAt(i,new T.Color(i%3?0xc91428:0x750516));}
  const disk=new T.CircleGeometry(1,12),mat=new T.MeshLambertMaterial({color:0x780516,transparent:true,opacity:.85,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-2});for(let i=0;i<poolCount;i++){const pool=new T.Mesh(disk,mat);pool.name='gibber-splatter';pool.rotation.x=-Math.PI/2;pool.visible=false;this.root.add(pool);this.pools.push(pool);}
 }
 private random(){this.seed=(Math.imul(this.seed,1664525)+1013904223)>>>0;return this.seed/4294967296;}
 step(age:number,point:T.Vector3,normal:T.Vector3){const dt=1/60;if(age<3.6){this.pending+=dt*(age<.55?210:age<1.8?85:36);while(this.pending>=1&&this.drops.length){this.pending--;const d=this.drops[this.cursor++%this.drops.length],speed=(age<.55?4.8:2.4)+this.random()*3;d.pos.copy(point);d.vel.copy(normal).multiplyScalar(speed).add(new T.Vector3((this.random()-.5)*3,1.8+this.random()*2,(this.random()-.5)*3));d.life=.6+this.random()*.5;d.size=.025+this.random()*.035;}}
  for(const d of this.drops){if(d.life<=0)continue;d.life-=dt;d.vel.y-=15*dt;d.pos.addScaledVector(d.vel,dt);if(d.pos.y<.025){d.life=0;const pool=this.pools[this.splatCursor++%this.pools.length];if(pool){pool.visible=true;pool.position.set(d.pos.x,.012+(this.splatCursor%4)*.002,d.pos.z);pool.scale.set(.08+this.random()*.22,.06+this.random()*.16,1);pool.rotation.z=this.random()*Math.PI;}}}
 }
 update(age:number){const fade=T.MathUtils.clamp((5-age)/.65,0,1);for(const [i,d]of this.drops.entries()){this.dummy.position.copy(d.pos);this.dummy.quaternion.setFromUnitVectors(T.Object3D.DEFAULT_UP,d.vel.clone().normalize());this.dummy.scale.set(d.size,d.size*(1+Math.min(4,d.vel.length()*.2)),d.size).multiplyScalar(d.life>0?fade:0);this.dummy.updateMatrix();this.spray.setMatrixAt(i,this.dummy.matrix);}this.spray.instanceMatrix.needsUpdate=true;if(this.pools.length)this.pools[0].material.opacity=.85*fade;}
 dispose(){this.root.removeFromParent();this.spray.geometry.dispose();(this.spray.material as T.Material).dispose();this.spray.dispose();if(this.pools.length){this.pools[0].geometry.dispose();this.pools[0].material.dispose();}}
}
