# Starfall Causeway (`causeway`)

2026-11-10 · Claude · branch `claude/stage-causeway` (base `claude/sleepy-ritchie-g8ruhv` at `e6d564dc`) · one of the four new stages
(owner request: "4 newer levels ... set a new bar for quality ... unique hazards ... VERY beautiful ... in the random selection").

**Pitch.** An ancient causeway of pale carved stone and star-metal hangs in deep space between two broken towers. Below it there
is nothing but the void: nebulae, a slow river of stars far beneath, drifting asteroids, the lost spans of the causeway floating
away, and a ringed gas giant low in the south-east. Cold cosmic light, warm rune glow. The stars fall here; a comet skims the
bridge; the solar wind blows across it.

Original design: the owner's reference was "a bridge in space"; nothing here takes a name, layout, look or element from any
other game.

## Layout (docs/arenas/scripts/causeway_m1.py)

x east, z north, y up; point symmetric (x, z) → (−x, −z); team A west (Tower A), team B east. 295 blocks (all static), 8 FFA
spawns and two team tables of 4; bounds x ±42, z ±14 (4 m of void past every edge), ceiling 24, killY −6 (−10 until the owner's
"On the bridge, we can let people get knocked off the map. Its a instant death when falling off", 2026-11-10: a fall kills about
half a second after it leaves the span or the walkways, credited to whoever hit them last). Every pilot column at an edge (the
rails, parapets and crown edges) stops bots only, so a player can jump over a rail or be knocked over it; the spires and gables
keep theirs under the floating crown stones.

| Level | y | What |
|---|---|---|
| Walkways | −3.2 | Two lower walkways (z ±6.8..9.8, x ±15.2) alongside the span, the tops of deep stone beams; inner rail whole, outer rail broken (falls). A floating stair (16 risers) at each end up to the plaza wings. |
| Span | 0 | The main span x ±20 (10 m wide) on its 7 m keel, broken at **the Breach** (x ±3) with the floating **keystone** (2.2 × 2.8 m) at its heart: two 1.9 m jumps, or a fall to the void. Four balconies (x ±9.4..16, out to z ±7.2), balustrades broken toward the Breach. The **plazas** (bridgeheads, x ±20..28) and the towers' **gate halls** (x ±28..38). |
| Crowns | 4.4 | Each tower's roof terrace, overhanging 1.8 m north and south, with the east gable and its window (sill 1.0 m, the sniper slit toward the span), the broken spire, broken parapets (falls). |

- **Routes:** the span (fast, exposed, the jump), the two walkways (flank, sheltered from the span, no jump), the crowns (power
  positions over the plazas). Each tower: the hall's inner stair (22 risers, from the north door) and the outer stair up its north
  face from the plaza (tower B: south). The **lantern gates** close the plaza wings at the walkway stairs' heads (2.2 m doors).
- **Cover:** star-metal pylons (full, 4.8 m) framing the balconies; the wardens (5.4 m) at each span entrance and the watchers
  (5.0 m) at the Breach; fallen column drums and blocks (half, 1.0-1.1 m); crystal braziers (half, 1.0 m); the gate halls' star
  altars (floor to vault) screen the arches.
- **Spawns:** team A in hall A (behind its altar), team B in hall B. FFA: both halls, both walkways by the Breach, the south wing
  of plaza A / north wing of plaza B, both crowns behind the gables. The draft checks every FFA and A-B pair blocked under ±1 m
  jitter at three eye heights (3D), spacing (FFA 9.6 m 3D, A-B 69 m), 3 m from every hazard volume on the spawn's floor, every
  spawn and plate reachable on foot, no tops in the 1.3-2.2 m band, stairs (risers ≤ 0.2 m, ≥ 1.6 m wide).
- **Nav** (generator): 1203 nodes, 1922 links (734 gated by the hazards); every spawn and all three levels reachable from FFA 0.
  Bots cannot jump the Breach; they cross by the walkways.

## Hazards (existing kinds; timings from the draft)

The Factory kinds time from the arm time (FightAt + 4 s); the maglev from FightAt. Nothing is live in the fight's first 4 s.

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **Falling stars** `causeway-star-{0,1,2}{a,b}` | `press` (re-rigged) | Six points in three point-symmetric pairs: the span by the Breach (±7, ∓2.4), the span's ends (∓19, ±2.6), the plazas before the tower arches (∓24.2, ∓0.6) | Period 20 s each, offsets 0, 3.3 ... 16.7 s: a strike every 3.3 s somewhere. 3.2 s telegraph, 0.55 s fall, 0.5 s blast, 0.35 s lift. First telegraph at FightAt + 19.4 s. | Lethal in a 2.6 m square column (the ring is drawn at its corners plus the capsule, r 2.26 m) from the end of the fall through the blast. The press kind's lethal band is absolute (ram underside below 1.8 m), so every strike lands on a y-0 floor. |
| **The comet pass** `causeway-comet` | `maglev` | The span's centre lane, x −20..20, z ±1.6, 0..2.6 m | Every 36 s from FightAt + 18; 7 s warning, 3.5 s danger, the pass at 38 m/s with a 16 m body (1.47 s), 2 s fading. Direction alternates each pass and round. | Lethal; bots wait (the maglev nav gate). |
| **The solar wind** `causeway-wind-{w,e}` | `vent` + push | The balcony section of each half, x −16.5..−8.5 (and its mirror), z ±10, y −3.2..3.4: the span, both balconies and the walkways under them | Period 20 s, offsets 2 / 12 s (they alternate); 2.6 s telegraph, 2 s gust. First telegraph at FightAt + 21.4 s. | 2 HP per 0.25 s tick (16 HP per gust) and a push of 12 m/s² along its axis (west half north, east half south) toward the balconies' broken balustrades and the walkways' broken rails. Walking into it holds you (the knockback's counter-steer). |

### Rules change for Astra (the only one)

`vent` hazards may now carry `push` (m/s²) along `axis`: `NativeArena.VentPush` (Runtime/NativeCausewayPresentation.cs), called
from the vent branch of `StepAuthoredHazards` (Runtime/NativeStageHazardsRuntime.cs, one call), adds `axis · push · level / 60` to
the `Knockback` of every live, connected, unlocked pilot touching the column each host tick through the burst (scaled by the
pilot's `knockbackTaken`), as the black-hole and repulsion fields do. Every vent on every other map has `push` 0 (CausewayChecks
pins it), so nothing else changes. Not sent: the push is host knockback like any other; a guest's predicted movement does not know
it and is corrected by the host's frames (as with fields and blasts). Bots: the vents carry damage, so the nav gate keeps them off
the section through the warning and the gust (but a bot already inside is pushed). For Astra to confirm or tune: the strength
(12 m/s² gives about 3.9 m/s at steady state, about 5 m of drift over a 2 s gust unresisted), whether minions should be pushed,
and whether prediction should apply it.

## The look (`causeway-starfall`)

- **Light:** a cold blue-white key from the distant sun (bearing 313°, elevation 31°), a violet rim, a dark violet trilight;
  linear fog 140-3200 m in deep indigo; the anime style on with a strong grade (saturation 1.25, contrast 1.15, violet shadows,
  warm lights); the advanced lighting (light volume, scatter, beams; no weather); eight practical lights (the four plaza braziers
  and the two crown braziers in cold cyan, the two star altars in warm gold); far plane 2600 m.
- **Stone:** pale warm sandstone flagstones (`cyber-tile`) on the decks, ashlar (`cyber-panel`) on walls, gables, spires and
  gates, star-metal (`cyber-metal`) pylons and altars; the kit's carved detail on every block (below).
- **The void (effects):** a deep star field all round and below the bridge (the sky dome only draws stars above the horizon), the
  brightest with spikes; five nebulae in layered veils with dark dust lanes; the river of stars drifting 420 m below; shooting
  stars; the distant sun's glare; the planet's atmosphere.
- **Backdrop (baked SourceLit):** the towers' buttressed, rune-banded stumps breaking into floating blocks with violet crystal
  hearts; the span's hanging arches and pendant finials with crystal tips under the keel; lantern chains under the walkways; the
  rubble frozen in its fall under the Breach; the lost spans of the causeway drifting on past both towers; a ruined shrine and a
  kneeling colossus on floating rocks; the asteroid belt; distant obelisks and islands; **the ringed planet** (a 230 m banded gas
  giant with a storm spot, self-lit toward the sun, and its rings with the Cassini gap and the planet's shadow across them).
- **Kit (NativeBlockLit instances, every attached part inside its block + 0.25 m):** the comet's star-metal channel and gold ticks,
  compass medallions; the keel's pilasters, cornice, rune band, lit cracks; the Breach's broken faces with rebar and cold light;
  balustrade plinths, copings, relief panels, crystal finials; pylon silver edges, crystal slits, gold bands, crowns; hall
  pilasters, string courses, rune-lit niches, the arch's keystone glow; altar rune columns; the gables' rose-window ring; the
  spire's ribs and broken crown; gate frames and lanterns; stair nosings and the floating flights' cold undersides; balcony
  corbels. **Sculpted** (the dressing hides these blocks' own boxes while the kit draws): the star-winged wardens and the watchers
  with their stars, the bronze crystal braziers, the fallen column drums.
- **Rig (per frame, from the replicated state):** the falling stars' burning stones and their shards; the comet lane's glyphs
  waking from the far end through the warning; the pylons' banners standing out in the solar wind; crystals floating over the
  pylons, braziers and spires; the Shattered Arch's nine voussoirs drifting over the Breach (2 m+ over every standable top); the
  near asteroids tumbling round the void (always outside the bounds).
- **Hazard looks (never reduced by the quality or the low tier):** a star's impact ring beating faster, its inner glyph ring and
  growing fill, the star kindling on its slanted path with an aiming shaft and a column of light over the ring; the fall (head,
  40 m tail, sparks); the flash, the shockwave across the stone, the dust; the scorched crater glowing out, embers and smoke. The
  comet far out along the bridge's axis sweeping in, the lane's glow (amber in the warning, ice-blue in the danger, sparks), the
  pass (core, coma, ion and dust tails, glitter) and its wake. The wind's shimmering front gathering upwind, light chevrons on the
  deck and the walkways pointing downwind, the gust's streaks driven across the section and its bright front sweeping through.
- **Audio** (existing clips with `causeway.*` bank keys the owner's bank may fill): the star's kindling, fall and impact (with
  the structure landing cue); the comet's warning, danger and pass (heard where it crosses the listener's line); the wind's rise
  and gust.
- **Low tier:** the beams fall back to billboards; atmosphere counts drop to a third; telegraphs are unchanged.

## Stage art (2026-11-10)

2026-11-10 · Claude (stage art, phase 2) · branch `claude/art-causeway` from `claude/stage-art` (`d0c8df39`). Owner: "Are the new
maps voxel? ... move away from box built ... more high quality designs and models. Make use of unity engine." The recipe is
`docs/arenas/STAGE-ART.md`. **Implemented on the branch and seen in the shared Editor; not merged, not released.**

With the stage bank `ChipArenaStageCauseway` (git-ignored, from the builder's Drive zip) the Causeway is no longer drawn from
boxes: `NativeStageArtDraw` draws the bridge modelled in Blender in place of every map block's box, the kit's 4175 boxes, the
rig's boxes and the box-built backdrop sectors. The gameplay geometry (blocks, colliders, nav, hazards and their timings, spawns)
is untouched and every rules check is unchanged; without the bank everything draws exactly as before. The effects
(`NativeCausewayFx`: the star field, nebulae, the telegraphs, the comet's coma and tails, the wind) and the planet with its rings
are as they were.

**Modelled** (an original design; `tools/stages/causeway_*.py`, the west half sculpted block by block and turned half round for
the east, as the map is point symmetric):
- *The span*: flagstone decks with the comet's star-silver channel, the lane's glyph tiles, gold and silver compass roses ringed
  with runes; corbelled wings with a moulded fascia; the keel's flanks as an arcade of stepped buttress piers carrying the wings
  on curved corbels, a blind pointed arch with a moulded archivolt and a starred roundel in each bay, rune friezes, a string
  course, the hull turning in under it with carved drops; **the Breach** as chiselled fracture faces eaten back under the deck's
  lip with cold light in the cracks; the floating **keystone** broken to a point that holds a crystal in gold claws.
- *Balustrades*: moulded plinths and rounded copings, blind pointed arcading on both faces, posts with pyramid caps and crystal
  finials; an end that meets nothing stands broken in toothed courses with rubble at its foot (never taller than a hand).
- *Star-metal pylons*: stepped feet, tapering eight-sided shafts with silver arrises and a lit crystal seam down each face, gold
  bands, a rune, a flared head with four claws under the floating crystal, the banner's rod.
- *Guardians* (wardens and watchers): moulded plinths with a rune band, robed figures with folds, a gold belt, a deep hood dark
  within, a halo, wings folded down the back, arms raised to the star they hold. *Braziers*: bronze bowls on three curved legs
  over a bed of crystal. *Fallen columns*: fluted drums with an astragal and a broken end; a column's stump on its base.
- *The towers*: buttresses stepping the whole height from the foot to the crown, blind arches below the floor line and rune-lit
  lancets above it, corbel tables under the crown's overhang; the front's great arch and doors (arches sprung above head height
  inside the square openings, moulded jambs, lit keystones, rune pilasters); the star altar as a stele with a niche, a gold
  star round a crystal and engaged columns; the crown's moulded edge, ceiling ribs and rune ring; the gable with buttress
  pilasters and pinnacles, the window's arch and sill, a rose of lit petals, blind lancets; the broken spire as an eight-sided
  turret with three storeys of lit lancets and a ragged top; the lantern gates with their lintels on corbelled half arches and
  bronze lanterns of warm glass.
- *Walkways, balconies, stairs*: arcaded beams; balcony slabs on flying brackets from the keel; solid flights with rounded
  nosings; the floating treads with a keel and a cold line under each.
- *The void* (in place of `NativeCausewayScenery`'s Near, Landmark and Far sectors, at the same places): the towers' stumps in
  battered tiers down to a broken underside, masonry breaking away below and a violet crystal heart in a star-metal cradle; twin
  hanging arches under each half span with hangers and turned pendants tipped with crystal; lantern chains under the walkways;
  the rubble under the Breach; the eight lost spans as lengths of the same bridge, both ends broken; the shrine (a colonnade under
  a broken dome on a stepped platform) and the colossus (a great guardian sunk to its waist beside its planted sword) on floating
  rocks; columns on small islands; the asteroid belt, far islands and obelisks.
- *The rig* (`Core/NativeCausewayArt.cs`): the falling stars as burning stones (molten within a scorched crust) and their shards;
  the comet's nucleus of dark ice with bright facets; the lane's glyph tiles lit in amber then cold blue by the rig's own level;
  the pylons' banners (three lengths of cloth, a gold hem, a rune) standing out in the solar wind; the floating crystals; the
  Shattered Arch's nine carved voussoirs; the crowns' drifting ruins as torn lengths of wall with lancets and a rune band; the
  near asteroids.

**How it moves.** `NativeCausewayRig` now tags each box part it builds with the art's slot (`Slot`, `Level`, `Kind`, set beside the
very `Parts.Add`; the parts themselves are unchanged). `NativeCausewayArt.Pose` places each tagged part's model at that part's own
centre and turn (a unit crystal, rock or shard scaled by its half extents), so the hazards' models stand exactly where the
simulation's states put the boxes. No rules, timing, collider or network change.

**Materials** (`ChipArena/StageLit`, unchanged): flagstone joints on the decks and walls, plain carved trim, star-metal, silver,
gold and bronze kept at half metal or less (a full metal shows only the black sky), baked dirt and occlusion (eased: an open-air
stage), emission above 1 on runes, crystals and lanterns for the bloom. The rougher fine surfaces (ashlar, carved grain) were
tried and dropped: under the anime style's stepped light a practical lamp breaks them into blotches.

**Low graphics tier**: the bank's Low tier (the same art, about a fifth of the triangles), not the box kit.

Rebuild (from `unity/ChipArenaConversion`; about 12 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout causeway LAYOUT.json
blender --background --factory-startup --python tools/stages/causeway_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage and the rig's props in Blender; `--tiers`, `--no-ao`,
`--no-void`, `--static-only` for quick looks.)

Files: `tools/stages/causeway_art.py` (the span, balustrades, pylons, the scene, the bank), `causeway_parts.py` (the sculpted
blocks), `causeway_props.py` (the rig), `causeway_void.py` (the void), `causeway_kit.py` (finishes and helpers);
`Core/NativeCausewayArt.cs`; the rig's tags in `Core/NativeCausewayKit.cs`; the hooks in `Runtime/NativeCausewayPresentation.cs`;
`Tests/CausewayArtChecks.cs` (`--causeway-art`, also in the full run). Shared foundation, small and general: the bank's line in
`NativeStageArt.Banks`, `NativeStageArtPose.AddScaled` (a draw scaled along the prop's own axes), one line each in
`StageArtLayout.Write` and `Tests/Program.cs`.

Checks (`--causeway-art`): the slots; at every 0.05 s of two rounds every draw is its tagged part's (place, turn, size), a stone
for every falling star, the arch's nine, both crowns' ruins, sixteen asteroids, every banner and crystal; deterministic,
allocation free; the layout; the map's point symmetry; the wiring and the bank-absent fallback; and, where the bank is on the
machine: every block covered, every rig slot in three tiers, the chunks' tiers, the budget, the probe clear of blocks, the
play-space rule (within 2 m over a floor the art keeps within .25 m of a block) and the spawns.

**The bank** (2026-11-10): 19,752,651 bytes (budget: about 20 MB), 486 props, deterministic (the same layout builds the same
bytes). Static: 38 map chunks in three tiers and 8 far chunks in one; Fine 161,628 triangles (196,231 vertices), Mid 68,548
(104,204), Low with the far void 53,652 (88,158). Rig: 23 props; 4,214 / 3,606 / 1,338 triangles by tier; up to 187 rig draws a
frame (the pose buffer holds 256).

**Performance** (shared Editor, the span from plaza A toward the Breach, 10 s, one idle bot): High tier, the box kit 574 frames,
mean 17.45 ms, p95 20.87 ms; the art 1041 frames, mean 9.61 ms, p95 11.13 ms (a second run 889 frames, 11.27 / 12.74 ms). Low
graphics tier: the box kit 655 frames, mean 15.30 ms, p95 17.95 ms; the art 1259 frames, mean 7.94 ms, p95 9.09 ms. (Other agents
shared the machine during the runs; the direction is clear, the exact figures are not.)

**Seen in the shared Editor** (a match, one idle bot, god mode, the game's camera placed by `maincam`): before (the box kit,
`d0c8df39`) and after, the same ten views: wide from the south-west, the span from plaza A, the Breach and the keystone, the north
walkway and the keel's flank, tower A's front, crown A with the gable and spire, a warden and a pylon, under the span, the lost
spans, the planet over the span. The hazards on the art: a falling star's telegraph, impact flash, shockwave and thrown shards
(dark and burning); the solar wind lifting the pylons' banners; the comet lane's glyphs waking amber then cold with the comet
far down the bridge's axis. The Low graphics tier: the overview and the altar close. Editor build checks
(`PrototypeBuild.Validate`) pass on `a17c0cf4`, including "native map causeway: 8 spawn floors, capsule settling".

**What still draws as boxes with the bank**: nothing of the stage's own (every block, kit part, rig part and backdrop sector has
its model). Without the bank: everything, as before.

**Falls short / not seen**:
- The burning stone itself in its 0.55 s fall and the comet's pass across the camera were not caught in a frame (their models are
  checked against the rig's parts at every 0.05 s, and their shards, flash and lane were seen).
- Near a practical lamp the walls' flagstone grain still mottles a little under the anime style's stepped light (the tower's
  front by the plaza braziers); the fix belongs in the shared shader (less micro-normal under the style), not in this stage.
- The towers' stumps and the hanging arches under the span are plainer than the deck (seen only from the walkways and in falls);
  a fallen drum's broken end reads as a rounded boss from straight on; the colossus stands sunk rather than kneels.
- The Low tier's scripted views drifted (only the overview and the hall were framed); the bank-absent run in the Editor is the
  "before" capture; no native Windows build, multiplayer guest or killcam run on the art.
- No shadows from the practical lights (as STAGE-ART.md says for every stage).

## Music

The owner's song **Celestial Bell Motif** (Suno, 2026-11-10; 219.8 s) plays under the key `causeway-theme`
(`NativeStageMusic.Theme`). The map record's `music` keeps the placeholder `skyframe-rising-motif` as the fallback.

## Files

- Draft: `docs/arenas/scripts/causeway_m1.py` (layout, hazards, checks, plan PNG). Generator: one line in
  `unity/ChipArenaConversion/tools/generate_stage_hazards.py`; `UnityMaps.json` and `tools/asset-lock.json` regenerated (the
  generator also turns the five M1 maps' JSON `rotation` to true, which the loader already forces).
- Core: `NativeCausewayLooks.cs` (ids, kit kinds, places, the hazards' presentation poses, audio cues), `NativeCausewayScenery.cs`,
  `NativeCausewayKit.cs` (kit and rig), `NativeCausewayFx.cs`. Runtime: `NativeCausewayPresentation.cs` (draw component, SourceWorld
  and prewarm hooks, audio, the vent push).
- One line each in shared files: `NativeSourcePresentation.SourceWorld`, `NativeStagePrewarm.PrewarmStage`, `NativeAudio.Render`,
  `NativeArenaDressing.Render` (the sculpted blocks), `NativeStageHazardsRuntime.StepAuthoredHazards` (the push).
- Data: `MapLooks.json` (`causeway-starfall`). Pins: `NativeUnityMaps.ValidateShippedCatalog` (ids, Random set), `BrickfallChecks`
  (17 maps, Random list, 12 selectable), `TransitionLoadingChecks` (17 maps, the prewarm entry), `SurfaceChecks` (15 Unity maps),
  `MapStyleChecks` (styled list, 14 looks), `AdvancedLightingChecks` (advanced looks). `TransitionLoadingChecks`' loading-plan
  sanity bound on a map's aspect is now under 4 (was 3): the bridge's blocks run 76 x 19.6 m (3.9), and the loading HUD already
  fits any aspect into its frame.
- Checks: `Tests/CausewayChecks.cs` (`dotnet run --project Tests/CoreTests.csproj -c Release -- --causeway`; also in the full run).

## Numbers (CoreTests, Release, this PC)

Kit 4175 parts (packed once into instanced batches of 500); rig up to 269 parts a frame; effects up to 3507 quads a frame (low tier
1428); backdrop 50 119 vertices / 40 658 triangles in five meshes (three sectors, the planet, its rings); the rig and effects build
in about 0.3 ms a frame, allocation free. Map record: 295 blocks (0 replicated: all static), 68 pilot columns (58 bots only), nav 1303 nodes.

## Not done / not seen

- Seen in the shared Editor (bot matches, god mode, scripted cameras; normal and Low tier): the overview and wide shots, the
  walkways, the crown's gable window, the underside, the planet, a falling star's telegraph, fall and impact, the comet's danger
  and pass (once, from a distance), the solar wind's gust. Editor build checks (PrototypeBuild.Validate) pass, including
  "native map causeway: 8 spawn floors, capsule settling".
- Played only by bots; no human playtest, no network (guest) test, no Windows build.
- The pylons' banners in the wind were not clearly seen in a capture (they hang on the pylons' deck faces).
- The push strength and its feel against real input are unverified; bots do not brace against the wind.
- The sky dome's own stars stop at the horizon (the effects fill below); the planet is a smooth self-lit mesh (no specular).
- No ambience loop (`causeway-void` is a key only); hazard sounds are the existing clips.
- The killfeed has no hazard names (as on every map).
