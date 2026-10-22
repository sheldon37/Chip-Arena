# BoZo characters + Magica Cloth 2: integration plan

Status (2026-10-22, Claude): steps 1–2 done on the owner's PC (`claude/bozo-inventory`, docs only, merged into shared
after the owner's review): both packs imported, moved under `Assets/LocalAssets/`, and inventoried below; the owner's
decisions follow the inventory. Step 3 (rig adapter): implemented on `claude/bozo-rig-adapter`, not merged; it is not wired
into the live body path yet. Next: the BoZo drift measurement in Unity, then step 4 (first character). Owner decision: player characters come from the **BoZo: Stylized Modular
Characters – Anime Pack** (Unity Asset Store, v2.6.4), with **Magica Cloth 2** (v2.18.4) for hair and cloth motion. Both are
bought by the owner. The custom Blender base (`claude/lowpoly-base-model`, last checkpoint `5c3d6ae`, not merged) is stopped; its chest
core and right-wrist device (with the closed / opening / insert / accepted / active states) are kept as attachments for BoZo
characters. `docs/CHARACTER-LOWPOLY.md` (Phase A) still holds for the rig adapter, cosmetic schema, death compatibility,
the performance pass criteria and the Astra contract. Its triangle, LOD and bone caps do not apply to BoZo (owner,
2026-10-22). This file records what changes for an asset-pack body. Map restyle to match:
`docs/MAP-STYLE-ANIME.md` (branch `claude/anime-map-style`).

## Rules
- **Licensed, never committed.** Both packs live under `unity/ChipArenaConversion/Assets/LocalAssets/` (already git-ignored).
  After an Asset Store import, move the pack folders there before doing anything else, and check `git status` shows nothing
  from them.
- **Optional at compile time.** CI, the cloud checks and any machine without the packs must still compile and run, falling
  back to today's characters. All code that names BoZo or Magica types sits in an optional assembly (asmdef with
  `defineConstraints`), enabled by a local scripting define that an editor check sets only when the pack folders exist.
- **Presentation only.** Hit volumes, colliders, weapon frames, shot origins and the wire are unchanged; cloth and hair are
  simulated per client and never networked; first person never runs cloth.
- **Package manifest.** BoZo's shader needs Shader Graph; Magica Cloth 2 needs Burst, Collections and Mathematics. Changes to
  `Packages/manifest.json` are a build-tooling change: coordinate with Astra before committing them.

## Where the work runs
Unity-dependent steps (import, inventory, rig mapping, cloth, captures, profiling) run in a Claude Code session on the owner's
PC, which has the editor, the licence, a GPU and the packs. Docs, the repo check suite (`run5b`), Blender work and parallel
agents run in the cloud session. Both push to GitHub branches.

## Steps
1. **Import and verify** (owner's PC). Import Magica Cloth 2, then BoZo; accept their package dependencies; move both folders
   under `Assets/LocalAssets/`; open BoZo's character-creator scene once in the Built-in pipeline (pink materials mean Shader
   Graph is missing). Record versions.
2. **Inventory** (write into this file): BoZo skeleton (bone names, hierarchy, rest pose, height), body and outfit meshes per
   slot (head, eyes, tops, bottoms, footwear, gloves, hats, front and back hair, head and face accessories), blendshapes (body
   type, chest, weight, face, expressions), shader (BMAC_Toon properties, the 9 colour channels, outline), the merge-to-one-mesh
   path, the equip and save systems, triangle counts. Magica Cloth 2: version, runtime-build API, BoneCloth vs MeshCloth,
   colliders, culling and LOD.
3. **Rig adapter.** Map BoZo's humanoid bones onto the 22 `CapturedRig` joints (CHARACTER-LOWPOLY section c). Extra bones
   (spine segments, twist, fingers) are derived or held. BoZo's proportions are the visual skeleton; foot IK to the authority
   contacts and support-hand IK as designed; weapon on the visual hand (P1, owner 2026-10-22). Drift table vs the authority
   volumes, measured in Unity.
4. **First character.** A male preset (body blend, a hair, a default outfit) plus the chest core and the right-wrist device,
   player colours through BoZo's colour channels, and our lighting (picked lights, actor lights, muzzle lights) reaching its
   shader (bridge or port). Neutral turnaround and gameplay poses captured in Unity.
5. **Magica Cloth.** BoneCloth on BoZo's hair bones and cloth on every applicable outfit piece (owner 2026-10-22: hair,
   clothes, skirts, coats, hoods, scarves, straps): presets, body colliders including the core and device, distance and quality culling, a budget for 8 pilots, off in first
   person; killcams, corpses and intros re-simulate locally.
6. **Wider integration.** Cosmetic slots mapped to BoZo categories; Astra's look-map contract updated with BoZo ids; death and
   dismemberment on skinned BoZo meshes (regions from bone weights); legacy voxel fallback kept until parity; performance vs
   the voxel renderer (draws, skinning, cloth) on the owner's PC.

## Step 1 result: import and verify (2026-10-22, owner's PC)

Unity 6000.3.24f1, Built-in pipeline, worktree `Chip-Arena-Unity` on `claude/bozo-inventory` from shared `c4c4672`.

- **Versions** (Asset Store package headers in `%APPDATA%/Unity/Asset Store-5.x/`): BoZo 2.6.4 (product 323550, published
  16 Oct 2026, authored on 2022.3.62f1); Magica Cloth 2 2.18.4 (product 242307, 4 Oct 2026, authored on 2022.3.0f1).
- **Location.** Both imported to the Asset Store default (`Assets/BoZo_StylizedModularCharacters`, `Assets/MagicaCloth2`),
  where git does not ignore them and `tools/package_source.py` would package them. Moved with their `.meta` files to
  `Assets/LocalAssets/` (GUIDs kept; the Editor registered 1,695 moved assets). `Assets/LocalAssets.meta` uses the GUID
  `tools/install_local_assets.py` writes. `git status` shows nothing from either pack. An Asset Store re-import or update
  lands in the default folders again: move it, or re-import into the moved folders (GUIDs match).
- **Built-in shaders.** BoZo's three hand-written shaders (`BMAC_Toon`, `BoZo_LayerTextureShader` = `Hidden/TextureLayerer`,
  `BoZo_Decal`) arrive as URP versions; they fail on Built-in ("Couldn't open include file
  .../universal/ShaderLibrary/Core.hlsl"). The pack ships the Built-in versions in `STYLIZED_Built-In_Shaders.unitypackage`
  (same GUIDs); installed them. The URP originals are kept in the git-ignored `unity/ChipArenaConversion/LocalAssets/BoZo-URP-shaders/`.
  Its Shader Graphs carry both Built-in and Universal targets.
- **Compile.** Magica's `Example (Can be deleted)` (145 MB, own asmdef) failed: `WindDemo.cs` needs `WindZone` from the Wind
  module, which this project's manifest leaves out. Moved to the git-ignored `unity/ChipArenaConversion/LocalAssets/MagicaCloth2-Example/`
  (outside `Assets`, not compiled) rather than adding a module. The Editor then compiled with no script errors.
- **Import warnings left (pack content):** `BSMC_Head.fbx` (an old head no preset uses): "Avatar creation failed:
  Transform 'Hips' not found"; `BSMC_Head_02.fbx`: no smoothing groups for blendshape normals.
- **Shaders compile on Built-in:** all ten in the pack compile with no errors in the Editor: `Bozo/Bozo_Toon`, the six Shader
  Graphs, `Hidden/TextureLayerer`, `Hidden/BoZo_Decal` and the demo graph.
- **Broken materials (pack content):** 7 of 204 point at missing shaders and draw pink: three brow materials, `BSMC_BakeMat`,
  `UnderUpper_SimpleUnderShirt_Mat`, and two TextMeshPro font materials for the creator UI.
- **Not done:** opening `Scenes/StylizedCharacterCreator.unity`, which would replace the owner's open scene. The local
  inventory tool (below) renders all nine presets in a preview scene instead; none draw pink.

**Package manifest (not committed; Astra's call).** The imports added one line, `"com.unity.shadergraph": "17.3.0"`, and an
untracked `Packages/packages-lock.json`. Burst 1.8.30, Collections 2.6.8 and Mathematics 1.3.3 resolve only as Shader Graph's
dependencies (through `com.unity.render-pipelines.core` 17.3.0, with searcher 4.9.5, ugui 2.0.0, test-framework 1.6.0,
test-framework.performance 3.5.0, nuget.mono-cecil 1.11.6 and ext.nunit 2.0.5). Magica's asmdef compiles only while those
three resolve (`versionDefines` `MC2_BURST`/`MC2_COLLECTIONS`); without them its importer asmdefs `Client.Add` them into the
manifest on load. Committing it needs:
- the three listed explicitly;
- `tools/builtin-packages-6000.3.24f1.json` (the packager's catalog) extended: today `package_source.py` and
  `test_source_packages` reject `com.unity.shadergraph@17.3.0` on this PC.

**Tracked and shared settings the Editor and packs write (none committed).**
- Opening the project rewrites the minimal tracked `ProjectSettings/ProjectSettings.asset` (+691 lines) and re-serialises
  `InputManager.asset`; it creates 20 untracked `ProjectSettings/*.asset`. The stub's `m_ColorSpace: 1` is not a Unity
  key: the Editor runs Gamma. Builds still force Linear (`Editor/PrototypeBuild.cs:59`), so in-Editor looks differ from builds.
- Magica: `MagicaManager` `[InitializeOnLoadMethod]` adds `MAGICACLOTH2` to the selected target's Scripting Define Symbols on
  every domain reload (`Scripts/Core/Manager/MagicaManager.cs:151-170`). It lands in `ProjectSettings.asset` when the Editor
  saves. If that is ever committed, CI gets the define without the assembly.
- BoZo: `BoZo_AutoIncludeShaders` adds five shaders to Always Included in `GraphicsSettings.asset` (untracked) on every load.

## Step 2: inventory

Sources: the packs' files and code (static, file:line from the packs), a static FBX reader over all 140 BoZo FBX, and a
local Editor tool (`Assets/LocalAssets/ChipArenaLocalTools/Editor/BozoInventory.cs`, git-ignored with the packs).

### Skeleton
- `Outfits/Common/Body/BodyRig.fbx`: one Humanoid avatar (Mixamo-style mapping), Unreal-mannequin names, 107 transforms:
  `root` → `pelvis` → `spine_01`…`spine_05` → `neck_01`, `neck_02` → `head`; `clavicle`/`upperarm`/`lowerarm`/`hand` with
  metacarpals and 3-joint fingers; `thigh`/`calf`/`foot`/`ball`. There are also IK helpers (`ik_foot_*`, `ik_hand_gun`/`_l`/`_r`),
  `center_of_mass`, `interaction`, `breast_l/r`, and face bones (`brow`, `cheek`, `jaw`, `chin`, `mouth`, `nose`, `nosebridge`).
  Heads add `eyeRoot`/`IrisRoot` bones and hair adds chains (`additionalBones`, below).
- **Twist bones exist**: `upperarm_twist_01/02`, `shoulder_twist`, `lowerarm_twist_01/02`, `thigh_twist_01` (+`01_l1`) and
  `calf_twist_01` (+`01_l1`) per side. So the 22-joint candy-wrapper risk is solvable by deriving twist, not by cuffs.
- **Rest pose:**
  - The model's default and bind pose is an **A-pose**, measured in Unity: upper arms 53° below horizontal, hands at 1.06 m.
  - The avatar's Humanoid configuration stores a separate T-pose; only Mecanim uses it.
  - Bone frames are non-identity (local x runs down each chain). The game's `CapturedRig` rests arms-down with identity
    frames, so "copy local rotations 1:1" (section c of the Phase B design) does not apply as is.
  - The adapter needs a per-joint rest alignment computed once at bind: BoZo world = game world delta × BoZo rest world,
    where the delta is relative to each rig's own rest.
- **Rest joint positions** (Unity, metres; left is −x, front +z):

  | Joint | Position |
  |---|---|
  | pelvis | .987 |
  | spine_02 | 1.075 |
  | spine_05 | 1.395 |
  | neck_01 | 1.491 |
  | head | 1.573 |
  | chin bone | 1.546 |
  | upper arms | ±.133 at 1.425 |
  | elbows | ±.291 at 1.215 |
  | wrists | ±.427 at 1.060 |
  | hips | ±.093 at .961 |
  | knees | .536 |
  | ankles | .110 |
  | balls | .013 (z +.116) |

  Segments: upper arm .263, forearm .224, thigh .425, shin .428. Leg (hip to ankle) .852 m against the game's .50 m, so
  `legRatio` ≈ 1.70 (the Blender base's was 1.64).

**Mapping onto the 22 `CapturedRig` joints** (proposal for step 3; derived bones are presentation only):

| CapturedRig | BoZo | Notes |
|---|---|---|
| motion-root (0) | `root` + height offset | BoZo `root` is at the soles; the game's root is feet + .865 m |
| pelvis (1) | `pelvis` | |
| spine (2) | `spine_01`, `spine_02` | rotation shared between the two |
| chest (3) | `spine_03`, `spine_04`, `spine_05` | shared; `breast_l/r` held |
| neck (4) | `neck_01`, `neck_02` | shared |
| head (5) | `head` | face bones, `eyeRoot`, `IrisRoot` ride it |
| clavicle (6, 10) | `clavicle_l/r` | |
| shoulder (7, 11) | `upperarm_l/r` | `shoulder_twist`, `upperarm_twist_01/02` derived from the upper arm's roll |
| elbow (8, 12) | `lowerarm_l/r` | `lowerarm_twist_01/02` derived from the wrist's roll |
| wrist (9, 13) | `hand_l/r` | fingers held in a grip pose per weapon (none today) |
| hip (14, 18) | `thigh_l/r` | `thigh_twist_01*` derived |
| knee (15, 19) | `calf_l/r` | `calf_twist_01*` derived |
| ankle (16, 20) | `foot_l/r` | |
| toe (17, 21) | `ball_l/r` | |

Unmapped and held: `ik_*`, `center_of_mass`, `interaction`, fingers, face, hair chains (Magica).

### Bodies, slots and outfits
- **Slots:** 36 `OutfitType` assets, empty ScriptableObjects used only as name keys. Six have no outfits (Ears, HeadDetail,
  Iris, Overall, Shoulders, Waist). One outfit per slot; equipping replaces.
- **166 outfit prefabs** (each an `Outfit` component plus a nested FBX with its own skeleton copy). By slot: Top 24, HairBack 19,
  Bottom 16, Feet 13, HairFront 11, Hat 9, Gloves 8, Pupil 8, EyeBrows 6, Socks 6, FaceDetails 5, UpperFace 5, EyeLashes 4,
  HeadAcc 4, LowerFace 3, UnderLower 3, UnderUpper 3, and 1–2 each for Body, Head, Eyes, EyeShine, Teeth, MakeUp (×3),
  Neck, Leggings and FaceTexture. Underwear, socks, leggings, pupils, shine, make-up and face details are texture-only
  layers (0 triangles). `Top_ThickHenslay` has no Type and throws when equipped (pack bug).
- **Body:** `Body_AnimeBasic` / `Body_BasicBody` are 14 part meshes (ankles, back, chest, feet, hands, hips, lower arms,
  lower legs, neck, shoulders, upper arms, upper legs, waist, wrists). Outfit `tags` (CoverChest, CoverHips, CoverUpperArms…)
  switch the covered parts off (`OutfitHideByTag`) and set `Tag_*` blendshapes. Only four tops use `HideTypes` (they hide
  Bottom).
- **Rigid pieces:** every outfit is skinned (`AttachPoint` is empty on all 166).
- **Heads:** `Head_AnimeYoung` (presets) and `Head_BasicHead`. The face is a mesh with eyes, iris, pupil, shine, lashes,
  brows and teeth as separate pieces. It is a human face (not a mask) in the anime style, with large eyes. The owner keeps
  it as authored (2026-10-22).
- **LODs:** none. No LODGroup and no reduced meshes in the pack.

**Triangles** (static FBX count; Unity triangulates the same way). Body 9,600 (hands 2,976, feet 1,894, the other 12 parts
4,730); head 3,136; eyes 424; hair 546–2,585 per piece; tops 3,016–5,130; bottoms 878–2,742; feet 816–3,900; gloves
682–1,884. Per preset, as drawn after the body-part hides:

| Preset | Look | Sum | Body parts hidden | Drawn |
|---|---|---|---|---|
| Default_Boy | boxers | 16,536 | none | 16,536 |
| Zell | baggy pants, arm bands | 19,280 | hips, legs (1,900) | 17,380 |
| Kenji | suit, khakis, loafers | 22,828 | 5,888 | 16,940 |
| Jackal | jacket, capris, boots, gloves | 28,110 | 4,904 | 23,206 |
| Glover | cardigan, shorts, flats, gloves, cap | 30,221 | 4,520 | 25,701 |
| Default_Girl / Jayda / Hana | | 16,722 / 21,412 / 24,614 | | 16,722 / 16,892 / 22,520 |

So a dressed character draws about 17–26 k triangles: the store's "about 19 k" holds, and it is 2.4–3.7 × the Phase A LOD0
budget of 7,000. That budget is dropped for BoZo: the owner set no triangle cap (2026-10-22).

**Bones and weights** (Unity):
- Up to 38 weighted bones per mesh piece (hands, tops, gloves 26–38).
- 4 influences per vertex everywhere except the legacy `Body_BasicBody.fbx` parts (back and shoulders 6; chest, neck and
  upper arms 5).
- An assembled preset weights 85–93 distinct bones and references 107–117. These come from the humanoid set, twist,
  `breast`, the face, `eyeRoot`/`eyeSocket`/`IrisSize` and the hair chains. That is far over Phase A's 22.
- Heads, eyes and lashes bring a `headAnime` parent for their eye bones. It is not in `BodyRig`, so a binder has to add
  it under `head` (BoZo does this through `additionalBones`).
- Vertex colours are on 136 of 139 meshes; one UV set.

### Blendshapes and body shaping
- **Body** (9 per part, and the same 9 on tops, bottoms, gloves and some shoes, so clothes follow): `Shape_BodyType`
  (male ↔ female), `Chest`, `Weight`, `Belly`, `Muscle`, `Curvy`, `NeckThickness`, `WaistSize`, `ButtSize`.
- **Head_AnimeYoung** (53): face shapes (`BodyType`, `EarLength`, `EyeRoundness`, `Maturity`, `MouthWidth`, `Muscle`,
  `NeckThickness`, `Roundness`, `Sharpness`, `Weight`) plus about 43 `Expression_*` driven by animation clips only.
- `Tag_*` shapes (hair under hats and hoods, `Tag_HighKnee`, `Tag_ArmCover`) are set by outfit tags.
- **Bone modifiers.** `BodyShapeModifier` writes per-bone scale, position and rotation offsets by bone name, mirroring `_l`
  to `_r`. `Default_Boy` uses 16 of them, for example root ×1.025, spine_04 ×1.018, clavicle ×1.068 and the twist bones ×1.098.
  These are presentation-only offsets, many of them on bones outside the 22 joints.
- **Male/female:** `Shape_BodyType` plus an Animator float `Stance` (idle and walk trees). The base mesh (BodyType 0) is the
  female or neutral body; the male presets sit at 26–67 (Zell 26, Kenji 30, Default_Boy 45, Jackal 67) and the female ones at
  −15 to 0. Default_Boy: BodyType 45, Weight 15, NeckThickness 27, stance .25.

### Measured proportions
Stature and head come from the Unity inventory, with the presets assembled by bone name and their blendshapes applied.
Widths are read from the body FBX in bind pose under the same blendshape weights, because the hanging A-pose arms spoil a
width slice taken in Unity (see the note on the tool below). BoZo's bone modifiers (for example Default_Boy's root ×1.025)
are not applied.

| | BoZo male (Default_Boy) | Owner's target | Stopped Blender base | Authority (unchanged) |
|---|---|---|---|---|
| Stature, soles to crown | 1.787 m (1.83 m with its root ×1.025) | | 1.74 m | capsule 1.70 m |
| Head, chin to crown | .270 m (width .216, depth .235) | | .218 m | |
| Heads tall | **6.6** | 7.5–8, superseded: BoZo's normal head (2026-10-22) | 8 | |
| Head centre | 1.651 m | | 1.631 m | head box 1.49 m |
| Hair top | 1.81–1.83 m | | | |
| Shoulders, bideltoid | .462 m | broad | | voxel joints ±.345 |
| Chest (torso width) | .300 m | | | |
| Waist, narrowest | .241 m | | | |
| Hips | .350 m | | | |
| Shoulder ÷ waist / ÷ hip | 1.92 / 1.32 | V-taper | | |

How the shapes move the widths (bind pose, metres; shoulders / chest / waist / hips):
- BodyType 0 (the neutral base): .445 / .281 / .204 / .344.
- BodyType 100: .469 / .317 / .242 / .339.
- BodyType 100 with Muscle 100: .498 / .353 / .266 / .339.
- Jackal (BodyType 67, Muscle 26): .468 / .313 / .238 / .341.

All the presets have the same head (Head_AnimeYoung) and skeleton. Every one measures 6.5–6.8 heads tall, at 1.787–1.821 m
stature. Shoes put the soles 1–3.5 cm below the floor (BoZo raises the root only for heels), so the floor fit has to read
the shoes.

Reading it:
- BoZo's male is not narrow: about 46 cm across the shoulders over a 24 cm waist is a strong V-taper. The torso is shallow
  and narrow at the chest (30 cm). BodyType toward 100 plus some Muscle would broaden it, but the owner keeps BoZo's
  shapes as its presets set them (2026-10-22).
- **Head: keep BoZo's normal size (owner decision, 2026-10-22).** The figure stays about 6.6 heads tall, which supersedes
  the earlier 7.5–8 target. The anime "young" head is .27 m tall, against .22–.23 m for an adult.
  - If this is revisited, a scale through BoZo's `head` bone modifier changes it: about .87 gives 7.5 heads (crown about
    1.76 m), and about .80 gives 8.
  - Hair and hats scale with the head bone.
- Drift against authority, to re-measure through the adapter in step 3:
  - The visual head centre sits .16 m above the head box at rest (the Blender base .14 m; the voxel head .015 m).
  - The visual hands hang .43 m out to the side in the A-pose. That only matters before the adapter poses them.

Renders (preview scene, Gamma Editor; local scratch only, never committed):
- All nine presets were rendered front, side, back and three-quarter, both on their own lit Shader Graph materials and with
  a per-renderer approximation of the anime toon material.
- They show what our binder must add:
  - **body-part hiding**: skin pokes through the sleeves and trousers at the elbows and knees without it;
  - **BoZo's colour bake**: every hair is the material's default purple and the texture-only underwear is missing without it.
- The faces read as young anime faces with large eyes.

### Shaders, lighting and outlines
- **Materials:** 165 of the pack's 204 materials (Unity count; 157 of them outfit materials) use
  `BSMC_ClothesCustom.shadergraph` (Built-in Lit target, PBR-ish). The anime look
  is a different material: `Materials/BSMC_AnimeShader.mat` on `Bozo/Bozo_Toon` (the Built-in `BMAC_Toon`), applied to the
  merged character through the `BSMC_AnimeShader` MaterialPackage. Every preset uses it.
- **`Bozo/Bozo_Toon`** is a hand-written `.shader`, not a Shader Graph:
  - Passes: ForwardBase, ForwardAdd, an inverted-hull outline pass (no LightMode tag) and ShadowCaster.
  - Properties: `_Texture2D`, `_NormalMap`, `_IDMap`, `_Color_1`…`_Color_9`, outline (`_UseOutline`, `_OutlineColor`,
    `_OutlineDistance`), `_LightHeightDirectionStrength`, `_Brightness`, rim light (power, sharpness, colour, overlay),
    shadow colour, edge colour, edge and sharpness, `_RecieveShadows`, ambient, multi-light, decal and pattern maps with
    3 colours each.
- **How it reads light:** only Unity's main directional light (`_WorldSpaceLightPos0`, `_LightColor0`, shadow attenuation)
  through a smoothstep ramp with an edge-colour band, plus `ShadeSH9` ambient, rim light and fog. The ForwardAdd pass is
  broken:
  - its keyword `_USEMILTIPLELIGHTS` is misspelt, and the `[ToggleUI]` property never sets it;
  - it treats point-light positions as directions;
  - it swizzles colour `.rbg`.
  `_USEAMBIANT` is also a keyword behind a `[ToggleUI]`.
  None of our lights (picked practical lights, actor lights, muzzle lights, `ToonPoints`) reach it. **Recommendation for
  step 4: a port, not a bridge.** Our own skinned toon shader in the `ArenaToon`/SourceLit family, reading BoZo's inputs: the
  albedo or ID map, 9 colours with vertex-colour layer select, and the outline from vertex alpha.
- **Colour channels:** in the shader, texture RGB weights 3 colours inside a layer and vertex-colour RGB picks the layer
  (1–3, 4–6, 7–9). BoZo's runtime bakes colours into textures instead (below), where the `_IDMap` picks the layer.
  Only 29 of 162 outfit materials have an ID map.
- **Outline:** an inverted hull. Normals are pushed out by `_OutlineDistance` × 1 mm × vertex alpha in object space,
  drawn in `_OutlineColor`, and clipped where vertex alpha < .5. Its width is in metres, so it thins with distance, unlike
  our 1–2 px screen-space map lines.
- **Stencil:** every pass writes `Ref 221, WriteMask 221` (bits 1, 4, 8, 16, 64, 128), so it **sets** bit 16. Our edge pass
  draws where bit 16 is set (`NativeToonEdge` `Ref 16 ReadMask 16 Comp Equal`), so map lines would cross BoZo characters.
  This corrects `MAP-STYLE-ANIME.md` section 6: the shader is not a Shader Graph, and the fix is the port, which writes 0
  to bit 16 like SourceLit and NativeZombieLit.

### BoZo's runtime systems (`OutfitSystem`, `BoZo_CharacterOptimizer`, `BMAC_SaveSystem`)
- **Equip API:** `AttachOutfit`, `RemoveOutfit`, `LoadFromObject`/`LoadFromID`/`LoadCharacter`, `SetShape`, `SetStance`,
  `UpdateMaterial` and `MergeCharacter`. The caller instantiates outfits.
  - Bones are bound by name. Each outfit's renderer bones are remapped onto the body's bones, and `additionalBones` (hair
    chains, head eye bones) are reparented by parent name.
  - Loading is `async void` and Task-based (`Task.Yield`, `Resources.LoadAsync`), not coroutines.
- **Merge, runtime, on every equip, remove and load:**
  - Every active outfit is combined into one new mesh on the body renderer. Bone weights are remapped by name with the legacy
    4-influence `BoneWeight`; bind poses are recomputed. Blendshapes merge by name with frames collapsed. Only UV0 is kept.
  - Colours are not atlased: each outfit's 2048² textures share one UV space and are layered through `Hidden/TextureLayerer`
    into **five 2048² RenderTextures per character** (about 80 MB of GPU memory, so about 640 MB for 8 pilots).
  - Every colour change re-blits all of them, then rebinds and replays the Animator.
  - Outfits with `mergeTexture` off keep their own submesh and material.
  - The atlas code in the optimizer is never called and needs a shader that is not in the pack.
- **Allocation:** every equip instantiates, builds LINQ lists, dictionaries and bone arrays, and uses `DestroyImmediate`.
  This cannot meet Phase A's "0 B GC per frame once warm" or the presentation clock, so **use BoZo's assets, not its
  MonoBehaviours**. Recommendation: our own combine, once per outfit signature (Phase A section g). It covers name
  binding, part hides from tags, `Shape_`/`Tag_` weights, bone-modifier offsets, additional bones, and colours through
  our shader or a once-only bake.
- **Death and dismemberment:** the merged and source meshes keep normal 4-influence weights on named bones. Regions from
  dominant bones map cleanly onto the 11 Phase A regions, and the 14 body parts already split at the elbows, wrists, knees
  and ankles. Stump caps still have to be generated.
- **Saves:** `JsonUtility` `CharacterData` (`versionID` 4): body and face shape ids and values, bone modifiers, outfit ids
  (`"<Type>/<prefab>"` Resources paths), colours, decals, patterns, part visibility, stance and material package name. Files go
  to `persistentDataPath/BoZo_StylizedModularCharacters/CustomCharacters`. Ids are names, never GUIDs, which suits Astra's
  `look` map (CHARACTER-LOWPOLY section i) if we keep BoZo's ids.
- **Animation:** six Humanoid clips (idle and walk, male and female, a dizzy clip), expression clips on
  `blendShape.Expression_*`, and `HeeledFeet`. No Animator is required. Our poses drive the bones, and expressions could be
  driven by us.
- **Builds:** BoZo's `Resources` folders ship whole (about 174 outfits, 287 textures at 2048²), and Always Included adds
  its shaders. A build with the packs grows accordingly unless our own bake writes only what we use. That is
  Astra's build tooling.
- **Assemblies:** BoZo has no asmdef, so it compiles into Assembly-CSharp, as all our game code does (this project has no
  asmdefs). Any direct reference to BoZo types from tracked code breaks CI. At runtime we need Unity types only (meshes,
  materials, textures); an Editor-side baker can read BoZo data through `SerializedObject`.
- **Editor tools broken by the move (we don't need them):**
  - TextureBaker would write into a recreated `Assets/BoZo_StylizedModularCharacters/CustomOutfits/Resources`;
  - IconCapture's path rewrite breaks on `LocalAssets`;
  - the legacy CharacterObject upgrade fails;
  - one inspector banner is missing.
  The `Resources` loads and saved ids are unaffected.

### Magica Cloth 2
- **Cloth types:**
  - BoneCloth (Transform chains under root bones): the fit for BoZo hair, which brings its own chains (`Hair_Front`,
    `Hair_Back`…), and for `Cloth_Cape`.
  - BoneSpring: no gravity, a spring back to the pose; for stiff accessories.
  - MeshCloth (a reduced proxy of a readable skinned mesh): for skirts, dresses and other loose garments. Only 7 skirts and
    dresses ship a paint map (`MC2Map`); since the owner wants cloth on every applicable piece, the others need maps
    painted on BoZo's UVs, which stay local with the pack. It clones the mesh, uploads all vertices every frame and swaps the original back when culled; its build is
    asynchronous over several frames and allocates heavily. Not measured; it is the main cost risk.
- **Runtime build:**
  1. `AddComponent<MagicaCloth>()`, `Initialize()`, `DisableAutoBuild()`.
  2. Set `SerializeData` (cloth type, root bones or source renderers, paint, reduction, colliders).
  3. Load presets with `ImportJson` (parameters only).
  4. `ReplaceTransform` for name remaps, then `BuildAndRun()`. It runs once; a rebuild means a new component.
  Other calls: `SetParameterChange`, `ResetCloth(keepPose)`, `SetTimeScale(0–1)`, `SetSkipWriting`, `AddForce`; a
  disable mode of Reset or Keep.
- **Colliders:** sphere, capsule and infinite plane. Collision mode is Point or Edge; there is no per-cloth limit, but each
  particle checks every collider. Keep self-collision off (it is expensive). BoZo's base prefab adds 13 capsules to body
  bones.
- **Culling:**
  - Camera culling: Off, Reset, Keep, or follow the Animator (the default). It uses `Renderer.isVisible`, which includes
    shadow cameras and lags a frame, so first-person cloth must be switched off explicitly.
  - Distance culling: off by default; up to 100 m, from `Camera.main` by default, with a reset on crossing.
  - No LOD tiers. The levers are frequency, step cap, reduction, blend weight, time scale and disabling.
- **Time:** 90 Hz by default (30–150), at most 3 steps per frame. It hooks the PlayerLoop around LateUpdate and steps from
  `UnityEngine.Time` (scaled, unscaled or fixed); **manual stepping from our presentation clock is not possible without
  changing its source.** It is not deterministic: step counts follow frame time and build timing varies. Killcams, corpses
  and intros re-simulate locally (presentation only, nothing networked), but a replay's cloth will not match the live one
  exactly. Use `ResetCloth` on respawns and cuts, and Keep or `SetSkipWriting` to freeze corpses.
- **Burst jobs:** steady-state managed allocation looked absent in the hot paths read (not profiled).
- **Platforms:** the readme says every platform except WebGL; there are no shader or SRP dependencies.
- **BoZo's integration** (`ThirdParty/MagicaCloth`, `#if MAGICACLOTH2`): four cloths on `BSMC_CharacterBase` — Cape
  (Bone, Skirt preset), FrontHair and BackHair (Bone, ShortHair preset; BackHair off under a hood), and one MeshCloth (Skirt
  preset, the outfit's `MC2Map`). It destroys and rebuilds them after every merge. Use it as a reference only.

### What this changes in the plan
- **Optional compile.** Prefer no compile-time dependency at runtime: render BoZo data with Unity types; let an Editor or
  load-time baker turn it into our combined meshes in `LocalAssets`; fall back to voxel when the bake is absent. Only cloth
  needs Magica types at runtime. Put that bridge in its own asmdef referencing `MagicaClothV2`, with `defineConstraints` on
  a local define. Our Assembly-CSharp code reaches it through a registration hook, never by type.
  - Don't use `MAGICACLOTH2` as the guard: it is written into the tracked ProjectSettings.
  - Candidate: a git-ignored `Assets/csc.rsp` with `-define:CHIPARENA_LOCAL_BOZO`, written by a local install step.
  - Still to verify in the Editor: that `defineConstraints` sees `csc.rsp` defines.
- **Budget (owner, 2026-10-22: "as much as you need").** BoZo characters draw as authored, with no triangle cap; Phase A's
  caps (LOD0 7,000, 22 bones, 1 submesh) do not apply.
  - Today's dressed presets draw 17–26 k triangles, with about 90 bones and 4 influences.
  - LODs are optional, made only if the Phase C benchmark calls for them. The pack has none; the Editor's mesh LOD
    generation is one candidate, not verified on 6000.3.
  - Texture memory: one bake per outfit signature at BoZo's 2048², not five live 2048² RenderTextures per character, so the
    look is unchanged.
  - The Phase C pass criteria are unchanged: +0.5 ms CPU and +5 % GPU over voxel at 8 pilots, and 0 B GC per frame.
- **Shader:** port (above). Then match the map `lineColor` and width to the port's outline.
- **First person:** the view rig needs the lower arms, wrists, hands, and the sleeves and gloves weighted to `lowerarm`/`hand`.
  Hands are 2,976 triangles. Cloth is off.

### The local inventory tool
`Assets/LocalAssets/ChipArenaLocalTools/Editor/BozoInventory.cs` is git-ignored with the packs.
- **How it runs:** when `Temp/bozo-inventory.request` exists (its text names an output folder), or from Tools > Chip Arena
  (local).
- **What it does:** it builds each preset in a preview scene. It binds the outfits to `BodyRig` by bone name, after
  removing BoZo's own scripts before activation. Then it measures the result, reports shaders, materials, models and outfits,
  and renders the views.
- **Run on 2026-10-22:** Editor 6000.3.24f1, Built-in, Gamma. The standalone defines read `MAGICACLOTH2`, which Magica
  had added. Every section ran without errors.
- **Edits only compile on Editor focus,** but its request poll also runs in the background. A request written before the
  recompile therefore runs the old code. Its per-part width pass (the same measurement as the FBX widths above) is in the
  file for the next focused run.

## Step 3: rig adapter (`claude/bozo-rig-adapter`)

Presentation only. Nothing here reads or writes authority:
- the capsule, head box, melee sector, shot and aim origins, weapon frames, the network pose and every wire field stay on the
  22 `CapturedRig` joints;
- hit timing, hitboxes, damage, cooldowns, movement and networking are unchanged.

The adapter is not wired into the live body path yet; that is step 4.

**Files.**
- `Core/NativeVisualRig.cs`: the adapter. Pure System.Numerics in the source frame and the `Pose`'s body space (floor −.865).
  It allocates nothing per frame.
- `Runtime/NativeVisualRigBinding.cs`: binds it to a Unity bone hierarchy. It reads the rest pose once, mirrored into the
  source frame, and writes local turns plus the pelvis position each frame. It uses Unity types only and names no pack
  types, so it compiles on machines without the packs.
- `Tests/VisualRigChecks.cs` (`CoreTests --visual-rig`): checks the adapter against a synthetic Unreal-mannequin skeleton
  built from our own numbers, with no pack data.

**Bind (once per skeleton).**
- **Mapping:** bones are mapped by name (`NativeVisualRig.Mannequin`) using the table in step 2:
  - pelvis;
  - `spine_01`–`02` share the spine's turn half and half; `spine_03`–`05` share the chest's 34/33/33; `neck_01`–`02` share
    the neck's;
  - head, clavicles, upper arms, forearms, hands, thighs, calves, feet and balls one to one;
  - everything else rides its parent.
- **Rest alignment:** upper arms, forearms, thighs and calves are turned so they point down the game's rest directions
  (arms and legs straight down). BoZo's A-pose then lines up with the game's arms-down rest. Hands and fingers follow their
  forearm. Pelvis, spine, neck, head and clavicles keep BoZo's authored posture. Feet keep their flat world rest.
- **Leg ratio:** the visual hip-to-ankle over the game's .50 m (BoZo: see the measurements below).

**Per frame** (`NativeVisualRig.Pose`):
1. **Joint turns:** each bone's world turn is the game joint's world turn (identity rest frames, so the turn since rest)
   applied to its aligned rest. Split chains slerp the joint's local turn by their share. Arm and leg directions therefore
   equal the game's exactly. Hands, feet and head carry the game joint's turn.
2. **Twist helpers:**
   - forearm helpers take the hand's roll, and calf helpers the foot's, by their place along the bone;
   - upper-arm and thigh helpers counter the limb's own roll: all of it at the root (`shoulder_twist`), none at the far end.
3. **Root:** the pelvis moves by the game pelvis's offset from rest × the leg ratio, so crouches and bobs keep the game's
   knee angles.
4. **Planted feet:** two-bone IK puts a planted foot's ankle on the game ankle's ground point, with the knee toward the
   game knee's side. A foot counts as planted while the game ankle is within .03 m of its rest height, blending out by
   .10 m. Turn this off for rolls and airborne takes.
5. **Support grip:** on a weapon that may be held two-handed, the left hand keeps the game's right-to-left wrist offset,
   carried onto the visual right wrist. It solves two-bone IK and keeps the game wrist turn.
   - It engages only while the game's own wrists hold together (within .18 m, released by .30 m;
     `NativeVisualRig.HandsTogether`). A pistol run or a roll that swings the left hand free stays FK.
6. **Weapon (P1):** weapons draw at `WeaponFrame`: the visual right wrist's position with the game wrist's turn. Today's
   weapons draw at the game wrist matrix with the grip baked into their geometry, so the drawn weapon moves with the visual
   hand while its aim stays the game's.
7. **Floor fit:** optional. For rolls and tucks, it lifts the body until every bone clears the floor by its rest height
   (capped at .12 m). It only ever lifts, like `Pose.Lowest()`.

**Checked** (`CoreTests --visual-rig`, 43,217 checks), over:
- every captured clip in the four melee sets (8 samples each);
- the gun gait: pistol, rifle and machine gun in idle, aimed, run, sprint and roll, 647 poses in all.

On the synthetic skeleton (leg ratio 1.703):
- **Bind:** the limbs point straight down at the game's rest, keep their lengths, and the trunk, head and feet keep their
  authored turns.
- **Per pose:**
  - every limb's direction equals the game's (within 0.11°), and split chains end on the game joint's turn;
  - hands, feet and head carry the game turn, and the root moves by the leg ratio;
  - twist helpers take exactly their share of the roll;
  - all 524 planted feet reach the ground point, with the knee to the game knee's side;
  - all 351 two-handed grips with the hands together reach, at up to 93 % of the arm;
  - the support hand keeps the game wrist turn, and the grip is the game socket on the visual wrist;
  - the locals rebuild the pose.
- **Roll:** the floor fit keeps every bone over the floor.
- **Runtime:** no allocation over 1,000 frames, bit-for-bit repeats, and the binding mirrors at the boundary and names no
  pack types.

The synthetic skeleton's visual grip sits 0.37 m (mean) from the game's. That is the expected P1 cost, because its shoulders
are narrower and higher than the voxel rig's.

**Measured on BoZo in Unity:** pending the next Editor refresh. The local tool poses BoZo's skeleton with outfit A through
this adapter on 13 gun and melee poses, measures the drift of the head, wrist and muzzle against authority, and renders them.

## Decisions and design carried from the stopped Phase B branch

From `claude/lowpoly-base-model` (`5c3d6ae`, not merged; CHARACTER-LOWPOLY there, sections c, j and k), adapted to BoZo.

- **Proportions (owner, 2026-10-20, "Option B in principle").** The visual body has human proportions on a presentation-only
  visual skeleton driven by the game's poses. With BoZo, its skeleton is the visual skeleton. The authoritative rig is
  unchanged: `CapturedRig`, 22 joints, motion-root at feet + .865 m, a stocky 1.70 m layout, the capsule (radius .42, half
  height .85), the head box, melee sector, shot origin, aim origin, weapon frames and the network pose.
- **Adapter steps** (Phase B design; step 1 now needs the rest alignment above):
  1. Carry the game pose's joint rotations onto the visual joints.
  2. Visual root height = rest height + the game root translation × `legRatio` (1.64 for the Blender base, about 1.70 for
     BoZo), so crouches and bobs keep the game's knee angles.
  3. Planted feet (within about .03 m of the floor) use two-bone IK onto the game ankle's ground point, with the knee toward
     the game knee's plane, blending out as the foot lifts. The `Lowest()` floor fit is evaluated on the visual joints.
  4. Weapons ride the visual right hand at the same socket (P1). Two-handed holds IK the left hand to the game's
     right-to-left hand offset. One-handed weapons, empty hands and casts stay FK.
  5. Authority stays as it is.
- **Drift, measured for the Blender base** (1.74 m, 8 heads; to be re-measured for BoZo in Unity):
  - The visual head stays as close to the authoritative head box as the voxel head does (within .01–.06 m in every clip).
  - The weapon on the visual hand sits .21–.34 m from today's drawn frame. The visual muzzle is .26–.40 m from the shot
    origin at idle and aim (voxel .12–.18 m). Tracers already converge within 4.5 m (`NativeShotLaunch`), and hits are
    unaffected.
  - Support-hand IK needs .31–.33 m, within 87 % of reach. Foot IK needs up to .29 m, within 92 % of reach.
- **Proposals (owner chose P1 on 2026-10-22):**
  - **P1** (Claude, chosen): the weapon on the visual hand.
  - **P2** (Claude): the visual right hand IKs to today's weapon frame.
  - **P3** (Astra, authority, only if the owner wants hits to match the taller body): raise the head box and shot origin.
  - **P4**: corpse, wound, clearance, hat and claw code take fit volumes from the model instead of voxel box sizes.
- **Who reads joint positions today:**
  - Authoritative, unchanged: the capsule, the headshot box, the melee sector, the shot and aim origins.
  - Presentation that follows the visual skeleton: weapon sockets (`NativeKillBoxes.RightHandSocket`), muzzle FX,
    footfalls, the cinematic head, chest glows, the grab claw, corpses and gibs (`CapturedRig.Boxes`), wounds, arrows,
    band/armour/hat anchors, blade clearance, and the first-person view rig (elbows and hands).
- **Secondary motion (Phase B section k).** Nothing networked; never in first person. Off on the Low tier and with Reduced
  effects. Frozen beyond 15–20 m. A budget for 8 pilots. Killcams, replays, spectators and corpses re-simulate locally, and a
  corpse's chains settle then freeze with the ragdoll's sleep. Garment chains use the body's fit volumes as colliders.
  Magica Cloth 2 replaces the proposed custom spring solver. Its fixed sub-steps come from Unity time, not our presentation
  clock (above).
- **Owner's art direction (2026-10-20/21; the reference images are the owner's, not in the repo):**
  - Body: lean athletic, broad shoulders, V-taper, not overly muscular. The brief's "about 7.5–8 heads tall" is superseded
    for BoZo: on 2026-10-22 the owner chose BoZo's normal, unscaled head, which puts the figure at about 6.6 heads.
  - Face: a human face, not a mask; natural eyes without eyeliner; hair sits on the scalp.
  - Silhouette: clean stylization, not blocky or over-carved.
  - Maps: anime/toon with a cool, saturated shade palette (`MAP-STYLE-ANIME.md`).
  - **BoZo as authored (owner, 2026-10-22):** keep BoZo's design exactly as it is: body, head, face and eyes, with the
    shapes and bone modifiers its presets set. Where the brief above differs (head size, "natural eyes"), BoZo's design wins.
- **Pack choice:** BoZo over P09 Modular Humanoid Lite (2026-10-21).
  - Asset Store claims now checked:
    - about 104 items: the pack has 166 outfit prefabs, many of them texture layers;
    - male/female blend: yes, `Shape_BodyType` plus stance;
    - Mixamo-compatible humanoid: yes, with hair on its own chains;
    - toon shader with 9 colours and an outline: yes, but hand-written, and the URP version is the default;
    - merge to one mesh: yes, at runtime, with layered RenderTextures rather than an atlas;
    - JSON/ScriptableObject save: yes;
    - Built-in and URP: yes, with the bundled Built-in shader package;
    - Magica support: yes;
    - about 19 k triangles: 17–26 k drawn per dressed preset.
- **Kept from the stopped base:** the chest core and right-wrist device (closed / opening / insert / accepted / active).
  Their geometry is in `build_male_base.py` on that branch and can be exported as attachments on BoZo's `spine_04`/`spine_05`
  and `hand_r`/`lowerarm_twist_01_r`.
- **Builds with BoZo** are made on the owner's PC (with `Assets/LocalAssets/` populated). Cloud and CI builds show the voxel
  fallback. Extending the LocalAssets install for builds is Astra's tooling.
- **PC setup used:** `git worktree add -b claude/bozo-inventory ../Chip-Arena-unity origin/claude/sleepy-ritchie-g8ruhv`,
  opened in Unity Hub with 6000.3.24f1. The original checkout stays on `main`.

## Owner decisions after the inventory (2026-10-22)
- **Design:** keep BoZo's design exactly as it is: body, head (normal size, about 6.6 heads), face and eyes, with the
  presets' own shapes and bone modifiers. No head scale, no extra body shaping.
- **Budget:** no triangle cap for BoZo characters ("as much as you need"). The Phase C frame-time and GC criteria still apply.
- **Weapon placement: P1.** The weapon rides BoZo's visual right hand. P3 (authority) is not requested. P4 (fit volumes)
  stays a step 6 task.
- **Cloth: on every applicable piece.** Hair chains, capes, skirts, dresses, coats, hoods and other loose clothing. The
  standing rules still hold: off in first person, never networked, a budget for 8 pilots, tier and distance culling.
- **Default outfit: the owner's "Player model concept" sheet** (an image the owner shared on 2026-10-22, not in the repo).
  It describes:
  - short spiky dark hair;
  - a lower-face tech mask with a glowing visor band over the eyes;
  - a black jacket over a black tee, with a light-grey hood and trims and a glowing chevron on the chest;
  - a glowing emblem on the back (a smiley with X eyes);
  - forearm guards and armoured fingerless gloves, with glow strips;
  - black cargo joggers with straps, pouches on the belt, and knee plates with glow;
  - chunky black-and-white high-top sneakers with glowing strips.

  The sheet sets the rest of the look too:
  - **Glow colour:** the glow is the player's colour (cyan, red, purple, green, orange, yellow, pink or white).
  - **Same base for every class:** ranged, melee and magic.
  - **Customisation examples:** hood up, cap, headphones, mask variants, an antenna or tech piece.
  - **Scale:** about 1.8 m beside a zombie, which matches BoZo's 1.79 m.
  - **Style:** the sheet says "low poly", but BoZo's design stays as authored; the sheet sets the outfit, palette and glow,
    not the mesh style.

### Default outfit: concept against BoZo
**Owner choice (2026-10-22): candidate A**:
- `HairFront_Messy` + `HairBack_PineappleCut`;
- `Top_OpenHoodie`;
- `Bottom_BeltedPants`;
- `Feet_AthleticMidTop`;
- `Gloves_FingerlessGloves`.

It was rendered against candidate B (hard jacket, baggy pants, Wild Locks) in the concept's black, grey and cyan, with the
covered body parts hidden. A draws about 20 k triangles. The base pieces come from BoZo, and the tech pieces and glow are
ours to author.

| Concept element | From BoZo | To author |
|---|---|---|
| Short spiky dark hair | `HairFront_Messy` + `HairBack_PineappleCut` (or `HairBack_WildLocks`) | colour only |
| Jacket over a tee, light hood | `Top_OpenHoodie` (or `Top_HardJacket` for the high collar) | glow strips, the chest chevron |
| Back emblem | BoZo's decal system can place it | the emblem texture (our own art) |
| Cargo joggers with straps | `Bottom_BeltedPants` (or `Bottom_BaggyPants`) | knee plates, belt pouches, glow strips |
| High-top sneakers | `Feet_AthleticMidTop` | glow strips |
| Fingerless gloves | `Gloves_FingerlessGloves` | forearm guards with glow |
| Tech mask with glowing visor | none | mask (LowerFace) and visor (UpperFace), with variants |
| Glow in the player colour | none: BoZo's toon shader has no emission | an emission mask and player colour in our ported shader (step 4) |
| Hood up, cap, headphones, antenna | `Hat_BallCap`; `HeadAcc_AlienAttena` is a cartoon antenna | hood-up, headphones, tech antenna |
| Chest core, right-wrist device | none | kept from the stopped base |

**Where the pieces live.**
- Our own meshes (mask, visor, guards, plates, headphones) skinned to BoZo's bone names, and our own emblem art, contain no
  licensed content.
- Glow masks painted on BoZo's UV layouts, and BoZo-derived recolours, stay local with the pack.

## Open owner decisions
- **DEV-only pieces:** which BoZo pieces are DEV-only (the Sheldon adaptation of the "Sheldon Clothes" robe and the "Sheldon
  Head" hood).
- **From Phase A section j, still open:**
  - right wrist for every class;
  - a skin-tone palette or free colours, and whether faces and hair need entitlements;
  - today's hats on BoZo heads;
  - team identity (bands or a paint channel);
  - whether zombies stay voxel (recommended);
  - the backend allowlist deploy (a Sites deploy with owner approval).
- **Female body:** BoZo blends with `Shape_BodyType`. When it follows the male is undecided.
- **For Astra:**
  - the manifest and catalog change;
  - packaging guards against the packs' default import folders;
  - builds that need `Assets/LocalAssets` content;
  - the Editor running Gamma while builds force Linear.
