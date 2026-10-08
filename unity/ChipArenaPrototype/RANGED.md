# Ranged weapons test

The Unity lab now includes **Pistol, Rifle and Machine Gun** primary fire alongside Sword.
The source is Chip Arena `05950eaef80ff2483a9c44d898cad98be1a76bbc`;
`tools/ranged-provenance.json` identifies the source rules, poses, models and six original sound files.

## Play

Start **Play solo** with the default Pistol. Press **2 / 3 / 4** for Pistol / Rifle / Machine Gun,
or **1** for Sword. **Hold LMB** to fire, **hold Q** to aim, and **R** to reload.
Right click remains the timed block. Switching is rejected during firing cooldown, reload, roll,
stun or an active sword attack; retry when ready. Each gun keeps its remaining magazine when switched.
Reserve ammunition is unlimited; firing an empty gun starts a reload. Respawn refills the magazines.

| Weapon | Body / head damage | Magazine | Hip fire interval | Reload | Speed / gravity | Aim FOV |
| --- | --- | ---: | --- | --- | --- | ---: |
| Pistol | 23 / 34.5 | 7 | 0.42 s | 1.1 s | 52 / 32 | 55° |
| Rifle | 58 / 87 | 3 | 1.15 s | 2.6 s | 150 / 12 | 28° |
| Machine Gun | 8 / 12 | 48 | 0.085 s | 2.4 s | 49 / 36 | 52° |

Intervals execute on the existing 60 Hz authority. Pistol aimed fire uses the source's 1.12× interval.
Aiming narrows spread after settling, slows movement and changes camera sensitivity. Machine Gun
aimed fire accumulates spread during sustained bursts. Shots travel and fall under gravity;
aimed shots use a low ballistic arc to converge on the crosshair's traced point. Hip shots fall
below that point with distance. The Rifle is a fast projectile weapon, not hitscan.

The HUD shows magazine, reload time and hit/headshot receipts. There are weapon models, muzzle flashes,
velocity-oriented tracers, source gun/reload/hit sounds, shoulder/hand poses, recoil and animated magazines.
The original gun animations are procedural too; these are C# adaptations, not additional captured clips.

## Useful tests

1. Escape → **Stationary targets**, 8 bots. Aim at bodies and heads; compare hip fire against held Q.
2. Fire part of each magazine, reload, empty it, and switch away/back. Test beside cover and while moving.
3. Disable stationary targets for the mixed gun crowd. Compare 8 / 32 / 96 / 192 bots with matching settings.
4. **Animation viewer** → **5 / 6 / 7** selects Pistol / Rifle / Machine Gun. **[ / ]** cycles
   ready, aim, fire, reload and block; Space pauses. Escape offers slower playback.
5. Host and join two windows. Both must use this update: the bounded LAN protocol is now **CAP2**.
   Check each player's weapon, aim, ammo, reload, hit and respawn. Old CAP1 clients are rejected.

F8 CSV rows now include the local weapon. Weapon changes cancel an active capture.
The melee-animation toggle only changes Sword presentation; guns always use the articulated rig.

## Limits

This is a primary-gunplay test. The Pistol grenade, Rifle orbital strike, Machine Gun rocket and Chip
modifiers are not included. Sword's E remains the earlier diagnostic energy bolt. Other melee sets
remain viewer-only. There is no first-person weapon model or full production scope overlay.

Damage, magazines, cadence, reload, ballistic and aim constants come from the source. The authority,
flat map, body/head volumes, muzzle origin, block/roll/stagger rules and networking remain simplified.
Gun pose choreography and low-detail geometry are adapted; full production spring recoil, gun/body
clearance, all effects and detailed materials are not ported. A separate gameplay muzzle keeps cosmetic
barrel length/recoil from bypassing cover. This is not full balance, animation or performance parity.

35 managed combat/network checks, 909 ranged pose samples and the previous 3,384 captured-pose checks
pass in the authoring workspace. Runtime/Editor/Windows-conditional API compilation also passes.
No new native Unity rendering, audio playback, Windows EXE build or two-window playtest was run here.
The Windows builder runs the checks and verifies audio imports before building; see VALIDATION.md.
