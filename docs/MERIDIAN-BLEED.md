# Meridian: the bleed near each instance door

The owner asked for streets "influenced by the instance that is generated", so that players "know they're near an instance when we start seeing the 'influenced' designs". This is plan R3/R12 and W7 (`docs/EXPEDITION-OVERHAUL.md`), on the Developer walk's `meridian-b-candidate-3` layouts. It is presentation only. It adds no collider, rule, record or wire field.

## The bands: `Core/NativeCityBleed.cs`

`NativeCityBleed` gives each door's share at a place and the band it falls in. It reads only the door records: each door's route from its gate, and the instance the hall deal put behind it.

- **Distance.** A door's distance is measured along its own route. From a place, the cheapest way to the door is to walk to a point on the route, then along the route. The first 10 m off the route cost their own metres; each metre beyond that costs 2. So a street and its facades read the route's own metres, and a parallel street behind the blocks reads about one band further out.
- **Exact and seamless.** The minimum over each segment has a closed form, so the field is exact. It is continuous everywhere, with no cells. A 25 cm step changes a share by at most 0.005; the plan allows 0.08.
- **Share.** `ShareAt` is piecewise linear through the plan's band edges, and `Band` maps a share to its band:

  | Band | Share | Route metres from the door |
  | --- | --- | --- |
  | Threshold | 0.70 and over | 0-30 |
  | Warp | 0.45-0.70 | 30-60 |
  | Drift | 0.25-0.45 | 60-110 |
  | Rumour | 0.10-0.25 | 110-160 |
  | None | under 0.10 | beyond 160 (0 at 200) |

- **Other calls.**
  - `At(p)` gives the dominant door, its share and band.
  - `Theme(instance)` gives the theme id: `level0`, `poolrooms`, `wardzero`, `station`, `ironworks` or `final`. It takes the walk's names or the run's area ids.
  - `ThemeAt(p)` serves the street planner's `NativeMeridianDress.ThemeAt`. It returns the dominant theme from the drift band on, with a 0.03 seeded dither across the edge (6 m cells). Call it once per facade.
  - `ReplacesLamps(p)` is true from the drift band on, where the bleed's own lamps stand instead of the city's.
- **Ownership.** Thresholds are versioned `meridian-bleed-1`. Astra owns the class from A5a. Presentation may smooth on top of it but never changes `At` or `Band`.

## The approach dressing: `Core/NativeCityBleedDress.cs` and `Runtime/NativeCityBleedDraw.cs`

The planner walks each instance door's route from the band's outer edge (165 m) to the door front's line, 12 m out, where D2's door front begins. It plans pieces from the bank `ChipArenaCityBleed` (built by `tools/stages/meridian_bleed.py`). Every piece keeps the city's placement rules:

- **Floor paint:** 6 cm or less. Every corner lies on a floor's top, on a 2 m world grid. Paint never lies on paint.
  - **Fields** (Level 0's carpet, the Poolrooms' tile and pools, Ward Zero's vinyl, the Station's platform tile, the Ironworks' plates) come in 0.5 m sub-tiles. A cell whose neighbour is outside the field is ragged on that side (an edge or a corner variant, turned to face the gap). A cell between two gaps falls to scattered bits, and bits scatter a cell beyond the rim. So a field thins tile by tile and is never a hard rectangle; the threshold stays solid but its rim is broken too.
  - Whole cells are plain, because an instanced cell repeats. The accents (damp, worn vinyl, grime, maroon tiles, rust) ride the ragged cells.
  - **Stains and cracks** take the cells no field took: damp stains, puddles with a glint, worn decon lane paint, oil and cracked setts, cracks and soot. They are thickest in the warp, and a few appear in the rumour.
- **Wall pieces:** set on a collider face that is found from the street and is continuous and in sight across the whole bay. They are flat (6 cm or less) below 3.1 m over the floor; lamps, signs, sheeting and the upper skins stand above it. Bays beside ramps and stairs take only signs and posters, a metre or more over the floor.
- **Spans:** cables between two facing walls, with every point 3.1 m or more over the floor below.
- **Level 0's ceiling:** rows 2.5 m deep carried wall to wall between two facing walls (scaled to their gap), over its warp crossings and its whole threshold. The rows sit at 5.6 m, lower under lower walls but never under 4.4 m. Only where walls hold the ceiling on both sides: nowhere does it float, and nothing dangles. On candidate 3 that is The Arcades' south approach. Elsewhere Level 0 gets a troffer every 6 m on the walls it has.
- **Keep-outs:** nothing within 3.3 m of a join circle (8 m out from each door), and nothing inside a door front (the hall's frontage and 12 m in front of it).

Each instance's set:

| Instance | Drift | Warp | Threshold |
| --- | --- | --- | --- |
| Level 0 | Carpet creeping in, damp stains, EXIT signs, troffer span wires and troffer lamps (dying circuit by circuit) | Wallpaper and skirting on the walls, missing-brick panels, damp carpet, the ceiling over walled crossings | Wallpapered windowless walls, carpet underfoot, the ceiling between walls |
| The Poolrooms | Glowing storm drains, puddles, NO RUNNING plates, glass block, aqua pendants | White pool tile underfoot, tiled doorways with no rooms, pool lamps set low in the walls, lane ropes and pennants, rings in the flood | White tile walls; tile and pools underfoot (dark in the middle, shallow toward a tiled rim, caustic threads shimmering); steady light (no lamp dies) |
| Ward Zero | STAY INDOORS posters and leaflets, worn decon lane paint, PA horns, lamps and pendants in the cold flood #D4E6FF, turning red with the share | Containment growth (violet #A04CFF), quarantine sheeting above 3.1 m, decon tents over the street, every lamp red and turning, worn pale clinical vinyl underfoot | White cladding with the teal band (teal only as a band or a stripe, never the floor), sheeting above |
| Central Station | Tram rails, oil stains and cracked setts, roundels, signal heads as street lamps, catenary with droppers | Blue brick, a fan of tracks | Aged cream and green tile walls; aged cream platform tile with grime and the railway's maroon at its ragged rim |
| City Ironworks | Ash drifts, cracks and soot, roller doors, SAFETY FIRST plates, caged work lamps and work-lamp pendants | Riveted plate walls, slag in the gutters (faint, steady cracks: never a fake hazard), pipe bridges, sodium high-bays | Furnace brick, steel floor plates, a crane girder with its hook |

Rumour signs come every 36 m or so. Ward Zero's EVACUATION ROUTE signs always point away from the door.

### The light

- The lamps' colours are the art bible's (3.2): Level 0's fluorescent #F2EE9A, the Poolrooms' aqua #52E4E0, Ward Zero's alarm red #FF2A30 and cold flood #D4E6FF, the Station's signal red and green, the Ironworks' caged work lamp #FF9A30.
- About one lamp every 12 m, alternating sides. One in five is dead and one in ten flickers (the owner's rule), by the run seed.
- On a lower wall the bracket drops from 4.6 m to 3.9 m, still wholly over the clear height.
- In the drift, cables between facing walls up to 32 m apart carry a pendant lamp in the instance's light.
- Exceptions: Ward Zero's beacons turn, and the Poolrooms' threshold never fails.
- Each working lamp lays a soft pool on the floor.
- Over each instance door, the cloud deck glows in the bible's sky colour for it (an additive glow with no fog; Ward Zero's red over violet), so the rumour band reads from the camp.

### The atmosphere

Fog colour, ambient colour and (at the Ironworks) fog distance drift toward the dominant instance's mood by the camera's share, eased over about a second. The Poolrooms' threshold brightens: it is the one bright, calm place. The drawer gives the look back when it closes.

### Positions and the run seed

Positions follow the blocks and routes, so they are fixed per layout. The run seed picks variants, patches, and which lamps are dead or flicker. The plan is deterministic per seed and deal.

### Budget and fallback

- Up to about 600 pieces a walk, 230 within 60 m of an eye, and 74 instanced draws (at most 500 instances a draw).
- The bank is about 1.8 MB.
- Without the bank (CI, a build without the Drive zip, `--ca-no-pack-banks` or `--ca-no-city-bleed`), `NativeCityBleedDraw.Open` returns null and today's greybox draws unchanged.

## Themed module variants (for D1's street planner)

The bank also carries each theme's drift skins for the street planner's modules, as `module/<key>@<theme>/<tier>` (group `module`): Level 0's beige stucco offices with ribbon windows lit flat yellow, the Poolrooms' white tile and glass block, Ward Zero's white cladding and teal band, the Station's cream and green tile with arches, and the Ironworks' buff brick and roller doors.

- **Shapes:** four per theme (ground bay, door bay, upper bay, roof-line band). They are shared by every key of the planner's families as of `claude/meridian-dress` `b56b3e2b` (`VARIANT_KEYS`).
- **Hug rule:** the ground and upper bays hug their face (0.25 m in to 6 cm out).
- **Covering the threshold:** `NativeCityBleedDress.Covers(plan, point, normal)` tells the planner where the bleed's own skins stand. The planner leaves its module out there, because the bleed substitutes; it does not add.

**Wiring (once `claude/meridian-dress` is on shared):**

1. Set `NativeMeridianDress.ThemeAt = p => walkBleed?.ThemeAt(p)` when the walk opens, and null it when the walk closes.
2. Have the planner's `has` and the drawer's module table also read `ChipArenaCityBleed`, so `key@theme` resolves.
3. Have the planner skip its plain lamps where `ReplacesLamps` is true, and skip its facade modules where `Covers` is true.

## The Developer walk's door-deal picker

Settings > Developer > Walk Meridian greybox.

- **Layout menu:** a row per door (East, South, West). Each press turns that door to the next instance the other two doors do not hold. "Doors as dealt" goes back to the walk's own deal.
- **Walk keys:** N deals the next of five deals that together put every instance behind every door. B turns the bleed off and on, to compare the greybox against the dressing. The walk's box shows the band and share at the pilot.
- **How the deal is made:** the picker never changes `NativeCityLayout.DealHalls`. It uses the first run seed that deals the chosen three in that order (`NativeCityBleedDress.DealSeedFor`). The walk then re-opens, so every drawer rebuilds.

## Enemies (Astra's contract)

The Developer walk has no zombies, so the outfits by spawn (W7) are not wired. For the live run, `ZombieModelOf(id, kind)` should choose `NativeZombieLooks.Of(kind, id, theme)` as follows:

- **Theme:** `NativeCityBleedDress.OutfitOf(NativeCityBleed.Theme(instance))` (office, pool, ward, station or foundry), where the instance is the dominant door at the minion's spawn entry. The minion frame's entry is at `NativeNetworkFrame.cs:15,187`.
- **When:** only at drift share or more; elsewhere the map's own theme.
- **Caching:** per id, so late joiners and the killcam agree.
- **Spawns:** the instance creatures' spawns stay Astra's rules (Q13).

## Moving onto the live run (Astra)

The bleed reads only door records. To carry it over, the live run needs:

- **The bleed:** `new NativeCityBleed(doors)`, built from the run's route polylines (gate to door; the door is the last point) and the hall deal.
- **The plan:** `NativeCityBleedDress.Build(map, bleed, streets, worldSeed)`, with each route leg's width and kind.
- **The drawer:** `NativeCityBleedDraw.Open(bleed, plan, map, camera, low)` on the hub's open, and `Close()` on leaving.

Keep the routes' leg kinds (`flat`, `stair`, `ramp`) and widths, and the join circles at 8 m out from each door.

## Checks

- `CoreTests --bleed` (`BleedChecks`): five layouts with five deals each (every instance behind every door).
  - The share curve, thresholds and themes.
  - No seams: at most 0.08 per 25 cm at random places on every floor.
  - Every door street reads the plan's distances.
  - Deterministic per seed and deal.
  - Every piece inside the placement rules, measured by the check's own geometry.
  - Paint never on paint.
  - The skins' `Covers` answers.
  - Coverage, budgets and the lamp rule.
- `--bleed-scene LAYOUT EAST,SOUTH,WEST SEED OUT` writes a walk's map and the pieces' places for `tools/stages/zfight.py` (the pieces as `rig/bleed/<key>`).
- `--bleed-probe LAYOUT ROUTE` and `--bleed-table` are diagnostics.
