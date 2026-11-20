# The melee weapon intros (intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!" New weapon intros replace the archived signatures of the sword, the
daggers, the shield, the greatsword, the Katana and the Fangs; the Glaive's is not built yet (it keeps Crescent Return until one
registers). The framework and its contract are I0's (`docs/INTROS-V2.md`). Presentation only: no wire, save or rule change.

Each is an authored scene: a story beat, a modelled set (scripted Blender, the local bank `ChipArenaIntroMelee`, never committed),
the model playing the packs' clips from the motion bank, an authored camera, timed light, effects, a sound cue timeline and the live
name card beside the pilot in the hero frame. Each differs from the others in set, mood and camera.

| Weapon | Intro (`intro2-weapon-<spec>`) | Length / name | Set (bank prefix) | Mood | Card |
| --- | --- | --- | --- | --- | --- |
| Katana | Still Rain | 8.2 s / 6.6 s | `rain`: the Rain Garden, a walled courtyard at night | indigo, silver rain, one crimson | Wipe, right |
| Fangs | Apex | 7.4 s / 5.9 s | `canopy`: jungle temple ruins at night | toxic green in blue-black mist | Rise, right |
| Greatsword | Faultline | 8.0 s / 6.4 s | `rift`: a basalt quarry over a molten seam | ember on charcoal | Slam, left |
| Sword | Severed Dawn | 7.4 s / 5.9 s | `lightwell`: a ruined vaulted hall, one shaft of dawn | umber dark, white-gold shaft | Assemble, right |
| Shield | Hold the Line | 7.8 s / 6.3 s | `breach`: a broken fortress gate at dusk | smoke orange, steel blue | Slam, right |
| Daggers | Afterimage | 7.2 s / 5.8 s | `alley`: a night market lane with paper lanterns | teal and magenta neon | Rise, right |
| Glaive | (Orrery, not built) | | `observatory` (modelled, not banked) | | |

## How they are made

- **Code:** `Core/NativeIntroMelee.cs` (the shared base `NativeIntroMeleeBlade` and helpers) and one file per intro:
  `NativeIntroStillRain.cs`, `NativeIntroApex.cs`, `NativeIntroFaultline.cs`, `NativeIntroSeveredDawn.cs`, `NativeIntroHoldTheLine.cs`,
  `NativeIntroAfterimage.cs`. Each registers with one line on its slot in `Core/NativeIntroCatalog.cs`.
- **The body:** every melee intro derives from `NativeBladeIntro`, whose `Clip` the runtime lays on the model (the blade entrances'
  path), and implements `INativeIntroV2` itself (the camera rig, ramps, set, card, lens, cues, as `NativeIntroV2Script`).
  - The Katana and the Fangs hold their blades as the match does (`NativeBladeGrip`), and the runtime hands their blades in
    (`Blade`), so their trails follow the drawn blades.
  - The sword, daggers, shield and greatsword have no such grip. The visual body takes a weapon's hold on the first frame its clip
    plays (the game hand against the model's hand). So these intros open on their set's idle while the keyed rig plays the game's own
    capture of that idle (`Core/CapturedClips`, `NativeIntroMeleeBlade.Captured`): both rigs are on one pack clip, as in the match, so
    the hold is the match's. Where a capture of the clip exists (the cuts, the block, the walk), the keyed rig follows it all through,
    which gives the effects the blade's tip (`Anchor RightTip`, `TipLength` the weapon's) and gives Afterimage its echoes (the keyed
    rig's ghosts are the model, posed a moment behind).
  - Limit: a viewer whose first frame of the intro comes in the middle of a clip with no capture (a late joiner during the greatsword's
    plunge) takes a hold from mismatched poses.
- **Sets:** `tools/intros/melee_set.py` builds the bank from `melee_<set>.py` on `melee_kit.py` (the stage kit's finishes, tiers, AO
  and colour bakes). Props: `<set>/static` and `<set>/static/low`, and the moving parts as `<set>/<rig>` (`rain/bamboo-cut-0..2`,
  `rift/slab-0..13`, `breach/debris-0..3`, `alley/lantern-0..7`). Meshes are in Unity space with the pilot's spot at the origin;
  the scripts place them in the source frame (`NativeIntroMelee.U`).
  `blender --background --factory-startup --python tools/intros/melee_set.py -- --sets rain,canopy,rift,lightwell,breach,alley --out DIR`.
  Without the bank each intro draws a dark lit floor in its own light (`NativeIntroMelee.Plain`).
- **Sound:** each intro's cues name a generated key (`intro2.<intro>.<cue>`), an existing bank key and a built-in clip. The 59
  requests are in `docs/audio/intros/I3.json`, their contract lines on the I3 slot of `Core/NativeIntroSoundKeys.cs`.

## The intros

### Still Rain (Katana)
Night in the Rain Garden. The pilot stands sheathed in the rain, far lightning lights the gate; it sinks into the iai stance, the thumb
pushes the guard, and the one cut (the pack's stance-three draw): the rain stops in the air. In the frozen moment the camera rides the
cut's line (a plane rising toward the pilot's left) through the hanging drops, every drop on it split in two. Time comes back, the
halves fall apart, three bamboo stalks behind slide on the cut and fall onto the wall cap, the line breaks into falling light. The
sheathing; at the click the rain is thrown off the pilot. Stillness under the name. The rain and the freeze are the script's own clock
(`RainTime`); the bamboo stumps are cut on the same plane (`melee_rain.plane_y`).

### Apex (Fangs)
Night in the Canopy. The camera drifts through the ferns like prey; green eyes open on the gateway's lintel: the pilot, crouched there
(the stealth idle), venom beading on the fangs. It coils and leaps (the rogue lunge, slowed at the top of its arc) at the lens; both
fangs go into the earth in a bloom of venom, leaves and mist. It rises, venom smoking off the tips. The keyed root carries it off the
3.2 m lintel (`Way`).

### Faultline (greatsword)
Night in the Rift. A low glide over the floor to the pilot with the great sword; embers off the faults. The plunging slam (slowed into
the blow): the floor round it breaks into fourteen slabs that heave and tilt on a wave outward, molten light under the joints, cracks
racing to the walls, the quarry's seams flaring. The camera cranes up over the broken ring; the pilot rises in the glow.

### Severed Dawn (sword)
A ruined hall at dawn. The camera comes down the shaft of light to the pilot at its edge; the hand tightens on the grip; the light
reaches the visor. One forehand cut, too fast to see (slowed only after it), and the shaft is severed: its lower part slides off along
the cut and breaks into falling flakes, the hall going dark but for the blade, which holds the light. A slower backhand draws a ribbon
of it and pulls the flakes in; the dawn comes back under the name. The sun is a directional straight down through the cleft: the vault's
shadow makes the pool on the floor.

### Hold the Line (shield)
Dusk at the Breach. The pilot plants the shield in the gap; from behind it the ridge launches. Five burning bolts burst on the shield in
turn (the block's hit each time), near misses throw up earth and the game's own fire; the feet never move. Silence, smoke, the face
glowing hot; one bash blows the smoke back.

### Afterimage (daggers)
Lantern Alley at night. The pilot walks up the lane and stops; two echoes peel off to either side (teal and magenta), each a beat
behind. It cuts and the echoes cut after it, throwing their edges up through two lanterns' cords: the lanterns drop and roll. A second
exchange; the echoes fold back into the body as it crosses its daggers.

## Captures and clips
`F:/Chip Arena Development/scratch/intro-melee/clips/` (local): per weapon the old intro beside the new at High in real time, and a
Low still strip.

## Status
Implemented on `claude/intro-melee`; not merged. The Glaive's Orrery is designed and its set modelled (`melee_observatory.py`), not
built: its ring has no capture of the game's to set the hold from, and its match hold is a keyed solver on the model.
