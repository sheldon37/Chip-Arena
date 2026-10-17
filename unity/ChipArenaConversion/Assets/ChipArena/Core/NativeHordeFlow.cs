using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's presentation clock (owner 2026-10-17: "fix the zombies animations ... Improve these vastly"; docs/ZOMBIE-MOTION.md).
    /// One per presentation: every horde add's gait, attacks, reactions, entrances and deaths are posed on it, never on arena.Time
    /// (a guest's arena.Time only moves when a 20 Hz frame lands and leads its drawn bodies by the render delay; the host's is the
    /// 60 Hz fixed step, so at 144 fps two or three frames showed one pose). It follows the arena's field clock (the replica render
    /// time on a guest, the visual clock on the host, the replay's clock in the killcam):
    /// <list type="bullet">
    /// <item>on the host (and offline) exactly;</item>
    /// <item>on a guest it rides through the replica clock's stalls (NativeSnapshotClock clamps at the newest frame, so a late
    /// frame stops it and the next starts it again at the same rate) at a rate within <see cref="MaxSkew"/> of real time, the rate
    /// itself changing at most <see cref="SkewRate"/> per second, and eases back onto the field clock afterwards; a gap past
    /// <see cref="SnapError"/> (a stall, a rewind, a new arena) snaps.</item>
    /// <item>A guest's clock also slows as it nears the newest replicated frame (<see cref="Lead"/> past it, over
    /// <see cref="SoftZone"/>, never below <see cref="MinRate"/> of real time), so a late frame slows the adds for a moment instead
    /// of letting them run on far past what anyone has seen and then surge to catch up; it speeds back up at most
    /// <see cref="RateSlew"/> per second when the frame lands.</item>
    /// </list>
    /// Presentation only: it never feeds the authority. Fixed fields, no allocation.
    /// </summary>
    public sealed class HordeClock
    {
        public float MaxSkew=.1f,SkewRate=.8f,CatchUp=2.5f,SnapError=.3f,Lead=.04f,SoftZone=.05f,MinRate=.3f,RateSlew=6f;
        /// <summary>The clock (s, on the authority's timeline) and its rate's offset from real time (0 holds pace).</summary>
        public double Time{get;private set;}
        public double Skew=>skew;
        public bool Started=>started;
        private double skew,rate=1;private bool started;
        /// <summary>The clock's rate against real time this frame (1 holds pace), for the checks.</summary>
        public double Rate=>rate;
        public void Reset(){started=false;skew=0;rate=1;Time=0;}
        /// <summary>
        /// Advances to the field clock `target` over `dt` real seconds; `smooth` rides through its stalls (a guest), `newest` the
        /// newest replicated frame's time (a guest; infinity: none).
        /// </summary>
        public double Step(double target,float dt,bool smooth,double newest=double.PositiveInfinity)
        {
            if(double.IsNaN(target)||double.IsInfinity(target))return Time;
            double error=target-Time;
            if(!started||!smooth||Math.Abs(error)>SnapError){Time=target;skew=0;rate=1;started=true;return Time;}
            if(!(dt>0))return Time;
            double want=Math.Max(-MaxSkew,Math.Min(MaxSkew,error*CatchUp)),step=SkewRate*dt;
            skew+=Math.Max(-step,Math.Min(step,want-skew));double goal=1+skew;
            if(!double.IsNaN(newest)&&!double.IsInfinity(newest)){double room=newest+Lead-Time;if(room<SoftZone)goal*=Math.Max(MinRate,room/SoftZone);}
            double slew=RateSlew*dt;rate+=Math.Max(-slew,Math.Min(slew,goal-rate));if(rate<MinRate)rate=MinRate;
            Time+=dt*rate;return Time;
        }
    }

    /// <summary>
    /// One horde add's presented root: where its rig stands and faces this frame, as a continuous path. The boss motion library's
    /// follow (Core/NativeBossMotionFollow: BossRootFollow) under the horde's own tuning per kind:
    /// <list type="bullet">
    /// <item>A guest: the add's replicated snapshots (authority time, position, yaw) as a C1 Hermite curve sampled at the horde
    /// clock, past the newest held then eased to rest (no freeze, no stair step), through two goal-velocity springs.</item>
    /// <item>The host (or a guest before an add's first snapshot): the body as the 60 Hz simulation left it, carried forward by its
    /// velocity to the horde clock (at most one fixed step), through the same springs, so it moves every rendered frame.</item>
    /// <item>The facing: the shortest arc on the rate- and acceleration-limited turn (<see cref="BossTurn"/>; faster while winding
    /// up an attack, so the blow faces where the authority aims it), never the authority's snap.</item>
    /// <item>The lean: banked into a turn and pitched into speeding up and braking on second-order dynamics.</item>
    /// <item>Speed: from the smoothed root (<see cref="Velocity"/>), not differentiated from the drawn rig.</item>
    /// </list>
    /// A jump beyond 5 m (a teleport, a placement) snaps and sets <see cref="Jumped"/> for that frame. Presentation only: it reads
    /// what every peer holds and writes nothing back (hitboxes, colliders and movement stay the authority's). No allocation.
    /// </summary>
    public sealed class HordeFlow
    {
        public readonly BossRootFollow Follow=new BossRootFollow();
        // ---- output (Unity world: the body's centre where the rig root stands; yaw in degrees as the authority writes it) ----
        public Vector3 Position,Velocity;public float Yaw,YawRate,Bank,Pitch;public bool Jumped;
        public float Speed=>BossMath.FlatLength(Velocity);
        public int Snapshots=>Follow.Snapshots.Count;
        private int kind=-1;private bool hostKnown,winding;private Vector3 hostLast,hostVelocity;private double hostAt;private float turnRate,turnAccel;
        /// <summary>The per-kind turn (rad/s, rad/s^2), lean (bank cap, lean Hz, damping, pitch scale) and smoothing (half-life).</summary>
        public struct Tune{public float Turn,Accel,Bank,Hz,Damping,PitchScale,SpeedPitch,HalfLife;}
        // shambler, runner, brute, spitter, crawler, bloater, Hulk (the Hulk's own pivot steps turn it: its follow passes the turn on)
        private static readonly Tune[] tunes={
            new Tune{Turn=4.2f,Accel=20,Bank=.12f,Hz=1.5f,Damping=.45f,PitchScale=.55f,SpeedPitch=.012f,HalfLife=.12f},
            new Tune{Turn=6.5f,Accel=38,Bank=.2f,Hz=1.9f,Damping=.5f,PitchScale=.7f,SpeedPitch=.016f,HalfLife=.1f},
            new Tune{Turn=3.0f,Accel=11,Bank=.09f,Hz=1.15f,Damping=.5f,PitchScale=.45f,SpeedPitch=.01f,HalfLife=.14f},
            new Tune{Turn=4.6f,Accel=24,Bank=.12f,Hz=1.6f,Damping=.45f,PitchScale=.55f,SpeedPitch=.012f,HalfLife=.12f},
            new Tune{Turn=6.0f,Accel=34,Bank=.14f,Hz=2.0f,Damping=.55f,PitchScale=.35f,SpeedPitch=.006f,HalfLife=.1f},
            new Tune{Turn=2.6f,Accel=9,Bank=.08f,Hz=1.0f,Damping=.4f,PitchScale=.4f,SpeedPitch=.008f,HalfLife=.15f},
            new Tune{Turn=8f,Accel=40,Bank=.06f,Hz=.85f,Damping=.55f,PitchScale=.3f,SpeedPitch=.006f,HalfLife=.15f}};
        /// <summary>A kind's tuning (a zone kind's is its base body's).</summary>
        public static Tune TuneOf(int kind)=>tunes[Math.Max(0,Math.Min(tunes.Length-1,NativeHordeKinds.Base(NativeHordeKinds.Index(kind))))];
        /// <summary>How much faster it turns while winding up an attack (rate, acceleration).</summary>
        public const float WindTurn=1.8f,WindAccel=2.2f;
        public void Reset(){Follow.Reset();kind=-1;hostKnown=false;Jumped=false;}
        /// <summary>Takes a kind's tuning (once per kind; a preview swap re-tunes).</summary>
        public void Configure(int kind)
        {
            if(kind==this.kind)return;this.kind=kind;var t=TuneOf(kind);var f=Follow;
            f.PositionHalfLife=t.HalfLife;f.MaxExtrapolate=.3f;f.TurnHalfLife=.09f;turnRate=t.Turn;turnAccel=t.Accel;f.MaxTurnRate=t.Turn;f.MaxTurnAccel=t.Accel;
            f.BankScale=1;f.MaxBank=t.Bank;f.PitchScale=t.PitchScale;f.SpeedPitch=t.SpeedPitch;f.MaxPitch=.12f;f.LeanFrequency=t.Hz;f.LeanDamping=t.Damping;
        }
        /// <summary>A replicated snapshot of the add (guest): the frame's authority time, the body's position, the yaw (degrees).</summary>
        public void Observe(double time,Vector3 position,float yawDegrees)=>Follow.Observe(time,time,position,yawDegrees*BossMath.Deg);
        private void Turning(bool fast)
        {
            if(fast==winding)return;winding=fast;Follow.MaxTurnRate=turnRate*(fast?WindTurn:1);Follow.MaxTurnAccel=turnAccel*(fast?WindAccel:1);
        }
        /// <summary>A guest frame: the snapshots at `clock` (the horde clock), `dt` real seconds after the last.</summary>
        public void Replica(double clock,float dt,bool fast){Turning(fast);Follow.StepAt(clock,dt);Out();}
        /// <summary>
        /// The host (or a peer with no snapshots for the add): the body at `bodyTime` (the simulation's time) at `body` with its
        /// velocity (`velocityKnown` false: a kinematic body; its fixed steps give it), carried to `clock` and followed.
        /// </summary>
        public void Host(double clock,double bodyTime,Vector3 body,Vector3 velocity,bool velocityKnown,float yawDegrees,float dt,bool fast)
        {
            Turning(fast);bool snap=false;velocity=BossMath.Flat(velocity);if(!Finite(velocity))velocity=Vector3.Zero;
            if(!hostKnown||bodyTime<hostAt-1e-6||Vector3.Distance(body,hostLast)>Follow.Snapshots.TeleportDistance){hostKnown=true;hostLast=body;hostAt=bodyTime;hostVelocity=velocityKnown?velocity:Vector3.Zero;snap=Follow.Started;}
            else if(bodyTime>hostAt+1e-6){var est=BossMath.Flat(body-hostLast)/(float)(bodyTime-hostAt);hostVelocity=velocityKnown?velocity:est;hostLast=body;hostAt=bodyTime;}
            // (At most one fixed step ahead: the visual clock never runs further past the simulation.)
            float ahead=(float)Math.Max(0,Math.Min(.034,clock-bodyTime));
            Follow.Follow(body+hostVelocity*ahead,hostVelocity,yawDegrees*BossMath.Deg,dt,snap);Out();
        }
        private void Out()
        {
            var f=Follow;Position=f.Position;Velocity=f.Velocity;Yaw=BossMath.WrapDeg(f.Yaw/BossMath.Deg);YawRate=f.YawRate;Bank=f.Bank;Pitch=f.Pitch;Jumped=f.Jumped;
            if(!Finite(Position)){Position=hostLast;Velocity=Vector3.Zero;}
        }
        private static bool Finite(Vector3 v)=>!(float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z));
    }
}
