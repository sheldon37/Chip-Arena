using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The floor under a point (world x, z): its height. The runtime answers from the map (a raycast or the nav floor);
    /// the checks and the offline renders use <see cref="BossFlatGround"/>.</summary>
    public interface IBossGround{float Height(float x,float z);}
    public sealed class BossFlatGround:IBossGround{public float Y;public BossFlatGround(float y=0){Y=y;}public float Height(float x,float z)=>Y;}

    /// <summary>One leg, in the body's frame (x right, y up, z forward; metres from the body root on the ground under it).</summary>
    public struct BossLegDef
    {
        /// <summary>The hip (the leg's root joint) relative to the body origin (<see cref="BossGait.BodyPosition"/>).</summary>
        public Vector3 Hip;
        /// <summary>Where the foot stands at rest, relative to the root on the ground (its y is ignored: the ground decides).</summary>
        public Vector3 Home;
        /// <summary>The knee's bend direction (body frame): forward for a biped, up and out for a crawler.</summary>
        public Vector3 Pole;
        /// <summary>Thigh and shin lengths, the cycle offset (0..1; legs sharing one step together), the side (-1 left, 1 right),
        /// the ankle's height over the floor.</summary>
        public float Upper,Lower,Phase,Side,Ankle;
    }

    /// <summary>A gait's tuning (one per boss kind; shared by every instance). Speeds in m/s, strides in metres per cycle
    /// (one step of every leg), duty the stance share of a cycle, heights in metres, angles in radians.</summary>
    public sealed class BossGaitDef
    {
        public BossLegDef[] Legs=new BossLegDef[0];
        public float WalkSpeed=1.2f,RunSpeed=3.2f,WalkStride=1.8f,RunStride=3.4f,WalkDuty=.62f,RunDuty=.46f,WalkLift=.16f,RunLift=.32f;
        /// <summary>The slowest cadence while moving (Hz: slow walking takes short steps), the swing's time limits (s), the
        /// speeds the cycle starts and stops at (hysteresis).</summary>
        public float MinFrequency=.4f,MinSwing=.2f,MaxSwing=.9f,StartSpeed=.2f,StopSpeed=.08f;
        /// <summary>Standing: a foot further than this from home steps back under the body (one group at a time), taking this long.</summary>
        public float SettleDistance=.16f,SettleSwing=.42f;
        /// <summary>Moving: a planted foot this much beyond its stance range steps at once (a sharp turn, a shove); and any planted
        /// foot the hips would have to sink more than <see cref="MaxSink"/> (a share of the leg's length) to reach steps too.</summary>
        public float ReachMargin=.35f,MaxSink=.05f;
        /// <summary>The body origin's height over the ground (the pelvis for a biped, the hull for a crawler).</summary>
        public float BodyHeight=1;
        /// <summary>The body's dip at double support (m, walk and run), lateral sway toward the stance side (m), bank with
        /// support (rad), pelvis twist with the stride (rad), its dip per footfall (m/s of downward speed at walk pace).</summary>
        public float Bob=.04f,RunBob=.07f,Sway=.05f,Roll=.04f,Twist=.08f,LandDip=.25f;
        /// <summary>The weight shift's dynamics (second order: Hz, damping): lower is heavier and lazier.</summary>
        public float SupportFrequency=2.2f,SupportDamping=.55f;
        /// <summary>Where in the swing the foot is highest (0..1), the toe's pitch at lift-off and the heel's at strike (rad).</summary>
        public float LiftPeak=.4f,ToeOff=.35f,HeelStrike=.2f;
        /// <summary>How much the body follows the feet's floor (0 the root's height only, 1 the feet's mean).</summary>
        public float FloorFollow=.6f;
        /// <summary>Turning in place drives the cadence like walking at the turn rate times this radius (m), and a stepping foot
        /// lands half a stance ahead of the turn (so a big body turning on the spot keeps both groups stepping instead of falling
        /// behind its settle steps). 0, the default, leaves turning to the settle steps as before.</summary>
        public float TurnRadius=0;
    }

    /// <summary>
    /// The procedural gait for any number of legs (a biped, a quadruped, a six-legged crawler), driven by the body's ground speed so
    /// a planted foot never moves:
    /// <list type="bullet">
    /// <item>the cycle's cadence is the ground speed over the stride (strides lengthen from walk to run; slow walking takes short
    /// steps at <see cref="BossGaitDef.MinFrequency"/>), and each leg steps at its own phase offset;</item>
    /// <item>a planted foot is locked where it landed (exactly: its world position is not touched until it lifts); it unlocks on
    /// its beat (past the duty), when the body has left it out of reach (a turn, a shove, a stagger), or, standing, when it is
    /// out of place (settling under the body one group at a time, so the others hold it up);</item>
    /// <item>a swinging foot flies, on a lifted arc, from where it lifted to where the body will be when it lands (the root
    /// carried on by its velocity, the facing by its turn rate, half a stance ahead), and plants;</item>
    /// <item>the body dips at double support, sways and banks toward the stance side and dips on each footfall, all through
    /// second-order dynamics (weight, lag, a little overshoot); a stride that over-reaches sinks the hips until every planted foot
    /// is in reach; each leg's knee comes from two-bone IK toward its pole.</item>
    /// </list>
    /// Fixed arrays: no allocation per frame. Its own clock is the dt it is stepped by (the presentation's).
    /// </summary>
    public sealed class BossGait
    {
        public readonly BossGaitDef Def;public readonly int Legs;
        // ---- per leg output ----
        public readonly Vector3[] Foot,Knee,Hip;public readonly Quaternion[] FootRotation,Upper,Lower;
        public readonly bool[] Planted;
        /// <summary>Legs the rig owns this step (an arm raised for an attack, a limb folded): set before <see cref="Step"/>. A free
        /// leg's foot rides its home under the body (not planted, never swinging), is left out of the hips' sink, and is planted
        /// there again the step it is given back. All false by default.</summary>
        public readonly bool[] Free;
        /// <summary>The swing's progress (0..1; -1 planted), and the leg's forwardness (-1 behind .. 1 ahead of its hip, along the
        /// facing: an arm swings against it).</summary>
        public readonly float[] Swing,Forward;
        // ---- body output ----
        /// <summary>The body origin (the root on the ground raised by <see cref="BossGaitDef.BodyHeight"/>, bobbed, swayed and
        /// sunk) and its rotation (the facing with the lean given, the support's bank and the stride's twist).</summary>
        public Vector3 BodyPosition;public Quaternion BodyRotation=Quaternion.Identity;
        public float Bob,SwayOffset,BankOffset,TwistOffset,Sink;
        /// <summary>The rig's own body move for this step, set before <see cref="Step"/> (an attack's crouch and bend, a stagger's
        /// lean): added to the body (world offset, local rotation) before the hips and knees are solved, so the feet stay planted.</summary>
        public Vector3 ExtraOffset;public Quaternion ExtraRotation=Quaternion.Identity;
        /// <summary>The cycle (cycles), its rate (Hz), this frame's stride (m), duty and run blend, the ground speed, moving.</summary>
        public float Phase,Frequency,Stride,Duty,Run,Speed;public bool Moving;
        /// <summary>The legs that planted this frame (bit i: leg i) and how hard (m/s down at the strike).</summary>
        public int Landed;public readonly float[] LandSpeed;
        private struct Leg{public Vector3 Foot,From,To;public bool Swing;public double T0,T1;public float Lift,Yaw,FromYaw,ToYaw,Pitch;public int Cycle,Group;}
        private readonly Leg[] legs;private double time;private bool ready;
        private BossSecondOrder sway,bank,dip;private BossSpring sink;private int groups;
        public BossGait(BossGaitDef def)
        {
            Def=def;Legs=def.Legs.Length;legs=new Leg[Legs];Foot=new Vector3[Legs];Knee=new Vector3[Legs];Hip=new Vector3[Legs];
            FootRotation=new Quaternion[Legs];Upper=new Quaternion[Legs];Lower=new Quaternion[Legs];Planted=new bool[Legs];Free=new bool[Legs];Swing=new float[Legs];Forward=new float[Legs];LandSpeed=new float[Legs];
            // Legs that share a phase offset step together (a group): a tripod, a trot's diagonal, one biped foot.
            var seen=new float[Legs];
            for(int i=0;i<Legs;i++)
            {
                int g=-1;for(int k=0;k<groups;k++)if(Math.Abs(seen[k]-BossMath.Frac(def.Legs[i].Phase))<.01f){g=k;break;}
                if(g<0){g=groups;seen[groups++]=BossMath.Frac(def.Legs[i].Phase);}legs[i].Group=g;
            }
            sway.Configure(def.SupportFrequency,def.SupportDamping,0);bank.Configure(def.SupportFrequency,def.SupportDamping,0);dip.Configure(def.SupportFrequency*1.4f,def.SupportDamping,0);
        }
        /// <summary>Plants every foot at home under `root` (the ground point under the body) facing `yaw`: a fresh boss, a teleport.</summary>
        public void Reset(Vector3 root,float yaw,IBossGround ground)
        {
            var r=BossMath.Yaw(yaw);
            for(int i=0;i<Legs;i++)
            {
                ref var l=ref legs[i];var d=Def.Legs[i];var h=root+BossMath.Rotate(r,new Vector3(d.Home.X,0,d.Home.Z));h.Y=ground.Height(h.X,h.Z)+d.Ankle;
                l.Foot=l.From=l.To=h;l.Swing=false;l.Yaw=l.FromYaw=l.ToYaw=yaw;l.Pitch=0;l.Cycle=int.MinValue;
            }
            sway.Reset(0);bank.Reset(0);dip.Reset(0);sink.Reset(0);Moving=false;Speed=Frequency=Stride=0;ready=true;
        }
        private bool CanLift(int i)
        {
            int swinging=0;for(int k=0;k<Legs;k++){if(!legs[k].Swing)continue;swinging++;if(legs[k].Group!=legs[i].Group)return false;}
            return swinging<Math.Max(1,Legs/2);
        }
        // How far the hips (as last solved, before their sink) would have to sink for leg i's planted foot to be in reach.
        private float Overreach(int i)
        {
            if(!ready)return 0;var ld=Def.Legs[i];var dl=Hip[i]-legs[i].Foot;dl.Y+=Sink;float reach=(ld.Upper+ld.Lower)*.97f,h2=dl.X*dl.X+dl.Z*dl.Z;
            return h2>=reach*reach?dl.Y:Math.Max(0,dl.Y-(float)Math.Sqrt(reach*reach-h2));
        }
        private static float Arc(float s,float peak)=>s<peak?(float)Math.Sin(.5*Math.PI*s/peak):(float)Math.Cos(.5*Math.PI*(s-peak)/(1-peak));
        /// <summary>
        /// Steps the gait by `dt`: `root` the presented root on the ground, `yaw` its facing and `yawRate` its turn (rad, rad/s),
        /// `velocity` its ground velocity (m/s; what the feet must keep up with), `body` the body's rotation from the follow (its
        /// facing with the lean: <see cref="BossRootFollow.Body"/>; the support's twist and bank go on top; default: the facing).
        /// </summary>
        public void Step(Vector3 root,float yaw,float yawRate,Vector3 velocity,Quaternion body,float dt,IBossGround ground)
        {
            if(!ready)Reset(root,yaw,ground);dt=BossMath.Clamp(dt,0,.1f);time+=dt;Landed=0;
            var d=Def;var flat=BossMath.Flat(velocity);Speed=flat.Length();float cadence=d.TurnRadius>0?Math.Max(Speed,Math.Abs(yawRate)*d.TurnRadius):Speed;
            bool was=Moving;if(Moving){if(cadence<d.StopSpeed)Moving=false;}else if(cadence>d.StartSpeed)Moving=true;
            Run=BossMath.Smooth((Speed-d.WalkSpeed)/Math.Max(.01f,d.RunSpeed-d.WalkSpeed));
            float stride0=BossMath.Lerp(d.WalkStride,d.RunStride,Run);Duty=BossMath.Lerp(d.WalkDuty,d.RunDuty,Run);
            Frequency=Moving?Math.Max(d.MinFrequency,cadence/stride0):0;Stride=Moving?Speed/Frequency:0;
            // Setting off, the cycle starts on the beat: the group whose feet trail furthest behind the way it moves lifts now
            // (the rest a part-cycle later), instead of wherever the phase stood, which could leave a foot planted far behind.
            if(Moving&&!was)
            {
                int first=0;float behind=float.MaxValue;var dir=Speed>1e-4f?flat/Speed:BossMath.Forward(yaw);
                for(int i=0;i<Legs;i++){if(legs[i].Swing)continue;float a=Vector3.Dot(legs[i].Foot-root,dir);if(a<behind){behind=a;first=i;}}
                Phase=(float)Math.Floor(Phase)+1+BossMath.Frac(Duty-Def.Legs[first].Phase)+1e-3f;
            }
            Phase+=Frequency*dt;
            var facing=BossMath.Yaw(yaw);var fwd=BossMath.Forward(yaw);
            float stance=Moving?Duty/Frequency:0,reachMoving=Stride*Duty*.5f;
            for(int i=0;i<Legs;i++)
            {
                ref var l=ref legs[i];var ld=Def.Legs[i];float cyc=Phase+ld.Phase,u=BossMath.Frac(cyc);int cycle=(int)Math.Floor(cyc);
                var home=root+BossMath.Rotate(facing,new Vector3(ld.Home.X,0,ld.Home.Z));home.Y=ground.Height(home.X,home.Z)+ld.Ankle;
                if(Free[i])
                {
                    l.Swing=false;l.Foot=l.From=l.To=home;l.Yaw=l.FromYaw=l.ToYaw=yaw;l.Pitch=0;l.Cycle=cycle;
                    Foot[i]=home;Planted[i]=false;Swing[i]=-1;FootRotation[i]=facing;Forward[i]=0;continue;
                }
                if(!l.Swing)
                {
                    bool lift=false;float swingTime=0,off=Vector3.Distance(BossMath.Flat(l.Foot),BossMath.Flat(home));
                    if(Moving&&u>=Duty&&l.Cycle!=cycle){lift=true;swingTime=(1-u)/Frequency;}
                    else if(!Moving&&off>d.SettleDistance&&CanLift(i)){lift=true;swingTime=d.SettleSwing;}
                    else if((Moving&&off>reachMoving+d.ReachMargin*(ld.Upper+ld.Lower)||Overreach(i)>d.MaxSink*(ld.Upper+ld.Lower))&&CanLift(i)){lift=true;swingTime=Moving?Math.Min((1-u)/Frequency,d.SettleSwing):d.SettleSwing;}
                    if(lift)
                    {
                        l.Swing=true;l.From=l.Foot;l.T0=time;l.T1=time+BossMath.Clamp(swingTime,d.MinSwing,d.MaxSwing);l.Cycle=cycle;l.FromYaw=l.Yaw;
                        l.Lift=Moving?BossMath.Lerp(d.WalkLift,d.RunLift,Run):d.WalkLift*.6f;
                    }
                }
                if(l.Swing)
                {
                    // Where the body will be when this foot lands: carried on by the velocity, turned on by the turn rate, and half a
                    // stance ahead (so the foot passes under the hip mid-stance and leaves as far behind as it landed ahead).
                    float left=(float)Math.Max(0,l.T1-time),landYaw=yaw+yawRate*(left+(Moving&&d.TurnRadius>0?stance*.5f:0));
                    var ahead=root+flat*left+BossMath.Rotate(BossMath.Yaw(landYaw),new Vector3(ld.Home.X,0,ld.Home.Z))+(Moving?flat*(stance*.5f):Vector3.Zero);
                    ahead.Y=ground.Height(ahead.X,ahead.Z)+ld.Ankle;l.To=ahead;l.ToYaw=landYaw;
                    float s=BossMath.Clamp01((float)((time-l.T0)/Math.Max(1e-4,l.T1-l.T0))),e=BossMath.Smoother(s);
                    var at=Vector3.Lerp(l.From,l.To,e);float lift=l.Lift*Arc(s,d.LiftPeak);at.Y=BossMath.Lerp(l.From.Y,l.To.Y,e)+lift;
                    float before=l.Foot.Y;l.Foot=at;l.Yaw=BossMath.Lerp(l.FromYaw,l.FromYaw+BossMath.WrapPi(l.ToYaw-l.FromYaw),e);
                    l.Pitch=s<d.LiftPeak?d.ToeOff*(float)Math.Sin(Math.PI*s/d.LiftPeak):-d.HeelStrike*(float)Math.Sin(Math.PI*(s-d.LiftPeak)/(1-d.LiftPeak));
                    if(s>=1)
                    {
                        l.Swing=false;l.Foot=l.To;l.Yaw=l.ToYaw;l.Pitch=0;Landed|=1<<i;
                        LandSpeed[i]=dt>0?Math.Max(0,(before-l.Foot.Y)/dt):0;if(!Moving)l.Cycle=cycle;
                    }
                }
                Foot[i]=l.Foot;Planted[i]=!l.Swing;Swing[i]=l.Swing?BossMath.Clamp01((float)((time-l.T0)/Math.Max(1e-4,l.T1-l.T0))):-1;
                FootRotation[i]=BossMath.Yaw(l.Yaw)*BossMath.AxisAngle(Vector3.UnitX,l.Pitch);
                Forward[i]=BossMath.Clamp(Vector3.Dot(l.Foot-home,fwd)/Math.Max(.2f,ld.Upper+ld.Lower)*2.2f,-1,1);
            }
            // ---- the body: the support's dip, sway and bank (second order), the footfall dips, the floor it stands over ----
            float lifted=0,side=0,twist=0,floor=0;
            for(int i=0;i<Legs;i++)
            {
                var ld=Def.Legs[i];floor+=legs[i].Foot.Y-ld.Ankle-(legs[i].Swing?legs[i].Lift*Arc(Swing[i],d.LiftPeak):0);
                if(legs[i].Swing){float a=(float)Math.Sin(Math.PI*Swing[i]);lifted+=a;side-=ld.Side*a;}
                twist-=ld.Side*Forward[i];
            }
            floor/=Math.Max(1,Legs);float half=Math.Max(1,Legs*.5f),move=BossMath.Smooth(Speed/Math.Max(.05f,d.WalkSpeed));
            float bobTarget=-BossMath.Lerp(d.Bob,d.RunBob,Run)*(1-BossMath.Clamp01(lifted/half))*move;
            for(int i=0;i<Legs;i++)if((Landed&(1<<i))!=0)dip.Kick(-d.LandDip*BossMath.Clamp(.35f+Speed/Math.Max(.1f,d.WalkSpeed),.35f,2.2f));
            Bob=bobTarget+dip.Step(0,dt);
            SwayOffset=sway.Step(d.Sway*side/half,dt);BankOffset=bank.Step(d.Roll*side/half,dt);TwistOffset=d.Twist*twist/half*move;
            float baseY=BossMath.Lerp(root.Y,floor,d.FloorFollow);
            var right=BossMath.Right(yaw);var origin=new Vector3(root.X,baseY+d.BodyHeight+Bob,root.Z)+right*SwayOffset;
            if(body.LengthSquared()<.5f)body=facing;
            var rot=Quaternion.Normalize(body*BossMath.AxisAngle(Vector3.UnitY,TwistOffset)*BossMath.AxisAngle(Vector3.UnitZ,-BankOffset));
            origin+=ExtraOffset;if(ExtraRotation.LengthSquared()>.5f)rot=Quaternion.Normalize(rot*ExtraRotation);
            // A stride that over-reaches sinks the hips until every planted foot is within reach (fast down, slower back up).
            float need=0;
            for(int i=0;i<Legs;i++)
            {
                if(legs[i].Swing||Free[i])continue;var ld=Def.Legs[i];var hip=origin+BossMath.Rotate(rot,ld.Hip);float reach=(ld.Upper+ld.Lower)*.97f;
                var dlt=hip-legs[i].Foot;float h2=dlt.X*dlt.X+dlt.Z*dlt.Z;if(h2>=reach*reach){need=Math.Max(need,dlt.Y);continue;}
                float v=(float)Math.Sqrt(reach*reach-h2);if(dlt.Y>v)need=Math.Max(need,dlt.Y-v);
            }
            float sunk=need>sink.Value?sink.Step(need,.03f,dt):sink.Step(need,.14f,dt);Sink=Math.Max(0,sunk);
            origin.Y-=Sink;BodyPosition=origin;BodyRotation=rot;
            for(int i=0;i<Legs;i++)
            {
                var ld=Def.Legs[i];Hip[i]=origin+BossMath.Rotate(rot,ld.Hip);var pole=BossMath.Rotate(rot,ld.Pole);
                Knee[i]=BossIk.TwoBone(Hip[i],Foot[i],ld.Upper,ld.Lower,pole,out var end);
                Upper[i]=BossMath.Bone(Hip[i],Knee[i],pole);Lower[i]=BossMath.Bone(Knee[i],end,pole);
            }
        }
    }

    /// <summary>Two-bone IK (a thigh and shin, an upper arm and forearm).</summary>
    public static class BossIk
    {
        /// <summary>
        /// The middle joint (knee, elbow) for a limb from `root` reaching `target` with bone lengths `upper`, `lower`, bending
        /// toward `pole`. `end` is where the limb's end really is (the target, pulled into reach if it was out of it). `soft`
        /// (a share of the full reach, 0 exact) eases the last of the reach: a straightening limb's middle joint otherwise moves
        /// infinitely fast as its target crosses full reach (the IK pop); softened it approaches straight smoothly, falling
        /// short of a target in that last share by up to soft x reach.
        /// </summary>
        public static Vector3 TwoBone(Vector3 root,Vector3 target,float upper,float lower,Vector3 pole,out Vector3 end,float soft=0)
        {
            var d=target-root;float l=d.Length(),max=(upper+lower)*.9999f,min=Math.Abs(upper-lower)*1.0001f+1e-4f;
            var dir=l>1e-6f?d/l:-Vector3.UnitY;float c=l;
            if(soft>0){float band=soft*(upper+lower),start=max-band;if(c>start)c=start+band*(1-(float)Math.Exp(-(c-start)/band));}
            c=BossMath.Clamp(c,min,max);end=root+dir*c;
            float a=(upper*upper-lower*lower+c*c)/(2*c),h=(float)Math.Sqrt(Math.Max(0,upper*upper-a*a));
            var p=pole-dir*Vector3.Dot(pole,dir);float pl=p.Length();
            if(pl<1e-6f){p=Vector3.Cross(dir,Math.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX);pl=p.Length();}
            return root+dir*a+p/pl*h;
        }
    }
}
