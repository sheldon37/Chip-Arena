// Build-time only: run the pinned original client/siren/minions/hints.ts (hintLevel, possessHint) on real minions, and the
// trail branch of client/pve/horde-view.ts through gore.ts HordeGore.drip into flames.ts FlameField, and record what
// Tests/MinionHintChecks.cs replays through Core/NativeMinionHint.cs: hintLevel by possession age, the crown seam's and the
// halo's opacity and the halo's scale at each level (level 3's 1.35 boost), the grin glyph's visibility by camera distance
// (HINT_GLYPH_RANGE), its opacity and its lift over the head joint, the trail's gate read from horde-view.ts (level, walking
// speed, height over the floor, rate factor), its flame parameters, and how many flames a walk emits at several frame rates
// and effects qualities. Without a DOM the halo and grin textures are null, which hints.ts allows (the meshes still pose).
// The grin's canvas itself is drawn in headless Chromium by hints.ts glyph()'s own drawing code, read from the pinned text,
// and recorded as its straight-alpha RGBA (getImageData; raw deflate, base64), the texture Core/NativeMinionHint.cs Glyph bakes.
// Math.random is a seeded stream (three.js ids and the flame jitter draw from it; nothing recorded depends on it).
// Usage: node tools/export_minion_hint_fixtures.mjs <source checkout at 05950ea with node_modules> [chromium executable]
import fs from 'node:fs';import path from 'node:path';import zlib from 'node:zlib';import {createRequire} from 'node:module';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),chromium=process.argv[3];
const pinned={'src/client/siren/minions/hints.ts':'0c0b40e469f3486a00c61563be4c1d3467cd6bad','src/client/pve/horde-view.ts':'a67465897a2daae38c145ec4a8f3bf0c45ca6b93',
 'src/client/pve/gore.ts':'06e985e9900f740b37b17cbc30993368723f481e','src/client/siren/flames.ts':'66dc22da4a01a996e1cadcf501d3f8b399e0ba77',
 'src/client/siren/minions/model.ts':'9cf152e7038676aa29163959c77f5efba74ec54b','src/client/siren/minions/motion.ts':'5fb6ec6c8d07ba895c586bff39dcd93b82c3efd7'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
let stream=9137;Math.random=()=>{stream=(Math.imul(stream,1664525)+1013904223)>>>0;return stream/4294967296;};
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src/client',f)).href);
const hints=await load('siren/minions/hints.ts'),model=await load('siren/minions/model.ts'),motion=await load('siren/minions/motion.ts'),gore=await load('pve/gore.ts');
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const p9=v=>Number((+v).toPrecision(9));
const out={source:pinned,commit:'05950eaef80ff2483a9c44d898cad98be1a76bbc',range:hints.HINT_GLYPH_RANGE,levels:[...hints.HINT_LEVELS],ages:[],frames:[],trail:{}};
for(const age of [-5,-.01,0,.5,7.3,14.999,15,15.001,22,34.999,35,35.001,60,600])out.ages.push({age,level:hints.hintLevel(age)});
// possessHint on a blank-faced and a cracked minion, posed at each time (the glyph rides the head joint, in the rig group).
const minions=[];for(let s=100;s<106&&minions.length<2;s++){const m=model.createSirenMinion(s);if(!minions.some(x=>x.look.face===m.look.face))minions.push(m);}
if(minions.length<2)throw Error('need a blank and a cracked minion');
const head=new T.Vector3();
for(const m of minions)for(const level of [1,2,3,0])for(const t of [40.05,41.3,42.75,44.1,47.9])for(const dist of [6,24.99,25,25.01,38]){
 motion.animateSirenMinion(m,{yaw:.4,vel:{x:0,z:1.1},hp:1,maxHp:1},t,1);m.rig.group.updateMatrixWorld(true);hints.possessHint(m,level,t,dist);
 const [seam,halo,glyph]=hints.hintObjects(m);if(!seam)continue;m.rig.head.getWorldPosition(head);m.rig.group.worldToLocal(head);
 out.frames.push({face:m.look.face,phase:p9(m.look.phase),level,t,dist,seam:{visible:seam.visible,opacity:p9(seam.material.opacity)},halo:{visible:halo.visible,opacity:p9(halo.material.opacity),scale:p9(halo.scale.x)},
  glyph:{visible:glyph.visible,opacity:p9(glyph.material.opacity),pos:glyph.position.toArray().map(p9),scale:[p9(glyph.scale.x),p9(glyph.scale.y)]},head:head.toArray().map(p9)});}
// horde-view.ts showRig's trail branch, read from the pinned text, and the frame dt clamp of HordeView.update.
const view=fs.readFileSync(path.join(source,'src/client/pve/horde-view.ts'),'utf8');
const gate=view.match(/if\(level>=(\d+)&&dt>0&&Math\.hypot\(v\.vel\.x,v\.vel\.z\)>([\d.]+)\)this\.gore\.drip\(\{x:i\.pos\.x,y:i\.ground\+([\d.]+),z:i\.pos\.z\},dt\*(\d+),ctx\.quality,rig\.trail\);/);
const clamp=view.match(/dt=Math\.max\(0,Math\.min\(([\d.]+),ctx\.dt\|\|0\)\)/);if(!gate||!clamp)throw Error('horde-view.ts trail branch not found');
out.trail={level:+gate[1],speed:+gate[2],height:+gate[3],factor:+gate[4],dtClamp:+clamp[1],dripRate:gore.GORE.dripRate,runs:[]};
// A walk of `seconds` at `fps` (the drip accumulator starts at 0 as a new rig's trail does): flames emitted and the first one's parameters.
for(const quality of [1,.3])for(const fps of [30,60,144])for(const seconds of [1,3]){
 const g=new gore.HordeGore(new T.Scene()),calls=[],emit=g.flames.emit.bind(g.flames);g.flames.emit=(...a)=>{calls.push(a.slice(0,11));return emit(...a);};
 const acc={v:0},dt=Math.max(0,Math.min(out.trail.dtClamp,1/fps)),frames=Math.round(seconds*fps);
 for(let k=0;k<frames;k++)g.drip({x:1.5,y:.85+out.trail.height,z:-2},dt*out.trail.factor,quality,acc);
 const a=calls[0];out.trail.runs.push({quality,fps,seconds,frames,flames:calls.length,first:a?{pos:a.slice(0,3).map(p9),vel:a.slice(3,6).map(p9),life:a[6],size:a[7],tone:a[8],buoyancy:a[9],drag:a[10]}:null});
 g.dispose();}
// hints.ts glyph(): its canvas size, paths and drawing statements (between getContext and the CanvasTexture), run in Chromium.
const hintsText=fs.readFileSync(path.join(source,'src/client/siren/minions/hints.ts'),'utf8');
const glyphPaths=hintsText.match(/const GRIN_PATH='([^']+)',TEETH_PATH='([^']+)';/),glyphDraw=hintsText.match(/function glyph\(\)\{[^\n]*const c=canvas\((\d+),(\d+)\);[^\n]*const g=c\.getContext\('2d'\)!;\n([\s\S]*?)\n glyphTex=new T\.CanvasTexture\(c\)/);
if(!glyphPaths||!glyphDraw)throw Error('hints.ts glyph() not found');
const req=createRequire(path.join(source,'node_modules/'));let pw;try{pw=req('playwright');}catch{pw=createRequire('/opt/node-tools/node_modules/')('playwright');}
const browser=await pw.chromium.launch(chromium?{executablePath:chromium}:{});const page=await browser.newPage();
const canvas=await page.evaluate(({w,h,code,grin,teeth})=>{
 const c=Object.assign(document.createElement('canvas'),{width:w,height:h}),g=c.getContext('2d');new Function('g','GRIN_PATH','TEETH_PATH',code)(g,grin,teeth);
 const d=g.getImageData(0,0,w,h).data;let s='';for(let i=0;i<d.length;i+=8192)s+=String.fromCharCode(...d.subarray(i,i+8192));return {w,h,rgba:btoa(s)};
},{w:+glyphDraw[1],h:+glyphDraw[2],code:glyphDraw[3],grin:glyphPaths[1],teeth:glyphPaths[2]});
await browser.close();out.grin={w:canvas.w,h:canvas.h,deflate:zlib.deflateRawSync(Buffer.from(canvas.rgba,'base64'),{level:9}).toString('base64')};
const file=path.join(root,'Tests/minion-hint-parity.json');fs.writeFileSync(file,JSON.stringify(out)+'\n');
console.log('wrote',path.relative(root,file),out.ages.length,'ages',out.frames.length,'hint frames',out.trail.runs.length,'trail runs','grin canvas',out.grin.w+'x'+out.grin.h);
