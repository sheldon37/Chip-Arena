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
