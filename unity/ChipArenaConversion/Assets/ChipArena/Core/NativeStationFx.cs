using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Central Station kinds' effects as pure shapes (docs/EXPEDITION-CENTRAL-STATION.md section 7's VFX lists): seeded,
    /// a function of the effect's age on the presentation clock, so host, guests and the killcam draw the same frame
    /// (Runtime/NativeStationKindsPresentation.cs draws them through the shared effects renderer; the render harness through
    /// the same calls). No allocation: callers pass their own buffers.
    /// <list type="bullet">
    /// <item>Linesman: the jagged bolt (<see cref="Bolt"/>, re-jagged 30 times a second, with forks) for the arc, its chains, the
    /// coil's crawling arcs and the floor crackle; sparks (<see cref="Spark"/>) for the grounded feet, the battery's burst and
    /// a weak-point hit.</item>
    /// <item>Conductor: the whistle's white ring out to its 14 m (<see cref="WhistleRing"/>), the bell's red rings off the
    /// raised lantern (<see cref="BellRing"/>), the red flash on the buffed adds (<see cref="AboardFlash"/>), the lantern's
    /// glass shards (<see cref="Shard"/>).</item>
    /// <item>Undertow: ripple rings (<see cref="Ripple"/>), the bubbles converging on the target's feet in the grab's
    /// telegraph (<see cref="Converge"/>), the surfacing splash (<see cref="Splash"/>) and its drips (<see cref="Drip"/>).</item>
    /// <item>Pileup: the shove's dust in its 70 degree cone (<see cref="ShoveDust"/>), the door's scrape and frontal hits
    /// (sparks), the split's burst (<see cref="SplitChunk"/>).</item>
    /// </list>
    /// </summary>
    public static class NativeStationFx
    {
        private static float Hf(uint seed,int i,int salt)=>(float)NativeGoreRules.Hash01(seed,i,salt);
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float C01(float x)=>x<0?0:x>1?1:x;

        // ---- electricity ----
        public const int BoltPoints=12,BoltForks=3;
        /// <summary>The bolt's jag re-rolls this many times a second (a crackle, not a smooth wobble).</summary>
        public const float BoltRate=30;
        /// <summary>
        /// A jagged electric path from a to b into `into` (n points, both ends exact): each inner point thrown sideways off the
        /// line by up to `amp` times the length's square root (most in the middle), re-rolled BoltRate times a second.
        /// Returns n. `strand` keeps several bolts from the same seed apart.
        /// </summary>
        public static int Bolt(uint seed,int strand,float time,Vector3 a,Vector3 b,float amp,Vector3[] into,int n=BoltPoints)
        {
            n=Math.Max(2,Math.Min(n,into.Length));var d=b-a;float len=d.Length();var dir=len>1e-5f?d/len:Vector3.UnitY;
            var side=Vector3.Cross(dir,Math.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX);side=Vector3.Normalize(side);var up=Vector3.Cross(side,dir);
            int frame=(int)Math.Floor(time*BoltRate);uint s=seed^(uint)(strand*0x9E3779B1u)^(uint)(frame*0x85EBCA6Bu);float k=amp*(float)Math.Sqrt(Math.Max(.01f,len));
            for(int i=0;i<n;i++)
            {
                float u=(float)i/(n-1);var p=a+d*u;if(i>0&&i<n-1){float env=(float)Math.Sin(Math.PI*u);p+=side*((Hf(s,i,301)-.5f)*2*k*env)+up*((Hf(s,i,302)-.5f)*2*k*env);}
                into[i]=p;
            }
            return n;
        }
        /// <summary>Fork f of a bolt (n points along it): a short branch off one of its points, thrown out and down; false when this
        /// frame's bolt has no such fork.</summary>
        public static bool Fork(uint seed,int strand,int f,float time,Vector3[] bolt,int n,out Vector3 from,out Vector3 to)
        {
            int frame=(int)Math.Floor(time*BoltRate);uint s=seed^(uint)(strand*0x27D4EB2Fu)^(uint)(frame*0x165667B1u);from=to=Vector3.Zero;
            if(n<3||Hf(s,f,310)<.35f)return false;int at=1+(int)(Hf(s,f,311)*(n-2));from=bolt[Math.Min(n-2,at)];var dir=bolt[Math.Min(n-1,at+1)]-bolt[Math.Max(0,at-1)];
            float l=dir.Length();if(l<1e-4f)return false;dir/=l;var off=new Vector3(Hf(s,f,312)-.5f,-.4f-Hf(s,f,313)*.5f,Hf(s,f,314)-.5f);to=from+Vector3.Normalize(dir+off*1.6f)*(.12f+.3f*Hf(s,f,315))*Math.Min(1.5f,l*n*.25f);return true;
        }
        /// <summary>A bolt's strength at `age` s from its strike: a hard flash, then flickering out by BoltSeconds.</summary>
        public static float BoltLife(float age)
        {
            if(float.IsNaN(age)||age<0||age>=NativeStationKinds.BoltSeconds)return 0;float u=age/NativeStationKinds.BoltSeconds;
            float flick=((int)(age*BoltRate)&1)==0?1:.55f;return (1-u*u)*flick;
        }
        /// <summary>A target's jolt after the bolt (its slow's .35 s): crackle strength 0..1.</summary>
        public static float Jolt(float age)=>float.IsNaN(age)||age<0||age>=NativeStationKinds.ArcJolt+.15f?0:(1-age/(NativeStationKinds.ArcJolt+.15f))*(((int)(age*24)&1)==0?1:.6f);

        // ---- sparks (seeded streaks thrown from a point and falling) ----
        /// <summary>
        /// Spark i of a burst `age` s old, thrown at up to `speed` m/s inside a cone round `dir` (spread 0 a line, 1 a half sphere):
        /// its offset from the burst point, its velocity (for the streak), its life left 0..1; false once out.
        /// </summary>
        public static bool Spark(uint seed,int i,float age,float speed,Vector3 dir,float spread,out Vector3 off,out Vector3 vel,out float life)
        {
            off=vel=Vector3.Zero;life=0;float span=.25f+.35f*Hf(seed,i,321);if(age<0||age>span)return false;
            var r=new Vector3(Hf(seed,i,322)-.5f,Hf(seed,i,323)-.5f,Hf(seed,i,324)-.5f)*2;var d=Vector3.Normalize(dir+r*spread);float v=speed*(.35f+.65f*Hf(seed,i,325));
            var v0=d*v;const float G=9.8f;off=v0*age+new Vector3(0,-.5f*G*age*age,0);vel=v0+new Vector3(0,-G*age,0);life=1-age/span;return true;
        }

        // ---- the Conductor ----
        /// <summary>The whistle's ring at `age` s from the blast: out to its 14 m in .35 s, fading by .6 s.</summary>
        public static bool WhistleRing(float age,out float radius,out float alpha)
        {
            radius=alpha=0;if(float.IsNaN(age)||age<0||age>.6f)return false;float u=C01(age/.35f);radius=NativeStationKinds.WhistleRadius*(1-(1-u)*(1-u));alpha=(1-Sm((age-.15f)/.45f));return true;
        }
        /// <summary>Ring k of the last call's bell (one each ring of the lantern, 4.5 a second): its radius round the lantern and alpha.</summary>
        public static bool BellRing(float age,int k,out float radius,out float alpha)
        {
            radius=alpha=0;float start=.25f+k/4.5f;if(float.IsNaN(age)||start>NativeStationKinds.LastCallTelegraph)return false;float u=age-start;if(u<0||u>.5f)return false;
            radius=.15f+1.3f*Sm(u/.5f);alpha=1-u/.5f;return true;
        }
        /// <summary>A buffed add's flash (its seams red-white once, at the whistle) and its trail's strength over the buff.</summary>
        public static float AboardFlash(float sinceWhistle)=>float.IsNaN(sinceWhistle)||sinceWhistle<0||sinceWhistle>.45f?0:1-Sm(sinceWhistle/.45f);
        public static float AboardTrail(float sinceWhistle)=>float.IsNaN(sinceWhistle)||sinceWhistle<0||sinceWhistle>NativeStationKinds.AboardSeconds?0:Sm(sinceWhistle/.25f)*(1-Sm((sinceWhistle-(NativeStationKinds.AboardSeconds-.6f))/.6f));
        /// <summary>Glass shard i of the shattered lantern (or the window): offset, its size, its spin; out after .9 s.</summary>
        public static bool Shard(uint seed,int i,float age,out Vector3 off,out float size,out float spin)
        {
            off=Vector3.Zero;size=spin=0;if(age<0||age>.9f)return false;double a=Hf(seed,i,331)*Math.PI*2;float v=1.2f+1.8f*Hf(seed,i,332),up=1+1.6f*Hf(seed,i,333);
            float tt=Math.Min(age,.55f);off=new Vector3((float)Math.Cos(a)*v*tt,up*tt-4.9f*tt*tt,(float)Math.Sin(a)*v*tt);if(off.Y<-.2f)off.Y=-.2f;size=.015f+.02f*Hf(seed,i,334);spin=age*(8+10*Hf(seed,i,335));return true;
        }

        // ---- the Undertow ----
        /// <summary>Ripple ring k (of `count`, one every `period` s) `time` s into a ripple stream: its radius and alpha.</summary>
        public static bool Ripple(int k,float time,float period,float reach,out float radius,out float alpha)
        {
            radius=alpha=0;if(float.IsNaN(time)||time<0)return false;float age=time-k*period;if(age<0)return false;float life=period*3;age%=life;
            float u=age/life;radius=.12f+reach*(1-(1-u)*(1-u));alpha=(1-u)*Sm(age/.15f);return true;
        }
        /// <summary>Bubble i of the grab's telegraph, `u` (0..1) through it: from the Undertow toward the target's feet, rising
        /// and popping as they arrive; its place and size.</summary>
        public static bool Converge(uint seed,int i,float u,Vector3 from,Vector3 to,out Vector3 at,out float size)
        {
            at=Vector3.Zero;size=0;float start=Hf(seed,i,341)*.5f,v=(u-start)/(1-start);if(u<start||v>1)return false;
            var side=new Vector3(to.Z-from.Z,0,from.X-to.X);float l=side.Length();if(l>1e-4f)side/=l;float sw=(Hf(seed,i,342)-.5f)*.9f*(1-v);
            at=Vector3.Lerp(from,to,Sm(v))+side*sw;at.Y+=.02f+.03f*(float)Math.Sin(v*Math.PI*3+i);size=(.03f+.04f*Hf(seed,i,343))*(v>.85f?1+(v-.85f)*4:1);return true;
        }
        /// <summary>Splash drop i `age` s after the surfacing (or a fall into water): offset from the splash, size, alpha.</summary>
        public static bool Splash(uint seed,int i,float age,out Vector3 off,out float size,out float alpha)
        {
            off=Vector3.Zero;size=alpha=0;float span=.5f+.35f*Hf(seed,i,351);if(age<0||age>span)return false;double a=Hf(seed,i,352)*Math.PI*2;
            float r=.6f+1.4f*Hf(seed,i,353),up=2.2f+2.4f*Hf(seed,i,354);off=new Vector3((float)Math.Cos(a)*r*age,up*age-4.9f*age*age,(float)Math.Sin(a)*r*age);
            size=.04f+.06f*Hf(seed,i,355);alpha=1-age/span;return off.Y>-.05f;
        }
        /// <summary>Drip i off a wet body at `time`: how far it has fallen (m) from its point, its alpha; one every ~.7 s each.</summary>
        public static bool Drip(uint seed,int i,float time,out float fall,out float alpha)
        {
            fall=alpha=0;float period=.55f+.4f*Hf(seed,i,361),t=(time+period*Hf(seed,i,362))%period;if(t>.45f)return false;fall=4.9f*t*t+.02f;alpha=1-t/.45f;return true;
        }

        // ---- the Pileup ----
        /// <summary>Dust puff i of the shove, `age` s from its strike, in its 2.4 m 70 degree cone ahead (rig frame +z forward):
        /// offset, size, alpha.</summary>
        public static bool ShoveDust(uint seed,int i,float age,out Vector3 off,out float size,out float alpha)
        {
            off=Vector3.Zero;size=alpha=0;float span=.55f+.4f*Hf(seed,i,371);if(age<0||age>span)return false;float half=NativeStationKinds.ShoveCone*.5f*(float)Math.PI/180;
            float ang=(Hf(seed,i,372)*2-1)*half,reach=NativeStationKinds.ShoveReach*(.35f+.65f*Hf(seed,i,373))*Sm(Math.Min(1,age/.3f));
            off=new Vector3((float)Math.Sin(ang)*reach,.05f+.25f*Hf(seed,i,374)+.4f*age,(float)Math.Cos(ang)*reach);size=.35f+.55f*Hf(seed,i,375)+.6f*age;alpha=(1-age/span)*.8f;return true;
        }
        /// <summary>Chunk i of the split's burst `age` s after it: offset (thrown out and falling to the floor), size, spin.</summary>
        public static bool SplitChunk(uint seed,int i,float age,out Vector3 off,out float size,out float spin)
        {
            off=Vector3.Zero;size=spin=0;if(age<0||age>1.4f)return false;double a=Hf(seed,i,381)*Math.PI*2;float v=1+2.6f*Hf(seed,i,382),up=1.6f+2.2f*Hf(seed,i,383),tt=Math.Min(age,.8f);
            off=new Vector3((float)Math.Cos(a)*v*tt,.6f+up*tt-4.9f*tt*tt,(float)Math.Sin(a)*v*tt);if(off.Y<0)off.Y=0;size=.04f+.07f*Hf(seed,i,384);spin=tt*(6+9*Hf(seed,i,385));return true;
        }
        /// <summary>The window's dying tube: a flicker 0..1 by the clock and, for .4 s after a hit, its stutter.</summary>
        public static float WindowFlicker(uint seed,float time,float sinceHit)
        {
            float k=.82f+.18f*(float)Math.Sin(time*7.3f+seed%13);int f=(int)(time*12);if(Hf(seed,f,391)<.06f)k*=.25f;
            if(!float.IsNaN(sinceHit)&&sinceHit>=0&&sinceHit<.4f)k*=((int)(sinceHit*30)&1)==0?.15f:1.3f;return k;
        }
    }
}
