# Chip Arena — native Unity conversion

This separate Unity/C# project contains the native conversion candidate. The original Electron game and accepted Unity lab are retained. This is the full conversion candidate for the next owner test, with the feature audit in [CONVERSION-STATUS.md](CONVERSION-STATUS.md) and checks actually run in [VALIDATION.md](VALIDATION.md).

## Open or build

This source/build kit includes **Build-Windows.cmd** at the project root. It is a
double-clickable Windows command script; no separate build executable is needed.

1. In Unity Hub, install Unity **6000.3.24f1** with **Windows Build Support** and
   activate your license. The launcher compiler also requires **.NET Framework 4.8**.
2. Extract the whole kit into a writable folder on your laptop. Add the extracted
   `ChipArenaConversion` folder in Unity Hub and open it once to finish setup.
3. Close this project in the Unity Editor, then double-click **Build-Windows.cmd**.
   If Unity is installed elsewhere, set `UNITY_EDITOR` to its `Editor/Unity.exe`.
4. On success, share **Builds/Chip-Arena-Windows.zip** with players. It extracts to
   one **Chip Arena** folder containing **Chip Arena.exe** and all required files.
   Players extract once and launch; the Update Game button is already configured.
   For a local preview, use **Builds/Windows/Chip Arena.exe**.

The builder runs the native validation gates, compiles the game and updater, and
packages them together. It opens the output folder when complete. Check
`Builds/build.log` if it fails. Keep installed playtest copies outside this source
folder: building replaces `Builds/Windows` and the player ZIP. The internal
executable in `app` must stay with its supporting files.

Building on the laptop prepares the player ZIP and update feed locally; it does
not upload them. After testing, **Publish-Native.cmd** reviews and publishes the
local update to the existing test channel (Python 3.8+ and Git with push access
required). **Publish-Native.cmd --status** compares it without publishing. Share
only the player ZIP with players; the source kit and publishing tools are for the
build machine. Details are in [Updater/README.md](Updater/README.md).

## Current implementation

- All 15 source weapons, 236 Chips and 15 map layouts, native gun/magic/melee abilities, modifiers, summons, signed DEV unlocks, rounds, teams and drafts.
- Ward Zero co-op: waves, money, stations, recruits, revives, boss phases, horde limb damage, entry queues, vents and moving Ward scenery.
- Source-authored voxel models, textures, 165 motion clips, 20 entrance stages and 11 scripted elimination scenes; voxel wounds, headshot collapse, native physics bodies, combat glow, animated scenery, source music and sound effects.
- First/third person, ADS and rifle scope, timed block, lock-on, spectator switching, elimination replay, settings, appearance previews, the animated 3D Chip draft, build inspection and local DEV sandbox.
- Native LAN rooms, standalone WebSocket relay/discovery, reconnect seats, configured account service and headless authority. See [Relay/README.md](Relay/README.md).

Rendering, menu layout, procedural audio and physics use native adapters, so the result is not pixel-identical to Three.js/Rapier. A player-hosted room ends when its authority closes; automatic host migration is not present in the pinned original source either. The implementation has passed source/managed checks, but rendered Windows gameplay and the EXE build still await native acceptance.

| Control | Action |
|---|---|
| WASD / mouse | Move / look |
| Double-tap W | Sprint |
| LMB | Primary attack |
| Q held / R | Gun aiming / reload |
| Q / E | Magic Q / weapon E |
| E hold/release | Ground or thrown ability preview/cast |
| RMB | Timed block |
| Shift / Space | Roll / jump |
| V | First/third person |
| Tab | Melee lock-on; scoreboard when lock-on is unavailable |
| G | Scoreboard; click a pilot to inspect their Chips |
| C / F | Co-op Chip inventory / interact |
| 1–2 | Ansatsuken abilities |
| 1–4 | Siren abilities; Q switches mode by default |
| Left/right arrows | Switch spectator target |
| Space during replay | Skip replay |
| Escape | Menu; closes preview, inspection or replay first |
| F9 | Individual-draw rendering fallback |

Preferences are separate from the original game. Account and relay URLs must be configured; no credentials are bundled. Unity clients use their own room protocol and cannot join Electron rooms.
