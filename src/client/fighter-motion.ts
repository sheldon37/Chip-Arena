import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {RIG_LENGTHS} from './character-rig.js';
import {smoothMotion as ease} from './motion-quality.js';

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
function leg(r:CharacterRig,left:boolean,target:T.Vector3,pole=new T.Vector3(0,0,1)){
 limb(r,left?r.leftLeg:r.rightLeg,left?r.leftKnee:r.rightKnee,left?r.leftFoot:r.rightFoot,target,pole,RIG_LENGTHS.thigh,RIG_LENGTHS.shin);
}

/** Alternate from the authority's contact times. Overlapping recoil and preparation
 * prevent the previous fist snapping back when the next punch begins. */
export function punchString(r:CharacterRig,age:number,first:number,gap:number,count:number,firstLeft:boolean,style:'emperor'|'karate'){
 const strikes=Array.from({length:count},(_,i)=>{
  const phase=age-first-i*gap,prep=i===0?first:Math.min(.12,gap*.62),recovery=Math.min(style==='emperor'?.14:.17,gap*.87);
  const pulse=(start:number,end:number,hold:number,tail:number)=>ease((phase-start)/(end-start))*(1-ease((phase-hold)/tail));
  return {left:i%2===0?firstLeft:!firstLeft,coil:pulse(-prep,-prep*.65,-prep*.60,prep*.60),
   hip:pulse(-prep*.78,-.012,.012,recovery),chest:pulse(-prep*.58,-.005,.016,recovery*.92),
   arm:pulse(-prep*.43,0,.016,recovery*.80)};
 }).filter(s=>s.coil+s.hip+s.chest+s.arm>.00001);
 if(!strikes.length)return;
 r.group.updateMatrixWorld(true);
 const feet=[point(r,r.leftFoot),point(r,r.rightFoot)],hands=[point(r,r.leftHand),point(r,r.rightHand)];
 let drive=0,leftHeel=0,rightHeel=0;
 for(const s of strikes){
  // A left fist (+X) needs negative yaw to carry that shoulder toward +Z.
  const side=s.left?1:-1,turn=-side,heavy=style==='karate'?1.15:1;
  r.body.rotation.y+=turn*(.09*s.hip-.06*s.coil)*heavy;
  r.pelvis.rotation.y+=turn*(.23*s.hip-.10*s.coil)*heavy;
  r.spine.rotation.y+=turn*(.17*s.chest-.13*s.coil)*heavy;
  r.chest.rotation.y+=turn*(.18*s.chest-.12*s.coil)*heavy;
  r.chest.rotation.z+=side*.045*s.chest;r.head.rotation.y-=turn*.48*s.chest;
  const shoulder=s.left?r.leftShoulder:r.rightShoulder;
  shoulder.rotation.y+=turn*.13*s.chest;shoulder.rotation.x-=.06*s.arm;
  drive=Math.max(drive,s.hip);if(s.left)leftHeel=Math.max(leftHeel,s.hip);else rightHeel=Math.max(rightHeel,s.hip);
 }
 r.body.position.z+=.075*drive;r.body.position.y-=.018*drive;r.body.rotation.x+=.045*drive;
 for(const [i,heel] of [leftHeel,rightHeel].entries()){
  const foot=i===0?r.leftFoot:r.rightFoot,target=feet[i].clone();target.y+=heel*.025;
  leg(r,i===0,target);orient(r,foot,new T.Quaternion().setFromEuler(new T.Euler(-.12-.22*heel,(i===0?-.45:.45)*heel,0)));
 }
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

/** Knee chamber, support-foot pivot, heel thrust, rechamber, then plant. */
export function donkeyKick(r:CharacterRig,phase:number,prep:number,recovery:number){
 const enter=ease((phase+prep)/(prep*.60)),leave=1-ease((phase-recovery*.58)/(recovery*.42)),weight=enter*leave;
 if(weight<=0)return;
 const extend=ease((phase+prep*.40)/(prep*.40))*(1-ease((phase-recovery*.18)/(recovery*.40)));
 r.group.updateMatrixWorld(true);const support=point(r,r.leftFoot),rest=point(r,r.rightFoot);
 const pose=(node:T.Object3D,x:number,y:number,z:number)=>node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),weight);
 r.body.position.x+=.07*weight;r.body.position.z+=.16*weight;r.body.position.y-=.035*weight;
 pose(r.body,-.10-.12*extend,1.12,0);pose(r.pelvis,0,.12,0);
 // The pelvis stays over the support leg while the ribs lean away from the heel.
 pose(r.spine,-.10,-.08,-.50*extend);pose(r.chest,-.05,-.18,-.18*extend);
 pose(r.neck,0,-.25,0);pose(r.head,.02,-.73,.25*extend);
 pose(r.leftArm,-1.12,-.1,.30);pose(r.leftElbow,-1.5,0,0);
 pose(r.rightArm,.24+.35*extend,.05,-.35);pose(r.rightElbow,-1.3+.55*extend,0,0);
 leg(r,true,support);orient(r,r.leftFoot,new T.Quaternion().setFromEuler(new T.Euler(-.06,1.65,0)),weight);
 const target=new T.Vector3(-.12,-.16,.25).lerp(new T.Vector3(-.13,.02,.88),extend);rest.lerp(target,weight);
 leg(r,false,rest,new T.Vector3(-.65,.25,1));
 orient(r,r.rightFoot,new T.Quaternion().setFromEuler(new T.Euler(-Math.PI/2-.10,0,0)),weight);
 r.rightToe.rotation.set(-.12*weight,0,0);
 r.group.updateMatrixWorld(true);
 const guard=point(r,r.leftHand).lerp(point(r,r.head).add(new T.Vector3(.08,-.05,.18)),weight);
 limb(r,r.leftArm,r.leftElbow,r.leftHand,guard,new T.Vector3(1,-1,0),RIG_LENGTHS.upperArm,RIG_LENGTHS.forearm);
}
