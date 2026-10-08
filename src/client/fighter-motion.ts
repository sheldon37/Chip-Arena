import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {RIG_LENGTHS} from './character-rig.js';
import {smoothMotion as ease,gaitFoot,layerLeg} from './motion-quality.js';
import type {PlayerView} from '../shared/simulation.js';
import {GUARD_HOLD,GUARD_FADE} from './melee-guard.js';

const down=new T.Vector3(0,-1,0);
const point=(r:CharacterRig,node:T.Object3D)=>r.group.worldToLocal(node.getWorldPosition(new T.Vector3()));
/** Solve in fighter space so a turning torso cannot steer a straight punch off target. */
function limb(r:CharacterRig,upper:T.Object3D,lower:T.Object3D,end:T.Object3D,target:T.Vector3,pole:T.Vector3,a:number,b:number){
 r.group.updateMatrixWorld(true);
 const parent=upper.parent!,origin=upper.position,goal=parent.worldToLocal(r.group.localToWorld(target.clone())).sub(origin);
 const direction=goal.clone().normalize(),distance=T.MathUtils.clamp(goal.length(),.035,a+b-.002);
 const orientation=parent.getWorldQuaternion(new T.Quaternion()).invert().multiply(r.group.getWorldQuaternion(new T.Quaternion()));
 const bend=pole.clone().applyQuaternion(orientation);bend.addScaledVector(direction,-bend.dot(direction)).normalize();
 const along=(a*a-b*b+distance*distance)/(2*distance),height=Math.sqrt(Math.max(0,a*a-along*along));
 const elbow=direction.clone().multiplyScalar(along).addScaledVector(bend,height);
 upper.quaternion.setFromUnitVectors(down,elbow.clone().normalize());
 lower.quaternion.setFromUnitVectors(down,direction.multiplyScalar(distance).sub(elbow).normalize().applyQuaternion(upper.quaternion.clone().invert()));
 end.updateWorldMatrix(true,false);
}
function orient(r:CharacterRig,node:T.Object3D,rotation:T.Quaternion,weight=1){
 node.updateWorldMatrix(true,false);
 const q=node.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(r.group.getWorldQuaternion(new T.Quaternion())).multiply(rotation);
 node.quaternion.slerp(q,weight);
}
/** Ground-anchored legs for the DEV fighting styles. The gait (locomotion) owns the feet: its targets are the planted stance
 * when standing (stances.ts) and the stepping feet when moving, and the ground pass keeps them on the floor under whatever the
 * body does. Fighter layers only add per-foot edits over those targets during one sample (a heel pivot on the ball of the
 * foot, a kick path, a lunge) with weights that return to zero, and applyFighterLegs solves both legs once at the end (the
 * gait's own knee-hinged solve, floor-clamped, knee plane blended from the gait's): a zero edit is exactly the gait leg, so
 * nothing pops between the stance, the strikes and the stride, and planted feet never slide or sink.
 *  x/y/z    additive ankle shift (m)       yaw/heel  additive heading / heel lift (rad; heel pivots on the ball of the foot)
 *  to,tw    absolute rig-group ankle target and its weight      q,qw  absolute foot orientation and its weight
 *  pole,pw  knee-plane direction and its weight                  toe   additive toe bend */
export type FootEdit={x:number;y:number;z:number;yaw:number;heel:number;toe:number;to:T.Vector3;tw:number;q:T.Quaternion;qw:number;pole:T.Vector3;pw:number};
const newEdit=():FootEdit=>({x:0,y:0,z:0,yaw:0,heel:0,toe:0,to:new T.Vector3(),tw:0,q:new T.Quaternion(),qw:0,pole:new T.Vector3(),pw:0});
const edits:[FootEdit,FootEdit]=[newEdit(),newEdit()];let editing=false;
const FLOOR=-.795,BALL=.09,_target=new T.Vector3(),_orient=new T.Quaternion(),_euler=new T.Euler();
/** Start a fighter sample: no foot edits. */
export function beginFighterLegs(){for(const e of edits){e.x=e.y=e.z=e.yaw=e.heel=e.toe=e.tw=e.qw=e.pw=0;}editing=false;}
/** The edit record of one foot for this sample (edits add up across layers). */
export function editFoot(left:boolean){editing=true;return edits[left?0:1];}
/** Solve both legs over the gait targets with this sample's edits (nothing to do when no layer edited a foot). */
export function applyFighterLegs(r:CharacterRig){
 if(!editing)return;editing=false;
 for(let i=0;i<2;i++){
  const left=i===0,g=gaitFoot(r,left);if(!g)return;const e=edits[i],yaw0=g.yaw,yaw=g.yaw+e.yaw,heel=Math.max(0,e.heel),pitch=g.pitch+e.heel,toe=g.toe+e.toe-.45*heel;
  // A raised heel pivots on the ball of the foot (the ball holds its floor spot, the heel swings out and up); a flat foot
  // turns about the ankle, so the heel sets down exactly on its planted spot.
  const ball=BALL*T.MathUtils.clamp(heel/.2,0,1)**2;
  _target.set(g.x+e.x+ball*(Math.sin(yaw0)-Math.sin(yaw)),g.y+e.y+BALL*Math.sin(heel),g.z+e.z+ball*(Math.cos(yaw0)-Math.cos(yaw)));
  if(e.tw>0)_target.lerp(e.to,Math.min(1,e.tw));_target.y=Math.max(FLOOR,_target.y);
  const q=e.qw>0?_orient.setFromEuler(_euler.set(pitch,yaw,0,'YXZ')).slerp(e.q,Math.min(1,e.qw)):undefined;
  layerLeg(r,left,_target,yaw,pitch,toe,q,e.pw>0?e.pole:undefined,e.pw);
 }
}
/** Place an idle hand without changing bone lengths or detaching its weapon socket. */
export function placeHand(r:CharacterRig,left:boolean,target:T.Vector3,pole:T.Vector3){
 limb(r,left?r.leftArm:r.rightArm,left?r.leftElbow:r.rightElbow,left?r.leftHand:r.rightHand,target,pole,RIG_LENGTHS.upperArm,RIG_LENGTHS.forearm);
}

/** Alternate from the authority's contact times. Overlapping recoil and preparation
 * prevent the previous fist snapping back when the next punch begins. */
export function punchString(r:CharacterRig,age:number,first:number,gap:number,count:number,firstLeft:boolean,style:'emperor'|'karate',recoveryLimit=Infinity){
 const strikes=Array.from({length:count},(_,i)=>{
  const phase=age-first-i*gap,prep=i===0?first:Math.min(.12,gap*.62),recovery=Math.min(style==='emperor'?.14:.17,gap*.87,recoveryLimit);
  const pulse=(start:number,end:number,hold:number,tail:number)=>ease((phase-start)/(end-start))*(1-ease((phase-hold)/tail));
  return {left:i%2===0?firstLeft:!firstLeft,coil:pulse(-prep,-prep*.65,-prep*.60,prep*.60),
   hip:pulse(-prep*.78,-.012,.012,recovery),chest:pulse(-prep*.58,-.005,.016,recovery*.92),
   arm:pulse(-prep*.43,0,.016,recovery*.80)};
 }).filter(s=>s.coil+s.hip+s.chest+s.arm>.00001);
 if(!strikes.length)return;
 r.group.updateMatrixWorld(true);
 const hands=[point(r,r.leftHand),point(r,r.rightHand)];
 let drive=0,leftHeel=0,rightHeel=0;
 for(const s of strikes){
  // A left fist (+X) needs negative yaw to carry that shoulder toward +Z.
  const side=s.left?1:-1,turn=-side,heavy=style==='karate'?1.15:1;
  // The hips lead the shoulders: they wind first (coil), then snap through ahead of the chest.
  r.body.rotation.y+=turn*(.10*s.hip-.07*s.coil)*heavy;
  r.pelvis.rotation.y+=turn*(.28*s.hip-.13*s.coil)*heavy;
  r.spine.rotation.y+=turn*(.17*s.chest-.13*s.coil)*heavy;
  r.chest.rotation.y+=turn*(.18*s.chest-.12*s.coil)*heavy;
  r.chest.rotation.z+=side*.045*s.chest;r.head.rotation.y-=turn*.48*s.chest;
  const shoulder=s.left?r.leftShoulder:r.rightShoulder;
  shoulder.rotation.y+=turn*.13*s.chest;shoulder.rotation.x-=.06*s.arm;
  drive=Math.max(drive,s.hip);if(s.left)leftHeel=Math.max(leftHeel,s.hip);else rightHeel=Math.max(rightHeel,s.hip);
 }
 r.body.position.z+=.075*drive;r.body.position.y-=.018*drive;r.body.rotation.x+=.045*drive;
 // The driving foot pivots on its ball (heel out, heel up) while it stands planted; stepping feet belong to the gait.
 const stand=1-T.MathUtils.clamp(r.motion*1.6,0,1);
 if(stand>1e-3)for(const [i,heel] of [leftHeel,rightHeel].entries()){if(heel<=1e-4)continue;const e=editFoot(i===0),rear=i===1;e.heel+=(rear?.36:.14)*heel*stand;e.yaw+=(i===0?-1:1)*(rear?.42:.18)*heel*stand;}
 for(const [i,target] of hands.entries()){
  const left=i===0,side=left?1:-1,arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,hand=left?r.leftHand:r.rightHand;
  target.lerp(new T.Vector3(side*.20,.28,.24),drive);
  let extension=0;
  for(const s of strikes.filter(s=>s.left===left)){
   target.add(new T.Vector3(side*.035*s.coil,-.025*s.coil,-.09*s.coil));
   target.lerp(new T.Vector3(side*.12,.20,.96),s.arm);extension=Math.max(extension,s.arm);
  }
  limb(r,arm,elbow,hand,target,new T.Vector3(side*.7,-1,-.12),RIG_LENGTHS.upperArm,RIG_LENGTHS.forearm);
  orient(r,hand,new T.Quaternion().setFromEuler(new T.Euler(-Math.PI/2,0,side*.18)),extension);
 }
}

/** Knee chamber, support-foot pivot, heel thrust, rechamber, then plant. The knee rises into the chamber over the last punch's
 * recovery (a readable lift instead of a one-frame hitch), the heel snaps out on the authority's contact and the leg folds
 * straight back to the chamber before it steps down onto its stance spot by the string's end. */
export function donkeyKick(r:CharacterRig,phase:number,prep:number,recovery:number){
 const enter=ease((phase+prep*1.5)/(prep*1.05)),leave=1-ease((phase-recovery*.45)/(recovery*.55)),weight=enter*leave;
 if(weight<=0)return;
 const snap=Math.max(prep*.4,.042),x=T.MathUtils.clamp((phase+snap)/snap,0,1),extend=x*x*(3-2*x)*(1-ease((phase-recovery*.12)/(recovery*.33)));
 const pose=(node:T.Object3D,x:number,y:number,z:number)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 r.body.position.x+=.07*weight;r.body.position.z+=.16*weight;r.body.position.y-=.035*weight;
 pose(r.body,-.10-.12*extend,1.12,0);pose(r.pelvis,0,.12,0);
 // The pelvis stays over the support leg while the ribs lean away from the heel.
 pose(r.spine,-.10,-.08,-.50*extend);pose(r.chest,-.05,-.18,-.18*extend);
 pose(r.neck,0,-.25,0);pose(r.head,.02,-.73,.25*extend);
 pose(r.leftArm,-1.12,-.1,.30);pose(r.leftElbow,-1.5,0,0);
 pose(r.rightArm,.24+.35*extend,.05,-.35);pose(r.rightElbow,-1.3+.55*extend,0,0);
 // Support: planted on its gait spot, the foot turning with the hips about the ankle (standing; moving, it keeps stepping).
 // Kick: from its gait spot up to the knee chamber, heel thrust, rechamber and back down onto the same spot, the knee plane
 // and the sole turning toward the target with the weight (never a one-frame swap of the knee plane).
 const stand=1-T.MathUtils.clamp(r.motion*1.6,0,1),support=editFoot(true),kick=editFoot(false);
 support.q.setFromEuler(new T.Euler(-.06,1.65,0));support.qw=weight*stand;
 // The chamber tucks the heel under the kicking hip (never up past it, where the leg's direction would whip about the hip).
 kick.to.copy(point(r,r.rightLeg)).add(new T.Vector3(-.05,-.15,.07)).lerp(new T.Vector3(-.13,.02,.88),extend);kick.tw=weight;
 kick.q.setFromEuler(new T.Euler(-Math.PI/2-.10,0,0));kick.qw=weight;kick.pole.set(.08,.95,.3);kick.pw=weight;kick.toe-=.12*weight;
 r.group.updateMatrixWorld(true);
 const guard=point(r,r.leftHand).lerp(point(r,r.head).add(new T.Vector3(.08,-.05,.18)),weight);
 limb(r,r.leftArm,r.leftElbow,r.leftHand,guard,new T.Vector3(1,-1,0),RIG_LENGTHS.upperArm,RIG_LENGTHS.forearm);
}

const _gp=new T.Quaternion(),_gh=new T.Quaternion(),_gy=new T.Quaternion(),_gf=new T.Vector3(),UP=new T.Vector3(0,1,0);
/** Eyes on the opponent: turn the head about the vertical so its heading returns toward the rig's forward (the aim) by
 * `weight`, however the hips and chest wind and unwind under it (pitch and tilt are kept). */
export function holdGaze(r:CharacterRig,weight:number){
 if(weight<=0)return;_gp.identity();for(let n:T.Object3D|null=r.head.parent;n&&n!==r.group;n=n.parent)_gp.premultiply(n.quaternion);
 _gh.copy(_gp).multiply(r.head.quaternion);_gf.set(0,0,1).applyQuaternion(_gh);_gy.setFromAxisAngle(UP,-Math.atan2(_gf.x,_gf.z)*weight);
 r.head.quaternion.copy(_gp.invert().multiply(_gy).multiply(_gh));
}

/** Remember exits even on samples where a roll/reaction replaces the fighter layer. Some exit fields disappear when the
 * action ends, so a timestamp alone must not let the previous combo-ready pose come back afterward. */
type ReadyRec={id:string;spec:string;sampleTime:number;time:number;k:number;at:number;entryAt:number;entry:number;seenAt:number;seenUntil:number;seen:string;cut:number};const readyRecs=new WeakMap<object,ReadyRec>();
export function observeFighterReady(r:CharacterRig,p:PlayerView,time:number){
 const s=readyRecs.get(r);if(!s)return;
 const state=p.spec==='emperor'?p.emperor:p.spec==='ansatsuken'?p.arcade:undefined;
 if(s.id!==p.id||s.spec!==p.spec||time<s.sampleTime||p.hp<=0||!state||state.until<=state.at){readyRecs.delete(r);return;}
 s.sampleTime=time;
 for(const t of [p.roll?.at,p.hitReaction?.at,p.knockdown?.at,p.juggle?.at,p.lastBlock,p.lastSecondary,p.arcade?.parryAt,p.arcade?.demonStepAt]){
  if(t!==undefined&&t>=s.seenAt&&t<=time)s.cut=Math.min(s.cut,t);
 }
 if(p.guarding||p.blockTime>0)s.cut=Math.min(s.cut,time);
}
/** A primary string recovers directly into its next chamber, using the same visual hold/timeout as standard melee.
 * Damage-combo memory stays authoritative and unchanged. A new string hands the shown chamber to its first coil; a dodge,
 * parry, special or reaction releases it. The authority clears `action` at `until`, so remember the observed string kind. */
export function fighterReady(r:CharacterRig,p:PlayerView,time:number,state:{at:number;until:number;action?:string}|undefined,chains:string,style:'emperor'|'karate',carry:number){
 observeFighterReady(r,p,time);
 if(p.hp<=0)return 0;
 let s=readyRecs.get(r);if(!s){s={id:p.id,spec:p.spec,sampleTime:time,time,k:0,at:NaN,entryAt:NaN,entry:0,seenAt:NaN,seenUntil:NaN,seen:'',cut:Infinity};readyRecs.set(r,s);}
 if(state?.action){
  if(s.seenAt!==state.at||s.seenUntil!==state.until||s.seen!==state.action)s.cut=Infinity;
  s.seenAt=state.at;s.seenUntil=state.until;s.seen=state.action;
 }
 observeFighterReady(r,p,time);
 const kind=state?.action??(state&&state.at===s.seenAt&&state.until===s.seenUntil?s.seen:undefined),action=kind?{at:state!.at,until:state!.until,action:kind}:undefined;
 let k=0;
 if(action&&action.until>action.at&&time>=action.at&&time<action.until){
  // Entering a string: the hold this rig showed just before it began, handed over to the first strike's coil.
  if(s.entryAt!==action.at){s.entryAt=action.at;s.entry=s.at!==action.at&&time>=s.time&&time-s.time<.2?s.k:0;}
  k=s.entry*(1-ease((time-action.at)/.1));
  // The last recovery settles straight into the hold, without a detour back through the idle guard.
  const recovery=Math.min(.14/(p.attackRate||1),(action.until-action.at)*.25);
  if(action.action===chains)k=Math.max(k,ease((time-action.until+recovery)/recovery));
 }else if(action&&action.until>action.at&&action.action===chains&&time>=action.until){
  k=1-ease((time-action.until-GUARD_HOLD)/GUARD_FADE);
 }
 k*=s.cut<Infinity?1-ease((time-s.cut)/.22):1;
 k*=1-carry;s.time=time;s.k=k;s.at=action?.at??NaN;
 if(k<=1e-4)return 0;
 // A quicker, deeper bounce on the balls of the feet, weight settled back over the rear leg.
 const bounce=Math.sin(time*(style==='karate'?6.2:7.4))*.016;r.body.position.y+=(-.035+bounce)*k;editFoot(false).heel+=(.2+bounce*4)*k;
 if(style==='emperor'){
  // The next jab is the lead (left) fist: hips and chest wound toward the rear, the fist drawn in and low, rear hand high.
  r.body.rotation.y+=.07*k;r.pelvis.rotation.y+=.10*k;r.chest.rotation.y+=.08*k;r.head.rotation.y-=.1*k;
  r.leftArm.rotation.x+=.22*k;r.leftElbow.rotation.x-=.3*k;r.rightArm.rotation.x-=.12*k;
 }else{
  // Karate: the rear (right) fist chambered at the hip (hikite) with the lead hand out in front, hips wound back.
  r.body.rotation.y-=.08*k;r.pelvis.rotation.y-=.10*k;r.chest.rotation.y-=.06*k;r.head.rotation.y+=.08*k;
  r.rightArm.rotation.x+=.4*k;r.rightElbow.rotation.x-=.25*k;r.leftArm.rotation.x-=.12*k;r.leftElbow.rotation.x+=.2*k;
 }
 return k;
}
