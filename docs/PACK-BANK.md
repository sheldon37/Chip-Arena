# Pack banks: the owner's licensed packs in the game, without the Unity Editor

Owner (2026-10-28): "Lets do all of those" (the in-place upgrades from the drive's packs), "Do whatever needs to be done."

The local bake (`docs/BOZO-INTEGRATION.md`, `docs/PACK-EFFECTS.md`) needs the Unity Editor on the owner's PC. Pack banks are a
second route that a cloud session can run end to end:

1. A committed, standard-library Python tool (`unity/ChipArenaConversion/tools/pack_bank/`) reads the owner's packs as data.
   - FBX geometry is converted the way Unity's model importer does: the mesh in its node's space, the geometric offset kept,
     the file unit and the import's scale factor applied, X mirrored, the axis conversion on root nodes unless the import bakes it.
   - Prefab and material YAML: each mesh part with its transform and materials, and each material's albedo and emission
     texture and colour.
   - PSD (the merged image), TGA and PNG images, scaled down to the selection's limit.
2. It writes a **bank** (`Resources/<bank>.bytes`) and **textures** (`Resources/PackBank/<id>.png` with their import settings in
   the `.meta`).
   - They go under `Assets/LocalAssets/ChipArenaLocal/Resources`, the folder `tools/pack_visual_body.py` already packs into the
     licensed-assets zip (the `.bytes` beside the body prefab, the PNGs under it).
   - Nothing it writes is ever committed or published as files. The same rules apply as for the visual body
     (`docs/ASSET-CREDITS.md`).
3. The game reads them with `Core/NativePackBank` (the format) and `Runtime/NativePackBanks` (Resources, built meshes, loaded
   textures).
   - A missing bank, mesh or texture is null, so callers keep today's look: CI, cloud builds, other checkouts and the public
     source zips stay as they are.
   - `--ca-no-pack-banks` turns them all off for comparisons.

## The tool

```
python3 -I tools/pack_bank/build_pack_bank.py --packs DIR [--out ASSETS/LocalAssets] [--zip OUT.zip] tools/pack_bank/selections/*.json
```

- **`DIR`** holds the packs, one folder per pack, named as in the selections' `sources` (for example `DIR/Toon Harbor Pack/Assets/...`).
  - Each is the unpacked `.unitypackage` content with every asset's `.meta`. The importer's scale settings and the GUIDs come
    from there.
  - A package that nests its pipelines (Toon Harbor ships `BuiltIn_` and `URP_` packages) is unpacked from its Built-in one.
- **`--out`** writes into a checkout's `Assets/LocalAssets` for a local build.
- **`--zip`** writes the same paths alone, to copy over an `Assets/LocalAssets` before `tools/pack_visual_body.py` packs the
  licensed-assets zip. It adds `pack-bank-sources.json`, the pack file behind every output, for the private repository's
  `LICENSES.md`.
- **Selections** (`tools/pack_bank/selections/*.json`, committed) name what each bank takes: names and paths only, no pack
  content.
  - `bank` names the bank file: `ChipArenaPack<Name>`. One selection per bank.
  - A selection with only textures writes none: effect sheets are loaded by their id.
  - `sources`: id to the pack folder's path under `DIR`.
  - `props`: `id`, `source`, `prefab`, `group`. A prop is the prefab's visible mesh parts, in prefab space.
  - `textures`: `id`, `source`, `path`, and optionally `max`, `alpha`, `linear`, `mips`, `clamp`, `crop`. Textures that props'
    materials use are added on their own, as `<source>-<file stem>`.
  - `sheets`: effect sheets composed from pack textures (`tools/pack_bank/sheets.py`), each written as one texture by its
    `id`. Tiles name a `source`, a `path`, and optionally a flipbook `grid` and `frame`, a size (`scale` or `reach`),
    `rotate`, `mirror`, `squash` and `grow`.
    - `kind: toon`: the shared effects renderer's toon sheet layout (16 puffs, then 16 flash, crescent and spark tiles),
      rebuilt from cartoon masks into Core's channels (signed distances, the lit tone, erosion, outline). Linear and
      uncompressed (`textureCompression: 0`).
    - `kind: atlas`: a grid of sprites, premultiplied, sRGB, compressed.
  - `texture_max` sets the default size limit (1024).
- The output is deterministic: the same packs and selections give the same bytes.

## The bank format (version 1)

Little-endian. The full layout is in the `NativePackBank` comment:

- textures (names, flags);
- materials (albedo and emission texture, colour, emission colour, flags);
- meshes in Unity space (positions, normals, uv0, optional uv1 and colours, submeshes with their material);
- props (name, group, bounds, parts: mesh plus a 3x4 place).

`NativePackBank.Fit` places a prop in a box (a collision block or a kit part):
- footprint centred, base on the box's floor;
- quarter turns;
- scaled uniformly to fit, with an optional stretch toward the box.

## Checks

- **`Tests/test_pack_bank.py`** (the Python suite) builds a synthetic pack and compares the bank to
  `Tests/pack-bank-fixture.bytes`. The pack is a hand-written binary FBX with a geometric offset and a turned node, a prefab with
  a nested part, a material and a texture.
  - It also checks the PNG, TGA (RLE) and PSD (RLE) codecs, the axis conversion, the prefab flattening and the selection rules.
  - Nothing under `tools/pack_bank` may be anything but code and selections.
- **`CoreTests --pack-bank`** (`Tests/PackBankChecks.cs`, in the full run):
  - reads the same fixture and checks its content;
  - rewrites it to the same bytes;
  - refuses damaged banks;
  - checks the box fit;
  - pins the runtime loader;
  - scans the game code for pack names.

## Banks

| Bank | Selection | What |
|---|---|---|
| `ChipArenaPackKit` | `kit-harbor.json` | Toon Harbor props for the stages: barrels, crates, containers, pallets, fences, tanks, vehicles, lamps, pipes, cranes and more (296 props, 721 meshes, 5 atlas textures, 12.7 MB) |
| (textures only) | `fx-blasts.json` | `fx-toon-sheet`: the toon sheet from Epic Toon FX's cartoon masks, for blasts, smoke and fire (`docs/PACK-EFFECTS.md`; 1024 x 512, 0.7 MB) |
| (textures only) | `fx-magic.json` | `fx-magic-sheet`: the magic bolts' sprites from Magic Arsenal and Sci-Fi Arsenal (`docs/PACK-EFFECTS.md`; 2048 x 1024, 1.3 MB) |

The effect sheets, weapons and further kits are added by the streams that use them.
