/** Intro-only keyed curves and pure-function motion layers (pose-curves.ts stays frozen for combat).
 *
 * Every helper here is a pure function of the sampled time, so intros stay rewindable: a late join, a preview scrub
 * or a time jump gives exactly the pose an uninterrupted playback would. Nothing keeps state between calls. */
export interface IntroCurveOptions{
 /** Key indices the motion flows THROUGH: Catmull-Rom tangents instead of a dead stop at a reversal. */
 pass?:readonly number[];
 /** Key indices that are hits/landings: the curve arrives still travelling (1.3x the incoming slope), stops dead, then
  * rings out with a small analytic settle (exp(-18 tau) sin(38 tau), about `settle` of the incoming delta). */
 impact?:readonly number[];
 /** Key indices a strike accelerates INTO and carries through (a slash at contact): the tangent is 1.6x the incoming
  * slope, capped at 3x the outgoing one so the follow-through never overshoots its key. A reversal stops as usual. */
 drive?:readonly number[];
 /** Settle amplitude as a fraction of the incoming delta (default .08). */
 settle?:number;
}
const NONE:IntroCurveOptions={};
/** Cubic Hermite through (times, values). Unflagged keys use the same monotone tangents as poseCurve (no overshoot, a
 * stop only at a genuine extreme); `pass` and `impact` keys change that per key as described above. */
export function introCurve(times:readonly number[],values:readonly number[],time:number,options:IntroCurveOptions=NONE){
 const last=times.length-1;let value:number;
 if(time<=times[0])value=values[0];else if(time>=times[last])value=values[last];else{
  let i=0;while(times[i+1]<time)i++;
  const slope=(n:number)=>(values[n+1]-values[n])/(times[n+1]-times[n]);
  const tangent=(n:number,arriving:boolean)=>{
   if(n===0||n===last)return 0;
   if(options.impact?.includes(n))return arriving?1.3*slope(n-1):0;
   if(options.drive?.includes(n)){const a=slope(n-1),b=slope(n);return a*b<=0?0:Math.sign(a)*Math.min(1.6*Math.abs(a),3*Math.abs(b));}
   if(options.pass?.includes(n))return (values[n+1]-values[n-1])/(times[n+1]-times[n-1]);
   const a=slope(n-1),b=slope(n);return a*b<=0?0:2*a*b/(a+b);
  };
  const h=times[i+1]-times[i],u=(time-times[i])/h,u2=u*u,u3=u2*u;
  value=(2*u3-3*u2+1)*values[i]+(u3-2*u2+u)*h*tangent(i,false)+(-2*u3+3*u2)*values[i+1]+(u3-u2)*h*tangent(i+1,true);
 }
 if(options.impact)for(const n of options.impact){if(n<1)continue;value+=settle(time-times[n],(values[n]-values[n-1])*(options.settle??.08));}
 return value;
}
/** Damped ring-out after an impact: zero at tau<=0, starts moving in the direction of `amplitude`, gone by ~.3 s. */
export function settle(tau:number,amplitude:number){return tau<=0||tau>.6?0:amplitude*Math.exp(-18*tau)*Math.sin(38*tau);}
export const clamp01=(x:number)=>x<0?0:x>1?1:x;
export const smooth=(x:number)=>{x=clamp01(x);return x*x*(3-2*x);};
/** Ease-out that is still travelling at u=1 (a camera move that is alive when the cut arrives). */
export const easeOutLive=(x:number)=>{x=clamp01(x);return x*(1.6-.6*x);};
/** A 0..1..0 bump over [a,b] with smooth shoulders. */
export const bump=(x:number,a:number,b:number)=>{const u=clamp01((x-a)/(b-a));return Math.sin(Math.PI*u)**2;};
export interface HeroIdle{shift:number;roll:number;breath:number;headRoll:number;headYaw:number;sway:number;weight:number}
/** A living hold after the last settle key: a slow weight shift (~3.2 s), breathing, a head that settles a beat after the
 * chest and a small hand sway. Fades in over .6 s from `start`, so it grows out of the settle instead of starting on a
 * step. Pure function of time. */
export function heroIdle(time:number,start=3.2):HeroIdle{
 const u=time-start;if(u<=0)return {shift:0,roll:0,breath:0,headRoll:0,headYaw:0,sway:0,weight:0};
 const weight=smooth(u/.6),w=2*Math.PI/3.2,lagged=Math.max(0,u-.15);
 return {weight,shift:.018*Math.sin(w*u)*weight,roll:.025*Math.sin(w*u)*weight,breath:.012*Math.sin(2*Math.PI*u/2.6)*weight,
  headRoll:-.02*Math.sin(w*lagged)*weight,headYaw:.035*Math.sin(w*.5*lagged+.4)*weight*smooth(u/1.2),sway:.02*Math.sin(w*u+1.1)*weight};
}
