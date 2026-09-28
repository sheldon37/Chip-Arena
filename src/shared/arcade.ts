import type {Player,Projectile,Simulation,Prop} from './simulation.js';
import {CONFIG,v,Vec,add,sub,mul,norm,len,dot,forward,clamp} from './config.js';
import {ARCADE} from './arcade-content.js';
import {canUse} from './access.js';
import {refreshDot} from './elements.js';
export interface ArcadeState {action?:'combo'|'tatsu'|'shoryuken'|'charge'|'hadoken'|'shinku';at:number;until:number;index:number;total:number;charge:number;uppercutCd:number;hadokenCd:number;hits:number;lastHit:number;comboTarget?:string;comboDamage:number;parryUntil:number;parryAt:number;secondWind?:boolean;}
export interface ArcadeShot {super:boolean;red:boolean;}
type Action={kind:'combo'|'tatsu'|'shoryuken';at:number;gap:number;next:number;total:number;id:string};
type Echo={at:number;until:number;source:string;pos:Vec;dir:Vec;radius:number;damage:number;impulse:number;remaining:number;gap:number;id:string;impact:'punch'|'fireball';};
export class ArcadeSystem {
 actions=new Map<string,Action>();echoes:Echo[]=[];private used=new Set<string>();
 constructor(private sim:Simulation){}
 active(p:Player|undefined){return !!p&&p.spec==='ansatsuken'&&canUse(p,'weapon:ansatsuken');}
 state(p:Player):ArcadeState{return p.arcade??={at:0,until:0,index:0,total:0,charge:0,uppercutCd:0,hadokenCd:0,hits:0,lastHit:-10,comboDamage:0,parryUntil:0,parryAt:-10};}
 clear(){this.actions.clear();this.echoes=[];this.used.clear();for(const p of this.sim.players.values())p.arcade=undefined;}
 cancel(p:Player){this.actions.delete(p.id);if(p.arcade){p.arcade.action=undefined;p.arcade.charge=0;p.arcade.until=0;}}
 idle(p:Player){return p.hp>0&&p.connected&&!this.sim.leet.frozen(p)&&!this.stunned(p)&&p.blockTime<=0;}
 start(p:Player,kind:Action['kind']){
  if(!this.active(p)||!this.idle(p))return false;const a=this.state(p),now=this.sim.time;
  const canCancel=kind!=='combo'&&p.chips['arcade-cancel']&&a.action==='combo'&&a.lastHit>=a.at&&now-a.lastHit<.5;
  if((p.cooldown>0||this.actions.has(p.id)||a.action==='charge')&&!canCancel)return false;
  if(kind==='tatsu'&&p.secondaryCd>0||kind==='shoryuken'&&a.uppercutCd>0)return false;
  const cost=kind==='combo'?(p.chips['arcade-rush']?22:12):kind==='tatsu'?22:25;if(!this.sim.spendResource(p,cost))return false;
  if(kind==='combo')this.sim.chaos.meleeStart(p);
  const total=kind==='combo'?(p.chips['arcade-rush']?10:3):kind==='tatsu'?(p.chips['arcade-cyclone']?7:4):(p.chips['arcade-rising']?3:1);
  const gap=(kind==='combo'?(total===10?.115:.25):kind==='tatsu'?.18:.16)/p.stats.attackRate;
  const first=now+(kind==='shoryuken'?.08:.12)/p.stats.attackRate,until=first+(total-1)*gap+.22;
  this.actions.set(p.id,{kind,at:first,gap,next:0,total,id:this.sim.uid('arcade')});Object.assign(a,{action:kind,at:now,until,index:0,total,charge:0});p.cooldown=until-now;p.lastAttack=now;p.attackIndex=0;
  const recovery=p.chips['arcade-cancel']?.72:1;
  if(kind==='tatsu'){p.secondaryCd=ARCADE.tatsuCooldown*recovery;p.lastSecondary=now;p.ext=add(p.ext,add(mul(forward(p.input.yaw),5),v(0,3,0)));}
  if(kind==='shoryuken'){a.uppercutCd=ARCADE.shoryukenCooldown*recovery;p.ext=add(p.ext,add(mul(forward(p.input.yaw),4),v(0,10,0)));}
  this.sim.emit('arcade-move',p.body.translation(),{source:p.id,spec:p.spec,message:canCancel?'SPECIAL CANCEL':kind==='tatsu'?'TATSUMAKI':kind==='shoryuken'?(total===3?'SHIN SHORYUKEN':'SHORYUKEN'):'',color:0x84eaff});return true;
 }
 beginHadoken(p:Player){if(!this.active(p))return false;const a=this.state(p);if(!this.idle(p)||a.hadokenCd>0||p.cooldown>0||this.actions.has(p.id)||a.action==='charge')return true;
  if(p.chips['arcade-shinku']){if(p.stamina<12*p.stats.resourceCost)return true;Object.assign(a,{action:'charge',at:this.sim.time,until:this.sim.time+3,charge:0});this.sim.emit('arcade-charge',p.body.translation(),{source:p.id,spec:p.spec,color:0x8bdfff});}
  else this.hadoken(p,false);return true;
 }
 hadoken(p:Player,superShot:boolean){const s=this.sim,a=this.state(p);if(s.projectiles.size>=CONFIG.maxProjectiles||!s.spendResource(p,superShot?30:12)){this.cancel(p);return;}const dir=forward(p.input.yaw,p.input.pitch),pos=add(p.body.translation(),add(v(0,.18,0),mul(dir,.72))),id=s.uid('hadoken'),red=!!p.chips['arcade-red'];
  s.projectiles.set(id,{id,owner:p.id,originOwner:p.id,spec:'ansatsuken',pos,vel:mul(dir,superShot?27:23),damage:(superShot?22:23)*p.stats.damage,impulse:(superShot?180:320)*p.stats.impulse,life:4,radius:(superShot?.5:.24)*p.stats.projectileSize,direct:false,attackId:id,bounces:0,pierces:superShot?7:0,hit:new Set(),effectsUsed:true,homing:0,distanceLeft:75,gravity:0,burnDps:(red?12:4)+p.stats.burn,poisonDps:p.stats.poison,elements:p.stats.poison?['fire','poison']:['fire'],mods:{...p.chips},arcade:{super:superShot,red}});
  Object.assign(a,{action:superShot?'shinku':'hadoken',at:s.time,until:s.time+.48,charge:0,total:superShot?5:1,index:0});p.cooldown=.48;a.hadokenCd=superShot?3.4:ARCADE.hadokenCooldown;p.blockTime=0;p.lastAttack=p.lastShot=s.time;
  s.emit('arcade-move',pos,{source:p.id,spec:p.spec,message:superShot?'SHINKU HADOKEN':'HADOKEN',color:red?0xff6038:0x78dfff,radius:superShot?3:1});s.emit('shot',pos,{source:p.id,spec:p.spec,color:0x78dfff});
 }
 stunned(p:Player){return (p.hitStunUntil||0)>this.sim.time;}
 invulnerable(p:Player){return this.active(p)&&(p.arcade?.parryUntil||0)>this.sim.time;}
 parry(p:Player,point:Vec,source:string){if(!this.active(p))return;const a=this.state(p);this.cancel(p);p.blockCd=0;p.blockTime=0;p.cooldown=0;p.hitStunUntil=0;a.parryAt=this.sim.time;a.parryUntil=this.sim.time+ARCADE.parryInvulnerability;this.sim.emit('arcade-parry',point,{source:p.id,target:source,spec:p.spec,message:'PARRY',color:0x9ce7ff,radius:1.4});}
 confirmHit(p:Player,t:Player,damage:number,impact:'punch'|'kick'|'fireball',stun:number,message:string,big=false){
  const s=this.sim,a=this.state(p);if(a.comboTarget!==t.id||s.time-a.lastHit>ARCADE.comboMemory){a.hits=0;a.comboDamage=0;}a.comboTarget=t.id;a.hits++;a.comboDamage+=damage;a.lastHit=s.time;
  if(t.hp>0){t.hitStunUntil=Math.max(t.hitStunUntil||0,s.time+Math.max(.09,stun/p.stats.attackRate));t.move=v();t.dashTime=0;t.blockTime=0;t.guarding=false;t.parryTime=0;t.swing=null;t.cast=undefined;t.burst=0;this.cancel(t);}
  s.emit('arcade-hit',t.body.translation(),{source:p.id,target:t.id,spec:'ansatsuken',amount:a.hits,message,impact,radius:big?1.2:.7,color:impact==='kick'?0xffbc69:impact==='punch'?0xffecb0:0x83e7ff});
 }
 damageScale(p:Player,t:Player){return (p.chips['arcade-juggle']&&!t.grounded?1.45:1)*(p.chips['arcade-counter']&&this.sim.time-t.lastAttack<.35?1.35:1);}
 strike(p:Player,kind:Action['kind'],index:number,total:number,id:string){const s=this.sim,a=this.state(p),last=index===total-1,pos=add(p.body.translation(),v(0,.15,0)),dir=forward(p.input.yaw),radius=(kind==='tatsu'?2.65:kind==='shoryuken'?2.05:2)*p.stats.reach;
  a.index=index;p.attackIndex=index+1;p.lastAttack=s.time;
  const base=kind==='combo'?(last?19:total===10?6:index===0?9:11):kind==='tatsu'?9:last?30:11;
  const impulse=(last?(kind==='shoryuken'?850:kind==='combo'?650:450):45)*p.stats.impulse;
  for(const target of [...s.players.values(),...s.props.values()]){if(target===p||'stats'in target&&target.hp<=0||'material'in target&&target.material==='foundation')continue;const d=sub(target.body.translation(),pos),dist=len(d);if(dist>radius+.4||Math.abs(d.y)>(kind==='shoryuken'?3:1.8)||kind!=='tatsu'&&dot(norm(v(d.x,0,d.z)),dir)<Math.cos(Math.min(Math.PI,1.6*p.stats.meleeArc/2))||!s.lineOfSight(pos,target.body.translation(),p.id,target.id))continue;
   const before='stats'in target?target.hp:target.hp,blocked='stats'in target&&target.blockTime>0;
   const strikeDir=kind==='shoryuken'?norm(add(mul(dir,.3),v(0,1,0))):norm(add(norm(d),v(0,last?.25:0,0)));
   s.directHit(p,target,target.body.translation(),strikeDir,base*p.stats.damage*('stats'in target?this.damageScale(p,target):1),impulse*('stats'in target&&p.chips['arcade-counter']&&s.time-target.lastAttack<.35?1.4:1),`${id}:${index}`);
   if('stats'in target&&!blocked&&target.hp<before){this.confirmHit(p,target,before-target.hp,kind==='tatsu'||last&&kind==='combo'?'kick':'punch',last?.38:kind==='tatsu'?.22:total===10?.15:.30,p.chips['arcade-counter']&&s.time-target.lastAttack<.35?'COUNTER':last?'FINISH':'HIT',last);if(last&&p.chips['arcade-perfect']){p.stamina=Math.min(p.stats.maxStamina,p.stamina+22);p.hp=Math.min(p.stats.hp,p.hp+8);}}
  }
  for(const m of [...s.chaos.minions.values()]){const d=sub(m.body.translation(),pos);if(m.owner!==p.id&&len(d)<radius+.4&&(kind==='tatsu'||dot(norm(d),dir)>.25)&&s.lineOfSight(pos,m.body.translation(),p.id,m.id))s.chaos.hitMinion(m,base*p.stats.damage,p.id,impulse,norm(d));}
  if(p.chips['arcade-afterimage']&&kind==='combo'&&this.echoes.length<64)this.echoes.push({at:s.time+.22,until:s.time+.3,source:p.id,pos:add(pos,mul(dir,1.1)),dir,radius:1.1*p.stats.reach,damage:base*.38*p.stats.damage,impulse:50,remaining:1,gap:0,id:id+':echo:'+index,impact:'punch'});
  if(last&&kind==='shoryuken'&&p.chips['arcade-rising']){for(const prop of [...s.props.values()])if(prop.material!=='foundation'&&len(sub(prop.body.translation(),pos))<3.5)s.damageProp(prop,80,mul(norm(add(sub(prop.body.translation(),pos),v(0,2,0))),1500*p.stats.impulse),p.id,prop.body.translation());s.emit('arcade-burst',pos,{source:p.id,spec:p.spec,radius:3.5,color:0xffc276});}
  s.emit('arcade-strike',pos,{source:p.id,spec:p.spec,message:kind,amount:index,radius,color:0x8be7ff});
 }
 impact(shot:Projectile,target:Player|Prop,point:Vec,damage=shot.damage){if(!shot.arcade||!('stats'in target))return;const p=this.sim.players.get(shot.owner);if(!p)return;refreshDot(target.burns,shot.owner,shot.burnDps||4,this.sim.time,'fire');refreshDot(target.poisons,shot.owner,shot.poisonDps||0,this.sim.time,'poison');this.confirmHit(p,target,damage,'fireball',shot.arcade.super?.15:.26,shot.arcade.super?'SUPER':'HIT',shot.arcade.super);
  if(shot.arcade.super&&this.echoes.length<64)this.echoes.push({at:this.sim.time+.10,until:this.sim.time+.5,source:shot.owner,pos:{...point},dir:norm(shot.vel),radius:1.5,damage:Math.min(40,shot.damage*.45),impulse:550,remaining:4,gap:.10,id:shot.id+':'+target.id,impact:'fireball'});
 }
 save(p:Player,amount:number){if(!this.active(p)||!p.chips['arcade-secondwind']||this.used.has(p.id)||amount<p.hp)return false;this.used.add(p.id);p.hp=1;p.stamina=p.stats.maxStamina;this.state(p).secondWind=true;p.burns.clear();p.poisons.clear();this.sim.emit('arcade-move',p.body.translation(),{source:p.id,spec:p.spec,message:'ROUND TWO',color:0xffd565,radius:2});return true;}
 stepPlayer(p:Player,dt:number){if(!this.active(p)){this.cancel(p);return;}const s=this.sim,a=this.state(p);a.uppercutCd=Math.max(0,a.uppercutCd-dt);a.hadokenCd=Math.max(0,a.hadokenCd-dt);
  if(!this.idle(p)){this.cancel(p);return;}if(a.action==='charge'){a.charge=clamp((s.time-a.at)/ARCADE.chargeSeconds,0,1);if(!p.input.hadoken||s.time>=a.until)this.hadoken(p,a.charge>=1);}
  const action=this.actions.get(p.id);if(action){while(action.next<action.total&&s.time>=action.at+action.next*action.gap){this.strike(p,action.kind,action.next,action.total,action.id);action.next++;}if(action.kind==='tatsu'&&p.chips['arcade-cyclone'])for(const t of s.players.values())if(t!==p&&t.hp>0){const d=sub(p.body.translation(),t.body.translation());if(len(d)<4.5&&s.lineOfSight(p.body.translation(),t.body.translation(),p.id,t.id))t.ext=add(t.ext,mul(norm(d),dt*7));}if(s.time>=a.until)this.actions.delete(p.id);}
  if(a.action&&a.action!=='charge'&&s.time>=a.until)a.action=undefined;
 }
 step(){const s=this.sim;for(const e of this.echoes){if(s.time<e.at||e.remaining<=0)continue;const owner=s.players.get(e.source);if(!owner||!owner.connected||owner.hp<=0){e.remaining=0;continue;}for(const t of s.players.values())if(t!==owner&&t.hp>0&&len(sub(t.body.translation(),e.pos))<e.radius&&s.lineOfSight(e.pos,t.body.translation(),owner.id,t.id)){if(t.blockTime>0){s.successfulBlock(t,'projectile',t.body.translation(),owner.id);continue;}const actual=s.damage(t,e.damage,owner.id,'arcade-echo',e.dir,e.remaining===1?e.impulse:25);if(actual>0)this.confirmHit(owner,t,actual,e.impact,e.remaining===1?.26:.14,'ECHO');}e.remaining--;e.at+=e.gap;s.emit('arcade-echo',e.pos,{source:e.source,spec:'ansatsuken',radius:e.radius,color:0x80dfff});}this.echoes=this.echoes.filter(e=>e.remaining>0&&s.time<=e.until);}
}
