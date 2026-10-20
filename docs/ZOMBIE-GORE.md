# Zombie gore: standard zombies that bleed

Owner, 2026-10-19: "Lets fix this animated lines thing on the zombies. I think it was when we were initially gearing the
zombies for siren; But they've become more universal. I want them to be more "standard" zombies, that produce gore when shot /
gibbed, not just black flames. High-quality design and fx please :)". Follow-up: "The glowing eyes are still fine :)".

Branch `claude/zombie-gore`. This is presentation only. Damage, death timing, hitboxes, AI, movement, drops, networking and the wire format are unchanged.
The checks are in `Tests/ZombieGoreChecks.cs` (`--zombie-gore`).

## What was removed, and what stays

The Siren look had been put on every horde add. It is removed from the Siren's seven kinds: shambler, runner, brute,
spitter, crawler, bloater and Hulk.

| Source | Was | Now |
|---|---|---|
| `ZombieLit` rim (`Resources/NativeZombieLit.shader`) | A lavender rim at .16, plus a rising band of light lines (`sin(y*23 - t*6.5 ...)` x `_InkFlow` .008): the "animated lines" | A faint neutral rim (.5, .53, .58) at .14 keeps the silhouette in the dark. There is no flow term. |
| Glow cells (`K.Seam`, the `MinionFlicker` codes, `_MinionGlow` 1 on every horde material) | Every seam, crown crack, armour strip and split glowed in the kind's colour and flickered | Seams are dark dead veins (`ZVein`, `ZVeinDark`). The crown crack is crusted blood (`ZFleshDeep`). Visor, buckle, sternum, forearm and shin strips are dead accents (`ZDeadAccent`). The splits are raw flesh. No flicker code. `HordeFlickers(kind)` is true only for the zone kinds. |
| Spitter sac, acid and drool; bloater boils | Flickering glow | A steady glow in the kind's colour (`K.Juice`, `K.Glow` with no code on the seven) |
| Black-flame tongues (`NativeHordeModels.Tongues`, bank `horde-tongue`, `QueueHordeTongue`) | Two to four flame tongues per add | None (`Tongues` is empty, `MostTongues` 0) |
| Flame licks and cubes (`NativeMinionFx.Lick`, `FlameCube`, the lick and cube budgets) | On every add | Removed |
| Cracked-head violet haze (`NativeMinionFx.Haze`) | On the cracked look | Removed |
| Vent emergence (`NativeMinionFx.Effects` cubes) | Black-flame cubes | Dust and grit kicked up round the rim (`NativeHordeGore.VentDust`, `vfx.Smoke`) |
| Death | Charred, then burned away (`hordeDeathTint`, vanish, `PartVanish`) | A corpse with gore (below). The burn-away is kept only for the Cinderling's own ember crumble. |
| Hits | The Siren's black ink spray (`blood.Spray(..., ink:true)`) | Red blood through the gore |
| PvE gore for non-kind adds (`NativeGorePresentation`) | `NativeGoreFlames` hit cubes, stump flame drips, gib flame puffs, ink-coloured material | Blood-coloured. Kind-drawn adds skip it, because the horde gore covers them. |
| Bloater burst (`DrawHordeBursts`) | Ink cubes | The amber gas stays. Flesh, bone and blood are its remains (Burst fate). |
| Thrown parts (`HordePartsDraw`) | Charred and vanished | They lie, keep a raw cap, trail blood and sink with the corpses' life |

Kept unchanged:
- every kind's glowing eyes (owner);
- the Siren's possession hint: crown seam, halo, grin and trail. This is the Siren cue. The lines were on every add, so they never marked a summoned one, and no new cue is needed;
- the Ironworks heat and embers: the Cinderling's ember crumble, the Slagback's crust and the zone kinds' glowing seams;
- the Station kinds' designs and lights;
- the spitter's acid glob and puddle;
- the Hulk's and brute's slam, crash and breach effects;
- the Siren boss's own effects;
- the bloater's gas.

Models keep their geometry. Only colour and emission changed: the triangles, vertices, positions, normals and bones
in `station-enemies-baseline.txt` are unchanged. Silhouettes and hit volumes are untouched.

## The standard zombie

Over the existing voxel bodies:
- `ZombieStain` in the shared shader block puts stepped 7 cm patches of ground-in grime and old near-black blood over the suit,
  the armour and, more lightly, the skin. It is seeded by the add's variation.
- The seams read as dark rot.
- The crown's split is crusted blood over bone.
- The splits show raw flesh.

## Gore

Code:
- `Core/NativeHordeGore.cs` holds the pure rules and the fixed-size state.
- `Runtime/NativeHordeGorePresentation.cs` is the glue to the replicated events, the rig and the shared renderers.
- The voxels draw through `NativeVfxDraw.Block`, a lit box.
- The mist draws through `vfx.Vapor`.
- Pools are zombie-effects decals.
- The blood is the existing `NativeBloodSim` (drops, then splats on floors and walls).

### A hit (`HordeGoreHit`, every replicated Hit or Headshot on a horde add)

- **Cause** from the event's `Detail`, the attacker's spec and `DamageKind`:
  - field, burn, poison and chain ticks do not bleed;
  - "blast" is a blast;
  - melee is a blade;
  - a bow is an arrow;
  - a rifle or plasma overflow is heavy;
  - anything else is a bullet.
- **Where.** The drawn body's nearest part capsule to the replicated hit point (kind girth applied), on its surface facing the shot. A Headshot event, which adds do not send yet, forces the head.
- **Wound.** Up to 8 per add, kept in bone space so they ride the animation. The Full tier shows the newest 8, Reduced 5, Low 3.
  - Bullet: a recessed dark hole, torn flesh round it (a bone fleck on a head shot) and a blood run down the body that lengthens as it bleeds.
  - Blade: a gash along the swing.
  - Blast: a scorched patch.
  - Wounds stay glossy while wet and darken as they dry.
- **Burst.** Red mist (blow-back out of the entry, one puff on along the shot) and flesh bits: two in three back out of the entry, the rest on along the shot. They bounce once and shrink away within 2.4 s.
- **Spray** into the blood sim, from the wound along the shot:
  - puncture for a bullet; cavity (more and faster drops) for a head shot or a heavy round;
  - cut for a blade; crush for a blast.
  - It stains floors and walls.
- **Drips.** A decaying trickle out of each wound for 4.5 s (5 a wound on Full).

### A death (`HordeKindDeath` → `HordeGoreDeath`)

The fate comes from the killing blow:

| Fate | When |
|---|---|
| Corpse | Default |
| HeadGib | A head kill with a heavy round or a heavy melee blow, or one that took half the add's health (not an arrow) |
| Gib | A blast at 33 % of max HP or within 1.4 m; a heavy round or blow at 80 % |
| Crush | Maglev, train, press, crane or saw hazards |
| Burst | The bloater |

Gibs per 4 s are capped at 6 on Full, 4 on Reduced and 2 on Low, counted by the deaths' own times. Over the cap the add falls as a corpse.

- **Corpse.**
  - The kind's death take falls. The pool spreads from the moment the head is down, fast and then creeping, with 18 lobes.
  - At the take's end (2.3 s) the corpse keeps its pose with its wounds and drips.
  - It lies for the tier's life (22, 15 or 9 s), then sinks .42 m and dithers away over 2.4 s.
  - Corpses are capped at 24, 14 or 8 per tier. Past the cap the oldest sink first, ordered by death time and then id.
  - A body whose remains the cap sinks while it is still falling sinks from the pose it has. It never pops back.
- **Gib / Crush.**
  - Head, arms and thighs fly off along the blow as the model's own parts. Each tumbles, bounces and rests with a raw cap, trailing blood.
  - The trunk bursts into flesh, rib, fat and suit chunks (30, 18 or 9).
  - A mist cloud and a heavy crush and sever spray paint the floor and walls nearby.
  - A wider pool spreads.
- **HeadGib.**
  - The head is gone and a raw neck stub stands out of the cut.
  - Skull and flesh chunks are thrown up and back.
  - The neck fountains in pulses for 1.5 s as the body drops.
- **Melee kill.**
  - The existing `NativeMeleeSlice` plan throws its joints as parts, with raw stubs on the body.
  - Hits along the way leave gashes.
  - A true mesh-plane bisect of the zombie meshes is not used, because `NativeBisect` needs the source models' cores. The joint slice is the cut.
- **Severed limbs** on a living add (the authority's LIMB bits) leave a stump on the body and a thrown part.
- **Arrows** lodged in an add stay in its corpse (`HordeArrowBone`).

### Budgets, tiers and determinism

- Fixed arrays (96 adds' wounds, 48 bursts, 32 remains, 48 parts, 2048 voxels a frame) and no allocation per frame (checked over 3000 frames).
- Wounds are drawn for the nearest adds within 24 m (24, 14 or 8 adds by tier). Corpses use the Full model within 24 m, the Reduced model to 80 m, and none past that.
- Reduced and Low draw fewer bits, chunks, mist, drips and wounds, and keep fewer corpses for less time.
- The gore switch (`ArenaPresentation.Gore`) off gives corpses only: no wounds, gibs or pools.
- Everything is seeded from the add's id and the event's time and index. Pieces are pure functions of their age, so the host, guests and the killcam's replay draw the same gore.

## DEV preview

DEV panel > Previews > **Zombie gore preview**. It plays five lanes, looping every 16 s:

| Lane | Kind | Sequence | Result |
|---|---|---|---|
| 1 | Shambler | SMG hits, a head shot, a kill | Corpse |
| 2 | Runner | Rifle head kill | Head gib |
| 3 | Brute | A bullet, then a grenade | Gib |
| 4 | Shambler (look 1) | Blade hits, then a greatsword kill | Slice |
| 5 | Spitter | SMG burst kill | Corpse |

## Data the authority could add (Astra)

See the handoff. None of it is required.
- `HitPveAdd` knows `head` but emits `EventKind.Hit`.
- `Amount` is clamped to the remaining health, so overkill is unknown.
- The struck part is not sent.

The presentation infers the head from the nearest drawn part, and decides gibs from the cause and the clamped share.

## Checks

`--zombie-gore` runs `ZombieGoreChecks`, `MinionFxChecks` and `HordeKindChecks`. `ZombieGoreChecks` is also in the full run. It checks:
- **No treatment left:** no glow code on the seven, only eyes, acid and boils glow in the kind's colour, no tongues, and source scans.
- **Placement:** 80 shots on four kinds at four facings wound the part struck, at the hit, facing the shooter, with the spray along the shot. Head shots spray harder. Gashes run along the sweep. A blast wound faces the blast. Stubs stand out of the cut.
- **Caps and cleanup:** 60 kills over 30 s on each tier.
- **Determinism:** host, guest and killcam.
- **Tiers:** each lower tier draws less.
- **Allocation:** zero per frame.
- **Presentation only:** source scan.

Changed on purpose:
- `HordeKindChecks` (no flickering seams, no tongues, hits bleed through the gore);
- `MinionFxChecks` hooks (no licks, flame cubes or ranking; vent dust and hint);
- `IronworksKindChecks` (the seven grow no tongues);
- `station-enemies-baseline.txt` (49 `tongues` and `tierN` lines: colour and emission sums only).

## Not verified here

Unity Editor play, the Windows build, two-client play and the owner's look in Unity. The renders are an offline WebGL
emulation of `ZombieLit` and the gore, lit as a night alley. They are not Unity.
