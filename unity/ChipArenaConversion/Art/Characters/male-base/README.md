# Low-poly male base (Phase B)

The first skinned low-poly pilot body for `docs/CHARACTER-LOWPOLY.md`: male base, natural stylized face, `spiky-01`
hair, dark boxers, the chest core and the right-wrist chip device. Original geometry, built by script. Nothing here
is imported by Unity, and nothing is wired into the runtime: Phase D copies the binary and manifest into
`Assets/Conversion/Resources/Characters/`.

## Files

| File | What it is |
|---|---|
| `build_male_base.py` | The editable source. It rebuilds `male_base.blend` from nothing, deterministically. |
| `male_base.blend` | The builder's output: the body, hair, core and device pieces, the armature, weights, regions and the 2 materials. |
| `export_male_base.py` | It writes everything under `exports/`. Two runs are byte-identical. |
| `exports/male_base.bytes` | The runtime mesh library, `CALP` v1 (layout in the manifest's `binary.layout`). |
| `exports/male_base.manifest.json` | Bones, bind poses, pieces, paints, channels, regions, counts, hair springs, device hinge and states. |
| `exports/male_base.glb` | A preview for the owner (glTF binary, with the custom attributes `_HIDE` and `_DEATH`). |
| `<repo>/.cache/characters/male-base/male_base.fbx` | A preview FBX (-Z forward, Y up, metres, transforms applied). It is written outside the Unity project and not committed: the CoreTests pack guards reject any `.fbx` in the project. |

Rebuild and export from the repository root, with Blender 4.2 LTS:

```
blender -b --factory-startup --python-exit-code 1 --python unity/ChipArenaConversion/Art/Characters/male-base/build_male_base.py
blender -b --factory-startup unity/ChipArenaConversion/Art/Characters/male-base/male_base.blend --python-exit-code 1 \
    --python unity/ChipArenaConversion/Art/Characters/male-base/export_male_base.py
```

`Tests/test_lowpoly_male_base.py` checks the exports statically: the rig against `Core/CapturedRig.cs`, weights,
regions, budgets, winding, envelope and the right-wrist device.

## Skeleton

The skeleton is exactly `CapturedRig.Bones`: the same 22 names, indices, parents and rest offsets, with identity rest
frames (arms hanging straight down, palms in, thumbs forward). The game's local joint quaternions and offsets drive it
unchanged, and that is how every preview render is posed.
- **Root:** motion-root sits at feet + .865 m, as `NativeMeleeSlice.RootHeight`. It carries no weight.
- **Blender:** bones point up (rig +y) with roll 0, so a pose bone's rotation is the game's local quaternion, in
  source axes.
- **FBX:** every joint has a zero rest rotation and its CapturedRig offset. The only exception is Blender's axis
  conversion, which leaves -90° X on the `CA_Rig` node and +90° X on `motion-root`. The two cancel.
- **Optional secondary bones:** marked `secondary` in the manifest, each with a `fallback` joint. A runtime that
  ignores them folds their weights onto the fallback, which gives the rest shape with no gaps.
  - `sec-hair-{front,side,back}-{l,r}-{1,2}`: six 2-bone hair chains under `head`, with spring parameters in
    `springs`.
  - `sec-device-lid` and `sec-device-chip`: under `right-wrist`. Their hinge, insertion path and states are in
    `device`.

Proportions are the rig's: a stocky 1.70 m figure (doc section c, owner decision j1). The design answer is a
heroic upper body (broad deltoids and trapezius at the ±.345 m shoulders, a V taper to the waist), a large head
centred at 1.50 m, and short, sturdy legs.

## Paint and materials

There is one body material (`CA_Body`) and one emissive material (`CA_Glow`). No textures are used.
- **Vertex colour:** each face corner holds its paint's default sRGB colour, with alpha = channel × 32 / 255.
- **Channels** (doc d): 0 fixed, 1 skin, 2 hair, 4 secondary (the boxers), 5 accent glow (device, lid lens, chip),
  6 core glow.
- **Tinting by ratio**, as the bank does: `albedo = paint × param / paint default`. A skin tone, hair colour or glow
  colour swap therefore needs no new mesh.
- **Blender parameters:** the `CA_Body` RGB nodes `SkinTone`, `HairColor` and `SecondaryColor`, and on each glow
  piece the object properties `ca_glow_tint` and `ca_glow_gain`.

## Regions

Each vertex carries two regions.
- **Hide region:** one of the doc's 19 (scalp, face, neck, chest, abdomen, back, pelvis, and per side upper arm,
  forearm, hand, thigh, shin, foot).
- **Death region:** the ragdoll part root joint (`NativeKillEffectRules.PartRoots`), taken from the vertex's
  dominant joint.

Every hide region lies inside one death region (`regions.hideToDeath`). The coarse names (head, neck, torso
upper/lower, limbs) are in `regions.coarse`.

## Wrist device states

The device, lid and chip are rigid on the right wrist. The lid hinges on its proximal edge, and the chip seats flat
in the bay with its gold connector toward the wrist.
1. **Closed / idle:** lid shut, standby glow.
2. **Opening:** the lid swings 112° outward over .25 s and the bay walls light.
3. **Insertion:** the chip runs from its approach offset and tilt to seated over .3 s.
4. **Accepted:** the lid closes, the device flashes, and the lens shows the chip in its rarity colour.
5. **Activated:** the glow decays to standby over about 1.5 s.

The binding to the chip-select timelines is Phase D (doc e).
