using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One authority sample of a boss's root: its authority time (s), position and yaw (rad, unwrapped by the buffer).</summary>
    public struct BossSnapshot{public double Time;public Vector3 Position;public float Yaw;public bool Jump;}
    /// <summary>Where a <see cref="BossSnapshotBuffer.Sample"/> fell: no data, before the oldest, between two snapshots, or past
    /// the newest (extrapolated, within the clamp).</summary>
    public enum BossSampleKind:byte{Empty,Before,Interpolated,Extrapolated}

    /// <summary>
    /// The snapshots a boss's root arrived in (a fixed ring, oldest first), sampled as a C1 curve: cubic Hermite through them with
    /// each tangent from its neighbours, so the velocity never jumps at a snapshot the way a straight lerp between two does.
    /// Past the newest it extrapolates on the newest velocity, held for the first 40% of `maxExtrapolate` (a late frame usually
    /// lands by then) and then eased off to rest by `maxExtrapolate`, instead of freezing and then jumping. A snapshot more than <see cref="TeleportDistance"/> from the last is a jump
    /// (a teleport or placement): the curve is cut there and <see cref="Sample"/> reports the jump's time.
    /// </summary>
    public sealed class BossSnapshotBuffer
    {
        private readonly BossSnapshot[] ring;private int start,count;
        public float TeleportDistance=5;
        public BossSnapshotBuffer(int capacity=32){ring=new BossSnapshot[Math.Max(4,capacity)];}
        public int Count=>count;
        public BossSnapshot this[int i]=>ring[(start+i)%ring.Length];
        public double Newest=>count>0?this[count-1].Time:double.NaN;
        public void Clear(){start=count=0;}
        /// <summary>Adds a snapshot; false (ignored) when it is not newer than the newest (a duplicate or a stale frame).</summary>
        public bool Add(double time,Vector3 position,float yaw)
        {
            bool jump=false;
            if(count>0)
            {
                var last=this[count-1];if(!(time>last.Time+1e-6))return false;
                yaw=last.Yaw+BossMath.WrapPi(yaw-last.Yaw);jump=Vector3.Distance(position,last.Position)>TeleportDistance;
            }
            if(count==ring.Length){start=(start+1)%ring.Length;count--;}
            ring[(start+count)%ring.Length]=new BossSnapshot{Time=time,Position=position,Yaw=yaw,Jump=jump};count++;return true;
        }
        // The tangent (per second) at snapshot i: central over its neighbours, one-sided at an end or beside a jump.
        private void Tangent(int i,out Vector3 m,out float my)
        {
            var s=this[i];bool hasPrev=i>0&&!s.Jump,hasNext=i<count-1&&!this[i+1].Jump;
            if(hasPrev&&hasNext){var a=this[i-1];var b=this[i+1];float h=(float)(b.Time-a.Time);m=(b.Position-a.Position)/h;my=(b.Yaw-a.Yaw)/h;}
            else if(hasNext){var b=this[i+1];float h=(float)(b.Time-s.Time);m=(b.Position-s.Position)/h;my=(b.Yaw-s.Yaw)/h;}
            else if(hasPrev){var a=this[i-1];float h=(float)(s.Time-a.Time);m=(s.Position-a.Position)/h;my=(s.Yaw-a.Yaw)/h;}
            else{m=Vector3.Zero;my=0;}
        }
        /// <summary>
        /// The root at authority time `t`: position, velocity, yaw (unwrapped) and yaw rate; `jumpAt` the time of the newest jump
        /// at or before `t` (NaN: none in the buffer).
        /// </summary>
        public BossSampleKind Sample(double t,float maxExtrapolate,out Vector3 position,out Vector3 velocity,out float yaw,out float yawRate,out double jumpAt)
        {
            position=velocity=Vector3.Zero;yaw=yawRate=0;jumpAt=double.NaN;if(count==0)return BossSampleKind.Empty;
            for(int i=count-1;i>=0;i--){var s=this[i];if(s.Jump&&s.Time<=t){jumpAt=s.Time;break;}}
            var first=this[0];if(t<=first.Time){position=first.Position;yaw=first.Yaw;return BossSampleKind.Before;}
            var last=this[count-1];
            if(t>=last.Time)
            {
                Tangent(count-1,out var m,out float my);float dt=(float)(t-last.Time),ex=Math.Max(1e-3f,maxExtrapolate),hold=.4f*ex,fade=ex-hold;
                // The velocity is held, then eased to rest (1 - smoothstep); the distance is its integral: C1 with the curve at the
                // newest, and it stops after hold + fade / 2 seconds' travel.
                float decay,reach;if(dt<=hold){decay=1;reach=dt;}else{float w=Math.Min(1,(dt-hold)/fade);decay=1-w*w*(3-2*w);reach=hold+fade*(w-w*w*w+.5f*w*w*w*w);}
                position=last.Position+m*reach;velocity=m*decay;yaw=last.Yaw+my*reach;yawRate=my*decay;return BossSampleKind.Extrapolated;
            }
            int k=count-2;while(k>0&&this[k].Time>t)k--;
            var p0=this[k];var p1=this[k+1];
            if(p1.Jump){position=p0.Position;yaw=p0.Yaw;return BossSampleKind.Interpolated;}
            Tangent(k,out var m0,out float y0);Tangent(k+1,out var m1,out float y1);
            float span=(float)(p1.Time-p0.Time),u=(float)((t-p0.Time)/span),u2=u*u,u3=u2*u;
            float h00=2*u3-3*u2+1,h10=u3-2*u2+u,h01=-2*u3+3*u2,h11=u3-u2;
            float d00=(6*u2-6*u)/span,d10=(3*u2-4*u+1),d01=(-6*u2+6*u)/span,d11=(3*u2-2*u);
            position=h00*p0.Position+h10*span*m0+h01*p1.Position+h11*span*m1;velocity=d00*p0.Position+d10*m0+d01*p1.Position+d11*m1;
            yaw=h00*p0.Yaw+h10*span*y0+h01*p1.Yaw+h11*span*y1;yawRate=d00*p0.Yaw+d10*y0+d01*p1.Yaw+d11*y1;
            return BossSampleKind.Interpolated;
        }
    }

    /// <summary>
    /// The render clock on the authority's timeline, a fixed `Delay` behind the newest data: it estimates the authority clock from
    /// the fastest recent arrivals (a max filter on authority minus local time, decaying slowly so a longer route is learned), then
    /// runs a little faster or slower (at most <see cref="MaxSkew"/>) to hold the delay instead of freezing when a frame is late
    /// and jumping when it lands. When the render time does pass the newest data (a frame later than the cushion), the cushion
    /// grows by that much (up to <see cref="MaxExtraDelay"/>) and shrinks back slowly (<see cref="ExtraDecay"/> per second). The
    /// rate itself changes gradually (at most <see cref="SkewRate"/> per second), so the root's speed never steps when the
    /// estimate does. It never runs backwards; an error past <see cref="SnapError"/> (a stall, a
    /// reconnect) snaps.
    /// </summary>
    public sealed class BossRenderClock
    {
        public float Delay=.1f,MaxSkew=.08f,CatchUp=1.5f,SkewRate=.3f,SnapError=.5f,OffsetDecay=.05f,MaxExtraDelay=.12f,ExtraDecay=.01f;
        public double Time{get;private set;}
        private double offset,offsetAt,skew,latest,extra;private bool has,started;
        /// <summary>The cushion this clock holds now (Delay plus what late frames have added).</summary>
        public double Cushion=>Delay+extra;
        /// <summary>The clock's rate over real time (1 holds the delay).</summary>
        public double Rate=>1+skew;
        public bool Started=>started;
        public void Reset(){has=started=false;Time=0;skew=0;extra=0;latest=double.NegativeInfinity;}
        /// <summary>A snapshot stamped `authorityTime` arrived at local time `localTime`.</summary>
        public void Observe(double authorityTime,double localTime)
        {
            double sample=authorityTime-localTime;if(!has||authorityTime>latest)latest=authorityTime;
            if(!has){offset=sample;has=true;}else offset=Math.Max(sample,offset-OffsetDecay*Math.Max(0,localTime-offsetAt));
            offsetAt=localTime;
        }
        /// <summary>The render time for local time `localTime`, `dt` after the last call.</summary>
        public double Advance(double localTime,float dt)
        {
            if(!has)return Time;dt=Math.Max(0,dt);
            if(started){double late=Time-latest;if(late>0)extra=Math.Min(MaxExtraDelay,Math.Max(extra,late+.02));else extra=Math.Max(0,extra-ExtraDecay*dt);}
            double target=localTime+offset-Delay-extra;
            if(!started||Math.Abs(target-Time)>SnapError){Time=target;skew=0;started=true;return Time;}
double want=Math.Max(-MaxSkew,Math.Min(MaxSkew,(target-Time)*CatchUp)),step=SkewRate*dt;
            skew+=Math.Max(-step,Math.Min(step,want-skew));Time+=dt*(1+skew);return Time;
        }
    }

    /// <summary>
    /// A boss's presented root on any peer: the snapshots on the render clock (<see cref="Observe"/>, <see cref="Step"/>), or an
    /// externally clocked render time (<see cref="StepAt"/>: the arena's replica clock, or the host's visual clock with its fixed
    /// steps fed as snapshots), then:
    /// <list type="bullet">
    /// <item>position: two critically damped springs in a row, each tracking its goal's velocity too (no steady lag; the residual
    /// jitter, the curve's kinks and any correction are smoothed twice and decay over about <see cref="PositionHalfLife"/>);</item>
    /// <item>yaw: the shortest arc on the limited turn (<see cref="BossTurn"/>: <see cref="MaxTurnRate"/>, <see cref="MaxTurnAccel"/>);</item>
    /// <item>lean: the bank into a turn (centripetal speed x turn rate) and the pitch into speeding up and braking, through
    /// second-order dynamics, so the body sways with weight and settles.</item>
    /// </list>
    /// A jump (teleport) in the snapshots snaps everything and sets <see cref="Jumped"/> for that frame (the gait plants afresh).
    /// </summary>
    public sealed class BossRootFollow
    {
        public readonly BossSnapshotBuffer Snapshots=new BossSnapshotBuffer(32);
        public readonly BossRenderClock Clock=new BossRenderClock();
        public float PositionHalfLife=.14f,MaxExtrapolate=.3f;
        public float TurnHalfLife=.12f,MaxTurnRate=2.6f,MaxTurnAccel=10f;
        /// <summary>Bank per radian of the turn's lean angle (atan(v w / g)), its cap; pitch per radian of the speed change's lean,
        /// plus a lean per m/s of ground speed, and their cap; the lean's dynamics (Hz, damping).</summary>
        public float BankScale=1,MaxBank=.2f,PitchScale=.8f,SpeedPitch=.012f,MaxPitch=.16f,LeanFrequency=1.4f,LeanDamping=.6f;
        // ---- output ----
        public Vector3 Position,Velocity,Target,TargetVelocity;public float Yaw,YawRate,Bank,Pitch;public double RenderTime;
        public BossSampleKind Kind;public bool Jumped,Started;
        public float Speed=>BossMath.FlatLength(Velocity);
        /// <summary>The presented facing (yaw only) and the full body rotation with the lean.</summary>
        public Quaternion Facing=>BossMath.Yaw(Yaw);
        public Quaternion Body=>BossMath.Yaw(Yaw)*BossMath.AxisAngle(Vector3.UnitX,Pitch)*BossMath.AxisAngle(Vector3.UnitZ,-Bank);
        private BossSpring3 spring,smooth;private BossTurn turn;private BossSecondOrder bank,pitch;private double lastJump=double.NaN;private Vector3 lastVelocity;private bool configured;
        public void Reset(){Snapshots.Clear();Clock.Reset();Started=false;lastJump=double.NaN;Kind=BossSampleKind.Empty;}
        /// <summary>A snapshot arrived (`authorityTime` the frame's time, `localTime` the local clock at arrival).</summary>
        public void Observe(double authorityTime,double localTime,Vector3 position,float yaw)
        {if(Snapshots.Add(authorityTime,position,yaw))Clock.Observe(authorityTime,localTime);}
        /// <summary>Advances the render clock to `localTime` and follows.</summary>
        public void Step(double localTime,float dt){if(Snapshots.Count==0)return;StepAt(Clock.Advance(localTime,dt),dt);}
        /// <summary>Follows the snapshots at render time `renderTime` (on the authority's timeline).</summary>
        public void StepAt(double renderTime,float dt)
        {
            RenderTime=renderTime;Kind=Snapshots.Sample(renderTime,MaxExtrapolate,out var p,out var v,out float y,out float yr,out double jumpAt);
            // A jump counts once, when the render time first passes it (not again when its snapshot leaves the ring).
            if(Kind==BossSampleKind.Empty)return;bool jump=!double.IsNaN(jumpAt)&&jumpAt!=lastJump;if(jump)lastJump=jumpAt;
            Follow(p,v,y,dt,jump);
        }
        /// <summary>Follows a target given directly (already continuous: an offline drive, the gallery, the host's interpolated
        /// root): `velocity` its velocity, `snap` to jump to it.</summary>
        public void Follow(Vector3 target,Vector3 velocity,float yaw,float dt,bool snap=false)
        {
            if(!configured){bank.Configure(LeanFrequency,LeanDamping,0);pitch.Configure(LeanFrequency,LeanDamping,0);configured=true;}
            Target=target;TargetVelocity=velocity;Jumped=false;
            if(!Started||snap)
            {
                spring.Reset(target);spring.Velocity=velocity;smooth.Reset(target);smooth.Velocity=velocity;turn.Reset(Started?turn.Yaw+BossMath.WrapPi(yaw-turn.Yaw):yaw);bank.Reset(0);pitch.Reset(0);
                Position=target;Velocity=velocity;lastVelocity=velocity;Yaw=turn.Yaw;YawRate=0;Bank=Pitch=0;Jumped=Started;Started=true;return;
            }
            if(!(dt>0))return;
            // Two goal-velocity springs in a row: no steady lag (each tracks its goal's velocity), but the velocity reaching the
            // body is smoothed twice, so the noise and the kinks of the snapshot curve never reach it as acceleration spikes.
            // (A goal-velocity spring takes its goal as of the step's start: the frame's target, carried back by its velocity.)
            float h=PositionHalfLife*.5f;spring.Step(target-velocity*dt,velocity,h,dt);smooth.Step(spring.Value-spring.Velocity*dt,spring.Velocity,h,dt);turn.Step(yaw,TurnHalfLife,MaxTurnRate,MaxTurnAccel,dt);
            Position=smooth.Value;Velocity=smooth.Velocity;Yaw=turn.Yaw;YawRate=turn.Rate;
            // The lean: a turn's centripetal pull (v w) banks it in; speeding up pitches it forward, braking back.
            var accel=(Velocity-lastVelocity)/dt;lastVelocity=Velocity;var fwd=BossMath.Forward(Yaw);float speed=BossMath.FlatLength(Velocity);
            float along=Vector3.Dot(BossMath.Flat(accel),fwd),side=speed*YawRate;
            float wantBank=BossMath.Clamp((float)Math.Atan(side/BossMath.Gravity)*BankScale,-MaxBank,MaxBank);
            float wantPitch=BossMath.Clamp((float)Math.Atan(BossMath.Clamp(along,-8,8)/BossMath.Gravity)*PitchScale+speed*SpeedPitch,-MaxPitch,MaxPitch);
            Bank=bank.Step(wantBank,dt);Pitch=pitch.Step(wantPitch,dt);
        }
    }
}
