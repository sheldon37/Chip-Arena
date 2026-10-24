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
- **P10 anchored footwork** (superseded by "Footwork" below, which pins the soles and takes real steps). Each foot starts where it was shown as the swing began (the ankles are kept in world space
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
    - the footwork, measured at the soles (see "Footwork" below): through openers at the cap, default steps, early chains,
      sprint entries and a 60° windup turn, a sole on the floor turns about a pivot but slides at most 0.35 m/s in a frame
      and 1.5 cm in all, never sinks into the floor, and every step clears it by at least 5 cm; the lead foot plants inside
      the hit window, the rear foot lands a recovery step after it, the hips drop at least 2 cm into the plant (the
      greatsword deeper than the sword); a swing with no step or into a wall keeps both soles planted; a planted foot only
      turns with its heel up;
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
  - daggers: both reverse-grip fists held low and close beside the hips, elbows tucked back, with a light 12 cm pump
    (`DaggerX`..`DaggerLift`; 2026-10-14, owner: "keep the arms more still (it looks like theyre trying to move the
    forearms too much with sprinting)": the old 60 cm pump flexed each elbow about 40 degrees a stride), each blade at a
    steady lie tipped back along the forearm (`DaggerRight`, `DaggerLeft`; 2026-10-14: the live run-carry lie wobbled each
    stride and dipped the tips under the floor guard, which flicked the wrists). In the run and walk the left dagger hand
    keeps the weapon hand's share of the pack swing (`NativeLocoRules.LeftKeep`), not the empty free hand's. In the run
    the right (rear) blade's lie is tipped back (`NativeLocoRules.RearBladeTip`), so the blade clearance's floor term and
    the final floor guard no longer both lift it each stride, and the shoulders keep the arms' share of the clip's shrug;
  - shield: up in front of the chest, the shield shoulder leading;
  - greatsword (2026-10-15, owner: "Utilize the normal run animation with sword on ground, but lean forward abit to show
    a natural sprint movement"): the pack run's own carry (legs, stride, cadence, torso, the low right fist and the free
    left guard fist) with a .2 rad lean. The blade keeps the run's heading and roll. Only the wrist's pitch changes, so the
    stretched point rides 2 cm over the floor guard's plane (`DragBlade`, `DragTip`) at every stride phase, frame rate,
    turn and weave; the guard never takes the wrist. The free arm swings the run's guard whole from the shoulder, forward
    of the guard by up to .3 rad as the right foot leads (`GuardSwing`). The old shoulder carry is gone, with its support
    hand and extra bob;
  - pistol: port arms;
  - rifle: high port, both hands;
  - machine gun: a low ready, both hands;
  - magic and L33t H4x: both hands trailing back, the staff angled back; the casting hand holds the staff run's
    cycle-mean frame at the sprint weight (2026-10-14: the clip's free-hand frame turned about 115° a stride, the
    owner's "hand keeps rotating"; `NativeRangedLocomotion.Magic`).

  The DEV fighters and the Siren keep their own locomotion. Each route also gets a small extra forward lean (sword .07,
  daggers .1, greatsword .2, shield .03, pistol .04, rifle .03, machine gun .02, magic .04 rad).
- **Steady torso at 9.18 m/s** (before → after): head sway sword 2.3 → 2.5 cm, daggers 2.3 → 2.6, shield 2.1 → 2.0,
  greatsword 4.7 → 1.8 (the drag; 2.6 on the old shoulder carry), guns and magic 2.2 → 2.5–2.8. Chest roll stays at or
  under 5.5° and twist under 15°. The lean is what adds sway: a forward-leaned spine turns the chest's counter-twist into
  a little sideways head travel, so the guns' and magic's leans were halved to stay under 3 cm.
- **Third-person effect** (`Core/NativeSprintFx`, `Runtime/NativeSprintFxPresentation`): six accent streaks trail level
  off the shoulders, hips, head and back, and three dust puffs kick up behind each footfall (every 1.24 m). Both are
  camera-facing, never on the floor. They are skipped for the view pilot in first person. A greatsword dragging its point
  throws five faint hot scrape sparks back and up off the floor there (`NativeSprintFx.Scrape`, two with Reduced effects,
  halved on Low graphics). They read the arena rig's drag carry and blade tip, and fade as the point lifts.
- **First person:**
  - the view weapon takes a per-weapon sprint carry and a stronger stride bob (`NativeSprintRules.ViewCarry` in
    `NativeViewSway`): the sword drops and tips down, the greatsword drops 6.5 cm and tips 18° down (its dragged point),
    the pistol muzzle comes up, the rifle cants across, the machine gun dips;
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
- **Every weapon's air jumps** (2026-10-18, Astra's movement chips and Airborne modifier, PR #282: up to 6 charges for
  any weapon). Per spent charge, from the same presented signal (`Core/NativeAirJumpFx`):
  - the daggers and the Bow keep the flip, burst and whoosh exactly; the Cyber Lord keeps its own boost;
  - every other weapon plays the air step (`NativeJumpMotion`, 0.34 s): a lead knee driven up to the set's tuck, the
    trailing leg kicked down and back, a small forward crunch, the lead leg alternating with the charge; joint rotations
    only, the arms and weapon as the route holds them;
  - the same burst, about 6% smaller, brighter and whiter per chained charge, and `move.jump` a semitone higher per charge.
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

Implemented in PR #136 (`eff9743`) with Claude's follow-up on `claude/melee-sim` (.6 carry, dagger-only combo, presentation reads); see MELEE-SIMULATION-HANDOFF. The proposal below is the handoff record. A real plunging attack is simulation:
- `Swing.Air`, replicated as `Actor.AirAttack` (or similar), with a plunge start and an impact or land time;
- a landing-timed hit around the impact point, its bonus damage and its recovery;
- an impact event.

The presentation then needs only one line, `MeleeViewOf` setting `v.AirAttack`, plus the same flag in `AirView`,
`NativeAirAttackFxPresentation` and `AirAttackCues` (each passes -1 today). The air take's strike would then key to the
plunge's own window.

## Footwork (owner 2026-10-12: "Also, at more realism to the basic attack animation. Currently~ They slide across with their feet on the ground, make this appear a lot better.")

### What was wrong

P10 held the ankles in place, and the old probe measured the ankle (planted while within 1.2 cm of its floor height):
0.04–0.41 m/s. Measured at the soles, the lowest corner of the foot and toe boxes, the picture was different. The rear
foot "trailed low on its toe" with its ankle 3–5 cm up and its heel raised, so its toe was 1.8–4.6 cm under the floor and
was dragged along at the hips' speed: 4.7–17.6 m/s on every swing that stepped. The lead foot's lifts cleared the floor by
2.9 cm, and planted feet turned flat with the body.

### Approach

New `Core/MeleeFootwork.cs` holds the plan; `MeleeMotion.Feet` calls it for every captured cut but the daggers' hopped
spin. Presentation only: the body still travels by the authority's step (`StepAt/StepUntil/StepDistance`), never by root
motion. No change to hit timing, hitboxes, damage, cooldowns, movement, stamina or networking.

- **Pinned soles.** A foot touching the floor is pinned in world space. It is anchored either on its toe joint, with the
  heel raised to push off or pivot and the toes bent back flat, or on its heel's back edge while a heel strike rolls flat.
  The rig reads the world position from `GroupPosition`/`GroupYaw`, as the runtime places the root each frame, so a pinned
  foot holds however the presented body moves.
- **Real steps.** A foot leaves the floor straight up, travels on an arc, and comes down onto its spot. Its sole is kept
  at least `Clearance` (5.5 cm) clear through the travel. It lands heel first, the lead with the toe up 16°. The landing
  target is set from where the body will be at that moment: the simulated travel still to come, scaled by the travel
  actually observed. So a step into a wall lands where the body stopped, and a swing that does not move lifts no foot.
- **The plan, timed to the step window.**
  - **Lead foot.** It lifts once the body starts to move. It plants as early in the hit window as the leg reaches: +47 to
    +110 ms after the window opens, 29–69% of the 0.16 s window, and never before it.
  - **Rear foot.** It pushes off on its toes until its hip is too far away (`Hold`, at most what the leg reaches with the
    hips seated). It then lifts and lands its recovery step behind the lead in the recovery, re-squaring the cut's stance.
  - **Long steps.** A long step (from `LongStep`, 1.15 m: lock-on openers and chained lunges) strides with the rear foot
    first and then plants the lead, giving two short flights instead of one long skate. So does any step whose single
    bound would leave both feet off longer than `MaxFlight`, which is every greatsword step.
- **Push-off.** A planted foot that is about to be outrun lifts one frame ahead (at a lunge's peak the hips cover 10 cm a
  frame).
- **Weight.**
  - **Hips.** They drop into the plant and rise over the recovery (`Drop`, `DropHold`, `DropRise`), seated lower still
    when a planted foot needs the reach (`SeatSteps`, rising at most 1.2 m/s as before).
  - **Trunk lean.** Every step leans the trunk 4–6° into its cut as the lead plants (`StepLeanBase`), on top of the
    lunge's and the sprint entry's leans.
  - **Hips lead.** The pelvis turns 8° (the greatsword 10°) toward the blade's sweep ahead of the chest (`HipsLead`), and
    the capture keeps the shoulders.
  - **Greatsword.** It plants deepest and longest and takes the slowest recovery step.
  - **Shield.** Its drop also offsets the capture rising through the bash.
- **Pivots.** When the body turns well past a planted foot (a windup turn), its heel lifts and the foot turns on its toe
  joint after the body, then settles flat. The rear foot's heel also comes up through the cut's drive.
- **Transitions.**
  - **Start.** A swing starts from the feet as shown. A foot already in the air (a run's stride, a chain's recovery
    step) carries its speed into its first step, or comes down first on a short catch step.
  - **Pose blends.** The blend into and out of a swing now keeps each sole's orientation, not only its ankle
    (`MeleeRig.HoldAnkles(..., feet:true)`); it had tilted pinned soles 3–4 cm into the floor.
  - **Gait handoff.** The gait takes the swing's feet over exactly (`MeleeGait.SyncTolerance` 0 after a footwork frame,
    instead of within 1.5 cm). This removed a 0.7 m/s one-frame slip as every swing ended.
  - **Hit-stop.** It freezes the cut, not the feet, because the body keeps moving.
- **Daggers' spin hop.** It keeps its own hop, now 12 cm high. Its feet turn only while they are off the floor: before,
  they swept the floor at 9 m/s as it landed.
- **Leg pose.** `MeleeRig.StepFoot` turns the foot to its heading and then pitches it about its own axis. The source's
  `foot()` pitched about the group axis, which rolls a turned foot sideways. It also blends the foot's orientation in
  group space, because blending the local rotation across a re-solved knee tilted the sole.

The pack's own attack legs were considered and not used. The cut clips' legs move 0.1–0.4 m in place, with their root
travel stripped at import. Their windups are retimed 2.5–5× by `MeleeSwing`. No clip covers a 0.75–2.4 m step in 0.17–0.32 s,
a chain, a run entry or a wall. Stride-warping them 3–8× with foot-contact IK would not keep their contacts. Procedural
steps timed to the authority's step window do keep them, for every case.

### Before and after (offline probe, 60 Hz, flat floor, attackRate 1)

Before is shared `c0e5aa1` and after is this branch. "Sole slip" is the slowest-moving of a sole's three pivots (toe
joint, heel back edge, sole centre) on frames where the sole touches the floor in both frames. A foot turning on its toe
joint therefore counts as 0, and a dragged toe does not. "Sink" is how far a sole goes into the floor. "Step clearance"
is the lowest peak clearance of any step that travels more than 10 cm. "Lead plant" is the lead foot's touchdown after
the hit window opens. "Hip drop" is the pelvis's lowest point below its height as the swing began; it includes the
captured cut's own dip, and an entry from a sprint, walk or run starts from the run's height. The run case measures
the swing window plus 0.1 s of the run-out.

**Sword**

| Scenario | Sole slip, max (m/s) | Sole slip, mean (m/s) | Sink (cm) | Step clearance (cm) | Lead plant | Hip drop (cm) |
|---|---|---|---|---|---|---|
| default step | 7.05 → 0.00 | 0.768 → 0.000 | 3.5 → 0.0 | 2.9 → 11.6 | +77 ms (48%) | 12.1 → 6.1 |
| early chain | 7.05 → 0.00 | 0.976 → 0.000 | 5.2 → 0.0 | 2.8 → 9.5 | +77 ms (48%) | 12.1 → 8.3 |
| held repeat | 7.05 → 0.00 | 0.836 → 0.000 | 4.0 → 0.0 | 2.8 → 9.5 | +77 ms (48%) | 12.1 → 8.3 |
| lock-on opener (2.2 m) | 17.22 → 0.27 | 1.876 → 0.004 | 4.7 → 0.0 | 2.9 → 10.6 | +93 ms (58%) | 12.8 → 4.8 |
| opener into chain | 17.22 → 0.27 | 1.230 → 0.002 | 4.7 → 0.0 | 2.8 → 10.6 | +93 ms (58%) | 12.8 → 6.8 |
| sprint entry | 7.05 → 0.19 | 0.945 → 0.004 | 6.0 → 0.0 | 2.9 → 8.0 | +77 ms (48%) | 21.8 → 18.5 |
| walk entry | 7.05 → 0.05 | 0.915 → 0.002 | 4.3 → 0.0 | 2.9 → 10.4 | +143 ms (90%) | 26.6 → 19.3 |
| run in / run out | 7.05 → 0.61 | 1.109 → 0.029 | 4.0 → 0.4 | 2.9 → 10.4 | +143 ms (90%) | 25.9 → 18.7 |
| no step | 0.39 → 0.00 | 0.028 → 0.000 | 3.5 → 0.0 | 2.9 → 0.0 | — | 5.0 → 5.1 |
| into a wall | 5.47 → 0.01 | 0.472 → 0.000 | 3.5 → 0.0 | 2.9 → 0.0 | — | 12.1 → 4.7 |
| 60° windup turn | 7.05 → 0.04 | 0.991 → 0.000 | 3.6 → 0.0 | 2.8 → 10.8 | +77 ms (48%) | 12.0 → 8.3 |
| chain with hits (hit-stop) | 7.68 → 0.00 | 0.947 → 0.000 | 5.2 → 0.0 | 2.8 → 9.5 | +77 ms (48%) | 12.1 → 8.3 |

**Greatsword**

| Scenario | Sole slip, max (m/s) | Sole slip, mean (m/s) | Sink (cm) | Step clearance (cm) | Lead plant | Hip drop (cm) |
|---|---|---|---|---|---|---|
| default step | 4.73 → 0.03 | 0.509 → 0.000 | 2.7 → 0.0 | 2.9 → 9.7 | +63 ms (40%) | 16.6 → 17.8 |
| early chain | 6.47 → 0.05 | 0.645 → 0.001 | 10.7 → 0.0 | 2.8 → 9.5 | +63 ms (40%) | 16.7 → 17.8 |
| held repeat | 6.48 → 0.05 | 0.602 → 0.001 | 3.1 → 0.0 | 2.8 → 9.5 | +63 ms (40%) | 16.6 → 17.8 |
| lock-on opener (2.4 m) | 11.34 → 0.26 | 1.232 → 0.002 | 4.6 → 0.0 | 2.9 → 10.5 | +80 ms (50%) | 13.4 → 21.0 |
| opener into chain | 11.34 → 0.26 | 1.232 → 0.002 | 4.6 → 0.0 | 2.9 → 10.5 | +80 ms (50%) | 13.4 → 21.0 |
| sprint entry | 4.73 → 0.05 | 0.585 → 0.001 | 2.7 → 0.0 | 2.9 → 9.6 | +47 ms (29%) | 27.7 → 30.6 |
| walk entry | 4.73 → 0.11 | 0.576 → 0.001 | 3.0 → 0.0 | 2.9 → 9.4 | +63 ms (40%) | 27.8 → 30.6 |
| run in / run out | 4.73 → 0.61 | 0.663 → 0.015 | 4.9 → 0.4 | 2.9 → 9.5 | +47 ms (29%) | 27.2 → 30.8 |
| no step | 0.16 → 0.00 | 0.023 → 0.000 | 2.2 → 0.0 | 2.9 → 0.0 | — | 12.3 → 15.0 |
| into a wall | 3.49 → 0.00 | 0.228 → 0.000 | 2.7 → 0.0 | 2.9 → 0.0 | — | 16.6 → 17.8 |
| 60° windup turn | 4.73 → 0.03 | 0.478 → 0.001 | 2.9 → 0.0 | 2.9 → 9.7 | +47 ms (29%) | 16.7 → 17.8 |
| chain with hits (hit-stop) | 6.97 → 0.05 | 0.624 → 0.001 | 10.1 → 0.0 | 2.8 → 9.5 | +63 ms (40%) | 16.7 → 17.6 |

**Daggers**

| Scenario | Sole slip, max (m/s) | Sole slip, mean (m/s) | Sink (cm) | Step clearance (cm) | Lead plant | Hip drop (cm) |
|---|---|---|---|---|---|---|
| default step | 7.04 → 0.00 | 0.970 → 0.000 | 2.9 → 0.0 | 2.9 → 11.7 | +93 ms (58%) | 13.2 → 13.2 |
| early chain | 10.67 → 0.08 | 1.081 → 0.003 | 4.8 → 0.0 | 3.8 → 7.0 | +93 ms (58%) | 13.2 → 13.2 |
| held repeat | 14.17 → 0.05 | 1.151 → 0.001 | 2.9 → 0.0 | 2.8 → 7.0 | +93 ms (58%) | 13.2 → 13.2 |
| lock-on opener (2.0 m) | 17.59 → 0.27 | 2.454 → 0.007 | 4.6 → 0.0 | 2.9 → 9.0 | +110 ms (69%) | 13.2 → 13.2 |
| opener into chain | 17.59 → 0.27 | 1.292 → 0.003 | 4.6 → 0.0 | 2.8 → 9.0 | +110 ms (69%) | 13.2 → 13.2 |
| sprint entry | 7.04 → 0.03 | 1.224 → 0.001 | 5.8 → 0.0 | 2.9 → 7.4 | +93 ms (58%) | 26.9 → 26.9 |
| walk entry | 7.04 → 0.00 | 1.171 → 0.000 | 4.7 → 0.0 | 2.9 → 5.1 | — | 25.1 → 25.1 |
| run in / run out | 7.04 → 0.21 | 1.394 → 0.011 | 6.3 → 0.3 | 2.9 → 8.5 | — | 27.2 → 27.2 |
| no step | 0.18 → 0.00 | 0.031 → 0.000 | 2.9 → 0.0 | 2.9 → 0.0 | — | 13.2 → 14.2 |
| into a wall | 5.25 → 0.00 | 0.578 → 0.000 | 2.9 → 0.0 | 2.9 → 0.0 | — | 13.2 → 13.2 |
| 60° windup turn | 8.14 → 0.08 | 1.200 → 0.001 | 3.2 → 0.0 | 2.8 → 8.1 | +93 ms (58%) | 13.2 → 13.2 |
| chain with hits (hit-stop) | 10.66 → 0.08 | 1.086 → 0.003 | 4.8 → 0.0 | 3.8 → 7.0 | +93 ms (58%) | 13.2 → 13.2 |

**Shield**

| Scenario | Sole slip, max (m/s) | Sole slip, mean (m/s) | Sink (cm) | Step clearance (cm) | Lead plant | Hip drop (cm) |
|---|---|---|---|---|---|---|
| default step | 5.21 → 0.00 | 0.584 → 0.000 | 2.9 → 0.0 | 2.9 → 11.6 | +67 ms (42%) | 7.6 → 2.4 |
| early chain | 6.25 → 0.00 | 0.743 → 0.000 | 4.2 → 0.0 | 2.8 → 7.1 | +67 ms (42%) | 16.4 → 16.4 |
| held repeat | 6.25 → 0.00 | 0.636 → 0.000 | 2.9 → 0.0 | 2.8 → 7.1 | +67 ms (42%) | 16.4 → 16.4 |
| lock-on opener (2.0 m) | 13.69 → 0.27 | 1.517 → 0.004 | 4.7 → 0.0 | 2.9 → 9.0 | +100 ms (63%) | 7.8 → 8.2 |
| opener into chain | 13.69 → 0.27 | 0.951 → 0.002 | 4.7 → 0.0 | 2.8 → 9.0 | +100 ms (63%) | 16.4 → 19.1 |
| sprint entry | 5.05 → 0.04 | 0.689 → 0.001 | 3.0 → 0.0 | 2.9 → 8.7 | +83 ms (52%) | 21.6 → 16.6 |
| walk entry | 5.23 → 0.12 | 0.696 → 0.003 | 3.4 → 0.0 | 2.9 → 9.8 | +67 ms (42%) | 22.4 → 14.3 |
| run in / run out | 5.23 → 0.61 | 0.841 → 0.026 | 3.5 → 0.4 | 2.9 → 8.3 | +67 ms (42%) | 22.0 → 13.4 |
| no step | 0.33 → 0.00 | 0.034 → 0.000 | 2.9 → 0.0 | 2.9 → 0.0 | — | 3.5 → 0.3 |
| into a wall | 4.52 → 0.00 | 0.388 → 0.000 | 2.9 → 0.0 | 2.9 → 0.0 | — | 7.6 → 0.3 |
| 60° windup turn | 6.25 → 0.56 | 0.660 → 0.004 | 3.7 → 0.0 | 2.8 → 7.6 | +67 ms (42%) | 16.4 → 16.4 |
| chain with hits (hit-stop) | 7.04 → 0.00 | 0.721 → 0.000 | 4.2 → 0.0 | 2.8 → 7.1 | +67 ms (42%) | 16.4 → 16.4 |

The 0.61 m/s in "run in / run out" is the gait's first run-out step, not the handoff. The gait rolls a planted foot about
its ankle and slips up to 5.1 m/s while running with no swing at all; it is pre-existing locomotion and left alone here. The
other residuals, 0.2–0.56 m/s, are single frames: a foot lifting while it pivots, and a 2.0–2.4 m opener's 2–3-frame
stride contact. The strict corner measure (the mean of the touching corners) also sees the heel box's front edge sweep
2 mm a frame under the pinned toe box as the heel lifts: at most 0.14–0.39 m/s.

Renders (scratch, not committed) are side views that follow the pilot, before over after, with world-fixed footprints:
blue left, orange right, red where no pivot of the sole held still. Airborne ankle trails are drawn as dots. Strips: the
sword default step, combo and 60° windup turn; the greatsword combo and 2.4 m lunge; the daggers chain; the shield
combo; and a run into a swing and back out to a run.

### Tests

`Tests/MeleeFeelChecks.cs` `Feel` now measures the soles, not the ankles (see "Tests and parity" above). On the old code
the new checks fail everywhere:
- soles slip 4.7–17.6 m/s and 22–242 cm in all;
- they sink 2.5–12.9 cm into the floor;
- steps clear the floor by 1.9–3.8 cm;
- no recovery step lands;
- swings with no step or into a wall step 15–28 cm;
- a turning planted foot turns flat for 16 frames.

`Tests/melee-motion-pinned.json` is re-pinned for its 24 swing scenarios. The five hit-reaction scenarios stay on the
original within 0.34 mm, and the torso-only scenarios keep the lower body within 0.08 mm.

### Limits

The probe and checks are offline (no Unity Editor, Windows or two-client play). A remote pilot's presented position may
lag or smooth its swing's timing. The pinned feet hold regardless, but the landing targets assume the authority's step
curve. Uneven floors are not handled: the floor is taken as flat at the body's height, as before. Owner taste is open on
the stride-first long steps, the follow step's timing and the deeper shield drop.

## Sprint attacks (owner 2026-10-12: "can we give melee's a "sprint" attack? Of course each melee gets their own.")

### Presentation (Claude, `claude/melee-steps`)

Implemented on the branch only, not merged or released. The simulation is Astra's (`Core/NativeSprintAttack`, PR #133,
merged into this branch at `44c79cf`); see "weapon sprint primaries" in MELEE-SIMULATION-HANDOFF. This presentation
reads only her replicated fields (`SprintAttack`, `VisualWindup/Active/Duration`, `SwingAt/Until`,
`StepAt/Until/Distance`, `ChainAt`, the hop through `Grounded` and the root height). It changes no hit timing, hitbox,
damage, cooldown, movement, stamina or network data.

- **Pack takes.** `tools/export_pack_sprint_attacks.mjs` retargets one running attack per weapon into
  `Core/NativePackSprintAttacks.Generated.cs`. `tools/pack-sprint-attacks-provenance.json` records each track's SHA-256,
  which `Tests/SprintAttackPoseChecks` recomputes.
  - Sword "Lunging Slash": `Armed-Run-Attack-R1`, a long, low horizontal slash.
  - Daggers "Twin Lunge": `Armed-Run-Attack-Dual`, both blades together.
  - Shield "Shoulder Charge": `Armed-Shield-Run-Forward-Charge`, mirrored onto the shield arm.
  - Greatsword "Leaping Cleave": `2Hand-Sword-Run-Forward-Attack1`, a wide two-handed sweep.
- **Timing** (`MeleeMotion.SprintCut`; `NativeSprintAttackRules.ClipTime`; the `sprint/*` shapes in `MeleeSwing`). Each
  take's lead-in plays over the real windup, its strike across the real hit window, then the follow-through and the
  settle into the guard over the recovery.
  - Measured strike: sword 62 m/s at 0.267 s (window 0.16–0.34); shield furthest out, 92 cm, at 0.233 s (0.12–0.28);
    greatsword 83 m/s at 0.50 s (0.48–0.64).
  - The daggers' twin follows Astra's two sweeps (`NativeSprintAttack.Pulse`, half a window each, the second reversed).
    The take's cross-cut plays over the first half, at its furthest at 0.167 s (0.12–0.24). Over the second half it whips
    back out, easing into the opened pose. The right blade crosses 99 cm and comes back 106 cm.
  - A chain after a sprint attack enters the next cut from the take's live pose. The daggers can chain 0.15 s after
    their window.
- **Shield bash.** The charge already carries the shield near full reach, so the bash comes from the body. The chest
  turns the shield forward and pitches over it, the body pushes on 0.18 m, and the shield goes out a further 0.10 m. The
  bash peaks 60% into the window and is drawn back over the first 30% of the recovery.
- **Lean.** The takes drive their own trunk into the lunge, so a sprint attack keeps only the footwork's plant lean (no
  opener lean, no momentum lean). Stacked, the lean pitched the shield's trunk to 90°.

  | Weapon | Before: cut A on Astra's timing | After: own take |
  | --- | ---: | ---: |
  | Sword | 42° | 39° |
  | Daggers | 65° | 49° |
  | Shield | 26° | 60° (a dropped-shoulder charge) |
  | Greatsword | 26° | 28° |

- **Footwork** (`Core/MeleeFootwork`, from the sprint run-up). The steps of 2.0–2.6 m start with the swing
  (`StepAt = At`) and are long, stride-first steps. The greatsword's sprint attack is a leap on Astra's hop:
  - **Off:** both feet leave with the body (the rear 20 ms later), and a foot still in the air from the run goes
    straight into its leap.
  - **In the air:** they ride tucked under the body: 26 cm (lead) and 20 cm (rear) at mid-flight.
  - **Down:** they land as the body lands. Each is held until the body is within 2 cm of the floor, the lead first and
    the rear 30 ms later.
  - `NativeJumpMotion.Leap`, a minimal hook set before each `Melee` call, makes the jump layer draw no air or landing
    pose while the leap is drawn. Its air legs and leg-return blend dragged the landing soles 6–19 m/s and sank them
    12 cm.
  - A sprint attack is never read as a jump attack, though the hop is airborne. The rig passes `explicitAir = 0` for one.
    The first-person view and the jump-attack impact and sound take Astra's replicated air flag (`Actor.AirAttack`), which
    a sprint attack never carries.
- **First person** (`CapturedAnimation.SprintView`). The same take, clip time and shield bash on a scratch rig. It is kept
  through the greatsword's hop, blended in over 0.04 s and settled into the guard after the window.
- **Effects** (`Runtime/NativeSprintAttackFxPresentation.cs`, `Core/NativeSprintAttackFx`, `NativeSprintAttackWatch`).
  Every peer runs the same watch.
  - An entry dust burst at the feet.
  - Weapon-accent speed streaks through the hit window.
  - The greatsword's landing ring once it is down inside its window (`NativeSprintAttackRules.RingLive`, as Astra's
    `Ring`): dust rolling out and a ring of accent light spreading to her 1.6 m round the feet. None if it is still
    falling through the window (off a ledge).
  - The daggers' crescents draw the twin's two opposed sweeps (`NativeSlashFx.Frame`), one per half window.
  - All of it thins with Reduced effects and Low graphics.
- **Sound** (`NativeAudio.SprintAttackCues`): existing keys only. `move.roll` scuffs the entry. The landing ring adds
  `impact.ground` over the hop's own jump and landing sounds (`NativeEffectsAudio`), so the landing is not doubled.

### Before and after (offline probe, 60 Hz, flat floor, attackRate 1)

Each pilot runs at 9.18 m/s into one sprint attack on Astra's profile, with the arena rig's jump, locomotion and sprint
layers. "Before" is this branch before the sprint presentation (`2371fa6`): Astra's timing and the footwork's cut A.
Slip is the worst sole slip about its slowest pivot.

| Weapon | Slip, before → after (m/s) | Sink, before → after | Notes |
| --- | --- | --- | --- |
| Sword | 0.044 → 0.041 | 0 → 0 | |
| Daggers | 0.270 → 0.052 | 0 → 0 | |
| Shield | 0.041 → 0.041 | 0 → 0 | |
| Greatsword | 18.9 → 0.025 | 12.4 cm → 0 | Hops 62 cm; feet tucked 24 cm over the stance; both land at 0.467 s as the body does (the window opens at 0.48) |

Renders were made offline from the probe (side, front and its first-person replica, before over after); they are
scratch output, not committed.

### Tests

`Tests/SprintAttackPoseChecks` runs on Astra's profiles (`NativeSprintAttack.Apply`, the hop under 23 m/s², with the
jump layer attached as on the arena rigs):
- provenance and decode;
- the clip time, including the twin played out and back;
- the shield bash;
- strikes in the window and the twin's opposed sweeps;
- pinned soles (at most 0.26 m/s, 0 mm into the floor);
- the leap: feet off through the hop, tucked, the jump layer idle, landing with the body (also when the body comes down
  late, at 0.60 s);
- plain swings and chains keep their cuts;
- the watch: one entry, one ring, none off a ledge or under a special;
- the effects above the floor and inside the ring, the twin crescents, thinning;
- the hooks.

`Tests/MeleeFeelChecks` sprint entries now use the same profiles and hop. Astra's `Tests/SprintAttackChecks` (simulation)
is unchanged.

### Limits and handoff

- No Unity Editor, Windows or two-client run; the effects, the sounds and the first-person view model were compiled
  against the Unity player API, not seen in play.
- The probe's first-person render is a replica of `SprintView`.
- A guest's interpolated `Grounded` and height drive the leap. The landing hold covers a late flag, but this is not
  measured.
- Owner taste is open on:
  - the take choices (the greatsword's wide sweep rather than an overhead chop);
  - the shield's 60° charge;
  - the depth of the greatsword's landing crouch (hips 23 cm under the run, 10 cm under the standing guard).
- **For Astra** (simulation):
  - The step profile starts at rest (`StepAt = At`, smoothstep) while `MoveVelocity` is zeroed. A 9.2 m/s sprint drops to
    about 1.7 m/s for a frame, then lunges at up to 13 m/s, a visible hitch in the root (pelvis 9.2 m/s change in one
    frame). A step curve that starts at the entry speed would carry the run's momentum.
  - The hitbox sweeps horizontally while the shield bashes straight ahead.

### Astra night implementation, 2026-10-12

`codex/unity-night-handoff-20261012` integrates sprint PR #133, implements the owner's landing-timed jump attack (explicit Air/PlungeAt/ImpactAt state, guest prediction and Land event), and corrects S0b with an enforced eleven-cut captured-blade check. See MELEE-SIMULATION-HANDOFF's night section for the precise simulation and replication contract. The earlier proposal/presentation-only status above is historical; Windows animation retiming and real native duel measurements are still pending. No animation clips are changed by this simulation patch.

## Plunging stab (owner 2026-10-12: "Can we add a downward jump attack as well? If the player is facing downwards?"; choice "Plunging stab")

### Presentation (Claude, `claude/plunge-stab`)

On the branch only, not merged or released. Astra builds the rule (docs/NEW-KITS.md section 1). Until her kind field
exists, nothing changes in play: every air attack still shows as the forward jump attack. The presentation is proven on
fixtures and offline timelines built from the spec's numbers. It changes no hit timing, hitbox, damage, cooldown, movement,
stamina or network data.

- **One field mapping** (`Core/NativePlungeStab.cs`, `NativePlungeFields`). It is the only code that names the
  replicated fields. Every presenter reads its struct, `NativePlungeData`.
  - Read today: `Actor.AirAttack` (Swing.Air), `PlungeAt`, `ImpactAt`, and the commitment end once landed (the stuck
    recovery's end).
  - Stand-ins until Astra's fields land, one line each: `Kind(a)` (proposed `AirKind`, `"down"`; returns none today)
    and `Height(a)` (proposed fall height at impact; NaN today, so the watch measures the presented drop).
  - The impact event: `Land` with Detail `melee-down` (Source, Position, Radius; `Amount` read as the height when
    filled). `Direct()` splits the direct victim from the ring until a Detail says which: a hit before the landing, or one
    within 0.85 m of the feet.
- **Classification** (`NativePlungeRules.Classify`, `ForwardFlag`). Only the replicated kind makes a plunge, never the
  airtime. A sprint attack or a swing that a special, stun, roll, juggle or knockdown owns is neither. The forward jump
  attack's four watches (rig, first person, impact effect, cues) get flag 0 for a plunge, so it never shows that attack's
  take or impact. A swing without the kind stays the forward attack.
- **Timeline** (`NativePlungeRules.Clock`, on the replicated times; the presented landing and the impact event stand in
  only for a missing `ImpactAt`):

  | Phase | From | What shows |
  |---|---|---|
  | Windup | At (.08 s to PlungeAt) | the blade brought forward out of the guard, then turned point-down into the tuck |
  | Drop | PlungeAt | committed over .06 s: blade first under the pilot, legs drawn up, head down at the target |
  | Plant | ImpactAt (.045 s, +.035 s at 8 m) | the point already in (it leads the feet), driven in as the hips drop |
  | Stuck | | a strain held over the planted blade, the soles planted, lead flat, rear on its toes |
  | Pull-out | recovery x pull share | the grip draws back along the blade; the greatsword rocks it twice, then wrenches |
  | Settle | recovery x free share | the blade is out; the upper body returns to the guard over the planted feet |

  | | Daggers | Sword | Shield | Greatsword |
  |---|---|---|---|---|
  | Ring (spec) | 0.9 m | 1.0 m | 1.2 m | 1.4 m |
  | Stuck recovery (spec) | 0.30 s | 0.45 s | 0.50 s | 0.70 s |
  | Pull-out starts / free after impact | 0.13 / 0.20 s | 0.22 / 0.33 s | 0.24 / 0.38 s | 0.39 / 0.60 s |
  | Contact in the floor (2 m / 8 m) | 0.13 / 0.18 m | 0.20 / 0.28 m | 0.10 / 0.15 m | 0.36 / 0.46 m |

- **Poses** (`Core/NativePlungePose.cs`, `MeleeMotion.PlungeCut`). The pack has no downward or plunge take: its
  Air-Attack takes all chop forward (searched `Armed-Air-Attack-R1/L1`, `2Hand-Sword/Axe/Spear/Staff-Air-Attack1`,
  `Unarmed-Air-Attack1`, the `Special1` and `Fall`/`Land` takes). So the keys are authored here as hand targets, blade
  directions and leg targets in the rig's group space, solved through the rig's own IK. Each hand keeps the captured
  guard's roll (the least turn of its blade onto the key's).
  - Daggers: both reverse-grip blades point down and are driven in side by side, in a deep wide squat.
  - Sword: a one-handed downward stab angled ahead, the free fist out for balance.
  - Shield: the heater's bottom rim leads and slams in ahead. The free arm is drawn back, and the side sword stays
    sheathed on the shield's back.
  - Greatsword: a two-handed reverse grip, point down, planted deep and angled ahead, nearly kneeling.

  While planted, the contact (blade point, or the shield's bottom rim) is fixed on the floor at its key and the grip is
  solved back from it. The hips drop up to 0.16 m further when an arm cannot reach. A bigger fall crouches deeper and buries
  deeper. The rig owns the legs through the plunge: the jump layer stands aside as for the greatsword's leap
  (`JumpLayer.Leap`), and the floor guard leaves the planted blade alone until it comes free. The end hands the crouch to
  the guard on planted feet.
- **First person** (`NativePlungePose.ViewPose`, `CapturedAnimation.PlungeView`). The view model's grips sit low in
  view. Each blade points at the ground under the camera at the view pilot's real pitch (`Player.ViewPitch`; the view
  copy's own pitch is zeroed). The blade is leaned 0.7 toward the view's axis so it reads as a blade, not a dot: the
  pilot looks down the blade as it falls. The plant jolts the hands down; the pull-out lifts them back.
  - The drop takes the sprint's first-person screen: the edge vignette and speed lines at
    `NativePlungeRules.ScreenWeight`, streaming 1.8x faster. Reduced effects dim it and Low graphics thin it, as for
    the sprint.
  - The air-tear rides the view blade's point.
- **Effects** (`Core/NativePlungeFx.cs`, drawn through the melee kit's scene and sink in
  `Runtime/NativeAirAttackFxPresentation.cs`):
  - Drop: weapon-accent speed streaks trailing up off the blade, shoulders and knees. An air-tear at the point: a hot
    core in a flickering ring with a bow shock.
  - Impact, sized by the fall's 2-8 m weight (the damage bonus range):
    - a flash and shock ring;
    - a ring of light out to the rule's ring radius;
    - cracks over the floor;
    - hot sparks (embers for the greatsword);
    - grit chunks;
    - a dust wave rolling out;
    - through the destruction kit (`NativePlungeFx.Structures`): a crater stain, a chip and 3-7 crack decals, a
      concrete rubble burst from 3.5 m, and the camera kick.
  - The direct victim: a hit flash, a ring and sparks at its chest.
  - The pull-out: a puff of grit where the blade comes out (rubble chips for the greatsword).

  | Fall weight 0 (2 m) to 1 (8 m), sword ring | 2 m | 8 m |
  |---|---|---|
  | Crater radius | 0.40 m | 1.00 m |
  | Cracks reach | 0.45 m | 1.40 m |
  | Dust wave reach | 1.1 m | 2.1 m |
  | Rubble | none (from 3.5 m) | burst of size 1.2 |
  | Camera kick (peak, length) | 3.5 cm, 0.32 s | 10 cm, 0.60 s |

  The kick is the destruction kit's shake. It is heavier than the jump attack's 2.5-6.5 cm and stays inside the
  structure shake's rules: the camera-shake setting gates it, Reduced effects halve it, the 0.3 m cap and distance
  falloff apply. Reduced effects (0.3) thin the particles to about 40% and dim the flashes; Low graphics halves them
  again.
- **Audio** (`NativeAudio.AirAttackCues`, `NativePlungeFx.Sounds`). Existing bank keys only:
  - drop: `structure.whoosh` with the weapon's swing, lower (the shield's is `melee.miss`);
  - impact: `impact.ground`, `move.land`, a `structure.thud` growing with the fall, and the weapon's hit (the shield's
    is `melee.shield.bash`); lower and louder for a bigger fall;
  - pull-out: `impact.debris` and a short swing.
- **Tests** (`Tests/PlungeStabChecks`, 11 groups):
  - the mapping and the event;
  - the classification at rule and rig level (never the jump attack's or a sprint attack's states, no forward impact;
    the forward attack never shows the plunge);
  - the rig's phases frame by frame against the spec timeline at 3 and 8 m;
  - blade first and legs drawn up in the drop;
  - soles planted and the contact held in the floor (within 7 mm) until it comes free, the greatsword last;
  - the first-person blade at -45, -60 and -75 degrees;
  - sizes by height, drawn and in the destruction kit;
  - the Reduced and Low tiers;
  - replicated-data-only keying (a guest's late root, a killcam rig, missing fields standing in);
  - no allocation per frame (the rig allocates no more than the same swing as the forward attack: its existing view
    clone);
  - the runtime hooks. Two pinned strings in `SprintAttackPoseChecks` follow the hook's new form; their meaning is
    unchanged.
- **Not run:** the Unity Editor and play, Windows, a guest client, the real effects renderer. The offline renders use the
  harness's three.js stand-in over the real `NativeVfx` shader blocks.

### Fields needed from Astra

The presentation adapts in `NativePlungeFields` only:
- `Actor.AirKind` (or any name) with the value `"down"`, set on the host from the swing and in guest prediction
  (`NativeMeleePresentation.MeleePresentation`, `NativeReplicaMotion`);
- the fall height at impact on the actor (or in the `Land` event's `Amount`);
- `Land` with Detail `melee-down`, the ring radius and the weapon;
- optionally a Hit Detail that marks the direct victim.

Keep `PlungeAt` as the windup's end, `ImpactAt` as the landing, and the commitment end as the end of the stuck recovery.
