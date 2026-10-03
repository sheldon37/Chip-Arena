// PvE world view (design §8.4, §8.6; stream S6). Draws the Ward Zero machines and their dispense animations, chip
// pickups, the "choosing a chip" holograms and pose, revive beacons with the HELP bubble, kept bodies and their
// regeneration (downed.ts), recruit step-out shells, medic heal beams, holo tags and the boss look (growth, possession streak). Everything is a pure function of (view, state.time) plus bounded,
// dt-clamped particles; no lights are added. Vent lids, door leaves and cell gates live in siren/stage.ts (they belong
// to the hall's dressing) and read the same `PveView.entries`.
import * as T from 'three';
import type {GameEvent,PlayerView,Snapshot} from '../../shared/simulation.js';
import {PVE_TIMING,type PveView} from '../../shared/pve/contract.js';
import {PVE_MACHINES} from '../../shared/pve/layout.js';
import type {PveRigRef,PveVisualsContext} from './visuals.js';
import {MachinesView} from './machines.js';
import {PickupsView} from './pickups.js';
import {HologramView,TagView,choosingPose} from './hologram.js';
import {BeaconView} from './beacons.js';
import {BossLook,applyBossLook} from './boss-look.js';
import {downedBodies,type DownedBodies} from './downed.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
/** Recruit step-out (§8.6): over PVE_TIMING.recruitStep from recruit.at the rig scales 0.6 → 1, its cyan shell fades
 *  1 → 0 and it walks `back` → 0 metres out of the pod. Pure in t − at. */
export function recruitStep(at:number,t:number){const k=clamp((t-at)/PVE_TIMING.recruitStep),s=smooth(k);return {k,scale:.6+.4*s,shell:1-s,back:.6*(1-s)};}
export const HEAL_BEAM_SECONDS=.4;

class Shell {
 readonly group=new T.Group();readonly body:T.Mesh<T.CapsuleGeometry,T.MeshBasicMaterial>;readonly ring:T.Mesh<T.RingGeometry,T.MeshBasicMaterial>;
 constructor(){
  const mat=(o:number)=>new T.MeshBasicMaterial({color:0x5fe8ff,transparent:true,opacity:o,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false});
  this.body=new T.Mesh(new T.CapsuleGeometry(.42,.95,6,14),mat(.6));this.ring=new T.Mesh(new T.RingGeometry(.45,.62,32),mat(.8));this.ring.rotation.x=-Math.PI/2;
  this.group.name='pve-recruit-shell';this.group.add(this.body,this.ring);
 }
 dispose(){for(const m of [this.body,this.ring]){m.geometry.dispose();m.material.dispose();}}
}
interface Beam{from:string;to:string;at:number}
const a3=new T.Vector3(),b3=new T.Vector3(),UP=new T.Vector3(0,1,0);

export class WorldView {
 readonly group=new T.Group();
 machines?:MachinesView;pickups?:PickupsView;holograms?:HologramView;tags?:TagView;beacons?:BeaconView;boss?:BossLook;
 private shells=new Map<string,Shell>();private beams:Beam[]=[];private beamMeshes:T.Mesh<T.CylinderGeometry,T.MeshBasicMaterial>[]=[];
 private auras=new Map<string,T.Mesh<T.CylinderGeometry,T.MeshBasicMaterial>>();private hitAt=new Map<string,number>();
 /** Kept bodies of downed members and their regeneration (shared with death-effects.ts through the scene). */
 readonly downed:DownedBodies;
 constructor(readonly scene:T.Scene){this.group.name='pve-world';this.downed=downedBodies(scene);}
 private build(){
  if(this.machines)return;this.scene.add(this.group);
  this.machines=new MachinesView(this.group);this.pickups=new PickupsView(this.group);this.holograms=new HologramView(this.group);this.tags=new TagView(this.group);
  this.beacons=new BeaconView(this.group);this.boss=new BossLook(this.group);
 }
 update(state:Snapshot,view:PveView,ctx:PveVisualsContext){
  this.build();this.group.visible=true;const t=state.time;
  this.machines!.update(view,t,ctx.me);this.pickups!.update(view,t,ctx.me);
  this.holograms!.update(state,view,ctx.me,ctx.camera,ctx.rigs,this.hitAt);this.tags!.update(state,view,ctx.me,ctx.camera,ctx.rigs);
  this.beacons!.update(state,view,ctx.me,ctx.camera);this.downed.update(state,view,ctx);this.boss!.update(state.players,view,t,ctx.quality);
  this.updateShells(state,view);this.updateBeams(state,ctx);this.updateAuras(state,view);
 }
 /** Per player rig after its animation: grown boss scale, recruit step-out, choosing pose. false hides the rig. */
 decoratePlayer(rig:PveRigRef,p:PlayerView,state:Snapshot,view:PveView){
  const t=state.time;rig.group.scale.setScalar(1);
  if(!this.downed.decorate(rig,p,state,view))return false;
  if(p.spec==='siren')applyBossLook(rig,p,t);
  const r=view.recruits.find(x=>x.id===p.id);
  if(r&&t<r.at+PVE_TIMING.recruitStep){const s=recruitStep(r.at,t),m=PVE_MACHINES[r.m];rig.group.scale.multiplyScalar(s.scale);
   if(m){rig.group.position.x-=Math.sin(m.yaw)*s.back;rig.group.position.z-=Math.cos(m.yaw)*s.back;}rig.group.position.y-=(1-s.scale)*.85;}
  const row=view.party[p.id];if(row?.open&&p.hp>0)choosingPose(rig,clamp((t-(row.openAt??t-1))/.25));
  return true;
 }
 event(e:GameEvent,_state:Snapshot,view:PveView){
  this.build();this.boss!.event(e,view);
  if(e.type==='heal'&&e.source&&e.target&&e.source!==e.target&&view.recruits.some(r=>r.id===e.source&&r.role==='medic')){this.beams.push({from:e.source,to:e.target,at:e.time});if(this.beams.length>8)this.beams.shift();}
  if(e.type==='hit'&&e.target&&view.party[e.target]?.open)this.hitAt.set(e.target,e.time);
 }
 private updateShells(state:Snapshot,view:PveView){
  const t=state.time,live=new Set<string>();
  for(const r of view.recruits){if(t<r.at||t>=r.at+PVE_TIMING.recruitStep)continue;const p=state.players.find(x=>x.id===r.id);if(!p)continue;live.add(r.id);
   let s=this.shells.get(r.id);if(!s){s=new Shell();this.shells.set(r.id,s);this.group.add(s.group);}
   const st=recruitStep(r.at,t),m=PVE_MACHINES[r.m],bx=m?Math.sin(m.yaw)*st.back:0,bz=m?Math.cos(m.yaw)*st.back:0;
   s.group.visible=true;s.group.position.set(p.pos.x-bx,p.pos.y-(1-st.scale)*.85,p.pos.z-bz);s.group.scale.setScalar(st.scale);s.body.material.opacity=.6*st.shell*(.8+.2*Math.sin(t*40));
   s.ring.position.y=-.82;s.ring.material.opacity=.8*st.shell;}
  for(const [id,s] of this.shells)if(!live.has(id)){this.group.remove(s.group);s.dispose();this.shells.delete(id);}
 }
 private updateBeams(state:Snapshot,ctx:PveVisualsContext){
  const t=state.time;this.beams=this.beams.filter(b=>t-b.at<HEAL_BEAM_SECONDS&&t>=b.at-.5);let i=0;
  for(const b of this.beams){const from=state.players.find(p=>p.id===b.from),to=state.players.find(p=>p.id===b.to);if(!from||!to||from.hp<=0||to.hp<=0)continue;
   let m=this.beamMeshes[i];if(!m){m=new T.Mesh(new T.CylinderGeometry(.035,.035,1,8,1,true),new T.MeshBasicMaterial({color:0x7dffb0,transparent:true,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false}));m.name='pve-heal-beam';this.beamMeshes.push(m);this.group.add(m);}
   const rf=ctx.rigs.get(b.from),rt=ctx.rigs.get(b.to);a3.set(from.pos.x,(rf?rf.group.position.y:from.pos.y)+.35,from.pos.z);b3.set(to.pos.x,(rt?rt.group.position.y:to.pos.y)+.25,to.pos.z);
   const d=a3.distanceTo(b3);m.visible=d>.1;m.position.copy(a3).lerp(b3,.5);m.scale.set(1,d,1);if(d>.1)m.quaternion.setFromUnitVectors(UP,b3.clone().sub(a3).divideScalar(d));
   m.material.opacity=.8*(1-(t-b.at)/HEAL_BEAM_SECONDS);i++;}
  for(;i<this.beamMeshes.length;i++)this.beamMeshes[i].visible=false;
 }
 private updateAuras(state:Snapshot,view:PveView){
  const t=state.time,live=new Set<string>();
  for(const p of state.players){const row=view.party[p.id];if(!row||row.healAt===undefined||row.healUntil===undefined||t<row.healAt||t>=row.healUntil||p.hp<=0)continue;live.add(p.id);
   let a=this.auras.get(p.id);if(!a){a=new T.Mesh(new T.CylinderGeometry(.55,.62,1.9,20,1,true),new T.MeshBasicMaterial({color:0x6dffb0,transparent:true,depthWrite:false,blending:T.AdditiveBlending,side:T.DoubleSide,toneMapped:false}));a.name='pve-heal-aura';this.auras.set(p.id,a);this.group.add(a);}
   const k=(t-row.healAt)/(row.healUntil-row.healAt),wave=(t*1.6)%1;a.visible=true;a.position.set(p.pos.x,p.pos.y+.1,p.pos.z);a.scale.set(1+.15*wave,1,1+.15*wave);a.material.opacity=.28*(1-wave)*(1-smooth((k-.85)/.15));}
  for(const [id,a] of this.auras)if(!live.has(id))a.visible=false;
 }
 clear(){
  this.machines?.clear();this.pickups?.clear();this.holograms?.clear();this.tags?.clear();this.beacons?.clear();this.boss?.clear();this.downed.clear();
  for(const s of this.shells.values())s.dispose();this.shells.clear();this.beams=[];
  for(const m of [...this.beamMeshes,...this.auras.values()]){m.geometry.dispose();m.material.dispose();}this.beamMeshes=[];this.auras.clear();
  this.machines=this.pickups=this.holograms=this.tags=this.beacons=this.boss=undefined;
  this.group.clear();this.group.removeFromParent();this.hitAt.clear();
 }
}
