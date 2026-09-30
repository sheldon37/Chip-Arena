import * as T from 'three';
import {CharacterRig} from './character-rig.js';
import {fighterGuard} from './ansatsuken-animation.js';
import {foot as placeFoot,STANDING_REACH} from './rig-ik.js';
const ease=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
/** The strokes of 天, constructed in the game's geometry style. */
export function heavenMark(){const g=new T.Group(),geometry=new T.BoxGeometry(1,1,1),material=new T.MeshBasicMaterial({color:0xff481d,toneMapped:false});for(const [x,y,sx,sy,angle]of [[0,.18,.22,.038,0],[0,.065,.36,.038,0],[-.065,-.075,.038,.27,-.48],[.075,-.075,.038,.29,.5]]){const m=new T.Mesh(geometry,material);m.position.set(x,y,0);m.scale.set(sx,sy,.012);m.rotation.z=angle;g.add(m);}g.name='heaven-mark';return g;}
export function poseRainWarrior(r:CharacterRig,age:number){
 for(const o of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])o.rotation.set(0,0,0);
 r.group.position.set(0,.88,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=false;
 const turn=ease((age-3.15)/1.15),guard=ease((age-4.05)/.7),yaw=Math.PI*(1-turn)-.12*turn;r.group.rotation.set(0,yaw,0);
 // The half turn is two pivot steps instead of a turntable: the root swings around the planted right foot for the first
 // half (the left foot steps round with a knee lift), then around the planted left foot. Pure function of age.
 const foot=.19,mid=Math.PI*.5-.06,around=(y:number,side:number,ox:number,oz:number)=>{const c=Math.cos(y),s=Math.sin(y);return [ox-side*foot*c,oz+side*foot*s];};
 const [mx,mz]=around(mid,-1,foot,0),lx=mx+foot*Math.cos(mid),lz=mz-foot*Math.sin(mid),[px,pz]=turn<=.5?around(yaw,-1,foot,0):around(yaw,1,lx,lz);r.group.position.set(px,.88,pz);
 const stepL=Math.sin(Math.PI*T.MathUtils.clamp(turn/.5,0,1)),stepR=Math.sin(Math.PI*T.MathUtils.clamp((turn-.5)/.5,0,1));
 r.head.rotation.x=.18*(1-guard);r.leftLeg.rotation.set(-.45*stepL,0,.12);r.rightLeg.rotation.set(-.45*stepR,0,-.12);r.leftKnee.rotation.x=.9*stepL;r.rightKnee.rotation.x=.9*stepR;r.body.position.x=.05*(stepR-stepL);r.leftArm.rotation.set(.12,0,.2);r.rightArm.rotation.set(.12,0,-.2);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.25;
 if(age>2.45&&age<3.9)r.head.rotation.y=-.5*Math.sin(Math.PI*ease((age-2.45)/1.45));fighterGuard(r,age,guard);const pivot=Math.sin(turn*Math.PI);r.pelvis.rotation.y-=.16*pivot;r.chest.rotation.y+=.12*pivot;r.body.position.y-=.025*pivot;r.leftKnee.rotation.x+=.12*pivot;r.rightShoulder.rotation.y=-.06*guard;r.spine.rotation.x+=Math.sin(age*2.1)*.008+.014*Math.sin(age*1.4)*(1-turn);r.chest.rotation.x-=.01*Math.sin(age*1.4-.3)*(1-turn);
 // Feet on world anchors: each stays planted while it is the pivot and steps round on a small arc when it is free, so the
 // turn and the guard crouch neither slide nor sink them.
 const end=-.12,[ex,ez]=around(end,1,lx,lz),anchors:[number,number,number][][]=[[[-foot,0,Math.PI],[lx,lz,mid]],[[foot,0,Math.PI],[ex-foot*Math.cos(end),ez+foot*Math.sin(end),end]]];
 for(const left of [true,false]){const [from,to]=anchors[left?0:1],u=left?T.MathUtils.clamp(turn/.5,0,1):T.MathUtils.clamp((turn-.5)/.5,0,1),e=ease(u),wx=from[0]+(to[0]-from[0])*e,wz=from[1]+(to[1]-from[1])*e,wy=from[2]+(to[2]-from[2])*e,dx=wx-px,dz=wz-pz,c=Math.cos(yaw),sn=Math.sin(yaw);
  placeFoot(r,left,new T.Vector3(dx*c-dz*sn,-.795+.1*Math.sin(Math.PI*u),dx*sn+dz*c),wy-yaw,.25*Math.sin(Math.PI*u),1,undefined,STANDING_REACH);}
}
export function rainCamera(camera:T.PerspectiveCamera,age:number,aspect:number){
 const t=ease(age/2.4),turn=ease((age-3.15)/1.6),reveal=ease((age-4.75)/.75);
 let eye=new T.Vector3(.45,9.5,5.2).lerp(new T.Vector3(.65,1.25,2.6),t),target=new T.Vector3(0,4.9,0).lerp(new T.Vector3(0,1.12,0),t);
 if(age>=2.4&&age<3.15){eye.lerp(new T.Vector3(.18,1.25,1.9),ease((age-2.4)/.75));}
 eye.lerp(new T.Vector3(-1.25,1.15,3.55),turn);eye.lerp(new T.Vector3(.65,1.23,4.7),reveal);target.lerp(new T.Vector3(0,.95,0),reveal);
 if(aspect<1.35)eye.z*=1.35/Math.max(.65,aspect);camera.position.copy(eye);camera.up.set(0,1,0);camera.lookAt(target);camera.fov=40;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:age<2.4?0:age<3.15?1:age<4.75?2:3,target};
}
export class RainWarrior {
 group=new T.Group();private rain:T.InstancedMesh;private puddles:T.InstancedMesh;private matrix=new T.Object3D();private red=new T.PointLight(0xff351c,0,3);private mark:T.Group|undefined;
 constructor(scene:T.Scene){scene.add(this.group);this.rain=new T.InstancedMesh(new T.BoxGeometry(.012,.32,.012),new T.MeshBasicMaterial({color:0x89b7db,transparent:true,opacity:.55,depthWrite:false,toneMapped:false}),180);this.rain.frustumCulled=false;this.puddles=new T.InstancedMesh(new T.RingGeometry(.08,.088,12),new T.MeshBasicMaterial({color:0x5289ae,transparent:true,opacity:.35,depthWrite:false,side:T.DoubleSide}),36);this.puddles.frustumCulled=false;this.group.add(this.rain,this.puddles,this.red);const floor=new T.Mesh(new T.CylinderGeometry(12,12,.03,48),new T.MeshPhongMaterial({color:0x060b13,specular:0x6689ae,shininess:95}));floor.position.y=.006;this.group.add(floor);}
 update(age:number,r:CharacterRig,quality:number){
  if(!this.mark||this.mark.parent!==r.chest){this.mark?.removeFromParent();this.mark=heavenMark();this.mark.position.set(0,.12,-.182);this.mark.rotation.y=Math.PI;this.mark.scale.setScalar(1.16);r.chest.add(this.mark);}
  this.mark.visible=true;const glow=ease((age-.6)/1.5)*(1-.25*ease((age-4)/1));this.mark.scale.setScalar(1.16+Math.sin(age*2)*.025);this.red.position.copy(this.mark.getWorldPosition(new T.Vector3()));this.red.intensity=glow*2;
  this.rain.count=quality<.5?75:180;for(let i=0;i<this.rain.count;i++){const a=i*2.39996,radius=1.4+(i%23)*.22,y=9-((age*7+i*.618)%9);this.matrix.position.set(Math.cos(a)*radius+y*.09,y,Math.sin(a)*radius);this.matrix.rotation.set(0,0,.09);this.matrix.scale.setScalar(1);this.matrix.updateMatrix();this.rain.setMatrixAt(i,this.matrix.matrix);}this.rain.instanceMatrix.needsUpdate=true;
  for(let i=0;i<36;i++){const a=i*2.4,radius=.6+(i%11)*.5,cycle=(age*1.8+i*.37)%1;this.matrix.position.set(Math.cos(a)*radius,.028,Math.sin(a)*radius);this.matrix.rotation.set(-Math.PI/2,0,0);this.matrix.scale.setScalar(.2+cycle*4);this.matrix.updateMatrix();this.puddles.setMatrixAt(i,this.matrix.matrix);}this.puddles.instanceMatrix.needsUpdate=true;
 }
}
