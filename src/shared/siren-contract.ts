/**
 * Siren DEV boss: the shared contract between the systems branch (Astra: simulation, input, networking, audio wiring)
 * and the visuals branch (Claude: model, animation, VFX, intro scene, stage). Both branches compile against this file
 * and merge cleanly because each side only reads it. Change it only by agreement (record the request in DEV_LOG).
 *
 * Pure data and types. No imports from simulation.ts, so it compiles before and after the systems land.
 * Gameplay numbers (damage, ranges, radii, cooldowns) are NOT here; they belong to the systems branch.
 * Timings ARE here, because the animation must hit exactly when the gameplay hits.
 */
import type {Vec} from './config.js';

export const SIREN={
 spec:'siren',intro:'dev-siren',map:'siren',name:'Siren',track:'black-siren-protocol',
 grants:{spec:'weapon:siren',intro:'intro:dev-siren'},
 /** Kill effect names (DeathPresentation.effect). crush: victim of Last Laugh (gibPart 'head'). dissipate: Siren's own death. */
 deaths:{crush:'siren-crush',dissipate:'siren-dissipate'},
} as const;

export type SirenMode='melee'|'ranged';
export type SirenMoveId='swipe'|'rend'|'slam'|'rush'|'laugh'|'hellfire'|'damnation'|'eruption'|'rain'|'mark';
/** LMB is `primary`; keys 1-4 are `abilities[0..3]`. Q toggles the mode (owner may rebind). */
export const SIREN_SLOTS:Record<SirenMode,{primary:SirenMoveId;abilities:readonly [SirenMoveId,SirenMoveId,SirenMoveId,SirenMoveId]}>={
 melee:{primary:'swipe',abilities:['rend','slam','rush','laugh']},
 ranged:{primary:'hellfire',abilities:['damnation','eruption','rain','mark']},
};
export const SIREN_MOVE_NAMES:Record<SirenMoveId,string>={swipe:'Claw Swipe',rend:'Rending Claw',slam:'Grave Slam',rush:'Shadow Rush',laugh:'Last Laugh',hellfire:'Hellfire',damnation:'Damnation',eruption:'Eruption',rain:'Black Rain',mark:'Marked'};

/**
 * Base timings in seconds from the move's start. The simulation writes the ACTUAL absolute times into SirenMoveState
 * (at / hitAt / endAt / until), so if gameplay scales or tunes them the animation follows automatically. Keep the
 * proportions when tuning; the visuals read the absolute times, never this table, except as a fallback.
 *
 * Phase meaning of hitAt / endAt per move:
 *  swipe, rend   hitAt = claw contact begins; contact lasts `active`; until = hitAt + active + recover
 *  slam          hitAt = claws hit the ground (the knock-up instant)
 *  rush          hitAt = dash starts; endAt = dash ends (claws out, trail laid between them)
 *  laugh         hitAt = lunge starts (after the arms-out laugh); endAt = lunge ends. A catch sets caught/caughtAt and
 *                crushAt = caughtAt + crush; until = caughtAt + release. A whiff: until = endAt + whiff.
 *  hellfire,     hitAt = the projectile is released
 *  damnation
 *  eruption      hitAt = the cast lands (warning marker appears); the burst happens at hitAt + warning (world state)
 *  rain          hitAt = the rain begins; the cloud lasts `duration` (world state); Siren is free at until
 *  mark          endAt = the "You...." line ends and the crosshair locks on; hitAt = the seeker is thrown
 */
export const SIREN_TIMING={
 swap:.35,
 swipe:{windup:.12,active:.10,recover:.18},
 rend:{windup:.42,active:.14,recover:.49},
 slam:{windup:.38,recover:.62},
 rush:{windup:.14,dash:.44,recover:.24},
 laugh:{windup:2.0,lunge:.35,whiff:.9,lift:.5,head:.9,crush:3.2,release:3.8},
 hellfire:{windup:.16,recover:.26},
 damnation:{windup:.55,recover:.45},
 eruption:{windup:.30,warning:1.0,burst:.5,recover:.30},
 rain:{windup:.60,recover:.40,duration:10},
 mark:{voice:1.2,lock:1.2,recover:.5},
} as const;

/** Spawn points in Siren's local space (rig convention: +z forward, +x = Siren's LEFT side, y from the body position
 * `PlayerView.pos`). Projectiles should start here so they leave the hands the animation shows. Use sirenPoint(). */
export const SIREN_SPAWN={
 rightHand:{x:-.36,y:.62,z:.52},leftHand:{x:.36,y:.62,z:.52},
 /** Damnation: the two-handed ball held overhead during the windup. */
 overhead:{x:0,y:1.42,z:.12},
 /** Mark seeker: thrown from the right hand at chest height. */
 seeker:{x:-.3,y:.7,z:.7},
} as const;
/** Visual-only hover of the apparition above the physics body (the capsule is unchanged). */
export const SIREN_HOVER={height:.24,bob:.06,bobHz:.55} as const;

export function sirenPoint(pos:Vec,yaw:number,local:{x:number;y:number;z:number}):Vec{
 const s=Math.sin(yaw),c=Math.cos(yaw);return {x:pos.x+local.x*c+local.z*s,y:pos.y+local.y,z:pos.z-local.x*s+local.z*c};
}

/** PlayerView.siren (only while spec === 'siren'). */
export interface SirenPlayerState {
 mode:SirenMode;
 /** Sim time of the last mode change (the swap animation reads it). */
 modeAt:number;
 /** The move in progress, kept until `until` so late joiners and interpolation see the whole animation. */
 move?:SirenMoveState;
 /** Sim time each move is ready again (HUD). */
 ready?:Partial<Record<SirenMoveId,number>>;
}
export interface SirenMoveState {
 id:SirenMoveId;
 /** Increments on every activation, so a repeated move (alternating swipes) restarts its animation. */
 seq:number;
 at:number;hitAt:number;until:number;endAt?:number;
 /** swipe / hellfire: 1 = right hand, -1 = left hand (alternates). */
 side?:1|-1;
 /** rush: horizontal unit direction of the dash. */
 dir?:Vec;
 /** eruption / rain: ground point aimed at. */
 aim?:Vec;
 /** mark: the locked player. laugh: the player the lunge is aimed at. */
 target?:string;
 /** laugh only, set on the tick the lunge connects. */
 caught?:string;caughtAt?:number;crushAt?:number;
}

/** Snapshot.siren: persistent world effects owned by any Siren (absent when none exist). */
export interface SirenWorldView {
 patches:SirenFirePatch[];eruptions:SirenEruption[];rains:SirenRain[];marks:SirenMark[];grabs:SirenGrab[];
}
/** Lingering black fire. hellfire: fireball impact (~10 s). trail: Shadow Rush path segment (~3 s). scorch: Damnation crater. */
export interface SirenFirePatch {id:string;owner:string;kind:'hellfire'|'trail'|'scorch';pos:Vec;radius:number;at:number;until:number;}
/** Warning marker from `at`; the column bursts at burstAt and is gone at until. */
export interface SirenEruption {id:string;owner:string;pos:Vec;radius:number;at:number;burstAt:number;until:number;}
/** The rain cloud. Each drop is a projectile with siren.kind === 'drop'. */
export interface SirenRain {id:string;owner:string;center:Vec;radius:number;at:number;until:number;}
/** The "You...." lock. The crosshair shows from lockAt (beeps accelerate toward throwAt), and stays on the target while
 * the seeker (`shot`) lives. Removed when the seeker is gone. */
export interface SirenMark {id:string;owner:string;target:string;at:number;lockAt:number;throwAt:number;shot?:string;}
/** Last Laugh hold. The simulation pins the victim at the hand; visuals pose the victim (lift, struggle, engulf). */
export interface SirenGrab {owner:string;victim:string;at:number;crushAt:number;releaseAt:number;}

/** projectile.siren on Siren projectiles. The default projectile mesh must be skipped for these; Claude renders them. */
export interface SirenShot {
 kind:'hellfire'|'damnation'|'drop'|'seeker';
 born:number;
 /** drop: where and when it will land (the ground marker). */
 land?:Vec;landAt?:number;
 /** seeker: the marked player. */
 target?:string;
}

/** Snapshot.bossSong: set on the tick the Siren intro starts (song t=0 at that instant), kept across rounds and sets,
 * cleared at match end or lobby return. */
export interface BossSongState {track:'black-siren-protocol';startedAt:number;}

/**
 * Event types (GameEvent.type). Payload uses the existing GameEvent fields as listed; source is always the Siren.
 *  siren-mode      message = new mode
 *  siren-swipe     pos = claw arc centre, amount = side (1 right, -1 left), radius = reach
 *  siren-rend      pos = arc centre, radius = reach
 *  siren-slam      pos = impact point on the ground, radius
 *  siren-rush      pos = dash start, from = dash end (emitted when the dash starts)
 *  siren-laugh     pos = Siren (laugh audio cue, Last Laugh windup start)
 *  siren-lunge     pos = Siren, target = aimed player
 *  siren-catch     pos = victim, target = victim
 *  siren-crush     pos = victim head, target = victim
 *  siren-whiff     pos = Siren
 *  siren-cast      pos = release point, message = move id (hellfire | damnation | eruption | rain | mark)
 *  siren-explode   pos, radius (Damnation impact)
 *  siren-eruption  pos, radius (the burst instant)
 *  siren-drop      pos (a rain drop landed)
 *  siren-mark      pos = Siren, target (the "You...." voice cue)
 *  siren-lock      pos = target, target (crosshair locks; beeps start)
 *  siren-seeker-out    pos (seeker extinguished on indestructible cover)
 *  siren-seeker-hit    pos, target
 *  siren-hit       pos, target, amount = damage, message = move id (any Siren damage confirm)
 */
export type SirenEventType='siren-mode'|'siren-swipe'|'siren-rend'|'siren-slam'|'siren-rush'|'siren-laugh'|'siren-lunge'|'siren-catch'|'siren-crush'|'siren-whiff'|'siren-cast'|'siren-explode'|'siren-eruption'|'siren-drop'|'siren-mark'|'siren-lock'|'siren-seeker-out'|'siren-seeker-hit'|'siren-hit';

/** Intro cues by beat index of the boss track (song t=0 is the instant the Siren intro starts). Times precomputed. */
export const SIREN_INTRO={
 duration:7.2678,nameAt:6.4261,
 cues:[
  {cue:'dark',beat:-1,time:0},
  {cue:'bulb',beat:1,time:.6037},
  {cue:'flicker',beat:3,time:1.428},
  {cue:'glimpse',beat:6,time:2.6645},
  {cue:'reveal',beat:9,time:3.9184},
  {cue:'blackout',beat:13,time:5.596},
  {cue:'smile',beat:14,time:6.0082},
  {cue:'name',beat:15,time:6.4261},
  {cue:'end',beat:17,time:7.2678},
 ],
} as const;
export type SirenIntroCue=typeof SIREN_INTRO.cues[number]['cue'];

/** Accessors: let both branches read the new fields before/after the systems add them to the snapshot types. */
export const sirenOf=(p:object):SirenPlayerState|undefined=>(p as {siren?:SirenPlayerState}).siren;
export const sirenWorldOf=(state:object):SirenWorldView|undefined=>(state as {siren?:SirenWorldView}).siren;
export const sirenShotOf=(shot:object):SirenShot|undefined=>(shot as {siren?:SirenShot}).siren;
export const bossSongOf=(state:object):BossSongState|undefined=>(state as {bossSong?:BossSongState}).bossSong;
