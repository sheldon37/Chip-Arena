# Melee feel: evaluation, plan and handoff (2026-10-12)

Owner request, 2026-10-12 (verbatim): "we may need to balance melee to stand on par with ranged/magic. They need more
ways to gap close, and also, we need to improve the "fluidity" of them, it doesnt feel smooth swinging right now now.
Think like dark souls swinging, its fluid and allows multiple swings. Maybe we need small movements forward when swinging
too? Please evaluate. Also, the movement for melee is a little weird on the torse, notice how the body rocks when moving."

- **Scope:** the Unity game (`unity/ChipArenaConversion`). Every `file:line` below is at commit `19c00c7` and relative to
  `unity/ChipArenaConversion/Assets/ChipArena/` unless stated.
- **Ownership** (AGENTS.md role split): each recommendation is tagged **PRESENTATION** (Claude: poses, blending, VFX
  timing) or **SIMULATION** (Astra: input, timing, turning, steps, abilities, bots, replication).
- **Status:** the presentation items (P1–P5, P7, P8 and the crescent sync) are implemented on branch
  `claude/melee-feel` (see [Presentation work](#presentation-work-on-claudemelee-feel)); P6 waits for S2. The simulation
  items are a proposal for Astra; none is started.
- **Evidence:** reading the code, plus an offline probe (local notes, not committed). The probe compiles `Core/`
  outside Unity, poses pilots through the real `MeleeMotion`, `MeleeGait` and `GunMotion`, and drives them with an
  authority timeline rebuilt from `NativeMeleeRules` with held LMB, as `ConversionGame` sends it. One pilot, flat floor,
  attackRate 1, no chips, 60 Hz. Renders use an offline box harness, not Unity. The Dark Souls figures are approximate
  genre conventions, not measured frame data.

## Findings

### 1. Swings don't feel smooth

The cause is mostly structural (simulation), with presentation errors on top.

**Current combo structure** (attackRate 1, no chips):

| | Sword | Daggers | Shield | Greatsword |
|---|---|---|---|---|
| Windup (`w.windup`) | 0.14 s | 0.09 | 0.20 | 0.42 |
| Active hit window | 0.16 | 0.16 | 0.16 | 0.16 |
| Recovery (`Recovery()`, Core/NativeMeleeRules.cs:21) | 0.30 | 0.15 | 0.30 | 0.50 |
| Swing period `Until` (Core/NativeMeleeRules.cs:26, Runtime/NativeMelee.cs:40) | 0.62 (0.633 on the 60 Hz tick) | 0.40 | 0.75 | 1.18 |
| Lockout after the hit window | 0.32 | 0.15 | 0.39 | 0.60 |
| Forward step (Core/NativeMeleeRules.cs:27) | 0.65 m | 0.85 | 0.55 | 0.45 |
| Stamina per swing | 14 | 7 | 16 | 20 |

**Simulation causes:**
- **No input buffer.** `ConversionGame` sends `Shoot = Input.GetMouseButton(0)`, held state
  (Runtime/ConversionGame.cs:112). The edge flag `InputFrame.Attack` exists but is never set (Core/ArenaModel.cs:25,34).
  `TryMelee` refuses while `Time < AttackEnd` (Runtime/NativeMelee.cs:36). A held button chains exactly at `Until`; a
  click released before `Until` is lost. Probe: one click 0.1 s into recovery, released 50 ms later, gives 2 clicks and
  1 swing. Q and E already buffer (`BufferedSlot/BufferedAt`, Runtime/NativeArena.cs:27); LMB does not.
- **No chain point.** Recovery can only be cancelled by a roll (Runtime/NativeMelee.cs:24-27). Every swing pays its full
  recovery and starts again from neutral.
- **No combo reset.** `p.Combo` only increments (Runtime/NativeMelee.cs:38), so after a pause the next swing continues
  the cycle and the sword's narrow 0.8 rad third cut can be the opener. The old lab model reset after 2.4 s
  (Core/ArenaWorld.cs:158).
- **Turning is frozen.** Body yaw is locked from swing start to `AttackEnd` (Runtime/NativeArena.cs:124-125) while the
  camera keeps turning; the next swing takes the new yaw in one tick.
- **Full root lock.** `MoveVelocity` is zeroed at swing start and while locked (Runtime/NativeMelee.cs:42,
  Runtime/NativeArena.cs:156). Only the scripted 0.45–0.85 m step moves the body, and the heaviest weapon steps least.
  Walking then restarts from 0 at 35 m/s² (0.19 s to 6.8 m/s). Sprint is refused while LMB is held
  (Runtime/NativeArena.cs:146).
- **Networking (inferred, not run).** Guests do not predict swings: prediction stays unlocked until the host's
  `AttackUntil` arrives (Runtime/NativeReplicaMotion.cs:19), so a guest keeps walking for about one round trip after
  LMB and is then corrected.

**Presentation causes** (held chain, hits landing, blade-tip speed at 60 Hz):
- The retimed captured cuts put the blade's fastest motion *before* the hit window. `CapturedClip.AttackTime`
  (Core/CapturedClip.cs:52-64) maps contact marks m1..m2 onto the 0.16 s window, but the authored whip starts 15–45 ms
  before m1, so it plays at about 1× in the last frames of the windup and is then stretched across the window at
  0.13–0.4×.
- Inside the window the blade nearly freezes, and at its end it stops dead:

  | | Windup peak | Active window min | Speed at recovery start | Guard idle before the next held swing |
  |---|---|---|---|---|
  | Sword | 100–134 m/s | 5–6 | 0.07–0.4 (dead stop) | 0.017–0.033 s |
  | Daggers | 60–88 | 1.2–6.9 | 2–23 | 0 |
  | Shield | 19–22 | 1.6–2.1 | 3–5 | 0.083 s |
  | Greatsword | 47–66 | 2.9–5.6 | cut B: 106 m/s one-frame pop, then 0.75 | 0.10 s |

  With whiffs and no hit-stop, the sword's mean tip speed inside the window is 18.7 m/s against a 7× higher windup peak.
- **No follow-through.** Sword, shield and greatsword recovery slerps straight from the contact pose to the guard
  (Core/MeleeMotion.cs:262-266), against docs/DESIGN_INTENT.md ("anticipation … impact, follow-through, and recovery").
- **Short visual recovery.** `NativeArena` already sends `VisualDuration = AttackEnd − AttackAt`
  (Runtime/NativeArena.cs:267), but `MeleeMotion` uses `Recovery(spec)` (Core/MeleeMotion.cs:76,80-85). Shield (0.39 vs
  0.30 s) and greatsword (0.60 vs 0.50 s) reach the guard early and stand there.
- **Turn snap.** A changed commit yaw is not blended (`if(u<1&&!commit&&!strike)`, Core/MeleeMotion.cs:96). A 40° camera
  turn during recovery jumps the body 0°→40° in one frame: a 66 m/s blade-tip pop.
- **Dagger windups** are compressed ×4.7–5.7 (0.43–0.51 s of clip into 0.09 s).
- **Slash crescent lag.** The crescent sweeps the authority arc uniformly over `HitAt..HitUntil`
  (Core/NativeWeaponFx.cs:243-249, `Frame` :277-288), but the visible blade has whipped past before `HitAt`, so the
  effect trails the body by about 50–100 ms.
- **Hitbox sweep mirrored (found while syncing the crescent; SIMULATION).** `NativeMeleeRules.Angle`
  (Core/NativeMeleeRules.cs:30) sweeps `Yaw + (u − 0.5)·Arc`, ported from the source where yaw grows toward the pilot's
  *left*. In the native frame yaw grows toward the *right*, so every swing's hit test runs mirrored: cut A sweeps the
  hitbox left→right while the captured blade (and the source's sweep) goes right→left; the `Reverse` cuts mirror the other
  way. The contact parity check passes because it replays the source in the source frame
  (Core/NativeMeleeChecks.cs:34). Which side of the arc is hit first changes; the arc, reach and timing do not. The slash
  crescent showed the hitbox's direction, so it opened against the blade.

### 2. Melee can't close the gap

- Everyone moves at 6.8 m/s (Runtime/NativeArena.cs:151); nobody is slowed when backpedalling or firing.
- Melee's closers: sprint (double-tap W, ×1.35 = 9.18 m/s, forward cone only, not while LMB is held; 12 stamina/s);
  rolls (4.0–5.8 m, chained with sprint no faster than sprint, about 9 m/s); the 0.45–0.85 m swing step; the ~2 m
  greatsword Wolf Spin hop. Lock-on (Tab, Runtime/NativeLockOn.cs) steers the camera only: no dash, lunge or magnetism.
- From 15 m a perfect-aim ranged or magic pilot deals **76–136 damage** before a sprinting sword pilot can swing (HP 100).
- **The agreed Q/E kit (docs/MELEE-ABILITIES.md) is not started:** no GameContent abilities, no branch, no PR. Only the
  presentation piece (the block effect) exists.

Time to weapon contact (reach + 0.45 m) from standstill, walking / sprinting, against a static shooter:

| Distance | Sword | Daggers | Shield | Greatsword |
|---|---|---|---|---|
| 10 m | 1.13 / 0.90 s | 1.24 / 0.98 | 1.19 / 0.94 | 1.00 / 0.80 |
| 15 m | 1.87 / 1.44 | 1.98 / 1.53 | 1.93 / 1.49 | 1.74 / 1.35 |
| 20 m | 2.60 / 1.99 | 2.71 / 2.07 | 2.66 / 2.03 | 2.47 / 1.89 |

Perfect-aim damage taken by a sword pilot while closing (walk / sprint, projectile travel included; halve for a
realistic 50–60 % hit rate):

| Weapon | 10 m | 15 m | 20 m |
|---|---|---|---|
| Pistol | 69 / 69 | 115 / 92 | 161 / 115 |
| Machine gun | 104 / 88 | 176 / 136 | 248 / 184 |
| Rifle | 58 / 58 | 116 / 116 | 174 / 116 |
| Fire | 75 / 50 | 102 / 76 | 152 / 102 |
| Water | 60 / 60 | 100 / 80 | 140 / 120 |
| Lightning | 60 / 60 | 105 / 90 | 150 / 105 |
| Darkness | 68 / 68 | 102 / 102 | 136 / 102 |

Closing on a kiting target (sword sprinting, the target walking backward at 6.8 m/s and firing; closing rate 2.38 m/s):

| Distance | Now | +2 m opener lunge | +2.6 m sprint attack | +Flash Step (7 m, 0.34 s) | +8 % melee speed | Backpedal ×0.85 for all | Flash Step + 8 % |
|---|---|---|---|---|---|---|---|
| 10 m | 2.96 s | 2.39 | 2.07 | 0.36 | 2.26 | 2.07 | 0.36 |
| 15 m | **5.06** | 4.50 | 4.17 | 2.46 | 3.87 | 3.54 | 1.96 |
| 20 m | 7.16 | 6.60 | 6.27 | 4.56 | 5.47 | 5.01 | 3.57 |

### 3. The torso rocks while moving

- `MeleeGait.Locomotion` twists the procedural pelvis ±0.14 rad and rolls it ±0.05 rad (Core/MeleeGait.cs:195). Then
  `MeleeMotion.Ready` applies the captured idle, walk and run clips (Core/MeleeMotion.cs:237-247), and
  `MeleeCapture.Apply` slerps the spine, chest, neck and head to the clip (Core/MeleeCapture.cs:56-60). Above about
  3.5 m/s the clip's torso replaces the procedural counter-rotation completely.
- The clips' spine/chest twist (42–50° local p-p) was authored to counter the capture's own pelvis (25–30° p-p). Over
  the procedural pelvis (16° p-p, another phase) it leaks into the chest. The clips also play 1.9× (walk) to 2.6× (run)
  their authored 0.8 s cycle, 3× when sprinting (stride rate, Core/MeleeGait.cs:42,131).
- Guns cancel the travel lean, roll and hip twist on the chest while the gun is held (Core/GunMotion.cs:115-120); melee
  did not.

Measured (degrees peak-to-peak in the pilot's frame, over 3 s of steady travel):

| At 6.8 m/s | Chest twist | Chest roll | Chest nod | Shoulder tilt | Head sideways sway | Mean chest pitch |
|---|---|---|---|---|---|---|
| Sword / daggers | 52.2 | 20.4 | 23.2 (6.5 Hz) | 21.8 | 6.0 cm | 49° |
| Shield | 24.7 | 10.7 | 22.9 | 10.1 | 5.8 cm | 37° |
| Greatsword | 30.5 | 22.2 | 27.3 | 22.9 | 15.7 cm | 39° |
| Rifle (reference) | 13.4 | 0.9 | 0.5 | 0.9 | 1.1 cm | 10° |

At a 3 m/s walk the sword chest twists 39.4° with 11.1 cm of head sway (rifle: 5.9°, 1.8 cm). Peak chest twist rate at a
run was 908°/s (rifle 135°/s).

## Prioritised recommendations

Effort: XS < ½ day, S ≈ 1 day, M ≈ 2–3 days, L ≥ 1 week. "No sim impact" means no change to hit timing, hitboxes,
damage, cooldowns, movement, stamina or network data.

| # | Change | Tag | Effort | Impact | Status |
|---|---|---|---|---|---|
| 1 | P1 steady locomotion torso | PRESENTATION | S | No sim impact | Done on `claude/melee-feel` |
| 2 | S1 LMB input buffer | SIMULATION | S | No timing/hitbox/damage/cooldown change; one edge bit on the wire. Biggest "multiple swings" fix | Proposed |
| 3 | P2–P8 swing retime, follow-through, real recovery length, turn blend, cut B pop, dagger windups, crescent sync | PRESENTATION | M | No sim impact | Done on `claude/melee-feel` |
| 3b | S0 un-mirror the hitbox sweep (`NativeMeleeRules.Angle` in the native frame) | SIMULATION | XS | Which side of the arc is hit first; arc, reach and timing unchanged. Needs the contact parity check converted to the native frame | Proposed |
| 4 | G1 melee Q/E kit (docs/MELEE-ABILITIES.md) | SIMULATION; animation, VFX, HUD PRESENTATION | L | New abilities, damage, cooldowns, replication | Not started |
| 5 | S2 chain points | SIMULATION | M | Chained DPS +20–25 %; stun escape window ~0.40→0.26 s | Proposed |
| 6 | S3 windup turning | SIMULATION | S–M | Hitbox yaw can change before the active window only | Proposed |
| 7 | S4 step defaults + magnetism (G2 opener lunge) | SIMULATION | M | Movement during the step window (up to ~16 m/s) | Proposed |
| 8 | P6 chain entry from the follow-through (after #5) | PRESENTATION | S | No sim impact | Waits for S2 |
| 9 | G3 sprint attack | SIMULATION + PRESENTATION | M | New attack kind, replicated flag | Proposed |
| 10 | G8 melee bot approach AI, then the duel harness | SIMULATION | M | Bots only; needed to measure balance | Proposed |
| 11 | S5 combo reset | SIMULATION | XS | Opener always cut A | Proposed |
| 12 | S7 guest swing prediction | SIMULATION | M–L | Predicted lock and step, reconciled to the host | Proposed |
| 13 | G4 melee speed +6–8 % / sprint drain 8/s; G5 50 % block refund on a reflect | SIMULATION | XS each | Tuning; only after #4, #7, #10 are measured | Gated |
| 14 | S6 recovery release | SIMULATION | S | Gates and movement; optional | Proposed |
| 15 | G6 ×0.85 projectile damage while sprinting; G7 backpedal ×0.85; charged heavy | SIMULATION | XS–L | Cross-class balance; owner decision | Owner |

## Simulation proposal for Astra

In this order for fluidity (S1–S5), then gap closing. Numbers at attackRate 1; scale by `1/attackRate` like the
existing timings.

- **S0. Un-mirror the hitbox sweep.** In the native frame the sweep should run `Yaw − (u − 0.5)·Arc` (and `+` for the
  `Reverse` cuts), so the hit order matches the captured blades and the source. Keep `NativeMeleeChecks` replaying the
  source by converting its fixture (negate yaw and delta x) rather than the formula. The slash crescent already follows
  the visible blade, so it and the hitbox then agree; no presentation change is needed.

- **S1. LMB input buffer.**
  - Send the press edge: `pending.Attack |= Input.GetMouseButtonDown(0)`; check that the native input codec carries
    `InputFrame.Attack`.
  - Store a press made between `HitAt` and the end of recovery (`AttackEnd`), and during the last 0.2 s of a roll.
    Fire it at the earliest legal tick. Expire it after **0.4 s**. Keep held-LMB repeat.
  - No change to timing, hitboxes, damage or cooldowns; one edge bit on the wire.
- **S2. Chain points.** Allow the next combo step at `max(windup + active + c·recovery, 0.8·interval)`, c = 0.4 for
  daggers and sword, 0.5 for shield and greatsword:

  | | Chain period | Was | Sustained DPS |
  |---|---|---|---|
  | Daggers | **0.32 s** | 0.40 | 37.5 → 47 |
  | Sword | **0.50 s** | 0.62 | 48 → 60 |
  | Shield | **0.60 s** | 0.75 | 31 → 38 |
  | Greatsword | **0.95 s** | 1.18 | 44 → 55 |

  - A swing that is not chained keeps its full `AttackEnd`, so the whiff punish is unchanged.
  - About +20–25 % chained DPS (an intended melee buff). Stamina per second rises (sword 28/s), so chains stay capped at
    5–14 swings. Stagger immunity (stun end + 0.5 s) still prevents stun-locks; the target's free window between stun
    end and the second hit shrinks from about 0.40 s to 0.26 s, still enough to block or roll.
  - Update `NativeMeleeValidation`, which checks the committed-primary gates.
- **S3. Windup turning.** Turn the swing yaw toward `Input.Yaw` during the windup at daggers **720°/s**, sword
  **540°/s**, shield **450°/s**, greatsword **300°/s** (at most about 65°, 75°, 90° and 126°); **lock it at `HitAt`**.
  During recovery the body may follow the input at 180°/s so the next swing never snaps more than about 30°. Lock-on
  already steers `Input.Yaw`, so this doubles as lock-on tracking. The hitbox direction changes before the active
  window, never during it.
- **S4. Step defaults and magnetism.**
  - Default step without a target: daggers **0.9 m**, sword **0.8**, shield **0.75**, greatsword **1.0**.
  - Target: the nearest visible enemy within ±35° of the swing yaw and within reach + 2 m. Step =
    clamp(distance − 0.8·reach, default, max), stopping at 0.9 m separation. Keep the `StepAt/StepUntil` windows.

    | Max step | Daggers | Sword | Shield | Greatsword |
    |---|---|---|---|---|
    | Chained swing | 1.3 m | 1.4 | 1.2 | 1.6 |
    | First swing (opener lunge, G2) | 2.2 m | 2.0 | 1.8 | 2.4 |

  - Peak step speed about 10–16 m/s (roll-like). Hit timing unchanged; the hitbox origin moves with the body. The host
    computes the step at swing start; `StepDistance` is already replicated.
- **S5. Combo reset.** Reset `Combo` when `Time > AttackEnd + 0.35 s`, so the opener is always cut A (sword arc 1.6 rad).
- **S6. Recovery release (optional).** Block from `HitUntil + 0.5·recovery`; walk at 50 % speed from
  `HitUntil + 0.7·recovery`; do not zero `MoveVelocity` at release.
- **S7. Guest swing prediction.** Predict the swing commitment (movement lock and step) on the LMB edge and reconcile to
  the host. **Risk today:** a guest keeps walking for about one round trip after LMB and is then pulled back
  (Runtime/NativeReplicaMotion.cs:19); with S4's longer lunges the correction grows.
- **G1. The Q/E kit** (docs/MELEE-ABILITIES.md): not started. It is the largest gap-closing fix: Flash Step alone turns
  the 15 m kite from 5.06 s into about 2.5 s (table above).
- **G3. Sprint attack.** LMB after ≥ 0.3 s of sprint: 2.6 m lunge (daggers 3.0, greatsword 2.4), windup +0.04 s,
  damage ×1.15, stamina ×1.3, recovery ×1.3; chains into step 2; punishable on a whiff.
- **G8. Melee bot approach AI.** Bots sprint, roll in and lunge. docs/ABILITIES.md blames the walk-in AI for the ~100 %
  duel losses.
- **Gate before tuning knobs.** Re-run the bot duel harness after G1 + G2 (+ G8), target 25–55 % in cover, before
  G4 (melee speed +6–8 %, sprint drain 12→8/s), G5 (50 % block-cooldown refund on a projectile reflect) or G6 (×0.85
  projectile damage while sprinting forward).
- **Owner decisions:** G7 backpedal ×0.85 for every class (changes ranged feel; kiting closure becomes 3.4 m/s), and a
  charged heavy (hold LMB).
- Not recommended: a block slow (Unity already walks at full speed while blocking); a separate lock-on dash (duplicates
  Flash Step and Shadow Step; prefer magnetism).

## Presentation contract Astra must keep

`Core/MeleeMotion`, the first-person view model, the killcam and guests read only replicated actor fields, set in
`Runtime/NativeMeleePresentation.cs` and `Runtime/NativeArena.cs:262-267`:

- **Swing timing:** `AttackAt` (last swing start), `Combo`, `SwingAt/SwingUntil` (= `HitAt/HitUntil` until the window
  ends), `CommitAt/CommitUntil` (= swing start and `AttackEnd`), and `VisualWindup/VisualActive/VisualDuration`
  (`VisualDuration = AttackEnd − AttackAt`; P4 uses it as the real recovery length). A chained swing (S2) must
  start a new `AttackAt` and increment `Combo` as today; presentation blends into it from the current pose.
- **Commit yaw:** `CommitYaw` is the swing yaw. Once S3 lands, update it every tick during the windup and freeze it at
  `HitAt`; presentation eases a changed commit yaw over 0.1 s (P5), so per-tick updates are smooth.
- **Hit window:** `SwingAt/SwingUntil` must stay the authority's `HitAt/HitUntil`: the captured sweep, the slash crescent
  and the hit-stop are all placed in it. The crescent reads `Combo + 1` to pick the cut the pilot plays.
- **Step fields:** `StepAt/StepUntil/StepDistance`, computed by the host at swing start (with magnetism up to 2.4 m).
  The footwork plants against them.
- **Attack kind:** add a replicated flag (or kind) for the **sprint attack** and the **opener lunge**, so presentation
  can choose the lunge clip and footwork for long steps.
- Presentation never writes authority state; none of the presentation items below changes hit timing, hitboxes,
  damage, cooldowns, movement, stamina or network data.

## Presentation work on `claude/melee-feel`

### P1. Steady locomotion torso (done)

`MeleeMotion.Ready` / `SteadyTorso` (Core/MeleeMotion.cs on the branch):
- Each captured walk and run clip keeps its cycle-mean torso (the weapon-carry posture, `CapturedClip.CycleMean`) and
  only **35 % (walk) / 25 % (run)** of its per-stride swing on the spine, chest, neck and head.
- The spine counters **75 % of the gait's pelvis twist and 80 % of its roll** (applied as a share of the pelvis
  rotation's exact inverse), so the shoulders keep a small counter-swing, as the gun hold cancels it on the chest.
- The run's forward hunch lifts 0.26 rad (15°) at run weight, 0.22 rad more when backpedalling (the gait's backpedal
  share, `MeleeGait.Back`), with the neck turned back so the gaze stays where it was.
- All weights follow the existing carry and run blends, so walking, running, stopping and the attack transitions stay
  continuous.

Before → after (probe; the Unity-owned check `Tests/MeleeFeelChecks.cs` measures the same quantities):

| | Chest twist | Chest roll | Chest nod | Head sway | Mean chest pitch |
|---|---|---|---|---|---|
| Sword / daggers run (targets ≤ 15°, 6°, 6°, 3 cm) | 52.2 → **10.3** | 20.4 → **3.0** | 23.2 → **5.8** | 6.0 → **2.3 cm** | 49 → 37° |
| Sword walk | 39.4 → 13.9 | 10.1 → 3.7 | 1.5 → 0.5 | 11.1 → 5.2 cm | 10° |
| Shield run | 24.7 → 5.2 | 10.7 → 2.5 | 22.9 → 5.6 | 5.8 → 2.2 cm | 37 → 26° |
| Greatsword run | 30.5 → 6.8 | 22.2 → 7.6 | 27.3 → 6.0 | 15.7 → 4.7 cm | 39 → 26° |
| Sword backpedal 6.8 m/s | 48.6 → 8.4 | 17.4 → 1.7 | 22.4 → 6.1 | 8.8 → 3.5 cm | 43 → 18° |
| Sword lock-on circle 6.8 m/s | 45.0 → 8.9 | 16.5 → 2.0 | 22.8 → 6.1 | 5.6 → 2.3 cm | 42 → 29° |

The largest frame-to-frame step of the chest's turn rate through speed ramps, turns, strafes, backpedals and stops fell
from 410–556°/s to 138–152°/s; running into and out of a swing from 306–447°/s to 77–237°/s. The greatsword run keeps
more roll (7.6°) than the sword: its captured run carries the blade across the body with a large authored roll.

### P2–P8. Swing presentation (done)

`Core/MeleeMotion.cs` on the branch: the static `MeleeSwing` (shared by the third-person rigs, the first-person view model
in `Runtime/CapturedAnimation.cs` and the crescent), `MeleeMotion.Cut`, `RecoverySeconds`, `PresentedYaw`, `HitStop` and
`FinishPose`; `MeleeCapture.FollowThrough`; `NativeSlashFx.Frame` in `Core/NativeWeaponFx.cs`.

- **P2 retime.** Each cut names its authored sweep (clip seconds, from the authored blade-tip speed) and plays it
  linearly across the hit window at 0.4–0.75× its authored speed; the windup plays the cut's pace keys rescaled up to the
  sweep, ending at the window's clip rate. Sword cut B's crowded pace keys are eased halfway to even steps.
- **P3 follow-through.** After the window the clip carries on at the window's rate and decelerates evenly to rest at
  the cut's follow-through end, while the live pose blends toward the fitted guard (cubic, over the recovery); the wrists
  still interpolate in chest space. The daggers keep their captured recovery and wrist limiter.
- **P4 real recovery.** While the commitment is replicated, the recovery runs to `CommitUntil` (the authority's
  `AttackEnd`), not `Recovery(spec)`; the footwork, pose transitions and guard hold use the same end.
- **P5 turn blend.** A swing that starts away from the shown facing, or whose commit yaw changes, eases in over 0.1 s
  (cubic ease-out); the strike window keeps the authority's yaw.
- **P7 daggers.** The cuts start after their authored wind-back (the entry blend from the guard stands in for it):
  windup compression ×4.7–5.7 → ×2.5–3.0.
- **P8 / hit-stop.** The greatsword cut B pop came from the hit-stop's 0.09 s catch-up racing the clip. The lost time is
  now caught up at most 1.3×, over the follow-through if needed.
- **Transitions.** Pose transitions into, out of and between the captured swings carry the last frame's joint velocity
  (as the reactions already did), so no swing starts or ends on a held frame.
- **Crescent sync.** The head follows the posed blade of the cut being played: it grows as the blade crosses the arc,
  in the blade's direction, and holds where the blade left it while fading. The shield's bash grows with the face's travel.
  This is opposite to today's mirrored hitbox order until S0.
- **P6 chain entry** waits for S2. The held chain already meets the next swing at the guard (at most 2–3 frames below
  1 m/s; before, 7). Skipping the entry blend on a held chain was tried and rejected: it made the next windup start with
  a jolt.

Held-LMB chains, 60 Hz, mean blade-tip speed in the hit window (whiffs), before → after:

| | Sword A / B / C | Daggers A / B / C / D | Shield A / B | Greatsword A / B |
|---|---|---|---|---|
| Mean in the window | 18.7 / 18.4 / 28.4 → **53.2 / 51.8 / 61.3** | 9.6 / 7.7 / 10.0 / 27.7 → **24.8 / 24.2 / 14.9 / 24.1** | 5.6 / 4.6 → 8.7 / 10.5 | 18.8 / 31.1 → **37.4 / 63.4** |
| Fastest frame | windup (100–134 m/s) → **in the window** (94–117) | windup → windup for A–C (the 90 ms windup repositions both blades: 37–73 m/s, was 73–88) | windup → in the window | windup (cut B) → in the window |
| Leaving the window | 0.07–0.4 m/s (dead stop) → **12–29 m/s** | 5–47 → 5–47 | 3–5 → 3–5 | 4 / 55 → 17 / 26 (cut A/B, then the chop meets the floor) |
| Frames below 1 m/s at the next held swing | 4 → 0 | 1 → 0 | 7 → 2–3 | 7 → 2 |

- With hits landing mid-window (the presentation hit-stop holds 0.03–0.075 s), the sword still averages 43–45 m/s in
  the window. The greatsword's worst frame fell from 118 m/s, a ×2.6 pop at recovery start, to 94 m/s with no pop
  (×1.3).
- The worst one-frame spike over all chains is ×1.8 (sword cut B's windup). A 40° camera turn before a held swing used
  to snap the facing in one frame (a 66 m/s tip pop); the facing now steps at most 17° per frame.
- While the blade crosses the arc, the crescent head stays within 5° of it. Before, it ran the opposite way at a uniform
  rate.

## Tests and parity

- `Tests/MeleeMotionChecks.cs` still pins `MeleeMotion` to the original game's frames (`Tests/melee-motion-parity.json`,
  `tools/export_melee_motion.mjs`) within 5 mm for every scenario the owner did not ask to change. Owner-requested
  deviations are listed in its `Deviations` table and compared with the Unity-owned `Tests/melee-motion-pinned.json`
  (regenerate with `CHIP_PIN_MELEE=1` after an intended change). A listed scenario must still deviate, and a torso-only
  deviation keeps its lower body (body, pelvis, legs, feet) on the original within 5 mm.
- The swing changes move every attacking frame, so the combo, run-attack-block (torso and swing), Echo Strike/Hurricane,
  fast-rate and reach-variant scenarios are pinned too. Only the five hit-reaction scenarios stay on the original.
- `Tests/MeleeFeelChecks.cs` pins the new behaviour:
  - torso amplitude bounds for every melee weapon at a walk, run, sprint, strafe, backpedal and lock-on circle, and the
    lifted run hunch;
  - continuity through speed ramps and runs into and out of a swing;
  - the shared swing timing: continuous, monotone, the sweep across the window, leaving it at the window's rate;
  - held chains, whiffs and hits: tip speed inside the window (sword ≥ 40, greatsword ≥ 30 m/s), the peak inside it, no
    one-frame pop, no dead stop leaving it, no idle at the guard;
  - the 40° turn case, and the crescent head on the posed blade.
- `Tests/WeaponFxChecks.cs` checks the crescent against the visible blade instead of `NativeMeleeRules.Angle`.

## Verification limits

Not run: the Unity Editor, the Windows build, two-client networking, the killcam on a live match, the bot duel harness
and a live playtest. The closing-damage figures assume perfect aim against a non-dodging target (an upper bound).
