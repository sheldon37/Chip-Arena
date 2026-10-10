// Build-time only: run the pinned original client/siren/fx.ts (SirenFx.updateMarks, markBeeps) and client/siren-audio.ts
// and record Marked's "You...." crosshair, the lock-on telegraph the source draws on the marked player, for
// Tests/SirenMarkChecks.cs to replay through Core/NativeSirenFx.cs (NativeSirenMark): when it shows, its billboard
// transform, the ring, inner square and tick poses, colour and opacity frame by frame, the beep schedule and the beep
// tones. Usage: node tools/export_siren_mark_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/siren/fx.ts':'e4ffbcb23188e7a9fb733c75314f0a2229429e6d','src/client/siren-audio.ts':'7d7604caa74bb0e632e0e99378d1ce2517fb8641',
 'src/shared/siren-contract.ts':'ad494dfd203d8f12cdfc69b2c73004807af28ccc'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const fxm=await import(pathToFileURL(path.join(source,'src/client/siren/fx.ts')).href),audio=await import(pathToFileURL(path.join(source,'src/client/siren-audio.ts')).href);
const contract=await import(pathToFileURL(path.join(source,'src/shared/siren-contract.ts')).href);
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const p9=v=>Number(v.toPrecision(9));const v3=o=>[o.x,o.y,o.z].map(p9);
const out={timing:{...contract.SIREN_TIMING.mark},palette:{warn:0xff2f44},beeps:[],frames:[],audio:[]};

// 1. markBeeps for the real lock (voice 1.2 s then lock 1.2 s) and other spans (the schedule is general).
for(const [lockAt,throwAt] of [[11.2,12.4],[5,5.5],[0,3],[2,2.01],[100.25,101.45]])out.beeps.push({lockAt,throwAt,at:fxm.markBeeps(lockAt,throwAt).map(p9)});

// 2. updateMarks frame by frame: the target 2 m off, a third-person camera, a mark cast at 10 (lock 11.2, throw 12.4).
const scene=new T.Scene(),fx=new fxm.SirenFx(scene),camera=new T.PerspectiveCamera(60,16/9,.08,400);
camera.position.set(-1.2,2.1,4.6);camera.lookAt(2,1.1,-3);camera.updateMatrixWorld(true);
const target={id:'t',pos:{x:2,y:.95,z:-3},yaw:0,hp:100},siren={id:'s',pos:{x:-4,y:.95,z:6},yaw:0,hp:100};
const mark={id:'m1',owner:'s',target:'t',at:10,lockAt:11.2,throwAt:12.4,shot:'q1'};
const state=(time,marks,players=[target,siren])=>({time,players,projectiles:[],siren:{patches:[],eruptions:[],rains:[],marks,grabs:[]}});
const crosshair=()=>fx.group.children.find(o=>o.name==='siren-crosshair');
const times=[10,11.19,11.2,11.24,11.3,11.45,11.5,11.62,11.8,11.95,12.1,12.2,12.3,12.38,12.4,12.55,13.0,14.2,17.05];
for(const time of times){
 // The pinned source moves the target between frames; the crosshair follows it.
 if(time>=12)target.pos={x:2.4,y:.95,z:-2.6};
 fx.update(state(time,[mark]),time,new Map(),1,camera);const g=crosshair();
 if(!g){out.frames.push({time,shown:false});continue;}
 const [ring,inner,...ticks]=g.children,mat=ring.material;
 out.frames.push({time,shown:true,pos:v3(g.position),quat:[g.quaternion.x,g.quaternion.y,g.quaternion.z,g.quaternion.w].map(p9),scale:p9(g.scale.x),
  ring:{z:p9(ring.rotation.z),inner:ring.geometry.parameters.innerRadius,outer:ring.geometry.parameters.outerRadius,segments:ring.geometry.parameters.thetaSegments},
  inner:{z:p9(inner.rotation.z),inner:inner.geometry.parameters.innerRadius,outer:inner.geometry.parameters.outerRadius,segments:inner.geometry.parameters.thetaSegments},
  ticks:ticks.map(t=>({p:v3(t.position),z:p9(t.rotation.z),s:v3(t.scale)})),
  color:mat.uniforms.uColor.value.getHex(),opacity:p9(mat.uniforms.uOpacity.value),depthTest:mat.depthTest,depthWrite:mat.depthWrite,transparent:mat.transparent,
  doubleSide:mat.side===T.DoubleSide,renderOrder:g.renderOrder,sharedMaterial:[inner,...ticks].every(o=>o.material===mat)});
}
// The target leaves (or the mark is removed): the crosshair goes with it the same frame.
fx.update(state(17.1,[mark],[siren]),17.1,new Map(),1,camera);out.frames.push({time:17.1,shown:!!crosshair(),targetGone:true});
fx.update(state(17.2,[]),17.2,new Map(),1,camera);out.frames.push({time:17.2,shown:!!crosshair(),markGone:true});
out.camera={pos:v3(camera.position),quat:[camera.quaternion.x,camera.quaternion.y,camera.quaternion.z,camera.quaternion.w].map(p9)};

// 3. siren-audio.ts beeps: one oscillator per beep, scheduled on the audio clock, tone rising with the lock.
{const played=[];let clock=0;const node=()=>({connect(n){return n;},disconnect(){}});
 const context={state:'running',get currentTime(){return clock;},createOscillator(){const o={...node(),frequency:{value:0},start(when){played.push({freq:p9(o.frequency.value),when:p9(when)});},stop(){}};return o;},
  createGain(){return {...node(),gain:{setValueAtTime(v,w){this.v=v;},exponentialRampToValueAtTime(){}}};}};
 const a=new audio.SirenAudio(),marks=[{id:'m1',owner:'s',target:'t',at:10,lockAt:11.2,throwAt:12.4}];
 for(let k=0;k<=100;k++){const t=11+k*.02;clock=t-11;a.update({time:t,generation:1,bossSong:{startedAt:0},siren:{marks}},context,node());}
 out.audio=played;}

fs.writeFileSync(path.join(root,'Tests/siren-mark-parity.json'),JSON.stringify({source:pinned,...out})+'\n');
console.log('Exported',out.beeps.length,'beep schedules,',out.frames.length,'crosshair frames,',out.audio.length,'beeps');
