# Cyber City: the stage art pass

2026-11-11 · Claude (stage art pass on the older stages) · branch `claude/artpass-cybercity` from `claude/sleepy-ritchie-g8ruhv`
(`db641c9c`). Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The bank
is local (git-ignored); the integrator packs it into the builder's Drive zip.

Owner (2026-11-11): "I want to revamp ALL the current stages out of the blocky style. Making them a LOT more detailed and styled
... Ensure theres no Z-Fighting, remove invisible barriers etc."

Cyber City keeps its layout, its hazards and every rule: the map record (blocks, colliders, nav, hazards, spawns) is untouched and
the regenerated catalogue is byte for byte the same. What changed is what draws. The recipe is docs/arenas/STAGE-ART.md; this is
what Cyber City adds to it.

## What is modelled

Scripts (tools/stages/): `cybercity_art.py` (the run), `cybercity_kit.py` (finishes; face frames; walls with openings; neon tube
lettering on a stroke font; LED strips, screens with abstract posters, hazard stripes, balustrades, louvres, lofted skins),
`cybercity_street.py` (the street and the Cut), `cybercity_city.py` (the buildings, the furniture, the near ring), and
`cybercity_props.py` (the breakable and carried blocks, the train).

- **The street**: flagstone promenade and plaza, the asphalt avenue between kerbs, lane marks, crossings, the fell zones'
  hatched borders, round manholes, puddles; wet patches baked into the ground's smoothness (`cybercity_city.wet`), so the neon
  lies in them through the stage's reflection probe.
- **The Cut**: board-marked walls on plinths between pilasters, cable trays on brackets with their cables, junction boxes with
  conduits, steel lintels over the alcoves, striped lips, drains under gratings, the guide beam with its rails and coil
  brackets; the stair pits; the tunnel portals' headwalls with the HELIX LINE signs in neon tube on their boards.
- **The frontage**: rows of buildings, one or two bays each in their own cladding: shopfronts in recesses (a lit window on a
  tiled riser, goods in silhouette, a glazed door), roller shutters under their boxes, fascia signs, cloth awnings, three
  storeys of window bands (mullions, sills, lit panes, half-drawn blinds), pilasters, ledges, a cornice, condensers on brackets
  with fan grilles and pipes, blade signs, big screens on struts, and roof plant (water tanks, masts with beacons, cabinets,
  sign scaffolds).
- **The podiums** (Nova, Vega): piers with recessed panels and fins, sills with rail caps, lintels, paved storeys with troffers
  in their soffits, stairs with lit nosings, parapets under copings, louvred plant cabinets, lobby desks; the spires as curtain
  walls (mullions, spandrels, lit panes) with corner light fins, a stepped crown, a mast and its beacon, and their names in neon.
- **The Helix station**: chamfered columns with the corner lights let into the chamfers under flared heads, the concourse deck
  on cross girders carrying the under-lights, the glazed lift shaft with its doors and the head's HELIX signs, glass
  balustrades with lit handrails, slatted benches, stair-bridges, skybridges.
- **The markets**: corrugated hoardings with posters, posts carrying one catenary cable of paper lanterns and lattice canopy
  girders, the noodle bar (a lit serving hatch, cloth flags, a red light box, NOODLE in neon over the roof), its counter, the
  capsule containers (corrugated, portholes, doors with locking bars), the arcade booth.
- **The avenue, yards and promenade**: parked hover vans and cabs (rounded bodies, glazing, pods, light bars), kiosks, ad
  pillars, street lamps with curved arms, the yards' canopies (TAXI, DOCK in neon), chargers, concrete barriers, planters with
  clipped shrubs, waiting pods, the couplings (steel posts with ceramic insulators, bus beams with the HV mark).
- **Block models** (34): the market stalls (plank fronts, crates of fruit, paper lanterns, menu screens), vending machines (cans
  on lit shelves), wheeled bins, hover bikes, and the billboards: lattice struts with a striped collar and a red plate where an
  explosive should go, the truss, the screen's housing (its picture is still the atmosphere's) and its bezel. A billboard
  falls as before: its models ride the falling blocks.
- **The train** (the one rig): three cars lofted from superellipse sections, tapered at both ends, with a windscreen, lit
  windows, doors, light lines, the head and tail lights and the levitation glow; posed by Core `NativeCyberCityArt.Pose` from
  the maglev's head exactly where `NativeCyberCityKit.TrainPlace` puts the kit's boxes.
- **The backdrop's near ring and the Helix Line's tunnels** (Core `NativeCyberCityArt.Near`): the blocks right behind the
  frontage as buildings with lit window bands, parapets, neon edges, tanks, masts and stair heads; the tunnels as ribbed tubes
  with their lights. Chunks `static/near-<side>`.

## What stays the kit's boxes, and why

- **Every driven kit part** (Core `NativeCyberCityArt.Kept`, 944 parts): the Cut's edge strips, the maglev's coils, the SAFE
  and EXIT lights, the climb lights, the couplings' rails, lamps and deck plates, the flickering signs (GAME, CAPSULE, 24H).
  They change colour or level every frame from the hazard states, so every telegraph is exactly the box kit's. The art models
  what they sit in or on (the lintels' boards, the stairs' chamfers, the marquee boards) and leaves their places clear.
- The fallen billboard's rubble (the dressing's boxes, as every map's rubble and fragments), a destroyed block's stub.
- The far backdrop (the street plates, the expressways, the towers) and the box backdrop behind the main menu.
- The atmosphere and the hazard effects (Core NativeCyberCityFx): unchanged.

Under the art the steady kit parts are not drawn: on a static block always, on a breakable or carried block while its model
draws (`NativeStageArt.Keep`); with its stub they come back. The art does not fade between the camera and the pilot as the
kit's parts above 2.5 m did (the chunks are whole meshes).

## No z-fighting

Every tier's static scene, the near ring and each prop go through `stagekit.lift_coplanar`. The scripts' own small offsets count
as one plane too (`cybercity_art.EPS`: 5.5 mm, 6.5 mm, 12.5 mm by tier), so layered faces always stand the tier's full lift
apart. A ground mark lies in one layer: the fell zones' side strips stop short of the end ones, a painted mark that would cross
a border is left out, a puddle keeps off the marks. Measures: see "Numbers".

## No invisible walls

`tools/generate_stage_hazards.py` now routes Cyber City through `open_stage` (`EDGES['cybercity']`, `OVERHANG['cybercity']`). Its
eight pilot columns all stay, none for bots only (no column opens onto a lethal drop):

| Column | Why it stays |
| --- | --- |
| `cybercity-frontage-top-n/s/e/w` | The map's edge: the frontage's tops at 16 m (the near ring stands behind them). |
| `cybercity-tunnel-e/w` | The map's edge at the tunnel mouths: beyond them the tunnel is backdrop with no floor. The art now shows the wall: a light gate across each mouth (emitter posts and six thin beams). |
| `cybercity-spire-top-nova/vega` | The spires' tops at the 24 m ceiling: the crown, the mast and the beacon stand on them. |

`CyberCityArtChecks` pins the eight and the rule.

## Runtime

- Core `NativeCyberCityArt.cs`: the bank's name, the slots, `Pose`, `Kept`, `Modelled`, `Near`.
- Runtime `NativeCyberCityArtPresentation.cs` (a partial of ArenaPresentation): `MetroArtPrepare` (with the kit, behind the
  loading screen through PrewarmStage), `MetroArtRender`, `MetroArtHas`. `NativeCyberCityPresentation.cs` calls them: the art
  before the kit; a part the art has is skipped; the box train and the near ring's batches draw only without the art.
- The Low graphics tier draws the bank's Low tier (as the other art stages).
- Without the bank (CI, a build without the Drive zip) nothing changes: every box draws as before.

## Rebuild

```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout cybercity LAYOUT.json
blender --background --factory-startup --python tools/stages/cybercity_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets
```
About 15 s. `--preview DIR [--views a,b]`, `--tiers`, `--no-ao`, `--static-only`, `--stats`, `--own-mid`.

## Checks

`--cybercity-art` (Tests/CyberCityArtChecks.cs, also in the full run): the cars against the kit's box train at every 0.05 s of
two rounds; which parts stay boxes; the near ring; the columns; the layout export; the wiring and the fallback; and, with the
bank on the machine, its covers, models, tiers, the cars' envelopes, the play-space rule and the spawns.

## Numbers (2026-11-11)

The bank: `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageCyberCity.bytes`, 15,943,946 bytes (15.2 MB), 1,041 props.
- Static: 78 chunks (72 of the stage, 6 of the near ring and tunnels). Fine 186,662 triangles (204,592 vertices); Mid the Fine
  meshes again (none of its own: a Mid of its own was 143,792 triangles and took the bank to 21.7 MB); Low 47,256 (74,441).
- Block models: 34; 18,792 / 10,808 / 3,320 triangles by tier.
- The train: 3 cars; 2,276 / 1,936 / 1,116 triangles.

Z-fighting (`zfight.py`, eps 4 mm): 0.02 m2 of same-facing coplanar overlap at each tier (Fine, Mid, Low); no map block is left
uncovered beside the art. Before the scene was settled the Fine tier held 281 m2.

Play space: 79,484 Fine vertices under 2 m over a floor; 24 farther than .25 m from a block, the farthest .32 m; nothing in a
spawn.

Frame times (shared Editor, High tier, the plaza and station from the south-west promenade, 10 s, runs back to back):
the box kit mean 11.53 and 10.71 ms, p95 13.08 and 11.85 ms; the art mean 9.76, 9.44 and 9.72 ms, p95 11.32, 10.62 and 11.48 ms.
(Earlier the same night, with other sessions' suites running: boxes 11.04 / 10.76 ms, art 12.61 / 12.42 ms with 200 ms stalls;
those runs were not back to back.)

Seen in the Editor: twelve views at High and at the Low graphics tier, before and after; the maglev's warning and pass (the
edge strips amber then red, the EXIT signs, the modelled train in the Cut) and the west coupling charging and live on the art.

## Not done / not seen

- A billboard's fall, a destroyed stall's stub and the killcam on the art were not captured (the block models ride the blocks'
  transforms by the shared drawer; the kit's steady parts return with a stub by `MetroArtHas`).
- The fallen billboard's rubble, the far backdrop and the menu's backdrop are still boxes (above).
- The art does not fade between the camera and the pilot.
- The frontage's shop windows are flat lit panes with silhouettes; the podium's shell is finished pieces rather than sculpted.
- Native Windows build, multiplayer guests: not run. No bank-absent run in the Editor on this branch's code (the before
  captures are the base commit; the fallback is checked by source in CyberCityArtChecks and by the full suite without the bank).
