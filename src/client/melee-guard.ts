import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {hand,braceWeapon} from './rig-ik.js';
import {smoothMotion as ease,timeJumped} from './motion-quality.js';
import {meleeRecovery} from '../shared/souls-combat.js';
import {isFirstPersonRig} from './pose-flags.js';
/** Combat-ready melee guard and running carry (presentation only).
 * The guard is the pose every standard melee phrase starts and ends in (primary-melee-motion derives its first and last keys
 * from it) and the idle the melee weapons rest in, so combos and idles flow through one stance instead of a neutral.
 * Hand targets are chest space (x is the rig's left, y up, z forward); rotations are the wrist's chest-space Euler (XYZ),
 * the blade runs along the weapon's +z. The feet, crouch and hip yaw come from the melee entries in stances.ts.
 * While running, the arms keep locomotion's pump and only the weapon turns into a carry that reads the class:
 * sword and greatsword trail tip-back (held near a fixed angle in the rig's frame so they never windmill overhead),
 * daggers turn to a reverse grip along the forearms and the shield rides flat on its forearm.
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
const _v=new T.Vector3(),_e=new T.Euler(),_h=new T.Quaternion();
const hashPhase=(id:string)=>{let h=2166136261;for(let i=0;i<id.length;i++){h^=id.charCodeAt(i);h=Math.imul(h,16777619);}return ((h>>>0)%9973)/9973*Math.PI*2;};
/** Pose the guard at `weight` (hands by IK from whatever the layers below posed). A slow, per-player blade drift keeps the
 * held guard alive (deterministic: a pure function of time and the player id). */
export function applyMeleeGuard(r:CharacterRig,p:PlayerView,time:number,weight:number){
 const G=GUARDS[p.spec];if(!G||weight<=.001)return;const ph=p.id?hashPhase(p.id):0,drift=Math.sin(time*1.3+ph)*.025,bob=Math.sin(time*.9+ph*1.7)*.008;
 const m=G.main;_v.set(m.x,m.y+bob,m.z);_e.set(m.rx+drift,m.ry+drift*.6,m.rz);
 if(G.brace){braceWeapon(r,_v,_e,weight);return;}
 hand(r,false,_v,_e,weight);const o=G.off;hand(r,true,_v.set(o.x,o.y-bob,o.z),_e.set(o.rx-drift*.5,o.ry,o.rz),weight);
}
/** Running blade carry: the rig-group direction the blade trails toward (tip back and a little down and out) and how much
 * of the arm pump it cancels. The blade only turns about the hand's own x axis (it stays in the plane the fist holds it
 * in), so the grip still reads and the pose stays a plain hand-relative rotation. */
const CARRY:Record<string,{x:number;y:number;z:number;hold:number}>={sword:{x:-.25,y:.2,z:-1,hold:.85},greatsword:{x:-.3,y:.06,z:-1,hold:.92}};
const _y=new T.Vector3(),_z=new T.Vector3(),_t=new T.Vector3();
/** Running carry at `carry` (0..1): counter-rotates the weapon(s) under locomotion's arm pump (sword and greatsword trail
 * tip-back instead of windmilling overhead), daggers turn to a reverse grip, the shield rides flat on its forearm.
 * Chain rotations are read from local quaternions (no world matrix update). */
export function carryMeleeWeapon(r:CharacterRig,p:PlayerView,carry:number){
 if(carry<=.001)return;
 if(p.spec==='daggers'){for(const w of [r.weapon,r.offWeapon]){w.rotation.x=T.MathUtils.lerp(w.rotation.x,-1.38,carry);w.rotation.z=T.MathUtils.lerp(w.rotation.z,w===r.weapon?.35:-.35,carry);}return;}
 if(p.spec==='shield'){r.weapon.rotation.x=T.MathUtils.lerp(r.weapon.rotation.x,.04,carry);return;}
 const c=CARRY[p.spec];if(!c)return;
 // The hand socket's y and z axes in the rig group's frame; the x rotation that best points the blade (+z) along the carry.
 _h.identity();for(let n:T.Object3D|null=r.weapon.parent;n&&n!==r.group;n=n.parent)_h.premultiply(n.quaternion);
 _y.set(0,1,0).applyQuaternion(_h);_z.set(0,0,1).applyQuaternion(_h);_t.set(c.x,c.y,c.z).normalize();
 // The turn always runs back over the top (decreasing x) and never through the floor, so the direction never flips
 // while the carry fades in or out (the target sits well away from the incoming hold, so there is no branch to cross).
 const best=Math.atan2(-_y.dot(_t),_z.dot(_t)),from=r.weapon.rotation.x,turn=T.MathUtils.euclideanModulo(best-from,Math.PI*2)-Math.PI*2;
 r.weapon.rotation.x=from+turn*c.hold*carry;
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
/** The running carry's target weight from the stride blend. The first-person view model never carries (a trailing blade
 * would leave the camera's view); it keeps the guard or the relaxed hold while running. */
const runCarry=(r:CharacterRig)=>isFirstPersonRig(r)?0:ease((r.motion-.10)/.65);
const _hq=new T.Quaternion(),_sq=new T.Quaternion(),_xq=new T.Quaternion(),_X=new T.Vector3(1,0,0);
const socket=(r:CharacterRig,w:T.Object3D,out:T.Quaternion)=>{out.identity();for(let n:T.Object3D|null=w.parent;n&&n!==r.group;n=n.parent)out.premultiply(n.quaternion);return out;};
/** A weapon's orientation in the rig group's frame, from local quaternions. For the sword and greatsword `carry` gives the
 * orientation the running carry at that weight would give the hand as it is posed now (the weapon itself is not moved). */
export function weaponOrientation(r:CharacterRig,p:PlayerView,w:T.Object3D,out:T.Quaternion,carry?:number){
 socket(r,w,out);const c=w===r.weapon&&carry!==undefined?CARRY[p.spec]:undefined;if(!c)return out.multiply(w.quaternion);
 _y.set(0,1,0).applyQuaternion(out);_z.set(0,0,1).applyQuaternion(out);_t.set(c.x,c.y,c.z).normalize();
 const from=RELAXED[p.spec]??.5,turn=T.MathUtils.euclideanModulo(Math.atan2(-_y.dot(_t),_z.dot(_t))-from,Math.PI*2)-Math.PI*2;
 return out.multiply(_xq.setFromAxisAngle(_X,from+turn*c.hold*carry!));
}
/** Turn a weapon in its hand so its orientation (rig group frame) moves from where the layers above left it toward `to`
 * by `weight`: an attack blending in over the running carry keeps the trailing blade (and its roll) while the hand under it
 * rises into the phrase, instead of carrying the pumping hand's twist up and over the top. */
export function steerWeapon(r:CharacterRig,w:T.Object3D,to:T.Quaternion,weight:number){
 if(weight<=.001)return;socket(r,w,_hq);_sq.copy(_hq).multiply(w.quaternion).slerp(to,weight);w.quaternion.copy(_hq.invert().multiply(_sq));
}
