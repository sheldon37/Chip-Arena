// Run export_models.mjs first; this writes what Tests/SirenSmileFaceChecks.cs holds Core/NativeSirenSmileFace.cs to
// (Tests/siren-smile-face-parity.json): the dev-siren entrance face of tools/siren_smile.mjs sampled at 60 Hz from .1 s before
// the 'smile' cue to the stage hold's end (plus the lit stare and the blackout stare), with
//  - every smile channel, each fang's reach and head-local transform (the 6 varying entries of its affine map; z and the
//    grin origin are fixed), the body layer the entrance bake carries and the couture glow lines (siren_design.mjs);
//  - the eyes from the verified source Siren itself (client/siren/model.ts applySirenLook at the smile's open, squint and
//    anger, the eye raised by the squint's lift), the eye halos, and the cheeks from tools/siren_design.mjs dress() at the
//    smile's lift (three.js scene graph), all relative to the head;
//  - the rest mouth's fangs (the lower row's unit mesh holding -v) mapped onto the arena grin (siren_design.mjs grinRow), checked here.
// Usage: node tools/export_siren_smile.mjs <temporary output of export_models.mjs>
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL,fileURLToPath} from 'node:url';
import * as D from './siren_design.mjs';import * as F from './siren_smile.mjs';
let exportSeed=9137;Math.random=()=>{exportSeed=(Math.imul(exportSeed,1664525)+1013904223)>>>0;return exportSeed/4294967296;};
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),stage=path.resolve(process.argv[2]),load=p=>import(pathToFileURL(path.join(stage,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(stage,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('client/character-rig'),{buildSiren,applySirenLook}=await load('client/siren/model'),M=await load('client/siren/materials'),{SIREN_INTRO}=await load('shared/siren-contract');
const cue=n=>SIREN_INTRO.cues.find(c=>c.cue===n).time,cues={blackout:cue('blackout'),smile:cue('smile'),name:SIREN_INTRO.nameAt};
if(Math.abs(cues.name-cues.smile-F.NAME)>1e-12)throw Error('name beat offset');
const r9=x=>Number(x.toPrecision(9)),six=m=>[m[0],m[1],m[3],m[4],m[5],m[7]].map(r9);
const rig=createCharacterRig('#70e1d0'),parts=buildSiren(rig);D.finish(T,parts,{grin:true,talons:true});const design=D.dress(T,rig,parts,M,{groups:['cheek']});
const cheeks=['cheek-box-l','cheek-trim-l','cheek-box-r','cheek-trim-r'].map(id=>design.objects.get(id));
// A node's transform relative to the head, row-major 3x4.
const local=o=>{const m=new T.Matrix4();for(let q=o;q&&q!==rig.head;q=q.parent){q.updateMatrix();m.premultiply(q.matrix);}const e=m.elements;return [e[0],e[4],e[8],e[12],e[1],e[5],e[9],e[13],e[2],e[6],e[10],e[14]];};
// The rest mouth is the arena grin: the unit fang mapped by toothMatrix lands on grinRow's vertices.
{const p={wPos:F.REST.w,wNeg:F.REST.w,uy:F.REST.uy,uk:F.REST.uk,ly:F.REST.ly,lk:F.REST.lk,tilt:0,open:0};let worst=0;
 for(const row of ['upper','lower']){const ref=D.grinRow(row),n=row==='upper'?D.GRIN.upper:D.GRIN.lower;let k=0;
  for(let i=0;i<n;i++){const m=F.toothMatrix(row,i,p,1),sv=row==='upper'?1:-1,P=D.TOOTH_SHAPE.map(([u,v])=>[m[0]*u+m[1]*sv*v+m[3],m[4]*u+m[5]*sv*v+m[7]]);
   for(const [a,b,c] of D.TOOTH_FAN){const cr=(P[b][0]-P[a][0])*(P[c][1]-P[a][1])-(P[c][0]-P[a][0])*(P[b][1]-P[a][1]);for(const q of cr>=0?[a,b,c]:[a,c,b]){worst=Math.max(worst,Math.abs(P[q][0]-ref[k]),Math.abs(P[q][1]-ref[k+1]));k+=3;}}}
  if(k!==ref.length)throw Error('grin row '+row);}
 if(worst>1e-12)throw Error('rest mouth differs from the arena grin by '+worst);}
const ages=[4.4,5.7];for(let i=0;i<=192;i++)ages.push(cues.smile-.1+i/60);
const samples=ages.map(age=>{
 const f=F.face(age,cues),p=f.channels;
 Object.assign(parts.state,{ranged:0,open:p.open,squint:p.squint,anger:p.anger,glow:1,flare:0,lean:0,time:age});applySirenLook(parts);
 for(const e of parts.eyes)e.position.y=F.EYE.y+F.EYE.lift*p.squint;
 design.update({time:age,clock:age,c:p.c});const glow=D.entranceGlow(age,cues),b=F.body(f.s);
 return {age:r9(age),s:r9(f.s),grin:f.grin,shape:Math.round(p.anger*40),vis:r9(p.vis),glow:r9(p.glow),wPos:r9(p.wPos),wNeg:r9(p.wNeg),uy:r9(p.uy),uk:r9(p.uk),ly:r9(p.ly),lk:r9(p.lk),tilt:r9(p.tilt),open:r9(p.open),
  anger:r9(p.anger),squint:r9(p.squint),halo:r9(p.halo),c:r9(p.c),chuckle:r9(p.chuckle),strain:r9(p.strain),bite:r9(p.bite),reach:[...p.reachU,...p.reachL].map(r9),
  body:[b.headX,b.headY,b.headZ,b.neckX,b.chestX,b.clavicle].map(r9),seam:r9(glow.seam),filament:r9(glow.filament),
  teeth:f.teeth.map(six),eyes:parts.eyes.map(e=>local(e).map(r9)),cheeks:cheeks.map(o=>local(o).map(r9))};});
const out={source:'05950eaef80ff2483a9c44d898cad98be1a76bbc',cues,beats:{blackout:F.BLACKOUT_BEAT,smile:F.SMILE_BEAT},offsets:{name:F.NAME,beat16:F.BEAT16,end:F.END,hold:F.HOLD_END},
 closed:F.CLOSED,rest:F.REST,bite:F.BITE,jaw:F.JAW,reach:{upper:D.UPPER_REACH,lower:D.LOWER_REACH},fang:{outline:F.FANG,inset:D.GRIN.inset,shape:D.TOOTH_SHAPE.flat().map(r9),fan:D.TOOTH_FAN.flat()},
 eye:F.EYE,halo:F.HALO,halos:parts.eyeHalos.map(h=>local(h).map(r9)),mouthZ:F.MOUTH_Z,samples};
fs.writeFileSync(path.join(root,'Tests/siren-smile-face-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',samples.length,'entrance face samples');
