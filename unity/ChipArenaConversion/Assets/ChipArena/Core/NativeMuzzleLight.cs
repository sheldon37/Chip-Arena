using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One gun's muzzle light: its colour (sRGB hex; linear on the GPU), peak (times the linear colour), reach (m), the
    /// pulse's attack and exponential decay (s) and, under sustained fire, the share it holds between rounds.</summary>
    public struct MuzzleLightLook {public int Color;public float Peak,Range,Attack,Decay,Sustain;}

    /// <summary>
    /// The light a gun's muzzle flash throws (owner 2026-10-20: "I still want to make use of lighting"). Every shot lights the
    /// floor, the walls, the kit, the pilots, the horde and the station's kinds round the shooter, and the first-person gun and
    /// hands, briefly: a fast attack (3-4 ms) and an exponential decay (pistol 45 ms, rifle 65, machine gun 40). It is not a
    /// Unity light and not one of the map's practical slots: four muzzle slots of their own (Resources/NativeMuzzleLight.cginc,
    /// MuzzleLights), which NativeBlockLit adds to its light block and SourceLit, ZombieLit and NativeLit add as emission on
    /// every map (the dressed maps' blocks have no add pass, so a Unity point light never reached them, and the old pulse only
    /// took a projectile light the nearest projectiles left free). A shooter gets at most one slot whatever its fire rate:
    /// under sustained fire (the machine gun, Overdrive, Fan the Hammer) the rounds merge into one flickering light, the
    /// strongest round's pulse over a held floor, never a sum. The nearest shooters to the camera take the slots (the budget
    /// follows the graphics tier, Combat glow, Reduced effects and Alarm lights); the rest keep only their flash sprites.
    /// Deterministic: a shooter's light is a pure function of its rounds' replicated times (the Shot events every peer's frame
    /// carries for a second, and Actor.LastShot), its id and the presentation clock, so host, guests, late joiners and the
    /// killcam light the same rounds the same way. Cosmetic only: nothing here reaches the authority or the network frame.
    /// </summary>
    public static class NativeMuzzleLight
    {
        /// <summary>The shader's slots; the rounds a shooter's light remembers; offers a frame takes.</summary>
        public const int Slots=4,History=6,Candidates=16;
        /// <summary>The span of rounds a light reads back (s), the longest gap still sustained fire (s), the faintest light worth
        /// a slot, the pulse's tail (decays: it ends exactly there), the light's softening radius near the muzzle (m; the
        /// first-person view's own, softer and dimmer, so the player's own flash never whites out the view gun or a wall in
        /// front of it), and how far ahead of the crown the light sits (m).</summary>
        public const float Window=.5f,SustainGap=.16f,Floor=.01f,Tail=6,Near=.25f,ViewNear=.5f,ViewPeak=.65f,Lead=.1f;
        /// <summary>The inverse-square share of the falloff (the shader's MUZZLE_FALL): 1 / (1 + Fall (d / range)^2), so the pool
        /// is hot by the gun (half at a third of the reach) and the window takes it to 0 at the reach.</summary>
        public const float Fall=8;
        /// <summary>The light of a gun's rounds (and its kit's).</summary>
        public static bool Look(string spec,MuzzleKit kit,out MuzzleLightLook l)
        {
            switch(spec)
            {
                case "pistol":l=new MuzzleLightLook{Color=0xffc68a,Peak=2.6f,Range=6f,Attack=.004f,Decay=.045f,Sustain=.55f};break;
                case "rifle":l=new MuzzleLightLook{Color=0xffbf7c,Peak=3f,Range=7.5f,Attack=.004f,Decay=.065f,Sustain=.5f};break;
                case "machinegun":l=new MuzzleLightLook{Color=0xffc282,Peak=2.2f,Range=6.5f,Attack=.003f,Decay=.04f,Sustain=.6f};break;
                default:l=default;return false;
            }
            if(kit==MuzzleKit.Fan){l.Peak*=1.1f;l.Sustain=.6f;}
            else if(kit==MuzzleKit.Overdrive){l.Color=0xffd29c;l.Peak=2.5f;l.Range=7f;l.Decay=.045f;l.Sustain=.7f;}
            return true;
        }
        /// <summary>One round's pulse at `age` (0..1): a linear attack, then exp(-t / Decay), tapering to exactly 0 over its last
        /// decay before Tail decays.</summary>
        public static float Pulse(in MuzzleLightLook l,double age)
        {
            if(!(age>=0))return 0;if(age<l.Attack)return (float)(age/l.Attack);
            double t=(age-l.Attack)/l.Decay;if(t>=Tail-1e-6)return 0;float e=(float)Math.Exp(-t);return t>Tail-1?e*(float)(Tail-t):e;
        }
        // Alarm lights Calm ("no flashes: a slow glow"): a 60 ms smooth rise and a .2 s fall.
        private static float CalmPulse(double age){if(!(age>=0))return 0;if(age<.06)return NativeWeaponFx.Smooth(age/.06);double t=(age-.06)/.2;return t>=Tail-1e-6?0:(float)Math.Exp(-t)*(t>Tail-1?(float)(Tail-t):1);}
        /// <summary>
        /// The rounds of `owner` a light reads at presentation `time`, newest first into `into` (History long): its gun Shot
        /// events (Kind Shot, Source the shooter, Amount its gun's WeaponId) no later than `time` and within Window of it, with
        /// Actor.LastShot (a round whose event the per-tick cap dropped still flashes), each once (2 ms apart at least). The count.
        /// </summary>
        public static int Shots(IReadOnlyList<ArenaEvent> events,int owner,float lastShot,double time,float[] into)
        {
            int n=0;if(into==null)return 0;
            if(events!=null)for(int i=0;i<events.Count;i++){var e=events[i];if(e.Kind==EventKind.Shot&&e.Source==owner&&e.Amount>.5f&&e.Amount<3.5f)n=Insert(into,n,e.Time,time);}
            return Insert(into,n,lastShot,time);
        }
        private static int Insert(float[] into,int n,float t,double time)
        {
            if(float.IsNaN(t)||t>time+1e-4||t<time-Window)return n;
            for(int i=0;i<n;i++)if(Math.Abs(into[i]-t)<.002f)return n;
            int at=n;while(at>0&&into[at-1]<t)at--;if(at>=into.Length)return n;
            int last=Math.Min(n,into.Length-1);for(int i=last;i>at;i--)into[i]=into[i-1];into[at]=t;return Math.Min(n+1,into.Length);
        }
        /// <summary>
        /// A shooter's merged light at `time` (0 to about 1.1; times Peak and the colour): the strongest of its rounds' pulses
        /// (each round its own seeded strength, .88-1.12, on Full alarm lights) over a held floor while the rounds come
        /// SustainGap or closer: Sustain times the burst's depth (half at the second round, all from the third), held until 1.15
        /// gaps after the newest round and then decaying, flickering per 1/90 s on Full. Alarm lights Reduced: slower decay, no
        /// flicker; Calm: a slow glow (no flashes: half a 60 ms-rise, .2 s-fall glow per round, summed, at most one). `shots`
        /// newest first (Shots).
        /// </summary>
        public static float Level(in MuzzleLightLook look,float[] shots,int count,double time,int owner,NativeKitFlash flash)
        {
            if(shots==null||count<=0)return 0;var l=look;if(flash==NativeKitFlash.Reduced)l.Decay*=1.6f;bool full=flash==NativeKitFlash.Full,calm=flash==NativeKitFlash.Calm;
            // Calm: half a glow per round, summed and held at one, so sustained fire is a steady glow, a lone round a soft one.
            if(calm){float sum=0;for(int i=0;i<count;i++)sum+=CalmPulse(time-shots[i]);return Math.Min(1,.5f*sum);}
            float p=0;
            for(int i=0;i<count;i++)
            {
                double age=time-shots[i];if(age<0)continue;float j=full?.88f+.24f*(float)NativeWeaponFx.Hash(NativeWeaponFx.Seed(shots[i],owner),7):1;
                float v=j*Pulse(l,age);if(v>p)p=v;
            }
            int burst=1;for(int i=0;i+1<count&&shots[i]-shots[i+1]<=SustainGap;i++)burst++;
            float s=0;
            if(burst>1)
            {
                double gap=shots[0]-shots[1],age=time-shots[0],hold=gap*1.15;
                if(age>=0)
                {
                    float h=age<=hold?1:(float)Math.Exp(-(age-hold)/l.Decay);s=l.Sustain*Math.Min(1,(burst-1)*.5f)*h;
                    if(full)s*=.8f+.2f*(float)NativeWeaponFx.Hash(NativeWeaponFx.Seed(shots[0],owner),1000+(long)Math.Floor(time*90));
                }
            }
            return Math.Max(p,s);
        }
        /// <summary>
        /// The frame's light budget: how many shooters take a slot and the gain on their light. Combat glow Off: none; the low
        /// graphics tier: one; Combat glow Low or Reduced effects: two; otherwise four. Reduced effects .7, Combat glow Low .8,
        /// Alarm lights Reduced .75 and Calm .5 (at most two slots).
        /// </summary>
        public static void Budget(float glow,float effects,bool low,NativeKitFlash flash,out int lights,out float gain)
        {
            lights=glow<=0?0:low?1:glow<.5f||effects<.5f?2:Slots;if(flash==NativeKitFlash.Calm)lights=Math.Min(lights,2);
            gain=(effects<.5f?.7f:1)*(glow<.5f?.8f:1)*(flash==NativeKitFlash.Reduced?.75f:flash==NativeKitFlash.Calm?.5f:1);
        }
        /// <summary>The linear colour a level lights with (the colour times Peak times level; the view's own times ViewPeak).</summary>
        public static Vector3 Rgb(in MuzzleLightLook l,float level,bool view)
        {
            NativeBlastSim.Linear(l.Color,out double r,out double g,out double b);float k=l.Peak*level*(view?ViewPeak:1);return new Vector3((float)r*k,(float)g*k,(float)b*k);
        }
        /// <summary>
        /// The shader's MuzzleLights (Resources/NativeMuzzleLight.cginc) on the CPU: slot k at pos[4k..] (x, y, z, range) with
        /// colour rgb[4k..] (linear r, g, b and its softening radius); (1 - (d / range)^2)^2, times d^2 / (d^2 + near^2), times
        /// 1 / (1 + Fall (d / range)^2), times the wrapped N.L; nothing at all while count is 0.
        /// </summary>
        public static Vector3 Shade(Vector3 world,Vector3 n,float wrap,float[] pos,float[] rgb,int count)
        {
            var sum=Vector3.Zero;if(count<1)return sum;
            for(int k=0;k<Slots;k++)
            {
                var p=new Vector3(pos[k*4],pos[k*4+1],pos[k*4+2]);var d=p-world;float len2=Math.Max(Vector3.Dot(d,d),.000001f),len=(float)Math.Sqrt(len2);
                float q=len/Math.Max(pos[k*4+3],.0001f),a=Sat(1-q*q),w=rgb[k*4+3],near=len2/(len2+w*w),fall=1/(1+q*q*Fall),ndl=Sat((Vector3.Dot(n,d/len)+wrap)/(1+wrap));
                sum+=new Vector3(rgb[k*4],rgb[k*4+1],rgb[k*4+2])*(a*a*near*fall*ndl);
            }
            return sum;
        }
        private static float Sat(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// The frame's muzzle lights: every live shooter offers its light (Offer, at most one per shooter: a first-person view's
    /// offer wins over its own third-person one), then Resolve gives the nearest `budget` to the eye a slot (ties by id), the
    /// rest none. Memoryless, so the same frame and eye always pick the same slots. Fixed arrays: nothing allocates.
    /// </summary>
    public sealed class NativeMuzzleLightPicker
    {
        private readonly int[] owner=new int[NativeMuzzleLight.Candidates];private readonly Vector3[] at=new Vector3[NativeMuzzleLight.Candidates],rgb=new Vector3[NativeMuzzleLight.Candidates];
        private readonly float[] range=new float[NativeMuzzleLight.Candidates],near=new float[NativeMuzzleLight.Candidates],distance=new float[NativeMuzzleLight.Candidates];
        private readonly bool[] view=new bool[NativeMuzzleLight.Candidates],taken=new bool[NativeMuzzleLight.Candidates];private int offers;
        /// <summary>The slots this frame: positions with ranges (x, y, z, range) and linear colours with the softening radius (r, g,
        /// b, near), Slots of each; unused slots sit far below with no colour. Count: the slots in use; Ids: their shooters.</summary>
        public readonly float[] Pos=new float[NativeMuzzleLight.Slots*4],Rgb=new float[NativeMuzzleLight.Slots*4];public readonly int[] Ids=new int[NativeMuzzleLight.Slots];
        public int Count{get;private set;}
        public int Offers=>offers;
        public NativeMuzzleLightPicker(){Clear();}
        public void Begin(){offers=0;}
        public void Clear(){offers=0;Count=0;Fill(0);}
        /// <summary>A shooter's light this frame (`level` from NativeMuzzleLight.Level); `first`: the first-person view's own.</summary>
        public void Offer(int id,Vector3 position,in MuzzleLightLook look,float level,bool first)
        {
            if(!(level>NativeMuzzleLight.Floor)||float.IsNaN(position.X)||float.IsNaN(position.Y)||float.IsNaN(position.Z))return;
            int slot=-1;for(int i=0;i<offers;i++)if(owner[i]==id){slot=i;break;}
            if(slot>=0&&view[slot]&&!first)return;if(slot<0){if(offers>=NativeMuzzleLight.Candidates)return;slot=offers++;}
            owner[slot]=id;at[slot]=position;rgb[slot]=NativeMuzzleLight.Rgb(look,level,first);range[slot]=look.Range;near[slot]=first?NativeMuzzleLight.ViewNear:NativeMuzzleLight.Near;view[slot]=first;
        }
        /// <summary>Picks the slots for `eye`: the nearest `budget` offers (at most Slots), each lit at `gain`.</summary>
        public void Resolve(Vector3 eye,int budget,float gain)
        {
            int n=Math.Min(Math.Min(budget,NativeMuzzleLight.Slots),offers);for(int i=0;i<offers;i++){distance[i]=Vector3.Distance(at[i],eye);taken[i]=false;}
            Count=0;
            for(int s=0;s<n;s++)
            {
                int best=-1;for(int i=0;i<offers;i++){if(taken[i])continue;if(best<0||distance[i]<distance[best]||distance[i]==distance[best]&&owner[i]<owner[best])best=i;}
                if(best<0)break;taken[best]=true;var c=rgb[best]*Math.Max(0,gain);if(!(c.X+c.Y+c.Z>0))continue;
                int k=Count++;Pos[k*4]=at[best].X;Pos[k*4+1]=at[best].Y;Pos[k*4+2]=at[best].Z;Pos[k*4+3]=range[best];Rgb[k*4]=c.X;Rgb[k*4+1]=c.Y;Rgb[k*4+2]=c.Z;Rgb[k*4+3]=near[best];Ids[k]=owner[best];
            }
            Fill(Count);
        }
        private void Fill(int from)
        {
            for(int k=from;k<NativeMuzzleLight.Slots;k++){Pos[k*4]=0;Pos[k*4+1]=-10000;Pos[k*4+2]=0;Pos[k*4+3]=1;Rgb[k*4]=Rgb[k*4+1]=Rgb[k*4+2]=0;Rgb[k*4+3]=NativeMuzzleLight.Near;Ids[k]=0;}
        }
    }
}
