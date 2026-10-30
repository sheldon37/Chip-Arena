using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// Two sprint arm fixes (owner 2026-10-14), sampled over steady run and sprint cycles in chest space:
///  - daggers, "while running/sprinting, the back arm wiggles a bit", then "Was the dagger run fixed? It still looks like its
///    doing the same thing with the arm" and "We can probably keep the arms more still (it looks like theyre trying to move
///    the forearms too much with sprinting)":
///    the sprint holds both reverse-grip fists low and close with a light pump (Core/NativeSprintMotion DaggerX..DaggerLift):
///    each elbow's flex and each forearm's swing a stride, the fists' pump and the forearms' turn rate at 240 Hz (no kink);
///    each blade keeps a steady lie (DaggerRight/DaggerLeft) with its tip over the floor guard's plane, so the guard never
///    flicks a wrist;
///    the run: the right (rear) fist's wrist in every run direction, both elbows' flex and both blades' tip travel a stride
///    (Core/NativeLocomotionCarry RearBladeTip: two floor corrections no longer fight over the rear blade; the shoulders keep
///    the arms' share of the clip's shrug), the left blade's lie spread;
///  - magic, "while sprinting, the hand keeps rotating": the casting hand's orientation stays bounded through a sprint and
///    does not drift over 20 s (Core/NativeRangedLocomotion holds its cycle-mean frame at the sprint weight);
///  - both: no pop through the run-to-sprint blend; the daggers' run and sprint allocate nothing per frame.
/// Every measure is printed before any bound is checked.
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
    // One pilot travelling `dir` (rad from its facing, + to its left; the facing stays along +z) at `speed` (melee or magic
    // route, the arena layers on as on the arena rigs).
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
        public void Frame(float t,float speed,bool sprinting,float dir=0)
        {
            float dt=last<-90?Dt:t-last;last=t;var vel=new Vector3((float)Math.Sin(dir),0,(float)Math.Cos(dir))*speed;root+=vel*dt;
            A.Velocity=new V3(vel.X,0,vel.Z);A.Sprinting=sprinting;A.Position=new V3(root.X,root.Y-.865f,root.Z);
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
    private static float Angle(Vector3 a,Vector3 b)=>(float)Math.Atan2(Vector3.Cross(a,b).Length(),Vector3.Dot(a,b))*Deg;
    private static Quaternion Mean(List<Quaternion> qs)
    {
        Vector4 s=Vector4.Zero;var r=qs[0];foreach(var q in qs){var x=Quaternion.Dot(q,r)<0?-q:q;s+=new Vector4(x.X,x.Y,x.Z,x.W);}
        s=Vector4.Normalize(s);return new Quaternion(s.X,s.Y,s.Z,s.W);
    }
    // A steady stretch: `strides` whole strides after `settle` s at `hz`; per hand (0 left, 1 right) the frames' hand frames,
    // blade tips, forearm and upper-arm directions, elbow flex (deg) and fist positions (chest space), the tips' lowest height
    // (group space) and the stride count.
    private sealed class Cycle
    {
        public readonly List<Quaternion>[] Hand={new List<Quaternion>(),new List<Quaternion>()};public readonly List<Vector3>[] Tip={new List<Vector3>(),new List<Vector3>()};
        public readonly List<Vector3>[] Fore={new List<Vector3>(),new List<Vector3>()},Fist={new List<Vector3>(),new List<Vector3>()};public readonly List<float>[] Flex={new List<float>(),new List<float>()};
        public float[] Low={float.MaxValue,float.MaxValue};public float Strides,Dt;
    }
    private static readonly int[] Hands={CapturedRig.LHand,CapturedRig.RHand},Elbows={CapturedRig.LElbow,CapturedRig.RElbow},Shoulders={CapturedRig.LArm,CapturedRig.RArm};
    private static Cycle Sample(string spec,float speed,bool sprint,float strides,float settle=1.5f,int hz=60,float dir=0)
    {
        var p=new Pilot(spec);var c=new Cycle{Dt=1f/hz};var tip=MeleeSet.For("daggers").Tip;float s0=float.NaN;
        for(int f=0;f<40*hz;f++)
        {
            float t=1+f*c.Dt;p.Frame(t,speed,sprint,dir);if(t<1+settle)continue;if(float.IsNaN(s0))s0=p.Stride;
            if(p.Stride-s0>=strides*MeleeMath.TAU)break;
            for(int k=0;k<2;k++)
            {
                c.Hand[k].Add(p.HandInChest(Hands[k]));var w=p.Tip(Hands[k],tip);c.Tip[k].Add(p.InChest(w));c.Low[k]=Math.Min(c.Low[k],w.Y);
                Vector3 s=p.InChest(p.Pose.WorldPosition[Shoulders[k]]),e=p.InChest(p.Pose.WorldPosition[Elbows[k]]),h=p.InChest(p.Pose.WorldPosition[Hands[k]]);
                c.Fore[k].Add(h-e);c.Fist[k].Add(h);c.Flex[k].Add(Angle(e-s,h-e));
            }
            c.Strides=(p.Stride-s0)/MeleeMath.TAU;
        }
        return c;
    }
    private static float Path(List<Vector3> pts,float strides){float d=0;for(int i=1;i<pts.Count;i++)d+=Vector3.Distance(pts[i],pts[i-1]);return d/Math.Max(1e-3f,strides);}
    private static float Spread(List<Quaternion> qs){var m=Mean(qs);float s=0;foreach(var q in qs){float a=Angle(q,m);s+=a*a;}return (float)Math.Sqrt(s/qs.Count);}
    private static float Span(List<Quaternion> qs){var m=Mean(qs);float s=0;foreach(var q in qs)s=Math.Max(s,Angle(q,m));return 2*s;}
    private static float Turn(List<Quaternion> qs,float strides){float d=0;for(int i=1;i<qs.Count;i++)d+=Angle(qs[i],qs[i-1]);return d/Math.Max(1e-3f,strides);}
    private static float MaxStep(List<Quaternion> qs){float s=0;for(int i=1;i<qs.Count;i++)s=Math.Max(s,Angle(qs[i],qs[i-1]));return s;}
    // The span of a set of directions: twice the widest angle from their mean (deg).
    private static float Span(List<Vector3> ds){Vector3 m=Vector3.Zero;foreach(var d in ds)m+=Vector3.Normalize(d);float s=0;foreach(var d in ds)s=Math.Max(s,Angle(d,m));return 2*s;}
    private static float Range(List<float> xs){float lo=float.MaxValue,hi=float.MinValue;foreach(var x in xs){lo=Math.Min(lo,x);hi=Math.Max(hi,x);}return hi-lo;}
    // The wrist's largest change of angular velocity between frames (deg/s^2): a flick.
    private static float Flick(List<Quaternion> qs,float dt=Dt)
    {
        float worst=0;Vector3 prev=Vector3.Zero;bool has=false;
        for(int i=1;i<qs.Count;i++)
        {
            var d=Quaternion.Normalize(Quaternion.Inverse(qs[i-1])*qs[i]);if(d.W<0)d=-d;float s=(float)Math.Sqrt(Math.Max(0,1-d.W*d.W)),a=2*(float)Math.Acos(Math.Min(1,d.W));
            Vector3 w=s<1e-7f?Vector3.Zero:new Vector3(d.X,d.Y,d.Z)/s*(a*Deg/dt);if(has)worst=Math.Max(worst,Vector3.Distance(w,prev)/dt);prev=w;has=true;
        }
        return worst;
    }
    // A forearm's largest change of turn rate between frames (deg/s^2): a kink in its swing (the turn of its direction).
    private static float Kink(List<Vector3> ds,float dt)
    {
        float worst=0;Vector3 prev=Vector3.Zero;bool has=false;
        for(int i=1;i<ds.Count;i++)
        {
            Vector3 a=Vector3.Normalize(ds[i-1]),b=Vector3.Normalize(ds[i]),x=Vector3.Cross(a,b);float l=x.Length();
            Vector3 w=l<1e-9f?Vector3.Zero:x/l*(Angle(a,b)/dt);if(has)worst=Math.Max(worst,Vector3.Distance(w,prev)/dt);prev=w;has=true;
        }
        return worst;
    }
    private static readonly string[] Side={"left (front)","right (rear)"};
    private static void Daggers(List<string> log)
    {
        // The run, straight ahead: in the daggers' stance the right fist is the rear one. Both blades' tip travel, both elbows'
        // flex (the shoulders keep the arms' share of the clip's shrug) and the left lie's spread.
        var run=Sample("daggers",RunSpeed,false,6);
        float leftPath=Path(run.Tip[0],run.Strides)*100,rightPath=Path(run.Tip[1],run.Strides)*100,leftSpread=Spread(run.Hand[0]),leftFlex=Range(run.Flex[0]),rightFlex=Range(run.Flex[1]);
        log.Add("daggers run "+RunSpeed+" m/s, "+run.Strides.ToString("0.0")+" strides: blade tip travel "+leftPath.ToString("0.0")+" cm a stride left (front), "+rightPath.ToString("0.0")+" right (rear); elbow flex "+leftFlex.ToString("0.0")+"/"+rightFlex.ToString("0.0")+" deg a stride; the left lie's spread "+leftSpread.ToString("0.00")+" deg rms");
        Check(leftPath<=40&&rightPath<=40,"daggers run: each blade's tip travels at most 40 cm a stride ("+leftPath.ToString("0.0")+", "+rightPath.ToString("0.0")+")");
        Check(leftFlex<=4&&rightFlex<=4,"daggers run: each elbow flexes at most 4 deg a stride ("+leftFlex.ToString("0.0")+", "+rightFlex.ToString("0.0")+")");
        Check(leftSpread<=3,"daggers run: the left blade's lie spreads at most 3 deg rms ("+leftSpread.ToString("0.00")+")");
        // The rear wrist in every run direction: the clip's own light turn only (the floor corrections once lifted its blade at
        // two points of each stride: 10-16 deg of turn, much of it at twice the stride rate).
        string dirs="";
        foreach(var (name,dir) in new[]{("ahead",0f),("strafe left",(float)Math.PI/2),("strafe right",-(float)Math.PI/2),("backpedal",(float)Math.PI),("diagonal",(float)Math.PI/4),("back diagonal",3*(float)Math.PI/4)})
        {
            var c=Sample("daggers",RunSpeed,false,5,dir:dir);float span=Span(c.Hand[1]),turn=Turn(c.Hand[1],c.Strides);
            dirs+=(dirs.Length>0?"; ":"")+name+" "+span.ToString("0.0")+" deg, "+turn.ToString("0.0")+" a stride";
            Check(span<=7&&turn<=16,"daggers run "+name+": the rear wrist turns at most 7 deg, 16 deg a stride ("+span.ToString("0.0")+", "+turn.ToString("0.0")+")");
        }
        log.Add("daggers run "+RunSpeed+" m/s, the right (rear) wrist's frame in chest space: "+dirs);
        // The sprint: both fists held low and close with a light pump, at steady blade lies, both tips above the floor guard's
        // plane (the guard never engages).
        var sprint=Sample("daggers",SprintSpeed,true,6);var fast=Sample("daggers",SprintSpeed,true,6,hz:240);
        float clear=MeleeClearance.FloorClear;
        for(int k=0;k<2;k++)
        {
            string side=Side[k];float span=Span(sprint.Hand[k]),step=MaxStep(sprint.Hand[k]),flick=Flick(sprint.Hand[k]),margin=(sprint.Low[k]-clear)*100;
            float flex=Range(sprint.Flex[k]),swing=Span(sprint.Fore[k]),kink=Kink(fast.Fore[k],fast.Dt),zMin=float.MaxValue,zMax=float.MinValue;
            foreach(var h in sprint.Fist[k]){zMin=Math.Min(zMin,h.Z);zMax=Math.Max(zMax,h.Z);}float pump=(zMax-zMin)*100;
            log.Add("daggers sprint "+SprintSpeed+" m/s, "+side+" arm: elbow flex "+flex.ToString("0.0")+" deg and forearm swing "+swing.ToString("0.0")+" deg a stride, fist pump "+pump.ToString("0.0")+" cm, forearm turn-rate kink "+kink.ToString("0")+" deg/s^2 at 240 Hz; blade lie span "+span.ToString("0.00")+" deg, largest frame step "+step.ToString("0.00")+" deg, wrist flick "+flick.ToString("0")+" deg/s^2, tip "+margin.ToString("0.0")+" cm over the floor guard's plane at its lowest");
            Check(flex<=8&&swing<=30,"daggers sprint: the "+side+" arm is held (elbow flex "+flex.ToString("0.0")+", forearm swing "+swing.ToString("0.0")+" deg a stride; at most 8 and 30)");
            Check(pump>=8&&pump<=20,"daggers sprint: the "+side+" fist keeps a light pump ("+pump.ToString("0.0")+" cm, 8-20)");
            Check(kink<=10000,"daggers sprint: the "+side+" forearm's turn never kinks at 240 Hz ("+kink.ToString("0")+" deg/s^2)");
            Check(span<=1&&step<=.5f,"daggers sprint: the "+side+" blade holds its lie ("+span.ToString("0.00")+" deg, step "+step.ToString("0.00")+")");
            Check(margin>=2,"daggers sprint: the "+side+" tip stays at least 2 cm over the floor guard's plane ("+margin.ToString("0.0")+" cm)");
            Check(flick<=2000,"daggers sprint: no wrist flick on the "+side+" fist ("+flick.ToString("0")+" deg/s^2)");
        }
        // The blend: run, sprint from 1.5 s, run again from 2.5 s. The fists turn from the run carry's lie onto the held carry's
        // within the sprint's .15 s blend (15.1 deg a frame at most with the 2026-10-12 pump).
        Blend("daggers",log,new[]{0,1},15.5f);
        // No allocation per frame: running in two directions, then sprinting, the measured pass warmed by the identical one.
        var a=new Pilot("daggers");float t0=1;
        void Pass(){for(int f=0;f<240;f++){float t=t0+f*Dt;bool on=f>=160;a.Frame(t,on?SprintSpeed:RunSpeed,on,f<80?0:f<160?(float)Math.PI/2:0);}t0+=240*Dt;}
        Pass();long before=GC.GetAllocatedBytesForCurrentThread();Pass();long used=GC.GetAllocatedBytesForCurrentThread()-before;
        log.Add("daggers run (ahead and strafing) and sprint: "+used+" bytes allocated over 240 frames after the identical warm pass");
        Check(used==0,"daggers run and sprint: no allocation per frame ("+used+" bytes)");
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
