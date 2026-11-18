#if ARENA_TESTS
using System;
using System.Numerics;
using System.Text;
namespace ChipArena.UnityConversion
{
    // Zombies running and turning at once (owner 2026-11-18: "The issue appears to occur specifically when zombies are running and
    // turning simultaneously. Their legs begin moving unnaturally, potentially twisting, shuffling, sliding, or otherwise becoming
    // distorted during directional changes"; docs/ZOMBIE-LOCOMOTION.md "Running and turning"). The host's 60 Hz authority (its
    // velocity and facing set each tick toward the pilot, as StepPveHorde does) through the presented root (Core/NativeHordeFlow), the
    // gait and the model's binding, drawn at 120 frames a second; every number is the model's legs as drawn. `--zombie-runturn`
    // prints them; the checks hold the fix.
    internal static class ZombieRunTurnChecks
    {
        private const float Deg=57.29578f,Sim=1/60f,Frame=1/120f;
        private static float Wrap(float d){d%=360;if(d>180)d-=360;if(d<-180)d+=360;return d;}
        private static float YawOf(Vector2 v)=>(float)Math.Atan2(v.X,v.Y)*Deg;
        /// <summary>One route: the authority's wanted direction (Unity world, horizontal) and speed at sim time `t`, from the add at `at`;
        /// `attack` the seconds since a blow started (NaN: none).</summary>
        internal delegate void Route(float t,Vector2 at,out Vector2 dir,out float speed,out float attack);
        internal sealed class Result
        {
            public float Skate,SkateSum,Spin,KneeFoot,HipFoot,SwingSnap,Cross=9,HipsLow=9,HipsHigh=-9,HipsRate,KneeMost,Sunk,Steps,Seconds,Off,Lift;public int SkateN,Frames;
            public string Row(string name)=>string.Format("{0,-30}{1,6:0.0}{2,6:0.00}{3,6:0.0}{4,6:0}{5,6:0}{6,6:0.0}{7,7:0.00}{8,6:0.00}{9,6:0.00}{10,6:0.0}{11,6:0}{12,6:0.00}{13,6:0.0}{14,6:0}{15,6:0.00}",
                name,Skate,SkateN>0?SkateSum/Frames*120:0,Spin,KneeFoot,HipFoot,SwingSnap,Cross,HipsLow,HipsHigh-HipsLow,HipsRate,KneeMost,Sunk,Steps/Math.Max(1e-3f,Seconds),Off,Lift);
        }
        internal const string Header="                                skate slide  spin kneeF  hipF swing  cross  hips  bob  hipV  knee  sunk steps  off  lift";
        internal const string Header2="                                 cm   cm/s deg/f  deg   deg  deg/f    m    legs  legs  l/s   deg   rig   /s   deg  legs";
        /// <summary>Runs `route` for `seconds` on kind `kind` (id 40003's takes unless `id`), measuring from `from` s.</summary>
        internal static Result Drive(NativeVisualRig.Rig rig,int kind,Route route,float seconds,float from,int id=40003,Action<float,NativeHordeGait,NativeVisualRig.State,HordeMotionInput> each=null)
        {
            NativeZombieMotion.Warm();var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);
            var st=NativeHordeKinds.ProposedRow(kind);float leg=rig.LegUpper+rig.LegLower;
            var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.AttackOf(kind,st.attack,false),Side=1};
            float floor;{var s0=new NativeVisualRig.State(rig);var rest=new CapturedRig.Pose();rest.Reset();rest.World();NativeVisualRig.Pose(s0,rest,default(NativeVisualRig.Grasp),false,false);floor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            var body=new Vector2(0,0);var vel=Vector2.Zero;float yaw=0,attack=float.NaN,simT=0;var r=new Result();
            var lastAnkle=new Vector2[2];var lastYaw=new float[2];var lastFwd=new Vector3[2];var lastUp=new Vector3[2];var lastPlanted=new bool[2];bool have=false;float lastHips=0;
            int frames=(int)(seconds/Frame);
            for(int i=0;i<=frames;i++)
            {
                float t=i*Frame;
                // (The authority's 60 Hz tick: its velocity and facing set at once, its body carried by them.)
                while(simT<=t+1e-5f)
                {
                    route(simT,body,out var dir,out float speed,out attack);
                    if(dir.LengthSquared()>1e-6f){dir=Vector2.Normalize(dir);yaw=YawOf(dir);}vel=dir*speed;body+=vel*Sim;simT+=Sim;
                }
                float simAt=simT-Sim;bool winding=!float.IsNaN(attack)&&attack>=0&&attack<f.Strike;
                flow.Host(t,simAt,new Vector3(body.X,.85f,body.Y),new Vector3(vel.X,0,vel.Y),true,yaw,Frame,winding);
                f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;f.AttackAge=float.IsNaN(attack)?float.NaN:attack+(t-simAt);
                g.Pose(f,p,null);p.World();
                NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);s.Dt=Frame;NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                each?.Invoke(t,g,s,f);
                if(t<from){for(int a=0;a<2;a++){lastPlanted[a]=false;}have=false;continue;}
                r.Frames++;r.Seconds+=Frame;r.Off=Math.Max(r.Off,Math.Abs(Wrap(g.Yaw-YawOf(new Vector2(f.Velocity.X,f.Velocity.Z))))*(g.Speed>1?1:0));
                Vector2 W(Vector3 l){var w=g.ToWorld(l,f.Root);return new Vector2(w.X,w.Z);}
                float DirYaw(Vector3 l){var a=W(l)-W(Vector3.Zero);return YawOf(a);}
                var pelvis=s.Position[rig.Of[CapturedRig.Pelvis]];float hips=(pelvis.Y-floor)/leg;r.HipsLow=Math.Min(r.HipsLow,hips);r.HipsHigh=Math.Max(r.HipsHigh,hips);if(have)r.HipsRate=Math.Max(r.HipsRate,Math.Abs(hips-lastHips)/Frame);lastHips=hips;
                float pelvisYaw=DirYaw(Vector3.Cross(s.Position[rig.Of[CapturedRig.LHip]]-s.Position[rig.Of[CapturedRig.RHip]],Vector3.UnitY));
                var la=g.ToLocal(g.FootWorld[0],f.Root);var ra=g.ToLocal(g.FootWorld[1],f.Root);r.Cross=Math.Min(r.Cross,la.X-ra.X);r.Sunk=Math.Max(r.Sunk,g.Sunk);
                for(int a=0;a<2;a++)
                {
                    int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],k=s.Position[rig.Of[hip+1]],an=s.Position[rig.Of[hip+2]];
                    int toe=rig.Of[a==0?CapturedRig.LToe:CapturedRig.RToe];var ankle=W(toe>=0?s.Position[toe]:an);var fwd=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[hip+2]);var up=Vector3.Transform(Vector3.UnitY,p.WorldRotation[hip+2]);
                    Vector3 W3(Vector3 d){var w=W(d)-W(Vector3.Zero);return Vector3.Normalize(new Vector3(w.X,d.Y,w.Y));}var fw=W3(fwd);var uw=W3(up);float flat=new Vector2(fwd.X,fwd.Z).Length();fwd.Y=0;bool aimed=flat>.4f;float footYaw=aimed?DirYaw(fwd):lastYaw[a];
                    bool planted=!g.FootAir[a]&&(g.Landed&(1<<a))==0;
                    float kneeDeg=180-(float)(Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(h-k),Vector3.Normalize(an-k)))))*Deg);r.KneeMost=Math.Max(r.KneeMost,kneeDeg);
                    r.Lift=Math.Max(r.Lift,(an.Y-floor)/leg*(planted?1:0));
                    if(planted)
                    {
                        if(have&&lastPlanted[a]){float d=(ankle-lastAnkle[a]).Length()*100;r.Skate=Math.Max(r.Skate,d);r.SkateSum+=d;r.SkateN++;r.Spin=Math.Max(r.Spin,Math.Abs(Wrap(footYaw-lastYaw[a])));}
                        // (The knee over the foot: the knee's bend off the hip-ankle line, flat, against the foot's facing; and the foot off the hips' facing.)
                        var line=Vector3.Normalize(an-h);var bend=k-h;bend-=line*Vector3.Dot(bend,line);bend.Y=0;
                        if(bend.Length()>.02f)r.KneeFoot=Math.Max(r.KneeFoot,Math.Abs(Wrap(DirYaw(bend)-footYaw)));
                        r.HipFoot=Math.Max(r.HipFoot,Math.Abs(Wrap(footYaw-pelvisYaw)));
                    }
                    else if(have&&!lastPlanted[a]&&g.FootAir[a]){float A(Vector3 x,Vector3 y)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(x,y))))*Deg;r.SwingSnap=Math.Max(r.SwingSnap,Math.Max(A(fw,lastFwd[a]),A(uw,lastUp[a])));}
                    if((g.Landed&(1<<a))!=0)r.Steps++;
                    lastAnkle[a]=ankle;lastYaw[a]=footYaw;lastFwd[a]=fw;lastUp[a]=uw;lastPlanted[a]=planted||(g.Landed&(1<<a))!=0;
                }
                have=true;
            }
            return r;
        }
        // ---- the routes (the authority's chase: StepPveHorde turns the facing and the velocity at once toward where it goes) ----
        private static Route Straight(float speed)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{dir=new Vector2(0,1);sp=speed;atk=float.NaN;};
        // Stands, then runs straight off at `at0` (a blow's recovery, the authority's stop and go).
        private static Route SetOff(float speed,float at0=2)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{dir=new Vector2(0,1);sp=t>=at0?speed:0;atk=float.NaN;};
        private static Route Turn(float speed,float deg,float at0=3)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=(t>=at0?deg:0)/Deg;dir=new Vector2((float)Math.Sin(a),(float)Math.Cos(a));sp=speed;atk=float.NaN;};
        private static Route Arc(float speed,float degPerSecond,float at0=3)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=Math.Max(0,t-at0)*degPerSecond/Deg;dir=new Vector2((float)Math.Sin(a),(float)Math.Cos(a));sp=speed;atk=float.NaN;};
        // A pilot circling (radius `radius` m about a point 6 m ahead, at `pilot` m/s), the add running at it.
        private static Route Circle(float speed,float radius,float pilot)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=t*pilot/radius;var c=new Vector2(0,6);var target=c+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*radius;dir=target-at;sp=dir.Length()>1.1f?speed:0;atk=float.NaN;};
        // Two pilots: it runs at one, then at `at0` the other (120 degrees to its right) is nearer.
        private static Route Switch(float speed,float at0=3)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{var target=t<at0?new Vector2(0,60):at+new Vector2((float)Math.Sin(120/Deg),(float)Math.Cos(120/Deg))*40;dir=target-at;sp=speed;atk=float.NaN;};
        // Runs, turns 90 degrees onto a pilot, and swings at it .3 s later (the wind-up's lunge, then it stands through the recovery).
        private static Route TurnAttack(int kind,float speed,float at0=3)
        {
            var st=NativeHordeKinds.ProposedRow(kind);float hit=NativeHordeKinds.Strike(st,false,0),rec=NativeHordeKinds.Recover(st,false,0),swing=at0+.3f;
            return (float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{dir=t<at0?new Vector2(0,1):new Vector2(1,0);sp=speed;atk=float.NaN;
                if(t>=swing){atk=t-swing;sp=atk<hit&&kind!=NativeHordeKinds.Spitter?st.lunge/Math.Max(.05f,hit):0;if(atk>rec+.4f){atk=float.NaN;sp=speed;}}};
        }
        // A crowd's route round an obstacle: the nav's grid corners and the separation's shoves (a seeded jitter of up to 35 degrees, 4 a second).
        private static Route Weave(float speed,uint seed)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{int n=(int)(t*4);float j=(float)(NativeGoreRules.Hash01(seed,n,7)-.5)*70,leg=((int)(t/1.2f)%2==0?35:-35);float a=(leg+j)/Deg;dir=new Vector2((float)Math.Sin(a),(float)Math.Cos(a));sp=speed;atk=float.NaN;};
        internal static (string name,Route route,float seconds,float from)[] Scenarios(int kind)
        {
            var st=NativeHordeKinds.ProposedRow(kind);float run=st.run,walk=st.walk;
            return new[]{("run straight",Straight(run),6f,3f),("run set off",SetOff(run),4.5f,1.9f),("run arc 90/s",Arc(run,90),6f,3.2f),("run arc 180/s",Arc(run,180),6f,3.2f),("run turn 90",Turn(run,90),5f,2.9f),("run turn 180",Turn(run,180),5f,2.9f),
                ("run circle chase",Circle(run,2.5f,run*1.15f),9f,4f),("run target switch",Switch(run),5f,2.9f),("run turn into attack",TurnAttack(kind,run),5.5f,2.9f),("run weave",Weave(run,7),9f,3f),
                ("walk straight",Straight(walk),6f,3f),("walk turn 90",Turn(walk,90),5f,2.9f),("walk turn 180",Turn(walk,180),5.5f,2.9f),("walk circle chase",Circle(walk,1.8f,walk*1.15f),9f,4f)};
        }
        internal static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute};
        // ---- the regression checks ----
        private static int checks;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie run and turn: "+what);checks++;}
        /// <summary>
        /// Running and turning at once on the model's legs, every modelled biped kind (`--zombie-runturn-checks`, and the full run):
        /// the hips stay up through gradual and sharp running turns instead of dipping into a squat; the knee rides over its planted foot;
        /// a swinging foot turns smoothly; the model's planted ankle stands on the gait's footprint, across the floor as well as fore and
        /// aft; a landing foot is turned on into the turn; the straight run keeps its knees over its feet. Numbers on a synthetic
        /// skeleton: how it reads is the Editor captures' to show.
        /// </summary>
        public static void Run(string root)
        {
            checks=0;var rig=ModelPoseChecks.Measured();float worstDrop=0,meanDrop=0,worstKnee=0,worstSwing=0,worstHipRate=0;int arcs=0;
            foreach(int kind in Kinds)
            {
                var sc=Scenarios(kind);string Name(string n)=>NativeHordeKinds.Ids[kind]+" "+n;
                Result Of(string n){foreach(var x in sc)if(x.name==n)return Drive(rig,kind,x.route,x.seconds,x.from);throw new Exception(n);}
                var straight=Of("run straight");
                // (Running on a curve or at a pilot circling, the hips as high as running straight: the rig no longer sinks its hips to reach
                // a foot the turn left beside it, NativeHordeLoco.Squeeze, which the model's hips used to drop with.)
                foreach(var n in new[]{"run arc 90/s","run arc 180/s","run circle chase","run turn 90"})
                {
                    var r=Of(n);float drop=straight.HipsLow-r.HipsLow;worstDrop=Math.Max(worstDrop,drop);meanDrop+=drop;arcs++;worstHipRate=Math.Max(worstHipRate,r.HipsRate);
                    if(n!="run turn 90")Check(drop<=.11f,Name(n)+": the hips stay up through the turn (lowest "+r.HipsLow.ToString("0.00")+" legs, running straight "+straight.HipsLow.ToString("0.00")+")");
                    Check(r.HipsRate<=8,Name(n)+": the hips never drop or pop (fastest "+r.HipsRate.ToString("0.0")+" legs/s)");
                }
                // (The knee over its planted foot, and a planted foot's ball kept where it stands, through every running turn.)
                foreach(var n in new[]{"run arc 90/s","run arc 180/s","run turn 90","run turn 180","run circle chase","run target switch","run turn into attack"})
                {
                    var r=Of(n);worstKnee=Math.Max(worstKnee,r.KneeFoot);
                    Check(r.KneeFoot<=30,Name(n)+": the knee rides over its planted foot (at most "+r.KneeFoot.ToString("0")+" deg off it)");
                    Check(r.Skate<=1.2f,Name(n)+": a planted foot's ball stays put ("+r.Skate.ToString("0.0")+" cm a frame at most)");
                }
                // (A swinging foot takes its twist over the capture's turn, so it never spins straight as it lifts.)
                foreach(var n in new[]{"walk turn 90","walk turn 180","walk circle chase","run turn 180","run target switch"})
                {
                    var r=Of(n);worstSwing=Math.Max(worstSwing,r.SwingSnap);
                    Check(r.SwingSnap<=30,Name(n)+": a swinging foot turns smoothly ("+r.SwingSnap.ToString("0.0")+" deg in a 120th of a second at most)");
                }
                Check(straight.KneeFoot<=20&&straight.Cross>.05f,Name("run straight")+": running straight the knees over the feet, the feet apart ("+straight.KneeFoot.ToString("0")+" deg, "+straight.Cross.ToString("0.00")+" m)");
            }
            meanDrop/=Math.Max(1,arcs);Check(meanDrop<=.04f,"through the running turns the hips on average as high as running straight (lower by "+meanDrop.ToString("0.000")+" legs)");
            // ---- a sharp running turn at any moment of the stride: the trailing foot steps before the body runs on over it ----
            // (Eight starts across a stride, either way: a foot the body ran sideways off steps as one out of reach does, NativeHordeGait.AcrossMost.)
            float worstSharp=9;
            foreach(int kind in Kinds)foreach(float deg in new[]{90f,-90f})
            {
                var st=NativeHordeKinds.ProposedRow(kind);float low=9;
                for(int k=0;k<8;k++){float at0=3+k*.071f;var r=Drive(rig,kind,Turn(st.run,deg,at0),at0+2.2f,at0-.1f);low=Math.Min(low,r.HipsLow);}
                worstSharp=Math.Min(worstSharp,low);
                Check(low>=.89f,NativeHordeKinds.Ids[kind]+" turning "+deg.ToString("0")+" degrees at a run: the hips stay up whenever in the stride it turns (lowest "+low.ToString("0.00")+" legs over eight starts)");
            }
            // ---- the model's planted ankle on the gait's footprint, through a turn (both ways across the floor) ----
            {
                float gap=0;int seen=0;
                Drive(rig,NativeHordeKinds.Runner,Arc(NativeHordeKinds.ProposedRow(NativeHordeKinds.Runner).run,180),5f,3.2f,40003,(t,g,s,f)=>
                {
                    if(t<3.2f)return;
                    for(int a=0;a<2;a++)
                    {
                        if(g.FootAir[a]||g.Heel[a]>0||(g.Landed&(1<<a))!=0)continue;int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;
                        var ma=s.Position[rig.Of[hip+2]];var fp=g.ToLocal(g.FootWorld[a],f.Root);gap=Math.Max(gap,new Vector2(ma.X-fp.X,ma.Z-fp.Z).Length());seen++;
                    }
                });
                Check(seen>20&&gap<.01f,"running round a curve, the model's flat planted ankle stands on the gait's footprint ("+(gap*100).ToString("0.00")+" cm at most over "+seen+" foot-frames)");
            }
            // ---- a landing foot turned on into the turn: half the stance's turn, the way the body turns ----
            {
                float most=0,least=0;
                foreach(float rate in new[]{120f,-120f})
                {
                    float lead=0;
                    Drive(rig,NativeHordeKinds.Shambler,Arc(NativeHordeKinds.ProposedRow(NativeHordeKinds.Shambler).run,rate),5f,3.5f,40003,(t,g,s,f)=>
                    {
                        if(t<3.5f)return;for(int a=0;a<2;a++)if((g.Landed&(1<<a))!=0){float d=Wrap(g.FootTurn(a)-g.Yaw);lead=rate>0?Math.Max(lead,d):Math.Min(lead,d);}
                    });
                    if(rate>0)most=lead;else least=lead;
                }
                Check(most>3&&most<=NativeHordeGait.LeadMost+.01f&&least<-3&&least>=-NativeHordeGait.LeadMost-.01f,"a landing foot is turned on into the turn, either way (right "+most.ToString("0.0")+", left "+least.ToString("0.0")+" deg, at most "+NativeHordeGait.LeadMost+")");
            }
            // ---- the rig and the binding agree: a footprint drawn in for the rig's legs and spread back out is where it was ----
            {
                Check(Math.Abs(NativeHordeGait.KneeFollow-1)<1e-6f,"the knee follows its foot's whole twist");
                var src=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ChipArena/Core/NativeHordeLoco.cs"));
                Check(src.Contains("at.X=hip.X+(at.X-hip.X)/legsNow")&&src.Contains("at.X=hip.X+(at.X-hip.X)*legsNow"),"the gait draws a footprint in and spreads it out across the floor as well as fore and aft");
                var vr=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ChipArena/Core/NativeVisualRig.cs"));
                Check(vr.Contains("a.X=h.X+(a.X-h.X)*s.Stride; a.Z=h.Z+(a.Z-h.Z)*s.Stride;"),"the model's binding spreads the game ankle back out both ways (NativeVisualRig.GameAnkle)");
            }
            Console.WriteLine("PASS Zombie run and turn ("+checks+" checks: 4 kinds on the model's legs; running turns' hips at most "+worstDrop.ToString("0.00")+" legs under the straight run's (mean "+meanDrop.ToString("0.000")+"), fastest "+worstHipRate.ToString("0.0")+" legs/s; sharp turns at any moment of the stride at least "+worstSharp.ToString("0.00")+" legs; knee off its planted foot at most "+worstKnee.ToString("0")+" deg; swinging foot at most "+worstSwing.ToString("0.0")+" deg a 120th; presentation only)");
        }
        /// <summary>`--zombie-runturn`: the table, every kind on the model through every route.</summary>
        public static void Probe()
        {
            var rig=ModelPoseChecks.Measured();var o=new StringBuilder();o.AppendLine(Header);o.AppendLine(Header2);
            foreach(int kind in Kinds)foreach(var sc in Scenarios(kind)){var r=Drive(rig,kind,sc.route,sc.seconds,sc.from);o.AppendLine(r.Row(NativeHordeKinds.Ids[kind]+" "+sc.name));}
            Console.WriteLine(o.ToString());
        }
        /// <summary>`--zombie-runturn-trace kind scenario`: one route frame by frame.</summary>
        /// <summary>`--zombie-runturn-sweep`: the sharp running turns started at eight moments of the stride (the worst of them; diagnostic).</summary>
        public static void Sweep()
        {
            var rig=ModelPoseChecks.Measured();var o=new StringBuilder();o.AppendLine(Header);o.AppendLine(Header2);
            foreach(int kind in Kinds)
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(var (name,mk) in new (string,Func<float,Route>)[]{("run turn 90",a=>Turn(st.run,90,a)),("run turn -90",a=>Turn(st.run,-90,a)),("run turn 180",a=>Turn(st.run,180,a)),("run target switch",a=>Switch(st.run,a)),("walk turn 90",a=>Turn(st.walk,90,a)),("walk turn 180",a=>Turn(st.walk,180,a))})
                {
                    var w=new Result{Cross=9,HipsLow=9,HipsHigh=-9};
                    for(int k=0;k<8;k++)
                    {
                        float at0=3+k*.071f;var r=Drive(rig,kind,mk(at0),at0+2.2f,at0-.1f);
                        w.Skate=Math.Max(w.Skate,r.Skate);w.KneeFoot=Math.Max(w.KneeFoot,r.KneeFoot);w.HipFoot=Math.Max(w.HipFoot,r.HipFoot);w.SwingSnap=Math.Max(w.SwingSnap,r.SwingSnap);w.Cross=Math.Min(w.Cross,r.Cross);
                        w.HipsLow=Math.Min(w.HipsLow,r.HipsLow);w.HipsHigh=Math.Max(w.HipsHigh,r.HipsHigh);w.HipsRate=Math.Max(w.HipsRate,r.HipsRate);w.KneeMost=Math.Max(w.KneeMost,r.KneeMost);w.Sunk=Math.Max(w.Sunk,r.Sunk);w.Off=Math.Max(w.Off,r.Off);w.Lift=Math.Max(w.Lift,r.Lift);
                        w.Steps+=r.Steps;w.Seconds+=r.Seconds;w.SkateSum+=r.SkateSum;w.SkateN+=r.SkateN;w.Frames+=r.Frames;
                    }
                    o.AppendLine(w.Row(NativeHordeKinds.Ids[kind]+" "+name));
                }
            }
            Console.WriteLine(o.ToString());
        }
        /// <summary>`--zombie-runturn-cost`: the gait's and the model binding's time a frame (microseconds) through the circle chase (diagnostic).</summary>
        public static void Cost()
        {
            var rig=ModelPoseChecks.Measured();
            foreach(int kind in Kinds)
            {
                var st=NativeHordeKinds.ProposedRow(kind);var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);
                var f=new HordeMotionInput{Kind=kind,Id=40003,Seed=NativeHordeKinds.Seed(40003),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};
                var route=Circle(st.run,2.5f,st.run*1.15f);var body=Vector2.Zero;var sw=new System.Diagnostics.Stopwatch();var sb=new System.Diagnostics.Stopwatch();int n=0;
                for(int i=0;i<24000;i++)
                {
                    float t=i*Frame;route(t,body,out var dir,out float sp,out _);if(dir.LengthSquared()>1e-6f)dir=Vector2.Normalize(dir);var vel=dir*sp;body+=vel*Frame;
                    flow.Host(t,t,new Vector3(body.X,.85f,body.Y),new Vector3(vel.X,0,vel.Y),true,YawOf(dir),Frame,false);
                    f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;
                    bool timed=i>=4000;if(timed)sw.Start();g.Pose(f,p,null);if(timed)sw.Stop();
                    p.World();if(timed)sb.Start();NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow);s.Dt=Frame;NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);if(timed){sb.Stop();n++;}
                }
                Console.WriteLine(string.Format("{0,-10} gait {1:0.00} us, fit+bind {2:0.00} us a frame",NativeHordeKinds.Ids[kind],sw.Elapsed.TotalMilliseconds*1000/n,sb.Elapsed.TotalMilliseconds*1000/n));
            }
        }
        internal static float TraceFrom=0,TraceFor=1.6f;internal static int TracePhase=-1;
        public static void Trace(int kind,string scenario)
        {
            var rig=ModelPoseChecks.Measured();float leg=rig.LegUpper+rig.LegLower;
            float floor;{var s0=new NativeVisualRig.State(rig);var rest=new CapturedRig.Pose();rest.Reset();rest.World();NativeVisualRig.Pose(s0,rest,default(NativeVisualRig.Grasp),false,false);floor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            var list=Scenarios(kind);
            if(TracePhase>=0){var st=NativeHordeKinds.ProposedRow(kind);float at0=3+TracePhase*.071f;Route r=scenario=="run turn 90"?Turn(st.run,90,at0):scenario=="run turn -90"?Turn(st.run,-90,at0):scenario=="run turn 180"?Turn(st.run,180,at0):Switch(st.run,at0);list=new[]{(scenario,r,at0+2.2f,at0-.1f)};}
            foreach(var sc in list)
            {
                if(sc.Item1!=scenario)continue;int n=0;
                Drive(rig,kind,sc.route,sc.seconds,sc.from,40003,(t,g,s,f)=>
                {
                    if(t<sc.from-.1f+TraceFrom||t>sc.from+TraceFrom+TraceFor||n++%2!=0)return;
                    string Foot(int a)
                    {
                        int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;Vector3 h=s.Position[rig.Of[hip]],k=s.Position[rig.Of[hip+1]],an=s.Position[rig.Of[hip+2]];
                        var line=Vector3.Normalize(an-h);var bend=k-h;bend-=line*Vector3.Dot(bend,line);float kneeYaw=(float)Math.Atan2(-bend.X,bend.Z)*Deg;
                        return string.Format("{0} x{1,5:0.00} z{2,5:0.00} y{3,4:0.00} tw{4,4:0} kn{5,4:0} hl{6:0.0}",g.FootAir[a]?"air":"pln",(an.X-h.X)/leg,(an.Z-h.Z)/leg,(an.Y-floor)/leg,Wrap(g.FootTurn(a)-g.Yaw),kneeYaw,g.Heel[a]);
                    }
                    var la=g.ToLocal(g.FootWorld[0],f.Root);var ra=g.ToLocal(g.FootWorld[1],f.Root);float vy=(float)Math.Atan2(f.Velocity.X,f.Velocity.Z)*Deg;
                    Console.WriteLine(string.Format("{0:0.000} yaw{1,6:0} go{10,5:0} v{2:0.0} run{3:0.0} | L {4} | R {5} | cross {11,5:0.00} sunk {6:0.00} hips {7:0.00} raise {8:0.00} stand {9:0.00}",
                        t-sc.from,g.Yaw,g.Speed,g.RunNow,Foot(0),Foot(1),g.Sunk,(s.Position[rig.Of[CapturedRig.Pelvis]].Y-floor)/leg,s.Raise,s.Stand,vy,la.X-ra.X));
                });
            }
        }
    }
}
#endif
