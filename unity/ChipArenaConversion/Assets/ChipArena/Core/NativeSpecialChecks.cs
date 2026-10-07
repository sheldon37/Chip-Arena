#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class SpecialFixtures {public string commit;public SpecialStartFixture[] starts;public SpecialStrikeFixture[] strikes;public SpecialRecoveryFixture[] recoveries;public SpecialFlightFixture[] flights;public SpecialCeilingFixture[] ceilings;public SpecialEnvelopeFixture[] envelopes;}
    [Serializable] public sealed class SpecialStartFixture {public string spec;public float rate,resource,remaining,release,until,cooldown;public int projectiles;public bool grounded,busy,committed,started;}
    [Serializable] public sealed class SpecialStrikeFixture {public string kind;public float yaw,range,extra;public V3 delta;public bool hit;}
    [Serializable] public sealed class SpecialRecoveryFixture {public bool used;public V3 move;public float yaw,landed,pickup,roll,rollEnd,until;public SpecialRecoverySample[] samples;}
    [Serializable] public sealed class SpecialRecoverySample {public float time;public bool pickup,@protected;public string phase;public V3 motion;}
    [Serializable] public sealed class SpecialFlightFixture {public V3 initial,velocity,displacement;public float gravity,dt;}
    [Serializable] public sealed class SpecialCeilingFixture {public V3 position,delta,center;public float radius,fraction;public bool hit;}
    [Serializable] public sealed class SpecialEnvelopeFixture {public string spec;public float rate,time,coil,drive,recovery,weight;}
    public static class NativeSpecialChecks
    {
        private static void Check(bool ok,string message){if(!ok)throw new Exception("Melee specials: "+message);}
        private static void Near(float a,float b,string label,float tolerance=.00005f)=>Check(MathUtil.Finite(a)&&Math.Abs(a-b)<=tolerance,label+" "+a+" != "+b);
        public static List<string> Run(ConversionContent content,SpecialFixtures f)
        {
            Check(f.commit=="05950eaef80ff2483a9c44d898cad98be1a76bbc"&&f.starts.Length==96,"fixture identity");int frames=0;
            foreach(var start in f.starts)
            {
                var weapon=content.Weapon(start.spec);bool begin=NativeSpecialRules.CanBegin(start.spec,start.grounded,start.busy,start.committed,false,start.projectiles,start.resource,(float)weapon.secondaryCost);
                Check(begin==start.started,"start gate "+start.spec);if(!begin)continue;
                var a=NativeSpecialRules.Begin(start.spec,start.rate,2,.7f);Near(a.Release,start.release,"release");Near(a.Until,start.until,"action end");Near((NativeSpecialRules.Windup(start.spec)+NativeSpecialRules.Recovery(start.spec))/start.rate,start.cooldown,"primary cooldown");Near(start.resource-(float)weapon.secondaryCost,start.remaining,"resource debit");
                Check(!NativeSpecialRules.CanLand(a,a.Release+.1f,true,0),"premature slam");
                if(start.spec=="greatsword")
                {
                    a.Released=true;Check(!NativeSpecialRules.CanLand(a,a.Release+.1f,true,0)&&!NativeSpecialRules.CanLand(a,a.Release+.4f,false,0)&&!NativeSpecialRules.CanLand(a,a.Release+.4f,true,2),"slam without landing");
                    Check(NativeSpecialRules.CanLand(a,a.Release+.4f,true,0),"real landing rejected");a.Landed=a.Release+.4f;Check(!NativeSpecialRules.CanLand(a,a.Landed+.1f,true,0),"repeated slam");
                }
            }
            foreach(var s in f.strikes)Check(NativeSpecialRules.Strike(s.delta,s.range,s.extra,s.yaw,s.kind)==s.hit,"strike filter "+s.kind);
            foreach(var r in f.recoveries)
            {
                var k=new NativeRecovery{At=1,Until=6,PickupUsed=r.used};NativeSpecialRules.Land(k,2);
                Near(k.PickupUntil,r.pickup,"pickup duration");Near(k.RollAt,r.roll,"roll delay");Near(k.RollUntil,r.rollEnd,"roll duration");Near(k.Until,r.until,"recovery duration");
                foreach(var s in r.samples)
                {
                    Check(NativeSpecialRules.Pickup(k,s.time)==s.pickup&&NativeSpecialRules.Protected(k,s.time)==s.@protected,"recovery protection");
                    Check(NativeSpecialRules.Phase(k.Landed,k.PickupUntil,k.RollAt,k.RollUntil,k.PickupUsed,s.time)==s.phase,"recovery phase");
                    Near((NativeSpecialRules.RecoveryMotion(k,s.time,r.move,r.yaw*180/(float)Math.PI)-s.motion).Length,0,"recovery motion");
                }
                var direction=k.Direction;NativeSpecialRules.RecoveryMotion(k,2.65f,new V3(-5,0,3),123);Near((k.Direction-direction).Length,0,"roll direction changed after latch");
            }
            foreach(var flight in f.flights){var velocity=flight.initial;var delta=NativeSpecialRules.Ballistic(ref velocity,flight.gravity,flight.dt);Near((velocity-flight.velocity).Length,0,"ballistic velocity");Near((delta-flight.displacement).Length,0,"ballistic chord");}
            foreach(var c in f.ceilings){bool hit=NativeSpecialRules.Ceiling(c.position,c.delta,c.radius,out float fraction,out V3 center);Check(hit==c.hit,"ceiling hit");if(hit){Near(fraction,c.fraction,"ceiling fraction");Near((center-c.center).Length,0,"ceiling center");}}
            foreach(var e in f.envelopes)
            {
                var action=NativeSpecialRules.Begin(e.spec,e.rate,2,0);var a=new Actor{SpecialKind=action.Kind,SpecialAt=action.At,SpecialRelease=action.Release,SpecialUntil=action.Until};
                NativeSpecialPose.Envelope(a,e.time,out float coil,out float drive,out float recovery,out float weight);Near(coil,e.coil,"pose coil");Near(drive,e.drive,"pose drive");Near(recovery,e.recovery,"pose recovery");Near(weight,e.weight,"pose weight");
            }
            // Pose transforms are native adaptations. Check finite/unit rotations, rewindability and
            // repeat-render stability independently of the source-envelope comparisons above.
            foreach(string spec in new[]{"sword","daggers","shield","greatsword"})foreach(bool first in new[]{false,true})
            {
                var action=NativeSpecialRules.Begin(spec,1,2,0);
                for(int frame=0;frame<240;frame++)
                {
                    float time=2+frame/120f;var a=new Actor{SpecialKind=action.Kind,SpecialAt=2,SpecialRelease=action.Release,SpecialUntil=action.Until,SpecialReleased=time>=action.Release,SpecialLanded=spec=="greatsword"&&time>=3?3:-1};
                    var x=new CapturedRig.Pose();var y=new CapturedRig.Pose();NativeSpecialPose.Special(x,a,time,first);NativeSpecialPose.Special(y,a,time,first);Pose(x,y,false);frames++;
                }
            }
            foreach(bool used in new[]{false,true})for(int frame=0;frame<240;frame++)
            {
                float time=1+frame/120f;var a=new Actor{DownAt=1,DownUntil=3,DownLanded=2,DownPickup=2.22f,DownUsed=used,DownRoll=2.4f,DownRollEnd=2.8f,DownDirection=new V3(1,0,-1).Normalized,Velocity=new V3(0,7-23*(time-1),0),Yaw=135};
                var x=new CapturedRig.Pose();var y=new CapturedRig.Pose();NativeSpecialPose.Recover(x,a,time);NativeSpecialPose.Recover(y,a,time);Pose(x,y,true);frames++;
            }
            return new List<string>{"Source special parity: 96 start gates, 324 strike filters, 216 recovery samples, 36 ballistic steps, 24 ceilings and 108 pose envelopes", "Native special/recovery poses: "+frames+" finite, repeatable samples; recovery floor clearance and latched roll heading"};
        }
        private static void Pose(CapturedRig.Pose a,CapturedRig.Pose b,bool floor)
        {
            for(int i=0;i<a.Rotation.Length;i++)
            {
                Near(a.Rotation[i].Length(),1,"unit pose rotation",.0002f);Near(Vector3.Distance(a.Position[i],b.Position[i]),0,"repeat position");Near(1-Math.Abs(Quaternion.Dot(a.Rotation[i],b.Rotation[i])),0,"repeat rotation");
            }
            Check(MathUtil.Finite(a.Lowest()),"finite pose bounds");if(floor)Check(a.Lowest()>=-.8651f,"recovery below capsule floor");
        }
    }
}
#endif
