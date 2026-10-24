// Build-time only: run the pinned original client/emperor-effects.ts, matrix-effects.ts, hadouken-visual.ts and
// demon-afterimages.ts on scripted snapshots and record what Tests/DevFxChecks.cs replays through Core/NativeDevFx.cs:
// the Emperor bolt instances (all three layers on some frames, the glow layer on budget frames) (fist, juggle, impacts, storm trails, the 512 budget), the Leet glyph pool (trails, static
// columns, the 640 cap), the nuke blast, nuke field and tree transforms, the hadouken dome parts and tongues, and the
// Demon Step afterimage pool. None of these use Math.random.
// Usage: node tools/export_dev_fx_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/emperor-effects.ts':'c4934c6a1f9ca8557a42a7aa9bce2006e904a0c5','src/client/matrix-effects.ts':'87ce6848e45781f96adbaa8a66589da322631670',
 'src/client/hadouken-visual.ts':'1b0904929a9112ac1b57bf242396a956363f376d','src/client/demon-afterimages.ts':'44ed2e57de2feca16ea648b809647168580fab9a','src/shared/demon-step.ts':'d14ad005b1a564691b15dc1a20921f7620eec4ef'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src/client',f)).href);
const EM=await load('emperor-effects.ts'),MX=await load('matrix-effects.ts'),HV=await load('hadouken-visual.ts'),DA=await load('demon-afterimages.ts');
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const p9=v=>Number(v.toPrecision(9));const scene=new T.Scene();const out={runs:{}};
const inst=(m,n)=>[...m.instanceMatrix.array.slice(0,n*16)].map(p9);
const cols=(m,n)=>m.instanceColor?[...m.instanceColor.array.slice(0,n*3)].map(p9):[];

// 1. Emperor bolts: a rigged and an unrigged Electric fist, a dead Emperor, a juggle, every impact radius, storm trails
// past the 512-segment budget, then Reduced quality (no trails).
{const fx=new EM.EmperorEffects(scene),hand=new T.Group();hand.position.set(1.2,1.4,-.6);scene.add(hand);const rigs=new Map([['emp-1',{rightHand:hand}]]);
 const [glow,dark,core]=fx.group.children;const trails=[];for(let i=0;i<24;i++)trails.push({pos:{x:-6+i*.5,y:.95,z:3-i*.25},at:9.71+i*.013});
 const events=[[2,{type:'emperor-hit',id:41,pos:{x:2,y:1.4,z:0},radius:1.3}],[3,{type:'emperor-hit',id:42,pos:{x:-1,y:1.2,z:2},radius:.65}],[4,{type:'emperor-hit',id:57,pos:{x:0,y:.95,z:-3}}],
  [6,{type:'emperor-bound',id:60,pos:{x:3,y:.9,z:1},radius:1.8}],[9,{type:'arcade-hit',id:61,pos:{x:0,y:0,z:0}}]];
 for(let i=0;i<18;i++)events.push([12,{type:'emperor-hit',id:70+i,pos:{x:i*.3,y:1,z:-i*.2},radius:.65}]);
 const frames=[];
 for(let k=0;k<30;k++){const t=10+k/30,q=k<20?1:.3;
  for(const [at,e] of events)if(at===k)fx.event(e,t);
  const state={time:t,players:[{id:'emp-1',spec:'emperor',hp:100,pos:{x:1,y:1,z:-1},emperor:{electricAt:10.05}},{id:'emp-22',spec:'emperor',hp:100,pos:{x:-3,y:.9,z:2},emperor:{electricAt:10.2}},
   {id:'emp-3',spec:'emperor',hp:0,pos:{x:5,y:1,z:5},emperor:{electricAt:10.1}},{id:'v',spec:'pistol',hp:80,pos:{x:2,y:1.5,z:0},juggle:{lastHit:10.1,hits:3}}],emperorTrails:k<16?trails.slice(0,k<8?3:24):[]};
  fx.update(state,t,rigs,q);const n=glow.count,full=[0,3,6].includes(k),some=full||[12,18].includes(k);
  frames.push({t:p9(t),q,count:n,glow:some?inst(glow,n):null,dark:full?inst(dark,n):null,core:full?inst(core,n):null});}
 out.runs.emperor={frames,events:events.map(([at,e])=>({at,...e})),trails,hand:[1.2,1.4,-.6]};fx.clear();scene.remove(fx.group);}

// 2. Leet glyphs, static columns, the nuke blast, the nuke field and the tree.
{const fx=new MX.MatrixEffects(scene),camera=new T.PerspectiveCamera();camera.quaternion.setFromEuler(new T.Euler(-.3,.8,.1));
 const events=[],frames=[];let id=0,frame=0;const ev=(t,e)=>events.push({id:++id,time:t,frame,...e});
 const fields=[{id:'f1',kind:'nuke',pos:{x:3,y:.9,z:-2},at:19.5},{id:'f2',kind:'tree',pos:{x:-4,y:0,z:5},at:19.9}];
 for(let k=0;k<90;k++){const t=20+k/60,q=k<60?1:.3;frame=k;
  if(k===5)ev(t,{type:'leet-slender-hop',pos:{x:-3,y:0,z:6},from:{x:-4.5,y:0,z:5.2},message:'f2'});
  if(k===8)ev(t-.6,{type:'leet-slender-hop',pos:{x:9,y:0,z:9},from:{x:8,y:0,z:8},message:'f2'});
  if(k===10)ev(t,{type:'minionDeath',message:'slender',pos:{x:-3,y:1.23,z:6}});
  if(k===11)ev(t,{type:'minionDeath',message:'zombie',pos:{x:1,y:1,z:1}});
  if(k===12)ev(t-.2,{type:'leet-nuke',pos:{x:3,y:.03,z:-2}});
  if(k===20)for(let j=0;j<10;j++)ev(t,{type:'leet-slender-hop',pos:{x:j,y:0,z:-j},from:{x:j-1,y:.5,z:-j},message:'f2'});
  if(k===65)ev(t,{type:'leet-slender-hop',pos:{x:2,y:0,z:2},message:'f2'});
  const projectiles=[{id:'b1',spec:'leet',pos:{x:-2+k*.2,y:1.3,z:1-k*.05}},{id:'b2',spec:'pistol',pos:{x:0,y:1,z:0}}];if(k<30)projectiles.push({id:'b3',spec:'leet',pos:{x:4,y:2-k*.02,z:-1}});if(k>=40)projectiles.push({id:'b3',spec:'leet',pos:{x:5,y:1,z:5}});
  const state={generation:3,time:t,events:events.slice(),projectiles,leetFields:k<70?fields:fields.slice(1)};fx.update(state,camera,q);
  const [m0,m1]=fx.meshes,n0=m0.count,n1=m1.count;const blasts=[...fx.blasts.values()].map(b=>({p:[b.group.position.x,b.group.position.y,b.group.position.z].map(p9),s:p9(b.group.scale.x),ring:p9(b.group.children[2].scale.x),opacity:p9(b.material.opacity)}));
  const nuke=fx.objects.get('f1'),tree=fx.objects.get('f2')?.getObjectByName('tree');
  const full=[0,1,6,13,21,66,85].includes(k);
  frames.push({t:p9(t),q,n:[n0,n1],m0:full?inst(m0,n0):null,m1:full?inst(m1,n1):null,c0:full?cols(m0,n0):null,c1:full?cols(m1,n1):null,blasts,
   nuke:nuke?{p:[nuke.position.x,nuke.position.y,nuke.position.z].map(p9),glyphs:nuke.children.slice(1).map(o=>[o.position.x,o.position.y,o.position.z,o.rotation.y].map(p9))}:null,tree:tree?p9(tree.scale.y):null});}
 const tree=fx.objects.get('f2').getObjectByName('tree');
 out.runs.leet={camera:[camera.quaternion.x,camera.quaternion.y,camera.quaternion.z,camera.quaternion.w].map(p9),events,frames,
  tree:tree.children.map(o=>({p:[o.position.x,o.position.y,o.position.z].map(p9),r:[o.rotation.x,o.rotation.y,o.rotation.z].map(p9),s:[o.scale.x,o.scale.y,o.scale.z].map(p9),size:[o.geometry.parameters.width,o.geometry.parameters.height,o.geometry.parameters.depth]}))};fx.clear();}

// 3. Hadouken domes, red and blue, Full then Reduced.
{const runs=[];
 for(const red of [true,false]){const v=new HV.HadoukenVisual(red),frames=[];const [shell,core,rim,...tongues]=v.group.children;
  for(let k=0;k<14;k++){const t=30+k/60,q=k<8?1:.3,radius=(red?.5:.24)*1.3,vel=k===5?{x:0,y:0,z:0}:{x:23*Math.cos(.4+k*.05),y:2-k*.3,z:23*Math.sin(.4+k*.05)};
   v.update({pos:{x:1+k*.4,y:1.2,z:-2+k*.1},vel,radius},t,q);const g=v.group.quaternion;
   frames.push({t:p9(t),q,radius:p9(radius),vel:[vel.x,vel.y,vel.z].map(p9),pos:[v.group.position.x,v.group.position.y,v.group.position.z].map(p9),quat:[g.x,g.y,g.z,g.w].map(p9),
    core:[core.position.z,core.scale.x,core.scale.y,core.scale.z].map(p9),shell:[shell.position.z,shell.scale.x,shell.scale.y,shell.scale.z].map(p9),rim:[rim.position.z,rim.scale.x,rim.rotation.z].map(p9),
    tongues:tongues.map(m=>m.visible?[...m.geometry.getAttribute('position').array].map(p9):null),hot:tongues.map(m=>m.material===tongues[0].material)});}
  const mats=[core.material,rim.material,shell.material].map(m=>({color:m.color.getHex(),opacity:m.opacity,additive:m.blending===T.AdditiveBlending}));
  runs.push({red,frames,materials:mats});v.dispose();}
 out.runs.hadouken=runs;}

// 4. Demon Step afterimages: sampling, the slot ring, fade, dead owners, Reduced quality and the epoch clear.
{const fx=new DA.DemonAfterimages(scene),rig=()=>{const g=new T.Group();g.add(new T.Mesh(new T.BoxGeometry(.3,.6,.2)));return {group:g};};const rigs=new Map([['a1',rig()],['a2',rig()]]);
 const pool=fx.pool,frames=[];
 for(let k=0;k<80;k++){const t=40+k/60,q=k<45?1:.3,epoch=k<70?1:2;
  const p=(id,at,dead)=>({id,spec:'ansatsuken',hp:dead?0:100,connected:true,dashTime:t>=at&&t-at<.3?.3-(t-at):0,arcade:{demonStepAt:at}});
  const state={generation:epoch,setNumber:1,round:1,time:t,players:[p('a1',40.05,k>=30&&k<40),p('a2',k<45?40.2:40.85),{id:'x',spec:'pistol',hp:100,connected:true,dashTime:.2}]};
  fx.update(state,rigs,q);frames.push({t:p9(t),q,epoch,slots:pool.map(e=>[p9(e.at),e.owner,e.mesh.visible?1:0,p9(e.mesh.material.opacity)])});}
 out.runs.demon={frames,players:[['a1',40.05,30,40],['a2',40.2,40.85]]};fx.clear();}

fs.writeFileSync(path.join(root,'Tests/dev-fx-parity.json'),JSON.stringify({source:pinned,...out})+'\n');
console.log('Exported emperor',out.runs.emperor.frames.length,'leet',out.runs.leet.frames.length,'hadouken',out.runs.hadouken.length,'demon',out.runs.demon.frames.length,'frames');
