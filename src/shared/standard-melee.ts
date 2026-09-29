import {projectileHeadshot,HEADSHOT_MULTIPLIER} from './headshots.js';
import RAPIER from '@dimforge/rapier3d-compat';
import type {Player,Projectile,Simulation} from './simulation.js';
import {CONFIG as C,v,add,sub,mul,norm,len,dot,forward,type Vec,type SpecId,type Input} from './config.js';
import {cameraPosition} from './view.js';
import {WEAPONS} from './content.js';
import {attackElements,refreshDot} from './elements.js';
import {ceilingContact} from './ceiling-field.js';
import {downProtected,pickupOpen} from './recovery.js';
export type MeleeActionKind='dagger-throw'|'wolf-spin'|'sword-launch'|'shield-throw';
export interface MeleeAction {kind:MeleeActionKind;at:number;releaseAt:number;until:number;yaw:number;released?:boolean;landedAt?:number;shotId?:string;}
export interface MeleeShot {kind:'dagger'|'shield';returning?:boolean;}
export function meleeMotionInput(input:Input,action:MeleeAction|undefined,time:number):Input{return action?.kind==='wolf-spin'&&time<action.until?{...input,jump:false,dash:false}:input;}
export const normalMelee=(spec:SpecId)=>['sword','daggers','greatsword','shield'].includes(spec);
/** Action times are shared by simulation, animation, and network snapshots. */
export const MELEE_SPECIALS={
 sword:{kind:'sword-launch',windup:.26,recover:.50},
 daggers:{kind:'dagger-throw',windup:.19,recover:.28},
 greatsword:{kind:'wolf-spin',windup:.28,recover:.55},
 shield:{kind:'shield-throw',windup:.30,recover:.40},
} as const;
export class StandardMelee {
 constructor(private s:Simulation){}
 busy(p:Player){return !!p.meleeAction&&this.s.time<p.meleeAction.until;}
 cancel(p:Player){p.meleeAction=undefined;}
 start(p:Player){
  const s=this.s,w=WEAPONS[p.spec];if(!normalMelee(p.spec)||this.busy(p)||p.spec==='greatsword'&&!p.grounded)return;
  if((p.spec==='daggers'||p.spec==='shield')&&s.projectiles.size>=C.maxProjectiles)return;
  if(!s.spendResource(p,w.secondaryCost||0))return;
  const info=MELEE_SPECIALS[p.spec as keyof typeof MELEE_SPECIALS],rate=p.stats.attackRate;
  p.meleeAction={kind:info.kind,at:s.time,releaseAt:s.time+info.windup/rate,until:s.time+(info.windup+info.recover)/rate,yaw:p.input.yaw};
  if(p.spec==='greatsword')p.meleeAction.until=s.time+3.5;
  p.lastSecondary=p.lastAttack=s.time;p.secondaryCd=w.secondaryCooldown;p.cooldown=(info.windup+info.recover)/rate;p.swing=null;
  s.emit('melee-special',p.body.translation(),{source:p.id,spec:p.spec,message:info.kind,color:w.color});
 }
 stun(target:Player,seconds:number){const s=this.s;if(target.hp<=0)return;target.hitStunUntil=Math.max(target.hitStunUntil||0,s.time+seconds);target.move=v();target.dashTime=0;target.swing=null;target.cast=undefined;target.burst=0;target.parryTime=target.blockTime=0;target.guarding=false;this.cancel(target);s.arcade.cancel(target);s.emperor.cancel(target);}
 /** Reuses the single legs-up pickup and roll-away recovery, including existing protection. */
 launch(target:Player,owner:Player,lift:number){
  const s=this.s,used=!!target.knockdown?.pickupUsed||!!target.juggle?.pickupUsed||pickupOpen(target.knockdown,s.time);
  this.stun(target,.24);s.arcade.knockDown(target);target.knockdown!.pickupUsed=used;target.knockdown!.source=owner.id;target.knockdown!.juggleUntil=s.time+.9;target.knockdown!.juggleHits=0;
  target.ext.y=Math.max(0,lift-target.body.linvel().y);target.grounded=false;
 }
 primaryImpact(owner:Player,target:Player,hpBefore:number){if(target.hp>=hpBefore||target.hp<=0||!normalMelee(owner.spec))return;
  if(owner.spec==='shield')this.stun(target,.36/Math.sqrt(owner.stats.attackRate));
  else if(pickupOpen(target.knockdown,this.s.time))this.launch(target,owner,4.8);
  else if(target.knockdown&&target.knockdown.landedAt===undefined&&this.s.time<(target.knockdown.juggleUntil||0)&&(target.knockdown.juggleHits||0)<2){target.knockdown.juggleHits=(target.knockdown.juggleHits||0)+1;target.ext.y=Math.max(0,2.5-target.body.linvel().y);}
 }
 step(p:Player){const s=this.s,a=p.meleeAction;if(!a)return;
  if(p.hp<=0||!p.connected||!normalMelee(p.spec)||s.arcade.stunned(p)||s.leet.frozen(p)||p.blockTime>0){this.cancel(p);return;}
  if(a.kind==='shield-throw'&&a.shotId&&s.projectiles.has(a.shotId))a.until=Math.max(a.until,s.time+.1);
  if(s.time>=a.until){this.cancel(p);return;}
  if(!a.released&&s.time>=a.releaseAt){a.released=true;
   if(a.kind==='dagger-throw'||a.kind==='shield-throw')this.throw(p,a);
   else if(a.kind==='sword-launch'){this.strike(p,WEAPONS.sword.range*p.stats.reach,1.12,240,'launch');s.emit('melee-launch',p.body.translation(),{source:p.id,spec:p.spec,color:WEAPONS.sword.color});}
   else {p.ext=add(mul(forward(a.yaw),7),v(0,10-p.body.linvel().y,0));p.grounded=false;}
  }
  // The impact is triggered by a real landing, never by an animation-only timer.
  if(a.kind==='wolf-spin'&&a.released&&a.landedAt===undefined&&s.time>a.releaseAt+.20&&p.grounded&&p.body.linvel().y<1){
   a.landedAt=s.time;a.until=s.time+.55/p.stats.attackRate;p.cooldown=Math.max(p.cooldown,.55/p.stats.attackRate);p.ext.x*=.25;p.ext.z*=.25;
   this.strike(p,3.7*p.stats.reach,1.12,760,'slam');s.emit('melee-slam',add(p.body.translation(),v(0,-.75,0)),{source:p.id,spec:p.spec,radius:3.7*p.stats.reach,color:0xe8d4b2});
  }
 }
 private strike(p:Player,range:number,multiplier:number,impulse:number,kind:'slam'|'launch'){
  const s=this.s,origin=add(p.body.translation(),v(0,.1,0)),f=forward(p.meleeAction!.yaw),attack=s.uid('melee-special');
  for(const target of [...s.players.values(),...s.props.values()]){
   if(target.id===p.id||'stats'in target&&(target.hp<=0||!s.canHit(p.id,target.id)||downProtected(target.knockdown,s.time)))continue;
   const d=sub(target.body.translation(),origin),extra='stats'in target?.45:Math.min(.8,len(target.size)*.3);
   if(len(d)>range+extra||Math.abs(d.y)>(kind==='launch'?2.5:1.8)+extra||kind==='launch'&&dot(norm(v(d.x,0,d.z)),f)<.45||!s.lineOfSight(origin,target.body.translation(),p.id,target.id))continue;
   const hp='stats'in target?target.hp:0,dir=norm(add(d,v(0,kind==='slam'?.2:.5,0)));
   s.directHit(p,target,target.body.translation(),dir,WEAPONS[p.spec].damage*p.stats.damage*multiplier,impulse*p.stats.impulse,attack);
   if('stats'in target&&target.hp<hp&&target.hp>0){if(kind==='launch')this.launch(target,p,7.4);else {this.launch(target,p,4.4);target.ext=add(target.ext,mul(norm(v(d.x,0,d.z)),3));}}
  }
 }
 private throw(p:Player,a:MeleeAction){
  const s=this.s;if(s.projectiles.size>=C.maxProjectiles)return;const shield=a.kind==='shield-throw',w=WEAPONS[p.spec],id=s.uid('melee-shot');
  const pos=add(p.body.translation(),v(0,.3,0)),aim=forward(p.input.yaw,p.input.pitch),camera=cameraPosition(p.body.translation(),p.input.yaw,p.input.pitch,p.input.camera||'third',(o,d,n)=>s.ray(o,d,n,p.id)?.timeOfImpact??null),hit=s.ray(camera,aim,shield?25:48,p.id),target=add(camera,mul(aim,hit?.timeOfImpact??48)),dir=norm(sub(target,pos)),speed=(shield?24:35)*p.stats.projectileSpeed;
  s.projectiles.set(id,{melee:{kind:shield?'shield':'dagger'},id,originOwner:p.id,born:s.time,pos,vel:mul(dir,speed),owner:p.id,spec:p.spec,damage:w.damage*p.stats.damage,impulse:(shield?280:95)*p.stats.impulse,life:shield?3:2,radius:shield?.28:.095,direct:true,attackId:id,bounces:p.stats.bounces,pierces:0,hit:new Set(),effectsUsed:false,homing:0,distanceLeft:shield?19:48,gravity:shield?0:3,elements:attackElements(p.stats.burn,p.stats.poison),burnDps:p.stats.burn,poisonDps:p.stats.poison,mods:{...p.chips}});
  a.shotId=id;p.lastShot=s.time;s.emit('shot',pos,{source:p.id,spec:p.spec,color:w.color});
 }
 /** Returning shields are visual retrievals: they cannot damage a second time or grant resources. */
 stepShot(shot:Projectile,dt:number){if(!shot.melee)return false;const s=this.s,shield=shot.melee.kind==='shield',original=s.players.get(shot.originOwner||shot.owner);
  if(shield&&(!original||original.hp<=0||!original.connected)){s.projectiles.delete(shot.id);return true;}
  if(shield&&(shot.life<=dt||shot.distanceLeft<=0))shot.melee.returning=true;
  if(shot.melee.returning){const delta=sub(add(original!.body.translation(),v(0,.25,0)),shot.pos),distance=len(delta),step=40*dt;
   if(distance<step+.4){s.projectiles.delete(shot.id);if(original!.meleeAction?.shotId===shot.id)original!.meleeAction.until=s.time+.15;s.emit('catch',shot.pos,{source:original!.id,spec:'shield',color:0x9ddfff});}
   else {shot.vel=mul(norm(delta),40);shot.pos=add(shot.pos,mul(norm(delta),step));}return true;
  }
  shot.life-=dt;if(shot.life<=0||shot.distanceLeft<=0){s.projectiles.delete(shot.id);return true;}
  shot.vel.y-=shot.gravity*dt;const displacement=mul(shot.vel,dt),distance=Math.min(len(displacement),shot.distanceLeft),dir=norm(displacement);
  shot.distanceLeft-=distance;const hit=s.world.castShape(shot.pos,{x:0,y:0,z:0,w:1},dir,new RAPIER.Ball(shot.radius),0,distance,true,undefined,undefined,undefined,undefined,c=>{const id=s.colliderOwners.get(c.handle)||'',minion=s.chaos.minions.get(id);return id!==shot.owner&&!shot.hit.has(id)&&s.canHit(shot.owner,minion?.owner||id)&&minion?.owner!==shot.owner;});
  const field=ceilingContact(shot.pos,mul(dir,distance),shot.radius);
  if(field&&(!hit||field.distance<=hit.time_of_impact)){s.emit('ceiling-hit',field.point,{source:shot.owner,spec:shot.spec,radius:shot.radius,color:0x75ddff,message:shot.bounces?'BOUNCE':'ABSORB'});
   if(shot.bounces>0){shot.bounces--;shot.vel.y=-Math.abs(shot.vel.y);shot.pos=field.center;shot.damage*=original?.stats.bounceRetention??.7;}
   else if(shield){shot.pos=field.center;shot.melee.returning=true;}else s.projectiles.delete(shot.id);return true;
  }
  if(!hit){shot.pos=add(shot.pos,mul(dir,distance));return true;}
  const id=s.colliderOwners.get(hit.collider.handle)||'',point=add(shot.pos,mul(dir,hit.time_of_impact)),target=s.players.get(id)||s.props.get(id),owner=s.players.get(shot.owner);shot.pos=point;
  if(owner&&target){
   if('stats'in target&&target.hp>0&&target.blockTime>0){if(shield){s.successfulBlock(target,'projectile',point,shot.owner);shot.melee.returning=true;}else s.reflectProjectile(shot,target,point);return true;}
   const before='stats'in target?target.hp:0,headshot=!shield&&'stats'in target&&projectileHeadshot({pos:target.body.translation(),yaw:target.input.yaw,knockdown:target.knockdown,juggle:target.juggle},point,shot.vel,shot.radius);
   if(shot.direct)s.directHit(owner,target,point,dir,shot.damage,shot.impulse,shot.attackId,false,false,headshot);
   else if('stats'in target){const actual=s.damage(target,shot.damage*(headshot?HEADSHOT_MULTIPLIER:1),owner.id,'reflected',dir,shot.impulse,point,false,false,headshot,attackElements(shot.burnDps||0,shot.poisonDps||0));if(actual>0){refreshDot(target.burns,owner.id,shot.burnDps||0,s.time,'fire');refreshDot(target.poisons,owner.id,shot.poisonDps||0,s.time,'poison');}}
   else s.damageProp(target,shot.damage*owner.stats.demolition,mul(dir,shot.impulse),owner.id,point);
   if(shield&&'stats'in target&&target.hp<before&&target.hp>0)this.launch(target,owner,3.8);
   s.emit('impact',point,{source:shot.owner,target:id,spec:shot.spec,color:WEAPONS[shot.spec].color,material:'material'in target?target.material:undefined});
  }else {const minion=s.chaos.minions.get(id);if(minion&&owner){s.chaos.direct(owner,minion,point,dir,shot.attackId);s.chaos.hitMinion(minion,shot.damage,shot.owner,shot.impulse,dir);}}
  if(shield)shot.melee.returning=true;
  else if(target&&'material'in target&&shot.bounces>0){shot.bounces--;shot.vel=sub(shot.vel,mul(hit.normal1,2*dot(shot.vel,hit.normal1)));shot.pos=add(point,mul(norm(shot.vel),shot.radius+.04));shot.damage*=original?.stats.bounceRetention??.7;}
  else s.projectiles.delete(shot.id);return true;
 }
}
