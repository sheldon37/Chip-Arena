# The ranged weapons' intros (Intros v2, I4)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!" These four replace the old signatures, which are archived
(`docs/INTROS-V2.md`): Deadeye, Longwatch, Belt Rattle and Arrowfall.

| Weapon | Intro (id) | Length / name beat | Set | Card |
| --- | --- | --- | --- | --- |
| Pistol | **Sundown** (`intro2-weapon-pistol`) | 7.8 s / 6.0 s | a frontier street at sundown, a timber belfry at the edge of town | Wipe, warm |
| Rifle | **One Breath** (`intro2-weapon-rifle`) | 8.4 s / 6.4 s | a rain-soaked rooftop at night over a city avenue | Type, ice blue |
| Machine gun | **Brass Storm** (`intro2-weapon-machinegun`) | 7.8 s / 6.0 s | a freight depot's yard at night under sodium floodlights | Slam, sodium |
| Bow | **Lightfall** (`intro2-weapon-bow`) | 8.0 s / 6.2 s | an old shrine in a moonlit forest clearing | Assemble, gold |

Presentation only: no wire, save or rule change. Each is a pure function of the presentation clock (scrub-safe), allocation-free
per frame, and passes `Tests/IntroV2Checks.cs` (determinism, allocation, budgets, Reduced effects, the streak rule, cues).

## Files

- Core: `NativeIntroRanged.cs` (the shared base: the set's frame, the hands and the drawn muzzle, the camera for the effects, the
  streak rule's `Line`, a camera-plane `Star`, the muzzle flash), `NativeIntroSundown.cs`, `NativeIntroOneBreath.cs`,
  `NativeIntroBrassStorm.cs`, `NativeIntroLightfall.cs`. One registry line each in `NativeIntroCatalog.cs`.
- The sets: `tools/intros/ranged_set.py` (driver), `ranged_kit.py` (shared modelling on `tools/stages/stagekit.py`), and one module
  per set: `ranged_sundown.py`, `ranged_onebreath.py`, `ranged_brass.py`, `ranged_lightfall.py`.
- Sounds: `docs/audio/intros/I4.json` (55 keys, 2 takes each) and the I4 slot in `Core/NativeIntroSoundKeys.cs`.

## The set bank

`ChipArenaIntroRanged.bytes` (about 7.0 MB, Fine and Low tiers), never committed. Build it with Blender 5.2.3:

```
blender --background --factory-startup --python tools/intros/ranged_set.py -- --out <folder> [--sets sundown,onebreath,brass,lightfall] [--preview DIR]
```

It lands in `<folder>/ChipArenaLocal/Resources/`; copy it into `Assets/LocalAssets/ChipArenaLocal/Resources/` to see the sets. The
master is kept in `F:/Chip Arena Development/scratch/intro-ranged/bank/`. Without it, each intro draws the dark lit void tinted to its
palette.

- Everything is modelled in stagekit's Unity space with the pilot at the origin facing +z, and drawn turned by `Facing`; a set
  point (ux, uy, uz) is `NativeRangedIntro.S(ux, uy, uz)` in the source frame.
- The camera's far plane stays the game's 200 m, so every sky dome is 180 m and drawn round the eye's ground point (it never
  parallaxes). Sundown's mesas and the far desert, and One Breath's far city ring, are drawn there too. Far objects were brought
  inside the far plane at the same bearing and angular size.
- Small moving pieces go through `INativeIntroSet.Batch` (instanced): debris, casings, shards, leaves, the planted arrows.

## Sundown (pistol)

| t (s) | Beat |
| --- | --- |
| 0 | Low behind the pilot walking out toward the low sun; a tumbleweed crosses; the belfry at the end of the street |
| 1.15 | The bell tolls by itself (close profile as the head comes up) |
| 1.85 | It tolls again; its headstock cracks (long lens over the shoulder) |
| 2.25 | The hand by the hip (close), stillness |
| 2.8 | The belfry gives way: shingles and boards fall, the bell drops; the pilot draws from the hip (side-on wide) |
| 3.55 | One shot, slowed: the camera rides beside the bullet through the falling boards |
| 3.72 | It strikes the bell as it lands on the plinth: a ringing strike, sparks |
| 3.98–5.3 | The bell tips off the plinth, drops into the street and rolls to the boots (ground level) |
| 4.5–5.2 | The barrel up by the face; the smoke blown away |
| 6.0 | The name (Wipe), the bell rocking at the boots; its last ring |

Motion: the model plays `Armed-Walk-Slow`, `Armed-Unsheath-R-FromArmed-Hips`, `Armed-Idle-Pistol-R` and `Pistol-Attack-R1`; the keyed
rig drives the stillness, the smoke blow and the rest. No gun spin: the framework has no held-weapon turn (I0, 2026-11-20).

## One Breath (rifle)

| t (s) | Beat |
| --- | --- |
| 0 | A crane down through the rain onto the pilot knelt at the parapet, the rifle on the coping; lightning far off |
| 1.3 | Macro on the scope's lens: the drone's red eye in the glass, rain beading |
| 2.0 | From the drone's side, at 3 degrees: the scope glints; the drone's eye turns to it |
| 2.62 | Profile on the visor and the scope: one breath, held; the rain slows to a hang |
| 3.3 | The shot: the flash, the brake's jets, the hanging rain thrown back |
| 3.3–3.6 | The camera rides the round across the avenue between the towers into the eye (slowed) |
| 3.6 | The eye bursts; the drone falls burning between the towers |
| 4.72 | The spent case rings down on the wet coping and steams |
| 5.35–6.05 | The pilot rises, the rifle at the low carry (the owner's rest-low rule) |
| 6.4 | The name (Type) from outside the parapet, the beacon blinking behind |

Motion: the keyed rig with arm IK (`NativeEntrancePose.Reach`): the kneel, the right hand's bore on the drone, the left hand on the
fore-end, then the low carry. The model follows the keyed rig, so the drawn rifle points where the keys aim it.

## Brass Storm (machine gun)

| t (s) | Beat |
| --- | --- |
| 0 | Black; the belt clanks home |
| 0.45, 0.8, 1.15 | Three sodium floods bang on one after another, showing the yard (high wide crane) |
| 1.45 | The pilot steps up to the sandbags; 1.72 racks the handle (low front) |
| 1.95–4.35 | A 38-round burst traversing the shutter: tracers every third round, sparks, a drum knocked over (3.05), a crate burst (3.62); the brass pours out (slowed side dolly) and heaps round the boots (floor level); wide from the yard |
| 4.38 | Dry click; the barrel glows |
| 4.4–5.3 | The camera pushes back from the shutter through the drifting smoke and the rays of the work light shining through the holes |
| 6.0 | The name (Slam), the gun lowered, rays streaming past |

Motion: the keyed rig with arm IK: the low carry, the raise, the traverse (the bore follows each round's strike), a small kick per
round (a steady lean in Reduced effects), the rack, the lowering. Reduced effects: the muzzle burns steady instead of strobing, the
floods rise smoothly, no glints.

## Lightfall (bow)

| t (s) | Beat |
| --- | --- |
| 0 | A crane down through the canopy with the falling leaves onto the shrine; fireflies, mist |
| 1.15 | The nock from the quiver (over the shoulder) |
| 1.95 | The raise and draw toward the sky (a low orbit); the fireflies spiral into the arrowhead, the gate's glyphs wake |
| 2.75 | Full draw: the arrowhead's star (close); the leaves stop in the air |
| 3.25 | The release, slowed: a bolt of light climbs (low under the bow) |
| 3.72 | It blooms high above and splits (from the terrace's edge, looking up) |
| 4.12–5.2 | The volley of light rains round the shrine (high wide); each landing a burst of petals; the lanterns light |
| 4.9–5.45 | The bow comes down |
| 6.2 | The name (Assemble), the moon behind, the arrows standing in the moss |

Motion: the Bow's own arms (`NativeBowPose`) on the keyed rig, as Arrowfall's were; the bow, string, arrow and quiver are the match's,
drawn by the runtime from the intro's view (`INativeIntroBow`).

## Sound

Each intro is cues only (no bed). Every cue names a generated key (`intro2.<intro>.<cue>`), an existing bank key (`alt`) and a
built-in clip, so it plays synced before generation. Cues sit on the presentation clock, so the slowed beats (each machine-gun
round, the bullet's strike) slow with the picture. Until the batch is generated the existing sounds carry every cue; the quiet
ones use the nearest fits (the smoke blow `structure.whoosh`, the held breath `pve.ironworks.stoker.breath`, the case's steam
`chip.bomb.fuse-hiss`, the floods' warm-up `boss.hum.row.buzz`). Only the grove's night air is generated-key only (nothing existing
fits a quiet forest): silent until generated. The requests (55 keys, 110 takes, about 200 s) are batched by the framework's
`tools/intros/compile_sound_requests.py`.

## Rules kept

- Guns aim in the aim pose and rest low (the rifle and the machine gun end at the low carry).
- Streaks over a metre lie on their head's camera plane (`NativeRangedIntro.Line`, `NativeCacheOpenFx.OntoHeadPlane`): the bullet,
  the tracers, the light rays, the bolt of light, the volley.
- No ring outlines; no shake in Reduced effects (only the rig's handheld, dropped there); nothing flashes faster than 3 Hz there.
