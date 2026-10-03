// Gun E handlers (design §5): Frag Grenade, Underbarrel Rocket, Orbital Mark. Runtime imports: config, abilities only.
import type {AbilityHandler,AbilitySystem,CastContext,ShotHandler as Shot} from './ability-system.js';
import type {Simulation,Player,Projectile} from './simulation.js';
import type {Minion} from './chaos.js';
import {v,add,sub,mul,dot,len,norm,clamp,type Vec} from './config.js';
import {abilityRadius,ABILITY_DEFS} from './abilities.js';
const owner=(sys:AbilitySystem,shot:Projectile)=>sys.sim.players.get(shot.owner),tag=(shot:Projectile)=>shot.ability!,def=(shot:Projectile)=>ABILITY_DEFS[tag(shot).id],data=(shot:Projectile)=>tag(shot).data??={};
/** Muzzle shot toward the crosshair convergence point; `damage` carries the scaled main damage. */
function fire(sys:AbilitySystem,c:CastContext,life:number){const r=sys.aimRay(c.p,c.aim,c.def.range);sys.spawnShot(c.p,c.def,{cast:c.cast,pos:r.start,dir:sub(r.target,r.start),damage:sys.D(c.def,c),life});}
/** Removes the shot; the blast belongs to its current owner (a Block reflect hands it over). */
function boom(sys:AbilitySystem,shot:Projectile,at:Vec,propScale=1){const p=owner(sys,shot),d=def(shot);sys.sim.projectiles.delete(shot.id);if(p)sys.blast(p,at,abilityRadius(d,p.stats),shot.damage,d.impulse!,d.fx,{propScale});}
/** Direct impact worth `part` of the main damage. */
function strike(sys:AbilitySystem,shot:Projectile,target:Player|Minion,point:Vec,part:number){const p=owner(sys,shot);if(p)sys.hit(p,target,shot.damage*part,norm(shot.vel),150,point,{direct:true});return p;}
/** The fuse never ends sooner than .45 s after the first contact. */
function touch(sys:AbilitySystem,shot:Projectile){const t=tag(shot);if(!data(shot).c){data(shot).c=1;t.fuse=Math.max(t.fuse!,sys.sim.time+.45);}}
const FRAG:Shot={restitution:1,
 tick(sys,shot){const t=tag(shot),rest=data(shot).r as Vec|undefined;if(sys.sim.time>=t.fuse!)boom(sys,shot,shot.pos);else if(rest)shot.pos={...rest};},
 onEnemy(sys,shot,target,point){if(!('stats'in target)){boom(sys,shot,point);return 'stop';}touch(sys,shot);if(!data(shot).i){data(shot).i=1;strike(sys,shot,target,point,8/60);}shot.vel=mul(shot.vel,.3);return 'bounce';},
 onSurface(sys,shot,_point,normal){touch(sys,shot);const n=norm(normal),vn=mul(n,dot(shot.vel,n));shot.vel=add(mul(sub(shot.vel,vn),.25),mul(vn,.4));
  if(n.y>.6&&len(shot.vel)<1.5){data(shot).r={...shot.pos};shot.gravity=0;shot.vel=v(1e-4,0,0);return 'glide';}return 'bounce';},
 onExpire(sys,shot){boom(sys,shot,shot.pos);}};
/** Contact before `arm` metres: impact damage and a puff, no blast. */
function dud(sys:AbilitySystem,shot:Projectile,point:Vec,target?:Player|Minion){sys.sim.projectiles.delete(shot.id);const p=target?strike(sys,shot,target,point,15/55):owner(sys,shot);sys.sim.emit('impact',point,{source:p?.id??null,spec:shot.spec,color:0x9a9a9a,message:'rocket-dud'});}
const ROCKET:Shot={
 onEnemy(sys,shot,target,point,_n,armed){if(armed)boom(sys,shot,add(point,mul(norm(shot.vel),.3)),2);else dud(sys,shot,point,target);return 'stop';},
 onSurface(sys,shot,point,_n,_prop,armed){if(armed)boom(sys,shot,point,2);else dud(sys,shot,point);return 'stop';},
 onExpire(sys,shot){boom(sys,shot,shot.pos,2);}};
/** Bomb field on the tag: follows it until `lockAt`, strikes where it locked. */
function mark(sys:AbilitySystem,shot:Projectile,at:Vec,attach?:string){const p=owner(sys,shot),d=def(shot);sys.sim.projectiles.delete(shot.id);
 if(p)sys.zone(p,'bomb',at,abilityRadius(d,p.stats),d.fuse!,shot.damage,d.fx,{impulse:d.impulse,blastKind:'ability',lockAt:sys.sim.time+d.duration!,cast:tag(shot).cast,...(attach?{attach}:{})});}
const MARK:Shot={
 onEnemy(sys,shot,target,point){strike(sys,shot,target,point,10/85);mark(sys,shot,{...target.body.translation()},target.id);return 'stop';},
 onSurface(sys,shot,point){mark(sys,shot,point);return 'stop';}};
export const GUN_HANDLERS:Record<string,AbilityHandler>={
 'frag-grenade':{point:(sys,c)=>sys.aimPoint(c.p,c.aim,c.def.range).point,shot:FRAG,
  release(sys,c){const start=sys.aimRay(c.p,c.aim,0).start,d=sub(c.point!,start),t=clamp(len(d)/16,.45,1.1);
   sys.spawnShot(c.p,c.def,{cast:c.cast,pos:start,vel:add(mul(d,1/t),v(0,c.def.gravity!*t/2,0)),damage:sys.D(c.def,c),fuse:sys.sim.time+t+c.def.fuse!,life:4,travel:200});}},
 rocket:{shot:ROCKET,release:(sys,c)=>fire(sys,c,2.5)},
 'orbital-mark':{shot:MARK,release:(sys,c)=>fire(sys,c,1)},
};
export function gunPrimaryHit(_sim:Simulation,_p:Player,_target:Player|Minion,_point:Vec,_actual:number):void{}
