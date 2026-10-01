import {len, mul, norm, add, type Vec} from './config.js';
import type {BodyWound, DeathMotion} from './body-damage.js';

/** Cosmetic physics budget, independent of the damage/knockback stats. */
export const BODY_RAGDOLL = {maxSpeed:24, maxDetachedKick:10, maxSeconds:5} as const;
const capped = (velocity:Vec,max:number) => len(velocity)>max?mul(norm(velocity),max):{...velocity};

/** The death snapshot already contains the victim's resulting velocity. Applying death.force again
 * launches light bullet victims twice. Only a detached subtree receives the extra local impulse. */
export function bodyPartVelocity(motion:DeathMotion, wound:BodyWound|undefined, part:string, direction:Vec):Vec {
  const inherited=capped(motion.velocity,BODY_RAGDOLL.maxSpeed);
  if (!wound?.destroyedPart || part!==wound.destroyedPart) return inherited;
  const mass=part==='head'?5:part==='spine'?30:part.includes('knee')||part.includes('elbow')?3:7;
  const kick=Math.min(BODY_RAGDOLL.maxDetachedKick,wound.momentum/mass);
  return capped(add(inherited,mul(norm(direction),kick)),BODY_RAGDOLL.maxSpeed);
}

/** Claude can sample from authoritative time; skipped packets do not restart the sequence. */
export function bodyDeathPhase(motion:DeathMotion, age:number) {
  const time=Math.max(0,Number.isFinite(age)?age:0);
  if(motion.kind!=='kneel-fall'||time>=motion.releaseAt) return {phase:'ragdoll' as const,progress:1,physicsAge:Math.max(0,time-motion.releaseAt)};
  if(time<motion.kneesAt) return {phase:'kneel' as const,progress:time/Math.max(.001,motion.kneesAt),physicsAge:0};
  return {phase:'fall' as const,progress:(time-motion.kneesAt)/Math.max(.001,motion.releaseAt-motion.kneesAt),physicsAge:0};
}
