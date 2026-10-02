// PvE holograms (design §5.5, §8.6, stream S6): the "choosing a chip" hologram over remote players whose personal chip
// screen is open (a fan of cards, a spinning chip and a projector cone; it blinks red while they are under attack), the
// choosing pose, and the small holo tags (BOT over party bots, role badges over recruits). Cyan, unlit, additive,
// depth-tested, no lights. Canvas text is optional: without a DOM (tests) labels draw nothing but keep their geometry.
import * as T from 'three';
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {PVE_TIMING,decodeAdd,type AddAttack,type PveView,type RecruitRole} from '../../shared/pve/contract.js';
import {sirenOf} from '../../shared/siren-contract.js';
import {chipGeometry} from './chip-model.js';
import type {PveRigRef} from './visuals.js';

export const HOLO={cyan:0x5fe8ff,red:0xff2f44,height:.55,maxDistance:55,attackedSeconds:1.2,blinkHz:6} as const;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));

// ---- canvas labels ----------------------------------------------------------------------------------------------
type Canvas2D={canvas:HTMLCanvasElement|OffscreenCanvas;ctx:CanvasRenderingContext2D|OffscreenCanvasRenderingContext2D};
/** A 2D canvas when the platform has one (browser); null in Node tests. */
export function makeCanvas(w:number,h:number):Canvas2D|null{
 try{
  if(typeof document!=='undefined'){const c=document.createElement('canvas');c.width=w;c.height=h;const ctx=c.getContext('2d');return ctx?{canvas:c,ctx}:null;}
  if(typeof OffscreenCanvas!=='undefined'){const c=new OffscreenCanvas(w,h),ctx=c.getContext('2d');return ctx?{canvas:c,ctx}:null;}
 }catch{/* no canvas */}
 return null;
}
export const HOLO_FONT='"Rajdhani","Bahnschrift","Arial Narrow",ui-sans-serif,sans-serif';
/** A camera-facing text label that redraws only when its text changes. */
export class HoloLabel {
 readonly sprite:T.Sprite;private canvas:Canvas2D|null;private texture:T.Texture;private text='';
 constructor(readonly w=256,readonly h=64,private style:{color?:string;glow?:string;size?:number;sub?:number}={}){
  this.canvas=makeCanvas(w,h);this.texture=this.canvas?new T.CanvasTexture(this.canvas.canvas as HTMLCanvasElement):new T.Texture();this.texture.colorSpace=T.SRGBColorSpace;
  this.sprite=new T.Sprite(new T.SpriteMaterial({map:this.texture,transparent:true,depthWrite:false,toneMapped:false}));this.sprite.name='pve-holo-label';
 }
 set(text:string,sub=''){
  const key=text+'\n'+sub;if(key===this.text)return;this.text=key;const c=this.canvas;if(!c)return;const {ctx}=c,w=this.w,h=this.h,size=this.style.size??Math.round(h*.52);
  ctx.clearRect(0,0,w,h);ctx.textAlign='center';ctx.textBaseline='middle';ctx.shadowColor=this.style.glow??'rgba(95,232,255,.9)';ctx.shadowBlur=h*.12;ctx.fillStyle=this.style.color??'#dffaff';
  ctx.font=`700 ${size}px ${HOLO_FONT}`;ctx.fillText(text,w/2,sub?h*.36:h/2);
  if(sub){ctx.font=`600 ${this.style.sub??Math.round(size*.62)}px ${HOLO_FONT}`;ctx.fillStyle='#ffffff';ctx.fillText(sub,w/2,h*.76);}
  this.texture.needsUpdate=true;
 }
 dispose(){this.texture.dispose();(this.sprite.material as T.Material).dispose();}
}

// ---- the choosing hologram ---------------------------------------------------------------------------------------
/** Hit delay of an add attack (the window between windup start and damage). */
export const attackHitTime=(a:AddAttack)=>a==='smash'?PVE_TIMING.smashHit:a==='lunge'?PVE_TIMING.lungeHit:PVE_TIMING.swipeHit;
/** §5.5: a chooser is "under attack" while the authoritative hurtAt is younger than 1.2 s, an add that targets them is
 *  winding up (attackAt ≤ t < attackAt + hit), or a Siren move targets them. Pure in (state, view, id). */
export function underAttack(state:Pick<Snapshot,'time'|'players'>,view:PveView,id:string,localHitAt?:number){
 const t=state.time,row=view.party[id];
 if(row?.hurtAt!==undefined&&t-row.hurtAt>=0&&t-row.hurtAt<HOLO.attackedSeconds)return true;
 if(localHitAt!==undefined&&t-localHitAt>=0&&t-localHitAt<HOLO.attackedSeconds)return true;
 for(const tuple of view.adds){const a=decodeAdd(tuple,view.slots);if(a.target===id&&a.attackAt!==undefined&&t>=a.attackAt&&t<a.attackAt+attackHitTime(a.attack))return true;}
 for(const p of state.players){const m=sirenOf(p)?.move;if(m&&m.target===id&&p.hp>0&&t<m.until)return true;}
 return false;
}
/** Red blink phase while attacked (on for half of each 1/blinkHz period). */
export const holoBlinkRed=(attacked:boolean,t:number)=>attacked&&Math.floor(t*HOLO.blinkHz*2)%2===0;

let cardTexture:T.Texture|undefined;
function cardsTexture(){
 if(cardTexture)return cardTexture;const c=makeCanvas(128,176);
 if(c){const {ctx}=c;ctx.clearRect(0,0,128,176);ctx.strokeStyle='#ffffff';ctx.lineWidth=6;ctx.fillStyle='rgba(255,255,255,.16)';
  ctx.beginPath();ctx.roundRect(6,6,116,164,12);ctx.fill();ctx.stroke();ctx.globalAlpha=.55;ctx.fillStyle='#ffffff';
  for(let y=10;y<170;y+=6)ctx.fillRect(8,y,112,2);ctx.globalAlpha=1;ctx.lineWidth=4;ctx.strokeRect(40,34,48,48);ctx.fillRect(54,48,20,20);
  for(let i=0;i<4;i++){ctx.fillRect(46+i*10,26,4,8);ctx.fillRect(46+i*10,82,4,8);}ctx.fillRect(26,104,76,8);ctx.fillRect(34,120,60,6);ctx.fillRect(34,134,60,6);
  cardTexture=new T.CanvasTexture(c.canvas as HTMLCanvasElement);}
 else cardTexture=new T.Texture();
 return cardTexture;
}
function fanGeometry(){
 const parts:T.BufferGeometry[]=[];
 for(let i=-1;i<=1;i++){const g=new T.PlaneGeometry(.3,.41);g.translate(0,.2,0);g.rotateZ(-i*.32);g.translate(i*.15,-.02*Math.abs(i),.01*i);parts.push(g);}
 const pos:number[]=[],uv:number[]=[],idx:number[]=[];let base=0;
 for(const g of parts){const p=g.attributes.position.array,u=g.attributes.uv.array,ix=g.index!.array;for(const n of p)pos.push(n);for(const n of u)uv.push(n);for(const n of ix)idx.push(n+base);base+=g.attributes.position.count;}
 const out=new T.BufferGeometry();out.setAttribute('position',new T.Float32BufferAttribute(pos,3));out.setAttribute('uv',new T.Float32BufferAttribute(uv,2));out.setIndex(idx);return out;
}
const additive=(color:number,opacity:number,map?:T.Texture)=>new T.MeshBasicMaterial({color,...(map?{map}:{}),transparent:true,opacity,depthWrite:false,depthTest:true,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false});

/** One hologram (3 draws: card fan, spinning chip, projector cone). */
class Holo {
 readonly group=new T.Group();readonly fan:T.Mesh;readonly chip:T.Mesh;readonly cone:T.Mesh;readonly mats:T.MeshBasicMaterial[];
 constructor(fan:T.BufferGeometry){
  this.group.name='pve-hologram';
  const m=[additive(HOLO.cyan,.75,cardsTexture()),additive(HOLO.cyan,.95),additive(HOLO.cyan,.16)];this.mats=m;
  this.fan=new T.Mesh(fan,m[0]);this.chip=new T.Mesh(chipGeometry().die,m[1]);this.chip.scale.setScalar(.42);this.chip.position.y=.62;
  const cone=new T.CylinderGeometry(.34,.05,.55,16,1,true);cone.translate(0,-.25,0);this.cone=new T.Mesh(cone,m[2]);
  for(const o of [this.fan,this.chip,this.cone]){o.renderOrder=3;o.frustumCulled=false;}
  this.group.add(this.cone,this.fan,this.chip);
 }
 dispose(){for(const m of this.mats)m.dispose();(this.cone.geometry as T.BufferGeometry).dispose();}
}
const head=new T.Vector3();
/** Holograms over every remote player whose personal chip screen is open. */
export class HologramView {
 readonly group=new T.Group();private pool=new Map<string,Holo>();private fan=fanGeometry();
 /** Ids drawn last update and whether each was red (tests). */
 shown=new Map<string,{red:boolean}>();
 constructor(parent:T.Object3D){this.group.name='pve-holograms';parent.add(this.group);}
 update(state:Snapshot,view:PveView,me:string,camera:T.Camera,rigs:ReadonlyMap<string,PveRigRef>,hitAt?:ReadonlyMap<string,number>){
  const t=state.time;this.shown.clear();
  for(const p of state.players){const row=view.party[p.id];if(!row?.open||p.id===me||p.hp<=0)continue;
   const rig=rigs.get(p.id);if(rig&&!rig.group.visible)continue;
   if(rig){rig.head.updateWorldMatrix(true,false);rig.head.getWorldPosition(head);}else head.set(p.pos.x,p.pos.y+.75,p.pos.z);
   if(camera.position.distanceTo(head)>HOLO.maxDistance)continue;
   let h=this.pool.get(p.id);if(!h){h=new Holo(this.fan);this.pool.set(p.id,h);this.group.add(h.group);}
   const red=holoBlinkRed(underAttack(state,view,p.id,hitAt?.get(p.id)),t),age=t-(row.openAt??t),grow=clamp(age/.25);
   h.group.visible=true;h.group.position.set(head.x,head.y+HOLO.height,head.z);h.group.rotation.set(0,Math.atan2(camera.position.x-head.x,camera.position.z-head.z),0);
   h.group.scale.setScalar(.2+.8*grow*(2-grow));h.chip.rotation.set(-1.2,0,0,'YXZ');h.chip.rotation.y=t*3.2;h.chip.position.y=.62+.03*Math.sin(t*2.6);h.fan.position.y=.02*Math.sin(t*1.7);
   const flicker=.85+.15*Math.sin(t*37+p.id.length);
   h.mats[0].color.setHex(red?HOLO.red:HOLO.cyan);h.mats[0].opacity=.75*flicker;h.mats[1].color.setHex(red?HOLO.red:HOLO.cyan);h.mats[2].color.setHex(red?HOLO.red:HOLO.cyan);h.mats[2].opacity=red?.3:.16;
   this.shown.set(p.id,{red});}
  for(const [id,h] of this.pool)if(!this.shown.has(id))h.group.visible=false;
 }
 /** Draw calls of one hologram (tests). */
 static draws=3;
 materials(id:string){return this.pool.get(id)?.mats??[];}
 clear(){for(const h of this.pool.values()){this.group.remove(h.group);h.dispose();}this.pool.clear();this.shown.clear();}
}
/** §8.6 choosing pose, applied after the rig animation: head down .25 rad, right forearm raised toward the chest. */
export function choosingPose(rig:PveRigRef,amount=1){rig.head.rotation.x+=.25*amount;rig.rightArm.rotation.x+=(-1.1-rig.rightArm.rotation.x)*amount;rig.rightArm.rotation.z+=(.45-rig.rightArm.rotation.z)*amount;}

// ---- tags --------------------------------------------------------------------------------------------------------
export const ROLE_TAG:Record<RecruitRole,{text:string;color:string}>={dps:{text:'⚔ DPS',color:'#ffd2a6'},tank:{text:'⛨ TANK',color:'#a9d4ff'},medic:{text:'✚ MEDIC',color:'#a6ffcb'}};
/** Small holo tags over party bots (BOT) and recruits (role); 1 draw each. */
export class TagView {
 readonly group=new T.Group();private pool=new Map<string,HoloLabel>();shown=new Set<string>();
 constructor(parent:T.Object3D){this.group.name='pve-tags';parent.add(this.group);}
 update(state:Snapshot,view:PveView,me:string,camera:T.Camera,rigs:ReadonlyMap<string,PveRigRef>){
  this.shown.clear();
  for(const p of state.players as PlayerView[]){if(p.id===me||p.hp<=0)continue;const recruit=view.recruits.find(r=>r.id===p.id),bot=view.party[p.id]?.kind==='bot';
   if(!recruit&&!bot)continue;const rig=rigs.get(p.id);if(rig&&!rig.group.visible)continue;
   if(rig){rig.head.updateWorldMatrix(true,false);rig.head.getWorldPosition(head);}else head.set(p.pos.x,p.pos.y+.75,p.pos.z);
   const d=camera.position.distanceTo(head);if(d>40)continue;
   let l=this.pool.get(p.id);if(!l){l=new HoloLabel(192,48,{size:30});this.pool.set(p.id,l);this.group.add(l.sprite);}
   const tag=recruit?ROLE_TAG[recruit.role]:{text:'BOT',color:'#dffaff'};l.set(tag.text,recruit&&recruit.owner===me?'YOURS':'');
   (l.sprite.material as T.SpriteMaterial).color.set(tag.color);
   // Above the overhead health bar (which sits at pos.y + 1.3).
   l.sprite.visible=true;l.sprite.position.set(p.pos.x,p.pos.y+1.62,p.pos.z);const s=.11*Math.max(4,Math.min(d,18));l.sprite.scale.set(s*.4*4,s*.4,1);
   if(!(this.shown.has(p.id)))this.shown.add(p.id);}
  for(const [id,l] of this.pool)if(!this.shown.has(id))l.sprite.visible=false;
 }
 clear(){for(const l of this.pool.values()){this.group.remove(l.sprite);l.dispose();}this.pool.clear();this.shown.clear();}
}
