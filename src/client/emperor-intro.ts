import * as T from 'three';
import {CharacterRig,supportHand} from './character-rig.js';
import {introCurve} from './intro-curves.js';
import {footAt,plantFeet,plantOf,type IntroStep,type Plant} from './intro-footwork.js';
import {EmperorEffects} from './emperor-effects.js';
const ease=(x:number)=>{x=T.MathUtils.clamp(x,0,1);return x*x*(3-2*x);};
/** Wrist at brow -> lift the hand -> hold the challenge. No second salute or stance reset.
 * The fighter faces the camera (+z, chest square to it) on the bladed Mishima stance with world-planted feet: the hips
 * lead the oath (they square up and rise first), the chest, head and free arm follow a beat later, and the rear foot steps
 * up under the squared hips in time with the lift. The springy guard bounce fades into a slow, menacing hold that keeps
 * breathing and shifting its weight between the planted feet. The raised right arm itself is constant from 2.4 s. */
const OATH_YAW=.18,LEAD:Plant={x:.17,z:.08,yaw:-.3},REAR:Plant={x:-.13,z:-.23,yaw:-.62},SQUARE:Plant={x:-.16,z:-.13,yaw:-.36};
const OATH_ROOT={x:0,z:0,yaw:OATH_YAW},OATH_LEFT=plantOf(OATH_ROOT,LEAD),OATH_RIGHT=plantOf(OATH_ROOT,REAR);
const OATH_STEPS:IntroStep[]=[{...plantOf(OATH_ROOT,SQUARE),lift:1.02,land:1.46,h:.05}];
export function poseEmperor(r:CharacterRig,t:number){
 for(const o of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])o.rotation.set(0,0,0);
 r.group.position.set(0,.88,0);r.group.rotation.set(0,OATH_YAW,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=true;r.weapon.rotation.set(0,0,0);r.offWeapon.rotation.set(0,0,0);
 // Kinetic chain: the hips lead the oath, the chest and free arm trail, the head arrives last; the right arm keeps the
 // original lift (it reaches its held pose exactly at 2.4 s).
 const lift=ease((t-.8)/1.6),hips=ease((t-.62)/1.5),chest=ease((t-.74)/1.62),head=ease((t-.9)/1.75),free=ease((t-.86)/1.7);
 const spring=1-ease((t-.55)/1.3),beat=t*9.1,bounce=(Math.sin(beat)+.3*Math.sin(2*beat-.8))*spring,late=Math.sin(beat-.9)*spring;
 // Living hold: slow breathing, a weight shift between the planted feet with a counter-rolled chest, the free arm settling,
 // and the chest swelling with the chin lifting on the 4.1 s beat.
 const hold=ease((t-2.2)/1),breath=Math.sin(2*Math.PI*t/3.1)*(.35+.65*hold),drift=Math.sin(2*Math.PI*(t-2.4)/3.4)*hold,swell=Math.sin(Math.PI*T.MathUtils.clamp((t-3.92)/.62,0,1))**2;
 r.body.position.y=-.07+.011*bounce+.012*hips+.006*breath+.01*swell;r.body.position.x=.02*drift;
 r.body.rotation.set(.04+.01*late-.05*chest,-.3+.18*hips,0);r.pelvis.rotation.set(0,-.12+.07*hips,.018*drift);
 r.spine.rotation.set(.012*late+.008*breath,-.06*chest,Math.sin(t*.9)*.01*spring);
 r.chest.rotation.set(-.008*late-.012*breath-.035*swell,.28-.23*chest,.006*Math.sin(beat*.5)*spring-.022*drift);
 r.neck.rotation.x=.006*breath;r.head.rotation.set(-.01*late+.17*(1-head)-.03*head-.06*swell+.004*Math.sin(2*Math.PI*(t-.2)/3.1),.12-.12*head+.02*Math.sin(2*Math.PI*(t-2.6)/3.4)*hold,-.015*Math.sin(2*Math.PI*(t-2.55)/3.4)*hold);
 r.rightShoulder.rotation.set(-.06*hips,.08*hips,-.09*lift);r.leftShoulder.rotation.z=.01*breath;
 r.rightArm.rotation.set(-1.65-1.28*lift,.1,-.08-.22*lift);r.rightElbow.rotation.x=-1.5+1.34*lift;r.rightHand.rotation.set(.18*lift,0,.16-.32*lift);
 r.leftArm.rotation.set(-.65+.8*free-.03*late,.0,.17+.025*drift);r.leftElbow.rotation.x=-1.7+.65*free-.04*hold*Math.sin(2*Math.PI*(t-2.4)/2.9);r.leftHand.rotation.z=-.12;
 // The free hand starts on the raised wrist and lets go smoothly as the lift gets under way.
 const grip=1-ease((t-.85)/1.05);if(grip>0){const target=new T.Object3D();target.position.set(.035,.045,0);r.rightHand.add(target);supportHand(r,target,grip);target.removeFromParent();}
 plantFeet(r,[footAt(t,OATH_LEFT,[]),footAt(t,OATH_RIGHT,OATH_STEPS)],OATH_ROOT,.3);
}
/** Three shots on one eased, continuous path, all from the front (the fighter faces +z): a close-up of the wrist at the
 * brow, a low pull-back as the hand rises, then a slow low-angle push on the oath. */
const CAM_KEYS=[0,.8,2.4,7.8],CAM_EYE=[[.14,1.6,1.32],[.1,1.52,1.62],[.3,1.0,3.5],[.12,.98,3.4]],CAM_TARGET=[[-.06,1.5,0],[-.06,1.46,0],[0,1.0,0],[0,.98,0]];
const camAt=(keys:number[][],t:number)=>new T.Vector3(...[0,1,2].map(i=>introCurve(CAM_KEYS,keys.map(k=>k[i]),t,{pass:[1,2]})) as [number,number,number]);
export function emperorCamera(camera:T.PerspectiveCamera,t:number,aspect:number){
 const eye=camAt(CAM_EYE,t),target=camAt(CAM_TARGET,t);
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
