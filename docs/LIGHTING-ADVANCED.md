# Advanced lighting (light volume, volumetric beams, fog scattering, weather)

Owner (2026-10-28): "I want to have advanced lighting and much better environment + stage development", good with the
implementation of the Volumetric Light Beam, UniStorm / Weather Maker and Bakery packs, "as long as it produces proper glow +
lighting".

Those packs run on their own scripts and shaders, which cannot ship in this public repository, and the game's maps are built at
run time from JSON (`UnityMaps.json`), which an Editor bake cannot reach. So the game has its own equivalents, written in its
own shaders and Core code, with the packs read only as technique references (nothing of theirs ships or loads):

| Part | Pack it stands in for | Where |
|---|---|---|
| Light volume: ambient occlusion, sky share and one coloured bounce, baked per map at run time | Bakery (lightmaps, light probes) | `Core/NativeLightVolume.cs`, `Resources/NativeAdvancedLight.cginc` |
| Volumetric beams in place of the billboard shafts | Volumetric Light Beam | `Core/NativeLightBeams.cs`, `Resources/NativeVolumetricBeam.shader`, `Runtime/NativeVolumetricBeams.cs` |
| Height fog with the key's and the practical lights' scattering | (the beams' and the weather packs' fog) | `Resources/NativeAdvancedLight.cginc` |
| Rain, drizzle, snow, wet faces, puddles with rain rings, lightning | UniStorm / Weather Maker | `Core/NativeWeather.cs`, `Resources/NativeAdvancedLight.cginc`, `NativeNightSky.shader` |
| A Balanced (medium) graphics tier | | `Core/NativeAdvancedLighting.cs` (`NativeGraphicsTiers`), `Runtime/NativeSettings.cs` |

Every part is opt-in per look (MapLooks.json `advanced`, below) and off on the low tier. With a part off, every global it reads
is 0 and every shader function returns its input unchanged, so the old maps, a look without the block, the low tier, the
entrance stages, the chip draft and the rooftop draw exactly as before. Presentation only: nothing here touches simulation,
rules, networking, saves, hitboxes or timing.

## The tiers

Settings · Graphics: Auto, High, Balanced (new), Performance. Core `NativeGraphicsTiers.Pick`:

- **Performance** is the low tier, unchanged (no shadows, no MSAA, 3 pixel lights, no bloom, grade or outline).
- **Auto** keeps today's rule for the low tier (under 2 GB of video memory or below shader model 4.5), then picks Balanced
  under 6 GB and High from 6 GB.
- **Balanced** and **High** both draw everything the game drew before; only the advanced lighting differs
  (`NativeAdvancedLooks.Resolve`):

| | Low | Balanced | High |
|---|---|---|---|
| Light volume | off (today's ambient) | 1.25 x the look's cell, 28 rays | the look's cell, 40 rays |
| Volumetric beams | off (the billboard shafts) | one dust octave | two dust octaves |
| Fog scattering | off | on | on |
| Weather | off (Cyber City keeps its own rain) | 0.6 x the fall | the full fall |

Reduced effects halves the weather's fall and the lightning, and turns each strike into one soft pulse (no strobe).

A saved choice the game does not know reads as Auto; old saves ("auto", "high", "performance") keep their meaning.

## MapLooks `advanced`

```json
"advanced":{
  "gi":{"on":true,"occlusion":0.6,"bounce":1.0,"floor":0.4,"cell":1.0,"actors":0.8},
  "scatter":{"on":true,"color":11052186,"density":0.014,"height":0.0,"falloff":7.0,"max":0.3,"key":0.35,"phase":0.55,"lights":0.6},
  "beams":{"on":true,"gain":1.0,"dust":0.65,"soft":2.0},
  "weather":{"kind":"","amount":0,"wind":0,"wet":0,"puddles":0,"lightning":0,"interval":0,"lightningColor":0}}
```

- `gi`: `occlusion` how far the baked occlusion darkens the ambient (0..1), never below `floor`; `bounce` the gain on the baked
  bounce; `cell` the cell asked for (m; the bake widens it to stay in budget); `actors` the share bodies take.
- `scatter`: the fog's own `color` (sRGB), `density` per metre at `height` (m) falling off by e every `falloff` m above it, its
  greatest opacity `max`; `key` the key light's share scattered toward the eye with the forward lobe `phase` (Henyey-Greenstein
  g); `lights` the practical lights' glow in the air.
- `beams`: `gain` on the shafts' light, `dust` how much the dust breaks them up, `soft` the soft edge's power.
- `weather`: `kind` "", "rain", "drizzle" or "snow"; `amount` of the kind's full fall; `wind` (m/s along x); `wet` and `puddles`
  for the wet faces; `lightning` the flashes' strength (0 none) about every `interval` s, in `lightningColor`.

Validation (`NativeAdvancedLooks.Validate`, from `NativeMapLooks.Validate`) refuses out-of-range values, an unknown weather
and a fall without a kind. The anime style's styled copy keeps the block (`NativeMapStyle.Styled`).

The looks that have it (the night-* looks of the old Electron maps have none):

| Look | Volume | Scattering | Beams | Weather |
|---|---|---|---|---|
| factory-first-shift (Factory) | yes | warm haze | the skylight sheets | |
| golden-demolition (Brickfall) | yes | golden dust | the sun shafts | |
| neon-rain (Cyber City) | yes | blue rain haze, the neon's glow | the searchlights | heavy rain, wet streets, puddles, lightning |
| quarantine (Ward Zero) | no (its own ceiling light field) | dark haze, the alarms' glow | the high-bays and the Spine | |
| expedition-city | yes | dusk haze | the lamp shafts | a light storm (rain .45, lightning) |
| central-station | yes | sodium haze | moonlight and sodium cones | a light drizzle where no block covers (kept light: the broken vault is kit, which the cover map does not see) |
| ironworks | yes | furnace haze | the roof monitors | |

The town's storm is optional: set its `weather.kind` to "" to turn it off.

## Light volume (the runtime "GI")

`Core/NativeLightVolume.Build`, once per map at rest (the killcam, a round reset and a rematch reuse it):

1. The blocks into a voxel grid at half the cell, each block thickened to at least one voxel so a 20 cm wall or deck still stops
   a ray (each voxel keeps the first block that fills it, for its colour).
2. Per open cell: a shadow ray to the key, one to each practical light in range (a visibility bit each, up to 64 lights: the
   look's 8 and the map presentation's candidates, the town's, the station's and the Ironworks' lamps), and 10 rays up for a
   first sky share (a ray that meets nothing within 40 m sees the sky).
3. Per open cell, 40 rays (28 on Balanced) over a golden-angle sphere turned by the cell's hash: the openness within 3.5 m,
   the sky seen, and at each hit within 14 m the light on that face (the key by its facing and the visibility of the cell in
   front, the lights the same way, the ambient by facing times that cell's sky share) times the block's albedo (its look's
   colour, x 0.8).
4. A 1-2-1 blur over the open cells; then the open values carried into the solid cells (as the Ward field does), so a face
   never reads the inside of its own block.

Texel: rgb the bounce (light the shaders add to their ambient term), a the occlusion: openness x (0.45 + 0.55 x sky share),
about 0.9 in the open and 0.4 to 0.5 under a roof.
Budget: at most 64^3 cells (2 MB of RGBAHalf; the cell widens to fit: 1 m on the arenas, about 1.8 m on the 240 m town).
Deterministic (each cell its own sum; worker threads never change the result). Bake times on this cloud machine (4 shared
CPUs): about 1.9 s for the Factory at 40 rays; the runtime starts it two frames after the map's look is applied (behind the
round's start), uploads the half floats once on the main thread, and keeps the volume off until then.

The shaders (`NativeAdvancedLight.cginc` ArenaGI) read it at the surface moved out half a cell along its normal, fade it out
over the box's last 1.5 cells, and add `ambient x (lerp(1, max(occlusion, floor), strength) - 1) + bounce x gain` to their
own light:

- NativeBlockLit: on its SH ambient (the legacy path) and on the anime style's ambient through its ramp and occlusion band.
- SourceLit, ZombieLit and the visual body (and its outline): as emission on a lit part, at the body share (`actors`), on the
  ambient the lighting model takes (Unity's SH, or the style's flattened SH).

So props and pilots darken under roofs and in corners, and pick up the lamps' colour bouncing off nearby walls and floors,
while the toon ramp keeps its bands. The volume is the map at rest: blocks broken in a round keep their occlusion until the
next map load (the light it shows round a breach is the intact map's).

## Volumetric beams

`Core/NativeBeamList`: the maps' FX builders (Brickfall's sun shafts, the Factory's skylight sheets, Central Station's moonlight
and sodium cones, the town's lamp shafts, Ward Zero's high-bay cones and the Spine, the Ironworks' roof monitors, Cyber City's
searchlights) hand their shafts to the frame's list in place of their billboards while the runtime gives them one (Balanced
and High with the look's beams on); without a list they draw the billboards exactly as before. A beam is a soft cone (or a
flat elliptic column for a slot) from its source to its end, its light at the source (the billboard's colour x opacity x 2.4
x the look's gain).

`Runtime/NativeVolumetricBeams` draws them in one instanced draw of a unit open cone (24 x 10), additive and HDR so the bloom
takes the cores. `NativeVolumetricBeam.shader` per pixel:

- the air the view ray crosses inside the beam: the chord through its local ellipse from where the ray meets the side to
  where it leaves (or the beam's end when looking down it), over the beam's width, to the soft-edge power, so the beam is soft
  at its silhouette and brightest seen end on;
- the fall-off from the source (a soft start over its first 6%) to nothing at its end;
- the scene's depth (where the camera draws its depth texture: every tier above the low one), so it fades into the floor and
  walls it meets instead of cutting them;
- a fade near the camera, and per beam as the camera nears or enters it (`NativeBeamList.Outside`);
- scrolling dust (value noise, two octaves on High), and Unity's fog fading it to nothing.

Technique notes from Volumetric Light Beam (read, not copied): it shades a cone mesh from both sides with a fresnel blended
toward a tangent-plane projection for long beams, boosts the glare when looking along the axis, fades near the camera's near
plane and inside the beam, attenuates between linear and quadratic fall-offs, and breaks the beam with a 3D noise texture.
Ours reaches the same soft edges and axis glare with the chord-thickness term in one pass (no inside pass: the camera fades
it out instead), and uses procedural noise (no texture).

## Fog scattering

`ArenaScatter` (NativeBlockLit before Unity's fog; SourceLit, ZombieLit and the visual body in their finalcolor, an add pass
taking only the fade):

- height fog: density x exp(-(y - height)/falloff), flat below the base, its optical depth along the view ray in closed form,
  its opacity capped at `max`, in the look's `color` plus the key scattered toward the eye (Henyey-Greenstein, `phase`);
- the practical slots' glow in the air: each light's own fall-off (1 - r^2/R^2)^2 integrated along the ray inside its range
  in closed form (a fifth-degree polynomial: `ScatterChord`, checked against a numeric integral in CoreTests), times the fog's
  density there and `lights`, so lamps and neon bloom into the haze round them;
- with the volume on, the fog's own light follows the occlusion at the ray's middle and the key's share what sees the sky there,
  so a roofed hall's air stays dark and the sun's scattering stays outdoors (its shafts are the beams).

Orthographic cameras (the pilot portraits, which stand far from the arena under their own lights) take neither the volume nor
the scattering.

## Weather

`Core/NativeWeatherFx` is Cyber City's rain (NativeCyberCityFx RainFx and SplashFx) made a system: streaks (rain, drizzle) or
soft flakes (snow) wrapped in a box round the eye so the fall follows the camera, drifting with the wind, stopping at the map's
cover (`NativeWeatherCover`: the topmost block over each 1 m column, as Cyber City's roof map) and never too close to the eye;
rain rings out splashes on the tops round the eye. One NativeFxDraw draws it (two draws). On Cyber City the advanced weather
draws the rain and the city's own stands down (`NativeCyberCityFx.ExternalRain`); on the low tier the city keeps its own.

Wet faces (NativeBlockLit only, `_ArenaWet`): only what the rain reaches is wet, at or above the cover's top over it (the
weather hands its cover map to the shaders as a small RHalf height texture; under a roof, an awning or a deck stays dry). The
albedo darkens (more on top faces), puddles fill the low spots of a noise on up-facing faces, each 0.6 m puddle cell rings out at
its own phase while it rains, and a sheen mirrors the sky and the fog's glow (Fresnel) with the key's and the practical lights'
glints, sharp across and long along the reflected ray, so lamps and neon streak toward the eye across wet streets.

Lightning (`NativeWeatherFx.Lightning`): in about three of four `interval`-second slots a strike of two or three pulses within
half a second (Reduced effects: one soft pulse). The runtime adds the flash to what sees the sky (the volume's open share) and
to the night sky's clouds (`NativeNightSky` fragFlash, 0 unchanged).

## Bakery: why an Editor bake cannot apply here

Bakery (like Unity's own lightmapper) bakes lightmaps and light probes in the Editor for the renderers of a saved scene: it
needs the geometry as scene objects with lightmap UVs at bake time, writes per-renderer lightmap textures and indices into the
scene and a LightingData asset, and Unity applies them only to those renderers when that scene loads. This game builds every
map at run time from `UnityMaps.json` (instanced NativeBlockLit draws of a shared unit mesh, posed per frame; kits and props
from Core), and its blocks break, so:

- there is no scene geometry to bake against, and no per-renderer lightmap to apply (instanced draws share one mesh, and
  NativeBlockLit compiles `nolightmap`);
- a bake would be stale the moment a block breaks or a kit's parts move;
- light probes baked for a scene would have no scene to load with.

What it would take if a map ever became a baked scene: an Editor tool that instantiates the map's blocks and kits as static
renderers with lightmap UVs (a per-block unwrapped mesh instead of the shared lattice), Bakery's bake (lightmaps plus light
probes, or its volume textures), NativeBlockLit with the lightmap variants back (drop `nolightmap`, keep the builds' keyword
list), the breakable blocks left out of the bake (dynamic, lit by probes), and the scene loaded in place of the runtime build.
The light volume above gives most of what that bake would (occlusion, bounce, coloured light from lamps) without any of it,
and survives a map edit, a new map and the public source build.

## Fallbacks

- The low tier, a look without `advanced`, the old maps, a stage, the draft and the rooftop: every global 0
  (`NativeAdvancedLighting.Off`, from NativeLighting.StyleOff and Rooftop; Apply turns the map's back on).
- No 3D half textures, or a failed bake: the volume stays off (a warning in the log).
- No beam shader or no instancing: the FX builders get no list and keep their billboards.
- No depth texture: the beams cut the scene as billboards do (no soft fade).

## Checks

`CoreTests --advanced-lighting` (also in the full run): the looks' blocks and their validation, the tiers, the volume (a
roofed room occluded, a lamp's red bounce off a red wall, the key's bounce, determinism, the budget on the real maps, the half
floats against .NET's), the beams (the camera inside, the unit mesh's winding, the Factory's and Brickfall's hand-over, the
low tier), the weather (cover, determinism, amounts, kinds, allocation free, the lightning's rhythm and its reduced pulse), the
glow integral, and source pins on the shaders and the runtime; then the lighting pins it can touch (MapLookChecks,
BlockLookChecks, MapStyleChecks, WardLightingChecks, ZombieLightChecks, ArmorLightChecks, BloomChecks, FactoryChecks,
BrickfallSceneryChecks, CyberCityChecks).

## Astra handoff

None needed: nothing here changes simulation, rules, networking, saves or authority. The weather is presentation only (no
footing, visibility or audio rules); if rain should ever change footing or sound, that is a rules change for Astra.
