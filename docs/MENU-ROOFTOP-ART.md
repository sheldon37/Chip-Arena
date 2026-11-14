# The main menu's rooftop: the art, the screens, the logo

2026-11-11 · Claude (visuals) · branch `claude/menu-rooftop` from `claude/sleepy-ritchie-g8ruhv` (`f0275643`).
Status: **implemented on the branch and seen in the shared Editor at High and Low** (see "Seen in the Editor").
Not merged, not released. The bank is local (git-ignored); the integrator packs it into the builder's Drive zip.

Owner (2026-11-11), on the home screen's "Pilot's Rooftop": "I would like to bring this to update to date non-boxy style as
well. Much improvements over our current design (Changing animations happening on the TV screens as well, since things on
there are out dated. Maybe reference the "Ricochet" thats on there currently as an easter egg lol)". Later the same day: a new
logo ("This is also the new logo for the game") and "Yes, icon too".

Four things changed: the scene is modelled (1), the holo screens rotate ads of today's game (2), the old RICOCHET card is an
easter egg (3), and the game's logo is the owner's new one, with an icon set made from it (4). The recipe behind (1) is the
stages' (docs/arenas/STAGE-ART.md); this page is what the menu adds to it.

## 1. The modelled scene

### What Core still decides

Core `NativeRooftopCity` builds the same deterministic city as before: the camera loop, the lots, every tower's heights and
setbacks, the roof plant, masts, tanks, signs, the actors' sites, the lanes, the lights and the sprites. Nothing about the
composition moved. Two additions make the art possible:

- **Parts** (`RooftopQuad.Part`): `PerchPart` (our own roof and the blade sign), `CityPart` (the 168 towers the loop's camera
  sees within Core's middle distance, 750 m, with their built detail, every placed lot: the actors' roofs and the screens'
  towers, the bridge, the huts, the screens' frames) and `Far` (everything else: the 1,309 far and unseen towers, the street's
  glow, the puddles). The art can stand in for the first two.
- **Notes** (`Build(quality, notes: true)`; only the layout export asks): one record per piece Core builds (a lot, a tier, a
  plant unit, a mast, a tank, a billboard, a balcony, a condenser, a fire escape, a hanging sign, a crown, a helipad, a trim, a
  screen's frame ...) with its numbers. `Tests/RooftopLayout.cs` writes them with the camera, the perch, the screens, the
  sites, the sprites and the composition's targets:

```
dotnet run --project Tests/CoreTests.csproj -c Release -- --rooftop-layout LAYOUT.json
blender --background --factory-startup --python tools/stages/rooftop_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets
```
About 20 s. `--preview DIR [--views a,b]`, `--tiers 0,2`, `--no-ao`, `--stats` (where the faces go, which faces still share a
plane), `--check` (the sight lines).

### What is modelled (tools/stages/)

`rooftop_art.py` (the run), `rooftop_kit.py` (the layout in Unity's frame, finishes, facade faces, footprints),
`rooftop_perch.py`, `rooftop_city.py`, `rooftop_cars.py`, on `stagekit.py` and `cybercity_kit.py`.

- **Our roof**: the lip the pilot sits on (Core's .32 m top, .42 m deep) under a coping that overhangs, with the cyan strip as
  a diffuser in a channel under it, expansion joints and flashing; a membrane with laps, ballast, a paver walk, drains and wet
  patches (baked into the smoothness: the sky lies in them; Core's puddles with their rain rings stay on top); the HVAC unit
  where Core's stands (a panelled casing on feet and pads, louvres, the fan in its cowl under a guard, a control box with the
  green lamp, a conduit and a condensate line); the vent stack with a flashing collar and a cowl on stays, mushroom vents; the
  lattice mast with rings and diagonals, its dish, antenna panels, a cable to a junction box and guy wires; the pipes on
  sleepers with flanges and a valve wheel; the cable tray along the lip as a ladder tray with its cables; banded cases under a
  tarp; the tool case, two cans, a thermos and a lantern by the pilot; the string of lamps (a bulb under a shade at each of
  Core's glows); the stair house (door in a lit frame, a ROOF ACCESS board in tube, a ladder), the tank, a skylight, condensers
  and a dish toward it (in frame at 21:9); the blade sign's casing in magenta tube; our tower below with moulded storey ledges
  and a service run down its side; the old TV set (3).
- **The towers**: every wall is a *facade face*: the bank carries it for the menu's own facade shader, so a modelled tower
  keeps its procedural bays, rooms and lights and matches the far towers that stay quads. The shapes are the art's: glazed
  towers with generously rounded corners, a dark crown band, fins, and a lantern of lit storeys under a hipped glass roof;
  corporate towers stepping back on moulded ledges, tubes up each tier's corners, a braced crown ring or a helipad (deck on a
  drum, safety net, painted ring, lit H, edge lamps); residential slabs with balconies (glass or solid upstands, rails,
  planters), condensers with fan grilles, tanks on braced legs with hoops, a lid and a ladder, and billboards (lattice posts, a
  board, a word in neon tube inside a tube border, a catwalk, flood lamps); masonry mid-rises with a bracketed cornice, fire
  escapes (grated landings, rails, flights with treads) and hanging signs with stacked tube letters; parapets under copings;
  roof plant (air handlers on skids with louvres and fans in cowls, ducts with flanges, stair houses); lattice masts under
  Core's beacons; the neon trims as tubes.
- **The actors' roofs**: flat and clear where the pilots stand, run and leap (everything keeps 5 m from every composition
  target and under half a metre, so no sight line is cut), a low coping, laps, and low plant along the edges (skylights,
  condensers, vent cowls, hatches, cable trays, bollard lamps); the duel's roof takes a painted ring; the skybridge is a deck
  on a trussed girder with glass balustrades and lit handrails; the huts have doorways.
- **The screens' frames**: a moulded bezel with corner nodes, a ribbed casing, lattice brackets back to the tower, a service
  catwalk, flood lamps on Core's lamp glows.
- **The flying cars** (`rig/car-<kind>`): five hover vehicles (a cab, a saloon, a coupe with fins, a van, a hauler) lofted
  from superellipse sections with glazed canopies, lift pods glowing underneath, head and tail light bars and side strips, in
  place of Core's two boxes. Each lane car keeps one kind (`NativeRooftopBank.CarKind`); the near lane's two fly the fine
  models, the rest the low ones; drawn instanced.

Words on signs are made up (NOVA, HELIX, RAMEN, ARCADE ...). Nothing in the art names a class, a boss or a stage.

### The bank and the runtime

`Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageRooftop.bytes` (+ `.meta`), git-ignored. Its props (Core
`NativeRooftopBank`): `static/<name>/<tier>` (scenery for `ChipArena/NativeRooftopModel`), `facade/<name>/<tier>` (walls: uv0 the
metric coordinate along the wall and the tower's top, uv1 its seed and its style + .9 x its lit share, colour.r its hue),
`rig/car-<kind>/<tier>`, `cover/perch`, `cover/city`. Tier 0 is the High and reduced-effects tiers, 2 the Low graphics tier
(leaner rounds, no condensers, rails, treads or tube letters on hanging signs).

- Runtime `NativeRooftopModels.Load(low)`: reads the bank once per quality tier into two meshes (all the solid scenery; all
  the walls, turned back into the facade shader's layout) and the car meshes, then lets the bank go. `NativeRooftopScene`
  builds Core's three quad meshes without the parts the bank covers and draws the art beside them.
- `Resources/NativeRooftopModel.shader`: one short opaque pass for vertex-coloured art in the rooftop's own light
  (`NativeRooftopLights.cginc`: the street's glow, the eight lights' spill, the sky), the moon's key in the anime style's two
  bands, the sky mirrored by smoothness, the baked occlusion; a glowing finish (kind 11) is unlit at albedo x gain for the
  bloom; the same stencil mark and depth pass as the other rooftop shaders (the outline); instanced for the cars.
- **Without the bank** (CI, a build without the Drive zip, `--ca-no-pack-banks`): `Load` returns null, every quad draws, the
  box cars fly. `RooftopChecks` pins a hash of every quad and sprite the fallback draws to the city as it was on the base
  commit (`bfb9e5499c5d3542`, computed on `f0275643` with the same code), apart from the two things added on purpose: the TV
  set's twelve quads and its light.

### Numbers (2026-11-11, bank built on this branch)

- Bank: 19,756,588 bytes (18.8 MB), 22 props. The Drive zip's visual body was about 369 MB of its 480 MB cap.
- High: 196,567 triangles (192,075 solid in 260,779 vertices; 4,492 facade); Low: 69,156. Cars: 4,028 triangles for the five
  fine models, 1,516 low. Core's quads kept beside the art: 30,062 of 63,536 (the far towers, the street, the puddles).
- Draw calls with the art (counted from the code; not measured in the engine): 3 quad meshes + 2 art meshes + 6 screens + up to 7 car
  batches (5 kinds far, at most 2 near) + 6 sprite layers = 24, against 16 before (3 + 5 screens + 1 car mesh + 6 + the sky in
  both). The four opaque meshes also draw a depth pass each while the outline is on.
- Screen textures: 29 slides at 320 px and 3 fixed ads at 384 px, RGBA with mipmaps: about 18 MB (before: 5 ads at 384 px, 4 MB).

### No z-fighting

Same-facing faces nearer than a plane's tolerance are stacked by `stagekit.lift_coplanar`, by range: our roof 3 mm apart or
less set 6 mm apart (Low: 8 / 20 mm); the city within 260 m of the camera 12 mm / 30 mm; beyond, 50 mm / 100 mm (a 24-bit
depth buffer through the .5 m near plane parts about 11 mm at 300 m and 67 mm at 750 m; the layers the scripts paint on
faces start at those distances: `Ctx.o`). Measured after settling, the walls counted with the solids: perch 0 m2, city near
0 m2, city far 11.4 m2 at High (14 pairs, all of them a neon tube's own faces lying on their neighbours of the same finish);
Low: 0 / 0.8 / 52 m2, the same tubes.

**Against what Core draws over the art (2026-11-13).** Owner: "Small amount of Z-Fighting on the menu behind the player". The
measure above settles the art among itself. Core's seven puddles (the Far part: they draw over the modelled roof, 12 mm over
the sheet) were not in it, and the membrane's laps (14 cm strips across the roof every 1.9 m) stood exactly 12 mm proud: at each
of seven crossings a lap lay in a puddle's plane, on the roof behind the seated pilot, and the two traded pixels as the camera
drifted (a dark dash inside the puddle that crawled). The laps now stand 6 mm proud, under the water (`rooftop_perch.py`:
`PUDDLE, LAP`; RooftopChecks holds the script to Core's height). Low never had laps.

How it was found, for the next time: (1) a software scan of the menu's own view (each pixel's two nearest front faces among
the bank's scenery and walls, Core's kept quads and the screens: where both face the same way and lie in one plane, they
fight) named the pair, 3,100 to 3,800 px of a 1470 x 719 view round the loop, and nothing else in one plane that shows (a few
roof caps of one finish and colour overlap themselves; two puddles' quads overlap where both are cut away); (2) in the Editor
the scene's clock is held and only the eye steps along its loop (`NativeRooftopScene.CaptureEyeTime`, a DEV capture hook like
`CaptureTime`): with nothing animated, two frames of the same eye are identical to the pixel, so what changes between steps
is depth fighting or a thin line's aliasing.

- **Seen in the Editor** (shared Editor, High, 1470 x 719, no menu, the scene held at 30 s, the eye stepped .2 s at a time through ten
  steps at four places on its loop; released bank on `60bce18c`, rebuilt bank on `9c2c1245`; a control pair of the same eye differs
  in 0 px): before, each lap shows as a dark dash inside its puddle and as a strip that changes at every step; after, the puddles lie
  whole. Pixels of the roof's part of the view (735 x 299) that changed in three steps or more, at a 5 x 5 tolerance: 911 / 1,393 /
  887 / 488 before, 9 / 13 / 10 / 18 after; the whole view: 1,098 / 1,567 / 1,054 / 657 before, 196 / 188 / 178 / 187 after.
- **What is left** (not depth fighting): the far towers' neon tubes and the mast's lattice, a pixel wide or less, step along their
  length as the eye moves (aliasing of thin bright lines); near rain streaks slide (parallax); the
  dish's sky mirror moves with the eye; the pilot's own body changes (not isolated: probably its cloth, which runs on the real clock). The brand screen, 57 mm off
  its modelled backing 450 m away (2.4 steps of a 24-bit buffer), does not flicker.

**The rooftop bank's layers.** The bank writer records stacked faces as layers for `ChipArena/StageLit`
(docs/arenas/STAGE-ART.md section 14). `NativeRooftopModel` reads none, and the rooftop's chunks are named (`perch`,
`city-0`, `city-1`), which the writer has since taken for far scenery (`FAR_REACH`): a rebuild split 24,305 triangles more
(19,940,068 bytes). `StageBank.far_layers` (off for the rooftop) keeps a rebuild byte-identical to the released bank
(a0e5f332...) but for the change made: checked before the laps were touched.

### Sight lines

`--check` casts the camera's rays at 24 places round its loop to the 71 composition targets Core itself keeps clear (the
screens' centres and corners, the duel, the watcher, every 2 m of both runners' runs, the seated pilot): 0 blocked at both
tiers.

## 2. The holo screens

Core `NativeRooftopAds` (new) holds the rotating ads; `NativeRooftopCity.ScreenAnim` (was `ScreenState`) times them; the Runtime
rasterises each slide on a worker thread as before (`NativeRooftopAds.Compose`, engine-free) and uploads at most two a frame;
`Resources/NativeRooftopScreen.shader` animates them.

| Screen (kind) | Was | Shows now |
| --- | --- | --- |
| the largest, 48 m (`classes`, was `siren`) | The Siren, "Boss Test" | a slide for each public class (the content's weapons that are not `devOnly`, in its order: 14 today) and the roster ("14 CLASSES"), 6 s each |
| the 4:3 one, 30 m (`stages`, was `chip`) | the RICOCHET card | the nine remodelled stages and the Expedition, 8 s each |
| the far one, 34 m (`brand`, was `arena`) | the CHIP ARENA wordmark | the logo, "NINE STAGES", the logo, "EXPEDITION", 12 s each |
| the CHIP banner, the ARENA blade sign | | unchanged |
| the old TV set on our roof (`retro`) | | static; the easter egg (3) |

A slide is two layers the shader moves apart: the **picture** (rgb: a backdrop in the slide's accent, a kicker, the title in
its band, a ticker strip) and the **mask** (alpha: a pilot's silhouette in its family's pose (ranged levelled, melee with
the blade back over the shoulder, magic with an orb raised) beside the class's own icon; a stage's emblem: Breakwater's
lighthouse, Pier 9's wheel, Skyframe's garden top, Deepwell's dome, Polaris Station's observatory under the aurora, Brickfall's
wall, Ward Zero's cross, the Factory's gear, Cyber City's skyline; the Expedition's drop pod). Each slide plays in: the picture
wipes on behind a lit edge and eases back from a push-in, the mask strikes in from the right with speed lines, a flash and a
punch, the title slides in along its band, the ticker scrolls; slides change through a glitch cut; the logo takes a glint and
a slow glow. Reduced effects: no push-in, no glitch, the slide held past its entrance, the ticker still.

The art's glyphs gained the rest of the alphabet, the digits and the hyphen. The Siren's ad and the heartbeat and equalizer
loops are gone. Every rotation's length divides the scene clock's 4800 s wrap. **Nothing DEV only or unreleased is on a
screen**: the class slides are built from the content with `devOnly` left out, and `RooftopChecks` fails if any slide names a
DEV-only weapon or a boss (NOCTIEL, the Siren ...).

## 3. The Ricochet easter egg

The RICOCHET "RARE" chip card left the rotation. An old cream tube TV sits on a crate on our roof, left of the pilot, turned to
the camera (Core `RetroCenter`, .8 x .6 m; a two-box set in the fallback, a modelled one with dials, speaker slots and rabbit
ears in the art). It shows static in fat scanlines. For 16 s of every 96 s (first 70 s after the menu opens) the old card
bounces round it like a screensaver's logo, taking a new colour at every edge; its travel is timed (3.2 s across, 2 s up) so
that half way through each showing it hits a corner dead on. The set's glow is one of the scene's eight lights (it took the
stair house lamp's slot, which lit nothing in frame) and rises while the card shows.

## 4. The logo and the icons

- `Assets/ChipArena/Resources/ChipArenaLogo.png` (833 x 547, transparent): matted from the last, still second of the owner's
  logo video (the median of 20 frames; the background keyed out, the glows kept soft and un-premultiplied so no black fringe
  shows on any ground). Credit: docs/ASSET-CREDITS.md.
- The home screen's title and the panel screens' top bar draw it where the monoline wordmark was (`Wordmark` in
  NativeHomeMenu.cs: 3.3 cap heights tall at home, rising in over a slow warm glow); the brand screen's logo slide lays it on
  the screen's dark field. Without the image the vector wordmark draws as before.
- `Assets/ChipArena/Branding/ChipArenaIcon.ico` (16, 24, 32, 48, 64, 128, 256), `ChipArenaIcon-1024.png` and a PNG per size,
  made by `tools/branding/make_icons.py`: the full logo at 64 px and up; below it a mark drawn from the logo's parts (the
  purple chip, its gold pins, the orange swoosh, a cream C), 32 and 16 laid out on their own pixel grids so the pins stay
  whole pixels. **Not wired** into Player Settings, the launcher, the updater or the Site: that is packaging (Astra).

## Checks

`--rooftop` (Tests/RooftopChecks.cs, also in the full run). Pins that changed, and why:
- the screens' kinds (six, renamed for what they show) and their lights (five screens' + the sign: the TV set's light);
- `ScreenState` and the 4 s cut to a screen's own loop are gone: the checks are now the rotations (every slide shows, a cut
  only at a slide's ends, each rotation divides the wrap, carries over it) and the egg (16 of 96 s, first at 70 s, a corner
  hit half way, a new colour each bounce, static until then);
- the ads: three fixed ones (the card as `ricochet`), and every slide rasterised: its title and ticker drawn in their bands,
  its mask over the picture alone; the class slides equal the public weapons; no DEV-only class, no boss;
- new: the pinned fallback hash, the parts, the notes, the bank's plan (a synthetic bank), the wiring and the fallback (source),
  the logo; with the bank on the machine: its covers, tiers, budget (under 30 MB), facade vertices and the roof's span.

## Seen in the Editor (2026-11-11, shared Editor, 1470 x 719 Game view)

Captures of the home screen with the scene's clock held (DEV hooks `NativeRooftopScene.CaptureTime` / `CaptureBare`, set only by
the local capture tool): eight moments round the camera's loop with and without the menu, before (the base commit with the
hooks, no bank) and after, at High and at the Low graphics tier; every class and stage slide and the brand rotation; a slide's
entrance; the egg.

- Both new shaders compile and draw; the towers' walls draw through the facade shader; the cars fly instanced; the logo
  imports and draws as the title and on the brand screen.
- The title logo and the TV set are clear of the home screen's menu (the set sits under the last entry, left of the pilot).
- The egg: the card on the set at 71, 74, 77.95 (in the corner), 78.3, 82 and 84.5 s, a new colour each time. After the
  first look the card was made larger (half the screen) and brighter, and its bounce flash toned down; seen again on the
  final head (merged with shared `a3c26e5b`, after the boot intro): 10.08 ms mean, 11.85 p95 there.
- Frame time over 6 s of the running menu (Editor, back to back with other sessions' work, so indicative only):
  High 10.38 ms mean / 11.65 p95 before, 9.58 / 11.40 after; Low 9.85 / 10.92 before, 9.29 / 10.73 after.
- The bank's read: the first build read it on the main thread in 752 ms at the menu's first frame. It is now parsed on a
  worker thread (`NativeRooftopModels.Prewarm`; 136 ms there at High, 96 ms at Low); `Load` held the main thread 164 ms (High)
  and 112 ms (Low), the wait for the worker included. Anything shown before the home screen (the start-up intro) can call
  `Prewarm(low)` to hide the wait; nothing calls it early yet.
- Seen on the base commit, not caused here: at the Low graphics tier the quad fallback draws some of our roof's boxes in raw
  red, green and blue. With the art those boxes are not drawn.

## Not verified

- Draw calls in the engine (the counts above are from the code). Reduced effects, 21:9 and other aspect ratios in the engine.
- A native Windows build; the bank travelling through the Drive zip and the installer.
- `Prewarm` called early (no caller yet), and the first-frame cost in a player build.
- The panel screens' top-bar logo (only the home screen was captured).
- The icons are not wired anywhere.
