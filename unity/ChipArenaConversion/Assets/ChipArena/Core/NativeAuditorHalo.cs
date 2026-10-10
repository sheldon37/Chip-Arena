using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>Halo mesh data in the source frame (+x the rig's left); the Runtime mirrors x once (NativeAuditorHaloDraw).</summary>
    public sealed class HaloMesh
    {
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
        /// <summary>TEXCOORD0 (kind, along/corner x, index, side/corner y) and TEXCOORD1 (piece parameters), as Resources/NativeAuditorHalo.shader reads them.</summary>
        public readonly List<Vector4> Data=new List<Vector4>(),Extra=new List<Vector4>();public readonly List<int> Indices=new List<int>();
        public void Clear(){Positions.Clear();Normals.Clear();Data.Clear();Extra.Clear();Indices.Clear();}
        public int Add(Vector3 p,Vector3 n,Vector4 data,Vector4 extra){Positions.Add(p);Normals.Add(n);Data.Add(data);Extra.Add(extra);return Positions.Count-1;}
        public void Quad(int a,int b,int c,int d){Indices.Add(a);Indices.Add(b);Indices.Add(c);Indices.Add(a);Indices.Add(c);Indices.Add(d);}
    }

    /// <summary>One Auditor halo at one moment (source frame; the halo group's own space unless noted). Filled in place, so a frame allocates nothing.</summary>
    public sealed class AuditorHaloFrame
    {
        /// <summary>The group in the head cosmetic socket (auditor-halo.ts: y .46 with the bob, the slow spin, the -.12 tilt, .72 scale).</summary>
        public Vector3 GroupPosition;public Quaternion GroupRotation;public float GroupScale;
        /// <summary>The arcs' extra turn about the group's up axis (rad) and brightness.</summary>
        public readonly float[] Spin=new float[NativeAuditorHalo.Arcs],Pulse=new float[NativeAuditorHalo.Arcs];
        /// <summary>The crackle across each gap: arc i's end to the next arc's start, its strength (0 none) and jitter seed.</summary>
        public readonly Vector3[] BoltFrom=new Vector3[NativeAuditorHalo.Arcs],BoltTo=new Vector3[NativeAuditorHalo.Arcs];public readonly float[] Bolt=new float[NativeAuditorHalo.Arcs],BoltSeed=new float[NativeAuditorHalo.Arcs];
        /// <summary>The black flames: base on the ring, height and half width (group units; the shader narrows a flame seen end-on), sway phase, rim strength; Order draws them far to near.</summary>
        public readonly Vector3[] FlameBase=new Vector3[NativeAuditorHalo.Flames];public readonly float[] FlameHeight=new float[NativeAuditorHalo.Flames],FlameWidth=new float[NativeAuditorHalo.Flames],FlamePhase=new float[NativeAuditorHalo.Flames],FlameRim=new float[NativeAuditorHalo.Flames];
        public readonly int[] Order=new int[NativeAuditorHalo.Flames];
        /// <summary>The reduced halo's flat flames: animateCosmetics' scale (x, y) and in-plane tilt for each.</summary>
        public readonly Vector2[] FinScale=new Vector2[NativeAuditorHalo.Flames];public readonly float[] FinTilt=new float[NativeAuditorHalo.Flames];
        /// <summary>Rising embers (group space) and their opacity (0 hidden).</summary>
        public readonly Vector3[] Ember=new Vector3[NativeAuditorHalo.Embers];public readonly float[] EmberAlpha=new float[NativeAuditorHalo.Embers];
        /// <summary>The soft red haze and the halo light's strength (0..1), and the wrapped clock the shader animates its noise on.</summary>
        public float Glow,Light,Clock;public bool Simple;
    }

    /// <summary>
    /// The shader inputs for one halo draw in Unity's frame (x mirrored, the arc turn negated), as Resources/NativeAuditorHalo.shader
    /// declares them: _HaloA (clock, opacity, haze, light), _HaloSpin, _HaloPulse, _HaloBolts[6], _HaloFlames[24], _HaloFins[12], _HaloEmbers[8].
    /// </summary>
    public sealed class AuditorHaloUniforms
    {
        public Vector4 A,Spin,Pulse;
        public readonly Vector4[] Bolts=new Vector4[NativeAuditorHalo.Arcs*2],Flames=new Vector4[NativeAuditorHalo.Flames*2],Fins=new Vector4[NativeAuditorHalo.Flames],Embers=new Vector4[NativeAuditorHalo.Embers];
    }

    /// <summary>
    /// The Auditor halo (DEV hat): one presentation for every place a pilot wears it (the arena for host, guests and bots, the
    /// killcam, the intro rig, the chip draft stage, corpses and death scenes, and the menu portraits). It keeps the source's
    /// identity (auditor-halo.ts: three broken red arcs .32 from the centre on the head socket, raised .46 and tilted -.12,
    /// bobbing and slowly turning, with twelve black flames on the ring) and its group motion exactly (NativePortraitRules.HaloGroup),
    /// and upgrades the look: smooth tapered arcs with a white-hot core and soft additive glow shells, the arcs turning against
    /// the flames with their own breathing so the gaps open and close, electric crackle across the gaps (always when a gap is
    /// narrow, otherwise in short random bursts), slim black horn flames drawn by the shader (tall and short ones, hooking
    /// outward as seen and narrowing end-on like the source's flat flames, so the arcs show between them; dark core, thin
    /// ember-red rim, tongues licking upward, flickering heights), rising embers, a faint red haze and a red light on the
    /// wearer's head and shoulders.
    /// Reduced effects keep the arcs and the inner glow and draw the source's flat black flames with animateCosmetics' flicker.
    /// Time-dependent state is computed here, on the presentation clock; the shader adds only noise detail.
    /// </summary>
    public static class NativeAuditorHalo
    {
        public const string Hat="auditor-halo";
        public const int Arcs=3,Flames=12,Embers=8;
        // Pieces (TEXCOORD0.x): the shader switches on these.
        public const float KindArc=0,KindShell=1,KindBolt=2,KindEmber=3,KindHaze=4,KindFlame=5,KindFin=6;
        // ---- Geometry (group space, before the group's .72 scale) ----------------------------------------------------------
        /// <summary>The source's arc radius, span (.57 pi), start angles (i * 2.1) and the raised middle arc.</summary>
        public const float Radius=NativePortraitRules.HaloArcRadius,Span=NativePortraitRules.HaloArc,Step=2.1f,Raised=.025f;
        /// <summary>The core tube (thicker than the source's .019 so it reads at arena distance) and the two glow shells' radii.</summary>
        public const float Tube=.022f,InnerShell=.052f,OuterShell=.095f;
        public const int ArcSegments=64,TubeSides=10,ShellSegments=40,ShellSides=8,BoltSegments=10;
        /// <summary>The flames stand on the source's .30 ring (twelve, pi/6 apart); the haze sprite's size.</summary>
        public const float FlameRing=.30f,HazeSize=.95f;
        /// <summary>
        /// Slim horns like the source outline: a big flame's half width and height, the small ones' share of each (alternating,
        /// each flame a little different so the crown has no even rhythm), and the flame quad's reach in half widths (room for
        /// the shader's hook, sway and tongues).
        /// </summary>
        public const float FlameHalfWidth=.095f,FlameTall=.335f,SmallWidth=.8f,SmallHeight=.55f,FlameQuad=1.6f;
        public const float EmberSize=.022f;
        // ---- Motion -------------------------------------------------------------------------------------------------------
        /// <summary>The arcs turn at -.31 rad/s against the group's +.18 (so they drift backwards while the flames go round) and each breathes ±.085 rad.</summary>
        public const float Counter=-.31f,Wobble=.085f;
        private static readonly float[] WobbleRate={.71f,.53f,.89f};
        /// <summary>A gap narrower than this always crackles; otherwise a gap bursts in about 12% of sixth-second windows.</summary>
        public const float CrackleGap=.22f,BurstChance=.12f;
        public const float EmberRise=.55f;
        /// <summary>The shader's noise clock wraps here (float precision for long sessions); the pop is in noise only.</summary>
        public const float ClockWrap=512;
        // ---- Colours (sRGB; the Runtime converts for the colour space, which the project pins to Linear) -------------------
        public const int Deep=0x4a0010,Red=0xff1635,Hot=0xff5a4c,White=0xfff1ec,GlowColor=0xff1735,BoltColor=0xffc8d6,EmberColor=0xff5a24,Soot=0x080007,Inner=0x22030f,Rim=0xff2a1a,LightColor=0xff2a3a;

        // Arc i's centre line at s (0..1 along it) before its turn: auditor-halo.ts TorusGeometry(.32, ..., .57 pi) at
        // rotation (pi/2, 0, i*2.1) puts the point at ring angle i*2.1 + s*span: (R cos, y, R sin) in the group.
        public static float Angle(int arc,float s)=>arc*Step+s*Span;
        public static Vector3 Centre(int arc,float s){float a=Angle(arc,s);return new Vector3(Radius*(float)Math.Cos(a),arc==1?Raised:0,Radius*(float)Math.Sin(a));}
        /// <summary>The tube's taper: full in the middle, sharp blade tips at both ends.</summary>
        public static float Taper(float s)=>(float)Math.Pow(Math.Max(0,Math.Sin(Math.PI*Clamp01(s))),.42);
        public static Quaternion Turn(float spin)=>MeleeMath.AxisAngle(MeleeMath.Y,spin);
        /// <summary>Arc `arc`'s centre line at s this frame (its turn about the group's up axis applied).</summary>
        public static Vector3 ArcPoint(AuditorHaloFrame f,int arc,float s)=>Vector3.Transform(Centre(arc,s),Turn(f.Spin[arc]));
        /// <summary>The ring angle between arc i's end and the next arc's start this frame (a turn of +d about up moves an arc by -d in ring angle).</summary>
        public static float Gap(AuditorHaloFrame f,int i)
        {int j=(i+1)%Arcs;float end=Angle(i,1)-f.Spin[i],start=Angle(j,0)-f.Spin[j];if(j==0)start+=MeleeMath.TAU;return start-end;}

        // A tube along arc `arc` (radius scaled by Taper) with `sides` around: kind, s, the arc, `layer` (shell 0 inner, 1 outer).
        private static void AddTube(HaloMesh m,int arc,float radius,int segments,int sides,float kind,float layer)
        {
            int start=m.Positions.Count;
            for(int k=0;k<=segments;k++)
            {
                float s=k/(float)segments,a=Angle(arc,s),r=radius*Taper(s);var c=Centre(arc,s);var outward=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
                for(int j=0;j<=sides;j++){float v=j/(float)sides*MeleeMath.TAU;var n=outward*(float)Math.Cos(v)+MeleeMath.Y*(float)Math.Sin(v);m.Add(c+n*r,n,new Vector4(kind,s,arc,layer),Vector4.Zero);}
            }
            // Counter-clockwise seen from outside: along (+angle) by around (+v toward up).
            for(int k=0;k<segments;k++)for(int j=0;j<sides;j++){int a=start+k*(sides+1)+j,b=a+sides+1;m.Quad(a,a+1,b+1,b);}
        }
        /// <summary>The three core arcs (one mesh; the shader turns each by its own spin).</summary>
        public static void BuildArcs(HaloMesh m){for(int i=0;i<Arcs;i++)AddTube(m,i,Tube,ArcSegments,TubeSides,KindArc,0);}
        /// <summary>The glow drawn under the flames: the shells (only the inner one when simple) and, in full, the haze.</summary>
        public static void BuildGlow(HaloMesh m,bool simple)
        {
            for(int i=0;i<Arcs;i++){AddTube(m,i,InnerShell,ShellSegments,ShellSides,KindShell,0);if(!simple)AddTube(m,i,OuterShell,ShellSegments,ShellSides,KindShell,1);}
            if(!simple)Sprite(m,KindHaze,0,HazeSize,Vector3.Zero);
        }
        /// <summary>The glow drawn over the flames (full halo only): the crackle across the gaps and the embers.</summary>
        public static void BuildSparks(HaloMesh m)
        {
            // Crackle: per gap two crossed ribbons (plane 0 and 1) of BoltSegments; the shader places them between the uniforms' ends.
            for(int b=0;b<Arcs;b++)for(int plane=0;plane<2;plane++)
            {
                int start=m.Positions.Count;
                for(int k=0;k<=BoltSegments;k++)for(int side=-1;side<=1;side+=2)m.Add(Vector3.Zero,MeleeMath.Y,new Vector4(KindBolt,k/(float)BoltSegments,b,side),new Vector4(plane,k,0,0));
                for(int k=0;k<BoltSegments;k++){int a=start+k*2;m.Quad(a,a+1,a+3,a+2);}
            }
            for(int e=0;e<Embers;e++)Sprite(m,KindEmber,e,EmberSize*(.75f+.5f*Hash(e*7+3)),Vector3.Zero);
        }
        private static void Sprite(HaloMesh m,float kind,int index,float size,Vector3 at)
        {
            int start=m.Positions.Count;
            for(int c=0;c<4;c++){float x=c==0||c==3?-1:1,y=c<2?-1:1;m.Add(at,MeleeMath.Z,new Vector4(kind,x,index,y),new Vector4(size,0,0,0));}
            m.Quad(start,start+1,start+2,start+3);
        }
        /// <summary>Twelve flame slots: camera-facing quads the shader stands on the ring from the uniforms (slot order: far to near).</summary>
        public static void BuildFlames(HaloMesh m)
        {
            for(int n=0;n<Flames;n++)
            {
                int start=m.Positions.Count;
                for(int c=0;c<4;c++){float x=c==0||c==3?-FlameQuad:FlameQuad,y=c<2?-.1f:1;m.Add(Vector3.Zero,MeleeMath.Z,new Vector4(KindFlame,x,n,y),Vector4.Zero);}
                m.Quad(start,start+1,start+2,start+3);
            }
        }
        /// <summary>The reduced halo's flames: the source's flat flame shape (NativePortraitRules.Flame) at each ring point, opaque black.</summary>
        public static void BuildFins(HaloMesh m)
        {
            var outline=NativePortraitRules.FlameOutline();var tris=NativePortraitRules.EarClip(outline);
            for(int n=0;n<Flames;n++)
            {
                float a=n*MeleeMath.PI/6;var at=new Vector3((float)Math.Cos(a)*FlameRing,0,(float)Math.Sin(a)*FlameRing);int start=m.Positions.Count;
                foreach(var p in outline)m.Add(at,MeleeMath.Y,new Vector4(KindFin,p.X,n,p.Y),Vector4.Zero);
                foreach(int i in tris)m.Indices.Add(start+i);
            }
        }

        /// <summary>The halo at `time` (any presentation clock; it only needs to run forward smoothly).</summary>
        public static void Evaluate(float time,bool simple,AuditorHaloFrame f)
        {
            f.Simple=simple;f.Clock=time-(float)Math.Floor(time/ClockWrap)*ClockWrap;
            NativePortraitRules.HaloGroup(time,out f.GroupPosition,out f.GroupRotation,out f.GroupScale);
            for(int i=0;i<Arcs;i++)f.Spin[i]=Counter*time+Wobble*(float)Math.Sin(WobbleRate[i]*time+i*Step);
            // Crackle: a narrow gap arcs continuously; any gap bursts now and then; the strength flickers at 24 Hz and the bolt re-forms at 18 Hz.
            int window=(int)Math.Floor(time*6),flicker=(int)Math.Floor(time*24),jitter=(int)Math.Floor(time*18);
            for(int i=0;i<Arcs;i++)
            {
                float near=Smooth((CrackleGap-Gap(f,i))/.1f),burst=!simple&&Hash(window*3+i)<BurstChance?1:0;
                f.Bolt[i]=simple?0:Math.Max(near*.9f,burst)*(.55f+.45f*Hash(flicker*7+i*13));f.BoltSeed[i]=(jitter*3+i)%4093;
                f.BoltFrom[i]=ArcPoint(f,i,1);f.BoltTo[i]=ArcPoint(f,(i+1)%Arcs,0);
            }
            for(int i=0;i<Arcs;i++)
            {
                float surge=Math.Max(f.Bolt[i],f.Bolt[(i+Arcs-1)%Arcs]);
                f.Pulse[i]=.86f+.10f*(float)Math.Sin(2.6f*time+i*1.9f)+.05f*(float)Math.Sin(9.7f*time+i*4.3f)+.3f*surge;
            }
            for(int n=0;n<Flames;n++)
            {
                float a=n*MeleeMath.PI/6;bool big=n%2==0;f.FlameBase[n]=new Vector3((float)Math.Cos(a)*FlameRing,0,(float)Math.Sin(a)*FlameRing);
                float tall=big?.9f+.2f*Hash(n*23+1):SmallHeight*(.9f+.2f*Hash(n*29+7));
                f.FlameHeight[n]=tall*(FlameTall+.06f*(float)Math.Sin(3.1f*time+n*1.7f)+.035f*(float)Math.Sin(7.9f*time+n*2.9f)+.02f*(float)Math.Sin(13.3f*time+n*.9f));
                f.FlameWidth[n]=(big?1:SmallWidth)*(FlameHalfWidth+.008f*(float)Math.Sin(4.3f*time+n));f.FlamePhase[n]=n*1.37f;f.FlameRim[n]=.75f+.25f*(float)Math.Sin(5.3f*time+n*2.3f);f.Order[n]=n;
                // animateCosmetics: scale (.65 + .25 sin(4t + n), .65 + .35 sin(6t + 1.7n)), roll sin(5t + n) * .17.
                NativePortraitRules.HaloFlame(n,time,out _,out _,out var scale);f.FinScale[n]=new Vector2(scale.X,scale.Y);f.FinTilt[n]=(float)Math.Sin(time*5+n)*.17f;
            }
            for(int e=0;e<Embers;e++)
            {
                if(simple){f.EmberAlpha[e]=0;f.Ember[e]=Vector3.Zero;continue;}
                float life=1.7f+.9f*Hash(e*11+1),run=time/life+Hash(e*5+2);int cycle=(int)Math.Floor(run);float q=run-cycle;
                float a=MeleeMath.TAU*Hash(e*31+cycle*7)+q*.7f,r=FlameRing+.07f*(Hash(e*17+cycle*13)-.5f);
                f.Ember[e]=new Vector3(r*(float)Math.Cos(a)+.03f*(float)Math.Sin(q*9+e),.02f+q*EmberRise,r*(float)Math.Sin(a));
                f.EmberAlpha[e]=Smooth(q/.12f)*(1-Smooth((q-.45f)/.55f))*(.7f+.3f*(float)Math.Sin(time*17+e*2.1f));
            }
            f.Glow=simple?0:.9f+.1f*(float)Math.Sin(1.7f*time);
            float mean=(f.Pulse[0]+f.Pulse[1]+f.Pulse[2])/3;f.Light=simple?0:Clamp01(.55f+1.4f*(mean-.86f));
        }
        /// <summary>Flame draw order far to near from `eye` (group space): premultiplied flames blend correctly over each other.</summary>
        public static void Sort(AuditorHaloFrame f,Vector3 eye)
        {
            for(int n=0;n<Flames;n++)f.Order[n]=n;
            for(int i=1;i<Flames;i++){int o=f.Order[i];float d=Vector3.DistanceSquared(f.FlameBase[o],eye);int j=i-1;while(j>=0&&Vector3.DistanceSquared(f.FlameBase[f.Order[j]],eye)<d){f.Order[j+1]=f.Order[j];j--;}f.Order[j+1]=o;}
        }
        /// <summary>
        /// The uniforms in Unity's frame: positions x-mirrored, the arc turns negated (a mirror turns a rotation about up the
        /// other way), the flames packed in draw order. `opacity` fades the whole halo (corpses, death scenes).
        /// </summary>
        public static void Pack(AuditorHaloFrame f,float opacity,AuditorHaloUniforms u)
        {
            static Vector3 M(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
            u.A=new Vector4(f.Clock,Clamp01(opacity),f.Glow,f.Light);u.Spin=new Vector4(-f.Spin[0],-f.Spin[1],-f.Spin[2],0);u.Pulse=new Vector4(f.Pulse[0],f.Pulse[1],f.Pulse[2],0);
            for(int i=0;i<Arcs;i++){u.Bolts[i]=new Vector4(M(f.BoltFrom[i]),f.Bolt[i]);u.Bolts[i+Arcs]=new Vector4(M(f.BoltTo[i]),f.BoltSeed[i]);}
            for(int k=0;k<Flames;k++){int n=f.Order[k];u.Flames[k]=new Vector4(M(f.FlameBase[n]),f.FlameHeight[n]);u.Flames[k+Flames]=new Vector4(f.FlameWidth[n],f.FlamePhase[n],Hash(n*19+5),f.FlameRim[n]);}
            for(int n=0;n<Flames;n++)u.Fins[n]=new Vector4(f.FinScale[n].X,f.FinScale[n].Y,f.FinTilt[n],0);
            for(int e=0;e<Embers;e++)u.Embers[e]=new Vector4(M(f.Ember[e]),f.EmberAlpha[e]);
        }
        /// <summary>The shader's arc turn (Resources/NativeAuditorHalo.shader Turn) of a Unity-frame vertex by a packed spin.</summary>
        public static Vector3 ShaderTurn(Vector3 p,float spin){float c=(float)Math.Cos(spin),s=(float)Math.Sin(spin);return new Vector3(p.X*c+p.Z*s,p.Y,-p.X*s+p.Z*c);}
        /// <summary>The reduced flame's offset from its ring point (Unity frame): the shape point scaled, rolled, then along the ring's outward direction and up.</summary>
        public static Vector3 FinOffset(Vector3 ring,float x,float y,Vector4 fin)
        {
            float sx=x*fin.X,sy=y*fin.Y,c=(float)Math.Cos(fin.Z),s=(float)Math.Sin(fin.Z);var outward=Vector3.Normalize(new Vector3(ring.X,0,ring.Z));
            return outward*(sx*c-sy*s)+MeleeMath.Y*(sx*s+sy*c);
        }

        // PCG-style integer hash to [0, 1): the same on every machine, so the bursts are deterministic for a clock.
        public static float Hash(int n){unchecked{uint x=(uint)n*747796405u+2891336453u;x=((x>>(int)((x>>28)+4u))^x)*277803737u;x=(x>>22)^x;return (x&0xffffff)/16777216f;}}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
