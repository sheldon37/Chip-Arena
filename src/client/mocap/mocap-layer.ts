import type {CharacterRig} from '../character-rig.js';
import type {PlayerView} from '../../shared/simulation.js';
import type {SpecId} from '../../shared/config.js';
import type {attackPose} from '../character-animation.js';
import {meleeRecovery} from '../../shared/souls-combat.js';
import * as T from 'three';
import {smoothMotion as ease,gaitFoot,layerLeg} from '../motion-quality.js';
import {phraseFootwork,phraseSpins} from '../primary-melee-motion.js';
import {CHANNEL_COUNT,sampleClip,retime,clipDuration,type MocapClip} from './mocap-clip.js';
import {applyMocapPose,blendRows,blendRowsTurning,applyMocapRecovery,finishMocapRecovery,keepBladesOffFloor} from './mocap-pose.js';
import {isFirstPersonRig,mocapPosed} from '../pose-flags.js';
import {GREATSWORD_CLIPS as GS} from './greatsword-clips.js';
import {SWORD_CLIPS as SW} from './sword-clips.js';
import {DAGGERS_CLIPS as DG} from './daggers-clips.js';
import {SHIELD_CLIPS as SH} from './shield-clips.js';
import type {MocapPoseOptions} from './mocap-pose.js';

/** Per-weapon mocap sets (presentation only). A weapon without a set, or a state its set leaves out, keeps the procedural
 * layers. `cuts` cycle with the authoritative attackIndex (1-based). Every attack clip's marks are retimed onto that
 * weapon's authoritative windup / active window / recovery, so the captured contact always lands in the hit window.
 * `fit` adapts every pose of the set to the weapon (one-handed grip, clearance dimensions). Every cut stands on the
 * procedural phrase's footwork (primary-melee-motion phraseFootwork: planted steps against the authoritative travel and the
 * dagger 4th cut's hopped spin). `gait`: the guard stands on the procedural legs too (the pack's idle stance is wider than
 * the gait's, and settling into it at every stop slides the planted feet: locomotion-root-sync); the guard's, block's and
 * stagger's captured hips height and pelvis ride over them, each grounded leg re-solved onto the gait's foot (replant). `anticipation` (0..1,
 * default 1) is the share of a cut's captured anticipation the windup keeps: below 1 its poses ease toward the straight
 * path from the start pose to the contact pose (the daggers' .09 s windup cannot carry their full captured pull-back).
 * Source: the owner's Explosive RPG Character pack (scripts/mocap/convert-explosive.mjs; the shield set read left↔right
 * onto the game's right-hand shield, the greatsword from its two-handed sword set). */
export interface MocapSet {idle?:MocapClip;walk?:MocapClip;run?:MocapClip;cuts?:readonly MocapClip[];block?:MocapClip;blockHit?:MocapClip;stagger?:MocapClip;fit?:Pick<MocapPoseOptions,'oneHand'|'floor'|'pommel'|'halfWidth'|'clear'>;gait?:boolean;anticipation?:number|readonly number[]}
export const MOCAP_SETS:Partial<Record<SpecId,MocapSet>>={
 greatsword:{idle:GS.idle,walk:GS.walk,run:GS.run,cuts:[GS.cutA,GS.cutB],block:GS.block,blockHit:GS.blockHit,stagger:GS.stagger,fit:{floor:true},gait:true},
 sword:{idle:SW.idle,walk:SW.walk,run:SW.run,cuts:[SW.cutA,SW.cutB,SW.cutC],block:SW.block,blockHit:SW.blockHit,stagger:SW.stagger,fit:{oneHand:true,floor:true,pommel:.19,halfWidth:.05},gait:true,anticipation:[1,.6,1]},
 daggers:{idle:DG.idle,walk:DG.walk,run:DG.run,cuts:[DG.cutA,DG.cutB,DG.cutC,DG.cutD],block:DG.block,blockHit:DG.blockHit,stagger:DG.stagger,fit:{oneHand:true,floor:true,pommel:.19,halfWidth:.045},gait:true,anticipation:.35},
 shield:{idle:SH.idle,walk:SH.walk,run:SH.run,cuts:[SH.cutA,SH.cutB],block:SH.block,blockHit:SH.blockHit,fit:{oneHand:true,floor:true,clear:'none'},gait:true},
};
/** Development switch (the lab's before/after strips); the game always runs with it on. */
export const MOCAP={enabled:true};
/** The rig's mocap set. The first-person view model keeps its procedural poses, which are framed for the camera (the
 * captured guard holds the blade upright in front of the face, across the crosshair). */
export const mocapSet=(spec:string,r:CharacterRig):MocapSet|undefined=>MOCAP.enabled&&!isFirstPersonRig(r)?MOCAP_SETS[spec as SpecId]:undefined;

/** After the whole pose (animateCharacter): a `floor` set's blades stay above the floor (keepBladesOffFloor), on grounded
 * frames a captured layer posed (procedural specials, knockdowns, juggles and deaths keep their own blades). */
export function mocapFloorGuard(r:CharacterRig,p:PlayerView,time?:number){
 if(!p.grounded||!mocapPosed(r)||!mocapSet(p.spec,r)?.fit?.floor)return;
 if(time===undefined){keepBladesOffFloor(r);return;}
 const rate=p.attackRate||1,end=p.swingUntil??p.lastAttack+p.windup+.16/rate,age=time-end;
 const recovering=p.spec==='daggers'&&(p.attackIndex||1)%4!==0&&age>=0&&age<meleeRecovery(p.spec)/rate+1&&!p.guarding&&p.blockTime<=0&&!(p.hitStunUntil&&p.hitStunUntil>time)&&!(p.roll&&p.roll.at>p.lastAttack)&&!(p.meleeAction&&time<p.meleeAction.until);
 finishMocapRecovery(r,time,recovering,rate,`${p.id}:${p.spec}`);
}
const row=new Float32Array(CHANNEL_COUNT),rowB=new Float32Array(CHANNEL_COUNT),rowC=new Float32Array(CHANNEL_COUNT),_ft=new T.Vector3();
/** A `gait` set's captured hips height and pelvis over the gait's legs: each grounded leg re-solved onto this sample's gait
 * foot (as the ground pass would), so the layers after it (a hit reaction's plants) read the ankles where they show. */
function replant(r:CharacterRig,p:PlayerView){
 if(!p.grounded)return;
 for(const left of [true,false]){const f=gaitFoot(r,left);if(!f)return;_ft.set(f.x,f.y,f.z);layerLeg(r,left,_ft,f.yaw,f.pitch,f.toe);}
}
/** The cut clip for a 1-based attack index. */
export function cutClip(set:MocapSet,attackIndex:number|undefined){const c=set.cuts!;return c[((Math.max(1,attackIndex||1)-1)%c.length+c.length)%c.length];}
/** Clip time for an attack `age` (seconds since lastAttack): [start, contact start, contact end, settled] ↔ [0, windup,
 * windup + window, windup + window + recovery], the clip's `pace` keys (if any) at equal shares of the windup. */
const _at:number[]=[],_mk:number[]=[];
export function cutTime(clip:MocapClip,age:number,windup:number,window:number,recovery:number){
 const e=windup+window,m=clip.marks!,p=clip.pace;if(!p?.length)return retime([0,windup,e,e+recovery],m,age);
 const n=p.length+1;_at.length=_mk.length=0;_at.push(0);_mk.push(m[0]);for(let i=0;i<p.length;i++){_at.push(windup*(i+1)/n);_mk.push(p[i]);}
 _at.push(windup,e,e+recovery);_mk.push(m[1],m[2],m[3]);return retime(_at,_mk,age);
}
/** An Echo Strike follow-up's base: the cut before it as it left its contact (a follow-up starts at that cut's swingUntil,
 * mid-cut, far from the guard), so the follow-up blends in from where the body is. */
export function mocapFollowupBase(r:CharacterRig,p:PlayerView){
 const set=mocapSet(p.spec,r);if(!set?.cuts)return false;const prev=cutClip(set,Math.max(1,(p.attackIndex||1)-1));
 sampleClip(prev,prev.marks![2],row);applyMocapPose(r,row,{weight:1,legs:0,clear:'turn',...set.fit});return true;
}
/** A primary cut (replaces the procedural phrase). `lag` is the hit-stop's phrase lag (primary-melee-motion hitStop). */
export function mocapCut(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,lag=0,weight=1){
 const set=mocapSet(p.spec,r);if(!set?.cuts)return false;const clip=cutClip(set,p.attackIndex),rate=p.attackRate||1,recovery=meleeRecovery(p.spec)/rate,age=a.age-lag;
 const ct=cutTime(clip,age,a.windup,a.window,recovery),m=clip.marks!;sampleClip(clip,ct,row);
 // A partial anticipation: the windup's pose eases toward the straight path between the start and contact poses.
 const slotOf=((Math.max(1,p.attackIndex||1)-1)%set.cuts.length),keep=(typeof set.anticipation==='number'?set.anticipation:set.anticipation?.[slotOf])??1;
 if(keep<1&&ct>m[0]&&ct<m[1]){sampleClip(clip,m[0],rowB);sampleClip(clip,m[1],rowC);blendRowsTurning(rowB,rowC,(ct-m[0])/(m[1]-m[0]),rowB);blendRowsTurning(row,rowB,1-keep,row);}
 // The capture poses the upper body, heading and hips height; the feet are the procedural phrase's footwork (planted,
 // anchored to the floor against the authoritative step travel, stepping only while lifted; the dagger spin's hopped
 // turn), so captured legs, which hang from the travelling root, never skate.
 // The spinning cut (the daggers' 4th) turns the body a full circle over the strike and its recovery, the captured thrust
 // held forward of it; the footwork hops round with it. A complete turn never unwinds.
 // Retain the contact pose, then take the short rotation back to the live guard instead of compressing the capture's
 // wrist regrip into the short game recovery. Quaternion frame blends never pass a blade axis through zero.
 const settle=ease((age-a.windup-a.window)/recovery);
 if(settle>0&&set.idle&&p.spec!=='daggers'){sampleClip(clip,m[2],row);sampleClip(set.idle,p.lastAttack+a.windup+a.window+recovery,rowB);applyMocapRecovery(r,row,rowB,settle,{weight,...set.fit},`${p.id}:${p.spec}:${p.lastAttack}:${p.attackIndex}:${p.reach}:${rate}`);}
 else applyMocapPose(r,row,{weight,legs:0,clear:'turn',...set.fit});
 const spin=phraseSpins(p.spec,p.attackIndex),turn=spin?ease((age-a.windup)/(a.window+recovery))*Math.PI*2:0;
 r.body.rotation.y+=turn*weight;phraseFootwork(r,p,a,weight,turn);
 // The Hurricane chip's full turn over the strike window, as the procedural phrases add it.
 if(p.chips?.hurricane&&!spin)r.body.rotation.y+=ease((age-a.windup)/a.window)*Math.PI*2*weight;
 return true;
}
/** Walk-to-run blend input: the planar speed eased per rig (~.25 s), so a stop or an action that zeroes the velocity in one
 * snapshot fades the run's carry out with the carry weight instead of swapping clips in a frame. Restarts after a rewind or
 * a gap (as the gait does). */
const speeds=new WeakMap<object,{v:number;t:number}>();
function gaitSpeed(r:object,p:PlayerView,time:number){
 const target=Math.hypot(p.vel.x,p.vel.z);let s=speeds.get(r);
 if(!s||time<s.t-.1||time-s.t>.35){s={v:target,t:time};speeds.set(r,s);}
 else{const dt=Math.max(0,time-s.t);s.v+=(target-s.v)*(1-Math.exp(-dt/.25));s.t=Math.max(s.t,time);}
 return s.v;
}
/** Standing guard idle under the running carry (`carry` 0..1 from meleeReady): the idle owns the body while standing (its
 * stance is the stance every captured cut starts from, so cuts enter without the legs jumping); the
 * walk/run captures pose the torso, arms and weapon over the game's gait (stride-synced: clip mark 0 is the left-foot strike,
 * the gait's stride phase 0) as the carry builds. */
export function mocapReady(r:CharacterRig,p:PlayerView,time:number,carry:number){
 const set=mocapSet(p.spec,r);if(!set?.idle)return false;
 const still=1-ease(r.motion/.2);
 // The captured guard is the whole base (not blended over the procedural guard): the carries then turn the wrists between
 // two captured grips, never from the procedural grip (the daggers' is the opposite way round from their reverse grip).
 // (A layer the next one fully covers skips its arms and blade clearance, or is skipped: the clearance is the costly part.)
 const walking=carry>.001&&!!set.walk&&!!set.run,k=walking?ease((gaitSpeed(r,p,time)-3.2)/2.8):0;
 sampleClip(set.idle,time,row);applyMocapPose(r,row,{weight:1,legs:set.gait?0:still,root:still,...set.fit,...(walking&&carry>=.999?{arms:false}:{})});
 if(set.gait&&still>.001)replant(r,p);
 if(walking){
  const phase=((r.stride/(2*Math.PI))%1+1)%1;
  // Walk, then run over it: the two carries hold the blade far apart (raised guard / trailed over the shoulder), so they
  // blend through the joints' rotations (slerp) instead of averaging their blade vectors.
  if(carry*k<.999){sampleClip(set.walk!,set.walk!.marks![0]+phase*clipDuration(set.walk!),row);applyMocapPose(r,row,{weight:carry,legs:0,root:0,...set.fit});}
  if(k>.001){sampleClip(set.run!,set.run!.marks![0]+phase*clipDuration(set.run!),row);applyMocapPose(r,row,{weight:carry*k,legs:0,root:0,...set.fit});}
 }
 return true;
}
/** Raised block: the capture's raise retimed onto the game's ~.13 s raise, then its guard held; a blocked hit plays the
 * captured block impact from its authoritative time. `legs` keeps walking feet on the gait. */
export function mocapBlock(r:CharacterRig,p:PlayerView,time:number,since:number,impactAt:number|undefined,legs:number){
 const set=mocapSet(p.spec,r);if(!set?.block)return false;const m=set.block.marks!;
 sampleClip(set.block,retime([0,.13,.36],m,time-since),row);
 if(set.blockHit&&impactAt!==undefined&&time>=impactAt){
  const h=set.blockHit,u=time-impactAt,hm=h.marks!;
  // Driven back at the impact (peak ~.1 s), recovered into the guard by ~.45 s; blended in over 40 ms, out over the tail.
  const k=ease(u/.04)*(1-ease((u-.35)/.12));if(k>.001){sampleClip(h,retime([0,.1,.45],hm,u),rowB);blendRows(row,rowB,k,row);}
 }
 applyMocapPose(r,row,{weight:1,legs:set.gait?0:legs,root:legs,...set.fit});if(set.gait&&legs>.001)replant(r,p);return true;
}
/** Hitstun base pose: the captured reel retimed into the authoritative stun (impact → extreme at ~35% → recovered at the
 * stun's end), scaled by how frontal the push is (the capture is a hit from the front). The procedural impulse reaction
 * still rides on top (hit-reaction-motion). */
export function mocapStagger(r:CharacterRig,p:PlayerView,time:number,carry:number){
 const set=mocapSet(p.spec,r),h=p.hitReaction;if(!set?.stagger||!mocapReady(r,p,time,carry))return false;
 // Over the guard idle (the pose the stun releases into), at most .7: the impulse reaction adds its own recoil on top.
 const at=h&&h.at<=time?h.at:time,until=Math.max(p.hitStunUntil||0,at+.05),span=until-at,front=h?Math.min(1,Math.max(.35,-h.direction.z*1.4+.35)):.6;
 sampleClip(set.stagger,retime([0,span*.4,span],set.stagger.marks!,time-at),row);
 applyMocapPose(r,row,{weight:.7*front*ease((time-at)/.06)*(1-ease((time-until+.1)/.1)),...(set.gait?{legs:0}:{}),...set.fit});if(set.gait)replant(r,p);return true;
}
