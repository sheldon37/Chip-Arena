// Volumetric chip blasts (2026-10-03): pooled, bounded, quality-aware and keyed to the authoritative radius.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {ChipBlasts,blastStyle,blastTint,BLAST_CAPACITY,BLAST_FRONT_SECONDS,BLAST_MIN_VOXELS,type BlastStyle} from '../src/client/chip-blasts.js';
import {ABILITY_FX} from '../src/shared/abilities.js';
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

// V sweep (design §13.1, §13.3): ability tags, tints, occupancy-scaled blasts and the ability field emitters.
const ABILITY_STYLES:Record<string,BlastStyle>={frag:'kinetic',rocket:'fire','orbital-mark':'shock','flame-lance':'fire','meteor-strike':'meteor','tidal-wave':'kinetic','geyser-wall':'frost','blink-strike':'shock','ball-lightning':'shock',shackles:'void',singularity:'implosion'};
test('ability blast tags map to the §13.1 styles and every ability fx tag is covered',()=>{
 for(const [tag,style] of Object.entries(ABILITY_STYLES))assert.equal(blastStyle(tag),style,tag);assert.deepEqual([...ABILITY_FX].sort(),Object.keys(ABILITY_STYLES).sort());
 assert.equal(STYLES.length,11,'no new style');assert.ok(Object.values(ABILITY_STYLES).every(s=>STYLES.includes(s)));
});
test('ability tags that share a style have distinct tints (kinetic grey vs water blue; shock red-white, cyan, violet; fire default vs white-gold)',()=>{
 const groups=new Map<BlastStyle,string[]>();for(const [tag,style] of Object.entries(ABILITY_STYLES))groups.set(style,[...(groups.get(style)||[]),tag]);
 for(const [style,tags] of groups){const looks=tags.map(t=>JSON.stringify(blastTint(t)??'default'));assert.equal(new Set(looks).size,tags.length,`${style}: ${tags.join(',')}`);}
 assert.equal(blastTint('rocket'),undefined,'rocket keeps the fire default');assert.ok(blastTint('frag')&&blastTint('orbital-mark')&&blastTint('blink-strike')&&blastTint('ball-lightning')&&blastTint('flame-lance')&&blastTint('tidal-wave'));
 // The tint recolours the blast (core/hot/cool), so a tinted blast spawns other colours than the untinted style.
 const colours=(tint?:ReturnType<typeof blastTint>)=>{const {fx}=fresh();fx.blast('shock',{x:0,y:1,z:0},3,{quality:1,tint});fx.update(.05);fx.glow.mesh.instanceColor!.needsUpdate=true;const c=fx.glow.mesh.instanceColor!.array.slice(0,30).join();fx.dispose();return c;};
 assert.notEqual(colours(),colours(blastTint('orbital-mark')));
});
test('pool pressure: 4 E finales + 3 ability fields + chip fields, on saturated pools, all get their minimum share on the full radius',()=>{
 const {fx}=fresh(),fields:FieldView[]=[
  {id:'c1',owner:'a',kind:'acid',pos:{x:40,y:1,z:0},radius:3,born:0,until:9,arm:0,damage:1},{id:'c2',owner:'a',kind:'fire',pos:{x:40,y:1,z:8},radius:3,born:0,until:9,arm:0,damage:1},
  {id:'c3',owner:'a',kind:'storm',pos:{x:48,y:1,z:0},radius:4,born:0,until:9,arm:0,damage:1},{id:'c4',owner:'a',kind:'frost',pos:{x:48,y:1,z:8},radius:3.5,born:0,until:9,arm:0,damage:1},
  {id:'a1',owner:'a',kind:'black-hole',pos:{x:56,y:1,z:0},radius:4.5,born:0,until:9,arm:0,damage:1,fx:'singularity'},{id:'a2',owner:'a',kind:'fire',pos:{x:56,y:1,z:8},radius:3.2,born:0,until:9,arm:0,damage:1,fx:'meteor-strike'},
  {id:'a3',owner:'a',kind:'bomb',pos:{x:64,y:1,z:0},radius:3,born:0,until:9,arm:0,damage:1,fx:'orbital-mark',lockAt:8}];
 let t=0;for(;t<1;t+=1/60){for(const f of fields)fx.field(f,t,1);fx.update(1/60);}
 for(let n=0;n<30;n++)fx.blast('fire',{x:-30,y:1,z:n},6,{quality:1});assert.ok(fx.solid.count>=BLAST_CAPACITY.solid*.95&&fx.glow.count>=BLAST_CAPACITY.glow*.95,'saturated');
 const finales:[BlastStyle,number,string][]=[['meteor',3.5,'meteor-strike'],['frost',2.2,'geyser-wall'],['kinetic',4.2,'frag'],['implosion',4,'singularity']];
 const centers=finales.map((_,i)=>({x:i*12,y:1,z:-20}));for(const [i,[style,radius,tag]] of finales.entries())assert.ok(fx.blast(style,centers[i],radius,{quality:1,tint:blastTint(tag)})>=BLAST_MIN_VOXELS,tag);
 const reach=finales.map(()=>0);for(let k=0;k<=Math.ceil(BLAST_FRONT_SECONDS*120);k++){for(let i=0;i<fx.glow.count;i++){const p=fx.glow.at(i);for(const [n,c] of centers.entries()){const d=Math.hypot(p.x-c.x,p.z-c.z);if(Math.abs(p.y-c.y)<4&&d<=finales[n][1]*1.06)reach[n]=Math.max(reach[n],d);}}fx.update(1/120);for(const f of fields)fx.field(f,t+=1/120,1);}
 for(const [n,[,radius,tag]] of finales.entries())assert.ok(reach[n]>=radius*.94,`${tag} reaches ${reach[n].toFixed(2)} of ${radius}`);
 assert.deepEqual(BLAST_CAPACITY,{solid:900,glow:700,lights:3});assert.equal(fx.solid.mesh.instanceMatrix.count,900);assert.equal(fx.glow.mesh.instanceMatrix.count,700);fx.dispose();
});
test('occupancy thins blasts and stops ambient field work so blasts keep room',()=>{
 const count=(fill:number)=>{const {fx}=fresh();for(let n=0;n<fill;n++)fx.blast('fire',{x:-30,y:1,z:n},6,{quality:1});const before=fx.solid.spawned+fx.glow.spawned;fx.blast('fire',{x:0,y:1,z:0},3,{quality:1});const n=fx.solid.spawned+fx.glow.spawned-before;fx.dispose();return n;};
 assert.ok(count(0)>count(20),'fewer decorative voxels under pressure');assert.ok(count(20)>=BLAST_MIN_VOXELS);
 const {fx}=fresh();for(let n=0;n<30;n++)fx.blast('fire',{x:-30,y:1,z:n},6,{quality:1});const f:FieldView={id:'s',owner:'a',kind:'storm',pos:{x:0,y:1,z:0},radius:4,born:0,until:9,arm:0,damage:1},glow=fx.glow.spawned;
 for(let t=0;t<.5;t+=1/60)fx.field(f,t,1);assert.equal(fx.glow.spawned,glow,'no ambient spawns while the pools are saturated');fx.dispose();
});
test('field telegraph motes rise exactly on the gameplay radius, and a fresh field lands with a volumetric arrival impact',()=>{
 for(const [kind,radius] of [['acid',3],['bomb',4],['mine',2.3],['implosion',4],['time',4]] as const){const {fx}=fresh(),f:FieldView={id:'r',owner:'a',kind,pos:{x:2,y:1,z:-1},radius,born:0,until:9,arm:0,damage:1,fx:kind==='bomb'?'delayed-gratification':undefined};
  fx.field(f,.01,1);const arrival=fx.glow.count;assert.ok(arrival>10,`${kind} arrival impact`);let front=0;for(let t=0;t<BLAST_FRONT_SECONDS;t+=1/120){fx.update(1/120);for(let i=0;i<fx.glow.count;i++){const p=fx.glow.at(i),d=Math.hypot(p.x-2,p.z+1);if(d<=radius*1.06)front=Math.max(front,d);}}assert.ok(front>=radius*.94,`${kind} arrival reaches ${front.toFixed(2)}`);
  fx.clear();fx.field(f,1,1);let rim=0;for(let t=1;t<4;t+=1/60){const before=fx.glow.count;fx.field(f,t,1);for(let i=before;i<fx.glow.count;i++){const p=fx.glow.at(i);if(Math.abs(Math.hypot(p.x-2,p.z+1)-radius)<1e-4&&p.y<.2)rim++;}fx.update(1/60);}assert.ok(rim>=10,`${kind} rim motes ${rim}`);fx.dispose();}
 const {fx}=fresh(),old:FieldView={id:'o',owner:'a',kind:'acid',pos:{x:0,y:1,z:0},radius:3,born:0,until:9,arm:0,damage:1};fx.field(old,2,1);assert.equal(fx.glow.count+fx.solid.count,0,'a field first seen late (join, lab warm-up) skips the arrival');fx.dispose();
});
test('ability field emitters: Orbital Mark column, shrinking ring and lock flash; Meteor Strike meteor; Geyser fill order',()=>{
 const run=(f:FieldView,from:number,to:number)=>{const {fx}=fresh();fx.field(f,from,1);const pts:{x:number;y:number;z:number;t:number}[]=[];let flash=false;for(let t=from;t<to;t+=1/60){const n=fx.glow.count;fx.field(f,t,1);for(let i=n;i<fx.glow.count;i++)pts.push({...fx.glow.at(i),t});fx.update(1/60);flash||=fx.flashes.some(l=>l.light.intensity>0);}fx.dispose();return {pts,flash};};
 const mark:FieldView={id:'m',owner:'a',kind:'bomb',pos:{x:0,y:1,z:0},radius:3,born:0,until:1.2,arm:0,damage:60,fx:'orbital-mark',lockAt:1,attach:'b'};
 const early=run(mark,0,.9);assert.ok(early.pts.some(p=>p.y>6&&Math.hypot(p.x,p.z)<.2),'targeting column from the sky');assert.equal(early.flash,false,'no lock flash before lockAt');
 const ringAt=(t0:number,t1:number)=>Math.max(...early.pts.filter(p=>p.t>=t0&&p.t<t1&&p.y<.2&&Math.hypot(p.x,p.z)>3.05).map(p=>Math.hypot(p.x,p.z)),0);assert.ok(ringAt(0,.3)>ringAt(.6,.9)+.3,'the ring shrinks toward the blast radius');
 assert.ok(run(mark,.95,1.15).flash,'lock flash at lockAt');
 const meteor=run({id:'s',owner:'a',kind:'bomb',pos:{x:0,y:1,z:0},radius:3.5,born:0,until:1,arm:0,damage:70,fx:'meteor-strike'},0,1);assert.ok(meteor.pts.some(p=>p.t>.56&&p.y>3),'a meteor falls in the last 0.45 s');assert.ok(!meteor.pts.some(p=>p.t<.5&&p.y>3),'not before');
 const geyser=run({id:'g',owner:'a',kind:'bomb',pos:{x:0,y:1,z:0},radius:2.2,born:0,until:1,arm:0,damage:30,fx:'geyser-wall'},0,1),inner=(t0:number,t1:number)=>Math.max(...geyser.pts.filter(p=>p.t>=t0&&p.t<t1&&p.y<.2&&Math.abs(Math.hypot(p.x,p.z)-2.2)>1e-3).map(p=>Math.hypot(p.x,p.z)),0);
 assert.ok(inner(0,.25)<=2.2*.25+.01&&inner(.75,1)>inner(0,.25),'water fills the ring from the centre as the geyser arms');
});
test('impacts, lines and the pong paddle are pooled voxels (no scene objects added)',()=>{
 const {scene,fx}=fresh(),children=scene.children.length;fx.impact({x:0,y:1,z:0},1.5,0xffd66c,{quality:1});fx.impact({x:0,y:1,z:0},2,0x93db67,{quality:1,ground:true});fx.line({x:0,y:1,z:0},{x:3,y:1,z:0},0xffffff,0xc4b4ff,1);fx.paddle({x:0,y:1,z:0},{x:0,y:0,z:1},0xff5269,1);
 assert.equal(scene.children.length,children);assert.ok(fx.glow.count>40);fx.impact({x:0,y:1,z:0},1,0xffffff,{quality:0});fx.dispose();
});
