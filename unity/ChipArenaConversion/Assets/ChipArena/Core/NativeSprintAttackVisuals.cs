using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee sprint attacks' presentation (owner 2026-10-12: "can we give melee's a "sprint" attack? Of course each melee
    /// gets their own."). A grounded swing the authority flags as entered from a sprint (Swing.SprintAttack, replicated as
    /// Actor.SprintAttack) plays the weapon's own running take from the owner's pack (tools/export_pack_sprint_attacks.mjs,
    /// NativePackSprintAttacks.Generated) instead of its first ground cut:
    /// - sword "Lunging Slash": a running lunge into a long low horizontal slash;
    /// - daggers "Twin Lunge": both blades cross-cut on the first contact and open back out on the second (the authority's two
    ///   sweeps, NativeSprintAttack.Pulse: the second reversed, half a window each);
    /// - shield "Shoulder Charge": shield first, shoulder dropped, with a bash (the shield driven out) at the hit;
    /// - greatsword "Leaping Cleave": the authority's hop (NativeSprintAttack.Hop) with both feet tucked, a sweeping two-handed
    ///   cleave as it lands planted, and the landing ring (NativeSprintAttack.Ring, RingRadius) in dust and accent light.
    /// The take follows the replicated swing timing (VisualWindup/VisualActive/VisualDuration, SwingAt/SwingUntil, StepAt/
    /// StepUntil/StepDistance: Astra's per-weapon profiles, NativeSprintAttack.Apply). The feet are the footwork's
    /// (Core/MeleeFootwork): the run's stride carries into the strike's steps, the greatsword leaps with both feet. Presentation
    /// only: timing, hitbox, damage, steps and every rule are the authority's. No allocation.
    /// </summary>
    public static class NativeSprintAttackRules
    {
        /// <summary>The weapon's sprint take (null for a weapon without one: it keeps its first cut from a sprint).</summary>
        public static CapturedClip Clip(string spec)
        {
            var names=NativePackSprintAttacks.Names;for(int i=0;i<names.Length;i++)if(names[i]==spec)return NativePackSprintAttacks.Clips[i];return null;
        }
        /// <summary>Whether a presented swing plays its sprint take: flagged a sprint attack, grounded (a swing begun in the air
        /// is a jump attack) and the weapon has one.</summary>
        public static bool Plays(string spec,bool sprintAttack,bool air)=>sprintAttack&&!air&&Clip(spec)!=null;
        /// <summary>The greatsword leaps: both feet leave the floor with the take's hop and land together with the cleave.</summary>
        public static bool Leaps(string spec)=>spec=="greatsword";
        /// <summary>The daggers' twin: two sweeps, half the hit window each, the second reversed (NativeSprintAttack.Pulse).</summary>
        public static bool Twin(string spec)=>spec=="daggers";
        /// <summary>The landing ring's reach round the greatsword's feet (NativeSprintAttack.Ring, before a target's size).</summary>
        public const float RingRadius=1.6f;
        /// <summary>The take's clip time `age` seconds into the swing (MeleeSwing.ClipTime). The twin plays the take's cross-cut
        /// over the first half of the window and whips back out over the second (easing into the opened pose), then holds that
        /// pose while the follow-through settles it into the guard.</summary>
        public static float ClipTime(CapturedClip c,string spec,float age,float windup,float active,float recovery)
        {
            if(!Twin(spec))return MeleeSwing.ClipTime(c,age,windup,active,recovery);
            var s=MeleeSwing.For(c);float half=Math.Max(.0005f,Math.Max(.001f,active)*.5f);
            if(age<=windup+half)return MeleeSwing.ClipTime(c,age,windup,half,recovery);
            float d=s.SweepStart-s.SweepEnd,u=Math.Min(1,(age-windup-half)/half);
            return MeleeMath.Hermite(s.SweepEnd,s.SweepStart,.6f*d,.3f*d,u);
        }
        // ---- the shield's bash ----
        /// <summary>The charge already holds the shield near the arm's full reach, across the front, so the bash comes from the
        /// body: the chest turns the held shield forward by BashTurn and pitches over by BashPitch (radians), the body pushes on
        /// BashPush and the shield goes out the last BashReach and up BashLift (metres). It rises over the last BashLead of the windup to its
        /// peak 60% of the way through the hit window and is drawn back over the first 30% of the recovery: a punch, not a lean.</summary>
        public const float BashReach=.1f,BashLift=.04f,BashTurn=-.14f,BashPitch=.08f,BashPush=.18f,BashLead=.6f;
        public static float Bash(float age,float windup,float active,float recovery)
        {
            windup=Math.Max(.001f,windup);active=Math.Max(.001f,active);float peak=windup+.6f*active;
            if(age<peak)return MeleeMath.Ease((age-(1-BashLead)*windup)/Math.Max(.001f,peak-(1-BashLead)*windup));
            return 1-MeleeMath.Smooth((age-peak)/Math.Max(.001f,windup+active-peak+.3f*Math.Max(.001f,recovery)));
        }
        // ---- the effects' moments ----
        /// <summary>The greatsword's landing ring is live (NativeSprintAttack.Ring): the pilot is down inside the hit window.</summary>
        public static bool RingLive(float time,float hitAt,float hitUntil,bool grounded)=>grounded&&time>=hitAt&&time<=hitUntil;
    }

    /// <summary>
    /// The sprint attacks' effects (drawn by Runtime/NativeSprintAttackFxPresentation; positions in the Unity world, all above
    /// the floor): a dust burst kicked up round the feet and thrown forward as the swing leaves the sprint; weapon-accent speed
    /// streaks trailing the pilot through the hit window; and the greatsword's landing ring: dust rolling out over the floor and
    /// a ring of accent light spreading to the ring's reach (RingRadius) round its feet as it lands its cleave. Reduced effects
    /// and Low graphics thin them. No allocation.
    /// </summary>
    public static class NativeSprintAttackFx
    {
        public const float BurstLife=.45f,StreakLife=.18f,PuffLife=.5f,ShockLife=.24f,FloorClear=.04f;
        public const int Burst=8,ReducedBurst=3,Streaks=4,ReducedStreaks=2,Puffs=10,ReducedPuffs=4,Motes=18,ReducedMotes=8;
        public static int Count(int full,int reduced,float quality,bool low)=>NativeAirAttackFx.Count(full,reduced,quality,low);
        private static float Out(float u)=>1-(1-u)*(1-u)*(1-u);
        private static float Hash(int seed,int k){uint h=(uint)(seed*83492791)^(uint)(k*2654435761u);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216f;}
        private static Vector3 Ahead(float yawDegrees){float a=yawDegrees*(float)Math.PI/180;return new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));}
        /// <summary>Entry dust puff k of n: kicked off the floor round the feet, thrown forward along the facing and out to the
        /// sides, growing and fading.</summary>
        public static bool Kick(int k,int n,int seed,float age,Vector3 feet,float yawDegrees,float floor,out Vector3 at,out float size,out float opacity)
        {
            at=feet;size=opacity=0;float u=age/BurstLife;if(!(u>=0&&u<1)||n<=0)return false;
            Vector3 f=Ahead(yawDegrees),side=new Vector3(f.Z,0,-f.X);float s=(k%2==0?1:-1)*(.35f+.65f*Hash(seed,k)),ahead=.15f+.85f*Hash(seed,k+7);
            Vector3 dir=Vector3.Normalize(f*ahead+side*s*.8f);float r=(.15f+.75f*Out(u))*(.8f+.4f*Hash(seed,k+3));
            at=new Vector3(feet.X+dir.X*r,floor+FloorClear+.08f+.14f*Out(u),feet.Z+dir.Z*r);
            size=.18f+.3f*Out(u);opacity=.45f*(float)Math.Pow(1-u,1.3)*Math.Min(1,age/.03f);return true;
        }
        /// <summary>Speed streak k of n through the hit window (window share `u`): accent lines trailing back from the pilot at
        /// chest to knee height, sliding back and fading as the strike carries on.</summary>
        public static bool Streak(int k,int n,int seed,float u,Vector3 feet,float yawDegrees,out Vector3 from,out Vector3 along,out float alpha)
        {
            from=feet;along=Vector3.Zero;alpha=0;if(!(u>=0&&u<1)||n<=0)return false;
            Vector3 f=Ahead(yawDegrees),side=new Vector3(f.Z,0,-f.X);float lane=(k+.5f)/n-.5f,h=.35f+.75f*Hash(seed,k+11),length=.45f+.4f*Hash(seed,k+13);
            from=feet+side*(lane*.7f)+new Vector3(0,h,0)-f*(.25f+.5f*u);along=-f*length*(1-.5f*u);alpha=(float)Math.Sin(Math.PI*u)*(.7f+.3f*Hash(seed,k+17));return true;
        }
        /// <summary>The landing ring's dust puff k of n: spaced round the feet, rolling out over the floor to the ring's reach,
        /// growing and fading.</summary>
        public static bool Puff(int k,int n,int seed,float age,Vector3 feet,float floor,out Vector3 at,out float size,out float opacity)
        {
            at=feet;size=opacity=0;float u=age/PuffLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=(float)(2*Math.PI*(k+.3f+.4f*Hash(seed,k))/n),r=NativeSprintAttackRules.RingRadius*(.2f+.75f*Out(u))*(.9f+.15f*Hash(seed,k+5));
            at=new Vector3(feet.X+(float)Math.Sin(a)*r,floor+FloorClear+.08f+.12f*Out(u),feet.Z+(float)Math.Cos(a)*r);
            size=.22f+.34f*Out(u);opacity=.5f*(float)Math.Pow(1-u,1.3)*Math.Min(1,age/.03f);return true;
        }
        /// <summary>The landing ring's accent mote k of n: a ring of light just over the floor, spreading from the feet to the
        /// ring's reach and fading as it gets there.</summary>
        public static bool Mote(int k,int n,float age,Vector3 feet,float floor,out Vector3 at,out float size,out float alpha)
        {
            at=feet;size=alpha=0;float u=age/ShockLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=(float)(2*Math.PI*k/n),r=NativeSprintAttackRules.RingRadius*(.15f+.85f*Out(u));
            at=new Vector3(feet.X+(float)Math.Sin(a)*r,floor+FloorClear+.03f,feet.Z+(float)Math.Cos(a)*r);
            size=.26f-.1f*u;alpha=(float)Math.Pow(1-u,1.4)*.8f;return true;
        }
    }

    /// <summary>
    /// One presented pilot's sprint attacks, from its replicated swing fields: whether the current swing is a sprint attack, and
    /// the moments its effects and cues key to (the entry as it begins, the hit window, the greatsword's landing ring going
    /// live). A rewind or a gap restarts it; each moment fires once per swing.
    /// </summary>
    public sealed class NativeSprintAttackWatch
    {
        public float SwingAt=-100,HitAt=-100,HitUntil=-100,Time=-100;public bool Sprint;public int Entries,Impacts;private bool impacted;
        public void Reset(){SwingAt=HitAt=HitUntil=Time=-100;Sprint=false;impacted=false;}
        /// <summary>`attackAt` the swing's start (-100 for none); `sprintAttack` the replicated flag (the authority sets it only on
        /// a grounded opener, so a swing already hopping is still one); `swingAt`/`swingUntil` its hit window while known;
        /// `grounded` the replicated contact; `owned` whether a special, stun, roll, juggle or knockdown owns the pilot.</summary>
        public void Observe(float attackAt,bool sprintAttack,float swingAt,float swingUntil,bool grounded,string spec,float time,bool owned=false)
        {
            if(!MathUtil.Finite(time))return;
            if(Time<-90||time<Time-MeleeGait.REWIND||time-Time>.35f){Reset();Time=time;}
            Time=Math.Max(Time,time);
            if(attackAt>-90&&attackAt!=SwingAt&&time>=attackAt-1e-4f)
            {
                SwingAt=attackAt;HitAt=HitUntil=-100;impacted=false;
                Sprint=!owned&&NativeSprintAttackRules.Plays(spec,sprintAttack,false);if(Sprint)Entries++;
            }
            if(attackAt==SwingAt&&swingAt>-90){HitAt=swingAt;HitUntil=Math.Max(swingAt,swingUntil);}
            if(Sprint&&!impacted&&HitAt>-90&&!owned&&NativeSprintAttackRules.Leaps(spec)&&NativeSprintAttackRules.RingLive(time,HitAt,HitUntil,grounded)){impacted=true;Impacts++;}
        }
        /// <summary>The hit window's share at `time` while the current sprint attack strikes (NaN outside it).</summary>
        public float Window(float time)=>Sprint&&HitAt>-90&&HitUntil>HitAt&&time>=HitAt&&time<HitUntil?(time-HitAt)/(HitUntil-HitAt):float.NaN;
    }
}
