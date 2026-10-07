# Chip Arena — native Unity conversion

**Foundation milestone; the full conversion is not complete.** This project is separate from both the Electron game and the accepted Unity lab. The target is native Unity/C# gameplay and physics, with no Node/Rapier sidecar.

## Run this milestone

1. Extract the full source ZIP into a **new folder**. Do not overlay `ChipArenaPrototype` or your installed game.
2. Add the `ChipArenaConversion` folder in Unity Hub and use **6000.3.24f1**.
3. Open **Chip Arena → Open test arena**, then Play. Choose an arena, standard weapon and 1–7 bots.
4. To make an EXE, close this project in the Editor and run **Build-Windows.cmd**.
5. Wait for `UNITY_TEST_BUILD_READY`, then run `Builds/Windows/ChipArena-Native-Conversion-TEST.exe`.

The build creates `Builds/Chip-Arena-Native-Conversion-TEST-Windows.zip`. Keep the entire Windows folder together. The voxel CA launcher icon is retained. This source archive does not contain a precompiled EXE.

| Control | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Double-tap W | Sprint |
| LMB | Gun/magic primary; individual melee strikes |
| Q held / R | Gun aiming / reload |
| RMB | Timed block |
| Shift / Space | Roll / jump |
| Tab | Scoreboard |
| Escape | Pause and menu |
| F9 | Individual-draw rendering fallback |

## What is available

- All 15 original **collision layouts**, including slopes, upper floors, dynamic props and support relationships. Decorative map scenery is not yet ported.
- Native Unity rigid bodies, capsule pilots, swept projectiles, basic standard-weapon combat and simple bots.
- Local free-for-all or alternating teams, rounds/sets, eliminations, rematch and a turn-based losing-side Chip draft.
- Native source-stat calculations and eligibility for all 236 source Chips. **89 stat Chips** are enabled in the combat draft; unsupported mechanics are excluded. The complete catalogue is browsable as reference.
- Source captured melee clips and articulated gun poses inherited from the accepted lab.
- Basic fire burn/blast, water slow, lightning chain and darkness drain, plus native saw/spike/lava schedules. These combat integrations still need native acceptance.
- Separate preferences under `ChipArenaConversion/Chip Arena Native Conversion TEST`; no production accounts, saves, updater or service endpoints are used.

This is not yet the full game. Q/E combat abilities, special Chip mechanics, DEV styles, Siren/PvE, body damage/ragdoll fidelity, production UI/audio/cosmetics and multiplayer remain work in progress. Read **CONVERSION-STATUS.md** for the exact boundary.

## Required Windows check

First run the build. It now checks 60 original-source parity cases, all 15 map imports, 120 spawn-floor queries and capsule settling in native Unity physics before packaging. This gate is implemented but could not execute in the Linux authoring workspace without an activated Unity Editor.

Then test ramps, stairs and upper floors; shooting close to cover; dynamic props and breakable supports; all standard primaries; round reset and one complete losing-player draft. Preserve `Builds/build.log` and report movement/contact problems before treating the new physics as a reliable base for the remaining conversion.

## Source and tests

Pinned game source: `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Accepted Unity lab: `f1207e65b9ba3db39239b7996c8b516fa58bd775`.

`tools/source-lock.json` pins 108 source files. `tools/export_source.mjs <source-checkout>` verifies those blobs, then exports definitions/layouts and reference fixtures at build time. The shipped game loads JSON and C#; no JavaScript engine is embedded. `tools/conversion-inventory.json` records the wider remaining source/assets.

Run `dotnet run --project Tests/CoreTests.csproj -c Release`, then `python3 -m unittest discover -s Tests -p test_source_packages.py` and `python3 tools/package_source.py --check`. See VALIDATION.md for actual checks and their limits.
