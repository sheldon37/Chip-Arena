// Run export_models.mjs first; this samples its verified, transpiled source (client/siren/model.ts applySirenLook and
// client/siren/motion.ts animateSirenRig) into what Core/NativeSirenLook.cs needs and what Tests/SirenLookChecks.cs checks:
//  - Assets/Conversion/Resources/SirenLook.json: per Siren motion clip (the same keys, timings and 60 fps sampling as
//    export_motion.mjs), the face targets of each frame before finishLook's smoothing (open, squint), so the runtime
//    smooths them with its own frame time exactly as the source does (the flare is a function of the move's age).
//  - Tests/siren-look-parity.json: every animated piece's transform relative to its joint after applySirenLook for scripted
//    states, the eye outlines of shapeEye, and source timelines (melee, ranged, a mode swap, damnation and the grab) of
//    animateSirenRig at 60 fps with the resulting look state, piece transforms and the Damnation ball.
// Usage: node tools/export_siren_look.mjs <temporary output of export_models.mjs>
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL,fileURLToPath} from 'node:url';import {gzipSync} from 'node:zlib';
let exportSeed=9137;Math.random=()=>{exportSeed=(Math.imul(exportSeed,1664525)+1013904223)>>>0;return exportSeed/4294967296;};
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),stage=path.resolve(process.argv[2]),load=p=>import(pathToFileURL(path.join(stage,'src/client',p+'.js')));
const commit='05950eaef80ff2483a9c44d898cad98be1a76bbc';
const {createCharacterRig}=await load('character-rig'),{animateSirenRig}=await load('siren/motion'),{buildSiren,applySirenLook,shapeEye}=await load('siren/model'),{tickSirenClock}=await load('siren/materials');
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const fps=60,dt=1/fps,k=1-Math.exp(-dt*18),r6=x=>Number(x.toFixed(6)),r9=x=>Number(x.toPrecision(9));
// export_motion.mjs: move [hit, duration] and the pilot view it samples.
const timings={swipe:[.12,.4],rend:[.42,1.05],slam:[.38,1],rush:[.14,.82],laugh:[2,3.25],hellfire:[.16,.42],damnation:[.55,1],eruption:[.3,.6],rain:[.6,1],mark:[2.4,2.9]};
const rangedMoves=['hellfire','damnation','eruption','rain','mark'];
const pilot={id:'native',spec:'siren',hp:100,grounded:true,vel:{x:0,y:0,z:0},pos:{x:0,y:.9,z:0},attackRate:1,lastAttack:-10,blockTime:0,guarding:false,chips:{},yaw:0,pitch:0};
const moveView=(kind,side,at=10)=>{const [hit,duration]=timings[kind];return {...pilot,siren:{mode:rangedMoves.includes(kind)?'ranged':'melee',modeAt:0,move:{id:kind,seq:1,at,hitAt:at+hit,endAt:at+hit+.44,until:at+duration,side}}};};
const grabView=(at=8)=>({...pilot,siren:{mode:'melee',modeAt:0,move:{id:'laugh',seq:1,at,hitAt:at+2,caught:'victim',caughtAt:at+2,crushAt:at+5.2,until:at+5.8,side:1}}});

// 1. Face tracks. finishLook smooths s += (target - s)·k with k = 1 - e^(-18·dt); each frame's target is recovered exactly
// from two consecutive full-precision states of that same step.
const clips=[];
function track(key,duration,loop,view){
 const rig=createCharacterRig('#70e1d0'),parts=buildSiren(rig),frames=Math.ceil(duration*fps)+1,values=[];let open=0,squint=0;
 for(let i=0;i<frames;i++){const time=10+duration*i/(frames-1);animateSirenRig(rig,view,{players:[]},time,dt,0);const s=parts.state;
  values.push(r6(open+(s.open-open)/k),r6(squint+(s.squint-squint)/k));open=s.open;squint=s.squint;}
 clips.push({key,duration,loop,frames,values});
}
for(const ranged of [false,true])track('siren-idle-'+(ranged?'ranged':'melee'),2,true,{...pilot,siren:{mode:ranged?'ranged':'melee',modeAt:0}});
for(const [kind,[,duration]] of Object.entries(timings))for(const side of [1,-1])track('siren-'+kind+'-'+side,duration,false,moveView(kind,side));
track('siren-grab',3.8,false,grabView());
fs.writeFileSync(path.join(root,'Assets/Conversion/Resources/SirenLook.json'),JSON.stringify({schema:1,commit,data:gzipSync(JSON.stringify({fps,clips}),{level:9}).toString('base64')})+'\n');

// 2. Pieces in Core/NativeSirenLook.cs order, each with its joint and its chain of nodes below that joint.
function pieces(rig,parts){
 const list=[];const add=o=>{let joint=-1,chain=[];for(let q=o;q;q=q.parent){joint=bones.findIndex(b=>rig[b]===q);if(joint>=0)break;chain.push(q);}if(joint<0)throw Error('Unjointed piece '+o.name);list.push({node:o,joint,chain});};
 parts.eyes.forEach(add);parts.eyeHalos.forEach(add);add(parts.grinUpper);add(parts.grinLower);parts.claws.forEach(add);parts.orbs.forEach(add);parts.orbHalos.forEach(add);parts.wisps.forEach(add);
 for(const t of parts.tongues)t.group.children.forEach(add);return list;
}
const local=p=>{const m=new (p.node.matrix.constructor)();for(const n of p.chain){n.updateMatrix();m.premultiply(n.matrix);}return m;};
// Row-major, the top three rows (the bottom row is 0 0 0 1).
const rows=m=>{const e=m.elements;return [e[0],e[4],e[8],e[12],e[1],e[5],e[9],e[13],e[2],e[6],e[10],e[14]].map(r9);};
const shown=p=>{for(const n of [p.node,...p.chain])if(!n.visible)return false;return true;};
const capture=(list,parts,only=null)=>({pieces:(only??list.map((_,i)=>i)).map(i=>rows(local(list[i]))),shown:list.map(p=>shown(p)?1:0),eyeHalo:r9(parts.eyeHalos[0].material.opacity),anger:r9(parts.state.angerShown)});

const out={source:commit,fps,smoothing:18,swap:.35,eyes:[],states:[],keys:[],timelines:[]};
// 3. shapeEye outlines (x, y per vertex; z is 0).
const T=await import(pathToFileURL(path.join(stage,'node_modules/three/build/three.module.js')));
for(const anger of [0,.0125,.25,.4,.425,.5,.8,1,1.3,-.2])for(const side of [1,-1]){const g=new T.BufferGeometry();g.setAttribute('position',new T.BufferAttribute(new Float32Array(40*9),3));shapeEye(g,side,anger);
 const p=g.getAttribute('position'),xy=[];for(let i=0;i<p.count;i++)xy.push(r9(p.getX(i)),r9(p.getY(i)));out.eyes.push({side,anger,xy});}
// 4. applySirenLook on scripted states.
{
 const rig=createCharacterRig('#70e1d0'),parts=buildSiren(rig),list=pieces(rig,parts);
 out.joints=list.map(p=>p.joint);
 const states=[
  {ranged:0,open:0,squint:0,anger:.5,glow:1,time:0,lean:0,flare:0,clock:0},
  {ranged:0,open:.3,squint:.6,anger:.9,glow:1,time:10.37,lean:.4,flare:0,clock:10.37},
  {ranged:1,open:.2,squint:.1,anger:.7,glow:1,time:12.81,lean:-.3,flare:.5,clock:12.81},
  {ranged:.5,open:0,squint:0,anger:.5,glow:1,time:3.3,lean:0,flare:0,clock:3.3},
  {ranged:.019,open:0,squint:0,anger:.5,glow:1,time:4.1,lean:0,flare:0,clock:4.1},
  {ranged:.021,open:0,squint:0,anger:.5,glow:1,time:4.1,lean:0,flare:0,clock:4.1},
  {ranged:1,open:1,squint:1,anger:1.2,glow:0,time:57.25,lean:1,flare:1,clock:57.25},
  {ranged:.93,open:.05,squint:.8,anger:.4125,glow:1,time:1234.567,lean:-1,flare:.2,clock:1234.567},
  {ranged:0,open:.6,squint:0,anger:.0125,glow:.5,time:7.77,lean:.1,flare:0,clock:9.01},
  {ranged:.2,open:.9,squint:.45,anger:.95,glow:1,time:.5,lean:-.6,flare:.8,clock:.5},
 ];
 for(const s of states){Object.assign(parts.state,s);tickSirenClock(s.clock);applySirenLook(parts);rig.group.updateMatrixWorld(true);out.states.push({state:s,...capture(list,parts)});}
}
// 5. Timelines of animateSirenRig at 60 fps (a fresh rig each): what the clip key/age, mode and clock give the runtime.
{
 const keep=[0,1,4,5,6,7,8,9,10,11,12,17,18,23,24,25,26,30,129,135];out.timelinePieces=keep;
 function timeline(name,start,frames,every,view){
  // Per frame, what the runtime reads from the replicated pose: the clip (key index), its age and length, ranged mode.
  const rig=createCharacterRig('#70e1d0'),parts=buildSiren(rig),list=pieces(rig,parts),samples=[],drive=[];
  for(let i=0;i<frames;i++){const time=start+i/fps,v=view(time,i);animateSirenRig(rig,v,{players:[]},time,dt,0);rig.group.updateMatrixWorld(true);
   const m=v.siren.move,s=parts.state,active=m&&time<m.until,key=active?(m.caught?'siren-grab':'siren-'+m.id+'-'+m.side):'siren-idle-'+v.siren.mode,at=active?(m.caught?m.caughtAt:m.at):0;
   if(!out.keys.includes(key))out.keys.push(key);drive.push(out.keys.indexOf(key),r9(time),r9(active?time-at:time),active?r9(m.until-at):0,v.siren.mode==='ranged'?1:0);
   // The Damnation ball and its halo, relative to the rig root (parts.root has no transform of its own).
   const charge=parts.charge.visible?[...rows(parts.charge.matrix),...rows(parts.chargeHalo.matrix)]:null;
   if(i%every===0||i===frames-1)samples.push({frame:i,look:{ranged:r9(s.ranged),open:r9(s.open),squint:r9(s.squint),anger:r9(s.anger),flare:r9(s.flare),lean:r9(s.lean),glow:r9(s.glow)},charge,...capture(list,parts,keep)});}
  out.timelines.push({name,frames,drive,samples});
 }
 const withMove=(kind,side)=>t=>{const v=moveView(kind,side);if(t<v.siren.move.at)v.siren.move=undefined;return v;};
 timeline('melee-rend',9.9,84,3,withMove('rend',1));
 timeline('ranged-hellfire',9.95,42,2,withMove('hellfire',-1));
 // The mode flips on frame 6 at exactly that frame's time, which is when the runtime sees the replicated flag change.
 timeline('swap-to-ranged',10,40,2,(t,i)=>({...pilot,siren:i<6?{mode:'melee',modeAt:0}:{mode:'ranged',modeAt:10+6/fps}}));
 timeline('ranged-damnation',9.95,72,3,withMove('damnation',1));
 timeline('grab',10,228,6,t=>grabView());
}
fs.writeFileSync(path.join(root,'Tests/siren-look-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',clips.length,'face tracks;',out.states.length,'states;',out.timelines.length,'timelines');
