// Build-time only: run the pinned original field telegraphs (client/chaos-visuals.ts: ground glow, cores, domes, the
// ally/enemy tint), the Siren floor telegraphs (client/siren/fx.ts: scorch decal, eruption warn ring and fill) and the
// ability aim preview (client/ability-preview.ts) and record what they draw for Tests/FieldChecks.cs.
// Owner cuts in the Unity presentation (2026-10-11 effects overhaul, Runtime/NativeFieldPresentation.cs), not applied here:
// the ground glow disc is not drawn at all ("that pancake ring effect is still there"), and the core cubes are drawn as the
// modelled chip bomb (NativeFieldRules.Bomb, same timing), the black hole's lit core and halo and orbit streaks, the preview
// motes as soft glows. Core still plans the source's disc and cubes (NativeFieldRules.GlowDisc, Cubes), which these
// fixtures pin, so they record the source unchanged.
// Usage: node tools/export_field_fixtures.mjs <source checkout at 05950ea with node_modules (three, esbuild)>
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import {fileURLToPath} from 'node:url';import {createRequire} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const files={'src/client/chaos-visuals.ts':'bb3764b08d006d0f8e9d7b249ccadc64a7826173','src/client/siren/fx.ts':'e4ffbcb23188e7a9fb733c75314f0a2229429e6d','src/client/siren/materials.ts':'37a6f92a012f3e4b6542f0008be352fd41a80de8','src/client/ability-preview.ts':'6067ac3564a83b7e872a67c33402686067c1fec7'};
for(const [file,blob] of Object.entries(files)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
// The modules import the shared game code, so they are bundled (esbuild from the source checkout) instead of stripped.
const esbuild=createRequire(path.join(source,'node_modules/'))('esbuild');
const entry=`export {ChaosVisuals,telegraphColor,TELEGRAPH,FIELD_GLOW_ALPHA} from './src/client/chaos-visuals.ts';export {SirenFx} from './src/client/siren/fx.ts';export {AbilityPreview} from './src/client/ability-preview.ts';export * as T from 'three';`;
const built=await esbuild.build({stdin:{contents:entry,resolveDir:source,loader:'ts'},bundle:true,write:false,platform:'node',format:'esm',nodePaths:[path.join(source,'node_modules')],logLevel:'warning',target:'es2022'});
const bundle=path.join(fs.mkdtempSync(path.join(os.tmpdir(),'field-fixtures-')),'bundle.mjs');fs.writeFileSync(bundle,built.outputFiles[0].text);
const {ChaosVisuals,telegraphColor,TELEGRAPH,FIELD_GLOW_ALPHA,SirenFx,AbilityPreview,T}=await import(bundle);
const r4=v=>Math.round(v*1e5)/1e5,p=new T.Vector3(),q=new T.Quaternion(),s=new T.Vector3(),m=new T.Matrix4();

// Snapshot pieces the visuals read: time, fields, players (owner/viewer and team), pve, options.gameMode; summons idle.
const snap=(time,fields,o={})=>({time,fields,players:o.players??[{id:'a',team:'red'},{id:'b',team:'blue'}],pve:o.pve,options:{gameMode:o.mode},events:[],minions:[],generation:0});
const role=d=>d.core?'core':d.disc!==undefined?'disc'+d.disc:d.orbit!==undefined?'orbit'+d.orbit:d.body?'body':d.fuse?'fuse':null;
function children(g){
 g.updateMatrixWorld(true);const out=[];
 for(const o of g.children){const name=o.name==='field-glow'?'glow':o.name==='field-dome'?'dome':role(o.userData)??'inner';o.matrixWorld.decompose(p,q,s);
  const mat=o.material;out.push([name,...[p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z].map(r4),mat.color.getHex(),r4(mat.opacity),o.visible?1:0]);}
 return out;
}
// Every field kind and fx tag the Unity match creates (chip fields, ability zones), at body-centred, floor and raised points.
const cases=[
 ['black-hole',null,5,3.5,.95],['black-hole','singularity',7.5,2.2,.95],['acid','acid-rain',3,4,.95],['fire','wildfire',2.5,3,0],['fire','meteor-strike',3.2,3,0],['frost','blizzard',3.5,3,.95],
 ['time','time-warp',4,4,.95],['time','freezer-block',5,3,1.8],['repulsion','repulsion-pad',3.5,3,.95],['repulsion','panic-button',6,4,.95],['heal','med-garden',2.5,4,.95],['storm','lightning-rod',4,3,.95],
 ['bomb',null,3,1,.95],['bomb','bubble-mine',3,1,.95],['bomb','sticky-bomb',3,1.2,1.3],['bomb','delayed-gratification',4,2.2,.95],['bomb','aftershock',3.5,.45,0],['bomb','revenge-mail',3,1,.95],
 ['bomb','artillery',3,.85,0],['bomb','thunderstep',2.5,.35,.95],['bomb','comet',3.5,.32,.95],['bomb','meteor-strike',4.5,.9,0],['bomb','orbital-mark',3,1,.95],['bomb','geyser-wall',3,.8,0],
 ['mine',null,2.5,6,.95],['mine','proximity-mail',2.5,6,.95],['mine','bread-crumbs',2.8,5,0],['mine','mirror-mine',2.3,6,.95],['implosion','implosion-bomb',4,.9,.95],['implosion',null,4,.9,2.4]];
const fields=[];
for(const [kind,fx,radius,duration,y] of cases){
 const born=.25,until=born+duration,times=[born,born+.05,born+.2,born+duration*.37,born+duration*.5,born+duration*.62,until-.45,until-.31,until-.2,until-.07,until-.013].filter((t,i,a)=>t>=born&&t<until&&a.indexOf(t)===i);
 // The local pilot 'a' is the viewer and the owner is 'b' (an enemy in free-for-all), so ability fields pick the enemy tint.
 const field={id:'f1',owner:'b',kind,pos:{x:1.5,y,z:-2},radius,born,until,arm:born+.6,damage:0,fx:fx??undefined};
 const cv=new ChaosVisuals(new T.Scene()),frames=[];
 for(const t of times){cv.update(snap(t,[field]),'a');frames.push({t:r4(t),children:children(cv.fields.get('f1'))});}
 fields.push({kind,fx,radius,born,until,pos:[1.5,y,-2],viewer:'enemy',frames});
}
// Geometry of the shared unit disc and dome, in world space for one field (1.5, .95, -2; R 3), with per-vertex alpha.
function geometry(kind,fx){
 const cv=new ChaosVisuals(new T.Scene()),f={id:'g',owner:'a',kind,pos:{x:1.5,y:.95,z:-2},radius:3,born:0,until:4,arm:0,damage:0,fx};cv.update(snap(.5,[f]),'a');
 const g=cv.fields.get('g');g.updateMatrixWorld(true);const out={};
 for(const o of g.children){if(o.name!=='field-glow'&&o.name!=='field-dome')continue;const geo=o.geometry,pos=geo.attributes.position,col=geo.attributes.color,world=[],alpha=[];
  for(let i=0;i<pos.count;i++){p.fromBufferAttribute(pos,i).applyMatrix4(o.matrixWorld);world.push(r4(p.x),r4(p.y),r4(p.z));alpha.push(r4((col?col.getW(i):1)*o.material.opacity));}
  out[o.name==='field-glow'?'disc':'dome']={world,alpha,index:Array.from(geo.index.array),transparent:o.material.transparent,depthWrite:o.material.depthWrite,side:o.material.side,toneMapped:o.material.toneMapped,blending:o.material.blending,renderOrder:o.renderOrder,vertexColors:o.material.vertexColors};}
 return out;
}
const glowGeometry=geometry('acid'),domeGeometry=geometry('time');
// telegraphColor truth table. Unity inputs per row: a local viewer exists; owner is the viewer; PvE; teams mode; same team.
const tint=[];
for(const [kind,fx] of [['bomb','orbital-mark'],['bomb','meteor-strike'],['fire','meteor-strike'],['bomb','geyser-wall'],['black-hole','singularity'],['bomb','artillery'],['bomb','thunderstep'],['bomb','comet'],['bomb','bubble-mine'],['black-hole',null],['mine','proximity-mail'],['time','time-warp'],['implosion','implosion-bomb'],['bomb',null],['heal','med-garden']])
 for(const [label,me,owner,pve,mode,ownerTeam,meTeam] of [['no viewer',undefined,'b',false,undefined,'red','red'],['self','a','a',false,undefined,'red','blue'],['enemy ffa','a','b',false,undefined,'red','red'],['pve','a','b',true,undefined,'red','blue'],['teams ally','a','b',false,'teams','red','red'],['teams enemy','a','b',false,'teams','red','blue'],['owner left','a','gone',false,'teams','red','red'],['owner left pve','a','gone',true,undefined,'red','red']]){
  const players=[{id:'a',team:meTeam},{id:'b',team:ownerTeam}],f={id:'t',owner,kind,pos:{x:0,y:0,z:0},radius:3,born:0,until:1,arm:0,damage:0,fx:fx??undefined};
  const color=telegraphColor(f,snap(0,[f],{players,pve:pve?{}:undefined,mode}),me);
  tint.push({kind,fx,label,viewer:me!==undefined,self:owner===me,ownerKnown:owner!=='gone',pve,teams:mode==='teams',sameTeam:owner!=='gone'&&ownerTeam===meTeam,color});
 }
// Siren floor telegraphs: scorch decals under fire patches, the eruption warning ring and fill.
const fx=new SirenFx(new T.Scene()),patches=[],eruptions=[];
for(const [kind,radius,at,until] of [['hellfire',1.1,.4,10.4],['trail',.85*1.4,.4,3.4],['scorch',1.6,.4,6.4]]){
 const f={id:'p-'+kind,owner:'s',kind,pos:{x:-1,y:.2,z:3},radius,at,until},frames=[];
 for(const t of [.4,.45,.5,.6,.64,.9,until-.9,until-.8,until-.5,until-.2,until-.1,until-.03]){fx['updatePatches']([f],t,1/60);const v=fx['patches'].get(f.id);v.decal.updateMatrixWorld(true);v.decal.matrixWorld.decompose(p,q,s);frames.push({t:r4(t),visible:v.decal.visible?1:0,pos:[r4(p.x),r4(p.y),r4(p.z)],scale:r4(s.x),rot:[r4(q.x),r4(q.y),r4(q.z),r4(q.w)]});}
 const mat=fx['patches'].get(f.id).decal.material;patches.push({kind,radius,at,until,pos:[-1,.2,3],color:mat.color.getHex(),opacity:mat.opacity,depthWrite:mat.depthWrite,polygonOffset:[mat.polygonOffsetFactor,mat.polygonOffsetUnits],renderOrder:fx['patches'].get(f.id).decal.renderOrder,frames});fx['updatePatches']([],until,1/60);
}
const flickerGlsl='float f=1.0-uFlicker*(0.5+0.5*sin(uTime*37.0+sin(uTime*11.0)*3.0))';
for(const [radius,at] of [[2.2,1],[2.2*1.6,3.25]]){
 const e={id:'e'+at,owner:'s',pos:{x:2,y:.1,z:-1},radius,at,burstAt:at+1,until:at+1.5},frames=[];
 for(const t of [at,at+.01,at+.2,at+.5,at+.77,at+.99,at+1,at+1.2,at+1.49]){fx['updateEruptions']([e],t,1/60);const v=fx['eruptions'].get(e.id),read=o=>{o.updateMatrixWorld(true);o.matrixWorld.decompose(p,q,s);return {visible:o.visible?1:0,pos:[r4(p.x),r4(p.y),r4(p.z)],scale:r4(s.x),rot:[r4(q.x),r4(q.y),r4(q.z),r4(q.w)]};};
  // The flicker is shader code (materials.ts glowMaterial); the GLSL is checked below and evaluated here with the same clock.
  frames.push({t:r4(t),ring:read(v.ring),fill:read(v.fill),flicker:r4(1-.15*(.5+.5*Math.sin(t*37+Math.sin(t*11)*3)))});}
 const ring=fx['eruptions'].get(e.id).ring,fill=fx['eruptions'].get(e.id).fill,ringGeo=ring.geometry.parameters,fillGeo=fill.geometry.parameters;
 if(!ring.material.fragmentShader.includes(flickerGlsl))throw Error('Siren glow flicker changed');
 eruptions.push({radius,at,burstAt:e.burstAt,until:e.until,pos:[2,.1,-1],ringGeometry:ringGeo,fillGeometry:fillGeo,ringColor:ring.material.uniforms.uColor.value.getHex(),ringOpacity:ring.material.uniforms.uOpacity.value,ringFlicker:ring.material.uniforms.uFlicker.value,fillColor:fill.material.uniforms.uColor.value.getHex(),fillOpacity:fill.material.uniforms.uOpacity.value,side:ring.material.side,frames});fx['updateEruptions']([],e.until,1/60);
}
// Ability aim preview motes (instanced cubes): positions and scales after update(circles, time).
const preview=[];
for(const [label,circles,t] of [['meteor',[{pos:{x:1,y:.1,z:-3},radius:4.5,clamped:false}],.3],['clamped',[{pos:{x:1,y:.1,z:-3},radius:4.5,clamped:true}],1.7],['small',[{pos:{x:0,y:0,z:0},radius:1.5,clamped:false}],2.25],['geyser',[-3,0,3].map(k=>({pos:{x:k,y:.4,z:5},radius:3,clamped:false})),.9],['singularity',[{pos:{x:-2,y:1,z:2},radius:7.5,clamped:false}],12.6],['cap',[0,1,2,3].map(k=>({pos:{x:k*9,y:0,z:0},radius:6,clamped:false})),4.4]]){
 const a=new AbilityPreview(new T.Scene());a.update(circles,t);const motes=[];for(let i=0;i<a.mesh.count;i++){a.mesh.getMatrixAt(i,m);m.decompose(p,q,s);motes.push([r4(p.x),r4(p.y),r4(p.z),r4(s.x)]);}
 const mat=a.mesh.material;preview.push({label,t,circles:circles.map(c=>({pos:[c.pos.x,c.pos.y,c.pos.z],radius:c.radius,clamped:c.clamped})),motes,color:mat.color.getHex(),opacity:mat.opacity,size:a.mesh.geometry.parameters.width,renderOrder:a.mesh.renderOrder});
}
const out={source:{commit:'05950ea',files},constants:{glowAlpha:FIELD_GLOW_ALPHA,ally:TELEGRAPH.ally,enemy:TELEGRAPH.enemy},glowGeometry,domeGeometry,fields,tint,siren:{patches,eruptions,flickerGlsl},preview};
fs.writeFileSync(path.join(root,'Tests/field-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',fields.length,'field runs,',fields.reduce((n,f)=>n+f.frames.length,0),'frames,',tint.length,'tint rows,',patches.length+eruptions.length,'Siren telegraphs,',preview.length,'preview cases');
