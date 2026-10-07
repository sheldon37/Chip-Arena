# Validation — native conversion foundation

No native Unity playtest or Windows executable build was run in the authoring workspace.

## Passed

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

`NativeBuildChecks` parses the actual imported Unity TextAsset, reruns the source parity checks, and creates each of the 15 map layouts in a separate Unity physics scene. It simulates settling, checks 120 spawn-floor rays, confirms pilots did not fall through, and verifies block counts. It restores the original active scene/gravity and removes its temporary scenes.

The existing native clip-binding, shader, audio import, icon and Windows package checks also run before the EXE is produced. A successful managed compile does not establish that these engine checks pass. A failed gate stops packaging; inspect Builds/build.log rather than bypassing the check.

## Remaining acceptance

- Unity import, shader compilation, native scene/physics gate, actual UI layout, sound and visuals.
- Capsule movement on ramps, stairs, moving props and upper floors; camera obstruction, dynamic support release, projectile collision and ground/ceiling behavior.
- Primary weapon/roll/block contacts and frame timing, status effects, hazards, complete rounds/sets/draft/rematch.
- Windows EXE launch/icon and isolated preferences. New multiplayer is not implemented yet; old TCP tests are not a substitute.
- Full mechanic, presentation, online, account and save compatibility; all pending rows in CONVERSION-STATUS.md.

No production update, live endpoint, save migration or automatic release was performed.
