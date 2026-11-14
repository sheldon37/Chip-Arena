# Match-start spawn-in and the countdown float

Owner (2026-10-13): "Lets also add a spawning in animation; Players tend to float for 3 seconds when match is starting currently lol".

Claude built the spawn-in (presentation) on `claude/spawn-in`, base `e2e894e`. The simulation is unchanged. The root cause is in
simulation, so this file also carries the hand-off to Astra (the last section).

## Why pilots float (inspected on `e2e894e`)

1. **Every spawn is 0.23 m too high.** The spawn table puts the capsule centre at `y = 1.1` on a floor whose top is `0`.
   That holds for all 8 points of GameContent's global table (all 15 GameContent maps use it) and for the 16 points of each
   UnityMaps table (Brickfall, Factory, Cyber City). The ground motor rests a capsule at `HalfHeight + Skin = 0.87`
   (`NativeGroundMotor`), so each pilot appears 0.23 m above its rest. Its presented feet (`body - 0.865`) are at 23.5 cm.
   It is the same on every map, every spawn, every round and every set: `Reset` calls `SpawnPilot` for each round.
2. **The contact flag stays "airborne" until the fight starts.** Outside `NativePhase.Fight`, `NativeArena.Step` zeroes
   every pilot's velocity and returns before `ProbePilotGround`. A new `NativePilot` starts each round with
   `Grounded = false`, so `Actor.Grounded` stays false through the intro and the 3 s countdown. The jump layer
   (`NativeJumpTrack` treats a first airborne sample as already in the air) poses every arena rig in its jump/Fall pose.
   On the first fight tick the probe finds the floor, and the Land take and the `move.land` sound play. This tucked
   air pose is the "float" the owner sees.
3. **The body's height during the countdown (inferred, not measured).** `Physics.Simulate` still runs each tick with
   gravity -23 after the velocity is zeroed, so PhysX should integrate one tick of gravity per tick (6.4 mm, 0.38 m/s).
   The capsule would then reach the floor about 0.65 s after the reset: during the first round's intro, or early in a
   later round's countdown. If PhysX instead holds it at 1.1 (a sleeping body, for example), the fight's first ticks
   drop it. The probe finds the floor on the 5th fight tick (gap 0.25 to the 0.2 reach after 4 ticks of gravity) and the
   motor snaps the rest, so the touchdown is about 0.067 s after GO. A Unity log of `Body.position.y` through a countdown
   would settle which one happens. The presentation reads right either way.

Guests see the same thing. Positions and the flag come from the host's frames. Guest prediction is locked outside the
fight (`CanMove` false) and copies the host's flag. The killcam replays those frames.

## The spawn-in

All of it lives in `Core/NativeSpawnIn.cs` (pure, engine free) and `Runtime/NativeSpawnInPresentation.cs`, with one-line
hooks in `ArenaPresentation.Render`, `RenderGlow`, `SourceCharacter`, `RenderStatus`, the combat glow, the overhead HUD, the
first-person view copy and `ConversionGame` (camera and screen).

- **Clock.** The spawn-in is a pure function of replicated match state and the presentation clock:
  `End = Countdown ? PhaseEnd : FightAt`, and the spawn-in's age is now minus `End`. The host, guests, late joiners and the
  killcam therefore draw the same moment, on every round. A live guest's own predicted pilot is drawn
  `GuestLead` (0.1 s, the snapshot clock's delay) ahead, so its drop starts when its controls unlock. Intro phases and
  drafts show nothing. The entrance owns the screen until the countdown starts, so nothing overlaps.
- **Timeline** (seconds from GO):

  | Time | What happens |
  |---|---|
  | -3.0 to -2.7 | The beam drives down from the sky (or from the ceiling) onto the spawn. A floor ring and pool flash. |
  | -2.75 to -1.55 | The body forms from the feet up. Each part goes from a dithered reveal to solid, using its posed height. A hologram slice of the pilot's own body skin, with scanlines, leads the solid front. A scan ring and voxel sparks ride the front. The forming parts glow in the team colour. |
  | -1.55 | The glitch flash and the magenta/cyan RGB split complete the build. On Full there are two small glitch blips later in the hold. |
  | -1.55 to 0 | The beam holds the pilot. Its feet stay at the spawn's own height (the float, now on purpose) with a ±2.2 cm bob. Its rig is posed **on the ground**, so the Fall loop no longer plays. Motes stream up. A ring pulse fires on the 2 and 1 ticks. In the last 0.3 s the beam tightens and brightens. |
  | 0 (GO) | The beam retracts upward with a flash. The pilot drops for the authority's own fall, `AuthorityFall(lift) + 1 tick` (0.083 s on today's spawns), posed airborne so the jump layer lands it. |
  | Touchdown | A hardlight impact ring, a fast inner ring, a floor flash, light dust and sparks. The local pilot's camera gets a 4.5 cm / 0.9° dip that settles within 0.32 s. |

- **Skins, hats and weapons.** The hologram, flash and split use the pilot's own body skin and tier (Standard, Sheldon
  Clothes or Prototype). Each of these waits for its part: the hat for the head, the weapon and bow for the hand, body Chips
  for the chest, the Cyber Lord's hardlight look for the hands, and team bands and armour halos for their joints. The
  overhead health bar waits until the body is whole. The colour follows the bands' rule: the team colour in a teams match;
  otherwise, and always for my own pilot, the pilot's accent.
- **First person.** My view rides the hold, bob and drop, and kicks at the touchdown (camera shake only; Reduced effects
  scales it to 0.4). The screen shows:
  - a navy veil above a scanline that wipes up the screen with the build;
  - a soft vignette in my colour while the beam holds me;
  - an edge split at the completion;
  - a gentle flash at GO.

  My own column is not drawn; the floor ring and motes still are. Peaks: veil 0.32, vignette 0.24, flash 0.22.
- **Tiers.** Reduced effects: fewer motes, no split, glitch or shimmer, a softer flash and tint, a smaller bob, and a
  calmer screen. The low graphics tier: no hologram or dust, coarser rings, no front sparks. Nothing allocates per frame.

## Adapting to a simulation fix

The release mode comes from the authored spawn table, so peers agree and the round-1 intro (when the body may already have
sunk) looks the same as later rounds.

- A spawn lifted more than `LiftMin` (6 cm) over its rest **drops**: it is held at its own height and falls for
  `AuthorityFall`.
- A grounded spawn is **lowered**: the beam holds the feet 16 cm up and lowers them to the floor **by GO**. There is no drop,
  a softer ring, no dust and a third of the kick.

Nothing else needs to change on the presentation side. `SpawnInChecks` measures the lift from data instead of pinning 0.23,
so it keeps passing either way. It does pin the authority's gravity (-23) and the presented feet offset (0.865) that
`AuthorityFall` is derived from.

## Hand-off to Astra (simulation; not done here)

> **Root cause.** Every spawn table places the capsule centre at y 1.1 on a floor at 0. That is 0.23 m over the ground
> motor's rest (0.87), on all 18 maps. Outside the fight, `NativeArena.Step` zeroes velocity and returns before
> `ProbePilotGround`, so `p.Grounded` (fresh each round, false) replicates "airborne" through the intro and the 3 s
> countdown. Every rig's jump layer then plays its air pose until the first fight tick, and the Land take and `move.land`
> fire at GO. Probably `Physics.Simulate` still integrates one tick of gravity per held tick (6.4 mm, about 0.38 m/s), so
> the body sinks to the floor about 0.65 s after the reset (please confirm with a log of `Body.position.y`). If it does
> not, the probe finds the floor on the 5th fight tick and the motor snaps the remaining 0.17 m.
>
> **Option 1 (recommended): ground the spawns.** Author the spawn centres at floor + `HalfHeight + Skin` (0.87 on today's
> maps), or snap them at `SpawnPilot` with a downward probe. Also run `ProbePilotGround` for every pilot before the
> non-fight early return (or set the flag from that probe), so `Actor.Grounded` is true while pilots stand in the
> intro/countdown. Keep zeroing velocity. This removes the float for every consumer (bots, spectators, the HUD). The
> presentation switches on its own to lowering the pilot to the floor by GO; no presentation change is needed.
>
> **Option 2: keep the hold, but make it explicit.** Make bodies kinematic at the spawn through the intro/countdown (no
> PhysX sink) and replicate a spawn state, for example `Actor.SpawnHeldUntil = FightAt` (or `SpawnAt` plus a held flag).
> Leave the contact flag false. The presentation already drops the pilot for `NativeSpawnIn.AuthorityFall(lift)` from the
> authored height. Map the new field in `NativeSpawnInPresentation.SpawnEvaluate` (one place, Claude) instead of
> inferring it from the phase.
>
> Either way: if you change gravity (-23), the presented feet offset (0.865), the 3 s countdown or the ground reach,
> `SpawnInChecks` will point at the constant to update (`NativeSpawnIn.Countdown`, `Gravity`, `FeetOffset`). Do not change
> timings for the presentation's sake: the hold ends exactly at the fight start from `PhaseEnd`/`FightAt` as they are.
