// Build-time only: run the pinned original client/pve/gore.ts, beacons.ts and downed.ts (and siren/minions/look.ts) and
// record what Tests/GoreBeaconChecks.cs compares: hash01/FNV, gib() chunks and ink counts, chunkAt(), HordeGore.update()
// slot matrices, the 12-chunk cap and rewind, drops(), beaconState(), ringDrawCount()/ringGeometry(), helpWidth(),
// helpBob(), and (in headless Chromium) the beacons.ts motes map and HELP bubble canvases.
// Usage: node tools/export_gore_beacon_fixtures.mjs <source checkout at 05950ea with node_modules> [chromium executable]
import fs from 'node:fs';import path from 'node:path';import {createRequire} from 'node:module';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),chromium=process.argv[3];
const pinned={'src/client/pve/gore.ts':'06e985e9900f740b37b17cbc30993368723f481e','src/client/pve/beacons.ts':'833ac1ee6f1c0d1ced4f6e00a4538670cb6b75fb',
 'src/client/pve/downed.ts':'d55118853db5c7b34a3ae7cbaf0943a7fc196249','src/client/siren/minions/look.ts':'4cd063ed0c97719beab82ec4f7e82e501cc78cb4'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src/client',f)).href);
const gore=await load('pve/gore.ts'),look=await load('siren/minions/look.ts'),beacons=await load('pve/beacons.ts'),downed=await load('pve/downed.ts');
const out={};const scene={add(){},remove(){}};
out.hash01=[[0,0,0,0],[1,7,0,0],[123456789,13,0,0],[4294967295,1,2,0],[987654321,5,3,1],[42,2,1,0],[2654435761,25,9,0]].map(([seed,a,b,c])=>({seed,a,b,c,v:look.hash01(seed,a,b,c)}));
out.fnv=['','lArm','10042','head','rShin','7'].map(s=>({s,v:look.minionSeed(s)}));
const events=[
 {pos:{x:1.2,y:1.18,z:-3.4},from:{x:.6,y:0,z:.8},message:'lArm',time:12.345,target:'10042'},
 {pos:{x:-4,y:.95,z:2},from:{x:0,y:-1,z:0},message:'head',time:30.0016,target:'10007'},
 {pos:{x:0,y:.4,z:0},from:{x:-.3,y:.2,z:-.9},message:'rShin',time:7.5,target:'10100'},
 {pos:{x:3,y:.62,z:5},message:'lLeg',time:100.25,target:'10003'},
 {pos:{x:-2,y:1.0,z:-1},from:{x:1,y:0,z:0},message:'mystery',time:55.555,target:'10011'},
];
const g=new gore.HordeGore(scene);out.gibs=[];
for(const [i,e] of events.entries()){const q=i%2?.3:1,ink=[];g.gib(e,q,(pos,color,count,power)=>ink.push({count,power,color}));const c=g.list[g.list.length-1];
 out.gibs.push({event:e,quality:q,seed:c.seed,at:c.at,p:c.p,v:c.v,size:c.size,spin:c.spin,ink});}
out.chunkAt=[];for(const c of g.list)for(const age of [0,.05,.2,.45,.7,1,1.3,2,3.9]){const o={x:0,y:0,z:0,landed:false};gore.chunkAt(c,age,o);out.chunkAt.push({p:c.p,v:c.v,size:c.size,age,x:o.x,y:o.y,z:o.z,landed:o.landed});}
const M=g.chunks.matrix.constructor,V=g.chunks.position.constructor,Q=g.chunks.quaternion.constructor,m=new M(),pp=new V(),qq=new Q(),ss=new V();
const slots=h=>{const r=[];for(let k=0;k<h.chunks.count;k++){h.chunks.getMatrixAt(k,m);m.decompose(pp,qq,ss);r.push({p:[pp.x,pp.y,pp.z],q:[qq.x,qq.y,qq.z,qq.w],s:[ss.x,ss.y,ss.z]});}return r;};
out.poses=[];for(const [i,e] of events.entries()){const h=new gore.HordeGore(scene);h.gib(e,1,()=>{});
 for(const age of [-.2,0,.1,.35,.6,.9,1.5,3,3.5,3.8,3.99,4.01]){const t=e.time+age;h.update(t,1/60);out.poses.push({event:i,age,t,count:h.chunks.count,live:h.liveChunks(t),list:h.list.length,slots:slots(h)});}}
const several=new gore.HordeGore(scene);events.slice(0,4).forEach((e,i)=>several.gib({...e,time:50+i*.2},1,()=>{}));
out.multi=[50.1,50.5,51,53.7,54.1,54.5].map(t=>{several.update(t,1/60);return {t,count:several.chunks.count,slots:slots(several)};});
const col=[],C=g.chunks.instanceColor.array;for(let i=0;i<12*3;i++)col.push(C[i]);out.slotColors=col;
const cap=new gore.HordeGore(scene);for(let i=0;i<15;i++)cap.gib({pos:{x:i,y:1,z:0},from:{x:0,y:0,z:1},message:'lFore',time:200+i*.01,target:'1'+i},1,()=>{});
out.cap={length:cap.list.length,first:cap.list[0].at,live:cap.liveChunks(200.2)};cap.update(199,1/60);out.cap.afterRewind=cap.list.length;
out.drops=[0,.3,.5,1,2].map(q=>({q,n:gore.HordeGore.drops(q)}));
const view=party=>({stage:'wave',party});
out.beacon=[[{selfReviveAt:10,downAt:5,connected:true},7],[{selfReviveAt:10,downAt:5,connected:true},11],[{selfReviveAt:10,connected:true},8],[{reviveCost:200,connected:true},3],[{connected:true},3],[{selfReviveAt:9.2,downAt:4.2,connected:true},8.95]]
 .map(([row,t])=>({row,t,...beacons.beaconState(view({a:row}),'a',t)}));
out.ring=[0,.5,1/3,.99,1,1.5,-1].map(p=>({p,n:beacons.ringDrawCount(p)}));
const rg=beacons.ringGeometry(),pos=rg.getAttribute('position').array,rc=rg.getAttribute('color').array;
out.ringVerts={count:rg.getAttribute('position').count,first:[...pos.slice(0,18)],arcFirst:[...pos.slice(48*18,48*18+18)],levels:[rc[0],rc[48*18]]};
out.helpWidth=[1,5,12,26,40].map(d=>({d,w:downed.helpWidth(d)}));
out.helpBob=[[0,'1'],[1.3,'1'],[2.7,'101'],[10.05,'7'],[3.3,'']].map(([t,id])=>({t,id,v:downed.helpBob(t,id)}));
// The canvases, drawn by the source code (beacons.ts:33-35 motes; :54-56 the bubble, without its lettering).
const req=createRequire(path.join(source,'node_modules/'));let pw;try{pw=req('playwright');}catch{pw=createRequire('/opt/node-tools/node_modules/')('playwright');}
const browser=await pw.chromium.launch(chromium?{executablePath:chromium}:{});const page=await browser.newPage();
const [motes,shape]=await page.evaluate(([motesSrc,helpSrc])=>{
 const run=(src,w,h)=>{const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;const ctx=canvas.getContext('2d');new Function('ctx',src)(ctx);return Array.from(ctx.getImageData(0,0,w,h).data);};
 return [run(motesSrc,64,256),run(helpSrc,256,128)];
},[(()=>{const s=fs.readFileSync(path.join(source,'src/client/pve/beacons.ts'),'utf8').split('\n')[33];return s.slice(s.indexOf('const {ctx}=c,')+'const {ctx}=c,'.length).replace(/^/,'const ').replace(/;\s*$/,'')+';'+fs.readFileSync(path.join(source,'src/client/pve/beacons.ts'),'utf8').split('\n')[34].replace(/moteTexture=.*$/,'');})(),
 (()=>{const L=fs.readFileSync(path.join(source,'src/client/pve/beacons.ts'),'utf8').split('\n');const a=L[53].slice(L[53].indexOf('const {ctx}=c,')+'const {ctx}=c,'.length);return 'const '+a+L[54]+L[55]+L[56].replace(/ctx\.textAlign.*$/,'');})()]);
await browser.close();
const b64=a=>Buffer.from(a).toString('base64');
out.motesAlpha=b64(Array.from({length:64*256},(_,i)=>motes[i*4+3]));
const half=[];for(let y=0;y<64;y++)for(let x=0;x<128;x++)for(let c=0;c<4;c++){let s=0;for(const dy of [0,1])for(const dx of [0,1])s+=shape[((y*2+dy)*256+x*2+dx)*4+c];half.push(Math.round(s/4));}
out.helpShape2x=b64(half);
const round=o=>typeof o==='number'?(Number.isInteger(o)?o:Number(o.toPrecision(10))):Array.isArray(o)?o.map(round):o&&typeof o==='object'?Object.fromEntries(Object.entries(o).map(([k,v])=>[k,round(v)])):o;
const fixture={source:pinned,...round(out),_canvas:'motesAlpha: 64x256 alpha, rows top-down; helpShape2x: 128x64 RGBA (2x2 means) of the 256x128 bubble drawn without its text.'};
fs.writeFileSync(path.join(root,'Tests/gore-beacon-parity.json'),JSON.stringify(fixture)+'\n');
console.log('Exported',out.gibs.length,'gibs,',out.poses.length,'poses,',out.multi.length,'multi frames,',out.beacon.length,'beacon states and the motes/HELP canvases');
