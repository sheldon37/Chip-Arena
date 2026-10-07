# Validation — native conversion camera/ability checkpoint

No native Unity playtest or Windows executable build was run in the authoring workspace.

## New checkpoint checks (2026-10-07)

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
- First-person framing for all weapons/ADS/reload/block, toggle persistence, camera obstruction, source magic poses and shadow visibility.
- All eleven ability handlers: cancellation/cost/cooldown, target previews, close-cover collision, bounce/reflection, pierce, blink clearance, tracking, timed fields and status/proc interactions.
- Primary weapon/roll/block contacts and frame timing, status effects, hazards, complete rounds/sets/draft/rematch.
- Windows EXE launch/icon and isolated preferences. New multiplayer is not implemented yet; old TCP tests are not a substitute.
- Full mechanic, presentation, online, account and save compatibility; all pending rows in CONVERSION-STATUS.md.

No production update, live endpoint, save migration or automatic release was performed.
