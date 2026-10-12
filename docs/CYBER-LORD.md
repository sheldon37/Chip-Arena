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
- Kernel Panic's authority and replicated state are Astra's (PR #138, `d68043d`, in shared `1426250`, merged into this
  branch). The presentation reads them as they are.
- The rest of the kit is still spec-only: no content entry, combo or Q/E/1-3 rules exist. A Cyber Lord cannot be picked
  or played yet, so the checks drive the presentation through fixtures.

## The contract and the one field mapping

Everything reads replicated data through **`CyberLordFeed`** (`Core/NativeCyberLord.cs`). It is the only place that knows
field names. When the rest of Astra's fields land, change `CyberLordFeed.Read` and `CyberLordFeed.Details`. Nothing else
needs to change. `Tests/CyberLordChecks` enforces this: only the feed reads the kit and `Actor.Kernel`.

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

### The rest of the kit: still a proposal

These fields are read from `Actor.CyberKit` (a `NativeCyberKit`):

| View field | Proposed source | Spec meaning |
|---|---|---|
| `ComboStep`, `ComboAt`, `ComboUntil` | `ComboStep`, `ComboAt`, `ComboUntil`; else the standard `Actor.Combo`, `AttackAt`, `AttackUntil` | LMB Jab / Cross / Pulse Palm window |
| `ComboHit` | `Actor.SwingAt` inside the window, else the spec share (.42 / .41 / .62) | the hit moment each take's impact is pinned to |
| `At`, `Release`, `Until`, `RecoverUntil`, `Ready` [0-3] | `Casts[slot]` (Q, E, 1, 2, 3) | cast, active start, active end, recovery end, cooldown ready |
| `DashFrom`, `DashTo`, `TrailUntil` | same names | Q path and 1.2 s trail |
| `WallAt`, `WallYaw`, `WallBorn`, `WallUntil` | same names | E wall transform and life |
| `LinkTarget`, `LinkAt`, `LinkUntil` | same names | 2 tether target and end (early end = broken) |
| `CrashAt`, `CrashBorn`, `CrashUntil` | same names | 3 field position, slam and end |

Events are read with their `Detail` (`CyberLordFeed.Details`):
- proposed: `cyber-jab`, `cyber-cross`, `cyber-palm`, `data-dash`, `firewall`, `packet-burst`, `siphon-link`,
  `system-crash`;
- Astra's: `kernel-panic-1` to `-5`, and `kernel-panic-wave` on the others caught by the shockwave.
The kinds read are Hit, AbilityHit, Heal (the Siphon's heal), Absorb (the Firewall's absorbed shot), AbilityCast and
BurnTick.

`Actor.CyberKit` is `[NonSerialized]`, so the network frame is unchanged until Astra replicates it. `Actor.ViewCopy`
deep-copies it, and Astra's `Kernel` too. `CyberLordFixture` writes the spec's timeline the way the host would: the kit,
and `Actor.Kernel` as her `ConnectKernel` resolves it on an open floor. It is for the checks and the render harness; the
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
  each Heal event. It snaps with sparks if broken early.
- **3 System Crash**: energy gathers in the fists over the .5 s windup. The slam sends an upright wave out to 4 m. The
  field is a volume of corrupted voxels popping out of the floor, rising data and rim scan columns. Its victims show the
  glitched stun (RGB-split ghosts and crawling pixel blocks) for the 1 s stun.
- **4 Kernel Panic** (below).

Owner rule kept: no glowing disc, ring or tile lies on the floor. Areas read through their volume. Slam waves are upright.
`CyberLordChecks` checks this on every drawn piece.

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
- **Loadout tile**: once the content entry is `devOnly`, the existing tile shows the DEV violet dot. The role line reads
  "DEV · hardlight fists and cyber spells".
- **Locker entrance**: "DEV · Hardlight Boot" (`dev-cyberlord`), with a DEV badge, a diamond icon and the text "Built from
  light, then a hardlight palm strike." It is gated like every `dev-` cosmetic (a guest or a scoped grant cannot pick it).
- **Entrance** (`Core/NativeCyberLordIntro.cs`, directed). It plays on the standard 4.8 s with the name card at 3.2 s, on
  the shipped pulse-wave bed (150 bpm). `dev-*` styles get no music key by contract.
  1. A scan of light builds the pilot from the shins up.
  2. The gauntlets form on a close shot.
  3. Jab (1.2 s), cross (1.6 s), then the Pulse Palm at the lens (2.0 s, slowed, caught up before the card).
  4. The stance settles on the 3.2 s accent.
  A Cyber Lord pilot plays it by default.
- **Audio** (`Core/NativeCyberLordSounds.cs`): existing bank keys only (18 keys), with shipped fallback clips. State cues
  fire once each; event cues are skipped for the target, who already hears its hurt sound.

Effects quality (Reduced) thins the particles and never strobes, flashes or tears. The low graphics tier also thins the
constructs and voxels and drops smoke.

## Animation impacts

None on the rules. The takes are timed to the replicated windows and hits; they never drive them. The participants' clock
warp is presentation only, and the bodies stand where the snapshot puts them.

## What the rules side still needs to provide (Astra)

Kernel Panic is done (PR #138). Still needed:
- A content entry `cyberlord`: `devOnly`, class `melee` (unarmed), mana 100, keys Q, E, 1-4.
- The combo and Q, E, 1-3: the replicated fields above, or any equivalent. Map them in `CyberLordFeed`.
- Events with the proposed Details, including a Heal for each Siphon tick and an Absorb for each Firewall-absorbed shot.
- The input path for 1-3.
- The account service accepting `dev-cyberlord` for DEV accounts. `src/relay/accounts.ts` keeps only `sheldon-intro` among
  the DEV-only intros today.

## Checks and renders

`Tests/CyberLordChecks.cs` covers:
- the proposal's numbers against NEW-KITS section 3;
- the one field mapping;
- each pack take's impact on its replicated hit;
- a copied actor, and the kit round-tripped through JSON, drawing the identical frame in any order;
- the effect lifecycles, the tiers and the no-floor-disc rule;
- the camera, clock and cut-in rules;
- the DEV gate on every UI entry, the HUD states and the audio keys;
- zero per-frame allocation.

The offline renders come from a scratch WebGL harness: real Core, real `NativeVfxDraw` and `NativeWeaponFxDraw`, and the
shader SHARED blocks. The 2D overlays (the cut-in, banner, HUD, tile and Locker) are canvas mock-ups of the runtime layout.
None of this has run in the Unity Editor or on Windows.
