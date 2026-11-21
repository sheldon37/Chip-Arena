# The melee weapon intros (intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!" Then: "we dont need the matches own annimation system. Think like the top
dog and Bombardier ones, they have unique animations". New weapon intros replace the archived signatures of the sword, the daggers, the
shield, the greatsword, the Katana, the Glaive and the Fangs. The framework and its contract are I0's (`docs/INTROS-V2.md`). Presentation
only: no wire, save or rule change.

Each is an authored scene: a story beat, a modelled set (scripted Blender, the local bank `ChipArenaIntroMelee`, never committed), a body
keyed for the model it is drawn on, an authored camera, timed light, effects, a sound cue timeline, and the live name card centred in the
clean band under the pilot's feet in a full-length hero frame (`HeroFull`, `NameBelow`).

| Weapon | Intro (`intro2-weapon-<spec>`) | Length / name | Set (bank prefix) | Mood | Card |
| --- | --- | --- | --- | --- | --- |
| Katana | Still Rain | 8.2 s / 6.6 s | `rain`: the Rain Garden, a walled courtyard at night | indigo, silver rain, one crimson | Wipe |
| Fangs | Apex | 7.4 s / 5.9 s | `canopy`: jungle temple ruins by moonlight | toxic green in blue-green mist | Rise |
| Greatsword | Faultline | 8.0 s / 6.4 s | `rift`: a basalt quarry over a molten seam | ember on charcoal | Slam |
| Sword | Severed Dawn | 7.4 s / 5.9 s | `lightwell`: a ruined vaulted hall, one shaft of dawn | umber dark, white-gold shaft | Assemble |
| Shield | Hold the Line | 7.8 s / 6.3 s | `breach`: a broken fortress gate at dusk | smoke orange, steel blue | Slam |
| Daggers | Afterimage | 7.2 s / 5.8 s | `alley`: a night market lane with paper lanterns | teal and magenta neon | Rise |
| Glaive | Orrery | 7.6 s / 6.0 s | `observatory`: a summit observatory at twilight | dusk rose, brass, star-white | Assemble |

## How they are made

- **Code:** `Core/NativeIntroMelee.cs` (the base `NativeIntroMeleeBlade`, the wield paths and the hold) and one file per intro:
  `NativeIntroStillRain.cs`, `NativeIntroApex.cs`, `NativeIntroFaultline.cs`, `NativeIntroSeveredDawn.cs`, `NativeIntroHoldTheLine.cs`,
  `NativeIntroAfterimage.cs`, `NativeIntroOrrery.cs`. Each registers with one line on its slot in `Core/NativeIntroCatalog.cs`.
- **The body (batch 2, on the framework's pose tools; `docs/INTROS-V2.md` 9a-9c):**
  - Keyed with `NativeEntranceKeys` (no clips): hinged planted legs, the feet and drops in the model's metres (`ModelFeet`: `Start` and
    `Steps` are where the drawn ankles go; `ModelGround` for the Fangs' lintel), and Afterimage's walk on the framework's walk builder.
  - **The weapon on a keyed path** (`NativeIntroWield`): the grip and the weapon's axis (a blade's line, the shield's face, the Glaive's
    ring) per key, mostly in the keyed chest's frame (`InChest`: it rides the body's lean, drop and turn), in the keyed rig's proportions
    (its shoulders at (±.345, .18, 0) from the chest joint, the arm .52 long).
  - **The hold** (`HoldAt`): the arm solved as a body holds a weapon. The elbow is chosen round the shoulder-wrist line by a soft minimum
    over 24 poles (the forearm's roll, the upper arm's twist, the wrist's bend, the shoulder's ranges, the elbow out of the torso, the arm's
    rest), so it moves smoothly; the hand points along the forearm; the forearm carries at most 45 degrees of the hand's roll (the drawn
    forearm keeps the keyed twist only within 60), the wrist the rest. Two hands on a hilt share the turn (`TwoHands`), and the off hand on
    a two-hander is solved again after the framework's grip IK, free to roll round the hilt. The model's off fist is the runtime's grasp
    (`INativeIntroGrip`: the greatsword, the Katana's two-handed beats, the shield's rim).
  - **Contacts on the model** (`HoldDrawn`, `DrawnTarget`): where the drawn grip must meet the world (the great sword's point in the floor,
    the Katana's hands on the saya drawn on the model's pelvis, the Fangs in the earth, the Glaive's release and catch), the keyed wrist is
    put where the model's arm, hung from its own shoulder, reaches the same way; residual offsets measured on the model are aimed out.
    Blends between two holds blend their targets and solve once.
  - Reviewed offline as stick figures of the model as drawn (front, side, three-quarters, the intro's camera) with a blade-through-body
    report, in the task's scratch tools (not in the repo).
- **Sets:** `tools/intros/melee_set.py` builds the bank from `melee_<set>.py` on `melee_kit.py` (the stage kit's finishes, tiers, AO
  and colour bakes): `<set>/static`, `<set>/static/low` and the moving parts as `<set>/<rig>` (`rain/bamboo-cut-0..2`, `rift/slab-0..13`,
  `breach/debris-0..3`, `alley/lantern-0..7`, `observatory/arm-ring-0..3`, `observatory/ringblade`). Meshes are in Unity space with the
  pilot's spot at the origin; the scripts place them in the source frame (`NativeIntroMelee.U`).
  `blender --background --factory-startup --python tools/intros/melee_set.py -- --out DIR` (all seven sets, 11.9 MB).
  Without the bank each intro draws a dark lit floor in its own light (`NativeIntroMelee.Plain`).
- **Sound:** each cue names a generated key (`intro2.<intro>.<cue>`), an existing bank key and a built-in clip. The 74 requests are in
  batch 2's rows, `docs/audio/intros-b2/I3.json`, compiled into `docs/audio/sound-requests-intros-b2.json` (148 takes, 273 s), their
  contract lines on the I3 slot of `Core/NativeIntroSoundKeys.cs`. No queue job is written here (the integrator's, at release).
- **Checks:** `Tests/IntroMeleeChecks` (`--intro-melee`, and in the full suite): the seven on their slots; on the model as the runtime binds
  it, no drawn weapon through the drawn head or torso, the great sword's point in the floor and resting on it, the saya in the Katana's left
  hand and the blade along it in the sheathing, the Fangs on the lintel, the Glaive's ring leaving the drawn hand and coming back to it,
  the orrery's rings over the floor. `IntroV2Checks` and `--intro-limits` hold the rest.

## The intros

### Still Rain (Katana)
Night in the Rain Garden. The pilot stands sheathed in the rain, head bowed, the left hand on the saya; far lightning lights the gate and
the head comes up. It sinks into the iai stance (the right foot forward, the right hand across to the hilt), and draws: the hips snap round,
the foot stamps, the arm goes out long to the right, and the rain stops in the air. In the frozen moment the camera rides the cut's line
through the hanging drops, every drop on it split in two. Time comes back and the bamboo stalks behind slide on the cut and fall; the left
hand joins the right on the hilt and the sword goes up and down in one cut (kirioroshi), the chiburi flicks the rain off the blade, the
blade is sheathed along the saya, and at the click the rain is thrown off the pilot. Stillness under the name.

### Apex (Fangs)
Moonlight in the Canopy. The camera drifts through the ferns like prey; green eyes open on the gateway's lintel: the pilot, crouched there
on its feet (the model's feet on the lintel's top), one fang down at the stone. It cocks both claws by its shoulders and leaps long out over
the lens, the legs trailing, the fangs raised and spread; its feet find the floor a moment before both fangs go into the earth before them
in a bloom of venom, leaves and mist. It rises into a hunter's stance, the fangs low with their points forward, venom smoking off them.

### Faultline (greatsword)
Night in the Rift. A low glide over the floor to the pilot in a middle guard, the great sword in both hands. It sinks as the blade rises and
tips back over its right shoulder, leaps, and chops it over and down; the point bites the floor far ahead and the blow's shock throws the
left hand off the hilt (out for balance) as it lands in a deep squash: the floor round it breaks into fourteen slabs that heave and tilt
on a wave outward, molten light under the joints, cracks racing to the walls. The head comes up; it rises, wrenches the blade out and swings
it down to its right side, its point set on the floor ahead, and stands beside it in the glow (before, the shoulder rest read as raised
behind the head).

### Severed Dawn (sword)
A ruined hall at dawn. The camera comes down the shaft of light to the pilot at its edge, the sword low; the grip sets; the chin comes up
into the light. It coils, chambers the blade over its right shoulder and cuts down through the light onto a lunging step, and the shaft is
severed: its lower part slides off along the cut and breaks into falling flakes, the hall going dark but for the blade, which holds the
light. A slower backhand draws a ribbon of it across the front; a brief salute, and the blade is lowered out to the right under the name
(before, the salute's hilt hid the visor).

### Hold the Line (shield)
Dusk at the Breach. The pilot steps into the gap and plants the shield low; it looks up over the rim as the ridge launches. The shield
comes up before its chest, the left fist bracing its rim, and five burning bolts burst on it in turn, each rocking it back; near misses
throw up earth and the game's own fire. Silence, smoke, the face glowing hot; a bash off the back leg with a step blows the smoke back, and
it stands with the shield lowered at its side, the face clear under the name (before, the shield hid it, and the guard over the head read
as cover from rain).

### Afterimage (daggers)
Lantern Alley at night. The pilot walks up the lane on the model's feet and stops; two echoes peel off to either side (teal and magenta),
each a beat behind. It crosses its daggers in two backhand cuts, the right and then the left, the echoes cutting after it and throwing their
edges up through two lanterns' cords; a lunge drives both daggers out; the echoes fold back into the body as it crosses its daggers before
its chest, and it stands with them low at its sides, points forward (before, one was held out sideways under the name).

### Orrery (Glaive)
Twilight in the Observatory, an open stone observatory on a mountain summit. Four great armillary rings hang level over the pilot, turning
slowly, their inlays lit. The camera comes down through them to the pilot on the dais, the ring blade at its side. It winds the ring across
its body and throws it backhand: the ring ricochets round the orrery, striking each armillary ring in turn (each rings, its inlay flaring,
and swings out of level), and comes back round behind the pilot to the hand, caught high. At the catch the four rings swing into an
armillary sphere round it and lock with a chime, starlight falls through the oculus onto the dais, and it stands under the name with the
ring at its side, facing out. While the ring flies the held ring is not drawn (`Sheathed`) and the set's ring blade flies on its banked,
spinning path.

## Captures and clips
`F:/Chip Arena Development/scratch/intro-melee/clips/b2/` (local): per weapon the intro at High in real time, the old intro beside the new,
a Low still strip, the drawn pilot from the side and at three-quarters at its beats, and a sheet of its High frames.

## Status
Batch 2 (the owner's batches "if this will improve the quality (Hopefully dramatically)") on `claude/intro-melee`: all seven re-keyed on
the framework's pose tools, 0 `--intro-limits` findings; not merged. The integrator reviews the clips, merges, and packs the bank.
