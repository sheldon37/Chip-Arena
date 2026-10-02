// PvE chip pickups (design §4.3, §8.6, stream S6): the spinning chip a chip dispenser pops out, tinted in its drop tier's
// RARITIES colour (rainbow cycles hue), with an additive glow (marked "YOURS" for the owner) and a light beam for epic and
// above. At most 4 draws each and no lights. The pop is an arc from the machine slot to the rest point, pure in
// (at, born, t) where at = born − PRICES.dispenseDelay is the purchase.
import * as T from 'three';
import type {Vec} from '../../shared/config.js';
import type {DropTier,PvePickupView,PveView} from '../../shared/pve/contract.js';
import {PRICES} from '../../shared/pve/balance.js';
import {PVE_MACHINES} from '../../shared/pve/layout.js';
import {BEAM_TIERS,createChipModel,disposeChipModel,tierColor,tintChip,type ChipModel} from './chip-model.js';
import {HOLO_FONT,makeCanvas} from './hologram.js';
import {SLOT} from './machines.js';

export const PICKUP={scale:.62,pop:.45,hop:.75,spin:2.2,bob:.08,restY:.9} as const;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
/** Machine slot in world space (where a popping chip starts). */
export function slotPoint(m:number):Vec{const k=PVE_MACHINES[m]??PVE_MACHINES[0];return {x:k.x+Math.sin(k.yaw)*SLOT.z,y:SLOT.y,z:k.z+Math.cos(k.yaw)*SLOT.z};}
/** Pickup placement at time t (pure in at, born, t): hidden inside the machine until born − pop, then an arc from the slot
 *  to `rest` arriving at born, then a slow bob. `spin` is the y rotation (faster while popping). */
export function pickupPose(from:Vec,rest:Vec,born:number,t:number):{visible:boolean;pos:Vec;spin:number;k:number}{
 const start=born-PICKUP.pop;
 if(t<start)return {visible:false,pos:{...from},spin:0,k:0};
 if(t<born){const k=clamp((t-start)/PICKUP.pop),e=1-(1-k)*(1-k);
  return {visible:true,k,pos:{x:from.x+(rest.x-from.x)*e,y:from.y+(rest.y-from.y)*k+PICKUP.hop*Math.sin(Math.PI*k),z:from.z+(rest.z-from.z)*e},spin:(t-start)*PICKUP.spin*4};}
 return {visible:true,k:1,pos:{x:rest.x,y:rest.y+PICKUP.bob*Math.sin(t*2),z:rest.z},spin:born*PICKUP.spin*4+(t-born)*PICKUP.spin};
}
/** The purchase time implied by a pickup (S3 creates it at the use with born = at + dispenseDelay). */
export const pickupBoughtAt=(k:Pick<PvePickupView,'born'>)=>k.born-PRICES.dispenseDelay;

function glowTexture(label:boolean){
 const c=makeCanvas(256,256);if(!c)return new T.Texture();const {ctx}=c,g=ctx.createRadialGradient(128,96,0,128,96,96);
 g.addColorStop(0,'rgba(255,255,255,.95)');g.addColorStop(.25,'rgba(255,255,255,.45)');g.addColorStop(1,'rgba(255,255,255,0)');ctx.fillStyle=g;ctx.fillRect(0,0,256,192);
 if(label){ctx.font=`700 40px ${HOLO_FONT}`;ctx.textAlign='center';ctx.textBaseline='middle';ctx.shadowColor='rgba(255,255,255,.9)';ctx.shadowBlur=8;ctx.fillStyle='#ffffff';ctx.fillText('YOURS',128,222);}
 const t=new T.CanvasTexture(c.canvas as HTMLCanvasElement);t.colorSpace=T.SRGBColorSpace;return t;
}
let glows:{mine:T.Texture;other:T.Texture}|undefined;
const beamGeometry=new T.CylinderGeometry(.16,.3,7,12,1,true).translate(0,3.5,0);

class Pickup {
 readonly group=new T.Group();readonly chip:ChipModel=createChipModel();readonly glow:T.Sprite;readonly beam:T.Mesh<T.CylinderGeometry,T.MeshBasicMaterial>;mine=false;
 constructor(){
  glows??={mine:glowTexture(true),other:glowTexture(false)};this.group.name='pve-pickup';this.chip.group.scale.setScalar(PICKUP.scale);
  this.glow=new T.Sprite(new T.SpriteMaterial({map:glows.other,transparent:true,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false}));this.glow.scale.set(1.5,1.5,1);this.glow.position.y=-.18;
  this.beam=new T.Mesh(beamGeometry,new T.MeshBasicMaterial({transparent:true,opacity:.22,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false}));this.beam.position.y=-PICKUP.restY;
  this.group.add(this.beam,this.chip.group,this.glow);
 }
 draws(){let n=0;this.group.traverse(o=>{if(o.visible&&((o as T.Mesh).isMesh||(o as T.Sprite).isSprite))n++;});return n;}
 set(k:PvePickupView,t:number,me:string){
  const pose=pickupPose(slotPoint(k.m),k.pos,k.born,t);this.group.visible=pose.visible;if(!pose.visible)return;
  this.group.position.set(pose.pos.x,pose.pos.y,pose.pos.z);this.chip.group.rotation.set(.35,pose.spin,0);tintChip(this.chip,k.tier,t);
  const mine=k.owner===me;if(mine!==this.mine){this.mine=mine;(this.glow.material as T.SpriteMaterial).map=mine?glows!.mine:glows!.other;(this.glow.material as T.SpriteMaterial).needsUpdate=true;}
  const c=tierColor(k.tier,t),pulse=.8+.2*Math.sin(t*4);(this.glow.material as T.SpriteMaterial).color.copy(c);(this.glow.material as T.SpriteMaterial).opacity=(mine?.95:.5)*pulse;
  this.beam.visible=BEAM_TIERS.has(k.tier as DropTier)&&pose.k>=1;this.beam.material.color.copy(c);this.beam.material.opacity=(.16+.08*Math.sin(t*3))*(mine?1:.6);this.beam.position.y=-(pose.pos.y);
 }
 dispose(){disposeChipModel(this.chip);(this.glow.material as T.Material).dispose();this.beam.material.dispose();}
}
export class PickupsView {
 readonly group=new T.Group();private pool=new Map<string,Pickup>();
 constructor(parent:T.Object3D){this.group.name='pve-pickups';parent.add(this.group);}
 update(view:PveView,t:number,me:string){
  const live=new Set<string>();
  for(const k of view.pickups){live.add(k.id);let p=this.pool.get(k.id);if(!p){p=new Pickup();this.pool.set(k.id,p);this.group.add(p.group);}p.set(k,t,me);}
  for(const [id,p] of this.pool)if(!live.has(id)){this.group.remove(p.group);p.dispose();this.pool.delete(id);}
 }
 get(id:string){return this.pool.get(id);}
 clear(){for(const p of this.pool.values()){this.group.remove(p.group);p.dispose();}this.pool.clear();}
}
