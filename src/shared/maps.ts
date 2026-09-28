import type {ArenaBlock} from './arena.js';
import {v,Material} from './config.js';
export const MAPS=[
 {id:'foundry',name:'Shatter Foundry',tag:'Breakable cover · raised flanks',theme:'industrial',sky:0x060810,floor:0x26333e,accent:0xff7848,music:null},
 {id:'moonlit',name:'Moonlit Wilderness',tag:'Flat dandelion field · ruined castle',theme:'moonlit',sky:0x080e23,floor:0x1b2930,accent:0xb4d9ff,music:'moonlit-wilderness'},
 {id:'neon',name:'Neon District',tag:'Shopfronts · cross streets · rooftop steps',theme:'city',sky:0x110b20,floor:0x252235,accent:0xfa62df,music:'violet-avenue'},
 {id:'freight',name:'Freight Terminal',tag:'Container lanes · loading bays',theme:'freight',sky:0x08161d,floor:0x23363a,accent:0x51e5ca,music:'cargo-pulse'},
 {id:'rooftops',name:'Skyline Exchange',tag:'Rooftop structures · aerial walkways',theme:'rooftops',sky:0x0b132c,floor:0x2c3247,accent:0x9da6ff,music:'skyline-run'},
 {id:'reactor',name:'Reactor Nine',tag:'Reactor pylons · control buildings',theme:'reactor',sky:0x071a18,floor:0x253b35,accent:0x70ffc2,music:'isotope'},
 {id:'outpost',name:'Dusk Outpost',tag:'Desert barracks · watch posts',theme:'desert',sky:0x251720,floor:0x675047,accent:0xffb879,music:'dust-signal'},
 {id:'citadel',name:'Fallen Citadel',tag:'Ruined halls · arches · broken towers',theme:'castle',sky:0x111025,floor:0x373142,accent:0xbe92ff,music:'stone-oath'},
 {id:'frozen',name:'Frostline Depot',tag:'Snowbound warehouses · supply cover',theme:'snow',sky:0x0c1c30,floor:0x7b8d9f,accent:0x80e4ff,music:'cold-current'},
 {id:'canal',name:'Canal Night Market',tag:'Market stalls · canal bridges',theme:'canal',sky:0x111a27,floor:0x38414d,accent:0xffd389,music:'lantern-drift'},
 {id:'forge',name:'Obsidian Forge',tag:'Smelter halls · basalt cover',theme:'forge',sky:0x210b14,floor:0x31262d,accent:0xff654c,music:'cinder-engine'},
] as const;
export type MapId=typeof MAPS[number]['id'];
export function mapId(raw:unknown):MapId{return MAPS.some(m=>m.id===raw)?raw as MapId:'foundry';}
export function arenaMap(raw:unknown){return MAPS.find(m=>m.id===mapId(raw))!;}
/** Authoritative, deterministic layouts with eight clear perimeter spawn pads. */
export function expansionBlocks(id:Exclude<MapId,'foundry'>):ArenaBlock[]{
 const out:ArenaBlock[]=[];let serial=0;
 const b=(x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material='stone',extra:Partial<ArenaBlock>={})=>{const key=`${id}-${++serial}`;out.push({id:key,pos:v(x,y,z),size:v(sx,sy,sz),material,...extra});return key;};
 b(0,-.75,0,64,1.5,56,'foundation');
 const crate=(x:number,z:number,y=.65)=>b(x,y,z,1.3,1.3,1.3,'wood',{dynamic:true,mass:10});
 const step=(x:number,z:number,width=4,height=1.6)=>{const count=Math.ceil(height/.18);for(let n=0;n<count;n++)b(x,(n+1)*height/(count*2),z+n*.26,width,(n+1)*height/count,.28,'foundation');};
 const building=(x:number,z:number,w:number,d:number,h:number,material:Material='stone')=>{b(x-w/2,h/2,z,.65,h,d,material);b(x+w/2,h/2,z,.65,h,d,material);for(const s of [-1,1]){for(const side of [-1,1])b(x+side*(w/4+1),h/2,z+s*d/2,Math.max(.7,w/2-2),h,.65,material);b(x,h-.45,z+s*d/2,4,.9,.65,material);}b(x,h+.2,z,w+1,.4,d+1,'foundation');};
 const column=(x:number,z:number,h=3)=>b(x,h/2,z,1.4,h,1.4);
 const arch=(x:number,z:number,w=7)=>{const l=b(x-w/2,1.6,z,.9,3.2,1),r=b(x+w/2,1.6,z,.9,3.2,1);b(x,3.5,z,w+1,.6,1,'stone',{mass:100,support:[l,r]});};
 for(const s of [-1,1])for(const x of [-13,13])b(x,.4,s*27.5,13,.8,.8,'foundation');
 switch(id){
 case 'moonlit':for(const s of [-1,1]){for(const z of [-12,12]){b(s*18,.7,z,3.3,1.4,1.2);b(s*25,.5,z+s*3,2,1,2);}b(s*8,.5,s*14,3,1,1);}break;
 case 'neon':for(const x of [-13,13])for(const z of [-10,10])building(x,z,8,7,3.2);for(const s of [-1,1]){step(s*13,s*10-7.5,4,3.4);for(const z of [-4,4])crate(s*6,z);b(s*26,.7,s*14,4,1.4,2,'metal');}b(0,.6,0,3,1.2,3,'metal');break;
 case 'freight':for(const s of [-1,1]){building(s*18,s*12,8,10,3.3,'metal');for(const z of [-12,0,12]){b(s*8,1.35,z,4,2.7,6,'metal');crate(s*11,z+4);}step(s*18,s*12-9,4,3.5);}for(const x of [-3,3])for(const z of [-5,5])crate(x,z);break;
 case 'rooftops':for(const s of [-1,1]){building(s*15,0,7,12,3.3);step(s*15,-10,4,3.5);b(s*8,3.5,0,7,.45,3,'foundation');b(s*15,4.2,3,2,1.4,2,'metal');for(const z of [-14,14])b(s*10,.9,z,3,1.8,2,'metal');}b(0,3.5,0,9,.45,3,'foundation');for(const z of [-8,8])for(const x of [-3,3])crate(x,z);break;
 case 'reactor':for(const x of [-7,7])for(const z of [-7,7]){b(x,2,z,2.8,4,2.8,'metal');b(x,4.15,z,3.4,.3,3.4,'foundation');}for(const s of [-1,1]){building(s*20,s*12,7,7,3.3,'metal');for(const z of [-3,3])b(s*15,.8,z,3,1.6,1,'metal');crate(s*4,s*15);}arch(0,0,8);break;
 case 'outpost':for(const x of [-15,15])for(const z of [-10,10])building(x,z,8,7,2.8);for(const s of [-1,1]){step(s*15,-17,3.5,3);b(s*6,.8,s*7,4,1.6,1);for(const z of [-3,3])crate(s*25,z);}for(const z of [-9,9])b(0,.65,z,3,1.3,1.1);break;
 case 'citadel':for(const s of [-1,1]){building(s*18,0,7,12,4);arch(0,s*9,9);for(const z of [-13,13]){column(s*10,z,4);b(s*18,.8,z,5,1.6,1.2);}step(s*18,-10,4,4.2);}for(const x of [-4,4])for(const z of [-3,3])column(x,z,2);break;
 case 'frozen':for(const s of [-1,1]){building(s*16,s*10,10,9,3.1,'metal');b(s*6,1,s*9,2,2,4);for(const z of [-4,4])crate(s*12,z);step(s*16,s*10-8.5,4,3.3);}for(const x of [-3,3])b(x,.8,0,2,1.6,3);break;
 case 'canal':for(const s of [-1,1])for(const z of [-12,0,12]){building(s*15,z,5,5,2.7,'wood');crate(s*19,z+3.5);}for(const z of [-13,0,13]){b(0,.18,z,8,.36,4,'foundation');for(const side of [-1,1])b(0,.6,z+side*2,8,.65,.35,'wood');}break;
 case 'forge':for(const s of [-1,1]){building(s*18,0,8,10,3.6,'metal');step(s*18,-9,4,3.8);for(const z of [-12,12]){b(s*8,1.2,z,4,2.4,3);crate(s*13,z+3);}}for(const x of [-5,5])column(x,0,3.6);arch(0,0,9);break;
 }
 return out;
}
