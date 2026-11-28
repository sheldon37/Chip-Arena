// Status-effect and team-band parity samples. These run the pinned source StatusRig (client/status-effects.ts) and
// teamArmor (client/team-identity.ts) themselves, not the native port. Run after export_models.mjs has
// verified/transpiled the original source modules into <stage> (node_modules linked there):
//   node tools/export_status_fixtures.mjs <stage>
import fs from 'node:fs';import path from 'node:path';import {createRequire} from 'node:module';import {pathToFileURL,fileURLToPath} from 'node:url';
const stage=path.resolve(process.argv[2]),root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),req=createRequire(path.join(stage,'package.json'));
const T=await import(pathToFileURL(req.resolve('three'))),load=p=>import(pathToFileURL(path.join(stage,'src',p+'.js')));
const {StatusRig,STATUS_VFX}=await load('client/status-effects'),{teamArmor}=await load('client/team-identity'),{TEAMS}=await load('shared/teams');
const round=v=>Math.round(v*1e6)/1e6,hex=c=>c.getHex();
const geometry=g=>({positions:Array.from(g.attributes.position.array,round),index:g.index?Array.from(g.index.array):null});
// One rig per mode, reused across samples as the renderer does (update() rewrites every child it shows).
const rigs={world:new StatusRig(false),first:new StatusRig(true)};
const meshes=r=>{const out=[];r.root.traverse(o=>{if(o.isMesh)out.push(o);});return out;};
const shown=(o,r)=>{for(let a=o;a;a=a.parent){if(!a.visible)return false;if(a===r.root)return true;}return true;};
const kind=(o,r)=>r.flames.includes(o)?'flame':r.flames.includes(o.parent)?'core':r.embers.includes(o)?'ember':r.bubbles.includes(o.parent)?['bubble','rim','glint'][o.parent.children.indexOf(o)]:o.parent===r.slow?'slow':o.parent===r.mark?'mark':'?';
const first=rigs.world,materials={},describe=m=>({color:hex(m.color),opacity:m.opacity,transparent:m.transparent,depthWrite:m.depthWrite,doubleSide:m.side===T.DoubleSide,toneMapped:m.toneMapped,fog:m.fog});
for(const o of meshes(first))materials[kind(o,first)]=describe(o.material);
first.update([],0,new T.PerspectiveCamera(),1,true,{slowed:{factor:.4,until:1,kind:'shackles'}});materials.shackle=describe(first.slow.children[0].material);
const camera=new T.PerspectiveCamera(60,16/9,.05,500);camera.position.set(-3.1,2.4,5.2);camera.lookAt(2.3,.6,-1.7);camera.updateMatrixWorld(true);
const samples=[];
const fire=[{element:'fire',dps:4,remaining:1.2}],poison=[{element:'poison',dps:2,remaining:3}],both=[...fire,...poison];
function sample(mode,quality,time,statuses,active,slowed,mark){
 const r=rigs[mode];r.root.position.set(mode==='world'?2.3:0,mode==='world'?.9:0,mode==='world'?-1.7:0);
 r.update(statuses,time,camera,quality,active,{slowed,mark});r.root.updateMatrixWorld(true);
 samples.push({mode,quality,time,active,fire:statuses.some(s=>s.element==='fire'&&s.remaining>0),poison:statuses.some(s=>s.element==='poison'&&s.remaining>0),slowed:slowed??null,mark:mark??null,root:r.root.position.toArray(),
  meshes:meshes(r).map(o=>{const v=shown(o,r);return {kind:kind(o,r),visible:v,color:hex(o.material.color),matrix:v?o.matrixWorld.elements.map(round):null};})});
}
for(const time of [0,.37,1.234,2.9,7.77]){
 const unlocked={born:time-.3,lockAt:time+.5,until:time+.7},locked={born:time-.9,lockAt:time-.1,until:time+.1};
 sample('world',1,time,both,true,{factor:.6,until:time+1,kind:'tidal-wave'},unlocked);
 sample('world',1,time,both,true,{factor:.4,until:time+.5,kind:'shackles'},locked);
 sample('world',.3,time,both,true,{factor:.6,until:time+1,kind:'add-slow'},undefined);
 sample('first',1,time,both,true,{factor:.4,until:time+.5,kind:'shackles'},unlocked);
 sample('first',.3,time,both,true,undefined,undefined);
}
for(const t of [.6,1.81,3.3])for(const n of [0,1,2,3,4,5,6,7]){const time=t+n*.0137;sample('world',1,time,fire,true,undefined,{born:time-.45,lockAt:time+n*.01-.04,until:time+.2});}
sample('world',1,4.2,poison,true,{factor:.6,until:4.1,kind:'tidal-wave'},{born:3,lockAt:3.8,until:4.1});
sample('world',1,4.2,both,false,{factor:.6,until:5,kind:'tidal-wave'},{born:4,lockAt:4.8,until:5});
sample('world',1,4.2,[{element:'fire',dps:4,remaining:0}],true,undefined,undefined);
sample('world',1,5.5,[],true,undefined,{born:5,lockAt:undefined,until:6});
// teamArmor needs only the four parent joints of a CharacterRig.
const bands=[];const rig={group:new T.Group(),chest:new T.Group(),spine:new T.Group(),leftArm:new T.Group(),rightArm:new T.Group()};for(const k of ['chest','spine','leftArm','rightArm'])rig.group.add(rig[k]);
for(const team of [...Object.keys(TEAMS),undefined]){teamArmor(rig,team);for(const k of ['chest','spine','leftArm','rightArm'])for(const o of rig[k].children)if(o.name==='team-identity')bands.push({team:team??null,parent:k,visible:o.visible,color:hex(o.material.color),toneMapped:o.material.toneMapped,position:o.position.toArray().map(round),scale:o.scale.toArray().map(round)});}
const r=rigs.world,flame=r.flames[0],bubble=r.bubbles[0];
const out={source:'05950eaef80ff2483a9c44d898cad98be1a76bbc',vfx:STATUS_VFX,teams:Object.values(TEAMS).map(t=>parseInt(t.color.slice(1),16)),materials,
 camera:{position:camera.position.toArray().map(round),quaternion:camera.quaternion.toArray().map(round)},
 geometry:{flame:geometry(flame.geometry),ember:geometry(r.embers[0].geometry),bubble:geometry(bubble.children[0].geometry),rim:geometry(bubble.children[1].geometry),glint:geometry(bubble.children[2].geometry)},
 samples,bands};
fs.writeFileSync(path.join(root,'Tests/status-parity.json'),JSON.stringify(out)+'\n');console.log('Exported',samples.length,'status samples and',bands.length,'team bands');
