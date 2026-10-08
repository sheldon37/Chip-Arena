import * as T from 'three';
import {CharacterRig} from './character-rig.js';
import {fighterGuard} from './ansatsuken-animation.js';
import {introCurve,smooth} from './intro-curves.js';
import {hand} from './rig-ik.js';
import {footAt,plantFeet,plantOf,type IntroStep,type Plant} from './intro-footwork.js';
const ease=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
/** The strokes of 天, constructed in the game's geometry style. */
export function heavenMark(){const g=new T.Group(),geometry=new T.BoxGeometry(1,1,1),material=new T.MeshBasicMaterial({color:0xff481d,toneMapped:false});for(const [x,y,sx,sy,angle]of [[0,.18,.22,.038,0],[0,.065,.36,.038,0],[-.065,-.075,.038,.27,-.48],[.075,-.075,.038,.29,.5]]){const m=new T.Mesh(geometry,material);m.position.set(x,y,0);m.scale.set(sx,sy,.012);m.rotation.z=angle;g.add(m);}g.name='heaven-mark';return g;}
/** The half turn: one yaw curve that flows through the hand-over between the two pivot steps (no stop at the switch). */
const TURN_KEYS=[3.12,3.68,4.3],TURN_YAW=[Math.PI,Math.PI*.5-.06,-.12],LAND_L=3.6,LIFT_R=3.8,LAND_R=4.26;
const turnYaw=(t:number)=>introCurve(TURN_KEYS,TURN_YAW,t,{pass:[1]});
/** Stoic stance (square, shoulder width) and the karate guard's bladed stance (stances.ts KARATE_STANCE). */
const SQUARE_L:Plant={x:.15,z:0,yaw:0},SQUARE_R:Plant={x:-.15,z:0,yaw:0},GUARD_L:Plant={x:.17,z:.13,yaw:-.15},GUARD_R:Plant={x:-.15,z:-.21,yaw:-.55};
const rotate=(yaw:number,p:Plant)=>({x:p.x*Math.cos(yaw)+p.z*Math.sin(yaw),z:-p.x*Math.sin(yaw)+p.z*Math.cos(yaw)});
// The root orbits the planted right foot for the first step, then the planted left foot; the two pivots agree when the
// left foot lands and the root blends between them over the double support, so its velocity never jumps.
const START_L=plantOf({x:0,z:0,yaw:Math.PI},SQUARE_L),START_R=plantOf({x:0,z:0,yaw:Math.PI},SQUARE_R);
const aroundRight=(yaw:number)=>{const o=rotate(yaw,SQUARE_R);return {x:START_R.x-o.x,z:START_R.z-o.z};};
const landYaw=turnYaw(LAND_L),landRoot=aroundRight(landYaw),LEFT_SPOT=plantOf({...landRoot,yaw:landYaw},GUARD_L);
const aroundLeft=(yaw:number)=>{const o=rotate(yaw,GUARD_L);return {x:LEFT_SPOT.x-o.x,z:LEFT_SPOT.z-o.z};};
const END_ROOT={...aroundLeft(TURN_YAW[2]),yaw:TURN_YAW[2]};
/** Rain Warrior's root (world x/z and yaw) at an age; the intro camera follows it. */
export function rainRoot(t:number){const yaw=turnYaw(t),a=aroundRight(yaw),b=aroundLeft(yaw),w=smooth((t-LAND_L)/(LIFT_R-.1-LAND_L));return {x:a.x+(b.x-a.x)*w,z:a.z+(b.z-a.z)*w,yaw};}
// Left: steps round onto the guard's lead spot, then pivots on its ball while the right foot steps. Right: pivots on its
// ball under the first step, then steps round onto the guard's rear spot.
const LEFT_STEPS:IntroStep[]=[{...LEFT_SPOT,lift:3.14,land:LAND_L,h:.08},{...LEFT_SPOT,yaw:TURN_YAW[2]+GUARD_L.yaw,lift:LAND_L,land:4.3,h:0}];
const RIGHT_STEPS:IntroStep[]=[{...START_R,yaw:turnYaw(LIFT_R-.12)+SQUARE_R.yaw,lift:3.12,land:LIFT_R-.12,h:0},{...plantOf(END_ROOT,GUARD_R),lift:LIFT_R,land:LAND_R,h:.08}];
/** Hadoken beat on the hit (4.6 s): the hands cup at the rear hip as the turn completes, drive forward on the hit, then the
 * fighter settles into the karate guard before the name card. Weights are pure functions of age. */
export function rainHadoken(t:number){
 const charge=smooth((t-3.94)/.42)*(1-smooth((t-4.34)/.28)),release=smooth((t-4.34)/.28)*(1-smooth((t-4.9)/.5)),guard=smooth((t-4.72)/.66);return {charge,release,guard};
}
const _q=new T.Quaternion(),_e=new T.Euler(),_palm=new T.Vector3(),_wrist=new T.Euler();
const toward=(o:T.Object3D,x:number,y:number,z:number,w:number)=>{if(w>0)o.quaternion.slerp(_q.setFromEuler(_e.set(x,y,z)),w);};
export function poseRainWarrior(r:CharacterRig,age:number){
 for(const o of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])o.rotation.set(0,0,0);
 const t=Math.max(0,age),g=rainRoot(t);r.group.position.set(g.x,.88,g.z);r.group.rotation.set(0,g.yaw,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=false;
 // Stoic rain stance: head bowed, arms loose, a slow deep breath and a faint weight drift (it fades into the turn).
 const breath=Math.sin(2*Math.PI*(t-.3)/3.3),still=1-smooth((t-2.9)/.5),drift=Math.sin(2*Math.PI*(t-.8)/4.6)*still,look=smooth((t-2.3)/1.4);
 r.body.position.y=-.03+.007*breath;r.body.position.x=.014*drift;r.spine.rotation.x=.016*breath;r.chest.rotation.x=-.014*breath;r.neck.rotation.x=.006*breath;
 r.head.rotation.x=.2*(1-look)+.014*Math.sin(2*Math.PI*(t-.7)/3.3)*still;r.leftShoulder.rotation.z=.022*breath;r.rightShoulder.rotation.z=-.022*breath;
 r.leftArm.rotation.set(.1+.02*breath,0,.19+.012*breath);r.rightArm.rotation.set(.1+.02*breath,0,-.19-.012*breath);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.25-.03*breath;
 // The head looks back over the shoulder before the body turns (it leads the turn).
 r.head.rotation.y=-.5*Math.sin(Math.PI*smooth((t-2.45)/1.45));
 // Kinetic chain through the turn: hips lead the root, chest trails it, the loose arms flare and swing late.
 const lead=turnYaw(t+.07)-g.yaw,trail=turnYaw(t-.06)-g.yaw,spin=(turnYaw(t+.03)-turnYaw(t-.03))/.06,swing=(turnYaw(t-.04)-turnYaw(t-.1))/.06;
 r.pelvis.rotation.y+=.7*lead;r.spine.rotation.y+=.45*trail-.35*lead;r.chest.rotation.y+=.45*trail-.35*lead;
 r.leftArm.rotation.z+=.035*Math.abs(swing);r.rightArm.rotation.z-=.035*Math.abs(swing);r.leftArm.rotation.x+=.03*swing;r.rightArm.rotation.x-=.03*swing;r.body.position.y-=.008*Math.abs(spin);
 // Guard, then the hadoken charge and release layered over it.
 const {charge,release,guard}=rainHadoken(t);fighterGuard(r,t,guard,false);
 toward(r.pelvis,0,-.35,0,charge);toward(r.chest,0,-.5,0,charge);toward(r.leftArm,-.9,-.65,-.2,charge);toward(r.rightArm,-.68,.5,.2,charge);toward(r.leftElbow,-1.46,0,0,charge);toward(r.rightElbow,-1.66,0,0,charge);toward(r.leftHand,.25,0,-.4,charge);toward(r.rightHand,.25,0,.4,charge);
 toward(r.body,.12,-.1,0,release);toward(r.pelvis,0,.08,0,release);toward(r.chest,0,0,0,release);
 r.body.position.y-=.05*charge+.03*release;r.body.position.z+=.12*release;r.head.rotation.x-=.05*release;
 // Palms driven out together, heels of the hands touching (chest-space targets), wrists bent back.
 if(release>0)for(const left of [true,false])hand(r,left,_palm.set(left?.055:-.055,.07,.4),_wrist.set(-2.2,left?1.1:-1.1,0),release);
 const left=footAt(t,START_L,LEFT_STEPS),right=footAt(t,START_R,RIGHT_STEPS);right.heel+=.3*release;
 plantFeet(r,[left,right],g,.4);
}
export function rainCamera(camera:T.PerspectiveCamera,age:number,aspect:number){
 const t=ease(age/2.4),turn=ease((age-3.15)/1.6),reveal=ease((age-4.75)/.75),root=rainRoot(age),follow=new T.Vector3(root.x,0,root.z);
 let eye=new T.Vector3(.45,9.5,5.2).lerp(new T.Vector3(.65,1.25,2.6),t),target=new T.Vector3(0,4.9,0).lerp(new T.Vector3(0,1.12,0),t);
 if(age>=2.4&&age<3.15){eye.lerp(new T.Vector3(.18,1.25,1.9),ease((age-2.4)/.75));}
 eye.lerp(new T.Vector3(-1.25,1.15,3.55),turn);eye.lerp(new T.Vector3(.65,1.23,4.7),reveal);target.lerp(new T.Vector3(0,.95,0),reveal);
 if(aspect<1.35)eye.z*=1.35/Math.max(.65,aspect);eye.add(follow);target.add(follow);camera.position.copy(eye);camera.up.set(0,1,0);camera.lookAt(target);camera.fov=40;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:age<2.4?0:age<3.15?1:age<4.75?2:3,target};
}
export class RainWarrior {
 group=new T.Group();private rain:T.InstancedMesh;private puddles:T.InstancedMesh;private matrix=new T.Object3D();private red=new T.PointLight(0xff351c,0,3);private mark:T.Group|undefined;
 /** The hadoken: a blue-white orb gathered between the cupped hands, driven out along the facing on the hit. */
 private orb=new T.Mesh(new T.IcosahedronGeometry(.1,1),new T.MeshBasicMaterial({color:0xa9e4ff,transparent:true,opacity:0,depthWrite:false,toneMapped:false,blending:T.AdditiveBlending}));private orbLight=new T.PointLight(0x6cc8ff,0,3.2);
 constructor(scene:T.Scene){scene.add(this.group);this.rain=new T.InstancedMesh(new T.BoxGeometry(.012,.32,.012),new T.MeshBasicMaterial({color:0x89b7db,transparent:true,opacity:.55,depthWrite:false,toneMapped:false}),180);this.rain.frustumCulled=false;this.puddles=new T.InstancedMesh(new T.RingGeometry(.08,.088,12),new T.MeshBasicMaterial({color:0x5289ae,transparent:true,opacity:.35,depthWrite:false,side:T.DoubleSide}),36);this.puddles.frustumCulled=false;this.group.add(this.rain,this.puddles,this.red,this.orb,this.orbLight);this.orb.name='rain-hadoken';const floor=new T.Mesh(new T.CylinderGeometry(12,12,.03,48),new T.MeshPhongMaterial({color:0x060b13,specular:0x6689ae,shininess:95}));floor.position.y=.006;this.group.add(floor);}
 update(age:number,r:CharacterRig,quality:number){
  if(!this.mark||this.mark.parent!==r.chest){this.mark?.removeFromParent();this.mark=heavenMark();this.mark.position.set(0,.12,-.182);this.mark.rotation.y=Math.PI;this.mark.scale.setScalar(1.16);r.chest.add(this.mark);}
  this.mark.visible=true;const glow=ease((age-.6)/1.5)*(1-.25*ease((age-4)/1));this.mark.scale.setScalar(1.16+Math.sin(age*2)*.025);this.red.position.copy(this.mark.getWorldPosition(new T.Vector3()));this.red.intensity=glow*2;
  const {charge,release}=rainHadoken(age),flight=Math.max(0,age-4.56),power=Math.max(charge,release>0&&flight<.7?1-smooth((flight-.3)/.4):0),root=rainRoot(age);
  this.orb.visible=power>.01;if(this.orb.visible){const mid=r.leftHand.getWorldPosition(new T.Vector3()).add(r.rightHand.getWorldPosition(new T.Vector3())).multiplyScalar(.5);mid.x+=Math.sin(root.yaw)*(.08+flight*6.5);mid.z+=Math.cos(root.yaw)*(.08+flight*6.5);this.orb.position.copy(mid);this.orb.scale.setScalar((.5+.9*smooth(charge*1.4)+.3*smooth(flight/.15))*(1+.08*Math.sin(age*41)));this.orb.rotation.set(age*5,age*7,0);(this.orb.material as T.MeshBasicMaterial).opacity=.7*power;}
  this.orbLight.position.copy(this.orb.position);this.orbLight.intensity=this.orb.visible?2.4*power:0;
  this.rain.count=quality<.5?75:180;for(let i=0;i<this.rain.count;i++){const a=i*2.39996,radius=1.4+(i%23)*.22,y=9-((age*7+i*.618)%9);this.matrix.position.set(Math.cos(a)*radius+y*.09,y,Math.sin(a)*radius);this.matrix.rotation.set(0,0,.09);this.matrix.scale.setScalar(1);this.matrix.updateMatrix();this.rain.setMatrixAt(i,this.matrix.matrix);}this.rain.instanceMatrix.needsUpdate=true;
  for(let i=0;i<36;i++){const a=i*2.4,radius=.6+(i%11)*.5,cycle=(age*1.8+i*.37)%1;this.matrix.position.set(Math.cos(a)*radius,.028,Math.sin(a)*radius);this.matrix.rotation.set(-Math.PI/2,0,0);this.matrix.scale.setScalar(.2+cycle*4);this.matrix.updateMatrix();this.puddles.setMatrixAt(i,this.matrix.matrix);}this.puddles.instanceMatrix.needsUpdate=true;
 }
}
