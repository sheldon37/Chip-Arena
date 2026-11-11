# Skyframe (`skyframe`), rebuilt

2026-11-11 · Claude · branch `claude/rework-skyframe` (base `claude/sleepy-ritchie-g8ruhv` at `f81e663e`) · one of the five
2026-10-31 stages rebuilt from scratch (owner, 2026-11-11: "COMPLETELY rework ... Polaris Station, Deepwell, Skyframe, Pier 9,
and Breakwater. These all look kinda thrown together and all factory like. Maybe give them all more of a unique feel, very good
designs, and nice animations"; the theme approved the same day: "Yes, start them").

Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The stage bank is
local (git-ignored); the integrator packs it into the builder's Drive zip.

**Pitch.** A sky garden at the top of a tower that is still going up, above a sea of clouds at sunset. Terraces of pale stone,
glass and steel with lawns, trees, clipped hedges, living walls and hanging gardens down the tower's glass; the unfinished
floors' red steel frame rising over the lift core; the climbing crane's mast through it and its long jib swinging a bundle of
beams round the tower all match; wind turbines turning at the corners, airships drifting past, birds wheeling round the frame,
the clouds sliding by below. Low glass balustrades you can vault and open edges where a fall kills in half a second.

Original design: nothing here takes a name, layout, look or element from any other game. The old stage (floor 31 of a concrete
construction site at noon, dressed with the harbour kit's containers; `docs/arenas/NEW-STAGES.md` 3, `skyframe_m1.py`) is
history: the map id, the name, the crane hazard's kind and the random rotation are what it keeps.

## Layout (docs/arenas/scripts/skyframe_m2.py)

x east, z north, y up; point symmetric (x, z) → (−x, −z); team A west, team B east. 274 blocks (6 breakable), 8 FFA spawns and
two team tables of 4; the structure x ±26, z ±22; bounds x ±30, z ±26 (4 m of air past every edge), ceiling 24, killY −6 (a
body is dead about half a second after it leaves the garden).

| Level | y | What |
|---|---|---|
| Cloud terraces | −3.2 | Two terraces hung outside the garden's north and south edges (24 × 6 m), under the garden's lip and out of its sight: the flank routes. A stair of slabs over the clouds at each end up to the garden. |
| Garden | 0 | The terrace itself, 52 × 32 m round the **lift core** (10 × 10 m, to 12 m: the west base never sees the east). A lift lobby is cut into the core's north face and another into its south. A lawn with a tree in each drop-zone quarter, a reflecting pool before each base, the north and south lanes between the gallery and the edge. Two **pavilions** (garden rooms, 12 × 9.4 m, two doors each) in the south-west and north-east corners. |
| Gallery and roofs | 3.6 | The **gallery**: a 4 m ring round the core on eight steel columns (the frame goes on up from them). The pavilions' **roof terraces**. A **glass bridge** from each roof to the gallery. |

- **Routes:** each base to the gallery by its own stair (north-west / south-east) or by its pavilion's roof stair and the glass
  bridge; round the core by the north or the south lane, or above them on the gallery; the cloud terraces under the lanes.
  Every balustrade is 1.1 m and vaults; the gallery and the roofs drop to the garden through their gaps.
- **Cover:** the core and its lobbies; the columns (full, thin); living walls (full, 2.8 m) before each base's flank, across
  each forecourt, before each lobby and before each pavilion's door; clipped hedges and stone planters (half, 1.0-1.1 m); a
  bundle of beams (half); pallets of paving and crates of plants (breakable); the tree trunks; the landed bundle of beams
  itself, for six seconds at a time.
- **Spawns:** team A before its hedge at the west edge, team B at the east. FFA: both bases, both cloud terraces, both
  pavilions, both lift lobbies. The draft checks every FFA and A-B pair blocked under ±1 m jitter at three eye heights (3D),
  spacing (FFA 9.1 m, A-B 45 m), 3 m from every hazard volume on the spawn's floor, every spawn and floor reachable on foot,
  no tops in the 1.3-2.2 m band, stairs (risers ≤ 0.2 m, ≥ 1.6 m wide), the crane's zones and its way round the tower clear.
- **Nav** (generator): 2171 nodes, 3480 links (382 gated by the hazards); every spawn and all three levels reachable.

### No invisible walls

The generator's `open_stage` (as on the four 2026-11-10 stages): of the draft's 60 pilot columns, 36 stay.
- **8 for everyone**: one over each tree (`skyframe-tree-*`, `OVERHANG`): the crown hangs over the trunk's top, so nothing
  stands there.
- **28 for bots only** (`bots: true`): the balustrades at a lethal drop (the garden's west and east edges and its corners, the
  roofs' outer edges, the cloud terraces' outer edges and ends). A player vaults them or is knocked over them and falls.
- Every other balustrade (the gallery's, the bridges', the roofs' inner edges, the garden's over the cloud terraces) has none.
`EDGES` is empty for Skyframe: no wall caps the map.

## Hazards (existing kinds; timings from the draft)

The Factory kinds time from the arm time (FightAt + 4 s). Nothing is live in the fight's first 4 s.

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **The beam drop** `skyframe-crane` | `crane` | Four zones round the tower, in turn: the north-west lawn (−15, 0, 11), the east pavilion's roof (20, 3.6, 11), the south-east lawn (15, 0, −11), the west pavilion's roof (−20, 3.6, −11). Odd rounds start one zone on. | Every 20 s from arm + 6: 6 s travel (the jib slews, the trolley runs), a 3 s telegraph over the zone, a 0.95 s fall from 11 m, 6 s landed, 1.5 s regrip, 2 s hoist. First drop at FightAt + 19 s. | Lethal in the zone (6.6 × 1.8 m, 2.2 m high) through the fall's end and the impact; then the bundle (6 × 0.9 × 1.2 m) is cover for six seconds. |
| **The crosswind** `skyframe-wind-{n,s}` | `vent` + push (`pushMode: wind`) | The middle 8 m of each cloud terrace (x −11..−3 north, 3..11 south), its whole depth, 3 m high | Period 20 s, offsets 2 / 12 s (they alternate); 2.6 s telegraph, a 2 s gust. First telegraph at FightAt + 21.4 s (north), + 31.4 s (south). | 2 HP per 0.25 s tick (16 HP a gust) and a push of 12 m/s² straight off the terrace, toward the 4 m gap in its outer balustrade. Walking into it holds you. |
| **Open edges** | bounds / killY | Every edge: the gaps in the garden's balustrades, the roofs' gaps, the cloud terraces' gaps and their stairs' open sides | always | A fall is dead at y −6. |

### Rules notes for Astra

- **No rules or networking code changed.** The crane is the Factory's `crane` kind as before (zones on two floor levels, as the
  old Skyframe had); the crosswind is the Causeway's `vent` with `push` and `pushMode: "wind"` (Runtime `NativeArena.VentPush`,
  unchanged). Skyframe is now the second map with a wind vent: `Tests/CausewayChecks.cs`' pin "no other map's vent is a wind"
  now excepts Skyframe.
- The crane's `a`/`b` only place the park (their midpoint, over the north lane) and carry the jib's height (y 38) for the
  presentation; the simulation moves the hook in straight hops between zones, the presentation slews the jib and runs the
  trolley on the same easing (the zones ring the tower, so the carried bundle never crosses the frame).
- The pilot columns: see "No invisible walls" above. For Astra to confirm or tune: the gust's strength on a 6 m ledge (the same
  numbers as the Causeway's), and whether bots should avoid the roofs' drop zones' landed bundle.

## The look (`skyframe-golden-hour`)

- **Light:** the sun low in the south-south-west (bearing 205°, elevation 18°: off the teams' axis, so neither base looks into
  it), warm gold; a blue-violet rim from the north-east; a trilight ambient of violet sky, peach horizon and warm ground
  bounce; linear fog 120-2300 m in peach; long soft shadows (4 cascades, 95 m); the anime style on (saturation 1.18, contrast
  1.12, violet shadows, gold lights, a toon sky with banded clouds); the advanced lighting (light volume, scatter pooled at the
  cloud level, beams; no weather); six practical lights (the two lift lobbies, the two pavilions, the arcade under the gallery
  west and east); far plane 2400 m.
- **Materials:** pale limestone paving (flagstones), poured concrete on the core, red-oxide primer on the frame, dark bronze
  steel, glass (opaque, polished: it takes the stage art's reflection probe, which Skyframe gives a sunset sky), timber
  battens, stone troughs, four greens and three flower colours.
- **Set pieces that move all match** (Core `NativeSkyframeRig`, the same on every client and in the killcam, from the match
  clock): the **crane** (its top slewing, the trolley running, the ropes paying out, the hook and its spreader, the bundle
  carried, dropped, landed and hoisted); the **trees' crowns** swaying, harder as a gust rises; the **windsocks** on the cloud
  terraces filling through the wind's telegraph; four **wind turbines** turning at the tower's corners; three **airships** on
  slow circuits round the tower; eighteen **birds** wheeling round the frame; thirty-four **cloud islands** drifting with the
  wind below, north and south of the tower.
- **Effects** (Core `NativeSkyframeFx`): the low sun's glare and streak, light pooled on the clouds under it, glowing haze
  drifting over the sea; gold dust and petals on the wind round the eye (faster in a gust); lantern halos; glints running
  along the balustrades as the eye crosses the sun's mirrored ray; red beacons on the crane, the turbines and the airships.
- **Hazard looks (never reduced by the quality or the low tier):** the next drop zone waking in amber as the load swings
  over; through the telegraph its frame and corner posts beating faster and turning red, the hatch filling, the bundle's
  shadow closing in and a warning shaft down from it; the fall's streaks; the impact's flash, ring, dust and sparks; the landed
  bundle's steady red frame. The crosswind's chevrons running off the terrace, wisps gathering, the lit lip of the gap; then
  the gust's streaks driven across the section and its bright front. The windsock is the wind's second telegraph.
- **Audio** (existing clips with `skyframe.*` bank keys the owner's bank may fill): the crane setting off, its horn twice, the
  release, the impact (with the structure landing cue), the hoist; the wind rising and its gust.
- **Without the stage bank** (CI, a build without the Drive zip): the box kit (Core `NativeSkyframeKit`: joints, lawns, pools,
  painted zones, the glazed fascia and its vines, lift doors, handrails, battens, soil and shrubs, the frame overhead as boxes),
  the box rig (the same movers as boxes) and the box-built backdrop (Core `NativeSkyframeScenery`: the tower's body, the
  turbines' masts, the other towers, the cloud sea) draw, lit by the same look.
- **Low tier:** the bank's Low tier; the effects' atmosphere counts drop to a third; telegraphs are unchanged.

## Stage art

The recipe is `docs/arenas/STAGE-ART.md`. With the bank `ChipArenaStageSkyframe` every block, kit part, rig part and backdrop
sector is drawn from models made in Blender; the gameplay geometry is the map record's and no rule reads the art.

**Modelled** (`tools/stages/skyframe_*.py`):
- *The garden*: limestone paving with a darker border course and bands round the core, the two lawns in stone kerbs (mown in
  stripes), the two reflecting pools, the painted drop zones (frame and hatch), lights set flush in the paving along the
  balustrades and round the arcade; its edge as a stone coping over a steel channel and the top storey's glazing, with the
  **hanging gardens**: stone troughs under the lip in every other bay, leaves over their edges and vines and flowers down the
  glass.
- *The lift core*: poured concrete with formwork joints; in each lobby a steel door frame, two stainless leaves, a call panel
  and a floor indicator lit aqua, a ceiling light and a lit head over the mouth.
- *The gallery and bridges*: paved decks on steel edge beams with joists and soffit lights; glass-floored bridges between
  I-beams; **H-section columns** in red primer with base plates, bolts and collars.
- *Balustrades*: a steel shoe, glass panes with gaps between them, slim stainless posts, a round handrail.
- *Stairs*: stone flights with rounded nosings; the cloud terraces' stairs as stone treads on two steel stringers over the
  clouds.
- *The pavilions*: stone walls on a darker base course, timber battens on both faces, a glazed clerestory band with steel
  glazing bars, a stone cornice; steel door heads with a light under each and their jambs; roofs paved, with a strip of lawn, a
  coping and the room's ceiling lights; a timber planter bar inside.
- *Planting*: stone troughs with a rounded rim, soil and low planting (shrubs and flowers, never over a hand above the rim);
  tapering trunks with a root flare; hedges as swelling clipped masses shaded round, their greens in drifts, on stone
  troughs; living walls in steel frames, planted on both faces with flowers among the leaves.
- *The site*: a strapped bundle of six I-beams on timber dunnage; (breakable, in their own frames) a pallet of paving slabs
  on a timber pallet, strapped, and a slatted crate of plants.
- *The cloud terraces*: paved slabs on steel knee braces back to the tower, the crosswind's painted chevrons and warning strip
  at the gap, the windsocks' poles off the outer edge.
- *Overhead*: the **unfinished frame** (the eight columns going on up three to five storeys, ring and radial I-beams thinning
  out storey by storey, corrugated decking on some bays of the first two floors with a tarped stack on a few, debris netting
  on some upper bays); the core's concrete going on up 6 m to its pour line with starter bars and a climbing formwork deck
  and rail; the **crane's mast** in four chords, laced, up to its slewing ring at 37 m.
- *The rig* (`Core/NativeSkyframeArt.cs`, 19 slots): the crane's top (turntable, machinery deck, cab with a wrapped
  windscreen, the triangular laced jib, the counter-jib with its deck, rail, winch, machinery house and four counterweights,
  the cat head and its pendants, three beacons), the trolley, a unit rope (hoist ropes and slings), the hook block with its
  sheaves and striped spreader beam, the bundle; three tree crowns (boughs and masses of leaves, some gold where they catch the
  sun); the windsock in orange and white bands; the turbine rotor (a spinner and three tapered, twisted blades with red tips);
  two airships (a cream and orange one with a timber gondola, a white and blue one with a dorsal sail; lit gondola windows,
  engine pods, fins); a bird and its two wings; four cloud islands.
- *The sky* (far chunks, one tier): the **tower's body** going down into the clouds (glass between stone floor bands, lit
  offices, two planted setbacks with trees), the turbines' outriggers, masts and nacelles, sixteen **other towers** standing
  out of the sea (some with roof gardens, some still going up with their own cranes, all with a beacon), the **cloud sea** (a
  rolling sheet out to 1.7 km shaded by its slope to the sun, sixty-odd heaped islands standing on it) and twenty great
  **banks** on the horizon.

**How it moves.** `NativeSkyframeRig` tags each box part it builds with the art's slot (`Slot`, set beside the very
`Parts.Add`); `NativeSkyframeArt.Pose` places each tagged part's model at that part's own centre and turn (a rope and a cloud
scaled by the part's half extents, an airship by its half width). The bundle stands exactly where the simulation's state puts
its collider.

**The reflection probe** (a small general addition to `Runtime/NativeStageArt.cs`): a stage may now set `ProbeAt` and
`ProbeSky` before `Prepare`. Skyframe stands its probe over the north lane (the default, the centre of the art's bounds, is
inside the lift core) and gives it a sunset sky colour instead of near black, so the glass and the steel catch the sun.

**The play-space rule** (`--skyframe-art`): within 2 m over a floor the Fine art keeps within .25 m of a block. The core's
and the columns' tops (12 m: no jump reaches them; the frame goes on up from them) are not floors.

Rebuild (from `unity/ChipArenaConversion`; about 25 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout skyframe LAYOUT.json
blender --background --factory-startup --python tools/stages/skyframe_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage, the sky and the rig's props in Blender; `--tiers`,
`--no-ao`, `--no-sky`, `--static-only` for quick looks.)

**The bank** (2026-11-11): 11,096,951 bytes (budget: about 20 MB), 484 props. Static: 44 map chunks in three tiers and 5
far chunks in one; Fine 73,828 triangles (80,782 vertices), Mid 34,518 (47,794), Low 7,676 for the map and about 85,000 for the
far chunks (the cloud sea and its islands are most of them). Rig: 19 slots; the seven near parts 5,304 / 1,996 / 832 triangles
by tier, the twelve far parts 5,844 in one model each; up to 113 rig draws a frame. Breakables: 2 models for 6 blocks, 1,646
/ 1,250 / 44.

**Z-fighting** (`zfight.py` on the bank; coplanar same-facing overlap): Fine 0.03 m2, Mid 0.02 m2, Low 0.01 m2 (far chunks
included). The stage's inlays (kerb, lawn, stripes, pools, paint, lights) are stacked in layers of their own
(`skyframe_kit.rect(layer=)`, a layer the tier's `LIFT_TIER`), so their order is the script's and not the faces' areas';
`stagekit.lift_coplanar` settles what is left, and the far chunks at their range (2 cm apart to 5 cm).

**Numbers** (CoreTests, Release, this PC): the box kit 2,180 parts (119 overhead); the rig up to 331 parts a frame; effects up
to 384 quads a frame (low tier 226); the box backdrop 17,620 vertices in three meshes; rig and effects 0.16 ms a frame,
allocation free. Map record: 274 blocks (6 breakable), 36 pilot columns (28 bots only), nav 2,171 nodes.

**Performance** (shared Editor, High tier, 10 s, one idle bot; other agents shared the machine, so the direction is clearer
than the figures): the old stage (the M1 box presentation, `f81e663e`, from the west slab toward the crane well) 838 frames,
mean 11.94 ms, p95 18.09 ms; the rebuilt stage on its art (from the west base toward the core) 1,289 frames, mean 7.77 ms,
p95 8.97 ms.

**Seen in the shared Editor** (a match, one idle bot, god mode, the game's camera placed by `maincam`): the old stage in six
views (before); the rebuilt stage in ten: wide from the west, the west base toward the core, the north lane, the gallery, the
west roof toward the crane, the north cloud terrace, up at the frame and the crane, the sunset from the east roof, wide from
the north-east, north over the clouds; the same ten on the Low graphics tier. The crosswind on the north cloud terrace through
its telegraph and gust (the windsock filling and standing out, the chevrons running off the edge, the gust's streaks and its
front). The beam drop from above the north edge: the bundle carried over a pavilion roof, the zone's frame waking amber and turning red, the fall, the impact's ring and the landed bundle. The ten views were taken one bank revision before the last (the hedges' and living walls' greens were calmed after them; that bank is the one installed and checked). Not seen: the bank-absent box kit, rig and backdrop in the Editor (the shared Editor's queue starved the run; they are checked by CoreTests and by source).

**Checks**: `--skyframe` (57,665 checks) and `--skyframe-art` (817,956; with the bank on the machine: every block covered,
every breakable and rig slot modelled, the chunks' tiers, the budget, the play-space rule with 0 of 51,696 vertices over
.25 m from a block, nothing in a spawn); the focused suites that pin Skyframe (`--new-stages`, `--new-stage-looks`,
`--stage-kits`, `--advanced-lighting`, `--map-style`, `--transitions`, `--surfaces`, `--zone-music`, `--causeway`,
`--stage-art`, `--dev-tools`); the full suite (1,002 PASS lines, exit 0, at `6cc3c458`'s code: the later commits changed only
the Blender scripts and docs); the Unity compile check (both assemblies); the pin scanner (0 broken). The Editor's build checks (`PrototypeBuild.Validate` through `wtvalidate.sh`) pass on `bbdd6c43`, including "native map skyframe: 8 spawn floors, capsule settling" and "M1 runtime skyframe" (the landed bundle's collider at each zone's height).

## Music

The stage keeps its current track, the owner's **`skyframe-rising-motif`** (`NativeStageMusic.Theme`), as the placeholder. A
song written for the rebuilt stage takes the key **`skyframe-theme`**; its Suno prompt (style and lyric fields) is in
`docs/arenas/STAGE-MUSIC-PROMPTS.md` "The 2026-11-11 rebuilds". The map record's `music` stays the generator's fallback.

**In (2026-11-11):** the owner's **Garden Above the Clouds** (237.200 s) plays as `skyframe-theme`; Rising Motif stays
packaged.

## Files

- Draft: `docs/arenas/scripts/skyframe_m2.py` (layout, hazards, checks, plan PNG; `skyframe_m1.py` stays as history).
  Generator: `tools/generate_stage_hazards.py` reads the M2 draft (`REBUILT`), `EDGES`/`OVERHANG` take Skyframe's entries;
  `UnityMaps.json` and `tools/asset-lock.json` regenerated (only Skyframe's record changed).
- Core: `NativeSkyframeLooks.cs` (ids, kit kinds, the shared places, the crane's and the wind's presentation moments, audio
  cues), `NativeSkyframeKit.cs` (the box kit and the rig), `NativeSkyframeScenery.cs`, `NativeSkyframeFx.cs`,
  `NativeSkyframeArt.cs`. Runtime: `NativeSkyframePresentation.cs` (draw component, SourceWorld and prewarm hooks, audio).
- Art: `tools/stages/skyframe_art.py` (the stage on its blocks, the frame, the bank), `skyframe_props.py` (breakables, rig),
  `skyframe_sky.py` (far chunks), `skyframe_kit.py` (finishes and shapes).
- Shared files touched (small): `Core/NativeNewStageLooks.cs` (Skyframe leaves `Maps` and `Looks`; `Index` keeps the other
  four stages' numbers through a private `Stages` list), `Core/NativeStageArt.cs` (the bank's line),
  `Runtime/NativeStageArt.cs` (`ProbeAt`, `ProbeSky`), one line each in `Runtime/NativeSourcePresentation.cs`,
  `NativeStagePrewarm.cs`, `NativeAudio.cs`; `MapLooks.json` (the `skyframe` entry is now `skyframe-golden-hour`);
  `PackDressing.json` (Skyframe's entry and the kit only it used, `brick-piles-2x1`, removed: the harbour kit no longer
  dresses it).
- Tests: `Tests/SkyframeChecks.cs` (`--skyframe`), `Tests/SkyframeArtChecks.cs` (`--skyframe-art`), both in the full run;
  one line each in `Program.cs` and `StageArtLayout.cs`. Pins changed in shared tests: `AdvancedLightingChecks` (the look's
  name), `NewStageLookChecks` and `StageKitChecks` (Skyframe's M1 cases and its 34 dressed blocks removed),
  `NewStagesChecks` (the crane is one of three hazards now), `TransitionLoadingChecks` (the prewarm entry), `CausewayChecks`
  (the wind pin excepts Skyframe).

## Not done / not seen

- Played only by one idle bot under scripted cameras: no human playtest, no bot match judged for flow, no network (guest) test,
  no native Windows build, no killcam run on the art.
- The gust's strength on a 6 m ledge and the bots' handling of the landed bundle are untuned (the Causeway's numbers).
- The box kit, rig and backdrop (the bank-absent look) were not seen in the Editor and are plain by design; they exist so CI and a build without the Drive zip
  still draw a readable stage.
- The glass is opaque (the stage shader has no transparency): it reads as glass by its polish and the probe's sunset sky.
- The Low graphics tier's hedges and living walls are plain green masses and its paving shows the shader's speckle more.
- No ambience loop (`skyframe-wind` is a key only); hazard sounds are the existing clips; no song for the new stage yet.
- No shadows from the practical lights (as STAGE-ART.md says for every stage). The jib's own shadow falls outside the shadow
  distance at this sun height; the frame's, the trees' and the carried bundle's fall across the garden.
- Frame time on the Low tier was not measured; the perf views before and after are not the same place (the map is new).
