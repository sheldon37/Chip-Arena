// Volumetric chip blasts (2026-10-03): pooled, bounded, quality-aware and keyed to the authoritative radius.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {ChipBlasts,blastStyle,BLAST_CAPACITY,BLAST_FRONT_SECONDS,type BlastStyle} from '../src/client/chip-blasts.js';
import type {FieldView} from '../src/shared/chaos.js';

const STYLES:BlastStyle[]=['fire','shock','implosion','frost','toxic','void','nova','kinetic','quake','meteor','mine'];
const fresh=()=>{const scene=new T.Scene(),fx=new ChipBlasts(scene);return {scene,fx};};

test('every style spawns volumetric voxels above the ground (not a flat decal) and a light flash',()=>{
 for(const style of STYLES){const {fx}=fresh();fx.blast(style,{x:0,y:1,z:0},3,{quality:1});fx.update(.12);
  assert.ok(fx.solid.count+fx.glow.count>20,style);let top=-1e9;for(let i=0;i<fx.glow.count;i++)top=Math.max(top,fx.glow.at(i).y);for(let i=0;i<fx.solid.count;i++)top=Math.max(top,fx.solid.at(i).y);
  assert.ok(top>1.2,`${style} rises off the ground (${top.toFixed(2)})`);assert.ok(fx.flashes.some(f=>f.light.intensity>0),style);fx.dispose();}
});
test('the shock front ends exactly on the gameplay radius',()=>{
 for(const radius of [1.5,2.8,4,6]){const {fx}=fresh();fx.blast('kinetic',{x:2,y:1,z:-1},radius,{quality:1});
  let t=0;while(t<BLAST_FRONT_SECONDS-1/120){fx.update(1/120);t+=1/120;}let max=0;
  for(let i=0;i<fx.glow.count;i++){const p=fx.glow.at(i);if(p.life>BLAST_FRONT_SECONDS-.01&&p.life<BLAST_FRONT_SECONDS+.01)max=Math.max(max,Math.hypot(p.x-2,p.z+1));}
  assert.ok(Math.abs(max-radius)<radius*.06,`front ${max.toFixed(3)} vs radius ${radius}`);fx.dispose();}
});
test('pools never grow: capacity is fixed, overflow is dropped, scene children stay constant',()=>{
 const {scene,fx}=fresh();const children=scene.children.length;for(let n=0;n<200;n++){fx.blast(STYLES[n%STYLES.length],{x:n%7,y:1,z:0},5,{quality:1});fx.update(1/60);}
 assert.ok(fx.solid.count<=BLAST_CAPACITY.solid&&fx.glow.count<=BLAST_CAPACITY.glow);assert.equal(scene.children.length,children);
 assert.equal(fx.solid.mesh.instanceMatrix.count,BLAST_CAPACITY.solid);for(let n=0;n<240;n++)fx.update(1/60);assert.equal(fx.solid.count+fx.glow.count,0);assert.ok(fx.flashes.every(f=>f.light.intensity===0));fx.dispose();
});
test('effects quality scales the voxel count; zero spawns nothing; compatibility drops lights',()=>{
 const counts=[0,.5,1].map(q=>{const {fx}=fresh();fx.blast('fire',{x:0,y:1,z:0},3,{quality:q});const n=fx.solid.count+fx.glow.count;fx.dispose();return n;});
 assert.equal(counts[0],0);assert.ok(counts[1]<counts[2]&&counts[1]>0);
 const {fx}=fresh();fx.blast('fire',{x:0,y:1,z:0},3,{quality:1,compat:true});fx.update(.05);assert.ok(fx.flashes.every(f=>f.light.intensity===0));assert.ok(fx.solid.count+fx.glow.count<counts[2]);fx.dispose();
});
test('field emitters are rate limited, stay inside the field radius and forget dead fields',()=>{
 const {fx}=fresh();const f:FieldView={id:'f1',owner:'a',kind:'storm',pos:{x:0,y:1,z:0},radius:4,born:0,until:3,arm:0,damage:18};
 for(let t=0;t<=3;t+=1/60){fx.field(f,t,1);fx.update(1/60);}assert.ok(fx.glow.count>0);assert.ok(fx.glow.count<BLAST_CAPACITY.glow/2);
 for(const kind of ['acid','fire','frost','time','black-hole','repulsion','heal'] as const){const g={...f,id:kind,kind};const {fx:e}=fresh();for(let t=0;t<=1;t+=1/60){e.field(g,t,1);e.update(1/60);}
  for(let i=0;i<e.glow.count;i++){const p=e.glow.at(i);assert.ok(Math.hypot(p.x,p.z)<=g.radius*1.15+.6,kind);}e.dispose();}
 fx.prune(new Set());assert.equal(fx.fieldClocks,0);fx.dispose();
});
test('authoritative blast tags map to matching styles',()=>{
 assert.equal(blastStyle(undefined),'fire');assert.equal(blastStyle('thunderstep'),'shock');assert.equal(blastStyle('implosion'),'implosion');assert.equal(blastStyle('artillery'),'meteor');
 assert.equal(blastStyle('comet'),'fire');assert.equal(blastStyle('cluster-mine'),'mine');assert.equal(blastStyle('aftershock'),'quake');assert.equal(blastStyle('corpseParty'),'toxic');assert.equal(blastStyle('blockPulse'),'nova');
});
