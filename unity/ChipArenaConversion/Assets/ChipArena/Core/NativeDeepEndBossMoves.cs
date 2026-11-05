using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The head's path: a list of legs on the clock, in the room's frame (from the abyss's centre, on the deck's level), each a pure function of time. A Hermite curve between two states, or a
    /// ballistic arc (a rise decelerates under its own weight, hangs and falls). Velocity is continuous across legs, so nothing ever cuts and nothing moves at a constant speed. A ring: the oldest
    /// legs fall off (the rig holds what it has already passed).
    /// </summary>
    public sealed class DeepEndRoute
    {
        public struct Leg{public float T0,T1,G;public Vector3 P0,V0,P1,V1;}
        private readonly Leg[] legs;private int first,count;
        public float EndTime{get;private set;}public Vector3 EndP{get;private set;}public Vector3 EndV{get;private set;}public float StartTime=>count>0?legs[first].T0:EndTime;
        public int Count=>count;
        public DeepEndRoute(int capacity=256){legs=new Leg[capacity];}
        public void Reset(float t,Vector3 p,Vector3 v){first=count=0;EndTime=t;EndP=p;EndV=v;last=0;}
        private void Push(in Leg l){if(count==legs.Length){first=(first+1)%legs.Length;count--;}legs[(first+count)%legs.Length]=l;count++;EndTime=l.T1;EndP=l.P1;EndV=l.V1;}
        /// <summary>A Hermite leg from the end state to `p1` moving at `v1` at time `t1`.</summary>
        public void To(float t1,Vector3 p1,Vector3 v1){if(!(t1>EndTime+1e-4f)){EndP=p1;EndV=v1;EndTime=Math.Max(EndTime,t1);return;}Push(new Leg{T0=EndTime,T1=t1,P0=EndP,V0=EndV,P1=p1,V1=v1,G=-1});}
        /// <summary>Holds still until `t1` (eased in from the end state, ending at rest).</summary>
        public void Hold(float t1)=>To(t1,EndP+EndV*Math.Min(.4f,Math.Max(0,t1-EndTime))*.5f,Vector3.Zero);
        /// <summary>A ballistic leg from the end state until `t1`, under gravity `g` (m/s^2, downward): the horizontal speed holds, the height is a true parabola.</summary>
        public void Fly(float t1,float g)
        {
            float T=t1-EndTime;if(!(T>1e-4f))return;var p=EndP+EndV*T+new Vector3(0,-.5f*g*T*T,0);var v=EndV+new Vector3(0,-g*T,0);Push(new Leg{T0=EndTime,T1=t1,P0=EndP,V0=EndV,P1=p,V1=v,G=g});
        }
        private int last;
        /// <summary>The head at `t`: before the first leg its start, past the last its end coasting to rest (eased over a second, so a late leg never jolts it).</summary>
        public void At(float t,out Vector3 p,out Vector3 v)
        {
            if(count==0||t>=EndTime){float u=Math.Max(0,t-EndTime);float k=Math.Min(1,u);v=EndV*(1-k)*(1-k);p=EndP+EndV*(k-k*k+k*k*k/3f);return;}
            if(t<=legs[first].T0){p=legs[first].P0;v=Vector3.Zero;return;}
            int i=last<count?last:0;
            while(i>0&&legs[(first+i)%legs.Length].T0>t)i--;
            while(i<count-1&&legs[(first+i)%legs.Length].T1<=t)i++;
            last=i;ref var l=ref legs[(first+i)%legs.Length];
            float tau=t-l.T0;
            if(l.G>=0){p=l.P0+l.V0*tau+new Vector3(0,-.5f*l.G*tau*tau,0);v=l.V0+new Vector3(0,-l.G*tau,0);return;}
            float T=l.T1-l.T0,s=tau/T,s2=s*s,s3=s2*s;
            p=l.P0*(2*s3-3*s2+1)+l.V0*(T*(s3-2*s2+s))+l.P1*(-2*s3+3*s2)+l.V1*(T*(s3-s2));
            v=(l.P0*(6*s2-6*s)+l.P1*(-6*s2+6*s))/T+l.V0*(3*s2-4*s+1)+l.V1*(3*s2-2*s);
        }
    }

    /// <summary>
    /// The moves as paths of the head (doc sections 4 and 6): given the record, what the head does on the record's own hit. Each path puts the contact on `hitAt`: the mouth crosses the surface
    /// at Breach's hit, the body lands at the Flop's, the wave front leaves at Seiche's; everything else eases around those moments. The body is a trail of the head (Core DeepEndBossRig), so a
    /// path is all a move needs. <see cref="Append"/> is called once per action start, by the rig (live) or by the DEV preview's script at its start. Pure. Presentation only.
    /// </summary>
    public static class DeepEndBossPaths
    {
        /// <summary>A rise's weight (m/s^2): lighter than the world's, the way a huge body in water moves.</summary>
        public const float RiseG=8.2f,IntroG=2.72f;
        /// <summary>How far under the surface it swims hidden; the arc's shallowest curvature needs this horizontal speed through a leap (m/s).</summary>
        public const float SwimDepth=4.6f,LeapHorizontal=7f,LeapRise=9.6f,FlopDepth=4.4f;
        private static Vector3 Flat(Vector3 d){d.Y=0;float l=d.Length();return l>1e-4f?d/l:new Vector3(0,0,1);}
        private static float Hash(uint s,int i)=>BossMath.Hash(s,i);
        /// <summary>A hidden swim to under (x, z): under the surface by the swim's depth, arriving with `v`.</summary>
        public static void Swim(DeepEndRoute r,float t1,float x,float z,float surface,Vector3 v){r.To(t1,new Vector3(x,Math.Min(surface-SwimDepth,-2.8f),z),v);}
        /// <summary>The swim between two moves when the record gives none: a slow S through the water under the pit's edge, its offset from the seed.</summary>
        public static void Cruise(DeepEndRoute r,float t1,Vector3 to,uint seed)
        {
            float T=t1-r.EndTime;if(!(T>.05f))return;var a=r.EndP;var mid=(a+to)*.5f;var off=Vector3.Cross(Flat(to-a),Vector3.UnitY);mid+=off*((Hash(seed,1)-.5f)*Math.Min(12,(to-a).Length()*.6f));
            var dir=Flat(to-a);float speed=Math.Min(9,(to-a).Length()/Math.Max(.2f,T)*1.05f);r.To(r.EndTime+T*.5f,mid,dir*speed);r.To(t1,to,dir*speed*.7f);
        }
        /// <summary>The arrival: up the shaft on a helix out of the black, narrowing to the centre, breaking the surface at the beat, rising under its own weight toward the party, hanging, and falling forward into the fight.</summary>
        public static void Arrival(DeepEndRoute r,float spawned,in DeepEndBossBeats b,float yawRad)
        {
            float surface=PoolroomsLayout.WaterY;var f=BossMath.Forward(yawRad);var side=BossMath.Right(yawRad);
            const float vy=5.6f,vh=2.9f;float apexT=vy/IntroG;
            float t0=spawned+2.55f,tb=spawned+b.Break;float T=tb-t0;const float depth=23,R=2.6f,turns=1.05f;
            r.Reset(spawned,new Vector3(R,surface-depth-34,0),Vector3.Zero);r.Hold(spawned+.3f);
            // Up from far below, out of sight, straight up the shaft to the helix's start.
            Vector3 H(float u)
            {
                float y=surface-(1-u)*vy*T-(depth-vy*T)*(1-u)*(1-u);float a=BossMath.Tau*turns*u+1.2f;float k=BossMath.Smoother((u-.62f)/.38f);float rad=R*(1-k);
                return new Vector3(rad*(float)Math.Cos(a),y,rad*(float)Math.Sin(a))-f*(NativeDeepEndBoss.BreakBack*k);
            }
            Vector3 D(float u){const float e=.002f;return (H(Math.Min(1,u+e))-H(Math.Max(0,u-e)))/((Math.Min(1,u+e)-Math.Max(0,u-e))*T);}
            r.To(t0,H(0),D(0));
            const int n=26;for(int i=1;i<=n;i++){float u=i/(float)n;var v=i==n?f*vh+new Vector3(0,vy,0):D(u);r.To(t0+T*u,H(u),v);}
            // The rise: a true parabola to the hang, the fall forward past the hold's end, into the water.
            float fallT=(float)Math.Sqrt(2*(vy*vy/(2*IntroG))/IntroG);r.Fly(tb+apexT+fallT,IntroG);
            // Under, and back round toward the pit.
            var p=r.EndP;var v0=r.EndV;r.To(r.EndTime+1.3f,new Vector3(p.X,surface-4.2f,p.Z)+f*3.4f,f*3.2f+new Vector3(0,-1.2f,0));
        }
        /// <summary>A move's path from the head's state at its start (on the record's own times).</summary>
        public static void Append(DeepEndRoute r,in DeepEndBossView v,float surface)
        {
            var m=default(DeepEndBossMove);int k=v.Move;if(k<0)return;m=NativeDeepEndBoss.Moves[k];float a=v.actionAt,hit=DeepEndBossView.At(v.hitAt)?v.hitAt:a+m.Anticipation,len=DeepEndBossView.At(v.duration)?v.duration:m.Duration,end=a+len;
            var t=new Vector3(v.targetX,0,v.targetZ);var c=new Vector3(v.x,0,v.z);bool hasTarget=!float.IsNaN(v.targetX);if(!hasTarget)t=c;
            switch(m.Id)
            {
                case "breach":
                {
                    // Comes up under the locked circle, mouth up, crossing the surface on the hit; arcs over toward the room's middle and dives.
                    var dir=Flat(-t);if(t.LengthSquared()<4)dir=new Vector3((float)Math.Sin(v.yaw*BossMath.Deg),0,(float)Math.Cos(v.yaw*BossMath.Deg));
                    var cross=new Vector3(t.X,surface,t.Z);var launch=dir*LeapHorizontal+new Vector3(0,LeapRise,0);
                    Swim(r,hit-.62f,t.X-dir.X*5.2f,t.Z-dir.Z*5.2f,surface,dir*4+new Vector3(0,3.6f,0));
                    r.To(hit,cross,launch);
                    float flight=2*LeapRise/RiseG;r.Fly(hit+flight,RiseG);
                    var p=r.EndP;var e=r.EndV;r.To(r.EndTime+.9f,p+dir*5+new Vector3(0,-4.2f,0),dir*3.6f+new Vector3(0,-1.6f,0));
                    break;
                }
                case "jet":
                {
                    // Rises where it is, mouth toward the far pilot; holds through the sweep (its neck swings it, the rig's look), then rolls forward and goes down.
                    var dir=Flat(t-c);var top=new Vector3(c.X,surface+4.4f,c.Z);
                    Swim(r,a+.08f,c.X-dir.X*3,c.Z-dir.Z*3,surface,new Vector3(0,3.4f,0));
                    r.To(hit-.08f,top,dir*.6f+new Vector3(0,.5f,0));
                    r.To(hit+m.Strike+m.Follow,top+new Vector3(0,.5f,0)+dir*.6f,dir*.3f);
                    r.To(end,top+dir*2.2f+new Vector3(0,-.6f,0),dir*3.2f+new Vector3(0,-2.6f,0));
                    r.Fly(end+1.15f,RiseG*.9f);var p=r.EndP;r.To(r.EndTime+.8f,p+dir*3.4f+new Vector3(0,-3.8f,0),dir*2.6f+new Vector3(0,-1,0));
                    break;
                }
                case "flop":
                {
                    // Under the line at speed while the shadow darkens (hidden under the tile), so that when the rig's lift brings it up the whole body lies along it: the lift raises it and the hit lands it (Rig FlopLift).
                    var from=t;var to=new Vector3(v.aimX,0,v.aimZ);var dir=Flat(to-from);float depth=Math.Min(surface-FlopDepth,-3f);
                    r.To(a+.1f,from+new Vector3(0,depth,0)-dir*8,dir*30);
                    r.To(a+.92f,to+new Vector3(0,depth,0),dir*6);
                    r.To(hit,to+dir*1.8f+new Vector3(0,depth,0),dir*2.4f);
                    r.To(end,to+dir*3.2f+new Vector3(0,depth,0),dir*1.1f);
                    r.To(end+1.2f,to+dir*2+new Vector3(0,depth-.4f,0),-dir*2.4f+new Vector3(0,-1,0));
                    break;
                }
                case "thrash":
                {
                    // Holds high at the surface while the tail comes round (Rig ThrashSweep).
                    var dir=BossMath.Forward(v.yaw*BossMath.Deg);var at=r.EndP;var near=new Vector3(at.X,surface+1.6f,at.Z);
                    r.To(hit,near+dir*1.4f,dir*1.8f+new Vector3(0,.7f,0));r.To(end,near+dir*3.6f,dir*1.8f+new Vector3(0,-.7f,0));r.Fly(end+.9f,RiseG*.8f);
                    break;
                }
                case "drain":
                {
                    // Up out of the abyss at the room's centre, the mouth open over the pit, turning slowly with the water.
                    var top=new Vector3(0,surface+3.4f,0);
                    r.To(a+.3f,new Vector3(0,surface-9,0),new Vector3(0,6.5f,0));
                    r.To(hit-.05f,top,new Vector3(0,.5f,0));
                    r.To(hit+m.Strike+m.Follow,top+new Vector3(0,.4f,0),Vector3.Zero);
                    r.To(end+1.2f,new Vector3(0,surface-3.2f,0),new Vector3(0,-5.8f,0));
                    r.To(end+2.1f,new Vector3(0,surface-8,0),new Vector3(0,-3.4f,0));
                    break;
                }
                case "seiche":
                {
                    // To the far wall (hidden), then its back drives the wave across the room, breaking through the crest. The wave goes on and reflects without it.
                    var d=new Vector3(v.aimX,0,v.aimZ);if(d.LengthSquared()<.01f)d=new Vector3(0,0,1);d=Flat(d);float w=NativeDeepEndArena.Half-1.4f;
                    Swim(r,hit-.3f,-d.X*(w+3),-d.Z*(w+3),surface,d*6);
                    r.To(hit,-d*w+new Vector3(0,surface-1.1f,0),d*m.Speed);
                    r.To(hit+m.Strike*.5f,d*(w-2)+new Vector3(0,surface-1.2f,0),d*m.Speed*.8f);
                    r.To(hit+m.Strike*.5f+1.1f,d*(w-5)+new Vector3(0,surface-5,0),-d*3+new Vector3(0,-1,0));
                    break;
                }
                case "lap":
                {
                    // Round the room's edge at the surface, tightening; rolled so the flank lamps show (the rig's roll). A rounded square, not a circle: it runs into the corners, so no corner is outside its lap.
                    const int n=24;float T=m.Strike+m.Follow,ang=v.yaw*BossMath.Deg;
                    Vector3 Lap(float u){var q=LapAt(u,ang,m,T);q.Y=surface-.25f;return q;}
                    Vector3 Dl(float u){const float e=.004f;return (Lap(u+e)-Lap(u-e))/(2*e*T);}
                    Swim(r,hit-.4f,Lap(0).X*.9f,Lap(0).Z*.9f,surface,Dl(0)*.6f+new Vector3(0,3,0));
                    r.To(hit,Lap(0),Dl(0));for(int i=1;i<=n;i++){float u=i/(float)n;r.To(hit+T*u,Lap(u),Dl(u));}
                    var p=Lap(1);var e2=Dl(1);r.To(r.EndTime+1.4f,p+e2*.8f+new Vector3(0,-4.4f,0),e2*.3f+new Vector3(0,-1.2f,0));
                    break;
                }
                default:break; // The Lull and the Whistle don't move the head.
            }
        }
        /// <summary>Last Lap's path at u (0..1 over its `T` s), from the start angle: a rounded square tightening from the table's radius to its inner one, at the table's speed.</summary>
        public static Vector3 LapAt(float u,float ang,in DeepEndBossMove m,float T)
        {
            float rad=m.Radius+(NativeDeepEndBoss.LapInner-m.Radius)*BossMath.Smooth(BossMath.Clamp01(u));float mean=(m.Radius+NativeDeepEndBoss.LapInner)*.5f;float an=ang+u*T*m.Speed/Math.Max(8,mean);
            float cs=(float)Math.Cos(an),sn=(float)Math.Sin(an);const float k=6;float sq=(float)Math.Pow(Math.Pow(Math.Abs(cs),k)+Math.Pow(Math.Abs(sn),k),-1/k);return new Vector3(rad*sq*cs,0,rad*sq*sn);
        }
        /// <summary>A phase change: the head comes up through the middle of the pit and stands over it for the 3.5 s window, then sinks.</summary>
        public static void PhaseChange(DeepEndRoute r,float at,float surface,int phase)
        {
            var top=new Vector3(0,surface+5.1f,0);r.To(at+.25f,new Vector3(0,surface-7.5f,0),new Vector3(0,6.2f,0));r.To(at+1.5f,top,new Vector3(0,.8f,0));
            r.To(at+NativeDeepEndBoss.PhaseChangeSeconds-.3f,top+new Vector3(0,.25f,0),Vector3.Zero);r.To(at+NativeDeepEndBoss.PhaseChangeSeconds+1.4f,new Vector3(0,surface-3,0),new Vector3(0,-5,0));r.To(r.EndTime+.8f,new Vector3(0,surface-7.5f,0),new Vector3(0,-3,0));
        }
        /// <summary>The death: it comes to stand in the pit, the mouth closing, straight up out of the shaft: then the rig draws the whole body back down it, tail first, so the crown goes last (Rig Retract).</summary>
        public static void Death(DeepEndRoute r,float at,float surface,float settle)
        {
            r.To(at+settle*.55f,new Vector3(0,surface+.5f,0),new Vector3(0,1.4f,0));r.To(at+settle,new Vector3(0,surface+2.1f,0),new Vector3(0,.1f,0));
        }
    }
}
