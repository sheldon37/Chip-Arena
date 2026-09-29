/** Monotone cubic Hermite interpolation carries velocity through passing poses,
 * preserves intentional reversals and never overshoots contact keys. */
export function poseCurve(times:number[],values:number[],time:number){
 if(time<=times[0])return values[0];const last=times.length-1;if(time>=times[last])return values[last];let i=0;while(times[i+1]<time)i++;
 const slope=(n:number)=>(values[n+1]-values[n])/(times[n+1]-times[n]);
 const tangent=(n:number)=>{if(n===0||n===last)return 0;const a=slope(n-1),b=slope(n);return a*b<=0?0:2*a*b/(a+b);};
 const h=times[i+1]-times[i],u=(time-times[i])/h,u2=u*u,u3=u2*u;
 return (2*u3-3*u2+1)*values[i]+(u3-2*u2+u)*h*tangent(i)+(-2*u3+3*u2)*values[i+1]+(u3-u2)*h*tangent(i+1);
}
