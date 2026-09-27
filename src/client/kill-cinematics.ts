import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import {createCharacterRig} from './character-rig.js';
import {buildWeapon} from './weapon-visuals.js';
import {createClownRig} from './clown-rig.js';
import {dressRig} from './cosmetics.js';
/** Cosmetic only. Timelines use authoritative death age, so observers stay in sync. */
export const cinematic=(kind:string):KillEffectFactory=>(scene,_original,p,d)=>{
 const root=new T.Group();scene.add(root);root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;
 const victim=createCharacterRig(p.color,p.cosmetics?.bodyColor);dressRig(victim,p.cosmetics);root.add(victim.group);buildWeapon(victim.weapon,p.spec);if(p.spec==='daggers')buildWeapon(victim.offWeapon,p.spec,true);
 const dropped=new T.Group();buildWeapon(dropped,p.spec);root.add(dropped);dropped.visible=false;dropped.position.set(-.5,.1,.15);
 const props:T.Object3D[]=[];let clown:ReturnType<typeof createCharacterRig>|undefined;
 if(kind==='dev-clown'){clown=createClownRig();root.add(clown.group);}
 if(kind==='confetti')for(let n=0;n<65;n++){const m=new T.Mesh(new T.BoxGeometry(.05,.12,.025),new T.MeshBasicMaterial({color:new T.Color().setHSL(n/65,.85,.6)}));root.add(m);props.push(m);}
 if(kind==='ascension'){const beam=new T.Mesh(new T.CylinderGeometry(.8,.8,10,20,1,true),new T.MeshBasicMaterial({color:0x95ddff,transparent:true,opacity:.22,side:T.DoubleSide,depthWrite:false}));beam.position.y=4;root.add(beam);props.push(beam);}
 if(kind==='ghost')victim.group.traverse(o=>{if(o instanceof T.Mesh){o.material=new T.MeshBasicMaterial({color:0xc7e4ff,transparent:true,opacity:.6});}});
 return {count:kind==='confetti'?65:1,update(age){
  if(kind==='dev-clown'&&clown){const fear=Math.max(0,Math.min(1,(age-.25)*2));victim.weapon.visible=age<.3;victim.offWeapon.visible=false;dropped.visible=age>=.3;dropped.position.y=Math.max(-.78,.1-(age-.3)**2*5);dropped.rotation.set(Math.min(Math.PI/2,(age-.3)*5),0,Math.min(.3,age*.2));victim.head.rotation.y=Math.sin(age*8)*.8*fear;victim.spine.rotation.x=-.16*fear;victim.leftArm.rotation.z=-.25*fear;victim.rightArm.rotation.z=.25*fear;victim.leftElbow.rotation.x=-1.1*fear;victim.rightElbow.rotation.x=-1.1*fear;victim.body.position.y=Math.sin(age*25)*.015*fear;
   const approach=age<1?-18:age<2.25?-18+(age-1)*14.2:Math.max(-.25,(age-2.25)*13),z=age<2? -2.8:Math.min(0,-2.8+(age-2)*11.2);clown.group.position.set(approach,0,z);clown.group.rotation.y=Math.PI/2;const stride=age*30;clown.leftLeg.rotation.x=Math.sin(stride)*1;clown.rightLeg.rotation.x=-Math.sin(stride);clown.leftKnee.rotation.x=Math.max(0,Math.sin(stride))*1.2;clown.rightKnee.rotation.x=Math.max(0,-Math.sin(stride))*1.2;clown.leftArm.rotation.x=age>2.1?-1.2:Math.sin(stride)*1.1;clown.rightArm.rotation.x=age>2.1?-1.3:-Math.sin(stride)*1.1;clown.body.position.y=Math.abs(Math.sin(stride))*.11;
   if(age>2.25){victim.group.position.set(approach+.45,.2,0);victim.group.rotation.z=-Math.PI/2;victim.leftLeg.rotation.x=Math.sin(stride)*.8;victim.rightLeg.rotation.x=-Math.sin(stride)*.8;victim.leftArm.rotation.x=-1.8;victim.rightArm.rotation.x=-1.6;victim.head.rotation.y=Math.sin(stride)*.25;}
  }else if(kind==='launch'){victim.group.position.y=age*age*4;victim.group.position.x=age*age*1.5;victim.group.rotation.z=age*5;victim.leftArm.rotation.z=-1.4;victim.rightArm.rotation.z=1.4;}
  else if(kind==='shrink'){const t=Math.max(0,1-age*.7);victim.group.scale.set(t,t*(1+.3*Math.sin(age*13)),t);victim.group.position.y=-.8*(1-t);victim.group.rotation.y=age*8;}
  else if(kind==='ascension'){victim.group.position.y=Math.max(0,age-.6)**2*1.8;victim.leftArm.rotation.z=-.7;victim.rightArm.rotation.z=.7;root.rotation.y=d.yaw+age*.8;}
  else if(kind==='ghost'){victim.group.position.y=age*.85;victim.group.scale.set(1,1+age*.12,1);victim.group.position.x=Math.sin(age*3)*.3;victim.leftArm.rotation.z=-1;victim.rightArm.rotation.z=1;victim.group.traverse(o=>{if(o instanceof T.Mesh)(o.material as T.MeshBasicMaterial).opacity=Math.max(0,.6-age*.12);});}
  else if(kind==='confetti'){victim.group.scale.setScalar(Math.max(0,1-age*4));props.forEach((m,i)=>{const a=i*2.399,r=(1+i%5*.35)*age;m.position.set(Math.sin(a)*r,Math.max(-.8,age*(3+i%7*.4)-age*age*2),Math.cos(a)*r);m.rotation.set(age*(i%4+1),age*3,age*4);m.scale.setScalar(Math.min(1,Math.max(0,5-age)));});}
 },dispose(){scene.remove(root);root.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry'){o.geometry.dispose();}});for(const o of props)if(o instanceof T.Mesh){o.geometry.dispose();(o.material as T.Material).dispose();}}};
};
