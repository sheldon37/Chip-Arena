# Siren zombies: presentation and the rules hand-off (owner 2026-10-12)

Owner request: "Sirens minions need to look more zombie like (Based on player models) Their animations also look a lot
more simple then what we used to have? We do have the zombie mo-cap ones we could utilize from the pack share. They still
use old model when entering through doors and vents~ Brutes can be big muscle versions, that knockbacks and slam. For now,
we will have them enter from the middle of the map by jumping down and creating a small slam effect on their landing. (I am
planning to revamp this map, so keep this in mind)" and "(Especially there walk animations, its not great)".

Split: Claude built the presentation on `claude/siren-zombies` (models, mocap animation, entrances, slam effects, camera
shake, the jump-down's presentation contract, Dev Sandbox preview, checks). Astra owns the rules: the jump-down spawn,
the knock-back and slam numbers, the map's drop entries, the hit volumes and networking (the prompt at the end). Nothing
in the simulation, the colliders, the attack timing, the damage, the spawns or the network changed. This builds on
docs/SIREN-MINION-VARIANTS.md (kinds, stats, events), which stays the rules spec for the kinds.

## Simulation agreement — 2026-10-12 (Astra, `codex/siren-brute-drops`)

Implemented locally on the merged presentation base `eaab6443`; the earlier presentation-only status and proposal below describe the original hand-off.

- `PveEntry.height` now exists. `NativeHordeDrop.HeightOf` reads it; zero/omitted uses 6.5 m. Ward Zero's `PveContent.json` entry 16, `drop-centre`, carries the landing at (0, 0), facing and height. Move `from`/`exit` and adjust `yaw`/`height` there when revamping the map. No landing coordinates are embedded in simulation code. The export tool preserves native drop entries.
- Brutes rotate through available drop entries exclusively; full queues wait. With no drops they use the existing entry selection. Other kinds retain their existing entrances. Every drop uses `activeAt = spawnAt + Duration(height)` (2.705535 s at 6.5 m), touchdown at `activeAt - .9` (1.805535 s). The body stays at the exit, kinematic and collider-disabled, immune and untargetable until touchdown. During recovery it is physical, hittable and lockable; attacks begin at `activeAt`.
- Touchdown fires once: horizontal 2.4 m disc, body-centre vertical difference strictly below 1.5 m, world line of sight, damage 10→5 linearly, outward impulse 380 plus 2.5 m/s upward before knockback modifiers. Blocking clashes, takes no damage and halves both shove components. Nearby exposed PvE adds receive outward impulse 200 through their existing external-motion channel.
- Smash keeps the 1.6 m disc 1.2 m ahead, .43 s hit, damage and recovery. Impulse is now 420 with 2.5 m/s upward, directed away from the disc centre (facing fallback at the exact centre), without knock-down. Its existing block clash/stagger remains unchanged. The brute row in `NativeHordeKinds` matches the updated content.
- Claude: the shared height accessor is already wired. Authority now emits `Impact` details `minion-drop` and `minion-slam`, target = add id, position = floor slam centre, radius = 2.4 / 1.6. Existing clock-based effects remain; avoid doubling them if consuming these events. No new replicated fields. Entry validation accepts drop index 16 and later indices through 255. The optional .55 × 2.0 capsule/nav resize was **not** applied; no re-sculpt is required for this change.
- Managed drop checks and the existing kind/motion checks pass. `NativeBuildChecks` now calls `ValidateHordeDrops` for authority collider/LOS/combat/replica coverage. That Editor/PhysX gate and Windows/two-client play are unrun here; they remain the acceptance step before release.

## 1. What changed (presentation only)

- **Models** (`Core/NativeZombieModels.cs`): every kind is a zombie built from the pilot's own blocks and proportions
  (the Standard body's cube head, plated chest, pauldrons, cuffs, knee pads, soles), rotted and torn: grey-green skin with
  rot and bruises through a ripped undersuit, armour cracked and half gone (looks differ in which pieces they keep), the jaw
  hanging open, eyes glowing in the kind's colour, the Siren's ink running through the wounds as veins that flicker cell by
  cell (the glow codes, so possession, char and failing power still read). Shambler 3 looks; runner (stripped, thin,
  long-clawed, bone spine), brute (big muscle version: swollen chest bursting its plate, trapezius hump over a small sunken
  head, huge shoulders, forearms and fists), spitter (throat sac), crawler (emaciated) and bloater (swollen belly) 2 each.
  Three crowd tiers as before (Full / Reduced / Impostor under 7200 / 3000 / 600 triangles). Every posed body stays inside
  today's capsule (no hit-volume change; see section 5 for the brute's proposal).
- **Animation** (`Core/NativeZombieClips.cs`, `Core/NativeZombieMotion.cs`, `Core/NativeHordeMotion.cs`): the owner's ZOMBIE
  PRO mocap, retargeted by `tools/export_pack_zombie.mjs` into `Core/NativePackZombie.Generated.cs` (39 tracks, pose data
  only; no FBX, mesh, texture, audio or `.meta` is committed; SHA-256 per track in `tools/pack-zombie-provenance.json`).
  - Walks and runs: the captured upper body, hips and head on the gait's planted feet. The cadence follows the take
    (playback rate `tempo × (v / v0)^0.7`, clamped .55-2.8), each foot's stance and swing phases come from the take's own
    foot contacts, the swing lift, foot turn and knee direction from the capture, so a planted foot never slides at the
    authority's speeds and the stride still reads as the take. Each add picks its takes by its id (seeded variety).
  - Attacks retimed so the take's impact frame shows exactly at `lastAttack + hit`, wind-up held to the telegraph, the
    recovery to `max(cycle, hit + .05)`; the crawl/armless overrides keep their timing. Spit, belly and pounce stay
    procedural (no matching take).
  - Hits: the felt-hit reactions (recoil, shove, hit-stop, flash, ink) as before, with a captured knock-back reel laid over
    a stagger. Gibs, slices, char and burn-away unchanged.
  - Deaths: captured, falling the way the killing blow pushes (back, forward, to its left or right), the head down by
    .95 s, on the floor (no pops: the exporter makes the channels continuous; deaths keep the capture's whole forward fold).
    Crawler and bloater deaths stay procedural (flip, burst).
- **Entrances on the zombie, never the old bank clips** (`Runtime/NativeHordeKindPresentation.cs`): the vent climb (claws
  up the shaft, hands slammed past the rim, haul, stand on the pack's crawl-to-stand), the door and cell (the waking take
  while the authority holds it, then the gait walks it in on planted steps), the dive into a vent (stand-to-crawl). All on
  the kind's own model and the authority's entry timeline.
- **Brute slam upgrade** (`Runtime/NativeHordeZombiePresentation.cs`, `Core/NativeHordeFx.cs`): at the smash's strike, dust
  thrown round the authority's disc, cracks run out of it, a short camera shake for pilots within 9 m and, for the local
  pilot inside the disc (the one the authority shoves), a 3.2° kick of the view up and back. Settings · camera shake gates
  it; Reduced effects halves it (`ConversionGame`, beside the structure shake).
- **The brute's jump-down** (section 2), presented today from map data and the replicated entry; previewed in the Dev
  Sandbox gallery (brute: drop; shambler: vent climb; runner: door walk-in; others: wake).
- **Checks**: `Tests/ZombieMotionChecks.cs` (track provenance, sets, planted ankles on the drawn rig, strike timing, entrances,
  deaths, the drop timeline, map-data reads, shake bounds, no allocation, presentation-only runtime) and the existing
  `Tests/HordeKindChecks.cs` (capsules, tiers, timing).

### Clip mapping per kind

| kind | walk takes | run takes | idles | blow | entrances and more |
|---|---|---|---|---|---|
| `shambler` | chase-1, chase-2, chase-5 | sprint-1, sprint-3 | shamble-1, shamble-2, idle-3 | swipe-r1/r2 (right arm), swipe-l1/l2 (left) | all upright kinds: knock-1..5 reel on a stagger; deaths back-1..3, front-1..3, left-1, right-1..2 by push; wake (door/cell), crawl-stand (vent climb), stand-crawl (dive) |
| `runner` | sprint-1, sprint-3 | sprint-3, sprint-4 | shamble-2, idle-3 | lunge-1, lunge-2 | as above |
| `brute` | walk-4, walk-6 | chase-5, chase-2 | idle-3, shamble-1 | smash-1, smash-2 | jump-start, jump-air, jump-land (the jump-down) |
| `spitter` | chase-1, walk-6 | chase-2, sprint-1 | shamble-1, idle-3 | spit: rears back on the start of knock-1, then procedural (hinged jaw, from the mouth) | as above |
| `crawler` | procedural four-limbed trot | - | - | pounce (procedural) | procedural deaths |
| `bloater` | walk-6, walk-4 | chase-1 | idle-3 | belly (procedural) | procedural swell and burst |
| `hulk` | walk-4, walk-6 (tempo .62) | chase-5 | idle-3, shamble-1 | smash-1, smash-2 (1.1 s wind-up) | roar on the start of knock-3 (hinged jaw); jump-start/air/land on the Hulk drop profile; knock-n only on a stagger (25% of max hp); deaths at 1.8x with a crash |

Sources (pack takes): walk-n `Zombie_Walk_F_n_Loop`, chase-n `Zombie_Chase_n_Loop`, sprint-n `Zombie_HyperChase_n_Loop`,
swipes `Zombie_Atk_Arm_1/2_R/L_SHORT_Loop`, smash-1/2 `Zombie_Atk_Arms_4/4A_SHORT_Loop`, lunge-1/2
`Zombie_HyperAttack_3/1_SHORT_Loop`, knock-n `Zombie_Atk_KnockBack_n`, deaths `Zombie_Death_*`, wake
`MOB_Stand_to_Zombie_Idle_1_v2`, vent `Zombie_Crawl_1_to_Stand` / `Zombie_Stand to_Crawl_1`, jump
`Zombie_HyperChase_5_Jump_Start / Air / End_HA1` (frame ranges in the provenance). Re-export: `node
unity/ChipArenaConversion/tools/export_pack_zombie.mjs <pack>/Assets/Zombie_27` (deterministic; see DEVELOPMENT.md).

## 2. The jump-down: presentation contract (`Core/NativeHordeDrop.cs`)

An add whose replicated entry (`NativeMinionFrame.entry`) is a map entry of kind `"drop"` drops onto that entry's `exit`
point, facing its `yaw`, from `height` m above it (today `NativeHordeDrop.DefaultHeight` 6.5 m: `PveEntry` has no height
field yet). Timeline from the replicated `spawnAt` to `activeAt`:

| phase | length | shown |
|---|---|---|
| warn | whatever is left (.7 s at the proposed duration) | the landing ring closing in on the spot; nothing else |
| leap | .35 s | an ink tear opens over the spot; the brute appears in it, crouches and springs (jump start) |
| fall | `sqrt(2 (height + .35) / 24)` s (.76 s from 6.5 m) | tucked fall (jump air), its shadow on the spot |
| land | at `LandAt = activeAt - .9` | the slam: shockwave ring, cracks, thrown debris, dust ring, ink splash, `siren.slam` + `impact.ground` (existing bank keys), a .55 s camera shake within 12 m |
| recover | .9 s to `activeAt` | the landing take's recovery, then the gait |

Proposed whole entrance: `NativeHordeDrop.Duration(6.5)` = .7 + .35 + .76 + .9 = 2.71 s. A shorter or longer rules
timeline only stretches the warning (the leap and fall are laid back from the landing). Everything comes from the entry
index, `spawnAt` and `activeAt`, so host, guests and the killcam agree with no new frame field. No coordinates are in the
code: a revamped map moves the drop by editing its entries (checked: no Ward Zero entry coordinate appears in the drop's
files).

## 3. What the rules have today (inspected)

- Brute smash (`NativePveHorde.PveAddStrike`): every pilot within the 1.6 m disc 1.2 m ahead of the brute, within 1.8 m
  height, in melee line of sight, takes `damage` 15 with `impulse` 260 along brute-to-pilot; a block clashes and staggers
  the brute .6 s. Hit `.43`, cycle 1.7. There is no other knock-back rule, no charge, and no landing slam.
- Entries: `PveContent.json` `entries` of kind vent / cell / door (from, exit, yaw). No `drop` kind, no height. Brutes
  spawn through the same entries as everyone.
- The add frame already replicates `entry`, `spawnAt`, `activeAt`, `travelling`; the presentation needs nothing more.

## 4. Proposed rules (numbers are starting points; record changes here)

- **Brute knock-back smash.** Keep the disc and timing. Raise the shove from impulse 260 to 420 with a small upward pop
  (about 2.5 m/s), pushed from the disc centre outward (a pilot at the centre straight away from the brute). No knock-down.
  A block keeps the clash. Optionally emit `minion-slam` (VARIANTS §6) so a cancelled smash never shows its shockwave.
- **Brute landing slam.** At `LandAt`: pilots within 2.4 m of the landing point (|dy| < 1.5 m, line of sight from .45 m
  above it) take 10 damage at the centre falling linearly to 5 at the rim, impulse 380 outward plus the same pop; a block
  clashes (no damage, half the shove). Other adds within the radius are shoved (impulse 200, no damage). Optional event
  `minion-drop` (`Position` landing point, `Radius` 2.4) at `LandAt`; the presentation already plays the slam from
  `activeAt` without it.
- **Brute charge.** As VARIANTS §3 (6-12 m, .6 s wind-up, 7 m/s up to 1.6 s, 18 damage, impulse 600).

## 5. Hit volume proposal for the muscle brute

Today the brute uses the shambler's capsule (radius .42, height 1.7), so the muscle model is squeezed inside it: the
chest is .6 wide and the shoulders sit at the capsule's edge. Proposal: brute capsule radius .55, height 2.0 (centre
1.0 above the floor, root height 1.0), nav agent radius to match. In the same change update `NativeHordeKinds.Radius /
Height / Root` for the brute (Tests/HordeKindChecks reads the collider from NativePveHorde.cs and fails until both
match). Claude then re-sculpts the brute about 1.18 times larger to fill it (a presentation follow-up; reach and the
smash disc can stay or grow with it).

## 6. Known limits

- Unity Editor play, Windows builds and multiplayer were not run; the renders are the harness's software renders.
- The jump-down shows only once the rules spawn a brute through a `drop` entry (today only the gallery shows it).
- The drop height is the default until `PveEntry.height` exists; the landing ring is drawn at `SlamRadius` 2.4 m
  whether or not a landing rule exists.
- Spit, belly and pounce, and the crawler's gait, are procedural (the pack has no matching takes).

## 7. The Hulk (owner 2026-10-13, presentation only)

Owner: "Add a Hulk zombie too, similar to brute but much bigger model size, huge knockback, very slow, very high hp. 1/2
max spawned. The jump in too, shaking the ground and a even bigger stage effect when landing. They can also break through
the walls to enter (Stage will eventually holographically recover broken pieces)".

- Kind `hulk` (`NativeHordeKinds.Hulk` 6; content-gated: no rules row yet, the stats are the VARIANTS proposal). Drawn on
  the shared rig at `Scale` 1.8 (the rig root's localScale; the gait runs in rig metres and is scaled back), so the mocap,
  the IK and the checks are the brute's at 1.8 times. Model (`NativeZombieModels.Hulk`, two looks, three tiers: 3626-3740 /
  1838-1940 / 528-540 triangles, plus a 160 / 72 triangle jaw on full / reduced): huge shoulders, hump and forearms, fists .28, thick legs, a small
  head low between the shoulders, cracked hide with hot Siren-ink veins (seam `0xff2e4d`).
- Proposed hit capsule (not applied; colliders unchanged): radius .95, height 3.06, centre and root 1.53 above the floor
  (`HulkRadius`, `WorldHeight`). Every pose (stand, walk, run, smash, roar, landing crouch) keeps trunk and head inside it
  (Tests/HulkSpitterChecks).
- Heavy walk: each footfall within range adds a small camera shake, a dust puff and a fading floor crack (no timing or
  movement change). Smash on the brute's takes with a 1.1 s wind-up: a 2.8 m telegraph disc 2 m ahead (the proposal),
  at LastAttack + hit a big shockwave, radial cracks, heavy debris and a strong shake; a pilot it hits gets a shove burst
  (streaks, ring) on top of the authority's knock-back. Roars on a seeded schedule (every 12-16 s after its entrance,
  never during a blow; `NativeHordeRoar`) with `structure.groan`. Staggers only from a hit of at least 25% of its max hp
  (`NativeHordeKinds.Staggers`); a lighter hit is a flinch. Its death falls at scale and crashes down at .95 s where the
  chest lands: dust, cracks, a shake (`structure.thud`).
- Jump-in: drop entries are reused with `NativeHordeDrop.Hulk` (`Profile(kind)`): warn 1.6 s (rumbling cracks spreading
  over the spot, a pulsing ring scaled to the 5.5 m slam), leap .5 s, fall from the default 9 m (`sqrt(2 (h + .5) / 24)`,
  .89 s), land at `activeAt - 1.5`, recover 1.5 s: `Duration(9, Hulk)` = 4.49 s. Landing: two shockwaves, a flash, a
  crater with chunks, big radial cracks (9 s), a dust wall, heavy debris, props within 8.8 m jolting (drawn only; their
  colliders untouched), a camera shake with pitch and roll within 34 m, falling off with distance;
  `structure.rumble`, `blast.quake`, `structure.boom` (existing keys).

## 8. The wall breach: presentation contract (`Core/NativeBreach.cs`)

An add whose replicated entry is a map entry of kind `"breach"` breaks the wall segment the map names
`breach:<entry id>` (a GameObject whose renderers are the panel; its colliders are the authority's). The wall's box
comes from the segment's first mesh bounds, its outward normal from the entry's `from` (behind) to `exit` (in the
arena). Timeline from the add's replicated `spawnAt` / `activeAt`:

| phase | when | shown |
|---|---|---|
| knock | `spawnAt` to `breakAt` (2.1 s proposed) | the add pounds the wall from behind (blows at .55, 1.2, 2.1 s): the panel shudders, cracks spread from each blow, dust sifts out; `structure.crack` / `structure.thud` |
| break | `breakAt = activeAt - 1.3` | the panel is hidden and a broken panel with a ragged hole the add's size drawn instead; up to 40 chunks burst into the arena with dust and a flash; `structure.boom` / `structure.debris` |
| through | `breakAt` to `activeAt` | the add charges from `from` through the hole to `exit` (the rules move it) |
| rubble | to `restoreAt` (until replicated: `breakAt + 12`) | the chunks lie where they bounced |
| rebuild | `restoreAt` to `restoreAt + 2.6` | a hardlight scan sweeps the hole bottom to top with a grid; the pieces turn into hologram wireframes, fly back to their slots and solidify; `siren.reveal` then `structure.snap` |
| solid | from `restoreAt + 2.6` (`SolidAt`) | the segment's own renderers return with a hardlight flash over the hole's outline; the rules return its collision here |

Proposed replicated fields (nothing reads them yet; the presentation uses the defaults): a frame list
`breaches: [{entry, breakAt, restoreAt}]` on the PvE frame, so a peer joining mid-breach shows the hole and the restore
can be delayed past the default. The presentation never moves colliders. No coordinates in code: the Ward Zero rework
places breach walls and the Hulk drop spot in map data; the presentation reads any entry generically.

## 9. The spitter's acid (owner 2026-10-13, presentation only)

Owner: "for siren minions; The spitter should shoot do a windup animation and shoot acidic spit from their mouth,
creating a small puddle of poison (Can corrode destructibles). Can interupt their spit by hitting them."

- Wind-up (proposed hit .8 s, `NativeHordeKinds.SpitHit`): rears back on the start of the knock-1 reel, the throat sac
  swells and glows acid green, the hinged jaw unhinges (a separate jaw model on the head joint), drool drops, a hiss
  (`field.acid`). Release at LastAttack + hit from the posed mouth socket (`NativeHordeModels.MouthSocket`), not the
  chest: the glob flies its arc (about 9 m/s across, `Flight(d) = max(.35, d / 9 + .12)` s, apex .9 + .08 d m) with a
  dripping, sizzling trail (`magic.darkness.shot`), splashes where it lands; a pilot within .6 m of the landing (plus its body radius) gets an
  acid splash that sizzles and drips off the body for 1.8 s (the poison itself is the existing status / damage screen).
- Puddle: bubbles and fumes, creeps out to the radius (1.3 m proposed; never past it) with a clear bright edge, fades
  over its 4 s. Destructible props (arena props and static blocks, e.g. Brickfall's structure blocks) the glob or the
  puddle touches corrode: smoking pits round the contact, a dripping corroded tint and hiss; when the block breaks the
  existing structure effects take over.
- Interrupt: any damaging hit in the wind-up (`NativeHordeKinds.Interrupts`, inferred from the replicated hit and
  LastAttack until a cancel is replicated) cancels the shown spit: a flinch, the sac deflates over .35 s, the jaw snaps
  shut, the acid dribbles from the mouth and fizzles in a small pool at its feet (1.4 s); no glob.
- Until the rules emit `minion-spit` the DEV preview predicts the glob from the strike; once they do, the presentation
  launches it from the event (`From` mouth, `Position` landing, `Time` launch, `Amount` flight s, `Radius` puddle).

### Known limits (2026-10-13)

- Unity Editor play, Windows builds and multiplayer were not run. The renders come from the harness's software
  renderer, which runs Core only; the Runtime draw code (breach panel, jolts, acid, gallery) compiles under the Unity
  check but has not been seen in Unity.
- No match spawns a Hulk, a breach or a glob until the rules do. The Dev Sandbox gallery shows all of them.
- Breach: until the rules remove the segment's collision, the hole is only visual. The restore uses the default
  (break + 12 s) until `restoreAt` is replicated. The wall's box comes from the segment's first mesh bounds, so only a
  box-shaped segment breaks cleanly.
- The spit interrupt is inferred from the replicated hit and LastAttack. A peer that misses the hit event can disagree
  until the cancel is replicated. Which props corrode is decided by presentation proximity, not by rules damage.
- A jolted prop is drawn with `Graphics.DrawMesh` with its renderer hidden for .75 s. Props without a MeshRenderer on
  their root are skipped, and a MaterialPropertyBlock tint on a jolting prop is not carried over.
- The Hulk's proposed capsule (r .95, h 3.06) is not applied anywhere. Its hit volume is whatever the rules give it.

### High-quality effects pass (2026-10-14, presentation only)

Owner: "Improve visual fx from spitter/hulk zombie (make them high quality) acid needs to look better. Slam/landing/wall
break [effects] also needs improvements from hulk/brute". Core `NativeZombieFx` (pure shapes) drawn by Runtime
`NativeZombieFxDraw` through the shared effects renderer (lit glossy blobs and rubble, toon puffs, teardrops, glows, the
upright slam wave) and a decal layer queued before the effect billboards. No rule, record, radius or time changes:

| effect | driven by | size / time it keeps to |
|---|---|---|
| glob in flight | acid record / `minion-spit` event (`from`, `to`, `at`, `flight`) | rides `NativeHordeFx.Glob` (the arc the server validates); lands at `at + flight` |
| wind-up clot, jaw strings, drool | `LastAttack`, `NativeSpitterRules.Hit` | only before the release; the glob takes over at `LastAttack + Hit` |
| splash, crown, spatter | the landing (`at + flight`) | every drop lands inside `NativeSpitterRules.Radius` |
| pool film, ring, bubbles, fumes | the landing, `NativeSpitterRules.Radius` / `Duration` | liquid never past the radius; the ring holds the radius until `Duration`; evaporates over the last 1.3 s; an etch fades 5 s after |
| smash impact | `LastAttack + hit` (the authority's strike) | wave, cracks, dust and landed rubble inside `NativeHordeKinds.SmashRadiusOf(kind)` round `SmashAheadOf(kind)` |
| jump-in landing | `NativeHordeDrop.LandAt(ActiveAt, profile)` | inside the profile's `SlamRadius` (brute 2.4 m, Hulk 5.5 m) |
| wall breach | `NativeBreach` blows (`.55/1.2/2.1` s after the knock start), `breakAt`, replicated `restoreAt` | chips, burst grit, sparks, dust curtain; hardlight remnants until `restoreAt`; motes in the rebuild |

Timing coupling with the Hulk/brute body animation (separate branch): the effects fire on the replicated marks above, so a
smash or landing pose must put its impact frame on `LastAttack + hit` / `LandAt`, and a breach punch on the `NativeBreach`
blows, as today's takes do. The effects never read a pose.

## 10. The heavy kinds' body animation (owner 2026-10-14)

Owner: "Slam/landing/wall break animation also needs improvements from hulk/brute. Hulk zombie needs adjustments on their
movement (normal zombie movement looks odd at their size). I'd like them to all have a charge animation (they can charge
through walls) and a couple of punch animations (like a straight punch, and a swipe)."

Split: Claude built the body animation on `claude/hulk-motion` (`Core/NativeHordeHeavy.cs`, `Core/NativeHulkMoves.cs`,
`Runtime/NativeHordeHulkMotion.cs`, hooks in `Core/NativeHordeMotion.cs` and `Runtime/NativeHordeKindPresentation.cs`,
`Tests/HulkMotionChecks.cs`). The effects are `claude/zombie-fx` (section 9's high-quality pass); they fire on the same
rule marks and never read a pose. The rules below marked *proposed* are Astra's to implement; until then those animations
are dormant.

### 10.1 What plays today, and what drives it

| animation | driven by (existing state) | contact frame |
|---|---|---|
| Hulk heavy walk / run | replicated position and velocity (the gait's own stride) | each footfall (`Landed`): the stomp dust, cracks and step cue already key on it |
| Hulk turn in place | the replicated yaw while standing | a pivot step per up to 50 degrees, the foot on the turning side first |
| Hulk and brute smash | `LastAttack`, the content's `hit` / `cycle` (unchanged) | both fists down by the floor at `LastAttack + hit`; the pack's hammer take still lands its impact frame there |
| Hulk and brute jump-in landing | entry, `SpawnAt`, `ActiveAt` (`NativeHordeDrop`) | touchdown at `LandAt = ActiveAt - profile.Recover`: Hulk three-point (a fist on the floor), brute both fists; deepest by +.15 s, risen by `ActiveAt` |
| breach knock (entry breach; a Hulk breaking a wall block) | entry `SpawnAt` / the dynamic record's `breakAt` | a right on `NativeBreach.Blows[0]` (.55 s), a left on `[1]` (1.2 s), the shoulder on `[2]` = the break (2.1 s), then a lowered-shoulder carry to 3.4 s |

- **Heavy locomotion** (Hulk only): its own stride table instead of a zombie take scaled up: about .93 steps/s with .8 m
  steps walking (.75 m/s), 1.2 steps/s with 1 m steps running (1.15 m/s), quick 1.35 m steps at a 4.5 m/s charge. The
  body sways over the stance foot, the pelvis and shoulders roll and counter-turn, the knees stay bent, each footfall
  sends a jolt down the body (chest and head a beat late), the arms hang wide and swing late, the feet are lifted and
  stamped down, the head stays steady. Standing it breathes; its idle and twitches run slower (a big body's time). Only
  15% of a zombie walk take remains under it. The feet plant under the hips' balance offset, so a planted foot never
  slides and the drawn ankle stays on its spot (checked at walk, run and charge speed, straight and turning 20 deg/s).
  The audio step spacing (`NativeHordeGait.Stride`) reads the same stride.
- **Turn in place**: standing, the presented facing turns only on pivot steps (about 70 deg/s); a wind-up, a knock or a
  charge's wind-up turns fast (410 deg/s) so a blow faces where the authority aims it. Walking turns at 135 deg/s.
- **Smash** (Hulk and brute): keys over the pack's hammer: rising tall with the fists overhead, dropping into the blow,
  both fists driven to the floor on the strike, a short heavy hold, a heave back up by the recovery. The take's impact
  frame stays exactly at `LastAttack + hit` (checked).
- **Landings** (both profiles): both fists raised for the fall's last .3 s, the touchdown pose at `LandAt` exactly, the
  deepest just after, standing by `ActiveAt`. The Hulk's mirrors its planted fist by its seed.
- **Breach knock**: the entry breach's and a dynamic wall block's knock both follow `NativeBreach.Blows`; the knock
  replaces the hammer take the knock used before (the job and its effects are unchanged).
- Sources: the zombie pack's idle under the heavy gait, its hammer and jump takes (already exported; no new export). The
  Explosive pack's unarmed cross, jab, running attack and dash (already exported for the Cyber Lord) were tried on the Hulk
  model and not used: a boxer's bladed stance and timing. The punches, swipe, charge, knock and landing poses are keyed
  poses in the existing pose system. No licensed file was read or committed.

### 10.2 Proposed: the straight punch and the swipe (dormant; the choice is amended by 10.5)

The rules choose one move when an attack starts and replicate it with `LastAttack`:

| field / name | proposal |
|---|---|
| `NativePveAdd.Move`, `NativeMinionFrame.move` | int: 0 the content attack (the smash, as today), 1 straight punch, 2 swipe; written with `LastAttack` |
| side (the striking arm) | not replicated: `NativeHulkMoves.Side(lastAttack, lost)` on every peer and the host (the presentation's existing hash) |
| choice | Hulk: punch when the target is 1.2-3.2 m away inside a 40 deg cone; swipe when it (or two or more pilots) is within 3 m inside 150 deg; otherwise the smash (and at least every third attack). Deterministic from `SpawnRandom`. Crawling or armless: today's attack timing |
| punch timing | hit `.62` s (`NativeHulkMoves.PunchHit`), cycle `2.0` (`RecoverAt = LastAttack + max(cycle, hit + .05)`) |
| punch volume | a capsule along the facing from .6 to 3.4 m ahead, radius .75, abs(dy) < 2.5, melee line of sight; damage 24 x wave scale; impulse 750 along the facing plus 2.5 m/s up; a block clashes, no damage, half the shove |
| swipe timing | hit `.78` s (`SwipeHit`), cycle `2.4` |
| swipe volume | a 3.2 m sector, 150 deg about the facing, abs(dy) < 2.5; damage 20; impulse 650 across the facing in the sweep's direction (from the swinging arm's side), 40% outward, plus 2 m/s up; block as the punch |
| stagger | as today: a hit of 25% max hp in the wind-up cancels it |
| event (optional) | `Impact` Detail `hulk-move` at `LastAttack`: Target add id, Amount move id, Time `LastAttack` |

Key poses (the presentation's, from `LastAttack`): punch: fist cocked by the shoulder by .34 s, held to .5, driven
straight to full reach at .62, held .12, recoiled, standing at 2.0; mirrored for the left arm. Swipe: arm drawn back wide
on its own side by .39, held to .64, crossing in front at .78, carried across to the other side by .94, over-balanced,
standing at 2.4. The effects job draws the punch with the existing lunge streaks and the swipe with the claw crescent at
the move's strike (no new effect; a dedicated pass is a later FX task).

### 10.3 Proposed: the charge (dormant; v1, superseded by 10.5 for the start, speed, run length, walls and replication)

| item | proposal |
|---|---|
| start | a Hulk not attacking, staggered or entering, its target 7-16 m away on the same floor with a clear straight line, cooldown 9 s |
| wind-up | `.9` s (`ChargeWind`) standing, the heading locked at its start; a 25% stagger in it cancels the charge |
| run | 4.5 m/s (`ChargeSpeed`) along the heading for at most 2.4 s (`ChargeRun`); no pathing, no attacks; takes hits |
| a pilot in the way | stops; 28 damage, impulse 1000 along the heading plus 4.5 m/s up (a block: no damage, half the shove); the Hulk is held 1.0 s (`Stop(Pilot)`) |
| an eligible wall (`NativeHulkBreach.Eligible`) | broken without stopping: a dynamic `NativeBreachRecord` with `breakAt` = the contact, no knock, collision off at once, the usual rebuild; proposed record flag `charged` so the breach's knock cracks are skipped |
| anything else solid | stops; the Hulk is stunned 1.6 s (`Stop(Solid)`, hittable, a punish window) |
| spent | slows to a stop; held .7 s (`Stop(Spent)`) |
| replication | `NativeMinionFrame.chargeAt` (start, -100 none), `chargeEndAt` (-100 while running), `chargeEnd` (0 running, 1 pilot, 2 solid, 3 spent); host, guests, late joins and the killcam all read them |
| events (optional) | `hulk-charge` at the start (Target add id, Position start, Normal heading, Time), `hulk-charge-end` at the end (Amount 1/2/3, Position contact, Time) |

Key poses (from `chargeAt` / `chargeEndAt`): rearing up, then the right foot scraped back and braced, the shoulder
lowered and coiled, a lunge forward on the launch (.9 s); the run holds the lowered right shoulder, the lead arm tucked,
the other arm pumping on the heavy gait's charge stride; a wall broken on the run jolts the shoulder. A pilot hit: the
shoulder drives through, a recoil with the head snapped up, standing by 1.0 s. A solid stop: the body bounces back, the
head snaps back, the arms fly out, a dazed slump and reel, standing by 1.6 s. Spent: a braking lean, standing by .7 s.

### 10.4 Wiring and status

- `Runtime/NativeHordeHulkMotion.cs` `HordeHulkEvent` / `HordeHulkState` is the one place the presentation reads the
  proposed state: today from the events above (none are emitted, so nothing plays). When the frame fields exist, read
  `move`, `chargeAt`, `chargeEndAt`, `chargeEnd` there instead; the poses need nothing else. The audio's strike cue still
  uses the content's strike for a reported move (a follow-up with the frame fields).
- The Dev Sandbox gallery's third Hulk (beside the first) plays all of it on a script: a turn in place, the heavy walk and
  run, a straight punch, a swipe with the left arm, a charge into a solid stop, a wall knock and its carry.
- Animation impacts: no hit timing, hit volume, damage, cooldown, movement, spawn, record or network change. The presented
  Hulk facing lags the authority's while it pivots in place (about 1.7 s for 120 deg); the authority's own facing and
  every blow's aim are unchanged. During a keyed blow, knock or charge a fist may be drawn up to .18 rig m (.32 m) past
  the usual reach limit round the hit capsule (a punch at full extension).
- Checks: `Tests/HulkMotionChecks.cs` (17,582: stride and cadence, planted feet, pivots, every contact on its mark, the
  proposed key poses on their contract times, determinism, no allocation, runtime draws only). Unity Editor, Windows,
  two-client play and the killcam were not run; the poses were reviewed on the harness's software renders only.

### 10.5 Contract v2: punches and a charge at the hunted pilot (proposed 2026-10-14, Astra to confirm)

Owner (2026-10-14, Windows test build): "The hulk needs to punch and charge at the player too, not just the wall. The charge
is avoidable if they move out of the way in time. (Hulk can collide and break walls too.) They literally can charge from
across the map to the player breaking the walls if they are hunting them and 'know' where they are." and "Its currently only
using the ground slam".

PROPOSED, Astra to confirm. Astra's implementation wins where it differs; record the final numbers here and in
`Core/NativeHulkMoves.cs` (the presentation mirrors them; `Tests/HulkChargeChecks.cs` pins the contract checks). Claude's
presentation is built against this contract on `claude/hulk-charge` and plays as soon as the events (or the frame fields
through the one binding point) arrive.

**A. Attacks at pilots (Hulk; the brute may share them later).** When an attack starts the authority chooses a move from the
hunted pilot (`NativeHulkMoves.Choose`, a reference you may adopt or replace):

| situation (from the add's root, horizontal) | move |
|---|---|
| two or more pilots inside the smash disc (2.8 m, 2 m ahead) | 0 smash (content attack, unchanged) |
| the hunted pilot 1.2-3.4 m ahead within 20 deg of the facing (`PunchCone`, `PunchMin`, `PunchReach`) | 1 straight punch |
| the hunted pilot within 3.2 m inside 150 deg (too close for the punch, or wide of it) | 2 swipe |
| anything else (walls and props, a pilot outside both) | 0 smash |
| crawling or armless | today's attack |

Timings, volumes, damage, impulse, block and stagger are 10.2's (punch .62 / 2.0, capsule .6-3.4 m r .75, 24 dmg, 750 + 2.5 up;
swipe .78 / 2.4, 3.2 m 150 deg sector, 20 dmg, 650 across + 40% out + 2 up; a block clashes: no damage, half the shove). The
"at least every third attack is a smash" rule of v1 is dropped (the owner: it only uses the slam).

**B. The charge at the hunted pilot.**

| item | proposal |
|---|---|
| start | a Hulk active, not attacking, staggered, entering or knocking, hunting a pilot it **knows** the position of: line of sight from its eye (2.6 m) to the pilot's chest now, or seen within `ChargeMemory` 2.5 s (aim at the last seen point), or the hunt state reveals the pilot (Siren / director / DEV hunt). The authority defines "knows". Horizontal distance at least `ChargeMinRange` 7 m, same floor (abs dy < 1.5), off cooldown. **No maximum range**: it may cross the map. The lane from the Hulk to the target point (its capsule swept) must hold no unbreakable solid and keep a floor under it (breakables, pilots and adds ignored by this test) |
| wind-up | `ChargeWind` .9 s standing; the yaw turns to track the target (up to 410 deg/s); a 25% stagger cancels it (cooldown 4 s) |
| lock | at chargeAt + .9 the heading **locks** to the yaw then (replicated through the add's yaw, held unchanged for the run) and the target point is the pilot's position then (known or last known); no lead |
| run | `ChargeSpeed` 6 m/s (v1 4.5) straight along the locked heading: no pathing, no homing, no attacks; it takes hits (they do not stop it) |
| breakables in the lane | broken on contact **without stopping**; speed x `ChargeSlow` .6 for `ChargeSlowFor` .3 s after each, then 6 m/s again |
| which breakables | (a) Ward Zero / Expedition interior wall blocks `NativeHulkBreach.Eligible` allows: a dynamic `NativeBreachRecord` {block, actor, point = contact, normal = heading, holeSize = the block's size, breakAt = contact, restoreAt = DefaultRestoreAt}, collision off at once, the usual rebuild and occupancy hold; (b) Hulk breach panels (`breach:<id>` segments, e.g. Ward Zero's and the Expedition town's) whose far side is in bounds: the entry `NativeBreachRecord` {entry, actor = the Hulk, breakAt = contact}, collision off at once, the usual restore; a panel on the boundary stops it as Solid; (c) destructible structure blocks and props (`arena.Props` / structure segments, not `Indestructible`, not machine fixtures): destroyed through the existing `DamageProp` / fracture path (its replicated props, segments and events), impulse along the heading. **Never**: the map shell and bounds walls, floors, ceilings, stairs and ramps, hazard objects, door frames, indestructible props |
| optional record flag | `NativeBreachRecord.charged` (bool) for (a) and (b); the presentation infers it from the actor's running charge when absent |
| other adds in the lane | shoved aside (impulse 450 away from the lane, no damage); they never stop it |
| end: pilot | any living pilot whose capsule meets the Hulk's swept capsule (abs dy < 2), not only the target: 28 x wave scale damage, impulse 1000 along the heading + 4.5 m/s up; a block: no damage, half the shove; held `Stop(Pilot)` 1.0 s |
| end: solid | an unbreakable solid, a boundary panel or a floor gap / ledge ahead: stunned `Stop(Solid)` 1.6 s (hittable, staggerable) |
| end: spent | `ChargeOvershoot` 4 m past the locked target point's projection on the lane, or the longest run `ChargeRun` 16 s (96 m): a braking stop, held `Stop(Spent)` .7 s |
| cooldown | `ChargeCooldown` 10 s from the end |
| avoidable | a pilot who leaves the lane (about 1.4 m from its line: .95 + .45) before the body reaches them is missed: from 7 m that is 1.17 s of run after the lock, after a .9 s tell |

**C. Replication (what the presentation reads).** On `NativeMinionFrame` and `NativePveAdd` (host, guests, late joins,
replay / killcam restore them):

| field | meaning |
|---|---|
| `move` (int) | 0 smash/content, 1 punch, 2 swipe; written with LastAttack |
| `chargeAt` (float) | the wind-up's start; -100 none |
| `chargeEndAt` (float) | the end; -100 while winding up or running |
| `chargeEnd` (int) | 0 running, 1 pilot, 2 solid, 3 spent |
| `chargeTarget` (int) | the hunted pilot's seat id (0 none): only that pilot's view gets the warning |
| yaw (existing) | the locked heading during the run |
| breach / structure records and events (existing) | the walls broken on the run, so every peer breaks the same ones |

Impact events stay the fallback (keep emitting them even with the fields; the presentation accepts either): `hulk-move`
(Target add id, Amount move id, Time LastAttack); `hulk-charge` (Target add id, **Amount the hunted seat id**, Position start,
Normal facing, Time chargeAt); `hulk-charge-end` (Target add id, Amount 1/2/3, Position contact, Time end).

**D. What the presentation does with it (claude/hulk-charge).**

- Punch and swipe from the move id (event today, field later): the keyed poses of 10.2 on their strike times, the striking
  fist's glow through the wind-up, a trail over the drive (straight back for the punch, along the sweep for the swipe), the
  swipe's crescent at the Hulk's scale, a burst at the fist on the strike, and for a pilot inside the proposed volume
  (`NativeHulkMoves.InPunch` / `InSwipe`, the presentation's guess as the smash's disc already is) a burst at the chest, a
  streak along the shove and a kick of that pilot's view.
- The charge (Core `NativeHulkCharge.State`, Runtime `NativeHordeHulkMotion.cs` / `NativeHordeHulkCharge.cs`): the wind-up's
  tell (rearing, the jaw bellowing, the seam glow gathering, the right foot scraped back with dust and a furrow, a faint line
  along its facing); at the lock a ground streak sweeps 12 m along the locked heading with the lane's edges and chevrons; the run
  holds the lowered shoulder for as long as the authority runs it (held at 4.5 / 6 / 7.5 m/s for 30 s on the heavy gait's planted
  feet; a lost end is caught by the add standing still); walls in its lane (the eligible blocks, the breach panels, the gallery's
  walls) shake and dust from .55 s before contact (a rattled drawn copy or the revamp's block dressing nudged; colliders never
  moved); a break of this Hulk's in its run bursts with no knock and flinches the shoulder (it runs on); the ends' impacts,
  shakes and the struck pilot's kick.
- The warning, for `chargeTarget` only: a HUD bracket round the Hulk on screen (danger red in its lane, warn amber out of it;
  corners over the crosshair's clear circle are left out, so nothing sits on the aim), chevron arrows toward it off screen or
  behind (behind always points down), deeper, larger and faster as the urgency rises (distance and closing speed; never falls as
  it closes), a soft edge glow when near; the wind-up's bellow, a rumble at the launch and when close, its footfalls heard through
  walls; a gentle shake at each footfall as it closes. Settings · camera shake gates the shakes; Reduced effects halves them,
  stills the pulse and thins the effects.
- The Dev Sandbox gallery's fourth Hulk (to the local pilot's left) plays it all without the rules: a punch and a swipe at a
  hardlight dummy, a turn to the pilot, the wind-up and lock at them (the warning plays), a 34 m charge through two walls of its
  own, then alternately a pilot hit (a second dummy) and a solid stop (a dark pillar).
- Binding the frame fields is the body of `HordeHulkFrame` in `Runtime/NativeHordeHulkMotion.cs` (three lines, written in its doc
  comment); nothing else changes.

**E. Animation impacts (Claude's work).** No hit timing, hitbox, damage, cooldown, movement, spawn, record or network change.
The presented facing in the run is the locked heading (the authority's yaw by contract). A crash-through flinch is drawn in the
run (pose only; the speed change is the authority's). The proposed rule numbers above (6 m/s, 16 s, 4 m overshoot, 2.5 s memory,
10 s cooldown, .6 x for .3 s, the move choice) are proposals for Astra.

## Prompt for Astra

```
Task: Siren zombie brutes, simulation side (owner 2026-10-12: "Brutes can be big muscle versions, that knockbacks and
slam. For now, we will have them enter from the middle of the map by jumping down and creating a small slam effect on
their landing. (I am planning to revamp this map, so keep this in mind)"). Claude's presentation is on
claude/siren-zombies (docs/SIREN-ZOMBIES.md, Core/NativeHordeDrop.cs); it already draws the jump-down from the add's
replicated entry, spawnAt and activeAt. Base: the current shared Unity head.

1. Map data, not code: add an entry kind "drop" to PveContent.json `entries` (and tools/export_pve.mjs if it generates
   them), e.g. {"id":"drop-centre","kind":"drop","from":{x,z},"exit":{x,z},"yaw":<facing>,"height":6.5}: `exit` is the
   landing point (Ward Zero today: a clear spot at the map's middle with 2.4 m of open floor), `from` the same point
   (reserved for a future ledge), `height` the fall in metres. Add `public float height;` to PveEntry (0 means
   NativeHordeDrop.DefaultHeight 6.5) and tell Claude, who switches NativeHordeDrop.HeightOf to read it. No coordinate
   in code: the owner is revamping the map and will move the entry.
2. Spawning (NativePveHorde): while an arena has drop entries, brutes spawn only through them (round-robin; fall back
   to today's entries when none). Timeline: spawnAt = the spawn, activeAt = spawnAt + NativeHordeDrop.Duration(height)
   (2.71 s from 6.5 m: warn .7, leap .35, fall sqrt(2(h+.35)/24), recover .9); LandAt = activeAt - .9. Until LandAt
   the body is held kinematic at `exit`, untargetable, takes no damage and does not block; from LandAt it can be hit;
   it attacks from activeAt. Replicate nothing new (entry, spawnAt, activeAt already travel).
3. Landing slam at LandAt: pilots within 2.4 m of the landing point (|dy| < 1.5, line of sight from .45 m above it)
   take 10 damage at the centre falling linearly to 5 at the rim, impulse 380 outward plus about 2.5 m/s up; a block
   clashes (no damage, half the shove); other adds in the radius are shoved (impulse 200, no damage). Optional event
   Detail "minion-drop" (Position, Radius) at LandAt.
4. Brute knock-back smash: keep the 1.6 m disc 1.2 m ahead and the .43 hit; impulse 260 -> 420 with about 2.5 m/s up,
   pushed from the disc centre outward; no knock-down; the block clash unchanged. Emit "minion-slam" if you can
   (docs/SIREN-MINION-VARIANTS.md section 6). The charge in VARIANTS section 3 stays proposed.
5. Hit volume (optional, with the owner): brute capsule radius .42 -> .55, height 1.7 -> 2.0 (centre 1.0, root 1.0),
   nav agent radius to match; update Core/NativeHordeKinds Radius/Height/Root for the brute in the same change
   (Tests/HordeKindChecks reads the collider from NativePveHorde.cs) and hand the re-sculpt back to Claude.
6. Tests: rule tests for the drop timeline (activeAt - spawnAt == NativeHordeDrop.Duration(height)), the kinematic hold,
   the landing slam's radius/falloff/block, the smash shove and the fallback without drop entries. Keep
   Tests/HordeKindChecks.cs and Tests/ZombieMotionChecks.cs passing.
Do not change presentation files beyond NativeHordeKinds' mirrored numbers; hand visual follow-ups (height field read,
brute re-sculpt, slam event wiring) back to Claude in DEV_LOG.
```

## Prompt for Astra: the Hulk, the wall breach and the spitter's acid (2026-10-13)

```
Task: Siren Hulk, wall breach entrances and the spitter's acid, simulation side. Owner 2026-10-13: "Add a Hulk zombie
too, similar to brute but much bigger model size, huge knockback, very slow, very high hp. 1/2 max spawned. The jump in
too, shaking the ground and a even bigger stage effect when landing. They can also break through the walls to enter
(Stage will eventually holographically recover broken pieces)" and "for siren minions; The spitter should shoot do a
windup animation and shoot acidic spit from their mouth, creating a small puddle of poison (Can corrode destructibles).
Can interupt their spit by hitting them." Claude's presentation is on claude/hulk-zombie (docs/SIREN-ZOMBIES.md
sections 7-9, Core/NativeHordeKinds.cs, Core/NativeHordeDrop.cs, Core/NativeBreach.cs). Numbers are proposals; record
changes in docs/SIREN-MINION-VARIANTS.md and tell Claude, who mirrors them in NativeHordeKinds.

A. Hulk kind
1. Add "hulk" to the PvE content (VARIANTS row): hp 1600, damage 32, walk .75, run 1.15, reach 3, start 2.6, hit 1.1,
   cycle 3.4, lunge .15, impulse 900, reward 300, limb .55, head .6, stagger .2; attack "smash" (the disc: 2.8 m radius,
   centred 2 m ahead, |dy| < 2.5 m, melee line of sight), plus about 4 m/s upward pop, pushed from the disc centre
   outward; a block clashes with half the shove and no damage. At most 2 alive (1 below the later waves is fine).
2. Hit volume: capsule radius .95, height 3.06, centre 1.53 above the floor, root height 1.53 (presentation:
   NativeHordeKinds.HulkRadius .954, WorldHeight 3.06, RootHeight 1.53); nav agent radius to match. Keep the numbers
   in NativeHordeKinds and the collider equal (Tests/HulkSpitterChecks reads them).
3. Stagger rule: only a hit of at least 25% of max hp staggers (and cancels its wind-up); a heavy melee hit alone does
   not (NativeHordeKinds.Staggers; the other kinds keep "heavy melee or 25%").
4. Entrances: the Hulk spawns through "drop" entries (the brute's contract) on NativeHordeDrop.Hulk:
   activeAt = spawnAt + NativeHordeDrop.Duration(height, NativeHordeDrop.Hulk) (4.49 s from the default 9 m: warn 1.6,
   leap .5, fall sqrt(2 (h + .5) / 24), recover 1.5); LandAt = activeAt - 1.5. Hold it kinematic and untargetable
   until LandAt. Landing slam at LandAt: pilots within 5.5 m (|dy| < 2, line of sight from .45 m up) take 25 at the
   centre falling to 10 at the rim, impulse 900 outward plus 4 m/s up; a block halves the shove, no damage; other adds
   shoved (impulse 400). Dynamic props within 5.5 m may take a small impulse (the presentation already jolts them
   visually). Optional event "minion-drop" (Position, Radius 5.5). When an arena has breach entries, the Hulk may use
   those instead (B); a breach-capable brute is optional.

B. Breach entries (new entry kind "breach"; map data by the Ward Zero rework, no coordinates in code)
1. PveContent entries: {"id":"<id>","kind":"breach","from":{x,z} (behind the wall, outside the arena),"exit":{x,z}
   (in the arena, clear floor), "yaw":<facing>, "restore": 12 (optional seconds from the break)}. The map holds a wall
   segment GameObject named "breach:<id>" (NativeBreach.ObjectName): its renderers are what Claude breaks, its
   colliders (and any nav obstacle) are yours. Several breach walls per map are fine.
2. Timeline (NativeBreach): the add spawns at `from` (kinematic, hidden behind the wall, untargetable);
   breakAt = spawnAt + 2.1 (NativeBreach.Knock; blows at .55, 1.2, 2.1); activeAt = breakAt + 1.3
   (NativeBreach.Through), the add moving from `from` to `exit` in that window (targetable from breakAt);
   restoreAt = breakAt + restore (default 12); solidAt = restoreAt + 2.6 (NativeBreach.Rebuild).
3. Collision: at breakAt disable the segment's colliders (and carve the nav). At solidAt enable them again; if a pilot
   or add overlaps the segment at restoreAt, push the restore back (check every .5 s) and replicate the new restoreAt,
   or push the overlapping body out along the wall normal at solidAt (pick one, record it). A breach in progress is
   not reused by another add until solidAt.
4. Replicate on the PvE frame: breaches: [{entry:"<id>", breakAt, restoreAt}] (keep each until solidAt), so a peer
   joining mid-breach and the killcam agree; tell Claude, who switches NativeHordeBreachPresentation from its default
   restore (breakAt + 12) to the frame. Optional events "minion-breach" (Position = wall centre, Target = add id) at
   breakAt and "breach-restored" at solidAt.

C. Spitter acid
1. Wind-up hit .8 (NativeHordeKinds.SpitHit; VARIANTS row now hit .8, recovery 1.8), cycle 2.6, range 11.
2. At LastAttack + hit launch a glob from the mouth (about 1.2 m above the floor, .3 m ahead of the root; the
   presentation's socket is NativeHordeModels.MouthSocket) to the target's floor point led by its velocity times the
   flight, clamped to 11 m: flight = NativeHordeFx.Flight(d) = max(.35, d / 9 + .12) s, apex .9 + .08 d m
   (NativeHordeFx.Glob; you can step the same arc). Emit Detail "minion-spit" with From (mouth), Position (landing),
   Time (launch), Amount (flight s), Radius (puddle 1.3), Target (add id); the presentation launches the glob from it.
3. Landing: pilots within .6 m of the landing (plus their radius) take 7 direct damage. A puddle (radius 1.3, 4 s):
   every pilot inside gets poison through the existing DoT path: RefreshDot needs a NativePilot source today, so add a
   PvE-source overload keyed "pve:poison" (4 dps, duration .5, refreshed every .25 s while inside), so ticks still
   emit PoisonTick (damage screen, sounds). Stacking: one PvE poison per pilot (the strongest dps, the latest end);
   several puddles do not stack. Adds are not poisoned.
4. Corrosion: destructible props (arena.Props, e.g. Brickfall's structure blocks; skip Indestructible) touching the
   puddle (bounds within the radius) take 6 every .5 s through DamageProp(prop, amount, Vector3.zero, point, null,
   "acid"), and 10 on a direct hit, so the existing stress / fracture path and structure effects run. Optional event
   "minion-corrode" (Position, Target prop index) for Claude to key the pits on.
5. Interrupt: any damaging hit in the wind-up (0 <= now - LastAttack < hit) cancels the spit (no glob); the next
   attack may start after 60% of the cycle (partial reset). This matches NativeHordeKinds.Interrupts, which the
   presentation already infers from the replicated hit and LastAttack. Replicate the cancel (the minion frame's
   staggerAt or a cancelAt, or a Detail "minion-spit-cancel" event) so a peer that missed the hit still agrees.

D. Tests: rule tests for the Hulk stats, cap, capsule and stagger rule; the Hulk drop timeline and slam; the breach
timeline, collision removal and return, the restore delay and replication; the glob flight, direct hit, puddle DoT
(no stacking), corrosion through DamageProp and the interrupt. Keep Tests/HordeKindChecks.cs, ZombieMotionChecks.cs
and HulkSpitterChecks.cs passing. Do not change presentation files beyond NativeHordeKinds' mirrored numbers; hand
visual follow-ups (restoreAt from the frame, the cancel event, minion-corrode wiring) back to Claude in DEV_LOG.
```

## Shared-head reconciliation (2026-10-13, PR #160)

Merged shared `b177323`: kept both Brute/Hulk `DropProfile` timelines and made both `HeightOf` overloads honour positive finite `PveEntry.height`, falling back to the selected kind's default (Brute 6.5 m, Hulk 9 m). The Ward Zero draft's 7/13 m heights therefore reach both presentation profiles. Tests cover both defaults, authored heights and invalid values.

The 260 -> 420 conflict is **impulse**, not HP: Brute HP stays 240. Keep PR #160's 420 outward smash with upward pop. Keep the shared spitter proposal at `SpitHit = .8f`, matching §9's wind-up/interrupt presentation; this PR does not implement the pending spitter or Hulk simulation. `HulkSpitterChecks` and `HordeDropChecks` both run. The revamp's `hulk` entry routing and full Hulk/breach rules remain the separate §7-9 / WARDZERO-SIMULATION-CONTRACT §7 task; this change does not import the revamp or enable Hulk spawning.

The Hulk proposal remains 900 impulse. Its check now pins 900 and >2x the stronger Brute (420); the old >=3x comparison assumed the superseded 260 Brute impulse. No Hulk rebalance.

## Spitter / wave / shortcut implementation — 2026-10-13

`codex/unity-spitters-wave-budget` supersedes the presentation-only spitter status above. Spitters are enabled in waves 2–3 and every Siren phase (weight .15; at most four). The stats mirror NativeHordeKinds: 45 HP and 30 kill reward. Authority launches the mouth arc at .8 s and cancels on any damaging pre-release hit (retry in 1.56 s). Crawling/armless spitters retain the mouth attack and .8 s telegraph. The server checks the launch arc for world obstruction; obstructed lobs are suppressed. Landing: 7 base damage within .6 m + pilot radius. Pool: radius 1.3, duration 4 s, poison 4 base DPS refreshed for .5 s every .25 s; pools share one strongest/latest PvE poison. Corrosion: 10 on landing and 6 every .5 s, through DamageProp; protected scenery and zombies are excluded. Direct damage/poison follow wave damage scaling.

`NativePveFrame.acid` retains up to 16 records (id, actor, from, to, at, flight, damageScale); `NativeMinionFrame.spitCancelAt` retains cancellations. Presentation consumes records without a visual redesign, including late joins and replay; phase holds clear pools. Both peers must have matching PveContent, included in the join revision. Relay protocol stays 7.

Hulks may now approach a nearby eligible interior wall when the same-floor direct route saves more than 1 m over the cached detour. The far side must fit the body and provide safe supported space. Knock/restoration timings are unchanged; the stuck detector remains the fallback. Eligible walls/colliders are cached and shortcut physics is limited to four checks/sec/Hulk.

Waves use the existing finite 22/28/34 solo totals with existing party scaling, fixed at wave start. Clear requires no unspawned quota and no living enemies; no <=2 quiet clear or 150 s timeout. Boss reinforcement rules are unchanged. Catch-up creates at most two adds per tick, and crowd separation uses nearby spatial cells rather than rescanning the whole crowd.

Editor/PhysX, Windows gameplay, and measured end-to-end FPS are pending. Detailed checks and handoff: DEV_LOG and the task PR.

## Prompt for Astra: the heavy kinds' charge and punches (2026-10-14)

```
Task: Siren Hulk charge, straight punch and swipe, simulation side. Owner 2026-10-14: "I'd like them to all have a charge
animation (they can charge through walls) and a couple of punch animations (like a straight punch, and a swipe)." Claude's
animation is on claude/hulk-motion (docs/SIREN-ZOMBIES.md section 10, Core/NativeHulkMoves.cs, Core/NativeHordeHeavy.cs,
Runtime/NativeHordeHulkMotion.cs); it is authored to the numbers below and dormant until you replicate the state. Numbers
are proposals; record changes in section 10 and keep NativeHulkMoves' mirrored constants equal (Tests/HulkMotionChecks).
Do not change the existing smash, drop or breach marks (LastAttack + hit, LandAt, NativeBreach.Blows): the effects
(claude/zombie-fx) and the animation key on them.

A. Attack moves (Hulk; the brute may use the same later)
1. When an attack starts choose a move: 1 straight punch (target 1.2-3.2 m inside a 40 deg cone), 2 swipe (target, or two
   or more pilots, within 3 m inside 150 deg), else 0 = the content smash (at least every third attack). Deterministic
   from SpawnRandom. Crawling or armless keep today's attack.
2. Timing: punch hit .62 / cycle 2.0, swipe hit .78 / cycle 2.4 (StrikeAt = LastAttack + hit; RecoverAt = LastAttack +
   max(cycle, hit + .05)). The 25% stagger cancels a wind-up as today.
3. Volumes: punch = capsule along the facing from .6 to 3.4 m, radius .75, |dy| < 2.5, melee LOS; 24 damage x wave scale;
   impulse 750 along the facing + 2.5 m/s up. Swipe = 3.2 m sector, 150 deg, |dy| < 2.5; 20 damage; impulse 650 across
   the facing in the sweep direction (from NativeHulkMoves.Side(LastAttack, lost)'s side to the other), 40% outward,
   + 2 m/s up. A block clashes: no damage, half the shove.
4. Replicate: NativePveAdd.Move and NativeMinionFrame.move (int) written with LastAttack; guests, late joins and the
   killcam restore it. Optional Impact Detail "hulk-move" at LastAttack (Target add id, Amount move id).

B. Charge (Hulk)
1. Start when not attacking, staggered or entering, the target 7-16 m away on the same floor with a clear straight line;
   cooldown 9 s. Wind-up .9 s standing, heading locked; a 25% stagger in it cancels.
2. Run 4.5 m/s along the heading up to 2.4 s; no pathing, no attacks; it takes hits.
3. Ends: a pilot in the way -> stop, 28 damage, impulse 1000 along the heading + 4.5 up (block: no damage, half shove),
   held 1.0 s. An eligible interior wall (NativeHulkBreach.Eligible) -> broken without stopping: a dynamic breach record
   with breakAt = contact, no knock (add a `charged` flag so the breach effects skip the knock cracks), collision off at
   once, the usual rebuild and occupancy hold. Anything else solid -> stop, stunned 1.6 s (hittable). Run out -> stop,
   held .7 s.
4. Replicate on NativeMinionFrame: chargeAt (-100 none), chargeEndAt (-100 while running), chargeEnd (0 running,
   1 pilot, 2 solid, 3 spent). Optional events "hulk-charge" (start: Target, Position, Normal heading, Time) and
   "hulk-charge-end" (Amount 1/2/3, Position contact, Time).

C. Hand back to Claude in DEV_LOG: the field names as implemented; Claude switches Runtime/NativeHordeHulkMotion.cs
   (HordeHulkState) from the events to the fields, wires the move's strike cue, and asks the effects owner for a charge
   and punch effects pass. Tests: move choice and timing, both volumes, block, stagger cancel, the charge's wind-up
   lock, each end, the wall break without a knock, replication through JsonUtility and replay. Keep HordeKindChecks,
   ZombieMotionChecks, HulkSpitterChecks, ZombieFxChecks and HulkMotionChecks passing.
```

## Prompt for Astra: the Hulk's punches and charge at the hunted pilot, contract v2 (2026-10-14)

```
Task: Siren Hulk attacks and charge at pilots, simulation side. Owner 2026-10-14 (Windows test build): "The hulk needs to
punch and charge at the player too, not just the wall. The charge is avoidable if they move out of the way in time. (Hulk can
collide and break walls too.) They literally can charge from across the map to the player breaking the walls if they are
hunting them and 'know' where they are." and "Its currently only using the ground slam". Contract: docs/SIREN-ZOMBIES.md 10.5
(proposed; your implementation wins, record the final numbers there and in Core/NativeHulkMoves.cs). Claude's presentation is on
claude/hulk-charge and plays as soon as the state below is replicated.

A. Attacks: choose the move at the attack's start from the hunted pilot (NativeHulkMoves.Choose is a reference): 2+ pilots in
   the smash disc -> smash; pilot 1.2-3.4 m inside 20 deg -> punch (1); within 3.2 m inside 150 deg -> swipe (2); else smash.
   Crawling/armless: today's attack. Timings/volumes/damage/impulse/block as section 10.2. Drop v1's every-third-attack smash.
B. Charge: start when hunting a pilot it knows (LOS now, or seen within 2.5 s -> last seen point, or the Siren/director/DEV hunt
   state), >= 7 m horizontal, same floor, off cooldown (10 s from the previous end), the lane to the target point free of
   unbreakable solids and with floor under it. No maximum range. Wind-up .9 s tracking the target (25% stagger cancels, 4 s
   cooldown). At chargeAt+.9 lock the heading to the yaw (hold the yaw there) and the target point; run 6 m/s straight, no homing,
   no attacks, hits do not stop it.
   Breakables on contact, without stopping (speed x.6 for .3 s each): eligible interior wall blocks (dynamic NativeBreachRecord,
   breakAt = contact, normal = heading, collision off at once, usual rebuild/occupancy hold), in-bounds Hulk breach panels (entry
   record, actor = the Hulk, breakAt = contact; a boundary panel stops it as Solid), destructible props/structure blocks (destroy
   through DamageProp/fracture, impulse along the heading). Never shell/bounds walls, floors, ceilings, stairs/ramps, hazards,
   door frames, indestructibles, machine fixtures. Optional NativeBreachRecord.charged. Adds in the lane: shoved aside 450, no
   damage.
   Ends: pilot (any pilot meeting the swept capsule, |dy|<2): 28 x wave scale, 1000 along heading + 4.5 up, block halves shove
   and no damage, held 1.0 s. Solid (unbreakable, boundary panel, ledge/floor gap): stunned 1.6 s. Spent: 4 m past the locked
   target point's projection or 16 s of run: held .7 s.
C. Replicate on NativeMinionFrame + NativePveAdd (host, guests, late join, replay): move (int), chargeAt (-100 none),
   chargeEndAt (-100 while running), chargeEnd (0 running, 1 pilot, 2 solid, 3 spent), chargeTarget (hunted seat id, 0 none);
   the yaw holds the locked heading; walls through the existing breach/structure records and events. Keep the Impact events too:
   hulk-move (Target add, Amount move), hulk-charge (Target add, Amount hunted seat, Position start, Normal facing, Time),
   hulk-charge-end (Target add, Amount 1/2/3, Position contact, Time).
D. Tests: move choice; each volume and the block; the wind-up's tracking and stagger cancel; the lock (heading and target point
   fixed after .9 s); a cross-map run; each breakable kind broken without stopping and the slow-down; never the shell/floor;
   each end; the cooldown; a pilot stepping 1.5 m out of the lane after the lock is missed; replication through JsonUtility,
   late join and replay. Keep HordeKindChecks, ZombieMotionChecks, HulkSpitterChecks, ZombieFxChecks, HulkMotionChecks and
   HulkChargeChecks passing (update NativeHulkMoves' mirrored constants if you change a number).
E. Hand back to Claude in DEV_LOG: the field names as implemented. Claude fills HordeHulkFrame (Runtime/NativeHordeHulkMotion.cs,
   three lines in its doc comment) and wires the move's strike cue. Do not change presentation files beyond NativeHulkMoves'
   mirrored numbers.
```
