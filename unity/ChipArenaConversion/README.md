# Chip Arena — native Unity conversion

This separate Unity/C# project contains the native conversion candidate. The original Electron game and accepted Unity lab are retained. Work continues toward the full conversion; see [CONVERSION-STATUS.md](CONVERSION-STATUS.md) for remaining gaps and [VALIDATION.md](VALIDATION.md) for checks actually run.

## Open or build

1. Extract the whole source archive into a new folder and add `ChipArenaConversion` in Unity Hub.
2. Open with Unity **6000.3.24f1**, choose **Chip Arena → Open test arena**, then Play.
3. For Windows, close this project in the Editor and run **Build-Windows.cmd**.
4. Wait for **UNITY_TEST_BUILD_READY**. Run `Builds/Windows/ChipArena-Native-Conversion-TEST.exe`.

The builder runs the import, content, animation, shader and native physics gates before packaging `Builds/Chip-Arena-Native-Conversion-TEST-Windows.zip`. Keep the entire Windows folder together. The EXE retains the voxel CA icon. This source archive contains no precompiled executable; an activated Windows Editor is required.

## Current implementation

- All 15 source weapons, 236 Chips and 15 map layouts, native gun/magic/melee abilities, modifiers, summons, signed DEV unlocks, rounds, teams and drafts.
- Ward Zero co-op: waves, money, stations, recruits, revives, boss phases, horde limb damage, entry queues, vents and moving Ward scenery.
- Source-authored voxel models, textures, 165 motion clips, 20 entrance stages and 11 scripted elimination scenes; native physics bodies, combat glow, source music and sound effects.
- First/third person, ADS and rifle scope, timed block, lock-on, spectator switching, elimination replay, settings, appearance previews, build inspection and local DEV sandbox.
- Native LAN rooms, standalone WebSocket relay/discovery, reconnect seats, configured account service and headless authority. See [Relay/README.md](Relay/README.md).

Rendering and physics use native adapters. Player-host migration and remaining body/environment details are still being completed. These are implemented systems, not a claim that rendered Windows gameplay has passed acceptance.

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
