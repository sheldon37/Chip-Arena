# Expedition — presentation contract (Claude)

Integration bridge: [EXPEDITION-INTEGRATION.md](EXPEDITION-INTEGRATION.md) (Astra) records the runtime
entry points, replication, hooks and remaining scene/migration dependencies. Rules and transactions:
[`EXPEDITION.md`](EXPEDITION.md). This file covers what the screens read, what they send, and what
presentation still needs from the integration.

History: views, screens, intents and the dev preview on `claude/expedition-presentation` (base shared
`9270d95`); live binding, the new fields and the world pieces on `claude/expedition-live` (base shared
`3c836c4`, Astra's PR #207, merged with shared `09096af`), both 2026-10-14.

## Status

The screens draw in the real game **whenever a session is bound**: on the host after
`ConversionGame.BindExpedition`, on a guest from the paired `ExpeditionViewReceiver`. Nothing binds a
session yet and no menu entry exists, so Expedition stays unplayable until a scene and director exist.
The `--ca-expedition-preview` dev preview is separate, labelled DEV ONLY, and binds nothing.

The screens never change the run and never decide a rule. Every number comes from the view: wallets,
prices, quotes, allocations, dice rolls and winners, eligibility, votes, countdowns, cooldowns, transfer
progress and link measurements. A control raises an intent; it counts as done only when the view shows
it. Refusals, missing hooks, timeouts and unsupported operations say what they are and never read as
success. Presentation never rolls dice, allocates rewards, buys, claims or keeps its own economy.

## Files

Core files are engine-free; Runtime files draw with the cyber HUD kit (`NativeHudChrome`,
`NativeCyberTheme`, `NativeUi`).

| File | Role |
| --- | --- |
| `Core/NativeExpeditionView.cs` | `ExpeditionView` DTO and `Build` (Astra's projection), `Normalize`, `Leaks` (privacy audit, now also flags a world anchor of a private reward), `ExpeditionViewSources`, `ExpeditionTransferLog` |
| `Core/NativeExpeditionCommands.cs` | Intents, host commands, guest sender (an accepted reply shows as accepted, still pending), `ExpeditionIntentTracker` (state-driven confirmation) |
| `Core/NativeExpeditionPresentation.cs` | Screen models: board, objective, countdowns, roster, loot, boss shares, dice timing, extraction (honest host refusal), continue model and rows, caches, pick history, quotes, candidates, transfer steps, captions, withdrawn-vote captions |
| `Core/NativeExpeditionWorld.cs` | World pieces at real anchors, their labels (cached), culling, beacon-style geometry, the DEV greybox camera |
| `Core/NativeExpeditionPreview.cs` | DEV PREVIEW scenario driver (real authority; stand-ins below) |
| `Runtime/NativeExpeditionHud.cs` | Drawing: minimal HUD, board, loot (`Marker`), chips (with pick history), sanctuary (quotes), building (`Door`), victory (continue), caches, dice, transfer (candidates), transfer banner, world glyphs and labels |
| `Runtime/NativeExpeditionScreen.cs` | The shared screen layout (live and preview), the dice overlay, the DEV greybox view |
| `Runtime/NativeExpeditionLiveUI.cs` | Live binding: HUD in the scene, the J screen, intents through `ExpeditionCommands` |
| `Runtime/NativeExpeditionWorldPresentation.cs` | 3D world pieces (one dynamic mesh, `NativeBeacon` shader) and their projected labels |
| `Runtime/NativeExpeditionPreviewScreen.cs`, `Runtime/NativeExpeditionPreviewUI.cs` | DEV preview layout and its `--ca-expedition-preview` hook |
| `Tests/ExpeditionPresentationChecks.cs` | Managed checks (`CoreTests.dll --expedition-view`, also in the full suite) |

`Runtime/ConversionGame.cs` has five hook lines: the J/Escape keys, local play paused while the screen is
open (as the PvE chip screen does), the screen in `GuiFrame`, the in-scene HUD, and the world draw at the
start of `LateFrame`.

## Live binding

- **Source:** `ConversionGame.ExpeditionView` (host: rebuilt from the bound session; guest: the paired
  public + private frames). `ExpeditionCommands` sends: the host's session commands, or the guest channel.
  The live UI caches the command path per binding.
- **In the scene** (a live view, not a menu, intro or replay): the world pieces, the minimal HUD, a compact
  host-transfer line while a transfer exists, the dice reveal of a new stored record (not modal; it closes
  itself), and a `J · EXPEDITION` hint.
- **J** opens the Expedition screen (board, phase panel, chips, caches, loot; the dice reveal is modal
  there). J or Escape closes it. Victory opens it once per loop for the choice.
- **Guests:** Pending ("sent to the host") → Pending ("accepted by the host", from an accepted reply) →
  Confirmed only when the replicated view shows it. A refused reply shows Refused at once with its reason.
  No reply and no reflection within 6 s shows Failed. A Confirmed reply counts as an acceptance only.
- The tracker resets when the run changes. A confirmed vote the authority has since withdrawn reads
  "Replaced by your Continue choice" / "Replaced by your extraction vote" / "Withdrawn since".

## Intents and confirmation

| Intent | Host-local path | Confirmed when the view shows |
| --- | --- | --- |
| `VoteExit` / `CancelExit` | `VoteExit(account, yes)` | the viewer in / out of `exitVotes` (or the party has left that building) |
| `VoteExtraction` / `CancelExtraction` | `VoteExtraction(account, yes)` | the viewer in / out of `extractVotes` (or extracted) |
| `VoteContinue` / `CancelContinue` | `VoteContinue(account, yes)` | the viewer in / out of `continueVotes` (or the loop advanced) |
| `Extract` | `hooks.Extract` (save, then publish) | `mine.extracted && mine.extractionSaved` |
| `NextLoop` | `AdvanceWhenReady()` | `loop` increased |
| `Claim` | `hooks.Offers` then `Claim` | the reward `claimed` |
| `Select` | `hooks.Eligible` then `Select` | the reward `consumed` (or settled out of the view) |
| `Reprint` | `hooks.Reprint` (evidence) then `Reprint` | the target alive |

Guests send the same intents over `expedition-command`; the host runs them through the same path.

## Screens and the fields they read

- **Minimal HUD:** `mine.wallet`/`catchup`; objective from phase/instances; the running countdown
  (`exitAt`/`extractAt` against `elapsed`); "your chips waiting" from `mine.rewards`.
- **World board:** `instances[]`, final area, `elapsed`, `difficulty*`, roster (`members[]`, `continueVotes`).
- **Building:** `Door` (sealed/open), quota, bosses, departure vote.
- **Sanctuary:** `savedRevision`; extract alone (`mine.canExtract`; the host with peers gets the honest
  refusal: leaving needs a host transfer and this build cannot move the host, with the `HostTransfer`
  reason on the control); group vote (`extractVotes`, `extractNeeded`); reprint machine with each downed
  pilot's quote (`members[].reprintCost`: none shows "price unavailable", never $0, and its control stays off).
- **Caches** (town): each `chests[]` offer before purchase: price, `STOCK <generation>`, "RESTOCKING · Ns"
  only while `cooldown > elapsed`, "BOUGHT · YOURS" only for the viewer's own purchase of the current
  generation (`mine.rewards`, `...:chest:<generation>`), the wallet's shortfall, and whether the cache has an
  anchor here. Range is never claimed (the view carries none). Buying is not a control: no intent exists.
- **Your chips:** wallet, rewards by source, Open → Pick, **picked this loop** (`mine.rewards[].selected`,
  the viewer's only), held chips.
- **Loot:** ordinary drops (cyan diamond, first pickup) against reserved boss chips (magenta lock), NOT
  ELIGIBLE per boss whose record does not list the viewer.
- **Dice reveal:** stored `dice[]` only (spins, locks on stored rolls, lights stored winners). The world
  boss's record (`<loop>:<world>:final:boss`) is named WORLD BOSS and goes through the same loot, dice and
  world screens; the victory panel adds a world-boss loot line.
- **Victory:** extract alone and the group vote; **Continue** (`continueVotes`): n / remaining ready, the
  button (downed pilots can agree), "everyone remaining must agree", the waiting reason when everyone
  agreed but nobody is alive, and each pilot's choice (CONTINUE, CONTINUE · DOWN, DECIDING, VOTED TO EXTRACT,
  DOWN · CAN STILL AGREE, EXTRACTED / OFFLINE · NOT COUNTED). Continue and the extraction vote replace each other.
- **Host transfer:** elect and freeze (authority); reserve endpoint (`endpointReserved`), store package
  (`packageStored`), destination ACK (`ackReceived`, "deadline set" from `deadline`) — done only as reported
  and only with `endpointMigration`, otherwise "Endpoint migration is not supported in this build"; commit or
  abort. Candidates: RTT, jitter and **ping timeouts** (`loss` is timed-out pings, not packet loss);
  `reachable`/`capable` false read "NOT PROBED" / "NOT SUPPORTED YET". A link with no answered ping says so.

## World pieces

Built by `NativeExpeditionWorld.Build(view)` for the anchors whose `area` is the view's `area`:

| Piece | Anchor | Look | Lines |
| --- | --- | --- | --- |
| Ordinary drop | reward anchor of a public `monster` loot entry | cyan diamond (3D octahedron), light column while open | FIRST PICKUP · ORDINARY DROP · ANYONE; YOURS; TAKEN · BY X |
| Reserved boss chip | reward anchor of a `boss` loot entry | magenta chip tile with shackle; struck danger tile when the viewer is a member not in the record | YOURS / FOR X / NOT ELIGIBLE; RESERVED, CLAIMED, OPENED · PICK A CHIP; boss name |
| Door | `door` anchor; building = instance named by the anchor's `area` (inside), else by its `id` (outdoor entrance) | ring + column, HUD `Door` hologram (sealed/open) | SEALED · QUOTA p / t or BOSS d / n DOWN; OPEN; ENTER TOGETHER · QUOTA t; UNFINISHED |
| Reprint machine | `machine` anchor | pedestal, ring, column while someone is down | REPRINT MACHINE · NAME · $quote or PRICE UNAVAILABLE · +n MORE DOWN / CARRIED BY X |
| Chip cache | `chest` anchor with an offer of the same id | crate, ring, column while buyable | $price · CHIP CACHE · STOCK G; RESTOCKING · Ns; BOUGHT · YOURS; $n SHORT |

- A missing anchor, an anchor in another area, a reward not in the view (picked, private, unknown), a door
  with no building and a chest without an offer draw nothing. There is never a guessed origin.
- A boss's chips share its death point: they stand in a small ring round it and one label at the anchor
  speaks for the cluster ("YOURS × 2 · ALSO FOR NOVA, ASTRA", "NOT ELIGIBLE · FOR ...").
- Claimed ordinary drops show dimmed (taken); picked rewards disappear. No piece is local-authoritative:
  claiming goes through the commands.
- Drawing: one dynamic mesh with the revive beacon's shader and motes, in the theme palette; the HUD glyph
  only for doors (anchors carry no facing). Labels: projected, sized by depth, hidden behind walls,
  within 26 m, decluttered nearest first. Pieces are culled past 90 m. Reduced effects: no spin, bob or
  mote scroll. Building, culling, geometry and labels reuse buffers and cache strings: no allocation per
  frame for a steady view (checked).
- Anchor `y` is treated as the floor under the piece.

## Dev preview

Start the player with `--ca-expedition-preview` ("DEV ONLY · NOT A GAME MODE", in place of the main menu).
Scenarios: sanctuary, caches and first building; boss loot and dice; extraction and reprints; victory
(world-boss dice, continue votes); host transfer; guest commands. View as any pilot; **Greybox** shows the
current area's pieces at their anchors from a fixed camera with the live labels.

Preview stand-ins, labelled on screen: offers, prices, machine quotes (60 carried, 90 remote; one pilot
without a quote), a temporary save folder, the greybox anchors and cache offers, a loopback guest channel
(the real `ExpeditionGuestCommands` sender and reply path with 0.35 s latency and views published every
0.5 s; one scripted lost message), and simulated transport progress (endpoint reserved, package stored,
deadline, ACK). The transfer scenario first shows today's honest state: measured links, nothing probed, the
host's request refused.

## What presentation still needs from Astra

1. **Scene/director binding** (already Astra's list): create and bind the session, `SetWorld` anchors and
   offers, `DropAt`/`DefeatBossAt`/`FinishFinalAt` positions, `CanClaim`, `ReprintEvidence`, chest restocking,
   checkpoints. The scene's pickup interaction must send `ExpeditionIntent.Claim` (target = reward id)
   through `ConversionGame.ExpeditionCommands`; no proximity prompt is drawn without a scene.
2. **Purchase intent.** No `ExpeditionIntent` buys from a cache. Wanted: `Purchase` (target = chest id),
   validated by the scene's proximity and the current offer, then `NativeExpedition.Purchase(account,
   offer.generation, offer.price)`. Until then the caches panel says buying is not connected.
3. **Sold state.** Another pilot's purchase is private, so others see a bought generation as unsold until
   the scene restocks. Either restock (new generation + cooldown) on purchase, or add a public
   `ExpeditionChestOffer.sold` (no buyer).
4. **Door anchors.** Confirm the convention above, or add `ExpeditionWorldAnchor.building`. A facing
   (`yaw`) would let doors draw 3D leaves instead of the HUD hologram.
5. **Private reward anchors.** `PlaceReward` accepts chest and catch-up reward ids, and `Build` publishes
   every anchor, revealing a private reward's id. Project only anchors of public loot (or refuse them in
   `PlaceReward`). `Leaks` flags it; the world never draws it.
6. **Display names.** `members[]` carries account ids only; guests cannot map them to seat names. Add a
   public display name per member.
7. **Transfer deadline clock.** `deadline` has no documented clock (the authority clock is frozen during a
   handoff); the overlay says "deadline set" without a countdown until it is defined.
8. **Probes and migration** (Astra's list): reachable/capable probes, endpoint migration, package/ACK.
9. **Performance notes:** the host rebuilds the full view every frame and the host `ExpeditionCommands`
   getter allocates per call; the world pieces themselves allocate nothing per frame.
10. Optional per-account ineligibility reasons (unchanged).

## Verification

`CoreTests.dll --expedition-view` (also in the full suite) checks, among the earlier coverage:

- guests over the real reply path (sender → wire → `ExpeditionCommandRouter` on a real session → reply,
  views through `ExpeditionViewBatch` and the paired receiver): refused at once, accepted still pending,
  confirmed only by the replicated view, failed on timeout (also when accepted but never reflected), a
  replaced command's reply ignored, a Confirmed reply treated as an acceptance; the preview loopback too;
- continue: downed consent, all remaining must agree, a living pilot required, extracted/offline not
  counted, continue and extraction votes replacing each other with honest captions;
- caches: price, stock, cooldown countdown against `elapsed`, restocking from the data only, the buyer's
  private purchase, never "range";
- reprint quotes: none, zero and negative read "price unavailable", never $0;
- world-boss dice equal to the stored record for several splits, with the loot list and world pieces;
- transfer: unprobed candidates refused with the reason, nothing fabricated without endpoint migration,
  only reported steps done, unmeasured links;
- world pieces only at real anchors of the area, the look per reward kind and eligibility, clusters, a
  private anchor never drawn, geometry validity, reduced effects static, culling, and no allocation per frame;
- every preview step for every viewer: leak-free, codec round trip, pieces at real anchors, history private;
- source scans: palette only, no authority reference or mutator call in presentation, no randomness, no
  session binding, no menu entry.

Offline renders (scratch recorder plus a small software rasterizer of the same geometry; not committed):
screens, greybox and 3D world pieces at 1280x720 to 3440x1440. Not run: Unity Editor/IMGUI input,
Windows, real multiplayer clients, the 3D pieces in the engine.
