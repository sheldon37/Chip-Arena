# Gun sounds: distance layers, reloads, bolt and casings

Owner, 2026-11-24: "We can put this one together now" (phase 2's sound half of the effects plan,
https://claude.ai/artifact/3tjbdJCZpT1V4TwPds1sV9), and earlier: "Maybe the sounds? (I think they use distance sounds as well.)"
Claude (S1), branch `claude/gun-sounds`. Code: `Core/NativeGunSounds.cs` (the table), `Runtime/NativeGunAudio.cs` (the player),
two hook lines in `Runtime/NativeAudio.cs`, one in `Runtime/ConversionGame.cs`. Checks: `Tests/GunSoundChecks.cs` (`--gun-sounds`).

Presentation only. Every sound plays from the Shot events and the replicated reload and shot times every client already gets. No
rule, fire rate, hit timing, hitbox, damage, cooldown, movement value or network message changes. No animation changes.

## The pack guns

| Game gun | Look and role | Modern Weapons ALL-IN-ONE gun | Why |
|---|---|---|---|
| Pistol | WK-11 Viper, 9 mm semi-auto, .42 s | FN 509 (9 mm) | a 9 mm striker pistol; the Glock is a machine pistol, the Five-seveN 5.7 mm, the Desert Eagle a hand cannon |
| Rifle | Oryx SRM-12, bolt sniper, 1.15 s | M2010 ESR (bolt-action .300 Win Mag) | the pack's bolt sniper of the rifle's weight; the AX50 and CheyTac are .50-class anti-materiel |
| Machine gun | TR15 (AR-style), .085 s | M4 Carbine | the AR-15 family the TR15 is; the M249 is the heavier alternative (same calibre, a belt-fed thump) |

Reloads and the rifle's bolt come from the Tactical Shooter Pack's own recordings for the same three guns (`S_WK-11_Viper_Reload_Tactical`,
`S_SRM-12_TacticalReload`, `S_TR15_Reload_Tactical`, `S_SRM-12_ShotBoltOnly`), cut into parts at their quiet gaps. Casings come from the
Modern Weapons pack (9 mm for the pistol, 7.62 mm brass for the machine gun's 5.56, the sniper case for the rifle). The game has no
suppressors, so the suppressed takes are not used.

## How a shot is heard

Per shot, from the event's position (the bank's falloff and pan as today, `NativeEventSounds.Falloff`/`Pan`):

| Layer | Key | What | Weight before the falloff |
|---|---|---|---|
| Close | `gun.<gun>.near` | the pack's close shot with its body (`OneShot` + `Stop` mixed; the machine gun's `OneShot` alone, cut to .22 s) | cos of the blend |
| Distant | `gun.<gun>.far` | the pack's distant shot (`Dist_OneShot`) | sin of the blend |
| Tail | `gun.<gun>.tail` | the pack's tail (the machine gun's: its `Stop` and `Tail` mixed, played once its stream stops) | (.4 + .6 blend) / sqrt(falloff), at most 3 |

- The blend runs from 0 at 10 m to 1 at 60 m, linear in log distance (equal power). At 30 m: close .57, distant .82.
- The tail falls off at half the shot's dB rate: its heard weight stays .27-.36 from 5 to 80 m while the shot's falloff drops from .72
  to .09, so far away the shot rings out longer under a fainter crack.
- Every layer waits the sound's travel time (d / 343 m/s, at most .35 s; 87 ms at 30 m, 233 ms at 80 m), less the event's age.
- Four takes per close and distant layer, three per tail. The bank's shuffle bags never repeat a take back to back.
- The pistol and rifle ring a tail each shot (at most one every .38 s per shooter). The machine gun streams one-shots at its .085 s
  interval. Its release (body and tail) plays 1.6 intervals after the last shot, started as if it had begun with that shot. The pack's
  `Loop` is at the M4's own rate, not the game's .085 s, and would drift from the shots over the network, so it is not used.
- Tails and casings are low priority: they yield their voices first (24 voices, as today).

## Reloads, bolt and casings

The reload parts are placed on the reload clips' own clocks, so they land on the hands:

| Gun | Parts (s into the recording) | First person (F1: the clip fills the reload, the pistol warped) | Third person (T1: ReloadEnd on ReloadUntil) |
|---|---|---|---|
| Pistol, 1.1 s | out .42, in 1.85 | .14 s, .60 s | .15 s, .66 s |
| Rifle, 2.6 s | out .725, in 1.925 | .61 s, 1.62 s | .69 s, 1.82 s |
| Machine gun, 2.4 s | out .82, in 1.575, bolt 2.365 | .57 s, 1.09 s, 1.63 s | .66 s, 1.26 s, 1.89 s |

- The reloading pilot hears its parts in the view it plays (first person or third); everyone else hears the third person's. A reload
  that stops early plays no later part. A longer or shorter reload (chips) stretches the times with it.
- The pistol's tactical reload racks no slide (its slide is in each shot); the machine gun's ends on the bolt release.
- The rifle's bolt plays .44 s into its fire action (first person: the action fills the interval; third person: the fire clip's pace).
- Casings land .42 s (±.08) after the shot (the rifle's when its bolt opens) on the floor the shooter's footsteps last found:
  concrete (also stone and unknown floors), metal, wood, gravel; snow swallows them.
- Other pilots' reloads, bolts and casings fall off twice (close-range detail) and are skipped past 25 m.

## Without the clips

The keys are in the bank contract (`Core/NativeSoundBank.cs`). While a gun's `near` key has no clips, its shots play today's
`gun.<gun>.shot` route exactly; while its first reload part has none, the reloading pilot hears today's `gun.reload.start`/`end`.
Builds without LocalAssets are unchanged.

## The clips (private)

77 clips, 913,312 bytes in `Resources/EffectsAudio` (13.14 -> 14.06 MB of the 26 MB budget). Built with the bank's own chain
(`tools/build_effects_audio.py` in the private zip, extended with two optional source fields: `mix` sums layers recorded to play together,
`fade` sets a longer fade-out; every existing input builds as before). Mono, 44.1 kHz, Vorbis q5, true peak at or under -1 dBTP. The
zip's `LICENSES.md` names every source file; `docs/ASSET-CREDITS.md` has the packs.

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --gun-sounds` (also in the full run).
- `python -I tools/install_local_assets.py <zip> --check` on the zip that carries the clips.
