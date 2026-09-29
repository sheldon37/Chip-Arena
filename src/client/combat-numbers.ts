import {Vector3,type Camera} from 'three';
import type {Snapshot} from '../shared/simulation.js';
import type {Vec} from '../shared/config.js';
export type CombatNumber={id:number;target:string;heal:boolean;amount:number;at:number;pos:Vec;lane:number};
/** Consume authoritative health events once. Tiny regen/DoT ticks share a short burst. */
export class CombatNumberTrack{
 numbers:CombatNumber[]=[];private lastEvent=0;private generation=-1;private lastTime=-1;
 clear(){this.numbers=[];this.lastEvent=0;this.generation=-1;this.lastTime=-1;}
 update(state:Snapshot){
  if(state.time<this.lastTime||this.generation!==(state.generation||0))this.clear();
  this.generation=state.generation||0;this.lastTime=state.time;
  for(const event of state.events){if(event.id<=this.lastEvent)continue;this.lastEvent=event.id;
   if(!['hit','heal'].includes(event.type)||!event.target||!Number.isFinite(event.amount)||event.amount!<=0||state.time-event.time>1.15)continue;
   const heal=event.type==='heal',old=[...this.numbers].reverse().find(n=>n.target===event.target&&n.heal===heal&&event.time-n.at>=0&&event.time-n.at<.16);
   if(old){old.amount+=event.amount!;continue;}
   const target=state.players.find(p=>p.id===event.target),lane=this.numbers.filter(n=>n.target===event.target).length%3;
   this.numbers.push({id:event.id,target:event.target,heal,amount:event.amount!,at:event.time,pos:target?{x:target.pos.x,y:target.pos.y+1.15,z:target.pos.z}:{...event.pos,y:event.pos.y+.35},lane});
  }
  this.numbers=this.numbers.filter(n=>state.time-n.at<1.15).slice(-64);return this.numbers;
 }
}
export function numberLabel(n:Pick<CombatNumber,'amount'|'heal'>){const value=n.amount>=1?String(Math.round(n.amount)):n.amount.toFixed(n.amount<.1?2:1);return (n.heal?'+':'−')+value;}
export class FloatingCombatNumbers{
 readonly track=new CombatNumberTrack();constructor(private root:HTMLElement){}
 update(state:Snapshot,camera:Camera,visible:boolean,localId:string,firstPerson:boolean){
  if(!visible){this.root.replaceChildren();this.track.clear();return;}
  const numbers=this.track.update(state);camera.updateMatrixWorld();
  this.root.innerHTML=numbers.map(n=>{const age=Math.max(0,state.time-n.at),alpha=Math.min(1,(1.15-age)/.48),point=new Vector3(n.pos.x+(n.lane-1)*.20*age,n.pos.y+age*.9,n.pos.z).project(camera);
   // A first-person player cannot see their own head; place their feedback below the crosshair.
   const own=firstPerson&&n.target===localId;if(!own&&(point.z < -1||point.z>1||Math.abs(point.x)>1.1||Math.abs(point.y)>1.1))return '';
   const x=own?50+(n.lane-1)*3:(point.x*.5+.5)*100,y=own?62-age*7:(-.5*point.y+.5)*100;
   return `<span class="combat-number ${n.heal?'healing':'damage'}" style="left:${x.toFixed(2)}%;top:${y.toFixed(2)}%;opacity:${alpha.toFixed(2)};transform:translate(-50%,-50%) scale(${(1+Math.max(0,.16-age)*1.5).toFixed(2)})">${numberLabel(n)}</span>`;
  }).join('');
 }
}
