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
| `Runtime/NativeExpeditionChipScreen.cs` | The chip cards drawn on my wrist screen (the Boss Test's tray pieces through the screen's canvas maps; the 2D tray where the screen cannot be read), the follow-through, input routing and the chip cursor, the CHIPS tag, the wrist-look and wrist-plant hooks and the plant's install cue |
| `Core/NativeChipScreen.cs` | The wrist chip screen: its place, unfold and fold, cards, a pick's flying card, the canvas maps, the cursor hit test, the hit cue and its light budget (`NativeChipScreenLight`) |
| `Resources/NativeChipScreen.shader` | The screen's one-quad look (glass, grid, scanlines, rim, frame line, rules, card slots) and its rays, ring and motes; alpha-blended, under the bloom |
| `Runtime/NativeChipScreenPresentation.cs` | Builds every Expedition chooser's screen in place of the small wrist panel into its own mesh; my screen's state for the HUD and the chooser camera |
| `Tests/ChipScreenChecks.cs` | Managed checks (`CoreTests.dll --chip-screen`, also in the full suite) |
| `Core/NativeChipPlant.cs` | The wrist plant after each pick: its timeline (the Boss Test plant's), cue and pose layer over the wrist-hologram pose |
| `Tests/ExpeditionPickChecks.cs` | Managed checks (`CoreTests.dll --expedition-pick`, also in the full suite) |
| `Core/NativeChooserCamera.cs` | The chooser camera: the swing round to a front three-quarter shot while I am held choosing, its framing, collision fallback and hand-offs |
| `Runtime/NativeChooserCameraPresentation.cs` | Applies it after the aim and before the shakes while the camera is held; restores the field of view |
| `Tests/ChooserCameraChecks.cs` | Managed checks (`CoreTests.dll --chooser-camera`, also in the full suite) |

`Runtime/ConversionGame.cs` has these hook lines: the J/Escape/C keys (`ExpeditionLiveKeys`), local play
paused while the J screen is open, the J screen in `GuiFrame`, the in-scene HUD (which draws the chip tray), and
the world draw at the start of `LateFrame`. For chip picks there are also the Boss Test's C kept out of an
Expedition, the chip cursor freeing the cursor with no Alt (`look` and mouse look), `ExpeditionPickRoute()` after the play input, and
`ExpeditionPlantAudio()` after `PveFollowAudio()` in `LateFrame` (my plant holds my Install cue for its seat). The
chooser camera adds `ChooserCamera()` in `LateFrame` after the aim (`ConvergeAim`) and before the shakes,
`ChooserCameraRestore()` first in `UpdateCamera`, and `ChooserFramed` on the crosshair and on my own overhead health
bar (`NativeCombatHud.cs`) and SELECTING CHIP tag (`NativeChipSelectHud.cs`).

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
instead of selecting on the J menu." Owner direction 2026-10-15 supersedes PR #234: choosers are held in place and remain vulnerable
("Can we change the chip selection to just pressing C and it'll be us in a "Chip Selection" animation? I purposely wanted to make
people vulnerable when doing this." / "(Like we had for ward zero, but can keep the open view. I just want players to stay in place
while doing this.)" / "Still make them put their chip in their wrist after selected, then they are free to move."). So the
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
- **Picking.** 1–9, arrows and Enter, or a click on a card (no Alt), sends `Select(reward, chip)` through
  `ExpeditionLiveSubmit`.
  - **Follow-through.** It runs on the same pieces for the plant's span (`NativeChipPlant.Span`, 1.95 s). The others
    shatter on my wrist screen. The chosen card flies off it into my hand and shrinks to the chip as the hand takes it
    there: the plant's contact (.62 s), at the plant's chip projected onto the screen's canvas. On the 2D tray it flies
    into my wrist hologram (`ArenaPresentation.ChipSelectFocus`, the view model's in first person), or toward the hands
    below the bottom edge.
  - **Status.** It reads "Installing" until the view shows the reward consumed, then "Upgrade secured", with the tracked
    caption while a guest waits. A refusal returns to that reward's tray with its caption.
  - **Queue.** The next waiting reward follows, as the Boss Test opens its next queued pick.
  - **Install cue.** My `Install` event is held for the plant's seat (`NativeInstallCue` at `NativeDraftPlant.SoundAt`, as
    the Boss Test's stage plant does). Another pilot's plays as it arrives.
- **Header.** Built from the reward's source and kind; Expedition offers have no tier, so a boss reward reads
  "BOSS REWARD · <boss> · EPIC OR BETTER" in the epic tone and others use the neutral tone. YOU ARE VULNERABLE
  WHILE CHOOSING, N MORE WAITING (the viewer's other unpicked rewards), the key line and Choose later. Under
  attack, the Boss Test's blinking border and alarm show over the header ("C closes the chips to move and fight"). Its
  full-screen red wash is kept faint (12% blinking, 6% steady with Reduced effects), so the fight stays readable.
- **Input (client routing; host enforces the hold).** While the tray is drawn, digits 1–9 pick a chip, so the abilities
  they would start are dropped. Owner 2026-10-15: "The mouse should be taken over for the chip selection, we shouldnt
  have to hold alt to click." While the camera is held (`ExpeditionCameraHeld`, Astra's PR #241) and my screen or its
  follow-through is up, the cursor is unlocked and shown (`ExpeditionPickPointer`), clicks pick the card under it and
  mouse attack/block go to the tray; Alt changes nothing. Camera yaw/pitch and lock-on tracking freeze for the entire
  choosing hold, through the final wrist plant. When the hold ends the cursor re-locks and mouse look returns after the
  re-lock settles (two frames, so the cursor's travel is not read as a turn). The host cancels and blocks
  movement/actions, preserves the held orientation and refuses machine interaction while choosing. Opening in the air
  cancels upward momentum and holds horizontal position while gravity settles the pilot onto real ground; downward
  speed is retained through the tray and final plant. Actual freeze/scare effects still suspend them. Damage and the
  world/difficulty clock continue. An explicit close releases an unfinished tray; an automatic close after the final
  pick cannot shorten its plant. A locked cursor never hovers or clicks a card.
- **Wrist look and SELECTING CHIP tag.** These come from `members[].choosing` for every pilot, as the Boss Test
  reads each row's replicated open pick (`ArenaPresentation.ExpeditionChoosing`). In an Expedition the raised wrist
  projects the chip screen (below) in place of the small panel. Another pilot's screen shows three blank cyan cards;
  their offers and count stay private. Mine carries my offers' rarities. In first person the chooser camera swings
  out to show it on my body (below); the first-person look shows only while the camera is still at the eye. A damaging hit flinches the look
  (`NativeChipSelect.Flinch`): it yields up to 45% to the route's pose, so the hit reaction shows through. The screen's edge
  turns red for .45 s (below); the small wrist hologram (first person, the Boss Test) blinks red as before.
- **Wrist plant** (`Core/NativeChipPlant.cs`; docs/PVE-CHIP-SELECT.md "Expedition wrist plant"). Each pick plays it on
  the Boss Test plant's timeline:
  - The chosen card leaves the folding panel as the chip.
  - A hand presses it into the cuff in third person. With a long weapon, and in first person for every class, the display
    wrist takes it as it drops.
  - In first person, while my tray or its follow-through is up, my wrist eases beside the tray instead of under its cards
    (`NativeChipSelectPose.TrayWrist`). The greatsword and bow stay under it, as their view arms cannot reach.
  - The cuff pulses, and the arms return.
  - The authority's final hold ends with it (Install + `NativeDraftPlant.ReturnEnd`).
  - **Mine** runs from my pick, so the tray closing never drops it. It stops only if that pick is refused or fails. It
    returns to the hologram when another of my rewards follows on the tray (`ArenaPresentation.ExpeditionPlant`).
  - **Another pilot's** runs from the authority's `Install` event for them. Their flag reads choosing through the final
    plant as well, so a queued pick cannot be told from a final one. Their look lowers after each plant, then rises
    again after 0.5 s if they are still choosing.
- **Wrist chip screen** (owner 2026-10-15: "...we should also make it a holographic screen projected from the wrist.
  That way its easier to notice were selecting a chip"; `Core/NativeChipScreen.cs`,
  `Runtime/NativeChipScreenPresentation.cs`). In an Expedition the 2D bottom tray gives way to a world-space screen.
  The Boss Test keeps its tray and stage.
  - **Place.** A beam fans from the display-wrist emitter into a 1.06 × .52 m hardlight screen. It floats .74 m ahead,
    .42 m out to the side and .36 m over the body centre, turned 30 degrees toward the other front quarter, where the
    camera stands. The side is the display wrist's (`NativeChipScreen.OnLeft`). The rifle, the machine gun and the
    greatsword hold that wrist across the body in both hands, so their screen floats on the side the wrist is held.
  - **Look** (rebuilt 2026-10-15 for "Holographic display kind of buggy, and display is odd. Can we make it like a better holographic
    display with no artifacting?"; `Resources/NativeChipScreen.shader`). The screen is one quad drawn by its own shader: dark glass with a
    faint grid and scanlines, a soft inner rim and fresnel sheen, a thin bright chamfered frame line with corner brackets, header and footer
    rules on the HUD's bands, and the card slots inside it. Nothing is stacked or coplanar on it. Thin rays run from the emitter ring to its
    corners (two to its ends while it is a sliver), with a few motes, all behind it and drawn first; a pick's flying card is drawn last, in front.
    - Alpha-blended, premultiplied (`Blend One OneMinusSrcAlpha`), no depth write, one pass and one queue (`Transparent+2`): drawn again it
      converges on its own colour instead of adding up. The scene fog fades it out.
    - Light is budgeted in linear HDR under NativeBloom's threshold of 1 (`Core NativeChipScreenLight`; the shader's `CS_*` numbers are the
      same and the check reads them): the body's summed light is .72 at most (.93 over a lit wall at .5), so it never blooms. Only the thin
      frame line (about 2 px), its brackets, the emitter ring and a lit card's rim go over 1, as a soft glow.
    - Lines thinner than a pixel fade instead of breaking up; the grid and the scanlines fade out before they would alias. No flicker once
      open (the old 37 rad/s flicker and glitch dips are gone).
    - Under the HUD's faces each slot is a dim frame just outside the face in its rarity; a blank back (anyone else's) has a cyan rim and an
      emblem.
    - **Hit cue.** A damaging hit on that pilot (the live events, `NativeEventFeedback.Attacked`, as the flinch reads them) turns the frame
      line, brackets and rim red: full at the hit, gone .45 s later (`NativeChipScreen.Alert`), no blinking, never the glass or the cards.
    - The wrist plant's chip and the cuff's pulse still come from the wrist hologram's mesh (`NativeChipSelect.shader`, unchanged; the Boss
      Test keeps that hologram).
  - **Open and close.** It unfolds out of the wrist: its centre travels out of the emitter, the width opens first, then
    the height, with a short flicker (none with Reduced effects), and a scan line sweeps down revealing the cards as they are dealt. It folds
    back the same way when the look lowers, and with each pick's fold.
  - **Cards.** The HUD draws the real faces (`DrawTray` / `NativeChipCard`, badges, hover lift, holo scan) over my
    screen, mapped through three canvas bands (header, cards, footer), each fitted to where the camera projects its
    four corners. This keeps the faces within 1% of the frame width of the world cards; the slot under each face is a dim frame
    just outside it. The header (eyebrow, Choose a chip, YOU ARE VULNERABLE WHILE CHOOSING, N MORE WAITING, status), the key
    line (1–N PICK · CLICK A CARD · C OR ESC CLOSES) and Choose later sit on the screen. IMGUI maps the cursor back
    through the same matrix, so the card under the cursor is the one picked.
  - **Fallback.** Until the screen faces the camera with its cards at least 20% of the width wide, no faces are drawn
    (keys still work). If the settled camera still cannot read it (a corridor or closet left only a shot from behind),
    the 2D tray shows instead.
  - **A pick.** The chosen card flies off the screen into the hand by the plant's contact (.62 s), bowing toward the
    viewer and shrinking to the chip. The others fade (`NativeChipPlant.Fade`), and the screen folds into the wrist
    (`NativeChipPlant.Fold`) as the chip seats (1.14 s). It opens again for a queued pick. The timing is the Boss Test
    plant's (.62 / 1.14 / 1.95 s). Mine flies on the HUD's faces; another pilot's flies blank on their screen.
  - **First person.** The chooser camera swings out to the third-person shot, so the screen is seen in the world, not
    projected at the eye. The first-person wrist panel stays for the Boss Test.
- **Chooser camera** (owner 2026-10-15: "Lets swing the camera to us when choosing,"; `Core/NativeChooserCamera.cs`,
  `Runtime/NativeChooserCameraPresentation.cs`). The shoulder camera showed only my back, so while I am held choosing
  the camera swings round to face me and my wrist screen. Presentation only: no input, look or hold changes.
  - **When.** While the camera is held (`ExpeditionCameraHeld`, Astra's PR #241: the view's public choosing flag,
    through the final plant, alive). It stays through every queued pick and the final plant, and eases back at the
    release (C or Escape closing the choice, the final plant ending), on death or extraction.
  - **Swing.** .42 s each way (smoothstep). The path orbits my axis from the gameplay camera (every Settings · Camera mode)
    to the front shot, one way round for the whole swing. Each way is reversible mid-swing. At weight 0 nothing is
    applied, and `UpdateCamera` rebuilds the camera from the unchanged look and restores the field of view first, so
    gameplay resumes exactly where it would be.
  - **Shot.** A front three-quarter view from the other front quarter from my screen (`NativeChooserCamera.SideOf`),
    24 degrees off my facing, 50 degree field of view, looking 7 degrees down. It frames my crown, my display wrist
    (eased), my far shoulder and the open screen's corners, backed off until they fit 78% across and 74% up the frame.
    The cards' row then spans 38–47% of the width (4:3 to 21:9, standing or bowed).
  - **Turning.** Astra's hold keeps my yaw and pitch, so I do not turn while choosing. If the facing still changes,
    the shot follows it (eased at 9/s) and stays in front. The look pitch does not move it.
  - **Collision.** The shoulder rigs' sphere cast (radius .12, padding .2). The shot takes the first clear angle of
    24, 48, 80, the same on the other side, straight on, then behind either shoulder (125). It keeps a working choice
    for .4 s, eases between choices, and shortens the distance when nothing is clear. Every frame the camera is cast
    from the point it orbits and pulled in at once (eased back out at 12/s), so it never enters a wall, fence, gate or
    door. When the middle of the swing's way round is less than 60% clear and the other way is clearly clearer, it
    swings the other way, so it does not squeeze past my head.
  - **First person.** The camera leaves through the front of my face and turns round. The view model is off while swung,
    and my body is drawn once the camera is .45 m from the eye, while it is still out of view. First person returns
    when the swing ends.
  - **Hits.** Damage, the screen's red edge, the flinch and the damage screen are unchanged. The one camera shake applies
    to the camera shown, after the swing, and the first-person hit kick is off while my body is shown. The damage screen's
    directions follow the camera shown.
  - **HUD.** The crosshair and my own overhead health bar and SELECTING CHIP tag are hidden under the shot (other pilots'
    are unchanged). The cards are on my screen, not over me.
  - **Hand-off.** Death or extraction eases back onto the death camera (the same rig). A cutscene, a spectated pilot, a
    new area, or any screen that skipped the live camera (the killcam, loading) drops the swing at once.
  - Other pilots' view is unchanged.

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
| Door leaves | each closure whose id is a door anchor of the area; the closure volume (size, yaw) for the doorway plane; the approach for the party's side | The town's doors (an instance's exit is the laser door, below). An airlock unit at the closure volume: pockets either side, a header, a hazard-striped sill and jamb bands, status lamps and signs in the state colour (green open, amber moving, red sealed), and a dark airlock behind with a teal light. The pockets and header frame the closure face from outside it, and nothing solid stands in the open doorway the collider frees. Ward Zero's exit jambs and lintel bound exactly that face. Two leaves meet in the doorway plane and slide into the pockets. Ward Zero's portico and exit are biocontainment doors: clinical white leaves, hazard edges, a teal band and dark portholes. `Shut` is 0 when open, 1 when sealed, and eases over exactly 1 s from `began` while sealing or opening. Three lock bolts engage over the last 15% of sealing and draw back over the first 15% of opening. |
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

### The laser door (instance exits)

Claude, `claude/instance-door-lock` (base shared `dfb8aac`, merged with `598c41b`), 2026-10-17. Owner: "When entering an
instance, lets animate the door behind us getting locked (In this case, the laser door powering on) when its unlocked, we can
animate that too." Presentation only (`Core/NativeExpeditionLaserDoor.cs`, `Core/NativeExpeditionStateScene.Laser.cs`,
`Runtime/NativeLaserDoorPresentation.cs`, `Runtime/NativeLaserDoorAudio.cs`); the closure, its collider and every rule stay
the director's.

**What drives it in the live run.** Every live instance (Ward Zero `exp-ward-zero`, Central Station `exp-transit`, City
Ironworks `exp-foundry`) has one exit, `<area>:door:exit`, in its map's own portal (metal jambs at ±1.8 m and a lintel at
3.4–3.8 m; the closure volume is the 3.2 × 3.4 × 0.4 m opening). The party is placed at that door's entries, 1.4–4 m in front
of it and facing away: the door is behind them. The split-area director keeps an instance area in its Instance phase from the
run's start, so its exit is `sealed` (`began` 0) until `instances[].cleared`, then `opening` for one second, then `open`. There
is no closure transition when the party arrives. So:
- **Lock:** from this client's arrival in the area (`ExpeditionStateCues.LockAt`): the first view in which it is presented
  there (no barrier, its own `members[].pendingArea` clear, and the runtime's `ArrivalHeld` off while the stage loading screen
  is up or the arena lacks the map). The lock starts `ArriveDelay` (0.5 s) later and takes the director's one second. Until
  then the door stands by: the field sheet and the floor beam. A door this client saw `sealing` is not locked again.
- **Exact paths:** `sealing` plays the same lock from its `began` (the single-area director; any future arrival-driven
  closure); `opening` plays the unlock from its `began`, and the floor beam goes out only when the view says `open`, the
  moment the collider releases.
- **Teammates:** one arriving in my area well after me (not the party arriving together) re-seals the door: a floor-up pulse.

| Stage | Look |
| --- | --- |
| Always | Emitter columns on the portal's front (a lens per beam on the inner face, a charge rail, a status lamp and vent fins), the header over the lintel with its display, the sill (3 cm, its emitter groove flush) and the dark recess. Nothing solid inside the closure face. |
| Lock (1 s) | The charge climbs the rails; each beam fires as the charge reaches it, floor first and the top at 0.78 s, with a snap-on flash, a spark burst and a 45 ms travel across. LOCKING (amber, blinking) becomes SEALED at 0.84 s: the word slams on, the chevrons converge and the face flashes. Status lamps amber, then red. |
| Sealed | Nine beams (five on low graphics): a white-hot core in glow layers and a soft halo, humming (a fine shimmer, a rare dropout, a slow scan pulse climbing them). They light the floor in front and in the recess, the jambs, the emitters' faces, the lintel's underside and the recess wall. Haze glows drift along them and motes glint as they cross. SEALED with a shut padlock and chasing chevrons. |
| Unlock (1 s) | Every beam stutters for 0.16 s, then they go out top-down, each after its own stutter; the rails drain; each emitter cools from an ember. UNLOCKING, the padlock's shackle lifts, the chevrons slide apart. |
| Open | No beam, no sheet. The emitters finish cooling over 1.6 s, the lamps and OPEN go green, a soft green light on the floor. |

The field sheet is one faint premultiplied quad over exactly the closure face at the seal depth, drawn for as long as the
closure blocks (sealing, sealed, opening, any unknown state), so the door never looks passable while it blocks. Palettes:
Ward Zero red containment, Central Station teal, City Ironworks amber (the join circles' accents are the glow); any other
instance a cool cyan. Low graphics: five beams, no halo, haze, motes, sparks, fins or wash. Reduced effects: no flicker,
stutter, flash, travel or sparks (beams fade). The HUD shows ENTRY SEALED · *zone* · SEALED UNTIL CLEARED as it locks and
EXIT UNSEALED · *zone* CLEARED as it opens, for 3.2 s.

Sound keys (the owner generates them; until then the fallback plays): `expedition.door.lock` (shield), `expedition.door.lock.snap`
(electric-hook-hit, at SEALED), `expedition.door.unlock` (electric-hook), and the loop `expedition.door.hum` (the hazard kit's
built-in synthesized beacon hum) on one low-priority voice while beams stand. Like the join keys, they need registering in
`NativeSoundKeys` before the bank can carry their clips.

DEV panel, Camera tab (under the station and Ironworks previews): **Instance exit door preview** (`preview-door-lock`) cycles lock, sealed (3 s),
unlock and open (2 s) on the current instance's exit, or on an instance map loaded in the sandbox. It draws a stand-in
closure in place of the record; the record and its collider are untouched.

For an exact, shared lock (optional, Astra): seal the exit at the arrival on the authority clock. Today each client times the
lock from its own presentation; two clients arriving together see it within their view latency of each other.

### Quota markers (the last enemies)

Claude, `claude/quota-last-markers` (base shared `e13e610`), 2026-10-18. Owner: "We need some kind of indicator for the last
enemies of a quota (Like maybe last 5?) In case they get stuck so players know where to find them." Presentation only
(`Core/NativeQuotaMarks.cs` holds every rule, `Runtime/NativeQuotaMarksPresentation.cs` draws, `Runtime/NativeQuotaMarkAudio.cs`
pings); nothing reaches the authority.

- **When:** the viewer's current instance (`phase` Instance, `instances[].current`, its own `area`) is not cleared, has no boss
  yet (`bosses[]` empty and no `bossArrivals[]` entry for the area) and 1–5 kills remain (`target - progress`,
  `NativeQuotaMarks.Threshold`). Ward Zero, Central Station and City Ironworks all use these fields, so one rule covers them.
  Off at 0 (quota met), in the boss phase, outside the instance (hub, another area), while loading or frozen for a host
  transfer. Wave Defense is not covered: its waves end on a host-only spawn budget that guests do not receive.
- **Which:** every live hostile horde add of the presented arena (a split area's arena holds only its own adds), the 8 nearest
  the viewer (`Cap`). Never corpses, hub/town zombies, the zone boss or its summons, DEV monsters, or adds still queued at an
  entry. Positions are the adds' presented roots (`ArenaPresentation.HordeDrawnRoot`, the smoothed root every peer poses).
- **World marker:** a downward kite 0.6 m above the kind's head (the Hulk's 3.06 m capsule: 3.66 m above the floor; a
  crawling add over its crawl capsule), in the HUD's enemy tone (Danger `#ff4d6d`, apart from the hazards' amber and the
  allies' cyan), drawn over the scene so walls never hide it, with its distance in metres. 28 UI px within 6 m easing to 15 px
  by 48 m; a 1.2 s pulse (alpha .72–1, size +12%). Hidden within 4 m while the add is in plain sight (a ray from the camera to
  its chest through `NativeArena.WorldMask`), back by 6 m; behind a wall it keeps its marker at any range. Over the crosshair it
  drops to .55. Adds lined up behind each other: the farther marker is lifted straight up clear of the nearer one (its
  kite and label never overlap another's). It is an IMGUI overlay anchored to the world point (on top of the scene by construction) rather than a world
  mesh with depth testing off, so the metres label stays crisp.
- **Off screen:** an arrow with the distance for each marked add off screen or behind the camera, on a rounded rectangle inset
  from the edges (sides .06, top .14 under the objective and LAST tags, bottom .18 over the cards, fractions of the height);
  behind the camera it leans to the bottom (turn round); arrows closer than 0.16 rad are spread apart round their mean bearing.
- **HUD:** `LAST n · MARKED` (n the kills left) under the objective tag in the scene, and under the KILL QUOTA bar on the J
  screen's building panel.
- **Cue:** one soft ping when the markers first turn on for a quota (run, loop, area); not on a count change, not on coming back
  to the same quota. Key `expedition.quota.mark` (like the door keys it needs registering in `NativeSoundKeys` before the bank
  can carry a clip); until then, and without LocalAssets, the built-in synthesized ping (`NativeQuotaMarks.PingSamples`).
- **Tiers:** Reduced effects: no pulse, no glow. Low graphics: no glow. No allocation per frame.

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
- Owner direction 2026-10-15 (superseding the PR #234 correction that made choosing informational only): the host holds the chooser (PR #237, `codex/expedition-choice-hold`). OpenChoice feeds the existing Frozen / input-cancellation / position hold: movement, attacks, abilities, roll, reload and block cancel, machine and cache purchases refuse, and camera yaw/pitch freeze (owner follow-up 2026-10-15). Pilots remain vulnerable to ordinary damage; enemies, teammates, difficulty and run clock continue. Current selection remains open across queued picks. The final Select holds until its run time plus `NativeDraftPlant.ReturnEnd` (`ChoicePlantSeconds`, `choosingUntil`), and an automatic CloseChoice cannot shorten that. An explicit CloseChoice before the final pick releases at once. Death, disconnect, extraction, area loading and save resume clear the flag and the deadline. The client keeps tray input available but blocks mouse look and lock-on tracking using the public choosing flag, including the final plant; the wrist plant (`Core/NativeChipPlant.cs`) is the presentation of that final hold.
- Successful `Select` emits exactly one ordinary arena `EventKind.Install`, with the selecting pilot as owner/target, current position, chip id, and resulting stack count. Refused/repeated selections emit none. Inventory synchronization and snapshot restoration do not replay installs.
- Notices now say C for chip choices; J still opens the Expedition board. The C key and screen/hologram bindings belong to Claude's presentation branch and were not present on shared when this authority patch was authored.

Optional Boss Test offer parity (implemented/Siren filtering, tier weights, money chips, stale-offer reroll) and tiered physical machine pickups remain deferred. Existing Expedition offer/economy rules are unchanged.

Presentation notes for Astra (Claude, 2026-10-15; nothing further is needed for the hold):
1. *Optional data.* Other clients cannot tell a final pick's plant from a queued one: `members[].choosing` stays true through both. Their wrist look lowers after every plant and rises again after 0.5 s if they are still choosing. A public "final plant" signal would let it stay up for a queued pick. For example, the remaining `choosingUntil` (or a boolean) on `ExpeditionViewMember`. The plant's start already comes from the public Install event.
2. *Observation, not a request.* On a non-final pick, an explicit CloseChoice during that pick's plant releases at once. The flow sends CloseChoice when C or Escape is pressed mid-plant. The pilot is then free before the chip is in. The plant still plays its full span on every client. The owner's "put their chip in their wrist after selected, then they are free" would also be met by holding every successful Select to its Install time plus `ChoicePlantSeconds`.


## Join authority additions (2026-10-17, batch 3 item 15)

Additive public fields, populated for host and recipient projections:

| Field | Meaning |
| --- | --- |
| `joins[].h` | Actual counted cylinder height in metres, from layout `join.h`; base is `y`. Membership tests the pilot capsule centre, feet + 0.85 m, including top/base. |
| `joins[].deployedAt` | Run elapsed seconds when the last cohort transfer was accepted, or -1 before any acceptance/reset. Retained across empty snapshots until the next accepted transfer. This records loading start, not successful arrival; a subsequent load can cancel. |
| `joins[].deployedAccounts` | Exact sorted accounts captured at that acceptance. Detached arrays; empty before the first acceptance/reset. |
| `joins[].closedReason` | `ExpeditionJoinClosedReason`: Open=0, Cleared=1, Locked=2, Reserved=3. Only Open counts members. Current live policy emits Open/Cleared; the other values are supported for future admission policies, not new locks. |
| `members[].pendingArea` | Destination while the member has an active cohort transfer; null after commit/cancellation, and during ordinary reconnect readiness. `area` remains their physical source until commit. |

Claude binding: compare deploy records, not emptiness/deadline, to trigger deploy effects. Treat the first observed record as a baseline so joining a hub late does not replay old deployments. Use `deployedAccounts` for exact participants and `pendingArea` for LOADING INTO. Keep loading distinct from completed arrival. Use `h` for curtain/count-volume height and the enum for closure text. These fields do not themselves change Claude's existing model or HUD. Old readers ignore them; no protocol change is required.

Batch 3 items 14/16 remain conditional: the nine kinds' authority/AI/content rows and the station/Ironworks hazard authority are absent at base `fda8c68`. Do not set Shipped, bind preview state, or start presentation clocks before those rules exist and replicate per area. `StationViewOf`, `IronworksViewOf`, `NativeStationLive` and `NativeIronworksLive` remain inactive. The earlier prompts 4, 5 and 13 define that separate implementation; this follow-up does not claim to deliver it. The independent station tagline is corrected in the activation snapshot and regenerated live maps.
