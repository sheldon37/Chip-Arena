# Meridian's described route (Crown Hill: camp, the Hundred Steps, left toward City Hall)

Owner, 2026-11-25: "lets get work started" on Astra's brief, section 3 ("Give Meridian a route players can describe"). Astra's
handoff: [MERIDIAN-CITY-WALK.md](MERIDIAN-CITY-WALK.md). Presentation only: no collider, rule, route, nav, spawn or wire change.
Without the route bank every drawer keeps its look.

## The walk, moment by moment

| Moment | What is drawn | Pack |
| --- | --- | --- |
| Leaving camp | The camp's apron road along the north fence (tar), a zebra crossing at the north gate with give-way studs and zigzags, its granite kerb dropped across the crossing behind a tactile band, Belisha beacons on the gate's fence ends (over 3.1 m), and a York-flag footway edged in the same granite to the first tread. All flat paint of 6 cm or less. | none (the kit) |
| The Hundred Steps | Cast-iron balustrades on Astra's stair and landing guards (StreetHandrails), both sides of the four flights and three landings: a round handrail 1.05 m over each flight's slope, level on the landings and bent round each join, a bottom rail, bars every 12 cm (the Fine tier), newels seated on the treads on Astra's posts, ball-topped newels at the foot and the head. The guards' boxes are hidden while it draws; their colliders stay. | none (the kit) |
| Framing the tower | Hipped slate roofs with chimney stacks on Clock Street's four rows (lots 71 and 72 as one roof, 73, 74): from the foot of the steps the clock tower stands between them. | none (see below) |
| The left turn | The same kerb and footway up both sides of Clock Street from the stair head; the west one turns left at the top and runs along the first switchback's frontage. Meridian Stores on the corner lot's ground storey, turned to the junction: pilasters, stall risers, lit windows with shelves and produce crates, a recessed sliding door, a lit fascia, a striped awning on the switchback face, a corner blade sign and lamp. The CITY HALL sign (an arrow left, lit letters, two gooseneck lamps) high on the terrace wall that closes Clock Street, read from the top of the steps. | Modern Supermarket: produce crates, SALE cards, the sliding doors |
| Recognising Ironworks | Wherever the deal puts the Ironworks: a loading bay in the door front's first two wing bays right of the door (the hall's buff brick and blue bands carried through, a half-raised shutter over the furnace's glow, hazard-painted jambs, a sheet canopy on steel brackets with a hanging lamp, a steel window bay lit from inside, LOADING BAY 3 painted on the brick); and smoke over the far layer's three stacks: columns of low-poly puffs (Polygon Arsenal's look, opaque, no sorting) rising, leaning down the wind, swelling and shrinking away, lit orange just out of the flames. | Old Industry: the personnel door, the window bay, the canopy sheet, its gutter, the hanging lamp |

## Pack choices (and what was not used)

- **EasyRoads3D Pro** (owned) builds road meshes on terrain in the Editor. Meridian is generated per run from block plans at
  runtime, its floors are the street dress's generated surfaces, and the pack's runtime is third-party code that cannot go in
  git. The crossing, kerbs and footways are drawn from the Meridian kit as flat paint on the generated floors instead.
- **City & Terrace Pack** (owned, POLYBOX) is a loft interior with a modern skyline backdrop: glass and concrete towers of 7 to 36
  triangles whose windows live in baked 4K textures. It has no terrace rooflines, and its towers would stand against the art
  bible's Edwardian civic city. The roofs framing the tower are modelled in the Meridian kit (slate, lead, stock-brick stacks).
- **Modern Supermarket** (owned) is an interior pack; its produce, sale cards and sliding doors furnish Meridian Stores.
- **The Horror Hospital** (owned since 2026-11-24) is not used at the corner: a hospital entrance does not fit Clock Street's
  grocer, and the pack fits Ward Zero's door front better (the brief's own suggestion). Not downloaded.
- **Old Industry** (owned) gives the loading bay its parts. Its brick walls are red and 5,000 triangles a 3 m panel; the
  Ironworks front's buff brick is the kit's.
- **Polygon Arsenal** (owned since 2026-11-25) is a particle-system pack; the game's effects do not run pack particle systems.
  Its `SmokestackDark` (opaque low-poly puffs: 6 s lives, 3.5 m/s, 4 m, 12 a second, a slight lift) is the audition's model,
  scaled to the far stacks and drawn by the route's drawer: 16 puffs a plume, 24 s lives, 72 m of rise, 9 m at their fullest.

Pack meshes are read as data by `tools/pack_bank` (unity_assets, fbx; the FBX reader now reads ASCII arrays written
`Name: *N { a: ... }` and ASCII names in binary order, which Old Industry's Houdini exports need) and their albedo baked into the
vertex colours, so every piece draws in ChipArena/StageLit like the rest of the city (the city's lamp grid, the night's fog, the
toon steps). No pack texture ships.

## Pieces

| Piece | Where | What it does |
| --- | --- | --- |
| The placement | `Core/NativeMeridianRoute.cs` | Pure and deterministic per layout. Crown Hill (647) only for the walk's pieces; the Ironworks' loading bay and smoke on every layout the deal puts it in. The keys, the blocks drawn in place of (the Hundred Steps' `stair-guard-*`), the shop's faces, the street life's keep-outs, the landmark pieces replaced (two Ironworks wing bays), the plumes and the puffs' motion (`PuffAt`). |
| The bank | `tools/stages/meridian_route.py` (Blender 5.x, on stagekit and the Meridian kits) | `ChipArenaCityRoute.bytes` (2.2 MB, never committed; it ships in the Drive zip with the city banks). Keys: `route/647/{rails,crossing,footway,corner,sign,roofs}` (world frame), `route/front/ironworks/loading` (a door front's bay frame, two bays wide), `route/smoke/puff`. Fine and Low tiers. Reads the dump of CoreTests `--meridian-route-dump` and, with `--packs DIR`, the owner's extracted packs. Checks the art rule on the built meshes (report). |
| The drawer | `Runtime/NativeMeridianRoute.cs` | `NativeMeridianRouteDraw`: the pieces through the landmarks' drawer (`NativeMeridianLandmarksDraw.Build` takes a bank name now), the smoke as one mesh rebuilt each frame in the far material (fogged no further than the far layer). Allocation free per frame. |
| The live hook | `Runtime/NativeExpeditionWorldPresentation.cs` (`ShowMeridian`) | Places the route with the landmarks, leaves the shop's ground storey to it (`NativeMeridianDress.FacadeReplaced`, Core NativeMeridianDressLots: a bay wholly in it draws nothing of the street dress), removes the two replaced landmark bays, draws the route; `NativeMeridianStreetLifeLive` adds its keep-outs. Only while the bank is there. |

The Developer walk (Settings > Developer > Walk Meridian greybox) does not draw the route yet.

## The storefront trial (issue #450, 2026-11-25)

Astra's asset handoff ([docs/CLAUDE-UNITY-ASSETS.md](../CLAUDE-UNITY-ASSETS.md)): give the commercial district a recognisable
corner shop from Checkout Frenzy – Convenience Store Simulator (Crying Snow 2.1.0), seen from the City Hall approach, against the
owned Modern Supermarket front. Meridian Stores (the left turn's corner lot) has three storefronts, `NativeMeridianRoute.Storefront`:

| Storefront | Key | What it is | Fine / Low triangles | Bank |
| --- | --- | --- | --- | --- |
| `""` (ms) | `route/647/corner` | Release 27's grocer: green Edwardian shopfront, Modern Supermarket produce and sale cards in lit windows a window's depth deep. | 6,262 / 1,020 | release 27's, 2,220,863 bytes |
| `cf` (in force) | `route/647/corner-cf` | Checkout Frenzy's convenience store: its window and entrance modules (glazing left out), a lit white lightbox fascia with a red stripe and the name in red, an OPEN card in the door, and the lit room behind. | 8,441 / 900 | 2,379,881 bytes (+159,018) |
| `mix` | `route/647/corner-mix` | The grocer's front opening onto the same lit room. | 12,855 / 1,154 | 2,951,645 bytes (+730,782) |

The room (both `cf` and `mix`) fills the lot's ground storey inside its collider box, where no pilot reaches: a lit floor, ceiling
strips, back walls with a red band, four of the pack's double drinks fridges lit inside and filled with its cans and cartons (a kind
a shelf), the kit's shelving on the south wall and a gondola carrying the pack's boxed goods, the pack's checkout counter, ice-cream
freezer, a plant and the air curtain over the door. Pack meshes are read as data and their palette and label textures baked into the
corners (a face's mean over its label for the goods); the glass is left out, so the room reads through open windows. No pack
material, texture, script or plugin ships.

- **Materials and draw calls:** the shop stays one renderer of the route's two shared materials (solid and glow): 2 draw calls plus
  its shadow casters, as release 27's. Dropped in as the pack's prefabs, the room's fittings would be about 195 renderers, 205 draw
  calls and 13 materials (the goods' label textures, the palette, the glass) before batching, and Checkout Frenzy's front 9 renderers
  and 18 more draw calls with its wall material.
- **The pavement stays clear:** every storefront has the same lot faces, keep-outs (1.6 m aprons) and covers (CoreTests pins it);
  the bank script's art rule finds no point standing free of a collider below 3.1 m on any of them. The fronts stand at most 0.14 m
  proud below 3.1 m; the fascia (3.12 m to 3.93 m) and the blade sign are over head height, as release 27's. No collision change.
- **The pick:** `cf`. Where the walk turns left toward City Hall, the game camera swings west up the switchback and the shop's north
  face passes on the left: `cf`'s lit white fascia and open, lit shop (fridges, goods, counter) read as a corner shop there and from
  across Clock Street. `mix` keeps the grocer's front, its room reading from across the street. Release 27's front reads as a lit sign
  over cream glass. From the steps' head (about 33 m) release 27's large warm window is the brighter patch and `cf` reads as a white
  sign band over a dimmer window, so a brighter room (its walls' glow) is the next step if the shop should carry that far. `cf` is
  also the cheaper of the two new ones.
- **Fallback:** a bank without the storefront in force keeps release 27's front (`NativeMeridianRouteDraw.StorefrontInBank`), so code
  that ships ahead of the Drive zip changes nothing. Build the bank with `--storefronts cf` (Core's pick is the script's default);
  `--storefronts ms,cf,mix` builds the trial bank the captures used.

## Collision (Astra's)

Nothing here adds or moves a collider. Art that stands where no collider is (the DEV_LOG entry of 2026-11-25 has the list with
transforms and dimensions):

- **Clock Street's roofs**: the four rows are `pitched` in the plans and flat boxes today; the hipped roofs stand up to 4.5 m over
  their tops. The roofs are not reached on foot (the nearest walkable surface is 5 m off); a pilot boosted onto one by a chip would
  stand inside the slates until the roof wedges exist (D1's standing ask).
- **The balustrades' bars** stand under the guards' grips between Astra's posts. A standing pilot is stopped by the grip; anything
  passing under 0.85 m (a crawler) would pass through the bars.
- **The Belisha beacons** stand on the north gate's fence ends, 3.2 m to 4.5 m up.
- **The loading bay's canopy** projects 2.3 m at 3.6 m to 5.2 m over the door front's apron.

## Checks

`MeridianRouteChecks` (`--meridian-route`, in the full suite): every street plan's live deal placed as ShowMeridian places it;
Crown Hill alone carries the walk's pieces; the balustrades replace exactly the Hundred Steps' guards (both sides, every flight and
landing; the other flights keep theirs); the shop covers exactly the corner lot's ground storey on its east and north faces; the
keep-outs; on every deal with the Ironworks (the two live ones and a walk deal of each plan) the loading bay takes the two wing bays
beside the door and three plumes rise beyond the map; the smoke stays over its stacks, leans down the wind, swells and shrinks;
determinism; the runtime hooks. With `CHIPARENA_ROUTE_BANK` every key is in the bank under its triangle cap.
`--meridian-route-dump OUT.json` writes the bank script's input.
