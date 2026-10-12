using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// Unity-owned melee feel checks (owner 2026-10-12, docs/MELEE-FEEL.md); the source parity of everything else stays in
// MeleeMotionChecks. Every captured melee weapon is posed through MeleeMotion as the third-person rigs pose it, driven by
// scripted travel (in the source frame: radians, +x the rig's left).
// Steady locomotion torso: the chest twist, roll and nod and the head's sideways sway stay within bounds at a walk, a run,
// a sprint, strafing, backpedalling and circling a lock-on target (the sword and daggers run within the owner's targets:
// 15 deg twist, 6 deg roll, 6 deg nod, 3 cm head sway; before the fix 52 deg, 20 deg, 23 deg and 6 cm), the run's hunch
// stays lifted, and the chest turns without a pop through speed ramps, turns, stops and runs into and out of a swing.
// Swings (held-LMB chains through the authority's NativeMeleeRules timing, whiffs and hits): the shared swing timing
// (MeleeSwing) is continuous and monotone; the blade's fastest motion lands inside the hit window (sword at least 40 m/s and
// greatsword 30 m/s on average there, before the retime 18-31); no frame jumps (the greatsword's 106 m/s pop, the 66 m/s
// pop of a swing begun after a 40 deg camera turn); the follow-through leaves the window moving (no dead stop) and held
// shields and greatswords no longer stand at the guard between swings; the slash crescent's head stays on the posed blade.
internal static class MeleeFeelChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI;
    // A sole on the floor may turn about one of its pivots but not slide: the slowest pivot's largest step per frame and total.
    private const float Skid=.35f,SkidTotal=.015f;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Melee feel check failed: "+message);}
    public static IEnumerable<string> Run(string root,ConversionContent data)
    {
        foreach(var r in Torso())yield return r;
        foreach(var r in Continuity(data))yield return r;
        foreach(var r in Timing(data))yield return r;
        foreach(var r in Swings(data))yield return r;
        foreach(var r in Feel(data))yield return r;
        foreach(var r in Sweeps(data))yield return r;
    }
    // ---- the hitbox's sweep order against the captured blade (S0, Astra's f566b9a) ----
    // NativeMeleeRules.Angle now sweeps in the native frame. Each cut whose posed blade crosses the front one way must have its
    // hitbox sweep the same way (the crescent follows the blade). Cuts the per-cut sweep table still runs against the blade are
    // reported, not failed: they are Astra's to settle (docs/MELEE-FEEL.md S0).
    private static IEnumerable<string> Sweeps(ConversionContent data)
    {
        var against=new List<string>();int agree=0;
        foreach(var spec in Specs)
        {
            var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());var set=MeleeSet.For(spec);
            for(int i=1;i<=set.Cuts.Length;i++)
            {
                var sw=NativeMeleeRules.Begin(w,stats,1,i,0);float hitbox=Math.Sign(NativeMeleeRules.Angle(sw,sw.HitUntil)-NativeMeleeRules.Angle(sw,sw.HitAt));
                var v=MeleeSwing.VisibleSweep(set,set.Cut(i));
                if(hitbox==v.Direction)agree++;else against.Add(set.Cut(i).Name);
            }
        }
        Check(agree>=8,"the hitbox sweeps with the captured blade on "+agree+" cuts (at least 8 since S0)");
        yield return "melee sweeps: the hitbox crosses the front the way the captured blade does on "+agree+" cuts"+(against.Count>0?"; still against it: "+string.Join(", ",against)+" (Astra, docs/MELEE-FEEL.md S0)":"");
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

    // ---- the shared swing timing (third person, first-person view model, crescent) ----
    private static IEnumerable<string> Timing(ConversionContent data)
    {
        int cuts=0;
        foreach(var spec in Specs)
        {
            var set=MeleeSet.For(spec);var w=data.Weapon(spec);float windup=(float)w.windup,active=.16f,recovery=Math.Max(MeleeMotion.Recovery(spec),(float)w.interval-windup-active);
            foreach(var clip in set.Cuts)
            {
                var sh=MeleeSwing.For(clip);Check(sh.Start>=clip.Marks[0]&&sh.Start<sh.SweepStart&&sh.SweepStart<sh.SweepEnd&&sh.SweepEnd<=sh.FollowEnd&&sh.FollowEnd<=clip.Marks[3],clip.Name+" shape ordered within the clip");
                foreach(bool keep in new[]{false,true})
                {
                    float prev=MeleeSwing.ClipTime(clip,0,windup,active,recovery,keep);Check(Math.Abs(prev-sh.Start)<1e-5f,clip.Name+" starts at its shape's start");
                    for(int f=1;f<=600;f++)
                    {
                        float age=f*(windup+active+recovery+.05f)/600,ct=MeleeSwing.ClipTime(clip,age,windup,active,recovery,keep);
                        Check(ct>=prev-1e-5f,clip.Name+" clip time never runs back (age "+age.ToString("0.000")+")");Check(ct-prev<.02f,clip.Name+" clip time continuous (age "+age.ToString("0.000")+")");prev=ct;
                    }
                    Check(Math.Abs(MeleeSwing.ClipTime(clip,windup,windup,active,recovery,keep)-sh.SweepStart)<1e-4f&&Math.Abs(MeleeSwing.ClipTime(clip,windup+active,windup,active,recovery,keep)-sh.SweepEnd)<1e-4f,clip.Name+" the sweep spans the hit window");
                    float end=MeleeSwing.ClipTime(clip,windup+active+recovery,windup,active,recovery,keep);
                    Check(keep?Math.Abs(end-clip.Marks[3])<1e-4f:Math.Abs(end-sh.FollowEnd)<1e-4f,clip.Name+(keep?" the kept recovery ends on its settled mark":" the follow-through comes to rest at its end"));
                    // Leaving the window the clip keeps the window's rate (no dead stop); entering it, the windup ends near that rate.
                    float rate=(sh.SweepEnd-sh.SweepStart)/active,e=1e-3f,after=(MeleeSwing.ClipTime(clip,windup+active+e,windup,active,recovery,keep)-sh.SweepEnd)/e;
                    Check(Math.Abs(after-rate)<.08f*rate+.02f,clip.Name+" the follow-through leaves at the window's rate ("+after.ToString("0.00")+" vs "+rate.ToString("0.00")+")");
                }
                Check(MeleeSwing.Settle(windup+active,windup,active,recovery)<1e-5f&&Math.Abs(MeleeSwing.Settle(windup+active+recovery,windup,active,recovery)-1)<1e-4f,clip.Name+" the guard blend runs over the recovery");
                cuts++;
            }
        }
        yield return "melee swing timing: "+cuts+" cuts start on their shape, play their sweep across the hit window and leave it at the window's rate, follow through to rest (or the daggers' kept recovery to its mark) and never run back";
    }

    // ---- held swing chains ----
    private sealed class Frame{public float T,Tip,TipR,TipL,Yaw,BearingR,BearingL,Head;public string Phase;public int Swing;public bool HasHead;}
    // Four swings held as ConversionGame sends LMB (each starts at the last one's AttackEnd), optionally landing mid-window and
    // with a 40 deg camera turn through the first recovery; the presented rig, its fastest blade tip and the crescent per frame.
    private static List<Frame> Chain(ConversionContent data,string spec,bool hit,bool turn)
    {
        var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());var p=new Pilot(spec);var v=p.View;var frames=new List<Frame>();
        NativeSlashFx.Look(spec,out var look);NativeMeleeSwing swing=null;float end=0,input=0;int swings=0;Vector3? prevR=null,prevL=null;
        for(int f=0;f<400;f++)
        {
            float t=1+f*Dt;
            if(turn&&swings==1&&swing!=null&&t>swing.HitUntil&&t<end)input=Math.Min(40/Deg,input+40/Deg*Dt/.2f);
            if(swing!=null&&t>swing.HitUntil&&t>=swing.Until)swing=null;
            if(swings<4&&t>=end&&t>=1.1f)
            {
                swings++;swing=NativeMeleeFeel.Begin(w,stats,t,swings,input);swing.Until=Math.Max(swing.Until,t+(float)((w.windup+.28)/stats["attackRate"]));end=swing.Until;
                if(hit)p.Motion.Impacts.RecordHit(v.Id,(swing.HitAt+swing.HitUntil)/2);
            }
            bool live=swing!=null&&t<end;var st=swing!=null?NativeMeleeRules.Step(swing,t,Dt):new V3();
            // The source frame mirrors the native one: x and yaw negate.
            v.Vel=new Vector3(-st.x,0,st.z);v.Yaw=live?-swing.Yaw:-input;
            if(swing!=null){v.LastAttack=swing.At;v.Windup=swing.HitAt-swing.At;v.AttackRate=(float)stats["attackRate"];v.AttackIndex=swings;v.SwingAt=t<=swing.HitUntil?swing.HitAt:float.NaN;v.SwingUntil=t<=swing.HitUntil?swing.HitUntil:float.NaN;
                v.Commit=live;v.CommitAt=swing.At;v.CommitUntil=end;v.CommitYaw=-swing.Yaw;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}
            else v.Commit=false;
            p.Pose(t);p.Position+=v.Vel*Dt;
            var r=p.Motion.Rig;var set=MeleeSet.For(spec);Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,r.GroupYaw);
            Vector3 Tip(int hand)=>Vector3.Transform(r.ChainPoint(hand,-1)+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(hand,-1)),g)+r.GroupPosition;
            Vector3 tr=Tip(CapturedRig.RHand),tl=Tip(CapturedRig.LHand);float sr=prevR.HasValue?Vector3.Distance(tr,prevR.Value)/Dt:0,sl=prevL.HasValue?Vector3.Distance(tl,prevL.Value)/Dt:0;prevR=tr;prevL=tl;
            bool left=set.Dual&&sl>sr;Vector3 pelvis=Vector3.Transform(r.ChainPoint(CapturedRig.Pelvis,-1),g)+r.GroupPosition;
            var fr=new Frame{T=t,Tip=left?sl:sr,TipR=sr,TipL=set.Dual?sl:0,Swing=swings,Phase=swing==null?"after":t<swing.HitAt?"windup":t<=swing.HitUntil?"active":"recovery",Yaw=-r.GroupYaw,
                BearingR=-(float)Math.Atan2(tr.X-pelvis.X,tr.Z-pelvis.Z),BearingL=-(float)Math.Atan2(tl.X-pelvis.X,tl.Z-pelvis.Z)};
            if(swing!=null&&NativeSlashFx.Frame(swing,look,t,false,out _,out float head,out _,out _)){fr.Head=head;fr.HasHead=true;}
            frames.Add(fr);if(swings>=4&&t>end+.3f)break;
        }
        return frames;
    }
    private static float Mean(List<Frame> fs,int swing,string phase){float s=0;int n=0;foreach(var f in fs)if(f.Swing==swing&&f.Phase==phase){s+=f.Tip;n++;}return n>0?s/n:0;}
    private static IEnumerable<string> Swings(ConversionContent data)
    {
        var mins=new Dictionary<string,float>{{"sword",40},{"greatsword",30},{"daggers",12},{"shield",7}};string means="";float worstSpike=0;string spikeAt="";int synced=0,dwell=0;float worstSync=0;
        foreach(var spec in Specs)
        {
            float low=float.MaxValue;
            foreach(bool hit in new[]{false,true})
            {
                var fs=Chain(data,spec,hit,false);
                for(int k=1;k<=4;k++)
                {
                    float m=Mean(fs,k,"active");if(!hit){low=Math.Min(low,m);Check(m>=mins[spec],spec+" swing "+k+" moves the blade "+m.ToString("0.0")+" m/s on average through the hit window (at least "+mins[spec]+")");}
                    // The fastest frame of the swing lands in its window (the daggers' 90 ms windup also repositions both blades).
                    Frame peak=null;foreach(var f in fs)if(f.Swing==k&&f.Phase!="after"&&(peak==null||f.Tip>peak.Tip))peak=f;
                    if(spec!="daggers"&&!hit)Check(peak.Phase=="active",spec+" swing "+k+" peaks in the "+peak.Phase+" ("+peak.Tip.ToString("0")+" m/s), not inside the hit window");
                    // No dead stop where the follow-through takes over (the daggers keep their captured recovery and wrist limiter).
                    if(spec!="daggers"&&!hit){Frame lastActive=null,firstRecovery=null;foreach(var f in fs)if(f.Swing==k){if(f.Phase=="active")lastActive=f;else if(f.Phase=="recovery"&&firstRecovery==null)firstRecovery=f;}
                        Check(firstRecovery.Tip>=.25f*lastActive.Tip&&firstRecovery.Tip>=2,spec+" swing "+k+" stops dead leaving the window ("+lastActive.Tip.ToString("0.0")+" -> "+firstRecovery.Tip.ToString("0.0")+" m/s)");}
                }
                // No one-frame pop: a frame several times faster than both neighbours, or an absurd tip speed.
                for(int i=1;i+1<fs.Count;i++){float n=Math.Max(fs[i-1].Tip,fs[i+1].Tip),ratio=fs[i].Tip/Math.Max(2,n);if(fs[i].Tip>8&&ratio>worstSpike){worstSpike=ratio;spikeAt=spec+(hit?" hits":" whiffs")+" "+fs[i].Phase+" "+fs[i].Tip.ToString("0")+" m/s";}Check(fs[i].Tip<150,spec+(hit?" hits":"")+" blade tip at "+fs[i].Tip.ToString("0")+" m/s");}
                // Held swings reach the guard as the next one starts: at most 3 frames below 1 m/s within 0.1 s of the boundary
                // (before: 7 for the shield and greatsword, which stood at the guard for 0.08-0.10 s).
                if(!hit)for(int k=2;k<=4;k++)
                {
                    float at=0;foreach(var f in fs)if(f.Swing==k){at=f.T;break;}
                    int still=0,run=0;foreach(var f in fs)if(f.T>=at-.1f-1e-4f&&f.T<at+.1f){run=f.Tip<1?run+1:0;still=Math.Max(still,run);}
                    Check(still<=3,spec+" stands at the guard "+still+" frames before swing "+k);dwell=Math.Max(dwell,still);
                }
                // The slash crescent's head stays on the posed blade while the blade crosses the arc (for the daggers the blade that
                // travels further through the window, as the crescent picks it).
                if(!hit&&spec!="shield")for(int k=1;k<=4;k++)
                {
                    float pr=0,pl=0;foreach(var f in fs)if(f.Swing==k&&f.Phase=="active"){pr+=f.TipR;pl+=f.TipL;}
                    foreach(var f in fs)if(f.Swing==k&&f.Phase=="active"&&f.HasHead)
                    {
                        NativeSlashFx.Look(spec,out var look);float bearing=pl>pr?f.BearingL:f.BearingR,half=Math.Min(look.MaxArc,spec=="sword"&&k%3==0?.8f:(float)data.Weapon(spec).arc)*.45f;
                        if(Math.Abs(MeleeMath.Wrap(bearing-f.Yaw))>half)continue;float err=Math.Abs(MeleeMath.Wrap(f.Head-bearing));synced++;worstSync=Math.Max(worstSync,err);
                        Check(err<.2f,spec+" swing "+k+" crescent head "+(err*Deg).ToString("0")+" deg off the blade at "+f.T.ToString("0.000"));
                    }
                }
            }
            means+=(means.Length>0?", ":"")+spec+" "+low.ToString("0");
        }
        Check(worstSpike<=2.5f,"blade tip pop: "+spikeAt+" is x"+worstSpike.ToString("0.0")+" its neighbours");
        // A swing begun after a 40 deg camera turn eases its facing over MeleeMotion.TurnBlend instead of one frame.
        var turn=Chain(data,"sword",false,true);float jump=0,turnSpike=0;
        for(int i=1;i<turn.Count;i++){jump=Math.Max(jump,Math.Abs(MeleeMath.Wrap(turn[i].Yaw-turn[i-1].Yaw)));if(i+1<turn.Count&&turn[i].Tip>8)turnSpike=Math.Max(turnSpike,turn[i].Tip/Math.Max(2,Math.Max(turn[i-1].Tip,turn[i+1].Tip)));}
        Check(jump*Deg<=20&&turnSpike<=2.5f,"a swing after a 40 deg camera turn: facing step "+(jump*Deg).ToString("0")+" deg per frame, blade pop x"+turnSpike.ToString("0.0"));
        yield return "melee swings: held chains move the blade through the hit window at least "+means+" m/s on average (whiffs), the sword, shield and greatsword peak inside it and leave it moving, no frame pops (worst x"+worstSpike.ToString("0.0")+", "+spikeAt+"), held swings meet the next one (at most "+dwell+" frames below 1 m/s), a 40 deg turn eases in ("+(jump*Deg).ToString("0")+" deg per frame at most), the crescent head stays within "+(worstSync*Deg).ToString("0")+" deg of the blade over "+synced+" frames";
    }

    // ---- the native feel (NativeMeleeFeel): early chains, lock-on lunges, sprint entries, windup turning, combo resets ----
    private sealed class Beat{public float T,Tip,Yaw,CommitYaw,Pitch,Hips;public Vector3 TipLocal;public Vector3[] Feet=new Vector3[2];public bool[] Planted=new bool[2];public float[] Heel=new float[2],Heading=new float[2];public string Phase,State;public int Swing;public bool Commit;}
    private sealed class Struck{public float At,HitAt,HitUntil,ChainAt,Distance;public bool Chained,Opener;public int Index;}
    // Skate/Slide: the soles' slip on the floor, the slowest of their pivots (toe joint, heel edge, sole centre) per frame, its
    // largest and its total; Sink: how far a sole went into the floor; Flights: each step off the floor (foot, lift, land, the
    // sole's highest clearance, the distance travelled, whether it left during a swing rather than the gait's settling after it).
    private sealed class Play{public readonly List<Beat> Beats=new List<Beat>();public readonly List<Struck> Swings=new List<Struck>();public readonly float[] Skate=new float[2],Slide=new float[2];public float Sink;
        public readonly List<(int foot,float lift,float land,float clear,float dist,bool swing)> Flights=new List<(int,float,float,float,float,bool)>();}
    // A sole as posed: its lowest corner above the floor (the foot and toe boxes), its pivots in world space, heel pitch and heading.
    private struct Sole{public float Low,Pitch,Heading;public Vector3 Toe,Heel,Mid,Ankle;}
    private static Sole SoleOf(MeleeRig r,bool left)
    {
        int fb=left?CapturedRig.LFoot:CapturedRig.RFoot;Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,r.GroupYaw);float floor=r.GroupPosition.Y+MeleeRig.FLOOR-MeleeFootwork.Sole,low=float.MaxValue;
        Vector3 W(Vector3 v)=>Vector3.Transform(v,g)+r.GroupPosition;
        foreach(var box in CapturedRig.Boxes)
        {
            if(box.Bone!=fb&&box.Bone!=fb+1)continue;Quaternion q=r.ChainQuat(box.Bone,-1);Vector3 o=r.ChainPoint(box.Bone,-1);
            for(int c=0;c<8;c++){Vector3 h=new Vector3((c&1)!=0?.5f:-.5f,(c&2)!=0?.5f:-.5f,(c&4)!=0?.5f:-.5f)*box.Size;low=Math.Min(low,W(o+Vector3.Transform(box.Position+h,q)).Y-floor);}
        }
        Quaternion fq=r.ChainQuat(fb,-1);Vector3 ankle=r.ChainPoint(fb,-1),fwd=Vector3.Transform(Vector3.UnitZ,fq);
        return new Sole{Low=low,Toe=W(r.ChainPoint(fb+1,-1)),Heel=W(ankle+Vector3.Transform(new Vector3(0,-.07f,-.135f),fq)),Mid=W(ankle+Vector3.Transform(new Vector3(0,-.07f,-.025f),fq)),Ankle=W(ankle),
            Pitch=(float)Math.Asin(MeleeMath.Clamp(-fwd.Y,-1,1)),Heading=(float)Math.Atan2(fwd.X,fwd.Z)+r.GroupYaw};
    }
    // The host's melee input as NativeMelee and NativeMeleeAbilities run it each tick (StepMelee's windup turn and retirement,
    // then StepMeleeInput's edge buffer, early chain and combo reset; a lock target straight ahead sets the step, a sprint
    // run-up stops dead at the first swing), presented through MeleeMotion. Times are seconds after the first press.
    // wall: the body is held where it stands (a step into a wall), whatever the simulated step.
    private static Play Drive(ConversionContent data,string spec,float[] presses,float lockDistance=0,bool sprint=false,float turnTo=0,float turnAt=99,float until=2.4f,bool wall=false)
    {
        var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());float rate=(float)stats["attackRate"];var p=new Pilot(spec);var v=p.View;var play=new Play();
        const float T0=1;NativeMeleeSwing swing=null;Struck cur=null;float attackEnd=0,comboUntil=0,buffer=-1,yaw=0;int combo=-1;Vector3 at=Vector3.Zero,target=new Vector3(0,0,lockDistance);Vector3? prevTip=null;var prevSole=new Sole?[2];var air=new (float at,float clear,Vector3 from,bool swing)[2];air[0].at=air[1].at=-1;
        var set=MeleeSet.For(spec);
        for(int f=0;f*Dt<T0+until;f++)
        {
            float t=f*Dt,input=(t<T0+turnAt?0:Math.Min(1,(t-T0-turnAt)/.2f)*turnTo)/Deg;
            if(swing!=null){if(t<swing.HitAt){float desired=lockDistance>0?(float)Math.Atan2(target.X-at.X,target.Z-at.Z)*Deg:input*Deg;swing.Yaw=NativeMeleeFeel.Turn(swing.Yaw,desired,spec,Dt);yaw=swing.Yaw;}if(t>swing.HitUntil&&t>=swing.Until)swing=null;}
            bool edge=false;foreach(float pt in presses)if(Math.Abs(t-T0-pt)<Dt*.5f)edge=true;
            if(edge&&NativeMeleeKitRules.BufferOpen(swing,t))buffer=t+NativeMeleeFeel.BufferSeconds;
            bool queued=buffer>=t,chain=queued&&NativeMeleeKitRules.ChainReady(swing,t);
            if((edge||queued)&&(chain||t>=attackEnd))
            {
                if(t>comboUntil)combo=-1;combo++;if(!chain)yaw=input;
                swing=NativeMeleeFeel.Begin(w,stats,t,combo+1,yaw);swing.SprintAttack=!chain&&sprint&&play.Swings.Count==0;
                Vector3 d=target-at;if(lockDistance>0&&Vector3.Dot(d,new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw)))>0){swing.Distance=NativeMeleeFeel.Distance(spec,chain,d.Length());swing.OpenerLunge=!chain&&swing.Distance>NativeMeleeFeel.DefaultStep(spec);}
                swing.Until=Math.Max(swing.Until,t+(float)((w.windup+.28)/rate));comboUntil=swing.Until+NativeMeleeFeel.ComboPause;attackEnd=swing.Until;buffer=-1;
                cur=new Struck{At=t-T0,HitAt=swing.HitAt-T0,HitUntil=swing.HitUntil-T0,ChainAt=swing.ChainAt-T0,Distance=swing.Distance,Chained=chain,Opener=swing.OpenerLunge,Index=combo+1};play.Swings.Add(cur);
            }
            bool live=swing!=null&&t<attackEnd,run=sprint&&play.Swings.Count==0;var st=swing!=null&&live?NativeMeleeRules.Step(swing,t,Dt):new V3();
            Vector3 vel=new Vector3(st.x,0,(run?9.18f:0)+st.z);
            // The source frame mirrors the native one: x and yaw negate.
            v.Vel=new Vector3(-vel.X,0,vel.Z);v.Sprinting=run;v.Yaw=live?-swing.Yaw:-input;
            if(cur!=null)
            {
                v.LastAttack=cur.At+T0;v.Windup=cur.HitAt-cur.At;v.AttackRate=rate;v.AttackIndex=combo+1;bool window=swing!=null&&t<=swing.HitUntil;v.SwingAt=window?swing.HitAt:float.NaN;v.SwingUntil=window?swing.HitUntil:float.NaN;
                v.Commit=live;if(live){v.CommitAt=swing.At;v.CommitUntil=attackEnd;v.CommitYaw=-swing.Yaw;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}v.OpenerLunge=live&&swing.OpenerLunge;v.SprintAttack=live&&swing.SprintAttack;
            }
            p.Pose(t);
            var r=p.Motion.Rig;Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,r.GroupYaw);
            Vector3 local=r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(CapturedRig.RHand,-1)),tip=Vector3.Transform(local,g)+r.GroupPosition;
            var b=new Beat{T=t-T0,Tip=prevTip.HasValue?Vector3.Distance(tip,prevTip.Value)/Dt:0,TipLocal=local,Yaw=r.GroupYaw,CommitYaw=v.CommitYaw,Commit=v.Committed(t),Swing=play.Swings.Count,State=p.Motion.State,Pitch=Lean(p),
                Phase=swing==null?(cur==null?"idle":"after"):t<swing.HitAt?"windup":t<=swing.HitUntil?"active":"recovery"};prevTip=tip;
            b.Hips=Vector3.Transform(r.ChainPoint(CapturedRig.Pelvis,-1),g).Y+r.GroupPosition.Y;
            for(int k=0;k<2;k++)
            {
                // A sole on the floor (its lowest corner within 1.2 cm of it) must hold still about one of its pivots; one in
                // the air is on a step (counted from the first press).
                var so=SoleOf(r,k==0);b.Feet[k]=so.Ankle;b.Planted[k]=so.Low<.012f;b.Heel[k]=so.Pitch;b.Heading[k]=so.Heading;
                if(b.T>=0)
                {
                    play.Sink=Math.Max(play.Sink,-so.Low);
                    if(b.Planted[k]&&prevSole[k].HasValue&&prevSole[k].Value.Low<.012f)
                    {
                        var q=prevSole[k].Value;float Off(Vector3 a,Vector3 c){Vector3 m=a-c;m.Y=0;return m.Length();}
                        float pin=Math.Min(Off(so.Toe,q.Toe),Math.Min(Off(so.Heel,q.Heel),Off(so.Mid,q.Mid)));play.Skate[k]=Math.Max(play.Skate[k],pin/Dt);play.Slide[k]+=pin;
                    }
                    if(!b.Planted[k]&&(!prevSole[k].HasValue||prevSole[k].Value.Low<.012f))air[k]=(b.T,so.Low,so.Ankle,b.Commit);
                    else if(!b.Planted[k])air[k].clear=Math.Max(air[k].clear,so.Low);
                    else if(prevSole[k].HasValue&&prevSole[k].Value.Low>=.012f&&air[k].at>=0){Vector3 d=so.Ankle-air[k].from;d.Y=0;play.Flights.Add((k,air[k].at,b.T,air[k].clear,d.Length(),air[k].swing));}
                }
                prevSole[k]=so;
            }
            play.Beats.Add(b);p.Position+=(wall?new Vector3(0,0,run?9.18f:0):v.Vel)*Dt;at+=(wall?Vector3.Zero:vel)*Dt;
        }
        return play;
    }
    // The trunk's forward lean (deg): the head ahead of the pelvis in the pilot's frame, whatever the torso's twist.
    private static float Lean(Pilot p){Vector3 h=p.LocalPoint(CapturedRig.Head)-p.LocalPoint(CapturedRig.Pelvis);return (float)Math.Atan2(h.Z,h.Y)*Deg;}
    private static IEnumerable<string> Feel(ConversionContent data)
    {
        var notes=new List<string>();
        // Early chains: each press from the last swing's hit on is buffered (0.4 s) and chains at its ChainAt.
        var chainPress=new Dictionary<string,float[]>{{"sword",new[]{0,.2f,.7f}},{"daggers",new[]{0,.12f,.44f,.76f}},{"shield",new[]{0,.25f,.85f}},{"greatsword",new[]{0,.6f,1.55f}}};
        float worstSkate=0,worstSlide=0,worstSink=0,worstClear=1,worstDip=1,worstStall=99;string slideAt="",clearAt="",dipAt="",stallAt="";
        // Footwork (owner 2026-10-12: "They slide across with their feet on the ground", docs/MELEE-FEEL.md "Footwork"): a sole on
        // the floor holds still about a pivot (at most `skate` m/s in a frame, `slide` in all), never sinks into the floor, and
        // every step that travels lifts the sole clear of it (before: toe-dragged rear feet slid 5-17 m/s, 3-11 cm deep, and
        // steps cleared 3 cm).
        void Feet(Play pl,string what,float skate,float slide)
        {
            for(int k=0;k<2;k++){if(pl.Skate[k]>worstSkate)worstSkate=pl.Skate[k];if(pl.Slide[k]>worstSlide){worstSlide=pl.Slide[k];slideAt=what;}}
            worstSink=Math.Max(worstSink,pl.Sink);
            Check(pl.Skate[0]<=skate&&pl.Skate[1]<=skate&&pl.Slide[0]<=slide&&pl.Slide[1]<=slide,what+": soles on the floor slip "+Math.Max(pl.Skate[0],pl.Skate[1]).ToString("0.00")+" m/s, "+(Math.Max(pl.Slide[0],pl.Slide[1])*100).ToString("0.0")+" cm in all (at most "+skate+" m/s, "+(slide*100).ToString("0.0")+" cm)");
            Check(pl.Sink<=.005f,what+": a sole sinks "+(pl.Sink*100).ToString("0.0")+" cm into the floor");
            foreach(var fl in pl.Flights)if(fl.dist>.1f&&fl.swing){if(fl.clear<worstClear){worstClear=fl.clear;clearAt=what;}Check(fl.clear>=.05f,what+": a "+(fl.dist*100).ToString("0")+" cm step clears the floor by only "+(fl.clear*100).ToString("0.0")+" cm");}
        }
        foreach(var spec in Specs)
        {
            var pl=Drive(data,spec,chainPress[spec]);
            Check(pl.Swings.Count==chainPress[spec].Length,spec+" early chain drove "+pl.Swings.Count+" swings");
            for(int i=1;i<pl.Swings.Count;i++)Check(pl.Swings[i].Chained&&Math.Abs(pl.Swings[i].At-pl.Swings[i-1].ChainAt)<Dt,spec+" swing "+(i+1)+" chains at the last one's ChainAt");
            Feet(pl,spec+" early chain",Skid,SkidTotal);
            // The chain enters from the live follow-through: the blade neither stalls nor, over the first 40% of the entry blend,
            // closes on the guard faster than that follow-through would have without the chain (an entry from the guard did:
            // the greatsword's blade stopped, 9.6 -> 1.8 m/s, and went back to the guard first).
            Vector3 guard=pl.Beats[30].TipLocal;
            for(int i=1;i<pl.Swings.Count;i++)
            {
                var s=pl.Swings[i];float[] earlier=new float[i];Array.Copy(chainPress[spec],earlier,i);var alone=Drive(data,spec,earlier);
                float entry=MeleeMotion.ChainEntry(s.HitAt-s.At),before=0,slow=float.MaxValue,dip=1;
                for(int j=0;j<pl.Beats.Count&&j<alone.Beats.Count;j++)
                {
                    var b=pl.Beats[j];if(b.T<s.At-1e-4f){before=b.Tip;continue;}
                    float own=Vector3.Distance(alone.Beats[j].TipLocal,guard);
                    if(b.T<=s.At+.4f*entry+1e-4f&&own>=.2f)dip=Math.Min(dip,Vector3.Distance(b.TipLocal,guard)/own);
                    if(b.T<=s.At+.1f)slow=Math.Min(slow,b.Tip);
                }
                float stall=slow/Math.Max(1.5f,before);
                if(dip<worstDip){worstDip=dip;dipAt=spec+" swing "+(i+1);}if(stall<worstStall){worstStall=stall;stallAt=spec+" swing "+(i+1);}
                Check(dip>=.7f,spec+" chained swing "+(i+1)+" swings its blade back toward the guard ("+(dip*100).ToString("0")+"% of the follow-through's distance from it)");
                Check(stall>=.3f,spec+" chained swing "+(i+1)+" stalls the blade ("+before.ToString("0.0")+" -> "+slow.ToString("0.0")+" m/s)");
            }
            // No frame pop (the daggers' 90 ms windup repositions both blades against the floor guard: x3.0 at 66 m/s before).
            for(int i=1;i+1<pl.Beats.Count;i++){var b=pl.Beats[i];if(b.Tip>8)Check(b.Tip/Math.Max(2,Math.Max(pl.Beats[i-1].Tip,pl.Beats[i+1].Tip))<=(spec=="daggers"?3.2f:2.5f)&&b.Tip<120,spec+" early chain blade pop at "+b.T.ToString("0.000")+" ("+b.Tip.ToString("0")+" m/s)");}
        }
        notes.Add("early chains enter from the live follow-through (the blade keeps at least "+(worstStall*100).ToString("0")+"% of its speed, "+stallAt+", and at least "+(worstDip*100).ToString("0")+"% of the follow-through's distance from the guard, "+dipAt+")");
        // Lock-on openers at each weapon's cap (OpenerLunge) and default steps: the soles hold, the stepping foot plants inside
        // the hit window, the rear foot follows with a recovery step, the hips drop into the plant and the chest leans into the
        // lunge.
        float leanLow=float.MaxValue,dropLow=float.MaxValue;var drops=new Dictionary<string,float>();
        foreach(var spec in Specs)
        {
            float cap=NativeMeleeFeel.StepCap(spec,false);var lunge=Drive(data,spec,new[]{0f},cap+1.15f,until:1.2f);var plain=Drive(data,spec,new[]{0f},until:1.2f);var s=lunge.Swings[0];
            Check(s.Opener&&Math.Abs(s.Distance-cap)<1e-3f&&!plain.Swings[0].Opener,spec+" opener steps the cap ("+s.Distance.ToString("0.00")+" m)");
            Feet(lunge,spec+" "+cap.ToString("0.0")+" m opener",Skid,SkidTotal);Feet(plain,spec+" default step",Skid,SkidTotal);
            // The stepping foot (the cut's lead) touches down between the hit window's start and its end; the other foot lands
            // its recovery step after the window, before the swing ends.
            MeleeMotion.Describe(spec,0,out int sign,out _,out _);int leadFoot=sign>0?0:1;
            foreach(var pl in new[]{lunge,plain})
            {
                var sw=pl.Swings[0];string what=spec+(pl==lunge?" opener":" default step");float plant=float.NaN,follow=float.NaN,end=sw.HitUntil+.6f;
                foreach(var fl in pl.Flights){if(fl.foot==leadFoot&&fl.dist>.3f&&float.IsNaN(plant))plant=fl.land;if(fl.foot!=leadFoot&&fl.land>sw.HitUntil-.02f&&float.IsNaN(follow))follow=fl.land;}
                Check(plant>=sw.HitAt-.03f-1e-4f&&plant<=sw.HitUntil+Dt,what+" plants its lead foot at "+plant.ToString("0.000")+" s (hit window "+sw.HitAt.ToString("0.00")+"-"+sw.HitUntil.ToString("0.00")+")");
                Check(!float.IsNaN(follow)&&follow<=end,what+" follows with its rear foot's recovery step (landing at "+follow.ToString("0.000")+" s)");
                // The hips drop into the plant: the pelvis at least 2 cm under its height as the swing began.
                float start=pl.Beats.Find(b=>b.T>=0).Hips,low=float.MaxValue;foreach(var b in pl.Beats)if(b.T>=plant-.05f&&b.T<=plant+.15f)low=Math.Min(low,b.Hips);
                float drop=start-low;dropLow=Math.Min(dropLow,drop);if(pl==plain)drops[spec]=drop;Check(drop>=.02f,what+" drops its hips "+(drop*100).ToString("0.0")+" cm into the plant (at least 2)");
            }
            float Most(Play pl){float m=float.MinValue;foreach(var b in pl.Beats)if(b.T>=s.HitAt&&b.T<=s.HitUntil)m=Math.Max(m,b.Pitch);return m;}
            float lean=Most(lunge)-Most(plain);leanLow=Math.Min(leanLow,lean);Check(lean>=6,spec+" opener leans "+lean.ToString("0.0")+" deg further than a default step through the hit window (at least 6)");
            // A swing entered from a sprint carries the run's momentum: the chest pitches on over the planted feet.
            var dash=Drive(data,spec,new[]{0f},sprint:true,until:1.2f);Feet(dash,spec+" sprint entry",Skid,SkidTotal);
            float carry=0,still=0;foreach(var b in dash.Beats)if(b.T>=.03f&&b.T<=.12f)carry+=b.Pitch;foreach(var b in plain.Beats)if(b.T>=.03f&&b.T<=.12f)still+=b.Pitch;float more=(carry-still)/6;
            Check(more>=4,spec+" sprint entry leans "+more.ToString("0.0")+" deg further than a standing swing over 0.03-0.12 s (at least 4)");
            // A swing that does not travel (a lock target within reach: no step; or the body held by a wall) keeps both soles
            // planted: no step, no slip.
            foreach(var (pl,what) in new[]{(Drive(data,spec,new[]{0f},1f,until:1.2f),"swing with no step"),(Drive(data,spec,new[]{0f},until:1.2f,wall:true),"swing into a wall")})
            {
                Check(pl.Swings[0].Distance<NativeMeleeFeel.DefaultStep(spec)+1e-3f,spec+" "+what+" setup");
                Check(pl.Flights.TrueForAll(fl=>fl.dist<.05f),spec+" "+what+" steps "+(pl.Flights.Count>0?pl.Flights.Max(fl=>fl.dist)*100:0).ToString("0")+" cm");
                Check(pl.Skate[0]<=.1f&&pl.Skate[1]<=.1f&&pl.Sink<=.005f,spec+" "+what+": the soles slip "+Math.Max(pl.Skate[0],pl.Skate[1]).ToString("0.00")+" m/s, sink "+(pl.Sink*100).ToString("0.0")+" cm");
            }
        }
        // The greatsword is the heaviest: its hips drop deeper into the plant than the sword's.
        Check(drops["greatsword"]>drops["sword"],"the greatsword drops its hips "+(drops["greatsword"]*100).ToString("0.0")+" cm, no deeper than the sword's "+(drops["sword"]*100).ToString("0.0"));
        notes.Add("openers at the cap, default steps, early chains and sprint entries keep every sole on the floor pinned (at most "+worstSkate.ToString("0.00")+" m/s about its slowest pivot, "+(worstSlide*100).ToString("0.0")+" cm in all, "+slideAt+"; at most "+(worstSink*1000).ToString("0")+" mm into the floor), every step clears it by at least "+(worstClear*100).ToString("0.0")+" cm ("+clearAt+"), the lead foot plants in the hit window, the rear foot follows in the recovery, the hips drop at least "+(dropLow*100).ToString("0.0")+" cm into the plant (the greatsword "+(drops["greatsword"]*100).ToString("0.0")+"), lunges lean at least "+leanLow.ToString("0")+" deg; swings with no step or into a wall stay planted");
        // Windup turning (NativeMeleeFeel.TurnRate toward an aim turned 60 deg through the first windup, then a chain from that
        // heading): the facing tracks the committed heading without lag and never turns faster than TurnFollow x the rate.
        {
            var pl=Drive(data,"sword",new[]{0,.2f},turnTo:60,turnAt:.03f,until:1.4f);float lag=0,fastest=0,limit=NativeMeleeFeel.TurnRate("sword")*MeleeMotion.TurnFollow/Deg*Dt*1.01f;
            for(int i=1;i<pl.Beats.Count;i++){var b=pl.Beats[i];if(b.Commit&&b.T>MeleeMotion.TurnBlend)lag=Math.Max(lag,Math.Abs(MeleeMath.Wrap(b.Yaw-b.CommitYaw)));if(b.T>0)fastest=Math.Max(fastest,Math.Abs(MeleeMath.Wrap(b.Yaw-pl.Beats[i-1].Yaw)));}
            Check(pl.Swings.Count==2&&pl.Swings[1].Chained,"turn drive chains");
            // Turning over a planted foot pivots it on its toes: the heel is up whenever the foot's heading turns on the floor.
            Feet(pl,"sword windup turn",Skid,SkidTotal);int flatTurns=0;
            for(int i=1;i<pl.Beats.Count;i++)for(int k=0;k<2;k++){var b=pl.Beats[i];if(b.T>0&&b.Planted[k]&&pl.Beats[i-1].Planted[k]&&Math.Abs(MeleeMath.Wrap(b.Heading[k]-pl.Beats[i-1].Heading[k]))*Deg>1.5f&&b.Heel[k]<.1f)flatTurns++;}
            Check(flatTurns==0,"a planted foot turns flat on the floor through the windup turn ("+flatTurns+" frames)");Check(lag*Deg<=1,"the facing lags the windup turn by "+(lag*Deg).ToString("0.0")+" deg (at most 1)");Check(fastest<=limit,"the facing turns "+(fastest*Deg/Dt).ToString("0")+" deg/s in a frame (at most "+(limit*Deg/Dt).ToString("0")+")");
            // A remote pilot's heading arrives at 20 Hz (36 deg steps of a 720 deg/s turn): the facing glides between them.
            var m=new MeleeMotion();var v=new MeleeView{Id="remote",Spec="sword",Commit=true,CommitAt=1,CommitUntil=3,LastAttack=1};float worst=0,trail=0,prev=0;
            for(int f=0;f<60;f++){float t=1+f*Dt;v.CommitYaw=Math.Min(10,f/3)*36/Deg;float y=m.PresentedYaw(v,t,0);if(f>0)worst=Math.Max(worst,Math.Abs(y-prev));trail=Math.Max(trail,Math.Abs(MeleeMath.Wrap(v.CommitYaw-y)));prev=y;if(f==59)Check(Math.Abs(MeleeMath.Wrap(v.CommitYaw-y))<1e-3f,"the facing reaches the replicated heading");}
            Check(worst<=limit&&trail*Deg<=37,"a 20 Hz heading: the facing steps "+(worst*Deg).ToString("0.0")+" deg per frame (at most "+(limit*Deg).ToString("0.0")+"), trailing "+(trail*Deg).ToString("0")+" deg");
            notes.Add("the facing follows a 720 deg/s windup turn within "+(lag*Deg).ToString("0.0")+" deg, at most "+(fastest*Deg/Dt).ToString("0")+" deg/s, into a chain from that heading; a 20 Hz heading glides at most "+(worst*Deg).ToString("0.0")+" deg per frame");
        }
        // Combo index across a buffered chain and a ComboUntil reset: cut A, the buffered cut B, then cut A again.
        foreach(var spec in new[]{"sword","daggers"})
        {
            var pl=Drive(data,spec,new[]{0,spec=="sword"?.2f:.12f,1.6f},until:2.4f);
            Check(pl.Swings.Count==3&&pl.Swings[1].Chained&&!pl.Swings[2].Chained&&pl.Swings[2].Index==1,spec+" combo drive: "+pl.Swings.Count+" swings, last index "+(pl.Swings.Count>2?pl.Swings[2].Index:0));
            string[] want={"cutA","cutB","cutA"};
            for(int i=0;i<3;i++){string seen=null;foreach(var b in pl.Beats)if(b.Swing==i+1&&b.T>=pl.Swings[i].At+.05f){seen=b.State;break;}Check(seen!=null&&seen.StartsWith(want[i]),spec+" swing "+(i+1)+" presents "+seen+" (expected "+want[i]+")");}
        }
        notes.Add("a buffered chain presents the next cut and a press after ComboUntil starts again from cut A");
        yield return "melee feel: "+string.Join("; ",notes);
    }
}
