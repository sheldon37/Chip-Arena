import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {smooth} from './pose-math.js';
/** Knockdown presentation (air, flat, legs-up pickup, roll and rise), moved verbatim from sampleCharacter.
 * Pure function of time and the authoritative knockdown timestamps. Caller guarantees p.knockdown&&time<p.knockdown.until. */
export function knockdownMotion(r:CharacterRig,p:PlayerView,time:number){
 const k=p.knockdown!,air=k.landedAt===undefined,age=time-k.at,legsUp=!air&&!k.pickupUsed&&time<(k.pickupUntil||0),rolling=!air&&k.rollAt!==undefined&&time>=k.rollAt&&time<(k.rollUntil||0),rolled=!air&&k.rollUntil!==undefined&&time>=k.rollUntil,rise=rolled?smooth((time-k.rollUntil!)/Math.max(.01,k.until-k.rollUntil!)):0,u=rolling?smooth((time-k.rollAt!)/Math.max(.01,k.rollUntil!-k.rollAt!)):0;
 r.body.rotation.set(air?-Math.min(1.3,.2+age*2):-Math.PI/2*(1-rise)-Math.PI*2*u,rolling||rolled?(Math.atan2(k.rollX??0,k.rollZ??-1)-p.yaw)*(1-rise):0,air?Math.sin(age*5)*.12:0);
 r.body.position.y=air?0:-.64*(1-rise)+(rolling?Math.sin(u*Math.PI)*.12:0);
 r.leftArm.rotation.set(rolling?-1.1:-.45,0,.7*(1-rise));r.rightArm.rotation.set(rolling?-1.1:-.45,0,-.7*(1-rise));r.leftElbow.rotation.x=r.rightElbow.rotation.x=rolling?-1.4:-.3;
 r.leftLeg.rotation.set(legsUp||rolling?-1.15:air?-.5:0,0,.12);r.rightLeg.rotation.set(legsUp||rolling?-.9:air?.3:0,0,-.12);r.leftKnee.rotation.x=legsUp||rolling?1.25:air?.65:.08+.85*rise*(1-rise);r.rightKnee.rotation.x=legsUp||rolling?1.1:air?.25:.08+.85*rise*(1-rise);r.head.rotation.x=air?-.3:rolling?.3:0;
}
