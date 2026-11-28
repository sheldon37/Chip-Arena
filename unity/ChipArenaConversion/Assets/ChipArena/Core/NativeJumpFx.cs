using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The air jump's burst, first the dagger's (owner 2026-10-12: "Daggers can double-jump"), since the movement chips every
    /// weapon's (NativeAirJumpLook: the per-weapon kind and the per-charge step): where an air jump is detected
    /// (NativeJumpTrack), a small air burst under the feet and an accent-coloured afterimage puff along the body. Every part
    /// is upright or round, never a flat disc: the burst is a camera-facing ring with short streaks flaring out and down
    /// from under the feet, kept FloorClear above the floor the pilot jumped from (it never touches the floor); the
    /// afterimage is a column of soft accent puffs where the body was, drifting up as they fade. Pure functions of the burst's
    /// age, centre and seed in Unity world space (host, guests, killcam and spectators draw the same frame); Reduced effects
    /// and Low graphics thin the counts (Count). Presentation only: nothing reads back into the match. Runtime/
    /// NativeJumpFxPresentation draws it through the shared effects renderer.
    /// </summary>
    public static class NativeJumpFx
    {
        /// <summary>Lifetimes (s): the whole burst, its ring, its streaks and the afterimage puffs.</summary>
        public const float Life=.5f,RingLife=.28f,StreakLife=.24f,PuffLife=.45f;
        /// <summary>The burst centre's drop below the feet and its least height above the floor (m).</summary>
        public const float Below=.14f,FloorClear=.15f;
        /// <summary>Full and Reduced effects counts (Low graphics halves either).</summary>
        public const int Streaks=12,ReducedStreaks=5,Puffs=3,ReducedPuffs=1;
        /// <summary>The look: ring and streak HDR gain over the accent, the streak width (m) and the puff opacity.</summary>
        public const float RingGain=2.2f,StreakGain=2.6f,StreakWidth=.045f,PuffOpacity=.45f,PuffGain=1.3f;
        public static int Count(int full,int reduced,float quality,bool low){int n=quality<.5f?reduced:full;return low?Math.Max(1,(n+1)/2):n;}
        /// <summary>The burst centre for feet at `feet` (Unity world, the presented root) over a floor at height `floor`.</summary>
        public static Vector3 Centre(Vector3 feet,float floor)=>new Vector3(feet.X,Math.Max(feet.Y-Below,floor+FloorClear),feet.Z);
        private static float Out(float u)=>1-(1-u)*(1-u)*(1-u);
        /// <summary>The ring: a camera-facing ring round the centre, growing from .3 to 1.15 m wide and fading. `scale`: a chained
        /// charge's smaller pop (NativeAirJumpLook.Scale; 1, the dagger's, is exact).</summary>
        public static bool Ring(float age,out float size,out float alpha,float scale=1)
        {
            size=alpha=0;float u=age/RingLife;if(!(u>=0&&u<1))return false;
            size=(.3f+.85f*Out(u))*scale;alpha=(float)Math.Pow(1-u,1.5);return true;
        }
        private static float Hash(int seed,int k){uint h=(uint)(seed*73856093)^(uint)(k*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216f;}
        /// <summary>
        /// Streak k of n: out from the centre round a golden-angle fan, tilted .25-.6 down, decelerating over StreakLife;
        /// `head` its leading end, `dir` its unit direction, `length` its trailing length (m), `alpha` its fade. Its head and
        /// tail stay FloorClear above `floor`. `scale` shortens a chained charge's reach and trail (1 is exact).
        /// </summary>
        public static bool Streak(int k,int n,int seed,float age,Vector3 centre,float floor,out Vector3 head,out Vector3 dir,out float length,out float alpha,float scale=1)
        {
            head=centre;dir=Vector3.UnitX;length=alpha=0;float u=age/StreakLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=k*2.39996323f+Hash(seed,k)*.6f,down=.25f+.35f*Hash(seed,k+31);dir=Vector3.Normalize(new Vector3((float)Math.Cos(a),-down,(float)Math.Sin(a)));
            float reach=.55f+.25f*Hash(seed,k+57),travel=reach*Out(u);head=centre+dir*((.08f+travel)*scale);length=Math.Min(travel,.12f+.28f*(1-u))*scale;
            float low=floor+FloorClear;if(head.Y<low)head.Y=low;if(head.Y-dir.Y*length<low&&dir.Y<0)length=Math.Max(0,(head.Y-low)/Math.Max(1e-3f,-dir.Y));
            alpha=(1-u)*(1-u);return length>1e-3f;
        }
        /// <summary>
        /// Afterimage puff k of n: where the body was at the air jump (`feet` the presented root then), from the knees to the
        /// shoulders, drifting up and fading over PuffLife, growing from .3 to .6 m (times `scale`, 1 exact).
        /// </summary>
        public static bool Puff(int k,int n,int seed,float age,Vector3 feet,out Vector3 at,out float size,out float opacity,float scale=1)
        {
            at=feet;size=opacity=0;float u=age/PuffLife;if(!(u>=0&&u<1)||n<=0)return false;
            float h=n==1?.95f:.55f+.75f*k/(n-1),side=(Hash(seed,k+101)-.5f)*.16f,ahead=(Hash(seed,k+211)-.5f)*.16f;
            at=feet+new Vector3(side,h+.3f*u,ahead);size=(.3f+.3f*Out(u))*scale;opacity=PuffOpacity*(float)Math.Pow(1-u,1.3)*Math.Min(1,age/.03f);return true;
        }
    }
}
