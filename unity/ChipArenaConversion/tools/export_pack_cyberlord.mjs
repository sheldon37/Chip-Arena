// Retargets the unarmed and martial takes of the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity
// asset) onto the voxel rig for the DEV Cyber Lord's presentation (Core/CyberLordMotion: the fighting stance and its run,
// the hardlight combo, the guard, the six abilities, the Kernel Panic choreography, the victim's tumble and the entrance).
// Development tool, presentation only. The pack's FBX and .meta files are never committed: point the script at an
// extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_cyberlord.mjs <pack>/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackCyberLord.Generated.cs (retargeted pose
// tracks in the 64-channel mocap encoding, Core/CapturedClip, with their marks) and tools/pack-cyberlord-provenance.json
// (each clip's source, frame range, size, marks and the SHA-256 of its encoded track and input file, which
// Tests/CyberLordChecks recomputes). The same input files give byte-identical output. The retarget is the melee
// converter's (scripts/mocap/mixamo-retarget.mjs through explosive-pack.mjs) with empty hands: one-handed (the support hand
// never grips), the stance's mean heading carried by the torso so the hips stay square over the gait's procedural feet.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'Unarmed'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_cyberlord.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose,packEvents}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const U=f=>`Unarmed/RPG-Character@Unarmed-${f}.FBX`;
/**
 * The takes, in Core/CyberLordMotion's slot order. `loop`: the take is a loop (sampled at 30 Hz); otherwise an action at
 * 60 Hz whose marks are [start, the pack's own impact (its Hit or Shoot clip event, else `hit`), end] in clip seconds.
 * `from`/`to` trim the take (pack frames at 30 Hz) where only part of it is wanted.
 */
const CLIPS=[
 // The fighting stance (fists up, bladed) and the hardlight guard; the run and sprint carry.
 {name:'stance',file:U('Idle-Alert1')},
 {name:'guard',file:U('Block'),loop:true},
 {name:'run',file:U('Run-Forward'),loop:true},
 {name:'sprint',file:U('Sprint'),loop:true},
 // The hardlight combo: lead jab, rear cross, the two-palm Pulse Palm thrust.
 {name:'jab',file:U('Attack-L1')},
 {name:'cross',file:U('Attack-R1')},
 {name:'palm',file:U('Cast-Dual-Attack1')},
 // Q Data Dash, E Firewall, 1 Packet Burst, 2 Siphon Link, 3 System Crash.
 {name:'dash',file:U('Dash-Forward'),hit:.2},
 {name:'firewall',file:U('Cast-Dual-Summon1')},
 {name:'burst',file:U('Cast-Dual-Attack2')},
 {name:'siphon',file:U('Cast-R-Attack2')},
 {name:'crash',file:U('Cast-Dual-AOE1')},
 // 4 Kernel Panic: the lunge opener, the Breaker, the rising kick, the hammer-fist and the final slam.
 {name:'lunge',file:U('Run-Forward-Attack1-Right')},
 {name:'breaker',file:U('Attack-R3')},
 {name:'kick',file:U('Attack-Kick-R1')},
 {name:'hammer',file:U('Air-Attack1')},
 {name:'slam',file:U('Cast-Dual-AOE2')},
 // The Kernel Panic victim: launched back, the knockdown, and the glitched stun.
 {name:'launched',file:U('Knockback-Back1')},
 {name:'knockdown',file:U('Knockdown1')},
 {name:'stunned',file:U('Stunned'),loop:true},
 // The entrance's power-up.
 {name:'boost',file:U('Boost1')},
];
const STANCE='stance';
// The pack mixes .fbx and .FBX: names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX';const cal=calibrate(packRestPose(resolve(REF)),{mirror:false,grip:'blade'});
function range(file){
 const meta=fs.readFileSync(resolve(file)+'.meta','utf8'),num=k=>{const m=meta.match(new RegExp('\\n\\s+'+k+': (.*)'));if(!m)throw new Error(file+'.meta: no '+k);return Number(m[1]);};
 return {first:num('firstFrame'),last:num('lastFrame'),loop:num('loopTime')===1};
}
const r4=x=>+x.toFixed(4),f32=x=>String(x)+'f';
const opts={oneHand:true,soften:true,poleTurn:10,splitTwist:true};
// The stance's mean heading, carried by the torso in every take (the melee converter's guard heading).
const stance=CLIPS.find(c=>c.name===STANCE),st=load(stance.file),sr=retarget(st.obj,st.clip,cal,30,{...opts}),heading=sr.reduce((s,r)=>s+r[0],0)/sr.length;
const lines=['// GENERATED by tools/export_pack_cyberlord.mjs from the owner\'s Explosive RPG Character pack: retargeted pose tracks only',
 '// (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackCyberLord {','public static readonly CapturedClip[] Clips = {'];
const provenance=[];
for(const c of CLIPS){
 const {obj,clip}=load(c.file),rg=range(c.file),loop=!!c.loop,fps=loop?30:60;
 if(loop&&!rg.loop)throw new Error(c.name+': the take does not loop');
 let rows=retarget(obj,clip,cal,fps,{...opts,heading,loop});
 const first=c.from??rg.first,last=c.to??rg.last,a=Math.round(first/30*fps),b=Math.round(last/30*fps);
 if(b>=rows.length||b-a<4)throw new Error(`${c.name}: frame range ${first}-${last} outside the take`);rows=rows.slice(a,b+1);
 for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(c.name+': non-finite channel');
 const end=r4((rows.length-1)/fps),events=[...packEvents(resolve(c.file),'Hit'),...packEvents(resolve(c.file),'Shoot')].map(t=>t-first/30).filter(t=>t>0&&t<end);
 const impact=events.length?r4(Math.min(...events)):c.hit!=null?r4(c.hit):null;
 const marks=loop?[0]:impact!=null?[0,impact,end]:[0,end];
 const data=encode(rows),source=path.basename(c.file).replace(/^RPG-Character@|\.fbx$/gi,'');
 lines.push('new CapturedClip('+[JSON.stringify('cyberlord/'+c.name),JSON.stringify(source),fps,rows.length,loop,'new float[] {'+marks.map(f32).join(',')+'}','new float[] {}',JSON.stringify(data)].join(',')+'),');
 provenance.push({clip:'cyberlord/'+c.name,source,range:[first,last],fps,frames:rows.length,loop,marks,heading:r4(heading),sha256:createHash('sha256').update(data).digest('hex'),input:createHash('sha256').update(fs.readFileSync(resolve(c.file))).digest('hex')});
 console.log(c.name.padEnd(10),source.padEnd(28),'frames',rows.length,'marks',marks.join(' '));
}
lines.push('};','public static readonly string[] Names = {'+CLIPS.map(c=>JSON.stringify(c.name)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackCyberLord.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-cyberlord-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,heading:r4(heading),clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'Cyber Lord clips, stance heading',heading.toFixed(3));
