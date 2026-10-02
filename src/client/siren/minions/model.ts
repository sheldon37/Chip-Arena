import * as T from 'three';
import {createCharacterRig,setRigDetail,type CharacterRig} from '../../character-rig.js';
import {voxelMesh,customLayout,baseLayout,retagVoxelPart,type VoxelLayout,type UnitBox} from '../../voxel-kit.js';
import {minionLook,partLook,hash01,MISSING,type MinionLook,type MinionOptions,type PartCondition} from './look.js';
import {corruptedPart} from './layouts.js';
import {minionMaterial,categoryColor,TONGUE_MATERIAL,MINION_CUBE,MINION_PALETTE,type MinionCategory} from './materials.js';
import {MinionFx} from './fx.js';

/**
 * A Siren minion: our own Pilot rig (same joints, sockets and mesh names, so every pose helper, IK, LOD and ragdoll path
 * still applies), corrupted by its seed into a rotting, black-flame-burned, wounded zombie. Additions to the rig:
 * up to three black-flame tongues on its burns, an optional weapon scrap in the right hand, and one effect mesh for the
 * emergence pool and the burning-away death. All geometry, layouts and materials are shared by every minion.
 */
const SHELL=/^(hip-shell|abdomen-shell|ribcage|upper-chest|collar|blank-head|crown)$|-(upper-arm|forearm|thumb|thigh|calf|shin)$/,ACCENT=/^(waist-band|sternum)$|-(shoulder-cap|cuff|kneecap)$/,EDGE=/^(back-panel|jaw)$|-(palm|fingers|foot)$/,FLEX=/^(abdomen|neck-joint)$|-(elbow-joint|hip-joint|knee-joint|heel)$/;
/** The rig part categories (the same name registry as character-rig's colour paths). */
export function categoryOf(name:string):MinionCategory|undefined{return name.startsWith('armor-')?'strip':name.endsWith('-heel')?'edge':SHELL.test(name)?'shell':ACCENT.test(name)?'accent':EDGE.test(name)?'edge':FLEX.test(name)?'flex':undefined;}

export interface MinionTongue {mesh:T.Mesh;base:T.Quaternion;height:number;phase:number}
export interface MinionPart {mesh:T.Mesh;category:MinionCategory;scale:T.Vector3;y:number;live:T.Material;charred:T.Material}
export interface SirenMinion {
 rig:CharacterRig;look:MinionLook;seed:number;
 tongues:MinionTongue[];fx:MinionFx;scrap?:T.Mesh;
 /** Visible body parts with their rest scale and rest height in the rig group (the death burns them away by height). */
 parts:MinionPart[];
 /** Rig-group height of the floor under the feet at rest. */
 floor:number;
 low:boolean;
}
const MINIONS=new WeakMap<T.Object3D,SirenMinion>();
export const sirenMinionOf=(rig:{group:T.Object3D})=>MINIONS.get(rig.group);

// Black-flame tongue: a cluster of stepped voxel flames (one tall, two short), darkest at the root and paling toward
// the tips like the Siren's black fire; one mesh each, mirrored for variety.
type Lick=[number,number,number,[number,number][]];
const LICKS:Lick[]=[[0,0,0,[[.056,.034],[.049,.034],[.041,.034],[.033,.032],[.026,.03],[.019,.028],[.012,.026],[.007,.022]]],[-.03,0,-.008,[[.032,.03],[.026,.03],[.019,.027],[.012,.024],[.007,.02]]],[.029,0,.01,[[.03,.028],[.023,.027],[.016,.025],[.009,.022]]]];
// Tongues and scraps keep their own layout in low detail (the default plain variant is a unit box: a metre wide here).
const tongueLayouts:VoxelLayout[]=[1,-1].map(k=>{const boxes:UnitBox[]=[];
 for(const [x0,y0,z0,steps] of LICKS){let y=y0;steps.forEach(([w,h],i)=>{const f=i/(steps.length-1),x=k*(x0+(x0?Math.sign(x0)*.006*i:.005*Math.sin(i*1.7))),pale=1+7*f*f;boxes.push({box:[x,y+h/2,z0,w,h,w],color:[pale,pale*.95,pale*1.15]});y+=h*.92;});}
 const l=customLayout('siren-minion-tongue-'+k,boxes);l.custom=false;l.plain=l.ghost=l;return l;});

// Weapon scraps in weapon-socket space (+z along the blade): absolute sRGB colours, converted to scrap multipliers.
type ScrapBox=[number,number,number,number,number,number,number];
const SCRAPS:Record<'sword'|'pistol'|'pipe',ScrapBox[]>={
 sword:[[0,0,.02,.07,.07,.25,0x2a2320],[0,0,-.12,.06,.06,.05,0x7a4a2c],[0,0,.17,.27,.05,.06,0x6d5631],[.11,.005,.17,.04,.055,.065,0x4d3c24],[0,0,.31,.08,.032,.22,0x6b6d68],[0,0,.43,.055,.032,.04,0x5e605b],[.017,0,.46,.025,.03,.03,0x5e605b],[-.02,.017,.27,.03,.006,.05,0x8a4b25],[.018,-.017,.36,.035,.006,.04,0x8a4b25]],
 pistol:[[0,0,.04,.05,.05,.28,0x3a3c3e],[0,0,-.11,.042,.042,.03,0x1d1d1f],[0,.012,.24,.075,.085,.17,0x4a4c4e],[0,-.07,.29,.056,.13,.065,0x2b2622],[0,-.04,.2,.02,.035,.06,0x2b2622],[.04,.02,.26,.006,.04,.08,0x8a4b25],[-.035,0,.08,.006,.035,.09,0x7b4422]],
 pipe:[[0,0,.2,.05,.05,.64,0x5b4b40],[0,0,-.08,.06,.06,.035,0x463a31],[0,0,.15,.058,.058,.03,0x7d4626],[0,0,.38,.058,.058,.025,0x7d4626],[0,.035,.53,.07,.12,.07,0x4c3e34],[0,.11,.53,.05,.06,.05,0x5b4b40]],
};
const scrapLayouts=new Map<string,VoxelLayout>();
function scrapLayout(kind:'sword'|'pistol'|'pipe'){
 let l=scrapLayouts.get(kind);if(l)return l;const base=categoryColor('scrap');
 l=customLayout('siren-minion-scrap-'+kind,SCRAPS[kind].map(([x,y,z,w,h,d,hex])=>{const c=new T.Color(hex);return {box:[x,y,z,w,h,d],color:[c.r/base.r,c.g/base.g,c.b/base.b]};}));l.custom=false;
 // Small enough to keep in low detail (the default plain variant is a unit box, a metre wide at scale 1).
 l.plain=l.ghost=l;scrapLayouts.set(kind,l);return l;
}

const UP=new T.Vector3(0,1,0),tmp=new T.Vector3(),box=new T.Box3();
export function createSirenMinion(seed:number,options:MinionOptions={}):SirenMinion{
 const look=minionLook(seed,options);seed=look.seed;
 const rig=createCharacterRig('#2b2733',MINION_PALETTE.shell,true);rig.group.userData.sirenMinion=seed;
 rig.group.updateMatrixWorld(true);
 const hidden=new Set(look.missing==='none'?[]:MISSING[look.missing].hide),parts:MinionPart[]=[],anchors:{joint:T.Object3D;pos:T.Vector3;normal:T.Vector3;crown:boolean}[]=[];
 const meshes:T.Mesh[]=[];rig.group.traverse(o=>{if(o instanceof T.Mesh)meshes.push(o);});
 let strip=0;
 for(const mesh of meshes){
  const name=mesh.name,category=categoryOf(name),base=baseLayout(mesh);if(!category)continue;
  if(hidden.has(name)){mesh.visible=false;continue;}
  const live=minionMaterial(category,look.pool),charred=minionMaterial(category,look.pool,true);mesh.getWorldPosition(tmp);
  // Parts without a voxel sculpt (the abdomen core) only take the material.
  if(!base){mesh.material=live;parts.push({mesh,category,scale:mesh.scale.clone(),y:tmp.y,live,charred});continue;}
  let pl:{condition:PartCondition;variant:number}=partLook(look,name);
  if(category==='strip')pl={condition:hash01(seed,++strip,77)<.3?'dead':'rot',variant:0};
  const part=corruptedPart({name,category,size:mesh.scale.clone(),base,condition:pl.condition,variant:pl.variant,rot:look.rot,face:look.face,jawless:look.missing==='jaw'&&name==='blank-head'});
  retagVoxelPart(mesh as T.InstancedMesh,part.layout);mesh.material=live;
  for(const a of part.anchors)anchors.push({joint:mesh.parent!,pos:new T.Vector3(...a.pos).add(mesh.position),normal:new T.Vector3(...a.normal),crown:name==='crown'});
  parts.push({mesh,category,scale:mesh.scale.clone(),y:tmp.y,live,charred});
 }
 // Black-flame tongues on the burns (the crown split first): leaning out of the surface, then up.
 anchors.sort((a,b)=>Number(b.crown)-Number(a.crown));
 const tongues:MinionTongue[]=anchors.slice(0,3).map((a,i)=>{
  const mesh=voxelMesh(MINION_CUBE,TONGUE_MATERIAL,tongueLayouts[i%2]);mesh.name='siren-minion-tongue';mesh.castShadow=false;mesh.receiveShadow=false;
  mesh.position.copy(a.pos);const dir=a.normal.clone().multiplyScalar(.55).add(UP).normalize(),base=new T.Quaternion().setFromUnitVectors(UP,dir);mesh.quaternion.copy(base);a.joint.add(mesh);
  mesh.scale.setScalar(1.25);return {mesh,base,height:a.crown?1.2:.85+.3*hash01(seed,i,21),phase:hash01(seed,i,22)*6.28};});
 // A weapon scrap carried as a club (a broken sword, a pistol held by the barrel, a length of pipe).
 let scrap:T.Mesh|undefined;
 if(look.scrap!=='none'){scrap=voxelMesh(MINION_CUBE,minionMaterial('scrap',look.pool),scrapLayout(look.scrap));scrap.name='siren-minion-scrap';scrap.castShadow=true;rig.weapon.add(scrap);}
 for(const s of [rig.sockets.head,rig.sockets.chest,rig.sockets.back])s.visible=false;rig.offWeapon.visible=false;
 // The floor under the rest pose: the lowest point of the boots.
 let floor=-.86;const boot=rig.group.getObjectByName('left-foot');if(boot){box.setFromObject(boot);if(Number.isFinite(box.min.y))floor=box.min.y;}
 const fx=new MinionFx(seed,floor);rig.group.add(fx.mesh);
 const minion:SirenMinion={rig,look,seed,tongues,fx,scrap,parts,floor,low:false};MINIONS.set(rig.group,minion);return minion;
}

/** Low detail (SVG compatibility or 'performance' graphics): plain tinted boxes, two tongues at most. */
export function setSirenMinionDetail(m:SirenMinion,low:boolean){setRigDetail(m.rig,low);m.low=low;m.tongues.forEach((g,i)=>{if(i>=2)g.mesh.visible=!low;});}
/** Builds (and drops) `count` minions so the shared corrupted layouts are cached before adds spawn: the first minions
 * cost ~15 ms each to carve and paint, later ones ~1.6 ms. Call it during the Siren intro. */
export function prewarmSirenMinions(count=24){for(let i=0;i<count;i++)createSirenMinion((i*2654435761+0x51ed)>>>0);}
/** Removes the minion from the scene. Shared layouts and materials stay alive for the next minions. */
export function disposeSirenMinion(m:SirenMinion){m.rig.group.removeFromParent();m.fx.dispose();}
/** Mesh and instance counts (tests, budgets, the design doc). */
export function sirenMinionStats(m:SirenMinion){let meshes=0,visible=0,instances=0,triangles=0;m.rig.group.traverse(o=>{if(!(o instanceof T.Mesh))return;meshes++;let shown=o.visible;for(let p=o.parent;p&&shown;p=p.parent)shown=p.visible;if(o.name==='siren-minion-fx')shown=false;if(!shown)return;visible++;const n=o instanceof T.InstancedMesh?o.count:1;instances+=n;triangles+=n*12;});return {meshes,visible,instances,triangles};}
