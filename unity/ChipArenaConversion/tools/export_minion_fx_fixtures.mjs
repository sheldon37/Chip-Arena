// Build-time only: run the pinned original client/siren/minions (model.ts createSirenMinion, motion.ts animateSirenMinion,
// fx.ts MinionFx, materials.ts) and record what Tests/MinionFxChecks.cs replays through Core/NativeMinionFx.cs: the six horde
// variants' faces, floors and tongue anchors (Core/NativeMinionFx.Generated.cs, written by tools/export_models.mjs), each
// tongue's position, turn and scale over idle, vent-climb and dying frames, MinionFx's cube matrices and colours for the
// emergence and the burn-away at full and reduced quality, and how many glow cells (GLOW_FLAG) each variant's visible parts
// carry per flicker class, which the bank must hold as emission codes. Math.random is the exporter's seeded stream (only
// three.js object ids draw from it; the minion look is hashed from its seed).
// Usage: node tools/export_minion_fx_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/siren/minions/fx.ts':'c4b5ecd1340ce4e81f7f36708208f92582f7119f','src/client/siren/minions/model.ts':'9cf152e7038676aa29163959c77f5efba74ec54b',
 'src/client/siren/minions/motion.ts':'5fb6ec6c8d07ba895c586bff39dcd93b82c3efd7','src/client/siren/minions/materials.ts':'2fac21c98844772252a352e2a0004729e6956d6a',
 'src/client/siren/minions/look.ts':'4cd063ed0c97719beab82ec4f7e82e501cc78cb4','src/client/siren/minions/layouts.ts':'a86071ae3e48848d8079cd24d11280c8f22fe67a'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
let stream=9137;Math.random=()=>{stream=(Math.imul(stream,1664525)+1013904223)>>>0;return stream/4294967296;};
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=f=>import(pathToFileURL(path.join(source,'src/client',f)).href);
const model=await load('siren/minions/model.ts'),motion=await load('siren/minions/motion.ts'),fxm=await load('siren/minions/fx.ts'),look=await load('siren/minions/look.ts'),materials=await load('siren/minions/materials.ts');
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const p9=v=>Number((+v).toPrecision(9)),arr=a=>Array.from(a,p9);
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const out={source:pinned,commit:'05950eaef80ff2483a9c44d898cad98be1a76bbc',glowFlag:materials.GLOW_FLAG,variants:[],tongueFrames:[],fx:[]};
const minions=[];
for(let v=0;v<6;v++){const m=model.createSirenMinion(100+v);minions.push(m);const B=bones.map(n=>m.rig[n]);
 // Glow cells on the parts the bank bakes (shown meshes; the tongues and the effect mesh leave the joint batches).
 const classes=[0,0,0];let cells=0;
 m.rig.group.traverse(o=>{if(!o.isInstancedMesh||!o.instanceColor||o.name==='siren-minion-tongue'||o.name==='siren-minion-fx')return;for(let q=o;q;q=q.parent)if(!q.visible)return;const u=o.material.minionUniforms;if(!u)return;const c=new T.Color();
  for(let k=0;k<o.count;k++){o.getColorAt(k,c);if(c.r<materials.GLOW_FLAG-.5)continue;cells++;const f=u.uFlicker.value;classes[f>.87?2:f>.6?1:0]++;}});
 out.variants.push({seed:100+v,face:m.look.face,floor:p9(m.floor),glow:{cells,classes},
  tongues:m.tongues.map((g,j)=>({joint:B.indexOf(g.mesh.parent),layout:j%2,pos:arr(g.mesh.position.toArray()),quat:arr(g.base.toArray()),height:p9(g.height),phase:p9(g.phase)}))});}
// Tongue frames: idle, a vent climb from t = 10 and a death at t = 20.
const frames=[];for(const v of [0,2,5]){for(const t of [3.1,4.42,7.77])frames.push({v,t});for(const e of [.3,.9,1.5,2.2])frames.push({v,t:10+e,entryAt:10,e});for(const d of [.5,1.0,1.8])frames.push({v,t:20+d,deathAt:20,d});}
for(const f of frames){const m=minions[f.v],view={yaw:0,vel:{x:0,z:0},hp:1,maxHp:1};if(f.entryAt!==undefined)view.entry={kind:'vent',at:f.entryAt,first:true,rimZ:motion.VENT_RIM_Z};if(f.deathAt!==undefined)view.deathAt=f.deathAt;
 motion.animateSirenMinion(m,view,f.t,1);
 out.tongueFrames.push({variant:f.v,time:f.t,e:f.e??null,d:f.d??null,tongues:m.tongues.map(g=>({pos:arr(g.mesh.position.toArray()),quat:arr(g.mesh.quaternion.toArray()),scale:arr(g.mesh.scale.toArray())}))});}
// MinionFx cubes.
const points=[[0,1.6,.1],[0,1.2,0],[0,.9,0],[.12,.5,.1],[-.12,.5,.05],[.3,1.1,0]];
for(const seed of [look.minionSeed('siren-add-7'),2654435761])for(const quality of [1,.3]){
 const floor=-.865,run=(e,d)=>{const fx=new fxm.MinionFx(seed,floor);fx.update(e,d,points.map(p=>new T.Vector3(...p)),quality);const n=fx.mesh.count,slots=[];
  // Shown slots only (a hidden slot holds the zero matrix), with their slot index.
  for(let i=0;i<n;i++){const mm=new T.Matrix4(),c=new T.Color();fx.mesh.getMatrixAt(i,mm);if(mm.elements.slice(0,15).every(x=>x===0))continue;fx.mesh.getColorAt(i,c);slots.push({i,m:arr(mm.elements),c:arr(c.toArray())});}
  out.fx.push({seed,quality,floor,e:e??null,d:d??null,points:points.flat(),visible:fx.mesh.visible,slots});};
 for(const e of [.05,.4,1.0,1.75,2.2,2.4])run(e,undefined);
 for(const d of [.2,.7,1.2,1.9,2.6,3.05,3.2])run(undefined,d);}
const file=path.join(root,'Tests/minion-fx-parity.json');fs.writeFileSync(file,JSON.stringify(out)+'\n');
console.log('wrote',path.relative(root,file),out.variants.length,'variants',out.tongueFrames.length,'tongue frames',out.fx.length,'fx runs');
