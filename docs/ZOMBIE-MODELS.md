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

## 4b. Motion on the model (2026-10-29, `claude/model-zombie-motion`)

Owner (2026-10-29): "the idle; they stand "unnaturally" like bending their lower back weird", "What we were using for the voxel
ones were working, but it doesn't seem to translate well for the player models".

**Why.** The gait poses the voxel rig (a .615 m torso on .50 m legs, one block a joint); the model is a .50 m torso on .85 m
legs with a five-bone spine. Measured on the first outfit's measured skeleton (`CoreTests --zombie-posture`), four voxel-era
choices read wrong on it:
- **The hunch hinged at the belt.** The capture's lumbar and lower thoracic bend both sit on the rig's Spine joint (the
  retarget maps Spine and Spine1 there), which the model spreads over `spine_01`/`spine_02`, 2 to 9 cm above the pelvis:
  60 to 76% of the trunk's bend sat there.
- **The hips sat far back.** The gait sits the hips back to keep the face over the hit capsule (Balance), and the binding
  scaled the pelvis's whole offset by the legs' ratio (1.71), across as well as up.
- **The knees folded.** The capture's hips sit .025 under its standing height and the gait's floor .015 over the model's soles;
  in a stride the gait also sinks the hips so the short voxel legs reach the feet. On the model's 1.71-times-longer legs (its
  feet are the gait's) all three only bend the knees: about 45 to 50 degrees at rest, 64 to 84 walking.
- **The arms splayed.** The gait sets the narrow voxel bodies' clavicles in and reaches the hands from there, angling the arms
  out; the model copies arm directions from its own shoulders.

**The fit** (`Core/NativeZombiePosture.cs`, on a copy of the gait's pose the model binds; the rig, voxel tiers, gore, hit volumes
and the authority keep the gait's own):
- 60% of the Spine joint's bend moves up onto the Chest joint (`spine_03`–`05`); the chest's, neck's and head's world turn
  are unchanged, so the gaze and reach are the gait's;
- each upper arm turns back to the direction from the shoulder its reach was planned from;
- the model's pelvis takes the horizontal offset at the game's metres (`NativeVisualRig.State.Across`);
- it rises by the clip's .025, the floor gap and the gait's stride sink (`NativeHordeGait.Sunk`), as far as each planted leg
  reaches with 1% of its length to spare (`Stand`, `Rise`; it eases by 6 mm a pose, and comes down at once where a planted leg
  needs it; with nothing planted, in a dive or a fall, it eases out);
- a legless crawl's pelvis sits at the gait pelvis's own height (`Level`; it floated about 40 cm up before).

Pilots are untouched (their state's stance is off). Nothing changes timing, reach, volumes or rules: the blows still land on
the gait's contact frames.

**Measured** (synthetic skeleton, two looks per kind, 4 s each at 60 fps after a 1.5 s settle; not a render):

| kind | gait | lumbar share of the trunk bend | knee (mean) | hips behind the feet | upper arm out |
|---|---|---|---|---|---|
| shambler | rest | 0.63 → 0.26 | 45° → 24° | 16 → 10 cm | 34.5° → 25.1° |
| shambler | walk | 0.75 → 0.31 | 64° → 42° | 9 → 3 cm | 43.1° → 35.7° |
| shambler | run | 0.70 → 0.28 | 72° → 58° | 15 → 7 cm | 47.7° → 40.0° |
| runner | rest | 0.67 → 0.27 | 50° → 26° | 10 → 6 cm | 22.2° → 15.6° |
| runner | walk | 0.61 → 0.26 | 84° → 71° | 4 → 4 cm | 37.9° → 31.6° |
| runner | run | 0.63 → 0.26 | 80° → 68° | 11 → 7 cm | 41.4° → 35.3° |
| spitter | rest | 0.72 → 0.29 | 47° → 24° | 16 → 9 cm | 29.1° → 18.9° |
| spitter | walk | 0.71 → 0.29 | 68° → 46° | 5 → 3 cm | 29.6° → 20.3° |
| spitter | run | 0.72 → 0.29 | 73° → 59° | 13 → 6 cm | 48.5° → 39.0° |
| brute | rest | 0.71 → 0.29 | 43° → 23° | 18 → 11 cm | 19.7° → 19.7° |
| brute | walk | 0.69 → 0.28 | 66° → 47° | 2 → 2 cm | 20.3° → 20.4° |
| brute | run | 0.76 → 0.32 | 71° → 51° | 14 → 7 cm | 47.0° → 47.0° |
| hulk | rest | 0.72 → 0.29 | 61° → 44° | 3 → 2 cm | 47.0° → 47.4° |
| hulk | walk | 0.65 → 0.26 | 75° → 65° | 6 → 5 cm | 46.4° → 46.4° |
| hulk | run | 0.62 → 0.25 | 85° → 80° | 13 → 9 cm | 48.9° → 48.8° |

Planted ankles stay on their marks (p95 0.0 cm); the hips move no faster than before (p95 lower for every kind but the
Hulk's walk, .60 → .77 m/s); the head stays over the hit capsule. The brute and the Hulk set no clavicle inset, so their arms
are unchanged; the Hulk's deep knees are its heavy stance (by design). The runner still walks low: its chase takes are
captured crouched.

**Also:** Graveyard Shift's and Corpse Party's summoned zombie draws on the model (a shambler variant), falls on it and bleeds
blood drops instead of voxel puffs (docs/BOZO-INTEGRATION.md, "First person, body chips and summons on the model").

## 5. Not done yet (open)

- A Windows player measurement of the crowd (the Editor's numbers above are the only ones).
- Wounds fitted to the model's surface during life (a living add's bullet marks; the decay openings are fixed per variant).
- Arrows lodging on the model's surface, not the voxel joints.
- A reduced model for the Reduced tier, replacing the voxel style at distance.
- The owner's per-type motion notes (shambler drag, runner intent, spitter guard, brute and hulk weight): the captures show the
  gait's motion on the model with blows on time; any change that needs a rule goes to Astra as a measured mismatch (none known).
- The posture fit (4b) in Unity: the stance on real outfits, the runner's low chase, the Hulk's knees; the ZOMBIE PRO exports
  still carry the retarget's spine softening made for the voxel torso (`scripts/mocap/mixamo-retarget.mjs`), which a
  re-export for the model could drop (it needs the private pack).
- A head burst (`HeadGib`) still leaves the gore's own stump and fountain on the model's collapsed neck.

## 6. Checks

`CoreTests --zombie-models` covers the table, the pick, the shape names, the shader's zombie look, the throat sac's geometry, the
decay sites (placed on a mannequin's joints, left and right, square to their limbs) and the runtime's wiring (openings, pits,
bones, model gore parts).
`--zombie-gore` admits the new presentation file among the gore's readers.
`--zombie-posture` measures the model fit (4b) and pins its wiring.

None of these is a Unity render. Visual quality, foot contact on the model, crowd cost and multiplayer views are verified only in
Unity on the owner's PC.
