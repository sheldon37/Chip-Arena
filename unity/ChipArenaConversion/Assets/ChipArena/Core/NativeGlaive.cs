using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Glaive's names and numbers (docs/GLAIVE.md): the owner's melee class (2026-11-09: "Glaive ... It's throwable, slices like a
    /// sword, etc. Lets make the combos flow nicely and the animation very nice; with great quality effects"). An original ring blade, held by
    /// its inner grip in the right hand: four cuts that flow into each other (a flat forehand, a rising backhand, a whirl all round and a
    /// boomerang flick that is caught again), a skimming lunge on a sprint, Q throws it to ricochet between enemies and come back (Q or the
    /// left click while it is out bursts it), E sets it circling the wielder. The rules (Runtime NativeGlaiveAuthority.cs) and the
    /// presentation both read these, so a number lives in one place. Pure.
    /// </summary>
    public static class NativeGlaive
    {
        public const string Spec="glaive",Name="Glaive";
        public const string Swing="glaive-cut",Skim="glaive-skim",Throw="glaive-throw",Halo="glaive-halo",Burst="glaive-burst";
        // The disc's states (NativeGlaiveKit.Disc): in the hand, thrown out (ricocheting), coming back, circling the wielder, burst (re-forming
        // in the hand), the combo's boomerang flick.
        public const int Hand=0,Out=1,Back=2,Orbit=3,Gone=4,Flick=5;
        // Hit kinds (NativeGlaiveHit.Kind): a cut, the whirl's, the flick's, the throw's, a ricochet's, the return's, the halo's, the burst.
        public const string HitCut="cut",HitWhirl="whirl",HitFlick="flick",HitThrow="throw",HitRicochet="ricochet",HitReturn="return",HitHalo="halo",HitBurst="burst",HitCatch="catch",HitSkim="skim";
        public const int MaxHits=40,Slots=2;
        public static readonly string[] SlotKeys={"Q","E"};
        /// <summary>Cooldowns (s) in slot order: Q from the catch (or the re-forming), E from the halo's end.</summary>
        public static readonly float[] Cooldown={6,10};

        // ---- the combo ----
        /// <summary>The four cuts: wind-up to the hit (s), the whole move (s), damage, reach (m) and half arc (rad). The whirl (2) cuts all round;
        /// the flick (3) throws the ring out on a boomerang's loop (FlickReach ahead, FlickWide to the side) and catches it again, cutting
        /// everyone within FlickRadius of it once each way.</summary>
        public static readonly float[] SwingHit={.15f,.15f,.2f,.16f},SwingLength={.4f,.4f,.56f,.7f},SwingDamage={16,16,22,18},SwingReach={2.5f,2.5f,2.7f,0},SwingArc={1.05f,1.05f,3.15f,0};
        public const float ComboWindow=.45f,SwingPush=4,FlickTime=.46f,FlickReach=5.5f,FlickWide=1.4f,FlickRadius=.8f,HandHeight=.35f;
        public const int Steps=4;
        // ---- the skim (a sprint's attack) ----
        public const float SkimWind=.08f,SkimTime=.2f,SkimReach=4.5f,SkimWidth=1.2f,SkimDamage=20,SkimPush=6,SkimRecover=.26f;
        // ---- the throw (Q) ----
        /// <summary>Thrown ThrowRelease s after the press along the aim at ThrowSpeed m/s, ThrowReach m at most; the first enemy within
        /// ThrowRadius takes ThrowDamage and it ricochets to the nearest other in sight within RicochetRange m (Ricochets times, each
        /// RicochetScale as hard), then comes back at ReturnSpeed m/s cutting everyone it passes once (ReturnDamage) until it is caught
        /// (CatchRadius of the hand). MostOut s at most before it comes back on its own.</summary>
        public const float ThrowRelease=.14f,ThrowRecover=.26f,ThrowSpeed=30,ThrowReach=24,ThrowRadius=.55f,ThrowDamage=26,RicochetRange=9,RicochetScale=.85f,ReturnSpeed=34,ReturnDamage=12,ReturnRadius=.6f,CatchRadius=.75f,MostOut=4;
        public const int Ricochets=3;
        // ---- the burst (Q or the left click while it is out) ----
        public const float BurstRadius=3.4f,BurstDamage=30,BurstPush=7,BurstLift=4,Reform=.45f,BurstAfterThrow=.2f;
        // ---- the halo (E) ----
        /// <summary>Released HaloRelease s after the press, the ring circles the wielder HaloRadius m out at HaloTurns turns a second for
        /// HaloTime s, cutting each enemy it passes (HaloDamage, each at most every HaloEvery s); then it comes back to the hand.</summary>
        public const float HaloRelease=.2f,HaloRadius=1.7f,HaloTurns=2.2f,HaloTime=4,HaloDamage=10,HaloEvery=.35f,HaloHitRadius=.7f;

        /// <summary>The throw's way from the aim (`x`, `y`, `z`, unit): its pitch kept between ThrowLowest and ThrowHighest degrees.</summary>
        public const float ThrowLowest=-6,ThrowHighest=35;
        public static Vector3 ThrowAim(float x,float y,float z)
        {
            var flat=new Vector3(x,0,z);float l=flat.Length();if(l<1e-4f)flat=Vector3.UnitZ;else flat/=l;
            float p=(float)(Math.Asin(Math.Max(-1,Math.Min(1,y)))*180/Math.PI);p=Math.Max(ThrowLowest,Math.Min(ThrowHighest,p));float r=p*(float)Math.PI/180;
            return flat*(float)Math.Cos(r)+Vector3.UnitY*(float)Math.Sin(r);
        }
        public static string AbilityName(string id)
        {
            switch(id){case Swing:return "Ring Cuts";case Skim:return "Skimming Cut";case Throw:return "Ricochet Throw";case Halo:return "Halo";case Burst:return "Burst";default:return "";}
        }
        /// <summary>The icon an ability's card draws (the shared set's).</summary>
        public static string Icon(string id)=>id==Throw?"rotate-cw":id==Halo?"orbit":id==Burst?"sparkles":id==Skim?"wind":"swords";
        public static bool IsHit(string k)=>k==HitCut||k==HitWhirl||k==HitFlick||k==HitThrow||k==HitRicochet||k==HitReturn||k==HitHalo||k==HitBurst||k==HitCatch||k==HitSkim;

        /// <summary>The flick's ring at age `u` (0..1) of its loop from the hand at `from`, thrown along `dir` (flat): out on the right, back
        /// on the left.</summary>
        public static Vector3 FlickAt(Vector3 from,Vector3 dir,float u)
        {
            u=u<0?0:u>1?1:u;var d=Flat(dir);var side=new Vector3(d.Z,0,-d.X);float a=(float)Math.PI*u;
            return from+d*(FlickReach*(float)Math.Sin(a))+side*(FlickWide*(float)Math.Sin(2*a))+Vector3.UnitY*(.15f*(float)Math.Sin(a));
        }
        /// <summary>The halo's ring at `time` round the wielder's middle `centre`, begun at `at` (its first turn from the facing `dir`).</summary>
        public static Vector3 OrbitAt(Vector3 centre,Vector3 dir,float at,float time)
        {
            var d=Flat(dir);float a0=(float)Math.Atan2(d.X,d.Z),a=a0+(float)(2*Math.PI*HaloTurns*Math.Max(0,time-at));float grow=Math.Min(1,Math.Max(0,(time-at)/.18f));float r=HaloRadius*(.35f+.65f*grow);
            return centre+new Vector3((float)Math.Sin(a)*r,.05f*(float)Math.Sin(a*2),(float)Math.Cos(a)*r);
        }
        /// <summary>Where the record has the disc at `time` (its sampled flight carried on; the flick's and the halo's paths), and whether it
        /// is out of the hand. `centre`: the wielder's middle now (the halo circles it, the return homes on it).</summary>
        public static bool DiscAt(NativeGlaiveKit k,float time,Vector3 centre,out Vector3 at)
        {
            at=centre;if(k==null||k.Disc==Hand||k.Disc==Gone)return false;
            switch(k.Disc)
            {
                case Flick:at=FlickAt(N(k.DiscFrom),N(k.DiscDir),(time-k.DiscAt)/FlickTime);return true;
                case Orbit:at=OrbitAt(centre,N(k.DiscDir),k.DiscAt,time);return true;
                default:
                {
                    float dt=Math.Max(0,Math.Min(.25f,time-k.DiscSampleAt));at=N(k.DiscPos)+N(k.DiscVel)*dt;
                    // Coming back it homes on the hand: drawn closing on where the wielder is now.
                    if(k.Disc==Back){var to=centre+Vector3.UnitY*HandHeight-at;float l=to.Length();if(l<ReturnSpeed*dt&&l>1e-4f)at=centre+Vector3.UnitY*HandHeight;}
                    return true;
                }
            }
        }
        /// <summary>The ring in the hand (0..1): gone while it is out or burst, grown back as it re-forms.</summary>
        public static float InHand(NativeGlaiveKit k,float time)
        {
            if(k==null)return 1;if(k.Disc==Hand){float u=(time-k.DiscAt)/.12f;return k.DiscAt>-50&&u<1&&k.Reformed?Math.Max(0,u):1;}return 0;
        }
        private static Vector3 Flat(Vector3 v){v.Y=0;float l=v.Length();return l>1e-5f?v/l:Vector3.UnitZ;}
        internal static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
    }

    /// <summary>One moment of contact for the effects and the sound (the last 1.5 s): `Kind` one of NativeGlaive's hit kinds, where, which way,
    /// and who.</summary>
    [Serializable] public sealed class NativeGlaiveHit
    {
        public int Id,Victim;public string Kind;public float At;public V3 Pos,Dir;
    }
    /// <summary>
    /// The Glaive's replicated kit (docs/GLAIVE.md "The record"): everything the pose, the effects, the sound and the HUD read. The authority
    /// (Runtime NativeGlaiveAuthority.cs) writes it; copied in snapshots, late join and the killcam (`Actor.Glaive`), checked with Valid.
    /// </summary>
    [Serializable] public sealed class NativeGlaiveKit
    {
        /// <summary>What the pilot is doing, for the pose: the move's id and its times (CastRelease: its hit, or the moment it leaves the hand),
        /// the combo's step and when the combo lapses.</summary>
        public string CastKind;public float CastAt=-100,CastRelease,CastUntil,ComboUntil;public int CastStep,Combo;
        /// <summary>Absolute ready times: Q, E.</summary>
        public float[] Ready;
        /// <summary>The disc (NativeGlaive's states): since when, its sampled flight (`DiscPos`, `DiscVel` at `DiscSampleAt`), where a flick began
        /// and which way it or the halo set off (`DiscFrom`, `DiscDir`), its ricochets so far; `Reformed`: back in the hand from a burst.</summary>
        public int Disc,Bounces;public float DiscAt=-100,DiscSampleAt;public V3 DiscPos,DiscVel,DiscFrom,DiscDir;public bool Reformed;
        public NativeGlaiveHit[] Hits;
        public void CapturePresence(){}
        public void RestorePresence(){if(string.IsNullOrEmpty(CastKind))CastKind=null;}
        public NativeGlaiveKit ViewCopy()
        {
            var c=(NativeGlaiveKit)MemberwiseClone();c.Ready=Ready==null?null:(float[])Ready.Clone();
            if(Hits!=null){c.Hits=new NativeGlaiveHit[Hits.Length];for(int i=0;i<Hits.Length;i++){var h=Hits[i];c.Hits[i]=h==null?null:new NativeGlaiveHit{Id=h.Id,Victim=h.Victim,Kind=h.Kind,At=h.At,Pos=h.Pos,Dir=h.Dir};}}
            return c;
        }
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;bool K(string s)=>s==null||s.Length<=24;
            if(!K(CastKind)||!F(CastAt)||!F(CastRelease)||!F(CastUntil)||!F(ComboUntil)||CastStep<0||CastStep>=NativeGlaive.Steps||Combo<0||Combo>=NativeGlaive.Steps)return false;
            if(Disc<NativeGlaive.Hand||Disc>NativeGlaive.Flick||Bounces<0||Bounces>NativeGlaive.Ricochets||!F(DiscAt)||!F(DiscSampleAt)||!V(DiscPos)||!V(DiscVel)||!V(DiscFrom)||!V(DiscDir))return false;
            if(Ready!=null){if(Ready.Length>NativeGlaive.Slots)return false;foreach(var r in Ready)if(!F(r))return false;}
            if(Hits!=null){if(Hits.Length>NativeGlaive.MaxHits)return false;foreach(var h in Hits)if(h==null||!NativeGlaive.IsHit(h.Kind)||!F(h.At)||!V(h.Pos)||!V(h.Dir))return false;}
            return true;
        }
    }
    /// <summary>Where the presentation reads a pilot's Glaive kit: its snapshot actor's.</summary>
    public static class NativeGlaiveView
    {
        public static NativeGlaiveKit Of(Actor a)=>a?.SourceSpec==NativeGlaive.Spec?a.Glaive:null;
    }
}
