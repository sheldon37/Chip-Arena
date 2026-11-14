// Retargets the hit-reaction clips of the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity asset)
// onto the voxel rig for the native hit layer (Core/NativePackReact): directional GetHit, Block-GetHit and the Stunned loop
// per weapon style. Development tool, presentation only. The pack's FBX and .meta files are never committed: point the
// script at an extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_reactions.mjs <pack>/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackClips.Generated.cs (the retargeted pose
// tracks in the 64-channel mocap encoding the melee clips use, Core/CapturedClip) and tools/pack-reactions-provenance.json
// (each clip's source, size, impact marks and the SHA-256 of its encoded track, which Tests/PackReactionChecks recomputes).
// The same input files give byte-identical output. The retarget is the melee converter's (scripts/mocap/mixamo-retarget.mjs
// through explosive-pack.mjs): the same calibration, grips, two-handed hilt clearance and guard heading as the set's
// captured melee clips, so a reaction starts from the guard the melee route holds.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'Armed'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_reactions.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const A=f=>`Armed/RPG-Character@Armed-${f}.FBX`,H=f=>`2Hand-Sword/RPG-Character@2Hand-Sword-${f}.FBX`,S=f=>`Armed-Shield/RPG-Character@Armed-Shield-${f}`,U=f=>`Unarmed/RPG-Character@Unarmed-${f}.FBX`;
/** Reaction sets (Core/NativePackReact.Set order). Slots: hitF / hitF2 (a front blow / a heavy front blow),
 * hitL (struck on the rig's left side: the body is pushed to its right), hitR, stun (the Stunned loop), block / block2 (a blow
 * absorbed by the guard), guard (the first frame of the guard the body hits start from: the Stunned loop is layered as its
 * offset from it, every other clip as its offset from its own first frame). `mirror` reads a left-arm capture onto the right arm, so its L and R files swap. The idle is the
 * melee set's guard, whose mean pelvis turn (heading) the torso carries in every clip, as the melee converter does. */
const SETS=[
 {name:'sword',idle:A('Idle'),hitF:A('GetHit-F1'),hitF2:A('GetHit-F2'),hitL:A('GetHit-L1'),hitR:A('GetHit-R1'),stun:A('Stunned'),block:A('Block-R-GetHit1'),block2:A('Block-R-GetHit2')},
 {name:'daggers',grip:'reverse',leftBlade:true,idle:A('Idle'),hitF:A('GetHit-F1'),hitF2:A('GetHit-F2'),hitL:A('GetHit-L1'),hitR:A('GetHit-R1'),stun:A('Stunned'),block:A('Block-Dual-GetHit1'),block2:A('Block-Dual-GetHit2')},
 {name:'greatsword',twoHand:true,idle:H('Idle'),hitF:H('GetHit-F1'),hitF2:H('GetHit-F2'),hitL:H('GetHit-L1'),hitR:H('GetHit-R1'),stun:H('Stunned'),block:H('Block-GetHit1'),block2:H('Block-GetHit2')},
 // The pack's shield set has no GetHit or Stunned capture: the shield pilot takes the Armed body reactions (the layer keeps
 // the shield arm on its own guard) and the shield set's own block hits, read mirrored onto the right arm like its melee set.
 {name:'shield',grip:'shield',idle:S('Idle.fbx'),idleMirror:true,hitIdle:A('Idle'),hitF:A('GetHit-F1'),hitF2:A('GetHit-F2'),hitL:A('GetHit-L1'),hitR:A('GetHit-R1'),stun:A('Stunned'),block:S('Block-GetHit1.FBX'),block2:S('Block-GetHit2.FBX'),blockMirror:true},
 // Fists: the DEV fighters (gun and magic pilots keep the procedural layer: this torso swept their muzzle and staff aside).
 {name:'unarmed',idle:U('Idle'),hitF:U('GetHit-F1'),hitF2:U('GetHit-F2'),hitL:U('GetHit-L1'),hitR:U('GetHit-R1'),stun:U('Stunned'),block:U('Block-GetHit1'),block2:U('Block-GetHit2')},
];
// GetHit-B1 is not exported: on the voxel rig its arched recoil reads as an upright flinch, so a blow from behind keeps the
// procedural lurch (Core/NativePackRules.Direction).
const SLOTS=['hitF','hitF2','hitL','hitR','stun','block','block2','guard'];

// The pack mixes .fbx and .FBX: names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',cals=new Map();
const calFor=(mirror,grip)=>{const k=mirror+grip;if(!cals.has(k))cals.set(k,calibrate(packRestPose(resolve(REF)),{mirror,grip}));return cals.get(k);};
const ix=n=>CHANNELS.indexOf(n),TORSO=['spine','chest','neck','head'].map(n=>ix(n+'.x'));

/** The impact peak (source seconds): the frame where the torso and head have turned furthest from the clip's first frame,
 * within its first 60%; marks [start, impact, settled]. */
function marks(rows,fps){
 let best=0,at=0;const n=rows.length,stop=Math.max(2,Math.floor((n-1)*.6));
 for(let i=1;i<=stop;i++){let s=0;for(const c of TORSO)s+=Math.hypot(rows[i][c]-rows[0][c],rows[i][c+1]-rows[0][c+1],rows[i][c+2]-rows[0][c+2]);if(s>best+1e-6){best=s;at=i;}}
 return [0,+(at/fps).toFixed(4),+((n-1)/fps).toFixed(4)];
}
const f32=x=>String(x)+'f';
const lines=['// GENERATED by tools/export_pack_reactions.mjs from the owner\'s Explosive RPG Character pack: retargeted pose tracks only',
 '// (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackClips {','public static readonly CapturedClip[][] Sets = {'];
const provenance=[];
for(const set of SETS){
 const twoHand=!!set.twoHand,grip=set.grip||'blade',leftBlade=!!set.leftBlade;
 // The guard heading of the idle a slot starts from (the shield's body hits start from the Armed guard, its blocks from its own).
 const headingOf=(file,mirror)=>{const {obj,clip}=load(file),ir=retarget(obj,clip,calFor(mirror,grip),30,{mirror,oneHand:!twoHand});return ir.reduce((s,r)=>s+r[0],0)/ir.length;};
 const guard=headingOf(set.idle,!!set.idleMirror),body=set.hitIdle?headingOf(set.hitIdle,false):guard;
 lines.push('new CapturedClip[] {');
 for(const slot of SLOTS){
  const file=slot==='guard'?set.hitIdle||set.idle:set[slot],blocking=slot.startsWith('block'),mirror=blocking?!!set.blockMirror:false,heading=blocking?guard:body,loop=slot==='stun',fps=loop?30:60,{obj,clip}=load(file);
  let rows=retarget(obj,clip,calFor(mirror,grip),fps,{mirror,oneHand:!twoHand,bothHands:twoHand,leftBlade,soften:true,poleTurn:10,splitTwist:true,heading,loop:loop||slot==='guard'});
  if(slot==='guard')rows=[rows[0],rows[0]];
  for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${set.name}/${slot}: non-finite channel`);
  const m=loop||slot==='guard'?[]:marks(rows,fps),data=encode(rows),source=path.basename(file).replace(/^RPG-Character@|\.fbx$/gi,'')+(mirror?' (mirrored)':'');
  lines.push('new CapturedClip('+[JSON.stringify(set.name+'/'+slot),JSON.stringify(source),fps,rows.length,loop,'new float[] {'+m.map(f32).join(',')+'}','new float[] {}',JSON.stringify(data)].join(',')+'),');
  provenance.push({clip:set.name+'/'+slot,source,fps,frames:rows.length,loop,marks:m,sha256:createHash('sha256').update(data).digest('hex'),input:createHash('sha256').update(fs.readFileSync(resolve(file))).digest('hex')});
 }
 lines.push('},');
 console.log(set.name,'guard heading',guard.toFixed(3),'body',body.toFixed(3));
}
lines.push('};','public static readonly string[] Names = {'+SETS.map(s=>JSON.stringify(s.name)).join(', ')+'};','public static readonly string[] Slots = {'+SLOTS.map(s=>JSON.stringify(s)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackClips.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-reactions-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'reaction clips');
