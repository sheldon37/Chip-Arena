import {clamp,forward,mul,v,type Vec,type SpecId} from './config.js';

/** Gameplay load units, shared by authority, prediction, loadout and HUD. */
export const WEAPON_WEIGHT:Record<SpecId,number>={siren:4,daggers:2,sword:5,shield:8,greatsword:12,pistol:2,rifle:7,machinegun:10,fire:4,water:4,lightning:3,darkness:5,leet:3,emperor:1,ansatsuken:1};
export const standardWeapon=(spec:SpecId)=>['daggers','sword','shield','greatsword'].includes(spec);
export const usesRoll=(spec:SpecId)=>spec!=='ansatsuken';
const meleeBaseCost=(spec:SpecId)=>spec==='daggers'?16:20+2*WEAPON_WEIGHT[spec];
/** Primary swing stamina (owner 2026-10-05: another ~25% lower): daggers 7, sword 14, shield 16, greatsword 20. */
export const MELEE_COST_SCALE=.45;
export const meleeCost=(spec:SpecId)=>Math.round(meleeBaseCost(spec)*MELEE_COST_SCALE);
/** E specials keep their earlier cost (daggers 30, sword 38, shield 45, greatsword 55). */
export const meleeSpecialCost=(spec:SpecId)=>spec==='daggers'?30:Math.ceil(meleeBaseCost(spec)*1.25);
export const meleeRecovery=(spec:SpecId)=>(spec==='greatsword'?.5:spec==='daggers'?.15:.3);
export const meleeHitStun=(spec:SpecId)=>.16+WEAPON_WEIGHT[spec]*.018;
export const STAGGER_ESCAPE_SECONDS=.75;

export interface AttackCommitment {at:number;until:number;yaw:number;stepAt:number;stepUntil:number;distance:number;/** An Echo Strike follow-up (its first strike already landed). */followup?:true;}
export function attackCommitment(spec:SpecId,at:number,windup:number,window:number,rate:number,interval:number,followups=0):AttackCommitment {
 return {at,until:at+Math.max(interval,windup+window+meleeRecovery(spec)/rate+followups*(.08+.16)/rate),yaw:0,
  stepAt:at+windup*.55,stepUntil:at+windup+window*.8,distance:spec==='daggers'?.85:spec==='sword'?.65:spec==='shield'?.55:.45};
}
export const committed=(p:{attackCommitment?:AttackCommitment},time:number)=>!!p.attackCommitment&&time<p.attackCommitment.until;
/** First strike of a primary from its serialized commitment (stepAt = at + windup·.55), shared by authority and prediction. */
export const strikeAt=(a:AttackCommitment)=>a.at+(a.stepAt-a.at)/.55;
/** Roll-cancel (owner 2026-10-04): a cancel at least half a tick before the first strike refunds the attack cooldown. */
export const beforeStrike=(a:AttackCommitment,time:number)=>!a.followup&&a.stepAt>a.at&&time<strikeAt(a)-.5/60;
/** The pilot rolled after its last attack started: that attack's phrase is over (presentation; a roll cancels it). */
export const rollCancelled=(p:{roll?:RollState;lastAttack:number},time:number)=>!!p.roll&&p.roll.at>p.lastAttack&&p.roll.at<=time;
const smooth=(u:number)=>{u=clamp(u,0,1);return u*u*(3-2*u);};
export const attackTravel=(a:AttackCommitment,time:number)=>a.distance*smooth((time-a.stepAt)/Math.max(.01,a.stepUntil-a.stepAt));
export function attackStep(a:AttackCommitment|undefined,time:number,dt:number):Vec {
 if(!a||dt<=0)return v();const duration=Math.max(.01,a.stepUntil-a.stepAt);
 return mul(forward(a.yaw),a.distance*(smooth((time+dt-a.stepAt)/duration)-smooth((time-a.stepAt)/duration))/dt);
}

export interface RollState {at:number;until:number;invulnerableAt:number;invulnerableUntil:number;direction:Vec;distance:number;}
export interface RollStats {dashCooldown:number;rollDistance:number;rollSpeed:number;rollCost:number;rollWindow:number;}
export function rollProfile(spec:SpecId,stats:RollStats){
 const weight=WEAPON_WEIGHT[spec],duration=(.4+weight*.0125)/clamp(stats.rollSpeed,.75,1.65);
 const distance=(6.2-weight*.18)*clamp(stats.rollDistance,.65,2),cost=Math.ceil((20+weight*.75)*clamp(stats.rollCost,.4,2));
 const iframeStart=.075,iframeSeconds=Math.min(.18+clamp(stats.rollWindow,0,.10),duration*.6,duration-iframeStart-.09);
 return {weight,duration,distance,cost,iframeStart,iframeSeconds,cooldown:Math.max(duration+.12,.75*stats.dashCooldown)};
}
export const rolling=(p:{roll?:RollState},time:number)=>!!p.roll&&time>=p.roll.at&&time<p.roll.until;
export const rollInvulnerable=(p:{roll?:RollState},time:number)=>!!p.roll&&time>=p.roll.invulnerableAt&&time<p.roll.invulnerableUntil;
export function rollVelocity(roll:RollState,time:number,dt:number):Vec {
 const duration=roll.until-roll.at;
 return mul(roll.direction,roll.distance*(smooth((time+dt-roll.at)/duration)-smooth((time-roll.at)/duration))/dt);
}
/** Damage already ticking and forced arena deaths cannot be erased by rolling. */
export const rollAvoids=(kind:string)=>!['fall','overtime','burn','poison','leet-scare'].includes(kind);
