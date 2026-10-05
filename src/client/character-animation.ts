import {rememberFinalPose} from './pose-transitions.js';
import {meleeBlockMotion} from './melee-block-motion.js';
import {hitReactionMotion,fallbackFlinch} from './hit-reaction-motion.js';
import {dodgeRollMotion} from './dodge-roll-motion.js';
import {committed,meleeRecovery,rollCancelled,rolling} from '../shared/souls-combat.js';
import {locomotion,groundPass,finishPose,GAIT} from './motion-quality.js';
import {meleeMotion,meleeReady} from './melee-motion.js';
import {comboReadyMotion} from './primary-melee-motion.js';
import {mocapStagger,mocapFloorGuard} from './mocap/mocap-layer.js';
import {observeFighterReady} from './fighter-motion.js';
import {sprintLean} from './melee-guard.js';
import {animateEmperor,animateJuggle} from './emperor-animation.js';
import {animateCosmetics} from './auditor-halo.js';
import {animateAnsatsuken} from './ansatsuken-animation.js';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,castWindup} from '../shared/content.js';
import {clamp} from '../shared/config.js';
import {CharacterRig,settleJointCovers} from './character-rig.js';
import {smooth} from './pose-math.js';
import {knockdownMotion} from './knockdown-motion.js';
import {updateAimWeight,rangedLayer,nonMeleeGuard,restoreHeldParts} from './ranged-motion.js';
import {magicLayer} from './magic-motion.js';
import {clearLegsAuthored} from './pose-flags.js';

/** Presentation tunables. `gait` is the live locomotion tuning table (cadence, stance length, lean, crouch, turn dead zone). */
export const ANIMATION={gait:GAIT,deathSeconds:5,deathVoxels:120,reducedDeathVoxels:48,maxDeathVoxels:280} as const;
export type AnimatedRig=CharacterRig;
export function attackPose(p:PlayerView,time:number){
 const w=WEAPONS[p.spec],rate=p.attackRate||1,age=Math.max(0,time-p.lastAttack),windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 const recoverySeconds=meleeRecovery(p.spec)/rate;
 // A roll that started after the attack cancelled it (roll-cancel): the phrase ends, it never resumes after the roll.
 const active=w.class==='melee'&&age<windup+window+recoverySeconds&&!rollCancelled(p,time);
 const load=active&&age<windup?smooth(age/windup):0;
 const strike=active&&age>=windup?clamp((age-windup)/Math.max(.025,window),0,1):0;
 const recovery=active&&age>=windup+window?1-smooth((age-windup-window)/recoverySeconds):1;
 const swing=active?(age<windup?-.9*load:(-.9+strike*2.5)*recovery):0;
 const shotAge=time-(p.lastShot??p.lastAttack),recoil=shotAge>=0?Math.exp(-shotAge*(p.spec==='rifle'?13:22)):0;
 const magic=spellPose(p,time),cast=magic.weight;
 const reload=p.reloadTime>0?Math.sin(clamp(1-p.reloadTime/Math.max(.1,w.reload),0,1)*Math.PI):0;
 return {load,strike,swing,recoil,cast,reload,active,age,shotAge,windup,window,recovery,magic};
}
/** A coil → release → hold → settle phrase, aligned with authoritative spell release. */
export function spellPose(p:PlayerView,time:number){
 const rate=p.attackRate||1,age=Math.max(0,time-p.lastAttack),releaseAt=castWindup(p.spec,rate),windup=releaseAt*.6,release=releaseAt-windup,hold=.07/rate,recover=.26/rate;
 const active=WEAPONS[p.spec].class==='magic'&&age<releaseAt+hold+recover;
 const coil=active&&age<windup?smooth(age/Math.max(.01,windup)):0;
 const drive=active&&age>=windup?smooth((age-windup)/Math.max(.01,release)):0;
 const settle=active&&age>releaseAt+hold?1-smooth((age-releaseAt-hold)/recover):1;
 return {coil,drive,weight:active?(age<windup?coil:settle):0,active,windup,releaseAt,age,settle};
}
/** Distance-driven locomotion under action layers. Authoritative attack timestamps keep the hit phase aligned. */
function sampleCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){
 observeFighterReady(r,p,time);
 clearLegsAuthored(r);animateCosmetics(r,time);
 // Commitment, hitstun and dodge rolls hold the stride: the root is moved by the action, not by running steps.
 const w=WEAPONS[p.spec],planted=committed(p,time)||(p.hitStunUntil||0)>time||rolling(p,time);if(planted)r.motion*=Math.exp(-clamp(dt,0,.05)*22);locomotion(r,{...p,pitch,vel:planted?{x:0,y:p.vel.y,z:0}:p.vel},time,dt,planted);sprintLean(r,p,dt);const pose=attackPose(p,time);
 r.weapon.visible=!p.weaponAway;r.weapon.position.set(0,0,0);r.weapon.rotation.set(.5,0,0);r.weapon.scale.set(1,1,w.class==='melee'?p.reach/w.range:1);r.offWeapon.position.set(0,0,0);r.offWeapon.rotation.set(.55,0,0);r.offWeapon.scale.copy(r.weapon.scale);restoreHeldParts(r);
 if(p.juggle&&time<p.juggle.until){animateJuggle(r,p,time);return pose;}
 if(p.knockdown&&time<p.knockdown.until){knockdownMotion(r,p,time);return pose;}
 if((p.hitStunUntil||0)>time){if(w.class==='melee'){const carry=meleeReady(r,p,time);if(!mocapStagger(r,p,time,carry))comboReadyMotion(r,p,pose,time,carry);}if(hitReactionMotion(r,p,time,true))return pose;fallbackFlinch(r,p,time);return pose;}
 if(dodgeRollMotion(r,p,time))return pose;
 if(p.spec==='emperor'){animateEmperor(r,p,time);hitReactionMotion(r,p,time);return pose;}
 if(p.spec==='ansatsuken'){animateAnsatsuken(r,p,time);hitReactionMotion(r,p,time);return pose;}
 // Swing during travel; settle into the usual aiming stance when stopping or firing.
 updateAimWeight(r,p,pose,w,dt);
 if(w.class!=='magic'&&r.weapon.parent!==r.sockets.rightHand)r.sockets.rightHand.add(r.weapon);
 if(w.class==='ranged'){
  rangedLayer(r,p,pose,time,dt,pitch);
 }else if(w.class==='magic'){
  magicLayer(r,p,pose,time,dt,pitch);
 }else if(p.guarding||p.blockTime>0){meleeBlockMotion(r,p,time);}else{meleeMotion(r,p,pose,time);}

 nonMeleeGuard(r,p);
 hitReactionMotion(r,p,time);
 return pose;
}

export function animateCharacter(r:AnimatedRig,p:PlayerView,time:number,dt:number,pitch=p.pitch){time=p.hp>0?(p.animationTime??time):time;if(p.spec==='siren')return attackPose(p,time);const pose=sampleCharacter(r,p,time,dt,pitch);groundPass(r,p,time,dt);finishPose(r,p,time);mocapFloorGuard(r,p,time);rememberFinalPose(r);settleJointCovers(r);return pose;}

/** Sandbox-only presentation sampler: never writes simulation, attacks, inputs, or health. */
export function animationPreviewPlayer(p:PlayerView,mode:string,time:number):PlayerView{
 const w=WEAPONS[p.spec],cycle=w.class==='melee'?Math.max(.75,w.interval+.45):w.class==='magic'?1.25:1.6,sampleAge=time%cycle;
 const out:PlayerView={...p,lastAbility:undefined,adsSince:undefined,hitReaction:undefined,hitStunUntil:0,roll:undefined,attackCommitment:undefined,animationTime:undefined,vel:{x:0,y:0,z:0},grounded:true,lastAttack:-10,lastShot:-10,blockTime:0,guarding:false,reloadTime:0,swingAt:undefined,swingUntil:undefined,attackRate:1,dashTime:0};
 if(mode==='walk'||mode==='run'){out.vel={x:Math.sin(p.yaw)*(mode==='walk'?2.8:6.8),y:0,z:Math.cos(p.yaw)*(mode==='walk'?2.8:6.8)};}
 if(['attack','windup','release','left-strike','right-strike'].includes(mode)){const age=mode==='windup'?(w.class==='magic'?castWindup(p.spec)*.58:Math.max(.01,w.windup*.92)):mode==='release'?(w.class==='magic'?castWindup(p.spec)+.025:w.windup+.10):mode.endsWith('strike')?w.windup+.10:sampleAge;out.attackIndex=mode==='left-strike'?2:mode==='right-strike'?1:Math.floor(time/cycle)+1;out.lastAttack=time-age;out.lastShot=out.lastAttack+castWindup(p.spec);out.swingAt=out.lastAttack+w.windup;out.swingUntil=out.swingAt+.16;}
 if(mode==='reload')out.reloadTime=w.reload?Math.max(.01,w.reload-time%w.reload):0;
 if(mode==='block')out.blockTime=.3;
 if(mode==='jump'){out.grounded=false;out.vel.y=4;}
 // Ability phrases and ADS (design §12.3): Q/E cast start, or aiming since, at each cycle start.
 if(mode==='ability-q')out.lastAbility=time-sampleAge;if(mode==='ability-e')out.lastSecondary=time-sampleAge;if(mode==='ads')out.adsSince=time-sampleAge;
 return out;
}
