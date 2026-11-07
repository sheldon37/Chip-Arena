using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The plunging stab's replicated data as the presentation reads it (owner 2026-10-12: "Can we add a downward jump attack as
    /// well? If the player is facing downwards?", choice "Plunging stab"; docs/NEW-KITS.md section 1). Astra builds the rule
    /// (trigger, drop, hits, replication, prediction, bots); everything that draws the stab (the rigs, the first-person view,
    /// the effects, the camera kick and the sounds) reads only this struct, filled in one place (NativePlungeFields.Of).
    /// Times are on the presentation clock of the actor they came from; NaN marks a value not known yet.
    /// </summary>
    public struct NativePlungeData
    {
        /// <summary>The swing is a plunging stab (the air-attack kind "down"); false for the forward jump attack and ground swings.</summary>
        public bool Down;
        /// <summary>The swing's start (the press), the drop's start (the windup's end) and the landing.</summary>
        public float At,PlungeAt,ImpactAt;
        /// <summary>The stuck recovery's end once landed (the swing's commitment end), and the fall height at the impact (metres).</summary>
        public float Until,Height;
        public static readonly NativePlungeData None=new NativePlungeData{At=-100,PlungeAt=float.NaN,ImpactAt=float.NaN,Until=float.NaN,Height=float.NaN};
    }

    /// <summary>
    /// THE field mapping (the one place in the presentation that names Astra's replicated fields; docs/MELEE-FEEL.md "Plunging
    /// stab"). Per docs/NEW-KITS.md section 1 and PR #140: the jump attack's Swing.Air (Actor.AirAttack), PlungeAt and ImpactAt,
    /// the air-attack kind Actor.AirKind ("down"), the fall height set at impact (Actor.FallHeight) and the impact event (Land,
    /// Detail "melee-down", radius, weapon). Fixtures and the offline harness build NativePlungeData directly.
    /// </summary>
    public static class NativePlungeFields
    {
        /// <summary>The proposed air-attack kind of the plunging stab and the proposed Detail of its impact event.</summary>
        public const string DownKind="down",ImpactDetail="melee-down";
        /// <summary>The replicated actor's plunge data (allocation-free).</summary>
        public static NativePlungeData Of(Actor a)
        {
            var d=NativePlungeData.None;if(a==null||!(a.AttackAt>-90))return d;
            d.At=a.AttackAt;d.Down=a.AirAttack&&!a.SprintAttack&&Kind(a)==DownKind;
            d.PlungeAt=a.AirAttack&&a.PlungeAt>a.AttackAt?a.PlungeAt:float.NaN;
            d.ImpactAt=a.AirAttack&&a.ImpactAt>=0&&a.ImpactAt>=a.AttackAt-1e-3f?a.ImpactAt:float.NaN;
            // The commitment end becomes the stuck recovery's end once the swing has landed (the jump attack's landing sets it).
            d.Until=MathUtil.Finite(d.ImpactAt)&&a.CommitAt>-90&&Math.Abs(a.CommitAt-a.AttackAt)<.002f&&a.CommitUntil>d.ImpactAt?a.CommitUntil:float.NaN;
            d.Height=Height(a);
            return d;
        }
        /// <summary>The air-attack kind: Astra's replicated Actor.AirKind ("down" for the stab, empty for the forward jump attack;
        /// docs/NEW-KITS.md "Plunging stab implementation contract", PR #140).</summary>
        public static string Kind(Actor a)=>a.AirKind;
        /// <summary>The replicated centre-height fall distance (Actor.FallHeight, metres; NaN while it is not yet set, so the
        /// presented drop is measured until the host fills it).</summary>
        public static float Height(Actor a)=>a.FallHeight>0&&a.FallHeight<200?a.FallHeight:float.NaN;
        /// <summary>The plunge's impact event: Land with Detail "melee-down" (Source the plunging pilot, Position the ground point,
        /// Radius the ring).</summary>
        public static bool Impact(in ArenaEvent e)=>e.Kind==EventKind.Land&&e.Detail==ImpactDetail;
        /// <summary>The fall height an impact event carries (NaN: none named yet; Amount is the stand-in once Astra fills it).</summary>
        public static float Height(in ArenaEvent e)=>e.Amount>0&&e.Amount<200?e.Amount:float.NaN;
        /// <summary>
        /// Whether a Hit from the plunging pilot is the direct victim's (the pilot the plunge lands on, or one the blade passes
        /// through during the drop) rather than a ring victim's: no Detail separates them yet, so a hit before the landing, or
        /// one within DirectReach of the pilot's feet at the landing, is the direct one.
        /// </summary>
        public static bool Direct(in ArenaEvent e,int source,float plungeAt,float impactAt,V3 feet)
        {
            if(e.Source!=source||e.Target<=0||e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)return false;
            if(!(e.Time>=plungeAt-1e-3f))return false;
            if(!MathUtil.Finite(impactAt))return true;if(e.Time>impactAt+NativePlungeRules.HitGrace)return false;if(e.Time<impactAt-1e-3f)return true;
            float dx=e.Position.x-feet.x,dz=e.Position.z-feet.z;return dx*dx+dz*dz<=DirectReach*DirectReach;
        }
        public const float DirectReach=.85f;
    }

    /// <summary>Which air attack a swing shows.</summary>
    public enum NativeAirStrike{None,Forward,Down}
    /// <summary>The plunging stab's pose phases (NativePlungeRules.Clock).</summary>
    public enum NativePlungePhase{None,Windup,Drop,Plant,Stuck,Pull,Settle}

    /// <summary>Where a plunging stab is at one moment (NativePlungeRules.Clock).</summary>
    public struct NativePlungeClock
    {
        public NativePlungePhase Phase;
        /// <summary>Seconds since the swing's start; the windup's tuck (0..1); the drop's commitment from the tuck (0..1); seconds
        /// since the drop started (0 before).</summary>
        public float Age,Tuck,Commit,Fall;
        /// <summary>Seconds since the impact (NaN before); the blade driven in (0..1); the pull-out (0..1, 1 = free); the
        /// settle back to the guard after it is free (0..1).</summary>
        public float Land,Plant,Pull,Settle;
        /// <summary>The fall height (m) and its share of the 2-8 m bonus range (0..1); the impact, pull-out start, free and end times.</summary>
        public float Height,Heavy,ImpactAt,PullAt,FreeAt,Until;
        /// <summary>The blade is in the ground (from the plant until it comes free).</summary>
        public bool Stuck=>Phase==NativePlungePhase.Plant||Phase==NativePlungePhase.Stuck||Phase==NativePlungePhase.Pull;
        public bool Landed=>Phase>=NativePlungePhase.Plant;
    }

    /// <summary>
    /// The plunging stab's presentation rules: the spec's numbers (docs/NEW-KITS.md section 1, Astra's to tune: .08 s windup, a
    /// straight drop at 24 m/s keeping .15 of the horizontal speed, rings of .9/1/1.2/1.4 m, stuck recoveries of .3/.45/.5/.7 s),
    /// the classification every presenter shares, and the pose timeline keyed to the replicated times. Presentation only:
    /// nothing here is read by the authority, and the rule's own numbers stay Astra's.
    /// </summary>
    public static class NativePlungeRules
    {
        /// <summary>The spec's proposal (fixtures and the fallback when a replicated time is missing).</summary>
        public const float Windup=.08f,DropSpeed=-24,HorizontalKeep=.15f,MinimumAir=.15f,PitchGate=-45,HeightFrom=2,HeightTo=8;
        public static bool Handles(string spec)=>spec=="sword"||spec=="daggers"||spec=="shield"||spec=="greatsword";
        public static float Radius(string spec)=>spec=="daggers"?.9f:spec=="sword"?1f:spec=="shield"?1.2f:spec=="greatsword"?1.4f:1;
        public static float Recovery(string spec)=>spec=="daggers"?.3f:spec=="sword"?.45f:spec=="shield"?.5f:spec=="greatsword"?.7f:.45f;
        /// <summary>The height bonus range as a 0..1 weight: 0 at 2 m (and below), 1 at 8 m and above (the +60% cap).</summary>
        public static float Heavy(float height)=>MathUtil.Finite(height)?MeleeMath.Clamp((height-HeightFrom)/(HeightTo-HeightFrom),0,1):0;

        // ---- classification ----
        /// <summary>
        /// What a swing shows: `down` is the replicated kind (NativePlungeData.Down), `explicitAir` the jump attack's flag (1, 0,
        /// or -1 for none: the presented airtime decides), `sprintAttack` the sprint flag. A sprint attack is never an air attack,
        /// a plunging stab is never the forward attack and never comes from the airtime alone, and a swing begun while a special,
        /// stun, roll, juggle or knockdown owns the pilot is neither.
        /// </summary>
        public static NativeAirStrike Classify(bool down,int explicitAir,bool airborne,bool sprintAttack,bool owned)
        {
            if(owned||sprintAttack)return NativeAirStrike.None;
            if(down)return NativeAirStrike.Down;
            return NativeAirAttackRules.Air(explicitAir,airborne)?NativeAirStrike.Forward:NativeAirStrike.None;
        }
        /// <summary>The forward jump attack's explicit flag for its watch (NativeAirAttackRules.Air): 0 for a plunging stab or a
        /// sprint attack, so neither ever shows as the forward attack or leaves its impact.</summary>
        public static int ForwardFlag(int explicitAir,bool sprintAttack,bool down)=>sprintAttack||down?0:explicitAir;

        // ---- the timeline (replicated times; MeleeMotion, the first-person view, the effects and the cues all read it) ----
        /// <summary>The drop's commitment from the tuck, the plant (the blade driven in, deeper for bigger falls), the strain's
        /// shiver while stuck (rad) and the windup's share of the swing that the guard keeps.</summary>
        public const float CommitBlend=.06f,PlantSeconds=.045f,HeavyPlant=.035f,Strain=.025f;
        /// <summary>The share of the stuck recovery at which the pull-out starts and the blade comes free (the greatsword
        /// wrenches free last: latest in share and in seconds).</summary>
        public static float PullShare(string spec)=>spec=="daggers"?.42f:spec=="greatsword"?.55f:.48f;
        public static float FreeShare(string spec)=>spec=="daggers"?.68f:spec=="greatsword"?.86f:spec=="shield"?.76f:.74f;
        /// <summary>A hit this long after the landing still belongs to the impact.</summary>
        public const float HitGrace=.1f;
        /// <summary>
        /// The pose timeline at `time`: the windup's tuck from `at` to `plungeAt` (the spec's .08 s when not replicated), the
        /// committed drop until `impactAt` (NaN while falling), the plant, the stuck hold, the pull-out and the settle over the
        /// recovery that ends at `until` (the spec's when not replicated). False before the swing and after its end.
        /// </summary>
        public static bool Clock(string spec,float at,float plungeAt,float impactAt,float until,float height,float time,out NativePlungeClock c)
        {
            c=default;c.Land=float.NaN;c.ImpactAt=c.PullAt=c.FreeAt=c.Until=float.NaN;
            if(!(at>-90)||!MathUtil.Finite(time)||time<at-1e-4f)return false;
            float pa=MathUtil.Finite(plungeAt)&&plungeAt>at?plungeAt:at+Windup;
            c.Age=time-at;c.Height=MathUtil.Finite(height)?Math.Max(0,height):float.NaN;c.Heavy=Heavy(height);
            c.Tuck=MeleeMath.Ease(c.Age/(pa-at));
            if(time<pa){c.Phase=NativePlungePhase.Windup;return true;}
            c.Fall=time-pa;c.Commit=MeleeMath.Ease(c.Fall/CommitBlend);
            if(!MathUtil.Finite(impactAt)||time<impactAt){c.Phase=NativePlungePhase.Drop;return true;}
            float r=MathUtil.Finite(until)&&until>impactAt+.05f?until-impactAt:Recovery(spec),plant=PlantSeconds+HeavyPlant*c.Heavy;
            c.ImpactAt=impactAt;c.Until=impactAt+r;c.PullAt=impactAt+Math.Max(plant,r*PullShare(spec));c.FreeAt=Math.Max(c.PullAt+.05f,impactAt+r*FreeShare(spec));
            if(time>=c.Until)return false;
            c.Land=time-impactAt;c.Plant=MeleeMath.Ease(c.Land/plant);c.Commit=1;c.Tuck=1;
            c.Pull=MeleeMath.Smooth((time-c.PullAt)/(c.FreeAt-c.PullAt));c.Settle=MeleeMath.Ease((time-c.FreeAt)/Math.Max(.02f,c.Until-c.FreeAt));
            c.Phase=time<impactAt+plant?NativePlungePhase.Plant:time<c.PullAt?NativePlungePhase.Stuck:time<c.FreeAt?NativePlungePhase.Pull:NativePlungePhase.Settle;
            return true;
        }
        /// <summary>The state label the rigs show (MeleeMotion.State, the first-person view).</summary>
        public static string Label(NativePlungePhase p)
        {
            switch(p){case NativePlungePhase.Windup:return "plunge windup";case NativePlungePhase.Drop:return "plunge drop";case NativePlungePhase.Plant:return "plunge impact";
                case NativePlungePhase.Stuck:return "plunge stuck";case NativePlungePhase.Pull:return "plunge pull-out";case NativePlungePhase.Settle:return "plunge recovery";default:return "plunge";}
        }
        /// <summary>How much faster the first-person speed lines stream through the drop than through a sprint.</summary>
        public const float LineRate=1.8f;
        /// <summary>The first-person speed lines' weight: in over the windup, full through the drop, out over .12 s after the
        /// impact (NativeSprintFx's screen, Reduced effects dimming it as the sprint's).</summary>
        public static float ScreenWeight(in NativePlungeClock c)
        {
            switch(c.Phase)
            {
                case NativePlungePhase.Windup:return .35f*c.Tuck;
                case NativePlungePhase.Drop:return .35f+.65f*MeleeMath.Ease(c.Fall/.08f);
                case NativePlungePhase.Plant:case NativePlungePhase.Stuck:return 1-MeleeMath.Ease(c.Land/.12f);
                default:return 0;
            }
        }
    }

    /// <summary>
    /// One presented pilot's plunging stabs, from its NativePlungeData and its presented root: the swing being drawn, its
    /// replicated drop and impact times (the impact event or the presented landing stand in until ImpactAt arrives), the fall
    /// height (replicated, else measured from the presented root between the drop's start and the landing), and one count per
    /// drop, impact and pull-out for the effects and cues. The rig, the first-person view, the effects and the audio each keep
    /// one over the same presented state. A rewind or a gap restarts it. No allocation.
    /// </summary>
    public sealed class NativePlungeWatch
    {
        public bool Down;public string Spec;
        public float At=-100,PlungeAt=float.NaN,ImpactAt=float.NaN,Until=float.NaN,Height=float.NaN,Time=-100,TopY=float.NaN,FloorY=float.NaN;
        public int Drops,Impacts,Frees;
        private bool dropped,impacted,freed;
        public void Reset(){Down=false;Spec=null;At=-100;PlungeAt=ImpactAt=Until=Height=TopY=FloorY=float.NaN;Time=-100;dropped=impacted=freed=false;}
        /// <summary>`d` this frame's data, `rootY` the presented root height (any fixed offset), `grounded` the replicated contact,
        /// `owned` whether a special, stun, roll, juggle or knockdown owns the pilot (a swing begun so is no plunge).</summary>
        public void Observe(in NativePlungeData d,string spec,float rootY,bool grounded,float time,bool owned=false)
        {
            if(!MathUtil.Finite(time))return;
            if(Time<-90||time<Time-MeleeGait.REWIND||time-Time>.35f){Reset();Time=time;}
            Time=Math.Max(Time,time);
            if(d.At>-90&&d.At!=At&&time>=d.At-1e-4f)
            {
                At=d.At;Spec=spec;Down=d.Down&&!owned&&NativePlungeRules.Handles(spec);PlungeAt=At+NativePlungeRules.Windup;ImpactAt=Until=Height=TopY=FloorY=float.NaN;dropped=impacted=freed=false;
            }
            if(!Down)return;
            if(d.At==At)
            {
                if(!d.Down){Down=false;return;}
                if(MathUtil.Finite(d.PlungeAt)&&d.PlungeAt>At)PlungeAt=d.PlungeAt;
                if(MathUtil.Finite(d.ImpactAt)&&d.ImpactAt>=At&&(float.IsNaN(ImpactAt)||!impacted))ImpactAt=Math.Max(d.ImpactAt,PlungeAt);
                if(MathUtil.Finite(d.Until))Until=d.Until;
                if(MathUtil.Finite(d.Height))Height=Math.Max(0,d.Height);
            }
            if(MathUtil.Finite(rootY)&&!impacted)TopY=float.IsNaN(TopY)?rootY:Math.Max(TopY,rootY);
            if(!dropped&&time>=PlungeAt){dropped=true;Drops++;}
            // The presented landing stands in for a replicated impact time not here yet (a guest's interpolated root).
            if(dropped&&float.IsNaN(ImpactAt)&&grounded&&time>PlungeAt+.02f)ImpactAt=time;
            if(!impacted&&MathUtil.Finite(ImpactAt)&&time>=ImpactAt)
            {
                impacted=true;Impacts++;FloorY=rootY;if(float.IsNaN(Height)&&MathUtil.Finite(TopY))Height=Math.Max(0,TopY-rootY);
            }
            if(impacted&&!freed&&NativePlungeRules.Clock(Spec,At,PlungeAt,ImpactAt,Until,Height,time,out var c)&&c.Phase==NativePlungePhase.Settle){freed=true;Frees++;}
        }
        /// <summary>The impact event for this pilot (NativePlungeFields.Impact): its time and height stand in for the actor's.</summary>
        public void ImpactEvent(float at,float height)
        {
            if(!Down||!(at>=At))return;if(float.IsNaN(ImpactAt)||!impacted)ImpactAt=Math.Max(at,PlungeAt);if(MathUtil.Finite(height)&&float.IsNaN(Height))Height=height;
        }
        /// <summary>The current plunge's timeline at `time` (false outside one).</summary>
        public bool Clock(float time,out NativePlungeClock c){c=default;return Down&&NativePlungeRules.Clock(Spec,At,PlungeAt,ImpactAt,Until,Height,time,out c);}
        /// <summary>Whether the plunge being watched is the swing that started at `attackAt`.</summary>
        public bool Is(float attackAt)=>Down&&At==attackAt;
    }
}
