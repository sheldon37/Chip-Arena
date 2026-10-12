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
        /// 4 Kernel Panic cast at `at` on `victim` standing at `victimFeet`: a whiff (no victim: the .6 s recovery), or a connect
        /// at the lunge's end (the .6 s cut-in, then the sequence) with the nominal waypoints, the wall-splat variant moving the
        /// launch's end to `wall` (the splat point) when given; `endedAt` ends it early.
        /// </summary>
        public static void KernelPanic(Actor a,float at,int victim,Vector3 victimFeet,bool connect=true,Vector3? wall=null,float endedAt=-1,int seed=7)
        {
            Cast(a,CyberLordSpec.Four,at);var k=a.CyberKit;var c=k.Casts[CyberLordSpec.Four];k.PanicSeed=seed;k.PanicYaw=a.Yaw;
            if(!connect){k.PanicOutcome=CyberLordSpec.Whiff;k.PanicVictim=0;c.Ready=at+CyberLordSpec.Cooldowns[CyberLordSpec.Four]*.5f;return;}
            float connectAt=c.Until;c.RecoverUntil=c.Until;k.PanicOutcome=endedAt>0?CyberLordSpec.EndedEarly:CyberLordSpec.Connect;k.PanicConnect=connectAt;k.PanicStart=connectAt+CyberLordSpec.CutIn;k.PanicEndedAt=endedAt;
            k.PanicVictim=victim;k.PanicAnchor=new V3(victimFeet.X,victimFeet.Y,victimFeet.Z);k.PanicWall=wall.HasValue;
            var f=KernelPanicPath.Forward(a.Yaw);var casterAt=victimFeet-f*1.0f;var pts=new Vector3[KernelPanicPath.Points];KernelPanicPath.NominalPoints(victimFeet,casterAt,a.Yaw,pts);
            if(wall.HasValue)
            {
                // The launch stops at the wall: the rest of the choreography rebuilt from the splat point (the host's clamp).
                var w=wall.Value;var d=pts[KernelPanicPath.LaunchEnd]-w;pts[KernelPanicPath.LaunchEnd]=w;
                for(int i=KernelPanicPath.Behind;i<=KernelPanicPath.Reappear;i++)pts[i]-=new Vector3(d.X,0,d.Z);
                pts[KernelPanicPath.Behind]=w-f*.9f;pts[KernelPanicPath.Behind].Y=0;pts[KernelPanicPath.Landing]-=f*.9f;pts[KernelPanicPath.Reappear]-=f*.9f;pts[KernelPanicPath.Apex]-=f*.6f;pts[KernelPanicPath.Above]-=f*.6f;
            }
            k.PanicPoints=new V3[pts.Length];for(int i=0;i<pts.Length;i++)k.PanicPoints[i]=new V3(pts[i].X,pts[i].Y,pts[i].Z);
        }
        /// <summary>Where the fixture places the caster and the victim during a Kernel Panic (the host would move the bodies so).</summary>
        public static void PanicBodies(Actor caster,Actor victim,float time,CyberLordView scratch)
        {
            CyberLordFeed.Read(caster,scratch);if(!(scratch.PanicConnect>-90))return;float age=time-scratch.PanicStart;
            if(time<scratch.PanicConnect)return;
            var v=KernelPanicPath.Victim(scratch,age);var c=KernelPanicPath.Caster(scratch,age,out _);
            if(victim!=null){victim.Position=new V3(v.X,v.Y,v.Z);victim.Grounded=v.Y<=.02f;}
            caster.Position=new V3(c.X,c.Y,c.Z);caster.Grounded=c.Y<=.02f;
            // The caster faces the victim through the sequence (the host's facing).
            var d=v-c;if(d.X*d.X+d.Z*d.Z>.01f&&age>=KernelPanicPath.Steps[1])caster.Yaw=(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        }
        /// <summary>The ids the fixtures' Hit events carry for each Kernel Panic step (the event's Detail), and when they land.</summary>
        public static float PanicHitTime(CyberLordView v,int step)=>step<1||step>5?-100:v.PanicStart+KernelPanicPath.HitAge(step);
    }
}
