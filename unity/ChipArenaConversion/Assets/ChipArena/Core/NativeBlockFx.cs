using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ChipArena.UnityConversion
{
    /// <summary>What one authoritative Block event stands for in the block effect (presentation only).</summary>
    public enum NativeBlockHit : byte { Clash, Deflect }
    /// <summary>The guard's silhouette, keyed off the blocker's weapon: sword a slim arc, greatsword a tall wide arc, daggers twin
    /// small arcs, shield a large hex dome segment; every other class (guns, staffs, fighters) a plain ward in front of the chest.</summary>
    public enum NativeGuardForm : byte { Arc, Tall, Twin, Hex, Ward }
    /// <summary>One guard form's proportions (metres and radians, third-person size; the first-person view scales them).</summary>
    public struct NativeGuardShape
    {
        public NativeGuardForm Form;
        /// <summary>Radius of the arc (or dome) about a pivot behind its centre; Span its horizontal angle; Height the band's
        /// height at its middle (Hex: the dome's vertical angle).</summary>
        public float Radius,Span,Height;
        /// <summary>How far ahead of the anchor the guard's centre sits; Bow how much the band's top and bottom lean back toward
        /// the pilot; Tilt the largest roll that follows the blade; Anchor the blend from the chest (0) to the blade middle (1);
        /// TwinYaw each dagger arc's outward turn and Spread its outward shift (m); RingMax the clash ring's final radius.</summary>
        public float Forward,Bow,Tilt,Anchor,TwinYaw,Spread,RingMax;
        /// <summary>The band's taper exponent along the arc (<see cref="NativeBlockFxShapes.Crescent"/>): low is a pointed
        /// crescent, high a band that keeps its height nearly to the tips.</summary>
        public float Taper;
        public int Columns,Rows;
    }
    /// <summary>Where a guard sits: its centre, facing (Forward, away from the pilot) and up, the roll that follows the blade and
    /// the size (1 third person, <see cref="NativeBlockFx.FirstPersonScale"/> on the view weapon).</summary>
    public struct NativeGuardFrame{public Vector3 Centre,Forward,Up;public float Tilt,Scale;}
    /// <summary>A clash's spray frame: the blade segment (Base to Tip), the contact point on it, the horizontal direction to the
    /// attacker (Toward), world up (sparks fall along -Up), the size, the clash ring's final radius and how far toward the
    /// attacker the ring stands off the contact (Lead, m; it clears the weapon).</summary>
    public struct NativeClashFrame{public Vector3 Base,Tip,Contact,Toward,Up;public float Scale,Ring,Lead;}
    /// <summary>
    /// A blocker as the presentation sees it this frame: the chest (body reference), facing and up (third person the rig's yaw
    /// and world up; first person the view camera's axes), the blade segments (Base0-Tip0 the right hand, Base1-Tip1 the
    /// left, daggers only; HasBlade false for guns, staffs and fists) and the size (1, or the first-person scale).
    /// </summary>
    /// <remarks>View: the first-person view weapon. Its guard stands as an arch over the blade (bent in the screen plane and
    /// leaning <see cref="NativeBlockFx.ViewArch"/> away from the eye, so its curve reads instead of a flat strip seen edge-on)
    /// and its clash ring is <see cref="NativeBlockFx.ViewRing"/> of the third-person size.</remarks>
    public struct NativeGuardPose{public Vector3 Chest,Forward,Up,Base0,Tip0,Base1,Tip1;public bool HasBlade,Twin,View;public float Scale;}
    /// <summary>The camera a camera-facing quad is built for: its position and up axis.</summary>
    public struct NativeBlockEye{public Vector3 Position,Up;}
    /// <summary>A short presentation-only camera kick (degrees and metres, camera-local).</summary>
    public struct NativeBlockHitch{public float Pitch,Roll,Back;}

    /// <summary>
    /// One vertex of the block effect's single dynamic mesh (Resources/NativeBlockFx.shader). Every quad carries its world
    /// position; the shader shades it by Mode:
    ///  - Arc (0) / Hex (1), the guard: U along the arc (-1..1, the tips at +-1), V across it (-1..1, the rim at +-1; Hex: the
    ///    dome's elliptic outline at |UV| = 1); Alpha its fade; Flare the white clash flash; Crack the crack fade (1 fresh, 0
    ///    gone); Time the shimmer clock; Cu, Cv the contact in guard UV; Mu, Mv metric coordinates (m) for the scanlines and cells.
    ///  - Ring (2), the clash shock ring: UV the quad (-1..1); Flare the ring radius and Crack its width (both in quad halves);
    ///    Time its normalized age.
    ///  - Streak (3), a spark or the deflect streak: U tail (0) to head (1), V across (-1..1); Flare its heat (1 white-hot);
    ///    Crack its taper; Mu its length over its width.
    ///  - Glow (4), the contact flash and the ping: UV the quad; Flare heat; Crack star strength (0 a soft glow); Time the
    ///    star's rotation.
    /// R, G, B are the linear guard colour (brightest channel 1); A the strength (first-person subtlety, Reduced effects).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeBlockVertex
    {
        public float X,Y,Z;
        public float R,G,B,A;
        public float U,V,Mode,Seed;
        public float Alpha,Flare,Crack,Time;
        public float Cu,Cv,Mu,Mv;
    }

    /// <summary>
    /// The block effect's pure rules (owner request 2026-10-11 "add a new block effect"; design: docs/MELEE-ABILITIES.md "The new
    /// block effect"). Presentation only: nothing here is read by the authority, the network frame, hit timing, hitboxes,
    /// damage, cooldowns or movement, and it never touches Time.timeScale.
    /// <list type="bullet">
    /// <item>Guard: while a pilot's replicated block window is open (render time before BlockUntil; guests open it at
    /// BlockUntil - NativeMeleeRules.BlockDuration(stats), <see cref="OpenAt"/>), an energy ward in the weapon's colour unfolds in
    /// front of the weapon (<see cref="Wipe"/>, <see cref="GuardAlpha"/>) and fades out at the window's end.</item>
    /// <item>Clash: a Block event that <see cref="Classify"/> calls a melee blow flares the guard white and cracks it
    /// (<see cref="Flare"/>, <see cref="CrackFade"/>, held visible <see cref="HoldSeconds"/> past a late block), throws an HDR spark
    /// spray along the blade (<see cref="Spark"/>), a white star with needles at the contact and an upright shock ring facing the
    /// attacker that expands and fades in <see cref="RingSeconds"/>, and kicks the blocker's camera for <see cref="HitchSeconds"/>.</item>
    /// <item>Deflect: a reflected projectile gets a bright streak along its new direction and a small spark ping.</item>
    /// </list>
    /// Clash or deflect (until the simulation adds a Block Detail of guard/clash/deflect, <see cref="Classify"/>): the event's
    /// Target is the attacker; a melee-class attacker within its reach (+<see cref="ReachSlack"/>) clashes, an unknown attacker (a
    /// PvE add's swipe has no seat) clashes, anything else deflects. A Detail of "clash" or "deflect" overrides the guess, and
    /// "guard" (the window-open notice) draws no hit.
    /// </summary>
    public static class NativeBlockFx
    {
        public const float ModeArc=0,ModeHex=1,ModeRing=2,ModeStreak=3,ModeGlow=4;
        public const string DetailGuard="guard",DetailClash="clash",DetailDeflect="deflect";
        // ---- timing (seconds) ----
        public const float OpenSeconds=.07f,CloseSeconds=.08f,FlareSeconds=.09f,CrackSeconds=.18f,HoldSeconds=.2f,RingSeconds=.18f;
        public const float SparkLifeMin=.16f,SparkLifeMax=.34f,NeedleSeconds=.11f,DeflectSeconds=.16f,PingSeconds=.12f,HitchSeconds=.05f;
        /// <summary>A Block event older than this when first seen draws nothing (a late joiner, a resumed view).</summary>
        public const float EventWindow=.4f;
        /// <summary>How long a clash or deflect record lives (its longest part).</summary>
        public const float RecordSeconds=.42f;
        // ---- sizes, speeds and gains ----
        public const float SparkSpeedMin=4f,SparkSpeedMax=11f,SparkDrag=3f,SparkGravity=8f,SparkBlur=.036f,SparkMinLength=.1f,SparkWidth=.045f;
        public const float NeedleLength=.46f,NeedleWidth=.055f,FlashSize=.42f;
        public const float DeflectSpeed=30f,DeflectLength=1.9f,DeflectWidth=.09f,PingSize=.42f;
        public const float HitchPitch=.7f,HitchRoll=.35f,HitchBack=.018f;
        public const float FirstPersonScale=.32f,FirstPersonStrength=.5f,FirstPersonHitStrength=.8f,ViewArch=.9f,ViewRing=.6f,ReachSlack=1.25f,DeflectStrength=.55f;
        public const int SparksFull=26,SparksMin=6,Needles=6,PingSparks=6,MaxRecords=16;
        /// <summary>The crescent's tip height (fraction of the middle height), so the band never collapses to a line.</summary>
        public const float CrescentTip=.08f;

        // ---- forms, shapes and colour ----
        /// <summary>The guard form for a blocker: its source spec, else (the prototype lab) its visual set, a gun its ward.</summary>
        public static NativeGuardForm FormOf(string spec,int visualSet,bool ranged)
        {
            switch(spec)
            {
                case "sword":return NativeGuardForm.Arc;
                case "greatsword":return NativeGuardForm.Tall;
                case "daggers":return NativeGuardForm.Twin;
                case "shield":return NativeGuardForm.Hex;
            }
            if(!string.IsNullOrEmpty(spec)||ranged)return NativeGuardForm.Ward;
            return visualSet==1?NativeGuardForm.Twin:visualSet==2?NativeGuardForm.Tall:visualSet==3?NativeGuardForm.Hex:NativeGuardForm.Arc;
        }
        public static NativeGuardShape Shape(NativeGuardForm form)
        {
            switch(form)
            {
                case NativeGuardForm.Tall:return new NativeGuardShape{Form=form,Taper=2.2f,Radius=.78f,Span=2.4f,Height=1.35f,Forward=.2f,Bow=.3f,Tilt=.2f,Anchor=.35f,RingMax=1.05f,Columns=28,Rows=8};
                case NativeGuardForm.Twin:return new NativeGuardShape{Form=form,Taper=1.5f,Radius=.4f,Span=1.6f,Height=.24f,Forward=.15f,Bow=.2f,Tilt=.5f,Anchor=1f,TwinYaw=.3f,Spread=.14f,RingMax=.56f,Columns=14,Rows=3};
                case NativeGuardForm.Hex:return new NativeGuardShape{Form=form,Taper=3f,Radius=1f,Span=2f,Height=1.5f,Forward=.18f,Bow=0,Tilt=0,Anchor=.5f,RingMax=1f,Columns=22,Rows=16};
                case NativeGuardForm.Ward:return new NativeGuardShape{Form=form,Taper=3f,Radius=.6f,Span=1.8f,Height=.5f,Forward=.6f,Bow=.3f,Tilt=0,Anchor=0,RingMax=.7f,Columns=20,Rows=4};
                default:return new NativeGuardShape{Form=NativeGuardForm.Arc,Taper=2.6f,Radius=.66f,Span=2.1f,Height=.36f,Forward=.3f,Bow=.28f,Tilt=.55f,Anchor=.6f,RingMax=.78f,Columns=24,Rows=4};
            }
        }
        /// <summary>The first-person size of a form's guard (the view weapon is a hand's length from the eye): the big tall arc
        /// and hex dome shrink further so the view stays clear.</summary>
        public static float ViewScale(NativeGuardForm form)=>form==NativeGuardForm.Tall||form==NativeGuardForm.Hex?FirstPersonScale*.7f:form==NativeGuardForm.Ward?FirstPersonScale*.9f:FirstPersonScale;
        /// <summary>The first-person body reference in view-camera space (x right, y up, z forward): below and just ahead of the
        /// eye; a bladeless ward hangs from a point further out, so its arch sits low in the view, clear of the crosshair.</summary>
        public static Vector3 ViewChest(NativeGuardForm form)=>form==NativeGuardForm.Ward?new Vector3(0,-.2f,.3f):new Vector3(0,-.26f,.12f);
        /// <summary>The weapon colour when no loadout is at hand (GameContent.json weapons; the prototype lab's sword set).</summary>
        public static int DefaultColor(string spec,int visualSet)
        {
            switch(spec)
            {
                case "sword":return 0xb6dfd7;case "daggers":return 0x96ecc5;case "shield":return 0x8cbee6;case "greatsword":return 0xeac8a0;
                case "emperor":return 0xff304f;case "ansatsuken":return 0x80dfff;case "siren":return 0x8e65cc;
            }
            if(string.IsNullOrEmpty(spec))return visualSet==1?0x96ecc5:visualSet==2?0xeac8a0:visualSet==3?0x8cbee6:0xb6dfd7;
            return 0x8fe8ff;
        }
        /// <summary>three's sRGB hex to linear (Color.setHex under ColorManagement).</summary>
        public static Vector3 Linear(int hex)
        {
            float C(int v){float c=v/255f;return c<=.04045f?c/12.92f:(float)Math.Pow((c+.055)/1.055,2.4);}
            return new Vector3(C((hex>>16)&255),C((hex>>8)&255),C(hex&255));
        }
        /// <summary>The neon guard colour: the weapon colour in linear light with its chroma pushed (the content colours are pale
        /// pastels) and its brightest channel at 1 (the shader adds the HDR gain). A grey keeps its hue (white).</summary>
        public static Vector3 GuardColor(int hex)
        {
            var c=Linear(hex);float lum=.2126f*c.X+.7152f*c.Y+.0722f*c.Z;const float push=2.2f;
            c=Vector3.Max(Vector3.Zero,new Vector3(lum)+(c-new Vector3(lum))*push);float m=Math.Max(c.X,Math.Max(c.Y,c.Z));
            return m>1e-4f?c/m:Vector3.One;
        }

        // ---- timing curves ----
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Smooth(float v){v=Clamp01(v);return v*v*(3-2*v);}
        private static float OutCubic(float v){v=1-Clamp01(v);return 1-v*v*v;}
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>When this block window opened: the authoritative BlockAt when it belongs to the window ending at
        /// <paramref name="blockUntil"/>; otherwise (guests, whose frame carries no blockAt, and the killcam) BlockUntil minus the
        /// window length NativeMeleeRules.BlockDuration(stats).</summary>
        public static float OpenAt(float blockAt,float blockUntil,float duration)
        {
            duration=Math.Max(0,duration);
            return blockAt<=blockUntil&&blockAt>=blockUntil-duration-.02f?blockAt:blockUntil-duration;
        }
        /// <summary>How far the guard has unfolded from its middle (0..1, eased) since the window opened.</summary>
        public static float Wipe(float time,float openAt)=>time<openAt?0:OutCubic((time-openAt)/OpenSeconds);
        /// <summary>The guard's fade inside its window: a quick fade-in and a <see cref="CloseSeconds"/> fade-out before the end.</summary>
        public static float GuardAlpha(float time,float openAt,float end)
        {
            if(!(time>=openAt)||!(time<end))return 0;
            return Math.Min(Clamp01((time-openAt)/(OpenSeconds*.6f)),Smooth((end-time)/CloseSeconds));
        }
        /// <summary>The white clash flash: full at the event, gone after <see cref="FlareSeconds"/>.</summary>
        public static float Flare(float age){if(!(age>=0)||age>=FlareSeconds)return 0;float k=1-age/FlareSeconds;return k*k;}
        /// <summary>The crack's fade: 1 at the event, 0 after <see cref="CrackSeconds"/> (the shader grows the cracks from it).</summary>
        public static float CrackFade(float age)=>!(age>=0)||age>=CrackSeconds?0:1-age/CrackSeconds;
        /// <summary>How far the cracks have run out from the contact at a crack fade (the shader's BlockFxCracks: .25 at once,
        /// full after 50 ms).</summary>
        public static float CrackReach(float fade)=>.25f+.75f*Clamp01((1-fade)*CrackSeconds/.05f);
        /// <summary>The guard stays visible through a clash for this much (a block landing at the window's last frame still flares).</summary>
        public static float Hold(float age)=>!(age>=0)||age>=HoldSeconds?0:1-Smooth(age/HoldSeconds);
        public static float RingRadius(float u,float max)=>Lerp(.1f,max,OutCubic(u));
        public static float RingWidth(float u,float max)=>max*Lerp(.16f,.035f,Clamp01(u));
        public static float RingAlpha(float u)=>!(u>=0)||u>=1?0:(float)Math.Pow(1-u,1.4)*Math.Min(1,.35f+u/.08f);
        /// <summary>The blocker's camera kick at a clash's <paramref name="age"/>: up and back with a little roll away from the
        /// attacker's side (<paramref name="side"/> -1..1), 10 ms in and out by <see cref="HitchSeconds"/>. scale: the settings
        /// scale (0 with camera shake off). False when nothing applies. A kick, not a freeze: a held mouse-look camera reads as
        /// input lag, and the aim point is taken before it.</summary>
        public static bool Hitch(float age,float side,float scale,out NativeBlockHitch kick)
        {
            kick=default;if(!(scale>0)||!(age>=0)||age>=HitchSeconds)return false;scale=Math.Min(1,scale);
            float e=age<.01f?age/.01f:1-Smooth((age-.01f)/(HitchSeconds-.01f));
            kick.Pitch=-HitchPitch*e*scale;kick.Roll=HitchRoll*Math.Max(-1,Math.Min(1,side))*e*scale;kick.Back=HitchBack*e*scale;return true;
        }

        // ---- clash or deflect ----
        /// <summary>The future Block Detail (Astra): "clash" or "deflect" names the hit; "guard" is the window-open notice.</summary>
        public static bool FromDetail(string detail,out NativeBlockHit hit)
        {
            hit=detail==DetailDeflect?NativeBlockHit.Deflect:NativeBlockHit.Clash;return detail==DetailClash||detail==DetailDeflect;
        }
        /// <summary>A Block event that draws no hit (the window-open notice).</summary>
        public static bool Notice(string detail)=>detail==DetailGuard;
        /// <summary>Clash or deflect for one Block event; see the class summary. distance: blocker to attacker (m); reach: the
        /// attacker's melee reach (weapon range x reach stat).</summary>
        public static NativeBlockHit Classify(string detail,bool attackerKnown,bool attackerMelee,float distance,float reach)
        {
            if(FromDetail(detail,out var hit))return hit;
            if(!attackerKnown)return NativeBlockHit.Clash;
            return attackerMelee&&distance<=Math.Max(0,reach)+ReachSlack?NativeBlockHit.Clash:NativeBlockHit.Deflect;
        }
        /// <summary>A melee attacker's reach: the weapon range times the reach stat (1 when absent or invalid).</summary>
        public static float Reach(float range,float reachStat)=>Math.Max(0,range)*(reachStat>0&&!float.IsInfinity(reachStat)?reachStat:1);

        // ---- deterministic randoms ----
        private static uint Hash(int seed,int i,int k)
        {
            unchecked{uint h=(uint)seed*0x9E3779B1u^(uint)i*0x85EBCA6Bu^(uint)k*0xC2B2AE35u;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return h;}
        }
        /// <summary>A uniform value in [0, 1) from a seed, an index and a channel (the same on host, guests and the killcam).</summary>
        public static float Rand(int seed,int i,int k)=>(Hash(seed,i,k)&0xffffff)/16777216f;
        /// <summary>A Block event's seed (its id).</summary>
        public static int Seed(int eventId)=>unchecked(eventId*7919+17);
        public static int SparkCount(float quality,bool low)
        {
            int n=(int)Math.Round(SparksFull*Clamp01(quality));if(low)n=n*3/5;return Math.Max(SparksMin,Math.Min(SparksFull,n));
        }
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f?v/l:fallback;}
        /// <summary>Where along the blade (0 base .. 1 tip) a blow from <paramref name="toward"/> meets it: the blade point
        /// nearest the ray from <paramref name="point"/> toward the attacker, kept off the very ends.</summary>
        public static float ContactOnBlade(Vector3 bladeBase,Vector3 tip,Vector3 point,Vector3 toward)
        {
            Vector3 d=tip-bladeBase;float dd=Vector3.Dot(d,d);if(dd<1e-8f)return .5f;
            Vector3 probe=point+Unit(toward,Vector3.Zero)*.35f;return Math.Max(.15f,Math.Min(.85f,Vector3.Dot(probe-bladeBase,d)/dd));
        }
        /// <summary>One clash spark at <paramref name="age"/>: born along the blade around the contact, thrown back toward the
        /// attacker, up and along the edge with drag and gravity. u is its normalized age; false once it has burnt out.</summary>
        public static bool Spark(in NativeClashFrame f,int seed,int i,float age,out Vector3 head,out Vector3 velocity,out float u)
        {
            float life=Lerp(SparkLifeMin,SparkLifeMax,Rand(seed,i,0));u=age/life;head=velocity=Vector3.Zero;if(!(age>=0)||u>=1)return false;
            Vector3 up=Unit(f.Up,Vector3.UnitY),blade=f.Tip-f.Base,along=Unit(blade,up),toward=Unit(f.Toward,Vector3.UnitZ),side=Unit(Vector3.Cross(up,toward),Vector3.UnitX);
            float c=ContactOnBlade(f.Base,f.Tip,f.Contact,toward),t=Clamp01(c+(Rand(seed,i,1)-.5f)*.5f);
            Vector3 start=f.Base+blade*t;
            Vector3 dir=Unit(toward*(.25f+.55f*Rand(seed,i,2))+up*(.2f+.8f*Rand(seed,i,3))+along*((Rand(seed,i,4)-.5f)*1.4f)+side*((Rand(seed,i,5)-.5f)*1.1f),up);
            float speed=Lerp(SparkSpeedMin,SparkSpeedMax,Rand(seed,i,6))*f.Scale,decay=(float)Math.Exp(-SparkDrag*age);
            Vector3 v0=dir*speed,g=-up*(SparkGravity*f.Scale);
            head=start+v0*((1-decay)/SparkDrag)+g*(.5f*age*age);velocity=v0*decay+g*age;return true;
        }
        /// <summary>The guard's centre: as deep as the blade middle plus the form's Forward (in front of the weapon), its side and
        /// height blended from the chest toward the blade by the form's Anchor (a held-across sword keeps the ward centred).</summary>
        public static Vector3 GuardCentre(Vector3 chest,Vector3 bladeMiddle,Vector3 forward,Vector3 up,in NativeGuardShape s,float scale)
        {
            Vector3 fw=Unit(forward,Vector3.UnitZ),u=Unit(up-fw*Vector3.Dot(up,fw),Vector3.UnitY),right=Unit(Vector3.Cross(u,fw),Vector3.UnitX),d=bladeMiddle-chest;
            return chest+fw*(Math.Max(0,Vector3.Dot(d,fw))+s.Forward*(scale>0?scale:1))+right*(Vector3.Dot(d,right)*s.Anchor)+u*(Vector3.Dot(d,u)*s.Anchor);
        }
        /// <summary>The guard's roll following the blade: the blade's angle across the facing (either end), at most max.</summary>
        public static float GuardTilt(Vector3 blade,Vector3 forward,Vector3 up,float max)
        {
            Vector3 fw=Unit(forward,Vector3.UnitZ),u=Unit(up-fw*Vector3.Dot(up,fw),Vector3.UnitY),right=Unit(Vector3.Cross(u,fw),Vector3.UnitX);
            float x=Vector3.Dot(blade,right),y=Vector3.Dot(blade,u);if(x*x+y*y<1e-8f||!(max>0))return 0;
            double a=Math.Atan2(y,x);if(a>Math.PI/2)a-=Math.PI;else if(a<-Math.PI/2)a+=Math.PI;return (float)Math.Max(-max,Math.Min(max,a));
        }
        /// <summary>A dagger arc's facing: the pilot's facing turned outward toward its side (side +1 the right hand) by yaw.</summary>
        public static Vector3 TwinForward(Vector3 forward,Vector3 up,float side,float yaw)
        {
            Vector3 fw=Unit(forward,Vector3.UnitZ),u=Unit(up-fw*Vector3.Dot(up,fw),Vector3.UnitY),right=Unit(Vector3.Cross(u,fw),Vector3.UnitX);float a=Math.Sign(side)*yaw;
            return fw*(float)Math.Cos(a)+right*(float)Math.Sin(a);
        }
        /// <summary>The first-person arch's axes from the view camera's: its facing (from the pivot to the arch's middle) leans
        /// <see cref="ViewArch"/> from the camera's up toward its forward; its across axis points back toward the eye and up.</summary>
        public static void ViewAxes(Vector3 cameraForward,Vector3 cameraUp,out Vector3 forward,out Vector3 up)
        {
            Vector3 f=Unit(cameraForward,Vector3.UnitZ),u=Unit(cameraUp-f*Vector3.Dot(cameraUp,f),Vector3.UnitY);float c=(float)Math.Cos(ViewArch),s=(float)Math.Sin(ViewArch);
            forward=u*c+f*s;up=u*s-f*c;
        }
        /// <summary>The guard frame(s) for a pose: one, or two for the daggers (index 1 the left hand). False when there is no such
        /// frame (index 1 of a single guard).</summary>
        public static bool Frame(in NativeGuardPose p,in NativeGuardShape s,int index,out NativeGuardFrame f)
        {
            f=default;float scale=p.Scale>0?p.Scale:1;bool twin=s.Form==NativeGuardForm.Twin&&p.Twin&&p.HasBlade;if(index>1||index==1&&!twin)return false;
            Vector3 b=index==1?p.Base1:p.Base0,t=index==1?p.Tip1:p.Tip0,mid=p.HasBlade?(b+t)*.5f:p.Chest,fw=Unit(p.Forward,Vector3.UnitZ),up=Unit(p.Up,Vector3.UnitY);
            if(p.View)ViewAxes(fw,up,out fw,out up);
            if(twin){Vector3 right=Unit(Vector3.Cross(up,fw),Vector3.UnitX);float side=Vector3.Dot(mid-p.Chest,right);if(side==0)side=index==1?-1:1;mid+=right*(Math.Sign(side)*s.Spread*scale);fw=TwinForward(fw,up,side,s.TwinYaw);}
            f=new NativeGuardFrame{Centre=GuardCentre(p.Chest,mid,fw,up,s,scale),Forward=fw,Up=up,Tilt=p.HasBlade?GuardTilt(t-b,fw,up,s.Tilt):0,Scale=scale};return true;
        }
        /// <summary>A clash's spray frame for a pose: the blade nearer the attacker (or, bladeless, a short span across the ward's
        /// front), the contact where the blow meets it, nudged toward the attacker, and the form's ring size.</summary>
        public static NativeClashFrame ClashFrame(in NativeGuardPose p,in NativeGuardShape s,Vector3 point,Vector3 toward,Vector3 worldUp)
        {
            float scale=p.Scale>0?p.Scale:1;Vector3 up=Unit(worldUp,Vector3.UnitY),dir=Unit(toward-up*Vector3.Dot(toward,up),Unit(p.Forward,Vector3.UnitZ));Vector3 b,t;
            if(p.HasBlade)
            {
                b=p.Base0;t=p.Tip0;
                if(s.Form==NativeGuardForm.Twin&&p.Twin){Vector3 right=Unit(Vector3.Cross(up,Unit(p.Forward,Vector3.UnitZ)),Vector3.UnitX);if(Vector3.Dot((p.Base1+p.Tip1)*.5f-(p.Base0+p.Tip0)*.5f,right)*Vector3.Dot(dir,right)>0){b=p.Base1;t=p.Tip1;}}
            }
            else
            {
                Frame(p,s,0,out var g);Vector3 right=Unit(Vector3.Cross(g.Up,g.Forward),Vector3.UnitX)*(.18f*scale);b=g.Centre-right;t=g.Centre+right;
            }
            float c=ContactOnBlade(b,t,point,dir);
            return new NativeClashFrame{Base=b,Tip=t,Contact=b+(t-b)*c+dir*(.06f*scale),Toward=dir,Up=up,Scale=scale,Ring=s.RingMax*(p.View?ViewRing:1),Lead=(p.View?.4f:.1f)*scale};
        }
        /// <summary>Where on the guard (guard UV) a blow from <paramref name="toward"/> lands: its angle from the guard's facing over
        /// the half span, a little above the middle (seeded).</summary>
        public static Vector2 ContactUv(in NativeGuardFrame f,in NativeGuardShape s,Vector3 toward,int seed)
        {
            Vector3 fw=Unit(f.Forward,Vector3.UnitZ),up=Unit(f.Up,Vector3.UnitY),right=Unit(Vector3.Cross(up,fw),Vector3.UnitX);
            float a=(float)Math.Atan2(Vector3.Dot(toward,right),Vector3.Dot(toward,fw)),u=a/Math.Max(.1f,s.Span*.5f);
            return new Vector2(Math.Max(-.8f,Math.Min(.8f,u)),.1f+.3f*(Rand(seed,0,9)-.5f));
        }
    }

    /// <summary>
    /// The block effect's single dynamic mesh, built on the CPU each frame from the pure rules above: guards, clash sprays and
    /// rings, deflect streaks and pings (see <see cref="NativeBlockVertex"/>). Camera-facing quads are built for the given eye.
    /// Arrays only grow; nothing allocates once warm.
    /// </summary>
    public sealed class NativeBlockMesh
    {
        public NativeBlockVertex[] Vertices=new NativeBlockVertex[512];
        public int[] Indices=new int[768];
        public int VertexCount,IndexCount;
        public void Clear(){VertexCount=0;IndexCount=0;}
        private int Reserve(int vertices,int indices)
        {
            if(VertexCount+vertices>Vertices.Length)Array.Resize(ref Vertices,Math.Max(Vertices.Length*2,VertexCount+vertices));
            if(IndexCount+indices>Indices.Length)Array.Resize(ref Indices,Math.Max(Indices.Length*2,IndexCount+indices));
            return VertexCount;
        }
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f?v/l:fallback;}
        private void Put(Vector3 p,Vector3 color,float strength,float u,float v,float mode,float seed,float alpha,float flare,float crack,float time,float cu=0,float cv=0,float mu=0,float mv=0)
        {
            Vertices[VertexCount++]=new NativeBlockVertex{X=p.X,Y=p.Y,Z=p.Z,R=color.X,G=color.Y,B=color.Z,A=strength,U=u,V=v,Mode=mode,Seed=seed,Alpha=alpha,Flare=flare,Crack=crack,Time=time,Cu=cu,Cv=cv,Mu=mu,Mv=mv};
        }
        private void Quad(int first)
        {
            Indices[IndexCount++]=first;Indices[IndexCount++]=first+1;Indices[IndexCount++]=first+2;
            Indices[IndexCount++]=first;Indices[IndexCount++]=first+2;Indices[IndexCount++]=first+3;
        }
        /// <summary>
        /// One guard: a crescent ribbon bent round its pivot (Arc, Tall, Twin's one blade, Ward) or an elliptic spherical patch
        /// (Hex). wipe: how far it has unfolded from its middle; alpha its fade; flare, crack and contact the clash on it; clock
        /// the shimmer clock; low: the low graphics tier (half the segments).
        /// </summary>
        public void Guard(in NativeGuardFrame f,in NativeGuardShape s,Vector3 color,float alpha,float wipe,float flare,float crack,float clock,Vector2 contact,int seed,float strength,bool low)
        {
            if(!(alpha>0)||!(strength>0))return;
            float sc=f.Scale>0?f.Scale:1,R=s.Radius*sc,open=Math.Max(.05f,Math.Min(1,wipe)),cos=(float)Math.Cos(f.Tilt),sin=(float)Math.Sin(f.Tilt);
            Vector3 fw=Unit(f.Forward,Vector3.UnitZ),up0=Unit(f.Up-fw*Vector3.Dot(f.Up,fw),Vector3.UnitY),right0=Unit(Vector3.Cross(up0,fw),Vector3.UnitX);
            Vector3 right=right0*cos+up0*sin,up=up0*cos-right0*sin,pivot=f.Centre-fw*R;
            int cols=Math.Max(2,low?s.Columns/2:s.Columns),rows=Math.Max(1,low?s.Rows/2:s.Rows);bool hex=s.Form==NativeGuardForm.Hex;
            float span=s.Span*open,mode=hex?NativeBlockFx.ModeHex:NativeBlockFx.ModeArc,seedF=seed&0xffff;
            int first=Reserve((cols+1)*(rows+1),cols*rows*6);
            for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
            {
                float u=-1+2f*i/cols,v=-1+2f*j/rows,a=u*span*.5f;Vector3 p;float mu,mv;
                if(hex)
                {
                    float b=v*s.Height*.5f*open,cb=(float)Math.Cos(b);
                    p=pivot+((fw*(float)Math.Cos(a)+right*(float)Math.Sin(a))*cb+up*(float)Math.Sin(b))*R;mu=a*R*cb;mv=b*R;
                }
                else
                {
                    float half=s.Height*.5f*sc*NativeBlockFxShapes.Crescent(u,s.Taper);
                    p=pivot+(fw*(float)Math.Cos(a)+right*(float)Math.Sin(a))*R+up*(v*half)-fw*(s.Bow*v*v*half);mu=a*R;mv=v*half;
                }
                Put(p,color,strength,u,v,mode,seedF,alpha,flare,crack,clock,contact.X,contact.Y,mu,mv);
            }
            for(int j=0;j<rows;j++)for(int i=0;i<cols;i++)
            {
                int k=first+j*(cols+1)+i;
                Indices[IndexCount++]=k;Indices[IndexCount++]=k+cols+1;Indices[IndexCount++]=k+cols+2;
                Indices[IndexCount++]=k;Indices[IndexCount++]=k+cols+2;Indices[IndexCount++]=k+1;
            }
        }
        /// <summary>
        /// A blocker's guard this frame: its window fade and unfold (<paramref name="openAt"/> to <paramref name="end"/>), and the
        /// newest clash or deflect on it (<paramref name="hasHit"/>, <paramref name="hitAge"/> seconds old, the attacker toward
        /// <paramref name="toward"/>): a clash flashes it white, cracks it and holds it visible past a late block; a deflect
        /// flashes it at <see cref="NativeBlockFx.DeflectStrength"/>. Returns whether anything was drawn.
        /// </summary>
        public bool GuardFor(in NativeGuardPose p,NativeGuardForm form,Vector3 color,float time,float openAt,float end,bool hasHit,NativeBlockHit hit,float hitAge,Vector3 toward,int seed,float strength,bool low)
        {
            var s=NativeBlockFx.Shape(form);float alpha=NativeBlockFx.GuardAlpha(time,openAt,end),wipe=NativeBlockFx.Wipe(time,openAt),flare=0,crack=0;
            if(hasHit)
            {
                bool clash=hit==NativeBlockHit.Clash;float k=clash?1:NativeBlockFx.DeflectStrength,hold=NativeBlockFx.Hold(hitAge)*k;
                flare=NativeBlockFx.Flare(hitAge)*k;crack=clash?NativeBlockFx.CrackFade(hitAge):0;if(hold>alpha){alpha=hold;wipe=1;}
            }
            if(!(alpha>0))return false;
            for(int index=0;index<2;index++)
            {
                if(!NativeBlockFx.Frame(p,s,index,out var f))break;
                Guard(f,s,color,alpha,wipe,flare,crack,time,NativeBlockFx.ContactUv(f,s,toward,seed),seed+index,strength,low);
            }
            return true;
        }
        /// <summary>An upright ring at <paramref name="centre"/> whose face looks along <paramref name="normal"/> (toward the
        /// attacker), at normalized age u: it expands to max and fades (the shader draws the band).</summary>
        public void Ring(Vector3 centre,Vector3 normal,Vector3 up,float max,float u,Vector3 color,float strength)
        {
            float a=NativeBlockFx.RingAlpha(u);if(!(a>0)||!(max>0)||!(strength>0))return;
            Vector3 n=Unit(normal,Vector3.UnitZ),right=Unit(Vector3.Cross(up,n),Vector3.Cross(n,Vector3.UnitX)),top=Vector3.Cross(n,right);float half=max*1.1f;
            float r=NativeBlockFx.RingRadius(u,max)/half,w=NativeBlockFx.RingWidth(u,max)/half;int first=Reserve(4,6);
            Put(centre-right*half-top*half,color,strength,-1,-1,NativeBlockFx.ModeRing,0,a,r,w,u);
            Put(centre+right*half-top*half,color,strength,1,-1,NativeBlockFx.ModeRing,0,a,r,w,u);
            Put(centre+right*half+top*half,color,strength,1,1,NativeBlockFx.ModeRing,0,a,r,w,u);
            Put(centre-right*half+top*half,color,strength,-1,1,NativeBlockFx.ModeRing,0,a,r,w,u);Quad(first);
        }
        /// <summary>A camera-facing streak from tail to head with round ends (width m); heat 1 is white-hot, taper dims the tail.</summary>
        public void Streak(Vector3 tail,Vector3 head,float width,in NativeBlockEye eye,Vector3 color,float alpha,float heat,float taper,float strength)
        {
            if(!(alpha>0)||!(width>0)||!(strength>0))return;
            Vector3 axis=head-tail;float len=axis.Length();if(len<1e-5f){Glow(head,width*2.5f,eye,color,alpha,heat,0,0,strength);return;}
            Vector3 d=axis/len,view=Unit((head+tail)*.5f-eye.Position,Vector3.UnitZ),side=Unit(Vector3.Cross(d,view),Unit(Vector3.Cross(d,eye.Up),Vector3.UnitX))*(width*.5f),cap=d*(width*.5f);
            float aspect=(len+width)/width;int first=Reserve(4,6);
            Put(tail-cap-side,color,strength,0,-1,NativeBlockFx.ModeStreak,0,alpha,heat,taper,0,0,0,aspect);
            Put(tail-cap+side,color,strength,0,1,NativeBlockFx.ModeStreak,0,alpha,heat,taper,0,0,0,aspect);
            Put(head+cap+side,color,strength,1,1,NativeBlockFx.ModeStreak,0,alpha,heat,taper,0,0,0,aspect);
            Put(head+cap-side,color,strength,1,-1,NativeBlockFx.ModeStreak,0,alpha,heat,taper,0,0,0,aspect);Quad(first);
        }
        /// <summary>A camera-facing glow of width size; star 0 is a soft glow, up to 1 adds an eight-point star turned by phase.</summary>
        public void Glow(Vector3 at,float size,in NativeBlockEye eye,Vector3 color,float alpha,float heat,float star,float phase,float strength)
        {
            if(!(alpha>0)||!(size>0)||!(strength>0))return;
            Vector3 view=Unit(at-eye.Position,Vector3.UnitZ),right=Unit(Vector3.Cross(eye.Up,view),Unit(Vector3.Cross(Vector3.UnitY,view),Vector3.UnitX)),top=Vector3.Cross(view,right);
            float h=size*.5f;int first=Reserve(4,6);
            Put(at-right*h-top*h,color,strength,-1,-1,NativeBlockFx.ModeGlow,0,alpha,heat,star,phase);
            Put(at+right*h-top*h,color,strength,1,-1,NativeBlockFx.ModeGlow,0,alpha,heat,star,phase);
            Put(at+right*h+top*h,color,strength,1,1,NativeBlockFx.ModeGlow,0,alpha,heat,star,phase);
            Put(at-right*h+top*h,color,strength,-1,1,NativeBlockFx.ModeGlow,0,alpha,heat,star,phase);Quad(first);
        }
        /// <summary>
        /// A clash at <paramref name="age"/>: the white-hot star and needles at the contact, the spark spray along the blade and,
        /// with <paramref name="ring"/>, the upright shock ring facing the attacker. quality: the effects quality (Reduced
        /// effects .3); low: the low graphics tier.
        /// </summary>
        public void Clash(in NativeClashFrame f,Vector3 color,float age,int seed,float quality,bool low,bool ring,in NativeBlockEye eye,float strength)
        {
            if(!(age>=0)||!(strength>0))return;float sc=f.Scale>0?f.Scale:1;
            float flare=NativeBlockFx.Flare(age);
            if(flare>0)Glow(f.Contact,NativeBlockFx.FlashSize*sc*(.75f+.25f*(1-flare)),eye,color,flare,1,1,NativeBlockFx.Rand(seed,0,10)*1.57f,strength);
            float nu=age/NativeBlockFx.NeedleSeconds;
            if(nu<1)
            {
                Vector3 toward=Unit(f.Toward,Vector3.UnitZ),up=Unit(f.Up,Vector3.UnitY),side=Unit(Vector3.Cross(up,toward),Vector3.UnitX);
                for(int k=0;k<NativeBlockFx.Needles;k++)
                {
                    float th=k*6.2831853f/NativeBlockFx.Needles+NativeBlockFx.Rand(seed,k,11)*.6f,reach=NativeBlockFx.NeedleLength*sc*(.6f+.4f*NativeBlockFx.Rand(seed,k,12));
                    Vector3 dir=Unit(side*(float)Math.Cos(th)+up*(float)Math.Sin(th)+toward*.35f,up);float out0=reach*(.15f+.85f*nu),in0=reach*Math.Max(0,nu*1.4f-.4f);
                    Streak(f.Contact+dir*in0,f.Contact+dir*out0,NativeBlockFx.NeedleWidth*sc*(1-.6f*nu),eye,color,1-nu*nu,1,.2f,strength);
                }
            }
            int n=NativeBlockFx.SparkCount(quality,low);
            for(int i=0;i<n;i++)
            {
                if(!NativeBlockFx.Spark(f,seed,i,age,out var head,out var velocity,out float u))continue;
                Vector3 trail=velocity*NativeBlockFx.SparkBlur;float l=trail.Length(),min=NativeBlockFx.SparkMinLength*sc;if(l<min)trail=l>1e-5f?trail*(min/l):Unit(f.Up,Vector3.UnitY)*min;
                Streak(head-trail,head,NativeBlockFx.SparkWidth*sc*(1-.45f*u),eye,color,1-u*u,1-u,.85f,strength);
            }
            if(ring&&age<NativeBlockFx.RingSeconds)Ring(f.Contact+Unit(f.Toward,Vector3.UnitZ)*Math.Max(0,f.Lead),f.Toward,f.Up,f.Ring*sc,age/NativeBlockFx.RingSeconds,color,strength);
        }
        /// <summary>A deflect at <paramref name="age"/>: a bright streak running out along the reflected direction and a small
        /// spark ping (a star and a few sparks) at the contact.</summary>
        public void Deflect(Vector3 point,Vector3 direction,Vector3 up,Vector3 color,float age,int seed,float quality,bool low,in NativeBlockEye eye,float strength,float scale=1)
        {
            if(!(age>=0)||!(strength>0))return;float sc=scale>0?scale:1;Vector3 dir=Unit(direction,Vector3.UnitZ);
            float u=age/NativeBlockFx.DeflectSeconds;
            if(u<1)
            {
                float head=Math.Max(.08f*sc,NativeBlockFx.DeflectSpeed*age*sc),tail=Math.Max(0,head-NativeBlockFx.DeflectLength*sc),a=1-Smooth01((u-.45f)/.55f);
                Streak(point+dir*tail,point+dir*head,NativeBlockFx.DeflectWidth*sc,eye,color,a,.55f,.9f,strength);
            }
            float p=age/NativeBlockFx.PingSeconds;
            if(p<1)Glow(point,NativeBlockFx.PingSize*sc*(.8f+.5f*p),eye,color,(1-p)*(1-p),1,.8f,NativeBlockFx.Rand(seed,0,13)*1.57f,strength);
            var f=new NativeClashFrame{Base=point,Tip=point,Contact=point,Toward=dir,Up=up,Scale=.6f*sc};
            int n=Math.Max(3,(int)Math.Round(NativeBlockFx.PingSparks*Math.Max(.5f,Math.Min(1,quality))*(low?.6f:1)));
            for(int i=0;i<n;i++)
            {
                if(!NativeBlockFx.Spark(f,seed+101,i,age*1.6f,out var head,out var velocity,out float su))continue;
                Vector3 trail=velocity*NativeBlockFx.SparkBlur;float l=trail.Length(),min=NativeBlockFx.SparkMinLength*.7f*sc;if(l<min)trail=l>1e-5f?trail*(min/l):dir*min;
                Streak(head-trail,head,NativeBlockFx.SparkWidth*.8f*sc,eye,color,1-su*su,1-su,.85f,strength);
            }
        }
        private static float Smooth01(float v){v=v<0?0:v>1?1:v;return v*v*(3-2*v);}
    }
    /// <summary>The crescent outline shared by the guard mesh and its checks.</summary>
    public static class NativeBlockFxShapes
    {
        /// <summary>The band's half height along the arc (u -1..1) as a fraction of its middle, sqrt(1 - |u|^taper): 1 at the
        /// middle, tapering to <see cref="NativeBlockFx.CrescentTip"/> at the tips (a low taper is a pointed crescent).</summary>
        public static float Crescent(float u,float taper){float k=1-(float)Math.Pow(Math.Min(1,Math.Abs(u)),Math.Max(1,taper));return k<=0?NativeBlockFx.CrescentTip:Math.Max(NativeBlockFx.CrescentTip,(float)Math.Sqrt(k));}
    }
}
