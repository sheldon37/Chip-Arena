using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>Linear RGB (HDR: may exceed 1) and a coverage alpha, premultiplied: 0 alpha is purely additive.</summary>
    public struct FxColor
    {
        public float R,G,B,A;
        public FxColor(float r,float g,float b,float a=0){R=r;G=g;B=b;A=a;}
        public static FxColor operator*(FxColor c,float k)=>new FxColor(c.R*k,c.G*k,c.B*k,c.A*k);
        public static FxColor Lerp(FxColor a,FxColor b,float t){t=Math.Max(0,Math.Min(1,t));return new FxColor(a.R+(b.R-a.R)*t,a.G+(b.G-a.G)*t,a.B+(b.B-a.B)*t,a.A+(b.A-a.A)*t);}
        public float Max=>Math.Max(R,Math.Max(G,B));
    }

    /// <summary>
    /// Weapon and combat-feel effects (owner 2026-10-11 effects overhaul, weapons milestone; rebuilt procedurally from the
    /// studied Epic Toon FX / Trails FX references, no pack file used): gun muzzle stars, bullet tracers and hard-surface
    /// impacts, energy projectile trails, melee slash crescents, roll afterimages, the pilot hit flash and the spell charge
    /// orb. This file holds the pure timing, shape and colour rules; Runtime/NativeWeaponFxPresentation.cs turns them into
    /// geometry (Resources/NativeWeaponFx.shader and NativeWeaponGhost.shader). Every value is a function of replicated data
    /// (shot times, swing windows, roll stamps, event ids and times, positions) and a seed derived from it, so host, guests
    /// and the killcam replay draw the same thing. Cosmetic only: nothing here is read by the authority, the hit rules, the
    /// network frame or the animation clocks.
    /// </summary>
    public static class NativeWeaponFx
    {
        /// <summary>A deterministic 0..1 value for stream index k of a seed (a SplitMix64 finaliser).</summary>
        public static double Hash(long seed,long k)
        {
            unchecked
            {
                ulong h=(ulong)seed*0x9E3779B97F4A7C15UL+(ulong)k*0xD1B54A32D192ED03UL+0x94D049BB133111EBUL;
                h^=h>>30;h*=0xBF58476D1CE4E5B9UL;h^=h>>27;h*=0x94D049BB133111EBUL;h^=h>>31;
                return (h>>11)*(1.0/9007199254740992.0);
            }
        }
        /// <summary>A seed from an authoritative timestamp (quantised to 1/4096 s, so a float that took a network round trip
        /// still lands on the same seed) and an owner id.</summary>
        public static long Seed(double time,int owner)=>(long)Math.Floor(time*4096+.5)*2654435761L+owner*40503L;
        public static float Clamp01(double v)=>(float)Math.Max(0,Math.Min(1,v));
        public static float Smooth(double v){float x=Clamp01(v);return x*x*(3-2*x);}
        public static FxColor Linear(int hex,float gain=1,float alpha=0){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new FxColor((float)r*gain,(float)g*gain,(float)b*gain,alpha);}
        /// <summary>A unit vector from two hashes (uniform on the sphere).</summary>
        public static Vector3 Sphere(double h0,double h1){double z=h0*2-1,a=h1*Math.PI*2,s=Math.Sqrt(Math.Max(0,1-z*z));return new Vector3((float)(s*Math.Cos(a)),(float)(s*Math.Sin(a)),(float)z);}
        /// <summary>Two unit vectors perpendicular to n and to each other.</summary>
        public static void Basis(Vector3 n,out Vector3 t,out Vector3 b)
        {
            n=n.LengthSquared()>1e-8f?Vector3.Normalize(n):Vector3.UnitY;var helper=Math.Abs(n.Y)<.9f?Vector3.UnitY:Vector3.UnitX;
            t=Vector3.Normalize(Vector3.Cross(helper,n));b=Vector3.Cross(n,t);
        }
        /// <summary>The metal cooling ramp of the hot sparks (white, #FF7900 at .09, #DF3C00 at .62, #180700 at 1), linear.</summary>
        public static FxColor Cooling(double u)
        {
            u=Math.Max(0,Math.Min(1,u));
            if(u<.09)return FxColor.Lerp(new FxColor(1,1,1),Linear(0xff7900),(float)(u/.09));
            if(u<.62)return FxColor.Lerp(Linear(0xff7900),Linear(0xdf3c00),(float)((u-.09)/.53));
            return FxColor.Lerp(Linear(0xdf3c00),Linear(0x180700),(float)((u-.62)/.38));
        }
    }

    /// <summary>The look of one gun's muzzle star (sizes in metres of the weapon frame; the first-person view model's own
    /// scale applies on top).</summary>
    public struct MuzzleLook
    {
        public float Life,Star,Side,SideWidth,Glow,Tilt,Gain;public int Sparks;
    }
    /// <summary>
    /// ETFX StandardMuzzle-style flash, rebuilt: three additive flame petals radiating from the muzzle in its plane (a star
    /// from the front), tilted along the barrel, rolled by the shot's seed; a side flame card along the barrel (an
    /// axis-facing billboard, so it reads from any side); a hot core glow; a few sparks in a narrow cone; and a light smoke
    /// wisp. The flash itself lasts 60-90 ms (rifle longest, machine gun shortest so its rounds strobe); the petals keep
    /// full opacity for two thirds of that and shrink to .35, as the reference. All by the replicated LastShot.
    /// </summary>
    public static class NativeMuzzleFx
    {
        public const int Petals=3,PetalColor=0xffd276,EdgeColor=0xff9f00,SparkColor=0xffff72,LightColor=0xffb46a;
        public const float LightIntensity=3.2f,LightRange=4.5f,LightLife=.15f,WispLife=.62f,SparkLifeMax=.25f;
        /// <summary>How long after the shot anything of it may still draw (the smoke wisp).</summary>
        public const double Span=.68;
        public static bool Look(string spec,out MuzzleLook look)
        {
            switch(spec)
            {
                case "pistol":look=new MuzzleLook{Life=.07f,Star=.22f,Side=.36f,SideWidth=.15f,Glow=.3f,Tilt=.7f,Gain=1,Sparks=4};return true;
                case "rifle":look=new MuzzleLook{Life=.09f,Star=.32f,Side=.6f,SideWidth=.22f,Glow=.42f,Tilt=.65f,Gain=1.25f,Sparks=6};return true;
                case "machinegun":look=new MuzzleLook{Life=.06f,Star=.25f,Side=.46f,SideWidth=.17f,Glow=.34f,Tilt=.68f,Gain=1.1f,Sparks=3};return true;
                default:look=default;return false;
            }
        }
        // Size over life 1 -> .35 (the reference curve), alpha full to 2/3 of the life, then out.
        private static float SizeOver(double u)=>(float)(1-.65*Math.Max(0,Math.Min(1,u)));
        private static float AlphaOver(double u)=>u<0||u>=1?0:u<2/3.0?1:(float)((1-u)*3);
        /// <summary>Petal k (0..2): its roll about the barrel (radians), length and width (m) and opacity at `age`.</summary>
        public static bool Petal(in MuzzleLook look,long seed,int k,double age,out float roll,out float length,out float width,out float alpha)
        {
            double u=age/look.Life;roll=length=width=alpha=0;if(!(u>=0&&u<1))return false;
            roll=(float)((NativeWeaponFx.Hash(seed,0)+k/3.0)*Math.PI*2+(NativeWeaponFx.Hash(seed,1+k)-.5)*.7);
            length=look.Star*(.8f+.4f*(float)NativeWeaponFx.Hash(seed,4+k))*SizeOver(u);width=length*.55f;alpha=AlphaOver(u);return true;
        }
        /// <summary>The side flame along the barrel: its length and width (m) and opacity.</summary>
        public static bool Side(in MuzzleLook look,long seed,double age,out float length,out float width,out float alpha)
        {
            double u=age/look.Life;length=width=alpha=0;if(!(u>=0&&u<1))return false;
            float h=(float)NativeWeaponFx.Hash(seed,8);length=look.Side*(.85f+.3f*h)*SizeOver(u);width=look.SideWidth*(.9f+.2f*h)*SizeOver(u*.6);alpha=AlphaOver(u);return true;
        }
        /// <summary>The core glow (#FFDE7C to #FF9F00 over 1.5 flash lives, HDR): size (m) and colour.</summary>
        public static bool Glow(in MuzzleLook look,double age,out float size,out FxColor color)
        {
            double u=age/(look.Life*1.5);size=0;color=default;if(!(u>=0&&u<1))return false;
            size=look.Glow*(1-.31f*(float)u);color=FxColor.Lerp(NativeWeaponFx.Linear(0xffde7c),NativeWeaponFx.Linear(0xff9f00),(float)u)*(1.2f*look.Gain*(float)(1-u)*(float)(1-u));return true;
        }
        /// <summary>Spark count at an effects quality (Reduced effects .3: about a third, at least one; low graphics: at most two).</summary>
        public static int SparkCount(in MuzzleLook look,float quality,bool low)=>Math.Max(1,Math.Min(low?2:look.Sparks,(int)Math.Round(look.Sparks*Math.Max(.2f,quality))));
        /// <summary>
        /// Spark k: barrel-frame offset (x, y across, z along the barrel) and velocity at `age`, its world drop (m, gravity .7 g)
        /// and opacity. 4-12 m/s in a 6 degree cone, .1-.25 s, damped.
        /// </summary>
        public static bool Spark(long seed,int k,double age,out Vector3 local,out Vector3 velocity,out float drop,out float alpha)
        {
            double life=.1+.15*NativeWeaponFx.Hash(seed,20+k),u=age/life;local=velocity=default;drop=alpha=0;if(!(u>=0&&u<1)||age<.01)return false;
            double t=age-.01,speed=4+8*NativeWeaponFx.Hash(seed,30+k),cone=.105*Math.Sqrt(NativeWeaponFx.Hash(seed,40+k)),a=NativeWeaponFx.Hash(seed,50+k)*Math.PI*2;
            var dir=new Vector3((float)(Math.Sin(cone)*Math.Cos(a)),(float)(Math.Sin(cone)*Math.Sin(a)),(float)Math.Cos(cone));
            double damp=1-.25*u;local=dir*(float)(.04+speed*t*damp);velocity=dir*(float)(speed*(1-.5*u));drop=(float)(.5*6.87*t*t);alpha=(float)(1-u*u);return true;
        }
        /// <summary>Smoke wisps (one on Reduced effects; none with both Reduced effects and low graphics).</summary>
        public static int WispCount(float quality,bool low)=>quality<.5f?(low?0:1):2;
        /// <summary>Wisp k: barrel-frame offset, rise (m, world up), size (m) and coverage (premultiplied, light grey).</summary>
        public static bool Wisp(long seed,int k,double age,out Vector3 local,out float rise,out float size,out float alpha)
        {
            double t=age-k*.03,u=t/WispLife;local=default;rise=size=alpha=0;if(!(u>=0&&u<1))return false;
            float h=(float)NativeWeaponFx.Hash(seed,60+k),h2=(float)NativeWeaponFx.Hash(seed,70+k);
            local=new Vector3((h-.5f)*.08f*(float)u,(h2-.5f)*.06f*(float)u,.05f+.22f*(float)(1-Math.Exp(-u*3)));rise=(float)(.3*t+.05*u);
            size=.07f+.3f*(float)Math.Sqrt(u);alpha=.2f*NativeWeaponFx.Smooth(u/.12)*(float)(1-u)*(float)(1-u);return true;
        }
        /// <summary>The light pulse (KriptoFX timing: up in 5 ms, linear out over .15 s), 0..1.</summary>
        public static float Light(double age)=>age<0||age>=LightLife?0:age<.0054?(float)(age/.0054):(float)(1-(age-.0054)/(LightLife-.0054));
    }

    /// <summary>
    /// Bullet tracers: a thin HDR streak from the round back along its flight, white-hot at its core with the gun's warm edge,
    /// never longer than the distance flown since the round was first seen (so it does not poke back through the gun).
    /// </summary>
    public static class NativeTracerFx
    {
        public const int EdgeColor=0xffe7aa;public const float CoreGain=3.4f;
        public static bool Traced(in NativeShotView v)=>v.Kind==NativeShotKind.Bullet&&v.Class=="ranged"&&!v.Siren&&!v.Bowling;
        public static float Width(string spec)=>spec=="rifle"?.034f:spec=="machinegun"?.024f:.027f;
        public static float Length(double speed,double travelled)=>(float)Math.Max(0,Math.Min(Math.Min(2.2,Math.Max(.4,speed*.03)),travelled));
    }

    /// <summary>
    /// Hard-surface impacts on top of the base layer's .12 s contact flash (NativeCombatGlow): an ETFX BulletExplosion-style
    /// star of six curved shards along the surface normal's hemisphere (.15-.2 s, shrinking), sparks that fly 3-11 m/s under
    /// gravity (bouncing off a floor) and cool white to #FF7900 to #DF3C00 to #180700 over .3-.6 s (the KriptoFX metal ramp), and a quick warm glow
    /// (#FFC500 to #FF7A00, .15 s). Seeded by the replicated event id.
    /// </summary>
    public static class NativeImpactFx
    {
        public const double Life=.62,GlowLife=.15;public const int Shards=6,Sparks=8;
        public static int ShardCount(float quality,bool low)=>quality<.5f?3:low?4:Shards;
        public static int SparkCount(float quality,bool low)=>quality<.5f?3:low?5:Sparks;
        // A direction in the hemisphere round n, biased toward it by `bias` (0: uniform hemisphere).
        private static Vector3 Hemisphere(Vector3 n,double h0,double h1,float bias)
        {
            var d=NativeWeaponFx.Sphere(h0,h1);if(Vector3.Dot(d,n)<0)d-=2*Vector3.Dot(d,n)*n;d=Vector3.Normalize(d+n*bias);return d;
        }
        /// <summary>Shard k: offset from the hit point, its direction (head), length and width (m) and opacity.</summary>
        public static bool Shard(long seed,int k,double age,Vector3 normal,out Vector3 offset,out Vector3 dir,out float length,out float width,out float alpha)
        {
            double life=.15+.05*NativeWeaponFx.Hash(seed,k),u=age/life;offset=dir=default;length=width=alpha=0;if(!(u>=0&&u<1))return false;
            dir=Hemisphere(normal,NativeWeaponFx.Hash(seed,10+k),NativeWeaponFx.Hash(seed,20+k),.35f);double speed=1+2*NativeWeaponFx.Hash(seed,30+k);
            float size=(float)(.1+.15*NativeWeaponFx.Hash(seed,40+k));length=size*1.6f*(float)(1-u);width=length*.42f;
            offset=dir*(float)(.05+speed*age+.5*length);alpha=(float)Math.Min(1,(1-u)*2.5);return true;
        }
        /// <summary>Spark k: offset from the hit point (gravity included), velocity and colour (cooling ramp, HDR) at `age`.</summary>
        public static bool Spark(long seed,int k,double age,Vector3 normal,out Vector3 offset,out Vector3 velocity,out FxColor color)
        {
            double life=.3+.3*NativeWeaponFx.Hash(seed,50+k),u=age/life;offset=velocity=default;color=default;if(!(u>=0&&u<1))return false;
            var dir=Hemisphere(normal,NativeWeaponFx.Hash(seed,60+k),NativeWeaponFx.Hash(seed,70+k),.6f);double speed=3+8*NativeWeaponFx.Hash(seed,80+k),g=9.81*.75;
            // Mild drag (dampen .2 of the reference) on the travel; gravity on top.
            double travel=speed*(1-Math.Exp(-2.2*age))/2.2;
            offset=dir*(float)travel+new Vector3(0,-(float)(.5*g*age*age),0);velocity=dir*(float)(speed*Math.Exp(-2.2*age))+new Vector3(0,-(float)(g*age),0);
            // On a floor (the normal mostly up) a spark that falls back through it bounces at .3 (the reference collides at .5).
            if(normal.Y>.7f&&offset.Y<0){offset.Y=-offset.Y*.3f;velocity.Y=Math.Abs(velocity.Y)*.3f;}
            color=NativeWeaponFx.Cooling(u)*(float)(1+2.4*(1-u));return true;
        }
        /// <summary>The quick glow: size (m) and colour.</summary>
        public static bool Glow(double age,out float size,out FxColor color)
        {
            double u=age/GlowLife;size=0;color=default;if(!(u>=0&&u<1))return false;
            size=.8f*(1-.31f*(float)u);color=FxColor.Lerp(NativeWeaponFx.Linear(0xffc500),NativeWeaponFx.Linear(0xff7a00),(float)u)*(1.7f*(float)(1-u));return true;
        }
    }

    /// <summary>
    /// Energy and magic projectiles (Sci-Fi PurplePlasmaMissile layering, ETFX MagicMissile white core): a white-hot core and
    /// a school-coloured glow on the existing model, world-space trail sprites emitted every Spacing metres flown (6 per
    /// metre; .08-.2 s, the reference's .15-.4 s shortened for these 26-55 m/s bolts; growing then shrinking, drifting), and a short tapered HDR ribbon over the last .1 s. Colours are the
    /// shot's halo colour (NativeShotRules.HaloColor: school, element or arcade). Sprite seeds are the shot key and the
    /// sprite's index along the path, so every peer emits the same sprites along the same path.
    /// </summary>
    public static class NativeTrailFx
    {
        public const double RibbonLife=.1;public const int RibbonSamples=10;
        public static bool Energy(in NativeShotView v)
        {
            if(v.Siren||v.Arcade!=0)return false;
            if(v.Kind==NativeShotKind.Bullet)return v.Class=="magic"&&!v.SwordBeam;
            return v.Kind==NativeShotKind.Ability&&(v.Ability=="ball-lightning"||v.Ability=="flame-lance"||v.Ability=="shadow-shackles");
        }
        /// <summary>Metres between trail sprites (Reduced effects: half as many; low graphics: two thirds).</summary>
        public static float Spacing(float quality,bool low)=>quality<.5f?.32f:low?.24f:.16f;
        /// <summary>The visual radius of the effects (at least .12 m: thin bolts still glow).</summary>
        public static float Radius(double radius)=>(float)Math.Max(.12,Math.Min(.6,radius));
        public static float CoreSize(float r)=>r*2.3f;
        public static float GlowSize(float r)=>r*7.5f;
        /// <summary>The white share of the core: darkness keeps more of its own hue.</summary>
        public static float CoreWhite(string spec)=>spec=="darkness"?.25f:.65f;
        /// <summary>Sprite `index` of a trail: life (s), size (m), offset and drift velocity.</summary>
        public static void Sprite(long seed,long index,float radius,out float life,out float size,out Vector3 offset,out Vector3 drift)
        {
            life=(float)(.08+.12*NativeWeaponFx.Hash(seed,index*4));size=radius*(2.6f+1f*(float)NativeWeaponFx.Hash(seed,index*4+1));
            offset=NativeWeaponFx.Sphere(NativeWeaponFx.Hash(seed,index*4+2),NativeWeaponFx.Hash(seed,index*4+3))*(radius*.6f);drift=offset*(.2f/Math.Max(.01f,radius*.6f));
        }
        /// <summary>Size over a sprite's life (.5, .91 at 9 %, 0 at the end) and its opacity (in by 6 %, out over the last 20 %).</summary>
        public static float SpriteSize(double u)=>u<0||u>=1?0:u<.09?(float)(.5+.41*u/.09):(float)(.91*(1-(u-.09)/.91));
        public static float SpriteAlpha(double u)=>u<0||u>=1?0:Math.Min(NativeWeaponFx.Clamp01(u/.06),NativeWeaponFx.Clamp01((1-u)/.2));
    }

    /// <summary>The look of one weapon's slash crescent.</summary>
    public struct SlashLook
    {
        /// <summary>Radius as a share of the swing's reach (Range), band thickness as a share of the radius, the fade after the
        /// window (s), the plane's roll (radians, about the swing's middle direction) and pitch (radians: its far side raised, so
        /// the chase camera and the first-person eye, both a little above the swing, see the crescent's face rather than its
        /// edge), the dark underlay's opacity, the
        /// bright edge's gain, how far it overshoots during the fade (radians), height above the feet and forward offset (m),
        /// the largest arc it draws (radians) and whether it is a twin arc.</summary>
        public float Reach,Thickness,Fade,Tilt,Pitch,Under,Edge,Overshoot,Height,Forward,MaxArc,MinRadius,MaxRadius;public bool Twin;
    }
    /// <summary>
    /// ETFX SwordSlashThick-style crescents for the standard melee strikes over the replicated hit window (Actor.SwingAt/
    /// SwingUntil = HitAt/HitUntil), centred on the committed yaw across the swing's Arc (weapon arc x meleeArc, the sword's
    /// every third strike .8). Since 2026-10-12 (owner: melee fluidity, docs/MELEE-FEEL.md) the head follows the visible blade
    /// (MeleeSwing.VisibleSweep of the cut the pilot plays): it grows as the posed blade crosses the arc, in the blade's
    /// direction, instead of sweeping the rules' angle uniformly (which runs mirrored to the captured cuts in the native frame).
    /// The crescent is thickest at the head, then fades fast while drifting on. Daggers: small, fast twin arcs; sword: one
    /// clean arc; greatsword: a wide, heavy arc with a darker underlay; shield: a short, fat bash arc. They replace the swept
    /// blade ribbon for these strikes (the fists, the Siren and released specials keep the ribbon).
    /// Since 2026-10-15 (owner: "weapon trails are too far / too early") the drawn crescent is swept from the posed blade
    /// itself (Core/NativeSlashTrail: its band, from the look's Thickness share of the blade out to the tip, through the
    /// blade's pass across the front); this class keeps its timing and opacity (Frame, read on the blade's own clock, so a
    /// hit-stop holds it with the blade), its look (Under, Edge, Fade, Twin) and the rules' arc (Radius, Tilt, the head's
    /// yaw), which the fixed arc was drawn from.
    /// </summary>
    public static class NativeSlashFx
    {
        public const int Segments=22;
        // Falling attacks carry a ten-second simulation safety window, not a visible slash lifetime.
        public static bool CanCapture(Actor a,float time)=>a!=null&&a.Alive&&Covers(a.SourceSpec)&&a.SwingAt>-90&&
            (!a.AirAttack||a.ImpactAt>=0)&&time>=a.SwingAt-1&&time<=a.SwingUntil+.3f;
        public static bool Covers(string spec)=>NativeMeleeRules.Standard(spec);
        /// <summary>The crescent stands in for the ribbon: a standard melee pilot's strike, not a released sword launch or wolf spin.</summary>
        public static bool Replaces(Actor a,float time)=>a!=null&&Covers(a.SourceSpec)&&!(a.SpecialReleased&&(a.SpecialKind=="sword-launch"||a.SpecialKind=="wolf-spin")&&time<a.SpecialUntil)&&!MeleeKitMotion.Sweeps(a,time);
        public static bool Look(string spec,out SlashLook look)
        {
            switch(spec)
            {
                case "daggers":look=new SlashLook{Reach=.72f,Thickness=.22f,Fade=.08f,Tilt=.26f,Pitch=.34f,Under=.22f,Edge=1.2f,Overshoot=.22f,Height=.78f,Forward=.05f,MaxArc=2.2f,MinRadius=.9f,MaxRadius=2.2f,Twin=true};return true;
                case "sword":look=new SlashLook{Reach=.72f,Thickness=.32f,Fade=.12f,Tilt=.22f,Pitch=.34f,Under=.3f,Edge=1.4f,Overshoot=.3f,Height=.76f,Forward=0,MaxArc=3.4f,MinRadius=1f,MaxRadius=3f};return true;
                case "greatsword":look=new SlashLook{Reach=.7f,Thickness=.42f,Fade=.2f,Tilt=.14f,Pitch=.3f,Under=.5f,Edge=1.7f,Overshoot=.4f,Height=.72f,Forward=-.05f,MaxArc=4.4f,MinRadius=1.4f,MaxRadius=4f};return true;
                case "shield":look=new SlashLook{Reach=.55f,Thickness=.5f,Fade=.1f,Tilt=.05f,Pitch=.3f,Under=.38f,Edge=1.3f,Overshoot=.15f,Height=.85f,Forward=.25f,MaxArc=1f,MinRadius=.8f,MaxRadius=2f};return true;
                default:look=default;return false;
            }
        }
        /// <summary>The crescent radius (m) for a swing's reach.</summary>
        public static float Radius(in SlashLook look,float range)=>Math.Max(look.MinRadius,Math.Min(look.MaxRadius,look.Reach*range));
        /// <summary>The tilt sign follows the visible blade's direction, so a strike rises toward its head either way.</summary>
        public static float Tilt(in SlashLook look,NativeMeleeSwing s)=>look.Tilt*Direction(s);
        /// <summary>The cut a swing plays (the captured set's cut for its attack index) and the visible blade over its window.</summary>
        // A sprint attack's crescent follows its sprint take's blade (Core/NativeSprintAttackVisuals).
        public static MeleeSwing.Visible Visible(NativeMeleeSwing s){var set=s!=null?MeleeSet.For(s.Spec):null;return set==null?null:MeleeSwing.VisibleSweep(set,s.SprintAttack&&NativeSprintAttackRules.Clip(s.Spec)!=null?NativeSprintAttackRules.Clip(s.Spec):set.Cut(s.Index));}
        /// <summary>The way the crescent opens round the yaw: the visible blade's (+1 toward the pilot's right, native yaw).</summary>
        public static float Direction(NativeMeleeSwing s){var v=Visible(s);return v!=null?v.Direction:NativeMeleeRules.Reverse(s)?-1:1;}
        /// <summary>
        /// One crescent at `time` (twin: the trailing copy, a quarter window late, inside and above the first, parallel to it):
        /// the tail and head yaw (radians, native: the tail at the arc's end where the visible blade enters it, the head where the
        /// blade has swept to, MeleeSwing.Swept, held there while fading and drifted on by the overshoot), its opacity and its
        /// radius scale. A capped arc (the shield's bash) keeps its middle. False before the window and once faded. The daggers'
        /// sprint twin (NativeSprintAttackRules.Twin) draws its two sweeps instead: the first over the window's first half, the
        /// trailing one over the second, opening back the other way as the blades do.
        /// </summary>
        public static bool Frame(NativeMeleeSwing s,in SlashLook look,float time,bool trailing,out float tail,out float head,out float alpha,out float grow)
        {
            tail=head=alpha=grow=0;if(s==null||s.Air&&s.ImpactAt<0)return false;bool twin=s.SprintAttack&&NativeSprintAttackRules.Twin(s.Spec)&&NativeSprintAttackRules.Clip(s.Spec)!=null;
            float window=Math.Max(.001f,s.HitUntil-s.HitAt),delay=trailing?window*.25f:0;if(twin){window*=.5f;delay=trailing?window:0;}float age=time-s.HitAt-delay;
            if(age<0||age>window+look.Fade)return false;
            var v=Visible(s);float sign=v!=null?v.Direction:NativeMeleeRules.Reverse(s)?-1:1,span=Math.Min((float)Math.PI*2,Math.Min(look.MaxArc,s.Arc));
            float u=Math.Min(1,age/window),swept=v!=null?MeleeSwing.Swept(v,u,span):u;
            if(twin&&trailing){sign=-sign;swept=v!=null?1-MeleeSwing.Swept(v,1-u,span):u;}
            tail=s.Yaw-sign*span*.5f;head=tail+sign*span*swept;
            float fade=age>window?(age-window)/look.Fade:0,drift=look.Overshoot*sign*fade*(2-fade)*.5f;tail+=drift;head+=drift;
            // Fades in as the blade enters the arc, so a strike that crosses it late does not start as a sliver at its end.
            alpha=(1-fade)*(1-fade)*NativeWeaponFx.Smooth(Math.Min(age/.015,swept/.08+.02))*(trailing&&!twin?.7f:1);grow=.9f+.08f*u+.06f*fade;return alpha>0;
        }
    }

    /// <summary>
    /// Roll afterimages (Trails FX snapshot approach): while a pilot rolls (the replicated Actor.RollAt and its roll duration,
    /// VisualRollDuration = RollEnd - RollAt), a snapshot of its posed voxel parts is taken every Spacing metres it travels;
    /// each copy draws as an additive fresnel ghost in the pilot's accent colour and fades over Life. Copies share a ring of
    /// slots (fewer with Reduced effects). The Ansatsuken's roll keeps its own Demon Step ghosts; the Siren does not roll.
    /// </summary>
    public sealed class NativeAfterimages
    {
        public const int Slots=24,ReducedSlots=10;public const double Life=.4,Spacing=.6,ReducedSpacing=1.1,Opacity=.55,Gain=1.5;
        public readonly double[] At=new double[Slots];public readonly int[] Owner=new int[Slots];private int index;
        private readonly Dictionary<int,(Vector3 At,double Roll)> last=new Dictionary<int,(Vector3,double)>();
        public NativeAfterimages(){for(int i=0;i<Slots;i++)At[i]=-10;}
        public static bool Eligible(Actor a,float time)=>a!=null&&a.Alive&&a.SourceSpec!="ansatsuken"&&a.SourceSpec!="siren"&&a.Rolling(time);
        /// <summary>A rolling pilot at `position`: true when it has travelled Spacing since its last copy of this roll (the
        /// first copy is taken one spacing into the roll, so the ghosts trail behind it).</summary>
        public bool Due(int owner,Vector3 position,double rollAt,double now,float quality)
        {
            if(now<rollAt)return false;
            if(!last.TryGetValue(owner,out var l)||l.Roll!=rollAt){last[owner]=(position,rollAt);return false;}
            return Vector3.Distance(position,l.At)>=(quality<.5f?ReducedSpacing:Spacing);
        }
        public int Take(int owner,Vector3 position,double rollAt,double now,float quality)
        {int slot=index++%(quality<.5f?ReducedSlots:Slots);if(index>=Slots*1000)index=0;At[slot]=now;Owner[slot]=owner;last[owner]=(position,rollAt);return slot;}
        public bool Live(int slot,double now){double age=now-At[slot];return age>=0&&age<Life;}
        public static double Fade(double age)=>age<0||age>=Life?0:Opacity*Math.Pow(1-age/Life,1.3);
        /// <summary>The ghost colour: the pilot's accent ember (NativeGlowRules.ArmorTint), HDR.</summary>
        public static FxColor Color(string accent){var t=NativeGlowRules.ArmorTint(accent);return new FxColor(t.X*(float)Gain,t.Y*(float)Gain,t.Z*(float)Gain);}
        public void Clear(){for(int i=0;i<Slots;i++)At[i]=-10;last.Clear();}
    }

    /// <summary>
    /// The hit flash on a struck pilot's body (ETFX's recommended pairing: a 60-100 ms material flash): a Hit or Headshot
    /// event above the source's .05 floor on a pilot flashes its body parts white, decaying through a red damage tint; a
    /// headshot flashes brighter, redder and a little longer (85 and 100 ms). It travels to the pilot shader in the per-instance accent
    /// tint's unused w (NativeSourceLit.shader StageTone): 1 is no flash, 1 + k a hit at strength k, 3 + k a headshot.
    /// </summary>
    public static class NativeHitFlash
    {
        public const double Life=.085,HeadLife=.1;public const float Gain=1.6f,HeadGain=2.6f;
        public static float Strength(double age,bool head){double life=head?HeadLife:Life;if(!(age>=0&&age<life))return 0;double u=1-age/life;return (float)Math.Pow(u,1.5);}
        public static float Code(double age,bool head,float quality=1){float k=Strength(age,head)*(quality<.5f?.7f:1);return k<=0?1:(head?3:1)+Math.Min(1,k);}
        public static void Decode(float code,out float k,out bool head){head=code>2.5f;k=Math.Max(0,Math.Min(1,code-(head?3:1)));}
        /// <summary>The flash colour at strength k (the shader's HitFlashColor): white fading through red; headshots brighter and redder.</summary>
        public static FxColor Color(float k,bool head)
        {
            var red=head?new FxColor(1,.16f,.1f):new FxColor(1,.42f,.32f);float w=NativeWeaponFx.Smooth((k-(head?.45f:.2f))/(head?.55f:.8f));
            return FxColor.Lerp(red,new FxColor(1,1,1),w)*(head?HeadGain:Gain);
        }
        /// <summary>How much of the lit colour the flash replaces: k x (.82 + .18 rim), headshots a little more.</summary>
        public static float Blend(float k,bool head,float rim)=>Math.Min(1,k*(head?.95f:.82f)*(.82f+.18f*rim));
    }

    /// <summary>
    /// The spell charge orb (ETFX MagicChargeBlue with Magic Arsenal's shrinking rings), around NativeSpellCharge's orb and
    /// halos: streaks spawned on a sphere converge on the orb (90 a second, .14-.24 s each, white turning to the school colour,
    /// stretched along their path), and rings shrink in to it (one every .14 s, .3 s each). Radii follow the orb's scale.
    /// Spawns are slots of the arena clock, so every peer and the killcam draw the same streaks.
    /// </summary>
    public static class NativeChargeFx
    {
        public const double Rate=90,ReducedRate=36,LifeMin=.14,LifeMax=.24,RingEvery=.14,RingLife=.3;public const float Radius=.3f;
        public static double StreakRate(float quality,bool low)=>quality<.5f?ReducedRate:low?Rate*.6:Rate;
        /// <summary>Streak k of the slot sequence at `time`: its unit direction (from the orb), progress u (0 at the sphere, 1 at
        /// the orb), opacity and the white share. False when not alive.</summary>
        public static bool Streak(long seed,long k,double rate,double time,out Vector3 dir,out float u,out float alpha,out float white)
        {
            dir=default;u=alpha=white=0;double born=k/rate,life=LifeMin+(LifeMax-LifeMin)*NativeWeaponFx.Hash(seed,k*3),age=time-born;if(!(age>=0&&age<life))return false;
            dir=NativeWeaponFx.Sphere(NativeWeaponFx.Hash(seed,k*3+1),NativeWeaponFx.Hash(seed,k*3+2));u=(float)(age/life);
            alpha=u<.5f?u/.5f:u<.74f?1:(1-u)/.26f;white=1-NativeWeaponFx.Smooth(u/.65);return true;
        }
        /// <summary>The first streak slot that may still be alive at `time`, and the last born by then.</summary>
        public static void Window(double rate,double time,out long first,out long last){first=(long)Math.Floor((time-LifeMax)*rate);last=(long)Math.Floor(time*rate);}
        /// <summary>Ring k (born every RingEvery s): size scale (1 to .33) and opacity (in to the middle, out).</summary>
        public static bool Ring(long k,double time,out float scale,out float alpha)
        {
            double age=time-k*RingEvery,u=age/RingLife;scale=alpha=0;if(!(u>=0&&u<1))return false;
            scale=(float)(1-.67*u);alpha=(float)(u<.5?u/.5:(1-u)/.5);return true;
        }
    }
}
