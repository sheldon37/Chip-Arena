// Build-time only: run the pinned original client/siren/intro.ts (SirenIntroScene, the dev-siren entrance: a bare cell lit by
// a bulb that buzzes on, stutters on the off-beats of black-siren-protocol, flashes the Siren into view, steadies on it and
// dies) and record its lights frame by frame for Tests/SirenIntroChecks.cs to replay through Core/NativeSirenIntroLight.cs:
// the bulb, fill and ambient intensities, the bulb's sway, the bulb glow and halo, the light colours, ranges and decays,
// the cue beats (shared/siren-contract.ts SIREN_INTRO) and the track's beats (shared/boss-music.ts), which drive both the
// stutter and the music. Usage: node tools/export_siren_intro_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/siren/intro.ts':'759a0d9b2dea4f31775ea2d572dd4dede7fd08cc','src/shared/siren-contract.ts':'ad494dfd203d8f12cdfc69b2c73004807af28ccc',
 'src/shared/boss-music.ts':'12a9c174c4401509a02eb253fa4ef6c7a07634b6','src/client/audio.ts':'dd56c11afd6e4a85474744f832d0c4d40500ac4f'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const intro=await import(pathToFileURL(path.join(source,'src/client/siren/intro.ts')).href),rigs=await import(pathToFileURL(path.join(source,'src/client/character-rig.ts')).href);
const contract=await import(pathToFileURL(path.join(source,'src/shared/siren-contract.ts')).href),music=await import(pathToFileURL(path.join(source,'src/shared/boss-music.ts')).href);
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const p9=v=>Number(v.toPrecision(9));
const scene=new T.Scene(),cell=new intro.SirenIntroScene(scene),rig=rigs.createCharacterRig('#70e1d0','#e5e7eb',true),camera=new T.PerspectiveCamera(40,16/9,.1,80);scene.add(rig.group);
// The scene's private lights and glow (TypeScript privacy only).
const {bulb,fill,ambient,bulbMat,bulbHalo,bulbMesh,fog}=cell;
const SI=contract.SIREN_INTRO,track=music.BLACK_SIREN_PROTOCOL,cue=name=>SI.cues.find(c=>c.cue===name).time;
// Ages: 120 Hz over the whole entrance, plus both sides of every beat, stutter window edge, cue and flash edge.
const ages=new Set();for(let i=0;i<=Math.ceil(SI.duration*120)+12;i++)ages.add(i/120);
const edge=t=>{for(const d of [-1e-4,0,1e-4])if(t+d>=0)ages.add(t+d);};
const beats=track.beats.slice(0,18);
for(let b=0;b<beats.length-1;b++){const span=beats[b+1]-beats[b];edge(beats[b]);for(const ph of [.15,.24,.42,.62])edge(beats[b]+ph*span);}
for(const c of SI.cues)edge(c.time);for(const t of [cue('bulb')+.08,cue('glimpse')+.11,cue('glimpse')+.32,cue('reveal')+.05])edge(t);
const frames=[];
for(const age of [...ages].sort((a,b)=>a-b)){
 cell.update(age,rig,camera,1);
 frames.push({age,bulb:p9(bulb.intensity),fill:p9(fill.intensity),ambient:p9(ambient.intensity),glow:p9(bulbMat.uniforms.uIntensity.value),halo:p9(bulbHalo.material.opacity),
  bulbPos:[bulb.position.x,bulb.position.y,bulb.position.z].map(p9),meshX:p9(bulbMesh.position.x),haloX:p9(bulbHalo.position.x),fillPos:[fill.position.x,fill.position.y,fill.position.z].map(p9)});
}
const lin=c=>[c.r,c.g,c.b].map(p9);
const out={source:pinned,
 lights:{bulb:{color:lin(bulb.color),distance:bulb.distance,decay:bulb.decay},fill:{color:lin(fill.color),distance:fill.distance,decay:fill.decay},ambient:{color:lin(ambient.color)},
  glow:{color:lin(bulbMat.uniforms.uColor.value),position:[bulbMesh.position.x,bulbMesh.position.y,bulbMesh.position.z].map(p9)},halo:{color:lin(bulbHalo.material.color),scale:p9(bulbHalo.scale.x),additive:bulbHalo.material.blending===T.AdditiveBlending},
  fog:{color:fog.color.getHex(),near:fog.near,far:fog.far}},
 intro:{duration:SI.duration,nameAt:SI.nameAt,cues:SI.cues.map(c=>({cue:c.cue,beat:c.beat,time:c.time}))},
 track:{id:track.id,file:track.file,introEnd:track.introEnd,beats:beats},
 frames};
fs.writeFileSync(path.join(root,'Tests/siren-intro-parity.json'),JSON.stringify(out)+'\n');
console.log('siren intro fixture:',frames.length,'frames');
process.exit(0);
