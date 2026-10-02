// PvE machines (design §4.2, §8.6, stream S6): the four chip dispensers, two recruit pods and two med stations of Ward
// Zero, built as merged-geometry props in the asylum's iron, enamel and warning-lamp language. Each machine is a few
// draws (body Lambert, lit parts Basic, one moving part, a price sign); their animations are pure in authoritative time
// from the machine uses (`PveView.machines`), pickups and recruits. Colliders come from F (`machineBlocks`), whose
// boxes the renderer hides: the models fill the same 1.3 × 2.2 × 0.9 footprint at spot − fwd·0.2.
import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {RARITIES} from '../../shared/content.js';
import type {MachineKind,PveMachineUse,PveRecruitView,PveView} from '../../shared/pve/contract.js';
import {PVE_MACHINES,type PveMachine} from '../../shared/pve/layout.js';
import {PRICES,RECRUIT} from '../../shared/pve/balance.js';
import {HoloLabel} from './hologram.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
/** Machine animation timings (seconds). */
export const MACHINE_TIMING={flapOpen:.15,flapHold:.5,flapClose:.2,podOpen:.4,podClose:.5,deny:.55,med:PRICES.healSeconds,whir:.6} as const;
/** Model-local points (origin on the floor at the spot, +z toward the hall). */
export const SLOT={y:1.02,z:.27} as const;

// ---- pure timelines ----------------------------------------------------------------------------------------------
const usesOf=(uses:readonly PveMachineUse[],m:number,result?:PveMachineUse['result'])=>uses.filter(u=>u.m===m&&(!result||u.result===result));
/** Chip slot flap 0..1: opens right after the purchase, holds while the chip pops out, closes. */
export function flapOpen(uses:readonly PveMachineUse[],m:number,t:number){
 let o=0;for(const u of usesOf(uses,m,'chip')){const k=t-u.at;o=Math.max(o,smooth(k/MACHINE_TIMING.flapOpen)*(1-smooth((k-MACHINE_TIMING.flapOpen-MACHINE_TIMING.flapHold)/MACHINE_TIMING.flapClose)));}
 return o;
}
/** The recruit pod door 0..1: opens over 0.4 s from the purchase, stays open until the recruit has stepped out
 *  (recruit.at + recruitStep), then closes. Before the recruit exists it waits for at + recruitDelay + recruitStep. */
export function podDoorOpen(uses:readonly PveMachineUse[],recruits:readonly PveRecruitView[],m:number,t:number,step=.6){
 let o=0;
 for(const u of usesOf(uses,m,'recruit')){const r=recruits.find(x=>x.m===m&&x.at>=u.at-1e-6&&x.at<=u.at+PRICES.recruitDelay+1),until=(r?r.at:u.at+PRICES.recruitDelay)+step;
  o=Math.max(o,smooth((t-u.at)/MACHINE_TIMING.podOpen)*(1-smooth((t-until)/MACHINE_TIMING.podClose)));}
 // A recruit hired before the use left the view (uses live 4 s) still steps out of an open door.
 for(const r of recruits)if(r.m===m)o=Math.max(o,smooth((t-(r.at-PRICES.recruitDelay))/MACHINE_TIMING.podOpen)*(1-smooth((t-(r.at+step))/MACHINE_TIMING.podClose)));
 return o;
}
/** Red deny flash 0..1. */
export function denyFlash(uses:readonly PveMachineUse[],m:number,t:number){let o=0;for(const u of usesOf(uses,m,'deny')){const k=t-u.at;if(k>=0&&k<MACHINE_TIMING.deny)o=Math.max(o,(1-k/MACHINE_TIMING.deny)*(Math.floor(k*10)%2?.4:1));}return o;}
/** Activity 0..1 (purchase whir / heal) used to drive the lit parts. */
export function activity(uses:readonly PveMachineUse[],m:number,t:number){
 let o=0;for(const u of usesOf(uses,m)){if(u.result==='deny')continue;const k=t-u.at,len=u.result==='med'?MACHINE_TIMING.med:MACHINE_TIMING.whir;if(k>=0&&k<len+.3)o=Math.max(o,1-smooth((k-len)/.3));}return o;
}

// ---- models ------------------------------------------------------------------------------------------------------
const tmp=new T.Color();
type Part=[w:number,h:number,d:number,x:number,y:number,z:number,color:number,rx?:number,ry?:number,rz?:number];
function boxes(list:readonly Part[],geometry?:(p:Part)=>T.BufferGeometry){
 const parts=list.map(p=>{const [w,h,d,x,y,z,c,rx=0,ry=0,rz=0]=p;const g=geometry?geometry(p):new T.BoxGeometry(w,h,d);g.rotateX(rx);g.rotateY(ry);g.rotateZ(rz);g.translate(x,y,z);
  const n=g.attributes.position.count,a=new Float32Array(n*3);tmp.setHex(c);for(let i=0;i<n;i++){a[i*3]=tmp.r;a[i*3+1]=tmp.g;a[i*3+2]=tmp.b;}g.setAttribute('color',new T.BufferAttribute(a,3));
  if(!g.index)g.setIndex([...Array(n).keys()]);return g;});
 return mergeGeometries(parts.map(g=>{for(const k of Object.keys(g.attributes))if(!['position','normal','uv','color'].includes(k))g.deleteAttribute(k);return g;}))!;
}
const IRON=0x1b191f,DARK=0x0b0a0e,PLUM=0x2c2733,STEEL=0x3a3542,PEWTER=0x6b6676,STRIPE=0x6b1520,RUST=0x3a221c,ENAMEL=0x8a857c,ENAMEL_DIRT=0x5c574f;
/** Shared footing: a plinth with hazard stripes along the front edge and bolts. */
function plinth():Part[]{const out:Part[]=[[1.3,.16,.9,0,.08,-.2,IRON]];for(let i=0;i<7;i++)out.push([.18,.1,.02,-.54+i*.18,.08,.255,i%2?IRON:STRIPE,0,0,.45]);for(const x of [-.56,.56])out.push([.06,.03,.06,x,.17,.17,PEWTER]);return out;}

/** Chip dispenser: a plum-steel cabinet with a lit display of chips, a lamp strip in the six rarity colours above a
 *  chip slot with a hinged flap, a coin panel, and a crown with the sign. */
function chipMachine(){
 const body:Part[]=[...plinth(),[1.12,1.86,.78,0,1.09,-.23,PLUM],[1.2,.14,.86,0,2.09,-.21,IRON],[1.04,.08,.8,0,2.2,-.24,STEEL],
  [.08,1.86,.1,-.6,1.09,.12,IRON],[.08,1.86,.1,.6,1.09,.12,IRON],[.9,.72,.06,0,1.6,.17,DARK],[.96,.06,.08,0,1.97,.18,PEWTER],[.96,.06,.08,0,1.23,.18,PEWTER],
  [.56,.2,.06,0,SLOT.y,.17,0x050407],[.62,.04,.08,0,SLOT.y+.12,.18,PEWTER],[.62,.04,.08,0,SLOT.y-.12,.18,PEWTER],[.5,.05,.18,0,SLOT.y-.13,.24,STEEL],
  [.18,.34,.05,.38,.7,.18,STEEL],[.08,.02,.06,.38,.8,.21,DARK],[.12,.12,.04,.38,.62,.21,IRON],[.6,.4,.04,-.1,.55,.17,IRON],[.06,1.4,.06,-.5,1,-.66,RUST],[.06,1.1,.06,.45,.9,-.66,RUST]];
 // Chip silhouettes on racks inside the display (dark, against the lit back glass).
 for(let r=0;r<3;r++){body.push([.84,.02,.12,0,1.36+r*.2,.11,PEWTER]);for(let c=0;c<4;c++)body.push([.12,.1,.02,-.3+c*.2+(r%2)*.05,1.42+r*.2,.12,0x14121a]);}
 const lit:Part[]=[[.84,.66,.02,0,1.6,.13,0x6f55c8],[.5,.03,.03,0,SLOT.y-.07,.2,0xb7a8ff],[.06,.06,.03,.38,.47,.21,0xff2f44]];
 const tiers=['common','uncommon','rare','epic','legendary','mythic'] as const;tiers.forEach((tier,i)=>lit.push([.11,.07,.04,-.3+i*.12,1.15,.19,new T.Color(RARITIES[tier].color).getHex()]));
 const flap:Part[]=[[.5,.17,.03,0,-.085,0,STEEL],[.5,.03,.04,0,-.17,.01,PEWTER]];
 return {body:boxes(body),lit:boxes(lit),moving:boxes(flap)};
}
/** Recruit pod: an upright capsule chamber, cyan-lit inside, with a ribbed glass door hinged on its right. */
function recruitPod(){
 const body:Part[]=[...plinth(),[1.2,2.0,.22,0,1.16,-.55,IRON],[.16,2.0,.86,-.57,1.16,-.21,STEEL],[.16,2.0,.86,.57,1.16,-.21,STEEL],[1.3,.24,.92,0,2.24,-.2,IRON],
  [1.0,.06,.6,0,.19,-.2,DARK],[.86,.04,.04,0,2.1,.2,PEWTER],[.2,.2,.16,0,2.42,-.2,IRON],[.06,1.9,.06,-.66,1.1,-.6,RUST],
  ...[0,1,2,3].map((i):Part=>[.03,1.8,.04,-.48+i*.32,1.15,-.42,PEWTER])];
 for(const x of [-.57,.57])for(let i=0;i<5;i++)body.push([.18,.04,.88,x,.45+i*.4,-.21,IRON]);
 const lit:Part[]=[[.92,1.86,.02,0,1.16,-.43,0x1d6f86],[.7,.03,.5,0,2.09,-.2,0x5fe8ff],[.7,.02,.5,0,.23,-.2,0x5fe8ff],[.16,.1,.14,0,2.48,-.2,0x5fe8ff],[.02,1.7,.02,-.49,1.15,.2,0x2fb7d8],[.02,1.7,.02,.49,1.15,.2,0x2fb7d8]];
 // The door, hinged at x=+.49 (its local origin): frame and ribs; the glass is a second, transparent mesh.
 const door:Part[]=[[.06,1.86,.06,-.03,1.15,0,IRON],[.06,1.86,.06,-.95,1.15,0,IRON],[.98,.06,.06,-.49,2.06,0,IRON],[.98,.06,.06,-.49,.24,0,IRON],
  ...[0,1,2].map((i):Part=>[.98,.03,.04,-.49,.7+i*.45,.01,STEEL]),[.04,.3,.06,-.85,1.15,.04,PEWTER]];
 return {body:boxes(body),lit:boxes(lit),moving:boxes(door)};
}
/** Med station: a chipped hospital-enamel cabinet with a lit cross, a dispenser nozzle and a small vitals screen. */
function medStation(){
 const body:Part[]=[...plinth(),[1.1,1.9,.7,0,1.11,-.25,ENAMEL],[1.16,.12,.76,0,2.1,-.24,IRON],[.82,.82,.05,0,1.55,.11,ENAMEL_DIRT],[.66,.66,.04,0,1.55,.14,DARK],
  [.5,.36,.05,0,.82,.11,IRON],[.42,.26,.03,0,.84,.13,DARK],[.26,.1,.14,0,.5,.13,STEEL],[.06,.18,.06,0,.4,.17,PEWTER],[.1,.5,.1,-.5,.6,.12,IRON],
  [.3,.2,.02,.36,1.9,.11,ENAMEL_DIRT],[.18,.5,.02,-.38,.9,.11,RUST],[.12,.3,.02,.42,1.1,.11,RUST],[.05,1.5,.05,.5,1,-.63,PEWTER]];
 const lit:Part[]=[[.5,.14,.03,0,1.55,.17,0x6dffb0],[.14,.5,.03,0,1.55,.17,0x6dffb0],[.36,.04,.02,0,.86,.15,0x6dffb0],[.04,.04,.02,.12,.8,.15,0xff2f44],[.06,.06,.03,0,.42,.21,0x6dffb0]];
 const ring:Part[]=[[.9,.9,.02,0,0,0,0x6dffb0]];
 return {body:boxes(body),lit:boxes(lit),moving:boxes(ring,()=>new T.RingGeometry(.38,.45,4,1).rotateZ(Math.PI/4))};
}
let models:Record<MachineKind,{body:T.BufferGeometry;lit:T.BufferGeometry;moving:T.BufferGeometry}>|undefined;
export function machineModels(){return models??={chip:chipMachine(),recruit:recruitPod(),med:medStation()};}
const BODY=new T.MeshLambertMaterial({vertexColors:true});
export const MACHINE_SIGN:Record<MachineKind,string>={chip:'CHIP',recruit:'RECRUIT',med:'MED'};

/** One machine on the floor: group at the spot facing the hall. */
export class MachineModel {
 readonly group=new T.Group();readonly lit:T.Mesh<T.BufferGeometry,T.MeshBasicMaterial>;readonly moving:T.Mesh;readonly pivot=new T.Group();readonly glass?:T.Mesh<T.PlaneGeometry,T.MeshBasicMaterial>;
 readonly sign=new HoloLabel(256,80,{size:40,sub:32});
 constructor(readonly machine:PveMachine){
  const g=machineModels()[machine.kind];this.group.name='pve-machine-'+machine.kind;this.group.position.set(machine.x,0,machine.z);this.group.rotation.y=machine.yaw;
  const body=new T.Mesh(g.body,BODY);body.castShadow=true;body.receiveShadow=true;this.lit=new T.Mesh(g.lit,new T.MeshBasicMaterial({vertexColors:true,toneMapped:false}));
  const movingMat=machine.kind==='med'?new T.MeshBasicMaterial({color:0x6dffb0,transparent:true,opacity:0,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false}):BODY;
  this.moving=new T.Mesh(g.moving,movingMat);this.moving.castShadow=machine.kind!=='med';
  if(machine.kind==='chip')this.pivot.position.set(0,SLOT.y+.1,SLOT.z);else if(machine.kind==='recruit')this.pivot.position.set(.49,0,.22);else this.pivot.position.set(0,1.55,.2);
  this.pivot.add(this.moving);
  if(machine.kind==='recruit'){this.glass=new T.Mesh(new T.PlaneGeometry(.88,1.78),new T.MeshBasicMaterial({color:0x5fe8ff,transparent:true,opacity:.2,depthWrite:false,side:T.DoubleSide,toneMapped:false}));this.glass.position.set(-.49,1.15,.0);this.pivot.add(this.glass);}
  this.sign.sprite.position.set(0,2.62,.05);this.sign.sprite.scale.set(1.25,.39,1);
  this.group.add(body,this.lit,this.pivot,this.sign.sprite);
 }
 /** Draw calls of this model (tests): every visible mesh and sprite. */
 draws(){let n=0;this.group.traverse(o=>{if((o as T.Mesh).isMesh||(o as T.Sprite).isSprite)n++;});return n;}
 update(view:PveView,t:number,me:string){
  const m=this.machine.m,uses=view.machines,deny=denyFlash(uses,m,t),act=activity(uses,m,t),idle=.75+.25*Math.sin(t*2.1+m*1.7);
  const level=this.machine.kind==='med'?.8+.2*Math.sin(t*1.3+m)+1.2*act:idle+1.1*act;
  this.lit.material.color.setRGB(level,level*(1-.85*deny),level*(1-.85*deny));
  if(this.machine.kind==='chip')this.pivot.rotation.x=-1.25*flapOpen(uses,m,t);
  else if(this.machine.kind==='recruit')this.pivot.rotation.y=1.85*podDoorOpen(uses,view.recruits,m,t);
  else{const k=act>0?((t*1.4)%1):0,mat=this.moving.material as T.MeshBasicMaterial;this.pivot.scale.setScalar(1+2.2*k);mat.opacity=act>0?.9*(1-k)*act:0;}
  const row=view.party[me],price=this.machine.kind==='chip'?row?.chipPrice??PRICES.chipBase:this.machine.kind==='recruit'?RECRUIT.price:PRICES.heal;
  const cooling=this.machine.kind==='med'&&row&&row.healReadyAt>t;
  this.sign.set(MACHINE_SIGN[this.machine.kind],cooling?`${Math.ceil(row.healReadyAt-t)}s`:`$${price}`);
  const signColor=this.machine.kind==='chip'?0xc9b8ff:this.machine.kind==='recruit'?0x8ff2ff:0x9dffc6;(this.sign.sprite.material as T.SpriteMaterial).color.setHex(deny>0?0xff5a6a:signColor).multiplyScalar(cooling?.55:1);
 }
 dispose(){this.lit.material.dispose();if(this.moving.material!==BODY)(this.moving.material as T.Material).dispose();this.glass?.material.dispose();this.glass?.geometry.dispose();this.sign.dispose();}
}
export class MachinesView {
 readonly group=new T.Group();readonly models:MachineModel[];
 constructor(parent:T.Object3D){this.group.name='pve-machines';parent.add(this.group);this.models=PVE_MACHINES.map(m=>new MachineModel(m));for(const m of this.models)this.group.add(m.group);}
 update(view:PveView,t:number,me:string){this.group.visible=true;for(const m of this.models)m.update(view,t,me);}
 /** World position of a machine's slot (where chips leave). */
 slotOf(m:number,out=new T.Vector3()){const k=PVE_MACHINES[m];const f={x:Math.sin(k.yaw),z:Math.cos(k.yaw)};return out.set(k.x+f.x*SLOT.z,SLOT.y,k.z+f.z*SLOT.z);}
 clear(){for(const m of this.models)m.dispose();this.group.removeFromParent();}
}
