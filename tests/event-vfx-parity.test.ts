// EventVfx parity (design §3.4b; FOUNDATION, handed to the CHIP VFX SWEEP). FOUNDATION moved the renderer's per-event body,
// blastEvent/blastFields/burst/beam and the particles verbatim into src/client/event-vfx.ts. This test feeds one event of
// every moved type (plus panic-button `nova` and chip `arc`s) and pins what the F base drew: scene meshes added, BurstPool
// spawns, ChipBlasts blasts (style:radius:quality) and fields, beam/torus particles and the hit/shake flags. The expected
// values were cross-checked against the untouched cfdce4b renderer code (identical). V may change an expected value only
// with a stated reason per converted chip (design §19.2); `nova` (panic-button) and `arc` must keep rendering.
// V sweep update (2026-10-03): the F-base pins are kept below as F_BASE for reference; EXPECTED is the swept output and
// every difference is listed with its reason next to it. The leet/stage entries are unchanged (byte-identical branches).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {EventVfx} from '../src/client/event-vfx.js';
import type {ChipBlasts} from '../src/client/chip-blasts.js';
import type {BurstPool} from '../src/client/burst-pool.js';
const ME='me';
function eventList(){let id=0;const e=(type:string,extra:Record<string,unknown>={})=>({id:++id,time:10,type,pos:{x:1,y:1,z:1},...extra});
 return [e('leet-control',{from:{x:0,y:1,z:0}}),e('leet-nuke'),e('stage-saw-hit'),e('stage-spikes-rise'),e('stage-lava-hit'),e('teleport',{from:{x:3,y:1,z:0}}),e('teleport'),e('summon'),
  e('hit',{source:ME,target:'b'}),e('hit',{source:'b',target:ME,message:'burn'}),e('hit',{source:'b',target:ME}),e('impact',{from:{x:0,y:1,z:-3},source:'w'}),e('impact',{source:'a'}),e('break',{color:0xc78342}),
  e('explosion',{message:'implosion-bomb',radius:4}),e('explosion',{radius:3}),e('explosion',{message:'frag',radius:4.2}),e('debrisHit',{target:ME}),e('parry'),e('blockHit'),e('reflect'),e('blockAbsorb'),e('collapse'),
  e('arc',{from:{x:2,y:1,z:2}}),e('arc',{radius:3}),e('arc'),e('push',{radius:5}),e('pull',{radius:5}),e('nova',{radius:4.2}),e('nova',{radius:6,message:'panic-button'}),
  e('blockPulse',{radius:3,mods:{supernova:1}}),e('blockPulse',{radius:3}),e('meleeRebound'),e('vacuum',{radius:5}),e('freeze',{radius:.9}),e('ability-cast',{message:'frag-grenade'}),e('slowed')];
}
function stateFor(events:any[]){return {time:10,tick:600,events,fields:[{id:'f1',owner:'a',kind:'implosion',pos:{x:0,y:.2,z:0},radius:4,born:9.5,until:10.4,arm:10.1,damage:30,fx:'implosion-bomb'},{id:'f2',owner:'a',kind:'black-hole',pos:{x:5,y:.2,z:0},radius:5,born:9,until:12.5,arm:9.6,damage:10,fx:'black-hole'}],
 players:[{id:ME,pos:{x:0,y:1,z:0},spec:'pistol'},{id:'w',pos:{x:0,y:1,z:5},spec:'water'},{id:'a',pos:{x:0,y:1,z:5},spec:'fire'},{id:'b',pos:{x:0,y:1,z:5},spec:'sword'}]} as any;}

// Buffer replacements preserve the same draw calls; count new effect objects, not resized pool meshes.
function instrument(scene:T.Scene,chip:ChipBlasts,bursts:BurstPool){const log={added:0,spawns:[] as number[],blasts:[] as string[],fields:[] as string[],impacts:[] as string[],lines:0};
 const impact=chip.impact.bind(chip);chip.impact=((pos,radius,hot,o)=>{log.impacts.push(`${radius}:${hot.toString(16)}${o.ground?':ground':''}`);return impact(pos,radius,hot,o);}) as typeof chip.impact;
 const line=chip.line.bind(chip);chip.line=((...a:Parameters<typeof chip.line>)=>{log.lines++;return line(...a);}) as typeof chip.line;const add=scene.add.bind(scene);scene.add=((...o:T.Object3D[])=>{log.added+=o.filter(x=>!['chip-blast-voxels','chip-blast-glow','pooled-impact-sparks'].includes(x.name)).length;return add(...o);}) as typeof scene.add;
 const spawn=bursts.spawn.bind(bursts);bursts.spawn=((pos,color,count,power,q)=>{log.spawns.push(count);return spawn(pos,color,count,power,q);}) as typeof bursts.spawn;
 const blast=chip.blast.bind(chip);chip.blast=((style,pos,radius,o)=>{log.blasts.push(`${style}:${radius}:${(o?.quality??1).toFixed(3)}${o?.tint?':tint':''}`);return blast(style,pos,radius,o);}) as typeof chip.blast;
 const field=chip.field.bind(chip);chip.field=((f,...rest)=>{log.fields.push(f.id);return field(f,...rest);}) as typeof chip.field;return log;}
/** F-base output (cfdce4b renderer code, verified identical), for reference. */
const F_BASE={added:27,spawns:[24,70,32,14,18,8,24,18,24,12,6,6,6,6,18,16,16,16,6,6,6,6,6,6,28,28,28,28,28,28,28,28],
 blasts:['void:1.2:0.500','void:1.2:0.500','implosion:4:1.000','fire:3:0.550','fire:4.2:1.000','shock:3:0.600','kinetic:6:1.000','nova:3:1.000','kinetic:3:1.000','void:5:1.000','frost:0.9:0.700'],
 fields:['f1','f2'],particles:27,hits:1,shakes:2};
/**
 * Swept output. Differences from F_BASE, by event, with the reason:
 * - teleport (wormhole, swap-shop, blink-cut, second-chance; Blink Strike): the 24/18/24-spark bursts and the beam mesh
 *   become a voxel departure impact (0.9 m) and a voxel streak (line); the arrival keeps its ChipBlasts void blast.
 * - summon (Necronomicon, puppet-master, graveyard-shift, corpse-party): the 12-spark burst becomes a 1.2 m ground impact.
 * - explosion (every chip/ability blast): the extra 16-spark burst drawn on top of the volumetric blast is removed;
 *   `frag` now maps to kinetic with its grey tint (§13.1; F mapped it to the fire default).
 * - arc with a radius (Static Discharge, Tesla Rhythm): the four random beam meshes on top of its shock blast are removed;
 *   arc with `from` (chains, Ball Lightning) and a bare arc draw voxel lines instead of beam meshes (1 + 4 lines).
 * - water bolt impact: the per-event TorusGeometry ring becomes a 0.6 m voxel splash.
 * - push/pull (legacy E only, simulation.ts): bursts, torus and the six pull beams are deleted (removed by source, §13.3).
 * - nova with message panic-button: its 28-spark burst is removed (the ChipBlasts kinetic blast stays). INTEGRATION removed
 *   the legacy lightning E (simulation.ts), the only other nova source, and with it that nova's 28-spark burst.
 * - blockPulse (Counter Pulse, Supernova) and vacuum (Vacuum Impact, Reverse Grenade, Singularity): the 28-spark bursts are
 *   removed; their ChipBlasts nova/kinetic/void blasts stay (chip-effects' flat pulse rings are gone too).
 * - meleeRebound (Rebound Engine, Counterweight, Bulwark, Repulsor): 28-spark burst and flat rings become a 1.5 m voxel impact.
 * - slowed (Soak, Tidal Wave, Geyser Wall, Shadow Shackles): new 0.8 m voxel impact (water blue, violet for shackles).
 * - added/particles 27 → 9: only the DEV/stage beams (leet 1, saw 7) and the weapon impact trail remain meshes.
 */
const EXPECTED={added:9,spawns:[24,70,32,14,18,8,6,6,6,6,18,6,6,6,6,6,6],
 blasts:['void:1.2:0.500','void:1.2:0.500','implosion:4:1.000','fire:3:0.550','kinetic:4.2:1.000:tint','shock:3:0.600','kinetic:6:1.000','nova:3:1.000','kinetic:3:1.000','void:5:1.000','frost:0.9:0.700'],
 fields:['f1','f2'],impacts:['0.9:b66cff','1.2:93db67:ground','0.6:8de9ee','1.5:91f7eb','0.8:8de9ee'],lines:6,particles:9,hits:1,shakes:2};

test('EventVfx draws the swept visuals for every moved event type (F-base differences listed above)',()=>{
 const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:true})),log=instrument(scene,vfx.chipBlasts,vfx.bursts),events=eventList(),state=stateFor(events);let hits=0,shakes=0;
 for(const e of events){vfx.blastEvent(e as never);const r=vfx.event(e as never,state,ME);if(r.hit)hits++;if(r.shake)shakes++;}vfx.fields(state);
 assert.deepEqual({...log,particles:vfx.particles.length,hits,shakes},EXPECTED);
 assert.deepEqual(EXPECTED.spawns.slice(0,6),F_BASE.spawns.slice(0,6),'leet and stage bursts are untouched');assert.equal(EXPECTED.hits,F_BASE.hits);assert.equal(EXPECTED.shakes,F_BASE.shakes);
 for(let i=0;i<60;i++)vfx.update(1/60);assert.equal(vfx.particles.length,0,'beams and tori expire');vfx.clear();assert.equal(vfx.particles.length,0);
});
test('panic-button nova and chip arcs still render (blast style or beam)',()=>{
 const run=(e:Record<string,unknown>)=>{const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:true})),log=instrument(scene,vfx.chipBlasts,vfx.bursts),ev={id:1,time:10,pos:{x:0,y:1,z:0},...e};vfx.blastEvent(ev as never);vfx.event(ev as never,stateFor([ev]),ME);return {...log,beams:vfx.particles.length,voxels:vfx.chipBlasts.glow.count+vfx.chipBlasts.solid.count};};
 const panic=run({type:'nova',message:'panic-button',radius:6});assert.deepEqual(panic.blasts,['kinetic:6:1.000']);assert.ok(panic.voxels>40);
 // Arcs are voxel lines now (V sweep): one line for a chain arc, four for a bare arc, the shock blast for a radius arc.
 const chain=run({type:'arc',from:{x:2,y:1,z:0}});assert.equal(chain.lines,1);assert.equal(chain.beams,0);assert.ok(chain.voxels>=8);assert.deepEqual(run({type:'arc',radius:3}).blasts,['shock:3:0.600']);assert.equal(run({type:'arc'}).lines,4);
});
test('quality and trail options are read on every call',()=>{
 let quality=1,trails=true;const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality,compat:false,trails}));const ev={id:1,time:10,type:'impact',pos:{x:0,y:1,z:0},from:{x:0,y:1,z:-2}};
 vfx.event(ev as never,stateFor([ev]),ME);assert.equal(vfx.particles.length,1);trails=false;vfx.event({...ev,id:2} as never,stateFor([ev]),ME);assert.equal(vfx.particles.length,1);
 quality=0;vfx.beam({x:0,y:0,z:0},{x:1,y:0,z:0},0xffffff);assert.equal(vfx.particles.length,1,'quality 0 skips beams');
});
