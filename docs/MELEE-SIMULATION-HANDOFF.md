# Melee simulation handoff — 2026-10-12

Implemented on `codex/unity-melee-abilities-20261012`, from owner-named `19c00c7`, with concurrent shared `c5d9d0a` integrated (Claude torso/entrances and the PvE pickup changes).
Claude owns animation, VFX, audio and HUD presentation. No new animation or effects assets are in this patch.

## Budget and timing decisions

The proposal contradicted its own shared limits. This implementation follows the owner's Q ≤30 / E ≤60 base budget and .15 s minimum telegraph: Flash Step and Shadow Step windups are .15 s, Bulwark is 20 + 10 wall damage, and Aegis is 20 on each of at most three distinct targets. Normal damage/stat chips still modify those base values. Whirlwind replaces Wolf Knight: seven 8.5-damage pulses total 59.5 base per target; only the first pulse staggers. Sword is 33.6 + 22 on its launched target; its 10-damage landing wave excludes that target. Kunai is 18 + 28.

The eight entries live in `tools/melee-abilities.json`, included by `tools/export_source.mjs` and already present in `Resources/GameContent.json`. Existing DEV and ranged/magic ability entries stay intact. Stamina uses abilityCost/resourceCost; cooldown uses abilityCooldown. Ability windups use the ability attack-rate helper with a .15 s floor. Sword/dagger/shield E recovery retains the existing special recovery divided by attackRate.

Recasts cost no additional stamina. Sword's .9 s recast window follows a successful launch; without the recast its original cast cooldown becomes 7 s. Skyfall and Kunai recasts have .2 s anticipation and recheck target, range, line of sight and free destination before striking. Teleports sweep the whole capsule path against cover. Skyfall waits for actual ground contact before its wave/bounce. A missed Kunai reduces E to four seconds from throw release. Rolls cancel windups, including recast windups, but not committed active abilities; cancellation spends the original cost/cooldown.

Bulwark absorbs three frontal projectiles at most; rear projectiles still hit. It stops on contact and carries the victim through swept collision for up to 6 m. Its wall stun extends that same impact, with the normal post-stun grace. Fissure damage is once per target; the travelled ground strip slows everyone by ×.7 for its two-second life. Every ability hit goes through timed-block, roll and damage rules. Whirlwind has no legacy leap, slam or super armour. Blocking an attack still punishes its attacker.

## Primary swing tuning

The owner supplied explicit values, then Claude's full `MELEE-FEEL.md` arrived in shared `c5d9d0a` during this task. The implementation uses the **owner's sword/daggers/shield/greatsword ordering**: turn rates 720/540/450/300°/s and default steps .9/.8/.75/1.0 m. The document's S3/S4 tables swap sword and daggers for those two rows; they do not override the owner's explicit mapping.

- `InputFrame.Attack` is the LMB press edge already present on the wire. Native Update now sets it, host input coalescing ORs it across packets, and HeldOnly consumes it once. Held LMB retains full-recovery repeat; a fresh edge enables the early chain.
- Buffer from `HitAt` through recovery, and during the last .2 s of a roll (S1). Each press expires after .4 s. An early greatsword press can expire before its .95 s chain point. Extra-strike chips finish before a paid chain can interrupt recovery.
- Each accepted swing pays its existing stamina cost. No queued/funded chain means full recovery. Combo resets .35 s after recovery, following S5. Stagger immunity stays at stun end + .5 s.
- Host turns yaw during windup, freezes it at HitAt, and starts a chain from the previous heading to avoid an instantaneous turn. Existing step windows stay intact. Movement is simulated, never animation root motion.
- `InputFrame.LockTarget` carries the selected pilot/minion id, not an authoritative position. The host rechecks enemy status, life, ≤24 m range and line of sight. A target in the forward hemisphere extends the step within the caps below, stopping 1.15 m short of its starting center. Without a valid lock, use the default step. World collision remains authoritative.
- Guest input acknowledgement/replay predicts local swing commitment, turning and step immediately, plus a private affordability estimate. The host owns actual stamina, damage and contacts. Rejection/cancellation replaces the prediction. Only a temporary local presentation copy uses the predicted pose; host snapshots and killcam records stay untouched. Its clock offset is fixed per swing, avoiding a new attack timestamp each render.

At attackRate 1 without extra strikes (timings scale by 1/attackRate):

| Weapon | Hit starts | Active ends | Chain starts | Full end | Stamina | Default step | Opener cap | Chained cap | Turn °/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Sword | .14 | .30 | .50 | .62 | 14 | .9 m | 2.2 m | 1.4 m | 720 |
| Daggers | .09 | .25 | .32 | .40 | 7 | .8 m | 2.0 m | 1.2 m | 540 |
| Shield | .20 | .36 | .60 | .75 | 16 | .75 m | 2.0 m | 1.2 m | 450 |
| Greatsword | .42 | .58 | .95 | 1.18 | 20 | 1.0 m | 2.4 m | 1.6 m | 300 |

The per-weapon caps are the initial mapping within the owner's 2.0–2.4 / 1.2–1.6 m bands; the proposal's exact cap/magnetism table differs. Keep this mapping until the native duel pass, then agree any changes. `SprintAttack` records a grounded swing entered from sprint; it does **not** add the separate proposed G3 damage/cost/recovery bonuses. Optional recovery movement, backpedal penalties and other balance knobs remain gated.

## Snapshot contract

Read `Actor.MeleeKit` (copied into each network snapshot and restored to `NativePilot.MeleeKit`). It is deep-copied for replay history; the shield array is never shared. Coordinates are world-space body coordinates, time is the match clock, and kit yaw is **radians** (Actor.Yaw/CommitYaw remain degrees).

| Fields | Meaning |
| --- | --- |
| Kind, Phase, At, Release, Until, Yaw | Canonical ability id; windup/active/recovery/recast/fall; cast start, damage/movement release and phase end |
| RecoverUntil, Tick | Recovery end (for all kit casts); Whirlwind emitted pulse count 0–7, including misses |
| RecastKind, Target, RecastUntil | Canonical id, launched/marked target id and E recast deadline; Whirlwind is targetless (`Target=0`) |
| MarkTarget, MarkUntil | Kunai mark owner-to-target relationship and expiry |
| Afterimage, AfterimageUntil, EmpowerUntil | Decoy position/lifetime and next dagger hit's +40% deadline |
| ShieldOut, ShieldPosition, ShieldChain | Shield away flag, current position, distinct hit ids in order (≤3); block is unavailable while away |
| FissureStart, FissureDirection, FissureAt, FissureUntil, FissureSpeed, FissureLength | Authoritative line origin/direction, lifetime, 25 m/s speed and wall-clamped length |
| SuperArmour | Retained compatibility field; Whirlwind leaves it false |
| Absorbed | Bulwark's absorbed projectile count (0–3) |

Primary fields remain `AttackAt`, `Combo`, `SwingAt/SwingUntil`, `CommitAt/CommitUntil/CommitYaw`, `StepAt/StepUntil/StepDistance`, `VisualWindup/VisualActive/VisualDuration` and `Followup`. CommitYaw updates each windup tick. Added actor fields: `SprintAttack`, `OpenerLunge`, `ChainAt`, `ComboUntil`, `MeleeBufferUntil`, `SwingRemaining`. The latter fields let guest acknowledgement replay resume a queued or extra-strike swing. Native frame validation rejects nonfinite timing/heading/distance and invalid counts.

Whirlwind's `Release` is spin start, `Until` is spin end, and `RecoverUntil` is the end of the .5 s recovery. Recast shortens both ends; `Tick` never resets until the cast clears. `Actor.SpecialUntil`/`AttackUntil` stay committed through recovery. For its spin clock use `(time-Release)/.45` and the authority's completed pulse count; do not infer a leap or landing. Recast opens at `Release+.5`, with `RecastKind="whirlwind"` and `RecastUntil=Until` but `Target=0`; HUD code must allow this targetless recast. Active motion is steered, ×.8 normal run speed, including guest prediction. Presentation remains Claude's handoff.

Legacy Actor.Special* fields remain for current animation compatibility (`sword-launch`, `dagger-throw`, `shield-throw`, `whirlwind`; recasts use `skyfall`/`kunai-recast`). New presentation should use MeleeKit.Kind and Phase. Empty/null Kind means no current cast; marks, decoys, shield and fissures can outlive it. Thrown effects also carry the canonical fx id in NativeEffectFrame.effect.

| Event | Payload beyond canonical fx id in Detail |
| --- | --- |
| AbilityCast | Source caster, Position origin, Normal heading |
| AbilityHit | Target victim, Position contact, Normal contact direction, Amount actual damage |
| Mark | Target marked id, Amount 1 on / 0 off |
| Teleport | From origin, Position destination |
| Recast | Target victim, From caster, Position target anticipation point; Whirlwind early end has Target 0 and Position caster |
| Absorb | Target projectile id, Position contact, Amount absorbed count |
| Bounce | Target next victim, Position/From previous contact, Normal next direction, Amount targets already hit |
| Land | Position landing, Target Skyfall victim, Radius wave radius |
| FissureStart | Position start, Normal direction, Amount speed, Radius half-width |

New event values are appended to EventKind; existing numeric values, including Teleport, are unchanged. Block uses Detail `guard` (Target 0), `clash` (melee/summon/add) or `deflect` (projectile). Hit blocks preserve attacker/owner id in Target and the collider surface contact, instead of the projectile sphere center or guessed effect classification.

Bots read the ability entries' distance/cost/cooldown rules, prioritize available gap closers, aim ballistic Kunai, use marked/launched recasts and are distracted by the one-second afterimage. Approach now uses forward sprint with a stamina reserve, lock-target lunges, visible attack-range checks and explicit chain edges. On legacy cover maps the capsule-wide route probe chooses a clear side; Brickfall keeps its structural navigation. Standard melee no longer spends rolls automatically just because a target is 4–11 m away.

`tools/check_melee_duels.py --unity <activated-Unity-executable> --seeds 4` runs the real `NativeArena` and isolated PhysX scene on Brickfall, Foundry and Freight, all four melee vs seven ranged/magic weapons, both spawn sides, four seeds (672 duels). Difficulty defaults to normal; flags select maps/difficulty/output. JSON records per-duel HP, elapsed time, winner, timeout, source commit and per-map/weapon win rates against the 25–55% band, with draws/timeouts visible. Seed pairs control both native and Unity RNG. It is a separate measurement command, not a probabilistic build gate. The existing Electron harness does not validate this Unity kit.

Launch was attempted here and stopped with `Unity Editor is unavailable` (exit 2); **no native duel results exist yet**. No extra balance tuning was done after that blocker.

## Accounts and builder

The shared account service accepts all seven procedural entrance ids and the twelve portrait backdrops (`lobbyBack`), with an explicit allowlist/default and unchanged grants. `profileSchema: 2` lets Unity distinguish a native-aware save from the old stripping service. Pending entrance choices stay account-scoped until the server acknowledges them or returns a different remote choice; upgrading the service or a failed save does not erase a pending choice. Seat frames now carry the actual overrideIntro flag. Retired/unknown picks normalize to hero-landing with override off (weapon signature), both on host receipt and guest frame application.

Owner Builder v4 already invokes `python tools/install_local_assets.py <source>` in the checked-out Unity project **before** building. Put the private `LocalAssets.zip` beside `Build-and-Publish.cmd`; a `LocalAssets/` folder, when present there, takes precedence. The ZIP/folder fingerprint participates in build reuse, so new audio invalidates the cache. Installer failure stops the build/publication path. This was exercised with a subprocess fixture and mocked build, plus the real installer tests for the 191-key/18-entrance contract. Raw licensed files remain ignored and excluded from source packages. No audio files or replacement builder are committed.

## Verification and remaining gates

Integrated managed/relay suite: 379 PASS, including the new NativeMeleeFeelChecks and Claude's torso checks. Python: 86 tests, one skip. Account integration: 8 passing tests; TypeScript check passes. Core/Runtime and the changed native network/build fixtures compile against genuine Unity player API assemblies (only Editor preview-scene entry points are compile stubs). Source package check and diff whitespace pass.

`ValidateSpecials` now exercises Q/E funding/commitment, roll windows, recasts, cover, absorb cap, carry/wall budget, fissure hit ledger/slow, armour and queued paid swings. NativeNetworkBuildChecks exercises the actual JsonUtility/guest/replay contract and entrance normalization. These fixtures are part of the existing Windows build gate, but **have not run in Unity Editor here**. Windows building, two-client/late-join/killcam play, balance duels and new Q/E VFX/audio integration are unrun. Claude's existing torso/entrance updates are integrated; his matching swing retime/blends remain separate presentation work. Account deployment and compiled test-channel publication are still pending; this is a source handoff.
