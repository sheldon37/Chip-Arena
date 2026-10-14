#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The heavy kinds' body animation (owner 2026-10-14: "Slam/landing/wall break animation also needs improvements from
    // hulk/brute. Hulk zombie needs adjustments on their movement (normal zombie movement looks odd at their size). I'd like them
    // to all have a charge animation (they can charge through walls) and a couple of punch animations (like a straight punch,
    // and a swipe)"). Presentation only: the Hulk's heavy stride against its speed, planted feet that never slide, turning in
    // place on pivot steps, every contact on the clock the rules (or the proposed contract) already use, the proposed moves'
    // key poses, deterministic, no allocation, and a runtime that only draws.
    internal static class HulkMotionChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hulk motion: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private const int K=NativeHordeKinds.Hulk;
        private static readonly float Sc=NativeHordeKinds.Scale[K];
        public static void Run(string root)
        {
            Stride();Planted();Turn();Smash();Landing();Knock();Punch();Swipe();Charge();Contract(root);Determinism();Allocation();Runtime(root);
            Console.WriteLine("PASS hulk motion ("+count+" checks: the Hulk's heavy stride and cadence at its speeds, planted feet on the drawn rig walking, running, turning and charging, turning in place on pivot steps, the smash's fists on the floor at the strike (Hulk and brute), the landings at LandAt, the breach's blows on NativeBreach.Blows, the proposed punch, swipe and charge key poses on their contract times, deterministic, no allocation, presentation-only runtime)");
        }
        private static HordeMotionInput Input(int kind,int id)=>new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Root=new Vector3(0,NativeHordeKinds.Root[kind],0),Attack=NativeHordeKinds.Attack.Smash,Side=1};
        private static float WorldSteps(int kind,float worldSpeed){float v=worldSpeed/NativeHordeKinds.Scale[kind];return 2*v/NativeHordeGait.Stride(kind,v);}

        // ---- the heavy stride: long, slow, matched to the speed (the step cue's spacing reads the same stride) ----
        private static void Stride()
        {
            float prev=0;for(float v=0;v<3;v+=.01f){float s=NativeHordeGait.HeavyStride(v);Check(s>=prev-1e-6f,"the heavy stride grows with the speed");prev=s;Check(NativeHordeGait.Stride(K,v)==s,"the Hulk's step spacing (audio, sheets) is its heavy stride");
                float d=NativeHordeGait.HeavyDuty(v);Check(d>=.38f-1e-5f&&d<=.66f+1e-5f&&NativeHordeGait.HeavyDuty(v+.01f)<=d+1e-6f,"the stance share shortens with the speed");}
            var st=NativeHordeKinds.ProposedRow(K);var bs=NativeHordeKinds.ProposedRow(NativeHordeKinds.Brute);
            float walk=WorldSteps(K,st.walk),run=WorldSteps(K,st.run),charge=WorldSteps(K,NativeHulkMoves.ChargeSpeed);
            Check(walk>=.8f&&walk<=1.1f,"the Hulk walks under about a step a second ("+walk+"/s)");
            Check(run>=1f&&run<=1.4f,"and runs at a little over one ("+run+"/s)");
            float stepW=st.walk/walk,stepR=st.run/run,leg=.5f*Sc;
            Check(stepW/leg>=.75f&&stepW/leg<=1.05f&&stepR/leg>=.95f&&stepR/leg<=1.25f,"its steps are long for its legs (walk "+stepW+" m, run "+stepR+" m, legs "+leg+" m)");
            Check(NativeHulkMoves.ChargeSpeed/charge<=1.5f&&charge<=3.8f,"a charge runs on quick strides its legs can reach ("+charge+"/s)");
            Check(walk<.4f*WorldSteps(NativeHordeKinds.Brute,bs.walk),"a far slower cadence than the brute's ("+walk+" against "+WorldSteps(NativeHordeKinds.Brute,bs.walk)+"/s)");
            Check(NativeHordeGait.Stamp(0)==0&&NativeHordeGait.Stamp(1)<1e-4f&&NativeHordeGait.Stamp(.5f)>.95f&&NativeHordeGait.Stamp(.92f)<NativeHordeGait.Stamp(.75f),"a step is lifted, carried and stamped down");
            Check(NativeHordeGait.Jolt(-.1f)==0&&NativeHordeGait.Jolt(.04f)>.9f&&NativeHordeGait.Jolt(.04f)<1.1f&&NativeHordeGait.Jolt(.7f)==0,"each footfall's jolt is bounded and brief");
        }
        // ---- planted feet on the drawn rig: a planted foot never moves, the drawn ankle stays on it, the cadence is the formula's ----
        private static void Planted()
        {
            var pose=new CapturedRig.Pose();var st=NativeHordeKinds.ProposedRow(K);
            foreach(int id in new[]{60000,61234,70011})foreach(var (speed,turn) in new[]{(st.walk,0f),(st.walk,20f),(st.walk,-20f),(st.run,0f),(st.run,20f),(st.run,-20f),(NativeHulkMoves.ChargeSpeed,0f)})
            {
                var g=new NativeHordeGait();var f=Input(K,id);var last=new Vector3[2];var air=new bool[2];float worst=0,worstDrawn=0,yaw=0;int steps=0,planted=0;Vector3 at=f.Root;float first=-1,lastStep=0;
                for(int i=0;i<900;i++)
                {
                    float t=i/60f;yaw+=turn/60;float r=yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed/Sc;at+=v/60;
                    f.Time=t;f.Root=at;f.Velocity=v;f.Yaw=yaw;f.Ground=0;g.Pose(f,pose);
                    for(int j=0;j<2;j++)
                    {
                        var drawn=g.ToWorld(pose.WorldPosition[j==0?CapturedRig.LFoot:CapturedRig.RFoot],at);
                        if(i>300&&!air[j]&&!g.FootAir[j]){worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));worstDrawn=Math.Max(worstDrawn,new Vector2(drawn.X-g.FootWorld[j].X,drawn.Z-g.FootWorld[j].Z).Length());planted++;Check(Math.Abs(drawn.Y-NativeHordeGait.AnkleHeight)<.01f,"a planted ankle stands on the floor");}
                        if(i>300&&(g.Landed&(1<<j))!=0){steps++;if(first<0)first=t;lastStep=t;}last[j]=g.FootWorld[j];air[j]=g.FootAir[j];
                    }
                }
                string what="hulk "+id+" at "+speed+" m/s turning "+turn+" deg/s";
                Check(worst<1e-4f,what+": a planted foot never moves (worst drift "+worst+" per frame, 0 per stride)");
                Check(worstDrawn<.012f,what+": the drawn ankle of a planted foot is on its spot (worst "+worstDrawn.ToString("0.0000")+" rig m)");
                float cadence=(steps-1)/Math.Max(1e-3f,lastStep-first),want=WorldSteps(K,speed);
                Check(steps>=6&&planted>steps&&Math.Abs(cadence-want)/want<.12f,what+": it steps on the heavy cadence ("+cadence.ToString("0.00")+" against "+want.ToString("0.00")+"/s)");
            }
        }
        // ---- turning in place: pivot steps, the foot on the turning side first, the facing reached, nothing sliding ----
        private static void Turn()
        {
            var pose=new CapturedRig.Pose();
            foreach(float to in new[]{120f,-90f})
            {
                var g=new NativeHordeGait();var f=Input(K,90100);var last=new Vector3[2];var air=new bool[2];int firstFoot=-1;float reached=float.NaN,prevYaw=0;bool turnedPlanted=false;
                for(int i=0;i<360;i++)
                {
                    float t=i/60f;f.Time=t;f.Yaw=t<1?0:to;f.Ground=0;g.Pose(f,pose);
                    // (A pivot's last turn and its foot's landing share a frame.)
                    if(i>0&&Math.Abs(g.Yaw-prevYaw)>.01f&&!g.FootAir[0]&&!g.FootAir[1]&&!air[0]&&!air[1])turnedPlanted=true;prevYaw=g.Yaw;
                    for(int j=0;j<2;j++){if(i>0&&!air[j]&&!g.FootAir[j])Check(Vector3.Distance(last[j],g.FootWorld[j])<1e-4f,"a planted foot stays put through a turn");if(t>=1&&firstFoot<0&&g.FootAir[j])firstFoot=j;last[j]=g.FootWorld[j];air[j]=g.FootAir[j];}
                    if(float.IsNaN(reached)&&Math.Abs(g.Yaw-to)<1)reached=t;
                }
                Check(!turnedPlanted,"turning "+to+": the body turns only on a pivot step (a foot in the air)");
                Check(firstFoot==(to>0?1:0),"turning "+to+": the foot on the turning side leads");
                Check(!float.IsNaN(reached)&&reached-1<=Math.Abs(to)/45f+.6f&&reached-1>=Math.Abs(to)/NativeHordeGait.PivotMost*.3f,"turning "+to+": the facing is reached on heavy pivots (in "+(reached-1).ToString("0.00")+" s)");
            }
            // A wind-up turns fast to face where the authority aims the blow.
            {
                var g=new NativeHordeGait();var f=Input(K,90101);var st=NativeHordeKinds.ProposedRow(K);f.Strike=st.hit;f.Recover=st.cycle;
                for(int i=0;i<60;i++){f.Time=i/60f;g.Pose(f,pose);}
                for(int i=0;i<16;i++){float t=1+i/60f;f.Time=t;f.Yaw=90;f.AttackAge=t-1;g.Pose(f,pose);}
                Check(Math.Abs(g.Yaw-90)<1,"a wind-up turns fast to the authority's aim ("+g.Yaw+")");
            }
        }
        // ---- the pose at `age` into a move, its world joints (source frame, the root at the origin) ----
        private static void Play(NativeHordeGait g,CapturedRig.Pose pose,ref HordeMotionInput f,float t0,float age,Action<float> set)
        {
            for(float t=0;t<t0+age-1e-4f;t+=1/60f){f.Time=t;set(t-t0);g.Pose(f,pose);}
            f.Time=t0+age;set(age);g.Pose(f,pose);
        }
        private static Vector3 W(CapturedRig.Pose p,int b)=>p.WorldPosition[b];
        // ---- the smash: the pack's hammer on the authority's strike, the rise, the drop, both fists on the floor at the strike ----
        private static void Smash()
        {
            var pose=new CapturedRig.Pose();
            foreach(int k in new[]{NativeHordeKinds.Hulk,NativeHordeKinds.Brute})
            {
                var st=NativeHordeKinds.ProposedRow(k);float H=NativeHordeKinds.Strike(st.hit,false,0),Rc=NativeHordeKinds.Recover(st.hit,st.cycle,false,0),lead=k==K?.22f:.12f;string n=NativeHordeKinds.Ids[k];
                float raised=-9,atStrike=9,peakBody=-9,dropBody=9;
                for(float a=0;a<=Rc;a+=1/60f)
                {
                    var g=new NativeHordeGait();var f=Input(k,91000+k);f.Strike=H;f.Recover=Rc;Play(g,pose,ref f,1,a,x=>f.AttackAge=x>=0?x:float.NaN);
                    float y=Math.Max(W(pose,CapturedRig.LHand).Y,W(pose,CapturedRig.RHand).Y);
                    if(a>=H-.4f&&a<=H-.1f)raised=Math.Max(raised,y);if(Math.Abs(a-H)<.5f/60)atStrike=y;
                    if(Math.Abs(a-(H-lead))<.5f/60)peakBody=pose.Position[CapturedRig.Body].Y;if(Math.Abs(a-(H+.1f))<.5f/60)dropBody=pose.Position[CapturedRig.Body].Y;
                    Check(g.AttackClip>=0&&g.Move==NativeHordeGait.Keyed.Smash,n+": the hammer's take plays under the smash's keys");
                }
                Check(raised>=NativeHordeGait.Floor+.9f,n+": a fist is raised high before the blow ("+(raised-NativeHordeGait.Floor).ToString("0.00")+" m over the floor)");
                Check(atStrike<=NativeHordeGait.Floor+.42f&&raised-atStrike>=.5f,n+": both fists are down by the floor at the strike (the higher wrist "+(atStrike-NativeHordeGait.Floor).ToString("0.00")+" m over it)");
                Check(peakBody-dropBody>=.09f,n+": it rises before the blow and drops into it ("+peakBody.ToString("0.00")+" to "+dropBody.ToString("0.00")+")");
                // The exact strike frame (no frame stepping): the fists at the floor targets' height.
                {var g=new NativeHordeGait();var f=Input(k,91000+k);f.Strike=H;f.Recover=Rc;Play(g,pose,ref f,1,H,x=>f.AttackAge=x>=0?x:float.NaN);
                 Check(Math.Abs(g.AttackClipTime-NativeZombieClips.Clip(g.AttackClip).Marks[2])<1e-5f,n+": the impact frame shows exactly at LastAttack + hit");}
            }
        }
        // ---- the landings: fists raised for the fall's last beat, down on the floor at LandAt, the deepest just after, risen by ActiveAt ----
        private static void Landing()
        {
            var pose=new CapturedRig.Pose();
            foreach(var p in new[]{NativeHordeDrop.Hulk,NativeHordeDrop.Brute})
            {
                string n=p.Heavy?"hulk":"brute";float h=p.DefaultHeight,span=NativeHordeDrop.Duration(h,p),land=NativeHordeDrop.LandAt(span,p);float low=9,lowAt=-1;
                for(float a=land-.25f;a<span;a+=1/120f)
                {
                    NativeHordeGait.Drop(pose,7,a,span,h,p,out _,out var ph);pose.World();float y=pose.Position[CapturedRig.Body].Y;
                    if(a>=land){if(y<low-1e-5f){low=y;lowAt=a-land;}}
                    if(a<land-.02f&&a>land-.08f)Check(ph==NativeHordeDrop.Phase.Air&&Math.Min(W(pose,CapturedRig.LHand).Y,W(pose,CapturedRig.RHand).Y)>W(pose,CapturedRig.Head).Y,n+": both fists over its head as it comes down");
                }
                Check(lowAt>=0&&lowAt<=.15f,n+": the landing's deepest just after LandAt (at +"+lowAt.ToString("0.00")+")");
                NativeHordeGait.Drop(pose,7,land+.05f,span,h,p,out _,out _);pose.World();
                float l=W(pose,CapturedRig.LHand).Y,r=W(pose,CapturedRig.RHand).Y;
                if(p.Heavy)Check(Math.Min(l,r)<=NativeHordeGait.Floor+.36f&&Math.Max(l,r)>Math.Min(l,r)+.1f,n+": a three-point landing (one fist on the floor)");
                else Check(Math.Max(l,r)<=NativeHordeGait.Floor+.4f,n+": both fists on the floor");
                NativeHordeGait.Drop(pose,7,span-.001f,span,h,p,out _,out _);Check(pose.Position[CapturedRig.Body].Y>=-.08f,n+": risen by ActiveAt (the gait takes over)");
                // The mirrored three-point landing for another seed (either arm).
            }
        }
        // ---- the breach's blows: a right and a left on NativeBreach.Blows' first two, the shoulder on the break ----
        private static void Knock()
        {
            Check(NativeBreach.Blows.Length==3&&Math.Abs(NativeBreach.Blows[0]-.55f)<1e-6f&&Math.Abs(NativeBreach.Blows[1]-1.2f)<1e-6f&&Math.Abs(NativeBreach.Blows[2]-NativeBreach.Knock)<1e-6f,"the knock is keyed to the breach's blows (.55, 1.2, the break)");
            var pose=new CapturedRig.Pose();float[] rz=new float[300],lz=new float[300],sz=new float[300];
            for(int i=0;i<300;i++)
            {
                float a=i/100f;var g=new NativeHordeGait();var f=Input(K,92000);Play(g,pose,ref f,1,a,x=>{f.Knocking=x>=0;f.KnockAge=x;});
                rz[i]=W(pose,CapturedRig.RHand).Z;lz[i]=W(pose,CapturedRig.LHand).Z;sz[i]=W(pose,CapturedRig.RArm).Z-W(pose,CapturedRig.LArm).Z+W(pose,CapturedRig.RArm).Z;
                Check(g.AttackClip<0&&g.Move==NativeHordeGait.Keyed.Knock,"a knock replaces the hammer's take");
            }
            int Peak(float[] z,float from,float to){int best=(int)(from*100);for(int i=(int)(from*100);i<=(int)(to*100)&&i<z.Length;i++)if(z[i]>z[best])best=i;return best;}
            int r0=Peak(rz,.3f,.9f),l1=Peak(lz,.9f,1.5f),s2=Peak(sz,1.8f,2.4f);
            Check(Math.Abs(r0/100f-NativeBreach.Blows[0])<=.05f&&rz[r0]>=.45f,"the right lands on the first blow ("+(r0/100f)+" s, "+rz[r0].ToString("0.00")+" m ahead)");
            Check(Math.Abs(l1/100f-NativeBreach.Blows[1])<=.05f&&lz[l1]>=.45f,"the left lands on the second ("+(l1/100f)+" s, "+lz[l1].ToString("0.00")+" m ahead)");
            Check(Math.Abs(s2/100f-NativeBreach.Blows[2])<=.08f,"the shoulder drives on the break ("+(s2/100f)+" s)");
            Check(rz[(int)(.43f*100)]<0&&lz[(int)(1.08f*100)]<0,"each blow is drawn back first");
        }
        // ---- the proposed straight punch: coiled back, full reach on the strike, mirrored for the left ----
        private static void Punch()
        {
            var pose=new CapturedRig.Pose();float H=NativeHulkMoves.PunchHit,Rc=NativeHulkMoves.Recover(NativeHulkMoves.Move.Punch,3.4f,false,0);
            Check(H==NativeHulkMoves.Strike(NativeHulkMoves.Move.Punch,1.1f,false,0)&&Rc==Math.Max(NativeHulkMoves.PunchCycle,H+.05f),"the punch's strike and recovery are the contract's");
            foreach(int side in new[]{1,-1})
            {
                int hand=side>0?CapturedRig.RHand:CapturedRig.LHand,arm=hand-2;float best=-9,bestAt=-1;
                for(float a=0;a<=Rc;a+=1/60f)
                {
                    var g=new NativeHordeGait();var f=Input(K,93000);f.Move=NativeHulkMoves.Move.Punch;f.Side=side;f.Strike=H;f.Recover=Rc;Play(g,pose,ref f,1,a,x=>f.AttackAge=x>=0?x:float.NaN);
                    Check(g.Move==NativeHordeGait.Keyed.Punch&&g.AttackClip<0,"a punch is keyed (no take)");
                    float z=W(pose,hand).Z;if(z>best){best=z;bestAt=a;}
                    if(Math.Abs(a-(H-.2f))<.5f/60)Check(z<=-.1f&&Vector3.Distance(W(pose,hand),W(pose,arm))<=.45f,"side "+side+": the fist is cocked back by its shoulder ("+z.ToString("0.00")+")");
                }
                var g2=new NativeHordeGait();var f2=Input(K,93000);f2.Move=NativeHulkMoves.Move.Punch;f2.Side=side;f2.Strike=H;f2.Recover=Rc;Play(g2,pose,ref f2,1,H,x=>f2.AttackAge=x>=0?x:float.NaN);
                float reach=Vector3.Distance(W(pose,hand),W(pose,arm)),zh=W(pose,hand).Z,x=W(pose,hand).X;
                Check(reach>=.5f,"side "+side+": the arm is at full reach on the strike ("+reach.ToString("0.000")+")");
                Check(zh>=.55f&&best-zh<=.15f&&bestAt>=H-.02f&&bestAt<=H+.2f,"side "+side+": the fist is driven straight ahead on the strike ("+zh.ToString("0.00")+" m, farthest "+best.ToString("0.00")+" at "+bestAt.ToString("0.00")+")");
                Check(x*side<0&&Math.Abs(x)<.3f,"side "+side+": with the striking arm, in front of the body");
            }
        }
        // ---- the proposed swipe: drawn back wide on its side, crossing the facing on the strike, carried across ----
        private static void Swipe()
        {
            var pose=new CapturedRig.Pose();float H=NativeHulkMoves.SwipeHit,Rc=NativeHulkMoves.Recover(NativeHulkMoves.Move.Swipe,3.4f,false,0);
            Check(H==NativeHulkMoves.Strike(NativeHulkMoves.Move.Swipe,1.1f,false,0)&&Rc==Math.Max(NativeHulkMoves.SwipeCycle,H+.05f),"the swipe's strike and recovery are the contract's");
            foreach(int side in new[]{1,-1})
            {
                int hand=side>0?CapturedRig.RHand:CapturedRig.LHand;float s=side>0?-1:1;// source x: the right arm's side is -x
                Vector3 At(float a){var g=new NativeHordeGait();var f=Input(K,94000);f.Move=NativeHulkMoves.Move.Swipe;f.Side=side;f.Strike=H;f.Recover=Rc;Play(g,pose,ref f,1,a,x=>f.AttackAge=x>=0?x:float.NaN);Check(g.Move==NativeHordeGait.Keyed.Swipe,"a swipe is keyed");return W(pose,hand);}
                var back=At(H-.14f);var hit=At(H);var through=At(H+.16f);
                Check(back.X*s>=.5f&&back.Z<.05f,"side "+side+": drawn back wide on its own side ("+back+")");
                Check(hit.Z>=.3f&&hit.X*s<back.X*s-.3f,"side "+side+": crossing in front on the strike ("+hit+")");
                Check(through.X*s<=-.2f,"side "+side+": carried across to the other side ("+through+")");
            }
        }
        // ---- the proposed charge: the wind-up's scrape and lowered shoulder, the run, the ends ----
        private static void Charge()
        {
            var pose=new CapturedRig.Pose();
            {
                var g=new NativeHordeGait();var f=Input(K,95000);float footStart=0,footEnd=0;bool braced=false;
                for(int i=0;i<=(int)(NativeHulkMoves.ChargeWind*60)+60;i++){float t=i/60f,a=t-1;f.Time=t;f.Charging=a>=0;f.ChargeAge=a;g.Pose(f,pose);if(i==59)footStart=g.FootWorld[1].Z;if(a>=.7f&&a<.72f)footEnd=g.FootWorld[1].Z;if(a>=.8f&&a<.82f)braced=!g.FootAir[1];
                    if(a>=0&&a<NativeHulkMoves.ChargeWind-1e-4f){Check(g.Move==NativeHordeGait.Keyed.ChargeWind,"the wind-up is keyed");Check(!g.FootAir[0],"the left foot stands braced");}}
                Check(footStart-footEnd>=.15f&&braced,"the wind-up scrapes the right foot back and braces it ("+(footStart-footEnd).ToString("0.00")+")");
                pose.World();Check(W(pose,CapturedRig.RArm).Z>W(pose,CapturedRig.LArm).Z+.12f,"the right shoulder leads at the launch");
                Check(Vector3.Transform(Vector3.UnitY,pose.WorldRotation[CapturedRig.Chest]).Z>.15f,"the chest is lowered forward at the launch");
            }
            foreach(var how in new[]{NativeHulkMoves.ChargeEnd.Pilot,NativeHulkMoves.ChargeEnd.Solid,NativeHulkMoves.ChargeEnd.Spent})
            {
                // (The hips balance over the hit volume, so the drive and the recoil read on the leading shoulder and the head.)
                float stop=NativeHulkMoves.Stop(how);float[] probe={0,.07f,.25f,stop+NativeHulkMoves.BlendOut+.05f};float[] sz=new float[4],up=new float[4];NativeHordeGait.Keyed last=NativeHordeGait.Keyed.None;
                for(int q=0;q<probe.Length;q++)
                {
                    var g=new NativeHordeGait();var f=Input(K,96000);float end=2.4f;
                    for(float t=0;t<=end+probe[q]+1e-4f;t+=1/60f){float a=t-.5f;f.Time=t;f.Charging=a>=0;f.ChargeAge=a;f.ChargeEnded=t>=end;f.ChargeEndAge=t-end;f.ChargeHow=t>=end?how:NativeHulkMoves.ChargeEnd.Running;
                        bool run=a>=NativeHulkMoves.ChargeWind&&t<end;f.Velocity=new Vector3(0,0,run?NativeHulkMoves.ChargeSpeed/Sc:0);f.Root=new Vector3(0,NativeHordeKinds.Root[K],Math.Max(0,Math.Min(t,end)-.5f-NativeHulkMoves.ChargeWind)*NativeHulkMoves.ChargeSpeed/Sc);g.Pose(f,pose);
                        if(run&&t>1.6f)Check(g.Move==NativeHordeGait.Keyed.ChargeRun,"the run holds the lowered shoulder");}
                    sz[q]=W(pose,CapturedRig.RArm).Z;up[q]=Vector3.Transform(Vector3.UnitZ,pose.WorldRotation[CapturedRig.Head]).Y;last=g.Move;
                }
                Check(last==NativeHordeGait.Keyed.None,how+": the end blends out by its stop ("+stop+" s)");
                if(how==NativeHulkMoves.ChargeEnd.Pilot)Check(sz[1]>sz[0]+.03f&&sz[2]<sz[1]-.05f&&up[2]>up[0]+.3f,"a pilot hit: the shoulder drives through, then recoils, the head snapped up ("+string.Join(", ",sz.Select(v=>v.ToString("0.00")))+")");
                if(how==NativeHulkMoves.ChargeEnd.Solid)Check(up[2]>0&&sz[2]<sz[0]-.25f,"a solid stop: bounced back, the head snapped back ("+up[2].ToString("0.00")+", "+sz[2].ToString("0.00")+")");
            }
            // A wall broken on the run jolts the shoulder; bounded.
            {
                var g=new NativeHordeGait();var f=Input(K,97000);var a0=new CapturedRig.Pose();
                for(float t=0;t<2;t+=1/60f){f.Time=t;f.Charging=true;f.ChargeAge=t;f.ChargeBroke=t>=1.5f;f.ChargeBreakAge=t-1.5f;f.Velocity=new Vector3(0,0,2.5f);f.Root=new Vector3(0,.85f,t*2.5f);g.Pose(f,pose);Check(Math.Abs(pose.Position[CapturedRig.Body].Z)<.4f,"a wall broken on the run jolts within reason");}
            }
        }
        // ---- the contract the presentation reads (docs/SIREN-ZOMBIES.md section 10) ----
        private static void Contract(string root)
        {
            Check(NativeHulkMoves.Of(0)==NativeHulkMoves.Move.Content&&NativeHulkMoves.Of(1)==NativeHulkMoves.Move.Punch&&NativeHulkMoves.Of(2)==NativeHulkMoves.Move.Swipe&&NativeHulkMoves.Of(7)==NativeHulkMoves.Move.Content,"move ids (0 content, 1 punch, 2 swipe; unknown: content)");
            Check(NativeHulkMoves.EndOf(1)==NativeHulkMoves.ChargeEnd.Pilot&&NativeHulkMoves.EndOf(2)==NativeHulkMoves.ChargeEnd.Solid&&NativeHulkMoves.EndOf(3)==NativeHulkMoves.ChargeEnd.Spent&&NativeHulkMoves.EndOf(0)==NativeHulkMoves.ChargeEnd.Running,"charge end ids");
            Check(NativeHulkMoves.Strike(NativeHulkMoves.Move.Punch,1.1f,true,0)==1.1f&&NativeHulkMoves.Strike(NativeHulkMoves.Move.Swipe,1.1f,false,2|4)==1.1f,"crawling or armless, the content's attack timing as today");
            Check(NativeHulkMoves.Heavy(NativeHordeKinds.Hulk)&&NativeHulkMoves.Heavy(NativeHordeKinds.Brute)&&!NativeHulkMoves.Heavy(NativeHordeKinds.Shambler),"the keyed moves are the heavy kinds'");
            Check(NativeHulkMoves.PunchHit<NativeHulkMoves.PunchCycle&&NativeHulkMoves.SwipeHit<NativeHulkMoves.SwipeCycle&&NativeHulkMoves.SwipeHit>NativeHulkMoves.PunchHit&&NativeHulkMoves.PunchHit<NativeHordeKinds.ProposedRow(K).hit,"a punch is quicker than a swipe, both quicker than the smash");
            Check(NativeHulkMoves.PhaseOf(.2f,float.NaN,NativeHulkMoves.ChargeEnd.Running)==NativeHulkMoves.ChargePhase.Wind&&NativeHulkMoves.PhaseOf(1.5f,float.NaN,NativeHulkMoves.ChargeEnd.Running)==NativeHulkMoves.ChargePhase.Run&&NativeHulkMoves.PhaseOf(2,.5f,NativeHulkMoves.ChargeEnd.Solid)==NativeHulkMoves.ChargePhase.Stop&&NativeHulkMoves.PhaseOf(4,2.5f,NativeHulkMoves.ChargeEnd.Solid)==NativeHulkMoves.ChargePhase.None,"the charge's phases from its clocks");
            Check(NativeHulkMoves.MoveEvent.StartsWith("hulk-")&&NativeHulkMoves.ChargeEvent.StartsWith("hulk-")&&NativeHulkMoves.ChargeEndEvent.StartsWith("hulk-"),"the accepted events");
            // The arm a blow swings is the presentation's own hash of LastAttack (the authority can compute the same).
            var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");Check(kinds.Contains("int side=((int)(lastAttack*7.31f)&1)==0?1:-1;"),"the side formula matches the presentation's HordeSide");
            for(float la=0;la<30;la+=.37f)foreach(int lost in new[]{0,2,4,6,16})Check(NativeHulkMoves.Side(la,lost)==(((int)(la*7.31f)&1)==0?(NativeHordeKinds.RightArmless(lost)?-1:1):(NativeHordeKinds.LeftArmless(lost)?1:-1)),"Side matches HordeSide");
        }
        // ---- determinism ----
        private static byte[] Sequence()
        {
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var ms=new MemoryStream();var bw=new BinaryWriter(ms);var f=Input(K,98000);var st=NativeHordeKinds.ProposedRow(K);
            void Rec(){for(int b=0;b<22;b++){bw.Write(pose.WorldPosition[b].X);bw.Write(pose.WorldPosition[b].Y);bw.Write(pose.WorldPosition[b].Z);}}
            for(int i=0;i<1500;i++)
            {
                float t=i/60f;f.Time=t;f.Yaw=t<2?0:t<4?100:0;bool walk=t>=4&&t<8;f.Velocity=new Vector3(0,0,walk?.42f:0);f.Root=new Vector3(0,.85f,Math.Max(0,Math.Min(t,8)-4)*.42f);
                f.AttackAge=t>=8&&t<10.5f?t-8:t>=11&&t<13.5f?t-11:float.NaN;f.Move=t<11?NativeHulkMoves.Move.Punch:NativeHulkMoves.Move.Swipe;f.Side=t<11?1:-1;f.Strike=t<11?NativeHulkMoves.PunchHit:NativeHulkMoves.SwipeHit;f.Recover=t<11?NativeHulkMoves.PunchCycle:NativeHulkMoves.SwipeCycle;
                f.Knocking=t>=14&&t<17.6f;f.KnockAge=t-14;f.Charging=t>=18&&t<24;f.ChargeAge=t-18;f.ChargeEnded=t>=21;f.ChargeEndAge=t-21;f.ChargeHow=NativeHulkMoves.ChargeEnd.Solid;
                g.Pose(f,pose);Rec();if(i%5==0){NativeHordeGait.Drop(pose,3,t%5,NativeHordeDrop.Duration(9,NativeHordeDrop.Hulk),9,NativeHordeDrop.Hulk,out _,out _);Rec();}
            }
            return ms.ToArray();
        }
        private static void Determinism(){var a=Sequence();var b=Sequence();Check(a.Length>0&&a.SequenceEqual(b),"the heavy gait, the moves and the landings are the same on every run (every peer, the killcam)");}
        // ---- no allocation ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var b=new NativeHordeGait();var f=Input(K,99000);var fb=Input(NativeHordeKinds.Brute,99001);var st=NativeHordeKinds.ProposedRow(NativeHordeKinds.Brute);
            void Frame(int i)
            {
                float t=i/60f,u=t%26;f.Time=t;f.Yaw=u<2?0:u<4?100:0;f.Velocity=new Vector3(0,0,u>=4&&u<8?.42f:u>=22?2.5f:0);f.Root=new Vector3(0,.85f,t*.3f);
                f.AttackAge=u>=8&&u<10.5f?u-8:u>=11&&u<13.5f?u-11:float.NaN;f.Move=u<11?NativeHulkMoves.Move.Punch:NativeHulkMoves.Move.Swipe;f.Strike=.7f;f.Recover=2.2f;
                f.Knocking=u>=14&&u<17.6f;f.KnockAge=u-14;f.Charging=u>=18;f.ChargeAge=u-18;f.ChargeEnded=u>=24.5f;f.ChargeEndAge=u-24.5f;f.ChargeHow=NativeHulkMoves.ChargeEnd.Pilot;f.ChargeBroke=u>=23;f.ChargeBreakAge=u-23;g.Pose(f,pose);
                fb.Time=t;fb.Root=new Vector3(2,.85f,0);fb.AttackAge=(i%120)/60f;fb.Strike=st.hit;fb.Recover=st.cycle;b.Pose(fb,pose);
                NativeHordeGait.Drop(pose,3,t%5,NativeHordeDrop.Duration(9,NativeHordeDrop.Hulk),9,NativeHordeDrop.Hulk,out _,out _);NativeHordeGait.Drop(pose,4,t%3,3,6.5f,out _,out _);
            }
            for(int i=0;i<400;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=400;i<2000;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1600 frames of the heavy gait, turns, punches, swipes, knocks, charges and landings ("+used+" bytes)");
        }
        // ---- the runtime: reads the replicated state and the events, draws only ----
        private static void Runtime(string root)
        {
            var m=R(root,"Assets/ChipArena/Runtime/NativeHordeHulkMotion.cs");var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            foreach(var bad in new[]{".Hp=",".Health=","Damage(","Emit(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","Body.position=","linearVelocity=","AddComponent<","Collider>().enabled","Events.Add"})Check(!m.Contains(bad),"the heavy motion runtime never writes `"+bad+"`");
            Check(kinds.Contains("HordeHulkEvent(e);")&&kinds.Contains("var move=HordeHulkAttack(m,a.Crawl,a.Lost,ref attack,ref strike,ref recover);")&&kinds.Contains("HordeHulkInput(ref f,m,now,entering,breach);")&&kinds.Contains("RoarAge=roar,Move=move};"),"live adds pose the knocks and the reported moves");
            Check(kinds.Contains("HordeGalleryHulkMoves(me,now,across,yawToMe,ground);")&&kinds.Contains("ClearHordeHulkMovesGallery();")&&kinds.Contains("ClearHordeHulkMoves();"),"the Dev Sandbox gallery previews the moves; a new arena forgets them");
            Check(m.Contains("NativeHulkMoves.MoveEvent")&&m.Contains("NativeHulkMoves.ChargeEvent")&&m.Contains("NativeHulkMoves.ChargeEndEvent")&&m.Contains("hordeBreaches[i]"),"the dormant moves read only the reported events and the breach records");
            foreach(var f in new[]{"NativeHordeHeavy.cs","NativeHulkMoves.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("UnityEngine")&&!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("new Random(")&&!Regex.IsMatch(t,@"DateTime|Stopwatch"),f+" is engine free and clock free");}
        }
    }
}
#endif
