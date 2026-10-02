import * as T from 'three';
import {customLayout,type VoxelLayout,type UnitBox} from '../../voxel-kit.js';
import {carve,solidsOf,WOUND_COLORS,type Solid,type CarveSpec} from '../../body-wounds.js';
import {GLOW_FLAG,MINION_PALETTE as P,categoryColor,type MinionCategory} from './materials.js';
import {hash01,minionSeed,type PartCondition,type MinionFace} from './look.js';

/**
 * Corrupted part layouts. Each rig part keeps its mesh, name, joint and transform; only its voxel instances change
 * (voxel-kit retagVoxelPart), so every consumer of the rig (animation, IK, LOD, SVG fallback, ragdoll copies) keeps working.
 * A layout is the part's own default sculpt, then:
 *  - wounds carved with the body-damage carver (body-wounds.ts carve: real openings lined with flesh, torn rims),
 *  - thin "paint" voxels laid on the outer surface (rot blotches, grime, char, scorch, embers, cracks, eyes, stumps),
 *  - every box coloured by its instance colour as a multiplier of the category's base colour (materials.ts).
 * Layouts depend only on (part, size, condition, variant, rot level, face), never on the minion seed, and are cached:
 * the cache is bounded by parts x variants however many minions spawn, and all minions share the GPU buffers.
 */
type V3=T.Vector3;type RGB=[number,number,number];
const v3=(x=0,y=0,z=0)=>new T.Vector3(x,y,z);
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const lin=(hex:number,k=1):RGB=>{const c=new T.Color(hex);return [c.r*k,c.g*k,c.b*k];};
const mixRGB=(a:RGB,b:RGB,t:number):RGB=>[a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t];

/** A point on a part (metres from the part centre) with its outward normal: where a black-flame tongue grows. */
export interface MinionAnchor {pos:[number,number,number];normal:[number,number,number]}
export interface CorruptedPart {layout:VoxelLayout;anchors:MinionAnchor[];instances:number}
export interface PartRequest {name:string;category:MinionCategory;size:V3;base:VoxelLayout;condition:PartCondition;variant:number;rot:0|1|2;face?:MinionFace;jawless?:boolean}

interface Cell {min:V3;max:V3;rgb:RGB;glow:boolean}
interface Face {axis:0|1|2;sign:1|-1}
const FRONT:Face={axis:2,sign:1},BACK:Face={axis:2,sign:-1},TOP:Face={axis:1,sign:1},BOTTOM:Face={axis:1,sign:-1};
const side=(s:number):Face=>({axis:0,sign:s<0?-1:1});

// ---------------------------------------------------------------------------------------------------------------
// Value noise (deterministic, part-local metres).
function vnoise(seed:number,x:number,y:number,z:number){
 const xi=Math.floor(x),yi=Math.floor(y),zi=Math.floor(z),xf=x-xi,yf=y-yi,zf=z-zi,s=(t:number)=>t*t*(3-2*t),u=s(xf),v=s(yf),w=s(zf);
 const h=(a:number,b:number,c:number)=>hash01(seed,xi+a,yi+b,zi+c),l=(a:number,b:number,t:number)=>a+(b-a)*t;
 return l(l(l(h(0,0,0),h(1,0,0),u),l(h(0,1,0),h(1,1,0),u),v),l(l(h(0,0,1),h(1,0,1),u),l(h(0,1,1),h(1,1,1),u),v),w);
}
const fbm=(seed:number,p:V3,scale:number)=>.65*vnoise(seed,p.x/scale,p.y/scale,p.z/scale)+.35*vnoise(seed^0x5bd1e995,p.x/(scale*.4),p.y/(scale*.4),p.z/(scale*.4));

// ---------------------------------------------------------------------------------------------------------------
// Surface sampling on the solids of one part.
const axes=(f:Face)=>[(f.axis+1)%3,(f.axis+2)%3] as const;
/** Outer surface along a face normal at (u,v), and whether that outermost solid is wound flesh. */
function topAt(solids:Solid[],f:Face,u:number,v:number){
 const [ua,va]=axes(f);let best:number|undefined,flesh=false;
 for(const s of solids){if(u<=s.min.getComponent(ua)+1e-4||u>=s.max.getComponent(ua)-1e-4||v<=s.min.getComponent(va)+1e-4||v>=s.max.getComponent(va)-1e-4)continue;
  const at=f.sign>0?s.max.getComponent(f.axis):-s.min.getComponent(f.axis);if(best===undefined||at>best+1e-6){best=at;flesh=s.flesh;}}
 return best===undefined?undefined:{at:best*f.sign,flesh};
}
/** Grid of cell centres over a face (cell size g), clipped to the part. */
function grid(size:V3,f:Face,g:number,fn:(u:number,v:number,i:number,j:number)=>void){
 const [ua,va]=axes(f),su=size.getComponent(ua),sv=size.getComponent(va),nu=Math.max(1,Math.floor(su/g)),nv=Math.max(1,Math.floor(sv/g)),ou=-(nu*g)/2,ov=-(nv*g)/2;
 for(let j=0;j<nv;j++)for(let i=0;i<nu;i++)fn(ou+(i+.5)*g,ov+(j+.5)*g,i,j);
}
/** A paint cell lying on the surface found at (u,v): `proud` above it, sunk .005 into it so no seam shows at an angle. */
function cellOn(out:Cell[],solids:Solid[],f:Face,u:number,v:number,gu:number,gv:number,proud:number,rgb:RGB,glow=false,allowFlesh=false){
 const top=topAt(solids,f,u,v);if(!top||(top.flesh&&!allowFlesh))return false;
 const [ua,va]=axes(f),min=v3(),max=v3(),outer=top.at+f.sign*proud,inner=top.at-f.sign*.005;
 min.setComponent(ua,u-gu/2).setComponent(va,v-gv/2).setComponent(f.axis,Math.min(outer,inner));max.setComponent(ua,u+gu/2).setComponent(va,v+gv/2).setComponent(f.axis,Math.max(outer,inner));
 out.push({min,max,rgb,glow});return true;
}
/** The 3D point of a face cell (for noise). */
function facePoint(f:Face,u:number,v:number,w:number){const p=v3(),[ua,va]=axes(f);p.setComponent(ua,u).setComponent(va,v).setComponent(f.axis,w);return p;}
/** Greedy 2D merge of equal cells (same face depth span, colour and glow): runs along u, then equal runs along v. Broad
 * blotches become a few boxes instead of one instance per cell. */
function mergeCells(cells:Cell[]):Cell[]{
 const groups=new Map<string,Cell[]>(),out:Cell[]=[];
 for(const c of cells){const d=[c.max.x-c.min.x,c.max.y-c.min.y,c.max.z-c.min.z],a=d.indexOf(Math.min(...d));
  const key=`${a}|${c.min.getComponent(a).toFixed(4)}|${c.max.getComponent(a).toFixed(4)}|${c.rgb.map(x=>x.toFixed(4)).join(',')}|${c.glow}`;let g=groups.get(key);if(!g)groups.set(key,g=[]);g.push(c);}
 for(const [key,list] of groups){const a=Number(key[0]),ua=(a+1)%3,va=(a+2)%3,eps=1e-5;
  list.sort((p,q)=>p.min.getComponent(va)-q.min.getComponent(va)||p.min.getComponent(ua)-q.min.getComponent(ua));
  const rows:Cell[]=[];for(const c of list){const prev=rows[rows.length-1];
   if(prev&&Math.abs(prev.min.getComponent(va)-c.min.getComponent(va))<eps&&Math.abs(prev.max.getComponent(va)-c.max.getComponent(va))<eps&&Math.abs(prev.max.getComponent(ua)-c.min.getComponent(ua))<eps)prev.max.setComponent(ua,c.max.getComponent(ua));
   else rows.push({min:c.min.clone(),max:c.max.clone(),rgb:c.rgb,glow:c.glow});}
  rows.sort((p,q)=>p.min.getComponent(ua)-q.min.getComponent(ua)||p.max.getComponent(ua)-q.max.getComponent(ua)||p.min.getComponent(va)-q.min.getComponent(va));
  let open:Cell|undefined;for(const r of rows){
   if(open&&Math.abs(open.min.getComponent(ua)-r.min.getComponent(ua))<eps&&Math.abs(open.max.getComponent(ua)-r.max.getComponent(ua))<eps&&Math.abs(open.max.getComponent(va)-r.min.getComponent(va))<eps)open.max.setComponent(va,r.max.getComponent(va));
   else{if(open)out.push(open);open=r;}}
  if(open)out.push(open);}
 return out;
}

// ---------------------------------------------------------------------------------------------------------------
// Palettes per category: absolute (linear) colours; converted to multipliers of the category colour at the end.
const C={
 rotDark:lin(P.rotDark),rotOlive:lin(P.rotOlive),rotBrown:lin(P.rotBrown),ooze:lin(P.ooze),grime:lin(P.grime),blood:lin(P.blood),
 char:lin(P.char),soot:lin(P.soot),scorch:lin(P.scorch),ember:lin(P.ember,.75),emberHot:lin(P.emberHot),crack:lin(P.crack,.7),eye:lin(P.eye),bone:lin(P.bone),
 flesh:lin(WOUND_COLORS.shallow),fleshMid:lin(WOUND_COLORS.mid),fleshDeep:lin(WOUND_COLORS.deep),core:lin(WOUND_COLORS.core),
};
const ROT_THRESHOLD=[.64,.58,.52];
/** Decay gathers low on the body (legs, boots, hips) and thins out on the head, chest and arms: a corpse, not a pattern. */
const REGION_BIAS=(name:string)=>/thigh|shin|calf|knee|foot|heel|hip/.test(name)?-.07:/head|crown|collar|upper-chest|upper-arm/.test(name)?.07:0;

/** Faces that show rot paint for a part (the four sides; tops on parts seen from above). */
function rotFaces(name:string,s:number):Face[]{
 const faces=[FRONT,BACK,side(1),side(-1)];if(/^(crown|upper-chest|collar)$|shoulder-cap$/.test(name))faces.push(TOP);return name==='crown'?[TOP,FRONT,side(s)]:faces;
}
/** Which face a burn or wound of this variant lands on. */
function featureFace(name:string,variant:number,s:number,wound:boolean):Face{
 if(name==='back-panel')return BACK;
 if(name==='blank-head')return [side(1),side(-1),BACK][variant%3];
 // Mostly on the front (what players see coming at them), sometimes the outer side, rarely the back.
 const torso=/^(ribcage|abdomen-shell|hip-shell)$/.test(name),k=(variant+(wound?1:0))%3;
 return k===2&&!wound?BACK:k===1?(torso?side(wound?-1:1):side(s)):FRONT;
}

type Spot={f:Face;u:number;v:number;r:number};
function paintRot(out:Cell[],solids:Solid[],req:PartRequest,seed:number,clear:Spot[]){
 // Decay is uneven: some parts stay almost clean, others are eaten through, and rot spreads around burns and wounds.
 const big=req.size.x*req.size.y>.03||req.size.y*req.size.z>.03,g=big?.05:.04,s=req.name.startsWith('right-')?-1:1,low=-req.size.y/2,cat=req.category,thr0=ROT_THRESHOLD[req.rot]+.2*(.5-hash01(seed,50))+REGION_BIAS(req.name);
 const skip=(f:Face,u:number,v:number)=>clear.some(c=>c.f.axis===f.axis&&c.f.sign===f.sign&&Math.hypot(u-c.u,v-c.v)<c.r+.025);
 const near=(f:Face,u:number,v:number)=>{let k=0;for(const c of clear)if(c.f.axis===f.axis&&c.f.sign===f.sign)k=Math.max(k,1-Math.hypot(u-c.u,v-c.v)/(c.r*2.4+.05));return Math.max(0,k);};
 for(const f of rotFaces(req.name,s))grid(req.size,f,g,(u,v)=>{
  if(skip(f,u,v))return;const top=topAt(solids,f,u,v);if(!top||top.flesh)return;const thr=thr0-.28*near(f,u,v),p=facePoint(f,u,v,top.at),n=fbm(seed,p,.15),n2=vnoise(seed^0x2f,p.x/.05,p.y/.05,p.z/.05),h=hash01(seed,Math.round(p.x*400),Math.round(p.y*400),Math.round(p.z*400));
  // Rot: broad blotches with darker cores (gloves and boots just get filthy); joints show dried flesh and blood.
  let rgb:RGB|undefined,proud=.003;
  if(cat==='flex'){if(n>thr-.04)rgb=n>thr+.1?C.blood:C.rotBrown;}
  else if(n>thr+.15){rgb=cat==='shell'?C.rotDark:C.rotBrown;proud=.005;}
  else if(n>thr)rgb=cat==='shell'?C.rotOlive:C.grime;
  // Grime gathers toward the lower end of every part; a few sickly ooze specks.
  if(!rgb&&f.axis!==1&&p.y<low+req.size.y*.22&&n2>.6-.08*req.rot)rgb=C.grime;
  if(cat==='shell'&&req.rot===2&&h>.993){rgb=C.ooze;proud=.006;}
  if(rgb)cellOn(out,solids,f,u,v,g,g,proud,rgb);
 });
}
/** Charred patch with an ember-cracked core, sooty ring and scorched rim. Returns where its tongue grows. */
function paintBurn(out:Cell[],solids:Solid[],req:PartRequest,seed:number,f:Face,center?:[number,number]):{anchor?:MinionAnchor;u:number;v:number;r:number}{
 const g=.025,[ua,va]=axes(f),su=req.size.getComponent(ua),sv=req.size.getComponent(va);
 const u0=center?center[0]:(hash01(seed,1)-.5)*su*.3,v0=center?center[1]:(hash01(seed,2)-.5)*sv*.3,r=clamp(.5*Math.min(su,sv),.06,.125);
 grid(req.size,f,g,(u,v)=>{
  const top=topAt(solids,f,u,v);if(!top||top.flesh)return;const p=facePoint(f,u,v,top.at),wob=1+.38*(vnoise(seed,p.x/.04,p.y/.04,p.z/.04)-.5),d=Math.hypot(u-u0,v-v0)/(r*wob);if(d>1)return;
  const h=hash01(seed,Math.round(u*400),Math.round(v*400),f.axis*2+f.sign),crack=Math.abs(vnoise(seed^0x77,p.x/.028,p.y/.028,p.z/.028)-.5);
  if(d<.55){if(crack<.07)cellOn(out,solids,f,u,v,g,g,.006,mixRGB(C.ember,C.emberHot,h<.3?.6:0),true);else cellOn(out,solids,f,u,v,g,g,.006+(h<.35?.006:0),h<.5?C.char:mixRGB(C.char,C.soot,.5));}
  else if(d<.82){if(h<.14)cellOn(out,solids,f,u,v,g,g,.005,mixRGB(C.emberHot,C.ember,.35),true);else cellOn(out,solids,f,u,v,g,g,.004,C.soot);}
  else cellOn(out,solids,f,u,v,g,g,.003,C.scorch);
 });
 const top=topAt(solids,f,u0,v0);if(!top)return {u:u0,v:v0,r};
 const pos=facePoint(f,u0,v0,top.at+f.sign*.004),normal=v3().setComponent(f.axis,f.sign);
 return {anchor:{pos:[pos.x,pos.y,pos.z],normal:[normal.x,normal.y,normal.z]},u:u0,v:v0,r};
}
/** Carves a wound (cavity, slash or a pair of punctures) and returns where it is so rot paint keeps clear of it. */
function carveWound(solids:Solid[],req:PartRequest,seed:number,f:Face):{solids:Solid[];u:number;v:number;r:number}{
 const [ua,va]=axes(f),su=req.size.getComponent(ua),sv=req.size.getComponent(va),span=Math.min(su,sv),kind=(['cavity','cut','puncture'] as const)[req.variant%3];
 const at=(du:number,dv:number)=>{const hit=v3().setComponent(f.axis,f.sign*req.size.getComponent(f.axis)/2).setComponent(ua,du).setComponent(va,dv);return hit;};
 const dir=v3().setComponent(f.axis,-f.sign),u=(hash01(seed,3)-.5)*su*.28,v=(hash01(seed,4)-.5)*sv*.28;
 const spec=(hit:V3,k:CarveSpec['kind'],radius:number,severity:number,salt:number):CarveSpec=>({hit,axis:f.axis,sign:f.sign,dir:dir.clone(),size:req.size.clone(),kind:k,trauma:k==='cut'?'cut':k==='cavity'?'blunt':'bullet',radius,severity,seed:(seed+salt)>>>0,low:true,body:[1,1,1],vertical:(hash01(seed,5)-.5)*.8});
 if(kind==='puncture'){const r=clamp(span*.13,.018,.026),off=span*.18;solids=carve(solids,spec(at(u-off*.6,v+off*.3),'puncture',r,.8,1));solids=carve(solids,spec(at(u+off*.5,v-off*.4),'puncture',r*.85,.7,2));return {solids,u,v,r:off+r};}
 const r=kind==='cut'?clamp(span*.36,.045,.085):clamp(span*.36,.04,.08);solids=carve(solids,spec(at(u,v),kind,r,.85,0));return {solids,u,v,r:kind==='cut'?r*1.5:r*1.1};
}
/** A blood/rot run below a wound on a vertical face. */
function paintDrip(out:Cell[],solids:Solid[],req:PartRequest,seed:number,f:Face,u:number,v:number,r:number){
 if(f.axis===1)return;const [ua]=axes(f),g=.022,len=3+Math.floor(hash01(seed,9)*4),down=ua===1;
 for(let k=0;k<len;k++){const jitter=(hash01(seed,10,k)-.5)*g*.8,uu=down?u-r-(k+.5)*g:u+jitter,vv=down?v+jitter:v-r-(k+.5)*g;cellOn(out,solids,f,uu,vv,g*(k===len-1?.7:1),g,.0025,k<2?C.fleshDeep:C.blood);}
}
/** Torn stump on the part's lower face: flesh, a protruding bone and a burnt, ragged rim. */
function paintStump(out:Cell[],solids:Solid[],req:PartRequest,seed:number){
 // The bone sits off-centre on a cell corner (a jagged 2x2 end, never a centred '+' glyph).
 const g=.022,sx=req.size.x,sz=req.size.z,odd=(n:number)=>Math.max(1,Math.floor(n/g))%2===1,bu=odd(sz)?(hash01(seed,31)<.5?.5:-.5)*g:0,bv=odd(sx)?(hash01(seed,32)<.5?.5:-.5)*g:0;
 grid(req.size,BOTTOM,g,(u,v)=>{const top=topAt(solids,BOTTOM,u,v);if(!top)return;
  // BOTTOM: u along z, v along x.
  const d=Math.hypot(u/(sz/2),v/(sx/2)),h=hash01(seed,Math.round(u*300),Math.round(v*300),3);
  if(Math.hypot((u-bu)/(sz/2),(v-bv)/(sx/2))<.34)cellOn(out,solids,BOTTOM,u,v,g,g,.045+(h<.5?.015:0),C.bone,false,true);
  else if(d<.72)cellOn(out,solids,BOTTOM,u,v,g,g,.008+.006*h,h<.4?C.flesh:h<.75?C.fleshMid:C.fleshDeep,false,true);
  else cellOn(out,solids,BOTTOM,u,v,g,g,.004,h<.5?C.char:C.soot,false,true);});
 // A ragged burnt band around the cut, of uneven height.
 for(const f of [FRONT,BACK,side(1),side(-1)])grid(req.size,f,g,(u,v)=>{const [ua]=axes(f),y=ua===1?u:v,h=hash01(seed,Math.round(u*300),Math.round(v*300),f.axis*2+f.sign);if(y<-req.size.y/2+g*(1+Math.floor(h*2.6)))cellOn(out,solids,f,u,v,g,g,.003,h<.3?C.blood:h<.7?C.soot:C.char);});
}
/** The bare lower head where the jaw plate was torn off: a row of teeth, a dark hollow, blood. */
function paintJawless(out:Cell[],solids:Solid[],req:PartRequest,seed:number){
 const g=.024,sz=req.size.z;
 grid(req.size,BOTTOM,g,(u,v,i)=>{const h=hash01(seed,i,Math.round(v*300),11);
  if(u>sz/2-g*1.5&&Math.abs(v)<.11){if(i%2===0||h<.2)cellOn(out,solids,BOTTOM,u,v,g*.8,g*.8,.03+.012*h,C.bone,false,true);}
  else if(u>-sz*.15)cellOn(out,solids,BOTTOM,u,v,g,g,.004,h<.6?C.core:C.fleshDeep,false,true);
  else cellOn(out,solids,BOTTOM,u,v,g,g,.003,C.soot,false,true);});
 grid(req.size,FRONT,g,(u,v,i)=>{if(v<-req.size.y/2+g*1.2&&Math.abs(u)<.13)cellOn(out,solids,FRONT,u,v,g,g,.003,hash01(seed,i,4)<.5?C.blood:C.fleshDeep);});
}
/** Burned-in Siren eyes: dark sockets with faint white slanted glows. */
function paintEyes(out:Cell[],solids:Solid[],req:PartRequest,seed:number){
 const g=.025,y=.041,glow:RGB=[C.eye[0]*.5,C.eye[1]*.5,C.eye[2]*.5];
 for(const sx of [1,-1]){const x=sx*.082;
  for(let i=-1;i<=1;i++)for(const j of [-.5,.5])cellOn(out,solids,FRONT,x+i*g,y+j*g-.004,g,g,.004,j>0&&i===sx?C.soot:C.char);
  // The inner glow cell sits lower: the slant of the Siren's eyes, in voxels.
  cellOn(out,solids,FRONT,x+.5*g*sx,y-.002,g,g*.7,.007,glow,true);
  cellOn(out,solids,FRONT,x-.5*g*sx,y-.01,g,g*.7,.007,glow,true);
  if(hash01(seed,sx+3)<.6)for(let k=0;k<2+Math.floor(hash01(seed,sx+5)*2);k++)cellOn(out,solids,FRONT,x+(hash01(seed,sx,k)-.5)*g,y-(1.4+k)*g,g*.7,g,.003,C.soot);
 }
}
/** A split down the head shell leaking black-flame light; continues across the crown (the tongue grows there). */
function paintCrack(out:Cell[],solids:Solid[],req:PartRequest,seed:number):MinionAnchor|undefined{
 const g=.02;
 if(req.name==='blank-head'){let x=-.05+.03*hash01(seed,1);
  for(let y=req.size.y/2-g/2;y>-.02;y-=g){x+=(hash01(seed,2,Math.round(y*100))-.5)*g*1.6;const wide=y>.06;
   cellOn(out,solids,FRONT,x,y,g,g,.006,C.crack,true);cellOn(out,solids,FRONT,x-g,y,g,g,.005,C.char);cellOn(out,solids,FRONT,x+g,y,g,g,.005,wide?C.char:C.soot);if(wide)cellOn(out,solids,FRONT,x+2*g,y,g,g,.004,C.soot);}
  return undefined;}
 // Crown: the split runs from the front edge to the middle; the tongue grows from its widest point.
 let x=-.04;for(let z=req.size.z/2-g/2;z>-.04;z-=g){x+=(hash01(seed,3,Math.round(z*100))-.5)*g*1.2;
  // TOP: u along z, v along x.
  cellOn(out,solids,TOP,z,x,g,g,.006,C.crack,true);cellOn(out,solids,TOP,z,x-g,g,g,.005,C.char);cellOn(out,solids,TOP,z,x+g,g,g,.005,C.char);}
 const top=topAt(solids,TOP,.02,x);return top?{pos:[x,top.at+.004,.02],normal:[0,1,0]}:undefined;
}
/** Dead armor plates: cracked, with the Siren's pale violet light flickering in the cracks. */
function paintCracks(out:Cell[],solids:Solid[],req:PartRequest,seed:number){
 // One crack across the plate's outward face: a glowing core with a charred lip, dark at its ends.
 const g=.02,s=req.name.startsWith('right-')?-1:1,f=/shoulder-cap|cuff/.test(req.name)?side(s):FRONT,[ua,va]=axes(f),su=req.size.getComponent(ua),sv=req.size.getComponent(va);
 if(Math.min(su,sv)<g*1.5)return;const vertical=sv>=su*.6,len=vertical?sv:su,steps=Math.max(3,Math.floor(len/g)-1);
 let a=(hash01(seed,1)-.5)*(vertical?su:sv)*.5;const drift=(hash01(seed,2)-.5)*.9,lipSide=hash01(seed,3)<.5?-1:1;
 for(let k=0;k<steps;k++){const b=len/2-g/2-k*g,u=vertical?a:b,v=vertical?b:a,end=k===0||k===steps-1,lit=!end&&hash01(seed,k,4)<.9;
  cellOn(out,solids,f,u,v,g,g,.004,lit?C.crack:C.char,lit);
  const lu=vertical?u+lipSide*g:u,lv=vertical?v:v+lipSide*g;if(!end)cellOn(out,solids,f,lu,lv,g,g,.003,C.char);
  a=clamp(a+(drift+(hash01(seed,k,5)-.5)*1.1)*g,-(vertical?su:sv)/2+g,(vertical?su:sv)/2-g);}
 // Grime on the dead plate.
 grid(req.size,f,.03,(u,v)=>{const h=hash01(seed,Math.round(u*300),Math.round(v*300),20);if(h<.14)cellOn(out,solids,f,u,v,.03,.03,.003,C.grime);});
}
/** Glow strips (armor-*): split along their length into failing segments. */
function stripCells(base:VoxelLayout,size:V3,seed:number,dead:boolean):Cell[]{
 const out:Cell[]=[];
 for(const s of solidsOf(base,size)){const dims=[s.max.x-s.min.x,s.max.y-s.min.y,s.max.z-s.min.z],a=dims.indexOf(Math.max(...dims)),n=Math.max(1,Math.round(dims[a]/.025));
  for(let i=0;i<n;i++){const min=s.min.clone(),max=s.max.clone();min.setComponent(a,s.min.getComponent(a)+dims[a]*i/n);max.setComponent(a,s.min.getComponent(a)+dims[a]*(i+1)/n);
   const h=hash01(seed,i,a,Math.round(s.min.y*100)),lit=!dead&&h>.3;out.push({min,max,rgb:lit?[1,1,1]:[.07,.06,.09],glow:lit});}}
 return out;
}

// ---------------------------------------------------------------------------------------------------------------
const cache=new Map<string,CorruptedPart>();
export const corruptedLayoutCount=()=>cache.size;
export function corruptedKey(req:PartRequest){
 const headish=req.name==='blank-head'||req.name==='crown';
 return `minion|${req.name}|${req.size.toArray().map(x=>x.toFixed(4)).join(',')}|${req.condition}|${req.variant}|${req.rot}${headish?'|'+(req.face||'blank'):''}${req.jawless?'|jawless':''}`;
}
/** The corrupted layout of one part (cached). */
export function corruptedPart(req:PartRequest):CorruptedPart{
 const key=corruptedKey(req);let hit=cache.get(key);if(hit)return hit;
 hit=build(req,key);cache.set(key,hit);return hit;
}

function build(req:PartRequest,key:string):CorruptedPart{
 const seed=(minionSeed(req.name)^Math.imul(req.variant+1,0x9e3779b1)^Math.imul(req.rot+1,0x85ebca77))>>>0,s=req.name.startsWith('right-')?-1:1;
 const base=categoryColor(req.category),baseRGB:RGB=[base.r,base.g,base.b],anchors:MinionAnchor[]=[];
 // Each part gets its own slight tint and brightness so a body never reads as one flat colour.
 const tint:RGB=req.category==='accent'||req.category==='strip'?[1,1,1]:(()=>{const k=.86+.22*hash01(seed,40);return [k*(1+.07*(hash01(seed,41)-.5)),k*(1+.06*(hash01(seed,42)-.5)),k*(1+.08*(hash01(seed,43)-.5))] as RGB;})();
 let cells:Cell[]=[];
 if(req.category==='strip')cells=stripCells(req.base,req.size,seed,req.condition==='dead');
 else{
  let solids=solidsOf(req.base,req.size);const clear:{f:Face;u:number;v:number;r:number}[]=[];
  if(req.condition==='wound'){const f=featureFace(req.name,req.variant,s,true),w=carveWound(solids,req,seed,f);solids=w.solids;clear.push({f,u:w.u,v:w.v,r:w.r});}
  const paint:Cell[]=[];
  if(req.condition==='burn'){const f=featureFace(req.name,req.variant,s,false),b=paintBurn(paint,solids,req,seed,f);if(b.anchor)anchors.push(b.anchor);clear.push({f,u:b.u,v:b.v,r:b.r});}
  if(req.condition==='stump')paintStump(paint,solids,req,seed);
  if(req.jawless)paintJawless(paint,solids,req,seed);
  if(req.face==='eyes'&&req.name==='blank-head'){paintEyes(paint,solids,req,seed);clear.push({f:FRONT,u:.082,v:.041,r:.06},{f:FRONT,u:-.082,v:.041,r:.06});}
  // Only the head and crown take the face (the cache key carries it for those two parts alone).
  if(req.face==='cracked'&&(req.name==='blank-head'||req.name==='crown')){const a=paintCrack(paint,solids,req,seed);if(a)anchors.push(a);if(req.name==='blank-head')clear.push({f:FRONT,u:-.04,v:.08,r:.07});}
  if(req.category==='accent'){if(req.condition!=='dead')paintCracks(paint,solids,req,seed);}
  else paintRot(paint,solids,req,seed,clear);
  if(req.condition==='wound'){const w=clear[0];paintDrip(paint,solids,req,seed,w.f,w.u,w.v,w.r*.8);}
  // The sculpt and carved flesh first, then paint (later cells win where they overlap nothing: paint sits proud).
  // Carved flesh colours are quantized (perceptual steps) so equal neighbours merge into larger boxes.
  const carved=solids.map(b=>({min:b.min,max:b.max,rgb:(b.flesh?b.color.map(quant):[b.color[0]*tint[0]*baseRGB[0],b.color[0]*tint[1]*baseRGB[1],b.color[0]*tint[2]*baseRGB[2]]) as RGB,glow:false}));
  cells.push(...(req.condition==='wound'?mergeCells(carved):carved));
  cells.push(...mergeCells(paint.map(c=>({...c,rgb:c.glow?c.rgb:mul(c.rgb,tint)}))));
 }
 // Absolute colours to multipliers of the category colour; glow cells carry the flag.
 const unit:UnitBox[]=cells.map(c=>{const ctr=c.min.clone().add(c.max).multiplyScalar(.5),e=c.max.clone().sub(c.min);
  const m=req.category==='strip'?c.rgb:[c.rgb[0]/Math.max(1e-4,baseRGB[0]),c.rgb[1]/Math.max(1e-4,baseRGB[1]),c.rgb[2]/Math.max(1e-4,baseRGB[2])];
  const color=c.glow?m.map(x=>x+GLOW_FLAG):[Math.min(m[0],GLOW_FLAG-1),m[1],m[2]];
  return {box:[ctr.x/req.size.x,ctr.y/req.size.y,ctr.z/req.size.z,e.x/req.size.x,e.y/req.size.y,e.z/req.size.z],color};});
 const plainColor=req.category==='strip'?(req.condition==='dead'?[.07,.06,.09]:[1+GLOW_FLAG,1+GLOW_FLAG,1+GLOW_FLAG]):tint;
 const plain=customLayout(key+'|plain',[{box:[0,0,0,1,1,1],color:plainColor}]);plain.custom=false;
 const layout=customLayout(key,unit,{plain,ghost:plain});layout.custom=false;
 return {layout,anchors,instances:unit.length};
}
const mul=(a:RGB,b:RGB):RGB=>[a[0]*b[0],a[1]*b[1],a[2]*b[2]];
const quant=(x:number)=>{const q=Math.round(Math.sqrt(Math.max(0,x))*10)/10;return q*q;};
