# Meridian's street dress (D1: the streets, the night and the lamps)

The approved Meridian look (board 3, the MERIDIAN art bible) drawn over the five `meridian-b-candidate-3` street plans in the
Developer walk (Settings > Developer > Walk Meridian greybox). Presentation only: no collider, rule, route, nav, spawn or wire
change. Without the city bank the walk draws its greybox exactly as before.

## Pieces

| Piece | Where | What it does |
| --- | --- | --- |
| The bank | `tools/stages/meridian_bank.py` on `meridian_kit.py` and `meridian_buildings.py` (board 3's approved kit, ported) and stagekit | 130 modules at LOD0 and LOD1 (`module/<key>/0`, `/2`): facade bays per typology, cornices, ghost signs, plates, road paint, puddles, hatches, the three lamp families in working, route and dead states, the 6:47 bracket clock, the Civil Defence siren, iron and stone parapets, the market column, the goods van, the viaduct's arch. Written as `ChipArenaCityMeridian.bytes` (3.4 MB). Never committed. A fill's faces turned down onto its floor and the clock's faces shut inside its drum are left out. |
| The dressing sheet | `tools/stages/meridian_sheet.py` writes `Core/NativeMeridianDressSheet.cs` | Each lot's style and roof, the plan's lamp points and the street names, from `docs/expedition/meridian-plans/*.json`. Rerun after a spec changes; `MeridianDressChecks` fails while a walk lot has no entry. |
| The planner | `Core/NativeMeridianDress*.cs` | Pure and deterministic per layout (the run seed moves only which lamps are dead or flicker). Every block role to modules and generated surfaces; the covered blocks; the lamps; the marks. |
| The drawer | `Runtime/NativeMeridianDressDraw.cs` | Combines into 32 m chunks (`NativeMeridianDress.DrawChunks`: a cell over the bible's caps, 60,000 triangles at LOD0 or 7,000 at LOD1, is cut in quarters by each piece's place until every part is under them): every chunk's LOD1 at load (in parallel batches, `NativeMeridianDress.CombineUnits`), LOD0 on a worker within 38 m (High) or 24 m (Balanced), Low draws LOD1 only; a cell's chunks switch together, on the cell's bounds, LOD0 once all of them have it; chunks past the fog's end plus 25 m are left out. The night's light. |
| The walk hook | `Runtime/NativeMeridianDressWalk.cs` | One call in `OpenCityWalk` and one in `CloseCityWalk`. |
| The greybox | `Runtime/NativeArenaDressing.cs` | Leaves out each block the dress drew this frame (`NativeMeridianDressDraw.Hides`) on the maps `NativeMeridianDress.HidesGreybox` names: the walk's five layouts and the live run's generated map (`expedition-generated-city`, since PR #432 one deal's street plan under this dress). Drawn both ways, the greybox's face and the dress's surface lay in one plane (the live hub's z-fighting, 2026-11-20; `HubZFightChecks`). |
| The night | `MapLooks.json` `expedition-night`, written by `docs/arenas/scripts/expedition_city.py --maplooks`; `NativeMeridianDress.LookTable`'s one row, applied by the walk's hook | The walk's five maps draw in the night once the dress (and its lamps) is made; without the bank the greybox keeps the town's dusk. The live expedition's map keeps its look. |

## The rules it keeps

- Art is never more than 0.25 m short of a collider: generated surfaces lie on the colliders' own faces; a module's recesses
  are at most 0.25 m deep.
- Nothing stands proud of a collider below 3.1 m over the floor in front of it, except flat paint of 6 cm or less: a facade
  module placed lower than that is its hugging variant (every vertex between 0.25 m in and 6 cm out of its face; the bank
  flattens the relief into that band). Cornices, oriels, lamp heads, clocks and sirens hang 3.1 m or more over their floor.
- Every visible face of a covered block is drawn (faces beyond the map's bounds excepted; two overlapping coplanar faces are
  drawn once), and no block is covered that is not.
- No free-standing furniture: until Astra's prop blocks exist, lamps hang on walls and span wires or sit flush in low walls,
  and the 6:47 clock and the siren are wall mounted.
- D2's blocks are the landmarks' (`NativeMeridianLandmarks.FixedCovers`: the exits' host lots, the court and City Hall, the
  halls): no module, surface, lamp, plate or paint of the dress on them, and they are not covered here (the landmarks hide them
  while their bank draws). The other landmark blocks (barges, cranes, the keep, the bandstand) stay greybox.
- No seam is see-through (2026-11-20, once the greybox no longer stood under the dress on the live map). Each surface is cut
  into its own grid, so where two met, one's vertices lay part-way along the other's edge, and the pinholes showed the void (the
  toon outline drew each as a small dark cross). Now every surface edge on an axis-aligned line takes the vertices its neighbours
  put within it (`NativeMeridianDress.SeamTable`): within a 32 m cell at the tier it draws, across cells at both tiers (a cell at
  LOD0 stands beside one at LOD1). A cell whose side gains vertices is fanned. Shared vertices are snapped to one canonical
  coordinate per axis, so they are bitwise the same. The floor runs on under the camp fence (the greybox draws it chamfered, and
  its foot's groove showed the void). The views cost 4-13% more triangles; the load about 0.04 s more in the Editor (the seam
  table sweeps each shared line once, the lines in parallel, and allocates almost nothing: Mono's GC makes allocating threads
  wait for each other).
- Coplanar meetings never fight: a face of the dress in the plane of a face the greybox or the landmarks draw is left to them;
  roof copings butt at the corners; the bank trims each shopfront to its 2.5 m bay, so the bays of a storey butt; the rest part
  by a few ChipArena/StageLit layers (kept few: the shader's offset grows with each): the ground storey 0 (a shopfront's fascia
  stands over its top), the upper storeys 1, the cornices 2 and on x-facing faces 3 (two meeting in a re-entrant corner), a
  fill's tiles 0 and 1 in turn, what hangs on a wall 4; paint keeps its own 3. A cornice is left out where a block stands
  against the face at the roof line (a landing, a neighbour's roof).
- A flight's sides end on its line, never on its treads' flat tops (2026-11-20, the owner: "a lot of unnatural staircasing";
  `Core/NativeMeridianDressStairs.cs`). Every flight is a MapRamp: a pilot stands on a closed wedge from its bottom up to the slope
  (Runtime `NativeCyberCityExitCollision`), and its treads are boxes from that bottom up to each step. Drawn tread by tread, a
  sloped street's retaining edge and coping stepped every 3-4 m, a stair's cheek every tread, and a fire escape stood as solid
  stepped wedges in its host's brick, hanging from each landing. Now each tread side is cut on the flight's line (a sloped
  street's surface; a stair's nosings, which its treads already reach): its body (brick, stone, or a fire escape's grille) runs
  up to a 0.3 m band (a coping or the iron stringer) whose top is the line, as trapezoids (`Quad.Taper`). A wall beside a sloped
  street is drawn down under the street (its treads no longer hide it: they stopped it at their tops and left notches open over
  the setts).
- A fire escape is iron and honest: its wedge is solid, so it is caged, not drawn open. Under the stringer, flat bars 0.3 m apart
  (`BarStep`; no pilot passes the 0.26 m gap, the eye does), one pattern down the whole run, with a foot rail; a grating under the
  flight; the stair's own sloped soffit behind them; the floor runs on under the cage. The bars stand out of the seam table
  (`Quad.Free`: a bar meets no surface edge to edge). Open flights need thin colliders: the notes' `meridian-staircasing-astra.md`.

## The roles

| Role (ids) | Dressed as |
| --- | --- |
| `lot` (the sheet's lots) | Facades bay by bay (2.5 m, stretched to the face) and storey by storey (4 m ground, 3 m upper) by style: rows and villas (doors and parlour windows, sashes, oriels), shops and the market (their own trades' goods, shutters), tenements (barred flats, the lit entry and stair column, polychrome bays), warehouses and sheds (loading doors, iron windows), civic (rusticated arches). Blind faces get blind windows and a ghost sign. Partly hidden storeys get plain wall, and so does a bay given to the bleed's skins (they hang on it). A cornice under every roof line that hangs over a street; flat tar roofs with a coping and their hatches; pavements and kerbs (flat paint) along every street frontage. |
| `terrace-*` | Tops by zone (the grounds, below): the square, its setts border, asphalt roads, setts yards; engineering-brick retaining walls with a stone coping, weathering from the floor in front |
| Stairs (MapRamp `stair`) | Granite treads and risers on the built treads; the sides a stone cheek under a 0.3 m sloped coping along the nosings, never stepped (the flights, below) |
| Fire escapes | Iron: grating treads, risers and landings; a 0.3 m black stringer along the nosings; under it the flight's wedge, solid to a pilot, caged by flat bars 0.3 m apart, a grating under it and the stair's own soffit behind them; the floor runs on under the cage (the flights, below) |
| Sloped streets (MapRamp `ramp`) | A setts surface on the wedge the pilots walk; the sides a straight engineering-brick retaining wall under a sloped stone coping whose top is the street's edge; the walls beside it run down under the street (the flights, below) |
| Bridges, skybridges, the causeway | Plank or setts decks, green iron or stone girders and soffits; parapets as iron panels or a stone balustrade filling their boxes; street rails as iron railings |
| Arcade roofs, lids | Lead tops, dark glazing from below (the Galleria, the arcades), soffits |
| Arcade posts | The market's cast-iron columns |
| The viaduct | Ballast deck, engineering brick, the Stride's arches hung between its piers |
| Wagons | Goods vans |
| Sheds, the goods shed's walls | Buff brick with copings |
| The Cordon and its towers | Concrete with a dark band |

## The grounds

2026-11-21, the owner on Crown Hill's sanctuary plaza at night: "Theres very big empty areas, Like I feel the grounds should be
textured? Unsure if they're intended to look like this still."

- **Cause:** every ground block's top was one finish, the asphalt (#2E3038, no fine surface), so the hub's plaza (8,000-12,000 m2 a
  plan) and every yard read as one flat dark slab at night. The retaining walls' brick bond faded out by 42 m, and their grime and
  contact shade rose from the block's bottom, buried 9 m under the terrace in front.
- **Zones** (`Core/NativeMeridianGround.cs`): each open floor of a ground block is cut on the zones' lines and drawn in its zone's
  finish:
  - the square: the plan's `hub.precinct` at y 0;
  - its border: 2 m of granite setts round the square's outer edge;
  - the roads: the routes' flat legs at their level, asphalt;
  - the yards: every other open floor (back streets, courts, quays), granite setts.
- **The finishes** (`NativeMeridianSurfaces`; StageLit's `Ground`, kinds 22-26, on the dress's generated surfaces only). Each has a
  fine part that fades with the other fine surfaces (18-42 m), and a broad part kept at every distance, which is what reads from a
  third-person camera at night. Every pattern is filtered by the pixel's footprint: a joint eases into its mean, and a detail fades
  before it would alias.
  - The square (#7E776C, about seven times the asphalt's albedo): York flags in 0.6 m courses, each with its own shade, some odd or
    relaid. Bays are framed by granite-sett bands on the camp's 8 m grid (`SquareBand`: a band runs out of each gate). The lanes out
    of the gates are trodden paler and polished.
  - The asphalt: patches relaid in darker tar with sealed seams, tar-sealed cracks, stains, dry dusty fields.
  - The setts: each sett with its own shade, the joints dark and green where they stay damp, tar-filled repairs.
  - The pavements' slabs.
  - The retaining brick (#4E4450; the bible's 4.4): the bond, rain streaks from the coping (lime or soot), rising damp with a ragged
    edge, salt bloom over it, rebuilt panels, and weep holes every 1.8 m with their stains. Its uv1.y carries the floor in front, so
    the weathering starts at the visible foot.
  - On a wet night (the look's weather) the floors darken and gloss, and puddles stand on level floors only, with the sky's sheen
    and every city lamp's glint in them as a streak toward the eye.
- **Light:** the street lamps throw 3.6 x sodium down (3.0 before). The plaza's Civil Defence floods (street life) throw 13 m pools,
  their light reaching about 21 m out over the square (9 m before). The square's gullies and manholes are flat paint.
- **Measured** (`MeridianGroundChecks`, the five live deals): squares of 7,200-12,200 m2 a plan. At night, the square's floor
  sends back 0.032-0.062 linear luminance on average; shared's asphalt under the same light sent back 0.0055-0.0107. Its darkest
  cells sit in the moon's shadow by the walls to the north, lit only by the rim fill: 0.0027, against shared's 0.0005. Worst views'
  triangles move by under 1%, except The Yards' High view at +6% (235,809).
- **The Editor and the owner's build:** the owner's screenshot floor reads about eight times darker (linear luminance 0.0033
  against 0.028) than the Editor's capture of the same commit and place. Judge brightness on a player build (the DEV_LOG's M4b entry).

## The night and the lamps

- **The look** (`expedition-night`, R4 and the art bible 3.6): the moon 0x9DB4E8 x 0.32 at 38 degrees from 340, the only
  shadow; teal rim; the ambient about a tenth of the town's dusk; navy fog 10-150 m; a lit cloud deck; rain 0.35; lightning
  every 50 s.
- **Where lamps hang:** the plan's lamp points (about one every 12 m along each route, alternating sides) on the nearest wall
  within 9 m (a swan-neck, its head 3.1 m or more over the floor under it), else a span pendant between walls up to 28 m
  apart, else a flush lamp in a low wall within 6 m; the side streets one every 9-18 m along the street frontages.
- **The lamp rule** (the owner): the routes home are the protected circuit (warmer glass, none dead, 1 in 10 flickering);
  the side streets 1 in 5 dead and 1 in 10 flickering. Where the lamps hang is the layout's; which are dead or flicker is the
  run's.
- **The light** (2026-11-21, the owner: "the light doesn't seem to be on the material properly? (Like this seems to be flat, in
  the air light)"): every working lamp lights the walls and floors round it, per pixel, through the city's lamp grid.
  - Before, only the eight practical slots (three on Low; three fewer beside the camp, whose kit lights took the weakest) put
    real light on a surface. Every other lamp showed a flat sodium disc laid over the floor (floating over a sloped street, whose
    floor falls away under a flat quad) and, on High, a bright constant cone in the air. The walls round those brackets stayed dark.
  - **The grid** (`Core/NativeCityLamps.cs`, `Resources/NativeCityLamps.cginc`, `Runtime/NativeCityLampLight.cs`): each light's
    place, reach and colour; 8 m cells over the city, each listing the lights whose reach touches it (16 at most, the brightest
    first). ChipArena/StageLit lights each pixel from its cell's lights exactly as from a slot (the slots' fall-off
    (1 - (d/r)^2)^2, N.L, the same highlight, the anime steps). It is on only while the lights' own map is lit and no stage or v2
    intro owns the light; every other map draws as before.
  - **Each lamp's light** (`Core/NativeMeridianDressLight.cs`): a swan-neck's 0.15 m under its bowl and a pendant's under its bowl,
    thrown down by its reflector (`NativeCityLamps.Overhead`: a lobe of power 1.2 straight down, keeping 0.45 sideways): sodium x 3.0
    (3.6 since 2026-11-21: the grounds) down and 1.35 sideways (the slots' old light all round), so the setts under a lamp take about twice and the wall under its
    bracket about 1.4 times what a slot gave (on the dark setts, #5A5652 with its grime, and the asphalt, #2E3038, the slot's light
    all round read as almost no pool once the flat disc was gone: the first captures); a flush lamp's 0.3 m in front of its lens,
    all round, x 1.6 (at the lens it lit only the street, never its own wall); the reach the pool's radius x 1.35; a route home's
    sodium warmer. A flickering lamp steps under 3 Hz; with Reduced effects it only breathes, never under 0.6.
  - **The slots** still carry the nearest lamps (8; Low 3) to the pilots and zombies (`ActorLights`), the greybox and the air's
    glow, marked (w -1) so StageLit takes them from the grid and never lights a surface twice.
  - **No pools.** A wet glint toward the eye (additive, about six metres) under each lamp within 40 m, only where the floor under
    it is level; none on Low.
  - **Beams** on High only where the camera draws its depth (they fade into the floor and walls they meet), about a third as
    bright, narrower, a little fainter seen side on, gone 30 m from the eye, none from a flush lamp.
  - **DARKNESS** puts the street lamps out: their light, glints, beams and glass (the glass and windows at the mutator's glow).
  - The street life's lit pieces (floods 2.8 down their aim, fires 1.8, flares 1.6, signs 1.0, festoons 0.6), the bleed's lamps
    and the camp's lamps and floods (both thrown down, their old light sideways; the fires all round) light through the same grid,
    their pools gone too.
- **Toon Harbor heads and VLB** reach players only through the private zip; neither has a Meridian bridge yet (the kit bank
  is not in the Editor's Resources), so our own fixtures and beams draw.

## For Astra (moving the live run onto candidate-3)

- Call `NativeMeridianDressDraw.Prepare(new NativeMeridianDress.Input{Map, Layout, Run, Routes, Hatches, Slots})` from the
  prewarm hook (behind the loading card) and drop it on leaving; keep the walk's block ids and roles (the planner reads ids:
  `terrace-`, `lot` ids in the sheet, `-tread-`, `fire-escape-*-landing`, `street-rail-`, `-parapet`, `cordon-`, `arcade-`,
  `viaduct-`, `rake-`/`train-car-`, the lids and bridges by name) and the routes' polylines, widths and kinds.
- Add the live map's id to `NativeMeridianDress.LookTable` and read it where the live map is lit (W1: `NativeLighting.Look`
  checks the table before `map.look`; WardZeroChecks:265 and WardLightingChecks:256 pin that line). (The live map sets
  `expedition-night` itself since PR #432. The arena dressing's hide line for the live map is done: `HidesGreybox`, 2026-11-20.)
- Rules this dress needs from you: `lamp-` prop blocks (0.3 m) at the sheet's lamp points that have no wall (59-68 of about
  100 per layout), `shelter-`, `barrier-` and `clock-` blocks for the free-standing furniture, rail blocks on the fire escapes'
  open sides and the stairs' drops, parapet blocks on walkable roofs, and ramp wedges under the lots marked `pitched` (today
  every lot is a flat box, so every roof is drawn flat and honest).
- Fire escapes as thin flights (`meridian-staircasing-astra.md`): each flight's pilot collider a sloped slab about 0.35 m deep
  under its slope instead of a wedge down to a flat bottom, its treads steps on it, and rails on the open sides. Pilots then pass
  under the flights at the street, and the dress can draw them open; until then each wedge is caged.

## Checks

`DressSeamChecks` (`--dress-seams`, in the full suite): the five plans on the walk and on a live deal, every surface combined as
the drawer combines it at LOD0 and LOD1: no vertex of one surface lies inside another's boundary edge on an axis-aligned line
(at one tier within a cell, at any pair across cells), the floor covers each camp fence slab's footprint, the triangle counts the
chunks are cut by are the meshes' own, and the drawer switches a cell's chunks together. `--dress-seams-probe` prints the counts
with the seams and with each surface on its own grid (shared's: 25,000-43,000 T-junctions on floors per plan), and the seam
table's time; `--dress-seams-load` (with `CHIPARENA_MERIDIAN_BANK`) times the load as shared's drawer ran it and as this one does.

`MeridianStairsChecks` (`--meridian-stairs`, in the full suite): every street plan's live deal dressed as the live run dresses
it, and on each of its flights: no side's corner over the flight's line; every point just under the line where a side is seen
drawn; every wall beside a sloped street drawn just over the street; a fire escape's treads only iron, nothing solid on them taller
than the stringer, every seen point of its wedge under the stringer within half a step of a bar, and the floor under its cage;
every trapezoid planar and wound as its face; nothing of a flight in one plane with another surface turned the same way on one
layer (a band at a first tread dipped into the terrace face it continues). It fails on the dress before 2026-11-20 (the stepped
sides, the brick wedges, the bare floors). `--meridian-stairs-probe` counts the flights and what hangs on them;
`--meridian-stairs-zfight PLAN` lists coplanar pairs touching a flight; `--meridian-stairs-export PLAN X Z RADIUS OUT.ply [roles]`
writes the live deal's dress for a Blender check render (with `roles`, a colour per role).

The flights' cost (2026-11-20, with the bank; shared → `claude/meridian-stairs`): worst views High / Low, Crown Hill 371,689 →
377,544 / 161,838 → 165,726; The Navigation 467,614 → 440,293 / 157,358 → 176,724; The Arcades 508,799 → 514,794 / 208,494 → 230,415;
The Stride 435,726 → 457,794 / 192,629 → 197,294; The Yards 240,698 → 222,355 / 104,181 → 122,180; draws up to 324 (High) and 194
(Low); surfaces 1.6-1.8 times (the bars). The Editor's load (`plan` + `LOD1 combine`): The Navigation 668 + 102 → 686 + 153 ms, Crown
Hill 424 + 131 → 423 + 166 ms. Captured views (one hold, xseeds 1013904226 and 2027808452): the sanctuary looking north-east, the
sloped streets from top and bottom, fire escapes near and far, the stone stairs.

`HubZFightChecks` (`--hub-zfight`, in the full suite): each street plan's live deal built as the station builds it, the dress
planned as the live run plans it, and everything drawn in the hub (64 m square at the camp) gathered: the greybox boxes the arena
dressing still draws (with their lifts), the dress's surfaces (with `CHIPARENA_MERIDIAN_BANK`, its modules) and the gates. No two
same-facing faces may share a plane there (4 mm; floors 30 mm; two greybox faces 2 mm, their lifts' step) unless StageLit's
layers part them. `--hub-zfight-probe` lists the hub's blocks and every pair as shared drew it and as the branch draws it.

`MeridianLampChecks` (`--meridian-lamps`, in the full suite): every street plan's live deal, its lamps' light as the grid holds it
and the shader's formula: the wall 1 m and 2 m under every swan-neck's bracket at least 0.6 and 0.35 (linear luminance before the
surface's colour; measured 1.09 and 0.90 at the least), the wall under every flush lens at least 0.2 (shared's slot gave 0.12; now
0.37), every pool's floor its lamp reaches at least 0.25 (0.42 at the least); the grid's cells give every light that reaches them
(none dropped, the whole city's lights as the live run adds them: the fullest cell 15 of 16, the Arcades); every lamp within 60 m
ahead of the owner's view up Crown Hill's west street lights its wall (17 of 17), and the cause there in shared (its eight slots: four
lamps up on the terraces, unseen from the sunken street, then the camp's three kit lights took the weakest, the swan-neck 22 m ahead on
the left wall among them, whose beam still drew over its dark wall); flicker under 3 Hz and the Reduced breath; the shader, drawers and
upload pinned; the per-frame packing allocates nothing. `--meridian-lamps-probe PLAN X Z R`
lists the lamps near a point with the light on their walls and floors (with `MERIDIAN_EYE=x,y,z,fx,fy,fz`, the slots' picks there);
`--meridian-lamps-stats` the least light per kind and plan.

`MeridianGroundChecks` (`--meridian-ground`, in the full suite; `--meridian-ground-probe` prints the numbers without failing): every
street plan's live deal. The precinct table is the plans' own. Every open floor of a ground block, and every sloped street, wears a
ground finish (the zone's), never a plain one. Every retaining wall face is engineering brick weathering from a floor between its
bottom and its foot. The square at night (the look's moon shadowed by the blocks, its rim fill, every city light as the drawers add
them) is never under 0.002 linear luminance and averages at least 0.02; shared's asphalt under the same light fails both. The
square's drains are flat paint, the floods reach 20 m, and StageLit's grounds are pinned.

`MeridianDressChecks` (`--meridian-dress`, in the full suite): the roles against the specs, the sheet, determinism, the theme
hook, the hugging rule, modules on their faces' planes, every seen face drawn, the route corridors' keep-out, the lamp rule, the
look table; with `CHIPARENA_MERIDIAN_BANK` set, every hugging module's vertices and the six standard views' triangles and draws
(High 0.9 M and 900 draws, Low 0.33 M and 450 for the dress itself). `--meridian-export` and `--meridian-zfight` write a PLY for a
check render and `zfight.py`'s input.
