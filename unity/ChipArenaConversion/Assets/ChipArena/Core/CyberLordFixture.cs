using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The spec's timeline as the proposed replicated data (docs/NEW-KITS.md section 3 numbers, CyberLordSpec.Timing for the
    /// windows the proposal leaves to the rules): what Tests/CyberLordChecks and the offline render harness drive the
    /// presentation with. It writes only a NativeCyberKit (and the actor's standard swing fields for the combo) the way the
    /// host is proposed to; nothing in the game calls it.
    /// </summary>
    public static class CyberLordFixture
    {
        public static Actor Pilot(int id,Vector3 feet,float yaw)=>new Actor{Id=id,SourceSpec=CyberLordSpec.Id,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Yaw=yaw,Position=new V3(feet.X,feet.Y,feet.Z),
            CyberKit=new NativeCyberKit(),MeleeKit=new NativeMeleeKit(),BodyColor="#e5e7eb",AccentColor="#22e6ff"};
        /// <summary>A combo swing `step` starting at `at` (the standard swing fields and the kit's own copy).</summary>
        public static void Combo(Actor a,int step,float at)
        {
            float d=CyberLordSpec.ComboDurations[step],hit=at+d*CyberLordSpec.ComboHitShare[step];
            a.Combo=step;a.AttackAt=at;a.AttackUntil=at+d;a.SwingAt=hit;a.SwingUntil=hit+.06f;a.LastAttack=at;
            var k=a.CyberKit;k.ComboStep=step;k.ComboAt=at;k.ComboUntil=at+d;
        }
        /// <summary>Ability `slot` cast at `at` on the fixture windows; returns its active (release) start.</summary>
        public static float Cast(Actor a,int slot,float at)
        {
            var t=CyberLordSpec.Timing[slot];var c=a.CyberKit.Casts[slot];c.At=at;c.Release=at+t[0];c.Until=c.Release+t[1];c.RecoverUntil=c.Until+t[2];c.Ready=at+CyberLordSpec.Cooldowns[slot];
            return c.Release;
        }
        /// <summary>Q Data Dash from the pilot's feet along its facing (7 m), the trail lasting 1.2 s from the dash's end.</summary>
        public static void DataDash(Actor a,float at)
        {
            float rel=Cast(a,CyberLordSpec.Q,at);var c=a.CyberKit.Casts[CyberLordSpec.Q];var k=a.CyberKit;var f=KernelPanicPath.Forward(a.Yaw);
            k.DashFrom=a.Position;k.DashTo=new V3(a.Position.x+f.X*CyberLordSpec.DashDistance,a.Position.y,a.Position.z+f.Z*CyberLordSpec.DashDistance);k.TrailUntil=c.Until+CyberLordSpec.TrailSeconds;
        }
        /// <summary>E Firewall .8 m ahead, across the facing, for 4 s from the release.</summary>
        public static void Firewall(Actor a,float at)
        {
            float rel=Cast(a,CyberLordSpec.E,at);var k=a.CyberKit;var f=KernelPanicPath.Forward(a.Yaw);
            k.WallAt=new V3(a.Position.x+f.X*CyberLordSpec.WallAhead,a.Position.y,a.Position.z+f.Z*CyberLordSpec.WallAhead);k.WallYaw=a.Yaw;k.WallBorn=rel;k.WallUntil=rel+CyberLordSpec.WallSeconds;
        }
        public static void PacketBurst(Actor a,float at)=>Cast(a,CyberLordSpec.One,at);
        /// <summary>2 Siphon Link on `target` from the release for 2.5 s (or broken at `broken` when given).</summary>
        public static void SiphonLink(Actor a,int target,float at,float broken=-1)
        {
            float rel=Cast(a,CyberLordSpec.Two,at);var k=a.CyberKit;k.LinkTarget=target;k.LinkAt=rel;k.LinkUntil=broken>rel?broken:rel+CyberLordSpec.LinkSeconds;
            if(broken>rel){var c=k.Casts[CyberLordSpec.Two];c.Until=broken;c.RecoverUntil=broken+CyberLordSpec.Timing[CyberLordSpec.Two][2];}
        }
        /// <summary>3 System Crash: the .5 s windup, then the 4 m field at the pilot's feet (the stun's length).</summary>
        public static void SystemCrash(Actor a,float at)
        {
            float rel=Cast(a,CyberLordSpec.Three,at);var k=a.CyberKit;k.CrashAt=a.Position;k.CrashBorn=rel;k.CrashUntil=rel+CyberLordSpec.CrashStun;
        }
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
            a.Kernel=s;a.HasKernel=true;
            if(!connect){s.Outcome=2;s.Until=at+NativeKernelPanicRules.Windup+NativeKernelPanicRules.WhiffRecovery;s.Ready=at+NativeKernelPanicRules.Cooldown*.5f;return;}
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
