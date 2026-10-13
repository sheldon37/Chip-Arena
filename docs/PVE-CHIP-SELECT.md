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

- **Display wrist.** The left wrist comes up, or the right one for the staffs, L33t H4x and the bow. The forearm crosses in
  front of the chest and the back of the wrist turns to the eyes.
- **Head and torso.** The head looks down at the display (about 35–38 degrees) and the chest bows a little toward it.
- **Weapons.** The weapon stays in its own hand:
  - Guns, swords and shields are lowered. The greatsword trails low behind with its tip off the floor, and the floor guard
    keeps every lowered tip above the floor.
  - The staff and the bow keep their idle hold exactly.
  - Carry axes are upright (in the rig root's frame), so a crouched melee stance does not tip them.
- **The other hand:**
  - Empty hands (Cyber Lord, the gauntlets) tap the lit card and swipe across the panel.
  - A pistol or dagger hand swipes along the panel's lower edge, with the weapon kept pointing down below the cards.
  - With long weapons, the display wrist flicks instead.
- **Rhythm.** One gesture runs per 2.4 s slot (2.1 s for flicks), on the pick's beat. Each contact moves the light to
  another card.
- **Blending.** The look eases in over 0.42 s and out over 0.3 s. A death drops it, and a clock jump or a gap longer than
  0.5 s (for example a revival) restarts it.

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
the pick still open: the close after Choose later, a menu over the world, or a hold.

- The raised wrist sits low left (low right for a right display), with a small panel above it, at most 20 degrees wide. The
  panel shrinks with its distance, so the bow's closer arm shows it at the same size.
- The weapon goes down out of view, and an armed hand never reaches into it.

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
| `Runtime/NativeChipSelectPresentation.cs` | Pose hook, view-model wrist and world/view meshes |
| `Runtime/NativeChipSelectHud.cs` | The tag |
| `Resources/NativeChipSelect.shader` | The hologram shader |

One-line hooks were added in `ArenaPresentation.PrepareCapturedPose` and `Render`, `NativeViewPresentation.RenderFirstPerson`,
`NativeCombatHud.CombatWorldHud` and `NativeSandbox`.

Checks: `Tests/ChipSelectChecks.cs` (managed) and `Tests/test_chip_select.py` (wiring).
