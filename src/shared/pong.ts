// Pong mythic chip hooks (design §3.4a, §9); imports only config/arena at runtime. Still inline: the volley count and split
// suppression (simulation.ts) and the chaos skips (chaos.ts).
import type {Simulation,Player,Projectile,Prop} from './simulation.js';
import type {Weapon,Stats,Chip} from './content.js';
import {type Vec,mul,sub,dot,norm,add,clamp} from './config.js';
import {ARENA} from './arena.js';
export const PONG_CHIP:Chip={id:"pong",rarity:"mythic",name:"Pong Shot",description:"One persistent shot at fixed 40% base speed, no gravity, unlimited full-damage bounces including arena edges. Next shot replaces it, even if reflected. Overrides volleys, splits, homing and returning flight; Block can still absorb it after 3 returns.",tags:["ranged"],max:1,icon:"\u2194",effects:[{stat:"pong",op:"set",value:1}]};
export const PONG={speedScale:.4,contactCooldown:.18} as const;
export function pongShot(_sim:Simulation,owner:Player,w:Weapon):boolean{return !!owner.stats.pong&&w.class==='ranged';}
/** Before the volley and the projectile cap check. */
export function pongBeforeSpawn(sim:Simulation,owner:Player):void{for(const [id,shot]of sim.projectiles)if(shot.originOwner===owner.id)sim.projectiles.delete(id);}
/** vel is set from fixedSpeed afterwards. */
export function pongInit(_sim:Simulation,_owner:Player,w:Weapon,shot:Projectile):void{shot.fixedSpeed=w.speed*PONG.speedScale;shot.life=1e9;shot.distanceLeft=1e9;shot.gravity=0;shot.homing=0;shot.returning=false;shot.zigzag=false;}
export function pongBotBallistics(w:Weapon,stats:Stats):{speed:number;gravity:number}{return {speed:w.speed*stats.projectileSpeed,gravity:0};}
/** true = the shot was removed. */
export function pongStep(sim:Simulation,shot:Projectile,_dt:number):boolean{if((sim.players.get(shot.originOwner||shot.owner)?.hp||0)<=0){sim.projectiles.delete(shot.id);return true;}shot.life=1e9;shot.distanceLeft=1e9;shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);return false;}
export function pongCanContact(shot:Projectile,id:string,time:number):boolean{return time-(shot.contacts?.[id]??-10)>PONG.contactCooldown;}
/** `ctx`: ceiling contact centre and the pre-contact velocity. */
export function pongSurface(sim:Simulation,shot:Projectile,point:Vec,_normal:Vec,where:'ceiling'|'bounds',ctx?:{center:Vec;velocity:Vec}):'handled'|'default'{
 if(where==='ceiling'){if(!ctx)return 'default';sim.emit('ceiling-hit',point,{source:shot.owner,spec:shot.spec,radius:shot.radius,color:0x75ddff,message:'BOUNCE'});shot.vel=ctx.velocity;shot.vel.y=-Math.abs(shot.vel.y);shot.pos=ctx.center;sim.emit('ricochet',point,{source:shot.owner,mods:shot.mods,color:0x75ddff});return 'handled';}
 let bounced=false;for(const [axis,low,high]of [['x',-ARENA.width/2,ARENA.width/2],['z',-ARENA.depth/2,ARENA.depth/2],['y',.2,24]] as const){if(shot.pos[axis]<low||shot.pos[axis]>high){shot.pos[axis]=clamp(shot.pos[axis],low,high);shot.vel[axis]*=-1;bounced=true;}}if(bounced)sim.emit('ricochet',shot.pos,{source:shot.owner,mods:shot.mods,color:0xff6475});return 'handled';
}
/** After the Block check: 'handled' skips damage, the impact event and the blast. */
export function pongBeforeImpact(_sim:Simulation,_shot:Projectile,_target:Player|Prop,_id:string,_point:Vec,_normal:Vec):'handled'|'default'{return 'default';}
/** true keeps the shot. */
export function pongAfterImpact(sim:Simulation,shot:Projectile,_target:Player|Prop,id:string,point:Vec,normal:Vec):boolean{const owner=shot.owner;shot.contacts??={};shot.contacts[id]=sim.time;shot.vel=sub(shot.vel,mul(normal,2*dot(shot.vel,normal)));shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);shot.pos=add(point,mul(norm(shot.vel),shot.radius+.10));sim.emit('ricochet',point,{source:owner,mods:shot.mods,color:0xff6475});return true;}
export function pongReflect(shot:Projectile,_blocker:Player):number{return shot.fixedSpeed!;}
