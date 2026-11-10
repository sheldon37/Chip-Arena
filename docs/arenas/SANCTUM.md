# The Hollow Sanctum (`sanctum`)

2026-11-10 · Claude (stage build) · branch `claude/stage-sanctum` from `claude/sleepy-ritchie-g8ruhv` (`e6d564dc`).

Owner request (2026-11-10): "Begin development of 4 newer levels ... set a new bar for quality ... unique hazards ... VERY beautiful,
with exceptional quality effects ... in the random selection." This stage's concept: "a church/old castle interior": a castle's
great chapel at night, moonlight through stained glass, hundreds of candles, iron chandeliers, galleries and a crypt.

Status: **implemented on the branch, seen in the Unity Editor ("Seen in the Editor" below), not merged, not released.** In Random.

## At a glance

| | |
|---|---|
| Play area | 60 x 40 m (bounds x +-30, z +-20; ceiling 22; killY -10), three levels |
| Built for | FFA 8, teams up to 4v4 |
| Levels | the crypt (-3.6), the church floor (0), the gallery loop (4.0); the vault at 21 |
| Spawns | team A in the narthex under the organ loft (west), team B under the reliquary loft (east); FFA 8 |
| Hazards | the falling chandeliers (crane), the great censer (saw-rail), holy fire (burner), the toll and the crypt gates (shutter) |
| Look | `sanctum-midnight`: the moon through the glass, candlelight, the light volume, scattering, beams, the toon grade |
| Music | placeholder `moonlit-wilderness`; the owner's song takes the key **`sanctum-theme`** |
| Blocks | 297 (14 breakable pews), 1 pilot column (the vault), 6 hazards; nav 2043 nodes / 3244 links (442 gated) |

## The design

A hall church (nave and aisles under one vault) with a crossing and short transept arms, in a castle at midnight. Point
symmetric in collision, (x, z) -> (-x, -z): team A's west end and team B's east end are the same boxes; only the dressing
differs (A: the narthex under the organ loft, the font; B: the sanctuary under the reliquary loft, the high altar and its
reredos).

- **The nave and the choir** (|z| <= 5.3, the full length): pews either side of the crimson runner (half cover, breakable, 60 hp),
  the four chandeliers over the central aisle, the crypt stairwells. The 60 m view down the axis is the stage's picture; the
  pews, piers and stairwell balustrades break it for play, and the spawns stand behind solid screens.
- **The aisles** (|z| 6.7..11.5) under the galleries: a confessional and a saint's plinth a side make a slalom (full cover).
- **The crossing** (12 x 12 m) under the oculus: the great censer's path, the brass star in the floor; the four crossing piers.
- **The transept arms** (|x| <= 6 to |z| 20): the founder's tomb (3.0 m, a perch), a low tomb, the arm stair to the bridge.
  The aisle passage under each bridge carries the holy fire.
- **The gallery loop** (4.0 m): the organ loft (A), the aisle galleries, the two rood lofts across the nave at the crossing's
  arches, the two bridges over the transept passages: a loop round the crossing with stone balustrades (vaultable, 1.0 m).
- **The crypt** (-3.6 m, 16 x 9.2 m, 3.2 m headroom) under the crossing: four pillars, tomb chests, the reliquary shrine in the
  middle (full cover), reached by one stair a side from the nave; sealed by the bell's gates.
- **Routes:** the axis (the most direct, under the chandeliers and through the censer's path), the aisle flanks (through the
  holy fire), the gallery loop (no hazards; exposed), the crypt (gated). Six stairs: one per team end (narthex to loft), one
  per transept arm (to the bridge), one per crypt door.

The draft `docs/arenas/scripts/sanctum_m1.py` builds it and checks: every FFA pair and every A-B pair blocked under +-1 m
jitter and three eye heights (3D, per floor); spawns 3 m or more from every hazard volume on their level; reachability on a
0.25 m raster over the three levels; every flight walkable bottom to top; no top in the 1.3-2.2 m band; point symmetry of
blocks, pilot columns and spawns; the crane zones, the censer's path and the fire curtains clear of blocks; 350 blocks at most.
Longest clear lines (eyes 1.6 m): 53.9 m on the floor (an aisle-to-aisle diagonal through the crossing), 58.9 m on the
galleries (loft to loft down the axis; neither end is a spawn).

## Hazards

All on existing kinds and their simulation (no rules or networking change). Nothing lethal or damaging in the first 4 s
(checked); telegraphs at least as long as today's for each kind.

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **The Fall of the Lights** (`sanctum-chandeliers`) | `crane`, 4 zones | the four chandeliers over the central aisle (x +-12.6, +-19.2), served in turn, alternating halves (odd rounds start one later) | every 15 s from FightAt + 9.5 s (each chandelier every 60 s): creak 3.2 s, fall 1.1 s from 9 m, 4.5 s down as half cover (the landed load, 2.8 x 1.0 x 2.8), regrip 1.5 s, hoist 2.8 s | lethal under it (2.2 m) while it falls |
| **The Thurible** (`sanctum-censer`) | `saw-rail` | the great censer on its rope through the crossing, south arch to north arch (x 0, z -5..5) | every 12 s from FightAt + 6 s: drawn back 2.2 s, swing 1.7 s, a 0.3 s beat, swing back 1.7 s | lethal on its path (1.4 x 2.4 x 1.4) while it swings |
| **Holy fire** (`sanctum-fire-n`, `-s`) | `burner`, 2 curtains each | the fire baskets under each transept bridge pour two curtains across the aisle passage (x +-2.6); the pocket between is safe | period 10 s; N first 6 s (even rounds +5), S first 11 s (even rounds -5): one passage every 5 s; kindle 1.6 s, pour 2.5 s, smoke 1.0 s | 8 per 0.25 s tick (crossing a curtain 2 ticks, a whole pour 80), non-lethal |
| **The Toll** (`sanctum-gate-w`, `-e`) | `shutter` | the great bell over the oculus; the iron gates at the crypt's two doors (x +-8.2) | every 32 s from FightAt + 14 s: three strokes 3.0 s, the gates fall 1.0 s, sealed 8.0 s, rise 1.5 s | a collider door with push 6 m/s and the interlock (never closes through a pilot); the crypt is sealed while they stand |

Telegraphs (light, sound and motion; never reduced by the quality or the tier):
- Chandeliers: the served one sways ever wider (to 0.16 rad), its crown slips twice on the chain (sounds at 45% and 80%), plaster
  sifts from its anchor, a ring of warning light on its zone (gold quickening to red) under a cone from the chandelier; the
  brass ring inlaid round every zone shows the danger at rest. The fall: the chain snaps and whips, flames streak; the crash:
  sparks, a dust ring, candles scattered and guttering, smoke from the wreck. The winch takes it up with its candles relit.
- Censer: its coals flare through the pierced lid, smoke billows, its path smoulders on the floor (a lit strip with bright
  edges, kindling through the warning, burning through the swing); the pull rope to the south bridge lets go; whooshes, a
  smoke trail and sparks while it swings.
- Holy fire: the baskets' coals kindle (the ward-burner grate look heating), embers drip, a faint curtain of light and the
  floor's brass channel flare through the warning; white-gold fire pours down in a curtain while it burns; smoke after.
- Toll: the bell swings through three strokes (deep tolls), rings of light fall down the oculus with each, dust shakes from the
  whole vault and every candle gutters; the gates' red lanterns strobe through the toll and burn red while they stand shut.

Sounds use the existing clips under `sanctum.*` bank keys the owner's effects bank can fill (`sanctum.chandelier.creak`,
`.slip`, `.snap`, `.crash`, `.winch`; `sanctum.censer.flare`, `.swing`, `.back`; `sanctum.fire.kindle`, `.roar`, `.hiss`;
`sanctum.bell.toll`; `sanctum.gate.fall`, `.slam`, `.rise`).

Proposal from the brief not taken: the bell as a "vent with push". The simulation's vent has no push (only damage ticks:
`NativeStageHazardsRuntime.StepAuthoredHazards`); a pushing vent would be a rules change in Astra's lane. The bell instead
tolls the crypt gates (the shutter, which pushes pilots out of the doorway as it closes).

## The look (`sanctum-midnight`, MapLooks.json)

- Key: the moon, cool blue-white, from elevation 42, bearing 200 (south-south-west), soft shadows at full strength: the vault and
  walls shut it out, so it reaches the floors only through the windows (openings at 8 m and up). The south lancets, the south
  transept rose and the west rose take it (`NativeSanctumLooks.MoonShare`).
- Ambient: a dark blue-violet trilight over a warm floor; a faint warm rim. Fog to 140 m; light scattering in the air with the
  practicals' glow; the light volume (occlusion and bounce, the kit's candle sources as bake candidates); volumetric beams.
- The toon style: cool shade, a moonlit key kept cool, a strong grade (saturation 1.14, contrast 1.22), dark violet lines.
- The eight practical slots are picked each frame (`NativeSanctumFx.Pick` -> `NativeLighting.PickedLights`, and
  `ActorLights` so pilots take them): the chandeliers (where they hang, fall and lie guttering), the censer's coals, the holy
  fire while it burns, the gates' red lanterns, and the kit's candle sources nearest the eye; a light near the cut-off fades
  before it is given up, so none pops.

### The kit (Core `NativeSanctumKit`, NativeBlockLit instances)
About 8900 parts and 500 candles: the piers' clustered shafts, bases and capitals; the walls' plinths, string courses, wall
shafts and blind arcading; the windows' jambs, mullions, pointed heads, quatrefoils, the roses' rings, spokes and cusped petals;
the galleries' fascias and oak-beamed soffits; the balustrades' copings and quatrefoils with votive candles; the stairs'
nosings; the pews' ends and backs; the west screen's niches and saints, the reredos's gilded panels and sunburst; the altars'
cloths and candlesticks; the confessionals; the saints in relief; the founders' tombs and effigies; the crypt's niches, skulls,
groin vault and red lamps; the floor's runner, the brass rings round the chandeliers' zones, the brass channels of the fire
curtains, the crossing's star, grave slabs; overhead, the ribbed vaults with gilded bosses, the oculus ring and the censer's
yoke, the belfry with its louvres, the organ (west) and the retable (east), the hanging sigils over the rood lofts.
Rules (checked): attached parts below 3 m lie within 0.25 m of a block (thin props, candles and sticks, within 0.9 m);
overhead parts stand 2 m or more over every standable top under them.

### The glass (Core `NativeSanctumScenery`, SourceLit sectors)
Every window's panes (16912) in eight designs of a jewel palette (ruby, sapphire, emerald, amber, violet, pale): each lancet's
two lights with a pearled border, a ground of diamond quarries, three ringed medallions (four petals round a jewel), lozenges
between them and a rosette in the head; each rose its eye, rings and petals; a dark backing behind panes cut short (the leading). HDR emission: a window the moon faces glows brightest toward its head (past the bloom's threshold).

### The rigs (Core `NativeSanctumRig`) and effects (Core `NativeSanctumFx`)
Posed only from the replicated state (the very functions the simulation decides hits with): the chandeliers (the served one is
the crane's load from the warning to the hoist; checked), the censer on its pendulum at the simulation's point along the path
(checked), the fire baskets, the bell and the portcullises (the shutters' closure; checked), the banners stirring (crimson on
A's half, blue on B's). The effects: every moonlit window's shaft down the moon's ray to the surface it meets (found once per
map against the blocks) and the window's coloured light as a mosaic of pools there, motes in the shafts, every candle's flame
and halo, incense haze, the telegraphs above. Allocation free per frame (checked), deterministic (checked); the low tier and
Reduced effects cut the atmosphere, never a telegraph (checked).

## Files

- `docs/arenas/scripts/sanctum_m1.py` (the draft and its checks), `docs/arenas/SANCTUM.md` (this file).
- Core: `NativeSanctumLooks.cs`, `NativeSanctumKit.cs`, `NativeSanctumScenery.cs`, `NativeSanctumRig.cs`, `NativeSanctumFx.cs`,
  `NativeSanctumCues.cs`. Runtime: `NativeSanctumPresentation.cs` (`NativeSanctumDraw`, the `SanctumScenery` and `PrewarmSanctum`
  hooks, the light volume's candidates), `NativeSanctumAudio.cs`. Tests: `SanctumChecks.cs` (`--sanctum`, also in the full run).
- One line each in shared files: `NativeSourcePresentation.SourceWorld` (`SanctumScenery(arena);`), `NativeStagePrewarm.PrewarmStage`
  (`PrewarmSanctum`), `NativeAudio` (`SanctumCues(arena,menu);`), `NativeAdvancedLightingPresentation.VolumeCandidates`,
  `tools/generate_stage_hazards.py` (the stage in the list, its placeholder song).
- Data: `UnityMaps.json` (regenerated; the five M1 maps' `rotation` flags now read true as the generator writes them, the value
  `NativeMapRules.Merge` already forced), `tools/asset-lock.json`, `MapLooks.json` (the `sanctum` entry).
- Pins: `NativeUnityMaps.ValidateShippedCatalog` (ids, Random), `NativeBuildChecks` (`ValidateM1Runtime` on the sanctum: the
  landed chandelier's collider at its zone), `BrickfallChecks`, `TransitionLoadingChecks`, `SurfaceChecks`, `MapStyleChecks`,
  `AdvancedLightingChecks`. These counts clash with the other new stages' branches at merge; the integrator fixes them.

## Music

No song yet. The map plays the existing `moonlit-wilderness` as a placeholder (generator list). The owner's Suno song takes the
key `sanctum-theme`: add `Assets/ChipArena/Resources/Audio/music/sanctum-theme.mp3` (meta from `breakwater-arena-duel.mp3.meta`),
a `case "sanctum":return "sanctum-theme";` in `NativeStageMusic.Theme`, an asset-lock entry, the length pin in
`NativeBuildChecks` and `ZoneMusicChecks`, and a row in `STAGE-MUSIC-PROMPTS.md`.

## Seen in the Editor (2026-11-10)

Captures in the shared Editor (a match with one bot, god mode, the game's own camera placed by `maincam`, shots on the arena
clock): the nave from both lofts, the aisles and galleries, the crossing and the south rose, the crypt; each hazard through its
telegraph and its moment (the chandelier's sway, cone, fall and landing; the censer drawn back, its smouldering path and its
swing; the holy fire kindling and pouring under the north bridge; the bell's rings down the oculus; the crypt gate falling and
sealing); the Low tier (`graphics:performance`). On the Low tier the moon key stands at a fifth (`NativeSanctumDraw.LowKey`): no
shadows there, so the shut hall would otherwise take it on every face; the shafts are the billboards and the glass, flames and
pools carry the light.

Performance (Editor, High tier, the nave toward the crossing, 983 frames over 10 s): mean 10.5 ms, p95 11.1 ms; the worst frame
(473 ms) is the screenshot the tool took inside the window. Kit 8948 parts (18 instanced batches of 500), the rig up to about
1500 parts a frame (6 batches), the glass 16912 panes in 3 SourceLit sectors, the effects about 2000 quads (capacity 7000).

## Not done / not seen

- Audio: the cues are wired and checked for timing, but not heard (the captures are pictures); the clip and pitch choices
  (a deep "metal" for the bell, "dash" whooshes for the censer) want an ear and, ideally, the owner's bank.
- Native Windows build, multiplayer guests and the killcam on this map: not run (the Editor match only).
- Bots: the nav is generated and checked for reachability; their play (the crypt gates, the censer's timing) is not reviewed.
- The main menu's 3D preview of the stage (RenderBackdrop) draws no sanctum dressing (as for the five M1 stages) and its orbit
  camera stands outside the hall, so it shows the shell; a cut-away preview would be a follow-up.
- No sky is seen except through the belfry's louvres over the oculus; the far layer of this interior is its glass.
