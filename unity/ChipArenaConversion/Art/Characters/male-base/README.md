# Low-poly male base (Phase B)

The first skinned low-poly pilot body for `docs/CHARACTER-LOWPOLY.md`: a lean young man of human proportions (1.74 m,
7.5 heads), a natural stylized face, short swept `spiky-01` hair, dark boxer-briefs with cyan trim, the chest core and
the right-wrist chip device. Original geometry, built by script. Nothing here
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

`Tests/test_lowpoly_male_base.py` checks the exports statically: the rig's names and parents and the recorded
authoritative offsets against `Core/CapturedRig.cs`, the visual skeleton's proportions, weights, regions, budgets,
winding, envelope and the right-wrist device. Two exports of one `.blend` are byte-identical; two builds give the same
content (the `.blend` container's internal pointer ids differ between runs).

## Form references (provenance)

Two small JSON tables give the body and head their anatomical shapes. They are this project's own measurements, not
third-party assets: no vertex, face or texture of another model is stored in them or used by the builder.

| File | Made by | What it holds |
|---|---|---|
| `form_reference.json` | `sample_form_reference.py` | Normalized torso cross-sections (half-ring shapes divided by their own width and depth) at landmark-normalized heights, and normalized limb ring radii (mean 1) for the thigh, shin, upper arm and forearm. |
| `head_reference.json` | `sample_head_reference.py` | A radial distance field of the head (a 2.5° theta/phi grid, divided by the head height), which grid cells see the eyeball, and a few landmarks: the eyeball, lid joints, nose tip, chin, a centre-line profile and the ear's angular bounds. |

Both were measured, with Blender 4.2, from the MakeHuman 1.1 base mesh (`makehuman/data/3dobjs/base.obj`), the macro
target `universal-male-young-maxmuscle-averageweight.target` and, for the head only, the face modifier targets listed
in `MODS` in `sample_head_reference.py`. They come from github.com/makehumancommunity/makehuman and are released under
CC0 1.0 Universal: see that repository's `LICENSE.md` section C and `LICENSE.ASSETS.md`, and each target file's header,
which says "explicitly released as CC0 in september 2020". CC0 asks for no attribution, but it is recorded here
anyway. None of those source files is committed. The builder scales the normalized shapes to its own stylized
dimensions on its own skeleton and places its own low-poly vertex layout on them.

To re-measure, download those files and run, for example:

```
blender -b --factory-startup --python sample_head_reference.py -- base.obj \
    universal-male-young-maxmuscle-averageweight.target <dir with the modifier targets> head_reference.json
```

## Skeleton

The skeleton is the visual skeleton of `docs/CHARACTER-LOWPOLY.md` section c: `CapturedRig.Bones`' 22 names, indices,
parents and identity rest frames (arms hanging straight down, palms in, thumbs forward) with human rest offsets
(`VIS_OFFSETS` in the builder). The game's authoritative offsets (`GAME_OFFSET`) are recorded beside them in the
manifest (`gameRestLocal`, `gameRestWorld`) for the presentation adapter, which applies the game's local rotations
1:1, scales the root's translation by `legRatio` and fixes the feet and the support hand by IK. Gameplay, hit volumes
and weapon frames keep the authoritative rig.
- **Root:** the visual motion-root sits at feet + 1.182 m (the game's at feet + .865). It carries no weight.
- **Blender:** bones point up (rig +y) with roll 0, so a pose bone's rotation is the game's local quaternion, in
  source axes.
- **FBX:** every joint has a zero rest rotation and its visual offset. The only exception is Blender's axis
  conversion, which leaves -90° X on the `CA_Rig` node and +90° X on `motion-root`. The two cancel.
- **Weights:** the mesh is modelled in the rest pose; the bone-heat pass runs on a copy with the arms spread 40°
  (they hang against the lats at rest) and rule-based blends place the limb weights relative to the visual joints.
- **Optional secondary bones:** marked `secondary` in the manifest, each with a `fallback` joint. A runtime that
  ignores them folds their weights onto the fallback, which gives the rest shape with no gaps.
  - `sec-hair-{front,side,back}-{l,r}-{1,2}`: six 2-bone hair chains under `head`, with spring parameters in
    `springs`.
  - `sec-device-lid` and `sec-device-chip`: under `right-wrist`. Their hinge, insertion path and states are in
    `device`.

## Body

- The torso is designed rather than measured: front, side and back profile curves per row (`TORSO`), rounded
  cross-sections whose roundness changes from the pelvis through the waist to the ribcage (`TORSO_ROUND`), and broad
  muscle forms laid over them (`TORSO_FORMS`: pectorals, sternum, abdominals, obliques over the iliac crests, lats,
  scapulae, spine, glutes). Rows follow the muscle flow (the pecs' lower edge rises into the armpit, the waistband dips
  into a V along the crests); 13 columns per half ring.
- The deltoid is a round cap on a sphere over the shoulder joint (two rings between the chest's socket and the arm);
  the limbs are lofted rings with per-ring shaping for the biceps and triceps, the forearm full near the elbow and flat
  at the wrist, the quadriceps' sweep, the knee's patella plane, and a calf high on the inside.
- The limbs' cross-sections still come from the measured form reference (`form_reference.json`).

## Head, face and hair

- The head is a designed low-poly layout (`FACE_LAYOUT`: the lid margins round each eyeball, the iris and pupil, lid
  creases, the brow strip, the nose's bridge, dorsum, alae and nostrils, the lip borders and mouth line, the chin and
  jaw lines, the hairline with its temples, sideburns and nape) placed on the measured head form, triangulated with
  constrained Delaunay in a conformal chart of the head (`build_head`). Facial thirds and eye widths follow the usual
  proportion rules; every feature is geometry, and paint only colours what the geometry shapes (no eyeliner, outlines
  or decals; the upper lid's plane is a shade darker so the eyes sit in their sockets).
- The scalp inside the hairline is painted on the hair channel. The `spiky-01` hair is a separate object: a thin cap
  over the scalp and swept, faceted clumps lying on it, with the six spring chains.
- Shading: one smooth group over the body, broken at creases over 32° (30° on the head, 21° on the feet), at paint
  borders and along the design creases (the pecs' lower edge, the abdominal lines, the linea alba); the exports carry
  corner normals.

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
