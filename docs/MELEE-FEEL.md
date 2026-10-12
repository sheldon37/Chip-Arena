# Melee feel: evaluation, plan and handoff (2026-10-12)

Simulation follow-up: the Q/E kit and owner-requested S1–S5/S7/G8 work are implemented on `codex/unity-melee-abilities-20261012`; see [the simulation handoff](MELEE-SIMULATION-HANDOFF.md) for exact live values, owner-versus-proposal differences, replicated fields and the native duel command. The evaluation and proposal below describe the `19c00c7` baseline. Native duel and Windows/two-client acceptance are still pending.

Second simulation follow-up: `codex/unity-melee-followup-20261012` on `3bb8995f` implements S0's native sweep direction and the dagger jump handoff below. Daggers get one air jump per airtime at `0.9 × JumpSpeed × jumpPower`, available after walking off a ledge too; landing resets it. Host, lab and guest prediction share commitment rules, including action locks and separate lava strokes. `Actor.AirJumps/AirJumpAt` carry the authority state; guest reconciliation restores/replays the budget. Claude's existing flip, burst and whoosh now consume the explicit signal, with the local prediction clock mapped for presentation and one cue per airtime. Obstructed dagger bots may use it near the apex. No input-wire change or optional balance tuning; the native duel gate is still pending. See DEV_LOG/task PR for verification and merge receipts.

Owner request, 2026-10-12 (verbatim): "we may need to balance melee to stand on par with ranged/magic. They need more
ways to gap close, and also, we need to improve the "fluidity" of them, it doesnt feel smooth swinging right now now.
Think like dark souls swinging, its fluid and allows multiple swings. Maybe we need small movements forward when swinging
too? Please evaluate. Also, the movement for melee is a little weird on the torse, notice how the body rocks when moving."

- **Scope:** the Unity game (`unity/ChipArenaConversion`). Every `file:line` below is at commit `19c00c7` and relative to
  `unity/ChipArenaConversion/Assets/ChipArena/` unless stated.
- **Ownership** (AGENTS.md role split): each recommendation is tagged **PRESENTATION** (Claude: poses, blending, VFX
  timing) or **SIMULATION** (Astra: input, timing, turning, steps, abilities, bots, replication).
- **Status:** P1–P5, P7, P8 and the crescent sync are merged in the shared Unity head (`fdd1914`). The follow-up over
  Astra's native feel (P6 chain entry, the turn follower, anchored step/lunge footwork, sprint momentum; see
  [P6 and P9–P11](#p6-and-p9p11-presentation-over-the-native-feel-done)) is integrated in shared `0c026f0`; it is not a compiled release. S0 is implemented in the second simulation follow-up above. The simulation proposal below is kept as the evaluation record; the simulation
  status is in the follow-up above.
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
| 2 | S1 LMB input buffer | SIMULATION | S | No timing/hitbox/damage/cooldown change; one edge bit on the wire. Biggest "multiple swings" fix | Done (Astra, `fdd1914`) |
| 3 | P2–P8 swing retime, follow-through, real recovery length, turn blend, cut B pop, dagger windups, crescent sync | PRESENTATION | M | No sim impact | Done on `claude/melee-feel` |
| 3b | S0 un-mirror the hitbox sweep (`NativeMeleeRules.Angle` in the native frame) | SIMULATION | XS | Which side of the arc is hit first; arc, reach and timing unchanged. Needs the contact parity check converted to the native frame | Done (Astra, `f566b9a`); S0b corrected on codex/unity-night-handoff-20261012 |
| 4 | G1 melee Q/E kit (docs/MELEE-ABILITIES.md) | SIMULATION; animation, VFX, HUD PRESENTATION | L | New abilities, damage, cooldowns, replication | Simulation done (Astra); presentation is a separate task |
| 5 | S2 chain points | SIMULATION | M | Chained DPS +20–25 %; stun escape window ~0.40→0.26 s | Done (Astra) |
| 6 | S3 windup turning | SIMULATION | S–M | Hitbox yaw can change before the active window only | Done (Astra) |
| 7 | S4 step defaults + magnetism (G2 opener lunge) | SIMULATION | M | Movement during the step window (up to ~16 m/s) | Done (Astra); lunge footwork on `claude/melee-feel` |
| 8 | P6 chain entry from the follow-through (after #5) | PRESENTATION | S | No sim impact | Done on `claude/melee-feel` |
| 9 | G3 sprint attack | SIMULATION + PRESENTATION | M | New attack kind, replicated flag | Flag only (Astra); momentum lean on `claude/melee-feel` |
| 10 | G8 melee bot approach AI, then the duel harness | SIMULATION | M | Bots only; needed to measure balance | Proposed |
| 11 | S5 combo reset | SIMULATION | XS | Opener always cut A | Done (Astra) |
| 12 | S7 guest swing prediction | SIMULATION | M–L | Predicted lock and step, reconciled to the host | Done (Astra) |
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
- **P5 turn blend.** A swing that starts away from the shown facing eases in over 0.1 s (cubic ease-out); the strike
  window keeps the authority's yaw. (Over the native windup turning this became a follower; see P9.)
- **P7 daggers.** The cuts start after their authored wind-back (the entry blend from the guard stands in for it):
  windup compression ×4.7–5.7 → ×2.5–3.0.
- **P8 / hit-stop.** The greatsword cut B pop came from the hit-stop's 0.09 s catch-up racing the clip. The lost time is
  now caught up at most 1.3×, over the follow-through if needed.
- **Transitions.** Pose transitions into, out of and between the captured swings carry the last frame's joint velocity
  (as the reactions already did), so no swing starts or ends on a held frame.
- **Crescent sync.** The head follows the posed blade of the cut being played: it grows as the blade crosses the arc,
  in the blade's direction, and holds where the blade left it while fading. The shield's bash grows with the face's travel.
  This is opposite to today's mirrored hitbox order until S0.
- **P6 chain entry:** see below. A held chain still meets the next swing at the guard (at most 2–3 frames below
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

### P6 and P9–P11. Presentation over the native feel (done)

Astra's simulation (`docs/MELEE-SIMULATION-HANDOFF.md`, shared `fdd1914`) added the LMB buffer, early chains at
`ChainAt`, windup turning, 0.75–1.0 m default steps, 2.0–2.4 m lock-on openers (`OpenerLunge`), `SprintAttack` and the
combo reset. All of the following is in `Core/MeleeMotion.cs`, presentation only: no hit timing, hitbox, damage,
cooldown, movement, stamina or network change. The step stays the simulated body movement; nothing adds root motion.
`MeleeView` gains `OpenerLunge` and `SprintAttack`, set in `CapturedAnimation.MeleeViewOf`.

- **P6 chain entry.** `MeleeMotion` remembers the swing it drew before the current one. A swing that starts before that
  one's end (an early chain) enters from its live pose: the previous cut carries on at its own timing (clip time,
  follow-through and settle) under the entry blend (`ChainEntry`: half the windup, 0.1–0.85 of it), straight into the
  next windup. The guard is no longer the entry base. A held full-recovery repeat keeps the guard entry.
- **P9 turn follower.** A new commitment still eases from the shown facing over `TurnBlend` (0.1 s). After that, the
  facing follows `CommitYaw` as it comes, at most `TurnFollow` (1.6) × `NativeMeleeFeel.TurnRate`. Restarting the blend
  every tick trailed a 720°/s windup turn by up to 31–35° and popped at the hit (1500–1860°/s in a frame). A chain that
  starts from the previous heading carries straight on. A 20 Hz replicated heading (36° snapshot steps) glides at most
  19.2° per frame. An entry blend still running at the strike finishes over `StrikeBlend` (0.04 s).
- **P10 anchored footwork.** Each foot starts where it was shown as the swing began (the ankles are kept in world space
  each frame). That start can be a chain's recovery step or a run's stride, so no planted foot slides into the stance,
  and the pose transition into a swing holds the ankles.
  - **Lead foot.** It flies with the travel and comes straight down as it plants in the hit window (the horizontal stride
    ends at 90% of the lift).
  - **Rear foot.** It holds while the hips can reach it (`RearReach`, 0.3 m), then trails low on its toe behind them,
    then steps up once the travel ends.
  - **Turning.** While the body turns over planted feet (the windup turn), the feet keep their world place and pivot,
    up to 46°.
  - **Lunge.** A step past the weapon's default (`Lunge`: full at an `OpenerLunge` 0.9 m past it; a chained lock-on step
    past it counts at 0.6) lifts the lead foot up to 1.2× higher, trails the rear foot on its toe and leans the trunk into
    the travel (`LungeLean` 0.3 rad, mostly in the spine, most as the lead plants). Default steps keep the source's lifts.
  - **Hips and lead plant.** The hips rise back at most `SeatRise` (1.2 m/s). As the feet planted the drop could fall
    7 cm in a frame, which popped a low dagger blade. The greatsword and shield plant their lead 0.26/0.27 m ahead
    (0.31 for the others), where the leg reaches the floor.
- **P11 sprint momentum.** A `SprintAttack` swing pitches the trunk on over the planted feet (`MomentumLean`, 0.24 rad;
  the body 6 cm ahead) as the authority stops it dead. The lean peaks 0.06 s in and rights itself through the hit window.

Offline probe, 60 Hz, one pilot, flat floor, attackRate 1. Before is `fdd1914`; after is this branch. Planted-foot slip
is the largest speed and the total distance of an ankle on the floor; L / R are the left and right foot.

| Case | Before | After |
|---|---|---|
| Sword 2.2 m opener | 0.78 / 0.33 m/s, 2.1 / 1.6 cm; both feet 9 cm up mid-step | 0.04 / 0.04 m/s, 0.5 / 0.4 cm; rear foot trails at most 6 cm up |
| Greatsword 2.4 m opener | 0.65 / 0.72 m/s, 3.1 / 2.8 cm; both feet 14 cm up | 0.14 / 0.13 m/s, 0.6 / 1.1 cm; the lead plants on the floor |
| Daggers / shield 2.0 m opener | 0.40 / 0.13, 0.49 / 0.25 m/s | 0.42 / 0.08, 0.92 / 0.06 m/s (one touchdown frame) |
| Sword early chain (presses 0, 0.42, 0.84 s) | 0.47 / 0.75 m/s, 4.2 / 4.2 cm | 0.41 / 0.47 m/s, 1.3 / 1.2 cm |
| Greatsword early chain (0, 0.8, 1.6 s) | 1.46 / 1.26 m/s, 11.4 / 15.7 cm | 0.03 / 0.12 m/s, 0.3 / 1.3 cm |
| Sword / greatsword sprint entry | 2.5 / 6.7 and 4.1 / 6.0 m/s, up to 24 / 33 cm | 0.05 / 0.05 and 0.41 / 0.12 m/s, at most 1.4 cm |
| Sword windup turn toward a 60° aim, then a chain | facing lags up to 35°, 1500 °/s in a frame | lags 0.0°, at most 720 °/s (the authority's) |
| Greatsword chain entry, blade tip m/s per frame | 9.6, 1.8, 32, 69 (stops, snaps toward the guard) | 9.6, 8.4, 7.3, 7.3, 9.9, 21, 36 (carries into the windup) |
| Sword chain entry, blade tip m/s per frame | 17, 10, 72 (×7 in a frame) | 17, 15, 34, 81 |

- The combo index is presented correctly across buffered presses and resets: cut A, then the buffered cut B at
  `ChainAt`, then cut A again after a press past `ComboUntil`. The chain entry is decided by the presented swings' times,
  not by the index, so a reset never enters from the previous cut.
- **Remaining.** The daggers' chained windups (90 ms) still reposition both blades against the floor guard (one frame
  ×2.9 at 22 m/s; before, ×3.0 at 66 m/s). The shield's 2.0 m opener slips its lead foot 1.4 cm on one touchdown frame.
  Planted feet pivot with the body past 46° of windup turn.
- **S0 (Astra) is fixed in `f566b9a`:** `NativeMeleeRules.Angle` now sweeps `s.Yaw-(clamp(u)-.5f)·Arc·(Reverse(s)?-1:1)`
  in the native frame. Re-checked against the posed blades (`MeleeFeelChecks.Sweeps`): the hitbox now crosses the front
  the way the captured blade does on 8 of the 11 cuts, and the crescent (which follows the blade) agrees with it there.
- **S0b (Astra), still open:** three cuts still sweep against their blade, because the per-cut `Reverse(s)` table does not
  match those takes: daggers cut C (index 3, the downward stab crossing to the right) and both shield cuts (the face
  crosses to the right; the hitbox always sweeps left). The check reports them without failing. The hitbox is not touched
  here.

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
  - the 40° turn case, and the crescent head on the posed blade;
  - the native feel (`Feel`), driven through the host's input rules (buffer, `ChainAt`, combo reset, windup turn, lock
    step, sprint stop):
    - early chains per weapon enter from the live follow-through: the blade keeps at least 30% of its speed and 70% of
      the unchained follow-through's distance from the guard;
    - planted feet slip at most 1 m/s and 3–6 cm through openers at the cap, default steps, early chains and sprint
      entries;
    - the lower foot stays within 7 cm of the floor, and the lead foot plants inside the hit window;
    - lunges lean 6° more than a default step, and sprint entries 4° more than a standing swing;
    - the facing follows a 720°/s windup turn within 1° and at most `TurnFollow` × the rate, and a 20 Hz heading glides;
    - cut A, a buffered B, then A again after `ComboUntil`.
- `Tests/WeaponFxChecks.cs` checks the crescent against the visible blade instead of `NativeMeleeRules.Angle`.

## Verification limits

Not run: the Unity Editor, the Windows build, two-client networking, the killcam on a live match, the bot duel harness
and a live playtest. The closing-damage figures assume perfect aim against a non-dodging target (an upper bound).

## Melee travel from the pack (owner 2026-10-12: "the arms kind of wiggle around when running. Make it look more natural when moving~ Refer to the movement in our animation pack")

- **Cause.** The voxel legs step at about 2.6 times the captured run's cadence. The captured carry's arm pump, phased to
  those steps, read as flapping hands: 13–33 cm of hand travel per stride at a run.
- **Clips.** `Core/NativeLocomotionCarry` lays the pack's directional loops over the captured melee route, from
  `tools/export_pack_locomotion.mjs`:
  - Walk-Slow;
  - Walk and the seven strafes;
  - Run in eight directions.

  The sets are sword and daggers from Armed (the daggers in a reverse grip), shield from Armed-Shield (mirrored, so its
  left and right clips swap), and greatsword from 2Hand-Sword.
- **Blending.** The two clips bracketing the travel direction are blended, each phased from its own left-foot strike to
  the gait's stride.
- **Carry.** The weapon hand keeps 15% (run) or 25% (walk) of the per-stride swing, and the free hand 35% or 45%. The
  torso keeps the steady-torso shares, less again off the forward line. A strafe's mean hip turn is folded into the spine,
  so the chest keeps facing the aim.
- **Legs** stay the gait's procedural planted feet: no skating.
- **Result.** At a run the hands travel 2–4 cm per stride, and every torso component stays within the bounds below. First
  person keeps 30% of the captured pump.
- **Where it applies:** arena rigs only. The source-parity checks keep the captured walk and run.
- **Animation impact:** none on hit timing, hitboxes, damage, cooldowns, movement or networking.

## Sprint (owner 2026-10-12: "Also, add a sprinting animation for all weapons. (Different on weapon of course)", "And a sprint effect to know when we're sprinting", "Make sprint effect visible in first person too")

- **When.** `NativeSprintRules.Shows`: the replicated `Actor.Sprinting` flag (host, guests, killcam and spectators all
  have it), grounded, above 4 m/s, and not busy. A swing or cast and its recovery, a block, aim, reload or shot, a roll,
  stun, juggle, knockdown, special or death drops it. The blend is a 0.15 s ramp, eased at both ends.
- **Per-weapon carry** (`Core/NativeSprintMotion`, over each route's run, the gait's planted feet unchanged):
  - sword: the blade swept low behind the hip, the free arm pumping (a ninja run);
  - daggers: both reverse-grip fists pumping hard and low;
  - shield: up in front of the chest, the shield shoulder leading;
  - greatsword: on the right shoulder in both hands, with a heavier bob;
  - pistol: port arms;
  - rifle: high port, both hands;
  - machine gun: a low ready, both hands;
  - magic and L33t H4x: both hands trailing back, the staff angled back.

  The DEV fighters and the Siren keep their own locomotion. Each route also gets a small extra forward lean (sword .07,
  daggers .1, greatsword .14, shield .03, pistol .04, rifle .03, machine gun .02, magic .04 rad).
- **Steady torso at 9.18 m/s** (before → after): head sway sword 2.3 → 2.5 cm, daggers 2.3 → 2.6, shield 2.1 → 2.0,
  greatsword 4.7 → 2.6, guns and magic 2.2 → 2.5–2.8. Chest roll stays at or under 5.5° and twist under 15°. The lean is
  what adds sway: a forward-leaned spine turns the chest's counter-twist into a little sideways head travel, so the guns'
  and magic's leans were halved to stay under 3 cm.
- **Third-person effect** (`Core/NativeSprintFx`, `Runtime/NativeSprintFxPresentation`): six accent streaks trail level
  off the shoulders, hips, head and back, and three dust puffs kick up behind each footfall (every 1.24 m). Both are
  camera-facing, never on the floor. They are skipped for the view pilot in first person.
- **First person:**
  - the view weapon takes a per-weapon sprint carry and a stronger stride bob (`NativeSprintRules.ViewCarry` in
    `NativeViewSway`): the sword drops and tips down, the greatsword lifts toward the shoulder, the pistol muzzle comes up,
    the rifle cants across, the machine gun dips;
  - the camera gets an eased +5° FOV kick, kept out of the view-model camera so the hands keep their framing, and a
    1.2 cm / 0.5° bob;
  - tinted speed lines stream out in the outer third of the screen, over a soft edge vignette (an `OnGUI` behind the
    HUD). The tint is the weapon's hue mixed with the pilot's accent.
- **Settings.** Camera shake off removes the FOV kick and the bob. Reduced effects halves them, thins the streaks, dust
  and lines, and dims the vignette. Low graphics halves the counts again.
- **Sound.** The footstep cadence quickens while sprinting (a step every 1.55 m instead of 1.9 m, slightly brighter). The
  bank has no wind or whoosh loop that fits, so a `move.sprint` loop would be a sound-bank addition.
- **HUD.** The existing SPRINTING tag (`NativeSprintTag`) is unchanged and reads the same flag. Weapons without a tint of
  their own take its colour.
- **Animation impact:** none on hit timing, hitboxes, damage, cooldowns, movement, stamina or networking. Sprint speed,
  input and prediction are untouched.

## Jump and double jump (owner 2026-10-12: "can we add jump animations? (Of course, changes based on weapon. Daggers can double-jump)")

### Presentation (Claude, `claude/jump-anim`)

- **Per-weapon jumps.** `Core/NativeJumpMotion` lays the owner's Explosive pack jump takes over each route's pose:
  - sword: Armed;
  - daggers: Armed, reverse grip;
  - shield: the Armed body, with the shield arm kept on its own guard;
  - greatsword: 2Hand-Sword;
  - rifle and machine gun: 2Hand-Shooting;
  - pistol and the DEV fighters: Unarmed;
  - the magic schools and L33t H4x: 2Hand-Staff;
  - the Siren keeps her own air tuck.

  The tracks come from `tools/export_pack_jumps.mjs` (`NativePackJumps.Generated.cs` plus provenance).
- **Phases** come from what every peer presents: the replicated contact flag, the presented root height and the
  presentation clock (`NativeJumpTrack`).
  - **Takeoff:** a push-off blended in over 0.06 s. There is no anticipation crouch, because a remote jump is only known
    once the pilot leaves the ground.
  - **Rise, apex, fall:** blended by the presented vertical speed, then the Fall loop.
  - **Landing:** the Land take's compression, scaled by the fall speed.
  - **Guest timing:** a guest's flags are about 0.1 s ahead of its interpolated pilots, so a landing waits for the
    presented root to stop.
- **The dagger double jump** (`NativeJumpRules.Flips`) is detected as an upward step in the presented vertical speed while
  airborne. It plays:
  - the Jump-Flip tuck with a procedural front flip (0.45 s);
  - an accent ring and streaks under the feet, plus an afterimage puff (`NativeJumpFx`): upright and round, never on the
    floor;
  - the dagger swing played faster as a whoosh.

  Since Astra's air jump (`f566b9a`, merged into the shared head), the replicated `Actor.AirJumpAt` drives the flip, burst
  and whoosh explicitly (`NativeJumpTrack.AirJumpSignal`), so a vent launch no longer shows a flip; the vertical-speed
  inference remains only for rigs without the signal (the source-parity checks).
- **Animation impact:** none on hit timing, hitboxes, damage, cooldowns, movement or networking.

### Simulation handoff (Astra)

Implemented in the second simulation follow-up above, including the optional explicit signal. The proposed rule and integration points below are retained as the handoff record.

**The rule (daggers only)**
- One air jump per airtime, available once the pilot is airborne. It resets on landing (`Grounded`).
- Impulse: proposed `0.9 × JumpSpeed × jumpPower` (7.65 m/s at base). Set `velocity.y` to the impulse rather than adding
  to it, as the grounded jump does.
- It is blocked by the same locks as the grounded jump (rolling, attack lock, incapacitated). Lava swimming keeps its own
  stroke.

**Code changes**
- **Authority:** `NativeArena` input (`Runtime/NativeArena.cs`, the `jumped` line, about line 162) and the lab
  `Core/ArenaWorld.cs` (about line 218) need matching changes.
- **Prediction:** `Core/NativePrediction.cs` (about line 42) must predict the air jump identically. Add an air-jump count
  to `NativePredictionState`, reset when grounded, so guests do not snap.
- **Input wire:** unchanged. `Jump` is already an edge flag; the air jump is the next `Jump` edge while airborne.
- **Bots** may use it, for example to clear cover (`NativeBots` already presses Jump when blocked).

**Optional explicit signal**
- Replicate an `AirJumpAt` time on `Actor`.
- The presentation then stops inferring: pass the time to `NativeJumpTrack.AirJumpSignal(at)` after `Observe`, in the
  three places that observe (`NativeJumpMotion.Layer`, `NativeJumpFxPresentation`, `NativeMoveFeelAudio`). From the
  first call, the vertical-speed inference is off.
- Without the signal, a vent launch while airborne also shows the flip.

## Jump attacks (owner 2026-10-12: "Give jump attacks to melee classes. (Think of Elden ring style jump attack animations) or if theres anything in the pack.")

### Presentation (Claude, `claude/melee-feel`)

Implemented on the branch only, not merged or released. `NativeMelee.TryMelee` has no grounded check, so a press in the
air starts a normal combo swing with the ground timing. This presentation plays that swing as a jump attack, keyed to its
real hit window. It changes no hit timing, hitbox, damage, cooldown, movement, stamina or network data.

- **Pack takes.** `tools/export_pack_air_attacks.mjs` retargets the pack's air attacks with each weapon's melee-converter
  options into `Core/NativePackAirAttacks.Generated.cs`; `tools/pack-air-attacks-provenance.json` records the SHA-256 per
  track, which `Tests/AirAttackChecks` recomputes. The exporter's output is byte-identical across runs.
  - Sword, daggers and shield use `Armed-Air-Attack-R1`, which raises the weapon overhead and chops down in front. The
    daggers' reverse grip turns it into a downward stab, and the shield (strapped on that fist) slams its face down.
  - The greatsword uses `2Hand-Sword-Air-Attack1`: it winds back at the side and brings the two-handed blade over the
    top to the front.
- **Detection** (`Core/NativeAirAttack.cs`, `NativeAirAttackWatch`). A swing whose start falls while the pilot is
  presented airborne is an air attack. Airborne comes from the jump layer's `NativeJumpTrack`, so host, guests, killcam
  and spectators agree with what they draw.
  - **Hook:** `NativeAirAttackRules.Air(explicitAir, airborne)`, fed by `MeleeView.AirAttack` (-1 until a replicated
    flag exists, then 1 or 0). The watch in the first-person view and in the impact effects and sound takes the same flag.
  - A swing begun while a Q/E special, stun, roll, juggle or knockdown owns the pilot is never a jump attack. An airborne
    Wolf Knight or Skyfall stays a special.
- **Elden Ring feel** (`MeleeMotion.AirCut`; `NativeAirAttackRules.ClipTime` and `MeleeSwing`'s air shapes).
  - **Raise:** the weapon goes up over the first 45% of the windup (at least 75 ms), eased out. The rest of the windup is
    the take's held overhead pose: the hang.
  - **Strike:** the hit window plays the chop and the take's held impact. The blade reaches the bottom two thirds of the
    way through the window at about 0.6× its authored speed. Fastest frame: sword 57 m/s, greatsword 114 m/s, shield
    34 m/s, all inside the window.
  - **After the window:** the follow-through, then the settle into the guard over the real recovery. In the air the jump
    layer keeps the legs.
  - **Heavy landing.** A landing during the attack (or within 0.05 s after it):
    - holds the pose up to 0.16 s;
    - brings the take's hips down to the stance's and sinks a further 0.12 m under a folded trunk;
    - scales the jump layer's landing drop by up to 2.1×. That is the one hook in `NativeJumpMotion`, `LandScale`, set
      before each `Melee` call.
    The pose transition out of a landed jump attack holds the ankles. A heavy landing sinks at least 9 cm deeper than a
    plain one, with the feet on the floor (within 0.7 cm).
  - **Ending in the air:** an attack that ends before landing leaves no impact and settles back to the guard that the air
    pose rides on.
- **Impact** (`Runtime/NativeAirAttackFxPresentation.cs`, `Core/NativeAirAttackFx`). Each peer watches the pilots with the
  same rules. A jump attack's landing leaves:
  - a dust ring rolling out over the floor;
  - cracks running out from the weapon's point of impact, plus a crack mark there;
  - a weapon-accent shock ring.
  All three scale with the strength (0.4–1, from the fall speed) and thin with Reduced effects and Low graphics.
  - **Shake:** `NativeStructureFx.Quake` adds a 0.35 s shake of 0.025–0.065 m under the structure shake's rules (distance
    falloff, the camera-shake setting, reduced effects, the 0.3 m cap). It reaches the attacker and nearby players.
  - **Sound:** `NativeAudio.AirAttackCues` plays the existing `impact.ground` under a lower `move.land`, louder and lower
    for a harder impact. No new bank key.
- **First person** (`CapturedAnimation.AirView`). The view rig keeps its own jump track and watch, and poses the air take
  on a scratch rig. The chest bows forward up to 1.05 rad (turning toward the centre) through the raise and the hang, so
  the raised blade hangs about 1.4 m in front of the camera, then chops down through the view (1.6 m) as the bow eases
  over the window and the recovery.
- **Tests** (`Tests/AirAttackChecks`): provenance; continuous, monotone clip time; standing, running and dagger
  double-jump attacks with the blade coming down from over the head; one impact for a landing during an attack, none for
  one that ends in the air or for an airborne special; ground swings keep their cuts; the explicit flag wins; the
  first-person chop; the effect stays on the floor.
- **Remaining:**
  - The daggers' 90 ms windup raises both blades faster than they chop (36–40 m/s, as their ground windups).
  - The slash crescent still follows the ground cut's sweep during a jump attack. With an explicit `Swing.Air` it can
    take `MeleeSwing.VisibleSweep` of the air take.
  - The hitbox still sweeps its horizontal arc while the blade chops vertically; the timing agrees, the shape does not.
  - Not run in Unity: the Unity effects, the shake and the first-person view model were compiled against the Unity
    player API but not seen in a play session.

### Simulation handoff (Astra)

These are proposals, not implemented. A real plunging attack is simulation:
- `Swing.Air`, replicated as `Actor.AirAttack` (or similar), with a plunge start and an impact or land time;
- a landing-timed hit around the impact point, its bonus damage and its recovery;
- an impact event.

The presentation then needs only one line, `MeleeViewOf` setting `v.AirAttack`, plus the same flag in `AirView`,
`NativeAirAttackFxPresentation` and `AirAttackCues` (each passes -1 today). The air take's strike would then key to the
plunge's own window.


### Astra night implementation, 2026-10-12

`codex/unity-night-handoff-20261012` integrates sprint PR #133, implements the owner's landing-timed jump attack (explicit Air/PlungeAt/ImpactAt state, guest prediction and Land event), and corrects S0b with an enforced eleven-cut captured-blade check. See MELEE-SIMULATION-HANDOFF's night section for the precise simulation and replication contract. The earlier proposal/presentation-only status above is historical; Windows animation retiming and real native duel measurements are still pending. No animation clips are changed by this simulation patch.
