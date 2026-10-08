# Validation — native Unity conversion

No native Unity playtest or Windows executable build was run in the authoring workspace.

## Weapon presentation, C11 (2026-10-08, `claude/wip-c11`)

- Runtime and Editor assemblies compile against the real Unity 6000.3.24f1 managed DLLs. `tools/check_relay.py` passes; the new WeaponPresentationChecks compare against `tools/export_weapon_fixtures.mjs <source checkout> <temp>` (writes `Tests/weapon-presentation-parity.json` from the pinned weapon-visuals.ts, melee-trails.ts, attackPose and three r180). They cover 195 primitive vertices, 292 module pieces over 34 loadouts (and the native stats on every key the modules read), 306 enchant pieces, 81 muzzle-flash frames, 4,432 sweep-window samples, and 94 ribbons over 79 frames, including the 16-sweep and 48-sample caps, reduced quality, a frame gap, time running backwards and trails switched off. Seven deliberate mutations of the Core rules were each caught. 24 Python tests pass; `tools/package_source.py` passes.
- **Not run:** Unity import, rendering on a GPU, the effects in a hosted or joined match, first person in the player, frame-time cost on the owner's PC.

## Melee runtime, presentation polish and updater integration (2026-10-08, `claude/sleepy-ritchie-g8ruhv`)

Run in the Linux authoring workspace on the combined tree (updater merge `d185be4` and later):

- Runtime and Editor assemblies compile against the real Unity 6000.3.24f1 managed DLLs with warnings as errors (`netstandard2.1`, `UNITY_STANDALONE_WIN`); a strict pass with unused-field warnings enabled is also clean.
- `dotnet run --project Tests/CoreTests.csproj`: 93 PASS lines, 0 failures. New: captured-melee motion parity against the transpiled pinned source (872 scripted frames, worst joint error 1.73 mm, 5 mm limit; shield frames after the first stun excluded, see ANIMATIONS.md), CSS cubic-bezier/keyframe curves, the UI canvas at 720p–4K and 3440×1440 (2150×900), intro name-card times and style names, and chip card text coverage (242 chips plus the four-chip Siren track).
- `python -m unittest discover -s Tests -p "test_*.py"`: 20 tests OK. New: Ward Zero bake (crowd off the origin, no full-intensity glows, six transparent beam parts) and the ten stage-hazard parts; the model count is now 139.
- `dotnet run --project Updater/Tests/UpdaterTests.csproj -c Release`: 51 updater transaction/archive checks pass. `tools/check_relay.py` passes. `tools/package_source.py --check` passes (605 source files; asset receipts include HazardModels.json, ChipText.json and the regenerated SourceModels/WardMotion/SceneryMotion).
- Exports reproduced from the pinned source: `tools/export_cinematics.mjs` (Ward Zero bake), `tools/export_hazards.mjs`, `tools/export_chip_text.mjs`; only Ward Zero models changed in SourceModels.json.

**Not run (needs the owner's Windows Unity):** Unity import, shader compilation and rendering of every new overlay, card, hazard, Ward mood, HUD and menu-skin change; the in-match melee runtime in motion; `Build-Windows.cmd` with the updater packaging (`UNITY_TEST_BUILD_READY`), the starter ZIP, and an actual Unity-to-Unity update through the menu card; `tools/check_updater_windows.py` (Windows only). Rendered appearance at 1280×720, 1920×1080, 2560×1440 and 3440×1440 is unverified.

## Presentation startup repair (2026-10-07, 20:27 owner report)

The owner reaches the match-setup UI but reports a failed match start. The
screenshot shows `CreateImpl is not allowed to be called from a MonoBehaviour
constructor (or instance field initializer)` on `ArenaPresentation`, followed by
null-reference errors. Its Ward `MaterialPropertyBlock` was allocated by a field
initializer. Allocation now occurs in `Initialize`, called after `AddComponent`
by `ConversionGame.Awake`. Existing shader properties and rendering behavior are
preserved. The later null-reference errors are consistent with incomplete startup;
their individual stacks were not supplied.

Previous native build checks constructed the arena and glow helpers but never the
presentation component. A new Editor gate creates that component, initializes the
native presentation, resets it twice and tears it down, repeated for two fresh
instances. It verifies the audio/particle/light pools and captures Unity errors,
including errors that Unity logs without propagating an exception. Initialization
and teardown now use the existing play/edit-aware release helper so this test can
run in an isolated preview scene. It does not draw or validate a frame.

Local 12 asset/package tests and source/GUID checks pass. The new native lifecycle
gate, rebuilt Windows executable and actual match start are **unrun here**. The
screenshot alone does not establish that the preceding Windows builder finished.
Unity's [initialization guidance](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html)
requires engine-dependent initialization outside the component constructor.

## Zero-cost ability gate repair (2026-10-07, late evening)

The owner's follow-up Windows log confirms the repaired texture gate passes:
all three base images survive import, mip chains are retained and truncated input
is rejected. Script compilation, source checks, imported models/motion/audio,
glow lifecycle, native melee and native E-special checks also pass. Execution then
stops at `Native combat gate: release/cost/cooldown frag-grenade`.

The combat validator incorrectly required every live ability to reduce mana and
refuse an empty mana pool. The pinned source defines grenade, rocket and orbital
mark with zero cost. Gameplay and source data are unchanged. The gate now checks
the exact configured debit, cooldown deadline and charge consumption, requires no
debit before release, exercises free abilities with zero mana, and retains paid
ability refusal without state changes. Separate failure messages identify which
condition failed. Managed source-fixture checks cover the exact-cost and zero-mana
boundary across all 44 exported ability/stat cases.

Local 12 Python asset/package checks, source receipts/GUIDs and diff checks pass.
The managed suite runs in conversion CI. The corrected native gate and remaining
Windows build gates still require the owner's activated Editor; no executable or
rendered gameplay acceptance is claimed from this repair.

## Windows import/build repair (2026-10-07, evening)

The owner's Unity 6000.3.24f1 log reached the content, special, melee, first-person,
glow and ability parity checks, then stopped in `NativeSourceModels` with
`LoadRawTextureData: not enough data provided`. It also reported the invalid
`NativeAccount.Start(...)` callback. These are code defects, not a failed license
activation: the log resolved the Unity Personal entitlement and executed the checks.

The three procedural textures contain only mip 0: 16,384 bytes for 64 x 64 and
262,144 bytes for each 256 x 256 RGBA image. The old loader allocated mip chains,
which require 21,844 / 349,524 bytes, then supplied only the base image. The loader
now validates that base image and calls `SetPixelData<byte>(pixels, 0)` followed by
`Apply(true, ...)` to generate mipmaps. Source pixels, repeats and mipmapping are
preserved. The account request helper is renamed `BeginRequest`, including every
caller; account behavior is otherwise unchanged.

An Editor-only gate uses the actual texture factory, checks all imported base
pixels and mip-chain presence, rejects a truncated payload and inspects the
account component's Start signature. That native gate and the Windows player
build are **unrun here**. Python checks additionally validate the three texture
payloads and their model references. See the repair branch/CI for executed checks;
these data checks do not establish native import, shader or gameplay acceptance.

Unity API references: [LoadRawTextureData](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.LoadRawTextureData.html),
[SetPixelData](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.SetPixelData.html),
[Apply](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.Apply.html).

## Full conversion candidate checks (2026-10-07)

This current section supersedes unfinished-feature notes in the historical checkpoint entries below.

- Runtime, Windows-player and Editor C# API compilation uses genuine Unity 6000.3.24f1 reference assemblies with warnings as errors. The Editor gate initially exposed an internal authority-flag access across assemblies; setup now stays inside the runtime validation component, preserving the private authority boundary.
- Original-source parity includes 84 wound-carving geometry/material cases and 64 hashes; eight collapse scenarios with eight samples each (planted feet, detached head, cover limits and release velocities); 79 summon, station/pickup, draft-pose/look and grab-timing samples. Existing content, melee, special, first-person, aiming and animation regressions remain passing.
- Eleven Python checks validate dependencies, 31 cinematic tracks / 133 models, wound-part bindings, dynamic scenery, Slender hierarchy and the original scare-image hash. Source package validation checks 92 large/binary asset receipts and unique Unity metadata.
- Real loopback TCP and protocol-3 WebSocket relay pass, including discovery, private rooms, source/version refusal, two-client ordered transport, 130 KB snapshots and cleanup. No deployed service is exercised.
- Expanded Windows-only gates cover actual gun/magic primary release/resources and swept contacts, cover, reload, reflection caps, all live Q/E releases, multishot/split ledgers, fatal headshot collapse selection, PvE purchases/pick ownership/healing/recruit limits/revival, plus the existing maps, melee, source clips, shaders and glow gates. These gates are compiled here but **not executed** without Unity.
- The newest files include source wound/collapse geometry, animated snow/water/smoke, source Slender/toy/zombie presentation, victim scare image/static/audio, station animations, health/damage HUD, death announcements and animated 3D drafts. Native presentation adapters are documented in CONVERSION-STATUS.md.
- **Not run:** activated import, shader compilation, native PhysX gates, rendered gameplay, live account flows, deployed rooms/headless authority or Windows EXE. Passing C# and data checks does not substitute for those tests. No performance/framerate or visual-fidelity acceptance is claimed.

## Historical checkpoints

## Stability/aiming checks (2026-10-07)

- Reproduced the previous first-person feedback: five repeated evaluations at an unchanged simulation time moved the magic wrist 3.43 cm in rig space (before the 0.78 view scale). The new shared runtime base-history path passes 960 repeated frames across melee, guns/rocket and four magic schools with stable joint positions and rotations, plus rewind/gap resets.
- 1,787 render-clock samples at 30/60/120/144/240 Hz verify continuous presentation between 60 Hz fixed ticks, monotonic time, pause/resume, reset and a one-tick limit. These are numeric clock tests, not measured game frame rates.
- ADS spring transition/settling/return, duplicate-time/reset behavior, scope eligibility, source 33vh mask dimensions and FOV counter-scale pass. Source ability-hud.ts and style.css hashes are added to the existing reference lock.
- All prior staff projection, glow/trail, original-source parity, managed regression and captured-pose checks pass. Runtime/Editor/Windows-conditional C# compiles against actual Unity 6000.3.24f1 assemblies with warnings as errors; four package regressions and source/GUID checks pass.
- New **unrun Windows Editor gates** repeat actual baked AnimationClip playback for magic/rocket poses and verify that interpolated render snapshots do not alter authority. Existing shader/physics/glow gates are retained.
- **Not run here:** activated native Unity execution, actual camera interpolation/frame pacing, first-person or scope rendering, Windows EXE build. Numeric projection/stability checks do not replace the owner's on-device test.

Unity 6000.3 references checked for this implementation: [Rigidbody interpolation](https://docs.unity3d.com/6000.3/Documentation/Manual/rigidbody-interpolation.html), [default-scene interpolation ownership](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.InterpolateBodies.html), [fixed clock](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-fixedTimeAsDouble.html). The default scene performs interpolation automatically; manual calls there fail, so the camera reads its interpolated Transform in LateUpdate.

## Staff/glow checks (retained, 2026-10-07)

- 5,808 full native staff-mesh projections at the default 75° / 16:9 view: all four schools, idle, repeated primaries, Q/E, three casting rates and guard transitions. Zero intersections with Claude’s crosshair box; idle/primary/held-guard crystal visibility satisfies the 95% gate; the nearest sampled shaft point is 0.462 m from the camera. The check shares runtime geometry, pose and view-placement math. It is a numerical projection check, not a rendered screenshot or a test of every aspect ratio/camera motion.
- Torso Euler round trips and guard spring blend/reset checks pass. Glow falloff, impact/trail envelopes, armor pulse, slow-projectile trail accumulation, expiry and clock reset checks pass.
- Five additional exact source blobs are pinned: projectile-glow, impact-glows, projectile-trails, armor-glow and Claude’s ranged/magic framing tests. Claude’s 2026-10-04 DEV_LOG staff fix was reviewed at source `05950ea`.
- Runtime, Editor and Windows-conditional C# compile against Unity 6000.3.24f1 assemblies with warnings as errors. Existing source parity, managed regression, captured-pose and four package-resolution checks pass.
- Added a **Windows Editor build gate, not run here**, for 200 simultaneous surface glows, primary/ability traces, seven pooled lights, natural expiry and reset. It skips draw submission and does not measure rendered brightness or GPU performance. Shader import/error checking now includes NativeGlow.
- **Not run here:** activated Unity import/native execution, shader compilation/rendering, visual brightness/clipping, native PhysX gameplay or a Windows EXE build.

## Previous camera/ability checks (retained)

- 1,728 original `magic-motion.ts` samples match 13 channels plus weights across four elements, primary streaks, all eight magic Q/E phrases, first-person variants, pitches and attack rates. Input timestamps are quantized to the native float clock before the original JavaScript functions run; expected outputs come from the original functions, not C#.
- 44 original `abilities.ts` rule cases cover every active ability and extreme stat clamps. Refusal/buffer/resource boundaries, input edge handling, invalid aim rejection, camera lifecycle, view-model authority isolation and left-hand staff/guard orientation also pass.
- Eleven new reference-file Git blobs verified against the pinned GitHub tree. The reproducible fixture exporter checks their hashes before evaluation.
- Existing eight native content/rule/match groups, 35 inherited managed checks and 34 captured clips / 3,384 poses still pass.
- Runtime, Editor and Windows-conditional assemblies compile against actual Unity 6000.3.24f1 references with warnings as errors; four package regressions and project/GUID checks pass.
- **Not run here:** Unity shader import, lighting/frame rendering, native ability contact/PhysX scenarios, Windows EXE build or interactive first-person play. This is a source checkpoint, not a verified binary.

## Previous foundation checks (retained)

- Exact Git-blob receipts for 108 pinned source files used by the export; catalogue contains 15 weapons, 236 Chips, 15 maps / 3,451 collision blocks and 14 Q/E definitions.
- Original-source fixtures: 60 cases / 4,620 stat values, eligibility order, deterministic offer order, roll profiles and 20 hazard clock samples match the native C# conversion.
- Native match checks: two-round set progression, losing-player draft ownership/readiness, single award, reset for the next set, simultaneous-knockout replay, team credit, skipped-bot awards, match completion and clean rematch.
- Native lava-volume regression: each channel applies one contact, excluding bridges and points outside the fluid. This deliberately corrects the source's duplicate channel contact; only hazard schedules claim exact parity.
- Inherited 35 managed combat/network checks, 909 ranged-pose samples and 34 clips / 3,384 captured-pose checks pass. These cover the retained lab code, not new native PhysX gameplay.
- Runtime, Editor and Windows-conditional C# API compilation against actual Unity 6000.3.24f1 managed assemblies, with warnings treated as errors.
- Four existing package dependency regressions; source structure/GUID validation and source ZIP integrity.
- Export reproduced identical content/fixture hashes from the 108 verified source blobs.

The direct Roslyn/.NET runtime route is used because the container cannot run the normal dotnet CLI process-information lookup. CI uses the normal .NET runner. No fake Unity types substitute for engine execution.

## Implemented Windows build gate — not run here

`NativeBuildChecks` parses the actual imported Unity TextAsset, reruns the source parity checks, and creates each of the 15 map layouts in an Editor preview scene. It checks that the physics scene is valid and separate from the default physics scene before simulation. A falling probe verifies that simulation advances; the gate then checks 120 spawn-floor rays, confirms pilots did not fall through, and verifies block counts. It restores the original active scene/gravity and closes preview scenes even if cleanup throws.

### First owner build log (2026-10-06, 21:32 EDT)

Unity 6000.3.24f1 imported the source and passed all eight native content/rule/match groups, including the 60 source parity cases / 4,620 values. The build then failed before any map simulation with `InvalidOperationException: This can only be used during play mode, please use EditorSceneManager.NewScene() instead.` The call was `SceneManager.CreateScene` in the Editor build check. No EXE completion was reported. Licensing warnings recovered before these checks and were not the terminating error.

The repair uses the public `EditorSceneManager.NewPreviewScene` / `ClosePreviewScene` pair, preserves the physics gate, verifies scene isolation and simulation progress, and prints a result for each map. The repaired Editor code compiles against actual 6000.3.24f1 assemblies. Native execution of the repair still requires the next Windows build log; a successful compile is not a successful native build.

API references: [Editor preview scenes](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.NewPreviewScene.html), [preview cleanup](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.ClosePreviewScene.html), [Edit-mode physics simulation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.Simulate.html).

The existing native clip-binding, shader, audio import, icon and Windows package checks also run before the EXE is produced. A successful managed compile does not establish that these engine checks pass. A failed gate stops packaging; inspect Builds/build.log rather than bypassing the check.

## Remaining acceptance

- Unity import, shader compilation, native scene/physics gate, actual UI layout, sound and visuals.
- Capsule movement on ramps, stairs, moving props and upper floors; camera obstruction, dynamic support release, projectile collision and ground/ceiling behavior.
- First-person stability while idle, casting, moving, jumping/landing and turning at different display rates; pause/resume, V toggle, round reset and rifle scope/ADS transitions.
- First-person framing for all weapons/ADS/reload/block, toggle persistence, camera obstruction, source magic poses and shadow visibility. Test staff guard transitions, moving casts, different aspect ratios and extreme camera angles.
- Combat glow Full/Low/Off and saved setting, nearby-wall light spill, surface marks following moving cover, sustained gunfire/spell bursts, destroyed-cover cleanup and round resets. Judge actual brightness, transparency and frame time on the target GPU.
- All eleven ability handlers: cancellation/cost/cooldown, target previews, close-cover collision, bounce/reflection, pierce, blink clearance, tracking, timed fields and status/proc interactions.
- Primary weapon/roll/block contacts and frame timing, status effects, hazards, complete rounds/sets/draft/rematch.
- Windows EXE launch/icon and isolated preferences. New multiplayer is not implemented yet; old TCP tests are not a substitute.
- Full mechanic, presentation, online, account and save compatibility; all pending rows in CONVERSION-STATUS.md.

No production update, live endpoint, save migration or automatic release was performed.

## Melee / riposte checkpoint (2026-10-07)

Passed here: 64 original-source swing fixtures / 448 timing and footwork samples / 5,376 contact candidates, 90 block/riposte cases and nine external-motion cases. The exporter verifies six pinned Git blobs before evaluating original TypeScript functions and sweep expressions. Counter-steering retains the source's pre-clamp external-speed calculation. Additional checks cover contact deduplication, occlusion not consuming contacts, integrated step distances, block clamps, rebound cap and non-repeating lift. Existing first-person, ability, content, animation and managed lab regressions remain passing.

Runtime, Editor and Windows-conditional compilation against actual Unity 6000.3.24f1 assemblies pass with warnings as errors. Package metadata checks and four package regressions pass. The source fixture export is deterministic. These are code/source checks; no native Unity execution or EXE was produced here.

The added Windows gate calls actual NativeArena handlers inside the isolated preview physics scene: all four primary hits, a target entering an active sweep, repeat-hit prevention, cover occlusion, teams, roll i-frames, human vs bot riposte, failed counters preserving protection, counter stagger, early/late roll-cancels, committed actions and native impulse assignment. It is compiled but **unrun here**. Melee visibility queries use the root's PhysicsScene and expand their nonalloc buffer on saturation. See [PhysicsScene.Raycast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.Raycast.html).

Owner acceptance: one same-weapon bot with Stationary targets off; Sword block/counter, Daggers two-hit/escape behavior, Greatsword windup/recovery roll-cancels, all four sweeps at walls/height differences, team protection, and first/third-person animation alignment. Compare knockback for guns and magic as well, since external velocity handling is shared. E specials and Shield knockdown remain unavailable, so do not use this milestone as their acceptance test.

## Melee E / recovery checkpoint (2026-10-07)

Passed here: nine verified source blobs evaluated by `tools/export_specials.mjs`; 96 start-gate cases, 324 strike filters, 216 recovery samples, 36 ballistic steps, 24 ceiling contacts and 108 special-animation envelopes. The source start adapter supplies data/resource methods only; no Rapier world is created. An additional 2,400 native special/recovery pose samples check finite unit rotations, deterministic replay and voxel floor clearance. All inherited managed checks pass.

Runtime, Editor and Windows-conditional player C# compile against real Unity 6000.3.24f1 assemblies. Four package tests and GUID/dependency validation pass. This is not a native Unity execution result.

Added **unrun** Windows build gates exercise actual NativeArena handlers and isolated PhysicsScene queries: E resource/commitment gates, launch and single pickup, two-hit air cap, protected ground recovery and latched roll direction, Greatsword real landing/shared target ledger, swept dagger/shield contacts, starting overlap and cover, reflection caps, harmless return/catch and dead-owner shield cleanup. Native clip repeat-frame checks now include launcher and Greatsword. Projectile glow lifecycle also includes a thrown shield.

Sphere casts are paired with explicit starting-overlap queries because a cast alone cannot detect every initial overlap; cover takes precedence if both cover and a target contain the spawn. See [Unity PhysicsScene.SphereCast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.SphereCast.html). No native PhysX/rendered gameplay, activated Editor build, or Windows EXE ran in this workspace. Build and playtest the four E moves and recovery before accepting feel or visual parity. Full source body/head volumes, planted recovery IK and physical ragdolls remain pending.

## Broad conversion checkpoint, 2026-10-07

Passed: complete inherited managed suites; 24 PvE scaling cases and six source wave schedules;
72 original-engine wound cases, 36 horde hit distributions, 80 exact PRNG outputs and 1,001 music positions;
debris energy/contact/group caps; bounded transport compression/UTF-8, two real loopback clients,
ordered messages, large snapshot delivery, disconnect/reconnect and bad-magic rejection. Four package dependency regressions pass.
Runtime, Windows-conditional player and Editor C# compile against Unity 6000.3.24f1 assemblies with warnings as errors.

Asset gates now require the original 83 audio files, source models, 159 motion clips, full draft catalog,
and error-free Unity source shaders. These **native gates have not run here**. Source audio and export files are
locked by size/Git blob hash in tools/asset-lock.json; unexpectedly large unlisted files still fail packaging.
No activated Editor, native PhysX/rendered test, live account service, internet/LAN session between computers,
or Windows EXE ran in this workspace. These checks do not certify complete conversion or visual parity.


## 2026-10-07 — entry, replay, account and relay checkpoint

- Managed source checks pass, including 241 original vent-climb samples, six entry timelines, 96 weighted-entry cases and vent boundary conditions.
- Real local WebSocket relay integration passes: public discovery/private exclusion, two isolated clients, 20 ordered messages, a 130 KB snapshot, source mismatch rejection and disconnect cleanup. TCP checks also pass.
- Runtime and Windows-player C# compile against real Unity 6000.3.24f1 reference assemblies; Editor C# also compiles. The .NET 8 relay compiles and runs. This container's dotnet CLI/MSBuild process inspection fails; equivalent Roslyn compilation and direct runtime execution were used locally. CI uses ordinary SDK builds.
- Four package tests and the asset receipt/metadata check pass. Sources now include 79 model exports, 165 source motion tracks, Ward moving-part metadata and all 83 original audio assets.
- Added native entry queues/travel/falls, original sky shader port, settings, lock-on, spectator/replay, developer practice/admin UI, relay/discovery and headless authority source. Native visual, UI, PhysX, headless Unity, live-account and Windows EXE execution remain unrun. Local relay tests are not evidence of a deployed internet service or TLS proxy test.
- Shader import gates now include the Ward pit and night sky. No native runtime acceptance or complete presentation equivalence is claimed.

## Source cinematic checkpoint (2026-10-07)

Four asset tests cover all 31 original sampled scenes / 124 native models: finite and correctly sized tracks, noncollapsed geometry, the clown weapon drop, the Siren blackout and animated demon/flame instances. Ragdoll forces, preview/inspection/draft UI and scene lighting compile against the real Unity Runtime, Windows-player and Editor assemblies. These checks do not execute shaders, render images, play audio or verify native body physics. Original source receipts and prior checks are retained.


## Native updater programming — 2026-10-08

CI run 37712635437 on `5d657a1` passed source/managed/real-relay verification,
18 Python package/publisher tests, 51 updater transaction/archive checks on both
Linux and Windows, and real .NET Framework launcher compilation plus Windows
process install/restart and startup-failure rollback fixtures. This found and
fixed Windows short-path executable identity handling. The final branch adds a
real-launcher interrupted-directory-swap recovery fixture to the same CI gate.

The new Unity runtime/UI and Editor packaging code have not been compiled or run
in an activated Unity Editor here. Native build/import/rendered acceptance and
an actual Unity-to-Unity update are still required. The source package contains
no executable and no compiled native feed has been published. Existing genuine
Unity compilation results elsewhere in this document predate this updater.
See `Updater/README.md` for release steps and the Claude UI integration contract.


### Owner build follow-up: Editor packaging compile repair

The owner's October 8 01:55 UTC Unity 6000.3.24f1 log shows successful
Editor-target runtime script compilation, then three CS0104 errors for the
ambiguous `CompressionLevel` name in `NativeUpdateBuild.cs`. An explicit
`System.IO.Compression.CompressionLevel` alias fixes those three references.
The corrected Editor assembly and Windows player have not been built here;
the owner must rerun `Build-Windows.cmd`. Launcher CI does not compile Unity
Editor packaging code and therefore did not catch this collision.


### Owner build follow-up: launcher source paths

The owner's subsequent `build.log` confirms the compression alias compiled,
all logged native gates passed (including presentation initialization, all 15
maps/120 spawn floors, gameplay and animation playback), and the Windows Unity
player build returned Success. The later launcher packaging step failed with
CS1504; it resolved the four launcher source filenames at the project root.
The final starter ZIP/feed completion markers were therefore absent.

The packager now calls the shared `NativeLauncherCompiler` implementation,
normalizes absolute Windows paths, validates source/configuration inputs, sets
an explicit working directory, and invokes csc through a UTF-8 response file.
Its Windows gate uses the exact implementation with mixed path separators,
spaces, an ICO and real source files; then exercises the resulting launcher.
Missing input is checked before the player build starts. Compiler diagnostics
identify `Builds/native-launcher.rsp` if a further failure needs investigation.
The revised Unity packaging invocation and actual in-game update remain owner
verification; successful player compilation alone is not complete packaging.

CI 37716445819 at `3036366` passed both native jobs, including the exact Windows
compiler invocation, missing-input rejection and subsequent launcher process tests.
