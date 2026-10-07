# Chip Arena — native Unity conversion

**Melee and riposte checkpoint; the full conversion is not complete.** This project is separate from both the Electron game and the accepted Unity lab. The target is native Unity/C# gameplay and physics, with no Node/Rapier sidecar.

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
| Q / E | Magic Q / weapon E abilities |
| E held, then released | Aim grenade, meteor, geysers or black hole; preview appears after 0.2 s |
| V | Switch first/third person; saved in the menu too |
| RMB | Timed block |
| Shift / Space | Roll / jump |
| Tab | Scoreboard |
| Escape | Pause and menu |
| F9 | Individual-draw rendering fallback |

## New in this checkpoint

- Standard melee now checks the source moving sweep throughout its active window. Targets and props can be hit once per swing; intervening cover and other pilots obstruct contact. The third Sword attack uses its narrower overhead sweep.
- Source attack footwork is present: 0.65 m Sword, 0.85 m Daggers, 0.55 m Shield and 0.45 m Greatsword, subject to native collisions. Attack commitment stops voluntary movement immediately.
- Timed melee blocks rebound/stun the attacker. Human melee blockers can counter with LMB or roll out during the remaining block window; an action that cannot start preserves the block. The counter replaces its own riposte stun with the weapon's ordinary stagger; Daggers retain the two-hit opportunity and stagger escape window.
- Shift can cancel a human standard primary. Cancelling before contact refunds only the remaining attack cooldown down to the roll duration, never spent stamina. Casts, reloads and ordinary block windows stay committed.
- Contact impulses now use source external-velocity decay, counter-steering and a single vertical kick. This changes knockback across weapon classes; actual PhysX response needs playtesting.
- **Bots use my weapon** makes weapon-specific duels easier to test. The HUD displays a riposte prompt after a successful melee block.

The menu title is **Melee & riposte**. Start with Sword, one bot, **Bots use my weapon** enabled and Stationary targets off. Try RMB just before its swing lands, then LMB; try Shift during your windup and recovery. Repeat with Daggers/Greatsword and check strikes near cover. First-person fixes, V toggle, scope, staff and glow remain included.

Melee E specials, Shield knockdown, launch/juggle/recovery and special Chip handlers are **still pending**. Shield currently uses a grounded stagger. The pinned source's old held-shield-guard path is inactive: its E definition is a throw, so no new held-guard control was invented here.

## First-person stability and aiming (retained)

The base animation is saved before casting/recoil/IK/torso adjustments. Camera and visual pilots use interpolated body transforms and a continuous animation clock. ADS eases into sight alignment; the rifle has a circular scope. Native Windows frame pacing and visual acceptance remain separate from numerical checks.

## Staff and glow update (retained)

- Claude's first-person staff fix is carried over: smaller staff, outward 20° roll, settled torso and a smooth two-hand guard. The oversized placeholder shaft/head now uses source proportions and four elemental silhouettes. Native rig placement is adjusted to keep the crystal visible and the crosshair clear.
- Primary and Q/E projectiles have soft additive halos, short trails and moving light spill. Surface hits leave soft glows attached to cover for their full 0.85-second lifetime. Armor glow and soft ability-field/explosion fills are included; circles remain for target previews only.
- **Combat glow: Full / Low / Off** is saved in the setup menu. Full uses seven reused point lights; Low keeps halos and trails with one projectile light. No extra rendering package is required.

Try Fire or Darkness, press **V**, fire repeatedly, cast Q/E, and enter/leave RMB guard. Then try a gun near a wall: the projectile and impact should illuminate nearby geometry. Compare the glow settings and restart a round to check cleanup.

## What is available

- All 15 original **collision layouts**, including slopes, upper floors, dynamic props and support relationships. Moon/rim lights, fog, receiving/casting shadows and wood/metal trim are now included. Full scenery is not yet ported.
- Native Unity rigid bodies, capsule pilots, swept projectiles, basic standard-weapon combat and simple bots.
- Local free-for-all or alternating teams, rounds/sets, eliminations, rematch and a turn-based losing-side Chip draft.
- Native source-stat calculations and eligibility for all 236 source Chips. **89 stat Chips** are enabled in the combat draft; unsupported mechanics are excluded. The complete catalogue is browsable as reference.
- Source captured melee clips, articulated gun poses, left-hand staff carry/guard, and element-specific primary/Q/E casting phrases. First person has a separate arm/weapon rig; it does not move the authoritative muzzle or hitboxes.
- Saved first/third-person preference. Gun ADS temporarily enters first person and uses centred sights; the rifle hides its housing while scoped.
- All eleven standard gun/magic ability handlers, mana costs, cooldowns, short input buffer, interruption, target previews and basic impact/zone effects. Native contacts and visual polish remain acceptance work.
- Basic fire burn/blast, water slow, lightning chain and darkness drain, plus native saw/spike/lava schedules. These combat integrations still need native acceptance.
- Separate preferences under `ChipArenaConversion/Chip Arena Native Conversion TEST`; no production accounts, saves, updater or service endpoints are used.

This is not yet the full game. Special Chip mechanics, melee E moves, DEV styles, Siren/PvE, body damage/ragdoll fidelity, production UI/audio/cosmetics and multiplayer remain work in progress. Read **CONVERSION-STATUS.md** for the exact boundary.

## Abilities in this checkpoint

| Weapon | Q | E |
| --- | --- | --- |
| Pistol | ADS | Frag Grenade |
| Machine Gun | ADS | Underbarrel Rocket |
| Rifle | ADS | Orbital Mark |
| Fire | Flame Lance | Meteor Strike |
| Water | Tidal Wave | Geyser Wall |
| Lightning | Blink Strike | Ball Lightning |
| Darkness | Shadow Shackles | Black Hole |

Hold E for grenade/ground-target previews; release to cast. Block, roll, death or opening the menu cancels the hold. Ability mana and cooldown are spent on release. Existing unsupported ability-modifier Chips remain excluded from the draft.

## Required Windows check

First run the build. It checks 1,728 source casting poses, 44 ability-rule cases, 60 original-source stat cases, all 15 map imports, 120 spawn-floor queries and capsule settling in native Unity physics before packaging. This gate is implemented but could not execute in the Linux authoring workspace without an activated Unity Editor.

Test **V** with every weapon, reload and ADS, switching near walls, death/round reset and persistence after restart. Test magic staff carry/block and the four primary casts. Exercise each Q/E against targets and cover; check mana/cooldown on interruption, grenade bounce, unarmed/armed rockets, mark tracking, piercing, blink clearance and timed fields.

Also test ramps, stairs and upper floors; shooting close to cover; dynamic props and breakable supports; all standard primaries; round reset and one complete losing-player draft. Preserve `Builds/build.log` and report movement/contact problems before treating the new physics as a reliable base for the remaining conversion.

## Source and tests

Pinned game source: `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Accepted Unity lab: `f1207e65b9ba3db39239b7996c8b516fa58bd775`.

`tools/source-lock.json` pins 108 source files. `tools/export_source.mjs <source-checkout>` verifies those blobs, then exports definitions/layouts and reference fixtures at build time. The shipped game loads JSON and C#; no JavaScript engine is embedded. `tools/presentation-provenance.json` additionally pins the eleven presentation/ability reference files; `node tools/export_presentation.mjs <source-checkout>` reproduces the new fixtures from original pure functions (timestamps quantized to the native float clock). `tools/conversion-inventory.json` records the wider remaining source/assets.

Run `dotnet run --project Tests/CoreTests.csproj -c Release`, then `python3 -m unittest discover -s Tests -p test_source_packages.py` and `python3 tools/package_source.py --check`. See VALIDATION.md for actual checks and their limits.
