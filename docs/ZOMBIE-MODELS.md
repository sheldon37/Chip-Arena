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

## 4c. On the PC: the legs in a stride, and the Hulk a mutant (2026-10-29, `claude/zombie-motion-pc`)

Owner (2026-10-29), after 4b: the motion review on the PC, then "Hulk zombies need to look a LOT different, they are essentially
mutated. Theyre supposed to be huge, muscled zombies. They look just like a taller version of the player model right now", and a
reference of the giant-brute archetype (a head sunk between boulder shoulders, a barrel chest, huge long arms on small legs).

**The legs (Core/NativeZombiePosture.Stance, NativeVisualRig.State.Room, NativeHordeGait.Crouch and Busy).** Measured in Unity
(the local tool's stance log: each knee's bend, the pelvis over the soles; five male A variants, idle facing the pilot and
hunting it on Brickfall), 4b's fit stood the adds up at rest but a stride still sat in a squat: the shambler's planted knee
about 50° walking (a person's is 5 to 20°), the Hulk 41° standing. 4b raised the hips only by the gait's own lowering (the
clip's .025, the floor gap, the stride sink), and the takes' hips sit lower still (1 to 4 cm of rig) on the voxel legs' share.
Now the model's hips also stand out of the takes' own crouch and by `Lift` (8 cm) more, as far as the planted legs reach with
`Room` to spare (.99 of the leg, about 16° of knee; the Hulk's .965, about 30°: its heavy stance), except where a blow, a reel, a
roar or a keyed move owns the pose (`Busy`): those crouches are the gait's to show. Between two planted steps (a run's flight)
the rise holds instead of climbing.

| measured | before (4b) | after |
|---|---|---|
| synthetic, planted knee walking / running (shambler, runner, spitter, brute, hulk) | 34-45° / 39-71° | 26-36° / 31-42° |
| synthetic, knees at rest | 23-26°, Hulk 44° | 20-21°, Hulk 32° |
| Unity, idle, lower knee (male A) | shambler 24°, runner 38°, spitter 23°, brute 20°, Hulk 41° | 18°, 16°, 16°, 17°, 30° |
| Unity, hunting (walk and blows), lower knee | shambler 50°, runner 43°, spitter 40°, brute 22°, Hulk 46° | 33°, 16°, 38° (spits from range: busy), 16°, 36° |

The captures (all five kinds, male and female A: idle, walk, run, a blow, a stagger, a fall and the corpse, a legless crawl)
show the hunch rounding the upper back, soft knees at rest, straight-legged strides, the crawl on the floor, planted feet and
blows on time. The hips' speed is no higher than the gait's (p95 within 4b's bounds).

**The Hulk, a mutant (Core/NativeZombieMutation, `NativeZombieLooks.Variant.Mutant`, Build `Right`/`Neck`/`Reach`).**
- The build: limbs to twice a person's girth, hands 1.85 to 2x, the shoulders' span 1.36 to 1.42 (the arms hang outside the
  trunk), the neck shortened to .6 (the head sits low between the shoulders), the arms 1.26 to 1.3 times as long (the elbow and
  wrist set out along the limb), the right arm a little past the left; pale grey-pink skin; the males bare-chested.
- The growth: muscle masses placed from the rest joints, summed as fields and polygonised as three smooth surfaces (surface
  nets: the trunk, each arm), so they meet in fillets instead of reading as separate balls. The trunk: trapezius and a mound
  behind and round the sunken head, a barrel chest, a gut, lats. Each arm: a boulder shoulder, the upper arm, a forearm club
  that flares and turns raw toward the fist. Bone spurs (shoulders, the back, the right forearm) and glowing pustules.
- Skinning: each vertex on the two bones whose masses it grew from, by their shares there, so the elbow and shoulder bend
  smoothly; spurs and pustules rigid. Drawn in the toon by its vertex colours with the outline; outside the decay openings.
- Cost: built once per variant when its first body is made (33 ms in the .NET checks; four variants), 6.5k vertices and 12.8k
  faces, one more skinned renderer on each Hulk.

**Crowd cost (Editor; i9-13900K, RTX 4090, Game view 2560x1440, Brickfall, 24 adds hunting a god-mode pilot, frames 11 to 19 s,
the same conditions as section 4).** Modelled 20.8 / 20.9 ms mean, 22.9 / 22.6 ms p95 (4: 20.2 / 20.3, 22.5 / 22.1): the fit's
pose copy and rise cost about .6 ms for 24 adds. Voxel 15.6 ms (17.3 p95), no zombies 8.5 ms (9.5 p95). Measured before the
Hulk's growth (one Hulk in this crowd). A Windows player was not measured.

## 4d. Runs and sprints: the legs in step and pointing forward (2026-10-30, `claude/zombie-gait-model`)

Owner (2026-10-30): "Zombie run looks a little weird at the moment, sometimes their legs are completely out of sync and move weird
directions. Evaluate the runs/sprints and all movements of zombies for new model. Please make changes and make it look more
realistic (Use pack as needed)".

**Why** (measured offline: each modelled kind's gait at its walk and run on up to six take pairs, bound to the measured skeleton
as the runtime binds it; and each ZOMBIE PRO loop take's own legs):
- **Swinging feet off the gait's.** The model only put a foot on the gait's ankle once it was nearly down (under 10 cm). In a
  swing it copied the gait's leg turns onto its 1.71-times-longer legs, so the foot swung 15 to 31 cm from where the gait put it.
  Then it snapped back onto the gait's ankle as it came down, at up to 25 m/s.
- **Knees aside and backward.** The knee bent toward the model's own copied knee, measured off the moved hip-ankle line. That
  turned the knees up to 45 degrees aside (p95), and 132 degrees (backward) on the brute's limping walk.
- **The takes out of step with the feet.** The gait plants the right foot half a cycle after the left, kept within .4-.6 of the
  exporter's right-strike mark. But those marks miss on several takes: chase-1-full's right foot strikes at .49 of its cycle,
  marked .19. The limping takes strike it at .70 (walk-4) and .75 (chase-5, chase-5-full). So the trunk, arms and swing lift
  played up to a third of a cycle out of step with the feet.
- **Sprints bounding.** The sprints' marks lift each foot a fifth of a cycle after it lands, so the run was in the air 60% of the
  time.
- **Dragged run strides.** A walking take run fast stands for less of the gait's cycle than of its own. The swinging foot's height
  was read from the take at the gait's phase, where the take's foot often still stood: it dragged, then kicked late.

**The fix** (presentation only: no rule, timing, hit volume, damage, cooldown, movement or network value changes; the blows still
land on the gait's contact frames):
- `Core/NativeZombieMotion`: each loop take's feet are measured once when the takes load. The right strike is read off the time
  between the two feet coming most forward under the hips, against the left strike at the cycle's start (where the exporter cuts
  the loops). Each take is then time-warped so its right strike plays at the gait's half cycle (`Warp`, up to .28 cycles; the
  take never plays slower than about a third of its pace). The right foot steps half a cycle after the left on every take
  (`Offset`). A sprint stands for its own measured stance (`Duty`: .42 with the clamp, not .2); the walks keep the marks'.
  `SwingLift` gives each foot's own swing arc, from most back to most forward, eased off and onto the floor.
- `Core/NativeHordeLoco` samples the takes on the warp. `Core/NativeHordeMotion` lifts a swinging foot on the takes' arcs over
  the gait's swing. It eases the low-knee lift in and out (the Hulk's foot jumped 10 cm in a frame as it lifted), caps a knee at
  20 degrees aside of forward about its leg (`KneeSplay`, a guard), and steps a planted foot whose heel has peeled all the way
  (the ball slid 3 cm as an add broke into a run).
- `Core/NativeVisualRig.State.Follow`, set by `NativeZombiePosture.Stance` for every upright zombie (not a legless crawl, not a
  pilot): every model foot goes to the gait's ankle, a swinging one too, lifted by the legs' ratio. Every knee bends the gait's
  knee's way off the gait's own leg, 5 cm forward of the hips.

**Measured** (synthetic skeleton, 5.5 s at 60 fps after a 1.5 s settle per take pair, the worst of up to six pairs; speeds in
the rig's metres, a kind's world speed over its scale; not a render):

| kind | gait (m/s) | swinging ankle off the gait's, max | fastest foot | knee aside, p95 | take in step (worst) | swing height (median, rig) |
|---|---|---|---|---|---|---|
| shambler | walk (1.50) | 19 → 0 cm | 15 → 4 m/s | 45° → 14° | 0.66 → 0.84 | 5 → 6 cm |
| shambler | run (3.40) | 28 → 0 cm | 11 → 7 m/s | 30° → 8° | 0.53 → 0.53 | 8 → 10 cm |
| runner | walk (2.60) | 26 → 0 cm | 10 → 8 m/s | 44° → 20° | 0.95 → 0.85 | 14 → 15 cm |
| runner | run (4.60) | 29 → 0 cm | 14 → 10 m/s | 27° → 22° | 0.92 → 0.91 | 12 → 14 cm |
| spitter | walk (1.60) | 17 → 0 cm | 6 → 4 m/s | 26° → 8° | 0.75 → 0.77 | 6 → 6 cm |
| spitter | run (2.90) | 26 → 0 cm | 10 → 6 m/s | 36° → 9° | 0.58 → 0.58 | 8 → 10 cm |
| brute | walk (1.30) | 16 → 0 cm | 10 → 3 m/s | 132° → 16° | 0.31 → 0.84 | 6 → 6 cm |
| brute | run (2.40) | 24 → 0 cm | 10 → 4 m/s | 42° → 16° | 0.59 → 0.63 | 6 → 9 cm |
| hulk | walk (0.42) | 26 → 0 cm | 16 → 3 m/s | 29° → 13° | 0.66 → 0.83 | 10 → 10 cm |
| hulk | run (0.64) | 31 → 0 cm | 25 → 4 m/s | 17° → 15° | 0.45 → 0.90 | 11 → 11 cm |

The knee is measured about the leg's own line. "In step" is the correlation of the take's own feet (fore-aft under its hips)
with the gait's. At the run, walking chases (chase-2, chase-1) stay near .55: their stance is longer than the run's, and the
swing arcs carry their feet. The fastest foot is now within twice the ground speed and 3 m/s, which is what a swinging foot
does. The runs' mean planted knee is 34 to 41 degrees (31 to 36 before; the Hulk's 42 as before), as a runner's bends at
mid-stance.

**For Unity on the PC:** the runner's and shambler's sprints (both feet should alternate under the hips, knees forward, a short
flight between steps), the brute's and Hulk's walk-4 (the limp in step), and the spitter breaking into a run and stopping.

## 4e. The pace: a stride for the model's own legs (2026-10-30, `claude/zombie-model-pace`)

Owner (2026-10-30): "I think a lot of the issue is the sped up animations on the zombie".

**Why** (measured offline). The gait's cadence and reach were tuned for the voxel rig's .50 m legs, and the model's feet follow
the gait's footprints (4d). The model's legs are 1.71 times as long (.85 m), so it took a short-legged body's steps: 2.95 steps of
.51 m a second at the shambler's 1.5 m/s walk, where a person takes about 2 of .75 m. Each take is played at that cadence over
its own, so the walking takes ran at 1.5 to 2.2 times their captured pace: 2.0 for chase-1 at the walk, 2.2 for chase-2 at the
shambler's run, 1.5 for sprint-2 at the runner's.

**The fix** (presentation only, the model's Full tier only: no rule, timing, hit volume, damage, cooldown, movement or network
value changes; an add drawn on the voxel model, on the Reduced and far tiers or of a kind not modelled, keeps the rig's own stride.
The rig of an add on the model is posed on the drawn-in footprints below. Effects placed on its leg joints (an arrow lodged in a
leg) were already off the model's 1.7-times-longer legs, and stay so):
- `NativeHordeGait.Legs`: the legs the stride is planned for, as a multiple of the rig's. The horde presentation sets it each
  pose to the model's ratio (`Rig.LegRatio`) for an add drawn on the model last frame, else 1. The Hulk keeps its own heavy
  stride and a crawl its own.
- `StepRate(kind, v, legs)`: longer legs step as the rig's do at the speed that is the same pace for them (the speed over the
  root of the legs, the Froude number), their time the root of their length slower. The cadence falls by legs^-.675 and the
  steps lengthen by legs^.675. The stance reach (`StanceMost`) and the out-of-reach re-step scale with the legs. The walk and
  run take choice is unchanged (by the authority's speed), so each kind keeps its takes.
- The rig cannot reach those footprints, so its legs are posed on a copy of each footprint drawn in fore and aft of its hip by
  the same ratio (`Squeeze`). Its sink, heel peel, landing clamp and knee rules work as before on that copy. The model's binding
  spreads the ankle back out (`NativeVisualRig.State.Stride`, from `NativeHordeGait.LegsNow`), so the model's feet land on the
  footprints. Its knee keeps the gait's own leg's bend.
- `NativeVisualRig.OnReach`: a followed foot the leg cannot reach where it stands rises onto the leg's reach over its own spot,
  rather than the straight leg falling short of it. This happens to the trailing foot lifting off behind a long run stride (up
  to 5 cm, a toe-off) and to one reaching out to land.

**Measured** (the rig's legs → the model's; world m/s; steps a second and step length from `StepRate`; the takes' pace is each
playing take's playback rate over its capture; the bloater and the crawler draw on the voxel model and are unchanged):

| kind | gait (m/s) | steps/s | step (m) | the takes' pace |
|---|---|---|---|---|
| shambler | walk (1.50) | 2.95 → 2.06 | .51 → .73 | chase-1 2.02 → 1.41, chase-2 1.67 → 1.17, chase-1-full 2.11 → 1.47 |
| shambler | run (3.40) | 3.93 → 2.74 | .87 → 1.24 | sprint-3 .98 → .68, chase-2 2.23 → 1.55 |
| runner | walk (2.60) | 3.58 → 2.49 | .73 → 1.04 | sprint-3 .89 → .62, sprint-2 1.07 → .75 |
| runner | run (4.60) | 5.11 → 3.04 | .90 → 1.51 | sprint-3 1.28 → .76, sprint-2 1.53 → .91 |
| spitter | walk (1.60) | 2.87 → 2.00 | .56 → .80 | chase-1 1.96 → 1.37, walk-6-full 1.72 → 1.20 |
| spitter | run (2.90) | 3.53 → 2.46 | .82 → 1.18 | chase-2 2.00 → 1.39, sprint-3 .88 → .62 |
| brute | walk (1.30) | 2.53 → 1.76 | .51 → .74 | walk-6-full 1.52 → 1.06, walk-4 1.68 → 1.17 |
| brute | run (2.40) | 3.13 → 2.18 | .77 → 1.10 | chase-2 1.77 → 1.24, chase-5-full 1.72 → 1.20 |
| hulk | walk, run | unchanged (its heavy stride) | | .72 to .78 |

Across the modelled kinds' walks and runs, the mean take pace goes from 1.59 to 1.09 times the capture and the fastest from
2.23 to 1.55. The sprints now play under their captured pace (.62 to .91): they were acted at 4 steps a second, faster than a
person runs at these speeds (2.5 to 3).

On the measured skeleton as the runtime binds it (`--zombie-gait`, five kinds, walk and run, up to four take pairs each):
- the model's ankles land on the footprints (0.0 cm) and a planted foot stands still (0.0 mm a frame);
- every step has a footfall, and the cadence is the planned one (within 8 %);
- the knees stay forward (21 degrees aside p95);
- the takes stay in step at the walk (each ankle fore and aft of its own hip: .78 worst on the limping walk-4, .90 mean). On the
  rig's legs this is .81 worst and .90 mean; the brute's walk-4 drops from .90 to .78 (its marks give its dragged right foot
  half a cycle).

The posture on the model's stride (`--zombie-posture`, which now runs the gait as the runtime does):
- planted knees in a stride 30 to 34 degrees mean (the Hulk's 36 and 42 as before; the runner's sprint 46, as a sprinter's at
  mid-stance, its bound 48 from 4 m/s);
- resting knees 20;
- the planted ankles on their marks.

**For Unity on the PC:** whether the walks now read at a natural pace, and whether the sprints read too slow (if they do, a
tempo for the runner on the model is a one-number change). Also check that no foot slides or pops when an add crosses between
the Full tier (model) and the Reduced tier (voxel): the stride switches with it.

## 4f. The Hulk in detail: anatomy, fists, calmer hips, the head on its neck (2026-10-30, `claude/hulk-detail`)

Owner (2026-10-30): "Improve hulk design again (I still see "clothes" inside there muscle like theyre meshed together, their
muscles look just like bubbles currently; Please work with much more detail. Also, the punch is open handed still, they need
to close their fists.) Movement still needs a bit more adjusting too, they look too "wiggly"", then mid-task: "Need to blend
the head better".

**What the captures showed (Brickfall, the local tool v232, close views at the Hulk's 1.8x):** the growth was thirteen smooth
masses summed as soft fields, so they melted into balloons; the women's vest and jacket and the model's own chest and arms
poked through it; the hands hung open (zombies are posed with no grasp); walking, the hips dropped 6 to 11 cm in a frame
several times a second; the trap and neck mounds rose over the chin, and the flesh drew bluer than the head's skin.

**The anatomy (Core/NativeZombieMutation, rewritten).**
- Each muscle its own form from its origin to its insertion on the rest joints (`Muscle`, `Mark`): a spindle or a fan slab
  thickest at its belly, its belly lifted off the line between them, mirrored for the left. 31 kinds, 62 placed: the traps
  (upper swollen round the sunk head, middle, lower), the sternocleidomastoid from under the jaw, the pecs in three fans, the
  deltoid's three heads, the lats, teres and infraspinatus, the erectors, four serratus fingers, the obliques, an eight-pack;
  the biceps, brachialis, both triceps heads, a swelling below the elbow under the brachioradialis, extensors and flexors. The
  right arm's grow by the variant's `Right`. Sizes scale with the shoulders' span.
- Each a signed distance, joined by a tight smooth minimum (`Crease`, 5.5 mm), so they meet in creases; a core (rib cage, belly,
  the waist into the trousers, the neck, the shoulder girdle, the limbs' bones) joined more softly under them. Surface nets on a
  1.05 cm grid (about 24,000 vertices, 48,000 faces).
- Colour: the skin tone warmed to what the skin texture draws (`SkinMatch`, measured on the head beside the flesh), lighter over
  the bellies, darker and redder down the creases and in hollows, mottled, stretch marks across the swollen masses, six patches
  of torn skin over raw striated muscle with a pale rim. Raised veins (17 tubes: forearms, biceps, shoulders, chest, neck), nine
  bone spurs, eight pustules (they glow).
- The women's top: no Hulk wears one (the grown flesh is the whole upper body); a woman's top colour is a torn chest wrap
  painted on the flesh (`top`), with no vein, tear or pustule under it. The decay opens where no flesh grows (the face, the legs).
- What it covers (`Growth.Covers`): per variant, copies of the worn meshes drop every triangle the flesh covers: the model's skin
  within 6 cm of it (the flesh replaces it), clothes and the head inside it, never past a wrist (the hands are the model's). On
  the male Hulk 5,705 of 28,574 worn triangles go; nothing of the skin or clothes shows through.
- The head: the neck's core ends below the jaw and the sternocleidomastoid starts there, so the face is clear of the flesh (a
  check); within 5 cm of where the flesh meets the head it is the head's tone, unmarked.
- Skinning: every vertex on four of the bones its muscles move with (the forearms on their twist bones), by its distance to each
  bone's segment, less the fifth's. Weighting each vertex by its own muscle's bones first tore the creases (a shoulder-blade
  vertex rode the forearm: shards in Unity); by distance alone the weights change smoothly (p99 .31 across an edge).
- Cost: built in parallel off the main thread from a variant's first body (30 ms in the .NET checks; longer in the Editor), the
  body drawing voxel until it is made; in a horde each mutant variant's first body is made ahead (one a frame), so a Hulk's
  arrives built.

**The fists (NativeVisualRig.Grasp.Clench).** The Hulk is posed with both hands closed and clenched past a handle's grip into a
tight fist (`Clenched`: fingers rolled into the palm, the thumb across them), .6 at rest and fully through a blow (the gait's
`Busy`). Other kinds stay open-handed.

**Calmer hips (NativeVisualRig State.Ease, `Carry`; NativeZombiePosture.HulkCarry .07 s).** The stance's rise rode each planted
leg's reach, 0.6 cm up a pose and down at once when the next foot came into reach, and the Hulk's stamp (its gait's hips drop with
the foot) showed until the rise climbed back. The Hulk's pelvis is now carried on a critically damped spring toward the stance,
under both followed feet's reach, by a margin while that reach has been falling (so it is down before a foot lands), never above a
planted foot's; stepped by the time since the body's last pose (the crowd's clock poses far adds less often). The other kinds keep
the rise (carrying them sank them into a squat).

| the Hulk on the model (60 Hz, rest units; drawn at 1.8x) | before | after |
|---|---|---|
| walk: biggest hip drop in a pose / travel / jerk p95 | 5.7 cm / 14.1 cm / 1.15 | 2.2 / 8.2 / .25 |
| run | 7.2 / 17.0 / 2.12 | 1.5 / 13.3 / .48 |
| planted knee mean in a stride (walk, run), at rest | 36, 42; 33 | 40, 44; 33 |

In Unity the walking male Hulk's fastest hip drop fell from 3.1 to 1.6 m/s (a noisier measure: the capture's shots slow the
Editor to about 20 fps).

## 4g. Zone kinds' kit: gear, tape and the fused commuter (2026-11-03, `claude/station-models`)

Owner (2026-11-03): rework the instance-unique Expedition enemies off voxels in very detailed designs, reviewing detail and
animation before done (docs/EXPEDITION-ENEMY-REWORK.md). The Central Station's four kinds are the first: variants in
`Core/NativeZombieLooks` (four each, on the bake's pieces), and each variant's kit (`Variant.Tape`, `Gear`, `Fused`, given in `Kit`).

**Gear, modelled in Blender** (`tools/gear/`, committed code; its output is local like the pack banks):
- `station_gear.py` models every piece with `gearkit.py`'s helpers (bmesh boxes, cylinders, tubes on rotation-minimizing
  frames, flat straps along a path, gridded decals projected onto curved surfaces, metaballs for flesh) and modifiers (bevel,
  solidify), in a design frame (Blender's X right, Y forward, Z up) at the bone it rides.
- `gearkit.bake` writes each prop into that bone's own frame (Unity's axes; a reflection, so the winding turns over), every
  vertex coloured (sRGB bytes) with its finish's dirt and wear baked in: creases and undersides darker, soft grime patches,
  paint chipped to its primer or metal on convex edges, worn bright edges on bare metal, raw red creases on flesh. Vertex alpha
  is the toon's outline (off on screws, lights and decals). Glowing finishes (lenses, vents, the lantern's globe, the door's
  window) are a second sub-mesh on a material with an emission colour.
- The fits are numbers in the script, measured from the local tool's `zdump` (the zombies' worn meshes and bones in root space,
  each vertex's heaviest bone): the skull in the head bone's frame (every variant wears the same head piece), the jacket's back
  and the shoulders' outline behind and over `spine_05` per build (the brute's and the women's), the sleeves round the forearms.
- Run: `blender --background --factory-startup --python tools/gear/station_gear.py -- --out unity/ChipArenaConversion/Assets/LocalAssets`
  (`--preview DIR` renders each prop with its vertex colours; `--only id,...`). It writes `ChipArenaLocal/Resources/ChipArenaPackGear.bytes`
  (docs/PACK-BANK.md's format), which `tools/pack_visual_body.py` packs into the licensed-assets zip beside the body.

| Prop | Bone | Triangles | What |
|---|---|---:|---|
| `linesman-hardhat-yellow`, `-white` | head | 10,300 | moulded shell on its suspension band, three ribs, brim with a front peak and rain lip, elastic strap, the headlamp (cracked lens, bezel, switch, cable round to the back), high-voltage triangle, the station's roundel, reflective strip |
| `linesman-pack`, `-pack-f` | spine_05 | 10,200 | case on two pads, rubber corner guards, the vent unit glowing blue through seven louvres (the weak point), a dial and three status lights, terminals in red and black boots with their cables into the sides, carry handle, hazard stripes, a high-voltage plate; the harness over the shoulders to a sternum strap and buckle, adjusters, D-rings, foot straps |
| `linesman-coil` | lowerarm_r | 3,300 | red and black jumper cables wound together, crocodile clamps bitten onto the coil |
| `conductor-badge` | head | 480 | the railway's winged wheel in brass on red enamel, on the peaked cap's crown |
| `conductor-whistle` | spine_05 | 2,900 | link chain round the neck over the lapels, the nickel whistle |
| `conductor-lantern`, `-broken` | prop | 3,300 | railway hand lamp: wire bail, chimney, vented bell, the red globe in its guard wires, the fount and its brass plate; broken, a jagged ring of dark glass |
| `undertow-weed-back`, `-arm-l`, `-arm-r` | spine_05, forearms | 5,100, 440 | kelp ribbons and stringy weed draped over the shoulders, a drowned cable with a frayed copper end; weed wound round each forearm, its tails past the wrist |
| `pileup-handrail`, `-f` | spine_05 | 1,200 | the carriage's grab pole bent across the chest, its flange still bolted, the bottom sheared |
| `pileup-door` | prop | 2,000 | stainless panels in their seal, livery stripe, the window lit by a dying tube behind cracked glass, kick plate, scratches, release handle, the inside handrail, the third commuter's arm clawing down the face |
| `pileup-flesh`, `-f` | spine_05 | 4,400 | the fused commuter's flesh over the second head's neck and arm: lumps, raw creases, veins, a broken collarbone, the suit torn round its foot |

**Runtime** (`Runtime/NativeZombieModelPresentation.cs`): `ZombieGear` hangs each worn prop on its bone as a plain mesh
renderer (no skinning: the project skins on the CPU), in the body's renderers, so it flashes, collapses with a lost part, flies
off with a severed one and settles with the corpse as the decay pits do. Carried props (the lantern, the door) are drawn by
`ZombieGearPropDraw` at the prop's matrix while the add is on the model (its glow by the lantern's flare and the window's
flicker), the voxel props beside a voxel body. The props follow the model's hands (`ZombieModelShift`: each game joint's place
on the model against the rig's), which close on them (`ZombieGrasp`); the Linesman's beam, halo and pack glow come from the
gear's lens and vents (`NativeZombieLooks.LinesmanLamp`, `LinesmanVents`).

**Tape**: the Linesman's jacket wears reflective tape drawn by the shader (`ZombieBand` in NativeVisualBodyFx.cginc): six
rings (below the chest and above the hem, round each upper arm and forearm) placed in the meshes' rest space from the rest
joints (`ZombieTapeRings`), so they fit every jacket and build and ride the cloth; silver over the piece's colour under its
grime, shining back toward the eye.

**The fused commuter** (the Pileup): `ZombieFuse` bakes, once as a body is made, its own head and face (bald, the scream's
shapes set) and its right arm posed raised and clawing (cut as a severed arm is), and carries both on `spine_05` at the right
shoulder (`ZombieFusedNeck`, `ZombieFusedShoulder`), swaying a little each pose; the gear's flesh covers the joins.

**Reviewed on the PC** (BoZo, brickfall): turnarounds of every kind, close-ups of the head and upper body, and each kind
hunting a pilot, captured from its wind-up to its recovery, its stagger and its death (gear and props follow throughout; the
Undertow sinks as its voxel did). The four kinds are live (`ZoneLive`).

## 4h. The Backrooms' entities on the model (2026-11-03, `claude/backrooms-entities`)

Owner: "Please start the backroom enemies now" (the designs of docs/EXPEDITION-ENEMY-REWORK.md section 4, on Astra's rules of
#317, section 6). The four humanoids are live; the Hound is a draft; the Tiler keeps its stand-in body.

| Kind | Variants (Core NativeZombieLooks) | Kit |
|---|---|---|
| Faceling | office workers yellowed by the damp: dress shirt or sweater vest, slacks or khakis, loafers, heels or flats | `Faceless`: the eyes, brows, lashes and teeth off; `faceling-mask-skin` (smooth skin over brow to chin, the sockets filled, the nose a soft rise), its lanyard and ID card; stands a little upright (its blank face shows) |
| Smiler | not on the model since 2026-11-03: its own apparition (4i) | |
| Drowned | heavy bathers, waterlogged pallor | `Stripes`: a vintage costume's stripes (the shader's ZombieStripe, 9 cm apart in rest space) on a tank or tube top and shorts; a rubber cap with petals (white or red) and fogged goggles |
| Lifeguard | the kind's 2.6 m, thin, faded reds, sun-bleached, bucket or sun hat, flip-flops | `lifeguard-whistle` on a red cord; `Carry`: the rescue hook (an aluminium pole with a shepherd's crook) in the right hand, held upright at its side (Core NativeZombiePosture.StaffCarry); stands upright |
| Hound (draft) | gaunt, hairless, in rags, bare feet | none yet: it runs on two legs until its gait on all fours |

New in the runtime: skin pieces (a gear prop whose id ends "-skin" draws with the head's own material and looks, every vertex
on a cheek texel, unlined); stripes; a carried prop at the right hand's weapon frame, drawn every frame; the upright stance
(Core NativeZombiePosture.Upright: the spine, chest, neck and head turned back toward the rest, eased off while a blow owns the
pose). The bake gained the pieces they wear (sweater vest, office heels, tube top, dolphin and simple shorts, flip-flops, bucket
and sun hats, bare feet).

Their moves (Runtime/NativeBackroomsKindsPresentation.cs, Core/NativeBackroomsMotion.cs):
- the authority's names onto the gait: the wake, howl, blink, whistle and hook are not blows; the Hound's pounce (contact .85
  s), the Smiler's .6 s and the Drowned's .4 s lunges and the Lifeguard's .5 s sweep play the gait's takes at those timings;
- the Faceling dormant at its wall (bowed toward it, swaying) and its wake (the head snaps round in .08 s, the neck and chest
  after it, handing back to the body after the .45 s tell); the Hound's howl (the head thrown back, the jaw open); the
  Lifeguard's whistle (the left hand to the mouth) and rescue hook (the pole raised back over the shoulder through the .6 s
  wind-up, thrust out at the pilot, hauled back through the drag); the Drowned sunk 1.25 m while submerged, rising .4 s after
  it surfaces.

Reviewed on the PC (BoZo, brickfall; `zset` sets a zone move on demand, since DEV spawns away from their maps make none):
turnarounds, head and upper-body close-ups of every kind, and strips of the Faceling's wake, the Hound's howl and the
Lifeguard's whistle and hook. Not yet seen on their own maps: the Smiler's blink (dark cells only) and the Drowned under water.

Open (the next pass): the Hound on all fours (a quadruped gallop on the model) and its pounce and pin; the Tiler's own body (a
tile crab on six legs); the Smiler's dread on the pilot it struck and the fixtures' stutter round its blink (its body is 4i); the Drowned's wake and
bubbles under water and its swim; the hook's line to the pilot through the drag; captures on Level 0 and the Poolrooms.

## 4i. The Smiler as an apparition, not a person (2026-11-03, `claude/smiler-apparition`)

Owner, with a reference image: "I think the smiler shouldn't be a player model. Keep in mind enemies dont have to always use
the player model. The smiler is usually an black ghostly apparition like this" (a billowing cloud of black smoke, two glowing
white eyes, a wide grin of yellowed teeth glowing red at the gums). Its variants and gear left the model (NativeZombieLooks,
`tools/gear/backrooms_gear.py`; the local gear bank keeps the two old props until its next rebuild, unused).

- Body (Runtime/NativeSmilerPresentation.cs, Resources/NativeSmiler.shader): one box round the cloud, raymarched. A signed
  field in the cloud's own space (a core, six lobes turning about it, a tail tapering to a wisp toward the floor and trailing its
  drift, the lunge's lobe on a neck of smoke) pushed in and out by a tileable 3D noise (Core NativeSmilerLook.Noise, 48^3, built
  off the main thread: big billows and fine curls boiling through it); a soft edge over a few centimetres with wisps outside it;
  near-black smoke lit a little by the scene and by its own face (red round the grin, white by the eyes). The march stops at the
  scene's depth (soft against walls and floor; the low tier has no depth texture and runs to the box); from inside the box (a
  pilot it lunges at) the back faces draw. Scene fog at the core's distance.
- Face: drawn in the march where the ray crosses a plane facing the camera on the cloud's near side, so it always turns to
  whoever looks at it; the smoke in front of it thins to wisps that drift across it. Two white eyes with halos (lids slanting
  in as they narrow), a crescent grin (two rows of uneven, partly pointed teeth, a few missing, yellowed and reddening toward
  the gums and corners, on glowing gums; parting on the lunge), the lips' red glow; bloom takes the eyes and teeth.
- Moves (Core NativeSmilerLook, on the authority's zone frame): at rest it floats and boils, its grin breathing, its eyes
  stuttering now and then like the dying fixtures; the blink (.5 s): the eyes shut, the smoke thins and spreads to nothing by the
  move, the grin last, then it gathers again (eyes, then grin) where the authority put it; the lunge (.6 s): drawn back and
  swollen, the grin widening and parting, the eyes narrowing and flaring, then a lobe thrown 1.55 m ahead with the face riding
  it and hauled back; a stagger's cancel eases back instead of snapping; spawn: gathers from nothing over .9 s.
- Hits tear black smoke off it along the shot (soft puffs in the effects pass) and roil it, the face stuttering; no wounds, no
  blood, no severed limbs. Death (2.2 s): the smoke thins, spreads and rises, a burst torn off, the eyes out first, the grin
  lingering and widening as it fades; no fall, no corpse, no gore. No footfalls and no zombie voice (its lunge's strike plays the
  rush, its blink is silent). Its rig is still posed on the gait (hits, arrows and effects find its body there), never drawn.

Reviewed on the PC (BoZo, brickfall, a DEV Smiler; `zset` for its moves): turnarounds (the face turns to every camera), the
lunge, blink, a hit and the death, frame by frame. Frame time with it 2 m ahead: mean 11.2 ms against 10.8 ms without, in the
Editor with other effects on screen (indicative, measured before the cloud was enlarged; the GPU cost on low-end hardware is
not measured). Not run: Level 0's dark
cells (lighting, the real blink move), a Windows build.

## 5. Not done yet (open)

- A Windows player measurement of the crowd (the Editor's numbers above are the only ones).
- The zone kinds' gear is dense for a crowd (the hard hat and the pack about 10,000 triangles each, the Linesman's kit 24,000;
  rigid renderers, so it costs drawing, not skinning); a lighter cut for the farther adds is open. The weeds, cables and clamps
  are rigid on their bones (they swing with the limb, not with gravity). The Pileup women wear the brute's flesh fit
  (`pileup-flesh-f` is the same piece); the fused head's face is fixed (it does not blink or move its jaw).
- Wounds fitted to the model's surface during life (a living add's bullet marks; the decay openings are fixed per variant).
- Arrows lodging on the model's surface, not the voxel joints.
- A reduced model for the Reduced tier, replacing the voxel style at distance.
- The owner's per-type motion notes (shambler drag, runner intent, spitter guard, brute and hulk weight): the captures show the
  gait's motion on the model with blows on time; any change that needs a rule goes to Astra as a measured mismatch (none known).
- The ZOMBIE PRO exports still carry the retarget's spine softening made for the voxel torso (`scripts/mocap/mixamo-retarget.mjs`),
  which a re-export for the model could drop (it needs the private pack).
- The Hulk's growth has no own wounds (hits on it show the body's) and its voxel tier is the old Hulk; outside a horde (DEV
  monsters, summons) a variant's first Hulk draws voxel until its growth is built. The other kinds still ride their legs' reach
  (section 4f): their hips travel 15 to 28 cm a stride on the model with drops of 1.4 to 6 cm in a pose (measured offline).
- The Hulk's skin tone against its head is matched by a constant (`SkinMatch`) measured under Brickfall's moonlight; other maps'
  light may show a difference.
- A head burst (`HeadGib`) still leaves the gore's own stump and fountain on the model's collapsed neck.
- The sprints play at .62 to .91 of their pace on the model's stride (4e); a faster tempo for the runner on the model is open
  until the owner sees it.
- The run sets still include walking chases (chase-2, chase-1, chase-5) played fast. Their feet now lift on their own arcs, but
  their trunk keeps the walk's rhythm (section 4d). A run set of the sprints alone would change the kinds' character: the owner's
  call.

## 6. Checks

`CoreTests --zombie-models` covers the table, the pick, the shape names, the shader's zombie look, the throat sac's geometry, the
decay sites (placed on a mannequin's joints, left and right, square to their limbs) and the runtime's wiring (openings, pits,
bones, model gore parts).
`--zombie-gore` admits the new presentation file among the gore's readers.
`--zombie-posture` measures the model fit (4b, 4c: the planted knee in a stride, a busy pose's crouch kept, the rise holding in
flight) and pins its wiring; it runs `--zombie-gait` too (4d: the takes' warps, offsets, stances and swing arcs; the model's
ankles on the gait's, the feet's speed, the knees, the feet alternating, the takes in step; the follow flag and the knee clamp;
4e: the stride on the model's legs, its cadence and footfalls, planted feet still, the takes' pace and the runtime's wiring; 4f:
the Hulk's carried hips against the old rise). `--zombie-models` also builds the Hulk's growth on the male Hulk's rest joints as
Unity measured them (4f: every muscle placed and mirrored, the eight-pack and the pecs standing off their creases, four weights
summing to 1 and smooth across edges, winding, the face clear, the traps' crest, what it covers, the women's wrap, determinism,
its build time, the fists' clench).

None of these is a Unity render. Visual quality, foot contact on the model, crowd cost and multiplayer views are verified only in
Unity on the owner's PC.
