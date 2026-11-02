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

Status: the kit bank, the layout contract with a stand-in Level 0 generator and a DEV preview drawn from the kit are
built (Claude, 2026-11-02; "Built so far" below). The level is not in the game: no map, generator, rules or look yet.

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

- **`BackroomsLayout`**, made from a seed and a level id. The same seed gives the same layout on host and guests: the
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
- **Owner:** whether Blender may be installed on the PC. The Poolrooms and the kit's packed textures are only in `.blend`
  files; Textures.rar needs a RAR reader.
