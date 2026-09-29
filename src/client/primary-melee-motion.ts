import * as T from 'three';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import type {attackPose} from './character-animation.js';
import {poseCurve} from './pose-curves.js';
import {attackCommitment,attackTravel} from '../shared/souls-combat.js';
import {smoothMotion as ease} from './motion-quality.js';
const down=new T.Vector3(0,-1,0),keys=[0,.55,1,1.28,1.65,2,2.4,3];
/** Analytic limb placement keeps the planted foot on the floor while the pelvis turns and drops. */
export function foot(r:CharacterRig,left:boolean,target:T.Vector3,yaw:number,heel:number,weight:number){
 const hip=left?r.leftLeg:r.rightLeg,knee=left?r.leftKnee:r.rightKnee,ankle=left?r.leftFoot:r.rightFoot,toe=left?r.leftToe:r.rightToe;
 r.group.updateMatrixWorld(true);const placed=r.group.worldToLocal(ankle.getWorldPosition(new T.Vector3())).lerp(target,weight);placed.y=Math.max(-.795,placed.y);const parent=hip.parent!,start=hip.position.clone(),end=parent.worldToLocal(r.group.localToWorld(placed)),delta=end.sub(start),d=T.MathUtils.clamp(delta.length(),.045,.497),dir=delta.normalize();
 const pole=new T.Vector3(0,0,1).applyQuaternion(r.group.getWorldQuaternion(new T.Quaternion())).applyQuaternion(parent.getWorldQuaternion(new T.Quaternion()).invert());pole.addScaledVector(dir,-pole.dot(dir)).normalize();
 const bend=dir.clone().multiplyScalar(d/2).addScaledVector(pole,Math.sqrt(Math.max(0,.25*.25-d*d/4))),upper=new T.Quaternion().setFromUnitVectors(down,bend.clone().normalize()),lower=new T.Quaternion().setFromUnitVectors(down,dir.clone().multiplyScalar(d).sub(bend).normalize().applyQuaternion(upper.clone().invert()));
 hip.quaternion.copy(upper);knee.quaternion.copy(lower);r.group.updateMatrixWorld(true);
 const desired=r.group.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(new T.Euler(heel,yaw,0)));ankle.quaternion.slerp(ankle.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(desired),weight);toe.rotation.x=-heel*.45*weight;
}
/** Independent hand targets let the blades describe arcs while the shoulders and elbows fold. */
export function hand(r:CharacterRig,left:boolean,target:T.Vector3,orientation:T.Euler,weight:number){
 const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,wrist=left?r.leftHand:r.rightHand,weapon=left?r.offWeapon:r.weapon;
 r.group.updateMatrixWorld(true);const parent=arm.parent!,start=arm.position.clone(),end=parent.worldToLocal(r.chest.localToWorld(target.clone())),delta=end.sub(start),d=T.MathUtils.clamp(delta.length(),.045,.518),dir=delta.normalize(),along=(.27*.27-.25*.25+d*d)/(2*d),pole=new T.Vector3(left?1:-1,-.65,-.3);pole.addScaledVector(dir,-pole.dot(dir)).normalize();
 const bend=dir.clone().multiplyScalar(along).addScaledVector(pole,Math.sqrt(Math.max(0,.27*.27-along*along))),upper=new T.Quaternion().setFromUnitVectors(down,bend.clone().normalize()),lower=new T.Quaternion().setFromUnitVectors(down,dir.clone().multiplyScalar(d).sub(bend).normalize().applyQuaternion(upper.clone().invert()));
 arm.quaternion.slerp(upper,weight);elbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);const q=r.chest.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(orientation));wrist.quaternion.slerp(wrist.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(q),weight);
 weapon.position.set(0,.065,-.055);weapon.rotation.set(0,0,0);
}
/** Reference phrases: paired dagger lunges, low heavy sweeps, stepping sword combo, hip-driven shield slam.
 * The active sweep remains 1..2. Anticipation and recovery supply the large body motion around it. */
export function primaryMeleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>){
 const phase=a.age<a.windup?a.age/a.windup:a.strike<1?1+a.strike:2+(1-a.recovery),c=(values:number[])=>poseCurve(keys,values,phase),weight=ease(a.age/Math.min(.065,a.windup));
 const index=Math.max(0,(p.attackIndex||1)-1),dagger=p.spec==='daggers',heavy=p.spec==='greatsword',shield=p.spec==='shield',reverse=dagger||heavy?index%2===1:!shield&&index%3===1,sign=reverse?-1:1,overhead=!heavy&&!dagger&&!shield&&index%3===2,spin=dagger&&index%4===3,low=heavy||dagger&&index%4===2;
 const turn=c(spin?[0,-.6,-1.4,-.8,.05,1.4,4.5,Math.PI*2]:heavy?[0,-.4,-1.15,-.65,.5,1.35,.7,0]:overhead?[0,-.28,-.42,-.15,.18,.42,.22,0]:shield?[0,-.38,-.65,-.24,.6,.8,.35,0]:[0,-.5,-.9,-.35,.38,1.05,.6,0])*(spin?1:sign);
 const crouch=c(heavy?[0,-.11,-.29,-.34,-.32,-.25,-.10,0]:overhead?[0,-.15,.08,.02,-.25,-.32,-.20,0]:shield?[0,-.05,-.12,-.13,-.22,-.25,-.10,0]:low?[0,-.19,-.27,-.22,-.31,-.26,-.13,0]:[0,-.11,-.17,-.12,-.22,-.27,-.12,0]);
 r.body.position.x+=c([0,-.025,-.05,.0,.05,.07,.03,0])*sign;
 r.body.position.z+=c([0,-.04,-.06,.02,.12,.14,.08,0]);r.body.position.y+=crouch;
 r.body.rotation.y+=turn;r.body.rotation.x+=c(heavy?[0,.06,.28,.40,.34,.20,.03,0]:overhead?[0,-.16,-.28,-.15,.42,.56,.28,0]:[0,-.08,-.16,.05,.23,.32,.14,0]);r.body.rotation.z+=c([0,-.035,-.08,-.03,.08,.12,.05,0])*sign;
 const twist=c([0,-.16,-.32,-.08,.27,.38,.13,0])*(overhead?.5:1)*sign;
 r.pelvis.rotation.y+=twist*.5;r.spine.rotation.y+=twist*.65;r.chest.rotation.y+=twist*.55;r.spine.rotation.x+=overhead?c([0,-.03,-.12,-.08,.10,.2,.06,0]):0;
 r.head.rotation.y-=twist*.7+(spin?0:turn*.48);r.head.rotation.x-=r.body.rotation.x*.55;r.leftShoulder.rotation.y+=twist*.35;r.rightShoulder.rotation.y-=twist*.35;
 // Lead heel lands into the cut; rear toes push off and the foot catches up in recovery.
 // Subtract the authored root travel from planted targets, so the feet do not skate with it.
 const commitment=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,p.attackRate||1,a.windup+a.window),now=p.lastAttack+a.age,travel=attackTravel(commitment,now),plantTime=p.lastAttack+a.windup+a.window*.65,plantTravel=attackTravel(commitment,plantTime);
 const leadingLeft=sign>0,stance=c([.13,.17,.22,.24,.25,.25,.20,.13]),plantWeight=weight;
 const catchup=ease((phase-1.05)/1.35),leadReturn=ease((phase-2.1)/.9),rearReturn=ease((phase-2.65)/.35);
 const leadZ=phase>=1.65?(.31+plantTravel-travel)*(1-leadReturn):c([0,.06,.20,.29,.31,.31,.31,0]);
 const rearZ=-travel*(1-catchup)-.14*catchup*(1-rearReturn);
 const leadLift=c(overhead?[0,.08,.24,.11,0,0,0,0]:heavy?[0,.035,.09,.045,0,0,0,0]:[0,.03,.075,.035,0,0,0,0])+Math.sin(Math.PI*leadReturn)*.09;
 const rearLift=Math.sin(Math.PI*catchup)*.12;
 const lead=new T.Vector3((leadingLeft?1:-1)*stance,-.795+leadLift,leadZ),rear=new T.Vector3((leadingLeft?-1:1)*stance,-.795+rearLift,rearZ);
 if(spin){const pivot=ease((phase-1.8)/1.2)*Math.PI*2;lead.applyAxisAngle(T.Object3D.DEFAULT_UP,pivot);rear.applyAxisAngle(T.Object3D.DEFAULT_UP,pivot);}
 foot(r,leadingLeft,lead,(spin?turn:0)+c([0,.05,.1,.1,.22,.32,.18,0])*sign,Math.sin(Math.PI*leadReturn)*-.15,plantWeight);
 foot(r,!leadingLeft,rear,(spin?turn:0)+c([0,-.1,-.2,.15,.6,.8,.3,0])*sign,Math.sin(Math.PI*catchup)*.38,plantWeight);
 if(heavy){
  // The changed reference drags the blade into a low rotational sweep, then rises with its weight.
  const target=new T.Vector3(c([-.09,-.28,-.40,-.27,.02,.14,-.02,-.09])*sign,c([.08,-.02,-.21,-.26,-.22,-.06,.22,.08]),c([.23,.03,-.05,.13,.39,.38,.18,.23]));
  r.group.updateMatrixWorld(true);const blade=r.group.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(new T.Euler(c([-.15,-.08,-.03,-.03,-.05,-.1,-.65,-.15]),c([.05,-.9,-1.65,-.85,.65,1.4,.5,.05])*sign+turn*.3,0)));
  const orientation=new T.Euler().setFromQuaternion(r.chest.getWorldQuaternion(new T.Quaternion()).invert().multiply(blade));braceWeapon(r,target,orientation,weight);
 }else if(shield){
  hand(r,false,new T.Vector3(c([-.24,-.15,-.08,-.22,-.45,-.48,-.30,-.24]),c([.08,.06,.04,.09,.15,.14,.09,.08]),c([.18,.20,.10,.23,.30,.29,.21,.18])),new T.Euler(c([0,.10,.18,.1,0,-.08,-.03,0]),c([-.15,-.2,-.3,-.3,-.65,-.85,-.4,-.15]),c([0,-.18,-.25,-.1,.12,.18,.07,0])),weight);
  hand(r,true,new T.Vector3(.39,c([-.04,-.1,-.16,-.13,-.02,.03,0,-.04]),c([.1,.0,-.13,-.05,.03,.12,.1,.1])),new T.Euler(0,0,.25),weight);
 }else if(dagger){
  const side=reverse?1:-1;
  hand(r,reverse,new T.Vector3(c([.25,.43,.49,.30,.08,.06,.14,.25])*side,c(low?[.04,.0,-.06,-.10,-.13,.1,.20,.04]:[.04,.22,.34,.23,-.06,-.23,-.09,.04]),c([.18,.0,-.09,.19,.40,.30,.23,.18])),new T.Euler(c(low?[0,.15,.25,.15,-.15,-.2,-.4,0]:[0,-.8,-1.15,-.65,.25,.75,.35,0]),c([-.35,-.85,-1.4,-.6,.5,1.2,.5,-.35])*sign,c([0,-.25,-.45,-.18,.22,.32,.12,0])*sign),weight);
  hand(r,!reverse,new T.Vector3(c([.25,.29,.36,.40,.35,.22,.25,.25])*-side,c([.02,.06,.12,.05,.14,.26,.14,.02]),c([.14,.15,.14,.07,-.08,.0,.10,.14])),new T.Euler(c([0,.15,.35,.1,-.45,-.8,-.35,0]),c([.2,.4,.75,1.1,.85,.2,.1,.2])*sign,c([0,.2,.35,.4,.25,.0,.0,0])*sign),weight);
 }else{
  hand(r,false,new T.Vector3(c(overhead?[-.24,-.18,-.13,-.12,-.07,-.05,-.14,-.24]:[-.24,-.4,-.49,-.27,-.05,.04,-.08,-.24]),c(overhead?[.04,.31,.57,.47,.02,-.22,-.1,.04]:[.04,.15,.26,.19,.0,-.13,-.04,.04]),c([.18,.0,-.09,.18,.41,.39,.25,.18])),new T.Euler(c(overhead?[0,-1.1,-2.05,-1.65,-.55,-.5,-.25,0]:[0,-.2,-.45,-.15,.35,.5,.2,0]),c(overhead?[0,-.1,-.1,0,.05,.1,.05,0]:[-.2,-.8,-1.35,-.55,.6,1.2,.55,-.2])*sign,c([0,-.18,-.32,-.08,.25,.38,.14,0])*sign),weight);
  hand(r,true,new T.Vector3(c([.29,.38,.46,.43,.32,.20,.26,.29]),c([-.04,-.06,-.02,.04,.13,.19,.09,-.04]),c([.08,-.04,-.12,-.02,.18,.27,.18,.08])),new T.Euler(-.3,c([0,.4,.7,.4,-.2,-.5,-.2,0]),.3),weight);
 }
 if(p.chips.hurricane&&!spin)r.body.rotation.y+=a.strike*Math.PI*2; // A complete turn never unwinds during recovery.
}
