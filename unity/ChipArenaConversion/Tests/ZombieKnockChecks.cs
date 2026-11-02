using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Zombies under fire on the model's legs (owner 2026-11-02: "we still needa fix zombie movement animations", a video of the
    // machine gun on a crowd in an Expedition run). The authority knocks a reeling add back and lets it run on several times a
    // second; the gait read only its speed, ran its forward cycle and planted each foot ahead along the facing while the root went
    // back, so the hips left the feet half a metre or more and sank into a lunge or a deep squat to keep them. Now the gait reads
    // the net motion while the knocks last (its jolts skid the planted feet), and steps the way the body travels when a shove
    // carries it aside or back (Core NativeHordeGait.KnockMotion, TravelOff). A corpse of an add that died up on another add's back
    // falls to the world's floor under it instead of hanging there (Core NativeMinionFx.CorpseFloor). Presentation only.
    internal static class ZombieKnockChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie knock: "+what);count++;}
        private const float Dt=1/60f,Deg=57.29578f;
        private static float Between(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)))))*Deg;
        private sealed class Result{public readonly List<float> Pelvis=new List<float>(),Trail=new List<float>(),Knee=new List<float>();}
        private static float P(List<float> a,double q){var o=a.OrderBy(x=>x).ToArray();return o[(int)(q*(o.Length-1))];}

        // The gait as the runtime runs an add drawn on the model (stride planned for its legs, fitted, in the zombie stance), the
        // root moving at vel(t), the hits (a pistol's: each staggers and the authority knocks the root back) fed to its reactions.
        private static Result Drive(NativeVisualRig.Rig rig,int kind,Func<float,Vector3> vel,float[] hits)
        {
            var g=new NativeHordeGait();g.Legs=rig.LegRatio;var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);
            var react=new NativeHordeReact();react.Bind(kind);int id=40000+kind*101;
            var f=new HordeMotionInput{Kind=kind,Look=0,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};
            var at=new Vector3(0,.85f,0);int next=0,pelvis=rig.Of[CapturedRig.Pelvis];var r=new Result();
            for(int i=0;i*Dt<6;i++)
            {
                float t=i*Dt;while(next<hits.Length&&hits[next]<=t)react.Add(hits[next++],new Vector2(0,-1),StaggerWeight.Flinch,23,false,false,true,.45f);
                var v=vel(t);at+=v*Dt;f.Time=t;f.Root=at;f.Velocity=v;f.Yaw=0;f.YawRate=0;f.Ground=0;g.Pose(f,p,react);p.World();
                NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                if(i<60)continue;Vector3 mid=Vector3.Zero;float knee=0,low=float.MaxValue;
                for(int side=0;side<2;side++){int hip=side==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],k=s.Position[rig.Of[hip+1]],a=s.Position[rig.Of[hip+2]];knee=Math.Max(knee,Between(k-h,a-k));mid+=a*.5f;low=Math.Min(low,a.Y);}
                r.Pelvis.Add(s.Position[pelvis].Y-low);r.Trail.Add(Math.Abs(mid.Z-s.Position[pelvis].Z));r.Knee.Add(knee);
            }
            return r;
        }
        public static void Run(string root)
        {
            count=0;var rig=ModelPoseChecks.Measured();var lines=new List<string>();
            foreach(int kind in new[]{NativeHordeKinds.Runner,NativeHordeKinds.Shambler})
            {
                float run=kind==NativeHordeKinds.Runner?3.4f:1.5f;string name=NativeHordeKinds.Ids[kind];
                var steady=Drive(rig,kind,t=>new Vector3(0,0,run),new float[0]);
                // A pistol on it from 2 s: each hit staggers it, knocked back at 2.2 m/s for .2 s, then an instant restart (the
                // authority's, docs/ZOMBIE-LOCOMOTION.md "Twitching under fire").
                var hits=new List<float>();for(float t=2;t<4.2f;t+=.42f)hits.Add(t);
                var shot=Drive(rig,kind,t=>{if(t>=2&&t<4.2f){float a=(t-2)%.42f;return a<.2f?new Vector3(0,0,-2.2f):new Vector3(0,0,run);}return new Vector3(0,0,run);},hits.ToArray());
                Check(P(shot.Pelvis,.05)>=P(steady.Pelvis,.05)-.08f,name+" under a pistol's staggers keeps its hips up: pelvis p5 "+P(shot.Pelvis,.05).ToString("0.00")+" against a steady run's "+P(steady.Pelvis,.05).ToString("0.00")+" (it sank into a squat)");
                Check(shot.Trail.Max()<=.45f,name+" under a pistol's staggers keeps its feet under it: they trail the hips at most "+shot.Trail.Max().ToString("0.00")+" m (they were left .6 m behind: a lunge)");
                lines.Add($"{name}: steady pelvis p5 {P(steady.Pelvis,.05):0.00}, trail max {steady.Trail.Max():0.00}; under staggers {P(shot.Pelvis,.05):0.00}, {shot.Trail.Max():0.00}");
            }
            // The pieces.
            Check(NativeHordeGait.TravelOff(new Vector2(0,1),new Vector2(0,1))==0&&NativeHordeGait.TravelOff(new Vector2(.6f,.8f),new Vector2(0,1))==0,"running on along the facing (or within about 45 degrees of it) steps along the facing as ever");
            Check(NativeHordeGait.TravelOff(new Vector2(1,0),new Vector2(0,1))==1&&NativeHordeGait.TravelOff(new Vector2(0,-1),new Vector2(0,1))==1,"carried aside or back, it steps the way it goes");
            var v=new Vector2(0,3.4f);Check(NativeHordeGait.KnockMotion(v,new Vector2(0,.7f),0,out float sp)==v&&sp==3.4f,"unhit, the gait reads the root's own motion");
            Check(NativeHordeGait.KnockMotion(new Vector2(0,-2.2f),new Vector2(0,.7f),1,out sp)==new Vector2(0,.7f)&&Math.Abs(sp-.7f)<1e-5f,"reeling, it reads the net motion");
            Check(NativeMinionFx.CorpseFloor(.8f,float.NaN,1)==.8f&&NativeMinionFx.CorpseFloor(.8f,.75f,1)==.8f,"a corpse with no floor under it, or a kerb's drop, lies where it fell");
            float last=float.MaxValue;bool down=true;for(float t=0;t<1;t+=.02f){float y=NativeMinionFx.CorpseFloor(.8f,0,t);down&=y<=last&&y>=0;last=y;}
            Check(down&&NativeMinionFx.CorpseFloor(.8f,0,0)==.8f&&NativeMinionFx.CorpseFloor(.8f,0,.41f)==0,"a corpse up on another add's back falls to the floor under it in about .4 s, never through it");
            string kinds=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"));
            Check(kinds.Contains("Ground=HordeGround(arena,deaths[i].position)")&&kinds.Contains("floorY=NativeMinionFx.CorpseFloor(death.position.y-NativeHordeKinds.RootHeight(d.Kind,false),d.Ground,fall)"),"the horde's deaths cast the floor under them once and their bodies fall to it");
            Console.WriteLine("PASS zombie knock ("+count+" checks: "+string.Join("; ",lines)+"; corpses fall to the floor under them)");
        }
    }
}
