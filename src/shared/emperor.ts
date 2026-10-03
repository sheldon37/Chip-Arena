import {downProtected,pickupOpen,landRecovery} from './recovery.js';
import type {Player,Simulation} from './simulation.js';
import {EMPEROR} from './emperor-content.js';
import {canUse} from './access.js';
import {v,Vec,add,sub,mul,len,norm,dot,forward,clamp} from './config.js';
export interface EmperorState {singleHit?:boolean;action?:'jab'|'electric'|'wave';at:number;until:number;index:number;total:number;hits:number;lastHit:number;comboTarget?:string;comboDamage:number;electricAt:number;}
export interface Juggle {pickupUsed?:boolean;electricHits?:number;at:number;until:number;hits:number;lastHit:number;source:string;spinUntil:number;bounced:boolean;wallUsed:boolean;}
export interface EmperorTrail {id:string;owner:string;pos:Vec;at:number;until:number;}
type Action={at:number;gap:number;next:number;total:number;startIndex:number;kind:'jab'|'electric';id:string};
export class EmperorSystem {
 actions=new Map<string,Action>();echoes:{source:string;pos:Vec;at:number;id:string}[]=[];trails:(EmperorTrail&{hit:Set<string>})[]=[];
 constructor(private sim:Simulation){}
 active(p:Player|undefined){return !!p&&p.spec==='emperor'&&canUse(p,'weapon:emperor');}
 state(p:Player):EmperorState{return p.emperor??={at:0,until:0,index:0,total:0,hits:0,lastHit:-10,comboDamage:0,electricAt:-10};}
 cancel(p:Player){p.electricStepTime=0;this.actions.delete(p.id);if(p.emperor){p.emperor.action=undefined;p.emperor.until=0;}}
 clear(){this.actions.clear();this.echoes=[];this.trails=[];for(const p of this.sim.players.values()){p.emperor=undefined;p.juggle=undefined;p.waveTime=0;p.electricStepTime=0;}}
 ready(p:Player){return this.active(p)&&p.hp>0&&p.connected&&!this.sim.leet.frozen(p)&&!this.sim.arcade.stunned(p)&&p.blockTime<=0;}
 waveSpeed(p:Player){return this.ready(p)&&p.cooldown<=0&&!this.actions.has(p.id)?EMPEROR.waveSpeed:undefined;}
 wave(p:Player){const s=this.sim,a=this.state(p);Object.assign(a,{action:'wave',singleHit:false,at:s.time,until:s.time+EMPEROR.waveSeconds});p.lastSecondary=s.time;p.secondaryCd=0;s.emit('emperor-wave',p.body.translation(),{source:p.id,spec:p.spec,color:0xff2846});if(p.chips['mishima-trail']&&this.trails.length<24)this.trails.push({id:s.uid('storm-step'),owner:p.id,pos:{...p.body.translation()},at:s.time,until:s.time+.7,hit:new Set()});}
 start(p:Player,kind:'jab'|'electric'){
  if(!this.ready(p)||p.cooldown>0||this.actions.has(p.id))return false;const s=this.sim,a=this.state(p),rate=p.stats.attackRate*(kind==='electric'&&p.chips['mishima-godfist']?1.4:1),total=kind==='electric'?1:p.chips['mishima-pressure']?3:2,gap=.17/rate;
  // One activation commits one strike; retain the next step for the ready stance's 2.4 s hold.
  const index=kind==='jab'&&a.singleHit&&a.until>a.at&&s.time<=a.until+2.4?(a.index+1)%total:0;
  if(kind==='jab'&&!s.spendResource(p,6/total))return false;
  const first=s.time+(kind==='electric'?EMPEROR.electricHit:.075)/rate,until=kind==='electric'?s.time+EMPEROR.electricRecovery/rate:index===total-1?first+.14/rate:s.time+gap;
  this.actions.set(p.id,{at:first,gap,next:0,startIndex:index,total:kind==='jab'?1:total,kind,id:s.uid('emperor')});Object.assign(a,{action:kind,at:s.time,until,index,total,singleHit:kind==='jab'});p.cooldown=until-s.time;p.lastAttack=s.time;p.attackIndex=index;p.waveTime=0;
  if(kind==='electric'){p.electricStepTime=p.grounded?EMPEROR.electricStepSeconds:0;a.electricAt=s.time;s.emit('emperor-electric',p.body.translation(),{source:p.id,spec:p.spec,color:0xff2846});}return true;
 }
 confirm(p:Player,t:Player,actual:number,electric:boolean,echo=false){const s=this.sim;if(!s.canHit(p.id,t.id)||downProtected(t.knockdown,s.time))return;const a=this.state(p),pickup=pickupOpen(t.knockdown,s.time),k=t.knockdown,air=!!t.juggle||!t.grounded||pickup;
  if(a.comboTarget!==t.id||s.time-a.lastHit>EMPEROR.comboMemory){a.hits=0;a.comboDamage=0;}a.comboTarget=t.id;a.hits++;a.comboDamage+=actual;a.lastHit=s.time;
  if(t.hp>0&&!s.siren.armored(t)){t.hitStunUntil=Math.max(t.hitStunUntil||0,s.time+(electric?.42:.25)/p.stats.attackRate);t.move=v();t.electricStepTime=0;t.dashTime=0;t.waveTime=0;t.blockTime=0;t.guarding=false;t.parryTime=0;t.swing=null;t.cast=undefined;t.burst=0;s.arcade.cancel(t);this.cancel(t);
   if(electric||air){const old=t.juggle?t.juggle:pickup&&k?{at:k.at,hits:k.hits||0,electricHits:k.electricHits||0,spinUntil:0,bounced:!!k.bounced,wallUsed:!!k.wallUsed,pickupUsed:true}:undefined,hits=(old?.hits||0)+1;const spin=!!p.chips['mishima-screw']&&hits%3===0;t.knockdown=undefined;const electricHits=(old?.electricHits||0)+(electric&&!echo?1:0);t.juggle={pickupUsed:pickup||old?.pickupUsed||false,electricHits,at:old?.at??s.time,until:s.time+EMPEROR.juggleSeconds,lastHit:s.time,hits,source:p.id,spinUntil:spin?s.time+.65:old?.spinUntil||0,bounced:old?.bounced||false,wallUsed:old?.wallUsed||false};
    const height=t.body.translation().y-p.body.translation().y,vy=electric?clamp(7.7-height*3.5,1.5,10):clamp(5-height*2,1,6);t.ext.y=vy-t.body.linvel().y;t.grounded=false;
    const dir=forward(p.input.yaw),built=Math.min(EMPEROR.electricPushMax,EMPEROR.electricPush+Math.max(0,electricHits-1)*EMPEROR.electricPushGrowth),push=(built+(p.chips['mishima-wall']?4:0))*p.stats.impulse*t.stats.knockbackTaken;
    t.ext.x=dir.x*push;t.ext.z=dir.z*push;
    if(p.chips['mishima-magnet']){const delta=sub(add(p.body.translation(),mul(dir,1.45)),t.body.translation()),lateral=sub(delta,mul(dir,dot(delta,dir))),steer=mul(norm(v(lateral.x,0,lateral.z)),Math.min(6,Math.hypot(lateral.x,lateral.z)*5));t.ext.x+=steer.x;t.ext.z+=steer.z;}

   }
  }
  if(electric&&p.hp>0&&p.chips['mishima-bloodline']){s.heal(p,5);p.stamina=Math.min(p.stats.maxStamina,p.stamina+12);}
  s.emit('emperor-hit',add(t.body.translation(),v(0,.2,0)),{source:p.id,target:t.id,spec:p.spec,amount:a.hits,impact:'punch',message:echo?'ECHO':electric?'ELECTRIC':air?'JUGGLE':'HIT',radius:electric?1.3:.65,color:0xff304f});
 }
 strike(p:Player,kind:'jab'|'electric',index:number,total:number,id:string){const s=this.sim,a=this.state(p),electric=kind==='electric',pos=add(p.body.translation(),v(0,.18,0)),dir=forward(p.input.yaw),range=(electric?2.4:2)*p.stats.reach;a.index=index;p.lastAttack=s.time;p.attackIndex=index+1;
  for(const t of [...s.players.values(),...s.props.values()]){if(t===p||'stats'in t&&t.hp<=0||'material'in t&&t.material==='foundation')continue;const d=sub(t.body.translation(),pos),horizontal=Math.hypot(d.x,d.z);if(horizontal>range+.35||Math.abs(d.y)>(electric?3.2:2.1)||dot(norm(v(d.x,0,d.z)),dir)<Math.cos(.9*p.stats.meleeArc)||!s.lineOfSight(pos,t.body.translation(),p.id,t.id))continue;
   const player='stats'in t,counter=player&&electric&&p.chips['mishima-counter']&&s.time-t.lastAttack<.4,screw=player&&p.chips['mishima-screw']&&((t.juggle?.hits||0)+1)%3===0,damage=(electric?EMPEROR.electricDamage:index===2?18:index?12:8)*p.stats.damage*(p.chips['mishima-rage']&&p.hp<p.stats.hp/3?1.45:1)*(counter?1.6:1)*(screw?1.25:1),before=t.hp,blocked=player&&t.blockTime>0;
   s.directHit(p,t,add(t.body.translation(),v(0,electric?.4:.25,0)),electric?norm(add(dir,v(0,1.2,0))):dir,damage,(electric?120:65)*p.stats.impulse,id+':'+index);
   if(player&&!blocked&&t.hp<before){this.confirm(p,t,before-t.hp,electric);if(electric&&p.chips['mishima-twin']&&this.echoes.length<32)this.echoes.push({source:p.id,pos:{...t.body.translation()},at:s.time+.13,id:id+':echo'});}
   if(p.hp<=0||s.arcade.stunned(p))return;
  }
  if(p.hp>0)for(const m of [...s.chaos.minions.values()]){const d=sub(m.body.translation(),pos);if(m.owner!==p.id&&len(d)<range+.5&&dot(norm(d),dir)>.2&&s.lineOfSight(pos,m.body.translation(),p.id,m.id))s.lifeOnHit(p,s.chaos.hitMinion(m,(electric?EMPEROR.electricDamage:10)*p.stats.damage,p.id,electric?550:150,dir));}
  s.emit('emperor-strike',pos,{source:p.id,spec:p.spec,message:kind,amount:index,color:0xff2846});
 }
 stepPlayer(p:Player){const s=this.sim;if(!this.ready(p)){this.cancel(p);return;}const a=this.state(p),action=this.actions.get(p.id);if(action){while(action.next<action.total&&s.time>=action.at+action.next*action.gap){this.strike(p,action.kind,action.startIndex+action.next,a.total,action.id);if(this.actions.get(p.id)!==action||!this.ready(p))return;action.next++;}if(s.time>=a.until)this.actions.delete(p.id);}if(a.action&&s.time>=a.until)a.action=undefined;}
 stepJuggle(p:Player){const s=this.sim,j=p.juggle;if(!j)return;if(p.hp<=0||!p.connected){p.juggle=undefined;return;}if(s.time>=j.until)j.until=s.time+.2;const owner=s.players.get(j.source),pos=p.body.translation();
  if(p.grounded&&s.time-j.lastHit>.15&&p.body.linvel().y<.5){if(owner?.hp&&owner.connected&&owner.chips['mishima-bound']&&!j.bounced&&!j.pickupUsed){j.bounced=true;j.pickupUsed=true;j.lastHit=s.time;j.until=s.time+.85;p.ext.y=6.5-p.body.linvel().y;p.grounded=false;s.damage(p,8*owner.stats.damage,owner.id,'mishima-bound');s.emit('emperor-bound',pos,{source:owner.id,target:p.id,color:0xff304f,radius:1.8});}else{p.juggle=undefined;p.knockdown={at:j.at,until:s.time+1,pickupUsed:j.pickupUsed,electricHits:j.electricHits,hits:j.hits,source:j.source,bounced:j.bounced,wallUsed:j.wallUsed};landRecovery(p.knockdown,s.time);}}
  if(p.juggle&&owner?.hp&&owner.chips['mishima-wall']&&!j.wallUsed&&s.time-j.lastHit>.06){const dir=forward(owner.input.yaw),hit=s.ray(pos,dir,.75,p.id),prop=hit?s.props.get(s.colliderOwners.get(hit.collider.handle)||''):undefined;if(prop&&prop.size.y>1){j.wallUsed=true;j.lastHit=s.time;j.until=s.time+.85;p.ext=add(mul(dir,-2.5),v(0,6-p.body.linvel().y,0));s.damage(p,12*owner.stats.damage,owner.id,'mishima-wall');s.emit('emperor-bound',pos,{source:owner.id,target:p.id,color:0xff304f,radius:1.8});}}
 }
 step(){const s=this.sim;for(const e of this.echoes){if(s.time<e.at)continue;const owner=s.players.get(e.source);if(owner?.hp&&owner.connected)for(const t of s.players.values())if(t!==owner&&s.canHit(owner.id,t.id)&&t.hp>0&&len(sub(t.body.translation(),e.pos))<1.6&&s.lineOfSight(e.pos,t.body.translation(),owner.id,t.id)){if(t.blockTime>0){s.successfulBlock(t,'projectile',t.body.translation(),owner.id);continue;}const actual=s.damage(t,EMPEROR.echoDamage*owner.stats.damage,owner.id,'mishima-echo');if(actual>0)this.confirm(owner,t,actual,true,true);}e.at=-1;}this.echoes=this.echoes.filter(e=>e.at>=s.time);
  for(const f of this.trails){if(f.until<=s.time)continue;const owner=s.players.get(f.owner);if(!owner?.hp||!owner.connected)continue;for(const t of s.players.values())if(t.id!==f.owner&&s.canHit(f.owner,t.id)&&t.hp>0&&!f.hit.has(t.id)&&len(sub(t.body.translation(),f.pos))<1.4&&s.lineOfSight(f.pos,t.body.translation(),owner.id,t.id)){f.hit.add(t.id);if(t.blockTime>0){s.successfulBlock(t,'projectile',t.body.translation(),owner.id);continue;}s.damage(t,6*owner.stats.damage,owner.id,'mishima-trail');s.emit('emperor-hit',t.body.translation(),{source:owner.id,target:t.id,color:0xff304f,message:'SHOCK'});}}this.trails=this.trails.filter(f=>f.until>s.time&&s.players.get(f.owner)?.hp);
 }
 views():EmperorTrail[]{return this.trails.map(({hit,...f})=>({...f,pos:{...f.pos}}));}
}
