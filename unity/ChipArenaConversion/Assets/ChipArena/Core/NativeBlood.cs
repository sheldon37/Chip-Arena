using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Blood that falls and stays (owner 2026-10-12: "blood coming off and hitting the ground (Can stay there)"), presentation
    /// only. Every rule here is a pure function of a replicated wound (its id, time, kind, severity, point and direction), the
    /// settings and the presentation clock: host, guests and the killcam spray the same droplets from the same wounds.
    /// <list type="bullet">
    /// <item>Droplets per wound (<see cref="Count"/>): cuts and cleaves spray the most, along the blade's travel (cast-off)
    /// and out of the wound; a bullet gives a small puff back out of the entry and a cone of droplets out of the exit side;
    /// crushes spatter round; bruises bleed a little, burns not at all. Reduced effects and the low graphics tier thin them.</item>
    /// <item>A droplet flies under gravity until it meets the arena (floors, walls, props and Brickfall blocks: the runtime's
    /// cast) and leaves a splat there: sized by the drop and its speed, stretched along its travel over the surface the
    /// shallower it lands (<see cref="Splat"/>), running down a wall.</item>
    /// <item>Splats stay for the rest of the round, darkening as they dry (<see cref="Darken"/>), in a pool of
    /// <see cref="NativeBloodSplats.Kept"/>: past it the oldest fades out. A splat on a block that breaks, falls or moves fades
    /// with it; the round's reset clears them all.</item>
    /// <item>A melee blade that draws blood carries it on its edge (<see cref="BladeShare"/>): each hit adds some, kept a few
    /// seconds, gone by ten.</item>
    /// </list>
    /// </summary>
    public static class NativeBloodRules
    {
        public const float Gravity=15,MaxLife=3,MinDrop=.011f,MaxDrop=.032f,MaxLength=.12f,MaxWidth=.065f,Lift=.009f;
        /// <summary>Fresh and dried splat colours (sRGB): the rim and the wet core.</summary>
        public const int Rim=0x5e0610,Core=0x8f0d18,DropColor=0x7a0912,DropDeep=0x4f0510;
        /// <summary>The Siren's ink (her minions bleed it, NativeHordeKindPresentation): near-black drops, splats with a violet wet core.</summary>
        public const int InkRim=0x120b1a,InkCore=0x2a1940,InkDrop=0x150d22,InkDeep=0x07040b;
        public static float Finite(float v,float fallback)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:v;
        /// <summary>The droplets a wound sprays: by its kind and severity, `quality` (1 full, .3 reduced effects) and the low tier.</summary>
        public static int Count(string kind,float severity,float quality,bool low)
        {
            float s=MeleeMath.Clamp(Finite(severity,.5f),0,1),q=MeleeMath.Clamp(Finite(quality,1),0,1);if(!(q>0))return 0;
            float n;switch(kind)
            {
                case "sever":n=26+10*s;break;case "cut":n=12+14*s;break;case "cavity":n=16+8*s;break;case "puncture":n=8+6*s;break;case "crush":n=16+8*s;break;
                case "bruise":n=2+2*s;break;case "squirt":n=14+8*s;break;case "drip":return 1;default:return 0;
            }
            n*=q<1?.35f+.55f*q:1;if(low)n*=.55f;return Math.Max(kind=="bruise"?1:3,(int)Math.Round(n));
        }
        /// <summary>A splatter's extra droplets for a melee kill (the plan's blood: a cleave, a slit throat), the same thinning.</summary>
        public static int Extra(float blood,float quality,bool low){float n=Math.Max(0,(Finite(blood,1)-1))*24;n*=quality<1?.35f+.55f*MeleeMath.Clamp(quality,0,1):1;if(low)n*=.55f;return (int)Math.Round(n);}
        public static float Hash(uint seed,int i,int k)=>(float)NativeWoundCarve.Hash(seed,i,k);
        /// <summary>
        /// Droplet `i` of `count` from a wound: its launch velocity (Unity world), size (m) and launch delay (s, a short gush,
        /// not one frame). `normal`: out of the wound; `along`: the blow's travel through it (the exit side for a bullet).
        /// </summary>
        public static void Drop(string kind,uint seed,int i,int count,Vector3 normal,Vector3 along,out Vector3 velocity,out float size,out float delay)
        {
            float H(int k)=>Hash(seed,i,k);float R(int k)=>H(k)-.5f;
            normal=Unit(normal,Vector3.UnitY);along=Unit(along,normal);Vector3 side=Unit(Vector3.Cross(along,Vector3.UnitY),Vector3.UnitX),up=Vector3.UnitY;
            size=MinDrop+(MaxDrop-MinDrop)*H(9)*H(10)+(kind=="sever"||kind=="crush"?.006f:0);delay=.05f*H(11)*(count>12?1:.5f);
            switch(kind)
            {
                case "puncture":case "cavity":
                {
                    // A quarter puff back out of the entry, the rest a cone out of the exit side.
                    if(i<Math.Max(2,count/4)){velocity=-along*(.6f+.9f*H(1))+normal*(.5f+.6f*H(2))+up*(.4f+.6f*H(3))+side*R(4)*1.2f;size*=.7f;delay*=.4f;return;}
                    float speed=kind=="cavity"?3.4f+2.6f*H(1):2.6f+2.2f*H(1);velocity=along*speed+side*R(4)*speed*.55f+up*(.5f+R(5)*1.4f+.25f*speed*H(3));return;
                }
                case "crush":velocity=normal*(1.6f+1.8f*H(1))+side*R(4)*3.2f+up*(1.2f+1.6f*H(3))+along*R(5)*1.6f;return;
                case "bruise":velocity=normal*(.6f+.5f*H(1))+up*(.4f+.4f*H(3))+side*R(4)*.6f;size*=.7f;return;
                // A crush squeezing blood out from under what crushed it (NativeSmashGib): low, flat jets along `along`.
                case "squirt":velocity=along*(2.6f+3.4f*H(1))+side*R(4)*1.8f+up*(.15f+.75f*H(3));size*=.9f;delay*=.6f;return;
                // A single drop leaving a wound (an arrow's entry, NativeArrowGib): it barely leaves the skin and falls.
                case "drip":velocity=normal*(.08f+.22f*H(1))+side*R(4)*.18f-up*(.05f+.25f*H(3));size=MinDrop+(MaxDrop-MinDrop)*.45f*H(9);delay=0;return;
                default:
                {
                    // Cuts (and severs): over half is cast off the blade along its travel, the rest jets out of the wound.
                    if(H(0)<.58f){float speed=2.2f+2.8f*H(1);velocity=along*speed+normal*(.4f+.9f*H(2))+up*(.6f+1.1f*H(3))+side*R(4)*1.1f;return;}
                    float jet=1.6f+2.2f*H(1);velocity=normal*jet+up*(.8f+1.2f*H(3))+side*R(4)*1.6f+along*R(5)*1.2f;return;
                }
            }
        }
        /// <summary>
        /// The splat a droplet of `size` leaves where it meets a surface with `normal` at `velocity`: its half length along
        /// `along` (the travel over the surface, or straight down a wall) and half width. Faster and shallower: longer; never
        /// past <see cref="MaxLength"/> x <see cref="MaxWidth"/>.
        /// </summary>
        public static void Splat(Vector3 velocity,Vector3 normal,float size,uint seed,out Vector3 along,out float length,out float width)
        {
            normal=Unit(normal,Vector3.UnitY);float speed=Finite(velocity.Length(),0);Vector3 tangent=velocity-normal*Vector3.Dot(velocity,normal);float slide=tangent.Length();
            float grazing=speed>1e-3f?MeleeMath.Clamp(slide/speed,0,1):0;size=MeleeMath.Clamp(Finite(size,.02f),MinDrop*.5f,MaxDrop*1.5f);
            along=slide>1e-3f?tangent/slide:Unit(Vector3.Cross(normal,Vector3.UnitX),Vector3.UnitZ);
            // A wall: the drop runs down it.
            if(normal.Y<.5f){var down=-Vector3.UnitY-normal*(-normal.Y);if(down.LengthSquared()>1e-4f)along=Vector3.Normalize(Vector3.Lerp(along,Vector3.Normalize(down),.7f));}
            // A drop spreads to about twice its size, more the harder it lands (half extents: a 2 cm drop at 5 m/s is a
            // 6 cm splat; a fast grazing one streaks to about 15 cm).
            float spread=.9f+.12f*Math.Min(speed,8);width=Math.Min(MaxWidth,size*spread*(.85f+.3f*Hash(seed,0,31)));
            length=Math.Min(MaxLength,width*(1+1.6f*grazing*grazing+(normal.Y<.5f?.9f:0)));
        }
        /// <summary>The splat's brightness as it dries: fresh 1, about .6 after half a minute, never below .52.</summary>
        public static float Darken(float age)=>!(age>0)?1:.52f+.48f*(float)Math.Exp(-age/14);
        /// <summary>A splat spreading where it lands (its first 70 ms) and fading (`fade` s from its fade start), 0..1.</summary>
        public static float Spread(float age)=>MeleeMath.Ease(Finite(age,1)/.07f)*.85f+.15f*MeleeMath.Clamp(Finite(age,1)/.07f,0,1);
        /// <summary>A blade's blood from one hit `age` seconds ago: .4 for 4 s, then easing to nothing by 10 s.</summary>
        public static float BladeShare(float age)=>!(age>=0)||age>=10?0:age<4?.4f:.4f*(1-MeleeMath.Smooth((age-4)/6));
        /// <summary>
        /// Blood along a melee blade's edge (`amount` 0..1 from the ledger): sliver `i` of <see cref="Slivers"/>, in the weapon
        /// hand's source frame (the kill-box blade: its edge at +x, from the tip back toward the hilt as the amount grows):
        /// centre and size. False when this sliver shows nothing.
        /// </summary>
        public const int Slivers=6;
        public static bool Sliver(string spec,int i,float amount,out Vector3 centre,out Vector3 size)
        {
            centre=size=Vector3.Zero;amount=MeleeMath.Clamp(Finite(amount,0),0,1);if(amount<.02f||i<0||i>=Slivers)return false;
            float z0,z1,edge,thick;switch(spec)
            {
                case "sword":z0=.33f;z1=1.17f;edge=.05f;thick=.036f;break;case "daggers":z0=.33f;z1=.73f;edge=.045f;thick=.036f;break;
                case "greatsword":z0=.34f;z1=1.58f;edge=.09f;thick=.036f;break;default:return false;
            }
            // Sliver i covers a stretch from the tip back; more blood reaches further down the blade and thickens.
            float reachBack=(z1-z0)*(.3f+.6f*amount),from=z1-reachBack*(i+1)/Slivers,to=z1-reachBack*i/Slivers;if(i>0)to+=.01f;
            float lift=.4f+.6f*MeleeMath.Smooth(amount*1.4f-i*.12f);if(lift<.05f)return false;
            float depth=(.012f+.016f*amount)*(1-.35f*i/Slivers)*lift;
            centre=new Vector3(edge-depth*.5f+.001f,-.065f,(from+to)*.5f);size=new Vector3(depth,thick,Math.Max(.01f,to-from));return true;
        }
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f&&l<1e6f?v/l:fallback;}
    }

    /// <summary>
    /// The persistent splat pool: <see cref="Kept"/> splats stay; adding past that fades the oldest out over
    /// <see cref="FadeSeconds"/> (in its <see cref="Spare"/> fading slots), and a splat whose surface breaks or moves fades over
    /// <see cref="SurfaceFade"/>. Each splat is keyed by its droplet, so the same wound fed twice (the killcam replay, a guest's
    /// clock rewind) never doubles one. Fixed arrays, no allocation once built.
    /// </summary>
    public sealed class NativeBloodSplats
    {
        public const int Kept=384,Spare=48,Capacity=Kept+Spare;public const float FadeSeconds=.6f,SurfaceFade=.25f;
        public struct Splat{public bool Live,Ink;public Vector3 Center,Normal,Along;public float Length,Width,Born,FadeAt,FadeFor;public uint Seed;public int Surface;public long Key;}
        public readonly Splat[] Items=new Splat[Capacity];
        /// <summary>How many stay (Kept; the runtime lowers it with reduced effects and on the low graphics tier).</summary>
        public int Limit=Kept;
        /// <summary>Live splats not yet fading, and all live ones.</summary>
        public int Steady{get;private set;}
        public int Live{get;private set;}
        /// <summary>Bumped whenever the set or a fade changes (the runtime rebuilds its mesh then, and as they dry).</summary>
        public int Version{get;private set;}
        public void Clear(){Array.Clear(Items,0,Items.Length);Steady=Live=0;Version++;}
        public bool Has(long key){for(int i=0;i<Capacity;i++)if(Items[i].Live&&Items[i].Key==key)return true;return false;}
        public int Add(long key,Vector3 center,Vector3 normal,Vector3 along,float length,float width,uint seed,int surface,float time,bool ink=false)
        {
            if(Has(key))return -1;
            while(Steady>=Math.Max(1,Math.Min(Kept,Limit)))
            {
                // The oldest steady splat starts fading.
                int old=-1;for(int i=0;i<Capacity;i++)if(Items[i].Live&&Items[i].FadeAt<0&&(old<0||Items[i].Born<Items[old].Born))old=i;
                if(old<0)break;Items[old].FadeAt=time;Items[old].FadeFor=FadeSeconds;Steady--;
            }
            int slot=-1;for(int i=0;i<Capacity&&slot<0;i++)if(!Items[i].Live)slot=i;
            // No free slot: the fading splat furthest along goes now.
            if(slot<0){float most=-1;for(int i=0;i<Capacity;i++){var s=Items[i];if(s.FadeAt>=0){float u=(time-s.FadeAt)/Math.Max(1e-3f,s.FadeFor);if(u>most){most=u;slot=i;}}}if(slot<0)return -1;Live--;}
            Items[slot]=new Splat{Live=true,Ink=ink,Center=center,Normal=normal,Along=along,Length=length,Width=width,Born=time,FadeAt=-1,FadeFor=FadeSeconds,Seed=seed,Surface=surface,Key=key};
            Steady++;Live++;Version++;return slot;
        }
        /// <summary>Every splat on `surface` fades (its block broke, fell or moved).</summary>
        public int FadeSurface(int surface,float time)
        {
            int n=0;for(int i=0;i<Capacity;i++){ref var s=ref Items[i];if(s.Live&&s.Surface==surface&&s.FadeAt<0){s.FadeAt=time;s.FadeFor=SurfaceFade;Steady--;n++;}}if(n>0)Version++;return n;
        }
        /// <summary>Every steady splat within `radius` of `center` fades over `fadeFor` s (a revive washes its body's blood and ink
        /// away: NativeReviveHolo).</summary>
        public int FadeNear(Vector3 center,float radius,float time,float fadeFor)
        {
            int n=0;float r2=radius*radius;
            for(int i=0;i<Capacity;i++){ref var s=ref Items[i];if(s.Live&&s.FadeAt<0&&Vector3.DistanceSquared(s.Center,center)<=r2){s.FadeAt=time;s.FadeFor=Math.Max(.05f,fadeFor);Steady--;n++;}}
            if(n>0)Version++;return n;
        }
        /// <summary>Drops the splats that have faded out (and any born after `time`: a rewind past them).</summary>
        public void Update(float time)
        {
            for(int i=0;i<Capacity;i++)
            {
                ref var s=ref Items[i];if(!s.Live)continue;
                if(s.FadeAt>=0&&time-s.FadeAt>=s.FadeFor||s.Born>time+.5f){if(s.FadeAt<0)Steady--;s.Live=false;Live--;Version++;}
            }
        }
        /// <summary>A splat's opacity at `time`: spreading in, then whole until it fades.</summary>
        public float Opacity(int i,float time)
        {
            var s=Items[i];if(!s.Live)return 0;float a=NativeBloodRules.Spread(time-s.Born);if(s.FadeAt>=0)a*=1-MeleeMath.Clamp((time-s.FadeAt)/Math.Max(1e-3f,s.FadeFor),0,1);return a;
        }
    }

    /// <summary>
    /// The droplets in flight: spawned from a wound by <see cref="NativeBloodRules.Drop"/>, integrated on the presentation clock
    /// (gravity, a little drag) and swept against the arena by the runtime's cast; a drop that meets something leaves its splat
    /// in the pool and is done; one that falls out of the arena or lives past <see cref="NativeBloodRules.MaxLife"/> just
    /// goes. Fixed arrays, no allocation once built.
    /// </summary>
    public sealed class NativeBloodSim
    {
        public const int Max=256;
        /// <summary>The runtime's sweep from `from` to `to` against the arena: the first hit's point, normal and a surface id
        /// (0: a surface no splat should stay on, such as a moving crate).</summary>
        public delegate bool Cast(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal,out int surface);
        public struct Drop{public bool Live;public Vector3 Position,Velocity;public float Size,Born,Delay;public uint Seed;public long Key;public bool Stain,Ink;}
        public readonly Drop[] Drops=new Drop[Max];public readonly NativeBloodSplats Splats=new NativeBloodSplats();
        private int cursor;private float clock=float.NaN;
        public int Flying{get{int n=0;for(int i=0;i<Max;i++)if(Drops[i].Live)n++;return n;}}
        public void Clear(bool splats){Array.Clear(Drops,0,Max);cursor=0;clock=float.NaN;Array.Clear(budgetId,0,Budgets);if(splats)Splats.Clear();}
        /// <summary>
        /// Sprays a wound's droplets from `origin` (Unity world). `key` identifies the wound (pilot, wound id, time): its drops'
        /// splat keys follow from it. `stain` false: the drops fly but leave no splat (a replay of a wound already stained).
        /// </summary>
        /// <summary>Per target, droplets refill at <see cref="BudgetRate"/> a second up to <see cref="BudgetBurst"/> (scaled by
        /// the same thinning): a rapid string of hits (the Whirlwind's pulses, a machine gun) keeps bleeding without flooding.
        /// A killing blow's spray (`kill`) is never cut short.</summary>
        public const float BudgetRate=40,BudgetBurst=44;public const int Budgets=16;
        private readonly int[] budgetId=new int[Budgets];private readonly float[] budgetLeft=new float[Budgets],budgetAt=new float[Budgets];private int budgetNext;
        /// <summary>How many of `wanted` droplets target `target` may spray at `time` (and takes them from its budget).</summary>
        public int Allow(int target,int wanted,float time,float quality,bool low,bool kill=false)
        {
            if(wanted<=0)return 0;float scale=(quality<1?.35f+.55f*MeleeMath.Clamp(quality,0,1):1)*(low?.55f:1);int slot=-1;
            for(int i=0;i<Budgets;i++)if(budgetId[i]==target+1){slot=i;break;}
            if(slot<0){slot=budgetNext++%Budgets;budgetId[slot]=target+1;budgetLeft[slot]=BudgetBurst*scale;budgetAt[slot]=time;}
            float dt=time-budgetAt[slot];if(dt<-.25f)dt=BudgetBurst;budgetAt[slot]=time;budgetLeft[slot]=Math.Min(BudgetBurst*scale,budgetLeft[slot]+Math.Max(0,dt)*BudgetRate*scale);
            int granted=kill?wanted:Math.Min(wanted,(int)Math.Floor(budgetLeft[slot]));if(!kill&&granted<1&&budgetLeft[slot]>=.5f)granted=1;budgetLeft[slot]=Math.Max(0,budgetLeft[slot]-granted);return granted;
        }
        /// <remarks>`carry` (m/s, Unity world; zero for none): what threw the blood moves it on, each drop taking a seeded share
        /// (.45 to 1) of it, so a passing train or a crusher's squeeze sprays along its travel (NativeSmashGib).</remarks>
        public int Spray(string kind,float severity,uint seed,long key,Vector3 origin,Vector3 normal,Vector3 along,float time,float quality,bool low,bool stain,int extra=0,int target=-1,bool kill=false,bool ink=false,Vector3 carry=default)
        {
            int count=NativeBloodRules.Count(kind,severity,quality,low)+Math.Max(0,extra);if(target>=0)count=Allow(target,count,time,quality,low,kill);
            bool carried=carry.LengthSquared()>1e-6f&&carry.LengthSquared()<1e6f;
            for(int i=0;i<count;i++)
            {
                NativeBloodRules.Drop(kind,seed,i,count,normal,along,out var v,out float size,out float delay);if(carried)v+=carry*(.45f+.55f*NativeBloodRules.Hash(seed,i,23));
                int slot=cursor++%Max;Drops[slot]=new Drop{Live=true,Position=origin,Velocity=v,Size=size,Born=time,Delay=delay,Seed=seed^(uint)(i*0x9e3779b1u),Key=key*64+i,Stain=stain,Ink=ink};
            }
            return count;
        }
        /// <summary>Moves the drops to `time` (at most .05 s a step, so a hitch never tunnels a drop through a floor).</summary>
        public void Step(float time,Cast cast,float floor=-50)
        {
            if(float.IsNaN(clock)||time<clock-.25f){clock=time;}
            float dt=MeleeMath.Clamp(time-clock,0,.1f);clock=time;if(dt<=0)return;int steps=dt>.05f?2:1;float h=dt/steps;
            for(int s=0;s<steps;s++)for(int i=0;i<Max;i++)
            {
                ref var d=ref Drops[i];if(!d.Live)continue;float age=time-d.Born;
                if(age>NativeBloodRules.MaxLife||d.Position.Y<floor){d.Live=false;continue;}if(age<d.Delay)continue;
                var v=d.Velocity;v.Y-=NativeBloodRules.Gravity*h;v*=1-.06f*h;var to=d.Position+v*h;
                if(cast!=null&&cast(d.Position,to,out var p,out var n,out int surface))
                {
                    d.Live=false;if(!d.Stain||surface==0)continue;
                    NativeBloodRules.Splat(v,n,d.Size,d.Seed,out var along,out float length,out float width);
                    Splats.Add(d.Key,p+n*NativeBloodRules.Lift,n,along,length,width,d.Seed,surface,time,d.Ink);continue;
                }
                d.Position=to;d.Velocity=v;
            }
        }
    }
}
