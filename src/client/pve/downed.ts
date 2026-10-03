// PvE downed bodies (owner, 2026-10-03): "when player is dead, show little 'Help' message that goes up and down near their
// death. Keep their body as well (even if they're gibbed, show a 'holographic regeneration that rebuilds missing body parts
// and heals wounds)" and "Regenerate when revived". While a party member (human or party bot) is down, death-effects.ts
// keeps its body on the floor exactly as it fell (wounds, severed or crushed parts, blood pools) instead of fading it after
// five seconds, and beacons.ts shows the bobbing HELP bubble. When the member comes back (PvePartyView.upAt: a paid revive,
// the solo self-revive or a free respawn) a cyan hologram scans the kept body, seals its wounds, rebuilds the missing parts
// voxel by voxel and the player stands up from that spot. A respawn on a distant pad dissolves the old body and materialises
// the player on the pad. Presentation only: HP, hitboxes, damage, timing and the authority are untouched. Everything is pure
// in (view, state.time) apart from the kept ragdoll's fixed-step physics, which every client replays from the same death.
import * as T from 'three';
import type {DeathPresentation,PlayerView,Snapshot} from '../../shared/simulation.js';
import {pveOf,type PveView} from '../../shared/pve/contract.js';
import type {CharacterRig} from '../character-rig.js';
import {seedTransition,TRANSITION_BONES} from '../pose-transitions.js';
import {currentLayout,retagVoxelPart} from '../voxel-kit.js';
import type {PveRigRef,PveVisualsContext} from './visuals.js';

export const DOWNED={
 /** A kept ragdoll holds this age (settled; the stock fade starts at 4.35 s). Effects may name their own (keepAge). */
 keepAge:4.2,
 /** Effects a kept death plays as chosen; any other effect id plays the impact ragdoll, so there is always a body. */
 keepable:['impact-ragdoll','ragdoll','dev-gibber','siren-crush'] as readonly string[],
 /** Effect budget of one kept body (outside ANIMATION.maxDeathVoxels, bounded by the party size). */
 budget:96,
 /** At most this many late bodies (fast-forwarded to their hold) are built per frame. */
 lateBuildsPerFrame:1,
 /** Regeneration: the body is scanned, sealed and rebuilt for `scan` s, then the player stands up over `stand` s. */
 scan:.8,stand:.6,
 /** Back up within this of the body: regenerate in place. Farther (a respawn pad): the old body dissolves over `dissolve`
  *  and the player materialises where they are. */
 placeRadius:1.6,dissolve:.7,
 /** The regeneration's visuals start this long after upAt on the presented clock (see DownedBodies.update). */
 settle:.05,
 /** Hologram voxels of one regeneration (missing parts, or a whole materialising body). */
 voxels:{high:360,low:120},
 /** HELP bubble: bottom edge this far above the floor point, bob amplitude (m) and period (s); width w = k·distance. */
 help:{bottom:2.62,bob:.11,period:1.15,k:.085,min:5,max:26,aspect:.5},
 color:0x5fe8ff,
} as const;
export const REGEN_SECONDS=DOWNED.scan+DOWNED.stand;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const hash=(s:string)=>{let h=2166136261;for(let i=0;i<s.length;i++)h=Math.imul(h^s.charCodeAt(i),16777619)>>>0;return h;};

// ---- pure rules (tests) ------------------------------------------------------------------------------------------
/** A kept body: a downed member of the party (humans and party bots, connected) while the PvE run shows. The body stays
 *  through the end outro too; only revive/respawn, a disconnect or leaving PvE releases it. */
export function bodyKept(p:Pick<PlayerView,'id'|'hp'|'death'>,view:PveView|undefined):boolean{
 if(!view||!p.death||p.hp>0)return false;const row=view.party[p.id];return !!row&&row.connected;
}
/** The HELP bubble rides on the revive beacon of every downed member, for everyone except the downed player themself
 *  (their HUD shows their state). Hidden once the run has ended. */
export function helpShown(p:Pick<PlayerView,'id'|'hp'>,view:PveView,me:string):boolean{
 return p.id!==me&&p.hp<=0&&view.stage!=='ended'&&!!view.party[p.id]?.connected;
}
/** Bubble bob (m), pure in time; members are phase-shifted so several bubbles do not move in lockstep. */
export const helpBob=(t:number,id='')=>DOWNED.help.bob*Math.sin((t/DOWNED.help.period+(hash(id)%997)/997)*Math.PI*2);
/** Bubble width (m) at a camera distance: constant on screen between `min` and `max` metres. */
export const helpWidth=(distance:number)=>DOWNED.help.k*clamp(distance,DOWNED.help.min,DOWNED.help.max);
/** How far the presented clock may trail upAt and still start the regeneration (at age 0). Remote snapshots present hp,
 *  deaths and the PvE view from the newest snapshot while `time` trails it by the interpolation delay (≤ 350 ms, more
 *  while a slow client catches up), so a member is up before the presented time reaches upAt. */
export const REGEN_LEAD=1;
/** Seconds since this member came back while its regeneration runs (0 while the presented clock still trails upAt), else
 *  undefined. */
export function regenAge(view:PveView|undefined,id:string,t:number):number|undefined{
 const up=view?.party[id]?.upAt;if(up===undefined)return undefined;const a=t-up;
 return a>-REGEN_LEAD&&a<REGEN_SECONDS?Math.max(0,a):undefined;
}
export interface RegenPhase{
 /** Scan front over the body, 0 → 1. */ scan:number;
 /** True from the stand-up on: the live rig shows (before it, the kept body or the materialising voxels do). */ rise:boolean;
 /** Stand-up progress 0 → 1. */ stand:number;
 /** Hologram strength: eases in, holds while scanning, fades over the stand-up. */ glow:number;
 done:boolean;
}
/** The regeneration timeline, pure in age (seconds since upAt). */
export function regenPhase(age:number):RegenPhase{
 const a=Math.max(0,age),stand=clamp((a-DOWNED.scan)/DOWNED.stand);
 return {scan:clamp(a/(DOWNED.scan*.8)),rise:a>=DOWNED.scan,stand,glow:a<DOWNED.scan?smooth(a/.15):1-smooth(stand),done:a>=REGEN_SECONDS};
}
/** In place when the member is back within placeRadius of the body; otherwise (respawn pad) the body dissolves. */
export const regenMode=(body:{x:number;z:number}|undefined,pos:{x:number;z:number}):'place'|'pad'=>
 body&&Math.hypot(body.x-pos.x,body.z-pos.z)<=DOWNED.placeRadius?'place':'pad';
/** Arrival time of a hologram voxel at scan coordinate s (0 feet … 1 head): just behind the scan front. */
export const voxelArrival=(s:number)=>.1+clamp(s)*DOWNED.scan*.8;

// ---- body joints ---------------------------------------------------------------------------------------------------
/** Ragdoll parts (rig joint names) and their parents. A destroyed part owns its subtree. */
export const JOINT_PARENT:Readonly<Record<string,string|undefined>>={pelvis:undefined,spine:'pelvis',head:'spine','left-shoulder':'spine','left-elbow':'left-shoulder',
 'right-shoulder':'spine','right-elbow':'right-shoulder','left-hip':'pelvis','left-knee':'left-hip','right-hip':'pelvis','right-knee':'right-hip'};
const JOINTS=Object.keys(JOINT_PARENT);
/** A part and every part below it. */
export const jointSubtree=(name:string)=>JOINTS.filter(j=>{for(let a:string|undefined=j;a;a=JOINT_PARENT[a])if(a===name)return true;return false;});
/** Ragdoll part → rig bone, in hierarchy order (parents first). */
const BONES:readonly [string,'pelvis'|'spine'|'head'|'leftArm'|'leftElbow'|'rightArm'|'rightElbow'|'leftLeg'|'leftKnee'|'rightLeg'|'rightKnee'][]=[['pelvis','pelvis'],['spine','spine'],['head','head'],
 ['left-shoulder','leftArm'],['left-elbow','leftElbow'],['right-shoulder','rightArm'],['right-elbow','rightElbow'],['left-hip','leftLeg'],['left-knee','leftKnee'],['right-hip','rightLeg'],['right-knee','rightKnee']];

/** What death-effects.ts hands over: a kill effect, optionally exposing its body. */
export interface KeptEffect{update(age:number):void;dispose():void;count:number;keep?(age:number):void;keepAge?:number;
 /** Ragdoll family: the part groups (children named after rig joints). Siren crush: its scene root. */ root?:T.Object3D;
 /** A scripted body (Siren crush victim). */ rig?:CharacterRig;
 /** Loose scene objects of the effect (blood pools, crush debris, flames): hidden as the scan cleans up. */ extras?:T.Object3D[]}
/** A kept body as the regeneration sees it. */
export interface Corpse{
 effect:KeptEffect;death:DeathPresentation;
 /** World matrix of a present joint (rig convention), undefined for a missing one. */ joint(name:string):T.Matrix4|undefined;
 /** Destroyed parts (severed or crushed subtree; the Siren-crushed head). */ missing:string[];
 /** Present body meshes (hologram overlay sources). */ meshes:T.Mesh[];
 /** Wound cells and cut caps (sealed by the scan). */ wounds:T.Object3D[];
 /** Wound-carved voxel parts (restored to their sculpt as the scan passes). */ carved:T.InstancedMesh[];
 /** Severed or thrown-free pieces lying away from the body (they dissolve). */ loose:T.Object3D[];
 /** Groups scaled away when the body dissolves (pad respawn). */ pieces:T.Object3D[];
 extras:T.Object3D[];
}
const _m=new T.Matrix4(),_p=new T.Vector3(),_q=new T.Quaternion(),_s=new T.Vector3(),_q2=new T.Quaternion(),_v=new T.Vector3();
/** The body of a kept effect, or undefined when it has none (then the member simply materialises). */
export function corpseOf(effect:KeptEffect,death:DeathPresentation):Corpse|undefined{
 const extras=(effect.extras||[]).filter(Boolean);
 if(effect.rig){const rig=effect.rig,root=effect.root||rig.group;root.updateMatrixWorld(true);
  const missing=rig.head.visible?[]:['head'],meshes:T.Mesh[]=[];rig.group.traverseVisible(o=>{if(o instanceof T.Mesh&&!o.userData.bodyWound)meshes.push(o);});
  return {effect,death,missing,meshes,wounds:[],carved:[],loose:[],pieces:[root],extras,
   joint:name=>{if(missing.includes(name))return undefined;const o=rig.group.getObjectByName(name);return o?o.matrixWorld.clone():undefined;}};}
 const root=effect.root;if(!root)return undefined;
 const parts=new Map(root.children.filter(c=>c.name in JOINT_PARENT).map(c=>[c.name,c]));if(!parts.has('pelvis')&&!parts.has('spine'))return undefined;
 const gone=typeof root.userData.gibPart==='string'?jointSubtree(root.userData.gibPart):[],missing=[...gone];
 const meshes:T.Mesh[]=[],wounds:T.Object3D[]=[],carved:T.InstancedMesh[]=[],loose:T.Object3D[]=[],pieces:T.Object3D[]=[];
 for(const [name,part] of parts){if(gone.includes(name)){loose.push(part);continue;}pieces.push(part);
  part.traverse(o=>{if(o.userData.bodyWound){wounds.push(o);return;}if(o instanceof T.Mesh&&o.visible){meshes.push(o);if(o instanceof T.InstancedMesh&&currentLayout(o)?.custom)carved.push(o);}});}
 root.updateMatrixWorld(true);
 return {effect,death,missing,meshes,wounds,carved,loose,pieces,extras,
  joint:name=>{const part=parts.get(name);if(!part||gone.includes(name))return undefined;const offset=part.userData.jointOffset as T.Quaternion|undefined;
   _q.copy(part.quaternion);if(offset)_q.multiply(offset);return new T.Matrix4().compose(part.position,_q,_s.set(1,1,1));}};
}
/** Visible meshes below a root, ignoring the root's own visibility (a live rig hidden for this frame). */
export function visibleMeshes(root:T.Object3D,out:T.Mesh[]=[]){for(const c of root.children){if(!c.visible)continue;if(c instanceof T.Mesh&&!c.userData.bodyWound)out.push(c);visibleMeshes(c,out);}return out;}
/** Rig bone for a ragdoll joint name. */
const rigJoint=(r:CharacterRig,name:string)=>{const b=BONES.find(x=>x[0]===name);return b?r[b[1]]:undefined;};
const isRig=(r:PveRigRef):r is PveRigRef&CharacterRig=>'pelvis' in r&&'body' in r&&'leftKnee' in r;

/** Hand the kept body's pose to the live rig (the pelvis where the body's lies, every joint rotation) and let the pose
 *  transition blend it into the live standing pose over `duration`: the player stands up from the body. */
export function standFrom(r:CharacterRig,joint:(name:string)=>T.Matrix4|undefined,duration:number):boolean{
 const pelvis=joint('pelvis');if(!pelvis)return false;
 r.group.updateMatrixWorld(true);pelvis.decompose(_p,_q,_s);
 r.body.getWorldQuaternion(_q2);r.pelvis.quaternion.copy(_q2.invert().multiply(_q));r.pelvis.updateMatrixWorld(true);
 r.pelvis.getWorldPosition(_v);_v.subVectors(_p,_v).applyQuaternion(r.group.getWorldQuaternion(_q2).invert()).divideScalar(r.group.scale.x||1);r.body.position.add(_v);
 r.group.updateMatrixWorld(true);
 for(const [name,bone] of BONES){if(name==='pelvis')continue;const m=joint(name),j=r[bone];if(!m||!j.parent)continue;
  m.decompose(_p,_q,_s);j.parent.getWorldQuaternion(_q2);j.quaternion.copy(_q2.invert().multiply(_q));j.updateMatrixWorld(true);}
 seedTransition(r,TRANSITION_BONES.map(b=>r[b].quaternion.clone()),r.body.position.clone(),duration);
 return true;
}

// ---- hologram ------------------------------------------------------------------------------------------------------
/** Shared animated scanline uniform (one program for every regeneration). */
const HOLO_TIME={value:0};
let holoMat:T.MeshBasicMaterial|undefined,flatMat:T.MeshBasicMaterial|undefined;
/** Cyan, additive, unlit; on WebGL horizontal world-space scanlines drift upward. The SVG path gets the flat colour. */
export function regenMaterial(compatibility=false){
 if(compatibility)return flatMat??=new T.MeshBasicMaterial({color:0xffffff,transparent:true,opacity:.55,depthWrite:false,toneMapped:false});
 if(holoMat)return holoMat;
 const m=new T.MeshBasicMaterial({color:0xffffff,transparent:true,opacity:.9,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false,fog:false});
 m.onBeforeCompile=s=>{s.uniforms.uHoloTime=HOLO_TIME;
  s.vertexShader='varying vec3 vHoloW;\n'+s.vertexShader.replace('#include <project_vertex>','#include <project_vertex>\nvec4 holoW=vec4(transformed,1.);\n#ifdef USE_INSTANCING\nholoW=instanceMatrix*holoW;\n#endif\nvHoloW=(modelMatrix*holoW).xyz;');
  s.fragmentShader='uniform float uHoloTime;\nvarying vec3 vHoloW;\n'+s.fragmentShader.replace('#include <dithering_fragment>','#include <dithering_fragment>\ngl_FragColor.rgb*=.5+.8*step(.5,fract(vHoloW.y*24.-uHoloTime*1.7));');};
 m.customProgramCacheKey=()=>'pve-regen-holo';
 return holoMat=m;
}
const _sa=new T.Vector3(),_sb=new T.Vector3(),BOX=new T.BoxGeometry(1,1,1),CYAN=new T.Color(DOWNED.color),WHITE=new T.Color(0xd8fbff),_c=new T.Color(),_c2=new T.Color();
interface Voxel{target:T.Matrix4;s:number;from:T.Vector3}
interface Overlay{src:T.Mesh;s:number}
const boxOf=(g:T.BufferGeometry)=>{if(!g.boundingBox)g.computeBoundingBox();return g.boundingBox!;};
/** World (or `space`-local) matrix of a mesh's box, inflated. */
function meshBox(mesh:T.Mesh,out:T.Matrix4,inflate=1.06,spaceInverse?:T.Matrix4){
 const b=boxOf(mesh.geometry);b.getCenter(_p);b.getSize(_s).multiplyScalar(inflate).max(_v.set(.02,.02,.02));
 out.compose(_p,_q.identity(),_s).premultiply(mesh.matrixWorld);if(spaceInverse)out.premultiply(spaceInverse);return out;
}
/** Voxel grid filling a box matrix (unit cube), at most `limit`; cell size near `size`. */
function fillVoxels(box:T.Matrix4,size:number,out:T.Matrix4[]){
 box.decompose(_p,_q,_s);const nx=Math.max(1,Math.round(_s.x/size)),ny=Math.max(1,Math.round(_s.y/size)),nz=Math.max(1,Math.round(_s.z/size));
 // The shell only: interior cells would stack additively into a white blob (and cost instances).
 for(let x=0;x<nx;x++)for(let y=0;y<ny;y++)for(let z=0;z<nz;z++){if(x>0&&y>0&&z>0&&x<nx-1&&y<ny-1&&z<nz-1)continue;
  const m=new T.Matrix4().compose(_v.set((x+.5)/nx-.5,(y+.5)/ny-.5,(z+.5)/nz-.5),_q2.identity(),new T.Vector3(.74/nx,.74/ny,.74/nz));out.push(m.premultiply(box));}
}

/** One instanced draw: a floor frame, the scan slab, overlay shells over present parts and converging voxels. Cells are in
 *  world space, or local to `space` (a live rig group) so they follow it. */
class HoloBody{
 readonly mesh:T.InstancedMesh;voxels:Voxel[]=[];overlay:Overlay[]=[];
 /** Scan axis (space coordinates): from `a` (s=0) to `b` (s=1); the scan pane's width and height across it. */ axis={a:new T.Vector3(),b:new T.Vector3(0,1,0),width:.9,height:.9};
 /** Floor rectangle corners (space coordinates, y = floor). */ floor={min:new T.Vector3(-.5,0,-.5),max:new T.Vector3(.5,0,.5)};
 private inverse=new T.Matrix4();
 constructor(parent:T.Object3D,capacity:number,compatibility:boolean,readonly space?:T.Object3D){
  this.mesh=new T.InstancedMesh(BOX,regenMaterial(compatibility),Math.max(1,capacity));this.mesh.name='pve-regen-holo';this.mesh.frustumCulled=false;this.mesh.renderOrder=4;
  this.mesh.instanceColor=new T.InstancedBufferAttribute(new Float32Array(Math.max(1,capacity)*3),3);this.mesh.count=0;this.mesh.matrixAutoUpdate=false;parent.add(this.mesh);
 }
 /** Scan coordinate of a point (space coordinates). */
 s(p:T.Vector3){_sa.subVectors(this.axis.b,this.axis.a);const l=_sa.lengthSq();return l>1e-6?clamp(_sb.subVectors(p,this.axis.a).dot(_sa)/l):0;}
 /** Overlay a set of meshes (their current matrices), computing scan coordinates. */
 setOverlay(meshes:readonly T.Mesh[]){if(this.space)this.inverse.copy(this.space.matrixWorld).invert();const at=new T.Vector3();
  this.overlay=meshes.slice(0,this.mesh.instanceMatrix.count-this.voxels.length-5).map(src=>{meshBox(src,_m,1,this.space?this.inverse:undefined);at.setFromMatrixPosition(_m);return {src,s:this.s(at)};});}
 private n=0;
 private put(m:T.Matrix4,color:T.Color,k:number){if(this.n>=this.mesh.instanceMatrix.count||k<=.002)return;this.mesh.setMatrixAt(this.n,m);this.mesh.setColorAt(this.n,_c.copy(color).multiplyScalar(k));this.n++;}
 update(age:number,o:{front:number;glow:number;overlay:(s:number,front:number)=>number;voxels:boolean;slab:boolean}){
  this.n=0;if(this.space){this.space.updateMatrixWorld(true);this.mesh.matrix.copy(this.space.matrixWorld);this.inverse.copy(this.space.matrixWorld).invert();}else this.mesh.matrix.identity();
  this.mesh.matrixWorldNeedsUpdate=true;const g=o.glow;
  // Floor frame: four thin bars.
  const {min,max}=this.floor,cx=(min.x+max.x)/2,cz=(min.z+max.z)/2,w=max.x-min.x,d=max.z-min.z,y=min.y+.015,pulse=.45+.2*Math.sin(age*14);
  for(const [x,z,sx,sz] of [[cx,min.z,w,.035],[cx,max.z,w,.035],[min.x,cz,.035,d],[max.x,cz,.035,d]] as const)this.put(_m.compose(_p.set(x,y,z),_q.identity(),_s.set(sx,.02,sz)),CYAN,g*pulse);
  // The scan slab, perpendicular to the axis at the front.
  if(o.slab&&o.front>0&&o.front<1){_v.subVectors(this.axis.b,this.axis.a);const l=_v.length();if(l>1e-4){_v.divideScalar(l);_p.copy(this.axis.a).addScaledVector(_v,o.front*l);
   // A faint pane across the axis with bright edges: up is world up projected off the axis (a lying body), else x.
   _sa.set(0,1,0).addScaledVector(_v,-_v.y);if(_sa.lengthSq()<1e-3)_sa.set(1,0,0).addScaledVector(_v,-_v.x);_sa.normalize();_sb.crossVectors(_sa,_v);
   const w=this.axis.width,h=this.axis.height,bar=.022,edge=(du:number,dw:number,sx:number,sz:number)=>{_m.makeBasis(_sb,_v,_sa).scale(_s.set(sx,.012,sz));_m.setPosition(_p.x+_sb.x*dw+_sa.x*du,_p.y+_sb.y*dw+_sa.y*du,_p.z+_sb.z*dw+_sa.z*du);this.put(_m,CYAN,g*.95);};
   _m.makeBasis(_sb,_v,_sa).scale(_s.set(w,.01,h)).setPosition(_p);this.put(_m,CYAN,g*.14);
   edge(h/2,0,w,bar);edge(-h/2,0,w,bar);edge(0,w/2,bar,h);edge(0,-w/2,bar,h);}}
  for(const v of this.overlay){const k=o.overlay(v.s,o.front);if(k<=0)continue;meshBox(v.src,_m,1.07,this.space?this.inverse:undefined);this.put(_m,CYAN,k*g);}
  if(o.voxels)for(const v of this.voxels){const arrive=voxelArrival(v.s),k=clamp((age-(arrive-.3))/.3);if(k<=0)continue;const e=1-(1-k)**3;
   // In flight: cyan; a short white flash as each lands; then a dim cyan lattice (shells overlap, so keep it low).
   v.target.decompose(_p,_q,_s);_p.addScaledVector(v.from,1-e);_s.multiplyScalar(.3+.7*e);const landed=clamp(1-(age-arrive)/.15);
   if(k<1)this.put(_m.compose(_p,_q,_s),CYAN,.5*g);else this.put(_m.compose(_p,_q,_s),landed>0?_c2.copy(CYAN).lerp(WHITE,landed):CYAN,(.32+.4*landed)*g);}
  this.mesh.count=this.n;this.mesh.instanceMatrix.needsUpdate=true;if(this.mesh.instanceColor)this.mesh.instanceColor.needsUpdate=true;
 }
 dispose(){this.mesh.removeFromParent();this.mesh.dispose();}
}
/** Seeded scatter for converging voxels: out from the body and up, 0.4-1.2 m. */
function scatter(seed:number,i:number,out:T.Vector3){let s=(seed^Math.imul(i+1,0x9e3779b1))>>>0;const r=()=>(s=(Math.imul(s,1664525)+1013904223)>>>0)/4294967296;
 const a=r()*Math.PI*2,u=r()*2-1,k=.4+.8*r(),h=Math.sqrt(1-u*u);return out.set(Math.cos(a)*h*k,Math.abs(u)*k*.8+.15,Math.sin(a)*h*k);}

// ---- the keeper ----------------------------------------------------------------------------------------------------
interface Regen{id:string;upAt:number;mode:'place'|'pad';corpse?:Corpse;onBody?:HoloBody;onRig?:HoloBody;handed:boolean;fired:Set<string>;
 scale:Map<T.Object3D,T.Vector3>;rigGroup?:T.Object3D}
/** Owner of kept bodies after their member is back up, and of the regeneration visuals. One per scene: death-effects.ts
 *  hands bodies over (adopt), PveVisuals draws (update) and hides or stands up the live rig (decorate). */
export class DownedBodies{
 readonly group=new T.Group();private waiting=new Map<string,Corpse>();private regens=new Map<string,Regen>();
 /** Snapshot time each live rig was last decorated (shown in third person) at. */
 private shown=new Map<string,number>();
 constructor(readonly scene:T.Scene){this.group.name='pve-downed';}
 /** True while death-effects.ts should keep this death's body. */
 kept(p:PlayerView,state:Snapshot){return bodyKept(p,pveOf(state));}
 /** A kept body whose member is back up. True: this keeper regenerates and later disposes it. False (a rewind, a
  *  missed window, no longer PvE): the caller disposes it as usual. */
 adopt(id:string,effect:KeptEffect,death:DeathPresentation,state:Snapshot):boolean{
  const view=pveOf(state),p=state.players.find(q=>q.id===id),up=view?.party[id]?.upAt;
  if(!view||!p||p.hp<=0||up===undefined||up<death.at-1e-6||regenAge(view,id,state.time)===undefined)return false;
  const corpse=corpseOf(effect,death);if(!corpse)return false;
  const old=this.waiting.get(id);if(old&&old.effect!==effect)old.effect.dispose();this.waiting.set(id,corpse);return true;
 }
 /** Draw every running regeneration; dispose finished ones. */
 update(state:Snapshot,view:PveView,ctx:PveVisualsContext){
  if(!this.group.parent)this.scene.add(this.group);
  const t=state.time,live=new Set<string>();HOLO_TIME.value=t;
  for(const p of state.players){const age=regenAge(view,p.id,t);if(age===undefined||p.hp<=0)continue;live.add(p.id);
   const upAt=view.party[p.id]!.upAt!;let r=this.regens.get(p.id);
   if(r&&r.upAt!==upAt){this.finish(r);r=undefined;}
   // Start once the presented clock has passed upAt: interpolated positions then show where the member came back
   // (in place, or on a pad). Until then the kept body stays as it lies and the live rig stays hidden.
   if(!r&&t-upAt<DOWNED.settle)continue;
   if(!r){r=this.start(p,upAt,state,ctx);if(!r)continue;}
   this.step(r,p,age,state,ctx);}
  for(const [id,r] of this.regens)if(!live.has(id))this.finish(r);
  // Bodies handed over without a regeneration to show (it ended before the first draw).
  for(const [id,c] of this.waiting)if(!this.regens.has(id)&&!live.has(id)){c.effect.dispose();this.waiting.delete(id);}
 }
 private start(p:PlayerView,upAt:number,state:Snapshot,ctx:PveVisualsContext):Regen|undefined{
  const corpse=this.waiting.get(p.id);this.waiting.delete(p.id);
  const rig=ctx.rigs.get(p.id),mode=regenMode(corpse?.death.pos,p.pos);
  const r:Regen={id:p.id,upAt,mode,corpse,handed:false,fired:new Set(),scale:new Map(),rigGroup:rig?.group};this.regens.set(p.id,r);
  const budget=ctx.quality<.5?DOWNED.voxels.low:DOWNED.voxels.high,seed=hash(p.id+':'+upAt);
  if(corpse&&!ctx.compatibility){corpse.effect.root?.updateMatrixWorld(true);
   const h=new HoloBody(this.group,5+corpse.meshes.length+(mode==='place'?budget:0),false);r.onBody=h;
   // Scan axis: feet (mean of the knees/hips) to the head (or the spine); the floor frame around the body.
   const at=(n:string)=>{const m=corpse.joint(n);return m?new T.Vector3().setFromMatrixPosition(m):undefined;};
   const feet=['left-knee','right-knee','left-hip','right-hip'].map(at).filter((v):v is T.Vector3=>!!v),top=at('head')??at('spine')??at('pelvis');
   const bounds=new T.Box3();for(const m of corpse.meshes){meshBox(m,_m,1);bounds.expandByPoint(_p.setFromMatrixPosition(_m));}
   if(bounds.isEmpty())bounds.setFromCenterAndSize(new T.Vector3(corpse.death.pos.x,corpse.death.pos.y-.6,corpse.death.pos.z),new T.Vector3(1,.4,1));
   // The scan runs feet → head over the body's own extent along that direction (a missing head ends it at the stump).
   const from=feet.length?feet.reduce((a,v)=>a.add(v),new T.Vector3()).divideScalar(feet.length):bounds.min.clone(),dir=(top??bounds.max).clone().sub(from);if(dir.lengthSq()<1e-6)dir.set(0,1,0);dir.normalize();
   let lo=Infinity,hi=-Infinity;for(const m of corpse.meshes){meshBox(m,_m,1);const d=_p.setFromMatrixPosition(_m).sub(from).dot(dir);lo=Math.min(lo,d);hi=Math.max(hi,d);}
   if(!(hi>lo)){lo=0;hi=1;}h.axis.a.copy(from).addScaledVector(dir,lo-.08);h.axis.b.copy(from).addScaledVector(dir,hi+.08);h.axis.width=.7;h.axis.height=.55;
   const floor=corpse.death.pos.y-.85;h.floor.min.set(bounds.min.x-.3,floor,bounds.min.z-.3);h.floor.max.set(bounds.max.x+.3,floor,bounds.max.z+.3);
   h.setOverlay(corpse.meshes);
   // Missing parts: the live rig's own meshes of that subtree, placed on the body's nearest present joint.
   if(mode==='place'&&corpse.missing.length&&rig&&isRig(rig)){rig.group.updateMatrixWorld(true);const boxes:T.Matrix4[]=[];
    for(const name of corpse.missing){const live=rig.group.getObjectByName(name);let a=JOINT_PARENT[name];while(a&&corpse.missing.includes(a))a=JOINT_PARENT[a];
     const anchor=a?corpse.joint(a):undefined,liveAnchor=a?rigJoint(rig,a):undefined;if(!live||!anchor||!liveAnchor||corpse.missing.includes(JOINT_PARENT[name]??''))continue;
     const rel=liveAnchor.matrixWorld.clone().invert();live.traverseVisible(o=>{if(o instanceof T.Mesh&&!o.userData.bodyWound)boxes.push(meshBox(o,new T.Matrix4(),1).premultiply(rel).premultiply(anchor));});}
    this.voxelise(h,boxes,budget,seed);}}
  // Materialising (a respawn pad, or no body to rebuild): voxels converge onto the live rig, which they follow.
  if(!ctx.compatibility&&(mode==='pad'||!corpse)&&rig&&isRig(rig)&&this.shown.get(p.id)===state.time){rig.group.updateMatrixWorld(true);const h=new HoloBody(this.group,5+budget+64,false,rig.group);r.onRig=h;
   const inverse=rig.group.matrixWorld.clone().invert(),meshes=visibleMeshes(rig.group),boxes=meshes.map(o=>meshBox(o,new T.Matrix4(),1,inverse));
   h.axis.a.set(0,-.85,0);h.axis.b.set(0,.95,0);h.axis.width=.8;h.floor.min.set(-.55,-.85,-.55);h.floor.max.set(.55,-.85,.55);
   this.voxelise(h,boxes,budget,seed);h.setOverlay(meshes);}
  return r;
 }
 private voxelise(h:HoloBody,boxes:T.Matrix4[],budget:number,seed:number){
  let volume=0;for(const b of boxes){b.decompose(_p,_q,_s);volume+=Math.abs(_s.x*_s.y*_s.z);}
  let size=clamp(Math.cbrt(volume/Math.max(1,budget))*1.05,.045,.16),cells:T.Matrix4[]=[];
  for(let tries=0;tries<4;tries++){cells=[];for(const b of boxes)fillVoxels(b,size,cells);if(cells.length<=budget)break;size*=1.18;}
  if(cells.length>budget){const step=cells.length/budget;cells=Array.from({length:budget},(_,i)=>cells[Math.floor(i*step)]);}
  const at=new T.Vector3();h.voxels=cells.map((target,i)=>{at.setFromMatrixPosition(target);return {target,s:h.s(at),from:scatter(seed,i,new T.Vector3())};});
 }
 private fire(r:Regen,key:string,ctx:PveVisualsContext,pos:T.Vector3|{x:number;y:number;z:number},count:number,power:number){
  if(r.fired.has(key))return;r.fired.add(key);ctx.burst({x:pos.x,y:pos.y,z:pos.z},DOWNED.color,Math.max(1,Math.round(count*(ctx.quality<.5?.5:1))),power);
 }
 private step(r:Regen,p:PlayerView,age:number,state:Snapshot,ctx:PveVisualsContext){
  const ph=regenPhase(age),c=r.corpse,thirdPerson=this.shown.get(p.id)===state.time;
  if(c){const dissolve=r.mode==='pad';
   const gone=dissolve?age>=DOWNED.dissolve:ph.rise;
   if(!gone){c.effect.keep?.(state.time-c.death.at);
    this.fire(r,'start',ctx,c.death.pos,14,2.2);
    const h=r.onBody,front=h?ph.scan:clamp(age/(DOWNED.scan*.8)),seal=front*1.2;
    // Wounds seal and carved parts regain their sculpt as the scan passes them; cut caps close.
    for(const w of c.wounds){w.getWorldPosition(_v);const s=h?h.s(_v):0,k=clamp((seal-s)/.12);if(k<=0)continue;this.shrink(r,w,k);if(k>=1&&!r.fired.has('w'+w.id))this.fire(r,'w'+w.id,ctx,_v.clone(),4,.9);}
    for(const m of c.carved){m.getWorldPosition(_v);if((h?h.s(_v):0)>seal||r.fired.has('c'+m.id))continue;r.fired.add('c'+m.id);const live=(r.rigGroup?.getObjectByName(m.name));const base=live&&currentLayout(live);if(base&&!base.custom)retagVoxelPart(m,base);}
    // Severed or thrown-free pieces dissolve where they lie; blood pools and fragments are cleaned up.
    for(const l of c.loose){const k=smooth((age-.08)/.4);if(k>0){l.getWorldPosition(_v);this.fire(r,'l'+l.id,ctx,_v.clone(),12,1.6);this.shrink(r,l,k);}}
    if(age>=.45)for(const e of c.extras)if(e.visible){e.visible=false;this.fire(r,'extras',ctx,{x:c.death.pos.x,y:c.death.pos.y-.7,z:c.death.pos.z},8,1.1);}
    if(dissolve){const k=smooth((age-.15)/(DOWNED.dissolve-.15));for(const piece of c.pieces)this.shrink(r,piece,k);}
    if(h)h.update(age,{front:dissolve?clamp(age/(DOWNED.dissolve*.6)):front,glow:dissolve?ph.glow*(1-smooth((age-.45)/(DOWNED.dissolve-.45))):ph.glow,slab:true,voxels:!dissolve,
     overlay:(s,f)=>f<s?0:.13+.5*clamp(1-(f-s)/.18)});}
   else{this.dropBody(r);if(!dissolve)this.fire(r,'up',ctx,{x:p.pos.x,y:p.pos.y,z:p.pos.z},12,1.8);}}
  // The live rig: materialising voxels before the rise; a shell that fades from the feet up during the stand-up.
  const rig=ctx.rigs.get(p.id);
  if(ph.rise&&!r.onRig&&rig&&!ctx.compatibility&&thirdPerson){rig.group.updateMatrixWorld(true);const h=new HoloBody(this.group,5+64,false,rig.group);r.onRig=h;const meshes=visibleMeshes(rig.group);
   h.axis.a.set(0,-.85,0);h.axis.b.set(0,.95,0);h.floor.min.set(-.55,-.85,-.55);h.floor.max.set(.55,-.85,.55);h.setOverlay(meshes);}
  if(r.onRig){const h=r.onRig;h.mesh.visible=ph.rise?thirdPerson:true;
   if(!ph.rise)h.update(age,{front:ph.scan,glow:ph.glow,slab:true,voxels:true,overlay:()=>0});
   else{if(r.mode==='pad'||!c)this.fire(r,'up',ctx,{x:p.pos.x,y:p.pos.y,z:p.pos.z},12,1.8);
    h.update(age,{front:ph.stand*1.25,glow:1-smooth(ph.stand),slab:false,voxels:false,overlay:(s,f)=>.32*(1-smooth((f-s)/.3))});}}
 }
 private shrink(r:Regen,o:T.Object3D,k:number){let base=r.scale.get(o);if(!base){base=o.scale.clone();r.scale.set(o,base);}o.scale.copy(base).multiplyScalar(Math.max(.001,1-k));o.visible=k<.999;}
 private dropBody(r:Regen){if(r.corpse){r.corpse.effect.dispose();r.corpse=undefined;}r.onBody?.dispose();r.onBody=undefined;}
 private finish(r:Regen){this.dropBody(r);r.onRig?.dispose();this.regens.delete(r.id);}
 /** Called by PveVisuals after the rig animation: false hides the live rig while its body regenerates (or materialises);
  *  on the first frame of the rise the kept body's pose is handed to the rig so the player stands up from it. */
 decorate(rig:PveRigRef,p:PlayerView,state:Snapshot,view:PveView):boolean{
  this.shown.set(p.id,state.time);const age=regenAge(view,p.id,state.time);if(age===undefined||p.hp<=0)return true;
  if(age<DOWNED.scan)return false;
  const r=this.regens.get(p.id);
  if(r&&!r.handed){r.handed=true;if(r.mode==='place'&&r.corpse&&isRig(rig))standFrom(rig,r.corpse.joint,Math.max(.2,DOWNED.stand-(age-DOWNED.scan)));}
  return true;
 }
 /** Running regenerations and bodies waiting for one (tests). */
 stats(){return {regens:[...this.regens.keys()],waiting:[...this.waiting.keys()],draws:[...this.regens.values()].reduce((n,r)=>n+(r.onBody?1:0)+(r.onRig?1:0),0)};}
 regen(id:string){const r=this.regens.get(id);return r?{mode:r.mode,corpse:!!r.corpse,voxels:(r.onBody?.voxels.length||0)+(r.onRig?.voxels.length||0),handed:r.handed}:undefined;}
 clear(){for(const r of [...this.regens.values()])this.finish(r);for(const c of this.waiting.values())c.effect.dispose();this.waiting.clear();this.shown.clear();this.group.removeFromParent();}
}
const keepers=new WeakMap<T.Scene,DownedBodies>();
/** The scene's keeper (death-effects.ts and PveVisuals share it). */
export function downedBodies(scene:T.Scene){let k=keepers.get(scene);if(!k){k=new DownedBodies(scene);keepers.set(scene,k);}return k;}
