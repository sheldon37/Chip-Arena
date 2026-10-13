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

Status: implemented on `claude/cyberlord` only. Not pushed, merged or released. The Runner-pack run and sprint, the
double-jump presentation, the twitching idle (also the DEV "Sheldon Idle" lobby pose) and the chained dash trails are on
`claude/cyberlord-run` (not pushed).
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
  - The run and sprint are the CLazy Runner pack's (see "Run and sprint"); the Explosive pack's `run` and `sprint` takes stay
    in its generated file, unused, so its take indices and hashes are unchanged. First person has its own view pose.
- **LMB combo**: Jab, Cross, Pulse Palm. Each take's impact lands on the replicated hit, and a chained step starts from
  the previous step's end. Hit events add white hardlight sparks with a hex ring and pixel shards, and the mana flows from
  the hit into the gauntlets. The palm adds a forward hex-ring cone (1.6 m).
- **RMB guard**: a hex hardlight panel in front of the crossed forearms that flares on a block.
- **Q Data Dash**: the body goes translucent and pixelated through the dash. RGB-split glitch bands streak from the start,
  and a 1.2 s trail of hovering data pixels and an upright light curtain pulses on each .3 s tick. The replicated kit keeps
  one dash, so a chained dash used to snap the last trail away mid-fade; the effects now remember each pilot's dash trails
  (`Core/NativeCyberLordDashTrails.cs`, noted from the replicated window by its start, so guests and late joiners see them
  too), and an older trail finishes its own fade while the new one grows. With Data Dash's cooldown at 0 (PR #159: only
  the .2 s dash and its mana gate it) a trail lives about 1.6 s from its dash to the end of its fade, so dashes .2 s apart
  keep about eight alive: the ring holds `TrailsPerPilot` 8 per pilot (48 in all), the oldest retires first, and past the
  pilot's three newest the older trails draw on the low tier (fewer blocks, no rising bits). No allocation per frame.
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

The owner asked first for "a idle pose ... (Matching the sheldon look) slight glitch effects", then (2026-10-12): "Change
Cyberlord idle to be similar to the sword idle, but more "psycho" appearance, like they're twitching, etc. Same will apply
to their idle". The code is in `Core/CyberLordIdle.cs`. One look serves the match and the portraits.

**The stance** is the sword's own standing idle (`CyberLordIdlePose.Base`: the owner's Explosive Armed-Idle, `MeleeSet`
"sword" Idle): hunched, knees bent, the arms hanging out wide. The empty hands close into fists: the right one (the sword
hand) is drawn in from the empty blade's reach and turned knuckles forward, the left a little in. In a match it is the base
row under the twitches with the gait's planted feet (the fighter stance's spots); the hips crouch lower over them.

**The twitches** (`CyberLordIdlePose.Twitch`) are the "psycho" layer. Each kind has its own seeded schedule per pilot:
windows of an irregular length, an event in some of them at a seeded moment, so nothing runs like a metronome. Each twitch
is a fast attack (about .035 to .07 s), a short hold and an eased release (`Envelope`). It is a pure function of the seed
and the time, with no per-frame noise: between snaps the head and hips hold dead still.
- the head jerks to a tilt, a turn or a pitch, snaps and holds (some with a second, smaller snap further over);
- a shoulder hitches up with the neck pulled toward it (a spasm);
- a fist clenches (the wrist curls, the forearm tenses) and its hardlight shell flares (`Clench`, the look's flare);
- the breath is ragged: warped, uneven cycles of a swinging depth, with sudden gasps;
- an occasional full-body flinch: the body jolts back and up, shoulders hunched, the hips dip, then slowly lets go;
- weight shifts that stop short: the hips run toward one side, accelerating, halt dead, hold and ease back.
The head also follows a share of the view's pitch. All of it is bounded (the checks pin the joints).

**When it shows** is unchanged: a third-person pilot settles into it after 0.9 s standing still and blends in over 0.6 s
(alive, grounded, not sprinting, at 0.35 m/s or less, the gait's travel at zero). It blends out within 0.08 s on a step,
a jab, a cast, a guard, a roll, a hit reaction, a stun, a jump or Kernel Panic. A swing or cast must finish its settle and
blend out first. A fresh rig of a pilot already standing still shows it at once. The presented state is `cyber-idle`.
First person is unchanged: the twitches never move the view or shake the camera.

**The glitch** stays slight and occasional, seeded per pilot on the Sheldon Intro's 24 Hz steps, once the stance has
settled: the RGB-split shimmer (`Split`), the slice tear (`Slice`, `HeadSlice`), the scanline band (`Scan`) and a rare
two-step jolt (`Twitch`). The flinch and the biggest head jerks now flicker the split in sync (`CyberTwitch.Sync`).

**Reduced effects**: fewer and smaller jerks (under half the angles) with softer, longer attacks, smaller clenches, no tear
or jolt, the split eased in and out (the synced flicker one slow swell), the band a soft glow, nothing strobes.

**Portraits** (lobby, Locker, pilot card, the Kernel Panic cut-in): `CyberLordIdlePose.Stand` through the
`NativeLobbyPoses.Sample` overload is the same sword idle and twitches on the match idle's feet, turned a little to the
camera, unarmed. `Runtime/NativePortraits.cs` draws its hardlight look with the band, the clench flare and the split
ghosts. With reduced motion (portrait time 0) it holds still: no twitch, no glitch.

The idle is presentation only. It reads the actor and never writes it, and the hitboxes are the capsule, not the pose.

### Sheldon Idle (the lobby pose)

The owner: "Can that Idle be set as "Sheldon Idle" need it to be selectable", for the idle pose (lobby, portraits, pilot
cards and results). `Core/NativeLobbyPoses.cs`:
- **The pick**: a fifth IDLE POSE card, "Sheldon Idle" (id `sheldon-idle`, icon `activity`, "Hunched and twitching."), after
  the source's four (`Keys` stays the four that `Tests/LobbyPoseChecks` pins to `lobby-presentation.ts`; `Count`, `Key`,
  `Label`, `Icon` add it). It is DEV only, gated like the Sheldon Head: `NativeCosmeticGate.SheldonIdle` is DevOnly and
  Restricted, the Locker lists it only with live DEV access and badges it DEV, and `NativeCosmeticGate.Pose` (through
  `NativeCosmeticRules.Pose`) turns it into `weapon` for anyone else on an account load and at the host's seat. Guests take
  the host's gated pick through `NativeLobbyPoses.Normalize`, which knows it; unknown ids are still `weapon`.
- **Where**: the lobby, Locker, pilot-card and PvE-result portraits, everywhere `NativeLobbyPoses.Sample` poses a pilot. The
  match never reads the pick: a Cyber Lord's match idle stays its class idle above.
- **The pose** is the Cyber Lord's stance (`CyberLordIdlePose.Stand`): the same body motion and twitches, the same planted
  feet, seeded per pilot. An armed pilot's weapon arm (both for the greatsword, the guns and the daggers) keeps the hero
  stance's hold, carried onto the hunched, turned chest (`Grip`: measured once against the hero stance, raised back by the
  crouch's drop), so the weapon stays in the hand and rides the twitches: the hand rises with its shoulder's hitch and the
  flinch, and the grip tightens with the clench. The support hand stays on a two-handed grip, and the free hand keeps the
  stance's fist. The upright staffs and the Bow stay upright. The fighters (unarmed), the Cyber Lord and any other spec keep
  the stance's fists. The Siren still stands as herself. The glitch is the Cyber Lord's only.
- **Default**: a Cyber Lord's "Standing with weapon" is this same stance, so Cyber Lord pilots stand in it by default.
- **Saving**: the Unity side saves and replicates it as any pose (prefs, the profile's cosmetics, the seat frames, 32
  characters at most). The account service does not keep it yet, so a DEV pilot's pick returns to `weapon` on the next
  account load. The prompt is below.

### Astra prompt: Sheldon Idle save

Please let the account service keep the DEV-only lobby pose `sheldon-idle` for DEV accounts.
- `src/shared/lobby-presentation.ts` `lobbyPose()` keeps only the `LOBBY_POSES` keys and falls back to `'weapon'`, and
  `src/shared/cosmetics.ts` `cosmetics()` runs every saved profile through it. So the pick is saved as `weapon`.
- Accept `sheldon-idle` only for an account with live DEV access, the way the DEV-only `sheldon-head` and `sheldon-intro`
  are kept (`src/relay/accounts.ts`). Keep it out of `LOBBY_POSES` itself, so the Electron lobby picker does not list it
  (no new Electron features).
- Unity needs no change: the Locker, `NativeCosmeticRules.Pose` (account load, host seat) and `NativeLobbyPoses.Normalize`
  (guests) already gate and carry it; `Tests/SheldonIdleChecks.cs` pins that path.

## Run and sprint

The owner: "Change the cyber lord running / sprint animation, I have this for reference [BIPER Runner Action Animation
Pack]... Unless theres better ones in the pack we have." The pack we have is the owner's CLazy Runner Action Animation Pack.
`tools/export_pack_cyberlord_run.mjs` retargets it (`scripts/mocap/clazy-pack.mjs`: the pack's Unreal mannequin skeleton
read as Mixamo, its T-pose from the avatar in each `.meta`) into `Core/NativePackCyberLordRun.Generated.cs`, with a SHA-256
per track in `tools/pack-cyberlord-run-provenance.json`. No FBX, texture or `.meta` is committed.

**The choice.** The pack's run families, measured on their root-motion twins: Jog 3.2 m/s, Dash 7.6 m/s, Boost 10.1 m/s
(PowerDash and the Charge variants are the same strides with other arm carries). Scaled to the voxel legs (half a human's),
the Dash's stride is the closest to the gait's at the 6.8 m/s run and the Boost's to the 9.18 m/s sprint.
- **Run: Mvm_Dash** in eight directions (ahead, the strafes Front_L / Front_R, the diagonals Front_L45 / R45, the
  backpedal Back and its diagonals Back_L45 / R45). An athlete's run: a forward lean, the fists pumping hip to chin.
- **Sprint: Mvm_Boost** ahead and on the forward diagonals (the authority sprints only within 53 degrees of the facing). The
  same fist pump driven deeper and longer, so the sprint ramps in and out without the arms changing their carry.
- Not chosen: the Charge sets (arms swept back, a ninja run) would swing the arms through a different carry on every sprint
  toggle and hide the hardlight fists behind the body. The Jog is too slow for any replicated speed. The Explosive Unarmed
  run and sprint were an upright jog with a bladed torso turned 50 degrees off the aim.

**How it plays** (`Core/CyberLordRun.cs`, `CyberLordRunCarry`, the base row of `CyberLordMotion`):
- The legs stay the gait's planted procedural feet. Their stride is matched to the replicated speed
  (`MeleeGait.CycleLength`), as every other carry's is.
- The two takes bracketing the travel direction are sampled at the gait's stride phase from each take's left-foot strike
  (measured on its root-motion twin) and blended. The sprint takes blend over them on the replicated `Sprinting` flag's eased
  .15 s ramp (`NativeSprintRules`).
- Each keeps part of its per-stride swing about its cycle mean (the voxel legs step about 1.7 times the captured cadence):
  the torso .7 (sprint .8), the fists .68 and .72 (sprint .8).
- A strafe's mean chest turn is folded out of the spine, so the chest stays on the aim in every direction. The pack's
  diagonals twist the chest up to 34 degrees off it over hips facing ahead, and the melee carry's hip-heading fold would
  have kept that twist.
- A reach seat (NativeRangedLocomotion's rule) lowers the hips just enough for each planted ankle to reach its gait target.
  The fighter route's gait let a planted ankle skate up to .7 m/s on run diagonals, and the old Cyber Lord run up to
  .96 m/s. Now the contact foot stays under .03 cm/s at the run and sprint speeds in every direction.
- The existing sprint effect, the hardlight look and the first-person view are unchanged.

## Double jump

The owner: "Give this class a double jump as well; Second jump has a cyber effect". The air jump itself is simulation
(Astra; the prompt below). Today `Core/NativeJumpSimulation.cs` grants it to the daggers only, so the presentation below does
not fire in a match until the grant lands. The Dev Sandbox previews it ("Preview Cyber Double Jump").

Everything keys on the replicated `Actor.AirJumps` / `AirJumpAt` of a Cyber Lord pilot, read through a `NativeJumpTrack` on
the presented root: the same rule the dagger flip uses. The host, guests (the local pilot's predicted signal and the remote
pilots' replicated one, drawn later), the killcam and spectators draw the same thing.
- **The jump and fall** are now the Unarmed pack set's legs and hips, like the other DEV fighters'
  (`NativeJumpRules.SetFor`).
- **The second jump's pose** (`CyberLordAirBoost`): the Runner pack's Esc_BoostDash_Up_Fwd, from just before its push-off. One
  leg stamps down onto the hardlight hex as the body leaves it. Both knees tuck by .3 s while the body leans into the rise,
  arms driving. It holds to .38 s and has blended back into the fall by .6 s. The jump layer (`NativeJumpMotion`) takes its
  legs and hips, the style layer its torso and fists. Both read one jump track, so they start on the same frame. A cast or
  swing in the air keeps priority.
- **The cyber effect** (`Core/NativeCyberLordAirBoostFx.cs`, part of the Cyber Lord's effects):
  - A hardlight hex platform flashes under the feet and spreads and spins a little: a horizontal hexagon of light, a
    magenta echo and a white inner hexagon, with upright spokes and sparks at its corners. It is kept .22 m over the floor
    it left, so it is never a disc on the floor.
  - Pixel shards shed off it.
  - A data-glitch afterimage of the body stays at the take-off: two RGB-split ghosts, torn and flickering on 24 Hz steps,
    fading in .3 s.
  - A short scanline trail strings up the rise, the lowest lines fading first.
  - The palette is the Cyber Lord's cyan, magenta and white.
- **First person** gets a gentler version: the platform's outer hexagon at half strength under the camera, a flare at the
  view fists, and a third of the take's fists on the view model. There is no afterimage, trail, spokes or shards, and no
  camera motion beyond the existing view hop.
- **Reduced effects**: one ring, a soft swell instead of the flash, steady and fainter ghosts, four unflickering scanlines.
  The low graphics tier halves the counts.
- **Audio**: the Data Dash's whoosh, lighter, and a glitch tick (`NativeCyberLordSounds.AirBoost`: existing bank keys
  `move.roll` and `dev.leet.glitch`, shipped fallbacks), on the jump track's air jump (`Runtime/NativeMoveFeelAudio.cs`).
  The Dev Sandbox preview is silent.
- **Never the dagger flip**: PR #159 makes `NativeJumpRules.Flips` follow `NativeJumpSimulation.CanAirJump`, so a granted
  Cyber Lord would also flip. The flip pose (`NativeJumpMotion`), its air burst (`NativeJumpFxPresentation`) and its whoosh
  (`NativeMoveFeelAudio`) skip `CyberLordAirBoost.Handles` pilots; the boost replaces them. When merging onto PR #159, keep
  its `Flips` and `SetFor` lines and add `case CyberLordSpec.Id:` to the Unarmed case
  (`case "bow":case "pistol":case "emperor":case "ansatsuken":case CyberLordSpec.Id:return Unarmed;`).

### Astra prompt: double jump

Please give the DEV Cyber Lord (`cyberlord`) one air jump exactly like the daggers'.
- In `Core/NativeJumpSimulation.Try`, replace `spec!="daggers"` with one shared rule (for example
  `NativeJumpSimulation.HasAirJump(spec)`, true for `daggers` and `cyberlord`). The authority (`ArenaWorld`,
  `NativeArena.StepPilot`) and the guest prediction (`NativePrediction`, through `JumpSpec`) then grant it the same way.
- Keep the daggers' rules: an edge press only, impulse `JumpSpeed` x `AirMultiplier`, landing resets the budget, locks and
  swimming refuse, `jumpPower` scales once, and a same-tick jump attack's commitment wins.
- The Cyber Lord's own locks also refuse it: casts, Data Dash and Kernel Panic (`kitBusy` / `locked`), as for its ground jump.
- Replicate it through the existing `Actor.AirJumps` (0..1) and `Actor.AirJumpAt` (the air jump's tick time).
  `NativeNetworkFrame.Valid` already bounds both, and `PresentedJump` already shifts the local guest's predicted time.
- `Runtime/NativeJumpValidation.cs` needs updating:
  - Add `cyberlord` to the spec loop. It is DEV-only, so the fixture seat needs the DEV grant (as the Cyber Lord's own gates
    set it up).
  - Change the `spec=="daggers"` predicate and its "dagger-only air jump" message to the shared rule.
  - The walk-off, `jumpPower`, JSON, prediction, acknowledgement and attack/stun lock checks can stay on the daggers. Repeat
    the prediction seed and the lock refusal for `cyberlord` if its kit locks differ.
- `Tests/JumpSimulationChecks.cs` lines 16-17 (`weapon.id=="daggers"`) need the same shared rule.
- Optional: `Runtime/NativeBots.cs` lines 37 and 67 let obstructed dagger bots air-jump near the apex; extend them if Cyber
  Lord bots should too.
- No presentation change is needed. The boost, its effect and its cue already key on the replicated fields for `cyberlord`
  pilots, and `Tests/CyberLordRunChecks.cs` drives them with that contract.

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
- The idle stance moves only the drawn rig: the sword idle's crouch over the same planted feet, the twitches (the hips up
  to 4.5 cm sideways and 2.2 cm down on a flinch), and the glitch's tear up to 5.5 cm sideways.
- The run and sprint carry the upper body and lean. The reach seat lowers the drawn hips by up to 10 cm, and the feet are
  the gait's.
- The boost poses the drawn rig in the air only.
- The Sheldon Idle poses the portrait rigs only (never a match rig).
- The trail ring draws more trails at once; the authority's trail is unchanged.
- The capsule, movement speed, jump height, hit timing, cooldowns and the network frame are untouched.

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
- The air jump (the double jump the owner asked for): the prompt is under "Double jump" above.
- Chained Data Dashes: the kit keeps one trail. A new dash resets `CyberTrailTick` and overwrites `DashFrom` / `DashTo` /
  `TrailUntil`, so the earlier trail stops damaging when the next dash starts, though it still draws its fade. With no
  cooldown (PR #159) a spammed chain leaves up to eight drawn trails but only the newest damages. If each trail should
  keep ticking for its own 1.2 s, the kit needs a small trail history (about eight per pilot). Otherwise the shorter damage
  is the rule, and the older drawn trails are cosmetic.
- The Sheldon Idle save: the prompt is under "Sheldon Idle" above.

## Checks and renders

`Tests/CyberLordChecks.cs` covers:
- the numbers against NEW-KITS section 3 and Astra's rules (her `NativeCyberRules.Predict`);
- the one field mapping, each record, the interrupt cuts and the Siphon heal pairing;
- each pack take's impact on its replicated hit;
- a copied actor, and the kit round-tripped through JSON, drawing the identical frame in any order;
- the effect lifecycles, the tiers and the no-floor-disc rule;
- the camera, clock and cut-in rules;
- the DEV gate on every UI entry, the HUD states and the audio keys;
- the idle stance: its triggers and blend out, the sword idle with fists, the twitches (pure, irregular, snapping and
  holding, every kind, bounded, the feet planted, fewer and smaller when reduced), the glitch schedule and its sync, the
  portraits;
- zero per-frame allocation.

`Tests/CyberLordRunChecks.cs` covers:
- the Runner pack takes against their provenance;
- the run in eight directions and the sprint on the replicated flag: the contact foot never slips, the takes follow the
  travel, the chest stays on the aim, the fists pump, the ramp has no pop;
- the boost keyed only on the replicated air jump, on the host's and a delayed guest's timeline, torso and legs on the same
  frame, the Dev Sandbox preview, the daggers' flip unchanged;
- its effect: once per air jump, its lifetime, the first-person and reduced versions, never flat on the floor, the audio keys;
- chained Data Dashes: two .7 s apart (both draw, the first finishes its fade), and six .2 s apart with no cooldown (all
  held, each finishing its own fade, the oldest first, the older ones on the low tier; past the cap the oldest retires);
- the flip's three paths skip the Cyber Lord;
- zero per-frame allocation, a Data Dash spam's ring included.

`Tests/SheldonIdleChecks.cs` covers the Sheldon Idle: the DEV-only listing and the gate on the account load and the host
seat, the fallback for unknown ids, the match never reading the pick, the Cyber Lord stance's body and twitches for 17
classes, each weapon on its own hand with the hand reaching its hold, two-handed support grips, the free fist, the weapon
clear of the podium and riding the twitches (less with reduced effects), the Cyber Lord's default, determinism and no
allocation.

The offline renders come from a scratch WebGL harness: real Core, real `NativeVfxDraw` and `NativeWeaponFxDraw`, and the
shader SHARED blocks. The 2D overlays (the cut-in, banner, HUD, tile and Locker) are canvas mock-ups of the runtime layout.
None of this has run in the Unity Editor or on Windows.
