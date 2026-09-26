import {Material,Vec,v} from './config.js';
export interface ArenaBlock {id:string;pos:Vec;size:Vec;material:Material;dynamic?:boolean;mass?:number;support?:string[];rotation?:{x:number;y:number;z:number;w:number};}
export function arenaBlocks():ArenaBlock[]{const a:ArenaBlock[]=[];const block=(id:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material,extra:Partial<ArenaBlock>={})=>a.push({id,pos:v(x,y,z),size:v(sx,sy,sz),material,...extra});
 block('foundation',0,-.75,0,27,1.5,25,'foundation');
 // Low perimeter walls leave deliberate, readable open ends for ring-outs.
 for(const x of [-13,13]){block(`rail${x}a`,x,.55,-8,1,1.1,9,'foundation');block(`rail${x}b`,x,.55,8,1,1.1,9,'foundation');}
 for(const z of [-12,12])for(const x of [-9,9])block(`end${z}${x}`,x,.45,z,6,.9,1,'foundation');
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
 return a;}
