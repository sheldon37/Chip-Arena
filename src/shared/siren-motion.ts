import type {SirenMoveState} from './siren-contract.js';
import {SIREN_TIMING as T} from './siren-contract.js';
import {SIREN_COMBAT as B} from './siren-content.js';
import {mul,v} from './config.js';
/** Shared capsule movement for authority and snapshot replay; visuals own the hover. */
export function sirenMotion(move:SirenMoveState|undefined,time:number,dt:number){
 if(!move||time>=move.until)return {locked:false,step:v()};
 const dash=(move.id==='rush'||move.id==='laugh'&&!move.caught)&&move.dir&&move.endAt&&time>=move.hitAt&&time<move.endAt;
 const distance=move.id==='rush'?B.rush.distance:B.laugh.range,seconds=move.id==='rush'?T.rush.dash:T.laugh.lunge;
 return {locked:true,step:dash?mul(move.dir!,distance/seconds*Math.min(1,(move.endAt!-time)/dt)):v()};
}
