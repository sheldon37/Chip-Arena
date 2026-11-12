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
| Music | the owner's **Moonlit Ostinato** (`sanctum-theme`) |
| Blocks | 297 (14 breakable pews), 1 pilot column (the vault, kept: owner 2026-11-10 removed the new stages' other invisible walls), 6 hazards; nav 2043 nodes / 3244 links (442 gated) |

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
- Chandeliers: the served one sways ever wider (to 0.16 rad, an ellipse, its hoop rocking behind the swing), its crown slips
  twice on the chain (sounds at 45% and 80%: a 7 cm drop with a rebound, a ripple up the chain, a burst of plaster and sparks
  at the crown ring); the second slip kills the swing into a shudder and its crown gives to one side, so it hangs nearly plumb
  over its zone when the chain goes. A ring of warning light on its zone (gold quickening to red) under a cone from the
  chandelier; the brass ring inlaid round every zone shows the danger at rest. The fall: the chain snaps above the crown (a
  flash, sparks; the upper length lashes up and hangs, links rain down after it, plaster falls from the anchor), the
  chandelier tips as it drops with its flames torn up into streaks, embers and smoke left along its path; its low rim rides
  the simulation's load height, so the rim meets the floor at the impact. The crash: the wreck slams flat, hops twice and
  rocks to rest; a flash, a shock ring across the floor, sparks that skip, a dust ring and a plume, embers; most of its
  candles are thrown off in arcs and roll to rest guttering, with shards of its arms. Through the regrip the winch pays the
  chain out to the wreck; the hoist rights it as it rises, its candles relit one by one, the debris gone.
- Censer: the tether from the south bridge holds it cocked back past its rope (its foot drawn toward the cleat); each of the
  warning's three heaves cocks it further with a snatch (the lid rattles, sparks cough through it, the tether twangs), its
  coals flare, smoke billows, its path smoulders on the floor (a lit strip with bright edges, kindling through the warning,
  burning through the swing). The release: the tether goes slack with a wave down it, the censer whips through and past
  its rope once, then the bowl trails the rope while it gathers speed and swings ahead of it while it slows; it turns on its
  swivel, its lid jumps on its chains at the release, the turn and the catch, the rope bows behind it; smoke lies along the
  arc where it passed, sparks fall from it, the coals' light crosses the floor under it. The tether is hauled in on the way
  back and catches it at the south arch with a rebound.
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

### The stage art (docs/arenas/STAGE-ART.md, 2026-11-10)
With the stage bank `ChipArenaStageSanctum` (local, from the builder's Drive zip) the hall is drawn as modelled meshes made in
Blender (`tools/stages/sanctum_art.py`, `sanctum_props.py`) on these very blocks and kit parts, lit by `ChipArena/StageLit`: the
walls with their lancets, roses and tracery, the clustered piers, the ribbed vaults, the floors, galleries, balustrades, stairs,
furniture, the crypt and 500 wax candles; the carved pews as block models while they stand; the chandeliers, censer, bell,
gates, fire baskets and banners as rig parts posed by Core `NativeSanctumArt` from the rig's state. The kit's and the rig's boxes
and the dressed blocks then stay undrawn; without the bank everything below draws as before.

### The glass (Core `NativeSanctumScenery`, SourceLit sectors)
Every window's panes (16912) in eight designs of a jewel palette (ruby, sapphire, emerald, amber, violet, pale): each lancet's
two lights with a pearled border, a ground of diamond quarries, three ringed medallions (four petals round a jewel), lozenges
between them and a rosette in the head; each rose its eye, rings and petals; a dark backing behind panes cut short (the leading). HDR emission: a window the moon faces glows brightest toward its head (past the bloom's threshold).

### The rigs (Core `NativeSanctumRig`) and effects (Core `NativeSanctumFx`)
Posed only from the replicated state (the very functions the simulation decides hits with): the chandeliers (the served one is
the crane's load from the warning to the hoist; checked), the censer on its pendulum at the simulation's point along the path
(checked; its lean, turn, lid, rope and tether are secondary motion about that point: "The hazards' polish" below), the fire baskets, the bell and the portcullises (the shutters' closure; checked), the banners stirring (crimson on
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

The owner's song **Moonlit Ostinato** (Suno, 2026-11-10; 194.9 s) plays under the key `sanctum-theme`
(`NativeStageMusic.Theme`). The record's `music` keeps the placeholder `moonlit-wilderness` as the fallback.

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

## The hazards' polish and the lights' depth fighting (2026-11-11)

Owner, on the test build (revision 639300460006585328): "Seems there is Z-Fighting with lights on the hollow sanctum in a lot of
places, also; the swinging hazard animation / dropping hazard animation need significant improvements." Branch
`claude/sanctum-polish` from `claude/sleepy-ritchie-g8ruhv` (`0ccb5a0a`). Visuals only: no hazard's state, timing, zone or kill
moment changed (the simulation's functions are untouched and the rigs still read only them).

### What fought, and the fixes
The stage bank itself measures clean (docs/arenas/STAGE-ART.md 14: 0.00 m2 exact, 1.3 m2 within 4 mm, with the rigs posed). What
fought was drawn at run time over it, none of it layered:
- **The moon's pools of window light** (about 240 additive quads) lay at the exact height of the block top the ray met: in the
  plane of the art's flagstones (y 0), under its runner (12 mm) and inlays (16 to 25 mm). A ray that met a pier's or a wall's
  side still drew a level quad there, half in the stone and half in the air; one that met a pew drew at the pew block's top,
  over its seat. Now each cell's four corners are traced down the moon's ray; a cell is kept only where all four land on the
  top of one height of static blocks .6 m or more wide (a floor, a gallery, a step, a tomb's lid; a breakable pew is passed
  through), a cell that straddles an edge is quartered once and only its whole quarters kept, and the pool lies 40 mm over
  that floor (`NativeSanctumFx.GroundLift`).
- **The telegraphs on the floor**: the chandelier's warning rings and glow (30 and 25 mm up), the censer's path strip, which
  is always lit (30 and 28 mm), the holy fire's glyph line (30 mm: the plane of its grate's bars), the gates' strobe, the
  toll's ring. All now lie at `GroundLift` or a few millimetres either side of it by their own order.
- **Every such quad is drawn nearer the eye** along each corner's own ray by more than the art's deepest stack of layers
  (`NativeSanctumFx.Ground`, Core `NativeStageArt.Nearer` at `MostLayers`: the same pixel, a nearer depth), as STAGE-ART 14
  does for kept kit boxes, so no distance or grazing angle brings them back into the floor's depth step.
- **The stained glass**: panes that overlap in one plane traded pixels as glowing colours: the border's pearls and the
  medallions over the ground's quarries, the medallions' ring panes at their corners, the jewel over its petals, the two
  lozenges between medallions, the head's rosette. Each overlapping set now stands a step (8 mm, `NativeSanctumScenery.Step`)
  nearer the hall than what it lies over (the ring's and the rosette's panes alternate half a step).

### The Thurible (Core `NativeSanctumRig.CenserMotion`, `Censer`)
The bowl's centre stays on the simulation's point at every moment (checked), on the arc from the yoke. About it:
- its axis leans north past its rope by `lean`: held cocked by the tether (.32 rad), three heaves of the warning at 12%, 42% and
  72% (each .075 rad more, a snatch with a ring), at the release a damped whip through and past the rope, then the bowl
  trails the rope by up to .2 rad while it gathers speed and leads it while it slows (both swings), a nod at the turn, and
  the catch (cocked back again with one rebound);
- it turns on its swivel (2.9 rad through the swing, unwound on the way back, a slow stir at rest); a small lean across the path;
- the lid lifts on its chains (a rattle at each heave, 12 cm at the release, 7 cm at the turn, 8 cm at the catch) and tips;
- the six chains bow behind the motion; the rope to the yoke is a line of twelve pieces, bowed behind the swing, with a ripple
  down it after the release; the tether to the cleat is taut while it holds (twanging at each heave and the catch), paid out
  slack (3% of its span) with a wave down it at the release, and hauled in over the way back.
The art draws it as `censer-body`, `censer-lid`, six chains and the rope and tether laid link by link (`NativeSanctumArt`).

### The Fall of the Lights (Core `NativeSanctumRig.Warned`, `ChandelierSet`, `LooseCandle`, `Debris`)
Each chandelier has a frame (`Body`, `BodyQ`) the box rig and the art both draw in; the simulation's load height stays in `Ring`.
- Warning, on the warning's own time (the first rig multiplied the whole hazard clock by a rising rate, so later cycles shook
  faster and faster): the swing and the slips above; the swing's last offset (under .35 m) eases out in the fall's first .6.
- Fall: tips to .30 rad about its own level axis, turning; its centre rides `Lift(tilt)` over the load height so the low rim
  is the load's underside below 4 m (within 9 cm, checked).
- Crash: the wreck's tilt rocks from .30 to its rest .11 (`Rocking`), it hops 20 cm and 6 cm (`Hop`), the collider is the
  simulation's as before. Eleven or so candles per chandelier are thrown (`LooseCandle`: an arc of .4 to .7 s, tumbling, a
  bounce, a roll; kept inside the central aisle), six iron shards, eight links from the break (they land .5 s after the crash)
  and eight lumps of plaster from the anchor (1 s after), each with one bounce (`Debris`).
- The chain: twelve points; a ripple after each slip; snapped, the upper 8.2 m lashes (`Lash`: up to .85 rad at its end a
  fifth of a second after the snap, a wave running down it) and hangs; a 1.2 m stub flails over the falling chandelier and
  drapes across the wreck; through the regrip it is paid out to the wreck's ring; the hoist is taut.
- The others shiver when one crashes. Ambient stir fades out before a chandelier's own cycle and in after it (nothing jumps).
Checked (`--sanctum`): stepping a round at 5 ms nothing drawn jumps by more than its honest speed (the fall's 16 m/s).

### The bank
`tools/stages/sanctum_props.py` adds six rig props (`censer-body`, `censer-lid`, `candle-loose`, `debris-iron`, `debris-link`,
`debris-plaster`; 122 KB in all); the first bank's `censer` and `floor-candles-K` stay for a runtime from before this. A runtime
with a bank from before this (`NativeStageArtDraw.HasRig`) poses those older models (`NativeSanctumArt.Pose(..., legacy)`), so
bank and build may ship in either order. The bank: 20,205,478 bytes (19.27 MiB; the check allows 21).

## Not done / not seen

- Audio: the cues are wired and checked for timing, but not heard (the captures are pictures); the clip and pitch choices
  (a deep "metal" for the bell, "dash" whooshes for the censer) want an ear and, ideally, the owner's bank.
- Native Windows build, multiplayer guests and the killcam on this map: not run (the Editor match only).
- Bots: the nav is generated and checked for reachability; their play (the crypt gates, the censer's timing) is not reviewed.
- The main menu's 3D preview of the stage (RenderBackdrop) draws no sanctum dressing (as for the five M1 stages) and its orbit
  camera stands outside the hall, so it shows the shell; a cut-away preview would be a follow-up.
- No sky is seen except through the belfry's louvres over the oculus; the far layer of this interior is its glass.
