import * as T from 'three';
import type {Snapshot} from '../../shared/simulation.js';
import {bossSongOf} from '../../shared/siren-contract.js';
import {BLACK_SIREN_PROTOCOL as TRACK,barAt,loopedSongTime,sectionAt,type BossSectionId} from '../../shared/boss-music.js';
import {SIREN_HALL as H,SIREN_COLUMNS,SIREN_ADD_SPAWNS,SIREN_CHIP_SPOTS,SIREN_VENTS,sirenStageHazards} from '../../shared/siren-stage-layout.js';
import {FlameField} from './flames.js';
import {PALETTE,glowMaterial,haloMaterial,decalMaterial,tickSirenClock} from './materials.js';
import {pveOf,PVE_TIMING,type EntryKind,type PveEntryView} from '../../shared/pve/contract.js';
import {PVE_ENTRIES,machinesPresent} from '../../shared/pve/layout.js';
import {ENTRY_CLOSE,entryStart,entryOpenAmount,VENT_PIT,wardVentDrawn,wardVentWarning} from '../../shared/pve/vent-pits.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const mix=(a:number,b:number,t:number)=>a+(b-a)*t;
const unit=new T.BoxGeometry(1,1,1),dummy=new T.Object3D(),col=new T.Color(),UP=new T.Vector3(0,1,0),ZERO=new T.Matrix4().makeScale(0,0,0);
const NIGHT_LIGHTS=new Set(['night-ambient','night-sky','night-moon','night-rim']);
/** Inside the sealed hall: no sky, a close violet haze, and the arena key light hung almost straight down from the vault
 * (so the 16 m walls do not throw half the floor into shadow). MapScenery applies this for map 'siren'. */
export const WARD_ZERO_AIR={background:0x050407,fog:0x0d0a12,near:30,far:118,key:[-3,30,-5] as [number,number,number]};
/** Gallery floor height, shared by the balconies and their audience. */
const BALCONY=9.4;

/** How each section of the song dresses the ward. Values blend over about a bar when the section changes. */
interface Mood {tubes:number;strobe:number;erratic:number;beacons:number;spin:number;grins:number;eyes:number;circle:number;red:number;night:number;organ:number;crowd:number;sway:number;thump:number;fan:number}
const MOODS:Record<BossSectionId,Mood>={
 build:   {tubes:.45,strobe:0, erratic:.8,beacons:.15,spin:.5, grins:.1,eyes:0, circle:.25,red:0,  night:.5, organ:.2, crowd:.1, sway:.25,thump:0,  fan:.3},
 groove:  {tubes:.9, strobe:0, erratic:.1,beacons:.45,spin:.9, grins:.35,eyes:0, circle:.45,red:.1, night:.5, organ:.6, crowd:.5, sway:.5, thump:.15,fan:.7},
 lift:    {tubes:.95,strobe:.25,erratic:.1,beacons:1,  spin:3.2,grins:.8,eyes:0, circle:.75,red:.35,night:.45,organ:.9, crowd:.85,sway:.8, thump:.6, fan:1.4},
 breather:{tubes:.18,strobe:0, erratic:1, beacons:0,  spin:.3, grins:.2,eyes:1, circle:.3,red:0,  night:.38,organ:.12,crowd:0,  sway:.15,thump:0,  fan:.2},
 climax:  {tubes:1,  strobe:1, erratic:0, beacons:1,  spin:5.5,grins:1, eyes:1, circle:1,  red:.6, night:.42,organ:1,  crowd:1,  sway:1,  thump:1,  fan:2.4},
 outro:   {tubes:.5, strobe:0, erratic:.5,beacons:.2, spin:.6, grins:.3,eyes:.4,circle:.3,red:.1, night:.5, organ:.35,crowd:.3, sway:.4, thump:.1, fan:.5},
};
const KEYS=Object.keys(MOODS.build) as (keyof Mood)[];

/** PvE entries (design §8.4). Ward doors stand in a shallow iron vestibule: the closed leaves sit DOOR_LEAF_D in front of
 *  the wall face, so an add waiting at its walk start (PVE_ENTRIES door `from`, 0.7 m from the wall) is hidden behind
 *  them until they swing into the hall. Hinges are DOOR_HINGE either side of the door centre. */
export const DOOR_LEAF_D=1.2,DOOR_HINGE=1.48,DOOR_LEAF_W=1.42,DOOR_SWING=1.75,VENT_HINGE=-1.12,VENT_SWING=1.95,CELL_STUB=.45/3.45;
// Entry open amounts are shared with the authority (pve/vent-pits.ts decides vent falls from the same curve).
export {ENTRY_CLOSE,entryStart,entryOpenAmount};

/** The open vent's pit: the floor quad over the hole renders a shaft below it (per-pixel ray into a box VENT_PIT.hole wide
 *  and PIT_DEPTH deep): dark walls with seams and rungs fading to black with depth, a black bottom, a faint lip glow.
 *  Drawn over the floor (polygon offset) so the tiles never show through; it fades in with the lid (fully dark from the
 *  authority's lethal open amount) and out as the lid shuts. Players falling in sink below the floor into it. */
const PIT_DEPTH=7,PIT_GEO=new T.PlaneGeometry(VENT_PIT.hole,VENT_PIT.hole).rotateX(-Math.PI/2);
export function ventShaftMaterial(){
 return new T.ShaderMaterial({transparent:true,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-3,polygonOffsetUnits:-6,toneMapped:true,
  uniforms:{uOpen:{value:0},uHalf:{value:VENT_PIT.hole/2},uDepth:{value:PIT_DEPTH},uLethal:{value:VENT_PIT.lethal}},
  vertexShader:`varying vec3 vLocal;varying vec3 vWorld;
void main(){vLocal=position;vec4 w=modelMatrix*vec4(position,1.);vWorld=w.xyz;gl_Position=projectionMatrix*viewMatrix*w;}`,
  fragmentShader:`uniform float uOpen,uHalf,uDepth,uLethal;varying vec3 vLocal;varying vec3 vWorld;
void main(){
 vec3 d=normalize(vWorld-cameraPosition),p=vec3(vLocal.x,0.,vLocal.z);
 float tx=abs(d.x)>1e-5?(sign(d.x)*uHalf-p.x)/d.x:1e9,tz=abs(d.z)>1e-5?(sign(d.z)*uHalf-p.z)/d.z:1e9,ty=d.y<-1e-5?-uDepth/d.y:1e9;
 float t=min(min(tx,tz),ty);vec3 h=p+d*max(t,0.);float down=-h.y;vec3 col=vec3(0.);
 if(t<ty){
  float side=tx<tz?0.:1.,u=tx<tz?h.z:h.x;
  vec3 base=mix(vec3(.13,.115,.16),vec3(.085,.075,.11),side);
  float seam=1.-smoothstep(.0,.03,abs(fract(down*1.6)-.5)-.44),panel=1.-smoothstep(.0,.025,abs(fract(u*1.15+.5)-.5)-.46);
  float rung=(1.-smoothstep(.0,.02,abs(u)-.32))*(1.-smoothstep(.0,.025,abs(fract(down*2.4)-.5)-.45))*step(.15,down)*side;
  col=base*(1.-.45*max(seam,panel))+vec3(.07,.065,.075)*rung;
  col*=exp(-down*.95);
  col+=vec3(.32,.05,.08)*exp(-down*7.)*.35;
 }
 float a=smoothstep(0.,uLethal,uOpen);
 gl_FragColor=vec4(col,a);
 #include <tonemapping_fragment>
 #include <colorspace_fragment>
}`});
}

/** The four walls as frames: u runs along the wall, d is the distance into the hall, and the frame's yaw faces the hall.
 * Dressing on a wall is grouped by side so a camera outside the hall (the menu overview) sees a cut-away, not a box. */
type Side='n'|'s'|'e'|'w';type Tag=Side|'v'|''|'door'|'pads';
const SIDES:Record<Side,{x:number;z:number;ry:number;half:number}>={
 n:{x:0,z:-H.halfZ,ry:0,half:H.halfX},s:{x:0,z:H.halfZ,ry:Math.PI,half:H.halfX},
 w:{x:-H.halfX,z:0,ry:Math.PI/2,half:H.halfZ},e:{x:H.halfX,z:0,ry:-Math.PI/2,half:H.halfZ},
};
const SIDE_KEYS=Object.keys(SIDES) as Side[];
function onWall(s:Side,u:number,y:number,d:number,sx:number,sy:number,sz:number,rz=0){const f=SIDES[s];dummy.position.set(u,y,d).applyAxisAngle(UP,f.ry);dummy.position.x+=f.x;dummy.position.z+=f.z;dummy.rotation.set(0,f.ry,rz);dummy.scale.set(sx,sy,sz);dummy.updateMatrix();return dummy.matrix;}
function at(x:number,y:number,z:number,sx:number,sy:number,sz:number,rx=0,ry=0,rz=0){dummy.position.set(x,y,z);dummy.rotation.set(rx,ry,rz);dummy.scale.set(sx,sy,sz);dummy.updateMatrix();return dummy.matrix;}
/** Where a world point sits in a wall's frame. */
const local=(s:Side,x:number,z:number)=>s==='n'?{u:x,d:z+H.halfZ}:s==='s'?{u:-x,d:H.halfZ-z}:s==='w'?{u:-z,d:x+H.halfX}:{u:z,d:H.halfX-x};
/** Along-wall positions of the shared columns standing against a wall (corners included), sorted. */
const columnsOn=(s:Side)=>SIREN_COLUMNS.map(c=>local(s,c.x,c.z)).filter(p=>Math.abs(p.d-1.4)<.05).map(p=>p.u).sort((a,b)=>a-b);
const place=<O extends T.Object3D>(o:O,s:Side,u:number,y:number,d:number):O=>{const f=SIDES[s];o.position.set(u,y,d).applyAxisAngle(UP,f.ry);o.position.x+=f.x;o.position.z+=f.z;o.rotation.set(0,f.ry,0);return o;};

/** A large painted grin (the Siren's mark), as flat triangles in the XY plane facing +z. */
function grinGeometry(w:number,h:number,teeth=13){
 const up=(x:number)=>h*.55+h*.9*(x/w)**2,lo=(x:number)=>-h*.45+h*1.5*(x/w)**2,pos:number[]=[],step=2*w/teeth;
 for(let i=0;i<teeth;i++){const a=-w+i*step,b=a+step,m=(a+b)/2;pos.push(a,up(a),0,m,lo(m)+(up(m)-lo(m))*.18,0,b,up(b),0);}
 for(let i=0;i<teeth-1;i++){const a=-w+(i+.5)*step,b=a+step,m=(a+b)/2;pos.push(a,lo(a),0,b,lo(b),0,m,up(m)-(up(m)-lo(m))*.18,0);}
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setAttribute('uv',new T.Float32BufferAttribute(new Array(pos.length/3*2).fill(0),2));g.computeVertexNormals();return g;
}
/** A round-headed arch of unit width, base at y=0, total height k (straight sides of k-.5 and a half-circle head). */
function archGeometry(k:number){const s=new T.Shape();s.moveTo(-.5,0);s.lineTo(-.5,k-.5);s.absarc(0,k-.5,.5,Math.PI,0,true);s.lineTo(.5,0);s.closePath();return new T.ShapeGeometry(s,16);}
const inArch=(x:number,y:number,w:number,k:number,pad:number)=>Math.abs(x)<=w/2-pad&&y>=pad&&(y<=(k-.5)*w||Math.hypot(x,y-(k-.5)*w)<=w/2-pad);

/** Static parts batched per group, material and shape: the hall is a few thousand parts but only a few dozen draws. */
class Kit {
 private parts=new Map<string,{tag:Tag;material:T.Material;geometry:T.BufferGeometry;list:T.Matrix4[]}>();
 add(tag:Tag,material:T.Material,matrix:T.Matrix4,geometry:T.BufferGeometry=unit){const key=tag+'|'+material.uuid+'|'+geometry.uuid;let p=this.parts.get(key);if(!p)this.parts.set(key,p={tag,material,geometry,list:[]});p.list.push(matrix.clone());}
 flush(groups:Record<Tag,T.Group>){for(const p of this.parts.values()){const m=new T.InstancedMesh(p.geometry,p.material,p.list.length);p.list.forEach((x,i)=>m.setMatrixAt(i,x));m.instanceMatrix.needsUpdate=true;m.receiveShadow=true;m.name='ward-kit';groups[p.tag].add(m);}this.parts.clear();}
}
/** Unlit instanced lamps (bulbs, glowing seams, eyes, organ strips) whose brightness is set every frame. Lamps tagged with a
 * cut-away wall collapse to nothing. */
class Lamps {
 readonly mesh:T.InstancedMesh;private base:T.Matrix4[]=[];private tint:T.Color[]=[];private tags:Tag[]=[];private cut='';
 constructor(parent:T.Object3D,private max:number,geometry:T.BufferGeometry=unit,name='ward-lamps',side:T.Side=T.FrontSide){
  this.mesh=new T.InstancedMesh(geometry,new T.MeshBasicMaterial({color:0xffffff,toneMapped:false,side}),max);this.mesh.instanceColor=new T.InstancedBufferAttribute(new Float32Array(max*3),3);
  this.mesh.count=0;this.mesh.frustumCulled=false;this.mesh.name=name;parent.add(this.mesh);
 }
 add(color:T.ColorRepresentation,matrix:T.Matrix4,tag:Tag=''){if(this.mesh.count>=this.max)return -1;const i=this.mesh.count++;this.base.push(matrix.clone());this.tint.push(new T.Color(color));this.tags.push(tag);this.mesh.setMatrixAt(i,matrix);this.mesh.setColorAt(i,col.setScalar(0));return i;}
 get size(){return this.mesh.count;}
 set(i:number,level:number,tint?:T.Color){if(i>=0)this.mesh.setColorAt(i,col.copy(tint??this.tint[i]).multiplyScalar(Math.max(0,level)));}
 move(i:number,matrix:T.Matrix4){if(i>=0)this.base[i].copy(matrix);}
 commit(cut:Record<Tag,boolean>,key:string,moved=false){
  if(moved||key!==this.cut){this.cut=key;for(let i=0;i<this.mesh.count;i++)this.mesh.setMatrixAt(i,cut[this.tags[i]]?ZERO:this.base[i]);this.mesh.instanceMatrix.needsUpdate=true;}
  if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;
 }
}
/** Deterministic pseudo-random numbers, so every client builds the same hall. */
function rng(seed:number){return ()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};}

/**
 * Ward Zero: the sealed great hall of a condemned asylum that the Siren has turned into her concert hall. Glazed tile and
 * bricked-up arches below, galleries of silhouetted patients above, a pipe organ under her giant face and rose window on
 * the north wall, chained great doors on the south wall that something keeps pounding on, ward doors and isolation cells
 * for the adds, chip pads on the running loop, and a vault of ribs, a slow exhaust fan, chandeliers hung with teeth,
 * failing work lights and speaker stacks. Everything moves with song time (the boss track's detected beats and
 * sections), so it pulses with the music and is identical for every viewer; the vents read the same schedule the
 * simulation damages with.
 */
export class WardZeroVisuals {
 readonly group=new T.Group();private active=false;private saved=new Map<T.Light,number>();private mood:Mood={...MOODS.build};private last=0;private lastBeat=-1;
 private groups:Record<Tag,T.Group>;private cut:Record<Tag,boolean>={n:false,s:false,e:false,w:false,v:false,'':false,door:false,pads:false};private cutKey='';
 private mat={
  tile:new T.MeshLambertMaterial({color:0x48524d}),grout:new T.MeshLambertMaterial({color:0x22272a}),plaster:new T.MeshLambertMaterial({color:0x37323d}),dark:new T.MeshLambertMaterial({color:0x0b0a0e}),
  wood:new T.MeshLambertMaterial({color:0x33241f}),iron:new T.MeshLambertMaterial({color:0x1b191f}),brick:new T.MeshLambertMaterial({color:0x2c1e21}),pewter:new T.MeshLambertMaterial({color:0x6b6676}),
  stone:new T.MeshLambertMaterial({color:0x3f3a45}),pad:new T.MeshLambertMaterial({color:0x2c2833}),stripe:new T.MeshLambertMaterial({color:0x6b1520}),bone:new T.MeshLambertMaterial({color:0xcfc7bb}),
  figure:new T.MeshLambertMaterial({color:0x070609}),scratch:new T.MeshLambertMaterial({color:0x7a7389}),ink:new T.MeshLambertMaterial({color:0x8d8699}),
 };
 private tubes!:T.InstancedMesh;private tubeLevel:number[]=[];private tubeLights:T.PointLight[]=[];private organLight=new T.PointLight(0xe6dcff,0,36,1.3);
 private beacons:{head:T.Group;beam:T.Mesh;lamp:T.ShaderMaterial;spot?:T.SpotLight;phase:number;angle:number}[]=[];
 private grins:{mat:T.ShaderMaterial;big:boolean}[]=[];private eyes:{mesh:T.Mesh;mat:T.ShaderMaterial}[]=[];private eyeHalos:T.Sprite[]=[];private rose!:T.ShaderMaterial;
 private circle!:{ring:T.ShaderMaterial;tickMat:T.ShaderMaterial;runes:T.Group};
 private vents:{id:string;grate:T.Group;lid:T.Group;glow:T.ShaderMaterial;acc:number;burst:boolean;pit:T.Mesh;shaft:T.ShaderMaterial}[]=[];
 private leaves=new Map<string,T.Group[]>();private gates=new Map<string,T.Group>();private entryAcc=new Map<string,number>();
 private lamps!:Lamps;private cracks:number[]=[];private sconces:number[]=[];private pipes:{i:number;d:number}[]=[];private seams:number[]=[];private alarms=new Map<string,number>();private pads:number[]=[];private keyhole=-1;
 private crowd!:{bodies:T.InstancedMesh;heads:T.InstancedMesh;eyes:Lamps;list:{tag:Side;x:number;y:number;z:number;ry:number;h:number;phase:number}[]};
 private chandeliers:{pivot:T.Group;bulbs:Lamps;phase:number}[]=[];private fan!:{blades:T.Group;glow:T.ShaderMaterial;angle:number};private clocks:{minute:T.Group;hour:T.Group}[]=[];
 private speakers:{cones:T.InstancedMesh;rings:Lamps;spots:T.Vector3[]}[]=[];private doors!:T.Group;
 private scanned=-1e9;private fire=new FlameField(700);private redWash=new T.HemisphereLight(0xff2a3a,0x000000,0);private fill=new T.HemisphereLight(0x8d86a6,0x1a1720,0);private washes=[new T.DirectionalLight(0xa59bd0,0),new T.DirectionalLight(0x8f88b8,0)];
 constructor(private scene:T.Scene){
  this.group.name='ward-zero';this.group.visible=false;scene.add(this.group);this.group.add(this.fire.mesh,this.redWash,this.fill);
  // Two soft, shadowless washes from opposite low diagonals, so the sides of cover read under the overhead key light.
  this.washes[0].position.set(12,8,16);this.washes[1].position.set(-12,8,-16);this.group.add(...this.washes);
  const g=(name:string,parent:T.Object3D=this.group)=>{const x=new T.Group();x.name='ward-'+name;parent.add(x);return x;};
  const sides={n:g('north'),s:g('south'),e:g('east'),w:g('west')};this.doors=g('doors',sides.s);
  this.groups={...sides,v:g('vault'),'':this.group,door:this.doors,pads:g('pads')};
  this.lamps=new Lamps(this.group,900);const kit=new Kit();
  this.buildFloor(kit);this.buildWalls(kit);this.buildAltar(kit);this.buildGreatDoors(kit);this.buildWardDoors(kit);this.buildColumns(kit);this.buildCells(kit);this.buildVault(kit);
  this.buildCrowd();this.buildBeacons();this.buildGraffiti(kit);this.circle=this.buildCircle();this.buildVents(kit);
  kit.flush(this.groups);
 }

 // ---- construction ---------------------------------------------------------------------------------------------
 /** Worn terrazzo tiles with grime, the chip pads on the running loop and hazard stripes in front of every add spawn. */
 private buildFloor(kit:Kit){
  const N=256,px=new Uint8Array(N*N*4),r=rng(97);
  for(let y=0;y<N;y++)for(let x=0;x<N;x++){const checker=((x>>6)+(y>>6))%2,grout=x%64<2||y%64<2,v=(grout?24:checker?66:54)+(r()-.5)*10+Math.sin(x*.21+Math.sin(y*.13)*2)*3,i=(y*N+x)*4;px[i]=v;px[i+1]=v*.94;px[i+2]=v*1.07;px[i+3]=255;}
  for(let c=0;c<16;c++){let x=r()*N,y=r()*N,a=r()*6.28;for(let k=0;k<80;k++){a+=(r()-.5)*.8;x=(x+Math.cos(a)+N)%N;y=(y+Math.sin(a)+N)%N;const i=((y|0)*N+(x|0))*4;px[i]=px[i+1]=px[i+2]=16;}}
  const tex=new T.DataTexture(px,N,N,T.RGBAFormat);tex.wrapS=tex.wrapT=T.RepeatWrapping;tex.repeat.set(H.halfX/2,H.halfZ/2);tex.colorSpace=T.SRGBColorSpace;tex.magFilter=T.LinearFilter;tex.minFilter=T.LinearMipmapLinearFilter;tex.generateMipmaps=true;tex.needsUpdate=true;
  const floor=new T.Mesh(new T.PlaneGeometry(2*H.halfX,2*H.halfZ),new T.MeshLambertMaterial({map:tex,polygonOffset:true,polygonOffsetFactor:-1,polygonOffsetUnits:-2}));floor.rotation.x=-Math.PI/2;floor.position.y=.004;floor.receiveShadow=true;floor.name='ward-floor';this.group.add(floor);
  // Grime: stains heavier toward the walls.
  const stains=new T.InstancedMesh(new T.PlaneGeometry(1,1),decalMaterial(0x000000,.3),48);for(let i=0;i<48;i++){const edge=i%2===0,x=edge?(r()<.5?-1:1)*(26+r()*5):(r()-.5)*50,z=edge?(r()-.5)*50:(r()<.5?-1:1)*(20+r()*7),s=2+r()*5;stains.setMatrixAt(i,at(x,.009,z,s,s*(.6+r()*.8),1,-Math.PI/2,0,r()*6.28));}
  stains.instanceMatrix.needsUpdate=true;stains.name='ward-grime';this.group.add(stains);
  // Chip pads: hexagonal iron docks with a glow ring and a centre light (chips will spawn here; they idle on the off-beat).
  const hex=new T.CylinderGeometry(.95,.95,.05,6),ring=new T.RingGeometry(.74,.86,6);ring.rotateX(-Math.PI/2);
  // They are where the PvE chip machines and recruit pods stand, so they are drawn only when the machines are (machinesPresent).
  this.pads=SIREN_CHIP_SPOTS.map(c=>{kit.add('pads',this.mat.iron,at(c.x,.025,c.z,1,1,1),hex);return this.lamps.add(0xe8e2ff,at(c.x,.056,c.z,.22,.02,.22,0,Math.PI/4,0),'pads');});
  this.padRings=new Lamps(this.groups.pads,SIREN_CHIP_SPOTS.length,ring,'ward-pad-rings');for(const c of SIREN_CHIP_SPOTS)this.padRings.add(0xb9a8ff,at(c.x,.055,c.z,1,1,1));
  // Threshold stripes in front of every add spawn.
  for(const a of SIREN_ADD_SPAWNS){const f=new T.Vector3(Math.sin(a.yaw),0,Math.cos(a.yaw)),side=new T.Vector3(f.z,0,-f.x),cell=a.kind==='cell',back=cell?3:-2.1,w=cell?5:3;
   for(let i=-w;i<=w;i++){const p=new T.Vector3(a.x,0,a.z).addScaledVector(f,back).addScaledVector(side,i*.5);kit.add('',i%2?this.mat.stripe:this.mat.iron,at(p.x,.008,p.z,.42,.012,.9,0,a.yaw+.5,0));}}
 }
 private padRings!:Lamps;
 /** Tiled wainscot, plaster, bricked arches and blind oculi in every bay, galleries along the walls, a cornice under the vault. */
 private buildWalls(kit:Kit){
  const arch=archGeometry(1.25),disc=new T.CircleGeometry(.5,28),ring=new T.RingGeometry(.5,.62,28),r=rng(31),m=this.mat;
  for(const s of SIDE_KEYS){const f=SIDES[s],L=2*f.half,cols=columnsOn(s),long=s==='e'||s==='w';
   kit.add(s,m.tile,onWall(s,0,1.3,.03,L,2.6,.06));for(let u=-f.half+.25;u<f.half;u+=.5)kit.add(s,m.grout,onWall(s,u,1.3,.065,.03,2.6,.02));for(const y of [.65,1.3,1.95])kit.add(s,m.grout,onWall(s,0,y,.065,L,.03,.02));
   kit.add(s,m.iron,onWall(s,0,.12,.07,L,.24,.1));kit.add(s,m.wood,onWall(s,0,2.66,.09,L,.16,.16));kit.add(s,m.plaster,onWall(s,0,(2.74+H.roof)/2,.02,L,H.roof-2.74,.04));
   kit.add(s,m.iron,onWall(s,0,15.45,.3,L,.5,.6));for(let u=-f.half+.3;u<f.half;u+=.6)kit.add(s,m.iron,onWall(s,u,15.08,.18,.22,.2,.36));
   for(let i=0;i+1<cols.length;i++){const a=cols[i]+.7,b=cols[i+1]-.7,mid=(a+b)/2,w=b-a,centre=Math.abs(mid)<1,door=!long&&Math.abs(Math.abs(mid)-18)<1;
    // Lower register: bricked-up arches with boards across and red light leaking through the cracks.
    if(!centre&&!door){const n=w>10?2:1,aw=Math.min(4.2,w/n-1.4);for(let j=0;j<n;j++){const u=a+(j+.5)*w/n,y0=3.3;
     kit.add(s,m.stone,onWall(s,u,y0-.25,.04,aw+.5,aw+.5,1),arch);kit.add(s,m.dark,onWall(s,u,y0,.05,aw,aw,1),arch);
     for(let y=.2;y<1.25*aw-.15;y+=.38)for(let x=-aw/2+((y/.38|0)%2?.2:.6);x<aw/2;x+=.82)if(inArch(x,y,aw,1.25,.12))kit.add(s,m.brick,onWall(s,u+x,y0+y,.08,.76,.32,.06));
     for(const k of [-1,1])kit.add(s,m.wood,onWall(s,u,y0+aw*(.45+k*.12),.13,aw*1.04,.26,.06,k*.32));
     for(let c=0;c<3;c++)this.cracks.push(this.lamps.add(PALETTE.warn,onWall(s,u+(r()-.5)*aw*.7,y0+aw*(.25+r()*.7),.115,.05,.5+r()*.6,.02,(r()-.5)*.9),s));}}
    // Upper register: blind oculi (the north centre gets the rose window, the side centres a clock, the south centre the doors' arch).
    if(!centre){const n=w>10?2:1;for(let j=0;j<n;j++){const u=a+(j+.5)*w/n,y=12.8,size=1.9;kit.add(s,m.stone,onWall(s,u,y,.04,size,size,1),ring);kit.add(s,m.dark,onWall(s,u,y,.05,size,size,1),disc);
     this.cracks.push(this.lamps.add(PALETTE.warn,onWall(s,u+(r()-.5)*.5,y+(r()-.5)*.4,.07,.04,size*.55,.02,(r()-.5)*1.4),s));}}
   }
   // Galleries: a slab, fascia, rail and balusters along the walls (the north and south centre bays stay open for the face and the doors).
   const segs=long?[[-f.half,f.half]]:[[-f.half,-12.7],[12.7,f.half]];
   for(const [a,b] of segs){const len=b-a,c=(a+b)/2;kit.add(s,m.wood,onWall(s,c,BALCONY-.15,.65,len,.3,1.3));kit.add(s,m.wood,onWall(s,c,BALCONY-.45,1.28,len,.5,.08));kit.add(s,m.iron,onWall(s,c,BALCONY+1,1.22,len,.07,.1));
    for(let u=a+.2;u<b;u+=.42)kit.add(s,m.iron,onWall(s,u,BALCONY+.5,1.22,.05,1,.05));for(let u=a+2;u<b-1;u+=4)kit.add(s,m.iron,onWall(s,u,BALCONY-.8,.6,.16,.7,1.1));}
  }
 }
 /** The north wall: a pipe organ in two banks around the Siren's grin, her giant eyes above, and a rose window bleeding red. */
 private buildAltar(kit:Kit){
  const m=this.mat,pipe=new T.CylinderGeometry(.5,.5,1,10);
  kit.add('n',m.wood,onWall('n',0,1.5,.12,22.4,3,.24));for(let u=-10;u<=10;u+=2)kit.add('n',m.dark,onWall('n',u,1.5,.25,1.5,2.2,.02));kit.add('n',m.iron,onWall('n',0,3.04,.2,22.6,.12,.4));
  for(const sg of [-1,1])for(let i=0;i<12;i++){const u=sg*(4.8+i*.55),h=4.2+i*.62;kit.add('n',m.pewter,onWall('n',u,3+h/2,.5,.4,h,.4),pipe);kit.add('n',m.dark,onWall('n',u,3.75,.71,.22,.18,.03));
   this.pipes.push({i:this.lamps.add(PALETTE.ash,onWall('n',u,(4.05+3+h-.25)/2,.71,.06,3+h-.25-4.05,.02),'n'),d:i/11});}
  this.paint('n',0,5.7,.06,3.8,.72,true);
  const eye=new T.CircleGeometry(1,40);
  for(const sg of [-1,1]){const mat=glowMaterial(0xffffff,{intensity:1});const e=place(new T.Mesh(eye,mat),'n',sg*2.1,8.9,.07);e.scale.set(1,.02,1);e.rotation.z=sg*.12;this.groups.n.add(e);this.eyes.push({mesh:e,mat});
   const halo=place(new T.Sprite(haloMaterial(0xf6f2ff,0)),'n',sg*2.1,8.9,.5);halo.scale.setScalar(4.5);this.groups.n.add(halo);this.eyeHalos.push(halo);}
  // Rose window: an iron ring with tracery over a red glow.
  this.rose=glowMaterial(PALETTE.warn,{intensity:.2,flicker:.15});this.groups.n.add(place(new T.Mesh(new T.CircleGeometry(2.3,40),this.rose),'n',0,12.4,.04));
  const torus=new T.TorusGeometry(2.35,.16,8,48);kit.add('n',m.iron,onWall('n',0,12.4,.12,1,1,1),torus);kit.add('n',m.iron,onWall('n',0,12.4,.12,.45,.45,1),torus);
  for(let i=0;i<12;i++){const a=i/12*Math.PI*2;kit.add('n',m.iron,onWall('n',Math.sin(a)*1.4,12.4+Math.cos(a)*1.4,.12,.12,1.9,.1,-a));}
  this.organLight.position.set(0,7,-24.5);this.group.add(this.organLight);
 }
 /** The south wall: chained great doors with a grin smeared across them. Something on the other side pounds on the kicks. */
 private buildGreatDoors(kit:Kit){
  const m=this.mat,arch=archGeometry(1.375),link=(a:T.Vector2,b:T.Vector2,d:number,tag:Tag)=>{const L=a.distanceTo(b),n=Math.floor(L/.3),rz=Math.atan2(b.y-a.y,b.x-a.x);for(let i=0;i<n;i++){const t=(i+.5)/n;kit.add(tag,m.iron,onWall('s',mix(a.x,b.x,t),mix(a.y,b.y,t),d,.34,i%2?.05:.14,i%2?.14:.05,rz));}};
  kit.add('s',m.stone,onWall('s',0,-.3,.03,9.4,9.4,1),arch);kit.add('s',m.dark,onWall('s',0,0,.04,8,8,1),arch);
  const tymp=new T.CircleGeometry(4,32,0,Math.PI);kit.add('s',m.wood,onWall('s',0,7,.06,1,1,1),tymp);for(let i=1;i<8;i++){const a=i/8*Math.PI;kit.add('s',m.iron,onWall('s',Math.cos(a)*2,7+Math.sin(a)*2,.1,.12,4,.06,a-Math.PI/2));}
  for(const sg of [-1,1]){kit.add('door',m.wood,onWall('s',sg*2,3.5,.14,3.94,7,.2));for(const y of [1.2,3.2,5.2,6.6])kit.add('door',m.iron,onWall('s',sg*2,y,.27,3.8,.22,.06));kit.add('door',m.iron,onWall('s',sg*.18,3.5,.28,.12,6.8,.08));}
  link(new T.Vector2(-3.8,6.6),new T.Vector2(3.8,.9),.42,'door');link(new T.Vector2(3.8,6.6),new T.Vector2(-3.8,.9),.46,'door');
  kit.add('door',m.iron,onWall('s',0,3.7,.6,.72,.82,.3));kit.add('door',m.iron,onWall('s',0,4.3,.6,.5,.5,.5),new T.TorusGeometry(.42,.06,6,16,Math.PI));
  const key=glowMaterial(PALETTE.warn,{intensity:.6,flicker:.4});this.doors.add(place(new T.Mesh(new T.PlaneGeometry(.1,.26),key),'s',0,3.62,.76));this.keyMat=key;
  const grin=glowMaterial(PALETTE.teeth,{intensity:.15});this.doors.add(place(new T.Mesh(grinGeometry(3.2,.6,13),grin),'s',0,4.25,.3));this.grins.push({mat:grin,big:true});
 }
 private keyMat!:T.ShaderMaterial;
 /** Ward doors (add spawns) in the north and south walls: a shallow iron vestibule (jambs and a striped lintel standing
  *  DOOR_LEAF_D proud of the wall, dark inside) closed by two hinged steel leaves with barred windows, and an alarm lamp
  *  above. In PvE the leaves swing into the hall when an add walks out (design §8.4). */
 private buildWardDoors(kit:Kit){
  const m=this.mat,D=DOOR_LEAF_D,leafKit=new T.BoxGeometry(1,1,1),win=new T.MeshBasicMaterial({color:PALETTE.warn,toneMapped:false});
  const leafMat=new T.MeshLambertMaterial({color:0xffffff});
  for(const a of SIREN_ADD_SPAWNS){if(a.kind!=='door')continue;const s:Side=a.z<0?'n':'s',u=local(s,a.x,a.z).u;
   // The vestibule: dark lining (back, sides, ceiling), iron jambs and the lintel.
   kit.add(s,m.dark,onWall(s,u,2.1,.09,3.04,4.2,.02));for(const k of [-1,1])kit.add(s,m.dark,onWall(s,u+k*1.52,2.1,D/2,.04,4.2,D));kit.add(s,m.dark,onWall(s,u,4.22,D/2,3.04,.04,D));
   for(const k of [-1,1]){kit.add(s,m.iron,onWall(s,u+k*1.68,2.2,(D+.1)/2,.36,4.4,D+.1));for(const y of [.6,2.2,3.8])kit.add(s,m.pewter,onWall(s,u+k*1.68,y,D+.11,.4,.08,.03));}
   kit.add(s,m.iron,onWall(s,u,4.45,(D+.1)/2,3.8,.4,D+.1));for(let i=0;i<8;i++)kit.add(s,i%2?m.stripe:m.iron,onWall(s,u-1.6+i*.46,4.45,D+.11,.46,.32,.02,.4));
   this.alarms.set(a.id,this.lamps.add(PALETTE.warn,onWall(s,u,4.95,D+.06,.5,.26,.26),s));
   // Two leaves, each pivoting on its hinge line (local +z faces the hall).
   const pivots:T.Group[]=[];
   for(const k of [-1,1]){const frame=new T.Group(),pivot=new T.Group();pivot.name=`ward-${a.id}-leaf-${k<0?'l':'r'}`;place(frame,s,u+k*DOOR_HINGE,0,D);frame.add(pivot);this.groups[s].add(frame);
    const parts:[number,number,number,number,number,number,number][]=[[-k*DOOR_LEAF_W/2,2.03,0,DOOR_LEAF_W,4.02,.1,0x1b191f],[-k*DOOR_LEAF_W/2,.32,.06,DOOR_LEAF_W-.1,.42,.02,0x6b1520],
     [-k*DOOR_LEAF_W/2,1.7,.06,DOOR_LEAF_W-.16,.06,.02,0x6b6676],[-k*DOOR_LEAF_W/2,3.15,.055,.56,.4,.02,0x0b0a0e],[-k*.06,2.03,.06,.06,3.9,.03,0x6b6676]];
    for(let i=0;i<3;i++)parts.push([-k*DOOR_LEAF_W/2+(i-1)*.13,3.15,.08,.04,.34,.04,0x1b191f]);
    const mesh=new T.InstancedMesh(leafKit,leafMat,parts.length);mesh.name='ward-door-leaf';
    parts.forEach(([x,y,z,sx,sy,sz,c],i)=>{mesh.setMatrixAt(i,at(x,y,z,sx,sy,sz));mesh.setColorAt(i,new T.Color(c));});mesh.castShadow=true;mesh.receiveShadow=true;pivot.add(mesh);
    const glow=new T.Mesh(leafKit,win);glow.name='ward-door-window';glow.position.set(-k*DOOR_LEAF_W/2,3.15,.06);glow.scale.set(.42,.26,.012);pivot.add(glow);pivots.push(pivot);}
   this.leaves.set(a.id,pivots);}
 }
 /** Bases, gallery bands and capitals on every column, with sconces on the ones facing the hall (they chase in the drops). */
 private buildColumns(kit:Kit){
  const m=this.mat,ring:{a:number;i:number}[]=[];
  for(const c of SIREN_COLUMNS){kit.add('',m.stone,at(c.x,.3,c.z,1.9,.6,1.9));kit.add('',m.wood,at(c.x,2.66,c.z,1.56,.16,1.56));kit.add('',m.stone,at(c.x,BALCONY-.1,c.z,1.7,.4,1.7));kit.add('',m.stone,at(c.x,14.7,c.z,1.9,.5,1.9));kit.add('',m.stone,at(c.x,15.1,c.z,2.2,.3,2.2));
   const corner=Math.abs(Math.abs(c.x)-30.6)<.05&&Math.abs(Math.abs(c.z)-26.6)<.05;if(corner)continue;
   const dx=Math.abs(Math.abs(c.x)-30.6)<.05?-Math.sign(c.x):0,dz=dx?0:-Math.sign(c.z),x=c.x+dx*.86,z=c.z+dz*.86;
   kit.add('',m.iron,at(c.x+dx*.78,3.45,c.z+dz*.78,.12+Math.abs(dx)*.16,.12,.12+Math.abs(dz)*.16));ring.push({a:Math.atan2(x,z),i:this.lamps.add(0xffe2b0,at(x,3.62,z,.2,.3,.2))});}
  this.sconces=ring.sort((p,q)=>p.a-q.a).map(p=>p.i);
  // The four restraint pylons: hazard stripes at the foot, iron cuffs with red seams, and chains slung between their tops.
  for(const x of [-6,6])for(const z of [-6,6]){for(let i=0;i<4;i++){const a=i*Math.PI/2;kit.add('',m.stripe,at(x+Math.sin(a)*.78,.35,z+Math.cos(a)*.78,i%2?1.56:.08,.18,i%2?.08:1.56));}
   for(const y of [2.2,12.6]){kit.add('',m.iron,at(x,y,z,1.9,.55,1.9));for(let i=0;i<4;i++){const a=i*Math.PI/2;this.seams.push(this.lamps.add(PALETTE.warn,at(x+Math.sin(a)*.96,y,z+Math.cos(a)*.96,i%2?1.7:.02,.06,i%2?.02:1.7)));}}}
  for(const [a,b] of [[-6,-6,6,-6],[6,-6,6,6],[6,6,-6,6],[-6,6,-6,-6]].map(([x0,z0,x1,z1])=>[new T.Vector3(x0,12.4,z0),new T.Vector3(x1,12.4,z1)]))this.chain(kit,'',a,b,1.7);
 }
 /** A hanging chain of alternating links from a to b, sagging by `sag` in the middle. */
 private chain(kit:Kit,tag:Tag,a:T.Vector3,b:T.Vector3,sag:number){
  const n=Math.max(2,Math.round(a.distanceTo(b)/.3)),p=(t:number)=>a.clone().lerp(b,t).setY(mix(a.y,b.y,t)-sag*4*t*(1-t));
  for(let i=0;i<n;i++){const p0=p(i/n),p1=p((i+1)/n);dummy.position.copy(p0).lerp(p1,.5);dummy.lookAt(p1);dummy.rotateZ(i%2?Math.PI/2:0);dummy.scale.set(.14,.05,p0.distanceTo(p1)*1.1);dummy.updateMatrix();kit.add(tag,this.mat.iron,dummy.matrix);}
 }
 /** Isolation cells: quilted padding, a lintel with the raised gate's teeth, and an alarm lamp over each opening. */
 private buildCells(kit:Kit){
  const m=this.mat;
  for(const a of SIREN_ADD_SPAWNS){if(a.kind!=='cell')continue;const sx=Math.sign(a.x),z=a.z;
   for(let i=0;i<7;i++)for(let j=0;j<5;j++)kit.add('',m.pad,at(sx*29.36,.45+j*.66,z-2.4+i*.8,.06,.6,.74));
   kit.add('',m.iron,at(sx*24.3,3.65,z,.55,.3,6.6));
   // The gate: 15 bars and two rails that retract into the lintel (raised, only the teeth show; PvE lowers it while closed).
   const gate=new T.Group();gate.name='ward-'+a.id+'-gate';gate.position.set(sx*24.3,3.5,z);this.group.add(gate);const bars=new T.InstancedMesh(unit,m.iron,17);bars.name='ward-cell-gate';
   for(let i=0;i<15;i++)bars.setMatrixAt(i,at(0,-1.725,-2.8+i*.4,.07,3.45,.07));bars.setMatrixAt(15,at(0,-3.2,0,.09,.09,5.8));bars.setMatrixAt(16,at(0,-1.6,0,.09,.09,5.8));bars.castShadow=true;gate.add(bars);
   gate.scale.y=CELL_STUB;this.gates.set(a.id,gate);
   this.alarms.set(a.id,this.lamps.add(PALETTE.warn,at(sx*23.98,3.65,z,.06,.22,.5)));}
 }
 /** The vault: ribs, a slow exhaust fan, chandeliers hung with teeth, failing work lights and four speaker stacks. */
 private buildVault(kit:Kit){
  const m=this.mat,v=this.groups.v,y=H.roof-.45;
  for(const z of [...new Set(SIREN_COLUMNS.filter(c=>Math.abs(Math.abs(c.x)-30.6)<.05).map(c=>c.z))])kit.add('v',m.iron,at(0,y,z,2*H.halfX,.9,.7));
  for(const x of [...new Set(SIREN_COLUMNS.filter(c=>Math.abs(Math.abs(c.z)-26.6)<.05&&Math.abs(Math.abs(c.x)-30.6)>.05).map(c=>c.x))])kit.add('v',m.iron,at(x,y,0,.7,.9,2*H.halfZ));
  // Exhaust fan over the centre, turning with the song's energy over a red glow.
  const fan=new T.Group();fan.position.set(0,H.roof-.8,0);v.add(fan);const torus=new T.TorusGeometry(3.1,.2,8,48);torus.rotateX(Math.PI/2);kit.add('v',m.iron,at(0,H.roof-.8,0,1,1,1),torus);
  const blades=new T.Group();fan.add(blades);for(let i=0;i<5;i++){const a=i/5*Math.PI*2,b=new T.Mesh(unit,m.iron);b.scale.set(2.7,.06,.7);b.position.set(Math.cos(a)*1.6,0,Math.sin(a)*1.6);b.rotation.set(.35,-a,0,'YXZ');blades.add(b);}
  const hub=new T.Mesh(new T.CylinderGeometry(.45,.45,.5,12),m.iron);fan.add(hub);const glow=glowMaterial(PALETTE.warnDeep,{intensity:.4});const disc=new T.Mesh(new T.CircleGeometry(3,40),glow);disc.rotation.x=Math.PI/2;disc.position.y=.7;fan.add(disc);
  this.fan={blades,glow,angle:0};
  // Chandeliers: iron hoops of bulbs with teeth hanging under them. They sway once a bar.
  const hoop=new T.TorusGeometry(1,.06,6,40);hoop.rotateX(Math.PI/2);
  for(const [x,z,rad,drop,phase] of [[0,0,2.4,4.6,0],[0,-15,1.6,4,.5],[0,15,1.6,4,.25]]){const pivot=new T.Group();pivot.position.set(x,H.roof-1.1,z);v.add(pivot);
   const chain=new T.Mesh(unit,m.iron);chain.scale.set(.07,drop,.07);chain.position.y=-drop/2;pivot.add(chain);const h=new T.Mesh(hoop,m.iron);h.scale.setScalar(rad);h.position.y=-drop;pivot.add(h);
   for(let i=0;i<3;i++){const a=i/3*Math.PI*2,foot=new T.Vector3(Math.cos(a)*rad,-drop,Math.sin(a)*rad),top=new T.Vector3(0,-drop+1.1,0),s=new T.Mesh(unit,m.iron);
    s.scale.set(.04,foot.distanceTo(top),.04);s.position.copy(foot).lerp(top,.5);s.quaternion.setFromUnitVectors(UP,top.clone().sub(foot).normalize());pivot.add(s);}
   const teeth=new T.InstancedMesh(new T.ConeGeometry(.07,1,4),m.bone,Math.round(rad*14));for(let i=0;i<teeth.count;i++){const a=i/teeth.count*Math.PI*2,L=.35+((i*7)%5)*.12;teeth.setMatrixAt(i,at(Math.cos(a)*rad,-drop-L/2-.04,Math.sin(a)*rad,1,L,1,Math.PI,0,0));}teeth.instanceMatrix.needsUpdate=true;pivot.add(teeth);
   const bulbs=new Lamps(pivot,24);for(let i=0;i<Math.round(rad*8);i++){const a=(i+.5)/Math.round(rad*8)*Math.PI*2;bulbs.add(0xffd9a8,at(Math.cos(a)*rad,-drop+.14,Math.sin(a)*rad,.11,.2,.11));}
   this.chandeliers.push({pivot,bulbs,phase});}
  // Work lights: rows of fluorescent tubes on cables; two lights carry their glow into the hall.
  const rows=[-19,-9,9,19],per=6;this.tubes=new T.InstancedMesh(unit,new T.MeshBasicMaterial({color:0xffffff,toneMapped:false}),rows.length*per);this.tubes.instanceColor=new T.InstancedBufferAttribute(new Float32Array(rows.length*per*3),3);
  let i=0;for(const x of rows)for(let j=0;j<per;j++){const z=-20+j*8;this.tubes.setMatrixAt(i,at(x,11.4,z,.16,.1,2.4));this.tubes.setColorAt(i,col.setScalar(1));this.tubeLevel.push(1);i++;
   kit.add('v',m.iron,at(x,11.6,z,.3,.1,2.5));for(const k of [-1,1])kit.add('v',m.iron,at(x,(11.6+H.roof)/2,z+k,.03,H.roof-11.6,.03));}
  this.tubes.name='ward-tubes';v.add(this.tubes);
  for(const x of [-11,11]){const l=new T.PointLight(0xdfe6ff,0,58,1.25);l.position.set(x,10.4,0);this.group.add(l);this.tubeLights.push(l);}
  // Speaker stacks hung in the corners, aimed at the floor in the middle; the cones pump on the beat.
  const cone=new T.CircleGeometry(.32,20),ringGeo=new T.RingGeometry(.34,.4,24);
  for(const [x,z] of [[-26,-22],[26,-22],[26,22],[-26,22]]){const g=new T.Group();g.position.set(x,12.3,z);g.lookAt(0,2,0);v.add(g);
   for(const yy of [-.58,.58]){const c=new T.Mesh(unit,m.iron);c.scale.set(1.6,1.1,1);c.position.y=yy;g.add(c);}
   const cones=new T.InstancedMesh(cone,m.dark,4),rings=new Lamps(g,4,ringGeo,'ward-speaker-rings'),spots:T.Vector3[]=[];
   for(const yy of [-.58,.58])for(const xx of [-.4,.4]){const p=new T.Vector3(xx,yy,.51);spots.push(p);rings.add(0xd9d0ff,at(p.x,p.y,p.z+.01,1,1,1));}cones.frustumCulled=false;g.add(cones);this.speakers.push({cones,rings,spots});
   kit.add('v',m.iron,at(x,(13.4+H.roof)/2,z,.05,H.roof-13.4,.05));}
 }
 /** Silhouetted patients watching from the galleries. They bob on the beat, freeze and stare in the breakdown. */
 private buildCrowd(){
  const r=rng(53),list:WardZeroVisuals['crowd']['list']=[];
  for(const s of SIDE_KEYS){const f=SIDES[s],cols=columnsOn(s),long=s==='e'||s==='w',segs=long?[[-f.half,f.half]]:[[-f.half,-12.7],[12.7,f.half]];
   for(const [a,b] of segs)for(let u=a+.9;u<b-.6;u+=1.05+r()*.9){if(cols.some(c=>Math.abs(c-u)<1)||r()<.28)continue;const p=new T.Vector3(u,0,.62).applyAxisAngle(UP,f.ry);
    list.push({tag:s,x:p.x+f.x,y:BALCONY,z:p.z+f.z,ry:f.ry+(r()-.5)*.5,h:.85+r()*.3,phase:r()});}}
  const bodies=new T.InstancedMesh(unit,this.mat.figure,list.length),heads=new T.InstancedMesh(new T.SphereGeometry(.17,10,8),this.mat.figure,list.length);bodies.frustumCulled=heads.frustumCulled=false;
  this.group.add(bodies,heads);const eyes=new Lamps(this.group,list.length*2,unit,'ward-crowd-eyes');for(const p of list)for(let k=0;k<2;k++)eyes.add(0xf3f0ff,ZERO,p.tag);
  this.crowd={bodies,heads,eyes,list};
 }
 /** Red rotating siren beacons: one over each ward door and one on each side wall; the side ones sweep real light across the hall. */
 private buildBeacons(){
  const spots:[Side,number,number][]=[['w',0,6.6],['e',0,6.6],...SIREN_ADD_SPAWNS.filter(a=>a.kind==='door').map(a=>{const s:Side=a.z<0?'n':'s';return [s,local(s,a.x,a.z).u,5.7] as [Side,number,number];})];
  const housing=new T.MeshLambertMaterial({color:0x1b1a1f}),beamGeo=new T.ConeGeometry(1.6,9,24,1,true);beamGeo.translate(0,-4.5,0);beamGeo.rotateX(-Math.PI/2);
  spots.forEach(([s,u,y],i)=>{const base=place(new T.Group(),s,u,y,.35);this.groups[s].add(base);
   const box=new T.Mesh(unit,housing);box.scale.set(.6,.25,.5);box.position.y=-.2;base.add(box);const lamp=glowMaterial(PALETTE.warn,{intensity:.4});base.add(new T.Mesh(new T.SphereGeometry(.24,16,10,0,Math.PI*2,0,Math.PI/2),lamp));
   const head=new T.Group();base.add(head);const beam=new T.Mesh(beamGeo,glowMaterial(PALETTE.warn,{transparent:true,opacity:0,additive:true,side:T.DoubleSide}));beam.position.y=.08;head.add(beam);
   const b:WardZeroVisuals['beacons'][number]={head,beam,lamp,phase:i*1.7,angle:0};
   if(i<2){const spot=new T.SpotLight(0xff2436,0,52,.32,.5,1.2);spot.position.set(0,.1,0);const target=new T.Object3D();target.position.set(0,-.3,4);head.add(spot,target);spot.target=target;b.spot=spot;}
   this.beacons.push(b);});
 }
 private paint(s:Side,u:number,y:number,d:number,w:number,h:number,big=false,parent:T.Object3D=this.groups[s]){const mat=glowMaterial(PALETTE.teeth,{intensity:.15});parent.add(place(new T.Mesh(grinGeometry(w,h,big?17:11),mat),s,u,y,d));this.grins.push({mat,big});}
 /** Grins over the side walls and inside the cells, clocks that run backwards on the beat, tally marks, and YOU. */
 private buildGraffiti(kit:Kit){
  for(const s of ['e','w'] as Side[]){this.paint(s,0,4.3,.06,1.6,.26);
   // A wall clock over the beacon, its hands ticking backwards one step per beat.
   const face=place(new T.Group(),s,0,12.8,.06);this.groups[s].add(face);face.add(new T.Mesh(new T.CircleGeometry(1.5,40),this.mat.bone));const rim=new T.Mesh(new T.TorusGeometry(1.55,.12,8,40),this.mat.iron);face.add(rim);
   const ticks=new T.InstancedMesh(unit,this.mat.iron,12);for(let i=0;i<12;i++){const a=i/12*Math.PI*2;ticks.setMatrixAt(i,at(Math.sin(a)*1.25,Math.cos(a)*1.25,.02,.06,i%3?.18:.34,.02,0,0,-a));}ticks.instanceMatrix.needsUpdate=true;face.add(ticks);
   const hand=(len:number,w:number)=>{const p=new T.Group();p.position.z=.05;face.add(p);const h=new T.Mesh(unit,this.mat.iron);h.scale.set(w,len,.03);h.position.y=len/2-.1;p.add(h);return p;};this.clocks.push({minute:hand(1.2,.07),hour:hand(.8,.11)});}
  for(const a of SIREN_ADD_SPAWNS)if(a.kind==='cell'){const s:Side=a.x<0?'w':'e',{u}=local(s,a.x,a.z);this.paint(s,u,2.55,2.68,.8,.13,false,this.group);}
  const r=rng(11),marks=(x:number,y:number,z:number,ry:number,groups:number)=>{for(let g=0;g<groups;g++)for(let i=0;i<5;i++){const a=g*.42+i*.06;kit.add('',this.mat.scratch,at(x+Math.cos(ry)*a,y+(g%3)*.48,z-Math.sin(ry)*a,.02,i===4?.42:.3,.012,0,ry,i===4?1.15:(r()-.5)*.12));}};
  marks(-26,1.3,-27.9,0,14);marks(14,1.4,-27.9,0,10);marks(-8,1.2,27.9,Math.PI,12);marks(31.9,1.3,-22,-Math.PI/2,9);marks(-31.9,1.2,22,Math.PI/2,11);
  for(const s of [-1,1])for(const z of [-11,11])marks(s*29.29,1.2,z-2.2,s>0?-Math.PI/2:Math.PI/2,6);
  // YOU, scrawled in strokes beside the great doors.
  const stroke=(x0:number,y0:number,x1:number,y1:number)=>{const L=Math.hypot(x1-x0,y1-y0);kit.add('s',this.mat.ink,onWall('s',(x0+x1)/2,(y0+y1)/2,.07,.12,L,.03,Math.atan2(-(x1-x0),y1-y0)));};
  const X=-7.8,Y=3.4;stroke(X-1.6,Y+1,X-1.2,Y+.4);stroke(X-.8,Y+1,X-1.2,Y+.4);stroke(X-1.2,Y+.4,X-1.2,Y-.2);stroke(X-.4,Y+.9,X-.4,Y-.1);stroke(X+.4,Y+.9,X+.4,Y-.1);stroke(X-.4,Y+.9,X+.4,Y+.9);stroke(X-.4,Y-.1,X+.4,Y-.1);
  stroke(X+.9,Y+1,X+.9,Y);stroke(X+1.7,Y+1,X+1.7,Y);stroke(X+.9,Y,X+1.7,Y-.1);
 }
 /** The containment circle in the middle of the floor: rings, ticks and rune marks. */
 private buildCircle(){
  const ring=glowMaterial(0xe8e2ff,{intensity:.2,side:T.DoubleSide});for(const [a,b] of [[4.3,4.45],[5.2,5.32],[2.2,2.28]]){const m=new T.Mesh(new T.RingGeometry(a,b,96),ring);m.rotation.x=-Math.PI/2;m.position.y=.02;this.group.add(m);}
  const tickMat=glowMaterial(0xe8e2ff,{intensity:.2});const ticks=new T.InstancedMesh(unit,tickMat,48);
  for(let i=0;i<48;i++){const a=i/48*Math.PI*2,long=i%4===0;ticks.setMatrixAt(i,at(Math.sin(a)*4.85,.021,Math.cos(a)*4.85,.05,.004,long?.7:.35,0,a,0));}
  ticks.instanceMatrix.needsUpdate=true;ticks.name='ward-ticks';this.group.add(ticks);
  const runes=new T.Group(),marks=new T.InstancedMesh(unit,ring,36);for(let i=0;i<12;i++){const a=i/12*Math.PI*2;for(let j=0;j<3;j++)marks.setMatrixAt(i*3+j,at(Math.sin(a)*3.3+(j-1)*.12,.022,Math.cos(a)*3.3,.05,.004,.28-(j%2)*.1,0,a+(j-1)*.6,0));}
  marks.instanceMatrix.needsUpdate=true;runes.add(marks);
  this.group.add(runes);
  const soot=new T.Mesh(new T.CircleGeometry(6.2,48),decalMaterial(0x000000,.35));soot.rotation.x=-Math.PI/2;soot.position.y=.012;this.group.add(soot);
  return {ring,tickMat,runes};
 }
 /** Floor grates over the vents: a red rim that fills as the vent arms, black fire when it erupts. */
 private buildVents(kit:Kit){
  for(const v of SIREN_VENTS){const g=new T.Group();g.name='ward-'+v.id;g.position.set(v.x,.015,v.z);this.group.add(g);
   // The grate is a lid hinged at x=VENT_HINGE, so an add climbing out (or diving in) can push it open (PvE, design §8.4).
   const glow=glowMaterial(PALETTE.warn,{transparent:true,opacity:0,side:T.DoubleSide});const rim=new T.Mesh(new T.RingGeometry(1.15,1.4,4,1),glow);rim.rotation.set(-Math.PI/2,0,Math.PI/4);rim.position.y=.01;g.add(rim);
   // The open vent is a black pit (owner 2026-10-04): a shaft drawn into the floor quad (see ventShaftMaterial), shown only while the lid is up.
   const shaft=ventShaftMaterial(),pit=new T.Mesh(PIT_GEO,shaft);pit.name='ward-'+v.id+'-pit';pit.position.y=-.003;pit.renderOrder=1;pit.visible=false;g.add(pit);
   const lid=new T.Group();lid.name='ward-'+v.id+'-lid';lid.position.set(VENT_HINGE,0,0);g.add(lid);const bars=new T.InstancedMesh(unit,this.mat.iron,9);bars.name='ward-vent-lid';bars.receiveShadow=true;
   const list=[...Array.from({length:6},(_,i)=>[2.2,.1,0,-1+i*.4]),[.1,2.3,-1.12,0],[.1,2.3,1.12,0],[2.34,.1,0,1.12]];list.forEach(([w,d,x,z],i)=>bars.setMatrixAt(i,at(x-VENT_HINGE,0,z,w,.03,d)));lid.add(bars);
   this.vents.push({id:v.id,grate:g,lid,glow,acc:0,burst:false,pit,shaft});}
 }

 // ---- per frame --------------------------------------------------------------------------------------------------
 setActive(on:boolean){
  if(on===this.active)return;this.active=on;this.group.visible=on;
  if(!on){for(const [l,i] of this.saved)l.intensity=i;this.saved.clear();this.fire.clear();this.scanned=-1e9;}
 }
 private own(o:T.Object3D){for(let x:T.Object3D|null=o;x;x=x.parent)if(x===this.group)return true;return false;}
 /** A camera outside the hall (the menu overview orbits above it) sees the near walls and the vault cut away. */
 private cutaway(c:T.Vector3){
  const k=this.cut;k.w=c.x<-H.halfX;k.e=c.x>H.halfX;k.n=c.z<-H.halfZ;k.s=c.z>H.halfZ;k.v=c.y>H.roof-.1;
  for(const s of [...SIDE_KEYS,'v'] as Tag[])this.groups[s].visible=!k[s];
 }
 /** Applies the same cut-away to the hall's own collision props (walls and roof) as the renderer draws them. */
 shell(props:Iterable<[string,T.Object3D]>){
  if(!this.active)return;
  for(const [id,o] of props){if(id.startsWith('siren-roof-'))o.visible=!this.cut.v;else if(id.startsWith('siren-wall-')){const p=o.position,s:Side=Math.abs(p.x)>H.halfX?(p.x<0?'w':'e'):(p.z<0?'n':'s');o.visible=!this.cut[s];}}
 }

 update(state:Snapshot,time:number,quality:number,camera?:T.Camera){
  if(!this.active)return;tickSirenClock(time);const dt=clamp(time-this.last,0,.05);this.last=time;
  const song=bossSongOf(state),raw=song?state.time-song.startedAt:-1,t=song?loopedSongTime(TRACK,Math.max(0,raw)):0;
  const pos=song&&raw>=0?barAt(TRACK,t):{bar:0,beat:1,phase:time%1,index:-1},section:BossSectionId=song&&raw>=0?sectionAt(TRACK,t):'build';
  // Ease toward the section's mood (about a bar).
  const target=MOODS[section],k=1-Math.exp(-dt*2.2);for(const key of KEYS)this.mood[key]+=(target[key]-this.mood[key])*k;const m=this.mood;
  const beatPulse=(1-pos.phase)**3,down=pos.beat===1?beatPulse:0,kick=pos.beat%2===1?beatPulse:0,off=pos.beat%2===0?beatPulse:0,q=quality<.5?.5:1;
  // A song-locked count of beats for everything that has to agree between viewers (sway, clocks, chases).
  const beats=pos.index>=0?pos.index+pos.phase:time*2,bar=((pos.beat-1)+pos.phase)/4,newBeat=pos.index!==this.lastBeat;this.lastBeat=pos.index;
  if(camera)this.cutaway(camera.position);
  this.cut.pads=!machinesPresent(state);this.groups.pads.visible=!this.cut.pads;const cut=this.cut;this.cutKey=SIDE_KEYS.filter(s=>cut[s]).join('')+(cut.v?'v':'')+(cut.pads?'p':'');

  // The arena's own night lights are mostly shut off in here: the ward is lit by its own tubes, sconces and beacons.
  // (Lights are re-collected about once a second rather than walking the scene every frame.)
  // Only the arena's own night lights (arenaLighting) are dimmed; effect lights (projectile glow, the Marked seeker) keep their own levels.
  if(time-this.scanned>1||time<this.scanned){this.scanned=time;this.scene.traverse(o=>{if(o instanceof T.Light&&NIGHT_LIGHTS.has(o.name)&&!this.own(o)&&!this.saved.has(o))this.saved.set(o,o.intensity);});}
  for(const [l,base] of this.saved)l.intensity=base*m.night;

  // Work lights: steady, pulsing with the beat, strobing in the climax, failing at random in the quiet parts.
  let sum=0;for(let i=0;i<this.tubeLevel.length;i++){const seed=i*12.9898,fail=m.erratic*(Math.sin(time*(3+i%5)+seed)>.55-.4*m.erratic?1:0)*(Math.sin(time*23+seed*3)>0?1:.2);
   const strobe=m.strobe>0?mix(1,pos.phase<.45?1:.08,m.strobe):1,level=clamp(m.tubes*(1-.9*fail)*strobe*(.82+.18*beatPulse));this.tubeLevel[i]=level;sum+=level;
   this.tubes.setColorAt(i,col.setRGB(level*.95,level*.97,level));}
  if(this.tubes.instanceColor)this.tubes.instanceColor.needsUpdate=true;const avg=sum/this.tubeLevel.length;for(const l of this.tubeLights)l.intensity=avg*300;

  // Beacons: spin with the section (integrated, so a section change never jumps the beam), flash on downbeats.
  for(const b of this.beacons){b.angle+=dt*m.spin;b.head.rotation.y=b.angle+b.phase;const bright=m.beacons*(.6+.4*down);b.lamp.uniforms.uIntensity.value=.25+1.4*bright;(b.beam.material as T.ShaderMaterial).uniforms.uOpacity.value=.16*bright*q;if(b.spot)b.spot.intensity=bright*300;}
  this.redWash.intensity=m.red*(.15+.25*down);this.fill.intensity=.4+.4*m.tubes;for(const w of this.washes)w.intensity=.22+.18*m.tubes;

  // The altar: organ pipes light like an equaliser, a wave running outwards from the face on every beat; the face and
  // rose window swell on the downbeat.
  const red=col.set(PALETTE.ash).lerp(new T.Color(PALETTE.warn),m.red*.8).clone();
  for(const p of this.pipes){const wave=clamp(1-Math.abs(pos.phase*1.4-p.d)*3.2),hold=.5+.5*Math.sin(beats*Math.PI*.5+p.d*5);this.lamps.set(p.i,.06+m.organ*(.25*hold+.85*wave*(pos.beat===1?1:.6)),red);}
  this.organLight.intensity=m.organ*(30+150*down)+m.eyes*25;this.rose.uniforms.uIntensity.value=.12+m.red*.9*(.5+.5*down)+m.organ*.25*kick;
  for(const g of this.grins)g.mat.uniforms.uIntensity.value=.08+m.grins*(g.big?.55+.45*down:.35+.4*beatPulse);
  // Giant eyes open in the breakdown and climax, with a slow blink every two bars.
  const blink=pos.bar%2===0&&pos.beat===4&&pos.phase>.6?0:1;for(const [i,e] of this.eyes.entries()){const open=Math.max(.02,m.eyes*blink);e.mesh.scale.set(1.1,1.5*open,1);e.mat.uniforms.uIntensity.value=.4+.8*m.eyes;(this.eyeHalos[i].material as T.SpriteMaterial).opacity=.35*m.eyes*blink;}

  // Sconces: a steady flicker that becomes a light chasing around the hall, one column per beat, in the drops.
  const chase=clamp((m.spin-1)/3),n=this.sconces.length,head=((Math.floor(beats)%n)+n)%n;
  this.sconces.forEach((i,j)=>{const back=((head-j)%n+n)%n,tail=Math.exp(-(back+pos.phase)*1.3),steady=.32+.08*Math.sin(time*7+j*1.7)+.1*beatPulse;this.lamps.set(i,mix(steady,.06+1.1*tail,chase));});
  for(const i of this.cracks)this.lamps.set(i,.08+m.red*(.5+.5*down)+.15*m.grins*kick);
  for(const i of this.seams)this.lamps.set(i,.05+m.red*1.3*down+.15*m.circle*beatPulse);
  // Add-spawn alarms idle at a slow pulse; chip pads breathe on the off-beat.
  let a=0;for(const i of this.alarms.values())this.lamps.set(i,.18+.12*Math.sin(time*1.6+a++)+.4*m.red*down);
  this.pads.forEach((i,j)=>{const level=.22+.4*off*(.4+m.circle);this.lamps.set(i,level*1.4);this.padRings.set(j,level);});this.padRings.commit(this.cut,'');
  this.keyMat.uniforms.uIntensity.value=.4+.8*m.red*down;
  this.updateEntries(state,time,dt,q);

  // Galleries: the audience bobs on the beat (some on the off-beat), freezes in the breakdown, and their eyes stare.
  const cr=this.crowd,eyeTint=col.set(0xf3f0ff).lerp(new T.Color(PALETTE.warn),m.red).clone(),eyeLevel=.08+.7*m.eyes+.25*m.crowd*down;
  cr.list.forEach((p,i)=>{const hidden=this.cut[p.tag],beat=p.phase<.35?off:kick,bob=m.crowd*.1*beat*p.h,tilt=m.crowd*.22*Math.sin(beats*Math.PI+p.phase*6)+(1-m.crowd)*.12*(p.phase-.5);
   if(hidden){cr.bodies.setMatrixAt(i,ZERO);cr.heads.setMatrixAt(i,ZERO);}else{cr.bodies.setMatrixAt(i,at(p.x,p.y+.5*p.h+bob,p.z,.46,p.h,.28,0,p.ry,tilt*.3));
    dummy.position.set(0,p.h+.17,0).applyEuler(new T.Euler(0,p.ry,tilt*.3));const hx=p.x+dummy.position.x,hy=p.y+bob+dummy.position.y,hz=p.z+dummy.position.z;cr.heads.setMatrixAt(i,at(hx,hy,hz,p.h,p.h,p.h,0,p.ry,tilt));
    for(const k of [-1,1]){dummy.position.set(k*.065*p.h,.02,.175*p.h).applyEuler(new T.Euler(0,p.ry,tilt));cr.eyes.move(i*2+(k+1)/2,at(hx+dummy.position.x,hy+dummy.position.y,hz+dummy.position.z,.05,.035,.02,0,p.ry,tilt));}}
   for(let k=0;k<2;k++)cr.eyes.set(i*2+k,hidden?0:eyeLevel*(.7+.3*Math.sin(time*3+p.phase*9)),eyeTint);});
  cr.bodies.instanceMatrix.needsUpdate=true;cr.heads.instanceMatrix.needsUpdate=true;cr.eyes.commit(this.cut,this.cutKey,true);

  // The vault: chandeliers sway once a bar, the fan turns with the song's energy, the speakers pump on the beat.
  for(const c of this.chandeliers){c.pivot.rotation.z=m.sway*.05*Math.sin(2*Math.PI*(bar+c.phase));c.pivot.rotation.x=m.sway*.025*Math.sin(2*Math.PI*(bar+c.phase)*2+1);
   for(let i=0;i<c.bulbs.size;i++)c.bulbs.set(i,(.35+.5*m.tubes)*(.8+.2*Math.sin(time*9+i*2.1))*(1-.5*m.erratic*(Math.sin(time*5+i)>.8?1:0))+.4*down*m.organ);c.bulbs.commit(this.cut,'');}
  this.fan.angle+=dt*m.fan*2.2;this.fan.blades.rotation.y=this.fan.angle;this.fan.glow.uniforms.uIntensity.value=.15+.6*m.red+.2*m.fan*kick;
  for(const s of this.speakers){s.spots.forEach((p,i)=>{s.cones.setMatrixAt(i,at(p.x,p.y,p.z+.07*m.organ*kick,1+.12*m.organ*kick,1+.12*m.organ*kick,1));s.rings.set(i,.1+.9*m.organ*kick);});s.cones.instanceMatrix.needsUpdate=true;s.rings.commit(this.cut,'');}
  // Clocks run backwards: the minute hand ticks one step per beat, snapping like a real movement.
  const tick=Math.floor(beats)+clamp((beats%1)*6);for(const c of this.clocks){c.minute.rotation.z=tick*Math.PI*2/16;c.hour.rotation.z=tick*Math.PI*2/192;}
  // Great doors: pounded from outside on the kicks, shedding dust from the lintel.
  this.doors.position.z=-m.thump*.09*kick;if(newBeat&&pos.beat%2===1&&m.thump>.3&&!this.cut.s)for(let i=0;i<Math.round(6*q*m.thump);i++)this.fire.emit((Math.random()-.5)*7.6,7.2+Math.random()*.6,H.halfZ-.5,0,-.5,-.4,1.4,.14,'smoke',-.8,1.2);
  // Containment circle: a ring of light that throbs with the music.
  this.circle.ring.uniforms.uIntensity.value=.08+m.circle*(.3+.7*down);this.circle.tickMat.uniforms.uIntensity.value=.05+m.circle*(.2+.5*beatPulse);this.circle.runes.rotation.y=time*.05*(1+m.spin*.3);
  this.lamps.commit(this.cut,this.cutKey);

  // Vents: read the same schedule the simulation damages with.
  const hazards=song&&raw>=0?sirenStageHazards(raw):[];
  for(const v of this.vents){const h=hazards.find(x=>x.id===v.id);const warn=Math.max(h?.warning??0,this.ventWarn.get(v.id)??0),on=!!h?.active;
   v.glow.uniforms.uOpacity.value=on?.95:Math.max(warn*.9,this.ventGlow.get(v.id)??0);v.glow.uniforms.uColor.value.setHex(on?0xffffff:PALETTE.warn);
   if(warn>0&&Math.random()<dt*q*20*warn)this.fire.emit(v.grate.position.x+(Math.random()-.5)*2,.05,v.grate.position.z+(Math.random()-.5)*2,0,.5+warn,0,.4,.06,'black',1.5,2);
   if(on){if(!v.burst){v.burst=true;this.fire.burst(v.grate.position.x,.1,v.grate.position.z,Math.round(28*q),2,.6,.14,'black',4,1);}
    v.acc+=dt*q*110;while(v.acc>=1){v.acc-=1;const a=Math.random()*Math.PI*2,r=Math.random()*1;this.fire.emit(v.grate.position.x+Math.cos(a)*r,.05,v.grate.position.z+Math.sin(a)*r,(Math.random()-.5)*.5,6+Math.random()*4,(Math.random()-.5)*.5,.38+Math.random()*.2,.12+Math.random()*.1,Math.random()<.06?'ember':Math.random()<.2?'smoke':'black',-3,1.4);}}
   else v.burst=false;}
  this.fire.update(dt);
 }
 private ventGlow=new Map<string,number>();private ventWarn=new Map<string,number>();
 /** PvE entries: vent lids, door leaves and cell gates follow the authoritative entry times (pure in time); their alarms
  *  flash while open, and black smoke spills out as each one opens. Without PvE every entry rests in its PvP pose.
  *  Ward Zero outside PvE (PvP, DEV sandbox): the authority's own `vents` schedule wins over any PvE view (only the sandbox's
  *  downed/revive previews inject one there, with no entries). Each vent warns for VENT_PIT.warn s (rim fill, black wisps,
  *  rattling lid), then its lid swings VENT_PIT.lead s ahead of the pit turning lethal (wardVentDrawn); no add ever climbs out. */
 private updateEntries(state:Snapshot,time:number,dt:number,q:number){
  const view=pveOf(state),ward=state.vents,open=new Map<number,PveEntryView>();for(const e of ward??view?.entries??[])open.set(e.e,e);this.ventGlow.clear();this.ventWarn.clear();
  for(const en of PVE_ENTRIES){const e=open.get(en.e),pit=!!ward&&en.kind==='vent',o=pit?wardVentDrawn(e,state.time):entryOpenAmount(en.kind,e,state.time),active=pit?o>.02:!!e&&state.time>=e.openAt&&state.time<e.closeAt+ENTRY_CLOSE[en.kind];
   if(en.kind==='vent'){const v=this.vents[en.e];if(!v)continue;const w=pit?wardVentWarning(e,state.time):0;if(w>0)this.ventWarn.set(v.id,w);
    v.lid.rotation.z=VENT_SWING*o+.06*w*Math.abs(Math.sin(state.time*34));v.pit.visible=o>1e-3;v.shaft.uniforms.uOpen.value=o;if(active)this.ventGlow.set(v.id,.25+.35*o*(Math.sin(time*10)>0?1:.4));
    if(o>.05&&o<.98&&!this.cut.v){let acc=(this.entryAcc.get(en.id)??0)+dt*q*40;while(acc>=1){acc-=1;this.fire.emit(v.grate.position.x+(Math.random()-.5)*1.8,.05,v.grate.position.z+(Math.random()-.5)*1.8,0,.8+Math.random(),0,.6,.08,Math.random()<.5?'black':'smoke',1,1.5);}this.entryAcc.set(en.id,acc);}
    continue;}
   if(en.kind==='door'){const pivots=this.leaves.get(en.id);if(pivots)pivots.forEach((pv,i)=>{pv.rotation.y=(i===0?-1:1)*DOOR_SWING*o;});}
   else{const gate=this.gates.get(en.id);if(gate)gate.scale.y=view?mix(1,CELL_STUB,o):CELL_STUB;}
   const lamp=this.alarms.get(en.id);if(lamp!==undefined&&active)this.lamps.set(lamp,Math.sin(time*14)>0?1.6:.12);
  }
 }
}
