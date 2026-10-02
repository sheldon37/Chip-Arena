import {forward,type Vec} from '../shared/config.js';
import {VIEW,type ViewRay} from '../shared/view.js';

/** Replay-only camera framing. Presentation: never used for aiming, muzzles or authority. */
export const REPLAY_CAMERA={shoulder:.85,lift:.22} as const;

/**
 * Pivot for the kill-cam third-person camera: moved to the recorded pilot's right shoulder and slightly up, so the
 * pilot's own body does not cover the target directly ahead. The sideways shift is shortened against props, so the
 * camera never starts inside a wall; cameraPosition() then applies its usual backward collision from this pivot.
 */
export function replayShoulder(pos:Vec,yaw:number,ray?:ViewRay):Vec{
 const side=forward(yaw-Math.PI/2),eye={x:pos.x,y:pos.y+VIEW.eyeHeight,z:pos.z};
 const hit=ray?.(eye,side,REPLAY_CAMERA.shoulder+VIEW.collisionPadding);
 const shift=hit==null?REPLAY_CAMERA.shoulder:Math.max(0,hit-VIEW.collisionPadding);
 return {x:pos.x+side.x*shift,y:pos.y+REPLAY_CAMERA.lift,z:pos.z+side.z*shift};
}
