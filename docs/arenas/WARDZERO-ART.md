# Ward Zero: the stage art pass

2026-11-11 · Claude (stage art pass on the older stages) · branch `claude/artpass-wardzero` from `claude/sleepy-ritchie-g8ruhv`
(`db641c9c`). Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The bank
is local (git-ignored); the integrator packs it into the builder's Drive zip.

Owner (2026-11-11): "I want to revamp ALL the current stages out of the blocky style. Making them a LOT more detailed and styled
... Ensure theres no Z-Fighting, remove invisible barriers etc."

Ward Zero (`siren`, and the Expedition's `expedition-ward-zero`, which shares its look) drew as boxes: 616 dressed map blocks and
6,870 kit boxes (Core NativeWardZeroKit). The art pass models the ward in Blender into its own stage bank and draws it with
`ChipArena/StageLit` through `NativeStageArtDraw` (the recipe: docs/arenas/STAGE-ART.md), in place of the blocks' boxes and the
kit's steady boxes. The gameplay geometry (blocks, colliders, the stairs' and ramps' wedges, nav, hazards and their timings,
entries, spawns) is untouched. Without the bank every box draws exactly as before. **Both map records use the one bank**: their
kits are the same part for part (checked), and the three blocks only the Expedition's record has (its exit frame) are rig props
drawn only where the map has them.

## How this stage differs from the four new ones

Ward Zero's kit is not a list of free parts but boxes in their blocks' frames, lit and moved each frame by the mood, the hazard
states, the entries' open amounts and the breaches. So the kit is split three ways (`NativeWardZeroKit.Family`, `Keeps`,
`Movers`; the boxes themselves are what they were):

- **Kept lit boxes (3,081 of 6,870)**: everything the kit lights or animates (`Keeps`: the troffers' diffusers that flicker and
  dim with the mood, lamps and beacons, the LED strips and nosings that chase up the routes, lettering, screens, the tanks'
  fluid, the burner grates' glow, the live gratings, the ink that spreads with the corruption). They still draw as their boxes
  over the art: flat lit panels, strips and letters are what they are, and their per-frame colours are the ward's mood and its
  telegraphs. The art is built round them (a troffer's tray, a high-bay's shroud, a beacon's cap and cage, a tank's steelwork)
  and keeps clear of them (cladding stands 5 to 7 mm proud where the kit's ink and lettering stand 12 mm and more).
- **Movers (236 parts in 108 groups on 17 models)**: the kit's moving parts are rig props posed by Core `NativeWardZeroArt` from
  the kit's own `Move`: the floor and ceiling vents' grate lids, the wall vents' louvres, the cell gates' round bars, the doors'
  leaves, the drop hatches' leaves, and all 48 blast shutter slats (each hidden while it is back in its lintel, as its box was).
- **Scenery (3,553 parts)**: every steady part is modelled, in the static chunks or on its block's own prop.

**Blocks that can leave are rig props too** (`NativeWardZeroArt.Posed`, 128 blocks; 131 with the exit frame): the interior walls
a Hulk may break out (every `NativeHulkBreach.Eligible` block: checked), the eight breach panels and the Expedition's exit
frame. Each is its own model in its block's frame with the kit's steady parts on it, drawn where the block stands with its
knock's shudder (`NudgeWardBlock`) and left out while it is broken out (`SetWardBlockBreach`) or its breach is open
(`SetWardBreach`), exactly as the block dressing and the kit parts on it are. The breach presentation's knocks, burst, pieces and
hologram are unchanged.

The shards' glowing teeth are the one lit family the art takes over (as lit glass splinters; their slow pulse is gone).

## What is modelled

- **Floors**: tiled floors on the slab and the decks (the shader's glazed tile), the ten floor vents as real openings with dark
  pits under their sliding grates, the Cradle's bolted steel seat (low, so the kit's ink creeps over it) with a dark tile band and a steel trim round it, hazard-striped
  thresholds and burner surrounds, painted lane dashes.
- **Walls**: every wall, the cell and observation rows and the shell's inner faces clad: a dark steel skirting, a tiled dado to
  1.15 m under a half-round steel cap rail, and panels about 1.2 m by 2.4 m with open joints (chamfered plates standing off a dark
  wall); narrow ends plain; free tops coped. Door and window frames, warning plates (slanted stripes), sign plates, round pipes
  on collars, the morgue's drawer fronts.
- **The roof**: a ceiling with deep I-beams one way and purlins in the bays between them (cut round the Spine hatch), round ducts
  with flanged joints and strap hangers, cable trays (ladder channels, rungs, four cables, hangers), the high-bays' shrouds, the
  troffers' trays, the beacons' turned caps and wire cages. Under the decks: soffits with downstand ribs between the troffers'
  rows (cut round the vents and lights).
- **Decks, bridges, gantries**: tiled decks with steel fascias, steel decks with cross members, the gantries' grating bars,
  I-beam stringers, striped nosings.
- **Rails**: round stainless handrails, turned posts with feet, a mid rail and two thin wires (open to shots and sight, closed
  to a body, as the colliders are), chamfered kick plates.
- **Stairs and ramps**: steps with their risers and the flights' solid panelled sides down to the floor, turned posts and one
  sloped handrail a flight side; the ramps' decks, curbs and striped bands, their skirts as solid walls.
- **Rooms**: cell doors (a heavy leaf with a sunk panel, three hinges, a wheel lock, bolts), the rows' steel canopy line, the
  nurse booths (posts, mullions, a counter, lit glazing), lab benches and consoles as cabinets with door plates and pulls, jars
  and analysers, specimen tanks (rounded plinth and cap, a header, round posts, pipes), morgue chambers and lockers (pillowed
  fronts, louvres), autopsy tables, showers (tiled stall, arm and rose), racks, the furnaces (round grill bars in a striped
  frame), triage screens, benches.
- **The Cradle**: the vessel's six shards as bowed walls of smoked armour glass in steel shoes with broken tops and lit splinters.
- **Entries and hazards**: frames, dark openings, hatch rings; the shutters' rounded housings and guide rails; the decon locks'
  turned nozzles; the breach panels (plate, striped frame, braces, rivet domes, dents, stencil plates).
- **Breakables** (12): transport stretchers (a fairing, a tube frame, mattress and pillow, side rails, castors) and equipment
  carts (drawer cabinet, rimmed tray, canisters, push bar), `block/<id>` props drawn while the block stands at
  `NativeStageArt.Keep` or better.

The stage is a sealed building: there is no backdrop outside it to model.

## No z-fighting

`stagekit.lift_coplanar` runs over each tier's whole rest scene (the static scenery with the posed blocks and the props in
place). Beyond it the script leaves out every face that can never be seen and would lie in another's plane: a block's faces a
static block covers whole (`hidden`; a face against a block that can leave stays), a kit part's faces on or in its block
(`buried`), the beams' ends in the shell, and a trim the kit leaves standing in the air (`adrift`: a jamb in an open doorway). `$SP/py/zfight.py` on the bank, both records, Fine (which the Mid props share) and Low:
**0 pairs, 0.00 m2**. What the stage still draws over the art: the kit's kept boxes (each stands off the art by the kit's own
layers), the vents' pit shader quad (1.2 cm over the floor, only while a vent is open), the effects' quads.

## No invisible walls

Both records' `pilotBlocks` are empty (they always were: Ward Zero's draft has no pilot columns), and the check now pins that as
the rule. The only colliders without a block's box are the stairs' and ramps' wedges (docs/arenas/WARDZERO-SIMULATION-CONTRACT.md): closed solids from foot to top "down to bottom". Their treads reach the floor and the ramps' skirts close their
sides, so the art under them is solid too (checked: every tread stands on its wedge's floor, every ramp has its sixteen skirts).
The rails' 0.8 m gap between the top bar and the kick plate stops a capsule but not shots: the art shows posts, a mid rail and
wires there, so it reads as it plays. Nothing was removed from the map records; `UnityMaps.json` is unchanged.

## Files

- Core: `NativeWardZeroKit.cs` (Family, Keeps, Movers; the boxes unchanged), `NativeWardZeroArt.cs` (new: slots, Posed, Pose),
  `NativeStageArt.cs` (the registry's line: both records).
- Runtime: `NativeWardZeroPresentation.cs` (prepare, pose, render; under the art only the kept boxes draw; the shudder to the art).
- Tests: `WardZeroArtLayout.cs` (new: the layout's ward part), `WardZeroArtChecks.cs` (new, `--wardzero-art`, also in the full
  run), `StageArtLayout.cs` (one line), `Program.cs` (the flag and the full run's line), `WardZeroChecks.cs` (its "no colliders,
  objects or bodies" pin allows the one component the presentation adds: the art's drawer).
- tools/stages: `wardzero_art.py`, `wardzero_kit.py`.
- The menu backdrop (WardZeroBackdrop) keeps the box kit, as the other stages' previews do.

Rebuild: `--stage-layout siren SIREN.json`, `--stage-layout expedition-ward-zero EXP.json`, then
`blender --background --factory-startup --python tools/stages/wardzero_art.py -- --layout SIREN.json --also EXP.json --out
<Assets>/LocalAssets` (about 35 s; `--preview DIR [--views a,b] [--preview-tier 2]`, `--tiers`, `--no-ao`, `--stats`, `--own-mid`). Deterministic:
two builds give the same bytes.

## The bank

`ChipArenaStageWardZero.bytes`: 18,188,161 bytes, 660 props besides the 619 covers. Static: 60 chunks (16 m cells, three height
bands); Fine 199,730 triangles (209,524 vertices), Mid the Fine meshes again (shared, as the Sanctum's), Low 30,252 (45,341).
Rig (17 mover models and 131 posed blocks): 72,572 / shared / 11,076 triangles. Breakables: 13,976 / shared / 4,704.
The Low graphics tier draws the bank's Low tier (the same art, about a seventh of the triangles), not the box kit.

## Checks

`--wardzero-art`: the registry (one bank, both records); the two records' kits the same part for part; every part kept, a mover
or scenery; 108 movers on 17 models, 48 slats on four; the posed blocks (every Hulk wall, every panel with its entry, the exit
frame on the Expedition only); at every 0.05 s of two rounds each mover drawn where the kit's `Move` puts its boxes (to 0.1 mm)
and hidden exactly when its box is; an open breach's panel and a broken-out wall undrawn with what rides on them, a shudder
carried; pure and allocation free; no pilot columns and solid wedges; the layout export; the wiring and the bank-absent fallback
(source); and, with the bank on the machine: its budget, every block of both records covered, every slot and breakable in its
tiers, the play-space rule (below 2 m over either floor the art, posed blocks and movers at rest keep within .25 m of a block),
nothing in a spawn.

## Seen, measured, not done

See the branch's report for the capture sheets and frame times (kept out of the repository). Not done or not seen:
- The kept lit boxes are still boxes by design (panels, strips, letters, screens, the tanks' fluid); the tanks' fluid is a lit
  box inside modelled steelwork.
- The shards' splinters no longer pulse with the mood (a static glow).
- A wall's shudder and break were checked by the pose checks, not seen in a PvE run in the Editor; multiplayer guests, the
  killcam on the art, a native Windows build and the main menu's preview (box kit) were not run.
- Practical-light shadows: as on the other art stages (the look's key casts none indoors).
