// PvE revive beacons (design §4.6, §8.6, stream S6): over every downed party member, a light column with rising motes,
// a floor ring whose arc fills with the solo self-revive timer, and a label ("F · REVIVE" over "$150", "SELF-REVIVE 0:04").
// Revives are instant (one F press), so there is no channel progress. Above it a small HELP speech bubble bobs (owner,
// 2026-10-03; downed.ts keeps the body below), for everyone but the downed player. Four draws each, no lights; pure in time.
import * as T from 'three';
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import type {PveView} from '../../shared/pve/contract.js';
import {HoloLabel,HOLO_FONT,makeCanvas} from './hologram.js';
import {DOWNED,helpBob,helpShown,helpWidth} from './downed.js';
import {inVentShaft} from '../../shared/pve/vent-pits.js';

export const BEACON={color:0xdff4ff,self:0xffd27a,radius:.95,height:3.6,segments:48} as const;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
export type BeaconMode='down'|'self';
export interface BeaconState{mode:BeaconMode;progress:number;label:string;sub:string}
const clock=(s:number)=>{s=Math.max(0,Math.ceil(s));return `${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;};
/** What the beacon over downed member `id` shows at time t (pure): the solo self-revive timer (progress
 *  (t−downAt)/(selfReviveAt−downAt)), else the instant paid revive (F press, price below). */
export function beaconState(view:PveView,id:string,t:number):BeaconState{
 const row=view.party[id];
 if(row?.selfReviveAt!==undefined){const from=row.downAt??t;return {mode:'self',progress:clamp((t-from)/Math.max(1e-6,row.selfReviveAt-from)),label:'SELF-REVIVE',sub:clock(row.selfReviveAt-t)};}
 return {mode:'down',progress:0,label:'F · REVIVE',sub:`$${row?.reviveCost??0}`};
}
/** Downed members that get a beacon: dead, in the party (humans and party bots), connected, and the run not over. */
export function beaconTargets(state:Pick<Snapshot,'players'>,view:PveView,t=Infinity):PlayerView[]{
 if(view.stage==='ended')return [];
 // A vent-fall body still in the shaft (pve/vent-pits.ts) cannot be reached: no beacon until it lands.
 return (state.players as PlayerView[]).filter(p=>p.hp<=0&&view.party[p.id]&&view.party[p.id].connected&&!inVentShaft(p.death,t));
}

let moteTexture:T.Texture|undefined;
function motes(){
 if(moteTexture)return moteTexture;const c=makeCanvas(64,256);
 if(c){const {ctx}=c,g=ctx.createLinearGradient(0,0,0,256);g.addColorStop(0,'rgba(255,255,255,0)');g.addColorStop(.6,'rgba(255,255,255,.18)');g.addColorStop(1,'rgba(255,255,255,.55)');ctx.fillStyle=g;ctx.fillRect(0,0,64,256);
  let s=7;const r=()=>(s=(Math.imul(s,1664525)+1013904223)>>>0)/4294967296;ctx.fillStyle='#ffffff';for(let i=0;i<26;i++){const x=r()*64,y=r()*256,w=1.5+r()*2.5;ctx.globalAlpha=.5+r()*.5;ctx.fillRect(x,y,w,w*1.6);}
  moteTexture=new T.CanvasTexture(c.canvas as HTMLCanvasElement);moteTexture.wrapS=moteTexture.wrapT=T.RepeatWrapping;}
 else moteTexture=new T.Texture();
 return moteTexture;
}
/** A floor ring (full, dim) followed by the progress arc (bright); drawRange shows ring + progress·arc. */
export function ringGeometry(segments:number=BEACON.segments){
 const pos:number[]=[],col:number[]=[],ring=(r0:number,r1:number,level:number)=>{for(let i=0;i<segments;i++){const a0=i/segments*Math.PI*2,a1=(i+1)/segments*Math.PI*2;
  const p=(r:number,a:number)=>[Math.sin(a)*r,0,Math.cos(a)*r];const q=[p(r0,a0),p(r1,a0),p(r1,a1),p(r0,a0),p(r1,a1),p(r0,a1)];for(const v of q){pos.push(...v);col.push(level,level,level);}}};
 ring(BEACON.radius-.06,BEACON.radius,.35);ring(BEACON.radius+.05,BEACON.radius+.16,1);
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setAttribute('color',new T.Float32BufferAttribute(col,3));return g;
}
/** Vertices drawn for a progress value (the base ring always; the arc proportionally). */
export const ringDrawCount=(progress:number,segments:number=BEACON.segments)=>segments*6+Math.round(clamp(progress)*segments)*6;

let helpMap:T.Texture|undefined;
/** The HELP bubble: a warm white rounded speech bubble with a tail toward the body and bold red lettering (2:1). */
function helpTexture(){
 if(helpMap)return helpMap;const c=makeCanvas(256,128);
 if(c){const {ctx}=c,r=26,x=10,y=8,w=236,h=84;ctx.clearRect(0,0,256,128);ctx.lineJoin='round';
  const bubble=()=>{ctx.beginPath();ctx.moveTo(x+r,y);ctx.arcTo(x+w,y,x+w,y+h,r);ctx.arcTo(x+w,y+h,x,y+h,r);ctx.lineTo(142,y+h);ctx.lineTo(128,120);ctx.lineTo(112,y+h);ctx.arcTo(x,y+h,x,y,r);ctx.arcTo(x,y,x+w,y,r);ctx.closePath();};
  ctx.shadowColor='rgba(0,0,0,.45)';ctx.shadowBlur=8;ctx.shadowOffsetY=3;bubble();ctx.fillStyle='#fff8ee';ctx.fill();ctx.shadowColor='transparent';
  ctx.lineWidth=7;ctx.strokeStyle='#ff3346';ctx.stroke();ctx.textAlign='center';ctx.textBaseline='middle';ctx.font=`800 62px ${HOLO_FONT}`;
  ctx.lineWidth=4;ctx.strokeStyle='#ffffff';ctx.strokeText('HELP!',128,y+h/2+3);ctx.fillStyle='#e8192e';ctx.fillText('HELP!',128,y+h/2+3);
  helpMap=new T.CanvasTexture(c.canvas as HTMLCanvasElement);helpMap.colorSpace=T.SRGBColorSpace;}
 else helpMap=new T.Texture();
 return helpMap;
}
const _at=new T.Vector3();
class Beacon {
 readonly group=new T.Group();readonly column:T.Mesh<T.CylinderGeometry,T.MeshBasicMaterial>;readonly ring:T.Mesh<T.BufferGeometry,T.MeshBasicMaterial>;readonly label=new HoloLabel(320,96,{size:38,sub:32});
 /** Drawn over the scene (depthTest off) so a downed teammate can be found behind adds and props. */
 readonly help=new T.Sprite(new T.SpriteMaterial({map:helpTexture(),transparent:true,depthTest:false,depthWrite:false,toneMapped:false}));
 constructor(){
  this.group.name='pve-beacon';
  this.column=new T.Mesh(new T.CylinderGeometry(BEACON.radius*.7,BEACON.radius*.85,BEACON.height,24,1,true),new T.MeshBasicMaterial({color:BEACON.color,map:motes(),transparent:true,opacity:.55,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false}));
  this.column.position.y=BEACON.height/2;this.ring=new T.Mesh(ringGeometry(),new T.MeshBasicMaterial({vertexColors:true,transparent:true,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false}));this.ring.position.y=.03;
  this.label.sprite.position.y=2.25;this.label.sprite.scale.set(1.5,.45,1);
  this.help.name='pve-help';this.help.renderOrder=6;this.help.center.set(.5,0);
  for(const o of [this.column,this.ring])o.renderOrder=2;this.group.add(this.column,this.ring,this.label.sprite,this.help);
 }
 dispose(){this.column.geometry.dispose();this.column.material.dispose();this.ring.geometry.dispose();this.ring.material.dispose();this.label.dispose();this.help.material.dispose();}
}
export class BeaconView {
 readonly group=new T.Group();private pool=new Map<string,Beacon>();
 /** Last state per beacon id (tests). */
 states=new Map<string,BeaconState>();
 /** Members whose HELP bubble showed last update (tests). */
 help=new Set<string>();
 constructor(parent:T.Object3D){this.group.name='pve-beacons';parent.add(this.group);}
 update(state:Snapshot,view:PveView,me='',camera?:T.Camera,compatibility=false){
  const t=state.time;this.states.clear();this.help.clear();
  for(const p of beaconTargets(state,view,t)){const pos=p.death?.pos??p.pos,s=beaconState(view,p.id,t);this.states.set(p.id,s);
   let b=this.pool.get(p.id);if(!b){b=new Beacon();this.pool.set(p.id,b);this.group.add(b.group);}
   const color=s.mode==='self'?BEACON.self:BEACON.color,pulse=.5+.5*Math.sin(t*3.2);
   b.group.visible=true;b.group.position.set(pos.x,0,pos.z);b.column.material.color.setHex(color);b.column.material.opacity=.32+.18*pulse+(s.mode==='down'?0:.2);
   b.column.material.map!.offset.y=-t*.35;b.ring.material.color.setHex(color);b.ring.geometry.setDrawRange(0,ringDrawCount(s.progress));b.ring.rotation.y=t*.4;
   b.label.set(s.label,s.sub);(b.label.sprite.material as T.SpriteMaterial).color.setHex(color);b.label.sprite.position.y=2.25+.06*Math.sin(t*2);
   // HELP: constant on-screen size between 5 and 26 m, its bottom above the label, bobbing (pure in t), a slight comic tilt.
   // The SVG compatibility path draws sprites as plain rectangles: no bubble there (the label and HUD remain).
   const help=helpShown(p,view,me,t)&&!compatibility;b.help.visible=help;if(help){this.help.add(p.id);const d=camera?camera.position.distanceTo(_at.set(pos.x,2.6,pos.z)):12,w=helpWidth(d);
    b.help.scale.set(w,w*DOWNED.help.aspect,1);b.help.position.y=DOWNED.help.bottom+.06*w+helpBob(t,p.id);b.help.material.rotation=.07*Math.sin(t*2.3+p.id.length);}}
  for(const [id,b] of this.pool)if(!this.states.has(id))b.group.visible=false;
 }
 clear(){for(const b of this.pool.values()){this.group.remove(b.group);b.dispose();}this.pool.clear();this.states.clear();}
}
