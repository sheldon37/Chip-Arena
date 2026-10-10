// Build-time only: run the pinned original client/chip-blasts.ts and record what its two voxel pools and pooled lights
// draw, frame by frame, for Tests/BlastChecks.cs, with the owner's lightning cut below. Usage: node tools/export_blast_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const file='src/client/chip-blasts.ts',blob='cfaf2150bc0493876bb0e93f614719e4be1e3aab',bytes=fs.readFileSync(path.join(source,file));
if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);
const threeUrl=pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href,T=await import(threeUrl);
// Owner direction 2026-10-10 ("Remove that blue/cyan lightning that shows when an area of effect is happening"), applied to
// the pinned source exactly as Core/NativeBlastSim.cs applies it, so the fixtures pin the directed look: a shock blast
// draws its sky bolt and ground arcs only when its hot colour is not cyan-to-blue (NativeBlastSim.BlueCyan/Lightning), and
// the storm field draws no strikes. Each cut must match the pinned text exactly once.
const OWNER_CUTS=[
 ["const top=ground+9,steps=n(22);let x=center.x,z=center.z;for(","if(__ownerLightning(pal)){const top=ground+9,steps=n(22);let x=center.x,z=center.z;for("],
 ["pal.cool,0,2,i*.004);}","pal.cool,0,2,i*.004);}}"],
 ["for(let k=0;k<n(5);k++){","if(__ownerLightning(pal))for(let k=0;k<n(5);k++){"],
 ["if(kind==='storm'){const s=spot();let x=s.x,z=s.z;const top=ground+3.6;for(let i=0;i<12;i++){const y=top-(top-ground)*(i/11);this.glow.spawn(x,y,z,0,0,0,.2,.24,.12,0xffffff,pal.hot);x+=(rnd()-.5)*.4;z+=(rnd()-.5)*.4;}this.glow.spawn(s.x,ground+.15,s.z,0,0,0,.18,.6,.2,0xffffff,pal.hot);for(let i=0;i<5;i++)this.glow.spawn(s.x,ground+.1,s.z,(rnd()-.5)*5,2+rnd()*2,(rnd()-.5)*5,.3,.08,.03,0xffffff,pal.cool,9);","if(kind==='storm'){"]];
let directed=bytes.toString('utf8');
for(const [from,to] of OWNER_CUTS){if(directed.split(from).length!==2)throw Error('Owner cut does not match the pinned source exactly once: '+from.slice(0,60));directed=directed.replace(from,()=>to);}
directed+=`\nfunction __ownerLightning(pal){const h=pal.hot,r=(h>>16)&255,g=(h>>8)&255,b=h&255,max=Math.max(r,g,b),d=max-Math.min(r,g,b);if(d<24)return true;const hue=max===r?60*(((g-b)/d+6)%6):max===g?60*((b-r)/d+2):60*((r-g)/d+4);return !(hue>=165&&hue<=250);}\n`;
const code=stripTypeScriptTypes(directed,{mode:'transform'}).replace(/from\s*'three'/g,`from '${threeUrl}'`);
const {ChipBlasts,blastStyle,blastTint,BLAST_FRONT_SECONDS,BLAST_CAPACITY,BLAST_MIN_VOXELS}=await import('data:text/javascript,'+encodeURIComponent(code));
const DT=1/60,ORIGIN={x:0,y:.95,z:0},m=new T.Matrix4(),p=new T.Vector3(),q=new T.Quaternion(),s=new T.Vector3(),c=new T.Color();
const r=v=>Number(v.toPrecision(7)),r4=v=>Math.round(v*1e5)/1e5;
// Per frame: voxel counts, index-weighted sums of position/size/colour over both pools (solid first, as drawn), the first
// drawn voxel in full, and every lit pooled light.
function capture(b,t){
 const sums=Array(9).fill(0);let n=0,first=null;
 for(const pool of [b.solid,b.glow])for(let i=0;i<pool.mesh.count;i++){
  pool.mesh.getMatrixAt(i,m);m.decompose(p,q,s);pool.mesh.getColorAt(i,c);const v=[p.x,p.y,p.z,s.x,s.y,s.z,c.r,c.g,c.b];n++;
  for(let k=0;k<9;k++)sums[k]+=v[k]*n;if(!first)first=[p.x,p.y,p.z,s.x,s.y,s.z,q.x,q.y,q.z,q.w,c.r,c.g,c.b].map(r4);}
 const lights=b.flashes.filter(f=>f.light.intensity>0).map(f=>[f.light.position.x,f.light.position.y,f.light.position.z,f.light.color.r,f.light.color.g,f.light.color.b,f.light.intensity,f.light.distance].map(r4));
 return {t:r4(t),solid:b.solid.mesh.count,glow:b.glow.mesh.count,sums:sums.map(r),first,lights};
}
const cases=[];
function run(c,start,times,field){
 const b=new ChipBlasts(new T.Scene());start(b);const frames=[];let frame=0;
 for(const t of times){const target=Math.round(t/DT);while(frame<target){const time=frame*DT;if(field&&time<field.until)b.field(field,time,c.quality,c.compat);b.update(DT);frame++;}frames.push(capture(b,frame*DT));}
 cases.push({...c,frames});
}
const blastTimes=[.05,.15,.3,.6,1,1.6],fieldTimes=[.3,1,2,2.4],styles=['fire','shock','implosion','frost','toxic','void','nova','kinetic','quake','meteor','mine'];
for(const style of styles)for(const radius of [1.2,3,6])run({kind:'blast',effect:style,radius,quality:1,compat:false},b=>b.blast(style,ORIGIN,radius,{quality:1}),blastTimes);
for(const style of styles)for(const [quality,compat] of [[.3,false],[1,true]])run({kind:'blast',effect:style,radius:3,quality,compat},b=>b.blast(style,ORIGIN,3,{quality,compat}),blastTimes);
// Tagged explosions exactly as event-vfx.ts blastEvent maps an 'explosion' message.
for(const tag of ['frag','tidal-wave','geyser-wall','orbital-mark','blink-strike','ball-lightning','flame-lance'])
 run({kind:'blast',effect:blastStyle(tag),tag,radius:3,quality:tag==='flame-lance'?.5:1,compat:false},b=>b.blast(blastStyle(tag),ORIGIN,3,{quality:tag==='flame-lance'?.5:1,tint:blastTint(tag)}),blastTimes);
for(const [radius,hot,ground] of [[.6,0x8de9ee,false],[1.5,0xffaa64,false],[1.2,0x93db67,true],[.9,0xb66cff,false]])
 run({kind:'impact',effect:'impact',hot,ground,radius,quality:1,compat:false},b=>b.impact(ORIGIN,radius,hot,{quality:1,ground}),blastTimes);
const fields=[['black-hole'],['acid'],['fire'],['frost'],['time'],['repulsion'],['heal'],['storm'],['bomb'],['implosion'],['mine'],['bomb','artillery'],['bomb','meteor-strike'],['bomb','orbital-mark'],['bomb','geyser-wall'],['bomb','thunderstep'],['bomb','comet'],['black-hole','singularity'],['mine','proximity-mail'],['bomb','bubble-mine']];
for(const [kind,fx] of fields){const field={id:'fixture',owner:'fixture',kind,pos:{...ORIGIN},radius:3,born:0,until:2.5,arm:0,damage:0,fx,lockAt:1.9};run({kind:'field',effect:kind,fx:fx??null,radius:3,quality:1,compat:false,until:2.5,lockAt:1.9},()=>{},fieldTimes,field);}
run({kind:'line',effect:'line',radius:0,quality:1,compat:false},b=>b.line({x:0,y:.95,z:0},{x:4,y:1.5,z:2},0xffffff,0xb66cff,1,.3,.22,.08),[.05,.1,.2]);
run({kind:'paddle',effect:'paddle',radius:0,quality:1,compat:false},b=>b.paddle({x:0,y:1,z:0},{x:1,y:.2,z:.5},0xff5269,1),[.05,.1]);
const out={source:{file,blob},owner:{lightning:'2026-10-10',cuts:OWNER_CUTS.length},constants:{frontSeconds:BLAST_FRONT_SECONDS,capacity:BLAST_CAPACITY,minVoxels:BLAST_MIN_VOXELS},dt:DT,origin:[ORIGIN.x,ORIGIN.y,ORIGIN.z],cases};
fs.writeFileSync(path.join(root,'Tests/blast-parity.json'),JSON.stringify(out)+'\n');console.log('Exported',cases.length,'chip blast runs,',cases.reduce((n,c)=>n+c.frames.length,0),'frames');
