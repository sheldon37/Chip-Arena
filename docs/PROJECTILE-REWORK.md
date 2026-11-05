# Straight projectiles and selective hitscan

Implemented by Astra, 2026-11-04, on `codex/projectile-rework`, from shared `a395de04`.
The owner clarified that “Arcane” means all four existing magic schools.

## Tuning

| Primary | Mode | Speed | Time over 30 / 60 m |
| --- | --- | --- | --- |
| Pistol | simulated, straight | 250 m/s | 120 / 240 ms |
| Machine Gun | simulated, straight | 200 m/s | 150 / 300 ms |
| Rifle | hitscan; simulated when travel is required | instant / 150 m/s fallback | 0 / 0 ms; fallback 200 / 400 ms |
| Fire | simulated, straight | 100 m/s | 300 / 600 ms |
| Water | simulated, straight | 110 m/s | 273 / 545 ms |
| Lightning | simulated, straight | 120 m/s | 250 / 500 ms |
| Darkness | simulated, straight | 80 m/s | 375 / 750 ms |

These are release-candidate starting values, not human-playtest conclusions. Authored bounds:
Brickfall 60 × 52 m; Factory/Cybercity 76 × 60 m; Transit 96 × 96 m; Foundry 100 × 68 m;
Expedition city 240 × 180 m. Cover limits most sight lines. At 60 Hz, ordinary pistol/MG shots
advance 4.17/3.33 m per tick, so swept contact is essential. The existing 160 m travel cap and
3-second lifetime remain; higher speed means slow weapons now reach that cap before expiry.
This intentionally increases practical reach on long unobstructed lanes; the weapon `range`
field (used by aiming/bots), damage/falloff, cadence, spread, recoil, resources and impulses are
unchanged. No explicit distance-damage falloff was added. Bow, ability projectiles, DEV kits
and Bombadier (28 m/s, gravity 23, range 25, .9 s primary fuse) retain their rules.

## Existing definitions and chip composition

`ContentWeapon` retains `speed`, `gravity` (reference acceleration), `shotRadius` and `range`.
New fields: `projectileMode` (`simulated` or `hitscan`, absent means simulated),
`gravityMultiplier` (default 1), `projectileRange`/`projectileLifetime` (zero means legacy
160 m / 3 s), `projectileBounces` (added to chip bounces), and
`projectileBounceRetention` (default 1, multiplies chip retention). These fields are validated.
`projectileImpactSpeed` preserves the old wound-energy baseline when travel speed changes;
it does not change damage or collision. The scale follows copied fragments.

Straight weapons retain their previous reference acceleration but set `gravityMultiplier=0`.
Existing gravity-increasing chips (Lobber, Meteor, Slow Ricochet) restore that reference before
applying their existing multipliers. Gravity reductions/removal still compose normally.
A gravity-reduction-only chip has no additional drop to remove on a plain straight weapon.
Future chips can add/set `projectileGravityAdd` in m/s², including negative acceleration;
this combines with the reference acceleration and then the existing `projectileGravity` stat.
Pong and boomerang retain their own no-gravity rules.

Rifle falls back per shot for changed projectile-speed stats, gravity, homing, boomerang,
zigzag, Pong, stop/go, acceleration, corkscrew/helix, orbitals, bowling/elevator shots, Leet
travel/lock state, freezing, or a hostile time/black-hole field. Hostile fields conservatively
select simulation even when the current trajectory might miss them. This keeps their effects.
Ray-compatible primary multishot, pierces, wall/ceiling ricochets, explosions and impact chips
reuse the ordinary impact handlers. Surface segments resolve within the same shot, with a
32-contact per-shot and 128-contact shared per-tick budget. If exceeded, the remaining shot continues simulated on following ticks.

Necessary exceptions: reflected/parried shots become simulated to retain reflection speed and
future interactions. Impact fragments and spawn extras (backfire/crossfire etc.) remain existing
simulated children rather than recursively becoming rays. Their limits and no-recursion rules
are retained. Straight shots do not curve solely because a reduction-only chip is equipped.

## Authority, collision and networking

Fire and projectile stepping explicitly refuse replica execution. The host uses existing
mathematical flight and PhysicsScene sphere sweeps with the original collision radius; no
per-bullet Rigidbody is introduced. Hitscan uses the same pilot/minion/prop filters, protection,
headshot rules, chip handlers and destruction. Cover bounds interceptor queries. Bots use the
same Fire path and resolved gravity. Poolrooms water remains non-colliding/non-cover; tile
walls, steps and lips retain physical occlusion. Water does not slow shots or attenuate blasts.

There is no new client-authoritative damage, client-reported hit, or target rewind. Guest input
is evaluated against current host colliders when received. This retains host advantage under
latency; it is not lag compensation. Real host/guest latency and moving-player feel require
Windows playtesting, in addition to native moving-position contact checks.

The compatibility revision includes the projectile rule version and invariant weapon profiles;
old or differently configured clients are refused by the existing revision handshake.
Travelling shots use the existing snapshot format, including resolved velocity/gravity/radius.
Instant shots do not leave a fake live projectile in snapshots. Existing endpoints are recorded.
An appended `EventKind.Hitscan` carries each segment through the existing event codec:
`Id` unique per segment, `Projectile` shared per shot, `Source` owner, `Time` authority time,
`From` start, `Position` end, `Detail` weapon id, `Radius` collision radius, `Color` weapon color.
Current snapshots/late join receive only the retained event window, not a future live ray.
Instant kills have death `projectile=0`, selecting the existing victim camera; simulated fallbacks,
reflections and fragments retain projectile-follow eligibility. No artificial slow flight is added.

## Claude handoff

No VFX, animation or camera artwork changed. The normal Shot/muzzle event still fires once.
Consume `EventKind.Hitscan` for short-lived rifle tracers, including miss and ricochet segments;
deduplicate by event Id, not Projectile, since a ricochet has several segments. Use authority Time
and do not replay stale segments on late join. The ordinary impact/ricochet events still exist.
Do not use tracer duration to delay damage or create a simulated bullet. Fast projectile trails
need review at low frame rates; magic shots use their existing school looks at the new speeds.
The only menu adapter change removes inaccurate “Projectiles fall” hints from straight weapons.
Review Poolrooms surface visuals for instant segment crossings if a new splash is desired;
there is no new water collision or splash rendering in this task. At close range, fast simulated
shots may begin and end between snapshots; review tracer reconstruction from the source muzzle
and authoritative hit/end metadata rather than delaying the simulation. A budget continuation or
reflection can have both completed ray segments and a live simulated Shot with the same id;
do not suppress its remaining flight. Hit events keep that id; only instant death camera
attribution uses zero.

## Presentation (Claude, `claude/ranged-polish`)

Done: Hitscan segments drawn by event id from the drawn muzzle, on authority time; stale ones skipped; rounds that begin and end between frames reconstructed from `NativeShotEnds`. Core `NativeTracers.cs`, Runtime `NativeTracerPresentation.cs`; the chips' parts and the box-built shots in Core `NativeChipLooks.cs`. Still asked of the rules: `Projectile` on gun Shot events or `Owner` on `NativeShotEnd`; `Projectile` and `Normal` on Ricochet; parent `Projectile` on Split; an Impact with `Normal` for world-collider hits; a sample time on `NativeBomb`. See DEV_LOG.

## Validation and release

Managed projectile checks cover profiles, integrated straight paths, chip fallback/composition,
old defaults and Bow/TNT preservation. Native `ValidateProjectileRework` runs through the
existing Windows ranged gate: immediate contact, cover, changed target position, no duplicate
damage, reflection fallback, travel modifiers, splits/multishot, ricochet, pierce, replica refusal,
killcam selection and real JsonUtility segment/endpoint round trip. It also reports a bounded
120-tick, eight-source MG multishot/speed/Overdrive stress workload (100-projectile cap).
That synthetic workload is not an eight-client or Expedition FPS measurement. Native results
and test-channel publication require the Windows builder receipt; semantic compilation alone
cannot establish PhysX contact correctness or measured game performance. See DEV_LOG/PR for
actual validation and release status.

Local verification: 284 focused rules checks; full managed suite exit 0, 954 PASS groups;
pinned Unity semantic compilation and 3,115-file source validation pass. Owner approved public upload and test update. Current shared `cdae8447` is integrated; final
integration checks and source/builder/publication receipts follow in the task PR. Source
upload alone is not a native build or test-channel publication receipt.
