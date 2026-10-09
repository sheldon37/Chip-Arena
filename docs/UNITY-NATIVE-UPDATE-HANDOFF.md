# Unity native update button — handoff from Claude to Astra (2026-10-08)

## Current checkpoint — 2026-10-08 evening EDT

The original investigation below is historical. The owner's Windows build with PR #67 v3 passed all build/package gates; the approved native candidate `83e6a566` was published at channel `c5251fb` and its complete public download verified. The owner reports the older installation updated successfully; extended acceptance remains open. See DEV_LOG for exact identity, hashes and limitations.

To remove local-PC builds, use the refreshed `codex/unity-cloud-current` candidate on shared `2604b2a`, which includes Claude C11, the four PR #67 repairs and PRs #63/#64's tooling. The owner is configuring the free-tier Unity Cloud project. First actual cloud build and artifact acceptance remain pending. Dashboard settings, non-secret project IDs and next steps are in [UNITY-CLOUD-BUILD-HANDOFF.md](UNITY-CLOUD-BUILD-HANDOFF.md). No new release or source merge occurred.

## Original investigation

Owner request (Sheldon, 2026-10-08): "Why isn't the update button grabbing this update?", then
"Can you create a passdown note for Astra? I can probably have it set this up."

Goal for Astra: make the Unity update button deliver new Unity builds to an installed copy, end to end.
Native feed publication is an owner step: `Updater/README.md` says "After native acceptance, the owner can publish",
and `PROJECT_STATE.md` says update acceptance is required first.

All `tools\…` and `Builds\…` paths below are relative to the Unity project folder `unity\ChipArenaConversion`
(or the extracted `ChipArenaConversion` folder). The repo-root `tools/` does not contain these scripts.

## Where things stand (verified 2026-10-08 ~12:50 UTC)

| Item | State |
|---|---|
| Branch | `claude/sleepy-ritchie-g8ruhv`, draft PR #62, base `codex/unity-updater` (PR #61 at `6235a82`). Code head `7bd4e02`; this note is a docs-only commit on top. |
| Updater code | `NativeUpdater.cs`, `NativeUpdateRules.cs`, `Updater/Launcher.cs` and `UpdateInstaller.cs`, `NativeUpdateBuild.cs`, `PrototypeBuild.cs` and `publish_native_update.py` are unchanged by Claude. `Updater/README.md` gained a 3-line UI note (`4fbd54c`), and `NativeBuildChecks.cs` gained a hazard-model gate. |
| Native feed | `https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/latest.json` returns **404**. The channel repo holds only `PUBLISHED.md`, `README.md` and `feed/` (Electron). No commit has ever touched `native/`. |
| Channel repo | Public (anyone with the URL can download). `main` = `5ddab5b`, about 1.15 GB. The cloud clone at `/home/user/chip-arena-test-updates` is at `72dfa69`, 4 commits behind; a fast-forward is enough. |
| PR #62 CI | `scope` passes. `verify` fails only on the TypeScript server build (4 errors) and about 12 unit failures, the same as the base `6235a82` (run 37716636624). PR #2 fixes the server build only. |
| Unity CI | `.github/workflows/unity-conversion.yml` runs on pushes to `codex/unity-conversion`, `codex/unity-build-fix` and `codex/unity-updater` that touch `unity/ChipArenaConversion/**`. `workflow_dispatch` is declared, but the workflow is not on `main` and has never been dispatched, so check that it can run on this branch. It runs relay, managed, Python and updater checks (Linux and Windows) and uploads the source ZIP (30 days). It never builds Unity. |
| Owner's existing build | **Not recorded in the repo.** See "Which build is installed" below. |

## Why the button finds nothing

1. `Build-Windows.cmd` only **prepares** `Builds\NativeUpdateFeed` (`latest.json` + `objects/*.part`) on the build PC.
   `NativeUpdateBuild.cs` logs `NATIVE_UPDATE_FEED_READY … (prepared only; no publication)`. The game never reads that folder.
   It reads only the hard-coded raw GitHub URL above (`NativeUpdateRules.Feed`).
2. With nothing published, the check gets HTTP 404, and `NativeUpdater` sets `Idle` with
   "No Unity update has been published yet." (`NativeUpdater.cs` Check()). Any other non-200 response becomes `Error`.
3. The home menu builds `4fbd54c` to `0250c6f` showed that Idle state as "Update check unavailable", the same text as a disabled
   updater. `7bd4e02` fixes the wording (see UI below). Older GUILayout builds showed it on the card as chip IDLE.

## Rules that decide whether an update is offered

- **The installed copy must be a starter install.** The player runs from `<root>\app\`, and `<root>` holds
  `ChipArena-Native-Conversion-TEST.exe` (the launcher) and `native-install.txt` = `chip-arena-unity/1`.
  Otherwise the state is `Unavailable`, and the status names the cause:
  - a wrong layout gives "Install the Windows starter ZIP once to enable updates.";
  - a junction or symlink at `<root>` or above gives "Install the native game in a regular folder, outside junctions or symbolic links.";
  - a missing native-install.txt or app\native-build.json gives the raw file error;
  - a bad identity gives "This is not a valid native Windows test build.".

  The Unity Editor and non-Windows players stay at "Updates are available in the Windows starter build." Use a plain local
  folder such as `C:\ChipArena\NativeTest`, not an OneDrive-synced Desktop or Documents.
- **Offered only if** `published.version != installed.version && published.revision > installed.revision`
  (`NativeUpdateRules.IsNewer`).
  - `version` = SHA-256 of every file under `Assets/`, `Packages/`, `ProjectSettings/` and `Updater/`, as it is on the build PC's disk at packaging time.
    That is after Unity opened the project and `PrototypeBuild.Prepare()` rewrote settings. It includes Unity-generated files and gitignored `Assets/LocalAssets` if present, and skips bin/obj.
    It carries no git SHA and cannot be mapped back to a commit reliably. Docs **outside** those four folders do not change it
    (`docs/`, the project-root `*.md`). `Updater/README.md` does change it.
  - `revision` = build PC `DateTime.UtcNow.Ticks` at packaging time. Keep the build PC clock accurate.
- **`Builds\Windows` is replaced by every build.** Every `Build-Windows.cmd` run deletes and recreates `Builds\Windows\app` and
  overwrites the starter ZIP. So the printed "Play:" copy is always the newest build and is never offered an update.
  Test from a starter ZIP extracted to its own folder, and never extract a newer ZIP over it.
  A build that fails early deletes `Builds\Windows\app` but leaves the old starter ZIP and the old `Builds\NativeUpdateFeed`.
- **Publisher refusals** (`publish_native_update.py stage()`) compare only with the *current* `native/windows-x64/latest.json`:
  - same version → "This source version is already published";
  - current revision ≥ new → "Refusing to publish an older build".

  So a build cannot be re-delivered while its version is the latest publication, and an install already on a version is never
  offered it again. It does not check published history.
- The automatic check runs **once per launch**. In `7bd4e02` and later, opening the card from the Update Game pill re-checks, and so does
  Check for updates. Raw GitHub caches `latest.json` (and the 404) for about 5 minutes (`max-age=300`).
- `CanInstall` = `State==Available && menu && !Online && (arena==null || match.Phase==NativePhase.Match)`. `Match` means the
  match has ended. `Download()` re-checks the predicate before downloading, after downloading and before restart, and aborts
  if a room or match became active.

## Which build is installed (identify from the screen)

The repo does not record which source the owner built, or whether it reached `UNITY_TEST_BUILD_READY`. Identify it from the UI:

| What the menu shows | Source range | Notes |
|---|---|---|
| A GUILayout form with a plain "Unity updates" section above Quit | `6235a82` / `d185be4` era (Codex) | Updater present. |
| A GUILayout panel moved left, with a separate **UNITY UPDATES card in the bottom-right corner** | `073c72a`–`de94ebe` (includes `2253947`) | Updater present. Old Escape bug (below). |
| The CHIP / ARENA home screen with an **Update Game** pill top right | `4fbd54c` or later | Escape fixed. `7bd4e02`+ has the corrected wording and re-check on open. |

On a working starter install, the card reads "Installed · <12 hex>". If it reads "starter build required" or the chip says
UNAVAILABLE, that copy is not running as a starter install.

**Old Escape bug (GUILayout builds before `4fbd54c`):** Escape on the main menu flips `menu` off. That disables
"Update and restart", but the menu looks exactly the same. If the button is greyed out with "Finish the match and leave
the room before updating." while you are on the main menu, press Esc once (or restart the game) and check that it is
enabled before clicking.

## Fastest owner path to see an update arrive

1. **Before rebuilding**, check whether `Builds\Chip-Arena-Native-Conversion-TEST-Windows.zip` exists from a build whose
   `Builds\build.log` reached `UNITY_TEST_BUILD_READY`. If it does, extract it into a new folder outside the project, for example
   `C:\ChipArena\NativeTest`. Run that folder's top-level EXE once and identify it with the table above. This is the
   **older install**. If no such ZIP exists, build once from any source, install that starter, and continue.
2. Update the source to `7bd4e02` or later. Close Unity, run `Build-Windows.cmd` from `unity\ChipArenaConversion`, and require both
   `UNITY_TEST_BUILD_READY` and `NATIVE_UPDATE_FEED_READY` in this run's `Builds\build.log`. The publisher never reads
   build.log, so it would happily validate a stale feed folder.
   - From git, check out the commit.
   - From a source ZIP, use `tools\package_source.py`, or the CI artifact if the workflow runs on this branch.
3. Optional dry run, into a **fresh empty** scratch folder, never the publish checkout:
   `python tools\publish_native_update.py --feed Builds\NativeUpdateFeed --channel-checkout C:\ChipArena\native-feed-preview`
   This validates the build only. It does not compare with the live feed. Compare `Builds\NativeUpdateFeed\latest.json`
   (version, revision) with the raw URL yourself. Rerunning into the same folder refuses as "already published".
4. Publish. This is the **owner decision**; read the next paragraph first:
   `python tools\publish_native_update.py --feed Builds\NativeUpdateFeed --channel-checkout C:\ChipArena\test-update-channel --publish`
   - **Needs:** Python 3.8+ and git with push rights to `sheldon37/chip-arena-test-updates`, plus a configured git
     `user.name` and `user.email`.
   - **Checkout:** the path must be absent, in which case it is cloned (about 1.15 GB the first time). Otherwise it must be a clean `main`, with no untracked files either,
     whose origin is the approved URL and which is at `origin/main` after the script's own fetch. For a stale checkout, run
     `git -C <checkout> merge --ff-only origin/main`.
   - **What it does:** it commits only `native/windows-x64/` and pushes `HEAD:main` without force.
   - **Recovery:** if the push fails, push the existing commit manually only if `origin/main` has not moved. Otherwise delete
     the checkout (or reset it to `origin/main`) and rerun. If the commit fails, delete or reset the checkout and rerun.
5. Wait until the raw `latest.json` URL returns 200, which can take about 5 minutes. In the installed copy, use Check for updates. The status should read
   "Update available · <first 12 hex of version> · N.N MB". In `7bd4e02`+ builds the badge reads "Update Available!" and
   the chip reads UPDATE READY. Then **Update and restart**: the game saves, closes, installs and restarts on the new build.

**Before publishing, know this:** the channel repo is public. Each native publication adds a full Development Unity player
to its history permanently, and old payload objects are kept. Removing one would need a history rewrite and force-push,
which AGENTS.md forbids on shared branches. The native script writes no `PUBLISHED.md` line and no DEV_LOG entry, so
record the version, revision and source commit in DEV_LOG yourself. Add a `PUBLISHED.md` line in a separate commit if
that convention should cover native builds.

Order matters because `revision` is the packaging time: the installed baseline must be **packaged before** the build you
publish. Build and install the baseline first, then build and publish the target.

## Suggested setup work for Astra (proposals; owner approves)

1. **One-command publish for the owner.** For example, a `Publish-Native.cmd` beside `Build-Windows.cmd`, or a flag, that:
   - checks this run's `build.log` markers and that `Builds\NativeUpdateFeed\latest.json` belongs to this run;
   - compares version and revision with the live raw `latest.json` and explains a refusal before trying;
   - dry-runs into a fresh temp folder;
   - prints the public and permanent warning and asks for a typed confirmation;
   - then runs `--publish` and writes a DEV_LOG and `PUBLISHED.md` record.

   Keep `--publish` explicit. Nothing should publish as a side effect of building.
2. **Baseline install guidance in the tooling.** Have `Build-Windows.cmd` print where the installed test copy should live and
   warn that `Builds\Windows` is replaced every build. Optionally keep a dated copy of each starter ZIP so an older install
   can always be recreated. Consider clearing a stale `Builds\NativeUpdateFeed\latest.json` at build start.
3. **Feed status command.** Print the live feed (version, revision, size) beside the local feed and the installed copy's
   `app\native-build.json`.
4. **Unity CI for this branch.** Confirm `workflow_dispatch` works on `claude/sleepy-ritchie-g8ruhv`, or add the branch to
   `push.branches`, so the owner gets a CI source ZIP of the exact commit to build.
5. **Owner acceptance test** (record in DEV_LOG):
   - update from the older install to the newer build;
   - settings survive;
   - a deliberately failing new build rolls back on startup (README contract);
   - cancel during a download leaves the install unchanged;
   - with an update Available, enter a room and confirm no install starts. The card is only on the Home screen in the new menu, so also start a download and then host a room: the install should abort.
6. **Authorization.**
   - The AGENTS.md standing publication instruction covers "the existing test Update channel" from the verified, approved shared-branch commit (`claude/visual-revision`). `DEVELOPMENT.md` ties that to `feed/`.
   - This Unity branch is neither that branch nor approved, and the native docs make publication an owner step.
   - Treat each native publication as needing the owner's explicit OK unless he extends the standing instruction.

## UI contract after Claude's menu work (keep these working)

- `InitializeUpdater()` is unchanged (`NativeUpdateUI.cs`, the last call in `ConversionGame.Awake`).
- `UpdatePanel(right, top)` (`NativeUpdateUI.cs`) is the update card. The home screen's Update Game pill
  (`NativeHomeMenu.cs` `UpdateControl`) shows a status line, toggles the card, and forces it open while Downloading,
  Verifying, Preparing or Restarting. The pill and card exist only on the Home screen; leaving Home closes the card.
- `7bd4e02`, UI only:
  - Idle badge: "No update published yet" for the 404 status; "Update cancelled" when the status mentions cancel; otherwise "Not checked yet".
  - Unavailable: "Updater off · open for details". The chip reads NOT PUBLISHED in the 404 case.
  - Opening the card calls `CheckForUpdates()` when not Busy and the state is Idle, Current or Error.
- The 404 case is detected by `Status.StartsWith("No Unity update")` (`NotPublished()` in `NativeHomeMenu.cs`). If you change
  that text in `NativeUpdater.cs`, update the check or expose a flag or shared constant.
- Escape on the main menu now steps back instead of clearing `menu`, and `menu` is forced true when there is no match.

## Other open items for Astra (unchanged by this handoff)

- **Issue #60** is the 146-finding Unity review (3 critical, 14 high). Its "fix first" networking and sign-in findings are still open,
  for example guests disconnected at match start and ECDsa missing on Mono, so sign-in and DEV unlocks fail. They may block the account and
  room acceptance checks above. Separately, Claude noted these parity items during presentation work:
  - PvP Siren intro phase is 9.0678 s vs 7.2678 s;
  - bots are queued in intros;
  - `skipBotDrafts` defaults to on;
  - bot draft tie-break;
  - no return-to-lobby countdown;
  - ceiling bounce at y>19;
  - the PvE pick source label is not replicated.
- **TypeScript CI baseline:** the server build errors (PR #2) plus about 12 unit failures and the WebSocket rematch timeout. These are not related to Unity.
- **Not yet verified on Windows:**
  - the new native menu (every screen, at 1080p and 3440×1440);
  - GUILayout panel contents;
  - LAN and relay rooms with a second player;
  - an actual Unity-to-Unity update.

## Files to read first

From `unity/ChipArenaConversion`:
- `Updater/README.md`
- `Build-Windows.cmd`
- `Assets/ChipArena/Editor/NativeUpdateBuild.cs` (PlayerFolder, Package, SourceHash) and `PrototypeBuild.cs` (BuildWindows, Prepare)
- `Assets/ChipArena/Core/NativeUpdateRules.cs` (Feed, IsNewer)
- `Assets/ChipArena/Runtime/NativeUpdater.cs` (Initialize, Check, Download)
- `tools/publish_native_update.py`
- `Assets/ChipArena/Runtime/NativeHomeMenu.cs` (UpdateControl, UpdateBadge) and `NativeUpdateUI.cs`

Also read the newest Claude and Codex entries in `docs/DEV_LOG.md`.
