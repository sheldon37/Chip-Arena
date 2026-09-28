import {verticalBlocks} from './vertical-arena.js';
import {mapId,expansionBlocks} from './maps.js';
import {Material,Vec,v} from './config.js';
export const ARENA={width:64,depth:56,spawnZ:21,edgeMargin:2.5} as const;
export interface ArenaBlock {id:string;pos:Vec;size:Vec;material:Material;dynamic?:boolean;mass?:number;support?:string[];rotation?:{x:number;y:number;z:number;w:number};}
export function arenaBlocks(map:unknown='foundry'):ArenaBlock[]{const key=mapId(map);if(key!=='foundry')return [...expansionBlocks(key),...verticalBlocks(key)];const a:ArenaBlock[]=[];const block=(id:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material,extra:Partial<ArenaBlock>={})=>a.push({id,pos:v(x,y,z),size:v(sx,sy,sz),material,...extra});
 block('foundation',0,-.75,0,ARENA.width,1.5,ARENA.depth,'foundation');
 // Low perimeter walls leave deliberate, readable open ends for ring-outs.
 for(const x of [-31.5,31.5]){block(`rail${x}a`,x,.55,-17,1,1.1,18,'foundation');block(`rail${x}b`,x,.55,17,1,1.1,18,'foundation');}
 for(const z of [-27.5,27.5])for(const x of [-20,20])block(`end${z}${x}`,x,.45,z,18,.9,1,'foundation');
 for(const side of [-1,1]){
 block(`ledge${side}`,side*9,.65,0,5,1.3,7,'foundation');
 // Stepped shallow ramps: 0.13m steps are traversable with the jump/controller.
 for(let i=0;i<6;i++)block(`ramp${side}${i}`,side*9,(i+1)*.1,-5.7+i*.43,4.8,(i+1)*.2,.46,'foundation');
 block(`stone${side}`,side*5,1,side*5,2,2,2,'stone');
 for(let i=0;i<3;i++)block(`crate${side}${i}`,side*(3+i*1.6),.55,side*1.8,1.1,1.1,1.1,'wood',{dynamic:true,mass:8});
 block(`metal${side}`,side*8,1.85,1.5,1.1,1.1,1.1,'metal',{dynamic:true,mass:44});
 }
 for(let x=-2;x<=2;x++)for(let y=0;y<2;y++)block(`wall${x}${y}`,x*1.1,.6+y*1.15,0,1.06,1.14,.6,'wood');
 // A pair of explicit supports suspends a heavy bridge beam.
 block('supportL',-2,1.3,6, .8,2.6,.8,'wood',{mass:16});
 block('supportR',2,1.3,6,.8,2.6,.8,'wood',{mass:16});
 block('bridge',0,2.95,6,6,.7,1.5,'stone',{mass:110,support:['supportL','supportR']});
 // Outer approaches: long east/west lanes with staggered, breakable cover.
 for(const side of [-1,1]){
  for(const z of [-17,-7,7,17]){
   block(`outer-stone${side}-${z}`,side*(Math.abs(z)===17?12:23),1.1,z,3.2,2.2,1.6,'stone');
   block(`outer-crate${side}-${z}`,side*16,.6,z+side*2,1.2,1.2,1.2,'wood',{dynamic:true,mass:10});
  }
  for(const z of [-12,12])for(let n=0;n<3;n++)block(`flank-cover${side}-${z}-${n}`,side*7+n*1.1,.9,z,1.05,1.8,.6,'wood');
  block(`sniper-deck${side}`,side*25,.6,side*16,6,1.2,7,'foundation');
  for(let n=0;n<6;n++)block(`outer-step${side}-${n}`,side*25,(n+1)*.1,side*16-5.4+n*.38,5.5,(n+1)*.2,.40,'foundation');
  block(`deck-cover${side}`,side*25,2.05,side*17.5,2,1.7,.8,'metal',{mass:60});
  block(`home-cover${side}`,side*4,1.05,side*21,2.6,2.1,1.2,'stone');
 }
 return [...a,...verticalBlocks(key)];}

/** Safe clear perimeter positions; first two preserve the original duel layout. */
export function playerSpawn(index:number,sandbox=false):Vec {
 if(sandbox&&index<2)return v(0,1.1,index?-1:-8);
 const points=[[0,-21],[0,21],[-23,0],[23,0],[-20,-22],[20,22],[20,-22],[-20,22]];
 const [x,z]=points[index%points.length];return v(x,1.1,z);
}
