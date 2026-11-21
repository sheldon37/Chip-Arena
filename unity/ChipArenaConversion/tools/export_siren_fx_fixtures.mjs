// Build-time only: run the pinned original client/siren/flames.ts, fx.ts, motion.ts (updateSirenFlames), first-person.ts
// and client/pve/gore.ts with Math.random replaced by a seeded mulberry32 stream, and record what Tests/SirenFxChecks.cs
// replays through Core/NativeFlameField.cs and Core/NativeSirenFx.cs: the flame pools after scripted runs of every siren-*
// event, the claw slashes, projectile trails, fire patches, eruptions, rain, the body fire, the first-person hand flames
// and arm pose, and the Ward Zero gore flames. three.js object UUIDs keep their own random stream (they are not effect
// randomness). Usage: node tools/export_siren_fx_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/siren/flames.ts':'66dc22da4a01a996e1cadcf501d3f8b399e0ba77','src/client/siren/fx.ts':'e4ffbcb23188e7a9fb733c75314f0a2229429e6d',
 'src/client/siren/motion.ts':'730ed1311413099f9132056cba64e86645aed7e8','src/client/siren/first-person.ts':'5685cb0229a197f8f98a5bbd9cc36544636fdcc9',
 'src/client/siren/materials.ts':'37a6f92a012f3e4b6542f0008be352fd41a80de8','src/client/pve/gore.ts':'06e985e9900f740b37b17cbc30993368723f481e'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src/client',f)).href);
const flames=await load('siren/flames.ts'),fxm=await load('siren/fx.ts'),motion=await load('siren/motion.ts'),fp=await load('siren/first-person.ts'),gore=await load('pve/gore.ts');
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
// Seeded effect randomness (mulberry32); three's own UUID draws use the real Math.random.
const real=Math.random;let rng=null;
const mulberry=a=>()=>{a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};
Math.random=function(){const caller=(new Error().stack||'').split('\n')[2]||'';return caller.includes('/node_modules/three/')?real():rng();};
const seed=s=>{rng=mulberry(s);};
const p9=v=>Number(v.toPrecision(9));
/** A FlameField's state for slots below n (the cursor of a run that never wraps, or the capacity). */
const state=(f,n=f.cursor)=>({cursor:f.cursor,live:f.live,n,pos:[...f.pos.slice(0,n*3)].map(p9),vel:[...f.vel.slice(0,n*3)].map(p9),rot:[...f.rot.slice(0,n*3)].map(p9),spin:[...f.spin.slice(0,n*3)].map(p9),
 age:[...f.age.slice(0,n)].map(p9),life:[...f.life.slice(0,n)].map(p9),size:[...f.size.slice(0,n)].map(p9),buoy:[...f.buoy.slice(0,n)].map(p9),drag:[...f.drag.slice(0,n)].map(p9),c0:[...f.c0.slice(0,n*3)].map(p9),c1:[...f.c1.slice(0,n*3)].map(p9)});
const instances=(mesh,n)=>({m:[...mesh.instanceMatrix.array.slice(0,n*16)].map(p9),c:[...mesh.instanceColor.array.slice(0,n*3)].map(p9)});
const out={seedNote:'mulberry32; each scenario reseeds',runs:{}};

// 1. FlameField alone: every tone, a burst, a wrap of the ring cursor, updates of 1/60 and a clamped .05 step.
{seed(11);const f=new flames.FlameField(40),frames=[];const tones=['black','soot','smoke','ember','white','warn'];
 tones.forEach((t,i)=>f.emit(i*.3,1+i*.1,-i*.2,.4-i*.1,1+i*.2,-.3+i*.05,.5+i*.2,.06+i*.01,t,2.2-i*.3,1.6+i*.2));
 f.burst(1,1.2,-.5,10,3,.8,.12,'black',1.2,2.2);f.burst(-1,.4,2,8,2,.6,.09,'ember',.5);
 for(let i=0;i<30;i++){f.update(i===12?.05:1/60);if(i===9)frames.push({after:10,instances:instances(f.mesh,40)});}
 f.burst(0,0,0,20,1.5,.4,.1,'smoke',1,1.4);f.update(1/60);
 out.runs.field={tones,state:state(f,40),frames};}

// 2. siren/fx.ts events and slashes.
const scene=new T.Scene();const sirenState=(extra={})=>({players:[],projectiles:[],...extra});
const describe=fx=>fx.transients.map(tr=>{const o=tr.obj,d={born:p9(tr.born),life:p9(tr.life)};
 if(o.isSprite)return {...d,type:'flash',scale:p9(o.scale.x),opacity:p9(o.material.opacity),p:[o.position.x,o.position.y,o.position.z].map(p9)};
 if(o.isMesh&&o.geometry.type==='RingGeometry')return {...d,type:'shock',inner:o.geometry.parameters.innerRadius,scale:p9(o.scale.x),opacity:p9(o.material.uniforms.uOpacity.value),additive:o.material.blending===T.AdditiveBlending,p:[o.position.x,o.position.y,o.position.z].map(p9)};
 if(o.isMesh&&o.geometry.type==='SphereGeometry')return {...d,type:'explosion',s:[o.scale.x,o.scale.y,o.scale.z].map(p9),turn:p9(o.rotation.y),fade:p9(o.material.uniforms.uFade.value),speed:o.material.uniforms.uSpeed.value,p:[o.position.x,o.position.y,o.position.z].map(p9)};
 if(o.children.length===1){const m=o.children[0];o.updateWorldMatrix(true,true);const e=m.matrixWorld.elements,rm=[0,4,8,12,1,5,9,13,2,6,10,14,3,7,11,15].map(i=>p9(e[i]));return {...d,type:'slash',sweep:p9(m.material.uniforms.uSweep.value),fade:p9(m.material.uniforms.uFade.value),matrix:rm};}
 const c=o.children.map(ch=>[ch.scale.x,ch.scale.z,ch.rotation.y].map(p9));return {...d,type:'cracks',scale:p9(o.scale.x),opacity:p9(o.children[0].material.opacity),p:[o.position.x,o.position.y,o.position.z].map(p9),boxes:c};});
const events=[
 ['slam',{pos:{x:1,y:.1,z:-2},radius:3.6},1],['slam-default',{pos:{x:-2,y:0,z:1}},1],['slam-reduced',{pos:{x:0,y:0,z:0},radius:4.1},.3],
 ['rush',{pos:{x:.5,y:.95,z:.5}},1],['laugh',{pos:{x:0,y:.95,z:3}},1],['mark',{pos:{x:-3,y:.95,z:0}},1],['lunge',{pos:{x:1,y:1,z:1}},1],
 ['catch',{pos:{x:2,y:1.2,z:0}},1],['crush',{pos:{x:2,y:1.8,z:0}},1],['whiff',{pos:{x:0,y:.95,z:-1}},.3],
 ['cast',{pos:{x:0,y:1.6,z:.5},message:'hellfire'},1],['cast-damnation',{pos:{x:0,y:2.3,z:0},message:'damnation'},1],['cast-rain',{pos:{x:4,y:0,z:4},message:'rain'},.3],
 ['explode',{pos:{x:3,y:.4,z:-1},radius:4},1],['explode-reduced',{pos:{x:3,y:.4,z:-1}},.3],['eruption',{pos:{x:-1,y:0,z:2},radius:2.5},1],
 ['drop',{pos:{x:1,y:0,z:1}},1],['seeker-out',{pos:{x:0,y:1.3,z:5}},1],['seeker-hit',{pos:{x:0,y:1.2,z:5}},1],['hit',{pos:{x:-1,y:1,z:0}},1]];
out.runs.events=[];
for(const [i,[name,e,q]] of events.entries()){seed(100+i);const fx=new fxm.SirenFx(scene),type='siren-'+name.replace(/-(default|reduced|damnation|rain)$/,'');
 fx.update(sirenState(),10,new Map(),q);fx.event({type,...e},10,q);const counts=[];
 for(let k=1;k<=8;k++){fx.update(sirenState(),10+k/60,new Map(),q);counts.push(fx.fire.cursor);}
 out.runs.events.push({name,type,event:e,quality:q,counts,transients:describe(fx),state:state(fx.fire)});fx.clear();scene.remove(fx.group);}
// Slashes: a Siren seen at src (lastPos), the event at src + the swing direction, so pivot = src + .38 and yaw = theta.
out.runs.slashes=[];
for(const [i,[type,side,theta,q]] of [['siren-swipe',1,0,1],['siren-rend',-1,.9,1],['siren-swipe',-1,-2.2,.3],['siren-rend',1,2.8,.3]].entries()){
 seed(200+i);const fx=new fxm.SirenFx(scene),src={x:1.5,y:.95,z:-.5},players=[{id:'s',pos:src,yaw:0,hp:100,siren:{}}];
 fx.update(sirenState({players}),20,new Map(),q);fx.event({type,source:'s',amount:side,radius:3,pos:{x:src.x+Math.sin(theta)*.5,y:src.y,z:src.z+Math.cos(theta)*.5}},20,q);
 const looks=[];for(let k=1;k<=30;k++){fx.update(sirenState({players}),20+k/60,new Map(),q);if(k===3||k===8)looks.push({k,transients:describe(fx),cursor:fx.fire.cursor});}
 out.runs.slashes.push({type,side,theta,quality:q,src,looks,state:state(fx.fire)});fx.clear();scene.remove(fx.group);}
// Aura: a laughing Siren pulses a dark ring every .7 s between at and hitAt.
{seed(260);const fx=new fxm.SirenFx(scene),players=[{id:'s',pos:{x:0,y:.95,z:0},yaw:0,hp:100,siren:{move:{id:'laugh',at:30,hitAt:32}}}],born=[];
 for(let k=0;k<=120;k++){fx.update(sirenState({players}),29.9+k/60,new Map(),1);}
 out.runs.aura={transients:describe(fx)};fx.clear();scene.remove(fx.group);}

// 3. Projectiles: hellfire, damnation, seeker and drop trails; the first two are removed at frame 20.
{seed(300);const fx=new fxm.SirenFx(scene);const shots=[
 {id:'h',pos:{x:0,y:1.5,z:0},vel:{x:0,y:0,z:20},siren:{kind:'hellfire',born:40}},{id:'d',pos:{x:1,y:2.4,z:0},vel:{x:2,y:3,z:12},siren:{kind:'damnation',born:40}},
 {id:'s',pos:{x:-2,y:1.2,z:3},vel:{x:1,y:0,z:2.8},siren:{kind:'seeker',born:39.5}},{id:'r',pos:{x:3,y:8,z:3},vel:{x:0,y:-10,z:0},siren:{kind:'drop',born:39.9,land:{x:3,y:0,z:3},landAt:40.7}}];
 const frames=[];fx.update(sirenState(),40,new Map(),1);
 for(let k=1;k<=26;k++){const t=40+k/60;const live=k<20?shots:shots.slice(2);for(const s of live){s.pos={x:s.pos.x+s.vel.x/60,y:s.pos.y+s.vel.y/60,z:s.pos.z+s.vel.z/60};}
  frames.push({t,shots:live.map(s=>({id:s.id,kind:s.siren.kind,born:s.siren.born,p:[s.pos.x,s.pos.y,s.pos.z],v:[s.vel.x,s.vel.y,s.vel.z]})),cursor:-1});
  fx.update(sirenState({projectiles:live.map(s=>({...s,pos:{...s.pos}}))}),t,new Map(),k>12?.3:1);frames[frames.length-1].cursor=fx.fire.cursor;}
 out.runs.shots={frames,state:state(fx.fire)};fx.clear();scene.remove(fx.group);}

// 4. Fire patches, an eruption through its burst, and rain.
{seed(400);const fx=new fxm.SirenFx(scene);const world={patches:[{id:'p1',owner:'s',kind:'hellfire',pos:{x:1,y:0,z:1},radius:1.1,at:50,until:60},{id:'p2',owner:'s',kind:'trail',pos:{x:-1,y:0,z:2},radius:.85,at:49.5,until:52.5}],
 eruptions:[{id:'e1',owner:'s',pos:{x:3,y:0,z:-2},radius:2.2,at:50,burstAt:51,until:51.5}],rains:[{id:'r1',owner:'s',center:{x:0,y:0,z:0},radius:7,at:50,until:60}],marks:[],grabs:[]};
 const frames=[];fx.update(sirenState({siren:world}),50,new Map(),1);
 for(let k=1;k<=100;k++){const t=50+k/60,q=k>70?.3:1;fx.update(sirenState({siren:world}),t,new Map(),q);const v=fx.eruptions.get('e1');
  frames.push({t,quality:q,cursor:fx.fire.cursor,column:v?{visible:v.column.visible,s:[v.column.scale.x,v.column.scale.y].map(p9),turn:p9(v.column.rotation.y),fade:p9(v.column.material.uniforms.uFade.value)}:null});}
 out.runs.ground={world,frames,state:state(fx.fire)};fx.clear();scene.remove(fx.group);}

// 5. Body fire (motion.ts updateSirenFlames) on a stand-in rig: the joints' world matrices are exported for the replay.
{const names={pelvis:1,spine:2,chest:3,head:5,leftShoulder:6,leftArm:7,leftElbow:8,leftHand:9,rightShoulder:10,rightArm:11,rightElbow:12,rightHand:13,leftKnee:15,rightKnee:19};
 const TONGUES=[['head',-.115,.33,.01,.85,-.2],['head',-.045,.35,-.03,1.2,-.32],['head',.04,.35,-.01,1.05,-.28],['head',.115,.33,.02,.8,-.18],['head',0,.31,-.12,1.0,-.62],['head',-.09,.29,-.13,.75,-.7],['head',.09,.29,-.12,.7,-.7],
  ['leftShoulder',.1,.07,0,.8,-.15],['leftShoulder',.17,.04,-.05,.6,-.3],['rightShoulder',-.1,.07,0,.8,-.15],['rightShoulder',-.17,.04,-.05,.6,-.3],
  ['chest',-.11,.31,-.12,.95,-.45],['chest',.09,.32,-.13,1.05,-.5],['chest',0,.22,-.17,.85,-.75],['chest',-.2,.12,-.15,.6,-.7],['chest',.2,.12,-.15,.6,-.7]];
 out.runs.body=[];
 for(const [n,cfg] of [{q:1,flare:0,ranged:0,lean:0,grab:false,vx:0,vz:0},{q:.3,flare:.6,ranged:.8,lean:.4,grab:true,vx:3,vz:-4}].entries()){
  seed(500+n);const group=new T.Group();group.position.set(1,.95,-2);group.rotation.y=.7;group.scale.setScalar(1.15);const rig={group};
  for(const [name,i] of Object.entries(names)){const j=new T.Object3D();j.position.set(Math.sin(i)*.3,Math.cos(i*1.3)*.5,Math.sin(i*2.1)*.2);j.rotation.set(Math.sin(i*.7)*.6,Math.cos(i*.9)*.8,Math.sin(i*1.7)*.4);group.add(j);rig[name]=j;}
  const tongues=TONGUES.map(([joint,x,y,z,height,lean],i)=>{const g=new T.Group();g.position.set(x,y,z);rig[joint].add(g);return {group:g,phase:i*1.7+x*9,height,lean};});
  const parts={root:group,flames:new flames.FlameField(420),tongues,state:{ranged:cfg.ranged,flare:cfg.flare}};group.updateMatrixWorld(true);
  // The joints' world matrices, row-major at full precision (the tongues ride on them).
  const joints=new Array(22*16).fill(0);for(const [name,i] of Object.entries(names)){const e=rig[name].matrixWorld.elements;for(let r=0;r<4;r++)for(let c=0;c<4;c++)joints[i*16+r*4+c]=e[c*4+r];}
  const frames=[];for(let k=0;k<14;k++){const t=5+k/60,dt=k===6?0:1/60;
   // applySirenLook's tongue flicker (model.ts:143-147), with s.time = t, s.lean and s.flare.
   for(const g of tongues){const nn=.5+.5*Math.sin(t*7.3+g.phase)*Math.sin(t*3.1+g.phase*1.9);g.group.scale.set(1,g.height*(.7+.55*nn)*(1+cfg.flare*.5),1);g.group.rotation.set(g.lean-.5*cfg.lean+.14*Math.sin(t*2.6+g.phase),0,.24*Math.sin(t*3.4+g.phase*1.3));}
   group.updateMatrixWorld(true);motion.updateSirenFlames(rig,parts,t,dt,cfg.q,false,{x:cfg.vx,z:cfg.vz},cfg.grab);frames.push({t,dt,cursor:parts.flames.cursor});}
  out.runs.body.push({cfg,joints,frames,state:state(parts.flames)});}}

// 6. First-person claws (first-person.ts sirenViewModel) on a stand-in view rig.
{seed(600);const group=new T.Group(),view={group,weapon:new T.Object3D(),offWeapon:new T.Object3D()};
 for(const [side,x] of [['left',.2],['right',-.2]]){const arm=new T.Object3D(),elbow=new T.Object3D(),hand=new T.Object3D();arm.position.set(x,.4,-.1);elbow.position.set(0,-.3,0);hand.position.set(0,-.25,0);group.add(arm);arm.add(elbow);elbow.add(hand);view[side+'Arm']=arm;view[side+'Elbow']=elbow;view[side+'Hand']=hand;}
 const frames=[];const p={spec:'siren',hp:100,siren:{mode:'ranged',modeAt:69.9}};
 for(let k=0;k<40;k++){const t=70+k/60;if(k===20){p.siren={mode:'melee',modeAt:t};}
  fp.sirenViewModel(view,p,t,1/60);group.updateMatrixWorld(true);const inv=group.matrixWorld.clone().invert();
  const rel=h=>{const m=h.matrixWorld.clone().premultiply(inv).elements;return [0,4,8,12,1,5,9,13,2,6,10,14,3,7,11,15].map(i=>p9(m[i]));};
  const r=o=>[o.rotation.x,o.rotation.y,o.rotation.z].map(p9);const clawGroup=view.leftHand.children.find(c=>c.name==='siren-view-claws'),orb=view.leftHand.children.find(c=>c.name==='siren-view-orb'),orbR=view.rightHand.children.find(c=>c.name==='siren-view-orb');
  frames.push({t,mode:p.siren.mode,modeAt:p.siren.modeAt,leftArm:r(view.leftArm),leftElbow:r(view.leftElbow),leftHand:r(view.leftHand),rightArm:r(view.rightArm),rightHand:r(view.rightHand),
   claw:{visible:clawGroup.visible,y:p9(clawGroup.scale.y)},orb:{visible:orb.visible,s:p9(orb.scale.x)},orbRight:p9(orbR.scale.x),left:rel(view.leftHand),right:rel(view.rightHand)});}
 const mesh=group.children.find(c=>c.name==='siren-view-flames');out.runs.view={frames,instances:instances(mesh,120)};}

// 7. Ward Zero gore flames (gore.ts hit, gib, drip and update).
{seed(700);const g=new gore.HordeGore(scene),runs=[];
 g.hit({x:1,y:1.05,z:2},.8,1,()=>{});g.hit({x:-1,y:1.05,z:0},-2.4,.3,()=>{});
 const gibs=[{pos:{x:.5,y:1.3,z:-1},from:{x:.6,y:.1,z:.8},message:'lArm',time:12.345,target:'10042'},{pos:{x:-2,y:.9,z:3},message:'head',time:30.5,target:'10007'}];
 for(const [i,e] of gibs.entries()){g.gib(e,i?.3:1,()=>{});runs.push({event:e,quality:i?.3:1,seed:g.list[g.list.length-1].seed});}
 const acc={v:.25},drips=[];for(let k=0;k<20;k++){g.drip({x:0,y:.6,z:1},1/60,k<10?1:.3,acc);drips.push(p9(acc.v));g.update(40+k/60,k===5?.08:1/60);}
 out.runs.gore={hits:[[1,1.05,2,.8,1],[-1,1.05,0,-2.4,.3]],gibs:runs,drips,state:state(g.flames)};}

fs.writeFileSync(path.join(root,'Tests/siren-fx-parity.json'),JSON.stringify({source:pinned,...out})+'\n');
console.log('Exported',out.runs.events.length,'events,',out.runs.slashes.length,'slashes, shots, ground, body, view and gore runs');
