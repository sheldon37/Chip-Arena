# The anime map style

Owner direction (2026-10-21): "rework the expedition maps to be more in-tune with the BoZo anime pack. We're moving away
from voxel now. The PVP maps: Factory, Brickfall, and Cyber City as well." The player characters move to the licensed BoZo
Stylized Modular Characters, Anime Pack (toon ramp, toon outlines, stylized proportions). That pack is never committed and
nothing here depends on it.

Owner review of the stage 1 pilots (2026-10-22): "keep it cool, merge it and start phase 2". The cool, saturated shade
palette and cool ground stay the style for every map; stage 1 is merged into shared.

Stage 1 (Claude, `claude/anime-map-style`): this document's audit and rules, the shared style foundation, and two pilot
maps, the Expedition city hub and Brickfall. Stage 2 (section 6): the remaining maps and the menu backdrops on the same
foundation. Stage 2 for the menus and 3D backdrops (Claude, `claude/anime-menu-backdrops`, merged into shared
2026-10-22): the rooftop, the draft corridor, the portraits and the 2D covers' palette (section 6, "Main menu and 3D
backdrops"). Stage 2 milestone 1 for the maps (Claude, `claude/anime-map-style-2`) adds the shared fixes (damaged and
toothed wraps, the city kit's shapes, bevelled debris, the rounder Brickfall skyline, the town's rounded small parts) and
the Factory and Cyber City; Ward Zero, Central Station and the Ironworks follow.

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
  (`points` 0.7 blends them). `pointCool` (stage 2, 0 by default; `_ArenaToonCool`) then turns the dim rings toward the
  shade tint's hue at their own luminance (fully as the light fades out, not at all from the stepped luminance
  `pointCoolLevel` up): a furnace's light warm near the heat and steel blue away from it (the Ironworks: 1, from 0.35).
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
  an octant, 144 vertices and 92 triangles against the legacy S0's 216 and 188). On the low tier the damaged stages keep
  the legacy meshes.
- **Damaged stages** (stage 2; `NativeToonMesh.WrapMesh` 1 to 4, ToonWrap's `stage` from uv0.w): on the normal tiers every
  stage draws a wrap, so a block's coping and plinth survive its first hit. S1 breaks two top corners and one plinth
  corner; S2 breaks all four top corners deeper and two plinth corners and pinches a fresh waist band a third of the way
  up and down (blocks too short for a waist draw S2 without it); the S3 stub keeps its plinth, loses its coping and has a
  ragged top over the cut. A break drops a corner by up to 35 cm and steps it in by up to 10 cm (the stub's rag up to
  12 cm), baked per vertex as shares (COLOR r and g) so the shader only scales them; fresh breaks are painted on the
  break's own rows only. The same chunks, telegraphs and stage thresholds as before: a stage still changes at the same
  health, and only its mesh is new.
- **Toothed ends** (stage 2; `NativeToonMesh.ToothedMesh`, `FLAG_TOOTH_POS` 512 and `FLAG_TOOTH_NEG` 1024): a big brick
  wall (at least 40 cm thick, 1 m long, twice as long as thick and 2.4 m tall) whose end along its longer axis is free
  steps every other 36 cm course in by up to 18 cm there, like brickwork left by a demolition. Free means no other block
  holds a point 6 cm past the end (at mid height and a third up and down) or 6 cm off either long face 25 cm from it, so
  a junction never teeth. `NativeMapStyle.ToothEnds` finds them once per styled map (a few ms in Core for the biggest
  map); the dressing draws them as one more batch list. The two rows of each course edge sit 3 mm apart, so the steps
  never meet in a T.
- **Kit shapes** (stage 2; opt-in flags a kit part carries, read by `ToonKitShape` and `ToonWrapShape`): `FLAG_RAKE` (8192)
  draws a pediment slab in toward its top at the pediment's slope (0.33: the Exchange's 0.55 m steps over a sixth of its
  10.3 m half span, so the stepped tympanum reads as one raking line with stone courses); `FLAG_AWNING` (16384) arches an
  awning strip up to 12 cm at its middle; `FLAG_CORNICE` (4096) gives a cornice a deep moulded underside and a crisp top;
  `FLAG_FRAME` (2048) paints a window pane's frame (7 cm) with a shade under its head. The city kit flags its pediment,
  awnings, cornices and window panes.
- **The town's small parts** (stage 2; `NativeMapStyle.NeedsWrap`): a kit part with no wrap class and no shape the wrap's
  rows carry (cornice, awning, rake) draws the same chamfered box on the rounded mesh. The town draws the parts that need
  the wrap first, so the batches holding them take the wrap and the rest the rounded box: the same batches and draws.
- **Debris** (stage 2; `NativeMapStyle.BevelFacet`): with the style on, the destruction kit's cubes are bevelled (faces,
  45-degree edge strips and corner facets, the bevel 0.3 of the thinnest half extent up to 5 cm), lit by the ramp.
- **Brickfall's skyline** (stage 2; `NativeBrickfallScenery.Build(anime)`): round water tanks (a 16-sided barrel, round
  hoops, a smooth cone in six steps) and octagonal chimney stacks with a cap and a round pot, chosen once by the brickfall
  look's style switch. The legacy skyline is built exactly as before (no hash is drawn differently).

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
- The material looks (terracotta to cobble, corrugated; brick paints its own) lean 40 to 70% toward their flat base. The pattern stays legible but
  reads as painted.
- Brick is suggested the way a painted background does it: bricks drawn in patches (about 60% of the face) and along the
  face's edges and foot, flat paint between, broad joints in a dark tone of the brick, a few bricks lit. Brick resolves to
  flat paint only from 2 to 4.5 cm a pixel (a big wall at combat range keeps its bricks) and the material-look rule below
  does not flatten it again. The patches show in shade too, and the shade carries a sky-bounce value gradient (the ambient about 0.93 at a wall's foot to 1.14 from
  12 m up, with `gradient` 0.14), so a big wall in shade reads less as one flat colour
  (not solved everywhere: section 7).
- Timber paints 22 cm boards whose edges turn the normal (a lit top edge, a dark under edge); corrugation ribs turn it
  too, so the ramp draws them as bands. A window's head shades the pane below it (the frame's depth).
- Past a few centimetres a pixel every pattern, the suggested bricks, the brace, the window shade and the normal turns
  resolve to flat paint (screen-space footprint fades), so courses, boards and ribs never beat into moire.
- Walls darken low and lighten high over the first 18 m of height (`gradient`, in the rest frame so stacked blocks run on).
- A broad soft wash replaces the fine noise (`wash`). The occlusion band replaces the old grime band (`baseShadow` 0).
- The baked bevel and corner shade are dropped (the rounded normal lights the edges).
- The sky's sheen lies on floors and roofs at grazing angles: the painted wet-street gradient.
- Glass, screens, neon, the bank looks, the hazard paint and every telegraph keep their own painting.
- `shadeFloor` (stage 2, 0 by default; `_ArenaToonFloor`): an albedo floor in the style, tinted by the shade tint, for a
  night map whose near-black paint loses its form (Cyber City 0.05).
- `brickCourses` (stage 2, 0 by default; `_ArenaToonFloor.y`): the share of every brick face drawn in the toon courses
  (stage 1 draws them in patches along the edges and the foot), for a map whose brick walls are its subject (the
  Ironworks' furnace walls, 1). The same dark joints and footprint fade. Cyber City's glass
  curtain walls, holo screens and tubes take neither the height gradient nor the wash: they only lose fine noise.

**Sky and grade.**
- `NativeNightSky` `AnimeSky` (`skyToon` 1): the zenith eased over the horizon, with a bright band at the horizon
  (`horizonBand`, brighter on the sun's side).
- Cumulus in three layers round the horizon (far, low and hazy to near, high and large; `clouds` the share of cells that
  hold one, `cloudScale` their size): each cloud a few round puffs cut flat at its base, a crisp silhouette one pixel
  soft, two tones turned by the puff's own round normal to the sun (`cloudLit`, `cloudShade`), a soft gradient up from a
  darker base, and a warm rim on the sun's side.
- Atmospheric perspective: the style's `fogColor` and a nearer `fogStart` (the fog's end, which sets the kits' draw range,
  never changes). `skyFog` (stage 2, 0 by default) blends the anime sky's horizon into the fog colour over twice the
  band's height (`_SkyToonFog`), so a fogged skyline never meets a bright horizon band in a hard line (the Factory, 1).
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
- **factory** (factory-first-shift, stage 2). Dusk through the bays and the skylights, a rusty orange key against teal
  shade:
  - key #FF9C60 at 1.05 from 36 degrees, bearing 150 (the map's own); terminator at N.L 0.3;
  - shade tint (0.5, 0.95, 0.95) (teal: green as strong as blue), lift 0.25; lit (1.12, 0.98, 0.84); rim #6CC4CC;
    ambient sky #58A8A6 x .62, equator #6E8C8A x .36; shadow grade (0.9, 1.02, 1.0);
  - sky: horizon #F2A272, zenith #2C5670, band #FFB070, clouds lit #FFB47E, shade #5E7E96, cover 0.55; the horizon
    blends into the fog (`skyFog` 1);
  - fog #7E96A0 from 30 m; haze #8FA2A6;
  - detail 0.5 (the grating and corrugation keep their pattern); the hazard paint and stripes keep theirs;
  - wrap: plinths, copings, posts; no broken corners (a working factory);
  - the sodium practicals (MapLooks lights) step into stairs like every point light; pipes and rails are rods by the
    chamfer rule; the kit, the rigs and the hazard kit draw the rounded box; the scenery is marked for the outline.
- **cybercity** (neon-rain, stage 2). Night, the closest map to the reference: a dark blue city with neon accents:
  - key: the moon, #8EA4FF at 0.6 from 54 degrees, bearing 315 (the map's own); rim #FF50D8 at 0.45;
  - shade tint (0.56, 0.7, 1.4), lift 0.35; lit (1.0, 1.0, 1.04); edge light 0.6; ambient sky #3A4E9C x .86, equator
    #4A4884 x .6; an albedo floor (`shadeFloor` 0.05, tinted by the shade tint) so the near-black vans and booths hold
    their form;
  - sky (night): horizon #3A2A70, zenith #0A1030, band #8A4AB0, clouds lit #7A62C0, shade #262C58, cover 0.5;
  - fog #2A3460 (the reference's shade) from 18 m; background #141A38;
  - lines #1A1438, fading 30 to 90 m in the rain; grade saturation 1.16, contrast 1.08;
  - the wet streets take the sky's sheen; the bank's looks keep their painting; the metro dressing, its backdrop and the
    train draw the rounded box; copings and posts, no plinths or broken corners.
- **quarantine** (Ward Zero: the arena and the Expedition instance share the look; stage 2). Sealed, clinical:
  - no sky (the anime sky off), fog #04080A from 6 m (the look's), the look's dim key and rim;
  - shade tint (0.62, 0.8, 1.3), lift 0.3; lit (1.08, 1.0, 0.88); a cool ambient floor (sky #6A8CB0 x .09, equator
    #4E6A82 x .07); the ceiling light field and the practicals step into stairs through `ToonPoints` (points 0.75);
  - tiles at 0.4 detail; the specimen tanks stay emissive; lines #1E1A2E, 25 to 70 m indoors;
  - the Ward kit draws the rounded box.
- **central-station** (Expedition Central Station, stage 2). Night under the broken glass roof:
  - the moon key #8FAEDC at 0.5 from 62 degrees (the map's own); the anime night sky (horizon #1A2A48, zenith #050818,
    clouds lit #4A5A8C, shade #161C34, band #34406E, cover 0.45);
  - shade tint (0.58, 0.72, 1.36), lift 0.25; lit (1.0, 1.0, 1.04); ambient sky #2A3E78 x .12; fog #0E1A24 from 12 m;
  - terrazzo and the cream tile flattened (flatten 0.4, detail 0.35); the sodium pools step into stairs (points 0.75);
  - lines #14122A, 30 to 100 m; the station kit draws the rounded box. The boss (THE 6:47) is a SourceLit model: it takes
    the ArenaToon lighting and, as an actor, no environment outline.
- **ironworks** (Expedition City Ironworks, stage 2). Sealed, the furnaces lit:
  - no sky, a cool fog #182028 from 14 m (the look's is #221A16), the look's dim key;
  - warm furnace light against steel-blue shade: the furnace field and the lamps step into stairs (points 0.6) and their
    dim rings turn to the shade tint's hue (`pointCool` 1 from 0.35), so the light is warm near the heat and cool away
    from it; shade tint (0.62, 0.76, 1.3), lift 0.3, a cool fill (sky #4A6AA0 x .3, equator #4E5468 x .18); lit (1.1,
    1.0, 0.88); grade saturation 1.12, contrast 1.0 (no darkening: the hall is no darker than the legacy);
  - the furnace walls' brick in courses over the whole face (`brickCourses` 1); black iron and the ingot moulds at 0.4
    detail; the molten metal stays `NativeMolten` (HDR, unstepped); the heat haze stays;
  - lines #1E1418, 25 to 90 m; the Ironworks kit draws the rounded box.
- All: highlight 0.55 (0.5 on the stage 2 interiors) past N.H 0.965; occlusion 0.4 (0.45) over 0.22 m.
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

  The triangle count is the wrap's real cost. Stage 2 draws the town's parts that need no wrap on the rounded box
  (`NativeMapStyle.NeedsWrap`: 22,548 of the town's 23,186 parts; 608 of the busiest frame's 12,376 need it). Measured in
  Core from the busiest eye, with the parts that need the wrap drawn first so a batch straddling the two takes the wrap:

  | City busiest frame (12,376 parts, 26 kit draws) | Vertices | Triangles |
  | --- | --- | --- |
  | Stage 1: every part on the wrap | 2.77 M | 5.49 M |
  | Stage 2: the split (1,000 parts on the wrap, 11,376 on the rounded box) | 1.86 M (-33%) | 1.49 M (-73%) |
  | Separate lists, for comparison (not used: 27 draws) | 1.83 M | 1.35 M |

  The draws stay at 26 (the same 500-part batches in a different order). The shadow casters (577) save 5% of their
  vertices (most of them need the wrap). GPU time is not measured: the gain is in vertex and triangle setup, which matters
  most on the low and mid GPUs.
- **Stage 2's other costs.** The damaged wraps are 224 to 280 vertices (S2's waist adds two rows); the toothed wrap 896
  vertices on at most a few dozen walls a map (one batch list more: one draw while any is drawn). `ToothEnds` runs once per
  styled map (0.5 to 4.4 ms in Core). The bevelled debris cube is 26 facets (44 triangles against 12) on at most the
  kit's few hundred cubes, built without allocating. The Factory, Cyber City and hazard kits swap the legacy S0 (216
  vertices, 188 triangles) for the rounded box (144, 92).
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
- Stage 2 adds to `MapStyleChecks`: the damaged and toothed wraps closed and outward at sizes from a rail to a frontage,
  copings and plinths kept through S2 and the stub's coping gone, the toothed courses stepping in at the flagged end only,
  `ToothEnds` on a wall that meets another and two free walls, the bevelled debris cube closed and wound outward, the kit
  shapes (rake, awning, cornice) and `NeedsWrap`, a part without a wrap class having the same extents on both meshes, the
  shader's stage 2 constants and lines against the Core, the runtime hooks, the per-map values and the allocation probe.
- The stage 1 "jagged shadow edges on the city lane wall": a harness artifact, not shadow-map aliasing. That view's camera
  and pilots stood 0.6 m inside the Exchange's block (x 49 to 61.5, z -16 to 13), so the "wall" was the back of the
  Exchange's front dressing seen from inside it, where a wainscot panel's and a door leaf's back faces lie in one plane and
  z-fight in vertical stripes. The pattern was the same with the key's shadows off and at four times the shadow map's
  density. Stage 2 moves the view onto the lane between the Exchange and the depot; the shadow edges there are clean.
- Specific Unity risks:
  - Stencil testing in a BeforeForwardAlpha command buffer with MSAA on.
  - The depth texture's orientation against the pass's pixel coordinates (the D3D flip).
  - Surface shaders with the custom `ArenaToon` model.
  - The crisp ramp shows shadow-map acne or texel steps more than Lambert did; check the cascades' bias in the Editor.
    What the Editor must confirm: on Brickfall's and the city's walls at combat height, with the shadow cascades as
    shipped, no acne bands on lit faces turning past the terminator and no stepped edges on cast shadows across walls; if
    either shows, raise the light's normal bias (not the depth bias, which detaches the shadows at the feet) before
    softening `ToonShadow`'s ease. The offline harness has one 4096 map 80 m across and cannot show the cascades' seams.

## 6. Stage 2 plan

Status: milestone 1 (the shared work, the Factory and Cyber City) and milestone 2 (Ward Zero, Central Station, the
Ironworks, the milestone 1 review's polish and the Ironworks fix) are merged into shared (2026-10-22).

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

Stage 2 for the menus (Claude, `claude/anime-menu-backdrops`, merged into shared 2026-10-22). Presentation only, behind the
same switch: `--ca-map-style legacy` draws every piece below in its legacy look exactly (offline, the after tree with
the switch off renders pixel-identical to the base). Core `NativeMenuStyle.cs` holds every value and the CPU copy of
every new formula; `Tests/MenuStyleChecks.cs` (`--menu-style`, in the full run) holds the shaders to it. The voxel pilot
figures in these scenes stay as they are (NativeBodyModels, SourceLit): they become BoZo characters later (the last
item).

- **Main menu, the Pilot's Rooftop ("Pilot's Overlook").** A dark blue anime cyber city at night with neon accents.
  - Its own toon values (`NativeMenuStyle.Rooftop`, a look with only a `toon` block): `NativeLighting.Rooftop` writes
    them through `StyleGlobals` each frame it shows, as `Apply` writes a map's, instead of `StyleOff`. So the seated
    pilot and the crowd take the ramp as pilots do on the anime maps (the night's magenta key through the ramp, its cyan
    rim in stairs), the grade is the menu's (saturation 1.12, contrast 1.05) and `NativeToonOutline` (already on
    `cameraView`, the menu camera) draws its pass from 60 to 320 m. The look overrides no light, ambient, sky or fog:
    the rooftop's night stays its own.
  - `Resources/NativeRooftopToon.cginc` (new, included by `NativeRooftopCommon`): `RoofSkyBase`, `RoofHaze` and
    `RoofFog` branch to it while `_ArenaToon.x` is on. A painted night gradient (zenith #070C24 to a blue horizon) with
    a crisp band of city glow swinging magenta to cyan round the horizon; the haze in four soft depth planes
    (`_RoofToonFog`); the street's violet glow low down.
  - `NativeRooftop.shader` (`RoofToonSurface`, modes 0, 1 and 4; **held since 2026-10-24**: this pass crashed the
    Windows Editor's shader compiler, so the world draws its legacy surfaces in both looks and the code below lives on
    `claude/rooftop-bisect` until the cause is found, DEV_LOG): towers in two flat tones of the cool shade (faces
    turned to a cool key and the rest) and a third on roofs, a painted glow up from the street; windows lit in runs of
    six along whole floors with two brightness levels (painted strips, not per-window noise; far, the bay average as
    before; the tower seed is rounded so an interpolated constant cannot speckle a window); props lifted out of
    near-black and compressed into a few flat values under two ambient bands, the spill in the style's stairs, a crisp
    wet sheen; puddles a crisp mirror band of the toon sky with crisp rings. Neon, cars and the screens' ads keep their
    light. A second, depth-only shadow caster pass puts the city into the camera depth texture the outline reads
    (nothing casts a shadow: no light on the rooftop does).
  - `NativeRooftopSky.shader`: crisp stars and three cumulus layers lit from below by the city (violet undersides, navy
    tops, a bright underside rim), drifting slowly, in place of the fbm deck; the two searchlights sweep in both looks.
    Values from the look's sky fields, set once on the rooftop's sky material by `NativeRooftopScene`.
  - `NativeRooftopScreen`: flatter scanlines and a crisp edge (the roll and glitch kept). `NativeRooftopSprite`: the
    mist and steam take crisp, flat-toned edges; every motion kept. The animation, the camera loop, the cast and the UI
    are untouched.
  - The low tier: no outline, no bloom and so no grade (as on the maps); the rest of the style draws.
- **Chip selection, the draft backdrop.** `NativeDraftBackdrop.shader`, behind its own switch `_BackToon` (set once with
  the material by `NativeDraftBackdropPresentation` from `NativeMenuStyle.DraftToon`):
  - flat-toned panels (two tones stepping with height, a third on the floor) lit by the far glow in cel bands down the
    corridor, in a soft vertical gradient: lit low, easing into the legacy navy by eye height (0.7 to 1.5 m), so the
    text-safe band's brightness cap never cuts a shape out of them;
  - the light's bands and the haze's soft bands ease into smooth gradients down the corridor (from fog 0.3 to 0.6, about
    11 to 29 m), so no contour closes behind the pilot's head; the far end is the legacy haze; the pilot's glow in soft
    bands;
  - no back wall: a painted wall 52 m behind the pilot read as a lit rectangle behind the head (owner review, 2026-10-22)
    and was removed;
  - ink from the corridor's own distance field: the floor-wall crease (by the hit distances and their screen derivatives)
    and the panel seams and ribs, which turn from cyan light to ink;
  - every neon element (trims, rails, packets, gates, glyph streams, motes) keeps its light; the text-safe bands and the
    brightness cap apply after the toon terms, as before.
- **PvE chip select in the arena.** `NativeChipSelect.shader` hologram: unchanged (it reads as an effect).
- **Lobby and pilot-card portraits, the Locker turntable, the Expeditions screen's party strip.** `NativePortraits.cs`:
  - a toon podium (`NativePortrait.shader` mode 3, display tones, not tone mapped): a dark navy top in two bands on the
    key, a crisp rim band in the backdrop's tint, the side over a darker foot;
  - the resolve pass (`NativePortraitResolve.shader`, `_PortraitToon`) draws the maps' line (#2A1E40, one pixel per 1080
    screen lines) from the portrait's own coverage, then composites a translucent, banded gradient under the pilot from
    the podium's row up 1.1 m, the cool shade #2A3460 taking the backdrop's tint toward its foot;
  - the backdrop colour picker keeps working: the card still draws the picked backdrop under the portrait, and its tint
    (`PortraitLook.Back`, a render input outside `Same()`, the accent on Auto) colours the podium's rim and the
    gradient.
- **Expeditions screen, loading screens and transitions.** Colour harmonisation only: the full-screen covers take the
  night navy #0E1430 and the dusk #1C2654 (`NativeCyberTheme.Night`, `Dusk`) instead of the void #05070F and navy
  #0A1024: the transition shutters' body and wash (and so the loading screen's base), the reduced loading cover, the
  Expeditions screen's veil and two card bodies. Cyan edges, magenta ticks and text unchanged; Void and Navy again under
  the legacy switch.
- **Entrance stages** (SceneMotion intros, `_StageLit` SourceLit). Rendered offline: hero-landing, grid-rez,
  orbital-drop and overclock, ACES (today's StageTone) against a candidate toon tone (the same exposure, a hue-keeping
  shoulder, luminance in three soft stairs, the cool shade tint low down). The candidate keeps the accents and a
  coloured key more saturated, but on the voxel bodies the stairs barely read (their faces are already flat) and nothing
  is clearly better; the tone lives in SourceLit, which stage 2 leaves alone. Recommendation: keep ACES on the voxel
  bodies; revisit with the BoZo characters, whose own material sets their tone (a hue-keeping tone suits them), behind a
  switch then.
- **Where the voxel pilots are:**
  - the rooftop crowd and seated pilot;
  - portraits (cards, Locker, lobby, Expeditions party strip);
  - the draft stage pilot;
  - entrance stages;
  - the chip-select pose;
  - the killcam (live bodies);
  - all arena pilots.

  All of them go through `NativeBodyModels` and SourceLit, so the BoZo swap replaces them in one place
  (docs/CHARACTER-LOWPOLY.md). On the rooftop they take the style's ramp (as on the anime maps); in the portraits and
  the draft their shading is unchanged.
- **Menu risks (Unity untested):** the rooftop city in the camera depth texture through its new shadow caster pass
  (Unity draws the depth texture with that pass; the renderer's cast-shadows flag is Off); the stencil and depth texture
  at BeforeForwardAlpha under MSAA, as on the maps; the portrait gradient's row against the resolve's uv on D3D (both
  are bottom-up for a render texture); the pilots' ramp under the rooftop's magenta key; FXC on the new branches (the
  HLSL front end compiles every changed shader).

### Character outlines with BoZo

BoZo's Shader Graph material will not write the stencil bit. The outline pass would then line a BoZo silhouette over a wall
drawn first. Two options:
- a stencil-clear redraw of the character renderers in the same command buffer, before the edge draw (one depth-tested
  draw per character);
- setting the stencil on BoZo's materials if its generated shader exposes it.

Done in BoZo step 4: the character shader clears the bit (`claude/bozo-toon`), and its outline takes the map's width and
`lineColor` rule (`claude/bozo-outline`; docs/BOZO-INTEGRATION.md).

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
- **Triangles.** 444 a part on the wrap (x2.4 the legacy); the town draws its small parts on the rounded box (section 4).
- **First hit.** Stage 2 keeps the bands through S1 and S2 (the wrap variants); the stub still loses its coping at S3.
  The low tier keeps the legacy chipped meshes.
- **Big brick walls in deep shade** read mostly as one maroon at combat range where the brick patches thin out
  (Brickfall's crate view). Milestone 2 raised Brickfall's broad wash (0.16); a light falloff across a face is the next
  lever if it is still too flat.
- **The Exchange's cupola** is four turned boxes per tier (the city kit's `Round`), so its crown reads as a stepped star
  behind the pediment. A true dome needs a round mesh (a new draw) or new kit geometry (the legacy look and the city's
  part budgets); left as is.
- **The Ironworks' cool shade comes from its point lights.** The hall is lit almost only by the furnace field and the
  lamps, so a cool ambient alone never showed; the dim rings of those lights turn steel blue instead (`pointCool`). The
  furnace deck stays warm (it is next to the heat) and reads about a tenth darker than the legacy there (the toon
  courses' dark joints against the legacy's light mortar); the hero view is as bright as the legacy. The ingot moulds'
  rust flattens at play distance (0.4 detail, kept on purpose).
- **Ward Zero's look is shared.** The quarantine look styles both the Ward Zero arena (`siren`) and the Expedition
  instance. Its troffers' colour comes from the ward light field (Core, by mood), not from the style.
- **Toothed ends** are found from the blocks as placed at load. A wall freed later by a neighbour's destruction keeps a
  clean end (no re-scan per frame).
- **Outline noise.** Very grazing floors near the fade start can draw stray crease lines. The plane test, the footprint fade
  and the distance fade limit this. Tune `crease` and the fade per map.
- **Brickfall's identity.** The hotel's panels and the frontage are still big masses; the wrap gives them plinths,
  copings, capitals and pilasters, and the brickwork is suggested, but the massing is the authority's.
