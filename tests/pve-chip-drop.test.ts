import {test} from 'node:test';
import assert from 'node:assert/strict';
import {arenaBlocks} from '../src/shared/arena.js';
import {buildNavGrid,cellOf} from '../src/shared/pve/nav.js';
import {machineBlocks,PVE_MACHINES,CHIP_DROP_DISTANCE} from '../src/shared/pve/layout.js';

// Owner 2026-10-03: chips popped out too close to the chip machine. They now land CHIP_DROP_DISTANCE in front of it,
// on open floor (a free party-nav cell), while recruits still step out at the pod's `dispense` point.
test('chip machines drop chips further out, on walkable floor',()=>{
 const grid=buildNavGrid([...arenaBlocks('siren'),...machineBlocks()],{radius:.42});
 const chips=PVE_MACHINES.filter(m=>m.kind==='chip');
 assert.equal(chips.length,4);
 for(const m of chips){
  const d=Math.hypot(m.chipDrop.x-m.x,m.chipDrop.z-m.z),pod=Math.hypot(m.dispense.x-m.x,m.dispense.z-m.z);
  assert.ok(d<=CHIP_DROP_DISTANCE+1e-9&&d>=pod+.6,`${m.id} drops ${d.toFixed(2)} m out (pod ${pod.toFixed(2)})`);
  // every point of the throw path from the machine front to the drop is open floor (no landing behind an obstacle)
  for(let s=pod;s<=d+1e-9;s+=.1){const k=s/d,x=m.x+(m.chipDrop.x-m.x)*k,z=m.z+(m.chipDrop.z-m.z)*k,c=cellOf(grid,x,z);assert.ok(c>=0&&!grid.blocked[c],`${m.id} path open at ${s.toFixed(1)} m`);}
 }
});
