// PvE horde visuals (design §8.2-§8.5, stream S5): the crowd LOD of HordeView, impostorPose and gore, on a three.js
// scene in Node (no GPU: counts, matrices and visibility only).
import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {HordeView,HORDE_RENDER,hordeMode,ventStand,VENT_EDGE} from '../src/client/pve/horde-view.js';
import {impostorPose,IMPOSTOR_BOXES,IMPOSTOR_STRIDE,IMPOSTOR_PARTS,type ImpostorView} from '../src/client/pve/impostors.js';
import {HordeGore,GORE,chunkAt} from '../src/client/pve/gore.js';
import {hintObjects} from '../src/client/siren/minions/hints.js';
import {prewarmSirenMinions} from '../src/client/siren/minions/index.js';
import {encodeAdd,LIMB,PVE_TIMING,PVE_EVENT,type PveAddView,type PveView} from '../src/shared/pve/contract.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {fixtureAdd,fixtureBase,pveFixture,fixtureEnteringAdds} from '../src/shared/pve/fixtures.js';
import type {Snapshot} from '../src/shared/simulation.js';

const SLOTS=['p1'];
const camera=()=>{const c=new T.PerspectiveCamera(60,16/9,.1,300);c.position.set(0,1,0);c.updateMatrixWorld();return c;};
type Mode='quality'|'performance'|'compatibility';
function rig(mode:Mode='quality'){const scene=new T.Scene(),h=new HordeView(scene);h.setGraphics(mode==='performance'?'performance':'auto');return {scene,h,cam:camera(),mode};}
function frame(r:ReturnType<typeof rig>,adds:PveAddView[],time:number,extra:Partial<PveView>={}){
 const view:PveView={...fixtureBase({time,ids:SLOTS}),stage:'wave',adds:adds.map(a=>encodeAdd(a,SLOTS)),entries:[],...extra};
 r.h.update({time} as unknown as Snapshot,view,{camera:r.cam,me:'p1',dt:1/60,quality:1,compatibility:r.mode==='compatibility',replay:false,rigs:new Map(),burst:()=>{}});return view;
}
/** n adds on a ring of horizontal radius `d` around the camera (angles spread), walking toward it. */
const ring=(n:number,d:number|((i:number)=>number),from=1,o:Partial<Parameters<typeof fixtureAdd>[0]>={})=>Array.from({length:n},(_,i)=>{
 const a=i*2.399,r=typeof d==='number'?d:d(i);return fixtureAdd({n:from+i,x:Math.sin(a)*r,z:Math.cos(a)*r,yaw:a+Math.PI,vel:{x:-Math.sin(a),z:-Math.cos(a)},...o});});
/** World x,z of every visible impostor box centre, grouped per impostor (11 boxes). */
function impostorCentres(h:HordeView){const m=h.impostors.mesh,out:{x:number;z:number}[]=[],mat=new T.Matrix4(),p=new T.Vector3();
 for(let i=0;i<m.count;i+=IMPOSTOR_BOXES){m.getMatrixAt(i,mat);p.setFromMatrixPosition(mat);out.push({x:p.x,z:p.z});}return out;}
const settle=(r:ReturnType<typeof rig>,adds:PveAddView[],t0:number,frames=20,extra?:Partial<PveView>)=>{for(let k=0;k<frames;k++)frame(r,adds,t0+k/60,extra);};

test.before(()=>{while(!prewarmSirenMinions(50)){/* warm the shared layouts once */}});

test('impostorPose is pure, rate independent and hides lost limbs', ()=>{
 const view:ImpostorView={pos:{x:3,y:.85,z:-2},yaw:.7,vel:{x:.8,z:1.1},state:'active',lost:0,side:1,attack:'swipe',kind:'shambler',attackAt:10.2};
 const at=(t:number)=>{const out=new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE);impostorPose(view,t,out);return out;};
 // Identical output for the same (view,t) whatever sequence of frames came before (30/60/120 Hz).
 const sample=(hz:number)=>{let last:Float32Array|undefined;for(let i=0;i<=hz;i++){const t=10+i/hz;const o=at(t);if(Math.abs(t-10.5)<1e-9)last=o;}return last!;};
 const a=sample(30),b=sample(60),c=sample(120);assert.deepEqual([...a],[...b]);assert.deepEqual([...b],[...c]);
 assert.ok([...a].every(Number.isFinite));
 // Lost limbs: the head, the left arm (upper and fore) and the right shin get zero matrices; the rest stay.
 const lost=new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE);impostorPose({...view,lost:LIMB.head|LIMB.lArm|LIMB.lFore|LIMB.rShin},10.5,lost);
 const zero=(k:number)=>lost.subarray(k*IMPOSTOR_STRIDE,(k+1)*IMPOSTOR_STRIDE).every(x=>x===0);
 for(const part of ['head','lUpper','lFore','rShin'] as const)assert.ok(zero(IMPOSTOR_PARTS.indexOf(part)),`${part} hidden`);
 for(const part of ['pelvis','chest','rUpper','rFore','lThigh','lShin','rThigh'] as const)assert.ok(!zero(IMPOSTOR_PARTS.indexOf(part)),`${part} shown`);
 // States: crawl lies low, a vent climb starts under the floor, a burned-away corpse and a dived add show nothing.
 const head=(o:Float32Array)=>o[IMPOSTOR_PARTS.indexOf('head')*IMPOSTOR_STRIDE+13];
 const crawl=new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE);impostorPose({...view,state:'crawl',pos:{x:3,y:.44,z:-2}},10.5,crawl);assert.ok(head(crawl)<.9,`crawling head at ${head(crawl)}`);
 assert.ok(head(a)>1.3,`standing head at ${head(a)}`);
 const climb=new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE);impostorPose({...view,state:'entering',entry:{kind:'vent',at:10.4,first:true}},10.5,climb);assert.ok(head(climb)<0,'climbing from under the floor');
 assert.equal(impostorPose({...view,deathAt:5},10.5,new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE)),false);
 assert.equal(impostorPose({...view,state:'vent',diveAt:9},10.5,new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE)),false);
});

test('no impostor inside the full-rig band while the near count fits the cap, in every mode', ()=>{
 for(const mode of ['quality','performance','compatibility'] as const){
  const r=rig(mode),R=HORDE_RENDER.fullRadius[mode],cap=HORDE_RENDER.maxRigs[mode];
  const adds=[...ring(cap,i=>3+(R-4)*i/cap),...ring(60-cap,i=>R+6+i*.6,100)];
  settle(r,adds,50,30);
  const m=r.h.metrics(),d=r.h.details();assert.equal(m.fullRigs,cap,mode);assert.equal(m.nearImpostors,0,mode);assert.equal(m.adds,60);
  for(const c of impostorCentres(r.h))assert.ok(Math.hypot(c.x,c.z)>R,`${mode}: impostor ${Math.hypot(c.x,c.z).toFixed(1)} m from the camera`);
  if(mode==='compatibility'){assert.equal(d.impostors,0,'no impostors on the compatibility path');assert.equal(d.hidden,60-cap);}
  else assert.equal(r.h.impostors.mesh.count,d.impostors*IMPOSTOR_BOXES);
  r.h.clear();
 }
});

test('40 adds inside 20 m: 24 full rigs, 16 near impostors, and the instance count matches far plus overflow', ()=>{
 const r=rig('quality'),adds=[...ring(40,i=>6+12*i/40),...ring(20,i=>30+i,200)];
 settle(r,adds,50,30);
 const m=r.h.metrics(),d=r.h.details();
 assert.equal(m.fullRigs,24);assert.equal(m.nearImpostors,40-24);assert.equal(m.adds,60);
 assert.equal(d.impostors,20+16,'far plus overflow adds are impostors');assert.equal(r.h.impostors.mesh.count,(20+16)*IMPOSTOR_BOXES);
 // The overflow are the farthest of the near adds: every rig is nearer than every near impostor.
 const rigDist=Math.max(...adds.filter(a=>r.h.rigOf(a.n)).map(a=>Math.hypot(a.pos.x,a.pos.z))),imp=impostorCentres(r.h).map(c=>Math.hypot(c.x,c.z)).filter(x=>x<20);
 assert.equal(imp.length,16);assert.ok(Math.min(...imp)>=rigDist-.5,`overflow at ${Math.min(...imp).toFixed(1)} m, rigs to ${rigDist.toFixed(1)} m`);
 // Draw budget (meshes + shadow casters + instanced batches).
 assert.ok(r.h.drawEstimate()<=1800,`quality draws ${r.h.drawEstimate()}`);
 // Shadows: none beyond 15 m.
 for(const a of adds){const mm=r.h.rigOf(a.n);if(!mm)continue;const d2=Math.hypot(a.pos.x,a.pos.z);if(d2>HORDE_RENDER.shadowRadius){let any=false;mm.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.castShadow)any=true;});assert.equal(any,false,`add at ${d2.toFixed(1)} m casts shadows`);}}
});

test('the possessed add 30 m away gets a full rig with hints and shadows within 2 frames', ()=>{
 const r=rig('quality'),adds=[...ring(30,i=>5+10*i/30)],p=fixtureAdd({n:77,x:30,z:0,yaw:-Math.PI/2,vel:{x:-.6,z:0}});adds.push(p);
 settle(r,adds.slice(0,30),40,30);
 const boss={id:'pve-boss',phase:2 as const,floor:0,hidden:true,possessed:77,possessedAt:41-20};
 frame(r,adds,41,{bossView:boss});frame(r,adds,41+1/60,{bossView:boss});
 const m=r.h.rigOf(77);assert.ok(m,'possessed add has a full rig');
 const shown=hintObjects(m).filter(o=>o.visible).map(o=>o.name);assert.ok(shown.includes('siren-minion-hint-seam'),`hints: ${shown}`);
 let shadow=false;m.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.castShadow)shadow=true;});assert.ok(shadow,'possessed rig casts shadows');
 // No hint adds an eye (owner face rule E1).
 for(const o of hintObjects(m))assert.ok(!/eye/i.test(o.name),o.name);
 // Ahead of the cap: the possessed rig does not take one of the 24 band slots.
 assert.equal(r.h.metrics().fullRigs,25);assert.equal(r.h.metrics().nearImpostors,30-24);
});

test('hysteresis keeps an add oscillating between 20 and 24 m on its rig; at most 2 rigs are created per frame', ()=>{
 const r=rig('quality');
 // Enter at 18 m, then oscillate 20..24 m: never flips back to an impostor.
 const at=(d:number)=>[fixtureAdd({n:5,x:d,z:0,yaw:Math.PI/2})];
 settle(r,at(18),10,4);assert.ok(r.h.rigOf(5));
 for(let k=0;k<120;k++){const d=22+2*Math.sin(k*.3);frame(r,at(d),11+k/60);assert.ok(r.h.rigOf(5),`flipped at ${d.toFixed(2)} m`);}
 frame(r,at(25),14);assert.equal(r.h.rigOf(5),undefined,'leaves the band beyond 24 m');
 // A burst of 30 near adds: rigs come 2 per frame.
 const s=rig('quality'),adds=ring(30,i=>4+i*.4);let before=0;
 for(let k=0;k<6;k++){frame(s,adds,20+k/60);const now=s.h.details().rigs;assert.ok(now-before<=HORDE_RENDER.createPerFrame,`${now-before} rigs in one frame`);before=now;}
});

test('rigs are disposed after deathAt+3.1 and corpses burn on their rigs until then', ()=>{
 const r=rig('quality'),dead=[fixtureAdd({n:9,x:4,z:2,state:'dead',deathAt:30})];
 settle(r,dead,30,4);assert.ok(r.h.rigOf(9),'corpse rig');
 frame(r,dead,30+PVE_TIMING.corpse-.05);assert.ok(r.h.rigOf(9));
 frame(r,dead,30+PVE_TIMING.corpse+.01);assert.equal(r.h.rigOf(9),undefined);assert.equal(r.h.details().rigs,0,'disposed');
 assert.equal(r.scene.getObjectByName('pve-add-9'),undefined,'removed from the scene');
});

test('the 60-add crowd fixture stays inside the draw budget in quality and performance', ()=>{
 for(const [mode,max] of [['quality',1800],['performance',900]] as const){
  const r=rig(mode),view=pveFixture('horde-crowd',{time:60,ids:SLOTS});
  for(let k=0;k<30;k++)r.h.update({time:60+k/60} as unknown as Snapshot,{...view},{camera:r.cam,me:'p1',dt:1/60,quality:1,compatibility:false,replay:false,rigs:new Map(),burst:()=>{}});
  assert.equal(r.h.metrics().adds,view.adds.length);
  assert.ok(r.h.drawEstimate()<=max,`${mode}: ${r.h.drawEstimate()} draws`);
  assert.ok(r.h.metrics().fullRigs<=HORDE_RENDER.maxRigs[mode]);
 }
 assert.equal(hordeMode(true,'high'),'compatibility');assert.equal(hordeMode(false,'performance'),'performance');assert.equal(hordeMode(false,'auto'),'quality');
});

test('entering adds: vent climbers stand past the pit edge, door walkers get the walk velocity', ()=>{
 const r=rig('quality'),t=100,{adds,entries}=fixtureEnteringAdds({time:t});
 // Camera next to the first vent so it gets a rig.
 const v0=PVE_ENTRIES[0];r.cam.position.set(v0.from.x+3,1.6,v0.from.z+3);r.cam.updateMatrixWorld();
 for(let k=0;k<10;k++)frame(r,adds,t+k/60,{entries});
 const vent=adds.find(a=>a.entry===0)!,m=r.h.rigOf(vent.n);assert.ok(m,'climbing add has a rig');
 const stand=ventStand(0,Math.round(vent.yaw*100)/100);assert.ok(Math.abs(m.rig.group.position.x-stand.x)<1e-6&&Math.abs(m.rig.group.position.z-stand.z)<1e-6,'placed at the stand point');
 const off=Math.hypot(stand.x-v0.from.x,stand.z-v0.from.z);assert.ok(off>VENT_EDGE&&off<2.2,`stand point ${off.toFixed(2)} m from the vent centre`);
 // The rig climbs: under the floor early in the climb (the fixture is 1.0 s in: head and shoulders up, body below).
 m.rig.group.updateMatrixWorld(true);const feet=m.rig.leftFoot.getWorldPosition(new T.Vector3()),hd=m.rig.head.getWorldPosition(new T.Vector3());
 assert.ok(feet.y<-.3&&hd.y>0,`feet ${feet.y.toFixed(2)} m, head ${hd.y.toFixed(2)} m`);
 // Door and cell walkers move with the walk velocity of their entry (the authority view has none while entering).
 const door=adds.find(a=>a.entry===12)!,en=PVE_ENTRIES[12];r.cam.position.set(en.exit.x,1.6,en.exit.z);r.cam.updateMatrixWorld();
 for(let k=0;k<6;k++)frame(r,adds,t+(10+k)/60,{entries});const dm=r.h.rigOf(door.n);assert.ok(dm,'door add has a rig');
 const foot=(s:number)=>{frame(r,adds,s,{entries});dm.rig.group.updateMatrixWorld(true);return dm.rig.leftFoot.getWorldPosition(new T.Vector3());};
 const a0=foot(t+.2),a1=foot(t+.45);assert.ok(a0.distanceTo(a1)>.02,'the walk-out animates its legs');
});

test('gore: at most 12 live limb chunks, drop counts follow effects quality, chunk paths are deterministic', ()=>{
 const scene=new T.Scene(),g=new HordeGore(scene),bursts:number[]=[];const burst=(_p:unknown,_c:number,n:number)=>{bursts.push(n);};
 for(let i=0;i<20;i++)g.gib({pos:{x:i*.3,y:1.2,z:0},from:{x:1,y:0,z:0},message:i%2?'lArm':'head',time:10+i*.05,target:'add-'+i},1,burst);
 g.update(11,1/60);assert.ok(g.liveChunks(11)<=GORE.chunkMax,`${g.liveChunks(11)} chunks`);assert.ok(g.chunks.count<=GORE.chunkMax);
 g.update(10+GORE.chunkLife+2,1/60);assert.equal(g.chunks.count,0,'chunks expire');
 bursts.length=0;g.hit({x:0,y:1,z:0},0,1,burst);g.hit({x:0,y:1,z:0},0,0,burst);g.hit({x:0,y:1,z:0},0,.5,burst);
 assert.deepEqual(bursts,[GORE.drops[1],GORE.drops[0],HordeGore.drops(.5)]);assert.ok(bursts[0]>bursts[2]&&bursts[2]>bursts[1]);
 // Two independent gore systems fed the same event pose the same chunk at the same time (hash-seeded, from event time).
 const mats=(s:number)=>{const sc=new T.Scene(),gg=new HordeGore(sc);gg.gib({pos:{x:1,y:1.3,z:2},from:{x:0,y:0,z:1},message:'rLeg',time:5,target:'add-3'},1,()=>{});gg.update(s,1/60);const m=new T.Matrix4();gg.chunks.getMatrixAt(0,m);return m.elements;};
 assert.deepEqual(mats(5.7),mats(5.7));
 const a={x:0,y:0,z:0,landed:false};chunkAt({p:{x:0,y:1,z:0},v:{x:1,y:2,z:0},size:[.2,.4,.2]},3,a);assert.ok(a.landed&&a.y>0,'lands on the floor');
});

test('a pve-gib event reaches the gore system; clear() removes everything', ()=>{
 const r=rig('quality'),adds=ring(3,5);settle(r,adds,20,3);
 r.h.event({id:1,type:PVE_EVENT.gib,time:20.05,pos:{x:1,y:1.2,z:1},from:{x:1,y:0,z:0},target:'add-1',message:'lArm'},{time:20.05} as unknown as Snapshot,{} as PveView);
 frame(r,adds,20.1);assert.equal(r.h.details().chunks,1);
 r.h.clear();assert.equal(r.h.details().rigs,0);assert.equal(r.h.impostors.mesh.count,0);assert.equal(r.h.gore.chunks.count,0);
});
