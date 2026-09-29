import {normalMelee} from '../shared/standard-melee.js';
import type {Snapshot} from '../shared/simulation.js';
import {Vec,v,clamp} from '../shared/config.js';
import {NETWORK as N} from '../shared/network-config.js';
/** Smooth a server-time timeline, not the irregular arrival timestamps. */
export class SnapshotBuffer {
 private samples:{state:Snapshot;at:number}[]=[];private gaps:number[]=[];private epoch='';private cursor:number|null=null;private lastRender=0;
 delayMs:number=N.interpolationMinMs;underruns=0;
 get gapP95(){const sorted=this.gaps.slice().sort((a,b)=>a-b);return sorted[Math.floor((sorted.length-1)*.95)]||0;}
 clear(){this.samples=[];this.gaps=[];this.epoch='';this.cursor=null;this.lastRender=0;this.delayMs=N.interpolationMinMs;this.underruns=0;}
 push(state:Snapshot,at=performance.now()){
  const last=this.samples.at(-1);if(last&&state.tick<=last.state.tick)return false;
  const epoch=`${state.generation||0}:${state.setNumber}:${state.round}:${state.phase}`;
  if(epoch!==this.epoch){this.samples=[];this.cursor=null;this.epoch=epoch;}
  if(last&&at>last.at){this.gaps.push(at-last.at);this.gaps=this.gaps.slice(-40);this.delayMs=clamp(this.gapP95*1.25,N.interpolationMinMs,N.interpolationMaxMs);}
  this.samples.push({state,at});this.samples=this.samples.slice(-40);return true;
 }
 sample(current:Snapshot,localId:string,now=performance.now()):Snapshot {
  if(this.samples.length<2)return current;
  const newest=this.samples.at(-1)!,target=newest.state.time+(now-newest.at-this.delayMs)/1000;
  const dt=this.lastRender?clamp((now-this.lastRender)/1000,0,.05):0;this.lastRender=now;
  if(this.cursor===null)this.cursor=target;else this.cursor+=dt*clamp(1+(target-this.cursor)*3,.8,1.2);
  const maximum=newest.state.time+N.extrapolationMaxMs/1000;
  if(this.cursor>maximum){this.cursor=maximum;this.underruns++;}
  let a=this.samples[0].state,b=newest.state;
  for(let i=1;i<this.samples.length;i++)if(this.samples[i].state.time>=this.cursor){a=this.samples[i-1].state;b=this.samples[i].state;break;}
  const f=clamp((this.cursor-a.time)/Math.max(.00001,b.time-a.time),0,1),extra=clamp(this.cursor-b.time,0,N.extrapolationMaxMs/1000);
  const mix=(x:Vec,y:Vec,velocity?:Vec)=>v(x.x+(y.x-x.x)*f+(velocity?.x||0)*extra,x.y+(y.y-x.y)*f+(velocity?.y||0)*extra,x.z+(y.z-x.z)*f+(velocity?.z||0)*extra);
  const angle=(x:number,y:number)=>x+Math.atan2(Math.sin(y-x),Math.cos(y-x))*f;
  const quaternion=(x:{x:number;y:number;z:number;w:number},y:typeof x)=>{const sign=x.x*y.x+x.y*y.y+x.z*y.z+x.w*y.w<0?-1:1;const q={x:x.x+(sign*y.x-x.x)*f,y:x.y+(sign*y.y-x.y)*f,z:x.z+(sign*y.z-x.z)*f,w:x.w+(sign*y.w-x.w)*f};const length=Math.hypot(q.x,q.y,q.z,q.w)||1;return {x:q.x/length,y:q.y/length,z:q.z/length,w:q.w/length};};
  return {...current,time:this.cursor,
   players:current.players.map(p=>{const pa=a.players.find(x=>x.id===p.id),pb=b.players.find(x=>x.id===p.id);let frame=p.id!==localId&&pa&&pb?{...p,pos:mix(pa.pos,pb.pos,pb.vel),yaw:angle(pa.yaw,pb.yaw),pitch:pa.pitch+(pb.pitch-pa.pitch)*f}:p;
    const roll=p.id===localId?p.roll:pb?.roll&&pb.roll.at<=this.cursor!?pb.roll:pa?.roll;frame={...frame,roll,animationTime:p.id===localId?newest.state.time+clamp((now-newest.at)/1000,0,N.extrapolationMaxMs/1000):this.cursor!};
    if(!normalMelee(p.spec))return frame;
    // Never pair an old render time with a future combo index: it discards the strike we are displaying.
    if(p.id===localId)return {...frame,animationTime:newest.state.time+clamp((now-newest.at)/1000,0,N.extrapolationMaxMs/1000)};
    const action=pb&&pb.lastAttack<=this.cursor!?pb:pa;if(!action||action.spec!==p.spec)return frame;
    return {...frame,animationTime:this.cursor!,attackIndex:action.attackIndex,lastAttack:action.lastAttack,lastSecondary:action.lastSecondary,swingAt:action.swingAt,swingUntil:action.swingUntil,swinging:action.swinging,meleeAction:action.meleeAction,attackCommitment:action.attackCommitment,weaponAway:action.weaponAway};
   }),
   props:current.props.map(p=>{const pa=a.props.find(x=>x.id===p.id),pb=b.props.find(x=>x.id===p.id);return p.dynamic&&pa&&pb?{...p,pos:mix(pa.pos,pb.pos,pb.vel),rot:quaternion(pa.rot,pb.rot)}:p;}),
   projectiles:current.projectiles.map(p=>{const pa=a.projectiles.find(x=>x.id===p.id),pb=b.projectiles.find(x=>x.id===p.id);return pa&&pb?{...p,pos:mix(pa.pos,pb.pos,pb.vel),vel:mix(pa.vel,pb.vel)}:p;})};
 }
}
