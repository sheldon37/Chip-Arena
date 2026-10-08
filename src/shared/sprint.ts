import type {Input} from './config.js';

/** Double-tap W sprint. Cost is per second of actual sprint movement, shared by host and prediction. */
export const SPRINT={speed:1.35,forwardDot:.6,doubleTapMs:300,drain:12} as const;
export interface SprintResource {stamina:number;staminaAt:number;sprintExhausted?:boolean}
/** Exhaustion stays latched until W is released: regeneration cannot produce repeated one-tick sprint bursts. */
export function sprintSpeed(p:SprintResource,input:Pick<Input,'sprint'>){
 if(!input.sprint)p.sprintExhausted=false;
 return p.stamina>1e-6&&!p.sprintExhausted?SPRINT.speed:undefined;
}
/** `regenerated` removes any regeneration earlier in this tick; sprint and recovery never run simultaneously. */
export function spendSprint(p:SprintResource,time:number,dt:number,regenerated=0){
 p.stamina=Math.max(0,p.stamina-regenerated-SPRINT.drain*Math.max(0,dt));p.staminaAt=time;
 if(p.stamina<=1e-6){p.stamina=0;p.sprintExhausted=true;}
}
