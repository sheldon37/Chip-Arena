import {Input,v,norm,forward,mul} from './config.js';
export interface Knockdown {juggleUntil?:number;juggleHits?:number;at:number;until:number;landedAt?:number;pickupUntil?:number;pickupUsed?:boolean;rollAt?:number;rollUntil?:number;rollX?:number;rollZ?:number;electricHits?:number;hits?:number;source?:string;bounced?:boolean;wallUsed?:boolean;}
export const RECOVERY={pickup:.22,rollDelay:.4,rollSeconds:.4,seconds:1,rollSpeed:6} as const;
export function landRecovery(k:Knockdown,time:number){k.landedAt=time;k.pickupUntil=k.pickupUsed?time:time+RECOVERY.pickup;k.rollAt=time+RECOVERY.rollDelay;k.rollUntil=k.rollAt+RECOVERY.rollSeconds;k.until=time+RECOVERY.seconds;}
export function pickupOpen(k:Knockdown|undefined,time:number){return !!k&&k.landedAt!==undefined&&!k.pickupUsed&&time<(k.pickupUntil??0);}
export function downProtected(k:Knockdown|undefined,time:number){return !!k&&k.landedAt!==undefined&&time<k.until&&!pickupOpen(k,time);}
export function recoveryMotion(k:Knockdown|undefined,time:number,input:Input){if(!k||k.rollAt===undefined||time<k.rollAt||time>=(k.rollUntil??0))return undefined;if(k.rollX===undefined){const d=Math.hypot(input.x,input.z)>.1?norm(v(input.x,0,input.z)):mul(forward(input.yaw),-1);k.rollX=d.x;k.rollZ=d.z;}return v(k.rollX*RECOVERY.rollSpeed,0,(k.rollZ||0)*RECOVERY.rollSpeed);}
