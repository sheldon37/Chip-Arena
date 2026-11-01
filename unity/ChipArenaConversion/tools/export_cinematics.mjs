// Source-authored cosmetic scenes are sampled offline. Runtime is only C# meshes and transform tracks.
// Usage: node tools/export_cinematics.mjs <pinned source> <temporary verified module stage>
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import {gzipSync} from 'node:zlib';
import {exportModel,models,textures,T,load,bones,root,inventory} from './export_models.mjs';
import * as SirenDesign from './siren_design.mjs';import * as SirenSmile from './siren_smile.mjs';
const {sirenParts}=await load('siren/model'),sirenMaterials=await load('siren/materials'),{SIREN_INTRO}=await import(pathToFileURL(path.join(process.argv[3],'src/shared/siren-contract.js')));
const {createCharacterRig}=await load('character-rig'),{clownLove}=await load('clown-love'),{bassVan}=await load('bass-van'),{cinematic}=await load('kill-cinematics'),{PlayerIntros}=await load('player-intros'),{ragingDemon}=await load('raging-demon'),{sirenCrush,sirenDissipate}=await load('siren/kills');
const fps=30,clips=[];const round=n=>Number(n.toFixed(5));
let seed=9137;Math.random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
const pilot={id:'native',name:'Pilot',spec:'pistol',hp:0,maxHp:100,color:'#70e1d0',developer:true,yaw:0,pitch:0,pos:{x:0,y:0,z:0},vel:{x:0,y:0,z:0},chips:{},cosmetics:{hat:'none',bodyColor:'#e5e7eb',intro:'neon-drop',overrideWeaponIntro:true},blockTime:0,blockCd:0};
const death={id:'fixture',at:0,pos:{x:0,y:.865,z:0},yaw:0,velocity:{x:0,y:0,z:0},direction:{x:0,y:0,z:1},seed:17,force:0};
// An instanced mesh becomes one sampled mesh per instance, its instance colour on that mesh's colour track. particleMaterial
// (the Siren's FlameField: black flame tongues, violet tips, embers) is a ShaderMaterial coloured only per instance and written
// to the screen unencoded, so its slots get a colour (else the bank's 0xaea4c7 fallback, lit lavender) holding the instance
// colour as a display value: Unity's linear colour is that value decoded from sRGB.
// A node marked userData.bakeStatic (the Siren's Unity-finish dressing, tools/siren_design.mjs) only becomes its own track
// when its transform moves: a still one batches on the joint track above it and shares that track's opacity.
// `head` (the dev-siren stage's rig head) is recorded as the clip's head track: the entrance face is drawn on it natively.
function sample(id,scene,duration,tick,victimGroup=null,exclude=null,dropped=null,head=null){
 const frames=Math.ceil(duration*fps)+1,tracks=new Map(),meshes=new Set(),prototype=createCharacterRig('#70e1d0'),victimNames=bones.map(b=>prototype[b].name);let victim=[];const sampledInstances=new Set(),lightTracks=new Map(),environment=[];
 if(victimGroup)victim=victimNames.map(n=>victimGroup.getObjectByName(n));
 for(let frame=0;frame<frames;frame++){
  const time=frame*duration/(frames-1);for(const inst of sampledInstances)for(const child of inst.userData.sampleChildren??[])child.removeFromParent();tick(time);
  const instances=[];scene.traverse(o=>{if(o.isInstancedMesh)instances.push(o);});for(const inst of instances){sampledInstances.add(inst);inst.userData.sampleChildren??=[];for(let i=0;i<inst.count;i++){let child=inst.userData.sampleChildren[i];if(!child){const mat=(Array.isArray(inst.material)?inst.material[0]:inst.material).clone();if(inst.instanceColor&&!mat.color&&mat.isShaderMaterial){mat.color=new T.Color(1,1,1);mat.userData.rawInstanceColor=true;}child=new T.Mesh(inst.geometry,mat);child.name='sample-instance-'+i;child.matrixAutoUpdate=false;inst.add(child);inst.userData.sampleChildren[i]=child;}if(child.parent!==inst)inst.add(child);inst.getMatrixAt(i,child.matrix);child.visible=true;child.material.opacity=(Array.isArray(inst.material)?inst.material[0]:inst.material).opacity;if(inst.instanceColor&&child.material.color){inst.getColorAt(i,child.material.color);child.material.color.multiply(inst.material.color??new T.Color(1,1,1));if(child.material.userData.rawInstanceColor)child.material.color.convertSRGBToLinear();}}for(let i=inst.count;i<inst.userData.sampleChildren.length;i++)inst.userData.sampleChildren[i].visible=false;}
  scene.updateMatrixWorld(true);
  const bg=scene.background?.isColor?scene.background:new T.Color(0x040611),fog=scene.fog;environment.push(...bg.toArray(),...(fog?.color??bg).toArray(),fog?.near??9,fog?.far??26);
  scene.traverse(o=>{if(!o.isLight)return;let visible=true;for(let a=o;a;a=a.parent)visible&&=a.visible;let light=lightTracks.get(o);if(!light){light={kind:o.isHemisphereLight?'hemisphere':o.isAmbientLight?'ambient':o.isDirectionalLight?'directional':'point',values:[]};lightTracks.set(o,light);}const pos=o.getWorldPosition(new T.Vector3());light.values.push(...pos.toArray(),...o.color.toArray(),visible?o.intensity:0,o.distance??0,...(o.groundColor??o.color).toArray());});
  const visit=(o,visible)=>{if(o===exclude)return;visible=visible&&o.visible;let node=tracks.get(o);if(!node){node={object:o,data:[],local:null,dynamic:false,color:null,dynamicColor:false,visible:false,index:tracks.size};tracks.set(o,node);}const mat=Array.isArray(o.material)?o.material[0]:o.material,alpha=visible?(mat?.opacity??1):0,local=o.matrix.elements.map(round).join(',')+':'+(o.userData.bakeStatic?'':round(alpha));const color=(mat?.color??new T.Color(1,1,1)).toArray().map(round),colorKey=color.join(',');if(node.color!==null&&node.color!==colorKey){node.dynamicColor=true;node.dynamic=true;}node.color=colorKey;if(node.local!==null&&node.local!==local)node.dynamic=true;node.local=local;node.visible||=visible&&alpha>0;node.data[frame]=[...o.matrixWorld.elements.map(round),round(alpha),...color];if(o.isMesh&&!o.isInstancedMesh&&visible)meshes.add(o);for(const c of o.children)visit(c,visible);};visit(scene,true);
 }
 // Animated ancestors batch all rigid child meshes; changing-opacity meshes own their track.
 const anchors=[scene,...[...tracks.values()].filter(n=>n.object!==scene&&n.dynamic&&n.visible).map(n=>n.object)];for(const bone of victim)if(bone&&!anchors.includes(bone))anchors.push(bone);
 const victimSet=new Set();if(victimGroup)victimGroup.traverse(o=>victimSet.add(o));if(dropped)dropped.traverse(o=>victimSet.add(o));if(dropped&&!anchors.includes(dropped))anchors.push(dropped);
 // Meshes that left the scene are sampled with their own transform and hidden outside their lifetime.
 for(const mesh of meshes)if(!scene.getObjectById(mesh.id)){scene.add(mesh);if(!anchors.includes(mesh))anchors.push(mesh);}
 const values=[];for(const anchor of anchors){const n=tracks.get(anchor);const rows=n?.data??[];for(let i=0;i<frames;i++){const row=rows[i]??[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,0,1,1,1];if(!n?.dynamicColor)row.splice(17,3,1,1,1);values.push(...row);}if(n?.dynamicColor&&anchor.material?.color)anchor.material.color.setRGB(1,1,1);}
 exportModel(id,scene,true,o=>meshes.has(o)&&!victimSet.has(o),anchors,true);
 if(head&&anchors.indexOf(head)<1)throw Error('No head track in '+id);
 clips.push({key:id,duration,frames,tracks:anchors.length,values,victim:victim.map(b=>anchors.indexOf(b)),dropped:dropped?anchors.indexOf(dropped):-1,...(head?{head:anchors.indexOf(head)}:{}),lights:[...lightTracks.values()],environment});console.log('Scene',id,anchors.length,'tracks',meshes.size,'meshes');
}
// The Siren's own death (siren/kills.ts sirenDissipate) builds a fresh source Siren inside the effect: it wears the Unity finish
// too (tools/siren_design.mjs: the finer grin, the longer talons, the stratified flame and the whole couture), so the dressing
// does not vanish on the frame she dies. It burns away with the body: sirenDissipate hides each source mesh once its front
// (-1 + 2.4 smoothstep((age - .5) / 1.6) m above the death point) passes the height the mesh had when the effect was built;
// each couture mesh is hidden by the same rule (the train's tail, which hangs below the front's start in the unposed rig, at the
// start), so it keeps its own track (bakeStatic off). The dressing's three.js uuids come
// from the seeded stream, which is put back so the effect's flame pool keeps its stream. Every Object3D the dressing makes
// moves the three.js id sequence of the scenes sampled after it, and the weapon entrances' bakes follow that sequence, so the
// dissipate is baked as the source has it in its place and baked again dressed after the last scene, from the same seed,
// replacing the first bake in place (dressedDissipate).
function dressDissipate(scene,d){
 const kept=seed,root=scene.getObjectByName('siren-dissipate'),group=root?.children[0],parts=group&&sirenParts({group});if(!parts)throw Error('No Siren in the dissipate effect');
 const prototype=createCharacterRig('#70e1d0'),rig={group};for(const b of bones){rig[b]=group.getObjectByName(prototype[b].name);if(!rig[b])throw Error('No '+b+' in the dissipate rig');}
 SirenDesign.finish(T,parts,{grin:true,talons:true,twist:true});const dressing=SirenDesign.dress(T,rig,parts,sirenMaterials);seed=kept;
 root.updateMatrixWorld(true);const burn=[],at=new T.Vector3(),sm=x=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);};
 for(const o of dressing.objects.values()){o.userData.bakeStatic=false;if(o.isMesh){o.getWorldPosition(at);burn.push({o,y:Math.max(-1,at.y-d.pos.y)});}}
 return age=>{dressing.update({time:age,clock:age,lean:0,flare:0,squint:0});const front=-1+2.4*sm((age-.5)/1.6);for(const e of burn)e.o.visible=e.y>=front;};
}
let dissipateSeed=null;
function dressedDissipate(){
 const after=seed;seed=dissipateSeed;const scene=new T.Scene(),original=createCharacterRig('#70e1d0','#e5e7eb',true),effect=sirenDissipate(scene,original.group,{...pilot},death,1,2000,{options:{gameMode:'ffa'}}),burn=dressDissipate(scene,death);
 sample('death-siren-dissipate',scene,5,t=>{effect.update(t);burn(t);});effect.dispose();seed=after;
 for(const [list,key] of [[models,'id'],[clips,'key']]){const first=list.findIndex(o=>o[key]==='death-siren-dissipate');if(first<0||first===list.length-1||list[list.length-1][key]!=='death-siren-dissipate')throw Error('dissipate bake order');list[first]=list.pop();}
}
for(const style of ['dev-clown','dev-clown-love','dev-bass-van','confetti','ascension','shrink','launch','ghost','dev-raging-demon','siren-crush','siren-dissipate']){
 if(style==='siren-dissipate')dissipateSeed=seed;
 const scene=new T.Scene(),original=createCharacterRig('#70e1d0','#e5e7eb',true),p={...pilot},effect=(style==='dev-raging-demon'?ragingDemon:style==='siren-crush'?sirenCrush:style==='siren-dissipate'?sirenDissipate:style==='dev-clown-love'?clownLove:style==='dev-bass-van'?bassVan:cinematic(style))(scene,original.group,p,death,1,2000,{options:{gameMode:'ffa'}});
 const victim=style==='siren-dissipate'?null:style==='dev-clown-love'?scene.getObjectByName('love-partner'):style==='dev-bass-van'?scene.getObjectByName('bass-passenger'):scene.children[0].children[0];const dropped=['dev-clown','confetti','ascension','shrink','launch','ghost'].includes(style)?scene.children[0].children[1]:null;sample('death-'+style,scene,5,t=>effect.update(t),victim,null,dropped);effect.dispose();
}
const styles=['neon-drop','vector-slide','signal-spin','arc-salute','pulse-wave',...['sword','daggers','shield','greatsword','pistol','rifle','machinegun','fire','water','lightning','darkness'].map(s=>'weapon-'+s),'dev-emperor','dev-ansatsuken','dev-matrix','dev-siren'];
// The dev-siren cell's Siren wears the Unity finish (tools/siren_design.mjs) in its chest-up frame: the collar, pauldrons,
// bodice, girdle, gauntlets, claw rings, the sculpted mask and the turning nimbus (the gown and train sit below the frame;
// the cheeks are drawn natively with the face), the finer grin, the longer talons and the stratified flame.
// Its glow lines keep their own tracks, dimmed through the blackout and kindled at the smile (SirenDesign.entranceGlow, on the
// intro's own cue times). The face is not baked: the source's eyes (one brow cut held for the whole bake) and its x-stretched
// grin are hidden, and Core/NativeSirenSmileFace.cs draws the stare and the acted smile on the recorded head track at the
// presentation clock; the bake carries the smile's body layer instead of the source's own smile dip (SirenSmile.stagePose).
const sirenStageGroups=['collar','pauldron','bodice','girdle','gauntlet','rings','nimbus','face'],sirenCue=name=>SIREN_INTRO.cues.find(c=>c.cue===name).time;
const sirenCues={blackout:sirenCue('blackout'),smile:sirenCue('smile'),name:sirenCue('name')};
for(const style of styles){const stage=new PlayerIntros(),p={...pilot,hp:100,cosmetics:{...pilot.cosmetics,intro:style}},state={generation:1,setNumber:1,round:1,time:0,phase:'intro',intro:{started:0,players:['native']},players:[p],options:{gameMode:'ffa'},events:[],minions:[],projectiles:[]};const duration=style==='dev-siren'?9.0678:style==='dev-emperor'?7.8:style==='dev-matrix'?8:style==='dev-ansatsuken'?7.2:4.8;stage.update(state,16/9);
 // The dressing's three.js uuids come from the seeded stream, which is put back so the cell's flame pool keeps its stream.
 let dressing=null,siren=null;if(style==='dev-siren'){const parts=siren=sirenParts(stage.rig),kept=seed;SirenDesign.finish(T,parts,{grin:true,talons:true,twist:true});dressing=SirenDesign.dress(T,stage.rig,parts,sirenMaterials,{groups:sirenStageGroups,glowTracks:true});dressing.update({time:0,clock:0,c:0});seed=kept;}
 sample('stage-'+style,stage.scene,duration,t=>{state.time=t;stage.update(state,16/9);if(siren)SirenSmile.stagePose(stage.rig,siren,t,sirenCues);dressing?.update({time:t,clock:t,lean:0,flare:0,c:0,glow:SirenDesign.entranceGlow(t,sirenCues)});},null,style==='dev-siren'?null:stage.rig.group,null,siren?stage.rig.head:null);stage.dispose();}
dressedDissipate();
function envelope(value){return JSON.stringify({schema:1,commit:inventory.sourceCommit,data:gzipSync(JSON.stringify(value),{level:9}).toString('base64')})+'\n';}
fs.writeFileSync(path.join(root,'Assets/Conversion/Resources/SourceModels.json'),envelope({models,textures}));fs.writeFileSync(path.join(root,'Assets/Conversion/Resources/SceneMotion.json'),envelope({clips}));
fs.writeFileSync(path.join(root,'tools/cinematic-export.json'),JSON.stringify({commit:inventory.sourceCommit,fps,clips:clips.map(({values,lights,environment,...c})=>({...c,lights:lights.length})),modelCount:models.length},null,2)+'\n');console.log('Exported',clips.length,'scenes',models.length,'models');
