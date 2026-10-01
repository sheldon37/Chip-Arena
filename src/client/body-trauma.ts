import * as T from 'three';
import {WOUND_COLORS,WOUND_MATERIAL,woundHash} from './body-wounds.js';

/**
 * Lethal trauma pieces for the impact ragdoll (docs/BODY-DAMAGE-HANDOFF.md): the exposed cross-sections where a part
 * was severed or crushed off, and the bounded fragments of a crushed part. Everything is a pure function of the death's
 * seed and age, so late snapshots, rewinds and repeated snapshots show the same frame.
 */
const cube=new T.BoxGeometry(1,1,1),clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const linear=(hex:number)=>new T.Color(hex);

/**
 * A voxel cross-section, lying in its local XZ plane with +Y pointing out of the body. `clean` is a blade or bullet cut:
 * a flush plate of red flesh around the bone and marrow, framed by the part's own shell. `ragged` is a crushed stump:
 * uneven depth, torn shell standing up at the edge, missing corners, bone shards. One instanced draw; the caller places
 * it and calls dispose().
 */
export function woundCap(o:{width:number;depth:number;kind:'clean'|'ragged';seed:number;shell:T.Color;bone?:'centre'|'back'|'none';low?:boolean}):T.InstancedMesh{
 const g=Math.min(o.low?.034:.025,Math.min(o.width,o.depth)/(o.low?5:7)),nx=Math.max(2,Math.round(o.width/g)),nz=Math.max(2,Math.round(o.depth/g)),cells:{x:number;z:number;h:number;w:number;d:number;y:number;color:T.Color}[]=[];
 const bone=o.bone??'centre',bx=0,bz=bone==='back'?-o.depth*.28:0,boneHalf=Math.max(g*.55,Math.min(o.width,o.depth)*.16),ragged=o.kind==='ragged';
 for(let i=0;i<nx;i++)for(let k=0;k<nz;k++){
  const x=(i+.5-nx/2)*(o.width/nx),z=(k+.5-nz/2)*(o.depth/nz),edge=i===0||k===0||i===nx-1||k===nz-1,corner=(i===0||i===nx-1)&&(k===0||k===nz-1),h=woundHash(o.seed,i,k);
  if(corner&&(ragged||h<.5))continue;if(ragged&&edge&&h<.3)continue;
  // A clean cut has no rim of its own (the part's shell frames it); a crushed stump keeps torn shell fragments at its edge.
  const isBone=bone!=='none'&&Math.abs(x-bx)<=boneHalf&&Math.abs(z-bz)<=boneHalf,rim=edge&&ragged&&h>.55;
  const color=rim?o.shell.clone().multiplyScalar(.7+.15*h):isBone?linear(Math.abs(x-bx)<boneHalf*.5&&Math.abs(z-bz)<boneHalf*.5&&!ragged?WOUND_COLORS.marrow:WOUND_COLORS.bone):linear(h<.45?WOUND_COLORS.shallow:h<.8?WOUND_COLORS.mid:WOUND_COLORS.deep).multiplyScalar(.88+.24*woundHash(o.seed,k,i,3));
  const height=ragged?(rim?.03+.02*h:.012+.035*woundHash(o.seed,i,k,5)):(isBone?.02:.014);
  cells.push({x,z,h:height,w:o.width/nx,d:o.depth/nz,y:height/2,color});
 }
 // Crushed bone: a few splinters standing out of the stump at an angle.
 if(ragged&&bone!=='none')for(let n=0;n<(o.low?1:3);n++){const a=woundHash(o.seed,n,9)*Math.PI*2,r=boneHalf*.6*woundHash(o.seed,n,10),h=.04+.05*woundHash(o.seed,n,11);
  cells.push({x:bx+Math.cos(a)*r,z:bz+Math.sin(a)*r,h,w:g*.45,d:g*.45,y:h/2,color:linear(WOUND_COLORS.bone)});}
 const mesh=new T.InstancedMesh(cube,WOUND_MATERIAL,cells.length),m=new T.Matrix4(),q=new T.Quaternion(),e=new T.Euler();
 const gap=ragged?.96:1;cells.forEach((c,i)=>{const tilt=ragged&&c.w<g*.5?.35:0;e.set(tilt*(woundHash(o.seed,i,12)-.5)*2,0,tilt*(woundHash(o.seed,i,13)-.5)*2);m.compose(new T.Vector3(c.x,c.y,c.z),q.setFromEuler(e),new T.Vector3(c.w*gap,c.h,c.d*gap));mesh.setMatrixAt(i,m);mesh.setColorAt(i,c.color);});
 mesh.name='gibber-wound';mesh.userData.bodyWound=true;mesh.castShadow=false;mesh.receiveShadow=true;return mesh;
}

/** Rig meshes whose cross-section a cut through each joint exposes (the thinner side of the joint; RIG_VERSION 2). */
export const CAP_MESH:Record<string,string>={head:'neck-joint',spine:'abdomen-shell','left-shoulder':'left-upper-arm','right-shoulder':'right-upper-arm','left-elbow':'left-forearm','right-elbow':'right-forearm','left-hip':'left-thigh','right-hip':'right-thigh','left-knee':'left-shin','right-knee':'right-shin'};
/** Which way each part's bone leaves its joint, in the joint frame: the head and trunk point up, limbs hang down. */
export const boneSign=(root:string)=>root==='head'||root==='spine'?1:-1;
const probe=new T.Vector3();
const insideAny=(p:T.Vector3,inverses:readonly T.Matrix4[])=>inverses.some(m=>{probe.copy(p).applyMatrix4(m);return Math.abs(probe.x)<=.5&&Math.abs(probe.y)<=.5&&Math.abs(probe.z)<=.5;});
/**
 * Where a part's geometry ends along the bone through a joint: the distance s (metres, along `bone`) from the joint to the
 * end face. `inverses` are the inverse world matrices of the part's body meshes (unit cubes). The parent's geometry lies
 * behind the joint (-bone), the child's in front (+bone). A cap set there is flush with the end of the part.
 */
export function faceAlong(joint:T.Vector3,bone:T.Vector3,inverses:readonly T.Matrix4[],side:'parent'|'child',reach=.14):number{
 if(!inverses.length)return 0;const step=.004,p=new T.Vector3(),inside=(s:number)=>insideAny(p.copy(joint).addScaledVector(bone,s),inverses);
 // Leaving the geometry: the parent outward along +bone, the child backward along -bone; entering it the other way.
 const out=side==='parent'?1:-1,start=inside(0);
 for(let n=1;n*step<=reach;n++){const s=(start?out:-out)*n*step;if(inside(s)!==start)return s-(start?out:-out)*step/2;}
 return 0;
}

/** A source for crush fragments: one body mesh (unit cube) at its world transform with its colour. */
export interface DebrisSource {matrix:T.Matrix4;color:T.Color}
/** A loose object thrown clear of a crushed part (hat, held weapon): keeps its own meshes and materials. */
export interface DebrisObject {object:T.Object3D}
interface Chunk {start:T.Vector3;vel:T.Vector3;spin:T.Vector3;size:number;color:T.Color}
interface Loose {object:T.Object3D;start:T.Vector3;rotation:T.Quaternion;vel:T.Vector3;spin:T.Vector3;radius:number}
const GRAVITY=15;
/** Where a ballistic point is at `t`: it falls onto `floor` and slides to a stop; a pure function of t. */
function ballistic(start:T.Vector3,vel:T.Vector3,floor:number,t:number,out:T.Vector3){
 const hit=(vel.y+Math.sqrt(Math.max(0,vel.y*vel.y+2*GRAVITY*Math.max(0,start.y-floor))))/GRAVITY,flight=Math.min(t,hit),slide=Math.max(0,t-hit),travel=flight+(1-Math.exp(-slide*4))*.12;
 out.set(start.x+vel.x*travel,t<hit?start.y+vel.y*t-GRAVITY/2*t*t:floor,start.z+vel.z*travel);
 if(t>=hit){const bounce=Math.max(0,Math.sin(Math.min(Math.PI,slide*9)))*Math.min(.12,Math.abs(vel.y)*.02)*Math.exp(-slide*3);out.y+=bounce;}
 return {landed:t>=hit,hit};
}
/** Bounded fragments of a crushed part, thrown along the blow and outwards, with any loose objects it carried. */
export class CrushDebris {
 readonly root=new T.Group();readonly count:number;private chunks:Chunk[]=[];private loose:Loose[]=[];private mesh:T.InstancedMesh;private floor:number;
 private m=new T.Matrix4();private p=new T.Vector3();private q=new T.Quaternion();private e=new T.Euler();private s=new T.Vector3();
 constructor(o:{sources:DebrisSource[];loose:DebrisObject[];origin:T.Vector3;direction:T.Vector3;inherited:T.Vector3;kick:number;severity:number;seed:number;limit:number;floor:number}){
  this.root.name='crush-debris';this.floor=o.floor;const dir=o.direction.clone();if(dir.lengthSq()<1e-6)dir.set(0,0,1);dir.normalize();
  // Candidates: each source box split into ~7 cm chunks (its own colour), then an even pick down to the limit.
  const candidates:{pos:T.Vector3;color:T.Color;size:number}[]=[];
  for(const src of o.sources){const scale=new T.Vector3();src.matrix.decompose(new T.Vector3(),new T.Quaternion(),scale);const counts=[scale.x,scale.y,scale.z].map(n=>Math.max(1,Math.min(4,Math.round(n/.075))));
   for(let x=0;x<counts[0];x++)for(let y=0;y<counts[1];y++)for(let z=0;z<counts[2];z++)candidates.push({pos:new T.Vector3((x+.5)/counts[0]-.5,(y+.5)/counts[1]-.5,(z+.5)/counts[2]-.5).applyMatrix4(src.matrix),color:src.color,size:Math.min(.085,Math.max(.04,Math.min(scale.x/counts[0],scale.y/counts[1],scale.z/counts[2])))});}
  // Loose objects (a hat, the weapon) come first within the limit; the rest of it is chunks.
  const looseList=o.loose.slice(0,Math.max(0,o.limit)),n=Math.max(0,Math.min(o.limit-looseList.length,candidates.length)),sev=clamp(o.severity);
  for(let i=0;i<n;i++){const c=candidates[Math.floor(i*candidates.length/n)],h=(k:number)=>woundHash(o.seed,i,k);
   // A third of the pieces show the inside: flesh and a little bone.
   const color=h(0)<.22?new T.Color(WOUND_COLORS.shallow).multiplyScalar(.8+.3*h(1)):h(0)<.3?new T.Color(WOUND_COLORS.bone):h(0)<.36?new T.Color(WOUND_COLORS.mid):c.color.clone().multiplyScalar(.75+.3*h(1));
   const out=c.pos.clone().sub(o.origin);out.y=Math.max(out.y,0);if(out.lengthSq()<1e-6)out.set(h(2)-.5,.5,h(3)-.5);out.normalize();
   const vel=o.inherited.clone().addScaledVector(out,(2+2.6*h(4))*(.6+.6*sev)).addScaledVector(dir,o.kick*(.35+.55*h(5))).add(new T.Vector3((h(6)-.5)*1.4,1.2+1.8*h(7),(h(8)-.5)*1.4));
   this.chunks.push({start:c.pos.clone(),vel,spin:new T.Vector3(h(9)*14-7,h(10)*14-7,h(11)*14-7),size:c.size*(.7+.4*h(12)),color});}
  this.mesh=new T.InstancedMesh(cube,WOUND_MATERIAL,Math.max(1,this.chunks.length));this.mesh.name='crush-fragments';this.mesh.castShadow=true;this.mesh.frustumCulled=false;this.mesh.count=this.chunks.length;
  this.chunks.forEach((c,i)=>this.mesh.setColorAt(i,c.color));this.root.add(this.mesh);
  looseList.forEach(({object},i)=>{const start=object.position.clone(),h=(k:number)=>woundHash(o.seed,i,20+k);const vel=o.inherited.clone().addScaledVector(dir,o.kick*.55).add(new T.Vector3((h(0)-.5)*1.6,2.4+1.4*h(1),(h(2)-.5)*1.6));
   this.loose.push({object,start,rotation:object.quaternion.clone(),vel,spin:new T.Vector3(h(3)*8-4,h(4)*8-4,h(5)*8-4),radius:.08});this.root.add(object);});
  this.count=this.chunks.length+this.loose.length;this.update(0);
 }
 update(age:number){
  const t=Math.max(0,age),fade=clamp((5-t)/.7);
  this.chunks.forEach((c,i)=>{const {landed,hit}=ballistic(c.start,c.vel,this.floor+c.size/2,t,this.p),spinT=landed?hit+.25*(1-Math.exp(-(t-hit)*4)):t;
   this.q.setFromEuler(this.e.set(c.spin.x*spinT,c.spin.y*spinT,c.spin.z*spinT));this.m.compose(this.p,this.q,this.s.setScalar(c.size*fade));this.mesh.setMatrixAt(i,this.m);});
  this.mesh.instanceMatrix.needsUpdate=true;
  for(const l of this.loose){const {landed,hit}=ballistic(l.start,l.vel,this.floor+l.radius,t,this.p),spinT=landed?hit+.3*(1-Math.exp(-(t-hit)*4)):t;l.object.position.copy(this.p);
   l.object.quaternion.copy(this.q.setFromEuler(this.e.set(l.spin.x*spinT,l.spin.y*spinT,l.spin.z*spinT))).multiply(l.rotation);l.object.scale.setScalar(Math.max(.001,fade));}
 }
 dispose(){this.root.removeFromParent();this.mesh.dispose();}
}
