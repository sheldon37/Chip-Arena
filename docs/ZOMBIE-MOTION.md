# Zombie motion (the horde's animation pass)

Owner request (2026-10-17): "Yes fix the zombies animations (Design as well, it seems they are still on the lower quality
end) Improve these vastly." This document covers the motion half (Unity Chip Arena, `unity/ChipArenaConversion`). The
design half (models and materials) is a separate branch, `claude/zombie-design`.

The pass changes presentation only. It reads replicated state and the presentation clock, and writes a pose and the rig's
drawn root and facing. It changes no rule, hit volume, collider, damage, timing, cooldown, movement, network message or
authority state. The bodies and colliders stay where the authority puts them; only the drawn rig moves differently.
It covers every kind posed by the horde presentation: the Siren's seven (shambler, runner, brute, spitter, crawler,
bloater, Hulk) and the zone kinds that reuse their gait. Status: on branch `claude/zombie-motion`, not merged, not
released.

## What was wrong (measured)

The offline harness runs the same Core code under an emulated 60 Hz authority and a 20 Hz network with jitter and late
frames, at 144 fps with ±15% frame times. It found these problems in the shipped motion (base `f3d8eb4`):

1. **The pose clock stepped.** Adds were posed at `arena.Time`. On a guest that is the newest frame, so the pose held on
   88% of rendered frames and then jumped 50 ms. It also ran about 0.1 s ahead of the drawn body. On a 144 fps host it is
   the 60 Hz step, so the pose held on 58% of frames.
2. **The drawn root stepped and froze.** On guests the root came from a straight lerp between two snapshots on a clock
   that clamps at the newest frame, so the root jolted at every snapshot and froze on late frames. Peak acceleration was
   215 to 586 m/s² across the seven reels. On the host the body moves at the 60 Hz steps, so peaks were 1600 to 2200
   m/s². Speed was differentiated from that stepping root, and the facing snapped to the authority's yaw.
3. **Feet slid.** In one frame, a planted foot could slide 7 cm (shambler), 13 cm (runner), 20 cm (Hulk), 28 cm (brute) or
   35 cm (crawler). The causes were landings beyond the leg's reach, a trailing foot left behind, a second foot waiting a
   whole cycle at set-off, and hips that dropped on the landing frame.
4. **Pops.** Wrist and elbow flips came from blending arm poses row by row. Consecutive blows cut each other off, and a
   guest cut off a blow the moment a newer `LastAttack` arrived. Knees popped from the instant sink, and the hips jumped
   when the balance switched its worst point. The head flipped when its target passed behind it. The drop's landing threw
   the fists 1 m in one frame and restarted its take when it finished. Before any gait had posed, the leap played a 6 s
   idle compressed into half a second.
5. **Little secondary motion.** The capture played as recorded, with nothing loose layered on top.
6. **Strikes decelerated into contact.** The smoothstep swing slowed down into the impact instead of speeding up.

## What changed and why

### Smooth on every peer

- **Horde clock** (`Core/NativeHordeFlow.cs`, `HordeClock`). Every add, its entrance and its death are posed on one clock,
  never `arena.Time`. On the host it is the visual clock exactly. On a guest it follows the replica render time
  (`ReplicaRenderTime`, the same timeline as the drawn bodies) and rides through that clock's stalls within ±10% of real
  time. When a late frame leaves it running past the newest data, it slows smoothly (never below 30%) instead of running
  far ahead and then surging to catch up.
- **Presented root** (`HordeFlow`, built on the boss library's `BossRootFollow`). On a guest the root follows a C1 Hermite
  curve through the add's own buffered snapshots (read through `Runtime/NativeReplicaFrames.cs`, read-only). On the host it
  follows the 60 Hz body carried forward by its velocity to the visual clock, at most one step ahead. Either way the root
  runs through two goal-velocity springs, so velocity never steps.
- **Facing.** The facing turns at a limited rate and acceleration, faster during a wind-up so the blow faces where the
  authority aims. The body banks into turns and pitches when it speeds up or brakes.
- **Speed** comes from the smoothed root and is eased on a spring (`SpeedHalfLife`). A dead stop from a run blends the
  take from run to stand over about a quarter second instead of flinging the arms across in a few frames.

### Feet that stay put

- **Landing within reach.** A foot that lands beyond the leg's reach is planted where the leg actually reached, so the
  ankle never slides after it.
- **Heel roll.** A trailing foot that the hips have carried past its reach peels its heel and rolls over its ball. The
  toe stays put and the ankle rises. The roll lets go over the first third of the swing.
- **Stepping instead of sliding.** A walking foot still out of reach steps at once, even if that means a moment in the air.
- **Set-off.** When an add starts moving, each foot lifts on its own phase instead of the second foot waiting out a whole
  cycle.
- **Steps under the hips.** Steps are planned under the balanced hips for every upright kind; this used to be the Hulk only.
- **Hip sink.** The hips ease down into a long stride on a spring (`SinkDown`/`SinkUp`) instead of dropping on the landing
  frame. A foot that was planted before this frame is still kept in reach at once.
- **Existing behaviour kept.** Foot-twist lock and twist steps in place, the coherent knee (`LegKept`, now also steady
  when the leg folds shut) and continuous balance.

### Secondary motion (`Core/NativeHordeLife.cs`)

- A loose head that lags turns and speed changes, nods on footfalls, lolls, snaps on hits (harder on a headshot) and jolts
  on its own blow.
- A chest that lags and sways over the stance foot. The body dips on footfalls and the shoulders drop with each step.
- Arms that dangle and trail the body's acceleration like weights on cords, swing out of turns and bounce on footfalls.
- Seeded traits: head looseness, a hunch, a dead arm (32% of adds), a limp (38%, eased onto the bad leg), and tics
  (sharp jerks at hashed times that every peer shares).
- Hurt adds sag: more hunch, a drooping head, heavier arms and a worse limp.
- The hinged jaws (spitter, Hulk) flap with the steps.

The Hulk's keyed moves, the Ironworks kinds' fists and the heavy plod keep their own motion.

### Strikes

- **Anticipation and contact.** The arm cocks during the telegraph, then the swing accelerates into contact (`StrikeIn`,
  an ease-in). Contact lands exactly at `LastAttack + strike` on the horde clock: the checks hold it to within one frame.
- **After contact.** The arm follows through, then settles with a small overshoot. The weight ramp holds the blow over the
  gait.
- **Tails and late frames.** A new blow cross-fades from the tail of the last one. A guest that learns of a newer blow
  before its clock reaches it plays the current blow out first.
- **Cancel.** A stagger in the wind-up still cancels the blow. The take freezes and fades out over `CancelFade` without
  ever reaching contact.
- **Arm layers.** Arm layers are solved per take and then blended joint by joint, which removes the wrist and elbow
  flips.

### Switches, reactions, entrances and deaths

- **Pop killer** (`Core/NativeHordeBlend.cs`, inertialization). These switches are marked and blend from what was shown:
  - a vent climb, dive, door wake or jump-down handing over to the gait;
  - the drop's touchdown;
  - a kind swap;
  - the death's first frame.

  Unmarked trunk and arm joints that jump faster than 26 rad/s are caught the same way. Legs, the body offset and the
  root are never held back, so planted feet stay exact. The strike's contact frame is never softened.
- **The drop.** The fall's raised fists come down onto the floor over the first 45 ms, accelerating, and the hips sink out
  of the fall into the compression. The finished landing holds its take's last frame.
- **Deaths.** The fall is posed on the horde clock, as the living add was. The living pose hands over to it without a pop.
- **Reactions.** Directional flinch, stagger and headshot reactions stay `NativeHordeReact`'s. The life layer adds a head
  snap, a headshot whip and arms that fly out with the push.

## Animation impacts (hit timing, hitboxes, damage, cooldowns, movement, networking)

None on the authority. Hit timing, hit volumes, colliders, damage, cooldowns, movement and the network are unchanged. The
contact frame lands at the authority's `LastAttack + strike` on the presentation clock. The checks confirm that every
posed body still sits inside its hit volume.

On a guest the presentation clock is the replica render time, about 0.1 s behind the newest frame, which is the same
timeline the bodies are drawn on. The shipped pose ran on the newest frame, ahead of the body. As a result, a guest now
sees an add's blow land where the add is drawn, but up to the render delay after the authority's damage event arrives.
This is the standard trade for interpolated remote entities. If it reads late in play, the cushion lives in the shared
snapshot clock (Astra's side; see prompt #19).

## Tuning knobs

| Where | Knob | Value | What it does |
| --- | --- | --- | --- |
| `HordeClock` | `MaxSkew`, `SkewRate`, `CatchUp`, `SnapError` | .1, .8/s, 2.5, .3 s | how a guest's clock rides through stalls and snaps |
| `HordeClock` | `Lead`, `SoftZone`, `MinRate`, `RateSlew` | .04 s, .05 s, .3, 6/s | slowing near the newest frame |
| `HordeFlow` tunes (per kind) | `Turn`, `Accel`, `Bank`, `Hz`, `Damping`, `PitchScale`, `SpeedPitch`, `HalfLife` | e.g. runner 6.5 rad/s, 38 rad/s², .2, 1.9 Hz, .5, .7, .016, .1 s | turn, lean and root smoothing |
| `HordeFlow` | `WindTurn`, `WindAccel` | 1.8, 2.2 | faster turning in a wind-up |
| `HordePoseBlend` | `HalfLife`, `PopTurn`, `PopRate`, `PopMove` | .11 s, .42 rad, 26 rad/s, .12 | the pop killer's decay and detection (deaths use .15 s) |
| `NativeHordeGait` | `StrikeIn`, `CancelFade` | 2.2, .3 s | strike acceleration exponent and cancel fade |
| `NativeHordeGait` | `SpeedHalfLife`, `SinkDown`, `SinkUp` | .08, .045, .1 s | the eased speed and hip sink |
| `NativeHordeGait` | `LegReach`, `BallAhead`, `BallDrop`, `HeelMost` | .492, .09, .02 rig units, 1 rad | reach and the heel roll |
| `NativeHordeGait` | `FoldNear`, `SlamIn`, `LimpEase` | .2 rig units, .045 s, .06 s | folded-leg steadiness, the drop's slam, limp easing |
| `NativeHordeLife` | `LifeReset` traits | loll .45-1.25, hunch -.04-.12 rad, dead arm 32%, limp 38%, tics .12-.42 Hz | per-seed variety |

## Cost per tier

The checks show zero bytes allocated per frame across every layer. Gait pose cost per add per frame was measured with a
Release build on .NET 8 in this Linux container, 24 adds, on a shared and noisy machine. These figures are not from
Unity, Mono or IL2CPP:

| Tier | Shipped gait | This branch |
| --- | --- | --- |
| Full (nearest adds) | ≈ 7-10 µs | ≈ 13-17 µs (everything) |
| Reduced (to 40 m) | same | ≈ 11-16 µs (no shoulders, tics, jaw or body dip) |
| Impostor (beyond) | same | ≈ 10-12 µs (lean only; no foot twist or heel roll; the pop killer is off) |

`HordeFlow` and `HordePoseBlend` add a few microseconds per add. At 24 adds the Full tier costs about 0.3 to 0.4 ms per
frame here.

## Checks

- `Tests/ZombieFlowChecks.cs` runs with `--zombie-flow` and in the default run. Scenario: a 20 Hz guest with jitter and
  late frames on the shipped snapshot clock, rendered at 144 Hz ±15%.
  - **Root:** peak acceleration 98.6 m/s² against the shipped lerp's 483. The worst case is a runner's one-tick dead stop.
  - **Clock:** no stalled frames, and the pose clock never holds (`arena.Time` held 87% of frames).
  - **Feet:** under 1 cm on the drawn rig for six kinds at their own walk and run, through a corner, a stop and a turn in
    place. The worst was 0.5 mm.
  - **Contact:** within one frame of `LastAttack + strike` for 16 blows. Strikes ease in, and a stagger cancels the blow
    before contact.
  - **Determinism:** bit-identical poses across peers. 60 Hz against 144 Hz stays within 2.9 cm at the root and 5.7 cm at
    the head.
  - **Frame rate:** doubling it halves the largest per-frame change (0.53).
  - **Pop killer:** behaves as specified. Traits and the hurt sag apply. Zero allocation. Source checks keep the
    presentation off `arena.Time` and writing nothing back.
- Existing checks updated where the change is intended:
  - `ZombieMotionChecks`: a rolled foot's ball stands on the floor.
  - `HordeRoamChecks`: the head's facing is averaged over a second.
  - `HulkSpitterChecks`: the cancel line.
  - `StationEnemyChecks`: the fall on the horde clock.
  - `station-enemies-baseline.txt`: the seven `kindN.motion` lines were rewritten. Every other line is unchanged.

## Harness results (offline, not Unity)

Guest reels at 20 Hz, 3 to 17.5 s. Each reel runs the entrance, walk, run with a sharp turn, stop, attacks, a front hit, a
headshot, a turn in place, a cancelled wind-up and a death. The table gives shipped → this branch.

| Kind | Root peak accel (m/s²) | Pose clock held | Planted slip in the gait, worst per frame | Joint accel p99 (m/s²) |
| --- | --- | --- | --- | --- |
| shambler | 249 → 43 | 1846/2095 → 0 | 6.8 cm → 0.01 cm | 10785 → 1528 |
| runner | 431 → 55 | 1846 → 0 | 13 cm → 0.01 cm | 8791 → 888 |
| brute | 215 → 63 | 1846 → 0 | 27.6 cm → 0.9 cm | 10293 → 1720 |
| spitter | 265 → 21 | 1846 → 0 | 1.1 cm → 0.6 cm | 8823 → 1553 |
| crawler | 586 → 176 | 1846 → 0 | 35 cm → 7.5 cm | 13181 → 3225 |
| bloater | 242 → 23 | 1846 → 0 | 4.2 cm → 0.01 cm | 7284 → 1009 |
| Hulk | 124 → 46 | 1846 → 0 | 20 cm → 1.0 cm | 5102 → 2823 |
| Hulk up close (its smashes; guest) | 106 → 27 | 1404/1589 → 0 | 11 cm → 0 | 11458 → 1644 |
| host shambler (144 fps) | 1613 → 61 | 1225 → 0 | 1.3 cm → 0.01 cm | 6012 → 1490 |
| host runner (144 fps) | 2182 → 104 | 1225 → 0 | 5.0 cm → 0.8 cm | 5864 → 950 |

## Known limits

- **The crawler is still the weakest kind.** Its hips ride only about 5 cm above its hind ankles, so a hind foot passing
  under them folds the leg shut. Its legs still turn fast at its run, its trot still spikes, and its planted slip reaches
  7.5 cm. Raising its hips would fix this, but that pushes its trunk out of its crawl hit volume. That is a posture change
  to make together with the design and the hit volume.
- **Entrances and deaths blend on purpose.** Marked transitions move the legs while they decay, so feet slide a few
  centimetres there.
- **Some pops are only caught, not removed.** Occasional arm pops (left and right blows alternating) still come from the
  source. The pop killer hides them; they are not removed at their source.
- **Only two kinds flap a jaw.** Only the spitter's and the Hulk's jaws are separate parts. The other kinds' heads have no
  jaw to move; that is a design note.
- **Not checked in Unity.** Nothing here was run in the Unity Editor or on Windows. Everything was checked in managed code,
  against compile checks with the Unity assemblies, and in the offline WebGL harness.

## For Astra (network side; prompt #19)

Optional fixes on the network side would make guests smoother still. Nothing in this pass depends on them:

1. Give `NativeSnapshotClock` (`Core/NativePrediction.cs:80-86`) the behaviour of `BossRenderClock`. It clamps at the
   newest frame (`:85`) and only resets after a 0.5 s gap (`:84`).
2. Index each buffered frame by id once on arrival, and sample a Hermite curve instead of `InterpolateReplica`'s straight
   lerp and per-entity `FirstOrDefault` closures (`Runtime/NativeReplicaMotion.cs:98-118`).
3. Send snapshots on a fixed 20 Hz grid: `nextSnapshot+=.05f` with a catch-up guard, instead of
   `nextSnapshot=Time.unscaledTime+.05f` (`Runtime/NativeNetwork.cs:70`).
4. Confirm whether `RigidbodyInterpolation.Interpolate` (`Runtime/NativePveHorde.cs:54`) does anything under
   `SimulationMode.Script` (`Runtime/ConversionGame.cs:67`).
5. Optionally add per-minion `previousAttack` and `cancelledAt` wire fields, so guests play blows exactly.

The new read-only accessor `Runtime/NativeReplicaFrames.cs` reads `replicaHistory`. Keep it, or tell Claude, if that
buffer changes.
