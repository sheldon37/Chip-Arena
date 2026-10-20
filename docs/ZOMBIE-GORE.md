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
- **Where (on the drawn body, since 2026-10-20).** Where the shot enters the drawn model: see "On the drawn body" below. With the authority's hit metadata (below), only the part it names is a candidate, and its head flag decides the head. Old frames keep the inference: a Headshot event forces the head, otherwise the first drawn part the shot enters decides. A lost limb is never a candidate on any frame, because it is not drawn.
- **Wound.** Up to 8 per add, kept in bone space so they ride the animation. The Full tier shows the newest 8, Reduced 5, Low 3.
  - Bullet: a recessed dark hole, torn flesh round it (a bone fleck on a head shot) and a blood run down the body's surface that lengthens as it bleeds.
  - Blade: a gash along the swing.
  - Blast: a scorched patch.
  - Wounds stay glossy while wet and darken as they dry.
- **Burst.** Red mist (blow-back out of the entry, one puff on along the shot) and flesh bits: two in three back out of the entry, the rest on along the shot. They bounce once and shrink away within 2.4 s.
- **Spray** into the blood sim, from the wound along the shot:
  - puncture for a bullet; cavity (more and faster drops) for a head shot or a heavy round;
  - cut for a blade; crush for a blast.
  - It stains floors and walls.
- **Drips.** A decaying trickle for 4.5 s (5 a wound on Full), from the run's tip as it is then.

### On the drawn body (2026-10-20, owner: "Zombie wounds appear off their body")

Until 2026-10-20 a wound went on a capsule round its joint: the Standard pilot's radius, times a per-kind girth, times the drawn scale. The capsules did not match the zombie models. The big kinds' heads were worst: the Hulk's head capsule had a 0.43 m radius against a drawn head of at most 0.25 m, because the head took the limb girth (1.6) while the Hulk's head is 0.6 wide. The blood run was one straight strip in the wound's plane, laid across the shot rather than down, up to 5.6 times the wound's size. Neighbouring runs crossed as X's beside the body. Measured against the drawn model, wound pieces stood up to 0.27 m off the shambler and 0.55 m off the Hulk (`Tests/ZombieGoreChecks.cs` `OnBody`; DEV_LOG has the per-kind table).

Now (`Core/NativeHordeGoreBody.cs`, `NativeHordeGore.PlaceOn`, `NativeHordeGoreState.OnBody`):
- **The surface.** Each joint's triangles of the kind's look at the Full tier, in the joint's own frame: the frame the runtime draws each part in. The add's own look applies as ZombieLit's vert draws it: head, torso and limb proportions, and missing pieces folded away. The hinged jaw is included, closed. The bank's loader hands over the Full models it builds (`NativeHordeGoreBody.Keep`, 31 bodies, about 6 MB), so no hit builds a model. A dangling part's sway (at most 2.4 cm) and the spit and belly swell are not followed.
- **The place.** The first drawn part the shot's ray enters. The ray is a projectile's own 3D travel (the event's `Normal`, its velocity) through the replicated point, else the flat push; a blast's ray runs in from the body's side facing it. If the ray misses every drawn part (it only grazed the authority's capsule), the wound goes on the nearest drawn point of the part the old rule picks. The wound lies on the surface, with its normal the face's.
- **The footprint.** The torn ring, the gash's length and the scorch are made smaller, down to 0.38 of their size, until every edge lies within 1.2 cm (rig) of the part. A wound on a thin part or at a box's edge is therefore a smaller wound.
- **The run.** Traced at the hit in three steps down the part's surface from under the wound, each step put back on the surface. It stops where the surface faces up or down, where a stretch's middle would leave the part, or where a step jumps. Each stretch is drawn along itself, with the drop at the tip.
- **Model tier.** Wounds sit on the Full model. A live add drawn Reduced shows none: it is past the nearest 24 adds (16 on low graphics), and the gore's own cap is 24, 14 or 8 adds. Falling bodies, corpses within the wounds' 24 m and the DEV preview are drawn Full.
- **Cost.** About 60–75 µs a hit in the managed checks (.NET 8 Release), nothing per frame: the placement and the run are traced once, at the hit.

### A death (`HordeKindDeath` → `HordeGoreDeath`)

The fate comes from the killing blow. "Health" below is the hit's strength over the add's max HP: the replicated damage, clamped to the health left, or with the metadata the larger of that and `Raw` (blasts and crushes keep the replicated damage, see below).

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

## Authority hit metadata (Astra, 2026-10-20)

`ArenaEvent` now carries optional horde-hit data. Events remain `EventKind.Hit`, so pilot Headshot announcers, hit sounds and statistics keep their existing behavior. The presentation binding is done (Claude, 2026-10-20, `claude/zombie-gore-bind`; see "Binding" below).

| Field | Contract |
| --- | --- |
| `HasHordeHit` | Metadata is present. Old frames default false; retain the existing visual inference. |
| `Head` | Any positive damage share selected head (part 1), even when torso is the largest share. |
| `Raw` | Original `amount` passed to `HitPveAdd`, before zone modifiers, limb multipliers and HP clamping. `Amount` retains actual damage. |
| `HasPart` | `Part` is known; distinguishes known torso from missing anatomy. |
| `Part` | Largest positive original share's LIMB bit: 0 torso, 1 head, 2/4 upper arms, 8/16 forearms, 32/64 thighs, 128/256 shins. Ties keep the first share. Recorded before already-lost parts are redirected to torso. |

Use `Raw` for the requested raw-damage/max-HP gore decision; `Raw - Amount` is not exact overkill because zone and limb modifiers can change damage. Normal hits inspect the existing shares with no extra random draws. The `Pve == null` direct-hit path resolves deterministic anatomy without changing its flat damage. Its blast path has no authority shares: `HasPart` is false and `Head` retains the supplied flag.

For `HordeGoreHit`, prefer `e.Head` when `HasHordeHit`; otherwise keep the existing Headshot/inferred fallback. Bind raw damage and the known part in the presentation's decision/wound placement separately. This authority change does not alter timing, hitboxes, damage, cooldowns or movement; networking gains these optional fields and validation.

### Binding (Claude, 2026-10-20)

`HordeGoreHit` reads only the replicated event and the add's replicated lost limbs. When `HasHordeHit` is false, every field below is ignored and the gore is bit for bit what it was (checked by a digest recorded on `8740ceb`).

| Field | Drives |
| --- | --- |
| `Head` | The head, alone: the wound goes on the head (even when the torso's share was larger), the spray is a cavity, the bits and mist are the head's, the head snaps harder (`NativeHordeReact`), and the death can burst the head. `Head` false means no head hit, even when the nearest drawn part is the head. |
| `HasPart`/`Part` | Where the wound goes: the nearest point of the drawn joints that make up the part (`NativeHordeGore.PartJoints`). Torso: pelvis, spine, chest, neck and clavicles. Head: the head. Upper arm or thigh: that joint. Forearm: the elbow joint and hand. Shin: the knee joint, foot and toe. The even bits are the rig's left, the same LIMB-bit-to-joint map the severed-limb gore uses. Part 0 with `HasPart` is a known torso. `HasPart` false keeps the nearest drawn part. |
| Lost parts | The authority names a part it has already severed, then moves the damage to the torso. A lost part is not drawn, so its wound goes on the nearest of what is left of the limb and the trunk (`NativeHordeGore.Struck`). For a lost forearm, that is the upper arm's stump; for a lost shin, the thigh's stump. For a lost upper arm or thigh, it is the trunk; for a lost head, the neck. A wound never lands on a hidden joint. The lost bits are the add's replicated ones, or for the killing blow the last ones it was drawn with. |
| `Raw` | The hit's strength (`NativeHordeGore.Strength`) is the larger of `Raw` and `Amount`. It drives the gib (a heavy round or heavy blow at 80 % of max HP), the head burst (a head kill at 50 %, not an arrow), the blood spray's severity and the burst's power. `Raw - Amount` is never used. A head share's 1.5 can make `Amount` the larger. |
| Blasts and crushes | Keep `Amount`. A blast's `Raw` is the damage at its centre, before the falloff the authority's shares apply over the distance, so it is not what reached the add. The blast keeps its rule: 33 % of max HP reaching it, or within 1.4 m. |

Animation impact: none on hit timing, hitboxes, damage, cooldowns, movement or networking. The only motion change is the head snap's existing headshot strength (2.2x) on a metadata head hit.

## Checks

`--zombie-gore` runs `ZombieGoreChecks`, `MinionFxChecks` and `HordeKindChecks`. `ZombieGoreChecks` is also in the full run. It checks:
- **No treatment left:** no glow code on the seven, only eyes, acid and boils glow in the kind's colour, no tongues, and source scans.
- **Placement:** 80 shots on four kinds at four facings wound the drawn part the shot enters first, at the entry and on the part, facing the shooter, with the spray along the shot. The authority's point comes from its capsule. Head shots spray harder. Gashes run along the sweep. A blast wound faces the blast. Stubs stand out of the cut.
- **On the drawn body (`OnBody`):** 1,106 hits on all 16 kinds and every look, standing and walking (drawn on the pose 1.6 and 4.5 s later), five causes and three gore tiers. Every wound piece (hole, torn flesh, gash, scorch, run stretch, drop) must be within 3.5 cm × max(1, scale) of its own drawn part; the worst is 3.7 cm, a Hulk piece. 95 % are within 2 cm (wound, run and drop), and every centre is on the part. The test's drawn body is its own: the model posed as the runtime draws it, with the shader's vert written out from the shader, not the gore's `NativeHordeGoreBody`. Regressions for the cause: a head shot on the brute and the Hulk is on the drawn head (the old capsule placement is 6 and 22 cm off it), and the runs go down the surface (the old strip lay a median 51° off). Also checked: an old frame never wounds a lost limb, live wounds draw only on the Full model, and the bank hands over its models.
- **Joint matrices:** Unity's `localToWorldMatrix` chain, written out as column-major products, converts through `NativeHordeGore.FromColumns` (which the runtime's `GoreN` calls) to exactly the matrices the checks pose. `Column` takes them back (`GoreU`).
- **Caps and cleanup:** 60 kills over 30 s on each tier.
- **Determinism:** host, guest and killcam.
- **Tiers:** each lower tier draws less.
- **Allocation:** zero per frame.
- **Presentation only:** source scan.
- **Authority metadata:**
  - present vs absent;
  - the head flag over a torso-largest share (and no head flag over a head-nearest point);
  - known placement for every LIMB bit on four kinds at four facings;
  - the authority's own anatomy (`NativeHordeDamage.Part`) agreeing with the drawn sides;
  - known torso vs unknown part;
  - lost parts over every closed set of lost limbs;
  - raw gib, head-burst and blast thresholds, and the spray and burst strength;
  - the old-frame digest (bit for bit; re-recorded on purpose for the on-the-body placement, `0xf8a1621b9ac8bba8`; the binding's own was `0x1a7d959dbc8e8969`);
  - host/guest/killcam with the metadata;
  - zero allocation with metadata hits.

Changed on purpose:
- `HordeKindChecks` (no flickering seams, no tongues, hits bleed through the gore);
- `MinionFxChecks` hooks (no licks, flame cubes or ranking; vent dust and hint);
- `IronworksKindChecks` (the seven grow no tongues);
- `station-enemies-baseline.txt` (49 `tongues` and `tierN` lines: colour and emission sums only).
- 2026-10-20 wound fix, `ZombieGoreChecks`:
  - placement is measured on the drawn model instead of capsules;
  - a wound's normal is now the surface's, so "faces the shooter" means only the side facing the shot (> 0, least 0.11), where before it was > 0.15 on a tilted normal;
  - the old-frame digest is re-recorded;
  - `Lost` is read on every frame, because a lost limb is not drawn; the stray-data check no longer treats it as authority data.

## Not verified here

Unity Editor play, the Windows build, two-client play and the owner's look in Unity. The renders are an offline WebGL
emulation of `ZombieLit` and the gore, lit as a night alley. They are not Unity.
