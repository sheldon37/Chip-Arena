# Top Dog Emporium (`emporium`)

2026-11-10 · Claude (presentation lane, with one small rules addition logged for Astra) · branch `claude/stage-emporium`
from shared `claude/sleepy-ritchie-g8ruhv` (`e6d564dc`).

Owner request (2026-11-10): "Begin development of 4 newer levels ... set a new bar for quality ... Give them all unique hazards
... VERY beautiful, with exceptional quality effects ... in the random selection." This stage's brief: "a hotdog emporium level
... make hotdog one funny, gimmicky hazards". The name nods to the game's own hot dog DEV class, the Top Dog.

Status: **implemented on the branch, not merged, not released.** Seen in the Unity Editor (see "Seen" below).

## The pitch

A grand glass-roofed food hall turned hot dog palace, at golden hour. Iron trusses carry an arched glass vault; the low sun
streams in from the south-west in long shafts and striped shadows. Under it: a diner of red vinyl booths on a black and white
checkerboard, mint counters with roller grills turning their sausages, a soda fountain, a jukebox with bubble tubes, neon
lettering on every wall ("TOP DOG EMPORIUM", "KETCHUP", "MUSTARD", "OPEN", "FRIES", "MENU"), and, turning over the middle of the
hall, a giant hot dog sign ringed with chasing marquee bulbs and neon mustard, above the Big Dog: a waving, blinking hot dog mascot
on its plinth. Two team kitchens face each other across the hall: the Ketchup Kitchen (west, team A) and the Mustard Kitchen
(east, team B). Bright, saturated, appetizing, cartoon-readable; and everything that kills you is food.

## Layout (58 x 46 m, point symmetric)

`docs/arenas/scripts/emporium_m1.py` is the draft and its checks (run it: plans, LOS, raster reachability, timings). Bounds
x ±29, z ±23, ceiling 16, killY -6; 250 blocks (+ floor), 12 breakable, 29 pilot columns (all kept by `open_tops`: walls, ovens,
kitchen walls, the plinth, towers and press heads are unstandable). Levels: the floor (0), the two bun bridges (2.4) and the two
sauce balconies (3.6).

- **The kitchen line** (mid band, z -8..8): the Big Dog plinth at the centre (full cover), a bun press either side (x ±6.5),
  a griddle beyond each (x ±13.6), four condiment towers (giant ketchup and mustard bottles, 6 m, full cover) by the lanes, prep
  tables and bun crates.
- **The Wiener Run**: two roller-grill lanes (z ±10, 4 m wide) the full length of the kitchen line between four wiener ovens
  (x ±18..±22.6). Crossed on foot, or safely over a **bun bridge** (a frankfurter deck in its bun, 2.4 m, a 12-riser flight
  each side; the wiener rolls under it).
- **The diners** (z ±12..±23): booths (two high-backed benches and a table each) under a **sauce balcony** (3.6 m, x ±14.4,
  reached by a stair along the wall at each end, or by a geyser), a counter, the soda fountain, the jukebox, the condiment pumps,
  freezers and pickle barrels in the corners, a menu kiosk on each balcony.
- **The team kitchens** (x ±22.6..±29): each spawn pocket is sealed for sight by baffles: a kitchen wall flush with the ovens
  with one 2.9 m door into an airlock where the fry station stands in front of it (2.6 m passages either side), and an aisle exit
  north and south (2.9 m) turned by a pantry rack. Three exits per team; no FFA or A-B pair in line of sight.
- **Spawns**: team A in the Ketchup Kitchen, B in the Mustard Kitchen; FFA: the two kitchens, one on each balcony behind its
  kiosk, a corner each side (behind a fridge), and one in each under-balcony nook. Checks: 28 FFA pairs and 16 A-B pairs blocked
  under ±1 m jitter at 1.2/1.6/1.9 m eyes in 3D; min FFA spacing 13.9 m; every spawn ≥ 4.7 m from a hazard.
- Navigation: 2306 nodes, 3784 links, 954 gated by the hazards (bots wait for the lanes, the presses, the griddles and the
  geysers through the existing LinkOpen rules).

## Hazards (existing kinds only)

| Hazard | Kind | Timing | Effect |
|---|---|---|---|
| **The Wiener Run** (`emporium-lane-n`, `-s`) | `maglev` | N: first pass 14 s (warning from 8 s), S: 26 s; each every 24 s, so a wiener rolls every 12 s, alternating lanes; warn 6 s, danger 3 s, pass 3.4 s at 11 m/s (direction alternates per pass) | lethal; flings the body along the lane (the maglev smash) |
| **The Bun Presses** (`emporium-press-w`, `-e`) | `press` | every 12 s, the two 6 s apart; warn 1.8 s, slam 0.32 s, held 1.0 s, lift 1.6 s; first warning 13.3 s / 19.3 s into the fight | lethal under the 3 x 3 m ram (the sesame bun hangs in view at 4.1 m between four posts; a death pancakes like the Factory's) |
| **The Griddles** (`emporium-griddle-w-a`, `-w-b`, `-e-a`, `-e-b`) | `burner` | two checkerboard pairs per griddle that go hot in turns: every 8 s each, a and b 4 s apart; warn 1.5 s, live 2.0 s, cool 1.0 s | 8 per 0.25 s tick (64 for a whole live window), never lethal |
| **The Geysers** (`emporium-geyser-n-w`, `-n-e`, `-s-e`, `-s-w`) | `vent` with `push` | every 10 s, the two of a diner 5 s apart; three pumps over 1.4 s, burst 1.1 s | launches whoever stands on it at 15 m/s (apex 4.9 m: onto the balcony through the gap in its rail), 3 per 0.5 s tick |

Nothing moves for the fight's first 4 s (the arm delay; every warning starts later). Telegraphs are never reduced by graphics
settings:
- **Wiener**: the lane-edge lamps run an amber chase the way it will come through the warning (oven door steaming and
  glowing at the portal it will burst from, an "ah-ooga" horn), a fast red chase through the danger (sparks from the door, a
  rising slide whistle, the flap rattling), then red ahead of it while it runs. It bursts through a pet-door flap, rolls without
  slipping (its grill stripes turn under a squiggle of mustard), bobs over the rollers, googly eyes looking where it goes, and
  boings into the far oven.
- **Bun press**: a double bell ding, the bun winds up with a tremble and a stretch, the zone strobes red, a cartoon drop shadow
  darkens and spreads under it; it slams (SPLORT) and squashes flat, springing back, flour and condiment splats flying out.
- **Griddle**: igniter clicks, the plates heat through orange (the burner look by light level) with shimmer and corner lamps,
  then white-hot with sizzle sparks, grease pops, smoke and hopping popcorn sausages.
- **Geyser**: the pump-dispenser beside it pumps three squelching strokes, the cap bulges and bubbles, then a fat glossy column of
  mustard (east) or ketchup (west) shoots up 5 m with a SPLOOSH and a boing, spraying droplets and leaving a splat on the floor.

## Rules addition (for Astra)

`vent` gains a push launch, used only by vents with `push > 0` (every vent shipped before has push 0, so nothing else changes;
EmporiumChecks pins that):
- `Core/NativeStageHazards.cs` `VentLaunch(h, state, body, verticalSpeed)`: true while the vent bursts, the capsule touches its
  column and the pilot is not already rising (vertical speed under 1 m/s).
- `Runtime/NativeStageHazardsRuntime.cs` `StepAuthoredHazards`, vent branch (host, Fight only): such a pilot gets
  `Knockback.y = max(Knockback.y, push)`, the existing one-shot lift path (as the trampoline chip and the DEV launch use).
- Not changed: damage ticks, kill credit, nav gating (a vent with damage closes its links while not parked), death kinds,
  replication (the knockback is the host's pilot state as today). The DEV hazard pulse already launches by `push`.

## Look

`MapLooks.json` `emporium` / `emporium-golden-hour`: a warm key at 32° from the south-west (soft shadows), a pink rim, warm
trilight, a dusk anime sky (orange horizon, violet zenith, low sun disc, pink clouds), linear warm fog, eight practicals (the
sign, two ovens, both kitchens, both balconies, the kitchen line), the anime style with a saturated grade (1.28) and pink rim,
advanced lighting on (GI, scatter, volumetric beams), no weather. Block looks dress every id (tile, stainless, vinyl, mint,
ketchup and mustard, bun and frankfurter).

Code (presentation only; nothing here is sim data, collides, damages or is sent):
- `Core/NativeEmporiumLooks.cs`: the map, the 36 kit kinds (`emporium-kit-*`) and their defaults, palette, the sign, the roof,
  the sun's ray.
- `Core/NativeEmporiumKit.cs`: the static kit (about 7,200 parts): floor paint (checkerboard, quarry tile, roller beds, hazard
  stripes, zones, splats and arrows), walls (tile, wainscot, frieze, posters, windows, neon lettering from a stroke font), ovens,
  kitchens, shelving and goods, booths, counters with stools, soda fountain, jukebox, pumps, freezers, barrels, crates, bun
  bridges, balconies, towers; overhead the glazed roof's trusses and purlins (shadow casters), pendants, conveyor rails.
- `Core/NativeEmporiumRig.cs`: the moving parts from the replicated state (wieners, flaps, lane lamps, buns, griddle plates,
  popcorn sausages, pumps, sauce columns) and the ambient animation (the turning sign, two overhead bun conveyors, roller grills,
  the waving mascot, the jukebox). Allocation free.
- `Core/NativeEmporiumFx.cs`: the effect quads (telegraphs never reduced; atmosphere by quality and a third on Low) and the
  sun's shafts through the roof (volumetric beams above Low, billboards on Low).
- `Core/NativeEmporiumScenery.cs`: the backdrop baked to three SourceLit sectors: the clerestory over the walls, the glowing
  sunburst gables, the street outside (blocks with lit floors, a rooftop hot dog billboard, water towers, a deco tower), the
  skyline. Its keep-out refuses any box in the play space below the roof line.
- `Core/NativeEmporiumSound.cs`, `Runtime/NativeEmporiumAudio.cs`: the cue plan on the match clock and synthesized comic
  samples (horn, slide whistle, rumble, boing, bell, splort, pop, clicks, sizzle, pumps, sploosh) on NativeAudio's voices.
- `Core/NativeSmashGib.cs` (one line): a bun press death on this map pancakes as the Factory's presses do.
- `Runtime/NativeEmporiumPresentation.cs`: the draw (NativeBlockLit instancing, SourceLit sectors, one FX batch, HDR gain for
  neon). Hooks: `EmporiumScenery(arena)` in SourceWorld, `PrewarmEmporium(arena)` in PrewarmStage, `EmporiumCues(arena,menu)`
  in NativeAudio.

## Music

The owner's song **Hot Dog Palace** (Suno, 2026-11-10; 182.8 s) plays under the key `emporium-theme`
(`NativeStageMusic.Theme`). The record's `music` keeps the placeholder `cybercity-neon-riot` as the fallback.

## Checks

- `python3 docs/arenas/scripts/emporium_m1.py`: plans and layout checks (PASS).
- `dotnet run --project Tests/CoreTests.csproj -c Release -- --emporium` (`Tests/EmporiumChecks.cs`, also in the full run): the record,
  nav to every spawn, arm delay and telegraph minimums, the vent push rule (and every shipped vent unchanged), the look, the
  kit's collider rule and the roof's clearance, the backdrop keep-out, the rigs following the replicated state, determinism, no
  allocation per frame (rig, effects), telegraphs never reduced, the sound plan, the press pancake, wiring and metas.
- Editor gate: `NativeBuildChecks` calls `ValidateEmporiumRuntime()` (`Runtime/NativeEmporiumValidation.cs`) on the real arena:
  the wiener kills in its lane (maglev death record), the bun press kills under its ram, a live griddle plate burns while its
  partner pair is cold, a geyser launches a standing pilot once per burst, not while pumping, not outside the fight.
- Pins updated: NativeUnityMaps (ids, Random set), BrickfallChecks (17 maps, rotation, 12 selectable), TransitionLoadingChecks
  (17 maps, the SourceWorld prewarm entry), SurfaceChecks (fifteen maps), MapStyleChecks (styled list, fourteen toon looks),
  AdvancedLightingChecks (the advanced set).

## Stage art (2026-11-10)

Owner (2026-11-10): "Are the new maps voxel? They look voxel lol ... Yes lets move away from box built, I want us to move into more
high quality designs and models. Make use of unity engine." The Emporium's art pass (Claude, branch `claude/art-emporium` on the
stage art foundation `claude/stage-art`; recipe: `docs/arenas/STAGE-ART.md`). Status: **implemented on the branch and seen in the
shared Editor; not pushed, not merged, not released.** The bank is local (git-ignored); the integrator packs it into the Drive zip.

With the stage bank `ChipArenaStageEmporium` the hall, its furniture and its rigs draw as meshes modelled in Blender, lit by
`ChipArena/StageLit`, in place of the kit's about 7,300 boxes, the dressed blocks and nearly all of the rig's boxes. Presentation
only: the map record, colliders, nav, hazards and their timings, spawns and every rules check are untouched; without the bank
every box draws exactly as before (CI, a build without the Drive zip).

Modelled (tools/stages/emporium_art.py, emporium_fit.py, emporium_props.py on emporium_kit.py and stagekit.py):
- **The hall**: the floor as a mosaic (every checkerboard tile its own face over the grout, mint glazed quarry tile on the kitchen
  line, roller beds with half-round chrome rollers and kerbs, warning stripes as real slanted bands, round sauce splats, chrome
  basins round the geysers); the walls with a skirting, a glazed teal wainscot, chrome dado and picture rails, cream glazed tile,
  a pink frieze with fluted strips and a coved cornice, arched windows with frames, mullions and sills, framed posters with a
  hot dog in relief; the glazed roof's arched trusses (chamfered chords, a tubular top chord, a zigzag web), purlins, eaves beams
  and glazing bars whose stripes fall across the hall; lathed enamel pendant lamps; every neon letter and icon as round tubes.
- **The fixtures**: the wiener ovens (stainless bodies, a rounded red portal moulding, rotisserie windows with chrome bezels,
  spits and sausages, lathed knobs, striped scalloped awnings, a chimney with its rain cap, framed signs); the kitchens' tiled
  walls with chrome bullnoses and a striped awning over each door; the fry stations' canopy hoods; pantry racks with lathed
  bottles, lidded jars and loaves; the Big Dog's plinth (moulded base and cornice, chrome band, framed signs ringed with marquee
  bulbs) and the Big Dog itself (a frankfurter in its bun with a chef's hat, googly eyes, a smile, sneakers, gloves and neon
  mustard); the bun presses (posts with base plates and bolts, a rounded head, the ram's hydraulic sleeve, the bell's yoke,
  beacons); the giant sauce bottles (rounded squeeze bottles with wrap-round labels, dripping shoulders, ribbed twist caps).
- **The furniture**: channel-backed vinyl booths with rolled tops and cushions, banded tables with little sauce bottles; the
  glazed mint counters with chrome beads, a pink top with a chrome edge, lathed stools with foot rings, roller grills with real
  rollers, a sneeze guard on posts, a cake dome; soda fountains with taps and levers; arched jukeboxes; the pump dispensers as
  giant pump bottles; kiosks, fridges with tube handles, prep tables; the bun bridges (a frankfurter deck with a round belly,
  rounded bun kerbs with sesame, one ribbon of mustard); the balconies (plank floor, soffit downlights, a glazed fascia with a
  neon tube on standoffs, lathed fluted columns, chrome rails with panels).
- **The stairs** (the four bridge flights and the four balcony stairs, `emporium_fit.flight`, each on its step blocks): tread
  slabs that overhang their risers and the open sides, with a chrome bullnose, a chrome edge and a dark grip strip; risers in
  their own colour (mustard on the bridges, teal on the balcony stairs); a cut stringer on each open side whose stepped top
  follows the treads (frankfurter red on the bridges, pink on the balcony stairs) over a chrome bead, and under it a panel
  (the bun with its sesame; glazed teal tile); a tubular chrome handrail on each open side (up from the first tread, along the
  flight .78 m over the nosings, down to the last, on slender posts with floor roses) and a wall rail on brackets where a
  balcony stair runs along the hall's wall; the flight's back under its landing framed in chrome over a rubber kick strip.
  The handrails are thin props without colliders (as the stools are): a pilot and a shot pass them.
- The jukeboxes' cabinets also have a speaker grille on each side (cloth behind chrome louvres in a chrome surround) and a
  service panel on the back.
- **The breakable blocks** (block models, drawn where the block stands, sooted with damage): slatted bun crates (one mesh for
  the ten), staved pickle barrels with hoops, labels and a pickle (one mesh for the two).
- **The rigs** (37 slots, Core `NativeEmporiumArt.Pose`, from the same replicated state and the same functions as
  `NativeEmporiumRig`): the Wiener Run's frankfurter (rolling, with charred grill marks) and its face that does not roll (domed
  googly eyes with wobbling pupils, brows, a grin, the mustard squiggle), the ovens' pet-door flaps; the presses' sesame bun
  (the rig's squash and stretch as a scale), chrome ram and brass bell; each griddle zone's grate over a bed that glows by its
  heat, and the popcorn sausages; the geysers' caps, pump rods and plunger heads, the lumpy sauce column and its splashing head;
  the turning sign (frankfurter in its bun, neon mustard, a marquee ring whose bulbs chase in three groups, "TOP DOG" in tubes
  on both faces of its board); the conveyors' hangers with their buns; the roller grills' turning sausages; the Big Dog's waving
  forearm and blink; the jukeboxes' bubble tubes.

What still draws as boxes, and why: the **telegraph lamps** (lane edges, press posts, griddle corners, geyser rims) and the
presses' **drop shadows** stay the box rig's (`NativeEmporiumRig.Kept`): they are small flat glowing lenses whose colours change
per lamp per frame (about 120 of them), they are the hazards' telegraphs (never reduced) and they read as lamps, not blocks;
a destroyed crate's or barrel's stub and every fragment (the dressing's, as on every map); the effects' quads and the backdrop
(Core NativeEmporiumScenery's SourceLit sectors, unchanged).

Code: `Core/NativeEmporiumArt.cs` (slots, `Pose`, `Casts`); `Core/NativeEmporiumRig.cs` (`Kept`, and the helpers the rig and the
art share: `BunPlace`, `Head`, `Bell`, `Popcorn`, `Bulge`, `PumpOf`, `Hanger`, `Grill`, `Blink`, `Wave`: the box rig poses exactly
as before); `Core/NativeEmporiumKit.cs` (`Family`, `Owner`, `Kind` for the layout export; nothing draws by them);
`Runtime/NativeEmporiumPresentation.cs` (the art's Prepare and Render; the kit only `if(!ArtDrawn)`; under the art only the
rig's kept boxes); `Tests/StageArtLayout.cs` (the Emporium's branch); `Core/NativeStageArt.cs` (the bank's line).
Foundation additions (small, general): the stage shader's `DETAIL_TILE` (16: glazed tiles on the world's grid, the parameter the
tile's size) and its constant in `stagekit.py`.

Rebuild (from `unity/ChipArenaConversion`, about 15 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout emporium LAYOUT.json
blender --background --factory-startup --python tools/stages/emporium_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b]` renders the Fine tier and the rig at a telling moment in Blender; `--tiers`, `--no-ao`,
`--static-only` for quick looks.) The bank: `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageEmporium.bytes`, 18238817 bytes
(the budget is about 20 MB), 541 props. Static: 48 chunks; Fine 246,188 triangles (175,320 vertices), Mid 115,602 (91,330), Low
63,698 (64,455). Rig: 37 parts; 14,166 / 7,893 / 3,928 triangles. Blocks: 1,794 / 1,540 / 626. The Low graphics tier draws the
bank's Low tier (the same art without the smallest details), not the box kit.

Checks: `dotnet run --project Tests/CoreTests.csproj -c Release -- --emporium-art` (`Tests/EmporiumArtChecks.cs`, also in the full
run): the slots; every posed model against the box rig's own part at every 0.05 s of two rounds (worst under 2 mm; the wiener both
ways, the buns' squash, stretch and slam, the geysers, the hot griddles, the flaps, a blink, the marquee's chase); the kept boxes
are lamps and drop shadows only; the draws fit the buffer, are deterministic and allocation free; the layout export; the wiring
and the bank-absent fallback; the tile surface; and, where the bank is on the machine, the bank: every block covered, the crates
and barrels and every rig slot in three tiers, the chunks' tiers, the budget, the play-space rule (below 2 m over the floor or a
balcony the art keeps within .25 m of a block: 796 of 82,597 vertices over, the farthest .81 m, a handrail's bend over the
first tread; the check allows 1 in 100, so 825 here: little room is left for more thin props) and the spawns. `--emporium`
(13,328 checks) and `--stage-art` pass unchanged.

Seen in the shared Editor (a match, one idle bot, the game's camera placed by `maincam`): before (the box kit, `d0c8df39`) and
after, the same eight views (the hall from the south-west and from the north balcony, the north lane, the east bun press, the
Big Dog and the sign, the north diner, the counter, the Ketchup Kitchen); the hazards on the art (the wiener rolling in both lanes with its face, a ketchup geyser bursting beside its pump, both griddle pairs heating to white-hot with their popcorn sausages, the buns hanging in their presses; a press's slam was not caught in a shot); the Low tier.
Frame time (Editor, the hall from the south-west, 10 s windows): High, the box kit 9.27 ms mean / 10.60 ms p95; the art 9.08 /
10.27 and 9.32 / 11.01 (two runs: the same within the runs' noise). Low: the box kit 7.57 / 8.85; the art 7.39 / 9.55.

Not done / falls short:
- The kit's secondary pieces are rebuilt from their own parts as chamfered, finished pieces rather than sculpted (the fridges'
  frost, the kiosks' screens, the ovens' sign boards, the fryers' baskets, door signs, the conveyor rails as plain rods).
- The glass (windows, screens, the sneeze guard) is opaque: glowing or glossy faces, not transparent panes.
- Round shapes sit inside square colliders (the barrels, the balcony columns: up to .25 m of unseen corner); the big bottles
  and pumps are rounded squares so their cover reads true.
- Practical-light shadows, the main menu's 3D preview, a native Windows build, multiplayer guests and the killcam on the art:
  not run (as the foundation's doc says for the Sanctum).

## Seen (Unity Editor, 2026-11-10)

Played in the shared Editor (3 bots) on the branch, main camera with full post (bloom, toon outline): the hall and vault, the
turning sign, the diners, the kitchens, the wiener rolling in both lanes, the bun press's warning, slam and squash, both griddle
pairs going hot in turns, both sauce geysers bursting (a pilot carried up to the balcony), the Low tier (graphics: performance).
Frame time in the Editor (12 s windows without screenshots, 3 bots): this stage mean 10.1 ms, p95 11.5 ms; Deepwell in the same
window mean 10.6 ms, p95 15.4 ms; this stage on Low mean 11.9 ms, p95 19.3 ms (a window nearer the match start). The static kit
is about 7,300 instanced parts (about 15 NativeBlockLit draws), the rig about 1,350 parts a frame, the effects up to about 600
quads.

## Not done / open

- Music (above). Owner clips for the cues may replace the synthesized samples (keys `emporium.*`).
- Regenerating the catalogue also writes `rotation: true` into the five M1 records (the generator's current rule; the merge
  already forced it), so their bytes change with no behaviour change.
