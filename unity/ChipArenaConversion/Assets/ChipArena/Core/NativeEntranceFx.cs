using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Where a directed entrance's effects go (Runtime: the shared NativeVfxDraw renderer, mirrored into the Unity frame).
    /// Positions in the source frame, colours linear (glows may exceed 1 for the bloom).
    /// </summary>
    public interface INativeEntranceFx
    {
        /// <summary>A soft glow sprite: profile 0 mote, 1 core (softer, larger), 3 snow (pale); coverage 0 is purely additive.</summary>
        void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed);
        /// <summary>A streak with its head at `head`, trailing along -velocity by velocity x blur (at least minLength).</summary>
        void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed);
        /// <summary>A lit, eroding smoke puff (u its normalized age; it fades out below `floor`).</summary>
        void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor);
    }
    /// <summary>
    /// Presentation only: the directed entrances' effect emitters. Each is closed form: every particle is a pure function of
    /// (seed, index, time), born on a fixed schedule and moved analytically (ballistic with drag), so nothing is stepped or
    /// stored and an entrance shows the same effects at any age, frame rate or scrub. `quality` (the effects setting, 0..1)
    /// thins them: a particle is kept when its own hash is under the quality, so a low setting draws a sparse subset of the
    /// same particles. Every emitter has a fixed particle budget, so a frame never exceeds the renderer's capacity.
    /// No flat discs or rings on the floor (owner rule): ground effects are puffs and streaks hugging it.
    /// </summary>
    public static class NativeEntranceFx
    {
        public static Vector3 Hex(int hex,float gain=1){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b)*gain;}
        private static bool Kept(int seed,int i,float quality)=>NativeEntranceMath.Rand(seed^0x5bd1e995,i)<Math.Max(.08f,quality);
        // Position of a particle with initial velocity v under gravity g and linear drag k after a seconds.
        private static Vector3 Ballistic(Vector3 p,Vector3 v,float g,float k,float a,out Vector3 vel)
        {
            if(k<1e-4f){vel=v+new Vector3(0,-g*a,0);return p+v*a+new Vector3(0,-.5f*g*a*a,0);}
            float e=(float)Math.Exp(-k*a),f=(1-e)/k;
            // dv/dt = -k v - g y: v(a) = v e + (g/k)(e-1) y.
            vel=v*e+new Vector3(0,g/k*(e-1),0);
            return p+v*f+new Vector3(0,-g/k*(a-f),0);
        }
        /// <summary>
        /// A burst of streak sparks from `origin` at `born`: `count` sparks along `bias` (unit, or zero for all round) spread by
        /// `spread`, speeds in [speed*.45, speed], gravity, drag, bouncing once off the floor. Hot to cool colour over life.
        /// </summary>
        public static void Sparks(INativeEntranceFx fx,float t,float born,Vector3 origin,Vector3 bias,float spread,int count,float speed,float life,float gravity,float drag,Vector3 hot,Vector3 cool,float width,int seed,float quality,float floor)
        {
            float a0=t-born;if(a0<0||a0>life*1.3f)return;
            for(int i=0;i<count;i++)
            {
                if(!Kept(seed,i,quality))continue;float l=life*(.55f+.45f*NativeEntranceMath.Rand(seed,i*7+1)),a=a0-.02f*NativeEntranceMath.Rand(seed,i*7+2);if(a<0||a>l)continue;
                var d=NativeEntranceMath.Direction(seed,i);if(bias!=Vector3.Zero){d=Vector3.Normalize(bias+d*spread);}
                float s=speed*(.45f+.55f*NativeEntranceMath.Rand(seed,i*7+3));var p=Ballistic(origin,d*s,gravity,drag,a,out var v);
                if(p.Y<floor){p.Y=floor+(floor-p.Y)*.3f;v.Y=-v.Y*.3f;}
                float u=a/l;var c=Vector3.Lerp(hot,cool,NativeEntranceMath.Smooth(u*1.4f))*(1-u)*(1-u*.5f);
                fx.Streak(p,v,c,width,width*1.6f,.035f,.85f,u,seed+i);
            }
        }
        /// <summary>Glowing motes rising from a disc of `radius` around `centre` between `from` and `to`, `rate` per second.</summary>
        public static void Embers(INativeEntranceFx fx,float t,float from,float to,Vector3 centre,float radius,float rate,float rise,float life,Vector3 color,float size,int seed,float quality,float swirl=0)
        {
            int first=(int)Math.Floor((Math.Max(from,t-life)-from)*rate),last=(int)Math.Floor((Math.Min(t,to)-from)*rate);
            for(int k=Math.Max(0,first);k<=last;k++)
            {
                if(!Kept(seed,k,quality))continue;float born=from+k/rate,a=t-born,l=life*(.6f+.4f*NativeEntranceMath.Rand(seed,k*5));if(a<0||a>l)continue;float u=a/l;
                float ang=NativeEntranceMath.Rand(seed,k*5+1)*6.2831853f+swirl*a,r=radius*(float)Math.Sqrt(NativeEntranceMath.Rand(seed,k*5+2));
                var p=centre+new Vector3((float)Math.Cos(ang)*r,rise*a*(1-.35f*u),(float)Math.Sin(ang)*r)+new Vector3(.08f*(float)Math.Sin(a*5+k),0,.08f*(float)Math.Cos(a*4+k*1.3f));
                float flicker=.7f+.3f*(float)Math.Sin(a*31+k*2.1f);fx.Glow(p,color*(NativeEntranceMath.Smooth(u*6)*(1-u)*flicker),0,size*(1-.4f*u),0,u,seed+k);
            }
        }
        /// <summary>
        /// A crater of dust from `born` at `centre`: puffs thrown outward along the floor (ground-hugging, never a flat disc),
        /// slowing with drag, growing and thinning; a few lift higher.
        /// </summary>
        public static void GroundDust(INativeEntranceFx fx,float t,float born,Vector3 centre,int count,float startRadius,float speed,float life,Vector3 albedo,float opacity,float size,int seed,float quality,float floor)
        {
            float a0=t-born;if(a0<0||a0>life)return;
            for(int i=0;i<count;i++)
            {
                if(!Kept(seed,i,quality))continue;float a=a0-.04f*NativeEntranceMath.Rand(seed,i*5);if(a<0)continue;float l=life*(.7f+.3f*NativeEntranceMath.Rand(seed,i*5+1));if(a>l)continue;float u=a/l;
                float ang=(i+.5f*NativeEntranceMath.Rand(seed,i*5+2))/count*6.2831853f,s=speed*(.6f+.6f*NativeEntranceMath.Rand(seed,i*5+3)),k=3.2f;
                float r=startRadius+s*(1-(float)Math.Exp(-k*a))/k,lift=(.05f+.35f*NativeEntranceMath.Rand(seed,i*5+4)*NativeEntranceMath.Rand(seed,i*5+4))*(1-(float)Math.Exp(-2.5f*a))+.08f;
                var p=centre+new Vector3((float)Math.Cos(ang)*r,0,(float)Math.Sin(ang)*r);p.Y=floor+lift+size*.18f;
                fx.Smoke(p,albedo,opacity*NativeEntranceMath.Smooth(u*8)*(1-u*u),size*(.55f+.9f*NativeEntranceMath.Smooth(u*1.5f)),u,seed+i,floor-.05f);
            }
        }
        /// <summary>A jet of smoke puffs from `origin` along `dir` between `from` and `to` (a vent, a barrel, a door seam).</summary>
        public static void Jet(INativeEntranceFx fx,float t,float from,float to,Vector3 origin,Vector3 dir,float rate,float speed,float life,float size,Vector3 albedo,float opacity,int seed,float quality,float floor,float rise=.4f,float spread=.25f)
        {
            int first=(int)Math.Floor((Math.Max(from,t-life)-from)*rate),last=(int)Math.Floor((Math.Min(t,to)-from)*rate);
            for(int k=Math.Max(0,first);k<=last;k++)
            {
                if(!Kept(seed,k,quality))continue;float born=from+k/rate,a=t-born,l=life*(.7f+.3f*NativeEntranceMath.Rand(seed,k*3));if(a<0||a>l)continue;float u=a/l;
                var d=Vector3.Normalize(dir+NativeEntranceMath.Direction(seed,k)*spread);float drag=3.5f,f=(1-(float)Math.Exp(-drag*a))/drag;
                var p=origin+d*(speed*f)+new Vector3(0,rise*a*a*.5f+rise*a*.3f,0);
                fx.Smoke(p,albedo,opacity*NativeEntranceMath.Smooth(u*6)*(1-u),size*(.4f+1.2f*NativeEntranceMath.Smooth(u)),u,seed+k,floor);
            }
        }
        /// <summary>A jagged bolt from `a` to `b`: `segments` streak segments displaced by a seeded midpoint pattern, re-seeded
        /// every `flicker` s, with hot glows at the joints and a soft halo.</summary>
        public static void Bolt(INativeEntranceFx fx,float t,Vector3 a,Vector3 b,int segments,float jag,float flicker,Vector3 core,Vector3 halo,float width,int seed,float quality)
        {
            int phase=(int)Math.Floor(t/Math.Max(.01f,flicker));int s=seed*31+phase*977;var axis=b-a;float len=axis.Length();if(len<1e-3f)return;var dir=axis/len;
            var side=Vector3.Normalize(Vector3.Cross(dir,Math.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var side2=Vector3.Cross(dir,side);
            Vector3 prev=a;
            for(int i=1;i<=segments;i++)
            {
                float u=(float)i/segments,env=(float)Math.Sin(Math.PI*u);
                var p=i==segments?b:a+axis*u+(side*NativeEntranceMath.Signed(s,i*2)+side2*NativeEntranceMath.Signed(s,i*2+1))*(jag*len*env*.18f);
                fx.Streak(p,p-prev,core,width,0,1,.15f,0,s+i);if(i%2==0&&Kept(s,i,quality))fx.Glow(p,halo,0,width*9,0,0,s+i);
                // A fork off every third joint.
                if(i%3==1&&i<segments-1&&Kept(s,i+50,quality)){var fork=p+(side2*NativeEntranceMath.Signed(s,i+70)+dir*.4f+side*NativeEntranceMath.Signed(s,i+90))*(len*.12f);fx.Streak(fork,fork-p,core*.6f,width*.6f,0,1,.3f,0,s+i+200);}
                prev=p;
            }
        }
        /// <summary>A volumetric light shaft from `top` to `bottom` (stacked soft glows widening toward the bottom) with dust motes
        /// drifting inside it. `level` 0..1 scales it.</summary>
        public static void Shaft(INativeEntranceFx fx,float t,Vector3 top,Vector3 bottom,float topRadius,float bottomRadius,Vector3 color,float level,int slices,int motes,int seed,float quality)
        {
            if(level<=.002f)return;
            for(int i=0;i<slices;i++)
            {
                float u=(i+.5f)/slices;var p=Vector3.Lerp(top,bottom,u);float r=NativeEntranceMath.Lerp(topRadius,bottomRadius,u);
                fx.Glow(p,color*(level*(.35f+.65f*u)*(1-.55f*u*u)),0,r*2.6f,1,0,seed+i);
            }
            var axis=bottom-top;
            for(int i=0;i<motes;i++)
            {
                if(!Kept(seed,i+500,quality))continue;float u=(NativeEntranceMath.Rand(seed,i*4)+t*(.03f+.04f*NativeEntranceMath.Rand(seed,i*4+1)))%1f;
                float r=NativeEntranceMath.Lerp(topRadius,bottomRadius,u)*.85f*(float)Math.Sqrt(NativeEntranceMath.Rand(seed,i*4+2)),ang=NativeEntranceMath.Rand(seed,i*4+3)*6.283f+t*.4f;
                var p=top+axis*u+new Vector3((float)Math.Cos(ang)*r,0,(float)Math.Sin(ang)*r);float tw=.5f+.5f*(float)Math.Sin(t*3+i*1.7f);
                fx.Glow(p,color*(level*.35f*tw),0,.035f,0,0,seed+i);
            }
        }
        /// <summary>A four-point star glint: a core glow and two crossing streaks (screen-ish aligned by the world axes given).</summary>
        public static void Glint(INativeEntranceFx fx,Vector3 at,Vector3 color,float size,Vector3 axisA,Vector3 axisB,int seed)
        {
            if(size<=1e-3f)return;fx.Glow(at,color,0,size,1,0,seed);
            fx.Streak(at+axisA*size*1.1f,axisA*size*2.2f,color*1.4f,size*.08f,0,1,0,0,seed+1);
            fx.Streak(at+axisB*size*.8f,axisB*size*1.6f,color*1.2f,size*.07f,0,1,0,0,seed+2);
        }
        /// <summary>A trail through `count` positions (newest first) as tapering streak segments.</summary>
        public static void Ribbon(INativeEntranceFx fx,ReadOnlySpan<Vector3> points,int count,Vector3 head,Vector3 tail,float width,int seed)
        {
            for(int i=1;i<count;i++)
            {
                float u=(float)i/count;var a=points[i-1];var b=points[i];if((a-b).LengthSquared()<1e-8f)continue;
                fx.Streak(a,a-b,Vector3.Lerp(head,tail,u)*(1-u),width*(1-.7f*u),0,1,.2f,u,seed+i);
            }
        }
    }
}
