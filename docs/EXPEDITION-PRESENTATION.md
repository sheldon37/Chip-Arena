# Expedition — presentation contract (Claude)

Integration follow-up: [EXPEDITION-INTEGRATION.md](EXPEDITION-INTEGRATION.md) records
the new runtime bridge, updated commands/fields and remaining scene/migration
dependencies. It supersedes the older missing-integration statements below.

Presentation side of the Expedition PvE mode, recorded 2026-10-14 on
`claude/expedition-presentation` (base shared `9270d95`, which contains Astra's
rules foundation from PR #188). The rules and transactions are in
[`EXPEDITION.md`](EXPEDITION.md). This file covers what the screens read, what
they send, and what the integration still has to provide.

## Status

The screens, the projection they read and the intent path are built and checked
against the real `NativeExpedition` authority. **No game mode uses them yet.**
Nothing enables an Expedition menu entry. A dev-only preview (below) shows them
over scripted scenarios.

The screens never change the run and never decide a rule. Every number comes from
the authority: wallets, prices, allocations, dice rolls and winners, eligibility,
countdowns and the scaling read-out. A control raises an intent. The intent counts
as done only when the authority's state shows it. Refusals, missing hooks and the
missing guest channel say what they are and never read as success.

## Files

All Core files are engine-free; Runtime files draw with the existing cyber HUD kit
(`NativeHudChrome`, `NativeCyberTheme`, `NativeUi`).

| File | Role |
| --- | --- |
| `Core/NativeExpeditionView.cs` | `ExpeditionView` DTO and `NativeExpeditionView.Build` (single-account projection), `Normalize` (receiver side), `Leaks` (privacy audit), `ExpeditionViewSources`, `ExpeditionTransferLog` |
| `Core/NativeExpeditionCommands.cs` | `ExpeditionCommand`, `IExpeditionCommands`, host adapter (real authority commands), guest adapter (unavailable), `ExpeditionIntegrationHooks`, `ExpeditionIntentTracker` (state-driven confirmation) |
| `Core/NativeExpeditionPresentation.cs` | Screen models: board, final area, objective line, countdowns, roster, loot rows, boss shares, dice reveal timing, extraction model, transfer steps, command captions |
| `Core/NativeExpeditionPreview.cs` | DEV PREVIEW ONLY scenario driver (real authority, preview stand-ins for offers, prices and the save transaction) |
| `Runtime/NativeExpeditionHud.cs` | Drawing: minimal HUD, world board, loot (with `Marker`), personal chips, sanctuary, building (with `Door`), victory, dice reveal, host-transfer overlay |
| `Runtime/NativeExpeditionPreviewScreen.cs`, `Runtime/NativeExpeditionPreviewUI.cs` | Preview layout and its `--ca-expedition-preview` hook (one line in `ConversionGame.GuiFrame`) |
| `Tests/ExpeditionPresentationChecks.cs` | Managed checks (`CoreTests.dll --expedition-view`, also in the full suite) |

## The view (replication contract)

`NativeExpeditionView.Build(run, account, sources)` runs where the authority lives
(the host). The DTO is field-only, so JsonUtility and the managed codec both
round-trip it. Everything except `viewer` and `mine` is the same for every
viewer (checked). It can therefore be sent as one public frame plus an owner-only
`mine` block.

| Field | Source |
| --- | --- |
| `revision`, `epoch`, `run`, `world`, `area`, `host`, `phase`, `loop`, `elapsed`, `loading` | Snapshot |
| `exitAt`, `extractAt`, `exitVotes[]`, `extractVotes[]` | Snapshot (authority clock times; compare with `elapsed`) |
| `exitSeconds`, `extractSeconds` | `NativeExpedition.ExitSeconds` / `ExtractSeconds` (countdown bar length) |
| `migrating`, `finalUnlocked`, `extractionUnlocked` | `Migrating`, `FinalUnlocked`, `ExtractionUnlocked` |
| `living`, `extractNeeded` | Connected living count; strict majority `living/2+1` (as `RefreshVotes`) |
| `difficulty`, `difficultyParty`, `difficultyStep` | `ExpeditionScaling.Evaluate(elapsed, party, loop, phase==Instance)`. `party` is the connected, not-extracted count (provisional until the director's roster lock) |
| `savedRevision` | `sources.Saved.checkpoint.revision` (the last durably written savepoint) |
| `members[]` | account, weapon, connected, alive, safe, extracted, carriedBy, host, exitVote, extractVote. No wallet, chips or perks |
| `instances[]` | id, target, progress, entered, cleared, current, bosses[], defeated[] |
| `loot[]` | Ordinary drops (`monster`, owner = whoever took it) and reserved boss rewards (`boss`, owner = survivor, `boss` = dice key). Owner and claimed/consumed only, never offers. Open ones, plus those settled in the current loop and area |
| `dice[]` | Copies of the current loop's `ExpeditionDice` (eligible, rolls, winners, each) |
| `mine` | Viewer only: wallet, catchup, chips, perks, alive/safe/connected/extracted, carriedBy, `carrying[]`, `canExtract` (`CanExtract`), `extractionSaved` (tombstone present in `sources.Saved`), `rewards[]` of any source (offers only while claimed and not consumed) |
| `transfer` | `active` (`Migrating`), stage (Frozen / Committed / Aborted / Refused), handoff ids from `sources.Transfer`, transport `candidates[]`, `endpointMigration` |

Rules the builder enforces: another account's offers, wallet, chips, purchases and
catch-up rewards never appear in any view. A commit is reported only when the
authority shows the new host at the new epoch. An abort is reported as a failure.

Receivers call `NativeExpeditionView.Normalize` after decoding: JsonUtility turns
absent strings into `""`. The screens also treat `""` as absent (checked over
every preview step).

## Intents and confirmation

| Intent | Host-local path | Confirmed when the view shows |
| --- | --- | --- |
| `VoteExit` / `CancelExit` | `VoteExit(account, yes)` | the viewer in / out of `exitVotes` (or the party has left that building) |
| `VoteExtraction` / `CancelExtraction` | `VoteExtraction(account, yes)` | the viewer in / out of `extractVotes` (or extracted) |
| `Extract` | `hooks.Extract` (the save-then-publish transaction) | `mine.extracted && mine.extractionSaved`. Extracted but unsaved stays "waiting for the save" |
| `NextLoop` (host only) | `NextLoop()` | `loop` increased |
| `Claim` | `hooks.Offers` then `Claim` | the reward `claimed` |
| `Select` | `hooks.Eligible` then `Select` | the reward `consumed` (or settled out of the bounded view) |
| `Reprint` | `hooks.Reprint` (cost, remote flag) then `Reprint` | the target alive |

`false` from the authority shows **Refused**. A missing hook shows
**Unavailable** with its reason. A guest shows **Unavailable** (no guest command
channel). An accepted send with no reflection in 6 s shows **Failed**.

## Screens

- **Minimal HUD:** wallet (top left, as the PvE money panel), objective tag (top
  centre, `Objective`), the running countdown under it (`Departure` or
  `GroupExtraction`: seconds, bar, votes), and a magenta "your chips waiting" tag.
- **World board:** each building's state (open / sealed door with quota / boss /
  cleared / unfinished), the final area (locked n/m, unlocked, engaged, defeated),
  the clock, step, loop and the HP / damage / count multipliers, and the party
  roster.
- **Building:** sealed or open `Door`, quota bar, bosses down or alive, and the
  departure vote (always shown; disabled until the door opens).
- **Sanctuary:** checkpoint saved (revision), extract alone (`Extraction` model;
  the host's control explains the transfer), the group extraction vote (votes,
  majority needed, countdown), and the reprint machine (downed pilots, carried or
  not).
- **Personal chips:** wallet, catch-up, each owned reward labelled by source
  (purchased, boss reserved, picked up, catch-up), Open → Claim, the viewer's own
  offers → Select, and the held chips.
- **Loot:** ordinary drops (cyan diamond, first pickup, taken by whom) against
  reserved boss chips (magenta lock, yours or another survivor's, claimed or not).
  A NOT ELIGIBLE row appears for each boss whose stored record does not list the
  viewer.
- **Dice reveal:** total, survivors, even share and dice extras (`ShareLine`).
  Dice spin with no number, lock in order on the stored rolls, then the stored
  winners light (+1). A tie at the cut is labelled as the host's seeded tie-break.
  An even split shows no dice.
- **Victory:** extract or stay, with each pilot's choice. The host starts the
  harder loop; others are told there is no continue vote yet.
- **Host transfer:** elect (authority), freeze (authority), store package and
  destination ACK (transport: "Not implemented in this build"), then commit or
  abort with the reason, and the transport's candidates.

## What the integration still has to provide (Astra)

1. **Replication.** A message carrying the built view per account, on revision
   change. Guests keep the newest epoch/revision and drop older ones. Send the
   public part once and `mine` only to its owner; the split is safe because the
   public part is identical for every viewer.
2. **Guest command channel.** A message `{transaction, intent, target, chip}`
   authenticated to the sender's account. The host runs it through the same path
   as `ExpeditionHostCommands` with the hooks, and replies `{transaction, state,
   reason}` so refusals show at once. Confirmation still comes from the next view.
3. **Hooks** (`ExpeditionIntegrationHooks`): catalog offers (Epic+ for bosses);
   `NativeContentRules` eligibility; the durable extraction transaction (pass the
   written savepoint as `sources.Saved`); and a reprint adapter (cost, range,
   channel, medic/machine permission).
4. **Missing fields.**
   - `reprintCost` per downed member or machine, so the price shows before the press.
   - World chest offers `{generation, price, cooldown}`, so purchases can be shown
     before they happen. Today only recorded purchases are shown.
   - `ExpeditionReward.selected` (the chosen chip id), for pick history.
   - Transfer progress from the transport: `packageStored`, `endpointReserved`,
     `ackReceived`, `deadline`. Plus `endpointMigration = true` once it exists.
   - Measured candidates (`ExpeditionHostCandidate`: loss, latency, jitter,
     reachable, capable) in `sources.Candidates`.
5. **Continue choice.** There is no continue vote; `NextLoop` is host-only. A
   per-pilot "ready for the next loop" display needs `continueVotes[]` or an
   equivalent command and policy.
6. **World pieces.** Rewards, chests, doors and machines have no positions. 3D
   pickups need a spawn position per reward id and anchors for doors and machines.
   The looks are already defined: `NativeExpeditionHud.Marker` and `Door`.
7. **Final-boss rewards.** `FinishFinal` issues none. Once the encounter adapter
   adds them as dice-keyed boss rewards, the same loot and dice screens apply.
8. **Ineligibility detail.** The dice record lists survivors only. The screen says
   "not alive and connected when it fell". Telling down, disconnected and not yet
   joined apart would need per-account reasons.

## Dev preview

Start the player with `--ca-expedition-preview`. The preview replaces the main
menu and is labelled "DEV ONLY · NOT A GAME MODE". It plays six scenarios step by
step:

- the town and first building;
- boss loot with survivors and dice;
- extraction, alone and as a group;
- victory;
- host transfer;
- guest view and refusals.

You can view as any pilot. Viewing as the host sends real host commands; any
other viewer is a guest.

Preview stand-ins: offers come from a fixed chip list, wallet credits and prices
are fixed, and saves go to a temporary folder deleted on exit. The host-transfer
commit step uses a simulated ACK and says so. Exit preview returns to the menu.

## Verification

`CoreTests.dll --expedition-view` (in the full suite too) checks:

- host and guest views; privacy on every view;
- the codec round trip, including JsonUtility-style decoding;
- refused, unavailable, timed-out and awaiting-save states never read as success;
- departure and pick confirmation edge cases;
- dead-at-death boss eligibility;
- uneven splits from 2/1 to 8/11, where the dice display equals the stored record;
- independent picks;
- individual extractions while others continue, the strict-majority group vote
  and a failed save;
- transfer freeze, abort, refusal and an unreflected commit;
- every preview step for every viewer;
- source scans: palette only, no authority reference in the screens, no
  randomness, no menu entry.

Offline renders came from a scratch IMGUI recorder; they are not committed. Not
run: Unity Editor/IMGUI input, Windows, multiplayer clients.
