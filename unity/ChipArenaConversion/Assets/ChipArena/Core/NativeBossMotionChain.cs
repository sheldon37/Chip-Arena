using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A chain's tuning (a spine or tail that springs back to shape, a cable or chain that hangs and drags). Rates are
    /// per second; lengths metres.</summary>
    public sealed class BossChainDef
    {
        /// <summary>Points including the anchor (segments = Count - 1), and each segment's length.</summary>
        public int Count=8;public float Length=.3f;
        /// <summary>The rest shape's direction in the anchor's frame (each segment continues it), and the segments' bend pole there.</summary>
        public Vector3 Rest=new Vector3(0,-1,0),Pole=Vector3.UnitZ;
        /// <summary>Pull toward the rest shape at the root and at the tip (1/s; 0 a free rope), velocity loss (1/s), gravity (m/s^2).</summary>
        public float Stiffness=6,TipStiffness=2,Damping=1.2f,Gravity=9.81f;
        /// <summary>Segment length limits as ratios (1, 1: inextensible; a chain's links bunch below 1).</summary>
        public float MinStretch=1,MaxStretch=1;
        /// <summary>Collision with the floor: the points' radius, and the sliding speed lost per second on it (1/s).</summary>
        public float Radius=.06f,Friction=6;
        /// <summary>Sub-step (s) and constraint passes per sub-step.</summary>
        public float MaxStep=1/120f;public int Iterations=3;
    }

    /// <summary>
    /// A verlet chain hung from a moving anchor (a hip, a shoulder, a hook's arm, the crawler's tail root): the anchor's motion
    /// drags it, gravity hangs it, stiffness springs it back toward its rest shape in the anchor's frame (tapering to the tip), the
    /// length limits hold its links, the floor (when given) holds it up with friction. The follow-through and secondary motion of
    /// spines, tails, cables, chains and hanging parts. Fixed sub-steps (at most <see cref="BossChainDef.MaxStep"/>), stable at
    /// any frame rate; no allocation per frame.
    /// </summary>
    public sealed class BossChain
    {
        public readonly BossChainDef Def;
        /// <summary>The points (0 the anchor) and each segment's rotation (local -Y along the segment, +Z toward the pole).</summary>
        public readonly Vector3[] Point;public readonly Quaternion[] Rotation;
        private readonly Vector3[] previous;private Vector3 anchor;private Quaternion frame=Quaternion.Identity;private bool ready;
        public BossChain(BossChainDef def){Def=def;int n=Math.Max(2,def.Count);Point=new Vector3[n];previous=new Vector3[n];Rotation=new Quaternion[n-1];}
        public void Reset(Vector3 at,Quaternion orientation)
        {
            anchor=at;frame=orientation;var dir=Vector3.Normalize(BossMath.Rotate(orientation,Def.Rest));
            for(int i=0;i<Point.Length;i++)Point[i]=previous[i]=at+dir*(Def.Length*i);ready=true;Orient();
        }
        /// <summary>Steps by `dt` with the anchor at `at` turned `orientation` (moved there smoothly across the sub-steps).</summary>
        public void Step(Vector3 at,Quaternion orientation,float dt,IBossGround ground=null)
        {
            if(!ready){Reset(at,orientation);return;}dt=BossMath.Clamp(dt,0,.1f);if(dt<=0){Orient();return;}
            var d=Def;int n=(int)Math.Ceiling(dt/d.MaxStep-1e-4f);if(n<1)n=1;float h=dt/n;
            var a0=anchor;var f0=frame;float keep=(float)Math.Exp(-d.Damping*h),slide=(float)Math.Exp(-d.Friction*h);var g=new Vector3(0,-d.Gravity*h*h,0);
            for(int k=1;k<=n;k++)
            {
                float w=k/(float)n;var a=Vector3.Lerp(a0,at,w);var f=BossMath.Slerp(f0,orientation,w);var dir=Vector3.Normalize(BossMath.Rotate(f,d.Rest));
                Point[0]=previous[0]=a;
                for(int i=1;i<Point.Length;i++)
                {
                    var p=Point[i];var v=(p-previous[i])*keep;previous[i]=p;p+=v+g;
                    // The rest shape: the anchor's straight continuation; the pull tapers from the root's stiffness to the tip's.
                    float t=(i-1)/(float)Math.Max(1,Point.Length-2),stiff=BossMath.Lerp(d.Stiffness,d.TipStiffness,t);
                    if(stiff>0){var rest=a+dir*(d.Length*i);p+=(rest-p)*(1-(float)Math.Exp(-stiff*h));}
                    Point[i]=p;
                }
                for(int it=0;it<Math.Max(1,d.Iterations);it++)
                {
                    // The links pull both their ends (the anchor's end only the free one): momentum-neutral, no energy pumped in.
                    for(int i=1;i<Point.Length;i++)
                    {
                        var s=Point[i]-Point[i-1];float l=s.Length(),want=BossMath.Clamp(l,d.Length*d.MinStretch,d.Length*d.MaxStretch);
                        if(l<1e-6f||want==l)continue;var c=s*((l-want)/l);if(i==1)Point[i]-=c;else{Point[i-1]+=c*.5f;Point[i]-=c*.5f;}
                    }
                    if(ground!=null)for(int i=1;i<Point.Length;i++)
                    {
                        float floor=ground.Height(Point[i].X,Point[i].Z)+d.Radius;if(Point[i].Y>=floor)continue;
                        Point[i].Y=floor;if(previous[i].Y<floor)previous[i].Y=floor;
                        // Sliding on the floor loses speed (the link drags rather than skates).
                        previous[i].X=Point[i].X+(previous[i].X-Point[i].X)*slide;previous[i].Z=Point[i].Z+(previous[i].Z-Point[i].Z)*slide;
                    }
                }
                // Then the lengths exactly, root to tip, carrying each point's previous position with it: a positional fix that
                // adds no velocity (a plain follow-the-leader pass would, and the chain would flail).
                for(int i=1;i<Point.Length;i++)
                {
                    var s=Point[i]-Point[i-1];float l=s.Length(),want=BossMath.Clamp(l,d.Length*d.MinStretch,d.Length*d.MaxStretch);
                    if(l<1e-6f||want==l)continue;var c=s*(want/l-1);Point[i]+=c;previous[i]+=c;
                }
                // The floor has the last word (a link pulled under it rests on it).
                if(ground!=null)for(int i=1;i<Point.Length;i++){float floor=ground.Height(Point[i].X,Point[i].Z)+d.Radius;if(Point[i].Y<floor){Point[i].Y=floor;if(previous[i].Y<floor)previous[i].Y=floor;}}
            }
            anchor=at;frame=orientation;Orient();
        }
        private void Orient()
        {
            var pole=BossMath.Rotate(frame,Def.Pole);
            for(int i=0;i<Rotation.Length;i++)Rotation[i]=BossMath.Bone(Point[i],Point[i+1],pole);
        }
    }
}
