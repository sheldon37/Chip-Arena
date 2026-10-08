import {WEAPON_INTROS,isWeaponIntro,type WeaponIntro} from '../shared/weapon-intros.js';
import {poseWeaponIntro,weaponIntroCamera} from './weapon-intro-motion.js';
import {introCurve,heroIdle,smooth,type IntroCurveOptions} from './intro-curves.js';
import {foot,STANDING_REACH} from './rig-ik.js';
import {poseEmperor,emperorCamera} from './emperor-intro.js';
import {poseRainWarrior,rainCamera} from './ansatsuken-intro.js';
import {poseMatrixThrone,matrixCamera} from './leet-intro.js';
import * as T from 'three';
import {CharacterRig,braceWeapon,supportHand} from './character-rig.js';
import {IntroStyle} from '../shared/intros.js';
export const INTRO_HIT:Record<IntroStyle,number>={...Object.fromEntries(Object.entries(WEAPON_INTROS).map(([key,value])=>[key,value.hit])) as Record<WeaponIntro,number>,'dev-siren':Infinity,'dev-emperor':4.1,'dev-ansatsuken':4.6,'dev-matrix':5.9,'neon-drop':.88,'vector-slide':1.08,'signal-spin':1.36,'arc-salute':1.56,'pulse-wave':1.62};
export const ease=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
type V=[number,number,number];
type Joint='body'|'pelvis'|'spine'|'chest'|'head'|'leftArm'|'rightArm'|'leftElbow'|'rightElbow'|'leftLeg'|'rightLeg'|'leftKnee'|'rightKnee'|'leftFoot'|'rightFoot';
type Pose={at:number;position?:V;turn?:number;bodyY?:number;joints?:Partial<Record<Joint,V>>};
const neutral:Partial<Record<Joint,V>>={leftArm:[-.10,0,.13],rightArm:[-.10,0,-.13],leftElbow:[-.18,0,0],rightElbow:[-.18,0,0]};
const poses:Record<Exclude<IntroStyle,'dev-siren'|'dev-emperor'|'dev-matrix'|'dev-ansatsuken'|WeaponIntro>,Pose[]>={
 'neon-drop':[
  {at:0,position:[0,4.1,0],turn:-.4,joints:{leftArm:[-2.3,0,.4],rightArm:[-2.5,0,-.4],leftLeg:[-.45,0,.1],rightLeg:[.3,0,-.1],leftKnee:[.9,0,0]}},
  {at:.32,position:[0,3.7,0],turn:-.25,joints:{leftArm:[-1.3,0,.5],rightArm:[-.9,0,-.3],leftLeg:[-.6,0,.1],rightLeg:[.25,0,-.1],leftKnee:[1.1,0,0]}},
  {at:.88,bodyY:-.06,joints:{body:[.5,0,0],head:[-.4,0,0],leftLeg:[-.85,0,.15],rightLeg:[-.85,0,-.15],leftKnee:[1.6,0,0],rightKnee:[1.6,0,0],leftArm:[-.3,0,.8],rightArm:[-.6,0,-.12],rightElbow:[0,0,0]}},
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
  {at:.85,position:[0,1.16,0],turn:-4.2,joints:{leftArm:[-.35,0,1.1],rightArm:[-.1,0,-1.2],leftElbow:[-.6,0,0],rightElbow:[-.38,0,0],leftLeg:[-.6,0,.1],rightLeg:[.2,0,-.1],leftKnee:[1.1,0,0]}},
  {at:1.36,turn:-6.4,bodyY:-.14,joints:{body:[.15,0,0],leftLeg:[-.3,0,.18],rightLeg:[-.3,0,-.18],leftKnee:[.6,0,0],rightKnee:[.6,0,0],leftArm:[0,0,.8],rightArm:[-.4,0,-.8]}},
  {at:2.15,turn:-6.5,joints:{head:[0,.2,0],rightArm:[-1.5,0,-.2],rightElbow:[-.5,0,0],leftArm:[0,0,.3],leftElbow:[-1,0,0],leftLeg:[0,0,.1],rightLeg:[0,0,-.1]}},
  {at:3.2,turn:-6.5,joints:{head:[0,.2,0],rightArm:[-1.3,0,-.2],rightElbow:[-.6,0,0],leftArm:[0,0,.3],leftElbow:[-1,0,0],leftLeg:[0,0,.1],rightLeg:[0,0,-.1]}},
 ],
 'arc-salute':[
  {at:0,position:[0,.88,-1.5]},
  {at:.45,position:[0,.88,-.84]},
  {at:.9,position:[0,.88,-.2]},
  {at:1.2,turn:-.2,joints:{leftElbow:[-.7,0,0]}},
  {at:2.45,turn:-.2,joints:{head:[0,.1,-.07],leftElbow:[-.7,0,0]}},
  {at:3.2,turn:-.25,joints:{head:[0,.15,0],chest:[-.05,0,0],leftArm:[.1,0,.25],leftElbow:[-1.15,0,0]}},
 ],
 'pulse-wave':[
  {at:0,turn:-.1,joints:{leftArm:[-.5,0,-.2],rightArm:[-.5,0,.2],leftElbow:[-1.5,0,0],rightElbow:[-1.5,0,0]}},
  {at:.75,turn:0,bodyY:-.12,joints:{body:[.14,0,0],head:[.18,0,0],leftArm:[-.8,0,-.4],rightArm:[-.8,0,.4],leftElbow:[-1.35,0,0],rightElbow:[-1.35,0,0],leftLeg:[-.25,0,.1],rightLeg:[-.25,0,-.1],leftKnee:[.5,0,0],rightKnee:[.5,0,0]}},
  {at:1.45,position:[0,1.22,0],turn:0,joints:{head:[.12,0,0],leftArm:[-1,0,-.35],rightArm:[-1,0,.35],leftElbow:[-1.1,0,0],rightElbow:[-1.1,0,0],leftLeg:[-.35,0,.12],rightLeg:[-.35,0,-.12],leftKnee:[.6,0,0],rightKnee:[.6,0,0]}},
  {at:1.75,position:[0,1.16,0],turn:0,joints:{body:[-.18,0,0],head:[-.1,0,0],leftArm:[-.5,0,1.3],rightArm:[-.5,0,-1.3],leftElbow:[-.3,0,0],rightElbow:[-.3,0,0],leftLeg:[0,0,.18],rightLeg:[0,0,-.18]}},
  {at:2.45,turn:-.2,joints:{leftArm:[-.4,0,.55],rightArm:[-.4,0,-.55],leftElbow:[-.4,0,0],rightElbow:[-.4,0,0],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
  {at:3.2,turn:-.2,joints:{head:[0,.1,-.07],leftArm:[-.6,0,.6],leftElbow:[-1.2,0,0],rightArm:[.05,0,-.2],rightElbow:[-.6,0,0],leftLeg:[0,0,.12],rightLeg:[0,0,-.12]}},
 ],
};
/** Complete keyed poses, sampled from absolute time: no accumulated rotations or stale preview state. */
export function poseIntro(r:CharacterRig,style:IntroStyle,age:number){
 if(style==='dev-siren')return; // SirenIntroStage owns this pose.
 if(isWeaponIntro(style)){poseWeaponIntro(r,style,age);return;}
 if(style==='dev-emperor'){poseEmperor(r,age);return;}if(style==='dev-ansatsuken'){poseRainWarrior(r,age);return;}if(style==='dev-matrix'){poseMatrixThrone(r,age);return;}const t=Math.max(0,age),frames=poses[style];
 for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);
 const times=frames.map(f=>f.at),landing=LANDING[style],curve:IntroCurveOptions={impact:landing,pass:times.map((_,i)=>i).filter(i=>i>0&&i<times.length-1&&!landing?.includes(i))};
 const at=(get:(f:Pose)=>number,time:number)=>introCurve(times,frames.map(get),time,curve),sample=(get:(f:Pose)=>number,offset=0)=>at(get,t-offset);
 const rootAt=(time:number)=>({x:at(f=>f.position?.[0]??0,time),y:at(f=>f.position?.[1]??.88,time),z:at(f=>f.position?.[2]??0,time),yaw:at(f=>f.turn??-.12,time)});
 const root=rootAt(t);if(style==='neon-drop'&&t<.88)root.y=.88+3.22*(1-(t/.88)**2);
 r.group.position.set(root.x,root.y,root.z);r.group.rotation.set(0,root.yaw,0);r.group.scale.setScalar(1);r.body.position.set(0,sample(f=>f.bodyY??0),0);r.weapon.visible=r.offWeapon.visible=false;
 // Gravity drop for neon-drop: lands at 7.3 m/s and the legs absorb it (critically damped dip), instead of easing to a stop.
 if(style==='neon-drop'&&t>.88){const tau=t-.88,speed=7.3,w=speed/(.2*Math.E);r.body.position.y-=speed*tau*Math.exp(-w*tau);}
 // Kinetic chain: elbows, chest, head and hands trail the pose they belong to.
 for(const key of ['body','pelvis','spine','chest','head','leftArm','rightArm','leftElbow','rightElbow','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as Joint[]){const lag=key.includes('Elbow')?.06:key==='chest'?.04:key==='head'?.07:0;r[key].rotation.set(...[0,1,2].map(axis=>sample(f=>(f.joints?.[key]||neutral[key]||[0,0,0])[axis],lag)) as V);}
 const breathing=Math.sin(t*2.2)*.007,idle=heroIdle(t);r.spine.rotation.x+=breathing+idle.breath;r.neck.rotation.x-=breathing*.6+idle.breath*.5;r.leftShoulder.rotation.z=breathing;r.rightShoulder.rotation.z=-breathing;r.head.rotation.z+=idle.headRoll;r.head.rotation.y+=idle.headYaw;
 const elbow=(left:boolean)=>sample(f=>(f.joints?.[left?'leftElbow':'rightElbow']||neutral[left?'leftElbow':'rightElbow']||[0,0,0])[0],.08);r.leftHand.rotation.x=-elbow(true)*.12;r.rightHand.rotation.x=-elbow(false)*.12;
 // Living hold: the weight drifts from foot to foot (hips over the feet, legs compensating so the feet stay put).
 const shift=idle.shift;r.body.position.x+=shift;r.pelvis.rotation.z+=idle.roll;r.chest.rotation.z-=idle.roll*1.1;
 if(style==='arc-salute')walkIn(r,t,rootAt);else{for(const leg of [r.leftLeg,r.rightLeg])leg.rotation.z-=shift/.5+idle.roll;for(const ankle of [r.leftFoot,r.rightFoot])ankle.rotation.z+=shift/.5;}
 if(style==='vector-slide'){const cross=ease((t-1.75)/.9);braceWeapon(r,new T.Vector3(.16,.09,.24),new T.Euler(0,0,.6),cross);const target=new T.Object3D();target.position.set(-.16,.025,.28);target.rotation.z=-.6;r.chest.add(target);supportHand(r,target,cross);target.removeFromParent();}
 if(style==='arc-salute'){const salute=ease((t-1.15)/.41)*(1-ease((t-2.25)/.55));r.head.rotation.x+=.06*Math.sin(Math.PI*smooth((t-1.5)/.9));r.chest.rotation.x-=.04*Math.sin(Math.PI*smooth((t-1.5)/.9));braceWeapon(r,new T.Vector3(-.24,.44+.025*smooth((t-1.5)/.8),.12),new T.Euler(-1.8,0,-.5),salute);}
}
/** Key indices that are landings (arrive travelling, then settle) per generic style. */
const LANDING:Partial<Record<IntroStyle,number[]>>={'neon-drop':[2],'vector-slide':[2],'signal-spin':[3],'pulse-wave':[3]};
/** arc-salute's walk-in as planted footsteps: [left, lift, land, lead] with the landing spot `lead` ahead of the root at
 * landing. A stateless phase layer adds the pelvis bob, hip yaw and counter arm swing, fading out as the walk ends. */
const WALK:[boolean,number,number,number][]=[[false,-.05,.22,.16],[true,.18,.45,.16],[false,.41,.68,.16],[true,.64,.91,.12],[false,.87,1.12,.03],[true,1.06,1.3,0]];
function walkIn(r:CharacterRig,t:number,rootAt:(time:number)=>{x:number;z:number;yaw:number}){
 const walk=1-smooth((t-.95)/.4),phase=2*Math.PI*(t-.22)/.46;
 r.body.position.y+=-.028*(.5+.5*Math.cos(2*phase))*walk;r.pelvis.rotation.y+=.08*Math.cos(phase)*walk;r.chest.rotation.y-=.07*Math.cos(phase)*walk;
 r.leftArm.rotation.x-=.42*Math.cos(phase)*walk;r.rightArm.rotation.x+=.42*Math.cos(phase)*walk;r.leftElbow.rotation.x-=.25*walk;r.rightElbow.rotation.x-=.25*walk;
 const g=rootAt(t),place=(time:number,left:boolean,lead:number)=>{const q=rootAt(time),x=left?.12:-.12;return {x:q.x+x*Math.cos(q.yaw)+lead*Math.sin(q.yaw),z:q.z-x*Math.sin(q.yaw)+lead*Math.cos(q.yaw),yaw:q.yaw};};
 for(const left of [true,false]){let anchor=place(0,left,left?.16:-.1),y=0,heel=0;
  for(const [foot_,lift,land,lead] of WALK){if(foot_!==left)continue;const next=place(land,left,lead);if(t>=land){anchor=next;heel=t<land+.14?-.2*(1-smooth((t-land)/.14)):0;continue;}
   if(t>lift){const u=(t-lift)/(land-lift),e=smooth(u);anchor={x:anchor.x+(next.x-anchor.x)*e,z:anchor.z+(next.z-anchor.z)*e,yaw:anchor.yaw+(next.yaw-anchor.yaw)*e};y=4*.07*u*(1-u);heel=.3-.5*smooth(u*1.15);}
   else if(t>lift-.08)heel=.3*smooth((t-lift+.08)/.08);
   break;}
  const dx=anchor.x-g.x,dz=anchor.z-g.z,c=Math.cos(g.yaw),sn=Math.sin(g.yaw);
  foot(r,left,new T.Vector3(dx*c-dz*sn,-.795+y+(heel>0?.08*Math.sin(heel):.05*Math.sin(-heel)),dx*sn+dz*c),anchor.yaw-g.yaw,heel,1,undefined,STANDING_REACH);}
}

/** A shot: eye/end are offsets from the root (lerped, or swept at constant radius when `orbit`), `anchor` pins the root to
 * the landing spot (a drop falls through a still frame instead of dragging the camera down with it). */
type Shot={until:number;eye:V;end:V;target:V;targetEnd?:V;fov:number;endFov?:number;roll?:number;orbit?:boolean;anchor?:boolean};
const hero:Shot={until:4.8,eye:[1.25,.16,4.1],end:[.65,.26,3.95],target:[0,-.08,0],fov:40};
const shots:Record<Exclude<IntroStyle,'dev-siren'|'dev-emperor'|'dev-matrix'|'dev-ansatsuken'|WeaponIntro>,Shot[]>={
 'neon-drop':[{until:.72,eye:[2.3,.5,4.8],end:[1.5,.2,4.3],target:[0,2.1,0],targetEnd:[0,.5,0],fov:42,roll:-.035,anchor:true},{until:1.55,eye:[2.4,-.4,3.8],end:[1.65,-.2,3.5],target:[0,-.13,0],fov:46,anchor:true},{until:3.2,eye:[-1.65,.25,2.65],end:[-.65,.38,3],target:[0,.25,0],fov:43},hero],
 'vector-slide':[{until:.62,eye:[1.6,-.52,2.4],end:[1,-.45,2.4],target:[0,-.44,0],fov:39,roll:.08},{until:1.65,eye:[2.8,.1,4.1],end:[1.5,.12,3.6],target:[0,0,0],fov:45},{until:3.2,eye:[-1.9,.4,3.2],end:[-.8,.34,3.1],target:[0,.23,0],fov:40},hero],
 'signal-spin':[{until:.48,eye:[-.9,.4,2.9],end:[-.5,.45,2.7],target:[0,.25,0],fov:39},{until:1.55,eye:[-3.3,.55,3.7],end:[3.1,.55,3.7],target:[0,.1,0],fov:48,roll:-.04,orbit:true},{until:3.2,eye:[2.1,.2,3.2],end:[.65,.4,3.1],target:[0,.28,0],fov:42},hero],
 'arc-salute':[{until:1.1,eye:[1.6,.15,3.9],end:[1,.28,3.55],target:[0,.17,0],fov:42},{until:2.35,eye:[-.7,.6,2.05],end:[-.35,.62,1.95],target:[-.03,.43,0],fov:42},{until:3.2,eye:[-1.4,.1,3.5],end:[.5,.2,4.1],target:[0,.1,0],fov:43},hero],
 'pulse-wave':[{until:1.28,eye:[1.3,.25,2.6],end:[.55,.3,2.35],target:[0,.17,.2],fov:42},{until:2.3,eye:[-.9,.15,3.1],end:[-2.4,1.6,5.2],target:[0,.1,0],fov:48,roll:.04},{until:3.2,eye:[2,.4,3.8],end:[1.25,.23,4.1],target:[0,.13,0],fov:43},hero],
};
export function introCamera(camera:T.PerspectiveCamera,style:IntroStyle,age:number,root:T.Vector3,aspect:number){
 if(style==='dev-siren')return {shot:0,target:root}; // SirenIntroStage owns the camera.
 if(isWeaponIntro(style))return weaponIntroCamera(camera,style,age,root,aspect);
 if(style==='dev-emperor')return emperorCamera(camera,age,aspect);if(style==='dev-ansatsuken')return rainCamera(camera,age,aspect);if(style==='dev-matrix')return matrixCamera(camera,age,aspect);let start=0;const seq=shots[style];let shot=seq[seq.length-1];for(const candidate of seq){shot=candidate;if(age<candidate.until)break;start=candidate.until;}const u=ease((age-start)/Math.max(.01,shot.until-start));
 const offset=new T.Vector3(...shot.eye).lerp(new T.Vector3(...shot.end),u);if(shot.orbit){const a0=Math.atan2(shot.eye[0],shot.eye[2]),a1=Math.atan2(shot.end[0],shot.end[2]),radius=T.MathUtils.lerp(Math.hypot(shot.eye[0],shot.eye[2]),Math.hypot(shot.end[0],shot.end[2]),u),a=T.MathUtils.lerp(a0,a1,u);offset.x=radius*Math.sin(a);offset.z=radius*Math.cos(a);}
 if(shot.anchor)root=new T.Vector3(root.x,.88,root.z);if(aspect<1.35)offset.multiplyScalar(1.35/Math.max(.65,aspect));camera.position.copy(root).add(offset);const target=root.clone().add(new T.Vector3(...shot.target).lerp(new T.Vector3(...(shot.targetEnd||shot.target)),u));camera.up.set(Math.sin((shot.roll||0)*(1-u)),1,0).normalize();camera.lookAt(target);camera.fov=T.MathUtils.lerp(shot.fov,shot.endFov??shot.fov,u);camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:seq.indexOf(shot),target};
}
