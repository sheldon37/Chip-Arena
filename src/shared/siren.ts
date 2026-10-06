import {SIREN_HALL} from './siren-stage-layout.js';
import {sirenMotion} from './siren-motion.js';
import {sirenSuperArmor} from './siren-motion.js';
import RAPIER from '@dimforge/rapier3d-compat';
import type {Player,Projectile,Simulation,GameEvent} from './simulation.js';
import {canUse} from './access.js';
import {CONFIG,v,add,sub,mul,len,norm,dot,forward,clamp,type Vec} from './config.js';
import {rolling,rollInvulnerable} from './souls-combat.js';
import {demonStepInvulnerable} from './demon-step.js';
import {downProtected} from './recovery.js';
import {cameraPosition} from './view.js';
import {SIREN_COMBAT as B} from './siren-content.js';
import {SIREN,SIREN_SLOTS,SIREN_TIMING as T,sirenPoint,SIREN_SPAWN,type SirenMoveId,type SirenMoveState,type SirenPlayerState,type SirenFirePatch,type SirenEruption,type SirenRain,type SirenMark,type SirenGrab,type SirenWorldView,type SirenShot,type SirenEventType} from './siren-contract.js';

const NO_BOOST:import('./siren-contract.js').SirenBoost={scale:1,reach:1,cooldown:1,at:0};const boostOf=(p:Player)=>p.siren?.boost??NO_BOOST;
/** Authority only. Snapshot fields come from the shared contract; no client clock drives damage. */
export class SirenSystem {
 private actions=new Map<string,{move:SirenMoveState;fired:boolean;hit:Set<string>;trailAt:number;locked:boolean}>();
 private held=new Map<string,{toggle:boolean;ability:number}>();
 private serial=0;private side=new Map<string,1|-1>();
 patches:SirenFirePatch[]=[];eruptions:(SirenEruption&{fired:boolean})[]=[];
 rains:(SirenRain&{next:number})[]=[];marks:SirenMark[]=[];grabs:SirenGrab[]=[];
 private fieldNext=0;
 constructor(private sim:Simulation){}
 active(p:Player|undefined):p is Player{return !!p&&p.spec===SIREN.spec&&canUse(p,SIREN.grants.spec);}
 state(p:Player):SirenPlayerState{return p.siren??={mode:'melee',modeAt:-10,ready:{}};}
 armored(p:Player){return this.sim.phase==='fight'&&this.active(p)&&sirenSuperArmor(p,this.sim.time);}
 busy(p:Player){return this.actions.has(p.id)||this.grabs.some(g=>g.victim===p.id);}
 heldVictim(p:Player){return this.grabs.some(g=>g.victim===p.id);}
 ready(p:Player){return this.active(p)&&p.connected&&p.hp>0&&this.sim.phase==='fight'&&!this.sim.leet.frozen(p)&&!this.sim.arcade.stunned(p)&&!rolling(p,this.sim.time)&&!this.heldVictim(p)&&p.blockTime<=0&&!this.sim.pve?.hidden(p);}
 private event(p:Player,type:SirenEventType,pos:Vec=p.body.translation(),extra:Partial<GameEvent>={}){this.sim.emit(type,pos,{source:p.id,spec:'siren',...extra});}
 private release(grab:SirenGrab){const t=this.sim.players.get(grab.victim);if(t){t.body.setGravityScale(1,true);t.body.setLinvel(v(),true);t.ext=v();t.move=v();}this.grabs=this.grabs.filter(g=>g!==grab);}
 cancel(p:Player){this.actions.delete(p.id);if(p.siren)p.siren.move=undefined;for(const g of [...this.grabs])if(g.owner===p.id||g.victim===p.id)this.release(g);this.marks=this.marks.filter(m=>m.owner!==p.id||!!m.shot);}
 remove(p:Player){this.cancel(p);this.held.delete(p.id);this.side.delete(p.id);this.patches=this.patches.filter(f=>f.owner!==p.id);this.eruptions=this.eruptions.filter(f=>f.owner!==p.id);this.rains=this.rains.filter(f=>f.owner!==p.id);this.marks=this.marks.filter(f=>f.owner!==p.id&&f.target!==p.id);for(const [id,q]of this.sim.projectiles)if(q.siren&&(q.owner===p.id||q.siren.target===p.id))this.sim.projectiles.delete(id);p.siren=undefined;}
 clear(){for(const g of [...this.grabs])this.release(g);this.actions.clear();this.held.clear();this.side.clear();this.patches=[];this.eruptions=[];this.rains=[];this.marks=[];this.fieldNext=0;for(const p of this.sim.players.values())p.siren=undefined;}
 /** PvE holds and possession: releases p's grabs and action and drops its patches, eruptions, rains, marks and Siren projectiles, but keeps p.siren (unlike remove). */
 clearEffects(p:Player){this.cancel(p);this.patches=this.patches.filter(f=>f.owner!==p.id);this.eruptions=this.eruptions.filter(f=>f.owner!==p.id);this.rains=this.rains.filter(f=>f.owner!==p.id);this.marks=this.marks.filter(f=>f.owner!==p.id);for(const [id,q]of this.sim.projectiles)if(q.siren&&q.owner===p.id)this.sim.projectiles.delete(id);}
 input(p:Player){if(!this.active(p))return;const prev=this.held.get(p.id)||{toggle:false,ability:0},toggle=p.input.sirenToggle===true,ability=p.input.sirenAbility||0;this.held.set(p.id,{toggle,ability});
  if(toggle&&!prev.toggle)this.toggle(p);if(ability&&ability!==prev.ability)this.start(p,SIREN_SLOTS[this.state(p).mode].abilities[ability-1]);
 }
 toggle(p:Player){if(!this.ready(p)||this.busy(p)||p.cooldown>0)return false;const a=this.state(p);a.mode=a.mode==='melee'?'ranged':'melee';a.modeAt=this.sim.time;p.cooldown=T.swap;this.event(p,'siren-mode',p.body.translation(),{message:a.mode});return true;}
 primary(p:Player){return this.start(p,SIREN_SLOTS[this.state(p).mode].primary);}
 private visibleTarget(p:Player,range:number,aimed=false){const origin=add(p.body.translation(),v(0,.45,0)),dir=forward(p.input.yaw,p.input.pitch);return [...this.sim.players.values()].filter(t=>t.connected&&t.hp>0&&this.sim.enemy(p.id,t.id)&&len(sub(t.body.translation(),origin))<=range&&(!aimed||dot(norm(sub(t.body.translation(),origin)),dir)>.7)&&this.sim.lineOfSight(origin,t.body.translation(),p.id,t.id,!!this.sim.pve)).sort((a,b)=>len(sub(a.body.translation(),origin))-len(sub(b.body.translation(),origin))||a.id.localeCompare(b.id))[0];}
 ground(pos:Vec){const s=this.sim,origin=add(pos,v(0,1,0));
  // Inside the sealed Ward Zero hall, never start above the roof: an Eruption aimed at the vault lands on the floor below it.
  if(s.options.map==='siren')origin.y=Math.min(origin.y,SIREN_HALL.roof-.05);
  const hit=s.world.castRay(new RAPIER.Ray(origin,v(0,-1,0)),40,true,undefined,undefined,undefined,undefined,(c:RAPIER.Collider)=>s.props.has(s.colliderOwners.get(c.handle)||''));return hit?add(origin,v(0,-hit.timeOfImpact,0)):v(pos.x,0,pos.z);}
 aim(p:Player){const s=this.sim,dir=forward(p.input.yaw,p.input.pitch),camera=cameraPosition(p.body.translation(),p.input.yaw,p.input.pitch,p.input.camera||'third',(a,d,n)=>s.ray(a,d,n,p.id,!!s.pve)?.timeOfImpact??null),hit=s.ray(camera,dir,B.aimRange,p.id,!!s.pve),point=add(camera,mul(dir,hit?.timeOfImpact??B.aimRange)),delta=sub(point,p.body.translation());return this.ground(add(p.body.translation(),mul(norm(delta),Math.min(B.aimRange,len(delta)))));}
 start(p:Player,id:SirenMoveId){const s=this.sim,a=this.state(p),slots=SIREN_SLOTS[a.mode];if(!this.ready(p)||this.busy(p)||p.cooldown>0||(a.ready?.[id]||0)>s.time||!(slots.primary===id||slots.abilities.includes(id)))return false;
   if(a.boost&&(id==='rain'&&this.rains.filter(r=>r.owner===p.id).length>=(a.boost.maxRains??Infinity)||id==='eruption'&&this.eruptions.filter(e=>e.owner===p.id).length>=(a.boost.maxEruptions??Infinity)))return false;
  if(id==='mark'&&this.marks.some(m=>m.owner===p.id))return false;const target=id==='mark'?this.visibleTarget(p,B.mark.range,true):undefined;if(id==='mark'&&!target)return false;
  const at=s.time,t=T[id],windup='windup'in t?t.windup:id==='mark'?T.mark.voice+T.mark.lock:0;
  const m:SirenMoveState={id,seq:++this.serial,at,hitAt:at+windup,until:at+windup+('recover'in t?t.recover:0)};
  if(id==='swipe'||id==='rend')m.until+=T[id].active;
  if(id==='swipe'||id==='hellfire'){m.side=this.side.get(p.id)||1;this.side.set(p.id,m.side===1?-1:1);}
  if(id==='rush'){m.dir=forward(p.input.yaw);m.endAt=m.hitAt+T.rush.dash;m.until=m.endAt+T.rush.recover;}
  if(id==='laugh'){m.endAt=m.hitAt+T.laugh.lunge;m.until=m.endAt+T.laugh.whiff;this.event(p,'siren-laugh');}
  if(id==='eruption'||id==='rain')m.aim=this.aim(p);
  if(id==='mark'){m.target=target!.id;m.endAt=at+T.mark.voice;this.marks.push({id:s.uid('siren-mark'),owner:p.id,target:target!.id,at,lockAt:m.endAt,throwAt:m.hitAt});this.event(p,'siren-mark',p.body.translation(),{target:target!.id});}
  a.move=m;if(sirenSuperArmor(p,s.time))p.hitReaction=undefined;a.ready??={};const tuning=B[id as keyof typeof B];a.ready[id]=at+Math.max((typeof tuning==='object'&&'cooldown'in tuning?tuning.cooldown:m.until-at)*boostOf(p).cooldown,boostOf(p).minRecast?.[id]??0);if(a.boost)m.reach=id==='laugh'?1:a.boost.reach;p.cooldown=m.until-at;p.lastAttack=at;p.attackIndex++;this.actions.set(p.id,{move:m,fired:false,hit:new Set(),trailAt:0,locked:false});return true;
 }
 private avoid(t:Player){return rollInvulnerable(t,this.sim.time)||demonStepInvulnerable(t,this.sim.time)||this.sim.arcade.invulnerable(t)||downProtected(t.knockdown,this.sim.time)||this.sim.pve?.untouchable(t)===true;}
 hit(p:Player,t:Player,damage:number,id:SirenMoveId,melee=false,dir=norm(sub(t.body.translation(),p.body.translation()))){if(!t.connected||t.hp<=0||!this.sim.enemy(p.id,t.id)||this.avoid(t))return 0;if(t.blockTime>0){this.sim.successfulBlock(t,melee?'melee':'projectile',t.body.translation(),p.id,damage,dir,100);return 0;}const actual=this.sim.damage(t,damage*p.stats.damage,p.id,'siren-'+id,dir,0,add(t.body.translation(),v(0,.2,0)),melee);if(actual>0)this.event(p,'siren-hit',t.body.translation(),{target:t.id,amount:actual,message:id});return actual;}
 private interrupt(t:Player){this.sim.arcade.cancel(t);this.sim.emperor.cancel(t);this.sim.standardMelee.cancel(t);this.cancel(t);t.swing=null;t.cast=undefined;t.burst=0;t.roll=undefined;t.dashTime=0;t.waveTime=0;t.blockTime=0;t.guarding=false;t.move=v();}
 private launch(p:Player,t:Player,speed:number){if(t.hp<=0||this.armored(t))return;this.interrupt(t);t.knockdown=undefined;t.hitStunUntil=this.sim.time+.45;t.juggle={at:this.sim.time,until:this.sim.time+2,hits:1,lastHit:this.sim.time,source:p.id,spinUntil:0,bounced:false,wallUsed:false};t.ext.y=speed-t.body.linvel().y;t.grounded=false;}
 private melee(p:Player,m:SirenMoveState,hit:Set<string>,range:number,damage:number,arc:number){const s=this.sim,pos=p.body.translation(),dir=forward(p.input.yaw);for(const t of s.players.values()){const d=sub(t.body.translation(),pos);if(hit.has(t.id)||Math.hypot(d.x,d.z)>range+.35||Math.abs(d.y)>1.9*boostOf(p).scale||dot(norm(v(d.x,0,d.z)),dir)<Math.cos(arc/2)||!s.lineOfSight(pos,t.body.translation(),p.id,t.id,!!s.pve))continue;hit.add(t.id);if(this.hit(p,t,damage,m.id,true)>0&&m.id==='rend'&&t.hp>0&&!this.armored(t)){this.interrupt(t);t.knockdown={at:s.time,until:s.time+1.2,source:p.id};t.ext=add(mul(dir,3),v(0,2.5,0));}if(p.hp<=0||s.arcade.stunned(p))break;}}
 private area(p:Player,pos:Vec,radius:number,damage:number,id:SirenMoveId,launch=0,falloff=false){for(const t of this.sim.players.values()){const d=sub(t.body.translation(),pos),dist=Math.hypot(d.x,d.z);if(dist>radius+.35||Math.abs(d.y)>4*boostOf(p).scale||!this.sim.lineOfSight(add(pos,v(0,.12,0)),t.body.translation(),p.id,t.id,!!this.sim.pve))continue;if(this.hit(p,t,damage*(falloff?Math.max(.15,1-dist/(radius+.4)):1),id)>0&&launch)this.launch(p,t,launch);}}
 patch(p:Player,pos:Vec,kind:SirenFirePatch['kind']='hellfire'){const group=this.patches.filter(f=>f.owner===p.id&&f.kind===kind),cap=kind==='trail'?B.trailCap:B.hellfire.patchCap;if(group.length>=cap)this.patches=this.patches.filter(f=>f!==group[0]);this.patches.push({id:this.sim.uid('siren-fire'),owner:p.id,kind,pos:this.ground(pos),radius:(kind==='trail'?.85:B.hellfire.patchRadius)*boostOf(p).reach,at:this.sim.time,until:this.sim.time+(kind==='trail'?B.rush.trailSeconds:B.hellfire.patchSeconds)});}
 spawn(p:Player,kind:SirenShot['kind'],extra:Partial<SirenShot>={},start?:Vec){const s=this.sim;if(s.projectiles.size>=CONFIG.maxProjectiles)return;const spawn=kind==='damnation'?SIREN_SPAWN.overhead:kind==='seeker'?SIREN_SPAWN.seeker:this.state(p).move?.side===-1?SIREN_SPAWN.leftHand:SIREN_SPAWN.rightHand,pos=start||sirenPoint(p.body.translation(),p.input.yaw,{x:spawn.x*boostOf(p).scale,y:spawn.y*boostOf(p).scale,z:spawn.z*boostOf(p).scale}),id=s.uid('siren-shot');
  const damage=kind==='drop'?B.rain.damage:kind==='seeker'?B.mark.damage:B[kind].damage,speed=kind==='drop'?B.rain.height/B.rain.fallSeconds:kind==='seeker'?B.mark.speed:B[kind].speed;
  const dir=kind==='drop'?v(0,-1,0):forward(p.input.yaw,p.input.pitch);
  const shot:Projectile={id,owner:p.id,spec:'siren',siren:{kind,born:s.time,...extra},pos:{...pos},vel:mul(dir,speed),damage,impulse:0,life:kind==='seeker'?1e9:5,radius:(kind==='drop'?.12:kind==='seeker'?B.mark.radius:B[kind].radius)*boostOf(p).reach,gravity:kind==='damnation'?B.damnation.gravity:0,distanceLeft:200,direct:false,attackId:id,bounces:0,pierces:0,hit:new Set(),effectsUsed:false,homing:0};s.projectiles.set(id,shot);return shot;
 }
 stepPlayer(p:Player,dt:number){if(!this.active(p))return;if(!this.ready(p)){this.cancel(p);return;}const s=this.sim,a=this.actions.get(p.id);if(!a)return;const m=a.move;
  if(m.id==='mark'&&!a.locked&&s.time>=m.endAt!){a.locked=true;const t=s.players.get(m.target!);if(t?.hp&&t.connected)this.event(p,'siren-lock',t.body.translation(),{target:t.id});else{this.cancel(p);return;}}
  if(!a.fired&&s.time>=m.hitAt){a.fired=true;
   switch(m.id){
    case 'swipe':case 'rend':this.event(p,m.id==='swipe'?'siren-swipe':'siren-rend',p.body.translation(),{radius:B[m.id].range*boostOf(p).reach,amount:m.side});break;
    case 'slam':{const pos=this.ground(p.body.translation());this.area(p,pos,B.slam.radius*boostOf(p).reach,B.slam.damage,'slam',B.slam.launch);this.event(p,'siren-slam',pos,{radius:B.slam.radius*boostOf(p).reach});break;}
    case 'rush':this.event(p,'siren-rush',p.body.translation(),{from:add(p.body.translation(),mul(m.dir!,B.rush.distance))});break;
    case 'laugh':{const t=this.visibleTarget(p,B.laugh.range);m.target=t?.id;m.dir=t?norm(v(t.body.translation().x-p.body.translation().x,0,t.body.translation().z-p.body.translation().z)):forward(p.input.yaw);this.event(p,'siren-lunge',p.body.translation(),{target:t?.id});break;}
    case 'hellfire':case 'damnation':{const shot=this.spawn(p,m.id);if(shot)this.event(p,'siren-cast',shot.pos,{message:m.id});break;}
    case 'eruption':this.eruptions.push({id:s.uid('siren-eruption'),owner:p.id,pos:m.aim!,radius:B.eruption.radius*boostOf(p).reach,at:s.time,burstAt:s.time+T.eruption.warning,until:s.time+T.eruption.warning+T.eruption.burst,fired:false});this.event(p,'siren-cast',m.aim!,{message:m.id});break;
    case 'rain':this.rains.push({id:s.uid('siren-rain'),owner:p.id,center:m.aim!,radius:B.rain.radius*boostOf(p).reach,at:s.time,until:s.time+T.rain.duration,next:s.time});this.event(p,'siren-cast',m.aim!,{message:m.id});break;
    case 'mark':{const target=s.players.get(m.target!),mark=this.marks.find(x=>x.owner===p.id&&!x.shot);if(!target?.hp||!target.connected||!mark){this.cancel(p);return;}const shot=this.spawn(p,'seeker',{target:target.id});if(shot){mark.shot=shot.id;this.event(p,'siren-cast',shot.pos,{message:'mark'});}else this.marks=this.marks.filter(x=>x!==mark);break;}
   }
  }
  if(m.id==='swipe'||m.id==='rend'){if(s.time>=m.hitAt&&s.time<m.hitAt+T[m.id].active)this.melee(p,m,a.hit,B[m.id].range*boostOf(p).reach,B[m.id].damage,B[m.id].arc);}
  if(m.id==='rush'&&s.time>=m.hitAt&&s.time<m.endAt!){this.melee(p,m,a.hit,B.rush.radius*boostOf(p).reach,B.rush.damage,Math.PI*2);if(s.time>=a.trailAt){this.patch(p,p.body.translation(),'trail');a.trailAt=s.time+.07;}}
  if(m.id==='laugh'&&!m.caught&&s.time>=m.hitAt&&s.time<m.endAt!){const t=s.players.get(m.target||'');if(t?.hp&&t.connected&&!this.avoid(t)&&!this.heldVictim(t)&&len(sub(t.body.translation(),p.body.translation()))<=B.laugh.catchRadius*boostOf(p).reach&&s.lineOfSight(p.body.translation(),t.body.translation(),p.id,t.id,!!s.pve)){this.interrupt(t);m.caught=t.id;m.caughtAt=s.time;m.crushAt=s.time+T.laugh.crush;m.until=s.time+T.laugh.release;p.cooldown=m.until-s.time;this.grabs.push({owner:p.id,victim:t.id,at:s.time,crushAt:m.crushAt,releaseAt:m.until});this.event(p,'siren-catch',t.body.translation(),{target:t.id});}}
  if(m.id==='laugh'&&!m.caught&&s.time>=m.endAt!&&!a.hit.has('whiff')){a.hit.add('whiff');this.event(p,'siren-whiff');}
  if(s.time>=m.until){this.actions.delete(p.id);this.state(p).move=undefined;}
 }
 /** Capsule motion stays standard; the visual facade supplies hover. Dash speed is integrated by the controller. */
 motion(p:Player,dt:number){return sirenMotion(this.actions.get(p.id)?.move,this.sim.time,dt);}
 pinVictim(p:Player){const g=this.grabs.find(g=>g.victim===p.id);if(!g)return false;const owner=this.sim.players.get(g.owner);if(!owner?.hp||!owner.connected||p.hp<=0){this.release(g);return false;}const lift=clamp((this.sim.time-g.at)/T.laugh.lift,0,1),pos=sirenPoint(owner.body.translation(),owner.input.yaw,{x:0,y:(.2+lift*.75)*boostOf(owner).scale,z:boostOf(owner).scale});p.body.setTranslation(pos,true);p.body.setLinvel(v(),true);p.body.setGravityScale(0,true);p.move=v();p.ext=v();return true;}
 step(dt:number){const s=this.sim;for(const g of [...this.grabs]){const p=s.players.get(g.owner),t=s.players.get(g.victim);if(!p?.hp||!p.connected||!t?.hp||!t.connected){this.release(g);continue;}if(s.time>=g.crushAt){if(s.pve){s.pve.sirenCrush(p,t);this.event(p,'siren-crush',add(t.body.translation(),v(0,.65,0)),{target:t.id});this.release(g);continue;}t.hp=0;t.lastSource=p.id;t.sourceAt=s.time;t.lastDamager={id:p.id,name:p.name};const point=add(t.body.translation(),v(0,.65,0));s.recordDeath(t,'siren-crush',v(),point,true);this.event(p,'siren-crush',point,{target:t.id});this.release(g);}else this.pinVictim(t);}
  for(const f of this.eruptions){const p=s.players.get(f.owner);if(!f.fired&&p?.hp&&p.connected&&s.time>=f.burstAt){f.fired=true;this.area(p,f.pos,f.radius,B.eruption.damage,'eruption',B.eruption.launch);this.event(p,'siren-eruption',f.pos,{radius:f.radius});}}
  for(const r of this.rains){const p=s.players.get(r.owner);if(!p?.hp||!p.connected)continue;while(s.time>=r.next&&r.next<r.until){r.next+=B.rain.dropEvery;const angle=s.random()*Math.PI*2,radius=Math.sqrt(s.random())*r.radius,land=this.ground(add(r.center,v(Math.cos(angle)*radius,0,Math.sin(angle)*radius)));this.spawn(p,'drop',{land,landAt:s.time+B.rain.fallSeconds},add(land,v(0,B.rain.height,0)));}}
  if(s.time>=this.fieldNext){this.fieldNext=s.time+B.fieldTick;const touched=new Set<string>();for(const f of this.patches){const p=s.players.get(f.owner);if(!p?.hp||!p.connected||s.time>=f.until)continue;for(const t of s.players.values()){const pos=t.body.translation(),key=f.owner+':'+t.id;if(touched.has(key)||Math.hypot(pos.x-f.pos.x,pos.z-f.pos.z)>f.radius+.3||Math.abs(pos.y-f.pos.y)>.95||!s.lineOfSight(add(f.pos,v(0,.15,0)),pos,p.id,t.id))continue;touched.add(key);this.hit(p,t,B.fireDps*B.fieldTick,'hellfire');}}}
  const alive=(owner:string)=>{const p=s.players.get(owner);return p?.hp&&p.connected&&this.active(p);};this.patches=this.patches.filter(f=>f.until>s.time&&alive(f.owner));this.eruptions=this.eruptions.filter(f=>f.until>s.time&&alive(f.owner));this.rains=this.rains.filter(f=>f.until>s.time&&alive(f.owner));this.marks=this.marks.filter(m=>alive(m.owner)&&s.players.get(m.target)?.hp&&(!m.shot||s.projectiles.has(m.shot)));void dt;
 }
 stepShot(shot:Projectile,dt:number){if(!shot.siren)return false;const s=this.sim,p=s.players.get(shot.owner),data=shot.siren;if(!p?.hp||!p.connected||!this.active(p)){s.projectiles.delete(shot.id);return true;}const target=data.target?s.players.get(data.target):undefined;
  if(data.kind==='seeker'){if(!target?.hp||!target.connected){s.projectiles.delete(shot.id);return true;}shot.vel=mul(norm(sub(add(target.body.translation(),v(0,.2,0)),shot.pos)),B.mark.speed);}else{shot.life-=dt;if(shot.life<=0){s.projectiles.delete(shot.id);return true;}}
  const displacement=add(mul(shot.vel,dt),v(0,-shot.gravity*dt*dt/2,0));shot.vel.y-=shot.gravity*dt;let remaining=len(displacement);const dir=norm(displacement);
  for(let attempts=0;remaining>1e-6&&attempts<CONFIG.maxBodies+CONFIG.maxPlayers;attempts++){
   const contact=s.world.castShape(shot.pos,{x:0,y:0,z:0,w:1},dir,new RAPIER.Ball(shot.radius),0,remaining,true,undefined,undefined,undefined,undefined,(c:RAPIER.Collider)=>{const id=s.colliderOwners.get(c.handle)||'',t=s.players.get(id);if(t)return t.id!==p.id&&t.connected&&t.hp>0&&!this.avoid(t)&&s.enemy(p.id,t.id)&&(data.kind!=='seeker'||t.id===data.target);return s.props.has(id);});
   if(!contact){shot.pos=add(shot.pos,mul(dir,remaining));break;}const distance=contact.time_of_impact,point=add(shot.pos,mul(dir,distance)),id=s.colliderOwners.get(contact.collider.handle)||'',prop=s.props.get(id),victim=s.players.get(id);shot.pos=point;
   if(data.kind==='seeker'&&prop&&prop.material!=='foundation'){s.removeProp(prop.id);s.emit('break',point,{source:p.id,material:prop.material});s.checkSupports(p.id);remaining-=distance;continue;}
   if(data.kind==='seeker'){if(prop)this.event(p,'siren-seeker-out',point);else if(victim){this.hit(p,victim,B.mark.damage,'mark');this.event(p,'siren-seeker-hit',point,{target:victim.id});}}
   else if(data.kind==='damnation'){this.area(p,point,B.damnation.blast*boostOf(p).reach,B.damnation.damage,'damnation',0,true);for(const q of [...s.props.values()])if(q.material!=='foundation'&&len(sub(q.body.translation(),point))<B.damnation.blast*boostOf(p).reach&&s.lineOfSight(point,q.body.translation(),id,q.id))s.damageProp(q,B.damnation.damage*p.stats.demolition,mul(dir,150),p.id,point);this.event(p,'siren-explode',point,{radius:B.damnation.blast*boostOf(p).reach});}
   else {if(victim)this.hit(p,victim,shot.damage,data.kind==='drop'?'rain':'hellfire');if(prop&&prop.material!=='foundation')s.damageProp(prop,shot.damage*p.stats.demolition,mul(dir,80),p.id,point);if(data.kind==='hellfire')this.patch(p,point);else this.event(p,'siren-drop',point);}
   s.projectiles.delete(shot.id);break;
  }
  return true;
 }
 view():SirenWorldView|undefined{if(![...this.sim.players.values()].some(p=>p.connected&&this.active(p))&&!this.patches.length&&!this.grabs.length)return;return {patches:this.patches.map(f=>({...f,pos:{...f.pos}})),eruptions:this.eruptions.map(({fired,...f})=>({...f,pos:{...f.pos}})),rains:this.rains.map(({next,...f})=>({...f,center:{...f.center}})),marks:this.marks.map(f=>({...f})),grabs:this.grabs.map(f=>({...f}))};}
}
