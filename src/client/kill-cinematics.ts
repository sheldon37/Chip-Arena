import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import type {CharacterRig} from './character-rig.js';
import {buildWeapon} from './weapon-visuals.js';
import {createClownRig} from './clown-rig.js';
import {dressRig} from './cosmetics.js';
import {cinematicVictim,DeathPoseCarry} from './cinematic-victim.js';
const sat=(x:number)=>Math.min(1,Math.max(0,x)),sm=(x:number)=>{x=sat(x);return x*x*(3-2*x);},bump=(x:number)=>x<=0||x>=1?0:Math.sin(Math.PI*x);
/** Loose limbs: staggered sines whose amplitude eases in, so a body in flight never holds a rigid T-pose. */
function flail(r:CharacterRig,age:number,amount:number,spread:number){
 const a=Math.sin(age*11),b=Math.sin(age*11+1.3),c=Math.sin(age*8+.6),e=Math.sin(age*8+2.2);
 r.leftArm.rotation.set(-.3+a*.45*amount,0,spread+b*.35*amount);r.rightArm.rotation.set(-.3+b*.45*amount,0,-spread-a*.35*amount);r.leftElbow.rotation.x=-.5+c*.35*amount;r.rightElbow.rotation.x=-.5+e*.35*amount;
 r.leftLeg.rotation.set(-.35+c*.5*amount,0,.12);r.rightLeg.rotation.set(-.1+e*.5*amount,0,-.12);r.leftKnee.rotation.x=.6+Math.max(0,e)*.5*amount;r.rightKnee.rotation.x=.4+Math.max(0,c)*.5*amount;r.head.rotation.x=-.2+a*.12*amount;
}
/** Cosmetic only. Timelines use authoritative death age, so observers stay in sync. The victim is the live model (armor
 * glow, team bands) and starts from the pose the fighter died in. Every update is a pure function of age. */
export const cinematic=(kind:string):KillEffectFactory=>(scene,original,p,d,_quality,_budget,state)=>{
 const root=new T.Group();scene.add(root);root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;
 const victim=cinematicVictim(p,state);dressRig(victim,p.cosmetics);root.add(victim.group);buildWeapon(victim.weapon,p.spec);if(p.spec==='daggers')buildWeapon(victim.offWeapon,p.spec,true);
 const carry=new DeathPoseCarry(victim,original);
 const dropped=new T.Group();buildWeapon(dropped,p.spec);root.add(dropped);dropped.visible=false;dropped.position.set(-.5,.1,.15);
 const props:T.Object3D[]=[],owned:T.Material[]=[];let clown:ReturnType<typeof createClownRig>|undefined,beam:T.Mesh<T.CylinderGeometry,T.MeshBasicMaterial>|undefined,ghost:T.MeshLambertMaterial|undefined;
 if(kind==='dev-clown'){clown=createClownRig();root.add(clown.group);}
 if(kind==='confetti')for(let n=0;n<65;n++){const m=new T.Mesh(new T.BoxGeometry(.05,.12,.025),new T.MeshBasicMaterial({color:new T.Color().setHSL(n/65,.85,.6)}));root.add(m);props.push(m);}
 if(kind==='ascension'){beam=new T.Mesh(new T.CylinderGeometry(.8,.8,10,20,1,true),new T.MeshBasicMaterial({color:0x95ddff,transparent:true,opacity:0,side:T.DoubleSide,depthWrite:false}));beam.position.y=4;root.add(beam);props.push(beam);}
 if(kind==='ghost'){ghost=new T.MeshLambertMaterial({color:0xc7e4ff,emissive:0x5c7fa8,transparent:true,opacity:.6,depthWrite:false});owned.push(ghost);victim.group.traverse(o=>{if(o instanceof T.Mesh)o.material=ghost!;});}
 return {count:kind==='confetti'?65:1,update(age){
  carry.reset();victim.group.position.set(0,0,0);victim.group.rotation.set(0,0,0);victim.group.scale.setScalar(1);
  if(kind==='dev-clown'&&clown){const fear=Math.max(0,Math.min(1,(age-.25)*2));victim.weapon.visible=age<.3;victim.offWeapon.visible=false;dropped.visible=age>=.3;dropped.position.y=Math.max(-.78,.1-(age-.3)**2*5);dropped.rotation.set(Math.min(Math.PI/2,(age-.3)*5),0,Math.min(.3,age*.2));victim.head.rotation.y=Math.sin(age*8)*.8*fear;victim.spine.rotation.x=-.16*fear;victim.leftArm.rotation.z=-.25*fear;victim.rightArm.rotation.z=.25*fear;victim.leftElbow.rotation.x=-1.1*fear;victim.rightElbow.rotation.x=-1.1*fear;victim.body.position.y=Math.sin(age*25)*.015*fear;
   const approach=age<1?-18:age<2.25?-18+(age-1)*14.2:Math.max(-.25,(age-2.25)*13),z=age<2? -2.8:Math.min(0,-2.8+(age-2)*11.2);clown.group.position.set(approach,0,z);clown.group.rotation.y=Math.PI/2;const stride=age*30;clown.leftLeg.rotation.x=Math.sin(stride)*1;clown.rightLeg.rotation.x=-Math.sin(stride);clown.leftKnee.rotation.x=Math.max(0,Math.sin(stride))*1.2;clown.rightKnee.rotation.x=Math.max(0,-Math.sin(stride))*1.2;clown.leftArm.rotation.x=age>2.1?-1.2:Math.sin(stride)*1.1;clown.rightArm.rotation.x=age>2.1?-1.3:-Math.sin(stride)*1.1;clown.body.position.y=Math.abs(Math.sin(stride))*.11;
   if(age>2.25){victim.group.position.set(approach+.45,.2,0);victim.group.rotation.z=-Math.PI/2;victim.leftLeg.rotation.x=Math.sin(stride)*.8;victim.rightLeg.rotation.x=-Math.sin(stride)*.8;victim.leftArm.rotation.x=-1.8;victim.rightArm.rotation.x=-1.6;victim.head.rotation.y=Math.sin(stride)*.25;}
  }else if(kind==='launch'){
   // Same flight as before (distance unchanged); a 120 ms impact squash first, then an accelerating spin with loose limbs.
   const hit=bump(age/.12);victim.group.position.y=age*age*4;victim.group.position.x=age*age*1.5;victim.group.rotation.z=5*Math.max(0,age-.06)**1.15-.25*hit;victim.group.scale.set(1+.08*hit,1-.14*hit,1+.08*hit);
   flail(victim,age,sm(age/.25),1.1+.3*sm(age/.4));victim.spine.rotation.x=.25*hit;
  }else if(kind==='shrink'){
   // A quick 1.12 pop before the implode; the spin winds up instead of starting at full speed.
   const pop=1+.12*bump(age/.12),t=Math.max(0,1-Math.max(0,age-.06)*.7)*pop;victim.group.scale.set(t,t*(1+.3*Math.sin(age*13)*sm(age/.2)),t);victim.group.position.y=-.8*(1-Math.min(1,t));victim.group.rotation.y=8*age*sm(age/.35);
   victim.leftArm.rotation.z=.4*sm(age/.2);victim.rightArm.rotation.z=-.4*sm(age/.2);victim.head.rotation.x=-.3*sm(age/.3);
  }else if(kind==='ascension'){
   const lift=Math.max(0,age-.6);victim.group.position.y=lift**2*1.8;root.rotation.y=d.yaw+age*.8*sm(age/.8);
   // Arms rise from the sides, the head tips back, legs dangle with a lagging pendulum.
   const raise=sm((age-.2)/.9),sway=Math.sin(age*3.1)*.12*sm((age-.6)/.6);victim.leftArm.rotation.set(-.15*raise,0,.2+.75*raise);victim.rightArm.rotation.set(-.15*raise,0,-.2-.75*raise);victim.leftElbow.rotation.x=victim.rightElbow.rotation.x=-.25*raise;
   victim.head.rotation.x=-.45*sm((age-.3)/.8);victim.spine.rotation.x=-.12*raise;victim.leftLeg.rotation.x=sway;victim.rightLeg.rotation.x=Math.sin(age*3.1-.7)*.12*sm((age-.6)/.6);victim.leftKnee.rotation.x=.15+sway*.5;victim.rightKnee.rotation.x=.2;
   if(beam)beam.material.opacity=.22*sm(age/.3)*(1-sm((age-2.2)/.8));
  }else if(kind==='ghost'){
   victim.group.position.y=age*.85;victim.group.scale.set(1,1+age*.12,1);victim.group.position.x=Math.sin(age*3)*.3;
   // Arms drift in slow waves, the far joints lagging the near ones.
   const drift=sm(age/.4);victim.leftArm.rotation.set(Math.sin(age*2.2)*.25*drift,0,.25+.75*drift+Math.sin(age*2.2-.6)*.15);victim.rightArm.rotation.set(Math.sin(age*2.2+1)*.25*drift,0,-.25-.75*drift-Math.sin(age*2.2+.4)*.15);
   victim.leftElbow.rotation.x=-.3+Math.sin(age*2.2-1.1)*.25*drift;victim.rightElbow.rotation.x=-.3+Math.sin(age*2.2-.1)*.25*drift;victim.leftLeg.rotation.x=Math.sin(age*1.7)*.15*drift;victim.rightLeg.rotation.x=Math.sin(age*1.7+1.4)*.15*drift;victim.head.rotation.x=.15*drift;
   if(ghost)ghost.opacity=Math.max(0,.6-age*.12);
  }else if(kind==='confetti'){const pop=1+.15*bump(age/.1);victim.group.scale.setScalar(Math.max(0,1-Math.max(0,age-.05)*4)*pop);props.forEach((m,i)=>{const a=i*2.399,r=(1+i%5*.35)*age;m.position.set(Math.sin(a)*r,Math.max(-.8,age*(3+i%7*.4)-age*age*2),Math.cos(a)*r);m.rotation.set(age*(i%4+1),age*3,age*4);m.scale.setScalar(Math.min(1,Math.max(0,5-age)));});}
  carry.apply(age);
 },dispose(){scene.remove(root);root.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry'){o.geometry.dispose();}});for(const o of props)if(o instanceof T.Mesh){o.geometry.dispose();(o.material as T.Material).dispose();}owned.forEach(m=>m.dispose());}};
};
