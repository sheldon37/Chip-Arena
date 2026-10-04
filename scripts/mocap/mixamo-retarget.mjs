// Offline Mixamo → voxel-rig retargeting (development tool; never imported by the game or the build).
// Loads a Mixamo FBX with three's FBXLoader under minimal Node shims, samples the clip and converts it into the channel
// layout of src/client/mocap/mocap-clip.ts (MOCAP_CHANNELS): torso, clavicle and leg joint rotations as rotation vectors
// in the rig's joint frames, hinge knees, the heading (yaw) and hips offset, and IK targets for the arms (chest space:
// main grip, blade/edge directions, elbow poles, the free hand and a two-hand grip weight). The arms are left to the
// game's own IK (rig-ik.ts) because the voxel rig's shoulders are much wider than a human's: copying arm rotations would
// part the hands off the grip.
import fs from 'node:fs';
import * as T from 'three';

// FBXLoader only touches these DOM globals for textures; animation-only files need nothing more.
globalThis.self ??= globalThis;globalThis.window ??= globalThis;
globalThis.document ??= {createElementNS:()=>({style:{},addEventListener(){},removeEventListener(){}}),createElement:()=>({style:{}})};
const {FBXLoader}=await import('three/examples/jsm/loaders/FBXLoader.js');

/** Some Mixamo exports carry a footer FBXLoader's end-of-content test misreads; cut the buffer at the top-level null record
 * and pad it so the loader stops exactly there. */
function trimFbx(d){let o=27;for(;;){const end=Number(d.readBigUInt64LE(o));if(end===0||end>=d.length)break;o=end;}const b=Buffer.alloc(o+176);d.copy(b,0,0,o);return b;}
export function loadFbx(file){const buf=trimFbx(fs.readFileSync(file));return new FBXLoader().parse(buf.buffer.slice(buf.byteOffset,buf.byteOffset+buf.byteLength),'');}

// Voxel rig proportions (src/client/character-rig.ts): hip-to-ankle .50, shoulder-to-wrist .52, shoulders at chest (±.345, .18).
const RIG={leg:.5,arm:.52,shoulderY:.18,shoulderX:.345};
/** Channel layout (keep in sync with MOCAP_CHANNELS in src/client/mocap/mocap-clip.ts). */
export const CHANNELS=['yaw','bx','by','bz',
 ...['pelvis','spine','chest','neck','head','lclav','rclav','lhip','rhip','lankle','rankle'].flatMap(n=>[n+'.x',n+'.y',n+'.z']),
 'lknee','rknee',
 'grip.x','grip.y','grip.z','blade.x','blade.y','blade.z','edge.x','edge.y','edge.z','rpole.x','rpole.y','rpole.z',
 'lhand.x','lhand.y','lhand.z','lfwd.x','lfwd.y','lfwd.z','lup.x','lup.y','lup.z','lpole.x','lpole.y','lpole.z','twohand'];

const UP=new T.Vector3(0,1,0);
const q=()=>new T.Quaternion(),v=()=>new T.Vector3();
const rotvec=(qq,out)=>{const w=T.MathUtils.clamp(qq.w,-1,1),s=Math.sqrt(Math.max(0,1-w*w)),a=2*Math.acos(Math.abs(w)),k=s<1e-8?2:(w<0?-a:a)/s;return out.set(qq.x*k,qq.y*k,qq.z*k);};

/** Skeleton calibration from the bind pose (Mixamo T-pose): each mapped bone's world rotation in the rig's rest pose
 * (arms hanging), whose inverse turns an animated Mixamo world rotation into the voxel joint's world rotation. */
export function calibrate(obj){
 obj.updateMatrixWorld(true);const B=n=>obj.getObjectByName('mixamorig'+n),wq=n=>B(n).getWorldQuaternion(q()),wp=n=>B(n).getWorldPosition(v());
 const C={};for(const n of ['Hips','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot'])C[n]=wq(n).invert();
 // Hands: T-pose arms rotated down to hang at the sides (left about -Z, right about +Z).
 C.LeftHand=q().setFromAxisAngle(new T.Vector3(0,0,1),-Math.PI/2).multiply(wq('LeftHand')).invert();
 C.RightHand=q().setFromAxisAngle(new T.Vector3(0,0,1),Math.PI/2).multiply(wq('RightHand')).invert();
 const legM=wp('LeftUpLeg').distanceTo(wp('LeftLeg'))+wp('LeftLeg').distanceTo(wp('LeftFoot'));
 const armM=wp('RightArm').distanceTo(wp('RightForeArm'))+wp('RightForeArm').distanceTo(wp('RightHand'));
 const hipsY=wp('Hips').y;
 // Blade axis and edge in the right hand's frame (the clips carry no sword): a hammer grip runs across the palm along the
 // knuckle line, so the blade leaves the fist from the pinky toward the index knuckle; the true edge leads along the
 // metacarpals (wrist to middle knuckle), perpendicular to it.
 const inv=wq('RightHand').invert(),R=wp('RightHand');
 const blade=wp('RightHandIndex1').sub(wp('RightHandPinky1')).applyQuaternion(inv),edge=wp('RightHandMiddle1').sub(R).applyQuaternion(inv);
 blade.normalize();edge.addScaledVector(blade,-edge.dot(blade)).normalize();
 return {C,legScale:RIG.leg/legM,armScale:RIG.arm/armM,hipsY,blade,edge};
}

/** Sample `clip` on `obj` at `fps`; returns one Float32Array row per frame in CHANNELS order. */
export function retarget(obj,clip,cal,fps){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const B=n=>obj.getObjectByName('mixamorig'+n);
 const n=Math.max(2,Math.round(clip.duration*fps)+1),rows=[];let yawPrev=0,hips0=null;
 const W={},Wp={};const names=['Hips','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot','LeftHand','RightHand'];
 const pos=['Hips','LeftArm','RightArm','LeftForeArm','RightForeArm','LeftHand','RightHand'];
 const parent={Spine1:'Hips',Spine2:'Spine1',Neck:'Spine2',Head:'Neck',LeftShoulder:'Spine2',RightShoulder:'Spine2',LeftUpLeg:'Hips',RightUpLeg:'Hips',LeftLeg:'LeftUpLeg',RightLeg:'RightUpLeg',LeftFoot:'LeftLeg',RightFoot:'RightLeg'};
 const rv=v(),tmp=q(),chestInv=q(),yawQ=q(),yawInv=q(),poleR=new T.Vector3(-1,-.7,-.2).normalize(),poleL=new T.Vector3(1,-.8,-.25).normalize();// rig-ik's default poles
 for(let i=0;i<n;i++){
  mixer.setTime(Math.min(clip.duration-1e-4,i/fps));// setTime(duration) would wrap a looping action to 0obj.updateMatrixWorld(true);
  for(const k of names)W[k]=B(k).getWorldQuaternion(q()).multiply(cal.C[k]);
  for(const k of pos)Wp[k]=B(k).getWorldPosition(v());
  // Heading from the pelvis forward, unwrapped so full turns stay continuous; every joint is expressed under it.
  const f=v().set(0,0,1).applyQuaternion(W.Hips);let yaw=Math.atan2(f.x,f.z);while(yaw-yawPrev>Math.PI)yaw-=2*Math.PI;while(yaw-yawPrev<-Math.PI)yaw+=2*Math.PI;yawPrev=yaw;
  yawQ.setFromAxisAngle(UP,yaw);yawInv.copy(yawQ).invert();for(const k of names)W[k].premultiply(yawInv);
  if(!hips0)hips0=Wp.Hips.clone();
  const row=[yaw,(Wp.Hips.x-hips0.x)*cal.legScale,(Wp.Hips.y-cal.hipsY)*cal.legScale,(Wp.Hips.z-hips0.z)*cal.legScale];
  const local=(k)=>{const p=parent[k];return p?tmp.copy(W[p]).invert().multiply(W[k]):tmp.copy(W[k]);};
  // Rig joints: pelvis←Hips, spine←Spine1 (Spine+Spine1), chest←Spine2, neck, head, clavicles, hips, ankles.
  // The voxel torso is long and blocky: a human's deep forward fold (low sweeps) reads as a collapse on it, so spine flexion
  // past .45 rad (and hip pitch past .12) is mostly taken out (the chest and arms keep their capture; only the lean is eased).
  for(const k of ['Hips','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','LeftFoot','RightFoot']){rotvec(local(k).normalize(),rv);if(k==='Spine1'&&rv.x>.45)rv.x=.45+(rv.x-.45)*.35;if(k==='Hips'&&rv.x>.12)rv.x=.12+(rv.x-.12)*.5;row.push(rv.x,rv.y,rv.z);}
  // Knees are hinges about the rig's x axis (positive folds the shin back).
  for(const k of ['LeftLeg','RightLeg']){const l=local(k),sg=l.w<0?-1:1;row.push(2*Math.atan2(sg*l.x,sg*l.w));}
  // Arms in chest space (yaw-removed world → chest frame), hands relative to the shoulder midpoint scaled to the rig's arm.
  chestInv.copy(W.Spine2).invert();const yawPos=p=>p.clone().applyQuaternion(yawInv);
  const mid=yawPos(Wp.LeftArm).add(yawPos(Wp.RightArm)).multiplyScalar(.5);
  const toChest=p=>yawPos(p).sub(mid).applyQuaternion(chestInv).multiplyScalar(cal.armScale).add(v().set(0,RIG.shoulderY,0));
  const dirChest=d=>d.clone().applyQuaternion(yawInv).applyQuaternion(chestInv).normalize();
  // Elbow pole (chest space, unit), solved for the rig's own reach: the voxel arm reaches its target along another line
  // than the captured arm, so the captured elbow offset is taken perpendicular to the rig's shoulder-to-target line. Where
  // little of it is left (a straight captured arm, or its elbow along the rig's arm) the previous frame's pole carries on,
  // transported along with the reach so it can never end up along the arm, and the pole eases toward the capture (about
  // a frame of lag): rig-ik's bend plane then turns with the arm instead of flipping the upper arm's twist in a frame.
  const pole=(s,e,h,target,sx,prev)=>{const S=yawPos(s),E=yawPos(e),H=yawPos(h),ax=H.clone().sub(S).normalize(),p=E.sub(S);p.addScaledVector(ax,-p.dot(ax));if(p.lengthSq()>1e-9)p.normalize().applyQuaternion(chestInv);
   const d=new T.Vector3(target.x-sx,target.y-RIG.shoulderY,target.z).normalize();p.addScaledVector(d,-p.dot(d));
   const along=prev.dot(d);if(Math.abs(along)<.97)prev.addScaledVector(d,-along).normalize();
   const k=T.MathUtils.smoothstep(p.length(),.25,.6);if(k>0)prev.lerp(p.normalize(),.6*k).normalize();return prev.clone();};
  // Hand targets stay inside the rig arm's reach with a soft knee (rig-ik clamps hard at .515: a kink in the blade's path).
  const reach=(p,sx)=>{const d=new T.Vector3(p.x-sx,p.y-RIG.shoulderY,p.z),l=d.length(),knee=.4,room=.1;if(l>knee)d.multiplyScalar((knee+room*Math.tanh((l-knee)/room))/l);return p.set(sx+d.x,RIG.shoulderY+d.y,d.z);};
  const RHq=B('RightHand').getWorldQuaternion(q()),blade=cal.blade.clone().applyQuaternion(RHq),edge=cal.edge.clone().applyQuaternion(RHq);
  const gap=Wp.LeftHand.distanceTo(Wp.RightHand),two=T.MathUtils.clamp((40-gap)/12,0,1);
  const g=reach(toChest(Wp.RightHand),-RIG.shoulderX),bl=dirChest(blade),ed=dirChest(edge);ed.addScaledVector(bl,-ed.dot(bl)).normalize();
  // Two-handed, the support hand takes the weapon's support grip (.11 down the grip from the main hand): the voxel
  // shoulders are far wider than a human's, so the grip shifts toward the support shoulder until that hand can reach it.
  if(two>0){const L=new T.Vector3(RIG.shoulderX,RIG.shoulderY,0),s=g.clone().addScaledVector(bl,-.11),d=s.distanceTo(L);if(d>.44)g.add(L.sub(s).normalize().multiplyScalar((d-.44)*two));}
  const rp=pole(Wp.RightArm,Wp.RightForeArm,Wp.RightHand,g,-RIG.shoulderX,poleR);row.push(g.x,g.y,g.z,bl.x,bl.y,bl.z,ed.x,ed.y,ed.z,rp.x,rp.y,rp.z);
  const lh=reach(toChest(Wp.LeftHand),RIG.shoulderX),LH=W.LeftHand.clone().premultiply(chestInv),lf=new T.Vector3(0,0,1).applyQuaternion(LH),lu=new T.Vector3(0,1,0).applyQuaternion(LH);
  const lp=pole(Wp.LeftArm,Wp.LeftForeArm,Wp.LeftHand,lh,RIG.shoulderX,poleL);row.push(lh.x,lh.y,lh.z,lf.x,lf.y,lf.z,lu.x,lu.y,lu.z,lp.x,lp.y,lp.z,two);
  rows.push(Float32Array.from(row));
 }
 return rows;
}

// Compact encoding (decoded by src/client/mocap/mocap-clip.ts): every channel quantized to 1/256 (radians, rig metres or
// unit-vector components), delta coded along time, zigzag varint in 5-bit groups over a 64-letter alphabet (bit 5 marks
// a continuation), channel after channel.
export const ALPHABET='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
export const QUANT=256;
export function encode(rows){
 let s='';const ch=rows[0].length;
 for(let c=0;c<ch;c++){let prev=0;for(const r of rows){const x=Math.round(r[c]*QUANT),d=x-prev;prev=x;let z=d<0?-2*d-1:2*d;do{const g=z&31;z>>>=5;s+=ALPHABET[g|(z?32:0)];}while(z);}}
 return s;
}

/** Loop phase key: the clip time (s) at which the left foot strikes the ground (its world speed drops under a quarter of its
 * swing peak after the peak), the rig gait's stride phase 0 (motion-quality.ts: the left foot lands at frac(stride/2π)=0). */
export function leftFootStrike(obj,clip,fps=60){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const foot=obj.getObjectByName('mixamorigLeftFoot');
 const n=Math.round(clip.duration*fps),sp=[];let prev=null;
 for(let i=0;i<=n;i++){mixer.setTime(Math.min(clip.duration-1e-4,i/fps));obj.updateMatrixWorld(true);const p=foot.getWorldPosition(v());sp.push(prev?p.distanceTo(prev)*fps:0);prev=p;}
 const peak=sp.indexOf(Math.max(...sp));for(let k=1;k<=n;k++){const i=(peak+k)%(n+1);if(i>0&&sp[i]<sp[peak]*.25)return +(i/fps).toFixed(3);}
 return 0;
}
