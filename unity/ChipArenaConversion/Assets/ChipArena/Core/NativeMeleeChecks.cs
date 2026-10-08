#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class MeleeFixtures {public string commit;public MeleeSwingFixture[] swings;public MeleeBlockFixture[] blocks;public MeleeExternalFixture[] externals;public MeleeWindowFixture[] windows;}
    [Serializable] public sealed class MeleeSwingFixture {public string spec;public int index;public float rate,at,yaw,hitAt,hitUntil,until,stepAt,stepUntil,distance,stun;public MeleeSampleFixture[] samples;}
    [Serializable] public sealed class MeleeSampleFixture {public float time,angle;public V3 step;public bool before;public MeleeContactFixture[] contacts;}
    [Serializable] public sealed class MeleeContactFixture {public V3 delta;public float extra;public bool hit;}
    [Serializable] public sealed class MeleeBlockFixture {public float damage,impulse,ready,travel,stun,punish;}
    [Serializable] public sealed class MeleeExternalFixture {public V3 ext,move,remaining,controlled;public float lift;}
    [Serializable] public sealed class MeleeWindowFixture {public float window,duration;}
    public static class NativeMeleeChecks
    {
        private static void Check(bool ok,string message){if(!ok)throw new Exception("Melee: "+message);}
        private static void Near(float a,float b,string field,float tolerance=.00003f)=>Check(MathUtil.Finite(a)&&Math.Abs(a-b)<=tolerance,field+" "+a+" != "+b);
        private static void Vector(V3 a,V3 b,string field,float tolerance=.00003f)=>Near((a-b).Length,0,field,tolerance);
        public static List<string> Run(ConversionContent content,MeleeFixtures f)
        {
            Check(f.commit=="05950eaef80ff2483a9c44d898cad98be1a76bbc"&&f.swings.Length==64,"fixture identity");int contacts=0,samples=0;
            foreach(var expected in f.swings)
            {
                var stats=NativeContentRules.Stats(content,expected.spec,new Dictionary<string,int>());stats["attackRate"]=expected.rate;
                var weapon=content.Weapon(expected.spec);var swing=NativeMeleeRules.Begin(weapon,stats,expected.at,expected.index,expected.yaw);
                Near(swing.HitAt,expected.hitAt,"windup");Near(swing.HitUntil,expected.hitUntil,"active");Near(swing.Until,expected.until,"commitment");
                Near(swing.StepAt,expected.stepAt,"step start");Near(swing.StepUntil,expected.stepUntil,"step end");Near(swing.Distance,expected.distance,"step distance");
                Near(NativeMeleeRules.HitStun(weapon,expected.rate),expected.stun,"hit stun");
                foreach(var sample in expected.samples)
                {
                    Near(NativeMeleeRules.Angle(swing,sample.time),sample.angle,"sweep angle");Vector(NativeMeleeRules.Step(swing,sample.time,1f/60),sample.step,"footwork",.001f);
                    Check(NativeMeleeRules.BeforeStrike(swing,sample.time)==sample.before,"half-tick cancel boundary");
                    foreach(var c in sample.contacts){Check(NativeMeleeRules.Contact(swing,sample.time,c.delta,c.extra)==c.hit,"source contact "+expected.spec+"/"+expected.index+" at "+sample.time);contacts++;}samples++;
                }
                // One contact per target across the active interval, and obstruction must not spend the contact.
                float t=(swing.HitAt+swing.HitUntil)/2;var delta=new V3((float)Math.Sin(swing.Yaw),0,(float)Math.Cos(swing.Yaw));
                Check(!swing.Contact("target",t,delta,.45f,false),"wall bypass");Check(swing.Contact("target",t,delta,.45f,true),"occlusion consumed future contact");
                Check(!swing.Contact("target",t+.001f,delta,.45f,true),"repeated damage in one swing");Check(swing.Contact("second",t,delta,.45f,true),"second target lost");
                var next=NativeMeleeRules.Begin(weapon,stats,expected.at,expected.index,expected.yaw);Check(next.Contact("target",t,delta,.45f,true),"contact ledger leaked to next swing");
                float travel=0;for(int n=0;n<180;n++)travel+=NativeMeleeRules.Step(swing,swing.At+n/60f,1f/60).Length/60;
                Near(travel,swing.Distance,"integrated footwork",.0001f);
            }
            foreach(var b in f.blocks){Near(NativeMeleeRules.BlockStun(b.damage,b.impulse),b.stun,"block stun");Near(NativeMeleeRules.BlockPunish(b.damage,b.ready,b.travel),b.punish,"riposte window");}
            foreach(var e in f.externals)
            {
                var remaining=e.ext;var motion=NativeMeleeRules.External(ref remaining,1f/60,out float steeringSpeed);
                Vector(remaining,e.remaining,"external decay");Near(motion.y,e.lift,"one-shot lift");Vector(NativeMeleeRules.CounterSteer(e.move,motion,steeringSpeed),e.controlled,"counter steer");
                var later=NativeMeleeRules.External(ref remaining,1f/60);Check(later.y==0,"vertical impulse repeats");
            }
            var baseStats=NativeContentRules.Stats(content,"sword",new Dictionary<string,int>());
            foreach(var w in f.windows){baseStats["blockWindow"]=w.window;Near(NativeMeleeRules.BlockDuration(baseStats),w.duration,"block window clamp");}
            Near(NativeMeleeRules.Rebound(30,1),280,"base rebound");Near(NativeMeleeRules.Rebound(1000,6),800,"rebound cap");
            Near(NativeMeleeRules.ReboundTravel(30,280,1,1,1),0,"base rebound needs no extra travel");
            return new List<string>{"Source melee parity: 64 swings, "+samples+" timing/footwork samples, "+contacts+" contacts, 90 block/riposte cases and 9 external-motion cases", "Melee contact ledgers, obstruction gating, integrated footwork, block clamps, rebound cap and one-shot lift checks"};
        }
    }
}
#endif
