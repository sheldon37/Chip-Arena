# Removing the 14 old maps (Astra handoff, refreshed 2026-10-28)

Owner (2026-10-28): "Currently, the expedition maps were a good direction (Along with Factory, Cyber City) Brickfall was the
baseline (I still need to get rid of the old maps)".

The 14 maps carried over from the Electron game are the whole Random rotation today. They are `foundry`, `moonlit`, `neon`,
`freight`, `rooftops`, `reactor`, `outpost`, `citadel`, `frozen`, `canal`, `forge`, `sawmill`, `spikevault` and `lavaworks`
(`Assets/Conversion/Resources/GameContent.json` → `maps`, exported from Electron `src/shared/maps.ts` by
`tools/export_source.mjs`). The Unity-era maps (`Assets/Conversion/Resources/UnityMaps.json`) are all `rotation:false`.

This updates `docs/arenas/ARENA_PROPOSAL.md` §6.2 with current line numbers (`shared 8f8f3d8`); its checklist still holds.
Removal is logic, content and tests, so it is **Astra's** PR. Claude's presentation follow-ups are listed at the end.

## Implemented by Astra (2026-10-28, PR #294)

Owner confirmed Brickfall, Factory and Cyber City in Random, Ward Zero selectable separately, and all music retained.
The Unity merge now keeps only authored UnityMaps entries, migrates retired/unknown saved picks to Brickfall, includes the
catalog policy in the peer revision, and guards an empty Random pool. Dedicated and appearance-preview defaults, Windows
validation fixtures and map generators use the retained arenas. Expedition instances stay outside arena selection.
Source exports, presentation branches and music files remain intact; Claude's visual cleanup below is still optional follow-up.

## Original decisions requested (resolved above)

1. **The new rotation.**
   - Proposal: `brickfall`, `factory`, `cybercity`, plus Ward Zero (`siren`) for PvP rounds if wanted.
   - `rotation:true` on those `UnityMaps.json` entries.
   - The Harbor stage (`docs/arenas/HARBOR_PROPOSAL.md`, from the Toon Harbor kit) would join it when built.
2. **The old maps' music tracks** stay or go. Deleting files breaks the audio clip gate.

## What breaks if the maps are simply deleted (Astra)

### Startup and lobby

| Place | What it does today |
|---|---|
| `Runtime/ConversionGame.cs:10,68` | The saved-map fallback is `"moonlit"`, and `content.Map` then throws in `Awake`: a fatal startup error for players with a saved map. |
| `Runtime/ConversionGame.cs:99` | PvE goes to `"siren"`; that one stays. |
| `Core/NativeRoomOptions.cs:8,12,21` | The default `"moonlit"`. `Normalize` throws when no non-siren map exists. The preview uses moonlit for Random and siren for co-op. |
| `Runtime/NativeLobbyLogic.cs:15` | Random picks rotation maps only: a `uint % 0` when the pool is empty. |
| `Runtime/NativeArena.cs:81` | Randomize each set uses rotation maps only. |
| `Runtime/NativeDedicated.cs:16` | Defaults to `"foundry"`. |
| `Runtime/NativePreview.cs:13` | Hard-codes `"foundry"`. |
| `Runtime/NativeRoomOptionsUI.cs:35-36` | The lobby dropdown lists every arena-selectable map; `NativeMapRules.ArenaSelectable` (`Core/NativeUnityMaps.cs:30`) already excludes `expedition-*`. |
| `Runtime/NativeDevPanel.cs:426`, `Runtime/NativeSandboxTools.cs:13` | DEV and sandbox map lists. |

Saved settings and accounts can still hold an old map id, so the fix must map unknown or removed ids to the new default rather
than throw. Join and handshake should refuse a peer whose map list differs: the content revision already covers `UnityMaps.json`,
so check it also changes with the allowlist.

### Pipeline

- **Re-export restores the maps.** Re-running `tools/export_source.mjs` brings all 14 back. Use a Unity-side allowlist or
  denylist (applied where `Core/NativeUnityMaps.cs:36-55` merges the two sources) instead of editing `GameContent.json`.
- **`siren` stays replaced.** The Unity map already replaces the legacy `siren` of `GameContent.json` (`NativeUnityMaps.cs:52-54`).

### Checks that pin the old maps

- `Tests/MapLookChecks.cs:69` (15 maps);
- `Tests/ParityFollowupChecks.cs:11`;
- `Tests/BrickfallChecks.cs:38` (rotation count);
- `Tests/FootfallAudioChecks.cs:32`;
- `Tests/NativeParityChecks.cs`, `Tests/NativeBuildChecks.cs` and `Tests/NativeNetworkBuildChecks.cs` (block and map counts);
- `Tests/EventFeedbackChecks.cs`;
- `Tests/test_cinematics.py`.

Grep the suite for the 14 ids and for `rotation` before landing.

## Claude's follow-ups (after Astra's PR merges)

- **Lighting and scenery.**
  - Branches keyed by old map ids: `NativeLighting.cs`, `NativeSceneryMotion.cs` (Moonlit snow, canal water, chimney smoke).
  - The 14 `night-*` entries in `MapLooks.json`.
  - The `map-<id>` models in `SourceModels.json`, which `NativeSourcePresentation.cs:71` draws. Removing them shrinks the build.
- **Music.** The rotation music bag (`Runtime/NativeAudio.cs:18`), after the owner's music decision.
- **Menus.** The home menu's "N ARENAS" count (`Runtime/NativeHomeMenu.cs:212`) follows the rotation flag on its own. The menu
  backdrop's default map.
- **Footsteps and impacts.** Footstep surfaces per map (`Core/NativeSoundCues.cs:361-386`) and the surface-impact map rules lose
  their old-map rows.

## Ready-to-paste prompt for Astra

> **Astra:** the owner wants the 14 old Electron maps removed from the Unity game.
> - The maps: foundry, moonlit, neon, freight, rooftops, reactor, outpost, citadel, frozen, canal, forge, sawmill, spikevault and
>   lavaworks.
> - The new Random rotation is the Unity-era PvP maps: brickfall, factory, cybercity, and siren/Ward Zero if the owner confirms.
>   Set `rotation:true` on them in `UnityMaps.json`.
> - The full checklist with current line numbers is `docs/arenas/OLD-MAPS-REMOVAL.md`, based on shared `8f8f3d8`; it refreshes
>   ARENA_PROPOSAL §6.2.
>
> Do it on a `codex/` task branch from the newest shared head:
> - Apply a Unity-side allowlist where `NativeUnityMaps` merges GameContent and UnityMaps, so re-running `export_source.mjs`
>   cannot bring the maps back.
> - Fix every fallback and default that names an old map (`ConversionGame`, `NativeRoomOptions`, `NativeDedicated`, `NativePreview`,
>   lobby Random and randomize-each-set, DEV and sandbox lists).
> - Map saved or unknown map ids to the new default instead of throwing.
> - Make sure the content revision changes so mismatched builds are refused at join.
> - Update the checks that pin 15 maps or the rotation count.
>
> Leave the old maps' presentation code and assets (lighting branches, scenery, `SourceModels.json` models, music) in place for
> Claude to remove after your PR merges. Keep the relay and LAN discovery unchanged. Report the branch, the checks you ran, and
> any presentation code you found still keyed by an old id.
