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

**Off since 2026-10-26** (`claude/no-pack-slashes`). The owner saw the line slashes on a lot of melee attacks: the sword's
finisher is every third swing of a chain, the daggers' every fourth and the greatsword's every second, so they showed
through most fights. The owner's verdict: "Nothing fits it currently". `NativePackSlashes.On` is false, so no finisher,
special move or Q/E ability cues them and the bank never loads. The tables below and the bake's effects stay for a move
that fits.

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

## Blasts, smoke, fire and the magic bolts from the effect packs (2026-10-28, `claude/pack-fx-blasts`)

Owner (2026-10-28): "Lets do all of those" (the in-place upgrades from the drive's packs), "Do whatever needs to be done".
This one comes in through the pack banks (`docs/PACK-BANK.md`), not the bake: the tool composes two sheets from the packs
and the licensed-assets zip carries them as textures the game loads by id.

- **Blasts, smoke and fire** (selection `fx-blasts.json`, texture `fx-toon-sheet`):
  - **Source:** Archanor's Epic Toon FX cartoon masks: `cloud_2x2_hardshadow` (the puffs), `explosion_round_3x3` (the round
    flash flipbook), `wave3` and two late `explosion_round_3x3` frames (the crescent licks), `triangle_curve` (the spike),
    `circle_sharp` (the lens, squashed), `sparkle3` (the star). The droplet tile stays Core's own teardrop.
  - **How:** the tool (`tools/pack_bank/sheets.py`, `toon`) rebuilds them into the shared effects renderer's toon sheet,
    same layout and channels as `Core/NativeVfxTextures.ToonSheet`:
    - R: the silhouette's signed distance (from the mask's alpha, an exact distance transform).
    - G: the lit tone, the silhouette shifted toward the light (upper left, as Core's) less the mask's inner creases. Each
      puff gets the game's hard shadow crescent at its lower right and the pack's folds between its lobes.
    - B: the erosion field (interior depth and round cells, Core's formula with a deeper interior), so puffs are still
      eaten into round blobs late in life.
    - A: a thin outline along the silhouette.
    - Puffs: the pack's four cloud shapes, each centred and sized to the tile (`reach`) and turned up to 26 degrees for 16
      variants (the shader still mirrors them by seed).
    - Linear, uncompressed (the four channels are independent fields), mipmapped, trilinear.
  - **What changes:** only the texture (`NativeVfxDraw.Upload` swaps it in when it is there). Every smoke, cloud, toon fire,
    toon water, flash, lick, spike, star and droplet keeps the game's palette, lighting, timing, sizes, erosion and low tier.
    That covers every blast style and the other effects that draw toon puffs (melee dust, the fuse, blood drops, the zombie
    effects).
- **The magic bolts** (selection `fx-magic.json`, texture `fx-magic-sheet`):
  - **Source:**
    - Archanor's Magic Arsenal: the Fire, Water and Lightning missiles' and explosions' sprites (`fire_orb`, `fire_ring`,
      `fire_flame`, `fire_smoke_molten`, `water_drop`, `water_splash`, `water_droplets`, `water_bubble`, `lightning_ball`,
      `lightning_new_arcs_2x2`, `lightning_new_arc`, `lightning_new_glow2`, `light_particle`, `arcane_glow`).
    - Archanor's Sci-Fi Arsenal for L33t H4x's cyber look: `laserball_green`, `symbol_hexagon_simple`, four `symbols`
      frames and eight `fire4x4_modular` frames.
  - **How:** the tool (`atlas`) fits each sprite into an 8 x 4 grid of 256 px tiles, premultiplied, sRGB, compressed. The
    tile order is `Core/NativeMagicPackFx`'s.
  - **Drawing:** the shared effects renderer draws a tile as a camera-facing sprite (`NativeVfxDraw.Pack`,
    `NativeVfx.shader` mode 12).
    - The sprite's brightest channel is its shape, and its whiteness is where it runs white-hot.
    - The rest takes the shot's own colour (`NativeShotRules.HaloColor`, the weapon's), so the schools keep the game's
      palette.
    - The sprites sort with the smoke and puffs.
  - **Per school** (`Core/NativeMagicPackFx`):
    - **Fire:** a flame orb in two counter-turning rings of fire; flame puffs and molten embers behind it; on a hit, two
      ring flashes, flung embers and an ember cloud (the fire staff's blast stays). Its sprites run hot through the fire
      blasts' flame yellow (`NativeBlastSim.FireYellow`) rather than white, which read pink on the weapon's orange.
    - **Water:** a swirling drop in a faint bubble; splashes, droplets and bubbles behind it; on a hit, a drop flash, two
      splashes, a droplet spray and rising bubbles.
    - **Lightning:** a ball flickering at 15 Hz with three arcs re-struck every 50 ms; small balls and arcs behind it; on a
      hit, a flash, a starburst and arc bolts struck afresh every 40 ms.
    - **L33t H4x:** a glowing ball in two counter-spinning hexagons; data bits behind it; on a hit, a cyber flare (the
      flipbook), an expanding hexagon and flung bits.
    - **Darkness:** keeps `NativeMagicFx`'s void orb (bolt, hit and surface burst).
  - **The bolt:**
    - The head replaces the energy core, and the shot's model gives way to it (`NativeShotPresentation.PackHeads`; its chip
      module parts stay).
    - The trail sprites keep `NativeTrailFx`'s spacing, life, size curve, offsets and drift, with the school's tile, spin
      and colour ramp. The white masks (the hexagon, the data bits, the flare flipbook) take the shot's colour all through
      (`NativeMagicPackFx.HotOf`).
    - The ribbon's newest point steps back into the head and draws as an older one, so the ribbon grows out of the head
      (the model hid its bright square end).
    - The halo, the spill light, the source trail and the 120-trail budget are unchanged.
  - **Hits** (`Runtime/NativeMagicPackPresentation.cs`):
    - What triggers one: a surface Impact event, or a direct (or reflected) pilot Hit, whose source holds a school's
      weapon.
    - Bursts keep a ring of 32, each lasting 0.5 s and seeded by the event id, sized 1.3 on a surface and 1.1 on a pilot.
      On a pilot the burst opens back along the bolt's path.
    - The weapon-coloured spark burst, the hard-surface shards and the white hit sparks stay.
    - Reduced effects draws 3 flung pieces and the low tier 5, against 8.
- **Fallback:** without the zip's textures (CI, cloud builds, the public source) or with `--ca-no-pack-banks`, the procedural
  sheet, the energy core, the model and the soft trail sprites draw exactly as before. The offline harness's fallback run
  gave the same billboards, meshes and quads as the base commit.
- **Animation impacts:** none on hit timing, hitboxes, damage, cooldowns, movement or networking (presentation only; every
  sprite is a pure function of the shot key, the sprite index, the event id and the presentation clock).
- **Checks:**
  - `--pack-fx-blasts` (`Tests/PackFxBlastsChecks.cs`): the schools, heads, trails and bursts (tiles in the sheet,
    additive, deterministic, fewer when reduced, nothing past their life, no allocation), the selections' ids and tile
    order, and the renderer's pins and fallbacks.
  - `Tests/test_pack_bank.py`: the distance transform, the toon sheet's layout and channels, the atlas, the sheet rules.
  - Offline WebGL renders (scratch harness: the real `NativeVfxDraw` queueing on stand-in Unity types, the shaders' own
    SHARED maths in GLSL, a dark grid floor, the bloom's knee and shoulder), before and after: a frag grenade, a rocket, a
    fire staff blast, smoke, and each school's bolt, wall hit and pilot hit.
- **Not yet:**
  - seen in Unity or a Windows build (the sheets' import settings come from the tool's `.meta`);
  - the soft smoke atlas (mode 5: mist, vapour, the bosses' fireballs) is still procedural;
  - the fire staff's hit uses Magic Arsenal's ring and embers, not the Sci-Fi fire flipbook the sheet carries for L33t H4x.

## Candidates from the same drive (not started)

- **Archanor's Sci-Fi Arsenal and Epic Toon FX:** projectiles and muzzle flashes (the explosions, smoke, fire and magic
  bolts are in, above).
- **Kronnect's Trails FX:** afterimages for rolls, dashes and sprint attacks.

Each would follow the same route: converted at the bake, drawn from events, presentation only.
