import * as T from 'three';
import {clamp} from '../shared/config.js';
import {rolling,rollVelocity} from '../shared/souls-combat.js';
import type {PlayerView} from '../shared/simulation.js';
import {settleJointCovers,type CharacterRig} from './character-rig.js';
import {smoothMotion as ease,plantLeg,ankleInGroup} from './motion-quality.js';

type Bone='pelvis'|'spine'|'chest'|'neck'|'head'|'leftShoulder'|'rightShoulder'|'leftArm'|'rightArm'|'leftElbow'|'rightElbow'|'leftHand'|'rightHand'|'leftLeg'|'rightLeg'|'leftKnee'|'rightKnee'|'leftFoot'|'rightFoot'|'leftToe'|'rightToe';
/** Fully tucked roll pose (local Euler x,y,z per bone): chin on the chest, rounded back, knees to the chest, forearms folded
 * in with the weapon hand kept close. The roll blends the live locomotion pose toward it by `tuck`. */
const TUCK:[Bone,number,number,number][]=[['pelvis',-.30,0,0],['spine',.42,0,0],['chest',.30,0,0],['neck',.42,0,-.2],['head',.6,.25,-.25],['leftShoulder',.10,0,.10],['rightShoulder',.10,0,-.10],['leftArm',-1.10,0,.34],['rightArm',-1.0,0,-.30],['leftElbow',-1.60,0,0],['rightElbow',-1.55,0,0],['leftHand',.2,0,0],['rightHand',.2,0,0],['leftLeg',-1.80,0,.14],['rightLeg',-1.62,0,-.14],['leftKnee',2.25,0,0],['rightKnee',2.15,0,0],['leftFoot',-.45,0,0],['rightFoot',-.5,0,0],['leftToe',0,0,0],['rightToe',0,0,0]];
/** The anticipation crouch (live pose toward it by `dip`, under the tuck): knees load, the chest folds toward the roll. */
const DIP:[Bone,number,number,number][]=[['spine',.22,0,0],['chest',.12,0,0],['leftLeg',-.75,0,.05],['rightLeg',-.55,0,-.05],['leftKnee',1.2,0,0],['rightKnee',1.05,0,0],['leftFoot',-.35,0,0],['rightFoot',-.4,0,0]];
/** The low crouch the tuck opens into (legs and spine; the exit plants then set the feet flat on the floor). */
const RISE:[Bone,number,number,number][]=[['spine',.2,0,0],['chest',.08,0,0],['leftLeg',-1.15,0,.1],['rightLeg',-.7,0,-.1],['leftKnee',1.8,0,0],['rightKnee',1.6,0,0],['leftFoot',-.55,0,0],['rightFoot',-.75,0,0]];
const _a=new T.Vector3(),_t=new T.Vector3(),_q=new T.Quaternion(),_e=new T.Euler(),_live=new T.Quaternion(),_live2=new T.Vector3(),_w=new T.Quaternion(),_d=new T.Quaternion(),_p=new T.Quaternion(),_s=new T.Quaternion(),X=new T.Vector3(1,0,0),Y=new T.Vector3(0,1,0),Z=new T.Vector3(0,0,1);
const wrap=(a:number)=>a-Math.PI*2*Math.round(a/(Math.PI*2));
/** Exit plants: [lead, group x, group z, foot yaw, phase start] (lead foot first, rear foot .04 later). */
const EXIT=[[true,.13,.15,.12,.72],[false,-.13,0,-.25,.76]] as const,_g=new T.Quaternion(),_h=new T.Quaternion(),_k=new T.Quaternion(),_ee=new T.Euler();
/** Standing sole height in rig-group space: braced ankles at -.795, heel box .07 below the ankle. */
export const ROLL_FLOOR=-.865;
/** Body shell parts (the rig's own part names; weapons and cosmetics are not collision parts). */
const SHELL=/^(hip-shell|abdomen-shell|abdomen|ribcage|upper-chest|collar|blank-head|crown|back-panel|jaw|waist-band|sternum|neck-joint)$|-(upper-arm|forearm|thumb|thigh|calf|shin|palm|fingers|foot|heel|shoulder-cap|cuff|kneecap|elbow-joint|hip-joint|knee-joint|deltoid)$/;
/** Shin and knee boxes may graze the floor in a kneeling crouch (their corners swing below the ankle); the exit guard ignores them. */
const KNEEL=/-(shin|calf|kneecap|knee-joint)$/;
const shells=new WeakMap<object,T.Mesh[]>(),_c=new T.Vector3(),_bb=new T.Box3();
function shellMeshes(r:CharacterRig){
 let list=shells.get(r);if(list)return list;list=[];const skip=new Set<T.Object3D>();r.weapon.traverse(o=>skip.add(o));r.offWeapon.traverse(o=>skip.add(o));
 r.body.traverse(o=>{if((o as T.Mesh).isMesh&&!skip.has(o)&&SHELL.test(o.name))list!.push(o as T.Mesh);});shells.set(r,list);return list;
}
/** Lowest point of the body shell in rig-group height (the group only yaws, so world y minus the group's y). Matrices must be current. */
export function lowestShell(r:CharacterRig,kneel=true){
 let min=Infinity;const gy=r.group.position.y;
 for(const m of shellMeshes(r)){if(!kneel&&KNEEL.test(m.name))continue;const g=m.geometry;if(!g.boundingBox)g.computeBoundingBox();_bb.copy(g.boundingBox!);const e=m.matrixWorld.elements;
  for(let i=0;i<8;i++){_c.set(i&1?_bb.max.x:_bb.min.x,i&2?_bb.max.y:_bb.min.y,i&4?_bb.max.z:_bb.min.z);const y=e[1]*_c.x+e[5]*_c.y+e[9]*_c.z+e[13];if(y<min)min=y;}}
 return min-gy;
}

/** A Souls roll: a quick dip toward the roll, a shoulder-first dive, a tight tucked somersault whose rounded back rolls along the
 * floor through a full turn, then the feet land and the fighter comes up into a low crouch. Phase = roll.at..until (pure).
 * The motion root is solved against the floor every frame: the lowest body part (heels in the dip, shoulders and back in the
 * tuck, the feet on the exit) touches the sole plane instead of the tumble pivoting in the air, and nothing goes below it.
 * Forward and side rolls face the roll direction (the heading turns in over the dip and pivots back over u .7-.92 above planted
 * feet); a roll more than ~115 degrees behind the facing is a backward somersault that keeps facing forward. No camera rotation. */
export function dodgeRollMotion(r:CharacterRig,p:PlayerView,time:number){
 if(!rolling(p,time))return false;
 const roll=p.roll!,u=clamp((time-roll.at)/(roll.until-roll.at),0,1),enter=ease(u/.2),leave=1-ease((u-.62)/.26),tuck=enter*leave,dip=Math.sin(Math.PI*clamp(u/.3,0,1))*(1-enter*.6);
 const travel=wrap(Math.atan2(roll.direction.x,roll.direction.z)-p.yaw),back=Math.abs(travel)>2,dir=back?-1:1,turn=back?wrap(travel-Math.PI):travel;
 // One continuous turn over u .07-.8: it starts slow in the dive, is fastest on the back and eases onto the feet.
 const rotation=dir*ease((u-.07)/.73)*Math.PI*2,heading=turn*ease(u/.2)*(1-ease((u-.7)/.22));
 const hold=ease(u/.12)*(1-ease((u-.74)/.18)),crouch=ease((u-.7)/.2),arc=Math.sin(Math.PI*clamp((u-.03)/.8,0,1));
 // Shoulder first: the torso yaws and banks so the leading (right) shoulder meets the floor, and unwinds as the roll opens.
 const shoulder=.55*arc*dir,bank=-.95*arc;
 _live.copy(r.body.quaternion);_live2.copy(r.body.position);
 r.body.quaternion.copy(_live).slerp(_d.setFromAxisAngle(Y,heading),hold).multiply(_w.setFromAxisAngle(X,rotation+dir*.22*dip)).multiply(_s.setFromAxisAngle(Y,shoulder)).multiply(_p.setFromAxisAngle(Z,bank));
 // Horizontally the hips lead into the dive and trail on the rise (the root carries the travel); height comes from the floor.
 r.body.position.set(0,_live2.y,dir*.10*Math.sin(Math.PI*clamp(u/.7,0,1))-dir*.06*crouch);
 r.body.position.x=_live2.x*(1-hold);r.body.position.z=_live2.z*(1-hold)+r.body.position.z*hold;
 for(const [bone,x,y,z] of DIP)r[bone].quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),dip*(1-tuck));
 const rise=ease((u-.5)/.22);for(const [bone,x,y,z] of RISE)r[bone].quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),rise*(1-tuck));
 for(const [bone,x,y,z] of TUCK)r[bone].quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),tuck);
 // Ground contact: settle the whole tumble onto the floor (the body never floats above it or sinks through it).
 settleJointCovers(r);r.group.updateMatrixWorld(true);r.body.position.y+=ROLL_FLOOR-lowestShell(r);
 // Coming up: the hips hand over from the floor contact to a low crouch over the planting feet, which rises toward the stance.
 const up=ease((u-.64)/.2);if(up>0)r.body.position.y+=(_live2.y-.14+.05*ease((u-.84)/.16)-r.body.position.y)*up;
 // Exit: lead foot, then rear foot, reach the floor ahead of the crouch spot by the roll travel still to come, so they land
 // still in the world as the authoritative roll decelerates; the ankle moves positionally from the tuck, never under the floor.
 const plant=()=>{if(u<=.72)return;
  const left=roll.until-time,w=left>1e-4?rollVelocity(roll,time,left):{x:0,y:0,z:0},yaw=r.group.rotation.y,c=Math.cos(yaw),sn=Math.sin(yaw),gx=(w.x*c-w.z*sn)*left,gz=(w.x*sn+w.z*c)*left;
  for(const [lead,x,z,footYaw,start] of EXIT){
   const k=ease((u-start)/.14);if(k<=1e-4)continue;ankleInGroup(r,lead,_a);_t.set(x+gx*(lead?1:.45),0,z+gz*(lead?1:.45));const reach=Math.hypot(_t.x,_t.z);if(reach>.3)_t.multiplyScalar(.3/reach);_t.y=-.795+.05*(1-k);
   _a.lerp(_t,k);_a.y=Math.max(_a.y,-.795+(k<1?.012:0));plantLeg(r,lead,_a,footYaw,1,k);
  }
 };
 _live2.copy(r.body.position);plant();
 // Nothing goes through the floor: a part the crouch or the unfolding legs pushed under it lifts the hips (and the feet re-plant).
 for(let pass=0;pass<4;pass++){settleJointCovers(r);r.group.updateMatrixWorld(true);const under=ROLL_FLOOR-lowestShell(r);if(under<=5e-4)break;r.body.position.y+=under*(pass<3?1.6:1);plant();}
 // Weapons remain attached to their hand sockets while the torso tumbles.
 r.weapon.rotation.set(.35,0,0);r.offWeapon.rotation.set(.35,0,0);
 r.group.updateMatrixWorld(true);
 for(const [hand,side] of [[r.rightHand,-1],[r.leftHand,1]] as const){
  const desired=r.group.getWorldQuaternion(_g).multiply(_h.setFromEuler(_ee.set(0,heading+side*Math.PI/2,0))).multiply(_h.setFromEuler(_ee.set(-.55,0,0)));
  hand.quaternion.slerp(hand.parent!.getWorldQuaternion(_k).invert().multiply(desired),tuck);
 }
 return true;
}
