import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,readdirSync} from 'node:fs';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {createCharacterRig,RIG_VERSION} from '../src/client/character-rig.js';
import {currentLayout} from '../src/client/voxel-kit.js';
import {probeMotion} from './helpers/motion-probe.js';
import {createSirenMinion,animateSirenMinion,setSirenMinionDetail,sirenMinionStats,minionLook,prewarmSirenMinions,corruptedLayoutCount,MINION_TIMING,MINION_FACES,DEFAULT_MINION_FACE,materialCount,type SirenMinionView,type SirenMinion} from '../src/client/siren/minions/index.js';
import {categoryOf} from '../src/client/siren/minions/model.js';
import {baseLayout} from '../src/client/voxel-kit.js';
import {GLOW_FLAG} from '../src/client/siren/minions/materials.js';

const SEEDS=Array.from({length:24},(_,i)=>(i*2654435761+17)>>>0);
const names=(o:T.Object3D)=>{const out:string[]=[];o.traverse(n=>{if(n.name)out.push(n.name);});return out;};
/** Every mesh's name, layout and material, in traversal order: the minion's full visual identity. */
function fingerprint(m:SirenMinion){const out:string[]=[];m.rig.group.traverse(o=>{if(!(o instanceof T.Mesh))return;const l=currentLayout(o);out.push(`${o.name}|${o.visible}|${l?.key}|${l?.count}|${(o.material as T.Material).name}|${o.position.toArray().map(x=>x.toFixed(5))}`);});return out.join('\n');}
const STATES:Record<string,(t:number)=>SirenMinionView>={
 idle:()=>({yaw:.4,vel:{x:0,z:0}}),walk:()=>({yaw:.4,vel:{x:.5,z:1.1}}),hurt:t=>({yaw:.4,vel:{x:0,z:1},hp:Math.max(0,100-t*30),maxHp:100}),run:()=>({yaw:-1,vel:{x:-3.2,z:2.4}}),
 lunge:()=>({yaw:0,vel:{x:0,z:0},attackAt:1,attack:'lunge'}),club:()=>({yaw:0,vel:{x:0,z:.6},attackAt:1,attack:'club'}),smash:()=>({yaw:0,vel:{x:0,z:0},attackAt:1,attack:'smash'}),
 flinch:()=>({yaw:0,vel:{x:0,z:1},hitAt:1,hitYaw:2}),emerge:()=>({yaw:0,vel:{x:0,z:0},spawnAt:1}),death:()=>({yaw:0,vel:{x:0,z:1.2},deathAt:1.3,attackAt:1.1}),
};
const finiteRig=(m:SirenMinion)=>{let ok=true;m.rig.group.traverse(o=>{for(const x of [...o.position.toArray(),...o.quaternion.toArray(),...o.scale.toArray()])if(!Number.isFinite(x))ok=false;});return ok;};

test('siren minions are deterministic per seed, differ between seeds and keep one family', ()=>{
 for(const seed of SEEDS.slice(0,8)){assert.equal(fingerprint(createSirenMinion(seed)),fingerprint(createSirenMinion(seed)));assert.deepEqual(minionLook(seed),minionLook(seed));}
 const prints=new Set(SEEDS.map(s=>fingerprint(createSirenMinion(s))));assert.equal(prints.size,SEEDS.length,'every seed looks different');
 // Same family: every seed rots, burns and shows the dying glow; most have wounds; looks vary in the knobs.
 const looks=SEEDS.map(s=>minionLook(s));
 for(const l of looks){const c=Object.values(l.parts).map(p=>p.condition);assert.ok(c.includes('burn'),'at least one burn');assert.ok(c.includes('cracked'),'cracked dying glow');}
 assert.ok(looks.filter(l=>Object.values(l.parts).some(p=>p.condition==='wound')).length>=looks.length*.7);
 for(const k of ['arms','scrap','missing','drag'] as const)assert.ok(new Set(looks.map(l=>l[k])).size>1,`${k} varies`);
 // Faces (the owner's choice): blank by default, a cracked head shell on some seeds, burned-in eyes only on request.
 const many=Array.from({length:400},(_,i)=>minionLook((i*7919+3)>>>0).face);
 for(const f of [...looks.map(l=>l.face),...many])assert.ok(f==='blank'||f==='cracked',`seeded face ${f}`);
 assert.ok(many.includes('blank')&&many.includes('cracked'),'both seeded faces appear');
 assert.ok(many.filter(f=>f==='blank').length>many.length*.6,`blank is the majority (${many.filter(f=>f==='blank').length}/${many.length})`);
 assert.equal(DEFAULT_MINION_FACE,'blank');assert.equal(minionLook(SEEDS[0],{face:'eyes'}).face,'eyes');
 // The face has its own hash: forcing one changes nothing else about the seed.
 for(const seed of SEEDS.slice(0,6)){const {face:_a,...rest}=minionLook(seed),{face:_b,...forced}=minionLook(seed,{face:'eyes'});assert.deepEqual(forced,rest);}
 // Face options stay available for the owner.
 for(const face of MINION_FACES)assert.notEqual(fingerprint(createSirenMinion(SEEDS[0],{face})),'');
 assert.notEqual(fingerprint(createSirenMinion(SEEDS[0],{face:'blank'})),fingerprint(createSirenMinion(SEEDS[0],{face:'eyes'})));
});

test('siren minions keep the pilot rig contract and stay inside the mesh budget', ()=>{
 const pilot=names(createCharacterRig('#ff6b35',undefined,true).group);
 let instances=0;
 for(const seed of SEEDS){
  const m=createSirenMinion(seed),own=new Set(names(m.rig.group));
  for(const n of pilot)assert.ok(own.has(n),`seed ${seed} keeps ${n}`);
  assert.equal(m.rig.group.name,'blank-mannequin-v2');assert.equal(m.rig.group.userData.rigVersion,RIG_VERSION);
  const st=sirenMinionStats(m);assert.ok(st.meshes<=55,`seed ${seed}: ${st.meshes} meshes`);assert.ok(st.instances<=1400,`seed ${seed}: ${st.instances} instances`);instances+=st.instances;
  // Characters stay Lambert/Basic (SVG compatibility path); materials are shared, never per minion.
  m.rig.group.traverse(o=>{if(o instanceof T.Mesh)assert.ok(o.material instanceof T.MeshLambertMaterial||o.material instanceof T.MeshBasicMaterial,o.name);});
 }
 assert.ok(instances/SEEDS.length<=1000,`average ${instances/SEEDS.length} instances`);
 const before=materialCount();for(const seed of SEEDS)createSirenMinion(seed+1);assert.ok(materialCount()<=before+24&&materialCount()<=48,'material pool is bounded');
 const a=createSirenMinion(SEEDS[1]),b=createSirenMinion(SEEDS[1]);assert.equal((a.rig.head.getObjectByName('blank-head') as T.Mesh).material,(b.rig.head.getObjectByName('blank-head') as T.Mesh).material);
});

test('compatibility detail falls back to plain tinted boxes and restores', ()=>{
 const m=createSirenMinion(SEEDS[2]),detail:T.Mesh[]=[];m.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.userData.detail&&o.visible)detail.push(o);});
 const full=sirenMinionStats(m).instances;setSirenMinionDetail(m,true);
 m.rig.group.traverse(o=>{if(o instanceof T.InstancedMesh&&o.visible&&!o.name.startsWith('siren-minion-'))assert.equal(o.count,1,`${o.name} is one box in low detail`);});
 for(const d of detail)assert.equal(d.visible,false,d.name);
 // No low-detail stand-in may outgrow its part (a plain unit box on a tongue or scrap would be a metre wide).
 for(const seed of SEEDS.slice(0,12)){const n=createSirenMinion(seed);setSirenMinionDetail(n,true);n.rig.group.updateMatrixWorld(true);const size=new T.Vector3();
  n.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.visible&&o.name!=='siren-minion-fx'){new T.Box3().setFromObject(o).getSize(size);assert.ok(Math.max(size.x,size.y,size.z)<.75,`seed ${seed} ${o.name} is ${size.toArray().map(x=>x.toFixed(2))} m in low detail`);}});}
 // Low detail keeps the corrupted colour (plain boxes are tinted) and the glow strips still flag their glow.
 const strip=m.rig.group.getObjectByName('armor-back-light') as T.InstancedMesh;assert.ok(strip.instanceColor);
 setSirenMinionDetail(m,false);assert.equal(sirenMinionStats(m).instances,full);for(const d of detail)assert.equal(d.visible,true,d.name);
});

test('every animation state gives finite poses, emerges from below the floor and burns away', ()=>{
 const m=createSirenMinion(SEEDS[3]);m.rig.group.position.set(0,-m.floor,0);
 for(const [state,view] of Object.entries(STATES))for(let t=0;t<=5;t+=1/30){animateSirenMinion(m,view(t),t);assert.ok(finiteRig(m),`${state} at ${t.toFixed(2)}`);}
 const head=new T.Vector3();
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0},spawnAt:0},.1);m.rig.group.updateMatrixWorld(true);m.rig.head.getWorldPosition(head);assert.ok(head.y<-.2,`head under the floor at spawn (${head.y.toFixed(2)})`);
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0},spawnAt:0},MINION_TIMING.emerge+.1);m.rig.group.updateMatrixWorld(true);m.rig.head.getWorldPosition(head);assert.ok(head.y>.75,`standing after emergence (${head.y.toFixed(2)})`);
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0},deathAt:0},.9);assert.equal(m.rig.body.visible,true);assert.equal(m.fx.mesh.visible,true,'death flames');
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0},deathAt:0},MINION_TIMING.death+.05);assert.equal(m.rig.body.visible,false,'burned away');
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0},deathAt:0},MINION_TIMING.deathFx+.05);assert.equal(m.fx.mesh.visible,false,'effects end');
 // Alive again (a rematch with the same rig): everything is restored.
 animateSirenMinion(m,{yaw:0,vel:{x:0,z:0}},20);assert.equal(m.rig.body.visible,true);for(const p of m.parts){assert.ok(p.mesh.scale.equals(p.scale),p.mesh.name);assert.equal(p.mesh.material,p.live);}
});

test('death and emergence start from the exact pose they interrupt (no pops)', ()=>{
 const bones=(m:SirenMinion)=>{m.rig.group.updateMatrixWorld(true);const q=new T.Quaternion();return [m.rig.head,m.rig.chest,m.rig.leftHand,m.rig.rightHand,m.rig.leftFoot,m.rig.rightFoot].map(b=>b.getWorldQuaternion(q).clone());};
 for(const seed of SEEDS.slice(0,4)){
  const m=createSirenMinion(seed),walk:SirenMinionView={yaw:.3,vel:{x:.4,z:1.2}};
  for(let t=0;t<2;t+=1/60)animateSirenMinion(m,walk,t);
  const before=bones(m);animateSirenMinion(m,{...walk,deathAt:2},2);const at=bones(m);
  before.forEach((q,i)=>assert.ok(q.angleTo(at[i])<.02,`seed ${seed} bone ${i} pops ${q.angleTo(at[i]).toFixed(3)} rad at death`));
  // Within the death, no bone turns more than 25 degrees in a 60 Hz frame.
  let prev=at;for(let t=2+1/60;t<2+MINION_TIMING.death;t+=1/60){animateSirenMinion(m,{...walk,deathAt:2},t);const now=bones(m);now.forEach((q,i)=>assert.ok(q.angleTo(prev[i])<25*Math.PI/180,`seed ${seed} bone ${i} at ${t.toFixed(2)}`));prev=now;}
  // Emergence hands over to the walk, and attacks and flinches blend in and out, without a single-frame snap: the largest
  // per-frame turn halves when the rate doubles (smooth motion), and stays below 45 degrees even in a strike.
  for(const view of [{...walk,spawnAt:10},{...walk,attackAt:10.3,attack:'lunge' as const},{...walk,attackAt:10.3,attack:'club' as const},{...walk,attackAt:10.3,attack:'smash' as const},{...walk,hitAt:10.3,hitYaw:1}]){
   const peak=(hz:number)=>{const e=createSirenMinion(seed);let last:T.Quaternion[]|undefined,max=0;
    for(let t=10;t<10+MINION_TIMING.emerge+.4;t+=1/hz){animateSirenMinion(e,view,t);const now=bones(e);if(last&&t>10.5)now.forEach((q,i)=>{max=Math.max(max,q.angleTo(last![i])*180/Math.PI);});last=now;}return max;};
   const p60=peak(60),p120=peak(120),label=`seed ${seed} ${JSON.stringify(view)}: ${p60.toFixed(1)} deg/frame at 60 Hz, ${p120.toFixed(1)} at 120 Hz`;
   assert.ok(p60<45,label);assert.ok(p120<p60*.62+1,label);
  }
 }
});

test('walking and running feet are ground-anchored at 30, 60 and 120 Hz', async()=>{
 await initPhysics();
 // The shared probe (tests/helpers/motion-probe.ts) on a minion rig, one fresh minion per rate.
 for(const seed of SEEDS.slice(4,8))for(const speed of [.8,1.3,4.5])for(const hz of [30,60,120]){
  const m=createSirenMinion(seed),good=m.look.drag>0?'right':'left';
  const r=probeMotion({spec:'pistol',hz,rig:m.rig,travel:true,from:1,to:3.5,player:(t,b)=>({...b,yaw:0,vel:{x:0,y:0,z:speed},hitStunUntil:-1e9}),animate:(_r,p,t)=>animateSirenMinion(m,{yaw:p.yaw,vel:p.vel},t)});
  const foot=r.feet[good];
  assert.ok(foot.plantedFrames>0,`seed ${seed} ${speed} m/s ${hz} Hz: the good foot plants`);
  assert.ok(foot.maxSlip<.02,`seed ${seed} ${speed} m/s ${hz} Hz: planted good foot slips ${foot.maxSlip.toFixed(3)} m`);
  for(const f of [r.feet.left,r.feet.right])assert.ok(f.medianPlantedSpeed<.05,`seed ${seed} ${speed} m/s ${hz} Hz: planted feet stay put (${f.medianPlantedSpeed.toFixed(3)} m/s)`);
  assert.ok(r.minAnkleY>=-.796,'never through the floor');
 }
});

test('poses are reproducible after a rewind and the presentation never uses Math.random', ()=>{
 const a=createSirenMinion(SEEDS[9]),b=createSirenMinion(SEEDS[9]),view:SirenMinionView={yaw:.2,vel:{x:0,z:1}};
 for(let t=0;t<4;t+=1/60)animateSirenMinion(a,view,t);
 animateSirenMinion(a,view,1);animateSirenMinion(b,view,1);
 const pose=(m:SirenMinion)=>{const out:number[]=[];m.rig.group.traverse(o=>out.push(...o.quaternion.toArray(),...o.position.toArray()));return out;};
 assert.deepEqual(pose(a),pose(b),'a rewound minion poses exactly like a fresh one');
 const dir='src/client/siren/minions';for(const f of readdirSync(dir))assert.ok(!/Math\.random/.test(readFileSync(`${dir}/${f}`,'utf8')),`${f} uses Math.random`);
 assert.ok(!/Math\.random/.test(readFileSync('tools/visual-lab/minions.ts','utf8')));
 // Glow flag sanity: non-glowing cells never reach the flag value.
 const m=createSirenMinion(SEEDS[10]);m.rig.group.traverse(o=>{if(!(o instanceof T.InstancedMesh)||!o.instanceColor||o.name==='siren-minion-fx')return;const c=o.instanceColor.array;for(let i=0;i<o.count;i++){const r=c[i*3];assert.ok(r<GLOW_FLAG-1+1e-6||r>=GLOW_FLAG,`${o.name} cell ${i} colour ${r}`);}});
});

test('a face only changes the head and crown, whichever minion builds a shared layout first', async()=>{
 // Two fresh copies of the layout cache: one builds a part after a cracked head and crown (as a cracked minion does), the
 // other builds it alone. The cached result must not depend on the face (the key leaves it out for other parts).
 const path='../src/client/siren/minions/layouts.js',A=await import(path+'?first=cracked'),B=await import(path+'?first=blank');
 const rig=createCharacterRig('#2b2733',undefined,true),req=(name:string,face:'blank'|'cracked')=>{const mesh=rig.group.getObjectByName(name) as T.Mesh;return {name,category:categoryOf(name)!,size:mesh.scale.clone(),base:baseLayout(mesh),condition:'rot' as const,variant:1,rot:2 as const,face};};
 A.corruptedPart(req('blank-head','cracked'));A.corruptedPart(req('crown','cracked'));
 for(const name of ['jaw','ribcage','left-thigh','neck-joint','upper-chest']){const a=A.corruptedPart(req(name,'cracked')),b=B.corruptedPart(req(name,'blank'));
  assert.equal(a.layout.key,b.layout.key,name);assert.equal(a.instances,b.instances,`${name}: ${a.instances} cells after a cracked face, ${b.instances} alone`);assert.equal(a.anchors.length,b.anchors.length,`${name} grows no crack tongue`);}
 // Every minion of a seed is identical whatever was built before it.
 for(const seed of SEEDS.slice(0,6)){const st=sirenMinionStats(createSirenMinion(seed,{face:'cracked'}));assert.ok(st.instances<=1400,`seed ${seed} cracked: ${st.instances} instances`);}
});

test('planted feet stay put while turning, through the walk-run band and while speeding up; strides match across rates', async()=>{
 await initPhysics();
 const probe=(seed:number,hz:number,at:(t:number)=>{yaw:number;speed:number})=>{const m=createSirenMinion(seed);
  return {m,r:probeMotion({spec:'pistol',hz,rig:m.rig,travel:true,from:1,to:4,player:(t,b)=>{const s=at(t);return {...b,yaw:s.yaw,vel:{x:s.speed*Math.sin(s.yaw),y:0,z:s.speed*Math.cos(s.yaw)},hitStunUntil:-1e9};},animate:(_r,p,t)=>animateSirenMinion(m,{yaw:p.yaw,vel:p.vel},t)})};};
 const cases:Record<string,{at:(t:number)=>{yaw:number;speed:number};slip:number;median:number;rates:number[]}>={
  // The probe's own root integration on a curve leaves ~.03 m/s at 30 Hz (it halves per rate doubling): judge running turns at 60/120 Hz.
  'walk turning left':{at:t=>({yaw:1.5*t,speed:1.1}),slip:.03,median:.05,rates:[30,60,120]},
  'walk turning right':{at:t=>({yaw:-1.5*t,speed:1.1}),slip:.03,median:.05,rates:[30,60,120]},
  'run turning':{at:t=>({yaw:1.2*t,speed:3.5}),slip:.03,median:.05,rates:[60,120]},
  'walk-run band 2.3':{at:()=>({yaw:.3,speed:2.3}),slip:.03,median:.05,rates:[30,60,120]},
  'walk-run band 2.8':{at:()=>({yaw:.3,speed:2.8}),slip:.03,median:.05,rates:[30,60,120]},
  'speeding up to a run':{at:t=>({yaw:0,speed:t<2?1.1:t<2.5?1.1+(t-2)/.5*3.1:4.2}),slip:.1,median:.05,rates:[30,60,120]},
 };
 for(const [name,c] of Object.entries(cases))for(const seed of SEEDS.slice(0,4))for(const hz of c.rates){
  const {m,r}=probe(seed,hz,c.at),good=m.look.drag>0?r.feet.right:r.feet.left,label=`${name}, seed ${seed}, ${hz} Hz`;
  assert.ok(good.plantedFrames>0,label);assert.ok(good.maxSlip<c.slip,`${label}: the good foot slips ${good.maxSlip.toFixed(3)} m`);
  for(const f of [r.feet.left,r.feet.right])assert.ok(f.medianPlantedSpeed<c.median,`${label}: planted foot moves ${f.medianPlantedSpeed.toFixed(3)} m/s`);
 }
 // After a speed change the same timestamp gives the same stride at 30 and 120 Hz (trapezoidal phase steps).
 for(const seed of SEEDS.slice(0,4)){
  const at=(hz:number)=>{const m=createSirenMinion(seed);for(let i=0;i<=3*hz;i++){const t=i/hz,sp=t<1?1.1:t<1.5?1.1+(t-1)/.5*3.1:4.2;animateSirenMinion(m,{yaw:0,vel:{x:0,z:sp}},t);}m.rig.group.updateMatrixWorld(true);return [m.rig.leftFoot,m.rig.rightFoot,m.rig.leftHand,m.rig.rightHand].map(b=>b.getWorldPosition(new T.Vector3()));};
  const a=at(30),b=at(120);a.forEach((p,i)=>assert.ok(p.distanceTo(b[i])<.01,`seed ${seed} joint ${i}: ${p.distanceTo(b[i]).toFixed(3)} m apart at 30 vs 120 Hz`));
 }
});

test('a fallen body and its weapon scrap rest on the floor instead of sinking through it', ()=>{
 const box=new T.Box3(),lowest=(m:SirenMinion,scrap:boolean)=>{let y=Infinity;m.rig.group.updateMatrixWorld(true);
  m.rig.group.traverse(o=>{if(!(o instanceof T.Mesh)||o.name==='siren-minion-fx'||o.name==='siren-minion-tongue'||(o.name==='siren-minion-scrap')!==scrap)return;
   let shown=o.visible;for(let p=o.parent;p&&shown;p=p.parent)shown=p.visible;const part=m.parts.find(p=>p.mesh===o);
   // Parts burning away shrink about their own centres; judge them while they keep most of their size.
   if(!shown||(part&&o.scale.x<part.scale.x*.5))return;box.setFromObject(o);y=Math.min(y,box.min.y);});return y;};
 for(const seed of SEEDS.slice(0,8)){
  const m=createSirenMinion(seed);m.rig.group.position.set(0,-m.floor,0);const walk:SirenMinionView={yaw:0,vel:{x:0,z:1.1}};
  for(let t=0;t<2;t+=1/60)animateSirenMinion(m,walk,t);
  for(let d=0;d<2.2;d+=1/30){animateSirenMinion(m,{...walk,deathAt:2},2+d);
   const body=lowest(m,false);assert.ok(body>-.05,`seed ${seed} ${d.toFixed(2)} s into the death: the body is ${(-body).toFixed(3)} m under the floor`);
   if(m.scrap?.visible){const y=lowest(m,true);assert.ok(y>-.13,`seed ${seed} ${d.toFixed(2)} s into the death: the scrap is ${(-y).toFixed(3)} m under the floor`);}}
  // Alive, a scrap drags along the floor rather than through it.
  if(m.scrap)for(const view of [{yaw:0,vel:{x:0,z:0}},walk,{yaw:0,vel:{x:0,z:.6},attackAt:5,attack:'club' as const},{yaw:0,vel:{x:0,z:0},attackAt:5,attack:'smash' as const}])
   for(let t=4;t<6;t+=1/30){animateSirenMinion(m,view,t);const y=lowest(m,true);assert.ok(y>-.06,`seed ${seed} alive ${JSON.stringify(view)} at ${t.toFixed(2)}: the scrap is ${(-y).toFixed(3)} m under the floor`);}
 }
});

test('a full prewarm, a few milliseconds at a time, caches every layout a seed can need', ()=>{
 let slices=0;while(!prewarmSirenMinions(5))slices++;assert.ok(slices>10,'the prewarm is split across frames');
 const before=corruptedLayoutCount();for(let i=0;i<120;i++)createSirenMinion((i*0x9e3779b1+12345)>>>0);
 assert.equal(corruptedLayoutCount(),before,'new seeds build no layouts after the prewarm');assert.equal(prewarmSirenMinions(),true);
});
