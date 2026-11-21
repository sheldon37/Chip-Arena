# Expedition runtime integration — 2026-10-14

## Current integration status (verified against shared `5aa2e0a`, 2026-10-14)

The M1 maps/navigation/manifest are imported (`exp-layout-2`), the town dressing is
present, and the state presentation binds the PR #216 fields. Earlier draft and
"next" paragraphs below are historical and must not be treated as the current
handoff. See [CLAUDE-EXPEDITION-HANDOFF.md](CLAUDE-EXPEDITION-HANDOFF.md) for the
current role split and acceptance checklist.

Expedition is still not playable: the scene/combat adapter, runtime geometry
probes and collision/placement, encounter spawning/death callbacks and interaction
input must be connected. Host migration is also unfinished. Presentation being
implemented does not establish Windows/two-client acceptance.


Branch `codex/expedition-integration`, base shared/builder `65f9f131`.
This is an integration bridge for the presentation contract, **not a playable
scene or a completed host-migration transport**. The normal menu remains closed.

## Runtime entry points

The scene/director creates a `NativeExpedition`, then calls
`ConversionGame.CreateExpeditionSession(run)`. Configure the session's world,
interaction adapters and quote source, then call `BindExpedition(session)` on the
signed-in host. Read `ConversionGame.ExpeditionView` and `ExpeditionCommands`
from presentation. The existing room pump steps the bound authority and carries
its views/commands. Binding is deliberate; the preview remains an independent,
scripted authority with temporary saves.

`SetWorld(anchors, chests)` persists door, machine, chest and reward anchors, plus
chest generation/price/cooldown metadata. Coordinates are explicit world-space
positions for the anchor's area. `DropAt`, `DefeatBossAt`, `FinishFinalAt` assign
actual encounter positions to every issued reward ID; `PlaceReward` supports
subsequent authority placement. Missing anchors mean not placed, never a guessed
origin. `cooldown` is an authority-clock deadline (compare with `elapsed`); scene
code owns chest restocking policy and supplies `CanPurchase` proximity evidence.
`Purchase` commands use persisted offers and public sold status; `RestockChest`
accepts only a fresh generation. See the presentation follow-up contract for
`BindExpeditionScene`, `InteractExpedition`, building/yaw and clock semantics. No chest scheduler or city
layout is invented by this bridge.

## Replication and authentication

- `expedition-public`: one encoded `ExpeditionView`, with viewer/mine stripped,
  reused for all participating authenticated connections.
- `expedition-private`: run, epoch, revision, viewer, and **only that owner's** mine.
- `expedition-command`: run/epoch fence plus transaction, intent, target, chip.
- `expedition-reply`: transaction, state, reason. Accepted means Pending.

The host publishes at up to 20 Hz, coalescing intermediate tick revisions. It
also refreshes when source-only metadata or recipients change. Frames use the
existing bounded reliable queue; overload disconnects rather than silently
confirming unsent state. Guest pairing accepts only matching run/epoch/revision
and its own account, normalizes decoded fields, and rejects older completed
versions. A new room resets the receiver. There is no full-snapshot broadcast.

The host resolves sender identity from the verified seat grant and checks expiry;
there is no account field in the command. Lobby profile replacement is blocked
while a session is bound. Commands retain the current rate budget. The router
bounds and deduplicates transactions, rejects changed payload reuse and stale
run/epoch requests, and executes `ExpeditionHostCommands` with session hooks.
Journal exhaustion fails closed. Duplicate journals are in-memory, not a durable
cross-process exactly-once guarantee. Guest refusals flow directly into
`ExpeditionIntentTracker`; ACKs never substitute for reflected state.

## Hooks and rules

- Offers: three seeded catalog choices, existing `NativeContentRules` eligibility,
  caps and exclusions. Boss pools are restricted to epic/legendary/unique; an empty
  eligible pool refuses, with no lower-rarity fallback. Selection rechecks rules.
- Ordinary pickup requires `CanClaim(account, reward)` from the scene. Reserved
  personal picks do not need ground-pickup proximity.
- Reprint: `ReprintEvidence` must supply host-observed cost, distance/range,
  uninterrupted channel duration and machine/medic permission. Remote recovery
  requires a machine; the authority rechecks wallet, carry/safe state and victim.
  `Sources.ReprintCost` supplies the public victim quote; absent price shows as
  unavailable. Scene code must keep that quote consistent with its pricing policy.
- Extraction: simulate on a detached run, write/flush the extraction journal,
  then replace live authority and `Sources.Saved`. Write failure preserves the
  live state. Existing unreadable saves cannot be overwritten; loaded tombstones
  must already be reflected in the supplied authority. Safe checkpoint creation
  remains explicit (`session.Checkpoint()`). No account XP receipt is claimed.
- Final boss: `FinishFinal(drops=-1)` now issues dice-keyed boss rewards before
  Victory, once, with the same frozen living/connected eligibility and split as
  building bosses. The At overload adds positions. `selected` persists privately.
- Continue policy: all connected, not-extracted pilots choose Continue (downed
  pilots can consent). Individual extraction remains independent. The session
  starts the next loop only when everyone remaining has consented and at least
  one pilot is alive. Disconnect/extraction removes that pilot's vote; loop reset
  clears all votes. Legacy direct `NextLoop()` remains an authority API for scripted
  fixtures; the command path uses `AdvanceWhenReady()`.

## Remaining dependencies and honest transfer state

The projection carries `packageStored`, `endpointReserved`, `ackReceived`,
`deadline`, `endpointMigration`, candidate metrics and reprint quotes. Values
must come from observed adapter progress, never merely from authority handoff.
The current transport does **not** reserve/migrate endpoints or transfer the full
live combat package. Consequently EndpointMigration stays false, no ACK/package
progress is fabricated, and host extraction with remaining peers stays refused.
Candidate RTT and jitter are measured on existing links; loss is the proportion
of timed-out ping probes, not network-layer packet loss. Reachable/capable stay
false until a destination endpoint is actually probed and supported.

Still required: city/instance scene binding, actual combat event callbacks,
placement coordinates, machine/channel/medic measurements, chest lifecycle,
checkpoint cadence, candidate endpoint probes, complete combat snapshot transfer,
durable destination package/ACK, reconnect and split-brain fencing. Optional
per-account boss-ineligibility reasons are unchanged. `difficultyParty` remains
connected, not-extracted pilots until the director establishes its roster policy.
Unity/Windows rendering, two real clients and disk behavior require native
acceptance; managed checks cannot establish those outcomes.

## Area-ready transport (codex/expedition-director-lifecycle)

`expedition-area-ready` now carries an `ExpeditionAreaReadyRequest`: `run`, `epoch`,
`target`, `generation` and `layout`. It has no account or placement coordinates.
The host takes account identity from the verified, unexpired peer seat and applies
the existing command rate budget. The Core router checks the run/epoch and active
loading state before the director checks membership, target, generation and
fingerprint. Duplicate tickets are idempotent; they never finish placement.

Scene adapter sequence (Astra-owned, still to implement):
1. Capture `ExpeditionAreaReady.Capture(ConversionGame.ExpeditionView)` before
   starting the load. Retain that exact ticket through asynchronous completion.
2. Load that target map and validate its layout/collision. Only after success,
   call `ReportExpeditionAreaLoaded(ticket)`. Never capture a new ticket in an old
   load's completion callback, and never acknowledge from the presentation timer.
3. On guests, Pending means queued. The client retries the same detached ticket
   at 2 Hz while the corresponding barrier remains active, so a rate-budget
   refusal does not permanently strand a successful load. A changed/completed
   barrier or room reset cancels the retry. Readiness and completion are confirmed
   only by the public barrier; no local success screen is synthesized.
4. The host reports its own successful load through the same Core router. It then
   calls `CompleteAreaLoad` with real atomic placement; a failed placement keeps
   the barrier frozen. Neither an ACK nor the new message calls it automatically.

Continue into a new loop now clears arrivals and starts a new barrier even when
`area` remains the city. The session synchronizes this immediately after advancing
so projection cannot publish a new loop with the previous loop's ready count.
The placement uses sanctuary slots, not a stale instance-return doorway.

This closes the wire path, not the scene loader or combat integration. No menu is
opened and no endpoint migration capability is advertised by this change.
