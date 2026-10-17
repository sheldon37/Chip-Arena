// The Bow's motion from the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity asset), for the bow
// presentation (owner 2026-10-12: "For ranged i want to add a "Bow" class"; Core/NativeBowMotion.cs). Two outputs:
//  - the 2Hand-Bow travel loops (slow walk, the walk and seven strafes, the run in eight directions) retargeted onto the voxel
//    rig exactly as tools/export_pack_ranged_locomotion.mjs retargets the gun and staff carries (the left-foot strike as phase
//    0), written to Assets/ChipArena/Core/NativePackBow.Generated.cs (pose tracks in the 64-channel mocap encoding);
//  - derived numbers only from the draw, hold and release takes (Attack1-6 full shots, Aiming / Aiming-Pull held poses,
//    Aiming-Fire releases, Idle): the draw and release timing, the follow-through travel, the bladed stance's yaw shares, the
//    aim lean and the carry/ready hand layout, in tools/pack-bow-provenance.json with each input's SHA-256.
// Development tool, presentation only. The pack's FBX and .meta files are never committed: point the script at an extracted
// copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_bow.mjs <pack>/Animations
// The same input files give byte-identical output; Tests/BowChecks recomputes the loops' hashes and compares the Core keys
// with the provenance.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'2Hand-Bow'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_bow.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,leftFootStrike,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose,packEvents}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const B=f=>`2Hand-Bow/RPG-Character@2Hand-Bow-${f}.FBX`;
// The pack spells the forward runs "Foward".
const run=d=>B('Run-'+d.replace('Forward','Foward'));
const DIRS=['Forward','Forward-Left','Left','Backward-Left','Backward','Backward-Right','Right','Forward-Right'];
const SLOTS=['slow',...DIRS.map(d=>'walk-'+d.toLowerCase()),...DIRS.map(d=>'run-'+d.toLowerCase())];
const fileOf=slot=>{if(slot==='slow')return B('Walk-Slow');const [family,...rest]=slot.split('-'),d=DIRS[DIRS.findIndex(x=>x.toLowerCase()===rest.join('-'))];return family==='walk'?(d==='Forward'?B('Walk'):B('Strafe-'+d)):run(d);};
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',cal=calibrate(packRestPose(resolve(REF)),{mirror:false,grip:'blade'});
const sha=f=>createHash('sha256').update(fs.readFileSync(resolve(f))).digest('hex');
function range(file){
 const meta=fs.readFileSync(resolve(file)+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
const provenance=[];
/** One loop as a CapturedClip constructor line (and its provenance entry): the shooting carry's retarget (unmirrored: the
 * pack holds the bow in its left hand, as the game does). */
function clipLine(name,file){
 const {obj,clip}=load(file),rg=range(file),fps=30;
 if(!rg.loop)throw new Error(file+': not a loop');
 let rows=retarget(obj,clip,cal,fps,{mirror:false,oneHand:true,soften:true,poleTurn:10,splitTwist:true,heading:0,loop:true});
 const a=Math.round(rg.first/30*fps),b=Math.round(rg.last/30*fps);if(b>=rows.length||b-a<4)throw new Error(`${name}: frame range outside the take`);rows=rows.slice(a,b+1);
 for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${name}: non-finite channel`);
 const strike=+Math.min(leftFootStrike(obj,clip,60,{mirror:false}),(rows.length-1)/fps).toFixed(4),data=encode(rows),source=path.basename(file).replace(/^RPG-Character@|\.fbx$/gi,'');
 provenance.push({clip:name,source,range:[rg.first,rg.last],fps,frames:rows.length,loop:true,strike,sha256:createHash('sha256').update(data).digest('hex'),input:sha(file)});
 return 'new CapturedClip('+[JSON.stringify(name),JSON.stringify(source),fps,rows.length,true,'new float[] {'+String(strike)+'f}','new float[] {}',JSON.stringify(data)].join(',')+')';
}

// ---- derived numbers from the draw / hold / release takes (chest space: x the rig's left, y up, z forward) ----
const ix=n=>CHANNELS.indexOf(n),vec=(r,n)=>[0,1,2].map(k=>r[ix(n+'.'+'xyz'[k])]),sub=(a,b)=>a.map((v,i)=>v-b[i]),len=a=>Math.hypot(...a),r4=x=>+x.toFixed(4),v4=a=>a.map(r4);
const pose=f=>{const {obj,clip}=loadPackFbx(resolve(f));return retarget(obj,clip,cal,30,{oneHand:true,soften:false});};
const aim=pose(B('Aiming')),pull=pose(B('Aiming-Pull')),fire=pose(B('Aiming-Fire')),idle=pose(B('Idle'));
// Held aim poses: CM at frame 0, UM (aim up) at frame 2, DM (down) at 4 (the .meta's single-frame clip ranges).
const CM=0,UM=2,DM=4;
const arrow=r=>{const d=sub(vec(r,'lhand'),vec(r,'grip'));return d.map(v=>v/len(d));};
const pitchOf=d=>Math.asin(d[1]);
// The release (Aiming-Fire CM, frames 1-10): the draw hand's travel from the anchor, its peak and the return.
const anchor=vec(pull[CM],'grip'),along=arrow(pull[CM]);
let peak=0,peakAt=0,back=0,down=0,out=0;
for(let i=1;i<=10;i++){const d=sub(vec(fire[i],'grip'),anchor),b=-(d[0]*along[0]+d[1]*along[1]+d[2]*along[2]);if(b>peak){peak=b;peakAt=i;back=b;down=-d[1];out=d[2];}}
let settle=10;for(let i=peakAt;i<=10;i++){const d=sub(vec(fire[i],'grip'),anchor);if(len(d)<.02){settle=i;break;}}
// The full shots (Attack1-6): the time the bow arm reaches its extension (the draw to the anchor) and the Shoot event.
const shots=[];
for(let n=1;n<=6;n++){
 const f=B('Attack'+n),rows=pose(f),shoot=packEvents(resolve(f),'Shoot')[0];let ext=0;
 const l0=vec(rows[0],'lhand');for(let i=1;i<rows.length;i++){const l=vec(rows[i],'lhand');if(l[0]>.68){ext=i;break;}}
 const s=Math.round(shoot*30);let fp=0,fpAt=s;const a0=vec(rows[Math.max(0,s-1)],'grip');
 for(let i=s;i<Math.min(rows.length,s+6);i++){const d=len(sub(vec(rows[i],'grip'),a0));if(d>fp){fp=d;fpAt=i;}}
 shots.push({clip:'2Hand-Bow-Attack'+n,shoot:r4(shoot),extended:r4(ext/30),followPeak:r4((fpAt-s)/30),followTravel:r4(fp),input:sha(f)});
}
const mean=k=>r4(shots.reduce((s,x)=>s+x[k],0)/shots.length);
// The bladed stance: the heading (hips) and the spine and chest yaw at full draw (rotation-vector y, rad), the head's turn back.
const yawOf=(r,n)=>vec(r,n)[1];
const blade={hips:r4(-pull[CM][ix('yaw')]),spine:r4(-yawOf(pull[CM],'spine')),chest:r4(-yawOf(pull[CM],'chest')),neck:r4(yawOf(pull[CM],'neck')),head:r4(yawOf(pull[CM],'head'))};
blade.total=r4(blade.hips+blade.spine+blade.chest);
// The aim lean: the spine's and chest's roll (rotation-vector z) between the up (UM) and level (CM) holds, against the aim pitch.
const rollOf=(r,n)=>vec(r,n)[2];
const lean={spine:r4(rollOf(pull[UM],'spine')-rollOf(pull[CM],'spine')),chest:r4(rollOf(pull[UM],'chest')-rollOf(pull[CM],'chest')),pitchUp:r4(pitchOf(arrow(pull[UM]))-pitchOf(arrow(pull[CM]))),
 spineDown:r4(rollOf(pull[DM],'spine')-rollOf(pull[CM],'spine')),chestDown:r4(rollOf(pull[DM],'chest')-rollOf(pull[CM],'chest'))};
// Carry (Idle, mean of the loop) and ready (Aiming CM: nocked, raised, undrawn) hand layout.
const avg=(rows,n)=>{const s=[0,0,0];for(const r of rows){const v=vec(r,n);for(let k=0;k<3;k++)s[k]+=v[k]/rows.length;}return v4(s);};
const idleRows=idle.slice(0,61);
const derived={
 drawToAnchor:mean('extended'),shoot:mean('shoot'),quickFollowPeak:mean('followPeak'),quickFollowTravel:mean('followTravel'),
 release:{peak:r4(peakAt/30),settle:r4(settle/30),back:r4(back),down:r4(down),out:r4(out)},
 drawLength:r4(len(sub(vec(pull[CM],'lhand'),vec(pull[CM],'grip')))),readyLength:r4(len(sub(vec(aim[CM],'lhand'),vec(aim[CM],'grip')))),
 blade,lean,carry:{bowHand:avg(idleRows,'lhand'),drawHand:avg(idleRows,'grip')},ready:{bowHand:v4(vec(aim[CM],'lhand')),drawHand:v4(vec(aim[CM],'grip'))},
 note:'chest space (x the rig\'s left); blade: hips (heading), spine and chest yaw at full draw (rad, turning the left shoulder to the target), neck/head turn back; lean: roll added for the up hold against its aim pitch; release: Aiming-Fire CM draw-hand travel from the anchor (peak/settle s, back along the arrow, down, out m)',
};

const lines=['// GENERATED by tools/export_pack_bow.mjs from the owner\'s Explosive RPG Character pack: retargeted pose tracks only',
 '// (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackBow {','/// <summary>The 2Hand-Bow travel loops in NativeRangedLocomotion\'s slot order (NativePackRangedLocomotion.Slots).</summary>','public static readonly CapturedClip[] Loco = {'];
for(const slot of SLOTS)lines.push(clipLine('bow/'+slot,fileOf(slot))+',');
lines.push('};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackBow.Generated.cs'),lines.join('\n')+'\n');
const sources=[B('Aiming'),B('Aiming-Pull'),B('Aiming-Fire'),B('Idle')].map(f=>({clip:path.basename(f).replace(/^RPG-Character@|\.fbx$/gi,''),input:sha(f)}));
fs.writeFileSync(path.join(root,'tools/pack-bow-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',calibration:REF,channels:CHANNELS.length,quantum:1/256,clips:provenance,shots,sources,derived},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'bow loops;',JSON.stringify(derived));
