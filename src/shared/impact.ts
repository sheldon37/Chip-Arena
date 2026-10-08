import {CONFIG, Vec, sub, add, cross, dot, clamp} from './config.js';
export function pointVelocity(linear:Vec,angular:Vec,point:Vec,center:Vec):Vec{return add(linear,cross(angular,sub(point,center)));}
export function impactDamage(mass:number,closingSpeed:number){if(!Number.isFinite(mass)||mass<=0||!Number.isFinite(closingSpeed))return {energy:0,damage:0};const speed=Math.max(0,closingSpeed),energy=.5*mass*speed*speed;return {energy,damage:speed<CONFIG.minimumImpactSpeed?0:clamp(CONFIG.damageScale*Math.max(0,energy-CONFIG.safeEnergy),0,CONFIG.maximumDamagePerImpact)};}
export function closingSpeed(a:Vec,b:Vec,normalFromAToB:Vec){return Math.max(0,dot(sub(a,b),normalFromAToB));}
export class ImpactLedger {
 private pairs=new Map<string,{last:number;touching:boolean}>();private groups=new Map<string,{at:number;damage:number}>();
 beginTick(){for(const p of this.pairs.values())(p as any).seen=false;}
 claim(pair:string,group:string,time:number,damage:number){let p=this.pairs.get(pair);if(!p){p={last:-Infinity,touching:false};this.pairs.set(pair,p);} (p as any).seen=true;
 const allowed=!p.touching&&time-p.last>=CONFIG.impactPairCooldown;p.touching=true;if(!allowed||damage<=0)return 0;
 p.last=time;let g=this.groups.get(group);if(!g||time-g.at>CONFIG.fragmentWindow){g={at:time,damage:0};this.groups.set(group,g);}const amount=Math.min(damage,Math.max(0,CONFIG.fragmentDamageCap-g.damage));g.damage+=amount;return amount;}
 endTick(time:number){for(const [k,p]of this.pairs){if(!(p as any).seen)p.touching=false;if(time-p.last>10&&!p.touching)this.pairs.delete(k);}for(const[k,g]of this.groups)if(time-g.at>2)this.groups.delete(k);}
 clear(){this.pairs.clear();this.groups.clear();}
}
