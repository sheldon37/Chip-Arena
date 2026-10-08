import * as T from 'three';
import type {BodyWound} from '../shared/body-damage.js';
import type {GibPart} from '../shared/body-hit.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
import {createCharacterRig} from './character-rig.js';
import {VoxelPart,voxelMesh,customLayout,layoutBoxes,baseLayout,retagVoxelPart,releaseVoxelLayout,setVoxelDetail,EMPTY_LAYOUT,type UnitBox,type VoxelLayout} from './voxel-kit.js';

/**
 * Living wounds (owner request 2026-10-01; contract in docs/BODY-DAMAGE-HANDOFF.md). The authority sends each accepted
 * physical impact as a BodyWound in the victim's upright capsule frame. Here it is mapped once onto the rig part it
 * names (a ray through the rest pose finds the struck mesh and face), then carved into that mesh's voxels: the shell
 * around the opening is removed and the walls of the hole are lined with flesh-coloured voxels. A carved part keeps its
 * mesh, name, material and transform (only its instances change), so the wound moves with every animation and is
 * copied into ragdolls, knockdowns and death effects with the rest of the part. Pure presentation: no hitbox, HP or
 * gameplay state is read back from it.
 */

type V3=T.Vector3;
const v3=(x=0,y=0,z=0)=>new T.Vector3(x,y,z);
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
/** Deterministic per-wound noise in [0,1): the same wound looks the same on every client and after every rebuild. */
export function woundHash(seed:number,a=0,b=0,c=0){let h=(seed^Math.imul(a+101,0x9e3779b1)^Math.imul(b+211,0x85ebca77)^Math.imul(c+307,0xc2b2ae3d))>>>0;h=Math.imul(h^(h>>>16),0x7feb352d)>>>0;h=Math.imul(h^(h>>>15),0x846ca68b)>>>0;return ((h^(h>>>16))>>>0)/4294967296;}
export function stringSeed(s:string){let h=2166136261;for(let i=0;i<s.length;i++)h=Math.imul(h^s.charCodeAt(i),16777619);return h>>>0;}

// ---------------------------------------------------------------------------------------------------------------
// Rest-pose geometry. Every rig is built from the same parts (RIG_VERSION 2), so the mapping from a capsule-local
// wound to (joint, mesh, face) is computed on one reference rig and reused for all players.

export const GIB_ROOTS=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'] as const;
const PART_ROOTS:Record<GibPart,string[]>={head:['head'],spine:['spine','pelvis'],'left-shoulder':['left-shoulder'],'right-shoulder':['right-shoulder'],'left-elbow':['left-elbow'],'right-elbow':['right-elbow'],'left-hip':['left-hip'],'right-hip':['right-hip'],'left-knee':['left-knee'],'right-knee':['right-knee']};
/** Sockets and overlays that never carry flesh: hats, weapons, team bands, glow strips, block shields. */
const SKIP=new Set(['head-cosmetic','chest-cosmetic','back-socket','weapon-socket','offhand-weapon-socket','left-hand-socket','right-hand-socket','team-identity','enchantment','spell-charge','body-chips','timed-block','timed-guard']);
export interface RestMesh {name:string;root:string;center:V3;size:V3;min:V3;max:V3}
let rest:Map<string,RestMesh[]>|undefined;
/** Body meshes owned by each gib root at rest (rig-group frame = the authority's capsule frame at rest). */
export function restMeshes(root:string):RestMesh[]{
 if(!rest){
  rest=new Map();const r=createCharacterRig('#ffffff',0xdce5e8,true);r.group.updateMatrixWorld(true);const roots=new Set<string>(GIB_ROOTS);
  for(const name of GIB_ROOTS){const joint=r.group.getObjectByName(name)!,list:RestMesh[]=[];
   const walk=(o:T.Object3D)=>{if(o!==joint&&(roots.has(o.name)||SKIP.has(o.name)))return;
    if(o instanceof VoxelPart&&!o.userData.jointCover&&!o.userData.detail&&!o.name.startsWith('armor-')){const center=new T.Vector3(),size=new T.Vector3();o.matrixWorld.decompose(center,new T.Quaternion(),size);
     list.push({name:o.name,root:name,center,size,min:center.clone().addScaledVector(size,-.5),max:center.clone().addScaledVector(size,.5)});}
    for(const c of o.children)walk(c);};
   walk(joint);rest.set(name,list);}
 }
 return rest.get(root)||[];
}

// ---------------------------------------------------------------------------------------------------------------
// Anchoring: which mesh and face of the rig a wound lands on.

export interface WoundAnchor {
 /** Rig joint that owns the mesh, and the mesh name inside it. */
 root:string;mesh:string;
 /** Hit point on the face, in metres from the mesh centre (mesh axes = capsule axes at rest). */
 hit:V3;axis:0|1|2;sign:1|-1;
 /** Inward travel direction in mesh axes (unit), kept at least 60° into the face. */
 dir:V3;
 /** Mesh size in metres (rest). */
 size:V3;
}
function rayBox(o:V3,d:V3,min:V3,max:V3){let t0=-Infinity,t1=Infinity,axis=0;
 for(let a=0;a<3;a++){const oa=o.getComponent(a),da=d.getComponent(a),lo=min.getComponent(a),hi=max.getComponent(a);
  if(Math.abs(da)<1e-9){if(oa<lo||oa>hi)return undefined;continue;}
  let n=(lo-oa)/da,f=(hi-oa)/da;if(n>f){const s=n;n=f;f=s;}if(n>t0){t0=n;axis=a;}if(f<t1)t1=f;if(t0>t1)return undefined;}
 return {t0,t1,axis};}
const unitAxis=(axis:number,sign:number)=>v3().setComponent(axis,sign);
function anchorOn(mesh:RestMesh,hitPoint:V3,axis:number,sign:1|-1,travel:V3):WoundAnchor{
 const hit=hitPoint.clone().sub(mesh.center);for(let a=0;a<3;a++)if(a!==axis)hit.setComponent(a,clamp(hit.getComponent(a),-mesh.size.getComponent(a)/2,mesh.size.getComponent(a)/2));
 hit.setComponent(axis,sign*mesh.size.getComponent(axis)/2);
 // The channel runs along the shot, but never skims the surface: at least 60 degrees into the face.
 const inward=unitAxis(axis,-sign),dir=travel.lengthSq()>1e-9?travel.clone().normalize():inward.clone();
 if(dir.dot(inward)<.5){dir.addScaledVector(inward,.5-dir.dot(inward)+.35).normalize();}
 return {root:mesh.root,mesh:mesh.name,hit,axis:axis as 0|1|2,sign,dir,size:mesh.size.clone()};
}
/** The face(s) a wound lands on: the first body mesh of the named part along the shot, plus the mesh behind a thin
 * overlay (sternum, back panel) so the hole goes through the plate into the body. Deterministic for a given wound. */
export function woundAnchors(w:Pick<BodyWound,'part'|'point'|'direction'|'normal'>):WoundAnchor[]{
 const meshes=(PART_ROOTS[w.part]||[]).flatMap(root=>restMeshes(root));if(!meshes.length)return [];
 const point=v3(w.point.x,w.point.y,w.point.z),travel=v3(w.direction.x,w.direction.y,w.direction.z);
 if(!(travel.lengthSq()>1e-9)||!Number.isFinite(travel.x+travel.y+travel.z))travel.set(-w.normal.x,-w.normal.y,-w.normal.z);
 if(!(travel.lengthSq()>1e-9)||!Number.isFinite(travel.x+travel.y+travel.z))travel.set(0,0,-1);
 travel.normalize();
 const along=(origin:V3,dir:V3,keep:V3)=>{
  const hits=meshes.map(m=>({m,r:rayBox(origin,dir,m.min,m.max)})).filter((h):h is {m:RestMesh;r:{t0:number;t1:number;axis:number}}=>!!h.r&&h.r.t1>=0).sort((a,b)=>a.r.t0-b.r.t0);
  if(!hits.length)return undefined;
  const first=hits[0],axis=first.r.axis,sign=(dir.getComponent(axis)>0?-1:1) as 1|-1,out=[anchorOn(first.m,origin.clone().addScaledVector(dir,first.r.t0),axis,sign,keep)];
  // A thin plate in front of the body: carry the hole into the next mesh along the line.
  if(first.m.size.getComponent(axis)<.05){const next=hits.find(h=>h!==first&&h.r.t0>=first.r.t1-.01&&h.r.t0<=first.r.t1+.08);
   if(next){const na=next.r.axis,ns=(dir.getComponent(na)>0?-1:1) as 1|-1;out.push(anchorOn(next.m,origin.clone().addScaledVector(dir,next.r.t0),na,ns,keep));}}
  return out;};
 const shot=along(point.clone().addScaledVector(travel,-.6),travel,travel);if(shot)return shot;
 // No line through the part (a grazing capsule contact, or a blade's contact point just off the surface): the nearest
 // face of the nearest solid mesh (thin plates such as the sternum only count when clearly closer), entered straight in;
 // a plate covering that spot is carved as well.
 let best:{m:RestMesh;d:number}|undefined;for(const m of meshes){const plate=Math.min(m.size.x,m.size.y,m.size.z)<.05,d=point.clone().clamp(m.min,m.max).distanceTo(point)+(plate?.05:0);if(!best||d<best.d)best={m,d};}
 const m=best!.m,local=point.clone().sub(m.center);let axis=0,score=-Infinity;
 for(let a=0;a<3;a++){const s=Math.abs(local.getComponent(a))/Math.max(1e-6,m.size.getComponent(a)/2);if(s>score){score=s;axis=a;}}
 const sign=(local.getComponent(axis)>=0?1:-1) as 1|-1,spot=point.clone().clamp(m.min,m.max),inward=unitAxis(axis,-sign);
 return along(spot.clone().addScaledVector(inward,-.3),inward,inward)||[anchorOn(m,spot,axis,sign,inward)];
}

// ---------------------------------------------------------------------------------------------------------------
// Carving. Everything below works in metres around the mesh centre. A wound replaces a small block of the part (a grid
// of cells aligned with the struck face) with: the shell cells that remain, a torn rim, and flesh cells lining the hole.

/** sRGB hexes for the flesh palette (stylised: blood red to a near-black core, bone, char, bruise). */
export const WOUND_COLORS={shallow:0xa8141f,mid:0x780a14,deep:0x4f0610,core:0x2c0307,cut:0xbd1a24,cutDeep:0x6c0812,bone:0xe8dfcd,marrow:0x7d2a22,char:0x1b1714,soot:0x2e2620,ember:0xe2621e,bruise:0x4a2236} as const;
const linear=(hex:number,k=1)=>{const c=new T.Color(hex);return [c.r*k,c.g*k,c.b*k] as [number,number,number];};
export interface Solid {min:V3;max:V3;color:[number,number,number];flesh:boolean}
export interface CarveSpec {hit:V3;axis:0|1|2;sign:1|-1;dir:V3;size:V3;kind:BodyWound['kind'];trauma:BodyWound['trauma'];radius:number;severity:number;seed:number;low:boolean;body:[number,number,number];vertical?:number}
/** Removal shape of one wound, evaluated on cell offsets from the hit in face coordinates: a,b along the face (U,V),
 * d = depth below the face (metres, >= 0 inside the part). Allocation-free: carving runs inside the render frame. */
type Shape={g:number;n:number;m:number;removed:(a:number,b:number,d:number,i:number,j:number,k:number)=>boolean;scorched?:(a:number,b:number,d:number,i:number,j:number,k:number)=>boolean};

function shapeOf(s:CarveSpec):Shape{
 const ax=s.axis,ua=(ax+1)%3,va=(ax+2)%3,thick=s.size.getComponent(ax),spanU=s.size.getComponent(ua),spanV=s.size.getComponent(va),span=Math.min(spanU,spanV);
 const r=Math.max(.012,s.radius),sev=clamp(s.severity),scale=s.low?1.5:1,seed=s.seed,jit=(i:number,j:number,k:number)=>woundHash(seed,i,j,k)-.5;
 // The shot in face coordinates: (du,dv) along the face, dd into the part (> 0).
 const du=s.dir.getComponent(ua),dv=s.dir.getComponent(va),dd=-s.sign*s.dir.getComponent(ax),drift=Math.hypot(du,dv);
 // In-face unit vectors (a,b components) for "up" (rig y at rest) and the horizontal across it.
 const upU=ua===1?1:0,upV=va===1?1:0,hasUp=upU+upV>0,vert=hasUp?[upU,upV]:[1,0],horiz=[vert[1],-vert[0]];
 const stab=s.trauma==='pierce'&&(s.kind==='puncture'||s.kind==='cavity');
 if(s.kind==='cut'){
  const g=(r>=.12?.016:.0125)*scale,L=clamp(r*2.8,4*g,.92*Math.max(spanU,spanV)),W=g*(r>=.12?2:1.15),Dm=Math.min(2.5*g+2.5*g*sev,.38*thick);
  const tilt=(woundHash(seed,7)-.5)*.9+clamp((s.vertical||0)*1.2,-.5,.5),la=horiz[0]*Math.cos(tilt)+vert[0]*Math.sin(tilt),lb=horiz[1]*Math.cos(tilt)+vert[1]*Math.sin(tilt);
  return {g,n:Math.ceil(L/2/g)+2,m:Math.ceil(Dm/g)+2,removed:(a,b,d,i,j)=>{const along=a*la+b*lb,across=Math.abs(-a*lb+b*la),end=2*Math.abs(along)/L;
   return Math.abs(along)<=L/2&&across<=W/2+.12*g*jit(i,j,0)&&d>=0&&d<=Dm*Math.sqrt(Math.max(0,1-end*end*end));}};
 }
 if(stab){
  const g=.0125*scale,cav=s.kind==='cavity',Ls=clamp(r*2.4*(cav?1.5:1),3*g,.12),W=g*(cav?2.2:1.1),Ds=Math.min(6*g+2*g*sev,.55*thick),tilt=(woundHash(seed,9)-.5)*.5;
  const la=vert[0]*Math.cos(tilt)+horiz[0]*Math.sin(tilt),lb=vert[1]*Math.cos(tilt)+horiz[1]*Math.sin(tilt);
  return {g,n:Math.ceil((Ls/2+drift*Ds)/g)+2,m:Math.ceil(Ds/g)+2,removed:(a,b,d)=>{if(d<-.5*g||d>Ds)return false;const t=d/Math.max(.2,dd),ra=a-du*t,rb=b-dv*t;
   return Math.abs(ra*la+rb*lb)<=Ls/2*(1-.3*d/Ds)&&Math.abs(-ra*lb+rb*la)<=W/2;}};
 }
 if(s.kind==='cavity'){
  const g=clamp(r*.3,.016,.03)*scale,rc=Math.min(r*(.8+.35*sev),.45*span),dc=Math.min(.95*rc,.5*thick),shift=.25*rc,ca=du*shift,cb=dv*shift,cd=dd*shift;
  return {g,n:Math.ceil(rc/g)+2,m:Math.ceil((dc+shift)/g)+2,removed:(a,b,d,i,j,k)=>{const x=a-ca,y=b-cb,z=d-cd;return d>=0&&(x*x+y*y)/(rc*rc)+z*z/(dc*dc)<=1+.44*(.5+sev)*jit(i,j,k);}};
 }
 if(s.kind==='bruise'){
  const g=clamp(r*.28,.016,.03)*scale,rb=Math.min(r*(.75+.35*sev),.48*span),deep=sev>.55;
  return {g,n:Math.ceil(rb/g)+2,m:deep?4:3,removed:(a,b,d,i,j,k)=>{const lat=Math.hypot(a,b);return k===0&&lat<=rb*(1+.25*jit(i,j,0))||deep&&k===1&&lat<=.42*rb;}};
 }
 if(s.kind==='scorch'){
  const g=clamp(r*.28,.016,.03)*scale,rs=Math.min(Math.max(2*g,r*1.25),.48*span);
  return {g,n:Math.ceil(rs/g)+2,m:2,removed:()=>false,scorched:(a,b,d,i,j,k)=>k===0&&Math.hypot(a,b)<=rs*(1+.3*jit(i,j,0))};
 }
 // Puncture: a narrow channel along the shot, tapering as it goes in.
 const g=clamp(r*.3,.009,.016)*scale,rh=Math.max(1.5*g,.62*r),D=Math.min(4.5*g+2.5*g*sev,.42*thick);
 return {g,n:Math.ceil((rh+drift*D)/g)+2,m:Math.ceil(D/g)+2,removed:(a,b,d,i,j,k)=>{if(d<-.5*g||d>D)return false;const t=d/Math.max(.2,dd),ra=a-du*t,rb=b-dv*t;
  return Math.hypot(ra,rb)<=rh*(1-.3*d/D)+.15*g*jit(i,j,k);}};
}

function subtract(b:Solid,r:{min:V3;max:V3}):Solid[]{
 if(b.max.x<=r.min.x||b.min.x>=r.max.x||b.max.y<=r.min.y||b.min.y>=r.max.y||b.max.z<=r.min.z||b.min.z>=r.max.z)return [b];
 const out:Solid[]=[],lo=b.min.clone(),hi=b.max.clone();
 for(let a=0;a<3;a++){
  if(lo.getComponent(a)<r.min.getComponent(a)){const max=hi.clone().setComponent(a,r.min.getComponent(a));out.push({min:lo.clone(),max,color:b.color,flesh:b.flesh});lo.setComponent(a,r.min.getComponent(a));}
  if(hi.getComponent(a)>r.max.getComponent(a)){const min=lo.clone().setComponent(a,r.max.getComponent(a));out.push({min,max:hi.clone(),color:b.color,flesh:b.flesh});hi.setComponent(a,r.max.getComponent(a));}
 }
 return out.filter(s=>s.max.x-s.min.x>1e-6&&s.max.y-s.min.y>1e-6&&s.max.z-s.min.z>1e-6);
}

/** Carves one wound into a set of solids (shell boxes with their shade, and earlier flesh cells). Returns the new set.
 * The block of cells it replaces is aligned with the struck face: cells left solid either keep the shell (the outer
 * layer and a darkened, torn rim) or become flesh lining where they face the opening; hidden cells are dropped. */
export function carve(solids:Solid[],s:CarveSpec):Solid[]{
 const shape=shapeOf(s),{g}=shape;let {n,m}=shape;
 while((2*n+1)*(2*n+1)*m>9000&&n>2)n--;m=Math.min(m,24);
 const ax=s.axis,ua=(ax+1)%3,va=(ax+2)%3,sign=s.sign,face=s.hit.getComponent(ax),hu=s.hit.getComponent(ua),hv=s.hit.getComponent(va);
 // Solids as flat arrays in face coordinates (u,v along the face, w along the axis) for an allocation-free inner loop.
 const count=solids.length,lo=new Float64Array(count*3),hi=new Float64Array(count*3);
 solids.forEach((b,i)=>{for(const [o,a] of [[0,ua],[1,va],[2,ax]] as const){lo[i*3+o]=b.min.getComponent(a);hi[i*3+o]=b.max.getComponent(a);}});
 const order=[...solids.keys()].sort((a,b)=>Number(solids[b].flesh)-Number(solids[a].flesh));
 const solidAt=(u:number,v:number,w:number)=>{for(const i of order){const o=i*3;if(u>lo[o]&&u<hi[o]&&v>lo[o+1]&&v<hi[o+1]&&w>lo[o+2]&&w<hi[o+2])return i;}return -1;};
 const size=2*n+1,layer=size*size,cells=layer*m,source=new Int32Array(cells),gone=new Uint8Array(cells),burnt=new Uint8Array(cells);
 const wOf=(k:number)=>face-sign*(k+.5)*g,id=(i:number,j:number,k:number)=>k*layer+(j+n)*size+(i+n);
 let deepest=0;
 for(let k=0;k<m;k++)for(let j=-n;j<=n;j++)for(let i=-n;i<=n;i++){const c=id(i,j,k),a=i*g,b=j*g,d=(k+.5)*g;const src=solidAt(hu+a,hv+b,wOf(k));source[c]=src;if(src<0)continue;
  if(shape.removed(a,b,d,i,j,k)){gone[c]=1;deepest=Math.max(deepest,k+1);}else if(shape.scorched?.(a,b,d,i,j,k))burnt[c]=1;}
 // The replaced block, from just outside the face (proud plates over the hole go with it) to its last layer.
 const regionMin=v3(),regionMax=v3();
 regionMin.setComponent(ua,hu-(n+.5)*g).setComponent(va,hv-(n+.5)*g).setComponent(ax,Math.min(face+sign*.03,face-sign*m*g));
 regionMax.setComponent(ua,hu+(n+.5)*g).setComponent(va,hv+(n+.5)*g).setComponent(ax,Math.max(face+sign*.03,face-sign*m*g));
 const region={min:regionMin,max:regionMax},inRegion=(p:V3)=>p.x>regionMin.x&&p.x<regionMax.x&&p.y>regionMin.y&&p.y<regionMax.y&&p.z>regionMin.z&&p.z<regionMax.z;
 const out:Solid[]=[];for(const b of solids){if(b.flesh){if(!inRegion(b.min.clone().add(b.max).multiplyScalar(.5)))out.push(b);}else out.push(...subtract(b,region));}
 const cut=s.kind==='cut',half=g/2,rows=new Map<string,Solid>();
 // Narrow openings (punctures, stab slots) stay bright red down their walls; only the very bottom of a puncture goes dark.
 const narrow=s.kind==='puncture'||s.trauma==='pierce',fleshAt=(k:number)=>{const f=k/Math.max(1,deepest-1);return linear(cut?(f<.5?WOUND_COLORS.cut:WOUND_COLORS.cutDeep):narrow?(f<.5?WOUND_COLORS.shallow:WOUND_COLORS.mid):f<.34?WOUND_COLORS.shallow:f<.67?WOUND_COLORS.mid:WOUND_COLORS.deep);};
 const box=(i:number,j:number,k:number,lift=0)=>{const min=v3(),max=v3(),w=wOf(k)+sign*lift;min.setComponent(ua,hu+i*g-half).setComponent(va,hv+j*g-half).setComponent(ax,w-half);max.setComponent(ua,hu+i*g+half).setComponent(va,hv+j*g+half).setComponent(ax,w+half);return {min,max};};
 const NB=[[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]] as const;
 for(let k=0;k<m;k++)for(let j=-n;j<=n;j++)for(let i=-n;i<=n;i++){const c=id(i,j,k),src=source[c];if(src<0||gone[c])continue;
  let exposed=false,surface=k===0;
  for(const [di,dj,dk] of NB){const ii=i+di,jj=j+dj,kk=k+dk;
   if(ii<-n||ii>n||jj<-n||jj>n||kk<0||kk>=m){if(!surface&&solidAt(hu+ii*g,hv+jj*g,wOf(kk))<0)surface=true;continue;}
   const nb=id(ii,jj,kk);if(gone[nb])exposed=true;else if(source[nb]<0)surface=true;}
  if(!exposed&&!surface&&!burnt[c])continue;
  const from=solids[src],hash=woundHash(s.seed,i,j,k),tint=.86+.28*hash;
  if(from.flesh){out.push({...box(i,j,k),color:from.color,flesh:true});continue;}
  if(burnt[c]){out.push({...box(i,j,k),color:hash<.06&&!s.low?linear(WOUND_COLORS.ember):linear(hash<.55?WOUND_COLORS.char:WOUND_COLORS.soot,tint),flesh:true});continue;}
  if(exposed&&k>0){
   if(s.kind==='bruise'){const b=s.body,dark=linear(WOUND_COLORS.bruise);out.push({...box(i,j,k),color:hash<.18?linear(WOUND_COLORS.mid):[b[0]*.275+dark[0]*.45,b[1]*.275+dark[1]*.45,b[2]*.275+dark[2]*.45],flesh:true});continue;}
   const core=s.kind==='puncture'&&k>=deepest-1,col=core?linear(WOUND_COLORS.core):fleshAt(k);out.push({...box(i,j,k),color:[col[0]*tint,col[1]*tint,col[2]*tint],flesh:true});continue;}
  // Shell that remains. The rim of an opening is darkened (soot, torn edge); a cavity's rim is lifted into petals.
  const rim=exposed&&k===0,shade=from.color[0]*(rim?(cut?.82:.68):1),lift=rim&&s.kind==='cavity'&&!s.low?(.15+.4*hash)*g:0;
  // A bullet or blade opening leaves a bloody torn edge (the shell shade pulled toward red); bruises and burns do not.
  if(rim&&s.kind!=='bruise'&&s.kind!=='scorch'){const red=linear(WOUND_COLORS.shallow),mix=s.kind==='cut'?.35:.5;out.push({...box(i,j,k,lift),color:[shade*(1-mix)+red[0]*mix,shade*(1-mix)+red[1]*mix,shade*(1-mix)+red[2]*mix],flesh:true});continue;}
  const solid:Solid={...box(i,j,k,lift),color:[shade,shade,shade],flesh:false};
  // Merge plain shell cells along the face into rows (fewer instances); rims stay single.
  if(!rim){const key=`${j}|${k}|${shade.toFixed(4)}`,prev=rows.get(key);if(prev&&Math.abs(prev.max.getComponent(ua)-solid.min.getComponent(ua))<1e-6){prev.max.setComponent(ua,solid.max.getComponent(ua));continue;}rows.set(key,solid);}
  out.push(solid);
 }
 return out;
}

/** The part's sculpt as solids in metres (unit-space layout boxes times the part size). */
export function solidsOf(layout:VoxelLayout,size:V3):Solid[]{
 return layoutBoxes(layout).map(({box:[x,y,z,w,h,d],color})=>{const c=v3(x*size.x,y*size.y,z*size.z),e=v3(Math.abs(w*size.x)/2,Math.abs(h*size.y)/2,Math.abs(d*size.z)/2);return {min:c.clone().sub(e),max:c.clone().add(e),color:[color[0],color[1],color[2]] as [number,number,number],flesh:false};});
}
const toUnit=(list:Solid[],size:V3):UnitBox[]=>list.map(b=>{const c=b.min.clone().add(b.max).multiplyScalar(.5),e=b.max.clone().sub(b.min);return {box:[c.x/size.x,c.y/size.y,c.z/size.z,e.x/size.x,e.y/size.y,e.z/size.z],color:b.color};});
export interface CarvedPart {shell:VoxelLayout;cells:VoxelLayout;cellCount:number}
/** Carves every wound on one part. The low-detail (plain box) variant is carved only if low detail or a see-through
 * material ever asks for it, so a new wound costs one carve (about a millisecond) in the frame it appears. */
export function carvePart(key:string,base:VoxelLayout,size:V3,specs:CarveSpec[]):CarvedPart{
 const run=(start:Solid[])=>{let solids=start;for(const s of specs)solids=carve(solids,s);return {shell:solids.filter(b=>!b.flesh),flesh:solids.filter(b=>b.flesh)};};
 const sculpt=run(solidsOf(base,size));let plainRun:{shell:Solid[];flesh:Solid[]}|undefined,plainShell:VoxelLayout|undefined,plainCells:VoxelLayout|undefined;
 const plain=()=>plainRun??=run([{min:size.clone().multiplyScalar(-.5),max:size.clone().multiplyScalar(.5),color:[1,1,1],flesh:false}]);
 const shell=customLayout(key,toUnit(sculpt.shell,size)),cells=customLayout(key+'|cells',toUnit(sculpt.flesh,size),{ghost:EMPTY_LAYOUT});
 const shellPlain=()=>plainShell??=customLayout(key+'|plain',toUnit(plain().shell,size)),cellsPlain=()=>plainCells??=customLayout(key+'|cells|plain',toUnit(plain().flesh,size));
 Object.defineProperty(shell,'plain',{get:shellPlain,enumerable:true});Object.defineProperty(shell,'ghost',{get:shellPlain,enumerable:true});Object.defineProperty(cells,'plain',{get:cellsPlain,enumerable:true});
 return {shell,cells,cellCount:sculpt.flesh.length};
}

// ---------------------------------------------------------------------------------------------------------------
// Fresh-wound spray: one pooled instanced draw for every player. Droplets fall, land as flat splats and fade.

type Drop={pos:V3;vel:V3;life:number;max:number;size:number;ground:number;landed:boolean;color:T.Color};
export type SprayKind='blood'|'chips'|'embers';
export class WoundSpray {
 readonly mesh:T.InstancedMesh;private drops:Drop[]=[];private cursor=0;private dummy=new T.Object3D();private color=new T.Color();
 constructor(scene:T.Scene,readonly capacity=160){
  this.mesh=new T.InstancedMesh(new T.BoxGeometry(1,1,1),new WoundMaterial(),capacity);this.mesh.name='wound-spray';this.mesh.frustumCulled=false;this.mesh.count=0;
  for(let i=0;i<capacity;i++){this.drops.push({pos:v3(),vel:v3(),life:0,max:1,size:0,ground:0,landed:false,color:new T.Color()});this.mesh.setColorAt(i,this.color.set(0xffffff));}
  scene.add(this.mesh);
 }
 /** count droplets from `origin`, mostly along `normal` (out of the wound) with some along `along` (the shot). */
 emit(origin:V3,normal:V3,along:V3,count:number,kind:SprayKind,ground:number,seed:number,tint?:T.Color){
  for(let n=0;n<count;n++){const d=this.drops[this.cursor++%this.capacity],r=(k:number)=>woundHash(seed,n,k)-.5;
   d.pos.copy(origin);d.landed=false;d.ground=ground;
   const speed=kind==='embers'?.8+woundHash(seed,n,3):1.6+2.4*woundHash(seed,n,4);
   d.vel.copy(normal).multiplyScalar(speed).addScaledVector(along,.6*speed*woundHash(seed,n,5)).add(v3(r(0)*1.6,(kind==='embers'?1.2:.9)+r(1),r(2)*1.6));
   d.life=d.max=kind==='embers'?.5+.4*woundHash(seed,n,6):kind==='chips'?.9:1.4+.8*woundHash(seed,n,6);d.size=kind==='chips'?.022+.02*woundHash(seed,n,7):.016+.022*woundHash(seed,n,7);
   if(kind==='chips')d.color.copy(tint||this.color.set(0xb9c2c6)).multiplyScalar(.7+.4*woundHash(seed,n,8));else if(kind==='embers')d.color.set(woundHash(seed,n,8)<.5?0xff7a2a:0x2a2420);else d.color.set(woundHash(seed,n,8)<.6?WOUND_COLORS.shallow:WOUND_COLORS.mid);}
 }
 update(dt:number){
  const step=Math.min(.05,Math.max(0,dt));let live=0;
  for(let i=0;i<this.capacity;i++){const d=this.drops[i];if(d.life<=0)continue;d.life-=step;
   if(!d.landed){d.vel.y-=15*step;d.pos.addScaledVector(d.vel,step);if(d.pos.y<=d.ground+.006){d.pos.y=d.ground+.006;d.landed=true;}}
   const fade=clamp(d.life/Math.min(.5,d.max));this.dummy.position.copy(d.pos);
   if(d.landed)this.dummy.scale.set(d.size*2.2*fade,.004,d.size*2*fade);else this.dummy.scale.set(d.size,d.size*(1+Math.min(2.5,d.vel.length()*.15)),d.size).multiplyScalar(fade);
   this.dummy.rotation.set(0,i*1.7,0);this.dummy.updateMatrix();this.mesh.setMatrixAt(live,this.dummy.matrix);this.mesh.setColorAt(live,d.color);live++;}
  this.mesh.count=live;this.mesh.instanceMatrix.needsUpdate=true;if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;
 }
 clear(){for(const d of this.drops)d.life=0;this.mesh.count=0;}
 dispose(){this.mesh.removeFromParent();this.mesh.geometry.dispose();(this.mesh.material as T.Material).dispose();this.mesh.dispose();}
}

// ---------------------------------------------------------------------------------------------------------------
// The live layer: reconciles each rig with its player's snapshot wounds every frame.

/** Flesh material: wound colours live in the instance colours, independent of the body colour. The rig shell is softly
 * self-lit in the arena (armor glow), so flesh also emits a share of its own colour; otherwise every opening reads as a
 * black hole under the match lighting. A subclass so clones (block-cooldown tint, styled ragdolls) keep the shader edit. */
export class WoundMaterial extends T.MeshLambertMaterial{
 constructor(readonly glow=.3){super({color:0xffffff});this.onBeforeCompile=shader=>{shader.fragmentShader=shader.fragmentShader.replace('#include <emissivemap_fragment>',`#include <emissivemap_fragment>
#ifdef USE_COLOR
	totalEmissiveRadiance += vColor.rgb * ${this.glow.toFixed(3)};
#endif`);};}
 override customProgramCacheKey(){return 'body-wound-'+this.glow;}
 override clone():this{return new (this.constructor as new(glow:number)=>this)(this.glow).copy(this);}
}
/** One shared flesh material for wound cells, caps and spray. */
export const WOUND_MATERIAL=new WoundMaterial();
/** The unit cube again, but not typed BoxGeometry: consumers that read every rig box as one body block (voxel burst,
 * afterimage poses) skip the flesh cells, which sit inside a part that is already counted. */
export const WOUND_GEOMETRY=new T.BufferGeometry().copy(new T.BoxGeometry(1,1,1));WOUND_GEOMETRY.name='wound-cells';
interface Applied {wound:BodyWound;anchors:WoundAnchor[]}
interface PartWounds {mesh:T.InstancedMesh;keys:Set<string>;cells?:VoxelPart;dirty:boolean}
interface RigWounds {group:T.Group;applied:Map<string,Applied>;parts:Map<T.InstancedMesh,PartWounds>;low:boolean;body:[number,number,number]}
/** Stable identity across snapshots and rematches (ids restart in a new simulation, the authority time does not repeat). */
export const woundKey=(w:BodyWound)=>`${w.id}@${w.at}`;
const usable=(w:BodyWound)=>!w.destroyedPart&&Number.isFinite(w.point.x+w.point.y+w.point.z+w.radius);
function findMesh(group:T.Object3D,root:string,name:string){const joint=group.getObjectByName(root);const m=joint?.getObjectByName(name);return m instanceof VoxelPart?m:undefined;}

export class BodyWounds {
 readonly spray:WoundSpray;private rigs=new Map<T.Group,RigWounds>();
 constructor(scene:T.Scene){this.spray=new WoundSpray(scene);}
 /** Call once per frame after the rigs are posed and before death effects copy them (wounds must ride along). */
 update(state:Snapshot,rigs:ReadonlyMap<string,{group:T.Group}>,options:{quality:number;enabled?:boolean;dt:number;firstPerson?:{group:T.Group;playerId:string};hideSpray?:string}){
  const seen=new Set<T.Group>(),enabled=options.enabled!==false,low=options.quality<.5;
  const apply=(group:T.Group,p:PlayerView,spray:boolean)=>{seen.add(group);const wounds=enabled&&p.spec!=='siren'?(p.wounds||[]).filter(usable):[];
   let rw=this.rigs.get(group);if(!rw){if(!wounds.length)return;rw={group,applied:new Map(),parts:new Map(),low,body:bodyColor(p)};this.rigs.set(group,rw);}
   this.reconcile(rw,wounds,low,state.time,spray?p:undefined);};
  for(const p of state.players){const r=rigs.get(p.id);if(r)apply(r.group,p,p.id!==options.hideSpray);}
  const first=options.firstPerson,self=first&&state.players.find(p=>p.id===first.playerId);if(first&&self)apply(first.group,self,false);
  // Rigs that left the scene (player gone, rig rebuilt): restore and free their buffers.
  for(const [group,rw] of this.rigs)if(!seen.has(group)){this.restore(rw);this.rigs.delete(group);}
  this.spray.update(options.dt);
 }
 private reconcile(rw:RigWounds,wounds:BodyWound[],low:boolean,time:number,sprayFor?:PlayerView){
  if(rw.low!==low){rw.low=low;for(const part of rw.parts.values())part.dirty=true;}
  const desired=new Map(wounds.map(w=>[woundKey(w),w]));
  for(const [key,a] of rw.applied)if(!desired.has(key)){for(const part of rw.parts.values())if(part.keys.delete(key))part.dirty=true;rw.applied.delete(key);}
  const fresh:Applied[]=[];
  for(const [key,w] of desired){if(rw.applied.has(key))continue;const anchors=woundAnchors(w),a={wound:w,anchors};rw.applied.set(key,a);
   for(const anchor of anchors){const mesh=findMesh(rw.group,anchor.root,anchor.mesh);if(!mesh)continue;let part=rw.parts.get(mesh);if(!part){part={mesh,keys:new Set(),dirty:true};rw.parts.set(mesh,part);}part.keys.add(key);part.dirty=true;}
   if(time-w.at<.3&&time>=w.at-.05)fresh.push(a);}
  for(const part of rw.parts.values())if(part.dirty)this.rebuild(rw,part);
  for(const [mesh,part] of rw.parts)if(!part.keys.size)rw.parts.delete(mesh);
  if(sprayFor&&fresh.length)this.splash(rw,fresh,sprayFor,low);
 }
 private rebuild(rw:RigWounds,part:PartWounds){
  part.dirty=false;const mesh=part.mesh,old=part.cells;
  if(!part.keys.size){releaseVoxelLayout(mesh);retagVoxelPart(mesh);if(old){old.removeFromParent();old.dispose();part.cells=undefined;}return;}
  const base=baseLayout(mesh);if(!base)return;
  const list=[...part.keys].map(k=>rw.applied.get(k)!).filter(Boolean).sort((a,b)=>a.wound.at-b.wound.at||(a.wound.id<b.wound.id?-1:1));
  const specs:CarveSpec[]=[];let size:V3|undefined;
  for(const a of list)for(const anchor of a.anchors){if(anchor.mesh!==mesh.name)continue;size=anchor.size;specs.push(specFor(a.wound,anchor,rw.low,rw.body));}
  if(!specs.length||!size)return;
  const carved=carvePart(`wound|${mesh.name}|${[...part.keys].join(',')}`,base,size,specs);
  releaseVoxelLayout(mesh);retagVoxelPart(mesh,carved.shell);
  const cells=voxelMesh(WOUND_GEOMETRY,WOUND_MATERIAL,carved.cells) as VoxelPart;cells.name='wound-cells';cells.userData.bodyWound=true;cells.castShadow=false;cells.receiveShadow=true;setVoxelDetail(cells,!!mesh.userData.voxelLow);
  if(old){old.removeFromParent();old.dispose();}mesh.add(cells);part.cells=cells;
 }
 private splash(rw:RigWounds,fresh:Applied[],p:PlayerView,low:boolean){
  rw.group.updateMatrixWorld(true);const ground=(p.death&&p.hp<=0?p.death.pos.y:p.pos.y)-.85;
  for(const a of fresh){const anchor=a.anchors[0],mesh=anchor&&findMesh(rw.group,anchor.root,anchor.mesh);if(!mesh)continue;const w=a.wound;
   const origin=mesh.localToWorld(anchor.hit.clone().divide(anchor.size)),normal=unitAxis(anchor.axis,anchor.sign).transformDirection(mesh.matrixWorld),along=anchor.dir.clone().transformDirection(mesh.matrixWorld);
   const kind:SprayKind=w.kind==='bruise'?'chips':w.kind==='scorch'?'embers':'blood',base=w.kind==='cavity'?14:w.kind==='cut'?10:w.kind==='bruise'||w.kind==='scorch'?5:7;
   const count=Math.max(2,Math.round(base*(.6+.6*clamp(w.severity))*(low?.5:1)));this.spray.emit(origin,normal,along,count,kind,ground,stringSeed(w.id),new T.Color(rw.body[0],rw.body[1],rw.body[2]));}
 }
 private restore(rw:RigWounds){for(const part of rw.parts.values()){releaseVoxelLayout(part.mesh);retagVoxelPart(part.mesh);if(part.cells){part.cells.removeFromParent();part.cells.dispose();}}rw.parts.clear();rw.applied.clear();}
 /** Number of carved parts and flesh cells on a rig (tests, budgets). */
 stats(group:T.Group){const rw=this.rigs.get(group);let cells=0;for(const p of rw?.parts.values()||[])cells+=p.cells?.count||0;return {wounds:rw?.applied.size||0,parts:rw?.parts.size||0,cells};}
 clear(){for(const rw of this.rigs.values())this.restore(rw);this.rigs.clear();this.spray.clear();}
 dispose(){this.clear();this.spray.dispose();}
}
function bodyColor(p:PlayerView):[number,number,number]{const c=new T.Color(p.cosmetics?.bodyColor||0xdce5e8);return [c.r,c.g,c.b];}
export function specFor(w:BodyWound,a:WoundAnchor,low:boolean,body:[number,number,number]):CarveSpec{
 return {hit:a.hit.clone(),axis:a.axis,sign:a.sign,dir:a.dir.clone(),size:a.size.clone(),kind:w.kind,trauma:w.trauma,radius:w.radius,severity:w.severity,seed:stringSeed(w.id),low,body,vertical:w.direction.y};
}
