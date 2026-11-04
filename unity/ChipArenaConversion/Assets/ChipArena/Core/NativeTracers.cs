using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>Whose round a tracer is, from the viewer's seat: its own, a friend's or a foe's. In a free-for-all every other pilot's
    /// round is simply a round (Other): only teams and co-op have friends to tell from foes.</summary>
    public enum NativeTracerSide:byte{Own,Other,Ally,Enemy}

    /// <summary>
    /// A round's drawn path, newest last, the tracer's memory: every place the round was drawn (and any corner it turned), so its tail
    /// lies along where it really went, a ballistic arc, a homing curve, a ricochet's corner, not a straight line back along its
    /// velocity. A fixed ring (no allocation): a round's drawn positions alone (host, guest or killcam), read by nothing but the
    /// effects. Cosmetic only.
    /// </summary>
    public sealed class NativeTracerPath
    {
        public const int Capacity=16;
        // Samples this close together extend one segment (the dot of the unit directions): a straight round is one segment, whatever its samples.
        public const float Straight=.9996f;
        private readonly Vector3[] points=new Vector3[Capacity];private int first,count;
        public int Count=>count;
        public Vector3 Newest=>count>0?points[(first+count-1)%Capacity]:default;
        public void Clear(){first=count=0;}
        public void Push(Vector3 at)
        {
            if(count==Capacity){first=(first+1)%Capacity;count--;}
            points[(first+count)%Capacity]=at;count++;
        }
        /// <summary>The newest sample moves (a round that has not gone a sample's worth since).</summary>
        public void MoveNewest(Vector3 at){if(count>0)points[(first+count-1)%Capacity]=at;else Push(at);}
        /// <summary>
        /// Walks back from the newest sample: skips `skip` metres, then writes up to `length` metres of the path into `into` (head
        /// first) with each point's distance from the head in `along` (from 0), merging samples that run straight. Returns how
        /// many points: below two there is nothing to draw.
        /// </summary>
        public int Walk(float skip,float length,Vector3[] into,float[] along)
        {
            if(count==0||!(length>0))return 0;var at=Newest;int n=0;float left=length,gone=Math.Max(0,skip),walked=0;
            if(gone<=0){into[0]=at;along[0]=0;n=1;}
            for(int k=count-2;k>=0&&left>1e-4f&&n<into.Length;k--)
            {
                var p=points[(first+k)%Capacity];var d=p-at;float l=d.Length();if(l<1e-5f)continue;
                if(gone>0)
                {
                    if(l<=gone){gone-=l;at=p;continue;}
                    at+=d*(gone/l);l-=gone;gone=0;d=p-at;into[0]=at;along[0]=0;n=1;
                }
                if(l>left){p=at+d*(left/l);l=left;}
                walked+=l;left-=l;
                if(n>=2)
                {
                    var a=into[n-1]-into[n-2];float la=a.Length(),dot=Vector3.Dot(a,p-into[n-1]);
                    if(dot>=Straight*la*l){into[n-1]=p;along[n-1]=walked;at=p;continue;}
                }
                into[n]=p;along[n]=walked;n++;at=p;
            }
            return n;
        }
    }

    /// <summary>The look of one round's tracer, fixed when it is first seen (its widths in metres, the tail's seconds of flight and its
    /// clamps in metres, colours linear).</summary>
    public struct NativeTracerStyle
    {
        /// <summary>The hot line and the soft sheath round it (0: none): widths and gains. Full: this round carries the sheath and
        /// the head's glint (of a machine gun's belt one round in three; the others are the thin line alone).</summary>
        public float Core,Sheath,CoreGain,SheathGain;public FxColor CoreColor,Edge;public bool Full;
        /// <summary>The tail: seconds of flight it spans, within MinLength and MaxLength metres; Skip: metres behind the head where it starts
        /// (an arrow's shaft).</summary>
        public float Tail,MinLength,MaxLength,Skip,Fade;
        /// <summary>The bright point at the head (0: none), which is what a round flying straight at or away from the eye shows.</summary>
        public float HeadSize,HeadGain;
        /// <summary>The short-lived line a very fast round leaves along its path (Life 0: none): its life (s), width (m), gain and colour.</summary>
        public float LingerLife,LingerWidth,LingerGain;public FxColor LingerColor;
    }

    /// <summary>
    /// The gun rounds' tracers (owner 2026-11-04: "fast, sharp, visible projectile trails; clear directional movement; minimal
    /// excessive glow; distinct visual identities for each weapon", "avoid making conventional bullets resemble slow-moving energy
    /// orbs"). A round is a thin hot needle along its drawn path, its head at the round's own place: no model, no halo, no ribbon.
    /// Each gun has its own: the pistol a thin warm-white line in gold; the machine gun amber, one round in three a full tracer and
    /// the rest a thin line, so a stream reads without clutter; the rifle a long cold blue-white needle that leaves a line along
    /// its whole path for a moment. The tail is seconds of flight, so a faster round is longer by itself (whatever the rules' speed,
    /// or a chip's) and a very fast one starts to leave the rifle's line. Fan the Hammer's and Overdrive's rounds take their kit's
    /// look (NativeTracerLook). A chip gives the sheath its colour and never the core, so the gun stays readable under any stack.
    /// Friends' rounds are quiet in team modes and co-op, a foe's sheath runs red. Sizes never fall under a couple of pixels
    /// (Pixels), so a round reads across the map. Effects quality and the low tier drop the sheath, the head and the line.
    /// Pure functions of a round's replicated identity and its first speed. Cosmetic only.
    /// </summary>
    public static class NativeTracers
    {
        public const int PistolCore=0xfff6e0,PistolEdge=0xffc25a,GunCore=0xffe8c0,GunEdge=0xff9a32,RifleCore=0xf2faff,RifleEdge=0x86c8ff,RifleLine=0xbfe4ff,DriveCore=0xfff4e4,DriveEdge=0xff3c0c,DriveHot=0xff6a14,HostileEdge=0xff3a24;
        /// <summary>The fewest pixels wide a core, a sheath and a head are drawn at any distance.</summary>
        public const float CorePixels=2,SheathPixels=6,HeadPixels=7;
        /// <summary>The glare under a scoped view as the rifle fires: its life (s) and colour.</summary>
        public const float ScopeGlareLife=.07f;public const int ScopeGlare=0xffd8a8;
        /// <summary>A gun round's chip modules are drawn at this share of their size (on a needle, not on a fat box).</summary>
        public const float ModuleShare=.42f;
        /// <summary>The most rounds a frame that show their chip modules.</summary>
        public const int ModuleRounds=24;
        /// <summary>Whether a round shows its chip modules at all: a machine gun's belt, only its full tracers.</summary>
        public static bool Modules(string spec,int round)=>spec!="machinegun"||round%3==0;
        /// <summary>The tail's fall-off on the first-person pilot's own rounds (above .5: the shader's tracer mode).</summary>
        public const float OwnFade=.6f;
        /// <summary>A round is on its first heading within this (cos 3.5 degrees): its tail is still one straight line.</summary>
        public const float StraightCos=.998f;
        /// <summary>The head's glint fades out close to the eye (0 within half a metre, full from 3.5 m): a round passing the camera is not a flash in the face.</summary>
        public static float HeadNear(float distance)=>Math.Max(0,Math.Min(1,(distance-.5f)/3));
        /// <summary>The tail with the Projectile trails setting off.</summary>
        public const float PlainLength=.4f;
        /// <summary>A turn this sharp in one step (cos 41 degrees) is a corner: a bounce or a reflect, not a homing curve.</summary>
        public const float TurnCos=.75f;
        /// <summary>From this speed (m/s) a round starts to leave a line whatever its gun, at most this long (s), full by Fast.</summary>
        public const float FastFrom=170,FastFull=350,FastLine=.07f;
        /// <summary>The most sheaths and heads a frame draws (the nearest-first order of the bullet list is not kept: past these the
        /// rounds are their cores alone, under a burst of chip-multiplied rounds).</summary>
        public const int MaxSheaths=40,MaxHeads=56;
        /// <summary>A sheath and a head past this distance (m) are not worth their quads.</summary>
        public const float SheathRange=90;

        private static FxColor L(int hex)=>NativeWeaponFx.Linear(hex);
        // Each gun's look, read once (a burst of chip-multiplied rounds fixes hundreds of looks a second).
        private static readonly NativeTracerStyle GunStyle=new NativeTracerStyle{Core=.02f,Sheath=.065f,CoreGain=3f,SheathGain=.55f,CoreColor=L(GunCore),Edge=L(GunEdge),Tail=.034f,MinLength=.6f,MaxLength=6,Fade=1.1f,HeadSize=.06f,HeadGain=1.4f},
            RifleStyle=new NativeTracerStyle{Core=.028f,Sheath=.09f,CoreGain=5,SheathGain=.65f,CoreColor=L(RifleCore),Edge=L(RifleEdge),Full=true,Tail=.07f,MinLength=2,MaxLength=20,Fade=1,HeadSize=.1f,HeadGain=2.4f,LingerLife=.32f,LingerWidth=.02f,LingerGain=.7f,LingerColor=L(RifleLine)},
            PistolStyle=new NativeTracerStyle{Core=.024f,Sheath=.08f,CoreGain=3.6f,SheathGain=.6f,CoreColor=L(PistolCore),Edge=L(PistolEdge),Full=true,Tail=.04f,MinLength=.6f,MaxLength=7,Fade=1.1f,HeadSize=.07f,HeadGain=1.6f};
        /// <summary>The side of a round fired from seat `firer`, seen from seat `viewer`.</summary>
        public static NativeTracerSide Side(NativeSeat viewer,NativeSeat firer,bool teams,bool cooperative)
        {
            if(viewer==null||firer==null)return NativeTracerSide.Other;if(viewer.id==firer.id)return NativeTracerSide.Own;
            if(cooperative)return NativeTracerSide.Ally;if(!teams)return NativeTracerSide.Other;return viewer.team==firer.team?NativeTracerSide.Ally:NativeTracerSide.Enemy;
        }
        /// <summary>The chip colour a round's sheath takes (the modules' own colours, NativeShotRules.Modules), one only: an element first, then
        /// what most changes where the round goes. False for a round without one.</summary>
        public static bool Tint(in NativeShotModules m,bool fire,bool poison,out FxColor tint)
        {
            int hex=fire?NativeShotRules.Fire:poison?NativeShotRules.Poison:m.Pong>0?0xff5269:m.Homing>0?0xf482d4:m.Split>0?0x96ffb7:m.Bounces>0?0xffcc49:m.Boomerang>0?0x7fffe0:m.Zigzag>0?0x96b5ff:m.Velocity>0?0x91f2ff:m.Pierces>0?0xf3ffff:m.Vacuum>0?0xd58cff:m.LifeSteal>0?0xf273b5:0;
            tint=hex!=0?L(hex):default;return hex!=0;
        }
        /// <summary>
        /// A round's look. `spec` its gun; `kit` the kit it was fired under; `round` its id (the machine gun's one in three); `speed` and
        /// `baseSpeed` its first speed and its weapon's; the chips it carries; whose it is; the effects tiers.
        /// </summary>
        public static NativeTracerStyle Of(string spec,in NativeTracerLook kit,int round,float speed,float baseSpeed,in NativeShotModules chips,bool fire,bool poison,NativeTracerSide side,float quality,bool low)
        {
            NativeTracerStyle s=spec=="machinegun"?GunStyle:spec=="rifle"?RifleStyle:PistolStyle;if(spec=="machinegun")s.Full=round%3==0;
            if(kit.Kit)
            {
                float wide=kit.Width/Math.Max(.001f,NativeTracerFx.Width(spec));s.Core*=wide;s.Sheath*=wide;s.Tail*=kit.Length;s.MaxLength*=kit.Length;s.CoreGain*=kit.Gain/NativeTracerFx.CoreGain;s.SheathGain*=kit.Sheath/.5f;
                s.Edge=L(kit.Edge);s.Full=true;
                if(spec=="machinegun"){s.Edge=FxColor.Lerp(L(DriveEdge),L(DriveHot),kit.Heat);s.CoreColor=L(DriveCore);s.HeadGain*=1.5f;s.HeadSize*=1.3f;s.Sheath*=1.25f;s.SheathGain*=1.35f;s.Tail*=1.2f;s.MaxLength*=1.2f;}
            }
            // A round faster or slower than its gun's is thinner or heavier (its length follows its speed by itself).
            if(baseSpeed>1&&speed>1){float k=(float)Math.Sqrt(baseSpeed/speed),w=Math.Max(.8f,Math.Min(1.3f,k));s.Core*=w;s.Sheath*=w;}
            float fast=NativeWeaponFx.Clamp01((speed-FastFrom)/(FastFull-FastFrom));
            if(fast>0&&s.LingerLife<FastLine*fast){s.LingerLife=FastLine*fast;s.LingerWidth=s.Core*.7f;s.LingerGain=.4f;s.LingerColor=FxColor.Lerp(s.Edge,s.CoreColor,.5f);}
            if(Tint(chips,fire,poison,out var tint)){s.Edge=FxColor.Lerp(s.Edge,tint,.8f);s.SheathGain*=1.35f;s.Sheath*=1.1f;}
            if(side==NativeTracerSide.Ally){s.CoreGain*=.6f;s.SheathGain*=.5f;s.HeadGain*=.5f;s.Sheath*=.75f;}
            else if(side==NativeTracerSide.Enemy){s.Edge=FxColor.Lerp(s.Edge,L(HostileEdge),.5f);s.SheathGain*=1.3f;}
            if(quality<.5f){s.Sheath=0;s.LingerLife=0;s.HeadSize=0;}else if(low){s.LingerLife*=.6f;if(!s.Full){s.Sheath=0;s.HeadSize=0;}}
            if(!s.Full){s.Sheath=0;s.HeadSize=0;s.CoreGain*=.85f;}
            return s;
        }
        /// <summary>The look of an arrow's trail, from NativeBowFx.TrailLook: the arrow's own colour, draw and length; its shaft is the model.</summary>
        public static NativeTracerStyle Arrow(float draw,NativeBow.Arrow kind,NativeTracerSide side,float quality,bool low)
        {
            NativeBowFx.TrailLook(draw,kind,out float length,out float width,out float gain,out var color);
            var s=new NativeTracerStyle{Core=Math.Max(.018f,width),Sheath=.05f,CoreGain=gain*1.5f,SheathGain=gain*.25f,CoreColor=FxColor.Lerp(new FxColor(color.X,color.Y,color.Z),L(NativeBowFx.TrailCore),.22f),Edge=new FxColor(color.X,color.Y,color.Z),Full=true,MinLength=length*1.2f,MaxLength=length*1.2f,Skip=NativeBow.ArrowLength*.85f,Fade=1.2f,HeadSize=.09f,HeadGain=1+gain*.5f};
            if(side==NativeTracerSide.Ally){s.CoreGain*=.6f;s.SheathGain*=.5f;s.HeadGain*=.5f;}else if(side==NativeTracerSide.Enemy){s.Edge=FxColor.Lerp(s.Edge,L(HostileEdge),.5f);s.SheathGain*=1.3f;}
            if(quality<.5f||low){s.Sheath=0;if(quality<.5f)s.HeadSize=0;}
            return s;
        }
        /// <summary>The tail's length (m) at a speed.</summary>
        public static float Length(in NativeTracerStyle s,float speed)=>Math.Max(s.MinLength,Math.Min(s.MaxLength,speed*s.Tail));
        /// <summary>A world width (m) drawn no thinner than `pixels` at `distance` (pixel: a pixel's height at 1 m).</summary>
        public static float Width(float width,float pixels,float distance,float pixel)=>Math.Max(width,pixels*pixel*Math.Max(0,distance));
        /// <summary>
        /// Where a round's old line (through `from`, heading `d0`) meets its new one (through `to`, heading `d1`): the corner of a bounce, the
        /// closest points' middle, held between `from` and `to`'s reach. `from` when the lines are parallel.
        /// </summary>
        public static Vector3 Corner(Vector3 from,Vector3 d0,Vector3 to,Vector3 d1)
        {
            float b=Vector3.Dot(d0,d1),den=1-b*b;var r=from-to;float reach=(to-from).Length()+.5f;if(den<1e-4f)return from;
            float c=Vector3.Dot(d0,r),f=Vector3.Dot(d1,r),s=(b*f-c)/den,u=(f-b*c)/den;
            s=Math.Max(0,Math.Min(reach,s));u=Math.Max(-reach,Math.Min(reach,u));return (from+d0*s+to+d1*u)*.5f;
        }
        /// <summary>A round's fading line at `age`: 0 gone, 1 fresh; a line thins as it fades (the shader).</summary>
        public static float Line(double age,float life)=>age<0||!(age<life)?0:(float)Math.Pow(1-age/life,1.5);
    }

    /// <summary>Which impact a round makes (NativeRoundImpacts).</summary>
    public enum NativeRoundImpact:byte{None,Pistol,MachineGun,Rifle,Ricochet,Fork,Body}

    /// <summary>
    /// Where a gun's round lands: per gun its own feedback, thrown along the round's rebound off the surface, so the impact says which way
    /// the shot came from: the pistol a bright star and a good handful of hot sparks; the machine gun a tiny flash and two or three
    /// sparks (a stream of them stays a stream); the rifle a sharp four-point star, a straight white spike and a tight fast spray
    /// (precision, not a burst). A ricochet throws a gold fan along its new heading, a splitting round's fork a green flick, a
    /// round into a body a small star on the hit point (its blood and flash are the body's). All small, so a close fight stays
    /// visible through them. Seeded by the round or the event, on the presentation clock. Cosmetic only.
    /// </summary>
    public static class NativeRoundImpacts
    {
        public const double Life=.42;
        private struct Look{public int Sparks;public float SpeedMin,SpeedMax,LifeMin,LifeMax,Cone,Width,Gravity,Rebound;}
        private static Look Of(NativeRoundImpact k)
        {
            switch(k)
            {
                case NativeRoundImpact.Pistol:return new Look{Sparks=7,SpeedMin=4,SpeedMax=12,LifeMin=.16f,LifeMax=.38f,Cone=.55f,Width=.014f,Gravity=7,Rebound=.7f};
                case NativeRoundImpact.MachineGun:return new Look{Sparks=3,SpeedMin=3,SpeedMax=9,LifeMin=.1f,LifeMax=.24f,Cone=.7f,Width=.011f,Gravity=7,Rebound=.55f};
                case NativeRoundImpact.Rifle:return new Look{Sparks=9,SpeedMin=10,SpeedMax=26,LifeMin=.08f,LifeMax=.2f,Cone=.26f,Width=.011f,Gravity=4,Rebound=.9f};
                case NativeRoundImpact.Ricochet:return new Look{Sparks=8,SpeedMin=8,SpeedMax=20,LifeMin=.1f,LifeMax=.28f,Cone=.3f,Width=.013f,Gravity=5,Rebound=1};
                case NativeRoundImpact.Fork:return new Look{Sparks=5,SpeedMin=4,SpeedMax=10,LifeMin=.1f,LifeMax=.2f,Cone=1,Width=.012f,Gravity=3,Rebound=0};
                case NativeRoundImpact.Body:return new Look{Sparks=3,SpeedMin=2.5f,SpeedMax=6,LifeMin=.08f,LifeMax=.16f,Cone=.8f,Width=.01f,Gravity=4,Rebound=.2f};
                default:return default;
            }
        }
        /// <summary>A gun's impact (None for anything else: its own effects land it).</summary>
        public static NativeRoundImpact Gun(string spec)=>spec=="pistol"?NativeRoundImpact.Pistol:spec=="machinegun"?NativeRoundImpact.MachineGun:spec=="rifle"?NativeRoundImpact.Rifle:NativeRoundImpact.None;
        /// <summary>Whether a machine gun's hit plays the surface's debris effect too: one in three, so a burst into a wall is not a cloud.</summary>
        public static bool Debris(string spec,long seed)=>spec!="machinegun"||Math.Abs(seed)%3==0;
        /// <summary>The sparks this impact throws.</summary>
        public static int Sparks(NativeRoundImpact k,long seed,float quality,bool low)
        {
            int n=Of(k).Sparks;if(k==NativeRoundImpact.MachineGun&&NativeWeaponFx.Hash(seed,7)<.3)n+=2;
            return quality<.5f?Math.Max(1,n/3):low?Math.Max(1,n*2/3):n;
        }
        /// <summary>The way a round rebounds off a surface: its flight mirrored about the normal, out of the surface.</summary>
        public static Vector3 Rebound(Vector3 flight,Vector3 normal)
        {
            if(flight.LengthSquared()<.25f)return normal;var r=flight-2*Vector3.Dot(flight,normal)*normal;float l=r.Length();return l>1e-4f?r/l:normal;
        }
        /// <summary>Spark k at `age`: offset from the hit point, its velocity, colour and width.</summary>
        public static bool Spark(NativeRoundImpact kind,long seed,int k,double age,Vector3 normal,Vector3 flight,out Vector3 offset,out Vector3 velocity,out FxColor color,out float width)
        {
            var l=Of(kind);offset=velocity=default;color=default;width=l.Width;double life=l.LifeMin+(l.LifeMax-l.LifeMin)*NativeWeaponFx.Hash(seed,50+k),u=age/life;if(!(u>=0&&u<1))return false;
            var scatter=NativeWeaponFx.Sphere(NativeWeaponFx.Hash(seed,60+k),NativeWeaponFx.Hash(seed,70+k));var axis=Vector3.Normalize(Rebound(flight,normal)*l.Rebound+normal*(1-l.Rebound)+new Vector3(0,1e-4f,0));
            var dir=Vector3.Normalize(axis+scatter*l.Cone);if(Vector3.Dot(dir,normal)<.05f)dir=Vector3.Normalize(dir+normal*(.1f-Vector3.Dot(dir,normal)));
            double speed=l.SpeedMin+(l.SpeedMax-l.SpeedMin)*NativeWeaponFx.Hash(seed,80+k),damp=3.2,travel=speed*(1-Math.Exp(-damp*age))/damp;
            offset=dir*(float)travel+new Vector3(0,-(float)(.5*l.Gravity*age*age),0);velocity=dir*(float)(speed*Math.Exp(-damp*age))+new Vector3(0,-(float)(l.Gravity*age),0);
            if(normal.Y>.7f&&offset.Y<0){offset.Y=-offset.Y*.3f;velocity.Y=Math.Abs(velocity.Y)*.3f;}
            float g=(float)(1-u);
            switch(kind)
            {
                case NativeRoundImpact.Rifle:color=FxColor.Lerp(new FxColor(1,1,1),L(NativeTracers.RifleEdge),(float)u*1.3f)*(.6f+3.4f*g);break;
                case NativeRoundImpact.Ricochet:color=FxColor.Lerp(new FxColor(1,1,1),L(NativeImpactCues.RicochetColor),Math.Min(1,(float)u*2.5f))*(.6f+3f*g);break;
                case NativeRoundImpact.Fork:color=FxColor.Lerp(new FxColor(1,1,1),L(NativeImpactCues.SplitColor),Math.Min(1,(float)u*3))*(.5f+2.2f*g);break;
                default:color=NativeWeaponFx.Cooling(u)*(float)(1+2.6*g);break;
            }
            return true;
        }
        /// <summary>The quick glow on the point: size (m) and colour; a body's and the machine gun's are small.</summary>
        public static bool Flash(NativeRoundImpact kind,double age,out float size,out FxColor color)
        {
            float big;double life;FxColor c;
            switch(kind)
            {
                case NativeRoundImpact.Pistol:big=.36f;life=.07;c=L(0xffc98a)*1.7f;break;
                case NativeRoundImpact.MachineGun:big=.2f;life=.05;c=L(0xffb060)*1.3f;break;
                case NativeRoundImpact.Rifle:big=.42f;life=.06;c=L(0xc8e6ff)*2.4f;break;
                case NativeRoundImpact.Ricochet:big=.34f;life=.07;c=L(NativeImpactCues.RicochetColor)*2;break;
                case NativeRoundImpact.Fork:big=.3f;life=.07;c=L(NativeImpactCues.SplitColor)*1.8f;break;
                case NativeRoundImpact.Body:big=.16f;life=.05;c=L(0xffe6c8)*1.6f;break;
                default:size=0;color=default;return false;
            }
            double u=age/life;size=0;color=default;if(!(u>=0&&u<1))return false;float k=(float)(1-u);size=big*(.7f+.3f*k);color=c*(k*k);return true;
        }
        /// <summary>The bright star (the Sparkle sprite): size (m), roll (seeded) and colour. The rifle's is the sharp one; the machine gun has none.</summary>
        public static bool Star(NativeRoundImpact kind,long seed,double age,out float size,out float roll,out FxColor color)
        {
            float big;double life;FxColor c;
            switch(kind)
            {
                case NativeRoundImpact.Pistol:big=.32f;life=.06;c=L(0xfff0d8)*2.6f;break;
                case NativeRoundImpact.Rifle:big=.95f;life=.09;c=L(0xe8f6ff)*4;break;
                case NativeRoundImpact.Ricochet:big=.5f;life=.07;c=L(0xfff2c0)*3;break;
                case NativeRoundImpact.Fork:big=.42f;life=.08;c=L(0xd8ffe6)*2.6f;break;
                case NativeRoundImpact.Body:big=.2f;life=.05;c=L(0xfff4e8)*2;break;
                default:size=roll=0;color=default;return false;
            }
            double u=age/life;size=roll=0;color=default;if(!(u>=0&&u<1))return false;float k=(float)(1-u);size=big*(.6f+.4f*k);roll=(float)(NativeWeaponFx.Hash(seed,3)*Math.PI);color=c*k;return true;
        }
        /// <summary>The straight spike thrown along the rebound (the rifle's, a ricochet's): its length (m), width (m) and colour.</summary>
        public static bool Spike(NativeRoundImpact kind,double age,out float length,out float width,out FxColor color)
        {
            length=width=0;color=default;float big;double life;FxColor c;
            if(kind==NativeRoundImpact.Rifle){big=1.3f;life=.07;c=L(0xeef8ff)*3.4f;width=.028f;}else if(kind==NativeRoundImpact.Ricochet){big=.8f;life=.06;c=L(0xfff2c0)*2.6f;width=.024f;}else return false;
            double u=age/life;if(!(u>=0&&u<1))return false;float k=(float)(1-u);length=big*(.35f+.65f*(float)Math.Sqrt(u));color=c*k;return true;
        }
        /// <summary>How many dust puffs hang where it landed.</summary>
        public static int Puffs(NativeRoundImpact kind,float quality,bool low)=>quality<.5f||low?0:kind==NativeRoundImpact.Rifle?2:kind==NativeRoundImpact.Pistol?1:0;
        /// <summary>Puff k: offset along the normal, rising, its size and premultiplied coverage.</summary>
        public static bool Puff(long seed,int k,double age,Vector3 normal,out Vector3 offset,out float size,out float cover)
        {
            double u=age/Life;offset=default;size=cover=0;if(!(u>=0&&u<1))return false;
            float h=(float)NativeWeaponFx.Hash(seed,90+k);offset=normal*(float)(.05+.4*(.5+.5*h)*(1-Math.Exp(-age*7)))+new Vector3(0,(float)(.25*age),0);size=.08f+.3f*(float)Math.Sqrt(u);cover=.16f*NativeWeaponFx.Smooth(u/.1)*(float)(1-u)*(float)(1-u);return true;
        }
        private static FxColor L(int hex)=>NativeWeaponFx.Linear(hex);
    }
}
