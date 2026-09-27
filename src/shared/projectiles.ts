import {Vec,add,mul,dot,len,norm,clamp,cross,v} from './config.js';
/** Rotate by a bounded angle without changing projectile speed. */
export function steerVelocity(velocity:Vec,toward:Vec,maxRadians:number):Vec {
 const speed=len(velocity);if(speed===0||len(toward)===0||maxRadians<=0)return velocity;
 const a=norm(velocity),b=norm(toward),angle=Math.acos(clamp(dot(a,b),-1,1));
 if(angle<=maxRadians)return mul(b,speed);
 let axis=cross(a,b);if(len(axis)<1e-7)axis=cross(a,Math.abs(a.y)<.9?v(0,1,0):v(1,0,0));axis=norm(axis);
 return mul(norm(add(mul(a,Math.cos(maxRadians)),mul(cross(axis,a),Math.sin(maxRadians)))),speed);
}

/** Constant gravity over one fixed tick; collision sweeps follow this short chord. */
export function ballisticStep(velocity:Vec,gravity:number,dt:number){
 return {displacement:add(mul(velocity,dt),v(0,-.5*gravity*dt*dt,0)),velocity:add(velocity,v(0,-gravity*dt,0))};
}
/** Low ballistic arc used only to produce bot/test-pilot aim intentions. */
export function ballisticPitch(distance:number,height:number,speed:number,gravity:number):number{
 if(distance<.001||gravity<=0||speed<=0)return Math.atan2(height,distance);
 const squared=speed*speed,discriminant=squared*squared-gravity*(gravity*distance*distance+2*height*squared);
 return discriminant<0?Math.PI/4:Math.atan((squared-Math.sqrt(discriminant))/(gravity*distance));
}
