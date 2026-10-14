using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// Two sprint arm fixes (owner 2026-10-14), sampled over steady run and sprint cycles at 60 Hz in chest space:
///  - daggers, "while running/sprinting, the back arm wiggles a bit": the back (left) arm's blade travel and angular spread
///    in the run; in the sprint each blade holds a steady lie (Core/NativeSprintMotion DaggerRight/DaggerLeft) with its
///    tip above the floor guard's plane, so the guard never flicks a wrist (the fists still pump as before);
///  - magic, "while sprinting, the hand keeps rotating": the casting hand's orientation stays bounded through a sprint and
///    does not drift over 20 s (Core/NativeRangedLocomotion holds its cycle-mean frame at the sprint weight);
///  - both: no pop through the run-to-sprint blend. Every measure is printed before any bound is checked.
/// </summary>
internal static class SprintArmChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI,SprintSpeed=9.18f,RunSpeed=6.8f;
    private static readonly List<string> failures=new List<string>();
    private static void Check(bool ok,string what){if(!ok)failures.Add(what);}
    public static void Run(string root)
    {
        failures.Clear();var log=new List<string>();Daggers(log);Magic(log);
        foreach(var line in log)Console.WriteLine((failures.Count==0?"PASS":"MEASURE")+" sprint arms: "+line);
        if(failures.Count>0)throw new InvalidOperationException("Sprint arms: "+string.Join("; ",failures));
    }
    // One pilot travelling along +z (melee or magic route, the arena layers on as on the arena rigs).
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor A;private readonly MeleeView v=new MeleeView();
        private readonly MeleeMotion melee;private readonly MagicMotion magic;public readonly NativeSprintMotion Layer=new NativeSprintMotion();
        private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];private float last=-100;private Vector3 root=new Vector3(0,.865f,0);
        public Pilot(string spec)
        {
            int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=7,SourceSpec=spec,Weapon=WeaponId.Sword,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,VisualSet=spec=="daggers"?1:0};
            if(mv>0||spec=="leet")magic=new MagicMotion{SprintLayer=Layer,Locomotion=new NativeRangedLocomotion(),JumpLayer=new NativeJumpMotion()};
            else{melee=new MeleeMotion{Impacts=new MeleeImpacts(),Locomotion=new NativeLocomotionCarry(),SprintLayer=Layer,JumpLayer=new NativeJumpMotion()};v.Id="7";v.Spec=spec;}
        }
        public void Frame(float t,float speed,bool sprinting)
        {
            float dt=last<-90?Dt:t-last;last=t;var vel=new Vector3(0,0,speed);root+=vel*dt;
            A.Velocity=new V3(0,0,speed);A.Sprinting=sprinting;A.Position=new V3(root.X,root.Y-.865f,root.Z);
            if(magic!=null){magic.Rig.GroupPosition=root;magic.Rig.GroupYaw=0;magic.Pose(A,t,Pose,channels,0,false,juggle,out _);magic.Finish(Pose,t);return;}
            v.Vel=vel;v.Grounded=true;v.Sprinting=sprinting;v.Hp=A.Hp;melee.Rig.GroupPosition=root;melee.Rig.GroupYaw=melee.PresentedYaw(v,t,v.Yaw);
            melee.Sample(v,t,dt,r=>{});melee.Rig.WritePose(Pose);Pose.World();
        }
        public float Stride=>(melee?.Rig??magic.Rig).Stride;
        public Quaternion HandInChest(int hand)=>Quaternion.Normalize(Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest])*Pose.WorldRotation[hand]);
        public Vector3 InChest(Vector3 world)=>Vector3.Transform(world-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest]));
        public Vector3 Tip(int hand,Vector3 tip)=>Pose.WorldPosition[hand]+Vector3.Transform(tip,Pose.WorldRotation[hand]);
    }
    private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))))*Deg;
    private static Quaternion Mean(List<Quaternion> qs)
    {
        Vector4 s=Vector4.Zero;var r=qs[0];foreach(var q in qs){var x=Quaternion.Dot(q,r)<0?-q:q;s+=new Vector4(x.X,x.Y,x.Z,x.W);}
        s=Vector4.Normalize(s);return new Quaternion(s.X,s.Y,s.Z,s.W);
    }
    // A steady stretch: `strides` whole strides after `settle` s; returns the frames' hand frames, blade tips (chest space),
    // tip heights (group space) and the stride count.
    private sealed class Cycle{public readonly List<Quaternion>[] Hand={new List<Quaternion>(),new List<Quaternion>()};public readonly List<Vector3>[] Tip={new List<Vector3>(),new List<Vector3>()};public float[] Low={float.MaxValue,float.MaxValue};public float Strides;}
    private static readonly int[] Hands={CapturedRig.LHand,CapturedRig.RHand};
    private static Cycle Sample(string spec,float speed,bool sprint,float strides,float settle=1.5f)
    {
        var p=new Pilot(spec);var c=new Cycle();var tip=MeleeSet.For("daggers").Tip;float s0=float.NaN;
        for(int f=0;f<2000;f++)
        {
            float t=1+f*Dt;p.Frame(t,speed,sprint);if(t<1+settle)continue;if(float.IsNaN(s0))s0=p.Stride;
            if(p.Stride-s0>=strides*MeleeMath.TAU)break;
            for(int k=0;k<2;k++){c.Hand[k].Add(p.HandInChest(Hands[k]));var w=p.Tip(Hands[k],tip);c.Tip[k].Add(p.InChest(w));c.Low[k]=Math.Min(c.Low[k],w.Y);}
            c.Strides=(p.Stride-s0)/MeleeMath.TAU;
        }
        return c;
    }
    private static float Path(List<Vector3> pts,float strides){float d=0;for(int i=1;i<pts.Count;i++)d+=Vector3.Distance(pts[i],pts[i-1]);return d/Math.Max(1e-3f,strides);}
    private static float Spread(List<Quaternion> qs){var m=Mean(qs);float s=0;foreach(var q in qs){float a=Angle(q,m);s+=a*a;}return (float)Math.Sqrt(s/qs.Count);}
    private static float Span(List<Quaternion> qs){var m=Mean(qs);float s=0;foreach(var q in qs)s=Math.Max(s,Angle(q,m));return 2*s;}
    private static float MaxStep(List<Quaternion> qs){float s=0;for(int i=1;i<qs.Count;i++)s=Math.Max(s,Angle(qs[i],qs[i-1]));return s;}
    // The wrist's largest change of angular velocity between frames (deg/s^2): a flick.
    private static float Flick(List<Quaternion> qs)
    {
        float worst=0;Vector3 prev=Vector3.Zero;bool has=false;
        for(int i=1;i<qs.Count;i++)
        {
            var d=Quaternion.Normalize(Quaternion.Inverse(qs[i-1])*qs[i]);if(d.W<0)d=-d;float s=(float)Math.Sqrt(Math.Max(0,1-d.W*d.W)),a=2*(float)Math.Acos(Math.Min(1,d.W));
            Vector3 w=s<1e-7f?Vector3.Zero:new Vector3(d.X,d.Y,d.Z)/s*(a*Deg/Dt);if(has)worst=Math.Max(worst,Vector3.Distance(w,prev)/Dt);prev=w;has=true;
        }
        return worst;
    }
    private static void Daggers(List<string> log)
    {
        // The run: the back (left) hand holds the second blade and keeps the weapon hand's share of the pack's swing.
        var run=Sample("daggers",RunSpeed,false,6);
        float backPath=Path(run.Tip[0],run.Strides)*100,frontPath=Path(run.Tip[1],run.Strides)*100,backSpread=Spread(run.Hand[0]),frontSpread=Spread(run.Hand[1]);
        log.Add("daggers run "+RunSpeed+" m/s, "+run.Strides.ToString("0.0")+" strides: back (left) blade tip travel "+backPath.ToString("0.0")+" cm a stride, its lie spread "+backSpread.ToString("0.00")+" deg rms (front blade "+frontPath.ToString("0.0")+" cm, "+frontSpread.ToString("0.00")+" deg)");
        Check(backPath<=40&&backPath<=frontPath,"daggers run: the back blade travels at most 40 cm a stride and no more than the front blade ("+backPath.ToString("0.0")+" vs "+frontPath.ToString("0.0")+")");
        Check(backSpread<=3,"daggers run: the back blade's lie spreads at most 3 deg rms ("+backSpread.ToString("0.00")+")");
        // The sprint: both fists pump at steady blade lies, both tips above the floor guard's plane (the guard never engages).
        var sprint=Sample("daggers",SprintSpeed,true,6);
        float clear=MeleeClearance.FloorClear;
        for(int k=0;k<2;k++)
        {
            string side=k==0?"back (left)":"front (right)";float span=Span(sprint.Hand[k]),step=MaxStep(sprint.Hand[k]),flick=Flick(sprint.Hand[k]),margin=(sprint.Low[k]-clear)*100,pump=0;
            float zMin=float.MaxValue,zMax=float.MinValue;foreach(var tp in sprint.Tip[k]){zMin=Math.Min(zMin,tp.Z);zMax=Math.Max(zMax,tp.Z);}pump=(zMax-zMin)*100;
            log.Add("daggers sprint "+SprintSpeed+" m/s, "+side+" fist: blade lie span "+span.ToString("0.00")+" deg, largest frame step "+step.ToString("0.00")+" deg, wrist flick "+flick.ToString("0")+" deg/s^2, tip "+margin.ToString("0.0")+" cm over the floor guard's plane at its lowest, tip pump "+pump.ToString("0")+" cm");
            Check(span<=1&&step<=.5f,"daggers sprint: the "+side+" blade holds its lie ("+span.ToString("0.00")+" deg, step "+step.ToString("0.00")+")");
            Check(margin>=2,"daggers sprint: the "+side+" tip stays at least 2 cm over the floor guard's plane ("+margin.ToString("0.0")+" cm)");
            Check(flick<=2000,"daggers sprint: no wrist flick on the "+side+" fist ("+flick.ToString("0")+" deg/s^2)");
            Check(pump>=30,"daggers sprint: the "+side+" fist still pumps its blade ("+pump.ToString("0")+" cm)");
        }
        // The blend: run, sprint from 1.5 s, run again from 2.5 s. The fists turn from the run carry's lie onto the pump's within
        // the sprint's .15 s blend (15.1 deg a frame at most before this change); the steady lie adds no step to it.
        Blend("daggers",log,new[]{0,1},15.5f);
    }
    private static readonly string[] Schools={"fire","water","lightning","darkness","leet"};
    private static void Magic(List<string> log)
    {
        float worstSpan=0,worstDrift=0,worstPath=0;string each="";
        foreach(var spec in Schools)
        {
            // A 20 s sprint: the casting (right) hand's frame in chest space, its span over the steady sprint, its turn a stride
            // and the drift between the first and last two seconds (no accumulation).
            var c=Sample(spec,SprintSpeed,true,72);int n=c.Hand[1].Count,w=(int)(2/Dt);
            float span=Span(c.Hand[1]),path=0;for(int i=1;i<n;i++)path+=Angle(c.Hand[1][i],c.Hand[1][i-1]);path/=Math.Max(1e-3f,c.Strides);
            float drift=Angle(Mean(c.Hand[1].GetRange(0,w)),Mean(c.Hand[1].GetRange(n-w,w)));
            each+=(each.Length>0?"; ":"")+spec+" "+span.ToString("0.0")+" deg, "+path.ToString("0.0")+" deg a stride, drift "+drift.ToString("0.00");
            worstSpan=Math.Max(worstSpan,span);worstDrift=Math.Max(worstDrift,drift);worstPath=Math.Max(worstPath,path);
            Check(span<=3&&path<=3,spec+" sprint: the casting hand holds its frame (span "+span.ToString("0.0")+" deg, "+path.ToString("0.0")+" deg a stride)");
            Check(drift<=1,spec+" sprint: the casting hand does not drift over "+c.Strides.ToString("0")+" strides ("+drift.ToString("0.00")+" deg)");
        }
        log.Add("magic sprint "+SprintSpeed+" m/s for about 20 s, the casting hand's frame in chest space: "+each);
        Blend("fire",log,new[]{1},0);Blend("darkness",log,new[]{1},0);
    }
    // Run, sprint at 1.5 s, run at 2.5 s: the hands' frame steps through the two .15 s blends against the steady run's (or `cap`).
    private static void Blend(string spec,List<string> log,int[] hands,float cap)
    {
        var p=new Pilot(spec);var prev=new Quaternion[2];float blend=0,steady=0;
        for(int f=0;f<240;f++)
        {
            float t=1+f*Dt;bool on=t>=1.5f&&t<2.5f;p.Frame(t,on?SprintSpeed:RunSpeed,on);
            foreach(int k in hands)
            {
                var q=p.HandInChest(Hands[k]);if(f>0){float a=Angle(q,prev[k]);if(t>=1.5f&&t<1.7f||t>=2.5f&&t<2.7f)blend=Math.Max(blend,a);else if(t>1.2f&&t<1.5f||t>2.8f)steady=Math.Max(steady,a);}prev[k]=q;
            }
        }
        log.Add(spec+" run-sprint-run: the hand frame steps at most "+blend.ToString("0.0")+" deg a frame through the blends (steady run "+steady.ToString("0.0")+")");
        Check(blend<=Math.Max(steady*1.25f,cap),spec+": no pop through the blend ("+blend.ToString("0.0")+" deg a frame, steady run "+steady.ToString("0.0")+")");
    }
}
