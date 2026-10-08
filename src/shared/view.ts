import {Vec,add,mul,len,norm,forward,v} from './config.js';
export type CameraMode='first'|'third';
export const VIEW={eyeHeight:.52,thirdDistance:4.4,thirdLift:.55,collisionPadding:.2,firstFov:75,thirdFov:60} as const;
export type ViewRay=(origin:Vec,direction:Vec,distance:number)=>number|null;
/** Both renderer and authority derive camera position from the player's capsule. */
export function cameraPosition(position:Vec,yaw:number,pitch:number,mode:CameraMode,ray?:ViewRay):Vec{
 const eye=add(position,v(0,VIEW.eyeHeight,0));if(mode==='first')return eye;
 const offset=add(mul(forward(yaw,pitch),-VIEW.thirdDistance),v(0,VIEW.thirdLift,0)),distance=len(offset),direction=norm(offset),hit=ray?.(eye,direction,distance);
 return add(eye,mul(direction,hit==null?distance:Math.max(.15,hit-VIEW.collisionPadding)));
}
/** Never fire from the camera; trace the physical socket from inside the capsule. */
export function weaponMuzzle(position:Vec,yaw:number,ray?:ViewRay):Vec{
 const eye=add(position,v(0,VIEW.eyeHeight,0)),offset=add(mul(forward(yaw),.68),mul(forward(yaw-Math.PI/2),.18));
 const direction=norm(offset),distance=len(offset),hit=ray?.(eye,direction,distance);
 return add(eye,mul(direction,hit==null?distance:Math.max(0,hit-.04)));
}
