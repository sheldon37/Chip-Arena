# Split-area integration handoff — 2026-10-16

Status: incomplete and inactive on `codex/expedition-split-integration`, parent
`e75d2516cd94273c3ad3366a642e969e09280c72`. This branch does not implement
working split-party gameplay. Do not merge it into the builder as a finished
stand-in fix or activate exp-layout-4 based on managed/compile results.

## Implemented

- `ExpeditionJoinRules`: host-only 3-second countdown, eligible members currently
  inside at expiry only, empty cancellation, capsule centre feet + 0.85, inclusive
  cylinder, airborne membership, ordinal single-door resolution, detached views.
  Transfer refusal starts a new full countdown on the next tick.
- `ExpeditionCohorts.Active/Members`: detached snapshots for the runtime adapter.
- `NativeExpeditionCohortPlacement`: reserve validated supported empty slots, hold
  cohort input, move existing pilot GameObjects and seats between local scenes,
  retain vitals, update occupancy, restore source on rollback.
- `NativeExpeditionAreaTransfers`: connects the inactive registry and cohort
  state machine to join membership and slot reservations. Existing Ward occupants
  are not held while a newcomer waits for readiness. Requires both runtimes to
  already exist and be ready; does not itself load scenes or bind transport.
- Native import fixture for subset movement, rollback, unchanged bystander,
  seat/scene ownership and rejection before mutation for unequal clocks.

## Remaining blockers

1. Pilot cooldowns and kit timers use arena-local clocks. Runtime clocks diverge
   during empty suspension or PvE hold. Placement deliberately refuses unequal
   clocks; implement and test an explicit timer migration contract. Active
   projectiles/fields, DOT references, rewind history, corpse/contact ledgers and
   held cast state need ownership audit and failure recovery before activation.
2. Replace the single arena FixedUpdate path with registry scheduling and stable
   account/seat routing; preserve one session clock tick and one scene simulation.
   Rebind local presentation without resetting the other area's encounter.
3. Director, progression, damage/revive/reprint, closures, encounters, participant
   freeze, wipe, extraction and checkpoints still use the old global run area.
   Implement the owner-approved cross-area table in EXPEDITION-HUB.md.
4. Negotiate split-area wire capability; route each recipient's frame/projection,
   input, event and ack with area/epoch/transfer identity. Existing network code
   still broadcasts one area; never expose two-area data through it.
5. Owner removed run saves/resume on 2026-10-16. No save-envelope or schema-1
   reader work remains. Finish packet-byte, active-count and memory instrumentation;
   run required Windows worst-case timing.
6. Connect `joins[]` records (doorId, area, joinInside, joinStartedAt, duration),
   `members[].atJoin/area`, and viewer-local area/phase/loading/areaBarrier to
   the public projection. Only then replace F entry alongside subset transfer.
   Preserve F exit/interactions. Claude owns the visual ring/countdown/party list.
7. After all gates, switch live exp-layout-4 and BOTH LiveSha256 and Python PINNED.
   Fresh runs no longer load saved layouts. Live connections still validate layout identity.

## Verification

Managed C#9 warnings-as-errors compile passes. Join-rule suite: 25 assertions;
cohort: 69; registry/query audit: 292; co-op: 41; director: 98; join draft: 38;
layout: 268. Source package check: 2219 files.

Unity Player/Runtime/Editor semantic compilation uses pinned Unity 6000.3.24f1
reference assemblies; compiler warnings in existing presentation/updater fields
are unrelated. This is not execution of Unity or its native PhysX fixtures.

Not run: new native placement fixture, split-area transport acceptance, Windows
one-host/three-guest play with hub bystander, Windows two-area timing. No smoothness
or performance claim. Shared and builder were observed at bb4b36a before push;
this work has not been merged, built, published or enabled.

## Follow-up: fresh runs and wire guards

Live no-save policy was merged and synced to shared/builder at `8d1ec86` on
2026-10-16, including the separately merged airborne chip-selection fix.
Publication has not been verified. The gated split additions remain on this branch.
This integration branch adds `ExpeditionAreaChannel<T>` for explicitly bound
area/epoch/transfer identities with independent monotonic frame/event streams;
`ExpeditionAreaCommandGate` rejects stale, duplicate, unready and wrong-account
input. Capability negotiation contract is defined, but not advertised by live
connections until the scene/director path is integrated.

`ExpeditionAreaProjection.Build` requires a detached area-specific provider,
refuses cross-area anchors, chests, arrivals, closures, loot, dice and hub state,
and separates recipient-private data. Shared-public batching explicitly refuses
split sessions. View receivers require explicit ticket binding and clear pending
state on transfer/reconnect; guest replies must match both request and current
identity. `joins[]`, `members[].area/atJoin` keep the agreed field names.
The current global director is deliberately prohibited from supplying split views.
A per-area provider and actor-frame transport call sites still need integration.

Checks: 25 area-wire checks, 11 fresh-run checks, 91 integration, 481 rules,
8370 presentation, 41 co-op and 98 director pass, plus C#9 warnings-as-errors
compilation and Player/Runtime/Editor Unity API compilation. Source package: 2225
files. No native execution, live split transport or Windows timing claim.
