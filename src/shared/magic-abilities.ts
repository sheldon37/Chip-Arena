// Magic Q/E handlers, primary hit effects (Soak, lightning chain) and school-chip effects (design §6, §8).
import type {AbilityHandler,AbilitySystem as Y,CastContext as Ctx,ShotOptions} from './ability-system.js';
import type {Simulation,Player,Projectile as Q} from './simulation.js';
import type {Minion} from './chaos.js';
import {abilityRadius,type AbilityDef} from './abilities.js';
import {WEAPONS} from './content.js';
import {refreshDot} from './elements.js';
import {projectileHeadshot,HEADSHOT_MULTIPLIER} from './headshots.js';
import {projectileBodyImpact as impact} from './body-damage.js';
import {MAGIC_KITS as K} from './magic-kit.js';
import {type Vec,v,add,sub,mul,len,norm,dot,clamp,forward} from './config.js';
const soaked=new WeakMap<Player,number>(),LIT=WEAPONS.lightning.color;
const own=(y:Y,q:Q)=>y.sim.players.get(q.owner),lift=(a:Vec)=>add(a,v(0,.15,0)),pos=(p:Player)=>p.body.translation();
const slow=(y:Y,t:Player,d:AbilityDef,p:Player)=>y.slow(t,d.speedFactor!,d.slowFor!,d.fx,p.id);
/** Visual-only impact (§13.1). */
const fx=(y:Y,p:Player,at:Vec,radius:number,message:string)=>y.sim.emit('explosion',at,{radius,source:p.id,spec:p.spec,color:WEAPONS[p.spec].color,message});
/** Muzzle → crosshair. */
const aimed=(y:Y,c:Ctx,o:Partial<ShotOptions>={})=>{const r=y.aimRay(c.p,c.aim,c.def.range);y.spawnShot(c.p,c.def,{cast:c.cast,pos:r.start,dir:sub(r.target,r.start),damage:y.D(c.def,c),...o});};
/** Ground-targeted E: fixed at cast start; +closeFuse within 4 m of the caster. */
const ground=(y:Y,c:Ctx)=>y.aimPoint(c.p,c.aim,c.def.range,{ground:true}).point,fuse=(c:Ctx,i=0)=>c.def.fuse!+.35*i+(len(sub(c.point!,pos(c.p)))<4?c.def.closeFuse!:0);
const bomb=(y:Y,c:Ctx,at:Vec,until:number)=>y.zone(c.p,'bomb',at,y.R(c.def,c),until,y.D(c.def,c),c.def.fx,{cast:c.cast,impulse:c.def.impulse});
/** Necronomicon replaces magic E: its bots wait for its cooldown instead of pressing E every tick. */
const E={point:ground,can:(_:Y,c:Ctx)=>!c.p.chips.necronomicon||c.p.secondaryCd<=0};
const burst=(y:Y,q:Q,at:Vec)=>{const p=own(y,q),d=K.lightning.e;if(p)y.blast(p,at,abilityRadius(d,p.stats),q.damage,400,d.fx);};

/**
 * - Tidal Wave: knockback along the wave (Undertow: toward the caster) and a slow; floors glide, walls stop, loose props fly.
 * - Blink Strike: path hits within 1.4 m plus an arrival discharge; Storm Recall refunds 2 s per enemy struck (max 4). Its
 *   bot escapes an enemy within 4 m or sidesteps an incoming projectile.
 * - Meteor Strike: impact, then burning ground (Cinder Field ×1.5 radius and duration).
 * - Geyser Wall: three geysers 3 m apart across the aim, erupting left to right, snapped onto props below.
 * - Ball Lightning: the shot's `fuse` is the next zap time (PvE holds shift it).
 * - Black Hole: Abyssal Core ×1.25 field and collapse radius, ×1.2 collapse damage.
 * - Field ticks use DoT kinds (burn, poison), so they never cancel a roll.
 */
export const MAGIC_HANDLERS:Record<string,AbilityHandler>={
 'flame-lance':{release:(y,c)=>aimed(y,c,{elements:['fire']}),shot:{
  onEnemy(y,q,t,at){const p=own(y,q),d=K.fire.q;if(p){const head='stats'in t&&projectileHeadshot({...t,pos:t.body.translation(),yaw:t.input.yaw},q.pos,q.vel,q.radius);
   if(y.hit(p,t,q.damage*(head?HEADSHOT_MULTIPLIER:1),norm(q.vel),q.impulse,at,{direct:true,headshot:head,bodyImpact:impact(q),elements:['fire']})>0&&'stats'in t)refreshDot(t.burns,p.id,d.burn!*q.damage/d.damage!,y.sim.time,'fire');fx(y,p,at,.8,d.fx);}
   return q.pierces-->0?'pass':'stop';},
  onSurface(y,q,at){const p=own(y,q);if(p)fx(y,p,at,.8,K.fire.q.fx);return 'stop';}}},
 'tidal-wave':{release(y,c){const f=forward(c.aim.yaw);aimed(y,c,{pos:add(pos(c.p),v(f.x*.6,.25,f.z*.6)),dir:f,life:c.def.duration});},shot:{reflect:false,
  onEnemy(y,q,t,at){const p=own(y,q),d=K.water.q;if(p){const k=add(mul(norm(q.vel),p.chips.undertow?-700:d.impulse!),v(0,200,0));
   if(y.hit(p,t,q.damage,norm(k),len(k),at)>0&&'stats'in t)slow(y,t,d,p);fx(y,p,at,1,d.fx);}return 'pass';},
  onSurface(y,q,at,n,prop){if(n.y>.6)return 'glide';if(prop?.dynamic)y.sim.launchProp(prop,mul(norm(q.vel),600),q.owner,at);return 'stop';}}},
 'blink-strike':{
  point:(_,c)=>{const i=c.p.input;return add(pos(c.p),mul(Math.hypot(i.x,i.z)>.1?norm(v(i.x,0,i.z)):forward(c.aim.yaw),c.def.range));},
  release(y,c){const p=c.p,s=y.sim,a={...pos(p)},D=y.D(c.def,c),hit=new Set<string>();y.blink(p,sub(c.point!,a));
   const b={...pos(p)},e=s.events.at(-1),seg=sub(b,a),L=len(seg);if(e?.type==='teleport')e.message=c.def.fx;
   for(const n of y.enemiesNear(p,mul(add(a,b),.5),L/2+1.4,{los:false})){const at=add(a,mul(seg,clamp(dot(sub(n.pos,a),seg)/(L*L||1),0,1)));
    if(len(sub(n.pos,at))<=1.4&&s.lineOfSight(at,n.pos,p.id,n.target.id)&&y.hit(p,n.target,D,sub(n.pos,at),0,n.pos)>0){hit.add(n.target.id);s.emit('arc',n.pos,{from:at,source:p.id,color:LIT});}}
   const r=y.blast(p,b,y.R(c.def,c),D*2/3,300,c.def.fx);for(const id of r.hit)hit.add(id);
   if(p.chips['storm-recall'])p.abilityCd=Math.max(0,p.abilityCd-Math.min(4,2*(hit.size+ +(r.minions>0))));},
  bot(y,p,i,b){const s=y.sim,o=pos(p);let d=b.target&&b.distance<4?sub(o,b.target):undefined;
   for(const q of s.projectiles.values()){const r=sub(o,q.pos),t=dot(r,q.vel)/Math.max(1,dot(q.vel,q.vel));if(!d&&q.owner!==p.id&&s.canHit(q.owner,p.id)&&t>0&&t<.3&&len(sub(r,mul(q.vel,t)))<.9)d=v(q.vel.z,0,-q.vel.x);}
   if(!d)return false;d=norm(v(d.x,0,d.z));i.x=d.x;i.z=d.z;i.ability=true;i.attack=false;return true;}},
 'shadow-shackles':{release:(y,c)=>aimed(y,c),shot:{
  onEnemy(y,q,t,at){const p=own(y,q),d=K.darkness.q;if(p){const a=y.hit(p,t,q.damage,norm(q.vel),0,at,{direct:true,bodyImpact:impact(q)});
   if(a>0){y.sim.heal(p,a*.5);if('stats'in t)slow(y,t,d,p);}fx(y,p,at,1,d.fx);}return 'stop';}}},
 'meteor-strike':{...E,release:(y,c)=>bomb(y,c,c.point!,fuse(c)),
  detonate(y,f,o){if(f.kind!=='bomb')return;const d=K.fire.e,k=o.chips['cinder-field']?1.5:1;y.blast(o,lift(f.pos),f.radius,f.damage,f.impulse!,d.fx);
   y.zone(o,'fire',f.pos,3.2*k*f.radius/d.radius!,d.duration!*k,12*f.damage/d.damage!,d.fx,{cast:f.cast,damageKind:'burn'});}},
 'geyser-wall':{...E,
  release(y,c){const s=y.sim,r=forward(c.aim.yaw-Math.PI/2);for(let i=0;i<3;i++){const g=add(c.point!,mul(r,3*i-3)),h=s.ray(add(g,v(0,2,0)),v(0,-1,0),6);
   if(h&&s.props.has(s.colliderOwners.get(h.collider.handle)||''))g.y+=2-h.timeOfImpact;bomb(y,c,g,fuse(c,i));}},
  detonate(y,f,o){const d=K.water.e;for(const id of y.blast(o,lift(f.pos),f.radius,f.damage,f.impulse!,d.fx).hit)slow(y,y.sim.players.get(id)!,d,o);}},
 'ball-lightning':{...E,point:undefined,release:(y,c)=>aimed(y,c,{life:c.def.duration,fuse:y.sim.time+c.def.tick!}),shot:{
  tick(y,q){const s=y.sim,p=own(y,q),t=q.ability!,d=K.lightning.e;if(!p||s.time<t.fuse!)return;t.fuse!+=d.tick!;
   for(const n of y.enemiesNear(p,q.pos,7,{limit:2})){y.hit(p,n.target,11*q.damage/d.damage!,sub(n.pos,q.pos),0,n.pos);s.emit('arc',n.pos,{from:{...q.pos},source:p.id,color:LIT});}},
  onEnemy:()=>'pass',onSurface(y,q,at){burst(y,q,at);return 'stop';},onExpire:(y,q)=>burst(y,q,q.pos)}},
 singularity:{...E,release:(y,c)=>y.zone(c.p,'black-hole',c.point!,y.R(c.def,c)*(c.p.chips['abyssal-core']?1.25:1),c.def.duration!,c.def.tick!*c.scale,c.def.fx,{cast:c.cast,force:c.def.force,damageKind:'poison'}),
  detonate(y,f,o){const d=K.darkness.e,at=lift(f.pos);y.blast(o,at,f.radius*4/d.radius!,f.damage/d.tick!*d.damage!*(o.chips['abyssal-core']?1.2:1),0,d.fx);y.sim.vacuum(o,at,1300);}},
};

export function magicPrimaryHit(sim:Simulation,p:Player,target:Player|Minion,point:Vec,_actual:number):void{
 const w=WEAPONS[p.spec],t=sim.time;
 // Soak: speedFactor .8 for .8 s, at most once per 1.5 s per target.
 if(p.spec==='water'&&'stats'in target&&t>=(soaked.get(target)??-9)+1.5&&sim.abilities.slow(target,.8,.8,'soak',p.id))soaked.set(target,t);
 if(!w.chain)return;
 // Chain to the nearest enemy player or add within 4 m with LOS; a prop only as a fallback.
 for(const n of sim.abilities.enemiesNear(p,point,4,{los:false,exclude:new Set([target.id]),limit:4})){if(!sim.lineOfSight(point,n.pos,target.id,n.target.id))continue;const d=norm(sub(n.pos,point));
  if('stats'in n.target)sim.damage(n.target,w.chain,p.id,'chain',d,0,n.pos);else sim.chaos.hitMinion(n.target,w.chain,p.id,0,d,{point:n.pos,kind:'energy'});sim.emit('arc',point,{from:n.pos,source:p.id,color:w.color});return;}
 for(const other of sim.props.values())if(other.material!=='foundation'&&len(sub(other.body.translation(),point))<3&&sim.lineOfSight(point,other.body.translation(),target.id,other.id)){const t=other.body.translation(),arcEnd=v(t.x,t.y,t.z);sim.damageProp(other,w.chain,v(),p.id,arcEnd);sim.emit('arc',point,{from:arcEnd,color:w.color});break;}
}
