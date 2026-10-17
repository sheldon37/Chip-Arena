using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The projectile ceiling field's hit holograms (client/ceiling-field.ts CeilingFieldVisuals at 05950ea) as pure rules.
    /// Each ceiling-hit event lays a 7 x 7 m additive plane, facing down and up, 2.5 cm under the hit, whose fragment shader
    /// draws a ring growing at 4.5 m/s from .2 m, a half-metre grid revealed inside it with drifting scanlines, and a flash at
    /// the centre, all in the event's colour (0x75ddff when it has none) and fading out over 1.05 s. At most 24 show: a new
    /// hit drops the oldest. Ages run on the presentation clock (the source's state.time), from the moment the event is
    /// first seen; events older than .4 s draw nothing, and a hologram whose age is negative (the clock went back) goes.
    /// The plane coordinates p are the shader's (uv - .5) * 7: source world x and -z from the hit (Unity x is mirrored).
    /// Nothing here is read by the simulation or the network frame.
    /// </summary>
    public sealed class NativeCeilingField
    {
        public const int Cap=24,DefaultColor=0x75ddff;
        /// <summary>PlaneGeometry(7, 7); the plane sits .025 m under the event, which in the source is always on the field
        /// plane at CEILING_FIELD.height (shared/ceiling-field.ts).</summary>
        public const float Size=7,Life=1.05f,FieldHeight=24,Drop=.025f,Fresh=.4f;
        public struct Hit{public float X,Z,Born;public int Color;}
        private readonly Hit[] hits=new Hit[Cap];
        public int Count{get;private set;}
        public Hit this[int i]=>hits[i];
        public void Clear()=>Count=0;
        /// <summary>The colour a ceiling-hit event draws in: its own, else the source default.</summary>
        public static int ColorOf(int eventColor)=>eventColor!=0?eventColor:DefaultColor;
        /// <summary>The source skips an event when state.time - event.time &gt; .4.</summary>
        public static bool FreshEvent(float now,float eventTime)=>!(now-eventTime>Fresh);
        /// <summary>event(): at 24 holograms the oldest goes, then the new one is added (born = the clock now).</summary>
        public void Add(float x,float z,float born,int color)
        {
            if(Count>=Cap){for(int i=1;i<Count;i++)hits[i-1]=hits[i];Count--;}
            hits[Count++]=new Hit{X=x,Z=z,Born=born,Color=color};
        }
        /// <summary>update(time): holograms older than Life, or born after <paramref name="now"/>, are removed.</summary>
        public void Update(float now)
        {
            int kept=0;
            for(int i=0;i<Count;i++){float age=now-hits[i].Born;if(age<0||age>Life)continue;hits[kept++]=hits[i];}
            Count=kept;
        }

        // ---- the fragment shader (Resources/NativeCeilingField.shader is the same maths in CG) ------------------------
        public static double Radius(double age)=>.2+age*4.5;
        private static double Smooth(double e0,double e1,double x){double t=Math.Max(0,Math.Min(1,(x-e0)/(e1-e0)));return t*t*(3-2*t);}
        private static double Fract(double x)=>x-Math.Floor(x);
        /// <summary>
        /// The shader's alpha at plane point p (metres) and age: (grid * reveal * .36 * scan + ring * .85 + flash) * fade.
        /// The ring is exp(-((d - radius) / .09)^2), written as a square (GLSL pow of a negative base is undefined; the
        /// compilers fold pow(x, 2.) to x * x).
        /// </summary>
        public static double Alpha(double px,double py,double age)
        {
            double d=Math.Sqrt(px*px+py*py),radius=Radius(age),k=(d-radius)/.09,ring=Math.Exp(-k*k);
            double cx=Math.Abs(Fract(px*2)-.5),cy=Math.Abs(Fract(py*2)-.5),grid=1-Smooth(.015,.06,Math.Min(cx,cy));
            double reveal=(1-Smooth(radius-.1,radius+.3,d))*Math.Exp(-d*.65);
            double fade=Math.Pow(Math.Max(0,1-age/1.05),1.5),flash=Math.Exp(-age*18)*Math.Exp(-d*3),scan=.65+.35*Math.Sin(px*16+age*19);
            return (grid*reveal*.36*scan+ring*.85+flash)*fade;
        }
        /// <summary>
        /// What reaches the screen, sRGB 0..1: the ShaderMaterial has no output colour conversion, so the screen gets the tint
        /// uniform's linear value (Color(hex)) unencoded, times the fragment alpha clamped to [0, 1], added (AdditiveBlending).
        /// </summary>
        public static void Display(int color,double alpha,out double r,out double g,out double b)
        {NativeBlastSim.Linear(color,out r,out g,out b);double a=Math.Max(0,Math.Min(1,alpha));r*=a;g*=a;b*=a;}
        /// <summary>Plane corner <paramref name="i"/> (0..3) in source plane coordinates p (metres from the hit).</summary>
        public static void Corner(int i,out float px,out float py){float h=Size*.5f;px=(i==1||i==2)?h:-h;py=i>=2?h:-h;}
    }
}
