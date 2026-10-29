#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The zombies' motion on the player's model (owner 2026-10-29: "the idle; they stand "unnaturally" like bending their lower
    // back weird"; Core/NativeZombiePosture). Each modelled kind's gait (Core NativeHordeGait, as the runtime drives it) at rest, at
    // its walk and at its run, bound to the first outfit's measured skeleton (ModelPoseChecks.Measured) as the runtime binds it
    // before (the gait's pose as it is) and after (fitted, with the zombie stance). Measured: where the trunk bends (the lumbar
    // share of the bend from the pelvis to the top of the back), the knees, how far the hips sit behind the feet, how far the arms
    // hang out from the body, the planted feet on their marks, the hips' speed and the head over the hit capsule. Then: the fit
    // keeps the chest's, neck's and head's world turn; the stance is off for a pilot; a legless crawl's pelvis at the gait's
    // height; a dying add's rise eases out; the same input gives the same pose. The numbers are printed for docs/ZOMBIE-MODELS.md.
    // A synthetic skeleton, not a render: how it reads on screen is the owner's PC's to judge.
    internal static class ZombiePostureChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie posture: "+what);count++;}
        private const float Deg=57.29578f,Dt=1/60f;
        private static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};

        private sealed class Drive
        {
            public readonly NativeHordeGait G=new NativeHordeGait();public readonly CapturedRig.Pose P=new CapturedRig.Pose();public HordeMotionInput F;public Vector3 At;
            public Drive(int kind,int id,int look){F=new HordeMotionInput{Kind=kind,Look=look,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};At=new Vector3(0,.85f,0);}
            public void Step(float t,float speed){var v=new Vector3(0,0,speed);At+=v*Dt;F.Time=t;F.Root=At;F.Velocity=v;F.Yaw=0;F.YawRate=0;F.Ground=0;G.Pose(F,P);}
        }
        private sealed class Stats
        {
            public readonly List<float> Lumbar=new List<float>(),Upper=new List<float>(),Knee=new List<float>(),Back=new List<float>(),Spread=new List<float>(),Foot=new List<float>(),Rise=new List<float>(),Head=new List<float>(),GameHead=new List<float>();
            public float Share=>Lumbar.Sum()/Math.Max(1e-4f,Lumbar.Sum()+Upper.Sum());
        }
        private static float Mean(List<float> a)=>a.Count==0?0:a.Average();
        private static float P95(List<float> a){if(a.Count==0)return 0;var o=a.OrderBy(x=>x).ToArray();return o[(int)(.95*(o.Length-1))];}
        private static float Angle(Quaternion q){q=Quaternion.Normalize(q);return 2*(float)Math.Acos(Math.Min(1,Math.Abs(q.W)))*Deg;}
        private static float Between(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)))))*Deg;

        public static void Run(string root)
        {
            var rig=ModelPoseChecks.Measured();
            int pelvis=rig.Of[CapturedRig.Pelvis],low=rig.Of[CapturedRig.Spine],top=rig.Of[CapturedRig.Chest],head=rig.Of[CapturedRig.Head];
            Check(pelvis>=0&&low>=0&&top>=0&&head>=0&&rig.Names[low]=="spine_02"&&rig.Names[top]=="spine_05","the measured skeleton maps the trunk (pelvis, spine_02, spine_05, head)");
            var fit=new CapturedRig.Pose();var lines=new List<string>();
            float worstIdleKnee=0,worstShare=0,worstFoot=0,worstBack=0;
            foreach(int kind in Kinds)
            {
                var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[NativeHordeKinds.Index(kind)];
                foreach(var (gait,speed) in new[]{("rest",0f),("walk",st.walk/sc),("run",st.run/sc)})
                {
                    var before=new Stats();var after=new Stats();
                    for(int look=0;look<2;look++)
                    {
                        var d=new Drive(kind,40000+kind*101+look*17,look);var sb=new NativeVisualRig.State(rig);var sa=new NativeVisualRig.State(rig);float lastB=float.NaN,lastA=float.NaN;
                        for(int i=0;i<330;i++)
                        {
                            d.Step(i*Dt,speed);var p=d.P;p.World();
                            NativeVisualRig.Pose(sb,p,default(NativeVisualRig.Grasp),true,false);
                            NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(sa,kind,false,d.G.Sunk);NativeVisualRig.Pose(sa,fit,default(NativeVisualRig.Grasp),true,false);
                            if(i<90){lastB=sb.Position[pelvis].Y;lastA=sa.Position[pelvis].Y;continue;}
                            Measure(rig,sb,p,before,ref lastB);Measure(rig,sa,fit,after,ref lastA);
                            float gh=new Vector2(p.WorldPosition[CapturedRig.Head].X,p.WorldPosition[CapturedRig.Head].Z).Length();before.GameHead.Add(gh);after.GameHead.Add(gh);
                            // The fit keeps the trunk's top and the gaze where the gait put them.
                            if(i==200)foreach(int j in new[]{CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head})
                                Check(Angle(Quaternion.Inverse(p.WorldRotation[j])*fit.WorldRotation[j])<.05f,NativeHordeKinds.Ids[kind]+" "+gait+": the fit keeps "+CapturedRig.Bones[j].Name+"'s world turn");
                        }
                    }
                    string name=NativeHordeKinds.Ids[kind]+" "+gait;
                    lines.Add($"{name,-14} lumbar share {before.Share:0.00} -> {after.Share:0.00}; knee {Mean(before.Knee),4:0}deg -> {Mean(after.Knee),4:0}deg; hips behind the feet {Mean(before.Back):0.000} -> {Mean(after.Back):0.000} m; arm out {Mean(before.Spread),4:0.0}deg -> {Mean(after.Spread),4:0.0}deg; planted ankle off its mark p95 {P95(before.Foot)*100:0.0} -> {P95(after.Foot)*100:0.0} cm; hips' speed p95 {P95(before.Rise):0.00} -> {P95(after.Rise):0.00} m/s; head from the axis max {before.Head.Max():0.00} -> {after.Head.Max():0.00} (gait {before.GameHead.Max():0.00})");Console.WriteLine("  "+lines[lines.Count-1]);
                    float bend=Mean(after.Lumbar)+Mean(after.Upper);
                    if(bend>4){Check(after.Share<=.5f&&after.Share<before.Share,name+": the trunk bends up the back, not at the belt (lumbar share "+after.Share.ToString("0.00")+", was "+before.Share.ToString("0.00")+")");worstShare=Math.Max(worstShare,after.Share);}
                    if(gait=="rest"){Check(Mean(after.Knee)<=(kind==NativeHordeKinds.Hulk?48:30)&&Mean(after.Knee)<Mean(before.Knee),name+": stands on its legs, knees soft (mean "+Mean(after.Knee).ToString("0")+" deg, was "+Mean(before.Knee).ToString("0")+")");worstIdleKnee=Math.Max(worstIdleKnee,Mean(after.Knee));
                        Check(Mean(after.Back)<=.12f,name+": the hips at most 12 cm behind the feet ("+Mean(after.Back).ToString("0.000")+")");worstBack=Math.Max(worstBack,Mean(after.Back));}
                    Check(P95(after.Foot)<=.01f,name+": the planted ankles stay on their marks (p95 "+(P95(after.Foot)*100).ToString("0.0")+" cm)");worstFoot=Math.Max(worstFoot,P95(after.Foot));
                    Check(P95(after.Rise)<=P95(before.Rise)*1.25f+.15f,name+": the hips move no faster than the gait's own (p95 "+P95(after.Rise).ToString("0.00")+" m/s, was "+P95(before.Rise).ToString("0.00")+")");
                    Check(after.Head.Max()<=Math.Max(after.GameHead.Max(),NativeHordeGait.Reach(kind))+.05f,name+": the head stays over the hit capsule ("+after.Head.Max().ToString("0.00")+")");
                    Check(Mean(after.Spread)<=Mean(before.Spread)+.5f,name+": the arms hang no wider than before ("+Mean(after.Spread).ToString("0.0")+" deg)");
                }
            }
            Pilot(rig);Crawl(rig,pelvis);Dying(rig,pelvis);Repeat(rig);Wiring(root);
            Console.WriteLine("PASS zombie posture: "+count+" checks (five kinds at rest, walk and run on the measured skeleton: lumbar share at most "+worstShare.ToString("0.00")+", resting knees at most "+worstIdleKnee.ToString("0")+" deg, hips at most "+worstBack.ToString("0.00")+" m behind the feet, planted ankles within "+(worstFoot*100).ToString("0.0")+" cm; the trunk's top and the gaze kept; pilots untouched)");
        }
        private static void Measure(NativeVisualRig.Rig r,NativeVisualRig.State s,CapturedRig.Pose game,Stats o,ref float lastY)
        {
            int pelvis=r.Of[CapturedRig.Pelvis],low=r.Of[CapturedRig.Spine],top=r.Of[CapturedRig.Chest],head=r.Of[CapturedRig.Head];
            o.Lumbar.Add(Angle(Quaternion.Inverse(s.Turn[pelvis])*s.Turn[low]));o.Upper.Add(Angle(Quaternion.Inverse(s.Turn[low])*s.Turn[top]));
            Vector3 mid=Vector3.Zero;
            for(int side=0;side<2;side++)
            {
                int hip=side==0?CapturedRig.LHip:CapturedRig.RHip,vh=r.Of[hip],vk=r.Of[hip+1],va=r.Of[hip+2];
                Vector3 h=s.Position[vh],k=s.Position[vk],a=s.Position[va];o.Knee.Add(Between(k-h,a-k));mid+=a*.5f;
                float w=side==0?s.LeftFootWeight:s.RightFootWeight;
                if(w>=.999f){var g=game.WorldPosition[hip+2];var mark=new Vector3(g.X,r.AlignedPosition[va].Y+g.Y-NativeVisualRig.AnkleRest(hip),g.Z);o.Foot.Add((a-mark).Length());}
                int arm=r.Of[side==0?CapturedRig.LArm:CapturedRig.RArm],elbow=r.Of[side==0?CapturedRig.LElbow:CapturedRig.RElbow];
                var u=s.Position[elbow]-s.Position[arm];float outward=side==0?u.X:-u.X;o.Spread.Add((float)Math.Atan2(outward,Math.Max(1e-4f,-u.Y))*Deg);
            }
            o.Back.Add(mid.Z-s.Position[pelvis].Z);
            o.Head.Add(new Vector2(s.Position[head].X,s.Position[head].Z).Length());
            float y=s.Position[pelvis].Y;if(!float.IsNaN(lastY))o.Rise.Add(Math.Abs(y-lastY)/Dt);lastY=y;
        }
        // A pilot's binding never takes the zombie stance (the state's flags are off): bit for bit the old Positions path.
        private static void Pilot(NativeVisualRig.Rig rig)
        {
            var s=new NativeVisualRig.State(rig);Check(!s.Across&&!s.Level&&s.Stand==0&&s.Raise==0,"a new binding's stance is off");
            var p=new CapturedRig.Pose();p.Position[CapturedRig.Body]=new Vector3(.03f,-.06f,-.04f);p.World();
            NativeVisualRig.Pose(s,p,default(NativeVisualRig.Grasp),true,false);int pelvis=rig.Of[CapturedRig.Pelvis];
            var expect=rig.PelvisRest+(p.WorldPosition[CapturedRig.Pelvis]-rig.GamePelvisRest)*rig.LegRatio;
            Check((s.Position[pelvis]-expect).Length()<1e-5f&&s.Raise==0,"a pilot's pelvis is the game's offset times the legs' ratio, as before");
        }
        // A legless crawl: the model's pelvis at the gait pelvis's own height (the legs collapse; the trunk lies where the gait's does).
        private static void Crawl(NativeVisualRig.Rig rig,int pelvis)
        {
            var s=new NativeVisualRig.State(rig);var p=new CapturedRig.Pose();p.Position[CapturedRig.Body]=new Vector3(0,-.62f,.1f);p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(1.2f);p.World();
            NativeZombiePosture.Stance(s,NativeHordeKinds.Shambler,true,0);NativeVisualRig.Pose(s,p,default(NativeVisualRig.Grasp),false,false);
            Check(Math.Abs(s.Position[pelvis].Y-p.WorldPosition[CapturedRig.Pelvis].Y)<1e-4f&&s.Raise==0,"a crawl's pelvis at the gait's height ("+s.Position[pelvis].Y.ToString("0.000")+" against "+p.WorldPosition[CapturedRig.Pelvis].Y.ToString("0.000")+")");
            Check(Math.Abs(s.Position[pelvis].Z-(rig.PelvisRest.Z+p.WorldPosition[CapturedRig.Pelvis].Z-rig.GamePelvisRest.Z))<1e-4f,"and as far along as the gait's");
        }
        // A death (nothing planted): the rise eases out a step a pose and is gone by Stand/RaiseStep poses.
        private static void Dying(NativeVisualRig.Rig rig,int pelvis)
        {
            // (A gait crouched as at rest, hips .06 under standing, its feet at the rig's floor.)
            var s=new NativeVisualRig.State(rig);var p=new CapturedRig.Pose();p.Position[CapturedRig.Body]=new Vector3(0,-.06f,0);p.World();NativeZombiePosture.Stance(s,NativeHordeKinds.Shambler,false,0);
            NativeVisualRig.Pose(s,p,default(NativeVisualRig.Grasp),true,false);float stood=s.Raise;Check(stood>0&&stood<=s.Stand+1e-6f,"standing, the hips rise (by "+stood.ToString("0.000")+" m of "+s.Stand.ToString("0.000")+")");
            int poses=0;while(s.Raise>0&&poses<100){float was=s.Raise;NativeVisualRig.Pose(s,p,default(NativeVisualRig.Grasp),false,false);Check(was-s.Raise<=NativeVisualRig.RaiseStep+1e-6f,"the rise eases out");poses++;}
            Check(s.Raise==0&&poses<=(int)Math.Ceiling(stood/NativeVisualRig.RaiseStep)+1,"and is gone in "+poses+" poses");
        }
        // The same input, the same pose (no history but the rise's and the arms').
        private static void Repeat(NativeVisualRig.Rig rig)
        {
            Vector3[] Run(){var d=new Drive(NativeHordeKinds.Shambler,777,0);var s=new NativeVisualRig.State(rig);var fit=new CapturedRig.Pose();for(int i=0;i<120;i++){d.Step(i*Dt,i<60?0:1.5f);d.P.World();NativeZombiePosture.Fit(d.P,fit);NativeZombiePosture.Stance(s,NativeHordeKinds.Shambler,false,d.G.Sunk);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);}return (Vector3[])s.Position.Clone();}
            var a=Run();var b=Run();Check(a.Zip(b,(x,y)=>(x-y).Length()).Max()==0,"the same gait poses the same model, bit for bit");
        }
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string zm=R("Runtime/NativeZombieModelPresentation.cs"),rig=R("Core/NativeVisualRig.cs"),fit=R("Core/NativeZombiePosture.cs");
            Check(zm.Contains("minionPose.World();var pose=zombieFitPose;NativeZombiePosture.Fit(minionPose,pose);NativeZombiePosture.Stance(z.Body.Binding.State,z.Kind,z.Crawl,z.Sunk);")
                &&zm.Contains("z.Body.Binding.Apply(pose,default(NativeVisualRig.Grasp),planted,airborne);"),"the model binds the fitted copy (the rig, its voxel tiers and gore keep the gait's pose)");
            Check(R("Runtime/NativeHordeKindPresentation.cs").Contains("ZombieModelPose(rig,m.Id,kind,s.Tier,planted,airborne,a.Crawl,s.Gait.Sunk,a.Lost,"),"the add's crawl reaches the model");
            Check(rig.Contains("if(s.Across||s.Level||s.Raise!=0) lift=Stance(s,pose,lift);")&&rig.Contains("if(s.Stand>0||s.Raise!=0) Rise(s,pose,plantFeet);"),"the stance runs only for a state that asks for it");
            foreach(string w in new[]{"NativeArena","Authority","Pilot.","Collider","Strike(","Recover("})Check(!fit.Contains(w),"the fit reads no rule or authority: "+w);
        }
    }
}
#endif
