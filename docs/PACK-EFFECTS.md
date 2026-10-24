# Effects from the owner's asset packs

Owner (2026-10-24): "Implement whatever we have on asset packs that would work good on this project, make them fit."

The game draws its combat effects in its own code-driven effects renderer, not with Unity particle systems. The packs'
effects therefore come in through the owner's local bake, like the visual body (`docs/BOZO-INTEGRATION.md`):

- the bake converts a pack's prefab to the engine's own built-in particle shaders, so no pack shader or script ships;
- the result ships in the licensed-assets zip, which the builder installs;
- a presentation-only player draws it from the game's replicated events.

Without the bake nothing extra draws: CI, cloud builds, other checkouts and the public source zips stay as before.

## Surface impacts (2026-10-24, `claude/surface-impacts`)

A gun's plain round that hits a prop now does two things:

- It kicks up the surface's own debris: stone dust and chips, metal sparks, or wood splinters and dust.
- It leaves a bullet hole that fades.

Before, a surface hit drew only the weapon-coloured spark burst (`NativeImpactCues`), which stays.

- **Source:** kripto289's Impacts and Muzzle Flashes. `ConcreteImpact` is used for stone, `MetalImpact` for metal and
  `WoodImpact` for wood, each with its bullet-hole texture.
- **The bake** (local tool `bozo-impacts.request`; the body bake runs it too) adds `VisualImpacts` to the visual body's
  prefab. For each kind it holds:
  - `Effect`: the pack's particle systems without their decal, scripts or lights.
    - The pack's flipbook dust becomes `Legacy Shaders/Particles/Alpha Blended`, and its sparks become
      `Legacy Shaders/Particles/Additive`. Those shaders draw twice the tint, so the tints are halved.
    - Debris stays on `Standard`.
    - Distortion layers are dropped.
    - Textures are at most 1024: the 4096 dust sheet becomes a 1024 copy.
    - The whole effect is scaled to 0.6 (hierarchy scaling), and debris lives at most 1.6 s.
  - `Decal`: a 0.14 m quad with the pack's bullet hole, alpha blended.
- **Kinds** (`Core/NativeSurfaceImpacts`, tested): `metal` and `wood` prop materials by name, every other surface stone, as
  the impact sound already does (`NativeEventSounds`).
- **What shows:**
  - Only plain pistol, rifle and machine-gun rounds. A round with a `Detail` belongs to a special or a kit, which draws its
    own; melee and magic draw their own.
  - Impact events older than 0.3 s draw nothing, and a new arena's earlier events never play.
- **Runtime** (`Runtime/NativeSurfaceImpactPresentation.cs`, from `WeaponFx` each frame):
  - The effect plays 2 cm out of the surface along its normal, spun at random.
  - At most 6 of a kind play at once (3 with reduced effects), the oldest replaced.
  - At most 48 holes show, 4 mm off the surface, each for 9 s and fading over the last 2.5. Each hole's spin and size come
    from its event id, so every client places the same hole.
  - The low graphics tier draws the holes only.
  - Pooled: no allocation once warm.
- **Checks:**
  - `--visual-rig` covers the kinds, the gun-only rule, the hole fade and the wiring.
  - Play mode on Moonlit, local tool `autobot=` (the bot AI drives the local pilot with the requested gun):
    - machine-gun and pistol rounds play the stone impact on the snow, at the cap of 6;
    - 12–48 holes show, then fade.
  - The converted effects were also rendered in the Editor (stone, metal, wood).
- **Not yet:**
  - holes on moving or breaking props stay where the hit was (they fade in 9 s);
  - surfaces whose material is snow, dirt or glass use stone (the pack also has dirt, glass, brick, rock, plaster and water
    impacts).

## The slash crescents' ink brush (2026-10-24, `claude/slash-brush`)

Owner: "keep going with the next packs, starting with the sword slash arcs."

- **What it does:** the sword family's slash crescent (`NativeSlashFx`: the blade's real sweep, timed by the rules,
  coloured by the chips) now takes the pack's ink-brush look. Streaks run along the swing with ragged ends at the tail and
  the head:
  - the colour band is streaked, keeping a faint body between the streaks;
  - the dark underlay becomes ink streaks, mirrored across the band so ink and colour never line up stroke for stroke.
- **Unchanged:** the crescent's shape, arc, timing, colours and every rule.
- **Why not the pack's own slash prefabs:** they are fixed rings around a demo knight, fired by its animation's event
  scripts, on a dissolve shader. The game's crescent already follows the blade, so only the brush texture was taken.
- **Source:** Ordoss' Magic Slashes FX, its `WhiteScratches` texture. The streaks fill the rectangle u 0.515–0.833,
  v 0.070–0.908 of its alpha, measured from the PNG at the bake.
- **The bake** (local tool `bozo-slashes.request`; the body bake runs it too) adds `VisualSlashes/Brush` to the visual
  body's prefab: an inactive quad whose material carries the texture, its local position and scale the rectangle.
- **Runtime:**
  - `NativeWeaponFxPresentation.WithSlashBrush` hands the texture to the match's drawer (`NativeWeaponFxDraw.SlashBrush`).
  - `Resources/NativeWeaponFx.shader` reads it in the fragment, outside its shared block, for crescents only and only when
    `_SlashBrushOn`.
  - Without the bake the crescent is the procedural shape alone.
- **Checks:**
  - `--visual-rig` covers the wiring; the full suite covers the shader's existing checks.
  - The local tool's `bozo-slashlook.request` drew a synthetic sweep through the game's own drawer and shader, plain and
    brushed, over a dark and a snow-bright floor.
  - Play mode showed the crescent on the AI's sword strikes.
- **Then:** the pack's slash rings, claw marks and line slashes on specials and finishers (the next section).

## A dead pilot dissolves (2026-10-24, `claude/visual-dissolve`)

- **What it does:** a pilot's visual body, as its corpse or authored death scene fades, now dissolves into hexagonal cells.
  The burning front glows the pilot's accent at 3 times its colour, with bloom. Before, the body vanished at half opacity.
- **When:** it runs across the corpse's own fade window (`NativeDeathPresentation`: the last 0.65 s of its 5 s), so no
  timing changes. Gory deaths and voxel-effect deaths keep the voxel body as before.
- **Source:** Advanced Dissolve (Amazing Assets), its `Cutout Map 15` (hexagons). The pack's shaders and scripts cannot
  ship, nor be committed in our public shaders, so the dissolve is our own.
- **Shader:** `NativeVisualBodyFx.cginc DissolveFront`:
  - `_Dissolve` 0 to 1 cuts the noise read on each part's own texture coordinates (three tiles), so the cells stay on the
    cloth as the ragdoll moves;
  - a 0.07 band at the front glows `_DissolveEdge`;
  - at 0 nothing is read or cut.
  - Both the toon body and its outline use it.
- **The bake** (local tool `bozo-dissolve.request`; the body bake runs it too) adds `VisualDissolve/Noise` to the prefab:
  an inactive quad whose material carries the map, with the import set to repeat and linear.
- **Runtime:** `NativeVisualBodyPresentation`:
  - once the bake loads, it sets the noise as the global `_VisualDissolveNoise`;
  - `VisualCorpseShown` and `VisualSceneShown` write `_Dissolve` = 1 - opacity and the accent front on the dead body's
    renderers, only when the value moves;
  - the body hides at full dissolve.
- **Checks:**
  - `--visual-rig` covers the wiring.
  - The local tool's `look=1` rendered the baked body at dissolve 0, 0.25, 0.5, 0.75 and 0.9: glowing cells, then holes,
    then scattered cells.
  - A forced self-kill in Play mode ran, but the slow-motion capture missed the corpse's fade window; it was not seen in a
    match.

## Slash rings, claw marks and line slashes on finishers and specials (2026-10-24, `claude/pack-slashes`)

Owner: "Next, bring the slash pack's ring slashes, claw marks and line slashes onto the special moves and finishers."

- **What plays** (`Core/NativePackSlashes`), tinted to the pilot's sweep colour where the pack's stroke is a colour:

  | Move | Effect | Laid |
  | --- | --- | --- |
  | Sword third cut (the thrust) | line slashes | ahead |
  | Daggers fourth cut | claw marks | ahead |
  | Greatsword second cut | ring, turning the cut's way | flat round the pilot, hip height |
  | Whirlwind, Wolf Spin | ring | flat, a little lower |
  | Rising Dragon, sword launch | ring | stood up across the facing |
  | Flash Step, Shadow Step, Bulwark Charge | line slashes | ahead |
  | Dagger throw | claw marks | ahead |

  Skyfall, kunai marks, the shield's throw and ricochet and Earthsplitter keep only their own effects.
- **When:** a finisher at its strike's hit window (the replicated swing), a special or Q/E ability at its release, once;
  late or stale ones draw nothing. Off on the low graphics tier and for my own first-person pilot.
- **Source:** Ordoss' Magic Slashes FX: `Eff1_Left` and `Eff1_Right` (the ring and its mirror), `Eff6` (only its serial hits,
  their ring and long cut dropped, started at once) and `Eff4` (claws).
- **The bake** (local tool `bozo-slashfx.request`; the body bake runs it too) adds `VisualSlashFx/<kind>`:
  - each pack prefab's particle systems without lights or scripts, placed in the pilot's frame;
  - every pack material converted to our own `Resources/NativePackSlash.shader`;
  - coloured strokes named `Tint ...` for the runtime tint.
  The shader is our own: the stroke scrolled by the particle stream and wobbled by a drifting shift texture, cut by a
  dissolve texture against the particle's alpha, and a plain mode for sparks. The local tool's `look=1` renders our
  conversion beside the pack's own: they match.
- **Runtime:** `NativePackSlashPresentation.PackSlashFx`, after the weapon effects each frame. It pools up to four of each
  kind, places them on the rig root (the hips, its yaw or roll) and replays the systems not under another.
- **Checks:**
  - `--visual-rig`: the move table, the timing and the wiring; no pack names in our code.
  - Play mode, the tool's new `packfx=`: each look forced on the local pilot in a match and captured from the match
    camera.
  - Real triggers: a bot's Flash Step and Rising Dragon, and a dagger bot's fourth cut, played theirs.
  - Not seen live: the sword's and greatsword's finishers (the AI bots rarely chain combos).

## Candidates from the same drive (not started)

- **Archanor's Sci-Fi Arsenal and Epic Toon FX:** projectiles, muzzle flashes, explosions and magic in the toon style.
- **Kronnect's Trails FX:** afterimages for rolls, dashes and sprint attacks.

Each would follow the same route: converted at the bake, drawn from events, presentation only.
