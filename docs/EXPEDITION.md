# Expedition — programming and presentation contract

Owner design recorded 2026-10-13. First implementation: `codex/expedition-foundation`,
based on shared Unity commit `f043b7cb83f103cc0e78c7b3a33bd6a0cfc0574f`.

## Delivery status

This is an **opt-in, engine-independent rules foundation**, not a playable mode.
Nothing enables an Expedition menu button, changes the existing four-player Siren
mode, or modifies PvP, combat, animation timings or the current network protocol.
The rules support up to eight connected participants. The existing transport does
not yet support Expedition drop-in or host migration.

| System | Implemented in this milestone | Still required before play |
| --- | --- | --- |
| Run | Membership, instance quota/boss gates, final gate, victory/loop state | City selection/content, scene orchestration, actual enemies and world boss |
| Difficulty | Configurable time/player/instance/loop multipliers | Spawn director, applying stats, balance and performance tests |
| Departure | One-vote instance countdown, unanimous shortcut, majority extraction countdown, individual extraction | Interactions/UI, persistence transaction, session disconnect/close |
| Chips | Personal purchase reservation, first pickup, private selection ownership, boss survivor split/dice, catch-up entitlement | Content-backed offers, Epic+/Unique catalog, physical drops/chests, chest respawn scheduler |
| Death | Absorbed data ownership, paid reprint, optional safe-machine remote recovery | Range/channel checks, machine/medic authorization, actors and holograms |
| Saves | Versioned detached state, safe checkpoint/restore, extraction tombstones, atomic desktop file store with corruption detection | UI, lifecycle wiring, trusted account receipts and recovery UX |
| Handoff | Candidate election, freeze/abort/ack/commit contract with revision and epoch | Endpoint transfer, authenticated ACK/digest, complete arena serializer, reconnect barrier, crash recovery |
| Progression | Three validated perk IDs per starting loadout | XP/account levels, completion receipts, achievements, unlock catalogs and actual perk effects |
| Mutators | Design recorded below | Roll/vote rules, catalog and implementation |

## Agreed game design

- Menu name: **Expedition**. A themed open world with dangerous buildings; an
  efficient full clear including the final world boss should take about one hour.
  Up to eight players. First theme: zombie city, Ward Zero plus two or three other
  buildings. Proposed Ward Zero boss is a Zombie Scientist. Moving/redesigning
  Siren is a separate content task; existing Siren is unchanged.
- The party enters a building together. No split indoor/outdoor simulation.
  An unbreakable entrance closes until that building's boss encounter is defeated.
  Meet a kill quota, then fight the boss; multiple boss entities all need to die.
  Buildings can be selected in any order. Clearing all opens the final outdoor
  boss area. Victory offers extraction or a harder loop retaining the build.
- Buildings remain active after the boss for an extended stay. A single living
  participant's departure vote starts a shared countdown; zero votes cancels;
  unanimous living votes skip the remaining time. Dead/disconnected players do
  not vote. Exit returns everyone together to the outdoor entrance, not directly
  into sanctuary.
- After the first building completion, living players can individually extract
  from the safe location while others continue. A group extraction countdown
  requires a strict majority of connected living participants and cancels below
  that threshold. Only consenting players depart; remaining players continue.
  Host departure first transfers authority to the best eligible connection.
- Each player has a wallet. Purchased chips belong to the buyer; ordinary drops
  are first pickup. Each chip selection belongs to one player. Boss eligibility
  freezes at the instant of death: one reward per living connected participant
  by default. A configured total is divided evenly; deterministic dice assign
  the remainder, including when fewer chips than survivors are configured.
  Boss offers should be Epic+ with authored boss-exclusive Unique effects.
- Dead pilots can be absorbed at the wrist and carried to a currency-paid
  reprint machine. A medic perk may permit field reprints. Unabsorbed pilots also
  need a recovery route. This milestone includes an optional remote recovery
  operation at sanctuary; its interaction and cost remain content decisions.
- Time difficulty persists across the open world, buildings and harder loops.
  Picking chips and sitting in sanctuary do not stop it. Loading, an empty
  session and a deliberate handoff freeze it. No offline elapsed-time penalty.
- Saves resume the last safe-location checkpoint, not an arbitrary combat frame.
  Drop-in players get catch-up selections once; accidental reconnect restores
  their existing build/death state. Successful extraction bars that account
  from rejoining this run, including later loops and checkpoint rollback.
- PvP and PvE completions both earn permanent XP, largely active play time and
  objectives (including optional objectives). Rewards include achievements,
  cosmetics and PvE perks. Three PvE perk slots can define roles, improve starting
  stats or influence chip offers. PvE perk stats must never enter PvP rules.
- Optional party slot-machine mutators can make runs beneficial, difficult or
  chaotic. Not required for the first playable slice; no reward multipliers or
  mutator voting policy have been committed.

## Authority API

Files are under `unity/ChipArenaConversion/Assets/ChipArena/Core/`.
`NativeExpedition.Create` takes stable account/world/instance IDs, seed, weapon and
instance quota definitions. The host owns one instance; all commands run on one
thread after authenticating the caller. Return `false` means no committed change.
`Snapshot()` returns a detached, full authority snapshot for persistence/handoff.
**It is not a public network frame.** It contains every player's chip offers.

| Presentation need | Authority source / command |
| --- | --- |
| World/building objective board | `phase`, `area`, `instances[].progress/target/cleared`, `FinalUnlocked` |
| Boss encounter ready/sealed door | `StartBosses`, `DefeatBoss`; door opens only when `cleared` |
| Departure headcount/countdown | `exitVotes`, connected/alive membership, `exitAt - elapsed`; `VoteExit` |
| Individual/group extraction | `CanExtract`, `VoteExtraction`, `extractAt`, `ExtractionDue`, `Extract` |
| Difficulty clock | `elapsed`, `loop`, `ExpeditionScaling.Evaluate` |
| Reprint marker/wrist carry | `members[].alive/carriedBy`; `Absorb`, `Reprint` |
| Personal chip panel | Only local-owner `rewards[].offers`; `Claim`, `Select` |
| Dice animation | Persisted `dice[].eligible/rolls/winners/each`; never roll again in presentation |
| Catch-up panel | `members[].catchup`; `IssueCatchup` creates a personal reward |
| Connection transfer overlay | `Migrating`, `PrepareHandoff`, `AbortHandoff`, `CommitHandoff` |
| Save confirmation | Confirm only after `NativeExpeditionSaveStore.Write` succeeds |

The integration layer must validate proximity, line-of-sight, actual kills,
machine/medic permission, costs, chest cooldowns, account unlocks and chip catalog
eligibility. Never accept client-authored snapshots, prices, arbitrary chip IDs,
boss deaths, dice or credit amounts. `Claim` accepts **host-generated** offers;
`Select` rechecks eligibility to handle concurrent pending selections. Use existing
`NativeContentRules` for caps/exclusions. `highQuality` is an offer-generation hint,
not implemented rarity enforcement. Final-boss rewards still need the encounter
adapter; `FinishFinal` currently changes the victory state only.

Send sanitized public run state and separate owner-only chip offers. Keep `Tick`
on the host; advance in steps up to 0.25 seconds, subdividing active-play deltas.
It does not automatically issue extraction rewards, destroy clients or save to
disk. `ExtractionDue` is a request to the integration transaction, not proof that
departure has been persisted.

## Save and transfer transactions

`ExpeditionSavepoint.Capture` requires the living connected party at sanctuary
and rejects loading/handoff. It saves the complete rules state, including chips,
wallets, death state, unclaimed rewards, dice, catch-up usage, loop and objectives.
`Restore(host)` returns that checkpoint with only the resume host connected,
votes cleared and a new authority epoch. Other saved players reconnect normally.
Current combat actors/projectiles are intentionally not part of a safe checkpoint.

For extraction, prepare the changed run on a detached copy, call
`checkpoint.WithExtractions(proposedState)`, and durably write before publishing
the changed live state or confirming permanent rewards. On write failure keep
the old authority and report failure. Extraction tombstones are separate from
checkpoint progress, so returning to an older checkpoint cannot bring an
extracted player back. Tombstones also cover newcomers absent from that checkpoint.

`NativeExpeditionPersistence.Open(runId)` provides a Unity JsonUtility codec and
uses the game's existing user-data directory, under `Expeditions`. The portable
store writes/flushes a temporary file then atomically replaces the primary with
a backup. Corrupt/missing primary files return no checkpoint; they do **not**
automatically restore an old backup that could lose extraction tombstones.
SHA-256 detects corruption, not cheating. Saves are not valid proof of account XP.
Atomic-replace/JsonUtility behavior still requires the Windows/Unity acceptance gate.

Host departure: elect from transport-verified reachable/capable peers, preferring
lower loss then latency+jitter. `PrepareHandoff` freezes the rules. Capture the
**rest of the live simulation** (enemies, projectiles, timers, pickups, scene,
positions, RNG streams and transport membership) alongside the returned rules.
Destination must durably store/verify the package, reserve the new endpoint and
ACK the transaction/digest/revision/epoch before `CommitHandoff`. Commit all leaving
accounts, persist their extraction tombstones and distribute the committed state
before releasing the old host. Timeout calls `AbortHandoff`; nobody has extracted.
The new host needs the committed snapshot, not the pre-commit proposal. Split-brain
fencing, reconnect barriers and sudden-host-loss recovery are still transport work.
Connection-quality ordering alone does not implement host migration.

## Provisional balance and integration gates

Ten-second vote timers, lower-median held-chip catch-up (maximum 256 selections),
and `ExpeditionScaling` defaults are tuning scaffolding, not final balance.
Health, damage and spawn-count multipliers are bounded to avoid numerical runaway;
party size does not directly multiply enemy damage. Apply encounter scaling at a
defined spawn/wave boundary so disconnects cannot heal or suddenly kill enemies.
The first playable director must decide the exact roster-lock policy.

Sanctuary must prevent incoming **and outgoing** damage, aggro pulling and pickups
across its boundary. Idle sanctuary time must not generate XP. Menus outside it
leave pilots vulnerable. These require simulation hooks, not just a UI flag.
Perks are chosen before the initial clock starts in this foundation; late-join
loadout selection still needs a staging step. No XP, achievement or account write
is wired yet. Use immutable account reward receipt IDs outside the run checkpoint
so resuming/replaying a checkpoint cannot farm the same completion reward.

DTOs deliberately bound member history (512) and event/reward history (16,384),
with 16 building definitions and 32 bosses per building. An endless-release build
needs durable history compaction; on hitting a bound, commands refuse additions.
Do not silently claim unbounded runtime from these initial safeguards.

Next programming slice: one greybox city/sanctuary and Ward Zero adapter, authored
spawn director, sanitized replication and scene barriers; then real drop-in,
transfer/reconnect and save transactions; then account XP/perks and additional
buildings/final boss. Visual work can start with the board, sealed door, departure
panel, personal chip UI, reprint machine and dice display against this contract.

## Presentation contract (Claude)

The screens read one projection per account, `NativeExpeditionView.Build(run,
account, sources)`. It holds the public run state plus only that account's
wallet, rewards (offers while a pick is pending) and chips. The same DTO is the
guest replication contract. Screens send intents through `IExpeditionCommands`
(host-local commands; guests unavailable until a command channel exists) and
confirm them only from the authority's state.

Fields, screens, the dev preview (`--ca-expedition-preview`) and the remaining
integration needs are in [`EXPEDITION-PRESENTATION.md`](EXPEDITION-PRESENTATION.md):
replication message, guest command channel, hooks, reprint cost, chest offers,
transfer progress, continue vote and world positions. Not playable; no menu entry.

## Verification

`Tests/ExpeditionChecks.cs`, invoked by the full managed suite or `--expedition`,
exercises 1–8 players, quota/boss gates, loops, cancellation/majorities, rewards,
deterministic dice, survivor timing, catch-up/reconnect/death, paid recovery,
checkpoint rollback/tombstones, disk integrity, handoff abort/commit and scaling.
These are rules tests, not Unity scene, actual network migration or visual tests.
