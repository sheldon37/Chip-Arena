import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {hand,braceWeapon} from './rig-ik.js';
import {smoothMotion as ease} from './motion-quality.js';
import {meleeRecovery} from '../shared/souls-combat.js';
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
 const best=Math.atan2(-_y.dot(_t),_z.dot(_t)),from=r.weapon.rotation.x;r.weapon.rotation.x=from+T.MathUtils.euclideanModulo(best-from+Math.PI,Math.PI*2)-Math.PI;
 r.weapon.rotation.x=from+(r.weapon.rotation.x-from)*c.hold*carry;
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
 * builds, and into the relaxed idle as readiness lapses. */
export function meleeReady(r:CharacterRig,p:PlayerView,time:number,weight=1){
 const carry=ease((r.motion-.10)/.65),relaxed=RELAXED[p.spec];if(relaxed!==undefined)r.weapon.rotation.x=relaxed;
 carryMeleeWeapon(r,p,carry*weight);applyMeleeGuard(r,p,time,(1-carry)*meleeReadiness(p,time)*weight);return carry;
}
