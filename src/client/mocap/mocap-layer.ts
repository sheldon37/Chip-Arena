import type {CharacterRig} from '../character-rig.js';
import type {PlayerView} from '../../shared/simulation.js';
import type {SpecId} from '../../shared/config.js';
import type {attackPose} from '../character-animation.js';
import {attackCommitment,attackTravel,meleeRecovery} from '../../shared/souls-combat.js';
import {smoothMotion as ease,shiftAnkles,captureAnkles,blendAnkles,ankleSet,type AnkleSet} from '../motion-quality.js';
import {CHANNEL_COUNT,MOCAP_CHANNELS,sampleClip,retime,clipDuration,type MocapClip} from './mocap-clip.js';
import {applyMocapPose,blendRows,keepBladesOffFloor} from './mocap-pose.js';
import {isFirstPersonRig,markLegsAuthored} from '../pose-flags.js';
import {GREATSWORD_CLIPS as GS} from './greatsword-clips.js';
import {SWORD_CLIPS as SW} from './sword-clips.js';
import {DAGGERS_CLIPS as DG} from './daggers-clips.js';
import {SHIELD_CLIPS as SH} from './shield-clips.js';
import type {MocapPoseOptions} from './mocap-pose.js';

/** Per-weapon mocap sets (presentation only). A weapon without a set, or a state its set leaves out, keeps the procedural
 * layers. `cuts` cycle with the authoritative attackIndex (1-based). Every attack clip's marks are retimed onto that
 * weapon's authoritative windup / active window / recovery, so the captured contact always lands in the hit window.
 * `fit` adapts every pose of the set to the weapon (one-handed grip, clearance dimensions); `spin` lists the 1-based cut
 * slots that add a full body turn through the strike (the procedural dagger 4th cut's spin). `anticipation` (0..1,
 * default 1) is the share of a cut's captured anticipation the windup keeps: below 1 its poses ease toward the straight
 * path from the start pose to the contact pose (the daggers' .09 s windup cannot carry their full captured pull-back).
 * Sources: greatsword, the owner's Mixamo Great Sword Pack; sword, daggers and shield, the owner's Explosive RPG Character
 * pack (the shield set read left↔right onto the game's right-hand shield). */
export interface MocapSet {idle?:MocapClip;walk?:MocapClip;run?:MocapClip;cuts?:readonly MocapClip[];block?:MocapClip;blockHit?:MocapClip;stagger?:MocapClip;fit?:Pick<MocapPoseOptions,'oneHand'|'pommel'|'halfWidth'|'clear'>;spin?:readonly number[];anticipation?:number}
export const MOCAP_SETS:Partial<Record<SpecId,MocapSet>>={
 greatsword:{idle:GS.idle,walk:GS.walk,run:GS.run,cuts:[GS.cutA,GS.cutB],block:GS.block,blockHit:GS.blockHit,stagger:GS.stagger},
 sword:{idle:SW.idle,walk:SW.walk,run:SW.run,cuts:[SW.cutA,SW.cutB,SW.cutC],block:SW.block,blockHit:SW.blockHit,stagger:SW.stagger,fit:{oneHand:true,pommel:.19,halfWidth:.05}},
 daggers:{idle:DG.idle,walk:DG.walk,run:DG.run,cuts:[DG.cutA,DG.cutB,DG.cutC,DG.cutD],block:DG.block,blockHit:DG.blockHit,stagger:DG.stagger,fit:{oneHand:true,pommel:.19,halfWidth:.045},spin:[4],anticipation:.35},
 shield:{idle:SH.idle,walk:SH.walk,run:SH.run,cuts:[SH.cutA,SH.cutB],block:SH.block,blockHit:SH.blockHit,fit:{oneHand:true,clear:'none'}},
};
/** Development switch (the lab's before/after strips); the game always runs with it on. */
export const MOCAP={enabled:true};
/** The rig's mocap set. The first-person view model keeps its procedural poses, which are framed for the camera (the
 * captured guard holds the blade upright in front of the face, across the crosshair). */
export const mocapSet=(spec:string,r:CharacterRig):MocapSet|undefined=>MOCAP.enabled&&!isFirstPersonRig(r)?MOCAP_SETS[spec as SpecId]:undefined;

/** The one-handed (Explosive pack) sets stand their guard on the procedural legs: the pack's idle stance is wider than the
 * gait's and settling into it at every stop slides the planted feet (locomotion-root-sync). Their cuts' legs are captured. */
const gaitStance=(set:MocapSet)=>!!set.fit?.oneHand;
/** After the whole pose (animateCharacter): the one-handed sets' blades stay above the floor (keepBladesOffFloor). */
export function mocapFloorGuard(r:CharacterRig,p:PlayerView){if(mocapSet(p.spec,r)?.fit?.oneHand)keepBladesOffFloor(r);}
const stances=new WeakMap<CharacterRig,{at:number;legs:AnkleSet}>(),row=new Float32Array(CHANNEL_COUNT),rowB=new Float32Array(CHANNEL_COUNT),rowC=new Float32Array(CHANNEL_COUNT),CH={bx:MOCAP_CHANNELS.indexOf('bx'),bz:MOCAP_CHANNELS.indexOf('bz')};
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
/** A primary cut (replaces the procedural phrase). `lag` is the hit-stop's phrase lag (primary-melee-motion hitStop). */
export function mocapCut(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,lag=0,weight=1){
 const set=mocapSet(p.spec,r);if(!set?.cuts)return false;const clip=cutClip(set,p.attackIndex),rate=p.attackRate||1,recovery=meleeRecovery(p.spec)/rate,age=a.age-lag;
 const ct=cutTime(clip,age,a.windup,a.window,recovery),m=clip.marks!;sampleClip(clip,ct,row);
 // A partial anticipation: the windup's pose eases toward the straight path between the start and contact poses.
 const keep=set.anticipation??1;
 if(keep<1&&ct>m[0]&&ct<m[1]){sampleClip(clip,m[0],rowB);sampleClip(clip,m[1],rowC);blendRows(rowB,rowC,(ct-m[0])/(m[1]-m[0]),rowB);blendRows(row,rowB,1-keep,row);}
 // Out of a gait stance the legs step into the captured stance over the whole windup (the upper body's entry is shorter),
 // blended by their ankles: the captured legs are posed whole, then re-solved from the stance the cut began on (taken on
 // its first frame: the gait follows the legs a layer placed, so it cannot be re-read) toward their own.
 // (`legs` is a share of `weight`: 1/weight poses the captured legs whole under the entry blend.)
 const settle=gaitStance(set)?ease(age/Math.max(a.windup,.1)):1;let from:AnkleSet|undefined;
 if(settle<1){let s=stances.get(r);if(!s){s={at:NaN,legs:ankleSet()};stances.set(r,s);}if(s.at!==p.lastAttack){s.at=p.lastAttack;captureAnkles(r,s.legs);}from=s.legs;}
 applyMocapPose(r,row,{weight,legs:from?1/Math.max(weight,1e-3):1,clear:'turn',...set.fit});if(from)blendAnkles(r,from,settle);
 // The cut places the legs (an action layer's feet, as the procedural phrase's rig-ik foot() marks them): the ground pass
 // keeps their ankles.
 markLegsAuthored(r);
 // The captured feet hold their floor spots from the contact plant (.65 of the active window, as the procedural lead foot
 // plants) through the window, then ease back under the body over the recovery. Both the authoritative step travel (it
 // moves the rig root) and the capture's own hips travel (root motion and lunges, which the pose leaves out) would carry
 // them: the ankles shift by the hips travel since the plant minus the step travel since the plant.
 const c=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,rate,a.windup+a.window),now=p.lastAttack+age,plantAge=a.windup+a.window*.65,plant=p.lastAttack+plantAge;
 if(now>plant){const k=(1-ease((now-plant-a.window*.35-recovery*.3)/(recovery*.6)))*weight;
  if(k>1e-4){sampleClip(clip,cutTime(clip,plantAge,a.windup,a.window,recovery),rowB);const step=attackTravel(c,Math.min(now,c.stepUntil))-attackTravel(c,plant);
   shiftAnkles(r,(row[CH.bx]-rowB[CH.bx])*k,(row[CH.bz]-rowB[CH.bz]-step)*k);}}
 // A full body turn through the strike, as the procedural phrases add it: the set's spinning cut (over the strike and its
 // recovery) and the Hurricane chip (over the strike window); a complete turn never unwinds.
 const slot=((Math.max(1,p.attackIndex||1)-1)%set.cuts.length)+1;
 if(set.spin?.includes(slot))r.body.rotation.y+=ease((age-a.windup)/(a.window+recovery))*Math.PI*2*weight;
 else if(p.chips?.hurricane)r.body.rotation.y+=ease((age-a.windup)/a.window)*Math.PI*2*weight;
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
 const stand=1-carry,still=1-ease(r.motion/.2);
 if(stand>.001){sampleClip(set.idle,time,row);applyMocapPose(r,row,{weight:stand,legs:gaitStance(set)?0:still,root:still,...set.fit});}
 if(carry>.001&&set.walk&&set.run){
  const phase=((r.stride/(2*Math.PI))%1+1)%1,k=ease((gaitSpeed(r,p,time)-3.2)/2.8);
  // Walk, then run over it: the two carries hold the blade far apart (raised guard / trailed over the shoulder), so they
  // blend through the joints' rotations (slerp) instead of averaging their blade vectors.
  sampleClip(set.walk,set.walk.marks![0]+phase*clipDuration(set.walk),row);applyMocapPose(r,row,{weight:carry,legs:0,root:0,...set.fit});
  if(k>.001){sampleClip(set.run,set.run.marks![0]+phase*clipDuration(set.run),row);applyMocapPose(r,row,{weight:carry*k,legs:0,root:0,...set.fit});}
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
 applyMocapPose(r,row,{weight:1,legs,root:legs,...set.fit});return true;
}
/** Hitstun base pose: the captured reel retimed into the authoritative stun (impact → extreme at ~35% → recovered at the
 * stun's end), scaled by how frontal the push is (the capture is a hit from the front). The procedural impulse reaction
 * still rides on top (hit-reaction-motion). */
export function mocapStagger(r:CharacterRig,p:PlayerView,time:number,carry:number){
 const set=mocapSet(p.spec,r),h=p.hitReaction;if(!set?.stagger||!mocapReady(r,p,time,carry))return false;
 // Over the guard idle (the pose the stun releases into), at most .7: the impulse reaction adds its own recoil on top.
 const at=h&&h.at<=time?h.at:time,until=Math.max(p.hitStunUntil||0,at+.05),span=until-at,front=h?Math.min(1,Math.max(.35,-h.direction.z*1.4+.35)):.6;
 sampleClip(set.stagger,retime([0,span*.4,span],set.stagger.marks!,time-at),row);
 applyMocapPose(r,row,{weight:.7*front*ease((time-at)/.06)*(1-ease((time-until+.1)/.1)),...set.fit});return true;
}
