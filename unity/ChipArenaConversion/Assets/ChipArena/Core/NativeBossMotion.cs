using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The shared motion foundation for the Expedition bosses (owner 2026-10-17: "begin the bosses for Central Station and City
    /// Ironworks; I want these animations to be a lot more fluid than what we have now"; docs/BOSS-MOTION.md). Presentation only:
    /// nothing here is read by the authority, and no rule, hitbox, damage, timing, network or authority state changes.
    /// <list type="bullet">
    /// <item>NativeBossMotion.cs (this file): the math the rest shares (<see cref="BossMath"/>), the pose a boss writes
    /// (<see cref="BossPose"/>) and the pop killer (<see cref="BossInertializer"/>).</item>
    /// <item>NativeBossMotionSpring.cs: critically damped springs on a half-life (<see cref="BossSpring"/>, <see cref="BossSpring3"/>,
    /// <see cref="BossSpringQ"/>), the rate- and acceleration-limited turn (<see cref="BossTurn"/>) and second-order dynamics
    /// (<see cref="BossSecondOrder"/>, <see cref="BossSecondOrder3"/>) for overshoot and settle.</item>
    /// <item>NativeBossMotionFollow.cs: the replicated root (<see cref="BossSnapshotBuffer"/>, <see cref="BossRenderClock"/>,
    /// <see cref="BossRootFollow"/>): Hermite through the snapshots on a delayed, rate-adjusted clock, a soft extrapolation clamp,
    /// a goal-velocity spring, the shortest-arc limited turn and the lean into turns and speed changes.</item>
    /// <item>NativeBossMotionGait.cs: the procedural gait for N legs (<see cref="BossGait"/>): planted feet that lock, unlock on the
    /// stride's own beat (or when over-reached), swing on an arc to where the body will be, two-bone IK (<see cref="BossIk"/>), the
    /// body's bob, sway and roll from support, the ground from a callback (<see cref="IBossGround"/>).</item>
    /// <item>NativeBossMotionChain.cs: spring/verlet chains (<see cref="BossChain"/>) for spines, tails, cables and hanging parts.</item>
    /// <item>NativeBossMotionAction.cs: the attack phrase (<see cref="BossActionDef"/>, <see cref="BossActionPlayer"/>): anticipation,
    /// strike, contact, follow-through and recover on eased curves, a pure function of the authority's start time.</item>
    /// <item>NativeBossMotionLayers.cs: the additive layers: breath and idle noise, look-at and aim, hit flinch, stagger.</item>
    /// </list>
    /// Unity axes throughout (+X right, +Y up, +Z forward; a yaw turns +Z toward +X, as Quaternion.Euler(0, yaw, 0)); angles in
    /// radians unless a name says degrees; metres and seconds. Every type keeps fixed fields and arrays: no allocation per frame.
    /// </summary>
    public static class BossMath
    {
        public const float Pi=(float)Math.PI,Tau=2*(float)Math.PI,Deg=(float)Math.PI/180,Gravity=9.81f;
        public static float Clamp(float x,float a,float b)=>x<a?a:x>b?b:x;
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>Smoothstep (C1: zero slope at both ends).</summary>
        public static float Smooth(float t){t=Clamp01(t);return t*t*(3-2*t);}
        /// <summary>Smootherstep (C2: zero slope and curvature at both ends).</summary>
        public static float Smoother(float t){t=Clamp01(t);return t*t*t*(t*(6*t-15)+10);}
        public static float Frac(float x)=>x-(float)Math.Floor(x);
        /// <summary>An angle wrapped to (-pi, pi].</summary>
        public static float WrapPi(float a){a%=Tau;if(a>Pi)a-=Tau;else if(a<=-Pi)a+=Tau;return a;}
        public static float WrapDeg(float d){d%=360;if(d>180)d-=360;else if(d<=-180)d+=360;return d;}
        /// <summary>The horizontal facing of a yaw (sin, 0, cos) and its right (cos, 0, -sin).</summary>
        public static Vector3 Forward(float yaw)=>new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
        public static Vector3 Right(float yaw)=>new Vector3((float)Math.Cos(yaw),0,-(float)Math.Sin(yaw));
        /// <summary>The yaw (rad) a horizontal direction faces.</summary>
        public static float YawOf(Vector3 d)=>(float)Math.Atan2(d.X,d.Z);
        public static Vector3 Flat(Vector3 v)=>new Vector3(v.X,0,v.Z);
        public static float FlatLength(Vector3 v)=>(float)Math.Sqrt(v.X*v.X+v.Z*v.Z);
        public static Quaternion Yaw(float yaw)=>AxisAngle(Vector3.UnitY,yaw);
        public static Quaternion AxisAngle(Vector3 axis,float angle){float h=angle*.5f,s=(float)Math.Sin(h);return new Quaternion(axis.X*s,axis.Y*s,axis.Z*s,(float)Math.Cos(h));}
        /// <summary>Unity's Quaternion.Euler order (z, then x, then y) in radians.</summary>
        public static Quaternion Euler(float x,float y,float z)=>AxisAngle(Vector3.UnitY,y)*AxisAngle(Vector3.UnitX,x)*AxisAngle(Vector3.UnitZ,z);
        /// <summary>Rotates v by q.</summary>
        public static Vector3 Rotate(Quaternion q,Vector3 v)=>Vector3.Transform(v,q);
        public static Quaternion Inverse(Quaternion q)=>Quaternion.Conjugate(q);
        /// <summary>The same rotation with w >= 0 (the short way round for logs and blends).</summary>
        public static Quaternion Abs(Quaternion q)=>q.W<0?new Quaternion(-q.X,-q.Y,-q.Z,-q.W):q;
        /// <summary>The rotation's scaled axis (axis times angle, rad).</summary>
        public static Vector3 Log(Quaternion q)
        {
            q=Abs(q);var v=new Vector3(q.X,q.Y,q.Z);float s=v.Length();if(s<1e-7f)return v*2;
            float angle=2*(float)Math.Atan2(s,q.W);return v*(angle/s);
        }
        /// <summary>The rotation of a scaled axis (axis times angle, rad).</summary>
        public static Quaternion Exp(Vector3 v)
        {
            float angle=v.Length();if(angle<1e-7f){var q=new Quaternion(v.X*.5f,v.Y*.5f,v.Z*.5f,1);return Quaternion.Normalize(q);}
            return AxisAngle(v/angle,angle);
        }
        /// <summary>Spherical blend the short way round (normalized lerp for close rotations).</summary>
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {
            if(Quaternion.Dot(a,b)<0)b=new Quaternion(-b.X,-b.Y,-b.Z,-b.W);
            return Quaternion.Normalize(Quaternion.Slerp(a,b,t));
        }
        /// <summary>The rotation turning +Z to `forward` with +Y as near `up` as it can (Unity's Quaternion.LookRotation).</summary>
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)
        {
            float fl=forward.Length();if(fl<1e-7f)return Quaternion.Identity;var f=forward/fl;
            var r=Vector3.Cross(up,f);float rl=r.Length();
            if(rl<1e-6f){r=Vector3.Cross(Math.Abs(f.Y)<.9f?Vector3.UnitY:Vector3.UnitX,f);rl=r.Length();}
            r/=rl;var u=Vector3.Cross(f,r);return FromBasis(r,u,f);
        }
        /// <summary>The rotation whose images of +X, +Y, +Z are r, u, f (orthonormal).</summary>
        public static Quaternion FromBasis(Vector3 r,Vector3 u,Vector3 f)
        {
            // m[row][col] with columns r, u, f
            float m00=r.X,m01=u.X,m02=f.X,m10=r.Y,m11=u.Y,m12=f.Y,m20=r.Z,m21=u.Z,m22=f.Z,tr=m00+m11+m22;Quaternion q;
            if(tr>0){float s=(float)Math.Sqrt(tr+1)*2;q=new Quaternion((m21-m12)/s,(m02-m20)/s,(m10-m01)/s,.25f*s);}
            else if(m00>m11&&m00>m22){float s=(float)Math.Sqrt(1+m00-m11-m22)*2;q=new Quaternion(.25f*s,(m01+m10)/s,(m02+m20)/s,(m21-m12)/s);}
            else if(m11>m22){float s=(float)Math.Sqrt(1+m11-m00-m22)*2;q=new Quaternion((m01+m10)/s,.25f*s,(m12+m21)/s,(m02-m20)/s);}
            else{float s=(float)Math.Sqrt(1+m22-m00-m11)*2;q=new Quaternion((m02+m20)/s,(m12+m21)/s,.25f*s,(m10-m01)/s);}
            return Quaternion.Normalize(q);
        }
        /// <summary>The shortest rotation turning direction a to direction b (any lengths; identity for a zero one).</summary>
        public static Quaternion FromTo(Vector3 a,Vector3 b)
        {
            float la=a.Length(),lb=b.Length();if(la<1e-7f||lb<1e-7f)return Quaternion.Identity;a/=la;b/=lb;float d=Vector3.Dot(a,b);
            if(d<-.999999f){var axis=Vector3.Cross(Math.Abs(a.X)<.9f?Vector3.UnitX:Vector3.UnitY,a);return AxisAngle(Vector3.Normalize(axis),Pi);}
            var c=Vector3.Cross(a,b);return Quaternion.Normalize(new Quaternion(c.X,c.Y,c.Z,1+d));
        }
        /// <summary>A limb's rotation: local -Y along the bone (from its joint toward its child), local +Z toward `pole`.
        /// Identity is a limb hanging straight down with its knee (or elbow) facing forward.</summary>
        public static Quaternion Bone(Vector3 from,Vector3 to,Vector3 pole)
        {
            var down=to-from;float l=down.Length();if(l<1e-7f)return Quaternion.Identity;var y=-down/l;
            var z=pole-y*Vector3.Dot(pole,y);float zl=z.Length();
            if(zl<1e-6f){z=Vector3.Cross(y,Vector3.UnitX);zl=z.Length();if(zl<1e-6f){z=Vector3.Cross(y,Vector3.UnitZ);zl=z.Length();}}
            z/=zl;var x=Vector3.Cross(y,z);return FromBasis(x,y,z);
        }
        /// <summary>A deterministic hash of (seed, i) in [0, 1).</summary>
        public static float Hash(uint seed,int i)
        {
            uint h=seed*0x9E3779B1u^(uint)i*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;
            return (h&0xFFFFFF)/16777216f;
        }
        /// <summary>Smooth deterministic noise in [-1, 1] (quintic-blended value noise: continuous slope and curvature).</summary>
        public static float Noise(uint seed,float t)
        {
            float f=(float)Math.Floor(t);int i=(int)f;float u=t-f,a=Hash(seed,i)*2-1,b=Hash(seed,i+1)*2-1;
            return a+(b-a)*(u*u*u*(u*(u*6-15)+10));
        }
    }

    /// <summary>
    /// One boss's pose for a frame: every joint's world position and rotation by index (the runtime sets its parts from these,
    /// parents before children: Runtime/NativeBossPoseApply.cs), and the presented root. Fixed arrays, reused every frame.
    /// </summary>
    public sealed class BossPose
    {
        public readonly int Count;public readonly Vector3[] Position;public readonly Quaternion[] Rotation;
        public Vector3 RootPosition;public Quaternion RootRotation=Quaternion.Identity;
        public BossPose(int count){Count=count;Position=new Vector3[count];Rotation=new Quaternion[count];for(int i=0;i<count;i++)Rotation[i]=Quaternion.Identity;}
        public void Set(int i,Vector3 position,Quaternion rotation){Position[i]=position;Rotation[i]=rotation;}
        public void CopyFrom(BossPose o)
        {
            int n=Math.Min(Count,o.Count);Array.Copy(o.Position,Position,n);Array.Copy(o.Rotation,Rotation,n);RootPosition=o.RootPosition;RootRotation=o.RootRotation;
        }
        /// <summary>into = a blended toward b by w (positions lerped, rotations slerped the short way); into may be a or b.</summary>
        public static void Blend(BossPose a,BossPose b,float w,BossPose into)
        {
            w=BossMath.Clamp01(w);int n=Math.Min(into.Count,Math.Min(a.Count,b.Count));
            for(int i=0;i<n;i++){into.Position[i]=Vector3.Lerp(a.Position[i],b.Position[i],w);into.Rotation[i]=BossMath.Slerp(a.Rotation[i],b.Rotation[i],w);}
            into.RootPosition=Vector3.Lerp(a.RootPosition,b.RootPosition,w);into.RootRotation=BossMath.Slerp(a.RootRotation,b.RootRotation,w);
        }
        /// <summary>The largest joint move (m) and turn (rad) from `o` to this pose: the per-frame continuity the checks bound.</summary>
        public void Delta(BossPose o,out float move,out float turn)
        {
            move=turn=0;int n=Math.Min(Count,o.Count);
            for(int i=0;i<n;i++)
            {
                move=Math.Max(move,Vector3.Distance(Position[i],o.Position[i]));
                float d=Math.Abs(Quaternion.Dot(Rotation[i],o.Rotation[i]));turn=Math.Max(turn,2*(float)Math.Acos(Math.Min(1,d)));
            }
        }
    }

    /// <summary>
    /// The pop killer (inertialization): when the source pose jumps (a reset, a teleport, an action the peer joined late, a model
    /// swap), <see cref="Transition"/> records how far the shown pose is from the new source, and <see cref="Apply"/> decays that
    /// offset to nothing on critically damped springs. The output is the source plus the decaying offset: continuous at the jump,
    /// exactly the source once settled, and no lag in between (unlike low-passing the whole pose).
    /// </summary>
    public sealed class BossInertializer
    {
        private readonly BossSpring3[] position;private readonly BossSpringQ[] rotation;private BossSpring3 root;private BossSpringQ rootTurn;private bool live;
        public BossInertializer(int count){position=new BossSpring3[count];rotation=new BossSpringQ[count];Clear();}
        public bool Active=>live;
        public void Clear(){for(int i=0;i<position.Length;i++){position[i].Reset(Vector3.Zero);rotation[i].Reset(Quaternion.Identity);}root.Reset(Vector3.Zero);rootTurn.Reset(Quaternion.Identity);live=false;}
        /// <summary>The source is about to jump from what was shown (`shown`) to `next`: keep the difference, to decay.</summary>
        public void Transition(BossPose shown,BossPose next)
        {
            int n=Math.Min(position.Length,Math.Min(shown.Count,next.Count));
            for(int i=0;i<n;i++)
            {
                position[i].Reset(shown.Position[i]-next.Position[i]);
                rotation[i].Reset(BossMath.Abs(shown.Rotation[i]*BossMath.Inverse(next.Rotation[i])));
            }
            root.Reset(shown.RootPosition-next.RootPosition);rootTurn.Reset(BossMath.Abs(shown.RootRotation*BossMath.Inverse(next.RootRotation)));live=true;
        }
        /// <summary>output = source plus the offsets, decayed by `halfLife` over `dt` (output may be source).</summary>
        public void Apply(BossPose source,float halfLife,float dt,BossPose output)
        {
            if(!live){if(!ReferenceEquals(source,output))output.CopyFrom(source);return;}
            int n=Math.Min(position.Length,Math.Min(source.Count,output.Count));float left=0;
            for(int i=0;i<n;i++)
            {
                var p=position[i].Step(Vector3.Zero,halfLife,dt);var q=rotation[i].Step(Quaternion.Identity,halfLife,dt);
                output.Position[i]=source.Position[i]+p;output.Rotation[i]=Quaternion.Normalize(q*source.Rotation[i]);
                left=Math.Max(left,Math.Max(p.Length(),BossMath.Log(q).Length()));
            }
            var rp=root.Step(Vector3.Zero,halfLife,dt);var rq=rootTurn.Step(Quaternion.Identity,halfLife,dt);
            output.RootPosition=source.RootPosition+rp;output.RootRotation=Quaternion.Normalize(rq*source.RootRotation);
            if(left<1e-5f&&rp.Length()<1e-5f)live=false;
        }
    }
}
