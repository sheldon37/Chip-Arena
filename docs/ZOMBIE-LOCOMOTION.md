# Zombie locomotion (the horde's movement pass)

Owner request (2026-10-19): "I think I want to change the zombie animations for their movement, their arms also seem to
detach slightly. Can you take a look into the movement for them again? Their may be a better animation to use. Also, it
needs to be more fluid. Currently, it's still like rapid back and forth movement that doesn't look realistic."

This pass changes presentation only (Unity Chip Arena, `unity/ChipArenaConversion`). It reads the presented root's speed and
the add's kind, look and seed, and it writes the pose. It changes no rule, hit volume, collider, damage, timing, cooldown,
movement speed, AI, network message or authority state. It builds on the motion pass (`docs/ZOMBIE-MOTION.md`: the horde
clock, the presented root, planted feet, secondary motion, strikes, pop killer), which it leaves in place.
Status: on branch `claude/zombie-locomotion`, not merged, not released.

## What was wrong (measured)

The measurements come from an offline harness. It runs the same Core code and drives it with an emulated 60 Hz authority
(the runtime's chase rules and the crowd's separation push), a 20 Hz network and 144 fps frames. It also renders the built
voxel models with the ZombieLit vertex code. The harness is scratch, not in the repository.

### 1. "Rapid back and forth": the takes played at two to three times their capture's speed

The pack's walks and chases are slow shambles: 0.12 to 0.54 m/s on the rig. The authority walks the kinds at 1.1 to 1.6 m/s and
runs them at 1.8 to 4.6 m/s. The shipped mapping (`NativeZombieMotion.Rate`) scaled each take's own pace by the speed's 0.7
power and capped it at 2.8. Every take therefore played much faster than it was captured. That included its trunk, arms and
head, not just the stepping:

| kind at its walk | shipped playback rate | shipped steps per second | stride per step |
| --- | --- | --- | --- |
| shambler, 1.5 m/s | 2.0 to 2.8 times | 4.1 | 0.37 m |
| brute, 1.3 m/s | 2.1 to 2.7 times | 3.4 to 4.1 | 0.32 m |
| spitter, 1.6 m/s | 2.6 to 2.8 times | 4.4 | 0.39 m |
| bloater, 1.1 m/s | 1.8 to 2.3 times | 2.9 | 0.38 m |
| rigger, conductor (zone) | 2.6 to 2.8 times | 4.2 to 4.3 | 0.30 to 0.36 m |

The runs fared no better: the shambler stepped 5.0 times a second, the runner 6.2 and the cinderling 6.9. On the drawn rig
this showed up as follows (shipped figures, harness, steady walk):

- The trunk and arm joints turned at 4.2 to 6.7 rad/s (p95 averaged over the ten upper joints). The worst single joint
  reached 9.6 to 16.6 rad/s.
- The hands' p95 jerk was 24,000 to 58,000 m/s³.
- About half of the hands' motion was above 4 Hz (54 % for the shambler). The hands reversed direction 11 times a second.

That is the rapid back-and-forth.

A second, smaller cause appeared in crowds. The separation push jostles the root's speed (±0.23 m/s on average, up to
±0.63). The walk-to-run blend and the cadence read that speed through a 0.08 s spring, so a jostled add flickered between
its walk and run takes.

Two things measured as fine and were left alone: the drawn root (the motion pass already smoothed it) and the facing. The
presented yaw rate stays 1.5 rad/s p95 in the crowd, the same before and after, because `HordeFlow` already limits the turn
rate and acceleration.

### 2. "Arms detach": the shoulder blocks hung off the chest

The rig's clavicles sit at ±0.275 m (the pilot's chest is 0.53 m wide). The zombie bodies are narrower:

- the Siren kinds' voxel chests are 0.26 × W wide either side (W is 0.76 to 1.0 for the narrow kinds);
- the zone kinds use classic bodies.

Each shoulder block starts at its clavicle and runs outward, so the shoulder block and the arm hung beside the torso with a
gap. The gap was measured on the built meshes, with the per-add proportions applied (a "slit" ray from the shoulder block's
inner face and the closest approach of the two parts):

| kind | shipped gap at rest |
| --- | --- |
| shambler | 5.3 to 7.3 cm |
| runner | 4.2 to 6.2 cm |
| spitter | 4.6 to 10 cm |
| crawler | 7.9 to 9.7 cm |
| bloater | up to 4 cm |
| cinderling | 6 to 9 cm |
| rigger | 3.7 cm |
| conductor | 4 cm |
| undertow | 9 cm |

The brute, the Hulk, the stoker, the slagback, the ladleman, the linesman and the pileup already meet their shoulders. In
motion, the capture's clavicles and the secondary motion's shoulder drops widened the gaps further (the shambler's run: 5.5
cm). The rest-pose sheet in the renders shows the arms hanging free of the torso.

## What changed and why

The new file is `Core/NativeHordeLoco.cs` (a partial of `NativeHordeGait`). The other changes are small hooks in
`Core/NativeHordeMotion.cs`, `Core/NativeZombieMotion.cs`, `Core/NativeZombieClips.cs` and `Core/NativeHordeRoamMotion.cs`.

- **The cadence is the body's, not the take's** (`StepRate`).
  - Steps per second are 2.56 × v^0.35 (v in rig m/s), scaled by the kind's tempo. A step is never longer than the legs
    carry: 0.5 m at a walk, up to 0.9 m by 2.6 m/s, where every body is running.
  - `NativeZombieMotion.Rate` now time-warps each take to that cadence. Every take of a kind steps at the same rate at a
    given speed, the stride takes most of a speed change, and the walk-to-run blend never changes the cadence.
  - The cadence follows Froude-style scaling of a human gait to the rig's 0.5 m legs. At 1.5 m/s that is about 3 steps a
    second, with steps of half a metre: long, heavy reaching steps instead of a 4-per-second shuffle.
- **Calm and blur** (`SampleCalm`, `Calm`, `CalmGain`). A take played faster than its capture swings its trunk and arms
  less about its cycle's mean pose.
  - The gain is rate^-0.6, never under 0.45. Each take's cycle mean is computed once, in `NativeZombieMotion.Warm`.
  - On the Full tier, a three-tap blur over the cycle (up to ±0.06 cycles at twice the pace) removes the capture's quick
    wobbles. The stride's own swing keeps 96 %.
  - The hips' bob keeps its full depth (the weight landing on each step). The legs are the planted gait's, as before.
  - Each kind also has a steadiness (`Steady`) at its own pace: the shambler 0.82, the runner 0.9, the brute and the bloater
    0.72, the spitter 0.85, the rest 1. The heavy kinds do not throw their shoulders about as the pack's frenzied sprints do.
- **Gait speed and blend** (`GaitSpeedStep`).
  - The cadence and the take blend read a gait speed eased off the presented root's speed (half-life 0.18 s). The cadence
    never falls below 0.8 of the real speed, so a body that sets off takes quick steps.
  - The walk-to-run blend moves at most 2.2 per second.
  - Setting off and standing use two thresholds (0.24 and 0.14 rig m/s).
  - The feet still land on the root's real speed and velocity, so nothing slides.
- **Stance within reach** (`Reachable`, `StanceMost` 0.56 m). The hips never travel farther over a planted foot than the legs
  reach. At speed the stance shortens into a run's flight instead of tearing the trailing foot off early, out of step.
- **Shoulders on the chest** (`ShoulderInset`, `Shoulders`).
  - Each kind and look has its clavicles set in, so the shoulder block's inner face sits about half a centimetre into the
    chest of the narrowest add the per-add proportions make: shambler 7.8 cm, runner 6.7, spitter 7.2 and 10.5 (looks 0
    and 1), crawler 10, bloater 1.2, cinderling 8.4 and 6.4, rigger 3.2, conductor 4.5, undertow 9.6. The other kinds are 0.
  - The clavicles are set in before the arms are solved (`NativeZombieClips.Pose(..., shoulders)`), so the hands still reach
    the capture's chest-space targets. Only the shoulders move.
  - This covers the gait, the captured and procedural deaths, the vent climb (its claw targets move in with the shoulders),
    the dive and the crawler's trot. The Ironworks grips solve to their tools as before (`--ironworks-props` passes).
  - The deaths, the vent climb and the dive are called without the look, so they take the kind's deepest inset. Only the
    spitter's and the cinderling's looks differ: their shoulders sit up to 3.3 cm deeper in the chest there, with no gap.
- **Takes per kind** (`NativeZombieMotion.Warm`). The exporter adds four cycles: `chase-1-full`, `chase-5-full`, `walk-6-full`
  (cleaner cycles of the pack's full-length loops) and `sprint-2`. 50 tracks now; the pack itself stays out of git.

| kind | walk takes (was) | run takes (was) | why |
| --- | --- | --- | --- |
| shambler | chase-1, chase-2, chase-1-full (chase-1, chase-2, chase-5) | sprint-3, chase-2 (sprint-1, sprint-3) | the reaching chases are the smoothest at its cadence (upper joints 1.3 rad/s p95); chase-5 bounced its hands and head; sprint-1 was the jerkiest sprint (head jerk 24,000 m/s³); some shamblers now lurch-run on the chase |
| runner | sprint-3, sprint-2 (sprint-1, sprint-3) | sprint-3, sprint-2 (sprint-3, sprint-4) | sprints near their own pace; sprint-4 throws its shoulders furthest (chest yaw 1.3 rad peak to peak); the low-armed sprint-2 gives variety |
| brute | walk-6-full, walk-4 (walk-4, walk-6) | chase-2, chase-5-full (chase-5, chase-2) | the full loop of the heavy walk is the smoothest (hands 3 % above 4 Hz) |
| spitter | chase-1, walk-6-full (chase-1, walk-6) | chase-2, sprint-3 (chase-2, sprint-1) | sprint-1 jerked its head at 37,000 m/s³ |
| bloater | walk-6-full, walk-4 (walk-6, walk-4) | chase-1, chase-2 (chase-1) | the smoothest waddle; a second run take for variety |
| Hulk | unchanged | unchanged | its own heavy stride (`NativeHordeHeavy`) already plods at 0.9 to 1.2 steps a second |
| crawler | procedural | procedural | unchanged gait; only its shoulders |

The zone kinds (`NativeIronworksKinds`, `NativeStationKinds`) keep the takes they name. They get the body's cadence, the calm,
the stance and the shoulders.

## Results (harness, shipped → this branch)

Figures are for the steady walk and run on the host at 144 fps, the 20 Hz guest reels (entrance, walk, run with a sharp
turn, stop, attacks, hits, a cancelled blow, death) and a 32-add crowd with separation. Joints are the p95 averaged over the
ten upper joints.

| scene | steps/s | upper joints p95 (rad/s) | chest yaw rate p95 (rad/s) | hands' jerk p95 (m/s³) | shoulder gap |
| --- | --- | --- | --- | --- | --- |
| shambler walk 1.5 m/s | 4.24 → 3.05 | 4.2 → 1.3 | 2.0 → 0.6 | 32,500 → 8,700 | 3.5 → 0.1 cm |
| shambler run 3.4 m/s | 4.97 → 4.07 | 6.4 → 2.3 | 8.2 → 0.9 | 15,600 → 16,300 | 3.1 → 0.1 cm |
| runner walk 2.6 m/s | 4.14 → 3.72 | 5.2 → 4.5 | 6.8 → 4.4 | 12,100 → 16,600 | 3.4 → 0.1 cm |
| runner run 4.6 m/s | 6.17 → 5.31 | 7.8 → 5.1 | 10.4 → 6.7 | 25,600 → 15,300 | 3.4 → 0.1 cm |
| brute walk 1.3 m/s | 3.42 → 2.51 | 5.3 → 1.9 | 2.9 → 1.7 | 36,600 → 6,100 | 0.1 → 0.1 cm |
| spitter walk 1.6 m/s | 4.38 → 2.99 | 6.7 → 2.0 | 3.7 → 0.9 | 57,800 → 8,600 | 5.7 → 0.1 cm |
| bloater walk 1.1 m/s | 2.94 → 2.29 | 4.6 → 1.8 | 2.5 → 0.8 | 24,300 → 5,100 | 0.1 → 0.1 cm |
| conductor walk (zone) | 4.21 → 2.64 | 6.5 → 2.1 | 5.5 → 2.0 | 29,800 → 10,500 | 3.9 → 0.1 cm |
| undertow walk (zone) | 3.95 → 3.04 | 5.2 → 2.2 | 2.1 → 0.9 | 30,100 → 8,600 | 8.8 → 0.1 cm |
| reel, shambler (guest) | 4.60 → 3.37 | 7.9 → 3.1 | 7.8 → 3.5 | 39,400 → 9,400 | 5.6 → 0.1 cm |
| reel, brute (guest) | 3.83 → 2.50 | 7.3 → 3.9 | 5.4 → 2.3 | 47,700 → 19,700 | 0.2 → 0.2 cm |
| crowd, 32 adds (guest) | 4.08 → 3.20 | 7.3 → 3.9 | 6.5 → 3.5 | 34,000 → 16,100 | - |

Other results:

- **Hands' high-frequency share.** The shambler's walk went from 54 % above 4 Hz to 21 %, the spitter's from 34 % to 8 %.
- **Crowd jostle.** The take blend now moves 21 % as far as the raw jostled speed would move it, and the cadence's jitter
  halves (phase rate p95 change 4.6 → 2.1 cycles/s² on the guest).
- **Planted feet.** They stay within 1.5 cm on the drawn rig in steady gaits, turns, setting off and stopping, for every
  captured kind (the checks).
- **Hulk.** Unchanged.
- **Runner.** It keeps a sprinter's trunk by design, so its numbers fall least.

The shoulder gap above is the closest approach of shoulder block to chest at rest or in motion. For the cinderling, the
closest approach is 0.6 to 0.8 cm in motion (7.1 before). Its classic chest is hollow at the middle, so the slit ray reads
nothing useful there.

## Cost

The gait costs about 10.9 µs per add per frame on the Full tier, against 9.1 µs shipped: the two blur samples a take. The
measurement is a managed Release build on .NET 8 in this Linux container, 24 adds, on a heavily loaded machine. It is not
Unity, Mono or IL2CPP.

The Reduced and Impostor tiers skip the blur and add about a microsecond for the calm. Nothing allocates per frame
(`Tests/ZombieLocoChecks`).

## Animation impacts

There are none on hit timing, hitboxes, damage, cooldowns, movement or networking:

- The strike timing and contact frame are the motion pass's, unchanged (the zombie motion and flow checks pass).
- The clavicle inset only moves the drawn shoulders inward. The hands reach the same chest-space targets, and every body
  stays inside its hit volume (`HordeKindChecks` passes).
- The cadence is presentation only: the authority's speeds, AI and separation are untouched.

## Tuning knobs (`Core/NativeHordeLoco.cs`)

| knob | value | what it does |
| --- | --- | --- |
| `CadenceAt1`, `CadenceExp` | 2.56, 0.35 | steps per second at 1 rig m/s and the speed's power |
| `StepMost`, `RunStepMost` | 0.5, 0.9 m | the longest step walking and by 2.6 m/s |
| `CalmExp`, `CalmFloor` | 0.6, 0.45 | the swing's gain at playback rate r: r^-0.6, never under 0.45 |
| `BlurMost` | 0.06 cycles | the Full tier's phase blur at twice the pace or more |
| `Steady` | 0.72 to 1 | each kind's steadiness at its own pace |
| `GaitHalfLife`, `BlendSlew` | 0.18 s, 2.2/s | the gait speed's easing and the blend's most change a second |
| `MoveOn`, `MoveOff` | 0.24, 0.14 m/s | the speeds an add sets off and stands at |
| `StanceMost` | 0.56 m | the most the hips travel over a planted foot |
| `ShoulderInset` | per kind and look | how far in the clavicles sit |

## Checks

`Tests/ZombieLocoChecks.cs` runs with `--zombie-loco` (with the zombie motion and flow checks) and in the full run. It
checks:

- **Cadence.** Every take of a kind steps at the body's cadence. The walkers stay under 3.3 steps a second at their walk,
  and no take plays more than 2.2 times its pace there.
- **Fluidity.** Through the real gait at each kind's walk and run:
  - upper joints under 3 rad/s p95 (6 on sprint takes);
  - chest yaw rate under 2.5 rad/s (7.5 sprinting);
  - the hands' energy above 2.5 times the stride's frequency under 35 %.
- **Blends.** Through stand, walk, run and back, and a crowd's jostle on the walk-run boundary:
  - the phase only moves forward and its rate never steps (at most 0.028 cycles/s a frame);
  - no upper joint pops;
  - the blend moves at most `BlendSlew` a second, and the jostle moves it under 35 % as far as the raw speed would (21 % measured).
- **Feet.** The drawn ankle, or a rolled foot's ball, stays on its spot: within 1.5 cm walking, running and turning, 2 cm
  setting off and stopping inside a quarter second.
- **Shoulders.** On every kind's and look's built mesh, at rest and over 12 adds' proportions:
  - the shoulder block meets the chest and the upper arm meets its shoulder block;
  - no slit over 1.2 cm where the clavicles are set in;
  - each inset closes a real gap (the shipped clavicles leave up to 10 cm on 17 looks);
  - walking and running, the shoulder block stays within 0.6 cm of the chest.
- **Allocation.** None.
- **Source.** The authority and network sources never read the pass, and it never reaches them. The exporter writes the
  four new keys.

Existing checks changed on purpose:

- `HordeRoamChecks` (the wander): a planted foot that rolls onto its ball is now followed by its ball. The wander's longer
  steps at the body's cadence roll the trailing foot, as in `ZombieMotionChecks`.
- `station-enemies-baseline.txt`: the `takes`, `give`, `motion` and `death` lines of the Siren kinds were rewritten
  (`StationBaseline.Measure` on this branch). Every other line is unchanged.

## Known limits

- The runner and the sprinting shamblers keep the pack's sprint trunk swing: 4.4 to 6.7 rad/s chest yaw p95. It is the
  capture's own, at its own pace, steadied 10 to 18 %.
- In a dense crowd, a separation burst that throws an add from a stand to 5 m/s in a tenth of a second can drag a planted
  foot 4 to 5 cm in one frame before it lifts. The shipped gait showed the same, up to 5.6 cm.
- The ladleman's slow walk holds one foot 0.8 cm off for a frame on a curve.
- Fluidity was judged from the harness's metrics and renders, not Unity. The Unity Editor, Windows and two-client play were
  not run.
- The Rigger's chain hang poses (`NativeIronworksMotion.RiggerHang`) keep the rig's clavicles; the gap shows only in
  those poses.

## Twitching under fire (2026-10-20)

Owner report (2026-10-20): "their movement still seems too fast (Not talking about their movement speed, rather their
animations, they look like they're 'twitching' towards the player)". The recording shows Central Station, a pilot firing a
pistol at shamblers that run at them. Branch `claude/zombie-twitch-fix`; presentation only.

### What was measured

An offline lab (scratch, not in the repository) emulates the authority at 60 Hz from `StepPveHordeCore`, `HitPveAdd` and
`StructureNavMoveCore` on the real `expedition-transit` nav graph. It presents the add through the real Core code as
`NativeHordeKindPresentation` does: Unity's interpolated transform and the visual clock on the host, 20 Hz snapshots on a
guest. Each add enters 10 m from the pilot and is shot with the pistol every .42 s. Every pistol hit staggers a shambler or a
runner: 23 damage is a quarter of its health. The table gives head and chest world turn rates while it chases, in rad/s, ranked
by what removing each cause did at the base (shambler, host, 144 fps):

| cause | what it did | measured effect |
| --- | --- | --- |
| 1. The stagger's reel started over on every hit | The knockback take snapped back to its start, came in again within 54 ms and ran at twice its capture's pace | head p95 23.2 → 14.9 and p50 8.2 → 4.2, chest 10.2 → 5.2 without it |
| 2. The stand-to-move take blend at a restart | The stagger ends with an instant 3.4 m/s restart, and the take crossed from standing to the chase in two or three frames | head max 55 → 22 once rate-limited |
| 3. The hurt sag stepped with health | A hit takes a quarter of the health in one frame: the head's droop, the hunch and the loose head's gain stepped with it | head max 22 → 12 once eased |
| 4. The grid route's zig-zag | The station's nav is a 1 m grid with four links a node, and the empty hazard list keeps the authority off the straight line, so its facing snaps up to 59 degrees two to four times a second | facing p95 3.0 rad/s against 0 on a straight chase; head p95 6.0 against 3.9 |
| 5. Smaller onsets | Hit envelopes rose at full speed, the head snap landed in one frame, the root's acceleration fed the head's lag at up to 9 m/s², and the twitch and tics ramped in 40 to 60 ms | a few rad/s of peak each |

Not offenders:

- **The cadence.** 3.8 steps/s for the shambler's run and 4.5 to 4.7 for the runner. That fits Froude scaling for the rig's 0.5 m
  legs, and it is unchanged.
- **The host's interpolated transform.** It is fed in as if it were the body (a 60 Hz sawtooth of one tick). The drawn root
  differed by 2 mm: 11.6 against 11.4 cm p95 from the body.
- **More root smoothing.** It was tried and rejected. A 0.2 s half-life cuts the root's acceleration by a third but takes the
  drawn body 18 cm from the hit volume.
- **Reading a backward slide as stagger steps.** It was tried and rejected: head p95 got worse, 10.9 → 12.3.

The root itself stays the authority's (see the note for Astra below).

### What changed

- **One continuous stumble** (`NativeHordeReact.Stumble`).
  - Each authority stagger is a link. It eases in over .16 s, scaled by the hit's weight: a pistol's flinch weight plays .5 of
    the reel, a step weight .75, a heavy blow 1. It lets go over .38 s from .12 s before the authority does.
  - Overlapping links are one stumble with the largest link's weight. The knockback take plays at its own pace from the
    stumble's start and holds at .42 s.
  - It is a pure function of the replicated hits and the clock. The gait's reel take, the trunk's arch and the arms' fling read it.
- **The stand-to-move blend** changes at most `MoveSlew` (3) a second for the upright kinds.
- **The health the sag reads** is eased (`HurtHalfLife` .2 s).
- **The body's acceleration** that the head, chest and hands lag behind is eased over .06 s and held within 5 m/s²
  (`LifeAccelEase`, `LifeAccelMost`).
- **A hit's snap** feeds the same impulse in over .035 s (`HitSpread`).
- **The hit envelope** rises from rest (`u^1.5 e^(1.5(1-u))`) to the same peak and then falls as before.
- **The head's twitch** eases in over .14 s and its tics over .1 s.
- **The facing's goal** is the authority's facing eased over .26 s while the add travels (`HordeFlow.HeadingTau`, through
  `BossRootFollow.HeadingTau`, which is off for every boss). It is off through a wind-up, so a blow still faces the aim.

### Results (lab, shambler at the base → now; host 144 fps unless stated)

| scene | head p50 / p95 / max | chest p95 | upper joints p95 | facing p95 | one-frame joint turn near a hit |
| --- | --- | --- | --- | --- | --- |
| under pistol fire | 8.2 / 23.2 / 64.8 → 3.0 / 6.8 / 10.3 | 10.2 → 3.5 | 12.0 → 3.2 | 0.64 → 0.34 | 0.43 → 0.12 rad |
| runner under fire | 8.4 / 24.5 / 59.8 → 3.3 / 7.4 / 13.8 | 11.2 → 3.6 | 13.2 → 3.8 | 0.94 → 0.41 | 0.43 → 0.35 (a wrist, see the limits) |
| 20 Hz guest under fire | 7.7 / 23.1 / 43.0 → 2.7 / 6.8 / 12.2 | 10.1 → 4.0 | 11.7 → 3.2 | 0.70 → 0.40 | 0.43 → 0.11 |
| 30 fps under fire | 6.9 / 19.2 / 33.6 → 2.6 / 6.2 / 10.8 | 8.3 → 3.1 | 8.0 → 3.1 | 0.83 → 0.38 | 0.20 → 0.11 |
| chasing, no fire | 2.3 / 6.0 / 11.0 → 2.2 / 4.7 / 8.0 | 2.7 → 2.3 | 2.3 → 2.3 | 2.99 → 1.64 | - |
| runner chasing, no fire | 4.2 / 10.0 / 15.2 → 3.8 / 7.7 / 12.2 | 5.9 → 5.2 | 4.7 → 4.6 | 3.89 → 1.99 | - |

Unchanged:

- The drawn root's acceleration under fire: 84 m/s² p95, 169 max for the shambler. It is the authority's knock-back and restart.
- Its distance from the body: 11.6 cm p95.
- The step rate under fire: 3.1 steps/s.
- The cadence chasing.

`Tests/ZombieTwitchChecks` (`--zombie-twitch`) holds these through its own scene, with the base's numbers from the same scene.

### Animation impacts

None on hit timing, hitboxes, damage, cooldowns, movement speed or networking:

- The contact frame still lands within one frame of `LastAttack + strike`, including a blow that starts as a stumble lets go.
- The posed bodies stay inside their hit volumes (`HordeKindChecks`).
- No authority, rules or wire file changed.
- The drawn facing now trails the authority's by up to about 6 degrees on a grid route. The effects keep the authority's facing.
- Host, guests and the killcam derive everything from the replicated hits, the clock and the presented root, as before.

### For Astra (rules and AI, not changed here)

- **The steering.** The Unity maps' `hazards` is an empty array, so `StructureNavMoveCore` never takes its straight-line
  shortcut (`Map.hazards==null`). The station's nav is a 4-connected 1 m grid: 28,634 of its 28,642 links are 1 m. A slanted
  approach therefore snaps the heading up to about 59 degrees two to four times a second. The presentation now eases the
  facing, but the root's sideways wobble remains: 31 m/s² p95 against 4.9 straight. Possible fixes:
  - treat an empty hazard list as none;
  - string-pull the route;
  - add diagonal links.
- **The pistol's stagger.** Each hit stops a charging shambler dead for .45 s and knocks it back at 2.2 m/s: a 5.6 m/s velocity
  reversal in one tick. The stagger then ends with an instant 3.4 m/s restart. To stay on the body, the drawn root has to turn
  round at 84 m/s² p95. Possible fixes: ramp the restart over .2 to .3 s, spread the knock-back over a few ticks, or give light
  weapons a lighter stagger.

### Known limits

- The root's jolt under fire and its sideways wobble on a grid route are the authority's (above).
- In 60 s under fire, a runner's left wrist turned .38 rad in one frame once (its sprint take). That is under the pop killer's
  .42 rad limit.
- The owner's recording repeats 18 of its 131 frames, so the game or the recorder ran under 30 fps. Frame pacing is not
  addressed here.
- The Unity Editor, Windows and two-client play were not run.
