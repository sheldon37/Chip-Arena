import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import {createCharacterRig,RIG_LENGTHS,type CharacterRig} from './character-rig.js';
import {createClownRig} from './clown-rig.js';
import {dressRig} from './cosmetics.js';
import {buildWeapon} from './weapon-visuals.js';
import {CLOWN_LOVE,loveDip} from './clown-love-timing.js';

const clamp=(n:number)=>T.MathUtils.clamp(n,0,1);
const smooth=(n:number)=>{const t=clamp(n);return t*t*(3-2*t);};
const down=new T.Vector3(0,-1,0);
/** Two arms meet shared dance-hand targets; the chest and hips stay separated. */
function danceHand(r:CharacterRig,side:'left'|'right',target:T.Vector3){
 const arm=side==='left'?r.leftArm:r.rightArm,elbow=side==='left'?r.leftElbow:r.rightElbow,hand=side==='left'?r.leftHand:r.rightHand;
 r.group.updateMatrixWorld(true);
 const start=arm.position.clone(),delta=arm.parent!.worldToLocal(target.clone()).sub(start),distance=T.MathUtils.clamp(delta.length(),.05,.575),dir=delta.normalize();
 const a=RIG_LENGTHS.upperArm,b=RIG_LENGTHS.forearm+.06,along=(a*a-b*b+distance*distance)/(2*distance),height=Math.sqrt(Math.max(0,a*a-along*along));
 const pole=new T.Vector3(side==='left'?1:-1,-1,-.2);pole.addScaledVector(dir,-pole.dot(dir)).normalize();
 const bend=start.clone().addScaledVector(dir,along).addScaledVector(pole,height),end=start.clone().addScaledVector(dir,distance);
 arm.quaternion.setFromUnitVectors(down,bend.clone().sub(start).normalize());
 elbow.quaternion.setFromUnitVectors(down,end.sub(bend).normalize().applyQuaternion(arm.quaternion.clone().invert()));hand.rotation.set(0,0,0);
}
function resetPose(r:CharacterRig){
 for(const joint of [r.group,r.body,r.spine,r.head,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee])joint.rotation.set(0,0,0);
 r.body.position.set(0,0,0);
}
function run(r:CharacterRig,t:number){
 const stride=Math.sin(t*27);r.leftLeg.rotation.x=stride*.85;r.rightLeg.rotation.x=-stride*.85;
 r.leftKnee.rotation.x=Math.max(0,stride)*1.1;r.rightKnee.rotation.x=Math.max(0,-stride)*1.1;
 r.leftArm.rotation.x=-stride;r.rightArm.rotation.x=stride;r.body.position.y=Math.abs(stride)*.085;
}
function heartGeometry(){
 const shape=new T.Shape();shape.moveTo(0,-.5);shape.bezierCurveTo(-.9,.12,-.65,.85,0,.4);shape.bezierCurveTo(.65,.85,.9,.12,0,-.5);
 return new T.ShapeGeometry(shape,10);
}
/** Small geometry label works in WebGL and the existing SVG compatibility renderer. */
function oneUpGeometry(){
 const glyphs=['010/110/010/010/111','101/101/101/101/111','110/101/110/100/100'];
 const vertices:number[]=[];
 glyphs.forEach((g,c)=>g.split('/').forEach((row,y)=>[...row].forEach((pixel,x)=>{if(pixel!=='1')return;const a=(c*4+x-5.5)*.075,b=(2-y)*.075,s=.067;vertices.push(a,b,0,a+s,b,0,a+s,b+s,0,a,b,0,a+s,b+s,0,a,b+s,0);}))); 
 const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(vertices,3));geometry.computeVertexNormals();return geometry;
}

/** Five-second cosmetic timeline: run-in, hand-held dance, scoop, getaway. */
export const clownLove:KillEffectFactory=(scene,_original,p,d,quality)=>{
 const root=new T.Group();root.name='clown-love';root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;scene.add(root);
 const victim=createCharacterRig(p.color,p.cosmetics?.bodyColor),clown=createClownRig();victim.group.name='love-partner';clown.group.name='love-clown';clown.group.scale.setScalar(1.1);
 dressRig(victim,p.cosmetics);root.add(victim.group,clown.group);buildWeapon(victim.weapon,p.spec);if(p.spec==='daggers')buildWeapon(victim.offWeapon,p.spec,true);
 const dropped=new T.Group();dropped.name='love-dropped-weapon';buildWeapon(dropped,p.spec);if(p.spec==='daggers'){const second=new T.Group();buildWeapon(second,p.spec,true);second.position.x=.2;dropped.add(second);}root.add(dropped);
 const hearts=heartGeometry(),text=oneUpGeometry(),pink=new T.MeshBasicMaterial({color:0xff559b,side:T.DoubleSide,transparent:true,depthWrite:false}),mint=new T.MeshBasicMaterial({color:0xa3ff93,side:T.DoubleSide,transparent:true,depthWrite:false});
 const pops=CLOWN_LOVE.beats.map((at,i)=>{const group=new T.Group();group.name='love-pop-'+i;const label=new T.Mesh(text,mint);label.position.y=.27;group.add(label);const count=quality<.5?1:3;for(let n=0;n<count;n++){const heart=new T.Mesh(hearts,pink);heart.scale.setScalar(.21+n*.025);heart.position.set((n-(count-1)/2)*.4,-.1-Math.abs(n-1)*.15,.01);group.add(heart);}root.add(group);
  for(const mesh of group.children as T.Mesh[])mesh.onBeforeRender=(_renderer,_scene,camera)=>{mesh.quaternion.copy(group.getWorldQuaternion(new T.Quaternion()).invert().multiply(camera.getWorldQuaternion(new T.Quaternion())));mesh.updateMatrixWorld(true);};
  return {group,at};});
 return {count:quality<.5?12:24,update(age){
  const t=Math.max(0,age);resetPose(victim);resetPose(clown);victim.group.position.set(0,0,0);clown.group.position.set(-8,.09,-.6);
  victim.weapon.visible=t<.25;victim.offWeapon.visible=t<.25&&p.spec==='daggers';dropped.visible=t>=.25;
  dropped.position.set(.3,Math.max(-.77,.05-(t-.25)**2*5),.35);dropped.rotation.set(Math.min(Math.PI/2,Math.max(0,t-.25)*7),.3,.3);
  const entrance=smooth((t-.15)/.85),takeHands=smooth((t-.8)/.25);
  clown.group.position.set(T.MathUtils.lerp(-8,-.48,entrance),.09,T.MathUtils.lerp(-.6,0,entrance));clown.group.rotation.y=Math.PI/2;run(clown,t);
  victim.group.position.x=.48*takeHands;victim.group.rotation.y=-Math.PI/2*takeHands;victim.head.rotation.y=Math.sin(t*12)*.3*(1-takeHands);
  if(t>=CLOWN_LOVE.danceStart){
   const carry=smooth((t-CLOWN_LOVE.carryStart)/.35),dip=loveDip(t),step=Math.sin((t-CLOWN_LOVE.danceStart)*Math.PI*5),direction=Math.floor(Math.max(0,t-1.1)/.4)%2?1:-1;
   clown.group.position.set(-.48,.09,step*.09);victim.group.position.set(.48,0,step*.09);
   clown.group.rotation.y=Math.PI/2;victim.group.rotation.y=-Math.PI/2;
   for(const rig of [clown,victim]){resetPose(rig);rig.leftLeg.rotation.x=-.23*dip;rig.rightLeg.rotation.x=-.23*dip;rig.leftKnee.rotation.x=.46*dip;rig.rightKnee.rotation.x=.46*dip;rig.body.position.y=-.055*dip;rig.head.rotation.z=-direction*dip*.12;}
   clown.group.rotation.y=Math.PI/2;victim.group.rotation.y=-Math.PI/2;
   victim.spine.rotation.z=direction*.58*dip;clown.spine.rotation.z=-direction*.24*dip;
   root.updateMatrixWorld(true);
   for(const side of ['left','right'] as const){const z=side==='left'?-.31:.31,target=root.localToWorld(new T.Vector3(0,.14-dip*.04,z+step*.09));danceHand(victim,side,target);danceHand(clown,side==='left'?'right':'left',target);}
   if(t>=CLOWN_LOVE.carryStart){
    // Smoothly lift into a sideways bridal carry, then run away together.
    const travel=Math.max(0,t-4)**2*11;
    clown.group.position.set(-.48+travel,.09,0);clown.group.rotation.y=T.MathUtils.lerp(Math.PI/2,Math.PI*.35,carry);run(clown,t);
    clown.leftArm.rotation.set(-1.2,0,-.2);clown.rightArm.rotation.set(-1.2,0,.2);clown.leftElbow.rotation.x=clown.rightElbow.rotation.x=-.75;
    victim.group.position.set(T.MathUtils.lerp(.48,-.28,carry)+travel,carry*.37,.39*carry);
    victim.group.rotation.set(0,-Math.PI/2*(1-carry),-Math.PI/2*carry);victim.spine.rotation.z=0;
    victim.leftLeg.rotation.x=-.28*carry;victim.rightLeg.rotation.x=.24*carry;victim.leftKnee.rotation.x=.5*carry;victim.rightKnee.rotation.x=.45*carry;
    victim.leftArm.rotation.set(-1.2*carry,0,-.2);victim.rightArm.rotation.set(-1.1*carry,0,.2);victim.leftElbow.rotation.x=victim.rightElbow.rotation.x=-.65*carry;
   }
  }
  for(const {group,at}of pops){const a=t-at;group.visible=a>=0&&a<.72;if(group.visible){group.position.set(Math.sin(at*5)*.22,1.15+a*1.7,.05);group.scale.setScalar(Math.min(1,.5+a*7)*Math.min(1,(.72-a)*5));group.rotation.y=-d.yaw;}}
 },dispose(){scene.remove(root);hearts.dispose();text.dispose();pink.dispose();mint.dispose();}};
};
