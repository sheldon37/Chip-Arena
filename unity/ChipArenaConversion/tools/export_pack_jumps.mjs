// Retargets the jump clips of the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity asset) onto
// the voxel rig for the native jump layer (Core/NativeJumpMotion): per weapon style the Jump take (crouch, push-off, rise,
// tuck and the fall-entry pose), the Fall loop, the Land take (impact compression back to the guard) and the Jump-Flip,
// plus the style's guard frame the layer measures every offset from. Development tool, presentation only. The pack's FBX
// and .meta files are never committed: point the script at an extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_jumps.mjs <pack>/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackJumps.Generated.cs (the retargeted pose
// tracks in the 64-channel mocap encoding the melee clips use, Core/CapturedClip) and tools/pack-jumps-provenance.json
// (each clip's source, frame range, size, marks and the SHA-256 of its encoded track, which Tests/JumpMotionChecks
// recomputes). The same input files give byte-identical output. The retarget is the melee converter's
// (scripts/mocap/mixamo-retarget.mjs through explosive-pack.mjs) with the reaction exporter's options, so a jump starts
// from the guard the melee route holds. The pack keeps Jump and Land in one take: the .meta frame ranges split it.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'Armed'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_jumps.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

/** Jump sets (Core/NativeJumpRules order). `clips(f)` names a set file. The shield has no jump capture: it takes the Armed
 * body (the runtime keeps the shield arm on its own guard); the guns take 2Hand-Shooting (rifle, machine gun) and Unarmed
 * (pistol), the magic schools 2Hand-Staff, the DEV fighters' fists Unarmed (their routes keep their own arms). */
const A=f=>`Armed/RPG-Character@Armed-${f}.FBX`,H=f=>`2Hand-Sword/RPG-Character@2Hand-Sword-${f}.FBX`,G=f=>`2Hand-Shooting/RPG-Character@2Hand-Shooting-${f}.FBX`,
 T=f=>`2Hand-Staff/RPG-Character@2Hand-Staff-${f}.FBX`,U=f=>`Unarmed/RPG-Character@Unarmed-${f}.FBX`;
const SETS=[
 {name:'sword',clips:A},
 {name:'daggers',clips:A,grip:'reverse',leftBlade:true},
 {name:'greatsword',clips:H,twoHand:true},
 {name:'shield',clips:A,grip:'shield'},
 {name:'shooting',clips:G},
 {name:'staff',clips:T},
 {name:'unarmed',clips:U},
];
/** Slots: guard (the set's Idle first frame: every clip is laid as its offset from it), jump (the Jump take: crouch,
 * push-off, rise and tuck into the fall-entry pose), fall (the Fall loop), land (the Land take: from the air pose through
 * the impact compression back to the guard), flip (the Jump-Flip, from the push-off through the tucked flip into the
 * fall-entry pose; its pelvis turns through a full circle, which the layer replaces with a procedural flip). */
const SLOTS=['guard','jump','fall','land','flip'],FILES={guard:'Idle',jump:'Jump',fall:'Fall',land:'Land',flip:'Jump-Flip'};

// The pack mixes .fbx and .FBX: names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',cals=new Map();
const calFor=grip=>{if(!cals.has(grip))cals.set(grip,calibrate(packRestPose(resolve(REF)),{mirror:false,grip}));return cals.get(grip);};
/** The clip's frame range (30 Hz source frames) and loop flag from its Unity import settings. */
function range(file){
 const meta=fs.readFileSync(resolve(file)+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
const ix=n=>CHANNELS.indexOf(n),BY=ix('by'),LK=ix('lknee'),RK=ix('rknee');
const r4=x=>+x.toFixed(4);
/** Marks (clip seconds). jump: [crouch bottom (lowest hips in the first 40%), push-off (straightest legs after it), tuck
 * (most folded legs after the push-off), end]; land: [start, deepest compression, end]; fall, flip: none. */
function marks(slot,rows,fps){
 const n=rows.length,knees=r=>r[LK]+r[RK];
 if(slot==='jump'){
  let low=0;for(let i=1;i<=Math.floor((n-1)*.4);i++)if(rows[i][BY]<rows[low][BY]-1e-6)low=i;
  let push=low;for(let i=low+1;i<n;i++){if(knees(rows[i])<knees(rows[push])-1e-6)push=i;else if(i>push+2)break;}
  let tuck=push;for(let i=push+1;i<n;i++)if(knees(rows[i])>knees(rows[tuck])+1e-6)tuck=i;
  return [r4(low/fps),r4(push/fps),r4(tuck/fps),r4((n-1)/fps)];
 }
 if(slot==='land'){let deep=0;for(let i=1;i<n;i++)if(rows[i][BY]<rows[deep][BY]-1e-6)deep=i;return [0,r4(deep/fps),r4((n-1)/fps)];}
 return [];
}
const f32=x=>String(x)+'f';
const lines=['// GENERATED by tools/export_pack_jumps.mjs from the owner\'s Explosive RPG Character pack: retargeted pose tracks only',
 '// (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackJumps {','public static readonly CapturedClip[][] Sets = {'];
const provenance=[];
for(const set of SETS){
 const twoHand=!!set.twoHand,grip=set.grip||'blade',leftBlade=!!set.leftBlade,cal=calFor(grip),opts={oneHand:!twoHand,bothHands:twoHand,leftBlade,soften:true,poleTurn:10,splitTwist:true};
 // The guard heading (the idle's mean pelvis turn), carried by the torso in every clip as the melee converter does.
 const idle=load(set.clips(FILES.guard)),ir=retarget(idle.obj,idle.clip,cal,30,{...opts,loop:true}),heading=ir.reduce((s,r)=>s+r[0],0)/ir.length;
 lines.push('new CapturedClip[] {');
 for(const slot of SLOTS){
  const file=set.clips(FILES[slot]),{obj,clip}=load(file),rg=slot==='guard'?{first:0,last:0,loop:false}:range(file),loop=slot==='fall',fps=loop?30:60;
  let rows=retarget(obj,clip,cal,fps,{...opts,heading,loop:loop||slot==='guard'});
  if(slot==='guard')rows=[rows[0],rows[0]];
  else{const a=Math.round(rg.first/30*fps),b=Math.round(rg.last/30*fps);if(b>=rows.length||b-a<2)throw new Error(`${set.name}/${slot}: frame range ${rg.first}-${rg.last} outside the take`);rows=rows.slice(a,b+1);}
  if(loop!==(slot==='fall')||slot==='fall'&&!rg.loop)throw new Error(`${set.name}/${slot}: unexpected loop setting`);
  for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${set.name}/${slot}: non-finite channel`);
  const m=marks(slot,rows,fps),data=encode(rows),source=path.basename(file).replace(/^RPG-Character@|\.fbx$/gi,'');
  lines.push('new CapturedClip('+[JSON.stringify(set.name+'/'+slot),JSON.stringify(source),fps,rows.length,loop,'new float[] {'+m.map(f32).join(',')+'}','new float[] {}',JSON.stringify(data)].join(',')+'),');
  provenance.push({clip:set.name+'/'+slot,source,range:slot==='guard'?[0,0]:[rg.first,rg.last],fps,frames:rows.length,loop,marks:m,sha256:createHash('sha256').update(data).digest('hex'),input:createHash('sha256').update(fs.readFileSync(resolve(file))).digest('hex')});
 }
 lines.push('},');
 console.log(set.name,'guard heading',heading.toFixed(3));
}
lines.push('};','public static readonly string[] Names = {'+SETS.map(s=>JSON.stringify(s.name)).join(', ')+'};','public static readonly string[] Slots = {'+SLOTS.map(s=>JSON.stringify(s)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackJumps.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-jumps-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'jump clips');
