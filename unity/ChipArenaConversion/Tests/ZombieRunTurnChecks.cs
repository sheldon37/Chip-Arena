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
            public float Skate,SkateSum,Spin,KneeFoot,HipFoot,SwingSnap,SnapLand,Cross=9,HipsLow=9,HipsHigh=-9,HipsRate,KneeMost,Sunk,Steps,Seconds,Off,Lift;public int SkateN,Frames;
            public string Row(string name)=>string.Format("{0,-30}{1,6:0.0}{2,6:0.00}{3,6:0.0}{4,6:0}{5,6:0}{6,6:0.0}{7,7:0.00}{8,6:0.00}{9,6:0.00}{10,6:0.0}{11,6:0}{12,6:0.00}{13,6:0.0}{14,6:0}{15,6:0.00}",
                name,Skate,SkateN>0?SkateSum/Frames*120:0,Spin,KneeFoot,HipFoot,SwingSnap,Cross,HipsLow,HipsHigh-HipsLow,HipsRate,KneeMost,Sunk,Steps/Math.Max(1e-3f,Seconds),Off,Lift);
        }
        internal const string Header="                                skate slide  spin kneeF  hipF swing  cross  hips  bob  hipV  knee  sunk steps  off  lift";
        internal const string Header2="                                 cm   cm/s deg/f  deg   deg  deg/f    m    legs  legs  l/s   deg   rig   /s   deg  legs";
        /// <summary>Runs `route` for `seconds` on kind `kind` (id 40003's takes unless `id`), measuring from `from` s.</summary>
        internal static CapturedRig.Pose DrivePose;
        internal static Result Drive(NativeVisualRig.Rig rig,int kind,Route route,float seconds,float from,int id=40003,Action<float,NativeHordeGait,NativeVisualRig.State,HordeMotionInput> each=null)
        {
            NativeZombieMotion.Warm();var g=new NativeHordeGait{Legs=rig.LegRatio};var p=new CapturedRig.Pose();var fit=new CapturedRig.Pose();var s=new NativeVisualRig.State(rig);var flow=new HordeFlow();flow.Configure(kind);
            // (As the runtime draws it: the root at the kind's height, the gait in its own units (the Hulk is drawn 1.8 times its rig), the
            // Hound fitted on all fours.)
            float scale=NativeHordeKinds.Scale[NativeHordeKinds.Index(kind)],rootY=NativeHordeKinds.DrawRoot(kind,false);bool quad=NativeBackroomsMotion.Quad(kind);
            var st=NativeHordeKinds.ProposedRow(kind);float leg=rig.LegUpper+rig.LegLower;
            var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.AttackOf(kind,st.attack,false),Side=1};
            float floor;{var s0=new NativeVisualRig.State(rig);var rest=new CapturedRig.Pose();rest.Reset();rest.World();NativeVisualRig.Pose(s0,rest,default(NativeVisualRig.Grasp),false,false);floor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            var body=new Vector2(0,0);var vel=Vector2.Zero;float yaw=0,attack=float.NaN,simT=0;var r=new Result();
            var lastAnkle=new Vector2[2];var lastYaw=new float[2];var lastFwd=new Vector3[2];var lastUp=new Vector3[2];var lastPlanted=new bool[2];var lastAir=new bool[2];bool have=false;float lastHips=0;
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
                flow.Host(t,simAt,new Vector3(body.X,rootY,body.Y),new Vector3(vel.X,0,vel.Y),true,yaw,Frame,winding);
                f.Time=t;f.Root=flow.Position/scale;f.Velocity=flow.Velocity/scale;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;f.AttackAge=float.IsNaN(attack)?float.NaN:attack+(t-simAt);
                g.Pose(f,p,null);p.World();
                NativeZombiePosture.Fit(p,fit,!quad);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow,g.Outreach,g.LegsAcross);s.Dt=Frame;NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                DrivePose=p;each?.Invoke(t,g,s,f);
                if(t<from){for(int a=0;a<2;a++){lastPlanted[a]=false;lastAir[a]=false;}have=false;continue;}
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
                    // (A swinging foot's turn, its lift and landing frames included: in the air this frame or the last.)
                    if(have&&(g.FootAir[a]||lastAir[a])){float A(Vector3 x,Vector3 y)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(x,y))))*Deg;float turn=Math.Max(A(fw,lastFwd[a]),A(uw,lastUp[a]));r.SwingSnap=Math.Max(r.SwingSnap,turn);if(lastAir[a]&&!g.FootAir[a])r.SnapLand=Math.Max(r.SnapLand,turn);}
                    if((g.Landed&(1<<a))!=0)r.Steps++;
                    lastAnkle[a]=ankle;lastYaw[a]=footYaw;lastFwd[a]=fw;lastUp[a]=uw;lastPlanted[a]=planted||(g.Landed&(1<<a))!=0;lastAir[a]=g.FootAir[a];
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
        private static Route Circle(float speed,float radius,float pilot,float a0=0)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=a0+t*pilot/radius;var c=new Vector2(0,6);var target=c+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*radius;dir=target-at;sp=dir.Length()>1.1f?speed:0;atk=float.NaN;};
        // Turning at `rate` from `at0`, stopping and setting off every `period` s (a crowd's shove, a blocked path, a blow's recovery).
        internal static Route StopGo(float speed,float rate,float period,float at0=3)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=Math.Max(0,t-at0)*rate/Deg;dir=new Vector2((float)Math.Sin(a),(float)Math.Cos(a));sp=t<at0||(int)((t-at0)/period)%2==0?speed:0;atk=float.NaN;};
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
            checks=0;var rig=ModelPoseChecks.Measured();
            // (Each pairing's own values on 2026-11-18, Tests/zombie-runturn-bounds.txt: sampled points, one add of each pairing from one start,
            // not a population's worst; held within a few degrees and .005 legs, so a change to one pairing shows.)
            var pins=new System.Collections.Generic.Dictionary<string,float[]>();var floors=new System.Collections.Generic.Dictionary<string,float[]>();var inv0=System.Globalization.CultureInfo.InvariantCulture;
            foreach(var line in System.IO.File.ReadAllLines(System.IO.Path.Combine(root,"Tests/zombie-runturn-bounds.txt")))
            {
                if(line.Length==0||line[0]=='#')continue;
                if(line.StartsWith("FLOORS ")){var c=line.Substring(7).Split('|');floors[c[0]+"|"+c[1]]=new[]{float.Parse(c[2],inv0),float.Parse(c[3],inv0)};}
                else{var c=line.Split('|');pins[c[0]+"|"+c[1]+"|"+c[2]]=new[]{float.Parse(c[3],inv0),float.Parse(c[4],inv0),float.Parse(c[5],inv0)};}
            }
            float worstDrop=0,meanDrop=0,worstKnee=0,worstSwing=0,worstHipRate=0,worstHipFoot=0,worstRate=0,worstCircle=9;int arcs=0,pairs=0;
            // (Every pairing of a kind's walk and run takes, IdsOf: the bounds hold for the worst of them.)
            foreach(int kind in Kinds)foreach(int id in IdsOf(kind))
            {
                pairs++;var sc=Scenarios(kind);string Name(string n)=>NativeHordeKinds.Ids[kind]+" #"+id+" "+n;var seen=new System.Collections.Generic.Dictionary<string,Result>();
                Result Of(string n){if(seen.TryGetValue(n,out var had))return had;foreach(var x in sc)if(x.name==n)return seen[n]=Drive(rig,kind,x.route,x.seconds,x.from,id);throw new Exception(n);}
                var straight=Of("run straight");float rate0=straight.Steps/Math.Max(1e-3f,straight.Seconds);
                // (Running on a curve or at a pilot circling, the hips as high as running straight. The review measured either of two changes
                // holding these gradual turns alone: the footprint drawn in across the floor while it runs (NativeHordeLoco.Squeeze), so the rig
                // no longer sinks its hips to reach a foot the turn left beside it, which the model's hips dropped with; or a foot left that
                // far aside stepping as one out of reach does (NativeHordeGait.AcrossMost). The sharp turns, below, need the second.)
                foreach(var n in new[]{"run arc 90/s","run arc 180/s","run circle chase","run turn 90"})
                {
                    var r=Of(n);float drop=straight.HipsLow-r.HipsLow;worstDrop=Math.Max(worstDrop,drop);meanDrop+=drop;arcs++;worstHipRate=Math.Max(worstHipRate,r.HipsRate);
                    if(n!="run turn 90")Check(drop<=.11f,Name(n)+": the hips stay up through the turn (lowest "+r.HipsLow.ToString("0.00")+" legs, running straight "+straight.HipsLow.ToString("0.00")+")");
                    Check(r.HipsRate<=8,Name(n)+": the hips never drop or pop (fastest "+r.HipsRate.ToString("0.0")+" legs/s)");
                }
                // (The circle chase from four places round the pilot's circle: at one, as it settles into the chase running 30 degrees off its
                // facing, a stride sinks about as far as at a reversal's worst moment, the accepted CircleFloor: "Still open".)
                for(int j=1;j<4;j++)
                {
                    var st=NativeHordeKinds.ProposedRow(kind);var r=Drive(rig,kind,Circle(st.run,2.5f,st.run*1.15f,j*1.57f),9f,4f,id);worstCircle=Math.Min(worstCircle,r.HipsLow);
                    Check(r.HipsLow>=CircleFloor&&r.HipsRate<=8,Name("run circle chase")+" from a quarter "+j+" round: the hips stay up (lowest "+r.HipsLow.ToString("0.00")+" legs, floor "+CircleFloor+", fastest "+r.HipsRate.ToString("0.0")+" legs/s)");
                }
                // (The knee over its planted foot, a planted foot's ball kept where it stands and the foot turned no further from the hips than
                // a planted foot shows, NativeHordeGait.TwistMost; and no more steps than running straight, through every running turn.)
                foreach(var n in new[]{"run arc 90/s","run arc 180/s","run turn 90","run turn 180","run circle chase","run target switch","run turn into attack","run weave"})
                {
                    var r=Of(n);worstKnee=Math.Max(worstKnee,r.KneeFoot);
                    Check(r.KneeFoot<=KneeMost,Name(n)+": the knee rides over its planted foot (at most "+r.KneeFoot.ToString("0")+" deg off it; one add, one start)");
                    Check(r.Skate<=1.2f,Name(n)+": a planted foot's ball stays put ("+r.Skate.ToString("0.0")+" cm a frame at most)");
                    if(n=="run turn into attack")continue;
                    // (A reversal, or the weave's sudden shoves, turns the hips past a planted foot until it steps round: TwistMoving and the
                    // stance's turn on top.)
                    float hipMost=n=="run turn 180"||n=="run weave"?HipFootReverse:HipFootMost;worstHipFoot=Math.Max(worstHipFoot,r.HipFoot);
                    Check(r.HipFoot<=hipMost,Name(n)+": a planted foot stays turned with the hips (at most "+r.HipFoot.ToString("0")+" deg off their facing, "+hipMost+" allowed; one add, one start)");
                    // (The circle chase stops and sets off again by its pilot: a step or two more.)
                    float rate=r.Steps/Math.Max(1e-3f,r.Seconds),allowed=rate0*(n=="run circle chase"?1.15f:1.1f)+.05f;worstRate=Math.Max(worstRate,rate/Math.Max(1e-3f,rate0));
                    Check(rate<=allowed,Name(n)+": no more steps than running straight ("+rate.ToString("0.00")+" a second, running straight "+rate0.ToString("0.00")+")");
                }
                // (A swinging foot takes its twist over the capture's turn, so it never spins straight as it lifts; a quick step off a peeled
                // heel takes the capture's turn over SwingTurnEase at least; its lift and landing frames counted.)
                foreach(var n in new[]{"walk turn 90","walk turn 180","walk circle chase","run turn 180","run target switch"})
                {
                    var r=Of(n);worstSwing=Math.Max(worstSwing,r.SwingSnap);
                    Check(r.SwingSnap<=SwingMost,Name(n)+": a swinging foot turns smoothly ("+r.SwingSnap.ToString("0.0")+" deg in a 120th of a second at most; one add, one start)");
                }
                Check(straight.KneeFoot<=20&&straight.Cross>.05f,Name("run straight")+": running straight the knees over the feet, the feet apart ("+straight.KneeFoot.ToString("0")+" deg, "+straight.Cross.ToString("0.00")+" m)");
                // (Each bounded route against this pairing's own sampled values: the knee and the foot within PinDegrees, a swinging foot within
                // PinSwing and never past SwingMost.)
                foreach(var n in BoundRoutes)
                {
                    var r=Of(n);Check(pins.TryGetValue(NativeHordeKinds.Ids[kind]+"|"+id+"|"+n,out var pin),Name(n)+": its sampled values are pinned");
                    Check(r.KneeFoot<=pin[0]+PinDegrees&&r.HipFoot<=pin[1]+PinDegrees,Name(n)+": the knee and the planted foot as they were sampled (knee "+r.KneeFoot.ToString("0.0")+", pinned "+pin[0].ToString("0.0")+"; foot off the hips "+r.HipFoot.ToString("0.0")+", pinned "+pin[1].ToString("0.0")+" deg; one add, one start)");
                    Check(r.SwingSnap<=Math.Min(SwingMost,pin[2]+PinSwing),Name(n)+": a swinging foot as it was sampled ("+r.SwingSnap.ToString("0.0")+" deg a 120th, pinned "+pin[2].ToString("0.0")+", "+SwingMost+" at most; one add, one start)");
                }
            }
            meanDrop/=Math.Max(1,arcs);Check(meanDrop<=.04f,"through the running turns the hips on average as high as running straight (lower by "+meanDrop.ToString("0.000")+" legs)");
            // ---- a sharp running turn at any moment of the stride: the trailing foot steps before the body runs on over it ----
            // (Eight starts across a stride, either way, every take pairing: a foot the body ran sideways off steps as one out of reach does,
            // NativeHordeGait.AcrossMost. A reversal and a new target behind sink about as far as on shared at their worst moment, each
            // pairing held at its own sampled worst: "Still open".)
            float worstSharp=9,worstRev=9,worstSwitch=9;int unguarded=0;
            foreach(int kind in Kinds)foreach(int id in IdsOf(kind))
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                float Low(Func<float,Route> mk){float low=9;for(int k=0;k<8;k++){float at0=3+k*.071f;var r=Drive(rig,kind,mk(at0),at0+2.2f,at0-.1f,id);low=Math.Min(low,r.HipsLow);}return low;}
                foreach(float deg in new[]{90f,-90f})
                {
                    float low=Low(a0=>Turn(st.run,deg,a0));worstSharp=Math.Min(worstSharp,low);
                    Check(low>=.89f,NativeHordeKinds.Ids[kind]+" #"+id+" turning "+deg.ToString("0")+" degrees at a run: the hips stay up whenever in the stride it turns (lowest "+low.ToString("0.00")+" legs over eight starts)");
                }
                float rev=Low(a0=>Turn(st.run,180,a0)),sw=Low(a0=>Switch(st.run,a0));worstRev=Math.Min(worstRev,rev);worstSwitch=Math.Min(worstSwitch,sw);
                // (Floored at this pairing's own sampled worst less .005 legs; a floor at the hips' clamp, HipsClamp, cannot fail: that pairing's
                // route is unguarded at its worst moment, and says so.)
                Check(floors.TryGetValue(NativeHordeKinds.Ids[kind]+"|"+id,out var fl),NativeHordeKinds.Ids[kind]+" #"+id+": its sampled floors are pinned");
                float revFloor=fl[0]-.005f,swFloor=fl[1]-.005f;if(revFloor<=HipsClamp+.001f)unguarded++;if(swFloor<=HipsClamp+.001f)unguarded++;
                Check(rev>=revFloor,NativeHordeKinds.Ids[kind]+" #"+id+" reversing at a run: at its worst sampled moment the hips no lower than accepted (lowest "+rev.ToString("0.000")+" legs over eight starts, floor "+revFloor.ToString("0.000")+(revFloor<=HipsClamp+.001f?", at the hips' clamp: unguarded":"")+")");
                Check(sw>=swFloor,NativeHordeKinds.Ids[kind]+" #"+id+" switching to a target behind and to its right at a run: at its worst sampled moment the hips no lower than accepted (lowest "+sw.ToString("0.000")+" legs over eight starts, floor "+swFloor.ToString("0.000")+(swFloor<=HipsClamp+.001f?", at the hips' clamp: unguarded":"")+")");
            }
            // ---- stopping and setting off while it turns: the landing foot's turn never flicks ----
            // (The second review, 2026-11-18: the landing lead's stance switched with the planner's moving, so it jumped as a zombie stopped
            // or set off; eased by the move weight toward a cadence already zeroed, a landing foot turned 16 degrees in its landing frame (the
            // third review); toward the cadence it steps at, 7: NativeHordeGait.LeadOf. Shared 11, before the lead and with it switched.
            // Stopping and setting off every .35 s only.)
            float worstStopGo=0,worstStopGoSwing=0;
            foreach(int kind in Kinds)foreach(int id in IdsOf(kind))foreach(float rate in new[]{90f,-150f})
            {
                var st=NativeHordeKinds.ProposedRow(kind);
                foreach(float speed in new[]{st.walk,st.run}){var r=Drive(rig,kind,StopGo(speed,rate,.35f),7f,3f,id);worstStopGo=Math.Max(worstStopGo,r.SnapLand);worstStopGoSwing=Math.Max(worstStopGoSwing,r.SwingSnap);
                    Check(r.SnapLand<=LandMost,NativeHordeKinds.Ids[kind]+" #"+id+" stopping and setting off every .35 s at "+speed.ToString("0.0")+" m/s while turning "+rate+" deg/s: a landing foot never flicks round ("+r.SnapLand.ToString("0.0")+" deg in its landing 120th of a second at most; this period only)");
                    Check(r.SwingSnap<=StopGoMost,NativeHordeKinds.Ids[kind]+" #"+id+" stopping and setting off every .35 s at "+speed.ToString("0.0")+" m/s while turning "+rate+" deg/s: a swinging foot turns no faster than shared's worst ("+r.SwingSnap.ToString("0.0")+" deg in a 120th of a second at most; this period only)");}
            }
            // ---- the model's planted ankle on the gait's footprint: running round a curve, walking round one, setting off from a stand ----
            {
                void Footprint(string what,int kind,Route route,float seconds,float from)
                {
                    float gap=0;int seen=0;
                    Drive(rig,kind,route,seconds,from,40003,(t,g,s,f)=>
                    {
                        if(t<from)return;
                        for(int a=0;a<2;a++)
                        {
                            if(g.FootAir[a]||g.Heel[a]>0||(g.Landed&(1<<a))!=0)continue;int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;
                            var ma=s.Position[rig.Of[hip+2]];var fp=g.ToLocal(g.FootWorld[a],f.Root);gap=Math.Max(gap,new Vector2(ma.X-fp.X,ma.Z-fp.Z).Length());seen++;
                        }
                    });
                    Check(seen>20&&gap<.01f,what+", the model's flat planted ankle stands on the gait's footprint ("+(gap*100).ToString("0.00")+" cm at most over "+seen+" foot-frames)");
                }
                Footprint("running round a curve",NativeHordeKinds.Runner,Arc(NativeHordeKinds.ProposedRow(NativeHordeKinds.Runner).run,180),5f,3.2f);
                Footprint("walking round a curve",NativeHordeKinds.Shambler,Arc(NativeHordeKinds.ProposedRow(NativeHordeKinds.Shambler).walk,90),6f,3.2f);
                Footprint("setting off from a stand into a run, through the walk-to-run blend",NativeHordeKinds.Shambler,SetOff(NativeHordeKinds.ProposedRow(NativeHordeKinds.Shambler).run),4.5f,1.9f);
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
            // ---- walking turns: the hips held as high as shared's (8865e817, before this branch) or the straight walk's ----
            // (The review, 2026-11-18: drawn in across the floor at a walk too, walking turns dipped where shared's had not. Walking keeps
            // shared's footing, NativeHordeGait.LegsAcross; the lead counts for drawing, not for stepping round; a walking foot steps nearer
            // its hip, AcrossWalk. Shared's lowest hips over each route's starts for every walking kind, both speeds and each walk take,
            // Tests/zombie-runturn-walks-shared.txt, from `--zombie-runturn-walktable` on shared.)
            float walkWorst=9,walkGain=0,worstWalkSwing=0;int walkN=0;
            {
                var shared=new System.Collections.Generic.Dictionary<string,float>();var inv=System.Globalization.CultureInfo.InvariantCulture;
                foreach(var line in System.IO.File.ReadAllLines(System.IO.Path.Combine(root,"Tests/zombie-runturn-walks-shared.txt")))
                {if(line.Length==0||line[0]=='#')continue;var c=line.Split('|');shared[c[0]+"|"+c[1]+"|"+c[2]+"|"+c[3]]=float.Parse(c[4],inv);}
                foreach(int kind in WalkKinds)foreach(float k in new[]{1f,.7f})foreach(int id in WalkIdsOf(kind))
                {
                    string key0=NativeHordeKinds.Ids[kind]+"|"+k.ToString("0.0",inv)+"|"+id+"|";var table=WalkTable(rig,kind,k,id);float straight=table[0].low;
                    foreach(var (route,low,mean) in table)
                    {
                        if(route=="walk straight")continue;Check(shared.TryGetValue(key0+route,out float pin),key0+route+": shared's walking value is pinned");
                        walkWorst=Math.Min(walkWorst,low-Math.Min(pin,straight));walkGain+=low-pin;walkN++;
                        // (The accepted cost, WalkFloors: held at today's, not shared's.)
                        int accepted=Array.FindIndex(WalkFloors,w=>w.key==key0+route);
                        if(accepted>=0){var w=WalkFloors[accepted];Check(low>=w.low&&mean>=w.mean,key0+route+": walking, the hips no lower than accepted (lowest "+low.ToString("0.000")+", mean "+mean.ToString("0.000")+" legs; floors "+w.low.ToString("0.000")+", "+w.mean.ToString("0.000")+"; shared "+pin.ToString("0.000")+")");continue;}
                        Check(low>=Math.Min(pin,straight)-.03f,key0+route+": walking, the hips as high as shared's or the straight walk's (lowest "+low.ToString("0.000")+" legs over its starts; shared "+pin.ToString("0.000")+", walking straight "+straight.ToString("0.000")+")");
                    }
                }
                Check(walkGain/Math.Max(1,walkN)>=0,"through the walking turns the hips on average at least shared's (by "+(walkGain/Math.Max(1,walkN)).ToString("0.000")+" legs over "+walkN+" routes)");
                // (A swinging foot through the walking turns at any moment of the stride, every walk take.)
                foreach(int kind in Kinds)foreach(int id in WalkIdsOf(kind))foreach(var (name,n,at) in WalkRoutes(kind))if(name.StartsWith("walk turn"))
                    for(int j=0;j<n;j++){var (route,seconds,from)=at(j);worstWalkSwing=Math.Max(worstWalkSwing,Drive(rig,kind,route,seconds,from,id).SwingSnap);}
                Check(worstWalkSwing<=WalkSwingMost,"a swinging foot turns smoothly through the walking turns at any moment of the stride ("+worstWalkSwing.ToString("0.0")+" deg in a 120th of a second at most)");
            }
            // ---- the walking sideways step pinned where it pays: the shambler's arcs at 1.2 times its walk, in the walk-to-run blend ----
            // (Without AcrossWalk these fell to the hips' floor, .79 to .77 legs: `--zombie-runturn-band`.)
            float worstBand=9;
            {
                var st=NativeHordeKinds.ProposedRow(NativeHordeKinds.Shambler);
                foreach(float rate in new[]{105f,120f,150f})
                {
                    float mean=0;for(int j=0;j<4;j++){float at0=3+j*.13f;mean+=Drive(rig,NativeHordeKinds.Shambler,Arc(st.walk*1.2f,rate,at0),at0+5,at0+1).HipsLow/4;}worstBand=Math.Min(worstBand,mean);
                    Check(mean>=.9f,"the shambler at 1.2 times its walk round a "+rate+" degree a second arc keeps its hips up (mean of four starts' lowest "+mean.ToString("0.000")+" legs)");
                }
            }
            // ---- the Hulk's and the Hound's knees lean the hips' way, as before: the lean to the toes is a biped's own stride's ----
            // (The review, 2026-11-18: leaning to the toes, the Hound's knees flipped from side to side, its pelvis's forward lying across
            // the body on all fours, and the Hulk's moved up to 8 cm. NativeVisualRig.KneeLeansToToes. A planted foot turned 40 degrees on
            // the floor moves a biped's knee on its own stride after it, and the Hulk's and the Hound's not at all.)
            float kneeBiped,kneeHulk,kneeHound;
            {
                Vector3 KneeWith(int kind,float stride,float footYaw)
                {
                    var s=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.Reset();int foot=CapturedRig.LHip+2;
                    pose.Rotation[foot]=CapturedRig.Rot(Vector3.UnitY,footYaw/Deg)*pose.Rotation[foot];pose.World();
                    NativeZombiePosture.Stance(s,kind,false,0,0,0,stride,0,stride);s.Dt=Frame;NativeVisualRig.Pose(s,pose,default(NativeVisualRig.Grasp),true,false);
                    return s.Position[rig.Of[CapturedRig.LHip+1]];
                }
                float Moved(int kind,float stride)=>(KneeWith(kind,stride,40)-KneeWith(kind,stride,0)).Length();
                kneeBiped=Moved(NativeHordeKinds.Shambler,rig.LegRatio);kneeHulk=Moved(NativeHordeKinds.Hulk,1);kneeHound=Moved(NativeBackroomsMotion.HoundKind,rig.LegRatio);
                var st0=new NativeVisualRig.State(rig);NativeZombiePosture.Stance(st0,NativeHordeKinds.Hulk,false,0,0,0,1,0,1);bool hulkLeans=NativeVisualRig.KneeLeansToToes(st0);
                NativeZombiePosture.Stance(st0,NativeBackroomsMotion.HoundKind,false,0,0,0,rig.LegRatio,0,rig.LegRatio);bool houndLeans=NativeVisualRig.KneeLeansToToes(st0);
                Check(!hulkLeans&&!houndLeans&&kneeHulk<1e-5f&&kneeHound<1e-5f&&kneeBiped>.01f,"a planted foot turned on the floor leaves the Hulk's and the Hound's knees where the hips lean them and turns a biped's after it ("+(kneeHulk*100).ToString("0.00")+", "+(kneeHound*100).ToString("0.00")+" and "+(kneeBiped*100).ToString("0.0")+" cm)");
            }
            // ---- the model presentation hands the binding the gait's draw-in across the floor (the game's own wiring) ----
            {
                Check(Math.Abs(NativeHordeGait.KneeFollow-1)<1e-6f,"the knee follows its foot's whole twist");
                var zp=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ChipArena/Runtime/NativeZombieModelPresentation.cs"));
                Check(zp.Contains("z.Across=gait?.LegsAcross??1;")&&zp.Contains("z.Stride,z.Reach,z.Across);"),"the model presentation hands the binding the gait's draw-in across the floor");
                // (The gore's joints, which no check drives: the model's shift for the add's own kind only, a thrown part's voxel fallback on the
                // rig's own joints, and the voxel model's wounds on its own.)
                var gp=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeGorePresentation.cs"));
                var kp=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"));
                Check(gp.Contains("if(!zombieBodies.TryGetValue(id,out var z)||z.Kind!=kind||z.Body.Root==null||z.PosedFrame<Time.frameCount-1)return false;"),"the gore's model shift is taken only for the add's own kind");
                Check(kp.Contains("hordePartBone??=b=>{var p=hordeDrawPart;var m=HordePartTurn(p,hordeDrawAge,out _,true)*p.RigBones[b];")&&kp.Contains("var j=(rigJoints?p.RigBones:p.Bones)[p.Joint].GetColumn(3);")
                    &&kp.Contains("var turn=HordePartTurn(p,age,out var stump,!modelPart);var m=turn*(modelPart?p.Bones:p.RigBones)[p.Joint];"),"a thrown part's voxel fallback, its cap and its trail on the rig's own joints");
                Check(gp.Contains("var bones=GoreBones(j.Rig);"),"the voxel model's wounds on its own joints");
            }
            Console.WriteLine("PASS Zombie run and turn ("+checks+" checks: 4 kinds, "+pairs+" take pairings (one add each, sampled starts: not a population's worst), on the model's legs; running turns' hips at most "+worstDrop.ToString("0.00")+" legs under the straight run's (mean "+meanDrop.ToString("0.000")+"), fastest "+worstHipRate.ToString("0.0")+" legs/s; sharp turns at any moment of the stride at least "+worstSharp.ToString("0.00")+" legs, reversals "+worstRev.ToString("0.00")+", targets behind "+worstSwitch.ToString("0.00")+" ("+unguarded+" of their floors at the hips' clamp, unguarded), circle chases from any start "+worstCircle.ToString("0.00")+"; knee off its planted foot at most "+worstKnee.ToString("0")+" deg, the foot off the hips at most "+worstHipFoot.ToString("0")+" deg, steps at most "+worstRate.ToString("0.00")+" of the straight run's; swinging foot at most "+Math.Max(worstSwing,worstWalkSwing).ToString("0.0")+" deg a 120th, stopping and setting off "+worstStopGoSwing.ToString("0.0")+" (landing "+worstStopGo.ToString("0.0")+")"+"; walking turns' hips at worst "+walkWorst.ToString("+0.000;-0.000")+" legs off shared's or the straight walk's, on average "+(walkGain/Math.Max(1,walkN)).ToString("+0.000;-0.000")+" over shared's over "+walkN+" routes; the blend's arcs at least "+worstBand.ToString("0.000")+"; a planted foot turned 40 degrees moves the Hulk's and Hound's knees "+(Math.Max(kneeHulk,kneeHound)*100).ToString("0.00")+" cm, a biped's "+(kneeBiped*100).ToString("0.0")+"; presentation only)");
        }
        /// <summary>The bounds, from the worst sampled take pairing (IdsOf; one add each, one start: not a population's worst): the planted knee
        /// off its foot and the planted foot off the hips' facing on a running route (deg; a reversal's and the weave's apart); a swinging foot's
        /// fastest turn (deg a 120th of a second) on the routes, over every start of the walking turns, and stopping and setting off every .35 s
        /// (its landing frame apart); the accepted floor (legs) of the circle chase from four starts; each pairing's own values held within
        /// PinDegrees and PinSwing (Tests/zombie-runturn-bounds.txt); the hips' clamp, at which a floor cannot fail (legs: the lowest anywhere
        /// on the walking table, the gait's sink cap, its deepest crouch, on the model). Shared on the same: knee 56, foot 76, swing 42,
        /// stop-and-go 40 (landing 11), circle .87.</summary>
        internal const float KneeMost=30,HipFootMost=42,HipFootReverse=60,SwingMost=19,WalkSwingMost=32,StopGoMost=30,LandMost=9,CircleFloor=.77f,PinDegrees=3,PinSwing=2,HipsClamp=.7643f;
        /// <summary>The walking turns' accepted cost: the routes (kind|speed|id|route) lower than shared's by more than .03 legs at their worst
        /// start or on average over their starts, held at their values on 2026-11-18 (the lowest and the mean of the starts' lowest hips, less
        /// .005). Seventeen of 280; 22 others higher at their worst (Tests/zombie-runturn-walks-shared.txt, docs/ZOMBIE-LOCOMOTION.md).</summary>
        internal static readonly (string key,float low,float mean)[] WalkFloors={("shambler|1.0|40003|walk turn 90",0.800f,0.848f),("shambler|1.0|40003|walk turn 180",0.759f,0.796f),("shambler|0.7|40003|walk turn 90",0.759f,0.891f),("shambler|0.7|40003|walk turn -90",0.762f,0.888f),("shambler|0.7|40004|walk arc 120/s",0.972f,0.989f),("runner|1.0|40004|walk turn -90",0.816f,0.938f),("runner|1.0|40004|walk circle r1.5 x1.3",0.935f,0.973f),("runner|1.0|40004|walk circle r2.5 x1.3",0.803f,0.954f),("brute|1.0|40004|walk arc 90/s",0.761f,0.761f),("brute|0.7|40004|walk arc 120/s",0.762f,0.762f),("brute|0.7|40004|walk turn -90",0.775f,0.920f),("faceling|1.0|40004|walk turn -90",0.889f,0.925f),("faceling|0.7|40003|walk turn -90",0.759f,0.840f),("linesman|1.0|40003|walk arc 120/s",0.762f,0.762f),("linesman|1.0|40003|walk arc 135/s",0.763f,0.763f),("linesman|1.0|40003|walk turn -90",0.759f,0.818f),("linesman|0.7|40003|walk turn -90",0.843f,0.879f)};
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
        // ---- walking turns: arcs, turns and circle chases at walk speed (a zombie walks while its pilot is more than 7 m off) ----
        internal static Route WalkCircle(float speed,float radius,float k,float a0=0)=>(float t,Vector2 at,out Vector2 dir,out float sp,out float atk)=>{float a=a0+t*speed*k/radius;var c=new Vector2(0,6);var target=c+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*radius;dir=target-at;sp=dir.Length()>1.1f?speed:0;atk=float.NaN;};
        /// <summary>The walking routes for kind `kind` at `k` of its walk: each a name and, for start `j` of its count, the route, its
        /// length and when it is measured from (arcs and turns started at eight moments of the stride, circle chases with the pilot
        /// starting at four places round its circle).</summary>
        internal static (string name,int count,Func<int,(Route route,float seconds,float from)> at)[] WalkRoutes(int kind,float k=1)
        {
            float walk=NativeHordeKinds.ProposedRow(kind).walk*k;
            var l=new System.Collections.Generic.List<(string,int,Func<int,(Route,float,float)>)>();
            foreach(float rate in new[]{45f,90f,120f,135f})l.Add(("walk arc "+rate+"/s",8,j=>{float at0=3+j*.071f;return (Arc(walk,rate,at0),at0+5,at0+1);}));
            foreach(float deg in new[]{90f,-90f,180f})l.Add(("walk turn "+deg,8,j=>{float at0=3+j*.071f;return (Turn(walk,deg,at0),at0+2.5f,at0-.1f);}));
            foreach(var (radius,x) in new[]{(1.5f,1f),(1.5f,1.3f),(1.8f,1.15f),(2.5f,1f),(2.5f,1.3f),(4f,1f),(4f,1.3f)})
                l.Add(("walk circle r"+radius+" x"+x,4,j=>(WalkCircle(walk,radius,x,j*1.57f),9f,3.5f)));
            return l.ToArray();
        }
        /// <summary>The upright kinds the walking routes are measured on (the Backrooms' faceling and linesman walk the same gait).</summary>
        internal static int[] WalkKinds=>new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Resolve("faceling"),NativeHordeKinds.Resolve("linesman")};
        /// <summary>`--zombie-runturn-walks`: every walking route, at the walk and at .7 of it, the worst and the mean of its starts' lowest
        /// hips (legs), and its steps a second (diagnostic).</summary>
        public static void Walks()
        {
            var rig=ModelPoseChecks.Measured();
            foreach(int kind in WalkKinds)foreach(float k in new[]{1f,.7f})
            {
                var straight=Drive(rig,kind,Straight(NativeHordeKinds.ProposedRow(kind).walk*k),6f,3f);
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0,-9} x{1:0.0} {2,-21} hipsLow {3:0.000} mean {3:0.000} steps/s {4:0.00}",NativeHordeKinds.Ids[kind],k,"walk straight",straight.HipsLow,straight.Steps/Math.Max(1e-3f,straight.Seconds)));
                foreach(var (name,n,at) in WalkRoutes(kind,k))
                {
                    float low=9,sum=0,steps=0,secs=0,kneeF=0;
                    for(int j=0;j<n;j++){var (route,seconds,from)=at(j);var r=Drive(rig,kind,route,seconds,from);low=Math.Min(low,r.HipsLow);sum+=r.HipsLow;kneeF=Math.Max(kneeF,r.KneeFoot);steps+=r.Steps;secs+=r.Seconds;}
                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0,-9} x{1:0.0} {2,-21} hipsLow {3:0.000} mean {4:0.000} steps/s {5:0.00} kneeF {6:0}",NativeHordeKinds.Ids[kind],k,name,low,sum/n,steps/Math.Max(1e-3f,secs),kneeF));
                }
            }
        }
        /// <summary>`--zombie-runturn-band`: the shambler's walking arcs over speed (.6 to 2 of its walk) and turn rate, the mean of four
        /// starts' lowest hips (legs; diagnostic).</summary>
        public static void Band()
        {
            var rig=ModelPoseChecks.Measured();int kind=NativeHordeKinds.Shambler;var st=NativeHordeKinds.ProposedRow(kind);var inv=System.Globalization.CultureInfo.InvariantCulture;
            var rates=new[]{45f,60f,75f,90f,105f,120f,150f};var sb=new StringBuilder();
            foreach(float k in new[]{.6f,.8f,1f,1.2f,1.5f,2f})
            {
                sb.Clear();sb.Append(string.Format(inv,"band {0:0.00} m/s",st.walk*k));
                foreach(float rate in rates){float mean=0;for(int j=0;j<4;j++){float at0=3+j*.13f;mean+=Drive(rig,kind,Arc(st.walk*k,rate,at0),at0+5,at0+1).HipsLow/4;}sb.Append(string.Format(inv," {0:0}/s {1:0.000}",rate,mean));}
                Console.WriteLine(sb.ToString());
            }
        }
        /// <summary>Ids from 40003 on that between them draw every pairing of kind `kind`'s walk and run takes
        /// (NativeZombieMotion.Walk and Run off the id's seed), 40003 first.</summary>
        internal static int[] IdsOf(int kind)
        {
            var ids=new System.Collections.Generic.List<int>();var seen=new System.Collections.Generic.HashSet<(int,int)>();
            var w=NativeZombieMotion.Walks(kind);var r=NativeZombieMotion.Runs(kind);int want=0;
            for(int id=40003;id<40400&&(want==0||seen.Count<want);id++)
            {
                uint seed=NativeHordeKinds.Seed(id);var pair=(NativeZombieMotion.Walk(kind,seed),NativeZombieMotion.Run(kind,seed));
                if(want==0){var all=new System.Collections.Generic.HashSet<(int,int)>();for(int j=40003;j<41003;j++){uint sj=NativeHordeKinds.Seed(j);all.Add((NativeZombieMotion.Walk(kind,sj),NativeZombieMotion.Run(kind,sj)));}want=all.Count;}
                if(seen.Add(pair))ids.Add(id);
            }
            return ids.ToArray();
        }
        /// <summary>Of <see cref="IdsOf"/>, those with a walk take of their own (walking, the run take does not show).</summary>
        internal static int[] WalkIdsOf(int kind)
        {
            var ids=new System.Collections.Generic.List<int>();var seen=new System.Collections.Generic.HashSet<int>();
            foreach(int id in IdsOf(kind))if(seen.Add(NativeZombieMotion.Walk(kind,NativeHordeKinds.Seed(id))))ids.Add(id);return ids.ToArray();
        }
        /// <summary>The lowest hips (legs) over each walking route's starts for kind `kind` at `k` of its walk, add `id`: route name, worst,
        /// mean of the starts' lowest; the straight walk first.</summary>
        internal static System.Collections.Generic.List<(string route,float low,float mean)> WalkTable(NativeVisualRig.Rig rig,int kind,float k,int id)
        {
            var o=new System.Collections.Generic.List<(string,float,float)>();var straight=Drive(rig,kind,Straight(NativeHordeKinds.ProposedRow(kind).walk*k),6f,3f,id);o.Add(("walk straight",straight.HipsLow,straight.HipsLow));
            foreach(var (name,n,at) in WalkRoutes(kind,k)){float low=9,sum=0;for(int j=0;j<n;j++){var (route,seconds,from)=at(j);var r=Drive(rig,kind,route,seconds,from,id);low=Math.Min(low,r.HipsLow);sum+=r.HipsLow;}o.Add((name,low,sum/n));}
            return o;
        }
        /// <summary>`--zombie-runturn-walktable`: <see cref="WalkTable"/> for every walking kind, both speeds and each id with its own walk
        /// take, as `kind|speed|id|route|lowest|mean` lines (what Tests/zombie-runturn-walks-shared.txt holds for shared).</summary>
        public static void WalkTableAll()
        {
            var rig=ModelPoseChecks.Measured();var inv=System.Globalization.CultureInfo.InvariantCulture;
            foreach(int kind in WalkKinds)foreach(float k in new[]{1f,.7f})foreach(int id in WalkIdsOf(kind))
                foreach(var (route,low,mean) in WalkTable(rig,kind,k,id))Console.WriteLine(string.Format(inv,"{0}|{1:0.0}|{2}|{3}|{4:0.000}|{5:0.000}",NativeHordeKinds.Ids[kind],k,id,route,low,mean));
        }
        /// <summary>`--zombie-runturn-ids`: each kind's walk and run takes and the ids that draw every pairing of them (diagnostic).</summary>
        public static void Ids()
        {
            foreach(int kind in WalkKinds)
            {
                string N(int c)=>c<0?"-":NativeZombieClips.Key(c);var o=new StringBuilder();o.Append(NativeHordeKinds.Ids[kind]).Append(": walks ");
                foreach(int c in NativeZombieMotion.Walks(kind))o.Append(N(c)).Append(' ');o.Append("runs ");foreach(int c in NativeZombieMotion.Runs(kind))o.Append(N(c)).Append(' ');o.Append("| ");
                foreach(int id in IdsOf(kind)){uint sd=NativeHordeKinds.Seed(id);o.Append(id).Append('=').Append(N(NativeZombieMotion.Walk(kind,sd))).Append('/').Append(N(NativeZombieMotion.Run(kind,sd))).Append(' ');}
                Console.WriteLine(o.ToString());
            }
        }
        /// <summary>The bytes allocated over 1800 frames of each kind on the model's legs turning (a circle chase that stops and sets off by
        /// its pilot, then a stop-and-go turn) through the gait, the fit and the binding, after 600 to warm (ZombieLocoChecks.Allocation).</summary>
        internal static long TurningAllocation()
        {
            var rig=ModelPoseChecks.Measured();var adds=new System.Collections.Generic.List<(int kind,NativeHordeGait g,CapturedRig.Pose p,CapturedRig.Pose fit,NativeVisualRig.State s,HordeFlow flow,HordeMotionInput f)>();
            foreach(int kind in Kinds)foreach(int id in IdsOf(kind)){var flow=new HordeFlow();flow.Configure(kind);adds.Add((kind,new NativeHordeGait{Legs=rig.LegRatio},new CapturedRig.Pose(),new CapturedRig.Pose(),new NativeVisualRig.State(rig),flow,new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full}));}
            var at=new Vector2[adds.Count];
            void Tick(int i)
            {
                float t=i*Frame;
                for(int a=0;a<adds.Count;a++)
                {
                    var (kind,g,p,fit,s,flow,f)=adds[a];var st=NativeHordeKinds.ProposedRow(kind);float ang=t*(i<1200?1.4f:-2.2f),speed=i<1200?st.run:(i/40)%2==0?st.walk:0;
                    var dir=new Vector2((float)Math.Sin(ang),(float)Math.Cos(ang));var vel=dir*speed;at[a]+=vel*Frame;
                    flow.Host(t,t,new Vector3(at[a].X,.85f,at[a].Y),new Vector3(vel.X,0,vel.Y),true,YawOf(dir),Frame,false);
                    f.Time=t;f.Root=flow.Position;f.Velocity=flow.Velocity;f.Yaw=flow.Yaw;f.YawRate=flow.YawRate;f.Bank=flow.Bank;f.Pitch=flow.Pitch;f.Ground=0;
                    g.Pose(f,p,null);p.World();NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow,g.Outreach,g.LegsAcross);s.Dt=Frame;NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);
                }
            }
            for(int i=0;i<600;i++)Tick(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=600;i<2400;i++)Tick(i);return GC.GetAllocatedBytesForCurrentThread()-before;
        }
        /// <summary>The routes whose values Run holds for each take pairing (Tests/zombie-runturn-bounds.txt).</summary>
        internal static readonly string[] BoundRoutes={"run arc 90/s","run arc 180/s","run turn 90","run turn 180","run circle chase","run target switch","run turn into attack","run weave","walk turn 90","walk turn 180","walk circle chase"};
        /// <summary>`--zombie-runturn-bounds`: Tests/zombie-runturn-bounds.txt, each take pairing's knee off its foot, foot off the hips and
        /// swinging foot's fastest turn on the bounded routes (`kind|id|route|knee|foot|swing`), and its running reversal's and switch's
        /// worst hips over eight starts (`FLOORS kind|id|reverse|switch`): what Run holds, less its margins.</summary>
        public static void Bounds()
        {
            var rig=ModelPoseChecks.Measured();var inv=System.Globalization.CultureInfo.InvariantCulture;
            foreach(var h in new[]{
                "# The zombie run-turn checks' per-pairing values on 2026-11-18 (claude/zombie-run-turn-tests): for one add of each pairing of a kind's walk and",
                "# run takes (ZombieRunTurnChecks.IdsOf) on each bounded route, from one start, the planted knee off its foot and the planted foot off the hips'",
                "# facing (deg) and a swinging foot's fastest turn (deg a 120th of a second); and its worst hips (legs) over eight starts of a running reversal",
                "# and a switch to a target behind. Sampled points, not a population's worst. ZombieRunTurnChecks holds each at its own value: the angles",
                "# within a few degrees, the floors within .005 legs. Written by CoreTests --zombie-runturn-bounds.",
                "# kind|id|route|knee|foot|swing, or FLOORS kind|id|reverse|switch"})Console.WriteLine(h);
            foreach(int kind in Kinds)foreach(int id in IdsOf(kind))
            {
                var sc=Scenarios(kind);
                foreach(var n in BoundRoutes)foreach(var x in sc)if(x.name==n){var r=Drive(rig,kind,x.route,x.seconds,x.from,id);Console.WriteLine(string.Format(inv,"{0}|{1}|{2}|{3:0.0}|{4:0.0}|{5:0.0}",NativeHordeKinds.Ids[kind],id,n,r.KneeFoot,r.HipFoot,r.SwingSnap));}
                var st=NativeHordeKinds.ProposedRow(kind);
                float Low(Func<float,Route> mk){float low=9;for(int k=0;k<8;k++){float at0=3+k*.071f;low=Math.Min(low,Drive(rig,kind,mk(at0),at0+2.2f,at0-.1f,id).HipsLow);}return low;}
                Console.WriteLine(string.Format(inv,"FLOORS {0}|{1}|{2:0.0000}|{3:0.0000}",NativeHordeKinds.Ids[kind],id,Low(a0=>Turn(st.run,180,a0)),Low(a0=>Switch(st.run,a0))));
            }
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
                    p.World();if(timed)sb.Start();NativeZombiePosture.Fit(p,fit);NativeZombiePosture.Stance(s,kind,false,g.Sunk,g.Crouch,g.Busy,g.LegsNow,g.Outreach,g.LegsAcross);s.Dt=Frame;NativeVisualRig.Pose(s,fit,default(NativeVisualRig.Grasp),true,false);if(timed){sb.Stop();n++;}
                }
                Console.WriteLine(string.Format("{0,-10} gait {1:0.00} us, fit+bind {2:0.00} us a frame",NativeHordeKinds.Ids[kind],sw.Elapsed.TotalMilliseconds*1000/n,sb.Elapsed.TotalMilliseconds*1000/n));
            }
        }
        /// <summary>`--zombie-runturn-snap kind scenario`: the frames a swinging foot turns more than 15 degrees in (diagnostic).</summary>
        internal static bool SnapAll;internal static float SnapFrom;
        public static void Snap(int kind,string scenario)
        {
            var rig=ModelPoseChecks.Measured();
            foreach(var sc in Scenarios(kind))
            {
                if(sc.name!=scenario)continue;var lastF=new Vector3[2];var lastU=new Vector3[2];var was=new bool[2];
                Drive(rig,kind,sc.route,sc.seconds,sc.from,40003,(t,g,s,f)=>
                {
                    for(int a=0;a<2;a++)
                    {
                        int hip=a==0?CapturedRig.LHip:CapturedRig.RHip;var q=DrivePose.WorldRotation[hip+2];
                        Vector3 W3(Vector3 d){var w=g.ToWorld(d,f.Root)-g.ToWorld(Vector3.Zero,f.Root);return Vector3.Normalize(w);}
                        var fw=W3(Vector3.Transform(Vector3.UnitZ,q));var up=W3(Vector3.Transform(Vector3.UnitY,q));
                        if(SnapAll&&t>=sc.from+SnapFrom&&t<=sc.from+SnapFrom+.08f)Console.WriteLine(string.Format("  {0:0.000} {1} air {2} heel {3:0.00} fwd ({4:0.00},{5:0.00},{6:0.00}) up ({7:0.00},{8:0.00},{9:0.00})",t-sc.from,a==0?"L":"R",g.FootAir[a],g.Heel[a],fw.X,fw.Y,fw.Z,up.X,up.Y,up.Z));
                        if(g.FootAir[a]&&was[a]&&t>=sc.from&&!SnapAll){float A(Vector3 x,Vector3 y)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(x,y))))*Deg;float d=Math.Max(A(fw,lastF[a]),A(up,lastU[a]));
                            if(d>15||SnapAll)Console.WriteLine(string.Format("{0:0.000} foot {1} snap {2:0.0} deg tw {3:0} heel {4:0.00} yaw {5:0} speed {6:0.00} run {7:0.00}",t-sc.from,a==0?"L":"R",d,Wrap(g.FootTurn(a)-g.Yaw),g.Heel[a],g.Yaw,g.Speed,g.RunNow));}
                        lastF[a]=fw;lastU[a]=up;was[a]=g.FootAir[a];
                    }
                });
            }
        }
        internal static float TraceFrom=0,TraceFor=1.6f;internal static int TracePhase=-1;
        public static void Trace(int kind,string scenario)
        {
            var rig=ModelPoseChecks.Measured();float leg=rig.LegUpper+rig.LegLower;
            float floor;{var s0=new NativeVisualRig.State(rig);var rest=new CapturedRig.Pose();rest.Reset();rest.World();NativeVisualRig.Pose(s0,rest,default(NativeVisualRig.Grasp),false,false);floor=s0.Position[rig.Of[CapturedRig.LHip+2]].Y-NativeVisualRig.AnkleOver;}
            var list=Scenarios(kind);
            if(TracePhase>=0){var st=NativeHordeKinds.ProposedRow(kind);float at0=3+TracePhase*.071f;Route r=scenario=="run turn 90"?Turn(st.run,90,at0):scenario=="run turn -90"?Turn(st.run,-90,at0):scenario=="run turn 180"?Turn(st.run,180,at0):Switch(st.run,at0);list=new[]{(scenario,r,at0+2.2f,at0-.1f)};}
            // (The running circle chase from a quarter `phase` round the pilot's circle: "run circle chase" with a phase.)
            if(TracePhase>=0&&scenario=="run circle chase"){var st=NativeHordeKinds.ProposedRow(kind);list=new[]{(scenario,Circle(st.run,2.5f,st.run*1.15f,TracePhase*1.57f),9f,4f)};}
            // (A walking route by its `--zombie-runturn-walks` name, "x0.7 " in front for the slower walk; the phase picks its start.)
            if(scenario.StartsWith("walk arc ")||scenario.StartsWith("walk circle ")||scenario.StartsWith("x0.7 ")||TracePhase>=0&&scenario.StartsWith("walk turn "))
            {
                float k=scenario.StartsWith("x0.7 ")?.7f:1;string nm=k<1?scenario.Substring(5):scenario;int j=Math.Max(0,TracePhase);
                foreach(var (name,n,at) in WalkRoutes(kind,k))if(name==nm){var (route,seconds,from)=at(Math.Min(j,n-1));list=new[]{(scenario,route,seconds,from)};}
            }
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
