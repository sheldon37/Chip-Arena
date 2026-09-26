import {Vec,add,mul,dot,len,norm,clamp,cross,v} from './config.js';
/** Rotate by a bounded angle without changing projectile speed. */
export function steerVelocity(velocity:Vec,toward:Vec,maxRadians:number):Vec {
 const speed=len(velocity);if(speed===0||len(toward)===0||maxRadians<=0)return velocity;
 const a=norm(velocity),b=norm(toward),angle=Math.acos(clamp(dot(a,b),-1,1));
 if(angle<=maxRadians)return mul(b,speed);
 let axis=cross(a,b);if(len(axis)<1e-7)axis=cross(a,Math.abs(a.y)<.9?v(0,1,0):v(1,0,0));axis=norm(axis);
 return mul(norm(add(mul(a,Math.cos(maxRadians)),mul(cross(axis,a),Math.sin(maxRadians)))),speed);
}
