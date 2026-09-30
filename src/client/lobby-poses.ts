import * as T from 'three';
import {settleJointCovers,braceWeapon,type CharacterRig} from './character-rig.js';
import {foot,hand} from './rig-ik.js';
import {WEAPONS} from '../shared/content.js';
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
 if(pose==='weapon'){heroStance(r,p.spec,time,breath,sway);settleJointCovers(r);return;}
 r.body.position.set(0,0,0);r.body.rotation.set(0,0,0);r.pelvis.rotation.set(0,0,0);r.spine.rotation.set(0,0,0);r.chest.rotation.set(0,0,0);r.neck.rotation.set(0,0,0);r.head.rotation.set(0,0,0);
 r.leftShoulder.rotation.set(0,0,.03);r.rightShoulder.rotation.set(0,0,-.03);r.leftHand.rotation.set(0,0,0);r.rightHand.rotation.set(0,0,0);
 r.leftLeg.rotation.set(0,0,.08);r.rightLeg.rotation.set(0,0,-.08);r.leftKnee.rotation.set(.12,0,0);r.rightKnee.rotation.set(.12,0,0);r.leftFoot.rotation.set(-.12,0,0);r.rightFoot.rotation.set(-.12,0,0);r.leftToe.rotation.set(0,0,0);r.rightToe.rotation.set(0,0,0);
 if(pose==='chair'){
  r.body.position.y=-.19+breath*.003;r.body.rotation.x=-.08;r.spine.rotation.x=.06+breath*.014;r.head.rotation.set(.03+breath*.012,sway*.065,-.025);
  r.leftLeg.rotation.set(-1.44,.04,.12);r.rightLeg.rotation.set(-1.39,-.04,-.14);r.leftKnee.rotation.x=1.52;r.rightKnee.rotation.x=1.48;r.leftFoot.rotation.x=r.rightFoot.rotation.x=.02;
  r.leftArm.rotation.set(-.18,0,.16);r.rightArm.rotation.set(-.22,0,-.16);r.leftElbow.rotation.set(-1.28,0,0);r.rightElbow.rotation.set(-1.20,0,0);r.leftHand.rotation.x=r.rightHand.rotation.x=-.16;
 }else if(pose==='sleep'){
  // Curled on the right side facing the camera: the lower arm pillows the head, the top arm rests on the hip, knees drawn up.
  r.body.position.set(.24,-.5,-.05);r.body.rotation.set(0,0,1.47);r.pelvis.rotation.set(.05,0,.04);r.spine.rotation.set(.16+breath*.014,0,-.03);r.chest.rotation.set(.08+breath*.015,0,-.04);r.neck.rotation.set(.12,0,-.16);r.head.rotation.set(.06,.1+breath*.02+sway*.015,-.1);
  r.leftLeg.rotation.set(-.95,0,.06);r.rightLeg.rotation.set(-.62,0,-.02);r.leftKnee.rotation.x=1.35;r.rightKnee.rotation.x=1.05;r.leftFoot.rotation.x=.35;r.rightFoot.rotation.x=.25;
  r.rightArm.rotation.set(-2.55,0,-.35);r.rightElbow.rotation.set(-1.75,0,0);r.rightHand.rotation.set(.2,0,.3);r.leftArm.rotation.set(-.42,0,.05+breath*.01);r.leftElbow.rotation.set(-.55,0,0);r.leftHand.rotation.set(0,0,-.1);
 }else{
  // Weight on one leg (contrapposto), forearms stacked with the hands resting on the opposite biceps.
  r.body.rotation.set(.015,-.10,sway*.009);contrapposto(r,breath);r.spine.rotation.x+=breath*.012;r.head.rotation.set(-.055,sway*.075+.1,.03);
  r.leftArm.rotation.set(-.75,0,-.6);r.rightArm.rotation.set(-.72,0,.6);r.leftElbow.rotation.set(-1.5,0,0);r.rightElbow.rotation.set(-1.5,0,0);
  placeHand(r,true,new T.Vector3(-.235,.04+breath*.003,.19),new T.Vector3(.45,-1,.2));
  placeHand(r,false,new T.Vector3(.235,.10+breath*.003,.235),new T.Vector3(-.45,-1,.25));
  r.leftHand.rotation.set(.1,0,-.25);r.rightHand.rotation.set(-.08,0,.25);
 }
 settleJointCovers(r);
}

/** Standing weight shift: the pelvis settles over a straight weight leg and rolls toward the relaxed one, the chest counter-rolls,
 * the weight foot is planted and the free foot rests forward on its toes (foot IK), so the free knee bends. Pure function of time. */
function contrapposto(r:CharacterRig,breath:number,shift=1){
 r.body.position.set(-.022*shift,-.006+breath*.002,0);r.pelvis.rotation.set(0,.06*shift,-.035*shift);r.spine.rotation.set(.02,-.03*shift,.03*shift);r.chest.rotation.set(-.01+breath*.01,-.02*shift,.012*shift);r.neck.rotation.set(.02,0,-.02*shift);
 foot(r,false,new T.Vector3(-.13,-.795,-.02),-.12,0,1);foot(r,true,new T.Vector3(.19,-.795+.03*shift,.1),.3,.32*shift,1);
}
const relax=(r:CharacterRig,left:boolean,breath:number)=>{const s=left?1:-1;(left?r.leftArm:r.rightArm).rotation.set(-.06+breath*.01,0,s*.13);(left?r.leftElbow:r.rightElbow).rotation.set(-.32,0,0);(left?r.leftHand:r.rightHand).rotation.set(0,0,-s*.06);};
const aim=(x:number,y:number,z:number,roll=0)=>{const q=new T.Quaternion().setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(x,y,z).normalize());if(roll)q.multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(0,0,1),roll));return new T.Euler().setFromQuaternion(q);};
/** Presentation-only hero stances for the 'Standing with weapon' lobby pose (the combat idle is only the starting point). */
function heroStance(r:CharacterRig,spec:string,time:number,breath:number,sway:number){
 const w=WEAPONS[spec as keyof typeof WEAPONS];if(!w||spec==='ansatsuken'||spec==='emperor')return;
 r.body.rotation.set(0,.12+sway*.015,0);r.head.rotation.set(-.04+breath*.012,sway*.07-.1,.02);r.leftShoulder.rotation.set(0,0,.02+breath*.006);r.rightShoulder.rotation.set(0,0,-.02-breath*.006);
 contrapposto(r,breath);r.leftHand.rotation.set(0,0,0);r.rightHand.rotation.set(0,0,0);
 const lift=breath*.004;
 if(spec==='greatsword'){braceWeapon(r,new T.Vector3(-.2,-.04+lift,.25),aim(-.3,.6,-.74,1.35),1);relax(r,true,breath);}
 else if(spec==='sword'){braceWeapon(r,new T.Vector3(-.25,-.34+lift,.14),aim(-.45,-.36,.82,-.25),1);relax(r,true,breath);}
 else if(spec==='daggers'){hand(r,false,new T.Vector3(-.27,-.33+lift,.13),aim(-.3,-.55,.78,-.4),1);hand(r,true,new T.Vector3(.27,-.33+lift,.13),aim(.3,-.55,.78,.4),1);}
 else if(spec==='shield'){hand(r,false,new T.Vector3(-.3,-.33+lift,.12),aim(-.3,-.12,.95),1);relax(r,true,breath);}
 else if(spec==='pistol'){hand(r,false,new T.Vector3(-.3,-.36+lift,.09),aim(-.32,-.8,.5,-.5),1);relax(r,true,breath);}
 else if(w.class==='ranged'){braceWeapon(r,new T.Vector3(-.06,-.04+lift,.21),aim(.4,-.5,.76),1);}
 else if(w.class==='magic'){hand(r,true,new T.Vector3(.3,-.2+lift,.15),aim(.05,1,.08),1);r.weapon.position.set(0,0,0);r.weapon.rotation.set(0,0,0);relax(r,false,breath);}
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
