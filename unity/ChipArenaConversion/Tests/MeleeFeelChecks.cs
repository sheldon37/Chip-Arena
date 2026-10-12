using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Unity-owned melee feel checks (owner 2026-10-12, docs/MELEE-FEEL.md); the source parity of everything else stays in
// MeleeMotionChecks. Every captured melee weapon is posed through MeleeMotion as the third-person rigs pose it, driven by
// scripted travel (in the source frame: radians, +x the rig's left).
// Steady locomotion torso: the chest twist, roll and nod and the head's sideways sway stay within bounds at a walk, a run,
// a sprint, strafing, backpedalling and circling a lock-on target (the sword and daggers run within the owner's targets:
// 15 deg twist, 6 deg roll, 6 deg nod, 3 cm head sway; before the fix 52 deg, 20 deg, 23 deg and 6 cm), the run's hunch
// stays lifted, and the chest turns without a pop through speed ramps, turns, stops and runs into and out of a swing.
internal static class MeleeFeelChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Melee feel check failed: "+message);}
    public static IEnumerable<string> Run(string root,ConversionContent data)
    {
        foreach(var r in Torso())yield return r;
        foreach(var r in Continuity(data))yield return r;
    }

    // ---- the pilot driver ----
    private sealed class Pilot
    {
        public readonly MeleeMotion Motion=new MeleeMotion{Impacts=new MeleeImpacts()};
        public readonly MeleeView View;
        public Vector3 Position;private float last=-100;
        public Pilot(string spec){View=new MeleeView{Id="feel-"+spec,Spec=spec};}
        public void Pose(float t)
        {
            float dt=last<-90?Dt:t-last;last=t;var r=Motion.Rig;r.GroupPosition=Position;r.GroupYaw=Motion.PresentedYaw(View,t,View.Yaw);Motion.Sample(View,t,dt);
        }
        // A bone's rotation and position in the pilot's own frame (its presented facing removed).
        public Quaternion Local(int bone)=>Motion.Rig.ChainQuat(bone,-1);
        public Vector3 LocalPoint(int bone)=>Motion.Rig.ChainPoint(bone,-1);
        public Quaternion World(int bone)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,Motion.Rig.GroupYaw)*Local(bone);
    }
    private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(a,b))))*Deg;
    private static Vector3 YawPitchRoll(Quaternion q)
    {
        Vector3 f=Vector3.Transform(Vector3.UnitZ,q),r=Vector3.Transform(Vector3.UnitX,q);
        return new Vector3((float)Math.Atan2(f.X,f.Z)*Deg,(float)Math.Asin(Math.Clamp(-f.Y,-1,1))*Deg,(float)Math.Asin(Math.Clamp(r.Y,-1,1))*Deg);
    }
    private sealed class Range{public float Min=float.MaxValue,Max=float.MinValue,Sum;public int N;public void Add(float v){Min=Math.Min(Min,v);Max=Math.Max(Max,v);Sum+=v;N++;}public float PP=>Max-Min;public float Mean=>Sum/Math.Max(1,N);}
    // Chest angular rate (deg/s) and its largest frame-to-frame step: a pop is a rate that jumps between two frames.
    private sealed class Rates
    {
        private Quaternion? prev;private float prevRate=float.NaN;public float MaxStep;
        public void Add(Quaternion q,bool record){if(prev.HasValue){float rate=Angle(prev.Value,q)/Dt;if(record&&!float.IsNaN(prevRate))MaxStep=Math.Max(MaxStep,Math.Abs(rate-prevRate));prevRate=rate;}prev=q;}
    }

    // ---- steady locomotion torso ----
    private struct Gait{public float Yaw,Roll,Nod,Hunch,Sway;}
    // Steady travel at `speed` in direction `dir` (radians from the facing), or circling a target 4.5 m away while facing it.
    private static Gait Travel(string spec,float speed,bool sprint,float dir,bool lockOn)
    {
        var p=new Pilot(spec);var v=p.View;var yaw=new Range();var roll=new Range();var nod=new Range();var sway=new Range();const float R=4.5f;
        for(int f=0;f<270;f++)
        {
            float t=1+f*Dt;Vector3 vel;
            if(lockOn){float a=speed*f*Dt/R;p.Position=new Vector3((float)Math.Sin(a)*R,0,R-(float)Math.Cos(a)*R);v.Yaw=(float)Math.Atan2(-p.Position.X,R-p.Position.Z);vel=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a))*speed;}
            else{vel=new Vector3((float)Math.Sin(dir),0,(float)Math.Cos(dir))*speed;p.Position+=vel*Dt;}
            v.Vel=vel;v.Sprinting=sprint;p.Pose(t);
            if(f<90)continue;
            var c=YawPitchRoll(p.Local(CapturedRig.Chest));yaw.Add(c.X);nod.Add(c.Y);roll.Add(c.Z);sway.Add(p.LocalPoint(CapturedRig.Head).X*100);
        }
        return new Gait{Yaw=yaw.PP,Roll=roll.PP,Nod=nod.PP,Hunch=nod.Mean,Sway=sway.PP};
    }
    private static IEnumerable<string> Torso()
    {
        var cases=new (string Name,float Speed,bool Sprint,float Dir,bool Lock)[]{("walk",3,false,0,false),("run",6.8f,false,0,false),("sprint",9.18f,true,0,false),("strafe",6.8f,false,(float)Math.PI/2,false),("backpedal",6.8f,false,(float)Math.PI,false),("backpedal walk",2.5f,false,(float)Math.PI,false),("lock-on circle",6.8f,false,0,true)};
        int n=0;string worst="";float worstYaw=0;
        foreach(var spec in Specs)foreach(var c in cases)
        {
            var g=Travel(spec,c.Speed,c.Sprint,c.Dir,c.Lock);string at=spec+" "+c.Name+": chest twist "+g.Yaw.ToString("0.0")+", roll "+g.Roll.ToString("0.0")+", nod "+g.Nod.ToString("0.0")+" deg p-p, head sway "+g.Sway.ToString("0.0")+" cm";
            bool walk=c.Speed<4,owner=(spec=="sword"||spec=="daggers")&&(c.Name=="run"||c.Name=="sprint");
            // The owner's targets for the sword (and the daggers, on the same captured run); bounds with a margin elsewhere.
            if(owner)Check(g.Yaw<=15&&g.Roll<=6&&g.Nod<=6&&g.Sway<=3,at+" (targets 15, 6, 6 deg and 3 cm)");
            else if(walk)Check(g.Yaw<=18&&g.Roll<=5&&g.Nod<=4&&g.Sway<=6.5f,at+" (walk bounds 18, 5, 4 deg and 6.5 cm)");
            else Check(g.Yaw<=15&&g.Roll<=10&&g.Nod<=7.5f&&g.Sway<=6,at+" (run bounds 15, 10, 7.5 deg and 6 cm)");
            // The run's forward hunch stays lifted (the sword run averaged 49 deg of chest pitch before the fix).
            if(c.Name=="run")Check(g.Hunch<=40,spec+" run chest pitch averages "+g.Hunch.ToString("0.0")+" deg (at most 40)");
            if(g.Yaw>worstYaw){worstYaw=g.Yaw;worst=at;}n++;
            if(owner&&c.Name=="run")yield return "melee steady torso "+at;
        }
        yield return "melee steady torso: "+n+" travel cases (4 weapons x walk, run, sprint, strafe, backpedal, backpedal walk, lock-on circle) within bounds; largest twist "+worst;
    }

    // ---- continuity: speed ramps, turns and runs into and out of a swing ----
    private static IEnumerable<string> Continuity(ConversionContent data)
    {
        float worstRamp=0,worstSwing=0;string rampAt="",swingAt="";
        foreach(var spec in Specs)
        {
            // 0 -> 1.5 -> 3 -> 4.6 -> 6.8 -> sprint 9.18 -> a 90 deg/s turning run -> strafe -> backpedal -> strafe walk -> stop, at 35 m/s2.
            var p=new Pilot(spec);var v=p.View;var rates=new Rates();var plan=new (float At,float Speed,bool Sprint,float Dir,float Turn)[]{(0,0,false,0,0),(.5f,1.5f,false,0,0),(1.3f,3,false,0,0),(2.2f,4.6f,false,0,0),(3,6.8f,false,0,0),(4,9.18f,true,0,0),(5,6.8f,false,0,90),(6,6.8f,false,90,0),(6.8f,6.8f,false,180,0),(7.6f,3,false,-90,0),(8.4f,0,false,0,0),(9.2f,0,false,0,0)};
            Vector3 vel=Vector3.Zero;float yaw=0;int k=0;
            for(int f=0;f*Dt<9.2f;f++)
            {
                float t=f*Dt;while(k+1<plan.Length&&plan[k+1].At<=t)k++;var s=plan[k];float d=s.Dir/Deg+yaw;
                Vector3 want=new Vector3((float)Math.Sin(d),0,(float)Math.Cos(d))*s.Speed,dv=want-vel;float max=35*Dt;if(dv.Length()>max)dv*=max/dv.Length();vel+=dv;yaw+=s.Turn/Deg*Dt;
                v.Yaw=yaw;v.Vel=vel;v.Sprinting=s.Sprint;p.Pose(1+t);p.Position+=vel*Dt;rates.Add(p.World(CapturedRig.Chest),t>.3f);
            }
            Check(rates.MaxStep<=200,spec+" chest rate jumps "+rates.MaxStep.ToString("0")+" deg/s between frames through the speed ramp (at most 200)");
            if(rates.MaxStep>worstRamp){worstRamp=rates.MaxStep;rampAt=spec;}
            // Run at 6.8 m/s, swing (the authority stops the walk and steps), run again from the attack end at 35 m/s2. The
            // gait frames before the swing and from three frames after the attack end (past the pose transition's first frame)
            // stay free of pops.
            var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());
            p=new Pilot(spec);v=p.View;rates=new Rates();var outRates=new Rates();vel=Vector3.Zero;NativeMeleeSwing swing=null;float end=0,startAt=1.2f;
            for(int f=0;f*Dt<startAt+2.4f;f++)
            {
                float t=f*Dt;
                if(swing==null&&t>=startAt&&end==0){swing=NativeMeleeRules.Begin(w,stats,t,1,0);swing.Until=Math.Max(swing.Until,t+(float)((w.windup+.28)/stats["attackRate"]));end=swing.Until;vel=Vector3.Zero;}
                bool locked=t<end;Vector3 step=Vector3.Zero;if(swing!=null&&locked){var st=NativeMeleeRules.Step(swing,t,Dt);step=new Vector3(-st.x,0,st.z);}
                if(!locked){Vector3 dv=new Vector3(0,0,6.8f)-vel;float max=35*Dt;if(dv.Length()>max)dv*=max/dv.Length();vel+=dv;}
                v.Vel=locked?step:vel;v.Yaw=0;bool live=swing!=null&&locked;
                if(swing!=null){v.LastAttack=swing.At;v.Windup=swing.HitAt-swing.At;v.AttackRate=(float)stats["attackRate"];v.AttackIndex=1;v.SwingAt=t<=swing.HitUntil?swing.HitAt:float.NaN;v.SwingUntil=t<=swing.HitUntil?swing.HitUntil:float.NaN;
                    v.Commit=live;v.CommitAt=swing.At;v.CommitUntil=end;v.CommitYaw=0;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}
                p.Pose(1+t);p.Position+=v.Vel*Dt;var q=p.World(CapturedRig.Chest);
                rates.Add(q,t>.6f&&t<startAt);outRates.Add(q,t>=end+3*Dt);
            }
            float jump=Math.Max(rates.MaxStep,outRates.MaxStep);
            Check(jump<=300,spec+" chest rate jumps "+jump.ToString("0")+" deg/s between frames running into or out of a swing (at most 300)");
            if(jump>worstSwing){worstSwing=jump;swingAt=spec;}
        }
        yield return "melee locomotion continuity: the chest rate steps at most "+worstRamp.ToString("0")+" deg/s per frame through speed ramps, turns, strafes, backpedals and stops ("+rampAt+") and "+worstSwing.ToString("0")+" running into and out of a swing ("+swingAt+")";
    }
}
