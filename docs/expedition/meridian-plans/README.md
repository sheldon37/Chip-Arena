# Meridian size-B street plans: build notes for Astra

Five hand-designed layouts that replace `meridian-b-candidate-2`. Claude drew them (2026-11-19) after the owner's
walk ("these all look like the same pathing"). Nothing here is in the repository. The specs are design data for you to
build into the City Layout Contract (A4) and to review.

| Layout | Spec | Plan | Aerial |
| --- | --- | --- | --- |
| 1. Crown Hill | `crown-hill.json` | `crown-hill-plan.png` | `aerials/crown-hill-aerial.png` |
| 2. The Navigation | `navigation.json` | `navigation-plan.png` | `aerials/navigation-aerial.png` |
| 3. The Arcades | `arcades.json` | `arcades-plan.png` | `aerials/arcades-aerial.png` |
| 4. The Stride | `stride.json` | `stride-plan.png` | `aerials/stride-aerial.png` |
| 5. The Yards | `yards.json` | `yards-plan.png` | `aerials/yards-aerial.png` |

Also here: `comparison-sheet.png` (all five plans and the 20 exits), `exit-check.json` (the 20-exit test result) and
`src/` (the generator that writes all of it).

## Format (`meridian-street-plan/1`)

The axes are the game's: x east, z north, y up, in metres. **The camp is unchanged in every layout:** the 49.6 m
square at the origin on y 0, with its four 10 m gates at N (0, 25), E (25, 0), S (0, -25) and W (-25, 0). **What moves is
the map bounds**, so the hub's place in the city changes without touching `NativeExpeditionHub`.

| Key | What it is | Exact or rule |
| --- | --- | --- |
| `bounds` | minX, maxX, minZ, maxZ (always 600 × 480, asymmetric about the camp), ceiling, killY (lowest floor − 6) | Exact |
| `tiers` | Named ground heights used by the layout | Info |
| `hub` | The camp rectangle, the gates, and `precinct` rects: the hub's footprint on y 0, kept clear of lots | Exact |
| `ground` | Terrace boxes: x0..x1, z0..z1, bottom..top. A greedy merge of a 1 m height raster, so every retaining wall is a box face | Exact |
| `ramps` | MapRamp records: `kind` stair or ramp, `low` and `high` centre points, `width`, `bottom` (low.y − 0.25), `toward`, footprint. Your `Ramp()` makes the treads (0.25 m stair, 0.5 m ramp). Stairs come as flights of 3.5 m rise at 26.6° with 3 m landings, and the landings are in `ground` | Exact |
| `blocks` | ContentBlocks: x0..x1, z0..z1, bottom..top, so `pos` = the centre and `size` = the extents. Each has a `role` (below) and, for roofs, `roof` (walkable/pitched), `hatch`, `roof_link` | Exact |
| `routes` | One per gate (`n` `e` `s` `w`): `points` [x, y, z] polylines (horizontal between points, so a sloped leg's ends carry its heights), `segments` with heading, length, width, kind (flat, stair, ramp, bridge, roof), cover and y0/y1, the walked `length`, and `exit60` (the first 60 m signature) | Exact |
| `doors` | Instance doors `a` `b` `c`: the point on the hall face, `facing`, `level` (ground, raised or sunk), the 10 × 9 m mouth, the `hall` block, the plaza rect, and the bleed radii (30/60/110/160 m) | Exact |
| `court` | The final court: the walled yard (7 m walls, a 12 m gateway with a lintel at +10), the City Hall block and the tower (12 × 12 m, 62 m above the court, rising above the ceiling as the art bible allows) | Exact |
| `links` | Pilot-only links, off the horde graph: `fire-escape` from a street foot `a` to a roof `b`, 1.5–3 m wide | Endpoints exact; build each as zigzag stair flights on the named face |
| `landmarks` | Named points for the dressing pass and the wayfinding checks | Info |
| `lamps` | A lamp point about every 12 m along each route, alternating sides (art data) | Exact positions |
| `fill` | The zones and styles that made the lots (rule, seed) | Rule. The resulting lots are already explicit in `blocks` |
| `checks` | Reach results, lot and block counts | Info |

**Block roles:** `lot` (a building), `hall` (an instance door's building), `city-hall`, `tower`, `court-wall`,
`court-gate`, `bridge` (deck 1 m thick), `viaduct` (the Stride's deck), `pier`, `parapet` (1.1 m), `wall`, `post`,
`arcade-roof` and `lid` (a slab over a passage with at least 3.2 m headroom: arcades, the Galleria glass, the Undercroft
roof, the jetties, the covered footbridge), `tunnel-roof`, `wagon` (3.2 m parked wagons, cover), `landmark`, `shed`, and
`cordon`.

**Slots:** route `n` always leads to the final court, as `DealHalls` slot 0 does today, and City Hall stays north (art
bible 5.5). Doors `a`, `b` and `c` are the E, S and W routes, i.e. `DealHalls` slots 1–3. Each layout has one ground, one
raised and one sunk instance door:

| Layout | Court | A (E gate) | B (S gate) | C (W gate) |
| --- | --- | --- | --- | --- |
| Crown Hill | +28 | raised +14 | ground −7 (the Quay, lowest street) | sunk −7 (the Hill Cutting) |
| The Navigation | +7 | sunk −14 (canal bed) | ground 0 | raised +14 |
| The Arcades | 0 | ground 0 | sunk −7 (the Undercroft) | raised +7 (on the Leads) |
| The Stride | +14 | raised +7 (viaduct deck) | sunk −14 (the Goods Cut) | ground −7 (lowest street) |
| The Yards | +14 | sunk −14 (Main Line Cut) | ground 0 | raised +14 |

Every hall is the same kind of neutral mass (30–44 m wide, 18–22 m deep, 16 m tall, with a 10 × 9 m mouth), so any of
the five instances can take any door. The influenced district (R12) is dressing on top of it.

## What is exact and what is a rule

- **Exact:** the ground, ramps, routes, doors, court, bridges, the viaduct, arcades, lids, parapets on bridges and the
  viaduct, the landmark blocks, and every lot as listed. Build them as they are.
- **Lots:** made by the fill rule in `src/meridian_plan.py`.
  - Rows, shops and tenements are perimeter blocks: street walls round a courtyard, 10–12 m streets, at least one passage
    into every courtyard.
  - Market, warehouse, villa, civic and shed zones are a seeded grid.
  - A final pass removed any lot that sealed a pocket. The lots in `blocks` are the output, so take them as listed.
  - If a route moves, rerun the rule rather than nudging lots by hand.
- **Rules you apply while building:**
  - **Parapets.** A 1.1 m parapet on every walkable edge of a route with a drop over 2 m, except where a drop is the
    design. There is no fall damage, so the Promenade's balustrade can be jumped. Bridges and the viaduct already have
    theirs.
  - **Roof hatches.** Every block with `hatch: true` gets a roof-hatch spawn entry (R11, no safe roofs).
  - **Lamps** stand where `lamps` lists them. Keep spawn entries off the door approaches, as today.
  - **The Cordon.** `cordon` blocks stand 14 m above the highest ground along each edge. Add watchtowers every 60 m as
    today.
- **Walkable roofs reached on foot:** roofs level with the next terrace's street (`roof_link: step-*`), arcade roofs,
  roofs on the Leads, and fire escapes. Other walkable roofs are chip-only, which is honest, and carry hatches.

## New primitive

**None is needed.** Everything is axis-aligned boxes and axis-aligned MapRamp wedges, because `Ramp()` lays its treads
along one axis. There are no diagonal streets: the variety comes from topology, levels and cover, not angles.

- Glass (the Galleria) is a plain block drawn as glass.
- The canal is drained, so its bed is ordinary ground.
- Optional, only if the owner wants diagonal streets later: a yawed box/ramp. It is not used here.

## Checks run here (Python, `src/`), and their results

| Check | Rule | Result |
| --- | --- | --- |
| Route length | 250–300 m walked, gate to door or gateway | 255–297 m, all 20 |
| Route ends at its door | Last point within 0.5 m of the door point and at its height | All 20 |
| Route floor and headroom | Every 1 m of every route stands on a box top or ramp surface within 0.35 m of the route's height, with 3.2 m of Hulk headroom | 0 problems |
| Ramp grades | Stairs 0.5 (26.6°), at least 3 m wide; sloped streets 1:8 to 1:6 (and 1:7); haul ramps 1:4, at least 10 m wide | As built |
| No trapped surface | 2 m raster of every standable surface (1.8 m headroom). Walk or step up to 1.1 m, drop any height, running jump across 4 m, plus fire escapes. Every surface must lead back to the camp on foot | 0 trapped cells in all five |
| Roofs on foot | Walkable roofs the camp reaches without chips | 47/79, 78/117, 163/222, 52/87, 48/104 |
| 20-exit difference | The first 60 m of every exit, sampled every 2 m. Every pair of the 20 must differ in at least 2 of 5 attributes: level profile (RMS ≥ 1.5 m), turn pattern (sides, or turn stations more than 10 m apart), main width (≥ 3 m), cover, and the approach sequence (flat, stair+, stair−, ramp+, ramp−, bridge, roof) | 190 pairs, 0 failures. No two exits share both approach sequence and turn pattern. Closest pairs are in `exit-check.json` |

**Suggested ports into CoreTests** (LayoutContract checks):
- the 20-exit difference rule above, on `exit60` profiles taken from the built geometry;
- route length and door-on-route;
- route floor and headroom on the real colliders;
- a no-trap reach sweep over the generated nav, including roofs.

They are cheap and catch a template creeping back in.

## Size and budget

| Layout | Ground boxes | Other blocks | Ramps | Lots | Lots ≥ 19 m | Facade area (lots) |
| --- | --- | --- | --- | --- | --- | --- |
| Crown Hill | 92 | 217 | 28 | 190 | 14 | 141k m² |
| The Navigation | 89 | 203 | 31 | 157 | 43 | 168k m² |
| The Arcades | 49 | 465 | 12 | 236 | 29 | 197k m² |
| The Stride | 73 | 316 | 26 | 237 | 27 | 169k m² |
| The Yards | 79 | 169 | 26 | 62 | 11 | 68k m² |

- The Arcades' block count includes 135 skybridges and the arcade posts.
- Stair treads are not counted here, because `Ramp()` adds them.
- **Estimate, not measured:** candidate-2's uniform 25 m grid holds up to about 330 lots of 10–15 × 15 m, about 300k m²
  of facade. These plans hold 23–66% of that.
- Sight lines are broken by tiers, cuts, the viaduct and covered streets, not by filling every lot.
- The real gate is the measured worst view (High 2,177,224 / Low 821,492 triangles) on the greybox, with art. Measure it
  per layout before freezing.

## Not checked here

- Not loaded in Unity.
- No Hulk-radius nav graph, node counts, crowd timing or sim benchmark (A4 asks for those on the B layouts).
- No triangle or draw counts, and no spawn placement.
- The reach sweep is a 2 m raster approximation of pilot movement, not native pilot physics.
- Fire escapes are links, not built geometry.

## Regenerating

```
cd src
python build_all.py      # JSON specs, plan PNGs, the checks above (prints any failure)
python exits.py          # the 20-exit test (exit-check.json) and comparison-sheet.png
blender -b --factory-startup --python blender_aerial.py -- ../<layout>.json ../aerials/<layout>-aerial.png 96
```

Blender 5.2.3 is at installed on the owner's PC. It renders on the GPU in
about 10 s per layout. The coloured route ribbons in the aerials are for reading only, not game art.
