# Meridian's street life (the empty streets)

The owner, 2026-11-20: "Also, streets seem very empty? (Not talking about zombies, but like theres no life essentially like props
or anything, it still feels kind of 'grey box-ish'". Their screenshot showed the live sanctuary at night: lit facades round a bare,
dark plaza.

Two parts:

- **Part 1, street life (this branch, presentation only).** A zombie-outbreak city at night, abandoned but recently lived in,
  drawn over the street dress (D1, [MERIDIAN-DRESS.md](MERIDIAN-DRESS.md)), the landmarks (D2) and the bleed (D3) without a single
  collider: road markings, cracks, litter and blood on the floor; posters, graffiti and boarded windows on the walls; bin bags and
  bicycles against them; air conditioners, awnings, signs and floods above 3.1 m; washing lines, wires, bunting and festoons across
  the streets; flares, barrel fires and steam. In the live run and the Developer walk.
- **Part 2, the solid street props (Astra's to build).** Cars, wrecks, buses, benches, dumpsters, barricades, police cordons,
  newsstands, bus shelters, planters, bollards, hydrants and lamp posts need collision blocks first. This branch proposes them
  (stems, sizes, placement rules, density, seeding, validation) and draws them on the proposed blocks in a DEV preview in the
  Developer walk only. Nothing solid draws in a live run before its block exists.

## Part 1: street life

### Pieces

| Piece | Where | What it does |
| --- | --- | --- |
| The bank | `tools/stages/meridian_streets.py` (Blender 5.2, on stagekit and `meridian_bleed.py`'s finishes) | 81 kinds at LOD0 and LOD1 (`rig/streets/<key>/0`, `/2`), each checked against its envelope as it is written. `ChipArenaCityStreets.bytes`, 1.3 MB (LOD0 12,302 triangles for all 81, LOD1 4,790). Never committed: it reaches players in the LocalAssets zip like the city banks. Without it nothing draws. |
| The table | `Core/NativeMeridianStreetLife.cs` | The kinds (key, mount, envelope, casts, glow, light), the mounts' rules, a piece's light over time (`Level`). |
| The planner | `Core/NativeMeridianStreetLifePlan.cs` | Pure and deterministic per layout and run (no clock, no Unity). Reads the city, the D1 plan (its modules, lamps, paint, plates), the landmarks' places and the bleed's plan, and places every piece. |
| The drawer | `Runtime/NativeMeridianStreetLifeDraw.cs` | Plans on a worker (182-315 ms in .NET per layout), then draws instanced by kind with 32 m cell culling, reach per mount and tier, per-instance glow and tint, and the lights' pools, halos, flames and steam near the eye. No colliders. `--ca-no-city-streets` switches it off. |
| The live hook | `Runtime/NativeMeridianStreetLifeLive.cs` | Two lines in `NativeExpeditionWorldPresentation.ShowMeridian`: close the old, open the new after the landmarks are built. |
| The walk hook | `Runtime/NativeMeridianStreetLifeWalk.cs` | Two lines in `NativeLayeredCityWalkPreview` (open after the landmarks, close with them). Also the part 2 preview. |

### The mounts and the honesty rule

No invisible walls, nothing solid-looking a pilot can walk through:

- **Floor** (38 kinds): flat paint, 6 cm or less, lying wholly on a floor's top (its corners, edges' middles and centre). Never on
  another piece of paint or on the dress's own paint. Road markings, manholes, drains, grates, cracks, patches, oil, skids,
  scorches, litter, blood, chalk, the evacuation's Civil Defence stencils, tents' pitches, ash, flares, and the bleed's rumour
  litter.
- **Wall** (16): flat art on a collider's face, from 6.2 cm out (past the facade modules' 6 cm hugging band) to 10 cm, on a face
  that runs on behind the whole piece. Posters (stay indoors, missing, Civil Defence, bills), tags, messages, the searched-house X,
  boarded windows, notices, handprints, hazard paint, stencils.
- **Low** (8): small clutter standing against a wall, within 0.25 m of its face and under 1.6 m. Bin bags, a bicycle, cardboard,
  sandbags, a half-drum fire, a meter box, a payphone hood, a downpipe (as tall as its wall).
- **Upper** (14): wholly 3.1 m or more over the floor under it. Air conditioners, window boxes, awnings, blade signs, the
  survivors' banners, flags, the Civil Defence floods, traffic signals on walls.
- **Span** (4): across a street between two facing walls, every point 3.1 m or more over the floor. Wires, washing lines,
  bunting, festoons.
- **Hung** (1): hung from a span, 3.1 m or more up: the traffic signals stuck on amber.

Nothing comes inside the camp (its 25.8 m square, under 9 m), onto a cache or its approach, onto the landmarks' blocks or door
fronts, onto an instance door's apron, into the bleed's drift band and beyond (the bleed's own dressing takes over there; its
rumour band gets each instance's litter, leading on to it; the final court's theme is left plain), or onto a stair, a sloped street
or a fire escape; no wall dressing comes within 1.5 m of their footprints (M1 draws their iron and retaining walls there).

### Where it goes

- **The routes:** dashes every 6 m down the corridors, manholes, and along the walls drains, yellow lines, grates and bus stop
  marks; a zebra and SLOW at the turns, or a box junction.
- **The camp's plaza (the precinct, 62 m round the camp):** the Civil Defence's marks at each gate and painted lanes out of all
  of them (30-50 m), the tents' pitches (most of them still there), their fires' ash, and a ring of ten flares, seven in ten
  still burning: the way in, marked for whoever comes next. Up to ten walls within 80 m that face the camp carry Civil Defence
  floods.
- **The fittings, by district** (the D1 lot styles): tenements get washing lines, wires, air conditioners; shops and the market
  get awnings (some torn), blade signs, bunting and festoons; rows and villas window boxes and wires; warehouses and sheds hazard
  paint and stencils; civic fronts flags. Downpipes on many walls.
- **The story on the walls**, by the bay's category (blind, shutter, opening, door, ware, flat): boards over windows, the X on
  searched doors, posters and graffiti, notices; the survivors' banners (10 at most, 60 m apart).
- **The floor's scatter:** per 2 m cell, in drifts rather than salt and pepper: litter, leaves, glass, cans, clothes, cardboard,
  blood pools, trails and prints.
- **The trail:** flares at route turns within 170 m of the camp and at its gates, barrel fires between 40 and 230 m.

### The light

Lit kinds are signs, signals, fires, flares, floods and festoons. A piece's state is the run's: steady, flickering, dead, blinking,
burning, spent or steaming. Within the 3 Hz rule, and slower with Reduced effects:

| State | Normal | Reduced effects |
| --- | --- | --- |
| Flicker (signs) | steps, 2.7 a second at most | a slow steady swell (0.35 Hz) |
| Blink (signals) | amber at 1 Hz, eased | steady |
| Burning (fires, flares) | three swells under 2.4 Hz | under 0.7 Hz |

The flames, halos and pools draw within 70 m of the eye, the grates' steam within 45 m.

### What decides what

Where the city's fittings stand (markings, drains, awnings, signs, wires, floods) is the layout's: the same in every run of a plan.
The story (litter, blood, posters, graffiti, banners, flares, fires, tents) and every light's state are the run's, from its world
seed.

### Budgets

Worst views over the five plans (the checks' six standard views a plan, the bank's own meshes): High 39,360-91,051 triangles and
97 draws at most against a cap of 150,000 and 170; Low 14,611-37,217 and 62 draws at most against 45,000 and 110. Low draws
overhead pieces to 55% of their reach and the rest to 70%.

## Part 2: the solid street props (the proposal for Astra)

The props stand only on collision blocks, which are Astra's to build. `Core/NativeMeridianStreetProps.cs` holds the table and the
rules; `Core/NativeMeridianStreetPropsPlan.cs` (`NativeMeridianStreetLife.ProposeProps`) places one layout's candidates by them. The
contract for Astra is the notes folder's `meridian-street-props-astra.md`; this section is its summary.

### The stems

Block sizes in metres (across, height, along the prop's front), axis-aligned, turned only in quarter turns. A model fills its block
to within 0.25 m on every side below 3.1 m. Fill is the share of a layout's candidates a run fills.

| Stem | Size | Fill | What |
| --- | --- | --- | --- |
| `car` | 1.8 x 1.45 x 4.2 | 0.7 | an abandoned Vale parked at the kerb (six paints, smashed, roof rack) |
| `wreck` | 1.9 x 1.45 x 4.3 | 0.85 | a crashed or burnt-out Vale |
| `police` | 1.8 x 1.6 x 4.2 | 1 | the police's Vale at a cordon |
| `bus` | 2.55 x 3.05 x 11 | 0.85 | a route 647 single-decker at its stop |
| `shelter` | 3.6 x 2.6 x 1.6 | 0.9 | the cast-iron bus shelter, its back to the wall |
| `bench` | 1.8 x 0.85 x 0.66 | 0.85 | a municipal bench |
| `bin` | 0.6 x 1.0 x 0.6 | 0.85 | a cast-iron litter bin |
| `dumpster` | 2.0 x 1.4 x 1.25 | 0.85 | a commercial dumpster against a wall |
| `barricade` | 3.5 x 2.1 x 1.5 | 0.9 | the survivors' furniture barricade across half an alley mouth |
| `barrier` | 2.3 x 1.1 x 0.6 | 1 | a steel crowd barrier, end to end in a cordon |
| `newsstand` | 2.4 x 2.4 x 1.3 | 0.85 | a news kiosk against a shop wall |
| `kiosk` | 1.2 x 2.6 x 1.2 | 0.9 | the Type M telephone kiosk |
| `planter` | 1.5 x 0.9 x 1.0 | 0.85 | a stone planter |
| `bollard` | 0.3 x 0.95 x 0.3 | 0.9 | a cannon bollard |
| `hydrant` | 0.4 x 0.85 x 0.4 | 0.9 | a fire hydrant at the kerb |
| `lamp` | 0.3 x 6.5 x 0.3 | 1 | the Type 6 sodium post where no wall carries a lamp (D1's `lamp-` ask) |

Block ids: `prop-<stem>-<index>`. A prop's top is never a street (the horde lattice and the planners read the prefix).

### The rules

- **Lanes:** a clear lane down every route, half as wide as the larger of 3.5 m and the route's half width less the 2.6 m kerb
  band, so a horde and a Hulk always pass. Nothing within 6 m of a route's turn but bollards and hydrants on its pavement.
- **Keep-outs:** not inside the camp, nor within 14 m of a gate's middle; not on a cache's keep-out or a metre past it; not within
  18 m of an instance door or the final court's gateway; not on the landmarks' features; not within 4 m of a stair's, a sloped
  street's or a fire escape's footprint.
- **Footing:** the whole footprint on a street's top (a terrace; a bridge only for the small stems), nothing else in the box.
- **The gap rule:** on each side the nearest wall or prop is flush (5 cm or less) or a pilot's width away (1 m or more), so no slot
  traps a pilot.
- **The lattice:** with every candidate in, the horde's lattice keeps every node it reached without them, but those under a prop.
  A candidate that closes a pocket is dropped, the one nearest each cut node, five passes at most (a layout still cut after them
  would get no props; none is).
- **Per street type:** cars in the gutters (0.2 m off the kerb, 9 m apart at least) of streets 10-40 m between walls; bins and
  hydrants at the kerb (1.95 m out); bollards in pairs at the ends of shop and civic fronts on a route; benches and newsstands
  flush against shops, planters by civic fronts, a telephone kiosk by a row's end; dumpsters flush against tenements, warehouses
  and sheds; bus stops on wide shopping and civic streets off the
  routes (14 m or more), a 647 a pilot's width beyond the shelter; in alleys (5-9 m) dumpsters and now and then a barricade across
  half a mouth, a Hulk's 3.2 m always left; wrecks and dumpsters in the yards and quays; the camp's plaza with lamp posts on two
  rings (31 and 50 m), planters and benches by them, the Civil Defence's cordons funnelling two gates' approaches (barriers 7.5 m
  off the lane's axis from 33 m out, a police car beside one), cars queued in the outer ring.
- **Density and seeding:** the candidates are the layout's alone (268-677 a layout); a run fills each stem's share by its world
  seed (about 80% overall). Any subset of the candidates keeps every rule, so a layout is validated once with all of them in.

### The DEV preview

Settings > Developer > Walk Meridian greybox (Editor and development builds only): every candidate's model on its proposed block,
from `ChipArenaCityStreetProps.bytes` (29 models, 2.8 MB, built by `tools/stages/meridian_street_props.py` on the street kit in
`tools/stages/meridian_street_kit/`; never committed). A magenta banner says it is not gameplay. Keys: **P** the props, **O** the
proposed blocks (the run's filled ones bright, the empty ones faint) and the routes' clear lanes, **L** the street life (part 1).
The pilot walks through the props: nothing here is a collider.

## Checks

`StreetLifeChecks` (`--streets`, in the full suite): the table's rules; the light's flash rates sampled at 60 Hz for 20 s (3 Hz
at most, 1 Hz with Reduced effects); per plan, every piece's honesty (the keep-outs, the floor's support, paint on paint and on
the dress's paint, a wall piece's face plane, depth and continuity, the overhead clearance, no block inside any piece); determinism;
the layout's fittings fixed across runs; the plan without the other drawers; the live deals' plans as the live run builds them; the
hooks' source lines. With `CHIPARENA_STREETS_BANK` (a path to `ChipArenaCityStreets.bytes`) the six standard views' triangles and
draws a plan, High and Low. `--streets-probe` prints the counts; `STREETS_SPOTS=1` the busiest cells by district;
`STREETS_DUMP=<dir>` each live deal's blocks, routes, pieces and props as CSV.

`StreetPropsChecks` (`--street-props`, in the full suite): the table; per plan, determinism, candidates the layout's alone, every
candidate against the rules on its own geometry (the lanes sampled along each leg, the gap rule), the lattice with all candidates
in, the live deals' exits keeping an open apron within 8 m. With `CHIPARENA_STREETPROPS_BANK` (a path to the props' bank) every
model inside its block's envelope (within 0.25 m, nothing more than 0.25 m out below 3.1 m).

## Known limits

- Traffic signals are rare (0-2 a plan) and none hang from spans yet: few turns have a long enough straight leg into them.
- D3's bleed flicker steps at 11 Hz with no Reduced-effects path (not this branch's file; reported).
- The live exits' picks may move once Astra adds the blocks (0-3 a deal in the check); re-pin them then.
