// Authoritative ranged/magic Q+E abilities: gating, charges, windups, press buffer, ADS, handler toolkit, ability shots and
// fields, slows, leech, bot plans (design §3.4-§3.9, §3.14; frozen after FOUNDATION). Deterministic: sim.random() only.
import RAPIER from '@dimforge/rapier3d-compat';
import type {Simulation,Player,Projectile,Prop} from './simulation.js';
import type {FieldView,FieldKind,FieldOptions,Minion} from './chaos.js';
import {fieldSpeed} from './chaos.js';
import {WEAPONS,type Stats} from './content.js';
import {CONFIG as C,type Vec,type Input,v,add,sub,mul,dot,len,norm,clamp,forward} from './config.js';
import {ballisticStep,ballisticPitch} from './projectiles.js';
import {cameraPosition,weaponMuzzle} from './view.js';
import type {ElementId} from './elements.js';
import type {BodyImpact} from './body-damage.js';
import {ceilingContact} from './ceiling-field.js';
import {rolling,committed,rollInvulnerable} from './souls-combat.js';
import {demonStepInvulnerable} from './demon-step.js';
import {abilityUser,kitFor,abilityDef,abilityActive,ABILITY_DEFS,FX_ABILITY,ABILITY_FX,castTime,abilityCooldown,abilityCost,abilityRadius,abilityScale,maxCharges,adsProfile,adsMove,adsSpread,abilityAimPoint,
 type AbilityDef,type AbilitySlot,type AbilityAim} from './abilities.js';
import {GUN_HANDLERS,gunPrimaryHit} from './gun-abilities.js';
import {MAGIC_HANDLERS,magicPrimaryHit} from './magic-abilities.js';

export interface CastContext{p:Player;def:AbilityDef;stats:Stats;scale:number;echo:boolean;cast:string;aim:AbilityAim;point?:Vec}
export type ShotTarget=Player|Minion;
export interface ShotHandler{
 /** May delete the shot. */tick?(sys:AbilitySystem,shot:Projectile,dt:number):void;
 /** Default 'stop'; 'bounce' uses `restitution` (default .5). */
 onEnemy?(sys:AbilitySystem,shot:Projectile,target:ShotTarget,point:Vec,normal:Vec,armed:boolean):'stop'|'pass'|'bounce';
 /** Prop, foundation or ceiling (prop undefined). Default 'stop'. */
 onSurface?(sys:AbilitySystem,shot:Projectile,point:Vec,normal:Vec,prop:Prop|undefined,armed:boolean):'stop'|'bounce'|'glide';
 /** Life or travel exhausted; the shot is removed afterwards. */onExpire?(sys:AbilitySystem,shot:Projectile):void;
 /** false: Block does not reflect it. */reflect?:boolean;restitution?:number;
}
export interface BotCtx{distance:number;visible:boolean;difficulty:number;target?:Vec;targetVel?:Vec}
export interface AbilityHandler{
 /** Cast-start precondition (refusal costs nothing). */can?(sys:AbilitySystem,ctx:CastContext):boolean;
 /** Fixed at cast start (ground-targeted). */point?(sys:AbilitySystem,ctx:CastContext):Vec|undefined;
 /** Also runs for echoes. */release(sys:AbilitySystem,ctx:CastContext):void;
 shot?:ShotHandler;
 /** Every ability-field end; replaces the default (bomb/implosion explode, + vacuum; triggered mines). */detonate?(sys:AbilitySystem,f:FieldView,owner:Player,cause:'until'|'trigger'|'evict'):void;
 /** true = it set the input (Blink). */bot?(sys:AbilitySystem,p:Player,input:Input,b:BotCtx):boolean;
}
export interface BlastResult{players:number;minions:number;hit:string[];/** Minion ids that took damage. */adds?:string[]}
export interface BlastOptions{props?:boolean;propScale?:number;kind?:string}
export interface HitOptions{direct?:boolean;headshot?:boolean;bodyImpact?:BodyImpact;elements?:ElementId[];kind?:string}
export interface ShotOptions{cast:string;pos?:Vec;dir?:Vec;vel?:Vec;speed?:number;gravity?:number;radius?:number;damage?:number;impulse?:number;life?:number;travel?:number;fuse?:number;elements?:ElementId[];data?:Record<string,unknown>;pierces?:number}
export interface NearOptions{los?:boolean;exclude?:Set<string>;limit?:number}
export interface Near{target:ShotTarget;pos:Vec;distance:number}

export const ABILITY_HANDLERS:Record<string,AbilityHandler>={...GUN_HANDLERS,...MAGIC_HANDLERS};
const copy=(a:Vec):Vec=>v(a.x,a.y,a.z);
const BUFFER=.3,LEECH_FLUSH=.25;

export function primaryHitEffects(sim:Simulation,p:Player,target:Player|Minion,point:Vec,actual:number){if(!abilityUser(p.spec))return;if(WEAPONS[p.spec].class==='magic')magicPrimaryHit(sim,p,target,point,actual);else gunPrimaryHit(sim,p,target,point,actual);}

export class AbilitySystem{
 private timeline:{at:number;fn:()=>void;owner?:string}[]=[];
 constructor(readonly sim:Simulation){}
 handler(def:AbilityDef|undefined):AbilityHandler|undefined{return def?ABILITY_HANDLERS[def.id]:undefined;}
 clear(){this.timeline=[];}
 remove(p:Player){this.timeline=this.timeline.filter(e=>e.owner!==p.id);this.reset(p);}
 reset(p:Player){p.abilityCd=0;p.lastAbility=-10;p.eCharges=maxCharges(p.stats);p.abilityCast=p.pendingPress=p.adsSince=p.adsBloom=p.slowed=p.slowImmuneUntil=p.empower=p.leech=undefined;}
 revive(p:Player){p.abilityCast=p.pendingPress=p.slowed=p.slowImmuneUntil=p.empower=p.leech=undefined;}
 cancel(p:Player,mode:'windup'|'buffer'|'death'='windup'){p.abilityCast=undefined;if(mode!=='windup')p.pendingPress=undefined;if(mode==='death'){p.adsSince=p.adsBloom=p.empower=p.leech=undefined;}}
 busy(p:Player){return !!p.abilityCast;}
 schedule(at:number,fn:()=>void,owner?:string){const e={at,fn,owner};let i=this.timeline.length;while(i>0&&this.timeline[i-1].at>at)i--;this.timeline.splice(i,0,e);}

 input(p:Player){
  const s=this.sim,time=s.time,kit=kitFor(p.spec);if(!kit)return;
  const max=maxCharges(p.stats);if(p.eCharges>max)p.eCharges=max;if(p.secondaryCd<=0&&p.eCharges<max){p.eCharges++;if(p.eCharges<max)p.secondaryCd=abilityCooldown(kit.e,p.stats);}
  const aiming=!!kit.ads&&p.input.ability===true&&p.hp>0&&!rolling(p,time)&&!s.leet.frozen(p);
  if(aiming){if(p.adsSince===undefined)p.adsSince=time;}else if(p.adsSince!==undefined||p.adsBloom){p.adsSince=undefined;p.adsBloom=undefined;}
  if(p.abilityCast&&time>=p.abilityCast.at)this.release(p);
  if(p.pendingPress){if(time-p.pendingPress.at>BUFFER)p.pendingPress=undefined;else this.press(p,p.pendingPress.slot,true);}
  if(p.input.ability===true&&kit.q.kind!=='ads'&&abilityActive(p.spec,'q'))this.press(p,'q');
 }
 step(_dt:number){
  const s=this.sim,time=s.time;
  for(let n=0;n<256&&this.timeline.length&&this.timeline[0].at<=time;n++)this.timeline.shift()!.fn();
  for(const p of s.players.values()){
   if(p.slowed&&p.slowed.until<=time){if(p.slowed.factor<=.6)p.slowImmuneUntil=time+1;p.slowed=undefined;}
   if(p.slowImmuneUntil!==undefined&&p.slowImmuneUntil<=time)p.slowImmuneUntil=undefined;
   if(p.empower&&(p.empower.shots<=0||p.empower.until<time))p.empower=undefined;
   if(p.leech&&p.leech.hp>=1&&time-p.leech.at>=LEECH_FLUSH){const hp=p.leech.hp;p.leech={hp:0,at:time};s.heal(p,hp);}
  }
 }
 shift(dt:number){
  const s=this.sim;for(const e of this.timeline)e.at+=dt;
  for(const f of s.chaos.fields.values())if(f.fx&&ABILITY_FX.has(f.fx)){f.born+=dt;f.until+=dt;f.arm+=dt;if(f.start!==undefined)f.start+=dt;if(f.lockAt!==undefined)f.lockAt+=dt;}
  for(const q of s.projectiles.values())if(q.ability){if(q.born!==undefined)q.born+=dt;if(q.ability.fuse!==undefined)q.ability.fuse+=dt;}
  for(const p of s.players.values()){if(p.abilityCast)p.abilityCast.at+=dt;if(p.pendingPress)p.pendingPress.at+=dt;if(p.slowed)p.slowed.until+=dt;if(p.slowImmuneUntil!==undefined)p.slowImmuneUntil+=dt;if(p.empower)p.empower.until+=dt;if(p.adsSince!==undefined)p.adsSince+=dt;if(p.adsBloom)p.adsBloom.at+=dt;if(p.leech)p.leech.at+=dt;}
 }

 press(p:Player,slot:AbilitySlot,retry=false):'cast'|'buffered'|'refused'{
  const s=this.sim,time=s.time,def=abilityDef(p.spec,slot);
  const refuse=()=>{if(p.pendingPress?.slot===slot)p.pendingPress=undefined;return 'refused' as const;};
  if(!def||def.kind==='ads'||!abilityActive(p.spec,slot))return refuse();
  if(s.phase!=='fight'||p.hp<=0||!p.connected||p.blockTime>0||s.siren.heldVictim(p)||s.leet.frozen(p))return refuse();
  if(WEAPONS[p.spec].class==='magic'&&p.mana<abilityCost(def,p.stats)*p.stats.resourceCost)return refuse();
  const noCharge=slot==='e'&&p.eCharges<=0;
  if(slot==='q'?p.abilityCd>BUFFER:noCharge&&p.secondaryCd>BUFFER)return refuse();
  if(p.cast||p.abilityCast||rolling(p,time)||committed(p,time)||s.arcade.stunned(p)||(slot==='q'?p.abilityCd>0:noCharge)){if(!retry||!p.pendingPress)p.pendingPress={slot,at:time};return 'buffered';}
  const aim:AbilityAim={yaw:p.input.yaw,pitch:p.input.pitch,camera:this.aimCamera(p)},cast=s.uid('cast'),h=this.handler(def);
  const ctx:CastContext={p,def,stats:p.stats,scale:1,echo:false,cast,aim};
  if(h?.can&&!h.can(this,ctx))return refuse();
  const point=h?.point?.(this,ctx),at=time+castTime(def.windup,p.stats);
  p.abilityCast={slot,id:def.id,at,aim,...(point?{point:copy(point)}:{}),cast};p.pendingPress=undefined;
  if(slot==='q')p.lastAbility=time;else p.lastSecondary=time;
  s.emit('ability-cast',p.body.translation(),{source:p.id,spec:p.spec,message:def.id,amount:at});
  if(at<=time)this.release(p);
  return 'cast';
 }
 release(p:Player){
  const c=p.abilityCast;if(!c)return;p.abilityCast=undefined;const s=this.sim,def=abilityDef(p.spec,c.slot);
  if(!def||def.id!==c.id||s.phase!=='fight'||p.hp<=0||p.blockTime>0||s.arcade.stunned(p)||s.leet.frozen(p))return;
  const manaHigh=WEAPONS[p.spec].class==='magic'&&p.mana>=.75*p.stats.maxMana;
  if(!s.spendResource(p,abilityCost(def,p.stats)))return;
  if(c.slot==='q')p.abilityCd=abilityCooldown(def,p.stats);else{p.eCharges=Math.max(0,p.eCharges-1);if(p.secondaryCd<=0)p.secondaryCd=abilityCooldown(def,p.stats);}
  p.cooldown=Math.max(p.cooldown,castTime(def.lockout,p.stats));
  const h=this.handler(def),ctx:CastContext={p,def,stats:p.stats,scale:abilityScale(p.stats,manaHigh),echo:false,cast:c.cast,aim:c.aim,...(c.point?{point:c.point}:{})};
  h?.release(this,ctx);
  s.emit('ability',p.body.translation(),{source:p.id,spec:p.spec,message:def.id});
  if(c.slot==='e'&&p.stats.spellEcho>0&&h)this.schedule(s.time+.8,()=>{if(p.hp>0&&s.players.get(p.id)===p&&s.phase==='fight')h.release(this,{...ctx,scale:abilityScale(p.stats,manaHigh,true),echo:true,cast:s.uid('cast')});},p.id);
  if(c.slot==='q'&&p.stats.qEmpower>0)p.empower={until:s.time+3,mult:1+p.stats.qEmpower,shots:Math.ceil(.6/WEAPONS[p.spec].interval)};
 }

 aimCamera(p:Player):'first'|'third'{return p.adsSince!==undefined?'first':p.input.camera||'third';}
 shotTuning(owner:Player){
  const w=WEAPONS[owner.spec],time=this.sim.time;let spread=w.spread,damage=1;
  if(owner.adsSince!==undefined){spread=adsSpread(owner,owner.spec,time,w.spread);if(adsProfile(owner.spec)?.bloom){const n=owner.adsBloom&&time-owner.adsBloom.at<=.25?owner.adsBloom.n+1:1;owner.adsBloom={n,at:time};}}
  if(owner.empower){if(owner.empower.shots>0&&time<=owner.empower.until){damage=owner.empower.mult;owner.empower.shots--;}if(owner.empower.shots<=0||time>owner.empower.until)owner.empower=undefined;}
  return {spread,damage,camera:this.aimCamera(owner)};
 }
 intervalScale(p:Player){return p.adsSince!==undefined?adsProfile(p.spec)?.interval??1:1;}
 statusFactor(p:Player){return p.slowed&&p.slowed.until>this.sim.time?Math.max(.4,p.slowed.factor):1;}
 motionScale(p:Player){return Math.max(.4,Math.min(fieldSpeed([...this.sim.chaos.fields.values()],p.id,p.body.translation(),owner=>this.sim.canHit(owner,p.id),this.sim.time),this.statusFactor(p)));}
 motionFactor(p:Player){return adsMove(p.spec,p.input)*this.motionScale(p);}

 onDamage(source:Player|string|null|undefined,actual:number){
  const p=typeof source==='string'?this.sim.players.get(source):source;if(!p||!(actual>0))return;const s=p.stats;
  if(s.manaSurge>0)p.mana=Math.min(s.maxMana,p.mana+actual*s.manaSurge);
  if(s.abilityLeech>0){p.leech??={hp:0,at:-1e9};p.leech.hp+=actual*s.abilityLeech;}
 }
 slow(target:Player|Minion,speedFactor:number,seconds:number,kind:string,source?:string){
  const s=this.sim,time=s.time;if(!('stats'in target))return !!source&&s.minionStatus(source,target,{slow:{factor:speedFactor,seconds}});if(target.spec==='siren'||s.siren.armored(target)||(target.slowImmuneUntil??-1)>time||target.hp<=0||!(seconds>0))return false;
  const until=time+seconds,cur=target.slowed&&target.slowed.until>time?target.slowed:undefined,factor=clamp(speedFactor,.4,1);
  target.slowed=cur?{factor:Math.min(cur.factor,factor),until:Math.max(cur.until,until),kind:factor<cur.factor?kind:cur.kind}:{factor,until,kind};
  s.emit('slowed',target.body.translation(),{target:target.id,source:source??null,message:kind,amount:factor,radius:seconds});return true;
 }

 aimRay(p:Player,aim:AbilityAim,range:number){
  const s=this.sim,pos=p.body.translation(),trace=(o:Vec,d:Vec,dist:number)=>s.ray(o,d,dist,p.id)?.timeOfImpact??null,dir=forward(aim.yaw,aim.pitch);
  const camera=cameraPosition(pos,aim.yaw,aim.pitch,aim.camera,trace),hit=s.ray(camera,dir,range+5,p.id);
  return {camera,dir,hitPoint:hit?add(camera,mul(dir,hit.timeOfImpact)):undefined,target:add(camera,mul(dir,hit?hit.timeOfImpact:range+5)),start:weaponMuzzle(pos,aim.yaw,trace)};
 }
 aimPoint(p:Player,aim:AbilityAim,range:number,opts:{ground?:boolean}={}){
  const s=this.sim,pos=p.body.translation(),trace=(o:Vec,d:Vec,dist:number)=>s.ray(o,d,dist,p.id)?.timeOfImpact??null;
  return abilityAimPoint(cameraPosition(pos,aim.yaw,aim.pitch,aim.camera,trace),pos,forward(aim.yaw,aim.pitch),range,!!opts.ground,trace);
 }
 D(def:AbilityDef,ctx:CastContext){return (def.damage||0)*ctx.scale;}
 R(def:AbilityDef,ctx:CastContext){return abilityRadius(def,ctx.stats);}
 cd(def:AbilityDef,stats:Stats){return abilityCooldown(def,stats);}
 spawnShot(p:Player,def:AbilityDef,opts:ShotOptions):Projectile|undefined{
  const s=this.sim;if(s.projectiles.size>=C.maxProjectiles)return undefined;
  const start=opts.pos??weaponMuzzle(p.body.translation(),p.input.yaw,(o,d,dist)=>s.ray(o,d,dist,p.id)?.timeOfImpact??null),speed=opts.speed??def.speed??30;
  const vel=opts.vel?copy(opts.vel):mul(norm(opts.dir??forward(p.input.yaw,p.input.pitch)),speed),id=s.uid('ability');
  const shot:Projectile={id,originOwner:p.id,born:s.time,owner:p.id,spec:p.spec,pos:copy(start),vel,damage:opts.damage??def.damage??0,impulse:opts.impulse??def.impulse??0,
   life:opts.life??C.projectileLifetime,radius:opts.radius??def.shotRadius??.15,direct:false,attackId:id,bounces:0,pierces:opts.pierces??def.pierce??0,hit:new Set(),effectsUsed:true,homing:0,
   distanceLeft:opts.travel??(def.range||C.projectileMaxTravel),gravity:opts.gravity??def.gravity??0,mods:{},reflections:0,elements:opts.elements??[],
   ability:{id:def.id,slot:def.slot,cast:opts.cast,travel:0,...(opts.fuse!==undefined?{fuse:opts.fuse}:{}),...(opts.data?{data:opts.data}:{})}};
  s.projectiles.set(id,shot);return shot;
 }
 blast(p:Player,point:Vec,radius:number,damage:number,impulse:number,fx:string,opts:BlastOptions={}):BlastResult{
  const r=this.sim.explode(p,point,radius,damage,impulse,opts.kind??'ability',opts.props??true,fx,opts.propScale??1);if(r.minions>0)this.onDamage(p,r.minions);return r;
 }
 hit(p:Player,target:Player|Minion|Prop,damage:number,dir:Vec,impulse:number,point:Vec,opts:HitOptions={}){
  const s=this.sim;let actual=0;
  if('stats'in target)actual=s.damage(target,damage,p.id,opts.kind??'ability',dir,impulse,point,false,false,!!opts.headshot,opts.elements??[],opts.bodyImpact);
  else if(s.chaos.minions.get(target.id)===target){actual=s.chaos.hitMinion(target as Minion,damage,p.id,impulse,dir,{point,kind:opts.bodyImpact?.kind==='blast'?'blast':'energy'});if(actual>0)this.onDamage(p,actual);}
  else if(s.props.has(target.id))s.damageProp(target as Prop,damage*p.stats.demolition,mul(dir,impulse),p.id,point);
  if(opts.direct&&actual>0)s.lifeOnHit(p,actual,false);
  return actual;
 }
 zone(p:Player,kind:FieldKind,point:Vec,radius:number,duration:number,damage:number,fx:string,opts:FieldOptions={}){return this.sim.chaos.field(p,kind,point,radius,duration,damage,opts.attach,fx,opts);}
 blink(p:Player,delta:Vec){this.sim.chaos.blink(p,delta);}
 enemiesNear(p:Player,point:Vec,radius:number,opts:NearOptions={}):Near[]{
  const s=this.sim,limit=opts.limit??Infinity,all:Near[]=[];
  for(const q of s.players.values()){if(q===p||q.hp<=0||opts.exclude?.has(q.id)||!s.canHit(p.id,q.id)||s.pve?.hidden(q))continue;const pos=q.body.translation(),d=len(sub(pos,point));if(d<=radius)all.push({target:q,pos:copy(pos),distance:d});}
  for(const m of s.chaos.minions.values()){if(m.owner===p.id||opts.exclude?.has(m.id)||!s.canHit(p.id,m.owner))continue;const pos=m.body.translation(),d=len(sub(pos,point));if(d<=radius)all.push({target:m,pos:copy(pos),distance:d});}
  all.sort((a,b)=>a.distance-b.distance);if(opts.los===false)return all.slice(0,limit);
  const out:Near[]=[];let rays=0;for(const n of all){if(out.length>=limit||rays>=4)break;rays++;if(s.lineOfSight(point,n.pos,p.id,n.target.id))out.push(n);}return out;
 }

 stepShot(shot:Projectile,dt:number):boolean{
  const tag=shot.ability;if(!tag)return false;const s=this.sim,def=ABILITY_DEFS[tag.id],h=this.handler(def)?.shot;
  if(!def){s.projectiles.delete(shot.id);return true;}
  h?.tick?.(this,shot,dt);if(!s.projectiles.has(shot.id))return true;
  const expire=()=>{h?.onExpire?.(this,shot);s.projectiles.delete(shot.id);};
  const step=Math.min(dt,shot.life);if(step<=0){expire();return true;}shot.life-=step;
  const before=copy(shot.vel),flight=ballisticStep(before,shot.gravity,step),distance=Math.min(len(flight.displacement),shot.distanceLeft),displacement=mul(norm(flight.displacement),distance);
  shot.vel=flight.velocity;shot.distanceLeft-=distance;if(distance<=0){expire();return true;}
  const owner=shot.owner,hit=s.world.castShape(shot.pos,{x:0,y:0,z:0,w:1},norm(displacement),new RAPIER.Ball(shot.radius),0,distance,true,undefined,undefined,undefined,undefined,(c:RAPIER.Collider)=>{const id=s.colliderOwners.get(c.handle)||'',m=s.chaos.minions.get(id);return id!==owner&&!rollInvulnerable(s.players.get(id)||{},s.time)&&!demonStepInvulnerable(s.players.get(id),s.time)&&s.canHit(owner,id)&&m?.owner!==owner&&s.canHit(owner,m?.owner||'')&&!shot.hit.has(id);});
  const field=ceilingContact(shot.pos,displacement,shot.radius);
  if(field&&(!hit||field.distance<=hit.time_of_impact)){tag.travel+=field.distance;const armed=tag.travel>=(def.arm??0);shot.pos=field.center;this.surface(shot,h,h?.onSurface?.(this,shot,field.point,v(0,-1,0),undefined,armed)??'stop',v(0,-1,0));return true;}
  if(!hit){shot.pos=add(shot.pos,displacement);tag.travel+=distance;if(shot.distanceLeft<=0||shot.life<=0)expire();return true;}
  const id=s.colliderOwners.get(hit.collider.handle)||'',point=add(shot.pos,mul(norm(displacement),hit.time_of_impact)),normal=copy(hit.normal1);
  tag.travel+=hit.time_of_impact;const armed=tag.travel>=(def.arm??0),player=s.players.get(id),minion=s.chaos.minions.get(id);
  if(player&&player.hp>0&&player.blockTime>0&&h?.reflect!==false){s.reflectProjectile(shot,player,point);return true;}
  if(player||minion){const r=h?.onEnemy?.(this,shot,(player||minion)!,point,normal,armed)??'stop';if(!s.projectiles.has(shot.id))return true;
   if(r==='pass'){shot.hit.add(id);shot.pos=add(point,mul(norm(shot.vel),.08));}else if(r==='bounce')this.bounce(shot,h,point,normal);else s.projectiles.delete(shot.id);return true;}
  shot.pos=point;this.surface(shot,h,h?.onSurface?.(this,shot,point,normal,s.props.get(id),armed)??'stop',normal);return true;
 }
 private bounce(shot:Projectile,h:ShotHandler|undefined,point:Vec,normal:Vec){const n=norm(normal),r=h?.restitution??.5;shot.vel=mul(sub(shot.vel,mul(n,2*dot(shot.vel,n))),r);shot.pos=add(point,mul(n,shot.radius+.02));}
 private surface(shot:Projectile,h:ShotHandler|undefined,r:'stop'|'bounce'|'glide',normal:Vec){
  if(!this.sim.projectiles.has(shot.id))return;
  if(r==='bounce')this.bounce(shot,h,shot.pos,normal);
  else if(r==='glide'){const n=norm(normal);shot.vel=sub(shot.vel,mul(n,dot(shot.vel,n)));shot.pos=add(shot.pos,mul(n,shot.radius*.5+.02));}
  else this.sim.projectiles.delete(shot.id);
 }

 detonate(f:FieldView,cause:'until'|'trigger'|'evict'='until'){
  const s=this.sim,owner=s.players.get(f.owner);if(!owner)return;const def=ABILITY_DEFS[FX_ABILITY[f.fx||'']||''],h=this.handler(def);
  if(h?.detonate){h.detonate(this,f,owner,cause);return;}
  if(f.kind!=='bomb'&&f.kind!=='implosion'&&!(f.kind==='mine'&&cause==='trigger'))return;
  const pos=add(f.pos,v(0,.15,0));this.blast(owner,pos,f.radius,f.damage,f.impulse??(f.kind==='implosion'?0:1100),f.fx||f.kind,{kind:f.blastKind??'ability'});
  if(f.kind==='implosion')s.vacuum(owner,pos,1300);
 }

 botKit(spec:Player['spec']){const k=kitFor(spec);return !!k&&(!!k.e.live||!!k.q.bot);}
 botAds(p:Player,input:Input,distance:number,visible:boolean){const k=kitFor(p.spec),b=k?.q.bot;if(!k?.ads||!b)return;input.ability=visible&&distance>=b.min&&distance<=b.max&&!rolling(p,this.sim.time);}
 botDesired(p:Player):number|undefined{const k=kitFor(p.spec);return k?.e.live?k.e.bot?.desired:undefined;}
 botCast(p:Player,input:Input,distance:number,visible:boolean,difficulty:number,target?:Vec,targetVel?:Vec):boolean{
  const s=this.sim,time=s.time,k=kitFor(p.spec);if(!k||this.busy(p)||p.cast||committed(p,time)||rolling(p,time))return false;
  const b:BotCtx={distance,visible,difficulty,...(target?{target}:{}),...(targetVel?{targetVel}:{})};
  const hq=k.q.live?this.handler(k.q):undefined;if(hq?.bot&&this.ready(p,k.q)&&hq.bot(this,p,input,b))return true;
  if(!visible)return false;
  const ok=(d:AbilityDef)=>!!d.live&&!!d.bot&&d.kind!=='ads'&&distance>=d.bot.min&&distance<=d.bot.max&&this.ready(p,d);
  const pick=k.q.bot?.priorityBelow!==undefined&&distance<k.q.bot.priorityBelow&&ok(k.q)?k.q:ok(k.e)?k.e:ok(k.q)?k.q:undefined;
  if(!pick||s.random()>[.4,.65,.85][difficulty])return false;
  const pos=p.body.translation(),eye=add(pos,v(0,.5,0)),bot=pick.bot!;let aimAt=target?copy(target):undefined;
  if(aimAt&&targetVel&&bot.lead)aimAt=add(aimAt,mul(v(targetVel.x,0,targetVel.z),bot.lead*(castTime(pick.windup,p.stats)+(pick.fuse??0))));
  if(aimAt&&bot.aim==='point')aimAt.y-=C.playerHalfHeight+C.playerRadius-.05;
  if(aimAt){if((bot.aim==='point'||bot.aim==='ballistic')&&target&&!this.clearTo(p,eye,target))return false;const d=sub(aimAt,eye),h=Math.hypot(d.x,d.z);input.yaw=bot.aim==='away'?Math.atan2(-d.x,-d.z):Math.atan2(d.x,d.z);
   input.pitch=bot.aim==='ballistic'?clamp(ballisticPitch(h,d.y,pick.speed||30,pick.gravity||0),-1.1,1.1):bot.aim==='away'?0:clamp(Math.atan2(d.y,h),-1.1,1.1);}
  input.camera='first';input.attack=false;input.dash=false;if(pick.slot==='e')input.secondary=true;else input.ability=true;return true;
 }
 private clearTo(p:Player,eye:Vec,to:Vec){const d=sub(to,eye),l=len(d);if(l<.01)return true;const hit=this.sim.ray(eye,norm(d),l,p.id);return !hit||hit.timeOfImpact>=l-1;}
 private ready(p:Player,d:AbilityDef){if(d.slot==='e'?p.eCharges<=0:p.abilityCd>0)return false;if(WEAPONS[p.spec].class==='magic'&&p.mana<abilityCost(d,p.stats)*p.stats.resourceCost)return false;const h=this.handler(d);return !h?.can||h.can(this,{p,def:d,stats:p.stats,scale:1,echo:false,cast:'',aim:{yaw:p.input.yaw,pitch:p.input.pitch,camera:'first'}});}
}
