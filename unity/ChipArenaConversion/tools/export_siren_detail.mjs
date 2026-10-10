// Run export_models.mjs first; this dresses its verified, transpiled source Siren (client/siren/model.ts buildSiren and
// applySirenLook) with the Unity finish of tools/siren_design.mjs as real three.js meshes and writes what
// Tests/SirenDetailChecks.cs holds Core/NativeSirenLook.cs + Core/NativeSirenDetail.cs to (Tests/siren-detail-parity.json):
//  - every detail piece's id, group, joint and tone, the tone colours and strengths, the talon and seam constants;
//  - the stratified flame's yaw and girth per tongue step, and the finer grin's two rows (as tools/export_models.mjs bakes
//    them into the bank's siren-teeth parts);
//  - for scripted look states, every piece's transform relative to its joint (the source's 136 with the stratified flame,
//    then the 260 detail pieces) and its visibility, from the dressed rig's own scene graph.
// Usage: node tools/export_siren_detail.mjs <temporary output of export_models.mjs>
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL,fileURLToPath} from 'node:url';
import * as D from './siren_design.mjs';
let exportSeed=9137;Math.random=()=>{exportSeed=(Math.imul(exportSeed,1664525)+1013904223)>>>0;return exportSeed/4294967296;};
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),stage=path.resolve(process.argv[2]),load=p=>import(pathToFileURL(path.join(stage,'src/client',p+'.js')));
const commit='05950eaef80ff2483a9c44d898cad98be1a76bbc';
const T=await import(pathToFileURL(path.join(stage,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('character-rig'),{buildSiren,applySirenLook}=await load('siren/model'),M=await load('siren/materials');
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const r9=x=>Number(x.toPrecision(9));
const rig=createCharacterRig('#70e1d0'),parts=buildSiren(rig);D.finish(T,parts,{grin:true,talons:true,twist:true});const design=D.dress(T,rig,parts,M);
// The source pieces in Core/NativeSirenLook.cs order, then the detail pieces in tools/siren_design.mjs pieces() order.
const list=[];const add=o=>{let joint=-1,chain=[];for(let q=o;q;q=q.parent){joint=bones.findIndex(b=>rig[b]===q);if(joint>=0)break;chain.push(q);}if(joint<0)throw Error('Unjointed piece '+o.name);list.push({node:o,joint,chain});};
parts.eyes.forEach(add);parts.eyeHalos.forEach(add);add(parts.grinUpper);add(parts.grinLower);parts.claws.forEach(add);parts.orbs.forEach(add);parts.orbHalos.forEach(add);parts.wisps.forEach(add);
for(const t of parts.tongues)t.group.children.forEach(add);if(list.length!==D.SOURCE_PIECES)throw Error('source pieces '+list.length);
const detail=D.pieces();for(const d of detail)add(design.objects.get(d.id));
const local=p=>{const m=new T.Matrix4();for(const n of p.chain){n.updateMatrix();m.premultiply(n.matrix);}return m;};
const rows=m=>{const e=m.elements;return [e[0],e[4],e[8],e[12],e[1],e[5],e[9],e[13],e[2],e[6],e[10],e[14]].map(r9);};
const shown=p=>{for(const n of [p.node,...p.chain])if(!n.visible)return false;return true;};
const lin=hex=>new T.Color(hex).toArray().map(r9);
const out={source:commit,count:list.length,first:D.SOURCE_PIECES,boxes:detail.filter(d=>d.tone!=='sprite').length,
 seamWidth:D.SW,talons:D.TALONS,nimbus:{y:D.NIMBUS.y,z:D.NIMBUS.z,tilt:D.NIMBUS.tilt,r:D.NIMBUS.r,spin:D.NIMBUS.spin},cheek:D.CHEEK,
 tones:{ink:['plate','deep','sheen','ash'].map(k=>({tone:k,hex:D.TONES[k].ink,linear:lin(D.TONES[k].ink),rim:D.TONES[k].rim,flow:D.TONES[k].flow,emissive:D.TONES[k].emissive??[0,0,0]})),
  glow:['seam','filament'].map(k=>({tone:k,hex:D.TONES[k].glow,intensity:D.TONES[k].intensity,linear:D.glowLinear(T,k).toArray().map(r9)})),sprite:{hex:D.CRACK_COLOR,opacity:D.CRACK_OPACITY}},
 pieces:detail.map((d,i)=>({id:d.id,group:d.group,joint:list[D.SOURCE_PIECES+i].joint,tone:D.TONE_ORDER.indexOf(d.tone),part:d.tone==='sprite'?null:D.PARTS[d.tone],moves:!!d.anim||!!list[D.SOURCE_PIECES+i].chain.some(n=>/^couture-(nimbus|train-\d|cheek-[lr])$/.test(n.name))||d.bone.endsWith('Claws')})),
 twist:parts.tongues.flatMap((t,n)=>t.group.children.map((c,i)=>{const w=D.TWIST(n,i);return [r9(w.yaw),r9(w.k)];})),
 grin:{upper:D.grinRow('upper').map(r9),lower:D.grinRow('lower').map(r9)},states:[]};
const states=[
 {ranged:0,open:0,squint:0,anger:.5,glow:1,time:0,lean:0,flare:0,clock:0},
 {ranged:0,open:.3,squint:.6,anger:.9,glow:1,time:10.37,lean:.4,flare:0,clock:10.37},
 {ranged:1,open:.2,squint:.1,anger:.7,glow:1,time:12.81,lean:-.3,flare:.5,clock:12.81},
 {ranged:.5,open:0,squint:0,anger:.5,glow:1,time:3.3,lean:0,flare:0,clock:3.3},
 {ranged:1,open:1,squint:1,anger:1.2,glow:0,time:57.25,lean:1,flare:1,clock:57.25},
 {ranged:.93,open:.05,squint:.8,anger:.4125,glow:1,time:1234.567,lean:-1,flare:.2,clock:1234.567},
 {ranged:0,open:.6,squint:0,anger:.0125,glow:.5,time:7.77,lean:.1,flare:0,clock:9.01},
 {ranged:.2,open:.9,squint:.45,anger:.95,glow:1,time:.5,lean:-.6,flare:.8,clock:.5},
 {ranged:0,open:.06,squint:1.4,anger:.9,glow:1,time:6.5,lean:.75,flare:0,clock:6.5},
 {ranged:0,open:0,squint:.25,anger:.6,glow:1,time:31.4,lean:.2,flare:.3,clock:30.1},
];
for(const s of states){Object.assign(parts.state,s);M.tickSirenClock(s.clock);applySirenLook(parts);design.update(s);rig.group.updateMatrixWorld(true);
 out.states.push({state:s,pieces:list.map(p=>rows(local(p))),shown:list.map(p=>shown(p)?1:0)});}
fs.writeFileSync(path.join(root,'Tests/siren-detail-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',detail.length,'detail pieces ('+out.boxes+' boxes);',out.states.length,'states of',list.length,'pieces');
