using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Presentation only: the shared maths of the directed entrances (owner 2026-10-11: "rework the entrances (Not the DEV
    /// ones) make them much more detailed with great camera handling"). Everything here is a pure function of its arguments
    /// (no state, no allocation), so an entrance drawn at any age, by a late joiner, a scrubbed preview or at any frame rate
    /// is the same frame. Source frame (three.js axes, +x the rig's left); Runtime mirrors x at the Unity boundary.
    /// </summary>
    public static class NativeEntranceMath
    {
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        public static float Smoother(float x){x=Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
        /// <summary>Smoothstep of t across [a, b].</summary>
        public static float Ramp(float t,float a,float b)=>Smooth((t-a)/(b-a));
        /// <summary>A 0..1..0 bump over [a, b] with smooth shoulders (sin squared).</summary>
        public static float Bump(float t,float a,float b){float u=Clamp01((t-a)/(b-a));float s=(float)Math.Sin(Math.PI*u);return s*s;}
        /// <summary>A flash: 0 before `at`, rising over `attack` s, then decaying exponentially with time constant `decay` s.</summary>
        public static float Pulse(float t,float at,float attack,float decay){float s=t-at;if(s<=0)return 0;return Smooth(s/Math.Max(1e-4f,attack))*(float)Math.Exp(-Math.Max(0,s-attack)/decay);}
        /// <summary>Ease out that is still travelling at 1 (a move alive when the next shot cuts in).</summary>
        public static float Live(float x){x=Clamp01(x);return x*(1.6f-.6f*x);}
        public static float EaseOut(float x){x=Clamp01(x);float i=1-x;return 1-i*i*i;}
        public static float EaseIn(float x){x=Clamp01(x);return x*x*x;}
        public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>A deterministic hash in 0..1 for (seed, index): every particle of an entrance is a pure function of its index.</summary>
        public static float Rand(int seed,int i)
        {
            uint h=(uint)seed*0x9E3779B1u^(uint)i*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;
            return (h&0xffffff)/16777216f;
        }
        public static float Signed(int seed,int i)=>Rand(seed,i)*2-1;
        /// <summary>A unit direction on the sphere (or the upper hemisphere) for (seed, index).</summary>
        public static Vector3 Direction(int seed,int i,bool upper=false)
        {
            float y=upper?Rand(seed,i*2):Signed(seed,i*2),a=Rand(seed,i*2+1)*6.2831853f,r=(float)Math.Sqrt(Math.Max(0,1-y*y));
            return new Vector3(r*(float)Math.Cos(a),y,r*(float)Math.Sin(a));
        }
        public static Quaternion Yaw(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,a);
        public static Vector3 RotateY(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X*c+v.Z*s,v.Y,-v.X*s+v.Z*c);}
        /// <summary>
        /// The intro curve (intro-curves.ts introCurve): a cubic Hermite through the keys `values[offset + k*stride]` at `times`;
        /// plain keys get monotone tangents (no overshoot, a stop only at a genuine extreme), `pass` keys (bit k) Catmull-Rom
        /// tangents (the motion flows through), `impact` keys arrive travelling (1.3x the incoming slope), stop dead and ring
        /// out with a small damped settle.
        /// </summary>
        public static float Curve(float[] times,float[] values,int offset,int stride,float time,uint pass=0,uint impact=0)
        {
            int last=times.Length-1;float value;
            float V(int k)=>values[offset+k*stride];
            if(time<=times[0])value=V(0);else if(time>=times[last])value=V(last);
            else
            {
                int i=0;while(i<last-1&&times[i+1]<time)i++;
                float Slope(int n)=>(V(n+1)-V(n))/Math.Max(1e-5f,times[n+1]-times[n]);
                float Tangent(int n,bool arriving)
                {
                    if(n==0||n==last)return 0;
                    if((impact>>n&1)!=0)return arriving?1.3f*Slope(n-1):0;
                    if((pass>>n&1)!=0)return (V(n+1)-V(n-1))/Math.Max(1e-5f,times[n+1]-times[n-1]);
                    float a=Slope(n-1),b=Slope(n);return a*b<=0?0:2*a*b/(a+b);
                }
                float h=times[i+1]-times[i],u=(time-times[i])/h,u2=u*u,u3=u2*u;
                value=(2*u3-3*u2+1)*V(i)+(u3-2*u2+u)*h*Tangent(i,false)+(-2*u3+3*u2)*V(i+1)+(u3-u2)*h*Tangent(i+1,true);
            }
            if(impact!=0)for(int n=1;n<=last;n++)if((impact>>n&1)!=0){float tau=time-times[n];if(tau>0&&tau<.6f)value+=(V(n)-V(n-1))*.08f*(float)Math.Exp(-18*tau)*(float)Math.Sin(38*tau);}
            return value;
        }
        /// <summary>
        /// The slow-motion beat (presentation clock): the entrance's body, effects and camera are drawn at Warp(age). Identical
        /// to age until `at` (the hit, so the sting's accent and the hit still coincide); at `at` the clock drops to `rate`
        /// (a hit-stop), holds it for `hold` s, eases back over `recover` s, and runs faster over the following `catchUp` s
        /// until it is exactly age again at at+hold+catchUp (before the name card), so durations and later beats are unchanged.
        /// C0 everywhere, monotone (its rate never drops to zero), closed form.
        /// </summary>
        public static float Warp(float age,float at,float rate,float hold,float recover,float catchUp)
        {
            float s=age-at;if(!(s>0)||rate>=1)return age;
            float d1=Math.Min(s,hold);if(s>hold){float x=Clamp01((s-hold)/recover);d1+=recover*(x-x*x*x+.5f*x*x*x*x);}
            float y=Math.Max(0,Math.Min(s-hold,catchUp));float d2=y*.5f-catchUp/(4*(float)Math.PI)*(float)Math.Sin(2*Math.PI*y/catchUp);
            float q=2*(1-rate)*(hold+recover*.5f)/catchUp;
            return age-(1-rate)*d1+q*d2;
        }
        /// <summary>The rate of Warp at `age` (d Warp / d age).</summary>
        public static float WarpRate(float age,float at,float rate,float hold,float recover,float catchUp)
        {
            float s=age-at;if(!(s>0)||rate>=1)return 1;
            float e1=s<=hold?1:1-Smooth((s-hold)/recover);float y=s-hold,e2=y>0&&y<catchUp?(float)Math.Pow(Math.Sin(Math.PI*y/catchUp),2):0;
            float q=2*(1-rate)*(hold+recover*.5f)/catchUp;return 1-(1-rate)*e1+q*e2;
        }
    }

    /// <summary>How a shot starts.</summary>
    public enum NativeEntranceCut : byte { Cut, Whip, Blend }
    /// <summary>
    /// One authored camera shot of a directed entrance: from `At` (presentation clock) until the next shot. Eye and look
    /// are three keys (start, middle, end: a quadratic Bezier through the middle, so a move can arc) relative to the shot's
    /// anchor, in the subject's frame at the shot's Face time: the stage origin blended toward the subject by `Track`. With `Orbit` the eye keys are (angle in degrees from
    /// the subject's front, radius, height) around the anchor and swept on the circle. `Ease`: 0 ease in-out, 1 ease out,
    /// 2 live (still travelling at the end), 3 linear.
    /// </summary>
    public struct NativeEntranceShot
    {
        public float At;public NativeEntranceCut In;public float InTime;
        public Vector3 Eye0,Eye1,Eye2,Look0,Look1,Look2;public float Fov0,Fov1,Roll0,Roll1,Track;public bool Orbit;public byte Ease;
        /// <summary>The presentation time whose subject heading orients the shot's offsets (held for the whole shot, so the
        /// camera never swings with the subject's turns); negative: the stage's own axes.</summary>
        public float Face;
        /// <summary>How much of the subject's height change the anchor follows (0: heights above the floor).</summary>
        public float TrackY;
        /// <summary>An anchor (NativeEntranceAnchor) the look follows, smoothed, with the Look keys as offsets from it; -1: none.</summary>
        public int Follow;
    }
    /// <summary>The camera of one frame (source frame).</summary>
    public struct NativeEntranceView{public Vector3 Eye,Look,Up;public float Fov;}
    /// <summary>
    /// The directed entrance camera: authored shots, eased (no linear starts or stops), cut, whip-panned or blended into
    /// each other, slowed with the warp, settling to a still hero frame before the gameplay camera takes over, and kept out
    /// of the floor, the pilot and the stage's solid props (Guard).
    /// </summary>
    public static class NativeEntranceCamera
    {
        /// <summary>The camera never goes lower than this above the floor top.</summary>
        public const float EyeClearance=.16f;
        /// <summary>Nor closer than this to the pilot's body line (pelvis to head, and the feet).</summary>
        public const float BodyClearance=.8f;
        private static float Eased(byte ease,float u)=>ease==1?NativeEntranceMath.EaseOut(u):ease==2?NativeEntranceMath.Live(u):ease==3?NativeEntranceMath.Clamp01(u):NativeEntranceMath.Smoother(u)*.6f+NativeEntranceMath.Smooth(u)*.4f;
        private static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float u){float v=1-u;return a*(v*v)+b*(2*u*v)+c*(u*u);}
        /// <summary>
        /// Shot `k` of `shots` at `time` (its own move, ignoring transitions), for a subject at `subject` with the frame's
        /// heading `heading` (offsets are authored in the subject's frame: +z its front, +x its left).
        /// </summary>
        public static void Shot(NativeEntranceShot[] shots,int k,float time,float end,Vector3 subject,Func<float,float> facing,out NativeEntranceView view,Func<int,float,Vector3> follow=null)
        {
            var s=shots[k];float heading=s.Face>=0&&facing!=null?facing(s.Face):0;float stop=k+1<shots.Length?shots[k+1].At:end;float u=Eased(s.Ease,(time-s.At)/Math.Max(1e-3f,stop-s.At));
            var anchor=new Vector3(subject.X*s.Track,subject.Y*s.TrackY,subject.Z*s.Track);Vector3 eye;
            if(s.Orbit)
            {
                float a0=s.Eye0.X,a1=s.Eye1.X,a2=s.Eye2.X;float angle=(1-u)*(1-u)*a0+2*u*(1-u)*a1+u*u*a2,r=Bezier(new Vector3(s.Eye0.Y),new Vector3(s.Eye1.Y),new Vector3(s.Eye2.Y),u).X,h=Bezier(new Vector3(s.Eye0.Z),new Vector3(s.Eye1.Z),new Vector3(s.Eye2.Z),u).X;
                float rad=angle*(float)Math.PI/180;eye=new Vector3(r*(float)Math.Sin(rad),h,r*(float)Math.Cos(rad));
            }
            else eye=Bezier(s.Eye0,s.Eye1,s.Eye2,u);
            var look=Bezier(s.Look0,s.Look1,s.Look2,u);var lookAt=s.Follow>=0&&follow!=null?follow(s.Follow,time)+NativeEntranceMath.RotateY(look,heading):anchor+NativeEntranceMath.RotateY(look,heading);
            view=new NativeEntranceView{Eye=anchor+NativeEntranceMath.RotateY(eye,heading),Look=lookAt,Fov=NativeEntranceMath.Lerp(s.Fov0,s.Fov1,u)};
            float roll=NativeEntranceMath.Lerp(s.Roll0,s.Roll1,u);var f=Vector3.Normalize(view.Look-view.Eye);var right=Vector3.Cross(f,Vector3.UnitY);if(right.LengthSquared()<1e-6f)right=Vector3.UnitX;right=Vector3.Normalize(right);
            var up=Vector3.Cross(right,f);view.Up=Vector3.Normalize(up*(float)Math.Cos(roll)+right*(float)Math.Sin(roll));
        }
        /// <summary>The camera at `time`: the active shot, with its whip or blend from the previous shot.</summary>
        public static void Evaluate(NativeEntranceShot[] shots,float time,float end,Func<float,Vector3> subject,Func<float,float> heading,out NativeEntranceView view,Func<int,float,Vector3> follow=null)
        {
            int k=0;while(k+1<shots.Length&&time>=shots[k+1].At)k++;
            // A whip straddles its cut: it starts half its length before the new shot and lands half after.
            int next=k+1<shots.Length&&shots[k+1].In==NativeEntranceCut.Whip&&time>=shots[k+1].At-shots[k+1].InTime*.5f?k+1:-1;
            Shot(shots,k,time,end,subject(time),heading,out view,follow);
            if(next>=0||shots[k].In==NativeEntranceCut.Whip&&time<shots[k].At+shots[k].InTime*.5f&&k>0)
            {
                int to=next>=0?next:k,from=to-1;float w=shots[to].InTime,start=shots[to].At-w*.5f,u=NativeEntranceMath.Clamp01((time-start)/w);
                Shot(shots,from,Math.Min(time,shots[to].At-1e-4f),end,subject(time),heading,out var a,follow);Shot(shots,to,Math.Max(time,shots[to].At),end,subject(time),heading,out var b,follow);
                // The swing: almost all of the turn in the middle third (smootherstep of smootherstep), a quick blur of a pan.
                float e=NativeEntranceMath.Smoother(NativeEntranceMath.Smoother(u));var pv=subject(time);view=Mix(a,b,e,new Vector3(pv.X,0,pv.Z));return;
            }
            if(shots[k].In==NativeEntranceCut.Blend&&k>0&&time<shots[k].At+shots[k].InTime)
            {
                Shot(shots,k-1,time,end,subject(time),heading,out var a,follow);float e=NativeEntranceMath.Smoother((time-shots[k].At)/shots[k].InTime);var pv=subject(time);view=Mix(a,view,e,new Vector3(pv.X,0,pv.Z));
            }
        }
        /// <summary>
        /// Two views mixed: the eye swung round `pivot` (angle on the shortest arc, radius and height lerped, so a transition
        /// orbits the subject instead of cutting through it), the look points lerped, fov and up mixed.
        /// </summary>
        public static NativeEntranceView Mix(NativeEntranceView a,NativeEntranceView b,float e,Vector3 pivot)
        {
            Vector3 ra=a.Eye-pivot,rb=b.Eye-pivot;float aa=(float)Math.Atan2(ra.X,ra.Z),ab=(float)Math.Atan2(rb.X,rb.Z),d=ab-aa;
            while(d>Math.PI)d-=2*(float)Math.PI;while(d<-Math.PI)d+=2*(float)Math.PI;
            float ang=aa+d*e,r=NativeEntranceMath.Lerp((float)Math.Sqrt(ra.X*ra.X+ra.Z*ra.Z),(float)Math.Sqrt(rb.X*rb.X+rb.Z*rb.Z),e);
            var eye=new Vector3(pivot.X+r*(float)Math.Sin(ang),NativeEntranceMath.Lerp(a.Eye.Y,b.Eye.Y,e),pivot.Z+r*(float)Math.Cos(ang));
            var up=Vector3.Normalize(Vector3.Lerp(a.Up,b.Up,e));
            return new NativeEntranceView{Eye=eye,Look=Vector3.Lerp(a.Look,b.Look,e),Up=up,Fov=NativeEntranceMath.Lerp(a.Fov,b.Fov,e)};
        }
        /// <summary>
        /// Keeps the eye clear: above the floor top by EyeClearance, at least BodyClearance from the pilot's body line (head to
        /// pelvis to the feet midpoint) and outside every solid box (centre, half size; axis aligned in the source frame) with a
        /// .25 m margin. Each push is a smooth function of the unguarded eye (a soft-max), so a guarded move stays smooth.
        /// </summary>
        public static Vector3 Guard(Vector3 eye,float floor,Vector3 head,Vector3 pelvis,Vector3 feet,ReadOnlySpan<Vector4> boxes)
        {
            // Body line: the nearest point on head-pelvis-feet.
            Vector3 near=Nearest(eye,head,pelvis);var n2=Nearest(eye,pelvis,feet);if((n2-eye).LengthSquared()<(near-eye).LengthSquared())near=n2;
            // Vertical distance counts .35 (the camera may look down on or up at the subject from close above or below).
            var off=eye-near;float dy=off.Y*.35f;var flat=new Vector3(off.X,0,off.Z);float dh=flat.Length(),d=(float)Math.Sqrt(dh*dh+dy*dy);
            if(d<BodyClearance+.2f)
            {
                float want=SoftMax(d,BodyClearance,.2f),h=(float)Math.Sqrt(Math.Max(0,want*want-dy*dy));var dir=dh>1e-4f?flat/dh:Vector3.UnitZ;
                eye=new Vector3(near.X+dir.X*h,eye.Y,near.Z+dir.Z*h);
            }
            for(int i=0;i+1<boxes.Length;i+=2)
            {
                var c=new Vector3(boxes[i].X,boxes[i].Y,boxes[i].Z);var h=new Vector3(boxes[i+1].X,boxes[i+1].Y,boxes[i+1].Z)+new Vector3(.25f);var l=eye-c;
                float ex=h.X-Math.Abs(l.X),ey=h.Y-Math.Abs(l.Y),ez=h.Z-Math.Abs(l.Z);if(ex<=0||ey<=0||ez<=0)continue;
                if(ex<=ez&&ex<=ey)eye.X+=Math.Sign(l.X==0?1:l.X)*ex;else if(ez<=ey)eye.Z+=Math.Sign(l.Z==0?1:l.Z)*ez;else eye.Y+=Math.Sign(l.Y==0?1:l.Y)*ey;
            }
            eye.Y=SoftMax(eye.Y,floor+EyeClearance,.06f);
            return eye;
        }
        private static Vector3 Nearest(Vector3 p,Vector3 a,Vector3 b){var ab=b-a;float t=Vector3.Dot(p-a,ab)/Math.Max(1e-6f,ab.LengthSquared());return a+ab*NativeEntranceMath.Clamp01(t);}
        /// <summary>A smooth max(x, floor): x well above, floor+k well below, C1 between.</summary>
        public static float SoftMax(float x,float floor,float k){float d=x-floor;return d>=k?x:d<=-k?floor:floor+(d+k)*(d+k)/(4*k);}
    }
}
