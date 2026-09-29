import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {poseCurve} from './pose-curves.js';
const clamp=T.MathUtils.clamp;
export const smoothMotion=(x:number)=>{x=clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);};
const down=new T.Vector3(0,-1,0);
/** Continuous support / swing foot path, solved through hip and knee rather than rigid leg swings. */
function stepLeg(r:CharacterRig,left:boolean,phase:number,run:number,forward:number,strafe:number,air:number){
 const hip=left?r.leftLeg:r.rightLeg,knee=left?r.leftKnee:r.rightKnee,foot=left?r.leftFoot:r.rightFoot,toe=left?r.leftToe:r.rightToe;
 const u=((phase/(Math.PI*2)+(left?0:.5))%1+1)%1,sprint=smoothMotion((run-.32)/.65),contact=T.MathUtils.lerp(.60,.38,sprint),stance=u<contact,swing=clamp((u-contact)/(1-contact),0,1);
 const walkTravel=stance?1-2*u/contact:-1+2*smoothMotion(swing);
 // Contact → compression → toe-off → heel recovery → knee drive → reach.
 // The heel folds high behind the hip before the knee passes under the body.
 const keys=[0,.17,.38,.55,.73,.90,1];
 const z=poseCurve(keys,[.26,.04,-.27,-.23,.05,.26,.26],u);
 const y=poseCurve(keys,[-.45,-.475,-.41,-.20,-.26,-.40,-.45],u);
 const travel=T.MathUtils.lerp(walkTravel*.21,z,sprint)*run;
 const lift=T.MathUtils.lerp(stance?0:Math.sin(Math.PI*swing)**1.4*.12,y+.475,sprint)*run;
 const target=new T.Vector3(strafe*travel,-.475+lift,forward*travel);
 // Slight preload keeps the joints soft at idle. Legs fold independently in flight.
 if(air>.001)target.lerp(new T.Vector3(left?-.025:.025,left?-.31:-.42,left?.23:-.1),air);
 const length=clamp(target.length(),.08,.495),dir=target.clone().normalize(),along=length/2,height=Math.sqrt(Math.max(0,.25*.25-along*along));
 const pole=new T.Vector3(0,0,1).addScaledVector(dir,-dir.z).normalize(),bend=dir.clone().multiplyScalar(along).addScaledVector(pole,height);
 hip.quaternion.setFromUnitVectors(down,bend.clone().normalize());knee.quaternion.setFromUnitVectors(down,target.clone().normalize().multiplyScalar(length).sub(bend).normalize().applyQuaternion(hip.quaternion.clone().invert()));
 const chain=r.body.quaternion.clone().multiply(r.pelvis.quaternion).multiply(hip.quaternion).multiply(knee.quaternion);foot.quaternion.copy(chain.invert());
 const ankle=poseCurve(keys,[-.14,0,.42,.55,.04,-.18,-.14],u);
 foot.rotateX(ankle*run);toe.rotation.set(stance?Math.max(0,u/contact-.65)*run*.8:0,0,0);
}
/** Bent elbows pump past the hip and forward; opposite the same-side foot. */
function swingArm(r:CharacterRig,left:boolean,phase:number,run:number,air:number){
 const arm=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,hand=left?r.leftHand:r.rightHand,sign=left?1:-1;
 const u=((phase/(Math.PI*2)+(left?0:.5))%1+1)%1,keys=[0,.17,.38,.55,.73,.90,1],weight=clamp(run,0,1);
 const shoulder=poseCurve(keys,[.66,.18,-.86,-.55,.18,.63,.66],u);
 const bend=poseCurve(keys,[-1.05,-1.25,-1.42,-1.55,-1.16,-1.04,-1.05],u);
 arm.rotation.set(T.MathUtils.lerp(left?-.12:-.16,shoulder,weight),0,sign*(.10+air*.22));
 elbow.rotation.set(T.MathUtils.lerp(-.26,bend,weight),0,0);
 hand.rotation.set(Math.sin(phase+(left?0:Math.PI)-.65)*.075*weight,0,sign*.05);
}
type Travel={forward:number;strafe:number;lean:number;lastSpeed:number};const travelState=new WeakMap<CharacterRig,Travel>();
export function locomotion(r:CharacterRig,p:PlayerView,time:number,dt:number){
 dt=clamp(dt,0,.05);const speed=Math.hypot(p.vel.x,p.vel.z),rawForward=(p.vel.x*Math.sin(p.yaw)+p.vel.z*Math.cos(p.yaw))/Math.max(.1,speed),rawStrafe=(p.vel.x*Math.cos(p.yaw)-p.vel.z*Math.sin(p.yaw))/Math.max(.1,speed);
 let state=travelState.get(r);if(!state){state={forward:1,strafe:0,lean:0,lastSpeed:speed};travelState.set(r,state);}
 const blend=1-Math.exp(-dt*13),target=p.grounded?clamp(speed/6.8,0,1.2):0;r.motion+=(target-r.motion)*blend;
 if(speed>.1){state.forward+=(rawForward-state.forward)*blend;state.strafe+=(rawStrafe-state.strafe)*blend;}
 state.lean+=(clamp((speed-state.lastSpeed)/Math.max(dt,.001)*.008,-.10,.10)-state.lean)*(1-Math.exp(-dt*9));state.lastSpeed=speed;
 r.stride+=Math.min(speed,11)*dt*2.05;const run=r.motion,gait=Math.sin(r.stride),breath=Math.sin(time*2.15),shift=Math.sin(time*.85+.4),idle=1-clamp(run,0,1);
 if(p.grounded&&!r.wasGrounded)r.land=.12;r.wasGrounded=p.grounded;r.land*=Math.exp(-dt*11);r.groundWeight+=(Number(p.grounded)-r.groundWeight)*(1-Math.exp(-dt*16));const air=1-r.groundWeight;
 r.body.position.set(shift*.014*idle,-.025-Math.cos(r.stride*2-.3)*.026*run+breath*.005*idle-r.land,0);
 r.body.rotation.set(run*.17*state.forward+state.lean+(p.dashTime?.15:0),0,-state.strafe*run*.08);
 r.pelvis.rotation.set(0,gait*.085*run,Math.cos(r.stride)*.035*run+shift*.012*idle);
 r.spine.rotation.set(-breath*.012*idle,-gait*.07*run,-shift*.01*idle);r.chest.rotation.set(breath*.008*idle,-gait*.10*run,-Math.cos(r.stride)*.022*run);
 r.neck.rotation.set(-r.body.rotation.x*.25,0,0);r.head.rotation.set(-p.pitch*.55-r.body.rotation.x*.4,gait*.07*run+Math.sin(time*.6)*.014*idle,shift*.009*idle);
 stepLeg(r,true,r.stride,run,state.forward,state.strafe,air);stepLeg(r,false,r.stride,run,state.forward,state.strafe,air);
 r.leftShoulder.rotation.set(-breath*.009*idle,gait*.045*run,.03+breath*.008*idle);r.rightShoulder.rotation.set(-breath*.009*idle,-gait*.045*run,-.03-breath*.008*idle);
 swingArm(r,true,r.stride,run,air);swingArm(r,false,r.stride,run,air);
 return {speed,run};
}
const bones=['body','pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe'] as const;
type StoredPose={q:T.Quaternion[];position:T.Vector3};
type Transition={key:string;time:number;at:number;duration:number;last:StoredPose;offset?:StoredPose};
const transitions=new WeakMap<CharacterRig,Transition>();
/** Preserve the incoming pose, decay its offset, and keep sampling the live attack underneath.
 * This is a transition correction, not an all-frame low-pass that would delay hit poses. */
export function finishPose(r:CharacterRig,p:PlayerView,time:number){
 const action=p.juggle?'juggle':p.knockdown?'down':(p.hitStunUntil||0)>time?'flinch':p.blockTime>0?'block':p.emperor?.action&&time<p.emperor.until?p.emperor.action:p.arcade?.action&&time<p.arcade.until?p.arcade.action:WEAPONS[p.spec].class==='melee'&&time-p.lastAttack<.8/(p.attackRate||1)?'attack-'+p.attackIndex:'base';
 const key=p.spec+':'+action+':'+p.grounded,current:StoredPose={q:bones.map(k=>r[k].quaternion.clone()),position:r.body.position.clone()};let s=transitions.get(r);
 if(!s||time<s.time||time-s.time>.35||action==='juggle'||action==='down'||action==='flinch'){s={key,time,at:time,duration:0,last:current};transitions.set(r,s);return;}
 if(key!==s.key){s.offset={q:current.q.map((q,i)=>q.clone().invert().multiply(s!.last.q[i])),position:s.last.position.clone().sub(current.position)};s.key=key;s.at=time;s.duration=action==='base'?.13:.055;}
 if(s.offset){const u=smoothMotion((time-s.at)/s.duration),identity=new T.Quaternion();for(let i=0;i<bones.length;i++)r[bones[i]].quaternion.multiply(s.offset.q[i].clone().slerp(identity,u));r.body.position.addScaledVector(s.offset.position,1-u);if(u>=1)s.offset=undefined;}
 s.time=time;s.last={q:bones.map(k=>r[k].quaternion.clone()),position:r.body.position.clone()};
}
