# BoZo characters + Magica Cloth 2: integration plan

Status (2026-10-22, Claude): steps 1–2 done on the owner's PC (`claude/bozo-inventory`, docs only, merged into shared
after the owner's review): both packs imported, moved under `Assets/LocalAssets/`, and inventoried below; the owner's
decisions follow the inventory. Step 3 (rig adapter): `claude/bozo-rig-adapter`, merged into shared; its drift on BoZo is
measured in Unity (step 3 below). Step 4 (first character): outfit A is wired into the arena's pilot bodies
(`claude/bozo-body`, below), drawn on our own lighting (`claude/bozo-toon`) with its outline matched to the map's lines
(`claude/bozo-outline`), with the hit flash and the spawn-in (`claude/bozo-flash`), the chest core and the right-wrist
chip device (`claude/bozo-core`); the chip-select display's move to the right wrist, hats and the other rigs are still to
do. Owner decision: player characters come from the **BoZo: Stylized Modular
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
- **Shader:** ported in step 4 (`ChipArena/VisualBodyToon`), its outline matched to the map's `lineColor` and width.
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
   equal the game's exactly, until the IK steps below. Feet and head carry the game joint's turn.
   - **Hands** (`NativeVisualRig.Wrist`, owner review 2026-10-22): the game's wrist frame is the voxel fist's and the
     weapon's, so holds bend it 90–150° off the forearm (rifle 107°, pistol 112°, greatsword idle 152°). An open BoZo hand
     there pointed at the floor.
   - The visual hand keeps the game wrist's roll in full and its bend up to 25°. Beyond that it takes a quarter of the
     bend: a rifle hold becomes 45°, and the deepest captured bend is now 60°.
   - The weapon frame keeps the game wrist turn, so aim and the drawn weapon are unchanged.
   - **Hanging arms** (`NativeVisualRig.Spread`, owner review 2026-10-22): the game hangs its arms straight down from voxel
     shoulders .35 m out. From BoZo's shoulder joints (.13 m out) the same arms buried the forearms and hands in the hips.
     - While a game upper arm hangs, the visual shoulder turns it 15° out, in the clavicle's frame so the arm's own roll is
       kept.
     - The turn fades out as the arm rises past 60° from down, so raised arms and weapon holds stay exactly the game's.
     - It carries down the limb, because limb chains hang from their visual parent.
   - **Fingers** (`NativeVisualRig.Curl`): each finger and the thumb curl by joint.
     - The curl axis comes from the chain's own slight rest curl, so no pack bone axes are assumed.
     - Angles: relaxed 15° / 25° / 15° (thumb 5° / 10° / 10°); closed round a grip 55° / 75° / 45° (thumb 20° / 30° / 30°).
     - The caller passes `rightGrip` (the weapon) and `leftGrip` (a shield, an off-hand blade). The left also closes while
       it supports a two-handed grip. Open hands relax.
2. **Twist helpers:**
   - forearm helpers take the hand's roll, and calf helpers the foot's, by their place along the bone;
   - upper-arm and thigh helpers counter the limb's own roll: all of it at the root (`shoulder_twist`), none at the far end;
   - arm helpers take these from the final pose, after the hands and IK (step 7).
3. **Root:** the pelvis moves by the game pelvis's offset from rest × the leg ratio, so crouches and bobs keep the game's
   knee angles.
4. **Hands inside the shoulder line** (`NativeVisualRig.Inside`; owner reviews 2026-10-22: the elbows looked inverted,
   then a first fix drew the arms in). The game's shoulders sit .35 m out from its spine, BoZo's shoulder joints .13 m.
   Copying the arm directions crossed BoZo's forearms whenever the game brought a hand in (pistol and rifle holds, the
   greatsword). The support arm then folded back out to its grip: its forearm pointed 115–177° off the game's.
   - **Sideways:** inside the game's shoulder line, each visual hand keeps its place from the chest's middle, scaled from the
     game's shoulder width to the visual one.
   - **Height:** the game's head joint rises .24 m over its shoulders, BoZo's head bone .15 m. A hold at the voxel's chest
     (a rifle .34 m under the head-box centre) put BoZo's hands at its mouth. Between the shoulder and the head, the hand's
     rise scales the same way. The drop peaks at the head's height and is gone by twice it, so raised swings stay raised.
   - **Reach:** the moved hand is drawn back out to the copied arm's extension (how straight the arm is) along its own
     direction. Solving the depth alone went steep near its limit: a raised greatsword's hand jumped 0.4 m in a frame.
   - **Elbow:** the whole arm swings at the shoulder by the shortest turn that brings the hand there, so the elbow keeps
     the game's bend exactly and the hand keeps its turn.
   - The shift fades in over the first .10 m inside the line. Hands at or outside it (hanging arms, swings out to the side)
     keep the copied arm.
   - The weapon rides the moved right hand (P1), and the support grip follows it.
   - **Ruled out on the way** (none of these merged):
     - The twist helpers: renders with the upper-arm counter-twist at full, a third and none looked alike.
     - A sideways shift alone: it bent the arms and drew aimed hands back to the chest and face (rifle arm 67 % extended
       against the game's 99 %).
     - Two-bone IK in place of the swing: it re-solved the elbow plane and turned the greatsword's weapon forearm up to
       91° off the game's.
5. **Fists and weapons out of the body** (`NativeVisualRig.Clear`; owner review 2026-10-22: the greatsword's fists and
   hilt in the face, long-gun stocks in the chest). The game keeps its blades off its own blocky body (`MeleeClearance`),
   not off BoZo's. A different head and chest, and hands moved inside the shoulder line, ran fists and weapons into them.
   - **The body:** the head is an ellipsoid and the torso an elliptic cylinder from the pelvis up to the shoulders
     (`Rig.HeadCenter`, `HeadRadii`, `TorsoHalfWidth`, `TorsoHalfDepth`). They are set from the skeleton by the proportions
     measured on outfit A, and a caller with the meshes may set its own:
     - head .22 × .27 × .23 m round a point .08 m over the head bone, which rises .148 m over the shoulder joints;
     - hooded top .30 × .22 m round the spine.
   - **What keeps out:**
     - each fist, turned as it will ride its handle: 8 cm from the head, which keeps the back of a closed hand out, and
       5 cm from the torso, where a hand may rest;
     - the held weapon's lines (`Hold.Tail` to `Tip`, `CrossA` to `CrossB`, each with its half-width): a blade from its
       pommel to 20 cm past its guard plus the crossguard bar; a gun from its back to its muzzle; a shield's height and
       breadth;
     - a two-handed weapon's support fist, at its handle, with the weapon hand's margins.
   - **How it moves:** fist and weapon move as one, straight out by the inside points' pushes averaged by depth. The arm
     reaches the wrist there, hinged, with the elbow on the animation's side, and the hand keeps its turn. Only that arm
     moves: the weapon rides its fist and a support hand follows the weapon. The fist is estimated as it will ride (its turn
     steadied from last frame's hand, as the hold turns it).
   - **Limits:** the move changes each frame by at most 3 cm more than the game's own hand moved. It keeps up with a cut
     that brings the hands to the head, and the greatsword's overhead raise passes them over it.
     - All at once, it jerked the greatsword's hands 16 cm and its support hand 40 cm in a frame.
     - Held to 3 cm a frame (owner review 2026-10-22: "greatsword cut A raised-hands face graze"), the raise's fists
       reached the head's middle while the game's hands moved 7–10 cm a frame. The push then flipped and the fists crossed
       the face (6 cm deep for two frames).
     - The game's head drop was not the cause: there the game's hands rise only 60 % of the way to its head.
     - BoZo's head sits low on a short neck, so the voxel's hands, which sweep under its chin, come at BoZo's jaw.
   - **Ruled out:**
     - a single deepest point: it flipped between two near-equal contacts frame to frame;
     - reaching with the elbow history: it was measured from a different elbow than the hold's reach after it, and rocked
       the elbows 5–7 cm a frame;
     - clearing a long blade's outer part: it sweeps past the head within a frame or two and jerked the hands 13 cm;
     - pushing the way the game's hand lies from its own head (under the chin, as the voxel's go): it held clear, but moved
       the raise's hands down by up to 30 cm, where the straight-out push moves them over the head by at most 12 cm;
     - keeping the wrist off the head too: it made the support hand jump 36 cm in a frame on the synthetic skeleton.
6. **Planted feet:** two-bone IK puts a planted foot's ankle on the game ankle's ground point. The knee bends toward
   where it is in the visual pose before IK, falling back to the game knee's side when that is degenerate. A foot counts as planted while the game ankle is within .03 m of its rest height, blending out by
   .10 m. Turn this off for rolls and airborne takes.
7. **Arms, kept anatomical** (`NativeVisualRig.Reach`; owner review 2026-10-22: arms and wrists still unnatural, hands
   not held through animations). Every arm the hands move (weapons, support grips, give-way) is posed here. Measured on
   the merged adapter, two causes were in the solve, not the animations or Unity:
   - **Inverted elbows:** the IK turned each bone the shortest way, so the upper arm's crease drifted off the fold, up to
     175° (rifle and machine-gun weapon arms; 40-60° on pistol and shield). Now the elbow is a hinge: the upper arm's
     fold side (+z of the game-aligned frame, where the game's own elbow folds) faces the forearm, and the forearm shares
     the hinge (`HingeTurns`).
   - **Twisted forearms:** the forearm twist bones kept the game's wrist roll after the hand was turned onto a handle, up to
     147° off. Arm twist bones now follow the final pose (`ArmTwists`): forearm bones take their share of the hand's roll
     on the forearm, and upper-arm bones counter the upper arm's roll on the shoulder.
   - **Elbow placement:** the elbow sits on the circle the bones allow, on the animation's side, swinging about the
     shoulder-wrist line only to spare the wrist a bend past 60°, the forearm a roll past 90°, the humerus a roll past 110°
     and the torso an elbow or forearm inside it. The swing is a soft minimum over the circle (at most ±90°). The
     greatsword's cuts need it: the game's own arm there rolls the upper arm 153-169° in the shoulder, past any human's.
   - **Torso:** an elbow or forearm resting against the torso line costs little (2 cm, as a 24° swing), sinking in steeply
     more (5 cm, as a 97° swing: the depth squared plus its fourth power). With the square alone a dagger cut's elbow sank
     9 cm into the belly.
   - **Frame to frame:**
     - The elbow moves with the animation's own elbow, keeping its correction (the swing off the animation's side), and only
       the correction is held and changes by at most 4° a frame. Measured from last frame's elbow alone, a fast dagger cut's
       elbow trailed the animation's by 68°, into the torso.
     - It follows only while the animation's side is sure and has moved under 45° since last frame; otherwise it stays with
       last frame's elbow. A straight arm names no side, and blending the two set a greatsword's support elbow circling 20°
       a frame.
     - A riding hand rolls about its handle by at most 8° a frame. Two-handed and shouldered weights move by at most .08 a
       frame.
     - The game can drop a two-handed grip, or a cut can whip the forearm across the handle's line, within a frame; these
       keep hands and elbows from snapping.
     - `State.Forget()` clears the history. The game body calls it after a gap in posing (death, first person, a hidden
       frame), a teleport or a weapon change. It does not call it on a cut: the transition checks cross cuts smoothly
       without it.
8. **Support grip:** on a weapon that may be held two-handed, the left fist closes on its `SupportHold` (without one, the
   left hand keeps the game's right-to-left wrist offset, carried onto the visual right wrist).
   - It engages while the game's own wrists hold together (within .18 m, released by .30 m; `HandsTogether`), eased.
   - **Reaching a far grip:**
     - the fist slides back along its handle (`Hold.Slide`: a long gun's handguard toward the receiver);
     - then the left shoulder reaches, as a body does (the clavicle turns toward the grip, up to 25°);
     - only what is still out of reach makes the weapon give way toward the left shoulder, with the right fist staying on it.
   - **The fist's turn** is set by the weapon where it has one (`Hold.Ahead`, rolling up to `Hold.Spread` toward the
     straightest wrist: a long gun's palm-up cup ±60°, a pistol grip ±25°), else by the straightest wrist.
   - **A long gun's handguard is cradled** (owner review 2026-10-22: rifle and machine-gun wrists): the palm lays it across
     the hand, the fist's axis turned 40° toward the gun's left (`Hold.Lay`; `Cradle`). A fist closed along the barrel
     bent BoZo's support wrist 91° as the arm reached along it; cradled, 46°. The fist still slides along the barrel.
     - It is aimed from the animation's forearm, the arm reaches, then four passes re-aim it halfway for the forearm the
       reach made.
     - Hand and forearm can't always agree: the fist sits ~9 cm off the wrist, so turning it swings the forearm, and some
       greatsword cuts have no turn straightest for the forearm it causes. A fixed number of smooth steps stays continuous
       frame to frame.
     - The last pass places the wrist so the fist sits on its grip exactly.
9. **Fists on handles** (`NativeVisualRig.HoldWeapon`; owner review 2026-10-22: the hands weren't holding the weapons).
   The game's wrist frame is the voxel fist's and the weapon's: a gun's raked pistol grip hangs 13-18 cm under the wrist,
   and a melee grip runs along the blade through the wrist. A voxel fist is a box over that spot.
   - **The fist, measured at bind** (`Rig.Fists`): each finger at its closed curl wraps the handle, so the handle runs
     through the centres of the circles its three joints draw. That gives the fist's centre, its axis (toward the thumb)
     and the hand's forward, from bone positions only.
   - **The weapon rides the hand (P1):** the weapon hand stays where the animation put the wrist. Its fist lays its axis
     along the handle with the straightest wrist the handle allows, and the weapon frame is placed so its grip sits in
     that fist. (Moving the hand to a weapon left at the old wrist bent the aiming arm to 81° against the animation's 28°.)
   - **Shouldered long guns:** while the game holds a rifle or machine gun up (its stock butt within .30 m of the game
     shoulder, fading by .45 m), the stock butt sits at the visual shoulder pocket, along the game's aim. The pocket is just
     inside and under the shoulder joint, on the body's front: `Rig.ShoulderFront`, measured .13 m ahead of BoZo's joint
     with the hood, less 2 cm where the butt presses in. At .05 m ahead the stock sat 8 cm inside the chest. The right fist
     reaches for its pistol grip (knuckles toward the muzzle, ±25°).
   - **The shield is braced** (owner review 2026-10-22: the arm through the shield). The game's forearm runs along the
     shield's back 15 cm behind its grip bar, so a shield moved onto BoZo's fist put the forearm inside it, out through
     the face. The shield now keeps the game's place on the wrist (its frame at the fist the animation gives), and the fist
     reaches back to the bar (`Hold.Braced`).
   - **Daggers** are held in a reverse grip (the blade out of the little-finger side): fresh solves chose it for 92 % of
     their poses, the only way round their forearms roll naturally. A fixed grip cannot flip mid-cut (the earlier
     either-way choice flipped a hand 176° in a frame).
   - **The handles** (`Blade`, `GreatswordSupport`, `ShieldBar`, `PistolGrip`, `GunSupport`, `Stock`; `Melee(spec)` adds
     a melee weapon's extent) follow `NativeWeaponModels`. Melee holds take `NativeSpecialPose.HeldRotation` (identity outside special moves). The shield
     draws on the right hand (its clips are mirrored); its fist holds the grip bar.
   - Callers pass a `Grasp` (finger curls, both hands' holds, the two-handed weight, support hold and stock). The older
     `Pose` form seats nothing.
   - Weapons draw at `WeaponFrame` (the off-hand's at `LeftWeaponFrame`): the frame placed in the fist, with the game
     wrist's turn, so the drawn weapon follows the visual hand while its aim stays the game's.
10. **Floor fit:** optional. For rolls and tucks, it lifts the body until every bone clears the floor by its rest height
   (capped at .12 m). It only ever lifts, like `Pose.Lowest()`.

**Checked** (`CoreTests --visual-rig`, 129,260 checks since the raised-hands fix; 43,217 at first), over:
- every captured clip in the four melee sets (8 samples each);
- the gun gait: pistol, rifle and machine gun in idle, aimed, run, sprint and roll, 647 poses in all;
- 2,492 transition frames at 60 Hz: every melee set from idle crossfading into each cut and the block, through it and back
  to idle, and every gun through idle, aim, firing, a run and back to aim.

On the synthetic skeleton (leg ratio 1.703):
- **Bind:** the limbs point straight down at the game's rest, keep their lengths, and the trunk, head and feet keep their
  authored turns.
- **Per pose:**
  - every limb's direction equals the game's (within 0.11°), and split chains end on the game joint's turn;
  - a hand inside the game's shoulder line (816 arm poses) reaches its chest-relative place, keeping the copied arm's
    extension, by the shortest swing at the shoulder; the elbow keeps the game's bend. Where the game's hands cross the
    middle (304), the visual ones cross at most .41 as far;
  - a far support grip: the left shoulder reaches at most 25° (114 poses); the weapon hand gives way only past that
    (at most .041 m), and only by the excess;
  - hands, feet and head carry the game turn, and the root moves by the leg ratio;
  - twist helpers take exactly their share of the roll;
  - all 524 planted feet reach the ground point, with the knee to the game knee's side;
  - all 351 two-handed grips with the hands together reach, at up to 97 % of the arm;
  - every arm is hinged: its bones lie along it, both share the elbow's axis, and the crease faces the fold (within 1°);
  - arm twist bones take their share of the final roll;
  - with each weapon's handles (1,078 seated fists): every fist closes on its handle's grip point (after any slide) with
    its axis along the handle, and its fingers turn with it. A riding fist keeps the animation's wrist and the straightest
    wrist the handle allows (within 3°); a reaching fist points as its weapon sets it, within its spread. A shouldered
    stock butt sits at the shoulder pocket, and a braced shield stays on the fist the animation gives;
  - every fist ends 8 cm outside the head and 5 cm outside the torso (within 1 cm, where its arm is not at full stretch);
    226 grasps moved a fist and weapon out, at most .16 m;
  - transitions: frame to frame, each hand turns at most three times the game's own hand plus 20° and moves at most three
    times its move plus 5 cm (6 cm for an elbow); the snaps this caught flipped a hand 37-177° in a frame. No fist centre
    enters the head in any transition frame. On the synthetic skeleton the greatsword raise never reaches the head, so
    this guards regressions only; the BoZo renders below are the evidence;
  - without handles the support hand keeps the game wrist turn, and the grip is the game socket on the weapon frame;
  - the locals rebuild the pose.
- **Roll:** the floor fit keeps every bone over the floor.
- **Runtime:** no allocation over 1,000 frames, bit-for-bit repeats (a fresh state each), about 34 µs a pose on the
  owner's PC (46-bone synthetic rig; BoZo has 148 transforms), and the binding mirrors at the boundary and names no pack
  types.

The synthetic skeleton's visual grip sits 0.33 m (mean; 0.37 before the inside reach) from the game's. That is the expected P1 cost, because its shoulders
are narrower and higher than the voxel rig's.

**Measured on BoZo in Unity (2026-10-22, owner's PC; local tool, renders in local scratch).**

BoZo's skeleton, dressed in outfit A, was posed through this adapter and binding on 15 gun and melee poses (re-measured
with the seated fists; 46 poses rendered in all, with every melee set's idle, run, block and cuts at three points in the
swing, holding the real Standard weapon models at the adapter's weapon frames).
- **Mapping:** all 22 joints map. Twist shares: forearm .61/.26, shoulder −1.0, upper arm −.66/−.39, thigh −.56/−.23,
  calf .45/.78.
- **Leg ratio:** 1.707 (thigh .425, shin .428; arm .263/.224). The binding covers 148 transforms, the rig plus hair chains.
- **Reference points** (metres, Unity frame, floor 0):
  - head box centre 1.49, the voxel head centre being the game head joint + .165;
  - shot origin (.2 right, 1.15 up, .48 ahead).

| Pose | Head to box: BoZo / voxel | BoZo weapon frame to game wrist | Muzzle to shot origin: BoZo / game | Support reach | Planted feet (reach) | Shoe soles |
|---|---|---|---|---|---|---|
| Rifle idle | .11 / .09 | .19 | 1.11 / 1.05 | 1.00 | .90, .90 | −.030 |
| Rifle aim | .11 / .09 | .19 | 1.11 / 1.05 | 1.00 | .90, .90 | −.030 |
| Rifle run | .21 / .14 | .51 | 1.23 / 1.21 | .95 | – | .075 |
| Rifle sprint | .26 / .24 | .50 | 1.21 / 1.23 | .94 | .88 | −.019 |
| Pistol aim | .12 / .03 | .39 | .69 / .40 | .97 | .92, .93 | −.030 |
| Pistol run | .21 / .14 | .39 | .54 / .67 | hands apart | – | .075 |
| Machine gun aim | .10 / .10 | .18 | .81 / .75 | 1.00 | .88, .88 | −.030 |
| Machine gun run | .21 / .14 | .51 | .93 / .92 | .95 | – | .075 |
| Sword idle | .38 / .42 | .29 | – | – | .81 | −.010 |
| Sword run | .43 / .45 | .32 | – | – | – | .031 |
| Sword cut A | .28 / .17 | .52 | – | – | – | .195 |
| Greatsword cut A | .26 / .26 | .30 | – | .77 | – | .149 |
| Shield block | .41 / .45 | .28 | – | – | .76, .76 | −.028 |
| Daggers run | .43 / .45 | .34 | – | – | – | .031 |

Reading it:
- **Head:** BoZo's head stays as close to the head box as the voxel head does, within −.04 to +.11 m of it in every pose
  (the game's crouched sword and shield stances lower both). No head-volume change is needed (P3 not needed).
- **Weapon (P1):** the drawn weapon sits .18-.52 m from today's (mean .35), because BoZo's shoulders are narrower and
  higher and long guns now shoulder. Tracers already converge on the simulated path within 4.5 m (`NativeShotLaunch`), and
  hits are unaffected.
- **IK reach:** every two-handed grip reaches (at most 95 % of the arm). The long guns' support fists slide back along the
  handguard (up to their limit in the run) and need no give-way. The pistol run lets go as the game's hands do. Planted
  feet use 76-93 % of the leg, so nothing hyperextends.
- **Arms against the merged adapter (`6470906c`),** at identical timestamps (62 poses, front, three-quarter and side, with
  hand close-ups; every attack at 10, 30, 50, 70 and 90 % of its swing):
  - Elbow crease off its fold: worst 175° before, 0° after.
  - Hand roll against its forearm twist bones: worst 147° before, 0° after.
  - Wrist bend: median 35° before, 29° after; worst 100° before, 98° after. The long guns' support hands bend about 92°:
    BoZo's nearly straight arm reaching along the barrel to a palm-up cup.
  - Torso clearance: no elbow or forearm newly inside the torso line.
  - Long-gun stocks: 23 cm in front of the shoulder before, 8 cm (in the pocket) after.
  - Every seated fist on its grip (the tool reported the long guns' support fists 11-18 cm off before its report counted the
    slide along the handguard; the Core checks place them exactly).
- **Renders:** pistol arms extend forward with both hands on the grip; rifles and machine guns shoulder with a bent weapon
  arm and the support hand under the barrel; sword, greatsword and dagger fists wrap their hilts through every attack
  frame; the shield block holds the shield before the forearm. The greatsword and sword cuts keep their trajectories.

**Final visual pass (2026-10-22, owner review: full-body validation, not hand close-ups).**

*Method.* BoZo in outfit A held the real Standard weapon models, posed through the adapter and binding in Unity (local
tool, renders in local scratch):
- **Melee:** every set's cut A, cut B, run and block: idle, a .15 s crossfade, the action, a .2 s crossfade, then idle.
  Front, three-quarter and side renders, body-framed, at idle, the action's start, every 15 % of it, its end, recovery and
  back at idle (11 timestamps).
- **Guns:** pistol, rifle and machine gun through idle, run, aim, firing, run, aim and idle (16 timestamps).
- **Measures:** every third frame and every rendered one, against BoZo's own meshes: weapon into head and torso, sleeve
  into torso and head, hand into head, and, for the shield, hand or forearm past its back face. Also the arm's elbow bend,
  wrist bend, hand and upper-arm roll and elbow-to-spine distance.
- **Baseline:** the merged adapter (`62f4e049`), at identical timestamps.

*Results, merged adapter → this pass* (depths in cm, frames affected out of those measured):

| Check | Before | After |
|---|---|---|
| Shield: hand or forearm past the back face | every frame of all four shield sequences, through the front face, up to 24 | 8 of 43 frames of the cut A bash, at most 4, behind the face |
| Greatsword: weapon into head | idle, block, cut B and run, up to 7 | none (cut A's 30 % since the raised-hands fix) |
| Greatsword: hand into head | cut A 30 % (6) | none at the marked frames; every frame of the raise: one, 2.4, the wrist's back (below) |
| Rifle and machine gun: weapon into chest | every frame, up to 7.5 | 20 and 27 of 123, at most 5 (run carry) |
| Rifle and machine gun: support wrist bend (90th percentile) | 93° | 46° |
| Daggers cut A: left elbow to spine, at least | 5 (9 into the belly) | 19 |
| Daggers cut A: right wrist bend and hand roll, worst | 126°, 174° | 40°, 80° |
| Daggers cut B: left wrist bend and hand roll, worst | 89°, 179° | 31°, 73° |
| Sword cut B: right wrist bend (90th percentile) | 114° | 64° |
| Pistol: right hand and upper-arm roll, worst | 141°, 165° | 50°, 57° |
| Greatsword block: support wrist bend (90th percentile) and upper-arm roll, worst | 80°, 179° | 44°, 77° |

*Greatsword raise, every frame* (raised-hands fix, against shared `3c303dfd`):
- **0.58–0.75 s at 60 fps:** the hands and pommel no longer cross the face. Before, at 0.667 s and 0.683 s, 246 and 204
  hand points sat 5.9 and 5.7 cm deep and the weapon 3.3 and 1.9 cm; at 0.733 s the weapon was 2.9 cm deep.
- **Path:** the hands now pass over the head, the push at most 12 cm.
- **Elsewhere:** the greatsword run's weapon-in-chest fell from 4 frames (5.9 cm) to one (2.4 cm), and cut A's worst right
  wrist bend from 118° to 67°. Nothing else changed beyond a centimetre or a few degrees.

*Remaining defects.* These are not hidden by grip seating:
- **Greatsword cut A raise:** for one frame (0.683 s) the back of the right wrist, 12 cm off the grip, brushes the cranium's
  side (2.4 cm). Keeping the wrist out too made the support hand jump (above).
- **Greatsword support elbow:** in idle, block and run it now rests 2-3 cm into the hoodie's front (25 of 56 block
  frames). It sits on the animation's side; before, it was 90-180° off it, with the wrist bent 80° and the upper arm
  rolled 179°.
- **Shield cut A bash (44-60 %):** the forearm grazes the shield's back face, at most 4 cm.
- **Rifle and machine-gun run carry:** the gun's body touches the chest, at most 5 cm.
- **Forearm roll:** some cuts still roll the hand 170-180° on the forearm: sword cuts A and B, the greatsword cuts, and the
  shield cut A swing. This roll is the game's own wrist animation, kept by design, and the twist bones share it.
- **A long blade's outer part** (beyond 20 cm past the guard) is not kept out of the head.
- **Rifle stance:** the weapon arm's elbow is raised level with the shoulder, as the game's own arm is. Not changed.
- **Transitions:** checked frame by frame on the synthetic skeleton at 60 Hz (2,492 frames), and rendered and measured on
  BoZo at the timestamps above. Still not watched in motion in the game (step 4).

**Found by the renders, for step 4:**
- **Shoes:** they put the soles 1–3 cm under the floor on planted feet, because BoZo raises the root only for heels. Done
  in step 4: the bake lifts the dressed body by its rest sole gap (outfit A: 2.8 cm).
- **Hands on weapons:** fixed by the seated fists. With the real weapon models drawn at the frames, every closed fist
  had floated beside its grip or hilt. A step 4 caller passes each weapon's `Grasp` (handles above, `Stock` for long
  guns), draws the weapons at `WeaponFrame` / `LeftWeaponFrame`, and calls `State.Forget()` on a cut, respawn or weapon
  swap.
- **Transitions in Unity:** frame-to-frame continuity is checked in Core on the synthetic rig. On BoZo, the final visual
  pass rendered and measured idle, movement, aim, fire, attack and recovery sequences (above). Watch the first wired
  character in motion (step 4).
- **Body for the clearance:** a step 4 caller sets `Rig.HeadCenter`, `HeadRadii`, `TorsoHalfWidth`, `TorsoHalfDepth` and
  `ShoulderFront` from the outfit's head and top meshes when they differ from outfit A's, and passes `Melee(spec)` holds
  (with the weapon's extent) and `PistolGrip`.
- **Fingers:** fixed. They stayed in BoZo's open rest, and the first renders also showed drooping wrists, hands buried at
  rest, and the leap cut's elbow. The grips, the eased wrist, the hanging spread and the IK bend side above fix all four,
  confirmed in Unity renders. Finger poses per weapon (trigger finger, two-handed overlap) can be tuned later.
- **Animator on the rig:** BoZo's `BodyRig` carries an enabled Animator with the humanoid `BodyRigAvatar` and no
  controller. The step 4 bake removes it, so nothing but the binding writes the bones.
- **Multiple renders per frame:** Unity skins a mesh once per frame unless `SkinnedMeshRenderer.forceMatrixRecalculationPerRender`
  is set. Any path that draws one character in several poses in a frame (pooled puppets, portraits) must set it.
- **Roll:** the gun "roll" sample landed after the roll ended (it reads as idle), so a roll is not measured here. The Core
  checks cover the roll's floor fit.

## Step 4: first character (`claude/bozo-body`)

Outfit A (candidate A above, the techwear palette) draws in place of the voxel pilot body in arena matches on the owner's
PC. Presentation only: the 22-joint rig still poses every frame, and authority reads neither body. Hit volumes, shot
origins, the weapon frames authority uses and the wire are unchanged.

**The bake (local, git-ignored).**
- `Tools > Chip Arena (local) > Bake visual body (outfit A)`, or a `Temp/bozo-bake.request` (an output folder, then the
  outfit ids), runs in the local tool. It dresses BoZo's `BodyRig` as the step 3 renders did: the pack's anime toon
  material per part, outfit A's palette, and the covered body parts dropped.
- It removes BoZo's scripts and the rig's Animator, and lifts the body so the shoe soles meet the floor.
- It names each renderer by slot (`Top.Top_OpenHoodie`) and the skeleton root `VisualSkeleton`.
- It saves `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaVisualBody.prefab`, with its materials beside it, in Unity
  types only.
- Outfit A: 16 renderers, 20,031 triangles, 148 bone transforms, 15 materials.
- Nothing from the bake is committed. A machine without it (CI, the cloud builder, any checkout without the packs) draws
  voxel pilots exactly as before.

**The wiring** (`Runtime/NativeVisualBodyPresentation.cs`, part of `ArenaPresentation`).
- **Switch:** when `Resources/ChipArenaVisualBody` exists, every arena pilot (players and bots; not the Siren) gets a
  visual body. `--ca-voxel-bodies` keeps the voxel bodies.
- **Settings · Player model** (owner, 2026-10-23, a fallback while the body is tested in game): New body (the default) or
  Classic voxel, saved with the preferences, shown only where the bake is installed. Classic applies at once, mid-match
  too (the pause menu's Settings): the visual bodies are not posed, so they hide and every pilot draws voxel. It changes
  only how this PC draws; nothing is sent to other players (`ClassicBodies`, `VisualBodyInstalled`).
- **Lifetime:** the body is instantiated under the pilot rig's root, at the floor point (.865 m under it), so it is
  destroyed with the rig.
- **Posing:** after the rig poses (`PrepareCapturedPose`), the body takes the same game pose through `NativeVisualRig` and
  the binding. Feet plant while grounded and not rolling or juggled; rolls and juggles fit the floor. History is cleared
  after a gap, a teleport or a weapon change.
- **Grasp:** guns hold `PistolGrip`, `GunSupport` and `Stock`. The melee sets hold `Melee(spec, held)`, with
  `NativeSpecialPose.HeldRotation` and the greatsword's support. Daggers hold both hands. A thrown shield or dagger opens
  the hand. Other kits (staffs, the bow, the DEV fighters) close the hand without a seat.
- **Hands:** the rig's hand bones are moved onto the visual weapon frames after posing, and put back before the rig poses
  again, so its own blends never read them. The drawn weapon, its modules, muzzle flash, blade sweeps and wrist effects
  therefore follow the visual hands with no change to the weapon code.
- **Colour:** each pilot's accent colour fills the outfit's accent channels (`_Color_3`, `_6`, `_9`) through a property
  block.
- **What is skipped:** `SourceCharacter` skips the voxel body, the spawn hologram, the living wounds, the hat or halo, the
  body dressing, the DEV Cyber Lord's look and the parry flash for a pilot with a visual body. The local pilot's body is
  hidden in first person.

**Verified in Unity (Play mode on the owner's PC; local tool, captures in local scratch).**
- **How:** the tool enters Play mode, starts a match with three bots through the game's own `StartMatch`, and renders the
  match camera and front, three-quarter and side views of each pilot. The shots render inside the game frame, so its
  instanced weapons and effects appear in them. The Editor's preference file is restored afterwards.
- **Matches:** three, with the local pilot on the rifle, the greatsword and the daggers. Every pilot drew outfit A in its
  own accent colour. The bots carried a machine gun, fire and darkness staffs, rifles, swords and shields.
- **Holds:**
  - the rifle shouldered with the scope at the eye and the support hand under the handguard;
  - the machine gun held two-handed;
  - the greatsword upright in both hands;
  - daggers in reverse grip in both hands;
  - shields braced before the body;
  - staffs in the right hand;
  - a thrown shield leaving the hands open, as the game's `WeaponAway` says.
- **Errors:** none from the new code.
- **Seen during the runs, probably not caused by this change:** twice, `NativeCorpse` threw "Scene with name 'Cosmetic body
  1 n' already exists". Corpses are keyed by death id and their physics scenes unload asynchronously, so a presentation
  reset while a death is shown can rebuild the same corpse. A voxel control run did not die the same way, so this is not
  proven pre-existing.
- **Frame rate:** in the Editor with the window in the background, one matched run each: about 105 fps with visual bodies,
  116 with voxel. Not a benchmark; the Phase C measurement is still to do.

**The shader port (`claude/bozo-toon`).** `Resources/NativeVisualBodyToon.shader` (`ChipArena/VisualBodyToon`), our own
shader reading the pack's material inputs. The bake converts each part's material onto it (`shader=pack` keeps the
pack's for comparisons).
- **Colour, as BoZo's:** a part's texture weights three colours in its rgb, and the vertex colour's rgb picks among three
  such layers (nine colours: the outfit's palette; the third of each is the accent the pilot's colour fills). Alpha cuts
  out lashes and hair tips. Outfit A uses no pattern or decal overlays, so they are not ported yet.
- **Light, as `ChipArena/SourceLit`'s (the voxel pilots'):**
  - the anime map style's model for the key and every pixel light (`ArenaToon`: Lambert while the style is off);
  - the actor lights while a map turns them on (the practical slots and the ceiling field: Ward Zero's revamp, the
    expedition zones);
  - the guns' muzzle lights on every map, wrapped and stepped as `SourceLit` adds them.
- **Outline:** BoZo's inverted hull, its width from the vertex alpha (outfit A: 2 mm), drawn in the fog; matched to the
  map's lines since (below).
- **Stencil:** every pass writes 0 to bit 16, as characters do, so the anime style's outline pass never lines a pilot.
- **Checked in Unity:**
  - **Stencil:** a wall marked as scenery behind the baked body, then the outline pass's stencil test at its own camera
    event (local editor render).
    - With the pack's shader, 36,649 of the body's 36,649 pixels stayed marked, so the map's lines
      would cross the character.
    - With ours, 0 of 36,649 (the wall's 157,751 marked pixels round it).
  - **Lights:** in Play mode on Moonlit and Ward Zero, one muzzle light and one practical light set beside the local
    pilot through the game's own light globals, for that camera only.
    - The pack's shader ignored both: only the drawn rifle took them.
    - Ours takes both: warm on the hoodie, pockets, face and hair from the muzzle light, blue from the practical light.
  - Matches on both maps drew normally, with the bots' own shots.
- **Look:** BoZo's own ramp kept a shaded side at about .89 of the lit one. On our lighting a pilot's shaded side takes the
  map's ambient, as the voxel pilots' does. On the moonlit maps a face turned from the key reads darker and cooler than in
  BoZo's previews. The anime-style maps lift their shade (`_ArenaToonShade`). Owner decision (2026-10-22): keep it.

**The outline matched to the map's lines (`claude/bozo-outline`).** The anime map style's lines (`NativeToonEdge`) are
1–2 px on screen at any distance and darken the surface they belong to toward the map's tint. BoZo's hull was 2 mm in
the world and flat black: about 1.2 px at 2.5 m, under a pixel past 4 m.
- **Width:** the hull is pushed out by the map's silhouette width: two lines of one pixel per 1080 screen lines
  (`max(1, round(height / 1080))`, as `NativeToonOutline` sets the map's), times the vertex alpha (none under .5, as
  BoZo's).
- **Colour:** the map's rule.
  - The pilot's own lit colour (key, ambient, actor and muzzle lights as the body takes them), multiplied toward
    `_ArenaEdgeColor` by the line strength `_ArenaEdge.x`, faded out from `_ArenaEdge.y` to `.z` m.
  - The hull is lit with its normal turned across the view, so a line takes the light of the edge it borders, not the
    far side's.
  - A map without the style writes no line globals. The pilot then keeps the style's own line: #2A1E40 (the menus' and
    most maps' tint), full strength, faded 40 to 110 m. The low tier is the same, with the map's tint and distances.
- **Depth:** the hull sits back along the view ray (the same pixel on screen) by 2 cm and four times its push.
  - Clothing layers don't speck through each other (a shoe's opening on the sock).
  - Small steps (a chin over the neck, hair strands) stop being lined as pixels grow, as the map's creases fade out
    with their footprint.
  - Limbs in front of the body keep their lines.
- **Material inputs:** `_OutlineColor` and `_OutlineDistance` are no longer read (the bake stops copying them);
  `_UseOutline` and the vertex alpha still choose where a line is drawn.
- **Checked in Unity:**
  - **Width and colour** (local editor render of the baked body, fov 40, 1080 lines; the area the line adds over the
    bare silhouette's perimeter):

    | Distance | Before: BoZo's 2 mm black | After: matched to the map |
    | --- | --- | --- |
    | 2.5 m | 1.24 px | 2.05 px |
    | 5 m | 0.62 px | 2.03 px |
    | 10 m | 0.32 px | 2.03 px |
    | 20 m | 0.16 px | 2.05 px |
    | 60 m | 0.04 px | 2.14 px |

    Line over body colour (median, per channel):
    - with Cyber City's line (#1A1438): (.077, .062, .176);
    - with no line globals: (.133, .090, .200), the fallback #2A1E40.

    That is about .75–.8 of each tint, so a line reads as a darker version of the pilot, as a map line does of a wall.
  - **Stencil:** 0 of 39,710 body-and-line pixels marked; the hull clears bit 16 too.
  - **Matches:** in Play mode on Brickfall (the style on; the map's own lines drawn on the shot cameras too) and Moonlit
    (no style), the local pilot at 4, 8 and 12 m at the match camera's field of view, 1920 × 1080: the line traces the
    silhouette at the map lines' weight. Matches ran normally.
- **Remaining:**
  - A black outfit hides most of its own line (black darkened is black), as on BoZo's previews.
  - Where outfit A's head turns its vertex alpha off (the jaw and neck), a short line can break into dots at mid range.
  - The Editor shots have no MSAA: stair-stepped edges, as the map lines'.
  - Weapons, drawn by `ChipArena/SourceLit`, have no outline, as before.

**The hit flash and the spawn-in (`claude/bozo-flash`).** The visual body takes the voxel pilot's struck flash and
match-start spawn-in in its own shaders, from the same values the voxel parts read. `Resources/NativeVisualBodyFx.cginc`
is shared by the body, its outline and the hologram. `SourceCharacter` hands a visual body its flash code and spawn-in
frame (`VisualBodyLook`), which writes them per renderer only while a spawn-in runs or the flash changes.
- **Hit flash:** SourceLit's shared block (its colour and blend, the code `NativeHitFlash` writes: a hit, a headshot).
  - It applies after the light and before the fog, more on the silhouette, not in the add passes.
  - The outline is the darker version of the flash.
- **Spawn-in, per pixel** where the voxel body works per part:
  - Core `NativeSpawnIn`'s solid, heat and slice at each pixel's height over the presented feet, from the same build front
    (the spawn-in presentation's `SpawnVisualLook` hands the constants over, so the shader holds none of its own).
  - The body, its outline and its cast shadow form from the feet up in the voxel parts' world-cell dither, in the
    hardlight tint (hottest where just formed) and the completion flash, then cool.
- **The hologram** (`Resources/NativeVisualBodyGhost.shader`): the voxel pilot's look (rim, scanlines on world height, the
  clock), on three extra material slots of each renderer, only while a spawn-in shows one. None on the low graphics tier
  (as `SpawnHologram`), and the split only on Full effects.
  - The slice leading the solid front, in the pilot's hologram colour.
  - The completion's flash silhouette over the whole body.
  - The RGB split: magenta and cyan copies moved along the camera's right, at the completion and the countdown's two blips.
- **Checks:**
  - `VisualRigChecks`: the flash block equals SourceLit's; the shader's solid, heat and slice equal Core's per-part
    functions at the same height (worst 1e-5 over a grid); the tint and flash are `SpawnTintOf`'s; the wiring; no
    spawn-in names in the visual body file (the spawn-in's own presentation-only rule); no pack names.
  - In Play mode on Brickfall, the property blocks read back after each shot:
    - the front climbs from 0.6 m under the floor to 2.16 m over it through the build, then turns formed;
    - the hologram slots are on while building, at the completion and at the two blips, and off after;
    - the tint fades from 1 to 0 over half a second;
    - the completion frame had flash .47 and a 3.4 cm split;
    - a hit and a headshot set through the game's own hit record drew codes 1.52–1.66 and 3.52–3.72, and the body
      flashed white through red, headshots brighter.
- **Fixed on the way:** at world-sized arguments the dither hash resolves to about 1/256, so cells hashing to exactly 0
  drew dark specks over the unformed body (SourceLit skips an unformed part whole). The hash is now floored above 0.
- **Remaining:**
  - The hologram has no depth pre-pass (the voxel one's single layer), so overlapping cloth layers can stack slightly
    brighter in the slice.
  - The dither cells are world-fixed, as the voxel body's, so a floating body's dither crawls as it bobs.
  - Still skipped on a visual body (`!visual`): Body Chips dressing, living wounds, the parry flash, the Cyber Lord's
    hardlight pieces, hats.

**The chest core and the right-wrist chip device (`claude/bozo-core`).** Our own pieces, kept from the stopped base
(docs/CHARACTER-LOWPOLY.md section e), built in code (`Core/NativePilotGear.cs`, SourceLit sections; models
`Runtime/NativePilotGearModels.cs`) and drawn on the visual body (`VisualGear`). Owner decision (2026-10-22): the device
sits on the right wrist for every class, and the chip-select display moves there too (done: below).
- **Placement:** the local bake measures two anchors on the baked outfit and saves them in the prefab, so the runtime holds
  no outfit numbers.
  - **Core:** on the outfit's front surface over the heart, 5.8 cm left of the midline at the sternum's height, facing
    along the surface. Outfit A's open jacket leaves only 3 cm of shirt between its flaps, so the core sits on the jacket
    as a housing (the plan's "core: housing" case).
  - **Device:** on the right forearm's axis 4.5 cm up from the wrist (the forearm twist bone), its housing on the back of
    the forearm (away from the palm), its band fitted to the sleeve's 95th-percentile radius there (4.7 cm on outfit A).
- **Pieces:**
  - **Core:** a faceted shield housing (7.6 cm across) with a bevelled rim and a gem glowing in the pilot's accent.
  - **Device:** a band with an accent strip, a housing with accent side strips and a screen, a raised frame round the
    chip bay, a lid hinged at the elbow end with a lens, and the game's chip cartridge seated in the bay.
- **States** (pure functions of what the pilot already presents; nothing written back):
  - **Core:** a slow pulse; a flicker under 30 % health; a flare as the body appears (a respawn, a revive, the end of the
    spawn-in build). It takes the body's hit flash and spawn-in reveal.
  - **Device, from the chip-select slot:**
    - the lid opens with the look's weight;
    - the bay glows ready in the accent;
    - from the plant's carry end the chip shows in the bay;
    - the bay pulses in the chip's rarity colour through the accepted pulse;
    - after the lid shuts, the lens keeps that colour, fading over 1.5 s.
- **Checks:**
  - `VisualRigChecks`:
    - every section faces out where it is seen; the shut lid clears the seated chip;
    - the states follow health and the plant's timeline;
    - the wiring; no pack names; values only per frame.
  - In Play mode on Brickfall, a staged pick and plant (the local tool sets the game's own Expedition hooks between Update
    and the render) walked the device through rest, open, the chip seated in its legendary orange, the pulse, the lid
    shutting and the lens fading. The core showed on the chest throughout.
- **Remaining:**
  - At full body the right forearm sits behind a held rifle's grip, so the device mostly shows in close views.
  - The sleeve's folds can cover the band's edge.

**The chip-select display on the right wrist (`claude/chip-wrist-right`).** Owner (2026-10-22): "Keep the weapon in the
right hand pointing down", short weapons from the wrist raised at chest height, the rifles and the greatsword from a lower
wrist beside the leg, and "the left hand taps and inserts the chip". The pose is the game rig's, so voxel pilots change
too (contract: `docs/PVE-CHIP-SELECT.md`).
- **Every pilot:** the right wrist comes up and its weapon hangs pointing down. The owner raised the short-weapon wrist to
  mid-chest on 2026-10-23 (`claude/chip-wrist-chest`). On the BoZo pistol pilot the hand now sits 1.26 m up, 10 cm under
  `spine_05` and between it and `spine_04`; it sat 18–20 cm under before. The left hand taps, swipes and takes the
  chip in the Expedition plant and on the draft stage. The shield, the staffs and the bow keep their left-hand hold, and the
  chip drops down the cone. First person puts a long weapon away while the look is up.
- **Visual body:**
  - The hologram, the Expedition screen and the plant's chip move from the game wrist to the device's bay
    (`VisualEmitter`). The two wrists sit 0.2 to 0.4 m apart, because the rigs' proportions differ.
  - The support grip lets go while the look is up.
- **Checks:**
  - `--chip-select`: per class, the weapon hangs, its tip stays off the floor, the head and panel stay clear and the eyes'
    line to the panel is open.
  - `--draft-stage` (new flag), `--chooser-camera` and `--chip-screen` pass.
  - In Play mode on Moonlit, a staged pick and plant was captured for the pistol, rifle and sword pilots and a voxel
    pistol pilot. The screen's emitter and the planted chip landed on the device bay, and the left hand carried the chip
    to it.
- **Remaining:**
  - The visual body's left hand follows the game rig's hand proportionally. It ends 10 to 15 cm (its hand bone) from the
    chip it carries, which reads as the fingers holding it but is not pinned.
  - Captures show a red and yellow wedge between the Expedition screen and the pilot, on voxel pilots too and before the
    emitter move. Not traced yet: the demo has a bot firing, and a hit turns the screen's edge red.

**Accent glow, first person and death (2026-10-23, `claude/visual-body-glow`).** Owner, after the first in-game test:
"lights still show where old player model limbs are. I actually want to make lights accentuate the blue colors on the
clothes for new model", "First person still shows voxel", "Death turns model into voxel".
- **Accent glow:**
  - The combat glow's armour halos sit at the game rig's joints; on a visual body they are skipped.
  - The armour spill light comes from in front of the chest core.
  - The toon shader's `_AccentGlow` adds emission over the accent channels (the pilot's colour on the clothing trims). It is
    1.2 (.6 with Reduced effects) and pulses with the block cooldown as the halos did.
- **First person:**
  - The view model wears the visual body's forearms and hands, as the voxel view shows the elbows and hands only.
  - A second instance sits under the view rig, its skinned meshes cut once (at runtime; the meshes import readable) to the
    triangles weighted half or more below the elbows, on layer 10 without shadows.
  - It is moved rigidly so its right weapon frame lands on the view rig's weapon hand, so the sights, aim and muzzle stay the
    game's. The left hand's item rides the visual left hand, and the wrist device shows.
- **Death:**
  - A pilot's corpse makes an instance posed from the rig at death. Each bone rides the ragdoll part of its game joint
    (`NativeCorpse.PartFrom`), with the sink, overlay and shrink.
  - The authored death scenes move each bone with its joint's motion since death.
  - A cut, crushed, smashed or bisected corpse, the swarm, soul and frost looks and a cinematic's own layer keep the voxel
    body, since their effects are made of its parts.
- **Checks:** `--visual-rig` (wiring) and Play mode on Moonlit:
  - the trims glow and the halos are gone;
  - first person with the pistol, sword and rifle shows the sleeve, glove and device on the weapon;
  - a forced self-kill shows the new body falling as the ragdoll.

**Life-size weapons (2026-10-24, `claude/visual-weapons`).** Owner (2026-10-23): "the guns seem "comically" big lol. I have
new asset bundles im adding to my drive, can we utilize these?" The visual body draws life-size models from two of the
owner's new packs (POLY Mega Weapons Kit, SciFantasy Sword Pack; `docs/ASSET-CREDITS.md`) in place of the Standard skin's:
- **Which:**
  - pistol: `SM_Army_Pistol`, 19 cm long (the game's runs 60 cm from its back to its muzzle);
  - rifle: `SM_Assault_Rifle_5,56`, .92 m (the game's 1.5 m);
  - machine gun: `SM_Light_Machine_Gun`, .93 m (the game's 1.2 m), its deployed bipod cut off in the bake;
  - sword: `RB_Falchion`, its tip 1.27 m from the fist (the game's 1.275 m);
  - greatsword: `RB_Greatsword` at 1.15 times, its tip 1.64 m from the fist (the game's 1.685 m).
  - The daggers, shield, bow, staffs and magic keep the game's models, as do the Prototype Series skin, the Classic body
    and first person.
- **The bake** (the local tool's weapons step; the body bake runs it too) adds `VisualWeapons` to the prefab, one entry per
  spec:
  - `Model`: inactive, so the first-person, corpse and scene instances never show it. The kit's meshes are on our toon with
    the kit texture as drawn (the sword pack's 2048 texture as a 1024 copy). The model is placed in the visual weapon frame
    with its fist point where the visual fist closes on the game's handle, the bore or blade along +z.
  - For the guns, `Fit` is the scale about a centre that lays the Standard gun's bore, muzzle and back on the kit's
    (pistol .313, rifle .609, machine gun .776). `Support` is where the support hand holds: the handguard, or the pistol
    grip's left panel for the two-handed pistol. `Stock` is the butt.
- **The runtime** (`Runtime/NativeVisualWeaponPresentation.cs`):
  - While the model is drawn, the right hand bone takes the fitted frame. Everything drawn there therefore lands on the model
    at its size: the muzzle flash and its light, where the rounds leave, the chip modules and the Q kit's pieces.
  - The visual hands' grasp uses its Support and Stock. The hand's scale goes back with its place before the rig poses again.
  - The blades need no fit: their tips stay where the game's were, so trails, blood and enchants keep their place.
  - Presentation only: aim, muzzle rules, reach, hit volumes, timing and the wire are unchanged.
- **Builds:** the packed bake grows to 202 files and 19.3 MB (the kit meshes, texture and the cut machine-gun mesh, the two
  sword meshes and the sword texture's copy).
- **Checks:** `--visual-rig` (wiring), and Play mode on Moonlit with each weapon:
  - every grip sits in its fist; the rifle and machine gun are shouldered with the support hand on the handguard; the pistol
    is held two-handed;
  - the rounds leave 4–11 mm from each kit muzzle;
  - forced shots light the face with the muzzle light.
  - The flash's shapes draw on the match camera only, so they are not seen in the orbit shots.
- **Not yet:**
  - first person, which needs each model's sight line for aiming down the sights;
  - the Standard skin's accent lights in the pilot's colour (the kit models have none);
  - the daggers.

**Animation revamp (started 2026-10-24).** Owner (2026-10-23): "Some of these animations really dont fit the new body",
"We may need to revamp the animations as well due to new body + height", "I thought we have a running animation pack
already? Cant we utilize that?"
- **Where the motion comes from today:**
  - The game's clips are the Explosive RPG Character pack's clips, captured onto the 22-joint rig (`tools/pack-*-provenance.json`
    names each source clip and frame range).
  - While travelling, the legs come from the procedural gait, not the clips. The visual body copies the rig's limb directions
    (`NativeVisualRig`).
  - The pistol's walk uses the sword set's `Armed-Walk` and its run the `Unarmed` runs; the pack's `1Hand-Pistol` set has
    no locomotion.
- **First look (local tool `bozo-retarget.request`):** the pack's own humanoid clips play on the body through Unity's humanoid
  retargeting (the body rig's avatar), sampled in the Editor:
  - The runs come out as natural full-body runs on the new proportions: forward lean, long strides, pumping arms.
  - Today's runs are upright and crouched with short strides.
  - The comparison sheets are in local scratch.
- **Proposed runtime design (presentation only):**
  - A humanoid layer: the visual body gets an Animator with the body rig's avatar and a manually evaluated PlayableGraph. It
    plays the pack clip the game pose comes from at the same time and weight, traced through the provenance.
  - The game's arm holds (grip, support hand, aim, chip select), the planted feet and the code-only layers (aim, recoil,
    leans, specials) then go over it as now.
  - Locomotion first: runs, walks and strafes blended by direction, their rate matched to the pilot's speed so the feet do not
    slide. Then idles, jumps and reactions.
  - Attacks keep the game's poses until traced, since their keys are the hit timing.
  - The clips ship in the licensed-assets zip: about 0.45 MB per FBX, so the locomotion sets need a larger budget.
  - Hit volumes, timing and the wire are unchanged.

**Not yet (step 4 continues):**
- **Owner's concept pieces:** the mask and visor.
- **Hats** (owner decision open); Body Chips dressing, living wounds, the parry flash and the Cyber Lord's pieces on the
  visual body.
- **Pattern and decal overlays** for other outfits; normal maps.
- **Other rigs still draw voxel:** intros, portraits, the draft and the death broadcast. Gory deaths (a cut, crushed or
  split body) and the swarm, soul and frost kill effects still use the voxel body (step 6 region-based death).
- **Captures:** the neutral turnaround.
- **The performance pass** (Phase C).
- **Builds (2026-10-23, `claude/visual-body-install`):** the owner's builder installs the bake from the licensed-assets
  zip it already reads (`G:\My Drive\Chip Arena Build Logs\LocalAssets.zip`), so test builds draw the new body and show
  Settings · Player model.
  - `tools/pack_visual_body.py BASE_ZIP OUTPUT_ZIP` adds the bake to a copy of that zip. It follows the bake prefab's GUID
    references through the text assets and every `.meta`. Outfit A comes to 176 files and 14.9 MB: the meshes, materials and
    textures it uses, their metas and the folder metas. A `visual-body.json` lists each file with its SHA-256. Shaders, shader
    graphs and scripts stay out (the body uses our tracked toon shader); the pack's own outfit materials are kept as text.
  - `tools/install_local_assets.py` checks that section and copies the files over `Assets/LocalAssets`, deleting nothing. The
    checks cover paths, kinds, hashes, metas, unique GUIDs, a 40 MB budget and the prefab in a Resources folder. A full local
    pack is never trimmed, and a zip without the section installs the audio as before.
  - After a new bake, run the packer on the Drive zip and replace it (keep the old one). The 2026-10-16 zip is kept beside it
    as `LocalAssets-2026-10-16.zip`.
  - Only the bake's dependencies ship, not the pack's own Resources folders, so builds grow by about the outfit's textures
    and meshes.

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
- **Animations may be fitted to the model** ("sizing and everything").
  - Claude's part: holds, grips, wrist angles and pose tuning for the BoZo body. They stay BoZo-only while the voxel
    bodies remain, through the adapter or a BoZo pose layer.
  - Astra's part: any change to authority sizing (capsule, head box, shot origin, weapon frames), decided by the owner.
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
  - (decided 2026-10-22: the right wrist for every class);
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
