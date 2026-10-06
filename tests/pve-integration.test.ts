// PvE integration fixes (S7): low-frame-rate interpolation catch-up in PvE rooms only; nav seeds for players standing in
// cells the conservative inflation closes; the vent exit and door leaf geometry the streams agreed on.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {SnapshotBuffer} from '../src/client/snapshots.js';
import type {Snapshot} from '../src/shared/simulation.js';
import {arenaBlocks} from '../src/shared/arena.js';
import {buildNavGrid,buildFlowField,cellOf,fieldDistance,seedCell} from '../src/shared/pve/nav.js';
import {HORDE} from '../src/shared/pve/balance.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {ventStand} from '../src/client/pve/horde-view.js';

function presentedAfter(fps:number,pve:boolean,seconds=20){
 const b=new SnapshotBuffer();let now=0,next=0,tick=0,presented=0;
 const base={players:[],props:[],projectiles:[],events:[],phase:'fight',setNumber:1,round:1,generation:0,...(pve?{pve:{adds:[]}}:{})};
 const snap=()=>({...base,tick,time:tick/20}) as unknown as Snapshot;
 for(let i=0;i<fps*seconds;i++){now+=1000/fps;while(next<=now){tick++;b.push(snap(),next);next+=50;}presented=b.sample(snap(),'me',now).time;}
 return {live:tick/20,presented};
}
test('PvE rooms keep the presented clock with the authority below 20 fps; PvP keeps its original step cap',()=>{
 for(const fps of [60,20,8,5]){const r=presentedAfter(fps,true);assert.ok(Math.abs(r.live-r.presented)<.3,`PvE ${fps} fps: ${r.presented} vs ${r.live}`);}
 const fast=presentedAfter(60,false);assert.ok(Math.abs(fast.live-fast.presented)<.3);
 const slow=presentedAfter(8,false);assert.ok(slow.live-slow.presented>5,'PvP at 8 fps is unchanged (still lags; not this branch’s change)');
});

test('nav: a player in a service corridor seeds the flow field through walkable cells, not across a wall',()=>{
 const g=buildNavGrid(arenaBlocks('siren' as never),{radius:HORDE.nav.partyRadius});
 for(const [x,z] of [[-31.4,10.8],[31.3,-14.2],[-31.2,-10.5]] as const){
  const raw=cellOf(g,x,z);assert.equal(g.blocked[raw],1,`(${x},${z}) is in a closed cell`);
  const seed=seedCell(g,x,z);assert.equal(g.blocked[seed],0);
  const c={x:g.x0+(seed%g.w+.5)*g.cell,z:g.z0+(Math.floor(seed/g.w)+.5)*g.cell};
  assert.ok(Math.abs(c.x)>29.5,`seed stays in the corridor side of the cells (${c.x},${c.z})`);
  const dist=buildFlowField(g,seed);assert.ok(Number.isFinite(fieldDistance(g,dist,0,0)),'reachable from the hall centre');
 }
 const open=cellOf(g,0,0);assert.equal(seedCell(g,0,0),open,'a free cell is its own seed');
});

test('layout: vent exits sit at the client climb stand point; door leaves on the hall side of the walk start',()=>{
 for(const e of PVE_ENTRIES.filter(e=>e.kind==='vent')){const s=ventStand(e.e,e.yaw);assert.ok(Math.hypot(s.x-e.exit.x,s.z-e.exit.z)<1e-9,e.id);}
 for(const e of PVE_ENTRIES.filter(e=>e.kind==='door'))assert.ok(Math.abs(e.leaf!.z)===26.8&&Math.abs(e.from.z)>26.8&&Math.abs(e.exit.z)<26.8,e.id);
});
