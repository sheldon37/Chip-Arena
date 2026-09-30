import type {SpecId} from '../shared/config.js';
/** Per-spec standing stance table (presentation only). Filled with NEUTRAL values and not read yet:
 * locomotion fills it and reads it at run ~0; melee-guard reads the melee entries for its feet.
 * Coordinates are rig-group space in metres: +x is the rig's left, +z is forward, y is fixed by the ground.
 * lead/rear are ankle targets; lead is the left foot unless leadLeft is false. Angles are radians.
 *  toeOut    positive turns the toes away from the body centre line
 *  crouch    body drop (negative lowers the pelvis; knees bend, ankles stay planted)
 *  pelvisYaw pelvis yaw offset (the chest counter-rotates back onto aim)
 *  kneeSoft  extra knee bend
 *  lean      forward body pitch */
export type FootStance={x:number;z:number;toeOut:number};
export type Stance={lead:FootStance;rear:FootStance;leadLeft:boolean;crouch:number;pelvisYaw:number;kneeSoft:number;lean:number};
/** Today's idle: feet straight under the hips (hip x = +/-.13), no crouch, yaw or lean. */
export const NEUTRAL_STANCE:Readonly<Stance>=Object.freeze({lead:Object.freeze({x:.13,z:0,toeOut:0}),rear:Object.freeze({x:-.13,z:0,toeOut:0}),leadLeft:true,crouch:0,pelvisYaw:0,kneeSoft:0,lean:0});
const specs:SpecId[]=['emperor','ansatsuken','leet','pistol','rifle','machinegun','fire','water','lightning','darkness','sword','daggers','shield','greatsword'];
export const STANCES:Readonly<Record<SpecId,Readonly<Stance>>>=Object.freeze(Object.fromEntries(specs.map(s=>[s,NEUTRAL_STANCE])) as Record<SpecId,Readonly<Stance>>);
export function stanceFor(spec:string):Readonly<Stance>{return STANCES[spec as SpecId]??NEUTRAL_STANCE;}
