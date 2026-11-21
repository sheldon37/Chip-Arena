#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The boss motion foundation (owner 2026-10-17: the Central Station and City Ironworks bosses "a lot more fluid than what we
    // have now"; docs/BOSS-MOTION.md): springs converge without overshoot at critical damping and exactly at any frame rate; the
    // limited turn never overshoots or exceeds its limits; the replicated root moves continuously through 20 Hz snapshots with
    // jitter, late frames and noise (its acceleration bounded, no freezes) where the shipped follow's spikes and stalls; planted
    // feet do not move at all (biped and crawler, walking, running, stopping, turning sharply, shoved); chains hold their
    // lengths and the floor; the action timeline is a pure function of the authority's start (two peers agree to the bit) and
    // lands contact on the authority's hit; the pose is continuous across action starts, crossfades, cancels and blends (its
    // per-frame change halves when the frame rate doubles); the layers stay in their limits; nothing allocates per frame.
    internal static class BossMotionChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Boss motion: "+what);count++;}
        private static readonly IBossGround Floor=new BossFlatGround(0);
        public static void Run(string root)
        {
            MathBasics();Springs();SecondOrder();Turn();Follow();Jitter(out string jitter);Feet(out string feet);Chain();Timeline();Continuity(out string cont);Layers();Allocation();Source(root);
            Console.WriteLine("PASS boss motion ("+count+" checks: critically damped springs halve on their half-life, never overshoot, are exact at any frame rate and track moving goals with no lag; the limited turn keeps its rate and acceleration and stops on the goal; "+
                jitter+"; "+feet+"; chains hold length and floor; the action timeline is bit-identical across peers and lands contact on the authority's hit; "+cont+"; look-at, flinch, stagger and breath stay bounded and continuous; 0 bytes per frame)");
        }

        // ---- math ----
        private static bool Near(Vector3 a,Vector3 b,float e=1e-4f)=>Vector3.Distance(a,b)<=e;
        private static void MathBasics()
        {
            var f=Vector3.Normalize(new Vector3(.3f,-.2f,-1));var q=BossMath.LookRotation(f,Vector3.UnitY);
            Check(Near(BossMath.Rotate(q,Vector3.UnitZ),f),"LookRotation turns +Z to the forward");Check(BossMath.Rotate(q,Vector3.UnitY).Y>.9f,"LookRotation keeps +Y up");
            Check(Near(BossMath.Rotate(BossMath.Yaw(BossMath.Pi/2),Vector3.UnitZ),Vector3.UnitX),"a yaw of +90 degrees turns +Z to +X (Unity's Euler(0, 90, 0))");
            Check(Near(BossMath.Forward(.7f),BossMath.Rotate(BossMath.Yaw(.7f),Vector3.UnitZ))&&Near(BossMath.Right(.7f),BossMath.Rotate(BossMath.Yaw(.7f),Vector3.UnitX)),"facing and right agree with the yaw");
            var e=BossMath.Euler(.3f,1.1f,-.4f);var e2=BossMath.Yaw(1.1f)*BossMath.AxisAngle(Vector3.UnitX,.3f)*BossMath.AxisAngle(Vector3.UnitZ,-.4f);
            Check(Math.Abs(Quaternion.Dot(e,e2))>.99999f,"Euler applies z, then x, then y (Unity's order)");
            Check(Math.Abs(Quaternion.Dot(BossMath.Exp(BossMath.Log(e)),e))>.99999f,"log and exp round-trip a rotation");
            Check(Math.Abs(Quaternion.Dot(BossMath.Bone(Vector3.Zero,-Vector3.UnitY,Vector3.UnitZ),Quaternion.Identity))>.99999f,"a limb hanging down with its knee forward is the identity");
            var b=BossMath.Bone(Vector3.Zero,new Vector3(1,-1,.5f),Vector3.UnitZ);Check(Near(BossMath.Rotate(b,-Vector3.UnitY),Vector3.Normalize(new Vector3(1,-1,.5f))),"a limb's -Y runs down the bone");
            for(float a=-20;a<20;a+=.37f){float w=BossMath.WrapPi(a);Check(w>-BossMath.Pi-1e-5f&&w<=BossMath.Pi+1e-5f&&Math.Abs(Math.Sin(w)-Math.Sin(a))<1e-3,"WrapPi wraps into (-pi, pi]");}
            float last=BossMath.Noise(7,0);for(float t=.001f;t<6;t+=.001f){float n=BossMath.Noise(7,t);Check(Math.Abs(n)<=1.0001f&&Math.Abs(n-last)<.01f,"noise is bounded and continuous");last=n;}
        }

        // ---- springs ----
        private static void Springs()
        {
            const float h=.2f;
            var one=new BossSpring();one.Reset(0);one.Step(1,h,h);Check(Math.Abs(one.Value-.5f)<1e-4f,"a spring at rest is halfway to a still goal after one half-life (one step)");
            foreach(float hz in new[]{30f,60f,144f,240f})
            {
                var s=new BossSpring();s.Reset(0);int n=(int)Math.Round(h*hz);float last=0;bool mono=true,under=true;
                for(int i=0;i<n;i++){s.Step(1,h,1/hz);mono&=s.Value>=last-1e-7f;under&=s.Value<=1+1e-6f;last=s.Value;}
                float y=BossSpring.HalfLifeConstant*n/hz/h,exact=1-(1+y)*(float)Math.Exp(-y);
                Check(Math.Abs(s.Value-exact)<2e-5f,"exact at "+hz+" Hz: on the closed form after "+n+" steps ("+s.Value+" vs "+exact+")");
                for(int i=0;i<(int)(5*hz);i++){s.Step(1,h,1/hz);mono&=s.Value>=last-1e-7f;under&=s.Value<=1+1e-6f;last=s.Value;}
                Check(mono&&under,"no overshoot at critical damping ("+hz+" Hz): rises monotonically and never passes the goal");
                Check(1-s.Value<1e-4f,"converges within 1e-4 in 26 half-lives ("+hz+" Hz)");
            }
            // A goal moving at 3 m/s: started on it at its speed the spring never lags; started at rest the lag dies away.
            var m=new BossSpring();m.Reset(0);m.Velocity=3;float g=0,worst=0;for(int i=0;i<600;i++){m.Step(g,3,.1f,1/144f);g+=3/144f;worst=Math.Max(worst,Math.Abs(m.Value-g));}
            Check(worst<1e-3f,"a goal-velocity spring tracks a moving goal with no lag ("+worst+")");
            m.Reset(0);g=0;for(int i=0;i<288;i++){m.Step(g,3,.1f,1/144f);g+=3/144f;}Check(Math.Abs(m.Value-g)<1e-3f&&Math.Abs(m.Velocity-3)<1e-2f,"from rest the lag decays to nothing (no steady error)");
            var v=new BossSpring3();v.Reset(Vector3.Zero);var goal=new Vector3(3,-1,2);float lastD=goal.Length();bool shrink=true;
            for(int i=0;i<720;i++){v.Step(goal,.15f,1/144f);float d=Vector3.Distance(v.Value,goal);shrink&=d<=lastD+1e-6f;lastD=d;}Check(shrink&&lastD<1e-4f,"the vector spring closes monotonically and converges");
            var q=new BossSpringQ();q.Reset(Quaternion.Identity);var qg=BossMath.Euler(.6f,2.1f,-.3f);float lastA=4;shrink=true;
            for(int i=0;i<720;i++){q.Step(qg,.15f,1/144f);float a=BossMath.Log(q.Value*BossMath.Inverse(qg)).Length();shrink&=a<=lastA+1e-5f;lastA=a;}
            Check(shrink&&lastA<2e-3f,"the rotation spring turns monotonically onto its goal ("+lastA+" rad left)");
        }
        private static void SecondOrder()
        {
            float Max(float f,float z,float r,float hz,out float end)
            {var s=new BossSecondOrder();s.Configure(f,z,r);s.Reset(0);float m=0;for(int i=0;i<(int)(6*hz);i++){s.Step(1,1/hz);m=Math.Max(m,s.Value);}end=s.Value;return m;}
            float crit=Max(2,1,0,144,out float e1);Check(crit<=1.002f&&Math.Abs(e1-1)<1e-3f,"second order at damping 1 settles without overshoot ("+crit+")");
            float wob=Max(2,.35f,0,144,out float e2);Check(wob>1.2f&&Math.Abs(e2-1)<1e-2f,"below 1 it overshoots ("+wob+") and settles");
            var a=new BossSecondOrder();a.Configure(2,.6f,-1.5f);a.Reset(0);float min=0;for(int i=0;i<300;i++){a.Step(1,1/144f);min=Math.Min(min,a.Value);}Check(min<-.05f,"a negative response anticipates (moves the wrong way first)");
            float big=Max(6,.5f,0,10,out float e3);Check(!float.IsNaN(big)&&big<2&&Math.Abs(e3-1)<.02f,"stable at 10 Hz frames for a 6 Hz system (sub-stepped)");
            var s30=new BossSecondOrder();var s144=new BossSecondOrder();s30.Configure(2,.5f,0);s144.Configure(2,.5f,0);s30.Reset(0);s144.Reset(0);
            for(int i=0;i<30;i++)s30.Step(1,1/30f);for(int i=0;i<144;i++)s144.Step(1,1/144f);Check(Math.Abs(s30.Value-s144.Value)<.01f,"the same after a second at 30 and 144 Hz");
        }
        private static void Turn()
        {
            foreach(float goalDeg in new[]{170f,-170f,90f,350f,-20f})
            {
                var t=new BossTurn();t.Reset(0);float goal=goalDeg*BossMath.Deg,want=BossMath.WrapPi(goal),maxRate=2,maxAccel=6;float lastRate=0,beyond=0;bool limits=true;
                for(int i=0;i<1440;i++)
                {
                    t.Step(goal,.12f,maxRate,maxAccel,1/144f);limits&=Math.Abs(t.Rate)<=maxRate+1e-4f&&Math.Abs(t.Rate-lastRate)<=maxAccel/144f+1e-4f;lastRate=t.Rate;
                    beyond=Math.Max(beyond,Math.Sign(want)*(t.Yaw-want));
                }
                Check(limits,"the turn keeps its rate and acceleration limits (to "+goalDeg+")");
                Check(beyond<.2f*BossMath.Deg,"the turn stops on the goal without passing it (to "+goalDeg+", by "+beyond/BossMath.Deg+" deg)");
                Check(Math.Abs(t.Yaw-want)<.2f*BossMath.Deg&&Math.Abs(t.Rate)<1e-2f,"the turn arrives the short way and rests (to "+goalDeg+")");
            }
            var h=new BossTurn();h.Reset(0);for(int i=0;i<40;i++)h.Step(179*BossMath.Deg,.12f,2,6,1/144f);float dir=Math.Sign(h.Rate);bool kept=true;
            for(int i=0;i<200;i++){h.Step((i%2==0?-179:179)*BossMath.Deg,.12f,2,6,1/144f);kept&=Math.Sign(h.Rate)==dir||h.Rate==0;}
            Check(kept,"a goal flickering across the half turn does not reverse the turn");
        }

        // ---- the replicated root ----
        private static void Follow()
        {
            var f=new BossRootFollow();var vel=new Vector3(2,0,1);double local=500;float worst=0,speedErr=0;
            for(int i=0;i<720;i++)
            {
                float dt=1/144f;local+=dt;double auth=local-500;if(i%7==0)f.Observe(auth,local+.04,vel*(float)auth,BossMath.YawOf(vel));
                f.Step(local,dt);if(i<200)continue;var truth=vel*(float)f.RenderTime;worst=Math.Max(worst,Vector3.Distance(truth,f.Position));speedErr=Math.Max(speedErr,Math.Abs(f.Speed-vel.Length()));
            }
            Check(worst<.02f&&speedErr<.05f,"clean 20 Hz snapshots: on the path ("+worst+" m) at its speed ("+speedErr+" m/s)");
            Check(Math.Abs(f.Clock.Delay-.1f)<1e-6f,"the render clock holds its delay");
            // A teleport (more than TeleportDistance) snaps and reports the jump once.
            var t=new BossRootFollow();double l=0;int jumps=0;
            for(int i=0;i<600;i++){float dt=1/144f;l+=dt;if(i%7==0)t.Observe(l,l+.04,new Vector3(l<1.5?0:20,0,0),0);t.Step(l,dt);if(t.Jumped)jumps++;}
            Check(jumps==1&&Math.Abs(t.Position.X-20)<1e-3f,"a teleport snaps the root once ("+jumps+")");
        }
        /// <summary>The shipped replica follow, reproduced for the baseline: Core/NativePrediction.cs NativeSnapshotClock and the
        /// minion branch of Runtime/NativeReplicaMotion.cs InterpolateReplica (two-snapshot lerp, clamped at the newest).</summary>
        private sealed class OldSnapFollow
        {
            private double time,latest;private readonly List<(double t,Vector3 p)> h=new List<(double,Vector3)>();
            public void Observe(double t,Vector3 p){if(h.Count==0){latest=t;time=t-.1;}h.Add((t,p));while(h.Count>32)h.RemoveAt(0);latest=Math.Max(latest,t);if(latest-time>.5)time=latest-.1;}
            public Vector3 Sample(float delta)
            {
                time=Math.Min(latest,time+Math.Max(0,Math.Min(.1,delta)));while(h.Count>2&&h[1].t<=time)h.RemoveAt(0);
                var a=h[0];var b=h.Count>1?h[1]:a;float blend=BossMath.Clamp01((float)((time-a.t)/Math.Max(.00001,b.t-a.t)));return Vector3.Distance(a.p,b.p)<5?Vector3.Lerp(a.p,b.p,blend):b.p;
            }
        }
        /// <summary>A boss path: accelerate, run a curve, brake, stand (60 Hz authority, its yaw snapping to the heading).</summary>
        private static void Route(double t,out Vector3 p)
        {
            // Speed 0 until .5 s, up to 3.2 m/s by 2 s, a curve of radius 6 m from 3 to 5 s, braking to rest by 6.5 s: integrated analytically
            // enough for a test (positions from a fine sum, cached).
            int i=(int)Math.Max(0,Math.Min(pathCache.Length-1,Math.Round(t*600)));p=pathCache[i];
        }
        private static readonly Vector3[] pathCache=BuildPath();
        private static Vector3[] BuildPath()
        {
            var r=new Vector3[600*10];var p=Vector3.Zero;float heading=0;
            for(int i=0;i<r.Length;i++)
            {
                float t=i/600f,speed=t<.5f?0:t<2?3.2f*(t-.5f)/1.5f:t<5?3.2f:t<6.5f?3.2f*(6.5f-t)/1.5f:0;if(t>=3&&t<5)heading+=speed/6/600f;
                p+=BossMath.Forward(heading)*(speed/600f);r[i]=p;
            }
            return r;
        }
        private static void Jitter(out string summary)
        {
            var rnd=new Random(1017);float Gauss(){double u=1-rnd.NextDouble(),v=rnd.NextDouble();return (float)(Math.Sqrt(-2*Math.Log(u))*Math.Cos(2*Math.PI*v));}
            // The host sends at 20 Hz (frame-quantised); 45 ms + jitter, and after the first second 5% of frames 120 ms late (ordered
            // delivery); positions carry 3 mm of noise (the authority's own micro-jitter: the wire carries full floats); the guest
            // renders at 144 Hz +-15%.
            var sends=new List<(double at,double t)>();double host=0,next=0;while(host<9){host+=1/60.0*(1+.18*(rnd.NextDouble()-.5));if(host>=next){next=host+.05;sends.Add((host,Math.Floor(host*60)/60));}}
            var arrive=new List<(double at,double t,Vector3 p)>();double last=0;
            foreach(var s in sends){double at=Math.Max(last,s.at+.045+Math.Abs(Gauss())*.014+(s.at>1&&rnd.NextDouble()<.05?.12:0));last=at;Route(s.t,out var p);arrive.Add((at,s.t,p+new Vector3(Gauss(),0,Gauss())*.003f));}
            var f=new BossRootFollow();var old=new OldSnapFollow();double local=2000;int k=0;
            Vector3 pn=default,vn=default,po=default,vo=default;bool have=false;float maxNew=0,maxOld=0,minRatio=9;int oldStalls=0,frames=0;
            while(local<2000+8.5)
            {
                float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));local+=dt;
                while(k<arrive.Count&&arrive[k].at+2000<=local){f.Observe(arrive[k].t,local,arrive[k].p,0);old.Observe(arrive[k].t,arrive[k].p);k++;}
                if(k==0)continue;f.Step(local,dt);var o=old.Sample(dt);
                if(have)
                {
                    var nv=(f.Position-pn)/dt;var ov=(o-po)/dt;
                    if(local-2000>1.2)
                    {
                        frames++;maxNew=Math.Max(maxNew,(nv-vn).Length()/dt);maxOld=Math.Max(maxOld,(ov-vo).Length()/dt);
                        Route(f.RenderTime,out var a);Route(f.RenderTime-.02,out var b);float truth=Vector3.Distance(a,b)/.02f;
                        if(truth>1){minRatio=Math.Min(minRatio,nv.Length()/truth);if(ov.Length()<.25f*truth)oldStalls++;}
                    }
                    vn=nv;vo=ov;
                }
                pn=f.Position;po=o;have=true;
            }
            Check(frames>1000,"the jitter run covers the path ("+frames+" frames)");
            Check(maxNew<18,"jittery 20 Hz snapshots: the presented acceleration stays bounded ("+maxNew.ToString("0.0")+" m/s^2; the path's own peak is about 2.5)");
            Check(maxNew*4<maxOld,"and well under the shipped follow's spikes ("+maxOld.ToString("0")+" m/s^2)");
            Check(minRatio>.7f,"the root never stalls while the boss moves (slowest frame "+minRatio.ToString("0.00")+" of the true speed)");
            summary="jittery 20 Hz snapshots (45 ms, +-14 ms, 5% 120 ms late, 3 mm noise, 144 Hz +-15% frames): peak acceleration "+maxNew.ToString("0.0")+" m/s^2 against the shipped follow's "+maxOld.ToString("0")+", no stall (slowest "+(minRatio*100).ToString("0")+"% of true speed; the shipped follow stalled on "+oldStalls+" moving frames)";
        }

        // ---- the gait: planted feet never move ----
        private static void Feet(out string summary)
        {
            int planted=0,steps=0;float drift=0,reach=0;
            foreach(bool crawler in new[]{false,true})
            {
                var def=crawler?BossCrawlerRig.Gait:BossBipedRig.Gait;var g=new BossGait(def);var follow=new BossRootFollow();var rnd=new Random(crawler?5:3);
                var prev=new Vector3[g.Legs];var was=new bool[g.Legs];var pos=Vector3.Zero;float heading=0,speed=0;double t=0;
                for(int i=0;i<144*14;i++)
                {
                    float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));t+=dt;
                    // walk, run, stop, a sharp turn at a walk, a stop, a shove
                    float want=t<1?0:t<3?1.4f:t<5.5?def.RunSpeed:t<7?0:t<10?1.4f:0;speed+=BossMath.Clamp(want-speed,-4*dt,3*dt);float wantHeading=t<8?0:2.6f;
                    heading+=BossMath.Clamp(BossMath.WrapPi(wantHeading-heading),-10*dt,10*dt);pos+=BossMath.Forward(heading)*(speed*dt);
                    var shove=t>11.5&&t<12.5?new Vector3(.8f*(float)Math.Sin(Math.Min(1,(t-11.5)*4)*Math.PI/2),0,0):Vector3.Zero;
                    follow.Follow(pos,BossMath.Forward(heading)*speed,heading,dt);
                    g.Step(follow.Position+shove,follow.Yaw,follow.YawRate,follow.Velocity,follow.Body,dt,Floor);
                    for(int l=0;l<g.Legs;l++)
                    {
                        if(g.Planted[l]&&was[l]){planted++;drift=Math.Max(drift,Vector3.Distance(prev[l],g.Foot[l]));}
                        if(g.Planted[l]){var ld=def.Legs[l];var end=g.Knee[l]+BossMath.Rotate(g.Lower[l],new Vector3(0,-ld.Lower,0));reach=Math.Max(reach,Vector3.Distance(end,g.Foot[l]));}
                        if((g.Landed&(1<<l))!=0)steps++;prev[l]=g.Foot[l];was[l]=g.Planted[l];
                        if(g.Moving)Check(Math.Abs(g.Frequency*g.Stride-g.Speed)<1e-3f,"the cadence times the stride is the ground speed");
                    }
                }
                // Standing still at the end, every foot is down and squared up under the body.
                bool down=true;for(int l=0;l<g.Legs;l++)down&=g.Planted[l];Check(down,(crawler?"crawler":"biped")+": standing, every foot is down");
                for(int l=0;l<g.Legs;l++){var home=follow.Position+BossMath.Rotate(BossMath.Yaw(follow.Yaw),new Vector3(def.Legs[l].Home.X,0,def.Legs[l].Home.Z));
                    Check(Vector3.Distance(BossMath.Flat(home),BossMath.Flat(g.Foot[l]))<=def.SettleDistance+1e-3f,(crawler?"crawler":"biped")+": the feet settle under the body after a stop");}
            }
            Check(planted>8000&&steps>60,"the feet were checked on "+planted+" planted frames over "+steps+" steps");
            Check(drift<1e-6f,"a planted foot never moves (largest move "+drift+" m)");
            Check(reach<.05f,"the leg reaches its planted foot (largest gap "+reach.ToString("0.000")+" m)");
            summary="planted feet never move ("+planted+" planted frames, "+steps+" steps, biped and crawler: walk, run, stop, sharp turn, shove; largest move "+drift+" m, leg-to-foot gap "+reach.ToString("0.000")+" m)";
        }

        private static void Chain()
        {
            var c=new BossChain(new BossChainDef{Count=10,Length=.2f,Stiffness=0,TipStiffness=0,Damping=1,Rest=new Vector3(1,0,0)});c.Reset(new Vector3(0,3,0),Quaternion.Identity);
            for(int i=0;i<144*6;i++)c.Step(new Vector3(0,3,0),Quaternion.Identity,1/144f,Floor);
            Check(c.Point[9].Y<3-.2f*9*.98f&&Math.Abs(c.Point[9].X)<.05f,"a free chain started sideways hangs straight down");
            for(int i=1;i<10;i++)Check(Math.Abs(Vector3.Distance(c.Point[i],c.Point[i-1])-.2f)<1e-3f,"its links keep their length");
            var tail=new BossChain(BossCrawlerRig.TailDef);tail.Reset(new Vector3(0,1.2f,0),Quaternion.Identity);var at=new Vector3(0,1.2f,0);
            for(int i=0;i<144*8;i++)
            {
                float t=i/144f;at.Z+=(t<4?3:0)/144f;var turn=BossMath.Yaw(t<4?(float)Math.Sin(t*2):0);tail.Step(at,turn,1/144f,Floor);
                for(int k=1;k<tail.Point.Length;k++){Check(!float.IsNaN(tail.Point[k].X),"the tail stays finite");Check(tail.Point[k].Y>=BossCrawlerRig.TailDef.Radius-1e-3f,"the tail stays on the floor");}
            }
            var before=tail.Point[15];tail.Step(at,Quaternion.Identity,1/144f,Floor);Check(Vector3.Distance(before,tail.Point[15])<1e-3f,"the dragged tail comes to rest");
            var big=new BossChain(BossCrawlerRig.TailDef);big.Reset(Vector3.Zero,Quaternion.Identity);big.Step(new Vector3(3,1,2),BossMath.Yaw(2),.1f,Floor);
            Check(!float.IsNaN(big.Point[15].X)&&Vector3.Distance(big.Point[15],new Vector3(3,1,2))<4.3f,"a long frame keeps the chain whole");
        }

        // ---- the action timeline ----
        private static void Timeline()
        {
            var def=BossBipedRig.Slam;Check(Math.Abs(def.HitAt-1.01f)<1e-5f&&def.ContactIndex==2,"the slam's authored hit is its anticipation plus strike");
            // The same age gives the same sample, however it was reached.
            var x=BossTimeline.Sample(def,1.234f);var y=BossTimeline.Sample(def,1.234f);Check(x.Progress==y.Progress&&x.Weight==y.Weight&&x.PhaseIndex==y.PhaseIndex,"the timeline is a pure function of the age");
            // Two peers: one sees the action at its start and samples at 60 Hz, the other joins late and samples at 144 Hz with
            // jitter; at the same authority times they agree to the bit.
            var a=new BossActionPlayer();var b=new BossActionPlayer();double start=10.25;var rnd=new Random(4);double tb=10.6;bool same=true;int compared=0;
            for(double ta=10.0;ta<13.5;ta+=1/60.0)
            {
                if(ta>=start)a.Play(def,7,start,1.2f,2.6f);a.Sample(ta);
                // (b joins at 10.6 s, a third of a second into the action: it has the record from then on.)
                while(tb<ta){tb+=1/144.0*(1+.3*(rnd.NextDouble()-.5));if(tb>=10.6)b.Play(def,7,start,1.2f,2.6f);}
                b.Sample(ta);if(b.CurrentDef==null)continue;compared++;same&=a.Current.Progress==b.Current.Progress&&a.Current.Weight==b.Current.Weight&&a.Current.Phase==b.Current.Phase&&a.Current.PhaseT==b.Current.PhaseT;
                BossBipedRig.SlamChannels(a.Current.Progress,out float s1,out float e1,out float p1,out float c1,out float h1);BossBipedRig.SlamChannels(b.Current.Progress,out float s2,out float e2,out float p2,out float c2,out float h2);
                same&=s1==s2&&e1==e2&&p1==p2&&c1==c2&&h1==h2;
            }
            Check(same&&compared>150,"two peers at different frame rates, one joining late, reproduce the same action pose at the same time ("+compared+" times)");
            // The authority's hit and length stretch the phases: contact lands on its hit at any timing.
            foreach(float hit in new[]{.7f,.99f,1.4f})
            {
                var at=BossTimeline.Sample(def,hit,hit,hit+1.6f);var before=BossTimeline.Sample(def,hit-.001f,hit,hit+1.6f);
                Check(at.Phase==BossPhaseKind.Contact&&Math.Abs(at.Progress-2)<1e-4f&&before.Phase==BossPhaseKind.Strike&&before.Progress<2,"contact lands on the authority's hit ("+hit+" s)");
                var end=BossTimeline.Sample(def,hit+1.6f,hit,hit+1.6f);Check(!end.Active&&end.Weight==0,"the action ends on the authority's length");
            }
            // The strike accelerates into contact; the anticipation starts and ends still.
            float v0=BossTimeline.Sample(def,.001f).Progress/.001f,vHit=(BossTimeline.Sample(def,1.01f).Progress-BossTimeline.Sample(def,1.009f).Progress)/.001f,vWound=(BossTimeline.Sample(def,.75f).Progress-BossTimeline.Sample(def,.749f).Progress)/.001f;
            Check(v0<.05f&&vWound<.05f&&vHit>6,"anticipation eases in and out ("+v0+", "+vWound+"); the strike is fastest at contact ("+vHit+"/s)");
            float peak=0;for(float t=1.49f;t<2.34f;t+=.001f)peak=Math.Max(peak,BossTimeline.Sample(def,t).Progress);Check(peak>4.02f,"the recovery overshoots rest and settles back ("+peak+")");
            Check(Math.Abs(BossTimeline.Sample(def,2.339f).Progress-4)<.01f,"the recovery lands on rest");
            float w0=BossTimeline.Sample(def,0).Weight,wMid=BossTimeline.Sample(def,1).Weight;Check(w0==0&&wMid==1,"the weight blends in from nothing");
            foreach(BossEase e in Enum.GetValues(typeof(BossEase)))if(e!=BossEase.Hold)Check(Math.Abs(BossEasing.Eval(e,0))<1e-6f&&Math.Abs(BossEasing.Eval(e,1)-1)<1e-5f,"ease "+e+" runs from 0 to 1");
        }

        // ---- continuity: the pose across action starts, crossfades, cancels and blends ----
        private static void Continuity(out string summary)
        {
            float Run(int hz,out float turn,List<float> events,List<float> eventMoves)
            {
                var rig=new BossBipedRig();var pos=Vector3.Zero;float speed=0,move=0;turn=0;var last=new BossPose(BossBipedRig.Count);bool have=false;
                for(int i=0;i<hz*9;i++)
                {
                    double t=i/(double)hz;float dt=1f/hz;float want=t<.5?0:t<2.2?1.4f:t<6.4?0:1.2f;speed+=BossMath.Clamp(want-speed,-3*dt,2.5f*dt);pos+=Vector3.UnitZ*(speed*dt);
                    rig.Follow.Follow(pos,Vector3.UnitZ*speed,0,dt);
                    if(t>=1.8)rig.Actions.Play(BossBipedRig.Slam,1,1.8);               // starts while walking
                    if(t>=3.6)rig.Actions.Play(BossBipedRig.Slam,2,3.6);               // a second slam crossfades over the first's recovery
                    if(t>=4.9)rig.Actions.Play(BossBipedRig.Slam,3,4.9);               // a third, cancelled by a stagger in its wind-up
                    if(t>=5.3){rig.Actions.Cancel(5.3);rig.Stagger.Start(5.3,new Vector3(0,0,-1),1,1.2f,rig.Follow.Yaw);}
                    if(t>=6.8)rig.Flinch.Add(6.8,new Vector3(1,0,0),1,rig.Follow.Yaw);
                    if(t>=7.1)rig.Flinch.Add(7.1,new Vector3(0,0,-1),1.2f,rig.Follow.Yaw);
                    rig.Update(t,dt,Floor,true,new Vector3(0,1.6f,8));
                    if(have)
                    {
                        rig.Pose.Delta(last,out float m,out float r);move=Math.Max(move,m);turn=Math.Max(turn,r);
                        for(int k=0;k<events.Count;k++)if(Math.Abs(t-events[k])<.06)eventMoves[k]=Math.Max(eventMoves[k],m);
                    }
                    last.CopyFrom(rig.Pose);have=true;
                }
                return move;
            }
            var ev=new List<float>{1.8f,1.8f+1.01f,3.6f,4.9f,5.3f,6.8f,7.1f};var m240=new List<float>(new float[ev.Count]);var m480=new List<float>(new float[ev.Count]);
            float a=Run(240,out float ta,ev,m240),b=Run(480,out float tb,ev,m480);
            Check(a<.3f&&ta<.35f,"at 240 Hz no joint moves more than .3 m or turns more than .35 rad in a frame ("+a.ToString("0.000")+" m, "+ta.ToString("0.000")+" rad, the fists at the strike)");
            Check(b<.62f*a&&tb<.62f*ta,"doubling the frame rate halves the largest per-frame change (continuous: "+(b/a).ToString("0.00")+", "+(tb/ta).ToString("0.00")+")");
            string[] names={"slam start while walking","contact","crossfade to a second slam","third slam","cancel and stagger","flinch","second flinch"};
            for(int k=0;k<ev.Count;k++)Check(m480[k]<.65f*m240[k]+1e-4f,"no pop at the "+names[k]+" ("+m240[k].ToString("0.000")+" m per 1/240 s, "+m480[k].ToString("0.000")+" per 1/480 s)");
            summary="the pose is continuous across action starts, contact, a crossfade, a cancel into a stagger and flinches (doubling the frame rate halves every per-frame change: "+(b/a).ToString("0.00")+")";
        }

        // ---- the layers ----
        private static void Layers()
        {
            var look=new BossLookAt();look.Reset();for(int i=0;i<300;i++)look.Step(Vector3.Zero,0,true,new Vector3(3,-2,3),1/144f);
            Check(Math.Abs(look.Yaw-BossMath.Pi/4)<.02f&&look.Pitch>.2f,"look-at turns onto a target in its range ("+look.Yaw+"), down to a lower one ("+look.Pitch+")");
            for(int i=0;i<300;i++)look.Step(Vector3.Zero,0,true,new Vector3(5,0,.4f),1/144f);Check(look.Yaw<=look.MaxYaw+1e-4f&&look.Yaw>.5f,"past its limit it holds at the limit, fading ("+look.Yaw+")");
            for(int i=0;i<500;i++)look.Step(Vector3.Zero,0,true,new Vector3(-.2f,0,-5),1/144f);Check(look.Weight<.05f&&Math.Abs(look.Yaw)<.1f,"a target behind fades the look back to centre");
            float lastY=look.Yaw;bool capped=true;look.Reset();for(int i=0;i<60;i++){look.Step(Vector3.Zero,0,true,new Vector3(5,0,.1f),1/144f);capped&=Math.Abs(look.Yaw-lastY)<=look.MaxRate/144f*1.05f+1e-3f;lastY=look.Yaw;}
            Check(capped,"the look never turns faster than its rate");
            var fl=new BossFlinch();fl.Add(1,new Vector3(0,0,1),1,0);fl.Add(1,new Vector3(0,0,1),1,0);float peak=0,peakAt=0;
            for(float t=1;t<2.8f;t+=.001f){fl.Sample(t,out float p,out _,out _);if(p>peak){peak=p;peakAt=t;}}
            fl.Sample(1,out float p0,out _,out _);fl.Sample(2.7,out float pEnd,out _,out _);
            Check(p0==0&&pEnd==0&&Math.Abs(peakAt-1-fl.Rise)<.02f&&Math.Abs(peak-fl.PitchGain)<.08f,"a flinch rises from nothing to its peak at its rise and dies away (a duplicate blow ignored)");
            var st=new BossStagger();st.Start(2,new Vector3(-1,0,0),1,1.2f,0);bool on=st.Sample(2.4,out var sh,out _,out _,out float cr,out float w);
            Check(on&&sh.X<-.2f&&cr>0&&w>.9f,"a stagger throws the hips along the push and bends the knees");Check(!st.Sample(3.21,out _,out _,out _,out _,out _),"and is over at its length");
            st.Sample(3.199,out var late,out _,out _,out _,out float wl);Check(late.Length()<.05f&&wl<.05f,"it returns to rest before it ends");
            float prev=BossBreath.Sample(0,.25f,9);float maxStep=0;for(int i=1;i<20000;i++){float b=BossBreath.Sample(i*.001,.25f,9);maxStep=Math.Max(maxStep,Math.Abs(b-prev));prev=b;}
            Check(maxStep<.01f,"the breath is continuous ("+maxStep+" per ms)");
        }

        // ---- no allocation per frame ----
        private static void Allocation()
        {
            var biped=new BossBipedRig();var crawler=new BossCrawlerRig();double local=100;var pos=Vector3.Zero;
            void Frame(int i)
            {
                float dt=1/144f;local+=dt;double t=local-100;pos+=new Vector3(1,0,.5f)*dt;
                if(i%7==0){biped.Follow.Observe(t,local+.05,pos,.4f);crawler.Follow.Observe(t,local+.05,pos+Vector3.UnitX*8,.4f);}
                biped.Follow.Step(local,dt);crawler.Follow.Step(local,dt);
                biped.Actions.Play(BossBipedRig.Slam,i/400,(i/400)*400/144.0);biped.Flinch.Add((i/90)*90/144.0,Vector3.UnitX,1,biped.Follow.Yaw);
                biped.Update(biped.Follow.RenderTime,dt,Floor,true,new Vector3(3,1,3));crawler.Update(crawler.Follow.RenderTime,dt,Floor,true,new Vector3(3,1,3));
            }
            for(int i=0;i<600;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=600;i<3600;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the rigs (follow, gait, chains, actions, layers, pose) allocate nothing per frame ("+used+" bytes over 3000 frames)");
        }

        private static void Source(string root)
        {
            string dir=Path.Combine(root,"Assets/ChipArena/Core");int files=0;
            foreach(var f in Directory.GetFiles(dir,"NativeBossMotion*.cs"))
            {
                files++;string s=File.ReadAllText(f);Check(!s.Contains("UnityEngine")&&!s.Contains("System.Linq"),Path.GetFileName(f)+" is pure Core (no UnityEngine, no LINQ)");
                Check(File.Exists(f+".meta"),Path.GetFileName(f)+" has its Unity meta");
            }
            Check(files==8,"the eight boss motion files ("+files+")");
            string apply=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBossPoseApply.cs"));Check(apply.Contains("BossPose")&&apply.Contains("SetPositionAndRotation"),"the runtime applies the pose to a model's parts");
            string doc=File.ReadAllText(Path.Combine(root,"../../docs/BOSS-MOTION.md"));
            foreach(string h in new[]{"## Audit","## API","## Example wiring","## Tuning","## What the authority must replicate"})Check(doc.Contains(h),"docs/BOSS-MOTION.md has "+h);
        }
    }
}
#endif
