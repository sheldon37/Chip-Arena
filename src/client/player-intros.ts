import * as T from 'three';
import {CharacterRig,createCharacterRig,braceWeapon} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {INTRO,IntroStyle,introFrame} from '../shared/intros.js';
import type {Snapshot} from '../shared/simulation.js';

const smooth=(n:number)=>{n=T.MathUtils.clamp(n,0,1);return n*n*(3-2*n);};
/** Absolute-time poses can be sampled directly after latency, a pause, or a camera switch. */
export function poseIntro(r:CharacterRig,style:IntroStyle,age:number){
 const t=Math.max(0,age),settle=smooth((t-1.5)/.65);
 for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);
 r.group.position.set(0,.88,0);r.group.rotation.set(0,-.12,0);r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=r.offWeapon.visible=false;
 r.leftArm.rotation.z=.13;r.rightArm.rotation.z=-.13;r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.15;
 if(style==='neon-drop'){
  const fall=smooth(t/.65),crouch=Math.sin(Math.PI*smooth((t-.65)/.75));r.group.position.y+=3.8*(1-fall);r.body.position.y=-crouch*.30;r.body.rotation.x=crouch*.45;
  r.leftLeg.rotation.x=r.rightLeg.rotation.x=-crouch*.7;r.leftKnee.rotation.x=r.rightKnee.rotation.x=crouch*1.2;r.leftArm.rotation.z=.3+crouch*.65;r.rightArm.rotation.z=-.3-crouch*.65;r.head.rotation.x=-crouch*.3;
  r.chest.rotation.y=-.12*settle;r.rightElbow.rotation.x=-.75*settle;
 }else if(style==='vector-slide'){
  const travel=smooth(t/.95),brake=Math.sin(Math.PI*travel);r.group.position.x=-3.5*(1-travel);r.group.rotation.y=.35*(1-settle);r.body.rotation.z=-.25*brake;r.body.position.y=-.12*brake;
  r.leftLeg.rotation.z=.65*brake;r.rightLeg.rotation.z=-.3*brake;r.leftKnee.rotation.x=.35*brake;r.leftArm.rotation.set(-.7*brake,0,.6*brake+.12);r.rightArm.rotation.set(.6*brake,0,-.45*brake-.12);r.rightElbow.rotation.x=-1.1*settle;r.head.rotation.y=-.25;
 }else if(style==='signal-spin'){
  const spin=smooth(t/1.3);r.group.rotation.y=-Math.PI*2*(1-spin)-.12;r.group.position.y+=Math.sin(Math.PI*spin)*.27;r.leftArm.rotation.z=.9*(1-settle)+.13;r.rightArm.rotation.z=-.9*(1-settle)-.13;r.leftElbow.rotation.x=-.2;
  r.rightArm.rotation.x=-1.25*settle;r.rightElbow.rotation.x=-.3;r.head.rotation.y=-.08;
 }else if(style==='arc-salute'){
  const lift=smooth((t-.15)/.65),lower=smooth((t-1.6)/.55),salute=lift*(1-lower);r.rightArm.rotation.set(-.55*salute,0,-.85*salute-.13);r.rightElbow.rotation.x=-2.05*salute;r.rightHand.rotation.z=-.35*salute;r.head.rotation.z=-.14*salute;r.chest.rotation.y=-.15*lift;r.leftElbow.rotation.x=-.8*lift;braceWeapon(r,new T.Vector3(-.19,.48,.18),new T.Euler(0,0,-.5),salute);
 }else{
  const charge=smooth(t/.7),spread=smooth((t-.7)/.55),hold=1-settle;r.body.position.y=Math.sin(Math.PI*smooth(t/1.6))*.13;r.leftArm.rotation.set(-.75*charge*hold,0,-.45*charge*(1-spread)+1.15*spread*hold+.13);r.rightArm.rotation.set(-.75*charge*hold,0,.45*charge*(1-spread)-1.15*spread*hold-.13);r.leftElbow.rotation.x=r.rightElbow.rotation.x=-1.4*charge*(1-spread)-.15;r.chest.rotation.x=-.1*spread*hold;
 }
}

/** Dedicated presentation stage; never moves the authoritative arena player or their collider. */
export class PlayerIntros {
 readonly scene=new T.Scene();readonly camera=new T.PerspectiveCamera(43,1,.1,80);
 private rig:CharacterRig|undefined;private key='';private rings:T.Mesh[]=[];private sparks:T.Mesh[]=[];private pulse:T.Mesh;private accent=new T.MeshBasicMaterial({color:0x75eeff,toneMapped:false});private sparkGeometry=new T.BoxGeometry(.035,.12,.035);private rim:T.PointLight;
 constructor(){
  this.scene.background=new T.Color(0x050711);this.scene.fog=new T.Fog(0x050711,9,24);this.scene.add(new T.HemisphereLight(0xb3d6ff,0x15101e,1));const key=new T.DirectionalLight(0xd9e6ff,1.6);key.position.set(-3,5,4);this.scene.add(key);this.rim=new T.PointLight(0x56eaff,7,9);this.rim.position.set(1,2,-2);this.scene.add(this.rim);
  const fill=new T.PointLight(0xf369db,4,8);fill.position.set(-3,1.3,1);this.scene.add(fill);
  const floor=new T.Mesh(new T.PlaneGeometry(80,80),new T.MeshLambertMaterial({color:0x0c1020}));floor.rotation.x=-Math.PI/2;floor.position.y=-.015;this.scene.add(floor);
  const grid=new T.GridHelper(40,40,0x365366,0x1a253d);grid.position.y=.003;this.scene.add(grid);
  for(let i=0;i<3;i++){const ring=new T.Mesh(new T.RingGeometry(1.1+i*.36,1.12+i*.36,64),new T.MeshBasicMaterial({color:i===1?0xe861e6:0x5be2ff,transparent:true,opacity:.65-i*.15,side:T.DoubleSide,toneMapped:false}));ring.rotation.x=-Math.PI/2;ring.position.y=.012+i*.002;this.scene.add(ring);this.rings.push(ring);}
  this.pulse=new T.Mesh(new T.RingGeometry(.9,.93,48),new T.MeshBasicMaterial({color:0x80efff,transparent:true,opacity:0,side:T.DoubleSide,depthWrite:false,toneMapped:false}));this.pulse.rotation.x=-Math.PI/2;this.pulse.position.y=.025;this.scene.add(this.pulse);
  for(let i=0;i<18;i++){const spark=new T.Mesh(this.sparkGeometry,this.accent);this.scene.add(spark);this.sparks.push(spark);}
 }
 update(state:Snapshot,aspect:number){
  const frame=introFrame(state);if(!frame){this.clear();return false;}const {player:p,style,age}=frame,key=p.id+JSON.stringify(p.cosmetics)+p.color;
  if(this.key!==key){this.clear();this.key=key;this.rig=createCharacterRig(p.color,p.cosmetics?.bodyColor,true);dressRig(this.rig,p.cosmetics);this.scene.add(this.rig.group);this.accent.color.set(p.color).lerp(new T.Color(0x89eaff),.5);this.rim.color.copy(this.accent.color);}
  poseIntro(this.rig!,style,age);this.camera.aspect=aspect;this.camera.updateProjectionMatrix();const sweep=1-smooth(age/INTRO.nameAt);this.camera.position.set(1.25+sweep*.85,1.35+sweep*.30,4.8);this.camera.lookAt(0,1.05,0);
  const hit={ 'neon-drop':.65,'vector-slide':.95,'signal-spin':1.3,'arc-salute':.8,'pulse-wave':1.05 }[style],burst=Math.max(0,age-hit),fade=Math.max(0,1-burst/1.05);
  this.pulse.rotation.set(style==='pulse-wave'?0:-Math.PI/2,0,0);this.pulse.position.set(0,style==='pulse-wave'?1.05:.025,style==='pulse-wave'?.35:0);this.pulse.visible=age>=hit&&fade>0;this.pulse.scale.setScalar(.2+burst*3);(this.pulse.material as T.MeshBasicMaterial).opacity=fade*.8;
  this.sparks.forEach((spark,i)=>{spark.visible=age>=hit&&fade>0;const a=i*Math.PI*2/this.sparks.length+hit;spark.position.set(Math.cos(a)*(.45+burst*1.6),.06+Math.sin(burst*Math.PI)*(.6+(i%3)*.25),Math.sin(a)*(.45+burst*1.6));spark.scale.setScalar(fade);spark.rotation.set(burst*2,a,burst*3);});
  this.rings.forEach((r,i)=>{r.rotation.z=age*(i%2?-.18:.14);});return true;
 }
 clear(){if(this.rig){this.rig.group.removeFromParent();this.rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')o.geometry.dispose();});}this.rig=undefined;this.key='';}
 dispose(){this.clear();this.scene.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.LineSegments){o.geometry.dispose();const materials=Array.isArray(o.material)?o.material:[o.material];materials.forEach(m=>m.dispose());}});this.scene.clear();}
}
