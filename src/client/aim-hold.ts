// Hold-to-aim on the client (owner 2026-10-04): the E aim preview appears only after the key has been held HOLD_AIM_SECONDS,
// and a roll, block or the other (non-ADS) ability cancels it until E is released, as the host cancels the cast.
import {kitFor,type HostCaps,HOLD_AIM} from '../shared/abilities.js';
import type {SpecId} from '../shared/config.js';
export class AimHoldTracker{
 private at:number|null=null;private canceled=false;
 /** `now` in seconds; `cancel` true while a cancelling input is down. */
 update(now:number,eDown:boolean,cancel:boolean){if(!eDown){this.at=null;this.canceled=false;return;}if(this.at===null)this.at=now;if(cancel)this.canceled=true;}
 heldFor(now:number){return this.at===null||this.canceled?0:Math.max(0,now-this.at);}
 reset(){this.at=null;this.canceled=false;}
}
/** Keys that cancel a held aim: roll/dash (Shift), block (RMB) and Q unless Q is the spec's ADS. */
export function aimCancel(keys:ReadonlySet<string>,block:boolean,spec?:SpecId){const q=spec?kitFor(spec)?.q:undefined;return block||keys.has('ShiftLeft')||keys.has('ShiftRight')||keys.has('KeyQ')&&q?.kind!=='ads';}
/** True when this host casts the spec's E on release (hold-to-aim). */
export const holdAimE=(caps:HostCaps|null|undefined,spec?:SpecId)=>!!caps?.aimHold&&!!spec&&HOLD_AIM.has(kitFor(spec)?.e.id??'');
