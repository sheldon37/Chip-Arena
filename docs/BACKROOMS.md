# Backrooms levels: plan, kit and the Astra/Claude split

Owner, 2026-11-02: "Can we begin creating a backrooms levels? We have the assets I believe".

Owner decisions (2026-11-02):

- **Where:** both an Expedition destination (rolled at the Deployment Station like Ward Zero, Central Station and City
  Ironworks) and a PvP arena map.
- **Layout:** a generated maze, seeded, from the kit's pieces. Every run is a different stretch.
- **First level:** Level 0 (yellow wallpaper, damp carpet, fluorescent ceiling). The Poolrooms come after.
- **Split:**
  - Astra: the generator, collision, navigation, spawns, objectives and rules.
  - Claude: the art (the kit in the game, materials, lights, atmosphere, the camera filter) and this spec.

Status:
- **Level 0** is in the game (Astra's rules below): the PvP map `backrooms-level-0` and the Expedition destination. It is drawn
  by Claude's procedural builder with the owner's add-on's textures (owner 2026-11-02: "Generated + its textures"; "The
  add-on's textures" below). A dedicated map look is still to do.
- **The Poolrooms:** the layout contract, a stand-in generator, the geometry, the water and a DEV preview are built (Claude,
  2026-11-02; "The Poolrooms" below). No map, generator or rules yet (Astra's).

## Level 0's look: the owner's reference (2026-11-02, supersedes the kit look below)

The owner sent the classic Level 0 photograph: "The backrooms isn't supposed to be open like the pictures shown ... we need to
go EXACTLY for this style".

- **The photo:** enclosed rooms and corridors behind thin papered partitions that run up to a low drop ceiling. Doorways are
  cut through them. Pale cream-yellow wallpaper with fine chevron stripes, a tan carpet, a wooden baseboard rail, white
  acoustic ceiling tiles on a T-grid with long recessed fluorescent strips, and flat, slightly overexposed yellow light.
- **So the level is built procedurally** (Core `BackroomsMesh`) instead of from the modular FBX kit: floor, ceiling, walls to
  the ceiling with doorways and jambs, baseboards, slim pillars and fixtures. The kit's walls, ceiling panels and wallpaper
  are a stylised take that doesn't match. Its pieces stay in the bank for later dressing (doors, ladder, vents).
- **Textures:** since 2026-11-02 the add-on's own baked colour maps (see "The add-on's textures"). Before that, the pack's
  "yellow offices" source images toned paler to the photo (`Level0/*_pale.png`, kept locally).
- **Proportions:**
  - cells 2.4 m (8 ft); ceiling 2.7 m (9 ft); walls 0.14 m thick;
  - doorways 1 m, cut to the ceiling;
  - pillars 0.5 m, only deep in open halls;
  - fixtures 1.2 × 0.3 m over every other cell in both directions.
- **Layout (the pack's archetype, `yellow_offices.json`):** a grid maze. Every inner edge starts walled. Then:
  - rooms of 2-5 cells a side and some long corridors are opened up;
  - they are joined by a random spanning tree of doorways (a 1 m doorway into a wall that continues on both sides, else
    the whole cell edge open), plus about 12% more for loops;
  - pillars appear only in the open halls.
- **Seen in Unity** (DEV preview, captures from inside at eye height): it matches the photo's structure and palette. Lone wall
  stubs where doorways meet are rare now.

## The add-on's textures (owner 2026-11-02: generated layouts with the add-on's textures)

The owner asked to use the Backrooms Pro Generator add-on (`backroom_blender-1.0.0.zip`, GPL-3.0, made for Blender 4.5) and
installed Blender (Steam, 5.2.3). The owner chose our generated layouts with the add-on's textures over fixed levels exported
from it: every run stays a fresh layout and Astra's Level 0 rules keep working.

- **Running it:** headless, from a driver script outside the repository: `blender --background --factory-startup --python`
  (no user preferences touched). It registers the add-on for that process only, generates an archetype at a seed and exports it
  for Unity (FBX tiles, 2K PBR bakes, a JSON sidecar).
  - On 5.2 the add-on needs one patch at runtime: a Geometry Nodes modifier's inputs are
    `mod.properties.inputs.<identifier>.value`, no longer `mod[identifier]`.
  - Only its exported textures are used. Its code and the generated `*Setup.cs` stay out of the game. The textures stay local
    like the packs (its assets are CC0 per its `SOURCES.txt`).
- **Level 0** (`yellow_offices`): its baked colour maps for the wallpaper (olive, fine striped chevrons), the carpet (golden
  tan), the ceiling grid and the trim (red-brown wood), as the bank textures `backrooms-l0-*`.
  - Repeats as the add-on lays them (measured on its exported meshes): carpet 1.82 m, wallpaper 1.5 m, trim 1 m. The ceiling
    grid stays at one repeat per cell, so its tiles line up with the fixtures.
  - Tints are white: the add-on's colours as they are.
  - The wallpaper bake darkens its bottom rows, which showed as a band at each vertical repeat. In the local copy, its bottom 84
    rows fade into the rows that continue its top (the stripes repeat every 14 rows).
- **The Poolrooms** (`poolrooms`): wall tile (0.48 m), floor tile (1.12 m), turquoise basin tile (0.8 m), ceiling plaster
  (2 m) and the water's normal map, as `backrooms-pool-*`.
- **Files:** `Assets/LocalAssets/Backrooms/Addon/` (PNG, with `SOURCES.txt`); the selection
  `tools/pack_bank/selections/kit-backrooms.json`.
- **Shipping:** the Backrooms bank rides in the builder's licensed-assets zip beside the visual body (`tools/pack_visual_body.py`
  packs the `.bytes` and `PackBank/*.png` there). Without it, the live Level 0 falls back to plain map blocks.

## The assets (licensed: never commit or publish them)

- **Source:** `G:/My Drive/Chip Arena Work/Unity Assets/Backrooms.zip` (6.4 GB).
  - Its FBX and `.blend` entries use Deflate64. Git Bash `unzip` and Python's `zipfile` cannot read them; a .NET 8
    `ZipFile` reader can.
- **Contents:**
  - `assets_all.fbx`: the modular kit (83 pieces, catalogued below).
  - `backrooms.fbx`: a demo scene built from the kit (174 × 45 m).
  - `Backrooms kit.blend`, `backrooms_packed.blend`, `BACKROOMS_2026_packed.zip`, `poolrooms assets.blend`,
    `extra-lights-assets_only.blend`: Blender scenes (no Blender on the owner's PC yet).
  - `Backrooms Textures.zip`: Carpet011 (colour, normal, roughness, displacement), the Level 0 wallpaper
    (`backrooms tile DG.png`: yellow-green chevrons and a dashed stripe), wood and mahogany panels, an outlet.
  - `Textures.rar` (518 MB, not opened: no RAR reader here).
  - `Backrooms Overlays.zip`: 25 found-footage overlays (noise, scanlines, tape damage, dust, grid, artifacts,
    interference).
  - Blender add-ons: a handycam filter, a maze generator, extra lights. Reference only; never run.
  - References and video lessons.
- **Local copy for Unity:** `Assets/LocalAssets/Backrooms/` (git-ignored): `Models/`, `Textures/`, `Overlays/`. Unity imports
  the FBX with Standard materials and no textures attached.
- **Into the game:** a pack bank (`docs/PACK-BANK.md`) named `ChipArenaPackBackrooms`.
  - Built by `tools/pack_bank` from a committed selection (names and paths only). Shipped in the Drive licensed-assets zip.
  - With no bank (CI, cloud builds, the public source zips), the level draws plain boxes in the same layout.
  - **Tool work needed:** the FBX's walls carry several materials (plaster, wallpaper, trim). The bank tool's `model` route
    gives one albedo per prop, so it needs a per-submesh material map.

## The kit (measured in Unity, metres)

**The grid: 3 m cells, 3 m from floor to ceiling.** Walls run along cell edges, 0.30-0.33 m thick.

| Pieces | Size | Use |
|---|---|---|
| `wall2_*`, `wall3_*`, `wall6_*`, `wall7_*` | 3 × 3 × 0.33 | A straight cell-edge wall. Finishes: wallpaper (+ plaster + trim), plaster, white plaster + dark trim. |
| `wall4_*` | 2 × 3 × 0.33 | A short wall (a gap or a doorway beside it). |
| `wall1_*` | 3.16 × 3 × 3.16 (an L) | An outside or inside corner. |
| `wall5_*` | 1.16 square | A wall end or post. |
| `pillar1_*` / `pillar2_*` | 1.33 / 3.3 square, 3 tall | Pillars (Level 0's open rooms). Plaster or white. |
| `floor_tile_flat`, `_edge`, `_corner`, `_end`, `_corridor` | 3 × 3 | Floor; edges and ends carry a skirting lip. |
| `floor1`, `floor2_*`, `floor3` | 2-3 m quads | Carpet and linoleum floor patches. |
| `floor_hole1`-`9` | 2-2.5 × 5 deep | Pits through the floor. |
| `ceiling_panel*` | 3 × 3 (nine 1 m tiles) | Ceiling grid: frame, backdrop and tiles. |
| `ceiling_lamp` | 0.97 square | Fluorescent panel, in place of a tile. |
| `ceiling_vent` | 0.97 square | Vent, in place of a tile. |
| `door`, `door_frame` | 1 × 2.17 | A door (exits, locked rooms). |
| `ladder` | 0.86 × 2.23 | A ladder. |
| `tunnel1`-`3` | about 1 m section | Crawl tunnels in the walls. |
| `wall_socket`, `wallpaper_peel` | small | Dressing. |

Materials (named, no textures): carpet, linoleum, wallpaper, plaster, plaster_white, wall_trim, wall_trim_dark,
ceiling_frame, ceiling_panels, ceiling_backdrop, ceiling_vent, lamp, door, ladder, wall_socket.

## The layout contract (Core, pure; the generator is Astra's)

One shared description that the generator fills and the art draws, so the two stay apart. A proposal for Astra to own and
change. Claude draws whatever it says.

- **`BackroomsLayout`** (as built, 2026-11-02: `Cell` 2.4, `Height` 2.7, `Thickness` 0.14, `Doorway` 1.0; per cell `Floor`
  carpet or pit, `Pillar` none or small, `Light` and its `Circuit`; edges Open, Wall or Short, a wall with a doorway). Made
  from a seed and a level id. The same seed gives the same layout on host and guests: the
  ticket carries the seed, as the generated city does.
  - `Width` × `Depth` cells of `Cell = 3` m, the origin at a corner; floor at y 0, ceiling at 3.
  - Per cell:
    - `Floor`: carpet, linoleum or pit.
    - `Ceiling`: tiles, plus each 1 m tile slot's fixture (none, lamp, vent).
    - `Pillar`: none, small or large, at the cell's centre.
    - `Props`: a socket or peeling paper on a wall face.
  - Per cell edge (north and east of each cell; the outer border always walled):
    - `Wall`: none, full or short (a 1 m gap);
    - `Finish`: wallpaper, plaster or white;
    - `Door`: none or a door, with its state.
  - **Lamps:**
    - Each lamp has a circuit id. Circuits flicker or go dark together; the presentation animates them.
    - Each circuit's state (on, flickering, dead) is part of the layout, or replicated if rules change it.
  - **Marked cells:** entry, exit, player spawns, enemy spawns and objective anchors.
- **Level 0 feel (for the generator):**
  - Mostly open, irregular rooms with scattered pillars and partial walls. Long corridors and dead ends. No windows.
  - The same yellow everywhere, a few dead or flickering lamps, rare pits.
  - Reference: the demo scene in `backrooms.fbx` and `Liminal Space References.zip`.
- **Sizes (proposals):**
  - Expedition: a run's Backrooms instance about 40 × 40 cells (120 m).
  - PvP: one fixed seed per map, about 16 × 16 cells (48 m), sightlines broken often.
- **Rules (Astra's to decide):**
  - Collision boxes from walls, pillars, doors and pit edges; navigation over open cells.
  - The Expedition objective (find the exit door, a quota, items to collect) and its entities (the horde, or Backrooms-only
    enemies later).
  - The PvP map's spawns and pickups.
  - The fluorescent hum's audio.

## The art (Claude)

1. **The bank:**
   - Bake the kit into `ChipArenaPackBackrooms` with a per-submesh material map: the wallpaper texture, Carpet011 and
     plaster.
   - Make the ceiling tiles and the lamp diffuser, which the pack has no textures for: acoustic tile and lamp glow.
2. **The draw:** the layout drawn from the bank, instanced by piece and material, with walls, floors, ceilings, pillars,
   lamps and dressing. The fallback is boxes in the same layout. Distance culling: the maze is large.
3. **The light:**
   - Fluorescent panels as the scene's practical lights through the existing kit light pool (nearest lamps lit).
   - A flat, sickly yellow ambient and a soft haze.
   - Lamps buzz and flicker by circuit; dark sections stay dark.
4. **The camera:**
   - An optional found-footage filter built from the pack's overlays: grain, scanlines, a little tape damage.
   - A setting, off by default in PvP; reduced effects lowers it.
5. **Later:**
   - The Poolrooms (white tile, water) from `poolrooms assets.blend`, once Blender, or an export of it, is available.
   - Entities' looks.

## Built so far (Claude, 2026-11-02)

- **The bank, `ChipArenaPackBackrooms`:** 83 props (one per kit piece), 14 materials, the wallpaper and carpet textures.
  - Selection: `tools/pack_bank/selections/kit-backrooms.json`. The carpet is the pack's JPEG converted to PNG beside it,
    since the tool reads PNG, TGA and PSD only.
  - Build it locally:
    `python -I tools/pack_bank/build_pack_bank.py --packs Assets/LocalAssets --out Assets/LocalAssets tools/pack_bank/selections/kit-backrooms.json`
  - It goes into the Drive licensed-assets zip like the other banks. It is not in the zip yet, so the builder has no
    Backrooms bank.
- **Three new bank-tool options** (`tools/pack_bank/build_pack_bank.py`, `Tests/test_pack_bank.py`):
  - `split`: each FBX node becomes its own prop, its translation dropped; callers place pieces by their bounds.
  - `materials`: an FBX material name maps to an albedo, a colour and an emission.
  - `wind_by_normals`: turns each triangle to face its corners' normals. The kit's floors and ceiling tiles are wound
    against their own normals, its lamps are not. Unity culls them inside-out the same way, so the tool corrects it.
- **Core `NativeBackrooms`:**
  - `BackroomsLayout`, the contract above, implemented as proposed. Astra may change it.
  - `Preview(seed, w, d)`: a seeded stand-in Level 0 generator.
  - `Plan`: the kit pieces within reach of the eye.
  - `Level`: circuit flicker. `NearLamps`: the lamps that light the scene.
  - Checks: `--backrooms` (`Tests/BackroomsChecks.cs`).
- **Runtime `NativeBackroomsPresentation`:**
  - Draws a layout from the bank, instanced through NativeBlockLit's pack path. The pack path needs an instanced
    `_BlockColor`, `_BlockRest` and `_BlockLook`; without them it draws black.
  - Dead lamps draw unlit, and the nearest lit panels feed the 8 practical light slots.
  - A DEV preview only (`NativeBackroomsPreview`, `--ca-backrooms-preview`), drawn at (1000, 0, 1000) and off by default.
- **Kit notes:**
  - Each ceiling panel has an open centre slot for a lamp, vent or the single tile `ceiling_panel2.002`.
  - `wall7_4` has window openings; Level 0 uses `wall2_1`.
  - Lamps glow only with `_ArenaPackGlow` at 1.
- **Seen in Unity:** offline captures from inside the preview.
  - Right: the wallpaper, carpet, panel rows, pillars, partial walls and vents.
  - Wrong, and owed to the preview's Brickfall look:
    - the ceiling reads navy (toon shade on faces away from the map's sun);
    - it is overexposed near the lamps;
    - the lamps and vents cast small sun shadows on the floor.

## The level's look (still to do, with its map)

- A map look for each Backrooms map (`Conversion/Resources/MapLooks.json`):
  - no key light, or one straight down at a low level;
  - a warm shade tint, so the downward ceiling isn't blue;
  - a warm trilight ambient;
  - `_ArenaPackGlow` at 1;
  - a soft yellow haze or fog.
- Light levels tuned there.

## Next steps

- **Astra:** review the layout contract and the generator rules above, then build the generator, collision, nav and spawns.
  Also decide how the destination and the PvP map enter the rotation.
- **Claude:**
  - extend the bank tool for per-submesh materials and bake the kit;
  - a DEV preview of a layout, standing in for the generator until it lands (as the hub preview did), drawn with the kit;
  - lighting and the first Unity captures.
- **Owner (done 2026-11-02):** Blender is installed. The add-on's exports provide Level 0's and the Poolrooms' textures.

## Seeded Level 0 rules (Astra, 2026-11-02)

Implemented in `Core/NativeBackroomsGenerator.cs` and `NativeBackroomsExpedition.cs`.
Uses Claude's current `BackroomsLayout` contract from shared `49281882`: 2.4 m cells, 2.7 m ceiling, 0.14 m walls and centred 1 m doorways. His seeded enclosed-room topology is extended with production placement and reachability validation.

- **PvP:** map `backrooms-level-0`, 16 × 16 cells (38.4 × 38.4 m), fixed seed 647031,
  selectable and in Random rotation. Eight graph-spread spawn cells; no pits in PvP.
- **Expedition:** area `exp-backrooms`, map `expedition-backrooms-level-0`, 40 × 40
  cells (96 × 96 m). The deployment pool rolls three destinations from the three
  authored interiors plus Level 0. Seed is the ticket's reconstructed `worldSeed`
  XOR `0xbac00001`, interpreted as a signed 32-bit integer. No peer chooses geometry.
- **Layout:** deterministic enclosed rooms and corridors from the shared topology, with
  seeded production placement. Pillars and optional pit cells are accepted only if every
  remaining navigable cell stays connected. Short walls form centred 1 m doorways;
  pilot/enemy navigation uses these and full-width openings. Entry room holds eight party slots.
- **Physics/nav:** solid ceiling at 2.7 m; full/short wall boxes, slim pillar
  boxes, floors and five-metre pit shafts. The kill plane is at -4 m. Cell-centre nav
  links exclude pillars and pits. Existing Expedition closure boxes supply the
  return-door collider and follow the replicated encounter state. Navigation excludes that doorway and is repaired to remain connected while it is closed. No new door art
  contract. Small-enemy and pilot routes are checked; Hulks are excluded because
  the 2.7 m ceiling cannot accommodate them.
- **Encounter:** existing 36-kill Expedition quota, shambler/runner/spitter/brute
  population, then reach the distant exit cache. Authority requires a living,
  connected pilot in range with line of sight and no load in progress. Completion
  uses the existing once-only encounter reward ledger (`level0-exit`); the normal
  cleared-instance return path opens. The exit cache also uses the existing chest
  interaction/economy. No fake Siren entity is spawned for this objective.
- **Replication:** ticket reconstruction includes the generator version and full
  layout identity in the bundle compatibility receipt. Existing area snapshots,
  closures, rewards and cohort transitions carry progress, completion and reconnect
  state. PvP content compatibility includes the generator version.
- **Presentation hook:** the live map feeds Claude's existing procedural mesh builder at world
  origin. PvP caches its fixed layout; Expedition uses the run bundle's layout.
  With the bank installed, kit-replaced collision blocks are hidden (pit shafts
  remain drawn). Without the bank, ordinary visible map blocks are the fallback.
  The DEV preview is still available separately.

**Claude handoff:** both map IDs above are ready for map looks, exit/entry signage,
objective guidance and art review. The rules do not change kit piece geometry,
pod timing or the fluorescent circuit design. The live map uses Claude's existing interior lighting while inside the maze; a dedicated map look remains pending. Fluorescent hum is not implemented. Verify ceiling,
short-wall gaps, pillar corners and pit edges against the baked bank on Windows;
no local GPU or two-client playtest was available to Astra.

## The Poolrooms (Claude, 2026-11-02; presentation contract)

The add-on's "poolrooms" archetype rebuilt as our generated level: white tile, still water, wide quiet halls.

- **Proportions:**
  - 2 m cells; a 3.6 m ceiling at its lowest, rising 1.2 m a step by room (a deep pool's room at least one step);
  - 0.3 m tiled walls;
  - round 0.7 m pillars on a 2-cell pitch in the halls;
  - round 0.9 m lights over every third cell both ways.
  - Openings between rooms are 2 or 3 cells wide, under elliptical arches springing at 1.5 m below the lower ceiling.
  - Pools: deep 1.3 m, wading 0.4 m, the water 0.15 m below the deck. A deep pool's steps are four treads from the deck to its
    floor.
- **The layout contract** (Core `PoolroomsLayout`, pure):
  - per cell: its floor (`Deck`, `Shallow`, `Deep`, `Steps`), which way its steps descend, its ceiling's height, a pillar,
    a light and its room;
  - walls on the cell edges (`Open` or `Wall`; the outer border always walled);
  - the entry cell.
  - `FloorAt` gives the floor's height anywhere in a cell (a tread on a steps cell); `WaterY` is the surface.
  - An open edge between two rooms is an opening, arched by the presentation.
- **The stand-in generator** (`NativePoolrooms.Preview`, deterministic from the seed):
  - the grid split again and again along its longer side into rooms of 4 to 10 cells a side;
  - each room a deep pool (about 38%), a wading pool (18%) or a hall (the entry's always a hall);
  - a pool inside a deck one cell wide, never touching a wall or an opening; a deep pool's steps on one of its sides, off
    its corners, descending inward;
  - the rooms joined by a random spanning tree of openings, plus about 35% more for loops.
- **The geometry** (`PoolroomsMesh`):
  - tiled decks; pools lined in turquoise tile, with walls from the deck down to the floor, tread by tread beside the steps;
  - the water's sheet over every pool cell;
  - wall runs to the higher ceiling; an arch's soffit and both spandrels;
  - plaster ceilings; round pillars; round lights.
  - It builds in blocks of 8 × 8 cells, each edge owned by one block.
- **The water** (`Resources/NativePoolWater.shader`):
  - transparent;
  - two drifting samples of the add-on's water normal map;
  - a Fresnel reflection of the room;
  - a sharp highlight from each of the practical lights (the nearest pool lights);
  - the ground haze and fog.
- **The DEV preview** (`Runtime/NativePoolroomsPresentation.cs`): off by default (`--ca-poolrooms-preview`), drawn at
  (-1000, 0, 1000), 30 × 30 cells. It draws only the blocks near the eye, and nothing without the bank's textures.
  - Inside, until the level has its own map look: a cool-white trilight ambient, a pale haze, the nearest lights in the
    practical slots, the sun and the toon style off.
- **Checks:** `--poolrooms` covers determinism, a walled border, connectivity, pools inside their decks, steps leading in, one
  flight per deep pool, one ceiling per room, faces wound out, the water's level, arches under the ceiling, blocks, the lights
  and the preview's wiring. `--poolrooms-map SEED W D` prints a layout as text.
- **Seen in Unity** (DEV captures at eye height, seed 7): tiled halls and pillars, arches between rooms, rippled turquoise
  pools with the steps visible under the water.

### The water's waves (owner 2026-11-02: "can we make sure to add fluid water physics?")

Split as usual. The water's motion and look are Claude's (built, below). What water does to bodies (wading, swimming, buoyancy,
climbing out) is Astra's rules (the handoff below).

- **The wave field** (Core `PoolWaves`, pure):
  - a 128 × 128 grid of 0.25 m texels (32 m) that follows the eye in whole texels, carrying its waves;
  - a damped wave equation stepped at 60 Hz (at most 4 steps a frame), waves moving 1.5 m/s and losing about half their
    height a second, no wave over 0.16 m;
  - only texels over pool cells are wet. A dry neighbour mirrors the wet one, so a pool's sides reflect the waves and no wave
    crosses a deck.
- **What disturbs it** (`NativePoolWaves`):
  - a body standing in the water (feet under the surface, within 1.8 m of it) and moving leaves a wake;
  - a body dropping through the surface faster than 1.5 m/s splashes, harder up to 8 m/s;
  - a shot striking a pool (an `Impact` or `Ricochet` event on or under the surface) splashes a little;
  - a blast over a pool (an `Explosion` event) splashes hard, wider with its radius.
  - Bodies are the arena's pilots and minions, read from their positions each frame. No new replicated data: every client
    makes the same waves from what it already sees.
- **The spray** (`PoolSpray`): each splash throws a crown of up to 34 drops up to 4.2 m/s, falling under gravity; a drop that
  falls back through the surface lands as a small ripple. At most 320 drops. They are drawn as small water-coloured
  diamonds stretched along their flight.
- **The surface** (`NativePoolWater`): the heights arrive each frame as a float texture over the field's square. Their slopes
  tilt the surface, which carries the room's reflection and the lights' highlights. Steep or raised crests turn to foam.
  The add-on's normal map adds a calmer base ripple.
- **DEV:** the preview takes pokes (`NativePoolroomsPreview.Pokes`) and a stand-in wader walking between two points, for
  captures before the map exists.
- **Checks** (`--poolrooms`):
  - wet texels only over pools; the deck never disturbed;
  - an entry's speed threshold; rings travelling out and dying away over seconds;
  - no wave crossing a deck into another pool; the same calls giving the same waves; waves carried when the field moves;
  - a hitch capped; the height limit;
  - wakes only when wading and moving; shots and blasts;
  - the spray falling back as ripples and keeping to its cap.
- **Seen in Unity** (DEV, seed 7): a stand-in wader's V-shaped wake, rings spreading from two splashes and reflecting off the
  pool's sides, the spray's drops.
- **Not yet:** an underwater view (a camera below the surface) and bodies bobbing at the surface wait for the swim rules. So do
  the splash sounds (existing `impact.water`, a wading loop).

**Astra handoff (water rules), from the owner's "fluid water physics":**
- Wading in a wading pool (0.25 m of water over its 0.4 m basin) or on a deep pool's steps: slower movement, no change to
  jumping or weapons.
- Deep pools (1.3 m basin, the water 1.15 m deep): the rules choose between wading chest-deep and swimming at the surface. If
  swimming: buoyancy holding the body at the surface, slow strokes, sinking when it stops, the steps or the edge to climb out
  (a ledge pull-up at the deck).
- Whether shots and blasts travel through water, and what water does to enemies (the Expedition's zombies) and to Hulks
  (the 3.6 m ceilings).
- The presentation already reads positions and the existing `Impact`, `Ricochet` and `Explosion` events. A swimming state on
  `Actor`, when it exists, would let it bob the body and draw the surface line on it.

**Astra handoff (rules):** the Poolrooms need a map and a production generator (this contract, or an extension of it), collision
and navigation, spawns, and the water's rules. Wading (0.15-0.25 m of water), deep water (swim or sink, climbing out by the
steps or the pool's edge), what water does to movement, weapons and enemies, and whether Hulks fit under the 3.6 m ceilings are
all the rules' call. The presentation only needs the layout. A map look and the level's sounds come after the map.


## Poolrooms production rules (Astra, 2026-11-03)

`NativePoolroomsGenerator` builds the production collision and navigation from `PoolroomsLayout`.
The fixed PvP map is `backrooms-poolrooms` (seed 647032, 24 × 24 cells), included in Random.
Expedition can select `exp-poolrooms` / `expedition-backrooms-poolrooms` (32 × 32 cells,
seed `worldSeed xor 0xbac00002`) among its three destinations. It uses the existing quota
then exit-cache encounter, with the separate `poolrooms-exit` once-only reward objective.
The catalogue now offers five destinations. Authenticated tickets reconstruct the layout on
both peers; generator version and generated identity participate in compatibility checks.
Both map IDs use the owner's `poolrooms-untitled` music.

- **Geometry:** 2 m cells, room ceilings at 3.6 m or higher, 0.3 m walls, 0.4 m shallow basins,
  1.3 m deep basins, and the surface at −0.15 m. Four 0.325 m risers match each deep flight.
  Collision uses solid basin/deck blocks and real ceilings. Arch collision conservatively
  subdivides the presentation's polygonal soffit into thin boxes; the edge can feel slightly
  tighter than the visible curve. All shells are indestructible. Production halls omit the
  preview's optional pillars so cell-centre navigation never targets a pillar's centre.
- **Navigation/spawns:** eight distinct dry player positions; Expedition's entry slots are
  grouped near its entry. Enemies spawn on dry decks at least 12 m from every player slot.
  Deck routes reach the exit without crossing a pool and avoid the return door’s closed edge. Pool routes include all four treads;
  no navigation edge tells an actor to climb a 1.3 m vertical basin wall. In this map only,
  the ground motor permits a 0.42 m step, allowing the shallow pool's 0.4 m lip; other maps
  retain their 0.35 m limit. Normal zombies use the same step clearance checks.
- **Movement decision: chest-deep wading.** These basins hold only 1.15 m of water, so bodies
  remain grounded. The speed multiplier is `1 − 0.5 × clamp(depth / 1.15, 0, 1)`: 0.8913 in
  shallow water and 0.5 on a deep floor (ordinary 6.8 m/s walking becomes 6.06 / 3.4 m/s).
  Steps progressively reduce immersion. The same rule feeds authority and guest prediction,
  ordinary zombie movement and lunges. Gravity, jumps, roll/ability bursts and knockback
  retain their existing rules. Steps provide the reliable deep exit; a normal jump can also
  clear the ledge. There is no swim mode, buoyancy, oxygen meter, drowning or pull-up action.
- **Combat:** water is not a collider or cover. Shots retain their speed, gravity, ricochets
  and damage; blasts retain their radius, falloff, impulses and ordinary solid obstruction.
  Tile walls, steps and basin lips still obstruct hits normally. Water grants no electrical
  bonus, fire immunity, damage reduction or automatic status cleansing. Existing impact and
  explosion events continue to drive Claude's waves and spray, without changing gameplay.
- **Enemies/Hulks:** shamblers, runners, spitters and brutes wade with depth-based slowdown;
  water does not kill or pacify them. The 3.06 m Hulk capsule fits under a 3.6 m flat ceiling,
  but its 0.954 m radius and low arch edges are not certified by these routes. Hulks are
  excluded from this destination's spawn allowlist. Hulkamania substitutes brutes here so it
  cannot stall the quota; its spawn cap/interval remain, without Hulk kill payouts. No
  wall-breaking exception is added.
- **Presentation hook:** `Actor.Wading`, `WaterDepth` (metres above feet), `Waterline` (world y)
  and `Swimming` are available. `Swimming` is false for this grounded ruleset. Authority and
  guest render snapshots derive the hook from the same layout and body position; no new wire
  payload is needed. The live renderer consumes `arena.Poolrooms` at the world origin; the DEV
  preview remains separate. Claude retains ownership of body bob, waterline shading and audio
  polish. Wave displacement is visual and never changes collision depth or movement speed.

Managed layout/route checks and pinned Unity compilation are required below; native Windows
step traversal, underwater cameras, visual arch alignment and two-client play remain separate
acceptance checks. Passing managed tests does not establish those results.
