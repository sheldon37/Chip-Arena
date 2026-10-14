#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The Hulk's charge at a pilot, presentation side (owner 2026-10-14, Windows test build: "The hulk needs to punch and charge at
    // the player too, not just the wall. The charge is avoidable if they move out of the way in time ... They literally can charge
    // from across the map to the player breaking the walls if they are hunting them and 'know' where they are."). Contract v2 is
    // Astra's to confirm (docs/SIREN-ZOMBIES.md 10.5); these check the presentation built against it: a run held at any length on
    // planted feet, the heading locked from the yaw, the crash-through flinch, the warning for the hunted pilot only with an
    // urgency that never falls as the distance closes, the marker clear of the crosshair, the event fallback, the lane, no
    // allocation, and runtime files that only draw.
    internal static class HulkChargeChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hulk charge: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private const int K=NativeHordeKinds.Hulk;
        private static readonly float Sc=NativeHordeKinds.Scale[K];
        public static void Run(string root)
        {
            Contract();LongRun();Lock();Flinch();Warning();Marker();Events();Lane();Allocation();Runtime(root);
            Console.WriteLine("PASS hulk charge ("+count+" checks: contract v2 numbers and the proposed move choice, a cross-map run held at 4.5 / 6 / 7.5 m/s for 30 s on planted feet with the lowered shoulder, the heading locked from the yaw at the wind-up's end, the crash-through flinch on a break in the run, the warning only for the hunted pilot with urgency never falling as it closes, the HUD marker clear of the crosshair, the event and frame-field fallbacks, the lane and the walls' tremble, no allocation, presentation-only runtime)");
        }
        private static HordeMotionInput Input(int id)=>new HordeMotionInput{Kind=K,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Root=new Vector3(0,NativeHordeKinds.Root[K],0),Attack=NativeHordeKinds.Attack.Smash,Side=1};

        // ---- contract v2 (proposed) and the move choice ----
        private static void Contract()
        {
            var st=NativeHordeKinds.ProposedRow(K);
            Check(NativeHulkMoves.ChargeSpeed>st.run*3&&NativeHulkMoves.ChargeSpeed<6.8f,"the run is far past the Hulk's run and under a sprinting pilot's 6.8 m/s ("+NativeHulkMoves.ChargeSpeed+")");
            Check(NativeHulkMoves.ChargeSpeed*NativeHulkMoves.ChargeRun>=80,"the longest run crosses Ward Zero's 80 m ("+NativeHulkMoves.ChargeSpeed*NativeHulkMoves.ChargeRun+" m)");
            Check(NativeHulkMoves.ChargeMinRange>=NativeHulkMoves.PunchReach+1&&NativeHulkMoves.ChargeOvershoot>0&&NativeHulkMoves.ChargeOvershoot<8&&NativeHulkMoves.ChargeMemory>0&&NativeHulkMoves.ChargeCooldown>NativeHulkMoves.ChargeWind,"range, overshoot, memory and cooldown are sane");
            Check(NativeHulkMoves.ChargeSlow>0&&NativeHulkMoves.ChargeSlow<1&&NativeHulkMoves.ChargeSlowFor>0&&NativeHulkMoves.ChargeSlowFor<1,"a crash-through slows the run briefly, never stops it");
            // A pilot side-stepping from the lane's centre clears it (the Hulk's .95 m and their .45 m) at a sprint well inside the run from 7 m.
            float clear=NativeHordeKinds.WorldRadius(K)+.45f,dodge=clear/6.8f,run=NativeHulkMoves.ChargeMinRange/NativeHulkMoves.ChargeSpeed;
            Check(dodge<run*.5f,"the shortest charge leaves time to leave the lane after the lock ("+dodge.ToString("0.00")+" s against "+run.ToString("0.00")+" s)");
            Check(NativeHulkMoves.Choose(2.5f,0,1,false,0)==NativeHulkMoves.Move.Punch,"the hunted pilot straight ahead at reach: the punch");
            Check(NativeHulkMoves.Choose(.8f,0,1,false,0)==NativeHulkMoves.Move.Swipe,"too close for the punch: the swipe");
            Check(NativeHulkMoves.Choose(2.5f,50,1,false,0)==NativeHulkMoves.Move.Swipe&&NativeHulkMoves.Choose(2.5f,-70,1,false,0)==NativeHulkMoves.Move.Swipe,"wide of the cone: the swipe");
            Check(NativeHulkMoves.Choose(2.5f,0,2,false,0)==NativeHulkMoves.Move.Content,"two or more in the disc: the smash");
            Check(NativeHulkMoves.Choose(3.3f,60,1,false,0)==NativeHulkMoves.Move.Content&&NativeHulkMoves.Choose(2.5f,100,1,false,0)==NativeHulkMoves.Move.Content,"out of both: the smash");
            Check(NativeHulkMoves.Choose(2.5f,0,1,true,0)==NativeHulkMoves.Move.Content&&NativeHulkMoves.Choose(2.5f,0,1,false,2|4)==NativeHulkMoves.Move.Content,"crawling or armless: the content's attack");
            Check(NativeHulkMoves.InPunch(0,2,0,.45f)&&NativeHulkMoves.InPunch(.9f,3,0,.45f)&&!NativeHulkMoves.InPunch(1.4f,2,0,.45f)&&!NativeHulkMoves.InPunch(0,-1,0,.45f)&&!NativeHulkMoves.InPunch(0,2,3,.45f),"the punch capsule along the facing");
            Check(NativeHulkMoves.InSwipe(-2,1,0,.45f)&&NativeHulkMoves.InSwipe(2,1,0,.45f)&&!NativeHulkMoves.InSwipe(0,-2,0,.45f)&&!NativeHulkMoves.InSwipe(0,4,0,.45f),"the swipe sector about the facing");
        }
        // ---- a cross-map run: held, planted, the lowered shoulder, at the run's real speed ----
        private static void LongRun()
        {
            var pose=new CapturedRig.Pose();float lean0=float.NaN,lean1=float.NaN;
            foreach(float speed in new[]{4.5f,NativeHulkMoves.ChargeSpeed,7.5f})
            {
                var g=new NativeHordeGait();var f=Input(96500);var c=NativeHulkCharge.State.None;c.Start(1,3);var last=new Vector3[2];var air=new bool[2];
                float worst=0,worstDrawn=0,z=0,first=-1,lastStep=0,leanMin=9,leanMax=-9;int steps=0;
                for(int i=0;i<60*33;i++)
                {
                    float t=i/60f;bool run=t>=1+NativeHulkMoves.ChargeWind;float v=run?speed:0;z+=v/60;
                    f.Time=t;f.Velocity=new Vector3(0,0,v/Sc);f.Root=new Vector3(0,NativeHordeKinds.Root[K],z/Sc);f.Yaw=0;f.Ground=0;
                    var ph=c.Step(t,0,v,new Vector3(0,0,z),out float age,out float endAge);
                    f.Charging=ph!=NativeHulkMoves.ChargePhase.None;f.ChargeAge=age;f.ChargeEnded=false;f.ChargeHow=NativeHulkMoves.ChargeEnd.Running;
                    g.Pose(f,pose);pose.World();
                    if(t>=1.1f&&t<1+NativeHulkMoves.ChargeWind)Check(ph==NativeHulkMoves.ChargePhase.Wind,"the wind-up before the run");
                    if(t>=3){Check(ph==NativeHulkMoves.ChargePhase.Run&&g.Move==NativeHordeGait.Keyed.ChargeRun,speed+" m/s: the run holds the lowered shoulder at "+t.ToString("0.0")+" s (no authored span ends it)");
                        float lean=Vector3.Transform(Vector3.UnitY,pose.WorldRotation[CapturedRig.Chest]).Z;leanMin=Math.Min(leanMin,lean);leanMax=Math.Max(leanMax,lean);}
                    for(int j=0;j<2;j++)
                    {
                        var drawn=g.ToWorld(pose.WorldPosition[j==0?CapturedRig.LFoot:CapturedRig.RFoot],f.Root);
                        if(t>4&&!air[j]&&!g.FootAir[j]){worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));worstDrawn=Math.Max(worstDrawn,new Vector2(drawn.X-g.FootWorld[j].X,drawn.Z-g.FootWorld[j].Z).Length());}
                        if(t>4&&(g.Landed&(1<<j))!=0){steps++;if(first<0)first=t;lastStep=t;}last[j]=g.FootWorld[j];air[j]=g.FootAir[j];
                    }
                }
                Check(worst<1e-4f,speed+" m/s for 30 s: a planted foot never slides (worst "+worst+" per frame)");
                Check(worstDrawn<.012f,speed+" m/s: the drawn ankle of a planted foot stays on its spot ("+worstDrawn.ToString("0.0000")+" rig m)");
                float cadence=(steps-1)/Math.Max(1e-3f,lastStep-first),want=2*(speed/Sc)/NativeHordeGait.Stride(K,speed/Sc);
                Check(steps>60&&Math.Abs(cadence-want)/want<.08f,speed+" m/s: the stride's cadence held over the whole run ("+cadence.ToString("0.00")+" against "+want.ToString("0.00")+"/s)");
                Check(leanMin>.15f,speed+" m/s: the chest stays lowered into the run ("+leanMin.ToString("0.00")+".."+leanMax.ToString("0.00")+")");
                if(float.IsNaN(lean0)){lean0=leanMin;lean1=leanMax;}else Check(Math.Abs(leanMin-lean0)<.05f&&Math.Abs(leanMax-lean1)<.05f,speed+" m/s: the same lowered shoulder and lean as at the other run speeds (no drift with the speed)");
            }
        }
        // ---- the heading locks from the replicated yaw at the wind-up's end ----
        private static void Lock()
        {
            var c=NativeHulkCharge.State.None;c.Start(10,4);float yawAtLock=float.NaN;int iAtLock=-1;
            for(int i=0;i<=300;i++)
            {
                // The yaw keeps turning (the wind-up tracks the target; later the replicated yaw is ignored by the lane).
                float t=10+i/60f,yaw=20+60*(i/60f);var ph=c.Step(t,yaw,5,new Vector3(i,0,0),out _,out _);
                if(ph==NativeHulkMoves.ChargePhase.Wind)Check(!c.Locked,"not locked in the wind-up");
                if(ph==NativeHulkMoves.ChargePhase.Run&&iAtLock<0){iAtLock=i;yawAtLock=yaw;Check(Math.Abs(t-10-NativeHulkMoves.ChargeWind)<1/60f+1e-4f,"the run starts at the wind-up's end");}
            }
            Check(c.Locked&&c.LockYaw==yawAtLock,"the heading is the yaw on the first frame past the wind-up ("+c.LockYaw+")");
            Check(Math.Abs(c.LockAt.X-iAtLock)<1e-3f&&Math.Abs(c.LockedAt-(10+iAtLock/60f))<1e-4f,"locked where and when it stood at the wind-up's end");
            Check(Math.Abs(20+60*(300/60f)-c.LockYaw)>200,"a later yaw never moves it");
            // A later yaw (the run) never moves it; the lane is that heading.
            var h=NativeHulkCharge.Heading(c.LockYaw);Check(Math.Abs(h.Length()-1)<1e-4f&&Math.Abs(Math.Atan2(h.X,h.Y)*180/Math.PI-c.LockYaw)<.01f,"the lane runs along the locked yaw");
            // Seen first mid-run (a late join): locked on that frame.
            var late=NativeHulkCharge.State.None;late.Start(0,4);late.Step(3,77,6,Vector3.Zero,out _,out _);Check(late.Locked&&late.LockYaw==77,"first seen mid-run, the heading is the yaw then");
            // The presented facing settles on the locked heading during the run (the gait turns to it, then holds).
            var g=new NativeHordeGait();var f=Input(96600);var pose=new CapturedRig.Pose();float z=0;
            for(int i=0;i<240;i++){float t=i/60f;bool run=t>=NativeHulkMoves.ChargeWind;float v=run?6:0;z+=v/60;f.Time=t;f.Charging=true;f.ChargeAge=t;f.Yaw=run?35:t*30;f.Velocity=run?new Vector3((float)Math.Sin(35*Math.PI/180),0,(float)Math.Cos(35*Math.PI/180))*v/Sc:Vector3.Zero;f.Root=new Vector3(0,NativeHordeKinds.Root[K],z/Sc);g.Pose(f,pose);}
            Check(Math.Abs(g.Yaw-35)<1,"the drawn body runs on the locked heading ("+g.Yaw+")");
        }
        // ---- a wall crashed through on the run: the flinch, and the run goes on ----
        private static void Flinch()
        {
            Check(NativeHulkCharge.Flinch(-.01f)==0&&NativeHulkCharge.Flinch(.5f)==0&&NativeHulkCharge.Flinch(.05f)>.9f,"the flinch is sharp and brief");
            for(float a=0;a<.5f;a+=.01f)Check(NativeHulkCharge.Flinch(a)>=0&&NativeHulkCharge.Flinch(a)<=1.0001f,"the flinch is bounded");
            var pose=new CapturedRig.Pose();float breakAt=4;Vector3[] A=new Vector3[400],B=new Vector3[400];var air=new bool[2];var last=new Vector3[2];float worst=0;
            for(int pass=0;pass<2;pass++)
            {
                var g=new NativeHordeGait();var f=Input(96700);float z=0;
                for(int i=0;i<400;i++)
                {
                    float t=i/60f;bool run=t>=NativeHulkMoves.ChargeWind;float v=run?(t>=breakAt&&t<breakAt+NativeHulkMoves.ChargeSlowFor?NativeHulkMoves.ChargeSpeed*NativeHulkMoves.ChargeSlow:NativeHulkMoves.ChargeSpeed):0;z+=v/60;
                    f.Time=t;f.Charging=true;f.ChargeAge=t;f.Velocity=new Vector3(0,0,v/Sc);f.Root=new Vector3(0,NativeHordeKinds.Root[K],z/Sc);f.ChargeBroke=pass==1&&t>=breakAt;f.ChargeBreakAge=t-breakAt;
                    g.Pose(f,pose);pose.World();(pass==0?A:B)[i]=pose.WorldPosition[CapturedRig.RArm];
                    if(pass==1&&t>breakAt-.1f)for(int j=0;j<2;j++){if(t>breakAt-.1f+1.5f/60&&!air[j]&&!g.FootAir[j])worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));last[j]=g.FootWorld[j];air[j]=g.FootAir[j];}
                    if(pass==1&&t>breakAt)Check(g.Move==NativeHordeGait.Keyed.ChargeRun,"it keeps running through the wall");
                }
            }
            int b0=(int)(breakAt*60);float back=0;for(int i=b0;i<b0+15;i++)back=Math.Max(back,A[i].Z-B[i].Z);
            Check(back>=.04f,"the leading shoulder is driven back by the wall ("+back.ToString("0.000")+" rig m)");
            Check(Vector3.Distance(A[b0+45],B[b0+45])<.06f&&Vector3.Distance(A[b0-2],B[b0-2])<1e-5f,"the run's pose again by .75 s, untouched before the break");
            Check(worst<1e-4f,"the feet stay planted through the flinch and the slow-down ("+worst+")");
            // A break in the run is a crash-through (no knock); one before the lock or after the end is not.
            var c=NativeHulkCharge.State.None;c.Start(10,1);
            Check(c.RanThrough(11.5f)&&!c.RanThrough(10.5f),"a break after the lock, not in the wind-up");c.End(14,NativeHulkMoves.ChargeEnd.Spent);
            Check(c.RanThrough(13.9f)&&!c.RanThrough(15),"none after its end");
        }
        // ---- the warning: the hunted pilot only; urgency never falling as it closes ----
        private static void Warning()
        {
            var hulk=new Vector3(0,0,30);var pilot=Vector3.Zero;var vel=new Vector3(0,0,-6);var head=new Vector2(0,-1);
            Check(!NativeHulkCharge.Warn(2,3,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,hulk,2.2f,vel,head,pilot,1,out _),"a pilot it does not hunt gets no warning");
            Check(!NativeHulkCharge.Warn(2,0,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,hulk,2.2f,vel,head,pilot,1,out _)&&!NativeHulkCharge.Warn(0,0,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,hulk,2.2f,vel,head,pilot,1,out _),"no hunted seat: no warning");
            Check(NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Wind,.3f,float.NaN,hulk,2.2f,Vector3.Zero,head,pilot,1,out var ww)&&ww.Active&&!ww.Running,"the hunted pilot is warned from the wind-up");
            Check(NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,hulk,2.2f,vel,head,pilot,1,out var w)&&w.Running&&w.InLane&&Math.Abs(w.Distance-30)<1e-3f&&Math.Abs(w.Closing-6)<1e-3f&&Math.Abs(w.Toward.Z-1)<1e-4f,"running at them: distance, closing, the direction toward it, in its lane");
            Check(NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,hulk,2.2f,vel,head,new Vector3(3,0,0),1,out var wo)&&!wo.InLane,"three metres off the lane: out of it");
            Check(NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Stop,3,.2f,hulk,2.2f,Vector3.Zero,head,pilot,1,out var wf)&&wf.Fade<1&&wf.Fade>0&&!NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Stop,3,.5f,hulk,2.2f,Vector3.Zero,head,pilot,1,out _),"after its end the warning fades out within .45 s");
            Check(!NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.None,0,float.NaN,hulk,2.2f,vel,head,pilot,1,out _),"no charge, no warning");
            foreach(bool running in new[]{false,true})foreach(float closing in new[]{0f,1,4.5f,6,7.5f})
            {
                float prev=-1;
                for(float d=80;d>=0;d-=.05f){float u=NativeHulkCharge.Urgency(d,closing,running);Check(u>=0&&u<=1,"urgency in 0..1");Check(u>=prev-1e-6f,"urgency never falls as the distance closes ("+(running?"run":"wind")+", closing "+closing+", at "+d.ToString("0.00")+" m)");prev=u;}
            }
            for(float d=0;d<70;d+=.5f){float prev=-1;for(float c=0;c<10;c+=.1f){float u=NativeHulkCharge.Urgency(d,c,true);Check(u>=prev-1e-6f,"urgency never falls as the closing speed rises");prev=u;}}
            Check(NativeHulkCharge.Urgency(60,6,true)<.5f&&NativeHulkCharge.Urgency(3,6,true)>.95f,"far is calm, a second from contact is the most");
            // Several Hulks after one pilot: the most urgent.
            NativeHulkCharge.Warn(3,3,1,NativeHulkMoves.ChargePhase.Run,2,float.NaN,new Vector3(0,0,8),2.2f,vel,head,pilot,1,out var nearW);Check(NativeHulkCharge.Over(nearW,w)&&!NativeHulkCharge.Over(w,nearW),"the nearer charge is the one shown");
            Check(NativeHulkCharge.CloseShake(0)==0&&NativeHulkCharge.CloseShake(1)<=.013f&&NativeHulkCharge.CloseShake(.9f)>NativeHulkCharge.CloseShake(.3f),"the closing shake is gentle and grows");
            Check(NativeHulkCharge.RumbleGain(0)==0&&NativeHulkCharge.RumbleGain(1)<=1&&NativeHulkCharge.RumbleGain(.8f)>NativeHulkCharge.RumbleGain(.3f),"the rumble grows with urgency");
        }
        // ---- the HUD marker: toward the Hulk, never on the crosshair ----
        private static void Marker()
        {
            int drawn=0;
            foreach(float aspect in new[]{16/9f,21/9f,4/3f})for(float vx=-.6f;vx<=1.6f;vx+=.04f)for(float vy=-.6f;vy<=1.6f;vy+=.04f)foreach(float vz in new[]{-5f,5f})foreach(float half in new[]{.01f,.06f,.2f,.5f})
            {
                var m=NativeHulkCharge.Layout(vx,vy,vz,half,aspect,.9f,1.3f,false);
                if(m.OnScreen&&!m.Ring&&m.Corners==0)continue;
                Check(NativeHulkCharge.FromCenter(m,aspect)>=NativeHulkCharge.AimClear-1e-4f,"nothing drawn on the crosshair ("+vx.ToString("0.00")+", "+vy.ToString("0.00")+", "+vz+")");drawn++;
                Check(m.X>=-.01f&&m.X<=1.01f&&m.Y>=-.01f&&m.Y<=1.01f,"the marker stays on the screen");
                if(!m.OnScreen&&vz>0)
                {
                    // The arrow points from the screen centre toward where the Hulk projects.
                    float ax=(vx-.5f)*aspect,ay=vy-.5f;double want=Math.Atan2(ay,ax),diff=Math.Abs(Math.IEEERemainder(m.Angle-want,2*Math.PI));Check(diff<1e-3,"the arrow points toward it");
                }
            }
            Check(drawn>1000,"markers were tested ("+drawn+")");
            var behind=NativeHulkCharge.Layout(.5f,.5f,-3,.1f,16/9f,.5f,0,false);Check(!behind.OnScreen&&behind.Y<.5f,"dead behind: the arrow points down (turn round)");
            for(float vx=-1;vx<=2;vx+=.05f)for(float vy=-1;vy<=2;vy+=.05f){var bh=NativeHulkCharge.Layout(vx,vy,-2,.1f,16/9f,.5f,0,false);Check(!bh.OnScreen&&bh.Y<.5f,"behind: always downward (turn round)");}
            var bl=NativeHulkCharge.Layout(.8f,.6f,-2,.1f,16/9f,.5f,0,false);Check(bl.X<.5f,"behind and to the left (mirrored projection): toward the left");
            var close=NativeHulkCharge.Layout(.52f,.5f,5,.14f,16/9f,.8f,0,false);Check(close.OnScreen&&close.Corners==15&&close.Size<=.14f*.62f*NativeHulkCharge.GrowMost+1e-4f,"close and on the crosshair: the bracket opens out of the clear circle, all four corners kept");
            var centred=NativeHulkCharge.Layout(.5f,.5f,4,.02f,16/9f,.5f,0,false);Check(centred.OnScreen&&centred.Ring,"a small Hulk on the crosshair: only the arcs outside the clear circle");
            var a=NativeHulkCharge.Layout(.5f,.5f,-3,.1f,16/9f,1,0,true);var b=NativeHulkCharge.Layout(.5f,.5f,-3,.1f,16/9f,1,.37f,true);Check(a.Alpha==b.Alpha&&a.Pulse==1,"Reduced effects: no pulse");
            var lo=NativeHulkCharge.Layout(-.3f,.5f,4,.1f,16/9f,.2f,0,true);var hi=NativeHulkCharge.Layout(-.3f,.5f,4,.1f,16/9f,.95f,0,true);Check(hi.Size>lo.Size&&hi.Alpha>lo.Alpha&&hi.Chevrons>lo.Chevrons,"more urgent: larger, brighter, deeper");
        }
        // ---- the event fallback (and the frame fields) ----
        private static void Events()
        {
            var c=NativeHulkCharge.State.None;Check(!c.Known,"none at first");
            Check(c.Event(NativeHulkMoves.ChargeEvent,3,5)&&c.Known&&c.ChargeAt==5&&c.Target==3,"hulk-charge starts it for the hunted seat (Amount)");
            Check(!c.Event("hulk-move",1,5)&&!c.Event("minion-slam",0,5),"other details are not the charge's");
            c.Event(NativeHulkMoves.ChargeEvent,3,5.01f);Check(c.ChargeAt==5,"a repeated start is the same charge");
            Check(c.Step(5.5f,10,0,Vector3.Zero,out _,out _)==NativeHulkMoves.ChargePhase.Wind&&c.Step(7,10,6,Vector3.Zero,out _,out _)==NativeHulkMoves.ChargePhase.Run,"wind-up, then the run");
            c.Event(NativeHulkMoves.ChargeEndEvent,2,8);Check(c.Ended&&c.How==NativeHulkMoves.ChargeEnd.Solid&&c.Step(8.5f,10,0,Vector3.Zero,out _,out float ea)==NativeHulkMoves.ChargePhase.Stop&&Math.Abs(ea-.5f)<1e-4f,"hulk-charge-end: the solid stop");
            Check(c.Step(8+NativeHulkMoves.Stop(NativeHulkMoves.ChargeEnd.Solid)+NativeHulkMoves.BlendOut+.01f,10,0,Vector3.Zero,out _,out _)==NativeHulkMoves.ChargePhase.None,"over by its stop and blend");
            var bad=NativeHulkCharge.State.None;bad.Event(NativeHulkMoves.ChargeEndEvent,1,4);Check(!bad.Known,"an end with no start is ignored");
            c.Event(NativeHulkMoves.ChargeEvent,0,20);Check(c.ChargeAt==20&&!c.Ended&&c.Target==0&&!c.Locked,"a new charge forgets the old one's end and lock");
            // The frame fields: the same phases.
            var f=NativeHulkCharge.State.None;f.Frame(-100,-100,0,0);Check(!f.Known,"chargeAt -100: none");
            f.Frame(5,-100,0,3);Check(f.Known&&f.Target==3&&!f.Ended,"chargeAt starts it, chargeTarget the seat");f.Frame(5,8,1,3);Check(f.Ended&&f.How==NativeHulkMoves.ChargeEnd.Pilot&&f.ChargeEndAt==8,"chargeEndAt and chargeEnd end it");
            // A lost end event: the add stands still after its launch, drawn as spent; the authority's end replaces it.
            var s=NativeHulkCharge.State.None;s.Start(0,1);NativeHulkMoves.ChargePhase ph=NativeHulkMoves.ChargePhase.None;
            for(int i=0;i<180;i++){float t=i/60f;ph=s.Step(t,0,t<2?6:0,Vector3.Zero,out _,out _);}
            Check(s.Ended&&s.Assumed&&s.How==NativeHulkMoves.ChargeEnd.Spent&&Math.Abs(s.ChargeEndAt-2)<.02f,"standing still after the launch: drawn as spent from when it stopped");
            s.Event(NativeHulkMoves.ChargeEndEvent,1,2.05f);Check(s.How==NativeHulkMoves.ChargeEnd.Pilot&&!s.Assumed,"the authority's end replaces the assumed one");
            // Never ended by its length while it keeps moving (cross-map at any length, short of the absurd).
            var longRun=NativeHulkCharge.State.None;longRun.Start(0,1);for(int i=0;i<50*60;i++)Check(longRun.Step(i/60f,0,i/60f<NativeHulkMoves.ChargeWind?0:6,Vector3.Zero,out _,out _)!=NativeHulkMoves.ChargePhase.Stop,"a moving run is held at any length");
        }
        // ---- the lane: walls ahead, their tremble ----
        private static void Lane()
        {
            var from=new Vector3(0,0,0);var h=new Vector2(0,1);var half=new Vector3(2.5f,2.1f,.25f);float r=.95f,ht=3.06f;
            Check(NativeHulkCharge.LaneHit(from,h,10,r,ht,new Vector3(0,2.1f,6),0,half,out float d)&&Math.Abs(d-(6-.25f-r))<1e-3f,"a wall across the lane: met at its near face less the body's radius ("+d+")");
            Check(!NativeHulkCharge.LaneHit(from,h,10,r,ht,new Vector3(4,2.1f,6),0,half,out _),"a wall clear to the side is not in it");
            Check(NativeHulkCharge.LaneHit(from,h,10,r,ht,new Vector3(3.2f,2.1f,6),0,half,out _),"a wall the body's side would clip is");
            Check(!NativeHulkCharge.LaneHit(from,h,10,r,ht,new Vector3(0,2.1f,-6),0,half,out _),"one behind is not");
            Check(!NativeHulkCharge.LaneHit(from,h,3,r,ht,new Vector3(0,2.1f,6),0,half,out _),"one past the reach is not (yet)");
            Check(!NativeHulkCharge.LaneHit(from,h,10,r,ht,new Vector3(0,7.5f,6),0,half,out _),"one on another floor is not");
            // Rotated: a wall turned along the lane is passed beside, one turned across it met.
            var h45=NativeHulkCharge.Heading(45);
            Check(NativeHulkCharge.LaneHit(from,h45,12,r,ht,new Vector3(h45.X*6,2.1f,h45.Y*6),45,half,out float d45)&&Math.Abs(d45-(6-.25f-r))<1e-3f,"a wall square to a 45 deg lane");
            Check(!NativeHulkCharge.LaneHit(from,h,12,r,ht,new Vector3(3.6f,2.1f,6),90,half,out _),"a wall along the lane beside it");
            for(float to=-.2f;to<1;to+=.01f){float s=Math.Abs(NativeHulkCharge.Tremble(to,to*3,7));Check(s<=.04f,"the shake is bounded");if(to>NativeHulkCharge.TrembleLead||to<-.05f)Check(s==0,"it shakes only just before the contact");}
            float most=0;for(float t=0;t<1;t+=.005f)most=Math.Max(most,Math.Abs(NativeHulkCharge.Tremble(.02f,t,7)));Check(most>.02f,"right before the contact it rattles");
            Check(NativeHulkCharge.LockStreak(.1f,out float l1,out float a1)&&NativeHulkCharge.LockStreak(.4f,out float l2,out _)&&l2>=l1&&a1>.9f&&!NativeHulkCharge.LockStreak(1.3f,out _,out _),"the lock's streak sweeps out, holds and is gone");
        }
        // ---- no allocation ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var f=Input(96800);var c=NativeHulkCharge.State.None;c.Start(0,2);float z=0;
            void Frame(int i)
            {
                float t=i/60f,v=t<NativeHulkMoves.ChargeWind?0:6;z+=v/60;var ph=c.Step(t,10,v,new Vector3(0,0,z),out float age,out float endAge);
                f.Time=t;f.Charging=true;f.ChargeAge=age;f.Velocity=new Vector3(0,0,v/Sc);f.Root=new Vector3(0,NativeHordeKinds.Root[K],z/Sc);f.ChargeBroke=i%300>200;f.ChargeBreakAge=(i%300-200)/60f;g.Pose(f,pose);
                NativeHulkCharge.Warn(2,2,1,ph,age,endAge,new Vector3(0,0,z),2.2f,new Vector3(0,0,v),NativeHulkCharge.Heading(10),new Vector3(1,0,80),i,out var w);
                NativeHulkCharge.Layout(.2f+(i%7)*.15f,.5f,i%2==0?3:-3,.1f,16/9f,w.Urgency,t,false);
                NativeHulkCharge.LaneHit(new Vector3(0,0,z),NativeHulkCharge.Heading(10),4,.95f,3.06f,new Vector3(0,2,z+3),10,new Vector3(2.5f,2.1f,.25f),out _);NativeHulkCharge.Tremble(.2f,t,3);NativeHulkCharge.Flinch(t%1);
            }
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<2100;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1800 frames of the run, its tracking, the warning, the marker and the lane ("+used+" bytes)");
        }
        // ---- the runtime: reads the replicated state and the events, draws only ----
        private static void Runtime(string root)
        {
            string charge=R(root,"Assets/ChipArena/Runtime/NativeHordeHulkCharge.cs"),motion=R(root,"Assets/ChipArena/Runtime/NativeHordeHulkMotion.cs"),hud=R(root,"Assets/ChipArena/Runtime/NativeHulkWarningHud.cs"),audio=R(root,"Assets/ChipArena/Runtime/NativeHulkChargeAudio.cs");
            string kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"),breach=R(root,"Assets/ChipArena/Runtime/NativeHordeBreachPresentation.cs"),game=R(root,"Assets/ChipArena/Runtime/ConversionGame.cs"),fight=R(root,"Assets/ChipArena/Runtime/NativeFightHud.cs");
            foreach(var text in new[]{charge,motion,hud,audio})foreach(var bad in new[]{".Hp=",".Health=","Damage(","Emit(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","Body.position=","linearVelocity=","AddComponent<","Collider>().enabled","Events.Add",".restoreAt=",".breakAt="})
                Check(!text.Contains(bad),"the charge's runtime never writes `"+bad+"`");
            Check(motion.Contains("h.Charge.Event(e.Detail,e.Amount,e.Time)")&&motion.Contains("NativeHulkMoves.MoveEvent")&&motion.Contains("private void HordeHulkFrame(NativeMinion m)"),"the events drive it; one place reads the frame fields once they exist");
            Check(motion.Contains("if(ph==NativeHulkMoves.ChargePhase.Run&&h.Charge.Locked)f.Yaw=h.Charge.LockYaw;"),"the run is drawn on the locked heading");
            Check(motion.Contains("HordeWallBroke(m.Id,h.Charge.ChargeAt+NativeHulkMoves.ChargeWind-.05f,now,out float brokeAt)")&&motion.Contains("b.Live&&b.Actor==actor&&b.BreakAt>=from"),"a break by this Hulk in its run (a block's or a panel's record) flinches it");
            Check(breach.Contains("s.SpawnAt=charged?r.breakAt:NativeBreach.KnockAt(r.breakAt)")&&breach.Contains("if(charged)spawnAt=breakAt;")&&breach.Contains("HordeChargedBreak(frame.actor,frame.breakAt)"),"a wall crashed through on contact bursts with no knock");
            Check(kinds.Contains("HordeHulkPosed(s,now);")&&kinds.Contains("HordeChargesFx(draw,arena,now,eye,right,up,low);")&&kinds.Contains("HordeTremblesDraw(now);")&&kinds.Contains("HordeHulkBlowFx(draw,arena,j,a,H,fwd,side,eye,right,up,half,now);")&&kinds.Contains("HordeGalleryHulkCharge(arena,me,now,across,yawToMe,ground);")&&kinds.Contains("ClearHordeHulkChargeGallery();"),"live adds and the gallery feed the charge's views, effects, trembles and blows");
            Check(charge.Contains("Graphics.DrawMesh(mesh,m,w.Materials[s]")&&charge.Contains("NudgeWardBlock(w.Block,by)")&&!charge.Contains("transform.position="),"a shaking wall is drawn rattled (its transform and collider never moved)");
            Check(charge.Contains("NativeHulkCharge.Warn(local,v.Target,")&&hud.Contains("presentation.HulkWarning")&&hud.Contains("w.Target!=p.Seat.id")&&fight.Contains("HulkWarningHud(p);"),"the HUD marker is the hunted pilot's own");
            Check(game.Contains("nativeAudio.HulkWarning=presentation.HulkWarning;nativeAudio.Render(arena,menu,cameraView);")&&R(root,"Assets/ChipArena/Runtime/NativeEffectsAudio.cs").Contains("HordeCues(arena);HulkChargeCues(arena);")&&audio.Contains("w.Frame<Time.frameCount-2"),"the audio hears the live warning only");
            Check(game.Contains("prefs.cameraShake&&!menu&&Spectator==null&&!cinematic.Directing)presentation.HordeShake("),"the charge's shakes ride the horde shake: Settings · camera shake gates them, Reduced effects halves them");
            Check(hud.Contains("prefs.reducedEffects")&&hud.Contains("ReplayActive"),"the HUD respects Reduced effects and stays out of replays");
            var core=R(root,"Assets/ChipArena/Core/NativeHulkCharge.cs");Check(!core.Contains("UnityEngine")&&!core.Contains("NativeArena")&&!core.Contains("DateTime")&&!core.Contains("new Random("),"NativeHulkCharge is engine free and clock free");
        }
    }
}
#endif
