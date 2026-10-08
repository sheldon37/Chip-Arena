import {clamp, norm, v, type Vec, type SpecId} from './config.js';
import {gibPartAt, type GibPart} from './body-hit.js';

/** Authority-owned presentation contract. These are stylized game units, not wound ballistics.
 * HP, collision shapes, attack timing and knockback remain owned by existing combat rules. */
export type TraumaKind = 'bullet' | 'cut' | 'pierce' | 'blunt' | 'blast' | 'energy';
export type WoundKind = 'puncture' | 'cavity' | 'cut' | 'bruise' | 'sever' | 'crush' | 'scorch';
export interface BodyImpact {
  kind: TraumaKind;
  mass: number;
  speed: number;
  radius: number;
  transfer: number;
  /** Expected unmodified health damage; keeps HP mitigation distinct from remaining HP. */
  referenceDamage: number;
  normal?: Vec;
}
export interface BodyWound {
  id: string;
  at: number;
  part: GibPart;
  kind: WoundKind;
  trauma: TraumaKind;
  /** Victim-local coordinates in the authority's upright capsule frame, not animated bone space. */
  point: Vec;
  normal: Vec;
  direction: Vec;
  radius: number;
  severity: number;
  energy: number;
  momentum: number;
  speed: number;
  /** Defined only on a lethal sever/crush. Descendant parts belong to the same detached subtree. */
  destroyedPart?: GibPart;
}
export interface DeathMotion {
  kind: 'ragdoll' | 'kneel-fall';
  /** Seconds relative to death.at; cosmetic motion never delays authoritative death. */
  kneesAt: number;
  releaseAt: number;
  facing: number;
  direction: Vec;
  /** Inherited velocity already includes gameplay knockback; do not apply force twice. */
  velocity: Vec;
}
export const MAX_BODY_WOUNDS = 8;
export const BODY_DEATH_TIMING = {kneesAt: .32, releaseAt: .82} as const;
const finite = (x: number, fallback = 0) => Number.isFinite(x) ? x : fallback;
const bounded = (x: number, lo: number, hi: number) => clamp(finite(x), lo, hi);
const vector = (x: Vec): Vec => v(finite(x.x), finite(x.y), finite(x.z));
const localVector = (x: Vec, yaw: number): Vec => v(x.x*Math.cos(yaw)-x.z*Math.sin(yaw), x.y, x.x*Math.sin(yaw)+x.z*Math.cos(yaw));

/** Central art/game tuning. Thresholds intentionally describe fictional voxel characters. */
export const BODY_TRAUMA = {
  head: {cavity: 65, sever: 280, cut: 38, crush: 110},
  limb: {cavity: 90, sever: 350, cut: 58, crush: 155},
  torso: {cavity: 125, sever: 1000, cut: 380, crush: 650},
  fastHeadSpeed: 110,
} as const;

/** Use the shot's original weapon identity even after reflection by a different class. */
export function projectileBodyImpact(shot: {spec: SpecId; vel: Vec; radius: number; melee?: {kind: 'dagger' | 'shield'}}): BodyImpact {
  const speed = Math.hypot(...Object.values(vector(shot.vel)));
  if (shot.melee?.kind === 'dagger') return {kind:'pierce',mass:.35,speed,radius:shot.radius,transfer:.42,referenceDamage:15};
  if (shot.melee?.kind === 'shield') return {kind:'blunt',mass:6,speed,radius:shot.radius,transfer:.16,referenceDamage:23};
  const bullet = shot.spec === 'pistol' || shot.spec === 'rifle' || shot.spec === 'machinegun';
  const size = bounded(shot.radius / (bullet ? .045 : .14), .4, 6);
  const mass = bullet ? ({pistol:.02,rifle:.04,machinegun:.012} as Record<string,number>)[shot.spec] * Math.min(12,size*size*size) : .1;
  const referenceDamage = ({pistol:23,rifle:58,machinegun:8,fire:22,water:18,lightning:12,darkness:27,leet:32,ansatsuken:39} as Partial<Record<SpecId,number>>)[shot.spec] || 30;
  return {kind:bullet?'bullet':'energy',mass,speed,radius:shot.radius,transfer:bullet?.4:.2,referenceDamage};
}

/** Effective striking mass/speed, not total carried mass. Animation does not determine authority hits. */
export function meleeBodyImpact(spec: SpecId, rate=1, piercing=false): BodyImpact {
  const profiles: Partial<Record<SpecId,[TraumaKind,number,number,number,number]>> = {
    sword:['cut',1.6,10,.08,30], greatsword:['cut',4.8,10,.14,52],
    daggers:[piercing?'pierce':'cut',.4,11,.035,15], shield:['blunt',7,8,.24,23],
    emperor:['blunt',3,8,.12,20], ansatsuken:['blunt',3.5,10,.12,39], siren:['cut',2,10,.1,9],
  };
  const [kind,mass,speed,radius,referenceDamage] = profiles[spec] || ['blunt',2,7,.15,30];
  return {kind,mass,speed:speed*Math.sqrt(bounded(rate,.15,5)),radius,transfer:.8,referenceDamage};
}

/** No source-weapon inference for burns, poison, thorns, falls or unknown scripted deaths. */
export function scriptedBodyImpact(kind: string, damage: number, impulse: number): BodyImpact | undefined {
  if (['blast','shrapnel','dashBlast','blockPulse','leet-nuke','ability'].includes(kind))
    return {kind:'blast',mass:4,speed:Math.sqrt(bounded(damage,0,1000)*5),radius:.25,transfer:.5,referenceDamage:Math.max(1,damage)};
  if (kind === 'siren-crush') return {kind:'blunt',mass:10,speed:20,radius:.2,transfer:1,referenceDamage:Math.max(1,damage)};
  // Other boss/DEV attacks can provide a more specific descriptor at their damage call.
  if (kind.startsWith('siren-') && impulse > 0)
    return {kind:'energy',mass:.2,speed:40,radius:.16,transfer:.2,referenceDamage:Math.max(1,damage)};
  return undefined;
}

export function resolveBodyWound(input: {
  id: string; at: number; position: Vec; yaw: number; point: Vec; direction: Vec;
  impact: BodyImpact; damage: number; lethal: boolean; headshot?: boolean;
}): BodyWound {
  const i = input.impact, yaw = finite(input.yaw), point = vector(input.point), position = vector(input.position);
  const direction = norm(vector(input.direction));
  const part = input.headshot ? 'head' : gibPartAt(position,yaw,point);
  const thresholds = part === 'head' ? BODY_TRAUMA.head : part === 'spine' ? BODY_TRAUMA.torso : BODY_TRAUMA.limb;
  const mass = bounded(i.mass,0,10000), speed = bounded(i.speed,0,1000);
  // Use post-mitigation potential damage, before remaining-HP clipping: 1 HP does not weaken a shot.
  const damageScale = bounded(input.damage / Math.max(1,finite(i.referenceDamage,1)) / (input.headshot?1.5:1),0,4);
  const energy = Math.min(100000,.5*mass*speed*speed*bounded(i.transfer,0,1)*damageScale);
  const momentum = Math.min(100000,mass*speed*bounded(i.transfer,0,1)*damageScale);
  let kind: WoundKind;
  if (i.kind === 'bullet') kind = input.lethal && energy >= thresholds.sever ? 'sever' : energy >= thresholds.cavity ? 'cavity' : 'puncture';
  else if (i.kind === 'cut') kind = input.lethal && energy >= thresholds.cut ? 'sever' : 'cut';
  else if (i.kind === 'pierce') kind = energy >= thresholds.cavity ? 'cavity' : 'puncture';
  else if (i.kind === 'blunt' || i.kind === 'blast') kind = input.lethal && energy >= thresholds.crush ? 'crush' : 'bruise';
  else kind = 'scorch';
  const radiusLimit = part === 'head' ? .15 : part === 'spine' ? .25 : .12;
  const localPoint = localVector(v(point.x-position.x,point.y-position.y,point.z-position.z),yaw);
  // Place verified head hits on the head volume even when the swept capsule contact is its shoulder.
  if (input.headshot) localPoint.y = clamp(localPoint.y,.48,.8);
  return {
    id:input.id,at:finite(input.at),part,kind,trauma:i.kind,
    point:v(clamp(localPoint.x,-.5,.5),clamp(localPoint.y,-.9,.9),clamp(localPoint.z,-.4,.4)),
    normal:localVector(norm(vector(i.normal || v(-direction.x,-direction.y,-direction.z))),yaw),
    direction:localVector(direction,yaw),
    radius:clamp(bounded(i.radius,.012,.3)*(kind==='cavity'?2:1),.012,radiusLimit),
    severity:clamp(energy / (i.kind==='cut'?thresholds.cut:i.kind==='blunt'||i.kind==='blast'?thresholds.crush:thresholds.sever),0,1),
    energy,momentum,speed,destroyedPart:kind==='sever'||kind==='crush'?part:undefined,
  };
}

export function deathMotion(input: {wound?: BodyWound; grounded: boolean; incapacitated: boolean; yaw: number; velocity: Vec}): DeathMotion {
  const w=input.wound, velocity=vector(input.velocity), yaw=finite(input.yaw);
  const kneel=!!w && w.part==='head' && w.trauma==='bullet' && w.speed>=BODY_TRAUMA.fastHeadSpeed
    && (w.kind==='cavity'||w.kind==='sever') && input.grounded && !input.incapacitated
    && Math.hypot(velocity.x,velocity.y,velocity.z)<8;
  return {kind:kneel?'kneel-fall':'ragdoll',kneesAt:kneel?BODY_DEATH_TIMING.kneesAt:0,
    releaseAt:kneel?BODY_DEATH_TIMING.releaseAt:0,facing:yaw,direction:v(Math.sin(yaw),0,Math.cos(yaw)),velocity};
}

export function copyBodyWound(w: BodyWound): BodyWound {
  return {...w,point:{...w.point},normal:{...w.normal},direction:{...w.direction}};
}
export function copyDeathMotion(m: DeathMotion): DeathMotion {
  return {...m,direction:{...m.direction},velocity:{...m.velocity}};
}
