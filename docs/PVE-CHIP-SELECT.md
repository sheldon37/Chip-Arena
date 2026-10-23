# PvE chip selection: what other pilots see

Owner, 2026-10-13: "Lets also add a 'Chip Selection' Animation for PVE (Similar to what we had for Electron), what I mean is
when other people are picking chips on PVE, we can see them looking at a holographic display through their wrist. Above,
it'll show a small animated Text 'Selecting Chip'."

Presentation only. It makes no change to the pick flow, the PvE rules, the network frame or saves.

## The state it reads

The look reads the replicated open pick that already exists, and no new field is needed.

| Where | What |
|---|---|
| `Runtime/NativeNetworkFrame.cs` | Every PvE row frame carries `open` (the open pick's id, 0 for none). The host writes `open=r.Open?.Id??0`, and guests rebuild `NativePveRow.Open` (`if(pf.id==rf.open)r.Open=pick;`). |
| `Runtime/NativeChipSelectPresentation.cs` `ChipSelecting` | This is the only reader. It passes `row.Open?.Id`, the pilot's HP and connection, `Pve.Holding`, `Pve.Cinematic` and `Stage=="ended"` to `Core/NativeChipSelect.Selecting`. |
| The pick's `At` (replicated grant time) | This is the gesture beat, so every client taps on the same rhythm. |
| The pick's `Offers` and `Tier` | These give the cards' rarity colours, and the rainbow pick cycles hues. |
| Snapshot and arena events | A damaging hit in the last 1.2 s makes the hologram and the tag blink red (the Electron alarm). |

Party bots have rows too, so a bot shows the look while it thinks over its wave pick (2–6 s).

**Not replicated: the open time.** Each client therefore eases in from the frame where it first sees the pick open. A late
joiner does the same.

**Optional for Astra, not required:** a per-row `openAt` in `NativePveRowFrame` would let every client start the raise and
the tag's typing from the true open moment. The presentation would read it in `ChipSelecting` only.

## Electron baseline

The baseline is `client/pve/hologram.ts`, `world-view.ts` and `board.ts` at 05950ea. It was rendered with its real modules
for the sheets.

- Over every remote chooser's head sat a cyan, additive hologram: a fan of three cards, a spinning chip die and a projector
  cone.
- It grew in over 0.25 s from `openAt`, flickered, and blinked red at 6 Hz while the chooser was hit.
- It was hidden for dead pilots and beyond 55 m.
- The choosing pose bent the head down 0.25 rad and raised the right forearm toward the chest.
- The party board showed "Choosing chip".

Unity keeps the trigger, the red alarm, the three cards and the cyan look. It moves the hologram onto the wrist and adds the tag.

## The look

### Pose (`NativeChipSelectPose`)

The pose layers over the route's own pose through the rig's `Overlay` and writes only the arms, neck, head, spine and chest.
The legs, pelvis and body root are untouched, so the feet stay planted and the gait walks under it.

- **Display wrist.** The right wrist comes up for every class (owner, 2026-10-22: the pilot's chip device rides it, see
  `docs/CHARACTER-LOWPOLY.md` section e). Before that the left came up, or the right for the staffs, L33t H4x and the bow.
  The forearm crosses in front of the chest and the back of the wrist turns to the eyes. The rifle, the machine gun and the
  greatsword hold it lower, in front of the hip (`LowRaised`), so the weapon can hang beside the leg.
- **Head and torso.** The head looks down at the display (about 35–38 degrees) and the chest bows a little toward it.
- **Weapons.** The weapon stays in its own hand:
  - A weapon in the right (display) hand hangs pointing down (owner, 2026-10-22). The pistol, the sword and a dagger hang
    from the raised wrist at chest height, turned away from the eyes. The rifle, the machine gun and the greatsword hang
    from the low wrist beside the right leg, never across the body (`HangAxis`, turned with the hips). The floor guard keeps
    every tip above the floor, and the checks keep the whole weapon clear of the head, the panel and the eyes' line to it.
  - A two-handed weapon's support hand lets go while the look is up.
  - Before 2026-10-22 the daggers trailed low and back (2026-10-15). Carried forward, the right dagger's raise swung its long blade through
    plumb, and the floor guard's lift axis is undefined there, so the hand spun about 22° in one frame at 240 Hz. The guard
    (`KeepOffFloor`) now lifts a near-plumb blade about the hand's level across-axis, always the same way round; away from
    plumb it matches `CapturedRig.BladeFloor` exactly.
  - The shield, the staffs and the bow keep their idle hold exactly (in the left hand).
  - Carry axes are upright (in the rig root's frame), so a crouched melee stance does not tip them.
- **The other hand (the left):**
  - An empty hand (Cyber Lord, the gauntlets, and every class whose weapon is in the right hand) taps the lit card and swipes
    across the panel.
  - A left dagger swipes along the panel's lower edge, kept pointing down below the cards.
  - Where it keeps its hold (the shield, the staffs, the bow), the display wrist flicks instead.
- **Rhythm.** One gesture runs per 2.4 s slot (2.1 s for flicks), on the pick's beat. Each contact moves the light to
  another card.
- **Blending.** The look eases in over 0.42 s and out over 0.3 s. A death drops it, and a clock jump or a gap longer than
  0.5 s (for example a revival) restarts it.
- **Turn blends.** The hands' turn blends (`ChipTurnBlend`) keep going round the same way from frame to frame. Before
  2026-10-15 a plain shortest-way slerp flipped the left dagger hand through 165° in one frame mid-raise. The checks now
  bound every hand's turn off the route (no flips) as well as its position.
- **Hits (2026-10-15).** A damaging hit flinches the look (`NativeChipSelect.Flinch`): it rises over 0.06 s, settles over
  0.4 s and yields up to 45% of the weight to the route's pose. The hit reaction underneath shows through the raised wrist
  and the gaze. The torso's own reaction already carries the arm.

### Hologram (`NativeChipSelectHolo` and `Resources/NativeChipSelect.shader`)

- **Layout.** A chamfered hardlight panel rises over the forearm and faces the wearer. It stands at least 30 degrees from
  flat so the pilots around it see its face.
- **Cards.** Up to three cards show the offers in their rarity colours. The lit card lifts toward the wearer, and a tap
  rings where it lands.
- **Projection.** A projection cone runs from an emitter ring on the cuff, with rising motes.
- **Effects.** Scanlines, flicker with a glitch dip, and the scene fog.
- **Draw cost.** One additive mesh holds every pilot, in one draw.

### Tag (`NativeChipSelectTag` and `Runtime/NativeChipSelectHud.cs`)

- **Text.** "SELECTING CHIP" types in by 0.66 s with a block cursor, then three dots light in turn. It sits on a navy
  chamfered plate with a cyan edge, a chip glyph and a breathing glow.
- **Placement.** It sits above the overhead name. It uses the overhead bars' occlusion ray, eased over 0.15 s, and their
  range. It fades between 20 and 30 m, inside the names' 32 m, and stays clamped on screen.
- **Never shown** for my own pilot in first person.

### First person

The local pick screen is full-screen and hides the arena, so the view-model wrist only shows when the arena is visible with
the pick still open: the close after Choose later, a menu over the world, or a hold. In an Expedition the tray is an overlay
and the authority holds the chooser, so my wrist shows under the tray and through the wrist plant.

- The raised wrist sits low right, with a small panel above it, at most 20 degrees wide. The panel shrinks with its
  distance, so the bow's closer arm shows it at the same size.
- A short weapon in the display hand points away from the camera. A long one (the rifles, the greatsword) is put away while
  the look is up past 0.15 and comes back as it lowers (`NativeChipSelect.Stows`): its stock or hilt would fill the view.
- A left-hand weapon goes down out of view, and an armed hand never reaches into it.

### Expedition wrist plant (`Core/NativeChipPlant.cs`)

After each Expedition pick the chosen card becomes a chip and goes into the display (right) wrist's cuff. Owner: "Still make them put
their chip in their wrist after selected, then they are free to move." This runs on the Boss Test plant's timeline
(`NativeDraftPlant`), laid over the pose above:

| Time | What happens |
|---|---|
| 0–0.3 s | The other cards fade, and the chosen card lifts toward the wearer. |
| 0.3–0.75 s | The panel folds away. |
| 0.62 s (`Contact`) | The left hand takes the card, which is a lit cartridge in its rarity colour from then on. |
| 1.14 s (`CarryEnd`, `SoundAt`) | The chip seats on the cuff, and the install cue plays. |
| 1.32 s (`PlantEnd`) | The chip has sunk into the cuff. |
| 1.14–1.84 s | The cuff pulses in the chip's colour. |
| 1.36–1.95 s | The arms return. |

The authority's final hold has the same span: `NativeChipPlant.Span` = `NativeDraftPlant.ReturnEnd` =
`NativeExpedition.ChoicePlantSeconds`.

- **Hand plant.** In third person the left hand takes the card at the contact when it is empty, wears a gauntlet or
  carries a dagger. It carries the chip over a small arc and presses it into the right wrist's cuff, palm down or with the
  dagger kept pointing down.
- **Wrist plant.** Where the left hand keeps its hold (the shield, the staffs, the bow), the chip drops down the projection
  cone, and the display wrist rises 2 cm to take it, then gives as it seats. First person always uses this, for every class,
  because a hand crossing the view hid the chip.
- **Visual bodies.** A pilot drawn with a visual body projects its hologram, its Expedition screen and the plant's chip from
  its wrist device's bay, not the game rig's wrist (`VisualEmitter`, `ChipSelectSlot.Shift`). The rigs' proportions differ,
  so the two wrists sit up to about 0.4 m apart.
- **First person beside the tray.** While my tray or its follow-through is on screen, the view-model wrist eases over
  0.3 s from under the cards to beside them (`NativeChipSelectPose.TrayWrist`). It sits right of the band, sized to the
  view's angles. The greatsword's and the bow's view arms cannot reach out there, so they stay
  under the tray.
- **Mine.** The look runs from my pick (the tray's follow-through clock), whatever the tray does. The tray's picked card
  flies into the hologram as the hand takes it.
- **Others.** Everyone else's look runs from the authority's `Install` event for them.
- **Return.** A pick with another of my rewards following on the tray returns the arms to the hologram pose, and the panel
  unfolds again. Otherwise the look lowers. Another pilot's look lowers after each plant, then rises again after 0.5 s
  for a queued pick (their flag reads choosing through the final plant as well).

### Tiers

| Tier | What changes |
|---|---|
| Reduced effects | No motes, flicker or scan travel. The red is steady, the tag is typed whole and the dots are still. |
| Low graphics | Panel and cards only: no cone, ring or motes. |

No tier allocates per frame. Four pilots fit in at most 248 vertices.

### Dev Sandbox

The pause menu has **Preview Chip Selecting** under Previews. With it on, every pilot in the sandbox shows the look on
stand-in offers (rare, epic and legendary). Use "Dummy weapon" to try each class.

## Files

| File | Contents |
|---|---|
| `Core/NativeChipSelect.cs` | Mapping, track, gestures, pose, hologram geometry and tag timing (engine-free) |
| `Core/NativeChipPlant.cs` | The Expedition wrist plant: timeline, cue and pose layer (engine-free) |
| `Runtime/NativeChipSelectPresentation.cs` | Pose hook, view-model wrist and world/view meshes |
| `Runtime/NativeChipSelectHud.cs` | The tag |
| `Resources/NativeChipSelect.shader` | The hologram shader |

One-line hooks were added in `ArenaPresentation.PrepareCapturedPose` and `Render`, `NativeViewPresentation.RenderFirstPerson`,
`NativeCombatHud.CombatWorldHud` and `NativeSandbox`.

Checks: `Tests/ChipSelectChecks.cs` (managed) and `Tests/test_chip_select.py` (wiring).
