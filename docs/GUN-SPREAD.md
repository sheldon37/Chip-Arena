# The guns: the zoom, the spread and the reactive crosshair

Owner, 2026-11-10: "Right click on ranged isn't 'zooming' into crosshair, it just makes a noise." and "I think we need a reactive
crosshair and spread. Currently I think we don't have initial spread on weapons."

## The zoom

Holding aim (right mouse, the pad's aim) with any gun that has an aim row zooms the camera to that row's field of view in first person:
the pistol to 55°, the machine gun to 52°, the rifle to 28° through its scope (Runtime `ConversionGame.cs`, the camera's `aim`). Before
this only the rifle zoomed (the camera's aim was gated on `NativeRangedRules.Scope`), while the rules aimed every gun and the aim sound
played, so the pistol and the machine gun aimed with no zoom.

## The spread (Core `NativeSpread`, host-side)

Each gun fires inside a cone (degrees, half-angle). The shot lands uniformly over the cone's disc (Runtime `NativeProjectiles.cs`).

| Gun | From the hip at rest | + at a full run | + in the air | Each shot's bloom | Bloom cap | Bloom holds | Then recovers |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Pistol | 1.0° | 1.2° | 2.5° | 0.9° | 3.0° | 0.3 s | 5°/s |
| Machine gun | 1.6° | 1.4° | 3.0° | 0.22° | 3.2° | 0.15 s | 6°/s |
| Rifle | 2.5° | 2.0° | 3.5° | 2.5° | 4.0° | 0.2 s | 3°/s |

- Moving opens the cone by the share of a full run (6.8 m/s at speed 1; a sprint no more than a run).
- Aiming narrows the whole cone by the aim row's spread as it settles (the content's `ads` rows): the pistol to 25% over 0.15 s, the
  machine gun to 40% over 0.2 s, the rifle to nothing over 0.3 s (a settled scope fires true).
- The machine gun climbs to its cap over about a second of fire and settles about half a second after it stops. A pistol fired as fast
  as it goes blooms a little, under its cap. The rifle recovers between its shots.
- The chips' changes still apply on top (Bullet Hose's 12°, the bipod's 30%, Slide Shot's half). Overdrive's rounds neither aim nor
  bloom. The fan, the bow, the Bombadier and the other classes keep their own spread.

These are rules (logged for Astra): before this the guns' spread was a tiny cube offset (well under half a degree from the hip).

## The crosshair (Runtime `NativeFightHud.Crosshair`)

For the pistol, the machine gun and the rifle the crosshair is the dot and four arms that sit on the edge of the cone the next shot lands
in, at the camera's field of view (`NativeSpread.Pixels`). They kick out with each shot and settle back (eased: out fast, back gently),
and move with running, jumping and aiming. The bloom is followed from the pilot's shots as they arrive (`ShotsFired`, `LastShot`), with
the same numbers as the rules. Other weapons keep the dot and two arms.

## Checks

`CoreTests --spread` (Tests/SpreadChecks.cs): the cones, the aim's narrowing, the bloom's climb, cap and recovery at each gun's rate, the
disc's uniform sampling, the screen mapping, and that the rules and the crosshair read the same numbers. `GunKitChecks` pins the zoom.
