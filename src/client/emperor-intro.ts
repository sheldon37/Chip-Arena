import * as T from 'three';
import {CharacterRig,supportHand} from './character-rig.js';
import {emperorGuard} from './emperor-animation.js';
import {EmperorEffects} from './emperor-effects.js';
const ease=(x:number)=>{x=T.MathUtils.clamp(x,0,1);return x*x*(3-2*x);};
export function poseEmperor(r:CharacterRig,t:number){
 for(const o of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])o.rotation.set(0,0,0);
 r.group.position.set(0,.88,0);r.group.rotation.set(0,-.1,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=true;r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);emperorGuard(r,t);
 if(t<3.25){const grip=ease(t/.7);r.rightArm.rotation.set(-.9,.15,-.08);r.rightElbow.rotation.x=-1.9;r.rightHand.rotation.set(0,0,.16);r.head.rotation.x=.1;
  const target=new T.Object3D();target.position.set(.035,.045,0);r.rightHand.add(target);supportHand(r,target,grip);target.removeFromParent();
 }else{const lift=ease((t-3.25)/.8),hold=1-ease((t-5.3)/.65),power=lift*hold;r.rightArm.rotation.x=-.55-2.42*power;r.rightElbow.rotation.x=-1.95+1.45*power;r.leftArm.rotation.set(-.45,0,.3);r.leftElbow.rotation.x=-1.4;r.body.rotation.x=-.06*power;r.chest.rotation.y=.28-.3*power;r.head.rotation.x=-.18*power;r.body.position.y+=.025*power;}
}
export function emperorCamera(camera:T.PerspectiveCamera,t:number,aspect:number){
 let eye=new T.Vector3(.6,1.6,2.0),target=new T.Vector3(-.08,1.4,.12);let shot=0;
 if(t<1.9){eye.lerp(new T.Vector3(-.25,1.55,1.85),ease(t/1.9));}else if(t<3.25){eye.set(-.52,1.48,1.65);target.set(-.08,1.42,.1);shot=1;}
 else if(t<5.4){const u=ease((t-3.25)/2.15),a=-.65+u*.95;eye.set(Math.sin(a)*3.9,.7+u*.3,Math.cos(a)*3.9);target.set(0,1.2,0);shot=2;}
 else{eye.set(.95,1.05,4.2).lerp(new T.Vector3(.65,1.15,4.0),ease((t-5.4)/2.4));target.set(0,.98,0);shot=3;}
 if(aspect<1.35)eye.z*=1.35/Math.max(.65,aspect);camera.position.copy(eye);camera.up.set(0,1,0);camera.lookAt(target);camera.fov=40;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot,target};
}
export class EmperorStage {
 group=new T.Group();private fx:EmperorEffects;private light=new T.PointLight(0xff163c,0,7);private stones:T.InstancedMesh;private object=new T.Object3D();
 constructor(scene:T.Scene){scene.add(this.group);this.fx=new EmperorEffects(this.group as unknown as T.Scene);this.group.add(this.light);this.stones=new T.InstancedMesh(new T.BoxGeometry(.12,.09,.15),new T.MeshLambertMaterial({color:0x33333f}),24);this.stones.frustumCulled=false;this.group.add(this.stones);}
 update(t:number,r:CharacterRig,quality:number){const strength=ease((t-.6)/1.5)*(1-.8*ease((t-5.35)/.7)),hand=r.rightHand.getWorldPosition(new T.Vector3());this.light.position.copy(hand);this.light.intensity=strength*3;this.fx.begin();if(strength>.03){this.fx.bolt(hand,t,11,.4+strength*.2);if(t>3.9&&t<5.35){for(let i=0;i<(quality<.5?2:5);i++)this.fx.bolt(new T.Vector3(Math.sin(i*2.4)*1.8,.3+i*.4,Math.cos(i*2.4)*1.8),t,i,1.4);}}this.fx.end();this.stones.count=quality<.5?12:24;for(let i=0;i<this.stones.count;i++){const a=i*2.4,rad=1.25+(i%5)*.23;this.object.position.set(Math.cos(a)*rad,.05+strength*(.1+Math.sin(t*2+i)**2*.6),Math.sin(a)*rad);this.object.rotation.set(t*.7+i,t*.5,0);this.object.scale.setScalar(.6+(i%4)*.2);this.object.updateMatrix();this.stones.setMatrixAt(i,this.object.matrix);}this.stones.instanceMatrix.needsUpdate=true;}
}
