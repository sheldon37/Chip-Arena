import * as T from 'three';
import type {KillEffectFactory} from './death-effects.js';
import {createCharacterRig,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {createBassVan,createVanDriver} from './bass-van-rig.js';
import {BASS_VAN,vanBass} from './bass-van-timing.js';

const clamp=(x:number)=>T.MathUtils.clamp(x,0,1),smooth=(x:number)=>{const a=clamp(x);return a*a*(3-2*a);};
function reset(r:CharacterRig){for(const j of [r.group,r.body,r.spine,r.head,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee])j.rotation.set(0,0,0);r.body.position.set(0,0,0);}
function seated(r:CharacterRig){r.leftLeg.rotation.x=r.rightLeg.rotation.x=-Math.PI/2;r.leftKnee.rotation.x=r.rightKnee.rotation.x=Math.PI/2;r.leftArm.rotation.x=r.rightArm.rotation.x=-.75;r.leftElbow.rotation.x=r.rightElbow.rotation.x=-.8;}
function run(r:CharacterRig,t:number){const stride=Math.sin(t*26);r.leftLeg.rotation.x=stride*.75;r.rightLeg.rotation.x=-stride*.75;r.leftKnee.rotation.x=Math.max(0,stride);r.rightKnee.rotation.x=Math.max(0,-stride);r.leftArm.rotation.x=-stride;r.rightArm.rotation.x=stride;}

/** Slapstick toss into an open music van, then a bass-powered getaway. */
export const bassVan:KillEffectFactory=(scene,_original,p,d,quality)=>{
 const root=new T.Group();root.name='bass-van';root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;scene.add(root);
 const vehicle=createBassVan(quality),driver=createVanDriver(),guy=driver.rig,victim=createCharacterRig(p.color,p.cosmetics?.bodyColor),van=vehicle.van;
 victim.group.name='bass-passenger';dressRig(victim,p.cosmetics);root.add(van,guy.group,victim.group);
 const seat=new T.Vector3(-.40,.13,.17),driverSeat=new T.Vector3(1.2,.12,.12),grabPos=new T.Vector3(-.67,.02,.1);
 const inVan=(r:CharacterRig,position:T.Vector3,yaw:number)=>{r.group.position.copy(position).applyMatrix4(van.matrix);r.group.quaternion.copy(van.quaternion).multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),yaw));};
 return {count:quality<.5?44:48,update(age){
  const t=Math.max(0,age),arrive=smooth(t/BASS_VAN.arrive),depart=Math.max(0,t-BASS_VAN.drive),bass=vanBass(t);
  reset(victim);reset(guy);victim.group.position.set(0,0,0);victim.weapon.visible=victim.offWeapon.visible=false;
  van.position.set(T.MathUtils.lerp(-10,.65,arrive)+depart*depart*17,bass*.20,-1.65);
  van.rotation.set(Math.sin((t-BASS_VAN.musicStart)*19)*bass*.035,0,0);van.updateMatrix();
  for(const wheel of vehicle.wheels)wheel.rotation.z=-van.position.x/.33;
  vehicle.cones.forEach(cone=>{cone.position.z=.369+bass*.06;});vehicle.rings.forEach(ring=>ring.scale.setScalar(1+bass*.07));
  vehicle.notes.forEach((note,i)=>{note.visible=t>=BASS_VAN.musicStart&&t<BASS_VAN.end;const phase=((t-BASS_VAN.musicStart)*1.4+i*.27)%1;note.position.set(-1.2+i*.6+Math.sin(i+t*2)*.12,2.17+phase*.8,.25);note.rotation.z=Math.sin(t*5+i)*.2;note.scale.setScalar((.7+bass*.4)*(1-phase*.45));});
  if(t<BASS_VAN.arrive){inVan(guy,driverSeat,Math.PI/2);seated(guy);}
  else if(t<BASS_VAN.grab){const walk=smooth((t-BASS_VAN.arrive)/(BASS_VAN.grab-BASS_VAN.arrive));guy.group.position.copy(driverSeat).applyMatrix4(van.matrix).lerp(grabPos,walk);guy.group.rotation.y=Math.PI/2;run(guy,t);}
  else if(t<BASS_VAN.toss+.1){guy.group.position.copy(grabPos);const wind=smooth((t-BASS_VAN.grab)/.17);guy.group.rotation.y=Math.PI/2+wind*.45;guy.spine.rotation.x=.17*(1-wind);guy.leftArm.rotation.x=guy.rightArm.rotation.x=-1.15-wind*.8;guy.leftElbow.rotation.x=guy.rightElbow.rotation.x=-.25;}
  else if(t<2.05){const back=smooth((t-BASS_VAN.toss-.1)/.7);guy.group.position.copy(grabPos).lerp(driverSeat.clone().applyMatrix4(van.matrix),back);guy.group.rotation.y=Math.PI/2;run(guy,t);}
  else{inVan(guy,driverSeat,Math.PI/2);seated(guy);guy.head.rotation.z=bass*.09;}
  if(t>=BASS_VAN.grab&&t<BASS_VAN.toss){const lift=smooth((t-BASS_VAN.grab)/(BASS_VAN.toss-BASS_VAN.grab));victim.group.position.y=lift*.28;victim.leftArm.rotation.z=.5*lift;victim.rightArm.rotation.z=-.5*lift;}
  else if(t>=BASS_VAN.toss&&t<BASS_VAN.musicStart){const flight=clamp((t-BASS_VAN.toss)/(BASS_VAN.musicStart-BASS_VAN.toss)),target=seat.clone().applyMatrix4(van.matrix),land=smooth((flight-.55)/.45);victim.group.position.set(target.x*flight,.28+(target.y-.28)*flight+Math.sin(flight*Math.PI)*.30,target.z*flight);victim.group.rotation.z=-Math.PI/2*Math.sin(flight*Math.PI);victim.leftArm.rotation.z=1.3*Math.sin(flight*Math.PI);victim.rightArm.rotation.z=-victim.leftArm.rotation.z;victim.leftLeg.rotation.x=T.MathUtils.lerp(-.7,-Math.PI/2,land);victim.rightLeg.rotation.x=T.MathUtils.lerp(.4,-Math.PI/2,land);victim.leftKnee.rotation.x=victim.rightKnee.rotation.x=Math.PI/2*land;victim.leftArm.rotation.x=victim.rightArm.rotation.x=-1.1*land;victim.leftElbow.rotation.x=victim.rightElbow.rotation.x=-.8*land;}
  else if(t>=BASS_VAN.musicStart){inVan(victim,seat,0);seated(victim);victim.head.rotation.z=Math.sin(t*12)*bass*.13;victim.leftArm.rotation.x=-1.1;victim.rightArm.rotation.x=-1.1;}
 },dispose(){scene.remove(root);vehicle.dispose();driver.dispose();}};
};
