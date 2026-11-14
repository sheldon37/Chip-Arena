using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Attack warnings on the ground, shared by every boss and enemy (owner 2026-11-06: "better visual indicators of his attacks
    /// ... all bosses and enemies"; docs/TELEGRAPHS.md). A warning is a shape on the floor in its own frame (x its right, y its
    /// front, metres) with the attack's timing already evaluated: how far the fill has come (it is full on the hit), the lock-on
    /// in the last moments, the seconds since the hit (the impact flash), and whether the hazard is live now. Runtime
    /// NativeTelegraphPresentation.cs draws every warning of a frame in one mesh through Resources/NativeTelegraph.shader, where
    /// the shape, its fill, its animated edge, its pattern and its flash are computed per pixel. Presentation only: each warning
    /// is the attack's own geometry (the rules say where and when); nothing here is read by a rule.
    /// </summary>
    public static class NativeTelegraph
    {
        // ---- shapes (A, B, C as below) ----
        /// <summary>A filled disc: A its radius.</summary>
        public const byte Circle=0;
        /// <summary>A band between radii: A outer, B inner.</summary>
        public const byte Ring=1;
        /// <summary>A wedge ahead: A its radius, B its half angle (degrees), C an inner radius.</summary>
        public const byte Cone=2;
        /// <summary>A lane ahead: A its length forward, B its half width, C its length behind.</summary>
        public const byte Lane=3;
        /// <summary>A square: A its half size.</summary>
        public const byte Square=4;
        /// <summary>A travelling arc (a wave's front): A its radius now, B its half thickness, C its half angle (degrees).</summary>
        public const byte Wave=5;
        public const byte Shapes=6;
        // ---- how the fill comes in ----
        public const byte FromEdge=0,FromCentre=1,Along=2;
        // ---- looks: a family each (Resources/NativeTelegraph.shader keeps the same numbers) ----
        /// <summary>The default: hot red-orange, chevrons running with the fill.</summary>
        public const byte Danger=0;
        /// <summary>NOCTIEL: white on black, a dial of ticks and engraved rings.</summary>
        public const byte Fallen=1;
        /// <summary>NOCTIEL's one-hit kills: denser, a black core with white spokes, a harder pulse.</summary>
        public const byte FallenKill=2;
        /// <summary>A safe place (the Eclipse's pools): soft, breathing, inviting.</summary>
        public const byte Safe=3;
        /// <summary>Industrial bosses: amber hazard stripes (the Station, the Ironworks).</summary>
        public const byte Industrial=4;
        /// <summary>Heat: molten orange shimmer.</summary>
        public const byte Molten=5;
        /// <summary>THE HUM: a fluorescent buzz that flickers.</summary>
        public const byte Fluorescent=6;
        /// <summary>THE DEEP END: water light, caustics.</summary>
        public const byte Water=7;
        /// <summary>The Siren: magenta.</summary>
        public const byte Siren=8;
        public const byte Styles=9;
        /// <summary>How long before the hit the lock-on flash runs, and how long the impact flash after it lasts.</summary>
        public const float LockSeconds=.28f,FlashSeconds=.35f;

        public struct Spec
        {
            public byte Shape,Style,Fill;
            /// <summary>Where (world, on the floor) and which way its front faces (degrees, the game's yaw: 0 is +z, 90 is +x).</summary>
            public Vector3 At;public float Yaw;
            public float A,B,C;
            /// <summary>The fill 0..1 (1 on the hit); the lock-on 0..1 (the last LockSeconds); seconds since the hit (negative before);
            /// the hazard live now (0..1: a lane still hurting after its hit); how strong it draws (0..1).</summary>
            public float Fill01,Lock,After,Live,Strength;
            public int Seed;
        }

        // ---- timing ----
        /// <summary>The fill for a warning from start to hit at now: eased so it reads early and closes hard.</summary>
        public static float Progress(float start,float hit,float now){if(!(hit>start))return now>=hit?1:0;float u=Clamp01((now-start)/(hit-start));return u*(.55f+.45f*u);}
        public static float LockOf(float hit,float now){float t=hit-now;return t<0?0:1-Clamp01(t/LockSeconds);}
        /// <summary>A warning's timing at now: it shows from start until FlashSeconds after the hit (or the live window's end).</summary>
        public static bool Timed(ref Spec s,float start,float hit,float now,float live=0)
        {
            if(now<start||now>hit+Math.Max(FlashSeconds,live))return false;
            s.Fill01=Progress(start,hit,now);s.Lock=LockOf(hit,now);s.After=now-hit;s.Live=live>0&&now>=hit&&now<=hit+live?1-Clamp01((now-hit-live+.25f)/.25f):0;
            if(s.Strength<=0)s.Strength=1;return true;
        }

        // ---- geometry ----
        /// <summary>The margin past a shape the quad keeps for its glow and its impact ring.</summary>
        public const float Margin=1.6f;
        /// <summary>The shape's extent in its own frame (x across, y forward), without the margin.</summary>
        public static void Extent(in Spec s,out float minX,out float maxX,out float minY,out float maxY)
        {
            switch(s.Shape)
            {
                case Ring:case Circle:minX=minY=-s.A;maxX=maxY=s.A;return;
                case Square:minX=minY=-s.A;maxX=maxY=s.A;return;
                case Lane:minX=-s.B;maxX=s.B;minY=-s.C;maxY=s.A;return;
                case Cone:case Wave:
                {
                    // A wedge facing +y: across, its edge rays reach r sin(half); ahead, r; behind, its inner corners (or the
                    // whole circle once it is wider than a half turn).
                    bool wave=s.Shape==Wave;float r=wave?s.A+s.B:s.A,inner=wave?Math.Max(0,s.A-s.B):s.C,half=(wave?s.C:s.B)*(float)Math.PI/180;
                    if(half>=Math.PI/2){minX=minY=-r;maxX=maxY=r;return;}
                    maxX=r*(float)Math.Sin(half);minX=-maxX;maxY=r;minY=inner*(float)Math.Cos(half);return;
                }
                default:minX=minY=-1;maxX=maxY=1;return;
            }
        }
        /// <summary>The quad that covers the shape and its margin, on the floor (world): corners counter-clockwise from above.</summary>
        public static void Corners(in Spec s,out Vector3 a,out Vector3 b,out Vector3 c,out Vector3 d,out Vector2 la,out Vector2 lb,out Vector2 lc,out Vector2 ld)
        {
            Extent(s,out float x0,out float x1,out float y0,out float y1);x0-=Margin;y0-=Margin;x1+=Margin;y1+=Margin;
            la=new Vector2(x0,y0);lb=new Vector2(x1,y0);lc=new Vector2(x1,y1);ld=new Vector2(x0,y1);
            a=World(s,la);b=World(s,lb);c=World(s,lc);d=World(s,ld);
        }
        /// <summary>A point of the shape's frame in the world (on the floor).</summary>
        public static Vector3 World(in Spec s,Vector2 local)
        {
            float y=s.Yaw*(float)Math.PI/180,sn=(float)Math.Sin(y),cs=(float)Math.Cos(y);
            // Its front is (sin yaw, cos yaw) on the floor, its right (cos yaw, -sin yaw).
            return s.At+new Vector3(cs*local.X+sn*local.Y,0,-sn*local.X+cs*local.Y);
        }
        /// <summary>The signed distance from a point of the shape's frame to its edge (negative inside), as the shader has it.</summary>
        public static float Distance(in Spec s,Vector2 p)
        {
            switch(s.Shape)
            {
                case Circle:return p.Length()-s.A;
                case Ring:return Math.Abs(p.Length()-(s.A+s.B)*.5f)-(s.A-s.B)*.5f;
                case Square:return Box(p,new Vector2(s.A,s.A));
                case Lane:return Box(p-new Vector2(0,(s.A-s.C)*.5f),new Vector2(s.B,(s.A+s.C)*.5f));
                case Cone:{float d=Pie(p,s.B,s.A);return s.C>0?Math.Max(d,s.C-p.Length()):d;}
                case Wave:return Math.Max(Math.Abs(p.Length()-s.A)-s.B,Pie(p,s.C,1e4f));
                default:return p.Length()-1;
            }
        }
        private static float Box(Vector2 p,Vector2 h){var d=new Vector2(Math.Abs(p.X)-h.X,Math.Abs(p.Y)-h.Y);return new Vector2(Math.Max(d.X,0),Math.Max(d.Y,0)).Length()+Math.Min(Math.Max(d.X,d.Y),0);}
        // A wedge facing +y (Inigo Quilez's sdPie): half the angle in degrees, r its radius.
        private static float Pie(Vector2 p,float halfDegrees,float r)
        {
            float h=halfDegrees*(float)Math.PI/180;if(h>=Math.PI*.999f)return p.Length()-r;var c=new Vector2((float)Math.Sin(h),(float)Math.Cos(h));
            p.X=Math.Abs(p.X);float l=p.Length()-r;float m=(p-c*Math.Clamp(Vector2.Dot(p,c),0,r)).Length();return Math.Max(l,m*Math.Sign(c.Y*p.X-c.X*p.Y));
        }
        /// <summary>A point in the world (on the floor) in the shape's frame.</summary>
        public static Vector2 Local(in Spec s,Vector3 world)
        {
            float y=s.Yaw*(float)Math.PI/180,sn=(float)Math.Sin(y),cs=(float)Math.Cos(y);var d=world-s.At;
            return new Vector2(cs*d.X-sn*d.Z,sn*d.X+cs*d.Z);
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;

        // ---- the Siren's kit (a pilot's, or the horde Siren's through her possessed add; Runtime NativeSiren.cs) ----
        /// <summary>A Siren field's warning at now: an eruption filling to its burst (its whole radius, SirenArea at At + 1 s); Black
        /// Rain's radius while drops can land in it; a fire patch's radius and the 0.3 m the burn reaches past it, while it burns
        /// (softer: there can be many). False when the field has no warning now.</summary>
        public static bool SirenField(string kind,Vector3 at,float radius,float bornAt,float until,float now,out Spec s)
        {
            s=new Spec{Shape=Circle,Style=Siren,Fill=FromEdge,At=at,A=radius,Seed=(int)(bornAt*16)};
            switch(kind)
            {
                case "eruption":return Timed(ref s,bornAt,bornAt+EruptionBurst,now,.1f);
                case "rain":s.Strength=.75f;return Timed(ref s,bornAt,bornAt,now,until-bornAt);
                case "hellfire":case "trail":s.A=radius+.3f;s.Strength=.5f;return Timed(ref s,bornAt,bornAt,now,until-bornAt);
                default:return false;
            }
        }
        /// <summary>An eruption's cast to its burst (Runtime NativeSiren: Next = At + 1).</summary>
        public const float EruptionBurst=1;
    }

    /// <summary>A frame's warnings, gathered by any presenter and drawn once (allocation free once sized).</summary>
    public sealed class NativeTelegraphList
    {
        public readonly NativeTelegraph.Spec[] Items;public int Count;
        public NativeTelegraphList(int max){Items=new NativeTelegraph.Spec[max];}
        public void Clear(){Count=0;}
        public bool Add(in NativeTelegraph.Spec s){if(Count>=Items.Length||s.Strength<=0)return false;Items[Count++]=s;return true;}
    }
}
