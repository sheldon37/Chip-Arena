using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The heavy kinds' proposed moves (owner 2026-10-14: "I'd like them to all have a charge animation (they can charge through
    /// walls) and a couple of punch animations (like a straight punch, and a swipe)"; and from the Windows test build: "The hulk
    /// needs to punch and charge at the player too, not just the wall. The charge is avoidable if they move out of the way in
    /// time. (Hulk can collide and break walls too.) They literally can charge from across the map to the player breaking the
    /// walls if they are hunting them and 'know' where they are." / "Its currently only using the ground slam"). The
    /// presentation's side of a contract the rules do not implement yet: contract v2, docs/SIREN-ZOMBIES.md section 10.5,
    /// PROPOSED, Astra to confirm (Astra's implementation wins where it differs; mirror its numbers here). The move ids, their
    /// timings and the proposed numbers the animation is authored to, and the replicated state the presentation reads. Nothing
    /// here is read by the authority today; no hit timing, hit volume, damage, cooldown, movement or network field changes until
    /// the rules adopt it. Engine free, no clock, no allocation.
    /// <list type="bullet">
    /// <item>Attack moves (<see cref="Move"/>), chosen by the authority when an attack starts and replicated with LastAttack
    /// (proposed <c>NativeMinionFrame.move</c>): at the hunted pilot a straight punch (in the punch's cone and reach) or a swipe
    /// (close or wide of it); the content's smash for two or more pilots in its disc, walls and props. The arm is
    /// <see cref="Side"/> of LastAttack on every peer, so no side field is needed.</item>
    /// <item>The charge at the hunted pilot (<see cref="ChargeEnd"/>): started when the Hulk hunts a pilot it knows the position of
    /// (line of sight now, or within <see cref="ChargeMemory"/>, or the Siren/director hunt state; the authority defines "knows"),
    /// at least <see cref="ChargeMinRange"/> away, off cooldown; no short maximum range: it may cross the map. The wind-up
    /// (<see cref="ChargeWind"/>) turns to track the target; its heading LOCKS at the wind-up's end (replicated through the add's
    /// yaw) and it runs straight at <see cref="ChargeSpeed"/>, no homing, so a pilot who leaves the lane in time is missed. It
    /// breaks the breakable walls, blocks and props in its lane on contact without stopping (slowed to <see cref="ChargeSlow"/>
    /// for <see cref="ChargeSlowFor"/> at each, replicated as the existing breach / structure records and events), and ends on a
    /// pilot hit, an unbreakable solid (stunned), or spent (<see cref="ChargeOvershoot"/> past the locked target point, or the
    /// longest run <see cref="ChargeRun"/>). Replicated as its start, its end, how it ended and the hunted pilot (proposed
    /// <c>chargeAt</c>, <c>chargeEndAt</c>, <c>chargeEnd</c>, <c>chargeTarget</c>).</item>
    /// <item>Until the frame fields exist the presentation also accepts the same state as Impact events (<see cref="MoveEvent"/>,
    /// <see cref="ChargeEvent"/>, <see cref="ChargeEndEvent"/>); with neither, every Hulk plays only what the rules drive today.
    /// Core/NativeHulkCharge tracks a charge from either.</item>
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
        // ---- contract v2 (proposed, Astra to confirm): the attack at the hunted pilot ----
        /// <summary>The punch's cone (degrees either side of the facing) and its least range (m): the pilot straight ahead at the
        /// fist's reach. Closer, or wide of the cone, the swipe.</summary>
        public const float PunchCone=20,PunchMin=1.2f;
        /// <summary>
        /// The proposed move when an attack starts (Astra's to adopt or replace): the hunted pilot `distance` m from the root
        /// (horizontal) at `angle` degrees off the facing, `inDisc` pilots inside the smash's disc. Two or more in the disc: the
        /// smash (it hits them all). The pilot in the punch's cone between <see cref="PunchMin"/> and <see cref="PunchReach"/>: the
        /// punch. Within <see cref="SwipeRadius"/> inside <see cref="SwipeArc"/>: the swipe. Otherwise (walls, props, a pilot out
        /// of both) the content's smash, as today. Crawling or armless: the content's attack.
        /// </summary>
        public static Move Choose(float distance,float angle,int inDisc,bool crawl,int lost)
        {
            if(crawl||NativeHordeKinds.Armless(lost)||inDisc>=2||float.IsNaN(distance))return Move.Content;float a=Math.Abs(angle);
            if(a<=PunchCone&&distance>=PunchMin&&distance<=PunchReach)return Move.Punch;
            if(a<=SwipeArc*.5f&&distance<=SwipeRadius)return Move.Swipe;
            return Move.Content;
        }
        /// <summary>Whether a pilot (radius `pilotRadius`) at (x right, z ahead) m in the add's facing frame, `dy` m above its floor,
        /// is inside the proposed punch capsule / swipe sector (the presentation's guess of the authority's hit, for the view kick
        /// and the burst at the struck pilot; the authority's own test decides the real hit).</summary>
        public static bool InPunch(float x,float z,float dy,float pilotRadius)
        {
            if(Math.Abs(dy)>=2.5f)return false;float r=PunchRadius+pilotRadius,along=Math.Max(PunchFrom,Math.Min(PunchReach,z));return x*x+(z-along)*(z-along)<=r*r;
        }
        public static bool InSwipe(float x,float z,float dy,float pilotRadius)
        {
            if(Math.Abs(dy)>=2.5f)return false;float d2=x*x+z*z,r=SwipeRadius+pilotRadius;if(d2>r*r)return false;
            return d2<1e-4f||Math.Abs(Math.Atan2(x,z))*180/Math.PI<=SwipeArc*.5f;
        }
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
        /// <summary>Contract v2 (proposed, Astra to confirm): the wind-up (s, standing, turning to track the target; the heading locks
        /// at its end), the run's speed (m/s: past the Hulk's 1.15 m/s run, a little under a sprinting pilot's 6.8, so it crosses a
        /// 40 m hall in under 7 s and is beaten by leaving its lane, not by outrunning it), the longest run (s: 96 m, across Ward
        /// Zero's 80 m; normally it is spent well before, past the locked target point), the least range it
        /// starts from (m; no maximum: it may cross the map), how far past the locked target point it overshoots before it is spent
        /// (m), how long a pilot out of sight is still known (s), and the cooldown after the end (s).</summary>
        public const float ChargeWind=.9f,ChargeSpeed=6f,ChargeRun=16f,ChargeMinRange=7,ChargeOvershoot=4,ChargeMemory=2.5f,ChargeCooldown=10;
        /// <summary>Each breakable it crashes through slows the run to this share of its speed for this long (s), then it runs on.</summary>
        public const float ChargeSlow=.6f,ChargeSlowFor=.3f;
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
        /// <summary>Impact Detail at the charge's start (the wind-up's): Target the add, Amount the hunted pilot's seat id (0 none),
        /// Position its start, Normal its facing then (the locked heading is the add's yaw at ChargeWind), Time the start.</summary>
        public const string ChargeEvent="hulk-charge";
        /// <summary>Impact Detail at the charge's end: Target the add, Amount how it ended (1 pilot, 2 solid, 3 spent), Position the
        /// contact, Time the end.</summary>
        public const string ChargeEndEvent="hulk-charge-end";
    }
}
