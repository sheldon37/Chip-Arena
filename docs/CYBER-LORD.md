# Cyber Lord — presentation

## 2026-10-12 simulation follow-up (`codex/cyber-jump-sheldon-idle`)

Based on merged presentation head `466130a1`. Cyber Lord now shares the dagger/Bow one-air-jump rule: edge input, landing reset, .9 × the already-scaled jump speed, swim separation and action locks. `AirJumps` / `AirJumpAt` remain the replicated signal; Claude's boost needs no visual change. Prediction now includes active Cyber casts in its jump lock; Data Dash already returns through its movement path, and Kernel Panic snapshots disable movement. On simultaneous Link/Kernel requests, prediction conservatively leaves the jump budget untouched until the authority resolves the targeted cast (a refused cast can reconcile to the host's jump).

Accounts source now preserves `sheldon-idle` only using the current account row's DEV access, on save and read. It remains absent from `LOBBY_POSES`, and the shared Electron normalizer still returns `weapon`. Claude's NativeNetwork host-seat and NativeProfileUI account-load edits are correct: both use `NativeCosmeticGate.Pose` with verified access; that gate requires DEV plus unexpired access and rejects item-only grants. No changes to those lines or visuals were needed. Accounts deployment is still required for server persistence.

Data Dash review: the authority stores one path/expiry/tick timer per pilot. A new dash replaces it and schedules the next tick .3 s after the new .2 s dash ends; recasting before that tick can erase all remaining trail damage. Each uninterrupted trail deals four ticks × 6 = 24 damage per eligible target, in addition to the 18 direct dash hit. The existing spec promises a 1.2 s trail and does not establish replacement as an intentional balance rule. Treat this as a single-trail implementation limitation exposed by zero cooldown, not an owner-approved design decision. No damage behavior changed here; independent damaging trails would need bounded authority history and an overlap/stacking decision. Claude's older fading trails remain cosmetic.


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

## Authority integration (2026-10-15)

`codex/unity-cyber-v2-stage-gate` implements the v2 authority from shared `c9a87f6`.
`NativeCyberKit.Version=2` owns `Kit2` and `Kit2R`, reusing the proposed records below.
No CyberKit2Spec numbers changed. Per-projectile `Draw` carries the hand, while
`Kit2R.PlasmaShot` carries the throw index. `UplinkFrom` becomes the latch-time body
centre when the cable connects so moving during flight never snaps the caster back.
The optional generic barrel/muzzle offset remains deferred. See DEV_LOG and the PR
for tests and promotion status; managed compilation is not Windows play acceptance.

## Kit v2 (2026-10-15): design and the rules contract

Owner (2026-10-15): "fix Cyber Lord class- its current abilities are ... boring lol. Cyberlord isn't intended to be
balanced, I want to have a very fun dev class to play. I like the melee "4" ability, but we need to change the other ones
completely. Ranged is also intended to be like a cyber mage- throwing like "Cyber-themed fire balls" for their basic attack.
Currently the animations are too bland with a lot of inconsistent things (Animation is bad right now on the ranged
especially) ... make the class a lot more fun to use, make sure the animations are great, beautiful fx, better basic
attacks, (much better combo animation on melee), overhaul abilities except melee special attack "4", make sure sprint works
fine (sprint animation on ranged doesn't look good at all), allow movement during ranged attacks on this class (Previously
it would make us stop to attack)".

Who builds what: Claude designs the kit and builds the presentation (poses, effects, HUD cards, icons, sounds routing, the
sandbox preview). Astra builds the rules: the casts, damage, movement, the wire record, prediction, bots and the sandbox
inputs. The numbers live in `Core/CyberLordKit2.cs` (`CyberKit2Spec`); its pure helpers (the storm's drops, the clones'
formation, the chain's falloff, the leap arc and dive, the orbital chase, the Overflow cadence) are shared: the host resolves
with them and every client draws with them. It is a DEV power fantasy, not balanced.

### The kit

| Key | Ability | What it does | Numbers (`CyberKit2Spec`) |
|---|---|---|---|
| Q (both) | Uplink | A hardlight cable along the aim. Terrain: the pilot zips to the anchor, keeping momentum (attack or jump out of it). An enemy: yanked to 2 m in front and stunned. A boss is not moved: the caster zips to it. A miss retracts. | 24 m, cable 90 m/s, zip 30 m/s for at most .8 s, yank .35 s, stun .6 s; 6 s, 10 mana |
| E (both) | Overclock | A 6 s power state: faster movement and attacks, cooldowns recover twice as fast, melee hits chain an arc to a nearby enemy, the ranged LMB splits into three. | +35% move, +60% attack rate, x2 recovery, arc 8 in 6 m, split 7° at 40%; 16 s, 30 mana |
| LMB (melee) | Hardlight Combo | Five steps (drawn as a martial string): jab, roundhouse kick, spinning heel kick (an arc round the pilot), launcher (a short launch), Pulse Palm (a cone shockwave). | .24/.26/.32/.34/.42 s, windups .08/.08/.12/.12/.16, damage 9/10/12/12/18, reset .7 s, reach 2.4 m, spin arc 3.5 rad, launch 6 m/s (juggle .8 s), palm shove 9 m/s |
| 1 (melee) | Fork Bomb | Three hardlight clones keep formation for 5 s and copy every combo swing on the nearest enemy near them; at the end (or a recast) each dashes to an enemy and bursts. | 35% of the swing within 3 m; burst dash .3 s within 10 m, 20 in 2.5 m; 14 s, 35 mana |
| 2 (melee) | Glitch Cut | An instant dash-slash through enemies along the aim; everyone passed is marked and all marks detonate together after a beat (a freeze-frame). | 9 m in .14 s, 1.2 m wide, marks burst .45 s after, 25 each; 8 s, 20 mana |
| 3 (melee) | Meteor Byte | Crouch, leap 5 m, hover while a reticle follows the aim, then dive with a giant hardlight fist: a shockwave that launches. | leap .45 s, hover .25 s, dive .2 s, 14 m reach, 35 in 4 m, launch 7 m/s (juggle 1 s); 12 s, 35 mana |
| 4 (melee) | Kernel Panic | Unchanged (the owner's favourite). | NativeKernelPanicRules |
| LMB (ranged) | Plasma Byte | Cyber fireballs from alternating hands, every third an Overflow from both hands; small splash. Fired while moving at full speed. | every .3 s, 38 m/s, radius .22, 10 direct + 6 splash in 1.6 m; Overflow radius .35, 18 + 10 in 2.6 m; 45 m; +3 mana per pilot hit |
| 1 (ranged) | Daisy Chain | Chain lightning: a bolt to the aimed enemy, then up to five more, each a little weaker, slowing each. | 28 m, 6° assist, hops within 8 m, 20 then -15% a hop, .06 s apart, 30% slow .6 s; 6 s, 20 mana |
| 2 (ranged) | Singularity | A thrown orb stops on the first surface or pilot and becomes a black hole that pulls enemies in, ticking, then implodes and throws them out. | orb 18 m/s to 24 m, 2.5 s, pull 7 m (6 m/s at the edge, 12 at the core), 5 per .5 s, implosion 40 in 4 m, shove 8 m/s; 15 s, 40 mana |
| 3 (ranged) | Packet Storm | A ring on the aimed floor point; ten cyber meteors rain into it. | 30 m, radius 6, 10 drops over 1.6 s (each seen falling .35 s), 14 in 2 m; 12 s, 35 mana |
| 4 (ranged) | Orbital Uplink | Call down a steerable orbital laser that chases the aim's floor point; the caster moves slowly while steering. Shares Kernel Panic's ultimate clock. | call-down .6 s, 4 s, radius 2.2, chase 9 m/s within 35 m, 30 per s in .25 s ticks, 40% slow, caster at 60% speed |
| 5 | Reconfigure | Unchanged (the mode switch). | CyberRangedSpec |

Movement: every ranged cast and Q/E keep the pilot moving (`MoveScale`; Orbital Uplink at .6). Today the host zeroes the
move velocity and holds AttackEnd for each cast (`NativeArena.CyberBegin`); for v2's ranged casts it must not. Glitch Cut,
Meteor Byte and Kernel Panic move the body themselves.

### Astra prompt: kit v2 rules

Please build the v2 rules from `Core/CyberLordKit2.cs` (numbers, windows, the shared helpers) and replace the v1 kit (Data
Dash, Firewall, Packet Burst, Siphon Link, System Crash; Bit Bolt, Ping Lance, Lag Field, Proxy Swap, Root Access) except
Kernel Panic and the key-5 switch. Proposed wire record, read only by `CyberLordFeed`:
- `NativeCyberKit.Version` = 2 when the v2 kit is live (the presentation keeps v1 until it sees 2).
- Per slot (`CyberKit2Spec` order): At, Release, Until, Ready (and a cut time when an interrupt ends a cast).
- Combo: the step 0-4. Plasma: the shot count since the chain began and each fireball as a projectile with Spec
  `cyberlord`, Detail `plasma-byte` or `plasma-overflow`, its hand.
- Uplink: From, Anchor (the hit point), the yanked pilot (0 none), the outcome (zip, yank, miss), ArriveAt.
- Overclock: At, Until.
- Fork Bomb: At, Until; per clone its position and yaw (the host's, per tick), its last strike time and target, its burst
  time and point.
- Glitch Cut: From, To, the marked pilots (up to 8), DetonateAt.
- Meteor Byte: From, Target (the reticle at the dive's start), LandAt.
- Daisy Chain: the struck pilots in order (up to 6) and their points, At.
- Singularity: From, Point (where it stopped), LandAt, ImplodeAt.
- Packet Storm: Centre, Seed, Release (drops from `StormDrop`).
- Orbital Uplink: CallAt, Start, Until, the column's floor point (per tick; `OrbitalStep`), an early end time.
- Event Details: `uplink-zip`, `uplink-yank`, `overclock`, `fork-strike`, `fork-burst`, `glitch-mark`, `glitch-detonate`,
  `meteor-impact`, `plasma-hit`, `plasma-overflow-hit`, `chain-hop`, `singularity-tick`, `singularity-implode`,
  `storm-drop`, `orbital-tick`.
- Prediction for the caster's own movement during ranged casts; bots that use the kit; the sandbox's numeric keys.
Report any number or field you change; the presentation follows the record.

### Presentation status

Built by Claude on `claude/cyberlord-v2-*` branches: the ranged cyber-mage animation (throws layered over the run so the
legs keep moving), the Plasma Byte fireball look on today's Bit Bolt events, the ranged sprint, the melee combo animation,
and every v2 ability's poses, effects, HUD cards and icons, previewed in the sandbox until the v2 record lands. Status per
branch is in DEV_LOG.
- Ranged (`claude/cyberlord-v2-ranged`): live today on the v1 rules: the cyber mage's stance, the alternate-hand throws on
  the Bit Bolt window, the fireball and its splash on today's bullets and events, the held fireballs and cast flash, the
  glide sprint and the fire sounds. Built and previewed, waiting for the v2 record: the Overflow, Daisy Chain, Singularity,
  Packet Storm and Orbital Uplink (poses, effects, HUD cards, icons, sounds). Details below.

### Ranged presentation (Kit v2 branch)

**Live today on the v1 rules** (`Core/CyberLordRangedMotion.cs`, `Core/NativeCyberLordPlasmaFx.cs`):
- **The stance**: a pitcher's set. The next throw's hand is loaded beside the head (`CyberRangedPose.Loaded`, the elbow set out
  and back), the other forward on the aim (`Guard`); the chest coils .17 rad toward the loaded side (less while running) and
  the neck takes the coil back out, so the head stays on the aim. The loaded hand half-follows the pitch, the forward hand all
  of it. The run adds a 1.6 cm bob at the stride; the legs are always the gait's.
- **The throws** (the newest window: v1's Bit Bolt, `RAt`/`RRelease`/`RUntil`/`BoltHand`; v2's Plasma Byte once its record
  is live): the loaded hand whips overhand from behind the shoulder on a curve through the release point (`ReleaseAt`),
  accelerating into the replicated release and decelerating out to full extension (`Extend`) one windup later, then
  settles forward while the other hand loads. The chest winds a little further, uncoils .3 rad through the release (the
  throwing shoulder forward) and comes round to the next stance's coil. A window starts and ends on the stance it inherits
  (the loaded hand is always the next throw's), so throws chain at any interval with no pop; a cut throw settles back.
  The Overflow (v2, every third) gathers both hands at the chest and pushes them out together, the chest leaning back then in.
- **The glide** (the ranged sprint, on the carry's eased sprint ramp): both arms swept back and out square to the run
  (`Glide`, from the shoulders' line whatever the torso's twist), the palms trailing and lifting at the stride, the dash
  sprint's lean lifted .3 rad. A throw from the glide swings forward from behind and back. Energy streams off the palms
  (`NativeCyberLordFx.Glide`: a magenta flame ribbon with a cyan and white heart, 1.5 m at full sprint, shedding pixels).
  The melee mode keeps the dash sprint's swept fists and streaks.
- **The Plasma Byte fireball** on every Cyber Lord shot (today's `bit-bolt` bullets; v2's `plasma-byte` / `plasma-overflow`):
  a white-hot core in a cyan heart and a magenta plasma body with a soft halo, three flame tongues streaming back (the
  weapons drawer's flame cards through the optional `ICyberFxFlames`; a sink without it draws a streak), a short cyan motion
  streak, a tail of pixel embers drifting where it passed, and on random 24 Hz steps an RGB-split copy of the core with a
  scanline tear. For its first .12 s it eases from the throwing hand onto the replicated path (the hand from the owner's view:
  `BoltHand` or the v2 shot's hand), so the ball the hand throws is the one that flies. Its halo, body and core keep a
  minimum angular size (`FarHalo` .03, `FarBody` .014, `FarCore` .006 rad), so it reads at 40 m. Sizes by kind: a Bit Bolt
  .85, a Plasma Byte 1, an Overflow 1.6 with code bytes orbiting it. A tongue is never drawn back past the eye (first person).
- **The splash** (a Hit / Impact with the fireball's Detail, the preview's impacts): a white flash, a plasma glow swelling,
  a cyan hex ring, nine plasma petals blown out round the surface normal (the event's Normal), hex shards flung with gravity,
  and on the world a scorch flicker (upright flame licks and a wisp of smoke for .8 s). v2's splash radius draws as a fading
  shell (1.6 m, the Overflow 2.6 m); today's Bit Bolt has no splash.
- **The hands**: a fireball held over each palm in ranged mode (the loaded hand's full, the forward hand's smaller;
  `CyberRangedPose.Hands` from the same windows as the pose), spent on the throw and re-forming over .22 s; a cast flash and
  a fan of flame where it leaves the hand.
- **Sounds**: each throw `magic.fire.shot` (shipped `fire`) with a faint `dev.leet.glitch`; its hits `magic.fire.hit`.
- v1's other casts (Ping Lance, Lag Field, Proxy Swap, Root Access) keep their looks from the new stance (the swap's tuck
  eases over .15 s, the lob's back-swing is shorter), the coil easing out as they start.

**Kit v2's ranged set** (previewed until the v2 record replicates; `Core/CyberKit2Ranged.cs`, `Core/NativeCyberLordKit2RangedFx.cs`,
`Core/CyberKit2RangedHud.cs`):
- **The record and its one mapping**: `CyberKit2RangedRecord` is the proposed wire record of the ranged half (fields below).
  `CyberLordFeed.Read` maps it into the view's one field (`CyberLordView.Kit2R`, a `CyberKit2RangedView`) through
  `CyberKit2RangedFeed.Map` only when its `Version` is 2; any other version leaves v1 exactly as it is. Until Astra's record
  lands, `CyberLordFeed.Kit2RangedSource` supplies it (the sandbox preview, the fixtures). When it lands, the one line in
  `CyberLordFeed.Read` reads her record instead. Event Details map through `CyberKit2RangedFeed.Hit` (values past v1's
  `CyberHit` list, so the shared enum is unchanged); `CyberLordFeed.ShotSlot` takes `plasma-byte` and `plasma-overflow` as
  the Cyber Lord's shots (the default drawer skips them) and `CyberShot.Kind` carries which.
- **1 Daisy Chain**: the right arm thrown out at the first strike through the .12 s windup with the left hand bracing its
  forearm, a tremor through the hops; arcs crackling off the hand through the windup. Then a jagged two-strand bolt (a cyan
  body, a white core, a soft cyan halo, a fork and magenta glitch sparks, re-seeded at 30 Hz) from the hand to the first
  pilot and on to each next on `HopAt(i)` (Release + i x HopDelay), each dimmer with the hop's damage falloff, lingering .2 s
  past the cast; a white flash and a cyan ring on each struck pilot and the slow's static crawling over them for .6 s. The
  struck pilot's live chest is used when it is in the scene, else the replicated point.
- **2 Singularity**: the orb (a black sphere with a magenta rim) gathered between the hands low at the chest and pushed out
  and up together; it flies trailing violet and two orbiting sparks to its stop. The hole (`OrbPoint`, lifted .9 m off a
  floor): a black event horizon, a white and a magenta photon ring, light bent in camera-facing arcs round it, a tilted
  accretion disk (two dashed rings turning and streaks spiralling in, violet to magenta to white), debris drawn in from the
  7 m reach along spirals, faint streaks at the reach's edge, an inward pulse each .5 s tick and the pulled pilots stretched
  toward it. The implosion: the hole collapses to a white point over .12 s, then a cyan shell, a ring at the hole's height
  and radial streaks out to the 4 m radius, shards flung, a little smoke.
- **3 Packet Storm**: the right arm raised to the sky through the windup (a beam of data from the hand), then brought down to
  point at the ring. A floating 6 m hex ring with stuttering upright posts at its corners and an inner dashed ring turning
  the other way, a glitch portal 16 m overhead. Each of the ten drops (`StormDrop` from the replicated seed) is telegraphed
  .55 s ahead by upright brackets and a light column, falls for its .35 s as a glowing cube trailing flame and pixels on a
  slant, and lands in a white burst, a floating ring out to its 2 m radius, flame petals, shards, a data pillar and smoke.
- **4 Orbital Uplink**: through the .6 s call-down the left hand rises to the sky with a data line up from it and the right
  arm points at the call-down point, where a targeting ring closes from 3.6 m to 2.2 m with turning brackets, a crosshair
  and a spotter line. Then the column (a white core in a cyan sheath with a deep-blue glow and magenta edges, code blocks
  pouring down it, a pulse on each .25 s tick) stands on `OrbitalAt(t)`: the replicated point advanced toward the replicated
  aim with `OrbitalStep`, so it chases smoothly between snapshots. The ground burns round its foot (upright flame licks,
  embers, smoke, a floating ring) and along its recent path (a small per-pilot ring of points noted every .08 s, fading over
  .8 s). The caster steers with a pointing arm that follows the column, the left hand held up; a faint link runs from the
  hand to the column's foot. The column narrows away over .25 s at its end. A camera in or near the column (a pilot it
  chases, or a chase camera it sweeps past) sees its light dimmed (30% within 1 m of its axis, full from 7 m).
- **Poses**: each cast's targets blend over the stance on its window (in over .12-.2 s, out over its settle), so the legs keep
  the gait's walk, run or strafe; the states are `cyber-plasma-byte`, `cyber-plasma-overflow`, `cyber-daisy-chain`,
  `cyber-singularity`, `cyber-packet-storm`, `cyber-orbital-call`, `cyber-orbital-steer` (and `cyber-ranged-sprint`).
- **HUD** (`CyberKit2RangedHud`; the drawer shows it once the record is live and the pilot is in ranged mode, v1's set
  otherwise): `1 · DAISY CHAIN`, `2 · SINGULARITY`, `3 · PACKET STORM`, `4 · ORBITAL UPLINK` with CHARGE through a windup,
  ACTIVE through the cast and its lasting effect (the hole to its implosion, the storm to its last drop), CALL and STEER for
  the column, the cooldown from the replicated ready (Orbital Uplink also waiting on Kernel Panic's clock), LOW MANA, READY.
- **Icons** (`tools/export_class_icons.mjs`): `ability-plasma-byte` (a fireball with flame tongues and shed bytes),
  `ability-daisy-chain` (a bolt hopping between three struck rings), `ability-singularity` (a hole with spiral arms),
  `ability-packet-storm` (meteor cubes streaking onto a ring), `ability-orbital-uplink` (a satellite's beam onto a ring).
  The melee branch adds its own.
- **Sounds** (`CyberKit2RangedSounds`, existing keys with shipped fallbacks): each throw `magic.fire.shot` (the Overflow adds
  `blast.fire`); the chain's charge `chip.delayed.charge`, its strike `magic.lightning.shot` and each hop `status.shock-zap`;
  the orb's throw `gun.frag.throw`, the hole `magic.singularity` and `field.black-hole`, the implosion `blast.implosion` and
  `blast.nova`; the storm's call `magic.meteor.incoming` and each drop `blast.meteor`; the column's mark `gun.orbital.mark`,
  its start `blast.shock` and `field.storm`, its end `dev.leet.glitch`; the hits by Detail. A cut cast is silent past its cut.
- **Tiers**: Reduced never strobes or flickers (steady glitches, fewer pieces), the low tier halves the counts and drops smoke.
  Nothing lies flat within 12 cm of the floor (rings float at .22 m, telegraphs stand up; arcs below a hole's floor are not
  drawn).
- **The Dev Sandbox preview**: DEV panel, Previews tab, GALLERIES, "Preview Cyber Kit v2 ranged" (`gallery-cyber-kit2-ranged`,
  `Runtime/NativeCyberLordKit2Preview.cs`). As a Cyber Lord, press 5 for ranged: the loop then plays six throws (two
  Overflows), Daisy Chain over the nearest pilots on the aim (an arc of points when none), Singularity where the aim meets the
  world, Packet Storm on the aimed floor point and Orbital Uplink steered by the live aim, on your pilot, locally. Move while
  it plays: the legs stay the run's. Nothing reaches the authority.

**Contract additions for Astra** (fields the presentation needs beyond the list above; the proposal is
`CyberKit2RangedRecord`, Unity-serializable, with `Valid()` bounds):
- Write a Version 2 record for every Cyber Lord from the start (all windows -100), not on its first cast: the stance's loaded
  hand is the next throw's, and v1's first bolt loads the other hand.
- Per slot a `CutAt` (the interrupt's time, -100 none) alongside At / Release / Until / Ready.
- `PlasmaShot`: the newest throw's index since the chain began (the hand is `PlasmaHand(PlasmaShot)`, the Overflow every
  third); the bullets' Detail `plasma-byte` or `plasma-overflow`.
- Daisy Chain: `ChainCount`, `ChainIds[6]`, `ChainPoints[6]` (the struck body centres) and `ChainFrom`.
- Singularity: `OrbPoint` is the hole's centre, lifted `HoleLift` (.9 m) off a floor the orb stopped on.
- Orbital Uplink: Release is the column's start and Until its end; `OrbitalCentre` (the call-down point), `OrbitalPoint` at
  the host tick `OrbitalPointAt` and `OrbitalAim` (the aim's floor point, clamped to `OrbitalRange`).
- v1 today: `CyberRangedRelease` spawns a bolt `.18` m to the pilot's left for `BoltHand` 0 (the presentation's right hand);
  the drawn fireball eases from the right hand regardless, but the spawn's side could follow the hand.

Melee and shared side (`claude/cyberlord-v2-melee`): the hardlight string is live today on v1's three steps (jab, cross,
Pulse Palm on v1's replicated windows); v2's five steps, Uplink, Overclock, Fork Bomb, Glitch Cut and Meteor Byte draw from
the proposed record below once it carries Version 2, and play now in the Dev Sandbox preview.

### Melee and shared presentation (v2)

**The hardlight martial string (2026-10-15, `claude/cyberlord-martial-combo`).** Owner: "Improve basic attacks for melee~
Its a bit basic right now. I want more expressive martial art attacks rather than just simple arm movement. Throw a little
"Cyber movement" into it as well." `Core/CyberLordCombo.cs` draws the combo as full-body martial arts on the replicated swing
(`CyberCombo.Swing`: At, Hit = the kit's Release, Until), key tables per step sampled with C1 Hermite curves, the apex of the
striking limb on the replicated hit (checked, with and without the Overclock's compression):

| Step (wire kind) | Technique | Striking limb |
|---|---|---|
| 1 `cyber-jab` | Glitch-step jab: the windup snaps forward in three frame-skip holds (`CyberCombo.Stutter`) as the lead foot steps in 16 cm and back out; the lead fist drives out, the rear fist chambers palm up at the hip; RGB afterimages trail the step-in. | lead fist |
| 2 `cyber-cross` | Rear roundhouse kick (mid): the rear knee chambers, the hips turn over (1.7 rad) on the lead foot pivoting on its ball, the body leans away, the shin snaps across the front at belt height; the rear arm counter-swings down and back, the lead fist guards. | rear foot |
| 3 `cyber-spin` | Spinning heel kick: a full turn to the right on the lead foot's ball (it pivots with the hips), the head spotting, the rear knee chambered behind the hip, the heel driven out at the target with the back turned (3.25 rad into the turn), sweeping on and landing bladed. | rear heel |
| 4 `cyber-rise` | Launcher: a deep drop, then a hop (the drawn body up 9-10 cm, the lead foot off the floor 6 cm) with the rear knee driven up to hip height in front (a jumping knee) as the rear fist rises through the chin line on the hit and on overhead. | rear fist, knee |
| 5 `cyber-palm` | Pulse Palm: the lead knee rises and stomps 16 cm in and 8 cm out into a low horse stance (the rear foot turns its toes forward), both palms thrust on the hit and held dead still for a glitch freeze-frame (`CyberCombo.PalmFreeze`, a quarter of the follow-through). | both palms |

v1's three records (jab, cross, palm) draw steps 1, 2 and 5. Chained steps blend from the pose the rig actually showed; the
first eases in from the stance (or the melee idle, `Core/CyberLordIdle.cs`, unchanged). The striking leg follows the keys'
`KX..KA` channels (an ankle path in the hips' turned frame, a knee direction, the foot's point) through `CyberComboFeet`: the
foot rises straight off the floor before the keys carry it across (`Lift`), then comes down onto its step home; only the
striking leg leaves the floor (the jab's step, the launcher's hop and the palm's stomp are low). A planted foot never slides
(its ball is pinned flat or pivoting; checked under 2 mm per frame, measured 0.1-0.4 mm), nothing goes through the floor, and a
planted foot the hips have left out of reach makes the drawn body sink (at most 10 cm, `CyberComboFeet.Reach` / `MaxSink`)
rather than lift off. Every foot is down by the window's end. The drawn body's offset from the capsule stays under 8 cm and
returns as the string settles. An interrupted step (`CutAt`) keeps its own motion on its nominal windows and fades over the
settle (a cut kick lowers its leg instead of snapping it home); a raised guard still takes over at the cut. The idle's
twitches under a strike keep the planted feet (`MeleeRig.HoldAnkles`). Reduced effects keep the smooth clock (no frame-skip).

Cyber movement and effects (`Core/CyberLordComboFx.cs`, on the limb that strikes): hardlight ribbons (white core, cyan sheath,
magenta glitch edge, pixel bits) behind the jab's and the uppercut's fist and the kicks' foot, a snap flare at full extension,
the heel kick's crescent at the foot's height swept the turn's way over `SpinArc`, the launcher's knee flare, climbing
crescent and upward pixel smear, the palm's pulse with the stomp blocks round the foot where it lands, the jab's RGB
afterimages behind its step-in and an RGB split held on every impact (through the palm's freeze-frame) as `CyberGhost.Split`
ghosts; with reduced effects one faint afterimage and a small steady split (never a strobe); the local first-person pilot draws
no ghosts or kick effects. Sounds use existing keys: the roundhouse lands on `dev.fist.hit` / `arcade-kick`, its swing on
`structure.whoosh`, the jab's swing adds a `dev.leet.glitch` blip, the palm adds a `move.land` stomp.

First person (`CyberLordMotion.FirstPerson`, the camera never moves): punches and palms drive out from a bent arm (the
stance's push toward the view, `ViewLift`, eases off under a step: `StrikePush`), the kicks keep the lead hand guarding while
the rear hand counter-swings out of view, and the spinning heel kick keeps both hands in front (the followed turn is left out
of the view model). Fork Bomb clones copy the whole string, kicks included, on their own rigs (checked).
Kernel Panic, its clones and the entrance keep their takes.

**The v2 path (waits for the record).** Files: `Core/CyberKit2Melee.cs` (the proposed record `CyberKit2MeleeRecord`, its
source, the view `CyberLordView.Kit2`, the one mapping `CyberKit2MeleeFeed`, the shared body paths and the fixture),
`Core/CyberKit2MeleeMotion.cs` (the poses), `Core/CyberKit2MeleeFx.cs`, `Core/CyberKit2MeleeHud.cs`,
`Core/CyberKit2MeleeSounds.cs`, `Runtime/NativeCyberKit2MeleePresentation.cs`. `CyberLordFeed.Read` calls
`CyberKit2MeleeFeed.Read` at its end; only a record with Version 2 maps, and then v1's combo and Q to 3 windows are cleared
(Kernel Panic and the ranged mode stay). Anything else leaves v1 bit-identical (checked: poses and effects).
- String: the martial string above (glitch-step jab, rear roundhouse kick, spinning heel kick, launcher, horse-stance Pulse
  Palm); the same strike machinery as v1's.
- Uplink: the cast pose and a cable (white core, cyan sheath, spinning links, a hex anchor) that flies at CableSpeed; a zip
  pose dragged along it with RGB-split streaks and a landing burst; a yank (the caster hauls, the victim is pulled and
  sparks with the stun); a miss reels back.
- Overclock: a flex and a white burst with a ring, then for 6 s RGB-split afterimages, a white-hot aura (`FlareOf`), fist
  crackle, chained arcs on `overclock` events and the HUD's edge glitch. `NativeCyberLordFx.OverclockSplit` is the hook for
  the ranged split's look.
- Fork Bomb: three see-through clones of the pilot model in `ForkSlot` formation, each posed on a rig of its own copying the
  caster's swing with a .06 s + .035 s x i lag (stand-in records under ids -2000 - caster x 3 - i, presentation only),
  glitch shimmer, a dash streak and a burst with an upright glitch ring.
- Glitch Cut: a coiled windup, the dash-slash streak with afterimages along the path, marks on every marked pilot, all
  detonating at DetonateAt (white X slashes, shards) with a freeze-frame flash on the caster's and the marked pilots'
  screens.
- Meteor Byte: crouch, leap (`LeapArc`), hover with a raised fist building a giant (about 0.8 m) hardlight fist and a floor
  reticle (upright posts, a floating hex ring) following the live aim, a dive (`DiveShare`) led by the fist, an impact
  shockwave and a crater of upright shards that lingers CraterSeconds. Nothing glows flat on the floor (checked).
- HUD: the Q, E, 1, 2, 3 cards switch to UPLINK, OVERCLOCK, FORK BOMB, GLITCH CUT, METEOR BYTE with their class-icon art
  (`ability-uplink`, `ability-overclock`, `ability-hardlight-combo`, `ability-fork-bomb`, `ability-glitch-cut`,
  `ability-meteor-byte`; `tools/export_class_icons.mjs`); the 4 card stays Kernel Panic's.
- Audio: existing bank keys only, each with a shipped fallback clip (`CyberKit2MeleeSounds.Keys`).

**Preview.** In the Developer Sandbox as a Cyber Lord: DEV panel, Previews, Galleries, "Preview Cyber Kit v2 melee"
(`gallery-cyber-kit2-melee`). A 17 s loop on your own pilot from where you stand, along your facing (the nearest pilot ahead,
such as the dummy, is the target): the five-step string, an Uplink zip (onto the first surface ahead, else 10 m out), an Uplink yank on the dummy, the
Overclock and a fast string, the Fork Bomb with a string and a burst, a Glitch Cut, a Meteor Byte 10 m ahead. The preview
writes the record as the proposed host would and moves only the drawn bodies (yours along the zip, dash and arc; the yanked
dummy); the authority, damage and movement are untouched. Toggle it off to return to v1.

**Contract additions and corrections for Astra (the record's names are hers to choose: only `CyberKit2MeleeFeed.Read`
changes).**
- Uplink also needs `LatchAt` (the cable's arrival; the zip and the yank start there), and an outcome 0 while the cable is
  still flying. Positions: the caster's points (UplinkFrom, CutFrom, CutTo, MeteorFrom) are Rigidbody centres like v1's;
  the clones' positions, burst points and the meteor's target are floor points; the anchor is the hit point.
- Per slot a cut time (`CutAt`) when an interrupt ends a cast, and the combo step for the LMB window (-1 none).
- Event Details for the new string steps: `cyber-spin`, `cyber-rise` (the jab, cross and palm keep `cyber-jab`,
  `cyber-cross`, `cyber-palm`). The `overclock` arc event's From should be the struck pilot's point it hops from.
- The body paths the effects assume (`CyberKit2Melee.ZipBody`, `YankBody`, `CutBody`, `MeteorBody`): the poses always read
  the replicated body; if the host moves bodies differently, report it and the helpers follow.

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
3.4 s, cooldown 20 and mana 60. The owner increased opener reach to 12 m and sweep radius to 1.2 m on 2026-10-12.

The hits land on the step starts (`HitAt`), so every take's impact is pinned there. The kick and the hammer wind up in
the last .1 s and .08 s of the previous step, then strike as the host teleports the body in. Step 4's three hits arrive
together; the fork-bomb clones stagger them visually, and their sounds play on the clones' beats.

The pilot frame's `kernelReady` gives the local pilot's HUD card its cooldown.

### The rest of the kit: Astra's `Actor.Cyber`

The source is `Actor.Cyber` (`NativeCyberKit`, `Core/NativeCyberRules.cs`, written by `StepCyberInput` /
`StepCyberWorld` in `Runtime/NativeCyberLord.cs`). The kit keeps one window, for the current or last cast (`Kind`, `At`,
`Release`, `Until`), plus a record of its own for Q, E, 1, 2 and 3. Points are Rigidbody centres (the feet .865 m below),
except `WallPoint`, which is the wall's bottom centre. The pilot is free again at `Until`; the rules have no recovery.

| View field | Source | Meaning |
|---|---|---|
| `ComboStep`, `ComboAt`, `ComboUntil`, `ComboHit` | `Combo`, `At`, `Until` (cut, below), `Release` while `Kind` is `cyber-jab` / `-cross` / `-palm` | the swing; its hit resolves at `Release` (.1 / .1 / .18 s of .28 / .30 / .45 s, over the attack rate) |
| `At`, `Release`, `Until` [Q] | `DashAt`, `DashAt`, `DashUntil` | the .2 s dash (windup 0) |
| `DashFrom`, `DashTo`, `TrailUntil` | the same names, as feet | the path and the 1.2 s trail (it ticks on along the whole path, even after an interrupt) |
| `At`, `Release`, `Until` [E] | `WallAt`, `WallAt`, `WallAt` + .2 | the cast |
| `WallAt`, `WallYaw`, `WallBorn`, `WallUntil` | `WallPoint`, `WallYaw`, `WallAt`, `WallUntil` | the 5 x 2.5 m wall for 4 s |
| `At`, `Release`, `Until` [1] | `PacketAt`, `PacketRelease`, `PacketUntil` (retained across later casts) | the shards fly at `Release` (.08 s) |
| `At`, `Release`, `Until` [2] | `LinkAt`, `LinkAt` + .2, `LinkUntil` | the reach over the cast, held through the drain |
| `LinkTarget`, `LinkAt`, `LinkUntil` | the same names | the tether; an early end (lost, or interrupted) snaps |
| `At`, `Release`, `Until` [3] | `CrashAt`, `Release` (`CrashAt` + .5), `CrashUntil` | the windup, the slam, the end at .8 s |
| `CrashAt`, `CrashBorn`, `CrashUntil` | `CrashPoint` as feet, the slam, the slam + 1 s | the field drawn for the 1 s stun (the rules hit once; there is no lasting field) |
| `Ready` [Q, E, 1, 2, 3] | `QReady`, `EReady`, `PacketReady`, `LinkReady`, `CrashReady` | the HUD cards' cooldowns |

`RecoverUntil` is `Until` plus a short presentation settle (.18 to .4 s) that a newer swing, cast or guard takes over
at once. It is not a rules window.

Interrupts: block, roll, incapacity or Kernel Panic clips an active cast's `Until` once and records
`CutAt` (reset to -1 on the next cast). Repeated interruption ticks leave that time unchanged.
`DashUntil`, `LinkUntil` and `CrashUntil` retain their actual early end. Packet Burst keeps its own
`PacketAt` / `PacketRelease` / `PacketUntil` / `PacketCutAt` across later casts. The feed uses the
replicated cut first; the old actor-time inference remains only as a compatibility fallback.
A crash cut before its slam draws no field, including after another cast replaces the current window.

An empty kit (a new pilot, or the host's reset after death or a lost DEV identity: all zeros) draws nothing. The actor's
attack window alone (Kernel Panic sets `AttackAt` too) is never read as a swing.

Events are read with their `Detail` (`CyberLordFeed.Details`):
- the cast kinds on Hit / AbilityHit (`cyber-jab`, `cyber-cross`, `cyber-palm`, `data-dash`, `firewall` for the burn,
  `packet-burst`, `siphon-link` for each drain, `system-crash`), and Absorb `firewall` for an absorbed shot;
- `kernel-panic-1` to `-5`, and `kernel-panic-wave` on the others caught by the shockwave.

Siphon heals carry `Detail="siphon-link"`. The feed accepts that directly and retains same-tick drain
pairing only for older detail-less events. AbilityCast already emits `packet-burst`; presentation reads its persistent kit record.

`Actor.ViewCopy` deep-copies the kit and the Kernel. `CyberLordFixture` writes both the way her host does: the kit as
`CyberBegin` / `StepCyberInput` do, an interrupt as `StepCyberWorld` does, and `Actor.Kernel` as `ConnectKernel`
resolves it on an open floor. The checks pin the fixture to her `NativeCyberRules.Predict` (windup, length, cost,
cooldown and points per cast, the combo at attack rate 1 and 1.25). It is for the checks and the render harness; the
game never calls it.

## What it looks like

- **Body** (`Core/NativeCyberLordLook.cs`, queued on the pilot's joints in `Runtime/NativeCyberLordPresentation.cs`).
  It is layered over any body skin; the body builder is untouched. The look depends on the pilot's Cyber Lord skin (owner
  2026-10-13: "Change Cyber lord weapon to instead just show an aura around the hands (Like a glitchy / cyber aura)", "We
  can use this current model as a prototype"; see "Skins" below). Both skins have:
  - glowing seams with a pulse running out from the chest;
  - a floating broken-hexagon crest with a magenta diamond behind the head;
  - a data shimmer of blinking motes.
  The **Standard** skin (the default) has no gauntlets: the pilot's own hands and lower forearms wear a glitch aura
  (`Core/NativeCyberLordAura.cs`). The aura ignites over .55 s when the pilot is first seen alive. Its hands flare on each
  combo hit and light up as the mana flows in; first person keeps a lighter aura on the view model's hands.
  The **Prototype Series** skin keeps the hardlight forearm gauntlets and fist shells, unchanged: they form from light edge
  by edge (.55 s), flare on each combo hit and light up with the mana.
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

### Skins: the aura (Standard) and the gauntlets (Prototype Series)

The pick is the `cyberlord` entry of the cosmetic skin map (`Core/NativeWeaponSkins.cs`), after the weapons and before
the body: `cyberlord:prototype` for the gauntlets. Standard is never written. A missing, unknown or malformed value reads
as Standard, the aura; that covers older clients, old saves and bots. An older client drops the entry and draws its own
gauntlets. The Locker's Skins tab lists the Cyber Lord's two finishes like a weapon's, behind the class's DEV gate, and
hovering one tries it on the stage. The entry has no model ids (`NativeWeaponSkins.Modelled` is false).

The aura (`NativeCyberLordAura`) is glowing voxel primitives on the hand and elbow joints, in the look's palette. It hugs
the fist and lower forearm a few millimetres off the skin, so it reads as an aura on the hand, not a weapon or a box:
- shards: voxel and pixel fragments that snap in and out on hashed cycles and drift outward, some floating further out;
- the RGB split: a magenta and a cyan copy of some shards a few millimetres apart, re-offset every glitch step (24 Hz);
- scanlines: short bands crawling down the forearm onto the back of the hand and the knuckles;
- data strands: dashed pixel columns rising straight up in the world off the fists (`CyberPiece.Rise`);
- glitch jumps: in about half of each .9 s slot, a hand's whole aura jumps 1.2 to 3.2 cm for one or two steps;
- the haze: a soft cyan glow round each fist and forearm, a flickering hot core at the knuckles, and a magenta and a cyan
  glow either side for the split. The match and first person draw it in the effects pass (`NativeCyberLordAura.Halo`); the
  entrance and the portraits draw their own soft glows.

It reacts to the gauntlets' signals. It ignites on the form time (respawn, first seen alive, the entrance's beat): the
shards rush in, flash and settle, and one scanline sweeps the arm. The striking hand's flare brightens and swells it and
bursts shards off the knuckles. The mana charge lights both hands and adds strands. A guard packs the shimmer denser and
faster. The Data Dash's dither dims and thins it. The Data Dash and sprint streaks still run from the hands.

Ranged mode: the aura condenses instead of fists opening into emitters. The fist's shards pull in to a tight emitter point
and a turning hex glyph ahead of each palm, at the look's `EmitterAt`; the casts' own anchors are unchanged. The right
hand is the main emitter (a hex and three prongs), the left the support coil (two counter-turning hexes). The scanlines
run faster into them and the strands die down. The key-5 switch keeps its glitch flash, shards and split.

The pilot body skins are untouched under it. The aura is seeded per pilot and a pure function of the presentation clock,
so host, guests, spectators and the killcam draw the same frame; no allocation per frame. Tiers:
- Reduced effects: slower, every piece eased in and out; no snap, blink, split or jump, a steady haze.
- The low graphics tier: fewer shards, one band, short strands, no split, the fist glow only.

The Prototype Series draws exactly the shared head's pieces (pinned digest in `Tests/CyberLordAuraChecks.cs`). One
existing quirk is kept because of that: its ranged emitters are built before `EmitterAt` is initialised, so they centre on
the wrist joint rather than 15.5 cm ahead of it. Moving `EmitterAt` above `emitters` in `NativeCyberLordLook` fixes it if
wanted. That would change the Prototype's ranged pieces, and the pin would need a new value.

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
- **Sprint: Mvm_ChargeBoost** ahead and on the forward diagonals (the authority sprints only within 53 degrees of the facing):
  the Boost's stride with the arms swept back, so the sprint reads as a different body line from the run, not the run sped
  up (owner, 2026-10-13).
  - `CyberLordRun.Swept` holds the fists past the hips, edges up and trailing (`CyberLordRunRules.Swept*`: chest-space
    (-+.21, -.47, -.34) m, the take's own swing kept at .2, a 5.5 cm swing at the stride). It leans the spine .35 and the
    chest .2 rad further ahead and lifts the head back .4 rad onto the aim.
  - The lean rides the takes' eased `Sprinting` ramp and the fists their own .3 s ease (`SweptBlend`), so they swing back
    and forward over a toggle instead of snapping.
  - While sprinting each fist trails a hardlight streak (`NativeCyberLordFx.Sprint`, growing and fading on the same eased
    ramp of the replicated flag; thinner and dimmer on the reduced tier, the streak alone on the low tier, none in the
    pilot's own first person).
  - First person is unchanged (the sprint row is third-person only).
- Not chosen: the plain Boost (the run's pump driven harder; the owner read it as the run sped up). PowerDash and
  PWChargeDash are the same lines with the arms lower or the torso bladed. The Jog is too slow for any replicated speed. The
  Explosive Unarmed run and sprint were an upright jog with a bladed torso turned 50 degrees off the aim.

**How it plays** (`Core/CyberLordRun.cs`, `CyberLordRunCarry`, the base row of `CyberLordMotion`):
- The legs stay the gait's planted procedural feet. Their stride is matched to the replicated speed
  (`MeleeGait.CycleLength`), as every other carry's is.
- The two takes bracketing the travel direction are sampled at the gait's stride phase from each take's left-foot strike
  (measured on its root-motion twin) and blended. The sprint takes blend over them on the replicated `Sprinting` flag's eased
  .15 s ramp (`NativeSprintRules`).
- Each keeps part of its per-stride swing about its cycle mean (the voxel legs step about 1.7 times the captured cadence):
  the torso .7 (sprint .8), the fists .68 and .72 (the sprint's swept fists keep .2 of the take's swing).
- Each take's mean chest turn, relative to the forward run's, is folded out of the spine, so the chest stays on the aim in
  every direction. The pack's diagonals twist the chest up to 34 degrees off it over hips facing ahead, the ChargeBoost
  sprint carries it bladed 35 degrees even ahead, and the melee carry's hip-heading fold would have kept those twists.
- A reach seat (NativeRangedLocomotion's rule) lowers the hips just enough for each planted ankle to reach its gait target.
  The fighter route's gait let a planted ankle skate up to .7 m/s on run diagonals, and the old Cyber Lord run up to
  .96 m/s. Now the contact foot stays under .03 cm/s at the run and sprint speeds in every direction.
- The existing sprint effect, the hardlight look and the first-person view are unchanged (the fist streaks are added).

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

## Ranged mode (key 5)

Owner (2026-10-13): "Give cyber lord a 2nd set of abilities for a "Ranged" mode. So, new 1-4 if they switch to ranged :)[We can
set this to the 5 hotkey to switch)". Design/presentation: `claude/cyberlord-ranged`. Authority implementation:
`codex/cyber-ranged-simulation` (2026-10-13), based on shared `762cffd5`; merge/build status is in DEV_LOG.

Implementation agreement: keep CyberRangedSpec's IDs, numbers, windows and authored presentation. The wire record is
`NativeCyberKit.Ranged` (`NativeCyberRangedState`), with the contract's fields and Unity-serializable `V3` vectors;
`CyberLordFeed.RangedMode` maps it to the existing view, reading Actor.Cyber once. A present authority record wins over the
old sandbox preview. Actor.SourceRanged/SourceModeAt are also written, and key 5 is an edge-only SirenAbility value.
Standard damage/resource/ability modifiers apply; Root keeps its 75-damage cap (37.5 when blocked), uses Kernel's
resource-only mana multiplier and unscaled shared ultimate clock, including Kernel's whiff refund. The field must find
walkable ground; a throw into a void creates no field. Its slow refreshes per authority tick and clears after leaving.
Presentation integration gates released bolt/lance/blink/beam effects after an interrupt and replaces the obsolete
sandbox preview button with the live key-5 instruction; no art, sound, pose, icon, kit number or effect styling changes. Native collisions, real JsonUtility/guest application and input locks
are covered by a new build gate, but require the Unity builder; managed checks alone do not establish those results.

### The kit (proposed numbers, `Core/CyberLordRanged.cs` `CyberRangedSpec`)

Key 5 reconfigures the hardlight fists into emitters and back. In ranged mode LMB and 1-4 change. RMB block, SHIFT roll,
Q Data Dash and E Firewall stay as they are in both modes, so Q and E need no change.

| Key | Ability | Role | Damage | Cooldown | Mana | Timing | Reach | Counterplay |
|---|---|---|---|---|---|---|---|---|
| 5 | Reconfigure | mode switch | none | 1 s from its start | 0 | .3 s; the pilot moves but cannot attack, cast, block or dash | none | the glitch flash, the fists turning to emitters, the HUD's 5 card |
| LMB | Bit Bolt | sustained poke | 9 per bolt; +3 mana per pilot hit | .26 s between bolts (scaled by the attack rate) | 0 | .05 s windup; the hands alternate | a 64 m/s projectile, radius .12 m, 40 m | visible travel (.22 s at 14 m): strafe; block |
| 1 | Ping Lance | long poke | 22 per pilot, pierces 2 | 5 s | 15 | .18 s charge, .32 s cast | an instant line, 45 m, radius .15 m | the charge and a dashed telegraph down the aim: step off it; Firewall absorbs; block |
| 2 | Lag Field | zone | 7 per .5 s tick (56 over 4 s) and 35% slow | 13 s | 30 | .16 s windup; the cube's flight distance/16 s (.35-1.1 s); forms over .35 s, lasts 4 s | the aimed floor point within 22 m; radius 4.5 m | edge posts rise before it lands; walk out; it does not block shots |
| 3 | Proxy Swap | escape | the mine: 18 and a 3 m/s shove | 11 s | 25 | .04 s windup, .12 s cast; the mine bursts .8 s after the blink | a 9 m blink toward the move input (straight back without one), path-constrained like Data Dash; the mine's radius 3 m | the proxy at the start and the mine's quickening fuse with its edge posts: step out of 3 m |
| 4 | Root Access | finisher | 45 + 30% of the target's missing health, capped at 75; a block halves it | Kernel Panic's 20 s clock (one ultimate clock for both 4s) | Kernel Panic's | a 1 s rooted charge (the aim still turns), then the beam; 1.3 s cast | an instant beam, 45 m, radius .4 m; stops on the first pilot, a wall or a Firewall | 1 s of charge with a danger line from .2 s and a warning beep everyone hears: break the line, block, Firewall, or stun the caster (a cut beam never fires) |

- The switch is refused, never queued, while any cast, combo swing, Data Dash, Siphon Link, Kernel Panic or Root charge is
  live, and while the pilot is stunned, rolling, juggled or dead.
- Cooldowns carry across modes: each ability keeps its own clock. The mana pool is shared, so switching never refunds or
  resets anything.
- Movement speed is the same in both modes. A respawn starts in melee mode.

### How it looks

Presentation only. It reads one record per pilot through `CyberLordFeed` (`CyberRangedRecord`; see the contract), so the
host, guests, spectators and the killcam draw the same frame.
- **The switch** (`Core/NativeCyberLordLook.cs` `Mode`, `Core/NativeCyberLordRangedFx.cs`): both forearms fold in across
  the chest with a glitch jolt at the middle. A white-cyan flash fires at each fist, shards scatter out (or snap in), and
  the body splits RGB for others. With the Standard skin the aura condenses into an emitter point and a turning hex glyph
  ahead of each palm (see "Skins"). With the Prototype Series the fist shells dim and shrink while the emitters build in
  piece by piece: a hex ring, a core and prongs on each hand, a three-ring barrel on the right forearm and a coil on the
  left. The mode eases over the .3 s from the replicated switch time.
- **The ranged stance and run** (`Core/CyberLordRangedMotion.cs` `CyberRangedPose`; the cyber mage since 2026-10-15, see
  "Ranged presentation (Kit v2 branch)" below): a pitcher's set, the next throw's hand loaded beside the head with a fireball
  over it, the other hand forward on the aim, the chest coiled toward the loaded side and the head on the aim. It rides over
  the run carry's planted legs with a small bob at the stride. A melee take (the guard, Q, E) owns the frame by its weight;
  the sprint becomes the glide. The idle stance yields to it.
- **The casts**, each on its replicated window:
  - Bit Bolt: thrown overhand from the loaded hand as a Plasma Byte fireball (see below), the other hand loading behind it;
    a cast flash and a fan of flame at the hand. It lands in a plasma burst with hex shards and a scorch flicker.
  - Ping Lance: the cannon arm cocks back over the charge, glowing, with the dashed telegraph down the aim; then it thrusts,
    and the lance (a white core in a cyan sheath with pings running along it) leaves a hex reticle on a struck pilot.
  - Lag Field: an underhand lob arcing about the shoulder. A tumbling data cube flies with upright posts rising round the
    landing point. Then a hexagonal dome of rings over a ring of stuttering posts, lagging packets inside, a pulse per
    tick, the pilots inside glitching, and a collapse into pixels.
  - Proxy Swap: the arms tuck through the blink, and a pixel streak runs along the path. The RGB-split proxy left at the
    start collapses into a spinning hardlight mine that blinks faster to the burst; posts show the burst's edge.
  - Root Access: both emitters close in with a growing core, converging streaks and spinning hex rings, plus a magenta
    danger line down the aim from .2 s. The beam is a white core in a cyan sheath with magenta edges and code blocks
    spiralling along it. Both arms are thrown back and up.
  - The beams and the telegraphs are true-perspective ribbons facing the eye, so the target looking down the line, and the
    shooter looking along it, still see them.
- **First person**: the emitters sit where the view's stance holds the fists, drawn in toward the centre. The switch folds
  them together in view, and the casts flash at the view's emitters. The pilot's own beams start on the view layer and run on
  in the world. The pilot never sees its own split, proxy or reticle.
- **HUD** (`Core/NativeCyberLordHud.cs`, `Runtime/NativeCyberLordScreen.cs`): Q and E stay. At the switch's middle the 1-4
  cards swap to Ping Lance, Lag Field, Proxy Swap and Root Access (CHARGE through its charge, ACTIVE through the field's
  life and the mine's fuse). A key-5 card is always last: "5 · MELEE" or "5 · RANGED", showing SWITCHING, then its
  cooldown, then READY.
- **Icons**: seven art icons in the class icon style (`tools/export_class_icons.mjs`): `ability-bit-bolt`,
  `ability-ping-lance`, `ability-lag-field`, `ability-proxy-swap`, `ability-root-access`, and the mode card's
  `ability-cyber-ranged` and `ability-cyber-melee`.
- **Audio** (`Core/NativeCyberLordSounds.cs`): existing bank keys only, with shipped fallbacks:
  - the switch (`dev.leet.glitch`, `chip.delayed.charge`);
  - each bolt (`magic.fire.shot` and a faint `dev.leet.glitch`; its landings `magic.fire.hit`);
  - the lance's charge and shot (`chip.delayed.charge`, `magic.flame-lance`);
  - the lob, the landing and the collapse (`gun.frag.throw`, `field.time`);
  - the blink (`chip.teleport`), and the mine's arm, beep and burst (`chip.mine.arm`, `chip.bomb.fuse-beep`, `blast.mine`);
  - Root Access's charge (`chip.delayed.charge`), its warning beep at the telegraph (`chip.bomb.fuse-beep`) and the beam
    (`blast.shock`, `magic.lightning.shot`, `blast.static`);
  - the landings by their ranged Detail.
  A cut cast is silent past its cut.
- **Tiers**: reduced effects never flicker or strobe, soften the flashes (the beam at .65) and thin the pieces; the low tier
  drops the secondary lines and glows. Nothing lies flat within 12 cm of the floor: the field's rings float from .5 m, and
  the posts stand up.
- **The sprint**: the melee mode keeps the hardlight dash sprint (see "Run and sprint"); the ranged mode glides (see
  below).

Animation impacts: none on the rules. The poses move only the drawn arms, chest (its coil and pitch) and (through the blink)
a 6 cm dip of the drawn hips. Hit timing, hitboxes, damage, cooldowns, movement and the network frame are untouched; the
presentation never drives a window.

### The contract

The presentation reads it in one place, `CyberLordFeed.RangedMode` (`Core/NativeCyberLord.cs`); Astra's real names change
only that mapping.
- **The mode**: the Siren's precedent. `Runtime/NativeSiren.cs:58` toggles `s.Ranged` and stamps `s.ModeAt` on Q, and
  `Runtime/NativeArena.cs:276` copies them to `Actor.SourceRanged` / `Actor.SourceModeAt`, which are already on the wire.
  Without a ranged record the feed reads those two for the Cyber Lord too, taking the switch's cooldown as ModeAt + 1 s.
- **Key 5**: `InputFrame.SirenAbility = 5`. `Runtime/ConversionGame.cs:126` maps Alpha1-4 today; `InputFrame.Valid` bounds
  it to 4, so it needs to admit 5 for the Cyber Lord (or use a new flag; Astra's call).
- **The casts**: one `CyberRangedRecord` per pilot (`Core/CyberLordRanged.cs`, `[Serializable]`, with `ViewCopy`).
  Embedding it in `NativeCyberKit` (`Actor.Cyber`) replicates it with the kit's deep copy and JSON.
  - `Ranged`, `ModeAt`, `ModeReady` (the mode, if kept here instead of the Siren fields).
  - Per slot (Bolt, Lance, Field, Swap, Root): `At`, `Release`, `Until` and `Ready`. An interrupt ends `Until` at the cut
    (a cut before `Release` sets `Release` there too: nothing fired).
  - `BoltHand` (0 right, 1 left, alternating), `LanceFrom` / `LanceTo`, `FieldFrom` / `FieldPoint`, `FieldLand` /
    `FieldUntil`, `SwapFrom` / `SwapTo`, `MineAt` (the burst time), `RootFrom` / `RootTo`. Points are world metres from the
    emitter height (the field and the swap at the feet).
  - `NativeNetworkFrame.Valid` needs to bound them: finite, five per array.
- **Bit Bolts** are bullets with `Spec = "cyberlord"` and `Detail = "bit-bolt"`. The default shot drawer skips exactly those
  (`CyberLordFeed.ShotSlot`); the Data Packets (Detail null) stay its own.
- **Events**: Hit / AbilityHit / Absorb with Details `bit-bolt`, `ping-lance`, `lag-field`, `proxy-swap`, `root-access`
  (already in `CyberLordFeed.Details`, mapped to `CyberHit.Bolt..Root`), and `EventKind.Impact` with the Detail where a bolt,
  the lance or the beam ends on the world.
- **Prediction**: the switch (mode, its time and cooldown) and the casts' mana and cooldowns in `NativeCyberRules.Predict`,
  as the melee kit's are.
- **Bots** (`Runtime/NativeBots.cs`, `BotCyber`): switch to ranged beyond about 8 m and back inside 5 m, with hysteresis.
  In ranged: bolts while visible, the lance at 8-40 m, the field on a target 6-20 m away, the swap when an enemy is within
  3 m and health is under 50%, and Root Access on a visible, unblocking target at 10-40 m under 60% health.

### Astra prompt: ranged mode

Please build the DEV Cyber Lord's ranged mode: rules, input, networking, prediction, bots and tests. The presentation is done
and waits on the replicated data. The design, numbers and contract are in docs/CYBER-LORD.md "Ranged mode", and the numbers
are `CyberRangedSpec` (`Core/CyberLordRanged.cs`).
- **Key 5 switches** melee ⇄ ranged (`InputFrame.SirenAbility = 5`, mapped beside Alpha1-4 in `Runtime/ConversionGame.cs:126`;
  extend `InputFrame.Valid` for it, or use a new flag).
  - Follow the Siren's toggle (`Runtime/NativeSiren.cs:58`): flip the mode, stamp the switch time.
  - Rules: a .3 s switch (moving allowed; attacks, casts, block and dash refused); the next switch 1 s after the start.
    Refused, never queued, while any cast, combo swing, Data Dash, Siphon Link, Kernel Panic or Root charge is live, or while
    stunned, rolling, juggled or dead. A respawn starts in melee.
  - Cooldowns and mana carry across modes.
- **Ranged LMB, Bit Bolt**: alternate-hand bullets, `Spec "cyberlord"`, `Detail "bit-bolt"`. Speed 64 m/s, radius .12, range
  40 m, 9 damage, one every .26 s scaled by the attack rate, no mana cost, +3 mana per pilot hit. Emit Hit with the Detail
  on a pilot, and Impact with the Detail on the world.
- **Ranged 1, Ping Lance**: 15 mana, 5 s. A .18 s windup, then an instant 45 m line (radius .15) from the emitter along the
  aim. 22 per pilot, pierces 2, stopped by the world; a Firewall absorbs it (Absorb). Record LanceFrom / LanceTo.
- **Ranged 2, Lag Field**: 30 mana, 13 s. A .16 s windup, then a cube lobbed to the aimed floor point (clamped to 22 m),
  landing after `FieldFlight(distance)`. A 4.5 m field forms over .35 s and lasts 4 s: 7 per .5 s tick to enemies inside
  and a 35% slow while inside. Record FieldFrom / FieldPoint / FieldLand / FieldUntil.
- **Ranged 3, Proxy Swap**: 25 mana, 11 s. A .04 s windup, then a blink up to 9 m toward the move input (straight back
  without one), path-constrained like Data Dash. A mine left at the start bursts .8 s later: radius 3, 18 damage, a 3 m/s
  shove outward. Record SwapFrom / SwapTo / MineAt (the burst time).
- **Ranged 4, Root Access**: Kernel Panic's mana, sharing its 20 s clock (one ultimate clock for both 4s). A 1 s rooted
  charge (the aim still turns), then an instant 45 m beam (radius .4) on the first pilot, wall or Firewall. Damage 45 + 30%
  of the target's missing health, capped at 75; a block halves it. A stun or interrupt cuts it before the release and
  nothing fires. Record RootFrom / RootTo.
- **Q, E, RMB and SHIFT** are unchanged in both modes. Kernel Panic is the melee 4 only.
- **Replicate**:
  - the mode on `Actor.SourceRanged` / `Actor.SourceModeAt` as the Siren does (`Runtime/NativeArena.cs:276`), or in the
    record;
  - each cast as a `CyberRangedRecord` (embed it in `NativeCyberKit`, or use your own names and change only
    `CyberLordFeed.RangedMode` in `Core/NativeCyberLord.cs`);
  - an interrupt ends the slot's `Until` at the cut (and `Release` too when it is before the release).
  Bound the new fields in `NativeNetworkFrame.Valid`. `a.Cyber` must stay read once in `NativeCyberLord.cs`
  (`Tests/CyberLordChecks.cs`).
- **Predict** the switch and the casts' mana and cooldowns in `NativeCyberRules.Predict`, as the melee kit's are.
- **Bots** (`BotCyber`): switch to ranged beyond about 8 m and back inside 5 m (with hysteresis, respecting the cooldown).
  Use bolts while visible, the lance at 8-40 m, the field on a target 6-20 m away, the swap when an enemy is within 3 m and
  health is under 50%, and Root Access on a visible, unblocking target at 10-40 m under 60% health.
- **Tests**: the switch's refusals and cooldown, no queueing, each ability's numbers and Details, the interrupt cuts,
  prediction parity, validation bounds, the JSON round trip, and bot use. `Tests/CyberLordRangedChecks.cs` drives the
  presentation through the preview record (`CyberRangedPreview`); once your fields exist, point the feed at them and keep
  those checks green.

## Kernel Panic

- **Opener.** The .25 s windup cocks the fist and drives the lunge punch, which lands at the contact. There the host
  moves the body up to 12 m at once, and an RGB-split blink trail shows the path for .3 s.
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
- **Locker**: the Q, E and 1-4 rows show the HUD cards' icons, and their costs read in mana (her kit spends mana). The
  Skins tab offers its Standard (the aura) and Prototype Series (the gauntlets) finishes.
- **Locker entrance**: "DEV · Hardlight Boot" (`dev-cyberlord`), with a DEV badge, a diamond icon and the text "Built from
  light, then a hardlight palm strike." It is gated like every `dev-` cosmetic (a guest or a scoped grant cannot pick it).
- **Entrance** (`Core/NativeCyberLordIntro.cs`, directed). It plays on the standard 4.8 s with the name card at 3.2 s, on
  the shipped pulse-wave bed (150 bpm). `dev-*` styles get no music key by contract.
  1. A scan of light builds the pilot from the shins up.
  2. The aura ignites (Standard) or the gauntlets form (Prototype Series) on a close shot, at the same beat.
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
- The ranged mode's hold, switch and casts move only the drawn arms and chest, and the blink a 6 cm dip of the drawn hips.
- The martial string (2026-10-15) moves only the drawn rig on the replicated windows: the drawn body shifts up to 8 cm off
  the capsule (step-ins, the heel kick's pivot, the palm's stance) and returns, rises up to 10 cm in the launcher's hop and
  sinks up to 10 cm to keep a planted foot in reach; the kicks' legs, the hop and the stomp land by each window's end.
- The capsule, movement speed, jump height, hit timing, cooldowns and the network frame are untouched.

## What the rules side still needs to provide (Astra)

The follow-up on base `a44046f` adds exact cut timestamps, persistent Packet Burst records, tagged Siphon heals,
and six real Locker descriptions. Account source allows `dev-cyberlord` only for DEV accounts; deployment is owner-gated.
Replica bullets now retain Shot.Draw and Shot.Detail. System Crash remains one hit, with a presentation field for its stun.
Actual Unity/Windows and two-client play remain to be checked.

Still pending or limited:
- System Crash remains a one-hit ability; its drawn field follows the stun.
- The air jump (the double jump the owner asked for): the prompt is under "Double jump" above.
- Chained Data Dashes: the kit keeps one trail. A new dash resets `CyberTrailTick` and overwrites `DashFrom` / `DashTo` /
  `TrailUntil`, so the earlier trail stops damaging when the next dash starts, though it still draws its fade. With no
  cooldown (PR #159) a spammed chain leaves up to eight drawn trails but only the newest damages. If each trail should
  keep ticking for its own 1.2 s, the kit needs a small trail history (about eight per pilot). Otherwise the shorter damage
  is the rule, and the older drawn trails are cosmetic.
- The Sheldon Idle save: the prompt is under "Sheldon Idle" above.
- The ranged mode (key 5 and its LMB and 1-4): the prompt is under "Ranged mode" above.

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
  travel, the chest stays on the aim, the run's fists pump and the sprint's sweep back past the hips, the ramp has no pop;
- the boost keyed only on the replicated air jump, on the host's and a delayed guest's timeline, torso and legs on the same
  frame, the Dev Sandbox preview, the daggers' flip unchanged;
- its effect: once per air jump, its lifetime, the first-person and reduced versions, never flat on the floor, the audio keys;
- chained Data Dashes: two .7 s apart (both draw, the first finishes its fade), and six .2 s apart with no cooldown (all
  held, each finishing its own fade, the oldest first, the older ones on the low tier; past the cap the oldest retires);
- the flip's three paths skip the Cyber Lord;
- zero per-frame allocation, a Data Dash spam's ring included.

`Tests/CyberLordAuraChecks.cs` covers the skins:
- the skin map's `cyberlord` entry: round trip, canonical order, missing, unknown, malformed and older maps reading as the
  aura, the longest map (298 of 320 characters), no model id;
- the Prototype Series against the shared head's pinned digest (`Pieces` and `PiecesIn`, any seed or guard);
- the Standard: no gauntlet, fist shell or emitter piece, the Prototype's seams, crest and shimmer kept, every aura piece
  small and on the hands or lower forearms, shards, split pairs, bands and rising strands present;
- the signals: the ignition, both hands, the glitch jumps, the striking hand's flare and burst, the mana, the guard, the
  dash's dither, the ranged emitters on `EmitterAt` (right main, left coil) with the shards condensing and the glyph turning;
- determinism in any order, the tiers (Reduced never strobes, splits or jumps; low and first person lighter), the haze,
  zero per-frame allocation, and the Runtime wiring.

`Tests/CyberLordRangedChecks.cs` covers the ranged mode on the preview record:
- the proposed numbers, the icons, the ranged Details, the shot slot (the Data Packets stay the default drawer's), the mode
  on the Siren's fields when no record exists;
- the switch's refusals and cooldown, the interrupt cuts, the eased mode weight and the bar swapping at its middle;
- the pose: every ranged state on its window, the feet planted, the hold on the aim and over the run, no spikes, first
  person; melee bit-identical with a source holding no record; a guest on a delayed copy of the record posing the host's
  frames exactly;
- the effects: each cast drawing on its window, never flat on the floor, the tiers, the switch's split for others only, a
  copied record drawing the same, the sprint streaks easing with the flag;
- the look's emitters (melee piece for piece unchanged), the HUD's two sets and the key-5 card, the sounds, and zero
  per-frame allocation.

`Tests/CyberKit2RangedChecks.cs` (`--cyber-kit2-ranged`) covers the cyber mage and Kit v2's ranged set on the proposed record:
- the contract: only CyberLordFeed maps (and reads the source of) the record, only on Version 2 (v1 untouched otherwise);
  bounds, the JSON round trip and the deep copy; the seven Details and the fireballs' shot kinds; the throws' hands and the
  chain's reset; the storm's drops, the orbital chase (never overshooting, kept in range) and the chain's hop times and
  falloff from CyberKit2Spec's helpers; the preview touching no rule, actor or event;
- the body: every v2 state, finite and continuous (no frame jump or spike), each throw's hand driving forward fastest on its
  replicated release (right, left, both, and v1's Bit Bolt), the legs the gait's while throwing and casting on a diagonal run
  on uneven frames, a rewound clock, the glide, a 6-frame delayed guest copy posing the same frames, first person;
- the effects: each on its window, determinism (a second instance and a copied record draw the identical frame), the chain
  hop by hop, a cut orb drawing nothing, the fireball's size at range, nothing flat on the floor, the tiers;
- the HUD cards' states, the five icons, the sounds (existing keys, shipped fallbacks, once each, a cut cast silent, v1's
  bolt as a fire whoosh), and zero allocation per frame.
`Tests/CyberLordRangedChecks.cs` keeps pinning v1's ranged mode; its stance check now reads the cyber mage's set (one hand
loaded beside the head, the other forward on the aim) instead of both emitters held together.

`Tests/SheldonIdleChecks.cs` covers the Sheldon Idle: the DEV-only listing and the gate on the account load and the host
seat, the fallback for unknown ids, the match never reading the pick, the Cyber Lord stance's body and twitches for 17
classes, each weapon on its own hand with the hand reaching its hold, two-handed support grips, the free fist, the weapon
clear of the podium and riding the twitches (less with reduced effects), the Cyber Lord's default, determinism and no
allocation.

The offline renders come from a scratch WebGL harness: real Core, real `NativeVfxDraw` and `NativeWeaponFxDraw`, and the
shader SHARED blocks. The 2D overlays (the cut-in, banner, HUD, tile and Locker) are canvas mock-ups of the runtime layout.
None of this has run in the Unity Editor or on Windows.

### Shared-head reconciliation (2026-10-13, PR #147)

Data Dash retains `DashCooldown=0`. Kernel Panic retains the PR #158 travelled-distance lunge and full travel window; it interrupts the previous Cyber cast and clears the Siphon tick. Facing remains yaw-only (`V3.Forward`): the capsule sweep and forward step follow a horizontal lunge; camera pitch must not turn it into a vertical dash. Ranged-mode presentation and the single `a.Cyber` read in `CyberLordFeed` are preserved.
