using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>How a v2 shot's move is eased over its length.</summary>
    public enum NativeIntroEase:byte{InOut,Out,In,Linear,Live}
    /// <summary>How a v2 shot comes in from the one before: a cut, a blend (both moves mixed over InTime) or a whip (a fast blend
    /// with a smear of motion: the eye turns through the gap).</summary>
    public enum NativeIntroCut:byte{Cut,Blend,Whip}

    /// <summary>
    /// The cinematic camera rig for v2 intros (docs/INTROS-V2.md "The camera rig"). Shots are built once in the intro's
    /// constructor with fluent calls (Hold, Dolly, Crane, Spline, Orbit, Hero; then Follow, Lead, Fov, Roll, Ease, In, Focus,
    /// Handheld on the shot just added; Bars for the letterbox), and Evaluate is a pure, allocation-free function of the
    /// presentation clock. Positions are the source frame (+x the rig's left, y up, the pilot at the origin facing
    /// NativeProceduralEntrance.Facing). Reduced effects drops the handheld shake and keeps every move.
    /// </summary>
    public sealed class NativeIntroCamera
    {
        public sealed class Shot
        {
            public float At;public NativeIntroCut In;public float InTime=.35f;
            /// <summary>Eye control points: one holds, two dolly in a line, three or more run a Catmull-Rom spline through them.</summary>
            public Vector3[] Path;
            /// <summary>An orbit instead of a path: degrees from the pilot's front (+ toward its left), radius and height, about Pivot (or the follow anchor).</summary>
            public bool Orbit;public float Deg0,Deg1,Radius0,Radius1,Height0,Height1;public Vector3 Pivot;
            /// <summary>The look target, from Look0 to Look1; with Follow, offsets from that anchor (smoothed), led by Lead s along its motion.</summary>
            public Vector3 Look0,Look1;public int Follow=-1;public float Lead;
            /// <summary>The eye rides along with the follow anchor (Track 1) or stays where it is put (0).</summary>
            public float Track;
            public float Fov0=40,Fov1=40,Roll0,Roll1;public NativeIntroEase Ease;
            /// <summary>Focus distance from/to (m; 0: the look target) and aperture (0 deep .. 1 very shallow).</summary>
            public float Focus0,Focus1,Aperture;
            /// <summary>Handheld drift (m and degrees at 1); dropped in Reduced effects.</summary>
            public float Handheld;
        }
        private readonly List<Shot> shots=new List<Shot>();
        private readonly List<(float at,float amount)> bars=new List<(float,float)>();
        private readonly List<(float at,float amount)> blacks=new List<(float,float)>();
        public int Count=>shots.Count;
        public Shot this[int i]=>shots[i];
        private Shot Add(Shot s){if(shots.Count>0&&s.At<shots[shots.Count-1].At)throw new ArgumentException("NativeIntroCamera: shots must be added in time order");shots.Add(s);return s;}
        private Shot Last=>shots.Count>0?shots[shots.Count-1]:throw new InvalidOperationException("NativeIntroCamera: add a shot first");

        // ---- shots ----
        /// <summary>A locked-off frame from `eye` on `look`.</summary>
        public NativeIntroCamera Hold(float at,Vector3 eye,Vector3 look,float fov=40){Add(new Shot{At=at,Path=new[]{eye},Look0=look,Look1=look,Fov0=fov,Fov1=fov});return this;}
        /// <summary>A dolly: the eye in a straight line from `from` to `to`, the look from `look0` to `look1`.</summary>
        public NativeIntroCamera Dolly(float at,Vector3 from,Vector3 to,Vector3 look0,Vector3 look1,float fov0=40,float fov1=40){Add(new Shot{At=at,Path=new[]{from,to},Look0=look0,Look1=look1,Fov0=fov0,Fov1=fov1});return this;}
        /// <summary>A crane: the eye rises (or falls) by `rise` m while it travels from `from` to `to`, on an arc (the boom swings).</summary>
        public NativeIntroCamera Crane(float at,Vector3 from,Vector3 to,float rise,Vector3 look0,Vector3 look1,float fov0=40,float fov1=40)
        {var mid=(from+to)*.5f+new Vector3(0,rise*.5f,0);Add(new Shot{At=at,Path=new[]{from,mid,to+new Vector3(0,rise,0)},Look0=look0,Look1=look1,Fov0=fov0,Fov1=fov1});return this;}
        /// <summary>A spline move through `path` (three or more points, Catmull-Rom; the ends are its first and last).</summary>
        public NativeIntroCamera Spline(float at,Vector3[] path,Vector3 look0,Vector3 look1,float fov0=40,float fov1=40){if(path==null||path.Length==0)throw new ArgumentException("NativeIntroCamera.Spline: no points");Add(new Shot{At=at,Path=(Vector3[])path.Clone(),Look0=look0,Look1=look1,Fov0=fov0,Fov1=fov1});return this;}
        /// <summary>An orbit about `pivot` (or the follow anchor): degrees from the pilot's front, radius and height swept from 0 to 1.</summary>
        public NativeIntroCamera Orbit(float at,Vector3 pivot,float deg0,float deg1,float radius0,float radius1,float height0,float height1,Vector3 look0,Vector3 look1,float fov0=40,float fov1=40)
        {Add(new Shot{At=at,Orbit=true,Pivot=pivot,Deg0=deg0,Deg1=deg1,Radius0=radius0,Radius1=radius1,Height0=height0,Height1=height1,Look0=look0,Look1=look1,Fov0=fov0,Fov1=fov1});return this;}
        /// <summary>The final hero framing: a slow push in from a slightly low three-quarter front on the chest/head, settling still
        /// (ease out) by the end. `side` +1 frames from the pilot's left, -1 its right; `height` the chest height (m).</summary>
        public NativeIntroCamera Hero(float at,float side=1,float height=1.15f,float distance=3.1f,float fov=32)
        {
            float yaw=NativeProceduralEntrance.Facing+side*.42f;var dir=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
            var from=dir*(distance+.45f)+new Vector3(0,height-.25f,0);var to=dir*distance+new Vector3(0,height-.18f,0);
            Add(new Shot{At=at,Path=new[]{from,to},Look0=new Vector3(0,height+.02f,0),Look1=new Vector3(0,height+.06f,0),Fov0=fov+2,Fov1=fov,Ease=NativeIntroEase.Out,In=NativeIntroCut.Cut});return this;
        }
        // ---- modifiers on the shot just added ----
        /// <summary>The look follows anchor `which` (Look0/Look1 become offsets from it), led by `lead` s along its motion.</summary>
        public NativeIntroCamera Follow(NativeEntranceAnchor which,float lead=0,float track=0){var s=Last;s.Follow=(int)which;s.Lead=lead;s.Track=track;return this;}
        public NativeIntroCamera Fov(float from,float to){var s=Last;s.Fov0=from;s.Fov1=to;return this;}
        /// <summary>Roll (degrees, + toward the rig's left) from/to.</summary>
        public NativeIntroCamera Roll(float from,float to){var s=Last;s.Roll0=from;s.Roll1=to;return this;}
        public NativeIntroCamera Ease(NativeIntroEase ease){Last.Ease=ease;return this;}
        /// <summary>How the shot comes in (a cut by default; a blend or a whip over `time` s).</summary>
        public NativeIntroCamera In(NativeIntroCut cut,float time=.35f){var s=Last;s.In=cut;s.InTime=Math.Max(.02f,time);return this;}
        /// <summary>A focus pull: from/to distance (m; 0 the look target), aperture 0..1.</summary>
        public NativeIntroCamera Focus(float from,float to,float aperture){var s=Last;s.Focus0=from;s.Focus1=to;s.Aperture=NativeEntranceMath.Clamp01(aperture);return this;}
        /// <summary>A handheld drift (about `amount` cm and degrees); none in Reduced effects.</summary>
        public NativeIntroCamera Handheld(float amount){Last.Handheld=Math.Max(0,amount);return this;}
        /// <summary>Letterbox key: from presentation time `at` the bars ease to `amount` (0..1) over .5 s.</summary>
        public NativeIntroCamera Bars(float at,float amount){bars.Add((at,NativeEntranceMath.Clamp01(amount)));bars.Sort((a,b)=>a.at.CompareTo(b.at));return this;}
        /// <summary>Fade-to-black key: from `at` the black eases to `amount` over .25 s.</summary>
        public NativeIntroCamera Black(float at,float amount){blacks.Add((at,NativeEntranceMath.Clamp01(amount)));blacks.Sort((a,b)=>a.at.CompareTo(b.at));return this;}

        // ---- evaluation ----
        private static float Eased(NativeIntroEase e,float u)
        {
            u=NativeEntranceMath.Clamp01(u);
            switch(e){case NativeIntroEase.Out:return NativeEntranceMath.EaseOut(u);case NativeIntroEase.In:return u*u*(2-u)*.5f+u*u*u*.5f;case NativeIntroEase.Linear:return u;case NativeIntroEase.Live:return NativeEntranceMath.Live(u);}
            return NativeEntranceMath.Smoother(u)*.6f+NativeEntranceMath.Smooth(u)*.4f;
        }
        private static Vector3 Path(Vector3[] p,float u)
        {
            int n=p.Length;if(n==1)return p[0];if(n==2)return Vector3.Lerp(p[0],p[1],u);
            float x=u*(n-1);int i=Math.Min(n-2,(int)x);float f=x-i;var p0=p[Math.Max(0,i-1)];var p1=p[i];var p2=p[i+1];var p3=p[Math.Min(n-1,i+2)];
            if(i==0)p0=2*p1-p2;if(i+2>n-1)p3=2*p2-p1;float f2=f*f,f3=f2*f;
            return .5f*(2*p1+(p2-p0)*f+(2*p0-5*p1+4*p2-p3)*f2+(3*p1-p0-3*p2+p3)*f3);
        }
        private static float Noise(float t,float seed)=>(float)(Math.Sin(t*1.13+seed)*.5+Math.Sin(t*2.37+seed*1.7)*.3+Math.Sin(t*4.11+seed*2.3)*.2);
        private float End(NativeEntranceScript s,int k)=>k+1<shots.Count?shots[k+1].At:Math.Max(shots[k].At+.01f,s.SettleAt);
        /// <summary>Shot `k`'s own frame at `t` (no transition).</summary>
        public void ShotView(NativeEntranceScript script,int k,float t,bool reduced,out NativeEntranceView view,out float focus,out float aperture)
        {
            var s=shots[k];float stop=End(script,k);float u=Eased(s.Ease,(t-s.At)/Math.Max(1e-3f,stop-s.At));
            Vector3 anchor=Vector3.Zero;if(s.Follow>=0){anchor=script.Smoothed(s.Follow,t);if(s.Lead!=0)anchor+=script.Velocity((NativeEntranceAnchor)s.Follow,t)*s.Lead;}
            Vector3 eye;
            if(s.Orbit)
            {
                float deg=NativeEntranceMath.Lerp(s.Deg0,s.Deg1,u)*(float)(Math.PI/180)+NativeProceduralEntrance.Facing,r=NativeEntranceMath.Lerp(s.Radius0,s.Radius1,u),h=NativeEntranceMath.Lerp(s.Height0,s.Height1,u);
                var pivot=s.Follow>=0?new Vector3(anchor.X,0,anchor.Z)+s.Pivot:s.Pivot;eye=pivot+new Vector3((float)Math.Sin(deg)*r,h,(float)Math.Cos(deg)*r);
            }
            else eye=Path(s.Path,u)+(s.Follow>=0?new Vector3(anchor.X,0,anchor.Z)*s.Track:Vector3.Zero);
            var look=Vector3.Lerp(s.Look0,s.Look1,u)+(s.Follow>=0?anchor:Vector3.Zero);
            if(!reduced&&s.Handheld>0){float a=s.Handheld*.01f;eye+=new Vector3(Noise(t,1),Noise(t,2)*.6f,Noise(t,3))*a;look+=new Vector3(Noise(t,4),Noise(t,5),Noise(t,6))*(a*.6f);}
            float roll=NativeEntranceMath.Lerp(s.Roll0,s.Roll1,u)*(float)(Math.PI/180);if(!reduced&&s.Handheld>0)roll+=Noise(t,7)*s.Handheld*.002f;
            var fwd=look-eye;if(fwd.LengthSquared()<1e-8f)fwd=new Vector3(0,0,-1);fwd=Vector3.Normalize(fwd);
            var up=Vector3.UnitY;if(roll!=0)up=Vector3.Transform(up,Quaternion.CreateFromAxisAngle(fwd,roll));
            view=new NativeEntranceView{Eye=eye,Look=look,Up=up,Fov=NativeEntranceMath.Lerp(s.Fov0,s.Fov1,u)};
            float fd=NativeEntranceMath.Lerp(s.Focus0,s.Focus1,u);focus=fd>0?fd:Vector3.Distance(eye,look);aperture=s.Aperture;
        }
        /// <summary>The camera at presentation time `t` (shots, their transitions; the handheld only without Reduced).</summary>
        public void Evaluate(NativeEntranceScript script,float t,bool reduced,out NativeEntranceView view)=>Evaluate(script,t,reduced,out view,out _,out _);
        public void Evaluate(NativeEntranceScript script,float t,bool reduced,out NativeEntranceView view,out float focus,out float aperture)
        {
            if(shots.Count==0){view=new NativeEntranceView{Eye=new Vector3(.9f,1.2f,3.6f),Look=new Vector3(0,1,0),Up=Vector3.UnitY,Fov=36};focus=3.7f;aperture=0;return;}
            int k=0;for(int i=1;i<shots.Count;i++)if(shots[i].At<=t)k=i;
            ShotView(script,k,t,reduced,out view,out focus,out aperture);
            var s=shots[k];if(k==0||s.In==NativeIntroCut.Cut)return;float e=(t-s.At)/s.InTime;if(e>=1)return;
            ShotView(script,k-1,t,reduced,out var from,out float ff,out float fa);float w=s.In==NativeIntroCut.Whip?NativeEntranceMath.Smoother(e):NativeEntranceMath.Smooth(e);
            view=NativeEntranceCamera.Mix(from,view,w,script.Anchor(NativeEntranceAnchor.Chest,t));focus=NativeEntranceMath.Lerp(ff,focus,w);aperture=NativeEntranceMath.Lerp(fa,aperture,w);
        }
        private static float Keyed(List<(float at,float amount)> keys,float t,float ease)
        {
            float v=0;for(int i=0;i<keys.Count;i++){if(keys[i].at>t)break;float from=v;v=NativeEntranceMath.Lerp(from,keys[i].amount,NativeEntranceMath.Smooth((t-keys[i].at)/ease));}
            return v;
        }
        /// <summary>The lens at `t`: the letterbox, the black, the focus.</summary>
        public void Lens(NativeEntranceScript script,float t,bool reduced,out NativeIntroLens lens)
        {
            Evaluate(script,t,reduced,out _,out float focus,out float aperture);
            lens=new NativeIntroLens{Letterbox=Keyed(bars,t,.5f),Black=Keyed(blacks,t,.25f),Focus=focus,Aperture=aperture};
        }
    }
}
