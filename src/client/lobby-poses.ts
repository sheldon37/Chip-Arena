import * as T from 'three';
import {settleJointCovers,type CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {animateCharacter} from './character-animation.js';
import {placeHand} from './fighter-motion.js';
import {lobbyPose,type LobbyPose} from '../shared/lobby-presentation.js';

/** Isolated presentation player: lobby previews never sample live attacks or damage. */
export function lobbyPlayer(p:PlayerView):PlayerView{return {...p,vel:{x:0,y:0,z:0},yaw:0,pitch:0,grounded:true,lastAttack:-100,lastShot:-100,blockTime:0,guarding:false,reloadTime:0,dashTime:0,waveTime:0,hitStunUntil:0,juggle:undefined,knockdown:undefined,emperor:undefined,arcade:undefined,leet:undefined,swingAt:undefined,swingUntil:undefined};}
export function animateLobbyPose(r:CharacterRig,p:PlayerView,time:number,dt:number,pose:LobbyPose=lobbyPose(p.cosmetics?.lobbyPose)){
 r.aimWeight=1;animateCharacter(r,lobbyPlayer(p),time,dt);
 const breath=Math.sin(time*1.65),sway=Math.sin(time*.72);
 r.weapon.visible=pose==='weapon';r.offWeapon.visible=pose==='weapon'&&['daggers','emperor','ansatsuken'].includes(p.spec);
 if(pose==='weapon')return;
 r.body.position.set(0,0,0);r.body.rotation.set(0,0,0);r.pelvis.rotation.set(0,0,0);r.spine.rotation.set(0,0,0);r.chest.rotation.set(0,0,0);r.neck.rotation.set(0,0,0);r.head.rotation.set(0,0,0);
 r.leftShoulder.rotation.set(0,0,.03);r.rightShoulder.rotation.set(0,0,-.03);r.leftHand.rotation.set(0,0,0);r.rightHand.rotation.set(0,0,0);
 r.leftLeg.rotation.set(0,0,.08);r.rightLeg.rotation.set(0,0,-.08);r.leftKnee.rotation.set(.12,0,0);r.rightKnee.rotation.set(.12,0,0);r.leftFoot.rotation.set(-.12,0,0);r.rightFoot.rotation.set(-.12,0,0);r.leftToe.rotation.set(0,0,0);r.rightToe.rotation.set(0,0,0);
 if(pose==='chair'){
  r.body.position.y=-.19+breath*.003;r.body.rotation.x=-.08;r.spine.rotation.x=.06+breath*.014;r.head.rotation.set(.03+breath*.012,sway*.065,-.025);
  r.leftLeg.rotation.set(-1.44,.04,.12);r.rightLeg.rotation.set(-1.39,-.04,-.14);r.leftKnee.rotation.x=1.52;r.rightKnee.rotation.x=1.48;r.leftFoot.rotation.x=r.rightFoot.rotation.x=.02;
  r.leftArm.rotation.set(-.18,0,.16);r.rightArm.rotation.set(-.22,0,-.16);r.leftElbow.rotation.set(-1.28,0,0);r.rightElbow.rotation.set(-1.20,0,0);r.leftHand.rotation.x=r.rightHand.rotation.x=-.16;
 }else if(pose==='sleep'){
  r.body.position.set(0,-.71,-.08);r.body.rotation.set(-Math.PI/2,0,.04);r.spine.rotation.x=breath*.014;r.chest.rotation.x=breath*.015;r.head.rotation.set(-.04,.18+breath*.018,0);
  r.leftLeg.rotation.set(-.06,0,.12);r.rightLeg.rotation.set(-.12,0,-.10);r.leftKnee.rotation.x=.08;r.rightKnee.rotation.x=.18;r.leftFoot.rotation.x=.08;r.rightFoot.rotation.x=.12;
  r.leftArm.rotation.set(-.1,0,-.05);r.rightArm.rotation.set(-.12,0,.08);r.leftElbow.rotation.set(-.50,0,-.30);r.rightElbow.rotation.set(-.65,0,.34);
 }else{
  r.body.position.y=-.02+breath*.004;r.body.rotation.set(.015,-.10,sway*.009);r.spine.rotation.x=breath*.012;r.head.rotation.set(-.055,sway*.075+.1,0);
  r.leftArm.rotation.set(-.75,0,-.6);r.rightArm.rotation.set(-.72,0,.6);r.leftElbow.rotation.set(-1.5,0,0);r.rightElbow.rotation.set(-1.5,0,0);
  placeHand(r,true,new T.Vector3(-.16,.095,.22),new T.Vector3(.45,-1,.2));
  placeHand(r,false,new T.Vector3(.16,.035,.26),new T.Vector3(-.45,-1,.2));
  r.leftHand.rotation.x=.12;r.rightHand.rotation.x=-.08;
 }
 settleJointCovers(r);
}

/** Chair dimensions match the mannequin's hip and shin lengths. */
export function lobbyChair(){
 const chair=new T.Group(),geometry=new T.BoxGeometry(1,1,1),frame=new T.MeshLambertMaterial({color:0x303744}),pad=new T.MeshLambertMaterial({color:0x6a4851}),trim=new T.MeshLambertMaterial({color:0xe6a878});
 const part=(name:string,pos:number[],size:number[],material:T.Material)=>{const m=new T.Mesh(geometry,material);m.name=name;m.position.fromArray(pos);m.scale.fromArray(size);chair.add(m);};
 part('seat',[0,.34,0],[.68,.09,.62],pad);part('backrest',[0,.79,-.27],[.68,.85,.09],pad);
 for(const x of [-.29,.29])for(const z of [-.24,.24])part('chair-leg',[x,.16,z],[.055,.31,.055],frame);
 for(const x of [-.37,.37]){part('armrest',[x,.60,0],[.09,.065,.55],trim);part('arm-support',[x,.46,-.17],[.045,.26,.045],frame);}
 return chair;
}
