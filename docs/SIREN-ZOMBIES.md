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
| `spitter` | chase-1, walk-6 | chase-2, sprint-1 | shamble-1, idle-3 | spit (procedural) | as above |
| `crawler` | procedural four-limbed trot | - | - | pounce (procedural) | procedural deaths |
| `bloater` | walk-6, walk-4 | chase-1 | idle-3 | belly (procedural) | procedural swell and burst |

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
