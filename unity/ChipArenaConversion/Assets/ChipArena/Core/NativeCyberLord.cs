using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The numbers the presentation keys on: Astra's rules (NativeCyberRules, the host's StepCyberInput /
    /// StepCyberWorld, NativeKernelPanicRules) where she names them, her literals otherwise. The replicated kit (Actor.Cyber)
    /// always wins; these size the effects and drive the fixtures' timeline.</summary>
    public static class CyberLordSpec
    {
        public const string Id="cyberlord",Entrance="dev-cyberlord";
        /// <summary>Slots: Q, E, then 1-4 (the DEV fighters' 1-4 input path).</summary>
        public const int Q=0,E=1,One=2,Two=3,Three=4,Four=5,Slots=6;
        public static readonly string[] Keys={"Q","E","1","2","3","4"};
        public static readonly string[] Names={"Data Dash","Firewall","Packet Burst","Siphon Link","System Crash","Kernel Panic"};
        public static readonly string[] Ids={"data-dash","firewall","packet-burst","siphon-link","system-crash","kernel-panic"};
        public static readonly float[] Cooldowns={NativeCyberRules.DashCooldown,12,4,10,14,NativeKernelPanicRules.Cooldown},Costs={20,35,15,25,40,NativeKernelPanicRules.Mana};
        /// <summary>The hardlight combo (Astra's NativeCyberRules): Jab .28 s, Cross .30 s, Pulse Palm .45 s and their damage; the
        /// reset after .6 s idle; range, arc, palm cone.</summary>
        public static readonly float[] ComboDurations=NativeCyberRules.ComboSeconds,ComboDamage=NativeCyberRules.ComboDamage;
        public const float ComboReset=.6f,ComboRange=2.2f,ComboArc=1.4f,PalmCone=1.6f,ManaPerHit=6,Mana=100;
        /// <summary>Each step's windup at attack rate 1 (StepCyberInput: .1, .1, .18 s): the hit resolves at the kit's Release, and
        /// the kit's Kind names the step.</summary>
        public static readonly float[] ComboWindups={.1f,.1f,.18f};
        public static readonly string[] ComboKinds={"cyber-jab","cyber-cross","cyber-palm"};
        /// <summary>Q: 7 m in .2 s (NativeCyberRules.DashPosition), the trail 1.2 s after, ticking every .3 s.</summary>
        public const float DashDistance=7,DashSeconds=.2f,TrailSeconds=1.2f,TrailTick=.3f;
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
        /// <summary>Each cast's windup and length as StepCyberInput begins it (CyberBegin: Release = At + windup, Until = At +
        /// length; Q to 3; Kernel Panic's come from Actor.Kernel). The pilot is free again at Until: the rules have no recovery.</summary>
        public static readonly float[] CastWindup={0,0,.08f,0,CrashWindup,PanicWindup},CastSeconds={DashSeconds,.2f,.25f,.2f,.8f,PanicWindup};
        /// <summary>The presentation's settle after a cast's end (the pose eases back to the stance; a newer swing, cast, block or
        /// move takes over at once, so it never holds the pilot). Not a rules window.</summary>
        public static readonly float[] Settle={.18f,.3f,.32f,.25f,.4f,0};
        public static int Slot(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return i;return -1;}
    }

    /// <summary>What a cyber event is (CyberLordFeed.Hit maps its Detail).</summary>
    public enum CyberHit{None,Jab,Cross,Palm,Dash,Firewall,Burst,Siphon,Crash,Panic1,Panic2,Panic3,Panic4,Panic5,PanicWave,Bolt,Lance,Field,Swap,Root}

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
        // The ranged mode (Core/CyberLordRanged.cs, the proposed record through CyberLordFeed): the mode and its switch, each
        // ranged slot's window and ready time (CyberRangedSpec order: the LMB bolt, then 1-4), the bolt's hand, the lance's and
        // the beam's lines (world, from the emitters), the field's throw, landing and life (the point at the feet), the swap's
        // path (feet) and its mine's burst.
        public bool Ranged;public float ModeAt=-100,ModeReady=-100;
        public readonly float[] RAt=new float[CyberRangedSpec.Slots],RRelease=new float[CyberRangedSpec.Slots],RUntil=new float[CyberRangedSpec.Slots],RRecover=new float[CyberRangedSpec.Slots],RReady=new float[CyberRangedSpec.Slots];
        public int BoltHand;public Vector3 LanceFrom,LanceTo,FieldFrom,FieldPoint,SwapFrom,SwapTo,RootFrom,RootTo;public float FieldLand=-100,FieldUntil=-100,MineAt=-100;
        public void Clear()
        {
            Id=0;Alive=false;Feet=default;Yaw=0;ComboStep=0;ComboAt=ComboUntil=ComboHit=-100;for(int i=0;i<CyberLordSpec.Slots;i++)At[i]=Release[i]=Until[i]=RecoverUntil[i]=Ready[i]=-100;
            DashFrom=DashTo=default;TrailUntil=-100;WallAt=default;WallYaw=0;WallBorn=WallUntil=-100;LinkTarget=0;LinkAt=LinkUntil=-100;CrashAt=default;CrashBorn=CrashUntil=-100;
            PanicId=PanicOutcome=PanicSeed=PanicVictim=PanicNext=0;PanicConnect=PanicStart=PanicEndedAt=-100;PanicAnchor=default;PanicYaw=0;PanicWall=false;PanicPointCount=0;
            PanicFacing=PanicCasterStart=PanicVictimStart=default;
            Ranged=false;ModeAt=ModeReady=-100;for(int i=0;i<CyberRangedSpec.Slots;i++)RAt[i]=RRelease[i]=RUntil[i]=RRecover[i]=RReady[i]=-100;
            BoltHand=0;LanceFrom=LanceTo=FieldFrom=FieldPoint=SwapFrom=SwapTo=RootFrom=RootTo=default;FieldLand=FieldUntil=MineAt=-100;
        }
        /// <summary>Ranged slot `slot`'s window at `time` (CyberRangedSpec order), as Phase.</summary>
        public CyberPhase RPhase(int slot,float time)
        {
            var p=new CyberPhase{At=RAt[slot],Release=RRelease[slot],Until=RUntil[slot]};p.End=Math.Max(p.Until,RRecover[slot]);
            if(!(p.At>-90)||time<p.At||time>=p.End)return p;p.Live=true;p.Age=time-p.At;float rel=p.Release>p.At?p.Release:p.At;
            if(time<rel){p.Part=0;p.U=Clamp01((time-p.At)/Math.Max(1e-4f,rel-p.At));}
            else if(time<p.Until){p.Part=1;p.U=Clamp01((time-rel)/Math.Max(1e-4f,p.Until-rel));}
            else{p.Part=2;p.U=Clamp01((time-p.Until)/Math.Max(1e-4f,p.End-p.Until));}
            return p;
        }
        /// <summary>The ranged mode's weight at `time` (0 melee .. 1 ranged), eased over the switch from ModeAt (before it, on a
        /// rewound clock, the mode it left).</summary>
        public float ModeWeight(float time)
        {
            float u=ModeAt>-90?Clamp01((time-ModeAt)/CyberRangedSpec.SwitchSeconds):1;u=u*u*(3-2*u);return Ranged?u:1-u;
        }
        /// <summary>The switch's progress at `time` (0..1 through the reconfiguration), or -1 outside one.</summary>
        public float SwitchU(float time)=>ModeAt>-90&&time>=ModeAt&&time<ModeAt+CyberRangedSpec.SwitchSeconds?(time-ModeAt)/CyberRangedSpec.SwitchSeconds:-1;
        /// <summary>Whether ranged cast `slot` reached its release (a cast cut before it ends on its release: no shot, no cue).</summary>
        public bool RReleased(int slot)=>RAt[slot]>-90&&RUntil[slot]>RRelease[slot]+1e-4f;
        /// <summary>Whether a ranged cast's window (to its end, not its settle) is live at `time`.</summary>
        public bool RangedCasting(float time){for(int i=0;i<CyberRangedSpec.Slots;i++)if(RAt[i]>-90&&time>=RAt[i]&&time<RUntil[i])return true;return false;}
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
    /// the replicated actor into CyberLordView, and a cyber event's Detail into what it is. It reads Astra's implemented
    /// state only: the kit (Actor.Cyber, NativeCyberKit, the host's StepCyberInput / StepCyberWorld, Runtime/NativeCyberLord.cs) for the combo and Q to
    /// 3, Kernel Panic (Actor.Kernel, NativeKernelPanic: positions are Rigidbody centres, the feet .865 m below; Outcome 0
    /// windup, 1 connect, 2 whiff, 3 interrupted, 4 complete) for 4, and her event Details (the cast kinds, kernel-panic-1 ..
    /// -5, kernel-panic-wave). Nothing else in the presentation names those fields. Allocation-free.
    /// </summary>
    public static class CyberLordFeed
    {
        public static bool Handles(string spec)=>spec==CyberLordSpec.Id;
        public static bool Handles(Actor a)=>a!=null&&a.SourceSpec==CyberLordSpec.Id;
        /// <summary>Fills `v` from the replicated actor; false (v cleared) for any other pilot.</summary>
        public static bool Read(Actor a,CyberLordView v)
        {
            v.Clear();if(!Handles(a))return false;v.Id=a.Id;v.Alive=a.Alive;v.Feet=a.Position;v.Yaw=a.Yaw;
            // Kernel Panic: Astra's state, on both participants; the caster's own cast only (a victim's copy belongs to its caster).
            var kp=a.Kernel;bool own=kp!=null&&kp.Id>0&&kp.Caster==a.Id;if(own)Kernel(kp,v);float k=own?kp.At:-100;
            var s=a.Cyber;RangedMode(a,v,s?.Ranged);
            if(s==null)return true;
            v.Ready[CyberLordSpec.Q]=s.QReady;v.Ready[CyberLordSpec.E]=s.EReady;v.Ready[CyberLordSpec.One]=s.PacketReady;v.Ready[CyberLordSpec.Two]=s.LinkReady;v.Ready[CyberLordSpec.Three]=s.CrashReady;
            // The kit keeps one window, the current or last cast's (Kind, At, Release, Until); the host's reset kit is all zeros.
            string kind=s.Kind??"";bool cast=kind.Length>0&&s.At>0&&s.Until>=s.At;float end=cast?CastEnd(a,s,k):-100;bool cut=cast&&Cut(a,s);
            // The combo: the kit's step, its swing from At to Until, the hit on Release (StepCyberWorld resolves it there).
            if(cast&&kind.StartsWith("cyber-",StringComparison.Ordinal)&&s.Combo>=0)
            {v.ComboStep=Mod3(s.Combo);v.ComboAt=s.At;v.ComboUntil=Math.Max(s.At+.01f,end);v.ComboHit=s.Release;}
            // Q Data Dash (windup 0): its own start and end (DashUntil, zeroed when an interrupt stops the body), the path's two
            // body centres and the damaging trail's end (the trail ticks on along the whole path either way).
            if(s.DashAt>0&&s.TrailUntil>s.DashAt)
            {
                float until=s.DashUntil>s.DashAt?s.DashUntil:Interrupt(a,k,s.DashAt,s.DashAt+CyberLordSpec.DashSeconds);if(Same(kind,"data-dash",s,s.DashAt))until=Math.Min(until,end);
                Window(v,CyberLordSpec.Q,s.DashAt,s.DashAt,until,cut&&Same(kind,"data-dash",s,s.DashAt)||s.DashUntil<s.DashAt+CyberLordSpec.DashSeconds-Eps);
                v.DashFrom=Feet3(s.DashFrom);v.DashTo=Feet3(s.DashTo);v.TrailUntil=s.TrailUntil;
            }
            // E Firewall (windup 0): the wall stands from the cast for 4 s (WallUntil) at WallPoint, its bottom centre, across WallYaw.
            if(s.WallAt>0&&s.WallUntil>s.WallAt)
            {
                bool now=Same(kind,"firewall",s,s.WallAt);Window(v,CyberLordSpec.E,s.WallAt,s.WallAt,now?end:s.WallAt+CyberLordSpec.CastSeconds[CyberLordSpec.E],now&&cut);
                v.WallAt=s.WallPoint;v.WallYaw=s.WallYaw;v.WallBorn=s.WallAt;v.WallUntil=s.WallUntil;
            }
            // Packet Burst retains its window across subsequent casts.
            if(s.PacketUntil>s.PacketAt)Window(v,CyberLordSpec.One,s.PacketAt,s.PacketRelease,s.PacketUntil,s.PacketCutAt>=s.PacketAt);
            else if(cast&&kind=="packet-burst")Window(v,CyberLordSpec.One,s.At,s.Release,end,cut);
            // 2 Siphon Link (windup 0): the reach over the .2 s cast, the tether from LinkAt to LinkUntil (2.5 s, the break's time
            // when lost, zeroed by an interrupt: then the interrupt's time), on LinkTarget.
            if(s.LinkAt>0&&s.LinkTarget!=0)
            {
                float link=s.LinkUntil>=s.LinkAt?s.LinkUntil:Interrupt(a,k,s.LinkAt,s.LinkAt+CyberLordSpec.LinkSeconds,s.LinkAt+CyberLordSpec.CastSeconds[CyberLordSpec.Two]);
                float reach=Same(kind,"siphon-link",s,s.LinkAt)?end:s.LinkAt+CyberLordSpec.CastSeconds[CyberLordSpec.Two];
                Window(v,CyberLordSpec.Two,s.LinkAt,Math.Min(reach,link),link,s.LinkUntil<s.LinkAt);
                v.LinkTarget=s.LinkTarget;v.LinkAt=s.LinkAt;v.LinkUntil=link;
            }
            // 3 System Crash: the .5 s windup, the slam at Release (one hit with a 1 s stun at CrashPoint, a body centre), to
            // CrashUntil; the field the presentation draws lasts the stun. An interrupt before the slam leaves no field.
            if(s.CrashAt>0&&s.CrashUntil>s.CrashAt)
            {
                bool now=Same(kind,"system-crash",s,s.CrashAt);float slam=now?s.Release:s.CrashAt+CyberLordSpec.CrashWindup,until=now?end:s.CrashUntil;
                Window(v,CyberLordSpec.Three,s.CrashAt,slam,until,now&&cut);
                if(until>=slam-Eps){v.CrashAt=Feet3(s.CrashPoint);v.CrashBorn=slam;v.CrashUntil=slam+CyberLordSpec.CrashStun;}
            }
            return true;
        }
        private const float Eps=.002f;
        /// <summary>The ranged mode's record before the rules replicate it (docs/CYBER-LORD.md "Ranged mode"): the runtime's Dev
        /// Sandbox preview, the checks' and render harness's fixture; null reads the actor alone.</summary>
        public static ICyberRangedSource RangedSource;
        /// <summary>The ranged mode into the view: the record's, else the actor's mode fields (Actor.SourceRanged / SourceModeAt,
        /// the Siren's mode on the wire today; the contract has the host set them for the Cyber Lord too).</summary>
        private static void RangedMode(Actor a,CyberLordView v,NativeCyberRangedState state)
        {
            if(state!=null)
            {
                v.Ranged=state.Ranged;v.ModeAt=state.ModeAt;v.ModeReady=state.ModeReady;v.BoltHand=state.BoltHand;
                for(int i=0;i<CyberRangedSpec.Slots;i++)
                {float at=state.At[i],until=Math.Max(at,state.Until[i]);v.RAt[i]=at;v.RUntil[i]=until;v.RRelease[i]=Math.Max(at,Math.Min(state.Release[i],until));v.RReady[i]=state.Ready[i];bool cut=until<at+CyberRangedSpec.Seconds[i]*.5f-Eps&&i!=0;v.RRecover[i]=at>-90?until+(cut?0:CyberRangedSpec.Settle[i]):-100;}
                Vector3 V(V3 p)=>new Vector3(p.x,p.y,p.z);
                v.LanceFrom=V(state.LanceFrom);v.LanceTo=V(state.LanceTo);v.FieldFrom=V(state.FieldFrom);v.FieldPoint=V(state.FieldPoint);v.FieldLand=state.FieldLand;v.FieldUntil=state.FieldUntil;
                v.SwapFrom=V(state.SwapFrom);v.SwapTo=V(state.SwapTo);v.MineAt=state.MineAt;v.RootFrom=V(state.RootFrom);v.RootTo=V(state.RootTo);return;
            }
            var r=RangedSource?.Of(a.Id);
            if(r==null){v.Ranged=a.SourceRanged;v.ModeAt=a.SourceModeAt;v.ModeReady=a.SourceModeAt>-90?a.SourceModeAt+CyberRangedSpec.SwitchCooldown:-100;return;}
            v.Ranged=r.Ranged;v.ModeAt=r.ModeAt;v.ModeReady=r.ModeReady;v.BoltHand=r.BoltHand;
            for(int i=0;i<CyberRangedSpec.Slots;i++)
            {
                float at=r.At[i],until=Math.Max(at,r.Until[i]);v.RAt[i]=at;v.RUntil[i]=until;v.RRelease[i]=Math.Max(at,Math.Min(r.Release[i],until));v.RReady[i]=r.Ready[i];
                // A cut cast (an interrupt moved its end before its own length) has no settle.
                bool cut=at>-90&&until<at+CyberRangedSpec.Seconds[i]*.5f-Eps&&i!=CyberRangedSpec.Bolt;v.RRecover[i]=at>-90?until+(cut?0:CyberRangedSpec.Settle[i]):-100;
            }
            v.LanceFrom=r.LanceFrom;v.LanceTo=r.LanceTo;v.FieldFrom=r.FieldFrom;v.FieldPoint=r.FieldPoint;v.FieldLand=r.FieldLand;v.FieldUntil=r.FieldUntil;
            v.SwapFrom=r.SwapFrom;v.SwapTo=r.SwapTo;v.MineAt=r.MineAt;v.RootFrom=r.RootFrom;v.RootTo=r.RootTo;
        }
        /// <summary>A replicated shot's ranged slot: a Cyber Lord's shot (Spec cyberlord) with a ranged Detail (the contract's
        /// Bit Bolt: Detail bit-bolt), else -1.</summary>
        public static int ShotSlot(string spec,string detail)=>spec==CyberLordSpec.Id&&detail!=null?CyberRangedSpec.Slot(detail):-1;
        /// <summary>Slot `slot`'s window: a cut cast ends where it was cut, its release no later, with no settle.</summary>
        private static void Window(CyberLordView v,int slot,float at,float release,float until,bool cut)
        {
            until=Math.Max(at,until);v.At[slot]=at;v.Release[slot]=Math.Min(Math.Max(at,release),until);v.Until[slot]=until;v.RecoverUntil[slot]=cut?until:until+CyberLordSpec.Settle[slot];
        }
        private static bool Same(string kind,string id,NativeCyberKit s,float at)=>kind==id&&Math.Abs(s.At-at)<=Eps;
        /// <summary>The planned end of the kit's cast: the actor's AttackUntil while its AttackAt is the cast's (the host's
        /// AttackEnd, which CyberBegin sets with AttackAt; a newer action such as Kernel Panic overwrites both), else the cast's
        /// own length: a combo step's scales with the attack rate as its windup does (the kit's Release shows it), an ability's
        /// is fixed (Tests/CyberLordChecks pins both to NativeCyberRules.Predict).</summary>
        private static float Planned(Actor a,NativeCyberKit s)
        {
            if(Math.Abs(a.AttackAt-s.At)<=Eps&&a.AttackUntil>=s.At)return a.AttackUntil;
            string kind=s.Kind??"";
            for(int i=0;i<3;i++)if(kind==CyberLordSpec.ComboKinds[i])return s.At+Math.Max(0,s.Release-s.At)*CyberLordSpec.ComboDurations[i]/CyberLordSpec.ComboWindups[i];
            int slot=CyberLordSpec.Slot(kind);return slot>=0&&slot<CyberLordSpec.Four?s.At+CyberLordSpec.CastSeconds[slot]:s.Until;
        }
        /// <summary>Whether an interrupt moved the kit's Until (StepCyberWorld sets it to the host's time on every tick a block,
        /// roll or incapacity lasts) off the planned end.</summary>
        private static bool Cut(Actor a,NativeCyberKit s)=>s.CutAt>=s.At||Math.Abs(s.Until-Planned(a,s))>Eps;
        /// <summary>Where the kit's cast ended: its Until, or where the interrupt that moved it began.</summary>
        private static float CastEnd(Actor a,NativeCyberKit s,float kernelAt)
        {
            if(s.CutAt>=s.At)return s.CutAt;
            float planned=Planned(a,s);if(Math.Abs(s.Until-planned)<=Eps)return s.Until;
            return Math.Max(s.At,Interrupt(a,kernelAt,s.At,Math.Min(s.Until,planned)));
        }
        /// <summary>The first interrupt after `from` and before `upTo` (a block, a roll, a hit reaction, the pilot's own Kernel
        /// Panic at `kernelAt`), else `otherwise` (`upTo` when not given). The kit does not keep the interrupt's time, so this
        /// is the presentation's best reading of the actor (a reaction that did not interrupt can end it early).</summary>
        private static float Interrupt(Actor a,float kernelAt,float from,float upTo,float otherwise=float.NaN)
        {
            float t=upTo;bool found=false;
            void Take(float x){if(x>from+Eps&&x<t){t=x;found=true;}}
            Take(a.BlockAt);Take(a.RollAt);Take(a.ReactionAt);Take(kernelAt);
            return found||float.IsNaN(otherwise)?t:Math.Min(upTo,otherwise);
        }
        private static V3 Feet3(V3 p)=>new V3(p.x,p.y-BodyCentre,p.z);
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
        /// <summary>The event Details in CyberHit order from Jab: Astra's cast kinds (the combo's steps and Q to 3, on Hit
        /// events as the damage kind, on the dash's Teleport and the wall's Absorb) and her Kernel Panic ones (kernel-panic-1 .. -5 on the victim, step 4 three times; kernel-panic-wave on the others).</summary>
        public static readonly string[] Details={"cyber-jab","cyber-cross","cyber-palm","data-dash","firewall","packet-burst","siphon-link","system-crash","kernel-panic-1","kernel-panic-2","kernel-panic-3","kernel-panic-4","kernel-panic-5","kernel-panic-wave",
            "bit-bolt","ping-lance","lag-field","proxy-swap","root-access"};
        public static CyberHit Hit(string detail)
        {
            if(detail==null)return CyberHit.None;
            for(int i=0;i<Details.Length;i++)if(detail==Details[i])return (CyberHit)(i+1);
            return CyberHit.None;
        }
        /// <summary>The Kernel Panic step (1..5) of an event, 0 for none.</summary>
        public static int PanicStep(CyberHit h)=>h>=CyberHit.Panic1&&h<=CyberHit.Panic5?h-CyberHit.Panic1+1:0;
        /// <summary>The event kinds the presentation reads: hits and ability hits (Detail: the damage kind), the Firewall's
        /// absorbs, the Siphon's heals (SiphonHeal; the casts' AbilityCast and the dash's Teleport are drawn from the kit) and
        /// the ranged set's impacts on the world (Impact with a ranged Detail; an undetailed impact maps to no hit).</summary>
        public static bool Kind(EventKind k)=>k==EventKind.Hit||k==EventKind.AbilityHit||k==EventKind.Heal||k==EventKind.Absorb||k==EventKind.Impact;
        /// <summary>Whether a hit or an ability hit (the strikes; not an absorb or a heal).</summary>
        public static bool Strike(EventKind k)=>k==EventKind.Hit||k==EventKind.AbilityHit;
        /// <summary>Tagged Siphon heals identify themselves; older detail-less events use same-tick drain pairing.</summary>
        public static bool SiphonHeal(EventKind kind,string detail,int source,int target,float time,int drainSource,float drainAt)=>
            kind==EventKind.Heal&&source==target&&source!=0&&(detail=="siphon-link"||string.IsNullOrEmpty(detail)&&source==drainSource&&Math.Abs(time-drainAt)<=Eps);
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
