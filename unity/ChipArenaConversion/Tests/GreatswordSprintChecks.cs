using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// The greatsword's sprint (owner 2026-10-15: "Fix sprint animation for greatsword (Utilize the normal run animation with sword
/// on ground, but lean forward abit to show a natural sprint movement)"; Core/NativeSprintMotion DragBlade, GuardSwing): over
/// the same pilot run at sprint speed without the sprint layer (the pack run's carry) at 60 Hz, 144 Hz and uneven frames:
///  - the run's legs, stride and cadence (the feet and the stride phase exactly the run's: nothing slides) and its low right
///    fist (the fist where the run holds it, the blade on the run's heading);
///  - the point dragged on the floor: the stretched tip DragTip over the floor guard's plane at every stride phase (so
///    MeleeCapture.KeepBladesOffFloor never takes the wrist: no flick), the wrist turning smoothly;
///  - the lean: the chest a little further forward than the run's, the gaze kept;
///  - the free left arm: the run's guard swung whole from the shoulder in step with the legs (the elbow keeps its bend);
///  - the blends into and out of the sprint and into Astra's sprint attack (its take, with the arena layers on) move the hands
///    and the point no more than the run's own entries do, and the point never dips through the floor guard's plane;
///  - first person: the view weapon dropped and tipped down instead of lifted onto the shoulder;
///  - the scrape (Core/NativeSprintFx.Scrape, Runtime/NativeSprintFxPresentation): faint hot sparks thrown back and up off the
///    floor at the dragged point while the rig shows the drag, thinned by Reduced effects and Low graphics, never under the floor.
/// Every measure is printed before any bound is checked.
/// </summary>
internal static class GreatswordSprintChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI,SprintSpeed=9.18f,RunSpeed=6.8f,Plane=MeleeClearance.FloorClear;
    private static readonly List<string> failures=new List<string>();
    private static void Check(bool ok,string what){if(!ok)failures.Add(what);}
    public static void Run(string root,ConversionContent data)
    {
        failures.Clear();var log=new List<string>();Steady(log);Blends(log);SprintAttack(data,log);View(log);Scrape(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine((failures.Count==0?"PASS":"MEASURE")+" greatsword sprint: "+line);
        if(failures.Count>0)throw new InvalidOperationException("Greatsword sprint: "+string.Join("; ",failures));
    }
    // A greatsword pilot travelling along its facing (+z), posed as the arena rigs are (pack locomotion, the jump layer and,
    // with `layer`, the sprint layer).
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly MeleeView V=new MeleeView{Id="7",Spec="greatsword",Hp=100};public readonly MeleeMotion Melee;
        public readonly NativeSprintMotion Layer;public Vector3 Root=new Vector3(0,.865f,0);private float last=-100;
        public Pilot(bool layer){Layer=layer?new NativeSprintMotion():null;Melee=new MeleeMotion{Impacts=new MeleeImpacts(),Locomotion=new NativeLocomotionCarry(),SprintLayer=Layer,JumpLayer=new NativeJumpMotion()};}
        public void Frame(float t,Vector3 vel,bool sprinting,bool grounded=true,float y=0)
        {
            float dt=last<-90?Dt:t-last;last=t;Root+=new Vector3(vel.X,0,vel.Z)*dt;Root.Y=.865f+y;
            V.Vel=vel;V.Grounded=grounded;V.Sprinting=sprinting;Melee.Rig.GroupPosition=Root;Melee.Rig.GroupYaw=Melee.PresentedYaw(V,t,V.Yaw);
            Melee.Sample(V,t,dt,r=>{});Melee.Rig.WritePose(Pose);Pose.World();
        }
        public Quaternion ChestQ=>Pose.WorldRotation[CapturedRig.Chest];
        public Vector3 InChest(Vector3 p)=>Vector3.Transform(p-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(ChestQ));
        public Quaternion HandInChest(int hand)=>Quaternion.Normalize(Quaternion.Inverse(ChestQ)*Pose.WorldRotation[hand]);
        // The stretched blade tip and the blade's direction (group space: y up, the floor guard's plane at FloorClear).
        public Vector3 Tip=>Pose.WorldPosition[CapturedRig.RHand]+Vector3.Transform(Melee.Rig.Reached(MeleeSet.For("greatsword").Tip),Pose.WorldRotation[CapturedRig.RHand]);
        public Vector3 Blade=>Vector3.Normalize(Tip-Pose.WorldPosition[CapturedRig.RHand]);
        public float ChestPitch{get{var u=Vector3.Transform(Vector3.UnitY,ChestQ);return (float)Math.Atan2(u.Z,u.Y)*Deg;}}
        public float HeadPitch{get{var u=Vector3.Transform(Vector3.UnitY,Pose.WorldRotation[CapturedRig.Head]);return (float)Math.Atan2(u.Z,u.Y)*Deg;}}
        public float Flex(bool left){int s=left?CapturedRig.LArm:CapturedRig.RArm;Vector3 a=Pose.WorldPosition[s+1]-Pose.WorldPosition[s],b=Pose.WorldPosition[s+2]-Pose.WorldPosition[s+1];return Angle(a,b);}
    }
    private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))))*Deg;
    private static float Angle(Vector3 a,Vector3 b)=>(float)Math.Atan2(Vector3.Cross(a,b).Length(),Vector3.Dot(a,b))*Deg;
    private static float Yaw(Vector3 d)=>(float)Math.Atan2(d.X,d.Z)*Deg;
    private static readonly int[] Legs={CapturedRig.LHip,CapturedRig.LHip+1,CapturedRig.LFoot,CapturedRig.LToe,CapturedRig.RHip,CapturedRig.RHip+1,CapturedRig.RFoot,CapturedRig.RToe};
    // ---- a steady sprint against the run at sprint speed ----
    private static void Steady(List<string> log)
    {
        string notes="";
        foreach(var (name,hz,jitter) in new[]{("60 Hz",60,false),("144 Hz",144,false),("uneven frames",144,true)})
        {
            var p=new Pilot(true);var run=new Pilot(false);var rng=new Random(5);float t=1,step=1f/hz;
            float lowTip=float.MaxValue,highTip=float.MinValue,legs=0,stride=0,fist=0,heading=0,wristStep=0,wristRate=0,flexL=0,lean=0,head=0,swingFore=float.MaxValue,swingAft=float.MinValue;int n=0;
            Quaternion? prev=null,prevRun=null;float runStep=0,runRate=0;var corr=new List<(float fist,float lead)>();float runGuard=0;
            while(t<6)
            {
                float dt=jitter?(float)(1/240.0+rng.NextDouble()*(1/40.0-1/240.0)):step;t+=dt;
                var vel=new Vector3(0,0,SprintSpeed);p.Frame(t,vel,true);run.Frame(t,vel,true);
                if(t<2.5f){prev=prevRun=null;continue;}
                Check(p.Layer.Carry==NativeSprintRules.Drag&&p.Layer.Weight>.999f,name+": the drag shows in a steady sprint");
                float tip=p.Tip.Y-Plane;lowTip=Math.Min(lowTip,tip);highTip=Math.Max(highTip,tip);
                foreach(int b in Legs)legs=Math.Max(legs,Vector3.Distance(p.Pose.WorldPosition[b],run.Pose.WorldPosition[b]));
                stride=Math.Max(stride,Math.Abs(p.Melee.Rig.Stride-run.Melee.Rig.Stride));
                fist=Math.Max(fist,Vector3.Distance(p.InChest(p.Pose.WorldPosition[CapturedRig.RHand]),run.InChest(run.Pose.WorldPosition[CapturedRig.RHand])));
                heading=Math.Max(heading,Math.Abs(MeleeMath.Wrap((Yaw(p.Blade)-Yaw(run.Blade))/Deg))*Deg);
                var q=p.HandInChest(CapturedRig.RHand);if(prev.HasValue){float a=Angle(prev.Value,q);wristStep=Math.Max(wristStep,a);wristRate=Math.Max(wristRate,a/dt);}prev=q;
                var qr=run.HandInChest(CapturedRig.RHand);if(prevRun.HasValue){float a=Angle(prevRun.Value,qr);runStep=Math.Max(runStep,a);runRate=Math.Max(runRate,a/dt);}prevRun=qr;
                flexL=Math.Max(flexL,Math.Abs(p.Flex(true)-run.Flex(true)));lean+=p.ChestPitch-run.ChestPitch;head+=p.HeadPitch-run.HeadPitch;n++;
                // The free fist against the run's guard, along the travel (group space), and the feet's lead (right ahead: +).
                float z=p.Pose.WorldPosition[CapturedRig.LHand].Z-run.Pose.WorldPosition[CapturedRig.LHand].Z;swingFore=Math.Max(swingFore==float.MaxValue?z:swingFore,z);swingAft=Math.Min(swingAft==float.MinValue?z:swingAft,z);
                corr.Add((p.Pose.WorldPosition[CapturedRig.LHand].Z,p.Pose.WorldPosition[CapturedRig.RFoot].Z-p.Pose.WorldPosition[CapturedRig.LFoot].Z));
                runGuard=Math.Max(runGuard,run.Tip.Y-Plane);
            }
            lean/=n;head/=n;float r=Correlation(corr);
            notes+=(notes.Length>0?"; ":"")+name+": point "+Cm(lowTip)+".."+Cm(highTip)+" over the plane (the run's held on it, at most "+Cm(runGuard)+"), feet and legs within "+Mm(legs)+" of the run's, stride phase "+stride.ToString("0.0e0")+" rad, the right fist within "+Mm(fist)+" and the blade's heading within "+heading.ToString("0.0")+" deg of the run's, the wrist at most "+wristStep.ToString("0.00")+" deg a frame ("+wristRate.ToString("0")+" deg/s; the run's, its point held on the plane by the floor guard, "+runStep.ToString("0.00")+", "+runRate.ToString("0")+"), lean +"+lean.ToString("0.0")+" deg (head +"+head.ToString("0.0")+"), the free fist "+Cm(swingAft)+".."+Cm(swingFore)+" fore of the run's guard in step (r "+r.ToString("0.00")+"), its elbow within "+flexL.ToString("0.0")+" deg";
            // The point rides the plane by DragTip, never under it (the floor guard would take the wrist) nor lifted off the floor.
            Check(lowTip>=NativeSprintMotion.DragTip-.008f&&highTip<=NativeSprintMotion.DragTip+.012f&&lowTip>.005f,name+": the point skims "+Cm(lowTip)+".."+Cm(highTip)+" over the floor guard's plane");
            Check(legs<.002f&&stride<1e-4f,name+": the run's legs and stride ("+Mm(legs)+", "+stride+" rad)");
            Check(fist<.002f&&heading<5,name+": the run's low right fist and the blade on its heading ("+Mm(fist)+", "+heading.ToString("0.0")+" deg)");
            Check(wristRate<=Math.Max(runRate*1.15f,runRate+20)&&wristStep<=runStep*1.15f+.3f,name+": the wrist turns no faster than the run's (no flick: "+wristStep.ToString("0.00")+" deg a frame, "+wristRate.ToString("0")+" deg/s; the run "+runStep.ToString("0.00")+", "+runRate.ToString("0")+")");
            Check(lean>=6&&lean<=16&&Math.Abs(head)<=lean,name+": a modest extra lean (+"+lean.ToString("0.0")+" deg, head +"+head.ToString("0.0")+")");
            Check(flexL<1&&swingAft>=-.01f&&swingFore>=.05f&&swingFore<=.18f&&r>.8f,name+": the free arm swings the run's guard in step ("+Cm(swingAft)+".."+Cm(swingFore)+", r "+r.ToString("0.00")+", elbow "+flexL.ToString("0.0")+" deg)");
        }
        log.Add("steady sprint against the run at sprint speed: "+notes);
    }
    private static float Correlation(List<(float a,float b)> xs)
    {
        float ma=0,mb=0;foreach(var x in xs){ma+=x.a;mb+=x.b;}ma/=xs.Count;mb/=xs.Count;float sab=0,saa=0,sbb=0;
        foreach(var x in xs){sab+=(x.a-ma)*(x.b-mb);saa+=(x.a-ma)*(x.a-ma);sbb+=(x.b-mb)*(x.b-mb);}return sab/(float)Math.Sqrt(Math.Max(1e-12f,saa*sbb));
    }
    private static string Cm(float m)=>(m*100).ToString("0.0")+" cm";
    private static string Mm(float m)=>(m*1000).ToString("0.0")+" mm";
    // ---- run to sprint to run, sprint start and stop ----
    private static void Blends(List<string> log)
    {
        string notes="";
        foreach(var (name,speed) in new (string,Func<float,(float v,bool s)>)[]{
            ("run, sprint, run",t=>t>=2&&t<3.5f?(SprintSpeed,true):(RunSpeed,false)),
            ("sprint from a standstill and stop",t=>t<2?(0f,false):t<2.3f?(SprintSpeed*(t-2)/.3f,true):t<3.5f?(SprintSpeed,true):t<3.75f?(SprintSpeed*(3.75f-t)/.25f,false):(0f,false))})
        {
            var p=new Pilot(true);var o=new Pilot(false);float hands=0,plain=0,wrist=0,plainWrist=0,low=float.MaxValue,tipStep=0,plainTip=0;Vector3[] last=null,lastO=null;Quaternion lq=default,lqO=default;
            for(int f=0;f<300;f++)
            {
                float t=1+f*Dt;var (v,s)=speed(t);var vel=new Vector3(0,0,v);p.Frame(t,vel,s);o.Frame(t,vel,s);
                var now=new[]{p.InChest(p.Pose.WorldPosition[CapturedRig.LHand]),p.InChest(p.Pose.WorldPosition[CapturedRig.RHand]),p.Tip};var nowO=new[]{o.InChest(o.Pose.WorldPosition[CapturedRig.LHand]),o.InChest(o.Pose.WorldPosition[CapturedRig.RHand]),o.Tip};
                var q=p.HandInChest(CapturedRig.RHand);var qO=o.HandInChest(CapturedRig.RHand);
                if(last!=null&&t>1.5f)
                {
                    hands=Math.Max(hands,Math.Max(Vector3.Distance(now[0],last[0]),Vector3.Distance(now[1],last[1])));plain=Math.Max(plain,Math.Max(Vector3.Distance(nowO[0],lastO[0]),Vector3.Distance(nowO[1],lastO[1])));
                    tipStep=Math.Max(tipStep,Vector3.Distance(now[2],last[2]));plainTip=Math.Max(plainTip,Vector3.Distance(nowO[2],lastO[2]));
                    wrist=Math.Max(wrist,Angle(lq,q));plainWrist=Math.Max(plainWrist,Angle(lqO,qO));
                }
                if(t>1.5f&&v>1)low=Math.Min(low,p.Tip.Y-Plane);
                last=now;lastO=nowO;lq=q;lqO=qO;
            }
            notes+=(notes.Length>0?"; ":"")+name+": the hands at most "+Cm(hands)+" a frame (the run alone "+Cm(plain)+"), the right wrist "+wrist.ToString("0.0")+" deg (the run "+plainWrist.ToString("0.0")+"), the point "+Cm(tipStep)+" a frame (the run "+Cm(plainTip)+"), never under the plane ("+Mm(low)+")";
            Check(hands<=plain+.012f&&wrist<=plainWrist+3&&tipStep<=plainTip+.03f,name+": no pop through the sprint's blends ("+Cm(hands)+" vs "+Cm(plain)+", "+wrist.ToString("0.0")+" vs "+plainWrist.ToString("0.0")+" deg, point "+Cm(tipStep)+" vs "+Cm(plainTip)+")");
            Check(low>=-.001f,name+": the point never dips through the floor guard's plane ("+Mm(low)+")");
        }
        log.Add("blends: "+notes);
    }
    // ---- into Astra's sprint attack (NativeSprintAttack: its step, timing and the greatsword's hop) ----
    private static void SprintAttack(ConversionContent data,List<string> log)
    {
        var w=data.Weapon("greatsword");var stats=NativeContentRules.Stats(data,"greatsword",new Dictionary<string,int>());float rate=(float)stats["attackRate"];
        bool dragged=false;
        float[] Entry(bool layer,out float strike,out string state)
        {
            var p=new Pilot(layer);const float T0=2,Gravity=23;NativeMeleeSwing swing=null;float end=0,vy=0,y=0;bool grounded=true;float hands=0,tip=0;strike=0;state="";Vector3[] last=null;
            for(int f=0;f*Dt<T0+1.4f;f++)
            {
                float t=f*Dt;
                if(swing==null&&Math.Abs(t-T0)<Dt*.5f){swing=NativeMeleeFeel.Begin(w,stats,t,1,0);NativeSprintAttack.Apply(swing,rate);swing.Until=Math.Max(swing.Until,t+(float)((w.windup+.28)/rate));end=swing.Until;if(NativeSprintAttack.Hop(swing)){vy=NativeSprintAttack.HopSpeed(swing,Gravity);grounded=false;}}
                bool live=swing!=null&&t<end;var st=live?NativeMeleeRules.Step(swing,t,Dt):new V3();bool run=swing==null;var v=p.V;
                if(swing!=null)
                {
                    v.LastAttack=swing.At;v.Windup=swing.HitAt-swing.At;v.AttackIndex=swing.Index;bool window=t<=swing.HitUntil;v.SwingAt=window?swing.HitAt:float.NaN;v.SwingUntil=window?swing.HitUntil:float.NaN;
                    v.Commit=live;if(live){v.CommitAt=swing.At;v.CommitUntil=end;v.CommitYaw=0;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}v.SprintAttack=live&&swing.SprintAttack;
                }
                p.Frame(t,new Vector3(-(float)st.x,vy,(run?SprintSpeed:0)+(float)st.z),run,grounded,y);
                if(!grounded){vy-=Gravity*Dt;y+=vy*Dt;if(y<=0){y=0;vy=0;grounded=true;}}
                var now=new[]{p.InChest(p.Pose.WorldPosition[CapturedRig.LHand]),p.InChest(p.Pose.WorldPosition[CapturedRig.RHand]),p.Tip};
                if(last!=null&&t>=T0-.3f)
                {
                    float hs=Math.Max(Vector3.Distance(now[0],last[0]),Vector3.Distance(now[1],last[1])),ts=Vector3.Distance(now[2],last[2]);
                    // The entry (from the press to the strike's lead-in) against the whole swing's fastest frame.
                    if(t<T0+.12f){hands=Math.Max(hands,hs);tip=Math.Max(tip,ts);}
                    if(t>=T0)strike=Math.Max(strike,ts);
                }
                if(swing!=null&&t<T0+.1f)state=p.Melee.State;last=now;if(layer&&swing==null&&t>T0-.05f)dragged=p.Layer.Carry==NativeSprintRules.Drag;
            }
            return new[]{hands,tip};
        }
        var on=Entry(true,out float strike,out string shown);var off=Entry(false,out float strikeOff,out _);
        Check(dragged&&shown.StartsWith("sprint attack"),"the sprint attack plays its take from the drag ("+shown+", dragging before the press: "+dragged+")");
        Check(on[0]<=off[0]+.015f&&on[1]<=off[1]+.03f&&on[1]<strike,"the sprint attack takes over from the drag without a pop (the hands "+Cm(on[0])+" a frame, the run's entry "+Cm(off[0])+"; the point "+Cm(on[1])+", the run's "+Cm(off[1])+", the strike's fastest "+Cm(strike)+")");
        log.Add("into the sprint attack ("+shown+"): over the entry the hands move at most "+Cm(on[0])+" a frame (entered from the run at sprint speed "+Cm(off[0])+") and the point "+Cm(on[1])+" ("+Cm(off[1])+"); the strike's fastest point frame "+Cm(strike)+" ("+Cm(strikeOff)+")");
    }
    // ---- the scrape sparks ----
    private static void Scrape(List<string> log)
    {
        var tip=new Vector3(3,.11f,5);var vel=new Vector3(0,0,SprintSpeed);int shown=0;float low=10,high=-10,ahead=-10,hottest=0;
        for(float t=0;t<3;t+=1/90f)for(int k=0;k<NativeSprintFx.Scrapes;k++)
        {
            Check(!NativeSprintFx.Scrape(k,9,t,0,tip,vel,0,out _,out _,out _),"no sparks without the drag");
            if(!NativeSprintFx.Scrape(k,9,t,1,tip,vel,0,out var head,out var motion,out float heat))continue;shown++;
            low=Math.Min(low,head.Y);high=Math.Max(high,head.Y);ahead=Math.Max(ahead,head.Z-tip.Z);hottest=Math.Max(hottest,heat);
            Check(heat>0&&heat<=1&&MathUtil.Finite(motion.X+motion.Y+motion.Z),"a spark's heat and motion are bounded");
        }
        Check(shown>0&&low>=.01f-1e-5f&&high<.5f&&ahead<=1e-4f,"sparks fly off the floor at the point and fall behind it ("+shown+" samples, "+Cm(low)+".."+Cm(high)+" up, never ahead of the point: "+Cm(ahead)+")");
        Check(NativeSprintFx.ScrapeContact(.11f)>.999f&&NativeSprintFx.ScrapeContact(NativeSprintFx.ScrapeReach)<1e-3f,"the scrape fades as the point lifts off the floor");
        Check(NativeSprintFx.Count(NativeSprintFx.Scrapes,NativeSprintFx.ReducedScrapes,1,false)==NativeSprintFx.Scrapes&&NativeSprintFx.Count(NativeSprintFx.Scrapes,NativeSprintFx.ReducedScrapes,.3f,false)==NativeSprintFx.ReducedScrapes&&NativeSprintFx.Count(NativeSprintFx.Scrapes,NativeSprintFx.ReducedScrapes,1,true)<NativeSprintFx.Scrapes,"Reduced effects and Low graphics thin the scrape");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)NativeSprintFx.Scrape(i%NativeSprintFx.Scrapes,9,i/90f,1,tip,vel,0,out _,out _,out _);
        Check(GC.GetAllocatedBytesForCurrentThread()==before,"the scrape allocates nothing");
        log.Add("the scrape: "+shown+" spark samples over 3 s ("+NativeSprintFx.Scrapes+" sparks, "+NativeSprintFx.ReducedScrapes+" with Reduced effects, fewer on Low), "+Cm(low)+" to "+Cm(high)+" over the floor, behind the point, fading as the point lifts, none without the drag, no allocation");
    }
    // ---- hooks ----
    private static void Hooks(string root,List<string> log)
    {
        string Read(string f)=>System.IO.File.ReadAllText(System.IO.Path.Combine(root,f));string fx=Read("Assets/ChipArena/Runtime/NativeSprintFxPresentation.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs"),sm=Read("Assets/ChipArena/Core/NativeSprintMotion.cs");
        Check(fx.Contains("if(drag!=null&&drag(a,now,out var point))")&&fx.Contains("FirstPerson,dragPoint??=DragPoint);")&&fx.Contains("rig.SprintCarry!=NativeSprintRules.Drag")&&ca.Contains("public string SprintCarry=>sprint?.Carry??NativeSprintRules.None;"),"the scrape reads the arena rig's drag and blade tip");
        Check(sm.Contains("LeanSpine(r,p.Spec,w);DragBlade(r,set,w);GuardSwing(")&&!sm.Contains("ClearBlade(")&&!sm.Contains("SupportHand("),"the greatsword sprint keeps the run's arms (no shoulder brace, no support hand)");
        foreach(var f in new[]{"Assets/ChipArena/Core/ArenaWorld.cs","Assets/ChipArena/Core/NativePrediction.cs","Assets/ChipArena/Core/NativeMeleeRules.cs","Assets/ChipArena/Core/NativeSprintAttack.cs"})Check(!Read(f).Contains("DragBlade")&&!Read(f).Contains("NativeSprintFx"),f+" has no drag presentation dependency");
        log.Add("hooks: the arena rigs' drag carry and blade tip feed the scrape; the sprint keeps the run's arms; simulation, prediction and the sprint attack rules untouched");
    }
    // ---- first person ----
    private static void View(List<string> log)
    {
        NativeSprintRules.ViewCarry("greatsword",out float drop,out float pitch,out float roll,out float bob);NativeSprintRules.ViewCarry("sword",out float sDrop,out float sPitch,out _,out _);
        Check(drop>=sDrop&&drop<=.09f&&pitch<sPitch&&pitch>-.45f&&Math.Abs(roll)<=.2f&&bob>0&&bob<=.6f,"first person: the view greatsword dropped ("+drop+" m) and tipped down ("+pitch+" rad), lower than the sword ("+sDrop+", "+sPitch+")");
        var a=new Actor{Id=3,SourceSpec="greatsword",Weapon=WeaponId.Sword,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,SprintSpeed)};var b=new Actor{Id=3,SourceSpec="greatsword",Weapon=WeaponId.Sword,Hp=100,Grounded=true,Velocity=new V3(0,0,SprintSpeed)};
        var on=new NativeViewSway();var off=new NativeViewSway();NativeViewSwayPose po=default,pf=default;
        for(int f=0;f<40;f++){po=on.Sample(NativeViewSway.Input(a,2+f*Dt,0,0,0));pf=off.Sample(NativeViewSway.Input(b,2+f*Dt,0,0,0));}
        Check(po.Pitch-pf.Pitch<-.25f&&po.Y-pf.Y<-.04f,"first person: the sprinting view greatsword sits lower and tips down ("+(po.Y-pf.Y).ToString("0.000")+" m, "+(po.Pitch-pf.Pitch).ToString("0.00")+" rad)");
        log.Add("first person: the view greatsword drops "+(drop*100).ToString("0.0")+" cm and tips "+(-pitch*Deg).ToString("0")+" deg down (the sword "+(sDrop*100).ToString("0.0")+" cm, "+(-sPitch*Deg).ToString("0")+" deg), measured "+((pf.Y-po.Y)*100).ToString("0.0")+" cm and "+((pf.Pitch-po.Pitch)*Deg).ToString("0")+" deg under the run's view");
    }
}
