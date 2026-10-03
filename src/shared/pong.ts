// Rally Pong v2 (design §9.2) behind the §3.4a hooks; imports only config/arena at runtime. Still inline: the volley
// count and split suppression (simulation.ts), the minion branch (an add hit consumes the ball) and the chaos skips.
import type {Simulation,Player,Projectile,Prop} from './simulation.js';
import type {Weapon,Stats,Chip} from './content.js';
import {type Vec,mul,sub,dot,norm,add,clamp,len,v} from './config.js';
import {ARENA} from './arena.js';
export const PONG_CHIP:Chip={id:'pong',rarity:'mythic',name:'Pong Shot',description:'Every shot becomes a pong ball with no drop (machine gun: every third shot). When a ball hits a wall, prop or ceiling after flying 6 m, it rallies toward the nearest enemy you can see ahead of it, faster and harder each time (3 rallies). The floor only bounces it. Several balls can be in play.',tags:['ability'],max:1,icon:'↔',prose:true,effects:[{stat:'pong',op:'set',value:1}],stats:[{text:'No projectile drop',tone:'good'},{text:'+3 aimed rallies per ball',tone:'good'},{text:'Each rally: +12% speed, up to +8 damage',tone:'good'},{text:'No splits, homing or returning flight',tone:'bad'}]};
/** Ball speed clamp; rallies (+`bounces`, up to maxRallies); a redirect needs `arm` m of travel and an enemy within `range`
 *  m and `cone`° of the mirror direction, led by `lead`; each bounce ×rallySpeed (≤ speedCap × start); each redirect +bonus×base
 *  (≤ bonusMax); weapons faster than `ballEvery` s fire a ball every ⌈ballEvery/interval⌉ shots; ballCap live balls per owner. */
export const PONG={minSpeed:30,maxSpeed:80,rallies:3,maxRallies:6,range:30,arm:6,cone:75,lead:.6,rallySpeed:1.12,speedCap:1.6,bonus:.15,bonusMax:8,ballEvery:.24,ballCap:24,life:3.5,travel:200,contactCooldown:.18};
/** Start speed, base damage, weapon blast radius and the last bounce point. */
type Ball=Projectile&{rally?:{speed:number;base:number;blast:number;mark:Vec}};
const fired=new WeakMap<Player,number>(),every=(w:Weapon)=>Math.max(1,Math.ceil(PONG.ballEvery/w.interval)),speedOf=(w:Weapon,s:Stats)=>clamp(w.speed*s.projectileSpeed,PONG.minSpeed,PONG.maxSpeed);
export function pongShot(_sim:Simulation,owner:Player,w:Weapon):boolean{if(!owner.stats.pong||w.devOnly||w.class==='melee')return false;const n=fired.get(owner)||0;fired.set(owner,n+1);return n%every(w)===0;}
/** Before the volley and the projectile cap check: the oldest live ball pops at the cap. */
export function pongBeforeSpawn(sim:Simulation,owner:Player):void{const mine=[...sim.projectiles.values()].filter(q=>q.pong&&q.originOwner===owner.id);if(mine.length>=PONG.ballCap)pop(sim,mine[0],mine[0].pos);}
/** vel is set from fixedSpeed afterwards. */
export function pongInit(_sim:Simulation,owner:Player,w:Weapon,shot:Projectile):void{const speed=speedOf(w,owner.stats);Object.assign(shot,{fixedSpeed:speed,life:PONG.life,distanceLeft:PONG.travel,gravity:0,homing:0,pierces:0,returning:false,zigzag:false,bounces:Math.min(PONG.maxRallies,PONG.rallies+owner.stats.bounces)});(shot as Ball).rally={speed,base:shot.damage,blast:w.blast||0,mark:{...shot.pos}};}
/** The machine gun aims for its ordinary shots (2 of 3); every other weapon for the ball. */
export function pongBotBallistics(w:Weapon,stats:Stats):{speed:number;gravity:number}{return every(w)>1?{speed:w.speed*stats.projectileSpeed,gravity:stats.boomerang?0:w.gravity}:{speed:speedOf(w,stats),gravity:0};}
/** true = removed: the ball dies with its owner; pulls never change its speed. */
export function pongStep(sim:Simulation,shot:Projectile,_dt:number):boolean{if((sim.players.get(shot.originOwner||shot.owner)?.hp||0)<=0){sim.projectiles.delete(shot.id);return true;}shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);return false;}
export function pongCanContact(shot:Projectile,id:string,time:number):boolean{return time-(shot.contacts?.[id]??-10)>PONG.contactCooldown;}
/** `ctx`: ceiling contact centre and the pre-contact velocity. Bounds below .2 m count as floor. */
export function pongSurface(sim:Simulation,shot:Projectile,point:Vec,_normal:Vec,where:'ceiling'|'bounds',ctx?:{center:Vec;velocity:Vec}):'handled'|'default'{
 if(where==='ceiling'){if(!ctx)return 'default';sim.emit('ceiling-hit',point,{source:shot.owner,spec:shot.spec,radius:shot.radius,color:0x75ddff,message:'BOUNCE'});const vel=ctx.velocity;vel.y=-Math.abs(vel.y);bounce(sim,shot,point,vel,false);return 'handled';}
 const vel={...shot.vel};let out=false,floor=false;for(const [axis,low,high]of [['x',-ARENA.width/2,ARENA.width/2],['z',-ARENA.depth/2,ARENA.depth/2],['y',.2,24]] as const){const p=shot.pos[axis];if(p<low||p>high){out=true;floor||=axis==='y'&&p<low;shot.pos[axis]=clamp(p,low,high);vel[axis]=p<low?Math.abs(vel[axis]):-Math.abs(vel[axis]);}}
 if(out)bounce(sim,shot,{...shot.pos},vel,floor);return 'handled';
}
/** After the Block check. A player hit takes the default path and consumes the ball; a surface with a rally left is a
 *  rally bounce (no prop damage, impact event or weapon blast). Upward-facing surfaces hit from above are floor. */
export function pongBeforeImpact(sim:Simulation,shot:Projectile,target:Player|Prop,id:string,point:Vec,normal:Vec):'handled'|'default'{if('stats'in target||shot.bounces<=0)return 'default';(shot.contacts??={})[id]=sim.time;bounce(sim,shot,point,sub(shot.vel,mul(normal,2*dot(shot.vel,normal))),Math.abs(normal.y)>.6&&shot.vel.y<0);return 'handled';}
/** Default path only (player hit, or the pop on a surface after the last rally): the ball is removed. */
export function pongAfterImpact(sim:Simulation,shot:Projectile,target:Player|Prop,_id:string,point:Vec,_normal:Vec):boolean{if(!('stats'in target))sim.emit('ricochet',point,{source:shot.owner,mods:shot.mods,color:0xff6475});return false;}
/** A Block return keeps the speed and the rallies, and restarts the 6 m arming. */
export function pongReflect(shot:Projectile,_blocker:Player):number{const r=(shot as Ball).rally;if(r)r.mark={...shot.pos};return shot.fixedSpeed!;}
/** Removes a ball; `blast` = the surface pop, where a blast weapon (fire) explodes. */
function pop(sim:Simulation,shot:Ball,point:Vec,blast=false){const o=sim.players.get(shot.owner),r=shot.rally;if(blast&&o&&r?.blast&&(shot.direct||shot.reflections)&&!shot.effectsUsed){shot.effectsUsed=true;sim.explode(o,point,shot.radius>.2?3:r.blast,shot.damage*.38,shot.impulse,'blast',true);}sim.emit('ricochet',point,{source:shot.owner,mods:shot.mods,color:0xff6475});sim.projectiles.delete(shot.id);}
/** One surface contact: pops without a rally left; else spends one, speeds up and mirror-bounces, or redirects at the
 *  nearest visible enemy in the cone (best-first, ≤ 4 LOS rays) and emits the `rally` ricochet for the VFX. */
function bounce(sim:Simulation,shot:Ball,point:Vec,vel:Vec,floor:boolean){
 const r=shot.rally;if(!r||shot.bounces<=0)return pop(sim,shot,point,true);
 shot.bounces--;const speed=shot.fixedSpeed=Math.min(r.speed*PONG.speedCap,shot.fixedSpeed!*PONG.rallySpeed),armed=len(sub(point,r.mark))>=PONG.arm,owner=sim.players.get(shot.owner);
 let dir=norm(vel),target:string|undefined;const from=add(point,mul(dir,shot.radius+.1));r.mark=point;
 if(!floor&&armed&&owner){let rays=0;for(const n of sim.abilities.enemiesNear(owner,from,PONG.range,{los:false})){const chest=add(n.pos,v(0,.15,0)),to=sub(chest,from);if(dot(norm(to),dir)<Math.cos(PONG.cone*Math.PI/180))continue;if(rays++>=4)break;
  if(sim.lineOfSight(from,chest,shot.owner,n.target.id)){dir=norm(add(to,mul(n.target.body.linvel(),len(to)/speed*PONG.lead)));target=n.target.id;shot.damage+=Math.min(PONG.bonus*r.base,PONG.bonusMax);break;}}}
 shot.vel=mul(dir,speed);shot.pos=from;sim.emit('ricochet',from,{source:shot.owner,mods:shot.mods,color:0xff6475,...(target?{message:'rally',from:point,target}:{})});
}
