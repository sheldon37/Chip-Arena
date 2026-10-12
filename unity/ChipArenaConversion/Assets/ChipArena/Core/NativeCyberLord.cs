using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's replicated state that is still a PROPOSAL for Astra's rules (docs/NEW-KITS.md section 3,
    /// docs/CYBER-LORD.md): the hardlight combo step, one cast record per ability slot, the Data Dash path, the Firewall, the
    /// Siphon Link and the System Crash field. Kernel Panic is NOT here: it is Astra's implemented contract (Actor.Kernel, a
    /// NativeKernelPanic, PR #138), read as it is. Nothing in the game writes this kit yet. The presentation never reads it
    /// directly: CyberLordFeed.Read is the one place that maps the replicated actor into CyberLordView, which every pose,
    /// effect, HUD card and camera reads. Times are match seconds, positions Unity world metres (feet height), yaws degrees
    /// (Unity: 0 faces +z).
    /// </summary>
    [Serializable] public sealed class NativeCyberKit
    {
        /// <summary>The hardlight combo: the current or last swing's step (0 Jab, 1 Cross, 2 Pulse Palm), its start and end.</summary>
        public int ComboStep;public float ComboAt=-100,ComboUntil=-100;
        /// <summary>One record per slot, CyberLordSpec.Q, E, One, Two, Three (Q Data Dash ... 3 System Crash); slot Four is unused
        /// (Kernel Panic's cast comes from Actor.Kernel).</summary>
        public NativeCyberCast[] Casts=NativeCyberCast.Slots();
        /// <summary>Q Data Dash: the dash path and the damaging trail's end.</summary>
        public V3 DashFrom,DashTo;public float TrailUntil=-100;
        /// <summary>E Firewall: the wall's base centre and yaw, when it formed and when it expires (or was broken).</summary>
        public V3 WallAt;public float WallYaw,WallBorn=-100,WallUntil=-100;
        /// <summary>2 Siphon Link: the tethered pilot, the link's start and end (expiry, lost sight or range).</summary>
        public int LinkTarget;public float LinkAt=-100,LinkUntil=-100;
        /// <summary>3 System Crash: the field's centre, when the slam made it and when it ends.</summary>
        public V3 CrashAt;public float CrashBorn=-100,CrashUntil=-100;
        public NativeCyberKit ViewCopy()
        {
            var c=(NativeCyberKit)MemberwiseClone();c.Casts=new NativeCyberCast[Casts?.Length??0];for(int i=0;i<c.Casts.Length;i++)c.Casts[i]=Casts[i]?.Copy()??new NativeCyberCast();return c;
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
        public static readonly float[] Cooldowns={6,12,4,10,14,NativeKernelPanicRules.Cooldown},Costs={20,35,15,25,40,NativeKernelPanicRules.Mana};
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
        /// <summary>Kernel Panic, Astra's numbers (NativeKernelPanicRules): the opener (windup, lunge range, capsule radius, whiff
        /// recovery) and the cut-in. The lunge itself is instant on the host (the body moves at the windup's end).</summary>
        public const float PanicWindup=NativeKernelPanicRules.Windup,PanicLunge=NativeKernelPanicRules.Range,PanicRadius=NativeKernelPanicRules.Radius,PanicWhiff=NativeKernelPanicRules.WhiffRecovery,CutIn=NativeKernelPanicRules.CutIn;
        /// <summary>The view's outcome (CyberLordView.PanicOutcome; 0 none or still winding up). Astra's NativeKernelPanic.Outcome
        /// (0 windup, 1 connect, 2 whiff, 3 interrupted, 4 complete) maps in CyberLordFeed.</summary>
        public const int Whiff=1,Connect=2,EndedEarly=3;
        /// <summary>The share of the windup the opener's lunge drive starts at (the take cocks, then drives to the contact).</summary>
        public const float PanicDrive=.6f;
        /// <summary>Fixture casts (the proposal leaves windup/active/recovery to the rules): per slot [windup, active, recovery].</summary>
        public static readonly float[][] Timing={new[]{.08f,.22f,.18f},new[]{.3f,.12f,.3f},new[]{.18f,.1f,.32f},new[]{.2f,LinkSeconds,.25f},new[]{CrashWindup,.12f,.4f},new[]{PanicWindup,0,PanicWhiff}};
        public static int Slot(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return i;return -1;}
    }

    /// <summary>What a cyber event is (CyberLordFeed.Hit maps its Detail).</summary>
    public enum CyberHit{None,Jab,Cross,Palm,Dash,Firewall,Burst,Siphon,Crash,Panic1,Panic2,Panic3,Panic4,Panic5,PanicWave}

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
        // Kernel Panic (from Astra's Actor.Kernel): the cast id, the outcome (CyberLordSpec codes), the connect (the cut-in's
        // start), the sequence start (after the cut-in), an early end's time, the anchor (the victim's feet at the connect),
        // the facing (unit, with the aim's pitch) and its yaw, the seed, the victim, the committed steps, the wall-splat flag,
        // both starts and the host's six resolved points each (feet; KernelPanicPath.Point names them).
        public int PanicId,PanicOutcome,PanicSeed,PanicVictim,PanicNext;public float PanicConnect=-100,PanicStart=-100,PanicEndedAt=-100;public V3 PanicAnchor;public float PanicYaw;public bool PanicWall;
        public Vector3 PanicFacing,PanicCasterStart,PanicVictimStart;
        public readonly Vector3[] CasterPoints=new Vector3[KernelPanicPath.HostPoints],VictimPoints=new Vector3[KernelPanicPath.HostPoints];public int PanicPointCount;
        public void Clear()
        {
            Id=0;Alive=false;Feet=default;Yaw=0;ComboStep=0;ComboAt=ComboUntil=ComboHit=-100;for(int i=0;i<CyberLordSpec.Slots;i++)At[i]=Release[i]=Until[i]=RecoverUntil[i]=Ready[i]=-100;
            DashFrom=DashTo=default;TrailUntil=-100;WallAt=default;WallYaw=0;WallBorn=WallUntil=-100;LinkTarget=0;LinkAt=LinkUntil=-100;CrashAt=default;CrashBorn=CrashUntil=-100;
            PanicId=PanicOutcome=PanicSeed=PanicVictim=PanicNext=0;PanicConnect=PanicStart=PanicEndedAt=-100;PanicAnchor=default;PanicYaw=0;PanicWall=false;PanicPointCount=0;
            PanicFacing=PanicCasterStart=PanicVictimStart=default;
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
        /// <summary>A named choreography point (KernelPanicPath.Anchor ... Fork) from the host's resolved points; the starts without them.</summary>
        public Vector3 Point(int i)
        {
            if(PanicPointCount<KernelPanicPath.HostPoints)return i==KernelPanicPath.Start||i==KernelPanicPath.Behind||i==KernelPanicPath.Above||i==KernelPanicPath.Fork||i==KernelPanicPath.Reappear?PanicCasterStart:PanicVictimStart;
            switch(i)
            {
                case KernelPanicPath.Anchor:return VictimPoints[0];case KernelPanicPath.LaunchEnd:return VictimPoints[1];case KernelPanicPath.Apex:return VictimPoints[2];
                case KernelPanicPath.HammerEnd:return VictimPoints[3];case KernelPanicPath.Landing:return VictimPoints[5];
                case KernelPanicPath.Start:return CasterPoints[0];case KernelPanicPath.Behind:return CasterPoints[1];case KernelPanicPath.Above:return CasterPoints[2];
                case KernelPanicPath.Fork:return CasterPoints[3];case KernelPanicPath.Reappear:return CasterPoints[4];
                default:return VictimPoints[0];
            }
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// THE field mapping (owner brief: "Keep the field mapping in one place, so it adapts when Astra's real names land"):
    /// the replicated actor into CyberLordView, and a cyber event's Detail into what it is. Kernel Panic reads Astra's
    /// implemented state (Actor.Kernel, NativeKernelPanic: positions are Rigidbody centres, the feet .865 m below; Outcome 0
    /// windup, 1 connect, 2 whiff, 3 interrupted, 4 complete) and her Details (kernel-panic-1 .. -5, kernel-panic-wave).
    /// The rest reads the still-proposed NativeCyberKit (Actor.CyberKit) and the actor's standard melee swing fields for the
    /// combo (Combo, AttackAt/AttackUntil, SwingAt); when those fields land, only this class changes. Allocation-free.
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
            // Kernel Panic: Astra's state, on both participants; the caster's own cast only (a victim's copy belongs to its caster).
            var kp=a.Kernel;if(kp!=null&&kp.Id>0&&kp.Caster==a.Id)Kernel(kp,v);
            if(k==null)return true;
            int n=Math.Min(CyberLordSpec.Four,k.Casts?.Length??0);
            for(int i=0;i<n;i++){var c=k.Casts[i];if(c==null)continue;v.At[i]=c.At;v.Release[i]=c.Release;v.Until[i]=c.Until;v.RecoverUntil[i]=c.RecoverUntil;v.Ready[i]=c.Ready;}
            v.DashFrom=k.DashFrom;v.DashTo=k.DashTo;v.TrailUntil=k.TrailUntil;
            v.WallAt=k.WallAt;v.WallYaw=k.WallYaw;v.WallBorn=k.WallBorn;v.WallUntil=k.WallUntil;
            v.LinkTarget=k.LinkTarget;v.LinkAt=k.LinkAt;v.LinkUntil=k.LinkUntil;
            v.CrashAt=k.CrashAt;v.CrashBorn=k.CrashBorn;v.CrashUntil=k.CrashUntil;
            return true;
        }
        /// <summary>The feet below a NativeKernelPanic position (a Rigidbody centre, as Actor.Position is the body less .865 m).</summary>
        public const float BodyCentre=.865f;
        private static Vector3 Feet(V3 p)=>new Vector3(p.x,p.y-BodyCentre,p.z);
        /// <summary>Kernel Panic from Astra's NativeKernelPanic into the view (slot Four's window, the sequence, the points).</summary>
        private static void Kernel(NativeKernelPanic s,CyberLordView v)
        {
            const int F=CyberLordSpec.Four;var f=s.Facing;v.PanicId=s.Id;v.PanicSeed=s.Seed;v.PanicNext=s.Next;v.PanicFacing=new Vector3(f.x,f.y,f.z);
            v.PanicYaw=f.x*f.x+f.z*f.z>1e-8f?(float)(Math.Atan2(f.x,f.z)*180/Math.PI):v.Yaw;v.PanicCasterStart=Feet(s.CasterStart);v.PanicVictimStart=Feet(s.VictimStart);
            // The opener: the windup cocks, the lunge drives to the contact at the windup's end (the host moves the body then).
            float contact=s.At+NativeKernelPanicRules.Windup;v.At[F]=s.At;v.Release[F]=s.At+NativeKernelPanicRules.Windup*CyberLordSpec.PanicDrive;v.Until[F]=contact;v.RecoverUntil[F]=s.Until;v.Ready[F]=s.Ready;
            bool sequence=s.Victim>0&&(s.Outcome==1||s.Outcome==3||s.Outcome==4)&&s.ConnectedAt>=s.At&&s.VictimPoints!=null&&s.CasterPoints!=null&&s.VictimPoints.Length==KernelPanicPath.HostPoints&&s.CasterPoints.Length==KernelPanicPath.HostPoints;
            if(sequence)
            {
                v.Until[F]=s.ConnectedAt;v.RecoverUntil[F]=s.ConnectedAt;v.PanicOutcome=s.Outcome==3?CyberLordSpec.EndedEarly:CyberLordSpec.Connect;
                v.PanicConnect=s.ConnectedAt;v.PanicStart=s.ConnectedAt+NativeKernelPanicRules.CutIn;v.PanicEndedAt=s.Outcome==3?s.Until:-100;v.PanicVictim=s.Victim;v.PanicWall=s.WallSplat;
                var a=Feet(s.Anchor);v.PanicAnchor=new V3(a.X,a.Y,a.Z);
                for(int i=0;i<KernelPanicPath.HostPoints;i++){v.CasterPoints[i]=Feet(s.CasterPoints[i]);v.VictimPoints[i]=Feet(s.VictimPoints[i]);}v.PanicPointCount=KernelPanicPath.HostPoints;
            }
            else if(s.Outcome==2){v.PanicOutcome=CyberLordSpec.Whiff;v.Until[F]=Math.Max(s.At+.01f,s.Until-NativeKernelPanicRules.WhiffRecovery);v.Release[F]=Math.Min(v.Release[F],v.Until[F]-.01f);}
            else if(s.Outcome==3){v.Until[F]=Math.Min(contact,s.Until);v.RecoverUntil[F]=s.Until;}
        }
        private static int Mod3(int x)=>((x%3)+3)%3;
        /// <summary>The proposed per-ability event Details (NEW-KITS section 3), in CyberHit order from Jab.</summary>
        /// <summary>The event Details in CyberHit order from Jab: the proposed per-ability ones (NEW-KITS section 3) and Astra's
        /// Kernel Panic ones (kernel-panic-1 .. -5 on the victim, step 4 three times; kernel-panic-wave on the others).</summary>
        public static readonly string[] Details={"cyber-jab","cyber-cross","cyber-palm","data-dash","firewall","packet-burst","siphon-link","system-crash","kernel-panic-1","kernel-panic-2","kernel-panic-3","kernel-panic-4","kernel-panic-5","kernel-panic-wave"};
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
    /// Kernel Panic's choreography as Astra replicates it (NativeKernelPanicRules: the step table, and six resolved points each
    /// for the caster and the victim, wall- and arena-clamped by the host): named points, and where the caster and the victim
    /// stand along it at a sequence age, exactly as NativeKernelPanicRules.Sample moves their bodies (the victim eased
    /// between its points within each step, the caster teleporting at each step), so the cameras and effects follow the
    /// bodies the snapshot shows.
    /// </summary>
    public static class KernelPanicPath
    {
        /// <summary>Step starts (s after the sequence start), the recovery and the end (Astra's table).</summary>
        public static readonly float[] Steps=NativeKernelPanicRules.Steps;
        public const float Recovery=NativeKernelPanicRules.RecoveryAt,End=NativeKernelPanicRules.Duration,RecoverySeconds=End-Recovery;
        /// <summary>Fork bomb: the three clones' strikes (s after the sequence start; the host's three step-4 hits all land at
        /// its start, the presentation staggers the clones).</summary>
        public static readonly float[] Clones={1.35f,1.5f,1.65f};
        /// <summary>The spec's distances the host resolves (NativeKernelPanicRuntime.ConnectKernel), for the fixtures.</summary>
        public const float Launch=8,LaunchRise=1.5f,KickUp=5,Shockwave=5,BlinkOffset=1.1f,AboveOffset=1.8f,HammerHeight=2,Reappear1=1.2f,ForkSide=1.2f;
        /// <summary>The host's point count per participant.</summary>
        public const int HostPoints=6;
        /// <summary>Named points: the anchor (the victim at the connect), the launch's end (or the wall-splat point), the
        /// caster's blink-behind spot, the victim's apex, the caster's blink-above spot, the landing (the column), the caster's
        /// reappearance, the caster's start, where the hammer drives the victim, and the caster's fork-bomb spot.</summary>
        public const int Anchor=0,LaunchEnd=1,Behind=2,Apex=3,Above=4,Landing=5,Reappear=6,Start=7,HammerEnd=8,Fork=9;
        /// <summary>When step `step` (1..5) lands, s into the sequence: Astra's hit times are the step starts
        /// (NativeKernelPanicRules.HitAt). The takes' impacts, the hit-stops, the screen glitch and the fixtures' events key on it.</summary>
        public static float HitAge(int step)=>step<1||step>5?float.NaN:Steps[step-1];
        public static int Step(float age){int s=0;for(int i=0;i<Steps.Length;i++)if(age>=Steps[i])s=i+1;return age<0?0:s;}
        public static Vector3 Forward(float yawDegrees){double r=yawDegrees*Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        private static int Segment(float age){int i=0;while(i<4&&age>=Steps[i+1])i++;return i;}
        /// <summary>Where the victim's feet are `age` s into the sequence (NativeKernelPanicRules.Sample; before it: the start).</summary>
        public static Vector3 Victim(CyberLordView v,float age)
        {
            if(v.PanicPointCount<HostPoints||!(age>=0))return v.PanicPointCount<HostPoints?v.PanicVictimStart:v.VictimPoints[0];
            if(age>=Recovery)return v.VictimPoints[5];
            int i=Segment(age);float end=i==4?Recovery:Steps[i+1],u=Smooth((age-Steps[i])/(end-Steps[i]));return v.VictimPoints[i]+(v.VictimPoints[i+1]-v.VictimPoints[i])*u;
        }
        /// <summary>Where the caster's feet are `age` s into the sequence (the host teleports it at each step), and whether it is
        /// drawn solid (glitched out through the fork bomb while the clones strike for it).</summary>
        public static Vector3 Caster(CyberLordView v,float age,out bool shown)
        {
            shown=true;if(v.PanicPointCount<HostPoints||!(age>=0))return v.PanicPointCount<HostPoints?v.PanicCasterStart:v.CasterPoints[0];
            if(age>=Recovery)return v.CasterPoints[5];int i=Segment(age);if(i==3)shown=age<Steps[3]+.08f||age>Steps[4]-.06f;return v.CasterPoints[i];
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
