// PvE chip model (design §8.6, stream S6): the spinning chip that chip dispensers pop out, tinted in the real RARITIES
// colours. A square package with pins (one Lambert draw, vertex colours) and a lit die with a rim trace (one Basic draw,
// tinted per tier; rainbow cycles hue). No lights: emissive looks come from unlit, untoned materials.
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {RARITIES} from '../../shared/content.js';
import type {DropTier} from '../../shared/pve/contract.js';

/** Hue cycle of the rainbow tier (turns per second). */
export const RAINBOW_HUE_RATE=.25;
const tmp=new T.Color();
/** The display colour of a drop tier at time t: RARITIES[tier].color, or a cycling hue for rainbow. */
export function tierColor(tier:DropTier,t:number,out=new T.Color()){
 if(tier==='rainbow')return out.setHSL(((t*RAINBOW_HUE_RATE)%1+1)%1,.95,.62);
 return out.set(RARITIES[tier].color);
}
/** Rainbow hue at time t, 0..1 (tests). */
export const rainbowHue=(t:number)=>((t*RAINBOW_HUE_RATE)%1+1)%1;
/** Tiers that also get a light beam column (epic and above). */
export const BEAM_TIERS:ReadonlySet<DropTier>=new Set(['epic','legendary','mythic','rainbow']);

function box(w:number,h:number,d:number,x:number,y:number,z:number,color:number){
 const g=new T.BoxGeometry(w,h,d);g.translate(x,y,z);const n=g.attributes.position.count,c=new Float32Array(n*3);tmp.setHex(color);
 for(let i=0;i<n;i++){c[i*3]=tmp.r;c[i*3+1]=tmp.g;c[i*3+2]=tmp.b;}g.setAttribute('color',new T.BufferAttribute(c,3));return g;
}
let shared:{body:T.BufferGeometry;die:T.BufferGeometry}|undefined;
/** Geometry of a unit chip (about 1 m across before scaling): package plus 4×5 pins, and the die with its rim and traces. */
export function chipGeometry(){
 if(shared)return shared;
 const parts:T.BufferGeometry[]=[box(.8,.1,.8,0,0,0,0x1d1a24),box(.62,.02,.62,0,.06,0,0x2b2733),box(.62,.02,.62,0,-.06,0,0x2b2733)];
 for(let side=0;side<4;side++)for(let i=0;i<5;i++){const u=-.32+i*.16,x=side<2?u:(side===2?-.47:.47),z=side<2?(side===0?-.47:.47):u;
  parts.push(box(side<2?.07:.16,.04,side<2?.16:.07,x,-.02,z,0x8d8699));}
 const lit:T.BufferGeometry[]=[];
 // Both faces carry the lit die, so the chip reads in its tier colour from every side as it spins.
 for(const f of [1,-1]){lit.push(box(.42,.03,.42,0,f*.085,0,0xffffff));const y=f*.072;
  for(const s of [-1,1]){lit.push(box(.66,.012,.02,0,y,s*.33,0xb0b0b0),box(.02,.012,.66,s*.33,y,0,0xb0b0b0));
   for(let i=0;i<3;i++){lit.push(box(.012,.01,.08,-.14+i*.14,y,s*.27,0x9a9a9a),box(.08,.01,.012,s*.27,y,-.14+i*.14,0x9a9a9a));}}}
 shared={body:mergeGeometries(parts)!,die:mergeGeometries(lit)!};
 return shared;
}
const BODY=new T.MeshLambertMaterial({vertexColors:true});
/** A chip model: group (scale it), body (Lambert, shared material) and die (Basic, own material to tint). */
export interface ChipModel{group:T.Group;tilt:T.Group;body:T.Mesh;die:T.Mesh<T.BufferGeometry,T.MeshBasicMaterial>}
export function createChipModel():ChipModel{
 const g=chipGeometry(),group=new T.Group();group.name='pve-chip';
 const body=new T.Mesh(g.body,BODY);body.castShadow=true;const die=new T.Mesh(g.die,new T.MeshBasicMaterial({vertexColors:true,toneMapped:false}));
 const tilt=new T.Group();tilt.add(body,die);group.add(tilt);return {group,tilt,body,die};
}
/** Tint the die for a tier at time t: exactly the tier colour (unlit and untoned, so it reads as lit). */
export function tintChip(m:ChipModel,tier:DropTier,t:number){tierColor(tier,t,m.die.material.color);}
export function disposeChipModel(m:ChipModel){m.die.material.dispose();}
