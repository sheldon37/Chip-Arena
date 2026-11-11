# Deepwell (`rig`): Deepwell Station

2026-11-11 · Claude (stage rework) · branch `claude/rework-rig` (base `claude/sleepy-ritchie-g8ruhv` at `f81e663e`) · one of the
five M1 stages rebuilt from scratch (owner, 2026-11-11: "revamp ALL the current stages out of the blocky style ... COMPLETELY
rework ... Polaris Station, Deepwell, Skyframe, Pier 9, and Breakwater ... a unique feel, very good designs, and nice animations";
the theme below approved the same day: "Yes, start them").

Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The stage's bank is
local (git-ignored); the integrator packs it into the builder's Drive zip.

The map keeps its id (`rig`: saves, lobbies and the Random list key on it) and its name (Deepwell). Everything else is new: it
was an offshore platform in a storm (docs/arenas/NEW-STAGES.md §4, `docs/arenas/scripts/rig_m1.py`: both stay as history).

**Pitch.** Deepwell Station, 3,800 m down: a research station on the ocean floor, in riveted steel, brass and thick glass. Glass
domes and tunnels look out on the deep: glowing jellyfish drifting, schools of fish wheeling, kelp swaying, a whale and her calf
passing the great windows, shafts of surface light, black smokers bubbling. At its heart the Well: a domed rotunda round the
drill shaft, whose ram pounds the wellhead over a glowing core. Warm lamps inside against the teal and cyan of the deep.

Original design: nothing here takes a name, layout, look or element from any other game.

## Layout (docs/arenas/scripts/rig_m2.py)

x east, z north, y up; point symmetric (x, z) → (−x, −z); team A west (wing A), team B east. 319 blocks (16 breakable supply
cases), 8 FFA spawns and two team tables of 4; the structure spans x ±28.4, z ±21.4 (56.8 × 42.8 m); bounds x ±29.4, z ±22.4,
ceiling 14, killY −4.4. 3v3 to 4v4 and FFA 8, as before.

| Deck | y | What |
|---|---|---|
| Moon-pool bays | −1.6 | North and south of the Well: a sunken bay each (16 × 10.6 m) round its **moon pool** (8 × 5.2 m of open water in the floor, no rail), the great window beyond it (16.8 × 8 m of glass), a ring tunnel's mouth west and east, eight steps up to the Well's door, the **survey sub** moored in the pool (an island: a 1.3 m jump from the south edge or a drop from the catwalk; half cover on the floor line). |
| Main deck | 0 | **The Well** (20 × 20 m): the drill floor round the **wellhead** (the piston's 4.4 m pad over the core), four pylons (full cover on both decks), consoles and cases under the gallery, a pressure door each way. **The wings** (10 × 18 m): each team's dormitory deck, lockers and an equipment rack screening the spine door. **The spine**: a 5 m glass tunnel from each wing to the Well. **The ring tunnels**: four 4 m glass tunnels, each from a wing's north or south door round to a bay's mouth, a bulkhead with a 2 m door across it, its last 3.2 m stepping down to the bay. |
| Gallery deck | 3.2 | **The gallery**: a 4.2 m ring round the open well under the dome, glass rails with a gap mid-side to drop through, a stair up each side of the Well (16 risers). **The lounges**: each wing's upper deck behind its panoramic window, a stair up its east wall. **The upper spine** between them. **The catwalks**: from the gallery's north and south doors out over the bays to end above the pools (a drop to the bay, the sub or the water). |

- **Routes:** the spine and the Well (fast, through the piston's pad); the gallery and the upper spine (over it, exposed to the
  whole dome); the four ring tunnels (the flanks, through the bays past the vents and the pools). Drops: the gallery's gaps,
  the catwalks, the lounges' stair wells.
- **Cover:** the pylons (7.2 m), racks, lockers and chambers (2.6 m, full); consoles, rails, cases and the subs (1.1-1.2 m, half);
  the ram itself while it stands on the wellhead (it closes the lines across the drill floor for 1.4 s every 16 s).
- **Spawns:** team A on wing A's dormitory deck behind its rack, team B on wing B's. FFA: both dormitories, both lounges, both
  bays (by the steps), a ring tunnel's inward run north-east and south-west. The draft checks every FFA and A-B pair blocked
  under ±1 m jitter at three eye heights (3D), spacing (FFA 9.4 m 3D, A-B 51 m), 3 m from every hazard volume on the spawn's
  floor, every spawn and plate reachable on foot, no tops in the 1.3-2.2 m band, stairs (risers ≤ 0.2 m, ≥ 1.6 m wide), point
  symmetry, and that the only cells with nothing under them beside a reachable floor are the two pools.
- **Falls:** only the moon pools. A pilot who steps, is shoved or drops into the water dies about 0.57 s later (killY −4.4, 2.8 m
  under the bays' floor); the vents beside each pool shove toward it.
- **Nav** (generator, 1 m grid; the M1 stage needed 0.5 m): 1719 nodes, 2798 links (258 gated by the hazards); every spawn and all
  three decks reachable from FFA 0; no node over the water (the subs are islands bots do not jump to). The record is 0.68 MB
  (the M1 record was 5.65 MB).

### No invisible walls

`tools/generate_stage_hazards.py` opens the stage like the four 2026-11-10 stages (`open_stage`; `EDGES['rig']`,
`OVERHANG['rig']`). Of the draft's 110 pilot columns 94 remain, none over anything a pilot may stand on or vault:

| Columns | Count | Why |
|---|---|---|
| Over the hull's steel walls, glass and the tunnels' bulkheads (`rig-top-wall-*`, `rig-top-glass-*`, `rig-top-bulkhead-*`) | 86 | They keep pilots inside the station: every one caps a full-height wall (3.2 m or more) whose roof above is glass ribs with no collider; a double jump would otherwise top a 3.2 m tunnel wall and walk out over the sea. A wall with another block on its whole top (the Well's steel under its glass) has none. |
| Over the four pylons (`rig-top-pylon-*`) | 4 | The drill's gantry sits on them (art, no collider). |
| Over the catwalks' rails (`rig-top-rail-catwalk-*`, bots only) | 4 | A lethal drop: bots vault whatever their obstacle ray hits; a player may jump or be knocked over them. |

Floors, stairs, rails, consoles, cases, racks, lockers, chambers and the subs have none (`DeepwellChecks.Columns`).

## Hazards (existing kinds; timings from the draft)

Both kinds time from the arm time (FightAt + 4 s). Nothing hurts in the fight's first 8 s.

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **The drill's piston** `rig-piston` | `press` | The wellhead at the Well's centre: a ram 4.4 m square and 4 m tall, parked 7 m up (clear over the gallery's eye lines) | Every 16 s: 9.6 s open, a 3.2 s telegraph, a 0.6 s stroke, 1.4 s held on the wellhead, a 1.2 s lift. First telegraph at FightAt + 13.6 s. | Lethal under the ram from the end of its stroke until it has lifted clear (the press kind's absolute band: its underside below 1.8 m). While held it is cover across the drill floor. |
| **The bubbling vents** `rig-vent-{n,s}-{w,e}` | `vent` + `wind` push | A floor vent each side of each moon pool, across the 3.2 × 4.2 m strip between the tunnel's mouth and the water, up to 3 m | Every 18 s: a 2.6 s telegraph, a 2 s blast. A bay's two vents alternate 9 s apart; a vent and its mirror share their clock. First telegraph at FightAt + 19.4 s (north-west and south-east) and + 28.4 s. | 2 HP per 0.25 s tick (16 HP a blast) and a push of 11 m/s² toward the pool (walk into it to hold). |

**Rules: none new.** The press is the Factory's; the vent's `wind` push is the one Starfall Causeway added on 2026-11-10
(`NativeArena.VentPush`, `NativeStageHazards.PushWind`), used here unchanged with its own strength (11 m/s²; the Causeway's is
12). One test pin moved with it: `CausewayChecks` asserted that no other map's vent is a wind; it now excepts Deepwell. For
Astra to confirm or tune: the push's strength against real input, and whether a minion should be pushed. The old stage's
burners (`rig-burner-*`) and blowdown vents are gone with it; the `burner` kind is still used by Ward Zero, the Emporium and the
Sanctum.

## The look (`rig-abyss`)

- **Light:** a pale cyan key from the surface far above (elevation 72°, soft shadows: the roofs' ribs fall across the decks), a
  dim teal rim, a deep teal trilight with a warm ground; linear fog 24-165 m in dark teal (the water closes the view: the whales
  come out of it and go back into it); no weather, no stars; the anime style on with a teal-and-amber grade (saturation 1.2,
  contrast 1.16); the advanced lighting (light volume, scatter, beams). Eight practical lights: the core under the wellhead
  (orange, flickering), the dome (cyan), both dormitories, both bays and both spines (warm lamp light). Far plane 420 m.
- **The sea (animated all match long, the same on every client: pure functions of the field clock, Core `NativeDeepwellLooks`):**
  a **whale and her calf** on a slow oval round the station (132 s a lap, so each great window sees them pass now and then),
  tails beating and flippers sculling; **22 jellyfish** hanging before the windows and in the four courts between the spine, the
  Well and the tunnels, each pulsing (a quick squeeze, a slow relax), rising on its pulse and glowing violet, cyan or rose with
  it; **three schools of 44 fish** wheeling, two before the bays' windows and one round the whole station; **30 stalks of kelp**
  in five groves, each four joints leaning on a swell that runs through the grove; a **survey drone** circling the dome with
  its lamp and beam ahead of it. Effects: ten shafts of surface light swaying down to the floor, marine snow, five black smokers
  with glowing throats and columns of bubbles, lamp buoys, the far habitats' lights and beacons.
- **The station (modelled; see "Stage art"):** riveted plate walls with ribs, brass bands, portholes and lamps; pressure-door
  frames with light bars; the glass as open frames (sills, mullions, transoms: the panes are glazed by the effects with a faint
  tint and reflections that slide as the eye moves, so the sea shows through); the Well's dome of ribs and hoops; the drill's
  housing and gantry; barrel roofs of ribs and purlins; pendant lamps; teak lounges; cream and sea-green panels.
- **The moon pools:** dark water with light coming up through it and glints drifting on it; it boils while a vent beside it works.
- **Hazard looks (never reduced by the quality or the low tier):** the piston: the core under the grate climbing from its resting
  glow, a ring round the wellhead beating faster (amber, then red), the pad's fill and edge, a curtain of light under the ram,
  the four klaxons on the pylons, the ram shuddering; the strike's flash, a shockwave across the deck and dust. A vent: its grate
  rattling, bubbles and mist gathering over it, chevrons marching toward the pool; then the grate thrown open, streaks driven
  across the strip, the water boiling, mist left hanging.
- **Audio** (existing clips with `deepwell.*` bank keys the owner's bank may fill): the piston's klaxon and its strike (with the
  structure landing cue), a vent's rattle and its blast.
- **Low tier:** the bank's Low tier; the shafts fall back to billboards; no caustics or sliding reflections; a third of the snow
  and bubbles; telegraphs unchanged.

## Code

- Draft: `docs/arenas/scripts/rig_m2.py` (layout, hazards, checks, plan PNG). Generator: `DRAFTS={'rig':'rig_m2'}`, the stage's
  `EDGES`/`OVERHANG` entries and the 1 m nav grid in `unity/ChipArenaConversion/tools/generate_stage_hazards.py`; `UnityMaps.json`
  and `tools/asset-lock.json` regenerated (only the `rig` record changed).
- Core: `NativeDeepwellLooks.cs` (ids, kit kinds, the hull's outline, the sea's creatures, the hazards' presentation poses, audio
  cues), `NativeDeepwellKit.cs` (the box kit and the rig), `NativeDeepwellFx.cs`, `NativeDeepwellArt.cs` (the art's slots and
  poses). Runtime: `NativeDeepwellPresentation.cs` (draw component, SourceWorld and prewarm hooks, audio).
- Data: `MapLooks.json` (`rig-abyss` in place of `rig-storm`); `PackDressing.json` (Deepwell's entry, which dressed the
  platform's crates, drums and tanks with the harbour kit, is gone).
- Off the shared M1 paths: `Core/NativeNewStageLooks` no longer lists `rig` (`Maps`, `Looks`); `Index` still returns the old stage
  numbers for the four that remain (0 Breakwater, 1 Pier 9, 2 Skyframe, 4 Polaris), so their kit, scenery, rig and effects are
  untouched; Deepwell's branches in `NativeNewStage{Kit,Scenery,Rig,Fx}` are unreachable and left for the integrator to drop.
- One line each in shared files: `NativeSourcePresentation.SourceWorld`, `NativeStagePrewarm.PrewarmStage`, `NativeAudio.Render`,
  `NativeArenaDressing.Render` (the glass blocks), `NativeStageArt.Banks`.
- Checks: `Tests/DeepwellChecks.cs` (`--deepwell`), `Tests/DeepwellArtChecks.cs` (`--deepwell-art`), both in the full run. Pins
  moved: `NewStagesChecks` (five maps; the burner's burst check went with the burners), `NewStageLookChecks` (Deepwell's weather,
  flare, columns, pack plan, rig and effects), `StageKitChecks` (the pack plan's counts), `AdvancedLightingChecks` (`rig-abyss`),
  `TransitionLoadingChecks` (the prewarm entry), `CausewayChecks` (the wind push).

## Stage art

The recipe is `docs/arenas/STAGE-ART.md`. With the stage bank `ChipArenaStageDeepwell` the station is drawn from models made in
Blender (`tools/stages/rig_art.py`, `rig_kit.py`, `rig_sea.py`, `rig_props.py`) in place of every map block's box, the box kit
and the box rig; without the bank (CI, a build without the Drive zip) the box kit and rig draw (Core `NativeDeepwellKit`).

**Modelled:**
- *Decks:* plate with joints on the world's grid, the lounges in teak; joists under the upper decks; the wellhead (warning
  stripes round the ram's pad, a bolted brass ring, the grate's bars over the core, quadrant marks); the pools' striped rims;
  the vents' brass frames and painted arrows.
- *Walls:* ribbed plate with rivet rows, skirtings, a brass band at hand height, painted panels (cream in the wings, sea-green
  in the bays), blind brass portholes, wall lamps on both storeys; pressure-door frames (rounded jambs and head proud of both
  faces, brass shoes, rivets, a light bar, a lit sign over the door; warning stripes on the tunnels' bulkheads).
- *Glass:* every glass block as an open frame: sill and head beams, T-section mullions with brass shoes every 2.4 m, brass
  transoms at each deck line, slim glazing bars. No pane is modelled (the stage shader is opaque): the effects glaze them.
- *The Well:* the ring beam on its walls, 24 meridian ribs and three hoops with brass bosses up to the drill's housing (a
  collared drum with a light ring under it), the gantry on the four pylons (eight-sided, brass banded, light strips), struts and
  hydraulic lines up to the housing.
- *Roofs:* barrel ribs and purlins over the tunnels, the spine, the wings and the bays; pendant lamps of brass with glowing
  bowls, flush lamps under the upper decks, pipe runs under the tunnels' eaves; a gantry crane over each pool.
- *Fittings (each on its block):* glass rails in brass posts and hand rails; stairs with round brass nosings; ridged consoles
  with a lit screen on each slope; equipment racks (panel doors, louvres, status lights, a cable tray); lockers; decompression
  chambers (a cream pressure vessel on saddles, brass bands, portholes, a wheel hatch, gas bottles); the survey subs (a round
  yellow hull, a deck to stand on, a glass nose, thrusters, lamps); the supply cases (breakable: one mesh for the sixteen).
- *The sea floor (rig_sea.py):* dunes out to 60 m (three tiers) and the plain to 170 m (one tier), rocks with coral and glowing
  anemones, the five smokers, the station's legs with footings and braces, two pipelines, twelve lamp buoys, rock spires on
  the horizon, the three far habitats.
- *The rig (Core NativeDeepwellArt, rig_props.py):* the ram (a collared piston with a striped shoe and lit slits) and its guide
  rods, the klaxons (their glow takes the telegraph's light), the core between the grate's bars, the vents' louvred grates;
  the whale's body (a pale belly), tail with flukes and flippers; a jellyfish's bell and arms (white glow, coloured by the
  pose); a fish; a kelp joint with blades; the survey drone.

**How it moves.** `NativeDeepwellRig` tags each box part it builds with the art's slot (beside the very `Parts.Add`).
`NativeDeepwellArt.Pose` places each tagged part's model at that part's own centre and turn, sized as `Sizing` says, so the
ram, the grates and the sea stand exactly where the box rig's do. No rules, timing, collider or network change.

**Materials** (`ChipArena/StageLit`, unchanged): calm finishes (the anime style's stepped light breaks a rough grain into
blotches): plate tiles on the decks, plain steel with worn edges, brass at half metal, wood grain on the lounges' teak; baked
dirt and occlusion (eased); emission above 1 on lamps, light strips, screens, the klaxons, the core and the creatures.

**Low graphics tier:** the bank's Low tier (the same art, about a fifth of the triangles), not the box kit.

Rebuild (from `unity/ChipArenaConversion`; about 30 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout rig LAYOUT.json
blender --background --factory-startup --python tools/stages/rig_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage and the rig's props in Blender; `--tiers`, `--no-ao`,
`--no-sea`, `--static-only`, `--stats` for quick looks.)

**What still draws as boxes with the bank:** a destroyed supply case's stub and fragments (the dressing's, as on every map).
The glass panes, the pools' water and every telegraph are effect quads (Core `NativeDeepwellFx`). Without the bank: everything.

**The bank** (2026-11-11): 18,816,151 bytes (budget: about 20 MB), 751 props. Static: 113 map chunks (16 m cells) in three tiers
and 3 far chunks in one; Fine 214,492 triangles (190,445 vertices), Mid 125,382 (113,334), Low with the far sea 39,348 (61,564).
Rig: 14 props; 4,370 / 2,576 / 1,274 triangles by tier; 317 rig draws a frame (the pose buffer holds 512; the fish and the kelp go
out instanced). The supply case: 1,036 / 844 / 204 triangles, one mesh for the sixteen.

**Z-fighting** (`zfight.py BANK UnityMaps.json rig --tier N`): 0.05 m2 at Fine (ten pairs of a few square centimetres: pipe
brackets against their pipes), 0.01 m2 at Mid, 0.00 m2 at Low; no block draws as a box beside the art. Every tier's static scene
goes through `stagekit.lift_coplanar` (it settles about 200 m2 of inlays, panels and trims); beams end open inside what they
meet. The box kit lays no two faces of one facing in one plane (`DeepwellChecks`: `SurfaceChecks.Clashes` over the kit and the
blocks it leaves drawn, 0 clashes).

**Performance** (shared Editor, High tier, one idle bot, 10 s; other agents shared the machine): the old stage (the platform from
its north-east corner) 1,248 frames, mean 8.02 ms, p95 9.05 ms; the new stage with the art (from the gallery across the Well)
1,276 frames, mean 7.84 ms, p95 9.00 ms; the same view with the box kit (no bank) 1,285 frames, 7.79 / 8.90 ms. Low graphics
tier, the art: 1,146 frames, 8.74 / 10.22 ms (taken while other captures queued; not slower by design). CoreTests: the rig and
effects build in 0.12 ms a frame, allocation free; the box kit is 2,033 parts, the rig up to 391 parts, the effects up to 715
quads (low tier 445).

## Seen in the Editor

A match in the shared Editor (one idle bot, god mode, the game's own camera placed by `maincam`), on `54a5e872`:
- **Before** (the platform, `f81e663e`): six views.
- **After, the art:** from high outside (the whole station on its legs, the dome, the whale overhead, jellyfish, kelp, the far
  habitats); the drill floor and the wellhead; up the pylons into the dome; the gallery; a dormitory and its lockers; a lounge
  (teak, the panoramic window, a jellyfish outside); the spine toward the Well; a ring tunnel (rib shadows across the deck, a
  school of fish over the roof); a bay's great window; the catwalk; the station from the seabed; a bay with its pool, chamber
  and sub; a pressure door; a console; under the hull.
- **The hazards:** the piston's telegraph (the curtain turning from amber to red, the klaxons, the core flaring), the ram on the
  wellhead with its shockwave, its lift; a vent's telegraph (the grate's glow, mist and bubbles), its blast (the grate thrown
  open, streaks toward the pool, the water boiling), the mist after.
- **The Low graphics tier** (five views) and **the box kit without the bank** (the same five: the fallback draws whole).
- Editor build checks (`PrototypeBuild.Validate`, `wtvalidate.sh`): ok on `54a5e872`, including "native map rig: isolated
  Edit-mode physics, 8 spawn floors, capsule settling".

## Music

The stage keeps the owner's **Offshore Rig** (`rig-offshore`, `NativeStageMusic.Theme`) as its placeholder; the record's `music`
fallback stays `factory-anvil-loop`. The song for the new place takes the key **`rig-theme`**; its Suno brief (style and lyric
fields) is in `docs/arenas/STAGE-MUSIC-PROMPTS.md` ("Deepwell — The Station Below"). To put it in: the MP3 at
`Assets/ChipArena/Resources/Audio/music/rig-theme.mp3` with a streaming `.meta`, an asset-lock entry, the length pin
(`Editor/NativeBuildChecks`, `Tests/ZoneMusicChecks`) and `case "rig":return "rig-theme";` in `NativeStageMusic.Theme`.

## Not done / not seen

- Played only by an idle bot under scripted cameras: no human playtest, no network (guest) test, no native Windows build, no
  killcam run on the art. The vents' push against real input and the jump to a pool's sub are unverified in play.
- The whale was seen only from outside and above (a silhouette in the fog); it was not caught passing a window from inside.
- No pane of glass is modelled: the glass is frames plus the effects' tint and reflections, so a pane has no highlight from
  the lamps and no refraction; the roofs between the ribs are open to the same water.
- The decks under the gallery and the upper decks are lit by the practical lights and the lamps' glow only (the key is above
  them): darker than the rooms under glass.
- The fine tier's chamber and sub read faceted close to; the consoles are simple ridged desks; the bays' sea-green panels are
  flat colour.
- No ambience loop (`rig-deep` is a key only); the hazards' sounds are existing clips under `deepwell.*` keys.
- `NativeNewStage{Kit,Scenery,Rig,Fx}` still carry Deepwell's old branches (unreachable); `ValidateM1Runtime`'s list in
  `Editor/NativeBuildChecks` still names `rig` (it only checks cranes there: harmless). Left for the integrator.
- The main menu's 3D preview draws no Deepwell dressing (as for the other new stages); the killfeed has no hazard names (as on
  every map).
