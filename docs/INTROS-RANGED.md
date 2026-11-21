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

Each name card sits in clean space beside or below the pilot (`card.X`/`Y`): Sundown low on the left over the street, One Breath on
the parapet's brick band below the pilot, Brass Storm beside the pilot in the dark over the containers, Lightfall beside the pilot in
the night sky under the moon.

Presentation only: no wire, save or rule change. Each is a pure function of the presentation clock (scrub-safe), allocation-free
per frame, and passes `Tests/IntroV2Checks.cs` (determinism, allocation, budgets, Reduced effects, the streak rule, cues).

## Files

- Core: `NativeIntroRanged.cs` (the shared base: the set's frame, the hands and the drawn muzzle, the camera for the effects, the
  streak rule's `Line`, a camera-plane `Star`, the muzzle flash), `NativeIntroSundown.cs`, `NativeIntroOneBreath.cs`,
  `NativeIntroBrassStorm.cs`, `NativeIntroLightfall.cs`. One registry line each in `NativeIntroCatalog.cs`.
- The sets: `tools/intros/ranged_set.py` (driver), `ranged_kit.py` (shared modelling on `tools/stages/stagekit.py`), and one module
  per set: `ranged_sundown.py`, `ranged_onebreath.py`, `ranged_brass.py`, `ranged_lightfall.py`.
- Sounds: `docs/audio/intros-b2/I4.json` (batch 2's rows: 55 keys, 72 takes) and the I4 slot in `Core/NativeIntroSoundKeys.cs`.
- Checks: `Tests/IntroRangedChecks.cs` (`--intro-ranged`), with authoring probes: `--intro-ranged-dump ID OUT` (the drawn body, its
  weapon and the camera every 1/30 s, for stick-figure previews), `--intro-ranged-limits`, `--intro-ranged-fit KIND` and
  `--intro-ranged-twist ID`.

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

Motion (batch 2): bespoke keyed animation on the model (owner, 2026-11-20: "we dont need the matches own annimation system"),
`ModelFeet` on. The walk is the framework's on the model (`NativeIntroPoses.Walk` along a `NativeIntroFeet.Track`: a stride of about
0.56 m every 0.56 s, each contact and passing keyed, the hips turning with the leading leg, the chest countering, the off arm
swinging), already under way at the first frame and stopping with the feet together; the head lifts to the belfry at the first
toll. The gun hand is keyed in `Arms` in the chest's frame (`NativeIntroGrip.Hold`): low by the right thigh, muzzle down, swinging a
little with the walk; whipped up to the high ready by the shoulder (barrel to the sky) as the knees give and the hips blade 24°
off the bell's line; snapped down onto the falling bell along a straight arm, the muzzle tracking it down to the strike; the shot's
flip through the wrist. The aim's line runs from the drawn muzzle (`ShotFrom`) to the strike, so the drawn bore lies on the bullet's
path. The Standard pistol is long (0.5 m to its crown), so the blow holds the barrel up beside the right cheek with the muzzle over
the brow (searched with `--intro-ranged-fit blow`: the wrist inside its range, the hand out of the torso). One-handed throughout:
a western duelist's shot, so no support grip.

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

Motion (batch 2; owner on the first version: "arms looked really odd": a limp front hand, tangled arms, crossed arms at the end):
a high kneel on the model (`ModelKneel`: the right knee on the floor, the left foot planted ahead), square to the drone, the torso
bladed as the match aims a long gun (`NativeIntroPoses.RifleAim`), the neck bowed and the head tilted back level and rolled onto the
stock, so the visor sits at the scope's eyepiece (searched with `--intro-ranged-fit kneel`). The main hand is the long guns' shouldered
hold (`NativeRangedIntro.ShoulderGun`: the match's long-gun hold with the trigger elbow down and out, below), the off fist on the
handguard by the grip IK (`GripAt`), the stock in the drawn shoulder (`Shoulder` 1). The fore-end lies over the coping. One breath (the chest filling, the clavicle settling), the shot's kick rocking the
body back. The rise: the weight over the front foot, the knee lifting, the back foot stepping up beside it as the hips rise, the
rifle down from the shoulder to the low carry (the owner's rest-low rule), the off hand still on the handguard. The camera's lens,
scope and shot frames and the spent case's port are on the drawn rifle (`AimWrist`; the lens .055 over the weapon frame, as hold 7's
macro shows it in the glass).

**The trigger elbow (owner 2026-11-21 on Brass Storm, "Arm doing weird things on machine gun intro again"; One Breath had the same, milder):**
the match's long-gun hold (`NativeIntroPoses.AimRifle`, the wrist under the cheek) keys a straight trigger arm on the keyed rig's wide
shoulders, so when the stock goes into the drawn shoulder the drawn elbow has no side to keep and lands level with the shoulder (the
upper arm raised 90-100°): a chicken wing. `ShoulderGun` brings the keyed wrist back, down and out toward the shoulder (−.08, −.12, −.14
in the chest's frame) so the keyed arm bends with its elbow down and out, and the drawn arm keeps that side: the drawn trigger elbow
.13-.18 m under the shoulder, the upper arm 50-60° off hanging, through the whole aim.

## Brass Storm (machine gun)

| t (s) | Beat |
| --- | --- |
| 0 | Black; the belt clanks home |
| 0.45, 0.8, 1.15 | Three sodium floods bang on one after another, showing the yard (high wide crane) |
| 1.45 | The pilot steps up to the sandbags; 1.72 racks the handle (low front) |
| 1.95–4.35 | A 38-round burst walked in one sweep: right across the shutter to the drum (knocked over, 2.66, seen from behind it), back across the shutter and the wall to the crate (burst, 3.92, in the foreground of a wide from the yard); tracers every third round, sparks; the brass pours out (slowed side dolly) and heaps round the boots (floor level) |
| 4.38 | Dry click; the barrel glows |
| 4.4–5.3 | The camera pushes back from the shutter through the drifting smoke and the rays of the work light shining through the holes |
| 6.0 | The name (Slam), the gun lowered, rays streaming past |

Motion (batch 2): `ModelFeet` on. In the dark the gun at the low carry, the head snapping to each flood as it bangs on (left, right,
then up the yard). The step in to the bags (the left foot forward, the stance of the canonical machine-gun aim), the gun up into the
shoulder as the off hand racks the handle (the grip IK's `Along` slides it back along the gun and home). The burst: RifleAim's blade
with the cheek on the stock (`--intro-ranged-fit stand-mg`), the knees soft and the weight in the gun. The trigger arm is
`ShoulderGun`'s (the right elbow down and a little out under the shoulder, the stock in the shoulder pocket, the forearms along the
gun; the owner's "Arm doing weird things" was the canonical hold's chicken wing, the elbow up by the helmet), the off hand on the
handguard. The torso and the hips carry the sweep (the hips three quarters, keyed on its turns; the spine and the chest the rest), so
the arms keep their hold; each round's aim is from the drawn grip as the body swings it, so every round leaves the drawn bore onto its
strike (`Tests/IntroRangedChecks`: within 2.4°); a small kick per round (a steady lean in Reduced effects). The rack slides the off
hand .2 m back along the gun (further put the off elbow in the chest with the gun held in at the shoulder).
Dry: a beat on the stock, then the head up and the gun down to the carry. Reduced effects: the muzzle burns steady instead of
strobing, the floods rise smoothly, no glints.

## Lightfall (bow)

| t (s) | Beat |
| --- | --- |
| 0 | A crane down through the canopy with the falling leaves onto the shrine; fireflies, mist |
| 1.15 | The bow comes up before the body, the arrow already on the string; the string hand meets the string at the set (1.71-1.85) |
| 1.95 | Side-on to the shot, the draw toward the sky (a low orbit); the fireflies spiral into the arrowhead, the gate's glyphs wake |
| 2.75 | Full draw: the arrowhead's star (close); the leaves stop in the air |
| 3.25–4.1 | The release, slowed, in profile from the chest's side at the head's height: the string hand flies back past the ear, a bolt of light climbs out of frame and blooms high above (3.72), its light on the archer |
| 4.12–5.2 | The volley of light rains round the shrine (high wide); each landing a burst of petals; the lanterns light |
| 4.9–5.45 | The bow comes down |
| 6.2 | The name (Assemble), the moon behind, the arrows standing in the moss |

Motion (batch 2): `ModelFeet` on. The draw is the canonical bow draw (`NativeIntroPoses.BowDraw`: the root a quarter turn right of
the shot, the feet across the line), the Bow's own arms (`NativeBowPose`, procedural) with the string hand on the nock by the grip IK
(`GripAt`, its `Pull` the bow view's). On the model the bow arm is kept straight (the bow's full extension) and the string drawn to
0.6, which puts the string hand at the drawn jaw (`Tests/IntroRangedChecks`: under 0.25 m from the eyes, the bow elbow under 25°); the
canonical 0.55 with its matching extension bent the drawn bow arm 120°. The string elbow (owner 2026-11-21: "check that bow one again"):
the canonical bow pole (back and up) took the drawn elbow 0.25 m over the shoulder at this 50° sky shot, the forearm through the helmet
to the jaw, and the raised arm across the face in the low full-draw shot. `DrawElbow` (−.8, −.2, −.55 in the leaned, side-on chest's
frame: back along the arrow, a little down and behind) puts the drawn elbow on the arrow's line at the shoulder's height (at most
+0.04 m through the draw), the forearm beside the head to the jaw; the grip takes the string hand over the hook's last 30%, so the
drawn elbow arrives on its side without a flip. The arrow is on the string from the start (the bow hand's
finger keeps it on the shelf, as the match's carry): the reach to the back quiver kept twisting the keyed arm past its limits, and a
v2 bow intro draws no arrow in the hand. The string hand hooks the string on an arc outside the body as the bow comes up, and after the
release it flies back straight on its forearm. The bow, string, arrow and quiver are the match's, drawn by the runtime from the
intro's view (`INativeIntroBow`). The slowed release (3.22-3.6 s, about 3.4-4.2 s of the clip) is seen in profile from the chest's side
at the head's height (owner 2026-11-21: the low shot from behind under the shoulders read the head and the arms oddly), held through
the bloom (a shot up at the bloom from under the canopy showed only leaves).

## The arms as they read (all four; owner 2026-11-21)

`--intro-limits` passed while the machine gun's and the bow's arms looked wrong, so `Tests/IntroRangedChecks` also reads every arm
of each intro every 1/30 s through its own shot camera (`--intro-ranged-arms ID [FROM TO]` prints the measures per frame):
- **no arm through the head:** the drawn arm's bone lines stay out of the drawn head's ellipsoid (NativeVisualRig's, the one fists and
  weapons are kept out of) widened by 3 cm;
- **no arm across the face:** in the shot camera, no arm in front of the face covers more than 15% of the face's disc (0.11 m round
  the drawn eyes);
- **the aiming elbows:** a long gun's trigger elbow at least 8 cm under its shoulder while the gun is shouldered (no chicken wing); the
  pistol's elbow within 5 cm over its shoulder and 4 cm over its shoulder-wrist line through the aim (a straight arm); the bow's string
  elbow under 10 cm over its shoulder through the draw (on the arrow's line, not over the head).
On the batch-2 poses of hold 7 these fail (One Breath's trigger elbow +0.07 m, Brass Storm's level with the shoulder, Lightfall's
forearm through the helmet); on the reworked ones every arm passes (the trigger elbows −0.14 and −0.15 m, the string elbow +0.04 m).

## Sound

Each intro is cues only (no bed). Every cue names a generated key (`intro2.<intro>.<cue>`), an existing bank key (`alt`) and a
built-in clip, so it plays synced before generation. Cues sit on the presentation clock, so the slowed beats (each machine-gun
round, the bullet's strike) slow with the picture. Until the batch is generated the existing sounds carry every cue; the quiet
ones use the nearest fits (the smoke blow `structure.whoosh`, the held breath `pve.ironworks.stoker.breath`, the case's steam
`chip.bomb.fuse-hiss`, the floods' warm-up `boss.hum.row.buzz`). Only the grove's night air is generated-key only (nothing existing
fits a quiet forest): silent until generated. The requests are batch 2's (`docs/audio/intros-b2/I4.json`: 55 keys, 72 takes, 131.7 s;
two takes for the hero sounds and anything heard many times, one for the small cues and the beds), compiled into
`docs/audio/sound-requests-intros-b2.json` by `tools/intros/compile_sound_requests.py --batch b2`; the integrator writes the job.

## Pack clips (not used yet)

Every beat is keyed. Candidates if a beat reads cartoonish, from `intro-pack-clips-index.md` (none of them a clip the match plays):
Sundown's walk `Frank_RPG_Gunslinger_Walk` (legs and torso, the gun hand keyed over it), One Breath's rise `2Hand-Shooting-Getup1`
or the kneel's `2Hand-Shooting-Crouch-Idle`. They would go in `ChipArenaIntroMotionRanged.bytes` through the framework's bake.

## Rules kept

- Guns aim in the aim pose and rest low (the rifle and the machine gun end at the low carry, off the shoulder).
- Every intro inside the anatomical limits, its drawn feet on the floor and its grips on (`--intro-limits`: 0 findings).
- Streaks over a metre lie on their head's camera plane (`NativeRangedIntro.Line`, `NativeCacheOpenFx.OntoHeadPlane`): the bullet,
  the tracers, the light rays, the bolt of light, the volley.
- No ring outlines; no shake in Reduced effects (only the rig's handheld, dropped there); nothing flashes faster than 3 Hz there.
