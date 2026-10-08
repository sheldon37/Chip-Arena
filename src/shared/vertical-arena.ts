import type {ArenaBlock} from './arena.js';
import type {Material} from './config.js';
import {interiorBuilding,type InteriorPlan} from './architecture.js';
/** Four hollow perimeter buildings with open entrances and protected interior ramps. */
export function verticalPlans(id:string):InteriorPlan[]{
 const material:Material=['canal','sawmill'].includes(id)?'wood':['citadel','moonlit','outpost','spikevault'].includes(id)?'stone':'metal';
 return [-1,1].flatMap(side=>[-1,1].map(end=>({id:`vertical-${id}-${side}-${end}`,x:side*25.5,z:end*12,width:8,depth:10,storeys:side===end?4:3,height:3.6,material})));
}
export function verticalBlocks(id:string):ArenaBlock[]{return verticalPlans(id).flatMap(p=>interiorBuilding(p).blocks);}
/** Reserve entrances as well as walls so old cover never seals a new building. */
export function addVerticalBuildings(blocks:ArenaBlock[],id:string):ArenaBlock[]{const plans=verticalPlans(id);const clear=blocks.filter(b=>b.pos.y+b.size.y/2<=.05||!plans.some(p=>Math.abs(b.pos.x-p.x)<b.size.x/2+p.width/2+.7&&Math.abs(b.pos.z-p.z)<b.size.z/2+p.depth/2+1.2));return [...clear,...verticalBlocks(id)];}
