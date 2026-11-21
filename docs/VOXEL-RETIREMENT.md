# Voxel retirement: the plan (final, 2026-11-18; approved by the owner with every default)

**Status:** final, waiting on the owner's decisions in section 8 (O1-O5). Nothing is built yet. Only the costume itself (V2) waits on O1; the groundwork (V0, V1) can start now.

**The owner (2026-11-18):** "I want to essentially remove voxel entirely BUT keep its data incase I use it for a map or game mode. The voxel player model + weapons can be selected as skins or costumes instead. All the new things ive been making, I want to remove the voxel dependency and move forward with a more detailed framing (since we're essentially going for much higher quality designs and things now)".

## Owner brief (one screen)
- **Voxel today is three layers:**
  - the voxel pilot, drawn by the Classic setting, by builds without the licensed bake, and for the Siren;
  - the old 22-joint voxel skeleton, which still poses what the model's animation packs do not cover, and every zombie;
  - hidden voxel boxes that still measure floors, ragdolls, wounds, cuts and gibs.
  - Newer work leans on it too: the far zombie crowd, cube-built props (the Bombadier's, the weapon techniques'), some chip pieces, and the pod eject now being built.
- **The end state:**
  - everything drawn is built, timed and socketed on the model;
  - voxel survives as a choice: a costume everyone sees, the Prototype weapons, Voxel breakup;
  - voxel survives as kept data: every voxel model, animation and map, byte-identical, tested and loadable for a voxel map or mode, and still the body of builds without the bake.
- **No rule moves:** hits, timing, damage, cooldowns, movement and networking stay as they are.
- **Releases:** nine stages, each a playable test-channel release, beside the expedition overhaul (section 4).
  - The costume is Stage 1.
  - The minimum that counts as done is the costume, no voxel on model pilots, and the zombie crowd on models (Stage 4).
- **Your decisions (section 8):**
  - O1, how the costume looks. Recommended: voxel blocks fitted to the model's frame. The alternative is today's exact voxel pilot on the old skeleton.
  - O2, who gets it. Recommended: everyone, free.
  - O3, the Classic setting. Recommended: removed only after a low-end check.
  - O4, builds without the bake. Recommended: they keep drawing voxel, and the builder refuses to publish them.
  - O5, the Siren. Recommended: voxel until her own model, at M7.
- **Your actions:** review the costume sheet; authorize Astra's account Worker deploy; name a low-end test PC or accept a constrained run; forward section 7 to Astra whole.
- **The approved overhaul plan changes in eleven places** (section 4.2). The main ones:
  - no voxel fallbacks for the new deployment scenes or the survivors;
  - the pod eject moves onto the model;
  - the crowd's performance work lands before the district creatures.

**Inputs:**
- six read-only sweeps of shared `claude/sleepy-ritchie-g8ruhv` at `4dc8c86f` (pilots and weapons, enemies, effects after death, world, engine and data, a whole-project text sweep), each with an independent verifier (appendix A);
- two critiques of the draft, one for feasibility and performance and one for completeness and owner intent. Their claims were checked in the code; every finding is applied or answered (appendix B);
- `docs/EXPEDITION-OVERHAUL.md`, approved 2026-11-18, with release 1 (M1, the drop) in progress;
- the in-flight M1 branches: W0 groundwork `ce27c994`, W2 `1ca6b394`, W3 `048adc30`, W4 `d0668565`.

**Base:**
- Line numbers are at `4dc8c86f`.
- Shared is now `b13940ce`, four commits on: the greybox size decision (docs) and the Thunderheart Carillon arena.
  - None of the four adds a voxel read: every token count below is identical at both commits.
  - The overhaul doc only gained 11 lines at its end, so its line numbers hold.
- Nothing was built, run or captured for this plan. Every figure comes from the code, the docs or measurements already in the DEV_LOG. Token counts are occurrences at `b13940ce`.

**Paths:**
- `Core/`, `Runtime/`, `Editor/` and `Resources/` are under `unity/ChipArenaConversion/Assets/ChipArena/`.
- The data files (`SourceModels.json`, `SourceMotion.json`, `SceneMotion.json`, `WoundParts.json`, `MapLooks.json` and the rest) are under `unity/ChipArenaConversion/Assets/Conversion/Resources/`.
- `Tests/` and `tools/` are under `unity/ChipArenaConversion/`; `docs/`, `src/` and `scripts/` are at the repo root.
- The licensed bake is `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaVisualBody.prefab`. It is git-ignored and ships only in the Drive zip.
- Abbreviations:
  - VBP: `Runtime/NativeVisualBodyPresentation.cs`;
  - SP: `Runtime/NativeSourcePresentation.cs`;
  - HKP: `Runtime/NativeHordeKindPresentation.cs`;
  - ZMP: `Runtime/NativeZombieModelPresentation.cs`;
  - DP: `Runtime/NativeDeathPresentation.cs`;
  - KEP: `Runtime/NativeKillEffectPresentation.cs`;
  - VR: `Core/NativeVisualRig.cs`;
  - OVH: `docs/EXPEDITION-OVERHAUL.md`.

**Owners:**
- **Claude:** presentation: art, models, animation, VFX, UI, and the client cosmetics contract files.
- **Astra:** rules, simulation, accounts, network, saves, unlocks, and build and publish tooling.
- **The owner** relays Astra's part (section 7).

**Effort scale:**
- **S:** one small branch.
- **M:** one package.
- **L:** several branches.
- **XL:** set by set, across milestones.

---

## 1. Inventory (deduplicated)

Each row merges the sweeps' findings with the verifiers' and critics' corrections. "Kept" is what survives as voxel-style data or code. The replacement names its package and stage (sections 3 and 4).

### 1A. Pilots, switches and cosmetics

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P1 | Settings · Player model "Classic voxel": one local, global switch that draws every pilot, summon and zombie (and THE 6:47) voxel on this PC. Never sent to other players. It is also the only all-voxel crowd, the cheapest crowd there is (ZMP:84) | `Runtime/NativeSettings.cs:19-21,66`; `Runtime/ConversionGame.cs:10,223`; VBP:81-82; 25 uses in 12 Runtime files | Fallback (asked for "just in case", DEV_LOG.md:7907) | Per O3: removed only in the same release as the costume, and only if V0's low-end run passes; otherwise kept, relabelled "Low-end bodies (this PC)". A DEV-only override and `--ca-voxel-bodies` stay. Once the row is gone, a saved "Classic" reads as New (V1, Stage 1) | Claude | M | Exact-line pins (VisualRigChecks:1806-1823 and others) | Model against voxel at 8 pilots is unmeasured; V0 measures it | The prefs parse; the voxel path behind the override |
| P2 | The model path sits inside the voxel path. About 20 scattered `Viewer/sourceModels/ClassicBodies/siren/VisualBodiesOn` gates; 8 model paths return when the voxel bank is null; model echoes need the voxel afterimage model | VBP:199,302,478,524,986,1121,1147,1366,1497,1548,1718,1855; `NativeWeaponFxPresentation.cs:270,296`; `NativeDevFighterEffects.cs:253-259`; SP:17 | Hidden gate | One predicate, `ModelBody(a)`, plus the joint-frame resolver (M6). No model path needs a voxel body built. The bank stays the models' instanced drawer until V12 splits it (V1, Stage 1) | Claude | S-M | A missed site silently draws voxel, or nothing | None | The legacy branches, behind the predicate |
| P3 | Builds without the licensed bake draw only voxel pilots: CI, the cloud builder, public clones, and a builder run without the Drive zip | VBP:7-12,142-157; `.gitignore:14-17`; `tools/install_local_assets.py:17-22`; `Runtime/NativeBootLoad.cs:64-65` | Fallback | The voxel style stays their declared body (O4). The builder refuses a test-channel build without the bake, through an Editor check started with `-requireVisualBody` (VA2, Astra's) | Both | S | Today a missing zip ships voxel pilots with no error | None | The legacy rig path and its twins |
| P4 | Voxel switches: `--ca-voxel-bodies`, `--ca-voxel-zombies`, the local tool's `zvoxel=1` and the DEV compare's toggle. (`--ca-zombie-drafts`, `--ca-no-pack-banks` and `--ca-no-stride` are not voxel switches) | VBP:72,148; ZMP:33,83; `NativeZombieComparePresentation.cs:8-17` | Tooling | Kept as DEV switches into the voxel style | Claude | S | Low | None | All |
| P5 | Voxel body skins Standard (armoured), Prototype (the original) and DEV Sheldon Clothes, picked by the skin map's `body` entry. They draw only where voxel draws; the Locker hides the row on the model | `Core/NativeBodySkins.cs`; `Core/NativeBodyModels.cs:238`; `Runtime/NativeBodySkinModels.cs`; `NativeLockerSkinsUI.cs:15-101`; `NativeLockerUI.cs:80`; SourceModels `pilot` (48 parts on 21 joints) | Cosmetic and data, already per seat | They become the costume's variant (2.3). The existing `body:sheldon` gate already holds the DEV variant on the host, at login and on save, so nothing new is gated | Claude | S | Old saves hold `body:prototype`, so `body` alone must never mean "wear voxel" | 2 skins × 3 tiers are sculpted at boot; build them on first use (V12) | All |
| P6 | The voxel costume (to build, per O1) | VisualDress VBP:799-815; the Hot Dog (`Runtime/NativeHotDogPresentation.cs`, `Core/NativeHotDog.cs`); `Core/NativeSheldonHood.cs`; VisualJointsOf and VisualJointFrame VBP:283-336 | Target | V2 (Stage 1). Under O1(a), blocks fitted to the model's frame. Under O1(b), today's voxel pilot per seat on the rig path (2.3) | Claude (+VA1) | L under (a); S-M under (b), plus lasting upkeep | The model is 1.79 m, with legs 1.71× the voxel's and much narrower shoulders; the owner's taste; worn attachments (2.3) | Under (a), one 3-10k-triangle skinned mesh per costumed pilot, outfit hidden, no cloth | SourceModels `pilot` and `hat-*`; NativeBodyModels |
| P7 | Prototype Series voxel weapons: per weapon, already on the wire. The bow's Prototype is a code sculpt with its own geometry hash. Katana, Fangs, Glaive, Top Dog and Bombadier have no Prototype | `Core/NativeWeaponSkins.cs:9-41,156-176`; `NativeVisualWeaponPresentation.cs:57-66`; `Core/NativeWeaponModels.Bow.cs:288-309`; `NativeWeaponSkinModels.cs:49-50` | Cosmetic and data; already drawn at the model's hand | Keep, with a Locker "voxel set" that remembers and restores the picks it replaces. The five without a Prototype keep Standard. Voxel versions of them would need new sculpts and a skin map past its 320-character cap (309 used today, about 396 with five more entries), which is Astra's frame bound (O2) | Claude | S | Voxel-size guns read large in a life-size hand (accepted 2026-10-29) | None | All, the bow's sculpt and hash included |
| P8 | Weapon sockets come from the voxel weapon frame (Standard sculpts, chip modules, the muzzle, enchants). The new Standard designs (Katana, Fangs, Glaive, TNT) are sculpted in that frame too. NativeKillBoxes' hand sockets, "the voxel fist's grip point", feed the model adapter | `Core/NativeWeaponModels.cs:7-14`; `NativeWeaponDressing.cs:6-10`; `NativeKillBoxes.Generated.cs:314`; VR:310-356,745-756 | Source of truth for sockets | Socket tables on each life-size model, in the hand's frame. The frame stays a convention for code sculpts (V10, Stage 3) | Claude | L | Tracer convergence, trails, sights, the rifle's top-run modules | None | The Prototype's sockets; NativeKillBoxes |
| P9 | Bodies outside the arena hold voxel weapons: the deploy cast's off-hand is always the Prototype, and the cast and the rooftop queue Prototype ids. Pack weapons (daggers, shield, staffs, bow) draw only on arena bodies. The DEV Greatsword shows the voxel greatsword in menus | `NativeDeployCast.cs:42,77,95,128`; `NativeRooftopPresentation.cs:410,417,466`; `NativePackWeaponPresentation.cs:6-17`; `NativeWeaponSkinModels.cs:58-59` | A leak, visible in W3's Deployment Station | Queue the weapon through the seat's skin; pack weapons on every model body; DEV specs mapped through `NativeDevMotions.Base`. It lands after W3 merges, on its own branch, or through W3's session, because a branch has one writer (V0) | Claude | S | DeployModelChecks, RooftopChecks, VisualRigChecks:1748 | Pooled pack weapons per body | Prototype ids for Prototype picks |
| P10 | First person draws voxel arms under Classic, without the bake, and for the Siren (a raised scope draws neither). The view rig is the source the model's forearms bind to | `NativeViewPresentation.cs:60-106`; VBP:966-1016 | Fallback and source | The costume's forearms on the view body (V2); first person on the model (V9, Stage 7) | Claude | M | ADS alignment; the Siren's ink arms | None | NativeFirstPersonPose |
| P11 | Hats are measured on the voxel head (scaled .55 about VoxelCrown; the Auditor Halo by fit). A crushed head throws the voxel hat at voxel size. The Sheldon Head on the Hot Dog shows the voxel hood | `Core/NativeHatLooks.cs:30-67`; `Core/NativeModelHatMeshes.cs`; KEP:27,164,186,348-352; `NativeSheldonHoodPresentation.cs:31-56` | Cosmetic and data | A model Halo; the model hat flies on a crush; a hood made for the Hot Dog; the costume wears `hat-*` at scale 1 (V3, Stages 1-2; V2) | Claude | S | ModelHatChecks, HatLookChecks | None | `hat-*`, NativeSheldonHead, the fit constants |
| P12 | Voxel twins of the body effects: spawn-in, ghosts, afterimages, the revive hologram, chips, armour halos, team bands, parry flash, the Katana's saya. One leftover shows on model pilots: live ghosts hold voxel weapons | SP:19,41,54,71,92; VBP:126-136,1848-1856; `NativeBladePresentation.cs:16,54`; `NativeArmorLights.cs:45-62` | Duplicates, plus one leftover | Ghosts snapshot the drawn weapon (V3, Stage 1); the twins draw only in the voxel style | Claude | M | Echo, BodyFx, SpawnIn and ArmorLight pins | Neutral | The twins |
| P13 | Status markers are cubes at voxel heights on every pilot: slow cubes, shackles, the Orbital Mark at y .62 | `Core/NativeStatusVisuals.cs:11-12,65-84`; `NativeStatusPresentation.cs:36,82` | Voxel VFX on model pilots (owner 2026-10-29: "NO VOXEL appearances") | VFX on the model's bones, through the resolver (V3, Stage 1) | Claude | S | StatusVisualChecks | None | The cube style |
| P14 | The DEV Cyber Lord has no model motion set and is posed from about 95 KB of rig tracks (164 rig references, 39 MeleeRig uses). Its shimmer sits on voxel box faces; its 89 cube pieces are its data-cube style | `Core/NativeVisualMotion.cs:290-294`; `Core/CyberLord*.cs`; `Core/NativeCyberLordLook.cs:236-244` | Source of truth | A model set (V9, Stage 7), and the shimmer on the model's skin (V3, Stage 2). Its data cubes stay as the class's style unless the owner objects (8.2) | Claude | M-L | Cyber Lord pins | None | The rig motions |
| P15 | The Siren (a pilot-seat boss and DEV class) has no model path, behind about ten gates. Her Last Laugh victim and her dissipate death are voxel | SP:23; VBP:199,478,524,986,1121,1548; `NativeSirenLook*`, `NativeSirenDrop*`; SourceModels `siren` | The class's look | Her own model on her own joints, through the boss-bank pipeline (O5; V11, Stage 8) | Claude | XL | Her smile, claw, crush and intro are built on voxel parts; the siren parity pins | One body | All her data |
| P16 | BIG HEADS and the prototype lab's animation Viewer keep voxel branches | `NativeMutatorPresentation.cs:14,75-78`; `Runtime/ArenaGame.cs:44-47,91-97`; `Runtime/ArenaPresentation.cs:41,229-247` | Fallback; tooling | Model-only big heads; the Viewer stays DEV tooling | Claude | S | ModelPoseChecks:410-411 | None | CapturedPose (the voxel style's draw) |

### 1B. Motion and the rig

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| M1 | The 22-joint CapturedRig (the Electron rig-v2 voxel proportions) poses whatever the model's motion bank does not cover: specials, uncovered kit moves (the ranged ability gestures and gun kits among them), knockdowns, juggles, entrances, the lobby, portrait and deploy poses, W3's eject (M12), the draft, first person, and every zombie. (Pilots' idles, locomotion, combos, blocks, rolls, jumps, aiming, casts and hit reactions already play the pack's clips on the model) | `Core/CapturedRig.cs:7-89`; `Core/NativeVisualMotion.cs:5-11,290-294`; `Runtime/ArenaPresentation.cs:155-156,229-236`; `Core/RangedAbilityPose.cs`; `Core/NativeGunKitMotion.cs` | Source of truth (presentation) | Model-native, set by set (V4 for the eject, V5, V8, V9), with a 22-joint pose derived from the drawn model for cosmetic consumers of joint ids (2.5) | Claude (keeping Astra's contract) | XL | Hit timing, parity pins, the match idles | One posing pass instead of two | CapturedRig, CapturedClips, SourceMotion, SceneMotion, the rig tracks |
| M2 | Pack retargets aim at voxel proportions and carry voxel-only edits: legs .5, arms .52, shoulders ±.345, IK arms, hilts kept off the voxel head and torso, spine and neck softened, and four more edits at run time | `scripts/mocap/mixamo-retarget.mjs:5-185`; `tools/export_pack_*.mjs`; `Core/NativePack*.Generated.cs` (11 tables, about 930 KB); `Core/NativePackReact.cs:37-241` | Source data the model inherits wherever it copies the rig | A new model-native retarget path: `tools/motion` (as for NOCTIEL), or the bake's humanoid route | Claude | L-XL | Hit windows, guest sync, the 480 MB body cap; scripts/mocap is shared with Electron | Neutral | scripts/mocap and every generated table, byte-identical |
| M3 | The model adapter works in voxel body space: GameChestHalf .265, the waist and hip halves and depths, GameHand, ArmSpread, the lap hands, the "voxel fist" wrist frame | VR:21-24,314,353,478,756,998,1037-1161 | Adapter constants | Retire each with the last state that copies the rig (V9) | Claude | M | Free hands sink into the hips or chest | None | Kept while any state copies the rig |
| M4 | The pose floor is the voxel boxes: Pose.Lowest over the 48 boxes lifts every clip sample, roll and zombie death to −.865 | `Core/CapturedRig.cs:102-138,203`; `Core/NativeSpecialPose.cs:133`; `Core/NativeHordeMotion.cs:1184`; `Core/NativeStationMotion.cs:307,328` | Source of truth | Floor contact from the model's soles (V16, Stage 3) | Claude | M | Poses float or sink | Neutral | The boxes and Lowest() |
| M5 | Voxel boxes are the clearance and contact proxies: intro arms, pack entrances, live blade clearance, the model's arm relax, ragdoll colliders, arrows, the Cyber Lord's look, and voxel zombies built from pilot blocks | `Core/NativeIntroMotion.cs:256-330`; `Core/NativeEntrancePack.cs:59-254`; `Core/MeleeClearance.cs:6-24`; VR:1059-1063; DP:65; `Core/NativeArrowGib.cs:23-108` | Source of truth | One proxy table per bake, outfit and costume variant: per-bone capsules from the skinned mesh (V16, Stage 3). It is the shared base for D1, D10, M10, V5 and the costume | Claude | L | Poses visibly change | Neutral | The boxes |
| M6 | Effect anchors read the rig's voxel-proportioned joints: 163 `rig.Bones[` reads in 59 Runtime files, and more under other names (`viewRig.Bones[` 13, `introRig`, `draftRig`, `hordeQueueRig`, `Rigs[k]`, `Player.Bones[`), against 9 uses of the model-joint helper VisualJointFrame | Runtime/*; VBP:331-335 | Anchor source | A per-actor joint-frame resolver: the model's joint when it is drawn, otherwise the rig's (V1, Stage 1). New reads go only through it | Claude | M-L | Effects float off bodies | One lookup per anchor | The rig, for the voxel style |
| M7 | Lobby, portrait and deploy poses are a port of Electron's lobby-poses.ts on the rig. The deploy cast samples them without the model fit | `Core/NativeLobbyPoses.cs:13-46,170-190`; `NativeDeployCast.cs:80-85`; `Tests/lobby-pose-parity.json` | Cosmetic (the only emote-like pick) | Re-author the existing keys on the model's motion bank (V4, Stage 2; Claude only). A new key needs the Worker and the host gate (Astra) | Claude (+Astra for new keys) | M | LobbyPoseChecks; the match idles stay as they are | None | NativeLobbyPoses and its fixture |
| M8 | Entrances: the intro rig with voxel clearances; baked `stage-*` scenes with voxel particle pools; Grid Rez builds the pilot voxel by voxel; the reveal forms the voxel body first; four DEV voxel stages; 48 cube pieces in the procedural, stage, Sheldon and DEV intros | `Runtime/NativeIntros.cs:49-245`; `Core/NativeIntroRules.cs:89-134`; `Core/NativeEntrancePack.cs`; `Core/NativeEntranceProcedural.cs:279-337`; `Core/NativeBodyReveal.cs`; `Core/NativeBladeIntros.cs:9-21` | Source of truth and cosmetic | Tracks played whole on the model (as the Katana's and the Fangs' are); model fit volumes; the particles as VFX; Grid Rez and the reveal in the model's shader (V5, Stage 4) | Claude | L | Intro, pack-intro and director pins; the 4.8 s rule unchanged | None | SceneMotion, SourceMotion, `stage-*` |
| M9 | The draft's stage pose is on the 22 joints and framed from CapturedRig.Boxes. (The plant pulse on the model already exists) | `Core/NativeDraftRules.cs:20-28`; `Core/NativeDraftStage.cs:133-142` | Source of truth | A model stage pose, framed from the model's head and hands (V4, Stage 2) | Claude | S | DraftStageChecks:43, DraftPlantChecks | None | The pose, for the voxel style |
| M10 | The part a blade takes is chosen on the voxel rig's rest proportions: the head from 1.22 m, an arm from .98 m | `Core/NativeMeleeSlice.cs:65-80,128,202-206`; `Core/NativeKillCause.cs:19-40` | Source of truth for model gore and the killcam's text | A per-outfit rest table measured from the bake (V16), still a pure function of replicated data. A drawn frame never chooses a part (V6, Stage 4) | Claude | S-M | MeleeBisectChecks, BladeChecks; host, guest and killcam must agree | None | The rig's table |
| M11 | The legacy rig path is already frozen for content added since the model. The Katana's and Fangs' Frank sets, the DEV Katana's and the DEV Greatsword's have no rig copies (no `*.Generated.cs` table holds them), so the voxel style shows the rig's generic moves for those classes | `Core/NativeVisualMotion.cs:120-146`; `Core/*.Generated.cs`; DEV_LOG.md:7911 | A gap in the kept style | Accept it as frozen while the voxel style serves builds without the bake, DEV and future maps. Bake derived 22-joint tracks offline, the way NativePack*.Generated is built, only when a voxel map or mode (VA5) or O1(b) needs them | Claude | S now; M per pack later | A voxel map would show generic moves for those classes until then | None | All rig data |
| M12 | W3's eject (M1, in flight) authors its crouch, fall and seat on the rig. It builds CapturedRig poses through MeleeRig (the hips' compression, spine and neck bends, harness grips in chest space through `Rig.Hand`) and samples the pack Jump take through `CapturedRig.Sample`. The cast plays "the same game-rig tracks on every body" | `Core/NativeDeployEject.cs:209-271` and `Runtime/NativeDeployCast.cs` on `claude/exp-w3-eject` at `048adc30` | Source of truth (new) | V4 (Stage 2): the crouch and the fall from the model's own Jump and Fall clips; the seat as a model pose, with the model's hand IK on the harness bar in the pod's frame. The rig versions stay for the voxel style | Claude | M | W3's checks and its eject timing; W3's session owns the branch until it merges | None | The rig versions |

### 1C. Menus and UI

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| U1 | Portraits: a whole voxel renderer, and model portraits framed "exactly as the voxel portrait frames it" | `Runtime/NativePortraits.cs:8-60,201`; `NativeVisualPortraitPresentation.cs:5-21,41,113,170-201` | Fallback and framing source | Model portraits for every pilot, costume included, framed from the model's head and bounds (V4) | Claude | M | LobbyPoseChecks parity | Same (one camera, 12 slots at most) | NativePortraits, for the voxel style |
| U2 | The rooftop pilot, the death broadcast's hologram and the draft pilot fall back to voxel | `NativeRooftopPresentation.cs:402-470`; `NativeDeathBroadcastPresentation.cs:21,78-86`; `NativeDraftPresentation.cs:107,148-155` | Fallback | The body predicate; a costumed pilot's bust is the costume (V4) | Claude | S | RooftopChecks; VisualRigChecks:398-400 | None | The voxel branches; NativeBroadcastEdges |
| U3 | Locker art in the voxel silhouette: body thumbnails, hat cards over a voxel head, every kill emblem's figure, the backdrop cards' bust | `NativeLockerSkinsUI.cs:156-195`; `NativeLockerUI.cs:411-456,534`; `NativePortraitBackUI.cs:8,339-352` | UI art | Model silhouettes (LkModelThumb) within the scroll-clip rule. The voxel figure stays only on the costume card and the Voxel breakup emblem (V4) | Claude | S-M | LockerChecks, PortraitBackChecks | None | The figure art |
| U4 | The deploy cast's Classic branch, and the overhaul's planned voxel fallbacks (W3's cast, W5's survivors) | `NativeDeployCast.cs:13-17,85-131`; OVH:505,608,1068,1167 | Planned fallback | A costume-aware cast whose voxel branch is chosen by the body predicate, not by Classic, because it is also the draw without the bake (V4). Survivors on the model only, not drawn without the bake | Claude | S | DeployPresentationChecks, DeployModelChecks | None | The voxel branch, for the voxel style |
| U5 | The lobby chair is a box prop under the model portraits | `Core/NativePortraitRules.cs:81-83`; `NativeVisualPortraitPresentation.cs:157-160` | Box prop | A modelled chair (V3, Stage 1) | Claude | S | Low | None | The box chair |
| U6 | Player-facing text that names voxel: "Classic voxel", "Voxel breakup", "Shatters into tumbling voxel cubes", Grid Rez's "Built voxel by voxel", "The original blocky build" | `NativeSettings.cs:66`; `NativeProfileUI.cs:10,21`; `Core/NativeLocker.cs:59,119`; `Core/NativeBodySkins.cs:24`; `Core/NativeWeaponSkins.cs:42` | Text | Relabel for the costume; keep every id (V2) | Claude | S | Renaming an id breaks saves and the Worker | None | Every id |
| U7 | The voxel CA launcher icon, which is also the Windows player's icon | `Assets/ChipArena/Branding/CAVoxelIcon.png`; `Editor/PrototypeBuild.cs:16,90,106` | Branding | Kept unless the owner asks for a new mark (8.2) | Both | S | PrototypeBuild throws if it is missing | None | The icon |

### 1D. After death

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| D1 | The cosmetic ragdoll is the rig's 11 parts with voxel box colliders. Only whole corpses are refit to the model; halves, pinned smashes, the Siren's drop and revive rebuilds are not | DP:15,59-67,104-112; `NativeCorpseFitPresentation.cs:6-79`; `NativeCollapsePresentation.cs:17-67`; `NativeInteractionPresentation.cs:72-74` | Source of truth for corpse physics | A ragdoll per bake, outfit and costume variant from V16's capsules, built directly at death, halves included. The joint ids stay as the mapping (V6, Stage 4) | Claude | L | Stability on the Hot Dog and the robe; every corpse feature keyed by rig parts; CorpseChecks, CollapseChecks | Small saving (no refit) | Today's NativeCorpse build and the WoundParts proxies |
| D2 | Every split cuts the voxel body first: Plan on the main thread, Split on a worker, the voxel halves uploaded on the main thread. That split sets each half's bodies, colliders, stand-in and fountains. Without the voxel bank there is no split | `NativeBisectPresentation.cs:45-103`; `NativeBisectCorpse.cs:33-127`; `NativeVisualCutPresentation.cs:282-285`; `Core/NativeBisect.cs:271-288` | Source of truth | Plan the halves from the model's own cut (CutJob already skins the model on a worker). Split runs only for voxel-style bodies (V6, Stage 4) | Claude | M | The SplitReady chain, sealed caps, halves never refit; BisectChecks, MeleeBisectChecks | Saves up to 8 ms and 5.6 MB per through-cut, plus main-thread work | NativeBisect, NativeBisectModels, the Saw Split preview |
| D3 | The kill wound is cut from the voxel rig's boxes. Stump caps sized to the voxel boxes draw over the model's own sever caps; crush and smash fragments take voxel sizes, places and voxel skin colours; the fountains anchor on the boxes | KEP:77-199,337-354; `Runtime/NativeKillModels.cs:97-130`; `Core/NativeKillEffectRules.cs:61-83`; `Core/NativeKillBoxes.Generated.cs` | Source of truth for model gibs | From the model: the crushed part's skinned volume, the outfit's colours and skin tone, the model's sever caps, fountains at its cap centres. Seeded as today (V6, Stage 3) | Claude | M | The 280-piece budget; host, guest and killcam determinism; kill-effect-parity.json | Neutral | NativeKillBoxes, NativeKillCaps, NativeKillDebris |
| D4 | Wounds are anchored on the voxel WoundParts boxes (WoundMark, then a 12 cm ray to the model's skin). The voxel carve runs for every wounded model pilot and at a revive's start, because only it fills the mark cache. Bruise chips and burn embers are cubes | SP:23,43; `NativeWoundGeometry.cs:29-63,163-171`; `NativeWoundPresentation.cs:10-26`; `NativeVisualWoundPresentation.cs:104-121`; `NativeReviveHoloPresentation.cs:55` | Source of truth; a wasted main-thread carve (1-8 ms per joint per new wound set); cubes | Place from the wound record (its capsule-local point, normal and direction) by a ray on the model's skin, with no carve for model bodies (V6, Stage 3). Chips become panel shards in the outfit's colour and embers become streaks (V3, Stage 2) | Claude | M | The on-hold "floating blood" report (fold it in); thin limbs; WoundSkinChecks:156-157 pins six WoundedPilot callers | Removes the recurring carves | NativeWoundCarve, NativeWoundSkin, WoundParts.json |
| D5 | The voxel corpse path is chosen per viewer (Classic, no bake, the Siren, failed model halves, the Siren's drop), not by the dead pilot | DP:336-388; VBP:1121 | Fallback | Gate on the dead pilot's seat (`seat.look` already replicates) and the bake; carve only inside the voxel branch (V6, Stage 3) | Claude | M | Soul Drift and Frostbreak branch order | Removes a death-time carve | The whole voxel corpse branch |
| D6 | Recorded death scenes: 11 `death-*` clips on the rig, with voxel props. The victim's bones move rigidly per rig joint, the held weapon is always voxel, and the Raging Demon's victim screen is voxel | `Runtime/NativeSceneMotion.cs:43-66`; VBP:1580-1600; SceneMotion.json (which also holds the 20 entrance stages) | Source of truth; voxel props | The drawn weapon now (V6, Stage 3); the victim's tracks through the model binding and the props remodelled per effect (V14) | Claude | S + XL | Scene timing, the cinematic camera, owned effects | Model props cost more than cubes | SceneMotion.json, NativeSceneMotion, the voxel victim path |
| D7 | The Siren's Last Laugh victim is always voxel, model pilots included | `NativeSirenDropPresentation.cs:22-99` | A leak | A model corpse on the drop body, posed from the clip's victim tracks, with the crushed head as a CrushPart (V6, Stage 3) | Claude | M | Release velocities; kept PvE bodies and their revive | Neutral | The voxel victim |
| D8 | The vent splat throws a cube puff, on model splats too; the scripted vent victim is voxel | `NativeVentDeathPresentation.cs:12,82-101`; `NativeVentFallPresentation.cs:63-67`; `Core/NativeVentFall.cs:19-21` | Cubes; fallback | Soft dust and the blood pool (V3, Stage 2); the scripted victim only in the voxel style | Claude | S | DeathFxChecks:62; VentFallChecks:74-80 | Neutral | NativeVentDeath; the scripted victim |
| D9 | Kill styles that are voxel by design. Voxel breakup cuts up to 120 cubes from the rig's boxes, in voxel skin colours, even on a model pilot. Confetti's cubes are props of its scene clip (D6); VoxelCloud runs only when the clip is skipped | KEP:84-101,357-365; DP:309,395-397,438-439; `Core/NativeLocker.cs:59,82` | Cosmetic (owned) | Keep `voxel-burst` deliberately voxel: cut from the costume's own blocks, or from the model's per-bone boxes in the outfit's colours (V6, per the default in 8.2) | Claude (Astra only if an id changes) | S | KillEffectChecks:141 | Neutral | NativeKillBurst, KillCube, VoxelCloud |
| D10 | Stuck arrows bury and pass through by the pilot's voxel boxes, for every target. Zombie arrows ride the unshifted rig joint | `Core/NativeArrowGib.cs:23-118`; `NativeArrowGibPresentation.cs:47-118`; `NativeBowPresentation.cs:163-188`; `NativeHordeGorePresentation.cs:213-221` | Fallback; the hole source | Depth and exit from ModelSeat.Thick; the same seat on zombie models (V6, Stage 3) | Claude | S | GibChecks:286 | Neutral | The boxes path |
| D11 | Revives: the repair cells, scan axis and floor ring are sized from the voxel `pilot` boxes, the craters come from the voxel carve, and regeneration voxels fly in, on model pilots too | `NativeRepairModels.cs:11-28`; `NativeInteractionPresentation.cs:57,90,100-128`; `NativeReviveHoloPresentation.cs:12-166,272` | Cosmetic; fallback | Repair glints on the model's skin from D4's hole list, or hardlight cells cut from the model's per-bone boxes as a deliberate holo style. It lands with D4, which supplies the hole list (V6, Stage 3) | Claude | S-M | ReviveHoloChecks, interaction-parity.json, the in-player 360-cell gate | Neutral | The voxel revive path |
| D12 | Some corpses start from the rig's pose: late deaths, killcam rebuilds, the local pilot dying in first person, and Classic | DP:265-272; VBP:292-307; `Core/NativeCorpseDress.cs:8-11`; `NativeReplay.cs:65,92` | Source of truth for those corpses | Keep each death's drawn joint frames in the round's death cache; first person starts from a hidden third-person pose (V6, Stage 3) | Claude | S | Memory per death | Negligible | The rig path for deaths never seen |
| D13 | PvE gore cubes for adds that are not a horde kind; Low-tier cell caps on model cuts | `NativeGorePresentation.cs:6-139`; `Core/NativeBisect.cs:134,158-171`; `NativeVisualCutPresentation.cs:19,138,152,468` | Fallback; LOD | Flesh lumps and blood drops; a cheap flesh cap on Low (V6) | Claude | S | Low-tier GPU | Small | The cube and cell paths |
| D14 | "Voxel" in name only: NativeBlastSim's particles (drawn as shells), BurstPool's voxel bursts (drawn as streaks), MaxDeathVoxels. The Cyber Lord's data cubes are its style | `Core/NativeBlastSim.cs`; `Core/NativeBurstPool.cs`; `Core/NativeCyberLordFx.cs`; `Core/CyberKit2MeleeFx.cs` | Not a body dependency | Rename at most; the Cyber Lord's cubes stay unless the owner objects (8.2) | Claude | S | blast-parity.json, blast-pinned.json | None | As they are |

### 1E. Enemies

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| E1 | The zombie gait poses the 22-joint rig, and the model solves its legs a second time: footprints are planned in model metres, squeezed for the rig, then spread back. The Hulk, the Hound and crawls plan on the rig's own legs | `Core/NativeHordeMotion.cs` and five more partials (2,758 lines); `Core/NativeHordeLoco.cs:60-89`; `Core/NativeZombiePosture.cs`; VR:240-261,782-935 | Source of truth (presentation) | A model-native gait over a skeleton descriptor, writing the model's bones, with a 22-joint pose derived for consumers. The voxel rig becomes one more descriptor (Legs=1) and stays bit-identical (V8, Stage 5) | Claude | XL | Every reviewed gait fix can regress; 238 run-turn checks; blows at LastAttack+Strike, hands within Radius+.26, head inside the part bands | No performance credit: the binding it replaces costs 5.7-8.4 µs and the new gait still needs the leg IK | The rig-descriptor gait and its checks; NativePackZombie |
| E2 | The ZOMBIE PRO takes are retargeted onto the rig (64-channel tracks, 162 KB) | `Core/NativePackZombie.Generated.cs`; `Core/NativeZombieClips.cs`; `scripts/mocap/mixamo-retarget.mjs` | Source data | Re-export them for the model's skeleton on the owner's PC, as derived tracks like today's (V8) | Claude | M | Marks re-measured; the private pack stays on the PC | None | The rig tracks |
| E3 | Past the nearest 24 adds (16 on Low) the crowd draws voxel: Reduced to 40 m, Impostor beyond. That is 64-168 adds in the 88-zombie hub and the Siren's up to 192. Tiers go by rank, so the 25th-nearest add is Reduced even at 5 m. Their deaths, corpses (to 80 m, for 22 s, up to 24) and severed parts stay voxel too | HKP:17-18,75-76,133-147,336-342,579,601,666,691; `NativeHordeGorePresentation.cs:197-206`; `Core/NativeHordeModels.cs:13-62` | LOD | Model LODs (V7 part 3, Stage 4): one mesh per kind silhouette (male and female per kind), not per variant, authored offline in the tools/gear Blender pipeline into the local bank. Each LOD has 1-2 joint weights per vertex and is skinned in the vertex shader from a per-instance 22-matrix palette: one instanced draw per LOD model, with clothes and skin tinted per instance. They are posed by a cheap model-proportion leg solve on strides planned for model legs on every tier. Adds within about 12 m stay skinned models (at most 8 more than the Full count at High, 4 at Low, on a decimated cut of the bake's pieces). Deaths, corpses and parts are covered | Claude (Astra's numbers) | L | Silhouette fidelity and seams; the look effects; light reach; the zip cap | The per-variant rigid design would have needed hundreds of draws (appendix B, F2) | The voxel tiers, as voxel-style data |
| E4 | The kind-model path needs the voxel kinds built. HordeKindsReady counts voxel sculpts (229 boot jobs, about 1.7 s); without them every add drops to the legacy horde-0..5 bank. The boot queue also builds model assets | HKP:101,115,231,618,649; `NativeHordeKindModels.cs:20,38`; `Core/NativeBootSculpts.cs:31-45` | Hidden gate | Gate on the presentation being ready and split the boot queue (V7 part 2, Stage 3). The voxel kinds stay built at boot, on workers, while they draw as the far tiers (until V7 part 3) and while their Full builds are the gore surface (until E10). A hit never builds one on the main thread | Claude | S-M | Boot ordering; HordeKindChecks:269; a lazy build would stall the first hit on each kind (`Core/NativeHordeGoreBody.cs:47-51`) | Boot time is saved later, not now | The kit |
| E5 | Backrooms kinds past the Full tier draw as shambler look 0 at their own scale. Pinned adds take Full slots (Full = max(pinned, 24 or 16)). Far detail is Central Station only, and an unprewarmed pinned add draws nothing | `Core/NativeHordeModels.cs:76-79`; HKP:94-98,137-146; ZMP:118,310,363-367 | LOD | Pinned, with far detail widened to every pinned kind, a prewarm and the make-limit bypass (V7 part 2, Stage 3). Until V7 part 3 a pinned add stays Full at any count, as today (HKP:146); from part 3, pinned adds beyond the Full count draw the LOD | Claude (Astra's caps) | S-M | Invisible arrivals; Level 0's 18 pinned adds | Pinned Full bodies are E21's cost | — |
| E6 | The Hulk's heavy stride is on the rig. Its growth (about 48k faces) draws voxel until it attaches, and it has a voxel far tier. HULKAMANIA allows 6 | `Core/NativeHordeHeavy.cs`; `Core/NativeZombieMutation.cs`; ZMP:57-59,102,362-364; `Core/NativeExpeditionMutators.cs:19-21` | Source of truth; LOD | Its stride in V8; pinned; a coarse growth LOD (about 6-8k faces); growths made ahead for DEV monsters and summons (V7 part 3) | Claude (Astra confirms 6) | M | Owner-tuned hips and fists | Six growths come to about 0.3 M triangles without the LOD | The voxel Hulk |
| E7 | The Hound's and the Backrooms and zone overlays pose the rig: the gallop, pounce and pin; Dormant, Wake, Howl, Whistle and Hook; the Station and Ironworks overlays; model fits that edit the rig; the pop killer and the static writers | `Core/NativeHordeMotion.cs:209,1197-1243`; `Core/NativeBackroomsMotion.cs`; `Core/NativeIronworksMotion.cs:43,307-316`; `Core/NativeStationMotion.cs`; `Core/NativeHordeBlend.cs` | Source of truth | Rewritten on the V8 gait, with props held in the model's fists | Claude | M (in V8) | Each overlay was reviewed on its own map | None | The rig versions |
| E8 | The Smiler and the Tiler pose the shambler's rig as an invisible anchor (they draw their own bodies, even without the zip) | `NativeSmilerPresentation.cs:13`; `NativeTilerPresentation.cs:10`; HKP:313-317,340-341 | Anchor | Sockets from their own bodies; no gait (V7 part 2) | Claude | S | Low | About 6 µs each | — |
| E9 | Effect sockets come from rig world joints: the claw glow, lunge flashes, the spit's wind-up at the voxel mouth socket, the belly swell, minion crowns and grins | HKP:348-360,418-423,480-511; `NativeMinionFxPresentation.cs:14-103` | Anchors | Model sockets (ZombieModelSocket and Shift). The glob's flight stays Astra's record (V7 part 2) | Claude | S-M | Low | One lookup | The rig versions |
| E10 | Zombie gore lives on the voxel surface: hits are placed on Full voxel surfaces (about 6 MB of copies), model zombies show no live wounds, and stumps and chunks are box-authored. (The cube look itself was replaced by orbs on 2026-11-09) | `Core/NativeHordeGoreBody.cs`; `Core/NativeHordeGore.cs:427,508-523,686-791`; `NativeHordeGorePresentation.cs:24,44-110,243-248` | Source of truth, and a gap | A per-variant gore surface from the model at rest; wounds on the model; flesh caps from its cut rings (V8's gore, Stage 7) | Claude | L | Fixed budgets (96 adds, 2,048 a frame); crowd CPU; ZombieGoreChecks digests | Frees the 6 MB when voxel kits do not draw | NativeHordeGoreBody, built only when voxel kits draw |
| E11 | Fallbacks into voxel enemies: no bake, `--ca-voxel-zombies`, Classic (also THE 6:47's box sculpt and summoned zombies), a bake without VisualSkeleton, the Viewer, and a frame of voxel on an empty pool or an unbuilt growth | ZMP:16-35,57-59,79-90,326,350-366; `NativeStationBossBody.cs:20,30` | Fallback | The no-bake and DEV switches stay; Classic is decoupled; a pool miss hides the add for one frame (V7 part 1, Stage 2, with W7's district outfits) | Claude | S | Low | None | The voxel kit, ZombieLit, the voxel gait |
| E12 | The DEV galleries (minion, Hulk, Station, gore) draw voxel only, so the owner reviews those moves on voxel bodies | HKP:725-797; `NativeHordeHulkGallery.cs:46,69,106`; `NativeStationKindsPresentation.cs:483`; `NativeHordeGoreGallery.cs:67` | Tooling | Draw them on the model, as the Ironworks gallery does (V7 part 2) | Claude | S | Low | None | A voxel toggle |
| E13 | Wall breaches burst into cubes, outside the debris switch (owner: "Breaking boxes also seem to still break into cubes") | `NativeHordeBreachPresentation.cs:193-198,229-230,297-298,343-344`; `Core/NativeBreach.cs:139,204-211,338-347` | Cosmetic | NativeDebrisShapes pieces in the wall's material, in the same slots and spins so the restore still flies them home. Cubes only under `debris:"voxel"` (V13, Stage 2) | Claude | S-M | BreakAt, RestoreAt and the collider are Astra's | Neutral (96 at most) | The cube path |
| E14 | The Ironworks brute's cream seams (owner decision 14, 2026-10-16) exist only in the voxel variants | `Core/NativeHordeModels.cs:37`; `Core/NativeIronworksModels.cs:247`; `Core/NativeHordeKinds.cs:165` | An owner decision held only in voxel data | A cream tell on the modelled brute (V7 part 2) | Claude | S | Taste | None | The cream variants |
| E15 | The crawler and the bloater have no model (not shipped; the DEV mix draws them) | `Core/NativeZombieLooks.cs:351-355`; `Core/NativeHordeKinds.cs:36-37` | Proposals | Model variants before either ships; until then they stay voxel-only in DEV (8.2) | Both | M | Low | — | Their sculpts |
| E16 | Summons and the Leet field's Slenderman: summoned zombies draw voxel until a body is ready, the Slenderman has no model path, and summon puffs are voxel | SP:139,150-171; `NativeSummonPresentation.cs:12-82`; `Runtime/NativeLeet.cs:102` | Presentation | The summon on the model, with the LOD while it waits; a modelled Slenderman (its own body); puffs as VFX (V11, Stage 7) | Claude | S-M | Low | None | `summon-*`, `slender`, SlenderRig.json |
| E17 | Boss sculpts: THE 6:47's box concept (no bank, Classic, the zombie flag); THE CONVERTER's code sculpt, whose file holds constants the authority reads | `NativeStationBossBody.cs:5-98`; `Core/NativeIronworksBossLook.cs:68-90`; `NativeZoneBossAuthority.cs:73,338` | Fallback art | Decouple Classic; keep both. Move THE CONVERTER's constants out before anyone trims its sculpt (Astra reviews) | Both | S | Deleting a constant with its sculpt | None | Both sculpts and every NativeIronworksBossModel constant |
| E18 | The only enemy body-in-capsule check poses voxel models | `Tests/HordeKindChecks.cs:112-174` | Test guarantee | A model capsule-fit check on the Measured skeleton (V7 part 2) | Claude | S-M | — | — | The voxel check |
| E19 | The overhaul multiplies zombie variants (W7's district outfits, in M2) against a per-variant pool: 6 spares, 8 ahead, 3 made a frame | ZMP:35,350; OVH:1325-1327,1365,1671-1676 | Overhaul risk (more voxel pops) | Hide on a miss and prewarm by district on a worker, in the same release as the district outfits (V7 part 1, Stage 2); the LOD as the miss fallback from Stage 4 | Claude | S | W7's acceptance checks only the pool limits | None | — |
| E20 | Every living add runs the full gait on every tier and writes its 22 rig Transforms each frame; the voxel queue then reads each part's matrix back. Impostors skip only the foot twist and heel roll, and Astra's 10 Hz throttle covers only the off-camera model fit | HKP:313,333,392; `Core/NativeHordeMotion.cs:350,369,1102`; DEV_LOG.md:5927-5930 | Cost on every tier | Animation LOD (V7 part 3, Stage 4): LOD adds posed at a staggered lower rate, with every attack's contact frame posed exactly; impostors on cached cycle poses chosen by speed, with no IK; LOD matrices written straight into the instance buffer, with no Transform hierarchy | Claude | M | Blows must still land on screen at LastAttack+Strike; the blend's contact-frame rule | Gait 7-16 µs an add on .NET 8 (docs/ZOMBIE-MOTION.md:142-153): 64 LOD adds cost about 0.4-1.0 ms and the Siren's 168 more about 1.0-2.7 ms, before Transform costs. Mono is unmeasured | The full-rate gait, for the Full tier and the voxel style |
| E21 | The Full tier's cost is mostly inside Unity. The project skins on the CPU: ZMP:140 says so, and the Editor-expanded settings read `gpuSkinning: 0` and `meshDeformation: 0` (the committed ProjectSettings.asset is a stub without either key). Each body has about 16 renderers, 15 materials and 148 bones, and Full bodies cast shadows to 60 m (far detail drops them past 43 m, for pinned kinds only). Editor, 24 adds: 20.8 ms modelled, 15.6 voxel, 8.5 none. Our own gait, fit and binding code is about 0.3-0.4 ms of that | ZMP:136-150; HKP:76; DEV_LOG.md:6186-6187,6400; docs/ZOMBIE-MODELS.md:166-172; docs/ZOMBIE-LOCOMOTION.md:544,767; docs/BOZO-INTEGRATION.md:796 | The crowd's largest cost | V15 (Stage 2): a GPU-against-CPU skinning A/B in the player, with Astra committing the setting (VA4); each body's renderers merged into 1-3 skinned meshes with its fixed shapes baked in; 2-bone skinning and half-rate posing past about 15 m; shadows only within about 20-25 m; the Full counts each tier affords measured in the player and applied with the LODs in Stage 4; a go/no-go before Stage 3 | Claude (+VA4) | M | The merge must keep covers, wound pits, cuts, tints and hides working; ZombieModelChecks | The model-over-voxel gap is about 4-5 ms in the Editor at 24 adds | — |

### 1F. World

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F1 | The dressed-block renderer draws every map's blocks as instanced chamfered boxes. It is the fallback on banked stages and the only look on the generated and authored cities, transit, foundry and training. It also draws the damage stub, the collapse tell and BLOCK_PACK's pack props. (New arenas skip it: Thunderheart Carillon draws its own mesh bank, `NativeArenaDressing.cs` on `b13940ce`) | `NativeArenaDressing.cs:8-140`; `Core/NativeBlockLooks.cs`; `Resources/NativeBlockLit.shader` | Fallback, and some tells | Bank coverage everywhere; this renderer only for the fallback and the voxel style; BLOCK_PACK moved to a StageLit pack path (V13, Stage 8) | Claude | L | Public builds have no banks; the Windows shader compiler is fragile | Neutral | All of it |
| F2 | The Blender stage art is modelled on the kits' exported parts. Kit parts: Sanctum, Substation, Emporium, Cyber City, Ward Zero, Factory, Polaris. Shared numbers: Pier 9, Breakwater, Causeway, Skyframe. Blocks only: Brickfall, Deepwell | `Tests/StageArtLayout.cs:13-55`; docs/arenas/STAGE-ART.md | Offline tooling | Freeze each layout as committed data; author new stages art-first, as Thunderheart Carillon was | Claude | M | A frozen layout drifting from its kit | None | The kits; StageArtLayout |
| F3 | Six stages pose their modelled moving parts from the box rig's parts: Breakwater, Causeway, Deepwell, Pier 9, Polaris, Skyframe. (The Sanctum and the Substation read the rig's state arrays; Cyber City and Ward Zero already read hazard state) | `Core/NativeBreakwaterArt.cs:5-41` and its peers; StageArtChecks:29-75 (2 mm, the Sanctum's), the peers at 1 mm | Source of truth every frame | Pose records from hazard and clock state, read by the art, the boxes, the FX and the lights (V13, Stage 7) | Claude | L | Hurting parts must match Astra's volumes and timings; 512 pose slots | A small CPU win | The rigs and boxes |
| F4 | Lit boxes kept over the art: Cyber City 944, Ward Zero 3,081, the Factory's and the Emporium's lamps, the club's 496 screen tiles | `Core/NativeCyberCityArt.cs:39`; `Core/NativeWardZeroKit.cs:90,168`; `Core/NativeStageArt.cs:44-89,156` | Telegraphs, by design | Glow sub-meshes in the bank tinted by a lamp batch, or kept as flat lit panels (V13, Stage 6) | Claude | L | Readability and timing | Neutral if instanced | The kept-box path |
| F5 | Box backdrops in released builds: Brickfall (about 15,000 boxes), the Factory, the Emporium, the club's and Pier 9's far sectors, Cyber City's far ring, the Sanctum's glass, the harbour yards' ground, the generated hub's skyline | `Core/NativeSceneryBuilder.cs:18-27`; `NativeBrickfallPresentation.cs:122-134`; `Core/NativeExpeditionCityScenery.cs:9,45-50`; `Core/NativePackScenery.cs` | Cosmetic | Far chunks per bank, or a shared skyline bank (the hub's in W8c) (V13, Stage 6) | Claude | L | Bank budgets (Brickfall has about 1 MB of 20 left); the zip cap | Similar | The scenery builders |
| F6 | The menu's map orbit draws box scenery for 4 maps and nothing for the other 9 banked stages | `Runtime/ConversionGame.cs:301-317`; `NativeBackdropPresentation.cs:9-29` | Menus | A backdrop mode for the stage-art drawer, or modelled scenes (V13, Stage 8) | Claude | M | A resident bank; a load hitch | One bank | The box backdrops |
| F7 | The Central Station and City Ironworks instances (6,743 and about 7,600 parts) have no stage bank. Their hazards are live, and their fields, lights and FX come from the kits. The overhaul takes their warp and threshold materials from stage banks that do not exist (OVH:156) | `Core/NativeExpeditionStationKit*.cs`; `Core/NativeIronworksKit*.cs`; `NativeExpeditionZoneHazards.cs:14-19` | Source of truth (the only look) | Stage banks on the stage-art recipe, rigs posed from the live hazard views, fields from fixture tables (V13, Stage 4) | Claude | L each | IronworksVisualChecks:241 must stay bit-identical; the molten shaders; the zip | Similar | The kits |
| F8 | Light fields, lights and FX are baked from the kits' glow lists: Ward Zero, the Ironworks, the city, the Sanctum's candles, Pier 9's bulbs, Deepwell's smoke | `Core/NativeWardZeroField.cs:107`; `Core/NativeIronworksField.cs:35`; `Core/NativeExpeditionCityLights.cs:27`; `Core/NativeSanctumFx.cs:46`; `Core/NativePierFx.cs:55`; `Core/NativeDeepwellFx.cs:144` | Source of truth | A fixture table, filled by the kit today and by the bank later, map by map as each bank lands (V13) | Claude | M | Bit-identical pins (HordeKindChecks:337, IronworksVisualChecks:241,244, WardLightingChecks:52,89) | None | The glow lists |
| F9 | The light volume (StageLit's AO and bounce) and the weather cover are baked from the blocks and their box looks | `Core/NativeLightVolume.cs:28-45,92,296-316`; `Core/NativeWeather.cs:14`; `NativeAdvancedLighting.cs:116-143` | Source of truth | Rasterise the bank's chunks, with W10's rebuild (V13, Stage 6) | Claude | M | Load time | Worker time | The block bake |
| F10 | Small box leftovers: the collapse tell (Cyber City's billboards lose it with the bank), the Backrooms' pit and the Poolrooms' door blocks, the damage stub, the state pieces, join pads and laser door, the hazard kit's 30 alarms, Calibration Bay | `NativeArenaDressing.cs:28-33,145-152`; `Runtime/NativeStageArt.cs:71-72,167`; `Core/NativeBackroomsGenerator.cs:104`; `NativeExpeditionStatesPresentation.cs:9-14`; `Core/NativeHazardKitBuild.cs` | Presentation; some gameplay tells | Cyber City's Tell hook and the Backrooms blocks (V13, Stage 2); modelled stubs, posts, pads, frames and beacons posed from the same states (V13, Stage 7). The studio stays unless the owner objects (8.2) | Claude | S-M each | A sealed door must read as blocking from its first frame | Neutral | Every box piece |
| F11 | Dead legacy trims: DetailProp builds 2-3 hidden cubes per wood or metal block (about 5,600 across 18 maps) on every arena build. It lives in Claude's lighting file; only its call site is Astra's | `Runtime/NativeLighting.cs:417-433`; called at `Runtime/NativeDestruction.cs:42` | Dead build work | DetailProp returns early on dressed maps, inside `NativeLighting.cs`; the call site is untouched. Kept for a voxel-classic map (V13, Stage 2) | Claude | S | Low | Arena-build CPU and GC | The trims' code |
| F12 | Legacy Electron world data is built at every boot: 15 `map-*` models (64% of the decoded SourceModels), `ward-motion-*`, the scenery and hazard models, WardMotion, and SceneryMotion (whose loader drives the sky's clock on every map) | `NativeSourceModels.cs:76-100`; `NativeSceneryMotion.cs:18-21`; `NativeWardPresentation.cs:5-30`; `Core/NativeUnityMaps.cs:68,81` | Data, dead in play | Build these meshes on first use, after moving the clock call out (V12). A playable voxel-classic map would also need Astra's legacy hazard rules kept | Claude (Astra for a mode) | S-M | WardZeroChecks:256-257; the commit checks | Boot time and memory | All of it |
| F13 | The unused M1 stage kit (its Maps list is empty). Its scenery helpers are a live library for seven stages, and rules files use the kits' id grammar | `Core/NativeNewStageLooks.cs:15-20`; `Core/NativeNewStageScenery.cs`; `Core/NativeExpeditionHub.cs:390`; `Core/NativeIronworksHazards.cs:69,102` | Inert code | Leave it in place: no deletions | Claude | — | NewStageLookChecks:10-61 | None | All |
| F14 | Entrance stage sets from the Electron game (`stage-*`, 10% of the decoded SourceModels) | `Runtime/NativeIntros.cs:216-235`; `Core/NativeEntranceStageModels.cs` | Cosmetic | A modelled set per style, with M8 (V5, Stage 4) | Claude | M | The dev-siren face rides the bake's head track | None | `stage-*`, SceneMotion |
| F15 | Debris is realistic by default, with the voxel cube style kept as a map switch (owner 2026-11-13). Prop fragments' physics boxes are Astra's prop physics, not voxel | `Core/NativeDebrisShapes.cs:6-35`; `Core/NativeMapLooks.cs:71-73`; `NativeDestruction.cs:38-56` | Done | Nothing. Physical fracture only if the owner asks (Astra) | — | — | — | — | NativeDebrisLook, DebrisChecks |

### 1G. Engine, data, contracts, tests and docs

| ID | Dependency | Where | Role | Replacement | Owner | Effort | Risk | Perf | Kept |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | The authority's volumes are the voxel pilot's: the capsule (radius .42, half height .85), the head box (the voxel head block at rest: +.64 m, half .17), the feet at body − .865 | `Core/NativeLocomotionRules.cs:8`; `Core/NativeCombatRules.cs:16-22`; `Runtime/NativeArena.cs:105,343,351`; `Tests/ParityLogicChecks.cs:60-62` | Rules | Keep them. The model's head already stays within −.04 to +.11 m of the box (docs/BOZO-INTEGRATION.md:686-687). BoZo P3 only on the owner's word (8.2) | Astra | — | Balance, if changed | None | The constants |
| G2 | The wound and death vocabulary on the wire. `NativeWound.part` is a rig joint index (validated below 22), chosen by voxel-shaped region thresholds; the trauma energies are tuned for voxel bodies; death records carry bank ids (`pilot`, `siren`, `summon-*`, `horde-N`), which three readers route on | `Core/NativeBodyDamage.cs:7-19`; `Runtime/NativeDeaths.cs:14-39`; `NativeNetworkFrame.cs:34,87`; `NativeSummonPresentation.cs:62`; `NativeGorePresentation.cs:91`; `NativeMinionFxPresentation.cs:183` | Wire contract | Keep all of it as opaque tokens; presentation maps them onto model bones. A denser skeleton would get a versioned part map, never a renumbering | Astra (the field), Claude (the readers) | S to keep | Replays; old clients | None | All |
| G3 | VisualSet and MagicVisual travel on the wire as weapon-family flags | `Core/ArenaModel.cs:45,98`; `Runtime/NativeArena.cs:109-110` | Wire data, not voxel | None | Astra | — | — | — | Both fields |
| G4 | The cosmetics transport. `seat.look` (512 characters at most) already replicates per seat into replays and killcams; the skin map (320 at most, longest 309) carries `body` and the Prototype picks. The look map's `Slots` are the five wardrobe slots, and code indexes five-entry arrays by them (`NativeRobotSuit.Under`, the zombie variants' `Pieces`, the Locker's rows). The account Worker has drifted from Unity: its look slots lack the robot defaults and the Hot Dog, so a signed-in open hoodie is lost on load, and its face keys and gun skins are stale | `Core/NativeLook.cs:20-48,125-139`; `Core/NativeRobotSuit.cs:37-46`; ZMP:419-426; `Runtime/NativeLockerOutfitUI.cs:19-24`; `Core/NativeWeaponSkins.cs:63-78`; VBP:97-108; `NativeNetwork.cs:44,190`; `NativeProfileUI.cs:51`; `src/relay/accounts.ts:27-56` | Data, save and network | The costume as a key in the look map, like `skin`, never a sixth slot; the variant from `body`; KeepSkin keeps it across logins on this PC; the Worker resynced (VA1) (V2) | Both | S-M | Until VA1 deploys, a signed-in costume pick lives on this PC only | None | `body` and the Prototype keys, unchanged |
| G5 | Voxel data exported from Electron at `05950eae`, commit-checked at load and asset-locked: SourceModels (6.6 MB gzipped, about 80 MB decoded, 140 models), SourceMotion, SceneMotion (about 94 MB decoded), WoundParts, HazardModels, SceneryMotion, WardMotion, SlenderRig; and the generated tables CapturedClips, NativeKillBoxes, NativeBroadcastEdges and the 11 NativePack* tables. The lock is enforced only when packaging, and it does not cover SirenLook.json or any generated table | `tools/export_models.mjs`; `tools/asset-lock.json`; `tools/package_source.py:47-52`; `NativeSourceModels.cs:30`; `Tests/WeaponSkinChecks.cs:24` | The data to keep | None: byte-identical, with digests checked in every CoreTests run (VoxelStyleChecks, V0) | Both | — | Any edit fails the lock or the digests | About 190 MB of JSON decoded at boot | All |
| G6 | NativeSourceModels is both the voxel bank and the instanced drawer for effects, model hats, hazards, machines and the new Standard weapons (the Glaive, the Katana's saya, the Fangs), and its constructor builds everything at load | `NativeSourceModels.cs:16-25,79-100`; `Runtime/NativeKillModels.cs:7-16` | Infrastructure | Split off a model-agnostic effects drawer; build voxel-only meshes on first use (V12) | Claude | L | Boot ordering | Boot time and memory | The bank |
| G7 | Electron shares the retarget tooling and the source data with Unity's exporters, and the Electron/Site game must keep working | `scripts/mocap/*`; `src/client/*`; AGENTS.md | Legacy runtime | Leave both byte-identical; model-native retargets are new paths | Both | — | CI's sparse checkout lacks `src/` and `scripts/` | None | All Electron voxel code |
| G8 | The licensed bake exists only on the owner's PC and in the Drive zip | docs/BOZO-INTEGRATION.md:788-798; `tools/install_local_assets.py:17-60` | The model side | The builder gate and its Editor check (VA2) | Astra | S | A single point of failure | The zip grows toward its 480 MB and 512 MiB caps | — |
| G9 | CI runs the full CoreTests on every push to `claude/**` and `codex/unity-*`, and the publisher needs it green | `.github/workflows/unity-conversion.yml:2-48`; `tools/check_relay.py:32-58` | Enforcement | Re-pin in the same commit; add the voxel ratchet and VoxelStyleChecks' digests (V0) | Both | S | A red CI blocks both agents | None | — |
| G10 | Docs that still state voxel as the game's identity or plan: DESIGN_INTENT.md:9 ("A 3D voxel arena game"), the overhaul's W3 and W5 fallbacks, the stale header at VBP:7-17,83-85, BOZO-INTEGRATION's still-voxel lists, VISUAL-REVISION-PLAN. AGENTS.md, which both agents read at every start, carries no voxel direction | The files named | Docs | Record the decision once: a dated two-line owner direction in AGENTS.md pointing to `docs/VOXEL-STYLE.md`; edit the living docs; leave the history (V0) | Both | S | Agents read DESIGN_INTENT and AGENTS.md at every start | None | The history |
| G11 | False positives a cleanup must not touch: the Camera's "classic" mode, the light volume's voxel grid, Blender's voxel remesh, VisualSet, "Impostor" as a tier name | `Core/NativeCameraRig.cs`; `Core/NativeLightVolume.cs:34-35,244-250`; `tools/gear/hum_boss.py` | None | None | — | — | Renaming `prefs.cameraMode` or VisualSet breaks saves and snapshots | — | All |
| G12 | Costs are unmeasured where they matter. The step-4 pilot numbers predate the default helmet and the robot clothing, a showing wound adds about 10k triangles, and the 8-pilot pass was never run. Every crowd figure is from the Editor (i9-13900K, RTX 4090) or from .NET 8 in a container, while the shipped player is a Development build on Mono. No low-end machine is defined | docs/BOZO-INTEGRATION.md:796,1826; docs/CHARACTER-LOWPOLY.md:504-508; `Editor/PrototypeBuild.cs:116,121`; `Editor/NativeCloudBuild.cs:25`; OVH:911,1782 | Perf unknown | Measured in the player with `PERF_SAMPLE` and `PERF_HITCH` (`Runtime/NativeHitchDiagnostics.cs:72-74`), the Editor-to-player ratio recorded once, and a low-end run (V0) | Claude | S | Decides O3 and the crowd budget | — | — |

### 1H. New content since the model

Each feature's voxel reads, counted at `b13940ce`. The counts are of these token families: voxel gates (`ClassicBodies`, `VisualBodiesOn`); voxel measures (`CapturedRig.Boxes`, `WoundedPilot(`); rig reads (`.Bones[` on a rig); rig authoring (`new CapturedRig.Pose(`, `new MeleeRig(`, `.AddE(`, `.WritePose(`, pose edits by CapturedRig index); and voxel-frame draws (`WeaponShape.Cube` pieces, `QueueHeld(`, `Queue("weapon-…`, `Queue("loose-…`).

| Feature | Voxel reads | What they are | Route | Package |
| --- | --- | --- | --- | --- |
| NOCTIEL (Lux Cecidit), THE HUM, THE DEEP END, the boss relics, Thunderheart Carillon | 0 | Their own skeletons or mesh banks, their own sockets, no rig reads (their `CapturedAnimation` uses are the Release helper only) | **The pattern to follow** | — |
| Top Dog (the Hot Dog class) | 2 rig reads | The hands and the head (for the mouth) read from the rig | The resolver | V1 |
| Glaive | 2 voxel-frame draws | The ring blade is a Standard code sculpt in the voxel weapon frame, drawn through the voxel bank's drawer (in hand and in its intro) | Life-size sockets; the drawer split | V10, V12 |
| Katana and Fangs (the blade classes) | 1 rig read, 2 voxel-frame draws, 4 cube pieces | Their motion is model-native (the Frank sets). The Katana's sheathed saya (`weapon-katana`) and the thrown fang (`loose-fangs`) draw through the bank's drawer; the blade intros and profile hold 4 cube pieces | The drawer split; modelled pieces | V12, V5 |
| DEV Katana, DEV Greatsword | 2 rig reads (the DEV fighters' effects) | Their motion is model-native. Their echoes need the voxel afterimage model, and the DEV Greatsword's menu weapon is the voxel greatsword | Echoes from the drawn model; DEV specs through `NativeDevMotions.Base` | V0, V1, V3 |
| Bombadier | 7 rig reads, 41 cube pieces | Mines, detonator, belt and satchel in play (23 pieces); the fuse and charges in its "higher-detail" intro (18); palm anchors on "the game rig's" hand | Modelled props; anchors through the resolver; the belt on the costume's attachment table | V3, V1, V2 |
| Weapon techniques | 1 rig read, 21 cube pieces, 1 voxel-frame held weapon | Tripod, bipod, stakes and ammo crate built from cubes. The Sentry Drop draws its gun through `QueueHeld` (the voxel-frame Standard model), while the thrown blade and the planted shield already use the model's own weapon through `WeaponAt` | Modelled props; the Sentry's gun through `WeaponAt` | V3 |
| Abilities and wild chips | 0 in their rules and presentation files; 17 rig pose edits (70 rig references) in the gestures | The frag grenade, orbital mark and rocket kick gestures (`Core/RangedAbilityPose.cs`) and the gun kits, Fan the Hammer and Overdrive (`Core/NativeGunKitMotion.cs`), are authored on the rig | Uncovered kit moves, on the model | V9 |
| Chips (cards, modules, body chips) | 3 rig reads, 6 cube pieces | Body-chip pieces from cubes; their sockets read rig joints. Owner: "I think some chips still use voxel implementations too" | Modelled pieces; sockets through the resolver | V3, V1 |
| DEV Cyber Lord | 16 rig reads, 3 box reads, 39 MeleeRig uses, 89 cube pieces | Its whole set is authored on the rig; its shimmer sits on voxel box faces; the cubes are its data-cube style | A model set; the shimmer on the skin; the cubes kept as style | V9, V3 |
| The Siren | 4 rig reads, 2 new rig poses, 1 voxel-measure read, 54 rig references | Her look, smile, claw and crush are built on voxel parts | Her own model | V11 (O5) |
| Entrances (procedural, stage, Sheldon, DEV) | 48 cube pieces | Props and stage pieces from cubes | Modelled sets | V5 |
| The overhaul's M1 work | W0, W2: 0. W3: 8 rig-authoring uses. W4: 1 bank draw | W3's eject states are rig-authored (M12); W4 draws its pod through the bank's drawer | The eject on the model (M12); the drawer split | V4, V12 |
| The overhaul's later packages (W5-W12) | Not written yet | — | They follow the new-content rule and the ratchet from the start | V0 |

---

## 2. Target architecture

### 2.1 Principles
1. **The model's skeleton is the truth for everything drawn.** New classes, weapons, enemies, animations, effects and menus are authored, timed and socketed on the model: the bake's skeleton, its motion bank, the life-size weapon models, and the mesh banks. NOCTIEL, THE HUM, THE DEEP END, the boss relics and Thunderheart Carillon already work this way (1H). This is the "more detailed framing".
2. **Costumes are looks; how the voxel one moves is the owner's call (O1).**
   - Under O1(a), the recommendation, a costume rides whatever the model plays, so new content works for every costume automatically. The voxel costume is the voxel look on the model's frame: 1.79 m, the model's proportions.
   - Under O1(b), the voxel costume is today's voxel pilot on the rig path for that seat. It is faithful, but every new move and effect then needs a rig version or shows the rig's generic one (M11).
   - The owner chooses from one sheet (2.3).
3. **The 22-joint rig stays as a contract, not as a source.**
   - The authority and the wire keep its joint ids, the wound frame, the .865 root and the bank-id tokens (Astra's).
   - Presentation derives a 22-joint pose from the drawn model for cosmetic consumers of joint ids (2.5).
   - Nothing new is authored on the rig. W3's eject states move onto the model (M12).
4. **Voxel draws only by choice:** a pilot's costume, a map or mode switch, a build without the bake, or a DEV switch. Any other voxel draw is a bug. The DEV voxel draw counter (V0) catches the ones on screen; the ratchet (V0) catches new voxel reads, rig authoring and cube props in code.
5. **Nothing the voxel style needs is deleted.**
   - The data stays byte-identical and loadable, guarded by the asset lock, digests in every CoreTests run (V0), the SHA pin and the parity fixtures.
   - The legacy rig path is frozen for content added since the model (M11).
6. **No rule moves.**
   - Hit volumes, timing, damage, cooldowns, movement and networking stay unchanged. Making hits match the taller model (BoZo P3) stays parked.
   - A drawn pose never feeds a choice that host, guests and killcam must agree on (2.5).
7. **The owner's standing decisions hold:**
   - the voxel debris map switch;
   - enemies may have bodies of their own (the Siren and the Slenderman get theirs);
   - the match idles stay as they are, so pose work means lobby and portrait poses;
   - no invisible walls: every costume, prop and modelled piece keeps to its collider rules, and the costume passes the capsule check (2.3).

### 2.2 One body predicate
`Core/NativeBodyStyle.cs` is pure, engine-free and Claude's. `ArenaPresentation.ModelBody(a)` is the only question a draw path asks. With it comes the joint-frame resolver `ArenaPresentation.JointFrame(a, j)` (M6): the model's joint when the model is drawn, otherwise the rig's.

**Inputs:**
- the bake, through VisualBodiesOn;
- the DEV override (`--ca-voxel-bodies`, or the DEV panel's "Voxel style (this PC)");
- the map's voxel keys, through NativeVoxelStyle (2.4);
- the actor: its spec and kind, and whether it is the Viewer's;
- the seat's look and skins.

| Case | Draws |
| --- | --- |
| A pilot, bake present | The model in its outfit |
| A pilot in the voxel costume, bake present | O1(a): the model's skeleton wearing the costume, on the same draw path as any outfit (2.3). O1(b): that seat draws the voxel style on the rig path |
| Any pilot on a map with `bodies:"costume"`, bake present | The costume, forced, in the pilot's own variant (Standard if none), with Prototype weapons where they exist |
| Any pilot on a map with `bodies:"classic"` | The voxel style on the rig path, with its twins, corpses and carve: today's voxel game |
| A class with its own body (Top Dog, and the Siren until V11) | The class's body. It wins over the costume and the map |
| The DEV override on | Every pilot in the voxel style, for comparison |
| No bake (CI, cloud, public builds) | Every pilot in the voxel style, in its variant. The builder refuses to publish such a build (VA2) |
| Zombies and zone kinds, bake present | The model at every distance: the Full tier, the near band, then the model LODs (V7) |
| Zombies with no bake, with `--ca-voxel-zombies`, or on a map with `zombies:"voxel"` | Their voxel kits on the rig descriptor |
| Survivors (W5) | The model only; not drawn without the bake |
| A body-pool miss | Hidden for one frame, or the LOD; never voxel |

### 2.3 The voxel costume

**The choice (O1).** One review sheet, V2's first step: quick prototypes captured on the owner's PC before the costume is built in full.
- **(a), recommended: the voxel look on the model's frame,** in three fits:
  1. long segments stretched in their core, with bevels and panel lines kept at their size, the chest and hip blocks narrowed to the model's shoulder and hip lines, and the head block on the head bone;
  2. the long segments rebuilt as stacked blocks;
  3. the wide voxel chest kept, with the arms offset. It is offered only if it passes the capsule check below.
- **(b): today's voxel pilot exactly,** per seat, on the rig path.
- **The sheet shows both beside today's Classic voxel:** front, side and back; idle, run and attack; a Katana combo, which shows (b)'s generic moves; a death.
- **Two taste questions on the same sheet:**
  - how a costumed pilot dies: flesh caps inside the voxel shell, or voxel cells at the cut;
  - whether a map's `deaths:"voxel"` may override a player's owned kill style.

| | (a) Blocks on the model's frame | (b) The old pilot on the rig path |
| --- | --- | --- |
| Looks | Blocky, but 1.79 m, with the model's long legs, narrower shoulders and a smaller head | Exactly today's: 1.70 m, hips at .57 m, shoulders at 1.10 m, a head block of about 34 cm |
| Moves | Everything the model plays, now and later | The rig's moves. The classes added since the model (Katana, Fangs, DEV Katana, DEV Greatsword) play generic moves. The fix is a derived pose (the model's skeleton posed hidden, its joint rotations put on the rig's bone lengths, feet and grips re-solved; M-L, and never an exact match) or an offline bake per pack (M11) |
| Deaths and effects | The model's: cut in the shader, model wounds, model corpses | The voxel corpse, bisect, carve and twins stay live in normal play for that seat (V3, V6 and V12 keep them per seat) |
| New content | Works automatically | Every new move, socket and effect needs a rig version or shows the generic one: a lasting voxel dependency, against "remove the voxel dependency" |
| Effort | L (V2) | S-M to start, then upkeep with every feature |

**How it is chosen:**
- In the Locker, a COSTUME card offers None and Voxel.
- Under Voxel, the variant cards: Standard (the sharper armoured voxel), Prototype (the original), and Sheldon Clothes (DEV).
- A "Voxel set" toggle switches every weapon that has a Prototype to it, and remembers on this PC the picks it replaced (Arsenal picks included). Turning it off restores them; the remembered list is never sent.
- Each weapon's own pick stays independent. The Katana, Fangs, Glaive, Top Dog and Bombadier keep Standard (O2).

**How it is saved** (no new field anywhere):
- **One key, not a slot.** The look map gains one key, `costume`, written like `skin`: after the five slots and before `skin` (for example `upper:hard-jacket,costume:voxel,skin:c48a62`).
  - It is not a sixth slot. `NativeLook.Slots` stays the five wardrobe slots, because code indexes five-entry arrays by slot: `NativeRobotSuit.Under`, the zombie variants' `Pieces` (ZMP:419-426) and the Locker's outfit rows.
- **NativeLook's helpers:**
  - it gains CostumeOf and WithCostume;
  - Normalize and Join write the key in its place;
  - Gate passes it through (it is not DEV-only unless O2 says so);
  - SameOutfit compares it, so putting the costume on or off re-dresses the body instead of retinting it in place (VBP:859-863).
- **Logins:** KeepSkin keeps this PC's costume while the account's saved look lacks it, as it already does for the skin tone and the Hot Dog (`Core/NativeLook.cs:131-139`; the login merge at `NativeProfileUI.cs:51`).
  - So Stage 1 works for signed-in players, expedition players included, before the Worker deploy.
  - Until VA1, the pick lives on this PC only.
- **The variant** is the existing `body:` entry of `prefs.weaponSkins`, and the weapons are the existing `spec:prototype` entries.
- **The account** keeps the same strings in `cosmetics.look` and `cosmetics.weaponSkins` once VA1 deploys. NativeLook's header gives carrying the look to clients and the account to Astra, so Astra approves the key and its place with VA1.

**How it is synced** (no new seat field, no frame-limit change, no protocol bump):
- **The path:** `seat.look` and `seat.weaponSkins` already travel per seat:
  - through the host's gates (`NativeNetwork.cs:44,190`: NativeLook.Gate, and NativeBodySkins.Gate for the Sheldon variant);
  - in the seat frames, at 512 and 320 characters, both unchanged;
  - into replays, killcams, spectators and late joiners, because VisualOutfitOf reads the displayed arena's seat (VBP:97-108).
- **Older builds:** an older client drops the unknown key and draws the outfit. An older host drops it for the whole room, because it normalizes every joining seat. Both go in the release notes.
- **Blind pick:** the host blanks the skin map in the lobby (`NativeNetwork.cs:199`). So the lobby shows the costume in Standard without Prototype weapons, and the match shows the real picks. CostumeChecks pin this.

**Precedence:**
1. a class body: Top Dog keeps the Hot Dog, and the Siren her own body (voxel until V11);
2. a map's `bodies` key;
3. the pilot's costume;
4. the outfit, the DEV Hot Dog piece in the top slot included: the costume hides it, as it hides every outfit piece.

The Sheldon Head hat on a costumed pilot draws as on the voxel figure: the voxel hood it was made for.

**What the costume hides and keeps** (a table in NativeVoxelCostume; CostumeChecks cover every row):

| Piece class | On a costumed pilot |
| --- | --- |
| Skinned outfit pieces (hair, top, bottoms, hands, shoes) and cloth | Hidden |
| The robot suit's rigid plates (about 16k triangles) | Hidden |
| `face:` gear (helm, half, both, visor, mask) | Hidden; the costume's head block is the face |
| Skin, eyes, lashes, brows and teeth | Hidden |
| Hats | Kept, at scale 1 on the costume's head through a costume hat fit (like NativeHotDog.HatFit). Model hats use the same fit |
| Pilot gear (the chest core, the wrist device) | Kept, at the costume's own anchors |
| Body chips, armour lights, team bands | Kept, at the costume's anchors |
| The Katana's saya; the Bombadier's belt and satchel | Kept, at the costume's hip and chest anchors |
| Held weapons | The pilot's picks at the model's hands. A Prototype draws the voxel weapon there, as today |
| W3's harness grips and the pod seat | The model's hands and seat, because the costume rides the model's skeleton |

**The attachment table:**
- It is measured once per variant and tier from the costume's built mesh: the chest core, wrist device, chip anchors, saya, belt and satchel, hat band, halos and bands. Each is a bone and an offset on the costume's surface.
- CostumeChecks hold every attachment within 1 cm of the costume's surface.
- The precedent: the owner had to report the Hot Dog's chest core sitting under the sausage's mouth (DEV_LOG.md:2696-2702).

**The capsule check:** at the idle and run poses, the costume's chest, hip and head blocks stay within the hit capsule's radius (.42 m) plus 2 cm, so a hit never lands on air beside a block.

**How it is built under O1(a)** (Core `NativeVoxelCostume`, engine-free; Runtime `NativeVoxelCostumePresentation`):
- **When:** once per variant and tier, on a worker, at first use. It is prewarmed when a seat's look changes or a map loads.
- **From what:** the 48 kept parts. Prototype is SourceModels `pilot`; Standard and Sheldon are the NativeBodyModels sculpts at Full, Reduced and Low.
- **Skinning:** every vertex is weighted 100% to its joint's mapped model bone (VisualJointsOf). The bind poses are read off the body's mesh, the Hot Dog's method.
- **The fit:** the one the owner picks, measured once from the bake's rest skeleton (segment lengths, the shoulder and hip lines, the head bone).
- **Colour:**
  - the voxel vertex colours go into a small palette texture, read through the toon shader's existing properties and tinted by the pilot's colours as the voxel bodies are;
  - the body and accent tags drive the accent-glow channel;
  - no new shader keyword (the Windows shader compiler is fragile); the builder's shader-compile pass runs before the merge.
- **Shader:** the model's toon shader, so the outline, hit flash, spawn-in, dissolve, cuts and wound pits are the body's own.

**How it is drawn under O1(a):**
- **Where:** VisualDress wears it in place of the outfit, per the table above. So every route that dresses a model body draws it, as with the Hot Dog: the live pilot, first-person forearms, corpses and their cuts, the draft, the bust, portraits, the Locker, the deploy cast and the eject, the rooftop, killcams and replays.
- **Death:** as the sheet's death question decides. Voxel breakup cuts its cubes from the costume's own blocks.
- **Cost:** one skinned mesh (10,000, 6,000 and 3,200 triangles at most per tier, the voxel skins' budgets), with no cloth and the outfit hidden. That is cheaper than a dressed model.
- **Without the bake,** the pilot draws the voxel style in the same variant.

**Under O1(b) instead:**
- ModelBody(a) is false for a costumed seat, which draws today's voxel pilot on the rig path in its variant, with its twins, corpse, bisect and carve. V3, V6 and V12 keep those per seat instead of per viewer.
- The derived pose or the offline bake (M11) gives it the model-only moves.
- The save, sync and precedence rules above are the same.

**What Astra adds:**
- VA1: the Worker resync, with the costume key approved and mirrored;
- a grant key, only if O2 makes the costume an unlock;
- nothing on the wire or in the rules.

### 2.4 The voxel style: kept, self-contained, loadable

**What it is:** the kept voxel data plus the legacy rig path, as one optional style. Builds without the bake draw it by default; a map, a mode or a DEV switch can turn it on.

**Its data stays exactly where it is, byte-identical:**
- SourceModels.json, all 140 models: `pilot`, `weapon-*`, `loose-*`, `hat-*`, `horde-*`, `summon-*`, `map-*`, `ward-*`, `stage-*`, `death-*`;
- SourceMotion, SceneMotion, WoundParts, HazardModels, SceneryMotion, WardMotion, SlenderRig and SirenLook;
- CapturedClips.Generated, NativeKillBoxes.Generated, NativeBroadcastEdges.Generated and the 11 NativePack*.Generated rig tracks;
- the lobby-pose port, and MapLooks' legacy rows;
- every exporter and provenance file; scripts/mocap; the Electron source.

**Its code stays at its paths.** Files are not moved, because moving them breaks source-text pins and Unity GUIDs:
- CapturedRig and CapturedAnimation, the style's skeleton and player;
- SourceCharacter's voxel branch;
- NativeBodyModels, NativeBodySkins and NativeBodySkinModels;
- the voxel horde kinds, with the rig-descriptor gait (Legs=1), and the voxel gore surface;
- the voxel twins (spawn-in, ghosts, revive cells, halos, bands);
- the voxel corpse, bisect and carve;
- NativePortraits;
- the scripted vent victim;
- KillBurst;
- NativeDebrisLook's cube style;
- the box scenery builders and the legacy `map-<id>` draw;
- the unused M1 stage kit.

**The switchboard,** `Core/NativeVoxelStyle.cs`:
- `Bodies(map, flags)`: none, costume or classic;
- `Weapons(map)`;
- `Debris(map)`, the existing NativeDebrisLook;
- `Zombies(map, flags)`;
- `Deaths(map)`;
- `World(map)`: bank, dressed blocks, or classic voxel blocks.
- The effect twins follow the body path: they draw wherever the voxel style draws a body (classic bodies, no bake, the DEV override).
- It is the only place that answers "is voxel on here?". Every voxel branch asks it, and the ratchet enforces that.

**The map switch:**
- MapLook gains `bodies`, `zombies`, `deaths` and `world` beside `debris`. They are presentation-only map data that every peer shares; map looks are in no compatibility hash.
- A map with every key set to voxel is today's voxel game. With `bodies:"costume"` instead, it is the new game in voxel dress.
- MapLooks.json is asset-locked but not generated: its rows are edited by hand or by a map's own script (`docs/arenas/scripts/expedition_city.py` writes the city's), and the expedition generator only rehashes its lock entry. So each change refreshes that entry in the same commit.
- A playable voxel game mode (a room option, its rotation and content revision) is Astra's (VA5). It feeds the same switchboard.

**It stays loadable:**
- **From V0,** VoxelStyleChecks' first half runs in every CoreTests run:
  - digests of every kept data file (SirenLook.json included) and every generated table above;
  - an engine-free build and pose of every voxel sculpt, horde kit and rig clip.
- **Every stage's acceptance** includes a `--ca-voxel-bodies` BoZo capture: the voxel style drawn, beside the model.
- **From V12,** the second half: a map with the voxel keys plays on host and guest.
- The Editor's bank-completeness checks stay. A DEV preview turns the style on for any map.
- **Lazy builds:** with the bake present, voxel-only meshes build on first use, prewarmed at map load or seat change, but only once nothing in normal play draws them: the far crowd from Stage 4, the gore surface from Stage 7. SourceModels' legacy maps build only when a classic world asks for them.

**What moves:**
- the model paths stop needing a voxel body built (V1);
- an instanced effects drawer splits off the bank (V12);
- NativeLighting's clock call moves out of the SceneryMotion loader (V12);
- the dead legacy trims are skipped on dressed maps, inside Claude's own file (V13).

**What stays exactly:**
- every data file and exporter;
- the Electron runtime;
- the parity fixtures;
- the debris switch;
- the frozen authored town (overhaul Q11);
- the unused M1 kit.

### 2.5 The 22-joint rig after the retirement
- **The wire (Astra), unchanged:**
  - joint ids 0-21 in wounds and killcam causes;
  - the wound's capsule frame;
  - Actor.Position = body − .865;
  - the death records' bank tokens;
  - the capsule and the head box.
- **Choices that host, guests and killcam must agree on** stay pure functions of replicated data and rest tables:
  - the part a blade takes (M10's per-outfit rest table);
  - the killcam's cause line, built from the slice plan;
  - wound placement, from the wound record;
  - the LOD tier.
  - A drawn pose depends on each client's clock, pose smoothing and pose throttle, so it never feeds them.
- **Derived (Claude), cosmetic only:** each frame, the drawn model's pose is reduced to 22 joint frames (VisualRigFromDrawn, generalised) for effects keyed by joint id: anchors, trails, the arrow's drawn seat, corpse starts. The joint-frame resolver (M6, V1) replaces direct `rig.Bones[` reads.
- **The voxel style:** the rig keeps posing voxel bodies from its own clips: the no-bake fallback, the DEV switch, classic maps, and O1(b)'s costumed seats.

---

## 3. Work packages

Each package is its own task branch from shared `origin/claude/sleepy-ritchie-g8ruhv` (the newest Unity integration head), with a PR into shared.

**Before merging:** the full CoreTests, `python tools/check_unity_compile.py` and `git diff --check`.

**Every package:**
- re-pins its checks in the same commit as its change, because CI runs the full CoreTests on every push;
- logs in DEV_LOG its animation impact, a "Voxel dependency" line, and the ratchet pins it changed;
- shows before-and-after BoZo captures, plus 2-4 side-by-side review clips (5-10 s, under 8 MB, half speed for fast motion, like compared with like);
- adds a `--ca-voxel-bodies` capture of the same moment, so the voxel style is seen to still draw.

### V0. No new voxel (Claude) [Stage 0, with M1]
- **The ratchet,** `Tests/VoxelRatchetChecks`. It pins per file, in Core and Runtime files outside the voxel-style list, the counts of five token families:
  - **voxel gates:** `ClassicBodies` (25 today), `VisualBodiesOn` (18);
  - **voxel measures:** `CapturedRig.Boxes` (23); `WoundedPilot(` (the six callers, already pinned by WoundSkinChecks:156-157);
  - **rig reads:** `rig.Bones[` (163 in 59 Runtime files), `viewRig.Bones[` (13), and the other rig names (`introRig`, `draftRig`, `hordeQueueRig`, `Rigs[`, `Player.Bones[`);
  - **rig authoring:**
    - `new CapturedRig.Pose(` (44 in 29 files), `new MeleeRig(` (20 in 14), `.AddE(` (59 in 10), `.WritePose(` (28 in 12), `CapturedRig.Sample(` (12);
    - pose edits by CapturedRig index: `.Rotation[CapturedRig.` (220), `.Position[CapturedRig.` (92), `.P[CapturedRig.` (80), `.Q[CapturedRig.` (26);
  - **voxel-frame draws:** `WeaponShape.Cube` pieces (228 in 25 files), `QueueHeld(`, and direct voxel-id queues (`Queue("weapon-`, `"loose-`, `"hat-`, `"pilot`, `"horde-`, `"summon-`).
  - **How it fails:** a count above its pin fails, and a new file starts at zero. The message names the model-native route: the resolver, the model's motion bank, VisualWeapons and WeaponAt, a mesh bank.
  - **Tokens, not types:** an engine-free CoreTests check cannot resolve C# types without a new compiler dependency. So the rule is that new rig reads go only through the resolver, and per-file pins catch the rest.
  - Each package lowers the pins on purpose.
- **The voxel draw counter** (DEV builds and the Editor):
  - it counts voxel-bank draws per frame by category: pilot bodies, voxel weapons, voxel hats, horde tiers, kill cubes, wound cubes, revive cells, breach cubes, and primitive pieces (QueuePiece cubes);
  - it shows in the DEV overlay and is logged by BoZo captures;
  - with the bake, no costume and no Prototype picks, every category reads 0 except the allowed ones: the Siren until V11, the Cyber Lord's data cubes, and primitive pieces until V3 and V5 replace them. Every stage's acceptance uses it.
- **VoxelStyleChecks, first half** (2.4): the digests, and the engine-free builds and poses.
- **The new-content rule,** in `docs/VOXEL-STYLE.md`:
  - new content is authored and socketed on the model, as NOCTIEL, THE HUM, THE DEEP END, the boss relics and Thunderheart Carillon are;
  - the 1H audit is its baseline;
  - any voxel read a package leaves is named in its DEV_LOG entry;
  - the owner's direction is quoted and dated in two lines in AGENTS.md, which both agents read at every start, pointing to the doc; DEVELOPMENT.md points to it too.
- **Text:**
  - the overhaul amendments (4.2), presented to the owner with this plan, not only in the DEV_LOG;
  - DESIGN_INTENT.md:9, quoting the owner's direction;
  - the stale VBP header (:7-17, :83-85).
- **Fixes (P9):** the deploy cast's and the rooftop's weapons go through the seat's skin; pack weapons draw on every model body; DEV specs map through NativeDevMotions.Base. They land after W3 merges, on their own branch, or through W3's session (one writer per branch).
- **Measurements** (W0's capture, extended):
  - 8 pilots in PvP, on the model and with Classic;
  - 4 pilots and 88 zombies in the hub, with and without `--ca-voxel-zombies`;
  - each at High, Balanced and Low;
  - in a Development Mono player, the shipped configuration, with `PERF_SAMPLE` p95 and p99 and `PERF_HITCH` (`Runtime/NativeHitchDiagnostics.cs:72-74`). The Editor run goes once beside it, to record the Editor-to-player ratio;
  - a low-end run for O3, on a PC the owner names or a constrained stand-in (the player held to 4 cores, at 1920×1080 and Low).
  - The skinning question is already answered (the project skins on the CPU); V15 compares the alternatives.
  - If a baseline is over its target, Claude reports it to the owner before any overhaul code merges, as the overhaul requires (OVH:937).
- **Acceptance:**
  - the ratchet and VoxelStyleChecks' first half run in the full CoreTests;
  - the ratchet's baseline names W3's eject states as rig-authored (M12), and V4 owns them;
  - DeployModelChecks, RooftopChecks and VisualRigChecks:1748 are re-pinned with P9;
  - the counter reads 0 for pilot bodies and voxel weapons on the Deployment Station's pads;
  - the measurements are in the overhaul's budget table (`docs/EXPEDITION-DEPLOYMENT.md`), naming the machine, the tier and the settings.
- **Captures:** the pads with the DEV Greatsword and each pack weapon, before and after; the frame table.

### V1. One body predicate and the joint-frame resolver (Claude) [Stage 1]
- **The predicate:** NativeBodyStyle and `ModelBody(a)` (2.2) replace the gates in P2.
- **The resolver (M6):** `JointFrame(a, j)`, VisualJointFrame generalised to every actor. New reads go through it; V3, V7 and the status markers use it first.
- **Hidden gates out:** the model paths stop needing a voxel body built:
  - the 8 `sourceModels` returns in VBP become checks for the drawer, not for voxel bodies;
  - ZombieModelsOn's gate;
  - the echoes snapshot the drawn model (`NativeWeaponFxPresentation.cs:270`, `NativeDevFighterEffects.cs:253`);
  - HordeKindsReady goes in V7 part 2.
- **Settings · Player model,** per O3, and only in the same release as V2:
  - `prefs.playerModel` is still parsed;
  - a saved "Classic" reads as New, with a one-time line: "Voxel is a costume now: Locker · Costume";
  - the DEV panel switch and `--ca-voxel-bodies` set the override;
  - if the low-end run fails, the row stays, relabelled "Low-end bodies (this PC)".
- **Contract:** no wire, rule or save change.
- **Acceptance:**
  - `ClassicBodies` falls from 25 to 2 at most (the override and its setter), plus the relabelled row's uses if it stays;
  - BodyStyleChecks cover every case in 2.2;
  - a headless check shows that the model draws with the voxel pilot set unbuilt (the bank stays the drawer until V12);
  - JointFrameChecks: the model's joint when drawn, the rig's otherwise;
  - re-pinned in the same commits: VisualRigChecks:123, 311, 398-400, 1669, 1737-1777 and 1806-1823; ModelPoseChecks:410-412; LockerChecks:106,130; BodySkinChecks:288; ZombieModelChecks:424; BodyChipModelChecks:160; SpawnInChecks:346.
- **Captures:**
  - the same arena moment with the override off and on;
  - a Siren pilot among model pilots;
  - a `--ca-voxel-bodies` run as the no-bake stand-in.

### V2. The voxel costume (Claude; Astra's VA1) [Stage 1, after O1]
- **Scope:**
  - 2.3, under the owner's O1 choice;
  - NativeLook's costume key and the KeepSkin step;
  - Core NativeVoxelCostume and Runtime NativeVoxelCostumePresentation (under O1(a));
  - the hide-and-keep table, the attachment table and the hat fit;
  - the review sheet;
  - the Locker's COSTUME card, and the voxel set with its restore;
  - the relabelled text (U6);
  - the precedence and blind-pick rules;
  - the release-note lines.
- **Not in scope:** a new DEV gate (the existing `body:sheldon` gate holds the DEV variant), and the build check (VA2).
- **Acceptance:**
  - **CostumeChecks:**
    - every vertex weighted 100% to one bone, and all 48 parts mapped (under O1(a));
    - the fit is deterministic, and each tier keeps its triangle budget;
    - the look round-trips: prefs, KeepSkin at a login whose saved look lacks the key, the host's gate, the seat frame, replay;
    - an old client's normaliser drops the key; Slots stays five (RobotSuitChecks:75-78 and ZombieModelChecks:120 kept exactly);
    - SameOutfit differs when only the costume differs;
    - every hide-and-keep row; every attachment within 1 cm of the costume's surface; the capsule check;
    - precedence (Top Dog, the DEV Hot Dog piece, a map's `bodies`), blind pick, and the voxel set's restore.
  - LookContractChecks and `Editor/NativeNetworkBuildChecks.cs:48-79` are re-pinned; the frame bounds (:199-205) are unchanged.
  - Hats sit within 1 cm of their band.
  - The counter shows costume parts only on costumed pilots.
  - A costumed pilot costs no more than a dressed one (V0's harness).
  - The builder's shader-compile pass is clean.
  - The owner approves the sheet before the stage ships.
- **Captures and clips:**
  - the sheet;
  - a 4-pilot match with one costumed pilot, seen by host and guest, with its killcam and a spectator's view;
  - first person per weapon;
  - the Locker card;
  - a split corpse; Voxel breakup;
  - the deploy pads, the pod seat, the harness and the climb-out; the rooftop;
  - clips: idle, run and attack on the model against the costume; a death.

### V3. Voxel leftovers on model pilots (Claude) [Stages 1-2]
- **Stage 1:**
  - live ghosts' weapons (P12);
  - status markers, through the resolver (P13);
  - the Sheldon hood on the Hot Dog, and the Halo (P11);
  - the lobby chair (U5);
  - the Sentry Drop's gun through WeaponAt (1H).
- **Stage 2:**
  - wound chips as panel shards and embers as streaks (D4's effects);
  - the crushed head's model hat (P11);
  - the vent dust (D8);
  - the Cyber Lord's shimmer on the model's skin (P14);
  - modelled props for the Bombadier (mines, detonator, belt, satchel) and the techniques (tripod, bipod, stakes, ammo crate) (1H);
  - the body-chip pieces (1H).
- **Acceptance:**
  - the counter reads 0 on model pilots through a scripted sweep: every status, a Demon Step, a dash, a crush, a vent fall, bruises and burns, a Bombadier round and each technique;
  - the ratchet's cube pins fall in those files;
  - re-pinned: VisualRigChecks (Echoes, BodyFx), StatusVisualChecks, HatLookChecks, ModelHatChecks, KillEffectChecks, DeathFxChecks:62, and the Bombadier's and techniques' presentation checks;
  - the revive's voxel path stays pinned (ReviveHoloChecks, interaction-parity.json); its model glints are V6's.
- **Captures:** each item, before and after.

### V4. Menus and the eject on the model (Claude) [Stage 2]
- **Scope:**
  - portraits framed from the model (U1);
  - the draft's stage pose and framing (M9);
  - the rooftop and the broadcast (U2);
  - the Locker's art (U3);
  - the deploy cast, with its voxel branch chosen by the predicate (U4, after W3 merges);
  - the four lobby poses and the DEV sheldon-idle re-authored on the model's motion bank, with the deploy cast playing them (M7). The match idles are untouched;
  - W3's eject states (M12): the crouch and the fall from the model's own Jump and Fall clips (every set carries them); the seat as a model pose, with the model's hand IK on the harness bar in the pod's frame; the rig versions kept for the voxel style.
- **Acceptance:**
  - LobbyPoseChecks gain model fixtures; the voxel fixtures are kept for the style;
  - W3's eject checks are re-pinned with the eject's timing unchanged, and the ratchet's rig-authoring pins fall in NativeDeployEject;
  - re-pinned: DraftStageChecks:43, DraftPlantChecks, PortraitBackChecks, RooftopChecks, DeployPresentationChecks, DeployModelChecks;
  - the counter reads 0 across the menus and the eject.
- **Captures:**
  - the Locker turntable and the home card;
  - lobby and results cards with 8 pilots;
  - the draft at 1 and at 12 chips;
  - each lobby pose, the broadcast and the rooftop;
  - the eject at 0.1 s steps, on the model and on the costume, and a clip of it with one costumed pilot.

### V5. Entrances on the model (Claude) [Stage 4]
- **Scope (M8 and F14):**
  - every entrance's tracks play whole on the model's motion bank (the precedent: `NativeIntros.cs:209-211`, `Core/NativeBladeIntros.cs`);
  - clearances from V16's proxy table;
  - the stage particles become VFX, and the 48 cube pieces become modelled props;
  - Grid Rez and the reveal become a build front in the model's shader, as the spawn-in already is;
  - the DEV voxel stages are re-staged, with modelled sets.
- **Acceptance:**
  - 4.8 s, unchanged;
  - no forearm or weapon inside the torso or head through any clip, measured on V16's capsules;
  - re-pinned: IntroMotionChecks:24-30, PackIntroChecks:188,219, IntroPresentationChecks, EntranceDirectorChecks, SirenIntroChecks:152, BodyRevealChecks:8,34;
  - SceneMotion untouched (test_cinematics.py), and the Editor still has its 31 scenes.
- **Captures:** each entrance at 0.1 s steps, on the model and on the costume, with one clip per entrance family.

### V6. After death on the model (Claude) [Stages 3-4]
- **Stage 3:**
  - D4: wounds placed from the record, with no carve for model bodies;
  - D11: the revive's repair glints from D4's hole list;
  - D3: the kill wound from the model;
  - D5: the per-seat gate;
  - D6: the drawn weapon in death scenes;
  - D7: the Siren's victim on the model;
  - D12: the corpse-start cache;
  - D10: arrows on pilots and on zombies.
- **Stage 4:**
  - D1: the ragdoll from V16's capsules, halves included;
  - D2: halves planned from the model's cut;
  - M10: the per-outfit rest table from V16.
- **Contract:** everything is a pure function of replicated data (the death seed, the wound record, `seat.look`) and rest tables, so host, guests, late joiners and killcams agree. The 22 joint ids remain the mapping.
- **Acceptance:**
  - WoundedPilot callers fall from 6 to 3 at most, in the voxel style only;
  - no KillCaps on model corpses;
  - the voxel Split never runs for a model body (counted), and the main-thread time it saved is measured;
  - MeleeBisectChecks and BladeChecks stay deterministic across the default outfit, the robot, the Hot Dog, the robe and the costume;
  - CorpseChecks, CollapseChecks, BisectChecks, GibChecks, KillEffectChecks, ReviveHoloChecks (with model cases) and kill-effect-parity.json pass, with the voxel cases kept;
  - the "floating blood" case is checked against the owner's still.
- **Captures:**
  - every kill style on the model, the Hot Dog and the costume;
  - a through-cut settling; a crush; a revive;
  - the same corpse from a late joiner and from the killcam;
  - a first-person death.

### V7. The crowd on the model (Claude; Astra's numbers) [Stages 2-4]
- **Part 1, Stage 2** (with W7's district outfits in M2):
  - E11: a pool miss hides the add for one frame;
  - E19: prewarm by district, on a worker.
- **Part 2, Stage 3** (after V15's go/no-go):
  - E4: the gate and the boot split. The voxel kinds stay built while they draw and while they are the gore surface;
  - E5: pinning, with far detail widened to every pinned kind, a prewarm and the make-limit bypass. Pinned adds stay Full at any count until part 3;
  - E12: the galleries; E9: the sockets; E14: the cream seams; E8: the anchors;
  - E18: the model capsule-fit check.
- **Part 3, Stage 4** (with W11a):
  - E3: the model LODs (below);
  - E20: animation LOD;
  - E6: the Hulk pinned, with a coarse growth LOD;
  - the voxel far tiers stop drawing when the bake is present, and are built lazily for the voxel style from then on.
- **The LOD route:**
  - LOD meshes per kind silhouette (male and female per kind), not per variant, authored offline in the tools/gear Blender pipeline into the local bank, and counted against the zip cap;
  - one mesh per LOD, with 1-2 joint weights per vertex, skinned in the vertex shader from a per-instance 22-matrix palette: one instanced draw per LOD model, with clothes and skin tinted per instance;
  - a decimated cut of the bake's pieces (about 12k triangles), authored with the LODs, for the Full tier at Low and for the near band;
  - the near band: adds within about 12 m stay skinned models past the Full count (at most 8 more at High and Balanced, 4 at Low), on the decimated cut;
  - posed by a cheap model-proportion leg solve (two-bone legs to the gait's spread feet), on strides planned for model legs on every tier, so a tier change never moves the feet;
  - dithered across the 3 m hysteresis, keeping a rim at night (overhaul risk 5).
- **Acceptance:**
  - the budgets below, measured in the player;
  - the counter reads 0 for zombies when the bake is present;
  - CrowdLodChecks: the budgets, joint coverage, deterministic tier choice, no per-frame allocation, LOD data loaded on workers;
  - AnimationLodChecks: every attack's contact frame posed exactly at LastAttack+Strike, never skipped by the stagger; impostor cycles chosen by speed;
  - HordeKindChecks' budget lines and eager-build line re-pinned, with ZombieLit kept bit-identical through W1;
  - ZombieGoreChecks for corpses;
  - station-enemies-baseline.txt rebased on purpose.
- **Captures and clips:**
  - the hub from 46 m with 88 zombies, at High, Balanced and Low, with an LOD tint;
  - the 3 m crossfade, and the near band in a dense horde;
  - HULKAMANIA's six Hulks;
  - Level 0's Facelings and Hounds arriving, with no invisible frames;
  - W11a's Hounds in the city;
  - a ranged kill at 60 m leaving a model corpse.

### V8. The zombie gait on the model (Claude) [Stage 5, its own release before M5; gore in Stage 7]
- **Scope:** E1, E2 and E7; then E10, the gore on the model, in Stage 7.
- **Quality work, not a performance package:**
  - no saving is claimed: the binding it replaces costs 5.7-8.4 µs an add, and a model-native gait still needs the same leg IK;
  - it ships in its own release, with the old gait behind a DEV A/B switch for that release, and the crowd baseline re-measured after.
- **Contract:**
  - the authority is unchanged;
  - blows land at LastAttack+Strike, hands stay within Radius+.26, and the head stays inside the part bands;
  - footfall cue moments may shift, and are logged;
  - the voxel descriptor reproduces today's voxel motion bit-identically.
- **Acceptance:**
  - **ModelGaitChecks** on the Measured skeleton:
    - hips over the soles, and the knee off its foot;
    - planted ankles slipping no more than .4 cm a frame;
    - cadence;
    - the 238 run-turn routes no worse than the 2026-11-18 numbers;
  - the existing gait checks are kept as the voxel descriptor's;
  - CPU no worse than V15's measured numbers;
  - an A/B in the DEV compare.
- **Captures and clips** (half speed):
  - the run-turn Editor moments re-shot: shambler, runner, brute;
  - the Hound's gallop and pin;
  - the Hulk's carry;
  - the owner-reviewed strips 4b-4f, 4m and 4o.

### V9. Pilot poses on the model (Claude) [Stages 5 and 7]
- **Stage 5:**
  - M1's remaining pilot states: specials, uncovered kit moves (the ranged ability gestures and the gun kits among them), knockdowns and juggles;
  - through a model-native retarget path (M2) that leaves scripts/mocap untouched;
  - standing on V16's sole floor;
  - M3's constants retired with their states.
- **Stage 7:**
  - first person on the model (P10), with ADS;
  - the Cyber Lord's set (P14).
- **Acceptance:**
  - the list of rig-copied pilot states is empty, or each remaining state is named in the DEV_LOG;
  - the ratchet's rig-read and rig-authoring pins well under today's;
  - re-pinned: AnimationChecks:31,67, MeleeFeelChecks:300, PackReactionChecks:233, SprintAttackPoseChecks:130, `Core/NativeSpecialChecks.cs:79`;
  - hit windows unchanged.
- **Captures:**
  - each special, knockdown, juggle and kit move per class, on the model and the costume;
  - first person per weapon, with ADS.

### V10. Weapon sockets on the life-size models (Claude) [Stage 3]
- **Scope (P8):**
  - per-weapon socket tables on VisualWeapons and the pack bank: muzzle, blade tip and base, support, stock, sight, module mounts;
  - the newer Standard designs (the Glaive, the Katana and its saya, the Fangs, the TNT) included;
  - the effects read them;
  - the Prototype keeps its own sockets.
- **Acceptance:**
  - tracers still converge within 4.5 m;
  - each socket lies within 1 cm of its drawn feature;
  - chip modules sit on the bake's weapons and the pack's;
  - WeaponSkinChecks pass.
- **Captures:** each weapon's muzzle flash, trail and modules.

### V11. Bodies of their own for the Siren, the Slenderman and the summons (Claude) [Stages 7-8]
- **Scope (P15, D5 and E16):**
  - each body built through the boss-bank pipeline: modelled in Blender on its own joints, into a local bank;
  - the summons on the model, and the Slenderman's own body, in Stage 7;
  - the Siren's look, smile, claw, crush, ink arms, intro and deaths re-anchored on her own body, in Stage 8 (O5).
- **Acceptance:**
  - the siren parity fixtures are kept for the voxel style, and new model checks are added;
  - captures of Ward Zero's fight and of the final court.

### V12. The voxel style, packaged (Claude; Astra's VA5) [groundwork from Stage 1, finished in Stage 8]
- **Scope:**
  - the switchboard and its six answers;
  - the map keys (`bodies`, `zombies`, `deaths`, `world`), each with its lock refresh;
  - the DEV preview;
  - VoxelStyleChecks' second half;
  - lazy builds for P5's skins, E4's kinds (once V7 part 3 stops drawing them), F12's maps and G6's split;
  - the clock call moved (F12);
  - `docs/VOXEL-STYLE.md` completed;
  - the ratchet at its floor.
  - No deletions.
- **Acceptance:**
  - a map with every voxel key plays on host and guest: classic pilots with their twins and corpses, voxel zombies, voxel debris, the classic world;
  - the same map with `bodies:"costume"`;
  - a `--ca-voxel-bodies` run draws today's voxel style;
  - boot time is measured before and after;
  - kept or re-pinned: WardZeroChecks:256-257, MapLookChecks:11,69, DebrisChecks:8,21-31, NewStageLookChecks:10-61, HordeKindChecks:269 and the Editor bank checks.
- **Captures:** the voxel map on host and guest, with both body values; the boot timeline.

### V13. World box leftovers (Claude) [Stages 2-8, alongside W8 and W10]
- **Stage 2:**
  - E13: the breach pieces;
  - F10: Cyber City's tell and the Backrooms blocks;
  - F11: the dead trims, returning early inside Claude's NativeLighting.cs.
- **Stage 4:**
  - F7: the Central Station and City Ironworks banks, landing before W7's M4 set pieces for those zones;
  - F8: fixture tables, map by map, as each bank lands.
- **Stage 6:** F9, the light volume, with W10; F5, the backdrops, with W8c and W10; F4, the lamps.
- **Stage 7:** F10's stubs, state pieces and alarms; F3, the pose records.
- **Stage 8:** F6, the menus; F1, BLOCK_PACK's own path.
- **Acceptance:**
  - per item, with `zfight.py` and the bank checks;
  - the fallback keeps drawing in public builds.

### V14. Death scenes and the voxel-era kill styles (Claude) [Stages 3-8, per the default in 8.2]
- **Scope:**
  - D6: the props and victims remodelled, effect by effect: Confetti cannon, Beam me up, the DEV clown car, Clown Love, Bass Van, and the Raging Demon's screen;
  - D9: Voxel breakup stays voxel.
- **Acceptance:**
  - every kill style plays on the model and on the costume;
  - SceneMotion is untouched;
  - KillEffectChecks pass.

### V15. The Full tier's cost (Claude; Astra's VA4) [Stage 2; go/no-go before Stage 3]
- **Scope (E21):**
  - a GPU-against-CPU skinning A/B in the player (CPU, GPU, GPU batched) at each tier; Astra commits the setting the numbers pick (VA4);
  - each zombie body's renderers merged at make into 1-3 skinned meshes (an atlas where materials differ only in their textures), with its fixed body and face shape weights baked in; only the animated face shapes stay live;
  - past about 15 m: 2-bone, then 1-bone skinning, and posing at half rate except inside a blow's window;
  - Full-tier shadows only within about 20-25 m;
  - the Full counts each tier affords, measured in the player (expected about 16, 12 and 8). They are applied only with V7 part 3, when the near band and the LODs take over from the voxel tiers; until then the counts stay 24 and 16, so no more adds draw voxel than today;
  - the go/no-go numbers written into the budget table before Stage 3's code merges.
- **Acceptance:**
  - ZombieModelChecks re-pinned: covers, wound pits, cuts, tints and hides survive the merge;
  - FullTierChecks: the merge is deterministic per variant; the shape bakes match the live shapes at their fixed weights; the skin-quality and shadow bands switch at their distances, with hysteresis;
  - the player-measured crowd cost per tier against V0's baseline, recorded;
  - no visible change at near range (A/B captures).
- **Captures:** 24 zombies near the camera, before and after, at each tier; the shadow band's edge; the 15 m skin-quality band.

### V16. Body proxies from the model (Claude) [Stage 3]
- **Scope (M4, M5):**
  - per-bone capsules measured from the skinned mesh, per bake, outfit and costume variant, built at load on a worker;
  - the floor from the model's soles;
  - one table that D1, D10, M10, V5 and the costume read.
- **Acceptance:**
  - BodyProxyChecks: every capsule lies inside the mesh's skin within a tolerance and covers it; the table is deterministic per bake and outfit;
  - poses stay on the floor within 1 cm through the clip sweep;
  - the voxel boxes path is kept for the voxel style.
- **Captures:** the capsules drawn over the model, the Hot Dog, the robe and the costume; a roll and a death on the floor.

### Astra's packages
The full text is in section 7.
- **VA1:** the account Worker resync and the costume key. The key is approved before Stage 1 merges; the deploy is owner-authorized.
- **VA2:** the builder refuses a test-channel build without the bake, through its Editor check (`-requireVisualBody`).
- **VA3:** the contracts kept, and the numbers the crowd budget needs.
- **VA4:** the skinning setting, once V15's A/B picks one.
- **VA5:** later, only on the owner's word, a voxel game mode or a playable voxel-classic map.
- **For information:** the dead trims change is made in Claude's own file; a new kind ships only with Claude's model look.

### Budgets: crowds and pilots
These are proposed. They are measured in the Development Mono player (V0, V15), not the Editor; the .NET 8 figures are only a guide.

| Item | Budget | Basis |
| --- | --- | --- |
| Frame, 4 pilots and 88 zombies | The overhaul's targets by M7: p95 12, 10 and 8 ms at High, Balanced and Low. Stages 3 and 4 have absolute gates (4.1), not a share of a headroom that may already be negative | OVH section 6; appendix B, F14 |
| Full bodies | Measured in V15 (expected about 16, 12 and 8) and applied in Stage 4 with the LODs; 24 and 16 until then. From Stage 4, pinned kinds count against them | HKP:76; E21 |
| Full-tier detail | 1-3 skinned meshes a body, fixed shapes baked; 2-bone skinning and half-rate posing past about 15 m, except in blow windows; shadows within about 20-25 m | V15 |
| The near band (within about 12 m, past the Full count) | At most 8 more bodies at High and Balanced, 4 at Low, the nearest first; on a decimated cut of the bake's pieces (about 12k triangles) at 1-2 bones, with no live face shapes, no shadows and a half-rate pose. Past the cap, the rest draw LOD1 | E3 |
| LOD1 (to 40 m) | 3,000 triangles at most; one palette-skinned instanced draw per kind silhouette; per-instance tints | The voxel tier's budget, `Core/NativeHordeModels.cs:62` |
| LOD2 (past 40 m) | 600 triangles at most; cached cycle poses; no IK | The same |
| Crowd triangles a view | High: 16 Full × about 29k (0.46 M) + 8 near-band × 12k (0.10 M) + 64 LOD × 3k (0.19 M) ≈ 0.75 M. Low: 8 Full on the decimated cut × 12k (0.10 M) + 4 near-band × 12k (0.05 M) + 76 LODs at 3k or 600 (0.10-0.15 M) ≈ 0.25-0.30 M. Four pilots add 0.13-0.17 M (17-26k each, plus 16.3k for the robot set). Both stay inside the overhaul's 2.0 M and 0.8 M for the whole view. Until Stage 4, Low's Full bodies draw the full meshes (8 × 29k ≈ 0.23 M) | appendix B, F4; docs/BOZO-INTEGRATION.md:199,2213 |
| Crowd draws | 150 at most at High, 80 at Low (inside 1,500 and 800), confirmed by V15. The palette route makes LOD draws one per kind silhouette and tier | E3 |
| Shadow casters | Full bodies within about 20-25 m only | Inside the overhaul's 1,200 |
| Hulks | Up to 6 (HULKAMANIA); a far growth of 8k faces at most | `Core/NativeExpeditionMutators.cs:19-21` |
| Main-thread CPU an add | Set in V15 from the Mono player. On .NET 8 today: gait 7-16 µs, fit and binding 5.7-8.4 µs. LOD adds write no Transforms | docs/ZOMBIE-MOTION.md:142-153; docs/ZOMBIE-LOCOMOTION.md:544,767 |
| Animation LOD | LOD1 posed every other frame, off-screen and LOD2 adds every fourth (staggered); every attack's contact frame posed exactly | E20 |
| LOD memory | 24 MB at most for a run's kind silhouettes, in the local bank, loaded on workers, with no main-thread stall over 2 ms | Proposed |
| Pool misses | Never voxel: hidden for one frame, or the LOD | E11 |
| The Siren's phase two (192 adds) | The Full count unchanged; the LOD tiers and animation LOD carry the rest | `Runtime/NativePve.cs:165` |
| Pilots | 8 model pilots (PvP) and 4 (expedition) within the tier targets; a costumed pilot costs no more than a dressed one | V0 |

---

## 4. Staged test-channel releases

### 4.1 The stages
Every stage is playable and published from the verified shared commit, per the standing instruction.

| Stage | Ships with | Contents | Playable | Gates |
| --- | --- | --- | --- | --- |
| 0 | M1, THE DROP (in progress) | V0 | M1's new start of a run, with no voxel weapons on the Deployment Station's pads | M1's. The player baselines are recorded before any overhaul code merges; if one is over its target, the owner hears first |
| 1, THE COSTUME | Its own release, after M1 and before M2 | V1, V2, V3's first part; VA1's key (and the deploy, if authorized); VA2 | Wear the voxel pilot as a costume every player sees, in killcams and replays too. The Classic row goes if the low-end run passed. No voxel bits on model pilots in play | O1 decided and the sheet approved; O3's low-end run done; VA1's key approved. Without the deploy, the pick lives on that PC, and the release notes say so |
| 2 | M2, NIGHT FALLS | V3's second part, V4, V7 part 1, V13's Stage-2 items, V15 | Menus, portraits, the draft, the lobby poses and the pod eject on the model; modelled props for the Bombadier and the techniques; breaches burst into real pieces; no voxel pop-in when the district outfits arrive; cheaper zombies up close | M2's; V15's go/no-go numbers recorded |
| 3 | M3, BELVEDERE CAMP | V6's first part, V7 part 2, V10, V16, W5's survivors on the model | Model-quality wounds, gibs and revives; the Siren's victim on the model; Backrooms creatures modelled at any distance (pinned at Full); weapon effects on the real models | An absolute crowd gate: the player-measured crowd cost per tier at or below V15's go/no-go numbers. A Windows host and guest run |
| 4 | M4, MODELLED MERIDIAN | V7 part 3, V6's second part, V5, V13's Station and Ironworks banks | No voxel zombie, corpse or limb anywhere; HULKAMANIA on models; entrances on the model; ragdolls and cuts from the model | 4 pilots and 88 zombies within the overhaul's tier targets in the player, or the owner accepts the measured gap (the overhaul's report rule). The difficulty and CPU review alongside W11a |
| 5 | Its own release, after M4 and before M5 | V8 (the old gait behind a DEV A/B switch), V9's first part | Zombies walk on the model's own legs; specials, kit moves and knockdowns on the model | The DEV A/B; ModelGaitChecks; the crowd baseline re-measured after |
| 6 | M5, THE LAYERED CITY | V13's Stage-6 items (the light volume, backdrops, lamps), part of V14 | The layered city's light and skyline built from its models, not boxes | M5's |
| 7 | M6, THE ROOFTOP HORDE | V8's gore, V9's second part, V11's Slenderman and summons, V13's Stage-7 items, part of V14 | Wounds on zombies; first person on the model; the Slenderman and the summons with bodies of their own; W11b's perches authored on the model's gait | M6's |
| 8 | M7, THE LAST CLIMB | V11's Siren, V12, V13's Stage-8 items, the rest of V14, the Low-tier pass | The Siren on her own model at the final court; a voxel-map preview on host and guest; the ratchet at its floor | A 4-player Windows run |

**The order:**
- Stage 1 delivers the owner's explicit ask and carries little risk.
- Stage 2 is the most visible change at low risk, and it makes each Full zombie cheaper before anything else touches the crowd.
- Stage 3 prepares M4's crowd.
- Stage 4 carries the biggest performance risk, so it ships with the overhaul's own crowd review.
- Stage 5 keeps the XL gait in a release of its own, before the layered city and before M6 needs it.
- The packaging and the Siren close the work at M7.

**What cannot slip without delaying the overhaul:**
- V0's amendments, now;
- V7 part 1, with W7's district outfits (M2);
- W5's survivors on the model (M3);
- V15's go/no-go before Stage 3, and V7 parts 2 and 3 before W11a goes wide (M4);
- V8, in its own release before M5, and before M6.

Any other stage may slip.

**The minimum that counts as done,** if capacity runs short:
- the costume (Stage 1);
- no voxel on model pilots (V1, V3, V6's first part: Stage 3);
- the crowd on models (V15 and V7: Stage 4).
- V13 and V14 follow the overhaul's map work. About 18 L or XL items sit beside the overhaul's own packages, so the rest is paced by Claude's capacity (risk 15).

**If the costume sheet is not approved:** Stage 1 ships V1's invisible parts and V3's first part, and the Classic row stays.

### 4.2 What this plan changes in the overhaul
These go to the owner with this plan, are logged in the DEV_LOG with V0, and are amended in `docs/EXPEDITION-OVERHAUL.md` and `docs/EXPEDITION-OVERHAUL-ASTRA.md`:
1. **R8 and W3** (:608, :1068): "so the visual, voxel and Classic bodies all play them" becomes: the model plays the eject. W3's crouch, fall and seat move onto the model's own clips and hand IK in V4 (Stage 2), and the voxel style plays the rig versions without the bake. The cast gains no Classic branch; its voxel branch is chosen by the body predicate (U4, M12, P9).
2. **R6 and W5** (:505, :1167): "a voxel fallback" for survivors is dropped. Without the bake, survivors are not drawn.
3. **W0** (:911): the frame capture adds the 8-pilot PvP pass and the Classic on-and-off pass. It runs in a Development Mono player with `PERF_SAMPLE` and `PERF_HITCH`, beside one Editor run, and adds a low-end run. The checks gain the ratchet, VoxelStyleChecks' first half and the counter (V0).
4. **R2 and W8b** (:190, :1383): modelled dressers in M3 wherever the bank has the stem; the box "kit dressers" only as the public fallback.
5. **W7 and W8** (:156): the Central Station and City Ironworks stage banks do not exist yet. V13 builds them in Stage 4, before W7's M4 set pieces for those zones; until then W7 uses the kits' looks.
6. **W7 in M2** (:1671-1676): the district outfits ship with V7 part 1 (a pool miss hides the add; prewarm by district), so they never pop in as voxel.
7. **W11a** (M4): district kinds in the city need V7 parts 2 and 3 (pinning, prewarm, the LODs) and V15's numbers. Otherwise far Hounds draw as voxel shamblers on all fours, and unprewarmed pinned adds draw nothing for frames. Pinned kinds take Full slots, so Astra's district caps and Claude's tiers are sized together (VA3).
8. **W11b** (M6): perches and drops are authored on the model's gait (V8).
9. **Section 6** (:1755-1790): add the crowd and pilot rows above (3, Budgets). Stages 3 and 4's absolute crowd gates replace the headroom share for the crowd, and the crowd is measured in the player.
10. **M7:** the final court keyed by its boss brings the Siren's own model (O5).
11. **Superseded owner decisions:** add "2026-10-17 'Keep voxel style, just improve it' (zombies)". The Prototype Series' "Keep it voxel" (2026-10-29) and "Model everywhere, classic will be stored as backup" (2026-10-25) stand: this plan fulfils them.

### 4.3 What is still voxel at the end
- by the player's choice: the costume, the Prototype weapons and Voxel breakup;
- by a switch: the voxel style, for builds without the bake, the DEV switches, and a future voxel map or mode (classic bodies, voxel zombies, the classic world, voxel deaths), with the debris switch;
- by standing defaults (8.2): the Cyber Lord's data cubes, and the frozen authored town's box style until the owner retires it;
- under O1(b) only: costumed seats on the rig path.

---

## 5. Tests and build checks to re-pin on purpose
CI runs the full CoreTests on every push, so each re-pin lands in the same commit as its change. Merging into shared also needs `check_unity_compile.py` and `git diff --check` (watch for spaces in Unity meta files).

| Package | Re-pin on purpose | Keep exactly | New |
| --- | --- | --- | --- |
| V0 | DeployModelChecks, RooftopChecks, VisualRigChecks:1748; WeaponSkinChecks (the DEV alias) | — | VoxelRatchetChecks; VoxelStyleChecks (first half); the counter's capture log |
| V1 | VisualRigChecks:123,311,398-400,1669,1737-1777,1806-1823; ModelPoseChecks:410-412; LockerChecks:106,130; BodySkinChecks:288; ZombieModelChecks:424; BodyChipModelChecks:160; SpawnInChecks:346; the station boss body's lines | BodySkinChecks:176-186 (the simulation never reads a body) | BodyStyleChecks; JointFrameChecks; the model-without-voxel headless check |
| V2 | LookContractChecks; `Editor/NativeNetworkBuildChecks.cs:48-79`; LockerChecks; HatLookChecks:125,347-349; ModelHatChecks; WeaponSkinChecks (the voxel set) | WeaponSkinChecks:24,40-41 (the SourceModels SHA); NativeNetworkBuildChecks:199-205 (the frame bounds); RobotSuitChecks:75-78 and ZombieModelChecks:120 (five slots) | CostumeChecks |
| V3 | VisualRigChecks (Echoes, BodyFx); StatusVisualChecks; KillEffectChecks; DeathFxChecks:62; ArmorLightChecks:99; the Bombadier's and techniques' presentation checks | VentFallChecks:74-80 (the voxel victim kept); ReviveHoloChecks and interaction-parity.json (the voxel path) | — |
| V4 | LobbyPoseChecks; DraftStageChecks:43; DraftPlantChecks; PortraitBackChecks; RooftopChecks; DeployPresentationChecks; DeployModelChecks; W3's eject checks | lobby-pose-parity.json (the voxel fixtures) | Model lobby-pose fixtures |
| V5 | IntroMotionChecks:24-30; PackIntroChecks:188,219; IntroPresentationChecks; EntranceDirectorChecks; SirenIntroChecks:152; BodyRevealChecks:8,34 | test_cinematics.py; `Editor/NativeBuildChecks.cs:140` (31 scenes) | Entrance clearance checks on V16's capsules |
| V6 | CorpseChecks; CollapseChecks; BisectChecks:30,86-90,145,204,245; MeleeBisectChecks:321; GibChecks:93-98,286; KillEffectChecks:36,141,195; WoundSkinChecks:115,156-157,160; DeathFxChecks:142; SirenDropChecks; BladeChecks; ReviveHoloChecks (the model cases) | wound-carve-parity.json; collapse-parity.json; kill-effect-parity.json and interaction-parity.json (the voxel cases) | RagdollFitChecks; model bisect-plan checks; WoundPlacementChecks |
| V7 | HordeKindChecks:23,96,269,278-311; ZombieModelChecks; ZombieGoreChecks:162,403,457-458; CrowdPoseChecks; station-enemies-baseline.txt | HordeKindChecks:330-345 (ZombieLit, through W1) and :112-174 (the voxel Inside check) | CrowdLodChecks; AnimationLodChecks; ModelCapsuleFitChecks |
| V8 | ZombieRunTurnChecks; ZombieGaitChecks:15; ZombiePostureChecks:11,51; ZombieLocoChecks; ZombieKnockChecks; ZombieMotionChecks:165; HulkMotionChecks; ModelPoseChecks:34; StationEnemyChecks:244 | The same suites, run on the voxel descriptor | ModelGaitChecks |
| V9 | AnimationChecks:31,67; MeleeFeelChecks:300; PackReactionChecks:233; SprintAttackPoseChecks:130; PackLocomotionChecks; the Cyber Lord checks; `Core/NativeSpecialChecks.cs:79` | scripts/mocap outputs; the pack provenance files | Model retarget checks |
| V10 | WeaponSkinChecks; the chip-module checks; shot-launch convergence | — | Socket checks |
| V11 | The Siren's look, drop and intro checks | siren-*-parity.json | Siren model checks |
| V12 | WardZeroChecks:256-257; MapLookChecks:11,69; DebrisChecks:8,21-31; HordeKindChecks:269; NewStageLookChecks:10-61 | `Editor/NativeBuildChecks.cs:140-159`; tools/asset-lock.json (refreshed with each MapLooks change); `package_source.py:47-52`; the commit check at `NativeSourceModels.cs:30` | VoxelStyleChecks (second half) |
| V13 | StageArtChecks:29-75,102,105,138-163; WardZeroArtChecks:9-11,42-45; CyberCityArtChecks:8,32-44; the Pier, Polar, Skyframe and Causeway ArtChecks; WardLightingChecks:52,89; BlockLookChecks:61-68 | IronworksVisualChecks:241,244 and HordeKindChecks:337 (bit-identical through W1); ExpeditionCityChecks:43-48 (the frozen town) | Bank checks per stage |
| V15 | ZombieModelChecks (the merge); HordeKindChecks' budget lines | — | FullTierChecks |
| V16 | — | The voxel boxes path, for the voxel style | BodyProxyChecks |
| VA1 (Astra) | The relay's account checks | Electron's catalogue | Worker round-trip tests per id, the costume key included |
| Everywhere | — | ParityLogicChecks:60-62 (the head box) | — |

---

## 6. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| 1 | Builds without the bake ship voxel pilots silently, or invisible ones if voxel were cut | The voxel style stays their body (O4); VA2's gate; VoxelStyleChecks keep it building |
| 2 | The Worker drops costume picks at login until it is resynced | KeepSkin keeps the pick on the same PC; VA1 early, which also fixes today's lost hoodie, face and gun picks; the release notes say so |
| 3 | The costume disappoints (long-legged, narrow-shouldered blocks), or the faithful option costs more than expected | O1's sheet shows (a)'s three fits and (b) before V2 is built, with (b)'s lasting costs stated (2.3) |
| 4 | Low-end PCs lose their all-voxel option, which is also the cheapest crowd | O3: the row goes only if the low-end run passes; otherwise it stays, relabelled |
| 5 | The crowd's cost at 88 zombies, and 192 in the Siren's phase two. The Full tier's cost is mostly Unity's skinning and renderers | V15 first (Stage 2), with a go/no-go before Stage 3; V7 part 3's palette-skinned LODs and animation LOD; absolute gates at Stages 3 and 4; the voxel tiers stay the far LOD until the model LOD meets its row; A0's harness |
| 6 | Source-text pins and a red CI | Re-pin in the same commit; small branches; the full CoreTests before merging into shared |
| 7 | Determinism (per-outfit slice tables, corpse caches, LOD choice) | Choices stay on replicated data and rest tables (2.5); checks; late-joiner and killcam captures |
| 8 | Hit timing regresses | Clips retimed to the rules; ModelGaitChecks' blow contact; AnimationLodChecks' contact frames; the animation impact logged per package |
| 9 | The wire contract changes by accident | VA3's list; `Editor/NativeNetworkBuildChecks`; never a renumbering |
| 10 | The Electron game breaks | scripts/mocap, `src/` and the data stay byte-identical; the asset lock and the digests; the Worker change is additive and Unity-only |
| 11 | Licences (the repo is public) | The costume is built from committed data. The LOD meshes and the decimated cut derive from the licensed bake, so they live only in the local bank and the Drive zip; derived tracks are handled as today's files are |
| 12 | Lazy builds cause first-use hitches | Builds on workers, prewarmed at map load and seat change; voxel kinds stay eager while they draw or serve the gore surface; measured |
| 13 | Overhaul coupling | Amendments now (4.2); the binding pieces (4.1); W3 and W5 carry no voxel |
| 14 | Scope creep into the world | A separate track (V13), with the owner-visible items first; physical fracture is out of scope |
| 15 | Claude's capacity beside the overhaul | The minimum done (4.1); stages may slip, except the binding ones |
| 16 | The Siren's systems are built on voxel parts | She stays voxel until V11; her fixtures are kept |
| 17 | Stale comments and docs steer agents back to voxel | V0's doc fixes; the AGENTS.md direction; the ratchet's messages |
| 18 | A hidden gate is missed and the model stops drawing | V1's headless check; the counter |
| 19 | Measurements mislead (Editor and .NET 8 numbers against the Mono player) | V0 and V15 measure in the player; the Editor-to-player ratio recorded; a low-end run |
| 20 | A sixth look slot would break dressing | The costume is a key like `skin`, never a slot; RobotSuitChecks and ZombieModelChecks stay exactly |
| 21 | An older host strips the costume for its whole room | A release-note line; it clears once every host updates |
| 22 | The voxel style decays: the frozen legacy path, unguarded data | VoxelStyleChecks' digests from V0; a `--ca-voxel-bodies` capture per package; M11's offline bake when a voxel map is commissioned |
| 23 | Two writers on W3's branch | P9 and the eject states land after W3 merges, or through W3's session |
| 24 | Merging zombie renderers breaks covers, wounds, cuts or tints | FullTierChecks; ZombieModelChecks; near-range A/B captures |

---

## 7. Hand-off to Astra (paste-ready)

```
===== BEGIN HAND-OFF TO ASTRA: VOXEL RETIREMENT =====
```

# Voxel retirement: what Astra builds (2026-11-18)

From Claude, passed on by the owner, who divides the work: Claude builds presentation, and you build the rules, accounts and build tooling below. This hand-off stands on its own. It ends with an END marker; if the marker is missing, ask the owner to forward it again.

## What the owner asked
"I want to essentially remove voxel entirely BUT keep its data incase I use it for a map or game mode. The voxel player model + weapons can be selected as skins or costumes instead. All the new things ive been making, I want to remove the voxel dependency and move forward with a more detailed framing (since we're essentially going for much higher quality designs and things now)" (2026-11-18).

## What Claude is doing (presentation only)
- Everything drawn moves onto the detailed model's own skeleton: menus, entrances, the pod eject, deaths, the zombie crowd and its gait.
- The voxel pilot becomes a costume that any pilot can wear and every player sees. It is one new key in the look map, `costume:voxel`, which already replicates per seat: no new seat field and no protocol bump. The voxel weapons stay the per-weapon Prototype Series.
- Settings · Player model "Classic voxel" goes once a low-end check passes; a DEV-only override stays.
- The zombie crowd gets cheaper first (merged renderers, baked shapes, nearer shadows), then model LODs with animation LOD.
- All voxel data and code is kept, as a "voxel style" for builds without the licensed bake, for DEV comparison, and for a future voxel map or mode. Nothing is deleted.
- There is no rule, hit volume, timing, damage, cooldown, movement or network change.

## Ground rules
- **Base:** fetch first; base on `origin/claude/sleepy-ritchie-g8ruhv` (shared, `b13940ce` on 2026-11-18) unless the owner names another. One task branch per package, with a PR into shared.
- **Before merging:** the full CoreTests, `python tools/check_unity_compile.py` and `git diff --check`.
- **CI** runs the full CoreTests on every push to `codex/unity-*` and `claude/**`. Two new checks:
  - VoxelRatchetChecks pins, per Core and Runtime file, the counts of voxel reads, rig reads, rig-pose authoring and cube props (`ClassicBodies`, `CapturedRig.Boxes`, `rig.Bones[`, `new CapturedRig.Pose(`, `.AddE(`, `WeaponShape.Cube` and more). If it fires on your branch, its message names the model-native route; or ask Claude.
  - VoxelStyleChecks holds digests of the kept voxel data files and generated tables. If you must regenerate one, re-pin it on purpose and say so in the DEV_LOG.
- **Files:**
  - Claude's: the presentation files; the client cosmetics contract (`Core/NativeLook.cs`, `NativeBodySkins.cs`, `NativeWeaponSkins.cs`); the new `Core/NativeBodyStyle.cs`, `NativeVoxelStyle.cs` and `NativeVoxelCostume.cs`; `Runtime/NativeLighting.cs`.
  - Yours: the account Worker (`src/relay/accounts.ts`), the builder scripts and build checks, ProjectSettings, and the rules and network files as in the overhaul hand-off.

## Do not change (the contract the retirement keeps)
- **Wounds and deaths:**
  - CapturedRig's joint ids 0-21 as the wound and death vocabulary;
  - `NativeBodyDamage.Part`'s thresholds and the trauma energies;
  - the wound's capsule frame and `NativeWound.part` < 22 validation (`NativeNetworkFrame.cs:87`);
  - `Actor.Position` = body − .865;
  - the death records' model tokens (`pilot`, `siren`, `summon-*`, `horde-N`). Three presentation readers route on the `horde-` prefix (`NativeSummonPresentation.cs:62`, `NativeGorePresentation.cs:91`, `NativeMinionFxPresentation.cs:183`); if you ever replace it, add a horde marker and tell Claude.
- **Wire fields:** VisualSet and MagicVisual.
- **Volumes:** the capsule (radius .42, half height .85) and the head box (+.64, half .17; ParityLogicChecks:60-62). The model's head already stays within −.04 to +.11 m of the box, so BoZo P3 is not needed unless the owner asks.
- **THE CONVERTER:** the constants your authority reads from `NativeIronworksBossModel` (StandHeight, PulpitRest, ForemanFeet). If its box sculpt is ever trimmed, move them out first; Claude will ask you.
- **The spit:** the glob's origin in the acid record (`NativePveAcid.cs:20,30`).
- **Kinds:** kind ids stay append-only. A new kind ships only with Claude's model look: without one it draws a voxel kit, or the shambler's if the kit lacks it too (`Core/NativeZombieLooks.cs:353-355,367`; `Runtime/NativeHordeKindPresentation.cs:14-15`). Tell Claude when you add a kind id (A5a and A5b).
- **The Electron game:** `src/client`, `src/shared` and `scripts/mocap` stay untouched, and the exported data files stay byte-identical (the asset lock).

## VA1. The account Worker: resync with Unity, and the costume key
**The problem today:** `src/relay/accounts.ts` has drifted from Unity:
- `LOOK_SLOTS` lacks the robot pieces, which are Unity's defaults, and the Hot Dog. Because `open-hoodie` is the Worker's default but not Unity's, a signed-in open-hoodie pick is dropped on load.
- `FACE_SKINS` (`mask-visor`, `visor`, `mask`) is stale.
- `WEAPON_SKINS` lacks the Arsenal Series.

**The contract** (mirror `Core/NativeLook.cs` and `Core/NativeWeaponSkins.cs` exactly):
- `LOOK_SLOTS`, in order, each slot's first id its default:
  - hair: spiky, wild, side-swept, tied-back, fringe-tail;
  - upper: robot-armour, open-hoodie, hard-jacket, hoodie, tee, tank, sweater, hot-dog, sheldon-robe;
  - lower: robot-legs, belted-pants, baggy-pants, skinny-jeans, cuffed-shorts, khakis, sheldon-pants;
  - hands: robot-gauntlets, fingerless, gloves, arm-bands, sweat-bands, bare;
  - feet: robot-boots, mid-tops, work-boots, sneakers, lace-ups, sheldon-boots, barefoot.
- **The costume is a separate key, not a slot:** `costume`, with the ids `none` (the default, never written) and `voxel`. It is written after the five slots and before `skin:`, and kept in that order. A sixth slot would break Unity's dressing code, which indexes five-entry arrays by slot.
- **Approve the key and its place before Claude's Stage 1 merges.** NativeLook's header gives carrying the look to clients and the account to you. The deploy can follow later.
- `DEV_LOOK` adds hot-dog. The costume is not DEV-only unless the owner decides so (O2).
- `FACE_SKINS`: helm (the default), half, both, visor, mask, none.
- `WEAPON_SKINS`: standard and prototype for every weapon; also arsenal and arsenal2 for pistol, rifle and machinegun.
- `BODY_SKINS` is unchanged (standard, prototype, and sheldon for DEV accounts).
- If the owner makes the costume an unlock (O2):
  - a grant key such as `look:voxel` in GRANTABLE_CONTENT;
  - mirrored in `NativeDevGrants.Server`, `NativeCosmeticGate.Restricted`, the host's seat gate and the account load;
  - Claude handles the Locker side.

**Acceptance:**
- a relay test round-trips each id and the costume key, in order;
- an unknown slot, key or id is still dropped;
- Electron's profile fields and rendering catalogue are untouched; the change is additive and Unity-only.

**Deploy:** the live account service is production. Deploy only with the owner's explicit authorization, and log it in DEV_LOG and the release receipt.

**Coupling:** Claude's KeepSkin step keeps the costume on the same PC until the deploy, so Stage 1 does not wait on it; only a login on another PC loses the pick.

## VA2. The builder refuses a test-channel build without the bake
- **Why:** today a builder run without the Drive LocalAssets zip ships voxel pilots with no error.
- **The gate:**
  - an Editor check in `Editor/NativeBuildChecks` that fails a build started with `-requireVisualBody` when the player build lacks `ChipArenaVisualBody`;
  - the builder starts its test-channel builds with it;
  - the runtime log line "Visual body: ChipArenaVisualBody found" can serve as a second check;
  - extend it to the motion bank and the 14 stage banks if that suits the builder.
- **Acceptance:** a run without the zip fails loudly; a run with it publishes as today.

## VA3. Numbers for the crowd budget (no code unless a cap changes)
Claude's crowd work (V15 in Stage 2, the LODs in Stage 4) is sized by your counts. Please confirm:
- A0's DEV load harness (already planned);
- the hub cap `min(96, 24+16·lobby)` (88 at four pilots);
- the Siren's phase-two 192;
- HULKAMANIA's `min(6, cap)` Hulks;
- W11a's caps: at most 6 instance kinds alive per district, at most 25% of spawns in warp and 40% at threshold.

Pinned zone kinds take Full slots, and Claude lowers the Full counts with the LODs in Stage 4 (expected about 16, 12 and 8), so tell Claude before raising any of these. Also: will the crawler and the bloater ever ship? Today they have only voxel bodies; Claude would model them first.

## VA4. The skinning setting
- Today the project skins on the CPU. The committed `ProjectSettings/ProjectSettings.asset` is a stub without the skinning keys, and the Editor expands it to `gpuSkinning: 0` and `meshDeformation: 0`.
- Claude's V15 (Stage 2) measures CPU, GPU and GPU batched skinning in the Development Mono player at each tier, and reports the numbers.
- Commit the setting the numbers pick, in the form the builder honours, and confirm the builder's player uses it. It is build configuration, so it is yours (or the owner's).

## VA5. Later, only on the owner's word: a voxel game mode or a playable voxel-classic map
- **The mode:** a room option, its rotation and the content revision. It feeds Claude's switchboard (`NativeVoxelStyle`), which already answers per map: bodies (costume or classic), zombies, deaths, world and debris. A map can already ask for them through its look, with no rules.
- **The 15 legacy Electron arenas** would need your legacy rules kept: `Core/NativeHazards.cs` (saws, spikes, lava), the spikevault events, the old footstep surfaces, and the merge that drops those maps (`Core/NativeUnityMaps.cs:68,81`). Please do not clean those up.

## For your information
- The dead legacy trims: `NativeArena.DetailProp` lives in Claude's `Runtime/NativeLighting.cs:417-433`, and Claude makes it return early on dressed maps there. Your call site (`Runtime/NativeDestruction.cs:42`) is untouched; there is nothing to do.
- An older host strips the costume key from every seat it relays, until it updates.

## Release coupling
- **VA1:** the key approved before Claude's Stage 1 merges; the deploy when the owner authorizes it.
- **VA2:** any time; ideally before Stage 1.
- **VA3:** answers before Claude's Stage 3 (the overhaul's M3), so V15's go/no-go and the LOD sizes use them.
- **VA4:** after V15's numbers (Stage 2).
- **VA5:** only on the owner's word.
- None of this bumps a protocol or a world Version.

```
===== END HAND-OFF TO ASTRA: VOXEL RETIREMENT =====
```

---

## 8. Owner decisions

### 8.1 Five decisions (the recommended answer first)
1. **O1. How the voxel costume looks and moves.**
   - **Recommended:** (a) voxel blocks fitted to the model's frame, in one of three fits. It moves, dies and works with everything new exactly like the model. It is taller and longer-legged than today's voxel pilot.
   - Or (b) today's voxel pilot exactly, per seat, on the old skeleton. It looks just like today. But the classes added since the model (Katana, Fangs, the DEV blades) play generic old moves on it unless converted one by one, its deaths stay the old voxel ones, and every new feature needs extra work for it.
   - You choose from one sheet showing both. It also asks how a costumed pilot dies, and whether a voxel map may override a player's kill style.
2. **O2. Who gets the costume, and what it holds.**
   - **Recommended:** free for every pilot and seen by every player. It offers the Standard and Prototype voxel bodies, with Sheldon Clothes staying DEV-only. Weapons stay per-weapon Prototype picks, plus a one-tap "voxel set" that can be undone. The five weapons without a Prototype keep Standard: voxel versions would need new sculpts and a larger skin map, which is an Astra change.
   - Or DEV-only, like the Hot Dog.
   - Or an unlock, which needs Astra's grant key with the Worker deploy.
3. **O3. Settings · Player model.**
   - **Recommended:** the low-end run first (V0). If the model holds a p95 of 16.7 ms (60 fps) or better at Low on that run, in both the 8-pilot match and the 88-zombie hub, the row goes in Stage 1 and a DEV-only switch stays. If not, the row stays, relabelled "Low-end bodies (this PC)", until V15 and V7 bring the model within that, and then it goes.
   - Or keep it as a local low-end option for good.
4. **O4. Builds without the licensed bake** (CI, the cloud builder, public clones).
   - **Recommended:** they keep the voxel style as their body (our own art, safe in a public repo), and the builder refuses to publish a test-channel build without the bake (VA2).
   - Or commission a licence-free in-house body, so that even public builds draw a modelled pilot (L).
5. **O5. The Siren.**
   - **Recommended:** she stays the one voxel pilot until her own model, on her own joints and not the player model, lands for the final court in Stage 8.
   - Or model her first, before the crowd work, which delays V7.
   - Or keep her voxel permanently, as the voxel boss.

### 8.2 Proceeding unless the owner objects

| Default | Where |
| --- | --- |
| Hit volumes stay as they are. The capsule and the head box were measured on the voxel pilot, and the model's head stays within −.04 to +.11 m of the box. Matching hits to the taller model (BoZo P3) stays parked | G1 |
| Every kill style keeps its id. Voxel breakup stays deliberately voxel, as the costume's signature. The death-scene props and victims (Confetti cannon, Beam me up, the DEV clown car, Clown Love, Bass Van, the Raging Demon) are remodelled over Stages 3-8 | D6, D9, V14 |
| A voxel map or mode can be fully classic: old pilots, old deaths, voxel zombies and the block world. That path stays working and tested; its details wait until the owner commissions one | 2.4, VA5 |
| The legacy rig path is frozen: in the voxel style, the classes added since the model play generic moves until a voxel map needs them | M11 |
| The voxel launcher icon, also the Windows player's icon, stays | U7 |
| The Cyber Lord keeps its data-cube style, on the model | P14, D14 |
| Calibration Bay keeps its studio look | F10 |
| The crawler and the bloater stay unshipped and voxel-only in DEV; they get models before either ships (Astra's content call) | E15 |
| No physical fracture of props | F15 |
| The frozen authored town keeps its box style (overhaul Q11) | — |

### 8.3 Owner actions, not decisions
- review the costume sheet (O1) before Stage 1 ships;
- authorize Astra's account Worker deploy (VA1), which also fixes the picks lost today;
- name a low-end test PC, or accept the constrained run (V0), before O3 is settled;
- forward section 7 to Astra whole: it ends with an END marker.

---

## Appendix A. What the sweeps' verification changed
1. **CI runs the full CoreTests on every push** (`tools/check_relay.py` runs CoreTests.dll). So every re-pin lands in its own commit, and a ratchet can be enforced.
2. **The model already plays its own pack clips for most pilot states.** The XL rig work is mostly the zombies and a named list of pilot states, not all motion.
3. **The costume under O1(a) needs the bake's skeleton.** Builds without the bake keep the rig-driven voxel style, so the twins and the vent victim stay (O4).
4. **The outfit already travels per seat into replays and killcams,** so the costume needs no new field. But the account Worker has drifted and needs its own owner-authorized deploy (VA1); it is outside A1's scope.
5. **The model sits inside the voxel path** through hidden gates: the bank's null checks, HordeKindsReady, the echoes. They go first (V1), before any lazy loading.
6. **The voxel leaks on model pilots are more than the ghosts' weapons:** kill caps and fragments, the crushed hat, revive cells, wound cubes, status cubes, the Sheldon hood on the Hot Dog, the Cyber Lord's shimmer, the Siren's victim and death-scene weapons.
7. **The authority's volumes and the wire vocabulary are the voxel pilot's.** They are kept as a contract and not reworked; P3 is not needed.
8. **The crowd budget is sized with Astra's counts:** pinned adds take Full slots, far detail is Station-only, and HULKAMANIA allows six Hulks.
9. **Gore already draws as orbs;** the breach chunks are still cubes, outside the debris switch.
10. **The "kept voxel on purpose" items were Claude's calls, not the owner's:** the vent victim, Voxel breakup, Confetti and the Siren's cubes. The owner decisions on record are the debris switch, "Model everywhere, classic will be stored as backup", and the Classic setting asked for "just in case".
11. **The unused M1 stage kit's scenery helpers are a live library for seven stages.** With the standing rule that no voxel data or code is deleted, the plan deletes nothing.
12. **Lazy loading saves less than first claimed:** the voxel pilot set is about 2.4% of SourceModels' vertices. The real savings are the code-built skins and horde tiers, and the dead map meshes.
13. **The Station's and the Ironworks' hazards are live** (Astra's zone combat binds them), so their modelled rigs must match the live authority.

## Appendix B. What the two critiques changed
Every high and medium finding was checked in the code and applied. One low finding was partly wrong (L6).

**Feasibility and performance:**
- **F1 [high], the Full tier is the crowd's cost, mostly Unity's:** applied. E21 and V15 (Stage 2), with a go/no-go before Stage 3; VA4 for the skinning setting. Confirmed: CPU skinning (ZMP:140, the expanded settings), Mono Development builds (`Editor/PrototypeBuild.cs:116,121`).
- **F2 [high], per-variant rigid LODs multiply draws and cannot be posed:** applied. E3's LODs are per kind silhouette, authored offline into the local bank, palette-skinned in one instanced draw each, posed by a model-proportion leg solve on model-leg strides, with a capped near band within 12 m.
- **F3 [high], every tier runs the full gait and 22 Transform writes:** confirmed (HKP:313,333,392). E20 and animation LOD in V7 part 3; the budgets are measured in the Mono player.
- **F4 [medium], the Low triangle row did not add up:** applied. The rows are recomputed (High about 0.75 M, Low 0.25-0.30 M), with a decimated cut for Low's Full bodies and the near band.
- **F5 [medium], the measurements could not settle the decisions:** applied. V0 measures in the Development Mono player with `PERF_SAMPLE` and `PERF_HITCH`, records the Editor-to-player ratio and adds a low-end run. The GPU-or-CPU question is dropped (CPU today); V15 compares the alternatives.
- **F6 [medium], V8's performance claims and its pairing with M5:** applied. V8 claims no saving, and ships alone in Stage 5 behind a DEV A/B switch, with the baseline re-measured.
- **F7 [medium], rules, network and build work inside Claude's packages:** applied. The Sheldon gate item is dropped (`body:sheldon` covers it); `-requireVisualBody` moved to VA2; Astra approves the costume key with VA1; drawn frames never choose a slice part or a killcam cause (2.5).
- **F8 [low], VA4 not needed:** confirmed (DetailProp is in `Runtime/NativeLighting.cs:417-433`). F11 is Claude's; VA4 is now the skinning setting.
- **F9 [high], a costume slot breaks dressing:** confirmed (`NativeRobotSuit.Under` and the zombie variants' `Pieces` have five entries, indexed by slot). The costume is a key like `skin`; SameOutfit compares it; KeepSkin keeps it across logins; VA1 mirrors it as a key.
- **F10 [medium], hide, precedence and interaction rules:** applied. The hide-and-keep table, precedence with the DEV Hot Dog piece and the Sheldon Head, the blind-pick rule (`NativeNetwork.cs:199` blanks the skin map in the lobby), the voxel set's restore and the capsule check (2.3).
- **F11 [medium], Stage 1 alone:** applied. KeepSkin removes the dependence on the Worker deploy (the login merge at `NativeProfileUI.cs:51` goes through it); the Classic row goes only with V2 and only after the low-end run; a rejected sheet leaves the row.
- **F12 [medium], lazy voxel kinds stall the gore:** confirmed (`NativeHordeGoreBody.Of` builds a Full kind on the calling thread). E4 in Stage 3 is the gate and the boot split only; the voxel kinds stay eager while they draw and while they are the gore surface.
- **F13 [medium], ordering and acceptance errors:** applied. Revive glints moved into V6 with D4; V1's acceptance restated ("the model draws with the voxel pilot set unbuilt"); Stage 3 keeps pinned adds Full at any count until the LOD; the deploy cast keeps its voxel branch, chosen by the predicate.
- **F14 [medium], frame-time allowances that may be negative:** applied. Stages 3 and 4 have absolute gates; the overhaul's report rule applies before code merges.
- **F15 [low]:** applied. P9 lands after W3 merges or through W3's session; the ratchet counts rig reads under every name; no new shader keyword for the palette; a minimum done.

**Completeness and owner intent:**
- **H1 [high], the costume is not the voxel player model, and the plan decided it without the owner:** applied. O1 offers (a) and (b) on one sheet before V2 starts; principle 2 no longer calls (b) rejected.
- **H2 [high], W3 authors the eject on the rig, and V0 passed without checking:** confirmed at `048adc30`. M12; V4 moves the eject onto the model; the ratchet counts rig authoring; amendment 1 is rewritten.
- **H3 [high], the ratchet and the counter miss rig authoring and cube props, and new content was never audited:** applied. 1H audits every feature since the model; the ratchet's five token families; the counter's primitive pieces; the Bombadier's and techniques' props and the Sentry's gun in V3.
- **M1 [medium], the legacy path will not keep up:** applied. M11 states it: frozen now, with an offline bake when a voxel map or O1(b) needs one.
- **M2 [medium], the voxel map switch does not make a voxel game:** applied. `bodies` takes costume or classic; `World(map)` joins the switchboard; the twins follow the body path; the details wait for a commissioned map (8.2).
- **M3 [medium], worn attachments float or sink:** applied. The attachment table with a 1 cm check, and the pod seat, harness and climb-out captures.
- **M4 [medium], foundations land after their users:** applied. The resolver moved into V1; V16's capsules and sole floor land in Stage 3.
- **M5 [medium], the voxel-pop fix a milestone late:** applied. E11 and E19 move to Stage 2, with W7's district outfits.
- **M6 [medium], loadability checked last:** applied. VoxelStyleChecks' digests and engine-free builds from V0; a `--ca-voxel-bodies` capture per package.
- **M7 [medium], E10 contradicted the keep rule:** applied. The voxel gore surface is kept, built only when voxel kits draw; the orbs note is added.
- **L1, O3's fallback:** O3 now says exactly what happens.
- **L2, scattered owner calls:** the owner brief at the top; 8.2's defaults; the amendments go to the owner with the plan.
- **L3, voxel versions of five weapons:** P7 and O2 state the cost.
- **L4, an older host strips the costume:** a release-note line (2.3).
- **L5, the direction belongs in AGENTS.md:** V0.
- **L6, MapLooks.json is generated:** partly wrong. Its rows are edited by hand or by a map's own script (`docs/arenas/scripts/expedition_city.py` writes the city's; Thunderheart Carillon's row arrived at `b13940ce` without the generator), and `tools/generate_expedition.py` only rehashes its lock entry. The fix that holds: each change refreshes that entry in the same commit.
- **L7, hand-off gaps and W3's branch:** section 7 adds the new-kind rule; P9 lands after W3 merges or through W3's session.
- **L8, two taste choices:** both are on O1's sheet.

---

## Appendix C: the owner's summary (as approved, "Yes, use defaults")

### Voxel retirement: summary for you (2026-11-18)

You asked for four things:
- remove voxel as the base of the game;
- keep all its data, for a possible voxel map or mode;
- offer the voxel pilot and weapons as a costume;
- build everything new on the detailed model.

This is the plan for that. Nothing is built yet.

## What still depends on voxel today
- **Pilots**
  - The "Classic voxel" setting draws every pilot and zombie as voxel on that PC.
  - Copies of the game without the licensed body draw only voxel pilots, and nothing warns about it. That covers public copies, cloud builds, and a builder run that is missing the asset zip.
  - The Siren boss is still voxel. The DEV Cyber Lord still moves on the old skeleton.
  - Small voxel bits still show on the new model: ghost trails holding voxel guns, status cubes, revive cells, wound cubes, and a hat that flies off at voxel size.
- **Animation**
  - The old voxel skeleton still drives everything the new animation packs do not cover. That means specials, knockdowns, entrances, lobby and menu poses, first person, and the new pod eject being built right now.
  - Hidden voxel boxes still decide where the floor is, how ragdolls collide, and where wounds and gibs land.
- **Zombies**
  - Only the nearest 24 zombies (16 on Low) are models. The rest of a crowd is voxel, and so are their corpses and severed limbs.
  - Every zombie first walks on the old skeleton. The model's legs are then worked out again on top.
  - Full-detail zombies are expensive: the processor shapes their bodies, not the graphics card, and each one is about 16 separate pieces.
- **Deaths:** cuts, gibs, wound marks and corpse physics are worked out on the voxel body first.
- **Newer content:** some props are built from plain cubes:
  - the Bombadier's mines and belt;
  - the weapon techniques' tripods and crates;
  - some chip pieces;
  - many entrance props.
- **Menus:** portraits, the draft, the rooftop and the Locker art fall back to voxel, or are framed on it.
- **World:**
  - many maps still draw box buildings as a fallback;
  - two expedition zones, Central Station and City Ironworks, have no modelled stage yet.
- **Already clean, and the pattern to follow:** NOCTIEL, THE HUM, THE DEEP END, the boss relics and the new Thunderheart Carillon arena use no voxel at all.

## Where it ends up
- **The model comes first.** Every class, weapon, enemy, animation, effect and menu is built, timed and fitted on the detailed model.
- **Voxel as a choice:**
  - the voxel pilot is a costume any player can pick. Everyone sees it, in killcams and replays too;
  - the voxel weapons stay as each weapon's Prototype skin, with a one-tap "voxel set";
  - Voxel breakup stays as a kill style.
- **Voxel as kept data:**
  - every voxel model, animation and map stays exactly as it is;
  - every build checks that it is unchanged and still loads, so it is ready for a voxel map or mode;
  - copies without the licensed body keep drawing it.
- **No gameplay change:** hits, timing, damage, movement and networking stay exactly as they are.

## The releases, in order
Each one is a playable test-channel update.

0. **With the expedition's first release (the drop):**
   - checks that stop new voxel from creeping in;
   - a voxel counter in the DEV overlay;
   - speed measurements in the real game build, including a low-end PC;
   - no voxel weapons on the deployment pads.
1. **The costume** (its own release, before Night Falls):
   - pick the voxel look in the Locker, and everyone sees it;
   - the Classic setting goes, if the low-end check passes;
   - no voxel bits left on model pilots in play.
2. **With Night Falls:**
   - menus, portraits, the draft, the lobby poses and the pod eject move onto the model;
   - the Bombadier's and the techniques' props are modelled;
   - breaking walls burst into real pieces;
   - no voxel pop-in when the district outfits arrive;
   - close-up zombies get cheaper to draw.
3. **With Belvedere Camp:**
   - wounds, gibs and revive effects come from the model;
   - the Siren's victim is on the model;
   - Backrooms creatures are modelled at any distance;
   - weapon effects sit on the real weapons;
   - survivors are on the model.
4. **With Modelled Meridian:**
   - no voxel zombie, corpse or limb anywhere: far-away zombies become simple models;
   - HULKAMANIA runs on models;
   - entrances are on the model;
   - ragdolls and cuts come from the model.
5. **Its own release, before the layered city:**
   - zombies walk on the model's own legs;
   - specials, kit moves and knockdowns are on the model.
6. **With the layered city:** the city's light and skyline are built from its models instead of boxes.
7. **With the rooftop horde:**
   - wounds show on zombies;
   - first person is on the model;
   - the Slenderman and the summons get bodies of their own.
8. **With the last climb:**
   - the Siren is on her own model at the final court;
   - a voxel-map preview, played with a host and a guest;
   - the last voxel checks reach their floor.

**If time runs short,** "done" means three things: the costume, no voxel on model pilots, and the zombie crowd on models (release 4). The world and death-scene leftovers can follow the map work.

**The plan also adjusts the approved expedition plan in a few places:**
- no voxel fallbacks for the new deployment scenes or the survivors;
- the pod eject moves onto the model;
- the zombie speed work comes before the district creatures.

These are written into the plan for your sign-off.

## Who builds what
- **Claude:** all the visual work. That means the costume, models, animation, effects, menus, the crowd's look and speed, and the checks.
- **Astra:**
  - the account update, so costume picks follow you to another PC. It also fixes the outfit, face and gun picks lost at login today. Going live needs your OK;
  - the builder refusing to publish a test build without the licensed body;
  - the skinning setting, once Claude has measured it;
  - the crowd numbers;
  - keeping the network and the rules exactly as they are.
- **You:**
  - the decisions below;
  - the costume review sheet;
  - your OK for Astra's account update;
  - a low-end test PC, or a simulated one;
  - forwarding Astra's part of the plan.

## Your decisions (recommended first)
1. **How the voxel costume looks.**
   - **Recommended:** voxel blocks fitted onto the new body. It is taller than the old pilot, with longer legs and narrower shoulders. But every new animation, death and effect works on it automatically.
   - **Or** the exact old stocky voxel pilot. It looks just like today, but it stays on the old animation system. Newer classes (Katana, Fangs and the DEV blades) play generic old moves on it, and every new feature needs extra work for it.
   - You choose from one review sheet showing both. The sheet also asks how a costumed pilot looks when it dies.
2. **Who gets the costume.**
   - **Recommended:** every player, free. Sheldon Clothes stays DEV-only. The Katana, Fangs, Glaive, Top Dog and Bombadier weapons keep their normal look, because voxel versions would need new models and a network size change.
   - **Or** DEV-only, like the Hot Dog.
   - **Or** an unlock, which needs an Astra change.
3. **The "Classic voxel" setting.**
   - **Recommended:** test a low-end PC first. If the game holds 60 fps at Low there, the setting goes and a DEV-only switch stays. If not, it stays, renamed "Low-end bodies (this PC)", until the zombie speed work lands.
   - **Or** keep it as it is.
4. **Copies without the licensed body** (public copies, cloud builds).
   - **Recommended:** they keep drawing the voxel pilots, and the builder refuses to publish a test build without the licensed body.
   - **Or** commission a licence-free modelled body, so even public copies look modern. That is a large job.
5. **The Siren.**
   - **Recommended:** she stays voxel until her own model is ready for the final court, in the last release.
   - **Or** model her first, which delays the zombie crowd work.
   - **Or** keep her voxel for good, as the voxel boss.

## Going ahead unless you say otherwise
- Hit sizes stay as they are.
- Every kill style stays, and Voxel breakup stays voxel. The death-scene props (the Confetti cannon, Beam me up and the others) get remodelled over time.
- A voxel map or mode can be fully classic: old pilots, old deaths, voxel zombies and box buildings. That path keeps working. The details wait until you want one.
- The voxel launcher icon stays, and the Cyber Lord keeps its data cubes.
