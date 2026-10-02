import type {Snapshot,DeathPresentation} from '../shared/simulation.js';
import {clamp,type Vec} from '../shared/config.js';

export const KILLCAM={before:3,after:.6,history:5,hz:20,maxFrames:112,maxEntities:100_000,waitMs:1000} as const;
export interface KillcamInfo {
 deathId:string;victimId:string;killerId:string|null;killerName:string|null;cause:string;
 start:number;end:number;time:number;progress:number;viewId:string;
 /** Recorded fatal-hit time, inside [start,end]; lets the UI mark the moment of death on the timeline. */
 at:number;
}
type Frame={state:Snapshot;entities:number};
type Pending={death:DeathPresentation;victimId:string;deadline:number};
type Clip={frames:Snapshot[];info:KillcamInfo;started:number};
const epoch=(s:Snapshot,id:string)=>`${id}:${s.generation}:${s.setNumber}:${s.round}:${s.options.map}`;
const allowed=(s:Snapshot)=>['fight','result','match'].includes(s.phase)&&!s.lobbyReturn;

/** Read-only client history. Never steps physics, sends inputs, or replaces the live authority snapshot. */
export class Killcam {
 private frames:Frame[]=[];private entities=0;private key='';private tick=-1;
 private alive=false;private seen='';private pending?:Pending;private clip?:Clip;
 get active(){return !!this.clip;}
 get waiting(){return !!this.pending;}
 get info():Readonly<KillcamInfo>|undefined{return this.clip?.info;}
 get bufferedFrames(){return this.frames.length;}
 get bufferedEntities(){return this.entities;}
 clear(){this.frames=[];this.entities=0;this.key='';this.tick=-1;this.alive=false;this.seen='';this.skip();}
 skip(){this.pending=undefined;this.clip=undefined;}

 /** Call for accepted authoritative snapshots, including every local simulation tick. */
 observe(s:Snapshot,id:string,now=performance.now()){
  const key=epoch(s,id);if(this.key!==key){this.clear();this.key=key;}
  const self=s.players.find(p=>p.id===id);
  if(!allowed(s)||!self?.connected){this.clear();return;}
  if(s.tick<=this.tick)return;this.tick=s.tick;
  const death=self.hp<=0?self.death:undefined;
  const fresh=!!death&&death.id!==this.seen;
  if(self.hp>0)this.skip();
  if(fresh){
   this.seen=death!.id;
   // Joining as an already dead spectator must not replay a death we never witnessed.
   if(this.alive&&this.frames.length&&s.time-this.frames[0].state.time>=.2)
    this.pending={death:structuredClone(death!),victimId:id,deadline:now+KILLCAM.waitMs};
  }
  this.alive=self.hp>0;
  const last=this.frames.at(-1)?.state;
  if(!last||fresh||s.time-last.time>=1/KILLCAM.hz-1e-6||last.phase!==s.phase){
   const entities=s.players.length+s.props.length+s.projectiles.length+s.events.length;
   this.frames.push({state:structuredClone(s),entities});this.entities+=entities;
   while(this.frames.length>1&&(this.frames.length>KILLCAM.maxFrames||this.entities>KILLCAM.maxEntities||s.time-this.frames[0].state.time>KILLCAM.history))
    this.entities-=this.frames.shift()!.entities;
  }
 }

 /** A wall-clock deadline finishes even when packets stop; a new round always wins over playback. */
 sample(live:Snapshot,id:string,now=performance.now()):Snapshot|undefined{
  const self=live.players.find(p=>p.id===id);
  if(this.key!==epoch(live,id)||!allowed(live)||!self?.connected||self.hp>0){this.skip();return;}
  if(this.pending&&(live.time>=this.pending.death.at+KILLCAM.after||now>=this.pending.deadline)){
   const {death,victimId}=this.pending;this.pending=undefined;
   const start=Math.max(this.frames[0]?.state.time??death.at,death.at-KILLCAM.before);
   const end=Math.min(this.frames.at(-1)?.state.time??death.at,death.at+KILLCAM.after);
   const frames=this.frames.map(f=>f.state).filter((s,i,a)=>s.time<=end&&(s.time>=start||a[i+1]?.time>start));
   if(frames.length>1&&end-start>=.2&&frames.some(s=>s.players.some(p=>p.id===id&&p.death?.id===death.id))){
    const killerId=death.source&&death.source!==id?death.source:null;
    this.clip={frames,started:now,info:{deathId:death.id,victimId,killerId,killerName:killerId?death.announcement?.killer??null:null,cause:death.kind,at:Math.min(end,Math.max(start,death.at)),start,end,time:start,progress:0,viewId:killerId||victimId}};
   }
  }
  const clip=this.clip;if(!clip)return;
  const t=clip.info.start+Math.max(0,(now-clip.started)/1000);
  if(t>clip.info.end){this.clip=undefined;return;}
  const frame=sampleReplay(clip.frames,t);
  // The recorded victim is a stable fallback for environmental deaths and missing killers.
  clip.info.viewId=frame.players.some(p=>p.id===clip.info.killerId)?clip.info.killerId!:clip.info.victimId;
  clip.info.time=t;clip.info.progress=clamp((t-clip.info.start)/(clip.info.end-clip.info.start),0,1);
  return frame;
 }
}

/** Only transforms interpolate. HP, attacks, deaths, props and projectile membership come from the past frame. */
export function sampleReplay(frames:readonly Snapshot[],time:number):Snapshot {
 let a=frames[0],b=a;
 for(let i=1;i<frames.length;i++){if(frames[i].time>time){b=frames[i];break;}a=b=frames[i];}
 const players=new Map(b.players.map(p=>[p.id,p])),props=new Map(b.props.map(p=>[p.id,p])),projectiles=new Map(b.projectiles.map(p=>[p.id,p]));
 const f=clamp((time-a.time)/Math.max(1e-6,b.time-a.time),0,1);
 const mix=(x:Vec,y:Vec):Vec=>({x:x.x+(y.x-x.x)*f,y:x.y+(y.y-x.y)*f,z:x.z+(y.z-x.z)*f});
 const angle=(x:number,y:number)=>x+Math.atan2(Math.sin(y-x),Math.cos(y-x))*f;
 return {...a,time,players:a.players.map(p=>{const next=players.get(p.id);
  return next&&(p.hp>0)===(next.hp>0)?{...p,animationTime:time,pos:mix(p.pos,next.pos),yaw:angle(p.yaw,next.yaw),pitch:p.pitch+(next.pitch-p.pitch)*f}:{...p,animationTime:time};}),
  props:a.props.map(p=>{const next=props.get(p.id);if(!p.dynamic||!next)return p;
   const x=p.rot,y=next.rot,sign=x.x*y.x+x.y*y.y+x.z*y.z+x.w*y.w<0?-1:1;
   const q={x:x.x+(sign*y.x-x.x)*f,y:x.y+(sign*y.y-x.y)*f,z:x.z+(sign*y.z-x.z)*f,w:x.w+(sign*y.w-x.w)*f};
   const n=Math.hypot(q.x,q.y,q.z,q.w)||1;
   return {...p,pos:mix(p.pos,next.pos),rot:{x:q.x/n,y:q.y/n,z:q.z/n,w:q.w/n}};}),
  projectiles:a.projectiles.map(p=>{const next=projectiles.get(p.id);return next?{...p,pos:mix(p.pos,next.pos),vel:mix(p.vel,next.vel)}:p;}),
  events:a.events.filter(e=>e.time<=time)};
}
