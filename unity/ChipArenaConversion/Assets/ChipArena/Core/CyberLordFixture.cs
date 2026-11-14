using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Astra's rules as the host writes them, on an open, flat floor at attack rate and cooldown scale 1: what
    /// Tests/CyberLordChecks and the offline render harness drive the presentation with. It writes Actor.Cyber the way
    /// NativeArena.StepCyberInput / CyberBegin do (and the actor's AttackAt / AttackUntil, which the snapshot copies from the
    /// host's AttackAt / AttackEnd) and Actor.Kernel the way NativeKernelPanicRuntime does; nothing in the game calls it.
    /// </summary>
    public static class CyberLordFixture
    {
        public static Actor Pilot(int id,Vector3 feet,float yaw)=>new Actor{Id=id,SourceSpec=CyberLordSpec.Id,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Yaw=yaw,Position=new V3(feet.X,feet.Y,feet.Z),
            Cyber=new NativeCyberKit(),MeleeKit=new NativeMeleeKit(),BodyColor="#e5e7eb",AccentColor="#22e6ff"};
        /// <summary>CyberBegin: the kit's one window (Kind, At, Release = At + windup, Until = At + length, Facing) and the
        /// actor's attack window; the cast's own cooldown is the caller's.</summary>
        private static NativeCyberKit Begin(Actor a,string kind,float at,float windup,float length)
        {
            var s=a.Cyber??=new NativeCyberKit();s.Kind=kind;s.CutAt=-1;s.At=at;s.Release=at+windup;s.Until=at+length;s.Resolved=false;s.Facing=V(KernelPanicPath.Forward(a.Yaw));
            a.AttackAt=a.LastAttack=at;a.AttackUntil=s.Until;return s;
        }
        /// <summary>A combo swing `step` starting at `at`: the hit resolves at the kit's Release.</summary>
        public static void Combo(Actor a,int step,float at)
        {
            var s=Begin(a,CyberLordSpec.ComboKinds[step],at,CyberLordSpec.ComboWindups[step],CyberLordSpec.ComboDurations[step]);s.Combo=step;s.LastComboEnd=s.Until;a.Combo=step;
        }
        /// <summary>Ability `slot` (Q to 3) cast at `at` with its own record; returns its release.</summary>
        public static float Cast(Actor a,int slot,float at)
        {
            var s=Begin(a,CyberLordSpec.Ids[slot],at,CyberLordSpec.CastWindup[slot],CyberLordSpec.CastSeconds[slot]);var f=KernelPanicPath.Forward(a.Yaw);var body=Body(a);
            switch(slot)
            {
                case CyberLordSpec.Q:s.QReady=at+CyberLordSpec.Cooldowns[slot];s.DashAt=at;s.DashUntil=at+CyberLordSpec.DashSeconds;s.TrailUntil=s.DashUntil+CyberLordSpec.TrailSeconds;
                    s.DashFrom=V(body);s.DashTo=V(body+f*CyberLordSpec.DashDistance);break;
                case CyberLordSpec.E:s.EReady=at+CyberLordSpec.Cooldowns[slot];s.WallAt=at;s.WallUntil=at+CyberLordSpec.WallSeconds;s.WallYaw=a.Yaw;
                    s.WallPoint=V(body-Vector3.UnitY*CyberLordFeed.BodyCentre+f*CyberLordSpec.WallAhead);break;
                case CyberLordSpec.One:s.PacketReady=at+CyberLordSpec.Cooldowns[slot];s.PacketAt=s.At;s.PacketRelease=s.Release;s.PacketUntil=s.Until;s.PacketCutAt=-1;break;
                case CyberLordSpec.Two:s.LinkReady=at+CyberLordSpec.Cooldowns[slot];s.LinkAt=at;s.LinkUntil=at+CyberLordSpec.LinkSeconds;break;
                case CyberLordSpec.Three:s.CrashReady=at+CyberLordSpec.Cooldowns[slot];s.CrashAt=at;s.CrashUntil=s.Until;s.CrashPoint=V(body);break;
            }
            return s.Release;
        }
        /// <summary>Q Data Dash from the pilot's body along its facing (7 m in .2 s), the trail lasting 1.2 s from the dash's end.</summary>
        public static void DataDash(Actor a,float at)=>Cast(a,CyberLordSpec.Q,at);
        /// <summary>E Firewall: the wall's bottom centre .8 m ahead of the feet, across the facing, for 4 s.</summary>
        public static void Firewall(Actor a,float at)=>Cast(a,CyberLordSpec.E,at);
        public static void PacketBurst(Actor a,float at)=>Cast(a,CyberLordSpec.One,at);
        /// <summary>2 Siphon Link on `target` for 2.5 s from the cast (or lost at `broken`: StepCyberWorld sets LinkUntil then).</summary>
        public static void SiphonLink(Actor a,int target,float at,float broken=-1)
        {
            Cast(a,CyberLordSpec.Two,at);var s=a.Cyber;s.LinkTarget=target;if(broken>at)s.LinkUntil=broken;
        }
        /// <summary>3 System Crash: the .5 s windup, then the slam at the pilot's body (25 damage, a 1 s stun in 4 m).</summary>
        public static void SystemCrash(Actor a,float at)=>Cast(a,CyberLordSpec.Three,at);
        /// <summary>StepCyberWorld's interrupt (a block, roll or incapacity) at `at`, lasting to `until`: the cast resolved, the dash
        /// and the link stopped, the kit's Until moved to the host's time each tick (here: `until`), the block on the actor.</summary>
        public static void Block(Actor a,float at,float until)
        {
            var s=a.Cyber;NativeCyberRules.Interrupt(s,at);a.BlockAt=at;a.BlockUntil=until;
        }
        private static Vector3 Body(Actor a)=>new Vector3(a.Position.x,a.Position.y+CyberLordFeed.BodyCentre,a.Position.z);
        /// <summary>
        /// 4 Kernel Panic cast at `at` on `victim` standing at `victimFeet`, written as Astra's host writes Actor.Kernel
        /// (NativeKernelPanicRuntime.StartKernelPanic / ConnectKernel / KernelWhiff / EndKernel) on an open, flat floor: a whiff
        /// (the instant 6 m lunge touches no one: .6 s recovery, half the cooldown), or a connect at the windup's end with the
        /// host's six resolved points each (Rigidbody centres; the launch's end moved to `wall`, the splat's body centre, when
        /// given); `endedAt` interrupts it there.
        /// </summary>
        public static void KernelPanic(Actor a,float at,int victim,Vector3 victimFeet,bool connect=true,Vector3? wall=null,float endedAt=-1,int seed=7)
        {
            const float C=CyberLordFeed.BodyCentre,Ground=.875f;var up=Vector3.UnitY;var f=KernelPanicPath.Forward(a.Yaw);
            var start=new Vector3(a.Position.x,a.Position.y+C,a.Position.z);
            var s=new NativeKernelPanic{Id=++kernelIds,Caster=a.Id,Seed=seed,At=at,Until=at+NativeKernelPanicRules.Windup,Ready=at+NativeKernelPanicRules.Cooldown,CasterStart=V(start),Anchor=V(start),VictimStart=V(start),CasterExit=V(start),VictimExit=V(start),Facing=V(f)};
            a.Kernel=s;a.HasKernel=true;a.AttackAt=a.LastAttack=at;a.AttackUntil=s.Until;
            if(!connect){s.Outcome=2;s.Until=at+NativeKernelPanicRules.Windup+NativeKernelPanicRules.WhiffRecovery;s.Ready=at+NativeKernelPanicRules.Cooldown*.5f;a.AttackUntil=s.Until;return;}
            float floor=victimFeet.Y;var v0=victimFeet+up*C;var contact=v0-f*1.0f;
            s.ConnectedAt=at+NativeKernelPanicRules.Windup;s.Until=s.ConnectedAt+NativeKernelPanicRules.CutIn+NativeKernelPanicRules.Duration;s.Outcome=1;
            s.Anchor=s.VictimStart=V(v0);s.CasterStart=V(contact);s.Victim=victim;
            var want=v0+f*KernelPanicPath.Launch+up*KernelPanicPath.LaunchRise;var v1=wall??want;s.WallSplat=wall.HasValue;var v2=v1+up*KernelPanicPath.KickUp;
            // (The host searches a .5 m grid for a clear landing; off a wall that is a step back from it.)
            var landing=new Vector3(v1.X,floor+Ground,v1.Z)-(wall.HasValue?f*.6f:Vector3.Zero);var v3=landing+up*KernelPanicPath.HammerHeight;var v4=landing;
            var side=Vector3.Cross(up,f)*((seed&1)==0?KernelPanicPath.ForkSide:-KernelPanicPath.ForkSide);var back=landing-f*KernelPanicPath.Reappear1;
            s.VictimPoints=new[]{V(v0),V(v1),V(v2),V(v3),V(v4),V(landing)};
            s.CasterPoints=new[]{s.CasterStart,V(v1-f*KernelPanicPath.BlinkOffset),V(v2+up*KernelPanicPath.AboveOffset),V(v3+side),V(back),V(back)};
            s.CasterExit=V(back);s.VictimExit=V(landing);
            if(endedAt>0){s.Outcome=3;s.Until=endedAt;}
            a.AttackUntil=s.Until;
        }
        private static int kernelIds;
        private static V3 V(Vector3 p)=>new V3(p.X,p.Y,p.Z);
        /// <summary>Where the host puts the caster and the victim during a Kernel Panic (NativeKernelPanicRules.Sample, feet).</summary>
        public static void PanicBodies(Actor caster,Actor victim,float time,CyberLordView scratch)
        {
            var s=caster.Kernel;if(s==null||s.Victim<=0||!(s.Outcome==1||s.Outcome==3||s.Outcome==4)||time<s.ConnectedAt)return;if(s.Outcome==3&&time>=s.Until)return;
            NativeKernelPanicRules.Sample(s,time,out var c,out var v);const float C=CyberLordFeed.BodyCentre;
            if(victim!=null){victim.Position=new V3(v.x,v.y-C,v.z);victim.Grounded=v.y-C<=.03f;}
            caster.Position=new V3(c.x,c.y-C,c.z);caster.Grounded=c.y-C<=.03f;
        }
        /// <summary>When each Kernel Panic step's Hit events land (NativeKernelPanicRules.HitAt: the step starts).</summary>
        public static float PanicHitTime(CyberLordView v,int step)=>step<1||step>5?-100:v.PanicStart+KernelPanicPath.HitAge(step);
    }
}
