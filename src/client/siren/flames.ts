import * as T from 'three';
import {particleMaterial,PALETTE} from './materials.js';

const unit=new T.BoxGeometry(1,1,1),m=new T.Matrix4(),q=new T.Quaternion(),e=new T.Euler(),s=new T.Vector3(),p=new T.Vector3(),c=new T.Color();
export type FlameTone='black'|'soot'|'smoke'|'ember'|'white'|'warn';
/** Start colour per tone; each tongue then ages toward its fade colour (black cores turn to violet smoke at the tips). */
const TONES:Record<FlameTone,()=>T.Color>={
 black:()=>c.setHex(PALETTE.ink).lerp(new T.Color(PALETTE.smoke),Math.random()*.7),
 soot:()=>c.setHex(PALETTE.ink).lerp(new T.Color(PALETTE.smoke),Math.random()*.5),
 smoke:()=>c.setHex(0x241c31).lerp(new T.Color(0x3a3150),Math.random()),
 ember:()=>c.setHex(PALETTE.ember).multiplyScalar(.75+Math.random()*.4),
 white:()=>c.setHex(0xffffff),
 warn:()=>c.setHex(PALETTE.warn).lerp(new T.Color(0xffd0d6),Math.random()*.3),
};
const FADES:Record<FlameTone,number>={black:0x5b5078,soot:0x2b2436,smoke:0x7d7299,ember:0x2a2233,white:0x8f86b0,warn:0x4a0a12};

/**
 * Voxel fire: a fixed pool of instanced cubes in WORLD space. Tongues rise with buoyancy, swell then shrink to nothing,
 * and tumble. One draw call per field. `parentInverse` lets the mesh live under a moving parent (a rig) while the
 * particles stay where they were emitted, so flames trail behind a moving Siren.
 */
export class FlameField {
 readonly mesh:T.InstancedMesh;private n:number;private cursor=0;
 private pos:Float32Array;private vel:Float32Array;private rot:Float32Array;private spin:Float32Array;private age:Float32Array;private life:Float32Array;private size:Float32Array;private buoy:Float32Array;private drag:Float32Array;private c0:Float32Array;private c1:Float32Array;
 live=0;
 constructor(capacity:number,material:T.Material=particleMaterial()){
  this.n=capacity;this.mesh=new T.InstancedMesh(unit,material,capacity);this.mesh.name='siren-flames';this.mesh.frustumCulled=false;this.mesh.castShadow=false;this.mesh.receiveShadow=false;
  this.mesh.instanceMatrix.setUsage(T.DynamicDrawUsage);this.mesh.instanceColor=new T.InstancedBufferAttribute(new Float32Array(capacity*3),3);this.mesh.instanceColor.setUsage(T.DynamicDrawUsage);
  this.pos=new Float32Array(capacity*3);this.vel=new Float32Array(capacity*3);this.rot=new Float32Array(capacity*3);this.spin=new Float32Array(capacity*3);
  this.age=new Float32Array(capacity).fill(1);this.life=new Float32Array(capacity).fill(1);this.size=new Float32Array(capacity);this.buoy=new Float32Array(capacity);this.drag=new Float32Array(capacity);this.c0=new Float32Array(capacity*3);this.c1=new Float32Array(capacity*3);
  m.makeScale(0,0,0);for(let i=0;i<capacity;i++)this.mesh.setMatrixAt(i,m);
 }
 /** Emit one cube. size is the peak edge length (m); buoyancy (m/s^2) lifts it; drag (1/s) slows it. */
 emit(x:number,y:number,z:number,vx:number,vy:number,vz:number,life:number,size:number,tone:FlameTone='black',buoyancy=2.2,drag=1.6){
  const i=this.cursor;this.cursor=(this.cursor+1)%this.n;const k=i*3;
  this.pos[k]=x;this.pos[k+1]=y;this.pos[k+2]=z;this.vel[k]=vx;this.vel[k+1]=vy;this.vel[k+2]=vz;
  // Mostly upright tongues that twist about the vertical, not tumbling debris.
  this.rot[k]=(Math.random()-.5)*.5;this.rot[k+1]=Math.random()*6.3;this.rot[k+2]=(Math.random()-.5)*.5;
  this.spin[k]=(Math.random()-.5)*1.2;this.spin[k+1]=(Math.random()-.5)*4;this.spin[k+2]=(Math.random()-.5)*1.2;
  this.age[i]=0;this.life[i]=Math.max(.05,life);this.size[i]=size;this.buoy[i]=buoyancy;this.drag[i]=drag;
  const start=TONES[tone]();this.c0[k]=start.r;this.c0[k+1]=start.g;this.c0[k+2]=start.b;c.setHex(FADES[tone]);this.c1[k]=c.r;this.c1[k+1]=c.g;this.c1[k+2]=c.b;this.mesh.setColorAt(i,start);
 }
 /** A spherical puff of n cubes around a point. */
 burst(x:number,y:number,z:number,n:number,speed:number,life:number,size:number,tone:FlameTone='black',up=1.2,buoyancy=2.2){
  for(let j=0;j<n;j++){const a=Math.random()*Math.PI*2,u=Math.random()*2-1,r=Math.sqrt(1-u*u),v=speed*(.35+Math.random()*.65);
   this.emit(x,y,z,Math.cos(a)*r*v,u*v*.6+up*(.4+Math.random()*.6),Math.sin(a)*r*v,life*(.6+Math.random()*.6),size*(.6+Math.random()*.6),tone,buoyancy);}
 }
 update(dt:number,parentInverse?:T.Matrix4){
  if(parentInverse)this.mesh.matrix.copy(parentInverse);let live=0;
  for(let i=0;i<this.n;i++){
   if(this.age[i]>=this.life[i])continue;const k=i*3;this.age[i]+=dt;const t=this.age[i]/this.life[i];
   if(t>=1){m.makeScale(0,0,0);this.mesh.setMatrixAt(i,m);continue;}live++;
   const d=Math.exp(-this.drag[i]*dt);this.vel[k]*=d;this.vel[k+1]=this.vel[k+1]*d+this.buoy[i]*dt;this.vel[k+2]*=d;
   this.pos[k]+=this.vel[k]*dt;this.pos[k+1]+=this.vel[k+1]*dt;this.pos[k+2]+=this.vel[k+2]*dt;
   this.rot[k]+=this.spin[k]*dt;this.rot[k+1]+=this.spin[k+1]*dt;this.rot[k+2]+=this.spin[k+2]*dt;
   // Swell over the first fifth, then shrink to nothing: a flame tongue, not a falling cube.
   const g=t<.2?t/.2:1-((t-.2)/.8)**1.35,edge=this.size[i]*Math.max(0,g);
   q.setFromEuler(e.set(this.rot[k],this.rot[k+1],this.rot[k+2]));m.compose(p.set(this.pos[k],this.pos[k+1],this.pos[k+2]),q,s.set(edge*.8,edge*1.7,edge*.8));this.mesh.setMatrixAt(i,m);
   const f=t*t;c.setRGB(this.c0[k]+(this.c1[k]-this.c0[k])*f,this.c0[k+1]+(this.c1[k+1]-this.c0[k+1])*f,this.c0[k+2]+(this.c1[k+2]-this.c0[k+2])*f);this.mesh.setColorAt(i,c);
  }
  this.live=live;this.mesh.instanceMatrix.needsUpdate=true;if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;
 }
 clear(){m.makeScale(0,0,0);for(let i=0;i<this.n;i++){this.age[i]=this.life[i]=1;this.mesh.setMatrixAt(i,m);}this.mesh.instanceMatrix.needsUpdate=true;this.live=0;}
 dispose(){this.mesh.dispose();}
}
