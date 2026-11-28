using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Poolrooms' water as a wave field (docs/BACKROOMS.md "The water's waves"; owner 2026-11-02: "can we make sure to add
    /// fluid water physics?"): a <see cref="Size"/> x Size grid of <see cref="Texel"/> m round the eye over a layout's pools, a
    /// damped wave equation stepped at a fixed <see cref="Rate"/>. Only pool cells are wet; a dry neighbour mirrors the wet one,
    /// so a pool's sides reflect the waves and no wave crosses a deck. What moves through or drops into the water disturbs it
    /// (<see cref="NativePoolWaves"/>). Heights in metres, up from the resting surface. Presentation only: what water does to
    /// bodies is the rules'. Pure; deterministic for one sequence of calls.
    /// </summary>
    public sealed class PoolWaves
    {
        /// <summary>The grid (texels a side, metres a texel: 32 m round the eye), its step rate (Hz), the waves' speed (m/s, a
        /// pool's ripples), the damping each step (about half the height lost a second) and the most a wave stands (m).</summary>
        public const int Size=128;public const float Texel=.25f,Rate=60f,Speed=1.5f,Damping=.989f,Limit=.16f;
        /// <summary>At most this many steps in one call (a hitch drops the backlog rather than spiralling).</summary>
        public const int MaxSteps=4;
        /// <summary>How far the eye may wander from the grid's middle (texels) before it moves (a whole number of texels, the waves
        /// carried with it).</summary>
        public const int Slack=16;
        /// <summary>Heights per texel (x + Size z), the grid's corner at (<see cref="CornerX"/>, <see cref="CornerZ"/>) texels from
        /// the layout's origin.</summary>
        public float[] Height{get;private set;}=new float[Size*Size];
        private float[] prev=new float[Size*Size],next=new float[Size*Size];
        public readonly bool[] Wet=new bool[Size*Size];
        public int CornerX,CornerZ;public Vector3 Origin{get;private set;}
        private PoolroomsLayout layout;private double carry;
        /// <summary>Steps taken since the grid was placed (checks).</summary>
        public long Steps{get;private set;}
        public PoolroomsLayout Layout=>layout;

        /// <summary>Whether the point (world x, z) is over a pool of layout `l` laid at `origin`.</summary>
        public static bool WaterAt(PoolroomsLayout l,Vector3 origin,float wx,float wz)
        {
            float c=PoolroomsLayout.Cell;int x=(int)MathF.Floor((wx-origin.X)/c),z=(int)MathF.Floor((wz-origin.Z)/c);
            return l.Inside(x,z)&&PoolroomsLayout.Pool(l.Floor[l.Index(x,z)]);
        }
        /// <summary>Keeps the grid round the eye over layout `l` at `origin`: placed afresh (still water) for a new layout,
        /// moved by whole texels (the waves carried, new ground still) once the eye strays <see cref="Slack"/> texels.</summary>
        public void Follow(PoolroomsLayout l,Vector3 origin,Vector3 eye)
        {
            int wantX=(int)MathF.Floor((eye.X-origin.X)/Texel)-Size/2,wantZ=(int)MathF.Floor((eye.Z-origin.Z)/Texel)-Size/2;
            if(!ReferenceEquals(l,layout)||origin!=Origin)
            {
                layout=l;Origin=origin;CornerX=wantX;CornerZ=wantZ;carry=0;Steps=0;
                Array.Clear(Height,0,Height.Length);Array.Clear(prev,0,prev.Length);MarkWet();return;
            }
            int dx=wantX-CornerX,dz=wantZ-CornerZ;if(Math.Abs(dx)<Slack&&Math.Abs(dz)<Slack)return;
            Shift(Height,next,dx,dz);(Height,next)=(next,Height);Shift(prev,next,dx,dz);(prev,next)=(next,prev);
            CornerX=wantX;CornerZ=wantZ;MarkWet();
        }
        private static void Shift(float[] from,float[] to,int dx,int dz)
        {
            Array.Clear(to,0,to.Length);
            for(int z=0;z<Size;z++){int sz=z+dz;if(sz<0||sz>=Size)continue;for(int x=0;x<Size;x++){int sx=x+dx;if(sx<0||sx>=Size)continue;to[x+Size*z]=from[sx+Size*sz];}}
        }
        private void MarkWet()
        {
            for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
            {
                int i=x+Size*z;Wet[i]=WaterAt(layout,Origin,Origin.X+(CornerX+x+.5f)*Texel,Origin.Z+(CornerZ+z+.5f)*Texel);
                if(!Wet[i]){Height[i]=0;prev[i]=0;}
            }
        }
        /// <summary>Pushes the surface by `amount` (m; down negative) over a round patch of `radius` m at (world x, z), eased to
        /// nothing at its rim; only the wet texels, within <see cref="Limit"/>. Returns whether it touched water.</summary>
        public bool Disturb(float wx,float wz,float radius,float amount)
        {
            if(layout==null)return false;float gx=(wx-Origin.X)/Texel-CornerX-.5f,gz=(wz-Origin.Z)/Texel-CornerZ-.5f,r=Math.Max(radius/Texel,.75f);
            int x0=Math.Max(0,(int)MathF.Floor(gx-r)),x1=Math.Min(Size-1,(int)MathF.Ceiling(gx+r)),z0=Math.Max(0,(int)MathF.Floor(gz-r)),z1=Math.Min(Size-1,(int)MathF.Ceiling(gz+r));
            bool any=false;
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                int i=x+Size*z;if(!Wet[i])continue;float d=MathF.Sqrt((x-gx)*(x-gx)+(z-gz)*(z-gz));if(d>=r)continue;
                float w=.5f*(1+MathF.Cos(MathF.PI*d/r));Height[i]=Math.Clamp(Height[i]+amount*w,-Limit,Limit);any=true;
            }
            return any;
        }
        /// <summary>Advances the waves by `dt` s in fixed steps (at most <see cref="MaxSteps"/>; a longer gap drops the rest).</summary>
        public void Step(double dt)
        {
            if(layout==null||dt<=0)return;carry+=dt;int n=(int)Math.Floor(carry*Rate);
            if(n>MaxSteps){n=MaxSteps;carry=0;}else carry-=n/Rate;
            float k=(Speed/Rate/Texel)*(Speed/Rate/Texel);
            for(int s=0;s<n;s++)
            {
                var h=Height;
                for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
                {
                    int i=x+Size*z;if(!Wet[i]){next[i]=0;continue;}float c=h[i];
                    // A dry or outside neighbour mirrors this texel: the wall reflects.
                    float l=x>0&&Wet[i-1]?h[i-1]:c,r=x<Size-1&&Wet[i+1]?h[i+1]:c,d=z>0&&Wet[i-Size]?h[i-Size]:c,u=z<Size-1&&Wet[i+Size]?h[i+Size]:c;
                    float v=(2*c-prev[i]+k*(l+r+d+u-4*c))*Damping;next[i]=v<-Limit?-Limit:v>Limit?Limit:v;
                }
                var old=prev;prev=Height;Height=next;next=old;Steps++;
            }
        }
        /// <summary>The surface's height at (world x, z) (bilinear; 0 off the grid or dry).</summary>
        public float HeightAt(float wx,float wz)
        {
            if(layout==null)return 0;float gx=(wx-Origin.X)/Texel-CornerX-.5f,gz=(wz-Origin.Z)/Texel-CornerZ-.5f;
            int x=(int)MathF.Floor(gx),z=(int)MathF.Floor(gz);if(x<0||z<0||x>=Size-1||z>=Size-1)return 0;float fx=gx-x,fz=gz-z;int i=x+Size*z;
            return (Height[i]*(1-fx)+Height[i+1]*fx)*(1-fz)+(Height[i+Size]*(1-fx)+Height[i+Size+1]*fx)*fz;
        }
        /// <summary>The field's energy (the sum of the squared heights; checks).</summary>
        public double Energy(){double e=0;foreach(float h in Height)e+=h*h;return e;}
    }

    /// <summary>
    /// What disturbs the Poolrooms' water (docs/BACKROOMS.md "The water's waves"): a body moving through it leaves a wake; a body
    /// dropping in, a shot striking it and a blast over it splash. Each returns how hard it splashed (0..1: none to the biggest)
    /// for the presentation's spray, or nothing (no water there). Pure.
    /// </summary>
    public static class NativePoolWaves
    {
        /// <summary>A wading body's wake: its push per second at speed (m of surface per m/s) and most, and its patch.</summary>
        public const float WakePush=.006f,WakeMost=.02f,WakeRadius=.42f;
        /// <summary>An entry: falling faster than this (m/s) through the surface splashes, harder with the speed up to Hard.</summary>
        public const float EntrySpeed=1.5f,EntryHard=8f,EntryPush=.13f,EntryRadius=.75f;
        /// <summary>A shot's splash and a blast's (its push and patch; a blast's grows with its radius, up to BlastMost m).</summary>
        public const float ShotPush=.035f,ShotRadius=.28f,BlastPush=.16f,BlastMost=2.4f;
        /// <summary>A body counts as in the water while its feet are under the surface and it reaches above it (a 1.8 m body).</summary>
        public const float BodyHeight=1.8f;

        /// <summary>Whether a body with its feet at `feet` stands in the water (its feet under the surface of a pool).</summary>
        public static bool InWater(PoolWaves w,Vector3 feet)=>w.Layout!=null&&PoolWaves.WaterAt(w.Layout,w.Origin,feet.X,feet.Z)&&feet.Y<w.Origin.Y+PoolroomsLayout.WaterY&&feet.Y>w.Origin.Y+PoolroomsLayout.WaterY-BodyHeight;
        /// <summary>A body at `feet` moving at `velocity` for `dt` s: a wake if it stands in the water and moves.</summary>
        public static void Wake(PoolWaves w,Vector3 feet,Vector3 velocity,float dt)
        {
            if(!InWater(w,feet))return;float speed=MathF.Sqrt(velocity.X*velocity.X+velocity.Z*velocity.Z);if(speed<.2f)return;
            w.Disturb(feet.X,feet.Z,WakeRadius,-Math.Min(WakeMost,WakePush*speed)*dt*PoolWaves.Rate*.25f);
        }
        /// <summary>A body whose feet went from `was` to `now` this frame at vertical speed `vy` (m/s): a splash if they crossed the
        /// surface downward fast enough over a pool. Returns its strength (0: none).</summary>
        public static float Entry(PoolWaves w,Vector3 was,Vector3 now,float vy)
        {
            float s=w.Origin.Y+PoolroomsLayout.WaterY;if(!(was.Y>=s&&now.Y<s)||vy>-EntrySpeed||w.Layout==null||!PoolWaves.WaterAt(w.Layout,w.Origin,now.X,now.Z))return 0;
            float k=Math.Clamp((-vy-EntrySpeed)/(EntryHard-EntrySpeed),0,1)*.75f+.25f;
            w.Disturb(now.X,now.Z,EntryRadius*(.8f+.4f*k),-EntryPush*k);return k;
        }
        /// <summary>A shot striking at `at` (on or under the surface of a pool): its splash's strength (0: not water).</summary>
        public static float Shot(PoolWaves w,Vector3 at)
        {
            if(w.Layout==null||!PoolWaves.WaterAt(w.Layout,w.Origin,at.X,at.Z)||at.Y>w.Origin.Y+PoolroomsLayout.WaterY+.35f)return 0;
            return w.Disturb(at.X,at.Z,ShotRadius,-ShotPush)?.35f:0;
        }
        /// <summary>A blast of `radius` m at `at` over or in a pool: its splash's strength (0: no water under it).</summary>
        public static float Blast(PoolWaves w,Vector3 at,float radius)
        {
            if(w.Layout==null||at.Y>w.Origin.Y+PoolroomsLayout.WaterY+radius)return 0;
            float r=Math.Clamp(radius*.6f,.6f,BlastMost);return w.Disturb(at.X,at.Z,r,-BlastPush)?1f:0;
        }
    }

    /// <summary>
    /// The spray a splash throws (docs/BACKROOMS.md "The water's waves"): a crown of drops thrown up and out from the splash,
    /// falling under gravity; a drop that falls back through the surface ends as a small ripple on the waves. At most
    /// <see cref="Most"/> drops (a new splash takes the oldest's places when full). Presentation only. Pure; its own seeded
    /// randomness.
    /// </summary>
    public sealed class PoolSpray
    {
        public const int Most=320;public const float Gravity=9.81f;
        /// <summary>A splash of strength 1: this many drops, thrown up at this speed (m/s) and out at this one, from this far round
        /// its centre (m); each drop this big (m) and its ripple when it lands (m of surface).</summary>
        public const int Drops=34;public const float Up=4.2f,Out=1.9f,Ring=.3f,Size=.045f,Landing=-.006f;
        public struct Drop{public Vector3 At,Velocity;public float Age,Size;public int Seed;}
        public readonly Drop[] Live=new Drop[Most];public int Count{get;private set;}
        private uint state=0x2545F491u;private int next;
        private float Next(){state^=state<<13;state^=state>>17;state^=state<<5;return (state&0xffffff)/16777216f;}
        /// <summary>A splash of strength `k` (0..1) at `at` on the surface: the stronger, the more drops, the higher and wider.</summary>
        public void Splash(Vector3 at,float k)
        {
            k=Math.Clamp(k,0,1);int n=(int)(4+(Drops-4)*k);
            for(int i=0;i<n;i++)
            {
                float a=Next()*MathF.PI*2,r=Ring*(.3f+.7f*Next())*(.5f+.5f*k),up=Up*(.35f+.65f*k)*(.55f+.45f*Next()),side=Out*(.3f+.7f*Next())*(.6f+.4f*k);
                var dir=new Vector3(MathF.Cos(a),0,MathF.Sin(a));
                var d=new Drop{At=at+dir*r+new Vector3(0,.02f,0),Velocity=dir*side+new Vector3(0,up,0),Age=0,Size=Size*(.5f+.8f*Next())*(.7f+.3f*k),Seed=next++};
                if(Count<Most)Live[Count++]=d;else Live[(int)(Next()*Most)%Most]=d;
            }
        }
        /// <summary>Advances the drops `dt` s; one falling back through `surface` (world y) ends, a ripple on `waves` if it lands over
        /// its water.</summary>
        public void Step(float dt,PoolWaves waves,float surface)
        {
            if(dt<=0)return;
            for(int i=0;i<Count;)
            {
                var d=Live[i];d.Velocity.Y-=Gravity*dt;d.At+=d.Velocity*dt;d.Age+=dt;
                if(d.At.Y<surface&&d.Velocity.Y<0||d.Age>3){if(waves!=null&&d.At.Y<surface)waves.Disturb(d.At.X,d.At.Z,.18f,Landing*(d.Size/Size));Live[i]=Live[--Count];continue;}
                Live[i]=d;i++;
            }
        }
        public void Clear()=>Count=0;
    }
}
