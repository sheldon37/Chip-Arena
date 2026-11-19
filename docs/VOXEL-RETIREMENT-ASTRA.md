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

### Confirmed crowd budget · 2026-11-18

| Scenario | Budget for Claude | Evidence and status |
| --- | --- | --- |
| A0 load fixture | 4 scripted pilots and 88 living hub zombies | Implemented for Editor/Development in [A0 PR #412](https://github.com/sheldon37/Chip-Arena/pull/412). Native player timings still pending. |
| Occupied hub | 40 / 56 / 72 / 88 for 1–4 lobby seats, then 96 for 5–8. Zero with no living pilot in the city. | `NativeExpeditionHubRules.Cap`, before mutators. |
| Hidden phase-two Siren | 72 / 120 / 168 / 192 for 1–4 pilots, with a ceiling of 192 thereafter | `NativePve.PveFlow` computes `min(192, 12 × (6 + 4 × (partySize − 1)))` during possession. Authored zone bosses use their own cap. |
| HULKAMANIA | Up to `min(6, base cap)` Hulks on eligible maps. Level 0 and Poolrooms substitute Brutes within the same cap. | `NativeExpeditionMutators.Cap`, `NativePveHorde.SpawnPveAddCore` and `NativeBackroomsKinds.SpawnKind`. |
| W11a district spawns | Up to 6 living instance-origin minions total per district. At most 25% of spawns in warp, 40% at threshold, zero from a cleared instance. | Approved A5a/Q13 limits for the upcoming implementation. Six counts individuals across all kinds. |

Pinned zone minions consume Full-detail slots. Stage 4's 16 / 12 / 8 High / Medium / Low counts are targets for Claude to measure. This handoff changes no cap. Crawler and Bloater stay in DEV with their voxel bodies. Either needs a detailed model before it enters a shipped spawn pool.

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
