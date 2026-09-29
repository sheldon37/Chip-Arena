import {dot,sub,add,mul,norm,type Vec} from './config.js';
/** Head bonus is only awarded for a projectile trajectory through the upper head volume.
 * The capsule's curved shoulder is not sufficient. Splash and melee never call this. */
export const HEADSHOT_MULTIPLIER=1.5;
export function projectileHeadshot(target:{pos:Vec;yaw:number;knockdown?:unknown;juggle?:unknown},origin:Vec,velocity:Vec,radius=0){
 if(target.knockdown||target.juggle)return false;
 const direction=norm(velocity),center=add(target.pos,{x:0,y:.64,z:0}),along=dot(sub(center,origin),direction);if(along<0)return false;
 const closest=sub(add(origin,mul(direction,along)),center),padding=Math.min(.06,Math.max(0,radius));
 // Match the mannequin's blank head, with only a small allowance for projectile radius.
 const x=closest.x*Math.cos(target.yaw)-closest.z*Math.sin(target.yaw),z=closest.x*Math.sin(target.yaw)+closest.z*Math.cos(target.yaw);
 return Math.abs(closest.y)<=.17+padding&&Math.abs(x)<=.17+padding&&Math.abs(z)<=.17+padding;
}
