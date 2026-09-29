import type {ArenaBlock} from './arena.js';
import {v,type Material,type Vec} from './config.js';
export interface InteriorPlan{id:string;x:number;z:number;width:number;depth:number;storeys:number;height:number;material:Material;}
export interface ClimbRoute{id:string;points:Vec[];}
/** Open stairwells on both sides prevent upper slabs from clipping the climb. */
export function interiorBuilding(p:InteriorPlan):{blocks:ArenaBlock[];route:ClimbRoute}{
 const blocks:ArenaBlock[]=[],points:Vec[]=[v(p.x,.98,p.z-p.depth/2-1),v(p.x,.98,p.z-p.depth/2+.5)];let serial=0;
 const block=(kind:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material='foundation',rotation?:ArenaBlock['rotation'])=>blocks.push({id:`${p.id}-${kind}-${++serial}`,pos:v(x,y,z),size:v(sx,sy,sz),material,...(rotation?{rotation}:{} )});
 const wide=p.width>=7.5,stairWidth=wide?2.1:1.9,stairX=p.width/2-stairWidth/2-.18,run=p.depth-2.4,angle=Math.atan2(p.height,run),length=Math.hypot(p.height,run);
 for(const side of [-1,1])for(const end of [-1,1])block('column',p.x+side*(p.width/2-.15),p.storeys*p.height/2,p.z+end*(p.depth/2-.15),.3,p.storeys*p.height,.3);
 for(let level=0;level<p.storeys;level++){
  const base=level*p.height,top=(level+1)*p.height,side=wide&&level%2?-1:1,dir=level%2?-1:1;
  const x=p.x+side*stairX,z0=p.z-dir*run/2,z1=p.z+dir*run/2,theta=-dir*angle;
  block('stair',x,base+p.height/2-.12/Math.cos(angle),p.z,stairWidth,.24,length,'foundation',{x:Math.sin(theta/2),y:0,z:0,w:Math.cos(theta/2)});
  // The central room and end landings leave full-height voids over every flight.
  const roomWidth=wide?p.width-2*(stairWidth+.25):p.width-stairWidth-.4,roomX=wide?p.x:p.x-(stairWidth+.4)/2;
  block('floor',roomX,top-.12,p.z,roomWidth,.24,p.depth);
  for(const end of [-1,1])block('landing',p.x,top-.12,p.z+end*(p.depth/2-.6),p.width,.24,1.2);
  for(const wallSide of [-1,1]){
   block('sill',p.x+wallSide*p.width/2,base+.65,p.z,.3,1.3,p.depth-1.2,p.material);
   block('lintel',p.x+wallSide*p.width/2,top-.28,p.z,.3,.56,p.depth-1.2,p.material);
   for(const end of [-1,1])block('facade',p.x+wallSide*(p.width+3)/4,base+(p.height-.55)/2,p.z+end*p.depth/2,(p.width-3)/2,p.height-.55,.3,p.material);
  }
  const landingZ=z0-dir*.55;points.push(v(p.x,base+.98,landingZ),v(x,base+.98,landingZ),v(x,base+.98,z0));
  for(let n=1;n<=12;n++)points.push(v(x,base+p.height*n/12+.98,z0+(z1-z0)*n/12));
  points.push(v(x,top+.98,z1+dir*.55),v(p.x,top+.98,z1+dir*.55));
 }
 return {blocks,route:{id:p.id,points}};
}
