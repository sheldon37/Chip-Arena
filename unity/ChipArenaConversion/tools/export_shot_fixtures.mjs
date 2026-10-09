// Build-time only: run the pinned original projectile visuals on scripted projectiles and record what they draw, frame by
// frame, for Tests/ShotChecks.cs. The renderer's own statements are cut verbatim from client/renderer.ts (the module
// constants, burst/beam, abilityShot, the shot loop and the frame calls around it) into a small stage class; everything
// they call is the source module itself: chip-effects.ts, event-vfx.ts (BurstPool sparks and beams), projectile-trails.ts,
// projectile-glow.ts, shared/content.ts and shared/elements.ts. Math.random is a seeded mulberry32 for repeatable runs.
// Usage: node tools/export_shot_fixtures.mjs <source checkout at 05950ea with node_modules>
// The fxref harness imports stageSource() from here to render the same stage in a browser.
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createRequire} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
export const SOURCE_FILES={
 'src/client/renderer.ts':'74ea0229d4df85a5f7ca45c291be10378d28d592','src/client/chip-effects.ts':'e73a6749e2ac39a9815b0b24a3ba13a2fcb730a0',
 'src/client/burst-pool.ts':'ecfeb774ec0a3d3e4a2867f757391596d287fd67','src/client/event-vfx.ts':'853efff749a052b17a82242745fdc0b7ed757a31',
 'src/client/projectile-trails.ts':'a6634eefab98f05db131e6c91fa21266b85dcfc7','src/client/projectile-glow.ts':'3555d72423610d6b53518d6fb847b4410728c939',
 'src/shared/content.ts':'b0613af732ff5a8fb28374212f4631ae41a0ca43','src/shared/elements.ts':'daece9fe825424298366e3689ac25ad8dad039a9',
 'src/client/chip-blasts.ts':'cfaf2150bc0493876bb0e93f614719e4be1e3aab'};
export function verify(source){
 for(const [file,blob] of Object.entries(SOURCE_FILES)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
}
const once=(text,start,end,what)=>{const a=text.indexOf(start);if(a<0||text.indexOf(start,a+1)>=0)throw Error('renderer.ts anchor not unique: '+what);const b=end===undefined?a+start.length:text.indexOf(end,a);if(b<0)throw Error('renderer.ts anchor end missing: '+what);return text.slice(a,b);};
/** The stage module: renderer.ts statements verbatim inside a minimal renderer object (TypeScript, bundled with esbuild). */
export function stageSource(source){
 verify(source);const r=fs.readFileSync(path.join(source,'src/client/renderer.ts'),'utf8');
 const lines=r.split('\n'),line=prefix=>{const hits=lines.filter(l=>l.startsWith(prefix));if(hits.length!==1)throw Error('renderer.ts line not unique: '+prefix);return hits[0];};
 const constants=once(r,'const tv=(v:Vec)=>','const materials=new Map','module constants');
 const ability=once(r,' abilityShot(m:T.Mesh,',' /** Event VFX','abilityShot');
 const burst=once(r,' burst(pos:Vec,color:number,count=8,power=3){','\n','burst'),beam=once(r,' beam(a:Vec,b:Vec,color:number){','\n','beam');
 const loop=line(' const shotIds=new Set(state.projectiles.filter(p=>!p.melee&&!this.sirenEffects.owns(p))');
 const melee=once(r,'if(this.projectileTrails)for(const shot of state.projectiles)if(shot.melee)this.trails.sample(',';','melee trails')+';';
 const glow=once(r,'this.projectileGlow.update(state.projectiles.filter(p=>!this.sirenEffects.owns(p))',';','projectile glow')+';';
 const chips=once(r,'this.chipEffects.update(state,this.visualTime)',undefined,'chip effects')+';';
 const render=once(r,'if(this.projectileTrails)this.trails.render(this.camera,this.visualTime)',undefined,'trail render')+';';
 const step=once(r,'this.visualTime+=dt',undefined,'visual time')+';',update=once(r,'this.vfx.update(dt)',undefined,'vfx update')+';';
 return `import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {WEAPONS,statsFor} from '../shared/content.js';
import {ELEMENTS} from '../shared/elements.js';
import {ChipEffects} from './chip-effects.js';
import {EventVfx} from './event-vfx.js';
import {ProjectileTrails} from './projectile-trails.js';
import {ProjectileGlow} from './projectile-glow.js';
type Vec={x:number;y:number;z:number};type Snapshot=any;
${constants}
export {T,WEAPONS,statsFor,ELEMENTS,abilityGeometry};
/** renderer.ts's projectile path on its own: frame() runs the source statements in the renderer's update() order. */
export class ShotStage{
 scene=new T.Scene();shots=new Map<string,T.Mesh>();trails=new ProjectileTrails(this.scene);projectileTrails=true;visualTime=0;effectsQuality=1;compatibility=false;
 sirenEffects={owns:(p:any)=>!!p.siren};camera=new T.PerspectiveCamera(60,16/9,.08,400);
 vfx=new EventVfx(this.scene,()=>({quality:this.effectsQuality,compat:this.compatibility,trails:this.projectileTrails}));
 chipEffects=new ChipEffects(this.scene);projectileGlow=new ProjectileGlow(this.scene);
${burst}
${beam}
${ability}
 frame(state:Snapshot,dt:number){
  ${step}
  ${melee}
${loop}
  ${update}
  ${glow}${chips}${render}
 }
}
`;
}
// three's generateUUID draws four Math.random() values for every object it creates. The game's Math.random is unseeded, so
// that interleaving has no visual meaning; here it would desynchronise the seeded stream from the port's, so UUIDs get a
// stream of their own (a harness-only change to three, not to the game code).
export const uuidStream={name:'shot-uuid-stream',setup(build){build.onLoad({filter:/three[\\/]build[\\/]three\.core\.js$/},args=>{
 const code=fs.readFileSync(args.path,'utf8'),from='Math.random() * 0xffffffff | 0',hits=code.split(from).length-1;if(hits!==4)throw Error('three generateUUID changed: '+hits);
 return {contents:'let __uuid=0x2545f491;const __uuidRandom=()=>(__uuid=Math.imul(__uuid,1664525)+1013904223>>>0)/4294967296;\n'+code.split(from).join('__uuidRandom() * 0xffffffff | 0'),loader:'js'};});}};
export async function bundleStage(source,platform='node'){
 const esbuild=createRequire(path.join(source,'node_modules/'))('esbuild');
 const out=await esbuild.build({plugins:[uuidStream],stdin:{contents:stageSource(source),resolveDir:path.join(source,'src/client'),sourcefile:'shot-stage.ts',loader:'ts'},bundle:true,format:platform==='node'?'esm':'iife',globalName:platform==='node'?undefined:'SHOTSTAGE',platform,write:false,nodePaths:[path.join(source,'node_modules')],logLevel:'warning',target:'es2020'});
 return out.outputFiles[0].text;
}

// ---- Scripted projectiles (positions in source metres; the fixture stores every frame's inputs) ----
export const DT=1/60,FRAMES=48,SIM_START=2.5,VISUAL_START=7.1,SEED=0x5a17,CAPTURES=[1,3,6,10,20,32,48];
/** Frames that also record every model's world-space vertices and every trail's drawn ribbon. */
export const DETAIL=[1,20];
export const CAMERA={pos:[-2,2.5,-8.4],target:[0,1,0],fov:60};
const shot=(o)=>({kind:'bullet',mods:{},elements:[],gravity:0,pos:[-5,1.3,0],...o});
const along=(speed,dy=0,dz=.12)=>{const l=Math.hypot(1,dy,dz);return [speed/l,speed*dy/l,speed*dz/l];};
export function cases(W){
 const speed=s=>W[s].speed,g=s=>W[s].gravity||0,id=(()=>{let n=100;return()=>++n;})();
 const bullet=(spec,o={})=>shot({id:id(),spec,radius:(W[spec].shotRadius??(W[spec].class==='magic'?.14:.045)),vel:along(speed(spec),.05),gravity:g(spec),...o});
 const ability=(spec,ab,o={})=>shot({id:id(),kind:'ability',spec,ability:{id:ab},...o});
 return [
  {name:'pistol',shots:[bullet('pistol')]},
  {name:'rifle',shots:[bullet('rifle',{pos:[-6,1.4,.5]})]},
  {name:'machinegun',shots:[bullet('machinegun'),bullet('machinegun',{pos:[-5.4,1.25,-.2],vel:along(speed('machinegun'),.02,.3)})]},
  {name:'pistol-bowling-velocity-ember',shots:[bullet('pistol',{radius:.09,vel:along(speed('pistol')*1.5625*.45,.08),mods:{velocity:2,'bowling-ball':1,ember:1},elements:['fire']})]},
  {name:'rifle-ember-poison',shots:[bullet('rifle',{vel:along(40,.04),mods:{ember:1,poison:1},elements:['fire','poison']})]},
  {name:'fire',shots:[bullet('fire',{elements:['fire']})]},
  {name:'fire-poison',shots:[bullet('fire',{mods:{poison:2},elements:['fire','poison']})]},
  {name:'water',shots:[bullet('water')]},
  {name:'water-ember',shots:[bullet('water',{mods:{ember:1},elements:['fire']})]},
  {name:'lightning',shots:[bullet('lightning')]},
  {name:'darkness',shots:[bullet('darkness')]},
  {name:'leet',shots:[bullet('leet')]},
  {name:'hadouken-red',shots:[bullet('ansatsuken',{radius:.24,vel:along(23,0),gravity:0,arcade:{red:true},mods:{'arcade-red':1},elements:['fire']})]},
  {name:'hadouken-blue-super',shots:[bullet('ansatsuken',{radius:.5,vel:along(27,0),gravity:0,arcade:{red:false,super:true},mods:{poison:1},elements:['fire','poison']})]},
  {name:'sword-beam',shots:[bullet('sword',{radius:.4,vel:along(24,0),gravity:0,mods:{'sword-beam':1}})]},
  {name:'frag-grenade',shots:[ability('pistol','frag-grenade',{radius:.14,vel:[7,4.5,1.5],gravity:20,restAt:.35,ability:{id:'frag-grenade',fuse:SIM_START+.95}})]},
  {name:'rocket',shots:[ability('machinegun','rocket',{radius:.2,vel:along(38,.03),gravity:2})]},
  {name:'orbital-mark',shots:[ability('rifle','orbital-mark',{radius:.08,vel:along(150,.02),gravity:6})]},
  {name:'flame-lance',shots:[ability('fire','flame-lance',{radius:.14,vel:along(70,.02),gravity:4,elements:['fire']})]},
  {name:'tidal-wave',shots:[ability('water','tidal-wave',{radius:1,pos:[-5,.6,0],vel:along(24,0,.2)})]},
  {name:'ball-lightning',shots:[ability('lightning','ball-lightning',{radius:.45,vel:along(11,0)})]},
  {name:'shadow-shackles',shots:[ability('darkness','shadow-shackles',{radius:.3,vel:along(48,.03),gravity:3})]},
  {name:'ability-unknown',shots:[ability('pistol','no-such-ability',{radius:.25,vel:along(30,.05)})]},
  {name:'modules',shots:[bullet('pistol',{radius:.135,vel:along(20,.05),mods:{ricochet:3,homing:1,splitter:1,piercing:1,velocity:1,kinetic:1,shrapnel:1,boomerang:1,zigzag:1,vacuum:1,meteor:1,leech:1,ember:1,poison:1},elements:['fire','poison']})]},
  {name:'modules-pong',shots:[bullet('pistol',{vel:along(30,.05),mods:{pong:1,boomerang:1,zigzag:1,ricochet:1}})]},
  {name:'melee-dagger',shots:[shot({id:id(),kind:'melee',melee:{kind:'dagger',at:0},spec:'daggers',radius:.095,vel:along(30,.12),gravity:18,mods:{ember:1,homing:1},elements:['fire']})]},
  {name:'melee-shield',shots:[shot({id:id(),kind:'melee',melee:{kind:'shield',at:0},spec:'shield',radius:.28,vel:along(22,0)})]},
  {name:'siren',shots:[shot({id:id(),spec:'siren',radius:.3,vel:along(18,0),siren:{kind:'seeker',born:SIM_START}}),bullet('pistol',{pos:[-5,1.8,1]})]},
  {name:'volley',shots:[bullet('machinegun',{pos:[-1,1.2,-6]}),bullet('machinegun',{pos:[-3,1.2,4]}),bullet('lightning',{pos:[0,1.5,-2]}),bullet('pistol',{pos:[2,1.1,8]}),ability('machinegun','rocket',{radius:.2,pos:[1,1.4,-4],vel:along(38,.03),gravity:2}),bullet('ansatsuken',{radius:.24,pos:[-2,1,-7],vel:along(23,0),gravity:0,arcade:{red:false},elements:['fire']})]},
  {name:'reduced-effects',quality:.3,shots:[bullet('fire',{elements:['fire']}),ability('machinegun','rocket',{radius:.2,pos:[-5,1.8,1],vel:along(38,.03),gravity:2}),bullet('darkness',{pos:[-5,.9,-1]}),bullet('lightning',{pos:[-5,2.2,0]})]},
  {name:'effects-off',quality:0,shots:[bullet('fire',{elements:['fire']}),bullet('lightning',{pos:[-5,2.2,0]})]},
 ];
}
/** Snapshot projectiles for frame k (after k steps of the scripted kinematics). */
export function inputs(c){
 const frames=[];const live=c.shots.map(s=>({s,p:[...s.pos],v:[...s.vel],g:s.gravity||0}));
 for(let k=1;k<=FRAMES;k++){
  const t=k*DT;
  for(const q of live){if(q.s.restAt!==undefined&&t>q.s.restAt){q.v=[0,0,0];q.g=0;}q.p=[q.p[0]+q.v[0]*DT,q.p[1]+q.v[1]*DT,q.p[2]+q.v[2]*DT];q.v=[q.v[0],q.v[1]-q.g*DT,q.v[2]];}
  frames.push(live.map(q=>({id:q.s.id,pos:[...q.p],vel:[...q.v]})));
 }
 return frames;
}
export function snapshot(c,frame,k){
 return {time:SIM_START+k*DT,projectiles:c.shots.map((s,i)=>{const f=frame[i];return {id:String(s.id),owner:'p1',reflections:0,spec:s.spec,radius:s.radius,pos:{x:f.pos[0],y:f.pos[1],z:f.pos[2]},vel:{x:f.vel[0],y:f.vel[1],z:f.vel[2]},elements:[...s.elements],mods:s.kind==='ability'?{}:{...s.mods},...(s.ability?{ability:{...s.ability}}:{}),...(s.melee?{melee:{...s.melee}}:{}),...(s.arcade?{arcade:{...s.arcade}}:{}),...(s.siren?{siren:{...s.siren}}:{})};})};
}
let rng=1;export function reseed(seed){rng=(seed>>>0)||1;}
export function random(){rng=(rng+0x6d2b79f5)>>>0;let t=rng;t=Math.imul(t^(t>>>15),t|1);t^=t+Math.imul(t^(t>>>7),t|61);return ((t^(t>>>14))>>>0)/4294967296;}
export function newStage(S,c){
 const stage=new S.ShotStage();stage.effectsQuality=c.quality??1;stage.visualTime=VISUAL_START;
 stage.camera.position.set(...CAMERA.pos);stage.camera.lookAt(new S.T.Vector3(...CAMERA.target));stage.camera.updateMatrixWorld(true);
 return stage;
}

// ---- Capture (Node export only) ----
const r6=v=>Math.round(v*1e6)/1e6,r5=v=>Math.round(v*1e5)/1e5;
function capture(S,stage,k){
 const detail=DETAIL.includes(k);
 const T=S.T,m=new T.Matrix4(),v=new T.Vector3(),p=new T.Vector3(),q=new T.Quaternion(),s=new T.Vector3(),hex=c=>c.getHex();
 // Nothing renders here, so world matrices are brought up to date through the parents explicitly.
 const mat=o=>{o.updateWorldMatrix(true,false);return o.matrixWorld.elements.map(r6);};
 const models=[];for(const [id,mesh] of stage.shots){const pos=mesh.geometry.getAttribute('position'),w=[];mesh.updateMatrixWorld(true);if(detail)for(let i=0;i<pos.count;i++){v.fromBufferAttribute(pos,i).applyMatrix4(mesh.matrixWorld);w.push(r5(v.x),r5(v.y),r5(v.z));}models.push({id:+id,m:mat(mesh),c:[r6(mesh.material.color.r),r6(mesh.material.color.g),r6(mesh.material.color.b)],a:mesh.material.opacity,transparent:!!mesh.material.transparent,...(detail?{world:w}:{})});}
 const kind=g=>({TorusGeometry:'ring',ConeGeometry:'cone',SphereGeometry:'orb',BoxGeometry:'cube'})[g.type];
 const modules=[];for(const [id,group] of stage.chipEffects.shots){const parts=group.children.map(c=>({shape:kind(c.geometry),hex:hex(c.material.color),m:mat(c)}));if(parts.length)modules.push({id:+id,parts});}
 const sparks=stage.vfx.bursts.items.map(b=>[r6(b.position.x),r6(b.position.y),r6(b.position.z),r6(b.scale),r6(b.color.r),r6(b.color.g),r6(b.color.b)]);
 const beams=stage.vfx.particles.map(b=>({m:mat(b.mesh),c:[r6(b.mesh.material.color.r),r6(b.mesh.material.color.g),r6(b.mesh.material.color.b)],e:[r6(b.mesh.material.emissive.r*b.mesh.material.emissiveIntensity),r6(b.mesh.material.emissive.g*b.mesh.material.emissiveIntensity),r6(b.mesh.material.emissive.b*b.mesh.material.emissiveIntensity)],life:r6(b.life)}));
 const trails=[];for(const [id,t] of stage.trails.traces){const pos=t.mesh.geometry.getAttribute('position'),col=t.mesh.geometry.getAttribute('color'),n=detail?t.mesh.geometry.drawRange.count/6*4:0,quads=[],fades=[];for(let i=0;i<n;i++){quads.push(r5(pos.getX(i)),r5(pos.getY(i)),r5(pos.getZ(i)));fades.push(r6(col.getX(i)));}
  trails.push({id:+id,hex:hex(t.mesh.material.color),width:t.width,seen:r6(t.seen),points:t.points.map(x=>[r6(x.pos.x),r6(x.pos.y),r6(x.pos.z),r6(x.at)]),opacity:r6(t.mesh.material.opacity),...(detail?{quads,fades}:{})});}
 const halos=[];for(const [id,h] of stage.projectileGlow.halos)halos.push({id:+id,p:[r6(h.position.x),r6(h.position.y),r6(h.position.z)],size:r6(h.scale.x),hex:hex(h.material.color)});
 const lights=stage.projectileGlow.lights.filter(l=>l.intensity>0).map(l=>[r6(l.position.x),r6(l.position.y),r6(l.position.z),r6(l.color.r),r6(l.color.g),r6(l.color.b),r6(l.intensity),r6(l.distance)]);
 return {frame:k,models,modules,sparks,beams,trails,halos,lights};
}
function geometryTable(S){
 // Canonical shapes: the shot loop's primary meshes at radius 1, abilityGeometry() for every ability id (and an unknown
 // one), and the chip-effects.ts part geometries taken from a live module group. Positions as stored (float32), indices.
 const stage=newStage(S,{});const g=geo=>({positions:[...geo.getAttribute('position').array].map(x=>Math.fround(x)),indices:geo.index?[...geo.index.array]:null});
 const probe=[['box','pistol'],['fire-cone','fire'],['water-sphere','water'],['dark-ico','darkness'],['magic-ico','lightning'],['arcade-sphere','ansatsuken']];
 const state={time:0,projectiles:probe.map(([,spec],i)=>({id:'g'+i,owner:'p',reflections:0,spec,radius:1,pos:{x:0,y:0,z:0},vel:{x:0,y:0,z:1},elements:[],mods:{}}))};
 stage.frame(state,DT);const out={};probe.forEach(([name],i)=>{out[name]=g(stage.shots.get('g'+i).geometry);});
 for(const [name,id] of [['frag','frag-grenade'],['rocket','rocket'],['orbital-mark','orbital-mark'],['flame-lance','flame-lance'],['tidal-wave','tidal-wave'],['ball-lightning','ball-lightning'],['shackles','shadow-shackles'],['fallback','no-such-ability']])out[name]=g(S.abilityGeometry(id));
 const parts=newStage(S,{});parts.frame({time:0,projectiles:[{id:'m',owner:'p',reflections:0,spec:'pistol',radius:.05,pos:{x:0,y:0,z:0},vel:{x:0,y:0,z:1},elements:[],mods:{ricochet:1,homing:1,shrapnel:1,splitter:1}}]},DT);
 const kinds={TorusGeometry:'ring',ConeGeometry:'module-cone',SphereGeometry:'orb'};for(const c of parts.chipEffects.shots.get('m').children){const k=kinds[c.geometry.type];if(k&&!out[k])out[k]=g(c.geometry);}
 return out;
}
async function main(){
 const source=path.resolve(process.argv[2]??'');verify(source);
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'shot-stage-')),file=path.join(dir,'stage.mjs');fs.writeFileSync(file,await bundleStage(source));
 Math.random=random;const S=await import(pathToFileURL(file).href);fs.rmSync(dir,{recursive:true,force:true});
 const out={source:{commit:'05950eaef80ff2483a9c44d898cad98be1a76bbc',files:SOURCE_FILES},dt:DT,frames:FRAMES,simStart:SIM_START,visualStart:VISUAL_START,seed:SEED,camera:CAMERA,geometries:geometryTable(S),cases:[]};
 for(const c of cases(S.WEAPONS)){
  const stage=newStage(S,c),frames=inputs(c),captures=[];reseed(SEED);
  for(let k=1;k<=FRAMES;k++){stage.frame(snapshot(c,frames[k-1],k),DT);if(CAPTURES.includes(k))captures.push(capture(S,stage,k));}
  out.cases.push({name:c.name,quality:c.quality??1,shots:c.shots.map(s=>({id:s.id,kind:s.kind,spec:s.spec,radius:s.radius,mods:s.kind==='ability'?{}:s.mods,elements:s.elements,ability:s.ability??null,melee:s.melee?.kind??null,arcade:s.arcade?(s.arcade.red?'red':'blue'):null,siren:!!s.siren,pos:s.pos,vel:s.vel,gravity:s.gravity||0,restAt:s.restAt??null})),last:frames[FRAMES-1],captures});
 }
 fs.writeFileSync(path.join(root,'Tests/shot-parity.json'),JSON.stringify(out)+'\n');
 console.log('Exported',out.cases.length,'projectile runs,',out.cases.reduce((n,c)=>n+c.captures.length,0),'captured frames,',Object.keys(out.geometries).length,'geometries');
}
if(process.argv[1]&&pathToFileURL(path.resolve(process.argv[1])).href===import.meta.url)await main();
