import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {hand,braceWeapon,supportHand} from './rig-ik.js';
import {smoothMotion as ease,timeJumped} from './motion-quality.js';
import {meleeRecovery} from '../shared/souls-combat.js';
import {isFirstPersonRig} from './pose-flags.js';
/** Combat-ready melee guard and running carry (presentation only).
 * The guard is the pose every standard melee phrase starts and ends in (primary-melee-motion derives its first and last keys
 * from it) and the idle the melee weapons rest in, so combos and idles flow through one stance instead of a neutral.
 * Hand targets are chest space (x is the rig's left, y up, z forward); rotations are the wrist's chest-space Euler (XYZ),
 * the blade runs along the weapon's +z. The feet, crouch and hip yaw come from the melee entries in stances.ts.
 * While running the weapon turns into a Dark Souls style sprint carry: the sword and daggers keep locomotion's arm pump and
 * hold their blades forward-down (a fixed angle in the rig's frame, pitching a little with the arm, never back past
 * vertical), the shield rides its pumping forearm face-forward and the greatsword is braced two-handed low to the
 * right-front, tip down near the floor.
 * The guard is combat readiness, not a permanent idle: it is up through every attack, special and timed block and for a
 * few seconds after, then the fighter relaxes into the plain idle (arms from locomotion, weapon resting in the hand).
 * Readiness is a pure function of authoritative timestamps (never of hits: a hit-stunned pose settles into the same idle
 * a fresh rig shows). */
export type GuardHand={x:number;y:number;z:number;rx:number;ry:number;rz:number};
export type MeleeGuard={main:GuardHand;off:GuardHand;brace:boolean};
const g=(x:number,y:number,z:number,rx:number,ry:number,rz:number):GuardHand=>Object.freeze({x,y,z,rx,ry,rz});
const GUARDS:Record<string,Readonly<MeleeGuard>>={
 // Mid guard: sword hand in front of the lower ribs, blade ~35 degrees up and forward, off hand relaxed at the belt.
 sword:Object.freeze({main:g(-.19,-.02,.30,-.62,.12,-.35),off:g(.22,-.10,.17,-.95,.35,.25),brace:false}),
 // Low at the chest, lead (left) hand forward, rear blade angled in behind it.
 daggers:Object.freeze({main:g(-.17,-.05,.25,-.38,.32,-.25),off:g(.11,.03,.36,-.5,-.18,.25),brace:false}),
 // Shield vertical on the forearm in front of the body, free hand at the hip.
 shield:Object.freeze({main:g(-.21,.06,.24,0,-.18,0),off:g(.30,-.10,.13,-.9,.25,.25),brace:false}),
 // Two-hand low guard (braceWeapon; the left hand takes the support grip), blade forward and down across the centre line.
 greatsword:Object.freeze({main:g(-.02,-.12,.24,.12,.34,-.1),off:g(0,0,0,0,0,0),brace:true}),
};
/** The spec's melee guard (undefined for non-melee specs). */
export function meleeGuard(spec:string):Readonly<MeleeGuard>|undefined{return GUARDS[spec];}
const _v=new T.Vector3(),_e=new T.Euler();
const hashPhase=(id:string)=>{let h=2166136261;for(let i=0;i<id.length;i++){h^=id.charCodeAt(i);h=Math.imul(h,16777619);}return ((h>>>0)%9973)/9973*Math.PI*2;};
/** Pose the guard at `weight` (hands by IK from whatever the layers below posed). A slow, per-player blade drift keeps the
 * held guard alive (deterministic: a pure function of time and the player id). */
export function applyMeleeGuard(r:CharacterRig,p:PlayerView,time:number,weight:number){
 const G=GUARDS[p.spec];if(!G||weight<=.001)return;const ph=p.id?hashPhase(p.id):0,drift=Math.sin(time*1.3+ph)*.025,bob=Math.sin(time*.9+ph*1.7)*.008;
 const m=G.main;_v.set(m.x,m.y+bob,m.z);_e.set(m.rx+drift,m.ry+drift*.6,m.rz);
 if(G.brace){braceWeapon(r,_v,_e,weight);return;}
 hand(r,false,_v,_e,weight);const o=G.off;hand(r,true,_v.set(o.x,o.y-bob,o.z),_e.set(o.rx-drift*.5,o.ry,o.rz),weight);
}
/** Sprint carries. Sword, daggers and shield: the weapon's orientation in the rig group's frame (Euler XYZ; the blade runs
 * along +z, rx tips it down, ry turns it toward the rig's left; mirrored for the left dagger), pitched by `swing` per radian
 * of its arm's pump (the arm forward lifts the tip a little, back drops it), and extra elbow flex (`elbow`, rad). The
 * greatsword is braced (both hands on the grip; the left takes the support grip) at a chest-space grip, so the arms ride
 * the torso's bob instead of pumping. */
const SPRINT:Record<string,{rx:number;ry:number;rz:number;swing:number;elbow:number}>={sword:{rx:.45,ry:-.14,rz:0,swing:-.2,elbow:0},daggers:{rx:.5,ry:.16,rz:0,swing:-.15,elbow:0},shield:{rx:-.08,ry:.35,rz:0,swing:.12,elbow:.15}};
const GS_CARRY={x:-.06,y:-.11,z:.18,rx:.2,ry:-.8,rz:1.45};
const _sp=new T.Euler(),_cq=new T.Quaternion(),_gq=new T.Quaternion();
/** The sprint carry's rig-group orientation for `w` (sword, daggers, shield), from its arm's current pump. */
function sprintOrientation(r:CharacterRig,c:{rx:number;ry:number;rz:number;swing:number},w:T.Object3D,out:T.Quaternion){
 const left=w===r.offWeapon,arm=left?r.leftArm:r.rightArm;return out.setFromEuler(_sp.set(c.rx+c.swing*T.MathUtils.clamp(arm.rotation.x,-1.2,1),left?-c.ry:c.ry,left?-c.rz:c.rz));
}
/** Running carry at `carry` (0..1): sword, daggers and shield turn their weapon(s) in the hand toward the sprint carry (a
 * slerp of the hand-relative rotation, so it fades in and out continuously); the greatsword braces into its two-handed low
 * carry (its support hand is re-seated on the grip part way in). */
export function carryMeleeWeapon(r:CharacterRig,p:PlayerView,carry:number){
 if(carry<=.001)return;
 if(p.spec==='greatsword'){
  // Part way in (a walk, or the carry easing in and out) the blade turns in the rig's frame from the hold it had toward
  // the carry's, instead of following the half-blended wrist and grip (which lifted the tip above the horizontal).
  const g=GS_CARRY;socket(r,r.weapon,_hq);_sq.copy(_hq).multiply(r.weapon.quaternion);braceWeapon(r,_v.set(g.x,g.y,g.z),_e.set(g.rx,g.ry,g.rz),carry);
  if(carry<.999){_cq.identity();for(let n:T.Object3D|null=r.chest;n&&n!==r.group;n=n.parent)_cq.premultiply(n.quaternion);steerWeapon(r,r.weapon,_sq.slerp(_cq.multiply(_gq.setFromEuler(_e)),carry),1);const grip=r.weapon.getObjectByName('support-grip');if(grip)supportHand(r,grip,carry);}
  return;
 }
 const c=SPRINT[p.spec];if(!c)return;
 if(c.elbow)r.rightElbow.rotation.x-=c.elbow*carry;
 for(const w of p.spec==='daggers'?[r.weapon,r.offWeapon]:[r.weapon]){socket(r,w,_hq);sprintOrientation(r,c,w,_sq);w.quaternion.slerp(_hq.invert().multiply(_sq),carry);}
}
/** Forward sprint lean under the melee carries (spine pitch, rad), building from a walk to a run on the stride blend, so it
 * collapses with the stride when an action plants the feet. Not on the first-person view model. */
const LEAN:Record<string,number>={sword:.1,daggers:.12,shield:.08,greatsword:.14};
/** Extra lean while the pilot sprints (double-tap W, every weapon), eased in and out per rig so the flag never snaps. */
const SPRINT_LEAN=.12,sprintBlend=new WeakMap<CharacterRig,number>();
export function sprintLean(r:CharacterRig,p:PlayerView,dt=0){
 const k=LEAN[p.spec],run=ease((r.motion-.1)/.65),b=sprintBlend.get(r)??0,n=b+((p.sprinting?1:0)-b)*(1-Math.exp(-Math.min(.05,Math.max(0,dt))*10));sprintBlend.set(r,n);
 if(isFirstPersonRig(r))return;if(k)r.spine.rotation.x+=k*run;if(n>1e-4)r.spine.rotation.x+=SPRINT_LEAN*n*run;
}
/** Seconds the guard stays up after the last melee action, then the time it takes to lower. */
export const GUARD_HOLD=2.4,GUARD_FADE=.8;
/** Combat readiness 0..1: 1 during a melee attack, special or timed block and for GUARD_HOLD s after it, then easing to 0
 * over GUARD_FADE s. A timestamp in the future (a rewind) does not count. */
export function meleeReadiness(p:PlayerView,time:number){
 const rate=p.attackRate||1,windup=p.swingAt!==undefined?Math.max(.025,p.swingAt-p.lastAttack):Math.max(.025,p.windup),window=p.swingUntil!==undefined&&p.swingAt!==undefined?p.swingUntil-p.swingAt:.16/rate;
 let end=-Infinity;const consider=(start:number,stop:number)=>{if(start<=time)end=Math.max(end,Math.min(stop,time));};
 consider(p.lastAttack,p.lastAttack+windup+window+meleeRecovery(p.spec)/rate);if(p.meleeAction)consider(p.meleeAction.at,p.meleeAction.until);if(p.lastBlock!==undefined)consider(p.lastBlock,p.lastBlock+.3);
 return end===-Infinity?0:1-ease((time-end-GUARD_HOLD)/GUARD_FADE);
}
/** Relaxed idle hold (hand-relative weapon Euler x) where the default dangle would not read: the greatsword rests its tip just
 * above the floor instead of through it. */
const RELAXED:Record<string,number>={greatsword:.27};
/** Idle guard (at combat readiness) under the running carry: the arms fade from the guard into locomotion's pump as the run
 * builds, and into the relaxed idle as readiness lapses. `carryShown` pins the carry weight (an attack blending in over the
 * carry it started from, so the carry does not also fade over the top underneath the phrase). */
export function meleeReady(r:CharacterRig,p:PlayerView,time:number,weight=1,carryShown?:number){
 const carry=runCarry(r),relaxed=RELAXED[p.spec];if(relaxed!==undefined)r.weapon.rotation.x=relaxed;
 // The guard fades against the eased carry too (not the stride blend), so an attack, hit or stop out of a run never
 // flips the weapon from the carry to the guard in a frame or two.
 const s=carryHold(r,carry,time,p),held=carryShown??s.value;carryMeleeWeapon(r,p,held*weight);applyMeleeGuard(r,p,time,(carryShown===undefined?s.guard:(1-held)*meleeReadiness(p,time))*weight);return held;
}
/** The weapon carry eases toward the run's carry weight (time constant ~.16 s) instead of following the stride blend,
 * which collapses in a few frames when an action plants the feet, so a hit or a stop turns the blade over ~.4 s rather
 * than in two frames. Per rig, deterministic; it starts on its target and restarts after a rewind or a jump over .35 s.
 * The step is exact for any dt up to that (a frame the layer skipped, such as a short roll, advances it by the gap), so
 * it settles the same at 30, 60 and 120 Hz. `guard` is the guard weight shown; `carry`/`ready` are the carry and guard
 * shown just before the attack starting at `at` (recorded on its first melee frame). */
type Hold={value:number;time:number;guard:number;at:number;carry:number;ready:number};const holds=new WeakMap<object,Hold>();
function carryHold(r:object,target:number,time:number,p:PlayerView){
 let s=holds.get(r);
 if(!s||timeJumped(s.time,time)){s={value:target,time,guard:1-target,at:time>=p.lastAttack?p.lastAttack:NaN,carry:target,ready:1-target};holds.set(r,s);}
 else{
  if(s.at!==p.lastAttack&&time>=p.lastAttack){s.at=p.lastAttack;s.carry=s.value;s.ready=s.guard;}
  const dt=T.MathUtils.clamp(time-s.time,0,.35);s.time=Math.max(time,s.time);s.value+=(target-s.value)*(1-Math.exp(-dt*6));
 }
 s.guard=(1-s.value)*meleeReadiness(p,time);return s;
}
/** How the current attack was entered, from the rig's own history (recorded on the attack's first melee frame): `carry` is
 * the running carry shown just before it (0..1; 0 when this rig did not see the attack begin) and `guard` the combat-ready
 * guard shown (1 when unknown). A phrase leaving the carry starts from its load instead of detouring through the guard, and
 * one leaving the carry or the relaxed idle blends in over more of the windup (primary-melee-motion). */
export function meleeEntry(r:object,p:PlayerView):{carry:number;guard:number}{const s=holds.get(r);if(s&&s.at===p.lastAttack){_entry.carry=s.carry;_entry.guard=s.ready;}else{_entry.carry=0;_entry.guard=1;}return _entry;}
const _entry={carry:0,guard:1};
/** Advance the carry hold on a melee frame that does not pose the guard (the body of an attack), so the next entry sees the
 * carry the fighter actually shows, not the value from the attack's first frames. */
export function settleCarry(r:CharacterRig,p:PlayerView,time:number){carryHold(r,runCarry(r),time,p);}
/** The running carry's target weight from the stride blend (full from a brisk walk, so walking shows the carry rather
 * than the pumping relaxed hold). The first-person view model never carries; it keeps the guard or the relaxed hold. */
const runCarry=(r:CharacterRig)=>isFirstPersonRig(r)?0:ease((r.motion-.06)/.45);
const _hq=new T.Quaternion(),_sq=new T.Quaternion(),_xq=new T.Quaternion(),_X=new T.Vector3(1,0,0);
const socket=(r:CharacterRig,w:T.Object3D,out:T.Quaternion)=>{out.identity();for(let n:T.Object3D|null=w.parent;n&&n!==r.group;n=n.parent)out.premultiply(n.quaternion);return out;};
/** A weapon's orientation in the rig group's frame, from local quaternions. For the sword, daggers and shield `carry` gives
 * the orientation the running carry at that weight would give the hand as it is posed now (the weapon itself is not moved). */
export function weaponOrientation(r:CharacterRig,p:PlayerView,w:T.Object3D,out:T.Quaternion,carry?:number){
 socket(r,w,out);const c=carry!==undefined?SPRINT[p.spec]:undefined;if(!c)return out.multiply(w.quaternion);
 sprintOrientation(r,c,w,_sq);_hq.copy(out).invert().multiply(_sq);return out.multiply(_xq.setFromAxisAngle(_X,RELAXED[p.spec]??.5).slerp(_hq,carry!));
}
/** Turn a weapon in its hand so its orientation (rig group frame) moves from where the layers above left it toward `to`
 * by `weight`: an attack blending in over the running carry keeps the trailing blade (and its roll) while the hand under it
 * rises into the phrase, instead of carrying the pumping hand's twist up and over the top. */
export function steerWeapon(r:CharacterRig,w:T.Object3D,to:T.Quaternion,weight:number){
 if(weight<=.001)return;socket(r,w,_hq);_sq.copy(_hq).multiply(w.quaternion).slerp(to,weight);w.quaternion.copy(_hq.invert().multiply(_sq));
}
