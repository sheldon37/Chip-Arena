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
  - the **shambler** is live on the model, both male and female variants (the owner's step 2); since the PC pass (section 4)
    all five kinds are;
  - runner, spitter, brute and hulk were drafts until then (`--ca-zombie-drafts`, or `NativeZombieLooks.Drafts` from the local
    tool, still draws every kind with variants).
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

## 4. The PC pass (2026-10-29, `claude/zombie-models-pc`)

Done on the owner's PC with the Unity Editor and the bake (owner, the same day: "Need to make the models look more undead, torn
clothes, flesh wounds, bones visible, etc. Also, they seem to render back to voxel form on death. Improve this massively").

- **Checked in Unity:** the eye mesh is `Eyes.Eyes_AnimeBasic` (`ZombieEyePart` matches); the shapes are `BS_<part>.Shape_<Name>`
  on the body parts and clothes and `BS_Head_AnimeYoung.Shape_Maturity|Sharpness|Roundness` on the head (`ShapeMatches` matches;
  `Shape_EyeRoundness` is rightly not `Roundness`). Neither needed a fix.
- **The zombie-only pieces in the bake** (the local tool's wardrobe), the pack's closest piece to each:
  - `z-work-jacket`, `z-utility-jacket`: Top_SchoolBoyJacket; `z-cardigan`: Top_ComfyCartagan; `z-track-jacket`: Top_ThickHenslay
    (raglan sleeves); `z-work-shirt`, `z-blouse`: Top_DressShirt; `z-zip-vest`, `z-denim-vest`: Top_SleevelessDressShirt;
  - `z-capri-leggings`: Bottom_RippedCapris; `z-flats`: Feet_BalletFlats;
  - not in the pack (fallbacks kept): `z-hoodie-vest`, `z-scrubs-top`, `z-joggers`, `z-scrub-pants`, `z-overalls`, `z-cargo-pants`.
- **The undead look:**
  - the shader's tears scaled to the pack's texture layout (each piece packed small in its texture, so the old noise made one
    leg-sized patch): small ragged holes with raw, bloodied skin in them; dried blood soaked into the cloth; gashes on the skin
    with bone at their deepest; trims and pockets brighter than the piece ease toward a dirty grey (the jeans' light pockets
    read as bare skin before); blank eyes without an outline (a map's tinted line round them read as an iris);
  - decay openings (Core `NativeZombieDecay`; two to four sites a variant): cut through skin and clothes in the meshes' rest
    space (the shader's `DecayClip` on their fourth texture coordinates, written once per mesh), each onto a flesh pit on its bone
    with the skeleton's own pieces there (Core `NativeModelSkeleton`): ribs through a flank or the back, forearm and upper arm
    bones, the jaw through a cheek, the tibia, the femur; planned once per variant at the bake's rest; corpses keep them;
  - heavier tears and rot, and blood, in the variant table.
- **Deaths on the model:** severed, sliced and gibbed (crushed) parts are cut from the zombie's own model (each renderer baked
  once at the launch, the triangles skinned mostly to that joint's bones kept), drawn with its looks; no voxel limbs. A gib's
  trunk still bursts in blood as before; the raw cap on a part is the gore's.
- **The spitter's throat sac** (Core `NativeZombieSac`): skinned on neck_01 and the head, swelling forward and down with
  `NativeHordeFx.Swell` through the spit's wind-up, its acid glow rising with it, released at the spit.
- **All five kinds live** (`NativeZombieLooks.Live`): each kind's variants were captured beside the player, and each blow lands
  on the model at the authority's contact time (the runner's lunge at +.34 s, the brute's slam at +.43 s, the hulk's punch at
  +.62 s, the shambler's swing at +.34 s).
- **Crowd cost (Editor, measured):** 24 adds hunting a god-mode pilot on Brickfall (Ward Zero is not playable as an arena
  match through the local tool), i9-13900K, RTX 4090, the Editor's Game view at 2560x1440, the default graphics tier:
  - modelled: mean 20.2 / 20.3 ms, p95 22.5 / 22.1 ms (two runs);
  - `--ca-voxel-zombies` (the same crowd voxel): mean 15.4 / 16.9 ms, p95 17.1 / 18.7 ms;
  - no zombies: mean 8.3 ms, p95 9.6 ms;
  - so about 4 ms a frame for 24 modelled adds over voxel (the profiler: the game's PlayerLoop about 20 against 15.5 ms).
  Not measured in a Windows player.

## 5. Not done yet (open)

- A Windows player measurement of the crowd (the Editor's numbers above are the only ones).
- Wounds fitted to the model's surface during life (a living add's bullet marks; the decay openings are fixed per variant).
- Arrows lodging on the model's surface, not the voxel joints.
- A reduced model for the Reduced tier, replacing the voxel style at distance.
- The owner's per-type motion notes (shambler drag, runner intent, spitter guard, brute and hulk weight): the captures show the
  gait's motion on the model with blows on time; any change that needs a rule goes to Astra as a measured mismatch (none known).
- A head burst (`HeadGib`) still leaves the gore's own stump and fountain on the model's collapsed neck.

## 6. Checks

`CoreTests --zombie-models` covers the table, the pick, the shape names, the shader's zombie look, the throat sac's geometry, the
decay sites (placed on a mannequin's joints, left and right, square to their limbs) and the runtime's wiring (openings, pits,
bones, model gore parts).
`--zombie-gore` admits the new presentation file among the gore's readers.

None of these is a Unity render. Visual quality, foot contact on the model, crowd cost and multiplayer views are verified only in
Unity on the owner's PC.
