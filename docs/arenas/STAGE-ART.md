# Stage art: modelled meshes and real materials (the recipe)

2026-11-10 · Claude (stage art, item 1: the foundation and the Hollow Sanctum) · branch `claude/stage-art` from
`claude/sleepy-ritchie-g8ruhv` (`062e5707`).

Owner (2026-11-10), on the four new stages: "Are the new maps voxel? They look voxel lol ... Please improve the quality of the maps-
Like better material design; Better modeling ... Yes lets move away from box built, I want us to move into more high quality designs
and models. Make use of unity engine."

Status: **the foundation and the Sanctum are implemented on the branch and seen in the shared Editor; not merged, not released.**
The Sanctum's bank is local (git-ignored); the integrator packs it into the builder's Drive zip (see "The bank").

Every new stage was built from instanced boxes (`NativeBlockLit` batches of each stage's Core kit and rig): thousands of boxes read
as voxels. The stage art replaces them, as the stage's *art only*, with meshes modelled in Blender and lit by Unity. The gameplay
geometry (the map record's blocks, their colliders, nav, hazards and their timings, spawns) is untouched: every rules check keeps
its meaning, and without the bank every box draws exactly as before.

## The pipeline at a glance

```
UnityMaps.json + the stage's Core kit ──CoreTests --stage-layout──▶ layout.json (blocks, kit parts: family, owner, kind, place)
layout.json ──Blender: tools/stages/<stage>_art.py on stagekit.py──▶ Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStage<Name>.bytes
bank ──Runtime NativeStageArtDraw (the stage's presentation owns one)──▶ static chunks (renderers, 3 LODs), block models, rig parts
rig state (Core <Stage>Rig.Build) ──Core <Stage>Art.Pose──▶ NativeStageArtPose (slot, place, tint per draw) ──▶ the rig's models
```

## 1. The layout export

`dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout <map> <out.json>` (Tests/StageArtLayout.cs) writes the
map's bounds, blocks (id, role, material, hp, position, size, rotation) and hazards (kind, a, b, centre, size, carry, zones), and,
for a stage whose kit it knows, the kit: every part with its **family** (the builder that made it: "pier", "lancet", "pew",
"candle", ...), its **owner** (the block it dresses), its role (attached / overhead), kind, colour, centre, half extents and
quaternion; the candles and warm lights; whatever the stage's rigs share (the Sanctum: the windows, the chandeliers' candles as
the rig places them, the bell, the censer's pivot). Units are the game's (metres, Unity axes, +y up).

A stage adds itself with one branch in `StageArtLayout.Write` and, in its kit, the three lists the Sanctum's has
(`NativeSanctumKit.Family/Owner/Kind`, filled where each part is added; nothing draws by them). A value the rig computes and the
models must match (a candle's height, a fallen chandelier's tilt) moves into a public static helper both use
(`NativeSanctumRig.Candle`, `DownTilt`, `BannerSway`), so the two can never drift apart.

## 2. Modelling: tools/stages/stagekit.py

Run inside Blender 5.2, headless (`E:/SteamLibrary/steamapps/common/Blender/blender.exe --background --factory-startup --python ...`).
Everything is authored straight in the game's space from the layout, so the models land on today's pieces.

- **Geometry** (`Geo`): faces with their own corners (position, normal), one finish each; a face orients itself to the normal it
  is given. Primitives: `box` (chamfered; each chamfer corner takes the normal of the face it lies on, so faces stay flat, edges
  shade round and a box needs only its 24 corners), `obox` (a kit part's turned box), `grid_face`, `lathe` (turned profiles:
  columns, candles, bells, finials), `sweep` (a section along a path with rotation-minimising frames: mouldings, ribs, tracery
  bars, rings, ropes), `tube`, `cylinder`, `pointed_arch`/`arch_height`/`arch_width` (two-centred pointed arches, segmental below
  a half-span's rise), `catmull`.
- **Finishes** (`fin`): an sRGB colour, the shader's fine surface (`DETAIL_*`), smoothness, metallic, how much dirt, grime and moss
  it takes, its edge wear and its glow gain (a glowing finish goes on the glow sub-mesh). The stage's script makes its own.
- **The bake**: `bake_ao` (Cycles ambient occlusion per corner over the whole stage, the movable blocks as occluders; a few seconds
  for half a million faces), then `Bake` (numpy): broad mottling, grime rising from the floors, dust in the hollows, moss in
  patches low and in the shade (a stage zone function says where), soot from a plume field over the candles and lamps
  (`SootField`), worn edges. Corners that meet share one occlusion, so the export merges them.
- **Tiers** (`Tier(0)` Fine, `Tier(1)` Mid, `Tier(2)` Low): arc and round segment counts, the flat faces' cell size, chamfers,
  and which details exist (`detail`: Fine and Mid; `tiny`: Fine only). The script builds the stage once per tier.
- **Chunks** (`Chunks`): static faces go into world-space cells (12 m in plan, split at two heights) by their centres.
- **The bank** (`StageBank`, tools/pack_bank/bank.py's format): `static/<chunk>/<tier>` (group "static", world space),
  `block/<block id>/<tier>` (group "block", the block's own frame: its centre; props may share one mesh), `rig/<name>/<tier>`
  (group "rig", the part's own frame), `cover/<block id>` (group "cover", no parts: a map block whose box the art replaces).
  Vertex: colour.rgb the baked albedo (sRGB), colour.a the occlusion, uv0 (smoothness, metallic), uv1 (detail kind, parameter;
  on the glow sub-mesh the emission gain).
- **Preview without Unity**: `stagekit.preview` renders the baked Fine tier in Blender (workbench, vertex colours times the
  occlusion) from named views; use it for every modelling iteration and keep the Editor for the light and the look.

The stage's own script (the Sanctum's: `tools/stages/sanctum_art.py` for the architecture, `sanctum_props.py` for the breakable
blocks, the rigs and their fixed fittings) reads the layout, sculpts the families that matter (walls, piers, vaults, windows,
furniture) and rebuilds every other kit part as a finished piece (`kit_part`: chamfered, its finish, round where the kit's part
is a bar, pleated where it is a curtain, a carved figure where it is a statue), so nothing the kit had is lost.

Rules the models keep (checked, see 6): below 2 m over its floor the art lies within .25 m of a block (a thin prop such as a
candlestick within .9 m), so nothing stands where there is no collider; higher is overhead; nothing stands in a spawn. A face
too thin to close (a vault web) relies on two-sided shadow casting (every static renderer casts both sides).

## 3. The bank

`Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStage<Name>.bytes` with its `.meta` (bank.py's deterministic guid), git-ignored,
never committed: it travels in the builder's Drive zip with the other local banks. Its own bank per stage, so building one never
touches another. `NativeStageArt.BankOf` names each stage's bank (one line per stage).

## 4. Runtime

- `Core/NativeStageArt.cs`: the registry, the plan (`Read`: chunks with their three tiers, block models and rig parts by name,
  the covers; a missing tier takes the nearest), the tier by distance (Fine within 16 m of a piece, Mid within 38 m, Low beyond;
  the low graphics tier always Low), the kept damage stage (`Keep` 2), and `NativeStageArtPose` (a fixed buffer of rig draws:
  slot, 3 x 4 place, tint; `Span` lays a unit-length chain or rope between two points).
- `Runtime/NativeStageArt.cs` (`NativeStageArtDraw`, one per stage presentation): `Prepare(map, rigSlots, casts)` once per map
  (the stage's prewarm calls it behind the loading screen): reads the bank, builds the static chunks as renderers (one solid and
  one glow material shared by all, two-sided shadows, no light probes), the block models and rig pieces, a reflection probe over
  the stage; then frees the bank's vertex copies (the meshes are on the GPU). `Render(arena, eye, low, pose)` each frame: each
  chunk's tier, the breakable blocks' models where their blocks stand (soot with their damage; past `Keep` the dressing's stub
  draws), the rig's draws, the probe rendered once a few frames in. `Ready` and the return of `Render` tell the stage to leave out
  its box kit and box rig. `Hides(map, block, stage)` tells `NativeArenaDressing` which boxes to leave out this frame (only
  while the art drew this frame, only that map's covers). Nothing is created without the bank.
- `Resources/NativeStageLit.shader` (`ChipArena/StageLit`): see 5.
- The stage's hooks (the Sanctum's, in `NativeSanctumDraw`): `art.Prepare(...)` in its Prepare; in its Render, build the rig as
  before (its state still feeds the effects and lights), pose the art, `ArtDrawn=art.Render(...)`, and draw the kit's and rig's
  boxes only `if(!ArtDrawn)`. The stage's glass, effects and practical lights stay as they are.
- A stage's Core art pose (the Sanctum's `Core/NativeSanctumArt.cs`): its rig slot names (the bank's `rig/<name>`) and `Pose(rig,
  hazards, render, pose)`, reading only the rig's state after its Build (pure, allocation free). For new rig outputs add public
  fields to the rig, filled where it already computes them (the Sanctum's: `Age`, `CenserAngle`, `GateClosure`, `FireHeat`, ...).

## 5. The material: ChipArena/StageLit

A forward base pass and a shadow caster (vert/frag, per pixel; the precedent is ChipArena/SourceLit's light and NativeBlockLit's
light block, so the art sits in the same light as the blocks and pilots):
- Unity's physically based BRDF (`BRDF1_Unity_PBS`) for the key with its shadow cascades and the ambient (the SH through the light
  volume, `ArenaGI`), the reflection probe's cubemap (`UnityGI_IndirectSpecular`, box projected) for the metals and the polish, all
  occluded by the baked occlusion;
- the arena's own light block (`StageLights`: the rim, the eight practical slots with a highlight each, the blast flashes, the Ward
  field, the muzzle lights), as NativeBlockLit lights its blocks;
- the anime style when the look turns it on: the key through the ramp, the flattened ambient, the points in stairs, a crisp
  highlight band and the occlusion band; the stencil mark (16) for the outline pass;
- the fine surfaces (procedural, world space, faded out from 18 to 42 m): ashlar courses with mortar and pillowed stones (the
  parameter scales the courses), flagstones with polish and cracks, carved grain, marble veins, wood grain along an axis,
  hammered iron with rust, brass with a patina in its hollows, a cloth weave with its sheen, wax, plaster;
- the glow sub-mesh unlit at albedo x gain x the draw's `_StageGlow` (a lantern's colour, the coals' heat) for the bloom;
  `_StageTint.w` a struck block's soot;
- the ground haze, the advanced scattering, Unity's fog.
It is instanced (`StagePreloadPlan.InstancedShaders` lists it; the warm-up compiles it).

## 6. Checks

`dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-art` (Tests/StageArtChecks.cs, also in the full run): the plan
and the names (a synthetic bank), the tiers, the kept stage; the Sanctum's poses against its box rig at every 0.05 s of two
rounds (every posed model stands where the rig's own part does: the chandeliers' hubs hanging and wrecked, the censer's coals, the
bell's lip, the gates' bars, the banners' cloth; every chandelier phase seen), deterministic and allocation free; the layout
export; the wiring and the bank-absent fallback (source checks); the shader. Where the bank is on the machine, the bank itself:
every block covered, every pew modelled in three tiers, every rig slot in three tiers, the chunks' tiers, the play-space rule and
the spawns (the counts are printed).

## 7. The Hollow Sanctum (the worked example)

Modelled: the walls as one face per wall plane with the 20 lancets and 4 roses cut through with splayed reveals and real tracery
(two-light lancets with trefoil heads, an eye and a quatrefoil; roses with rings, spokes and petals; a gilded boss) at the kit's
tracery plane, so the stained glass sits behind it as before; plinths, string courses, wall shafts and capitals, blind arcading
on the aisle and arm walls; the clustered piers (an octagonal core, attached and filleted shafts, moulded bases, bell capitals
with crockets); the ribbed groin vaults (webs cut on their groins, swept ribs, gilded bosses), the oculus and its ring; flagstone
floors, the runner, the brass rings and the crossing's star; galleries with cornices and oak-beamed soffits; balustrades of blind
quatrefoils; stairs with rounded nosings; carved oak pews (block models: poppyhead ends with blind arches, panelled backs,
velvet kneelers); the altars, plinths, screens and tombs as moulded bodies with fielded panels, the screens' statue niches under
pointed heads, the saints and statues as carved robed figures with haloes; the crypt's columns with cushion capitals, its low
groin vault, round-headed burial niches with skulls, its tomb chests and a gilded reliquary with an arcade and a crested roof; 500
wax candles with drips and wicks wherever the kit stands one. The rigs: wrought-iron chandeliers (hoops, scrollwork, drip pans,
arms, a turned hub, a crown) with their own candles, their wrecks and strewn candles, chains, the silver censer with its pierced
lid glowing, the bronze bell and clapper, the portcullises and their red lanterns, the holy fire's baskets and channels, the
banners. Everything else the kit carries is rebuilt from its parts as finished pieces (the organ, the retable, the sigils, the
belfry, the confessionals, the effigies).

Rebuild: `--stage-layout sanctum LAYOUT.json`, then
`blender --background --factory-startup --python tools/stages/sanctum_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets`
(about 30 s; `--preview DIR` renders the views, `--tiers`, `--no-ao`, `--static-only` for quick looks).

Numbers, seen, and what still falls short: see "Seen in the Editor" below.

## 8. Recipe for the next stage (Substation 128, the Emporium, the Causeway)

1. Kit lists: in the stage's Core kit add Family/Owner/Kind as `NativeSanctumKit` does; move any value the models must share with
   the rig into a public static helper; add the stage's branch to `StageArtLayout.Write`; export the layout.
2. Script: `tools/stages/<stage>_art.py` on stagekit: its finishes, its families sculpted (start with what fills the screen: the
   floors, walls and big masses; then furniture; then the small kit), `kit_part` for the rest; its rig and block props with
   their frames written down in its docstring; previews until the shapes read; then the bank (`--out`).
3. Core: `<Stage>Art.cs` (slots and `Pose`), the rig's new public outputs; `NativeStageArt.BankOf` gets its line.
4. Runtime: in the stage's presentation, `NativeStageArtDraw` as in `NativeSanctumDraw` (Prepare in its prepare/prewarm, Render
   and `if(!ArtDrawn)` round its box draws). The dressing hook is already generic.
5. Checks: extend `StageArtChecks` (its poses against its box rig, its bank's covers, slots and play-space rule).
6. See it: captures in the shared Editor (after replacing a bank the first match stalls a few seconds while Unity imports it: take
   a short warm-up capture first, or the camera windows drift), perf before and after (`perf=` over 10 s), the Low tier
   (`pref=graphics:performance`).
7. Report the bank's absolute path and the rebuild command, so the integrator can repack the Drive zip.

The Greys (Substation 128's DJ and crowd as Grey aliens) are rig props posed from the club rig's crowd and DJ transforms, exactly
as the chandeliers are posed from the Sanctum's.

## 9. The Top Dog Emporium (2026-11-10)

`docs/arenas/EMPORIUM.md` "Stage art" has the whole account (what is modelled, the 37 rig slots, the bank's numbers, the checks,
what is seen and what falls short). What it adds to the recipe:
- **`DETAIL_TILE`** (16) in the stage shader and stagekit.py: glazed tiles on the world's grid (sunk grout, a pillowed glaze, each
  tile a touch its own shade); the parameter is the tile's size in metres. A checkerboard whose squares do not sit on the world's
  grid is a mosaic of faces instead (every tile its own face a hair apart over a grout face).
- **Kept boxes**: a rig whose telegraph lamps change colour per lamp per frame keeps those boxes under the art
  (`NativeEmporiumRig.Kept`; the presentation's `Fill(parts, ArtDrawn ? Kept : null)`), so the pose buffer carries only the
  modelled parts and the telegraphs stay exactly the box rig's.
- **Scaled poses**: a squashing prop (the press's bun, the sauce column) scales all three axes by multiplying the pose's columns
  after `NativeStageArtPose.Add` (the art's own `Scaled`); a prop tinted per frame (a griddle's bed by its heat, a marquee group,
  a bubble tube) is a white glow sub-mesh times the pose's tint.
- **The art's own check file and flag** (`Tests/EmporiumArtChecks.cs`, `--emporium-art`), so stages do not edit one shared file.
- tools/stages/emporium_kit.py has shapes the kit lacks (skinned rings with their own normals, capsules, ellipsoids, lofts of
  rounded rectangles, part rounds for beads and bullnoses, a block's faces in the kit's terms, slanted warning stripes).

## Seen in the Editor

Captures in the shared Editor (a match, one idle bot, god mode, the game's own camera placed by `maincam`, shots on the arena
clock), before (the box kit, `062e5707`) and after (the art), the same eight views: the nave from the narthex and from the east
end, the crossing up to the oculus, the south aisle, the organ loft, the south transept and its rose, the crypt, a pier and the
pews close; the hazards on the art: the north holy fire kindling and pouring (baskets and channel glowing by the heat), the crypt
gate down with its lanterns red, the censer drawn back and swinging with its lid glowing, the bell's toll seen up the oculus.

Performance (Editor, High tier, the nave toward the crossing from the narthex, 10 s): the box kit 978 frames, mean 10.23 ms,
p95 11.25 ms; the art 1107 frames, mean 9.04 ms, p95 10.69 ms (the art replaces about 9000 instanced kit boxes, the rig's boxes
and the dressed blocks with 54 chunk renderers drawn at their tier and about 110 rig and pew draws).

The bank (2026-11-10): 42,057,192 bytes, 588 props. Static: 54 chunks; Fine 565,482 triangles (426,113 vertices), Mid 259,862
(199,869), Low 121,248 (118,881). Rig: 29 parts; 47,642 / 33,437 / 13,790 triangles by tier. The pew: 2,122 / 1,438 / 324
triangles, one mesh for the 14 pews. About 30 s to build.

The Low graphics tier draws the bank's Low tier, not the box kit: the same art, about a fifth of the Fine triangles, the probe at
64 px (the box kit would look like another stage, and costs more draws).

What still draws as boxes: a destroyed pew's stub (stage 3) and every fragment (the dressing's, as on every map); the kit's
secondary pieces rebuilt from their own parts as chamfered, finished pieces rather than sculpted (the organ's case and shades, the
retable's frames, the sigils' rays, the belfry, the confessionals, the effigies, the altars' candlesticks, the floor's inlays and
slabs); the stained glass (Core NativeSanctumScenery, unchanged) and the effects' quads.

Not done / not seen:
- Shadows from the practical lights: the eight slots are shader lights shared by every stage material (NativeBlockLit's light
  block); shadowing them would need Unity point lights with cube shadow maps and an add pass on every lit material (six shadow
  renders each). The moon's cascades shadow the art (the tracery's shadows fall through the windows).
- After 13:06 the shared Editor sat behind a "Low disk space" dialog (drive F: had 4.96 GB free), so the last bank (the moulded
  bodies, figures, screen niches, the crypt's niches, skulls and reliquary) was seen only in Blender's previews, and the Low tier,
  a chandelier's wreck on the art, the bank-absent run in the Editor and the Editor's build checks were not run.
- The bank is about 40 MB (the other local banks are up to 11 MB): the Fine tier carries most of it. A leaner Mid tier or a
  quantised vertex format in the bank would halve it.
- The main menu's 3D preview draws no Sanctum dressing (as before); native Windows build, multiplayer guests and the killcam on
  the art: not run.
