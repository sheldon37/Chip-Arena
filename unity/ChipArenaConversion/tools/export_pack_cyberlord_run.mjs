// Retargets the run, sprint and air-boost takes of the owner's CLazy "Runner Action Animation Pack" (a licensed Unity asset)
// onto the voxel rig for the DEV Cyber Lord's presentation (Core/CyberLordRun: the run in eight directions and the sprint
// in three, carried over the gait's planted procedural feet; the second jump's hardlight boost). Development tool,
// presentation only. The pack's FBX and .meta files are never committed: point the script at an extracted copy outside the
// repository:
//   node unity/ChipArenaConversion/tools/export_pack_cyberlord_run.mjs <pack>/Assets/CLazyRunnerActionAnimPack/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackCyberLordRun.Generated.cs (retargeted pose
// tracks in the 64-channel mocap encoding, Core/CapturedClip, with their marks) and tools/pack-cyberlord-run-provenance.json
// (each clip's source, frame range, size, marks and the SHA-256 of its encoded track and input files, which
// Tests/CyberLordRunChecks recomputes). The same input files give byte-identical output. The retarget is the melee
// converter's (scripts/mocap/mixamo-retarget.mjs through clazy-pack.mjs, the pack's mannequin skeleton) with empty hands
// and the heading left to the gait, as the melee travel carry (tools/export_pack_locomotion.mjs) does: a loop's phase 0 is
// its left foot's strike, measured on the pack's root-motion twin (its planted foot stands still in the world).
// Choice (docs/CYBER-LORD.md "Run and sprint"): the run is the Mvm_Dash set (7.6 m/s captured, the stride closest to the
// gait's at 6.8 m/s on the voxel legs), the sprint the Mvm_Boost set (10.1 m/s, a deeper drive at the 9.2 m/s sprint) with
// the same fist pump, so the sprint ramps without the arms changing their carry; the boost is Esc_BoostDash_Up_Fwd.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'P1_CLazyMovement'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_cyberlord_run.mjs <pack>/Assets/CLazyRunnerActionAnimPack/Animations');process.exit(1);}
const {calibrate,retarget,encode,leftFootStrike,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadClazyFbx,clazyRestPose,clazyRange}=await import(pathToFileURL(path.join(repo,'scripts/mocap/clazy-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const D=f=>`P1_CLazyMovement/Mvm_8way_Dash/CLazy@Mvm_Dash${f}`,B=f=>`P1_CLazyMovement/Mvm_8way_Boost/CLazy@Mvm_Boost${f}`;
/** The takes in Core/CyberLordRun's slot order: the run by direction from the facing, counter-clockwise (to the rig's left) in
 * 45 degree steps (NativeLocoRules' order: ahead, forward-left, left, back-left, back, back-right, right, forward-right; the
 * pack's Front_L / Front_R strafes turn the hips toward the travel, its Back_L45 / Back_R45 backpedal on the diagonals), the
 * sprint ahead and on the two forward diagonals (the authority sprints only within 53 degrees of the facing), then the
 * boost. `loop`: sampled at 30 Hz from the pack's loop range with the left-foot strike as its mark; otherwise an action at
 * 60 Hz whose marks are [start, the push-off (the last frame with a leg driven straight down: the less bent knee under .5 rad,
 * from which both knees tuck), end] in clip seconds. */
const CLIPS=[
 {name:'run-forward',file:D(''),loop:true},{name:'run-forward-left',file:D('_Front_L45'),loop:true},{name:'run-left',file:D('_Front_L'),loop:true},
 {name:'run-back-left',file:D('_Back_L45'),loop:true},{name:'run-back',file:D('_Back'),loop:true},{name:'run-back-right',file:D('_Back_R45'),loop:true},
 {name:'run-right',file:D('_Front_R'),loop:true},{name:'run-forward-right',file:D('_Front_R45'),loop:true},
 {name:'sprint-forward',file:B(''),loop:true},{name:'sprint-forward-left',file:B('_Front_L45'),loop:true},{name:'sprint-forward-right',file:B('_Front_R45'),loop:true},
 {name:'boost',file:'P2_CLazyEscape/Esc_BoostDash_UpDown/CLazy@Esc_BoostDash_Up_Fwd'},
];
// The pack mixes .fbx and .FBX: names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const want=(path.basename(f)+'.fbx').toLowerCase(),hit=listing.get(d).find(n=>n.toLowerCase()===want);if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cal=calibrate(clazyRestPose(resolve(CLIPS[0].file)),{mirror:false,grip:'blade'});
const r4=x=>+x.toFixed(4),f32=x=>String(x)+'f';
const opts={oneHand:true,soften:true,poleTurn:10,splitTwist:true,canonicalTwist:true,heading:0};
const sha=b=>createHash('sha256').update(b).digest('hex');
const lines=['// GENERATED by tools/export_pack_cyberlord_run.mjs from the owner\'s CLazy Runner Action Animation Pack: retargeted pose',
 '// tracks only (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackCyberLordRun {','public static readonly CapturedClip[] Clips = {'];
const provenance=[];
for(const c of CLIPS){
 const file=resolve(c.file),{obj,clip}=loadClazyFbx(file),rg=clazyRange(file),loop=!!c.loop,fps=loop?30:60;
 let rows=retarget(obj,clip,cal,fps,{...opts,loop});
 const a=Math.round(rg.first/30*fps),b=Math.round(rg.last/30*fps);
 if(b>=rows.length||b-a<4)throw new Error(`${c.name}: frame range ${rg.first}-${rg.last} outside the take`);rows=rows.slice(a,b+1);
 for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(c.name+': non-finite channel');
 const end=r4((rows.length-1)/fps);let marks,twin=null;
 if(loop){
  // The strike on the root-motion twin (the same take travelling): there the planted foot is still in the world.
  twin=file.replace(/\.fbx$/i,'_Root.FBX');if(!fs.existsSync(twin))throw new Error(c.name+': no root-motion twin');
  const t=loadClazyFbx(twin);marks=[Math.min(r4(leftFootStrike(t.obj,t.clip,60,{})-rg.first/30),end)];
 }
 else{
  const lk=CHANNELS.indexOf('lknee'),rk=CHANNELS.indexOf('rknee');let at=-1;rows.forEach((r,i)=>{if(Math.min(r[lk],r[rk])<.5)at=i;});
  if(at<0)throw new Error(c.name+': no push-off frame');marks=[0,r4(at/fps),end];
 }
 const data=encode(rows),source=path.basename(file).replace(/^CLazy@|\.fbx$/gi,'');
 lines.push('new CapturedClip('+[JSON.stringify('cyberlord-run/'+c.name),JSON.stringify(source),fps,rows.length,loop,'new float[] {'+marks.map(f32).join(',')+'}','new float[] {}',JSON.stringify(data)].join(',')+'),');
 const entry={clip:'cyberlord-run/'+c.name,source,range:[rg.first,rg.last],fps,frames:rows.length,loop,marks,sha256:sha(data),input:sha(fs.readFileSync(file))};
 if(twin)entry.strikeFrom=sha(fs.readFileSync(twin));provenance.push(entry);
 console.log(c.name.padEnd(22),source.padEnd(26),'frames',rows.length,'marks',marks.join(' '));
}
lines.push('};','public static readonly string[] Names = {'+CLIPS.map(c=>JSON.stringify(c.name)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackCyberLordRun.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-cyberlord-run-provenance.json'),JSON.stringify({pack:'CLazy Runner Action Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'Cyber Lord run clips');
