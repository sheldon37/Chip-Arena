import * as T from 'three';
import {WEAPON_INTROS,type WeaponIntro} from '../shared/weapon-intros.js';
import {poseCurve} from './pose-curves.js';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import {hand,foot} from './primary-melee-motion.js';
const keys=[0,.5,1.05,1.45,1.8,2.12,2.65,3.2,4.8];
const ease=(x:number)=>T.MathUtils.smoothstep(x,0,1);
/** Authored full-body entrance phrases. Absolute sampling makes previews and late joins identical. */
export function poseWeaponIntro(r:CharacterRig,style:WeaponIntro,age:number){
 const spec=WEAPON_INTROS[style].spec,t=Math.max(0,age),c=(v:number[])=>poseCurve(keys,v,t),magic=['fire','water','lightning','darkness'].includes(spec),gun=['pistol','rifle','machinegun'].includes(spec);
 for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);
 r.group.scale.setScalar(1);r.group.position.set(0,.88,0);r.group.rotation.set(0,0,0);r.body.position.set(0,0,0);r.weapon.visible=true;r.offWeapon.visible=spec==='daggers';r.weapon.position.set(0,0,0);r.weapon.rotation.set(0,0,0);r.offWeapon.position.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);
 const turn=spec==='daggers'?c([-.3,-1.2,-2.8,-4.1,-5.3,-6.1,-6.5,-6.5,-6.5]):spec==='sword'?c([-.9,-.8,-.7,-.95,.55,.8,.15,-.25,-.25]):spec==='shield'?c([.5,.25,.1,.05,-.15,-.4,-.25,-.25,-.25]):spec==='greatsword'?c([1.1,.85,.35,-.3,.5,.65,.25,-.25,-.25]):gun?c([-.7,-.4,-.25,-.1,.2,.1,-.1,-.2,-.2]):c([.5,.25,-.15,-.4,.35,.15,-.2,-.2,-.2]);
 r.group.rotation.y=turn;r.group.position.z=c(spec==='sword'?[-.9,-.65,-.4,-.35,.2,.25,.1,0,0]:spec==='greatsword'?[-.8,-.5,-.15,0,.2,.25,.1,0,0]:spec==='lightning'?[-1,-1,-.7,0,0,0,0,0,0]:[-.2,-.1,0,0,.1,.1,0,0,0]);
 const crouch=c(spec==='shield'?[-.34,-.38,-.2,-.3,-.18,-.05,-.03,-.03,-.03]:spec==='daggers'?[-.28,-.2,.02,.14,-.17,-.32,-.12,-.05,-.05]:spec==='greatsword'?[-.09,-.12,-.18,.08,-.3,-.18,-.03,-.03,-.03]:spec==='rifle'?[-.25,-.30,-.34,-.25,-.08,-.03,-.03,-.03,-.03]:spec==='lightning'?[1.8,1.5,.7,-.32,-.20,-.05,0,-.03,-.03]:[-.03,-.04,-.10,-.15,-.13,-.06,-.03,-.03,-.03]);r.body.position.y=crouch;
 const torque=c([-.05,-.18,-.30,-.32,.35,.22,.08,-.05,-.05]);r.pelvis.rotation.y=torque*.4;r.spine.rotation.y=torque*.6;r.chest.rotation.y=torque*.45;r.head.rotation.y=-torque*.7;r.body.rotation.x=c([.08,.10,.15,-.13,.35,.2,.04,0,0]);r.head.rotation.x=-r.body.rotation.x*.5;r.spine.rotation.x=Math.sin(t*2.2)*.008;
 const step=c([.05,.08,.17,.23,.29,.26,.17,.14,.14]);foot(r,true,new T.Vector3(.19,-.795,step),.1,0,1);foot(r,false,new T.Vector3(-.19,-.795,-step),-.25,.12,1);
 const h=(left:boolean,x:number,y:number,z:number,rx=0,ry=0,rz=0)=>hand(r,left,new T.Vector3(x,y,z),new T.Euler(rx,ry,rz),1);
 if(spec==='greatsword'){
  braceWeapon(r,new T.Vector3(c([-.28,-.30,-.12,-.10,-.06,.0,-.18,-.22,-.22]),c([-.30,-.28,.04,.5,-.23,-.04,.32,.3,.3]),c([-.05,.0,.2,.02,.43,.32,.05,.1,.1])),new T.Euler(c([.85,.7,-.1,-2,.8,.1,-.25,-.28,-.28]),c([.2,.25,-.25,-.3,.45,.7,2.6,2.8,2.8]),-.1),1);
 }else if(spec==='sword'){
  h(false,c([.10,.12,.14,.16,-.27,-.42,-.30,-.24,-.24]),c([-.21,-.21,-.20,-.16,.13,.18,.17,.1,.1]),c([.12,.12,.12,.15,.42,.28,.2,.28,.28]),c([0,0,-.1,-.2,.15,.25,-.3,-.65,-.65]),c([-1.4,-1.4,-1.4,-1.6,.8,1.5,.3,0,0]));h(true,.26,c([-.15,-.15,-.12,-.1,.04,.1,.02,-.04,-.04]),.16,0,0,.3);
 }else if(spec==='daggers'){
  const twirl=c([0,0,Math.PI,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2]);
  for(const left of [true,false]){const sign=left?1:-1;h(left,sign*c([.22,.32,.45,.43,.24,.10,.08,.12,.12]),c([-.12,.08,.28,.42,.28,.12,.2,.16,.16]),c([-.12,-.02,.05,.15,.25,.33,.25,.28,.28]),twirl+c([.2,-.3,-.9,-1.2,-.6,0,-.2,-.2,-.2]),sign*c([.4,.8,1.1,.8,.1,-.65,-.65,-.65,-.65]),sign*.18);}
 }else if(spec==='shield'){
  h(false,-.22,c([-.27,-.28,.10,-.15,.06,.20,.15,.15,.15]),c([.32,.34,.18,.47,.42,.30,.27,.30,.30]),c([.15,.2,.1,.02,-.05,-.1,-.08,-.08,-.08]),c([.15,.1,-.3,-.1,.2,.4,.25,.25,.25]));h(true,.37,c([-.18,-.22,-.05,.05,.12,.13,.1,.05,.05]),.02,-.5,0,.35);
 }else if(gun){
  if(spec==='pistol'){
   h(false,c([-.28,-.30,-.18,-.14,-.18,-.25,-.24,-.24,-.24]),c([-.1,.1,.4,.38,.15,.1,.12,.12,.12]),c([.12,.18,.12,.1,.25,.43,.34,.34,.34]),c([.3,-.35,-.9,-.7,-.15,0,-.1,-.1,-.1]),0,c([0,0,Math.PI,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2]));h(true,.25,-.12,.08,-.2,0,.15);
  }else{
   const heavy=spec==='machinegun';braceWeapon(r,new T.Vector3(c([-.15,-.14,-.10,-.1,-.03,-.02,-.04,-.06,-.06]),c(heavy?[.37,.38,.24,.18,.14,.12,.14,.15,.15]:[-.08,-.06,.12,.14,.14,.13,.14,.15,.15]),c([.08,.12,.22,.24,.28,.27,.25,.25,.25])),new T.Euler(c(heavy?[-1,-.9,-.5,-.25,-.3,-.4,-.15,-.12,-.12]:[.2,.12,.04,-.05,-.1,-.15,-.10,-.10,-.10]),.1,0),1);
   if(spec==='rifle'&&t<1.8)h(true,c([.3,.28,-.05,-.12,.1,.1,.1,.1,.1]),.07,c([.16,.2,.04,-.08,.12,.12,.12,.12,.12]),-.3,0,.2);
  }
 }else if(magic){
  h(false,-.42,c([.05,.12,.3,.45,.15,.13,.15,.14,.14]),.12,-Math.PI/2,.12,0);
  const y=spec==='fire'?c([-.1,.02,.15,.35,.5,.35,.24,.2,.2]):spec==='water'?c([-.1,.12,.34,.25,.02,.25,.3,.22,.22]):spec==='lightning'?c([.1,.35,.5,-.24,.06,.2,.32,.25,.25]):c([-.3,-.13,.08,.3,.38,.30,.20,.17,.17]);
  h(true,c([.2,.3,.39,.4,.3,.28,.28,.27,.27]),y,c([.10,.14,.18,.18,.28,.30,.25,.25,.25]),-Math.PI/2,0,.3);r.leftHand.rotation.y+=Math.sin(t*1.8)*.1;
 }
 const flash=r.weapon.getObjectByName('muzzle-flash');if(flash)flash.visible=spec==='machinegun'&&t>1.93&&t<2.33&&Math.floor((t-1.93)*24)%2===0;
 if(t>3.2)r.body.position.y+=Math.sin((t-3.2)*2.4)*.007;
}
export function weaponIntroCamera(camera:T.PerspectiveCamera,style:WeaponIntro,age:number,root:T.Vector3,aspect:number){
 const spec=WEAPON_INTROS[style].spec,hit=WEAPON_INTROS[style].hit,close=age<1.05,impact=age>=hit-.15&&age<hit+.5,hero=age>=3.2,u=ease((age-(hero?3.2:close?0:1.05))/(hero?1.6:close?1.05:2.15));
 const side=spec==='sword'||spec==='rifle'?-1:1,eye=close?new T.Vector3(side*.9,.35,2.15):hero?new T.Vector3(side*(1.15-u*.45),.2,3.9):new T.Vector3(side*(2.3-u*.65),impact?-.10:.5,4.4),target=root.clone().add(new T.Vector3(0,close?.25:.1,0));
 if(aspect<1.35)eye.multiplyScalar(1.35/Math.max(.65,aspect));camera.position.copy(root).add(eye);camera.up.set(0,1,0);camera.lookAt(target);camera.fov=close?42:hero?40:48;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot:hero?3:impact?2:close?0:1,target};
}
