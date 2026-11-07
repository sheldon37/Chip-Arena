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
        public struct Leg{public float T0,T1,Span,G;public Vector3 P0,V0,P1,V1;}
        private readonly Leg[] legs;private int first,count;
        public float EndTime{get;private set;}public Vector3 EndP{get;private set;}public Vector3 EndV{get;private set;}public float StartTime=>count>0?legs[first].T0:EndTime;
        public int Count=>count;
        public DeepEndRoute(int capacity=256){legs=new Leg[capacity];}
        public void Reset(float t,Vector3 p,Vector3 v){first=count=0;EndTime=t;EndP=p;EndV=v;last=0;}
        private void Push(in Leg l){if(count==legs.Length){first=(first+1)%legs.Length;count--;}legs[(first+count)%legs.Length]=l;count++;EndTime=l.T1;EndP=l.P1;EndV=l.V1;}
        /// <summary>A Hermite leg from the end state to `p1` moving at `v1` at time `t1`.</summary>
        public void To(float t1,Vector3 p1,Vector3 v1){if(!(t1>EndTime+1e-4f)){EndP=p1;EndV=v1;EndTime=Math.Max(EndTime,t1);return;}Push(new Leg{T0=EndTime,T1=t1,Span=t1-EndTime,P0=EndP,V0=EndV,P1=p1,V1=v1,G=-1});}
        /// <summary>Holds still until `t1` (eased in from the end state, ending at rest).</summary>
        public void Hold(float t1)=>To(t1,EndP+EndV*Math.Min(.4f,Math.Max(0,t1-EndTime))*.5f,Vector3.Zero);
        /// <summary>A ballistic leg from the end state until `t1`, under gravity `g` (m/s^2, downward): the horizontal speed holds, the height is a true parabola.</summary>
        public void Fly(float t1,float g)
        {
            float T=t1-EndTime;if(!(T>1e-4f))return;var p=EndP+EndV*T+new Vector3(0,-.5f*g*T*T,0);var v=EndV+new Vector3(0,-g*T,0);Push(new Leg{T0=EndTime,T1=t1,Span=T,P0=EndP,V0=EndV,P1=p,V1=v,G=g});
        }
        /// <summary>The route's end meets something: it goes on from here with this velocity (the one place a velocity may cut).</summary>
        public void Impact(Vector3 v){EndV=v;}
        /// <summary>Drops whatever the route still planned past `t` and ends it where the head is then, moving as it moves then: what a new move starts from. A move's legs run on past its record's end (its fall, its dive), and the rules start the next one on their own clock, so the next must carry on from here and not from the end of a plan it has cut off.</summary>
        public void CutAt(float t)
        {
            if(t>=EndTime){if(t<=EndTime+1e-4f)return;At(t,out var pc,out var vc);To(t,pc,vc);return;}
            At(t,out var p,out var v);
            while(count>0&&legs[(first+count-1)%legs.Length].T0>=t)count--;
            if(count>0)legs[(first+count-1)%legs.Length].T1=t;
            EndTime=t;EndP=p;EndV=v;last=0;
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
            float T=l.Span,s=tau/T,s2=s*s,s3=s2*s;
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
        /// <summary>How high the Jet holds its mouth over the water, the Flop's leap (its flight's seconds, its height), how high the head rides when it hauls itself over dry deck.</summary>
        public const float JetRise=7.2f,DrainRise=6.4f,FlopFlight=1.4f,FlopHeight=5.4f,DeckRide=1.02f;
        /// <summary>Whether (x, z) has water over it: the pool always, the deck only once the tide covers it.</summary>
        public static bool Wet(float x,float z,float surface)=>surface>.02f||NativeDeepEndArena.InPool(x,z);
        /// <summary>A place brought into the water: with the decks dry, `margin` inside the pool's edge; with the tide up it stays.</summary>
        public static Vector3 InWater(Vector3 p,float surface,float margin)
        {
            if(surface>.02f)return p;float e=NativeDeepEndArena.PoolHalf-margin;p.X=Math.Max(-e,Math.Min(e,p.X));p.Z=Math.Max(-e,Math.Min(e,p.Z));return p;
        }
        /// <summary>A height a resting body lies at: half in the water, or on the deck where it's dry.</summary>
        public static float RestY(float x,float z,float surface)=>Wet(x,z,surface)?surface+.42f:DeckRide;
        /// <summary>
        /// Gets the head out of sight from wherever a cut left it, without a jump: out of the air along its own momentum and under; stranded on dry deck, it hauls itself over the tile to the pool's edge first and goes in (never through the floor).
        /// A head already under stays where it is. Everything a move does afterwards starts from there.
        /// </summary>
        public static void Under(DeepEndRoute r,float surface)
        {
            var p=r.EndP;var v=r.EndV;
            if(!Wet(p.X,p.Z,surface))
            {
                var edge=InWater(p,surface,1.6f);var to=edge-p;to.Y=0;float d=to.Length();var dir=d>.01f?to/d:new Vector3(0,0,1);float T=Math.Max(.28f,Math.Min(1.9f,d/8.5f));
                r.To(r.EndTime+T,new Vector3(edge.X,DeckRide,edge.Z),dir*7);r.To(r.EndTime+.42f,new Vector3(edge.X,surface-2.7f,edge.Z)+dir*2.6f,dir*3.2f+new Vector3(0,-6.4f,0));return;
            }
            if(p.Y<surface-1.6f)return;
            // Forward, always: going straight back down would fold the body over itself. With no way of its own it turns toward the room's middle (the water, on dry decks).
            float drop=p.Y-(surface-2.7f),fall=Math.Max(.2f,Math.Min(.7f,drop/9f)),flat=new Vector2(v.X,v.Z).Length();var f=flat>1.4f?Flat(v):Flat(-p);
            float reach=Math.Max(Math.Min(4.2f,drop*.55f),flat*fall*.55f);var down=InWater(new Vector3(p.X,surface-2.7f,p.Z)+f*reach,surface,1.2f);r.To(r.EndTime+fall,down,f*Math.Max(2.6f,flat*.4f)+new Vector3(0,-6.8f,0));
        }
        /// <summary>From a hold at the top of a rise, over and down: it rolls forward over `dir`, falls under its own weight into the water ahead of it and goes on under. (A body follows its head: the head can't come back down the way it went up.)</summary>
        public static void Dive(DeepEndRoute r,Vector3 dir,float surface)
        {
            var p=r.EndP;if(surface<.02f&&!NativeDeepEndArena.InPool(p.X+dir.X*7,p.Z+dir.Z*7))dir=Flat(-p);
            r.To(r.EndTime+.42f,p+dir*1.3f+new Vector3(0,.12f,0),dir*3.4f+new Vector3(0,-1.1f,0));
            var q=r.EndP;var vq=r.EndV;float g=RiseG*.9f,h=Math.Max(0,q.Y-(surface-1.1f));float T=(vq.Y+(float)Math.Sqrt(vq.Y*vq.Y+2*g*h))/g;r.Fly(r.EndTime+T,g);
            var e=r.EndP;r.To(r.EndTime+.7f,e+dir*2.6f+new Vector3(0,-3.4f,0),dir*2.2f+new Vector3(0,-2.6f,0));
        }
        // A leg no earlier than a moment after the route's end: a cut's dive can run past a move's own timetable, and a move's legs then follow it rather than overwrite it.
        private static void Then(DeepEndRoute r,float t1,Vector3 p,Vector3 v){r.To(Math.Max(t1,r.EndTime+Math.Max(.05f,Vector3.Distance(r.EndP,p)/HiddenTop)),p,v);}
        /// <summary>The fastest it travels out of sight (m/s): the water is clear, so even a hidden swim has to take some time.</summary>
        public const float HiddenTop=64;
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
            const float vy=5.6f,vh=1.9f;float apexT=vy/IntroG;
            float t0=spawned+2.55f,tb=spawned+b.Break;float T=tb-t0;const float depth=42,R=2.6f,turns=1.75f;
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
                    // (It only surfaces where there's water: over dry decks its circle comes in to the pool's edge.)
                    if(surface<0&&!NativeDeepEndArena.InPool(t.X,t.Z)){float pe=NativeDeepEndArena.PoolHalf-.5f;t.X=Math.Max(-pe,Math.Min(pe,t.X));t.Z=Math.Max(-pe,Math.Min(pe,t.Z));}
                    Under(r,surface);
                    var dir=Flat(-t);if(t.LengthSquared()<4)dir=new Vector3((float)Math.Sin(v.yaw*BossMath.Deg),0,(float)Math.Cos(v.yaw*BossMath.Deg));
                    // With the decks dry it arcs toward the pool's far corner, no further than the water goes: it comes down in the pool, not on the tile.
                    float across=LeapHorizontal;
                    if(surface<=.02f){float pe2=NativeDeepEndArena.PoolHalf-1.2f;var far=new Vector3(t.X>0?-pe2:pe2,0,t.Z>0?-pe2:pe2);dir=Flat(far-t);across=Math.Min(LeapHorizontal,Math.Max(1.6f,(far-new Vector3(t.X,0,t.Z)).Length()-5.4f)*RiseG/(2*LeapRise));}
                    var cross=new Vector3(t.X,surface,t.Z);var launch=dir*across+new Vector3(0,LeapRise,0);
                    if(hit-.62f>r.EndTime+.05f)Swim(r,hit-.62f,t.X-dir.X*5.2f,t.Z-dir.Z*5.2f,surface,dir*4+new Vector3(0,3.6f,0));
                    Then(r,hit,cross,launch);
                    float flight=2*LeapRise/RiseG;r.Fly(hit+flight,RiseG);
                    var p=r.EndP;var e=r.EndV;r.To(r.EndTime+.9f,p+dir*5+new Vector3(0,-4.2f,0),dir*3.6f+new Vector3(0,-1.6f,0));
                    break;
                }
                case "jet":
                {
                    // Rises where it is, mouth toward the far pilot; holds through the sweep (its neck swings it, the rig's look), then rolls forward and goes down.
                    Under(r,surface);
                    // Over dry decks it stays well inside the pool: its fall forward has to land in the water too.
                    c=InWater(c,surface,5.4f);var dir=Flat(t-c);var top=new Vector3(c.X,surface+JetRise,c.Z);
                    // Up on a long slant from behind the place, so the body stands as one big curve under the head.
                    if(a+.08f>r.EndTime+.05f)Swim(r,a+.08f,c.X-dir.X*5.2f,c.Z-dir.Z*5.2f,surface,dir*2.4f+new Vector3(0,4.2f,0));
                    Then(r,hit-.08f,top,dir*1.5f+new Vector3(0,.6f,0));
                    Then(r,hit+m.Strike+m.Follow,top+new Vector3(0,.45f,0)+dir*.9f,dir*.35f);
                    Dive(r,dir,surface);
                    break;
                }
                case "flop":
                {
                    // A real leap. It runs up under the water to the line's start while the shadow darkens, comes out of the water and flies the length of the line, and the whole body comes down along it on the hit (the rig's Slam
                    // lays the arc it left behind flat, so all of it lands). Where the line ends on dry deck it lies stranded on the tile, then hauls itself back over the edge into the pool; it is never under the tile.
                    Under(r,surface);
                    var from=InWater(t,surface,2.4f);var to=new Vector3(v.aimX,0,v.aimZ);var dir=Flat(to-from);float length=new Vector2(to.X-from.X,to.Z-from.Z).Length();
                    float launchAt=Math.Max(hit-FlopFlight,r.EndTime+.2f+Vector3.Distance(r.EndP,new Vector3(from.X,surface,from.Z))/HiddenTop),T=Math.Max(.5f,hit-launchAt);float landY=RestY(to.X,to.Z,surface),startY=surface;
                    float g=8*FlopHeight/(T*T),rise=(landY-startY)/T+.5f*g*T;var launchV=dir*(length/T)+new Vector3(0,rise,0);
                    float underY=Math.Min(surface-2.6f,-2.2f);
                    Then(r,launchAt-.14f,new Vector3(from.X,underY,from.Z)-dir*2.4f,dir*Math.Min(12,length/T)+new Vector3(0,rise*.72f,0));
                    Then(r,launchAt,new Vector3(from.X,startY,from.Z),launchV);
                    r.Fly(Math.Max(hit,r.EndTime+.2f),g);
                    // It lands going forward; the slide dies away on the tile or in the water.
                    var land=r.EndP;r.Impact(dir*2.4f);r.To(Math.Max(hit+m.Strike+.5f,r.EndTime+.2f),new Vector3(land.X,RestY(land.X,land.Z,surface),land.Z)+dir*1.1f,dir*.35f);
                    var rest=r.EndP;r.To(Math.Max(end,r.EndTime+.2f),rest+dir*.3f,dir*.1f);
                    if(!Wet(rest.X,rest.Z,surface)){r.Hold(Math.Max(r.EndTime+.05f,hit+m.Strike+NativeDeepEndBoss.StrandSeconds));Under(r,surface);}
                    else{var back=r.EndP;r.To(r.EndTime+.9f,back+dir*2.4f+new Vector3(0,Math.Min(surface-3.2f,-2.6f)-back.Y,0),dir*1.8f+new Vector3(0,-3,0));}
                    break;
                }
                case "thrash":
                {
                    // Holds high at the surface while the tail comes round (Rig ThrashSweep).
                    var dir=BossMath.Forward(v.yaw*BossMath.Deg);var at=InWater(r.EndP,surface,4.6f);var near=new Vector3(at.X,surface+1.6f,at.Z);
                    if(surface<.02f&&!NativeDeepEndArena.InPool(near.X+dir.X*6,near.Z+dir.Z*6))dir=Flat(-near);
                    Then(r,hit,near+dir*1.4f,dir*1.8f+new Vector3(0,.7f,0));Then(r,end,near+dir*3.6f,dir*1.8f+new Vector3(0,-.7f,0));r.Fly(r.EndTime+.55f,RiseG*.8f);var p=r.EndP;r.To(r.EndTime+.7f,p+dir*2.6f+new Vector3(0,Math.Min(surface-3.2f,p.Y-3)-p.Y,0),dir*2+new Vector3(0,-2.6f,0));
                    break;
                }
                case "drain":
                {
                    // Up out of the abyss at the room's centre, the mouth open over the pit, turning slowly with the water.
                    Under(r,surface);
                    var top=new Vector3(0,surface+DrainRise,0);var f=BossMath.Forward(v.actionId*2.4f);
                    Then(r,a+.3f,new Vector3(0,surface-9,0)-f*2.6f,f*1.2f+new Vector3(0,6.5f,0));
                    Then(r,hit-.05f,top,f*1.2f+new Vector3(0,.5f,0));
                    Then(r,hit+m.Strike+m.Follow,top+f*.5f+new Vector3(0,.4f,0),f*.3f);
                    Dive(r,f,surface);
                    break;
                }
                case "seiche":
                {
                    // To the far wall (hidden), then its back drives the wave across the room, breaking through the crest. The wave goes on and reflects without it.
                    Under(r,surface);
                    var d=new Vector3(v.aimX,0,v.aimZ);if(d.LengthSquared()<.01f)d=new Vector3(0,0,1);d=Flat(d);float w=NativeDeepEndArena.Half-1.4f;
                    if(hit-.3f>r.EndTime+.05f)Swim(r,hit-.3f,-d.X*(w+3),-d.Z*(w+3),surface,d*6);
                    Then(r,hit,-d*w+new Vector3(0,surface-1.1f,0),d*m.Speed);
                    r.To(hit+m.Strike*.5f,d*(w-2)+new Vector3(0,surface-1.2f,0),d*m.Speed*.8f);
                    r.To(hit+m.Strike*.5f+1.1f,d*(w-5)+new Vector3(0,surface-5,0),d*3+new Vector3(0,-3.2f,0));
                    break;
                }
                case "lap":
                {
                    // Round the room's edge at the surface, tightening; rolled so the flank lamps show (the rig's roll). A rounded square, not a circle: it runs into the corners, so no corner is outside its lap.
                    const int n=24;float T=m.Strike+m.Follow,ang=v.yaw*BossMath.Deg;
                    Vector3 Lap(float u){var q=LapAt(u,ang,m,T);q.Y=surface-.25f;return q;}
                    Vector3 Dl(float u){const float e=.004f;return (Lap(u+e)-Lap(u-e))/(2*e*T);}
                    Under(r,surface);
                    if(hit-.4f>r.EndTime+.05f)Swim(r,hit-.4f,Lap(0).X*.9f,Lap(0).Z*.9f,surface,Dl(0)*.6f+new Vector3(0,3,0));
                    Then(r,hit,Lap(0),Dl(0));for(int i=1;i<=n;i++){float u=i/(float)n;r.To(hit+T*u,Lap(u),Dl(u));}
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
            Under(r,surface);var f=BossMath.Forward(phase*1.9f);var top=new Vector3(0,surface+5.1f,0);Then(r,at+.25f,new Vector3(0,surface-7.5f,0)-f*2.2f,f*1.1f+new Vector3(0,6.2f,0));Then(r,at+1.5f,top,f*1.1f+new Vector3(0,.8f,0));
            Then(r,at+NativeDeepEndBoss.PhaseChangeSeconds-.3f,top+f*.4f+new Vector3(0,.25f,0),f*.25f);Dive(r,f,surface);
        }
        /// <summary>The death: it comes to stand in the pit, the mouth closing, straight up out of the shaft: then the rig draws the whole body back down it, tail first, so the crown goes last (Rig Retract).</summary>
        public static void Death(DeepEndRoute r,float at,float surface,float settle)
        {
            Under(r,surface);Then(r,at+settle*.55f,new Vector3(0,surface+.5f,0),new Vector3(0,1.4f,0));Then(r,at+settle,new Vector3(0,surface+2.1f,0),new Vector3(0,.1f,0));
        }
    }
}
