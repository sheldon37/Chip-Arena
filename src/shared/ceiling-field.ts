import {ARENA} from './arena.js';
import {Vec,v,add,mul,len} from './config.js';
/** Projectile-only field: comfortably above the tallest roof; no player collision. */
export const CEILING_FIELD={height:24,halfWidth:ARENA.width/2,halfDepth:ARENA.depth/2} as const;
export function ceilingContact(pos:Vec,displacement:Vec,radius:number){
 const y=CEILING_FIELD.height-radius;
 if(displacement.y<=0&&pos.y<y)return null;
 const fraction=pos.y>=y?0:(y-pos.y)/displacement.y;
 if(!Number.isFinite(fraction)||fraction<0||fraction>1)return null;
 const center=add(pos,mul(displacement,fraction));
 if(Math.abs(center.x)>CEILING_FIELD.halfWidth+radius||Math.abs(center.z)>CEILING_FIELD.halfDepth+radius)return null;
 return {distance:len(displacement)*fraction,center:v(center.x,y-.015,center.z),point:v(center.x,CEILING_FIELD.height,center.z),fraction};
}
