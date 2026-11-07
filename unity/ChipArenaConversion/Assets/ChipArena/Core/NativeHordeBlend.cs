using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's pop killer (inertialization; docs/ZOMBIE-MOTION.md): when an add's source pose switches (a vent climb or a
    /// jump-down handing over to the gait, the door's waking take giving way to the walk, a dive, a kind swap, a gait reset, the
    /// living pose handing over to the death's first frame), <see cref="Mark"/> keeps the difference between what was shown last
    /// frame and the new source, rig root and facing included, and <see cref="Apply"/> decays it to nothing on critically damped
    /// springs. The output is the source plus the decaying offset: continuous at the switch, exactly the source once settled and
    /// never a lag in between (unlike low-passing the pose). A switch nobody marked (a joint turning faster than
    /// <see cref="PopRate"/>) is caught the same way, except where `guard` holds it off (a strike's contact frame is never
    /// softened). CapturedRig's 22 local joints and the body offset; presentation only; fixed arrays, no allocation.
    /// </summary>
    public sealed class HordePoseBlend
    {
        public const int Joints=22;
        /// <summary>The offset's half-life (s), and the per-frame turn (rad, at least; or rad/s times the frame) that reads as a pop.</summary>
        public float HalfLife=.11f,PopTurn=.42f,PopRate=26f,PopMove=.12f;
        private readonly Vector3[] lastP=new Vector3[Joints],offP=new Vector3[Joints],velP=new Vector3[Joints];
        private readonly Quaternion[] lastR=new Quaternion[Joints],srcR=new Quaternion[Joints];private readonly Vector3[] offR=new Vector3[Joints],velR=new Vector3[Joints],srcP=new Vector3[Joints];
        private Vector3 lastRoot,offRoot,velRoot;private float lastYaw,offYaw,velYaw;private bool has,pending,live;
        /// <summary>The transitions caught (marked or detected) since the add appeared, for the checks.</summary>
        public int Transitions{get;private set;}
        /// <summary>The unmarked pops caught (joints that jumped), for the checks.</summary>
        public int Pops{get;private set;}
        public bool Active=>live;
        public bool HasLast=>has;
        public void Reset(){has=pending=live=false;Transitions=0;Pops=0;}
        /// <summary>The next <see cref="Apply"/> blends from what was shown last into its source.</summary>
        public void Mark(){pending=true;}
        /// <summary>
        /// Writes this frame's pose into `p` (local joints), the rig root `root` (Unity world) and the facing `yaw` (degrees) as the
        /// source plus the decaying offset, and remembers what was shown. `dt` the frame's seconds; `guard` holds off pop detection.
        /// </summary>
        public void Apply(CapturedRig.Pose p,ref Vector3 root,ref float yaw,float dt,bool guard=false,float halfLife=-1)
        {
            if(halfLife<=0)halfLife=HalfLife;
            if(pending&&has)
            {
                for(int i=0;i<Joints;i++){offP[i]=lastP[i]-p.Position[i];velP[i]=Vector3.Zero;offR[i]=BossMath.Log(lastR[i]*Quaternion.Conjugate(p.Rotation[i]));velR[i]=Vector3.Zero;}
                offRoot=lastRoot-root;velRoot=Vector3.Zero;offYaw=BossMath.WrapDeg(lastYaw-yaw);velYaw=0;live=true;Transitions++;
            }
            else if(has&&!guard&&dt>0)
            {
                // A pop nobody marked (the source's own joint jumped since last frame): only the joints that jumped take a fresh
                // offset (the rest keep decaying theirs, and the root and facing, continuous by construction, are never held back),
                // so a joint that flips back and forth just holds.
                float turnLimit=Math.Max(PopTurn,PopRate*dt),moveLimit=Math.Max(PopMove,4*dt);bool any=false;
                // (The body offset and the legs are left alone: their planted feet are exact, and an offset would slide them.)
                for(int i=1;i<CapturedRig.LHip;i++)
                {
                    float d=Math.Abs(Quaternion.Dot(srcR[i],p.Rotation[i]));
                    if(2*(float)Math.Acos(Math.Min(1,d))>turnLimit){offR[i]=BossMath.Log(lastR[i]*Quaternion.Conjugate(p.Rotation[i]));velR[i]=Vector3.Zero;any=true;}
                    if(Vector3.Distance(srcP[i],p.Position[i])>moveLimit){offP[i]=lastP[i]-p.Position[i];velP[i]=Vector3.Zero;any=true;}
                }
                if(any){live=true;Pops++;}
            }
            pending=false;
            for(int i=0;i<Joints;i++){srcR[i]=p.Rotation[i];srcP[i]=p.Position[i];}
            if(live&&dt>0)
            {
                float y=BossSpring.Rate(halfLife),e=(float)Math.Exp(-y*dt),left=0;
                for(int i=0;i<Joints;i++)
                {
                    Decay(ref offP[i],ref velP[i],y,e,dt);Decay(ref offR[i],ref velR[i],y,e,dt);
                    left=Math.Max(left,Math.Max(offP[i].Length(),offR[i].Length()));
                }
                Decay(ref offRoot,ref velRoot,y,e,dt);{float j1=velYaw+offYaw*y;offYaw=e*(offYaw+j1*dt);velYaw=e*(velYaw-j1*y*dt);}
                left=Math.Max(left,Math.Max(offRoot.Length(),Math.Abs(offYaw)*BossMath.Deg));
                if(left<1e-4f)live=false;
            }
            if(live)
            {
                for(int i=0;i<Joints;i++){p.Position[i]+=offP[i];if(offR[i].LengthSquared()>1e-12f)p.Rotation[i]=Quaternion.Normalize(BossMath.Exp(offR[i])*p.Rotation[i]);}
                root+=offRoot;yaw=BossMath.WrapDeg(yaw+offYaw);
            }
            for(int i=0;i<Joints;i++){lastP[i]=p.Position[i];lastR[i]=p.Rotation[i];}lastRoot=root;lastYaw=yaw;has=true;
        }
        // A critically damped spring toward zero (the BossSpring closed form): the offset and its velocity over dt.
        private static void Decay(ref Vector3 x,ref Vector3 v,float y,float e,float dt){var j1=v+x*y;x=e*(x+j1*dt);v=e*(v-j1*y*dt);}
        /// <summary>Copies what another blend last showed (the death takes over a living add's last pose).</summary>
        public void CopyLast(HordePoseBlend o)
        {
            Array.Copy(o.lastP,lastP,Joints);Array.Copy(o.lastR,lastR,Joints);Array.Copy(o.srcP,srcP,Joints);Array.Copy(o.srcR,srcR,Joints);lastRoot=o.lastRoot;lastYaw=o.lastYaw;has=o.has;
            Array.Copy(o.offP,offP,Joints);Array.Copy(o.velP,velP,Joints);Array.Copy(o.offR,offR,Joints);Array.Copy(o.velR,velR,Joints);offRoot=o.offRoot;velRoot=o.velRoot;offYaw=o.offYaw;velYaw=o.velYaw;live=o.live;pending=false;
        }
    }
}
