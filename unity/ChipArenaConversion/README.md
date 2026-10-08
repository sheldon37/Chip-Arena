# Chip Arena — native Unity conversion

This separate Unity/C# project contains the native conversion candidate. The original Electron game and accepted Unity lab are retained. This is the full conversion candidate for the next owner test, with the feature audit in [CONVERSION-STATUS.md](CONVERSION-STATUS.md) and checks actually run in [VALIDATION.md](VALIDATION.md).

## Open or build

This package includes the October 7 Windows build repairs for procedural texture
mip upload and the account component's invalid `Start(...)` callback. Use the
whole project in a new folder; the repaired native build still needs to run.

1. Extract the whole source archive into a new folder and add `ChipArenaConversion` in Unity Hub.
2. Open with Unity **6000.3.24f1**, choose **Chip Arena → Open test arena**, then Play.
3. For Windows, close this project in the Editor and run **Build-Windows.cmd**.
4. Wait for **UNITY_TEST_BUILD_READY**. Run `Builds/Windows/ChipArena-Native-Conversion-TEST.exe`.

The builder runs the import, content, animation, shader and native physics gates before packaging `Builds/Chip-Arena-Native-Conversion-TEST-Windows.zip`. Keep the top-level launcher and `app` folder together. The EXE retains the voxel CA icon. This source archive contains no precompiled executable; an activated Windows Editor is required.

This version adds **Check for updates / Update and restart** to the menu. Install
the newly built starter ZIP once; later published Unity players can be downloaded
and installed from the button. No compiled Unity update has been published by this
change. Build/publish instructions and Claude's UI handoff are in
[Updater/README.md](Updater/README.md).

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
