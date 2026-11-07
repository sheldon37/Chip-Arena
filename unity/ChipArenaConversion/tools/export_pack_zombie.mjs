// Retargets the owner's MoCap Online "ZOMBIE PRO" pack (a licensed Unity asset) onto the voxel rig for the Siren's horde
// (Core/NativeZombieMotion): the walk, chase and sprint cycles, idles, attacks, knockbacks, deaths, the vent and door
// entrances and the brute's jump-down. Development tool, presentation only. The pack's FBX and .meta files are never
// committed: point the script at an extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_zombie.mjs <pack>/Assets/Zombie_27
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackZombie.Generated.cs (retargeted pose
// tracks in the 64-channel mocap encoding, Core/CapturedClip) and tools/pack-zombie-provenance.json (each clip's source,
// frame range, marks and the SHA-256 of its track, which Tests/ZombieMotionChecks recomputes). The same input files give
// byte-identical output. The retarget is the melee converter's (scripts/mocap/mixamo-retarget.mjs, through the pack's
// profile scripts/mocap/zombie-pack.mjs), both hands free.
//  - Cycles (walks, chases, sprints, idles): one gait cycle cut from the pack's loop, left foot strike to left foot strike
//    (the stride phase 0 the gait plants on), its seam spread over the cycle so the loop closes. Marks: [natural ground
//    speed on the rig (m/s), left lift, right strike, right lift] (the three as fractions of the cycle), read from the
//    pack's root-motion twin where the feet really plant.
//  - One-shots (attacks, knockbacks, deaths, entrances, the jump): the frame range given. Marks are clip seconds: an
//    attack's [start, wind-up peak, impact, settled], a death's [start, floor contact, end], the jump's per clip.
//    Deaths keep the capture's whole forward fold (retarget keepFold: the body lies flat on its face, legs along the floor).
//  - Every clip's channels are made continuous for the runtime's interpolation (continuous(): the same poses).
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'Animation/In_Place'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_zombie.mjs <pack>/Assets/Zombie_27');process.exit(1);}
const {calibrate,retarget,encode,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadZombieFbx,zombieRestPose,zombieRange}=await import(pathToFileURL(path.join(repo,'scripts/mocap/zombie-pack.mjs')));
const T=await import('three');
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const IP=f=>`Animation/In_Place/${f}_IPC.fbx`,RM=f=>`Animation/Root_Motion/${f}.fbx`;
/** The clips, in Core/NativeZombieMotion order. kind: cycle (a gait cycle from a loop), once (a one-shot frame range,
 * `range` in 30 Hz pack frames, the whole take without one). `cycle` picks the n-th left-foot cycle of the loop instead of
 * the one whose ends match best. `marks` override a one-shot's detected marks (clip seconds). */
const CLIPS=[
 // ---- idles (standing sways; the feet stay planted). Only the takes the kinds' sets use are exported. ----
 {key:'idle-3',file:'Zombie_Idle_3',kind:'idle',fps:15,range:[0,180]},
 {key:'shamble-1',file:'Zombie_Shamble_1',kind:'idle',fps:15},
 {key:'shamble-2',file:'Zombie_Shamble_2',kind:'idle',fps:15},
 // ---- walks (slow), chases (the lurching walk the horde closes in with), sprints (HyperChase) ----
 ...[4,6].map(i=>({key:'walk-'+i,file:`Zombie_Walk_F_${i}_Loop`,kind:'cycle'})),
 ...[1,2,5].map(i=>({key:'chase-'+i,file:`Zombie_Chase_${i}_Loop`,kind:'cycle'})),
 ...[1,3,4].map(i=>({key:'sprint-'+i,file:`Zombie_HyperChase_${i}_Loop`,kind:'cycle'})),
 // ---- attacks: one blow cut from each loop; marks [start, wind-up peak, impact, settled] (s) ----
 {key:'swipe-r1',file:'Zombie_Atk_Arm_1_R_SHORT_Loop',kind:'once',range:[0,21],marks:[0,.1,.22,.7]},
 {key:'swipe-l1',file:'Zombie_Atk_Arm_1_L_SHORT_Loop',kind:'once',range:[0,18],marks:[0,.06,.2,.6]},
 {key:'swipe-r2',file:'Zombie_Atk_Arm_2_R_SHORT_Loop',kind:'once',range:[0,18],marks:[0,.07,.2,.6]},
 {key:'swipe-l2',file:'Zombie_Atk_Arm_2_L_SHORT_Loop',kind:'once',range:[0,15],marks:[0,.06,.18,.5]},
 // the two-handed hammer: raised from the loop's end, then brought down (the range runs on through the loop's start)
 {key:'smash-1',file:'Zombie_Atk_Arms_4_SHORT_Loop',kind:'once',range:[54,100],marks:[0,.53,1.03,1.53]},
 {key:'smash-2',file:'Zombie_Atk_Arms_4A_SHORT_Loop',kind:'once',range:[27,65],marks:[0,.42,.82,1.26]},
 {key:'lunge-1',file:'Zombie_HyperAttack_3_SHORT_Loop',kind:'once',range:[0,17],marks:[0,.1,.22,.55]},
 {key:'lunge-2',file:'Zombie_HyperAttack_1_SHORT_Loop',kind:'once',range:[0,15],marks:[0,.1,.21,.5]},
 // ---- knockbacks (the reel of a blow that stops an attack) ----
 ...[1,2,3,4,5].map(i=>({key:'knock-'+i,file:`Zombie_Atk_KnockBack_${i}`,kind:'once',range:[0,36]})),
 // ---- deaths, by the way the body falls (marks [start, the head on the floor, end]) ----
 ...[1,2,3].map(i=>({key:'death-back-'+i,file:`Zombie_Death_Back_Mid_${i}`,kind:'once',fps:15,death:true})),
 ...[1,2].map(i=>({key:'death-front-'+i,file:`Zombie_Death_Forward_${i}`,kind:'once',fps:15,death:true})),
 {key:'death-front-3',file:'Zombie_Death_Left_2',kind:'once',fps:15,death:true},
 {key:'death-left-1',file:'Zombie_Death_Left_1',kind:'once',fps:15,death:true},
 ...[1,2].map(i=>({key:'death-right-'+i,file:`Zombie_Death_Right_${i}`,kind:'once',fps:15,death:true})),
 // ---- entrances: the door's wake, the vent's last haul to standing, the dive into a vent ----
 {key:'wake',file:'MOB_Stand_to_Zombie_Idle_1_v2',kind:'once',fps:15},
 {key:'crawl-stand',file:'Zombie_Crawl_1_to_Stand',kind:'once',fps:15,range:[99,200]},
 {key:'stand-crawl',file:'Zombie_Stand to_Crawl_1',kind:'once',range:[0,48]},
 // ---- the brute's jump-down: the leap, the fall, the landing ----
 {key:'jump-start',file:'Split_Jumps/Zombie_HyperChase_5_Jump_Start',kind:'once'},
 {key:'jump-air',file:'Split_Jumps/Zombie_HyperChase_5_Jump_Air',kind:'once',range:[0,30]},
 {key:'jump-land',file:'Split_Jumps/Zombie_HyperChase_5_Jump_End_HA1',kind:'once',land:true},
 // ---- the Central Station's kinds (docs/EXPEDITION-CENTRAL-STATION.md section 7; Core/NativeStationMotion.cs) ----
 // the Linesman's electric twitch (the flail's first second, laid over its idle) and its electrocuted collapse
 {key:'writhe-1',file:'Zombie_OnFire_1_SHORT',kind:'once',fps:15,range:[0,33]},
 {key:'death-shock',file:'Zombie_OnFire_2A_SHORT',kind:'once',fps:15,death:true},
 // the Conductor's upright idle; the reach under the arc's raised arm and the last call's raised lantern
 {key:'idle-1',file:'Zombie_Idle_1_v2',kind:'idle',fps:15},
 {key:'reach-1',file:'Zombie_Reach_1_Loop',kind:'once',fps:15,range:[0,66]},
 // the Undertow: one belly pull (its drag, played backward) and one lunging crawl stroke (its swim), looped by the motion
 {key:'crawl-1',file:'Zombie_Crawl_1_Loop',kind:'once',fps:15,range:[123,197]},
 {key:'crawl-2',file:'Zombie_Crawl_2_Loop',kind:'once',fps:15,range:[16,67]},
 // the Pileup's two-handed shove: the crouched brace, the push to full extension, the return; marks [start, braced, impact, settled]
 {key:'shove-1',file:'Zombie_Atk_Arms_4_SHORT_Loop',kind:'once',range:[18,50],marks:[0,.1,.7,1.0667]},
 // ---- the locomotion pass (docs/ZOMBIE-LOCOMOTION.md): the smoothest cycles of the pack's full-length loops for the shambler's
 // reaching walk, the brute's, spitter's and bloater's heavy walk and the brute's lurching charge, and the runner's low-armed sprint ----
 {key:'chase-1-full',file:'Zombie_Chase_1_Full_Loop',kind:'cycle'},
 {key:'chase-5-full',file:'Zombie_Chase_5_Full_Loop',kind:'cycle'},
 {key:'walk-6-full',file:'Zombie_Walk_F_6_Full_Loop',kind:'cycle'},
 {key:'sprint-2',file:'Zombie_HyperChase_2_Loop',kind:'cycle'},
];

const file=f=>{const p=path.join(dir,f);if(!fs.existsSync(p))throw new Error('missing pack file '+f);return p;};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadZombieFbx(file(f)));return cache.get(f);};
const cal=calibrate(zombieRestPose(file('MotusMan_v55/MotusMan_v55.fbx')),{grip:'blade'});
const OPTS={oneHand:true,leftBlade:true,soften:true,poleTurn:10,splitTwist:true,canonTwist:true};
const FPS=30,r4=x=>+x.toFixed(4),f32=x=>String(x)+'f';
const ix=n=>CHANNELS.indexOf(n);

/** Per frame (FPS): the hips' ground position and each toe's and ankle's world position (rig metres), and whether each foot is
 * planted (its toe or ankle still and low). */
function feet(obj,clip,n){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const B=k=>obj.getObjectByName('mixamorig'+k),wp=k=>B(k).getWorldPosition(new T.Vector3());
 mixer.setTime(0);obj.updateMatrixWorld(true);const k=cal.legScale;const out=[];
 for(let i=0;i<n;i++){mixer.setTime(Math.min(clip.duration-1e-4,i/FPS));obj.updateMatrixWorld(true);out.push({hips:wp('Hips').multiplyScalar(k),l:[wp('LeftToeBase').multiplyScalar(k),wp('LeftFoot').multiplyScalar(k)],r:[wp('RightToeBase').multiplyScalar(k),wp('RightFoot').multiplyScalar(k)]});}
 const planted=side=>out.map((f,i)=>{const a=out[Math.max(0,i-1)][side],b=out[Math.min(n-1,i+1)][side];return [0,1].some(j=>b[j].clone().sub(a[j]).setY(0).length()*FPS/2<.22&&f[side][j].y<.16);});
 return {frames:out,l:planted('l'),r:planted('r')};
}
/** A gait cycle: the loop's left-foot strikes on its root-motion twin; the cycle whose ends match best (or the n-th). */
function cycleOf(spec,rows){
 const rm=load(RM(spec.file)),n=rows.length,f=feet(rm.obj,rm.clip,n);
 const strikes=side=>{const s=[];for(let i=1;i<n;i++)if(f[side][i]&&!f[side][i-1])s.push(i);return s;};
 const L=strikes('l'),R=strikes('r');if(L.length<2)throw new Error(spec.key+': fewer than two left strikes');
 const cost=(a,b)=>{let s=0;for(let c=0;c<rows[a].length;c++)s+=Math.abs(rows[a][c]-rows[b][c]);return s;};
 let best=-1,bestCost=Infinity;for(let i=0;i+1<L.length;i++){const len=L[i+1]-L[i];if(len<8)continue;const c=cost(L[i],L[i+1]);if(spec.cycle===i||spec.cycle==null&&c<bestCost){best=i;bestCost=c;}}
 if(best<0)throw new Error(spec.key+': no usable cycle');
 const a=L[best],b=L[best+1],len=b-a;
 const lift=side=>{for(let i=a+1;i<b;i++)if(!f[side][i]&&f[side][i-1])return (i-a)/len;return .6;};
 const rLand=R.find(i=>i>a&&i<b),rl=rLand==null?.5:(rLand-a)/len;let rLift=.1;for(let i=(rLand??a)+1;i<b+len;i++){const k=Math.min(n-1,i);if(!f.r[k]&&f.r[Math.max(0,k-1)]){rLift=((i-a)/len)%1;break;}}
 const travel=f.frames[b].hips.clone().sub(f.frames[a].hips).setY(0).length(),speed=travel/(len/FPS);
 return {a,b,marks:[r4(speed),r4(lift('l')),r4(rl),r4(rLift)]};
}
/** A death's floor contact (s): the first frame the head is under 30 cm on the rig. */
function headDown(obj,clip){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const head=obj.getObjectByName('mixamorigHead'),n=Math.round(clip.duration*30);
 for(let i=0;i<=n;i++){mixer.setTime(Math.min(clip.duration-1e-4,i/30));obj.updateMatrixWorld(true);if(head.getWorldPosition(new T.Vector3()).y*cal.legScale<.3)return r4(i/30);}
 return r4(clip.duration*.5);
}
/** A landing's deepest compression (s): the lowest hips in its first 60 %. */
function deepest(rows,fps){const by=ix('by');let low=0;for(let i=1;i<=Math.floor((rows.length-1)*.6);i++)if(rows[i][by]<rows[low][by]-1e-6)low=i;return r4(low/fps);}
/**
 * Continuous channels for the clip's interpolation (the runtime samples Catmull-Rom across frames): the pelvis's split into a
 * heading and a tilt is ambiguous once the body lies down (the heading can flip by half a turn or spin between frames, the
 * tilt making up for it), and a rotation vector near a half turn can flip to its equivalent on the other side. Each frame keeps
 * its pose exactly: the heading unwrapped to the nearest half turn of the last frame's and kept from spinning, the pelvis's tilt
 * re-derived from the same whole rotation, and every rotation vector the equivalent (the same rotation) nearest the last
 * frame's. Frames without a flip are left as they are (the cycles and attacks export unchanged).
 */
function continuous(rows){
 const Y=new T.Vector3(0,1,0),yaw=ix('yaw'),pel=ix('pelvis.x'),joints=[pel,...['spine','chest','neck','head','lclav','rclav','lhip','rhip','lankle','rankle'].map(j=>ix(j+'.x'))];
 const q=(r,i)=>{const v=new T.Vector3(r[i],r[i+1],r[i+2]),a=v.length();return a<1e-9?new T.Quaternion():new T.Quaternion().setFromAxisAngle(v.divideScalar(a),a);};
 const log=x=>{let w=x.w,v=new T.Vector3(x.x,x.y,x.z);if(w<0){w=-w;v.negate();}const s=v.length();if(s<1e-9)return v.set(0,0,0);return v.multiplyScalar(2*Math.atan2(s,w)/s);};
 const near=(v,prev)=>{const a=v.length();if(a<1e-6)return v;let best=v,d=v.distanceTo(prev);for(const k of[-1,1]){const u=v.clone().multiplyScalar((a+2*Math.PI*k)/a),e=u.distanceTo(prev);if(e<d-1e-6){best=u;d=e;}}return best;};
 for(let f=1;f<rows.length;f++){
  const r=rows[f],prev=rows[f-1];
  const whole=new T.Quaternion().setFromAxisAngle(Y,r[yaw]).multiply(q(r,pel));let h=r[yaw];
  // Any heading recomposes the same pelvis with its re-derived tilt: the nearest half turn of the last frame's, and a lying
  // body's heading (ill-defined while the pelvis faces up or down) kept from spinning, at most .35 rad a frame.
  let d=h-prev[yaw];d-=Math.PI*Math.round(d/Math.PI);d=Math.max(-.35,Math.min(.35,d));h=prev[yaw]+d;
  if(Math.abs(h-r[yaw])>1e-7){const tilt=log(new T.Quaternion().setFromAxisAngle(Y,-h).multiply(whole));r[yaw]=h;r[pel]=tilt.x;r[pel+1]=tilt.y;r[pel+2]=tilt.z;}
  for(const j of joints){const v=near(new T.Vector3(r[j],r[j+1],r[j+2]),new T.Vector3(prev[j],prev[j+1],prev[j+2]));r[j]=v.x;r[j+1]=v.y;r[j+2]=v.z;}
 }
 return rows;
}
/** An idle's cycle: the loop's whole take, its seam spread. Marks: [0, 1, .5, 1] (both feet planted throughout). */
const provenance=[],lines=['// GENERATED by tools/export_pack_zombie.mjs from the owner\'s MoCap Online ZOMBIE PRO pack: retargeted pose tracks only',
 '// (no FBX, mesh, texture or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackZombie {','public static readonly CapturedClip[] Clips = {'];
for(const spec of CLIPS){
 const src=IP(spec.file),{obj,clip}=load(src),rg=zombieRange(file(src)),fps=spec.fps??FPS,k=fps/30;
 let rows=retarget(obj,clip,cal,fps,{...OPTS,keepFold:!!spec.death,loop:spec.kind!=='once'||rg.loop});
 // A range past the loop's end runs on through its start (the take's last frame is its first).
 const a0=spec.range?spec.range[0]:rg.first,b0=spec.range?spec.range[1]:rg.last,A=Math.round(a0*k),B=Math.round(b0*k),L=Math.round(rg.last*k);
 if(B>L){if(!rg.loop)throw new Error(spec.key+': range past the end of a take that is not a loop');rows=[...rows.slice(A,L),...rows.slice(0,B-L+1)];}
 else rows=rows.slice(A,Math.min(rows.length-1,B)+1);
 rows=continuous(rows);
 let marks,loop=spec.kind!=='once',range=[a0,b0];
 if(spec.kind==='cycle'){const c=cycleOf(spec,rows);rows=rows.slice(c.a,c.b+1);marks=c.marks;range=[a0+c.a,a0+c.b];}
 else if(spec.kind==='idle')marks=[0,1,.5,1];
 else if(spec.death)marks=[0,headDown(obj,clip),r4((rows.length-1)/fps)];
 else if(spec.land)marks=[0,deepest(rows,fps),r4((rows.length-1)/fps)];
 else marks=spec.marks??[0,r4((rows.length-1)/fps)];
 if(loop){const last=rows.length-1,d=rows[last].map((v,c)=>v-rows[0][c]);rows=rows.map((r,i)=>Float32Array.from(r,(v,c)=>v-d[c]*i/last));}
 for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${spec.key}: non-finite channel`);
 const data=encode(rows),source=path.basename(src).replace(/\.fbx$/i,'');
 lines.push('new CapturedClip('+[JSON.stringify('zombie/'+spec.key),JSON.stringify(source),fps,rows.length,loop,'new float[] {'+marks.map(f32).join(',')+'}','new float[] {}',JSON.stringify(data)].join(',')+'),');
 provenance.push({clip:'zombie/'+spec.key,source,range,fps,frames:rows.length,loop,marks,sha256:createHash('sha256').update(data).digest('hex'),input:createHash('sha256').update(fs.readFileSync(file(src))).digest('hex')});
 console.log(spec.key.padEnd(18),'frames',String(rows.length).padStart(4),'marks',marks.join(' '));
}
lines.push('};','public static readonly string[] Keys = {'+CLIPS.map(s=>JSON.stringify(s.key)).join(', ')+'};','}','}');
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativePackZombie.Generated.cs'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(root,'tools/pack-zombie-provenance.json'),JSON.stringify({pack:'MoCap Online ZOMBIE PRO MoCap Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/256,clips:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'zombie clips');
