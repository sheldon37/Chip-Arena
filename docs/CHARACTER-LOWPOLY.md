# Low-poly skinned characters: architecture audit and design (Phase A)

Status: design proposal from `claude/lowpoly-audit` (base shared `28a46ce`), merged into shared. No runtime
code changes. Owner (visuals): Claude. Game logic, accounts, networking and persistence: Astra (section i is a proposal
for Astra to implement). Look targets: the owner's player-model concept sheet (stylized faceted low poly, hood and mask,
cyan emissive accents, readable at range) and the base customization sheet (male and female bases, chest core, one
wrist chip device, modular slots).

All file paths are under `unity/ChipArenaConversion/Assets/ChipArena/` unless shown otherwise. "Source frame" is the
rig's maths frame (+x is the rig's left); "Unity frame" is that frame mirrored on x once at the Unity boundary.

## 0. Audit facts the design depends on

**The rig is the contract.**
- `Core/CapturedRig.cs` defines 22 joints, with fixed indices, names and rest offsets, and identity rest rotations. See
  the table in section c.
- `CapturedRig.Pose` is the only pose currency: local rotations and positions per joint, in the source frame. 52 Core
  files write or read it, and 18 of them call the hard-coded two-bone IK:
  - `CapturedRig.Arm`: upper arm .27 m, forearm .25 m.
  - `CapturedRig.Leg`: thigh .25 m, shin .25 m.
  - The floor is root -.865 (`Pose.Lowest` over `CapturedRig.Boxes`).
- Every route writes the same pose through `Runtime/NativePoseWriter.Write`, onto the 22 Transforms of a
  `Runtime/CapturedAnimation.Player`:
  - captured clips (`CapturedClip`, `CapturedClips.Generated.cs`, `NativePackClips.Generated.cs`), sampled by
    `Player.Sample`;
  - the route switch `NativePoseRoute.For`: CapturedMelee / `MeleeMotion`, Magic / `MagicPose`, Fighter, Gun /
    `GunMotion`, Bow / `NativeBowMotion`, Generic;
  - the layers: `NativeJumpMotion`, `NativeSprintMotion`, `NativeLocomotionCarry`, `NativeHitReact` / `NativeHitLayer`,
    `NativeStagger`, `NativeSpecialPose`, `CapturedRig.Roll`, `NativePoseTransition`;
  - first person, `NativeFirstPersonPose`;
  - intros: `NativeIntroMotion`, `NativeEntrancePose`, `NativePackIntros.Generated.cs`, `NativeSheldonIntro`;
  - lobby poses, `NativeLobbyPoses`;
  - the chip-select overlay: `NativeChipSelectPose`, `NativeChipPlant`.
- The joint indices are **authority data**. `Core/NativeBodyDamage.Part` assigns wounds to `CapturedRig` joints, and
  `NativeWound.part` and severs replicate them. They must never change.

**Hit volumes are not the rig.**
- `Core/GunRules.SegmentActor` uses body spheres at .35/.7/1.05 m (r .34) and a head sphere at 1.51 m (r .19).
- The movement capsule is `NativeLocomotionRules`: radius .42, half height .85.
- The visual head of today's bodies is centred at 1.505 m. A new body must keep its head centre near 1.51 m so
  headshots stay honest.

**How bodies draw today.**
- `Runtime/NativeSourceModels.cs` builds immediate-mode instanced batches:
  - `Queue(id, root, bone(b), visible(b), …, body/accent tint, partOpacity(b), partTint(b), hitFlash)` appends one
    instance per part.
  - `Flush` issues one `Graphics.DrawMeshInstanced` per (part, layer), up to 512 instances, with shadows on layer 0.
  - Per-instance tints ride `MaterialPropertyBlock` arrays.
- Every body is rigid parts on joints:
  - Standard and Sheldon Clothes: sculpted in `Core/NativeBodyModels.cs`, uploaded by `Runtime/NativeBodySkinModels.cs`.
  - Prototype: the `pilot` model of `Resources/SourceModels.json`.
- **About 25 call sites** queue a pilot body from bone matrices, not Transforms, through
  `Runtime/NativeBodySkinPresentation.cs` (`RigBody`, `BodyAt`, `BodyModel`, `BodyGhost`, `BodyAfterimage`):
  - the live rig and first person (`NativeSourcePresentation.SourceCharacter`, `NativeViewPresentation`);
  - intros, entrance ghosts and spawn-in;
  - portraits, the lobby rooftop and the draft;
  - corpses (`NativeDeathPresentation` `NativeCorpse.Bone(b)`), bisect halves, vent and smash deaths and Siren drops;
  - revive holograms, the death broadcast hologram;
  - Demon Step and dash ghosts and roll afterimages.
- Derived looks are built once per model: `Ghost`, `Afterimage`, `Hologram`, `EntranceGhost`, `Ink`, `WoundedPilot`.
- First person draws only the forearm and hand joints (`NativeViewRules.ViewModelBone`: joints 8, 9, 12, 13) on layer 10
  with `ViewCamera`, without shadows.

**Cosmetic flow.**
- **Picked:** in the Locker (`Core/NativeLocker.cs` categories; `Runtime/NativeLockerUI.cs`, `NativeLockerSkinsUI.cs`,
  `NativeHatColorUI.cs`).
- **Saved:** in `prefs` (`Runtime/ConversionGame.cs` `ConversionPreferences`: `bodyColor`, `accentColor`, `hat`,
  `hatColor`, `backColor`, `lobbyBack`, `lobbyPose`, `weaponSkins`) and in the account profile (`Runtime/NativeAccount.cs`
  `NativeCosmetics`).
- **Sent and checked:** each joining seat is normalized by the host with the peer's verified access
  (`Runtime/NativeNetwork.cs` `SanitizedSeat`). The seat is `Core/NativeMatch.cs` `NativeSeat`. It travels in **every**
  game frame and in lobby frames as `Runtime/NativeNetworkFrame.cs` `NativeSeatFrame`; `NativeGameFrame.Valid` caps each
  field's length.
- **Body skin pick:** the `body` entry of the weapon-skin map (`"body:prototype"`, `"body:sheldon"`;
  `Core/NativeWeaponSkins.cs`, `Core/NativeBodySkins.cs`). Old clients drop entries they do not know and draw Standard.
- **DEV gate:** `NativeCosmeticGate` in `Core/NativeHatLooks.cs`, plus `NativeBodySkins.Gate`. The Sheldon Clothes,
  the Sheldon Head, the Sheldon Intro and the Sheldon Idle need live DEV access.
- **Backend:** `src/relay/accounts.ts` `accountCosmetics` strictly allowlists every cosmetics field; unknown fields are
  dropped on save. Any new persisted field therefore needs a backend change and a Sites deploy (owner approval).

**Death and gore.**
- **Kill boxes:** `Core/NativeKillEffectRules.cs` and `Core/NativeKillBoxes.Generated.cs` hold the rig's voxel boxes
  with ragdoll part roots `{1,2,5,7,8,11,12,14,15,18,19}` and sever cap joints `{2,5,7,8,11,12,14,15,18,19}`.
- **Corpses:** `Runtime/NativeDeathPresentation.cs` `NativeCorpse` keeps 11 rigid bodies in a cosmetic physics scene,
  with colliders sized from `CapturedRig.Boxes` and `Bind[22]`. A bone's matrix is
  `Parts[Anchor[b]] * inverse * Bind[b]`.
- **Wounds:** `Core/NativeWoundSkin.cs` and `Runtime/NativeWoundGeometry.cs` carve holes into the sculpted bodies' own
  part meshes, per joint and cached.
- **Bisect:** `Core/NativeBisect.cs` cuts posed meshes in each piece's draw frame (`Runtime/NativeBisectModels.cs`,
  `NativeBisectCorpse.cs`); the cut can run on a worker during the hold.
- **Other deaths:** `Core/NativeSmashGib.cs`, `Core/NativeArrowGib.cs` (lodges halfway into `CapturedRig.Boxes`),
  `Core/NativeMeleeBisect.cs`, `NativeSawBisect.cs`.
- **Revive:** the repair cells (`Runtime/NativeReviveHoloPresentation.cs`).

**Zombies.**
- Every horde kind reuses the same 22-joint `CapturedAnimation.Player` and the same instanced rigid-part batches.
  Models: `Core/NativeZombieModels.cs`, `NativeZombieAnatomy.cs`, `NativeHordeModels.cs`, built at load by
  `Runtime/NativeHordeKindModels.cs`.
- Each kind has one material and 21 sections per kind, look and tier.
- The owner kept the zombies voxel (2026-10-17). They are out of scope here, apart from the shared rig and draw code
  they rely on, which this plan must not break.

**Chip selection already has a wrist look.**
- `Core/NativeChipSelect.cs` (`NativeChipSelectPose`, `NativeChipSelectTrack`) raises a "display wrist" under a
  hardlight panel. `Core/NativeChipPlant.cs` and `NativeDraftPlant.cs` arc the chosen card into a cuff on that wrist,
  where it pulses in the chip colour.
- Drivers, all read-only: the replicated PvE row `open`, the Expedition `members[].choosing`, Install events and the
  draft timeline.
- Today the display wrist is the **free hand**: the left, or the right when the weapon rides the left hand (staffs,
  L33t H4x, bow).
- On the PvP draft stage (`Core/NativeDraftStage.cs`) the reaching hand follows the card's side (`Left = card.X >= 0`),
  and the plant goes into the other wrist, so either wrist can take the chip.

**Binary assets.**
- **What exists:** no FBX, Blender files, prefabs or `.mat` files are tracked. Every mesh is code-built or a JSON
  resource. There is no LFS and no `.gitattributes` for art.
- **What `tools/package_source.py` enforces:**
  - every file under `Assets/` needs a `.meta` file with a unique GUID;
  - any file of 1 MB or more needs a receipt in `tools/asset-lock.json`;
  - only `Assets`, `Packages`, `ProjectSettings`, `Tests`, `tools`, `Relay` and `Updater` are packaged.
- **Licensed packs** live only in git-ignored `Assets/LocalAssets/` (installed by `tools/install_local_assets.py` from
  the owner's zip). They must never be committed or required for a build.
- **Other checks:** `tools/check_relay.py`, `check_unity_compile.py` and the Python tests do not constrain new asset
  types.

## 1. Measured current cost

The numbers below come from Core, built offline with the real builders; they are not Unity captures.

| Model (tier) | Parts / materials | Joints | Triangles | Vertices |
|---|---|---|---|---|
| Standard (Full / Reduced / Low) | 48 / 48 shared by the three tiers | 21 | 7,782 / 4,056 / 1,584 | 16,924 / 8,664 / 3,168 |
| Sheldon Clothes (Full / Reduced / Low) | 48 / 48 | 21 | 4,342 / 2,652 / 1,284 | 9,372 / 5,592 / 2,568 |
| Prototype `pilot` (one tier) | 48 / 48 | 21 | 2,616 | 5,232 |
| Hat (cap … cowboy; Auditor Halo) | 1 (Halo 3) | 1 | 24–144 (1,332) | 48–240 (990) |
| Zombie kinds, 7 (Full / Reduced / Impostor) | 21 sections / 1 per kind | 21 | 4,662–7,132 / 2,108–2,918 / 348–576 | 9.3–14.5 k / 4.2–5.8 k / 0.7–1.1 k |

Draws per frame today:
- **Body draws:** one instanced draw per (part, layer), shared by every pilot on the same skin and tier.
  - One kind of body in view (8 identical pilots) is **48 draws + 48 shadow-caster draws** and 48 material switches.
  - Each further (skin, tier) in view adds 48 + 48. The worst case, Standard and Sheldon at three tiers each plus the
    Prototype, is 336 + 336.
  - Without instancing (the fallback path) it is 48 per pilot.
- **Extras per pilot:**
  - the hat: 1–3 draws;
  - the weapon's parts;
  - body chips and 4 team bands;
  - a wounded pilot's cut parts, which break batching on the wounded joints.
- **First person:** 12 parts on layer 10.

A skinned low-poly character, as proposed (estimates; to be measured in Unity in Phase C):
- **Draws:** 1 `SkinnedMeshRenderer`, 1 opaque submesh and 1 material: **1 draw + 1 shadow draw per pilot**. 8 pilots
  are 8 + 8 draws and one material switch.
- **Rig:** 22 bones, up to 4 influences per vertex (target 2).
- **Triangles:** the outfit is combined into one mesh at about 4–7 k triangles at LOD0. Flat-shaded facets split
  vertices, so that is about 9–14 k vertices.
- **GPU skinning:** about 100 k vertices per frame for 8 pilots.
- **Memory:** about 1–1.7 MB of mesh per cached outfit (three LODs, Unity's 32-byte `BoneWeight` included).

So low poly is **not automatically cheaper**:
- Draws drop sharply.
- Vertices are similar to the Standard Full body.
- A per-frame skinning cost is added that rigid parts never had.
- Instancing across pilots is lost.
- Pose-frozen derived draws (ghosts, afterimages) would cost a skin pass each unless baked; see section g.

## a. Reuse / adapt / replace

| System | Today (where) | Decision |
|---|---|---|
| 22-joint rig, `CapturedRig.Pose`, every pose route, layer, IK, clip, intro, lobby pose, hit reaction, stagger, roll, chip-select overlay | `Core/CapturedRig.cs`, the files in section 0, `Runtime/CapturedAnimation.cs`, `NativePoseWriter` | **Reuse unchanged.** The skinned mesh binds to these exact joints (section c). |
| Hit volumes, capsule, wound part ids, death records | `GunRules`, `NativeLocomotionRules`, `NativeBodyDamage`, `NativeDeath` | **Unchanged.** Cosmetics never read or write them. |
| Body draw entry (`Queue(id, root, bone, visible, tints…)`) and the ~25 body call sites | `NativeSourceModels`, `NativeBodySkinPresentation` | **Adapt.** A skinned model id routes inside `Queue` to a skinned queue, so the call sites keep their code. |
| Rigid part meshes (Standard, Sheldon Clothes, Prototype) | `NativeBodyModels`, `SourceModels.json` | **Keep as legacy** body skins, selectable and unchanged. |
| Hats | `hat-<id>` bank models, `+tint` twins, `NativeHatLooks` | **Reuse** as headwear on the head joint, with a per-body-type fit transform. Native low-poly versions come later. |
| Sheldon Head / Sheldon Clothes | `NativeSheldonHead`, `NativeBodyModels` | **Keep the voxel versions.** Add modular Sheldon pieces (section d). |
| Armour-light tables (emitter → receiver per part) | `NativeArmorLight`, `NativeBodySkinModels.BodyArmorLights` | **Replace** on the skinned path: per-vertex emission and bloom; the legacy path is unchanged. |
| Per-part opacity and tint (spawn-in, revive and Cyber dithers, chip and block tints), hit flash | `Queue` `partOpacity`, `partTint`, `hitFlash` | **Adapt.** A per-joint table of 22 entries in the `MaterialPropertyBlock`; the shader reads each vertex's joint id. |
| Ghost, afterimage, hologram, entrance ghost, ink, recolour shaders | `NativeSourceModels` derived models | **Adapt.** Skinned variants for live poses; frozen poses are baked (section g). |
| Body chips, team bands, Cyber Lord pieces, weapon sockets | `NativeBodyChips`, `NativeStatusVisuals`, `NativeCyberLordLook`, `NativeKillBoxes` sockets | **Reuse**, with the attachment points and fit volumes of each body type (section c). |
| Ragdoll corpse | `NativeCorpse` | **Reuse.** The skinned body reads `Bone(b)`; colliders come from the fit volumes. |
| Severs, stump caps, crush | `NativeKillCaps`, `NativeKillDebris`, kill boxes | **Adapt.** Region masks, authored caps, detached weights (section f). |
| Bisect | `NativeBisect` cut | **Adapt.** Bake the pose, cut, rebind. |
| Wounds | `NativeWoundSkin` mesh carving | **Replace** on the skinned path with shader wound decals and the existing inside pieces. Mesh cuts are optional later. |
| Voxel burst, crush debris, smash, arrows, revive cells | box-based rules | **Reuse** the boxes and recolour from the outfit; arrow depth, colliders and repair cells use the fit volumes. |
| Zombies | voxel kinds on the shared rig | **Untouched.** |

## b. Asset pipeline

**Source.**
- Blender files and their build scripts live in `unity/ChipArenaConversion/Art/Characters/`. That is outside `Assets/`,
  so Unity never imports a `.blend` (Unity would invoke Blender on one), and outside the packaged folders.
- Add `*.blend binary` to `.gitattributes`.
- Keep each `.blend` under 5 MB. Prefer a text `bpy` build script where the shapes are procedural. LFS is not needed
  until the art passes about 50 MB.
- Only original or owner-made art. Anything derived from a licensed pack stays in `Assets/LocalAssets/`, and the build
  must never need it.

**Export.**
- `tools/export_character.py`, run headless:

  ```
  blender --background --factory-startup <file>.blend --python-exit-code 1 --python tools/export_character.py -- --out <dir>
  ```

  `--python-exit-code 1` makes a script error fail the run.
- **Deterministic:**
  - objects and pieces in sorted order;
  - triangulation by a fixed method;
  - stable loop order;
  - positions quantized to 0.01 mm and weights to 1/255;
  - no timestamps;
  - the Blender version recorded.

  Two runs must be byte-identical.
- **What it does:**
  - checks the armature against the joint spec;
  - converts axes: Unity x = −Blender x, y = Blender z, z = −Blender y;
  - fixes the winding so every face faces out by the cross product of its edges (the bank's convention);
  - writes the bind poses from the spec, never from Blender's bone rolls (section c);
  - checks each piece's triangle budget.
- **Outputs:**
  - one binary mesh library per body type and per slot library, `Assets/Conversion/Resources/Characters/*.bytes`
    (`TextAsset`), each kept under 1 MB, otherwise with an asset-lock receipt;
  - a JSON manifest (`CharacterCatalog.json`): ids, slots, tags, hide masks, LOD triangle counts, attachment points, fit
    volumes, and the SHA-256 of each binary, with `.meta` files and stable GUIDs written once.

**Runtime format: decision.** A runtime mesh resource, parsed in Core, uploaded to a `Mesh` with bone weights and
bind poses, and drawn by a `SkinnedMeshRenderer`. Not FBX.

Reasons:
- **Testable offline.** `Tests/CoreTests` compiles `Core/*.cs` without Unity, so it can verify the parser, skinning
  maths, combiner, hide masks, compatibility rules, budgets, skeleton match, bisect and bake, and zero-allocation
  per-frame paths. An FBX can only be checked through an Editor import, which this environment and CI cannot run.
- **Deterministic and reviewable.** Blender's FBX writer stamps creation times, and the Unity `ModelImporter` reorders
  and optimizes vertices by settings stored in the `.meta` file and by Editor version. Each regeneration risks churning
  sub-asset fileIDs. Our binary changes only when the art does, and the manifest diff is readable.
- **It matches the codebase.** Every body, weapon, hat and zombie is already Core data uploaded at load
  (`NativeBodySkinModels.BodyMesh`, `SourceModels` decode). No Animator or Avatar is needed; our poses drive the bones.
- **Cost:** a small binary parser, plus our own LOD and bounds handling, which we already do for the voxel tiers.

## c. Authoritative rig vs visual skeleton and adapter

Owner decision (2026-10-20, Option B in principle): human proportions for the visual body, on a presentation-only
skeleton driven by the game's poses. Nothing below changes gameplay, collision, hit volumes, weapon frames or the
replicated animation state. Status: the visual skeleton and the model are on `claude/lowpoly-base-model` (not merged);
the adapter is specified here and prototyped only in offline preview tooling; nothing is wired into the runtime.

**Two skeletons, one topology.**
- **Authoritative:** `CapturedRig.Bones`, unchanged: 22 joints, identity rest frames, motion-root at feet + .865 m. The
  game's poses, IK lengths (`Arm` .27/.25, `Leg` .25/.25) and every system below keep using it.
- **Visual:** the same 22 names, order, parents and identity rest frames, with human rest offsets: a 1.74 m figure of
  7.5 heads (head .232 m), legs about half the height. The mesh is skinned to it; the manifest records both offset
  sets (`restLocal` visual, `gameRestLocal` authoritative).

| Joint | Authoritative rest (y; x) | Visual rest (y; x) | Visual offset from parent |
|---|---|---|---|
| motion-root | .865 | 1.182 | (root) |
| pelvis / spine / chest | .625 / .73 / .92 | .942 / 1.047 / 1.247 | −.24 / .105 / .20 |
| neck / head (pivot) | 1.24 / 1.34 | 1.467 / 1.567 | .22 / .10 |
| clavicle / shoulder | 1.10; ±.275 / ±.345 | 1.402; ±.122 / ±.192 | (±.122, .155) / (±.07, 0) |
| elbow / wrist | .83 / .58 | 1.097 / .847 | −.305 / −.25 |
| hip / knee / ankle | .57 / .32 / .07; ±.13 | .887 / .467 / .067; ±.09 | (±.09, −.055) / −.42 / −.40 |
| toe | .05, z .09 | .025, z .135 | (0, −.042, .135) |
| head centre | voxel head box 1.505 | 1.624 (chin 1.508, crown 1.74) | – |

**Who reads joint positions (audit of `Core` and `Runtime`, 2026-10-21).**
- **Authoritative (unchanged by the visual skeleton; none read joint positions):**
  - the pilot capsule (`NativeLocomotionRules` radius .42, half height .85);
  - the headshot box (`NativeCombatRules.Headshot`: body + .64 m, ±.17 m, yaw-aligned) and, in the Core test world,
    `GunRules.SegmentActor` (spheres at .35/.7/1.05 m, head sphere 1.51 m);
  - the melee sector (`NativeDevVolumes`: origin + .15 m, ±1.6 m tall);
  - the shot origin (`NativeProjectiles.Fire`: body + .3 m up, .2 m right, .48 m ahead; `GunRules.Muzzle`);
  - the aim origin (`NativeAbilityRules.EyeHeight` .52 over the body).
- **Presentation that reads the rig's joint frames today (these would follow the visual skeleton):**
  - weapons on `RHand`/`LHand` (`GunPresentation`, `NativeGunKitPresentation`, bow, magic, chips) at
    `NativeKillBoxes.RightHandSocket` (0, −.065, .055);
  - muzzle flashes and the drawn rounds' start (`NativeWeaponFxPresentation.MuzzleFx`/`MuzzleRecord`): the hand frame
    times `MuzzleZ`; `NativeShotLaunch` then joins each drawn round to the simulated path within 4.5 m;
  - footfalls (`NativeFootfallPresentation`: the foot joints), the cinematic camera's head, chest-anchored glows,
    the interaction grab claw (`NativeInteractionPresentation`, checked by `NativeInteractionValidation`);
  - corpses and gibs (`NativeDeathPresentation`, `NativeBisectCorpse`, `NativeCollapsePresentation`: colliders from
    `CapturedRig.Boxes` per bone), wounds (`NativeBodyDamage.Part` height bands, `NativeWoundGeometry`), arrow
    depth and band/armour/hat anchors (`NativeArmorLight.RestLocal`, `NativeStatusVisuals.BandBones`);
  - blade clearance (`MeleeClearance`, presentation only, sized to the voxel shoulders and head);
  - the first-person view rig (`NativeViewPresentation`): elbows and hands only (`NativeAbilityRules.ViewModelBone`).

**Adapter (presentation only; prototype in offline tooling, Unity wiring is Phase D).**
1. Copy the game pose's local rotations 1:1 onto the visual joints.
2. Motion-root: visual rest height plus the game root translation × `legRatio` (1.64, visual hip-to-ankle .82 over
   the game's .50), so crouches and bobs keep the game's knee angles.
3. Feet: while a game foot is planted (within about .03 m of the floor), two-bone IK puts the visual ankle on the
   game ankle's ground point (the knee toward the game knee's plane), blending out as the foot lifts; a roll or a
   tuck keeps the game's `Lowest()` floor fit, evaluated on the visual joints.
4. Weapons ride the visual right hand at the same socket. Two-handed holds: the left hand IKs to the game's
   right-to-left hand offset carried onto the visual weapon. One-handed weapons, empty hands and casts keep their FK.
5. Authority stays as it is: capsule, head box, melee sector, shot origin, weapon frames and the network pose.

**Drift with rotations 1:1 (before IK), the game's own Core poses (`lpb-pose`, 31–61 frames per clip), metres, mean /
max, on the visual skeleton above (shoulders at ±.192 since the 2026-10-21 body redesign).** "Box" is the authoritative
head box centre (1.49 m over the floor); "grip" the weapon socket; "support IK" how far the left hand must move to keep
a two-handed grip; reach is the fraction of the visual arm or leg length used.

| Clip (weapon) | Head vs box: visual | Head vs box: game rig | Right grip vs game | Support IK | Left reach | Ankle vs game | Leg reach | Grip to shot origin: game / visual |
|---|---|---|---|---|---|---|---|---|
| idle (rifle) | .08 / .08 | .09 / .09 | .32 / .33 | .33 | .80 | .05 / .05 | .90 | .12 / .24 |
| idle (pistol) | .08 / .08 | .03 / .03 | .32 / .33 | .34 | .68 | .03 / .04 | .92 | .18 / .39 |
| run (rifle) | .21 / .22 | .16 / .18 | .27 / .28 | .32 | .67 | .16 / .20 | .90 | .49 / .29 |
| sprint (rifle) | .29 / .30 | .28 / .29 | .26 / .28 | .33 | .64 | .17 / .24 | .87 | .47 / .38 |
| rifle aim | .08 / .08 | .09 / .09 | .33 / .33 | .33 | .80 | .05 / .05 | .90 | .13 / .26 |
| melee swing (sword) | .39 / .48 | .42 / .50 | .20 / .24 | n/a | – | .07 / .14 | .64 | .87 / .72 |
| magic cast | .17 / .39 | .11 / .31 | .28 / .31 | n/a | – | .05 / .12 | .92 | .39 / .38 |
| roll (rifle) | .38 / 1.37 | .39 / 1.35 | .25 / .32 | .33 | .89 | .16 / .28 | .92 | .51 / .39 |
| dodge turn (rifle) | .28 / .31 | .27 / .30 | .27 / .29 | .33 | .64 | .16 / .27 | .90 | .46 / .37 |
| melee run swing | .46 / .70 | .46 / .69 | .23 / .29 | n/a | – | .18 / .29 | .92 | .78 / .59 |

Reading it:
- The visual head sits about as close to the authoritative head box as the voxel head does (within .01–.06 m in
  every clip); in motion both lean away from the fixed box by the same amounts. No head-volume change is needed.
- The weapon on the visual hand sits .20–.33 m from the game rig's weapon frame (narrower shoulders, higher chest).
  The visual muzzle ends up .24–.39 m from the simulation's shot origin at idle and aim (the voxel's is .12–.18 m) and
  closer than the voxel's while running (.29–.38 m against .46–.49 m). Tracers would start at the visual muzzle and
  join the simulated path within `NativeShotLaunch.Converge` (4.5 m), as they do today; hits are unaffected.
- Two-handed grips need the left hand moved about .33 m (twice the shoulder narrowing); every two-handed pose stays
  within 89 % of the visual arm's reach. Planted feet need up to .29 m of IK and stay within 92 % of the leg's reach: no
  hyperextension.
- The per-frame numbers through the full adapter (with IK) come with the animation tests, after the owner's go-ahead.

**PROPOSALS for the owner and Astra (none applied).**
- P1 (Claude, presentation): adapter as above, weapon on the visual hand. Cost: weapon and muzzle drift of
  .2–.33 m from today's drawn frame; no authority change.
- P2 (Claude, presentation; alternative to P1): the right hand IKs to the game's weapon frame (as an offset from the
  chest, scaled) so the drawn weapon and muzzle stay where the game draws them today; the arms bend to reach.
- P3 (Astra, authority; only if the owner wants hits to match the taller body): the head box and shot origin could be
  raised to the visual head and chest (+.13 m and +.25 m). The drift table shows no need for the head box today.
- P4 (Astra/Claude, presentation): the systems listed above that read `CapturedRig.Boxes` or voxel sizes (corpse
  colliders, wound bands, blade clearance, hat fits, the interaction claw) take per-part fit volumes from the model's
  manifest in Phase D instead.
- First person needs no change: the view rig shows forearms and hands, and both skeletons' forearms are .25 m long.

**Binding.**
- Vertices are in the visual motion-root frame (feet + 1.182 m), Unity axes, front +z; the soles at 0 m.
- The visual head is centred at 1.624 m (crown 1.74 m). The body and garments stay within the capsule radius (.42 m,
  plus .03 m) at rest; headwear and back pieces may exceed it by at most .25 m.
- Bind pose of joint *b* = inverse(translation(visual rest world of *b*)), identity rotations. The mesh is modelled in
  the rest pose (arms down); the bone-heat pass runs on a copy with the arms spread 40°, then the weights carry back.
- Vertex groups are named exactly as the joints. No weight on joint 0. The cuff, hands, fingers and the device ride
  the wrist joint.

**First person, attachment points and fit volumes.**
- The view rig (`NativeViewPresentation`) uses a second puppet on layer 10 with `ViewCamera` and no shadows, showing
  only the forearm-and-hand regions (death regions 8 and 12).
- The manifest records the chest core, the wrist device and the hair joint; hat fits, the back socket, body-chip
  anchors, team-band sockets, Cyber Lord anchors and one fit volume per ragdoll part are Phase C/D, on the visual body.

## d. Cosmetic schema

**Slots.** Each slot is independent. "none" is valid wherever noted.

| Slot | Ids (examples) | Notes |
|---|---|---|
| `body` (body type) | `m`, `f` | Both use the same skeleton; garments ship one mesh per body type, with a fallback to the slot default. |
| `face` | `face-01`… | A head-region variant; visible by default. |
| `skin` (skin tone) | `tone-01`…`tone-12` | A palette index (an owner decision: palette or free colour). |
| `hair` + `hairColor` | `none`, `short-01`, `spiky-01`… | Never tied to an outfit. Each hair has `full`, `under-hood` and `under-hat` variants and a short fringe. |
| `mask` | `none`, `sheldon-mask`, `visor-01`… | `covers: lower`, `full` or `eyes`. |
| `head` (headwear) | `none`, today's hat ids, `sheldon-hood`, `hood-01` | `hairMode: keep`, `compress` (under-hat), `hood` (under-hood) or `hide`. |
| `upper` | `none` (bare torso, or the sports bra on `f`), `arena-jacket`, `sheldon-coat` | `collar: none`, `low`, `high` or `hood-down`. |
| `lower` | `base` (boxers or shorts), `cargo-01`, `sheldon-trousers` | |
| `gloves` | `none`, `tactical-01`, `sheldon-gloves` | The right glove leaves the device zone free or ships a housing. |
| `feet` | `none` (barefoot), `runner-01`, `sheldon-boots` | |
| `back` | `none`, `pack-01` | Uses the back socket. |
| `equip` | `none`, `belt-01`, `holster-01` | |

**Colours.** `primary`, `secondary`, `accent` (emissive), `hairColor` and `core`, each "#rrggbb", or "" for Auto.
- Auto primary is the profile's `bodyColor`; Auto accent is the profile `color`.
- Auto core and wrist glow follow the accent.
- `hatColor` keeps tinting headwear.
- Vertex paint channels: 0 fixed, 1 skin, 2 hair, 3 primary, 4 secondary, 5 accent glow, 6 core glow, 7 team. A small
  palette per renderer, with no textures, one material, and the emission per vertex as today's bank does.

**Hide masks.**
- The base body is split into hide regions: scalp, face, neck, upper chest, abdomen, back, pelvis, and left and right
  upper arm, forearm, hand, thigh, shin and foot (20 bits). Each region lies inside one ragdoll part.
- Each garment declares the regions it hides. The combiner drops those body triangles, together with the base
  underwear in them, rather than layering over them.
- Garment edges sit on region seams, with a 1–2 cm skirt over the visible side.

**Permanent components.** These are never hidden.
- The chest core sits on the chest joint near the heart (source frame about +.05, .12, .15). Every upper garment
  declares `core: cutout` or `housing` (its own ring around the core).
- The device sits on the right wrist joint. Gloves and sleeves keep a keep-out volume around it.
- The export rejects any garment triangle inside either keep-out volume.

**Compatibility.** Resolution is deterministic, in Core, and identical on every client. It never refuses a
combination: it picks a variant or hides a sub-part.
1. The headwear's `hairMode` picks the hair variant: `hood` → `under-hood`, `compress` → `under-hat`, `hide` → none
   shown. The hair id itself is kept.
2. A `full` mask swaps a long fringe to the short fringe; `lower` and `eyes` masks keep the fringe.
3. A hood headwear hides an upper garment's `hood-down` part, and a `high` collar's top band.
4. Headwear on the skinned head uses its fit transform.

**Body skin and legacy mapping.**
- A fourth body-skin key, `modular` (wire entry `body:modular`), selects the low-poly path and reads the look.
- `standard`, `prototype` and `sheldon` stay the voxel bodies, unchanged.
- Old clients drop `body:modular` and draw Standard. That is the legacy fallback, together with the existing
  compatibility-rendering toggle (F9, `ForceCompatibilityRendering`) and a global switch that draws Standard.
- On the modular path:
  - `hat` maps to headwear (Auditor Halo and the Sheldon Head keep their own draws);
  - `bodyColor` maps to primary and the accent to accent.
- The Sheldon Clothes become the DEV-only preset `sheldon`:
  - `sheldon-coat`: a long dark navy/black coat with angular panels and cyan seams;
  - `sheldon-trousers`, `sheldon-gloves`, `sheldon-boots`;
  - `sheldon-mask`: crescent eyes **and** a triangular grin, glow on the accent channel; swappable on its own;
  - `sheldon-hood`: optional headwear.
- The Sheldon Intro dresses its pilot in this preset on the modular path.
- **Defaults:** body `m`, `face-01`, a mid skin tone, hair `short-01`, no mask, the headwear from `hat`.
  - Upper, lower and feet: **open owner decision** whether a new pilot wears the bare base or the Arena Pilot set.
    Proposed: the Arena Pilot set.
  - Gloves, back and equip: none.

## e. Chest core and wrist device

**Chest core.**
- A rigid piece on the chest joint with glow channel 6. Its states are pure functions of replicated data and
  presentation clocks:
  - idle: a slow pulse;
  - hit: the existing `NativeHitFlash` code;
  - low health: a flicker from the replicated hp;
  - down or dead: dim, then off on the corpse;
  - revived: re-ignites from `RevivedAt`.
- The chest body-chip pips are laid out around the core.

**Wrist device.**
- A piece on the right wrist joint: housing, lid hinge, slot, screen.
- States: **closed/idle → opening → chip insertion → chip accepted → activated (glow) → idle**. They are bound,
  read-only, to the existing chip-selection presentation:

| State | Driven by (unchanged authority data) |
|---|---|
| opening, open | `NativeChipSelectTrack` (a PvE row `open`, an Expedition `members[].choosing`), or the draft stage's pick (`NativeDraftStage`, after the authoritative pick time). The lid opens as the panel rises. |
| insertion | The `NativeChipPlant` / `NativeDraftPlant` timeline: the chip enters the cuff. |
| accepted | The plant's seat and cuff pulse, in the chip's rarity colour. |
| activated | The glow after accepted, decaying over about 1.5 s. Optionally a later binding to an active chip effect. |

- Chip authority, offers, inventories, rules, timing and multiplayer stay untouched. The presentation writes nothing
  back.
- **Change required** (presentation, Claude): the display wrist moves from the free hand to the right wrist for every
  class.
  - For right-hand weapons, the carried weapon points down (the plant already supports this) and the left hand taps or
    inserts.
  - Staff, L33t H4x and bow pilots already have the right hand free.
  - First person uses the right display (`NativeChipSelectPose` already has a right-hand case).
  - On the draft stage the left hand always carries the chip and the plant always goes into the right wrist
    (`NativeDraftStage.Left` stops following the card's side).
  - The held chip's size cap (.13 m, from the voxel palm's .145 m) follows the body type's palm width.

## f. Death compatibility

**Death regions.**
- Every vertex of the combined outfit carries one of the 11 ragdoll regions: pelvis, torso (spine, chest, neck and
  clavicles), head, and left and right upper arm, forearm with hand, thigh, and shin with foot.
- The region comes from the vertex's dominant joint mapped to its part root, or from an explicit `region:*` override
  in Blender. For example, a long coat's skirt stays on the pelvis.
- Clothing therefore travels with the right severed part: a sleeve with the forearm, the hood with the head.

**Region visibility.**
- A per-draw 11-bit mask in the shader collapses hidden regions' vertices. It serves severs, crush, first person and
  the per-bone `visible(b)` contract.

**Stump caps.**
- The export authors a cap pair at each sever joint (2, 5, 7, 8, 11, 12, 14, 15, 18, 19): a parent-side stump and a
  child-side end, each about 20–40 triangles.
- Caps draw only when that joint is severed. They keep today's layered look (`NativeBisect` `BisectLook`): the rim in
  the covering garment's paint (from the hide masks), then a lining band, then flesh.

**Detached weights.**
- A severed region's vertices weighted to joints outside the region fold onto the region's root.
- The fold is precomputed per region at export, so a flying forearm does not stretch back to the body.
- The remaining body keeps its full weights and hides the region.

**Ragdoll.**
- `NativeCorpse` is unchanged: 11 rigid bodies and their joints. The skinned puppet reads `Bone(b)`, so connected
  parts deform smoothly.
- Colliders come from the fit volumes. Squash and sprawl (`NativeSmashGib`) work through the bone matrices.

**Bisect on skinned meshes.**
- Bake the pose with Core linear blend skinning at the cut moment, using the corpse's `Bind` matrices.
- Cut with `NativeBisect`'s plane routine.
- Rebind: a new vertex takes the interpolated weights of its edge, and its bind position is the posed point
  inverse-skinned by those weights.
- Caps are weighted to the nearest part root of their half.
- Each half stays a skinned mesh driven by its half's ragdoll bodies. The work runs on the existing worker during the
  hold and never per frame.

**Wounds.**
- Up to 8 wound spheres per pilot, the replicated cap (`p.Wounds` ≤ 8). Each hit point is inverse-skinned into bind
  space on its region.
- The shader darkens and opens them with a rim and an inner colour. The existing wound inside pieces ride their
  joints.
- Mesh carving with `NativeWoundSkin` on the bind-space mesh, cached per (outfit, wounds), stays optional for later.

**Gibs.**
- Voxel burst and crush debris keep the kill boxes, coloured from the outfit's paint by region (a `GibColor`
  equivalent).
- Garments may list gib panels, as the Sheldon coat's `Cloth` boxes do today.

**Arrows.** Lodging depth uses the fit volumes.

**Legacy.** Every voxel body skin keeps today's path exactly.

## g. Performance plan and budget

**Mesh budget.**
- Triangles per pilot, whole outfit: LOD0 ≤ 7,000, LOD1 ≤ 3,500, LOD2 ≤ 1,400.
- 22 bones, at most 4 influences (target 2).
- 1 submesh and 1 material.
- Tiers chosen by `NativeBodySkins.Tier`, the same rule as today: graphics tier, Reduced effects, distance; first
  person keeps Full.

**Outfit combine.**
- Combined once per outfit signature: body type, every slot id and LOD, with colours in the palette and not in the
  mesh.
- Hidden regions dropped; the result cached, least recently used out, at most 16 signatures.
- Shared by the live rig, first person (with a mask), the corpse, the killcam, the portrait and the lobby.
- Built in the lobby or on change, off the frame, never per frame.

**Per frame.**
- No allocation once warm. Pooled puppets, a reused `MaterialPropertyBlock`, palettes in fixed arrays.
- 22 transform writes per matrix-driven draw.
- Conservative `localBounds` (a 1.2 m pose-independent sphere), with `updateWhenOffscreen` off.

**Frozen poses.** Ghosts, afterimages and death-broadcast stills are baked once when captured (Core linear blend
skinning into reused meshes) and drawn static, with no per-frame skin pass.

**Shadows.** Cast from LOD0 and LOD1 only. LOD2 and first person do not cast.

**Horde.** No change: zombies stay instanced rigid voxel parts. A skinned horde would need instanced or
baked-animation skinning, which is a separate decision.

**Benchmark (Phase C, Unity on Windows).**
- The same scene both ways (a DEV toggle): 8 pilots in combat, the Ward horde of 40 and effects.
- Record frame CPU and GPU time, draws, SetPass calls, skinning time (`SkinnedMeshRenderer` update in the Profiler) and
  GC.
- Pass criteria:
  - no more than +0.5 ms CPU and +5 % GPU over voxel at 8 pilots;
  - 0 B GC per frame after warm-up.
- Also check the project's Mesh Deformation setting. It is not in `ProjectSettings.asset`, so defaults apply; GPU
  skinning is preferred.
- CoreTests pin the counts (triangles, vertices, bones, influences, submeshes) and zero allocation in the Core
  per-frame code.

## h. Phases and acceptance

**B: male base proof of concept.** Built in parallel on `claude/lowpoly-base-model` (Blender).
- Male base: natural face, `short-01` hair, boxers, chest core and right wrist device. No other wrist gear, armour,
  gloves, boots or mask.
- Built on section c exactly. Deterministic export, and a manifest.
- Accepted when:
  - the skeleton has `CapturedRig`'s names, order and parents, with the visual offsets and the authoritative ones
    recorded (section c);
  - the weights are valid;
  - soles at 0 ± 1 cm, the visual head centre ± .03 m, within the capsule;
  - every vertex carries a hide region and a death region;
  - faces face out;
  - LOD0 ≤ 6,000 triangles;
  - two exports are byte-identical;
  - there is no licensed content;
  - offline renders posed by real Core poses (idle, run, sprint, rifle aim, melee cut, cast, roll, first-person arms)
    show clean joints;
  - the owner reviews the proportions (j1).
- No runtime change.

**C: small modular demo.**
- Content: 2 hairstyles, 2 tops, 2 pants, 1 removable mask and 1 pair of shoes.
- Code: the Core parser, skinning, combiner, compatibility and hide-mask logic, with CoreTests.
- Runtime: the skinned queue and puppets, behind a DEV-only `body:modular` in the Dev Sandbox and the Locker preview.
- Accepted when:
  - every combination resolves, with no clipping in the renders;
  - hair persists across tops, masks and headwear;
  - the benchmark (section g) passes;
  - one draw and one shadow per pilot.

**D: gameplay integration.**
- Every call site in section 0 draws the modular body:
  - live and first person;
  - intros, lobby poses, portraits, rooftop and draft;
  - spawn-in, hit flash, chips, bands, ghosts and afterimages;
  - corpses, severs, stumps, bisect, smash, vent, Siren drop and revive;
  - wounds and arrows.
- The core and wrist device states are bound. Astra's contract (section i) is implemented, and the backend allowlist
  is deployed with owner approval.
- Accepted when:
  - every path is checked by a CoreTests check or an offline render;
  - a two-client run, killcam, reconnect and late-join test show identical looks;
  - the legacy toggle restores voxel.

**E: full migration.**
- Each path has a parity sign-off.
- The default for new profiles flips to modular (owner decision).
- Standard, Prototype and Sheldon Clothes stay selectable legacy skins.
- The voxel path stays until the owner retires it.

## i. Astra contract (proposal)

Astra implements this; nothing here changes rules.

**Field.** A compact normalized look map, `look` (string), following the `weaponSkins` pattern:
- `slot:value` pairs, comma separated, in canonical slot order;
- only non-default entries;
- ids `[a-z0-9-]{1,24}`, colours `#rrggbb`;
- MaxLength 512;
- "" means every slot is at its default.
- Example: `body:f,hair:bob-01,hairColor:#2a2f3d,upper:arena-jacket,lower:cargo-01,feet:runner-01,accent:#22e6ff`.

**Where.**
- `ConversionPreferences.look` (default "").
- `NativeCosmetics.look`: no initializer, so a profile without it leaves the local pick alone (as `hatColor`).
- `NativeSeat.look` (default "").
- `NativeSeatFrame.look` in game and lobby frames, plus `LobbySeat`.
- `NativeGameFrame.Valid` and the lobby message validation reject a `look` longer than MaxLength.
- The backend `accountCosmetics` gets a `lookMap` allowlist built from the shared `CharacterCatalog.json`, and adds
  `modular` to `BODY_SKINS`.

**Normalization.** A Core `NativeLook.Normalize(map, access, now)`, allocation-free when reading, used on save, on
account load and in `SanitizedSeat`:
- unknown slots and ids are dropped (the slot default);
- invalid colours become Auto;
- an id unsupported for the body type is dropped;
- DEV-only ids (`dev: true` in the catalog: the `sheldon-*` pieces) need live DEV access (`NativeCosmeticGate.DevOnly`);
- restricted ids need a grant `look:<id>` (`NativeAccess.Allows`);
- `body:modular` stays DEV-gated until the owner opens it.

Compatibility resolution is presentation, not validation: the authority stores the picks as given.

**Behaviour.**
- Profile changes apply only in the lobby, as today.
- Reconnect and late join re-run `SanitizedSeat` from the profile message.
- Corpses, killcam, spectators, the final killcam and intros resolve the look by seat id, as the body skin does
  (`SeatWeaponSkins`). An optional `NativeDeath.look` snapshot would dress a corpse whose seat has left; Astra decides.
- Seats ride every game frame. The map adds at most about 0.5 KB per seat before compression. Sending it only in lobby
  frames and on change is Astra's call.

**Rule: no gameplay effect.** No hit volume, collider, damage, speed, timing, chip or network authority code reads
`look`. Proposed check: a CoreTests static scan, as ChipModuleChecks does, that the authority sources never name
`NativeLook`.

## j. Risks and open owner decisions

**Open decisions.**
1. **Proportions.** Decided by the owner (2026-10-20): human proportions on a presentation-only visual skeleton
   (section c). Open: the weapon-hand choice (P1 or P2) and whether any authoritative volume should follow (P3).
2. **Height.** The visual figure is 1.74 m (7.5 heads); the authoritative capsule (1.70 m) and head box (1.49 m) are
   unchanged. The drift table in section c measures the difference.
3. **Wrist side.** Confirm the right wrist for every class, which moves the chip-select display wrist off the free hand
   (section e).
4. **Default outfit.** A new pilot wears the bare base or the Arena Pilot set.
5. **Skin tones.** A palette or free colours. Do faces and hair need entitlements?
6. **Hats.** Keep today's hats on the modular head (with fit transforms) until low-poly versions exist?
7. **Team identity.** Team bands on garments, or a team paint channel on the core and device?
8. **Zombies.** Stay voxel? The sheet's walker suggests a later low-poly horde, which needs instanced skinning.
9. **Backend.** A Sites deploy for the allowlist (owner approval) before Phase D persistence.

**Risks.**
- Skinning has a per-frame cost, and instancing across pilots is lost; measured in Phase C.
- With only 22 joints, a wrist roll and an upper-arm roll twist the forearm and shoulder ("candy-wrapper"). The
  mitigation is cuffs and gloves weighted to the wrist; derived twist helpers come later.
- Pieces tuned to the voxel volumes (body chips, team bands, Cyber Lord pieces, hats, arrow depth) need fit offsets per
  body type, or they float or sink.
- Ghost and afterimage looks and the spawn-in, revive and Cyber dithers need skinned or baked variants of each shader,
  which is a broad surface to verify.
- No Unity, Windows, GPU or two-client run is possible here. Every number above that is not from Core is an estimate.

## k. Secondary motion (hair and cloth)

The owner asked for hair and clothing movement. It is presentation only: no authority, rule, timing, hit volume,
collider or network field reads or writes it.

**Chains.**
- Short spring chains of optional `sec-*` bones parented to a CapturedRig joint, recorded in the manifest
  (`springs`).
- Each chain records its spring parameters:
  - stiffness (Hz) and damping ratio toward the rigid-follow pose;
  - gravity, linear air drag and the maximum angle from the rigid direction;
  - colliders in joint frames (a head sphere, a neck capsule).
- The male base ships six 2-bone hair chains: fringe left and right, sides and back.
- They are tuned for spiky low-poly hair: about 8–10 Hz, damping .36–.42, 12–16°. The previews show a subtle bounce
  and sway, not jelly: a mean swing of about 4–10° in the run, 7–11° in a melee cut and under 1° at idle.

**Proposed Core solver** (Phase C or D, Claude). Deterministic and allocation-free:
- A jiggle offset per chain joint from its rigid target, integrated in fixed sub-steps (for example 1/180 s) from the
  presentation clock, never from the frame rate:
  - the target's own acceleration acts as an inertial force;
  - then the spring, drag and gravity;
  - then the bone lengths, the max angle and the colliders.
- Per-tier on/off: off at the Low tier and with Reduced effects.
- A budget of 8 pilots × N chains. Beyond 15–20 m the chains freeze at rest (LOD).
- First person never runs it.
- Killcam, replays, spectators and corpses re-simulate from the replayed pose, so nothing is networked. A corpse's
  chains settle, then freeze with the ragdoll's sleep.

**Garments (Phase C).** Garments add their own chains on the same terms:
- coat tails and hood (spine and chest), scarf ends and straps (neck and chest), cargo pockets and belt pouches (hips);
- sphere and capsule colliders come from the body type's fit volumes (section c), so cloth clears the thighs and
  torso;
- a chain's weights fold onto its parent joint when secondary motion is off.
