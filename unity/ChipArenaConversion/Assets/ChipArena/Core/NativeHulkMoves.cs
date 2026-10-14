using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The heavy kinds' proposed moves (owner 2026-10-14: "I'd like them to all have a charge animation (they can charge through
    /// walls) and a couple of punch animations (like a straight punch, and a swipe)"). The presentation's side of a contract the
    /// rules do not implement yet (docs/SIREN-ZOMBIES.md section 10 hands it to Astra): the move ids, their timings and the
    /// proposed numbers the animation is authored to, and the replicated state the presentation reads. Nothing here is read by
    /// the authority today; no hit timing, hit volume, damage, cooldown, movement or network field changes until the rules adopt
    /// it. Engine free, no clock, no allocation.
    /// <list type="bullet">
    /// <item>Attack moves (<see cref="Move"/>): the content's attack (the smash, as today), a straight punch and a swipe, chosen
    /// by the authority when an attack starts and replicated with LastAttack (proposed <c>NativeMinionFrame.move</c>). The
    /// arm is <see cref="Side"/> of LastAttack on every peer, so no side field is needed.</item>
    /// <item>The charge (<see cref="ChargeEnd"/>): a wind-up, a lowered-shoulder run that breaks through eligible walls without
    /// stopping, and an end (a pilot hit, a solid stop, or spent), replicated as its start, its end and how it ended (proposed
    /// <c>chargeAt</c>, <c>chargeEndAt</c>, <c>chargeEnd</c>).</item>
    /// <item>Until the frame fields exist the presentation also accepts the same state as Impact events (<see cref="MoveEvent"/>,
    /// <see cref="ChargeEvent"/>, <see cref="ChargeEndEvent"/>); with neither, every Hulk plays only what the rules drive today.</item>
    /// </list>
    /// </summary>
    public static class NativeHulkMoves
    {
        /// <summary>The attack an add plays at LastAttack (proposed <c>NativeMinionFrame.move</c>; 0 is today's content attack).</summary>
        public enum Move{Content=0,Punch=1,Swipe=2}
        /// <summary>Kinds that play the keyed heavy moves (the Hulk; the brute when the rules give it the same moves).</summary>
        public static bool Heavy(int kind)=>kind==NativeHordeKinds.Hulk||kind==NativeHordeKinds.Brute;
        public static Move Of(int id)=>id==1?Move.Punch:id==2?Move.Swipe:Move.Content;

        // ---- the straight punch (proposed rules; the animation's key poses are authored to these times) ----
        /// <summary>LastAttack to the fist's full extension (s), and to the add moving again.</summary>
        public const float PunchHit=.62f,PunchCycle=2f;
        /// <summary>The punch's hit volume: a capsule along the facing from <see cref="PunchFrom"/> to <see cref="PunchReach"/> m ahead,
        /// radius <see cref="PunchRadius"/>, |dy| under 2.5 m, melee line of sight; damage, impulse along the facing, upward pop.</summary>
        public const float PunchFrom=.6f,PunchReach=3.4f,PunchRadius=.75f,PunchDamage=24,PunchImpulse=750,PunchPop=2.5f;
        // ---- the swipe ----
        /// <summary>LastAttack to the arm crossing the facing (s), and to the add moving again.</summary>
        public const float SwipeHit=.78f,SwipeCycle=2.4f;
        /// <summary>The swipe's hit volume: a sector of <see cref="SwipeRadius"/> m, <see cref="SwipeArc"/> degrees about the facing,
        /// |dy| under 2.5 m; damage and an impulse across the facing in the sweep's direction (from the swinging arm's side), part
        /// outward, with an upward pop.</summary>
        public const float SwipeRadius=3.2f,SwipeArc=150,SwipeDamage=20,SwipeImpulse=650,SwipeOutward=.4f,SwipePop=2;
        /// <summary>A move's strike after LastAttack: the move's own, or the content attack's (crawling or armless: .34, as today).</summary>
        public static float Strike(Move m,float contentStrike,bool crawl,int lost)=>crawl||NativeHordeKinds.Armless(lost)||m==Move.Content?contentStrike:m==Move.Punch?PunchHit:SwipeHit;
        /// <summary>When the add moves again after LastAttack (the authority's <c>max(cycle, hit + .05)</c> on the move's own cycle).</summary>
        public static float Recover(Move m,float contentRecover,bool crawl,int lost)=>crawl||NativeHordeKinds.Armless(lost)||m==Move.Content?contentRecover:Math.Max(m==Move.Punch?PunchCycle:SwipeCycle,Strike(m,0,false,0)+.05f);
        /// <summary>The arm a blow swings (1 the right, -1 the left): a hash of LastAttack, the one arm left when the other is gone.
        /// Every peer and the authority compute the same from the replicated LastAttack.</summary>
        public static int Side(float lastAttack,int lost)
        {
            int side=((int)(lastAttack*7.31f)&1)==0?1:-1;
            if(side==1&&NativeHordeKinds.RightArmless(lost))side=-1;else if(side==-1&&NativeHordeKinds.LeftArmless(lost))side=1;return side;
        }

        // ---- the charge ----
        /// <summary>How a charge ended (proposed <c>NativeMinionFrame.chargeEnd</c>): still running, it hit a pilot (stops, shoves),
        /// it hit something solid it cannot break (stunned), or it ran its distance out (slows to a stop).</summary>
        public enum ChargeEnd{Running=0,Pilot=1,Solid=2,Spent=3}
        public static ChargeEnd EndOf(int id)=>id==1?ChargeEnd.Pilot:id==2?ChargeEnd.Solid:id==3?ChargeEnd.Spent:ChargeEnd.Running;
        /// <summary>The wind-up (s, standing, the heading locked at its start), the run's speed (m/s) and longest run (s), the range
        /// it starts from (m) and its cooldown (s).</summary>
        public const float ChargeWind=.9f,ChargeSpeed=4.5f,ChargeRun=2.4f,ChargeMinRange=7,ChargeMaxRange=16,ChargeCooldown=9;
        /// <summary>The run's hit on a pilot: damage, an impulse along the heading, an upward pop (a block halves the shove).</summary>
        public const float ChargeDamage=28,ChargeImpulse=1000,ChargePop=4.5f;
        /// <summary>How long the add is held after the end (s): the impact's recoil, the stun of a solid stop, the slowing of a spent run.</summary>
        public static float Stop(ChargeEnd how)=>how==ChargeEnd.Pilot?1f:how==ChargeEnd.Solid?1.6f:how==ChargeEnd.Spent?.7f:0;
        /// <summary>The phase of a charge `age` s after its start (`endAge` s after its end; NaN while running).</summary>
        public enum ChargePhase{None,Wind,Run,Stop}
        public static ChargePhase PhaseOf(float age,float endAge,ChargeEnd how)
        {
            if(float.IsNaN(age)||age<0)return ChargePhase.None;
            if(!float.IsNaN(endAge)&&endAge>=0)return endAge<Stop(how)+BlendOut?ChargePhase.Stop:ChargePhase.None;
            return age<ChargeWind?ChargePhase.Wind:ChargePhase.Run;
        }
        /// <summary>A move's blend out after its end (s).</summary>
        public const float BlendOut=.3f;

        // ---- the events the presentation accepts until the frame fields exist ----
        /// <summary>Impact Detail at LastAttack: Target the add, Amount the move id (1 punch, 2 swipe), Time LastAttack.</summary>
        public const string MoveEvent="hulk-move";
        /// <summary>Impact Detail at the charge's start: Target the add, Position its start, Normal its heading, Time the start.</summary>
        public const string ChargeEvent="hulk-charge";
        /// <summary>Impact Detail at the charge's end: Target the add, Amount how it ended (1 pilot, 2 solid, 3 spent), Position the
        /// contact, Time the end.</summary>
        public const string ChargeEndEvent="hulk-charge-end";
    }
}
