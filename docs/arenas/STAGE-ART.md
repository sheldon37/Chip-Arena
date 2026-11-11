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
- **Coplanar faces** (`lift_coplanar`; owner 2026-11-10: "We need to fix the Z-Fighting on the new maps"): each tier's static
  scene is settled before the bake and the chunks. Wherever a face lies in the plane of a larger one turned the same way (within
  `NEAR_TIER`: 3, 5, 8 mm by tier, closer than the depth buffer parts at the tier's range) and overlaps it, the smaller is moved
  out along its normal just far enough to stand `LIFT_TIER` (6, 12, 20 mm) over it, largest at the bottom, until none overlap.
  Only those faces move, along their own plane, so nothing opens (the larger face is right under the gap). The Causeway's far
  ruins use 2 cm and 5 cm (they draw 60-150 m off). Before it the four banks held 116-268 m2 of such overlap each (the dance
  floor on the hall floor, panels flush on walls, the walkways' trims); after it 0.1 m2 or less at any tier.
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
(about 15 s; `--preview DIR` renders the views, `--tiers`, `--no-ao`, `--static-only` for quick looks).

**The bank's budget** (2026-11-10): a stage's bank keeps to about 20 MB. The Sanctum's first bank was 42 MB; it is 18.9 MB with
its look kept (the same twelve views before and after, below). How:
- The static scenery is built on the Sanctum's own tiers (`Cut` in sanctum_art.py: stagekit's `Tier` with leaner counts). A
  curve takes the segments its turn needs (a pointed arch's half 8, not the half circle's 24); the vault's webs and ribs, the
  bosses and the capitals are built for the ten metres and more they are seen from; mouldings are swept without their backs and
  feet (`sweep_sides`); shafts lose the ends buried in their bases and capitals; the reveals' normals turn with the outline, so
  their corners merge (and the splays shade round); the 500 candles, the skulls, the arcades, the tracery's heads and the
  balustrades' quatrefoils are leaner; flat faces are cut at 1 m (the floors and the moulded bodies keep .75 m: their baked
  shade is the pews' contact shadows and the stiles between the panels).
- **The Mid props share the Fine meshes** (`StageBank.share`): every chunk still has its three props, nothing is stored twice,
  and the leaner Fine tier draws out to the Mid reach (38 m): 312,364 triangles at most, where the first bank's Fine tier alone
  was 565,482 (frame times below). `--own-mid` builds a Mid tier of its own instead (about 5 MB more: over the budget).
- The Low tier (the Low graphics tier's, and past 38 m) is about a sixth of the Fine triangles.
- Where the bake's occlusion made the look, the script keeps it on purpose: the balustrades' dark panels came from corners that
  lay under their strips, so the body's corners stay on the panels' lines and solid strips take part in the bake only (`OCC`).
- The rigs and the pews keep stagekit's tiers.

Numbers, seen, and what still falls short: see "Seen in the Editor" below.

### Starfall Causeway (2026-11-10): the third stage on the foundation

Done on `claude/art-causeway`: `docs/arenas/CAUSEWAY.md` "Stage art" (what is modelled, the bank, the numbers, what was seen).
What it adds to the recipe: a stage whose rig is all transforms can tag its rig's box parts with the art's slots and pose every
model from the tagged part itself (`NativeCausewayRig.Slot`, `NativeCausewayArt.Pose`), with `NativeStageArtPose.AddScaled` for
unit props; a point-symmetric map is sculpted once and turned half round; a backdrop too far for a finer tier goes in named
chunks with one tier (`static/<name>/2`); an open-air stage eases its baked occlusion and keeps its metals at half metal or
less; under the anime style the calm fine surfaces (flagstones, none) hold up near practical lights where ashlar and carved
grain break into blotches.

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

The bank (2026-11-10, slimmed to the budget): 18,854,526 bytes, 588 props. Static: 54 chunks; Fine 312,364 triangles (245,494
vertices), Mid the Fine meshes again (none of its own), Low 50,243 (57,011). Rig: 29 parts; 44,332 / 30,500 / 12,989 triangles
by tier. The pew: 2,122 / 1,438 / 324 triangles, one mesh for the 14 pews. About 15 s to build. (The first bank: 42,057,192
bytes; static Fine 565,482 triangles (426,113 vertices), Mid 259,862 (199,869), Low 121,248 (118,881); rig 47,642 / 33,437 /
13,790.)

The slimmed bank against the first, in the shared Editor (the eight views above and four close ones: up a lancet from the
gallery, the west altar and screen, the reliquary, a confessional and its saint; High and the Low graphics tier): the look
holds; close to, the balustrades' quatrefoils have twenty corners where they had twenty-four and their strips are flat, a candle
has one drip, and on the Low graphics tier the capitals, skulls and candles are plainer and the arcades are niches without
shafts. Frame times, the same view and tier as above, 10 s, two runs each: the first bank mean 9.70 and 9.39 ms, p95 11.19 and 11.15 ms; the slimmed one 8.68, 8.77 and 8.89 ms, p95 10.15, 9.83 and 9.78 ms (every
chunk within 38 m now draws the Fine meshes, which are fewer triangles than the first bank's Fine and Mid mix).

The Low graphics tier draws the bank's Low tier, not the box kit: the same art, about a sixth of the Fine triangles, the probe at
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
- The bank's vertices are 44 bytes each; a quantised vertex format would halve every stage's bank (a format change: not done).
- The main menu's 3D preview draws no Sanctum dressing (as before); native Windows build, multiplayer guests and the killcam on
  the art: not run.

## 10. Substation 128 (`club`): the club's art and the Greys

2026-11-10 · Claude (stage art, phase 2) · branch `claude/art-club` from `claude/stage-art` (`d0c8df39`). Status: **implemented
on the branch and seen in the shared Editor; not merged, not released.** The bank is local (git-ignored). The club's own notes:
docs/arenas/CLUB.md "Stage art".

Scripts (tools/stages/): `club_art.py` (the run, the structure and furniture on their blocks), `club_hall.py` (the shell, the
hall above the play space, the galleries and the pit, the substation's gear, drapes, posters), `club_props.py` (the breakable
props, the rig's machines), `club_greys.py` (the Grey DJ and crowd), `club_kit.py` (finishes and helpers they share: rounded
boxes `rbox`, lofted skins `skin`/`limb`/`ring`, I-beams, rivets, a wall plane with arched openings `arcade`, and `kit_part`,
which turns a kit bar into a round tube and a lit strip into a glow slab). Core: `NativeSubstationArt.cs` (slots and `Pose`),
`NativeClubRig`'s new outputs, `NativeClubKit`'s Family/Owner/Kind. Tests: `ClubArtLayout.cs` (the layout's club part),
`ClubArtChecks.cs` (`--club-art`, also in the full run).

What differs from the Sanctum's recipe, for the next stage to borrow:
- **Lit screens stay the rig's.** The dance floor's 270 tiles, the LED wall's 162 panels and the cube's 64 take a colour each
  every frame, so they stay instanced NativeBlockLit boxes (flat lit panels are what they are); the art models the frames
  round them. The rig puts them first (`NativeClubRig.Screens`) and the presentation fills only those while the art draws.
- **The backdrop's near sector gives way too.** The hall above 12 m, the galleries and the pit were box-built scenery
  (NativeClubScenery's Near sector); the bank models them and the presentation skips that sector while the art draws. The far
  sectors (the river, the bridge, the city) stay.
- **Cover reads as cover.** A rail is a smoked-glass pane between chrome posts and a truss tower has a black scrim sock in its
  lattice, because both stop shots; the gallery's rail (outside the bounds) is open bars.
- **A pose buffer of 512** (`NativeStageArtPose.Capacity`, was 256): the crowd is 64 figures of four draws.
- **Each rig prop is baked apart** (`club_props.bank_props` sets the props 40 m apart for the occlusion bake), so props that
  share the origin of their own frames do not shade one another.
- **Budget.** `club_art.py --stats` prints where each tier's vertices go. The Mid tier is what most of the hall shows most of
  the time (Fine reaches 16 m), so the tiers were cut by what is seen: rivets, buttons and glazing bars are Fine only, the
  roof's gussets and lantern bars Fine only, the truss lacing four-sided (three on Mid), cushions `rbox` with one segment.

The Greys (the owner: "The DJ and people on metro map; Lets make those into Aliens (The design of Greys)"): an original Grey,
tall and thin with smooth grey skin (the shader's wax surface, so the light wraps), a bulbous cranium lofted from nine rings,
big black almond eyes lying in the face's own curve, nose and mouth slits, a narrow torso, long arms and three long fingers
and a thumb. Rigid parts in their own frames (club_greys.py lists them), posed by `NativeSubstationArt` from the rig's own
numbers (`NativeClubRig.CrowdPose`, `DjNod`, `DjArm`, `DjLift`): a crowd figure is four draws (legs stretched to the bobbing
hip, body and head leaning with the sway, two arms hanging or turned over and waving); four looks (grey with a cyan glow
necklace, pale green-grey with magenta glow bracelets, blue-grey in a silver suit, light grey in a dark top with a violet chest
band) and the rig's own heights. The DJ is taller and paler, in a violet jacket with a flared collar and glow piping and chrome
headphones with glowing cups: six draws (legs, torso, head nodding and tipping on the beat, the right arm bent to the
headphone, the left upper arm and forearm: the hand on the mixer, thrown up through the build and the drop). The crowd's phone
lights are the effects' (unchanged: Core NativeClubFx.Phones reads the same places).

Rebuild: `--stage-layout club LAYOUT.json`, then
`blender --background --factory-startup --python tools/stages/club_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets`
(about 15 s; `--preview DIR [--views a,b]` renders the named views, `--tiers`, `--no-ao`, `--static-only`, `--stats`).

Seen in the Editor (2026-11-10; a match, one idle bot, god mode, the game's camera placed by `maincam`): ten views before (the
box kit) and after (the art): the hall from the bar and from the stage, the floor and truss, the DJ, a speaker stack, the west
gallery's crowd, the pit's crowd, the bar, a side lane's booth, the mezzanine; the hazards on the art (the charged floor live and
charging, the laser wall's posts riding their slots, the bass drop with the cones thrown out white); the Low graphics tier.

The bank: 15,903,565 bytes, 618 props. Static: 82 chunks; Fine 156,606 triangles (138,590 vertices), Mid 101,936 (94,847), Low
34,790 (45,962). Rig: 25 parts; 15,190 / 9,419 / 4,995 triangles. Breakables: 6 meshes for 20 blocks; 10,224 / 4,864 / 812.

Performance (Editor, High tier, the hall from the bar toward the stage, 10 s, runs taken back to back): the box kit 1,236 frames,
mean 8.09 ms, p95 9.28 ms (an earlier run 7.76 / 8.69); the art 1,194 frames, mean 8.38 ms, p95 9.20 ms: level with the boxes
within the runs' spread, not under them. Before the rig's repeated parts were drawn instanced (the drawer now batches a slot
posed eight times or more a frame with `DrawMeshInstanced`, its glow per instance) the art took 10.25 / 11.66 ms.

Shared foundation changes made for the club (small and general): `NativeStageArtPose.Capacity` 256 to 512; `NativeStageArtDraw`
draws a rig slot posed eight or more times a frame instanced; a static chunk wholly above the map's ceiling casts no shadow
(a roof over the hall would shade the look's key; the Sanctum has no such chunk); `NativeStageArt.Banks` and
`StageArtLayout.Write` take the club's line.

Not done / not seen:
- The frame time is level with the box kit's, not below it.
- The Mid tier's cast-iron columns read blotchy under the toon grade a few metres off (the iron surface's rust patches); the Low
  tier's Greys have mitts for hands and the Low platters are octagons.
- The gallery Greys stand 25 m and more from the floor against their wall of light: they read as Greys from the mezzanines,
  as silhouettes from the floor.
- No bank-absent run in the Editor with this branch's code (the before captures are the foundation commit, where the club has
  no bank line; the fallback is checked by source in ClubArtChecks). Native Windows build, multiplayer guests, the killcam on
  the art and the main menu's preview: not run.
- The lit LED screens are still boxes by design; the far backdrop is still box-built.

## 11. Skyframe (`skyframe`): a stage built with its art (2026-11-11)

Skyframe was rebuilt from scratch on 2026-11-11 (docs/arenas/SKYFRAME.md has the whole account: the layout, the hazards, the
look, what is modelled, the bank's numbers, what was seen). It is the first stage whose layout, box kit and art were made
together. What it adds to the recipe:
- **The probe's place and sky** (`NativeStageArtDraw.ProbeAt`, `ProbeSky`, set before `Prepare`; small and general): an
  open-air stage whose glass must catch the sky gives the probe the sky's colour instead of near black, and a stage whose
  bounds' centre lies inside a block (Skyframe's lift core) stands the probe elsewhere.
- **One model for every tier** for rig parts only ever seen from tens of metres (the crane's top at 38 m, the rotors, the
  airships, the birds, the cloud islands): `skyframe_props.bank_props` banks the mesh once and names it under all three tiers,
  since the drawer picks a rig part's tier by its distance.
- **The sky as far chunks and drifting rig props together**: the cloud sea is a static far chunk (a rolling sheet with heaped
  islands on it); the islands that drift are rig props posed from the match clock, in lanes the static islands leave clear.
- **No layout-kit export**: the Blender scripts work from the map's blocks by id stem and from a small `skyframe` object of
  shared numbers (`SkyframeArtChecks.Layout`: the crane, the frame's columns and which beams stand, the sun, the cloud level,
  the turbines, the windsocks), so the frame overhead and the rig agree with Core without a parts list.
- **Unreachable tops are not floors** in the play-space rule (`--skyframe-art`): the core's and the columns' tops at 12 m carry
  the frame on up.
- **Overhead garden pieces** (strings of lights, hanging troughs with vines, door canopies) hang at 2.15 m and more over the
  floors: the rule's "higher is overhead" used on purpose, so the garden can be lush without art to stand beside.

## 12. The Factory (`factory`): the art pass on an older stage

2026-11-11 · branch `claude/artpass-factory`. Its own page: docs/arenas/FACTORY-ART.md (what is modelled, what stays boxes, the pilot
columns, the numbers). What it adds to the recipe:
- **A kit without family lists**: `NativeFactoryKit.Writer` logs each part's kind and owner (the block, flight or hazard it dresses)
  only for the layout export (`Build(..., log: true)`); the script derives the family from the owner's id.
- **A point-symmetric map modelled once**: every south / B block is built as its north / A twin and turned half round
  (`factory_kit.turned`, `factory_art.canonical`), its kit parts with it, so doors, operator sides and forks face as the kit's do.
- **Kept static panes**: the skylights' glazing stays the kit's lit boxes (no sun shadow), packed apart (`kitKept`).
- **The lift's reach is the tier's whole lift** (`lift_coplanar(eps=LIFT_TIER - .2 mm, passes=12)`): a kit that paints its floor in
  3 to 12 mm layers is closer than the Mid and Low tiers' depth step even where nothing is exactly coplanar.
- **Fewer, larger chunks for one open room** (20 m cells, two bands: 36 chunks): each chunk renderer is a draw in every shadow
  cascade.
- **Pilot columns of an older stage** go through `open_stage` with the nav computed before the columns open, so the nav (and the
  catalogue's size) does not change.

## 13. The 2026-11-11 stage revamp: every stage's page

The owner's revamp of the older stages (each built by its own agent on its own branch, merged on `claude/stage-revamp`):
- Rebuilt from scratch, layout and art together: Pier 9 (docs/arenas/HARBOR.md), Deepwell (DEEPWELL.md), Polaris Station
  (POLAR.md), Skyframe (SKYFRAME.md, section 11 above).
- Art passes on a kept layout: Brickfall (BRICKFALL-ART.md: breakable blocks as models posed by their blocks, landed rubble as
  heaps, `DETAIL_BRICK`), Ward Zero (WARDZERO-ART.md: both records on one bank, movers and Hulk walls posed), the Factory
  (FACTORY-ART.md, section 12 above), Cyber City (CYBERCITY-ART.md).
