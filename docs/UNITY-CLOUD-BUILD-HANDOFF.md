# Unity cloud builds — Codex handoff to Claude (2026-10-08)

Owner asked to start Unity Build Automation so Windows builds can happen in the cloud,
and explicitly requested this handoff. The original Update button investigation remains
in [UNITY-NATIVE-UPDATE-HANDOFF.md](UNITY-NATIVE-UPDATE-HANDOFF.md).

## Status and branch

- **Current candidate (2026-10-08 evening EDT): `codex/unity-cloud-current`, based on
  shared `2604b2ae587b0af3a5378cd52bf10dba93143515`.** It preserves Claude's latest C11
  weapon presentation, carries the four PR #67 build-check/rescue repairs from `9903a66`,
  and reapplies the publisher/cloud tooling from PRs #63/#64. Use this candidate for the
  cloud runs; the original #64 branch predates the parity and presentation changes.
- Owner has created the Chip Arena cloud project and completed its first inspected build.
  Organization ID: `2474084689228`; project ID: `06275870-8c41-42b2-b21a-f75ff362f3dc`.
  These non-secret IDs are observed in the owner's dashboard. Build #2 confirms target
  ID `chip-arena`. Platform is Windows desktop 64-bit.
- Dashboard shows the free tier, including 200 Windows Micro minutes monthly. Use the
  included Windows Micro machine and keep automatic builds off during acceptance.
  No paid upgrade or spend is authorized. GitHub source connection uses repository-scoped
  Contents read-only access; the owner enters credentials directly in Unity's dashboard.
- The native test feed is already published at channel commit `c5251fb` and the owner
  reports an older installation updated successfully. That candidate came from the
  owner's local build of shared `5900583` plus PR #67 v3; it does **not** validate the
  newer C11 source or the cloud hooks. Builds #2/#3 validate the refreshed candidate's
  Unity export and Editor packaging. Build #3 runs the script but fails receipt lookup;
  the path-handling repair below is implemented, awaiting another cloud run. Unreleased.
- Current local tooling checks: 27 publisher/delivery tests collected, 26 pass and the
  Windows command-entrypoint case skips on Linux; Bash syntax and diff checks pass.
  Full source/relay/package and UpdaterTests run in GitHub CI on the pushed candidate.
  Builds #2/#3 passed the Windows Unity export and launcher compilation. Build #3 confirms
  Python is available; corrected path handling, delivery validation and collection still
  need another UBA run. No working Unity API client is claimed.

GitHub native CI [37866917576](https://github.com/sheldon37/Chip-Arena/actions/runs/37866917576) passes on implementation `8a99e962554887f758f4edc7132199aceb40f38d` (draft PR #68): Linux check_relay.py, full Python suite (42 collected, 41 pass, one Windows-only skip), package_source.py (737 files), cloud staging and 51 UpdaterTests; Windows all 16 publisher and eight cloud-delivery tests, staging/51 updater checks, shipping launcher compilation and real fixture-player install/restart/acknowledgement, rollback and interrupted-swap recovery. These are source/helper checks, not an actual Unity build. The follow-up commit records results in documentation only.

Owner setup checkpoint (2026-10-08 21:03 EDT): the owner reports all requested target settings completed except a Scripting backend selector, which was not present. The supplied PDF independently shows the correct branch, project subfolder, Unity 6000.3.24f1 and Windows 11 24H2 builder. Code confirms PreExport explicitly selects Mono and PostExport rejects a backend mismatch, so no additional dashboard field is needed. The inspected run below supersedes the earlier pending-build status; billing tier and automatic-build settings remain owner-reported.

## Build #3 receipt and path-handling repair (2026-10-08 22:25 EDT)

- Build #3 checked out `c2baa210251f441c4f1d8d97e7da04dc0ed54977`; Unity passed the
  same 86 checks, both export hooks and all three Editor packaging markers. At log
  line 8893 it **executed `tools/cloud_post_build.sh`**, confirming the dashboard path
  correction. Keep this project-relative setting and the existing export methods.
- At line 8895 Python failed with `No such file or directory:
  'BUILD_PATH/Builds/cloud-build-receipt.json'`. The actual Editor output is under
  `BUILD_PATH/p/unity/ChipArenaConversion/Builds`. This proves a project-root lookup
  defect. The old wrapper always passed Windows-backslash paths to whichever `python`
  was on PATH, while Python derived its root with platform-dependent pathlib. Cygwin's
  POSIX Python misinterpreting those paths explains the symptom; build #3 did not log
  the interpreter platform, so that precise runtime diagnosis remains inferred.
- Repaired `cloud_post_build.sh` to select POSIX/native paths from Python's `os.name`,
  pass the project root explicitly with required `--project`, and preserve failures in
  path conversion and the Python exit code. `package_cloud_build.py` no longer derives
  its project from `__file__`; it converts Unity's native Windows Editor-log path when
  running on Cygwin. It logs the interpreter platform and selected project for the next
  actual run. Receipt/identity/hash/starter validation and no-publication behavior stay intact.
- Three added regression tests failed before the repair and pass afterward: CLI from an
  unrelated working directory, Cygwin conversion of the Windows Editor-log path, and the
  actual Bash-to-Python entrypoint with spaces/real fixture delivery plus missing-receipt
  nonzero exit. Cygwin path conversion is emulated locally, not a real Cygwin execution.
  Local scoped publisher/delivery suite: **27 collected, 26 pass, one Windows-only skip**;
  Bash syntax and diff whitespace pass. Full source/Windows helper CI is pending this push.
- Despite the Python failure, UBA ended SUCCESS again. Require the fourth
  `NATIVE_CLOUD_DELIVERY_READY` marker **and** a validated delivery ZIP; the green provider
  badge alone is insufficient. The upload has only 228 raw-player files / 191,590,212
  unpacked bytes (CRC pass), no launcher, starter, feed, receipt or delivery.
- Upload receipts: `2474084689228-chip-arena-chip-arena-3.log`, 1,488,679 bytes,
  SHA-256 `1b814d846477261078ddaba28d304affc654078683d2773462bf29ceeed4d9f7`;
  ZIP of the same stem, 99,717,380 bytes,
  SHA-256 `180d3c9d4b50788ac00777781d94ed10f6b3ec22ac6d1508db548d223626a052`.
- Next: once this repair is pushed and its source checks pass, run the current
  `codex/unity-cloud-current` again with the already-corrected dashboard settings.
  Inspect the new full log, delivery sidecar/receipt and starter/feed before accepting or
  publishing. No owner-PC build is needed. Actual provider execution/collection and
  Windows gameplay/update acceptance remain open; current native feed is unchanged.

## Build #2 receipt — game built, delivery missing (2026-10-08 21:41 EDT)

- Uploaded full log confirms source `8fd2f78d84695f4f6fc0fafe2967e0fda5b707da`,
  target `chip-arena`, build `2`, Unity `6000.3.24f1` and Windows x64. Both export hooks
  ran; 86 PASS lines include real JsonUtility, all 15 maps / 120 spawn floors and the
  startup/glow/headshot/rescue gates. Unity exited 0; UBA ended `Finished: SUCCESS`.
- `NATIVE_UPDATE_FEED_READY`, `UNITY_TEST_BUILD_READY` and the matching
  `NATIVE_CLOUD_EXPORT_READY` are present. The Editor prepared the launcher/starter/feed
  on the builder. This establishes actual cloud compilation/export, not a Windows playtest.
- At log line 9803, UBA warns `Script configured, but not found at
  unity/ChipArenaConversion/tools/cloud_post_build.sh. Skipping.` There is no
  `NATIVE_CLOUD_DELIVERY_READY` marker. The downloaded ZIP has only 228 raw-player files
  (191,590,212 unpacked bytes), with no stable launcher, starter, feed, receipt or delivery.
  Its full CRC check passes, but it is **not an accepted updater delivery**.
- Correct the target's **Post-build script** to `tools/cloud_post_build.sh`, relative to
  the configured Unity project subfolder. The previous repository-relative instruction
  was wrong for this target. This diagnosis is inferred from the observed missing-file
  warning and project layout: the old path does not exist under the project, the shorter
  path does. Unity's general custom-script documentation says repository-relative, so
  retain this discrepancy and confirm the correction with the next actual run.
- Owner must save that dashboard field and start another build on `codex/unity-cloud-current`;
  this session cannot access Unity Cloud to edit the setting. Keep both export methods.
  Inspect the next full log and ZIP for the delivery marker, sidecar, portable receipt and
  matching starter/feed hashes before acceptance. Windows gameplay/update acceptance is
  still open. No local-PC rebuild, feed publication or merge was performed.
- Input receipts (no raw provider logs or binaries committed): log
  `2474084689228-chip-arena-chip-arena-2.log`, 1,618,352 bytes, SHA-256
  `4411960258badaaa37e1240fe4e97040ebd96a0cba0e76c5dde34dba7c507bc0`;
  ZIP `2474084689228-chip-arena-chip-arena-2.zip`, 99,717,383 bytes, SHA-256
  `c50614c48fae8aaea3a7ccb12bf99522f03af0129a268a2ec41d1af8293a5729`.

The following is the original tooling implementation history, superseded by the
current candidate/account status above:

- Task: `codex/unity-cloud-build`, based on PR #63 `codex/unity-native-publish`
  at `5331741da76c5ec317cf06006e79ffbd2558dcd0`.
- Draft PR [#64](https://github.com/sheldon37/Chip-Arena/pull/64); implementation commit
  `905ab8da171c1f9cb5976d83c2cda002faf556f3`. Stacked on #63; neither is merged here.
- Preserves Claude's corrected handoff from `e36253af63824c0b2d8178c2ecd2a5858bac0134`
  and PR #63's publisher follow-up. That Claude commit changes documentation only.
- Implemented: cloud export hooks, Windows player staging, validated delivery packaging,
  commit/build provenance and Linux/Windows CI coverage. The local build command reuses
  the same final packaging method; it retains its existing build settings.
- No gameplay, animation, hit timing, hitboxes, damage, cooldowns, movement, network,
  saves, menu UI or runtime updater behavior changed.
- **Not activated:** no Unity organization/project/target has been configured, no service
  credential obtained, no paid build started, no actual cloud player produced, no merge
  or native-feed publication. GitHub's workflow still checks source/helpers only.
- Account blocker: no Unity plugin was found in the available connector search. Opening
  Unity Cloud was rejected by the cloud browser's URL security policy before account
  access. Do not retry via a browser/network workaround. Continue with an authorized
  Unity connection or owner-completed account setup.

## One-time Build Automation configuration

Use Unity's dashboard for the owner's organization and project. Record their non-secret
IDs and the resulting build configuration ID in this handoff after setup. If DevOps
activation requires new terms, subscription or payment, the owner must handle those
account decisions; no budget or payment authorization was provided here.

| Setting | Value |
|---|---|
| Source | Private GitHub repository `sheldon37/Chip-Arena` |
| Initial candidate branch | `codex/unity-cloud-current`; record the full pushed commit for the first run |
| Project subfolder | `unity/ChipArenaConversion` |
| Editor | Exactly `6000.3.24f1` from ProjectVersion.txt |
| Builder OS | Windows; choose included Micro machine (launcher needs the installed .NET Framework compiler) |
| Target | Windows standalone, x86_64; graphical client, not dedicated server |
| Scripting backend | Mono; set automatically by `NativeCloudBuild.PreExport`, checked again in `PostExport`; no dashboard selector required |
| Development build | Enabled, matching Build-Windows.cmd |
| Scene | `Assets/ChipArena/Scenes/Prototype.unity` |
| Pre-export method | `ChipArena.UnityConversion.Editor.NativeCloudBuild.PreExport` |
| Post-export method | `ChipArena.UnityConversion.Editor.NativeCloudBuild.PostExport` |
| Post-build script (from the Unity project subfolder; verified by build #3) | `tools/cloud_post_build.sh` |
| First run | Clean build; Library caching only afterward |
| Automatic branch builds | Leave off for initial acceptance; one concurrent build |
| Feed write credential | None needed by this candidate build |

Grant Unity source access only to this repository with read access to contents (or the
equivalent repository deploy key). GitHub webhook write access is needed only if automatic
push-triggered builds are deliberately enabled later. Keep credentials in the provider's
secure settings, never source control, handoff notes or chat. The source credential does
not need write access to the separate public update-channel repository.

Do not point at `main`, the Electron shared branch or an old source ZIP. Confirm the
chosen source commit includes this integration and Claude's menu code. Initial machine
selection and any billed build require the owner's account/cost decision.

## Build behavior and artifacts

1. Pre-export requires the pinned Editor, Windows x64 and cloud build context, selects
   Mono/Development, and runs `PrototypeBuild.Validate()` (core, import, shader and native
   build gates). It invalidates old cloud receipts before checking.
2. Unity Build Automation builds its player once. Post-export copies that output into
   `Builds/Windows/app`, renaming its EXE and matching `_Data` directory together for the
   existing updater. The original export stays intact. Links, incomplete players,
   overlapping paths, nonempty destinations and name collisions stop packaging.
3. The existing packager compiles the stable launcher and creates the starter ZIP and
   native feed. A receipt ties their identity to `BUILD_REVISION`, `BUILD_TARGET`,
   `UCB_BUILD_NUMBER`, Editor version and the real Editor log path.
4. The post-build Bash script selects native Windows or POSIX paths to match Python,
   and passes the Unity project root explicitly. Python converts the Editor-log path
   under Cygwin and verifies that the receipt belongs to this job, both success markers and the run-specific export
   marker appear in the real log, and the starter's complete player bytes match the
   verified update archive. It includes only the current manifest's payload parts.
5. It adds `Chip-Arena-Native-Cloud-Delivery.zip` and its SHA-256 sidecar alongside UBA's
   raw exported EXE, for collection inside the normal Windows build artifact. **Verify
   this collection behavior on the next real UBA run.** A successful source CI job is
   not evidence that UBA collected the delivery.

The delivery contains the starter ZIP, `NativeUpdateFeed/`, `build.log`, the matching
`Windows/app/native-build.json` and `cloud-build-receipt.json`. The portable receipt
records full Git commit, cloud target/build number, Unity version, native source identity,
starter/update/log hashes and `published: false`; it omits the agent-local log path.
The native content hash is still distinct from the Git commit and includes Unity-written
settings. Cloud builds do not contain a developer's ignored LocalAssets.

Use the **starter ZIP inside the delivery** for installation. The outer raw Unity artifact
is not the updater installation layout. Keep an older starter installed outside project
build folders to test upgrades; never overwrite it with the next starter.

## Validation and activation checklist

- Local: eleven delivery tests pass; 15 publisher tests pass, with the Windows command
  test skipped on Linux. Shell syntax and whitespace checks pass.
- GitHub [run 37784636310](https://github.com/sheldon37/Chip-Arena/actions/runs/37784636310)
  passes on `905ab8da171c1f9cb5976d83c2cda002faf556f3`: Linux source/managed/relay,
  42 Python tests (one Windows-only skip), 623-file source package, C# staging fixtures
  and 51 updater transaction checks; Windows C# staging, all 16 publisher tests, all eight
  delivery tests, shipping launcher compilation and real fixture-player restart/rollback.
- Build #2 confirms actual Unity hook compilation/import, Windows UBA export, .NET launcher
  availability and the three Editor markers. Build #3 confirms the corrected Bash
  script path but exposes wrong Python project-root lookup. Repaired interpreter/path
  handling and final collection remain unverified. UBA can report success after either
  a missing script or a Python packaging failure; require the fourth delivery marker and ZIP contents.
- Next UBA run after the build #3 code repair: inspect all four markers, source SHA and completed
  delivery; verify the sidecar and portable receipt against the requested commit. Download
  the full UBA log too. Fix any observed integration failure before accepting it.
- Launch the inner starter on Windows. Perform menu/match/settings/account checks and
  then an older-to-newer update, cancellation, room gating and startup rollback. Issue #60
  still contains native networking/sign-in findings which may block these checks.

## Publishing an accepted cloud candidate

Cloud build hooks have **no publication side effect**. After native acceptance and the
owner's authorization, an agent with the downloaded delivery and the existing channel
Git credential can run PR #63's publisher. From the Unity project folder:

```text
python tools/publish_native_update.py --feed <delivery>/NativeUpdateFeed --build-log <delivery>/build.log --status
python tools/publish_native_update.py --feed <delivery>/NativeUpdateFeed --build-log <delivery>/build.log --channel-checkout <channel-checkout> --publish
```

The delivery's matching identity file makes `--build-log` portable. Record accepted source
commit, cloud build number, content identity, channel commit and actual raw-feed verification
in DEV_LOG. The public feed remains isolated at `native/windows-x64`; preserve Electron's
`feed/`. Do not broaden publication authorization or add a write token to every cloud build.
Automated triggering/status/download through Unity's API can be connected once an approved
service connection and organization/project/configuration IDs are available; no working
cloud API client or account connection is claimed by this change.

## Official references checked during implementation

- [Build configuration](https://docs.unity.com/en-us/build-automation/basic-build-configuration/overview)
- [Custom hooks and Windows path conversion](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process)
- [Provider environment variables](https://docs.unity.com/en-us/build-automation/reference/available-environment-variables)
- [Source access setup](https://docs.unity.com/en-us/build-automation/get-started-with-build-automation/connect-your-version-control-system)
- [Supported builders and platforms](https://docs.unity.com/en-us/build-automation/reference/supported-platforms-on-each-builder-os)
