# Unity cloud builds — Codex handoff to Claude (2026-10-08)

Owner asked to start Unity Build Automation so Windows builds can happen in the cloud,
and explicitly requested this handoff. The original Update button investigation remains
in [UNITY-NATIVE-UPDATE-HANDOFF.md](UNITY-NATIVE-UPDATE-HANDOFF.md).

## Status and branch

- Task: `codex/unity-cloud-build`, based on PR #63 `codex/unity-native-publish`
  at `5331741da76c5ec317cf06006e79ffbd2558dcd0`.
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
| Initial candidate branch | `codex/unity-cloud-build`; pin the pushed commit for the first run |
| Project subfolder | `unity/ChipArenaConversion` |
| Editor | Exactly `6000.3.24f1` from ProjectVersion.txt |
| Builder OS | Windows (launcher compilation uses the installed .NET Framework compiler) |
| Target | Windows standalone, x86_64; graphical client, not dedicated server |
| Scripting backend | Mono |
| Development build | Enabled, matching Build-Windows.cmd |
| Scene | `Assets/ChipArena/Scenes/Prototype.unity` |
| Pre-export method | `ChipArena.UnityConversion.Editor.NativeCloudBuild.PreExport` |
| Post-export method | `ChipArena.UnityConversion.Editor.NativeCloudBuild.PostExport` |
| Post-build script (from repository root) | `unity/ChipArenaConversion/tools/cloud_post_build.sh` |
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
4. The post-build Bash script converts Cygwin paths for Windows Python. Python verifies
   that receipt belongs to this job, both success markers and the run-specific export
   marker appear in the real log, and the starter's complete player bytes match the
   verified update archive. It includes only the current manifest's payload parts.
5. It adds `Chip-Arena-Native-Cloud-Delivery.zip` and its SHA-256 sidecar alongside UBA's
   raw exported EXE, for collection inside the normal Windows build artifact. **Verify
   this collection behavior on the first real UBA run.** A successful source CI job is
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

- Local: eight delivery tests pass; 15 publisher tests pass, with the Windows command
  test skipped on Linux. Shell syntax and whitespace checks pass.
- New C# staging checks run in the existing updater test executable on Linux and Windows;
  Windows also runs all eight delivery tests. Record the resulting GitHub run/commit below.
- Actual Unity hook compilation/import, Windows UBA export, .NET launcher tool availability,
  environment/path conventions, artifact collection and rendered gameplay remain unrun
  until the account is configured. The hooks deliberately fail if provider context is
  absent or different rather than label an unidentified build as accepted.
- First UBA run: inspect its log for all three markers, source SHA and completed delivery;
  download and verify the sidecar; inspect the receipt against the requested commit.
  Download the full UBA log too. Fix any observed integration failure before accepting it.
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
