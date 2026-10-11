// Build-time only: run the pinned original client/chip-blasts.ts and record what its two voxel pools and pooled lights
// draw, frame by frame, for Tests/BlastChecks.cs, with the owner's cuts below (lightning, area disc, effects overhaul). Usage: node tools/export_blast_fixtures.mjs <source checkout at 05950ea with node_modules>
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
// Owner direction 2026-10-11 ("Can we remove that 'area indicator' for impact effects? If it hits a wall or something, it
// kind of appears like a floating pancake"): no blast or impact slides its flat disc of voxels over the floor. Blasts give
// the disc's voxels to the shell (a Quake or Frost blast gets a shell of the disc's size; Nova adds them to its sphere front;
// Kinetic's grey solid disc becomes extra debris), a ground impact gives them to its dome, exactly as NativeBlastSim does.
// Every run below fires at (0, .95, 0), whose floor is the source's assumed y - .95 = 0: the ground case. The real-floor
// parameter and the airburst sphere (NativeBlastSim.NearGround) have no source counterpart; Tests/BlastChecks.cs covers them.
const DISC_CUTS=[
 ["this.shell(pos,ground,R,pal.hot,pal.cool,m(34));this.ring(pos,ground,R,pal.hot,pal.cool,m(36),true);","this.shell(pos,ground,R,pal.hot,pal.cool,m(34)+m(36));"],
 ["this.shell(pos,ground,R,pal.hot,pal.cool,m(40));this.ring(pos,ground,R,pal.core,pal.cool,m(40),true,.06,.2);this.ring(pos,ground,R*.98,0xd9e6ee,0x6f7d88,m(26),false,.12,.22);debris(10,R*2.2);","this.shell(pos,ground,R,pal.hot,pal.cool,m(40)+m(40));debris(10,R*2.2,pal.debris,m(26));"],
 ["const debris=(count:number,power:number,color=pal.debris)=>{for(let i=0,c=n(count);","const debris=(count:number,power:number,color=pal.debris,extra=0)=>{for(let i=0,c=n(count)+extra;"],
 ["this.ring(pos,ground,R,pal.hot,pal.cool,m(30),true,.05,.18);","this.shell(pos,ground,R,pal.hot,pal.cool,m(30));"],
 ["this.shell(pos,ground,R,pal.hot,pal.cool,m(36));this.ring(pos,ground,R,pal.core,pal.cool,m(36),true,.06,.18);","this.shell(pos,ground,R,pal.hot,pal.cool,m(36)+m(36));"],
 ["for(let i=0,c=m(56);i<c;i++)","for(let i=0,c=m(56)+m(34);i<c;i++)"],
 ["this.ring(pos,ground,R,pal.hot,pal.cool,m(34),true);sparks(20,R*3.4);","sparks(20,R*3.4);"],
 ["this.ring(pos,ground,R,pal.core,pal.cool,m(32),true);","this.shell(pos,ground,R,pal.core,pal.cool,m(32));"],
 ["this.shell(pos,ground,R,pal.hot,pal.cool,m(28));this.ring(pos,ground,R,pal.hot,pal.cool,m(30),true);","this.shell(pos,ground,R,pal.hot,pal.cool,m(28)+m(30));"],
 ["  if(opts.ground){const r0=Math.min(.25,R*.2);for(let i=0,c=n(26);i<c;i++){const a=i/c*Math.PI*2+rnd()*.6,x=Math.cos(a),z=Math.sin(a),s=(r0+(R*.92-r0)*Math.sqrt(rnd())-r0)/T0;this.glow.spawn(pos.x+x*r0,y0,pos.z+z*r0,x*s,0,z*s,T0*(1.1+rnd()),.17,.05,hot,cool);}}\n",""],
 ["for(let i=0,c=n(opts.ground?14:20);i<c;i++)","for(let i=0,c=opts.ground?n(14)+n(26):n(20);i<c;i++)"]];
// Owner direction 2026-10-11 (effects overhaul: "The effects seem a bit outdated"), applied exactly as Core does. Smoke
// colour: every style's smoke faded to the near-black 0x16181c, so frost steam and geyser spray turned black; a light style's
// smoke now ends at a darker, partly desaturated version of its own smoke colour (NativeBlastSim.SmokeEnd, __ownerSmokeEnd
// below). Stable tumble: a voxel's Euler offset was its pool slot i, which changes when an expiring voxel swap-removes it
// into another slot (a resting shard snapped); it is now a per-voxel seed counted from 0 after each clear and carried
// through the swap (NativeBlastPool), equal to i until the first expiry. Neither touches a random draw, count, lifetime,
// velocity or size. The draw roles, the extra draw-list floats and the shock records have no source counterpart.
const FX_CUTS=[
 ["pal.smoke,0x16181c,","pal.smoke,__ownerSmokeEnd(style,pal.smoke),"],
 ["this.a=new Float32Array(capacity*VoxelPool.A);","this.a=new Float32Array(capacity*VoxelPool.A);this.seed=new Float64Array(capacity);this.nextSeed=0;"],
 ["const i=this.count++;this.spawned++;","const i=this.count++;this.spawned++;this.seed[i]=this.nextSeed;this.nextSeed=(this.nextSeed+1)&0xffffff;"],
 ["this.a=grow(this.a,VoxelPool.A);","this.a=grow(this.a,VoxelPool.A);{const s=new Float64Array(capacity);s.set(this.seed.subarray(0,Math.min(this.count,capacity)));this.seed=s;}"],
 ["for(let n=0;n<VoxelPool.A;n++)this.a[k+n]=this.a[m+n];}","for(let n=0;n<VoxelPool.A;n++)this.a[k+n]=this.a[m+n];this.seed[i]=this.seed[last];}"],
 ["this.euler.set(age*sp*1.3+i,age*sp+i*.7,0);","this.euler.set(age*sp*1.3+this.seed[i],age*sp+this.seed[i]*.7,0);"],
 ["clear(){this.count=0;this.mesh.count=0;","clear(){this.count=0;this.nextSeed=0;this.mesh.count=0;"]];
let directed=bytes.toString('utf8');
for(const [from,to] of [...OWNER_CUTS,...DISC_CUTS,...FX_CUTS]){if(directed.split(from).length!==2)throw Error('Owner cut does not match the pinned source exactly once: '+from.slice(0,60));directed=directed.replace(from,()=>to);}
// NativeBlastSim.SmokeEnd: the same colour conversions as Core (three.js setHex/getHex) and the same constants.
directed+=`\nfunction __ownerSmokeEnd(style,smoke){if(!['frost','kinetic','shock','nova','implosion'].includes(style))return 0x16181c;const lin=c=>c<0.04045?c*0.0773993808:Math.pow(c*0.9478672986+0.0521327014,2.4),srgb=c=>c<0.0031308?c*12.92:1.055*Math.pow(c,0.41666)-0.055,hex=c=>Math.floor(Math.max(0,Math.min(255,srgb(c)*255))+.5);const r=lin(((smoke>>16)&255)/255),g=lin(((smoke>>8)&255)/255),b=lin((smoke&255)/255),l=.2126*r+.7152*g+.0722*b,e=c=>(c+(l-c)*.4)*.5;return hex(e(r))*65536+hex(e(g))*256+hex(e(b));}\n`;
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
const out={source:{file,blob},owner:{lightning:'2026-10-10',disc:'2026-10-11',cuts:OWNER_CUTS.length+DISC_CUTS.length,fx:{date:'2026-10-11',smoke:true,seed:true,cuts:FX_CUTS.length}},constants:{frontSeconds:BLAST_FRONT_SECONDS,capacity:BLAST_CAPACITY,minVoxels:BLAST_MIN_VOXELS},dt:DT,origin:[ORIGIN.x,ORIGIN.y,ORIGIN.z],cases};
fs.writeFileSync(path.join(root,'Tests/blast-parity.json'),JSON.stringify(out)+'\n');console.log('Exported',cases.length,'chip blast runs,',cases.reduce((n,c)=>n+c.frames.length,0),'frames');
