// Retargets and sequences clips of the owner's Explosive "RPG Character Mecanim Animation Pack" (a licensed Unity asset) into
// the pilot's body motion for the weapon-signature entrances (Core/NativeEntrancePack). Development tool, presentation only.
// The pack's FBX and .meta files are never committed: point the script at an extracted copy outside the repository:
//   node unity/ChipArenaConversion/tools/export_pack_intros.mjs <pack>/Animations
// It writes only under unity/ChipArenaConversion: Assets/ChipArena/Core/NativePackIntros.Generated.cs (one continuous pose
// track per entrance in the 64-channel mocap encoding of Core/CapturedClip, its foot plants and its hit) and
// tools/pack-intros-provenance.json (each entrance's clips, their source and target timing, the SHA-256 of every input file
// and of the encoded track, which Tests/PackIntroChecks recomputes). The same input files give byte-identical output.
// The retarget is the melee converter's (scripts/mocap/mixamo-retarget.mjs through explosive-pack.mjs): the same calibration,
// grips and two-handed hilt clearance as the gameplay melee clips. Each entrance is a sequence of segments: a source clip
// mapped onto the entrance clock through monotone timing keys ([entrance s, source s] pairs: trims, holds and the hit pinned
// to the sting's accent), cross-faded into the next over `blend` s, the next segment's root (heading and hips) continuing
// from where the previous one is at the blend start. Every channel is then turned and moved so the entrance ends at its hero
// facing over the stage origin, lifted so the planted feet meet the floor, and sampled at 60 Hz for the 4.8 s phase.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(here,'..'),repo=path.resolve(root,'../..');
const dir=process.argv[2];
if(!dir||!fs.existsSync(path.join(dir,'Armed'))){console.error('usage: node unity/ChipArenaConversion/tools/export_pack_intros.mjs <pack>/Animations');process.exit(1);}
const {calibrate,retarget,encode,CHANNELS,QUANT}=await import(pathToFileURL(path.join(repo,'scripts/mocap/mixamo-retarget.mjs')));
const {loadPackFbx,packRestPose}=await import(pathToFileURL(path.join(repo,'scripts/mocap/explosive-pack.mjs')));
const T=await import('three');
const quiet=console.warn;console.warn=()=>{};// FBXLoader warns about every unused FBX property

const A=f=>`Armed/RPG-Character@Armed-${f}.FBX`,H=f=>`2Hand-Sword/RPG-Character@2Hand-Sword-${f}.FBX`,SW=f=>`1Hand-Sword/RPG-Character@Sword-Attack-${f}.FBX`,R=f=>`Relax/RPG-Character@Relax-${f}.FBX`,
 SH=f=>`Armed-Shield/RPG-Character@Armed-Shield-${f}`,G=f=>`2Hand-Shooting/RPG-Character@2Hand-Shooting-${f}.FBX`,PI=f=>`1Hand-Pistol/RPG-Character@Pistol-${f}.FBX`;
/**
 * The entrances (NativeIntroRules weapon signatures; daggers keep their baked clip). `grip` and `twoHand` as the gameplay
 * set's (twoHand: the support hand holds the grip throughout and the hilt is kept clear of the head, pauldrons and torso;
 * grip 'gun': the barrel along the fist's metacarpals). `support`: [x, y, z, always] the support hand's grip in the weapon
 * hand's frame (RangedPose.Support for the guns), held while the capture holds the weapon in both hands, or throughout.
 * `hit` the sting's accent (NativeIntroMotion.Hit), `facing` the hero heading (rad, 0 faces +z) the entrance ends on, over
 * the stage origin; `breathe`: a held hero pose gets the procedural entrances' breathing from the name card (for a hold
 * the capture itself keeps still). Segment `keys`: [entrance s, source s] pairs (monotone; the first pair starts the segment); `blend`: the
 * cross-fade into it (s, from its first key); `loop`: the source wraps; `turn`: an extra heading (rad) on its root;
 * `keepYaw`: its heading is not continued from the segment before; `part: 'upper'`: only its torso and arms are layered
 * in (until `until`, then handed back over its blend). Entrance `air`: [entrance s, metres] keys of a leap; `rise`: the
 * same, applied over the feet once they are planted (a crouch held a little taller); `kick`: [entrance s, size] gun shots;
 * `aim`: [entrance s, rad] keys raising the weapon arm; `faceBy: 'chest'`: `facing` is the chest's heading.
 */
const ENTRANCES=[
 // Flash Draw (iaido): a calm relaxed stand, the blade coiled back across the body (the backhand's windup, slowed), the
 // backhand draw-cut sweeping left to right through the crescent on the 1.8 s hit, a power-up flourish raising the blade
 // overhead, then the blade raised in salute, the crouch held a little taller, through the name card.
 {style:'weapon-sword',grip:'blade',twoHand:false,hit:1.8,facing:-.25,breathe:true,rise:[[2.8,0],[3.2,.05],[4.8,.05]],seq:[
  {file:R('Unsheath-R-Hips'),keys:[[0,0],[1.2,.1]]},
  {file:SW('R2'),blend:.5,keys:[[.75,0],[1.45,.15],[1.64,.2],[1.8,.3156],[2.2,.62]]},
  {file:A('Boost1'),blend:.15,keys:[[2.1,.2],[2.55,.5],[2.85,.75]]},
  {file:A('Boost1'),blend:.3,keys:[[2.75,.04],[3.15,.09],[4.8,.12]]},
 ]},
 // Colossus Oath: the blade held up before the face in oath, a crouch gathering power, a leap with the blade raised
 // straight overhead (the jump's flight lifted .42 m), a two-handed chop that bites the floor on the 1.8 s hit as the pilot
 // lands in a deep crouch, the recovery, then the blade shouldered (the two-handed guard) through the name card.
 {style:'weapon-greatsword',grip:'blade',twoHand:true,hit:1.8,facing:-.25,air:[[1.2,0],[1.45,.42],[1.62,.36],[1.8,0]],seq:[
  {file:H('Idle-Alert1'),loop:true,keys:[[0,.15],[.8,.85]]},
  {file:H('Jump'),blend:.25,keys:[[.65,0],[1.12,.06],[1.25,.1],[1.62,.55]]},
  {file:H('Attack2'),blend:.1,keys:[[1.62,.52],[1.8,.62],[2.7,1]]},
  {file:H('Idle'),blend:.45,loop:true,keys:[[2.6,.3],[4.8,2.5]]},
 ]},
 // Bastion Drop: the shield at the side, a power-up crouch with the free fist raised, a hop (.22 m) that lands into the block
 // on the 1.45 s hit as the honeycomb barrier ripples out from it, the block held through the name card. The pack's
 // left-arm shield set is read mirrored onto the game's right hand, as the gameplay shield set.
 {style:'weapon-shield',grip:'shield',hit:1.45,facing:-.25,faceBy:'chest',air:[[1.05,0],[1.25,.22],[1.45,0]],seq:[
  {file:SH('Idle.fbx'),mirror:true,loop:true,keys:[[0,0],[.6,.6]]},
  {file:SH('Boost1.FBX'),mirror:true,blend:.15,keys:[[.45,0],[1.1,.6]]},
  {file:SH('Block.fbx'),mirror:true,blend:.18,loop:true,keys:[[1.15,0],[4.8,2.4]]},
 ]},
 // Deadeye: a slick reload checking the pistol, the arm thrown out into the one-hand aim held on the target, Attack-R1's
 // shot pinned to the 2.12 s hit (its kick starting on it) and the recoil carried up into the barrel-up hold.
 {style:'weapon-pistol',grip:'gun',hit:2.12,facing:-.25,faceBy:'chest',seq:[
  {file:PI('Reload-R1'),keys:[[0,.3],[1.05,1.5]]},
  {file:A('Idle-Pistol-R'),blend:.3,loop:true,keys:[[.85,0],[2.1,.75]]},
  {file:PI('Attack-R1'),blend:.12,keys:[[1.9,.25],[2.12,.365],[2.3,.47],[2.6,.52],[4.8,.55]]},
 ]},
 // Longwatch: the low ready, a drop into a crouch (Crouch-Idle's legs) with Fire-Rifle1's shouldered aim over it, raised
 // level (the crouch's lean tips the captured aim down), the 1.8 s shot's recoil kick, and back up to the low ready.
 {style:'weapon-rifle',grip:'gun',support:[.025,-.03,.10,1],hit:1.8,facing:-.25,faceBy:'chest',kick:[[1.8,1.3]],aim:[[.9,0],[1.2,.33],[2.3,.33],[2.6,0]],seq:[
  {file:G('Idle'),loop:true,keys:[[0,0],[.8,.8]]},
  {file:G('Crouch-Idle'),blend:.35,loop:true,keys:[[.65,0],[2.6,1.4]]},
  {file:G('Fire-Rifle1'),part:'upper',blend:.3,until:2.35,keys:[[.8,0],[1.8,.02],[1.9,.08],[2.5,.12]]},
  {file:G('Idle'),blend:.5,loop:true,keys:[[2.55,0],[4.8,2.25]]},
 ]},
 // Belt Rattle: the low ready, Activate racking the gun, the hip-braced Fire-Rifle2 stance kicking through the ten rounds
 // of the 1.93-2.33 s burst (24 Hz), then the hip hold settling, barrel smoking, through the name card.
 {style:'weapon-machinegun',grip:'gun',support:[.025,-.03,.10,1],hit:2.12,facing:-.25,faceBy:'chest',kick:Array.from({length:10},(_,i)=>[+(1.93+i/24).toFixed(4),.45]),seq:[
  {file:G('Idle'),loop:true,keys:[[0,0],[.6,.6]]},
  {file:G('Activate'),blend:.15,keys:[[.5,0],[1.35,.85]]},
  {file:G('Fire-Rifle2'),blend:.3,keys:[[1.3,0],[1.93,.02],[2.4,.1],[2.9,.3],[4.8,.55]]},
 ]},
];
if(process.env.PACK_INTROS_PLAN){const plan=JSON.parse(fs.readFileSync(process.env.PACK_INTROS_PLAN,'utf8'));for(const e of plan){const i=ENTRANCES.findIndex(x=>x.style===e.style);if(i>=0)ENTRANCES[i]=e;else ENTRANCES.push(e);}}
const FPS=60,SOURCE_FPS=120,DURATION=4.8,FRAMES=Math.round(DURATION*FPS)+1;

// The pack mixes .fbx and .FBX: names resolve case-insensitively.
const listing=new Map(),resolve=f=>{const d=path.join(dir,path.dirname(f));if(!listing.has(d))listing.set(d,fs.readdirSync(d));
 const hit=listing.get(d).find(n=>n.toLowerCase()===path.basename(f).toLowerCase());if(!hit)throw new Error('missing pack file '+f);return path.join(d,hit);};
const cache=new Map(),load=f=>{if(!cache.has(f))cache.set(f,loadPackFbx(resolve(f)));return cache.get(f);};
const REF='1Hand-Sword/RPG-Character@Sword-Attack-R1.FBX',cals=new Map();
const calFor=(mirror,grip)=>{const k=mirror+grip;if(!cals.has(k))cals.set(k,calibrate(packRestPose(resolve(REF)),{mirror,grip}));return cals.get(k);};
const ix=n=>CHANNELS.indexOf(n);
const YAW=0,BX=1,BY=2,BZ=3,ROT=ix('pelvis.x'),ROT_END=ix('rankle.z'),LKNEE=ix('lknee'),RKNEE=ix('rknee'),GRIP=ix('grip.x'),BLADE=ix('blade.x'),EDGE=ix('edge.x'),RPOLE=ix('rpole.x'),
 LHAND=ix('lhand.x'),LFWD=ix('lfwd.x'),LUP=ix('lup.x'),LPOLE=ix('lpole.x'),TWO=ix('twohand');

// The torso and arms (an `upper` segment's channels): spine, chest, neck, head, clavicles and every arm channel.
const UPPER=[...['spine','chest','neck','head','lclav','rclav'].flatMap(n=>[0,1,2].map(k=>ix(n+'.x')+k)),...Array.from({length:CHANNELS.length-GRIP},(_,k)=>GRIP+k)];
// ---- sources: each file retargeted once per option set at 120 Hz ----
const rowsCache=new Map();
function sourceRows(e,s){
 const mirror=!!s.mirror,grip=s.grip||e.grip,twoHand=s.oneHand?false:!!e.twoHand,loop=!!s.loop,key=[s.file,mirror,grip,twoHand,loop,!!e.leftBlade].join('|');
 if(!rowsCache.has(key)){const {obj,clip}=load(s.file);
  // A gun (grip 'gun') is calibrated as a blade, then its frame turned onto the gun's: the barrel leaves the fist along the
  // metacarpals (the blade frame's edge), the top of the gun along the grip toward the index knuckle (the blade axis).
  const rows=retarget(obj,clip,calFor(mirror,grip==='gun'?'blade':grip),SOURCE_FPS,{mirror,oneHand:!twoHand,bothHands:twoHand,leftBlade:!!e.leftBlade,soften:true,poleTurn:10,splitTwist:true,loop});
  if(grip==='gun')for(const r of rows){const b=v().set(r[BLADE],r[BLADE+1],r[BLADE+2]),ed=v().set(r[EDGE],r[EDGE+1],r[EDGE+2]),x=v().crossVectors(b,ed);
   r[BLADE]=ed.x;r[BLADE+1]=ed.y;r[BLADE+2]=ed.z;r[EDGE]=x.x;r[EDGE+1]=x.y;r[EDGE+2]=x.z;}
  for(const r of rows)for(const v of r)if(!Number.isFinite(v))throw new Error(`${e.style}: non-finite channel in ${s.file}`);
  rowsCache.set(key,{rows,duration:(rows.length-1)/SOURCE_FPS});}
 return rowsCache.get(key);
}
/** Monotone cubic through the timing keys (CapturedClip.Retime's tangents): the source time at entrance time `t`. */
function retime(keys,t){
 const n=keys.length-1;if(t<=keys[0][0])return keys[0][1];if(t>=keys[n][0])return keys[n][1];
 let i=0;while(keys[i+1][0]<t)i++;const at=k=>keys[k][0],m=k=>keys[k][1];
 const slope=k=>at(k+1)!==at(k)?(m(k+1)-m(k))/(at(k+1)-at(k)):0,tan=k=>{if(k===0)return slope(0);if(k===n)return slope(n-1);const a=slope(k-1),b=slope(k);return a*b<=0?0:2*a*b/(a+b);};
 const h=at(i+1)-at(i),u=(t-at(i))/h,u2=u*u,u3=u2*u;
 return (2*u3-3*u2+1)*m(i)+(u3-2*u2+u)*h*tan(i)+(-2*u3+3*u2)*m(i+1)+(u3-u2)*h*tan(i+1);
}
// ---- row maths ----
const q=()=>new T.Quaternion(),v=()=>new T.Vector3();
const quatOf=(r,i)=>{const x=v().set(r[i],r[i+1],r[i+2]),a=x.length();return a<1e-9?q():q().setFromAxisAngle(x.divideScalar(a),a);};
const rotvec=(qq,out,i)=>{if(qq.w<0)qq.set(-qq.x,-qq.y,-qq.z,-qq.w);const w=Math.min(1,qq.w),s=Math.sqrt(Math.max(0,1-w*w)),a=2*Math.acos(w),k=s<1e-8?2:a/s;out[i]=qq.x*k;out[i+1]=qq.y*k;out[i+2]=qq.z*k;};
const unit=(r,i)=>v().set(r[i],r[i+1],r[i+2]).normalize();
// A frame from a direction and a roll vector (blade/edge, lfwd/lup), slerped as a rotation so both stay unit and orthogonal.
const frameOf=(r,a,b)=>{const z=unit(r,a),y=v().set(r[b],r[b+1],r[b+2]);y.addScaledVector(z,-y.dot(z)).normalize();const x=v().crossVectors(y,z);return q().setFromRotationMatrix(new T.Matrix4().makeBasis(x,y,z));};
const putFrame=(qq,out,a,b)=>{const z=v().set(0,0,1).applyQuaternion(qq),y=v().set(0,1,0).applyQuaternion(qq);out[a]=z.x;out[a+1]=z.y;out[a+2]=z.z;out[b]=y.x;out[b+1]=y.y;out[b+2]=y.z;};
function lerpRows(a,b,k){
 const o=new Float64Array(a.length);for(let i=0;i<a.length;i++)o[i]=a[i]+(b[i]-a[i])*k;
 for(let i=ROT;i<ROT_END;i+=3)rotvec(quatOf(a,i).slerp(quatOf(b,i),k),o,i);
 putFrame(frameOf(a,BLADE,EDGE).slerp(frameOf(b,BLADE,EDGE),k),o,BLADE,EDGE);
 // The blade frame stores edge (its +y here) as the roll: edge is x of the rig's basis, but any orthonormal pair round-trips.
 putFrame(frameOf(a,LFWD,LUP).slerp(frameOf(b,LFWD,LUP),k),o,LFWD,LUP);
 for(const p of [RPOLE,LPOLE]){const n=Math.hypot(o[p],o[p+1],o[p+2])||1;o[p]/=n;o[p+1]/=n;o[p+2]/=n;}
 return o;
}
/** Segment `s` at source time `x` (linear between the 120 Hz rows; the source wraps when `loop`, carrying its root on). */
function sampleSource(src,s,x){
 const {rows,duration}=src;let lap=0;
 if(s.loop&&duration>0){lap=Math.floor(x/duration);x-=lap*duration;}else x=Math.max(0,Math.min(duration,x));
 const f=x*SOURCE_FPS,i=Math.min(rows.length-2,Math.floor(f)),r=lerpRows(rows[i],rows[i+1],f-i);
 if(lap){const last=rows[rows.length-1],first=rows[0];r[YAW]+=lap*(last[YAW]-first[YAW]);r[BX]+=lap*(last[BX]-first[BX]);r[BZ]+=lap*(last[BZ]-first[BZ]);}
 return r;
}
/** Turns a row's root (heading, hips x/z) by `yaw` about +y and moves it by (dx, dz). The joints and arms are body-relative. */
function place(r,yaw,dx,dz){const c=Math.cos(yaw),s=Math.sin(yaw),x=r[BX],z=r[BZ];r[YAW]+=yaw;r[BX]=x*c+z*s+dx;r[BZ]=-x*s+z*c+dz;return r;}

// ---- the voxel rig's legs (Core/CapturedRig: body, pelvis -.24, hips (+-.13, -.055), knee and ankle .25 apart) ----
const LEG={pelvis:-.24,hipX:.13,hipY:-.055,thigh:.25,shin:.25};
/** Per foot (left, right) for row `r` (world, root at the given height): the ankle, the foot's heading and the hip: [x, y, z, yaw, hx, hy, hz]. */
function ankles(r,rootY){
 const rootQ=q().setFromAxisAngle(v().set(0,1,0),r[YAW]),body=v().set(r[BX],rootY,r[BZ]).add(v().set(0,r[BY],0).applyQuaternion(rootQ));
 const pelvisQ=rootQ.clone().multiply(quatOf(r,ix('pelvis.x'))),pelvis=body.clone().add(v().set(0,LEG.pelvis,0).applyQuaternion(rootQ)),out=[];
 for(const left of [true,false]){
  const hipQ=pelvisQ.clone().multiply(quatOf(r,ix((left?'lhip':'rhip')+'.x'))),hip=pelvis.clone().add(v().set(left?LEG.hipX:-LEG.hipX,LEG.hipY,0).applyQuaternion(pelvisQ));
  const kneeQ=hipQ.clone().multiply(q().setFromAxisAngle(v().set(1,0,0),Math.max(0,r[left?LKNEE:RKNEE]))),knee=hip.clone().add(v().set(0,-LEG.thigh,0).applyQuaternion(hipQ));
  const ankle=knee.clone().add(v().set(0,-LEG.shin,0).applyQuaternion(kneeQ)),footQ=kneeQ.clone().multiply(quatOf(r,ix((left?'lankle':'rankle')+'.x'))),f=v().set(0,0,1).applyQuaternion(footQ);
  out.push(ankle.x,ankle.y,ankle.z,Math.atan2(f.x,f.z),hip.x,hip.y,hip.z);
 }
 return out;
}

/** The chest's heading (rad about +y) in row `r`. */
function chestYaw(r){const c=q().setFromAxisAngle(v().set(0,1,0),r[YAW]).multiply(quatOf(r,ix('pelvis.x'))).multiply(quatOf(r,ix('spine.x'))).multiply(quatOf(r,ix('chest.x')));const f=v().set(0,0,1).applyQuaternion(c);return Math.atan2(f.x,f.z);}
/** The entrance's track: rows at 60 Hz over the 4.8 s phase, its plants and lift. */
function build(e){
 const segs=e.seq.map(s=>({s,src:sourceRows(e,s),start:s.keys[0][0],end:s.keys[s.keys.length-1][0],blend:s.blend||0,yaw:0,dx:0,dz:0}));
 // (A segment past its last key holds its last source frame, so a blend may run on past it.)
 for(let k=1;k<segs.length;k++)if(segs[k].start<segs[k-1].start)throw new Error(`${e.style}: segment ${k} starts before the one before`);
 if(segs[0].start>0||segs[segs.length-1].end<DURATION)throw new Error(`${e.style}: the segments must cover 0-${DURATION} s`);
 const raw=(g,t)=>sampleSource(g.src,g.s,retime(g.s.keys,t));
 const composite=(t,upto=segs.length)=>{
  let r=place(raw(segs[0],t),segs[0].yaw+(segs[0].s.turn||0),segs[0].dx,segs[0].dz);
  for(let k=1;k<upto;k++){const g=segs[k];if(t<g.start)break;let w=g.blend>0?T.MathUtils.smootherstep(t,g.start,g.start+g.blend):1;
   // An `upper` segment layers only the torso and arms over the sequence (its legs and root stay the sequence's) and,
   // with `until` (an entrance s), hands them back over its blend from there.
   if(g.s.until!==undefined)w*=1-(g.blend>0?T.MathUtils.smootherstep(t,g.s.until,g.s.until+g.blend):t>=g.s.until?1:0);
   if(g.s.part==='upper'){const mine=raw(g,t),mix=Float64Array.from(r);for(const i of UPPER)mix[i]=mine[i];r=lerpRows(r,mix,w);}
   else r=lerpRows(r,place(raw(g,t),g.yaw,g.dx,g.dz),w);}
  return r;};
 // Each segment's root continues the sequence so far at its blend start (heading kept absolute with `keepYaw`).
 for(let k=1;k<segs.length;k++){const g=segs[k],t=g.start,prev=composite(t,k),mine=raw(g,t);if(g.s.part==='upper')continue;
  g.yaw=(g.s.keepYaw?0:prev[YAW]-mine[YAW])+(g.s.turn||0);const p=place(Float64Array.from(mine),g.yaw,0,0);g.dx=prev[BX]-p[BX];g.dz=prev[BZ]-p[BZ];}
 let rows=[];for(let f=0;f<FRAMES;f++)rows.push(composite(f/FPS));
 // `air`: [entrance s, metres] keys of a leap's height added to the body (the pack's jumps leave the flight to physics).
 if(e.air)rows.forEach((r,f)=>{r[BY]+=retime(e.air,f/FPS);});
 // `kick`: [entrance s, size] shots whose recoil rides back through the gun (the pack's rifle fire barely moves it): the
 // muzzle flips up about the axis across the barrel and the grip rides back and up along it, as a damped follow-through
 // (NativeIntroMotion.Follow: peak at ~.07 s, a dip past the aim at ~.27 s, settled by ~.6 s), the chest rocking with it.
 if(e.kick)rows.forEach((r,f)=>{let k=0;for(const [at,size] of e.kick){const tau=f/FPS-at;if(tau>0)k+=size*1.8*(1-Math.exp(-60*tau))*Math.exp(-7*tau)*Math.sin(17*tau);}if(!k)return;
  const b=v().set(r[BLADE],r[BLADE+1],r[BLADE+2]).normalize(),axis=v().crossVectors(b,v().set(0,1,0));if(axis.lengthSq()<1e-6)return;axis.normalize();const q1=q().setFromAxisAngle(axis,.16*k);
  for(const c of [BLADE,EDGE]){const d=v().set(r[c],r[c+1],r[c+2]).applyQuaternion(q1);r[c]=d.x;r[c+1]=d.y;r[c+2]=d.z;}
  r[GRIP]-=b.x*.035*k;r[GRIP+1]+=.015*k-b.y*.035*k;r[GRIP+2]-=b.z*.035*k;
  rotvec(quatOf(r,ix('chest.x')).multiply(q().setFromAxisAngle(v().set(1,0,0),-.04*k)),r,ix('chest.x'));});
 // `aim`: [entrance s, rad] keys turning the weapon arm up about the axis across the barrel through the weapon shoulder
 // (a crouch's lean tips a captured aim into the floor).
 if(e.aim)rows.forEach((r,f)=>{const a=retime(e.aim,f/FPS);if(!a)return;const b=v().set(r[BLADE],r[BLADE+1],r[BLADE+2]).normalize(),axis=v().crossVectors(b,v().set(0,1,0));if(axis.lengthSq()<1e-6)return;
  const q1=q().setFromAxisAngle(axis.normalize(),a),sh=v().set(-.345,.18,0);for(const c of [BLADE,EDGE,RPOLE]){const d=v().set(r[c],r[c+1],r[c+2]).applyQuaternion(q1);r[c]=d.x;r[c+1]=d.y;r[c+2]=d.z;}
  const g=v().set(r[GRIP],r[GRIP+1],r[GRIP+2]).sub(sh).applyQuaternion(q1).add(sh);r[GRIP]=g.x;r[GRIP+1]=g.y;r[GRIP+2]=g.z;});
 // The hero facing over the stage origin at the end: the pelvis's heading, or with `faceBy: 'chest'` the chest's (a gun
 // stance turns the hips well away from the chest and the barrel).
 const last=rows[rows.length-1],turn=e.facing-(e.faceBy==='chest'?chestYaw(last):last[YAW]);let c=Math.cos(turn),s=Math.sin(turn);const ex=last[BX]*c+last[BZ]*s,ez=-last[BX]*s+last[BZ]*c;
 rows=rows.map(r=>place(r,turn,-ex,-ez));
 for(const r of rows)if(r.some(x=>!Number.isFinite(x)))throw new Error(`${e.style}: non-finite composite`);
 // Foot contacts on the rig: low and slow (or very low), held at least .1 s; the body lifted so they stand on the floor.
 const A0=rows.map(r=>ankles(r,0)),contact=[[],[]];
 for(const side of [0,1]){
  const h=A0.map(a=>a[side*7+1]),floor=[...h].sort((x,y)=>x-y)[Math.floor(h.length*.08)],speed=A0.map((a,i)=>{const b=A0[Math.min(i+1,A0.length-1)],p=A0[Math.max(i-1,0)];return Math.hypot(b[side*7]-p[side*7],b[side*7+2]-p[side*7+2],b[side*7+1]-p[side*7+1])*FPS/2;});
  // A foot near the floor and not swinging is on it: a captured foot gliding over the floor (a cross-fade between two
  // stances, a lunge's dragged foot) is planted too, and steps to a new spot once it has drifted 10 cm from the old one.
  let on=h.map((y,i)=>y<floor+.035&&speed[i]<1.4||y<floor+.012);
  // Close short gaps and drop short touches.
  const runs=on=>{const out=[];let i=0;while(i<on.length){if(!on[i]){i++;continue;}let j=i;while(j<on.length&&on[j])j++;out.push([i,j-1]);i=j;}return out;};
  let rs=runs(on);for(let k=1;k<rs.length;k++)if(rs[k][0]-rs[k-1][1]<=4)for(let i=rs[k-1][1];i<=rs[k][0];i++)on[i]=true;
  const steps=[];
  for(let [a,b] of runs(on)){
   // A planted foot also lets go before the hip moves out of the voxel leg's reach (.497 m, a little more with the body's
   // sink: Core/NativeEntrancePack.Drop).
   while(a<=b){const o=Math.min(b,a+1),sx=A0[o][side*7],sy=A0[o][side*7+1],sz=A0[o][side*7+2],reach=i=>Math.hypot(A0[i][side*7+4]-sx,A0[i][side*7+5]-sy,A0[i][side*7+6]-sz);let e=a;
    while(e+1<=b&&Math.hypot(A0[e+1][side*7]-sx,A0[e+1][side*7+2]-sz)<.1&&reach(e+1)<.52)e++;
    steps.push([a,e]);a=e+8;}// the step between two spots takes .13 s off the floor
  }
  rs=steps.filter(([a,b])=>b-a>=6);contact[side]=rs;
 }
 const floorY=[];for(const side of [0,1])for(const [a,b] of contact[side])for(let i=a;i<=b;i++)floorY.push(A0[i][side*7+1]);
 floorY.sort((x,y)=>x-y);const median=floorY.length?floorY[Math.floor(floorY.length/2)]:Math.min(...A0.map(a=>Math.min(a[1],a[5])));
 // The rig's root stands .865 above the floor top with straight legs and the body at 0 (CapturedRig: the ankle .795 under
 // the root, its sole .07 under the ankle); the median planted ankle meets the floor at the sole's depth.
 const lift=-.795-median;for(const r of rows)r[BY]+=lift;
 // `rise`: [entrance s, metres] keys lifting the body over its planted feet (the runtime's leg IK keeps them on their spots).
 if(e.rise)rows.forEach((r,f)=>{r[BY]+=retime(e.rise,f/FPS);});
 const plants=contact.map(rs=>rs.flatMap(([a,b])=>[+(a/FPS).toFixed(4),+(b/FPS).toFixed(4)]));
 return {rows,plants,lift};
}

const f32=x=>{const s=String(x);return (s.includes('.')||s.includes('e')?s:s+'.0')+'f';};
const lines=['// GENERATED by tools/export_pack_intros.mjs from the owner\'s Explosive RPG Character pack: retargeted, sequenced pose',
 '// tracks only (no FBX, mesh or .meta data). Private game assets; see ANIMATIONS.md. Do not edit.','namespace ChipArena.UnityConversion {',
 'public static class NativePackIntros {','public static readonly NativePackIntro[] All = {'];
const provenance=[];
for(const e of ENTRANCES){
 const {rows,plants,lift}=build(e);
 // Quantize exactly as the encoder does, so the track decodes to the composite's own 1/256 values.
 const data=encode(rows.map(r=>Float32Array.from(r)));
 const clip=`new CapturedClip(${JSON.stringify('intro/'+e.style)},${JSON.stringify(e.seq.map(s=>path.basename(s.file).replace(/^RPG-Character@|\.fbx$/gi,'')).join(' + '))},${FPS},${rows.length},false,new float[] {${f32(e.hit)}},new float[] {},${JSON.stringify(data)})`;
 // The support hand's grip on the weapon (the weapon hand's frame; the gameplay sockets): the greatsword's .11 down the grip.
 const support=e.support||(e.twoHand?[0,0,-.11,1]:null);
 lines.push(`new NativePackIntro(${JSON.stringify(e.style)},${clip},${support?'new float[] {'+support.map(f32).join(',')+'}':'null'},${f32(e.hit)},new float[] {${plants[0].map(f32).join(',')}},new float[] {${plants[1].map(f32).join(',')}},${!!e.breathe}),`);
 provenance.push({style:e.style,fps:FPS,frames:rows.length,hit:e.hit,facing:e.facing,twoHand:!!e.twoHand,support:e.support||(e.twoHand?[0,0,-.11,1]:null),breathe:!!e.breathe,grip:e.grip,lift:+lift.toFixed(4),plants:{left:plants[0],right:plants[1]},
  segments:e.seq.map(s=>({source:path.basename(s.file).replace(/^RPG-Character@|\.fbx$/gi,''),keys:s.keys,blend:s.blend||0,loop:!!s.loop,input:createHash('sha256').update(fs.readFileSync(resolve(s.file))).digest('hex')})),
  sha256:createHash('sha256').update(data).digest('hex')});
 console.log(e.style,rows.length,'frames, plants L',plants[0].length/2,'R',plants[1].length/2,'lift',lift.toFixed(3),data.length,'chars');
}
lines.push('};','}','}');
const out=process.env.PACK_INTROS_OUT||path.join(root,'Assets/ChipArena/Core/NativePackIntros.Generated.cs');
fs.writeFileSync(out,lines.join('\n')+'\n');
if(!process.env.PACK_INTROS_OUT)fs.writeFileSync(path.join(root,'tools/pack-intros-provenance.json'),JSON.stringify({pack:'Explosive RPG Character Mecanim Animation Pack (owner licence; FBX never committed)',channels:CHANNELS.length,quantum:1/QUANT,fps:FPS,duration:DURATION,entrances:provenance},null,1)+'\n');
console.warn=quiet;console.log('wrote',provenance.length,'entrance tracks to',out);
