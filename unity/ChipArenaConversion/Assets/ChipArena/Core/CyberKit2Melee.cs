using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The proposed v2 wire record for the Cyber Lord's melee and shared slots (docs/CYBER-LORD.md "Kit v2", the Astra prompt):
    /// what her host would write each tick once the v2 kit lands (embedded in her kit record, its Version 2). Until then the Dev
    /// Sandbox preview, the checks and the render harness write it (CyberKit2MeleeFixture) and CyberLordFeed reads it through
    /// CyberKit2MeleeSource; only CyberKit2MeleeFeed names its fields. The ranged slots are the sibling record's. Points are
    /// world metres: the caster's own points are Rigidbody centres as v1's (the feet .865 m below), the anchor and the bursts
    /// are where they happen, the meteor's target and the clones are on the floor. -100 marks "none".
    /// </summary>
    [Serializable] public sealed class CyberKit2MeleeRecord
    {
        /// <summary>CyberKit2Spec.Version (2) when the v2 kit is live; any other value reads as v1.</summary>
        public int Version;
        /// <summary>Per slot (CyberKit2Spec order; this record fills Q, E, the melee LMB, 1, 2 and 3): the cast's start, release,
        /// end and the cooldown's ready time, and the time an interrupt cut it (-100 none).</summary>
        public float[] At=New(),Release=New(),Until=New(),Ready=New(),CutAt=New();
        /// <summary>The melee LMB's step (0..4: jab, cross, spinning backfist, rising uppercut, Pulse Palm) for its window.</summary>
        public int Combo=-1;
        /// <summary>Q Uplink: the caster's centre at the cast, the cable's hit point, the yanked pilot (0 none), the outcome
        /// (CyberKit2Melee.Zip / Yank / Miss; 0 still flying), when the cable latched and when the zip or the yank arrives.</summary>
        public V3 UplinkFrom,UplinkAnchor;public int UplinkTarget,UplinkOutcome;public float UplinkLatchAt=-100,UplinkArriveAt=-100;
        /// <summary>E Overclock: the power state's start (the cast's release) and end.</summary>
        public float OverclockAt=-100,OverclockUntil=-100;
        /// <summary>1 Fork Bomb: the clones' life (from the release) and per clone (CyberKit2Spec.Clones) its feet and yaw (the
        /// host's, per tick), its last strike and its target (0 none), and its burst's time and point (-100 none).</summary>
        public float ForkAt=-100,ForkUntil=-100;public V3[] ClonePos=new V3[CyberKit2Spec.Clones];public float[] CloneYaw=new float[CyberKit2Spec.Clones],CloneStrikeAt=New(CyberKit2Spec.Clones),CloneBurstAt=New(CyberKit2Spec.Clones);
        public int[] CloneTarget=new int[CyberKit2Spec.Clones];public V3[] CloneBurst=new V3[CyberKit2Spec.Clones];
        /// <summary>2 Glitch Cut: the dash's two centres, the marked pilots (up to MaxMarks) and when they detonate.</summary>
        public V3 CutFrom,CutTo;public int[] Marked=new int[MaxMarks];public int MarkedCount;public float DetonateAt=-100;
        /// <summary>3 Meteor Byte: the caster's centre at the cast and the reticle's floor point at the dive's start (the hover's
        /// reticle follows the live aim until then), and the landing.</summary>
        public V3 MeteorFrom,MeteorTarget;public float LandAt=-100;
        public const int MaxMarks=8;
        private static float[] New(int n=CyberKit2Spec.Slots){var a=new float[n];for(int i=0;i<n;i++)a[i]=-100;return a;}
        public CyberKit2MeleeRecord ViewCopy()
        {
            var c=(CyberKit2MeleeRecord)MemberwiseClone();c.At=(float[])At.Clone();c.Release=(float[])Release.Clone();c.Until=(float[])Until.Clone();c.Ready=(float[])Ready.Clone();c.CutAt=(float[])CutAt.Clone();
            c.ClonePos=(V3[])ClonePos.Clone();c.CloneYaw=(float[])CloneYaw.Clone();c.CloneStrikeAt=(float[])CloneStrikeAt.Clone();c.CloneBurstAt=(float[])CloneBurstAt.Clone();c.CloneTarget=(int[])CloneTarget.Clone();c.CloneBurst=(V3[])CloneBurst.Clone();
            c.Marked=(int[])Marked.Clone();return c;
        }
    }
    /// <summary>Where a v2 record comes from before Astra's host writes it (the preview, the fixtures, the clones' stand-ins).</summary>
    public interface ICyberKit2MeleeSource{CyberKit2MeleeRecord Of(int actor);}
    /// <summary>A plain store of records by actor id (allocation only when a pilot is first seen).</summary>
    public sealed class CyberKit2MeleeSource:ICyberKit2MeleeSource
    {
        private readonly Dictionary<int,CyberKit2MeleeRecord> records=new Dictionary<int,CyberKit2MeleeRecord>();
        public CyberKit2MeleeRecord Of(int actor)=>records.TryGetValue(actor,out var r)?r:null;
        public CyberKit2MeleeRecord For(int actor){if(!records.TryGetValue(actor,out var r)){r=new CyberKit2MeleeRecord{Version=CyberKit2Spec.Version};records[actor]=r;}return r;}
        public void Remove(int actor)=>records.Remove(actor);
        public void Clear()=>records.Clear();
        public bool Any=>records.Count>0;
    }

    /// <summary>
    /// The presentation's copy of one pilot's v2 melee and shared state for a frame (CyberLordView.Kit2; CyberKit2MeleeFeed fills
    /// it): Live only while the record's Version is 2. Points are feet unless named otherwise. No allocation once made.
    /// </summary>
    public sealed class CyberKit2MeleeView
    {
        public bool Live;
        /// <summary>The combo's swing: its step (0..4), start, hit (the release) and end (cut where an interrupt cut it).</summary>
        public int ComboStep;public float ComboAt=-100,ComboHit=-100,ComboUntil=-100;
        public readonly float[] At=new float[CyberKit2Spec.Slots],Release=new float[CyberKit2Spec.Slots],Until=new float[CyberKit2Spec.Slots],RecoverUntil=new float[CyberKit2Spec.Slots],Ready=new float[CyberKit2Spec.Slots];
        public readonly bool[] Cut=new bool[CyberKit2Spec.Slots];
        public Vector3 UplinkFrom,UplinkAnchor;public int UplinkTarget,UplinkOutcome;public float UplinkLatchAt=-100,UplinkArriveAt=-100;
        public float OverclockAt=-100,OverclockUntil=-100;
        public float ForkAt=-100,ForkUntil=-100;public readonly Vector3[] Clone=new Vector3[CyberKit2Spec.Clones],Burst=new Vector3[CyberKit2Spec.Clones];
        public readonly float[] CloneYaw=new float[CyberKit2Spec.Clones],CloneStrikeAt=new float[CyberKit2Spec.Clones],BurstAt=new float[CyberKit2Spec.Clones];public readonly int[] CloneTarget=new int[CyberKit2Spec.Clones];
        public Vector3 CutFrom,CutTo;public readonly int[] Marked=new int[CyberKit2MeleeRecord.MaxMarks];public int MarkedCount;public float DetonateAt=-100;
        public Vector3 MeteorFrom,MeteorTarget;public float LandAt=-100;
        public void Clear()
        {
            Live=false;ComboStep=0;ComboAt=ComboHit=ComboUntil=-100;for(int i=0;i<CyberKit2Spec.Slots;i++){At[i]=Release[i]=Until[i]=RecoverUntil[i]=Ready[i]=-100;Cut[i]=false;}
            UplinkFrom=UplinkAnchor=default;UplinkTarget=UplinkOutcome=0;UplinkLatchAt=UplinkArriveAt=-100;OverclockAt=OverclockUntil=-100;ForkAt=ForkUntil=-100;
            for(int i=0;i<CyberKit2Spec.Clones;i++){Clone[i]=Burst[i]=default;CloneYaw[i]=0;CloneStrikeAt[i]=BurstAt[i]=-100;CloneTarget[i]=0;}
            CutFrom=CutTo=default;MarkedCount=0;for(int i=0;i<Marked.Length;i++)Marked[i]=0;DetonateAt=-100;MeteorFrom=MeteorTarget=default;LandAt=-100;
        }
        /// <summary>Slot `slot`'s window at `time` (as CyberLordView.Phase: windup, active, recovery).</summary>
        public CyberPhase Phase(int slot,float time)
        {
            var p=new CyberPhase{At=At[slot],Release=Release[slot],Until=Until[slot]};p.End=Math.Max(p.Until,RecoverUntil[slot]);
            if(!(p.At>-90)||time<p.At||time>=p.End)return p;p.Live=true;p.Age=time-p.At;float rel=p.Release>p.At?p.Release:p.At;
            if(time<rel){p.Part=0;p.U=Clamp01((time-p.At)/Math.Max(1e-4f,rel-p.At));}
            else if(time<p.Until){p.Part=1;p.U=Clamp01((time-rel)/Math.Max(1e-4f,p.Until-rel));}
            else{p.Part=2;p.U=Clamp01((time-p.Until)/Math.Max(1e-4f,p.End-p.Until));}
            return p;
        }
        /// <summary>Whether the Overclock power state is on at `time` (its strength eases in over .12 s and out over .3 s).</summary>
        public float Overclock(float time)=>OverclockAt>-90&&time>=OverclockAt&&time<OverclockUntil+.3f?Smooth((time-OverclockAt)/.12f)*(1-Smooth((time-OverclockUntil)/.3f)):0;
        /// <summary>Whether `id` carries a Glitch Cut mark.</summary>
        public bool MarkedPilot(int id){for(int i=0;i<MarkedCount;i++)if(Marked[i]==id)return true;return false;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }

    /// <summary>
    /// The v2 record into the view: the one mapping (CyberLordFeed.Read calls Read at its end). With a live record (Version 2)
    /// the v1 melee combo and the v1 Q to 3 windows are cleared (v2 replaces them); Kernel Panic and the ranged mode stay as
    /// they were mapped. With none, or any other Version, nothing changes: v1 draws exactly as before.
    /// </summary>
    public static class CyberKit2MeleeFeed
    {
        /// <summary>Kit v2's melee and shared event Details (the contract's; the combo's jab, cross and palm keep v1's) in Kit2Hit order.</summary>
        public static readonly string[] Details={"cyber-spin","cyber-rise","uplink-zip","uplink-yank","overclock","fork-strike","fork-burst","glitch-mark","glitch-detonate","meteor-impact"};
        public static Kit2Hit Hit(string detail){if(detail==null)return Kit2Hit.None;for(int i=0;i<Details.Length;i++)if(detail==Details[i])return (Kit2Hit)(i+1);return Kit2Hit.None;}
        /// <summary>The record for a pilot: the preview's or fixtures' source, else a fork-bomb clone's stand-in.</summary>
        public static CyberKit2MeleeRecord Of(Actor a)=>a==null?null:CyberLordFeed.Kit2MeleeSource?.Of(a.Id)??CyberKit2Melee.Clones.Of(a.Id);
        public static void Read(Actor a,CyberLordView v)
        {
            var k=v.Kit2;k.Clear();var r=Of(a);if(r==null||r.Version!=CyberKit2Spec.Version)return;Map(r,k);
            // v2 replaces the v1 combo and the v1 Q, E, 1, 2 and 3 (Kernel Panic, slot Four, and the ranged mode stay).
            v.ComboStep=0;v.ComboAt=v.ComboUntil=v.ComboHit=-100;for(int i=0;i<CyberLordSpec.Four;i++)v.At[i]=v.Release[i]=v.Until[i]=v.RecoverUntil[i]=v.Ready[i]=-100;
            v.DashFrom=v.DashTo=default;v.TrailUntil=-100;v.WallBorn=v.WallUntil=-100;v.LinkTarget=0;v.LinkAt=v.LinkUntil=-100;v.CrashBorn=v.CrashUntil=-100;
        }
        private const float C=CyberLordFeed.BodyCentre;
        private static Vector3 Feet(V3 p)=>new Vector3(p.x,p.y-C,p.z);
        private static Vector3 W(V3 p)=>new Vector3(p.x,p.y,p.z);
        private static readonly int[] slots={CyberKit2Spec.Q,CyberKit2Spec.E,CyberKit2Spec.MeleeLmb,CyberKit2Spec.Fork,CyberKit2Spec.Cut,CyberKit2Spec.Meteor};
        public static void Map(CyberKit2MeleeRecord r,CyberKit2MeleeView k)
        {
            k.Live=true;
            foreach(int s in slots)
            {
                float at=r.At[s];if(!(at>-90)){k.Ready[s]=r.Ready[s];continue;}
                bool cut=r.CutAt[s]>=at;float until=Math.Max(at,cut?Math.Min(r.Until[s],r.CutAt[s]):r.Until[s]);
                k.At[s]=at;k.Release[s]=Math.Max(at,Math.Min(r.Release[s],until));k.Until[s]=until;k.Ready[s]=r.Ready[s];k.Cut[s]=cut;k.RecoverUntil[s]=cut?until:until+CyberKit2Spec.Settle[s];
            }
            int lmb=CyberKit2Spec.MeleeLmb;
            if(k.At[lmb]>-90&&r.Combo>=0){k.ComboStep=Math.Min(CyberKit2Spec.ComboSteps-1,r.Combo);k.ComboAt=k.At[lmb];k.ComboUntil=Math.Max(k.At[lmb]+.01f,k.Until[lmb]);k.ComboHit=k.Release[lmb];}
            k.UplinkFrom=Feet(r.UplinkFrom);k.UplinkAnchor=W(r.UplinkAnchor);k.UplinkTarget=r.UplinkTarget;k.UplinkOutcome=r.UplinkOutcome;k.UplinkLatchAt=r.UplinkLatchAt;k.UplinkArriveAt=r.UplinkArriveAt;
            k.OverclockAt=r.OverclockAt;k.OverclockUntil=r.OverclockUntil;
            k.ForkAt=r.ForkAt;k.ForkUntil=r.ForkUntil;
            for(int i=0;i<CyberKit2Spec.Clones;i++){k.Clone[i]=W(r.ClonePos[i]);k.CloneYaw[i]=r.CloneYaw[i];k.CloneStrikeAt[i]=r.CloneStrikeAt[i];k.CloneTarget[i]=r.CloneTarget[i];k.BurstAt[i]=r.CloneBurstAt[i];k.Burst[i]=W(r.CloneBurst[i]);}
            k.CutFrom=Feet(r.CutFrom);k.CutTo=Feet(r.CutTo);k.MarkedCount=Math.Max(0,Math.Min(CyberKit2MeleeRecord.MaxMarks,r.MarkedCount));for(int i=0;i<k.MarkedCount;i++)k.Marked[i]=r.Marked[i];k.DetonateAt=r.DetonateAt;
            k.MeteorFrom=Feet(r.MeteorFrom);k.MeteorTarget=W(r.MeteorTarget);k.LandAt=r.LandAt;
        }
    }

    /// <summary>
    /// The v2 melee kit's shared presentation helpers beyond CyberKit2Spec's: the outcomes, where the host puts the caster's
    /// body through the zip, the dash-slash and the meteor (the fixtures' bodies and the effects' paths; the poses always read
    /// the replicated body), the clones' formation, and the clones' stand-in records. Pure.
    /// </summary>
    public static class CyberKit2Melee
    {
        public const int Flying=0,Zip=1,Yank=2,Miss=3;
        /// <summary>The cable's emitter above the body centre (the casting fist at the cast) and a zip's stop short of its anchor.</summary>
        public const float EmitterHeight=.42f,ZipStandoff=.7f;
        /// <summary>When a cable cast at `release` from `from` reaches `anchor` (CableSpeed).</summary>
        public static float LatchAt(float release,Vector3 from,Vector3 anchor)=>release+Vector3.Distance(from,anchor)/CyberKit2Spec.CableSpeed;
        /// <summary>A zip's arrival: the anchor's distance less the standoff at ZipSpeed, at most ZipMax after the latch.</summary>
        public static float ZipArrive(float latch,Vector3 from,Vector3 anchor)=>latch+Math.Min(CyberKit2Spec.ZipMax,Math.Max(0,Vector3.Distance(from,anchor)-ZipStandoff)/CyberKit2Spec.ZipSpeed);
        /// <summary>A miss's cable: out to full range and back (s after the release).</summary>
        public const float MissSeconds=2*CyberKit2Spec.UplinkRange/CyberKit2Spec.CableSpeed;
        /// <summary>The caster's body centre through a zip (from the latch to the arrival: constant speed, eased at both ends).</summary>
        public static Vector3 ZipBody(Vector3 from,Vector3 anchor,float latch,float arrive,float time)
        {
            var dir=anchor-from;float len=dir.Length();if(len<1e-4f)return from;dir/=len;var stop=from+dir*Math.Max(0,len-ZipStandoff);
            float u=Clamp01((time-latch)/Math.Max(1e-3f,arrive-latch));u=u*u*(3-2*u)*.25f+u*.75f;return Vector3.Lerp(from,stop,u);
        }
        /// <summary>A yanked pilot's body centre: from where it stood to YankStop ahead of the caster over YankSeconds (eased in hard).</summary>
        public static Vector3 YankBody(Vector3 victim,Vector3 casterCentre,Vector3 facing,float latch,float time)
        {
            var stop=casterCentre+Flat(facing)*CyberKit2Spec.YankStop;stop.Y=victim.Y;float u=Clamp01((time-latch)/CyberKit2Spec.YankSeconds);u=1-(1-u)*(1-u)*(1-u);return Vector3.Lerp(victim,stop,u);
        }
        /// <summary>The dash-slash's body centre (Release to Until over CutSeconds, a sharp ease).</summary>
        public static Vector3 CutBody(Vector3 from,Vector3 to,float release,float until,float time){float u=Clamp01((time-release)/Math.Max(1e-3f,until-release));u=1-(1-u)*(1-u);return Vector3.Lerp(from,to,u);}
        /// <summary>The meteor's phases after the cast's start: the crouch's end (the leap), the hover, the dive, the landing.</summary>
        public static float LeapAt(float at)=>at+CyberKit2Spec.Windup[CyberKit2Spec.Meteor];
        public static float HoverAt(float at)=>LeapAt(at)+CyberKit2Spec.LeapSeconds;
        public static float DiveAt(float at)=>HoverAt(at)+CyberKit2Spec.HoverSeconds;
        /// <summary>The meteor's body centre: up LeapArc over the leap (straight up), held through the hover, DiveShare of the way
        /// down to the target over the dive (the target's floor plus the body centre).</summary>
        public static Vector3 MeteorBody(Vector3 from,Vector3 targetFloor,float at,float time)
        {
            float leap=LeapAt(at),hover=HoverAt(at),dive=DiveAt(at);var apex=from+Vector3.UnitY*CyberKit2Spec.LeapHeight;
            if(time<leap)return from;if(time<hover)return from+Vector3.UnitY*CyberKit2Spec.LeapArc((time-leap)/CyberKit2Spec.LeapSeconds);if(time<dive)return apex;
            var land=targetFloor+Vector3.UnitY*CyberLordFeed.BodyCentre;return Vector3.Lerp(apex,land,CyberKit2Spec.DiveShare((time-dive)/CyberKit2Spec.DiveSeconds));
        }
        /// <summary>Clone `i`'s feet in formation round a caster at `feet` facing `yawDegrees` (CyberKit2Spec.ForkSlot, rotated).</summary>
        public static Vector3 Formation(int i,Vector3 feet,float yawDegrees)
        {
            CyberKit2Spec.ForkSlot(i,out float x,out float z);var f=KernelPanicPath.Forward(yawDegrees);var r=new Vector3(f.Z,0,-f.X);return feet+r*x+f*z;
        }
        /// <summary>The clones' stand-in records (their own ids: -2000 - caster*3 - index), posed by their own rigs from the caster's
        /// swings (the effects write them as they draw the clones, Core/CyberKit2MeleeFx.cs).</summary>
        public static readonly CyberKit2MeleeSource Clones=new CyberKit2MeleeSource();
        public static int CloneId(int caster,int index)=>-2000-Math.Abs(caster)*CyberKit2Spec.Clones-index;
        public static bool IsClone(int id)=>id<=-2000;
        private static Vector3 Flat(Vector3 v){v.Y=0;float l=v.Length();return l>1e-5f?v/l:Vector3.UnitZ;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// Writes the v2 record the way the proposed host would (on an open floor, attack rate and cooldown scale 1): the checks', the
    /// render harness's and the Dev Sandbox preview's stand-in for Astra's rules; nothing in the game's authority calls it. Each
    /// call stamps one cast's window and its own fields; the caller moves bodies with CyberKit2Melee's paths.
    /// </summary>
    public static class CyberKit2MeleeFixture
    {
        private static V3 V(Vector3 p)=>new V3(p.X,p.Y,p.Z);
        private static Vector3 N(V3 p)=>new Vector3(p.x,p.y,p.z);
        /// <summary>Slot `slot`'s window from `at` (CyberKit2Spec's windup and length, its cooldown and an empty cut).</summary>
        public static void Begin(CyberKit2MeleeRecord r,int slot,float at,float rate=1)
        {
            r.Version=CyberKit2Spec.Version;float scale=slot==CyberKit2Spec.MeleeLmb?1/Math.Max(.1f,rate):1;
            r.At[slot]=at;r.Release[slot]=at+CyberKit2Spec.Windup[slot]*scale;r.Until[slot]=at+CyberKit2Spec.Seconds[slot]*scale;r.Ready[slot]=at+CyberKit2Spec.Cooldowns[slot];r.CutAt[slot]=-100;
        }
        /// <summary>A combo swing `step` at `at` (attack rate `rate`): the hit at its release.</summary>
        public static void Combo(CyberKit2MeleeRecord r,int step,float at,float rate=1)
        {
            int s=CyberKit2Spec.MeleeLmb;Begin(r,s,at,rate);step=Math.Max(0,Math.Min(CyberKit2Spec.ComboSteps-1,step));r.Combo=step;
            r.Release[s]=at+CyberKit2Spec.ComboWindups[step]/rate;r.Until[s]=at+CyberKit2Spec.ComboSeconds[step]/rate;r.Ready[s]=r.Until[s];
        }
        /// <summary>An interrupt at `at` (a block, a roll, a stun): the live cast in `slot` ends there.</summary>
        public static void Cut(CyberKit2MeleeRecord r,int slot,float at){if(r.At[slot]>-90&&at>=r.At[slot]&&at<r.Until[slot]){r.CutAt[slot]=at;r.Until[slot]=at;r.Release[slot]=Math.Min(r.Release[slot],at);}}
        /// <summary>Q Uplink from the caster's centre `from` to `anchor` (a surface, or the pilot `target`'s centre): zip,
        /// yank or a miss (anchor ignored: full range along `dir`).</summary>
        public static void Uplink(CyberKit2MeleeRecord r,float at,Vector3 from,Vector3 anchor,int outcome,int target=0)
        {
            Begin(r,CyberKit2Spec.Q,at);float rel=r.Release[CyberKit2Spec.Q];var emit=from+Vector3.UnitY*CyberKit2Melee.EmitterHeight;
            r.UplinkFrom=V(from);r.UplinkAnchor=V(anchor);r.UplinkOutcome=outcome;r.UplinkTarget=outcome==CyberKit2Melee.Yank?target:0;
            float latch=CyberKit2Melee.LatchAt(rel,emit,anchor);r.UplinkLatchAt=outcome==CyberKit2Melee.Miss?-100:latch;
            r.UplinkArriveAt=outcome==CyberKit2Melee.Zip?CyberKit2Melee.ZipArrive(latch,from,anchor):outcome==CyberKit2Melee.Yank?latch+CyberKit2Spec.YankSeconds:rel+CyberKit2Melee.MissSeconds;
        }
        /// <summary>E Overclock: the power state from the release for OverclockSeconds.</summary>
        public static void Overclock(CyberKit2MeleeRecord r,float at)
        {
            Begin(r,CyberKit2Spec.E,at);r.OverclockAt=r.Release[CyberKit2Spec.E];r.OverclockUntil=r.OverclockAt+CyberKit2Spec.OverclockSeconds;
        }
        /// <summary>1 Fork Bomb: the clones form round the caster's feet `feet` (facing `yaw`) at the release and live ForkSeconds.</summary>
        public static void Fork(CyberKit2MeleeRecord r,float at,Vector3 feet,float yaw)
        {
            Begin(r,CyberKit2Spec.Fork,at);r.ForkAt=r.Release[CyberKit2Spec.Fork];r.ForkUntil=r.ForkAt+CyberKit2Spec.ForkSeconds;
            for(int i=0;i<CyberKit2Spec.Clones;i++){r.ClonePos[i]=V(CyberKit2Melee.Formation(i,feet,yaw));r.CloneYaw[i]=yaw;r.CloneStrikeAt[i]=-100;r.CloneTarget[i]=0;r.CloneBurstAt[i]=-100;r.CloneBurst[i]=default;}
        }
        /// <summary>The host's per-tick clone state: each clone in formation round the caster (a strike faces its target).</summary>
        public static void ForkFollow(CyberKit2MeleeRecord r,Vector3 feet,float yaw)
        {
            for(int i=0;i<CyberKit2Spec.Clones;i++){if(r.CloneBurstAt[i]>-90)continue;r.ClonePos[i]=V(CyberKit2Melee.Formation(i,feet,yaw));if(r.CloneTarget[i]==0)r.CloneYaw[i]=yaw;}
        }
        /// <summary>Clone `i` copies a swing at `at` on `target` standing at `targetFeet` (it turns to face it).</summary>
        public static void ForkStrike(CyberKit2MeleeRecord r,int i,float at,int target,Vector3 targetFeet)
        {
            r.CloneStrikeAt[i]=at;r.CloneTarget[i]=target;var d=targetFeet-N(r.ClonePos[i]);if(d.X*d.X+d.Z*d.Z>1e-4f)r.CloneYaw[i]=(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        }
        /// <summary>The detonation (the life's end or a recast at `at`): each clone dashes DetonateDash to its point and bursts.</summary>
        public static void ForkBurst(CyberKit2MeleeRecord r,float at,Vector3[] points)
        {
            for(int i=0;i<CyberKit2Spec.Clones;i++){r.CloneBurstAt[i]=at+CyberKit2Spec.DetonateDash;r.CloneBurst[i]=V(points[i]);}r.ForkUntil=Math.Min(r.ForkUntil,at);
        }
        /// <summary>2 Glitch Cut from the centre `from` to `to` (the host's path-constrained dash), marking `marked`.</summary>
        public static void GlitchCut(CyberKit2MeleeRecord r,float at,Vector3 from,Vector3 to,params int[] marked)
        {
            Begin(r,CyberKit2Spec.Cut,at);r.CutFrom=V(from);r.CutTo=V(to);r.MarkedCount=Math.Min(CyberKit2MeleeRecord.MaxMarks,marked.Length);for(int i=0;i<r.MarkedCount;i++)r.Marked[i]=marked[i];
            r.DetonateAt=r.Until[CyberKit2Spec.Cut]+CyberKit2Spec.MarkDelay;
        }
        /// <summary>3 Meteor Byte from the centre `from`, diving onto the floor point `target`.</summary>
        public static void Meteor(CyberKit2MeleeRecord r,float at,Vector3 from,Vector3 target)
        {
            Begin(r,CyberKit2Spec.Meteor,at);r.MeteorFrom=V(from);r.MeteorTarget=V(target);r.LandAt=at+CyberKit2Spec.MeteorTotal;
        }
    }
}
