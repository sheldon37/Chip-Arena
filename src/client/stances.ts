import type {SpecId} from '../shared/config.js';
/** Per-spec standing stance table (presentation only). Locomotion reads it at run ~0 (idle feet, crouch, pelvis yaw, lean);
 * melee-guard reads the melee entries for its feet. Stance changes step with a lifted foot; they never slide.
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
const stance=(lead:[number,number,number],rear:[number,number,number],crouch:number,pelvisYaw:number,lean:number,kneeSoft=0):Readonly<Stance>=>Object.freeze({lead:Object.freeze({x:lead[0],z:lead[1],toeOut:lead[2]}),rear:Object.freeze({x:rear[0],z:rear[1],toeOut:rear[2]}),leadLeft:true,crouch,pelvisYaw,kneeSoft,lean});
/** Melee: combat-ready stagger (lead toe-out ~10 deg, rear ~25 deg), soft knees, hips turned slightly off the lead side. */
const MELEE=stance([.16,.09,.17],[-.17,-.08,.44],-.03,-.12,.03);
/** Magic: planted stagger with the staff side back. Pistol: shoulder width with a slight lean. */
const MAGIC=stance([.15,.08,.14],[-.16,-.07,.36],-.02,-.08,.02);
const table:Record<SpecId,Readonly<Stance>>={
 emperor:NEUTRAL_STANCE,ansatsuken:NEUTRAL_STANCE,leet:MAGIC,
 pistol:stance([.15,.04,.12],[-.15,-.03,.2],-.015,-.05,.035),
 // Rifle: bladed stance behind the gun; the pelvis turn is scaled by the aim weight (the chest stays on aim).
 rifle:stance([.09,.12,.12],[-.10,-.10,.5],-.025,-.35,.04),
 // Machine gun: wide and braced, knees soft, leaning into the weight.
 machinegun:stance([.19,.10,.2],[-.19,-.04,.4],-.03,-.1,.08,.15),
 fire:MAGIC,water:MAGIC,lightning:MAGIC,darkness:MAGIC,
 sword:MELEE,shield:MELEE,
 // Daggers (owner reference): a low, wide crouch, lead foot well forward and the rear heel turned out, ready to spring.
 daggers:stance([.19,.13,.2],[-.2,-.12,.5],-.09,-.15,.07,.12),
 greatsword:stance([.19,.12,.2],[-.19,-.11,.5],-.045,-.14,.05),
};
export const STANCES:Readonly<Record<SpecId,Readonly<Stance>>>=Object.freeze(table);
export function stanceFor(spec:string):Readonly<Stance>{return STANCES[spec as SpecId]??NEUTRAL_STANCE;}
