#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
namespace ChipArena.UnityConversion
{
    // The zombie pipeline measured stage by stage (owner 2026-11-04: "Determine precisely where the unnatural movement first appears";
    // docs/ZOMBIE-LOCOMOTION.md). `--zombie-stages` prints, for each kind's takes at its walk and run: the take as converted on the
    // rig (stage 2), the same take on the model's legs with no gait (stage 3), and the gait's pose on the rig and on the model (stage 4),
    // walking straight, then turning at the kinds' limited turn rates (stage 5). A synthetic skeleton, not a render: the numbers say
    // where a pose leaves the capture, and only the comparison mode in Unity shows how that reads.
    internal static class ZombieStageChecks
    {
        private const float Dt=1/120f,Deg=57.29578f;
        private sealed class Stat
        {
            public float Min=float.MaxValue,Max=float.MinValue,Sum;public int N;
            public void Add(float v){Min=Math.Min(Min,v);Max=Math.Max(Max,v);Sum+=v;N++;}
            public float Mean=>N==0?0:Sum/N;public float Range=>N==0?0:Max-Min;
        }
        // One body's numbers over whole cycles, its lengths in leg-lengths.
        private sealed class Body
        {
            public readonly Stat Fore=new Stat(),Hips=new Stat(),Knee=new Stat(),KneeBack=new Stat(),Lean=new Stat(),Pelvis=new Stat(),PelvisPitch=new Stat(),Shoulders=new Stat(),Elbow=new Stat(),UpperArm=new Stat(),Thigh=new Stat();
            public string Row(string name)=>string.Format("{0,-30}{1,6:0.00}{2,6:0.00}{3,6:0.00}{4,6:0.00}{5,5:0}{6,5:0}{7,5:0}{8,5:0}{9,5:0}{10,5:0}{11,5:0}{12,5:0}{13,5:0}",name,Fore.Max,Fore.Min,Hips.Mean,Hips.Range,Knee.Min,Knee.Max,Lean.Mean,Lean.Range,Pelvis.Range,Shoulders.Range,PelvisPitch.Mean,Elbow.Min,UpperArm.Max);
        }
        private const string Head="                                fore   aft  hips  bob knee knee lean lean pelv shld pelv  elb  arm";
        private const string Head2="                                legs  legs  legs legs  min  max mean rnge yawR yawR ptch  min  out";
        private static float Yaw(Vector3 across){return (float)Math.Atan2(-across.Z,across.X)*Deg;}
        private static float Wrap(float d){d%=360;if(d>180)d-=360;if(d<-180)d+=360;return d;}
        private static float Angle(Vector3 a,Vector3 b){float n=a.Length()*b.Length();return n<1e-9f?0:(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(a,b)/n)))*Deg;}
        private static void Rig(CapturedRig.Pose p,Body b,float floor,float leg,float zScale)
        {
            p.World();
            for(int side=0;side<2;side++)
            {
                int hip=side==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=p.WorldPosition[hip],k=p.WorldPosition[hip+1],a=p.WorldPosition[hip+2];
                b.Fore.Add((a.Z-h.Z)*zScale/leg);b.Knee.Add(180-Angle(h-k,a-k));b.Thigh.Add((float)Math.Atan2(k.Z-h.Z,h.Y-k.Y)*Deg);
                int arm=side==0?CapturedRig.LArm:CapturedRig.RArm;Vector3 s=p.WorldPosition[arm],e=p.WorldPosition[arm+1],w=p.WorldPosition[arm+2];
                b.Elbow.Add(180-Angle(s-e,w-e));b.UpperArm.Add(Angle(e-s,new Vector3(0,-1,0)));
            }
            var pel=p.WorldPosition[CapturedRig.Pelvis];b.Hips.Add((pel.Y-floor)/leg);
            var neck=p.WorldPosition[CapturedRig.Neck];b.Lean.Add((float)Math.Atan2(neck.Z-pel.Z,neck.Y-pel.Y)*Deg);
            b.Pelvis.Add(Yaw(p.WorldPosition[CapturedRig.LHip]-p.WorldPosition[CapturedRig.RHip]));
            b.Shoulders.Add(Yaw(p.WorldPosition[CapturedRig.LArm]-p.WorldPosition[CapturedRig.RArm]));
            var up=Vector3.Transform(Vector3.UnitY,p.WorldRotation[CapturedRig.Pelvis]);b.PelvisPitch.Add((float)Math.Atan2(up.Z,up.Y)*Deg);
        }
        private static void Model(NativeVisualRig.State s,Body b,float floor,float leg)
        {
            var r=s.Rig;Vector3 At(int j)=>s.Position[r.Of[j]];
            for(int side=0;side<2;side++)
            {
                int hip=side==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=At(hip),k=At(hip+1),a=At(hip+2);
                b.Fore.Add((a.Z-h.Z)/leg);b.Knee.Add(180-Angle(h-k,a-k));b.Thigh.Add((float)Math.Atan2(k.Z-h.Z,h.Y-k.Y)*Deg);
                int arm=side==0?CapturedRig.LArm:CapturedRig.RArm;Vector3 sh=At(arm),e=At(arm+1),w=At(arm+2);
                b.Elbow.Add(180-Angle(sh-e,w-e));b.UpperArm.Add(Angle(e-sh,new Vector3(0,-1,0)));
            }
            var pel=At(CapturedRig.Pelvis);b.Hips.Add((pel.Y-floor)/leg);
            var neck=At(CapturedRig.Neck);b.Lean.Add((float)Math.Atan2(neck.Z-pel.Z,neck.Y-pel.Y)*Deg);
            b.Pelvis.Add(Yaw(At(CapturedRig.LHip)-At(CapturedRig.RHip)));
            b.Shoulders.Add(Yaw(At(CapturedRig.LArm)-At(CapturedRig.RArm)));
            var up=Vector3.Transform(Vector3.UnitY,s.World[r.Of[CapturedRig.Pelvis]]);b.PelvisPitch.Add((float)Math.Atan2(up.Z,up.Y)*Deg);
        }
        private const float RigFloor=-.85f;
        /// <summary>The take at `phase` as converted, no gait: its planted ankle on the rig's floor.</summary>
        public static void RawPose(int clip,float phase,float[] row,CapturedRig.Pose p)
        {
            NativeZombieClips.SamplePhase(clip,phase,row);NativeZombieClips.Pose(row,p,true,0);
            var b=p.Position[CapturedRig.Body];b.Y+=RigFloor+NativeHordeGait.AnkleHeight-NativeZombieMotion.Ground(clip);p.Position[CapturedRig.Body]=b;p.World();
        }
        private static void RawModel(NativeVisualRig.State s,CapturedRig.Pose raw,CapturedRig.Pose fit)
        {
            NativeZombiePosture.Fit(raw,fit,false);s.Across=true;s.Level=false;s.Follow=true;s.Ease=0;s.Stride=s.Rig.LegRatio;s.Room=NativeVisualRig.KneeRoom;s.Stand=0;s.Raise=0;
            NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
        }
        public static void Run()
        {
            NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();float legRig=CapturedRig.Bones[CapturedRig.RKnee].Position.Length()+CapturedRig.Bones[CapturedRig.RFoot].Position.Length(),legModel=rig.LegUpper+rig.LegLower;
            var o=new StringBuilder();o.AppendLine("rig leg "+legRig.ToString("0.000")+" model leg "+legModel.ToString("0.000")+" ratio "+rig.LegRatio.ToString("0.000"));
            // ---- the takes as converted ----
            o.AppendLine();o.AppendLine("STAGE 2 / 3: each cycle as converted, on the rig and then on the model's legs (no gait, no plant)");o.AppendLine(Head);o.AppendLine(Head2);
            var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();var fit=new CapturedRig.Pose();
            float modelFloor=0;
            {var s0=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s0,Rest(fit),default(NativeVisualRig.Grasp),false,false);modelFloor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            o.AppendLine("The takes' own feet (rig metres; on/off the take's phases of contact, then ahead of the hip at each, outside the hip, roll rad, right's strike, duty, floor rise):");
            for(int c=0;c<NativeZombieClips.Count;c++)
            {
                if(!NativeZombieClips.Clip(c).Loop||NativeZombieClips.Speed(c)<=0)continue;
                float cap=NativeZombieMotion.OwnStrideOf(c);
                o.AppendLine(string.Format("{0,-14} L on {1:0.00} off {2:0.00} z {3:0.00}/{4:0.00} out {5:0.00} roll {6:0.00} | R on {7:0.00} off {8:0.00} z {9:0.00}/{10:0.00} out {11:0.00} roll {12:0.00} | right {13:0.00} duty {14:0.00}/{15:0.00} stride {16:0.00} rise {17:0.000}",NativeZombieClips.Key(c),
                    NativeZombieMotion.ContactOn(c,0),NativeZombieMotion.ContactOff(c,0),NativeZombieMotion.TouchOf(c,0),NativeZombieMotion.LiftOf(c,0),NativeZombieMotion.StandOut(c,0),NativeZombieMotion.RollOf(c,0),
                    NativeZombieMotion.ContactOn(c,1),NativeZombieMotion.ContactOff(c,1),NativeZombieMotion.TouchOf(c,1),NativeZombieMotion.LiftOf(c,1),NativeZombieMotion.StandOut(c,1),NativeZombieMotion.RollOf(c,1),
                    NativeZombieMotion.RightAt(c),NativeZombieMotion.OwnDuty(c,0),NativeZombieMotion.OwnDuty(c,1),cap,NativeZombieMotion.FloorRise(c,RigFloor+NativeHordeGait.AnkleHeight)));
            }
            o.AppendLine();
            var rawOf=new Dictionary<int,(Body rigBody,Body modelBody)>();
            for(int c=0;c<NativeZombieClips.Count;c++)
            {
                if(!NativeZombieClips.Clip(c).Loop||NativeZombieClips.Speed(c)<=0)continue;
                var rb=new Body();var mb=new Body();var s=new NativeVisualRig.State(rig);
                for(int k=0;k<240;k++){RawPose(c,k/240f,row,raw);Rig(raw,rb,RigFloor,legRig,1);RawModel(s,raw,fit);if(k<24)continue;Model(s,mb,modelFloor,legModel);}
                rawOf[c]=(rb,mb);o.AppendLine(rb.Row(NativeZombieClips.Key(c)+" rig"));o.AppendLine(mb.Row(NativeZombieClips.Key(c)+" model"));
            }
            // ---- the gait ----
            o.AppendLine();o.AppendLine("STAGE 4: the gait walking straight (rig pose, then the model's), same columns; then rate = playback over the take's own pace, stride k = the stride over the capture's on the same legs, stepHz");o.AppendLine(Head);o.AppendLine(Head2);
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute})
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(var (name,speed) in new[]{("walk",st.walk),("run",st.run)})
                {
                    var seen=new HashSet<(int,int)>();
                    for(int id=40000;id<40400&&seen.Count<3;id++)
                    {
                        uint seed=NativeHordeKinds.Seed(id);int w=NativeZombieMotion.Walk(kind,seed),r=NativeZombieMotion.Run(kind,seed);if(!seen.Add((w,r)))continue;
                        var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=kind,Id=id,Seed=seed,Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);
                        var s=new NativeVisualRig.State(rig);var rb=new Body();var mb=new Body();float ph0=0,t0=0;int steps=0;
                        const int warm=240,total=1200;
                        for(int i=0;i<total;i++)
                        {
                            var v=new Vector3(0,0,speed);at+=v*Dt;f.Time=i*Dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                            NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                            if(i<warm)continue;if(i==warm){ph0=g.Phase;t0=f.Time;}
                            Rig(p,rb,RigFloor,legRig,g.LegsNow);Model(s,mb,modelFloor,legModel);
                            if((g.Landed&1)!=0)steps++;if((g.Landed&2)!=0)steps++;
                        }
                        float hz=(g.Phase-ph0)/(f.Time-t0);int c=g.RunNow>.5f?r:w;float rate=hz*NativeZombieClips.Duration(c),stride=speed/hz,own=NativeZombieClips.Speed(c)*NativeZombieClips.Duration(c)*rig.LegRatio;
                        o.AppendLine(string.Format("{0} {1} {2:0.0} m/s on {3}/{4}: rate {5:0.00} stride k {6:0.00} ({7:0.00} m/cycle, capture {8:0.00}) {9:0.00} steps/s (capture {10:0.00})",NativeHordeKinds.Ids[kind],name,speed,NativeZombieClips.Key(w),NativeZombieClips.Key(r),rate,stride/own,stride,own,2*hz,2/NativeZombieClips.Duration(c)));
                        o.AppendLine(rb.Row("  gait rig"));o.AppendLine(mb.Row("  gait model"));o.AppendLine(rawOf[c].modelBody.Row("  capture model"));
                    }
                }
            }
            Takes(o,rig,fit,modelFloor,legModel);
            Turns(o,rig,fit,modelFloor,legModel);
            Console.WriteLine(o.ToString());
        }
        // ---- every take at the speeds the kinds move at: which stray least from the capture ----
        private static void Takes(StringBuilder o,NativeVisualRig.Rig rig,CapturedRig.Pose fit,float modelFloor,float legModel)
        {
            o.AppendLine();o.AppendLine("TAKES: on the shambler's gait, the model's legs. rate, stride k, steps/s, fore, aft (legs; capture's in brackets), bob, knee most, planted knee mean (capture's), foot's top speed m/s");
            string[] walks={"walk-4","walk-6","walk-6-full","chase-1","chase-1-full","chase-2","chase-5","chase-5-full"},runs={"sprint-1","sprint-2","sprint-3","sprint-4"};
            var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();
            void One(string key,float speed)
            {
                int c=NativeZombieClips.Need(key),id=40003;var g=new NativeHordeGait{Legs=rig.LegRatio,ForceTake=c};var p=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=NativeHordeKinds.Shambler,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);
                var s=new NativeVisualRig.State(rig);var m=new Body();var planted=new Stat();float top=0;var last=new Vector3[2];bool has=false;
                for(int i=0;i<1200;i++)
                {
                    var v=new Vector3(0,0,speed);at+=v*Dt;f.Time=i*Dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                    NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,f.Kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                    if(i<360)continue;Model(s,m,modelFloor,legModel);
                    for(int a=0;a<2;a++)
                    {
                        int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],k=s.Position[rig.Of[hip+1]],an=s.Position[rig.Of[hip+2]];
                        if(!g.FootAir[a])planted.Add(180-Angle(h-k,an-k));
                        var w=an+new Vector3(0,0,at.Z);if(has)top=Math.Max(top,(w-last[a]).Length()/Dt);last[a]=w;
                    }
                    has=true;
                }
                // The capture's own planted knee, through its contact stretches.
                var own=new Body();var ownKnee=new Stat();var so=new NativeVisualRig.State(rig);
                for(int k=0;k<240;k++)
                {
                    float ph=k/240f;RawPose(c,ph,row,raw);RawModel(so,raw,fit);if(k<24)continue;Model(so,own,modelFloor,legModel);
                    for(int a=0;a<2;a++){float on=NativeZombieMotion.ContactOn(c,a),d=NativeZombieMotion.OwnDuty(c,a),u=ph-on;u-=(float)Math.Floor(u);if(u>=d)continue;int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=so.Position[rig.Of[hip]],kn=so.Position[rig.Of[hip+1]],an=so.Position[rig.Of[hip+2]];ownKnee.Add(180-Angle(h-kn,an-kn));}
                }
                float hz=g.CycleRate,rate=hz*NativeZombieClips.Duration(c),stride=speed/hz,cap=NativeZombieMotion.OwnStrideOf(c)*rig.LegRatio;
                o.AppendLine(string.Format("{0,-10}{1,4:0.0} r {2:0.00} k {3:0.00} {4:0.0}/s fore {5:0.00} ({6:0.00}) aft {7:0.00} ({8:0.00}) bob {9:0.00} ({10:0.00}) knee {11,3:0} ({12,3:0}) plant {13,3:0} ({14,3:0}) top {15:0.0}",key.Replace("chase","ch").Replace("sprint","sp"),speed,rate,stride/cap,2*hz,m.Fore.Max,own.Fore.Max,m.Fore.Min,own.Fore.Min,m.Hips.Range,own.Hips.Range,m.Knee.Max,own.Knee.Max,planted.Mean,ownKnee.Mean,top));
            }
            foreach(float sp in new[]{1.1f,1.3f,1.5f,1.6f})foreach(var key in walks)One(key,sp);
            foreach(float sp in new[]{2.4f,2.6f,2.9f,3.4f,4.6f})foreach(var key in runs)One(key,sp);
        }
        // ---- stage 5: the presented root (Core/NativeHordeFlow) through the authority's turns ----
        private static void Turns(StringBuilder o,NativeVisualRig.Rig rig,CapturedRig.Pose fit,float modelFloor,float legModel)
        {
            o.AppendLine();o.AppendLine("STAGE 5: through a turn on the presented root (the authority turns at once): align s (facing within 15 deg of the way it goes), most off deg (facing off the travel), yaw deg/s, twist deg (a planted foot's turn off the facing), cross m (feet's sideways gap, negative: legs crossed), reach legs (a foot from its hip), kneeSide deg, steps, skate cm (a planted foot's most motion a frame)");
            o.AppendLine(string.Format("{0,-34}{1,6}{2,6}{3,6}{4,6}{5,7}{6,6}{7,6}{8,6}{9,6}{10,6}{11,6}","","align","off","yaw/s","twist","cross","reach","knee","steps","skate","lift","hips"));
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute})
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(var (name,speed,turn,spin) in new[]{("walk 45",st.walk,45f,0f),("walk 90",st.walk,90f,0f),("walk 180",st.walk,180f,0f),("run 45",st.run,45f,0f),("run 90",st.run,90f,0f),("run 180",st.run,180f,0f),("stand 90",0f,90f,0f),("stand 180",0f,180f,0f),("walk circle 90/s",st.walk,0f,90f),("run circle 90/s",st.run,0f,90f),("walk zigzag",st.walk,60f,-1f)})
                {
                    int id=40003;uint seed=NativeHordeKinds.Seed(id);var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);
                    var f=new HordeMotionInput{Kind=kind,Id=id,Seed=seed,Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};
                    var body=new Vector3(0,.85f,0);float dir=0,yawNow=0;const float t0=3f;const float dt=1/60f;float align=float.NaN,off=0,yawMost=0,twist=0,cross=9,reach=0,kneeSide=0,skate=0,lift=0,low=9;int steps=0;var last=new Vector3[2];var had=new bool[2];float lastYaw=0;
                    for(int i=0;i<60*7;i++)
                    {
                        float t=i*dt;
                        if(spin>0&&t>=t0)dir+=spin*dt;else if(spin<0&&t>=t0)dir=((int)((t-t0)*3)%2==0?1:-1)*turn;else if(t>=t0)dir=turn;
                        float r=dir/Deg;var d=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));var v=d*speed;body+=v*dt;yawNow=speed>0?dir:(t>=t0?turn:0);
                        flow.Host(t,t,body,v,true,yawNow,dt,false);
                        f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;g.Pose(f,p,null);p.World();
                        NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                        if(t<t0-.1f){for(int a=0;a<2;a++){last[a]=g.FootWorld[a];had[a]=!g.FootAir[a];}lastYaw=g.Yaw;continue;}
                        float want=yawNow,oy=Math.Abs(Wrap(g.Yaw-want));
                        if(spin==0&&t>=t0){if(oy<15){if(float.IsNaN(align))align=t-t0;}else align=float.NaN;off=Math.Max(off,oy);}
                        else if(t>=t0)off=Math.Max(off,oy);
                        yawMost=Math.Max(yawMost,Math.Abs(Wrap(g.Yaw-lastYaw))/dt);lastYaw=g.Yaw;
                        var la=g.ToLocal(g.FootWorld[0],f.Root);var ra=g.ToLocal(g.FootWorld[1],f.Root);cross=Math.Min(cross,la.X-ra.X);
                        for(int a=0;a<2;a++)
                        {
                            {int ah=a==0?CapturedRig.LHip:CapturedRig.RHip;lift=Math.Max(lift,(s.Position[rig.Of[ah+2]].Y-modelFloor)/legModel);low=Math.Min(low,(s.Position[rig.Of[CapturedRig.Pelvis]].Y-modelFloor)/legModel);}
                            if(!g.FootAir[a]){twist=Math.Max(twist,Math.Abs(Wrap(g.FootTurn(a)-g.Yaw)));if(had[a]&&(g.Landed&(1<<a))==0)skate=Math.Max(skate,Vector3.Distance(last[a],g.FootWorld[a])*100);}
                            last[a]=g.FootWorld[a];had[a]=!g.FootAir[a];if((g.Landed&(1<<a))!=0)steps++;
                            int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],k=s.Position[rig.Of[hip+1]],an=s.Position[rig.Of[hip+2]];
                            var flat=new Vector2(an.X-h.X,an.Z-h.Z);reach=Math.Max(reach,flat.Length()/legModel);
                            var line=Vector3.Normalize(an-h);var bend=k-h;bend-=line*Vector3.Dot(bend,line);if(bend.Length()>.03f){var fwd=Vector3.UnitZ-line*line.Z;if(fwd.Length()>.2f){fwd=Vector3.Normalize(fwd);var sd=Vector3.Cross(line,fwd);kneeSide=Math.Max(kneeSide,Math.Abs((float)Math.Atan2(Vector3.Dot(bend,sd),Vector3.Dot(bend,fwd)))*Deg);}}
                        }
                    }
                    o.AppendLine(string.Format("{0,-34}{1,6:0.00}{2,6:0}{3,6:0}{4,6:0}{5,7:0.00}{6,6:0.00}{7,6:0}{8,6}{9,6:0.0}{10,6:0.00}{11,6:0.00}",NativeHordeKinds.Ids[kind]+" "+name,align,off,yawMost,twist,cross,reach,kneeSide,steps,skate,lift,low));
                }
            }
        }
        private static CapturedRig.Pose Rest(CapturedRig.Pose p){p.Reset();p.World();return p;}
        /// <summary>The capture's feet about its hips on the model's legs (leg lengths: most forward, most back).</summary>
        public static void OwnReach(NativeVisualRig.Rig rig,int clip,out float fore,out float aft)
        {
            var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);float leg=rig.LegUpper+rig.LegLower;fore=float.MinValue;aft=float.MaxValue;
            for(int k=0;k<120;k++)
            {
                RawPose(clip,k/120f,row,raw);RawModel(s,raw,fit);if(k<12)continue;
                for(int a=0;a<2;a++){int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;float z=(s.Position[rig.Of[hip+2]].Z-s.Position[rig.Of[hip]].Z)/leg;fore=Math.Max(fore,z);aft=Math.Min(aft,z);}
            }
        }
        /// <summary>Rewrites the Station baseline's changed values (`--zombie-rebase`, for a pass that means to change how the Siren's kinds move), listing them.</summary>
        public static void Rebase(string root)
        {
            string path=System.IO.Path.Combine(root,"Tests/station-enemies-baseline.txt");var have=new Dictionary<string,string>();foreach(var kv in StationBaseline.Measure())have[kv.Key]=kv.Value;
            var lines=System.IO.File.ReadAllLines(path);int changed=0;
            for(int i=0;i<lines.Length;i++)
            {
                var l=lines[i];if(l.StartsWith("#")||l.Length==0)continue;int s=l.IndexOf(' ');string key=l.Substring(0,s),val=l.Substring(s+1);
                if(have.TryGetValue(key,out var now)&&now!=val){Console.WriteLine(key+": "+val+" -> "+now);lines[i]=key+" "+now;changed++;}
            }
            System.IO.File.WriteAllText(path,string.Join("\n",lines)+"\n");Console.WriteLine("rewrote "+changed+" values");
        }
        /// <summary>How far out the capture carries its upper arms on the model (deg, mean: the checks' measure of an arm's spread).</summary>
        public static float OwnSpread(NativeVisualRig.Rig rig,int clip)
        {
            var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var spread=new Stat();
            for(int k=0;k<120;k++)
            {
                RawPose(clip,k/120f,row,raw);RawModel(s,raw,fit);if(k<12)continue;
                for(int a=0;a<2;a++){int arm=a==0?CapturedRig.LArm:CapturedRig.RArm;var u=s.Position[rig.Of[arm+1]]-s.Position[rig.Of[arm]];float outward=a==0?u.X:-u.X;spread.Add((float)Math.Atan2(outward,Math.Max(1e-4f,-u.Y))*Deg);}
            }
            return spread.Mean;
        }
        /// <summary>The capture's planted knee on the model's legs (deg, mean through the take's contact stretches).</summary>
        public static float OwnPlantedKnee(NativeVisualRig.Rig rig,int clip)
        {
            var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var knee=new Stat();
            for(int k=0;k<240;k++)
            {
                float ph=k/240f;RawPose(clip,ph,row,raw);RawModel(s,raw,fit);if(k<24)continue;
                for(int a=0;a<2;a++){float u=ph-NativeZombieMotion.ContactOn(clip,a);u-=(float)Math.Floor(u);if(u>=NativeZombieMotion.OwnDuty(clip,a))continue;int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],kn=s.Position[rig.Of[hip+1]],an=s.Position[rig.Of[hip+2]];knee.Add(180-Angle(h-kn,an-kn));}
            }
            return knee.Mean;
        }
        /// <summary>The model's joints through a cycle (`--zombie-sticks file take speed kind`), the capture's and the gait's at the same phases of the take, as JSON for a stick-figure view.</summary>
        public static void Sticks(string file,string key,float speed,int kind)
        {
            NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();int c=NativeZombieClips.Need(key),id=40003;var g=new NativeHordeGait{Legs=rig.LegRatio,ForceTake=c};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var raw=new CapturedRig.Pose();var row=new float[CapturedClip.Channels];var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);var s=new NativeVisualRig.State(rig);var so=new NativeVisualRig.State(rig);
            int[] joints={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LArm+1,CapturedRig.LArm+2,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RArm+1,CapturedRig.RArm+2,CapturedRig.LHip,CapturedRig.LHip+1,CapturedRig.LHip+2,CapturedRig.LHip+3,CapturedRig.RHip,CapturedRig.RHip+1,CapturedRig.RHip+2,CapturedRig.RHip+3};
            var o=new StringBuilder();o.Append("{\"take\":\""+key+"\",\"speed\":"+speed.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"frames\":[");bool first=true;
            string Dump(NativeVisualRig.State st){var b=new StringBuilder("[");for(int j=0;j<joints.Length;j++){int v=rig.Of[joints[j]];var q=v<0?Vector3.Zero:st.Position[v];b.Append(j>0?",":"").Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,"[{0:0.000},{1:0.000},{2:0.000}]",q.X,q.Y,q.Z));}return b.Append("]").ToString();}
            for(int i=0;i<1400;i++)
            {
                const float dt=1/120f;var v=new Vector3(0,0,speed);at+=v*dt;f.Time=i*dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                RawPose(c,g.Phase,row,raw);RawModel(so,raw,fit);
                if(i<600||i%2!=0)continue;
                var l=g.ToLocal(g.FootWorld[0],f.Root);var r=g.ToLocal(g.FootWorld[1],f.Root);
                o.Append(first?"":",").Append("{\"phase\":"+(g.Phase-(float)Math.Floor(g.Phase)).ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+",\"air\":["+(g.FootAir[0]?1:0)+","+(g.FootAir[1]?1:0)+"],\"gait\":"+Dump(s)+",\"capture\":"+Dump(so)+"}");first=false;
            }
            o.Append("]}");System.IO.File.WriteAllText(file,o.ToString());Console.WriteLine("wrote "+file);
        }
        /// <summary>The trunk's chain on the takes: each joint's own bend and its world pitch (deg, + forward), on the rig and on the model, through a cycle's mean (`--zombie-trunk`).</summary>
        public static void Trunk()
        {
            NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();var fit=new CapturedRig.Pose();
            float Pitch(Quaternion q){var f=Vector3.Transform(Vector3.UnitZ,q);return (float)Math.Atan2(-f.Y,Math.Sqrt(f.X*f.X+f.Z*f.Z))*Deg;}
            float Bend(Quaternion q){var f=Vector3.Transform(Vector3.UnitZ,q);return (float)Math.Atan2(-f.Y,f.Z)*Deg;}
            int[] chain={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head};string[] names={"pelvis","spine","chest","neck","head"};
            foreach(var key in new[]{"chase-2","chase-5-full","walk-6","sprint-2","sprint-3","idle-3","shamble-1"})
            {
                int c=NativeZombieClips.Need(key);var world=new float[5];var own=new float[5];var model=new float[5];var s=new NativeVisualRig.State(rig);const int n=60;
                for(int k=0;k<n;k++)
                {
                    RawPose(c,k/(float)n,row,raw);RawModel(s,raw,fit);
                    for(int j=0;j<5;j++){world[j]+=Pitch(raw.WorldRotation[chain[j]])/n;own[j]+=Bend(raw.Rotation[chain[j]])/n;int v=rig.Of[chain[j]];if(v>=0)model[j]+=Pitch(s.World[v])/n;}
                }
                var b=new StringBuilder(key.PadRight(14));for(int j=0;j<5;j++)b.Append(string.Format("{0} bend {1,5:0} world {2,5:0} model {3,5:0} | ",names[j],own[j],world[j],model[j]));Console.WriteLine(b.ToString());
            }
        }
        /// <summary>A turn frame by frame (`--zombie-turn kind speed degrees`): the root's facing and velocity, each foot about the body and its state, the hips.</summary>
        public static void TurnTrace(int kind,float speed,float turn)
        {
            NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();int id=40003;var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);float leg=rig.LegUpper+rig.LegLower;
            var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var body=new Vector3(0,.85f,0);float dir=0;const float dt=1/60f;
            float floor;{var s0=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s0,Rest(fit),default(NativeVisualRig.Grasp),false,false);floor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            for(int i=0;i<300;i++)
            {
                float t=i*dt;if(t>=3)dir=turn;float r=dir/Deg;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;body+=v*dt;
                flow.Host(t,t,body,v,true,dir,dt,false);f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;g.Pose(f,p,null);p.World();
                NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                if(t<2.95f||t>4.4f||i%2!=0)continue;
                var l=g.ToLocal(g.FootWorld[0],f.Root);var rr=g.ToLocal(g.FootWorld[1],f.Root);float y0=(s.Position[rig.Of[CapturedRig.LHip+2]].Y-floor)/leg,y1=(s.Position[rig.Of[CapturedRig.RHip+2]].Y-floor)/leg,pel=(s.Position[rig.Of[CapturedRig.Pelvis]].Y-floor)/leg;
                Console.WriteLine(string.Format("TT {0:0.00} yaw {1,5:0} sp {2:0.0} hz {3:0.00} L {4} {5,5:0.00},{6,5:0.00} y{7:0.00} | R {8} {9,5:0.00},{10,5:0.00} y{11:0.00} | hips {12:0.00} sunk {13:0.00} stand {14:0.00} raise {15:0.00}",t-3,g.Yaw,g.Speed,g.CycleRate,g.FootAir[0]?"air":"pln",l.X/leg,l.Z/leg,y0,g.FootAir[1]?"air":"pln",rr.X/leg,rr.Z/leg,y1,pel,g.Sunk,s.Stand,s.Raise));
            }
        }
        /// <summary>One take at one speed frame by frame (`--zombie-trace key speed`): both feet's phase, state, place about the root, the model's ankle about its hip, the foot's speed.</summary>
        public static void Trace(string key,float speed)
        {
            NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();int c=NativeZombieClips.Need(key),id=40003;var g=new NativeHordeGait{Legs=rig.LegRatio,ForceTake=c};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=NativeHordeKinds.Shambler,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);var s=new NativeVisualRig.State(rig);var last=new Vector3[2];
            float leg=rig.LegUpper+rig.LegLower;
            Console.WriteLine("take on/off L "+NativeZombieMotion.ContactOn(c,0).ToString("0.00")+" "+NativeZombieMotion.ContactOff(c,0).ToString("0.00")+" duty "+NativeZombieMotion.OwnDuty(c,0).ToString("0.00")+" touch "+NativeZombieMotion.TouchOf(c,0).ToString("0.00")+" lift "+NativeZombieMotion.LiftOf(c,0).ToString("0.00"));
            for(int i=0;i<760;i++)
            {
                const float dt=1/120f;var v=new Vector3(0,0,speed);at+=v*dt;f.Time=i*dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,f.Kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                float top=0;var w=new Vector3[2];for(int a=0;a<2;a++){int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;w[a]=s.Position[rig.Of[hip+2]]+new Vector3(0,0,at.Z);top=Math.Max(top,(w[a]-last[a]).Length()/dt);last[a]=w[a];}
                if(i<600||i%3!=0&&top<7)continue;
                Vector3 h0=s.Position[rig.Of[CapturedRig.LHip]],a0=s.Position[rig.Of[CapturedRig.LHip+2]];
                Console.WriteLine(string.Format("{0,4} ph {1:0.00} L {2} z {3,6:0.00} rel {4,6:0.00} y {5,5:0.00} hip {6,5:0.00} heel {7:0.00} | R {8} z {9,6:0.00} | top {10:0.0} sunk {11:0.00} raise {12:0.00}",i,g.Phase-(float)Math.Floor(g.Phase),g.FootAir[0]?"air":"pln",g.ToLocal(g.FootWorld[0],f.Root).Z,(a0.Z-h0.Z)/leg,a0.Y,h0.Y,g.Heel[0],g.FootAir[1]?"air":"pln",g.ToLocal(g.FootWorld[1],f.Root).Z,top,g.Sunk,s.Raise));
            }
        }
        // ---- the regression checks: the gait against the capture it plays ----
        private static int checks;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie source motion: "+what);checks++;}
        /// <summary>
        /// The takes as captured (owner 2026-11-04: "I want the original Zombie PRO animations to look as close to their intended appearance as
        /// realistically possible"): each modelled kind at its walk and run on each of its takes, its model's hips, knees, feet, trunk and arms
        /// against the same take on the same legs with no gait, then the comparison mode and the turns. Numbers against the capture: that the
        /// motion reads as the pack's is the comparison mode's to show in Unity, not these checks'.
        /// </summary>
        public static void Source()
        {
            checks=0;NativeZombieMotion.Warm();var rig=ModelPoseChecks.Measured();float legModel=rig.LegUpper+rig.LegLower;var fit=new CapturedRig.Pose();var row=new float[CapturedClip.Channels];var raw=new CapturedRig.Pose();
            float modelFloor;{var s0=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s0,Rest(fit),default(NativeVisualRig.Grasp),false,false);modelFloor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            float worstRate=0,worstStretch=0,worstHips=0,worstKnee=0,worstYaw=0;int runs=0;
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute})
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(var (gait,speed) in new[]{("walk",st.walk),("run",st.run)})
                {
                    var seen=new HashSet<int>();
                    for(int id=40000;id<40200;id++)
                    {
                        uint seed=NativeHordeKinds.Seed(id);int take=gait=="walk"?NativeZombieMotion.Walk(kind,seed):NativeZombieMotion.Run(kind,seed);if(!seen.Add(take))continue;
                        var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=kind,Id=id,Seed=seed,Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var at=new Vector3(0,.85f,0);var s=new NativeVisualRig.State(rig);var m=new Body();
                        for(int i=0;i<1080;i++)
                        {
                            var v=new Vector3(0,0,speed);at+=v*Dt;f.Time=i*Dt;f.Root=at;f.Velocity=v;f.Yaw=0;f.Ground=0;g.Pose(f,p);p.World();
                            NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                            if(i>=360)Model(s,m,modelFloor,legModel);
                        }
                        int c=g.RunNow>.5f?g.RunClip:g.WalkClip;if(c!=take&&gait=="walk"&&g.RunNow<=.5f)continue;
                        var own=new Body();var so=new NativeVisualRig.State(rig);for(int k=0;k<240;k++){RawPose(c,k/240f,row,raw);RawModel(so,raw,fit);if(k>=24)Model(so,own,modelFloor,legModel);}
                        float rate=g.CycleRate*NativeZombieClips.Duration(c),stride=speed/g.CycleRate/(NativeZombieMotion.OwnStrideOf(c)*rig.LegRatio),stretch=Math.Max(1,stride);string name=NativeHordeKinds.Ids[kind]+" "+gait+" on "+NativeZombieClips.Key(c);runs++;
                        worstRate=Math.Max(worstRate,rate);worstStretch=Math.Max(worstStretch,stride);worstHips=Math.Max(worstHips,Math.Abs(m.Hips.Mean-own.Hips.Mean));worstKnee=Math.Max(worstKnee,m.Knee.Max-own.Knee.Max);worstYaw=Math.Max(worstYaw,Math.Abs(m.Pelvis.Range-own.Pelvis.Range));
                        Check(rate<=1.65f,name+": plays within 1.65 times its pace ("+rate.ToString("0.00")+")");
                        Check(stride<=NativeZombieMotion.OwnStretchMost+.05f,name+": strides within "+NativeZombieMotion.OwnStretchMost+" times its capture ("+stride.ToString("0.00")+")");
                        Check(m.Fore.Max<=own.Fore.Max*stretch+.18f&&m.Fore.Min>=own.Fore.Min*stretch-.18f,name+": the feet stay where the take has them, stretched with its stride ("+m.Fore.Max.ToString("0.00")+" to "+m.Fore.Min.ToString("0.00")+" legs, the take's "+own.Fore.Max.ToString("0.00")+" to "+own.Fore.Min.ToString("0.00")+" by "+stretch.ToString("0.00")+")");
                        Check(Math.Abs(m.Hips.Mean-own.Hips.Mean)<=.08f,name+": the hips stand where the take has them ("+m.Hips.Mean.ToString("0.00")+" legs, the take's "+own.Hips.Mean.ToString("0.00")+")");
                        Check(m.Hips.Range<=own.Hips.Range*stretch+.12f,name+": the hips ride no higher and lower than the take's ("+m.Hips.Range.ToString("0.00")+" legs, the take's "+own.Hips.Range.ToString("0.00")+")");
                        Check(m.Knee.Max<=own.Knee.Max+24,name+": the knee folds no further than the take's ("+m.Knee.Max.ToString("0")+" deg, the take's "+own.Knee.Max.ToString("0")+")");
                        Check(Math.Abs(m.Lean.Mean-own.Lean.Mean)<=6&&Math.Abs(m.Lean.Range-own.Lean.Range)<=6,name+": the trunk leans as the take's ("+m.Lean.Mean.ToString("0")+" +- "+m.Lean.Range.ToString("0")+" deg, the take's "+own.Lean.Mean.ToString("0")+" +- "+own.Lean.Range.ToString("0")+")");
                        Check(Math.Abs(m.Pelvis.Range-own.Pelvis.Range)<=6&&Math.Abs(m.Shoulders.Range-own.Shoulders.Range)<=8,name+": the pelvis and shoulders swing as the take's ("+m.Pelvis.Range.ToString("0")+", "+m.Shoulders.Range.ToString("0")+" deg, the take's "+own.Pelvis.Range.ToString("0")+", "+own.Shoulders.Range.ToString("0")+")");
                        Check(Math.Abs(m.Elbow.Min-own.Elbow.Min)<=26,name+": the arms reach as the take's (elbow bent at least "+m.Elbow.Min.ToString("0")+" deg, the take's "+own.Elbow.Min.ToString("0")+")");
                    }
                }
            }
            // ---- the comparison mode: one route, the left the capture itself ----
            {
                var z=new NativeZombieCompare{Kind=NativeHordeKinds.Shambler,Take=NativeZombieClips.Need("chase-2"),Speed=1.5f,Legs=rig.LegRatio};var l=new CapturedRig.Pose();var r=new CapturedRig.Pose();
                Check(new NativeHordeGait().StagePlant&&new NativeHordeGait().StagePosture&&new NativeHordeGait().StageRetime,"the game's gait runs with every stage on");
                float worst=0;
                for(int i=0;i<300;i++)
                {
                    z.Step(1/60f,l,r);l.World();r.World();if(i<120)continue;
                    Check(z.Left.WalkClip==z.Take&&z.Right.WalkClip==z.Take&&z.Left.RunClip==z.Take&&z.Right.RunClip==z.Take,"the comparison's take is both zombies' take");
                    NativeZombieClips.SamplePhase(z.Take,z.Left.Phase,row);NativeZombieClips.Pose(row,raw,true,0);raw.World();
                    for(int a=0;a<2;a++){int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;var dl=l.WorldPosition[hip+2]-l.WorldPosition[hip];var dr=raw.WorldPosition[hip+2]-raw.WorldPosition[hip];worst=Math.Max(worst,(dl-dr).Length());}
                }
                Check(worst<.01f,"the comparison's left zombie is the capture: its feet about its hips within a centimetre at the rig's scale ("+(worst*100).ToString("0.00")+" cm)");
                Check(Math.Abs(z.Left.CycleRate*NativeZombieClips.Duration(z.Take)-1)<.01f,"the left plays at its own pace");
                z.Readout(out float rr,out float rs,out float steps,out float ownSteps);Check(rr>1&&rr<1.65f&&rs>1&&rs<1.55f&&ownSteps<steps,"the readout: the right keeps up with the speed by its pace and its stride");
                // A stage off is the stage off, on the right as well.
                z.Plant=false;z.Posture=false;z.Retime=false;z.Reset();for(int i=0;i<60;i++)z.Step(1/60f,l,r);r.World();l.World();
                for(int b=0;b<CapturedRig.Bones.Length;b++)Check((r.WorldPosition[b]-l.WorldPosition[b]).Length()<.001f,"with every stage off the right is the left ("+CapturedRig.Bones[b].Name+")");
                z.Plant=true;z.Posture=true;z.Retime=true;z.Route=NativeZombieCompare.RouteKind.Turn180;z.Reset();float pauseAt=0;for(int i=0;i<30;i++)z.Step(1/60f,l,r);pauseAt=z.Time;z.Step(0,l,r);Check(z.Time==pauseAt,"a paused comparison holds its clock");
            }
            // ---- the turns: the body steps round rather than twisting over planted feet ----
            float mostTwist=0,leastCross=9;
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute})
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(var (name,speed,turn) in new[]{("walk 90",st.walk,90f),("walk 180",st.walk,180f),("run 90",st.run,90f),("run 180",st.run,180f),("stand 180",0f,180f)})
                {
                    int id=40003;var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);
                    var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};var body=new Vector3(0,.85f,0);float dir=0;const float dt=1/60f;var last=new Vector3[2];var had=new bool[2];float yawLast=0,yawMost=0;
                    for(int i=0;i<360;i++)
                    {
                        float t=i*dt;if(t>=3)dir=turn;float r=dir/Deg;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;body+=v*dt;
                        flow.Host(t,t,body,v,true,dir,dt,false);f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;g.Pose(f,p,null);p.World();
                        if(t<2.9f){for(int a=0;a<2;a++){last[a]=g.FootWorld[a];had[a]=!g.FootAir[a];}yawLast=g.Yaw;continue;}
                        yawMost=Math.Max(yawMost,Math.Abs(Wrap(g.Yaw-yawLast))/dt);yawLast=g.Yaw;
                        for(int a=0;a<2;a++)
                        {
                            if(!g.FootAir[a]){mostTwist=Math.Max(mostTwist,Math.Abs(Wrap(g.FootTurn(a)-g.Yaw)));Check(!(had[a]&&(g.Landed&(1<<a))==0)||Vector3.Distance(last[a],g.FootWorld[a])<.002f,NativeHordeKinds.Ids[kind]+" "+name+": a planted foot stays planted through the turn");}
                            last[a]=g.FootWorld[a];had[a]=!g.FootAir[a];
                        }
                        leastCross=Math.Min(leastCross,g.ToLocal(g.FootWorld[0],f.Root).X-g.ToLocal(g.FootWorld[1],f.Root).X);
                    }
                    if(speed==0)Check(yawMost*BossMath.Deg<=HordeFlow.TuneOf(kind).Turn*HordeFlow.TurnStanding+.2f,NativeHordeKinds.Ids[kind]+" "+name+": a standing body turns no faster than its steps carry it ("+yawMost.ToString("0")+" deg/s)");
                }
            }
            Check(mostTwist<=95,"through a turn no foot stays planted past about a quarter turn off the facing ("+mostTwist.ToString("0")+" deg, was 124)");
            Check(leastCross>=-.62f,"the feet cross no further than the walk's own stride carries them ("+leastCross.ToString("0.00")+" m, was -.41 on the zigzag alone)");
            Console.WriteLine("PASS Zombie source motion ("+checks+" checks: "+runs+" takes on the model's legs against the capture: at most "+worstRate.ToString("0.00")+" times the pace, "+worstStretch.ToString("0.00")+" the stride, hips within "+worstHips.ToString("0.00")+" legs, knee "+worstKnee.ToString("0")+" deg past, pelvis swing within "+worstYaw.ToString("0")+" deg; turns: planted twist "+mostTwist.ToString("0")+" deg, feet crossed "+leastCross.ToString("0.00")+" m; presentation only)");
        }
        /// <summary>Every check group that reads the gait, each to its own result (`--zombie-all`: the whole suite stops at its first failure).</summary>
        public static int All(string root)
        {
            int failed=0;
            void Go(string name,Action a){try{a();Console.WriteLine("ok   "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
            Go("ZombieSource",Source);Go("ZombieMotion",()=>ZombieMotionChecks.Run(root));Go("ZombieLoco",()=>ZombieLocoChecks.Run(root));Go("ZombieGait",()=>ZombieGaitChecks.Run(root));Go("ZombiePosture",()=>ZombiePostureChecks.Run(root));
            Go("ZombieKnock",()=>ZombieKnockChecks.Run(root));Go("ZombieTwitch",()=>ZombieTwitchChecks.Run(root));Go("ZombieFlow",()=>ZombieFlowChecks.Run(root));Go("ZombieModel",()=>ZombieModelChecks.Run(root));
            Go("ZombieGore",()=>ZombieGoreChecks.Run(root));Go("HordeRoam",()=>HordeRoamChecks.Run(root));Go("HordeKind",()=>HordeKindChecks.Run(root));Go("IronworksKind",()=>IronworksKindChecks.Run(root));Go("IronworksProp",()=>IronworksPropChecks.Run(root));
            Go("StationEnemy",()=>StationEnemyChecks.Run(root));Go("HulkMotion",()=>HulkMotionChecks.Run(root));Go("HulkCharge",()=>HulkChargeChecks.Run(root));Go("HulkSpitter",()=>HulkSpitterChecks.Run(root));
            Go("FootSurface",()=>FootSurfaceChecks.Run(root));Go("DevTools",()=>DevToolsChecks.Run(root));
            return failed;
        }
    }
}
#endif
