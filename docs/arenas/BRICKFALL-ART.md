# Brickfall: the stage art

2026-11-11 · Claude (stage art pass on the older stages) · branch `claude/artpass-brickfall` from `claude/sleepy-ritchie-g8ruhv`
(`db641c9c`). Status: **implemented on the branch and seen in the shared Editor; not merged, not released.** The bank is local
(git-ignored); the integrator packs it into the builder's Drive zip.

Owner (2026-11-11): "I want to revamp ALL the current stages out of the blocky style. Making them a LOT more detailed and styled ...
Ensure theres no Z-Fighting, remove invisible barriers etc."

Brickfall (`brickfall`, the default arena; docs/arenas/BRICKFALL-SIMULATION-CONTRACT.md) keeps its layout, its ten collapse segments,
their timings, crush shapes, nav and spawns exactly. What changed is what is drawn: with the stage bank
`ChipArenaStageBrickfall` every one of the map's 507 blocks, and the 106 rubble blocks its segments land as, is drawn as a model
(`ChipArena/StageLit`, Runtime `NativeStageArtDraw`) in place of its dressed box. Without the bank (CI, a build without the Drive
zip) nothing is made and the dressed blocks draw exactly as before. The recipe is docs/arenas/STAGE-ART.md; this is what Brickfall
adds to it.

## A stage without a kit: the blocks are the art

The four new stages model their Core kits. Brickfall has none: its look was its blocks' looks (`MapLooks.json` blockLooks, Runtime
`NativeArenaDressing`). So the art is modelled on the blocks themselves, and every block is covered (`cover/<id>`):

- **Static blocks (363) are world-space chunks** (32 chunks of 16 m, split at 14 m: the roofs over the frontage are their own).
- **Every block the simulation moves or breaks (144) is a `block/<id>` model** in the block's own frame, drawn where the block is:
  the hotel's 74 masonry panels and 12 floor slabs, its 4 corner piers, the tenements' lane facades (18 panels, 6 piers), the two
  skybridges (deck, two rails, bearing), the two kiln chimneys (base and four drums each) and the 30 cover props. They sag with
  the telegraph, pancake, peel, tear and topple exactly as their boxes did, because they are posed by the same transforms. A
  broken panel's fragments and stub are the dressing's boxes in the panel's brick look, as on every stage. Blocks alike share one
  mesh (each prop carries its own quarter turn; local +z is the side that looks out of its building): 154 block props, 42 meshes.
- **Each landed segment's rubble is one heap** (`NativeBrickfallArt.Heap`: the segment's first rubble block carries the model, the
  others are covered and draw nothing): a broken surface that follows the rubble blocks' tops, so feet stand on the heap where they
  stand on the blocks, skirted to the ground and strewn with loose bricks, slab shards, joists and bent bars. The drawer learned
  to draw models for a segment's rubble (static blocks that exist only once it has landed).
- **The collapse telegraph is the dressing's own**, on the models (`ArenaPresentation.BrickfallTell`, the drawer's new `Tell`
  hook): a stressed segment's blocks sag and lean by Core `NativeStructureFx.TelegraphFor` (the same record, clock and seed the
  dressing uses), their lit windows strobe with the telegraph's window light and go dark until it lands (a block model's glow
  sub-mesh times the hook's glow), and the fourteen structural members wear the load-bearing band as a bolted black strap with
  yellow chevrons, a DANGER plate and a lamp on each face whose lens is the model's glow: amber while the members are locked,
  strobing red under stress and at the last damage stage, dark otherwise (`NativeBlockLooks.Lamp`, `NativeBrickfallArt.Lamp`:
  the dressing's states and colours). "Unmarked brick never falls" still reads at a glance.

## What is modelled (tools/stages/brickfall_*.py)

`brickfall_kit.py` (finishes; walls as faces with their openings cut out, each opening with its reveal, sill, head and what fills
it: glass in a sash, boards, brick, a roller shutter, a door; mouldings run along a wall; a 5 x 7 face of raised letters; hazard
chevrons; the band), `brickfall_art.py` (the floor, the frontage, the main), `brickfall_parts.py` (the other static pieces),
`brickfall_props.py` (the block models and the heaps). The map is point symmetric: the north-west half is sculpted twice (other
names, tones and seeds the second time) and the second sculpt is turned half round; the hotel is sculpted whole.

- **The frontage** (the 14 m walls round the yard, which were blank brick): to north and south, four buildings a side in four
  bricks (red, plum, tan, rose), each with its shopfronts (shutters, boarded and glazed fronts under a lettered fascia), its
  entrance in the map's own recess (cheeks, soffit, a panelled door), two or three storeys of windows (segmental and round arches
  in brick rings with keystones, paired lights under stone lintels; glazed, lit, boarded or bricked up), strings, quoins, a
  corbelled cornice, a coping, a downpipe, and behind the parapet a slate roof with party walls, chimney stacks and pots; the
  swan-neck lamps where the look's sodium lights are. To east and west a brick warehouse of nine arched bays between piers (a
  loading shutter or a big window low, a lunette in the arch), its name painted along the frieze. The chimneys stand in their
  notches between two buildings' flank walls.
- **The yard's floor**: asphalt with patches, cobbled lanes along the frontages, paving round the buildings, lane paint, manholes
  and gratings; one cell is one face, so nothing lies on anything.
- **The Hotel Mercer**: buff brick panels with arched windows (lit, dark or boarded; the lit ones are the telegraph's), stone
  bands at the floor lines, quoined narrow panels, papered rooms inside with skirtings, parapets under copings; board floors on
  joists, a felted roof with a skylight curb; the corner piers on stone bases under stone caps, banded; the concrete stair tower
  with a painted dado, its flights and landings, the ground floor's seam walls with blind arches, the stair house; a neon blade
  sign (MERCER) on the north-east wing where the look's blue sign light is.
- **Ashby House and Bray House**: brick shells with blind windows by storey, papered rooms, board floors, a felted roof behind the
  parapet, the name over the south wall; the lane facade's panels and banded piers are the breakable models.
- **The skybridges**: chequer plate on riveted girders, plate-girder parapets (they are cover), banded steel bearings.
- **The kiln chimneys**: a stone-and-brick pedestal with the band, four eight-sided brick lengths in alternating tones with iron
  straps and rungs, the crown's oversailing courses round the flue's mouth.
- **The corner shops** (Kessel's, Dunmore's): cream faience on a dark plinth, shuttered and boarded fronts, lettered fascias, a
  cornice, the parapet, the long ramp as one slope.
- **The market and the yard**: panelled green kiosks with shuttered hatches and poster boards, rendered storerooms with roller
  doors, the market walls with blind arches under stone copings, the loading docks with steel nosings, buffers and hazard paint,
  the box trucks (a Luton body over the cab, so the top is one deck like the block's).
- **Street furniture that is cover**: the monuments' pedestals with bronze tablets, news kiosks, phone boxes glazed in small
  panes, the tram shelters (closed all round: they stop shots).
- **Cover props**: cribs of sleepers with a striped rail, pallets of new brick under bands, sandbag walls in bond, market stalls
  with canvas skirts and crates.

Cover reads as cover: what stops a shot is solid to the eye (a window in a breakable panel is glazed or boarded, never a hole).
`ChipArena/StageLit` gained **`DETAIL_BRICK`** (21): brickwork in running bond on the world's courses (225 x 75 mm, the parameter
scales it), light sunk mortar, each brick its own shade and a few burnt dark; where a course nears a pixel the bond eases into
its mean tone (screen derivatives), so far walls do not shimmer.

## No invisible walls

Brickfall had seven pilot columns. Four stay: the frontage's caps (`frontage-top-±1`, `frontage-side-top-±1`), which keep pilots
inside the map (beyond the wall's top is the backdrop's street, outside the bounds). Three went: the hotel's stair house
(`hotel-stairhouse-top`) and the two chimneys' crowns (`stack-n-top`, `stack-s-top`): a pilot may stand on anything they can
reach. (A chimney stands in the frontage, so the frontage's cap still covers its crown.) No column is at a lethal drop, so none
is `bots`. Where the data is: `tools/generate_brickfall.py` authors the columns; the catalogue keeps Brickfall's record as it
is, so `tools/generate_stage_hazards.py` applies the same rule to the kept record when the catalogue is regenerated, and
`Assets/Conversion/Resources/UnityMaps.json` and `tools/asset-lock.json` were edited in place to it (only Brickfall's
`pilotBlocks` changed; the content revision changes with it).

## No z-fighting

Every tier's static scene goes through `stagekit.lift_coplanar` before the bake. The stair flights' treads (which overlap along
their run in the map) are cut back to meet their neighbours, the ramps are one slope instead of their slices, and the floor is one
face a cell. Measured with `zfight.py` on the bank: see the numbers below. The look's six tarps (Core NativeBrickfallAtmosphere:
effect quads 6 cm off the blank frontage) do not draw while the art does (`NativeBrickfallFx.Tarps`): they lay over the modelled
windows, strings and piers. Without the bank they hang as before.

## Files

Core `NativeBrickfallArt.cs` (the bank's name, `Segment`, `Banded`, `Heap`, `Lamp`); Runtime `NativeBrickfallPresentation.cs`
(`BrickfallArtPrepare`, `BrickfallArt`, `BrickfallTell`), `NativeStagePrewarm.cs` (the art is prepared behind the loading screen);
Tests `BrickfallArtLayout.cs` (the layout's Brickfall part: looks, segments, rubble, lights, spawns, columns),
`BrickfallArtChecks.cs` (`--brickfall-art`, also in the full run); tools/stages `brickfall_art.py`, `brickfall_kit.py`,
`brickfall_parts.py`, `brickfall_props.py`.

Shared files touched (small, general): Runtime `NativeStageArt.cs` (the `Tell` hook on a block's model; models for a landed
segment's rubble), Core `NativeStageArt.cs` (`Banks`: one line), Resources `NativeStageLit.shader` and tools/stages `stagekit.py`
(`DETAIL_BRICK`), Tests `StageArtLayout.cs` (one line), `Program.cs` (the flag and the full run's line),
`tools/generate_stage_hazards.py` (one rule), `tools/generate_brickfall.py`, `UnityMaps.json`, `tools/asset-lock.json`.

Rebuild: `dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout brickfall LAYOUT.json`, then
`blender --background --factory-startup --python tools/stages/brickfall_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets`
(about 30 s; `--preview DIR [--views a,b]` renders the named views of the Fine tier, `--landed` shows every segment landed,
`--tiers`, `--no-ao`, `--static-only`, `--stats`).

NUMBERS
