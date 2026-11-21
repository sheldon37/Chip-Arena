using System;

namespace ChipArena.UnityConversion
{
    // Portrait backdrops: the effect behind the pilot on the lobby and results cards, the home pilot card and the Locker
    // turntable (Runtime/NativePortraitBackUI.cs draws them). The player picks one in the Locker's Appearance tab; it is
    // saved as prefs.lobbyBack and sent like the idle pose (profile cosmetics -> host seat -> lobby and match seat frames).
    // Presentation only: nothing here touches the simulation, and the authority never reads it except to normalise.
    // Every key is at most MaxLength characters; an absent, empty or unknown key is the Tick Ring, the frame every card drew
    // before backdrops could be picked, so older clients and bots look exactly as they did.
    // The maths below is pure and deterministic (a function of time, seed and element index, no Random state), in units of
    // the portrait ring's radius about its centre (x right, y down), so the same backdrop reads the same on a small lobby
    // card and on the Locker's large stage. Reduced effects slows every motion to ReducedRate (Clock) and holds the glitch
    // still.
    public static class NativePortraitBacks
    {
        public const int TickRing=0,CleanGlow=1,Sunburst=2,HexField=3,Circuit=4,Equalizer=5,Warp=6,Embers=7,Orbitals=8,Glitch=9,CrownHalo=10,NeonDiamond=11,Count=12;
        public static readonly string[] Keys={"tick-ring","clean-glow","sunburst","hex-field","circuit","equalizer","warp","embers","orbitals","glitch","crown-halo","neon-diamond"};
        public static readonly string[] Labels={"Tick Ring","Clean Glow","Sunburst","Hex Field","Circuit","Equalizer","Warp","Embers","Orbitals","Glitch","Crown Halo","Neon Diamond"};
        // The Locker card's one line.
        public static readonly string[] Texts=
        {
            "The classic turning tick ring and orbiting spark.",
            "Just a soft glow in your accent colour.",
            "Slowly turning rays of light.",
            "Hex cells shimmering in waves around you.",
            "Circuit traces with pulses racing outward.",
            "A spectrum of bars pulsing to the beat.",
            "Light streaks rushing past at warp speed.",
            "Glowing sparks drifting up around you.",
            "Electrons circling on atom-style orbits.",
            "Scanlines, RGB split and signal glitches.",
            "A radiant halo and a crown of light.",
            "A neon diamond frame with racing sparks.",
        };
        // Icons.json ids (Lucide), for lists and compact views.
        public static readonly string[] Icons={"circle-dashed","circle-dot","sun","boxes","cpu","audio-lines","expand","flame","orbit","scan-line","eclipse","diamond"};
        public const string Default="tick-ring";
        // The longest key a seat frame may carry (NativeGameFrame.Valid), as for the idle pose.
        public const int MaxLength=32;
        public static int Index(string key){if(key!=null)for(int i=0;i<Keys.Length;i++)if(key==Keys[i])return i;return TickRing;}
        public static string Normalize(string key)=>Keys[Index(key)];
        // How far above the portrait ring's centre the head sits (ring radii) in each idle pose, for the Crown Halo.
        public static float HeadAbove(int pose)=>pose==NativeLobbyPoses.Sleep?.15f:pose==NativeLobbyPoses.Chair?.42f:.48f;

        // ---- Shared maths -------------------------------------------------------------------------------------------
        // A stable hash in [0,1) for element `i` of a style (`salt` separates its uses).
        public static float Hash(int i,int salt)
        {
            unchecked{uint h=(uint)i*0x9E3779B1u^(uint)salt*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}
        }
        // Reduced effects runs every backdrop's motion at a fifth of its speed.
        public const float ReducedRate=.2f;
        public static float Clock(float t,bool reduced)=>reduced?t*ReducedRate:t;
        public static float Frac(float x)=>x-(float)Math.Floor(x);
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private const float Tau=6.2831853f;
        // A neon line's alpha at `distance` from its centre line: an anti-aliased core `line` wide (aa its edge softness) and a
        // gaussian glow of radius `glow` at `glowAlpha` (glow 0: the core alone; line 0: the glow alone). All in the same units.
        public static float LineAlpha(float distance,float line,float aa,float glow,float glowAlpha)
        {
            float d=Math.Abs(distance),core=line>0?Clamp01((line*.5f-d)/Math.Max(1e-4f,aa)+.5f):0;
            float halo=glow>0?glowAlpha*(float)Math.Exp(-(d*d)/(glow*glow)):0;return Math.Max(core,halo);
        }

        // ---- Sunburst -----------------------------------------------------------------------------------------------
        // Twelve rays, long and short in turn, so the pattern repeats every SunPeriod degrees; SunPhases baked turns of the
        // pattern cover one period and the drawer crossfades the two nearest, so the rays turn smoothly at SunSpeed.
        public const int SunRays=12,SunPhases=16;public const float SunSpeed=4f,SunPeriod=720f/SunRays;
        public static void SunPhase(float degrees,out int a,out int b,out float blend)
        {
            float x=Frac(degrees/SunPeriod)*SunPhases;a=(int)Math.Floor(x);blend=x-a;a%=SunPhases;if(a<0)a+=SunPhases;b=(a+1)%SunPhases;
        }
        // The baked ray pattern at (x, y) in [-1, 1] (y down; the texture's circle), turned by phase/SunPhases of a period.
        public static float SunTexel(float x,float y,int phase)
        {
            float r=(float)Math.Sqrt(x*x+y*y);if(r>=1)return 0;
            float turn=phase*(SunPeriod/SunPhases)*(Tau/360f),theta=(float)Math.Atan2(x,-y)-turn,half=SunRays/2;
            float c=(float)Math.Cos(half*theta),longRay=c>0?(float)Math.Pow(c,7):0,shortRay=c<0?(float)Math.Pow(-c,9):0;
            float inner=Smooth((r-.1f)/.2f),reachLong=1-Smooth((r-.32f)/.66f),reachShort=1-Smooth((r-.24f)/.46f);
            return Clamp01(inner*(longRay*reachLong+.7f*shortRay*reachShort));
        }

        // ---- Hex field ----------------------------------------------------------------------------------------------
        // Flat-top hex cells of circumradius 1 on their lattice: column c, row r (odd columns half a row lower).
        public static void HexCentre(int col,int row,out float x,out float y){x=col*1.5f;y=(row+((col&1)!=0?.5f:0))*1.7320508f;}
        // A cell's shimmer (0..1) at `distance` from the pilot (ring radii) and time t: an outward wave every 2.5 s, plus
        // the cell's own slow twinkle.
        public static float HexShimmer(int col,int row,float distance,float t)
        {
            float w=(float)Math.Cos(Tau*(distance*.6f-t*.4f)),wave=w>0?(float)Math.Pow(w,10):0;
            float s=(float)Math.Sin(t*1.15f+Hash(col*131+row*17,7)*Tau),twinkle=s>0?(float)Math.Pow(s,16):0;
            return Math.Min(1,wave*.9f+twinkle*.75f);
        }
        // The baked cell at (x, y) in [-1, 1]: a flat-top hexagon of circumradius .86 as a neon outline (`line` wide, with a
        // soft glow) or, `filled`, a soft lit cell brighter at its rim.
        public static float HexTexel(float x,float y,float line,bool filled)
        {
            float d=HexDistance(x,y,.86f*.8660254f);
            if(!filled)return LineAlpha(d,line,line*.5f,line*2.2f,.35f);
            return d>0?Clamp01(1-d/.05f):.3f+.7f*Smooth(1+d/.28f);
        }
        // Signed distance to a flat-top hexagon of inradius `apothem` (edges at the top and bottom, corners left and right):
        // the farthest of its edge planes (exact along the edges, a little short of the true distance past a corner).
        public static float HexDistance(float x,float y,float apothem)
        {
            x=Math.Abs(x);y=Math.Abs(y);return Math.Max(y-apothem,.8660254f*x+.5f*y-apothem);
        }

        // ---- Circuit ------------------------------------------------------------------------------------------------
        // Right-half traces (x right, y down, ring radii), each a run of horizontal and vertical legs from the body out to
        // a pad; the left half mirrors them, so trace i>=TraceHalf is trace i-TraceHalf with x negated.
        private static readonly float[][] TraceLegs=
        {
            new[]{.42f,-.62f, .74f,-.62f, .74f,-.94f, 1.16f,-.94f},
            new[]{.56f,-.24f, 1.02f,-.24f, 1.02f,-.52f, 1.5f,-.52f},
            new[]{.6f,.12f, 1.38f,.12f},
            new[]{.56f,.46f, .92f,.46f, .92f,.8f, 1.42f,.8f},
            new[]{.32f,.84f, .32f,1.16f, .78f,1.16f},
        };
        public const int TraceHalf=5,Traces=TraceHalf*2;
        public static int TracePoints(int trace)=>TraceLegs[trace%TraceHalf].Length/2;
        public static void TracePoint(int trace,int k,out float x,out float y){var p=TraceLegs[trace%TraceHalf];x=p[k*2]*(trace>=TraceHalf?-1:1);y=p[k*2+1];}
        public static float TraceLength(int trace)
        {
            var p=TraceLegs[trace%TraceHalf];float n=0;for(int k=2;k<p.Length;k+=2)n+=Math.Abs(p[k]-p[k-2])+Math.Abs(p[k+1]-p[k-1]);return n;
        }
        // The point a fraction `s` (0..1) of the way along a trace.
        public static void TraceAt(int trace,float s,out float x,out float y)
        {
            var p=TraceLegs[trace%TraceHalf];float sign=trace>=TraceHalf?-1:1,left=Clamp01(s)*TraceLength(trace);
            for(int k=2;k<p.Length;k+=2)
            {
                float len=Math.Abs(p[k]-p[k-2])+Math.Abs(p[k+1]-p[k-1]);
                if(left<=len||k==p.Length-2){float f=len>0?Clamp01(left/len):1;x=(p[k-2]+(p[k]-p[k-2])*f)*sign;y=p[k-1]+(p[k+1]-p[k-1])*f;return;}
                left-=len;
            }
            x=p[0]*sign;y=p[1];
        }
        // A trace's pulse at time t: how far along it the head is (0..1 travelling; above 1 it has arrived and its pad
        // flashes until PulseRest). Each trace keeps its own period and offset.
        public const float PulseRest=1.45f;
        public static float TracePulse(int trace,float t){float period=2.1f+1.3f*Hash(trace,61);return Frac(t/period+Hash(trace,67))*PulseRest;}

        // ---- Equalizer ----------------------------------------------------------------------------------------------
        // Bar `bar` of `count` (mirrored about the middle bar): its level 0..1 at time t. A kick at EqBeat beats a second (a
        // quick rise over the first 8% of the beat, then a decay) drives the bars just outside the pilot hardest; every bar
        // also wobbles on its own two sines. Continuous in t.
        public const float EqBeat=1.9f;
        public static float EqLevel(int bar,int count,float t)
        {
            int half=(count-1)/2,id=Math.Abs(bar-half);float x=half>0?id/(float)half:0;
            float beat=Frac(t*EqBeat),kick=Smooth(beat/.08f)*(float)Math.Exp(-beat*5.5f),shoulder=(float)Math.Exp(-(x-.45f)*(x-.45f)/.07f);
            float a=.5f+.5f*(float)Math.Sin(t*(2.7f+Hash(id,11)*2.6f)+Hash(id,13)*Tau),b=.5f+.5f*(float)Math.Sin(t*(4.9f+Hash(id,17)*3.4f)+Hash(id,19)*Tau);
            float envelope=.3f+.7f*(1-Smooth((x-.55f)/.45f));
            return Clamp01(envelope*(.16f+.42f*a*b+.12f*a)+.42f*kick*shoulder);
        }

        // ---- Warp ---------------------------------------------------------------------------------------------------
        // Streaks along fixed directions (whole steps of 360/StreakSteps degrees, clockwise from up), so each draws with its
        // own baked streak (StreakAngles over half a turn: a streak is symmetric) and never rotates.
        public const int Streaks=28,StreakSteps=72,StreakAngles=StreakSteps/2;
        public static int StreakDirection(int i){int d=(int)Math.Round((i+Hash(i,3)*.7f-.35f)*StreakSteps/(float)Streaks);return ((d%StreakSteps)+StreakSteps)%StreakSteps;}
        // Streak i's progress (0 just outside the pilot .. 1 at the stage edge) at time t, and its alpha there.
        public static float StreakProgress(int i,float t){float speed=.32f+.3f*Hash(i,5);return Frac(t*speed+Hash(i,9));}
        public static float StreakAlpha(float f)=>Smooth(f/.2f)*(1-Smooth((f-.72f)/.28f));
        // The baked streak at (x, y) in [-1, 1] for direction `angle` (of StreakAngles): a line through the centre .94 long
        // each way, `thick` at its middle and tapering to its ends, with a soft edge.
        public static float StreakTexel(float x,float y,int angle,float thick)
        {
            float a=angle*(180f/StreakAngles)*(Tau/360f),dx=(float)Math.Sin(a),dy=-(float)Math.Cos(a);
            float along=Math.Abs(x*dx+y*dy),across=Math.Abs(x*dy-y*dx);if(along>=.94f)return 0;
            float u=along/.94f,w=thick*(1-u*u);return Clamp01((w-across)/Math.Max(.02f,thick*.6f)+.5f)*(1-u*u*u);
        }

        // ---- Embers -------------------------------------------------------------------------------------------------
        // Ember i at time t: its x across the spread (-1..1, swaying), how far it has risen (0 at the floor .. 1 at the top),
        // its size factor and alpha (fading in at the floor, flickering, fading out near the top).
        public const int EmberCount=22;
        public static void Ember(int i,float t,out float x,out float rise,out float size,out float alpha)
        {
            float period=3.4f+2.6f*Hash(i,1);rise=Frac(t/period+Hash(i,2));
            x=Hash(i,3)*2-1+(float)Math.Sin(t*(1.1f+Hash(i,4))+i*1.7f)*.07f*(1+rise);
            size=(.55f+.45f*Hash(i,5))*(1-.45f*rise);
            float flicker=.78f+.22f*(float)Math.Sin(t*(8.5f+4*Hash(i,6))+i*2.3f);
            alpha=Smooth(rise/.12f)*(1-Smooth((rise-.55f)/.45f))*flicker;
        }

        // ---- Orbitals -----------------------------------------------------------------------------------------------
        // Three orbits (an ellipse OrbitA by OrbitB ring radii, tilted in the screen plane by OrbitTilt degrees) with two
        // electrons each, half an orbit apart.
        public const float OrbitA=1.22f,OrbitB=.36f;public static readonly float[] OrbitTilt={0f,60f,-60f};
        private static readonly float[] OrbitSpeed={.55f,-.42f,.36f};
        public static float ElectronAngle(int orbit,int k,float t)=>t*OrbitSpeed[orbit]*Tau*.35f+k*(Tau/2)+orbit*2.1f;
        public static float OrbitDirection(int orbit)=>OrbitSpeed[orbit]<0?-1:1;
        public static void Electron(int orbit,int k,float t,out float x,out float y,out float depth)
        {
            float theta=ElectronAngle(orbit,k,t);OrbitPoint(orbit,theta,out x,out y);depth=(float)Math.Sin(theta);
        }
        public static void OrbitPoint(int orbit,float theta,out float x,out float y)
        {
            float ex=OrbitA*(float)Math.Cos(theta),ey=OrbitB*(float)Math.Sin(theta),a=OrbitTilt[orbit]*(Tau/360f),c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            x=ex*c-ey*s;y=ex*s+ey*c;
        }
        // Half the height and width an orbit spans (ring radii).
        public static float OrbitHalfHeight(int orbit){float a=OrbitTilt[orbit]*(Tau/360f),s=(float)Math.Sin(a),c=(float)Math.Cos(a);return (float)Math.Sqrt(OrbitA*OrbitA*s*s+OrbitB*OrbitB*c*c);}
        public static float OrbitHalfWidth(int orbit){float a=OrbitTilt[orbit]*(Tau/360f),s=(float)Math.Sin(a),c=(float)Math.Cos(a);return (float)Math.Sqrt(OrbitA*OrbitA*c*c+OrbitB*OrbitB*s*s);}
        // The baked ring at (x, y) in [-1, 1]: an ellipse whose long half-axis is .9 and short one .9*aspect, tilted by
        // `tilt` degrees (clockwise, y down), drawn as a neon line `line` wide with a `glow` halo.
        public static float EllipseTexel(float x,float y,float aspect,float tilt,float line,float glow,float glowAlpha)
        {
            float a=tilt*(Tau/360f),c=(float)Math.Cos(a),s=(float)Math.Sin(a),u=x*c+y*s,v=-x*s+y*c,ra=.9f,rb=.9f*aspect;
            float k=(float)Math.Sqrt(u*u/(ra*ra)+v*v/(rb*rb));if(k<1e-5f)return 0;
            // First-order distance to the curve: the implicit function over its gradient's length.
            float gx=u/(ra*ra*k),gy=v/(rb*rb*k),grad=(float)Math.Sqrt(gx*gx+gy*gy);
            return LineAlpha((k-1)/Math.Max(1e-5f,grad),line,line*.5f,glow,glowAlpha);
        }

        // ---- Glitch -------------------------------------------------------------------------------------------------
        // Glitch bands come in bursts (GlitchBurst of every GlitchCycle seconds), each band jumping to a new place GlitchRate
        // times a second; reduced effects holds every band still and faint (no jumps or flashes). Band i at time t: its centre
        // y and height (in the panel's half-height), its sideways shift (half-widths) and alpha.
        public const int GlitchBands=4;public const float GlitchRate=8,GlitchCycle=2.8f,GlitchBurst=.32f;
        public static void GlitchBand(int i,float t,bool reduced,out float y,out float h,out float dx,out float alpha)
        {
            int step=reduced?0:(int)Math.Floor(t*GlitchRate);bool burst=reduced||Frac(t/GlitchCycle+i*.07f)<GlitchBurst;
            y=Hash(i*977+step,31)*1.8f-.9f;h=.04f+.1f*Hash(i*613+step,37);dx=(Hash(i*421+step,41)*2-1)*.16f;
            alpha=reduced?.22f:burst&&Hash(i*311+step,43)<.7f?.45f+.4f*Hash(i+step*7,47):0;
        }
        // The scanlines' downward scroll at time t, in periods (0..1).
        public static float ScanScroll(float t,bool reduced)=>Frac(Clock(t,reduced)*.6f);

        // ---- Crown halo ---------------------------------------------------------------------------------------------
        // The crown's rays fan over the upper half behind the head: ray k of HaloRays points HaloFan*(k-centre) degrees from
        // up (a whole streak step, so it draws with a baked streak), with a length factor and alpha that breathe in turn.
        public const int HaloRays=9;public const float HaloFan=20;
        public static int HaloRayDirection(int k){int d=(int)Math.Round((k-(HaloRays-1)/2f)*HaloFan/(360f/StreakSteps));return ((d%StreakSteps)+StreakSteps)%StreakSteps;}
        public static void HaloRay(int k,float t,out float length,out float alpha)
        {
            float side=Math.Abs(k-(HaloRays-1)/2f)/((HaloRays-1)/2f),beat=.5f+.5f*(float)Math.Sin(t*1.6f-k*.7f);
            length=(1-.45f*side)*(.82f+.18f*beat);alpha=(1-.5f*side)*(.55f+.45f*beat);
        }
        // Twinkle i of the halo's sparkles: brightness 0..1 at time t (a short glint every few seconds).
        public static float Twinkle(int i,float t){float s=(float)Math.Sin(t*(1.3f+.5f*Hash(i,71))+Hash(i,73)*Tau);return s>0?(float)Math.Pow(s,12):0;}

        // ---- Neon diamond -------------------------------------------------------------------------------------------
        // The point a fraction s (wrapping) clockwise from the top vertex round the unit diamond |x|+|y|=1.
        public static void DiamondPoint(float s,out float x,out float y)
        {
            s=Frac(s)*4;int side=(int)s;float f=s-side;
            switch(side){case 0:x=f;y=-1+f;break;case 1:x=1-f;y=f;break;case 2:x=-f;y=1-f;break;default:x=-1+f;y=-f;break;}
        }
        // Spark k (two, going opposite ways round) at time t: its place on the diamond (0..1 from the top vertex).
        public static float DiamondSpark(int k,float t){float s=Frac(t*.16f+k*.5f);return k%2==0?s:1-s;}
        // The baked diamond at (x, y) in [-1, 1]: |x|+|y|=.9 as a neon line `line` wide with a `glow` halo.
        public static float DiamondTexel(float x,float y,float line,float glow,float glowAlpha)=>LineAlpha((Math.Abs(x)+Math.Abs(y)-.9f)*.70710678f,line,line*.5f,glow,glowAlpha);
        // The baked circle at (x, y) in [-1, 1]: radius .9 as a neon line with a halo.
        public static float RingTexel(float x,float y,float line,float glow,float glowAlpha)=>LineAlpha((float)Math.Sqrt(x*x+y*y)-.9f,line,line*.5f,glow,glowAlpha);
    }
}
