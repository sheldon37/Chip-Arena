// Black blood and black flames for PvE adds (design §8.5, stream S5). Minions bleed ink, not red: body-wounds.ts and
// gibber-vfx.ts keep their pinned red palettes and are not used here.
//  - A hit on a full-detail add: 6-10 dark drops through the renderer's BurstPool (ctx.burst, one shared draw) plus two
//    black-flame cubes in this module's own FlameField.
//  - A pve-gib event: an ink spray cone from the joint along the hit direction, a black-flame puff, and one tumbling limb
//    chunk (pooled instanced cubes: ballistic, hash-seeded from the event time, at most 12 live, rewind-safe).
//  - Stumps on full-detail adds drip black flame (about 6 per second each).
// Counts scale with effectsQuality. The FlameField is cosmetic world-space fire (its own jitter is not authority data).
import * as T from 'three';
import type {Vec} from '../../shared/config.js';
import {FlameField} from '../siren/flames.js';
import {hash01} from '../siren/minions/look.js';
import {MINION_PALETTE} from '../siren/minions/materials.js';

export const GORE={dropColor:0x0c0a10,drops:[6,10] as const,hitFlames:2,gibFlames:12,spray:[10,18] as const,chunkMax:12,chunkLife:4,dripRate:6,flameCapacity:220} as const;
export const INK=0x0c0a10;
/** Limb-chunk box sizes (m) by LimbName. */
const CHUNK:Record<string,[number,number,number]>={head:[.3,.3,.28],lArm:[.16,.5,.17],rArm:[.16,.5,.17],lFore:[.15,.34,.16],rFore:[.15,.34,.16],
 lLeg:[.19,.52,.21],rLeg:[.19,.52,.21],lShin:[.17,.4,.19],rShin:[.17,.4,.19]};
interface Chunk {at:number;seed:number;p:Vec;v:Vec;size:[number,number,number];spin:Vec}
const G=9.8,m4=new T.Matrix4(),q=new T.Quaternion(),e=new T.Euler(),pv=new T.Vector3(),sv=new T.Vector3(),ZERO=new T.Matrix4().makeScale(0,0,0);

/** Chunk position at `age` seconds: a ballistic arc that lands on the floor (y = its half height), skids and stops. */
export function chunkAt(c:Pick<Chunk,'p'|'v'|'size'>,age:number,out:{x:number;y:number;z:number;landed:boolean}){
 const rest=c.size[1]*.25,vy=c.v.y,disc=vy*vy+2*G*Math.max(0,c.p.y-rest),tl=(vy+Math.sqrt(disc))/G;
 if(age<tl){out.x=c.p.x+c.v.x*age;out.z=c.p.z+c.v.z*age;out.y=c.p.y+vy*age-.5*G*age*age;out.landed=false;return out;}
 // After landing: skid to a stop over .35 s.
 const k=Math.min(age-tl,.35),slide=k-k*k/(2*.35);
 out.x=c.p.x+c.v.x*tl+c.v.x*.35*slide;out.z=c.p.z+c.v.z*tl+c.v.z*.35*slide;out.y=rest;out.landed=true;return out;
}

export class HordeGore {
 readonly flames:FlameField;readonly chunks:T.InstancedMesh;private list:Chunk[]=[];private scratch={x:0,y:0,z:0,landed:false};
 constructor(readonly scene:T.Scene){
  this.flames=new FlameField(GORE.flameCapacity);this.flames.mesh.name='pve-gore-flames';scene.add(this.flames.mesh);
  this.chunks=new T.InstancedMesh(new T.BoxGeometry(1,1,1),new T.MeshLambertMaterial({color:0xffffff}),GORE.chunkMax);this.chunks.name='pve-gore-chunks';
  this.chunks.instanceMatrix.setUsage(T.DynamicDrawUsage);this.chunks.frustumCulled=false;this.chunks.castShadow=false;this.chunks.receiveShadow=false;
  const c=new T.Color();for(let i=0;i<GORE.chunkMax;i++){this.chunks.setMatrixAt(i,ZERO);this.chunks.setColorAt(i,c.setHex(i%3?MINION_PALETTE.shell:0x8a8b6b).lerp(new T.Color(MINION_PALETTE.char),.35));}
  this.chunks.count=0;scene.add(this.chunks);
 }
 /** Number of limb chunks alive at `time` (at most GORE.chunkMax). */
 liveChunks(time:number){return this.list.filter(c=>time>=c.at&&time-c.at<GORE.chunkLife).length;}
 /** Drop counts for a quality (0..1). */
 static drops(quality:number){return Math.round(GORE.drops[0]+(GORE.drops[1]-GORE.drops[0])*Math.max(0,Math.min(1,quality)));}
 /** A hit on a full-detail add: ink drops at chest height (shared BurstPool) and two black-flame cubes. */
 hit(pos:Vec,hitYaw:number,quality:number,burst:(pos:Vec,color:number,count:number,power:number)=>void){
  const n=HordeGore.drops(quality);burst({x:pos.x,y:pos.y,z:pos.z},GORE.dropColor,n,2.2);
  const dx=Math.sin(hitYaw),dz=Math.cos(hitYaw);
  for(let i=0;i<GORE.hitFlames;i++)this.flames.emit(pos.x+dx*.1,pos.y,pos.z+dz*.1,dx*.8,.6,dz*.8,.55,.09,'black',1.8);
 }
 /** A severed limb (pve-gib): an ink spray cone along `from`, a black-flame puff and one tumbling chunk. */
 gib(e:{pos:Vec;from?:Vec;message?:string;time:number;target?:string},quality:number,burst:(pos:Vec,color:number,count:number,power:number)=>void){
  const seed=(Math.floor(e.time*1000)^hashId(e.target??'')^hashId(e.message??''))>>>0,f=e.from??{x:0,y:0,z:1},len=Math.hypot(f.x,f.y,f.z)||1,dx=f.x/len,dz=f.z/len,k=Math.max(.3,Math.min(1,quality));
  const spray=Math.round(GORE.spray[0]+(GORE.spray[1]-GORE.spray[0])*k);burst(e.pos,GORE.dropColor,Math.ceil(spray*.6),3.2);
  for(let i=0;i<spray;i++){const a=(hash01(seed,i,1)-.5)*.9,c=Math.cos(a),s=Math.sin(a),vx=(dx*c-dz*s)*(2+2.5*hash01(seed,i,2)),vz=(dx*s+dz*c)*(2+2.5*hash01(seed,i,2));
   this.flames.emit(e.pos.x,e.pos.y,e.pos.z,vx,.8+1.6*hash01(seed,i,3),vz,.45+.3*hash01(seed,i,4),.05+.04*hash01(seed,i,5),'soot',-4.5,1.2);}
  this.flames.burst(e.pos.x,e.pos.y,e.pos.z,Math.round(GORE.gibFlames*k),1.1,.7,.12,'black',1.1,2.0);
  const size=CHUNK[e.message??'']??CHUNK.lFore,sp=2.4+1.6*hash01(seed,7);
  this.list.push({at:e.time,seed,p:{...e.pos},v:{x:dx*sp+(hash01(seed,8)-.5)*1.2,y:2.2+1.5*hash01(seed,9),z:dz*sp+(hash01(seed,10)-.5)*1.2},size,
   spin:{x:(hash01(seed,11)-.5)*14,y:(hash01(seed,12)-.5)*10,z:(hash01(seed,13)-.5)*14}});
  while(this.list.length>GORE.chunkMax)this.list.shift();
 }
 /** Black flame dripping off a stump (call per stump per frame with its world position). */
 drip(pos:Vec,dt:number,quality:number,acc:{v:number}){
  acc.v+=dt*GORE.dripRate*Math.max(.3,quality);
  while(acc.v>=1){acc.v-=1;this.flames.emit(pos.x,pos.y,pos.z,0,-.4,0,.5,.06,'black',1.4,2.2);}
 }
 /** Advances the flames and poses the chunks for `time` (chunks are a pure function of their event time). */
 update(time:number,dt:number){
  this.flames.update(Math.max(0,Math.min(.05,dt)));
  this.list=this.list.filter(c=>time-c.at<GORE.chunkLife&&time>=c.at-.5);
  let n=0;
  for(const c of this.list){const age=time-c.at;if(age<0)continue;const at=chunkAt(c,age,this.scratch),spin=at.landed?Math.min(age,.6):age,shrink=1-Math.max(0,(age-GORE.chunkLife+.6)/.6);
   q.setFromEuler(e.set(c.spin.x*spin,c.spin.y*spin,c.spin.z*spin));m4.compose(pv.set(at.x,at.y,at.z),q,sv.set(c.size[0]*shrink,c.size[1]*shrink,c.size[2]*shrink));this.chunks.setMatrixAt(n++,m4);}
  this.chunks.count=n;this.chunks.instanceMatrix.needsUpdate=true;this.chunks.visible=n>0;
 }
 clear(){this.list=[];this.flames.clear();this.chunks.count=0;}
 dispose(){this.clear();this.flames.mesh.removeFromParent();this.flames.dispose();this.chunks.removeFromParent();this.chunks.geometry.dispose();(this.chunks.material as T.Material).dispose();this.chunks.dispose();}
}
function hashId(s:string){let h=2166136261;for(let i=0;i<s.length;i++)h=Math.imul(h^s.charCodeAt(i),16777619);return h>>>0;}
