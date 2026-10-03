// PvE navigation (design §3.2, stream S1): static occupancy grids over Ward Zero, bucketed-Dijkstra flow fields per
// target, steepest-descent directions and a grid line-of-sight test. Pure apart from HordeNav reading player positions and
// prop presence from the Simulation. Deterministic: fixed 8-neighbour order, integer costs (10/14), no clocks, no Math.random.
import type {ArenaBlock} from '../arena.js';
import type {Vec} from '../config.js';
import type {Simulation} from '../simulation.js';
import type {NavGrid,PveNav} from './contract.js';
import {HORDE} from './balance.js';

export interface NavGridData{blocked:Uint8Array;radius:number;cell:number;x0:number;z0:number;w:number;d:number}
export const NAV_BOUNDS={x0:-32,z0:-28,width:64,depth:56,yMin:.35,yMax:1.8} as const;
export const NAV_INF=0x3fffffff;
/** Fixed neighbour order [dx,dz,cost]: orthogonal first, then diagonals. */
const NB:readonly (readonly [number,number,number])[]=[[1,0,10],[-1,0,10],[0,1,10],[0,-1,10],[1,1,14],[1,-1,14],[-1,1,14],[-1,-1,14]];

/** Yaw of a block from its (y-axis) rotation quaternion. */
const blockYaw=(b:ArenaBlock)=>b.rotation?2*Math.atan2(b.rotation.y,b.rotation.w):0;
/** True when the block blocks walking: not dynamic and overlapping y∈[.35,1.8]. */
export const blocksWalking=(b:ArenaBlock)=>!b.dynamic&&b.pos.y+b.size.y/2>NAV_BOUNDS.yMin&&b.pos.y-b.size.y/2<NAV_BOUNDS.yMax;

/** Occupancy grid: a cell is blocked when its centre lies inside a walking-blocking block's footprint inflated by
 *  radius + half a cell (so the whole cell is clear for a capsule of that radius). Rotated blocks use their yaw. */
export function buildNavGrid(blocks:readonly ArenaBlock[],opts:{cell?:number;radius:number}):NavGridData{
 const cell=opts.cell??HORDE.nav.cell,w=Math.round(NAV_BOUNDS.width/cell),d=Math.round(NAV_BOUNDS.depth/cell),blocked=new Uint8Array(w*d),grow=opts.radius+cell*.5-1e-6;
 for(const b of blocks){if(!blocksWalking(b))continue;
  const yaw=blockYaw(b),c=Math.cos(yaw),s=Math.sin(yaw),hx=b.size.x/2+grow,hz=b.size.z/2+grow;
  const ex=Math.abs(c)*hx+Math.abs(s)*hz,ez=Math.abs(s)*hx+Math.abs(c)*hz;   // world AABB of the inflated footprint
  const xi0=Math.max(0,Math.floor((b.pos.x-ex-NAV_BOUNDS.x0)/cell)),xi1=Math.min(w-1,Math.floor((b.pos.x+ex-NAV_BOUNDS.x0)/cell));
  const zi0=Math.max(0,Math.floor((b.pos.z-ez-NAV_BOUNDS.z0)/cell)),zi1=Math.min(d-1,Math.floor((b.pos.z+ez-NAV_BOUNDS.z0)/cell));
  for(let zi=zi0;zi<=zi1;zi++)for(let xi=xi0;xi<=xi1;xi++){
   const dx=NAV_BOUNDS.x0+(xi+.5)*cell-b.pos.x,dz=NAV_BOUNDS.z0+(zi+.5)*cell-b.pos.z;
   // Local coordinates (rotate by −yaw about y; three.js yaw convention: local x = c·dx − s·dz, local z = s·dx + c·dz).
   const lx=c*dx-s*dz,lz=s*dx+c*dz;
   if(Math.abs(lx)<hx&&Math.abs(lz)<hz)blocked[zi*w+xi]=1;
  }
 }
 return {blocked,radius:opts.radius,cell,x0:NAV_BOUNDS.x0,z0:NAV_BOUNDS.z0,w,d};
}
export const cellOf=(g:NavGridData,x:number,z:number)=>{
 const xi=Math.max(0,Math.min(g.w-1,Math.floor((x-g.x0)/g.cell))),zi=Math.max(0,Math.min(g.d-1,Math.floor((z-g.z0)/g.cell)));return zi*g.w+xi;
};
export const cellCentre=(g:NavGridData,c:number)=>({x:g.x0+(c%g.w+.5)*g.cell,z:g.z0+(Math.floor(c/g.w)+.5)*g.cell});
/** May a step go from cell (xi,zi) by (dx,dz)? In bounds, target free, no corner cutting. */
function stepOk(g:NavGridData,xi:number,zi:number,dx:number,dz:number){
 const nx=xi+dx,nz=zi+dz;if(nx<0||nz<0||nx>=g.w||nz>=g.d)return false;if(g.blocked[nz*g.w+nx])return false;
 return !(dx&&dz&&(g.blocked[zi*g.w+nx]||g.blocked[nz*g.w+xi]));
}
/** Reusable Dial buckets (one per process; fields are built synchronously). */
const buckets:number[][]=[];
/** Bucketed Dijkstra from `target` over free cells (the target cell itself may be blocked). Writes into `out`. */
export function buildFlowField(g:NavGridData,target:number,out?:Int32Array):Int32Array{
 const dist=out&&out.length===g.w*g.d?out:new Int32Array(g.w*g.d);dist.fill(NAV_INF);dist[target]=0;
 for(const b of buckets)if(b)b.length=0;(buckets[0]??=[]).push(target);
 for(let dd=0;dd<buckets.length;dd++){const q=buckets[dd];if(!q||!q.length)continue;
  for(let i=0;i<q.length;i++){const c=q[i];if(dist[c]!==dd)continue;const xi=c%g.w,zi=(c/g.w)|0;
   for(const [dx,dz,w] of NB){if(!stepOk(g,xi,zi,dx,dz))continue;const n=(zi+dz)*g.w+xi+dx,nd=dd+w;if(nd<dist[n]){dist[n]=nd;(buckets[nd]??=[]).push(n);}}}
 }
 return dist;
}
/** Steepest-descent neighbour cell from c (or -1 when c is a local minimum). Never returns a blocked cell. */
export function descendCell(g:NavGridData,dist:Int32Array,c:number):number{
 const xi=c%g.w,zi=(c/g.w)|0;let best=dist[c],out=-1;
 for(const [dx,dz] of NB){if(!stepOk(g,xi,zi,dx,dz))continue;const n=(zi+dz)*g.w+xi+dx;if(dist[n]<best){best=dist[n];out=n;}}
 return out;
}
/** Unit direction from (x,z) toward the centre of the steepest-descent neighbour, or null at the goal / unreachable. */
export function descend(g:NavGridData,dist:Int32Array,x:number,z:number):{x:number;z:number}|null{
 const n=descendCell(g,dist,cellOf(g,x,z));if(n<0)return null;const t=cellCentre(g,n),dx=t.x-x,dz=t.z-z,l=Math.hypot(dx,dz);
 return l<1e-6?null:{x:dx/l,z:dz/l};
}
/** Path cost in metres from (x,z); a blocked start uses its best free neighbour. Infinity when unreachable. */
export function fieldDistance(g:NavGridData,dist:Int32Array,x:number,z:number):number{
 const c=cellOf(g,x,z);let d=dist[c];
 if(d>=NAV_INF){const xi=c%g.w,zi=(c/g.w)|0;for(const [dx,dz,w] of NB){const nx=xi+dx,nz=zi+dz;if(nx<0||nz<0||nx>=g.w||nz>=g.d)continue;const n=nz*g.w+nx;if(!g.blocked[n]&&dist[n]<NAV_INF)d=Math.min(d,dist[n]+w);}}
 return d>=NAV_INF?Infinity:d/10;
}
/** Grid line of sight: no blocked cell strictly between the two endpoint cells (Amanatides–Woo traversal). */
export function lineClear(g:NavGridData,a:{x:number;z:number},b:{x:number;z:number}):boolean{
 const start=cellOf(g,a.x,a.z),end=cellOf(g,b.x,b.z);if(start===end)return true;
 let xi=start%g.w,zi=(start/g.w)|0;const ex=end%g.w,ez=(end/g.w)|0;
 const dx=b.x-a.x,dz=b.z-a.z,sx=Math.sign(dx),sz=Math.sign(dz);
 const fx=(a.x-g.x0)/g.cell,fz=(a.z-g.z0)/g.cell;
 const tdx=dx?Math.abs(g.cell/dx):Infinity,tdz=dz?Math.abs(g.cell/dz):Infinity;
 let tmx=dx?((sx>0?Math.floor(fx)+1-fx:fx-Math.floor(fx))*g.cell)/Math.abs(dx):Infinity,tmz=dz?((sz>0?Math.floor(fz)+1-fz:fz-Math.floor(fz))*g.cell)/Math.abs(dz):Infinity;
 for(let guard=0;guard<g.w+g.d+4;guard++){
  if(tmx<tmz){tmx+=tdx;xi+=sx;}else{tmz+=tdz;zi+=sz;}
  if(xi<0||zi<0||xi>=g.w||zi>=g.d)return false;
  if(xi===ex&&zi===ez)return true;
  if(g.blocked[zi*g.w+xi])return false;
 }
 return true;
}

interface Field{cell:number;dist:Int32Array;tick:number}
/** The horde's PveNav (contract): party and boss grids from the stage blocks, lazily built flow fields per target id
 *  (rebuilt only when the target changes cell, at most HordeNav.buildsPerTick per sim tick), breakable polling. */
export class HordeNav implements PveNav{
 static buildsPerTick=2;
 grids:Record<NavGrid,NavGridData>|undefined;
 builds=0;rebuilds=0;
 private blocks:ArenaBlock[]=[];private breakable:string[]=[];private fields=new Map<string,Field>();
 private budgetTick=-1;private budget=0;private dirtyFlag=false;private pollAt=0;
 constructor(readonly sim:Simulation){}
 /** Rebuild from a block list (Horde.reset: the stage blocks plus machine blocks). */
 setBlocks(blocks:readonly ArenaBlock[]){
  this.blocks=blocks.filter(blocksWalking);
  this.breakable=this.blocks.filter(b=>b.material!=='foundation').map(b=>b.id);
  this.fields.clear();this.dirtyFlag=false;this.pollAt=0;this.build();
 }
 private build(){
  this.grids={party:buildNavGrid(this.blocks,{radius:HORDE.nav.partyRadius}),boss:buildNavGrid(this.blocks,{radius:HORDE.nav.bossRadius})};
  this.rebuilds++;for(const f of this.fields.values())f.cell=-1;   // stale: rebuild on next use
 }
 ready(){return !!this.grids;}
 dirty(){this.dirtyFlag=true;}
 /** Called once per horde tick: polls static breakable blocks every HORDE.nav.breakablePoll seconds and rebuilds. */
 poll(time:number){
  if(time>=this.pollAt){this.pollAt=time+HORDE.nav.breakablePoll;
   if(this.sim.props.size&&this.breakable.some(id=>{const p=this.sim.props.get(id);return !p||p.dynamic;}))this.dirtyFlag=true;}
  if(this.dirtyFlag){this.dirtyFlag=false;this.breakable=this.breakable.filter(id=>{const p=this.sim.props.get(id);return !!p&&!p.dynamic;});
   this.blocks=this.blocks.filter(b=>b.material==='foundation'||this.breakable.includes(b.id));this.build();}
 }
 /** Shift the poll clock during holds. */
 shift(dt:number){this.pollAt+=dt;}
 forget(id:string){for(const key of [...this.fields.keys()])if(key.endsWith(':'+id))this.fields.delete(key);}
 grid(g:NavGrid='party'){return this.grids?.[g];}
 private targetPos(id:string):Vec|undefined{const p=this.sim.players.get(id);return p?p.body.translation():undefined;}
 /** The flow field toward id on grid g, (re)built within the per-tick budget; a stale field is used when over budget. */
 field(id:string,g:NavGrid='party'):Int32Array|undefined{
  const grid=this.grids?.[g],t=this.targetPos(id);if(!grid||!t)return undefined;
  const key=g+':'+id,cell=cellOf(grid,t.x,t.z);let f=this.fields.get(key);
  if(f&&f.cell===cell)return f.dist;
  if(this.budgetTick!==this.sim.tick){this.budgetTick=this.sim.tick;this.budget=HordeNav.buildsPerTick;}
  if(this.budget<=0)return f&&f.cell>=0?f.dist:undefined;
  this.budget--;this.builds++;
  if(!f){f={cell,dist:new Int32Array(grid.w*grid.d),tick:this.sim.tick};this.fields.set(key,f);}
  buildFlowField(grid,cell,f.dist);f.cell=cell;f.tick=this.sim.tick;return f.dist;
 }
 direction(from:Vec,targetId:string,g:NavGrid='party'){const grid=this.grids?.[g],dist=this.field(targetId,g);return grid&&dist?descend(grid,dist,from.x,from.z):null;}
 distance(from:Vec,targetId:string,g:NavGrid='party'){
  const grid=this.grids?.[g],dist=this.field(targetId,g);if(grid&&dist)return fieldDistance(grid,dist,from.x,from.z);
  const t=this.targetPos(targetId);return t?Math.hypot(t.x-from.x,t.z-from.z):Infinity;
 }
 clear(a:Vec,b:Vec,g:NavGrid='party'){const grid=this.grids?.[g];return grid?lineClear(grid,a,b):false;}
}
