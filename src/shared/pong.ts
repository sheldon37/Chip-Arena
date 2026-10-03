// Pong mythic chip (design §3.4a). FOUNDATION extracted today's behaviour verbatim behind these hooks; CHIPS + PONG owns the
// rework (§9). The hooks take the simulation, weapon and stats as parameters, so this module imports only config/arena at
// runtime. The inline sites that stay in simulation.ts: the volley count (`pong||secondary?1:…`), split suppression
// (`!shot.pong`) and the chaos skips (chaos.ts spawned/shotStep).
import type {Simulation,Player,Projectile,Prop} from './simulation.js';
import type {Weapon,Stats,Chip} from './content.js';
import {type Vec,mul,sub,dot,norm,add,clamp} from './config.js';
import {ARENA} from './arena.js';
/** The catalogue row (same CHIPS index as before the extraction). */
export const PONG_CHIP:Chip={id:"pong",rarity:"mythic",name:"Pong Shot",description:"One persistent shot at fixed 40% base speed, no gravity, unlimited full-damage bounces including arena edges. Next shot replaces it, even if reflected. Overrides volleys, splits, homing and returning flight; Block can still absorb it after 3 returns.",tags:["ranged"],max:1,icon:"\u2194",effects:[{stat:"pong",op:"set",value:1}]};
/** Pong knobs. */
export const PONG={speedScale:.4,contactCooldown:.18} as const;
/** spawnProjectile per-shot gate: does this owner fire pong balls with this weapon? */
export function pongShot(_sim:Simulation,owner:Player,w:Weapon):boolean{return !!owner.stats.pong&&w.class==='ranged';}
/** Before spawning a pong volley (and before the projectile cap check): today every shot the owner launched is replaced. */
export function pongBeforeSpawn(sim:Simulation,owner:Player):void{for(const [id,shot]of sim.projectiles)if(shot.originOwner===owner.id)sim.projectiles.delete(id);}
/** Overrides on a freshly built shot (vel is set from fixedSpeed afterwards): fixed speed, endless life, no drop/homing/zigzag/return. */
export function pongInit(_sim:Simulation,_owner:Player,w:Weapon,shot:Projectile):void{shot.fixedSpeed=w.speed*PONG.speedScale;shot.life=1e9;shot.distanceLeft=1e9;shot.gravity=0;shot.homing=0;shot.returning=false;shot.zigzag=false;}
/** Bot ballistic aim for pong holders. */
export function pongBotBallistics(w:Weapon,stats:Stats):{speed:number;gravity:number}{return {speed:w.speed*stats.projectileSpeed,gravity:0};}
/** Per-tick step before flight; true = the shot was removed. */
export function pongStep(sim:Simulation,shot:Projectile,_dt:number):boolean{if((sim.players.get(shot.originOwner||shot.owner)?.hp||0)<=0){sim.projectiles.delete(shot.id);return true;}shot.life=1e9;shot.distanceLeft=1e9;shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);return false;}
/** castShape filter: a ball may touch the same collider again only after the contact cooldown. */
export function pongCanContact(shot:Projectile,id:string,time:number):boolean{return time-(shot.contacts?.[id]??-10)>PONG.contactCooldown;}
/** Ceiling field or arena bounds. 'handled' = the hook applied the bounce. `ctx` carries the ceiling contact centre and the
 *  pre-contact velocity integrated to the contact. */
export function pongSurface(sim:Simulation,shot:Projectile,point:Vec,_normal:Vec,where:'ceiling'|'bounds',ctx?:{center:Vec;velocity:Vec}):'handled'|'default'{
 if(where==='ceiling'){if(!ctx)return 'default';sim.emit('ceiling-hit',point,{source:shot.owner,spec:shot.spec,radius:shot.radius,color:0x75ddff,message:'BOUNCE'});shot.vel=ctx.velocity;shot.vel.y=-Math.abs(shot.vel.y);shot.pos=ctx.center;sim.emit('ricochet',point,{source:shot.owner,mods:shot.mods,color:0x75ddff});return 'handled';}
 let bounced=false;for(const [axis,low,high]of [['x',-ARENA.width/2,ARENA.width/2],['z',-ARENA.depth/2,ARENA.depth/2],['y',.2,24]] as const){if(shot.pos[axis]<low||shot.pos[axis]>high){shot.pos[axis]=clamp(shot.pos[axis],low,high);shot.vel[axis]*=-1;bounced=true;}}if(bounced)sim.emit('ricochet',shot.pos,{source:shot.owner,mods:shot.mods,color:0xff6475});return 'handled';
}
/** NEW site right after the Block check, before leet/impact damage: 'handled' skips damage, the impact event and the blast. */
export function pongBeforeImpact(_sim:Simulation,_shot:Projectile,_target:Player|Prop,_id:string,_point:Vec,_normal:Vec):'handled'|'default'{return 'default';}
/** After the impact; true keeps the shot (today: mirror off the contact normal at fixed speed). */
export function pongAfterImpact(sim:Simulation,shot:Projectile,_target:Player|Prop,id:string,point:Vec,normal:Vec):boolean{const owner=shot.owner;shot.contacts??={};shot.contacts[id]=sim.time;shot.vel=sub(shot.vel,mul(normal,2*dot(shot.vel,normal)));shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);shot.pos=add(point,mul(norm(shot.vel),shot.radius+.10));sim.emit('ricochet',point,{source:owner,mods:shot.mods,color:0xff6475});return true;}
/** Reflected speed when a blocker returns the ball. */
export function pongReflect(shot:Projectile,_blocker:Player):number{return shot.fixedSpeed!;}
