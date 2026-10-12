# Cyber Lord — presentation

DEV-only class `cyberlord`. The owner asked for "a class called "Cyber Lord". This is an unarmed class, using cyber themed
abilities. (1-4 hotkeys used along with Q and E) it will be a melee spellcasting hybrid." The chosen direction is the
hardlight brawler. The ultimate is Kernel Panic. The owner's words for it: "since we're adding an "Ultimate" im think using
a "skill cut-in" animation. If the first hit connects, it does a combo. Hits them far, teleport behind hit them again, in the
air teleport above, make this creative".

Who builds what:
- Astra (Codex) builds the rules: content entry, combo, abilities, mana, Kernel Panic sequence, replication, prediction
  and bots. The rule numbers and the replicated data are in `docs/NEW-KITS.md` section 3.
- Claude built everything below: presentation only. Nothing here changes the simulation, the network frame, hit timing,
  hitboxes, damage, cooldowns or movement.

Status: implemented on `claude/cyberlord` only. Not pushed, merged or released.
- The rules are Astra's: Kernel Panic (PR #138) and the rest of the kit (PR #142, `8a2ed88`: the content entry, the
  combo, Q, E, 1-3, prediction, bots, the DEV-only scope), both merged into this branch. The presentation reads her
  replicated state as it is. Two-client gameplay and the Unity runtime are unrun, so the checks still drive the
  presentation through fixtures that write her fields the way her host does.

## The contract and the one field mapping

Everything reads replicated data through **`CyberLordFeed`** (`Core/NativeCyberLord.cs`). It is the only presentation
code that names Astra's fields (`Actor.Cyber`, `Actor.Kernel`). If her names change, change `CyberLordFeed.Read` and
`CyberLordFeed.Details`; nothing else needs to change. `Tests/CyberLordChecks` enforces this. Every file that draws from
the view must not name the fields. Her simulation files and the fixtures may.

### Kernel Panic: Astra's implemented contract

The source is `Actor.Kernel` (`NativeKernelPanic`). It is on both participants; the feed reads it only on the caster
(`Caster == Id`). Positions are Rigidbody centres; the feet are .865 m below.

| View field | Source | Meaning |
|---|---|---|
| `At`, `Release`, `Until`, `RecoverUntil`, `Ready` [4] | `At`; `At` + .6 x windup; the contact (`At` + .25, or `ConnectedAt`); `Until`; `Ready` | the opener: cock, drive, contact (the host moves the body at once), whiff recovery, cooldown |
| `PanicOutcome` | `Outcome` 1 or 4 = connect, 2 = whiff, 3 with a victim = ended early, 0 = still winding up | which presentation plays |
| `PanicConnect`, `PanicStart`, `PanicEndedAt` | `ConnectedAt`; `ConnectedAt` + .6; `Until` when interrupted | the cut-in and sequence clock |
| `PanicVictim`, `PanicSeed`, `PanicWall`, `PanicNext`, `PanicId` | `Victim`, `Seed`, `WallSplat`, `Next`, `Id` | who, the clone and VFX seed, the wall-splat, committed steps, the cast |
| `PanicFacing`, `PanicYaw`, `PanicAnchor` | `Facing` (unit, with pitch) and its yaw; `Anchor` | the line of the combo |
| `PanicCasterStart`, `PanicVictimStart` | `CasterStart`, `VictimStart` | a whiff's lunge start; the cut-in freeze |
| `CasterPoints`, `VictimPoints` [6] | `CasterPoints`, `VictimPoints` | the host's resolved choreography |

`KernelPanicPath.Victim` and `Caster` follow those points exactly as `NativeKernelPanicRules.Sample` moves the bodies:
the victim eases between its points within each step, and the caster teleports at each step. `NativeKernelPanicRules`
also supplies the numbers: windup .25, cut-in .6, steps at 0 / .45 / .95 / 1.35 / 1.9 s, recovery at 2.6 s, the end at
3.4 s, cooldown 30 and mana 60.

The hits land on the step starts (`HitAt`), so every take's impact is pinned there. The kick and the hammer wind up in
the last .1 s and .08 s of the previous step, then strike as the host teleports the body in. Step 4's three hits arrive
together; the fork-bomb clones stagger them visually, and their sounds play on the clones' beats.

The pilot frame's `kernelReady` gives the local pilot's HUD card its cooldown.

### The rest of the kit: Astra's `Actor.Cyber`

The source is `Actor.Cyber` (`NativeCyberKit`, `Core/NativeCyberRules.cs`, written by `StepCyberInput` /
`StepCyberWorld` in `Runtime/NativeCyberLord.cs`). The kit keeps one window, for the current or last cast (`Kind`, `At`,
`Release`, `Until`), plus a record of its own for Q, E, 2 and 3. Points are Rigidbody centres (the feet .865 m below),
except `WallPoint`, which is the wall's bottom centre. The pilot is free again at `Until`; the rules have no recovery.

| View field | Source | Meaning |
|---|---|---|
| `ComboStep`, `ComboAt`, `ComboUntil`, `ComboHit` | `Combo`, `At`, `Until` (cut, below), `Release` while `Kind` is `cyber-jab` / `-cross` / `-palm` | the swing; its hit resolves at `Release` (.1 / .1 / .18 s of .28 / .30 / .45 s, over the attack rate) |
| `At`, `Release`, `Until` [Q] | `DashAt`, `DashAt`, `DashUntil` | the .2 s dash (windup 0) |
| `DashFrom`, `DashTo`, `TrailUntil` | the same names, as feet | the path and the 1.2 s trail (it ticks on along the whole path, even after an interrupt) |
| `At`, `Release`, `Until` [E] | `WallAt`, `WallAt`, `WallAt` + .2 | the cast |
| `WallAt`, `WallYaw`, `WallBorn`, `WallUntil` | `WallPoint`, `WallYaw`, `WallAt`, `WallUntil` | the 5 x 2.5 m wall for 4 s |
| `At`, `Release`, `Until` [1] | the kit's window while `Kind` is `packet-burst` | the shards fly at `Release` (.08 s) |
| `At`, `Release`, `Until` [2] | `LinkAt`, `LinkAt` + .2, `LinkUntil` | the reach over the cast, held through the drain |
| `LinkTarget`, `LinkAt`, `LinkUntil` | the same names | the tether; an early end (lost, or interrupted) snaps |
| `At`, `Release`, `Until` [3] | `CrashAt`, `Release` (`CrashAt` + .5), `CrashUntil` | the windup, the slam, the end at .8 s |
| `CrashAt`, `CrashBorn`, `CrashUntil` | `CrashPoint` as feet, the slam, the slam + 1 s | the field drawn for the 1 s stun (the rules hit once; there is no lasting field) |
| `Ready` [Q, E, 1, 2, 3] | `QReady`, `EReady`, `PacketReady`, `LinkReady`, `CrashReady` | the HUD cards' cooldowns |

`RecoverUntil` is `Until` plus a short presentation settle (.18 to .4 s) that a newer swing, cast or guard takes over
at once. It is not a rules window.

Interrupts: a block, roll or incapacity makes `StepCyberWorld` resolve the cast. Every tick it lasts, it zeroes
`DashUntil` and `LinkUntil` and moves `Until` to the host's time. The feed takes the planned end from the actor's
`AttackUntil` while its `AttackAt` is the cast's (`CyberBegin` sets both). Otherwise it uses the cast's own length (a
combo step scales with its `Release`). It cuts the window at the first `BlockAt`, `RollAt`, `ReactionAt` or own Kernel
Panic start inside it. A crash cut before its slam draws no field. The kit does not keep the interrupt's time, so a hit
reaction that did not interrupt can end a window early.

An empty kit (a new pilot, or the host's reset after death or a lost DEV identity: all zeros) draws nothing. The actor's
attack window alone (Kernel Panic sets `AttackAt` too) is never read as a swing.

Events are read with their `Detail` (`CyberLordFeed.Details`):
- the cast kinds on Hit / AbilityHit (`cyber-jab`, `cyber-cross`, `cyber-palm`, `data-dash`, `firewall` for the burn,
  `packet-burst`, `siphon-link` for each drain, `system-crash`), and Absorb `firewall` for an absorbed shot;
- `kernel-panic-1` to `-5`, and `kernel-panic-wave` on the others caught by the shockwave.

The Siphon's heal is a plain Heal with no Detail. `CyberLordFeed.SiphonHeal` pairs it with the caster's drain hit from
the same tick. The casts' AbilityCast events and the dash's Teleport are not read; the kit draws them.

`Actor.ViewCopy` deep-copies the kit and the Kernel. `CyberLordFixture` writes both the way her host does: the kit as
`CyberBegin` / `StepCyberInput` do, an interrupt as `StepCyberWorld` does, and `Actor.Kernel` as `ConnectKernel`
resolves it on an open floor. The checks pin the fixture to her `NativeCyberRules.Predict` (windup, length, cost,
cooldown and points per cast, the combo at attack rate 1 and 1.25). It is for the checks and the render harness; the
game never calls it.

## What it looks like

- **Body** (`Core/NativeCyberLordLook.cs`, queued on the pilot's joints in `Runtime/NativeCyberLordPresentation.cs`).
  It is layered over any body skin; the body builder is untouched. The look has:
  - hardlight forearm gauntlets and fist shells;
  - glowing seams with a pulse running out from the chest;
  - a floating broken-hexagon crest with a magenta diamond behind the head;
  - a data shimmer of blinking motes.
  The gauntlets form from light edge by edge (.55 s) when the pilot is first seen alive. They flare on each combo hit and
  light up as the mana flows in. First person keeps the arm pieces only.
- **Stance and locomotion** (`Core/CyberLordMotion.cs`): the owner's Explosive RPG pack Unarmed takes, retargeted by
  `tools/export_pack_cyberlord.mjs`. Hashes are in `tools/pack-cyberlord-provenance.json`; no FBX is committed.
  - It runs on the DEV fighter route (gait feet, the style layer, the reactions).
  - A bladed high guard, the head kept on the facing.
  - The pack run and sprint are carried at the gait's stride. First person has its own view pose.
- **LMB combo**: Jab, Cross, Pulse Palm. Each take's impact lands on the replicated hit, and a chained step starts from
  the previous step's end. Hit events add white hardlight sparks with a hex ring and pixel shards, and the mana flows from
  the hit into the gauntlets. The palm adds a forward hex-ring cone (1.6 m).
- **RMB guard**: a hex hardlight panel in front of the crossed forearms that flares on a block.
- **Q Data Dash**: the body goes translucent and pixelated through the dash. RGB-split glitch bands streak from the start,
  and a 1.2 s trail of hovering data pixels and an upright light curtain pulses on each .3 s tick.
- **E Firewall**: a 5 x 2.5 m hex wall .8 m ahead. It forms from the base centre out (.28 s), scans, ripples where shots
  are absorbed, flickers red where enemies burn on it, and collapses into falling pixels at 4 s.
- **1 Packet Burst**: 8 glitch shards from the palms in the 6 m, 50 degree cone, with RGB-split trails and glitch sparks on hits.
- **2 Siphon Link**: a jittering tether to the target, data packets streaming back, and a heal glint on the caster with
  each drain's heal. It snaps with sparks if it ends early.
- **3 System Crash**: energy gathers in the fists over the .5 s windup. The slam sends an upright wave out to 4 m. The
  field is a volume of corrupted voxels popping out of the floor, rising data and rim scan columns. Its victims show the
  glitched stun (RGB-split ghosts and crawling pixel blocks) for the 1 s stun.
- **4 Kernel Panic** (below).

Owner rule kept: no glowing disc, ring or tile lies on the floor. Areas read through their volume. Slam waves are upright.
`CyberLordChecks` checks this on every drawn piece.

## Idle stance

The owner asked: "Id like to add a idle pose for the DEV Cyber Lord class (Matching the sheldon look) slight glitch
effects". The code is in `Core/CyberLordIdle.cs`. It follows the Sheldon Intro's resolved ending
(`NativeSheldonIntroScript` Stare and Arms).

**The pose**
- The figure stands upright. The pelvis keeps only its heading and the hips rise 3.5 cm. The gait keeps the feet planted.
- The left hand rests on the hood's front edge at the temple, fingers over the brim, at the intro's `HandOnHood` and
  `HandTurn`. The arm starts from the intro's raised-arm key and keeps that elbow's side.
- With no Sheldon Head hood, two fingers rest at the bare temple instead.
- The right arm hangs loose, and the chest is turned a little toward the raised hand.
- A slow breath (3.2 s) lifts the chest and shoulders. The head holds its stare on the facing, chin a touch down, and
  follows a share of the view's pitch. Neither the breath nor the stance take under it moves the head or the hips.

**When it shows**
- A third-person pilot settles into it after 0.9 s standing still, blending in over 0.6 s. The pilot must be alive,
  grounded, not sprinting, at ground speed of 0.35 m/s or less, with the gait's travel at zero.
- It blends out within 0.08 s on any of these: a step, a jab, a cast, a guard, a roll, a hit reaction, a stun, a jump,
  or Kernel Panic.
- A swing or cast must have finished its settle and blend out first, and only then does the 0.9 s still time start.
- A fresh rig of a pilot already standing still (a late join, the killcam) shows the idle at once. Every window comes
  from the replicated actor and its view. The only memory is when the pilot last moved.
- The presented state is `cyber-idle`. First person is unchanged.

**The glitch** is slight and occasional: about one moment every 4 s, some 6% of the idle's frames. It is seeded per
pilot, on the Sheldon Intro's 24 Hz steps, and only shows once the stance has settled. There are four kinds:
- an RGB-split shimmer (`Split`): a magenta and a cyan ghost of the posed body, 2 to 4 cm apart across it, for 0.2 s;
- a slice tear (`Slice`, `HeadSlice`): the upper body, and some steps the head, torn up to 5.5 cm sideways for 0.125 s;
- a scanline band (`Scan`): a band sweeping the hardlight parts over 0.5 s and flickering them dim and bright;
- a rare twitch (`Twitch`): two steps of a neck and shoulder jolt, in some 6.5 s slots.

With reduced effects there is no slice and no twitch. The split rises and falls once at a lower strength, and the band
only brightens softly, so nothing strobes. In a match the tear and the twitch are in the rig's pose. The runtime draws
the ghosts (`CyberIdleGhosts`) and the band (`CyberLordBody`).

**Portraits** (lobby, Locker, pilot card, the Kernel Panic cut-in)
- A Cyber Lord in the standing pose uses `CyberLordIdlePose.Stand` through `NativeLobbyPoses.Sample`, the new overload
  that takes the hat, reduced effects and a seed. It stands even on both feet, unarmed, turned a little to the camera.
- `Runtime/NativePortraits.cs` draws its hardlight look (unlit boxes) with the scanline band, and the split ghosts as
  additive tinted copies.
- With reduced motion (portrait time 0) it holds still and does not glitch. Every other pilot's lobby pose is unchanged.

The idle is presentation only. It reads the actor and never writes it, and the hitboxes are the capsule, not the pose.

## Kernel Panic

- **Opener.** The .25 s windup cocks the fist and drives the lunge punch, which lands at the contact. There the host
  moves the body up to 6 m at once, and an RGB-split blink trail shows the path for .3 s.
  - Whiff: a fizzle and the stumble out through the .6 s recovery. The card shows the half cooldown.
  - Connect: the caster freezes on the punch with a hardlight tremor, and the sparks hang in the air.
- **Cut-in, .6 s** (`Core/NativeKernelPanicView.cs` KernelPanicCutIn, drawn by `Runtime/NativeCyberLordScreen.cs`).
  - The caster and the victim get a full-screen splash:
    - the caster's live 3D portrait (C14 `Portrait`, with hat and colours);
    - three diagonal hardlight bands sliding in and out;
    - a speed-line burst;
    - the glitch-typed title KERNEL PANIC with RGB-split copies;
    - the tag ULTIMATE // SKILL CUT-IN and both names.
  - Everyone else gets a slim banner under the round tag for 1.5 s. It never covers the crosshair.
- **Sequence** (`KernelPanicPath` and `CyberLordMotion`). The poses play on Astra's step table and points:
  1. Breaker: launch streaks; a wall-splat crack ring if a wall stops the launch.
  2. Blink-behind: glitch-teleport tears out of the old spot and in behind the victim, then the rising kick's streaks.
  3. Blink-above: tears in over the apex, and the hammer-fist's crescent trail.
  4. Fork bomb: the caster glitches out. Three hardlight clones tear in from three sides, strike in turn and shatter
     into pixels. The clones are see-through pilot ghosts on rigs of their own.
  5. Kernel Panic: the caster tears in by the landing. A column of falling code glyphs slams the victim. The upright
     shockwave races out 5 m with pixels flung off the floor, fading before its edge. Anyone it catches
     (`kernel-panic-wave`) gets a glitch spark.
  Then the .8 s recovery.
- **Victim.** The victim tumbles through the same steps over its own route: launched, kicked spinning, hammered head
  down, jolted by each clone, slammed flat, lying glitched. It flattens against the wall on a wall-splat.
- **Cameras** (`KernelPanicCamera`, the C16 rules). Only the caster and the victim are directed:
  - a low two-shot through the cut-in;
  - the Breaker tracked side-on;
  - a low angle under the kick;
  - a high angle over the hammer (the victim looks up into it);
  - an orbit through the fork bomb;
  - a low wide pushing in on the column;
  - a blend back to the player's camera.
  Walls are kept out by the runtime raycast. Camera shake follows Settings. Reduced effects give a gentler path: every
  change blends over .3 s, with no orbit, roll or shake and a wider lens. Everyone else keeps their own camera.
- **Feel** (`KernelPanicFeel`). Hit-stops on each step's hit and each clone strike, and a slow beat on the final slam.
  They are a warp of the participants' presentation clock only:
  - it is monotone and back on the match clock before the recovery ends;
  - the simulation clock is never touched;
  - with reduced effects the camera keeps the match clock.
  The participants also get a glitch colour shift on their screen (the existing glitch pass). Third parties see the
  unwarped choreography.

## HUD, icon, tile, Locker, entrance, audio

- **HUD**: Q, E and 1-4 cards in the cyber card drawer after RMB and SHIFT, with Lucide icons, countdowns, ACTIVE,
  LOW MANA and PANIC (`Core/NativeCyberLordHud.cs`). The health box shows MANA and STAMINA; the self overhead rows show
  mana.
- **Class icon**: `spec-cyberlord` (full and small drawings) in `tools/export_class_icons.mjs`, regenerated into
  `ClassIcons.json`.
- **Loadout tile**: the content entry is `devOnly`, so the existing tile shows the DEV violet dot under Melee and DEV for
  developers. The role line reads "DEV · hardlight fists and cyber spells". The card line is her entry's jab:
  "12 DMG · 3.6 / SEC · 2.2 M".
- **Locker**: the Q, E and 1-4 rows show the HUD cards' icons, and their costs read in mana (her kit spends mana).
- **Locker entrance**: "DEV · Hardlight Boot" (`dev-cyberlord`), with a DEV badge, a diamond icon and the text "Built from
  light, then a hardlight palm strike." It is gated like every `dev-` cosmetic (a guest or a scoped grant cannot pick it).
- **Entrance** (`Core/NativeCyberLordIntro.cs`, directed). It plays on the standard 4.8 s with the name card at 3.2 s, on
  the shipped pulse-wave bed (150 bpm). `dev-*` styles get no music key by contract.
  1. A scan of light builds the pilot from the shins up.
  2. The gauntlets form on a close shot.
  3. Jab (1.2 s), cross (1.6 s), then the Pulse Palm at the lens (2.0 s, slowed, caught up before the card).
  4. The stance settles on the 3.2 s accent.
  A Cyber Lord pilot plays it by default.
- **Audio** (`Core/NativeCyberLordSounds.cs`): existing bank keys only (17 keys), with shipped fallback clips. State cues
  fire once each; event cues are skipped for the target, who already hears its hurt sound.

Effects quality (Reduced) thins the particles and never strobes, flashes or tears. The low graphics tier also thins the
constructs and voxels and drops smoke.

## Animation impacts

None on the rules. The takes are timed to the replicated windows and hits; they never drive them. The participants' clock
warp is presentation only, and the bodies stand where the snapshot puts them.
The idle stance moves only the drawn rig: the hips 3.5 cm up over the same planted feet, and the glitch's tear up to
5.5 cm sideways. The capsule, movement, hit timing and the network frame are untouched.

## What the rules side still needs to provide (Astra)

Kernel Panic (PR #138) and the kit (PR #142) are done. The presentation falls back where her data has no field:
- No interrupt time in the kit: `Until` moves with the host's clock while an interrupt lasts. The feed estimates the cut
  from the actor's block, roll and reaction times. A `CutAt` (or keeping `Until` and adding `Interrupted`) would make it
  exact.
- Only one cast window: Packet Burst has no record of its own, so its window is gone once another cast replaces it.
- System Crash has no lasting field (one hit at .5 s). The presentation draws the field for the 1 s stun.
- The Siphon's heal has no Detail. The presentation pairs it with the drain hit from the same tick. A `siphon-link`
  Detail on the Heal would remove the pairing.
- The abilities' content `text` fields are their ids (for example "data-dash"), and the Locker rows show them as
  descriptions.
- The account service does not accept the `dev-cyberlord` entrance for DEV accounts (`src/relay/accounts.ts` keeps only
  `sheldon-intro` among the DEV-only intros).

## Checks and renders

`Tests/CyberLordChecks.cs` covers:
- the numbers against NEW-KITS section 3 and Astra's rules (her `NativeCyberRules.Predict`);
- the one field mapping, each record, the interrupt cuts and the Siphon heal pairing;
- each pack take's impact on its replicated hit;
- a copied actor, and the kit round-tripped through JSON, drawing the identical frame in any order;
- the effect lifecycles, the tiers and the no-floor-disc rule;
- the camera, clock and cut-in rules;
- the DEV gate on every UI entry, the HUD states and the audio keys;
- the idle stance: its triggers and blend out, the pose, the glitch schedule, reduced effects and the portraits;
- zero per-frame allocation.

The offline renders come from a scratch WebGL harness: real Core, real `NativeVfxDraw` and `NativeWeaponFxDraw`, and the
shader SHARED blocks. The 2D overlays (the cut-in, banner, HUD, tile and Locker) are canvas mock-ups of the runtime layout.
None of this has run in the Unity Editor or on Windows.
