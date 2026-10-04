import RAPIER from '@dimforge/rapier3d-compat';
import type {Player,Projectile,Simulation} from './simulation.js';
import type {Minion} from './chaos.js';
import {cameraPosition} from './view.js';
import {CONFIG,Input,Vec,v,add,sub,mul,len,norm,dot,forward,clamp,idleInput} from './config.js';
import {ARENA} from './arena.js';
import {canUse} from './access.js';
import {LEET,SLENDER} from './leet-content.js';
export interface LeetStatus {controlling?:string;controlledBy?:string;controlUntil?:number;frozenUntil?:number;scare?:{at:number;until:number;source:string};launchUntil?:number;viewYaw?:number;viewPitch?:number;}
/** Tree fields carry the Slenderman (2026-10-04 rework): walker = his feet, slender = his chaos-minion body (hp, every damage
 *  path), seen = his target is looking at him, sanity 0..1 (the victim's static; 1 = caught), hops = pages of tension,
 *  hopAt = when he may next relocate, from = where the last hop left. Views are quantised (see views()). */
export interface LeetField {id:string;kind:'nuke'|'tree';owner:string;pos:Vec;at:number;until:number;target?:string;walker?:Vec;
 slender?:string;seen?:boolean;sanity?:number;hops?:number;hopAt?:number;lostAt?:number;from?:Vec;yaw?:number;hp?:number;maxHp?:number;}
export interface LeetShot {kind:'bolt'|'brain'|'copy';target?:Vec;release?:number;launched?:boolean;}
const inactive=(p:Player)=>({...idleInput(),seq:p.input.seq,yaw:p.input.yaw,pitch:p.input.pitch});
export class LeetSystem {
 fields=new Map<string,LeetField>();private originals=new Map<string,Input>();private anchors=new Map<string,Vec>();private used=new Set<string>();private procs=new Map<string,number>();private history=new Map<string,{at:number;pos:Vec}[]>();
 constructor(readonly sim:Simulation){}
 active(p:Player|undefined,chip?:string):boolean{return !!p&&canUse(p,'weapon:leet')&&p.spec==='leet'&&(!chip||!!p.chips['l44t-'+chip]);}
 clear(){for(const f of this.fields.values())this.dropSlender(f);this.fields.clear();this.used.clear();this.procs.clear();this.history.clear();this.originals.clear();this.anchors.clear();for(const p of this.sim.players.values())p.leet={};}
 frozen(p:Player|undefined){return !!p?.leet?.scare||(p?.leet?.frozenUntil||0)>this.sim.time;}
 release(p:Player){const s=this.sim,ids=[p.id,p.leet?.controlling,p.leet?.controlledBy];for(const id of ids){const q=id?s.players.get(id):undefined;if(q?.leet){delete q.leet.controlling;delete q.leet.controlledBy;delete q.leet.controlUntil;delete q.leet.viewYaw;delete q.leet.viewPitch;}}}
 possess(owner:Player,target:Player){if(!this.active(owner,'brain')||owner===target||!this.sim.canHit(owner.id,target.id)||target.hp<=0||!target.connected||owner.leet?.controlledBy||target.leet?.controlling||target.leet?.controlledBy)return false;this.release(owner);owner.leet??={};target.leet??={};owner.leet.controlling=target.id;target.leet.controlledBy=owner.id;owner.leet.controlUntil=target.leet.controlUntil=this.sim.time+LEET.brainSeconds;target.cast=undefined;target.swing=null;owner.cast=undefined;owner.swing=null;this.sim.emit('leet-control',target.body.translation(),{source:owner.id,target:target.id,color:0x55ff88});return true;}
 routeInputs(){const s=this.sim;for(const p of s.players.values()){p.leet??={};if(p.leet.controlling){const q=s.players.get(p.leet.controlling);if(!q||!p.connected||!q.connected||p.hp<=0||q.hp<=0||(p.leet.controlUntil||0)<=s.time||!this.active(p,'brain'))this.release(p);}}
  for(const p of s.players.values()){if(!p.leet?.controlling)continue;const q=s.players.get(p.leet.controlling)!;this.originals.set(p.id,p.input);this.originals.set(q.id,q.input);q.input={...p.input,seq:q.input.seq};q.leet!.viewYaw=q.input.yaw;q.leet!.viewPitch=q.input.pitch;p.input=inactive(p);}
  for(const p of s.players.values())if(this.frozen(p)){if(!this.anchors.has(p.id))this.anchors.set(p.id,{...p.body.translation()});if(!this.originals.has(p.id))this.originals.set(p.id,p.input);p.input=inactive(p);p.cast=undefined;p.swing=null;p.burst=0;p.blockTime=0;p.guarding=false;p.move=v();p.ext=v();p.body.setLinvel(v(),true);}
 }
 restoreInputs(){for(const [id,pos]of this.anchors){const p=this.sim.players.get(id);if(p&&this.frozen(p)&&p.hp>0){p.body.setTranslation(pos,true);p.body.setLinvel(v(),true);}else this.anchors.delete(id);}for(const [id,input]of this.originals){const p=this.sim.players.get(id);if(p)p.input=input;}this.originals.clear();}
 visible(owner:string,pos:Vec){return [...this.sim.players.values()].filter(p=>this.sim.enemy(owner,p.id)&&p.connected&&p.hp>0&&len(sub(p.body.translation(),pos))<=LEET.lockRange&&this.sim.lineOfSight(pos,p.body.translation(),owner,p.id)).sort((a,b)=>len(sub(a.body.translation(),pos))-len(sub(b.body.translation(),pos)))[0];}
 spawned(owner:Player,attackId:string){if(!this.active(owner))return;for(const shot of this.sim.projectiles.values())if(shot.attackId===attackId){shot.leet={kind:'bolt'};shot.homing=0;shot.returning=false;shot.zigzag=false;shot.gravity=0;shot.life=LEET.shotLife;shot.chaos=undefined;}}
 orb(p:Player,pos:Vec,dir:Vec,kind:'brain'|'copy',target?:Vec,release?:number){const s=this.sim;if(s.projectiles.size>=CONFIG.maxProjectiles)return;const id=s.uid('leet-orb');s.projectiles.set(id,{id,pos:{...pos},vel:mul(dir,kind==='brain'?23:32),owner:p.id,originOwner:p.id,spec:'leet',damage:kind==='brain'?0:24,impulse:180,life:7,radius:kind==='brain'?.27:.16,direct:false,attackId:id,bounces:0,pierces:0,hit:new Set(),effectsUsed:true,homing:0,distanceLeft:100,gravity:0,leet:{kind,target:target?{...target}:undefined,release,launched:kind==='brain'}});}
 block(p:Player){if(!this.active(p,'brain'))return false;p.cast=undefined;p.swing=null;p.burst=0;p.guarding=false;p.blockCd=LEET.brainCooldown;p.blockTime=0;p.lastBlock=this.sim.time;this.orb(p,add(p.body.translation(),add(v(0,.2,0),mul(forward(p.input.yaw,p.input.pitch),.8))),forward(p.input.yaw,p.input.pitch),'brain');this.sim.emit('leet-brain',p.body.translation(),{source:p.id,color:0x55ff88});return true;}
 shotStep(shot:Projectile,dt:number){const s=this.sim;if(this.frozen(s.players.get(shot.owner)!))return false;const l=shot.leet;if(!l)return true;if(l.kind==='brain'||l.launched)return true;if((shot.reflections||0)>0){l.launched=true;return true;}if(!l.target){const enemy=this.visible(shot.owner,shot.pos);if(!enemy)return true;l.target={...enemy.body.translation()};l.release=s.time+LEET.lockPause;shot.fixedSpeed=Math.max(25,len(shot.vel));shot.vel=v();s.emit('leet-lock',shot.pos,{from:{...l.target},source:shot.owner,color:0x55ff88});}if(s.time<(l.release||0)){shot.life-=dt;if(shot.life<=0)s.projectiles.delete(shot.id);return false;}shot.vel=mul(norm(sub(l.target,shot.pos)),shot.fixedSpeed||32);l.launched=true;return true;}
 impact(owner:Player,shot:Projectile,point:Vec){const s=this.sim;if(!this.active(owner)||!shot.direct||this.procs.has(shot.attackId))return;this.procs.set(shot.attackId,s.time+10);
 if(this.active(owner,'nuke')&&[...this.fields.values()].filter(f=>f.owner===owner.id&&f.kind==='nuke').length<3){const id=s.uid('nuke');this.fields.set(id,{id,kind:'nuke',owner:owner.id,pos:{...point},at:s.time,until:s.time+LEET.nukeDelay});}
 if(this.active(owner,'copy'))for(let i=0;i<3;i++)this.orb(owner,add(point,v((i-1)*1.1,2.5,.4)),v(),'copy',point,s.time+.5+i*.16);
 if(this.active(owner,'uprising')){const target=this.visible(owner.id,point);if(target)for(const prop of [...s.props.values()].filter(p=>p.dynamic&&p.material!=='foundation'&&len(sub(p.body.translation(),point))<10).slice(0,8)){const dir=norm(sub(add(target.body.translation(),v(0,.3,0)),prop.body.translation()));s.launchProp(prop,mul(dir,prop.mass*45),owner.id);s.emit('leet-object',prop.body.translation(),{from:{...target.body.translation()},source:owner.id,color:0x55ff88});}}
 }
 secondary(p:Player){const s=this.sim;if(!this.active(p)||!s.spendResource(p,30))return;p.secondaryCd=LEET.secondaryCooldown;p.cooldown=.4;p.lastSecondary=p.lastAttack=s.time;
 if(this.active(p,'time')){for(const q of s.players.values())if(q!==p&&s.canHit(p.id,q.id)&&q.hp>0){q.leet??={};q.leet.frozenUntil=s.time+LEET.freezeSeconds;}s.emit('leet-freeze',p.body.translation(),{source:p.id,color:0x55ff88,radius:28});}
 if(this.active(p,'scare')){for(const [id,f]of this.fields)if(f.owner===p.id&&f.kind==='tree'){this.dropSlender(f);this.fields.delete(id);}const pos=add(p.body.translation(),mul(forward(p.input.yaw),2));pos.y=.05;const id=s.uid('tree');this.fields.set(id,{id,kind:'tree',owner:p.id,pos,at:s.time,until:s.time+SLENDER.life});}
 if(!this.active(p,'time')&&!this.active(p,'scare'))s.explode(p,p.body.translation(),6,35,1200,'root',true);
 }
 /** A mid-round sandbox respawn re-arms CTRL+Z and forgets the rewind trail (a round reset does this for everyone). */
 forget(id:string){this.used.delete(id);this.history.delete(id);}
 save(p:Player,amount:number,kind:string){if(kind==='leet-scare'||!this.active(p,'undo')||this.used.has(p.id)||amount<p.hp)return false;this.used.add(p.id);const past=this.history.get(p.id)?.[0];if(past)p.body.setTranslation(past.pos,true);this.sim.heal(p,p.stats.hp);p.ext=v();p.move=v();p.body.setLinvel(v(),true);p.burns.clear();p.poisons.clear();this.sim.emit('leet-undo',p.body.translation(),{source:p.id,color:0x55ff88,radius:3});return true;}
 scare(owner:Player,target:Player){if(!this.sim.canHit(owner.id,target.id)||target.leet?.scare||target.hp<=0)return;target.leet??={};target.leet.scare={at:this.sim.time,until:this.sim.time+LEET.scareSeconds,source:owner.id};target.cast=undefined;target.swing=null;target.blockTime=0;this.sim.emit('leet-scare',target.body.translation(),{source:owner.id,target:target.id,color:0x55ff88});}
 step(dt:number){const s=this.sim;for(const [key,until]of this.procs)if(until<s.time)this.procs.delete(key);for(const p of s.players.values()){
  if(p.leet?.scare&&s.time>=p.leet.scare.until){const source=p.leet.scare.source;delete p.leet.scare;s.damage(p,1e9,source,'leet-scare');}
  if(this.active(p,'undo')&&p.hp>0&&s.tick%6===0&&p.body.translation().y>0&&p.body.translation().y<8&&Math.abs(p.body.translation().x)<ARENA.width/2-2&&Math.abs(p.body.translation().z)<ARENA.depth/2-2){const a=this.history.get(p.id)||[];a.push({at:s.time,pos:{...p.body.translation()}});while(a.length&&a[0].at<s.time-3)a.shift();this.history.set(p.id,a);}
 }
 for(const [id,f]of this.fields){const owner=s.players.get(f.owner);if(!owner||!owner.connected||f.kind==='tree'&&owner.hp<=0){this.dropSlender(f);this.fields.delete(id);continue;}if(f.until<=s.time){this.dropSlender(f);this.fields.delete(id);if(f.kind==='nuke'){s.explode(owner,f.pos,LEET.nukeRadius,LEET.nukeDamage,LEET.nukeImpulse,'leet-nuke');s.emit('leet-nuke',f.pos,{source:owner.id,color:0x55ff88,radius:LEET.nukeRadius});}continue;}if(f.kind==='tree'&&s.time-f.at>=SLENDER.emerge&&!this.stalk(f,owner,dt))this.fields.delete(id);}
 }
 private dropSlender(f:LeetField){const m=f.slender?this.sim.chaos.minions.get(f.slender):undefined;if(m)this.sim.chaos.removeMinion(m,false);}
 /** Does `p` see the Slenderman? The authoritative camera (input yaw/pitch/camera mode) must face him within the view cone and
  *  have a clear line to his head or chest. */
 observed(p:Player,feet:Vec,slender:string){const s=this.sim,pos=p.body.translation(),eye=cameraPosition(pos,p.input.yaw,p.input.pitch,p.input.camera==='first'?'first':'third',(o,d,l)=>s.ray(o,d,l,p.id)?.timeOfImpact??null),look=forward(p.input.yaw,p.input.pitch);
  for(const y of [2.2,1.35]){const point=add(feet,v(0,y,0)),d=sub(point,eye),l=len(d);if(l>SLENDER.sight||dot(norm(d),look)<Math.cos(SLENDER.fov*Math.PI/180))continue;if(s.lineOfSight(eye,point,p.id,slender))return true;}return false;}
 /** One Slenderman tick (lore of Slender: The Eight Pages / The Arrival, adapted to the arena). False: the field ends. */
 private stalk(f:LeetField,owner:Player,dt:number):boolean{const s=this.sim;
  if(!f.slender){const feet=add(f.pos,v(.9,0,.2)),id=s.uid('slender'),body=s.world.createRigidBody(RAPIER.RigidBodyDesc.kinematicPositionBased().setTranslation(feet.x,feet.y+SLENDER.center,feet.z)),collider=s.world.createCollider(RAPIER.ColliderDesc.capsule(SLENDER.half,SLENDER.radius),body);
   const m:Minion={id,owner:owner.id,kind:'slender',pos:feet,yaw:0,hp:SLENDER.hp,maxHp:SLENDER.hp,speed:0,until:f.until,lastAttack:-10,nextAttack:Infinity,body,collider};
   s.chaos.minions.set(id,m);s.colliderOwners.set(collider.handle,id);f.slender=id;f.walker=feet;f.sanity=0;f.hops=0;f.hopAt=s.time+SLENDER.hopFirst;f.lostAt=s.time;f.seen=false;s.emit('summon',feet,{source:owner.id,message:'slender'});}
  const m=s.chaos.minions.get(f.slender);if(!m)return false;// destroyed: minionDeath was emitted by the hit
  f.hp=m.hp;f.maxHp=m.maxHp;const c=m.body.translation(),feet=v(c.x,c.y-SLENDER.center,c.z);f.walker=feet;
  let target=f.target?s.players.get(f.target):undefined;
  if(!target||target.hp<=0||!target.connected||target.leet?.scare||!s.enemy(owner.id,target.id)){target=[...s.players.values()].filter(q=>s.enemy(owner.id,q.id)&&q.connected&&q.hp>0&&!q.leet?.scare&&!s.pve?.hidden(q)).sort((a,b)=>len(sub(a.body.translation(),c))-len(sub(b.body.translation(),c)))[0];if(target?.id!==f.target){f.target=target?.id;f.sanity=0;}}
  if(!target){f.seen=false;return true;}
  const tp=target.body.translation(),gap=Math.hypot(tp.x-c.x,tp.z-c.z),seen=this.observed(target,feet,m.id);
  m.yaw=f.yaw=Math.atan2(tp.x-c.x,tp.z-c.z);
  if(seen){if(!f.seen)f.lostAt=undefined;f.sanity=Math.min(1,(f.sanity||0)+dt*(SLENDER.stare+SLENDER.close*clamp(1-gap/SLENDER.closeRange,0,1)));}
  else{if(f.seen||f.lostAt===undefined)f.lostAt=s.time;f.sanity=Math.max(0,(f.sanity||0)-dt*SLENDER.decay);}
  f.seen=seen;
  if(gap<=SLENDER.reach||(f.sanity||0)>=1){this.scare(owner,target);this.dropSlender(f);return false;}
  // Unobserved for a beat and off cooldown: relocate behind the target (the edge of vision), closer each time.
  if(!seen&&s.time>=(f.hopAt||0)&&s.time-(f.lostAt??s.time)>=SLENDER.unseen){const life=Math.max(1,f.until-f.at),aggr=clamp((s.time-f.at)/life,0,1),hops=f.hops||0,
    noise=Math.sin((hops+1)*12.9898+f.at*78.233)*43758.5453,r=noise-Math.floor(noise),angle=target.input.yaw+Math.PI+(r-.5)*2*SLENDER.behind,
    want=Math.max(SLENDER.reach-.25,gap*(SLENDER.shrink[0]+(SLENDER.shrink[1]-SLENDER.shrink[0])*aggr)),dir=forward(angle),eye=add(tp,v(0,.2,0)),wall=s.ray(eye,dir,want,target.id);
   let at=wall?Math.max(Math.min(want,SLENDER.reach-.25),wall.timeOfImpact-.45):want;
   const spot=add(tp,mul(dir,at));spot.x=clamp(spot.x,-31,31);spot.z=clamp(spot.z,-27,27);spot.y=tp.y-CONFIG.playerHalfHeight-CONFIG.playerRadius;at=Math.hypot(spot.x-tp.x,spot.z-tp.z);
   f.from=feet;f.hops=hops+1;f.hopAt=s.time+SLENDER.hop[0]+(SLENDER.hop[1]-SLENDER.hop[0])*aggr;m.body.setTranslation(v(spot.x,spot.y+SLENDER.center,spot.z),true);f.walker=spot;m.yaw=f.yaw=Math.atan2(tp.x-spot.x,tp.z-spot.z);
   s.emit('leet-slender-hop',spot,{from:feet,source:owner.id,target:target.id,amount:f.hops,message:f.id,color:0x55ff88});}
  return true;
 }
 views(){const r=(x:number|undefined)=>x===undefined?undefined:Math.round(x*100)/100;return [...this.fields.values()].map(f=>({...f,pos:{...f.pos},walker:f.walker?{...f.walker}:undefined,from:f.from?{...f.from}:undefined,sanity:r(f.sanity),hp:f.hp===undefined?undefined:Math.ceil(f.hp),hopAt:r(f.hopAt),lostAt:undefined,yaw:r(f.yaw)}));}
}
