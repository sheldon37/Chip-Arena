// EventVfx parity (design §3.4b; FOUNDATION, handed to the CHIP VFX SWEEP). FOUNDATION moved the renderer's per-event body,
// blastEvent/blastFields/burst/beam and the particles verbatim into src/client/event-vfx.ts. This test feeds one event of
// every moved type (plus panic-button `nova` and chip `arc`s) and pins what the F base drew: scene meshes added, BurstPool
// spawns, ChipBlasts blasts (style:radius:quality) and fields, beam/torus particles and the hit/shake flags. The expected
// values were cross-checked against the untouched cfdce4b renderer code (identical). V may change an expected value only
// with a stated reason per converted chip (design §19.2); `nova` (panic-button) and `arc` must keep rendering.
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

function instrument(scene:T.Scene,chip:ChipBlasts,bursts:BurstPool){const log={added:0,spawns:[] as number[],blasts:[] as string[],fields:[] as string[]};const add=scene.add.bind(scene);scene.add=((...o:T.Object3D[])=>{log.added+=o.length;return add(...o);}) as typeof scene.add;
 const spawn=bursts.spawn.bind(bursts);bursts.spawn=((pos,color,count,power,q)=>{log.spawns.push(count);return spawn(pos,color,count,power,q);}) as typeof bursts.spawn;
 const blast=chip.blast.bind(chip);chip.blast=((style,pos,radius,o)=>{log.blasts.push(`${style}:${radius}:${(o?.quality??1).toFixed(3)}`);return blast(style,pos,radius,o);}) as typeof chip.blast;
 const field=chip.field.bind(chip);chip.field=((f,...rest)=>{log.fields.push(f.id);return field(f,...rest);}) as typeof chip.field;return log;}
/** F-base output (cfdce4b renderer code, verified identical). */
const EXPECTED={added:27,spawns:[24,70,32,14,18,8,24,18,24,12,6,6,6,6,18,16,16,16,6,6,6,6,6,6,28,28,28,28,28,28,28,28],
 blasts:['void:1.2:0.500','void:1.2:0.500','implosion:4:1.000','fire:3:0.550','fire:4.2:1.000','shock:3:0.600','kinetic:6:1.000','nova:3:1.000','kinetic:3:1.000','void:5:1.000','frost:0.9:0.700'],
 fields:['f1','f2'],particles:27,hits:1,shakes:2};

test('EventVfx reproduces the F-base renderer event visuals for every moved event type',()=>{
 const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:true})),log=instrument(scene,vfx.chipBlasts,vfx.bursts),events=eventList(),state=stateFor(events);let hits=0,shakes=0;
 for(const e of events){vfx.blastEvent(e as never);const r=vfx.event(e as never,state,ME);if(r.hit)hits++;if(r.shake)shakes++;}vfx.fields(state);
 assert.deepEqual({...log,particles:vfx.particles.length,hits,shakes},EXPECTED);
 for(let i=0;i<60;i++)vfx.update(1/60);assert.equal(vfx.particles.length,0,'beams and tori expire');vfx.clear();assert.equal(vfx.particles.length,0);
});
test('panic-button nova and chip arcs still render (blast style or beam)',()=>{
 const run=(e:Record<string,unknown>)=>{const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:true})),log=instrument(scene,vfx.chipBlasts,vfx.bursts),ev={id:1,time:10,pos:{x:0,y:1,z:0},...e};vfx.blastEvent(ev as never);vfx.event(ev as never,stateFor([ev]),ME);return {...log,beams:vfx.particles.length};};
 assert.deepEqual(run({type:'nova',message:'panic-button',radius:6}).blasts,['kinetic:6:1.000']);
 assert.equal(run({type:'arc',from:{x:2,y:1,z:0}}).beams,1);assert.deepEqual(run({type:'arc',radius:3}).blasts,['shock:3:0.600']);assert.equal(run({type:'arc'}).beams,4);
});
test('quality and trail options are read on every call',()=>{
 let quality=1,trails=true;const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality,compat:false,trails}));const ev={id:1,time:10,type:'impact',pos:{x:0,y:1,z:0},from:{x:0,y:1,z:-2}};
 vfx.event(ev as never,stateFor([ev]),ME);assert.equal(vfx.particles.length,1);trails=false;vfx.event({...ev,id:2} as never,stateFor([ev]),ME);assert.equal(vfx.particles.length,1);
 quality=0;vfx.beam({x:0,y:0,z:0},{x:1,y:0,z:0},0xffffff);assert.equal(vfx.particles.length,1,'quality 0 skips beams');
});
