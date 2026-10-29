# Zombies on the player's model

Owner request (2026-10-29): "Implement the attached zombie designs in Chip Arena using the existing low-poly player model as
the starting point ... Shambler, Runner, Spitter, Brute, and Hulk, with two male and two female visual variants per type."
The reference sheets (four views and a face per variant) are the owner's; they are visual references, not geometry.

Presentation only: movement authority, capsules, damage volumes, reach, hit times, cooldowns, AI and the network are unchanged.
This replaces the owner's earlier "keep voxel style" for zombies (2026-10-17) and closes BOZO-INTEGRATION's open question
"whether zombies stay voxel".

## 1. What there is to build on (inventory, 2026-10-29)

**The player model.** One baked prefab (`Resources/ChipArenaVisualBody`, made on the owner's PC by the git-ignored local tool from
the licensed BoZo pack; docs/BOZO-INTEGRATION.md).
- **Skeleton and binding:**
  - 107-bone UE-style skeleton (`pelvis`, `spine_01–05`, `neck_01/02`, `head`, `clavicle/upperarm/lowerarm/hand_l|r`,
    `thigh/calf/foot/ball_l|r`, twist bones, fingers);
  - bound to the game's 22-joint pose by `Core/NativeVisualRig` (rest-aligned limbs, planted-foot IK, about 34 µs a pose).
- **Wardrobe:**
  - the bake's look data (`Resources/ChipArenaVisualLook`) lists each piece's slot, id, renderers, covered body parts and tags;
  - 25 player pieces plus the DEV Sheldon pieces;
  - the pack has 166 pieces (24 tops, 16 bottoms, 13 shoes, 30 hair), most of them not baked.
- **Body shapes:**
  - 9 blendshapes on every body part and on most clothes: `BodyType` (male/female), `Weight`, `Muscle`, `Belly`, `Chest`,
    `NeckThickness`, `Curvy`, `ButtSize`, `WaistSize`;
  - face shapes on the head (`Maturity`, `Sharpness`, `Roundness`, …);
  - all of them kept in the meshes and settable at run time.
- **Shader:** the toon shader (`ChipArena/VisualBodyToon`) draws nine colours per part, the outline, the hit flash and the skin tone
  (`SkinToned`).
- **Gaps:**
  - a single prefab and skeleton: no separate female or heavy body;
  - the binding reads bone positions and rotations but never scale.

**Today's zombies.** Code-built voxel bodies (`Core/NativeHordeModels`, one rigid mesh per joint, instanced), posed by their own
motion system:
- the ZOMBIE PRO mocap pack;
- the gait with world-planted feet;
- the authority's strike times;
- hit reactions, the pop killer, the guest clock.

All of it writes a `CapturedRig.Pose` on a per-add rig, the same 22 joints the model binds from. Other constraints:
- Kinds: shambler 0, runner 1, brute 2, spitter 3, crawler 4, bloater 5, hulk 6 (`Core/NativeHordeKinds`).
- Hit volumes: radius .42, height 1.7 for every kind but the Hulk (.954 by 3.06, drawn at 1.8×).
- Tiers:
  - the nearest 24 adds (16 on the low graphics tier) and the possessed one draw Full;
  - Reduced to 40 m, Impostor past it.
- Gore:
  - wounds fitted to the Full voxel surface;
  - limbs severed and sliced at joints, thrown as the model's own joint parts;
  - corpses as 22 kept bone matrices that sink away;
  - nothing of it goes through the pilots' corpse, ragdoll or bisect paths.

**Timings the models must meet (authority, unchanged).**

| kind | walk/run m/s | reach | hit at (s) | cycle (s) |
|---|---|---|---|---|
| shambler | 1.5 / 3.4 | 1.75 | .34 | 1.05 |
| runner | 2.6 / 4.6 | 2.0 | .34 | .85 |
| brute | 1.3 / 2.4 | 2.1 | .43 | 1.7 |
| spitter | 1.6 / 2.9 | 11 | .8 | 2.6 |
| hulk | .75 / 1.15 | 3 | 1.1; punch .62, swipe .78 | 3.4 |

The gait already lands every blow's contact frame at `LastAttack + hit`, so a model posed from that pose contacts on time.

## 2. The approach

One model, twenty variants as data (`Core/NativeZombieLooks`).

**Each variant is:**
- a piece per wardrobe slot, the first candidate the bake carries, falling back on one of today's pieces;
- each piece's colour, kept at its own light and shade;
- skin and blank eyes;
- body and face shapes by name;
- a bone build:
  - limb girth on the twist bones (leaves, their cross-section only);
  - hand and head size;
  - shoulder span (set before the binding reads the rest pose);
- wear: grime, a few torn patches, rotting skin, on texture coordinates so it stays on the cloth.

**Shared by every variant:** meshes, rig, materials, motion and gore.

**Female variants:**
- the pack's own `BodyType` (−15..35), `Curvy`, `ButtSize`;
- narrower shoulders through the build;
- they share the kind's motion.

**Silhouettes by build, not colour:**
- **Shambler:** average.
- **Runner:** lean.
- **Spitter:** gaunt, with the throat sac (to come).
- **Brute:** muscled or heavy-set, girth 1.12–1.25.
- **Hulk:** 1.8× with gorilla arms (girth 1.4–1.55, hands 1.45).

The checks hold the kinds' build signatures apart.

**Zombie-only pieces** (ids `z-…`, never in the Locker):
- `z-hoodie-vest`, `z-work-jacket`, `z-cardigan`, `z-track-jacket`, `z-zip-vest`, `z-work-shirt`, `z-utility-jacket`, `z-blouse`,
  `z-scrubs-top`, `z-denim-vest`;
- `z-joggers`, `z-capri-leggings`, `z-scrub-pants`, `z-overalls` (a lower piece with its bib), `z-cargo-pants`;
- `z-flats`.

The bake adds whichever the pack can supply. Until then each variant wears its fallback, the nearest of today's pieces (for
example the overalls fall back to baggy pants).

## 3. What is in the game (2026-10-29, `claude/zombie-models`)

- **Variant table and pick:** `Core/NativeZombieLooks` holds all twenty variants. The add's id picks the variant, the same on every
  peer.
- **Live kinds:**
  - the **shambler** is live on the model, both male and female variants (the owner's step 2);
  - runner, spitter, brute and hulk are drafts: `--ca-zombie-drafts`, or `NativeZombieLooks.Drafts` from the local tool, draws
    them all.
- **Runtime** (`Runtime/NativeZombieModelPresentation.cs`):
  - a Full-tier add of a modelled kind draws the model in its variant, posed from its gait's pose;
  - feet are planted on the gait, on a door's wake and on a landing; airborne in a dive and a jump-down;
  - no weapon grasp; the hit flash; the eyes glow brighter through a wind-up.
- **Body supply:**
  - bodies are kept per variant (6 spares) and reused;
  - new ones come from their own pool, made off the main thread while a horde is about (8 ahead, never at once), 3 a frame at
    most;
  - an add without one that frame draws its voxel model.
- **Gore and death:**
  - lost and sliced parts collapse at their joint on the model;
  - the voxel path's raw cap, blood and flying part stay;
  - the death plays on the model;
  - the settled corpse is baked once into static meshes, drawn sinking on the voxel corpse's timeline;
  - its body goes back to the spares.
- **Shader** (`NativeVisualBodyFx.cginc` `ZombieLook`, both passes): piece colour, grime, torn patches, rot and blank eyes. With it
  unset, every pilot is unchanged.
- **Voxel stays:**
  - the Reduced and Impostor tiers;
  - every kind not yet live;
  - without the bake, with `--ca-voxel-zombies`, or while Settings · Player model is Classic.

## 4. Not done yet (open)

**Owner's PC (Unity Editor, the pack, the bake):**
- the zombie-only pieces in the bake, with their covers and tags;
- checking that the eye mesh's renderer is `Eyes.*` and that the shape names match (the game logs them);
- the actual captures: the Shambler pair beside the player in neutral, walk, turn, attack, hit and death;
- tuning palettes, wear and builds against them;
- crowd cost: 24 modelled adds plus voxel tiers, Editor and Windows player, with the conditions stated.

**Code, after the captures:**
- the spitter's throat sac (a skinned mesh on the neck that swells with the spit, as the voxel sac does);
- wounds fitted to the model's surface (today a modelled add shows no wound marks; the blood and mist remain);
- flying parts and stumps drawn from the model, not the voxel part;
- arrows lodging on the model's surface, not the voxel joints;
- a reduced model for the Reduced tier, replacing the voxel style at distance.

**Motion:**
- the zombies' own motion is the gait's; the model only rebinds it;
- the owner's per-type notes (shambler drag, runner intent, spitter guard, brute and hulk weight) need the captures to judge.

Any motion change that would need a different rule goes to Astra as a measured mismatch; none is known today.

## 5. Checks

`CoreTests --zombie-models` covers the table, the pick, the shape names, the shader's zombie look and the runtime's wiring.
`--zombie-gore` admits the new presentation file among the gore's readers.

None of these is a Unity render. Visual quality, foot contact on the model, crowd cost and multiplayer views are verified only in
Unity on the owner's PC.
