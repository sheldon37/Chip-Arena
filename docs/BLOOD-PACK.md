# The blood pack

Owner, 2026-11-10: "I also have a new gun pack and a new blood pack id like to implement". The pack is RVFX's
"Blood Effects Pack" (Asset Store, owner licence; see `docs/ASSET-CREDITS.md`). Presentation only: no damage, hit or network
state is read back or changed. The droplets, the splat pool and the rest of `Core/NativeBlood.cs` are as they were.

## What the game draws with it

| Part | From the pack | Where | Game shader |
| --- | --- | --- | --- |
| Splashes | six simulated splash sheets (grey shading, alpha, normals) | every spray of blood: a wound on a living pilot, a death's gush, a bisect's, a smash's, the horde's, an arrow's exit | `ChipArena/NativeBloodSplash` |
| Decals | four sprite sheets of blood spreading where it lands (6 x 6 frames, normals) | every splat of the splat pool, and a pool under a kill | `ChipArena/NativeBloodDecal` |
| Flesh lumps | five meshes with albedo and normals | some of a gib's fragments (crush, smash, burst) | `ChipArena/NativeBloodGut` |

- **Splashes** (`Core/NativeBloodPack.cs` `NativeBloodSplash`, `NativeBloodSplashes`): a cut throws a jet along the blade's
  travel and a puff out of the wound (a deep cut a spray too); a shot a puff out of the exit and a small one back out of the
  entry; a crush a burst and sprays to its sides; a sever a larger set; a hard bruise one small puff. A killing blow adds the
  pack's large burst and is 20% larger. Each is one camera-facing quad with its sheet's start point on the wound and its
  travel along the spray, played once over about 0.6 s (it bursts out fast and hangs) with its frames blended, so slow
  motion stays smooth. They replace the wounds' vapour mist.
- **Decals** (`NativeBloodDecalLook`): each splat is one quad of a sheet (a splash with tendrils, a round pool, a smear, a
  tailed one), laid with the sheet's landing point on the splat and its travel along the droplet's. It spreads in over 0.32 s
  on the GPU and keeps its last frame. A kill lays one pool (22 to 52 cm half extent by the wound) on the floor under its
  wound 0.28 s later, spreading over 2.6 s. Colours, drying, fading, surfaces and the pool's size are the splat pool's.
- **Flesh lumps** (`NativeBloodGut`): of a gib's fragments every fifth from a seeded start, at most seven a body, is drawn as
  a pack lump (10 to 25 cm) in the fragment's place, thrown and turned as the fragment is.
- **Light** (`Resources/NativeBloodLight.cginc`): the ambient, the key light, the arena's eight practical slots, the blast
  flashes and the muzzle lights, with a wet highlight from the key and from the strongest practical light.

Everything is a pure function of the replicated wound (kind, severity, point, direction), its seed, the settings and the
presentation clock, so the host, guests and the killcam draw the same blood.

## Settings

| Setting | Splashes | Decals | Lumps |
| --- | --- | --- | --- |
| Gore off | none (and no blood at all from wounds) | none | none |
| Reduced effects | half as many, half the budget | as full (the splat pool keeps 240) | at most three |
| Low graphics tier | one a wound (two on a kill) | today's fan splats, no pools | none |

A shared budget (14 splashes a second, 12 at once in a burst) thins rapid hits; a killing blow is never cut. At most 40
splashes live at once.

## Fallbacks

Each part needs only its own entries in the bake. Without them the blood is as before: no splashes (the mist stays), the
bank's splat textures or the fan splats, and the game's own lumps. A builder without the Drive zip's entries draws the old
blood.

## The local bake

The pack lives in the Editor's checkout under `Assets/LocalAssets/RVFX/BloodEffectsPack` (git-ignored, never committed; its
scripts and its URP/HDRP packages are not installed). Its shaders and scripts never ship: the bake puts the pack's textures
and meshes on the game's own shaders.

- Request: `Temp/bozo-blood.request` (lines: `#version N`, an output folder, optionally `look=1` to render each entry and
  `force=1` to rewrite the texture copies). The local tool (`BozoInventory.cs`, `AddVisualBlood`) rebuilds `VisualBlood` in
  `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaVisualBody.prefab`; the full body bake does too. The Editor must
  be on a checkout that has the three shaders, or the materials already baked are kept.
- Output, all under `Assets/LocalAssets/ChipArenaLocal/VisualBody/Blood/`:
  - `splash-<name>.png`, `splash-<name>-normal.png`, `splash-<name>.mat` for `burst`, `spray`, `puff-a`, `puff-b`, `jet-a`,
    `jet-b`;
  - `decal-<name>.png`, `decal-<name>-normal.png`, `decal-<name>.mat` for `splash`, `round`, `smear`, `tail`;
  - `gut-<n>.png`, `gut-<n>-normal.png`, `gut-<n>.mat` for 1 to 5 (their meshes are the pack's own
    `Common/GutEffect/Gut_0<n>/Mesh/Gut_0<n>.fbx`).
- Sizes: the splash sheets at 1024 (normals 512), the decal sheets at 1024 (normals 768), the lumps' maps at 256: 45 files
  and their `.meta` files, 8.7 MB, plus the five FBX files (0.1 MB).
- `tools/pack_visual_body.py` follows the prefab's references, so the Drive zip takes those files, the five FBX files and
  their `.meta` files. Nothing else of the pack rides along. The installer's visual body cap is 240 MB (219 MB before this).
- The look's numbers (gloss, shading gain) are set by the runtime, not read from the baked materials.

## Checks

`dotnet run --project Tests/CoreTests.csproj -c Release -- --blood-pack` (`Tests/BloodPackChecks.cs`): the counts by wound
and setting, the flipbook, the quad's frame, the budget, the decal picks, the lumps, the shaders' contract and the fallbacks.
