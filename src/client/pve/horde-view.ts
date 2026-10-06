// PvE horde presentation (design §8.2-§8.5, stream S5): adds, corpses, gore and the crowd LOD.
//  - Full rigs (the real zombie Pilots, src/client/siren/minions) inside a camera band, nearest first, up to maxRigs; the
//    possessed add is always a full rig with shadows, ahead of the band and the cap.
//  - Everything else within impostorRadius is an impostor (impostors.ts: 11 boxes, one draw for the whole crowd). With
//    more adds inside the band than maxRigs, the farthest of them overflow to impostors (`nearImpostors`).
//  - SVG compatibility: at most 6 low-detail rigs, no impostors (adds beyond the cap are hidden).
// Every pose is a pure function of (add view, state.time) plus the minion gait phase; entry and crawl memos only record
// when something was first seen, so throttled animation and rewinds are safe.
import * as T from 'three';
import type {GameEvent,Snapshot} from '../../shared/simulation.js';
import {decodeAdd,PVE_EVENT,PVE_TIMING,type PveAddView,type PveView} from '../../shared/pve/contract.js';
import {PVE_ENTRIES} from '../../shared/pve/layout.js';
import {HORDE} from '../../shared/pve/balance.js';
import type {PveVisualsContext} from './visuals.js';
import {createSirenMinion,animateSirenMinion,prewarmSirenMinions,setSirenMinionDetail,disposeSirenMinion,minionSeed,sirenMinionStats,
 entryDuration,entryWalkStart,stumpsOf,VENT_RIM_Z,type SirenMinion,type SirenMinionView,type MinionEntry,type MinionOptions} from '../siren/minions/index.js';
import {possessHint,hintLevel} from '../siren/minions/hints.js';
import {ImpostorBatch,addGround,type ImpostorView} from './impostors.js';
import {HordeGore} from './gore.js';
import {StatusRig} from '../status-effects.js';
import {ADD_STATUS} from '../../shared/pve/contract.js';

/** Crowd LOD policy (design §10.6; mirrored in docs/PVE.md). */
export const HORDE_RENDER={maxRigs:{quality:24, performance:16, compatibility:6},
 fullRadius:{quality:20, performance:14, compatibility:14}, hysteresis:4, shadowRadius:15, throttleRadius:12,
 impostorMax:HORDE.maxAlivePossession+HORDE.maxCorpses, impostorRadius:70, createPerFrame:2, prewarmMs:3, spareRigs:4, shadowRigs:12} as const;
/** Small parts that never cast a shadow (the silhouette is the same without them; about 30 fewer shadow draws per rig). */
const NO_SHADOW=/fingers|thumb|cuff|palm|kneecap|-joint$|^collar$|^sternum$|^back-panel$|^armor-|^waist-band$|^jaw$|^crown$|calf|heel|siren-minion-(fx|tongue|hint)/;
export type HordeMode='quality'|'performance'|'compatibility';
export type HordeGraphics='auto'|'high'|'performance';
/** The renderer's graphics setting and compatibility path to a horde LOD mode. */
export const hordeMode=(compatibility:boolean,graphics:HordeGraphics):HordeMode=>compatibility?'compatibility':graphics==='performance'?'performance':'quality';
/** Seconds a hidden (out of band) rig is kept for reuse before it is disposed. */
const SPARE_SECONDS=3;
/** Most adds drawn with status effects at once. */
const STATUS_RIGS=16;
/** Vent pit half size (m): the edge an add climbs over, measured from the vent centre (siren/stage.ts grate). */
export const VENT_EDGE=1.08;
const KIND_OPTIONS:Record<PveAddView['kind'],MinionOptions>={shambler:{},runner:{scrap:'none'},brute:{scrap:'pipe',hunch:.55}};

interface Memo {entry?:MinionEntry;stand?:{x:number;z:number};crawlAt?:number;wasCrawl:boolean;seen:boolean;lastHit?:number;inBand:boolean}
interface Rig {n:number;m:SirenMinion;shown:boolean;lastFull:number;shadow:boolean|null;low:boolean|null;shadowMeshes:T.Mesh[];visibleMeshes:number;shadowDraws:number;lost:number;drips:{v:number}[];hinted:boolean;trail:{v:number}}
interface Item {v:PveAddView;n:number;pos:{x:number;z:number};ground:number;dist:number;possessed:boolean;possessedAt?:number;memo:Memo}

/** Still coming in through its entry: 'entering', or 'crawl' when it lost a leg on the way in (2026-10-04: entering adds
 *  in sight take hits; the authority keeps them on the entry path until they are active, when `entry` becomes -1). */
export const enteringView=(v:Pick<PveAddView,'state'|'entry'>)=>v.entry>=0&&(v.state==='entering'||v.state==='crawl');
/** Where an add stands at the end of a vent climb: just past the pit edge it faces (the rim is VENT_RIM_Z behind it). */
export function ventStand(e:number,yaw:number){
 const en=PVE_ENTRIES[e],sx=Math.sin(yaw),sz=Math.cos(yaw),edge=VENT_EDGE/Math.max(Math.abs(sx),Math.abs(sz),1e-6),d=edge-VENT_RIM_Z;
 return {x:en.from.x+sx*d,z:en.from.z+sz*d};
}

export class HordeView {
 graphics:HordeGraphics='auto';
 readonly gore:HordeGore;readonly impostors:ImpostorBatch;
 private rigs=new Map<number,Rig>();private memo=new Map<number,Memo>();private warm=false;private frame=0;private last=NaN;
 private marker:T.Sprite;private quality=1;private burst:PveVisualsContext['burst']=()=>{};
 private statusRigs=new Map<number,StatusRig>();
 private stats={fullRigs:0,nearImpostors:0,adds:0,impostors:0,hidden:0,pending:0};
 constructor(readonly scene:T.Scene){
  // Nothing joins the scene until the first PvE frame (a PvP scene stays untouched); clear() takes it all out again.
  this.impostors=new ImpostorBatch(HORDE_RENDER.impostorMax);this.gore=new HordeGore(scene);this.gore.flames.mesh.removeFromParent();this.gore.chunks.removeFromParent();
  // The possessed add's impostor (until its rig exists): an additive violet flame.
  this.marker=new T.Sprite(new T.SpriteMaterial({color:0x9d84ff,transparent:true,opacity:.85,blending:T.AdditiveBlending,depthWrite:false,toneMapped:false}));
  this.marker.name='pve-possessed-marker';this.marker.scale.set(.7,1.3,1);this.marker.visible=false;
 }
 /** The renderer's graphics setting (quality vs performance bands). The compatibility path comes from the context. */
 setGraphics(mode:HordeGraphics){this.graphics=mode;}
 /** Every frame while state.pve exists. */
 update(state:Snapshot,view:PveView,ctx:PveVisualsContext){
  const t=state.time,dt=Math.max(0,Math.min(.1,ctx.dt||0)),mode=hordeMode(ctx.compatibility,this.graphics),R=HORDE_RENDER.fullRadius[mode],cap=HORDE_RENDER.maxRigs[mode];
  if(Number.isFinite(this.last)&&(t<this.last-.35||t>this.last+1))this.timeJump(t);this.last=t;this.frame++;this.quality=ctx.quality;this.burst=ctx.burst;
  if(!this.warm)this.warm=prewarmSirenMinions(HORDE_RENDER.prewarmMs);
  if(!this.impostors.mesh.parent)this.scene.add(this.impostors.mesh,this.gore.flames.mesh,this.gore.chunks,this.marker);
  const cam=ctx.camera.getWorldPosition(_cam),b=view.bossView,possessedN=b&&b.possessed!==undefined&&b.foundAt===undefined?b.possessed:undefined;
  // 1. Decode, remember entries and falls, and place every add.
  const items:Item[]=[],present=new Set<number>();
  for(const tuple of view.adds){const v=decodeAdd(tuple,view.slots),memo=this.memoOf(v,view,t),pos=this.displayPos(v,memo,t),ground=addGround(v);
   present.add(v.n);items.push({v,n:v.n,pos,ground,dist:Math.hypot(pos.x-cam.x,ground+1-cam.y,pos.z-cam.z),possessed:v.n===possessedN,possessedAt:v.n===possessedN?b!.possessedAt:undefined,memo});}
  for(const n of [...this.memo.keys()])if(!present.has(n))this.memo.delete(n);
  // 2. The full set: the possessed add, then the nearest adds inside the band (4 m hysteresis), up to the cap.
  const inBand=(i:Item)=>i.dist<=R||(i.memo.inBand&&i.dist<=R+HORDE_RENDER.hysteresis);
  const band=items.filter(i=>!i.possessed&&inBand(i)).sort((a,b)=>a.dist-b.dist||a.n-b.n);
  const full=new Set<number>(),possessed=items.find(i=>i.possessed);if(possessed)full.add(possessed.n);
  band.slice(0,cap).forEach(i=>full.add(i.n));
  for(const i of items)i.memo.inBand=full.has(i.n)&&!i.possessed;
  // Shadows: the possessed add, and the nearest shadowRigs full rigs within shadowRadius (quality mode only).
  const shadowed=new Set<number>();if(mode==='quality'){if(possessed)shadowed.add(possessed.n);band.slice(0,cap).filter(i=>i.dist<=HORDE_RENDER.shadowRadius).slice(0,HORDE_RENDER.shadowRigs).forEach(i=>shadowed.add(i.n));}
  const nearImpostors=band.slice(cap).filter(i=>i.dist<=R).length;
  // 3. Rigs: create at most createPerFrame (possessed first, then nearest); the others wait as impostors.
  let created=0;const want=items.filter(i=>full.has(i.n)&&!this.rigs.has(i.n)).sort((a,b)=>Number(b.possessed)-Number(a.possessed)||a.dist-b.dist);
  for(const i of want){if(created>=HORDE_RENDER.createPerFrame)break;this.rigs.set(i.n,this.createRig(i.v));created++;}
  let shownRigs=0,pending=0;const drawn=new Set<number>(),byN=new Map(items.map(i=>[i.n,i]));
  for(const i of items){
   const rig=this.rigs.get(i.n);if(!full.has(i.n)||!rig){if(full.has(i.n))pending++;continue;}
   this.showRig(rig,i,t,dt,mode,ctx,shadowed.has(i.n));drawn.add(i.n);shownRigs++;
  }
  // 4. Hide rigs that left the full set; dispose dead, absent and stale spares (keep at most spareRigs, newest first).
  const spares:Rig[]=[];
  for(const [n,rig] of [...this.rigs]){const item=byN.get(n);
   if(!item||item.v.deathAt!==undefined&&t>=item.v.deathAt+PVE_TIMING.corpse){this.dispose(rig);continue;}
   if(drawn.has(n))continue;if(rig.shown){rig.shown=false;rig.m.rig.group.visible=false;}
   if(t-rig.lastFull>SPARE_SECONDS)this.dispose(rig);else spares.push(rig);}
  spares.sort((a,b)=>b.lastFull-a.lastFull).slice(HORDE_RENDER.spareRigs).forEach(r=>this.dispose(r));
  // 5. Impostors for everything else within range (none on the compatibility path).
  this.impostors.begin();this.marker.visible=false;let hidden=0;
  for(const i of items){if(drawn.has(i.n))continue;
   if(mode==='compatibility'||i.dist>HORDE_RENDER.impostorRadius){hidden++;continue;}
   if(this.impostors.add(this.impostorView(i,t),t)&&i.possessed){this.marker.visible=true;this.marker.position.set(i.pos.x,i.ground+1.9+.06*Math.sin(t*5),i.pos.z);this.marker.material.opacity=.6+.25*Math.sin(t*7.3);}}
  this.impostors.end();
  this.statuses(items,drawn,t,ctx,mode);
  this.gore.update(t,dt);
  this.stats={fullRigs:shownRigs,nearImpostors,adds:items.length,impostors:this.impostors.adds,hidden,pending};
 }
 /** Weapon statuses on adds (flames, poison bubbles, slow droplets; the player StatusRig), nearest first, at most STATUS_RIGS. */
 private statuses(items:Item[],drawn:Set<number>,t:number,ctx:PveVisualsContext,mode:HordeMode){
  const want=items.filter(i=>i.v.st&&i.v.deathAt===undefined&&(drawn.has(i.n)||mode!=='compatibility'&&i.dist<=HORDE_RENDER.impostorRadius)).sort((a,b)=>a.dist-b.dist).slice(0,STATUS_RIGS),keep=new Set<number>();
  for(const i of want){let r=this.statusRigs.get(i.n);if(!r){r=new StatusRig();r.root.name='pve-add-status';this.statusRigs.set(i.n,r);this.scene.add(r.root);}keep.add(i.n);
   const st=i.v.st!,statuses=[...(st&ADD_STATUS.burn?[{element:'fire' as const,dps:1,remaining:1}]:[]),...(st&ADD_STATUS.poison?[{element:'poison' as const,dps:1,remaining:1}]:[])];
   r.root.position.set(i.pos.x,i.ground+(i.v.state==='crawl'?.35:.85),i.pos.z);r.root.visible=true;
   r.update(statuses,t,ctx.camera,ctx.quality,true,st&ADD_STATUS.slow?{slowed:{factor:.6,until:t+1,kind:'add-slow'}}:{});}
  for(const [n,r] of [...this.statusRigs])if(!keep.has(n)){r.root.removeFromParent();this.statusRigs.delete(n);}
 }
 /** Burning/poisoned/slowed adds with a status rig (tests). */
 statusCount(){return this.statusRigs.size;}
 /** Every new snapshot event (pve-gib: ink spray, a black-flame puff and a tumbling limb chunk). */
 event(e:GameEvent,_state:Snapshot,_view:PveView){
  if(e.type===PVE_EVENT.gib)this.gore.gib({pos:e.pos,from:e.from,message:e.message,time:e.time,target:e.target},this.quality,this.burst);
 }
 /** getMetrics() fields (§7.8). */
 metrics(){return {fullRigs:this.stats.fullRigs,nearImpostors:this.stats.nearImpostors,adds:this.stats.adds};}
 /** Wider counters for tests and the perf script: impostor adds, hidden adds, rigs waiting to be created. */
 details(){return {...this.stats,rigs:this.rigs.size,impostorInstances:this.impostors.mesh.count,chunks:this.gore.liveChunks(this.last)};}
 /** Visible draw estimate: rig meshes (twice when they cast shadows) plus the instanced batches. */
 drawEstimate(){
  let n=0;for(const r of this.rigs.values())if(r.shown)n+=r.visibleMeshes+(r.shadow?r.shadowDraws:0);
  return n+(this.impostors.mesh.count>0?1:0)+(this.gore.chunks.count>0?1:0)+1+(this.marker.visible?1:0);
 }
 /** The full rig of add n, if it has one (tests, lab). */
 rigOf(n:number){const r=this.rigs.get(n);return r?.shown?r.m:undefined;}
 /** Remove everything from the scene (PvE ended or snapshot without pve). */
 clear(){for(const r of [...this.rigs.values()])this.dispose(r);for(const r of this.statusRigs.values())r.root.removeFromParent();this.statusRigs.clear();this.memo.clear();this.impostors.begin();this.impostors.end();this.gore.clear();this.marker.visible=false;this.last=NaN;
  for(const o of [this.impostors.mesh,this.gore.flames.mesh,this.gore.chunks,this.marker])o.removeFromParent();
  this.stats={fullRigs:0,nearImpostors:0,adds:0,impostors:0,hidden:0,pending:0};}

 // ------------------------------------------------------------------------------------------------------------
 private timeJump(t:number){for(const m of this.memo.values()){m.lastHit=undefined;if(m.crawlAt!==undefined&&m.crawlAt>t)m.crawlAt=undefined;}}
 private memoOf(v:PveAddView,view:PveView,t:number):Memo{
  let m=this.memo.get(v.n);const first=!m;if(!m){m={wasCrawl:false,seen:false,inBand:false};this.memo.set(v.n,m);}
  // Entering: the entry kind, the add's own start, and whether it opens the entry (its start is the entry's openAt).
  if(enteringView(v)&&v.spawnAt!==undefined&&(!m.entry||m.entry.at!==v.spawnAt)){
   const en=PVE_ENTRIES[v.entry];if(en){const open=view.entries.find(x=>x.e===v.entry&&x.mode==='spawn');
    m.entry={kind:en.kind,at:v.spawnAt,first:!open||Math.abs(open.openAt-v.spawnAt)<.05,rimZ:VENT_RIM_Z};
    m.stand=en.kind==='vent'?ventStand(v.entry,v.yaw):undefined;}}
  // Falls: remember when a seen-upright add went down (the hit that took the leg, or now).
  const crawl=v.state==='crawl'||(v.state==='dead'&&m.wasCrawl);
  if(crawl&&!m.wasCrawl&&!first&&m.seen)m.crawlAt=v.hitAt!==undefined&&t-v.hitAt<.5?v.hitAt:t;
  m.wasCrawl=crawl;m.seen=true;return m;
 }
 private displayPos(v:PveAddView,m:Memo,t:number){
  const en=m.entry;
  if(en?.kind==='vent'&&m.stand){const end=en.at+entryDuration(en);
   if(enteringView(v))return m.stand;
   // The authority creates the body at the vent centre (PVE_ENTRIES exit); ease from the stand point onto it.
   const k=(t-end)/.6;if(k<1&&Math.hypot(v.pos.x-m.stand.x,v.pos.z-m.stand.z)>.2){const w=k<=0?0:k*k*(3-2*k);return {x:m.stand.x+(v.pos.x-m.stand.x)*w,z:m.stand.z+(v.pos.z-m.stand.z)*w};}}
  if(v.state==='vent'&&v.diveAt!==undefined&&v.entry>=0&&PVE_ENTRIES[v.entry]?.kind==='vent'){
   // Diving: step from where it stood onto the open pit before dropping in.
   const c=PVE_ENTRIES[v.entry].from,dx=v.pos.x-c.x,dz=v.pos.z-c.z,d=Math.hypot(dx,dz),r=Math.min(d,VENT_EDGE-.5),k=Math.min(1,Math.max(0,(t-v.diveAt)/.3)),w=k*k*(3-2*k);
   const tx=d>1e-6?c.x+dx/d*r:c.x,tz=d>1e-6?c.z+dz/d*r:c.z;return {x:v.pos.x+(tx-v.pos.x)*w,z:v.pos.z+(tz-v.pos.z)*w};}
  return {x:v.pos.x,z:v.pos.z};
 }
 /** The minion view for an add: authority fields plus the entry memo (the walk velocity of a door or cell walk-out). */
 private minionView(i:Item,t:number):SirenMinionView{
  const v=i.v,m=i.memo,s:SirenMinionView={yaw:v.yaw,vel:{x:v.vel.x,z:v.vel.z},hp:v.hp,maxHp:1,attack:v.attack,side:v.side,lost:v.lost,crawl:v.state==='crawl'||(v.state==='dead'&&m.wasCrawl)};
  if(v.attackAt!==undefined)s.attackAt=v.attackAt;if(v.hitAt!==undefined){s.hitAt=v.hitAt;s.hitYaw=v.hitYaw;}if(v.deathAt!==undefined)s.deathAt=v.deathAt;
  if(v.diveAt!==undefined&&v.state==='vent')s.diveAt=v.diveAt;if(m.crawlAt!==undefined)s.crawlAt=m.crawlAt;
  const en=m.entry;
  // An add killed on the way in keeps its entry until the corpse is gone, so the pose at deathAt never pops.
  if(en&&(t-en.at<entryDuration(en)+.05||v.deathAt!==undefined&&v.deathAt-en.at<entryDuration(en))){s.entry=en;
   if(en.kind!=='vent'&&enteringView(v)){const e=PVE_ENTRIES[v.entry],walk=en.kind==='door'?PVE_TIMING.door.walk:PVE_TIMING.cell.walk,w0=en.at+entryWalkStart(en);
    s.vel=e&&t>=w0&&t<w0+walk?{x:(e.exit.x-e.from.x)/walk,z:(e.exit.z-e.from.z)/walk}:{x:0,z:0};}}
  return s;
 }
 private impostorView(i:Item,t:number):ImpostorView{
  const s=this.minionView(i,t),v=i.v;
  return {pos:{x:i.pos.x,y:v.pos.y,z:i.pos.z},ground:i.ground,yaw:v.yaw,vel:s.vel,state:v.state,lost:v.lost,side:v.side,attack:v.attack,kind:v.kind,
   attackAt:v.attackAt,deathAt:v.deathAt,diveAt:s.diveAt,entry:s.entry};
 }
 private createRig(v:PveAddView):Rig{
  const m=createSirenMinion(minionSeed(v.id),KIND_OPTIONS[v.kind]),shadowMeshes:T.Mesh[]=[];
  m.rig.group.traverse(o=>{if(o instanceof T.Mesh){if(NO_SHADOW.test(o.name))o.castShadow=false;else shadowMeshes.push(o);}});
  m.rig.group.name='pve-add-'+v.n;const visibleMeshes=sirenMinionStats(m).visible;m.rig.group.visible=false;this.scene.add(m.rig.group);
  return {n:v.n,m,shown:false,lastFull:-Infinity,shadow:null,low:null,shadowMeshes,visibleMeshes,shadowDraws:shownCount(shadowMeshes,m.rig.group),lost:-1,drips:[],hinted:false,trail:{v:0}};
 }
 private showRig(rig:Rig,i:Item,t:number,dt:number,mode:HordeMode,ctx:PveVisualsContext,shadow:boolean){
  const g=rig.m.rig.group,v=i.v,first=!rig.shown;rig.shown=true;rig.lastFull=t;g.visible=true;
  const low=mode==='compatibility';if(rig.low!==low){setSirenMinionDetail(rig.m,low);rig.low=low;}
  // Shadows only change when the add changes band (quality mode, within shadowRadius; the possessed add always).
  if(rig.shadow!==shadow){for(const mesh of rig.shadowMeshes)mesh.castShadow=shadow;rig.shadow=shadow;}
  g.position.set(i.pos.x,i.ground-rig.m.floor,i.pos.z);g.rotation.y=v.yaw;
  // Beyond throttleRadius the pose updates every other frame (it is pure in time, so skipping a frame is safe).
  const throttle=!first&&!i.possessed&&i.dist>HORDE_RENDER.throttleRadius&&(this.frame+i.n)%2===1;
  if(!throttle){animateSirenMinion(rig.m,this.minionView(i,t),t,ctx.quality);
   if(rig.lost!==v.lost){rig.lost=v.lost;g.visible=true;rig.visibleMeshes=sirenMinionStats(rig.m).visible;rig.shadowDraws=shownCount(rig.shadowMeshes,rig.m.rig.group);}}
  // Possession hints (no eyes): crown glow, grin glyph, and a black-flame trail from level 2.
  const level=i.possessed&&i.possessedAt!==undefined?hintLevel(t-i.possessedAt):0;
  if(level||rig.hinted){possessHint(rig.m,level,t,i.dist);rig.hinted=level>0;}
  if(level>=2&&dt>0&&Math.hypot(v.vel.x,v.vel.z)>.2)this.gore.drip({x:i.pos.x,y:i.ground+.25,z:i.pos.z},dt*2,ctx.quality,rig.trail);
  // Black blood on hits (full detail only) and black flame dripping off stumps.
  if(v.hitAt!==undefined&&v.hitAt!==i.memo.lastHit){if(t-v.hitAt<.3&&t>=v.hitAt&&!low)this.gore.hit({x:i.pos.x,y:i.ground+1.05,z:i.pos.z},v.hitYaw??0,ctx.quality,ctx.burst);i.memo.lastHit=v.hitAt;}
  if(!low&&dt>0&&i.dist<=HORDE_RENDER.throttleRadius&&v.deathAt===undefined){const stumps=stumpsOf(rig.m);
   for(let k=0;k<stumps.length;k++){rig.drips[k]??={v:hash(rig.n,k)};stumps[k].getWorldPosition(_p);this.gore.drip({x:_p.x,y:_p.y-.04,z:_p.z},dt,ctx.quality,rig.drips[k]);}}
 }
 private dispose(rig:Rig){disposeSirenMinion(rig.m);this.rigs.delete(rig.n);}
}
const _cam=new T.Vector3(),_p=new T.Vector3();
/** Meshes shown (own and ancestors' visibility) among `list`. */
function shownCount(list:readonly T.Object3D[],root:T.Object3D){let n=0;for(const o of list){let v=o.visible;for(let p=o.parent;v&&p&&p!==root;p=p.parent)v=p.visible;if(v)n++;}return n;}
const hash=(n:number,k:number)=>((Math.imul(n+1,0x9e3779b1)^Math.imul(k+1,0x85ebca77))>>>0)/4294967296;
