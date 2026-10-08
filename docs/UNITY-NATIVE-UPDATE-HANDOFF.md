# Unity native update button — handoff from Claude to Astra (2026-10-08)

Owner request (Sheldon, 2026-10-08): "Why isn't the update button grabbing this update?" and then
"Can you create a passdown note for Astra? I can probably have it set this up."
Goal for Astra: make the Unity update button deliver new Unity builds to an installed copy, end to end,
with the owner's approval for every native feed publication.

## Astra follow-up — 2026-10-08

Implemented on `codex/unity-native-publish`, based on this branch at `d804663`:

- `Publish-Native.cmd` validates the completed Windows build and previews exact build identities before
  typed publication confirmation. `Publish-Native.cmd --status` compares the verified local package with
  the public feed without writes. The Python interface remains available.
- Clean behind-only channel checkouts fast-forward; wrong repositories, local changes and ahead/divergent
  histories stop without resetting anything. Publication rechecks the channel after confirmation.
- Build output explains the separate installed baseline and explicitly says the prepared feed is local.
  The source packager includes the new command. Native CI now covers `codex/unity-*` and `claude/**` with
  native path filtering, including publisher tests and a Windows command-entrypoint test.
- PR #63 code `375bd25` passes native Linux and Windows CI (`37781110655`): all 16 Windows publisher
  tests, 51 updater checks, launcher compilation and fixture-player install/restart/rollback. Raw ZIP names
  are now validated before Python filename normalization can hide backslashes/NULs.
- Local publication tests use disposable bare Git repositories and fixture players, never the actual feed.
  A compiled Unity candidate and a real older installed player are still needed for end-to-end acceptance.
- At investigation time the native feed still returns 404. No compiled native update is published by this
  change. Nothing is merged and no gameplay, animation, UI, runtime updater or Electron feed is changed.

Use the updated `Updater/README.md` instructions below the Unity project for the current command workflow.
The original investigation below is retained as the historical handoff.

## Where things stand (verified 2026-10-08 ~09:30 UTC)

| Item | State |
|---|---|
| Branch with the menu work | `claude/sleepy-ritchie-g8ruhv`, head `7bd4e02`, draft PR #62 (base `codex/unity-updater`, PR #61 at `6235a82`) |
| Updater service | Astra's `NativeUpdater.cs`, `NativeUpdateRules.cs`, `Updater/`, Editor packaging. Unchanged by Claude. |
| Native feed | `https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/latest.json` returns **404**. The channel repo holds only `PUBLISHED.md`, `README.md` and `feed/` (Electron). Nothing has ever been published to `native/`. |
| Channel repo `main` | `5ddab5b`. The cloud clone at `/home/user/chip-arena-test-updates` is stale at `72dfa69`, so `--publish` from there would refuse until it is fast-forwarded. |
| PR #62 CI | `scope` passes. `verify` fails only on the TypeScript server build and unit tests, the same as the base `6235a82`. PR #2 fixes the server build only. |
| Unity CI | `.github/workflows/unity-conversion.yml` runs on pushes to `codex/unity-*` only. It never builds Unity; it produces the source ZIP artifact. |

## Why the button finds nothing

1. `Build-Windows.cmd` only **prepares** `Builds\NativeUpdateFeed` (`latest.json` + `objects/*.part`) on the
   build PC (`NativeUpdateBuild.cs` logs `NATIVE_UPDATE_FEED_READY … (prepared only; no publication)`).
   The game never reads that folder. It reads only the hard-coded raw GitHub URL above (`NativeUpdateRules.Feed`).
2. With nothing published, the check gets HTTP 404, and `NativeUpdater` sets `Idle` with
   "No Unity update has been published yet." (`NativeUpdater.cs` Check()).
3. Before `7bd4e02`, the home badge showed that Idle state as "Update check unavailable", the same text as a
   disabled updater. That made it look broken. The badge and card now say what is happening (see UI below).

## Rules that decide whether an update is offered

- **Installed copy must be a starter install.** The player runs from `<root>\app\`, and `<root>` holds
  `ChipArena-Native-Conversion-TEST.exe` (the launcher) and `native-install.txt` = `chip-arena-unity/1`. No
  junctions or symlinks are allowed anywhere up the path. Otherwise the state is `Unavailable` ("Install the Windows starter ZIP once…").
  The Unity Editor is always `Unavailable`.
- **Offered only if** `published.version != installed.version && published.revision > installed.revision`
  (`NativeUpdateRules.IsNewer`).
  - `version` = SHA-256 of every file under `Assets/`, `Packages/`, `ProjectSettings/` and `Updater/` on disk at build
    time (bin/obj skipped). It has no git SHA, and docs-only commits hash the same.
  - `revision` = build PC `DateTime.UtcNow.Ticks` at packaging time. Keep the build PC clock accurate.
- **`Builds\Windows` is never an update target in practice.** Every `Build-Windows.cmd` run deletes and recreates
  `Builds\Windows\app` and overwrites the starter ZIP. So the copy at the printed "Play:" path is always the newest build,
  and it can never be offered itself. Test from a starter ZIP extracted to its own folder, and never extract a newer ZIP over it.
- **Publisher refusals** (`tools/publish_native_update.py stage()`): it refuses if the same `version` is already published,
  or if the published revision is greater than or equal to the new one. So once a build of some source is published, that source can never be
  delivered as an update again. Rebuild changed source instead.
- The automatic check runs **once per launch**. The menu card now re-checks when it is opened from the Update Game pill,
  and "Check for updates" re-checks too. Raw GitHub can cache `latest.json` for about 5 minutes.
- "Update and restart" requires `CanInstall` = `menu && !Online && (arena==null || Phase==Match)`.

## Fastest owner path to see an update arrive

The owner already built once from the source ZIP Claude sent. It was most likely `2253947`, which contains the updater (`d185be4`).
That build can be the **older install**, as long as its starter ZIP has not been overwritten by a newer build:

1. **Before rebuilding**, extract the existing `Builds\Chip-Arena-Native-Conversion-TEST-Windows.zip` into a new folder outside
   the project (e.g. `C:\ChipArena\NativeTest`). Run that folder's top-level EXE once. Its update card (end of the menu panel)
   should read "Installed · …" and "No Unity update has been published yet." This older build has the old GUILayout menu with the update card at the bottom of the menu panel.
   In that build, pressing Esc on the main menu clears `menu`, which disables "Update and restart". Press Esc again,
   or restart, before installing. This bug is fixed in the new menu.
2. Update the project source to `7bd4e02` (git checkout or a fresh source ZIP from `tools/package_source.py`), close Unity,
   and run `Build-Windows.cmd`. Require `UNITY_TEST_BUILD_READY` and `NATIVE_UPDATE_FEED_READY` in `Builds\build.log`.
3. Optional dry run (writes only into a scratch folder):
   `python tools\publish_native_update.py --feed Builds\NativeUpdateFeed --channel-checkout C:\ChipArena\native-feed-preview`
4. Publish, **owner decision**:
   `python tools\publish_native_update.py --feed Builds\NativeUpdateFeed --channel-checkout C:\ChipArena\test-update-channel --publish`
   This needs Python 3.8+ and git with push rights to `sheldon37/chip-arena-test-updates`. The checkout path must be absent
   (it is cloned) or a clean `main` at `origin/main`. Only `native/windows-x64/` is committed, without force.
5. Wait until the raw `latest.json` URL returns 200. Then, in `C:\ChipArena\NativeTest`, use Check for updates. It should show
   "Update available · <version> · N MB" → **Update and restart** → the game closes, installs, restarts on the new build,
   and shows the new home menu.

If that old starter ZIP is gone, use a two-build path. Build and install a starter, make any change under the four hashed
folders, rebuild, then publish the second build. Do **not** publish the current `7bd4e02` build first if the owner wants
`7bd4e02` itself delivered as an update later; the publisher would refuse it as already published.

## Suggested setup work for Astra (proposals; owner approves)

1. **One-command publish for the owner.** For example, a `Publish-Native.cmd` next to `Build-Windows.cmd`, or a flag, that:
   - checks the last `build.log` markers;
   - runs the dry run, shows version and revision against what is currently published at the raw URL, and asks for a typed
     confirmation;
   - then runs `--publish`.

   Keep `--publish` explicit; nothing should publish as a side effect of building.
2. **Baseline install guidance in the tooling.** Have `Build-Windows.cmd` print where the installed test copy should live and warn
   that `Builds\Windows` is replaced every build. Optionally keep a dated copy of each starter ZIP (`Builds\starters\…`) so
   an older install can always be recreated.
3. **Status visible before publishing.** A small script or command that prints what the feed currently holds
   (version, revision, size) next to the local `Builds\NativeUpdateFeed\latest.json`. That makes "same version / older revision"
   refusals obvious before the owner tries.
4. **Unity CI for this branch.** `unity-conversion.yml` only runs on `codex/unity-*`. Add `claude/**`, or use
   `workflow_dispatch` on `claude/sleepy-ritchie-g8ruhv`, so the owner gets a CI source ZIP of the exact commit to build.
5. **Owner acceptance test to record** (DEV_LOG):
   - update to a newer build;
   - settings and account survive the update;
   - a deliberately failing new build rolls back on startup (README contract);
   - cancel during a download leaves the install unchanged;
   - an update offered while in a room or match stays disabled until leaving.
6. **Authorization.** The standing test-channel publication instruction in AGENTS.md covers the Electron `feed/` from
   `claude/visual-revision`. `Updater/README.md` says the owner publishes the native feed "after native acceptance".
   Treat each native publication as needing the owner's explicit OK unless he extends the standing instruction.

## UI contract after Claude's menu work (keep these working)

- `InitializeUpdater()` is unchanged (`NativeUpdateUI.cs`, called from `ConversionGame.Awake`).
- `UpdatePanel(right, top)` (`NativeUpdateUI.cs`) is the update card. The home screen's Update Game pill (`NativeHomeMenu.cs`
  `UpdateControl`) shows a status line, toggles the card, and forces it open while Downloading, Verifying, Preparing or Restarting.
- `7bd4e02`, UI only:
  - Badge text for Idle:
    - "No update published yet" when the status is the 404 text;
    - "Update cancelled" when the status mentions cancel;
    - otherwise "Not checked yet".
  - Unavailable shows "Updater off · open for details". The card chip reads NOT PUBLISHED in the 404 case.
  - Opening the card calls `CheckForUpdates()` unless the updater is busy, already Available, or Unavailable.
- The 404 case is detected by `Status.StartsWith("No Unity update")` (`NotPublished()` in `NativeHomeMenu.cs`). If you change that
  status text in `NativeUpdater.cs`, update the check or expose a flag/constant. A shared constant would be cleaner.
- Escape on the main menu now steps back instead of clearing `menu`, and `menu` is forced true when there is no match,
  so `CanInstall` holds on the home screen.

## Other open items for Astra (unchanged by this handoff)

- Issue #60 gameplay/protocol differences:
  - PvP Siren intro phase is 9.0678 s (the source is 7.2678 s);
  - bots are queued in intros;
  - `skipBotDrafts` defaults to on;
  - bot draft tie-break;
  - no return-to-lobby countdown;
  - ceiling bounce at y>19;
  - the PvE pick source label is not replicated.
- TypeScript CI baseline: server build errors (PR #2) plus 12–13 unit failures and the WebSocket rematch timeout. These are not related to Unity.
- Not yet verified on Windows:
  - the new native menu (every screen, 1080p and 3440×1440);
  - GUILayout panel contents;
  - LAN and relay rooms with a second player;
  - an actual Unity-to-Unity update.

## Files to read first

`unity/ChipArenaConversion/Updater/README.md`, `Build-Windows.cmd`, `Assets/ChipArena/Editor/NativeUpdateBuild.cs`
(Package, SourceHash), `Assets/ChipArena/Core/NativeUpdateRules.cs` (Feed, IsNewer), `Assets/ChipArena/Runtime/NativeUpdater.cs`
(Initialize, Check), `tools/publish_native_update.py`, `Assets/ChipArena/Runtime/NativeHomeMenu.cs` (UpdateControl, UpdateBadge),
`Assets/ChipArena/Runtime/NativeUpdateUI.cs`, and the newest Claude and Codex entries in `docs/DEV_LOG.md`.
