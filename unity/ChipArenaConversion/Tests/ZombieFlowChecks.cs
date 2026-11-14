#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The zombie motion pass (owner 2026-10-17: "fix the zombies animations ... Improve these vastly"; docs/ZOMBIE-MOTION.md): a
    // horde add on a guest at 20 Hz (jittered, late frames, the shipped snapshot clock) is drawn on a continuous clock and a
    // continuous root (bounded acceleration, never frozen) where the shipped path stepped and froze; planted feet hold still on
    // the drawn rig across speeds, turns, stops and turns in place; the blow's contact frame lands on the authority's hit within
    // one frame and a stagger in the wind-up never lets it land; the poses are a pure function of the inputs (two peers agree to the
    // bit) and nearly so across frame rates; doubling the frame rate halves every per-frame change; switches blend without a pop;
    // nothing allocates per frame; the crowd tiers cost what they say.
    internal static class ZombieFlowChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie flow: "+what);count++;}
        public static void Run(string root)
        {
            Clock();Jitter(out string jitter);Feet(out string feet);Contact(out string contact);Cancel();Determinism(out string det);Rates(out string rates);Blend();Life();Allocation();Tiers(out string tiers);Source(root);
            Console.WriteLine("PASS zombie flow ("+count+" checks: "+jitter+"; "+feet+"; "+contact+"; a stagger in the wind-up never lets the blow land and lets go in "+NativeHordeGait.CancelFade+" s; "+det+"; "+rates+"; switches and deaths blend without a pop, the legs and root never held back; seeded traits and hurt sag; 0 bytes per frame; "+tiers+")");
        }

        // ---- the horde clock: exact on the host, smooth through a guest's stalls, never backwards ----
        private static void Clock()
        {
            var c=new HordeClock();double t=5;for(int i=0;i<200;i++){t+=1/144.0;Check(c.Step(t,1/144f,false)==t,"the host's clock is the visual clock exactly");}
            // A guest's field clock that holds 60 ms every half second and then jumps back onto its timeline (the shipped snapshot
            // clock clamped at the newest frame while the next one is late).
            c.Reset();double field=10,lastClock=double.NaN;float most=0;int held=0;
            for(int i=0;i<144*8;i++)
            {
                float dt=1/144f;double wall=i*dt;bool stall=(wall%.5)<.06;if(!stall)field=10+wall;c.Step(field,dt,true);
                if(!double.IsNaN(lastClock)){Check(c.Time>lastClock,"the horde clock always moves forward");if(c.Time-lastClock<dt*.85)held++;}
                lastClock=c.Time;if(i>144)most=Math.Max(most,(float)Math.Abs(c.Time-field));
            }
            Check(held==0,"it never holds a frame through the stalls ("+held+")");Check(most<.09f,"and stays within "+most.ToString("0.000")+" s of the field clock");
            c.Step(field-2,1/144f,true);Check(Math.Abs(c.Time-(field-2))<1e-9,"a rewind (a new arena, a reconnect) snaps");
        }

        // ---- an authority's add: instant velocities (StepPveHorde sets them), a facing snapped to its heading, 60 Hz ----
        private sealed class Route
        {
            public readonly List<(double t,Vector3 p,Vector3 v,float yaw)> Ticks=new List<(double,Vector3,Vector3,float)>();
            public Route(double seconds,Func<double,(Vector3 v,float yawWant)> drive)
            {
                var p=new Vector3(0,.85f,0);float yaw=0;
                for(int k=0;k<=(int)(seconds*60);k++){double t=k/60.0;var (v,want)=drive(t);if(!float.IsNaN(want))yaw=want;else if(BossMath.FlatLength(v)>.05f)yaw=(float)(Math.Atan2(v.X,v.Z)*180/Math.PI);Ticks.Add((t,p,v,yaw));p+=v/60;}
            }
            public (double t,Vector3 p,Vector3 v,float yaw) At(double t){int k=(int)Math.Floor(t*60);return Ticks[Math.Max(0,Math.Min(Ticks.Count-1,k))];}
            public Vector3 Truth(double t){double x=t*60;int k=(int)Math.Floor(x);if(k<0)return Ticks[0].p;if(k>=Ticks.Count-1)return Ticks[Ticks.Count-1].p;return Vector3.Lerp(Ticks[k].p,Ticks[k+1].p,(float)(x-k));}
        }
        private static (Vector3,float) Chase(double t)=>Chase(t,1.5f,3.4f);
        // stand, walk, run, a 90 degree corner at a run, run, stop dead, turn round standing (the next swing's snap); rig units/s
        private static (Vector3,float) Chase(double t,float walk,float run)
        {
            if(t<1)return (Vector3.Zero,0);if(t<3)return (new Vector3(0,0,walk),float.NaN);if(t<4.5)return (new Vector3(0,0,run),float.NaN);
            if(t<6)return (new Vector3(run,0,0),float.NaN);if(t<7.5)return (Vector3.Zero,float.NaN);return (Vector3.Zero,t<8.5?90:-120);
        }
        // A kind's own chase: its content row's walk and run (the Hulk's are slow), in its rig's units.
        private static Route ChaseOf(int kind,double seconds)
        {
            var row=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];return new Route(seconds,t=>Chase(t,row.walk/sc,row.run/sc));
        }
        // The guest's network: 20 Hz sends (each frame's state, the timer restarted on send), 45 ms + jitter, 5% of frames 120 ms
        // late after the first second, in order.
        private static List<(double arrive,int tick)> Packets(Route path,int seed)
        {
            var rnd=new Random(seed);double Gauss(){double u=1-rnd.NextDouble(),v=rnd.NextDouble();return Math.Sqrt(-2*Math.Log(u))*Math.Cos(2*Math.PI*v);}
            var o=new List<(double,int)>();double host=0,next=0,last=0;double end=path.Ticks[path.Ticks.Count-1].t;
            while(host<end){host+=1/144.0*(1+.3*(rnd.NextDouble()-.5));if(host>=next){next=host+.05;int k=(int)Math.Floor(host*60);double at=Math.Max(last,host+.045+Math.Abs(Gauss())*.014+(host>1&&rnd.NextDouble()<.05?.12:0));last=at;o.Add((at,Math.Min(k,path.Ticks.Count-1)));}}
            return o;
        }

        // ---- jittery 20 Hz: the shipped lerp on the shipped clock against the horde clock and flow ----
        private static void Jitter(out string summary)
        {
            var path=new Route(9.5,Chase);var packets=Packets(path,1017);var rnd=new Random(5);
            var snap=new NativeSnapshotClock();var hist=new List<int>();var hc=new HordeClock();var flow=new HordeFlow();flow.Configure(NativeHordeKinds.Runner);
            double wall=0;int next=0;bool started=false;Vector3 po=default,vo=default,pn=default,vn=default;bool have=false;float maxOld=0,maxNew=0;int oldStalls=0,newStalls=0,frames=0,oldHeld=0,newHeld=0;double lastArena=double.NaN,lastPose=double.NaN;
            while(wall<9.3)
            {
                float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));wall+=dt;
                while(next<packets.Count&&packets[next].arrive<=wall){var k=packets[next].tick;if(!started){snap.Reset(path.Ticks[k].t);started=true;}snap.Observe(path.Ticks[k].t);hist.Add(k);flow.Observe(path.Ticks[k].t,path.Ticks[k].p,path.Ticks[k].yaw);next++;}
                if(!started)continue;double ct=snap.Advance(dt);double arena=path.Ticks[hist[hist.Count-1]].t;
                // OLD: InterpolateReplica's straight lerp between the bracketing received frames at the snapshot clock.
                int a=0,b=0;for(int i=0;i<hist.Count;i++){if(path.Ticks[hist[i]].t<=ct)a=i;else{b=i;break;}b=i;}
                var ta=path.Ticks[hist[a]];var tb=path.Ticks[hist[b]];float blend=BossMath.Clamp01((float)((ct-ta.t)/Math.Max(1e-5,tb.t-ta.t)));var o=Vector3.Lerp(ta.p,tb.p,blend);
                // NEW: the horde clock on the field clock, the flow on the snapshots.
                hc.Step(ct,dt,true,arena);flow.Replica(hc.Time,dt,false);var n=flow.Position;
                if(have&&wall>1.3)
                {
                    frames++;var ov=(o-po)/dt;var nv=(n-pn)/dt;maxOld=Math.Max(maxOld,(BossMath.Flat(ov-vo)).Length()/dt);maxNew=Math.Max(maxNew,BossMath.Flat(nv-vn).Length()/dt);
                    float truth=BossMath.FlatLength(path.At(ct).v);if(truth>1){if(BossMath.FlatLength(ov)<.25f*truth)oldStalls++;if(BossMath.FlatLength(nv)<.25f*truth)newStalls++;}
                    if(arena==lastArena)oldHeld++;if(hc.Time<=lastPose)newHeld++;
                    vo=ov;vn=nv;
                }
                else if(have){vo=(o-po)/dt;vn=(n-pn)/dt;}
                po=o;pn=n;have=true;lastArena=arena;lastPose=hc.Time;
            }
            Check(frames>1000,"the jitter run covers the chase ("+frames+" frames)");
            Check(newStalls==0,"the drawn root never stalls while the add moves (the shipped lerp stalled on "+oldStalls+" frames)");
            // (The worst is the runner's dead stop from 3.4 m/s, which the authority makes in one tick: the drawn root brakes over
            // about 0.1 s and rocks back a few centimetres. Everywhere else, the walk, the run and the corner, it stays under 60.)
            Check(maxNew<120,"its acceleration stays bounded through instant authority velocities and jittery snapshots ("+maxNew.ToString("0.0")+" m/s^2)");
            Check(maxNew*4<maxOld,"well under the shipped lerp's spikes ("+maxOld.ToString("0")+" m/s^2)");
            Check(newHeld==0&&oldHeld>frames/2,"the pose clock moves every frame (the shipped pose clock, arena.Time, held "+oldHeld+" of "+frames+" frames)");
            summary="a guest at 20 Hz (45 ms, +-14 ms, 5% 120 ms late, 144 Hz +-15% frames, the shipped snapshot clock): the drawn root's peak acceleration "+maxNew.ToString("0.0")+" m/s^2 against the shipped lerp's "+maxOld.ToString("0")+", 0 stalled frames against "+oldStalls+", the pose clock never held (arena.Time held "+(100*oldHeld/Math.Max(1,frames))+"% of frames)";
        }

        // ---- one add driven as the runtime drives it on the host: the 60 Hz body carried to the visual clock, the flow, the gait ----
        private sealed class Add
        {
            public readonly HordeClock Clock=new HordeClock();public readonly HordeFlow Flow=new HordeFlow();public readonly NativeHordeGait Gait=new NativeHordeGait();
            public readonly NativeHordeReact React=new NativeHordeReact();public readonly HordePoseBlend Blend=new HordePoseBlend();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            public int Kind,Id,Tier;public float Sc=1;public HordeMotionInput F;public Vector3 Root;public float Yaw;
            public Add(int kind,int id,int tier=0){Kind=kind;Id=id;Tier=tier;Sc=NativeHordeKinds.Scale[kind];React.Bind(kind);Flow.Configure(kind);}
            /// <summary>One rendered frame at wall time `w` (the simulation at its last 60 Hz step), the attack's age on the clock.</summary>
            public void Frame(Route path,double w,float dt,float lastAttack=float.NaN,float strike=.34f,float recover=1.05f,NativeHordeKinds.Attack attack=NativeHordeKinds.Attack.Swipe,int side=1,float cancelAt=float.NaN)
            {
                var tk=path.At(w);double simT=Math.Floor(w*60)/60;Clock.Step(w,dt,false);float now=(float)Clock.Time;
                Flow.Host(now,simT,tk.p*Sc,tk.v*Sc,true,tk.yaw,dt,!float.IsNaN(lastAttack)&&now-lastAttack>=0&&now-lastAttack<strike);
                float age=float.NaN;if(!float.IsNaN(lastAttack)){age=now-lastAttack;if(age<0||age>recover+.3f)age=float.NaN;}
                bool cancelled=!float.IsNaN(age)&&!float.IsNaN(cancelAt)&&now>=cancelAt;float inv=1/Sc;
                F=new HordeMotionInput{Kind=Kind,Id=Id,Seed=NativeHordeKinds.Seed(Id),Time=now,Yaw=Flow.Yaw,Ground=0,Root=new Vector3(Flow.Position.X,NativeHordeKinds.DrawRoot(Kind,false),Flow.Position.Z)*inv,Velocity=Flow.Velocity*inv,
                    Health=1,AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=side,Flow=true,YawRate=Flow.YawRate,Bank=Flow.Bank,Pitch=Flow.Pitch,Tier=Tier,Cancelled=cancelled,CancelAge=cancelled?now-cancelAt:0};
                Gait.Pose(F,Pose,React);Root=F.Root;Yaw=Gait.Yaw;
            }
            public Vector3 Ankle(int i)=>Gait.ToWorld(Pose.WorldPosition[i==0?CapturedRig.LFoot:CapturedRig.RFoot],Root)*Sc;
            public Vector3 Ball(int i)=>Gait.ToWorld(Pose.WorldPosition[i==0?CapturedRig.LToe:CapturedRig.RToe],Root)*Sc;
        }

        // ---- planted feet stay put on the drawn rig: speeds, the corner, the stop, the turn in place, jittery frame times ----
        private static void Feet(out string summary)
        {
            float worst=0;int planted=0,steps=0,rolled=0;string worstAt="";
            foreach(int k in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Spitter,NativeHordeKinds.Bloater,NativeHordeKinds.Hulk})
            {
                var path=ChaseOf(k,10);
                var add=new Add(k,73000+k*7);var rnd=new Random(k+3);var last=new Vector3[2];var lastBall=new Vector3[2];var was=new bool[2];var wasHeel=new bool[2];double w=0;float kindWorst=0;
                while(w<10)
                {
                    float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));w+=dt;add.Frame(path,w,dt);
                    for(int i=0;i<2;i++)
                    {
                        // The contact: the ankle while the foot lies flat, the ball once its heel has peeled (a trailing foot's roll).
                        bool on=!add.Gait.FootAir[i],heel=add.Gait.Heel[i]>0;var at=add.Ankle(i);var ball=add.Ball(i);
                        if(w>1.2&&on&&was[i])
                        {
                            var a=heel||wasHeel[i]?ball:at;var b=heel||wasHeel[i]?lastBall[i]:last[i];float d=new Vector2(a.X-b.X,a.Z-b.Z).Length();
                            if(d>kindWorst){kindWorst=d;if(d>worst){worst=d;worstAt=NativeHordeKinds.Ids[k]+" at "+w.ToString("0.00")+" s";}}planted++;if(heel)rolled++;
                        }
                        if((add.Gait.Landed&(1<<i))!=0)steps++;last[i]=at;lastBall[i]=ball;was[i]=on;wasHeel[i]=heel;
                    }
                }
                Check(kindWorst<.01f,NativeHordeKinds.Ids[k]+": a planted foot holds still on the drawn rig through the walk, run, corner, stop and turn in place (worst "+(kindWorst*1000).ToString("0.0")+" mm per frame)");
            }
            Check(steps>100&&planted>steps*20,"the feet step and stand ("+steps+" steps, "+planted+" planted frames, "+rolled+" on the ball)");
            summary="planted feet hold under 1 cm on the drawn rig for six kinds at their own walk and run, through a run's corner, a dead stop and a turn in place at 144 Hz +-15% (worst "+(worst*1000).ToString("0.0")+" mm per frame, "+worstAt+"; "+steps+" steps, "+rolled+" frames rolled onto the ball)";
        }

        // ---- contact: the take's impact on the frame the clock passes LastAttack + strike ----
        private static void Contact(out string summary)
        {
            int cases=0;float worstLate=0;
            foreach(var (k,attack,strike,recover) in new[]{(NativeHordeKinds.Shambler,NativeHordeKinds.Attack.Swipe,.34f,1.05f),(NativeHordeKinds.Runner,NativeHordeKinds.Attack.Swipe,.3f,.85f),(NativeHordeKinds.Brute,NativeHordeKinds.Attack.Smash,.62f,1.6f),(NativeHordeKinds.Shambler,NativeHordeKinds.Attack.Lunge,.43f,1.1f)})
                foreach(int side in new[]{1,-1})foreach(int seed in new[]{3,9})
                {
                    var path=new Route(4,t=>(Vector3.Zero,0));var add=new Add(k,74000+k*11+seed);var rnd=new Random(seed*7+side);double w=0;float last=1.6f+.013f*seed;bool hit=false;float prevClip=-1;
                    while(w<last+recover)
                    {
                        float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));w+=dt;add.Frame(path,w,dt,last,strike,recover,attack,side);
                        if(add.Gait.AttackClip<0)continue;var c=NativeZombieClips.Clip(add.Gait.AttackClip);float t=add.F.Time-last;
                        Check(add.Gait.AttackClipTime>=prevClip-1e-5f,"the take never runs backward");prevClip=add.Gait.AttackClipTime;
                        if(!hit&&add.Gait.AttackClipTime>=c.Marks[2]-1e-5f){hit=true;float late=t-strike;Check(late>=-1e-4f&&late<dt+1e-4f,NativeHordeKinds.Ids[k]+" "+attack+": the contact frame lands on the authority's hit within a frame ("+(late*1000).ToString("0.0")+" ms after)");worstLate=Math.Max(worstLate,late);cases++;}
                    }
                    Check(hit,"the blow lands");
                }
            // The procedural channels: the strike accelerates into contact and is exactly there at the strike.
            foreach(NativeHordeKinds.Attack a in Enum.GetValues(typeof(NativeHordeKinds.Attack)))foreach(float H in new[]{.3f,.42f,.62f})
            {
                float R=Math.Max(1.05f,H+.05f),h=1/240f;NativeHordeGait.Channels(H,H,R,a,out _,out float s0,out _,out _,out _);NativeHordeGait.Channels(H-h,H,R,a,out _,out float s1,out _,out _,out _);NativeHordeGait.Channels(H-2*h,H,R,a,out _,out float s2,out _,out _,out _);
                Check(s0==1&&s0-s1>s1-s2,a+": the swing is fastest on the contact frame (ease in, not a smoothstep)");
                NativeHordeGait.Channels(0,H,R,a,out _,out _,out _,out _,out float w0);NativeHordeGait.Channels(.01f,H,R,a,out _,out _,out _,out _,out float w1);Check(w0==0&&w1<.05f,a+": the attack layer comes in from nothing (no snap as it starts)");
                float most=0;for(float t=H;t<R;t+=.01f){NativeHordeGait.Channels(t,H,R,a,out _,out _,out _,out float st,out _);most=Math.Max(most,st);}Check(most>1.01f&&most<1.15f,a+": the recovery settles with weight (over and back, "+most.ToString("0.000")+")");
            }
            summary="the take's contact frame lands within one frame of LastAttack + strike on the horde clock ("+cases+" blows, 144 Hz +-15%, latest "+(worstLate*1000).ToString("0.0")+" ms), the procedural strikes ease in to contact";
        }

        // ---- a stagger in the wind-up: the blow never lands, the arms let go ----
        private static void Cancel()
        {
            var path=new Route(4,t=>(Vector3.Zero,0));var add=new Add(NativeHordeKinds.Shambler,74500);double w=0;float last=1.5f,strike=.34f,cancelAt=last+.2f;float maxClip=0;int clip=-1;
            while(w<last+1.3f){float dt=1/144f;w+=dt;add.Frame(path,w,dt,last,strike,1.05f,NativeHordeKinds.Attack.Swipe,1,cancelAt);if(add.Gait.AttackClip>=0){clip=add.Gait.AttackClip;maxClip=Math.Max(maxClip,add.Gait.AttackClipTime);}if(w>cancelAt+NativeHordeGait.CancelFade+.02f)Check(add.Gait.AttackClip<0,"the cancelled blow is gone after "+NativeHordeGait.CancelFade+" s");}
            Check(clip>=0&&maxClip<NativeZombieClips.Clip(clip).Marks[2],"the cancelled blow's take never reaches its contact");
        }

        // ---- two peers: bit-identical from identical inputs; close across frame rates ----
        private static void Determinism(out string summary)
        {
            var path=new Route(9,Chase);
            var a=new Add(NativeHordeKinds.Shambler,75001);var b=new Add(NativeHordeKinds.Shambler,75001);var rnd=new Random(11);double w=0;bool same=true;
            while(w<9){float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));w+=dt;a.Frame(path,w,dt,w>7.6&&w<8?7.6f:float.NaN);b.Frame(path,w,dt,w>7.6&&w<8?7.6f:float.NaN);for(int j=0;j<22;j++)same&=a.Pose.Rotation[j]==b.Pose.Rotation[j]&&a.Pose.Position[j]==b.Pose.Position[j];}
            Check(same,"two peers fed the same frames draw the same pose to the bit");
            // 60 Hz against 144 Hz, compared every twelfth of a second: the head (springs, lag, loll) and the root agree closely.
            var c=new Add(NativeHordeKinds.Shambler,75001);var d=new Add(NativeHordeKinds.Shambler,75001);double wc=0,wd=0;float worstHead=0,worstRoot=0;
            for(int s=1;s<=9*12;s++)
            {
                double until=s/12.0;while(wc<until-1e-9){float dt=(float)Math.Min(1/60.0,until-wc);wc+=dt;c.Frame(path,wc,dt);}while(wd<until-1e-9){float dt=(float)Math.Min(1/144.0,until-wd);wd+=dt;d.Frame(path,wd,dt);}
                if(until<1.5)continue;c.Pose.World();d.Pose.World();var hc=c.Gait.ToWorld(c.Pose.WorldPosition[CapturedRig.Head],c.Root);var hd=d.Gait.ToWorld(d.Pose.WorldPosition[CapturedRig.Head],d.Root);
                worstHead=Math.Max(worstHead,Vector3.Distance(hc,hd));worstRoot=Math.Max(worstRoot,Vector3.Distance(c.Root,d.Root));
            }
            // (The difference comes where the 60 Hz body's instant velocity changes, the start, the corner and the dead stop, reach
            // the two frame clocks up to a frame apart.)
            Check(worstRoot<.04f,"60 and 144 Hz peers place the root within 4 cm ("+(worstRoot*100).ToString("0.0")+" cm)");
            // (The springs are exact; the cycle integrates its cadence frame by frame and its steps lift on frames, so the two peers'
            // cycles stand a few hundredths of a cycle apart, most after a start or a stop.)
            Check(worstHead<.08f,"and the head within 8 cm (the cycles a few hundredths apart: "+(worstHead*100).ToString("0.0")+" cm)");
            summary="two peers agree to the bit; 60 against 144 Hz: root within "+(worstRoot*100).ToString("0.0")+" cm, head within "+(worstHead*100).ToString("0.0")+" cm";
        }

        // ---- doubling the frame rate halves the per-frame change (no pop anywhere) ----
        private static float Worst(Add add,Route path,double seconds,float hz,float attackAt)
        {
            double w=0;float worst=0;var prev=new Vector3[22];bool have=false;
            while(w<seconds)
            {
                float dt=1/hz;w+=dt;add.Frame(path,w,dt,w>=attackAt?attackAt:float.NaN);add.Pose.World();
                for(int j=0;j<CapturedRig.LHip;j++){var p=add.Pose.WorldPosition[j];if(have&&w>1.2){worst=Math.Max(worst,Vector3.Distance(p,prev[j]));}prev[j]=p;}have=true;
            }
            return worst;
        }
        private static void Rates(out string summary)
        {
            var path=new Route(9,Chase);float lo=Worst(new Add(NativeHordeKinds.Shambler,76001),path,9,60,7.7f),hi=Worst(new Add(NativeHordeKinds.Shambler,76001),path,9,120,7.7f);
            Check(hi/lo<.65f,"doubling the frame rate halves the trunk's and arms' largest per-frame change ("+(hi/lo).ToString("0.00")+": "+(lo*100).ToString("0.0")+" cm at 60 Hz, "+(hi*100).ToString("0.0")+" at 120)");
            summary="doubling the frame rate halves every per-frame change ("+(hi/lo).ToString("0.00")+")";
        }

        // ---- the pop killer ----
        private static void Blend()
        {
            var b=new HordePoseBlend();var p=new CapturedRig.Pose();var root=new Vector3(1,0,1);float yaw=10;
            for(int i=0;i<30;i++){p.Reset();var r2=root;float y2=yaw;b.Apply(p,ref r2,ref y2,1/60f);}
            // A marked switch: the next frame shows what was shown (continuous), then decays onto the new source.
            b.Mark();p.Reset();p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(1.2f,0,.4f);p.Position[CapturedRig.Body]=new Vector3(0,-.2f,.1f);var r=root+new Vector3(.5f,0,0);float y=yaw+40;
            b.Apply(p,ref r,ref y,1/60f);Check(Math.Abs(Quaternion.Dot(p.Rotation[CapturedRig.LArm],Quaternion.Identity))>.995f&&Vector3.Distance(r,root)<.06f&&Math.Abs(BossMath.WrapDeg(y-yaw))<4f,"a marked switch starts where the last frame was");
            for(int i=0;i<60;i++){p.Reset();p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(1.2f,0,.4f);p.Position[CapturedRig.Body]=new Vector3(0,-.2f,.1f);r=root+new Vector3(.5f,0,0);y=yaw+40;b.Apply(p,ref r,ref y,1/60f);}
            Check(Math.Abs(Quaternion.Dot(p.Rotation[CapturedRig.LArm],CapturedRig.Euler(1.2f,0,.4f)))>.9999f&&Vector3.Distance(r,root+new Vector3(.5f,0,0))<1e-3f&&!b.Active,"and settles onto the new source in a second");
            // An unmarked pop on an arm: held, then decayed; a leg's jump and the root are never held back (the feet and root are exact).
            var c=new HordePoseBlend();var q=new CapturedRig.Pose();for(int i=0;i<10;i++){q.Reset();var rr=root;float yy=0;c.Apply(q,ref rr,ref yy,1/144f);}
            q.Reset();q.Rotation[CapturedRig.RHand]=CapturedRig.Euler(2.4f);q.Rotation[CapturedRig.LKnee]=CapturedRig.Euler(1.5f);var r3=root+new Vector3(.3f,0,0);float y3=0;c.Apply(q,ref r3,ref y3,1/144f);
            Check(Math.Abs(Quaternion.Dot(q.Rotation[CapturedRig.RHand],Quaternion.Identity))>.999f,"an unmarked half-turn of a hand is held");
            Check(Math.Abs(Quaternion.Dot(q.Rotation[CapturedRig.LKnee],CapturedRig.Euler(1.5f)))>.9999f&&r3==root+new Vector3(.3f,0,0),"a leg's and the root's are not (their planted feet and path are exact)");
            Check(c.Pops==1&&c.Transitions==0,"counted as one caught pop");
            // The death's hand-over: the living blend's last shown pose carried over (CopyLast) and marked.
            var d=new HordePoseBlend();d.CopyLast(b);d.Mark();var e=new CapturedRig.Pose();var re=root;float ye=0;d.Apply(e,ref re,ref ye,1/60f);
            Check(Math.Abs(Quaternion.Dot(e.Rotation[CapturedRig.LArm],CapturedRig.Euler(1.2f,0,.4f)))>.995f,"a death takes over the living pose without a pop");
        }

        // ---- the add's traits and its sag ----
        private static void Life()
        {
            int dead=0,limp=0,n=0;var p=new CapturedRig.Pose();
            for(int id=20000;id<20200;id++){var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=0,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0)};g.Pose(f,p);n++;if(g.DeadArm>0)dead++;if(g.LimpAmount>0)limp++;
                var g2=new NativeHordeGait();g2.Pose(f,p);Check(g2.DeadArm==g.DeadArm&&g2.Loll==g.Loll&&g2.Hunch==g.Hunch&&g2.TicRate==g.TicRate,"an add's traits are its seed's (every peer draws the same)");}
            Check(dead>n/6&&dead<n/2&&limp>n/5&&limp<n*.6,"a share of a crowd drags a dead arm ("+dead+") or limps ("+limp+") of "+n);
            // Hurt, it sags: the head lower.
            float Head(float health){var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=0,Id=20007,Seed=NativeHordeKinds.Seed(20007),Health=health,AttackAge=float.NaN,Root=new Vector3(0,.85f,0)};float sum=0;for(int i=0;i<180;i++){f.Time=i/60f;g.Pose(f,p);if(i>=120){p.World();sum+=p.WorldPosition[CapturedRig.Head].Y;}}return sum/60;}
            Check(Head(.15f)<Head(1)-.02f,"a hurt add sags (its head lower)");
        }

        // ---- no allocation ----
        private static void Allocation()
        {
            var path=new Route(30,t=>(new Vector3((float)Math.Sin(t*.4)*2,0,(float)Math.Cos(t*.4)*2),float.NaN));var adds=new[]{new Add(0,77001),new Add(1,77002,1),new Add(2,77003),new Add(4,77004),new Add(6,77005,2)};
            var hc=new HordeClock();var flow=new HordeFlow();flow.Configure(1);double field=0;
            void Frame(int i)
            {
                double w=i/144.0;foreach(var a in adds){if(i%90==0)a.React.Add((float)w,new Vector2(1,0),StaggerWeight.Step,26,true,i%180==0,i%360==0,.45f);a.Frame(path,w,1/144f,(float)(Math.Floor(w/1.4)*1.4),.34f,1.05f);var r=a.Root;float y=a.Yaw;if(i%300==0)a.Blend.Mark();a.Blend.Apply(a.Pose,ref r,ref y,1/144f);}
                if(i%7==0)flow.Observe(w,new Vector3((float)w,0,0),(float)w*10);field+=1/144.0;hc.Step(field,1/144f,true);flow.Replica(hc.Time-.1,1/144f,i%50<10);
            }
            for(int i=0;i<600;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=600;i<3600;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the clock, flows, gaits with their secondary motion, reactions and pop killers allocate nothing per frame ("+used+" bytes over 3000 frames of five adds)");
        }

        // ---- the crowd tiers: what each costs per add per frame ----
        private static void Tiers(out string summary)
        {
            var path=new Route(12,Chase);var cost=new double[3];var lag=new float[3];
            for(int tier=0;tier<3;tier++)
            {
                var adds=new Add[24];for(int i=0;i<adds.Length;i++)adds[i]=new Add(i%3==0?1:0,78000+i,tier);
                for(int f=0;f<300;f++)foreach(var a in adds){a.Frame(path,f/144.0,1/144f);lag[tier]=Math.Max(lag[tier],a.Gait.HeadLag.Length());}
                var sw=Stopwatch.StartNew();int frames=0;for(int f=300;f<900;f++){foreach(var a in adds){a.Frame(path,f/144.0,1/144f);var r=a.Root;float y=a.Yaw;if(tier<2)a.Blend.Apply(a.Pose,ref r,ref y,1/144f);}frames++;}
                cost[tier]=sw.Elapsed.TotalMilliseconds*1000/(frames*adds.Length);
            }
            // (The timings are reported, not held: a shared machine's load moves them more than the tiers do. What the tiers skip is.)
            Check(lag[0]>.01f&&lag[1]>.01f&&lag[2]==0,"the impostor tier skips the secondary layers the nearer tiers draw (head lag "+lag[0].ToString("0.00")+", "+lag[1].ToString("0.00")+", "+lag[2].ToString("0.00")+" rad)");
            summary="per add per frame: Full "+cost[0].ToString("0")+" us, Reduced "+cost[1].ToString("0")+" us, Impostor "+cost[2].ToString("0")+" us (this machine, managed, 24 adds)";
        }

        // ---- the runtime: posed on the horde clock, the flow and the pop killer in the draw, nothing written back ----
        private static void Source(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var k=R("Runtime/NativeHordeKindPresentation.cs");
            Check(k.Contains("var s=HordeState(m.Id);float now=hordeNow;")&&k.Contains("hordeTimeline.Step(hordeClock,"),"every add is posed on the horde clock, never arena.Time");
            Check(k.Contains("HordeFlowStep(arena,m,s,kind,pos,now,")&&k.Contains("var fp=s.Flow.Position;pos=new Vector3(fp.X,fp.Y,fp.Z);")&&k.Contains("Flow=true,YawRate=s.Flow.YawRate,Bank=s.Flow.Bank,Pitch=s.Flow.Pitch,Tier=s.Tier"),"the drawn root, speed and facing are the flow's");
            Check(k.Contains("s.Blend.Apply(minionPose,ref r,ref yaw,hordeDt,")&&k.Contains("d.Blend.Apply(minionPose,ref hr,ref deathYaw,hordeDt,false,.15f);"),"switches and deaths go through the pop killer");
            Check(k.Contains("float fall=hordeNow-death.at;")&&k.Contains("StationDeathPose(d.Kind,d.Seed,fall);"),"the fall is posed on the horde clock, as the living add was");
            Check(k.Contains("HordeKindJob(s,m.Id,kind,attack,knocking?float.NaN:age,strike,recover,pos,m.Yaw,"),"the effects keep the authority's facing");
            var frames=R("Runtime/NativeReplicaFrames.cs");Check(!frames.Contains("replicaHistory.Add")&&!frames.Contains("replicaHistory.Remove")&&!frames.Contains("replicaHistory[i]=")&&!frames.Contains("SetReplicaPosition"),"the replica frames are only read");
            foreach(var f in new[]{"Core/NativeHordeFlow.cs","Core/NativeHordeBlend.cs","Core/NativeHordeLife.cs"}){var s=R(f);Check(!s.Contains("UnityEngine")&&!s.Contains("System.Linq")&&!s.Contains("new Random("),f+" is pure Core (no engine, no LINQ, no clock-free randomness)");}
            foreach(var bad in new[]{".Hp=","Damage(","StaggerAt=","LastAttack=","Body.position=","linearVelocity=","m.Yaw="})Check(!k.Contains(bad),"the horde presentation never writes `"+bad+"`");
        }
    }
}
#endif
