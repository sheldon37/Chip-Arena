import type {ArenaBlock} from './arena.js';
import {v,Material} from './config.js';
/** Permanent climb routes keep upper floors reachable after optional cover collapses. */
export function verticalBlocks(id:string):ArenaBlock[]{
 const out:ArenaBlock[]=[];let n=0;
 const b=(x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material='foundation',extra:Partial<ArenaBlock>={})=>{const key=`vertical-${id}-${++n}`;out.push({id:key,pos:v(x,y,z),size:v(sx,sy,sz),material,...extra});return key;};
 const levels=id==='citadel'||id==='rooftops'?10:8,roof=(levels+1)*1.2,skin:Material=['canal','sawmill'].includes(id)?'wood':['citadel','moonlit','outpost','spikevault'].includes(id)?'stone':'metal';
 for(const side of [-1,1]){
  const x=side*23,z=side*12;
  b(x,roof/2,z,2.6,roof,2.6); // intact service shaft / ruined keep
  for(let i=0;i<levels;i++){
   const face=i%4,h=(i+1)*1.2,dx=[0,3.9,0,-3.9][face],dz=[-3.9,0,3.9,0][face];
   b(x+dx,h-.18,z+dz,3.6,.36,3.6);
  }
  b(x,roof-.2,z,6,.4,6);
  // Roof parapets and facade panels are destructible; the floor remains usable.
  b(x-1.8,roof+.65,z+2.7,2,1.3,.5,skin);b(x+1.8,roof+.65,z-2.7,2,1.3,.5,skin);
  const support=b(x-side*5,3,z,1,6,1,skin,{mass:65});
  b(x-side*5,6.3,z,5,.6,4,skin,{support:[support],mass:100});
 }
 return out;
}
