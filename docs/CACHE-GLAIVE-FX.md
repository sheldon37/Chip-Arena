# Cache tiers, Glaive cues and the Magic Missiles trial (licensed effects)

Owner, 2026-11-25: "lets get work started", on Astra's revamp brief §4 ("Make existing interactions easier to read"). Claude (C2),
branch `claude/cache-glaive-fx`. Presentation only: no rule, timing, hitbox, damage, cooldown, movement or network message changes.
No animation changes.

## 1. Caches: Action RPG FX on the steel locker

The owner owns Archanor VFX's Action RPG FX (Asset Store 38222). Its four chest glows and four chest openings are its levels 1-4, on
STANDARD, RARE, EPIC and LEGENDARY. The procedural opening (`Core/NativeCacheOpenFx`) stays as it is; this layer sits beside it.

- **For sale:** a small glow and its sparkles at the latch line on the locker's front (a localized cue; nothing on the floor), at
  the nearest six lockers within 45 m of the camera.
- **The open moment:** one burst rises out of the tray as the lid lifts (R9's lid beat, 0.25 s after the open moment): the level's
  rays, a splash of sparkles and a few that linger. Then it is put away.
- **The levels** (`NativeCachePackFx.Levels`, the pack's own numbers from its ChestLv1-4 prefabs): sparkles at the latch 12, 17, 25
  and 40 a second; the opening's rays last 0.15-0.25 s up to 0.35-0.45 s, grow to 1.175-1.5 and run 50-90 % opaque; the splash
  throws 12, 35, 45 and 45 sparkles, the linger 6, 11, 18 and 18. The colour is the tier's (`NativeCacheTiers.Hex`); the sparkles
  take the pack atlas's tile nearest it (pale, sky blue, violet, orange).
- **Who sees what:** every client reads the same replicated open moment (`NativeCacheClock`), so the host and each guest play one
  burst per opening, of the tier the offer awarded, keyed by the cache and the generation that opened (`NativeCacheBursts`). A
  cache first seen already opened (a late joiner, an area just entered) plays none.
- **Off** with Reduced effects (the owner's rule: only the lid moves), on the low graphics tier, on the hologram cache and on the
  chip machines.
- **Two effects, not eight:** the prefab holds the pack's ChestLv1Glow (its floor aura turned to face the camera, its halo, its
  sparkles in a thin box along the latch line) and its ChestLv4Open (its light dropped). Each start sets the tier's level and colour,
  so the four levels cost two effects' bytes.

Code: `Core/NativeCachePackFx.cs`, `Runtime/NativeCachePackPresentation.cs`, one call in `Runtime/NativeCachePresentation.CacheFx`.

## 2. The Glaive's throw, ricochet and catch (Battle Sound Library)

The Glaive's cues already ask the effects bank for `topdog.ring-*` before today's stand-ins (`NativeRecordedCues.Weapon`) and the
synthesized sounds (`NativeToonSynth`), so three keys of the existing contract carry the new recordings. No code change was needed;
the checks pin the order.

| Key | Cue | Source (Battle Sound Library - Magic & Melee, 176417) | Takes |
|---|---|---|---|
| `topdog.ring-throw` | the throw's release (also the halo's) | the arrow fly-by (Doppler) from just before its peak: a receding whoosh | 4, 0.55 s |
| `topdog.ring-ping` | each throw hit and ricochet (rate 1 + 0.06 a bounce) | sword on metal: the blade and metal layers, its impact layer at -12 dB | 5, 0.5 s |
| `topdog.ring-catch` | the catch | fist on metal: the hand and impact layers, its metal layer at -9 dB | 4, 0.4 s |

- **Telling bounce from catch blind:** the ricochet is bright and rings (spectral centroid about 3.5 kHz); the catch is a dull, short
  grab with no ring (about 0.67 kHz). Today's stand-ins are two metal clings 1.4 times apart (5.2 and 3.7 kHz).
- **In a firefight** (a machine gun at 15 m, a pistol at 6 m, offline mix): the ricochet sits about 13 dB and the catch 14 dB over the
  gunfire in their own bands; the throw 3.6 dB (today's throw sat 15 dB under it).
- **Clips (private, not in git):** 13 clips, 123,366 bytes in `Resources/EffectsAudio` (14.06 -> 14.18 MB of 26 MB), through the
  bank's own chain (`tools/build_effects_audio.py` in the Drive zip, unchanged).

## 3. The Magic Missiles trial on the fire staff's bolt

The owner owns Archanor VFX's Magic Missiles (36250). The trial draws one existing PvP spell, the fire staff's primary bolt, as the
pack's FireMissile, and its hits as the pack's FireExplosion.

- **Trajectory:** the missile rides the bolt's own replicated head each frame (`NativeShotViews`: the authority's path, a guest's
  carried-on head). Its fire and smoke trails stream behind it in world space.
- **Impact timing:** the explosion plays on the same Impact or direct Hit event, on the same frame, that the sprite burst used
  (`NativeMagicPackPresentation.MagicPackHits`).
- **While the trial draws a bolt,** that bolt's energy core, pack head, trail sprites and ribbon are not drawn. Every other school,
  and the fire staff's abilities, keep their look.
- Without the prefab, or on the low graphics tier, the bolt draws exactly as today.

Code: `Core/NativeMissileTrial.cs`, `Runtime/NativeMissileTrialPresentation.cs`, one line in `NativeWeaponFxPresentation.ShotFx` and
two in `NativeMagicPackPresentation.MagicPackHits`.

## The effects folder (private; ships with the separate effects bank)

Both effects layers load from `Resources` in the git-ignored `Assets/LocalAssets/ChipArenaFx/` (the folder proposed for Astra's
effects bank, DEV_LOG 2026-11-23):

- `Resources/ChipArenaCacheFx.prefab` (`glow`, `open`), `Caches/Materials`, `Caches/Textures`;
- `Resources/ChipArenaMissileFx.prefab` (`missile`, `explosion`), `Missiles/Materials`, `Missiles/Textures`.

The effects are the packs' particle systems on Unity's built-in `Particles/Standard Unlit` shader (the packs' own material shader),
with no pack shader, script, sound, light or animator. They are built by the local Editor lab `ChipArenaC2Lab.cs` (git-ignored) from
pack subsets in `Assets/LocalAssets/ARPG Effects` and `Assets/LocalAssets/MagicMissiles`. Their large textures are downscaled to
256-512 px.

`tools/pack_visual_body.py` follows only the visual body's prefab, so this folder does not ship until the effects bank packs it. The
game keeps today's look without it.

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --cache-glaive-fx` (also in the full run).
- `python -I tools/install_local_assets.py <zip> --check` on the zip that carries the Glaive clips.
