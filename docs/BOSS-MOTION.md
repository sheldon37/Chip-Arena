# Boss motion foundation (Central Station and City Ironworks bosses)

Owner request (2026-10-17): "begin the bosses for Central Station and City Ironworks; I want these animations to be a lot
more fluid than what we have now; I will have to fix the zombie animations and things later."

This is the shared motion library the two bosses are built on (Unity Chip Arena, `unity/ChipArenaConversion`). It is
presentation only: it reads replicated state and the presentation clock, writes a pose, and changes no rule, hitbox,
damage, timing, cooldown, movement, network or authority state. No existing enemy uses it (the zombies, station and
Ironworks kinds and the Siren are untouched). Status: on branch `claude/boss-motion`; not merged, not live.

Files (all `Assets/ChipArena/Core/`, pure C# with no UnityEngine, unit-tested without Unity):

| File | What it holds |
| --- | --- |
| `NativeBossMotion.cs` | `BossMath` (axes, quaternion helpers, noise), `BossPose` (the output), `BossInertializer` (the pop killer) |
| `NativeBossMotionSpring.cs` | `BossSpring`, `BossSpring3`, `BossSpringQ` (critically damped, half-life), `BossTurn` (limited yaw), `BossSecondOrder`, `BossSecondOrder3` |
| `NativeBossMotionFollow.cs` | `BossSnapshotBuffer`, `BossRenderClock`, `BossRootFollow` (the replicated root) |
| `NativeBossMotionGait.cs` | `IBossGround`, `BossLegDef`, `BossGaitDef`, `BossGait` (N legs), `BossIk` (two-bone, soft) |
| `NativeBossMotionChain.cs` | `BossChainDef`, `BossChain` (verlet spines, tails, cables, chains) |
| `NativeBossMotionAction.cs` | `BossEase`, `BossPhase`, `BossActionDef`, `BossTimeline`, `BossActionPlayer` |
| `NativeBossMotionLayers.cs` | `BossBreath`, `BossLookAt`, `BossFlinch`, `BossStagger` |
| `NativeBossMotionRigs.cs` | `BossBipedRig`, `BossCrawlerRig`: the two reference rigs (the example wiring the checks and renders run) |

Plus `Runtime/NativeBossPoseApply.cs` (puts a `BossPose` on a model's transforms) and `Tests/BossMotionChecks.cs`.

## Audit: why current enemy and boss motion reads stiff

Evidence from the code at shared `claude/sleepy-ritchie-g8ruhv` `598c41b` (this branch's base), ordered by how much it costs. These are notes for the
owner's later zombie fix; none of them is changed here. Line numbers are at that commit.

1. **The enemies' pose clock steps.** Every horde add (zombies, station and Ironworks kinds, the Hulk) is posed at
   `now=arena.Time` (`Runtime/NativeHordeKindPresentation.cs:81`, `:166`, `:505`). On a guest, `arena.Time` only moves
   when a 20 Hz host frame lands (`Runtime/ArenaPresentation.cs:18-19` says so), so the gait cycle, swing feet, attack
   curves and hit reactions advance in 50 ms jumps while the body's position (lerped per frame) slides smoothly under
   them. It is also about 100 ms ahead of that position (the replica's positions are drawn at `ReplicaRenderTime`, the pose
   at the newest frame). On the host, `arena.Time` is the 60 Hz fixed step (`Runtime/ConversionGame.cs:275`) while the game
   targets 144 fps (`Runtime/ConversionGame.cs:66`): two or three rendered frames show the same pose. A continuous clock
   already exists for other presentation (`NativeVisualClock`, `Core/NativePoseHistory.cs:27-40`; `ReplicaRenderTime`).
   This is the largest single source of stiffness on guests.
2. **Host bodies move at 60 Hz too.** The minion body's `Rigidbody.interpolation=Interpolate`
   (`Runtime/NativePveHorde.cs:54`) does not interpolate under `Physics.simulationMode=SimulationMode.Script`
   (`Runtime/ConversionGame.cs:67`; Unity interpolates only bodies it steps itself; to confirm in the Editor), and the rig
   root is set from `m.Body.transform.position` (`Runtime/NativeSourcePresentation.cs:73`,
   `Runtime/NativeHordeKindPresentation.cs:245`), so at 144 fps the body holds still for frames and then moves.
3. **The replicated follow is a straight lerp between two snapshots that freezes at the newest.**
   `Runtime/NativeReplicaMotion.cs:98-118`: the position is `Vector3.Lerp` across the bracketing pair and the yaw
   `Mathf.LerpAngle` (`:104`, `:112`, `:117`), so the velocity jumps at every snapshot (C0 only; the yaw rate too); a gap of
   5 m or more snaps. `NativeSnapshotClock.Advance` (`Core/NativePrediction.cs:85`) clamps the render time at the newest
   snapshot, so a late frame freezes the body and the next one jumps it. The clock's cushion is set once, by the first
   frame's latency (`:83`), and only ever shrinks (every freeze eats into it; nothing restores it short of a 0.5 s stall,
   `:84`), so after a few late frames every jitter becomes a freeze. Measured in the managed checks with 20 Hz snapshots,
   45 ms latency, 14 ms jitter and 5% of frames 120 ms late: 12 frozen frames while moving and acceleration spikes of
   358 m/s^2 (the new follow: none frozen, peak 13.6). The per-minion `FirstOrDefault` lookups (`:116`) also allocate
   closures every frame.
4. **The snapshot cadence drifts and jitters.** `nextSnapshot=Time.unscaledTime+.05f` (`Runtime/NativeNetwork.cs:70`)
   restarts from the frame that sent, so the real interval is 50 ms plus up to one host frame (about 15-20 Hz, varying).
5. **Ground speed is differentiated from the presented root.** The horde gait takes its speed from how the drawn root moved
   (`Core/NativeHordeMotion.cs:355-357`) through a first-order 0.1 s filter. It inherits 1-3: zero motion between stepped
   clock ticks, a jump when one lands, a freeze when a frame is late; the stride and cadence react late.
6. **Facing snaps, then turns at a constant rate.** The authority sets the yaw straight at the target every tick
   (`Runtime/NativePveHorde.cs:154`); the gait turns toward it at a clamped constant rate (`Core/NativeHordeMotion.cs:351`:
   9 rad/s, 16 winding up), so turns start and stop with infinite angular acceleration and nothing leans into them. The
   pilot rig, which the Siren uses, takes the raw yaw (`Runtime/CapturedAnimation.cs:427`).
7. **Hard switches.** The Siren body plays one baked clip at a time and switches the frame a move starts or ends
   (`Runtime/NativeSourceMotion.cs:48-51`); the pose-history smoothing runs before that (`Runtime/CapturedAnimation.cs:477`
   then `:518`), so it never softens the switch. Her guard sets the arm rotations outright (`Core/NativeSirenDrift.cs:56-61`).
   On the pilot rig an attack blends in over 0.04 s (`Runtime/CapturedAnimation.cs:457`), a stagger snaps in at a fixed 0.7
   weight (`:473`) and the locomotion layer drops the frame an attack starts (`:431`). Bank-path minions pick idle, walk or
   run by speed thresholds (`Runtime/NativeSourcePresentation.cs:80`).
8. **Every curve is a smoothstep.** Strikes ease in and out (`Core/NativeHordeMotion.cs:454`,
   `Core/NativeStationMotion.cs:101`), so the hand slows into the contact frame and the impact reads soft. Nothing
   overshoots and settles; the attack layer's weight is 1 on its first frame (`Core/NativeHordeMotion.cs:458`).
9. **Little secondary motion.** The Conductor's lantern is a damped pendulum (`Core/NativeStationMotion.cs:172`), the one
   exception; the Ironworks chains are static sags (`Core/NativeIronworksMotion.cs:10`); arms, tails and hanging parts carry
   no follow-through.

What already works and should be kept: the horde gait plants feet world-locked and steps them on the take's cadence
(`Core/NativeHordeMotion.cs:384-416`), clips sample on Catmull-Rom (`Core/CapturedClip.cs:43-49`) and attacks retime on a
monotone C1 Hermite (`Core/CapturedClip.cs:77-88`).

Suggested order for the zombie fix (notes only): pose the horde on the continuous clock (`ReplicaRenderTime` on a guest,
`NativeVisualClock` on the host) and interpolate the host's root between fixed steps (1, 2); take the ground speed from
the follow, not by differentiating the drawn root (5); then the yaw (`BossTurn` and the lean, 6) and the strike curves
(`BossEase.In` into contact, `Settle` out of it, 8). The replica follow and snapshot clock (3, 4) are networking code:
Astra's to change; `BossRootFollow` shows one way.

## API

Axes everywhere: Unity's (+X right, +Y up, +Z forward; a yaw turns +Z toward +X, as `Quaternion.Euler(0, yaw, 0)`),
System.Numerics types, radians unless a name says degrees. Every type uses fixed fields and arrays: no allocation per
frame after construction.

### Springs and dynamics (`NativeBossMotionSpring.cs`)

- `BossSpring` / `BossSpring3` / `BossSpringQ`: critically damped springs solved exactly. `Step(goal, halfLife, dt)`: from
  rest, halfway to a still goal after `halfLife` seconds, never past it, and the same result at any frame rate.
  `Step(goal, goalVelocity, halfLife, dt)` follows a moving goal with no steady lag; pass the goal as of the step's start
  (`target - velocity*dt`). The quaternion spring's velocity is an angular velocity (world, rad/s).
- `BossTurn.Step(goalYaw, halfLife, maxRate, maxAccel, dt)`: the shortest arc, never faster than `maxRate`, accelerating
  and braking within `maxAccel`, stopping on the goal without overshoot; a goal flickering across the half turn keeps the
  current direction. `Yaw` is unwrapped (counts turns), `Rate` its speed.
- `BossSecondOrder` / `BossSecondOrder3`: `Configure(f, zeta, r)` (Hz, damping, response), then `Step(x, dt)` or
  `Step(x, xd, dt)`; `Kick(v)` adds an impulse. zeta below 1 overshoots and wobbles, r below 0 anticipates. Sub-stepped
  at 1/240 s, stable at any frame rate.

### Replicated root (`NativeBossMotionFollow.cs`)

- `BossRootFollow`: `Observe(authorityTime, localTime, position, yaw)` per snapshot, then each frame either
  `Step(localTime, dt)` (its own render clock) or `StepAt(renderTime, dt)` (an external one: on a guest pass
  `arena.ReplicaRenderTime` so the boss stays in step with every other effect). Or `Follow(target, velocity, yaw, dt)` for
  a continuous target (the host, offline). Output: `Position`, `Velocity`, `Speed`, `Yaw`, `YawRate`, `Bank`, `Pitch`,
  `Facing`, `Body` (the facing with the lean), `Jumped` (a teleport this frame).
  Inside: a `BossSnapshotBuffer` (cubic Hermite through the snapshots, tangents from neighbours, C1; past the newest the
  velocity is held for 40% of `MaxExtrapolate` and then eased to rest; a jump beyond `TeleportDistance` cuts the curve),
  two goal-velocity springs in a row (`PositionHalfLife`), a `BossTurn` (`MaxTurnRate`, `MaxTurnAccel`, `TurnHalfLife`) and
  second-order lean (`BankScale`, `MaxBank`, `PitchScale`, `SpeedPitch`, `MaxPitch`, `LeanFrequency`, `LeanDamping`).
- `BossRenderClock`: a render time on the authority's timeline `Delay` behind its estimate of the newest data (a max
  filter over arrival offsets). It runs up to `MaxSkew` faster or slower to hold the delay (the rate itself slews at
  `SkewRate`), grows its cushion after a late frame (`MaxExtraDelay`, back down at `ExtraDecay`), never runs backwards and
  snaps past `SnapError`.

### Gait (`NativeBossMotionGait.cs`)

- `BossGaitDef` (shared per kind): `Legs` (`BossLegDef`: `Hip`, `Home`, `Pole` in the body frame, `Upper`, `Lower`,
  `Phase` 0..1, `Side`, `Ankle`), speeds and strides at walk and run, duty, lift, `MinFrequency`, swing limits,
  `SettleDistance`/`SettleSwing`, `ReachMargin`, `MaxSink`, `BodyHeight`, `Bob`/`RunBob`, `Sway`, `Roll`, `Twist`,
  `LandDip`, `SupportFrequency`/`SupportDamping`, `LiftPeak`, `ToeOff`, `HeelStrike`, `FloorFollow`.
- `BossGait.Step(root, yaw, yawRate, velocity, body, dt, ground)`: `root` the presented root on the ground, `body` the
  follow's `Body`. Set `ExtraOffset`/`ExtraRotation` first for a rig's own body move (a crouch, a lean): the feet stay
  planted. Output per leg: `Foot`, `Knee`, `Hip`, `FootRotation`, `Upper`, `Lower` (bone rotations), `Planted`, `Swing`
  (0..1 or -1), `Forward` (-1 behind .. 1 ahead: what an arm swings against), `Landed` (bitmask this frame) and
  `LandSpeed`; for the body: `BodyPosition`, `BodyRotation`, `Bob`, `SwayOffset`, `BankOffset`, `TwistOffset`, `Sink`;
  and `Phase`, `Frequency`, `Stride`, `Duty`, `Run`, `Speed`, `Moving`. `Reset(root, yaw, ground)` plants every foot.
  The cadence is ground speed over stride, so a planted foot is exactly still. A foot lifts on its beat, when the body
  has left it beyond reach, when the hips would have to sink more than `MaxSink` for it, or (standing) when it is out of
  place, one group at a time. Legs sharing a phase offset are a group (a tripod, a trot diagonal). Setting off, the cycle
  starts on the beat with the trailing group.
- `IBossGround.Height(x, z)`: the floor under a point (`BossFlatGround` for tests).
- `BossIk.TwoBone(root, target, upper, lower, pole, out end, soft=0)`: the middle joint; `soft` (a share of the reach)
  removes the pop as a limb straightens.

### Chains (`NativeBossMotionChain.cs`)

- `BossChainDef`: `Count`, `Length`, `Rest` and `Pole` (anchor frame), `Stiffness` to `TipStiffness` (1/s toward the rest
  shape), `Damping`, `Gravity`, `MinStretch`/`MaxStretch`, `Radius`, `Friction`, `MaxStep`, `Iterations`.
- `BossChain.Step(anchor, orientation, dt, ground)`: verlet with symmetric link passes and a final exact-length pass that
  adds no velocity; `Point[]` and segment `Rotation[]` (local -Y down the segment).

### Actions (`NativeBossMotionAction.cs`)

- `BossActionDef(name, blendIn, blendOut, phases...)` with `BossPhase(kind, seconds, to, ease, shape)`. Kinds:
  `Anticipation`, `Strike`, `Contact`, `FollowThrough`, `Recover`. Eases: `In` (accelerate into the end), `Out`, `InOut`,
  `OutBack`, `Settle` (over and back, landing still), `Hold`, `Linear`. `To` is the rig's key coordinate (0 rest, 1 wound,
  2 struck, ...). `HitAt` is where the first contact phase starts.
- `BossTimeline.Sample(def, age, hitAt, duration)`: a pure function. The phases before contact stretch to the
  authority's hit and those after to the rest of its length, so contact lands on the authority's hit at any timing.
  `BossTimeline.Key(keys, progress)` reads a rig's key table.
- `BossActionPlayer`: `Play(def, id, start, hitAt, duration)` (idempotent: call it every frame with the replicated record),
  `Cancel(at)` (holds and fades), `Sample(time)` gives `Current` and `Previous` (`Progress`, `Weight`, `Phase`, `PhaseT`,
  `Struck`, `SinceHit`); a new action's blend-in hands the old one out on the same clock.

### Layers (`NativeBossMotionLayers.cs`)

- `BossBreath.Sample(time, rate, seed)` (in over 40%, out over 60%, slope-continuous) and `BossBreath.Idle(time, seed,
  channel, rate)` (smooth noise).
- `BossLookAt.Step(eye, bodyYaw, hasTarget, target, dt)` gives `Yaw`, `Pitch` (+ down) and `Weight`, within `MaxYaw`,
  `MaxUp`, `MaxDown`, lagging on `HalfLife`, capped at `MaxRate`; spread it down the spine with the rig's shares.
- `BossFlinch.Add(at, push, size, bodyYaw)` and `Sample(time, out pitch, out bank, out shift, delay)`: an analytic impulse
  response per blow (`Rise`, `Wobble`), deterministic, duplicates ignored; sample a lagging part with a small `delay`.
- `BossStagger.Start(at, push, size, seconds, bodyYaw)` and `Sample(...)`: hips thrown along the push and held (the gait
  steps after them), the trunk whipping and leaning, the knees giving, then a settle back.

### Pose and pop killer (`NativeBossMotion.cs`, `Runtime/NativeBossPoseApply.cs`)

- `BossPose`: `Position[i]`, `Rotation[i]` (world), `RootPosition`, `RootRotation`; `Blend`, `CopyFrom`, `Delta`. A joint
  is its pivot (hip, knee, shoulder) and its segment's rotation: local -Y runs down a limb, +Z forward, so identity is a
  limb hanging straight down with its knee forward.
- `BossInertializer.Transition(shown, next)` then `Apply(source, halfLife, dt, output)`: when the source jumps (a
  teleport, a late join, a model swap), the difference decays on springs: continuous, no lag once settled.
- `NativeBossPoseApply.Apply(pose, parts)` sets each part's world position and rotation in index order (parents first);
  `Matrix(pose, i, offset, scale)` for instanced drawing.

## Example wiring

`Core/NativeBossMotionRigs.cs` is the compiled example: `BossBipedRig` (heavy biped, about 4.6 m, long arms, an overhead
slam, shackle chains) and `BossCrawlerRig` (six legs on a tripod gait, a dragging cable tail, a tracking sensor head). A
boss presenter in the runtime follows the same pattern:

```csharp
// One per boss on screen (Runtime, presentation only).
sealed class StationBossView
{
    readonly BossBipedRig rig = new BossBipedRig();     // or the boss's own rig built the same way
    readonly Transform[] parts;                          // one per joint index
    readonly IBossGround ground;                         // map floor height (raycast or nav floor)

    // Every replicated frame (guest) or fixed step (host): the authority's record, never a pose.
    public void Observe(NativeGameFrame f, double arrivedLocal, BossRecord b)
    {
        rig.Follow.Observe(f.time, arrivedLocal, b.position, b.yaw * Mathf.Deg2Rad);
        if (b.actionId != 0) rig.Actions.Play(Actions[b.action], b.actionId, b.actionAt, b.hitAt, b.duration);
        if (b.cancelAt > 0) rig.Actions.Cancel(b.cancelAt);
        if (b.staggerAt > 0) rig.Stagger.Start(b.staggerAt, b.staggerPush, 1, b.staggerSeconds, rig.Follow.Yaw);
    }
    public void Hit(double at, Vector3 push, float size) => rig.Flinch.Add(at, push, size, rig.Follow.Yaw);

    // Every rendered frame.
    public void Present(double renderTime, float dt, bool hasTarget, System.Numerics.Vector3 target)
    {
        rig.Follow.StepAt(renderTime, dt);               // guest: arena.ReplicaRenderTime; host: the visual clock
        rig.Update(renderTime, dt, ground, hasTarget, target);
        NativeBossPoseApply.Apply(rig.Pose, parts);
    }
}
```

On the host, feed each fixed step as a snapshot (`Observe(simTime, simTime, ...)`) and present at the visual clock one
step behind (`StepAt(visualTime - 1/60, dt)`) so the 60 Hz simulation is drawn smoothly at 144 fps; or call
`Follow.Follow(position, velocity, yaw, dt)` when the position is already continuous. Build a boss's own rig the way the
two references do: a `BossGaitDef` for its legs, an action table (`BossActionDef` plus key tables per channel), chains for
anything that hangs or trails, second-order dynamics for parts that should lag (arms, chest, head), the layers on top, and
the pose written by joint index. Forms without legs use the same parts:

- **Fused locomotive:** `BossRootFollow` for the body; wheels turn by distance (`Speed` integrated over the radius); the
  couplings and hoses as `BossChain` (a `MinStretch` below 1 for slack); the body's sway from the follow's `Bank` through a
  `BossSecondOrder` (low frequency, zeta about 0.4) so it rocks on the curve and settles after.
- **Converter vessel:** a `BossSpringQ` or second-order tilt toward the pour angle, the pour as an action phrase
  (anticipation tilt back, a strike into the pour, a settle), a chain for the hanging hook or ladle.
- **Cranes and chains:** the boom's yaw on a `BossTurn`, the cable and hook on a `BossChain` (inextensible, gravity, floor
  friction), a jib's lag on second-order dynamics.

## Tuning

| Knob | Effect | Heavy biped | Crawler | Notes |
| --- | --- | --- | --- | --- |
| `PositionHalfLife` | how much snapshot noise and corrections are smoothed | 0.14 | 0.14 | below 0.08 jitter shows; above 0.25 corrections drift |
| `MaxTurnRate` / `MaxTurnAccel` | turn speed and how hard it winds into and out of turns | 1.9 / 6.5 | 2.2 / 8 | heavier: lower both; the authority's facing is unchanged |
| `MaxBank` / `LeanFrequency` / `LeanDamping` | lean into turns and speed changes | 0.16 / 1.1 / 0.55 | 0.08 / 1.6 / 0.6 | lower frequency reads heavier |
| `WalkStride` / `RunStride` | metres per cycle (a step of every leg) | 2.3 / 4.1 | 1.7 / 3.3 | about one leg length per step at a walk |
| `WalkDuty` / `RunDuty` | stance share | 0.64 / 0.5 | 0.6 / 0.46 | keep 0.5 or more for N legs, so a group is always down |
| `MinFrequency` | slowest cadence (slow walking takes short steps) | 0.52 | 0.55 | too low leaves feet behind when it sets off |
| `Bob` / `Sway` / `Roll` / `LandDip` | weight | 0.07 / 0.11 / 0.06 / 0.35 | 0.035 / 0.03 / 0.035 / 0.12 | the dip is per footfall, scaled by speed |
| `SupportFrequency` / `SupportDamping` | the weight shift's lag and overshoot | 1.6 / 0.5 | 2.6 / 0.6 | |
| `MaxSink` | how far the hips may sink before a foot steps instead | 0.05 | 0.05 | share of the leg's length |
| Arm second order (rig) | arm lag and follow-through | 1.7 Hz, 0.48 | | near the gait cadence it resonates: keep it 2x away |
| Anticipation / strike | readability against speed | 0.75 s / 0.26 s (`In` 2.2) | | strike peak speed is shape x the average |
| Contact / follow-through / recover | weight after the hit | 0.14 `Out` / 0.34 `InOut` / 0.85 `Settle` 1.4 | | `Settle` shape lower wobbles more |
| `BlendIn` / `BlendOut` | the action's hand-over with locomotion | 0.16 / 0.4 | | |
| Chain `Stiffness` / `TipStiffness` / `Damping` | stiff tail against free rope | | 3.2 / 0.15 / 1.1 | shackles: 0 / 0 / 0.7 |
| `BossIk` `soft` | removes the straightening pop | arms 0.06 | | legs stay exact (the sink keeps them in reach) |

Rules of thumb: the bigger the body, the lower every frequency (roughly with the square root of its size) and the longer
the anticipation; put the speed in the strike (`In` into contact) and the weight after it (`Out`, then `Settle`); let
parts lag (second order) rather than delaying them; never blend rotations that may be half a turn apart (blend targets
and solve, as the biped's arms do).

## What the authority must replicate (for Astra)

Per boss, on top of the root position and yaw that frames already carry:

- **Action:** an action id (which phrase), a sequence number (unique per start), the start time on the authority's clock
  (`Match.Time`), the hit time after the start and the length when they vary from the authored ones, the target (pilot
  id or point) and the facing at the start, and a cancel time if the action is interrupted.
- **Reactions:** blows as events with their time, the attacker's position (the direction) and a size; a stagger's start,
  push and length.
- **Placements:** a teleport or placement flag (or the existing 5 m rule).

Why start times rather than per-frame poses: a pose sent at 20 Hz arrives quantised, late and jittered, and is exactly
the stepping this foundation removes. A start time is one reliable value per action; every peer evaluates the same
analytic timeline at its own render rate (144 fps or more). The contact frame lands on `start + hitAt` on the authority's
clock on every peer: the host, every guest at its render delay, a late joiner (it reads the same record mid-action) and
the killcam. Bandwidth stays flat however many joints a boss has. Hit timing, hitboxes and damage stay authority-side
and unchanged: the presentation only aligns its contact frame with them.

## Checks and renders

`Tests/BossMotionChecks.cs` (in the default managed run and as `--boss-motion`):

- Springs converge, halve on their half-life, never overshoot at critical damping, match the closed form at 30-240 Hz
  and track moving goals with no lag.
- The turn keeps its rate and acceleration limits and stops on the goal.
- Jittery 20 Hz snapshots (45 ms, 14 ms jitter, 5% 120 ms late, 3 mm noise, 144 Hz +-15% frames): peak acceleration
  13.6 m/s^2 with no stall, against the shipped follow's 358 m/s^2 and 12 frozen moving frames.
- Planted feet never move (biped and crawler, walking, running, stopping, turning sharply, shoved), and the legs reach
  them.
- Chains hold length and the floor and settle.
- The action timeline is bit-identical across two peers at different frame rates, one joining late, and lands contact on
  the authority's hit.
- The pose is continuous across action starts, contact, a crossfade, a cancel into a stagger and flinches: doubling the
  frame rate halves every per-frame change.
- The layers stay in their limits; 0 bytes allocated per frame.

Offline renders (a WebGL harness in the scratch space through the same Core code, not Unity) are listed in the DEV_LOG
entry. Not verified here: the Unity Editor, a Windows build, real boss models and two-client play.

## Limits

- Procedural only: the bosses' bodies, keys and timings are theirs to author; the reference rigs are demonstrations.
- The gait is tested on flat ground; slopes and steps rely on `IBossGround`, and the feet do not pitch to the slope.
- Two-bone IK only (no full-body IK, no collision between parts); chains collide with the floor only.
- The inertializer ignores velocity at a transition (position-continuous, not velocity-continuous).
- `BossRenderClock`'s cushion can grow by up to 120 ms after late frames: prefer `StepAt(arena.ReplicaRenderTime)` so
  the boss stays in step with the other effects, and use the built-in clock only for a channel of its own.
- A sharp authority corner is rounded by the follow's springs, which cut it and then catch up (a brief speed overshoot of
  about 10% at a 90 degree corner at 3.4 m/s).
