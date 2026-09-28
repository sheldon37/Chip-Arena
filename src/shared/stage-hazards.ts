import type {Simulation} from './simulation.js';
import {v} from './config.js';
export interface StageHazard {id:string;kind:'saw'|'spikes'|'lava';x:number;y:number;z:number;sx:number;sz:number;active:boolean;warning:boolean;}
/** Shared clock and definitions drive both visuals and authoritative contact damage. */
export function stageHazards(map:unknown,time:number):StageHazard[]{
 if(map==='sawmill')return [-8,8].map((z,i)=>({id:'saw-'+i,kind:'saw',x:Math.sin(time*.65+i*Math.PI)*17,y:1.05,z,sx:2.4,sz:.65,active:true,warning:false}));
 if(map==='spikevault')return [-8,8].flatMap((x,i)=>[-5,5].map((z,j)=>{const cycle=(time+i*1.5+j*.65)%5;return {id:`spikes-${i}-${j}`,kind:'spikes' as const,x,y:0,z,sx:6,sz:5,active:cycle>=3.6,warning:cycle>=2.6&&cycle<3.6};}));
 if(map==='lavaworks')return [-5,5].map((x,i)=>({id:'lava-'+i,kind:'lava',x,y:.02,z:0,sx:4,sz:34,active:true,warning:false}));
 return [];
}
export class StageHazards {
 private hitAt=new Map<string,number>();
 constructor(private sim:Simulation){}
 clear(){this.hitAt.clear();}
 step(){const s=this.sim;if(s.phase!=='fight'||s.time-s.fightStart<2)return;
 for(const h of stageHazards(s.options.map,s.time)){if(!h.active)continue;for(const p of s.players.values()){
  if(p.hp<=0||!p.connected)continue;const pos=p.body.translation(),key=h.id+':'+p.id;
  if(s.time<(this.hitAt.get(key)??-Infinity))continue;
  const touching=h.kind==='saw'?Math.abs(pos.z-h.z)<.62&&Math.hypot(pos.x-h.x,Math.max(0,Math.abs(pos.y-h.y)-.43))<1.62:Math.abs(pos.x-h.x)<h.sx/2+.3&&Math.abs(pos.z-h.z)<h.sz/2+.3&&pos.y-.85<(h.kind==='spikes'?1.05:.22)&&pos.y>-.5;
  if(!touching)continue;
  const lava=h.kind==='lava',damage=lava?8:h.kind==='saw'?22:25,dir=lava?v():v(Math.sign(pos.x-h.x)*.4,.35,Math.sign(pos.z-h.z)||1);
  // These floor hazards explicitly hit downed players; ordinary attacks still cannot.
  const actual=s.damage(p,damage,null,'stage-'+h.kind,dir,lava?0:150,undefined,false,true);
  this.hitAt.set(key,s.time+(lava?.25:.65));if(actual>0)s.emit('hit',pos,{target:p.id,amount:actual,color:lava?0xff6933:0xffbb55,message:h.kind.toUpperCase()});
 }} }
}
