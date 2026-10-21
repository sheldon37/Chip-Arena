# The anime map style

Owner direction (2026-10-21): "rework the expedition maps to be more in-tune with the BoZo anime pack. We're moving away
from voxel now. The PVP maps: Factory, Brickfall, and Cyber City as well." The player characters move to the licensed BoZo
Stylized Modular Characters, Anime Pack (toon ramp, toon outlines, stylized proportions). That pack is never committed and
nothing here depends on it.

Stage 1 (Claude, `claude/anime-map-style`): this document's audit and rules, the shared style foundation, and two pilot
maps, the Expedition city hub and Brickfall. Stage 2 (other agents): the remaining maps and the menu backdrops on the same
foundation (section 6).

Presentation only. No collision, navigation, spawn, block layout, authority or resource data the game logic reads changes:
`UnityMaps.json` and `ExpeditionLayout.json` are byte for byte the base's (`ExpeditionCityChecks` pins both), and only the
presentation resource `MapLooks.json` gains `toon` blocks.

## 1. How the maps are drawn today (audit)

| Piece | What draws it | How it looks |
| --- | --- | --- |
| Dressed blocks (Brickfall, Factory, Cyber City, Ward Zero, the Expedition areas) and every kit part | `NativeBlockLit.shader`, instanced (500 a draw) from one lattice mesh per damage stage (`Core/NativeBlockLooks.cs`) | A box with a fixed 5 cm chamfer for every size. Vertex colour bakes +18% on the bevel and -22% within 30 cm of each corner. Flat facet normals from screen derivatives. Lambert key + trilight SH + the rim + 8 practical + 3 flash + 4 muzzle lights. The look is painted in rest space: brick bond, boards, plates and rivets, planks, setts, each unit with its own hash tone plus value noise, a 22% grime band to 1.2 m with noise, noisy edge wear, top dust and soot, the round's dust coat, and the 2.3 band, tag and lamp telegraphs. |
| The old maps' blocks, trims, gore | `NativeLit.shader` (Unity cubes, Lambert) | Flat colour per face. |
| Pilots, monsters, machines, weapons, Brickfall's skyline bake | `NativeSourceLit.shader` (surface, Lambert) | Flat-shaded boxes with baked per-vertex grime; entrance stages tone with ACES. |
| Zombies | `NativeZombieLit.shader` (own lighting model) | Voxel bodies with stepped mottling. |
| Sky | `NativeNightSky.shader` | Two-colour gradient, fbm clouds, disc and halo, stars. |
| Per-map look | `MapLooks.json` via `Core/NativeMapLooks.cs` and `Runtime/NativeLighting.cs` | Key and rim directionals, trilight ambient, linear fog, shadows, sky values, 8 practical lights, block looks. |
| City kit | `Core/NativeExpeditionCityKit*.cs`, `NativeExpeditionCityFx.cs`, `Runtime/NativeExpeditionCityPresentation.cs` | About 23,000 boxes. Round things are rotated boxes (`Round`: octagons), and the Exchange dome is five stepped tiers. |
| Brickfall | `NativeArenaDressing` (sim blocks), `NativeBrickfallScenery` (about 15,000 boxes baked into 5 SourceLit meshes), `NativeStructureFx` (debris cubes lit on the CPU), `NativeBrickfallFx` (atmosphere quads) | Huge brick boxes, a box skyline, cube debris. |
| Post | `NativeBloom` (OnRenderImage, normal tiers) | Bloom and a display shoulder, no grade. |

### What reads as voxel

1. **One chamfer for everything.** A 1 cm cable, a 1 m crate and a 20 m tower share the 5 cm bevel, which vanishes past
   about 10 m, so every silhouette is a hard cube.
2. **The uniform box shade.** Every box gets the same bevel highlight and the same corner darkening 30 cm from each
   corner, so stacked or adjoining boxes read as separate blocks with seams between them.
3. **Per-face flat colour.** Facet normals and plain Lambert give each face one flat value with no gradient. Faces read as
   tiles.
4. **Per-unit noise.** Hash tones per brick, plank and panel plus value noise give a voxel grain at every distance.
5. **Block bands.** Course, board and panel patterns run along each block's own axes, and the noisy grime band repeats
   1.2 m up every box whatever stands there.
6. **Boxes for round things.** Domes, tanks, posts and pipes are stepped or octagonal boxes.
7. **Physical, uncoloured light.** Shadows are dark ambient with no tint, and there are no outlines and no stylised sky.

The tag shading (2.3 chevron bands, the DANGER tag, the band lamp, window strobes) is gameplay telegraph, not a voxel tell.
It stays exactly as it is under the style.

### What an anime look needs

A clear cel ramp with coloured shade: two main bands (cool, saturated violet-blue shade and warm light) with a crisp,
slightly soft terminator, a third darker band in occlusion, and cast shadows in the same flat shade. Big clean colour fields
with few mid-tones and painted value gradients instead of noise. Strong dark lines on silhouettes and major creases, thin
or none on interior detail. Designed forms instead of boxes: copings, plinths, capitals, posts, broken corners, no straight
90 degree silhouette edge. Rounded edges with a painted highlight. A saturated painted sky with readable cumulus. Strong
atmospheric perspective. Neon kept as clean saturated emissive accents. A light grade.

## 2. Style rules

**The switch.** `MapLooks.json` `toon.on` per map look. The `--ca-map-style legacy` command line (or
`--ca-map-style=legacy`) restores every map's legacy look. The shaders read one uniform, `_ArenaToon.x`: 0 is the legacy
look exactly (each toon path sits behind that one uniform branch, and the helpers return their input unchanged), 1 is the
anime style. Entrances, the draft, the rooftop and every map without the style write 0.

**Palette.**
- Shade is cool and saturated: a `shade` tint of about (0.6, 0.72, 1.3) on the ambient, plus a `lift` of the key (0.2 to
  0.3) so shade stays luminous and walls never go near black.
- Light is warm: a `lit` tint of about (1.1, 1.0, 0.9) on the key.
- Saturation +14 to +16% in the grade.
- No near-black albedo fields: the street takes the sky's sheen at grazing angles.
- Neon stays saturated cyan, magenta and red, and sodium stays amber.
- Telegraph colours never change.

**Values and ramp** (`Resources/NativeToon.cginc`, CPU copy `Core/NativeMapStyle.cs`):
- Two main bands on N.L: shade below `lightEdge`, lit above it, the terminator `softness` (0.025) wide. Brickfall's
  terminator is at N.L 0.3 (sun 30 degrees up); the city's at 0.12, so the low sun still lights the street, the treads and
  the roofs (a grazing terminator striped every stair and stacked slab). `halfTone` 0 (a third, middle band is available).
- The key's shadow is kept apart from N.L and eased (`ToonShadow`, 0.15 to 0.85 of the shadow map's filtered value): a
  cast shadow's edge is crisp but slightly soft, it is the same flat shade as a face turned away, and shadow-map noise on
  a grazing face never flips whole bands.
- The third band, occlusion (`ToonOcclude`): the light deepened toward the shade tint by `occlusion` (0.4) in a crisp band
  `occlusionHeight` (0.22 m) up every wall's foot, on every downward face (under copings, ledges and slabs), under a
  crate's brace and under a window's head.
- The ambient is Unity's trilight SH, flattened 35% toward one sky-to-ground fill.
- Point lights (the practical slots, flashes, muzzle and actor lights, the ward field) are summed and stepped on the
  square root of their luminance into soft stairs (3 per unit). Lamp pools read as two or three flat rings
  (`points` 0.7 blends them).
- A stylised highlight: on rounded edges a crisp band where N.H passes `specularEdge` (0.965), `specular` (0.55) of the
  key, in the key's shadow.

**Outlines** (`Resources/NativeToonEdge.shader`, `Runtime/NativeToonOutline.cs`):
- What it is: one full-screen pass from a command buffer at `CameraEvent.BeforeForwardAlpha`. It reads the camera depth
  texture, which the normal tiers already render (the shadowed key and the soft particles ask for it).
- Silhouettes: a neighbour farther than the other side's slope carries on (the second difference of depth). Planes, even
  seen grazing, are never lined. A second ring of taps two pixels away lines deep steps (at least twice the gap) two
  pixels wide, so silhouettes are strong and shallow steps stay thin.
- Creases: where 1/depth bends, one pixel wide. A crease fades out as the pixel's footprint grows from 1.5 to 3.5 cm
  (eye depth x the camera's metres per pixel per metre, `_EdgeFoot`, set from the field of view and the pixel height), so
  interior lines resolve to flat paint at mid range at any resolution and zoom, instead of beating against stairs,
  mouldings and slats.
- Where the line sits: on the nearer pixel only, faded out 40 to 110 m, 1 px per 1080 lines.
- Colour: the scene multiplied toward a dark violet (`lineColor` #2A1E40). A line is a darker version of what it crosses,
  as a toon character's outline is.
- Off on the low tier. The low tier has no depth texture and no pass.
- Characters are not lined by it. The environment's shaders set stencil bit 16 (NativeBlockLit, NativeLit, the scenery's
  SourceLit materials through `_ToonEdgeRef` 16, the rooftop). The characters' shaders clear it (SourceLit's default,
  ZombieLit). With the line on the nearer pixel only, a character's silhouette is left to its own outline, while a wall
  in front of a character still lines its own edge.
- Costing: one full-screen pass of 9 depth taps per pixel, no scene draw and no new geometry pass. The inverted hull would
  draw every block batch twice: the city's busiest frame would go from 26 to 52 draws, and the hull splits at the boxes'
  hard edges.

**Shapes: the wrap** (`Core/NativeToonMesh.Wrap`, NativeBlockLit `ToonWrapClass`, `ToonWrapShape`, `ToonWrap`; CPU copy
`NativeMapStyle.WrapClass`, `WrapShape`, `WrapPoint`):
- On the normal tiers the intact stage of every sim block and kit part draws one mesh, the wrap: a lathe of a rounded
  section (a quarter circle at each corner and a post line either side of it, 28 points) through 8 rows, capped by a fan
  per corner and one over the inner post points. 224 vertices and 444 triangles; one mesh in place of one, so the same
  instanced batches and draws. The vertex stage places it per instance from its half extents, look and seed.
- Classes and their features (each feature switched per map in `toon`):
  - **wall** (masonry looks, at least 0.6 m tall, 20 cm thick, 60 cm long): a plinth at the foot under a sloped weathering
    (`plinths`), a coping band at the top over a sloped drip (`copings`), a 7 cm bevel on every edge, a rounded top and,
    on about 30% of blocks, one broken top corner that steps down and in (`chips`). A big wall (4 m long and 4 m tall)
    stands pilasters 35 cm wide at its ends (with the plinths).
  - **column** (a roughly square masonry pier, 1.2 m tall or more, up to 1.6 m across): a base and a capital standing up
    to 8 cm proud and a round section (radius a quarter of its half width).
  - **crate** (timber, up to 2.4 m tall and 3 m long): corner posts 3 cm proud of the boards, a bottom rail and a top rim
    (`posts`); its sides paint a diagonal brace board with a shade under it.
  - **container** (corrugated, 0.8 m tall and 0.6 m thick or more): posts and rails.
  - everything else: a box rounded on all its edges.
- Never above or below the block; bands and posts stand at most 11 cm proud of its faces (presentation only: collision
  is the block). The silhouette never meets the sky in a straight 90 degree edge: the coping steps, the top rounds, a
  corner breaks. The city turns plinths and pilasters off (its kit doors and shop fronts meet the street).
- The low tier keeps the lighter rounded box (`NativeToonMesh.Build`: every edge a quarter circle in 2 facets, every corner
  an octant, 144 vertices and 92 triangles against the legacy S0's 216 and 188). Damaged stages keep the legacy meshes,
  so breaks stay hard.

**Edges and bevels** (NativeBlockLit `ToonChamfer`, `ToonLattice`, `ToonRoundNormal`):
- The chamfer grows with the block. A rod (its two thinner half extents both at most 15 cm and within 60% of each other,
  at least 3 times as long: poles, pipes, posts, cables, rails) rounds its section at the lattice clamp. Any other block
  up to 1 m thick grows its chamfer by 0.16 per metre of its thinnest half extent, up to 8 cm. Buildings and thick walls
  keep 5 cm (the wall class's bevel is 7 cm), so the city's back-face rule and the kits' 2 mm layers hold.
- Shading rounds further than the geometry. The rounded-box normal with radius `round` (0.1) per metre of the thinnest half
  extent, from the chamfer up to 30 cm, turns a flat face over its last few centimetres. A tower's edge gets a 30 cm soft
  edge with no vertex more. A facet more than 60 degrees from it (a chip, a stub's break, a step of the wrap) keeps its
  own normal. Where that radius spans under 1 to 3 pixels the facet takes over, so a far edge never shimmers.
- The edge light: on a rounded edge, the key through the ramp (`edgeLight`), the highlight band and the rim colour where
  the edge turns from the eye.
- `FLAG_TAPER` (256) narrows a part 30% toward its top (water tank lids and cones in the city). It is a kit opt-in for roofs,
  caps and finials.

**Texture language** (NativeBlockLit `BlockSurface`, all behind the switch):
- Each look keeps `detail` (0.3 to 0.35) of its per-unit tones, grain and wear noise.
- The material looks (brick to cobble, corrugated) lean 40 to 70% toward their flat base. The pattern stays legible but
  reads as painted.
- Brick is suggested the way a painted background does it: bricks drawn in patches and along the face's edges and foot,
  flat paint between, broad joints in a dark tone of the brick, a few bricks lit.
- Timber paints 22 cm boards whose edges turn the normal (a lit top edge, a dark under edge); corrugation ribs turn it
  too, so the ramp draws them as bands. A window's head shades the pane below it (the frame's depth).
- Past a few centimetres a pixel every pattern, the suggested bricks, the brace, the window shade and the normal turns
  resolve to flat paint (screen-space footprint fades), so courses, boards and ribs never beat into moire.
- Walls darken low and lighten high over the first 18 m of height (`gradient`, in the rest frame so stacked blocks run on).
- A broad soft wash replaces the fine noise (`wash`). The occlusion band replaces the old grime band (`baseShadow` 0).
- The baked bevel and corner shade are dropped (the rounded normal lights the edges).
- The sky's sheen lies on floors and roofs at grazing angles: the painted wet-street gradient.
- Glass, screens, neon, the bank looks, the hazard paint and every telegraph keep their own painting.

**Sky and grade.**
- `NativeNightSky` `AnimeSky` (`skyToon` 1): the zenith eased over the horizon, with a bright band at the horizon
  (`horizonBand`, brighter on the sun's side).
- Cumulus in three layers round the horizon (far, low and hazy to near, high and large; `clouds` the share of cells that
  hold one, `cloudScale` their size): each cloud a few round puffs cut flat at its base, a crisp silhouette one pixel
  soft, two tones turned by the puff's own round normal to the sun (`cloudLit`, `cloudShade`), a soft gradient up from a
  darker base, and a warm rim on the sun's side.
- Atmospheric perspective: the style's `fogColor` and a nearer `fogStart` (the fog's end, which sets the kits' draw range,
  never changes).
- The grade: `NativeBloom`'s composite, after the shoulder. Saturation, log contrast about mid grey, and a split tint. It
  adds no pass. The low tier has no bloom and therefore no grade.

**Emissive and neon.** Emissive is never stepped or outlined differently. Neon tubes, signs, screens and lit windows stay
HDR for the bloom. Use neon as accents (cyan and magenta signs, red warnings), not as fill.

## 3. Values per pilot (MapLooks `toon`)

- **expedition-city.** The blue hour, close to the reference's dark blue city:
  - key #FFAE82 at 0.95 from 9 degrees, bearing 288; terminator at N.L 0.12;
  - shade tint (0.66, 0.78, 1.36), lift 0.3; ambient sky #5A6FB8 x .85, equator #6E6A9A x .6;
  - sky: horizon #F2A27A, zenith #1E2E5A, horizon band #FFC48A, clouds lit #FFB89A, shade #7A70B0, cover 0.62;
  - fog #7C6E9E from 12 m (end 175 m as before);
  - wrap: copings, broken corners, posts on; plinths and pilasters off;
  - lines #2A1E40; grade saturation 1.16, contrast 1.07.
- **brickfall** (golden-demolition). Golden hour:
  - key #FFD09A at 0.95 from 30 degrees, bearing 322; terminator at N.L 0.3;
  - shade tint (0.6, 0.72, 1.3), lift 0.2; ambient sky #7FA4E0 x .58, equator #9E9CC0 x .32;
  - sky: horizon #FFD8A0, zenith #3E60C8, band #FFC890, clouds lit #FFD2A0, shade #8C7CC8, cover 0.62;
  - fog #C0AEB0 from 20 m;
  - wrap: every feature on;
  - lines #2A1E40; grade saturation 1.14, contrast 1.06; detail 0.35.
- Both: highlight 0.55 past N.H 0.965; occlusion 0.4 over 0.22 m.
- The raw looks (golden-demolition's pinned preset, the city's dusk) are untouched underneath. The style's `key`, `rim`,
  `ambient` and `sky` replace them only while it is on.

## 4. Performance (measured in Core; Unity frame time not measured)

- **Draws and parts.** Unchanged: one mesh in place of one in the same instanced batches. The city's busiest frame is
  still 26 kit draws, and parts are unchanged, because the styled look keeps the fog end that sets the kits' ranges
  (checked).
- **Vertices and triangles.** Per intact block or part:

  | Mesh | Vertices | Triangles | City busiest frame (12,376 parts) |
  | --- | --- | --- | --- |
  | Legacy S0 | 216 | 188 | 2.67 M vertices, 2.33 M triangles |
  | Wrap (style, normal tiers) | 224 | 444 | 2.77 M vertices (+4%), 5.49 M triangles (x2.4) |
  | Rounded box (style, low tier) | 144 | 92 | 1.78 M vertices, 1.14 M triangles |

  The triangle count is the wrap's real cost. Most kit parts are small and need no bands; if Unity profiling on a mid GPU
  shows the triangle setup, stage 2 can draw the town's small parts with the rounded box (a second mesh is a second draw
  per batch).
- **Passes.**
  - One full-screen outline pass, normal tiers only, 9 depth taps a pixel.
  - No new geometry pass: the depth texture already exists there.
  - The grade adds no pass.
  - The anime sky replaces the legacy sky's work in the same pass: three cumulus layers of three cells of four puffs (36
    circle tests a sky pixel, no texture).
- **Shader ALU.** The toon branch adds three SH evaluations, the ramp, the occlusion, the highlight and the rounded normal
  per pixel on dressed blocks; the wrap adds its class and shape per vertex (a few dozen instructions). The legacy branch
  is skipped by a uniform branch. Two more interpolators (the instance axes) on NativeBlockLit.
- **CPU.** The styled looks are made once at load. The outline component does two compares per frame. Nothing allocates
  per frame (checked).

## 5. Verification and limits

- `MapStyleChecks` (`--map-style`) covers:
  - the switch, and the styled look keeping what budgets read;
  - the shaders' constants and formulas against the Core (ramp, shadow ease, occlusion, highlight, wrap classes and
    shapes, outline rings and footprint fade);
  - the legacy paths kept, the ramp maths, the rounded mesh and the shape rules;
  - the wrap: closed and outward for every class at sizes from a rail to a 56 m frontage, tapered or not, never above or
    below the block, at most 11 cm proud, deterministic; Brickfall's own blocks take the right classes; only the wrap
    takes the shader's wrap path;
  - the outline's line rule on synthetic depth (both widths, the footprint fade), zero per-frame allocations, the runtime
    hooks and the data.
- Offline renders run the game's Core dumps and the shaders' own code in WebGL (SwiftShader). They have no MSAA, one shadow
  map 80 m across around the view (Unity uses cascades) and a CPU bloom stand-in. The outline's stencil is emulated by a
  mask, and a stand-in toon character (the low-poly male base) has an inverted-hull outline standing in for BoZo's. A
  harness bug that dropped the cast shadows in the first view of each render job was fixed before the final renders.
- Not run: the Unity Editor and its surface-shader generator, FXC, a Windows build, GPU timing, two clients.
- Specific Unity risks:
  - Stencil testing in a BeforeForwardAlpha command buffer with MSAA on.
  - The depth texture's orientation against the pass's pixel coordinates (the D3D flip).
  - Surface shaders with the custom `ArenaToon` model.
  - The crisp ramp shows shadow-map acne or texel steps more than Lambert did; check the cascades' bias in the Editor.

## 6. Stage 2 plan

### Maps

Each map: write its MapLooks `toon` block (its wrap features too), switch its presentation's block mesh to the style's (the
`Shape`/`TownMesh` pattern: the wrap on the normal tiers, the rounded box on the low tier), mark its source-model scenery
`_ToonEdgeRef` 16, render before and after, and keep its checks' budgets.

Shared work for every map:
- Wrap variants for the damaged stages, so a block's coping and plinth survive its first hit (today the bands vanish at S1
  and the legacy chipped mesh shows).
- True brick toothing (courses stepping in and out at a wall's free ends) needs rows per course; a "toothed end" mesh for
  big brick walls is the next shape lever.
- The city kit: cornices, window frames with depth and rounded awnings as kit parts (today the wrap's copings, the
  window's head shade and the outlines carry them), and the town's small parts on the rounded box if profiling asks.
- Rounded debris cubes (`NativeStructureFx`) and Brickfall's box skyline (`NativeBrickfallScenery`).

- **Factory** (factory-first-shift, dusk):
  - The grating and corrugated looks keep their pattern at 0.5 detail.
  - Hazard paint and stripes stay crisp.
  - Pipes and rails become rods automatically.
  - The hazard kit and the rigs (`NativeFactoryPresentation`, `NativeHazardKitPresentation`) take the rounded mesh.
  - Sodium practicals in stairs. A rusty orange light against teal shade.
- **Cyber City** (neon-rain, night):
  - Closest to the reference.
  - A dark blue city: shade #2A3460, neon cyan, magenta and red as accents.
  - The wet streets take the sheen.
  - The bank looks (glass, holo, tube) keep their painting. Glass curtain walls only lose noise.
  - `NativeCyberCityPresentation`'s metro mesh becomes the rounded one.
  - Lines fade earlier (30 to 90 m) in the rain.
- **Ward Zero** (quarantine, sealed, no sky):
  - Clinical: cool shade, warm troffers.
  - The ceiling light field goes through `ToonPoints` (it already does inside BlockLights).
  - Tiles at 0.4 detail. The specimen tanks stay emissive.
  - Fog only, no sky. Lines 25 to 70 m indoors.
- **Central Station** (expedition-transit, night):
  - Terrazzo and cream tile flatten well. Sodium pools in stairs.
  - The station kit and its boss presentation take the rounded mesh.
  - The actor lighting the station turns on goes through the stairs already.
- **City Ironworks** (expedition-foundry, sealed):
  - Molten light stays HDR and unstepped (`NativeMolten` is its own shader).
  - The block shade keeps the heat glow. Black iron at 0.4 detail.
  - The heat haze stays.

### Main menu and 3D backdrops

The voxel pilot figures in these scenes become BoZo characters later. They are listed in the last item.

- **Main menu, the Pilot's Rooftop ("Pilot's Overlook").** `Runtime/NativeRooftopPresentation.cs`; `NativeRooftop`,
  `NativeRooftopSky`, `NativeRooftopScreen` and `NativeRooftopSprite` shaders. It is already a dark blue cyber city.
  - `NativeRooftop.shader` now includes the toon switch: its spill lights step under it and it marks the outline stencil.
    `NativeLighting.Rooftop` keeps the style off, so it draws exactly as before.
  - Stage 2: give the rooftop its own toon values (written by its presentation), add the cumulus sky to
    `NativeRooftopSky`, and attach the outline to the menu camera.
  - Its seated pilot and runners (`NativeRooftopActors`) are voxel bodies through SourceLit.
- **Chip selection, the draft backdrop.** `Runtime/NativeDraftBackdropPresentation.cs` and `NativeDraftBackdrop.shader`, a
  ray-cast cyber corridor.
  - Stage 2: flat-toned panels, cel bands on its ray-cast lighting, a painted gradient back wall, and lines from its own
    distance field.
  - The draft's pilot and held chip (NativeDraftPresentation) are voxel.
- **PvE chip select in the arena.** `NativeChipSelect.shader` hologram. Keep it as hardlight. It already reads as an
  effect.
- **Lobby and pilot-card portraits, the Locker turntable, the Expeditions screen's party strip.**
  `Runtime/NativePortraits.cs`, `NativePortrait.shader` and `NativePortraitResolve.shader`.
  - Stage 2: a toon podium and backdrop gradient in the portrait shader, and a matching outline in its resolve pass
    (portraits render to their own target, so they need their own pass).
  - The voxel pilot in each portrait is the BoZo swap's.
- **Expeditions screen and stage loading.** The screen is 2D apart from the portraits. The loading screens and transitions
  are 2D IMGUI shutters (`NativeScreenTransitionHud`). They only need colour harmonisation with the new palette.
- **Entrance stages** (SceneMotion intros, `_StageLit` SourceLit). The style is off on stages. Stage 2 decides whether
  intros take a toon tone instead of ACES.
- **Where the voxel pilots are:**
  - the rooftop crowd and seated pilot;
  - portraits (cards, Locker, lobby, Expeditions party strip);
  - the draft stage pilot;
  - entrance stages;
  - the chip-select pose;
  - the killcam (live bodies);
  - all arena pilots.

  All of them go through `NativeBodyModels` and SourceLit, so the BoZo swap replaces them in one place
  (docs/CHARACTER-LOWPOLY.md).

### Character outlines with BoZo

BoZo's Shader Graph material will not write the stencil bit. The outline pass would then line a BoZo silhouette over a wall
drawn first. Two options:
- a stencil-clear redraw of the character renderers in the same command buffer, before the edge draw (one depth-tested
  draw per character);
- setting the stencil on BoZo's materials if its generated shader exposes it.

Match the environment's `lineColor` and width to BoZo's outline settings when the pack is imported.

## 7. Risks

- **Unity untested.** It is untested whether the surface-shader generator accepts `ArenaToon` everywhere SourceLit is used,
  and whether the stencil and depth-texture orientation hold under MSAA at BeforeForwardAlpha. The Unity compile of the C#
  passes, and the HLSL front end (glslang) compiles NativeBlockLit, NativeToonEdge, NativeNightSky, NativeBloom,
  NativeRooftop and the ArenaToon lighting functions. FXC and the Editor are the owner's.
- **Old screenshots and parity.** The legacy look is bit-identical in its code paths while the switch is off. Two hooks
  (the actor and muzzle lights in SourceLit and NativeLit) now pass through `ToonPoints`, which returns its input.
- **Rounded mesh seams.** The rounded edge reads as a seam where two coplanar blocks meet (slab joints, stacked masses).
  This is softer than the legacy corner darkening, but still visible up close. The wrap's bands make stacked storeys read
  as string courses, which suits Brickfall.
- **The wrap stands proud.** Bands and posts stand up to 11 cm past the collider (presentation only): a player can look
  slightly into a coping or a pilaster at contact, and a kit part 2 mm in front of a wall can be covered where a band
  passes. The city turns plinths and pilasters off for its doors and shop fronts.
- **Triangles.** 444 a part on the normal tiers (x2.4 the legacy); see section 4.
- **First hit.** A block's bands disappear when it reaches S1 (legacy chipped mesh). It reads as the coping knocked off,
  but it is a pop.
- **Outline noise.** Very grazing floors near the fade start can draw stray crease lines. The plane test, the footprint fade
  and the distance fade limit this. Tune `crease` and the fade per map.
- **Brickfall's identity.** The hotel's panels and the frontage are still big masses; the wrap gives them plinths,
  copings, capitals and pilasters, and the brickwork is suggested, but the massing is the authority's.
