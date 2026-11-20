# The melee weapon intros (intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!" New weapon intros replace the archived signatures of the sword, the
daggers, the shield, the greatsword, the Katana and the Fangs; the Glaive's is not built yet (it keeps Crescent Return until one
registers). The framework and its contract are I0's (`docs/INTROS-V2.md`). Presentation only: no wire, save or rule change.

Each is an authored scene: a story beat, a modelled set (scripted Blender, the local bank `ChipArenaIntroMelee`, never committed),
the model playing the packs' clips from the motion bank, an authored camera, timed light, effects, a sound cue timeline and the live
name card centred in the clean band under the pilot's feet in a full-length hero frame (`HeroFull`, `NameBelow`). Each differs from the
others in set, mood and camera.

| Weapon | Intro (`intro2-weapon-<spec>`) | Length / name | Set (bank prefix) | Mood | Card |
| --- | --- | --- | --- | --- | --- |
| Katana | Still Rain | 8.2 s / 6.6 s | `rain`: the Rain Garden, a walled courtyard at night | indigo, silver rain, one crimson | Wipe |
| Fangs | Apex | 7.4 s / 5.9 s | `canopy`: jungle temple ruins at night | toxic green in blue-black mist | Rise |
| Greatsword | Faultline | 8.0 s / 6.4 s | `rift`: a basalt quarry over a molten seam | ember on charcoal | Slam |
| Sword | Severed Dawn | 7.4 s / 5.9 s | `lightwell`: a ruined vaulted hall, one shaft of dawn | umber dark, white-gold shaft | Assemble |
| Shield | Hold the Line | 7.8 s / 6.3 s | `breach`: a broken fortress gate at dusk | smoke orange, steel blue | Slam |
| Daggers | Afterimage | 7.2 s / 5.8 s | `alley`: a night market lane with paper lanterns | teal and magenta neon | Rise |
| Glaive | (Orrery, not built) | | `observatory` (modelled, not banked) | | |

## How they are made

- **Code:** `Core/NativeIntroMelee.cs` (the shared base `NativeIntroMeleeBlade` and helpers) and one file per intro:
  `NativeIntroStillRain.cs`, `NativeIntroApex.cs`, `NativeIntroFaultline.cs`, `NativeIntroSeveredDawn.cs`, `NativeIntroHoldTheLine.cs`,
  `NativeIntroAfterimage.cs`. Each registers with one line on its slot in `Core/NativeIntroCatalog.cs`.
- **The body: bespoke keyed animation, no match clips** (owner, 2026-11-20: "we dont need the matches own annimation system. Think
  like the top dog and Bombardier ones, they have unique animations"). Each intro keys its own choreography on `NativeEntranceKeys`
  (key poses for the root, the body's drop and the joints, with planted feet and `Steps` for foot contacts; impact keys ring out, pass
  keys flow through), and the model follows the keyed rig (`Clip` returns false). The weapon moves on its own keyed path,
  `NativeIntroWield`: per key, where the hand holds it and which way the blade (or the shield's face) points, in the pilot's frame.
  `Arms` solves the arm onto it (`Wield`, `NativeEntrancePose.Reach`): the wrist at the grip, the blade along the hand's +z, the hand
  rolled toward the shoulder so the wrist stays natural. The match's grip holds the weapon on the drawn hand, so the weapon follows
  the keyed wrist. Still Rain's hands on the hilt and the sheathing are solved against the saya on the keyed pelvis
  (`NativeKatanaSaya`'s rest), and Apex's feet stand on the lintel's top by leg IK until the leap.
  - The intros derive from `NativeBladeIntro` (the blade entrances' path: the Katana's sheathing by `Sheathed`, the Fangs' and the
    Katana's blades handed in for their trails) and implement `INativeIntroV2` themselves.
  - The effects on a blade take the blade as drawn (`DrawnBlade`, `BladeSweep`): the drawn hand joint's frame the runtime hands in
    (`INativeIntroHands`) is the frame the weapon is drawn on, the tip `TipLength` along its +z. Afterimage's echoes are the keyed
    rig's ghosts, so they repeat the keyed moves a beat behind and their trails take the keyed tips.
  - Offline preview: the keyed poses can be dumped and drawn as stick figures from the front, the side and the intro's camera (the
    task's scratch tools `camdump pose` and `stick.py`, not in the repo).
- **Sets:** `tools/intros/melee_set.py` builds the bank from `melee_<set>.py` on `melee_kit.py` (the stage kit's finishes, tiers, AO
  and colour bakes). Props: `<set>/static` and `<set>/static/low`, and the moving parts as `<set>/<rig>` (`rain/bamboo-cut-0..2`,
  `rift/slab-0..13`, `breach/debris-0..3`, `alley/lantern-0..7`). Meshes are in Unity space with the pilot's spot at the origin;
  the scripts place them in the source frame (`NativeIntroMelee.U`).
  `blender --background --factory-startup --python tools/intros/melee_set.py -- --sets rain,canopy,rift,lightwell,breach,alley --out DIR`.
  Without the bank each intro draws a dark lit floor in its own light (`NativeIntroMelee.Plain`).
- **Sound:** each intro's cues name a generated key (`intro2.<intro>.<cue>`), an existing bank key and a built-in clip. The bank
  keys are filled one-shot keys (the installed bank leaves the `topdog.blade-*` and some ironworks keys empty, and loops such as
  `field.fire` do not play as cues; either way a cue falls to its built-in clip). The 59 requests are in `docs/audio/intros/I3.json`,
  their contract lines on the I3 slot of `Core/NativeIntroSoundKeys.cs`.

## The intros

### Still Rain (Katana)
Night in the Rain Garden. The pilot stands sheathed in the rain, head bowed, the left hand on the saya; far lightning lights the gate and
the head comes up. It sinks into the iai stance (the right foot forward, the right hand across to the hilt), and the one cut: the hips
snap round, the foot stamps, the arm goes out long to the right, and the rain stops in the air. In the frozen moment the camera rides the
cut's line (a plane rising toward the pilot's left) through the hanging drops, every drop on it split in two. Time comes back, the
halves fall apart, three bamboo stalks behind slide on the cut and fall onto the wall cap, the line breaks into falling light. The
chiburi flick; the sheathing (the hilt raised across the front, the tip set in the saya's mouth, the blade slid home along it); at the
click the rain is thrown off the pilot. Stillness under the name, the hand on the hilt. The rain and the freeze are the script's own clock
(`RainTime`); the bamboo stumps are cut on the same plane (`melee_rain.plane_y`).

### Apex (Fangs)
Night in the Canopy. The camera drifts through the ferns like prey; green eyes open on the gateway's lintel: the pilot, in a
predator's crouch there, leaning out over the edge, one fang down at the stone, venom beading on it. It coils, both fangs drawn back,
and leaps long out over the lens (slowed at the top, stretched out, fangs raised); both fangs go into the earth in a deep crouch in a
bloom of venom, leaves and mist. It rises slowly into a hunter's stance, fangs low, venom smoking off the tips. The keyed root carries it off the
3.2 m lintel (`Way`).

### Faultline (greatsword)
Night in the Rift. A low glide over the floor to the pilot on guard, the great sword upright; embers off the faults. It sinks as the
blade rises over its head, leaps, and comes over at the top of the leap to drive the blade into the floor ahead in a deep squash (slowed
into the blow), head bowed: the floor round it breaks into fourteen slabs that heave and tilt on a wave outward, molten light under the joints, cracks
racing to the walls, the quarry's seams flaring. The camera cranes up over the broken ring; the head comes up, and the pilot wrenches
the blade out and swings it onto its right shoulder, standing in the glow.

### Severed Dawn (sword)
A ruined hall at dawn. The camera comes down the shaft of light to the pilot at its edge, the sword low; the grip sets; the chin comes
up into the light. It coils to the right, chambers the blade over the shoulder and cuts through the light down past the left hip onto a
stepping foot, too fast to see (slowed only after it), and the shaft is severed: its lower part slides off along
the cut and breaks into falling flakes, the hall going dark but for the blade, which holds the light. A slower backhand draws a ribbon
of it across the front and pulls the flakes in; the blade is raised before the face in salute, and the dawn comes back under the name. The sun is a directional straight down through the cleft: the vault's
shadow makes the pool on the floor.

### Hold the Line (shield)
Dusk at the Breach. The pilot steps into the gap and plants the shield low; it looks up over the rim as the ridge launches. The guard
comes up, and five burning bolts burst on the shield in turn, each rocking it back on its heels before it drives into the guard again;
near misses throw up earth and the game's own fire. Silence, smoke, the face glowing hot; a bash off the back leg with a step blows the
smoke back, and it stands with the shield on its arm.

### Afterimage (daggers)
Lantern Alley at night. The pilot walks up the lane and stops; two echoes peel off to either side (teal and magenta), each a beat
behind. A backhand and a forehand slash, the echoes cutting after it and throwing their edges up through two lanterns' cords: the
lanterns drop and roll to the lane's sides. A lunge with both daggers driven out; the echoes fold back into the body as it crosses its
daggers.

## Captures and clips
`F:/Chip Arena Development/scratch/intro-melee/clips/` (local): per weapon the old intro beside the new at High in real time, and a
Low still strip.

## Status
Implemented on `claude/intro-melee` as a first pass of the bespoke keyed animation; not merged. **Paused for batch 2** (owner,
2026-11-20: the intros in batches for quality): the six are re-posed with the framework's pose tools (every-bone mapping, joint
limits, two-handed grip IK, canonical poses) before they are captured again. Known issues per intro: the notes'
`intro-melee-status.md`. The Glaive's Orrery is designed and its set modelled (`melee_observatory.py`), not built: its ring needs
its own wield path and catches, and the Glaive keeps Crescent Return until it registers.
