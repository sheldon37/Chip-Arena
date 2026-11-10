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

No song yet. Placeholder: `cybercity-neon-riot` (the record's `music`). The owner's song takes the key **`emporium-theme`**
(`Assets/ChipArena/Resources/Audio/music/emporium-theme.mp3`, a `Theme` case, a ZoneMusicChecks pin and a prompt row).

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
