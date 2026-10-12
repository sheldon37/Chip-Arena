// Measures the owner's Explosive "RPG Character Mecanim Animation Pack" pistol takes (a licensed Unity asset) for the Fan
// the Hammer pose (Core/NativeGunKitMotion): the per-shot recoil profile of the gun shoulder around each authored Shoot event
// of the one-handed shots (Pistol-Attack-R1/R2/R3) and the support hand's peak speed and shot spacing of the dual-pistol
// volleys (Pistol-Attack-Dual1/2/3), retargeted onto the voxel rig with the melee converter's calibration. Development tool,
// presentation only: it writes derived numbers only (no pose track, FBX, mesh or .meta data). Point it at an extracted copy
// of the pack outside the repository:
//   node unity/ChipArenaConversion/tools/derive_fan_hammer.mjs <pack>/Animations
// It writes tools/fan-hammer-provenance.json (sources, Shoot events, the measured curves, the derived keys and the SHA-256 of
// each input file); Tests/GunKitChecks compares Core/NativeGunKitMotion's keys with it. The same inputs give the same output.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'1Hand-Pistol'))){console.error('usage: node unity/ChipArenaConversion/tools/derive_fan_hammer.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,CHANNELS}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose,packEvents}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const P=f=>`1Hand-Pistol/RPG-Character@Pistol-${f}.FBX`,REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',FPS=60;
const cal=calibrate(packRestPose(path.join(dir,REF)),{mirror:false,grip:'blade'});
const ix=n=>CHANNELS.indexOf(n),vec=(r,n)=>[0,1,2].map(k=>r[ix(n+'.'+'xyz'[k])]),dist=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);
const r4=x=>+x.toFixed(4),sha=f=>createHash('sha256').update(fs.readFileSync(path.join(dir,f))).digest('hex');
const take=f=>{const {obj,clip}=loadPackFbx(path.join(dir,f));return {rows:retarget(obj,clip,cal,FPS,{oneHand:true,soften:false}),shoot:packEvents(path.join(dir,f),'Shoot'),duration:r4(clip.duration)};};
// Frames after the Shoot event at which the profile is read (0 .. .25 s).
const FRAMES=[0,1,2,3,5,8,12,15];
const sources=[],kicks=[];
for(const name of ['Attack-R1','Attack-R2','Attack-R3']){
 const t=take(P(name));const shots=[];
 for(const s of t.shoot){const c=Math.round(s*FPS),pre=t.rows[c-1];
  // The gun shoulder's displacement (right clavicle rotation vector, radians) from the frame before the shot.
  const clav=FRAMES.map(k=>r4(dist(vec(t.rows[c+k],'rclav'),vec(pre,'rclav'))));shots.push({at:s,clavicle:clav});}
 sources.push({clip:'Pistol-'+name,duration:t.duration,shots,input:sha(P(name))});kicks.push(...shots);
}
const volleys=[];
for(const name of ['Attack-Dual1','Attack-Dual2','Attack-Dual3']){
 const t=take(P(name));let peak=0;
 // The support hand's peak speed (m/s) within .2 s of the volley's shots.
 const c0=Math.round(t.shoot[0]*FPS)-12,c1=Math.round(t.shoot[t.shoot.length-1]*FPS)+12;
 for(let i=Math.max(1,c0);i<Math.min(t.rows.length,c1);i++)peak=Math.max(peak,dist(vec(t.rows[i],'lhand'),vec(t.rows[i-1],'lhand'))*FPS);
 const gaps=t.shoot.slice(1).map((s,i)=>r4(s-t.shoot[i]));
 volleys.push({clip:'Pistol-'+name,duration:t.duration,shoot:t.shoot,gaps,supportPeak:r4(peak)});sources.push({clip:'Pistol-'+name,input:sha(P(name))});
}
// The derived recoil key: the cleanest single take (R3: the shot from a held aim, no aim swing after it) normalised to its
// peak; the rise is the mean of R1 and R3 normalised to theirs (R2's shot lands on its aim swing). A .45 s release to zero is
// added so a volley's stacked kicks settle (the takes return to their ready pose instead).
const r1=kicks.find(k=>k.at===sources[0].shots[0].at).clavicle,r3=sources[2].shots[0].clavicle;
const n=(a,upto)=>{const m=Math.max(...a.slice(0,upto));return a.map(x=>x/m);};
const rise1=n(r1,3),fall3=n(r3,FRAMES.length);
const times=[...FRAMES.map(k=>r4(k/FPS)),.45],values=[...FRAMES.map((k,i)=>r4(k<=2?(rise1[i]+fall3[i])/2:fall3[i])),0];
values[values.indexOf(Math.max(...values))]=1;
const speeds=volleys.map(v=>v.supportPeak),strike=r4(speeds.reduce((s,x)=>s+x,0)/speeds.length);
const out={pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',calibration:REF,fps:FPS,frames:FRAMES,sources,volleys,
 derived:{kickTimes:times,kickValues:values,supportStrikeSpeed:strike,note:'kick: R3 clavicle recoil normalised (rise: mean of R1 and R3), .45 s release added; supportStrikeSpeed: mean peak support-hand speed of the dual volleys (m/s)'}};
fs.writeFileSync(path.join(root,'tools/fan-hammer-provenance.json'),JSON.stringify(out,null,1)+'\n');
console.warn=quiet;console.log('kick',times.join(' '),'|',values.join(' '),'| strike',strike,'m/s');
