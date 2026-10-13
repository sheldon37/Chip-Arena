using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde kinds' attack telegraphs and effects (presentation only), as pure functions of what every peer holds: the add's
    /// replicated attack start (LastAttack), its kind's strike and recovery (NativeHordeKinds), its position and yaw, the
    /// presentation clock and its seed. Runtime/NativeHordeKindPresentation.cs draws them into the shared effect layers, within
    /// the horde's effect budgets and Reduced effects. Unity world axes throughout.
    /// <list type="bullet">
    /// <item>Every wind-up lights the add (<see cref="Brighten"/>): its seams, eyes and grin flare as it cocks, peak just before the
    /// strike and drop as it lands, so a crowd reads who is about to hit.</item>
    /// <item>Shambler swipe: the striking claw's tips glow through the telegraph, then the Siren's claw crescent rakes across
    /// at the strike (<see cref="Slash"/>).</item>
    /// <item>Runner lunge: hot pink streaks stretch behind it along the lunge it carries in its wind-up, and its blades flash at
    /// the strike (<see cref="Streak"/>).</item>
    /// <item>Brute smash: the authority's own area (a 1.6 m disc 1.2 m ahead) is drawn on the floor from the first frame of the
    /// wind-up, its fill closing in to the rim exactly at the strike (<see cref="SlamTelegraph"/>); then a shockwave ring, a
    /// flash and thrown debris (<see cref="Shockwave"/>, <see cref="Debris"/>).</item>
    /// <item>Spitter spit: the throat sac swells and glows (<see cref="Swell"/>), the glob arcs from the mouth to where it lands
    /// (<see cref="Glob"/>) and leaves an acid ink puddle that bubbles and fades (<see cref="Puddle"/>, <see cref="Bubble"/>).</item>
    /// <item>Crawler pounce: dust kicked at take-off and a bite flash; bloater: the belly swells and its seams strain, the slam
    /// puffs gas, and its death bursts (<see cref="Burst"/>).</item>
    /// <item>Melee kills on adds slice (<see cref="Gib"/>: the severed part tumbles, bounces and settles, then burns away with the body).</item>
    /// </list>
    /// No allocation.
    /// </summary>
    public static class NativeHordeFx
    {
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Hash(uint seed,int i,int k)=>(float)NativeGoreRules.Hash01(seed,i,k);

        // ---- the telegraph glow ----
        /// <summary>The add's glow gain at attack age a (1 at rest): up to 1.9 through the telegraph, 2.4 at the strike, back by
        /// the recovery. The bloater's swell and the spitter's sac add their own.</summary>
        public static float Brighten(float a,float strike,float recover)
        {
            if(float.IsNaN(a)||a<0||a>recover+.3f)return 1;
            float cock=Sm(a/Math.Max(.05f,strike*.8f)),flash=a<strike?Sm((a-strike+.06f)/.06f):1-Sm((a-strike)/.18f),fade=a<strike?1:1-Sm((a-strike)/Math.Max(.1f,(recover-strike)*.5f));
            return 1+.9f*cock*fade+.6f*flash;
        }
        /// <summary>How swollen the spitter's sac or the bloater's belly is (0..1): up through the wind-up, held, released at the strike.</summary>
        public static float Swell(NativeHordeKinds.Attack attack,float a,float strike)
        {
            if(attack!=NativeHordeKinds.Attack.Spit&&attack!=NativeHordeKinds.Attack.Belly||float.IsNaN(a)||a<0)return 0;
            if(a>=strike)return 1-Sm((a-strike)/.12f);float up=Sm(a/Math.Max(.05f,strike*.9f));
            return attack==NativeHordeKinds.Attack.Spit?up*(1+.07f*(float)Math.Sin(a*30)*up):up;
        }

        // ---- brute smash ----
        /// <summary>The smash telegraph at attack age a: the rim's opacity, the inner fill's radius (m, reaches the rim at the
        /// strike), and the fill's opacity; false outside the wind-up.</summary>
        public static bool SlamTelegraph(float a,float strike,out float rim,out float fill,out float fillAlpha)=>SlamTelegraph(a,strike,NativeHordeKinds.SmashRadius,out rim,out fill,out fillAlpha);
        /// <summary>The same for a disc of `radius` (the Hulk's).</summary>
        public static bool SlamTelegraph(float a,float strike,float radius,out float rim,out float fill,out float fillAlpha)
        {
            rim=fill=fillAlpha=0;if(float.IsNaN(a)||a<0||a>strike+.05f)return false;float u=Clamp01(a/strike);
            rim=.25f+.55f*Sm(a/.08f)+.2f*(float)Math.Sin(a*38)*u;fill=radius*u*u;fillAlpha=.12f+.28f*u;return true;
        }
        /// <summary>The slam's shockwave `since` seconds after the strike: ring radius (m), its width share, opacity; false when done.</summary>
        public static bool Shockwave(float since,out float radius,out float inner,out float alpha)
        {
            radius=inner=alpha=0;if(float.IsNaN(since)||since<0||since>.5f)return false;float u=since/.5f;
            radius=.3f+(NativeHordeKinds.SmashRadius+.7f)*(1-(1-u)*(1-u)*(1-u));inner=.72f+.2f*u;alpha=.85f*(1-u)*(1-u);return true;
        }
        public const int DebrisCount=18;
        /// <summary>Debris piece i thrown from the slam's centre `since` s after the strike: offset (m, Unity world), size, spin; false when settled out.</summary>
        public static bool Debris(uint seed,int i,float since,out Vector3 offset,out float size,out float spin,out float alpha)
        {
            offset=Vector3.Zero;size=spin=alpha=0;if(since<0||since>1.1f)return false;
            float a=(float)(Math.PI*2*(i+Hash(seed,i,1)*.7f)/DebrisCount),r=.35f+NativeHordeKinds.SmashRadius*.7f*Hash(seed,i,2),up=2.2f+2.6f*Hash(seed,i,3),out_=1.2f+1.8f*Hash(seed,i,4);
            float t=since,y=up*t-7.5f*t*t;if(y<0){float land=up/7.5f;float after=t-land;y=0;t=land+after*.15f;}
            offset=new Vector3((float)Math.Cos(a)*(r*.6f+out_*t),y,(float)Math.Sin(a)*(r*.6f+out_*t));size=.035f+.05f*Hash(seed,i,5);spin=t*(6+8*Hash(seed,i,6));alpha=1-Sm((since-.75f)/.35f);return true;
        }

        // ---- runner lunge ----
        public const int Streaks=5;
        /// <summary>Streak k behind a lunging runner at attack age a: its length along the travel (m), height above the floor, side
        /// offset, opacity; false outside the lunge (the wind-up's carry until just after the strike).</summary>
        public static bool Streak(uint seed,int k,float a,float strike,out float length,out float height,out float side,out float alpha,float scale=1)
        {
            length=height=side=alpha=0;if(float.IsNaN(a)||a<strike*.35f||a>strike+.22f)return false;
            float grow=Sm((a-strike*.35f)/(strike*.5f)),fade=a<strike?1:1-Sm((a-strike)/.22f);
            length=(.45f+.5f*Hash(seed,k,11))*grow;height=(.35f+1.0f*Hash(seed,k,12))*scale;side=(Hash(seed,k,13)-.5f)*.5f;alpha=.55f*fade*(.6f+.4f*Hash(seed,k,14));return true;
        }

        // ---- shambler swipe ----
        /// <summary>The claw crescent at attack age a: its sweep (0..1.9 along the arc, as the Siren's) and fade; false outside it.</summary>
        public static bool Slash(float a,float strike,out float sweep,out float fade)
        {
            sweep=fade=0;float start=strike-.07f;if(float.IsNaN(a)||a<start||a>strike+.3f)return false;float u=(a-start)/.37f;
            sweep=Math.Min(1.9f,u*4.2f);fade=1-Sm((u-.35f)/.65f);return true;
        }

        // ---- spitter ----
        /// <summary>The glob `since` s after launch on its arc from `from` to `to` over `flight` s (apex .9 m over the chord's
        /// middle at least): its position; false once it has landed.</summary>
        public static bool Glob(Vector3 from,Vector3 to,float flight,float since,out Vector3 at)
        {
            at=from;if(since<0||since>flight||flight<=0)return false;float u=since/flight;var p=Vector3.Lerp(from,to,u);float apex=.9f+.08f*Vector2.Distance(new Vector2(from.X,from.Z),new Vector2(to.X,to.Z));
            p.Y+=4*apex*u*(1-u);at=p;return true;
        }
        /// <summary>How long a glob flies to `distance` (m): about 9 m/s across, never under .35 s.</summary>
        public static float Flight(float distance)=>Math.Max(.35f,distance/9f+.12f);
        /// <summary>The acid puddle `since` s after the glob landed, lasting `duration`: its radius (spreading in .25 s) and opacity.</summary>
        public static bool Puddle(float since,float duration,float radius,out float r,out float alpha)
        {
            r=alpha=0;if(since<0||since>duration)return false;r=radius*(.35f+.65f*Sm(since/.25f))*Spread(since,duration);alpha=.85f*(1-Sm((since-duration+.8f)/.8f));return true;
        }
        public const int Bubbles=7;
        /// <summary>Bubble i on a puddle: its offset from the centre (m), size and opacity at `since`; false while it rests.</summary>
        public static bool Bubble(uint seed,int i,float since,float radius,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;float period=.6f+.6f*Hash(seed,i,21),phase=Hash(seed,i,22)*period,u=((since+phase)%period)/period;
            float a=(float)(Math.PI*2*Hash(seed,i,23)),r=radius*.8f*(float)Math.Sqrt(Hash(seed,i,24));offset=new Vector3((float)Math.Cos(a)*r,.01f+.04f*u,(float)Math.Sin(a)*r);
            size=(.03f+.04f*Hash(seed,i,25))*(float)Math.Sin(Math.PI*Math.Min(1,u*1.2f));alpha=u<.85f?.8f:0;return size>.002f&&alpha>0;
        }
        public const int Splashes=10;
        /// <summary>Splash drop i of a landing glob, `since` s after: offset from the landing point, size, opacity; false when done.</summary>
        public static bool Splash(uint seed,int i,float since,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>.5f)return false;float a=(float)(Math.PI*2*(i+Hash(seed,i,31)*.6f)/Splashes),v=1.4f+1.6f*Hash(seed,i,32),up=1.5f+1.8f*Hash(seed,i,33);
            float y=Math.Max(0,up*since-9.8f*since*since);offset=new Vector3((float)Math.Cos(a)*v*since,y,(float)Math.Sin(a)*v*since);size=.03f+.03f*Hash(seed,i,34);alpha=1-Sm((since-.3f)/.2f);return true;
        }

        // ---- bloater ----
        public const int BurstPieces=24;
        /// <summary>Burst piece i `since` s after the bloater bursts: offset from its belly (m), size, opacity, and whether it is
        /// gas (else an ink chunk); false when gone.</summary>
        public static bool Burst(uint seed,int i,float since,out Vector3 offset,out float size,out float alpha,out bool gas)
        {
            offset=Vector3.Zero;size=alpha=0;gas=i%3!=0;if(since<0||since>(gas?1.4f:1.1f))return false;
            float a=(float)(Math.PI*2*Hash(seed,i,41)),e=(float)Math.Asin(Hash(seed,i,42)*1.6f-.6f),v=(gas?1.6f:3.4f)*(.6f+.6f*Hash(seed,i,43));
            var d=new Vector3((float)(Math.Cos(e)*Math.Cos(a)),(float)Math.Sin(e),(float)(Math.Cos(e)*Math.Sin(a)));
            if(gas){float s=1-(float)Math.Exp(-since*3);offset=d*v*s*.55f+new Vector3(0,.35f*since,0);size=(.12f+.16f*Hash(seed,i,44))*(.4f+.9f*Sm(since/.4f));alpha=.55f*(1-Sm((since-.5f)/.9f));}
            else{float t=since;offset=d*v*t;offset.Y-=4.9f*t*t;size=.04f+.05f*Hash(seed,i,45);alpha=1-Sm((since-.8f)/.3f);}
            return true;
        }
        /// <summary>The belly slam's gas ring `since` s after the strike: radius, opacity.</summary>
        public static bool Puff(float since,float radius,out float r,out float alpha){r=alpha=0;if(since<0||since>.6f)return false;float u=since/.6f;r=.3f+radius*(1-(1-u)*(1-u));alpha=.5f*(1-u);return true;}

        // ---- melee slicing gibs ----
        /// <summary>
        /// A severed part `since` s after a melee kill on an add: thrown along the blow (`push`, Unity world, horizontal unit) up and
        /// out from its joint, spinning, bouncing twice on the floor (`floor` m below its start) and settling. Offset from the
        /// joint (m) and its turn (axis, angle). `weight`: how heavy the part is (1 a head or hand; a trunk half flies lower and
        /// slower, <see cref="Weight"/>).
        /// </summary>
        public static void Gib(uint seed,int i,float since,Vector2 push,float kick,float floor,out Vector3 offset,out Vector3 axis,out float angle,float weight=1)
        {
            float t=Math.Max(0,since),side=(Hash(seed,i,51)-.5f)*1.6f,inv=1/Math.Max(1,weight);var dir=push.LengthSquared()>1e-4f?Vector2.Normalize(push):new Vector2(0,1);var perp=new Vector2(dir.Y,-dir.X);
            Vector2 h=(dir*(1.4f+kick)+perp*side)*(.7f+.5f*Hash(seed,i,52))*inv;float vy=(1.6f+1.8f*Hash(seed,i,53)+.5f*kick)*(float)Math.Sqrt(inv),g=9.8f;
            // Ballistic to the floor, then two bounces losing most of their speed, then rest.
            float y,travel;float tl=(vy+(float)Math.Sqrt(vy*vy+2*g*Math.Max(0,floor)))/g;
            if(t<tl){y=vy*t-.5f*g*t*t;travel=t;}
            else{float after=t-tl,b=vy*.35f,tb=2*b/g;if(after<tb){y=-floor+b*after-.5f*g*after*after;travel=tl+after*.45f;}else{float after2=after-tb,b2=b*.3f,tb2=2*b2/g;y=-floor+(after2<tb2?b2*after2-.5f*g*after2*after2:0);travel=tl+tb*.45f+Math.Min(after2,tb2)*.15f;}}
            offset=new Vector3(h.X*travel,y,h.Y*travel);
            axis=Vector3.Normalize(new Vector3(perp.X,.3f+Hash(seed,i,54),perp.Y));float spin=(5+9*Hash(seed,i,55))*(Hash(seed,i,56)<.5f?-1:1)*inv;angle=spin*Math.Min(t,tl+.25f)+spin*.1f*Math.Max(0,Math.Min(t-tl-.25f,.4f));
        }

        /// <summary>A severed part's burn-away (0 whole .. 1 gone) at `age` s: from 1.2 s, gone by 2.3 s (the body's own pace).</summary>
        public static double PartVanish(float age)=>Sm((age-1.2f)/1.1f);

        /// <summary>How heavy a severed part is by its root joint: the trunk (spine) 3, a thigh 1.8, an upper arm 1.4, a head, forearm or shin 1.</summary>
        public static float Weight(int joint)=>joint==CapturedRig.Spine||joint==CapturedRig.Pelvis?3:joint==CapturedRig.LHip||joint==CapturedRig.RHip?1.8f:joint==CapturedRig.LArm||joint==CapturedRig.RArm?1.4f:1;

        // ---- footfalls ----
        public const int DustCount=6;
        /// <summary>Dust puff i at a heavy footfall or a take-off `since` s ago: offset (m) from the foot, size, opacity.</summary>
        public static bool Dust(uint seed,int i,float since,float power,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>.5f)return false;float a=(float)(Math.PI*2*(i+Hash(seed,i,61))/DustCount),v=(.5f+.7f*Hash(seed,i,62))*power;float s=1-(float)Math.Exp(-since*6);
            offset=new Vector3((float)Math.Cos(a)*v*s*.5f,.03f+.12f*s*power,(float)Math.Sin(a)*v*s*.5f);size=(.05f+.05f*Hash(seed,i,63))*(.6f+.8f*s)*power;alpha=.45f*(1-Sm(since/.5f));return true;
        }
        // ---- the brute's slam, upgraded: dust thrown round the disc, cracks run out of it ----
        public const int SlamDust=10,Cracks=7;
        /// <summary>Dust puff i of a slam `since` s after its strike, round a disc of `radius`: offset from the centre (m), size, opacity.</summary>
        public static bool SlamPuff(uint seed,int i,float since,float radius,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>.9f)return false;float a=(float)(Math.PI*2*(i+.5f*Hash(seed,i,21))/SlamDust),u=since/.9f;
            float r=radius*(.55f+.55f*(1-(1-u)*(1-u)))+.1f*Hash(seed,i,22);offset=new Vector3((float)Math.Cos(a)*r,.08f+.35f*u*(.6f+.4f*Hash(seed,i,23)),(float)Math.Sin(a)*r);
            size=.25f+.4f*u+.15f*Hash(seed,i,24);alpha=.55f*Sm(since/.05f)*(1-u)*(1-u);return true;
        }
        /// <summary>Crack i run out of a slam: its direction (rad), length (m) and opacity `since` s after the strike (they stay a moment).</summary>
        public static bool Crack(uint seed,int i,float since,float radius,out float angle,out float length,out float alpha)
        {
            angle=length=alpha=0;if(since<0||since>1.6f)return false;angle=(float)(Math.PI*2*(i+.6f*Hash(seed,i,25))/Cracks);
            length=radius*(.45f+.5f*Hash(seed,i,26))*Sm(since/.12f);alpha=.85f*(1-Sm((since-.9f)/.7f));return true;
        }

        // ---- the brute's jump-down (Core/NativeHordeDrop) ----
        /// <summary>The landing spot's telegraph `age` s after SpawnAt (`land` the touchdown age): the ring's radius (m, closing in to
        /// the slam's) and opacity, and the ink tear's size over the spot at the drop height; false once it has landed.</summary>
        public static bool DropMark(float age,float land,out float radius,out float alpha,out float tear)
        {
            radius=alpha=tear=0;if(age<0||age>land+.02f)return false;float u=Clamp01(age/Math.Max(.1f,land));
            radius=NativeHordeDrop.SlamRadius*(1.35f-.35f*u*u);alpha=(.25f+.55f*u)*Sm(age/.15f)*(.85f+.15f*(float)Math.Sin(age*24));
            tear=Sm(age/.25f)*(1-Sm((age-(land-.35f))/.35f));return true;
        }
        /// <summary>The landing slam's ring `since` s after touchdown: radius (m), width share, opacity; false when done.</summary>
        public static bool LandWave(float since,out float radius,out float inner,out float alpha)
        {
            radius=inner=alpha=0;if(since<0||since>.6f)return false;float u=since/.6f;
            radius=.4f+(NativeHordeDrop.SlamRadius+.6f)*(1-(1-u)*(1-u)*(1-u));inner=.7f+.22f*u;alpha=.9f*(1-u)*(1-u);return true;
        }
        /// <summary>Debris piece i of the landing (a smaller throw than the smash's).</summary>
        public static bool LandDebris(uint seed,int i,float since,out Vector3 offset,out float size,out float spin,out float alpha)
        {
            bool ok=Debris(seed^0x51ed,i,since,out offset,out size,out spin,out alpha);offset*=.8f;size*=.85f;return ok;
        }

        // ---- camera shake near a slam (position offset and a short pitch kick; Settings · camera shake gates it) ----
        public const int ShakeSlots=6;
        public struct Shake{public bool Live;public Vector3 At;public float Born,Peak,Range,Kick,Seconds,Roll;public uint Seed;}
        /// <summary>Remembers a slam's shake: at `at` (Unity world), its peak offset (m), reach (m) and, for a pilot inside the
        /// disc, the kick (deg) that throws the view up and back. The oldest slot is reused; the same slam twice is ignored.</summary>
        public static void AddShake(Shake[] slots,Vector3 at,float born,float peak,float range,float kick,uint seed)
        {
            int o=0;for(int i=0;i<slots.Length;i++){if(slots[i].Live&&slots[i].Seed==seed&&Math.Abs(slots[i].Born-born)<.05f)return;if(!slots[i].Live){o=i;break;}if(slots[i].Born<slots[o].Born)o=i;}
            slots[o]=new Shake{Live=true,At=at,Born=born,Peak=peak,Range=range,Kick=kick,Seed=seed};
        }
        /// <summary>A shake that lasts `seconds` (the Hulk's landing rolls on; a footfall is a short thump) with a roll kick (deg)
        /// for the shove a smash gives a pilot it hits.</summary>
        public static void AddShake(Shake[] slots,Vector3 at,float born,float peak,float range,float kick,uint seed,float seconds,float roll)
        {
            AddShake(slots,at,born,peak,range,kick,seed);
            for(int i=0;i<slots.Length;i++)if(slots[i].Live&&slots[i].Seed==seed&&Math.Abs(slots[i].Born-born)<.05f){slots[i].Seconds=seconds;slots[i].Roll=roll;}
        }
        public const float ShakeSeconds=.55f;
        /// <summary>The sum of the live shakes at the eye `eye` at time `now`: a position offset (m) and a pitch kick (deg, up).</summary>
        public static Vector3 ShakeAt(Shake[] slots,Vector3 eye,float now,out float pitch)=>ShakeAt(slots,eye,now,out pitch,out _);
        /// <summary>... and the roll kick (deg) of a shove.</summary>
        public static Vector3 ShakeAt(Shake[] slots,Vector3 eye,float now,out float pitch,out float roll)
        {
            var sum=Vector3.Zero;pitch=0;roll=0;
            for(int i=0;i<slots.Length;i++)
            {
                ref var s=ref slots[i];if(!s.Live)continue;float len=s.Seconds>0?s.Seconds:ShakeSeconds,t=now-s.Born;if(t<0||t>len){if(t>len||t<-1)s.Live=false;continue;}
                float d=Vector3.Distance(eye,s.At),fall=d>=s.Range?0:(1-d/s.Range)*(1-d/s.Range);if(fall<=0&&s.Kick<=0)continue;
                float env=(1-t/len)*(1-t/len)*Math.Min(1,t/.02f),w=t*55;
                sum+=new Vector3((float)Math.Sin(w+s.Seed%7),(float)Math.Sin(w*1.31f+1.7f)*.8f,(float)Math.Sin(w*.87f+3.1f)*.6f)*(s.Peak*fall*env);
                pitch+=s.Kick*env*(float)Math.Exp(-t*7);roll+=s.Roll*env*(float)Math.Exp(-t*5)*(float)Math.Cos(t*9);
            }
            return sum;
        }

        // =============================================================================================================
        // The Hulk (owner 2026-10-13: "huge knockback ... The jump in too, shaking the ground and a even bigger stage effect when
        // landing"): footfalls that crack the floor, a smash whose shockwave dwarfs the brute's, a landing that craters the floor
        // and throws up a wall of dust. Radii are the profile's (NativeHordeDrop.Hulk, NativeHordeKinds.HulkSmashRadius).
        // =============================================================================================================
        /// <summary>A shockwave of `radius` over `seconds`: its ring radius (m), width share and opacity `since` s after it starts.</summary>
        public static bool Wave(float since,float radius,float seconds,out float r,out float inner,out float alpha)
        {
            r=inner=alpha=0;if(since<0||since>seconds)return false;float u=since/seconds;
            r=.5f+radius*(1-(1-u)*(1-u)*(1-u));inner=.74f+.2f*u;alpha=.95f*(1-u)*(1-u);return true;
        }
        public const int DustWallCount=24;
        /// <summary>Puff i of the dust wall a heavy landing throws up round `radius`: rolling out and rising, tall, slow to clear.</summary>
        public static bool DustWall(uint seed,int i,float since,float radius,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>2.4f)return false;float a=(float)(Math.PI*2*(i+.5f*Hash(seed,i,71))/DustWallCount),u=Sm(since/1.6f);
            float r=radius*(.25f+.85f*(1-(float)Math.Exp(-since*2.6f)))+.3f*Hash(seed,i,72);offset=new Vector3((float)Math.Cos(a)*r,.3f+1.6f*u*(.6f+.4f*Hash(seed,i,73)),(float)Math.Sin(a)*r);
            size=.8f+1.8f*u+.5f*Hash(seed,i,74);alpha=.5f*Sm(since/.08f)*(1-Sm((since-.9f)/1.5f));return true;
        }
        /// <summary>The crater a heavy landing leaves: its radius (m) and darkness `since` s after touchdown (it stays, then fades).</summary>
        public static bool Crater(float since,float radius,out float r,out float alpha)
        {
            r=alpha=0;if(since<0||since>CraterSeconds)return false;r=radius*(.5f+.5f*Sm(since/.08f));alpha=.85f*(1-Sm((since-(CraterSeconds-2.5f))/2.5f));return true;
        }
        public const float CraterSeconds=9;public const int CraterChunks=12;
        /// <summary>Rubble chunk i heaped round the crater's rim: offset (m), size, tilt (rad) and opacity.</summary>
        public static bool CraterChunk(uint seed,int i,float since,float radius,out Vector3 offset,out float size,out float tilt,out float alpha)
        {
            offset=Vector3.Zero;size=tilt=alpha=0;if(since<0||since>CraterSeconds)return false;float a=(float)(Math.PI*2*(i+.7f*Hash(seed,i,75))/CraterChunks),r=radius*(.85f+.25f*Hash(seed,i,76));
            float pop=Sm(since/.12f);size=(.14f+.16f*Hash(seed,i,77))*pop;offset=new Vector3((float)Math.Cos(a)*r,size*.3f,(float)Math.Sin(a)*r);tilt=(Hash(seed,i,78)-.5f)*1.2f;alpha=1-Sm((since-(CraterSeconds-2))/2);return true;
        }
        public const int BigCracks=12;
        /// <summary>Crack i of a heavy landing or smash, out to `radius`: direction, length, and opacity (they stay a while).</summary>
        public static bool BigCrack(uint seed,int i,float since,float radius,float stay,out float angle,out float length,out float alpha)
        {
            angle=length=alpha=0;if(since<0||since>stay)return false;angle=(float)(Math.PI*2*(i+.6f*Hash(seed,i,81))/BigCracks);
            length=radius*(.5f+.6f*Hash(seed,i,82))*Sm(since/.18f);alpha=.9f*(1-Sm((since-stay*.6f)/(stay*.4f)));return true;
        }
        /// <summary>Debris piece i thrown out to `radius` (the smash's throw scaled to a heavy kind's area).</summary>
        public static bool HeavyDebris(uint seed,int i,float since,float radius,out Vector3 offset,out float size,out float spin,out float alpha)
        {
            bool ok=Debris(seed^0x6a1,i,since*.85f,out offset,out size,out spin,out alpha);float k=radius/NativeHordeKinds.SmashRadius;offset=new Vector3(offset.X*k,offset.Y*(.8f+.4f*k),offset.Z*k);size*=1.6f;return ok;
        }
        public const int StompCracks=4;public const float StompSeconds=4;
        /// <summary>Crack i a Hulk's footfall leaves round its foot: direction, length (m) and opacity, fading over <see cref="StompSeconds"/>.</summary>
        public static bool StompCrack(uint seed,int i,float since,out float angle,out float length,out float alpha)
        {
            angle=length=alpha=0;if(since<0||since>StompSeconds)return false;angle=(float)(Math.PI*2*(i+.8f*Hash(seed,i,85))/StompCracks);
            length=(.35f+.45f*Hash(seed,i,86))*Sm(since/.06f);alpha=.8f*(1-Sm((since-StompSeconds*.5f)/(StompSeconds*.5f)));return true;
        }

        // =============================================================================================================
        // The spitter's acid (owner 2026-10-13: "The spitter should shoot do a windup animation and shoot acidic spit from their
        // mouth, creating a small puddle of poison (Can corrode destructibles). Can interupt their spit by hitting them.")
        // =============================================================================================================
        public const int DroolDrops=5;
        /// <summary>Drool drop i falling from the mouth through the wind-up (`age` from LastAttack, the strike at `strike`): its offset
        /// below the mouth (m), size, opacity.</summary>
        public static bool Drool(uint seed,int i,float age,float strike,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(float.IsNaN(age)||age<.05f||age>strike+.1f)return false;
            float period=.32f+.18f*Hash(seed,i,91),u=((age+Hash(seed,i,92)*period)%period)/period,t=u*period;
            offset=new Vector3((Hash(seed,i,93)-.5f)*.05f,-(.02f+4.9f*t*t),.015f*Hash(seed,i,94));size=(.012f+.012f*Hash(seed,i,95))*(1-.3f*u);alpha=.85f*Sm(age/.2f)*(u<.9f?1:0);return true;
        }
        public const int TrailDots=8,TrailDrips=6;
        /// <summary>Trail dot i behind a flying glob: where the glob was (i+1) x 35 ms ago, its size share and opacity.</summary>
        public static bool GlobTrail(Vector3 from,Vector3 to,float flight,float since,int i,out Vector3 at,out float size,out float alpha)
        {
            at=from;size=alpha=0;float back=since-(i+1)*.035f;if(back<0||since>flight)return false;Glob(from,to,flight,back,out at);size=1-(i+1f)/(TrailDots+1);alpha=.75f*size;return true;
        }
        /// <summary>Drip i shaken off the glob in flight: it leaves the arc at its own moment and falls; false once it has landed or
        /// the glob has.</summary>
        public static bool TrailDrip(Vector3 from,Vector3 to,float flight,uint seed,int i,float since,out Vector3 at,out float alpha)
        {
            at=from;alpha=0;float leave=flight*(.1f+.8f*(i+Hash(seed,i,96))/TrailDrips),t=since-leave;if(t<0||since>flight+.6f)return false;
            Glob(from,to,flight,leave,out var p);var y=p.Y-4.9f*t*t;if(y<Math.Min(from.Y,to.Y)-.05f)return false;at=new Vector3(p.X,y,p.Z);alpha=.8f;return true;
        }
        public const int Sizzles=6;
        /// <summary>Sizzle smoke puff i rising off acid `since` s after it started eating (a loop): offset (m), size, opacity.</summary>
        public static bool Sizzle(uint seed,int i,float since,float spread,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0)return false;float period=.7f+.5f*Hash(seed,i,97),u=((since+Hash(seed,i,98)*period)%period)/period;
            float a=(float)(Math.PI*2*Hash(seed,i,99));offset=new Vector3((float)Math.Cos(a)*spread*Hash(seed,i,100),.05f+.5f*u,(float)Math.Sin(a)*spread*Hash(seed,i,100));
            size=.06f+.12f*u;alpha=.4f*Sm(u/.15f)*(1-u);return true;
        }
        /// <summary>The puddle's spread over its life (a share of its radius: it creeps out to the authority's radius, never past it,
        /// so its clear edge reads the area).</summary>
        public static float Spread(float since,float duration)=>.88f+.12f*Sm(since/Math.Max(.1f,duration*.8f));
        public const int Fumes=5;
        /// <summary>Fume i off a poison puddle: rising, drifting, offset (m), size and opacity.</summary>
        public static bool Fume(uint seed,int i,float since,float duration,float radius,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>duration)return false;bool ok=Sizzle(seed^0xf0e,i,since*.6f,radius*.7f,out offset,out size,out alpha);
            offset.Y*=1.6f;size*=1.5f;alpha*=.8f*(1-Sm((since-duration+1)/1));return ok;
        }
        public const int FizzleDrops=6;public const float FizzleSeconds=1.4f,FizzleRadius=.32f;
        /// <summary>The interrupted spit's acid, `since` s after the cancel: drop i dribbling from the mouth (`drop` m above the feet) to
        /// the floor, its offset from the mouth, size and opacity; false once it has landed.</summary>
        public static bool Fizzle(uint seed,int i,float since,float drop,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;float start=i*.06f+.04f*Hash(seed,i,101),t=since-start;if(t<0)return false;float y=-4.9f*t*t;if(-y>drop)return false;
            offset=new Vector3((Hash(seed,i,102)-.5f)*.12f,y,.06f+.05f*Hash(seed,i,103)+.15f*t);size=.018f+.014f*Hash(seed,i,104);alpha=.9f;return true;
        }
        /// <summary>The harmless fizzing spot at its feet after an interrupt: radius (m) and opacity.</summary>
        public static bool FizzlePool(float since,out float r,out float alpha)
        {
            r=alpha=0;if(since<.15f||since>FizzleSeconds)return false;r=FizzleRadius*(.4f+.6f*Sm((since-.15f)/.25f));alpha=.7f*(1-Sm((since-.7f)/(FizzleSeconds-.7f)));return true;
        }
        public const int Pits=7,CorrodeDrips=4;public const float CorrodeFade=1;
        /// <summary>Pit i eaten into a block's face round the acid's contact (face-local offset, m), its size and darkness `since` s
        /// after the acid touched it (they deepen while it lasts, `duration`).</summary>
        public static bool Pit(uint seed,int i,float since,float duration,float spread,out Vector2 at,out float size,out float alpha)
        {
            at=Vector2.Zero;size=alpha=0;float start=.25f*i/Pits;if(since<start||since>duration+CorrodeFade)return false;float a=(float)(Math.PI*2*Hash(seed,i,105)),r=spread*(float)Math.Sqrt(Hash(seed,i,106));
            at=new Vector2((float)Math.Cos(a)*r,(float)Math.Sin(a)*r);size=(.03f+.05f*Hash(seed,i,107))*(.4f+.6f*Sm((since-start)/Math.Max(.3f,duration*.6f)));alpha=.9f*(1-Sm((since-duration)/CorrodeFade));return true;
        }
        /// <summary>A corroded drip i running down a block's face from the contact: its face-local start, how far it has run (m, down)
        /// and opacity.</summary>
        public static bool CorrodeDrip(uint seed,int i,float since,float duration,float spread,out Vector2 at,out float run,out float alpha)
        {
            at=Vector2.Zero;run=alpha=0;float start=.15f+.4f*Hash(seed,i,108);if(since<start||since>duration+CorrodeFade)return false;
            at=new Vector2((Hash(seed,i,109)-.5f)*spread*1.6f,0);run=Math.Min(.5f+.3f*Hash(seed,i,110),.18f*(since-start));alpha=.75f*(1-Sm((since-duration)/CorrodeFade));return true;
        }
    }
}
