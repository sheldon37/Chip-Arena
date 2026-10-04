// Lag-spike regressions (2026-10-04): GPU programs must not be re-linked when per-event effect materials come and go,
// and the killcam history must not keep a separate copy of the static map in every frame (long major-GC pauses).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {ShaderWarmup} from '../src/client/shader-warmup.js';
import {Killcam,sameData} from '../src/client/killcam.js';
import {Simulation,initPhysics,type Snapshot} from '../src/shared/simulation.js';
await initPhysics();

function fakeRenderer(){
 const programs:{usedTimes:number}[]=[];let compiled=0;
 const r={info:{programs},compile:()=>{compiled++;}} as unknown as T.WebGLRenderer;
 return {r,programs,compiled:()=>compiled};
}

test('programs linked by the renderer stay pinned, once each',()=>{
 const {r,programs}=fakeRenderer(),scene=new T.Scene(),w=new ShaderWarmup(r,scene);
 programs.push({usedTimes:1},{usedTimes:3});w.retain();
 assert.deepEqual(programs.map(p=>p.usedTimes),[2,4]);
 w.retain();assert.deepEqual(programs.map(p=>p.usedTimes),[2,4],'no double pin');
 programs.push({usedTimes:1});w.retain();assert.deepEqual(programs.map(p=>p.usedTimes),[2,4,2]);
 // A material release (three: --usedTimes, delete at 0) can no longer drop a pinned program.
 for(const p of programs)p.usedTimes--;
 assert.ok(programs.every(p=>p.usedTimes>0));
});

test('warm-up compiles the scene once per composition key, with hidden probes for per-event variants',async()=>{
 const tick=()=>new Promise(r=>setImmediate(r));
 const {r,compiled}=fakeRenderer(),scene=new T.Scene(),w=new ShaderWarmup(r,scene),camera=new T.PerspectiveCamera();
 assert.equal(w.probes.parent,scene);assert.equal(w.probes.visible,false);assert.ok(w.probes.children.length>=20);
 const kinds=new Set(w.probes.children.map(o=>(o as T.Mesh).material).map(m=>(m as T.Material).type));
 for(const k of ['MeshBasicMaterial','MeshLambertMaterial','SpriteMaterial','PointsMaterial','LineBasicMaterial'])assert.ok(kinds.has(k),k);
 for(let i=0;i<5;i++){w.request('foundry|0|1|1|1');w.update(camera);await tick();}
 assert.equal(compiled(),1,'one compile after the key changes');
 for(let i=0;i<5;i++){w.request('foundry|0|1|1|2');w.update(camera);await tick();}
 assert.equal(compiled(),2);
 // With a Siren around, the scene is also compiled once with the seeker's extra point light, which is removed again.
 const lights=()=>{let n=0;scene.traverse(o=>{if((o as T.PointLight).isPointLight)n++;});return n;},before=lights();
 for(let i=0;i<5;i++){w.request('foundry|1|1|1|2',true);w.update(camera);await tick();}
 assert.equal(compiled(),4);assert.equal(lights(),before);
});

test('the first disposed ShaderMaterial of a shader source is kept, so its program key stays stable',()=>{
 new ShaderWarmup(fakeRenderer().r,new T.Scene());
 const src={vertexShader:'void main(){gl_Position=vec4(0.0);} // lag-test',fragmentShader:'void main(){gl_FragColor=vec4(1.0);} // lag-test'};
 let fired=0;const make=(compiled:boolean)=>{const m=new T.ShaderMaterial(src);m.addEventListener('dispose',()=>fired++);
  // three calls customProgramCacheKey() when it registers the material's shader stages for a program.
  if(compiled)assert.equal(m.customProgramCacheKey(),new T.Material().customProgramCacheKey());return m;};
 make(false).dispose();assert.equal(fired,1,'a material that was never compiled holds nothing and is disposed');
 for(let i=0;i<3;i++)make(true).dispose();
 assert.equal(fired,3,'one compiled anchor per source; later materials dispose normally');
});

test('sameData compares plain snapshot data structurally',()=>{
 assert.ok(sameData({a:1,b:{x:[1,2],y:NaN}},{b:{x:[1,2],y:NaN},a:1}));
 assert.ok(!sameData({a:1},{a:1,b:undefined}));assert.ok(!sameData({a:[1]},{a:{0:1}}));assert.ok(!sameData({p:{x:1}},{p:{x:1.0001}}));
});

test('killcam history shares unchanged props and events between frames and copies changed ones',()=>{
 const sim=new Simulation({map:'foundry'});sim.addPlayer('me');sim.addPlayer('bot');sim.phase='fight';
 const base=sim.snapshot();sim.dispose();
 assert.ok(base.props.length>20,'a map with props');
 const cam=new Killcam(),frames:Snapshot[]=[];
 for(let i=0;i<4;i++){
  const s=structuredClone(base);s.time=i/20;s.tick=i*3;
  s.events=[{id:1,time:0,type:'hit',pos:{x:0,y:0,z:0}},...(i>=2?[{id:2,time:.1,type:'shot',pos:{x:1,y:0,z:0}}]:[])];
  if(i===3)s.props[0].pos.x+=1;
  cam.observe(s,'me',i*50);
  frames.push((cam as unknown as {frames:{state:Snapshot}[]}).frames.at(-1)!.state);
  s.props[1].pos.x=999;s.events[0].pos.x=999;// the live snapshot changing later never reaches the history
 }
 assert.equal(frames.length,4);
 assert.equal(frames[1].props[1],frames[0].props[1],'static prop shared');
 assert.equal(frames[3].props[1],frames[0].props[1]);
 assert.notEqual(frames[3].props[0],frames[2].props[0],'moved prop copied');
 assert.equal(frames[3].props[0].pos.x,base.props[0].pos.x+1);
 assert.equal(frames[2].props[0].pos.x,base.props[0].pos.x);
 assert.equal(frames[3].events[0],frames[0].events[0],'event shared');
 assert.equal(frames[0].events[0].pos.x,0);assert.equal(frames[0].props[1].pos.x,base.props[1].pos.x);
 assert.equal(frames[3].events[1],frames[2].events[1]);
 assert.notEqual(frames[1].players,frames[0].players,'players are still copied per frame');
 for(const f of frames)assert.equal(JSON.stringify(f.props.map(p=>p.id)),JSON.stringify(base.props.map(p=>p.id)));
});
