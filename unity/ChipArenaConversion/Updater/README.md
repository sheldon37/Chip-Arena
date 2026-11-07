# Native Unity update button

This updater belongs only to the isolated Windows Unity conversion. It uses
`native/windows-x64/` in `sheldon37/chip-arena-test-updates`; the Electron `feed/`
channel, accounts, saves and gameplay protocol are independent.

## First installation and future updates

Build with Unity 6000.3.24f1 on Windows using `Build-Windows.cmd`. The build machine
needs an activated Unity Editor, Windows Build Support and .NET Framework 4.8.
All existing native build gates must pass. Extract the resulting
`Builds/Chip-Arena-Windows.zip` into a writable location. It contains exactly one
`Chip Arena` folder. Open that folder and run **`Chip Arena.exe`**; keep `app` and the
supporting files beside it. Shortcuts point to `Chip Arena.exe`. Windows players
need .NET Framework 4.8. The cloud export includes the same clean player ZIP beside
its technical delivery bundle. Share **only `Chip-Arena-Windows.zip`** with players.

Both the outer launcher and the Unity player in `app` are now named `Chip Arena.exe`.
This is bootstrap version 2: existing bootstrap-1 installations require a one-time
replacement with the new starter ZIP. Their updater rejects version 2 before
replacing any files. Extract into a new folder and run its outer `Chip Arena.exe`.
Settings and saved login continue using the existing per-user data folder.
Subsequent version-2 installations update normally through the test channel.
The technical seed ZIP name remains an automation input with identical contents.

Keep that installed copy **outside the source project** (for example,
`C:\Games\Chip Arena`). `Builds\Windows` and the starter ZIP are replaced by
each build. Extract your baseline starter before rebuilding, then leave that
installed folder alone so it can receive the newer build through the button.

This is a **one-time starter installation**. Older copies cannot acquire an
updater they do not contain. Later builds appear in the menu after their compiled
player is published. Choose **Update and restart** outside a match and room.
The game downloads and verifies the payload, saves settings, closes, installs and
restarts. An unsuccessful startup restores the preceding player. A source ZIP
cannot serve as a player update.

The native feed is not activated by committing this code. Until a compiled build
is published, the button reports that no Unity update has been published. Runtime
and UI integration still require an actual Unity Windows acceptance test.

## Publishing a compiled build

Each successful Windows build also produces `Builds/NativeUpdateFeed/latest.json`
and its `objects/` payload parts. Build identity is a SHA-256 of the project source;
a UTC build revision prevents downgrades. Rebuilding identical source does not
offer another update. Keep the build machine clock accurate.

### Owner shortcut

From this Unity project's folder, run `Publish-Native.cmd --status` to validate
the local compiled package and compare it with the public feed. This writes
nothing. A 404 means no native build is published; connectivity or permission
failures are reported separately. Same source and older revisions are explained.

After testing the Windows build, double-click **`Publish-Native.cmd`**. It requires
Python 3.8+ and Git with your existing push access to the test-channel repository.
It checks both success markers in `Builds\build.log`, matches the feed to the
packaged player, verifies every payload/ZIP, prepares a staging preview, and
shows the local and published identities. Type the displayed `PUBLISH <version>`
phrase to authorize that exact build, or press Enter to cancel. It rechecks the
channel after confirmation before staging/committing/pushing. Building alone
never publishes.

The shortcut uses `Builds\native-channel-checkout`. A clean, behind-only `main`
is fast-forwarded. Dirty, ahead or divergent checkouts are refused without reset;
inspect any unfinished commit after a failed push before retrying. Once pushed,
the tool checks the same public URL the game uses. GitHub cache delay is reported
as pending feed verification, not a failed push. Use `--status` again if needed.
Then open the **older installed copy** and choose Check for updates, followed by
Update and restart. A copy built from the published source will not offer itself.

The native feed is still empty until an accepted compiled build is published.
The shortcut does not compile Unity or prove a Unity-to-Unity update. Capture the
actual download/restart, settings/account retention, cancellation, room/match
install blocking and startup rollback results in DEV_LOG.

### Script interface

To validate and stage without publication:

```powershell
python tools/publish_native_update.py --feed Builds/NativeUpdateFeed --channel-checkout C:/ChipArena/native-feed-preview
```

After native acceptance, the owner can publish with an authenticated Git checkout:

```powershell
python tools/publish_native_update.py --feed Builds/NativeUpdateFeed --channel-checkout C:/ChipArena/test-update-channel --publish
```

The explicit `--publish` command clones the named test-channel repository if
absent, or requires a clean `main` checkout that can fast-forward to `origin/main`. It validates
all chunks and archive contents, commits only `native/windows-x64`, and pushes
without force. It retains prior payload objects so a download already underway
can finish. Never include account credentials in a player. Building and publishing
new releases remains a developer step; players no longer rebuild or replace files.

The script also accepts `--status`, `--confirm` (with `--publish`) and
`--build-log Builds/build.log`. The shortcut supplies the latter two guards;
explicit script callers are responsible for choosing and accepting their build.

## UI handoff for Claude

`Assets/ChipArena/Runtime/NativeUpdateUI.cs` is the presentation adapter. Restyle
that file as part of the menu work. `ConversionGame.cs` has two hooks:
`InitializeUpdater()` after settings initialization and `UpdatePanel()` in the
menu. Retain both, or call their equivalents from the replacement menu.
In the native home menu (`NativeHomeMenu.cs`) the top bar's Update Game pill shows
the state line and opens `UpdatePanel(right, top)` as a card beneath it; the card
stays open while a download is verified, prepared and installed.

Use `ConversionGame.Updater`:

| Read state | Purpose |
|---|---|
| `State`, `Status` | Availability, checking, download, verification, preparation, restart and errors |
| `Installed`, `Available` | Build identities; `NativeUpdater.Short(version)` for display |
| `Progress` | Download progress from 0 to 1 |
| `Busy`, `CanCancel`, `CanInstall` | Disable/enable controls and prevent installation during play |

Buttons call `CheckForUpdates()`, `InstallAvailableUpdate()` and `Cancel()`.
Do not call `Application.Quit()` from the update button or invent another download
path: the service owns preparation and restart ordering. Cancellation is available
during checking/downloading; file replacement begins only after preparation and
the requesting game has exited. Preserve menu access to errors and retry.

Keep updater logic in `NativeUpdater.cs`, `NativeUpdateRules.cs`, `Updater/` and
the editor packaging code intact while changing visuals. No combat animation
files are changed by this feature. Integrate this branch on top of the existing
Unity repairs before resolving the two menu hooks against ongoing animation work.

## Installation and recovery contract

The top-level launcher stays fixed while `app/` is swapped. The installer accepts
only the fixed HTTPS feed, native product/channel/platform, newer revisions,
bounded chunk/archive sizes and SHA-256 matches. Extraction rejects traversal,
links, Windows device names and case collisions. SHA-256 detects damaged content;
the GitHub channel account and HTTPS are the distribution trust boundary, not an
independent code-signing service. No credentials or elevation are embedded.

The helper stages everything before asking the running game to exit. It verifies
the requesting PID, executable path and start time, waits for exit, and refuses
replacement while another player in this installation remains open. It never
kills a supplied PID. It can close only the child it launched if that child fails
to acknowledge initialization within 90 seconds. Settings/account storage outside
`app/` is untouched. One previous player is retained in `.native-update/previous`.

A durable journal supports recovery after interruption. Always reopen through
the top-level launcher. Diagnostics are in `.native-update/updater.log` and
`result.json`; failed requests retain a small `error.txt`. If interrupted during
download, orphan transaction folders may remain: with the game and launcher
closed and **no journal.json present**, obsolete 32-character transaction folders
can be removed. Never remove a pending journal or its backup to bypass recovery.
Launcher protocol upgrades beyond the installed bootstrap version require a new starter ZIP.

## Verification

```text
python -m unittest discover -s Tests -p "test_*.py"
dotnet run --project Updater/Tests/UpdaterTests.csproj -c Release
python tools/check_updater_windows.py
```

The last gate requires Windows. It runs the same `NativeLauncherCompiler` code
used by the Editor packager, with mixed path separators, spaces, the actual .NET
Framework compiler and an ICO. It rejects missing source before compilation,
then exercises live child processes, locked executable handoff, restart and
startup-failure rollback with the resulting launcher and fixture players. Fixtures are
not Unity players and are never packaged in the game. Transaction tests also
cover journal interruption points and malicious/corrupt archives on Windows/Linux.
GitHub Actions runs these alongside existing native source, managed and relay
checks. None of these replaces the real Unity build and rendered gameplay test.
