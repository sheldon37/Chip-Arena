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
| The drawer | `Runtime/NativeMeridianDressDraw.cs` | Combines into 32 m chunks (`NativeMeridianDress.DrawChunks`: a cell over the bible's caps, 60,000 triangles at LOD0 or 7,000 at LOD1, is cut in quarters by each piece's place until every part is under them): every chunk's LOD1 at load, LOD0 on a worker within 38 m (High) or 24 m (Balanced), Low draws LOD1 only; chunks past the fog's end plus 25 m are left out. The night's light. |
| The walk hook | `Runtime/NativeMeridianDressWalk.cs` | One call in `OpenCityWalk` and one in `CloseCityWalk`. |
| The greybox | `Runtime/NativeArenaDressing.cs` | Leaves out each walk block the dress drew this frame (`NativeMeridianDressDraw.Hides`). |
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
- Coplanar meetings never fight: a face of the dress in the plane of a face the greybox or the landmarks draw is left to them;
  roof copings butt at the corners; the bank trims each shopfront to its 2.5 m bay, so the bays of a storey butt; the rest part
  by a few ChipArena/StageLit layers (kept few: the shader's offset grows with each): the ground storey 0 (a shopfront's fascia
  stands over its top), the upper storeys 1, the cornices 2 and on x-facing faces 3 (two meeting in a re-entrant corner), a
  fill's tiles 0 and 1 in turn, what hangs on a wall 4; paint keeps its own 3. A cornice is left out where a block stands
  against the face at the roof line (a landing, a neighbour's roof).

## The roles

| Role (ids) | Dressed as |
| --- | --- |
| `lot` (the sheet's lots) | Facades bay by bay (2.5 m, stretched to the face) and storey by storey (4 m ground, 3 m upper) by style: rows and villas (doors and parlour windows, sashes, oriels), shops and the market (their own trades' goods, shutters), tenements (barred flats, the lit entry and stair column, polychrome bays), warehouses and sheds (loading doors, iron windows), civic (rusticated arches). Blind faces get blind windows and a ghost sign. Partly hidden storeys get plain wall. A cornice under every roof line that hangs over a street; flat tar roofs with a coping and their hatches; pavements and kerbs (flat paint) along every street frontage. |
| `terrace-*` | Asphalt tops; engineering-brick retaining walls with a stone coping |
| Stair treads, fire escapes | Granite treads and risers on the built treads; iron grating on the fire escapes' treads, risers and landings. The solid steps' sides draw in the host lot's wall finish under a 0.3 m black iron stringer (the solid wall read as a black slab in iron) |
| Sloped streets (MapRamp `ramp`) | A setts surface on the wedge the pilots walk; the treads' sides as the stepped retaining edge |
| Bridges, skybridges, the causeway | Plank or setts decks, green iron or stone girders and soffits; parapets as iron panels or a stone balustrade filling their boxes; street rails as iron railings |
| Arcade roofs, lids | Lead tops, dark glazing from below (the Galleria, the arcades), soffits |
| Arcade posts | The market's cast-iron columns |
| The viaduct | Ballast deck, engineering brick, the Stride's arches hung between its piers |
| Wagons | Goods vans |
| Sheds, the goods shed's walls | Buff brick with copings |
| The Cordon and its towers | Concrete with a dark band |

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
- **The light:** the nearest lit lamps take the eight practical slots (three on Low) and light the art, the greybox and the
  pilots and zombies (`ActorLights`); every lit lamp lays a pool on its floor; near the eye each lays a wet streak toward it
  (camera-facing quads, about sixty, inside the 2,800 budget; none on Low); flickering heads draw apart with their glow; High
  adds soft beams under the nearest bowls (`NativeVolumetricBeams`).
- **Toon Harbor heads and VLB** reach players only through the private zip; neither has a Meridian bridge yet (the kit bank
  is not in the Editor's Resources), so our own fixtures and beams draw.

## For Astra (moving the live run onto candidate-3)

- Call `NativeMeridianDressDraw.Prepare(new NativeMeridianDress.Input{Map, Layout, Run, Routes, Hatches, Slots})` from the
  prewarm hook (behind the loading card) and drop it on leaving; keep the walk's block ids and roles (the planner reads ids:
  `terrace-`, `lot` ids in the sheet, `-tread-`, `fire-escape-*-landing`, `street-rail-`, `-parapet`, `cordon-`, `arcade-`,
  `viaduct-`, `rake-`/`train-car-`, the lids and bridges by name) and the routes' polylines, widths and kinds.
- Add the live map's id to `NativeMeridianDress.LookTable` and read it where the live map is lit (W1: `NativeLighting.Look`
  checks the table before `map.look`; WardZeroChecks:265 and WardLightingChecks:256 pin that line), and the arena dressing's
  hide line beside its `surveyBlocks` test for the live map.
- Rules this dress needs from you: `lamp-` prop blocks (0.3 m) at the sheet's lamp points that have no wall (59-68 of about
  100 per layout), `shelter-`, `barrier-` and `clock-` blocks for the free-standing furniture, rail blocks on the fire escapes'
  open sides and the stairs' drops, parapet blocks on walkable roofs, and ramp wedges under the lots marked `pitched` (today
  every lot is a flat box, so every roof is drawn flat and honest).

## Checks

`MeridianDressChecks` (`--meridian-dress`, in the full suite): the roles against the specs, the sheet, determinism, the theme
hook, the hugging rule, modules on their faces' planes, every seen face drawn, the route corridors' keep-out, the lamp rule, the
look table; with `CHIPARENA_MERIDIAN_BANK` set, every hugging module's vertices and the six standard views' triangles and draws
(High 0.9 M and 900 draws, Low 0.33 M and 450 for the dress itself). `--meridian-export` and `--meridian-zfight` write a PLY for a
check render and `zfight.py`'s input.
