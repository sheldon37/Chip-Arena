# Native Unity update button

This updater belongs only to the isolated Windows Unity conversion. It uses
`native/windows-x64/` in `sheldon37/chip-arena-test-updates`; the Electron `feed/`
channel, accounts, saves and gameplay protocol are independent.

## First installation and future updates

Build with Unity 6000.3.24f1 on Windows using `Build-Windows.cmd`. The build machine
needs an activated Unity Editor, Windows Build Support and .NET Framework 4.8.
All existing native build gates must pass. Extract the resulting
`Builds/Chip-Arena-Native-Conversion-TEST-Windows.zip` into a new writable folder.
Run the EXE at the top level and keep its `app` folder beside it. Shortcuts should
point to this top-level EXE. Windows players need .NET Framework 4.8.

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

From this Unity project's folder, validate and stage without publication:

```powershell
python tools/publish_native_update.py --feed Builds/NativeUpdateFeed --channel-checkout C:/ChipArena/native-feed-preview
```

After native acceptance, the owner can publish with an authenticated Git checkout:

```powershell
python tools/publish_native_update.py --feed Builds/NativeUpdateFeed --channel-checkout C:/ChipArena/test-update-channel --publish
```

The explicit `--publish` command clones the named test-channel repository if
absent, or requires a clean `main` checkout already at `origin/main`. It validates
all chunks and archive contents, commits only `native/windows-x64`, and pushes
without force. It retains prior payload objects so a download already underway
can finish. Never include account credentials in a player. Building and publishing
new releases remains a developer step; players no longer rebuild or replace files.

## UI handoff for Claude

`Assets/ChipArena/Runtime/NativeUpdateUI.cs` is the presentation adapter. Restyle
that file as part of the menu work. `ConversionGame.cs` has two hooks:
`InitializeUpdater()` after settings initialization and `UpdatePanel()` in the
menu. Retain both, or call their equivalents from the replacement menu.

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
Launcher protocol upgrades beyond bootstrap version 1 require a new starter ZIP.

## Verification

```text
python -m unittest discover -s Tests -p "test_*.py"
dotnet run --project Updater/Tests/UpdaterTests.csproj -c Release
python tools/check_updater_windows.py
```

The last gate requires Windows. It compiles the shipping launcher with the actual
.NET Framework compiler and exercises live child processes, locked executable
handoff, restart and startup-failure rollback with fixture players. Fixtures are
not Unity players and are never packaged in the game. Transaction tests also
cover journal interruption points and malicious/corrupt archives on Windows/Linux.
GitHub Actions runs these alongside existing native source, managed and relay
checks. None of these replaces the real Unity build and rendered gameplay test.
