using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A soft glow sprite of an opening frame (locker frame, metres): a mote, or a solid core bloom (never a ring).</summary>
    public struct NativeCacheGlow{public float X,Y,Z,Size,Gain;public int Hex;public bool Bloom;}
    /// <summary>A streak: its hot head (X, Y, Z) and the axis from its tail to its head (Ax, Ay, Az), its width, colour and gain,
    /// and how much dimmer its tail is (NativeVfxDraw.Streak with blur 1). Sparks, embers, the light from the gap, the columns.</summary>
    public struct NativeCacheStreak{public float X,Y,Z,Ax,Ay,Az,Width,Gain,Taper;public int Hex;}
    /// <summary>A soft puff: the lid's dust (smoke) or the cold vapour, its albedo, opacity, size and age (u).</summary>
    public struct NativeCachePuff{public float X,Y,Z,Size,Opacity,U,Glow;public int Hex,Seed;public bool Vapour;}
    /// <summary>One of the buyer's three cartridges: where, its roll (degrees), scale and opacity.</summary>
    public struct NativeCacheCartridge{public float X,Y,Z,Roll,Scale,Alpha;}

    /// <summary>
    /// Everything one cache draws this frame, in the locker's own frame (the origin on the floor under its middle, its front
    /// toward -z, the hinge at the back; <see cref="NativeCacheOpenFx.ToWorld"/> turns it by the anchor's yaw). Fixed arrays:
    /// refilling a frame allocates nothing.
    /// </summary>
    public sealed class NativeCacheFrame
    {
        public const int MaxGlows=256,MaxStreaks=192,MaxPuffs=48;
        /// <summary>The lid's angle (degrees about the hinge: 0 shut, 105 at its stop) and the latches' (degrees, negative open).</summary>
        public float Lid,Latch;
        /// <summary>The lock lamps: how many, each face's colour (sRGB hex) and gain (1 its rest light).</summary>
        public int Lamps;public readonly int[] LampHex=new int[4];public readonly float[] LampGain=new float[4];
        /// <summary>The lid's LED strip and EPIC's lit seams (the tinted-light bones): colour and gain; the tray's see-through glow.</summary>
        public int StripHex;public float StripGain,Seams,Inner;
        /// <summary>The opening's lights (gains times their colour): the spill from the mouth, the purchase's one flash, LEGENDARY's
        /// pillar light, EPIC's sustained seam light; their colours and reaches (m). The lamps' own small light.</summary>
        public float Spill,Flash,Pillar,SeamLight,LampLight;public int SpillHex,FlashHex;public float SpillRange,FlashRange;
        public readonly NativeCacheGlow[] Glows=new NativeCacheGlow[MaxGlows];public int GlowCount;
        public readonly NativeCacheStreak[] Streaks=new NativeCacheStreak[MaxStreaks];public int StreakCount;
        public readonly NativeCachePuff[] Puffs=new NativeCachePuff[MaxPuffs];public int PuffCount;
        public readonly NativeCacheCartridge[] Cartridges=new NativeCacheCartridge[3];public int CartridgeCount;
        /// <summary>The FX quads this frame draws (a glow, streak or puff one each, a cartridge three: the model and its glow).</summary>
        public int Quads=>GlowCount+StreakCount+PuffCount+3*CartridgeCount;
        public void Clear()
        {
            Lid=Latch=0;Lamps=0;StripHex=0;StripGain=Seams=Inner=0;Spill=Flash=Pillar=SeamLight=LampLight=0;SpillHex=FlashHex=0;SpillRange=FlashRange=0;
            GlowCount=StreakCount=PuffCount=CartridgeCount=0;for(int i=0;i<4;i++){LampHex[i]=0;LampGain[i]=0;}
        }
        public void Glow(float x,float y,float z,float size,int hex,float gain,bool bloom=false)
        {if(GlowCount>=MaxGlows||!(gain>1e-3f)||!(size>0))return;Glows[GlowCount++]=new NativeCacheGlow{X=x,Y=y,Z=z,Size=size,Hex=hex,Gain=gain,Bloom=bloom};}
        public void Streak(float hx,float hy,float hz,float tx,float ty,float tz,float width,int hex,float gain,float taper)
        {if(StreakCount>=MaxStreaks||!(gain>1e-3f))return;Streaks[StreakCount++]=new NativeCacheStreak{X=hx,Y=hy,Z=hz,Ax=hx-tx,Ay=hy-ty,Az=hz-tz,Width=width,Hex=hex,Gain=gain,Taper=taper};}
        public void Puff(float x,float y,float z,float size,int hex,float opacity,float u,int seed,bool vapour,float glow)
        {if(PuffCount>=MaxPuffs||!(opacity>1e-3f))return;Puffs[PuffCount++]=new NativeCachePuff{X=x,Y=y,Z=z,Size=size,Hex=hex,Opacity=opacity,U=u,Seed=seed,Vapour=vapour,Glow=glow};}
    }

    /// <summary>
    /// The Civil Defence cache's opening by rarity (Claude, 2026-11-19; the board 3 design, F:/Chip Arena Development/scratch/
    /// meridian/board3/work/fx/cache-openings.md, with the owner's three decisions on it: STANDARD opens with a glow and particles
    /// in its colour and no column; Reduced effects shows only the lid; the chip machine plays the same opening by its chip's
    /// rarity). A port of the design's openings.py: a pure function of the time since the open moment, the tier and a seed from
    /// the cache's id and generation (mulberry32, as NativeBurstPool), so the host, the guests and the killcam draw the same
    /// frames. Engine-free; presentation only: it reads replicated data and decides no rule.
    /// <para>R9's beats for every tier: the latches pop at 0.1 s, the lid opens at 0.25 s with light leaking from its gap, the
    /// buyer's three cartridges rise to chest height and fan out over 0.45-0.85 s (only the buyer sees them), dock on the
    /// buyer's pilot over 1.6-2.0 s, and at 2.2 s the lid shuts and the lamp faces turn red.</para>
    /// <para>The design's rules: no rings and nothing drawn on the floor (every spark rises and dies 0.3 m or more over it); no
    /// hazard look (no shockwave, camera shake or bolts); every light pulses 3 times a second or slower (LEGENDARY's strobe runs
    /// on its four small lamp faces only); every bloom is 0.45 m across or less, at the tray's rim, gone by 0.45 s.</para>
    /// </summary>
    public static class NativeCacheOpenFx
    {
        // ---- R9's beats and the design's limits ----
        public const float LatchAt=.10f,LidAt=.25f,ShutAt=2.2f,ShutLength=.18f,FanFrom=.45f,FanTo=.85f,DockFrom=1.6f,DockTo=2.0f;
        public const float LidMax=105,GlowMax=.45f,GlowTop=.5f,GlowEnd=.45f,SparkFloor=.3f,PulseHzMax=3,ColumnZ=.05f,ChestHeight=1.35f;
        /// <summary>For sale: N pulses (the lamps' count) every 2.4 s, 0.35 s apart, 0.16 s each (2.9 Hz at the fastest).</summary>
        public const float SaleCycle=2.4f,SaleStep=.35f,SaleOn=.16f;
        /// <summary>The buyer within 3 m: the column's foot below 2.2 m at 35 %, so the cartridges read in front of it.</summary>
        public const float BuyerNear=3,BuyerDimHeight=2.2f,BuyerDimShare=.35f;
        /// <summary>LEGENDARY's lamp faces (faces only: never a light) strobe at 16 Hz from 0.12 to 0.55 s.</summary>
        public const float FaceStrobe=16;
        // ---- the locker's marks (gates-caches pass 2's cd647_locker.py; NativeCacheLockerModels) ----
        public const float Body1=.45f,LidTop=.585f,MouthY=.47f,LipZ=-.29f,LampY=.406f,LampZ=-.288f,TrayY=Body1-.07f;
        public static readonly float[] Slots={-.24f,0,.24f};
        public static readonly float[] LatchX={-.33f,.33f};public const float LatchY=.41f,LatchZ=-.30f;
        /// <summary>The opening's length (purchase to last light) and its column's head (m) per tier: a glow in the niche, over the
        /// door heads, over a two-storey row's eaves, over the roofs.</summary>
        public static readonly float[] Length={1.2f,1.45f,1.7f,2.0f},Height={1.0f,4.0f,9.5f,40f};
        /// <summary>The lid's rise per tier (s): STANDARD lifts, RARE swings, EPIC is kicked, LEGENDARY flung.</summary>
        public static readonly float[] Rise={.42f,.22f,.12f,.06f};
        /// <summary>The whole opening, the shut included (the lid is home by then).</summary>
        public const float End=ShutAt+ShutLength;

        // ---- small maths (the design's) ----
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float e0,float e1,float x){float t=Clamp01((x-e0)/(e1-e0));return t*t*(3-2*t);}
        public static float EaseOut(float t,float p=3){t=Clamp01(t);return 1-(float)Math.Pow(1-t,p);}
        /// <summary>0 before a, eased up to 1 at peak, eased down to 0 at b.</summary>
        public static float Env(float t,float a,float peak,float b,float rise=1,float fall=1)
        {
            if(t<=a||t>=b)return 0;if(t<peak)return (float)Math.Pow(Smooth(0,1,(t-a)/Math.Max(1e-6f,peak-a)),rise);
            return (float)Math.Pow(1-Smooth(0,1,(t-peak)/Math.Max(1e-6f,b-peak)),fall);
        }
        /// <summary>The muzzle light's pulse: a fast attack and an exponential decay, ending at 6 decays.</summary>
        public static float Pulse(float t,float t0,float attack,float decay)
        {float a=t-t0;if(a<0||a>attack+6*decay)return 0;if(a<attack)return a/attack;return (float)Math.Exp(-(a-attack)/decay);}
        public static int Mix(int a,int b,float t)
        {
            t=Clamp01(t);int Ch(int s)=>(int)Math.Round(((a>>s)&255)+(((b>>s)&255)-((a>>s)&255))*t);
            return (Ch(16)<<16)|(Ch(8)<<8)|Ch(0);
        }
        /// <summary>A locker-frame point turned by the anchor's yaw (degrees about +Y, 0 faces +Z): the locker's front (-z) faces
        /// the anchor's facing. Returns the world offset (x, z) from the anchor.</summary>
        public static void ToWorld(float yawDegrees,float x,float z,out float wx,out float wz)
        {
            double a=(yawDegrees+180)*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);wx=x*c+z*s;wz=-x*s+z*c;
        }
        /// <summary>A world offset from the anchor in the locker's frame (the inverse of ToWorld).</summary>
        public static void ToLocal(float yawDegrees,float wx,float wz,out float x,out float z)
        {
            double a=(yawDegrees+180)*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);x=wx*c-wz*s;z=wx*s+wz*c;
        }
        /// <summary>The seed of a cache's opening: its id and the generation it opened (FNV-1a), so every peer agrees.</summary>
        public static uint Seed(string id,string generation)
        {
            uint h=2166136261;if(id!=null)foreach(char ch in id)h=(h^ch)*16777619;h=(h^':')*16777619;if(generation!=null)foreach(char ch in generation)h=(h^ch)*16777619;return h;
        }

        // ---- the openings, built once per tier, seed and Reduced effects ----
        private static readonly Dictionary<(int,uint,bool),NativeCacheOpening> built=new Dictionary<(int,uint,bool),NativeCacheOpening>();
        /// <summary>A tier's opening for a seed (built once; bounded: cleared past 64 openings).</summary>
        public static NativeCacheOpening Opening(NativeCacheTier tier,uint seed,bool reduced)
        {
            var key=(NativeCacheTiers.Index(tier),seed,reduced);if(built.TryGetValue(key,out var o))return o;
            if(built.Count>64)built.Clear();o=new NativeCacheOpening(tier,seed,reduced);built[key]=o;return o;
        }

        /// <summary>The cache's sounds (cache.* keys; the clips are requests, so each plays an existing sound until the owner's
        /// recordings land): when, its key, today's fallback clip, gain and rate. None is named for a hazard.</summary>
        public struct Cue{public float At,Gain,Rate;public string Key,Fallback;}
        private static readonly Cue[][] cues=
        {
            new[]{C(LatchAt,"cache.latch","ui",.35f,1.2f),C(LidAt,"cache.lid.open","metal",.22f,1.15f),C(.30f,"cache.open.standard","install",.5f,1.1f),C(ShutAt,"cache.lid.shut","wood",.35f,1.3f)},
            new[]{C(LatchAt,"cache.latch","ui",.4f,1.15f),C(LidAt,"cache.lid.open","metal",.28f,1.1f),C(.30f,"cache.open.rare","install",.6f,1.25f),C(ShutAt,"cache.lid.shut","wood",.35f,1.3f)},
            new[]{C(0,"cache.surge","shield",.35f,.8f),C(LatchAt,"cache.latch","ui",.45f,1.1f),C(LidAt,"cache.lid.kick","metal",.38f,.95f),C(LidAt,"cache.open.epic","win",.45f,1.15f),C(ShutAt,"cache.lid.shut","wood",.35f,1.3f)},
            new[]{C(0,"cache.strobe","countdown",.3f,1.4f),C(LatchAt,"cache.latch","ui",.5f,1),C(LidAt,"cache.lid.fling","metal",.45f,.85f),C(.28f,"cache.open.legendary","win",.65f,1),C(ShutAt,"cache.lid.shut","wood",.35f,1.3f)},
        };
        private static readonly Cue[] reducedCues={C(LatchAt,"cache.latch","ui",.35f,1.2f),C(LidAt,"cache.lid.open","metal",.22f,1.15f),C(ShutAt,"cache.lid.shut","wood",.35f,1.3f)};
        private static Cue C(float at,string key,string fallback,float gain,float rate)=>new Cue{At=at,Key=key,Fallback=fallback,Gain=gain,Rate=rate};
        public static Cue[] Cues(NativeCacheTier tier,bool reduced=false)=>reduced?reducedCues:cues[NativeCacheTiers.Index(tier)];
        /// <summary>The restock's click (R9: "at restock, a click"), and the cache.* keys the presentation plays (the requests).</summary>
        public static readonly Cue Restock=C(0,"cache.restock","ui",.3f,1.5f);
        public static readonly string[] Keys={"cache.latch","cache.lid.open","cache.lid.kick","cache.lid.fling","cache.lid.shut","cache.open.standard","cache.open.rare","cache.open.epic","cache.open.legendary","cache.surge","cache.seams","cache.strobe","cache.embers","cache.restock"};

        // ---- for sale (R9: the lamps blink the tier's count, a thin column of motes pulses in step, dim when you are short) ----
        /// <summary>For sale at the sale clock `t` (s; any origin): is the lamps' pulse lit.</summary>
        public static bool SaleLit(NativeCacheTier tier,float t)
        {float ph=Mod(t,SaleCycle);int k=(int)(ph/SaleStep);return k<NativeCacheTiers.Lamps(tier)&&ph-k*SaleStep<SaleOn;}
        /// <summary>The for-sale pulse as a smooth 0..1 (the column's gain in step with the lamps).</summary>
        public static float SaleLevel(NativeCacheTier tier,float t)
        {
            float ph=Mod(t,SaleCycle),best=0;
            for(int k=0;k<NativeCacheTiers.Lamps(tier);k++)best=Math.Max(best,Env(ph,k*SaleStep-.02f,k*SaleStep+.05f,k*SaleStep+SaleOn+.06f));
            return best;
        }
        private static float Mod(float t,float m){float r=t%m;return r<0?r+m:r;}
        /// <summary>
        /// The for-sale frame at the sale clock `t`: the lid shut, the lamps blinking the tier's count (steady with Reduced
        /// effects), the LED strip, and a thin column of 18 motes over a faint thread pulsing with them (Reduced: the thread only,
        /// still). `dim`: the viewer is short of money (the whole read at 35 %). Nothing on the floor.
        /// </summary>
        public static void ForSale(NativeCacheFrame f,NativeCacheTier tier,float t,bool reduced,bool dim)
        {
            f.Clear();int hex=NativeCacheTiers.Hex(tier),coreHex=NativeCacheTiers.CoreHex(tier),n=NativeCacheTiers.Lamps(tier);float k=dim?.35f:1;
            f.Lamps=n;bool on=!reduced&&SaleLit(tier,t);float lv=reduced?.4f:SaleLevel(tier,t);
            for(int i=0;i<n;i++){f.LampHex[i]=hex;f.LampGain[i]=(reduced?1f:on?1.7f:.3f)*k;}
            f.StripHex=hex;f.StripGain=.6f*k;f.Seams=k;f.Inner=0;f.LampLight=(.25f+.9f*lv)*k;
            for(int i=0;i<n;i++)if(f.LampGain[i]>.5f)f.Glow(LampX(tier,i),LampY,LampZ-.02f,.16f+.04f*f.LampGain[i],hex,(f.LampGain[i]-.5f)*.9f);
            // the thread under the motes (faint: a column of motes, not a tube); LEGENDARY's has a core
            float og=(.05f+.12f*lv)*k;f.Streak(0,LidTop+.02f,-.02f,0,LidTop+2.6f,-.02f,.05f,hex,og*2.4f,.6f);
            if(tier==NativeCacheTier.Legendary)f.Streak(0,LidTop+.02f,-.02f,0,LidTop+2.6f,-.02f,.022f,coreHex,(.15f+.3f*lv)*k*2.4f,.6f);
            if(reduced)return;
            for(int i=0;i<18;i++)
            {
                float ph=i*.618f%1,y=LidTop+.05f+Mod(t*.9f/2.5f+ph,1)*2.5f,a=i*2.4f+t*1.3f;
                float fade=Smooth(LidTop,LidTop+.3f,y)*(1-Smooth(LidTop+1.9f,LidTop+2.55f,y));
                f.Glow((float)Math.Cos(a)*.07f,y,(float)Math.Sin(a)*.07f-.02f,.062f,hex,(2.4f+3*lv)*fade*k);
            }
        }
        /// <summary>The lamps' x across the front (0.09 m apart, centred).</summary>
        public static float LampX(NativeCacheTier tier,int i){int n=NativeCacheTiers.Lamps(tier);return (i-(n-1)*.5f)*.09f;}
        /// <summary>The held state after the shut, while it restocks (and for a late joiner's cache already opened): the lid
        /// home, the lamp faces red (their light off: no red on the pavement), the strip red.</summary>
        public static void Shut(NativeCacheFrame f,NativeCacheTier tier)
        {
            f.Clear();int n=NativeCacheTiers.Lamps(tier);f.Lamps=n;for(int i=0;i<n;i++){f.LampHex[i]=NativeCacheTiers.Red;f.LampGain[i]=1.5f;}
            f.StripHex=NativeCacheTiers.Red;f.StripGain=.7f;f.Seams=1;
            for(int i=0;i<n;i++)f.Glow(LampX(tier,i),LampY,LampZ-.02f,.22f,NativeCacheTiers.Red,.9f);
        }
    }

    /// <summary>One tier's opening for one seed (NativeCacheOpenFx.Opening): its particles built once, then Frame(t).</summary>
    public sealed class NativeCacheOpening
    {
        private enum Role:byte{Mote,Core,Flash,Spark,Ember,Smoke,Vapour}
        private struct Part{public Role Role;public float T0,Life,Px,Py,Pz,Vx,Vy,Vz,S0,S1,Gain,Gravity,Drag,Floor,Opacity,Twinkle;public int C0,C1,Seed;}
        private struct Blade{public float Bx,By,Bz,Dx,Dy,Dz,L,W0,W1,Gain,T0,Peak,T1,Grow;public int Hex;}
        public readonly NativeCacheTier Tier;public readonly uint SeedValue;public readonly bool Reduced;
        public readonly int Hex,CoreHex;public readonly float Length,Height;
        private readonly Part[] parts;private readonly Blade[] blades;
        private static readonly float[][][] surge=SurgePaths(),laps=LapPaths();
        private static readonly float[] LatchSnap={.08f,.05f,.04f,.03f},InnerGain={1,1.4f,1.8f,2.2f},SpillReach={3.5f,5,6,8},FlashReach={3.5f,5.5f,6.5f,9};

        public NativeCacheOpening(NativeCacheTier tier,uint seed,bool reduced)
        {
            Tier=tier;SeedValue=seed;Reduced=reduced;int i=NativeCacheTiers.Index(tier);Hex=NativeCacheTiers.Hex(tier);CoreHex=NativeCacheTiers.CoreHex(tier);
            Length=NativeCacheOpenFx.Length[i];Height=NativeCacheOpenFx.Height[i];
            var p=new List<Part>(220);var b=new List<Blade>(20);
            // Reduced effects (owner, 2026-11-19: "only the lid opening"): no particles, blades, column, flash or strobe.
            if(!reduced)
            {
                switch(tier)
                {
                    case NativeCacheTier.Standard:BuildStandard(p);break;
                    case NativeCacheTier.Rare:BuildRare(p);break;
                    case NativeCacheTier.Epic:BuildEpic(p);break;
                    default:BuildLegendary(p,b);break;
                }
                GapBlades(b);
            }
            p.Sort((x,y)=>x.T0.CompareTo(y.T0));parts=p.ToArray();blades=b.ToArray();
        }
        /// <summary>The lowest any glint, ember or mote of this opening comes to the floor over its life (checks: every one rises
        /// and dies in the air, 0.3 m or more over the floor).</summary>
        public float LowestGlint()
        {
            float low=float.MaxValue;
            foreach(var p in parts)
            {
                if(p.Role!=Role.Spark&&p.Role!=Role.Ember&&p.Role!=Role.Mote)continue;
                for(float a=0;a<p.Life;a+=1/120f)
                {
                    float k=(float)Math.Exp(-p.Drag*a),fd=p.Drag>1e-4f?(1-k)/p.Drag:a,y;
                    if(p.Drag>1e-4f){float g=p.Gravity/p.Drag;y=p.Py+(p.Vy+g)*fd-g*a;}else y=p.Py+p.Vy*a-.5f*p.Gravity*a*a;
                    low=Math.Min(low,y);
                }
            }
            return low;
        }
        /// <summary>Particles and blades this opening holds (checks).</summary>
        public int ParticleCount=>parts.Length;public int BladeCount=>blades.Length;

        // ---- the seeded random (the design's Rnd: mulberry32 of an FNV-1a hash of its parts) ----
        private struct Rnd
        {
            private uint s;
            public Rnd(NativeCacheTier tier,string name,uint seed)
            {
                uint h=2166136261;foreach(char ch in NativeCacheTiers.Name(tier))h=(h^ch)*16777619;foreach(char ch in name)h=(h^ch)*16777619;
                for(int k=0;k<4;k++)h=(h^((seed>>(8*k))&255))*16777619;s=h;
            }
            public float U(){s+=0x6D2B79F5;uint a=s;uint t=(a^(a>>15))*(1|a);t=(t+((t^(t>>7))*(61|t)))^t;return ((t^(t>>14))&0xFFFFFFFF)/4294967296f;}
            public float U(float a,float b)=>a+(b-a)*U();
        }

        // ---- the emitters (the design's, in the locker frame) ----
        private void Dust(List<Part> o,int n,float t0,float t1,float end)
        {
            var r=new Rnd(Tier,"dust",SeedValue);
            for(int i=0;i<n;i++)
            {
                float x,y,z,t;if(i<2){x=NativeCacheOpenFx.LatchX[i]+r.U(-.03f,.03f);y=.43f;z=-.31f;t=t0+r.U(0,.03f);}
                else{x=r.U(-.4f,.4f);y=NativeCacheOpenFx.Body1+r.U(-.01f,.03f);z=NativeCacheOpenFx.LipZ-r.U(0,.02f);t=t0+(t1-t0)*(i-2)/Math.Max(1,n-3)+r.U(0,.02f);}
                float life=Math.Min(r.U(.7f,.85f),end-t);float vx=r.U(-.22f,.22f)+x*.4f,vy=r.U(.1f,.3f),vz=-r.U(.2f,.45f);
                o.Add(new Part{Role=Role.Smoke,T0=t,Life=life,Px=x,Py=y,Pz=z,Vx=vx,Vy=vy,Vz=vz,S0=r.U(.06f,.09f),S1=r.U(.18f,.24f),C0=0x9a9286,C1=0x6a655e,Gravity=.25f,Drag=2.2f,Floor=.02f,Opacity=.22f,Seed=i});
            }
        }
        private void Vapour(List<Part> o,int n,float t0,float t1,float s0,float s1,float pour,float z0,float z1,float l0,float l1,float opacity,int albedo,bool around)
        {
            var r=new Rnd(Tier,"vapour",SeedValue);
            for(int i=0;i<n;i++)
            {
                float t=t0+(t1-t0)*i/Math.Max(1,n-1)+r.U(0,.02f),x,z,vx,vy,vz;
                if(around)
                {
                    double a=2*Math.PI*(i+r.U(-.3f,.3f))/n;float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);if(sa>.35f)sa=-Math.Abs(sa);
                    x=ca*.42f;z=sa*.27f-.02f;vx=ca*r.U(s0,s1);vy=r.U(.15f,.35f);vz=sa*r.U(s0,s1);
                }
                else{x=r.U(-.4f,.4f);z=NativeCacheOpenFx.LipZ-.01f;vx=r.U(-.15f,.15f)+x*.3f;vy=r.U(-.05f,.2f);vz=-r.U(s0,s1);}
                o.Add(new Part{Role=Role.Vapour,T0=t,Life=r.U(l0,l1),Px=x,Py=NativeCacheOpenFx.Body1+r.U(0,.04f),Pz=z,Vx=vx,Vy=vy,Vz=vz,S0=z0*r.U(.85f,1.15f),S1=z1*r.U(.85f,1.15f),
                    C0=albedo,C1=albedo,Gravity=pour,Drag=2,Floor=.03f,Opacity=opacity,Seed=i});
            }
        }
        private void Motes(List<Part> o,string name,uint seed,int n,float t0,float t1,float v0,float v1,float swirl,float l0,float l1,float size,int c0,int c1,float gain,float drag,float sx,float sz,float end,float z0)
        {
            var r=new Rnd(Tier,name,seed);
            for(int i=0;i<n;i++)
            {
                float t=t0+(t1-t0)*i/Math.Max(1,n-1)+r.U(-.02f,.02f),x=r.U(-sx,sx),z=r.U(-sz,sz)+z0;double ang=Math.Atan2(z-z0,x)+Math.PI/2;float sw=r.U(.5f,1)*swirl;
                float vx=(float)Math.Cos(ang)*sw,vy=r.U(v0,v1),vz=(float)Math.Sin(ang)*sw,life=Math.Min(r.U(l0,l1),end-t);if(life<=.05f)continue;
                o.Add(new Part{Role=Role.Mote,T0=t,Life=life,Px=x,Py=.5f+r.U(0,.05f),Pz=z,Vx=vx,Vy=vy,Vz=vz,S0=size*r.U(.8f,1.2f),S1=size*r.U(.6f,.9f),C0=c0,C1=c1,Gain=gain*r.U(.75f,1.15f),Drag=drag,Twinkle=1,Seed=i});
            }
        }
        // Rising glints (the game's Sparks with a light gravity and a short life): each rises, slows and dies in the air, thrown
        // toward the street only (never into the niche's back).
        private void Sparks(List<Part> o,string name,uint seed,int n,float t0,float cx,float cy,float cz,float s0,float s1,float u0,float u1,float l0,float l1,int c1,float gain,float h0,float h1,float spread,float end,float size)
        {
            var r=new Rnd(Tier,name,seed);
            for(int i=0;i<n;i++)
            {
                float t=t0+r.U(0,.03f);double a=2*Math.PI*(i+r.U(-.5f,.5f))/n;float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);if(sa>0)sa=-sa;
                float h=r.U(h0,h1),s=r.U(s0,s1);float px=cx+r.U(-spread,spread)*4,pz=cz+r.U(-spread,spread)*2.5f;
                float vx=ca*s*(1-h),vy=s*h+r.U(u0,u1),vz=sa*s*(1-h),life=Math.Min(r.U(l0,l1),end-t);
                o.Add(new Part{Role=Role.Spark,T0=t,Life=life,Px=px,Py=cy,Pz=pz,Vx=vx,Vy=vy,Vz=vz,S0=size,S1=size*.6f,C0=0xffffff,C1=c1,Gain=gain*r.U(.7f,1.2f),Gravity=3.2f,Drag=1.8f,Floor=0,Seed=i});
            }
        }
        private static Part Bloom(Role role,float t0,float x,float y,float z,float size,float life,int c0,int c1,float gain)
            =>new Part{Role=role,T0=t0,Life=life,Px=x,Py=y,Pz=z,S0=role==Role.Flash?size*.55f:size,S1=size,C0=c0,C1=c1,Gain=gain};
        private void Embers(List<Part> o,int n,float t0,float t1)
        {
            var r=new Rnd(Tier,"embers",SeedValue);
            for(int i=0;i<n;i++)
            {
                float t=t0+(t1-t0)*(float)Math.Pow(r.U(),.8),y=3+21*(float)Math.Pow(r.U(),1.4),a=r.U(0,(float)(2*Math.PI)),rr=r.U(.2f,.6f),so=r.U(.5f,1.5f);
                float life=Math.Min(r.U(.8f,1.15f),2-t);if(life<.25f)continue;
                o.Add(new Part{Role=Role.Ember,T0=t,Life=life,Px=(float)Math.Cos(a)*rr,Py=y,Pz=(float)Math.Sin(a)*rr-.02f+NativeCacheOpenFx.ColumnZ,Vx=(float)Math.Cos(a)*so,Vy=-r.U(.8f,2),Vz=(float)Math.Sin(a)*so,
                    S0=r.U(.12f,.17f),S1=r.U(.07f,.09f),C0=0xffe6b0,C1=0xff7a2a,Gain=r.U(6,9),Gravity=1.6f,Drag=.6f,Twinkle=1,Seed=i});
            }
        }
        private void LatchSparks(List<Part> o,int n,float s0,float s1,float u0,float u1,float l0,float l1,float gain,float size)
        {
            for(int k=0;k<2;k++)Sparks(o,"latch-sparks"+k,SeedValue+(uint)k,n,NativeCacheOpenFx.LatchAt,NativeCacheOpenFx.LatchX[k],NativeCacheOpenFx.LatchY+.02f,NativeCacheOpenFx.LatchZ-.01f,s0,s1,u0,u1,l0,l1,Hex,gain,.65f,.9f,.01f,9,size);
        }

        // ---- the tiers ----
        // STANDARD (owner, 2026-11-19: "a glow with particles based on the rarity", no column): the lid lifts, light leaks from its
        // gap, the tray glows, the lid's dust and a little cold vapour, and motes and glints rise in its colour.
        private void BuildStandard(List<Part> p)
        {
            Dust(p,6,.12f,.30f,1.2f);
            Vapour(p,3,.30f,.40f,.05f,.12f,-.25f,.1f,.22f,.55f,.7f,.11f,0xeef2f6,false);
            Motes(p,"motes",SeedValue,12,.32f,.7f,.5f,.95f,.15f,.55f,.85f,.045f,0xffffff,Hex,2.8f,1.2f,.32f,.2f,1.2f,NativeCacheOpenFx.ColumnZ);
            Sparks(p,"fountain",SeedValue,8,.27f,0,.52f,-.02f,1.4f,2.2f,.3f,.7f,.35f,.45f,Hex,3.5f,.7f,.92f,.06f,1.0f,.018f);
            p.Add(Bloom(Role.Core,.27f,0,.47f,0,.36f,.18f,0xf1f5f9,0xf1f5f9,1.2f));
        }
        private void BuildRare(List<Part> p)
        {
            LatchSparks(p,3,1.2f,2,.4f,.9f,.18f,.26f,4.5f,.016f);
            Sparks(p,"fountain",SeedValue,16,.27f,0,.52f,-.02f,2.2f,3.4f,.3f,.8f,.4f,.55f,Hex,5.5f,.7f,.92f,.08f,1.0f,.022f);
            Vapour(p,8,.26f,.40f,.3f,.55f,1.2f,.1f,.24f,.7f,.9f,.13f,0xc8d8e8,false);
            Motes(p,"glints",SeedValue,10,.4f,1.1f,.8f,1.4f,.3f,.5f,.8f,.045f,0xeaf6ff,Hex,3,.9f,.32f,.2f,1.45f,NativeCacheOpenFx.ColumnZ);
            p.Add(Bloom(Role.Core,.26f,0,.48f,0,.40f,.19f,NativeCacheOpenFx.Mix(Hex,CoreHex,.5f),NativeCacheOpenFx.Mix(Hex,CoreHex,.5f),1.6f));
        }
        private void BuildEpic(List<Part> p)
        {
            LatchSparks(p,4,1.4f,2.4f,.4f,1,.2f,.28f,5,.016f);
            p.Add(Bloom(Role.Flash,.25f,0,.49f,-.02f,.42f,.16f,0xffffff,Hex,3.2f));
            Sparks(p,"kick-sparks",SeedValue,22,.25f,0,.52f,-.02f,2.6f,4.2f,.3f,.9f,.4f,.55f,Hex,6,.7f,.92f,.08f,1.2f,.022f);
            Vapour(p,10,.25f,.32f,.7f,1.2f,.2f,.1f,.24f,.6f,.8f,.13f,0xd8d0ec,true);
            Motes(p,"column-motes",SeedValue,20,.36f,1.1f,4.5f,7,.2f,.6f,1,.06f,0xf4e8ff,Hex,4.2f,.3f,.12f,.08f,1.7f,NativeCacheOpenFx.ColumnZ);
            p.Add(Bloom(Role.Core,.26f,0,.48f,0,.42f,.19f,Hex,Hex,1.7f));
        }
        private void BuildLegendary(List<Part> p,List<Blade> b)
        {
            LatchSparks(p,8,1.6f,3,.5f,1.2f,.22f,.34f,5.5f,.018f);
            for(int k=0;k<2;k++)p.Add(Bloom(Role.Flash,.10f,NativeCacheOpenFx.LatchX[k],NativeCacheOpenFx.LatchY+.03f,NativeCacheOpenFx.LatchZ-.03f,.24f,.1f,0xffffff,Hex,3.6f));
            p.Add(Bloom(Role.Flash,.25f,0,.49f,-.02f,.45f,.18f,0xfff4dc,Hex,3.4f));
            Sparks(p,"glint-fountain",SeedValue,36,.25f,0,.52f,-.02f,3.4f,6,.4f,1,.5f,.7f,Hex,7,.72f,.94f,.1f,1.2f,.024f);
            Vapour(p,12,.25f,.30f,1.1f,1.8f,.1f,.1f,.26f,.6f,.8f,.14f,0xede4d2,true);
            Motes(p,"pillar-motes",SeedValue,30,.30f,1.30f,5,9,.25f,.9f,1.3f,.07f,0xfff4d6,0xffc266,5,.3f,.18f,.12f,2,NativeCacheOpenFx.ColumnZ);
            Embers(p,60,.50f,1.2f);
            Motes(p,"glitter",SeedValue+1,12,.60f,1.55f,.3f,.7f,.4f,.35f,.45f,.045f,0xffffff,0xffe6b0,3.2f,1,.32f,.2f,2,NativeCacheOpenFx.ColumnZ);
            p.Add(Bloom(Role.Core,.26f,0,.48f,0,.45f,.19f,Hex,Hex,1.6f));
            // the crown of seven rays fanned up and out over the street side (205-335 degrees), never into the niche or the floor
            var r=new Rnd(Tier,"rays",SeedValue);int hexc=NativeCacheOpenFx.Mix(Hex,CoreHex,.55f);
            for(int k=0;k<7;k++)
            {
                double a=(205+130*k/6.0)*Math.PI/180,e=(58+r.U(-6,6))*Math.PI/180;
                b.Add(new Blade{Bx=0,By=.62f,Bz=NativeCacheOpenFx.ColumnZ*.5f,Dx=(float)(Math.Cos(a)*Math.Cos(e)),Dy=(float)Math.Sin(e),Dz=(float)(Math.Sin(a)*Math.Cos(e)),L=3*r.U(.85f,1.1f),W0=.05f,W1=.34f,Hex=hexc,Gain=1.5f*r.U(.85f,1.1f),T0=.30f,Peak=.46f,T1=1.3f,Grow=.2f});
            }
        }
        // Light leaking from the lid's gap as it lifts: blades fanned out of the front lip (and, from EPIC, the sides).
        private void GapBlades(List<Blade> o)
        {
            int ti=NativeCacheTiers.Index(Tier);int n=new[]{3,5,7,9}[ti];float L=new[]{.45f,.75f,.95f,1.15f}[ti],gain=new[]{.7f,1.3f,1.7f,2}[ti];
            var r=new Rnd(Tier,"gap",0);float rise=NativeCacheOpenFx.Rise[ti],t0=NativeCacheOpenFx.LidAt,peak=t0+Math.Max(.04f,rise*.45f),t1=t0+Math.Max(.3f,rise+.18f);int hexc=NativeCacheOpenFx.Mix(Hex,CoreHex,.45f);
            for(int i=0;i<n;i++)
            {
                float u=(i+.5f)/n*2-1;double yaw=u*38*Math.PI/180,el=r.U(24,48)*Math.PI/180;
                o.Add(new Blade{Bx=u*.38f,By=NativeCacheOpenFx.Body1+.012f,Bz=NativeCacheOpenFx.LipZ-.01f,Dx=(float)(Math.Sin(yaw)*Math.Cos(el)),Dy=(float)Math.Sin(el),Dz=(float)(-Math.Cos(yaw)*Math.Cos(el)),
                    L=L*r.U(.8f,1.15f),W0=.02f,W1=.13f,Hex=hexc,Gain=gain*r.U(.75f,1.1f),T0=t0,Peak=peak,T1=t1});
            }
            if(Tier==NativeCacheTier.Epic||Tier==NativeCacheTier.Legendary)
                for(int sx=-1;sx<=1;sx+=2){float l=(float)Math.Sqrt(.64+.36+.0225);o.Add(new Blade{Bx=sx*.46f,By=NativeCacheOpenFx.Body1+.012f,Bz=-.05f,Dx=sx*.8f/l,Dy=.6f/l,Dz=-.15f/l,L=L*.8f,W0=.02f,W1=.12f,Hex=hexc,Gain=gain*.8f,T0=t0,Peak=peak,T1=t1});}
        }

        // ---- the timeline ----
        /// <summary>The lid's angle at t (degrees): the kick by tier and its rebound off the stop, R9's shut at 2.2 s; Reduced
        /// effects a plain 0.32 s swing.</summary>
        public float Lid(float t)
        {
            const float L=NativeCacheOpenFx.LidMax;if(t<NativeCacheOpenFx.LidAt)return 0;
            if(t>=NativeCacheOpenFx.ShutAt)return t<NativeCacheOpenFx.End?L*(1-NativeCacheOpenFx.Smooth(0,1,(t-NativeCacheOpenFx.ShutAt)/NativeCacheOpenFx.ShutLength)):0;
            float a=t-NativeCacheOpenFx.LidAt;if(Reduced)return L*NativeCacheOpenFx.EaseOut(a/.32f,2);
            switch(Tier)
            {
                case NativeCacheTier.Standard:return 100*NativeCacheOpenFx.EaseOut(a/.42f,3);
                case NativeCacheTier.Rare:return Kick(a,.22f,6,9,4,true);
                case NativeCacheTier.Epic:return Kick(a,.12f,14,7.5f,4.5f,false);
                default:return Kick(a,.06f,17,6.5f,5,false);
            }
        }
        private static float Kick(float a,float rise,float amp,float lam,float hz,bool accelerate)
        {
            const float L=NativeCacheOpenFx.LidMax;if(a<rise){float u=a/rise;return L*(accelerate?u*u:NativeCacheOpenFx.EaseOut(u,2));}
            float b=a-rise;return L-amp*(float)Math.Exp(-lam*b)*Math.Abs((float)Math.Sin(2*Math.PI*hz*b));
        }
        public float Latch(float t)
        {
            if(t<NativeCacheOpenFx.LatchAt||t>=NativeCacheOpenFx.ShutAt+.1f)return 0;
            float snap=LatchSnap[NativeCacheTiers.Index(Tier)],u=(t-NativeCacheOpenFx.LatchAt)/snap;
            float over=Tier==NativeCacheTier.Standard||Reduced||u<1?0:6*(float)Math.Exp(-(t-NativeCacheOpenFx.LatchAt-snap)*30);
            return -35*NativeCacheOpenFx.EaseOut(u,2)-over;
        }
        /// <summary>The tray's see-through glow, with the lid.</summary>
        public float Inner(float t){float k=InnerGain[NativeCacheTiers.Index(Tier)];return Math.Min(1,Lid(t)/40)*k*(1-.6f*NativeCacheOpenFx.Smooth(Length-.2f,Length+.1f,t));}
        /// <summary>LEGENDARY's pillar share (its rise, hold and fade).</summary>
        public float PillarShare(float t)=>Tier!=NativeCacheTier.Legendary||t<.28f||Reduced?0:NativeCacheOpenFx.EaseOut((t-.28f)/.22f,3)*(1-NativeCacheOpenFx.Smooth(1.45f,2,t));
        /// <summary>The opening's lights at t (smooth envelopes and single pulses: 3 Hz or slower). None with Reduced effects.</summary>
        public void Lights(float t,out float spill,out float flash,out float pillar,out float seams)
        {
            spill=flash=pillar=seams=0;if(Reduced)return;
            switch(Tier)
            {
                case NativeCacheTier.Standard:spill=1.4f*NativeCacheOpenFx.Env(t,.25f,.42f,1.2f,1,1.4f);break;
                case NativeCacheTier.Rare:spill=1.9f*NativeCacheOpenFx.Env(t,.25f,.32f,.6f)+1.4f*NativeCacheOpenFx.Env(t,.32f,.6f,1.45f);flash=1.6f*NativeCacheOpenFx.Pulse(t,.27f,.02f,.1f);break;
                case NativeCacheTier.Epic:
                    spill=2.4f*NativeCacheOpenFx.Env(t,.2f,.27f,.6f)+1.6f*NativeCacheOpenFx.Env(t,.3f,.6f,1.7f);flash=2.2f*NativeCacheOpenFx.Pulse(t,.25f,.01f,.09f);
                    seams=1.3f*NativeCacheOpenFx.Env(t,0,.3f,1.35f)*(1+.12f*(float)Math.Sin(2*Math.PI*2*t));break;
                default:spill=2.6f*NativeCacheOpenFx.Env(t,.2f,.26f,.7f)+1.6f*NativeCacheOpenFx.Env(t,.3f,.7f,2);flash=3*NativeCacheOpenFx.Pulse(t,.25f,.01f,.12f);pillar=1.8f*PillarShare(t);break;
            }
        }
        /// <summary>The lit seams' gain over their rest (EPIC's surge and slow 2 Hz swell; every other tier 1).</summary>
        public float Seams(float t)
        {
            if(Tier!=NativeCacheTier.Epic||t<0)return 1;if(Reduced)return 1;
            float sw=1+.1f*(float)Math.Sin(2*Math.PI*2*t);
            if(t<NativeCacheOpenFx.LidAt)return 1+2.2f*NativeCacheOpenFx.Smooth(0,.22f,t);
            if(t<1.3f)return (3.2f-.6f*NativeCacheOpenFx.Smooth(NativeCacheOpenFx.LidAt,.5f,t))*sw;
            return 1+1.6f*(1-NativeCacheOpenFx.Smooth(1.3f,1.7f,t));
        }

        /// <summary>
        /// The opening's frame at t seconds from the open moment (t over the shut: the held state). buyer: this client bought it
        /// (the three cartridges, the column's foot dimmed within 3 m: buyerNear), docking toward (dx, dy, dz), the buyer's chest
        /// in the locker frame. hologram: today's hologram body (no lamps, latches or seams to light).
        /// </summary>
        public void Frame(NativeCacheFrame f,float t,bool buyer,bool buyerNear,float dx,float dy,float dz,bool hologram=false)
        {
            if(t>=NativeCacheOpenFx.End){NativeCacheOpenFx.Shut(f,Tier);return;}
            f.Clear();f.Lid=Lid(t);f.Latch=hologram?0:Latch(t);f.Inner=Inner(t);f.Seams=Seams(t);f.StripHex=Hex;f.StripGain=.6f;
            Lights(t,out f.Spill,out f.Flash,out f.Pillar,out f.SeamLight);f.SpillHex=Hex;f.FlashHex=CoreHex;
            int ti=NativeCacheTiers.Index(Tier);f.SpillRange=SpillReach[ti];f.FlashRange=FlashReach[ti];
            Lamps(f,t);
            if(!hologram)for(int i=0;i<f.Lamps;i++)if(f.LampGain[i]>.5f)f.Glow(NativeCacheOpenFx.LampX(Tier,i),NativeCacheOpenFx.LampY,NativeCacheOpenFx.LampZ-.02f,.16f+.04f*f.LampGain[i],f.LampHex[i],(f.LampGain[i]-.5f)*.9f);
            if(!Reduced)
            {
                Particles(f,t);Bladed(f,t);Column(f,t,buyer&&buyerNear);
                if(Tier==NativeCacheTier.Rare)Spiral(f,t);
                if(Tier==NativeCacheTier.Epic&&!hologram)Runners(f,t);
            }
            if(buyer&&!Reduced)Cartridges(f,t,dx,dy,dz);
        }
        // Each lock lamp face: the purchase's white flash (LEGENDARY: the double blink, then the faces strobe in alternating pairs),
        // EPIC's swell with its seams, the hold, then red after the shut. Faces only: the lamps' own light never strobes.
        private void Lamps(NativeCacheFrame f,float t)
        {
            int n=NativeCacheTiers.Lamps(Tier);f.Lamps=n;
            for(int i=0;i<n;i++)
            {
                int hex=Hex;float g;
                if(t>=NativeCacheOpenFx.ShutAt){hex=NativeCacheTiers.Red;g=1.5f*NativeCacheOpenFx.Smooth(NativeCacheOpenFx.ShutAt,NativeCacheOpenFx.ShutAt+.12f,t);}
                else if(Tier==NativeCacheTier.Legendary&&!Reduced&&t<.12f){bool on=t%.06f<.03f;hex=0xffffff;g=on?3:.2f;}
                else if(Tier==NativeCacheTier.Legendary&&!Reduced&&t<.55f){int k=(int)((t-.12f)*NativeCacheOpenFx.FaceStrobe*2);hex=CoreHex;g=i%2==k%2?3:.15f;}
                else if(Tier==NativeCacheTier.Legendary&&!Reduced)g=1.5f;
                else if(Tier==NativeCacheTier.Epic&&!Reduced&&t<NativeCacheOpenFx.LidAt){hex=CoreHex;g=1.2f+1.8f*NativeCacheOpenFx.Smooth(0,.22f,t);}
                else
                {
                    float k=t<.3f&&!Reduced?2.4f*(float)Math.Exp(-t/.06f):0,hold=t<Length?.3f:.3f*(1-NativeCacheOpenFx.Smooth(Length,Length+.2f,t));
                    if(Reduced)hold=1;hex=k>1?0xffffff:Hex;g=Math.Max(hold,k);
                }
                f.LampHex[i]=hex;f.LampGain[i]=g;
            }
            f.LampLight=t>=NativeCacheOpenFx.ShutAt?0:Reduced?.3f:Math.Max(t<Length?.3f:.3f*(1-NativeCacheOpenFx.Smooth(Length,Length+.2f,t)),1.6f*NativeCacheOpenFx.Pulse(t,0,.01f,.08f));
            if(t>=NativeCacheOpenFx.ShutAt){f.StripHex=NativeCacheTiers.Red;f.StripGain=.7f*NativeCacheOpenFx.Smooth(NativeCacheOpenFx.ShutAt,NativeCacheOpenFx.ShutAt+.12f,t);}
        }
        // The particles, each in closed form: linear drag and a light gravity (the blast pool's integration), so a late frame, a
        // late joiner and the killcam see the same particle at the same age.
        private void Particles(NativeCacheFrame f,float t)
        {
            for(int i=0;i<parts.Length;i++)
            {
                ref var p=ref parts[i];if(p.T0>t)break;float a=t-p.T0;if(a<0||a>=p.Life)continue;float u=a/p.Life;
                float k=(float)Math.Exp(-p.Drag*a),fd=p.Drag>1e-4f?(1-k)/p.Drag:a,x=p.Px+p.Vx*fd,z=p.Pz+p.Vz*fd,y,vy;
                if(p.Drag>1e-4f){float g=p.Gravity/p.Drag;y=p.Py+(p.Vy+g)*fd-g*a;vy=(p.Vy+g)*k-g;}else{y=p.Py+p.Vy*a-.5f*p.Gravity*a*a;vy=p.Vy-p.Gravity*a;}
                if(y<p.Floor)y=p.Floor;float vx=p.Vx*k,vz=p.Vz*k;int hex=NativeCacheOpenFx.Mix(p.C0,p.C1,u);float size=p.S0+(p.S1-p.S0)*u;
                float tw=p.Twinkle>0?.55f+.45f*(float)Math.Sin(t*(17+p.Seed%7*3.1f)+p.Seed*1.7f):1;
                switch(p.Role)
                {
                    case Role.Mote:f.Glow(x,y,z,size,hex,p.Gain*NativeCacheOpenFx.Smooth(0,.12f,u)*(float)Math.Pow(1-u,1.1)*tw);break;
                    case Role.Core:f.Glow(x,y,z,size,hex,p.Gain*NativeCacheOpenFx.Smooth(0,.12f,u)*(1-NativeCacheOpenFx.Smooth(.35f,1,u)),true);break;
                    case Role.Flash:f.Glow(x,y,z,p.S0+(p.S1-p.S0)*(float)Math.Sqrt(u),hex,p.Gain*(float)Math.Pow(1-u,.8),true);break;
                    case Role.Spark:
                    {
                        float g=p.Gain*(float)Math.Pow(1-u,1.3),len=(float)Math.Sqrt(vx*vx+vy*vy+vz*vz);float L=Math.Max(.05f,len*.035f);
                        float nx=len>1e-4f?vx/len:0,ny=len>1e-4f?vy/len:1,nz=len>1e-4f?vz/len:0;f.Streak(x,y,z,x-nx*L,y-ny*L,z-nz*L,Math.Max(.012f,size*1.4f),hex,g,.6f);break;
                    }
                    case Role.Ember:
                    {
                        float g=p.Gain*NativeCacheOpenFx.Smooth(0,.1f,u)*(float)Math.Pow(1-u,.7)*(.45f+.55f*tw);f.Glow(x,y,z,size,hex,g);
                        f.Streak(x,y,z,x-vx*.1f,y-vy*.1f,z-vz*.1f,size*.5f,hex,g*.8f,.7f);break;
                    }
                    case Role.Smoke:f.Puff(x,y,z,p.S0+(p.S1-p.S0)*NativeCacheOpenFx.EaseOut(u,2),hex,p.Opacity*(float)Math.Sqrt(1-u),u,p.Seed+(int)(SeedValue&1023),false,0);break;
                    default:f.Puff(x,y,z,p.S0+(p.S1-p.S0)*NativeCacheOpenFx.EaseOut(u,2),p.C0,p.Opacity*(float)Math.Sqrt(1-u),u,p.Seed+(int)(SeedValue&1023),true,.12f*NativeCacheOpenFx.Clamp01(f.Spill));break;
                }
            }
        }
        private void Bladed(NativeCacheFrame f,float t)
        {
            for(int i=0;i<blades.Length;i++)
            {
                ref var b=ref blades[i];float k=NativeCacheOpenFx.Env(t,b.T0,b.Peak,b.T1,1,1.3f);if(k<=1e-3f)continue;
                float L=b.L*(b.Grow>0?NativeCacheOpenFx.EaseOut((t-b.T0)/b.Grow,3):1);
                f.Streak(b.Bx,b.By,b.Bz,b.Bx+b.Dx*L,b.By+b.Dy*L,b.Bz+b.Dz*L,(b.W0+b.W1)*.5f,b.Hex,b.Gain*k,.92f);
            }
        }
        /// <summary>The tier's column at t (an outer light and a core), its foot 5 cm behind the tray's middle: RARE 4 m, EPIC
        /// 9.5 m, LEGENDARY's 40 m pillar. STANDARD has none (owner, 2026-11-19): its low soft glow stays in the niche.</summary>
        private void Column(NativeCacheFrame f,float t,bool dimFoot)
        {
            float og=0,cg=0,top=0,r0=0,r1=0,share=0,fade=.5f;const float B=NativeCacheOpenFx.MouthY,Z=NativeCacheOpenFx.ColumnZ;
            switch(Tier)
            {
                case NativeCacheTier.Standard:
                {
                    // the low soft glow in the niche (no column): a bloom over the tray rising to 1 m, its top fading
                    float k=NativeCacheOpenFx.Env(t,.28f,.45f,1.1f,1,1.3f);if(k<=0)return;
                    float h=B+(Height-B)*NativeCacheOpenFx.EaseOut((t-.28f)/.3f,2);
                    for(int j=0;j<3;j++){float y=B+(h-B)*(j+.5f)/3;f.Glow(0,y,Z,NativeCacheOpenFx.GlowMax*(1-.2f*j),Hex,.35f*k*(1-.25f*j));}
                    return;
                }
                case NativeCacheTier.Rare:{float k=NativeCacheOpenFx.Env(t,.30f,.5f,1.45f,1,1.2f);if(k<=0)return;top=B+(Height-B)*NativeCacheOpenFx.EaseOut((t-.30f)/.3f,2);r0=.16f;r1=.22f;og=.75f*k;cg=k;share=.3f;fade=.5f;break;}
                case NativeCacheTier.Epic:
                {
                    if(t<.30f)return;float rise=NativeCacheOpenFx.EaseOut((t-.30f)/.2f,3),k=1-NativeCacheOpenFx.Smooth(1.15f,1.7f,t),thin=1-.6f*NativeCacheOpenFx.Smooth(1.15f,1.7f,t);if(k<=0)return;
                    top=B+(Height-B)*rise;r0=.22f*thin;r1=.2f*thin;og=1.2f*k;cg=2*k;share=.28f;fade=.3f;break;
                }
                default:
                {
                    float s=PillarShare(t);if(s<=0)return;top=B+(Height-B)*NativeCacheOpenFx.EaseOut((t-.28f)/.22f,3);float thin=1-.75f*NativeCacheOpenFx.Smooth(1.45f,2,t),ck=1-NativeCacheOpenFx.Smooth(1.45f,1.75f,t);
                    r0=.24f*thin;r1=.5f*thin;og=.9f*s;cg=2.2f*ck*s;share=.3f;fade=.65f;break;
                }
            }
            float w=r0+r1;
            // the buyer's own client within 3 m: the foot below 2.2 m at 35 %, so the chips read in front of it
            if(dimFoot&&top>NativeCacheOpenFx.BuyerDimHeight)
            {
                float d=NativeCacheOpenFx.BuyerDimHeight,k=NativeCacheOpenFx.BuyerDimShare;
                f.Streak(0,B,Z,0,d,Z,w,Hex,og*k,0);f.Streak(0,d,Z,0,top,Z,w,Hex,og,fade);
                if(cg>0){f.Streak(0,B,Z,0,d,Z,w*share,CoreHex,cg*k,0);f.Streak(0,d,Z,0,top,Z,w*share,CoreHex,cg,fade);}
                return;
            }
            f.Streak(0,B,Z,0,top,Z,w,Hex,og,fade);if(cg>0)f.Streak(0,B,Z,0,top,Z,w*share,CoreHex,cg,fade);
        }
        // RARE's signature: a double spiral of motes up its 4 m column (closed form).
        private void Spiral(NativeCacheFrame f,float t)
        {
            const float t0=.32f,t1=1.45f,rad=.24f,rise=3.2f,spin=1.25f;if(t<t0||t>t1)return;int hex=NativeCacheOpenFx.Mix(CoreHex,Hex,.3f);
            for(int i=0;i<24;i++)
            {
                float born=t0+i/2*.045f,a=t-born;if(a<0)continue;float y=NativeCacheOpenFx.MouthY+.05f+rise*a;if(y>Height)continue;
                double ang=(i%2)*Math.PI+i*.21+2*Math.PI*spin*a;float rr=rad*(1-.25f*(y/Height));
                float fade=NativeCacheOpenFx.Smooth(NativeCacheOpenFx.MouthY,NativeCacheOpenFx.MouthY+.3f,y)*(1-NativeCacheOpenFx.Smooth(Height*.7f,Height,y))*(1-NativeCacheOpenFx.Smooth(t1-.3f,t1,t));
                f.Glow((float)Math.Cos(ang)*rr,y,(float)Math.Sin(ang)*rr+NativeCacheOpenFx.ColumnZ,.085f,hex,3.8f*fade);
            }
        }
        // EPIC's signature: light racing along the casing's lit seams (out from the ribs to the lip before the kick, then lapping).
        private void Runners(NativeCacheFrame f,float t)
        {
            if(t<0||t>1.32f)return;
            if(t<.3f)foreach(var path in surge){float L=PathLength(path),s=t<.25f?L*NativeCacheOpenFx.EaseOut(t/.25f,2):L,k=t<.25f?1:1-(t-.25f)/.05f;if(s>.02f&&k>0)Run(f,path,Math.Max(0,s-.16f),s,k);}
            if(t>=.35f)for(int i=0;i<laps.Length;i++)
            {
                var path=laps[i];float L=PathLength(path),v=1.6f+.35f*i,s=(v*(t-.35f)+L*i/laps.Length)%L;
                float k=NativeCacheOpenFx.Smooth(.35f,.45f,t)*(1-NativeCacheOpenFx.Smooth(1.15f,1.3f,t))*NativeCacheOpenFx.Smooth(0,.08f,s);if(k>0)Run(f,path,Math.Max(0,s-.2f),s,.85f*k);
            }
        }
        private void Run(NativeCacheFrame f,float[][] path,float s0,float s1,float gain)
        {
            // the stretch of the path from s0 to s1, corner by corner: a hot core and a soft glow, a glint at its head
            float acc=0;float hx=0,hy=0,hz=0;bool any=false;
            for(int i=0;i+1<path.Length;i++)
            {
                var a=path[i];var b=path[i+1];float dx=b[0]-a[0],dy=b[1]-a[1],dz=b[2]-a[2],d=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);if(d<1e-6f)continue;
                float u0=Math.Max(0,(s0-acc)/d),u1=Math.Min(1,(s1-acc)/d);acc+=d;if(u1<=u0)continue;
                float ax=a[0]+dx*u0,ay=a[1]+dy*u0,az=a[2]+dz*u0,bx=a[0]+dx*u1,by=a[1]+dy*u1,bz=a[2]+dz*u1;
                f.Streak(bx,by,bz,ax,ay,az,.013f,0xf8f0ff,14*gain,0);f.Streak(bx,by,bz,ax,ay,az,.055f,Hex,3.2f*gain,0);hx=bx;hy=by;hz=bz;any=true;
            }
            if(any)f.Glow(hx,hy,hz,.085f,Hex,4.5f*gain);
        }
        private static float PathLength(float[][] p){float L=0;for(int i=0;i+1<p.Length;i++){float dx=p[i+1][0]-p[i][0],dy=p[i+1][1]-p[i][1],dz=p[i+1][2]-p[i][2];L+=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);}return L;}
        // The EPIC casing's lit seams (cd647_locker: the ribs' crowns, a seam down each front cap, the lid's foot seam).
        private static float[][][] SurgePaths()
        {
            const float zr=-.2905f,zc=-.2835f,yf=.4365f,xc=.4225f;var o=new List<float[][]>();
            foreach(float y in new[]{.108f,.136f,.164f})for(int sx=-1;sx<=1;sx+=2)
                o.Add(new[]{new[]{0,y,zr},new[]{sx*.39f,y,zr},new[]{sx*xc,y,zc},new[]{sx*xc,.424f,zc},new[]{sx*xc,yf,zr},new[]{sx*.12f,yf,zr}});
            return o.ToArray();
        }
        private static float[][][] LapPaths()
        {
            const float zr=-.2905f,yf=.4365f;
            var foot=new[]{new[]{-.4595f,yf,.275f},new[]{-.4595f,yf,zr},new[]{.4595f,yf,zr},new[]{.4595f,yf,.275f}};
            var ribs=new[]{new[]{-.39f,.108f,zr},new[]{.39f,.108f,zr},new[]{.39f,.136f,zr},new[]{-.39f,.136f,zr},new[]{-.39f,.164f,zr},new[]{.39f,.164f,zr}};
            return new[]{foot,ribs,foot,ribs};
        }
        /// <summary>The buyer's three chips as cartridges (R9: only the buyer sees them): out of the tray's slots at 0.45 s, up to
        /// chest height and fanned by 0.85 s, held with a slow bob, then docking on the buyer's pilot over 1.6-2.0 s, as pending.</summary>
        private static void Cartridges(NativeCacheFrame f,float t,float dx,float dy,float dz)
        {
            if(t<NativeCacheOpenFx.FanFrom||t>2.02f)return;
            for(int k=0;k<3;k++)
            {
                float sx=NativeCacheOpenFx.Slots[k],u=NativeCacheOpenFx.EaseOut((t-NativeCacheOpenFx.FanFrom)/(NativeCacheOpenFx.FanTo-NativeCacheOpenFx.FanFrom),3);
                float x=sx+(sx*1.45f-sx)*u,y=NativeCacheOpenFx.TrayY+.05f+(NativeCacheOpenFx.ChestHeight-NativeCacheOpenFx.TrayY-.05f)*u,z=-.32f*u,roll=-14*(k-1)*u,scale=.55f+.45f*u,alpha=NativeCacheOpenFx.Smooth(.45f,.55f,t);
                if(t>NativeCacheOpenFx.FanTo)y+=(float)Math.Sin((t-NativeCacheOpenFx.FanTo)*4.2f+k*1.7f)*.015f;
                if(t>NativeCacheOpenFx.DockFrom){float d=NativeCacheOpenFx.EaseOut((t-NativeCacheOpenFx.DockFrom)/.38f,2);x+=(dx-x)*d;y+=(dy-y)*d;z+=(dz-z)*d;scale*=1-.7f*d;alpha*=1-NativeCacheOpenFx.Smooth(.75f,1,d);}
                f.Cartridges[f.CartridgeCount++]=new NativeCacheCartridge{X=x,Y=y,Z=z,Roll=roll,Scale=scale,Alpha=alpha};
            }
        }
    }
}
