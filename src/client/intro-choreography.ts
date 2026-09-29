import {poseCurve} from './pose-curves.js';
import {poseEmperor,emperorCamera} from './emperor-intro.js';
import {poseRainWarrior,rainCamera} from './ansatsuken-intro.js';
import {poseMatrixThrone,matrixCamera} from './leet-intro.js';
import * as T from 'three';
import {CharacterRig,braceWeapon,supportHand} from './character-rig.js';
import {IntroStyle} from '../shared/intros.js';
export const INTRO_HIT:Record<IntroStyle,number>={'dev-emperor':4.1,'dev-ansatsuken':4.6,'dev-matrix':5.9,'neon-drop':.88,'vector-slide':1.08,'signal-spin':1.36,'arc-salute':1.56,'pulse-wave':1.62};
export const ease=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
type V=[number,number,number];
type Joint='body'|'pelvis'|'spine'|'chest'|'head'|'leftArm'|'rightArm'|'leftElbow'|'rightElbow'|'leftLeg'|'rightLeg'|'leftKnee'|'rightKnee'|'leftFoot'|'rightFoot';
type Pose={at:number;position?:V;turn?:number;bodyY?:number;joints?:Partial<Record<Joint,V>>};
const neutral:Partial<Record<Joint,V>>={leftArm:[-.10,0,.13],rightArm:[-.10,0,-.13],leftElbow:[-.18,0,0],rightElbow:[-.18,0,0]};
const poses:Record<Exclude<IntroStyle,'dev-emperor'|'dev-matrix'|'dev-ansatsuken'>,Pose[]>={
 'neon-drop':[
  {at:0,position:[0,4.1,0],turn:-.4,joints:{leftArm:[-2.3,0,.4],rightArm:[-2.5,0,-.4],leftLeg:[-.45,0,.1],rightLeg:[.3,0,-.1],leftKnee:[.9,0,0]}},
  {at:.32,position:[0,3.7,0],turn:-.25,joints:{leftArm:[-1.3,0,.5],rightArm:[-.9,0,-.3],leftLeg:[-.6,0,.1],rightLeg:[.25,0,-.1],leftKnee:[1.1,0,0]}},
  {at:.88,bodyY:-.28,joints:{body:[.5,0,0],head:[-.4,0,0],leftLeg:[-.85,0,.15],rightLeg:[-.85,0,-.15],leftKnee:[1.6,0,0],rightKnee:[1.6,0,0],leftArm:[-.3,0,.8],rightArm:[-.6,0,-.12],rightElbow:[0,0,0]}},
  {at:1.2,bodyY:-.25,joints:{body:[.42,0,0],head:[-.4,0,0],leftLeg:[-.75,0,.15],rightLeg:[-.75,0,-.15],leftKnee:[1.4,0,0],rightKnee:[1.4,0,0],leftArm:[-.3,0,.6],rightArm:[-.4,0,-.12]}},
  {at:2.25,turn:-.25,joints:{chest:[0,-.15,0],rightArm:[-.35,0,-.25],rightElbow:[-1.3,0,0],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
  {at:3.2,turn:-.2,joints:{chest:[0,-.12,0],head:[0,.1,0],rightArm:[-.32,0,-.25],rightElbow:[-1.25,0,0],leftLeg:[0,0,.1],rightLeg:[0,0,-.1]}},
 ],
 'vector-slide':[
  {at:0,position:[-3.1,.88,0],turn:1.3,joints:{body:[.12,0,-.15],leftLeg:[-.5,0,.2],rightLeg:[.6,0,-.1],leftArm:[.6,0,.2],rightArm:[-.8,0,-.2]}},
  {at:.45,position:[-2.3,.88,0],turn:1.25,bodyY:-.18,joints:{body:[.25,0,-.18],leftLeg:[-.8,0,.2],rightLeg:[.8,0,-.2],leftKnee:[1.2,0,0],rightKnee:[.4,0,0],leftArm:[.8,0,.2],rightArm:[-.9,0,-.2]}},
  {at:1.08,position:[0,.88,0],turn:.2,bodyY:-.22,joints:{body:[.2,0,-.25],leftLeg:[-.35,0,.7],rightLeg:[-.4,0,-.25],leftKnee:[.7,0,0],rightKnee:[.7,0,0],leftArm:[-.2,0,1],rightArm:[.4,0,-.8]}},
  {at:1.6,turn:-.2,joints:{body:[0,0,.04],leftLeg:[0,0,.12],rightLeg:[0,0,-.12],rightArm:[-1.15,0,.6],rightElbow:[-1.6,0,0]}},
  {at:2.65,turn:-.22,joints:{head:[0,.1,-.06],leftArm:[-.85,0,-.7],leftElbow:[-1.9,0,-.1],rightArm:[-1.05,0,.7],rightElbow:[-1.65,0,.1],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
 ],
 'signal-spin':[
  {at:0,turn:-.12,joints:{leftArm:[-.2,0,.3],rightArm:[-.2,0,-.3]}},
  {at:.35,turn:-1.3,bodyY:-.12,joints:{body:[.1,0,0],leftLeg:[-.35,0,.1],rightLeg:[-.3,0,-.1],leftKnee:[.6,0,0],rightKnee:[.6,0,0],leftArm:[-.8,0,-.3],rightArm:[-.8,0,.3],leftElbow:[-1.2,0,0],rightElbow:[-1.2,0,0]}},
  {at:.85,position:[0,1.16,0],turn:-4.2,joints:{leftArm:[-.2,0,1.25],rightArm:[-.2,0,-1.25],leftLeg:[-.6,0,.1],rightLeg:[.2,0,-.1],leftKnee:[1.1,0,0]}},
  {at:1.36,turn:-6.4,bodyY:-.14,joints:{body:[.15,0,0],leftLeg:[-.3,0,.18],rightLeg:[-.3,0,-.18],leftKnee:[.6,0,0],rightKnee:[.6,0,0],leftArm:[0,0,.8],rightArm:[-.4,0,-.8]}},
  {at:2.15,turn:-6.5,joints:{head:[0,.2,0],rightArm:[-1.5,0,-.2],rightElbow:[-.25,0,0],leftArm:[0,0,.3],leftElbow:[-1,0,0],leftLeg:[0,0,.1],rightLeg:[0,0,-.1]}},
  {at:3.2,turn:-6.5,joints:{head:[0,.2,0],rightArm:[-1.35,0,-.15],rightElbow:[-.35,0,0],leftArm:[0,0,.3],leftElbow:[-1,0,0],leftLeg:[0,0,.1],rightLeg:[0,0,-.1]}},
 ],
 'arc-salute':[
  {at:0,position:[0,.88,-1.5],joints:{leftLeg:[-.55,0,0],rightLeg:[.55,0,0],rightKnee:[.7,0,0],leftArm:[.6,0,.1],rightArm:[-.6,0,-.1]}},
  {at:.45,position:[0,.9,-.8],joints:{leftLeg:[.55,0,0],rightLeg:[-.55,0,0],leftKnee:[.7,0,0],leftArm:[-.6,0,.1],rightArm:[.6,0,-.1]}},
  {at:.9,position:[0,.88,-.15],joints:{leftLeg:[-.4,0,0],rightLeg:[.4,0,0],rightKnee:[.6,0,0],leftArm:[.45,0,.1],rightArm:[-.45,0,-.1]}},
  {at:1.2,turn:-.2,joints:{leftElbow:[-.7,0,0]}},
  {at:2.45,turn:-.2,joints:{head:[0,.1,-.07],leftElbow:[-.7,0,0]}},
  {at:3.2,turn:-.25,joints:{head:[0,.15,0],chest:[-.05,0,0],leftArm:[.1,0,.25],leftElbow:[-1.15,0,0]}},
 ],
 'pulse-wave':[
  {at:0,turn:-.1,joints:{leftArm:[-.5,0,-.2],rightArm:[-.5,0,.2],leftElbow:[-1.5,0,0],rightElbow:[-1.5,0,0]}},
  {at:.75,turn:0,bodyY:-.12,joints:{body:[.14,0,0],head:[.18,0,0],leftArm:[-.8,0,-.4],rightArm:[-.8,0,.4],leftElbow:[-1.35,0,0],rightElbow:[-1.35,0,0],leftLeg:[-.25,0,.1],rightLeg:[-.25,0,-.1],leftKnee:[.5,0,0],rightKnee:[.5,0,0]}},
  {at:1.45,position:[0,1.22,0],turn:0,joints:{head:[.12,0,0],leftArm:[-1,0,-.35],rightArm:[-1,0,.35],leftElbow:[-1.1,0,0],rightElbow:[-1.1,0,0],leftLeg:[-.35,0,.12],rightLeg:[-.35,0,-.12],leftKnee:[.6,0,0],rightKnee:[.6,0,0]}},
  {at:1.75,position:[0,1.16,0],turn:0,joints:{body:[-.18,0,0],head:[-.1,0,0],leftArm:[-.5,0,1.4],rightArm:[-.5,0,-1.4],leftLeg:[0,0,.18],rightLeg:[0,0,-.18]}},
  {at:2.45,turn:-.2,joints:{leftArm:[-.4,0,.55],rightArm:[-.4,0,-.55],leftElbow:[-.4,0,0],rightElbow:[-.4,0,0],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
  {at:3.2,turn:-.2,joints:{head:[0,.1,-.07],leftArm:[-.6,0,.6],leftElbow:[-1.2,0,0],rightArm:[.05,0,-.2],rightElbow:[-.6,0,0],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
 ],
};
/** Complete keyed poses, sampled from absolute time: no accumulated rotations or stale preview state. */
export function poseIntro(r:CharacterRig,style:IntroStyle,age:number){
 if(style==='dev-emperor'){poseEmperor(r,age);return;}if(style==='dev-ansatsuken'){poseRainWarrior(r,age);return;}if(style==='dev-matrix'){poseMatrixThrone(r,age);return;}const t=Math.max(0,age),frames=poses[style];
 for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);
 const times=frames.map(f=>f.at),sample=(get:(f:Pose)=>number,offset=0)=>poseCurve(times,frames.map(get),t-offset);
 r.group.position.set(sample(f=>f.position?.[0]??0),sample(f=>f.position?.[1]??.88),sample(f=>f.position?.[2]??0));r.group.rotation.set(0,sample(f=>f.turn??-.12),0);r.group.scale.setScalar(1);r.body.position.set(0,sample(f=>f.bodyY??0),0);r.weapon.visible=r.offWeapon.visible=false;
 for(const key of ['body','pelvis','spine','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as Joint[]){const lag=key.includes('Elbow')?.025:key==='chest'?.016:key==='head'?-.025:0;r[key].rotation.set(...[0,1,2].map(axis=>sample(f=>(f.joints?.[key]||neutral[key]||[0,0,0])[axis],lag)) as V);}
 const breathing=Math.sin(t*2.2)*.007;r.spine.rotation.x+=breathing;r.neck.rotation.x-=breathing*.6;r.leftShoulder.rotation.z=breathing;r.rightShoulder.rotation.z=-breathing;
 r.leftHand.rotation.x=-r.leftElbow.rotation.x*.07;r.rightHand.rotation.x=-r.rightElbow.rotation.x*.07;
 if(style==='vector-slide'){const cross=ease((t-1.75)/.9);braceWeapon(r,new T.Vector3(.16,.09,.24),new T.Euler(0,0,.6),cross);const target=new T.Object3D();target.position.set(-.16,.025,.28);target.rotation.z=-.6;r.chest.add(target);supportHand(r,target,cross);target.removeFromParent();}
 if(style==='arc-salute'){const salute=ease((t-1.15)/.41)*(1-ease((t-2.25)/.55));braceWeapon(r,new T.Vector3(-.19,.48,.18),new T.Euler(0,0,-.5),salute);}
 // A restrained breathing hold keeps the final card alive.
 if(t>=3.2)r.body.position.y+=Math.sin((t-3.2)*2.4)*.007;
}

type Shot={until:number;eye:V;end:V;target:V;targetEnd?:V;fov:number;endFov?:number;roll?:number};
const hero:Shot={until:4.8,eye:[1.25,.23,4.1],end:[.65,.35,3.95],target:[0,.08,0],fov:40};
const shots:Record<Exclude<IntroStyle,'dev-emperor'|'dev-matrix'|'dev-ansatsuken'>,Shot[]>={
 'neon-drop':[{until:.72,eye:[2.3,.5,4.8],end:[1.5,.2,4.3],target:[0,.28,0],fov:42,roll:-.035},{until:1.55,eye:[2.4,-.4,3.8],end:[1.65,-.2,3.5],target:[0,-.13,0],fov:46},{until:3.2,eye:[-1.65,.25,2.65],end:[-.65,.38,3],target:[0,.25,0],fov:43},hero],
 'vector-slide':[{until:.62,eye:[1.6,-.52,2.4],end:[1,-.45,2.4],target:[0,-.44,0],fov:39,roll:.08},{until:1.65,eye:[2.8,.1,4.1],end:[1.5,.12,3.6],target:[0,0,0],fov:45},{until:3.2,eye:[-1.9,.4,3.2],end:[-.8,.34,3.1],target:[0,.23,0],fov:40},hero],
 'signal-spin':[{until:.48,eye:[-.9,.4,2.9],end:[-.5,.45,2.7],target:[0,.25,0],fov:39},{until:1.55,eye:[-3.3,.55,3.7],end:[3.1,.55,3.7],target:[0,.1,0],fov:48,roll:-.04},{until:3.2,eye:[2.1,.2,3.2],end:[.65,.4,3.1],target:[0,.28,0],fov:42},hero],
 'arc-salute':[{until:1.1,eye:[1.6,.15,3.9],end:[1,.28,3.55],target:[0,.17,0],fov:42},{until:2.35,eye:[-.7,.6,2.05],end:[-.35,.62,1.95],target:[-.03,.43,0],fov:42},{until:3.2,eye:[-1.4,.1,3.5],end:[.5,.2,4.1],target:[0,.1,0],fov:43},hero],
 'pulse-wave':[{until:1.28,eye:[1.3,.25,2.6],end:[.55,.3,2.35],target:[0,.17,.2],fov:42},{until:2.3,eye:[-.9,.15,3.1],end:[-2.4,1.6,5.2],target:[0,.1,0],fov:48,roll:.04},{until:3.2,eye:[2,.4,3.8],end:[1.25,.23,4.1],target:[0,.13,0],fov:43},hero],
};
export function introCamera(camera:T.PerspectiveCamera,style:IntroStyle,age:number,root:T.Vector3,aspect:number){
 if(style==='dev-emperor')return emperorCamera(camera,age,aspect);if(style==='dev-ansatsuken')return rainCamera(camera,age,aspect);if(style==='dev-matrix')return matrixCamera(camera,age,aspect);let start=0;const seq=shots[style];let shot=seq[seq.length-1];for(const candidate of seq){shot=candidate;if(age<candidate.until)break;start=candidate.until;}const u=ease((age-start)/Math.max(.01,shot.until-start));
 const offset=new T.Vector3(...shot.eye).lerp(new T.Vector3(...shot.end),u);if(aspect<1.35)offset.multiplyScalar(1.35/Math.max(.65,aspect));camera.position.copy(root).add(offset);const target=root.clone().add(new T.Vector3(...shot.target).lerp(new T.Vector3(...(shot.targetEnd||shot.target)),u));camera.up.set(Math.sin((shot.roll||0)*(1-u)),1,0).normalize();camera.lookAt(target);camera.fov=T.MathUtils.lerp(shot.fov,shot.endFov??shot.fov,u);camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:seq.indexOf(shot),target};
}
