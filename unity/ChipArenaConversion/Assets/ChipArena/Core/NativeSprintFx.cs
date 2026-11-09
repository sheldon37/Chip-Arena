using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The sprint effect (owner 2026-10-12: "And a sprint effect to know when we're sprinting", "Make sprint effect visible in
    /// first person too"): pure functions of the sprint weight (NativeSprintRules), a seed and the presentation clock, so host,
    /// guests, killcam and spectators draw the same thing. Third person: accent speed streaks trailing off the body and small
    /// dust kicks at each footfall (camera-facing particles only, never a disc on the floor), and a greatsword's dragged point
    /// throwing faint scrape sparks off the floor (Scrape). The high-quality pass (owner 2026-11-09: "lets make the sprint effect
    /// look better (alot better, high quality effects)"): ribbons of light trailing off the hands and feet (Trail*), pale wind
    /// sweeping back past the body (Wind), a burst as the sprint starts (Burst*: a ring of air behind the body, dust thrown out
    /// round the feet, streaks shot back), and heavier footfalls (more dust, grit kicked back, a flash at the foot). First
    /// person: speed lines at the
    /// screen edges and a soft edge vignette, tinted by the weapon and the pilot's accent (with the FOV kick, camera bob and
    /// view-weapon carry of NativeSprintRules and NativeViewSway). Reduced effects and Low graphics thin and dim it (Count,
    /// Dim). Presentation only. Runtime/NativeSprintFxPresentation draws it.
    /// </summary>
    public static class NativeSprintFx
    {
        // ---- third person ----
        /// <summary>Streak anchors in the pilot's frame (x right, y up from the feet, z forward): shoulders, hips, head, back.</summary>
        public static readonly Vector3[] Anchors={new Vector3(.32f,1.35f,-.05f),new Vector3(-.32f,1.35f,-.05f),new Vector3(.2f,.95f,-.1f),new Vector3(-.2f,.95f,-.1f),new Vector3(0,1.62f,-.1f),new Vector3(0,1.15f,-.2f)};
        public const int Streaks=6,ReducedStreaks=3,DustPuffs=4,ReducedDustPuffs=2,Lines=22,ReducedLines=9;
        /// <summary>A streak's cycle (s), trailing distance and length range (m), HDR gain and width (m).</summary>
        public const float StreakCycle=.32f,StreakTrail=.9f,StreakLength=.7f,StreakGain=2.2f,StreakWidth=.03f;
        /// <summary>A dust kick's life (s), size range (m), drift back and up (m), opacity and colour (the slam dust's warm grey).</summary>
        public const float DustLife=.6f,DustSize=.26f,DustGrow=.44f,DustBack=.85f,DustRise=.3f,DustOpacity=.5f;public const int DustColor=0xb2a89b;
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

        // ---- the limb trails (third person) ----
        /// <summary>The rig joints that trail light while sprinting: the hands, then the feet (with Reduced effects or Low graphics
        /// the hands only). A trail is the joint's path over its life (s; the feet's shorter), sampled every TrailStep s, its
        /// width at the head (m; the feet's narrower) tapering to a fifth at its tail, its HDR gain and how much its head whitens.</summary>
        public static readonly int[] TrailJoints={CapturedRig.LHand,CapturedRig.RHand,CapturedRig.LFoot,CapturedRig.RFoot};
        public const int TrailSamples=16,ReducedTrailJoints=2;
        public const float TrailLife=.24f,FootTrailLife=.15f,TrailStep=.018f,TrailWidth=.06f,FootTrailWidth=.035f,TrailGain=2.4f,TrailWhite=.3f;
        public static bool Foot(int joint)=>joint==CapturedRig.LFoot||joint==CapturedRig.RFoot;
        public static float TrailLifeOf(int joint)=>Foot(joint)?FootTrailLife:TrailLife;
        public static float TrailWidthOf(int joint)=>Foot(joint)?FootTrailWidth:TrailWidth;
        /// <summary>The joints that trail at the quality (Reduced effects or Low graphics: the hands only).</summary>
        public static int TrailCount(float quality,bool low)=>quality<.5f||low?ReducedTrailJoints:TrailJoints.Length;
        /// <summary>A trail's strength at a sample `age` s old over its `life`: 1 at the head, easing out to 0.</summary>
        public static float TrailFade(float age,float life)=>age<0||!(age<life)?0:(float)Math.Pow(1-age/life,1.5);
        /// <summary>Its width there (m) from the head's: a fifth of it at the tail.</summary>
        public static float TrailWidthAt(float strength,float head)=>head*(.2f+.8f*MeleeMath.Clamp(strength,0,1));

        // ---- the wind (third person) ----
        /// <summary>Pale streaks of air sweeping back past the body, wrapped round its sides and shoulders (none across the face):
        /// Winds of them (ReducedWinds; Low graphics halves either), each born WindAhead m in front and gone WindBehind m behind
        /// within WindCycle s, bowing out where it passes the body; its length (m), width (m), peak alpha and HDR gain.</summary>
        public const int Winds=12,ReducedWinds=5;
        public const float WindCycle=.24f,WindAhead=.55f,WindBehind=1.6f,WindLength=.8f,WindWidth=.012f,WindAlpha=.55f,WindGain=1.6f;
        /// <summary>
        /// Wind streak k at `time` for a pilot at `feet` facing `forward` (unit, horizontal), sprinting at `weight`: its leading end
        /// `head`, its trailing vector `tail` (level, back along the facing) and its `alpha`. Stateless: the same on every client.
        /// </summary>
        public static bool Wind(int k,int seed,float time,float weight,Vector3 feet,Vector3 forward,out Vector3 head,out Vector3 tail,out float alpha)
        {
            head=feet;tail=Vector3.Zero;alpha=0;if(weight<=1e-3f||k<0)return false;
            double cycle=time/WindCycle+Hash(seed,k+40);int round=(int)Math.Floor(cycle);float u=(float)(cycle-round);
            float a=Hash(seed^round,k+41),b=Hash(seed^round,k+43),c=Hash(seed^round,k+47);
            Vector3 right=Vector3.Normalize(new Vector3(forward.Z,0,-forward.X));
            // Round the body on its own side (alternating): from the hips up over the shoulder, on an ellipse taller than wide.
            float side=k%2==0?1:-1,phi=(-.35f+1.25f*a)*(float)Math.PI*.5f,r=.42f+.3f*b;
            float z=WindAhead-(WindAhead+WindBehind)*u,bow=1+.22f*(float)Math.Exp(-z*z*4);
            float x=side*r*(float)Math.Cos(phi)*bow,y=1.05f+.88f*(float)Math.Sin(phi);
            head=feet+right*x+Vector3.UnitY*y+forward*z;tail=-forward*(WindLength*(.55f+.45f*c));
            alpha=weight*WindAlpha*(float)Math.Sin(Math.PI*u)*(.6f+.4f*c);return alpha>1e-3f;
        }

        // ---- the start burst (third person) ----
        /// <summary>As a sprint starts (from standing or walking): a ring of air popping out behind the body at the chest, dust
        /// thrown out round the feet (BurstPuffs, ReducedBurstPuffs), streaks shot back off the body (BurstStreaks,
        /// ReducedBurstStreaks); all gone within BurstLife s. A sprint picked up again within BurstRest s makes none.</summary>
        public const int BurstPuffs=8,ReducedBurstPuffs=4,BurstStreaks=10,ReducedBurstStreaks=5;
        public const float BurstLife=.55f,BurstRest=.6f,BurstRingLife=.32f,BurstRingSize=2.6f,BurstGain=1.5f,BurstStreakLife=.3f;
        /// <summary>An ease out (fast, then settling).</summary>
        public static float Ease(float u){u=MeleeMath.Clamp(u,0,1);float v=1-u;return 1-v*v*v;}
        /// <summary>The ring of air `age` s into the burst: its size (m across) and strength (0..1).</summary>
        public static bool BurstRing(float age,out float size,out float strength)
        {
            size=strength=0;float u=age/BurstRingLife;if(!(u>=0&&u<1))return false;
            size=.5f+BurstRingSize*Ease(u);strength=(1-u)*(1-u)*Math.Min(1,age/.03f);return strength>1e-3f;
        }
        /// <summary>Burst puff k of n `age` s in, for a pilot that started at `feet` facing `forward`: thrown out round the feet,
        /// mostly back and to the sides, growing and thinning.</summary>
        public static bool BurstPuff(int k,int n,int seed,float age,Vector3 feet,Vector3 forward,out Vector3 at,out float size,out float opacity)
        {
            at=feet;size=opacity=0;float u=age/BurstLife;if(!(u>=0&&u<1)||n<=0)return false;
            Vector3 right=Vector3.Normalize(new Vector3(forward.Z,0,-forward.X));
            float t=n>1?k/(float)(n-1):.5f,angle=(float)Math.PI*(1+(t-.5f)*1.5f)+(Hash(seed,k+60)-.5f)*.35f;
            Vector3 dir=forward*(float)Math.Cos(angle)+right*(float)Math.Sin(angle);float o=Ease(u);
            at=feet+dir*(.2f+(.9f+.5f*Hash(seed,k+61))*o)+Vector3.UnitY*(.08f+.22f*u);size=.28f+.62f*o;
            opacity=.5f*(float)Math.Pow(1-u,1.3)*Math.Min(1,age/.04f);return true;
        }
        /// <summary>Burst streak k `age` s in: shot back and out off the body from the chest, its head, motion (m/s: the streak
        /// trails along it) and alpha.</summary>
        public static bool BurstStreak(int k,int seed,float age,Vector3 feet,Vector3 forward,out Vector3 head,out Vector3 motion,out float alpha)
        {
            head=feet;motion=Vector3.Zero;alpha=0;float u=age/BurstStreakLife;if(!(u>=0&&u<1))return false;
            Vector3 right=Vector3.Normalize(new Vector3(forward.Z,0,-forward.X));
            float around=(Hash(seed,k+70)*2-1)*(float)Math.PI*.55f,up=(Hash(seed,k+71)-.35f)*1.1f;
            Vector3 dir=Vector3.Normalize(-forward*1.4f+right*(float)Math.Sin(around)+Vector3.UnitY*up);float speed=7+5*Hash(seed,k+72);
            head=feet+Vector3.UnitY*(.7f+.8f*Hash(seed,k+73))+dir*(.25f+speed*BurstStreakLife*.45f*Ease(u));motion=dir*speed*(1-u*.7f);
            alpha=(1-u)*(1-u);return alpha>1e-3f;
        }

        // ---- the footfall's grit and flash (third person) ----
        /// <summary>Grit kicked back and up off each footfall (Grits, ReducedGrits), falling and coming to rest on the floor
        /// within GritLife s, about GritSize m; and a flash at the foot as it strikes (FlashLife s, FlashSize m, FlashGain).</summary>
        public const int Grits=3,ReducedGrits=1;public const int GritColor=0x5f574e;
        public const float GritLife=.42f,GritSize=.03f,GritGravity=9.8f,FlashLife=.12f,FlashSize=.42f,FlashGain=1.1f;
        /// <summary>Grit k of a kick born `age` s ago at `foot` (the floor at `floor`), the pilot moving along `travel`: where it is,
        /// its size (m) and its spin (radians).</summary>
        public static bool Grit(int k,int seed,float age,Vector3 foot,Vector3 travel,float floor,out Vector3 at,out float size,out float spin)
        {
            at=foot;size=spin=0;float u=age/GritLife;if(!(u>=0&&u<1))return false;
            Vector3 side=new Vector3(travel.Z,0,-travel.X);
            Vector3 v=-travel*(1.2f+1.4f*Hash(seed,k+80))+side*((Hash(seed,k+81)-.5f)*1.6f)+Vector3.UnitY*(1.4f+1.3f*Hash(seed,k+82));
            at=foot+v*age-Vector3.UnitY*(.5f*GritGravity*age*age);if(at.Y<floor+.012f)at.Y=floor+.012f;
            size=GritSize*(.6f+.8f*Hash(seed,k+83))*Math.Min(1,(1-u)*4);spin=age*(9+8*Hash(seed,k+84));return size>1e-4f;
        }
        /// <summary>The footfall flash's strength `age` s after the foot strikes.</summary>
        public static float Flash(float age){if(!(age>=0&&age<FlashLife))return 0;float v=1-age/FlashLife;return v*v;}

        // ---- the greatsword's drag (third person; Core/NativeSprintMotion.DragBlade, owner 2026-10-15) ----
        /// <summary>Scrape sparks off the dragged greatsword's point: Scrapes of them (ReducedScrapes with Reduced effects; Low
        /// graphics halves either), each kicked back and up off the floor where the point was at most ScrapeLife ago and falling,
        /// a short hot streak (the bisect sparks' colour, linear HDR) cooling as it goes. Only while the rig shows the drag, and
        /// scaled down as the point lifts (ScrapeContact).</summary>
        public const int Scrapes=5,ReducedScrapes=2;
        public const float ScrapeLife=.16f,ScrapeKick=1.4f,ScrapeRise=1.6f,ScrapeSpread=.6f,ScrapeGravity=9.8f,ScrapeWidth=.014f,ScrapeReach=.2f;
        public static readonly Vector3 ScrapeColor=new Vector3(4.2f,1.75f,.45f);
        /// <summary>How far down the dragged point is, from its height over the feet (m): 1 at the floor guard's height
        /// (about .11 m), easing to 0 at ScrapeReach.</summary>
        public static float ScrapeContact(float overFeet)=>1-MeleeMath.SmoothStep(overFeet,ScrapeReach*.65f,ScrapeReach);
        /// <summary>
        /// Spark k at `time` off the point at `tip` (world) of a pilot moving at `velocity` (world; the point moves with it), at
        /// `weight` (the sprint's, times ScrapeContact): its leading end `head` (never under `floor` + 1 cm), its world motion
        /// `motion` (m/s: the streak trails along it) and its `heat` (fades as (1 - age)^2). Stateless: the same on every client.
        /// </summary>
        public static bool Scrape(int k,int seed,float time,float weight,Vector3 tip,Vector3 velocity,float floor,out Vector3 head,out Vector3 motion,out float heat)
        {
            head=tip;motion=Vector3.Zero;heat=0;if(weight<=1e-3f||k<0)return false;
            double cycle=time/ScrapeLife+Hash(seed,k);int round=(int)Math.Floor(cycle);float u=(float)(cycle-round),age=u*ScrapeLife;
            float a=Hash(seed^round,k+3),b=Hash(seed^round,k+7)-.5f,c=Hash(seed^round,k+11);
            Vector3 level=new Vector3(velocity.X,0,velocity.Z);float speed=level.Length();Vector3 travel=speed>.1f?level/speed:Vector3.Zero,side=new Vector3(travel.Z,0,-travel.X);
            Vector3 kick=-travel*(ScrapeKick*(.4f+.6f*a))+side*(ScrapeSpread*b)+Vector3.UnitY*(ScrapeRise*(.5f+.5f*c));
            head=tip-level*age+kick*age-Vector3.UnitY*(.5f*ScrapeGravity*age*age);if(head.Y<floor+.01f)head.Y=floor+.01f;
            motion=kick-Vector3.UnitY*(ScrapeGravity*age);heat=MeleeMath.Clamp(weight,0,1)*(1-u)*(1-u);return heat>1e-3f;
        }

        // ---- first person (screen space: a line's radius is a fraction of the screen's half extent along its direction from
        // the centre, so 1 is the screen edge in every direction) ----
        /// <summary>A line's cycle (s), its start and travel along the radius, its length, its peak alpha and its width (pixels
        /// at 1080 p); the vignette's peak alpha and how dark its tint is.</summary>
        public const float LineCycle=.28f,LineFrom=.67f,LineTravel=.4f,LineLength=.2f,LineAlpha=.65f,LineWidth=2.6f,VignetteAlpha=.4f,VignetteShade=.3f;
        /// <summary>Line k's width against LineWidth: some hairlines, some heavier, so the lines read as streaming air.</summary>
        public static float LineWidthOf(int k)=>.5f+1.1f*Hash(7919,k+60);
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
