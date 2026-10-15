# Expedition — presentation contract (Claude)

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

**Acceptance pass** (`claude/expedition-acceptance` on PR #217's `3183514`, 2026-10-15). The checklist was played through
Astra's real session and director, with host and guest views decoded from the wire (`CoreTests --expedition-acceptance`).
It covers seals, binding and transfer, SAFE tags, boss arrivals, Continue into loop 2, and the town's clearance and budget.
Four presentation defects were found and fixed:
- The final gate's beacon and FINAL GATE OPEN were timed from the gate's opening. The party is inside the last building
  then, so they were never seen. The cue is now held until the client is back in town.
- Continue's transfer screen said ENTERING THE CITY. It now says STARTING LOOP n.
- Placements (the new loop's sanctuary slots, respawns) rippled the sanctuary edge as if pilots had crossed it.
- The airlock's pockets and header reached 0.12 m and 0.06 m into the doorway that collision frees.

World pieces also wait for the arena to hold the bound area's map. These are managed checks and offline renders only.
Unity play, Windows and real host/guest runs are blocked until the scene adapter exists (DEV_LOG).


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
| `Core/NativeExpeditionStates.cs` | Closure phase math, the presentation clock, cue memory (gate beacon, boss intros, sanctuary crossings), the layout rig, and the transfer, SAFE and boss models |
| `Core/NativeExpeditionStateScene.cs` | Door, final gate, sanctuary and boss-crack geometry: kit boxes (NativeBlockLit) and effect quads (NativeSirenFx) |
| `Runtime/NativeExpeditionStatesPresentation.cs` | Draws the scene (instanced kit, one effects batch); layout binding; the LateUpdate, in-scene HUD and transfer-screen hooks |
| `Runtime/NativeExpeditionStatesHud.cs` | Transfer screen, SAFE chip and SAFE tags, boss intro and plates (with health), final gate notice |
| `Tests/ExpeditionStatesChecks.cs` | Managed checks (`CoreTests.dll --expedition-states`, also in the full suite) |
| `Core/NativeExpeditionPick.cs` | Chip picks: reward rules, header, waiting counts, the tray's flow (Claim / OpenChoice / CloseChoice / Select), choosers from `members[].choosing` |
| `Runtime/NativeExpeditionChipScreen.cs` | The Boss Test's chip tray over the live view, its follow-through, input routing, the CHIPS tag and the wrist-look hook |
| `Tests/ExpeditionPickChecks.cs` | Managed checks (`CoreTests.dll --expedition-pick`, also in the full suite) |

`Runtime/ConversionGame.cs` has these hook lines: the J/Escape/C keys (`ExpeditionLiveKeys`), local play
paused while the J screen is open, the J screen in `GuiFrame`, the in-scene HUD (which draws the chip tray), and
the world draw at the start of `LateFrame`. For chip picks there are also the Boss Test's C kept out of an
Expedition, Alt freeing the cursor (`look` and mouse look) and `ExpeditionPickRoute()` after the play input.

## Live binding

- **Source:** `ConversionGame.ExpeditionView` (host: rebuilt from the bound session; guest: the paired
  public + private frames). `ExpeditionCommands` sends: the host's session commands, or the guest channel.
  The live UI caches the command path per binding.
- **In the scene** (a live view, not a menu, intro or replay): the world pieces, the minimal HUD, a compact
  host-transfer line while a transfer exists, the dice reveal of a new stored record (not modal; it closes
  itself), and a `J · EXPEDITION` hint.
- **J** opens the Expedition screen (board, phase panel, chips, caches, loot; the dice reveal is modal
  there). J or Escape closes it. Victory opens it once per loop for the choice. Chips are not picked on J:
  its chip panel lists the waiting rewards and a **C · CHOOSE CHIP (N)** line that closes J and opens the
  chip tray (below).
- **C** opens the chip tray (see "Chip picks").
- **Guests:** Pending ("sent to the host") → Pending ("accepted by the host", from an accepted reply) →
  Confirmed only when the replicated view shows it. A refused reply shows Refused at once with its reason.
  No reply and no reflection within 6 s shows Failed. A Confirmed reply counts as an acceptance only.
- The tracker resets when the run changes. A confirmed vote the authority has since withdrawn reads
  "Replaced by your Continue choice" / "Replaced by your extraction vote" / "Withdrawn since".

## Chip picks (the Boss Test's tray over the live view)

Owner 2026-10-15: "We should also utilize the same chip selection screen (That we utilize in the Boss Test)
instead of selecting on the J menu." Owner direction 2026-10-15 supersedes PR #234: choosers are held in place and remain vulnerable. So the
pick is the Boss Test's chip tray (`PveChipScreen`'s cards via `DrawTray`: entrances, holo highlight, number
badges, hover cue; `DraftKeys`) drawn **over the live view**, docked at the bottom centre between the health box
and the cooldowns. There is no 3D draft stage and the arena is never hidden. The Boss Test path itself is
unchanged.

- **Opening.** C (or the J panel's CHOOSE CHIP line) opens it on the viewer's next waiting reward: a claimed one
  with offers first, else an unopened one (reserved boss chip, catch-up, unopened purchase). The tray sends
  `Claim` for that one and shows it when its offers arrive. Collecting a reward never opens it (the contract):
  the Boss Test's **CHIPS N** tag with its C key counts it in the scene, in place of the minimal HUD's tag.
- **Choosing presence.** Showing a claimed reward sends `OpenChoice(reward)` unless the view already shows the
  viewer choosing; the host keeps it across queued picks and clears it after the final Install plus `NativeDraftPlant.ReturnEnd` (1.95 s), a death, disconnect, save resume, a load or an
  extraction. A load ends with `OpenChoice` again. Closing it (C, Escape, Choose later, or the tray closing
  itself) sends `CloseChoice` once while the view still shows choosing. A refused or unconfirmed `Claim` or
  `OpenChoice` closes the tray with a notice.
- **Picking.** 1–9, arrows and Enter, or a click while Alt is held, sends `Select(reward, chip)` through
  `ExpeditionLiveSubmit`. A follow-through runs on the same pieces (the others shatter, the chosen card drops
  out of view). It reads "Installing" until the view shows the reward consumed, then "Upgrade secured", with the
  tracked caption while a guest waits. A refusal returns to that reward's tray with its caption. The next
  waiting reward follows, as the Boss Test opens its next queued pick. The install cue is the authority's
  `Install` event.
- **Header.** Built from the reward's source and kind; Expedition offers have no tier, so a boss reward reads
  "BOSS REWARD · <boss> · EPIC OR BETTER" in the epic tone and others use the neutral tone. YOU ARE VULNERABLE
  WHILE CHOOSING, N MORE WAITING (the viewer's other unpicked rewards), the key line and Choose later. Under
  attack, the Boss Test's blinking border and alarm show over the header. Its full-screen red wash is kept faint
  (12% blinking, 6% steady with Reduced effects), so the fight stays readable.
- **Input (client routing; host enforces the hold).** While the tray is drawn, digits 1–9 pick a chip, so the abilities
  they would start are dropped. Holding Alt frees the cursor to click a card; mouse look, attack and block
  pause only while it is held. The host cancels and blocks movement/actions and refuses machine interaction while choosing, preserving camera rotation. Damage and the world/difficulty clock continue. An explicit close releases an unfinished tray; an automatic close after the final pick cannot shorten its plant.
  Without Alt the locked cursor never hovers or clicks a card.
- **Wrist look and SELECTING CHIP tag.** These come from `members[].choosing` for every pilot, as the Boss Test
  reads each row's replicated open pick (`ArenaPresentation.ExpeditionChoosing`). Another pilot's hologram shows
  three cyan cards; the offers stay private. Mine carries my offers' rarities, and in first person it steps aside
  while my tray is on screen.

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
| `OpenChoice` / `CloseChoice` | `SetChoosing(account, open, reward)` (host hold; no immunity) | the viewer's `members[].choosing` set / clear |
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
- **Your chips:** wallet, waiting rewards by source (READY · N OFFERS or UNOPENED), C · CHOOSE CHIP, **picked this loop** (`mine.rewards[].selected`,
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
| Door | `door` anchor; building = instance named by the anchor's `area` (inside), else by its `id` (outdoor entrance) | ring + column; the 3D leaves where the door has them (below), else the HUD `Door` hologram (sealed/open) | SEALED · QUOTA p / t or BOSS d / n DOWN; OPEN; ENTER TOGETHER · QUOTA t; UNFINISHED |
| Reprint machine | `machine` anchor | pedestal, ring, column while someone is down | REPRINT MACHINE · NAME · $quote or PRICE UNAVAILABLE · +n MORE DOWN / CARRIED BY X |
| Chip cache | `chest` anchor with an offer of the same id | crate, ring, column while buyable | $price · CHIP CACHE · STOCK G; RESTOCKING · Ns; BOUGHT · YOURS; $n SHORT |

- A missing anchor, an anchor in another area, a reward not in the view (picked, private, unknown), a door
  with no building and a chest without an offer draw nothing. There is never a guessed origin.
- A boss's chips share its death point: they stand in a small ring round it and one label at the anchor
  speaks for the cluster ("YOURS × 2 · ALSO FOR NOVA, ASTRA", "NOT ELIGIBLE · FOR ...").
- Claimed ordinary drops show dimmed (taken); picked rewards disappear. No piece is local-authoritative:
  claiming goes through the commands.
- Drawing: one dynamic mesh with the revive beacon's shader and motes, in the theme palette; the HUD glyph
  only for doors without 3D leaves this frame. Labels: projected, sized by depth, hidden behind walls,
  within 26 m, decluttered nearest first. Pieces are culled past 90 m. Reduced effects: no spin, bob or
  mote scroll. Building, culling, geometry and labels reuse buffers and cache strings: no allocation per
  frame for a steady view (checked).
- Anchor `y` is treated as the floor under the piece.

## Doors, gate, sanctuary, transfer and boss arrival

Claude, `claude/expedition-states` (shared base `6da0330`), 2026-10-14; bound to PR #216's fields on
`claude/expedition-states-bind`. All of it reads Astra's director state in the public view: `closures[]`, `areaBarrier`,
`bossArrivals[]` (with `x/y/z`, `hp/maxHp`) and `members[]` (`safe`, `seat`). It also reads the static manifest
`Resources/ExpeditionLayout.json`, which every client holds. Host and guests read the same fields, so they draw the same thing.
Nothing here writes the view, calls the director or touches collision.

**Binding.** The client parses the manifest once and computes its fingerprint: `NativeExpeditionLayout.Compatibility`, the
manifest bytes plus the content revision. The rig of the view's area is used only when `areaBarrier.layout` equals that
fingerprint (`NativeExpeditionStates.Bound`). The director publishes it as soon as it binds the layout, on the initial
inactive barrier (generation 0), so the doors, gate and sanctuary draw from the run's start. The 3D pieces draw only while
the arena holds that area's map (`NativeExpeditionStates.MapReady`). With a bound layout, so do the world pieces and their
holograms. The view names the next area before the scene loads it, and nothing of that area may stand in the previous map.
Anything missing draws nothing:
- no fingerprint, or a different one (the world pieces keep their anchor fallback);
- another map loaded;
- a door without a closure record. Its HUD `Door` hologram stays as the fallback.

**Clock.** Closure `began` and boss `spawned` are on the authority's run clock (`elapsed`). The presentation clock is the last
view's `elapsed`, run forward by local time between views (at most 0.25 s, because views arrive at 20 Hz). It holds still while
`loading` or `migrating`, never steps back by a sliver, and starts over for a new run.

| Piece | Reads | Look |
| --- | --- | --- |
| Door leaves | each closure whose id is a door anchor of the area; the closure volume (size, yaw) for the doorway plane; the approach for the party's side | An airlock unit at the closure volume: pockets either side, a header, a hazard-striped sill and jamb bands, status lamps and signs in the state colour (green open, amber moving, red sealed), and a dark airlock behind with a teal light. The pockets and header frame the closure face from outside it, and nothing solid stands in the open doorway the collider frees. Ward Zero's exit jambs and lintel bound exactly that face. Two leaves meet in the doorway plane and slide into the pockets. Ward Zero's portico and exit are biocontainment doors: clinical white leaves, hazard edges, a teal band and dark portholes. `Shut` is 0 when open, 1 when sealed, and eases over exactly 1 s from `began` while sealing or opening. Three lock bolts engage over the last 15% of sealing and draw back over the first 15% of opening. |
| Door seal | `Blocking` (state is not `open`: the collider's own predicate) | A hardlight seal over exactly the closure face: fill, bright border, slanted hazard hems, scanlines and a sweep. Amber while sealing or opening (it flashes on at the start of sealing), red while sealed. It stays up through the whole opening second and is gone the moment the view says `open`, so nothing looks open while it blocks. After that, sparks drift off for 0.5 s. |
| Final gate | the gate's closure (`exp-city:gate:final`) and its volume, facing the sanctuary | A steel portcullis that fills the gatehouse opening. Opening: lock clamps draw back into the towers over the first quarter, then the gate rises over the rest of the second into the lintel (sealing reverses this). A magenta hardlight lattice stays up while it blocks. During opening, cells dissolve with white flashes but at least a fifth remain, plus the frame and fill, until `open`. |
| Gate beacon | this client saw the gate go from sealed or sealing to opening or open, or first saw it opening. Timed from `began` if the client is in town then. The gate opens when the last building is cleared, with the party inside it, so that opening is held. It plays 0.5 s after the client is next in town with no barrier up, in step on the arrival's authority time. | A magenta pillar rising 70 m off the gatehouse, three ground rings and a face flash for 7 s, and the HUD notice FINAL GATE OPEN · THE FINAL AREA IS UNLOCKED for 4 s. A gate first seen open (a resume or late join) cues nothing. Sealing again (a new loop) drops a held cue. |
| Sanctuary boundary | the area's sanctuary volumes; the phase | A warm floor edge (a bright core fading inward), a low curtain with a slow wave, warm rising motes and corner glows. A shimmer ripple and ring appear where a pilot crosses (presentation's own position test; it never grants SAFE). A placement is not a crossing: sides are forgotten while a barrier is up, and a move of more than 3 m between two samples (a placement or respawn seen late) only records the side. Lit only in the Town and Victory phases, the ones in which the authority grants `safe`; otherwise a dim cool outline. The low tier keeps the floor edge; reduced effects stop the wave and motes. |
| SAFE chip | `mine.safe` (member, connected, not extracted); `members[].safe` for the party line | SAFE · SANCTUARY under the wallet, and PARTY n / m SAFE with up to three names. It is the authority's sanctuary membership (checkpoint, extraction, remote reprint). It never claims damage protection and is never derived from a position. |
| SAFE tags | `members[].seat` of each member with `safe`, connected, alive, not extracted (`ExpeditionSafeTags`); seat 0 is unbound | SAFE (green tick and diamond, no shield) over that pilot, in the slot above the overhead name: within 55 m, hidden behind walls (the health bars' ray), faded past 35 m, never over my own pilot in first person (`ExpeditionSafeTags.Alpha`). Public: every viewer sees the same tags. |
| Transfer screen | `areaBarrier` (active, target, ready, required); `loading`; `instances[].entered`; `loop` | Full screen over every screen, with the notice toast and build label kept on top: AREA TRANSFER · LOADING; ENTERING WARD ZERO for an instance target; RETURNING TO THE CITY for the world once a building was entered this loop. For a later loop's fresh barrier (Continue reloads the city into itself) it says STARTING LOOP n; otherwise ENTERING THE CITY. One pip per member the barrier waits for (up to 8), PARTY READY n / m, a bar, the paused-clock note and a loader. It fades in over 0.25 s and holds after the barrier drops until the arena holds the area's map (at most 3 s), then fades out over 0.45 s. |
| Boss intro | `bossArrivals[]` of the view's area, once per arrival, timed from `spawned` | BOSS ARRIVAL · area tag, the archetype's name (test archetypes keep TEST), a rule and a 0.6 s glitch split, and a short sting: a magenta pulse at the screen edges. It never moves the camera or covers the crosshair. 3.4 s. An arrival first seen more than 3 s after its spawn gets no intro. |
| Boss plates | the same arrivals; `hp/maxHp` from the view's current record; the building's `defeated`/`cleared`, the final-boss phase, or `hp` 0 | One plate per boss: BOSS, the name, a health bar and percentage (a living boss never reads 0%), DOWN once defeated. A legacy record (`maxHp` 0) has no bar and reads ACTIVE. A plate waits for its own intro; late joiners see only the plate. |
| Boss crack | the arrival's `x/y/z` (only on a record with `maxHp` above 0, the current contract); its intro flag; `spawned` | Cracks, roar rings, dust and a flash at the recorded spawn point for 2.6 s from `spawned`, once per arrival and only for arrivals seen within 3 s of their spawn. The pattern's seed is a stable hash of area and id (FNV-1a), so every client draws the same cracks. |

Names: every screen drops a layout area id's `exp-` prefix (`NativeExpeditionPresentation.Name`), so the imported
`exp-ward-zero` reads WARD ZERO in the objective line, the board and the banners.

Drawing: the kit boxes are NativeBlockLit instances, lit by the arena look and casting shadows, in chunks of 500; the hardlight,
glow and dust are one NativeSirenFx effects batch. Doors and the gate are culled past 140 m, sanctuary segments past 90 m. The
clock, cues, scene and HUD models allocate nothing per frame (checked); HUD strings are cached.

Space the door unit takes, so other kit stays clear of it: in the closure frame, |u| up to W + 0.3 (7 m wide for a 3.2 m
doorway), 0 to H + 0.55 high, and from the wall face (−D/2 + 0.01) to 0.3 m in front of the closure, sill 0.6 m. Inside the
closure face (|u| < W/2, above the 3 cm sill, below H, from 10 cm off the wall face) it draws nothing solid once open. The town
portico is x 43.4–44.0, z −3.5..3.5, y 0–4. The Ward Zero exit is x −37.95..−37.5, z −26.9..−19.9, and encloses the overlay's
jambs and lintel.

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
11. ~~Member to pilot binding~~: done. PR #216 `members[].seat`; SAFE tags over the pilots on `claude/expedition-states-bind`.
12. ~~Boss position~~: done. `bossArrivals[].x/y/z`; the crack and roar draw there.
13. ~~Boss health~~: done. `bossArrivals[].hp/maxHp`; the HUD boss plates show it. The combat adapter must call
    `UpdateBossHealth` for the bar to move.
14. ~~Layout fingerprint before the first load~~: done. `areaBarrier.layout` from the layout binding; the state pieces bind
    from the run's start.
15. ~~Chip choosing presence and Install on Select~~: done (PR #233, superseding #234: host hold through the final plant). The chip
    tray binds them on `claude/expedition-chip-screen`.
16. Optional, still open: Boss Test offer parity for Expedition picks (implemented / Siren filter, tier weights, money
    chips, stale-offer reroll; offers carry no tier today, so the tray's header names the source instead), and tiered
    physical pickups dispensed by the Ward machines (the Boss Test's walk-over chip) instead of a direct claim.

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

`CoreTests.dll --expedition-states` (also in the full suite), against the real director: closure phases and the 1 s
transitions (monotonic, exact at `began` and `began + 1`, bolts, the gate's unlock then rise); the presentation clock (lead
cap, frozen while loading or migrating, no slivers back, run reset). Doors through the director's real entry and clear: the
seal covers exactly the closure face from the first instant of sealing through the whole opening, and is gone at `open`. The
leaves part by the shut amount, cover the whole doorway when shut, sit in the pockets when open, and stay inside the closure
depth. No closure record, no rig and another area's rig each draw nothing. The gate: lattice up through opening, a monotonic
rise, the beacon timed from `began`, no cue for a gate first seen open. SAFE only from `mine.safe` and `members[].safe`:
crossings make ripples, never chips; non-members and strangers get none; the final-boss phase dims the boundary. Transfer
text and counts from the barrier, return vs first entry, display clamping and the fade. One intro per arrival over 500
frames, intros timed from `spawned`, late joiners get only the plate, a new loop replays and other areas play nothing.
Binding: the startup view (generation 0, inactive) and a guest's decoded copy bind; another or no fingerprint does not. Seats:
a tag per safe member's seat, the same for every viewer, none for seat 0, unsafe, downed or disconnected members, and a
replaced seat moves the tag. Boss: the crack at the recorded point from `spawned`, identical on another client, none for late
joiners, legacy records or another area's rig; plate health and percentage from `UpdateBossHealth`, never 0% alive, DOWN at
zero on defeat with max kept, no bar on a legacy record. The imported manifest's doors and gate get rigs facing their approach. No allocation over 240 frames.
Source scans: no randomness, no authority reference or mutator, no writes to view state, no collision, palette-only HUD.

Offline renders (scratch recorder plus a small software rasterizer of the same geometry; not committed):
screens, greybox and 3D world pieces at 1280x720 to 3440x1440. Not run: Unity Editor/IMGUI input,
Windows, real multiplayer clients, the 3D pieces in the engine.


## Astra follow-up contract — 2026-10-14

Implemented on `codex/expedition-live-integration`, based on Claude `22c14c2`:

- `ExpeditionIntent.Purchase` takes the chest id in `target`. The host resolves
  generation and price from the persisted offer, rejects cooldown/sold/wrong-area
  caches, and calls `CanPurchase(account, chest)` before charging. The callback
  must measure actual scene proximity; missing evidence refuses. Guest payloads
  never choose price or generation. Transaction retries return the original reply.
- `ExpeditionChestOffer.sold` is public. The buyer and reward remain private.
  `RestockChest(id, freshGeneration, price, availableAt)` is a scene-authority
  event; availability uses gameplay `elapsed`. Reused generations are refused.
  Purchase tracking retains the generation at submission, so a later restock
  does not hide confirmation. Sold caches display as unavailable to other pilots.
- `ExpeditionWorldAnchor.building` explicitly identifies the building. When
  absent, the existing inside `area` / outside `id` convention remains supported.
  `yaw` is degrees around world +Y, zero facing +Z; world pieces carry it through.
  Claude owns rendering door leaves from this field.
- Public projection filters private and unknown reward anchors, including source
  overrides and legacy saves. `PlaceReward` now rejects private reward ids.
- Members carry `name`, sanitized/bounded display text supplied by the host from
  account/verified-seat profiles. Identity and authorization still use account id.
  Live control labels use this name; an absent name falls back to the account id.
- `transfer.deadline` and `transfer.clock` use host monotonic transport seconds.
  Both are -1 when absent. Countdown at receipt is max(0, deadline-clock); a UI
  may subtract local monotonic time since receipt, never compare deadline directly
  with its own clock, UTC, or `view.elapsed`. Transport time continues while rules
  are frozen. The preview uses its independent Clock. No real migration timeout
  is claimed while endpointMigration is false.
- `BindExpeditionScene` installs authored anchors/offers plus claim, purchase,
  reprint-evidence and quote callbacks, then binds the durable session.
  `InteractExpedition(rewardId)` sends Claim through ExpeditionCommands;
  `InteractExpedition(chestId, true)` sends Purchase through the same path.
  These are scene integration entry points, not a new scene/director or input map.
- `ExpeditionCommands` returns a stable object per binding. Projection and link
  metrics run at most 20 Hz, including offline; rules continue ticking per frame.
  Idle extraction checks read elapsed without cloning the entire snapshot.

Still absent: an authored playable city/instance director, its real positions,
combat callbacks, pickup input, machine channels and restock schedule; actual
candidate endpoint probes, durable combat package transfer, destination ACK,
endpoint migration/reconnect and split-brain fencing. Existing link RTT/jitter/loss
are measured; reachable/capable remain false. The menu remains closed. Native
Unity compilation, Windows rendering and two-client play remain acceptance gates.

## Expedition state field handoff — 2026-10-14 (Astra)

On `codex/expedition-view-fields`, based on shared `768ebc9`, the public view now provides:

| Field | Meaning |
| --- | --- |
| `members[].seat` (`int`) | Current `NativeSeat.id`; `0` means no bound pilot. Resolve it against arena pilots, retaining the existing `safe`/connected/extracted checks. |
| `bossArrivals[].x`, `.y`, `.z` (`float`) | Immutable world spawn position, suitable for the arrival crack and roar. The existing `id` remains the encounter id, not a runtime entity id. |
| `bossArrivals[].hp`, `.maxHp` (`double`) | Latest authority health. Death through the director publishes zero HP and preserves max HP. A legacy frame with `maxHp == 0` has no health information. |
| `areaBarrier.layout` | Validated compatibility fingerprint as soon as the director binds the layout, including the initial inactive barrier (generation 0). No area load is needed. |

`BindExpedition` resolves seats from the current match's authenticated account mapping on every projection.
An unbound engine-free session may supply `Sources.Seat`; absent mappings publish zero.
The combat adapter must call `RecordBossArrival(id, archetype, knownArchetype, spawn, hp, maxHp)` after spawning,
and `UpdateBossHealth(area, id, hp, maxHp)` after damage/healing/scaling, before projection. Registration now requires
real position and health rather than inventing them; all repository callers are updated. Health updates leave
`spawned` and the position untouched. `BossDefeated` and `FinalDefeated` set the corresponding recorded HP to zero.
Existing 20 Hz public-view serialization includes source-only changes even when the rule revision is unchanged.
These fields are public for all viewers, including late joiners; private rewards are unchanged.

Claude: bind the SAFE tags, arrival effects and boss bar to these fields. The presentation is not changed here.
The authored playable scene/combat adapter and its session binding remain separate unfinished integration work;
this change does not claim Expedition is playable.


## Chip screen authority contract — 2026-10-15

For `claude/expedition-chip-screen`, use the existing authenticated `ExpeditionCommands` channel:

- On opening a pending personal pick, send `new ExpeditionCommand(ExpeditionIntent.OpenChoice, rewardId)`. The target is optional (any claimed, unconsumed personal reward then suffices). Opening another account's reward, an empty queue, or opening during loading is refused.
- On closing the chip screen (C, Escape, cancellation, or UI teardown), send `new ExpeditionCommand(ExpeditionIntent.CloseChoice)`. This is idempotent and may close during loading. Do not open choosing merely because a reward was collected or the J board is visible.
- Read `view.Member(account).choosing` / `view.members[].choosing` for the wrist hologram. This is a public boolean only: offers, wallet, held chips and machine/catch-up rewards remain private. Existing guest request run/epoch/authentication and transaction replay protections apply.
- Owner correction: choosing is informational only. Do not freeze pilots or cancel movement, attacks, abilities, block, roll or machine interaction because the chip screen is open. Pilots remain vulnerable to ordinary damage; enemies, teammates, difficulty and run clock continue. The chip screen must not add a choosing-based gameplay input lock. Current selection remains open across queued picks; consuming the final claimed reward clears the flag automatically. Close, death, disconnect, extraction, area loading and save resume clear it.
- Successful `Select` emits exactly one ordinary arena `EventKind.Install`, with the selecting pilot as owner/target, current position, chip id, and resulting stack count. Refused/repeated selections emit none. Inventory synchronization and snapshot restoration do not replay installs.
- Notices now say C for chip choices; J still opens the Expedition board. The C key and screen/hologram bindings belong to Claude's presentation branch and were not present on shared when this authority patch was authored.

Optional Boss Test offer parity (implemented/Siren filtering, tier weights, money chips, stale-offer reroll) and tiered physical machine pickups remain deferred. Existing Expedition offer/economy rules are unchanged.
