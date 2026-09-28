import * as T from 'three';
import {CharacterRig,supportHand} from './character-rig.js';
import {emperorGuard} from './emperor-animation.js';
import {EmperorEffects} from './emperor-effects.js';
const ease=(x:number)=>{x=T.MathUtils.clamp(x,0,1);return x*x*(3-2*x);};
/** Wrist at brow -> lift the hand -> hold the challenge. No second salute or stance reset. */
export function poseEmperor(r:CharacterRig,t:number){
 for(const o of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])o.rotation.set(0,0,0);
 r.group.position.set(0,.88,0);r.group.rotation.set(0,-.1,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=true;r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);emperorGuard(r,t);
 const lift=ease((t-.8)/1.6),breathe=Math.sin(t*2.2)*.008;
 r.rightArm.rotation.set(-1.65-1.28*lift,.1,-.08-.22*lift);r.rightElbow.rotation.x=-1.5+1.34*lift;r.rightHand.rotation.set(.18*lift,0,.16-.32*lift);
 r.leftArm.rotation.set(-.65+.8*lift,0,.17);r.leftElbow.rotation.x=-1.7+.65*lift;r.leftHand.rotation.z=-.12;
 r.body.rotation.x=-.045*lift;r.chest.rotation.y=.28-.18*lift;r.head.rotation.set(.17*(1-lift)-.04*lift,.12-.22*lift,0);r.body.position.y+=breathe;
 if(lift<1){const target=new T.Object3D();target.position.set(.035,.045,0);r.rightHand.add(target);supportHand(r,target,1-ease(lift/.6));target.removeFromParent();}
}
export function emperorCamera(camera:T.PerspectiveCamera,t:number,aspect:number){
 const reveal=ease((t-.75)/2.0),drift=ease((t-2.75)/5.05),eye=new T.Vector3(.24,1.62,1.45).lerp(new T.Vector3(.95,1.35,3.7),reveal).lerp(new T.Vector3(.48,1.31,4.05),drift),target=new T.Vector3(-.05,1.43,.02).lerp(new T.Vector3(0,1.04,0),reveal);
 if(aspect<1.35)eye.z*=1.35/Math.max(.65,aspect);camera.position.copy(eye);camera.up.set(0,1,0);camera.lookAt(target);camera.fov=40;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:t<.8?0:t<2.4?1:2,target};
}
export class EmperorStage {
 group=new T.Group();private fx:EmperorEffects;private light=new T.PointLight(0xff1434,0,5);private sky=new T.PointLight(0x88baff,8,22);private rain:T.InstancedMesh;private ripples:T.InstancedMesh;private object=new T.Object3D();
 constructor(scene:T.Scene){
  scene.add(this.group);this.group.name='emperor-rain-stage';this.fx=new EmperorEffects(this.group as unknown as T.Scene);this.sky.position.set(2.5,6,-4);this.group.add(this.light,this.sky);
  const floor=new T.Mesh(new T.CircleGeometry(17,64),new T.MeshPhongMaterial({color:0x070d17,specular:0x809bba,shininess:115}));floor.rotation.x=-Math.PI/2;floor.position.y=.006;this.group.add(floor);
  const rockGeo=new T.IcosahedronGeometry(1,0),rockMat=new T.MeshPhongMaterial({color:0x182536,specular:0x31465d,shininess:24,flatShading:true});for(let i=0;i<26;i++){const rock=new T.Mesh(rockGeo,rockMat),angle=.5+i*2.39996,radius=4.3+(i%5)*1.2;rock.position.set(Math.sin(angle)*radius,.4+(i%4)*.25,-2-Math.abs(Math.cos(angle))*radius);rock.scale.set(.7+(i%4)*.4,.9+(i%5)*.85,.75+(i%3)*.3);rock.rotation.set(i*.24,i*1.5,i*.16);this.group.add(rock);}
  this.rain=new T.InstancedMesh(new T.BoxGeometry(.009,.3,.009),new T.MeshBasicMaterial({color:0x9dbbdd,transparent:true,opacity:.43,toneMapped:false,depthWrite:false}),180);this.rain.name='emperor-rain';this.rain.frustumCulled=false;
  this.ripples=new T.InstancedMesh(new T.RingGeometry(.065,.071,12),new T.MeshBasicMaterial({color:0x6b92b7,transparent:true,opacity:.25,depthWrite:false,side:T.DoubleSide}),32);this.ripples.frustumCulled=false;this.group.add(this.rain,this.ripples);
 }
 update(t:number,r:CharacterRig,quality:number){
  const strength=ease((t-1)/.9),hand=r.rightHand.getWorldPosition(new T.Vector3());this.light.position.copy(hand);this.light.intensity=strength*2.2;this.sky.intensity=8+Math.sin(t*.7)*.7;
  this.fx.begin();if(strength>.03)this.fx.bolt(hand,t,11,.24+strength*.12);this.fx.end();
  this.rain.count=quality<.5?75:180;for(let i=0;i<this.rain.count;i++){const a=i*2.39996,rad=1.1+(i%29)*.21,y=8-((t*6.4+i*.618)%8);this.object.position.set(Math.cos(a)*rad+y*.1,y,Math.sin(a)*rad-1);this.object.rotation.set(0,0,.12);this.object.scale.setScalar(1);this.object.updateMatrix();this.rain.setMatrixAt(i,this.object.matrix);}this.rain.instanceMatrix.needsUpdate=true;
  this.ripples.count=quality<.5?16:32;for(let i=0;i<this.ripples.count;i++){const a=i*2.4,rad=1+(i%13)*.4,cycle=(t*1.7+i*.37)%1;this.object.position.set(Math.cos(a)*rad,.013,Math.sin(a)*rad);this.object.rotation.set(-Math.PI/2,0,0);this.object.scale.setScalar(.2+cycle*3.4);this.object.updateMatrix();this.ripples.setMatrixAt(i,this.object.matrix);}this.ripples.instanceMatrix.needsUpdate=true;
 }
}
