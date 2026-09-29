import type {Snapshot,GameEvent} from '../shared/simulation.js';
import type {ElementId} from '../shared/elements.js';
import {sameTeam} from '../shared/teams.js';
export function enemyHit(event:GameEvent,state:Snapshot,me:string){const source=state.players.find(p=>p.id===me),target=state.players.find(p=>p.id===event.target);return event.type==='hit'&&event.source===me&&event.target!==me&&!!target&&(event.amount||0)>0&&!sameTeam(source,target,state.options.gameMode);}
export class HitmarkerTrack {
 last=0;time=-1;generation=-1;at=-10;headshot=false;dot=false;elements:ElementId[]=[];elementAt=-10;
 clear(){this.last=0;this.time=-1;this.generation=-1;this.at=this.elementAt=-10;this.headshot=this.dot=false;this.elements=[];}
 update(state:Snapshot,me:string){if(state.time<this.time||this.generation!==(state.generation||0))this.clear();this.time=state.time;this.generation=state.generation||0;
  for(const e of state.events){if(e.id<=this.last)continue;this.last=e.id;if(state.time-e.time>.4||!enemyHit(e,state,me))continue;
   const dot=e.message==='burn'||e.message==='poison';if(!dot||state.time-this.at>.22){this.at=e.time;this.headshot=!dot&&!!e.headshot;this.dot=dot;}
   const elements=e.elements||(dot?[e.message==='burn'?'fire':'poison'] as ElementId[]:[]);
   if(elements.length){this.elements=[...new Set([...this.elements.filter(()=>state.time-this.elementAt<.5),...elements])];this.elementAt=e.time;}
  }
  return {visible:state.time-this.at<(this.dot?.20:.34),age:state.time-this.at,headshot:this.headshot,dot:this.dot,elements:state.time-this.elementAt<.7?this.elements:[]};
 }
}
const flame='<path d="M12 2c1 6-6 6-3 11 2 1 3-2 3-4 5 4 6 7 4 10-4 5-12 2-12-3 0-5 6-7 8-14Z"/>';
const poison='<path d="M8 3h8m-6 0v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3M8 15h8M10 18h.01M14 17h.01"/>';
export class Hitmarker {
 readonly track=new HitmarkerTrack();private root:HTMLDivElement;
 constructor(){this.root=document.createElement('div');this.root.className='combat-hitmarker';this.root.setAttribute('aria-hidden','true');this.root.innerHTML='<svg class="hitmark-cross" viewBox="0 0 100 100" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="square"><path d="m34 34-17-17m49 17 17-17M34 66 17 83m49-17 17 17"/><path class="hitmark-tips" d="m11 11-3-3m81 3 3-3M11 89l-3 3m81-3 3 3"/></svg><div class="hitmark-elements"></div>';this.root.hidden=true;document.body.append(this.root);}
 update(state:Snapshot,me:string,visible:boolean){if(!visible){this.root.hidden=true;this.track.clear();return;}const m=this.track.update(state,me),cross=this.root.querySelector<SVGElement>('.hitmark-cross')!;this.root.hidden=!m.visible&&!m.elements.length;
  this.root.classList.toggle('headshot',m.headshot);this.root.classList.toggle('dot-tick',m.dot);cross.style.opacity=m.visible?String(Math.min(1,((m.dot?.20:.34)-m.age)/.16)):'0';cross.style.transform=`scale(${m.dot?.68:1+Math.max(0,.08-m.age)*2.1})`;
  const e=this.root.querySelector<HTMLElement>('.hitmark-elements')!,markup=m.elements.map(element=>`<svg class="hitmark-${element}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round">${element==='fire'?flame:poison}</svg>`).join('');if(e.innerHTML!==markup)e.innerHTML=markup;
 }
}
