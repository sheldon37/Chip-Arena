# Zone boss authority handoff — 2026-10-17

Branch: `codex/zone-boss-authority`, based on shared `bc397907a6a3` and the first-arrival door change in #271, then merged with shared `03dddab1` (Station presentation). Now integrated with shared `ffeee79c2c97`; both presentation binding points are connected on this candidate.

Status: **rules candidate, not activated or released**. `NativeZoneBossRules.RuntimeEnabled` is false. The existing Siren remains in the live zones until authoritative hazard presentation and native acceptance are complete. This switch must not be flipped on the strength of managed or semantic compilation alone.

## Binding contract (final field names for this candidate)

Both zones use `NativePveFrame.zoneBoss` (`pve.zoneBoss`), exposed as `NativeArena.ZoneBoss`. Its optional presence flag is `hasZoneBoss`; absent records remain null through Unity JSON. The record class is `NativeZoneBossFrame` in Core/NativeZoneBossRules.cs.

| Fields | Meaning |
| --- | --- |
| `archetype`, `actor` | `station-647` or `ironworks-converter`; minion actor ID, not the reserved Siren seat |
| `phase`, `phaseAt` | 1–3 and the authority time of the phase transition |
| `action`, `actionId`, `actionAt`, `hitAt`, `duration`, `cancelAt` | Action ID string or null; incrementing action instance; absolute start/contact/cancellation times; duration in seconds |
| `targetSeat`, `targetX`, `targetZ`, `actionYaw`, `variant` | Locked target and aim, yaw in degrees; stomp leg or call vessel mask; second Station charge uses variant 1 |
| `laneX0`, `laneZ0`, `laneX1`, `laneZ1` | Charge endpoints; first endpoint also anchors melee/ring actions |
| `holdTarget`, `holdSide`, `holdUntil` | Held seat (-1 none), -1/+1 side, release deadline; early release clears the seat and records the actual release time |
| `staggerAt`, `staggerSeconds`, `staggerPushX`, `staggerPushZ`, `staggerCause`, `stagger` | Stagger timeline and optional displacement; cause `crash`, `express`, `part`, `phase`, `choke`, or `kneel`; Converter meter 0–1. Push components currently remain zero. |
| `lampBrokenAt`, `pantoBrokenAt`, `expressHitAt`, `sideBroken` | Station breaks; struck flank mask (left bit 0, right bit 1) |
| `liveAt`, `liveUntil` | Live Rail interval |
| `barrierDownAt`, `boardDownAt`, `roofOpenAt` | Station arena events |
| `spillAt`, `spillIds` | Station scripted add event and resulting actor IDs (up to six) |
| `parts`, `partAt[5]` | Converter intact crust bits 0–3 (front, left, right, back), pulpit bit 4; five individual break timestamps |
| `deathAt` | Death start; **use this name, not `diedAt`** |
| `hazards[]` | Up to 64 persistent hazard events, retained across action changes so late guests can draw surviving pools and charge trails |

Each hazard has `id`, `kind` (`pour`, `blow`, `deslag`, `charge`), fixed landing/ground target `x/y/z`, `radius`, `hitAt`, and `until`. These are event targets and lifetimes, not animated droplet poses. All wire floats are finite; absent event times use **-100**, absent seats **-1**. Convert negative event times to the presentation's NaN convention at its adapter boundary. Copy() detaches both arrays and nested hazard records; Valid() bounds arrays, identities, enum strings, lifetimes and finite values.

Root position, yaw and health come from the matching `minions[]` actor. The reserved PvE boss pilot is only an attribution/intro anchor: disconnected, kinematic, collider disabled. `bossArrivals[]` keeps the zone's boss identity and updated health. No leg, joint, spring or pose data is added to the boss record.

World-point spawned adds additionally carry `minions[].bossSpawn`; guests reconstruct a settled `boss` entry instead of replaying an unrelated vent/drop entrance. Existing ordinary entry behavior is unchanged.

## Timing and gameplay

- Intro hold stays **9.0678 seconds**. No beat-map regeneration is needed. The simulation hold retains the existing frozen-clock behavior; presentation should use the existing hold progress for arrival playback.
- Use the authority `hitAt` for contact, including the second Departure contact at hitAt + 2 seconds and the broken-lamp extension. Platform Alteration is the explicit exception to “anticipation equals contact”: contact is actionAt + **1.8 s**, after the 0.6 s gesture and 1.2 s pane warning. `pve.zone.glassAt[]` uses the same landing time.
- Station thresholds are 65% / 30%; Converter thresholds 70% / 35%. Damage carries across thresholds; phase changes use action/stagger timelines, without Siren reveal cinematics.
- The Station schedules the normal express in phase 1, then roughly 40-second calls in phases 2–3; the existing lever halt remains authoritative. Glass ambient intervals are off / 9 / 6 seconds. Barrier colliders disable and the fallen-board collider appears at their events.
- Converter `call` drives `pve.zone.converterAt`, `converterMask`, and phase 3 drives `overflowAt`. **These three use the zone hazard clock**, not arena time. Vessel lanes retain 2.5 s warning / 4 s live / 2 s clear; overflow is 8 s live / 6 s cool. The ladle stays parked.
- Persistent burns use the existing hazard damage path. Boss attacks use normal player damage/block/roll checks. Boss damage routes through the minion hit path, with armor, breakable parts, weak-point bonuses and held-victim release thresholds.
- The rules include lane walking/charges, sweep/ring/cone actions, grabs/releases, deterministic blow landing targets, scripted capped adds, and death delays. Station compound body and Converter hull/leg hurt volumes need native collision acceptance against the finished motion.

## Verification and activation gate

Managed rules tests cover phases, action clocks and wire copies, finite-value rejection, event bounds, hazard boundaries and deterministic rain. Player, Runtime and Editor compile against the pinned Unity references on the integrated 03dddab1 tree. Focused results: 274 boss-rule checks, 274 Station model checks, 697 Converter model checks, 158 Ironworks visual checks, 15,885 Station presentation checks, 23,269 laser-door checks, 9,683 state-scene checks, 108 director checks and 41 co-op checks. `ValidateZoneBossAuthority` is wired into the native Expedition activation fixture: it exercises a real weapon path, windup/contact/single-hit, phase transition, codec/guest application, hold release and death collision.

Local full managed regression stopped at BowChecks' 15,424-byte allocation assertion; compiling and running the unchanged bc39790 baseline reproduces the identical failure. Focused affected checks pass. This is not a green full-suite claim.

**Not yet run:** that fixture in Unity/PhysX, either boss's end-to-end native encounter, late-join/split-area multiplayer combat, or visual contact alignment. No claim of completed balance or native acceptance is made. In particular, check lane navigation and compound body fit in the real arena, all action hit volumes with the motion library, upper weak-point reachability, and world-spawned adds on guests. Candidate scheduling and hitbox numbers remain subject to that acceptance.

Remaining presentation integration: draw Converter persistent hazards from the authoritative targets and event times (the high-level views are now bound). Astra's next acceptance: run the native fixture and two-client encounter tests, resolve failures, then activate the switch and publish the integrated shared commit. Optional beat-lock work (#18) and general horde smoothing (#19) are not included; no NativeReplicaFrames accessors were removed.

## Other handoff answers

- #17: after a successful deployment, the deployer's ticket already points to the destination when `ExpeditionAreaProjection` chooses the view. They do **not** receive their own new hub `deployedAt` in that successful transfer path. Hub observers can receive it. Keeping both presentation cases supported is appropriate.
- #20: #271 keeps an unused split-instance exit open until the first ready cohort commits (`instance.entered`). The director then publishes `sealing` with authority elapsed and `sealed` after the existing one second. No `members[].arrivedAt` field was added.
- Cloud publishing: #270 adds bounded, redacted request diagnostics. Run 418 still returns HTTP 500, now classified from response wording as `quota_or_billing`. This is a diagnostic clue, not proof of the exact quota or a provider-side fix. No blind retry was added.
- Release receipt: desktop Build-and-Publish already successfully published shared/builder **bc397907a6a3** on 2026-10-17 at **20:33 UTC** (4:33 PM Eastern), newer than 5dc860d. The test feed version is **7b5a3845ab7a30b36fa767f9afc36af499179606df12a48a0584904aa80421c3**. Latest-Build.txt and Latest-Build-Status.txt confirm successful Unity build and publication. This receipt covers the pre-existing laser-door presentation, not this candidate.

Latest release verification at handoff: shared/builder **03dddab1da69** has now also been desktop-published, at **21:12 UTC** (status updated 21:12:51). Latest-Build.txt identifies 03dddab1 and a successful Unity build; Latest-Build-Status.txt reports publication succeeded. The test feed now serves **33093513d955ea654a1135a0813ec9972170a75af3fed1639c1d02eccfc435ad**. Neither #271 nor #272 is in that publication.

## Addendum integration — 2026-10-17 evening

Merged current shared `ffeee79c2c97`, including both presentations, Station stage wiring, voxel zombies and the zombie motion pass. Both `StationBossViewOf` and `IronworksBossViewOf` now bind through allocation-free NativeArena adapters and the pure `NativeZoneBossViews` converters. Missing/wrong-archetype actors still return None. The Ironworks model guard retains both the presentation exclusion and NativeZoneBossRules exclusion.

Confirmed Converter action contact remains `actionAt + Windup`; delayed droplets/lumps have their separate hazard landing times. Stomp variant is FL=0, FR=1, RL=2, RR=3 (all four quadrants now selected); call mask is A=1, B=2, both=3. Arrival still uses BossSongTime; death remains deathAt plus elapsed victory hold. The adapter maps `holdTarget` into the grab view's `targetSeat` (including early release) and cancellation/stagger into the existing choke/kneel/crash presentation moments.

TERMINUS Departure now keeps one actionId for **6.1 s**, including the lamp-broken case, with two damage windows beginning at `hitAt` and `hitAt + 2.0 s` and separate per-target hit ledgers. The second leg returns along the locked lane. Live Rail uses the presentation's actual six puddle centres. Station arena collision is derived directly from settled `BarrierPose(side,1)` and `BoardPose(1)` (including the board pivot). Original barrier colliders turn off; their source blocks remain. New collision-only objects represent the thrown segments.

Activation is still gated off. The native fixture now also checks host/guest view binding but has not run here. There is a further integration issue to resolve before enabling: Converter presentation generates rain/leftovers from its own layout and action history, while the rules' persistent `hazards[]` has authoritative targets. Those effects need to consume that event data or share one layout; merely binding the high-level view is not proof that every damaging pool matches its drawn warning. This is recorded explicitly rather than enabling unverified invisible/misplaced damage.

Publication verified independently: desktop Build-and-Publish already published shared `ffeee79c2c97` successfully at **2026-10-17 23:46 UTC / 7:46 PM Eastern**; status updated 23:46:31 reports publication succeeded and the Unity log names the exact source. This does not include PR #272. Cloud run **424** still returns HTTP 500 with diagnostic category `quota_or_billing`; the exact service/account condition remains unverified.

Addendum verification: 281 boss-rule checks, 274 Station model checks, 697 Converter model checks, 22,219 Station presentation checks, 16,156 Converter presentation checks, 23,269 laser-door checks and 9,683 state-scene checks passed. Player, Runtime and Editor semantic compilation passed against pinned Unity references. Native/PhysX and two-client acceptance remain unrun. The generic summon renderer skips both boss archetypes so their bound dedicated presentations own their rendering.
