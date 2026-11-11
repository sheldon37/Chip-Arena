using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blood pack's splashes (owner 2026-11-10: "a new blood pack id like to implement"; docs/BLOOD-PACK.md), presentation
    /// only. A wound that bleeds throws a few lit, fluid splashes where it opens: each one flipbook quad from the pack's
    /// simulated sheets (the local bake's VisualBlood, Runtime NativeBloodPackPresentation), laid from the wound along its
    /// spray, facing the camera. Everything here is a pure function of the replicated wound (kind, severity, point,
    /// direction), its seed, the settings and the presentation clock, so the host, guests and the killcam throw the same
    /// splashes; the droplets, the splats they leave and the rest of Core/NativeBlood are unchanged.
    /// <list type="bullet">
    /// <item>How many (<see cref="Count"/>): a cut a jet along the blade's travel and a puff out of the wound (a deep one a
    /// spray too); a sever a larger set; a shot a puff out of the exit and a small one back out of the entry; a crush a
    /// burst and sprays to the sides; a bruise one small puff. A killing blow adds the pack's large burst. Reduced effects
    /// halve them; the low graphics tier draws one (two on a kill).</item>
    /// <item>Each sheet's spray starts at its pivot (<see cref="PivotU"/>, <see cref="PivotV"/>) and travels along +u: the
    /// quad is laid with its pivot on the wound and +u along the splash's direction as the camera sees it.</item>
    /// <item>A splash plays its sheet once over its life (<see cref="Frame"/>, blended between frames so slow motion stays
    /// smooth), drifts a little along its spray and sags.</item>
    /// </list>
    /// </summary>
    public static class NativeBloodSplash
    {
        public const int Sheets=6,Burst=0,Spray=1,PuffA=2,PuffB=3,JetA=4,JetB=5;
        /// <summary>The bake's entries under VisualBlood/Splash, by sheet.</summary>
        public static readonly string[] Names={"burst","spray","puff-a","puff-b","jet-a","jet-b"};
        /// <summary>Each sheet's grid (square) and the frames it holds.</summary>
        public static readonly int[] Columns={5,4,4,4,4,4},Frames={24,15,15,15,15,15};
        /// <summary>Where each sheet's spray starts in a frame's cell: u from the left, v from the top.</summary>
        public static readonly float[] PivotU={.19f,.15f,.10f,.10f,.12f,.11f},PivotV={.52f,.52f,.55f,.51f,.58f,.57f};
        /// <summary>The splashes' tint (sRGB) over the pack's grey shading; the Siren's ink.</summary>
        public const int Tint=0x7d0a12,InkTint=0x1a1026;
        public const float Sag=1.1f,Drift=.16f,SizeMax=1.6f;
        public static int Count(string kind,float severity,float quality,bool low,bool kill)
        {
            float s=MeleeMath.Clamp(NativeBloodRules.Finite(severity,.5f),0,1);if(!(NativeBloodRules.Finite(quality,1)>0))return 0;int n;
            switch(kind)
            {
                case "sever":n=3;break;case "cut":n=s>.55f?3:2;break;case "cavity":n=3;break;case "puncture":n=2;break;case "crush":n=3;break;
                case "squirt":n=2;break;case "bruise":n=s>.35f?1:0;break;default:return 0;
            }
            if(kill&&n>0)n++;if(low)return Math.Min(n,kill?2:1);if(quality<1)n=(n+1)/2;return n;
        }
        /// <summary>
        /// Splash `i` of a wound (0 first: the one a thinned wound keeps): its sheet, direction (unit, Unity world), size (the
        /// frame cell's side, m), life and launch delay (s) and whether it is mirrored across its travel.
        /// </summary>
        public static void Puff(string kind,uint seed,int i,Vector3 normal,Vector3 along,float severity,bool kill,out int sheet,out Vector3 dir,out float size,out float life,out float delay,out bool flip)
        {
            float H(int k)=>NativeBloodRules.Hash(seed,i,90+k);float R(int k)=>H(k)-.5f;float s=MeleeMath.Clamp(NativeBloodRules.Finite(severity,.5f),0,1);
            normal=Unit(normal,Vector3.UnitY);along=Unit(along,normal);Vector3 side=Unit(Vector3.Cross(along,Vector3.UnitY),Vector3.UnitX),up=Vector3.UnitY;
            bool pick=H(0)<.5f;flip=H(1)<.5f;life=.5f+.16f*H(2);delay=i==0?0:.025f*i+.03f*H(3);
            switch(kind)
            {
                case "puncture":case "cavity":
                {
                    float big=kind=="cavity"?1.3f:1;
                    if(i==0){sheet=pick?PuffA:PuffB;dir=along+up*.15f+side*R(4)*.3f;size=(.5f+.26f*s)*big;}
                    else if(i==1){sheet=pick?JetA:JetB;dir=-along*.8f+normal*.4f+up*.2f+side*R(4)*.3f;size=(.42f+.14f*s)*big;life*=.8f;}
                    else if(i==2){sheet=Spray;dir=along*.7f+up*.5f+side*R(4)*.5f;size=(.5f+.22f*s)*big;}
                    else{sheet=Burst;dir=normal*.4f+along*.4f+up*.8f;size=.8f+.3f*s;life=.62f+.14f*H(2);}
                    break;
                }
                case "crush":
                {
                    if(i==0){sheet=Burst;dir=normal*.6f+up*.8f+side*R(4)*.3f;size=.8f+.3f*s;life=.62f+.14f*H(2);}
                    else if(i<3){sheet=Spray;dir=side*(i==1?1:-1)+up*.4f+normal*.3f+along*R(4)*.5f;size=.5f+.24f*s;}
                    else{sheet=pick?PuffA:PuffB;dir=normal+up*.5f+side*R(4);size=.55f+.2f*s;}
                    break;
                }
                case "squirt":
                {
                    sheet=i==0?JetA:JetB;dir=along+up*(.1f+.2f*H(5))+side*R(4)*(i==0?.2f:.7f);size=.58f+.3f*s;break;
                }
                case "bruise":sheet=pick?PuffA:PuffB;dir=normal+up*.3f+side*R(4)*.4f;size=.22f+.1f*s;life*=.8f;break;
                default:
                {
                    // Cuts and severs: the jet is cast off along the blade's travel, the puff comes out of the wound.
                    float big=kind=="sever"?1.25f:1;
                    if(i==0){sheet=pick?JetA:JetB;dir=along*.8f+normal*.45f+up*.2f+side*R(4)*.2f;size=(.68f+.34f*s)*big;}
                    else if(i==1){sheet=pick?PuffA:PuffB;dir=normal*.9f+up*.35f+along*R(4)*.5f;size=(.58f+.26f*s)*big;}
                    else if(i==2){sheet=Spray;dir=along*.5f+normal*.3f+up*.75f+side*R(4)*.4f;size=(.58f+.26f*s)*big;}
                    else{sheet=Burst;dir=normal*.5f+up*.9f+along*.3f;size=.8f+.3f*s;life=.62f+.14f*H(2);}
                    break;
                }
            }
            if(kill)size*=1.2f;size=Math.Min(size,SizeMax);dir=Unit(dir,normal);
        }
        /// <summary>The two frames a splash shows at `u` of its life (0..1) and the blend from the first to the second: it bursts
        /// out fast and hangs (half its frames in the first third of its life).</summary>
        public static void Frame(int sheet,float u,out int a,out int b,out float blend)
        {
            int last=Frames[sheet]-1;float t=1-MeleeMath.Clamp(NativeBloodRules.Finite(u,1),0,1);float f=(1-(float)Math.Pow(t,1.7))*last;a=Math.Min(last,(int)f);b=Math.Min(last,a+1);blend=f-a;
        }
        /// <summary>A frame's cell on its sheet: its lower left corner (uv, v up) and its side.</summary>
        public static void Cell(int columns,int frame,out float u0,out float v0,out float side)
        {
            side=1f/columns;u0=frame%columns*side;v0=1-(frame/columns+1)*side;
        }
        /// <summary>A splash's opacity at `u` of its life: whole, then gone over its last fifth (the sheets thin out themselves).</summary>
        public static float Opacity(float u)=>!(u>=0)||u>=1?0:u<.8f?1:1-MeleeMath.Smooth((u-.8f)/.2f);
        /// <summary>
        /// The quad of a splash born at `origin` along `dir`, `age` seconds old, seen from `eye`: its pivot point (the wound, drifted
        /// and sagged), its +u axis (the spray, as the camera sees it), its +v axis and its normal (toward the camera), all unit.
        /// A corner at cell uv (x, y; y up) is pivot + ux*(x-PivotU)*size + uy*(y-(1-PivotV))*size.
        /// </summary>
        public static void Quad(Vector3 origin,Vector3 dir,float size,float life,float age,bool flip,Vector3 eye,out Vector3 pivot,out Vector3 ux,out Vector3 uy,out Vector3 n)
        {
            float u=life>1e-4f?MeleeMath.Clamp(age/life,0,1):1;pivot=origin+dir*(Drift*size*(1-(1-u)*(1-u)))-Vector3.UnitY*(Sag*.5f*age*age);
            n=Unit(eye-pivot,Vector3.UnitZ);Vector3 flat=dir-n*Vector3.Dot(dir,n);
            // A spray straight at (or away from) the camera has no travel across the screen: it is laid along the screen's side.
            if(flat.LengthSquared()<.04f){Vector3 right=Unit(Vector3.Cross(Vector3.UnitY,n),Vector3.UnitX);flat+=right*(.2f-flat.Length()+.01f);}
            ux=Unit(flat,Vector3.UnitX);uy=Vector3.Cross(n,ux);if(flip)uy=-uy;
        }
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f&&l<1e6f?v/l:fallback;}
    }

    /// <summary>
    /// The live splashes: a ring of <see cref="Max"/> (the oldest goes first), fed per wound. A rapid string of hits (a
    /// machine gun, the horde) is thinned by a shared budget of <see cref="Rate"/> a second up to <see cref="BurstMax"/>
    /// (scaled with reduced effects and the low tier); a killing blow's splashes are never cut. Fixed arrays, no allocation
    /// once built.
    /// </summary>
    public sealed class NativeBloodSplashes
    {
        public const int Max=40;public const float Rate=14,BurstMax=12;
        public struct Puff{public bool Live,Ink,Flip;public int Sheet;public Vector3 Origin,Dir;public float Size,Life,Born;}
        public readonly Puff[] Items=new Puff[Max];private int cursor;private float budget=BurstMax,budgetAt=float.NaN;
        public void Clear(){Array.Clear(Items,0,Max);cursor=0;budget=BurstMax;budgetAt=float.NaN;}
        /// <summary>The splashes alive at `time` (the checks and the dev overlay).</summary>
        public int Alive(float time){int n=0;for(int i=0;i<Max;i++)if(Items[i].Live&&time-Items[i].Born<Items[i].Life&&time>=Items[i].Born-.5f)n++;return n;}
        public int Add(string kind,float severity,uint seed,Vector3 origin,Vector3 normal,Vector3 along,float time,float quality,bool low,bool kill,bool ink=false)
        {
            int count=NativeBloodSplash.Count(kind,severity,quality,low,kill);if(count<=0)return 0;
            float scale=(quality<1?.5f:1)*(low?.5f:1);if(float.IsNaN(budgetAt)||time<budgetAt-.25f){budget=BurstMax*scale;budgetAt=time;}
            budget=Math.Min(BurstMax*scale,budget+Math.Max(0,time-budgetAt)*Rate*scale);budgetAt=time;
            if(!kill){count=Math.Min(count,(int)Math.Floor(budget));if(count<=0)return 0;}budget=Math.Max(0,budget-count);
            for(int i=0;i<count;i++)
            {
                NativeBloodSplash.Puff(kind,seed,i,normal,along,severity,kill,out int sheet,out var dir,out float size,out float life,out float delay,out bool flip);
                Items[cursor++%Max]=new Puff{Live=true,Ink=ink,Flip=flip,Sheet=sheet,Origin=origin,Dir=dir,Size=size,Life=life,Born=time+delay};
            }
            return count;
        }
        /// <summary>Drops the splashes that have played out (and any born well after `time`: a rewind past them).</summary>
        public int Update(float time)
        {
            int n=0;for(int i=0;i<Max;i++){ref var p=ref Items[i];if(!p.Live)continue;if(time-p.Born>=p.Life||p.Born>time+.5f)p.Live=false;else n++;}return n;
        }
    }

    /// <summary>
    /// The blood pack's decals for the blood that lands and stays: each splat of Core/NativeBlood's pool is one quad of a
    /// pack sprite sheet (6 x 6 frames of blood spreading from where it landed: a splash with tendrils, a round pool, a
    /// smear and a tailed one), which spreads in as it lands (<see cref="DropSpread"/>) and keeps its last frame; a pool
    /// under a kill (<see cref="NativeBloodSplats.Splat.Pool"/>) spreads slowly (<see cref="PoolSpread"/>). The sheet's
    /// blood starts at its pivot and (but the round one) runs along +u: the quad is laid with the pivot on the splat and +u
    /// along its travel. The colours, the drying and the pool of splats are Core/NativeBlood's.
    /// </summary>
    public static class NativeBloodDecalLook
    {
        public const int Sheets=4,Splash=0,Round=1,Smear=2,Tail=3,Columns=6,Frames=36;
        /// <summary>The bake's entries under VisualBlood/Decal, by sheet.</summary>
        public static readonly string[] Names={"splash","round","smear","tail"};
        /// <summary>Where each sheet's blood lands in its cell (u from the left, v from the top) and how much of the cell its
        /// last frame spans along u.</summary>
        public static readonly float[] PivotU={.23f,.52f,.25f,.10f},PivotV={.40f,.52f,.45f,.47f},Span={.95f,.92f,.95f,.86f};
        public const float DropSpread=.32f,PoolSpread=2.6f,PoolDelay=.28f;
        /// <summary>
        /// Splat `seed` of half length `length` along its travel and half width `width`: its sheet, its turn off the travel
        /// (radians, about the surface normal), whether it is mirrored across and the cell's side (m).
        /// </summary>
        public static void Pick(uint seed,float length,float width,bool pool,out int sheet,out float spin,out bool mirror,out float size)
        {
            length=Math.Max(1e-3f,NativeBloodRules.Finite(length,.03f));width=Math.Max(1e-3f,NativeBloodRules.Finite(width,.03f));float g=length/width;
            float H(int k)=>NativeBloodRules.Hash(seed,k,83);mirror=H(2)<.5f;
            if(pool)sheet=H(0)<.65f?Round:Splash;
            else if(g>NativeBloodSplatLook.StreakAbove)sheet=H(0)<.55f?Tail:Smear;
            else if(g>NativeBloodSplatLook.SplashAbove)sheet=H(0)<.6f?Splash:Smear;
            else sheet=H(0)<.5f?Round:H(0)<.85f?Splash:Smear;
            spin=sheet==Round?H(3)*(float)Math.PI*2:(H(3)-.5f)*.5f;
            float reach=sheet==Round?Math.Max(length,width):Math.Max(length,width*1.15f);
            size=(pool?2f:3f)*reach*(.92f+.16f*H(1))/Span[sheet];
        }
        /// <summary>How long a splat takes to spread to its last frame.</summary>
        public static float Spread(bool pool)=>pool?PoolSpread:DropSpread;
        /// <summary>The frame (0..Frames-1, fractional) a splat shows `age` seconds after it landed: fast first, easing to rest.</summary>
        public static float Frame(float age,float spread)
        {
            float u=MeleeMath.Clamp(NativeBloodRules.Finite(age,spread)/Math.Max(1e-3f,spread),0,1);return (1-(1-u)*(1-u))*(Frames-1);
        }
        /// <summary>A pool under a kill: its half extent (m) by the wound's kind and severity; 0: none.</summary>
        public static float PoolSize(string kind,float severity,uint seed)
        {
            float s=MeleeMath.Clamp(NativeBloodRules.Finite(severity,.5f),0,1),h=NativeBloodRules.Hash(seed,0,97);
            switch(kind){case "sever":return .36f+.1f*s+.06f*h;case "cut":case "cavity":case "crush":return .28f+.1f*s+.06f*h;case "puncture":return .22f+.08f*s+.05f*h;default:return 0;}
        }
    }

    /// <summary>
    /// The pack's flesh lumps among a gib's fragments: of a crushed part's chunks (NativeKillDebris), every
    /// <see cref="Every"/>th from a seeded start, at most <see cref="Most"/> a body (half with reduced effects, none on the
    /// low tier), is drawn as one of the pack's five meshes in the chunk's place, turned and thrown as the chunk is.
    /// </summary>
    public static class NativeBloodGut
    {
        public const int Meshes=5,Every=5,Most=7;public const float Size=.2f;
        public static readonly string[] Names={"gut-1","gut-2","gut-3","gut-4","gut-5"};
        /// <summary>Whether chunk `i` of `chunks` is a flesh lump, and which mesh.</summary>
        public static bool Is(int i,int chunks,int seed,float quality,bool low,out int mesh)
        {
            mesh=0;if(low||i<0||chunks<=0)return false;int start=(int)((uint)seed%Every),k=i-start;if(k<0||k%Every!=0)return false;
            int index=k/Every,most=quality<1?Most/2:Most;if(index>=most)return false;mesh=(int)(((uint)seed/7u+(uint)index*3u)%Meshes);return true;
        }
        /// <summary>The lump's largest extent over the side of the cube it replaces (4 to 9.5 cm): about 10 to 25 cm.</summary>
        public static float Ratio(int i,int seed)=>2.6f*(.85f+.3f*NativeBloodRules.Hash((uint)seed,i,101));
    }
}
