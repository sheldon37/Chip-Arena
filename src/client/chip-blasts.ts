import * as T from 'three';
import type {Vec} from '../shared/config.js';
import type {FieldView} from '../shared/chaos.js';

/**
 * Volumetric voxel blasts for chip impacts and areas (owner request 2026-10-03: explosions must read as real
 * explosions, not flat ground decals). Cosmetic only: every blast is keyed to an authoritative event (position and
 * gameplay radius) or a live authoritative field; nothing here feeds back into the simulation.
 *
 * Budget: two InstancedMeshes (opaque voxels, additive glow voxels) with fixed capacity and preallocated typed
 * arrays, plus a fixed pool of point lights kept in the scene at zero intensity (no shader recompiles, no per-hit
 * allocation). Spawns past capacity are dropped. Counts scale with effects quality; compatibility mode drops lights
 * and most glow voxels.
 */
export type BlastStyle='fire'|'shock'|'implosion'|'frost'|'toxic'|'void'|'nova'|'kinetic'|'quake'|'meteor'|'mine';
/** Seconds the shock front takes to reach the gameplay radius. The front ends exactly at that radius. */
export const BLAST_FRONT_SECONDS=.3;
export const BLAST_CAPACITY={solid:900,glow:700,lights:3} as const;

const PALETTE:Record<BlastStyle,{core:number;hot:number;cool:number;smoke:number;debris:number}>={
 fire:{core:0xfff6d2,hot:0xffb340,cool:0xd8401c,smoke:0x57504c,debris:0x5b4636},
 meteor:{core:0xfff2c4,hot:0xff9a2e,cool:0xc8321a,smoke:0x332b28,debris:0x4a3a30},
 mine:{core:0xffe9e0,hot:0xff6a5a,cool:0xb8202c,smoke:0x352c2e,debris:0x4b3d3a},
 shock:{core:0xffffff,hot:0xbfe6ff,cool:0x6f8dff,smoke:0x2a3048,debris:0x8fd6ff},
 implosion:{core:0xffffff,hot:0xd59bff,cool:0x7a32d8,smoke:0x231834,debris:0x6b45a8},
 frost:{core:0xffffff,hot:0xc9f4ff,cool:0x5fb8ef,smoke:0xb9dceb,debris:0xe6fbff},
 toxic:{core:0xf2ffd0,hot:0xb8f53c,cool:0x4f9a25,smoke:0x3c5a2a,debris:0x4a5a30},
 void:{core:0xf3e2ff,hot:0xbc72ff,cool:0x4a1d86,smoke:0x1c1028,debris:0x7a4ac2},
 nova:{core:0xffffff,hot:0xfff0a8,cool:0xffa24a,smoke:0x4a3b30,debris:0xffd27a},
 kinetic:{core:0xffffff,hot:0xbff7ff,cool:0x52c7ff,smoke:0x3b4650,debris:0x8a99a6},
 quake:{core:0xfff1d8,hot:0xffc27a,cool:0x9a6a3a,smoke:0x6b5a48,debris:0x5d4b3a},
};
/** Maps an authoritative explosion `message` (the engine's blast kind or chip fx tag) to a style. */
export function blastStyle(message:string|undefined):BlastStyle{
 switch(message){
  case 'thunder':case 'thunderstep':case 'static':case 'storm':case 'lightning-rod':return 'shock';
  case 'implosion':case 'implosion-bomb':return 'implosion';case 'frost':case 'blizzard':return 'frost';case 'corpseParty':case 'toxic':case 'acid-rain':return 'toxic';
  case 'meteor':case 'artillery':return 'meteor';case 'mine':case 'cluster-mine':case 'mirror-mine':case 'bread-crumbs':case 'proximity-mail':return 'mine';
  case 'dashBlast':case 'kinetic':case 'bubble-mine':return 'kinetic';
  case 'quake':case 'aftershock':case 'trampoline':return 'quake';case 'vacuum':case 'void':case 'black-hole':return 'void';case 'blockPulse':case 'nova':return 'nova';
  default:return 'fire';
 }
}

/** Small deterministic generator so the module never depends on Math.random (repeatable lab strips and tests). */
function rng(seed:number){let s=seed>>>0||1;return()=>{s=(Math.imul(s,1664525)+1013904223)>>>0;return s/4294967296;};}
const tmpColor=new T.Color(),tmpColor2=new T.Color();

/** Fixed-capacity voxel particle pool rendered as one InstancedMesh. Structure-of-arrays, swap-remove. */
class VoxelPool{
 readonly mesh:T.InstancedMesh;count=0;
 private p:Float32Array;private v:Float32Array;private c0:Float32Array;private c1:Float32Array;private a:Float32Array;
 private matrix=new T.Matrix4();private quat=new T.Quaternion();private scale=new T.Vector3();private pos=new T.Vector3();private euler=new T.Euler();
 static readonly A=10;// age,life,size0,size1,gravity,drag,delay,floor,stretch,spin
 constructor(readonly capacity:number,glow:boolean,name:string){
  const material=new T.MeshBasicMaterial({color:0xffffff,toneMapped:false,...(glow?{transparent:true,blending:T.AdditiveBlending,depthWrite:false}:{})});
  this.mesh=new T.InstancedMesh(new T.BoxGeometry(1,1,1),material,capacity);this.mesh.instanceMatrix.setUsage(T.DynamicDrawUsage);
  this.mesh.setColorAt(0,new T.Color());this.mesh.count=0;this.mesh.frustumCulled=false;this.mesh.name=name;this.mesh.renderOrder=glow?3:0;
  this.p=new Float32Array(capacity*3);this.v=new Float32Array(capacity*3);this.c0=new Float32Array(capacity*3);this.c1=new Float32Array(capacity*3);this.a=new Float32Array(capacity*VoxelPool.A);
 }
 spawn(x:number,y:number,z:number,vx:number,vy:number,vz:number,life:number,size0:number,size1:number,color0:number,color1:number,gravity=0,drag=0,delay=0,floor=-1e9,stretch=1,spin=0){
  if(this.count>=this.capacity)return false;const i=this.count++,j=i*3,k=i*VoxelPool.A;
  this.p[j]=x;this.p[j+1]=y;this.p[j+2]=z;this.v[j]=vx;this.v[j+1]=vy;this.v[j+2]=vz;
  tmpColor.setHex(color0);this.c0[j]=tmpColor.r;this.c0[j+1]=tmpColor.g;this.c0[j+2]=tmpColor.b;tmpColor.setHex(color1);this.c1[j]=tmpColor.r;this.c1[j+1]=tmpColor.g;this.c1[j+2]=tmpColor.b;
  const a=this.a;a[k]=-delay;a[k+1]=Math.max(.01,life);a[k+2]=size0;a[k+3]=size1;a[k+4]=gravity;a[k+5]=drag;a[k+6]=delay;a[k+7]=floor;a[k+8]=stretch;a[k+9]=spin;return true;
 }
 /** Age and position of particle i (tests). */
 at(i:number){const j=i*3;return {x:this.p[j],y:this.p[j+1],z:this.p[j+2],age:this.a[i*VoxelPool.A],life:this.a[i*VoxelPool.A+1]};}
 private kill(i:number){const last=--this.count;if(i===last)return;const j=i*3,l=last*3;for(let n=0;n<3;n++){this.p[j+n]=this.p[l+n];this.v[j+n]=this.v[l+n];this.c0[j+n]=this.c0[l+n];this.c1[j+n]=this.c1[l+n];}const k=i*VoxelPool.A,m=last*VoxelPool.A;for(let n=0;n<VoxelPool.A;n++)this.a[k+n]=this.a[m+n];}
 update(dt:number,glow:boolean){
  const a=this.a,p=this.p,v=this.v;
  for(let i=this.count-1;i>=0;i--){const k=i*VoxelPool.A;a[k]+=dt;if(a[k]>=a[k+1]){this.kill(i);continue;}if(a[k]<0)continue;const j=i*3,drag=Math.exp(-a[k+5]*dt);
   v[j]*=drag;v[j+2]*=drag;v[j+1]=v[j+1]*drag-a[k+4]*dt;p[j]+=v[j]*dt;p[j+1]+=v[j+1]*dt;p[j+2]+=v[j+2]*dt;
   if(p[j+1]<a[k+7]){p[j+1]=a[k+7];if(v[j+1]<0){v[j+1]*=-.28;v[j]*=.6;v[j+2]*=.6;}}}
  let n=0;
  for(let i=0;i<this.count;i++){const k=i*VoxelPool.A,j=i*3,age=a[k];if(age<0)continue;const u=Math.min(1,age/a[k+1]),size=(a[k+2]+(a[k+3]-a[k+2])*u)*(glow?1:Math.min(1,(1-u)*4));
   this.pos.set(p[j],p[j+1],p[j+2]);const sp=a[k+9];this.euler.set(age*sp*1.3+i,age*sp+i*.7,0);this.quat.setFromEuler(this.euler);
   const stretch=a[k+8];if(stretch!==1){const vx=v[j],vy=v[j+1],vz=v[j+2],l=Math.hypot(vx,vy,vz);if(l>.05){this.quat.setFromUnitVectors(T.Object3D.DEFAULT_UP,this.pos.set(vx/l,vy/l,vz/l));this.pos.set(p[j],p[j+1],p[j+2]);}this.scale.set(size,size*stretch,size);}else this.scale.setScalar(size);
   this.matrix.compose(this.pos,this.quat,this.scale);this.mesh.setMatrixAt(n,this.matrix);
   tmpColor.setRGB(this.c0[j],this.c0[j+1],this.c0[j+2]);tmpColor2.setRGB(this.c1[j],this.c1[j+1],this.c1[j+2]);tmpColor.lerp(tmpColor2,Math.min(1,u*1.4));
   // Additive voxels fade by dimming; opaque voxels fade by shrinking (size1 carries that).
   if(glow)tmpColor.multiplyScalar((1-u)*(1-u)*1.15);this.mesh.setColorAt(n,tmpColor);n++;}
  this.mesh.count=n;this.mesh.instanceMatrix.needsUpdate=true;if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;
 }
 clear(){this.count=0;this.mesh.count=0;}
 dispose(){this.mesh.removeFromParent();this.mesh.geometry.dispose();(this.mesh.material as T.Material).dispose();this.mesh.dispose();}
}

type Flash={light:T.PointLight;at:number;peak:number;life:number};
export class ChipBlasts{
 readonly solid:VoxelPool;readonly glow:VoxelPool;readonly flashes:Flash[]=[];
 private time=0;private random=rng(0x51a57);private nextLight=0;private fieldClock=new Map<string,number>();
 constructor(readonly scene:T.Scene){
  this.solid=new VoxelPool(BLAST_CAPACITY.solid,false,'chip-blast-voxels');this.glow=new VoxelPool(BLAST_CAPACITY.glow,true,'chip-blast-glow');scene.add(this.solid.mesh,this.glow.mesh);
  for(let i=0;i<BLAST_CAPACITY.lights;i++){const light=new T.PointLight(0xffffff,0,8,2);light.name='chip-blast-light';scene.add(light);this.flashes.push({light,at:-1e9,peak:0,life:.3});}
 }
 private flash(pos:Vec,color:number,radius:number,peak:number,life:number,compat:boolean,quality:number){if(compat||quality<.5)return;const f=this.flashes[this.nextLight++%this.flashes.length];f.light.position.set(pos.x,pos.y+.4,pos.z);f.light.color.setHex(color);f.light.distance=Math.max(4,radius*3);f.at=this.time;f.peak=peak;f.life=life;}
 /** Sphere of directions, roughly even (golden spiral) with jitter. */
 private dir(i:number,n:number,hemi=false){const y=hemi?1-(i+.5)/n:1-2*(i+.5)/n,r=Math.sqrt(Math.max(0,1-y*y)),phi=i*2.39996+this.random()*.4;return [Math.cos(phi)*r,y,Math.sin(phi)*r] as const;}
 /** Ground ring of voxels whose front reaches exactly `radius` after BLAST_FRONT_SECONDS. */
 private ring(pos:Vec,ground:number,radius:number,color0:number,color1:number,count:number,glow:boolean,height=.08,size=.16){
  const pool=glow?this.glow:this.solid,r0=Math.min(.25,radius*.2),speed=(radius-r0)/BLAST_FRONT_SECONDS;
  for(let i=0;i<count;i++){const a=i/count*Math.PI*2+this.random()*.08,c=Math.cos(a),s=Math.sin(a);pool.spawn(pos.x+c*r0,ground+height,pos.z+s*r0,c*speed,0,s*speed,BLAST_FRONT_SECONDS+.001,size,size*.5,color0,color1);}
 }
 /** Hemispherical shock shell: the volumetric read of the blast radius (ends exactly at it). */
 private shell(pos:Vec,ground:number,radius:number,color0:number,color1:number,count:number){
  const r0=Math.min(.25,radius*.2),speed=(radius-r0)/BLAST_FRONT_SECONDS;
  for(let i=0;i<count;i++){const [x,y,z]=this.dir(i,count,true);this.glow.spawn(pos.x+x*r0,ground+y*r0,pos.z+z*r0,x*speed,y*speed,z*speed,BLAST_FRONT_SECONDS+.001,.2,.1,color0,color1);}
 }
 /**
  * One authoritative blast. `radius` is the gameplay radius from the event; the shock front and shell end exactly
  * on it, the fireball fills ~60% of it, debris and smoke may travel past it (cosmetic).
  */
 blast(style:BlastStyle,pos:Vec,radius:number,opts:{quality:number;compat?:boolean;color?:number}={quality:1}){
  const q=Math.max(0,Math.min(1,opts.quality));if(q<=0)return;const compat=!!opts.compat,pal=PALETTE[style],R=Math.max(.4,radius),n=(base:number)=>Math.max(1,Math.round(base*q*(compat?.4:1)*Math.min(1.6,.55+R*.18)));
  const ground=pos.y-.95,rnd=this.random,center={x:pos.x,y:Math.max(ground+.35,pos.y),z:pos.z};
  const core=(count:number,spread:number,life:number,size:number,rise=0)=>{for(let i=0,c=n(count);i<c;i++){const [x,y,z]=this.dir(i,c),s=spread*(.6+rnd()*.6);this.glow.spawn(center.x,center.y,center.z,x*s,y*s*.8+rise,z*s,life*(.7+rnd()*.5),size*(.8+rnd()*.5),size*.3,pal.core,pal.hot,-rise*.5,3.5);}};
  const fireball=(count:number,reach:number,life:number,size:number,rise:number)=>{for(let i=0,c=n(count);i<c;i++){const [x,y,z]=this.dir(i,c),s=reach*(2.2+rnd()*1.6);this.solid.spawn(center.x+x*.1,center.y+Math.abs(y)*.1,center.z+z*.1,x*s,Math.abs(y)*s*.7+rise,z*s,life*(.75+rnd()*.5),size*(.8+rnd()*.6),size*.12,pal.hot,pal.cool,-rise*.6,3.2,0,ground,1,2);}};
  const smoke=(count:number,reach:number,delay:number)=>{for(let i=0,c=n(count);i<c;i++){const [x,,z]=this.dir(i,c),s=reach*(.4+rnd()*.7);this.solid.spawn(center.x+x*reach*.35,center.y+.1,center.z+z*reach*.35,x*s,1.1+rnd()*1.3,z*s,1.1+rnd()*.7,.18+rnd()*.12,.55+rnd()*.35*Math.min(2,R/2),pal.smoke,0x16181c,-.25,1.6,delay,ground,1,.6);}};
  const debris=(count:number,power:number,color=pal.debris)=>{for(let i=0,c=n(count);i<c;i++){const [x,y,z]=this.dir(i,c,true),s=power*(.5+rnd()*.8);this.solid.spawn(center.x,center.y,center.z,x*s,Math.abs(y)*s+2+rnd()*3,z*s,.9+rnd()*.6,.09+rnd()*.12,.06,color,color,14,.4,0,ground+.04,1,6);}};
  const sparks=(count:number,power:number,color=pal.hot)=>{for(let i=0,c=n(count);i<c;i++){const [x,y,z]=this.dir(i,c),s=power*(.6+rnd()*.9);this.glow.spawn(center.x,center.y,center.z,x*s,Math.abs(y)*s+1,z*s,.35+rnd()*.35,.06,.03,pal.core,color,9,.8,0,ground+.03,4);}};
  switch(style){
   case 'fire':case 'mine':case 'meteor':{
    core(18,R*2.2,.24,.5);fireball(42,R*.6,.7,.62,1.4);this.shell(pos,ground,R,pal.hot,pal.cool,n(34));this.ring(pos,ground,R,pal.hot,pal.cool,n(36),true);smoke(16,R*.6,.18);debris(14,R*2.6);sparks(18,R*3.2);
    if(style==='meteor')for(let i=0,c=n(10);i<c;i++)this.glow.spawn(center.x+(rnd()-.5)*.6,center.y+.2,center.z+(rnd()-.5)*.6,(rnd()-.5)*1.4,6+rnd()*5,(rnd()-.5)*1.4,.55,.22,.06,pal.core,pal.cool,8,.5);
    this.flash(center,pal.hot,R,9+R*2,.38,compat,q);break;}
   case 'kinetic':{
    core(10,R*2,.16,.3);this.shell(pos,ground,R,pal.hot,pal.cool,n(40));this.ring(pos,ground,R,pal.core,pal.cool,n(40),true,.06,.2);this.ring(pos,ground,R*.98,0xd9e6ee,0x6f7d88,n(26),false,.12,.22);debris(10,R*2.2);sparks(14,R*3,pal.cool);smoke(6,R*.4,.05);this.flash(center,pal.hot,R,6+R,.28,compat,q);break;}
   case 'quake':{
    this.ring(pos,ground,R,pal.hot,pal.cool,n(30),true,.05,.18);
    for(let i=0,c=n(26);i<c;i++){const a=i/c*Math.PI*2,r=R*(.25+rnd()*.7),x=Math.cos(a)*r,z=Math.sin(a)*r;this.solid.spawn(pos.x+x,ground,pos.z+z,x*.6,5+rnd()*4,z*.6,1.1,.2+rnd()*.2,.08,pal.debris,0x3a3028,16,.3,rnd()*.12,ground+.05,1,4);}
    smoke(14,R*.7,0);this.flash(center,pal.hot,R,4,.25,compat,q);break;}
   case 'shock':{
    // A jagged bolt from the sky into the centre, a white core, a blue shock front and skittering arc sparks.
    const top=ground+9,steps=n(22);let x=center.x,z=center.z;for(let i=0;i<steps;i++){const y=top-(top-ground)*(i/steps);x+=(rnd()-.5)*.45;z+=(rnd()-.5)*.45;if(i===steps-1){x=center.x;z=center.z;}this.glow.spawn(x,y,z,0,0,0,.22+i/steps*.08,.22,.12,pal.core,pal.hot,0,0,i*.004);if(i%4===2)this.glow.spawn(x,y,z,(rnd()-.5)*8,(rnd()-.5)*3,(rnd()-.5)*8,.2,.1,.04,pal.core,pal.cool,0,2,i*.004);}
    core(12,R*1.6,.2,.34);this.shell(pos,ground,R,pal.hot,pal.cool,n(36));this.ring(pos,ground,R,pal.core,pal.cool,n(36),true,.06,.18);sparks(26,R*3.6,pal.cool);
    for(let k=0;k<n(5);k++){const a=rnd()*Math.PI*2,len=R*(.5+rnd()*.5);for(let s=1;s<=6;s++){const u=s/6;this.glow.spawn(center.x+Math.cos(a)*len*u+(rnd()-.5)*.3,ground+.15+rnd()*.5,center.z+Math.sin(a)*len*u+(rnd()-.5)*.3,0,0,0,.16,.12,.05,pal.core,pal.cool,0,0,.03+u*.04);}}
    this.flash(center,0xbfe6ff,R,12+R*2,.3,compat,q);break;}
   case 'implosion':{
    // Voxels rush in from the radius, then the centre pops.
    for(let i=0,c=n(44);i<c;i++){const [x,y,z]=this.dir(i,c,true),T0=.28;this.glow.spawn(center.x+x*R,ground+.2+y*R*.7,center.z+z*R,-x*R/T0,-y*R*.7/T0,-z*R/T0,T0,.16,.24,pal.cool,pal.hot);}
    core(16,R*2.4,.3,.38);this.shell(pos,ground,R,pal.hot,pal.cool,n(30));sparks(16,R*2.8);smoke(8,R*.3,.15);this.flash(center,pal.hot,R,10,.4,compat,q);break;}
   case 'void':{
    for(let i=0,c=n(40);i<c;i++){const a=i/c*Math.PI*2,r=R*(.6+rnd()*.4),T0=.35+rnd()*.1,x=Math.cos(a)*r,z=Math.sin(a)*r,tx=-Math.sin(a)*r*1.4;this.glow.spawn(center.x+x,center.y+(rnd()-.5)*1.2,center.z+z,-x/T0+tx*.4,(rnd()-.5)*.5,-z/T0+Math.cos(a)*r*.56,T0,.14,.2,pal.cool,pal.hot,0,0,0,-1e9,1,3);}
    for(let i=0,c=n(10);i<c;i++)this.solid.spawn(center.x,center.y,center.z,0,0,0,.42,.05,.26+rnd()*.1,0x07030f,0x1c0c33,0,0,.05,-1e9,1,5);
    this.flash(center,pal.hot,R,5,.3,compat,q);break;}
   case 'nova':{
    core(18,R*2.6,.25,.42);
    for(let i=0,c=n(56);i<c;i++){const [x,y,z]=this.dir(i,c),r0=.25,s=(R-r0)/BLAST_FRONT_SECONDS;this.glow.spawn(center.x+x*r0,center.y+y*r0,center.z+z*r0,x*s,y*s,z*s,BLAST_FRONT_SECONDS+.001,.24,.12,pal.core,pal.cool);}
    this.ring(pos,ground,R,pal.hot,pal.cool,n(34),true);sparks(20,R*3.4);this.flash(center,pal.hot,R,12,.4,compat,q);break;}
   case 'frost':{
    for(let i=0,c=n(26);i<c;i++){const [x,y,z]=this.dir(i,c,true),s=R*(1.4+rnd()*1.4);this.solid.spawn(center.x,center.y,center.z,x*s,Math.abs(y)*s+1.5,z*s,.9+rnd()*.4,.12,.05,pal.debris,pal.cool,9,1.5,0,ground+.04,3.5);}
    core(12,R*1.6,.22,.3);this.ring(pos,ground,R,pal.core,pal.cool,n(32),true);smoke(10,R*.5,.05);this.flash(center,pal.hot,R,6,.3,compat,q);break;}
   case 'toxic':{
    core(10,R*1.6,.2,.3);fireball(20,R*.5,.7,.4,.9);this.shell(pos,ground,R,pal.hot,pal.cool,n(28));this.ring(pos,ground,R,pal.hot,pal.cool,n(30),true);
    for(let i=0,c=n(18);i<c;i++){const [x,,z]=this.dir(i,c),s=R*(.3+rnd()*.5);this.solid.spawn(center.x,center.y,center.z,x*s,.6+rnd()*.8,z*s,1.6+rnd()*.6,.2,.6+rnd()*.3,pal.cool,pal.smoke,-.15,1.3,.1,ground,1,.5);}
    debris(10,R*2);this.flash(center,pal.hot,R,7,.35,compat,q);break;}
  }
 }
 /**
  * Ambient emitters for live authoritative fields (storms strike, acid bubbles, fire licks, frost shards, black holes
  * swallow). Rate-limited per field and by quality; everything stays inside the field's gameplay radius.
  */
 field(f:FieldView,time:number,quality:number,compat=false){
  const q=Math.max(0,Math.min(1,quality))*(compat?.4:1);const fused=f.kind==='bomb'&&(f.fx==='artillery'||f.fx==='thunderstep'||f.fx==='comet');if(q<=0||f.kind==='mine'||f.kind==='bomb'&&!fused)return;
  const rate=fused?40:({storm:7,acid:14,fire:22,frost:12,time:6,'black-hole':26,repulsion:8,heal:9,implosion:30} as Record<string,number>)[f.kind]??0;if(!rate)return;
  const last=this.fieldClock.get(f.id);if(last===undefined||time<last){this.fieldClock.set(f.id,time);return;}
  const due=Math.min(6,Math.floor((time-last)*rate*q));if(due<=0)return;this.fieldClock.set(f.id,last+due/(rate*q));
  const rnd=this.random,R=f.radius,ground=Math.max(.03,f.pos.y-.95),pal=PALETTE[f.kind==='storm'?'shock':f.kind==='acid'?'toxic':f.kind==='frost'?'frost':f.kind==='black-hole'||f.kind==='implosion'?'void':f.kind==='fire'?'fire':f.kind==='repulsion'?'mine':f.kind==='heal'?'toxic':'shock'];
  const spot=()=>{const a=rnd()*Math.PI*2,r=Math.sqrt(rnd())*R*.95;return {x:f.pos.x+Math.cos(a)*r,z:f.pos.z+Math.sin(a)*r,a,r};};
  for(let k=0;k<due;k++){
   if(fused){const left=f.until-time;
    // Meteor Shower: the meteor is visible falling onto its warning circle for the last 0.45 s.
    if(f.fx==='artillery'){if(left>.45)continue;const u=Math.max(0,left/.45),y=ground+.6+u*6,x=f.pos.x-u*2.4,z=f.pos.z-u*1.1;this.glow.spawn(x,y,z,(rnd()-.5)*.6,1.2,(rnd()-.5)*.6,.28,.42,.08,0xfff2c4,0xff7a2a);this.solid.spawn(x+(rnd()-.5)*.25,y+.2,z+(rnd()-.5)*.25,0,.4,0,.5,.2,.35,0x4a3a30,0x262020,-.2,1);}
    // Thunder Step: the target spot crackles before the bolt.
    else if(f.fx==='thunderstep'){const a=rnd()*Math.PI*2,r=f.radius*(.5+rnd()*.5);this.glow.spawn(f.pos.x+Math.cos(a)*r,ground+.08,f.pos.z+Math.sin(a)*r,-Math.cos(a)*r*2,2+rnd()*2,-Math.sin(a)*r*2,.22,.08,.03,0xffffff,0x8fb8ff);}
    // Comet Dash: the rolling pilot trails fire until the landing blast.
    else{this.solid.spawn(f.pos.x+(rnd()-.5)*.4,f.pos.y-.3+rnd()*.6,f.pos.z+(rnd()-.5)*.4,0,.8,0,.45,.3,.05,0xffc04a,0xd8401c,-.4,.6,0,-1e9,1,4);}
    continue;}
   if(f.kind==='storm'){const s=spot();let x=s.x,z=s.z;const top=ground+5.5;for(let i=0;i<9;i++){const y=top-(top-ground)*(i/8);this.glow.spawn(x,y,z,0,0,0,.14,.13,.08,0xffffff,pal.hot);x+=(rnd()-.5)*.35;z+=(rnd()-.5)*.35;}for(let i=0;i<3;i++)this.glow.spawn(s.x,ground+.1,s.z,(rnd()-.5)*4,2+rnd()*2,(rnd()-.5)*4,.25,.06,.03,0xffffff,pal.cool,9);
    const c=spot();this.solid.spawn(c.x,ground+4.6+rnd()*.5,c.z,(rnd()-.5)*.3,0,(rnd()-.5)*.3,1.1,.5,.2,0x3b4466,0x232838,0,.5);}
   else if(f.kind==='acid'){const s=spot();this.glow.spawn(s.x,ground+.05,s.z,0,.9+rnd()*.8,0,.7+rnd()*.4,.1,.22,pal.hot,pal.cool,0,0);if(rnd()<.35)this.solid.spawn(s.x,ground+.15,s.z,0,.5,0,1.2,.15,.45,pal.smoke,0x23301c,-.1,1);}
   else if(f.kind==='fire'){const s=spot();this.solid.spawn(s.x,ground+.05,s.z,(rnd()-.5)*.4,1.6+rnd()*1.4,(rnd()-.5)*.4,.55+rnd()*.35,.24,.04,pal.hot,pal.cool,-.4,.5,0,-1e9,1,3);if(rnd()<.4)this.glow.spawn(s.x,ground+.2,s.z,(rnd()-.5),2.5+rnd()*2,(rnd()-.5),.6,.05,.02,pal.core,pal.hot);}
   else if(f.kind==='frost'){const s=spot();this.solid.spawn(s.x,ground+.02,s.z,0,.15,0,1.2,.05,.12,0xe8fbff,0x8fd8ff,0,0,0,-1e9,4,0);this.glow.spawn(f.pos.x+(rnd()-.5)*R*1.6,ground+1.6+rnd(),f.pos.z+(rnd()-.5)*R*1.6,(rnd()-.5)*.3,-.9,(rnd()-.5)*.3,1.4,.05,.03,0xffffff,0xbfeaff);}
   else if(f.kind==='time'){const a=rnd()*Math.PI*2,y=rnd();this.glow.spawn(f.pos.x+Math.cos(a)*R*Math.sqrt(1-y*y),ground+y*R*.8,f.pos.z+Math.sin(a)*R*Math.sqrt(1-y*y),0,0,0,.9,.1,.04,0xdbe8ff,0x83aeff);}
   else if(f.kind==='black-hole'||f.kind==='implosion'){const a=rnd()*Math.PI*2,r=R*(.7+rnd()*.3),T0=.6,x=Math.cos(a)*r,z=Math.sin(a)*r;this.glow.spawn(f.pos.x+x,ground+.7+(rnd()-.5),f.pos.z+z,-x/T0-z*.8,(rnd()-.5)*.4,-z/T0+x*.8,T0,.12,.04,pal.hot,pal.core,0,0,0,-1e9,2.5);}
   else if(f.kind==='repulsion'){const a=rnd()*Math.PI*2;this.glow.spawn(f.pos.x+Math.cos(a)*.4,ground+.3+rnd()*.8,f.pos.z+Math.sin(a)*.4,Math.cos(a)*R/.5,0,Math.sin(a)*R/.5,.45,.12,.04,0xffd0da,0xff728f,0,.9,0,-1e9,1.2);}
   else if(f.kind==='heal'){const s=spot();this.glow.spawn(s.x,ground+.1,s.z,0,1.1,0,.9,.12,.05,0xd6ffe4,0x79ffa9);}
  }
 }
 update(dt:number){dt=Math.max(0,Math.min(.1,dt));this.time+=dt;this.solid.update(dt,false);this.glow.update(dt,true);
  for(const f of this.flashes){const u=(this.time-f.at)/f.life;f.light.intensity=u>=0&&u<1?f.peak*(1-u)*(1-u):0;}}
 /** Forget field clocks for fields that no longer exist. */
 prune(live:Set<string>){for(const id of this.fieldClock.keys())if(!live.has(id))this.fieldClock.delete(id);}
 get fieldClocks(){return this.fieldClock.size;}
 clear(){this.solid.clear();this.glow.clear();this.fieldClock.clear();for(const f of this.flashes){f.at=-1e9;f.light.intensity=0;}}
 dispose(){this.clear();this.solid.dispose();this.glow.dispose();for(const f of this.flashes)f.light.removeFromParent();}
}
