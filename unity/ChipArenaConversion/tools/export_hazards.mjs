// Stage hazard visuals (client/stage-hazards.ts) exported as separately animated parts; the runtime animates them from
// NativeHazards with the source's per-frame formulas. Geometry and materials come from the source class unchanged.
// Usage: node tools/export_hazards.mjs <pinned source> <temporary verified module stage> (same staging as export_models.mjs)
import fs from 'node:fs';import path from 'node:path';import {gzipSync} from 'node:zlib';
import {exportModel,models,T,load,root,inventory} from './export_models.mjs';
const first=models.length,{StageHazardVisuals}=await load('stage-hazards');
const build=(map,id)=>{const visuals=new StageHazardVisuals(new T.Scene());visuals.update(map,0);const g=visuals.group.getObjectByName(id);return {visuals,g:reset(g)};};
const reset=o=>{o.position.set(0,0,0);o.rotation.set(0,0,0);o.scale.set(1,1,1);o.updateMatrixWorld(true);return o;};
// Emission the source animates per frame is exported as unit white; the runtime supplies the source colour and intensity.
const unit=mesh=>{mesh.material.emissive.setRGB(1,1,1);mesh.material.emissiveIntensity=1;};
{const {visuals,g}=build('sawmill','saw-0'),track=reset(visuals.group.children.find(c=>!c.name));exportModel('hazard-saw',g);exportModel('hazard-saw-track',track);}
{const {g}=build('spikevault','spikes-0-0');reset(g.getObjectByName('spikes'));exportModel('hazard-spikes',g,null,o=>o.name!=='surface'&&o.name!=='spikes');exportModel('hazard-spikes-cones',g,null,o=>o.name==='spikes');unit(g.getObjectByName('surface'));exportModel('hazard-spikes-surface',g,null,o=>o.name==='surface');}
{const {g}=build('lavaworks','lava-0');reset(g.getObjectByName('currents'));exportModel('hazard-lava',g,null,o=>o.name==='edge');exportModel('hazard-lava-currents',g,null,o=>o.name==='crust');
 const bubbles=g.getObjectByName('bubbles').children;for(let k=0;k<2;k++){const bubble=reset(bubbles[k]);exportModel('hazard-lava-bubble-'+k,g,null,o=>o===bubble);}
 unit(g.getObjectByName('surface'));exportModel('hazard-lava-surface',g,null,o=>o.name==='surface');}
const hazards=models.splice(first);if(hazards.length!==10||hazards.some(m=>!m.sections.length||m.sections.some(s=>s.texture>=0)))throw Error('Hazard export incomplete');
const data=JSON.stringify({models:hazards,textures:[]});
fs.writeFileSync(path.join(root,'Assets/Conversion/Resources/HazardModels.json'),JSON.stringify({schema:1,commit:inventory.sourceCommit,data:gzipSync(data,{level:9}).toString('base64')})+'\n');
console.log('Hazard export complete',hazards.map(m=>m.id+':'+m.sections.reduce((n,s)=>n+s.positions.length/3,0)).join(' '));
