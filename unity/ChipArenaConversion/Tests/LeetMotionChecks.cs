#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // L33t H4x in third person (owner request 2026-10-09: it ran with the sword set's animations). The source poses it as a
    // magic-class staff pilot (content.ts class 'magic', stances.ts leet:MAGIC, character-animation.ts magicLayer): it has
    // no clips of its own. Its replicated MagicVisual stays 0 (the simulation keys element effects on it), so the presentation
    // school (MagicPose.School) routes it onto the magic gait. Checked here: the school and cast timing, the route for every
    // action (never the sword set, also in first person), the magic layer's state per action, the torso and arms moving
    // through the walk and run, the staff upright in the carrying hand and across the chest in the guard, no allocation per
    // frame and frame-to-frame continuity. Tests/MagicMotionChecks.cs compares its gait and casts with the pinned source.
    internal static class LeetMotionChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("L33t H4x motion: "+what);count++;}
        private static void Near(float a,float b,string what,float tol=1e-4f)=>Check(Math.Abs(a-b)<=tol&&!float.IsNaN(a),what+": "+a+" != "+b);
        private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))));
        private static Actor Leet()=>new Actor{Id=11,SourceSpec="leet",Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1)};
        private static readonly HashSet<string> SwordStates=new HashSet<string>{"idle","walk","run","block","stagger","hit reaction","cutA","cutB","cutC","cutD","cutA recovery","cutB recovery","cutC recovery","cutD recovery"};
        public static void Run()
        {
            Schools();Routes();Phrase();
            var log=new List<string>();Actions(log);Travel(log);Allocation(log);Continuity(log);
            Console.WriteLine("PASS L33t H4x motion ("+count+" checks: school and .18 s release, magic route for every action and no sword layers in first person, default-branch cast phrase, "+string.Join(", ",log)+")");
        }
        private static void Schools()
        {
            var a=Leet();Check(a.MagicVisual==0,"the simulation's element stays 0");Check(MagicPose.School(a)==MagicPose.Leet,"L33t H4x is a magic school");
            string[] specs={"fire","water","lightning","darkness"};
            for(int e=1;e<=4;e++){Check(MagicPose.School(new Actor{MagicVisual=e,SourceSpec=specs[e-1]})==e,specs[e-1]+" school unchanged");Near(MagicPose.Windup(e,1.3f),NativeAbilityRules.PrimaryWindup(e,1.3f),specs[e-1]+" windup unchanged");}
            foreach(var spec in new[]{"sword","daggers","greatsword","shield","emperor","ansatsuken","siren","pistol","rifle","machinegun",null,""})Check(MagicPose.School(new Actor{SourceSpec=spec})==0,(spec??"null")+" is no magic school");
            Check(MagicPose.School(null)==0,"no actor");
            // content.ts castWindup leet .18 / rate, the release NativeArena stamps.
            Near(MagicPose.Windup(MagicPose.Leet,1),.18f,"leet release");Near(MagicPose.Windup(MagicPose.Leet,1.25f),.144f,"leet release at 1.25 attack rate");
            Check(MeleeStance.For("leet")==MeleeStance.Magic&&MeleeStance.MagicSpec("leet"),"stances.ts leet:MAGIC");
            Check(!MeleeMotion.Handles("leet")&&!NativeFighterPose.Handles("leet"),"no captured sword set or fighter style");
        }
        // Every action the pilot can show, in third person (magic gait) and first person (generic path without sword layers).
        private static IEnumerable<(string name,Actor actor,float time,string state)> ActionTable()
        {
            const float t=20;
            Actor A(Action<Actor> set){var a=Leet();set(a);return a;}
            yield return ("idle",A(a=>{}),t,MagicMotion.Carry);
            yield return ("walk",A(a=>a.Velocity=new V3(0,0,2.8f)),t,MagicMotion.Carry);
            yield return ("run",A(a=>a.Velocity=new V3(0,0,6.8f)),t,MagicMotion.Carry);
            yield return ("sprint",A(a=>{a.Velocity=new V3(0,0,9);a.Sprinting=true;}),t,MagicMotion.Carry);
            yield return ("jump",A(a=>{a.Grounded=false;a.Velocity=new V3(0,6,3);}),t,MagicMotion.Carry);
            yield return ("primary cast",A(a=>{a.AttackAt=t-.1f;a.AttackUntil=t+.5f;}),t,MagicMotion.Cast);
            yield return ("cast released",A(a=>{a.AttackAt=t-.3f;a.LastShot=t-.12f;a.AttackUntil=t+.3f;}),t,MagicMotion.Cast);
            yield return ("guard",A(a=>{a.BlockAt=t-.1f;a.BlockUntil=t+.4f;}),t,MagicMotion.Guard);
            yield return ("roll",A(a=>a.RollAt=t-.1f),t,MagicMotion.Roll);
            yield return ("hit stun",A(a=>{a.StunUntil=t+.3f;a.ReactionAt=t-.05f;a.ReactionUntil=t+.3f;a.ReactionStrength=1;a.ReactionDir=new V3(0,0,1);}),t,MagicMotion.Carry);
            yield return ("juggle",A(a=>a.JuggleUntil=t+1.2f),t,MagicMotion.Juggle);
            yield return ("juggle over roll",A(a=>{a.JuggleUntil=t+1.2f;a.RollAt=t-.1f;}),t,MagicMotion.Juggle);
            yield return ("knockdown",A(a=>{a.DownAt=t-.2f;a.DownUntil=t+1;a.DownLanded=t-.1f;a.DownPickup=t+.6f;}),t,MagicMotion.Recovery);
            yield return ("special",A(a=>{a.SpecialKind="sword-launch";a.SpecialAt=t-.1f;a.SpecialRelease=t+.1f;a.SpecialUntil=t+.5f;}),t,"sword-launch");
            yield return ("dead",A(a=>a.Hp=0),t,MagicMotion.Carry);
        }
        private static void Routes()
        {
            foreach(var (name,a,_,_) in ActionTable())
            {
                Check(NativePoseRoute.For(a,false)==PoseRoute.Magic,name+": third person walks on the magic gait");
                Check(NativePoseRoute.For(a,true)==PoseRoute.Generic,name+": first person takes the view-model path");
                Check(!NativePoseRoute.SwordLayers(a),name+": no sword cut, block, stagger or blade floor");
            }
            // The other pilots keep their routes.
            Check(NativePoseRoute.For(new Actor{SourceSpec="sword",Weapon=WeaponId.Sword},false)==PoseRoute.CapturedMelee,"sword keeps captured melee");
            Check(NativePoseRoute.For(new Actor{SourceSpec="fire",MagicVisual=1,Weapon=WeaponId.Sword},false)==PoseRoute.Magic,"fire keeps the magic gait");
            Check(NativePoseRoute.For(new Actor{SourceSpec="emperor",Weapon=WeaponId.Sword},false)==PoseRoute.Fighter,"Emperor keeps its style");
            Check(NativePoseRoute.For(new Actor{SourceSpec="siren",Weapon=WeaponId.Sword},false)==PoseRoute.Generic&&NativePoseRoute.For(new Actor{SourceSpec="pistol",Weapon=WeaponId.Pistol},false)==PoseRoute.Gun,"the Siren stays generic, guns walk on the gun gait");
            Check(NativePoseRoute.SwordLayers(new Actor{SourceSpec="sword",Weapon=WeaponId.Sword})&&!NativePoseRoute.SwordLayers(new Actor{SourceSpec="fire",MagicVisual=3,Weapon=WeaponId.Sword}),"sword layers only for the sword family");
            Check(NativePoseRoute.For(null,false)==PoseRoute.Generic&&!NativePoseRoute.SwordLayers(null),"no actor");
        }
        // magic-motion.ts with spec 'leet': the default (fire-like) coil and release, the follow-through's wrist flick of .3
        // (fire .5), twist x1, released at .18 s.
        private static void Phrase()
        {
            var o=new float[13];var a=Leet();a.AttackAt=10;a.LastShot=-100;
            float w=MagicPose.Sample(a,10+.18f,0,false,o);Near(w,1,"full weight at release");
            Near(o[0],-1.65f+.18f,"release arm x");Near(o[1],0,"release arm y");Near(o[3],-.08f,"release elbow");Near(o[4],-.18f,"release wrist");Near(o[7],.4f,"release twist");
            MagicPose.Sample(a,10+.18f+.1f-1e-4f,0,false,o);Near(o[4],-.18f-.3f,"follow-through wrist flick (spec leet, not fire)",2e-3f);
            MagicPose.Sample(a,10+.18f*.6f,0,false,o);Near(o[0],-2.3f,"coil arm x");Near(o[7],-.48f,"coil twist");
            var fire=new Actor{MagicVisual=1,SourceSpec="fire",CastRate=1,AttackAt=10,LastShot=-100};var f=new float[13];
            MagicPose.Sample(fire,10+.16f+.1f-1e-4f,0,false,f);Near(f[4],-.18f-.5f,"fire keeps its .5 flick",2e-3f);
            // A first-person L33t H4x casts with the same phrase (MagicPose.Apply), not a sword cut.
            var p=new CapturedRig.Pose();a.AttackAt=10;MagicPose.Apply(p,a,10.18f,true,o,0);
            Check(Angle(p.Rotation[CapturedRig.RArm],MeleeMath.Euler(o[0],o[1],o[2]))<1e-3f,"first-person cast drives the free arm");
        }
        private sealed class Driver
        {
            public readonly MagicMotion Motion=new MagicMotion();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            public readonly float[] Channels=new float[13];public readonly NativeJuggleTrack Juggle=new NativeJuggleTrack();public readonly NativeGuardMotion Guard=new NativeGuardMotion();
            public bool React;public float Z;
            // As CapturedAnimation.Player.MagicGaitPose: juggle observed every frame, the rig at the travelled position.
            public string Step(Actor a,float t)
            {
                bool juggled=Juggle.Observe(a.JuggleUntil,t)&&NativeJugglePose.Active(a.JuggleUntil,t);
                bool roll=MagicMotion.Rolls(a,t,juggled);
                Motion.Rig.GroupPosition=new Vector3(0,.865f,Z);Motion.Rig.GroupYaw=roll?0:-a.Yaw*(float)Math.PI/180;
                string state=Motion.Pose(a,t,Pose,Channels,Guard.Sample(t,t<a.BlockUntil),juggled,Juggle,out React);
                Motion.Finish(Pose,t);return state;
            }
        }
        private static Vector3 Shaft(MagicMotion m)=>Vector3.Transform(Vector3.UnitZ,m.Rig.ChainQuat(CapturedRig.LHand,-1));
        private static void Actions(List<string> log)
        {
            float minUp=1,maxGuard=0;
            foreach(var (name,a,time,state) in ActionTable())
            {
                var d=new Driver();string got=null;
                // Settle 40 frames before the action time with the same actor, then sample at it.
                for(int f=40;f>=0;f--)got=d.Step(a,time-f/60f);
                Check(got==state,name+" shows "+(got??"null")+", expected "+state);Check(!SwordStates.Contains(got),name+" is not a sword-set state");
                Check(d.React==(name=="hit stun"),name+(d.React?" adds":" skips")+" the hit reaction");
                for(int b=0;b<MeleeRig.N;b++)Check(MathUtil.Finite(d.Motion.Rig.Q[b].W)&&Math.Abs(d.Motion.Rig.Q[b].Length()-1)<1e-3f&&MathUtil.Finite(d.Motion.Rig.P[b].X),name+" finite joint "+b);
                Check(d.Motion.GroundSkipped==(name.StartsWith("juggle")||name=="roll"||name=="knockdown"||name=="dead"),name+" ground pass "+(d.Motion.GroundSkipped?"skipped":"kept"));
                float up=Shaft(d.Motion).Y;
                if(state==MagicMotion.Carry&&name!="dead"&&name!="hit stun"||state==MagicMotion.Cast){Check(up>.82f,name+": the staff stands upright in the carrying hand ("+up.ToString("0.00")+")");minUp=Math.Min(minUp,up);}
                if(state==MagicMotion.Guard){Check(Math.Abs(up)<.35f,name+": the staff lies across the chest ("+up.ToString("0.00")+")");maxGuard=Math.Max(maxGuard,Math.Abs(up));}
            }
            // A juggled L33t H4x shows animateJuggle (body pitched back past -.6 rad, its height near .03 after the launch).
            var j=new Driver();var ja=Leet();ja.JuggleUntil=21.25f;for(float t=20;t<20.4f;t+=1/60f)j.Step(ja,t);
            Check(MeleeMath.ToEuler(j.Motion.Rig.Q[CapturedRig.Body]).X<-.6f&&Math.Abs(j.Motion.Rig.P[CapturedRig.Body].Y-.03f)<.02f,"juggle pose over the magic gait");
            log.Add("staff upright >= "+minUp.ToString("0.00")+" in carry/cast, guard |up| <= "+maxGuard.ToString("0.00"));
        }
        // The torso counter-twists and the free arm swings with the stride; the staff arm's carry gives way to the swing.
        private static void Travel(List<string> log)
        {
            var idle=new Driver();var ia=Leet();for(float t=0;t<1;t+=1/60f)idle.Step(ia,t);var carryArm=idle.Motion.Rig.Q[CapturedRig.LArm];
            string[] names={"walk","run"};float[] speeds={2.8f,6.8f};
            for(int k=0;k<2;k++)
            {
                var d=new Driver();var a=Leet();a.Velocity=new V3(0,0,speeds[k]);var lo=new float[4];var hi=new float[4];
                for(int i=0;i<4;i++){lo[i]=float.MaxValue;hi[i]=float.MinValue;}
                int[] bones={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest};float t=0,armLo=float.MaxValue,armHi=float.MinValue,carryAway=0;
                for(int f=0;f<150;f++,t+=1/60f)
                {
                    d.Z+=speeds[k]/60;d.Step(a,t);if(f<60)continue;
                    for(int i=0;i<3;i++){float y=d.Motion.Rig.E[bones[i]].Y;lo[i]=Math.Min(lo[i],y);hi[i]=Math.Max(hi[i],y);}
                    float swing=MeleeMath.ToEuler(d.Motion.Rig.Q[CapturedRig.RArm]).X;armLo=Math.Min(armLo,swing);armHi=Math.Max(armHi,swing);
                    carryAway=Math.Max(carryAway,Angle(d.Motion.Rig.Q[CapturedRig.LArm],carryArm));
                }
                for(int i=0;i<3;i++)Check(hi[i]-lo[i]>.06f,names[k]+" torso joint "+bones[i]+" swings ("+(hi[i]-lo[i])+" rad)");
                Check(armHi-armLo>(k==0?.15f:.3f),names[k]+" free arm swings ("+(armHi-armLo)+" rad)");
                if(k==1)Check(carryAway>.2f,"running lets the staff arm swing off its carry ("+carryAway+" rad)");
                log.Add(names[k]+" torso yaw "+(hi[0]-lo[0]).ToString("0.00")+"/"+(hi[1]-lo[1]).ToString("0.00")+"/"+(hi[2]-lo[2]).ToString("0.00")+" rad, arm swing "+(armHi-armLo).ToString("0.00")+" rad");
            }
        }
        // One scripted bout over every action; no managed allocation once warm.
        private static void Bout(Driver d,Actor a,ref float t,int frames)
        {
            for(int f=0;f<frames;f++,t+=1/60f)
            {
                float u=f/60f;a.Velocity=new V3(0,0,u<.5f?0:u<1.2f?2.8f:u<2?6.8f:0);a.Sprinting=u>=1.6f&&u<2;d.Z+=a.Velocity.z/60;
                if(f==150){a.AttackAt=t;a.AttackUntil=t+.6f;}if(f==168)a.LastShot=t;if(f==186){a.AttackAt=t;a.AttackUntil=t+.6f;}if(f==197)a.LastShot=t;
                if(f==240){a.BlockAt=t;a.BlockUntil=t+.4f;}
                if(f==290)a.RollAt=t;
                if(f==330)a.JuggleUntil=t+1.25f;if(f==390)a.JuggleUntil=0;
                if(f==420){a.StunUntil=t+.3f;a.ReactionAt=t;a.ReactionUntil=t+.3f;a.ReactionStrength=1;a.ReactionDir=new V3(0,0,1);}
                if(f==460){a.ReactionAt=-100;a.DownAt=t;a.DownUntil=t+.8f;a.DownLanded=t+.1f;a.DownPickup=t+.5f;}
                d.Step(a,t);
            }
        }
        private static void Allocation(List<string> log)
        {
            // Warm once (first frames of a pilot set up the gait), then the same bout again in continuous time.
            var d=new Driver();var a=Leet();float t=30;Bout(d,a,ref t,560);
            long before=GC.GetAllocatedBytesForCurrentThread();Bout(d,a,ref t,560);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"posing allocates "+bytes+" bytes over 560 frames");log.Add("no allocation over 560 frames of every action");
        }
        // Continuity through starting, walking, running, sprinting, stopping, a cast streak, a roll, the guard in and out and
        // the juggle launch and landing, sampled at 240 Hz. The source's own motion can be fast (the coil lifts the casting
        // hand about 1 m in .11 s) but is smooth: consecutive 1/240 s steps grow and shrink gradually. A snap is one step much
        // longer than both of its neighbours, so each point's step may exceed the larger neighbouring step by at most 1.5 cm
        // (upper body, both hands and the staff tip). Leaving a juggle, roll, guard or flinch blends as finishPose does.
        private static readonly int[] Upper={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,
            CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private static void Continuity(List<string> log)
        {
            const float Hz=240;var d=new Driver();var a=Leet();float t0=40,spike=0,spikeAt=-1,fastest=0;int spikePoint=-1,n=Upper.Length+1;
            var prev=new Vector3[n];var step=new float[n];var before=new float[n];
            bool cast1=false,shot1=false,cast2=false,shot2=false,roll=false,guard=false,juggle=false,land=false;
            for(int f=0;f<(int)(7.5f*Hz);f++)
            {
                float u=f/Hz,t=t0+u;a.Velocity=new V3(0,0,u<.5f?0:u<1.2f?2.8f:u<2?6.8f:u<2.4f?9:0);a.Sprinting=u>=2&&u<2.4f;d.Z+=a.Velocity.z/Hz;
                if(!cast1&&u>=3){cast1=true;a.AttackAt=t;a.AttackUntil=t+.6f;}if(!shot1&&u>=3.18f){shot1=true;a.LastShot=a.AttackAt+.18f;}
                if(!cast2&&u>=3.6f){cast2=true;a.AttackAt=t;a.AttackUntil=t+.6f;}if(!shot2&&u>=3.78f){shot2=true;a.LastShot=a.AttackAt+.18f;}
                if(!roll&&u>=4.6f){roll=true;a.RollAt=t;}
                if(!guard&&u>=5.4f){guard=true;a.BlockAt=t;a.BlockUntil=t+.4f;}
                if(!juggle&&u>=6.1f){juggle=true;a.JuggleUntil=t+1.25f;}if(!land&&u>=7f){land=true;a.JuggleUntil=0;}
                d.Step(a,t);var r=d.Motion.Rig;
                for(int k=0;k<n;k++)
                {
                    Vector3 p=k<Upper.Length?r.ChainPoint(Upper[k],-1):r.ChainPoint(CapturedRig.LHand,-1)+Shaft(d.Motion)*.9f;
                    float e=f>0?Vector3.Distance(p,prev[k]):0;fastest=Math.Max(fastest,e);
                    // The previous step against both of its neighbours.
                    if(f>2){float jump=step[k]-Math.Max(before[k],e);if(jump>spike){spike=jump;spikeAt=u-1/Hz;spikePoint=k;}}
                    before[k]=step[k];step[k]=e;prev[k]=p;
                }
            }
            Check(spike<.015f,"a joint snaps by "+spike.ToString("0.0000")+" m at "+spikeAt.ToString("0.000")+" s, point "+spikePoint);
            log.Add("no snap through 7.5 s of actions (largest step spike "+(spike*1000).ToString("0.0")+" mm at "+spikeAt.ToString("0.000")+" s point "+spikePoint+" over its neighbours, fastest step "+(fastest*100).ToString("0.0")+" cm at 240 Hz)");
        }
    }
}
#endif
