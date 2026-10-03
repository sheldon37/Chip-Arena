// Rally Pong v2 (design §9.2) behind the §3.4a hooks; imports only config/arena at runtime.
import type {Simulation,Player,Projectile,Prop} from './simulation.js';
import type {Weapon,Stats,Chip} from './content.js';
import {type Vec,mul,sub,dot,norm,add,clamp,len,v} from './config.js';
import {ARENA} from './arena.js';
export const PONG_CHIP:Chip={id:'pong',rarity:'mythic',name:'Pong Shot',description:'Shots become pong balls with no drop (machine gun: every third shot). A ball that hits a wall, prop or ceiling after flying 6 m rallies toward a nearby enemy you can see ahead of it, faster and harder each time (3 rallies, machine gun 1). The floor only bounces it.',tags:['ability'],max:1,icon:'↔',prose:true,effects:[{stat:'pong',op:'set',value:1}],stats:[{text:'No projectile drop',tone:'good'},{text:'+3 aimed rallies (machine gun 1)',tone:'good'},{text:'Each rally: +12% speed, up to +8 damage',tone:'good'},{text:'No splits, homing or returning flight',tone:'bad'}]};
/** §9.2 knobs; range, cone and lead tuned with seat-swapped bot duels. A weapon faster than `ballEvery` s fires a ball
 *  every N=⌈ballEvery/interval⌉ shots, with ⌈rallies/N⌉ rallies (+`bounces`, ≤ 6). */
export const PONG={minSpeed:30,maxSpeed:80,rallies:3,range:12,arm:6,cone:30,lead:.4,rallySpeed:1.12,speedCap:1.6,bonus:.15,bonusMax:8,ballEvery:.24,ballCap:24,life:3.5,travel:200,contactCooldown:.18};
type Ball=Projectile&{rally?:{cap:number;bonus:number;blast?:number;mark:Vec}};
const fired=new WeakMap<Player,number>(),every=(w:Weapon)=>Math.ceil(PONG.ballEvery/w.interval),speedOf=(w:Weapon,s:Stats)=>clamp(w.speed*s.projectileSpeed,PONG.minSpeed,PONG.maxSpeed);
const ricochet=(sim:Simulation,shot:Projectile,pos:Vec,extra={})=>sim.emit('ricochet',pos,{source:shot.owner,mods:shot.mods,color:0xff6475,...extra});
const pop=(sim:Simulation,shot:Projectile,pos:Vec)=>{ricochet(sim,shot,pos);sim.projectiles.delete(shot.id);};
export function pongShot(_sim:Simulation,owner:Player,w:Weapon):boolean{if(!owner.stats.pong||w.devOnly||w.class==='melee')return false;const n=fired.get(owner)||0;fired.set(owner,n+1);return n%every(w)<1;}
/** The oldest live ball pops at the cap. */
export function pongBeforeSpawn(sim:Simulation,owner:Player):void{const mine=[...sim.projectiles.values()].filter(q=>q.pong&&q.originOwner===owner.id);if(mine.length>=PONG.ballCap)pop(sim,mine[0],mine[0].pos);}
export function pongInit(_sim:Simulation,owner:Player,w:Weapon,shot:Projectile):void{const speed=speedOf(w,owner.stats);Object.assign(shot,{fixedSpeed:speed,life:PONG.life,distanceLeft:PONG.travel,gravity:0,homing:0,pierces:0,returning:false,zigzag:false,bounces:Math.min(6,Math.ceil(PONG.rallies/every(w))+owner.stats.bounces),rally:{cap:speed*PONG.speedCap,bonus:Math.min(PONG.bonus*shot.damage,PONG.bonusMax),blast:w.blast,mark:{...shot.pos}}});}
export function pongBotBallistics(w:Weapon,stats:Stats):{speed:number;gravity:number}{return {speed:speedOf(w,stats),gravity:0};}
/** true = removed (the ball dies with its owner). */
export function pongStep(sim:Simulation,shot:Projectile,_dt:number):boolean{if((sim.players.get(shot.originOwner||shot.owner)?.hp||0)<=0)return sim.projectiles.delete(shot.id);shot.vel=mul(norm(shot.vel),shot.fixedSpeed!);return false;}
export function pongCanContact(shot:Projectile,id:string,time:number):boolean{return time-(shot.contacts?.[id]??-10)>PONG.contactCooldown;}
/** The ceiling bounces from the contact centre, below the contact band. */
export function pongSurface(sim:Simulation,shot:Projectile,point:Vec,_normal:Vec,where:'ceiling'|'bounds',ctx?:{center:Vec;velocity:Vec}):'handled'|'default'{
 if(where==='ceiling'){const vel=ctx!.velocity;vel.y=-Math.abs(vel.y);sim.emit('ceiling-hit',point,{source:shot.owner,spec:shot.spec,radius:shot.radius,color:0x75ddff,message:'BOUNCE'});bounce(sim,shot,ctx!.center,vel);return 'handled';}
 const vel={...shot.vel};let out=false;for(const [axis,low,high]of [['x',-ARENA.width/2,ARENA.width/2],['z',-ARENA.depth/2,ARENA.depth/2],['y',.2,24]] as const){const p=shot.pos[axis];if(p<low||p>high){out=true;shot.pos[axis]=clamp(p,low,high);vel[axis]=Math.abs(vel[axis])*(p<low?1:-1);}}
 if(out)bounce(sim,shot,{...shot.pos},vel);return 'handled';
}
/** After the Block check: a surface hit with a rally left is a rally bounce (no prop damage, impact or blast). */
export function pongBeforeImpact(sim:Simulation,shot:Projectile,target:Player|Prop,id:string,point:Vec,normal:Vec):'handled'|'default'{if('stats'in target||shot.bounces<=0)return 'default';(shot.contacts??={})[id]=sim.time;bounce(sim,shot,point,sub(shot.vel,mul(normal,2*dot(shot.vel,normal))));return 'handled';}
/** Default path (a player hit or the pop): the ball is removed. */
export function pongAfterImpact(sim:Simulation,shot:Projectile,target:Player|Prop,_id:string,point:Vec,_normal:Vec):boolean{if(!('stats'in target))ricochet(sim,shot,point);return false;}
/** A Block return keeps the speed and rallies and restarts the arming. */
export function pongReflect(shot:Projectile,_blocker:Player):number{const r=(shot as Ball).rally;if(r)r.mark={...shot.pos};return shot.fixedSpeed!;}
/** No rally left: pop (a blast weapon explodes). Otherwise spend one, speed up, and mirror-bounce or redirect at the
 *  nearest in-cone enemy of the 4 nearest visible ones, with ricochet 'rally' (from = bounce point, pos, target). A
 *  contact that turns a falling ball upward is floor and never redirects. */
function bounce(sim:Simulation,shot:Ball,point:Vec,vel:Vec){
 const r=shot.rally!,o=sim.players.get(shot.owner);
 if(shot.bounces--<=0){if(o&&r.blast&&(shot.direct||shot.reflections))sim.explode(o,point,shot.radius>.2?3:r.blast,shot.damage*.38,shot.impulse,'blast',true);return pop(sim,shot,point);}
 const speed=shot.fixedSpeed=Math.min(r.cap,shot.fixedSpeed!*PONG.rallySpeed),from=add(point,mul(norm(vel),shot.radius+.1)),aim=(n:{pos:Vec})=>sub(add(n.pos,v(0,.15,0)),from);
 const n=!(shot.vel.y<0&&vel.y>0)&&o&&len(sub(point,r.mark))>=PONG.arm&&sim.abilities.enemiesNear(o,from,PONG.range).find(e=>dot(norm(aim(e)),norm(vel))>=Math.cos(PONG.cone*Math.PI/180));
 if(n){vel=add(aim(n),mul(n.target.body.linvel(),len(aim(n))/speed*PONG.lead));shot.damage+=r.bonus;}
 r.mark=point;shot.vel=mul(norm(vel),speed);shot.pos=from;ricochet(sim,shot,from,n?{message:'rally',from:point,target:n.target.id}:{});
}
