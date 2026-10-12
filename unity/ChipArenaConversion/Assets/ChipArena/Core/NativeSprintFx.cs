using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The sprint effect (owner 2026-10-12: "And a sprint effect to know when we're sprinting", "Make sprint effect visible in
    /// first person too"): pure functions of the sprint weight (NativeSprintRules), a seed and the presentation clock, so host,
    /// guests, killcam and spectators draw the same thing. Third person: accent speed streaks trailing off the body and small
    /// dust kicks at each footfall (camera-facing particles only, never a disc on the floor). First person: speed lines at the
    /// screen edges and a soft edge vignette, tinted by the weapon and the pilot's accent (with the FOV kick, camera bob and
    /// view-weapon carry of NativeSprintRules and NativeViewSway). Reduced effects and Low graphics thin and dim it (Count,
    /// Dim). Presentation only. Runtime/NativeSprintFxPresentation draws it.
    /// </summary>
    public static class NativeSprintFx
    {
        // ---- third person ----
        /// <summary>Streak anchors in the pilot's frame (x right, y up from the feet, z forward): shoulders, hips, head, back.</summary>
        public static readonly Vector3[] Anchors={new Vector3(.32f,1.35f,-.05f),new Vector3(-.32f,1.35f,-.05f),new Vector3(.2f,.95f,-.1f),new Vector3(-.2f,.95f,-.1f),new Vector3(0,1.62f,-.1f),new Vector3(0,1.15f,-.2f)};
        public const int Streaks=6,ReducedStreaks=3,DustPuffs=3,ReducedDustPuffs=1,Lines=14,ReducedLines=6;
        /// <summary>A streak's cycle (s), trailing distance and length range (m), HDR gain and width (m).</summary>
        public const float StreakCycle=.32f,StreakTrail=.9f,StreakLength=.45f,StreakGain=1.8f,StreakWidth=.035f;
        /// <summary>A dust kick's life (s), size range (m), drift back and up (m), opacity and colour (the slam dust's warm grey).</summary>
        public const float DustLife=.5f,DustSize=.22f,DustGrow=.3f,DustBack=.6f,DustRise=.25f,DustOpacity=.45f;public const int DustColor=0xb2a89b;
        public static int Count(int full,int reduced,float quality,bool low){int n=quality<.5f?reduced:full;return low?Math.Max(1,(n+1)/2):n;}
        /// <summary>The effect's brightness for the quality (Reduced effects dims it to .6).</summary>
        public static float Dim(float quality)=>quality<.5f?.6f:1;
        private static float Hash(int seed,int k){uint h=(uint)(seed*73856093)^(uint)(k*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216f;}
        /// <summary>
        /// Streak k at `time` for a pilot at `feet` facing `forward` (unit, horizontal) moving along `travel` (unit, horizontal),
        /// sprinting at `weight`: its leading end `head`, its trailing vector `tail` (from head back along the travel) and its
        /// fade `alpha`. Each streak sheds from its anchor, drifts back and fades within StreakCycle, staggered by its seed.
        /// </summary>
        public static bool Streak(int k,int seed,float time,float weight,Vector3 feet,Vector3 forward,Vector3 travel,out Vector3 head,out Vector3 tail,out float alpha)
        {
            head=feet;tail=Vector3.Zero;alpha=0;if(weight<=1e-3f||k<0||k>=Anchors.Length)return false;
            float off=Hash(seed,k),u=(float)((time/StreakCycle+off)%1.0);if(u<0)u+=1;
            Vector3 right=Vector3.Normalize(new Vector3(forward.Z,0,-forward.X)),a=Anchors[k];
            head=feet+right*a.X+Vector3.UnitY*a.Y+forward*a.Z-travel*(.12f+StreakTrail*u);
            tail=-travel*(StreakLength*(.6f+.4f*(1-u)));alpha=weight*(float)Math.Sin(Math.PI*u)*.85f;return alpha>1e-3f;
        }
        /// <summary>Dust puff k of a kick born `age` seconds ago at `foot`, the pilot then moving along `travel`.</summary>
        public static bool Dust(int k,int seed,float age,Vector3 foot,Vector3 travel,out Vector3 at,out float size,out float opacity)
        {
            at=foot;size=opacity=0;float u=age/DustLife;if(!(u>=0&&u<1))return false;
            Vector3 side=new Vector3(travel.Z,0,-travel.X)*((Hash(seed,k)-.5f)*.25f);float o=1-(1-u)*(1-u);
            at=foot+side-travel*(DustBack*o*(.7f+.3f*Hash(seed,k+7)))+Vector3.UnitY*(.04f+DustRise*u);size=DustSize+DustGrow*o;opacity=DustOpacity*(float)Math.Pow(1-u,1.5)*Math.Min(1,age/.04f);return true;
        }

        // ---- first person (screen space: a line's radius is a fraction of the screen's half extent along its direction from
        // the centre, so 1 is the screen edge in every direction) ----
        /// <summary>A line's cycle (s), its start and travel along the radius, its length, its peak alpha and its width (pixels
        /// at 1080 p); the vignette's peak alpha and how dark its tint is.</summary>
        public const float LineCycle=.28f,LineFrom=.67f,LineTravel=.4f,LineLength=.14f,LineAlpha=.6f,LineWidth=3,VignetteAlpha=.4f,VignetteShade=.3f;
        /// <summary>
        /// Speed line k at `time`: its direction from the screen centre (`angle`, radians), the radial fractions it spans
        /// (`inner` to `outer`, of the half extent: always in the outer third, so the aim stays clear) and its `alpha`.
        /// </summary>
        public static bool Line(int k,int seed,float time,float weight,out float angle,out float inner,out float outer,out float alpha)
        {
            angle=inner=outer=alpha=0;if(weight<=1e-3f)return false;
            float off=Hash(seed,k);double cycle=time/LineCycle+off;int round=(int)Math.Floor(cycle);float u=(float)(cycle-round);
            angle=k*2.39996323f+Hash(seed^round,k+13)*.5f;inner=LineFrom+LineTravel*u;outer=inner+LineLength*(.5f+u);alpha=weight*LineAlpha*(float)Math.Sin(Math.PI*u);return alpha>1e-3f;
        }
        /// <summary>The edge vignette's alpha at the sprint weight (dimmed with Reduced effects).</summary>
        public static float Vignette(float weight,float quality)=>MeleeMath.Clamp(weight,0,1)*VignetteAlpha*Dim(quality);
        /// <summary>The vignette's shape at the screen point (u, v in 0..1): 0 inside an ellipse over the middle of the screen
        /// (the aim and most of the view stay clear), rising smoothly to 1 at the corners.</summary>
        public static float VignetteShape(float u,float v)
        {
            float x=(u-.5f)*2,y=(v-.5f)*2,r=(float)Math.Sqrt(x*x+y*y)/1.41421356f,t=MeleeMath.Clamp((r-.45f)/.55f,0,1);return t*t*(3-2*t);
        }
        /// <summary>A speed line's shape along (a, 0..1 from inner to outer) and across (b, 0..1) it: tapered at both ends.</summary>
        public static float LineShape(float a,float b)
        {
            float along=(float)Math.Pow(Math.Max(0,Math.Sin(Math.PI*MeleeMath.Clamp(a,0,1))),.7),across=1-(float)Math.Pow(Math.Abs(b*2-1),2);return along*Math.Max(0,across);
        }
        /// <summary>The first-person tint: the weapon's hue (WeaponTint) mixed half and half with the pilot's accent, linear.</summary>
        public static Vector3 Tint(string spec,string accent)=>Vector3.Lerp(NativePortraitRules.Linear(WeaponTint(spec)),NativeGlowRules.ArmorTint(accent),.5f);
        public static int WeaponTint(string spec)
        {
            switch(spec)
            {
                case "sword":return 0xdfe8ff;case "daggers":return 0xc59cff;case "shield":return 0xffd479;case "greatsword":return 0xff9c5a;
                case "pistol":return 0x9fe6ff;case "rifle":return 0x8cffc9;case "machinegun":return 0xffb35a;
                case "fire":return 0xff7a3a;case "water":return 0x5ac8ff;case "lightning":return 0xfff27a;case "darkness":return 0xb06cff;case "leet":return 0x7aff9b;
                default:return NativeSprintTag.SprintColor;
            }
        }
    }
}
