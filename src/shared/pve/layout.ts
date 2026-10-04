// Ward Zero PvE data (design §3.3, §4.2, §6.1, D27). Pure data plus small geometry helpers; no Rapier, no clocks.
// sirenStageBlocks() is untouched: machines add their own colliders through machineBlocks() (edit map #19).
import type {ArenaBlock} from '../arena.js';
import {playerSpawn} from '../arena.js';
import {SIREN_VENTS,SIREN_ADD_SPAWNS,SIREN_CHIP_SPOTS} from '../siren-stage-layout.js';
import {CONFIG as C,type Vec} from '../config.js';
import type {EntryKind,MachineKind} from './contract.js';
import {PVE_SIREN} from './balance.js';

const xz=(x:number,z:number)=>({x,z});
const fwd=(yaw:number)=>xz(Math.sin(yaw),Math.cos(yaw));
/** Yaw that faces the hall centre from (x,z). */
export const faceCentre=(x:number,z:number)=>Math.atan2(-x,-z);
/** Standing capsule centre height for a standard player capsule on the Ward Zero floor (top at y=0). */
export const STAND_Y=C.playerHalfHeight+C.playerRadius+.05;

export interface PveMachine{m:number;id:string;kind:MachineKind;x:number;z:number;yaw:number;
 /** Interaction centre (spot + fwd·0.7): interact radius PRICES.interactRadius from here. */ front:{x:number;z:number};
 /** Where recruits step out (spot + fwd·1.3). */ dispense:{x:number;z:number};
 /** Where a dispensed chip lands (spot + fwd·CHIP_DROP_DISTANCE), clear of the machine. */ chipDrop:{x:number;z:number}}
/** Owner 2026-10-03: chips were landing too close to the machine. Default throw distance; machines whose front has an
 *  obstacle closer than that pass a shorter one (probed so the drop point stays on open, walkable floor). */
export const CHIP_DROP_DISTANCE=2.7;
const machine=(m:number,id:string,kind:MachineKind,x:number,z:number,drop=CHIP_DROP_DISTANCE):PveMachine=>{const yaw=faceCentre(x,z),f=fwd(yaw);
 return {m,id,kind,x,z,yaw,front:xz(x+f.x*.7,z+f.z*.7),dispense:xz(x+f.x*1.3,z+f.z*1.3),chipDrop:xz(x+f.x*drop,z+f.z*drop)};};
const spot=(i:number)=>SIREN_CHIP_SPOTS[i];
/** §4.2: 4 chip machines, 2 recruit pods on the existing chip pads, 2 med stations found by the clearance probe. */
export const PVE_MACHINES:readonly PveMachine[]=[
 machine(0,'chip-0','chip',spot(0).x,spot(0).z), machine(1,'chip-1','chip',spot(1).x,spot(1).z),
 machine(2,'chip-2','chip',spot(2).x,spot(2).z,2), machine(3,'chip-3','chip',spot(3).x,spot(3).z,2),
 machine(4,'chip-4','recruit',spot(4).x,spot(4).z), machine(5,'chip-5','recruit',spot(5).x,spot(5).z),
 machine(6,'med-0','med',6.5,-20), machine(7,'med-1','med',-6.5,20),
];
export const MACHINE_SIZE={x:1.3,y:2.2,z:.9} as const;
/** Machine colliders: a foundation block 1.3×2.2×0.9 at spot − fwd·0.2, id `pve-machine-<m>` (the renderer hides the box). */
export function machineBlocks():ArenaBlock[]{
 return PVE_MACHINES.map(m=>{const f=fwd(m.yaw);return {id:`pve-machine-${m.m}`,pos:{x:m.x-f.x*.2,y:MACHINE_SIZE.y/2,z:m.z-f.z*.2},
  size:{...MACHINE_SIZE},material:'foundation',rotation:{x:0,y:Math.sin(m.yaw/2),z:0,w:Math.cos(m.yaw/2)}};});
}

export interface PveEntry{e:number;id:string;kind:EntryKind;
 /** Walk start (doors and cells: behind the opening; vents: the vent itself). */ from:{x:number;z:number};
 /** Where the add's body is created (active). */ exit:{x:number;z:number};
 yaw:number;
 /** Doors only: the closed leaf plane (|z| of the leaves), the two hinge x positions
  *  and the leaf width. Leaves swing INTO the hall (S6 animates them from PVE_TIMING.door). S7: the leaf plane is the
  *  rendered door (wall face 28 − DOOR_LEAF_D 1.2 = 26.8), on the hall side of `from` so the waiting add is hidden. */
 leaf?:{z:number;hinges:[number,number];width:number;swing:1|-1};
 /** Cells only: the gate (teeth) plane x. The teeth rise over PVE_TIMING.cell.open. */
 gate?:{x:number}}
/** Vent climb end (S7 from S5): just past the square pit edge the add faces, centre + fwd·(1.08/max(|sin|,|cos|)+0.38);
 *  matches the client's ventStand() so the body appears where the climb animation ends. */
const ventExit=(x:number,z:number,yaw:number)=>{const f=fwd(yaw),d=1.08/Math.max(Math.abs(f.x),Math.abs(f.z),1e-6)+.38;return xz(x+f.x*d,z+f.z*d);};
const vents=SIREN_VENTS.map((v,i):PveEntry=>{const yaw=faceCentre(v.x,v.z);return {e:i,id:v.id,kind:'vent',from:xz(v.x,v.z),exit:ventExit(v.x,v.z,yaw),yaw};});
const cells=SIREN_ADD_SPAWNS.filter(a=>a.kind==='cell').map((a,i):PveEntry=>{const s=Math.sign(a.x);
 return {e:8+i,id:a.id,kind:'cell',from:xz(s*28.6,a.z),exit:xz(s*25.6,a.z),yaw:a.yaw,gate:{x:s*24.3}};});
const doors=SIREN_ADD_SPAWNS.filter(a=>a.kind==='door').map((a,i):PveEntry=>{const s=Math.sign(a.z);
 return {e:12+i,id:a.id,kind:'door',from:xz(a.x,s*27.3),exit:xz(a.x,s*25.0),yaw:a.yaw,leaf:{z:s*26.8,hinges:[a.x-1.48,a.x+1.48],width:1.42,swing:-s as 1|-1}};});
/** 16 entries, index = e: 0–7 vents (SIREN_VENTS order), 8–11 cells (cell-wn/ws/en/es), 12–15 doors (door-nw/ne/sw/se). */
export const PVE_ENTRIES:readonly PveEntry[]=[...vents,...cells,...doors];

export const PVE_LAYOUT={
 /** D27: south great doors, facing north. */
 bossSpawn:{x:PVE_SIREN.spawn.x,z:PVE_SIREN.spawn.z,yaw:PVE_SIREN.spawn.yaw},
 /** Respawn pads: the eight standard player spawn pads (playerSpawn). */
 pads:Array.from({length:8},(_,i)=>{const p=playerSpawn(i);return xz(p.x,p.z);}) as readonly {x:number;z:number}[],
} as const;
/** Standing capsule centre over (x,z) on the Ward Zero floor for a capsule of the given half height and radius. */
export const standCentre=(x:number,z:number,halfHeight:number=C.playerHalfHeight,radius:number=C.playerRadius):Vec=>({x,y:halfHeight+radius+.05,z});
