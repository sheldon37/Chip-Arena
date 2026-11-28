// Build-time only: run the pinned original client/siren/stage.ts WardZeroVisuals (Ward Zero's floor vents: the rim under each
// grate, the warning wisps, the eruption burst and black flame jets, the open-vent rim flicker, the lid rattle and the smoke
// spilling out while a lid swings) and client/siren/fx.ts (the Marked seeker's point light) with Math.random replaced by a
// seeded mulberry32 stream, and record what Tests/StageVentChecks.cs replays through Core/NativeStageVentFx.cs,
// NativeSeekerLight.cs, NativeVentRules.cs and NativeBossTrack.Vent. three.js object UUIDs keep their own random stream.
// The camera stands south of the hall (z > halfZ) so the south wall is cut away: the great doors' dust (not part of the vents)
// stays out of the shared random stream, while the vault (and with it the entry smoke) stays drawn.
// Usage: node tools/export_stage_vent_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/siren/stage.ts':'e970f383c445cd4e18d8be95344a820136ff128f','src/client/siren/flames.ts':'66dc22da4a01a996e1cadcf501d3f8b399e0ba77',
 'src/client/siren/fx.ts':'e4ffbcb23188e7a9fb733c75314f0a2229429e6d','src/client/siren/materials.ts':'37a6f92a012f3e4b6542f0008be352fd41a80de8',
 'src/shared/pve/vent-pits.ts':'e42405839f3797da723f60cee594b5c8105d18d5','src/shared/siren-stage-layout.ts':'51c17ff55199771c09767ca056585fffc5977919',
 'src/shared/boss-music.ts':'12a9c174c4401509a02eb253fa4ef6c7a07634b6'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src',f)).href);
const stage=await load('client/siren/stage.ts'),fxm=await load('client/siren/fx.ts'),layout=await load('shared/siren-stage-layout.ts'),pits=await load('shared/pve/vent-pits.ts'),music=await load('shared/boss-music.ts');
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const real=Math.random;let rng=null;
const mulberry=a=>()=>{a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};
Math.random=function(){const caller=(new Error().stack||'').split('\n')[2]||'';return caller.includes('/node_modules/three/')||!rng?real():rng();};
const seed=s=>{rng=mulberry(s);};
const p9=v=>Number((+v).toPrecision(9));
/** A FlameField's burning slots: [slot, pos xyz, vel xyz, rot xyz, spin xyz, age, life, size, buoyancy, drag, c0 rgb, c1 rgb]. */
const state=f=>{const slots=[];for(let i=0;i<f.n;i++){if(f.age[i]>=f.life[i])continue;const k=i*3,t3=a=>[a[k],a[k+1],a[k+2]];
 slots.push([i,...[...t3(f.pos),...t3(f.vel),...t3(f.rot),...t3(f.spin),f.age[i],f.life[i],f.size[i],f.buoy[i],f.drag[i],...t3(f.c0),...t3(f.c1)].map(p9)]);}
 return {cursor:f.cursor,live:f.live,n:f.n,slots};};
const out={source:pinned,commit:'05950eaef80ff2483a9c44d898cad98be1a76bbc',seedNote:'mulberry32; each run reseeds right before its first frame'};
const camera=new T.PerspectiveCamera();camera.position.set(0,5,40);

// ---- 1. The rim and the hall's vents -------------------------------------------------------------------------------
{rng=null;const ward=new stage.WardZeroVisuals(new T.Scene());ward.setActive(true);ward.group.updateMatrixWorld(true);
 const v=ward.vents[0],rim=v.grate.children.find(o=>o.isMesh&&o.geometry.type==='RingGeometry'),g=rim.geometry,pos=g.getAttribute('position'),local=[];
 for(let i=0;i<pos.count;i++){const p=new T.Vector3(pos.getX(i),pos.getY(i),pos.getZ(i)).applyMatrix4(rim.matrixWorld);local.push([p.x-v.grate.position.x,p.y,p.z-v.grate.position.z].map(p9));}
 const m=rim.material;
 out.rim={parameters:g.parameters,vertices:local,indices:[...g.index.array],color:m.uniforms.uColor.value.getHex(),intensity:m.uniforms.uIntensity.value,flicker:m.uniforms.uFlicker.value,opacity:m.uniforms.uOpacity.value,
  transparent:m.transparent,depthWrite:m.depthWrite,doubleSide:m.side===T.DoubleSide,normalBlending:m.blending===T.NormalBlending,fog:m.fog,toneMapped:m.toneMapped,renderOrder:rim.renderOrder,pitRenderOrder:v.pit.renderOrder,
  vents:ward.vents.map(x=>[x.grate.position.x,x.grate.position.y,x.grate.position.z]),layout:layout.SIREN_VENTS.map(x=>({x:x.x,z:x.z,group:x.group})),
  swing:stage.VENT_SWING,hinge:stage.VENT_HINGE,entryClose:pits.ENTRY_CLOSE.vent,warn:pits.VENT_PIT.warn,lead:pits.VENT_PIT.lead,capacity:ward.fire.n};}

// ---- 2. Scripted runs of WardZeroVisuals.update ----------------------------------------------------------------------
const ids=layout.SIREN_VENTS.map(v=>v.id);
function run(name,{seedValue,quality,start,frames,snapshot,checkpoints,entries=[]}){
 rng=null;const ward=new stage.WardZeroVisuals(new T.Scene());ward.setActive(true);seed(seedValue);
 const rows=[],marks=[];let time=start;
 for(let k=0;k<frames.length;k++){
  time+=frames[k];const s=snapshot(time);ward.update(s,time,quality,camera);
  const song=s.bossSong,raw=song?s.time-song.startedAt:-1,hazards=song&&raw>=0?layout.sirenStageHazards(raw):[];
  const view=s.pve&&s.pve.entries.length?s.pve.entries:null,list=(view?undefined:s.vents)??view??[];
  // One row per frame: time, the fire's cursor and live count, then per vent (0-7) the song hazard's warning, the drawn opening,
  // the Ward Zero warning, the open-vent glow, the rim opacity and the lid angle; bitmasks: hazard active, entry sent, rim
  // white (else warn red), pit visible.
  const bits=f=>ids.reduce((m,_,i)=>m|(f(i)?1<<i:0),0),hz=i=>hazards.find(x=>x.id===ids[i]);
  rows.push([p9(time),ward.fire.cursor,ward.fire.live,...ids.map((_,i)=>p9(hz(i)?.warning??0)),...ward.vents.map(v=>p9(v.shaft.uniforms.uOpen.value)),
   ...ids.map(id=>p9(ward.ventWarn.get(id)??0)),...ids.map(id=>p9(ward.ventGlow.get(id)??0)),...ward.vents.map(v=>p9(v.glow.uniforms.uOpacity.value)),...ward.vents.map(v=>p9(v.lid.rotation.z)),
   bits(i=>!!hz(i)?.active),bits(i=>list.some(y=>y.e===i)),bits(i=>{const h=ward.vents[i].glow.uniforms.uColor.value.getHex();if(h!==0xffffff&&h!==0xff2f44)throw Error('rim colour '+h);return h===0xffffff;}),bits(i=>ward.vents[i].pit.visible)]);
  if(checkpoints.includes(k))marks.push({frame:k,state:state(ward.fire)});
 }
 out.runs[name]={seed:seedValue,quality,start,dts:frames,ward:!snapshot(start).pve,pve:!!snapshot(start).pve,song:!!snapshot(start).bossSong,entries,frames:rows,checkpoints:marks};
}
out.runs={};
// Mostly 60 Hz, with a long frame (clamped to .05 s) and a short one.
const steps=(n,odd)=>Array.from({length:n},(_,k)=>odd&&k%97===41?.08:odd&&k%61===17?.004:1/60);
// The lift section starts at bar 45 (song time 74.29 s): warnings from bar 44 beat 3, eruptions on the downbeats.
run('song',{seedValue:501,quality:1,start:73.3,frames:steps(330,true),snapshot:t=>({time:t,bossSong:{startedAt:0}}),checkpoints:[80,200]});
run('reduced',{seedValue:502,quality:.3,start:75.1,frames:steps(150,false),snapshot:t=>({time:t,bossSong:{startedAt:0}}),checkpoints:[149]});
// Ward Zero outside PvE: two vents' openings (mode 'dive', the Ward Zero cycle's shape), warning, swing, open, close.
const wardVents=[{e:0,openAt:12,closeAt:16.5,mode:'dive'},{e:3,openAt:13.5,closeAt:18,mode:'dive'}];
run('ward',{seedValue:503,quality:1,start:10.2,frames:steps(560,true),snapshot:t=>({time:t,vents:pits.wardVentsSent(wardVents,t)}),checkpoints:[120,559],entries:wardVents});
// PvE: a climbing add's vent (spawn), a diving add's vent (dive), during the song (an eruption over an open vent).
const pveEntries=[{e:1,openAt:76,closeAt:78.2,mode:'spawn'},{e:6,openAt:75.4,closeAt:76.9,mode:'dive'}];
run('pve',{seedValue:504,quality:1,start:74.9,frames:steps(260,false),snapshot:t=>({time:t,bossSong:{startedAt:0},pve:{entries:pveEntries}}),checkpoints:[130,259],entries:pveEntries});

// ---- 3. The seeker's point light -----------------------------------------------------------------------------------
{rng=null;const scene=new T.Scene(),fx=new fxm.SirenFx(scene);seed(505);const shot=(id,x)=>({id,pos:{x,y:1.2,z:3},vel:{x:0,y:0,z:2.8},siren:{kind:'seeker',born:60}});
 const rows=[];for(let k=0;k<=24;k++){const t=60+k/30,list=k<16?[shot('a',0),shot('b',2)]:[shot('b',2)];fx.update({players:[],projectiles:list},t,new Map(),k%3===0?.3:1);
  const lights=[];scene.traverse(o=>{if(o.isPointLight)lights.push(o);});
  rows.push({time:p9(t),lights:lights.map(l=>{const w=new T.Vector3();l.getWorldPosition(w);return {intensity:p9(l.intensity),distance:l.distance,decay:l.decay,color:l.color.getHex(),pos:[w.x,w.y,w.z].map(p9)};}),shots:list.map(s=>[s.pos.x,s.pos.y,s.pos.z])});}
 const made=new fxm.SirenFx(new T.Scene());const v=made.makeShot({kind:'seeker',born:0});out.seeker={created:v.light.intensity,distance:v.light.distance,decay:v.light.decay,color:v.light.color.getHex(),
  halo:v.halo.material.color.getHex(),haloOpacity:v.halo.material.opacity,red:v.mesh.children.filter(o=>o.isSprite&&o!==v.halo).map(o=>({color:o.material.color.getHex(),opacity:o.material.opacity,scale:o.scale.x})),frames:rows};}

fs.writeFileSync(path.join(root,'Tests/stage-vent-parity.json'),JSON.stringify(out)+'\n');
console.log('wrote Tests/stage-vent-parity.json',Object.keys(out.runs).map(k=>k+':'+out.runs[k].frames.length).join(' '));
