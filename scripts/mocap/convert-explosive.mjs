// Converts the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity asset) into per-weapon mocap
// tables for the one-handed melee weapons (development tool). The FBX files are never committed; point the script at the
// pack's extracted Animations folder:
//   node scripts/mocap/convert-explosive.mjs <pack>/Animations [--preview <out.ts> [--events <out.json>]]
// writes src/client/mocap/{sword,shield,daggers}-clips.ts (deterministic for the same input files). --preview writes every
// candidate clip (raw clip time) for the visual lab instead (tools/visual-lab: mocap=<key>&support=0).
import fs from 'node:fs';
import path from 'node:path';
import {calibrate,retarget,encode,leftFootStrike,CHANNELS} from './mixamo-retarget.mjs';
import {loadPackFbx,packRestPose,packEvents} from './explosive-pack.mjs';

const args=process.argv.slice(2),dir=args[0],previewAt=args.indexOf('--preview'),preview=previewAt>=0?args[previewAt+1]:undefined,eventsAt=args.indexOf('--events'),eventsOut=eventsAt>=0?args[eventsAt+1]:undefined;
if(!dir||!fs.existsSync(path.join(dir,'Armed'))){console.error('usage: node scripts/mocap/convert-explosive.mjs <pack>/Animations [--preview out.ts]');process.exit(1);}
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const A=f=>`Armed/RPG-Character@Armed-${f}.FBX`,S=f=>`Armed-Shield/RPG-Character@Armed-Shield-${f}`,SW=f=>`1Hand-Sword/RPG-Character@Sword-Attack-${f}.FBX`,D=f=>`1Hand-Dagger/RPG-Character@Dagger-Attack-${f}.FBX`;
/** Per-weapon sets. `mirror` reads a left-arm capture onto the right arm (the game's shield is a right-hand weapon); grip
 * 'shield' orients the strapped shield on the fist (calibrate). Attack marks are [start, active start, active end, settled]
 * in source seconds; mocap-layer retimes them onto the weapon's [0, windup, windup + window, + recovery], so the active span
 * lands on the server's hit window. Active spans: the blade tip inside the weapon's front arc around the clip's authored
 * Hit marker (a thrust: Hit ± ~.04 s), at least .04 s. Every pack attack begins in the guard idle, so cuts start on
 * the clip's first frame and the windup plays the whole captured anticipation, paced (paceKnots) to fit the game's much
 * shorter windups; settled is back at the guard.
 * Sweep sides follow the server's (simulation.ts melee): sword 1 forehand (right to left), 2 backhand, 3 a narrow overhead;
 * daggers alternate right / left hand; the shield sweeps one way. */
const R={grip:'reverse',leftBlade:true};
export const SETS={
 sword:{name:'SWORD_CLIPS',clips:{
  idle:{file:A('Idle'),loop:true},
  walk:{file:A('Walk'),loop:true,strike:true},
  run:{file:A('Run-Forward'),loop:true,strike:true},
  // Forehand at the waist, right to left through the front.
  cutA:{file:SW('R1'),marks:[0,.30,.335,.80]},
  // Backhand at the waist, left to right.
  cutB:{file:SW('R3'),marks:[0,.33,.37,.80]},
  // The narrow overhead (server arc .8, high): a high diagonal forehand chopping down across the front. Every cut is a
  // right-hand clip: the pack's left-hand clips start from the mirrored guard, so entering one from the guard swaps the
  // arms through each other in a frame or two.
  cutC:{file:SW('R6'),marks:[0,.295,.335,.80]},
  block:{file:A('Block-R'),loop:true,marks:[0,.1,.2]},
  blockHit:{file:A('Block-R-GetHit1'),marks:[0,.15,.5]},
  stagger:{file:A('GetHit-F1'),marks:[0,.2,.5]},
 }},
 daggers:{name:'DAGGERS_CLIPS',clips:{
  idle:{file:A('Idle'),loop:true,...R},
  walk:{file:A('Walk'),loop:true,strike:true,...R},
  run:{file:A('Run-Forward'),loop:true,strike:true,...R},
  // Reverse (ice-pick) grip in both fists: the pack's dagger set stabs downward, which only reads with the blade under the
  // fist (a forward grip points the blades up through every stab and lunge).
  // 1: right-hand thrust; 2: left-hand thrust; 3: right-hand downward stab; 4: the left-hand thrust again under the full
  // body turn (MOCAP_SETS spin). Only unmirrored clips: each starts from the guard the idle holds.
  cutA:{file:D('R1'),...R,marks:[0,.51,.58,.88]},
  cutB:{file:D('L1'),...R,marks:[0,.43,.50,.80]},
  cutC:{file:D('R2'),...R,marks:[0,.425,.49,.79]},
  cutD:{file:D('L1'),...R,marks:[0,.43,.50,.80]},
  block:{file:A('Block-Dual'),loop:true,...R,marks:[0,.1,.2]},
  blockHit:{file:A('Block-Dual-GetHit1'),...R,marks:[0,.15,.5]},
  stagger:{file:A('GetHit-F1'),...R,marks:[0,.2,.5]},
 }},
 shield:{name:'SHIELD_CLIPS',clips:{
  idle:{file:S('Idle.fbx'),mirror:true,grip:'shield',loop:true},
  walk:{file:S('Walk.fbx'),mirror:true,grip:'shield',loop:true,strike:true},
  run:{file:S('Run-Forward.fbx'),mirror:true,grip:'shield',loop:true,strike:true},
  // 1: a lunging punch with the shield face (the lead foot planted through the contact); 2: a leaping rising bash.
  cutA:{file:S('Attack3.fbx'),mirror:true,grip:'shield',marks:[0,.467,.542,1.06]},
  cutB:{file:S('Attack1.fbx'),mirror:true,grip:'shield',marks:[0,.325,.425,1.02]},
  block:{file:S('Block.fbx'),mirror:true,grip:'shield',loop:true,marks:[0,.1,.2]},
  blockHit:{file:S('Block-GetHit1.FBX'),mirror:true,grip:'shield',marks:[0,.15,.5]},
 }},
};

/** Candidate clips for the lab (--preview): key → {file, mirror, loop}. */
const PREVIEW=[
 ...['Idle','Walk','Run-Forward','Block-R','Block-L','Block-Dual','Block-R-GetHit1','Block-R-GetHit2','Block-Dual-GetHit1','Block-Dual-GetHit2','GetHit-F1','GetHit-F2','Attack-Dual1','Attack-Dual2','Attack-Dual3','Stunned'].map(f=>[`Armed-${f}`,{file:A(f)}]),
 ...['L1','L2','L3','L4','L5','L6','L7','R1','R2','R3','R4','R5','R6','R7'].flatMap(f=>[[`Sword-${f}`,{file:SW(f)}],...(f[0]==='L'?[[`Sword-${f}.m`,{file:SW(f),mirror:true}]]:[])]),
 ...['L1','L2','L3','R1','R2','R3'].flatMap(f=>[[`Dagger-${f}`,{file:D(f)}],[`Dagger-${f}.m`,{file:D(f),mirror:true}],[`Dagger-${f}.r`,{file:D(f),grip:'reverse',leftBlade:true}],[`Dagger-${f}.m.r`,{file:D(f),mirror:true,grip:'reverse',leftBlade:true}]]),
 ...['Idle','Block-Dual'].map(f=>[`Armed-${f}.r`,{file:A(f),grip:'reverse',leftBlade:true}]),
 ...['Idle','Walk','Run-Forward','Attack1','Attack2','Attack3','Block','Block-GetHit1','Block-GetHit2'].map(f=>[`Shield-${f}.m`,{file:S(f+'.fbx'),mirror:true,grip:'shield'}]),
 // Two-handed sword (support hand on the grip): candidates for the greatsword.
 ...['Idle','Walk','Run-Forward','Attack1','Attack2','Attack3','Attack4','Attack5','Attack6','Block','Block-GetHit1','GetHit-F1'].map(f=>[`2H-${f}`,{file:`2Hand-Sword/RPG-Character@2Hand-Sword-${f}.FBX`,twoHand:true}]),
];

// The pack mixes .fbx and .FBX (even within a folder): names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map();
const load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
// Calibrate on the avatar T-pose (every file's .meta carries the same skeleton), per reading direction and grip.
const rest=f=>packRestPose(resolve(f)),REF=SW('R1'),cals=new Map();
const calFor=(mirror,grip)=>{const k=mirror+grip;if(!cals.has(k))cals.set(k,calibrate(rest(REF),{mirror,grip}));return cals.get(k);};
// Attacks are sampled at 60 fps: the pack's strikes turn the blade 100°+ between its 30 fps keys, and three's slerp between
// the keys (the mixer's quaternion tracks) carries the true in-between directions that the runtime's interpolation of
// blade vectors would cut across.
const fpsFor=(key,c)=>c.fps||(/Attack|Sword-|Dagger-/.test(key)||/Attack|Sword-|Dagger-/.test(c.file)?60:30);
/** Windup pace keys (MocapClip.pace): clip times splitting [t0, t1] into `knots`+1 equal shares of a paced path whose
 * density is the captured pose speed to the power `alpha` (hands and blades as the rig places them, the heading): with
 * alpha 1 the windup plays at an even pose speed, with 0 at the capture's own timing. Speeds under .5 rad/s count as .5
 * (a hold still takes a little time). */
const PACE_ALPHA=.8,ix=n=>CHANNELS.indexOf(n),VEC={grip:ix('grip.x'),blade:ix('blade.x'),lhand:ix('lhand.x'),lfwd:ix('lfwd.x')},YAW=ix('yaw');
const ang=(a,b,i)=>{const d=a[i]*b[i]+a[i+1]*b[i+1]+a[i+2]*b[i+2],l=Math.hypot(a[i],a[i+1],a[i+2])*Math.hypot(b[i],b[i+1],b[i+2]);return Math.acos(Math.max(-1,Math.min(1,d/(l||1))));};
const dist=(a,b,i)=>Math.hypot(a[i]-b[i],a[i+1]-b[i+1],a[i+2]-b[i+2]);
function paceKnots(rows,fps,t0,t1,{alpha=PACE_ALPHA,knots=7}={}){
 const S=[0];for(let i=0;i+1<rows.length;i++){const a=rows[i],b=rows[i+1];
  const v=Math.max(dist(a,b,VEC.grip)/.25,ang(a,b,VEC.blade),dist(a,b,VEC.lhand)/.25,ang(a,b,VEC.lfwd),Math.abs(b[YAW]-a[YAW]))*fps;
  S.push(S[i]+Math.max(.5,v)**alpha/fps);}
 const at=t=>{const x=Math.min(rows.length-1,Math.max(0,t*fps)),i=Math.min(rows.length-2,Math.floor(x));return S[i]+(S[i+1]-S[i])*(x-i);};
 const s0=at(t0),s1=at(t1),out=[];
 for(let k=1;k<=knots;k++){const s=s0+(s1-s0)*k/(knots+1);let i=0;while(i+1<S.length-1&&S[i+1]<s)i++;const t=(i+(s-S[i])/(S[i+1]-S[i]))/fps;out.push(+Math.min(t1,Math.max(t0,t)).toFixed(4));}
 return out;
}
const row=(c,oneHand,key='')=>{const {obj,clip}=load(c.file),mirror=!!c.mirror,fps=fpsFor(key,c),rows=retarget(obj,clip,calFor(mirror,c.grip||'blade'),fps,{mirror,oneHand,leftBlade:!!c.leftBlade,soften:true,poleTurn:10,splitTwist:true});
 const m=c.strike?[leftFootStrike(obj,clip,60,{mirror})]:c.marks,marks=m?`,marks:[${m.join(',')}]`:'';
 const pace=/^cut/.test(key)?`,pace:[${paceKnots(rows,fps,c.marks[0],c.marks[1]).join(',')}]`:'';
 return `{fps:${fps},frames:${rows.length}${c.loop?',loop:true':''}${marks}${pace},source:${JSON.stringify(path.basename(c.file).replace(/^RPG-Character@|\.fbx$/gi,'')+(mirror?' (mirrored)':''))},data:'${encode(rows)}'}`;};

if(preview){
 let out=`// GENERATED by scripts/mocap/convert-explosive.mjs --preview from the Explosive RPG Character pack. Do not edit.\n`+
  `// Channels: ${CHANNELS.length} (src/client/mocap/mocap-clip.ts MOCAP_CHANNELS).\nimport type {MocapClip} from '../../src/client/mocap/mocap-clip.js';\n`+
  `export const PREVIEW_CLIPS:Record<string,MocapClip>={\n`;
 for(const [key,c] of PREVIEW)out+=` ${JSON.stringify(key)}:${row(c,!c.twoHand,key)},\n`;
 fs.writeFileSync(preview,out+'};\n');console.warn=quiet;console.log('wrote',preview,PREVIEW.length,'clips');
 // --events <out.json>: each preview clip's authored Hit markers (source seconds) and length, for clip selection.
 if(eventsOut)fs.writeFileSync(eventsOut,JSON.stringify(Object.fromEntries(PREVIEW.map(([key,c])=>[key,{file:c.file,mirror:!!c.mirror,hit:packEvents(resolve(c.file)),duration:load(c.file).clip.duration}])),null,1));
}else{
 for(const [spec,set] of Object.entries(SETS)){
  let out=`// GENERATED by scripts/mocap/convert-explosive.mjs from the Explosive RPG Character pack. Do not edit.\n`+
   `// Channels: ${CHANNELS.length} (src/client/mocap/mocap-clip.ts MOCAP_CHANNELS).\nimport type {MocapClip} from './mocap-clip.js';\n`+
   `export const ${set.name}:Record<string,MocapClip>={\n`;
  for(const [key,c] of Object.entries(set.clips))out+=` ${JSON.stringify(key)}:${row(c,true,key)},\n`;
  const target=path.resolve(path.dirname(new URL(import.meta.url).pathname),`../../src/client/mocap/${spec}-clips.ts`);
  fs.writeFileSync(target,out+'};\n');console.log('wrote',target,out.length,'bytes',Object.keys(set.clips).length,'clips');
 }
 console.warn=quiet;
}
