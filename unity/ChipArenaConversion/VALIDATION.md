# Validation — native Unity conversion

No native Unity playtest or Windows executable build was run in the authoring workspace.

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
