#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The zombies' runs, sprints and walks on the player's model (owner 2026-10-30: "Zombie run looks a little weird at the moment,
    // sometimes their legs are completely out of sync and move weird directions. Evaluate the runs/sprints and all movements of
    // zombies for new model."; Core/NativeZombieMotion's warp and the takes' own feet, NativeVisualRig's State.Follow). First the
    // takes as measured: each loop's warp keeps its cycle (forward only, never slower than a third of its pace, its inverse its
    // inverse) and times its right strike to the gait's half cycle; the right foot steps half a cycle after the left on every
    // take; the sprints stand on the ground for their own stance, not the fifth of a cycle the marks gave them; each swing arc
    // leaves the floor and comes back to it. Then each modelled kind's gait at its walk and its run on a few take pairs, bound to
    // the first outfit's measured skeleton as the runtime binds it (fitted, with the zombie stance): the model's ankles on the
    // gait's (planted and swinging), no foot faster than a swing carries it, the knees forward of the leg, the feet alternating,
    // and the take's own feet in step with the gait's. A synthetic skeleton, not a render: how it reads on screen is the owner's
    // PC's to judge. The numbers are printed for docs/ZOMBIE-MODELS.md.
    internal static class ZombieGaitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie gait: "+what);count++;}
        private const float Dt=1/60f,Deg=57.29578f;
        private static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};
        private static float P95(List<float> a){if(a.Count==0)return 0;var o=a.OrderBy(x=>x).ToArray();return o[(int)(.95*(o.Length-1))];}
        private static float Corr(List<float> a,List<float> b)
        {
            int n=Math.Min(a.Count,b.Count);if(n<3)return 0;float ma=a.Take(n).Average(),mb=b.Take(n).Average();double ab=0,aa=0,bb=0;
            for(int i=0;i<n;i++){ab+=(a[i]-ma)*(b[i]-mb);aa+=(a[i]-ma)*(a[i]-ma);bb+=(b[i]-mb)*(b[i]-mb);}
            return (float)(ab/Math.Sqrt(Math.Max(1e-12,aa*bb)));
        }
        // The knee's turn aside about the leg's own line (deg): its bend off the hip-ankle line against forward made square to the
        // line (-1: a leg too straight to say).
        private static float Side(Vector3 hip,Vector3 knee,Vector3 ankle,float least)
        {
            var d=Vector3.Normalize(ankle-hip);var b=knee-hip;b-=d*Vector3.Dot(b,d);if(b.Length()<least)return -1;
            var f=Vector3.UnitZ-d*Vector3.Dot(Vector3.UnitZ,d);if(f.Length()<.2f)return -1;f=Vector3.Normalize(f);var s=Vector3.Cross(d,f);
            return Math.Abs((float)Math.Atan2(Vector3.Dot(b,s),Vector3.Dot(b,f)))*Deg;
        }

        public static void Run(string root)
        {
            NativeZombieMotion.Warm();
            Takes(out string takes);
            Model(out string model);
            Rules();
            Console.WriteLine("PASS zombie gait: "+count+" checks ("+takes+"; "+model+"; presentation only)");
        }

        // ---- the takes: the warp, the right foot's offset, the stances, the swing arcs ----
        private static void Takes(out string summary)
        {
            var loops=new SortedSet<int>();
            for(int k=0;k<NativeHordeKinds.All;k++){if(!NativeZombieMotion.Captured(k))continue;foreach(var set in new[]{NativeZombieMotion.Walks(k),NativeZombieMotion.Runs(k),NativeZombieMotion.Wanders(k)})foreach(int c in set)loops.Add(c);}
            Check(loops.Count>=8,"the kinds walk and run on the pack's loop takes ("+loops.Count+")");
            float slowest=1,moved=0;int sprints=0;
            foreach(int c in loops)
            {
                string key=NativeZombieClips.Key(c);
                Check(NativeZombieClips.Clip(c).Loop,key+": a gait take is a loop");
                // The warp: the cycle's start and end where they were, forward only, the inverse its inverse.
                Check(Math.Abs(NativeZombieMotion.Warp(c,0))<1e-5f&&Math.Abs(NativeZombieMotion.Warp(c,1)-1)<1e-4f&&Math.Abs(NativeZombieMotion.Warp(c,3.25f)-3-NativeZombieMotion.Warp(c,.25f))<1e-4f,key+": the warp keeps the cycle (0 at 0, a cycle a cycle)");
                float last=NativeZombieMotion.Warp(c,0);const int n=512;
                for(int i=1;i<=n;i++)
                {
                    float p=i/(float)n,w=NativeZombieMotion.Warp(c,p),rate=(w-last)*n;last=w;slowest=Math.Min(slowest,rate);
                    Check(rate>.3f,key+": the warped take only plays forward, never slower than about a third of its pace ("+rate.ToString("0.00")+" at "+p.ToString("0.00")+")");
                    if(i%32==0)Check(Math.Abs(NativeZombieMotion.Unwarp(c,w)-(p>=1?0:p))<2e-3f,key+": the unwarp undoes the warp at "+p.ToString("0.00"));
                }
                // The right foot's strike at the gait's half cycle, as far as the warp moves it; the right foot offset half a cycle.
                float right=NativeZombieMotion.Strike(c,1),want=.5f+Math.Max(-NativeZombieMotion.WarpMost,Math.Min(NativeZombieMotion.WarpMost,right-.5f));
                Check(Math.Abs(NativeZombieMotion.Warp(c,.5f)-want)<2e-3f,key+": the warp plays the take's right strike ("+right.ToString("0.00")+") at the gait's half cycle");
                Check(NativeZombieMotion.Offset(c,0)==0&&NativeZombieMotion.Offset(c,1)==.5f,key+": the right foot steps half a cycle after the left");
                moved=Math.Max(moved,Math.Abs(right-.5f));
                // The stances: a sprint's its own (most of a third of the cycle on each foot), a walk's the marks' (clamped as before).
                bool sprint=NativeZombieClips.Speed(c)>1;
                for(int foot=0;foot<2;foot++)
                {
                    float d=NativeZombieMotion.Duty(c,foot);
                    if(sprint)Check(d>=.3f&&d<=.42f,key+": a sprint stands on each foot for its own stance, not a fifth of the cycle ("+d.ToString("0.00")+")");
                    else{float m=foot==0?NativeZombieClips.LeftLift(c):NativeZombieClips.RightLift(c)-NativeZombieClips.RightLand(c);m-=(float)Math.Floor(m);Check(Math.Abs(d-Math.Max(.5f,Math.Min(.7f,m)))<1e-5f,key+": a walk's stance stays the marks' ("+d.ToString("0.00")+")");}
                    // The swing arc: off the floor at its start, back on it at its end, a real lift between.
                    float top=0,lastLift=0,jump=0;for(int i=0;i<=64;i++){float h=NativeZombieMotion.SwingLift(c,foot,i/64f);top=Math.Max(top,h);if(i>0)jump=Math.Max(jump,Math.Abs(h-lastLift));lastLift=h;}
                    Check(NativeZombieMotion.SwingLift(c,foot,0)==0&&NativeZombieMotion.SwingLift(c,foot,1)==0,key+": a swing arc leaves the floor and lands on it");
                    // (walk-4's limping right foot drags, about 2 cm up: the take's own.)
                    Check(top>=.015f&&top<.4f,key+": a swing lifts the foot ("+(top*100).ToString("0.0")+" cm)");
                    Check(jump<.025f,key+": a swing arc rises and falls smoothly ("+(jump*100).ToString("0.0")+" cm a 64th)");
                }
                if(sprint)sprints++;
            }
            Check(sprints>=2,"the sets carry sprint takes ("+sprints+")");
            // The exporter's right-foot marks miss on these takes (the measure reads the feet: the limp and the mis-marked chase).
            float limp=NativeZombieMotion.Strike(NativeZombieClips.Need("walk-4"),1),chase=NativeZombieMotion.Strike(NativeZombieClips.Need("chase-1-full"),1);
            Check(limp>.6f&&limp<.78f,"walk-4's right foot strikes late in the cycle, a limp ("+limp.ToString("0.00")+")");
            Check(chase>.4f&&chase<.58f&&Math.Abs(NativeZombieClips.RightLand(NativeZombieClips.Need("chase-1-full"))-chase)>.2f,"chase-1-full's right foot strikes about half a cycle in, not where its mark says ("+chase.ToString("0.00")+")");
            summary=loops.Count+" loop takes: warped forward only (slowest "+slowest.ToString("0.00")+" of the pace), right strikes up to "+moved.ToString("0.00")+" cycles off the half timed to it, "+sprints+" sprints on their own stance, every swing arc off and back on the floor";
        }

        // ---- the model on the gait ----
        private static void Model(out string summary)
        {
            var rig=ModelPoseChecks.Measured();var fit=new CapturedRig.Pose();
            float worstGap=0,worstPop=0,worstKnee=0,worstGameKnee=0,worstAlt=-1,worstSync=1,syncSum=0;int syncN=0,runs=0;
            foreach(int kind in Kinds)
            {
                var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[NativeHordeKinds.Index(kind)];
                foreach(var (gait,speed) in new[]{("walk",st.walk/sc),("run",st.run/sc)})
                {
                    var seen=new HashSet<(int,int)>();
                    for(int id=40000;id<40400&&seen.Count<4;id++)
                    {
                        uint seed=NativeHordeKinds.Seed(id);int w=NativeZombieMotion.Walk(kind,seed),r=NativeZombieMotion.Run(kind,seed);if(!seen.Add((w,r)))continue;
                        string name=NativeHordeKinds.Ids[kind]+" "+gait+" on "+NativeZombieClips.Key(w)+"/"+NativeZombieClips.Key(r);
                        var g=new NativeHordeGait();var p=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=kind,Id=id,Seed=seed,Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);
                        var s=new NativeVisualRig.State(rig);var row=new float[CapturedClip.Channels];var take=new CapturedRig.Pose();
                        var knee=new List<float>();var gameKnee=new List<float>();var ml=new List<float>();var mr=new List<float>();var tz=new[]{new List<float>(),new List<float>()};var gz=new[]{new List<float>(),new List<float>()};
                        float gap=0,pop=0;var last=new Vector3[2];bool has=false;int pel=rig.Of[CapturedRig.Pelvis];
                        for(int i=0;i<390;i++)
                        {
                            var v=new Vector3(0,0,speed);at+=v*Dt;f.Time=i*Dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                            NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                            if(i<90)continue;
                            int c=g.RunNow>.5f?r:w;NativeZombieClips.SamplePhase(c,NativeZombieMotion.Warp(c,g.Phase),row);NativeZombieClips.Pose(row,take,false);take.World();
                            for(int side=0;side<2;side++)
                            {
                                int hip=side==0?CapturedRig.LHip:CapturedRig.RHip,vh=rig.Of[hip],vk=rig.Of[hip+1],va=rig.Of[hip+2];
                                Vector3 ga=fit.WorldPosition[hip+2],ma=s.Position[va];
                                gap=Math.Max(gap,new Vector2(ma.X-ga.X,ma.Z-ga.Z).Length());
                                if(has)pop=Math.Max(pop,(ma-last[side]).Length()/Dt);last[side]=ma;
                                float k=Side(s.Position[vh],s.Position[vk],ma,.02f);if(k>=0)knee.Add(k);
                                float kg=Side(fit.WorldPosition[hip],fit.WorldPosition[hip+1],ga,.01f);if(kg>=0)gameKnee.Add(kg);
                                (side==0?ml:mr).Add(ma.Z-s.Position[pel].Z);
                                tz[side].Add(take.WorldPosition[hip+2].Z-take.WorldPosition[CapturedRig.Pelvis].Z);gz[side].Add(ga.Z-fit.WorldPosition[CapturedRig.Pelvis].Z);
                            }
                            has=true;
                        }
                        float kn=P95(knee),gk=P95(gameKnee),alt=Corr(ml,mr),sync=(Corr(tz[0],gz[0])+Corr(tz[1],gz[1]))*.5f,popMost=2.2f*speed+3.2f;
                        Check(gap<.01f,name+": the model's ankles go where the gait's do, swinging too ("+(gap*100).ToString("0.0")+" cm)");
                        Check(pop<popMost,name+": no model foot moves faster than a swing carries it ("+pop.ToString("0.0")+" m/s, "+popMost.ToString("0.0")+" at most)");
                        Check(kn<25,name+": the knees bend forward of the leg ("+kn.ToString("0")+" deg aside p95)");
                        // (The gait's own knees within its clamp, NativeHordeGait.KneeSplay, and a little for the knee's ease off last frame.)
                        Check(gk<NativeHordeGait.KneeSplay*Deg+3,name+": the gait's knees turn at most KneeSplay aside ("+gk.ToString("0")+" deg p95)");
                        Check(alt<-.5f,name+": the feet alternate ("+alt.ToString("0.00")+")");
                        worstGap=Math.Max(worstGap,gap);worstPop=Math.Max(worstPop,pop/popMost);worstGameKnee=Math.Max(worstGameKnee,gk);worstKnee=Math.Max(worstKnee,kn);worstAlt=Math.Max(worstAlt,alt);
                        // The take's own feet in step with the gait's at the walk (a run on a walking take stands less of its cycle than
                        // the take does: its swing arcs carry it, NativeZombieMotion.SwingLift).
                        if(gait=="walk"){Check(sync>.7f,name+": the take's feet step with the gait's ("+sync.ToString("0.00")+")");worstSync=Math.Min(worstSync,sync);syncSum+=sync;syncN++;}
                        runs++;
                    }
                }
            }
            Check(syncN>0&&syncSum/syncN>.85f,"the takes' feet step with the gait's at the walk on average ("+(syncSum/Math.Max(1,syncN)).ToString("0.00")+")");
            summary=runs+" walks and runs of five kinds on the measured skeleton: model ankles within "+(worstGap*100).ToString("0.0")+" cm of the gait's, the fastest foot at most "+(worstPop*100).ToString("0")+"% of a swing's 2.2 times the ground speed and 3.2 m/s, knees "+worstKnee.ToString("0")+" deg aside p95 (the gait's "+worstGameKnee.ToString("0")+"), the feet alternating ("+worstAlt.ToString("0.00")+" at worst), the takes in step at the walk ("+worstSync.ToString("0.00")+" worst, "+(syncSum/Math.Max(1,syncN)).ToString("0.00")+" mean)";
        }

        // ---- the flags and the knee's clamp ----
        private static void Rules()
        {
            var rig=ModelPoseChecks.Measured();var s=new NativeVisualRig.State(rig);
            Check(!s.Follow,"a pilot's binding never follows a gait's swinging feet");
            NativeZombiePosture.Stance(s,NativeHordeKinds.Runner,false,0,0,0);Check(s.Follow,"an upright zombie's model follows the gait's feet");
            NativeZombiePosture.Stance(s,NativeHordeKinds.Runner,true,0,0,0);Check(!s.Follow,"a legless crawl's model does not");
            // A pole turned 60 degrees aside comes back to the clamp; one within it stays.
            var hip=new Vector3(0,.5f,0);var ankle=new Vector3(0,0,.1f);var d=Vector3.Normalize(ankle-hip);
            var ahead=Vector3.Normalize(Vector3.UnitZ-d*d.Z);var side=Vector3.Cross(d,ahead);
            var wide=NativeHordeGait.Splayed(ahead*(float)Math.Cos(1.05)+side*(float)Math.Sin(1.05),hip,ankle);
            Check(Math.Abs((float)Math.Atan2(Vector3.Dot(wide,side),Vector3.Dot(wide,ahead))-NativeHordeGait.KneeSplay)<1e-3f,"a knee turned 60 degrees aside comes back to the clamp");
            var near=ahead*(float)Math.Cos(.2)-side*(float)Math.Sin(.2);Check(NativeHordeGait.Splayed(near,hip,ankle)==near,"a knee within the clamp is left as it is");
        }
    }
}
#endif
