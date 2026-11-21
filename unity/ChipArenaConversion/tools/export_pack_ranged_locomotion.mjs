// Retargets the directional locomotion loops of the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed
// Unity asset) onto the voxel rig for the gun and magic routes' travel carry (Core/NativeRangedLocomotion; owner 2026-10-12:
// "Give both ranged and magic different run/sprint animations, theirs are currently too basic"): per carry the slow walk,
// the walk and seven strafe directions and the run in eight directions, plus the pack's Unarmed sprint, so strafes and
// backpedals show real strafe and backpedal cycles under the procedural feet. Development tool, presentation only. The
// pack's FBX and .meta files are never committed: point the script at an extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_ranged_locomotion.mjs <pack>/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackRangedLocomotion.Generated.cs (retargeted
// pose tracks in the 64-channel mocap encoding, Core/CapturedClip) and tools/pack-ranged-locomotion-provenance.json (each
// clip's source, frame range, size, left-foot strike and the SHA-256 of its track and input, which
// Tests/RangedLocomotionChecks recomputes). The same input files give byte-identical output. The retarget is the melee
// travel carry's (tools/export_pack_locomotion.mjs: heading left to the gait, the left foot's strike as phase 0), so the
// clips phase to the gait's stride.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'2Hand-Shooting'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_ranged_locomotion.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,leftFootStrike,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const A=f=>`Armed/RPG-Character@Armed-${f}.FBX`,U=f=>`Unarmed/RPG-Character@Unarmed-${f}.FBX`,G=f=>`2Hand-Shooting/RPG-Character@2Hand-Shooting-${f}.FBX`,T=f=>`2Hand-Staff/RPG-Character@2Hand-Staff-${f}.fbx`;
/** Sets in Core/NativeRangedLocoRules order:
 *  - pistol: the pack has no pistol locomotion; its walk is the Armed walk (a relaxed free arm, the gun hand the runtime's
 *    own carry) and its run the Unarmed run in eight directions (an athletic pump: light and agile);
 *  - shooting (rifle and machine gun): the 2Hand-Shooting walk, strafes and runs, the rifle held at the ready;
 *  - staff (the four schools and L33t H4x): the 2Hand-Staff walk with the staff, its strafes and runs, mirrored, as the
 *    game holds the staff in the left hand (the pack's right) and casts with the right: its left and right clips swap.
 * The arms keep only their capture's hand targets (the runtime holds the guns and staff itself); one-handed throughout. */
const SETS=[
 {name:'pistol',slow:A('Walk-Slow'),walk:d=>d==='Forward'?A('Walk'):A('Strafe-'+d),run:d=>U('Run-'+d)},
 {name:'shooting',slow:G('Walk-Slow'),walk:d=>d==='Forward'?G('Walk'):G('Strafe-'+d),run:d=>G('Run-'+d)},
 {name:'staff',slow:T('Walk-Slow-WithStaff'),walk:d=>d==='Forward'?T('Walk-WithStaff'):T('Strafe-'+d),run:d=>T('Run-'+d),mirror:true,leftBlade:true},
];
/** The sprint every route's sprint torso keeps the cadence and lean of (Core/NativeSprintMotion), and the pistol's pump. */
const SPRINT=U('Sprint');
/** Slots: slow (Walk-Slow), then the walk family by direction from the facing, counter-clockwise (to the rig's left) in
 * 45 degree steps (Walk ahead, then the strafes), then the run family in the same order (Core/NativeLocoRules). */
const DIRS=['Forward','Forward-Left','Left','Backward-Left','Backward','Backward-Right','Right','Forward-Right'];
const SLOTS=['slow',...DIRS.map(d=>'walk-'+d.toLowerCase()),...DIRS.map(d=>'run-'+d.toLowerCase())];
const swap=d=>d.replace(/Left|Right/g,m=>m==='Left'?'Right':'Left');
function fileOf(set,slot){
 if(slot==='slow')return set.slow;
 const [family,...rest]=slot.split('-'),d=DIRS[DIRS.findIndex(x=>x.toLowerCase()===rest.join('-'))],dd=set.mirror?swap(d):d;
 return family==='walk'?set.walk(dd):set.run(dd);
}
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',cals=new Map();
const calFor=mirror=>{if(!cals.has(mirror))cals.set(mirror,calibrate(packRestPose(resolve(REF)),{mirror,grip:'blade'}));return cals.get(mirror);};
function range(file){
 const meta=fs.readFileSync(resolve(file)+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
const provenance=[];
/** One loop as a CapturedClip constructor line (and its provenance entry). */
function clipLine(name,file,{mirror=false,leftBlade=false}={}){
 const {obj,clip}=load(file),rg=range(file),fps=30,cal=calFor(mirror);
 if(!rg.loop)throw new Error(file+': not a loop');
 let rows=retarget(obj,clip,cal,fps,{mirror,oneHand:true,leftBlade,soften:true,poleTurn:10,splitTwist:true,heading:0,loop:true});
 const a=Math.round(rg.first/30*fps),b=Math.round(rg.last/30*fps);if(b>=rows.length||b-a<4)throw new Error(`${name}: frame range outside the take`);rows=rows.slice(a,b+1);
 for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${name}: non-finite channel`);
 const strike=+Math.min(leftFootStrike(obj,clip,60,{mirror}),(rows.length-1)/fps).toFixed(4),data=encode(rows),source=path.basename(file).replace(/^RPG-Character@|\.fbx$/gi,'')+(mirror?' (mirrored)':'');
 provenance.push({clip:name,source,range:[rg.first,rg.last],fps,frames:rows.length,loop:true,strike,sha256:createHash('sha256').update(data).digest('hex'),input:createHash('sha256').update(fs.readFileSync(resolve(file))).digest('hex')});
 return 'new CapturedClip('+[JSON.stringify(name),JSON.stringify(source),fps,rows.length,true,'new float[] {'+String(strike)+'f}','new float[] {}',JSON.stringify(data)].join(',')+')';
}
const lines=['// GENERATED by tools/export_pack_ranged_locomotion.mjs from the owner\'s Explosive RPG Character pack: retargeted pose',
 '// tracks only (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackRangedLocomotion {','public static readonly CapturedClip[][] Sets = {'];
for(const set of SETS){
 lines.push('new CapturedClip[] {');
 for(const slot of SLOTS)lines.push(clipLine(set.name+'/'+slot,fileOf(set,slot),set)+',');
 lines.push('},');
 console.log(set.name,SLOTS.length,'loops');
}
lines.push('};','public static readonly CapturedClip Sprint = '+clipLine('sprint',SPRINT)+';');
lines.push('public static readonly string[] Names = {'+SETS.map(s=>JSON.stringify(s.name)).join(', ')+'};','public static readonly string[] Slots = {'+SLOTS.map(s=>JSON.stringify(s)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackRangedLocomotion.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-ranged-locomotion-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'locomotion loops');
