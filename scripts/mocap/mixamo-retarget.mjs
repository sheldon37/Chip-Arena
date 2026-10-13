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
 * and pad it so the loader stops exactly there. The walk reads FBX 7.5's 64-bit record headers: older files (the
 * Explosive pack's 7.3, 32-bit headers) load as they are. */
function trimFbx(d){if(d.readUInt32LE(23)<7500)return d;let o=27;for(;;){const end=Number(d.readBigUInt64LE(o));if(end===0||end>=d.length)break;o=end;}const b=Buffer.alloc(o+176);d.copy(b,0,0,o);return b;}
export function loadFbx(file){const buf=trimFbx(fs.readFileSync(file));return new FBXLoader().parse(buf.buffer.slice(buf.byteOffset,buf.byteOffset+buf.byteLength),'');}

/** World transforms of the Mixamo-named bones, optionally mirrored left↔right through the character's sagittal plane
 * (x → -x; the Left* bone reads the reflected Right* bone): a left-arm capture (the Explosive shield set) drives the rig's
 * right arm. Calibration and retarget read through the same view, so the rest pose cancels exactly in both modes. */
function bones(obj,mirror=false){
 const B=n=>obj.getObjectByName('mixamorig'+n),swap=n=>n.startsWith('Left')?'Right'+n.slice(4):n.startsWith('Right')?'Left'+n.slice(5):n;
 if(!mirror)return {wq:n=>B(n).getWorldQuaternion(q()),wp:n=>B(n).getWorldPosition(v())};
 return {wq:n=>{const r=B(swap(n)).getWorldQuaternion(q());return r.set(r.x,-r.y,-r.z,r.w);},wp:n=>{const p=B(swap(n)).getWorldPosition(v());p.x=-p.x;return p;}};
}

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
/** The voxel rig's head, shoulder caps and torso (character-rig.ts; the runtime blade clearance's obstacles), each on the
 * joint that carries it: [joint, centre in the joint's frame, half extents]. Joint origins in chest space: head (0, .42, 0)
 * (.10 up the neck at .32), clavicles (±.275, .18, 0). The head block is the blank head with its jaw and crown; the torso
 * block the ribcage and upper chest with the sternum's plate (the 11 cm under the jaw stay free for the fists). */
const BLOCKS=[['head',[0,.1625,.005],[.17,.1975,.165]],['lclav',[.065,-.01,0],[.095,.06,.105]],['rclav',[-.065,-.01,0],[.095,.06,.105]],['chest',[0,.111,.005],[.265,.161,.165]]];
/** Points of the held hilt that must stay out of them: [along the blade from the main fist, along the edge, margin, body
 * only]: both fists (the support fist .11 down the grip; ~.05 of voxel fist round the wrist), the greatsword's pommel (.27
 * behind the main fist; the runtime's cut clearance only turns the blade, which cannot lift a pommel off the chest) and,
 * clear of the pauldrons and torso, the blade's slab where it leaves the guard (centre and edges of its .09 half-width;
 * the runtime's .02 clearance): there a turn about the grip has no lever. A blade held up across the face is left to the
 * runtime clearance, which turns it clear of the head. */
const HILT=[[-.27,0,.03],[-.11,0,.05],[0,0,.05],...[.22,.3].flatMap(s=>[[s,0,.02,1],[s,.09,.02,1],[s,-.09,.02,1]])];
/** Signed distance (negative inside) and continuous outward normal of a box, as blade-clearance.ts sdf: the nearest face's
 * normal blended with any face within 2 cm of as near, inside and (over the exact gradient) outside. */
const blend=(x,m)=>T.MathUtils.smoothstep(x-m,-.02,0);
function boxDistance(b,p,n){
 const l=p.clone().sub(b.c).applyQuaternion(b.qi),qx=Math.abs(l.x)-b.h.x,qy=Math.abs(l.y)-b.h.y,qz=Math.abs(l.z)-b.h.z,m=Math.max(qx,qy,qz),sx=Math.sign(l.x)||1,sy=Math.sign(l.y)||1,sz=Math.sign(l.z)||1;
 if(m>0){const ox=Math.max(qx,0),oy=Math.max(qy,0),oz=Math.max(qz,0);n.set((ox+.02*blend(qx,m))*sx,(oy+.02*blend(qy,m))*sy,(oz+.02*blend(qz,m))*sz).normalize().applyQuaternion(b.q);return Math.hypot(ox,oy,oz);}
 n.set(blend(qx,m)*sx,blend(qy,m)*sy,blend(qz,m)*sz).normalize().applyQuaternion(b.q);return m;
}
/** Moves a two-handed grip `g` (chest space) until the hilt points clear the rig's blocks as this row poses them (its neck,
 * head and clavicle channels): the capture's hands pass close in front of a human face and shoulder, which the voxel head
 * and pauldrons are far bigger than. Every penetrating point asks to move out along its normal; the requests are averaged
 * with the deepest weighing most, a few times over, so the grip's path stays continuous. */
function clearHilt(row,g,bl,ed){
 const R=k=>{const i=CHANNELS.indexOf(k+'.x'),r=v().set(row[i],row[i+1],row[i+2]),a=r.length();return a<1e-9?q():q().setFromAxisAngle(r.divideScalar(a),a);};
 const Rn=R('neck'),frames={chest:[v(),q()],head:[v().set(0,.1,0).applyQuaternion(Rn).add(v().set(0,.32,0)),Rn.clone().multiply(R('head'))],lclav:[v().set(.275,.18,0),R('lclav')],rclav:[v().set(-.275,.18,0),R('rclav')]};
 const boxes=BLOCKS.map(([j,c,h])=>{const [o,r]=frames[j];return {c:v().fromArray(c).applyQuaternion(r).add(o),q:r,qi:r.clone().invert(),h:v().fromArray(h)};});
 const p=v(),n=v(),acc=v();
 for(let it=0;it<8;it++){acc.set(0,0,0);let w=0;
  for(const [s,e,M,body] of HILT){p.copy(g).addScaledVector(bl,s).addScaledVector(ed,e);for(const b of body?boxes.slice(1):boxes){const pen=M-boxDistance(b,p,n);if(pen>0){const k=pen**3;acc.addScaledVector(n,k*pen);w+=k;}}}
  if(w<=0)break;g.addScaledVector(acc,1/w);}
}
const rotvec=(qq,out)=>{const w=T.MathUtils.clamp(qq.w,-1,1),s=Math.sqrt(Math.max(0,1-w*w)),a=2*Math.acos(Math.abs(w)),k=s<1e-8?2:(w<0?-a:a)/s;return out.set(qq.x*k,qq.y*k,qq.z*k);};

/** Skeleton calibration from the bind pose (Mixamo T-pose): each mapped bone's world rotation in the rig's rest pose
 * (arms hanging), whose inverse turns an animated Mixamo world rotation into the voxel joint's world rotation. */
export function calibrate(obj,{mirror=false,grip='blade'}={}){
 obj.updateMatrixWorld(true);const {wq,wp}=bones(obj,mirror);
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
 // Per hand, so a weapon in each fist (`leftBlade`: the daggers) gets the same convention from its own fingers.
 const axes=side=>{const inv=wq(side+'Hand').invert(),H=wp(side+'Hand');
  const blade=wp(side+'HandIndex1').sub(wp(side+'HandPinky1')).applyQuaternion(inv),edge=wp(side+'HandMiddle1').sub(H).applyQuaternion(inv);
  blade.normalize();edge.addScaledVector(blade,-edge.dot(blade)).normalize();
  // A reverse (ice-pick) grip: the blade leaves the fist on the little-finger side.
  if(grip==='reverse'){blade.negate();edge.addScaledVector(blade,-edge.dot(blade)).normalize();}
  // A strapped shield (grip 'shield'; the weapon model's +z is its face, +y its top): the face looks out of the back of
  // the hand (the palm's opposite: fingers × knuckle line), the top runs along the fist's knuckle line, so a fist held
  // knuckles-up in front of the chest presents the face forward, top up. "blade" is then the face, "edge" top × face.
  if(grip==='shield'){const face=edge.clone().cross(blade).normalize(),top=blade.clone().addScaledVector(face,-blade.dot(face)).normalize();blade.copy(face);edge.copy(top).cross(face).normalize();}
  return {blade,edge};};
 const {blade,edge}=axes('Right'),left=axes('Left');
 return {C,legScale:RIG.leg/legM,armScale:RIG.arm/armM,hipsY,blade,edge,bladeL:left.blade,edgeL:left.edge};
}

/** Sample `clip` on `obj` at `fps`; returns one Float32Array row per frame in CHANNELS order. `mirror` reads the capture
 * left↔right (calibrate with the same flag); `oneHand` keeps the support hand off the weapon (one-handed weapons). */
export function retarget(obj,clip,cal,fps,{mirror=false,oneHand=false,leftBlade=false,soften=false,poleTurn=0,splitTwist=false,canonTwist=false,canonicalTwist=false,keepFold=false,bothHands=false,hilt=bothHands,heading=0,loop=false}={}){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const {wq,wp}=bones(obj,mirror);
 const n=Math.max(2,Math.round(clip.duration*fps)+1),rows=[];let yawPrev=0,hips0=null;
 const W={},Wp={};const names=['Hips','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot','LeftHand','RightHand'];
 const pos=['Hips','LeftArm','RightArm','LeftForeArm','RightForeArm','LeftHand','RightHand'];
 const parent={Spine1:'Hips',Spine2:'Spine1',Neck:'Spine2',Head:'Neck',LeftShoulder:'Spine2',RightShoulder:'Spine2',LeftUpLeg:'Hips',RightUpLeg:'Hips',LeftLeg:'LeftUpLeg',RightLeg:'RightUpLeg',LeftFoot:'LeftLeg',RightFoot:'RightLeg'};
 const rv=v(),tmp=q(),chestInv=q(),yawQ=q(),yawInv=q(),poleR=new T.Vector3(-1,-.7,-.2).normalize(),poleL=new T.Vector3(1,-.8,-.25).normalize();// rig-ik's default poles
 // With `poleTurn` the poles start converged: frame 0 is solved repeatedly first (and a `loop` runs through once, carrying
 // its last frame's poles round into frame 0), so no clip opens, or wraps, with the poles still turning in from the defaults.
 const order=[];if(poleTurn){for(let k=0;k<40;k++)order.push(0);if(loop)for(let i=0;i<n;i++)order.push(i);}const skip=order.length;for(let i=0;i<n;i++)order.push(i);
 for(let j=0;j<order.length;j++){const i=order[j];
  mixer.setTime(Math.min(clip.duration-1e-4,i/fps));// setTime(duration) would wrap a looping action to 0obj.updateMatrixWorld(true);
  for(const k of names)W[k]=wq(k).multiply(cal.C[k]);
  for(const k of pos)Wp[k]=wp(k);
  // Heading from the pelvis forward, unwrapped so full turns stay continuous; every joint is expressed under it.
  const f=v().set(0,0,1).applyQuaternion(W.Hips);let yaw=Math.atan2(f.x,f.z);while(yaw-yawPrev>Math.PI)yaw-=2*Math.PI;while(yaw-yawPrev<-Math.PI)yaw+=2*Math.PI;yawPrev=yaw;
  yawQ.setFromAxisAngle(UP,yaw);yawInv.copy(yawQ).invert();for(const k of names)W[k].premultiply(yawInv);
  if(!hips0)hips0=Wp.Hips.clone();
  const row=[yaw-heading,(Wp.Hips.x-hips0.x)*cal.legScale,(Wp.Hips.y-cal.hipsY)*cal.legScale,(Wp.Hips.z-hips0.z)*cal.legScale];
  const local=(k)=>{const p=parent[k];return p?tmp.copy(W[p]).invert().multiply(W[k]):tmp.copy(W[k]);};
  // Rig joints: pelvis←Hips, spine←Spine1 (Spine+Spine1), chest←Spine2, neck, head, clavicles, hips, ankles.
  // The voxel torso is long and blocky: a human's deep forward fold (low sweeps) reads as a collapse on it, so spine flexion
  // past .45 rad (and hip pitch past .12) is mostly taken out (the chest and arms keep their capture; only the lean is eased).
  // `keepFold` keeps them as captured: a body falling on its face (a death) pitches its pelvis a quarter turn, and easing it
  // would leave the legs hanging down under the lying trunk. Off by default, so the earlier sets' tracks stay byte-identical.
  // `splitTwist`: the torso's twist (about the spine's own axis) is shared evenly between the waist and chest joints, the
  // chest's capture orientation (and the chest-space arms) unchanged: the voxel torso is two rigid blocks, which a
  // capture's whole torso turn at the waist (the Explosive pack's wind-ups twist it past 90°) shears apart.
  let spineQ,chestQ;
  // `heading` (rad): a constant heading moved out of the pelvis into the spine and chest (.45 / .55, as the runtime ground
  // pass shares a hip turn), the chest's capture orientation unchanged: a guard turned sideways keeps the hips square over
  // the procedural feet the runtime gives it, the torso carrying the turn.
  if(heading){const P=local('Hips').clone(),S=local('Spine1').clone(),C=local('Spine2').clone(),PS=P.clone().multiply(S),y=a=>new T.Quaternion().setFromAxisAngle(UP,a);
   spineQ=P.clone().invert().multiply(y(.45*heading)).multiply(P).multiply(S);chestQ=PS.clone().invert().multiply(y(.55*heading)).multiply(PS).multiply(C);}
  // `canonTwist` reads each joint's twist from the quaternion with w >= 0 (q and -q are one rotation, but their twist
  // angles differ by 2 pi, which split in half turned both blocks half a turn: rigs whose local spine quaternions come out
  // negated, such as the zombie pack's MotusMan). Off by default, so the earlier sets' tracks stay byte-identical.
  // The Cyber Lord run export passes the same rule as `canonicalTwist` (the CLazy runs land on w < 0 for a few frames).
  if(splitTwist){const s=(spineQ??local('Spine1')).clone(),c=(chestQ??local('Spine2')).clone(),tw=x=>(canonTwist||canonicalTwist)&&x.w<0?2*Math.atan2(-x.y,-x.w):2*Math.atan2(x.y,x.w),h=new T.Quaternion().setFromAxisAngle(UP,(tw(s)-tw(c))/2);
   spineQ=s.multiply(h.clone().invert());chestQ=h.multiply(c);}
  for(const k of ['Hips','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','LeftFoot','RightFoot']){rotvec((k==='Spine1'&&spineQ?tmp.copy(spineQ):k==='Spine2'&&chestQ?tmp.copy(chestQ):local(k)).normalize(),rv);if(!keepFold&&k==='Spine1'&&rv.x>.45)rv.x=.45+(rv.x-.45)*.35;if(!keepFold&&k==='Hips'&&rv.x>.12)rv.x=.12+(rv.x-.12)*.5;
   // `soften`: the voxel head is a big block, so a capture's deep neck/head bends (the Explosive pack's hunched combat
   // look) are eased past .9 rad, and its block feet past 1.2 rad (a stagger's rolled, pointed foot).
   const soft=!soften?0:k==='Neck'||k==='Head'?.9:k==='LeftFoot'||k==='RightFoot'?1.2:0;
   if(soft){const a=rv.length();if(a>soft)rv.multiplyScalar((soft+(a-soft)*.35)/a);}row.push(rv.x,rv.y,rv.z);}
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
  // `poleTurn` (rad/s): the pole instead turns about the reach toward the capture's at most this fast, so a capture whose
  // elbow crosses to the other side of the reach line (through a straight arm) swings the bend plane round instead of
  // flipping it (a lerp toward the opposite side passes through zero and flips in a frame).
  const pole=(s,e,h,target,sx,prev)=>{const S=yawPos(s),E=yawPos(e),H=yawPos(h),ax=H.clone().sub(S).normalize(),p=E.sub(S);p.addScaledVector(ax,-p.dot(ax));if(p.lengthSq()>1e-9)p.normalize().applyQuaternion(chestInv);
   const d=new T.Vector3(target.x-sx,target.y-RIG.shoulderY,target.z).normalize();p.addScaledVector(d,-p.dot(d));
   const along=prev.dot(d);if(Math.abs(along)<.97)prev.addScaledVector(d,-along).normalize();
   const k=T.MathUtils.smoothstep(p.length(),.25,.6);
   // (A pole left along the reach, which the transport could not take perpendicular, eases back by the lerp.)
   if(k>0&&poleTurn&&Math.abs(prev.dot(d))<.97){p.normalize();const ang=Math.atan2(new T.Vector3().crossVectors(prev,p).dot(d),prev.dot(p)),max=poleTurn/fps;prev.applyAxisAngle(d,T.MathUtils.clamp(ang*.6*k,-max,max)).normalize();}
   else if(k>0)prev.lerp(p.normalize(),.6*k).normalize();return prev.clone();};
  // Hand targets stay inside the rig arm's reach with a soft knee (rig-ik clamps hard at .515: a kink in the blade's path).
  const reach=(p,sx)=>{const d=new T.Vector3(p.x-sx,p.y-RIG.shoulderY,p.z),l=d.length(),knee=.4,room=.1;if(l>knee)d.multiplyScalar((knee+room*Math.tanh((l-knee)/room))/l);return p.set(sx+d.x,RIG.shoulderY+d.y,d.z);};
  const RHq=wq('RightHand'),blade=cal.blade.clone().applyQuaternion(RHq),edge=cal.edge.clone().applyQuaternion(RHq);
  // `bothHands`: the support hand holds the grip throughout (a capture whose free hand only follows the hilt loosely).
  const gap=Wp.LeftHand.distanceTo(Wp.RightHand);let two=oneHand?0:bothHands?1:T.MathUtils.clamp((40-gap)/12,0,1);
  const g=reach(toChest(Wp.RightHand),-RIG.shoulderX),bl=dirChest(blade),ed=dirChest(edge);ed.addScaledVector(bl,-ed.dot(bl)).normalize();
  // Two-handed, the support hand takes the weapon's support grip (.11 down the grip from the main hand): the voxel
  // shoulders are far wider than a human's, so the grip shifts toward the support shoulder until that hand can reach it.
  // A grip held far over on the main-hand side (the sprint's shoulder carry) would have to cross the chest for the support
  // hand to reach it, dragging the blade through the torso: there the support hand lets go instead. With `bothHands` it
  // never lets go: the grip comes all the way within its reach, then out of the head and torso blocks (clearHilt).
  // (Alternated a few times: the way out of the head can carry the support grip back out of reach.)
  for(let k=0;k<(bothHands?4:1)&&two>0;k++){const L=new T.Vector3(RIG.shoulderX,RIG.shoulderY,0),s=g.clone().addScaledVector(bl,-.11),need=s.distanceTo(L)-.44;
   if(need>0){if(!bothHands)two*=1-T.MathUtils.smoothstep(need,.22,.32);g.add(L.sub(s).normalize().multiplyScalar((bothHands?need:Math.min(need,.25))*two));}
   if(hilt)clearHilt(row,g,bl,ed);}
  const rp=pole(Wp.RightArm,Wp.RightForeArm,Wp.RightHand,g,-RIG.shoulderX,poleR);row.push(g.x,g.y,g.z,bl.x,bl.y,bl.z,ed.x,ed.y,ed.z,rp.x,rp.y,rp.z);
  // The free hand's wrist frame (the rig's wrist +z, +y); with `leftBlade` its +z is the left weapon's blade, calibrated from
  // the left fist like the right one, and +y blade × edge (the left wrist's weapon socket holds it along +z).
  const lh=reach(toChest(Wp.LeftHand),RIG.shoulderX),LH=W.LeftHand.clone().premultiply(chestInv);let lf=new T.Vector3(0,0,1).applyQuaternion(LH),lu=new T.Vector3(0,1,0).applyQuaternion(LH);
  if(leftBlade){const LHq=wq('LeftHand');lf=dirChest(cal.bladeL.clone().applyQuaternion(LHq));lu=dirChest(cal.bladeL.clone().cross(cal.edgeL).applyQuaternion(LHq));lu.addScaledVector(lf,-lu.dot(lf)).normalize();}
  const lp=pole(Wp.LeftArm,Wp.LeftForeArm,Wp.LeftHand,lh,RIG.shoulderX,poleL);row.push(lh.x,lh.y,lh.z,lf.x,lf.y,lf.z,lu.x,lu.y,lu.z,lp.x,lp.y,lp.z,two);
  if(j>=skip)rows.push(Float32Array.from(row));
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
export function leftFootStrike(obj,clip,fps=60,{mirror=false}={}){
 const mixer=new T.AnimationMixer(obj);mixer.clipAction(clip).play();const {wp}=bones(obj,mirror);
 const n=Math.round(clip.duration*fps),sp=[];let prev=null;
 for(let i=0;i<=n;i++){mixer.setTime(Math.min(clip.duration-1e-4,i/fps));obj.updateMatrixWorld(true);const p=wp('LeftFoot');sp.push(prev?p.distanceTo(prev)*fps:0);prev=p;}
 const peak=sp.indexOf(Math.max(...sp));for(let k=1;k<=n;k++){const i=(peak+k)%(n+1);if(i>0&&sp[i]<sp[peak]*.25)return +(i/fps).toFixed(3);}
 return 0;
}
