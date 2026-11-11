# Polaris Station (`polar`), rebuilt

2026-11-11 · Claude · branch `claude/rework-polar` (base `claude/sleepy-ritchie-g8ruhv` at `f81e663e`) · one of the five M1 stages
rebuilt from scratch (owner, 2026-11-11: "I want to revamp ALL the current stages out of the blocky style ... Maybe even
COMPLETELY rework the following stages: Polaris Station, Deepwell, Skyframe, Pier 9, and Breakwater ... a unique feel, very good
designs, and nice animations"; the theme below approved the same day: "Yes, start them").

Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The map keeps its id
(`polar`: saves, lobbies and the Random list key on it), its name and its look id (`polar-aurora`). The M1 stage of the same id
(docs/arenas/NEW-STAGES.md section 5, `docs/arenas/scripts/polar_m1.py`) is history: nothing of its layout, blocks or Toon Harbor
dressing remains, and the map is off the shared M1 code (Core `NativeNewStage*`).

**Pitch.** An arctic observatory on the ice shelf under a moving aurora. A giant radio dish turns and tilts over the observatory
terrace at the south; an icebreaker lies frozen into the shelf at the north, her bow toward the middle and her foredeck part of
the play space; the channel she cut has refrozen thin between them. Timber cabins with warm windows either side, a red lab and
a blue generator shed in the corners, ice crystal formations and heaved floes across the field, snow drifts, steam from vents,
a weather balloon rising, the sea past the shelf's open south edge. Blue snow light from a low moon, green from the aurora,
warm windows and strings of lamps.

Original design: no name, layout, look or element is taken from any other game.

## Layout (docs/arenas/scripts/polar_m2.py)

x east, z north, y up; **mirror symmetric about x = 0** ((x, z) -> (-x, z)); team A west, team B east. 258 blocks (26 of them
breakable, structural or carried), 8 FFA spawns and two team tables of 4; the shelf is 66 x 52 m, the play space x +-31,
z -25 .. 25 (62 x 50 m), ceiling 22, **killY -3** (the sea stands at -1.3: a body dies about half a second after it leaves the
shelf's edge or sinks with the ice).

| Level | y | What |
|---|---|---|
| The shelf | 0 | The whole field: both cabins' yards, the north pockets either side of the hull, the road before the terrace, the shelf path behind it. |
| The terrace | 2.4 | The observatory's platform (16 x 7.4 m) under the dish's pedestal: a stair up each side (12 risers), a parapet toward the field open in the middle, consoles (half cover), a windbreak and instrument racks behind (full cover round the spawn). |
| The foredeck | 3.2 | The icebreaker's deck from her stem (z 8.2) to the wheelhouse front (z 24): a gangway up each side (16 risers to a landing), bulwarks (half cover), the windlass, the foremast, the winch house and two lifeboat pods (full cover), crates. |

- **The middle.** The channel (x +-2.6, z -8.4 .. 7.8) is two ponds of **thin ice** either side of a solid crossing (z +-1.2). Slabs of
  ice the icebreaker heaved up line both banks (full cover), open at the crossing and at one gap a pond. South of it the road
  runs before the terrace, round the great crystal cluster.
- **Routes between the halves:** the crossing (narrow, covered by the floes), the thin ice anywhere (fast; it may be gone), the road
  before the terrace (two steam vents), over the terrace, the shelf path behind it (3.6 m wide along the open edge: a fall into
  the sea), and over the foredeck (a vent at each gangway's foot). The hull divides the north: the pockets either side meet only
  over the deck or round the bow.
- **Cover:** ice crystal formations (full, 3.0-4.4 m), heaved floes (full, 2.8-3.8 m), fuel tanks, snowcats, huts, tarped cargo,
  the cabins' and the lab's windbreaks (full); snow drifts, sleds, blades, benches, stoves (half, 1.0-1.2 m); crates and fuel
  drums (breakable).
- **Spawns:** team A in cabin A behind its screen wall (two doors east, the screen's ends round to them), team B in cabin B. FFA:
  both cabins, both labs (north corners), both generator sheds (south corners), the terrace behind the pedestal, the foredeck
  behind the winch house. The draft checks every FFA and A-B pair blocked under +-1 m jitter at three eye heights (3D), spacing
  (FFA 17 m, A-B 55 m), 3 m from every vent and 6 m from the thin ice on the spawn's floor, every spawn and level reachable on
  foot, no team spawn seen from the deck or the terrace, no ground sightline over 50 m (the longest 49 m; the map's diagonal is
  80 m), no tops in the 1.3-2.2 m band, mirror symmetry.
- **No invisible walls.** Six pilot columns in all, none lower than 4.9 m: the caps of the four pressure ridges (5.2-7.4 m, the
  west, east and north walls) and of the wheelhouse front (12 m), which keep pilots inside the map, and one over the pedestal's
  top (9.5 m, 7 m over the terrace) where the dish's yoke turns. Every roof, tank, floe, crystal, rail and bulwark can be stood on
  by whoever reaches it. The south edge has no rail and no column: a pilot who walks or is knocked off it falls into the sea.
- **Nav** (generator): 2942 nodes, 4888 links (64 gated by the vents); every spawn and all three levels reachable from FFA 0, and
  still reachable with all four sheets gone (their links leave with them: `tools/generate_stage_hazards.py` `segment_nav`).

## Hazards (existing kinds only; no rules change)

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **Steam vents** `polar-vent-road-{w,e}`, `polar-vent-gang-{w,e}` | `vent` with damage | The road before the terrace (x +-5.2, z -11.6) and each gangway's foot (x +-6.8, z 9.6): a 1.6 m square column 3.4 m tall | Period 14 s each, starts 1, 8, 4.5 and 11.5 s after the arm delay: one somewhere every 3.5 s. 9.6 s quiet, 1.8 s telegraph, 2.6 s burst. The first telegraph comes 14.6 s into the fight, the first burst 16.4 s. | 8 HP every 0.5 s to whoever stands in the column through the burst (up to 48 HP a burst). No push. Bots wait (the vent nav gate). |
| **Thin ice** `polar-thin-{n,s}-{0,1}` | collapse segments (`pancake`) | The channel's two ponds, two sheets each (5.2 m wide, 3.3 and 3.6 m long), each two `carried` plates either side of a `structural` fracture seam (36 HP) | A blast that breaks a seam (structural members take blast damage only, never before FightAt + 4 s) cracks its sheet for 1.4 s, then it sinks 3.2 m in 1.8 s and is gone. | Whoever stands on it rides it down under the sea (-3.2 m: under killY once the plates go) unless they jump clear in the first half second or so; open water stays for the round. At most three sheets fall at once (the destruction rules). |
| **The shelf's edge** | open edge to killY | The whole south side (z -25) | Always. | A fall kills in about half a second, credited to whoever hit the pilot last. |

Nothing damages in the fight's first 4 s. The vents and the sheets are pure functions of the replicated state (the Factory's
hazard clock; the segments' own replicated schedule), so every client and the killcam draw the same moment. No hazard state is
added to the network frame; no simulation, damage or networking code changed.

## The look (`polar-aurora`, rewritten)

- **Light:** a cool moon low in the south-east over the sea (the key, bearing 152, elevation 30, soft shadows), the aurora's
  green from the north as the rim (it edges every pilot), a blue trilight; linear fog 70-1100 m in deep navy; the night sky with
  stars and the moon's disc; the anime style on (blue shade, saturation 1.22, contrast 1.1, green rim); the advanced lighting
  (light volume, scatter, beams, **snow**); eight warm practical lights (both cabins, both labs, both sheds, the foredeck, the
  observatory); far plane 1500 m.
- **The aurora (effects, Core NativePolarFx):** four curtains arching round the north of the sky, 250-535 m out and 70-200 m up,
  each of rays green at the foot, teal through the middle, violet to the top, a rose fringe on the brightest, a bright hem along
  the foot; folds travel along them, pulses run through them and they sway, all match long (`NativePolarLooks.AuroraRay`); its
  light lies in long shimmering streaks on the sea and as a sheen on the thin ice.
- **Animated set pieces (all match long, from the field clock, the same on every client):** the **great dish** turning a round in
  96 s with a drift on it and tilting between 32 and 72 degrees over 74 s (its rim never lower than 7.8 m); the icebreaker's
  **searchlight** panning over the field (a volumetric shaft while the look's beams are on); a **weather balloon** let go every
  46 s from behind the west ridge and the east in turn, rising across the field with its sonde swinging under it; the weather
  masts' **anemometers**; stove smoke from every roof, the funnel's steam, snow falling, spindrift over the ice, the masts',
  pedestal's and dish's red beacons.
- **Hazard looks (never reduced by the quality or the low tier):** a vent's wisps when idle, an amber ring closing on the grate and
  beating faster, hissing jets and its four lamps amber through the warning, the white roaring column, a hot ring and white-hot
  lamps through the burst; a sheet's seam as a cold line, its cracks running from the seam to its edges and a red beating border
  through the 1.4 s crack, spray along its edges and a splash as it goes, rings on the open water and floes bobbing after. The
  destruction kit's own stress, fall and land effects and sounds play on top (as on every map with segments).
- **Audio:** the vent's hiss and burst on NativeAudio's voices ("polar.vent.hiss", "polar.vent.burst" bank keys the owner's bank
  may fill); the ice through the destruction kit's structure cues.
- **Low tier:** the beams fall back to billboards, the look's weather is off (the effects' own snowflakes stay), atmosphere counts
  drop to a third; telegraphs are unchanged; the art draws its Low tier.

## Stage art (the bank `ChipArenaStagePolar`)

docs/arenas/STAGE-ART.md is the recipe. With the bank (git-ignored, from the builder's Drive zip) `NativeStageArtDraw` draws the
station modelled in Blender in place of every map block's box, the box kit, the box rig and the box-built backdrop; without it
the boxes draw (below). The gameplay geometry is the map record's alone.

**Modelled** (`tools/stages/polar_*.py`; the west half modelled and mirrored, the ship and the observatory whole):
- *The shelf*: wind-packed snow with a few centimetres of sastrugi, the channel's walls and the shelf's cliff in layered blue
  ice, the sea, brash ice at the cliff's foot; the vents' collars (a steel ring, a grate, a hazard-striped rim).
- *Pressure ridges* (the walls): tumbled, faceted ice blocks 5-8 m high, snow on what faces up, falling away outside.
- *The icebreaker*: the hull lofted through the bow's blocks (boot-topping, red sides, a white strake, a rubbing strake, lit
  ports, anchors in their hawses, snow at her waterline), the planked foredeck with its steel margin and toe rail, bulwarks with
  stanchions and a timber cap, the winch house, lifeboat pods on cradles, the windlass's drums, the foremast with its crow's
  nest, yard and stays, strings of lamps down the stays, timber gangways; the wheelhouse front in three storeys with a walkway,
  railed bridge wings and a band of lit windows under a visor; past the bound her house, the raked funnel, the mainmast with its
  lights, boats in davits, the after house and a crane.
- *The observatory*: the concrete terrace with its plinth, coping and lit strip, stairs, the slatted windbreak, instrument racks
  with lit screens, consoles, the parapet, the pedestal (square to 2 m, then drawn in to a round tower with collars, a door,
  beacons and a gallery), the shelf beacon.
- *Buildings*: the cabins in courses of planks on a base course with corner posts, lit four-pane windows in frames with sills
  (dark glass inside), door lamps, low steel roofs under a thick lid of snow rolled over at the eaves, icicles, stovepipes;
  the labs and sheds in ribbed sheet; windbreaks before the doors; stoves with a glowing door, bunks, benches, the gensets.
- *The field*: ice crystal formations (a core and leaning six-sided crystals with lit seams), heaved floes (faceted slabs, deep
  blue at their feet, snow on top), drifts (a skirt and a crest in one heap), fuel tanks (a drum on a skid with corner posts,
  hoops, a hazard band, a ladder), snowcats (tracks round their wheels, an orange body, a cab with lit windows, lamps, a curved
  blade), louvred instrument huts with hipped roofs, tarped cargo roped down, lattice weather masts, sleds; strings of warm
  lamps from the cabins' eaves to the masts, tanks, huts and labs (all over 2.6 m up).
- *Breakable and sinking blocks* (block props, drawn where their blocks are): crates (boards in battens, a brace), fuel drums on a
  pallet, the thin ice's plates (clear blue, frost and cracks) and its seams (a dark refrozen strip with a flagged stake at each
  end: the flags mark the thin ice and go with it).
- *The rig* (`Core/NativePolarArt.cs`, eight slots): the dish (a panelled paraboloid with its back's trusses and ring girders,
  hub, counterweights, the feed on four legs and its red lamp), its yoke (a turntable, two arms to the bearings, the drive's
  cabin), the searchlight, the balloon and its sonde on its line, the anemometers, the vents' lamps (white light times the
  draw's tint: dark, amber, white-hot), the floes that bob where a sheet has gone.
- *The backdrop* (one tier): the sea to the horizon, the shelf beyond the ridges with the wind's ridges across it, its cliff
  east and west, icebergs and floes, the ice cap's rim (ranges round the north, snow on their shoulders, dark rock where sheer),
  a far met tower and depot.

**How it moves.** `NativePolarRig` tags each box part it builds with the art's slot beside the very add (`Slot`, `Level`); the art
poses each model at its tagged part's centre and turn, so the dish, the balloon and the lamps stand exactly where the box rig's
do. The thin ice's plates and seams are block props: the drawer follows their props as the segment sinks.

**Materials** (`ChipArena/StageLit`, unchanged): snow on the wax surface (the light wraps), smooth ice, painted steel at a quarter
metal or less (an open-air stage), timber grain along the boards, emission above 1 on the windows, lamps, beacons, screens and
the crystals' seams for the bloom, baked occlusion (eased to three quarters) and wear.

**Low graphics tier**: the bank's Low tier (the same art, about a quarter of the triangles), not the box kit. The Mid tier's props share the Fine meshes.

Rebuild (from `unity/ChipArenaConversion`; about 15 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout polar LAYOUT.json
blender --background --factory-startup --python tools/stages/polar_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage with the rig and the far backdrop in Blender;
`--tiers`, `--no-ao`, `--static-only`, `--stats`.)

## The box kit (without the bank)

Core `NativePolarKit` (727 NativeBlockLit parts on the dressed blocks: the hull's bands and deck, lit windows, snow caps,
the ice's facets and lit seams, hoops, tracks, grates; the sea under the shelf), `NativePolarRig` (the dish of 48 petals on its
yoke, the searchlight, the balloon, the anemometers, the vents' lamps, the floes) and `NativePolarScenery` (the shelf, the
icebreaker's after body, bergs, the horizon; baked SourceLit sectors). Every attached part stays within .25 m of its block (a
stovepipe within .9 m), the sea lies under y -1, every overhead rig part clears every standable top by 2 m, and the blocks and
the kit lay no two same-facing faces in one plane (PolarChecks).

## Music

The stage keeps its current song `polar-pulse-de-la-mort` (`NativeStageMusic.Theme`) as the placeholder. The owner's new song
takes the key **`polar-theme`**: its Suno prompt (style and lyric fields) is in docs/arenas/STAGE-MUSIC-PROMPTS.md "Polaris
Station — Northern Lights Array". To drop it in: the MP3 at `Assets/ChipArena/Resources/Audio/music/polar-theme.mp3` with a
streaming `.meta`, its asset-lock entry and length pin, and `case "polar":return "polar-theme";` in `NativeStageMusic.Theme`.

## Files

- Draft: `docs/arenas/scripts/polar_m2.py` (layout, hazards, segments, checks, plan PNG; `polar_m1.py` stays as history).
- Core: `NativePolarLooks.cs` (ids, kit kinds, the places and motions everything shares, the vents' and sheets' presentation,
  cues), `NativePolarKit.cs` (box kit and rig), `NativePolarScenery.cs`, `NativePolarFx.cs`, `NativePolarArt.cs`.
- Runtime: `NativePolarPresentation.cs` (draw component, SourceWorld and prewarm hooks, audio cues).
- Art: `tools/stages/polar_art.py`, `polar_ship.py`, `polar_props.py`, `polar_kit.py`.
- Checks: `Tests/PolarChecks.cs` (`--polar`), `Tests/PolarArtChecks.cs` (`--polar-art`); both in the full run.
- Data: `MapLooks.json` (`polar-aurora` rewritten), `UnityMaps.json` and `tools/asset-lock.json` regenerated (only the `polar`
  record changed), `PackDressing.json` (the old stage's Toon Harbor entry removed).
- Shared files touched (small): `Core/NativeNewStageLooks.cs` (`polar` out of `Maps` and `Looks`), `Core/NativeStageArt.cs` (the
  bank's line), `Runtime/NativeSourcePresentation.cs`, `NativeStagePrewarm.cs`, `NativeAudio.cs` (one line each),
  `tools/generate_stage_hazards.py` (`DRAFTS`, the `EDGES` and `OVERHANG` entries, `segment_nav`), `Tests/Program.cs` (two
  flags, two full-run lines), `Tests/StageArtLayout.cs` (one line), and the M1 pins that named Polaris: `NewStagesChecks`
  (its map list), `NewStageLookChecks` (a column example, the weather line, the dressed counts, the overhead threshold 150 to
  100), `StageKitChecks` (the expected count), `TransitionLoadingChecks` (the prewarm entry).

## Numbers and what was seen (2026-11-11)

**The bank**: 9,257,908 bytes (budget: about 20 MB), 643 props, deterministic. Static: 94 map chunks; Fine 125,278 triangles
(108,406 vertices); Mid shares the Fine meshes (none of its own: `--own-mid` builds one, about 3 MB more); Low with the far
backdrop's five chunks 33,342 (47,155). Rig: 8 props; 6,522 / 3,440 / 1,652 triangles. Breakable and sinking blocks: 26 props;
11,728 / 10,528 / 3,224. Up to 33 rig draws a frame.

**Z-fighting** (`zfight.py`, coplanar same-facing overlap between art parts): Fine 0.07 m2, Mid 0.07 m2 (the Fine meshes), Low
0.00 m2; no uncovered block's face coincides with the art. The static scene goes through `stagekit.lift_coplanar` at every
tier (1,300 pairs and 83 m2 settled at the Fine tier). The box kit and the dressed blocks: no two same-facing faces in one plane
(PolarChecks, SurfaceChecks' own rule).

**CoreTests** (Release, this PC): box kit 727 parts; rig up to 93 box parts a frame; effects up to 1,341 quads a frame (low tier
522); box backdrop 14,320 vertices; the rig and effects build in about 0.1 ms a frame, allocation free. Map record: 258 blocks,
6 pilot columns, nav 2,942 nodes, 4 segments, 4 hazards.

**Frame time** (shared Editor, High tier, from cabin A's door east across the field, 10 s, one idle bot; other agents shared the
machine): the M1 stage before (from its west garage toward the hall) 1,261 frames, mean 7.94 ms, p95 9.03 ms and 1,165 frames,
8.59 / 10.47 ms; the rebuilt stage with its art 1,136 frames, mean 8.82 ms, p95 10.22 ms and 1,142 frames, 8.76 / 10.07 ms:
level with the M1 stage's second run, about 0.9 ms over its first (not under it). Low graphics tier: 1,053 frames, 9.50 /
10.65 ms in the one run taken (no faster than High in that run: the runs' spread on a shared machine is as large as the gap).

**Seen in the shared Editor** (a match, one idle bot, god mode, the game's camera placed by `maincam`): ten views of the M1
stage before and of the rebuilt stage with its art after (overviews from the south-west and north-east, cabin A's door across
the field, the channel north to the bow under the aurora, the foredeck, the terrace north to the icebreaker, the road and the
terrace with a vent bursting, the shelf path and the sea, the north-west pocket and the lab, inside cabin A); the same ten on
the Low graphics tier; the box kit without the bank (an earlier commit; several of its windows drifted); the thin ice whole
with its seams' flags and the ice's glints. The aurora, the searchlight's shaft, the balloon, the strings of lamps, the dish
at different turns and a vent's burst are in those frames.

**Checks run**: `--polar` (7,256 checks) and `--polar-art` (with the bank: every block covered, the play-space rule, the spawns);
the full CoreTests suite (1,002 PASS lines, exit 0, the bank present); the Unity compile check; the Editor's build checks
(`PrototypeBuild.Validate`: ok on `5410178b`).

**Not done / not seen**:
- **The thin ice cracking and sinking was not seen in the Editor.** The scripted attempt (the Bombadier's attack on a seam) fired
  nothing. Its rules are the existing segments' (PolarChecks runs the state machine: a broken seam cracks its sheet, it sinks,
  it is gone, its nav links leave with it), and its effects, floes and block props are checked against that state, but nobody
  has watched it, ridden it down or judged its 1.4 s telegraph. A seam takes blast damage only (the existing `structural` rule):
  36 HP may want tuning once it is played.
- The frame time is level with the M1 stage's, not under it.
- Played only by an idle bot: no human playtest, no network (guest) test, no killcam on the art, no native Windows build, the
  main menu's 3D preview not looked at.
- The dish and the balloon were seen in still frames at different moments, not as a recording; the anemometers and the vents'
  lamps were not looked at close.
- From the high overview cameras the far shelf reads pale and bare; in play it is hidden behind the ridges.
- A pilot can stand on the hull's bow steps where the modelled bow is smooth (the colliders step in half metres); the deck's
  outline follows the blocks within a hand.
- No shadows from the practical lights (as STAGE-ART.md says for every stage). No ambience loop (`polar-wind` is a key only);
  the vents use existing clips.
- The other M1 stages' shared code still carries Polaris's dead branches (Core NativeNewStageKit / Scenery / Fx `case 4`):
  left for the integrator, since four agents edit those files.

## Depth fighting in the middle (2026-11-11)

Owner: "Polaris station has BAD z fighting in the middle area". Each of the eight thin-ice plates (block models,
`tools/stages/polar_props.py`) carried its seven snow drifts at one height (5 mm over the plate) and its five cracks at another
(8 mm): the drifts overlap one another, so 9 to 11 m2 of faces lay in one plane on every plate (72 m2 in all) and traded
pixels from the match camera: jagged pale patches over the ice. The static scene's lift never ran on block models. Now each
drift and each crack has its own height (the drifts 4 mm up in 1.2 mm steps, the cracks over them from 14 mm in 1 mm steps) and
the bank writer gives every face that lies on another a layer the stage shader parts at any distance (docs/arenas/STAGE-ART.md
section 14). The plates, their drifts, cracks, seams and flags look as before; the hazard (the sheets' cracking, the red border,
the collapse) is untouched. The Low tier's plates carry no drifts and never fought. Measured: 72 m2 before, 0 after, at every
tier; seen from the owner's camera (45 degrees down, 12 to 16 m) at High and on the Low graphics tier, two frames 3 cm apart.
