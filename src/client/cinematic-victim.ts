import * as T from 'three';
import {createCharacterRig,type CharacterRig} from './character-rig.js';
import {teamArmor} from './team-identity.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
const JOINTS=['body','pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe'] as const;
const ease=(x:number)=>{x=Math.min(1,Math.max(0,x));return x*x*x*(x*(x*6-15)+10);},scratch=new T.Quaternion(),point=new T.Vector3();
/** A kill-cinematic victim built like the live rig (armor glow and, in teams mode, the team bands), so the swap at death
 * does not change the model. Cosmetics are still dressed by the caller. */
export function cinematicVictim(p:PlayerView,state?:Snapshot):CharacterRig{
 const r=createCharacterRig(p.color,p.cosmetics?.bodyColor,true);teamArmor(r,state?.options?.gameMode==='teams'?p.team:undefined);return r;
}
/** Carries the dead rig's last pose into a cinematic victim. The pose is captured once, at spawn (joints matched by name),
 * and blended into the cinematic's own pose over the first `seconds`, so every update stays a pure function of age. */
export class DeathPoseCarry{
 private from:T.Quaternion[]=[];private fromBody=new T.Vector3();private rest:T.Quaternion[];private restBody:T.Vector3;private captured=false;
 constructor(private victim:CharacterRig,original?:T.Object3D,private seconds=.2){
  this.rest=JOINTS.map(j=>victim[j].quaternion.clone());this.restBody=victim.body.position.clone();if(!original)return;
  const found=JOINTS.map(j=>original.getObjectByName(victim[j].name));if(found.some(o=>!o))return;
  this.from=found.map(o=>o!.quaternion.clone());this.fromBody.copy(found[0]!.position);this.captured=true;
 }
 /** Start an update from the rest pose, for cinematics that only write some joints (keeps them order-independent). */
 reset(){JOINTS.forEach((j,i)=>this.victim[j].quaternion.copy(this.rest[i]));this.victim.body.position.copy(this.restBody);}
 /** Finish an update: blend from the captured death pose into whatever the cinematic just posed. */
 apply(age:number){
  if(!this.captured)return;const w=ease(age/this.seconds);if(w>=1)return;
  JOINTS.forEach((j,i)=>{const node=this.victim[j];node.quaternion.copy(scratch.copy(this.from[i]).slerp(node.quaternion,w));});
  this.victim.body.position.copy(point.copy(this.fromBody).lerp(this.victim.body.position,w));
 }
}
