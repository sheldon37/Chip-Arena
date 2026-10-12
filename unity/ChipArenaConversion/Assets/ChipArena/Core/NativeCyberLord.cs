using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's replicated state as PROPOSED for Astra's rules (docs/NEW-KITS.md section 3, docs/CYBER-LORD.md):
    /// the hardlight combo step, one cast record per ability slot, the Data Dash path, the Firewall, the Siphon Link, the
    /// System Crash field and the Kernel Panic sequence. Nothing in the game writes it yet. The presentation never reads it
    /// directly: CyberLordFeed.Read is the one place that maps the replicated actor (this proposal, or Astra's real fields
    /// once they land) into CyberLordView, which every pose, effect, HUD card and camera reads. Times are match seconds,
    /// positions Unity world metres (feet height), yaws degrees (Unity: 0 faces +z).
    /// </summary>
    [Serializable] public sealed class NativeCyberKit
    {
        /// <summary>The hardlight combo: the current or last swing's step (0 Jab, 1 Cross, 2 Pulse Palm), its start and end.</summary>
        public int ComboStep;public float ComboAt=-100,ComboUntil=-100;
        /// <summary>One record per slot, CyberLordSpec.Q, E, One, Two, Three, Four (Q Data Dash ... 4 Kernel Panic).</summary>
        public NativeCyberCast[] Casts=NativeCyberCast.Slots();
        /// <summary>Q Data Dash: the dash path and the damaging trail's end.</summary>
        public V3 DashFrom,DashTo;public float TrailUntil=-100;
        /// <summary>E Firewall: the wall's base centre and yaw, when it formed and when it expires (or was broken).</summary>
        public V3 WallAt;public float WallYaw,WallBorn=-100,WallUntil=-100;
        /// <summary>2 Siphon Link: the tethered pilot, the link's start and end (expiry, lost sight or range).</summary>
        public int LinkTarget;public float LinkAt=-100,LinkUntil=-100;
        /// <summary>3 System Crash: the field's centre, when the slam made it and when it ends.</summary>
        public V3 CrashAt;public float CrashBorn=-100,CrashUntil=-100;
        /// <summary>4 Kernel Panic: the outcome (CyberLordSpec.Whiff, Connect, EndedEarly; 0 none), the connect (the cut-in's
        /// start), the sequence start (after the cut-in), the anchor (the victim's feet at the connect), the caster's facing, the
        /// seed, the victim, an early end's time, the wall-splat flag and the resolved waypoints (KernelPanicPath.Point*).</summary>
        public int PanicOutcome;public float PanicConnect=-100,PanicStart=-100,PanicEndedAt=-100;public V3 PanicAnchor;public float PanicYaw;public int PanicSeed,PanicVictim;public bool PanicWall;
        public V3[] PanicPoints=Array.Empty<V3>();
        public NativeCyberKit ViewCopy()
        {
            var c=(NativeCyberKit)MemberwiseClone();c.Casts=new NativeCyberCast[Casts?.Length??0];for(int i=0;i<c.Casts.Length;i++)c.Casts[i]=Casts[i]?.Copy()??new NativeCyberCast();
            c.PanicPoints=(V3[])(PanicPoints??Array.Empty<V3>()).Clone();return c;
        }
    }
    /// <summary>One ability's replicated cast: start, active (release) start, active end, recovery end and the slot's ready time.</summary>
    [Serializable] public sealed class NativeCyberCast
    {
        public float At=-100,Release=-100,Until=-100,RecoverUntil=-100,Ready=-100;
        public NativeCyberCast Copy()=>(NativeCyberCast)MemberwiseClone();
        public static NativeCyberCast[] Slots(){var s=new NativeCyberCast[CyberLordSpec.Slots];for(int i=0;i<s.Length;i++)s[i]=new NativeCyberCast();return s;}
    }

    /// <summary>The spec's numbers (docs/NEW-KITS.md section 3) the presentation keys on. Astra may tune them; the replicated
    /// windows always win where they exist, these are the fallbacks and the fixtures' timeline.</summary>
    public static class CyberLordSpec
    {
        public const string Id="cyberlord",Entrance="dev-cyberlord";
        /// <summary>Slots: Q, E, then 1-4 (the DEV fighters' 1-4 input path).</summary>
        public const int Q=0,E=1,One=2,Two=3,Three=4,Four=5,Slots=6;
        public static readonly string[] Keys={"Q","E","1","2","3","4"};
        public static readonly string[] Names={"Data Dash","Firewall","Packet Burst","Siphon Link","System Crash","Kernel Panic"};
        public static readonly string[] Ids={"data-dash","firewall","packet-burst","siphon-link","system-crash","kernel-panic"};
        public static readonly float[] Cooldowns={6,12,4,10,14,30},Costs={20,35,15,25,40,60};
        /// <summary>The hardlight combo: Jab .28 s, Cross .30 s, Pulse Palm .45 s; the reset after .6 s idle; range, arc, palm cone.</summary>
        public static readonly float[] ComboDurations={.28f,.30f,.45f};
        public static readonly float[] ComboDamage={12,14,22};
        public const float ComboReset=.6f,ComboRange=2.2f,ComboArc=1.4f,PalmCone=1.6f,ManaPerHit=6,Mana=100;
        /// <summary>Where each combo step's hit lands in its duration (the pack takes' impacts: jab .42, cross .41, palm .82).</summary>
        public static readonly float[] ComboHitShare={.42f,.41f,.62f};
        public const float DashDistance=7,TrailSeconds=1.2f,TrailTick=.3f;
        public const float WallWidth=5,WallHeight=2.5f,WallAhead=.8f,WallSeconds=4,WallForm=.28f,WallCollapse=.35f;
        public const float BurstRange=6,BurstAngle=50,BurstShards=8;
        public const float LinkRange=12,LinkBreak=15,LinkSeconds=2.5f;
        public const float CrashWindup=.5f,CrashRadius=4,CrashStun=1;
        /// <summary>Kernel Panic: the opener (windup, lunge, capsule radius, whiff recovery), the cut-in and the sequence.</summary>
        public const float PanicWindup=.25f,PanicLunge=6,PanicRadius=.6f,PanicWhiff=.6f,CutIn=.6f;
        public const int Whiff=1,Connect=2,EndedEarly=3;
        /// <summary>Fixture casts (the proposal leaves windup/active/recovery to the rules): per slot [windup, active, recovery].</summary>
        public static readonly float[][] Timing={new[]{.08f,.22f,.18f},new[]{.3f,.12f,.3f},new[]{.18f,.1f,.32f},new[]{.2f,LinkSeconds,.25f},new[]{CrashWindup,.12f,.4f},new[]{PanicWindup,.2f,PanicWhiff}};
        public static int Slot(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return i;return -1;}
    }

    /// <summary>What a cyber event is (CyberLordFeed.Hit maps its Detail).</summary>
    public enum CyberHit{None,Jab,Cross,Palm,Dash,Firewall,Burst,Siphon,Crash,Panic1,Panic2,Panic3,Panic4,Panic5}

    /// <summary>One ability's window at a moment, from the view.</summary>
    public struct CyberPhase
    {
        public bool Live;public float At,Release,Until,End,Age;
        /// <summary>0 windup, 1 active, 2 recovery.</summary>
        public int Part;
        /// <summary>The share of the current part done (0..1).</summary>
        public float U;
    }

    /// <summary>
    /// The presentation's copy of one Cyber Lord pilot's replicated state for a frame (CyberLordFeed.Read fills it; no
    /// allocation once made). Every Cyber Lord pose, effect, HUD card, sound and camera reads this and the replicated actor
    /// and events only: a copied actor draws the same frame (Tests/CyberLordChecks).
    /// </summary>
    public sealed class CyberLordView
    {
        public int Id;public bool Alive;public V3 Feet;public float Yaw;
        public int ComboStep;public float ComboAt=-100,ComboUntil=-100,ComboHit=-100;
        public readonly float[] At=new float[CyberLordSpec.Slots],Release=new float[CyberLordSpec.Slots],Until=new float[CyberLordSpec.Slots],RecoverUntil=new float[CyberLordSpec.Slots],Ready=new float[CyberLordSpec.Slots];
        public V3 DashFrom,DashTo;public float TrailUntil=-100;
        public V3 WallAt;public float WallYaw,WallBorn=-100,WallUntil=-100;
        public int LinkTarget;public float LinkAt=-100,LinkUntil=-100;
        public V3 CrashAt;public float CrashBorn=-100,CrashUntil=-100;
        public int PanicOutcome;public float PanicConnect=-100,PanicStart=-100,PanicEndedAt=-100;public V3 PanicAnchor;public float PanicYaw;public int PanicSeed,PanicVictim;public bool PanicWall;
        public readonly Vector3[] PanicPoints=new Vector3[KernelPanicPath.Points];public int PanicPointCount;
        public void Clear()
        {
            Id=0;Alive=false;Feet=default;Yaw=0;ComboStep=0;ComboAt=ComboUntil=ComboHit=-100;for(int i=0;i<CyberLordSpec.Slots;i++)At[i]=Release[i]=Until[i]=RecoverUntil[i]=Ready[i]=-100;
            DashFrom=DashTo=default;TrailUntil=-100;WallAt=default;WallYaw=0;WallBorn=WallUntil=-100;LinkTarget=0;LinkAt=LinkUntil=-100;CrashAt=default;CrashBorn=CrashUntil=-100;
            PanicOutcome=0;PanicConnect=PanicStart=PanicEndedAt=-100;PanicAnchor=default;PanicYaw=0;PanicSeed=PanicVictim=0;PanicWall=false;PanicPointCount=0;
        }
        /// <summary>Slot `slot`'s window at `time` (Live from its start to the end of its recovery).</summary>
        public CyberPhase Phase(int slot,float time)
        {
            var p=new CyberPhase{At=At[slot],Release=Release[slot],Until=Until[slot]};p.End=Math.Max(p.Until,RecoverUntil[slot]);
            if(!(p.At>-90)||time<p.At||time>=p.End)return p;p.Live=true;p.Age=time-p.At;float rel=p.Release>p.At?p.Release:p.At;
            if(time<rel){p.Part=0;p.U=Clamp01((time-p.At)/Math.Max(1e-4f,rel-p.At));}
            else if(time<p.Until){p.Part=1;p.U=Clamp01((time-rel)/Math.Max(1e-4f,p.Until-rel));}
            else{p.Part=2;p.U=Clamp01((time-p.Until)/Math.Max(1e-4f,p.End-p.Until));}
            return p;
        }
        /// <summary>The live combo swing at `time`: its step, age and the swing's duration.</summary>
        public bool Combo(float time,out int step,out float age,out float duration)
        {
            step=ComboStep;age=time-ComboAt;duration=ComboUntil-ComboAt;return ComboAt>-90&&duration>0&&age>=0&&age<duration;
        }
        /// <summary>The Kernel Panic sequence's age at `time` (after the cut-in), or NaN outside it.</summary>
        public float PanicAge(float time)=>PanicOutcome==CyberLordSpec.Connect||PanicOutcome==CyberLordSpec.EndedEarly?time-PanicStart:float.NaN;
        public bool PanicLive(float time)
        {
            if(PanicOutcome!=CyberLordSpec.Connect&&PanicOutcome!=CyberLordSpec.EndedEarly||!(PanicConnect>-90))return false;
            float end=PanicOutcome==CyberLordSpec.EndedEarly&&PanicEndedAt>-90?PanicEndedAt:PanicStart+KernelPanicPath.End;return time>=PanicConnect&&time<end;
        }
        public Vector3 Point(int i)=>i>=0&&i<PanicPointCount?PanicPoints[i]:new Vector3(PanicAnchor.x,PanicAnchor.y,PanicAnchor.z);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// THE field mapping (owner brief: "Keep the field mapping in one place, so it adapts when Astra's real names land"):
    /// the replicated actor into CyberLordView, and a cyber event's Detail into what it is. Today it reads the proposed
    /// NativeCyberKit (Actor.CyberKit) and the actor's standard melee swing fields for the combo (Combo, AttackAt/AttackUntil,
    /// SwingAt); when Astra's fields land, only this class changes. Allocation-free.
    /// </summary>
    public static class CyberLordFeed
    {
        public static bool Handles(string spec)=>spec==CyberLordSpec.Id;
        public static bool Handles(Actor a)=>a!=null&&a.SourceSpec==CyberLordSpec.Id;
        /// <summary>Fills `v` from the replicated actor; false (v cleared) for any other pilot.</summary>
        public static bool Read(Actor a,CyberLordView v)
        {
            v.Clear();if(!Handles(a))return false;v.Id=a.Id;v.Alive=a.Alive;v.Feet=a.Position;v.Yaw=a.Yaw;
            var k=a.CyberKit;
            // The combo: the kit's own step and window, else the standard melee swing the rules may reuse (Combo, AttackAt/AttackUntil).
            if(k!=null&&k.ComboAt>-90){v.ComboStep=Mod3(k.ComboStep);v.ComboAt=k.ComboAt;v.ComboUntil=k.ComboUntil;}
            else if(a.AttackAt>-90){v.ComboStep=Mod3(a.Combo);v.ComboAt=a.AttackAt;v.ComboUntil=a.AttackUntil>a.AttackAt?a.AttackUntil:a.AttackAt+CyberLordSpec.ComboDurations[v.ComboStep];}
            if(v.ComboAt>-90)v.ComboHit=a.SwingAt>=v.ComboAt-1e-4f&&a.SwingAt<v.ComboUntil?a.SwingAt:v.ComboAt+(v.ComboUntil-v.ComboAt)*CyberLordSpec.ComboHitShare[v.ComboStep];
            if(k==null)return true;
            int n=Math.Min(CyberLordSpec.Slots,k.Casts?.Length??0);
            for(int i=0;i<n;i++){var c=k.Casts[i];if(c==null)continue;v.At[i]=c.At;v.Release[i]=c.Release;v.Until[i]=c.Until;v.RecoverUntil[i]=c.RecoverUntil;v.Ready[i]=c.Ready;}
            v.DashFrom=k.DashFrom;v.DashTo=k.DashTo;v.TrailUntil=k.TrailUntil;
            v.WallAt=k.WallAt;v.WallYaw=k.WallYaw;v.WallBorn=k.WallBorn;v.WallUntil=k.WallUntil;
            v.LinkTarget=k.LinkTarget;v.LinkAt=k.LinkAt;v.LinkUntil=k.LinkUntil;
            v.CrashAt=k.CrashAt;v.CrashBorn=k.CrashBorn;v.CrashUntil=k.CrashUntil;
            v.PanicOutcome=k.PanicOutcome;v.PanicConnect=k.PanicConnect;v.PanicStart=k.PanicStart;v.PanicEndedAt=k.PanicEndedAt;v.PanicAnchor=k.PanicAnchor;v.PanicYaw=k.PanicYaw;
            v.PanicSeed=k.PanicSeed;v.PanicVictim=k.PanicVictim;v.PanicWall=k.PanicWall;
            int m=Math.Min(KernelPanicPath.Points,k.PanicPoints?.Length??0);for(int i=0;i<m;i++){var p=k.PanicPoints[i];v.PanicPoints[i]=new Vector3(p.x,p.y,p.z);}v.PanicPointCount=m;
            return true;
        }
        private static int Mod3(int x)=>((x%3)+3)%3;
        /// <summary>The proposed per-ability event Details (NEW-KITS section 3), in CyberHit order from Jab.</summary>
        public static readonly string[] Details={"cyber-jab","cyber-cross","cyber-palm","data-dash","firewall","packet-burst","siphon-link","system-crash","kernel-panic-1","kernel-panic-2","kernel-panic-3","kernel-panic-4","kernel-panic-5"};
        public static CyberHit Hit(string detail)
        {
            if(detail==null)return CyberHit.None;
            for(int i=0;i<Details.Length;i++)if(detail==Details[i])return (CyberHit)(i+1);
            return CyberHit.None;
        }
        /// <summary>The Kernel Panic step (1..5) of an event, 0 for none.</summary>
        public static int PanicStep(CyberHit h)=>h>=CyberHit.Panic1&&h<=CyberHit.Panic5?h-CyberHit.Panic1+1:0;
        /// <summary>The event kinds the presentation reads with a cyber Detail: hits, ability hits, heals (the Siphon's) and absorbs (the Firewall's).</summary>
        public static bool Kind(EventKind k)=>k==EventKind.Hit||k==EventKind.AbilityHit||k==EventKind.Heal||k==EventKind.Absorb||k==EventKind.AbilityCast||k==EventKind.BurnTick;
    }

    /// <summary>
    /// Kernel Panic's choreography as replicated: the step table and the waypoints (resolved and arena-clamped by the host;
    /// NominalPoints is the spec's unclamped resolution, for fixtures and the render harness only), and where the caster and
    /// the victim are expected along it (the cameras' and effects' smooth path; the bodies always stand where the snapshot puts them).
    /// </summary>
    public static class KernelPanicPath
    {
        /// <summary>Step starts (s after the sequence start), the recovery and the end.</summary>
        public static readonly float[] Steps={0,.45f,.95f,1.35f,1.9f};
        public const float Recovery=2.6f,RecoverySeconds=.8f,End=Recovery+RecoverySeconds;
        /// <summary>Fork bomb: the three clones' strikes (s after the sequence start).</summary>
        public static readonly float[] Clones={1.35f,1.5f,1.65f};
        public const float Launch=8,LaunchRise=1.5f,KickUp=5,Shockwave=5,BlinkOffset=1.1f,AboveOffset=1.25f;
        /// <summary>Waypoints: the anchor (the victim at the connect), the launch's end (or the wall-splat point), the caster's
        /// blink-behind spot, the victim's apex, the caster's blink-above spot, the landing and column point, the caster's
        /// reappearance beside it, and the caster's feet at the connect.</summary>
        public const int Anchor=0,LaunchEnd=1,Behind=2,Apex=3,Above=4,Landing=5,Reappear=6,Start=7,Points=8;
        /// <summary>When step `step` (1..5) lands, s into the sequence: each step's start, the kick .1 s and the hammer .08 s in
        /// (the takes' impacts, the hit-stops, the screen glitch and the fixtures' hit events all key on it).</summary>
        public static float HitAge(int step)=>step<1||step>5?float.NaN:Steps[step-1]+(step==2?.1f:step==3?.08f:0);
        public static int Step(float age){int s=0;for(int i=0;i<Steps.Length;i++)if(age>=Steps[i])s=i+1;return age<0?0:s;}
        public static Vector3 Forward(float yawDegrees){double r=yawDegrees*Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        /// <summary>The spec's unclamped waypoints from the anchor, the caster's feet and facing (fixtures; the host clamps its own).</summary>
        public static void NominalPoints(Vector3 anchor,Vector3 caster,float yaw,Vector3[] into)
        {
            var f=Forward(yaw);var up=Vector3.UnitY;
            into[Anchor]=anchor;into[Start]=caster;into[LaunchEnd]=anchor+f*Launch+up*LaunchRise;into[Behind]=new Vector3(into[LaunchEnd].X,0,into[LaunchEnd].Z)+f*BlinkOffset+up*Math.Max(0,LaunchRise-.9f);
            into[Apex]=into[LaunchEnd]+up*KickUp;into[Above]=into[Apex]+up*AboveOffset-f*.15f;into[Landing]=new Vector3(into[Apex].X,anchor.Y,into[Apex].Z)+f*.6f;
            into[Reappear]=into[Landing]-f*1.05f;into[Reappear].Y=anchor.Y;
        }
        private static float Ease(float x){x=x<0?0:x>1?1:x;return 1-(1-x)*(1-x)*(1-x);}
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        /// <summary>Where the victim (feet) is expected `age` s into the sequence (before it: the anchor).</summary>
        public static Vector3 Victim(CyberLordView v,float age)
        {
            var a=v.Point(Anchor);if(!(age>0))return a;var l=v.Point(LaunchEnd);
            if(age<Steps[1]){float u=Ease(age/Steps[1]);var p=Vector3.Lerp(a,l,u);p.Y=a.Y+(l.Y-a.Y)*(float)Math.Sin(u*Math.PI*.5f);return p;}
            var apex=v.Point(Apex);if(age<Steps[2]){float u=Ease((age-Steps[1])/(Steps[2]-Steps[1]));return Vector3.Lerp(l,apex,u);}
            var land=v.Point(Landing);
            // The hammer drives it down; it falls through the clones' strikes and is slammed onto the landing at step 5.
            if(age<Steps[4]){float u=(age-Steps[2])/(Steps[4]-Steps[2]);float k=u*u*(.35f+.65f*u);var p=Vector3.Lerp(apex,land,k);p.Y=apex.Y+(land.Y-apex.Y)*k;return p;}
            return land;
        }
        /// <summary>Where the caster (feet) is expected `age` s into the sequence, and whether it is shown (hidden through the fork bomb).</summary>
        public static Vector3 Caster(CyberLordView v,float age,out bool shown)
        {
            shown=true;if(!(age>=Steps[1]))return v.Point(Start);
            if(age<Steps[2])return v.Point(Behind);
            if(age<Steps[3]){var a=v.Point(Above);float u=Smooth((age-Steps[2])/(Steps[3]-Steps[2]));a.Y-=1.4f*u*u;return a;}
            if(age<Steps[4]){shown=age<Steps[3]+.08f||age>Steps[4]-.06f;return v.Point(Reappear);}
            return v.Point(Reappear);
        }
        /// <summary>Whether the caster is drawn at `age` (glitched out through the fork bomb, the clones striking for it).</summary>
        public static float CasterSolid(float age)
        {
            if(!(age>Steps[3])||age>=Steps[4])return 1;
            float outU=Smooth((age-Steps[3])/.12f),inU=Smooth((age-(Steps[4]-.1f))/.1f);return Math.Max(1-outU,inU);
        }
        /// <summary>The camera's subject: between the victim and the caster, weighted to the victim.</summary>
        public static Vector3 Focus(CyberLordView v,float age)
        {
            var victim=Victim(v,age);var caster=Caster(v,age,out bool shown);return shown?Vector3.Lerp(victim,caster,.3f):victim;
        }
    }
}
