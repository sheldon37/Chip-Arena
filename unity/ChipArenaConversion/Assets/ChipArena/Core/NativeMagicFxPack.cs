using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Realistic magic (Claude MG1, 2026-11-25; effects plan phase 3; owner: "go with recommended", and "Yes, download it" for the licensed
    /// realistic effects pack): the magic schools' bolts, trails and hits drawn from that pack's photographic flipbooks (its flame burst,
    /// smoke wisps, plasma ball, lightning arcs, energy shell and spray waves), rebuilt here as pure functions of a bolt's recorded path,
    /// the presentation clock, the shot key and the replicated event id, so host, guests and the killcam draw the same frames.
    /// Runtime NativeVfxDraw draws them in its one sorted batch (NativeVfx.shader modes 16 glow, 17 fire, 18 smoke).
    /// <para>Per school, in the school's own colour (the shot's halo colour, NativeShotRules.HaloColor), which readability rests on:
    /// fire: a rolling flame ball with a white-hot core, flames licking back off it and turning to smoke, embers; water: a shimmering
    /// water shell round a bright core, spray waves streaming back, mist and droplets; lightning: a crackling plasma ball, arcs struck
    /// along its path, a flash, a plasma burst and arcs thrown out where it hits; L33t H4x: a green energy shell round a plasma core,
    /// glowing energy wisps behind it, a green flame burst; darkness keeps NativeVoidOrbFx's black core, lens and swirl, its toon puffs
    /// become realistic dark smoke and it burns with black flames (<see cref="DarkFlames"/>, <see cref="DarkBurst"/>).</para>
    /// <para>Where the sheets come from: the separate effects bank (git-ignored Assets/LocalAssets/ChipArenaFx/Resources/<see cref="Folder"/>,
    /// the folder the explosions and C2's cache effects use, shipped by Astra's effects-bank packer; tools/fx_bank/magic.py writes them).
    /// Without them (CI, cloud builds, other checkouts, the public source) nothing here draws and every bolt keeps today's look.</para>
    /// <para>What stays (V1's readability guardrails): the cast charge and every telegraph, the bolt's halo, spill and contact lights, the
    /// short HDR ribbon on its path, the fire staff's 1.5 m blast (the effects bank's fireball where installed), the void orb's core,
    /// lens, swirl, motes, ink and drain. Cosmetic only: no trajectory, impact timing, damage, rule, random draw or network message reads
    /// anything here.</para>
    /// </summary>
    public static class NativeMagicFxPack
    {
        /// <summary>The effects bank's magic atlases: Resources/<see cref="Folder"/>/<i>name</i> in Assets/LocalAssets/ChipArenaFx.</summary>
        public const string Folder="ChipArenaMagicFx";
        public static readonly string[] Names={"magic0","magic1"};
        public const int Atlases=2;
        public static string Path(int atlas)=>Folder+"/"+Names[atlas];
        /// <summary>The flipbooks in the atlases (tools/fx_bank/magic.py's layout): the flame burst (8 x 4, tall frames), smoke wisps (8 x 8),
        /// the plasma ball (16 x 8), lightning arcs (4 x 8, wide frames), the energy shell (8 x 8) and spray waves (8 x 16, wide frames).</summary>
        public const int Flame=0,Smoke=1,Plasma=2,Arcs=3,Shell=4,Waves=5,Subs=6;
        /// <summary>A flipbook's place: its atlas, its rect in it (fractions, from the image's top left) and its grid (row 0 at the top).</summary>
        public struct Sub{public int Atlas,Columns,Rows;public float X,Y,W,H;}
        public static readonly Sub[] Sheets=
        {
            new Sub{Atlas=0,X=0,Y=0,W=.5f,H=.5f,Columns=8,Rows=4},
            new Sub{Atlas=0,X=.5f,Y=0,W=.5f,H=.5f,Columns=8,Rows=8},
            new Sub{Atlas=0,X=0,Y=.5f,W=.5f,H=.5f,Columns=16,Rows=8},
            new Sub{Atlas=0,X=.5f,Y=.5f,W=.5f,H=.5f,Columns=4,Rows=8},
            new Sub{Atlas=1,X=0,Y=0,W=.5f,H=1,Columns=8,Rows=8},
            new Sub{Atlas=1,X=.5f,Y=0,W=.5f,H=1,Columns=8,Rows=16},
        };
        /// <summary>The atlases' pixel sizes (width, height).</summary>
        public static readonly int[] AtlasWidth={2048,2048},AtlasHeight={2048,1024};
        public static int Frames(int sub)=>Sheets[sub].Columns*Sheets[sub].Rows;
        /// <summary>A frame's width over its height (a camera-facing sprite keeps it: the flames are tall, the arcs and waves wide).</summary>
        public static float Aspect(int sub){var s=Sheets[sub];return s.W*AtlasWidth[s.Atlas]/s.Columns/(s.H*AtlasHeight[s.Atlas]/s.Rows);}
        /// <summary>The layer's switch (a DEV and capture comparison; the game leaves it on).</summary>
        public static bool On=true;

        // ---- small helpers: stable hashes, curves, colours ----
        public static float H(long seed,long i,uint salt){unchecked{uint h=(uint)seed*0x9e3779b1u^(uint)(seed>>32)*0x7feb352du^(uint)i*0x85ebca6bu^(uint)(i>>32)*0x27d4eb2fu^salt*0xc2b2ae35u;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Mix(float a,float b,float t)=>a+(b-a)*t;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>Evenly spaced keys over 0..1, smooth between keys.</summary>
        public static float Curve(float[] k,float u)
        {
            if(k==null||k.Length==0)return 1;if(k.Length==1||!(u>0))return k[0];if(u>=1)return k[k.Length-1];
            float x=u*(k.Length-1);int i=(int)x;float t=x-i;t=t*t*(3-2*t);return Mix(k[i],k[i+1],t);
        }
        private static Vector3 Sphere(long seed,long i,uint salt)
        {
            float z=H(seed,i,salt)*2-1,a=H(seed,i,salt+1)*6.2831853f,r=(float)Math.Sqrt(Math.Max(0,1-z*z));
            return new Vector3(r*(float)Math.Cos(a),z,r*(float)Math.Sin(a));
        }
        /// <summary>A direction over the hemisphere about a surface's normal (biased toward it), or the sphere on a pilot (no normal).</summary>
        private static Vector3 Out(long seed,long i,Vector3 normal,bool surface,float bias=.45f)
        {
            var d=Sphere(seed,i,40);if(!surface)return d;float n=Vector3.Dot(d,normal);if(n<0)d-=2*n*normal;return Vector3.Normalize(d+normal*bias);
        }
        /// <summary>A linear colour as a packed sRGB hex (its brightest channel at 1: the gain carries the strength).</summary>
        public static int Hex(Vector3 linear)
        {
            float m=Math.Max(linear.X,Math.Max(linear.Y,linear.Z));if(!(m>1e-5f))return 0;var c=linear/m;
            int S(float v){v=Clamp01(v);float s=v<=.0031308f?v*12.92f:1.055f*(float)Math.Pow(v,1/2.4)-.055f;return (int)Math.Round(Clamp01(s)*255);}
            return S(c.X)<<16|S(c.Y)<<8|S(c.Z);
        }
        private static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
        private static readonly Vector3 White=Vector3.One;
        /// <summary>The fire blasts' flame yellow (NativeBlastSim.FireYellow), linear: the fire school's hot core.</summary>
        public static readonly Vector3 FireHot=new Vector3(1,.62f,.2f);
        public const int FireSmokeHex=0x4f4945,DarkSmokeHex=0x1c1226,BlackFireHex=0x0e0814;
        /// <summary>How many of a count a quality keeps (at least one; Reduced effects .3 keeps a third, the low tier halves it).</summary>
        public static int Keep(int count,float quality,bool low){if(count<=1)return count;float q=Math.Max(.3f,Math.Min(1,quality))*(low?.5f:1);return Math.Max(1,(int)Math.Round(count*q));}

        // ---- emission ----
        private static void Add(NativeMagicList into,in NativeMagicSprite s){if(into!=null&&s.Size>1e-4f&&s.Opacity>.002f&&float.IsFinite(s.At.X+s.At.Y+s.At.Z))into.Add(s);}
        private static NativeMagicSprite Glow(int sheet,float frame,Vector3 at,float size,float roll,Vector3 tint,float gain,float hot,float occlusion,float opacity,Vector3 segment=default)
            =>new NativeMagicSprite{Look=NativeMagicLook.Glow,Sheet=sheet,Frame=frame,At=at,Size=size,Roll=roll,Hex=Hex(tint),Gain=gain,Hot=hot,Occlusion=occlusion,Opacity=Clamp01(opacity),Segment=segment};
        private static NativeMagicSprite Fire(float frame,Vector3 at,float size,float roll,float heat,float gain,int smokeHex,float self,float occlusion,float opacity,Vector3 segment=default)
            =>new NativeMagicSprite{Look=NativeMagicLook.Fire,Sheet=Flame,Frame=frame,At=at,Size=size,Roll=roll,Heat=heat,Gain=gain,Hex=smokeHex,Self=self,Occlusion=occlusion,Opacity=Clamp01(opacity),Segment=segment};
        private static NativeMagicSprite SmokeOf(int sheet,float frame,Vector3 at,float size,float roll,int hex,float gain,float self,float opacity,Vector3 segment=default)
            =>new NativeMagicSprite{Look=NativeMagicLook.Smoke,Sheet=sheet,Frame=frame,At=at,Size=size,Roll=roll,Hex=hex,Gain=gain,Self=self,Occlusion=1,Opacity=Clamp01(opacity),Segment=segment};
        private static NativeMagicSprite Light(Vector3 at,Vector3 tint,float size,float opacity,int profile=1)
            =>new NativeMagicSprite{Look=NativeMagicLook.CodeGlow,At=at,Tint=tint,Size=size,Opacity=Clamp01(opacity),Profile=profile,Sheet=-1};
        private static NativeMagicSprite Streak(Vector3 head,Vector3 velocity,Vector3 tint,float width,float opacity,float stretch=.035f)
            =>new NativeMagicSprite{Look=NativeMagicLook.CodeStreak,At=head,Velocity=velocity,Tint=tint,Size=width,Opacity=Clamp01(opacity),Stretch=stretch,Sheet=-1};
        private static NativeMagicSprite Drop(Vector3 head,Vector3 segment,Vector3 tint,float width,float coverage)
            =>new NativeMagicSprite{Look=NativeMagicLook.Drop,At=head,Segment=segment,Tint=tint,Size=width,Opacity=Clamp01(coverage),Sheet=-1};
        /// <summary>A looping frame of a flipbook at time t (fps), from frame `from` over `span` frames, offset by a phase 0..1.</summary>
        private static float Loop(int sub,float t,float fps,int from,int span,float phase)
        {float f=t*fps+phase*span;f-=(float)Math.Floor(f/span)*span;return Math.Min(Frames(sub)-1.001f,from+f);}
        /// <summary>A frame played once over a life (u 0..1) from frame `from` over `span` frames.</summary>
        private static float Play(int sub,float u,int from,int span)=>Math.Min(Frames(sub)-1.001f,from+Clamp01(u)*(span-1));

        // ---- the bolt's head ----
        /// <summary>
        /// A school bolt's head at `time` (the presentation clock): at its presented position `at`, flying along `dir` (unit, or zero before
        /// its second frame) at `speed` m/s; `radius` the visual radius (NativeTrailFx.Radius), `halo` the shot's colour (linear), `seed`
        /// the emitter's (the shot key). Cosmetic: the sprites ride the bolt's own presented head.
        /// </summary>
        public static void Head(NativeMagicList into,NativeMagicSchool school,Vector3 at,Vector3 dir,float speed,float radius,Vector3 halo,long seed,float time,float quality,bool low)
        {
            if(into==null||school==NativeMagicSchool.None)return;float r=Math.Max(.08f,radius),t=time,spin=H(seed,1,1)*6.2831853f;bool moving=dir.LengthSquared()>.25f;var back=moving?-dir:Vector3.Zero;
            switch(school)
            {
                case NativeMagicSchool.Fire:
                {
                    // A white-hot core in the fire's yellow, a warm halo, and three flames rolling round it, licking back along the flight.
                    Add(into,Light(at,FireHot*3.4f,r*2.6f,1));
                    Add(into,Light(at,Lerp(halo,FireHot,.3f)*.65f,r*7f,1,0));
                    for(int k=0;k<3;k++)
                    {
                        float ph=H(seed,k,2),frame=Loop(Flame,t,26,6,14,ph),size=r*(5.2f-.8f*k);var p=at+back*(r*(.15f+.6f*k));
                        var seg=moving?dir*(r*(1.6f+1.2f*k)):Vector3.Zero;
                        Add(into,Fire(frame,p,size,spin+k*2.1f+t*(k%2==0?2.4f:-3.1f),1.55f-.25f*k,2.3f,FireSmokeHex,.08f,.78f,.95f-.12f*k,seg));
                    }
                    break;
                }
                case NativeMagicSchool.Water:
                {
                    // A shimmering water shell round a bright core, its spray streaming back along the flight.
                    var pale=Lerp(halo,White,.45f);
                    Add(into,Light(at,pale*2.6f,r*2.2f,1));
                    Add(into,Glow(Shell,Loop(Shell,t,22,0,64,H(seed,3,3)),at,r*5.4f,spin+t*1.3f,pale,1.5f,.55f,0,1));
                    Add(into,Glow(Shell,Loop(Shell,t,17,0,64,H(seed,4,3)),at,r*4.4f,spin-t*1.9f,halo,1.1f,.3f,0,.8f));
                    if(moving)Add(into,SmokeOf(Waves,Loop(Waves,t,30,0,128,H(seed,5,3)),at+back*(r*2.2f),r*3.2f,0,Hex(pale),1.15f,.55f,.7f,dir*(r*5f)));
                    Add(into,Light(at,halo*.55f,r*7f,1,0));
                    break;
                }
                case NativeMagicSchool.Lightning:
                {
                    // A crackling plasma ball (two phases), arcs round it re-struck every 50 ms, a white core.
                    var hot=Lerp(halo,White,.35f);
                    Add(into,Light(at,hot*3f,r*2.2f,1));
                    Add(into,Glow(Plasma,Loop(Plasma,t,34,0,128,H(seed,6,4)),at,r*7.2f,spin,hot,2.6f,.75f,0,1));
                    Add(into,Glow(Plasma,Loop(Plasma,t,27,0,128,H(seed,7,4)),at,r*5.4f,spin+2.2f,halo,2.2f,.5f,0,.9f));
                    long beat=(long)Math.Floor(t*20);
                    for(int k=0;k<2;k++)
                    {
                        if(H(seed,beat*2+k,5)<.25f)continue;float frame=8+(int)(H(seed,beat*2+k,6)*8);float roll=H(seed,beat*2+k,7)*6.2831853f;
                        Add(into,Glow(Arcs,frame,at,r*(6f+2f*H(seed,beat*2+k,8)),roll,hot,2.8f,.85f,0,1));
                    }
                    Add(into,Light(at,halo*.6f,r*7.5f,1,0));
                    break;
                }
                case NativeMagicSchool.Leet:
                {
                    // A green energy shell round a plasma core, both turning; a bright core.
                    var hot=Lerp(halo,White,.3f);
                    Add(into,Light(at,hot*2.8f,r*2.2f,1));
                    Add(into,Glow(Plasma,Loop(Plasma,t,24,0,128,H(seed,9,4)),at,r*4.2f,spin,halo,2.3f,.55f,0,1));
                    Add(into,Glow(Shell,Loop(Shell,t,20,0,64,H(seed,10,4)),at,r*5.8f,spin+t*2.2f,hot,1.9f,.45f,0,1));
                    Add(into,Glow(Shell,Loop(Shell,t,14,0,64,H(seed,11,4)),at,r*4.6f,spin-t*3f,halo,1.4f,.25f,0,.8f));
                    Add(into,Light(at,halo*.6f,r*7f,1,0));
                    break;
                }
            }
        }

        // ---- the trail: sprites every Step metres along the path the bolt flew (NativeMagicTrails records it) ----
        /// <summary>One layer of a school's trail: a sprite every Step m (times the radius / .2), living Life0..Life1 s, sized Size0..Size1
        /// radii over its Grow curve, opacity over Alpha, rising Rise m/s, spread Spread radii; Back keeps it this many radii behind a live head.</summary>
        public sealed class Layer
        {
            public NativeMagicLook Look;public int Sheet=-1;public float Step=.3f,Phase,Life0=.1f,Life1=.2f,Size0=1,Size1=1,Spread,Rise,Back=1.5f,Gain=1,Hot,Heat,Self,Occlusion,Opacity=1;
            public float[] Alpha,Grow,HeatCurve;public int From,Span=1;public float Fps;public float Along;public float HaloWhite;public int Hex=-1;public float Gravity,Speed;public int Profile;
        }
        // A step of .3 m at a .2 m radius: wider bolts shed bigger sprites further apart. The fastest bolts (100-120 m/s) shed every ~.3 m.
        private static readonly Layer[] FireTrail=
        {
            new Layer{Look=NativeMagicLook.Fire,Sheet=Flame,Step=.5f,Life0=.07f,Life1=.13f,Size0=3.2f,Size1=4.2f,Spread=.35f,Rise=.6f,Back=1.2f,Gain=2.1f,Heat=1.4f,Self=.08f,Occlusion=.8f,
                From=6,Span=18,HeatCurve=new[]{1f,.8f,.45f},Alpha=new[]{.9f,1f,.7f,0},Grow=new[]{.8f,1.1f,1.2f},Along=1.4f},
            new Layer{Look=NativeMagicLook.Smoke,Sheet=Smoke,Step=2f,Phase=.5f,Life0=.3f,Life1=.45f,Size0=3f,Size1=4.4f,Spread=.6f,Rise=.9f,Back=6,Gain=.75f,Self=.12f,Hex=FireSmokeHex,
                From=8,Span=48,Alpha=new[]{0f,.55f,.4f,0},Grow=new[]{.6f,1.1f,1.6f}},
            new Layer{Look=NativeMagicLook.CodeStreak,Step=1.5f,Phase=.3f,Life0=.2f,Life1=.4f,Size0=.12f,Size1=.18f,Spread=.5f,Gain=4,HaloWhite=-1,Gravity=.25f,Speed=2.5f,Alpha=new[]{1f,1f,0}},
        };
        private static readonly Layer[] WaterTrail=
        {
            new Layer{Look=NativeMagicLook.Smoke,Sheet=Waves,Step=.7f,Life0=.12f,Life1=.2f,Size0=2.6f,Size1=3.4f,Spread=.25f,Back=1.2f,Gain=1.2f,Self=.55f,HaloWhite=.5f,From=0,Span=128,Fps=30,
                Alpha=new[]{.75f,.6f,.3f,0},Grow=new[]{.9f,1.2f},Along=3.5f},
            new Layer{Look=NativeMagicLook.Smoke,Sheet=Smoke,Step=2f,Phase=.5f,Life0=.3f,Life1=.45f,Size0=2.4f,Size1=3.4f,Spread=.5f,Rise=.2f,Back=5,Gain=1.25f,Self=.6f,HaloWhite=.7f,From=10,Span=44,
                Alpha=new[]{0f,.45f,.3f,0},Grow=new[]{.6f,1.2f,1.6f}},
            new Layer{Look=NativeMagicLook.Drop,Step=1.4f,Phase=.2f,Life0=.25f,Life1=.4f,Size0=.18f,Size1=.26f,Spread=.4f,Gravity=1,Speed=1.8f,HaloWhite=.6f,Opacity=.75f,Alpha=new[]{1f,1f,0}},
        };
        private static readonly Layer[] LightningTrail=
        {
            new Layer{Look=NativeMagicLook.Glow,Sheet=Arcs,Step=.7f,Life0=.06f,Life1=.11f,Size0=1.6f,Size1=2.4f,Spread=.2f,Back=.8f,Gain=2.6f,Hot=.85f,HaloWhite=.35f,From=8,Span=8,Fps=0,
                Alpha=new[]{1f,1f,0},Along=6f},
            new Layer{Look=NativeMagicLook.Glow,Sheet=Plasma,Step=.9f,Phase=.5f,Life0=.08f,Life1=.15f,Size0=2.2f,Size1=3.2f,Spread=.4f,Back=2,Gain=1.6f,Hot=.6f,HaloWhite=.2f,From=0,Span=128,Fps=40,
                Alpha=new[]{.9f,.7f,0},Grow=new[]{1f,.6f}},
        };
        private static readonly Layer[] LeetTrail=
        {
            new Layer{Look=NativeMagicLook.Glow,Sheet=Smoke,Step=.25f,Life0=.25f,Life1=.4f,Size0=2.6f,Size1=3.6f,Spread=.4f,Rise=.15f,Back=1.6f,Gain=1.3f,Hot=.2f,From=6,Span=50,
                Alpha=new[]{0f,.8f,.5f,0},Grow=new[]{.7f,1.2f,1.5f}},
            new Layer{Look=NativeMagicLook.Glow,Sheet=Plasma,Step=.3f,Phase=.5f,Life0=.12f,Life1=.22f,Size0=1.6f,Size1=2.4f,Spread=.6f,Back=1.4f,Gain=1.8f,Hot=.4f,HaloWhite=.2f,From=0,Span=128,Fps=36,
                Alpha=new[]{1f,.7f,0},Grow=new[]{1f,.5f}},
            new Layer{Look=NativeMagicLook.CodeStreak,Step=.5f,Phase=.25f,Life0=.3f,Life1=.55f,Size0=.1f,Size1=.14f,Spread=.6f,Gain=3.4f,HaloWhite=.3f,Speed=1.6f,Alpha=new[]{1f,1f,0}},
        };
        public static Layer[] Trail(NativeMagicSchool school)=>school==NativeMagicSchool.Fire?FireTrail:school==NativeMagicSchool.Water?WaterTrail:school==NativeMagicSchool.Lightning?LightningTrail:school==NativeMagicSchool.Leet?LeetTrail:null;
        /// <summary>The longest a school's trail sprite lives (s): how long a trail outlives its bolt.</summary>
        public static float TrailLife(NativeMagicSchool school){var t=Trail(school);float m=0;if(t!=null)foreach(var l in t)m=Math.Max(m,l.Life1);return m;}
        /// <summary>A layer's spacing at a radius and quality (Reduced effects and the low tier shed fewer).</summary>
        public static float StepOf(Layer l,float radius,float quality,bool low)=>l.Step*Math.Max(.6f,radius/.2f)/Math.Max(.3f,Math.Min(1,quality))*(low?1.6f:1);

        /// <summary>
        /// One trail's sprites at `time`: `trail` the recorded path (NativeMagicTrails), its school, radius, halo colour and key. A sprite's
        /// place, birth and look are pure functions of its distance along the path and the key.
        /// </summary>
        public static void Trail(NativeMagicList into,NativeMagicTrails.Trail trail,float time,float quality,bool low)
        {
            var layers=Trail(trail.School);if(into==null||layers==null||trail.Samples<2)return;float r=Math.Max(.08f,trail.Radius);var halo=trail.Halo;
            for(int li=0;li<layers.Length;li++)
            {
                var l=layers[li];float step=StepOf(l,r,quality,low);
                int first=(int)Math.Ceiling(trail.DistanceAt(time-l.Life1)/step-l.Phase),last=(int)Math.Floor(trail.Travel/step-l.Phase);
                for(int j=Math.Max(0,first);j<=last;j++)
                {
                    float d=(j+l.Phase)*step;if(!trail.At(d,out var at,out float born,out var dir))continue;long k=j*31+li*1000003L;long seed=trail.Key;
                    float life=Mix(l.Life0,l.Life1,H(seed,k,1)),a=time-born;if(a<0||a>=life)continue;float u=a/life;
                    float behind=trail.Live?trail.Travel-d:1e9f;if(behind<l.Back*r)continue;
                    var jitter=Sphere(seed,k,10)*(l.Spread*r*(float)Math.Sqrt(H(seed,k,2)));var p=at+jitter+new Vector3(0,l.Rise*a,0);
                    float size=Mix(l.Size0,l.Size1,H(seed,k,3))*r*Curve(l.Grow,u),opacity=Curve(l.Alpha,u)*l.Opacity,roll=H(seed,k,4)*6.2831853f+(H(seed,k,5)-.5f)*1.2f*u;
                    var tint=l.HaloWhite>=0?Lerp(halo,White,l.HaloWhite):FireHot;
                    float frame=l.Sheet<0?0:l.Fps>0?Loop(l.Sheet,a,l.Fps,l.From,l.Span,H(seed,k,6)):Play(l.Sheet,u,l.From,l.Span);
                    if(l.Sheet==Arcs)frame=l.From+(int)(H(seed,k,6)*l.Span);
                    var seg=l.Along>0&&dir.LengthSquared()>.25f?dir*(l.Along*r):Vector3.Zero;
                    switch(l.Look)
                    {
                        case NativeMagicLook.Fire:Add(into,Fire(frame,p,size,roll,Curve(l.HeatCurve,u)*l.Heat,l.Gain,FireSmokeHex,l.Self,l.Occlusion,opacity,seg));break;
                        case NativeMagicLook.Smoke:Add(into,SmokeOf(l.Sheet,frame,p,size,seg.LengthSquared()>0?0:roll,l.Hex>=0?l.Hex:Hex(tint),l.Gain,l.Self,opacity,seg));break;
                        case NativeMagicLook.Glow:Add(into,Glow(l.Sheet,frame,p,size,seg.LengthSquared()>0?0:roll,tint,l.Gain,l.Hot,l.Occlusion,opacity,seg));break;
                        case NativeMagicLook.CodeStreak:case NativeMagicLook.Drop:
                        {
                            // Embers, sparks and droplets thrown off the path: out from it, falling (gravity, in g) or drifting.
                            var v=Sphere(seed,k,12)*(l.Speed*(.5f+.5f*H(seed,k,13)));var q=p+v*a-new Vector3(0,.5f*l.Gravity*9.81f*a*a,0);var vel=v-new Vector3(0,l.Gravity*9.81f*a,0);
                            if(l.Look==NativeMagicLook.CodeStreak)Add(into,Streak(q,vel,tint*l.Gain,size,opacity));
                            else Add(into,Drop(q,vel*.04f,tint,size,opacity));
                            break;
                        }
                    }
                }
            }
        }

        // ---- hits ----
        /// <summary>A hit's sprites live this long (s).</summary>
        public const float ImpactLife=1.1f;
        /// <summary>
        /// A school bolt's hit at `age` seconds: `at` the hit point, `normal` the struck surface's (zero on a pilot: the burst opens all
        /// round), `seed` from the replicated event id, `tint` the bolt's colour (linear), `scale` its size (1.3 a surface, 1.1 a pilot).
        /// Nothing outside 0..ImpactLife.
        /// </summary>
        public static void Impact(NativeMagicList into,NativeMagicSchool school,Vector3 at,Vector3 normal,long seed,float age,float scale,Vector3 tint,float quality,bool low)
        {
            if(into==null||school==NativeMagicSchool.None||!(age>=0)||age>=ImpactLife)return;float s=Math.Max(.2f,scale),t=age;
            bool surface=normal.LengthSquared()>.25f;var n=surface?Vector3.Normalize(normal):Vector3.UnitY;var c=at+(surface?n*(.08f*s):Vector3.Zero);
            switch(school)
            {
                case NativeMagicSchool.Fire:
                {
                    // A flash, two flame bursts rolling out of the point (the staff's 1.5 m blast is the blast sim's), embers, a little smoke.
                    if(t<.12f)Add(into,Light(c,FireHot*(4f*(1-t/.12f)),s*2.4f,1));
                    for(int k=0;k<2;k++){float life=.5f+.12f*k,u=t/life;if(u<1){var p=c+Out(seed,k,n,surface,.8f)*(.18f*s*k)+new Vector3(0,.5f*t,0);
                        Add(into,Fire(Play(Flame,u,4,26),p,s*(1.25f+.35f*k)*(.75f+.4f*(float)Math.Sqrt(u)),H(seed,k,20)*6.2831853f,1.7f*(1-.6f*u),2.4f,FireSmokeHex,.1f,.8f,u<.75f?1:(1-u)/.25f));}}
                    Embers(into,c,n,surface,seed,t,Keep(14,quality,low),FireHot,4.5f,6,10,s);
                    for(int k=0;k<Keep(3,quality,low);k++){float life=.8f+.2f*H(seed,k,21),u=(t-.12f)/life;if(u>0&&u<1)Add(into,SmokeOf(Smoke,Play(Smoke,u,6,50),c+Out(seed,k+9,n,surface)*(.4f*s)+new Vector3(0,.6f*t,0),s*(1.1f+.6f*u),H(seed,k,22)*6.2831853f,FireSmokeHex,.8f,.12f,Curve(new[]{0f,.6f,.35f,0},u)));}
                    break;
                }
                case NativeMagicSchool.Water:
                {
                    // A flash, the shell bursting, mist rolling out, spray waves thrown out and droplets falling.
                    var pale=Lerp(tint,White,.5f);
                    if(t<.1f)Add(into,Light(c,pale*(3f*(1-t/.1f)),s*1.8f,1));
                    {float u=t/.28f;if(u<1)Add(into,Glow(Shell,Play(Shell,u,0,64),c,s*(.6f+1.4f*(float)Math.Sqrt(u)),H(seed,1,23)*6.2831853f,pale,1.8f*(1-u),.5f,0,1));}
                    for(int k=0;k<Keep(4,quality,low);k++){float life=.6f+.3f*H(seed,k,24),u=t/life;if(u<1)Add(into,SmokeOf(Smoke,Play(Smoke,u,4,56),c+Out(seed,k,n,surface)*(.5f*s*(float)Math.Sqrt(u))+new Vector3(0,.25f*t,0),s*(.9f+.8f*u),H(seed,k,25)*6.2831853f,Hex(pale),1.3f,.6f,Curve(new[]{.2f,.75f,.4f,0},u)));}
                    for(int k=0;k<Keep(6,quality,low);k++){float life=.3f+.12f*H(seed,k,26),u=t/life;if(u<1){var d=Out(seed,k+20,n,surface,.2f);
                        Add(into,SmokeOf(Waves,Play(Waves,u,0,128),c+d*(.55f*s*(float)Math.Sqrt(u)),s*.7f,0,Hex(pale),1.2f,.6f,Curve(new[]{.9f,.6f,0},u),d*(s*1.2f*(.4f+.6f*u))));}}
                    for(int k=0;k<Keep(16,quality,low);k++)
                    {
                        float life=.45f+.3f*H(seed,k,27);if(t>=life)continue;var d=Out(seed,k+40,n,surface,.6f);var v0=d*(2.5f+3f*H(seed,k,28))*s+new Vector3(0,1.2f,0);
                        // Air slows the spray (drag 2/s) while it falls.
                        float slow=(float)Math.Exp(-2*t);var p=c+v0*((1-slow)/2)-new Vector3(0,4.9f*t*t,0);var v=v0*slow-new Vector3(0,9.81f*t,0);Add(into,Drop(p,v*.035f,pale,.05f*s,.8f*(1-t/life)));
                    }
                    break;
                }
                case NativeMagicSchool.Lightning:
                {
                    // A flash, a plasma burst, arcs thrown out from the point and re-struck every 40 ms, sparks.
                    var hot=Lerp(tint,White,.4f);
                    if(t<.12f)Add(into,Light(c,hot*(5f*(1-t/.12f)),s*2.6f,1));
                    {float u=t/.32f;if(u<1)Add(into,Glow(Plasma,Play(Plasma,u,20,100),c,s*(1.6f+.8f*u),H(seed,1,30)*6.2831853f,hot,3f*(1-u*u),.7f,0,1));}
                    long beat=(long)Math.Floor(t/.04f);
                    for(int k=0;k<Keep(5,quality,low);k++)
                    {
                        float u=t/.36f;if(u>=1||H(seed,beat*8+k,31)<.2f)continue;var d=Out(seed,beat*8+k,n,surface,.3f);float len=s*(.7f+.9f*H(seed,beat*8+k,32));
                        Add(into,Glow(Arcs,8+(int)(H(seed,beat*8+k,33)*8),c+d*(len*.5f),s*.55f,0,hot,3f*(1-u),.85f,0,1,d*len));
                    }
                    Embers(into,c,n,surface,seed,t,Keep(12,quality,low),hot,4f,7,12,s,.15f);
                    break;
                }
                case NativeMagicSchool.Leet:
                {
                    // A flash, two green flame bursts (glowing, the pack's green explosion), the shell popping, sparks.
                    var hot=Lerp(tint,White,.3f);
                    if(t<.12f)Add(into,Light(c,hot*(4f*(1-t/.12f)),s*2.2f,1));
                    for(int k=0;k<2;k++){float life=.5f+.1f*k,u=t/life;if(u<1)Add(into,Glow(Flame,Play(Flame,u,4,26),c+Out(seed,k,n,surface,.8f)*(.15f*s*k)+new Vector3(0,.35f*t,0),s*(1.2f+.3f*k)*(.8f+.35f*(float)Math.Sqrt(u)),H(seed,k,34)*6.2831853f,k==0?hot:tint,2.2f,.35f,0,u<.7f?1:(1-u)/.3f));}
                    {float u=t/.3f;if(u<1)Add(into,Glow(Shell,Play(Shell,u,0,64),c,s*(.5f+1.3f*(float)Math.Sqrt(u)),H(seed,2,35)*6.2831853f,hot,2f*(1-u),.4f,0,1));}
                    Embers(into,c,n,surface,seed,t,Keep(12,quality,low),hot,3.4f,5,8,s,.2f);
                    break;
                }
            }
        }
        // Embers or sparks: count streaks out over the hemisphere (or sphere) at vmin..vmax m/s, falling (gravity in g), drag, fading.
        private static void Embers(NativeMagicList into,Vector3 c,Vector3 n,bool surface,long seed,float t,int count,Vector3 tint,float gain,float vmin,float vmax,float s,float gravity=.35f)
        {
            for(int k=0;k<count;k++)
            {
                float life=.35f+.35f*H(seed,k,50);if(t>=life)continue;float u=t/life;var d=Out(seed,k+60,n,surface,.5f);float v=(vmin+(vmax-vmin)*H(seed,k,51))*Math.Max(.6f,s*.8f);
                float travel=v*(1-(float)Math.Exp(-3*t))/3;var p=c+d*travel-new Vector3(0,.5f*gravity*9.81f*t*t,0);var vel=d*(v*(float)Math.Exp(-3*t))-new Vector3(0,gravity*9.81f*t,0);
                Add(into,Streak(p,vel,tint*(gain*(1-u)),.035f*Math.Max(.7f,s),1,.03f));
            }
        }

        // ---- darkness: the void orb's realistic smoke and black flames ----
        /// <summary>A darkness smoke puff (each of NativeVoidOrbFx's toon puffs: its tail, wisps and burst) as a realistic smoke wisp: the same
        /// place, colour, opacity, age and seed, a little larger (the wisp fills less of its frame than a toon puff).</summary>
        public static void DarkSmoke(NativeMagicList into,Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed)
        {
            long k=(long)seed;var c=albedo.LengthSquared()>1e-8f?albedo:new Vector3(.02f,.01f,.03f);
            Add(into,SmokeOf(Smoke,Play(Smoke,Clamp01(u)*.8f+.2f*H(k,1,60),2,60),at,size*1.7f,H(k,2,61)*6.2831853f,Hex(c),Math.Max(c.X,Math.Max(c.Y,c.Z))*1.6f,.35f,opacity*.95f));
        }
        /// <summary>Black flames licking back off a darkness orb in flight (at its presented position, flying along `dir`), each with a dark
        /// violet glow inside it (the orb's rim colour); `radius` the orb's shot radius, `key` its shot key.</summary>
        public static void DarkFlames(NativeMagicList into,Vector3 at,Vector3 dir,float radius,long key,float time,float quality,bool low)
        {
            float r=Math.Max(.1f,radius),t=time;bool moving=dir.LengthSquared()>.25f;var fwd=moving?Vector3.Normalize(dir):Vector3.Zero;int n=Keep(3,quality,low);
            for(int k=0;k<n;k++)
            {
                float ph=H(key,k,62),frame=Loop(Flame,t,22,6,16,ph),size=r*(4.4f-.6f*k),roll=ph*6.2831853f+t*(k%2==0?2:-2.6f);var p=at-fwd*(r*(.4f+.7f*k))+new Vector3(0,r*.15f*k,0);
                var seg=moving?fwd*(r*(2f+1.5f*k)):Vector3.Zero;
                Add(into,Fire(frame,p,size,roll,0,1,BlackFireHex,.05f,1,.9f-.15f*k,seg));
                Add(into,Glow(Flame,frame,p,size*.8f,roll,Rim,.9f,0,0,.55f-.1f*k,seg*.8f));
            }
        }
        /// <summary>The void orb's rim violet (NativeVoidOrbFx.RimHex), linear.</summary>
        public static readonly Vector3 Rim=NativeMagicMath.Lin(NativeVoidOrbFx.RimHex);
        /// <summary>A darkness orb's burst (each NativeVoidOrbFx impact: a direct hit, a surface or the end of its range) at `age` after it:
        /// black flames rolling out after its implosion, over the void orb's own smoke and glows.</summary>
        public static void DarkBurst(NativeMagicList into,Vector3 at,Vector3 normal,bool surface,long seed,float age,float scale,float quality,bool low)
        {
            float t=age-NativeVoidOrbFx.ImplodeSeconds;if(into==null||!(t>=0)||t>=.8f)return;float s=Math.Max(.3f,scale);var n=surface&&normal.LengthSquared()>.25f?Vector3.Normalize(normal):Vector3.UnitY;
            for(int k=0;k<Keep(3,quality,low);k++)
            {
                float life=.6f+.15f*H(seed,k,63),u=t/life;if(u>=1)continue;float frame=Play(Flame,u,4,26),size=s*(1.3f+.3f*k)*(.75f+.4f*(float)Math.Sqrt(u)),roll=H(seed,k,64)*6.2831853f,op=u<.7f?.95f:.95f*(1-u)/.3f;
                var p=at+Out(seed,k,n,surface,.7f)*(.25f*s*k)+new Vector3(0,.4f*t,0);
                Add(into,Fire(frame,p,size,roll,0,1,BlackFireHex,.05f,1,op));
                Add(into,Glow(Flame,frame,p,size*.8f,roll,Rim,1.4f*(1-u),0,0,op*.7f));
            }
        }
    }

    /// <summary>What a magic sprite is drawn as: a glowing flipbook in the school's colour (mode 16), a flame flipbook through the fire ramp
    /// with its cool part a smoke (mode 17), a lit smoke flipbook (mode 18), or one of the code's own HDR glows, streaks and teardrops.</summary>
    public enum NativeMagicLook:byte{Glow,Fire,Smoke,CodeGlow,CodeStreak,Drop}
    /// <summary>One sprite of a magic recipe at one moment: where and how big, its flipbook (Sub) and frame, its colour (a packed sRGB hex for
    /// the flipbooks, linear HDR Tint for the code's), gain, white-hot share (Hot), heat (fire), self light, occlusion (0 adds, 1 blends),
    /// opacity, and a segment it lies along (fire: its tip toward the tail; glow and smoke: their width along it).</summary>
    public struct NativeMagicSprite
    {
        public NativeMagicLook Look;public int Sheet,Hex,Profile;public Vector3 At,Segment,Velocity,Tint;
        public float Size,Roll,Frame,Opacity,Gain,Hot,Occlusion,Heat,Self,Stretch;
    }
    /// <summary>A reused list of magic sprites: no allocation once it has grown.</summary>
    public sealed class NativeMagicList
    {
        public NativeMagicSprite[] Items=new NativeMagicSprite[128];public int Count;
        public void Clear()=>Count=0;
        public void Add(in NativeMagicSprite s){if(Count==Items.Length)Array.Resize(ref Items,Items.Length*2);Items[Count++]=s;}
    }

    /// <summary>
    /// The paths the school bolts flew (presentation only): each bolt's presented head is recorded every frame it is drawn (Seen), and its
    /// trail sprites are pure functions of the distance along that path (NativeMagicFxPack.Trail). A bolt gone from the frame stops
    /// recording; its trail lives out, then its slot is reused. A rewound clock (a new arena, the killcam's replica) starts over.
    /// Fixed pools: no allocation once built.
    /// </summary>
    public sealed class NativeMagicTrails
    {
        public const int MaxTrails=48,MaxSamples=64;public const float SampleStep=.15f;
        public sealed class Trail
        {
            public long Key;public NativeMagicSchool School;public bool Used,Live;public float Radius,LastSeen,FirstSeen;public Vector3 Halo;
            public int Samples;public readonly Vector3[] Path=new Vector3[MaxSamples];public readonly float[] Time=new float[MaxSamples],Distance=new float[MaxSamples];
            public float Travel=>Samples>0?Distance[Samples-1]:0;
            /// <summary>The flight direction at the newest sample (zero before the second).</summary>
            public Vector3 Dir{get{if(Samples<2)return Vector3.Zero;var d=Path[Samples-1]-Path[Samples-2];float l=d.Length();return l>1e-5f?d/l:Vector3.Zero;}}
            public float Speed{get{if(Samples<2)return 0;float dt=Time[Samples-1]-Time[Samples-2];return dt>1e-5f?(Distance[Samples-1]-Distance[Samples-2])/dt:0;}}
            /// <summary>The point at `distance` along the path, when the bolt passed it, and its direction there.</summary>
            public bool At(float distance,out Vector3 at,out float time,out Vector3 dir)
            {
                at=default;time=0;dir=default;if(Samples<2||distance<Distance[0]||distance>Distance[Samples-1])return false;int i=1;while(i<Samples-1&&Distance[i]<distance)i++;
                float span=Distance[i]-Distance[i-1],f=span>1e-5f?Math.Max(0,Math.Min(1,(distance-Distance[i-1])/span)):1;at=Vector3.Lerp(Path[i-1],Path[i],f);time=Time[i-1]+(Time[i]-Time[i-1])*f;
                dir=span>1e-5f?(Path[i]-Path[i-1])/span:Vector3.Zero;return true;
            }
            /// <summary>How far along the path the bolt was at `time`.</summary>
            public float DistanceAt(float time)
            {
                if(Samples==0||time<=Time[0])return Samples>0?Distance[0]:0;
                for(int i=1;i<Samples;i++)if(time<=Time[i]){float span=Time[i]-Time[i-1],f=span>1e-5f?(time-Time[i-1])/span:1;return Distance[i-1]+(Distance[i]-Distance[i-1])*f;}
                return Travel;
            }
        }
        private readonly Trail[] trails=new Trail[MaxTrails];private float clock=float.NegativeInfinity;
        public NativeMagicTrails(){for(int i=0;i<trails.Length;i++)trails[i]=new Trail();}
        public Trail this[int i]=>trails[i];
        public int Count=>trails.Length;
        public void Clear(){foreach(var t in trails){t.Used=false;t.Live=false;t.Samples=0;}clock=float.NegativeInfinity;}
        private Trail Find(long key){foreach(var t in trails)if(t.Used&&t.Key==key)return t;return null;}
        private Trail Take()
        {
            foreach(var t in trails)if(!t.Used)return t;Trail oldest=null;
            foreach(var t in trails)if(!t.Live&&(oldest==null||t.LastSeen<oldest.LastSeen))oldest=t;if(oldest!=null)return oldest;
            foreach(var t in trails)if(oldest==null||t.FirstSeen<oldest.FirstSeen)oldest=t;return oldest;
        }
        /// <summary>A bolt drawn this frame at its presented head `at` (time: the presentation clock). Returns its trail (its direction and
        /// speed so far).</summary>
        public Trail Seen(long key,NativeMagicSchool school,Vector3 at,float time,float radius,Vector3 halo)
        {
            if(time<clock-.25f)Clear();clock=Math.Max(clock,time);
            var t=Find(key);if(t==null){t=Take();t.Used=true;t.Key=key;t.Samples=0;t.FirstSeen=time;}
            t.School=school;t.Radius=radius;t.Halo=halo;t.Live=true;t.LastSeen=time;
            if(t.Samples>0&&(Vector3.Distance(t.Path[t.Samples-1],at)<SampleStep||time<=t.Time[t.Samples-1]))
            {
                // Too close to the last sample: the newest sample follows the head (its time and distance kept monotonic).
                if(t.Samples>1){int i=t.Samples-1;if(time>t.Time[i-1]){t.Path[i]=at;t.Time[i]=time;t.Distance[i]=t.Distance[i-1]+Vector3.Distance(t.Path[i-1],at);}}
                return t;
            }
            if(t.Samples==MaxSamples){Array.Copy(t.Path,1,t.Path,0,MaxSamples-1);Array.Copy(t.Time,1,t.Time,0,MaxSamples-1);Array.Copy(t.Distance,1,t.Distance,0,MaxSamples-1);t.Samples--;}
            float d=t.Samples>0?t.Distance[t.Samples-1]+Vector3.Distance(t.Path[t.Samples-1],at):0;t.Path[t.Samples]=at;t.Time[t.Samples]=time;t.Distance[t.Samples]=d;t.Samples++;
            return t;
        }
        /// <summary>Every trail's sprites at `time`: a bolt not seen this frame stops recording (its trail lives out its sprites' life).</summary>
        public void Draw(NativeMagicList into,float time,float quality,bool low)
        {
            if(time<clock-.25f){Clear();return;}clock=Math.Max(clock,time);
            foreach(var t in trails)
            {
                if(!t.Used)continue;if(t.LastSeen<time-1e-4f)t.Live=false;
                if(!t.Live&&time-t.LastSeen>NativeMagicFxPack.TrailLife(t.School)+.05f){t.Used=false;t.Samples=0;continue;}
                NativeMagicFxPack.Trail(into,t,time,quality,low);
            }
        }
    }
}
