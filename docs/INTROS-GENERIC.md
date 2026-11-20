# The five generic intros (Intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well! Lets generate... 5 new intros". These five replace the archived
generic entrances (Hero Landing, Orbital Drop, Grid Rez, Holo Projection, Blackout, Skyline Zip, Overclock). Any pilot can pick
them: they are weapon-agnostic, the hands stay empty and the body plays the model's motion bank, layered in code.

The contract they are built on is `docs/INTROS-V2.md` (agent I0, `claude/intro-framework`). This page is the authored design:
the story, the set, the camera's language, the beats, the motion, the effects and the sound of each.

| Intro | Mood | Arrival | Set | Camera language |
| --- | --- | --- | --- | --- |
| **Cloudbreak** | awe, dawn gold | from above: a re-entry streak, a flip and a thruster-braked touchdown | a summit relay station above a sea of cloud | vertical: a look up, a long-lens fall-follow, a slow-motion touchdown, a low hero |
| **Whiteout** | cold mystery, then warmth | out of the white: a frosted figure kneeling in a blizzard thaws and rises | a polar outpost at night, its floodlights in the snow | telephoto: one slow push into the storm, a close insert, a reveal as the storm parts |
| **Slipstream** | speed, neon | travel: riding a maglev's roof out of a tunnel, a leap and a sliding stop | an underground maglev station | kinetic: on the roof and beside the train keeping pace, a cut to the platform for the leap, a slide into the lens |
| **Waygate** | ceremony, dusk | a summons: an ancient ring gate wakes, opens, and the pilot floats out of it | a canyon plaza with a carved stone ring gate | formal: a centred push-in, an arc round it as it comes through, a centred hero framed by the ring |
| **Flashbulb** | glamour, swagger | a showstopper: a stride down a red carpet, three poses frozen by photographers' flashes | a theatre's red carpet at night under a chasing marquee | the dolly ahead of a walk, then three photographs (held poses from three angles, each lit by its flash) and a cover shot |

**The default:** Cloudbreak (see "The default" below).

## Shared rules
- **Timing:** 7.4-8.2 s each, the name beat between 5.6 and 6.4 s; the pilot holds a hero pose under the name.
- **Weapon-agnostic:** no weapon in the hands (the registry gives them no weapon spec, so nothing is drawn in them).
- **Body:** keyed on the intro rig (`NativeEntranceKeys` and planned steps, as Top Dog and Last Warning are) and drawn on the model;
  where a pack clip fits the beat the model plays it from the motion bank, retimed in code (`Clip`, docs/INTROS-V2.md section 2):
  Flashbulb's stride is `Armed-Walk` at 1.33x and Slipstream's run along the roof `Unarmed-Sprint` at 1.45x. The keyed rig stays
  the camera's and the effects' anchors and the Classic body's pose. No new pack clips were needed.
- **Sets:** each modelled in Blender 5.2.3 (headless) by its own script (`tools/intros/generic_<set>.py`, run by
  `tools/intros/generic_sets.py`, shapes from `generic_kit.py` on stagekit), two tiers (Fine, `/low`), into the local bank
  `ChipArenaIntroGeneric.bytes` (6.4 MB; never committed). Every far piece (sky domes, peaks, clouds, canyon) lies inside the
  game's 200 m far plane. Without the bank each intro draws its own plain fallback (a floor far wider than its fog, a soft pool,
  a horizon glow) in its own light, never blocks.
- **Effects:** `NativeVfxDraw` (glows, streaks, smoke); a streak longer than 1 m is laid on its head's camera plane
  (`NativeIntroGenericScript.Line`); no ring outlines (shockwaves are dust, snow and steam); nothing flashes faster than 3 Hz in
  Reduced effects and there is no camera shake there.
- **Sound:** each intro has a cue timeline on its beats, every cue with an existing bank key and a built-in clip as stand-ins.
  The generated sounds are requested in `docs/audio/intros/I2.json` (49 keys, 82 takes, 128 s of generation), their contract
  lines in `Core/NativeIntroSoundKeys.cs`; the framework compiles every author's rows into the builder's queue.

## Where it lives
| Part | Files |
| --- | --- |
| The intros | `Core/NativeIntroGeneric.cs` (the shared base: the set's frame, long streaks, the plain fallback), `Core/NativeIntroCloudbreak.cs`, `NativeIntroWhiteout.cs`, `NativeIntroSlipstream.cs`, `NativeIntroWaygate.cs`, `NativeIntroFlashbulb.cs` |
| Registry | `Core/NativeIntroCatalog.cs`: Cloudbreak on the default's slot (`intro2-default`), the other four on generic slots 1-4 (`intro2-whiteout`, `intro2-slipstream`, `intro2-waygate`, `intro2-flashbulb`) |
| Sets | `tools/intros/generic_sets.py` and its modules; the bank's master copy in `F:/Chip Arena Development/scratch/intro-generic/bank/` |
| Sound | `docs/audio/intros/I2.json`, `Core/NativeIntroSoundKeys.cs` (the I2 slot) |
| Checks | `Tests/IntroGenericChecks.cs` (`CoreTests --intro-generic`), and the framework's `Tests/IntroV2Checks.cs` (`--intros-v2`) |

To rebuild the bank: `blender --background --factory-startup --python unity/ChipArenaConversion/tools/intros/generic_sets.py --
--out DIR [--preview DIR]` (about 25 s with occlusion), then copy `DIR/ChipArenaLocal/Resources/ChipArenaIntroGeneric.bytes`
and its `.meta` into `Assets/LocalAssets/ChipArenaLocal/Resources/`.

## Cloudbreak (7.6 s, the name at 5.7 s)
Dawn above the clouds. A summit pokes out of a sea of cloud; a relay mast blinks red on it. A boom, and a white-hot point falls out
of the sky on a contrail: the pilot in a head-first dive, glowing from the re-entry. It flares, tucks and flips feet first, its boots'
thrusters fire a braking burn and it settles onto the summit's pad in slow motion; the wash blows grit and snow across the pad and
snaps the windsocks out. It rises as the sun breaks the cloud line behind it and floods the pad with gold.

**The set (summit relay):** a craggy faceted rock summit falling away to the cloud sea, snow on its ledges; an octagonal concrete
landing pad with a hazard-striped edge, a ring, three chevrons, a touchdown mark and eight edge lights; a red-and-white lattice relay
mast with two dish antennas, platforms and a red beacon; a corrugated equipment hut with a door, a vent and a stack; a solar array on
the slope; a yellow railing; two windsock poles (their socks turn in the wind); four far snow peaks; the cloud sea and far cumulus;
the dawn sky and the sun.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The pad at dawn, the beacon blinking | low on the pad, looking up past the mast at the sky and the horizon, creeping in |
| 0.35 | A distant boom | (holds) |
| 0.45 | The streak: a white-hot head, a contrail laid on the camera's plane, smoke hanging along it | (rising) |
| 1.30 | The dive: head first, the shoulders' glow cooling, wind streaming past, mist peeling off | a long lens from the pad's edge, following the fall |
| 2.20 | The summit rushing up | falling alongside it |
| 2.45 | The flare (belly to the pad, arms wide), the tuck, feet first; the thrusters ignite | (follows) |
| 2.95 | The braking burn bites: slow motion (0.35x for 0.6 s) | wide and low at the pad's edge |
| 3.12 | Touchdown in a deep crouch, arms out low; grit and snow blown out over the pad, the windsocks snap out | (holds, slowing) |
| 4.40 | It rises; the thrusters sputter three times and cool | a low orbit round it |
| 5.05 | The sun breaks the cloud line: gold floods the pad; it looks to the sun | (orbit ends) |
| 5.70 | **The name** (a wipe). The hero, chin up | low three-quarter from its left, the sun behind its shoulder |
| 6.45 | One last puff of the thrusters | (holds) |

**Motion:** keyed: the dive (root pitched over, arms back), the flare, the tuck and the feet-first brake (the root on a Hermite path
from the sky to the pad), the crouch landing on planted feet, the rise and the look to the sun, the hero hold. Cyan afterimages
through the flip.

## Whiteout (8.0 s, the name at 6.2 s)
Night, a blizzard, a polar outpost lost in it. The frame is a wall of snow backlit by the outpost's floodlights (the fog itself is
the storm). A shape kneels in it, frosted white. Its visor lights. Heat lines glow up its suit and steam starts to rise; the frost
cracks and falls away in shards. It rises, steam pouring off it, clenches a fist, and a pulse of heat blows the snow off the ground
round it in a dome of steam, the ground left dark and wet. The storm parts: the outpost, its masts and the aurora above.

**The set (polar outpost):** a snowfield with drifts and wind-carved sastrugi, far plains and ice ridges; a corrugated half-round hut
with red end walls, a door, two lit windows, a caged lamp, snow on its back and drifts at its foot; a floodlight mast with four lamps
aimed at the pilot; a red-and-white radio mast with guy wires and a beacon; fuel drums; flag-stake markers; a loaded sled; ice
blocks; the aurora's three curtains; the night sky. The thaw is a wet disc the pulse grows.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | White: snow driving across, backlit; a dim kneeling shape emerging from the fog, frosted white | telephoto, low, one slow push into the storm |
| 1.40 | Its visor lights | (the push goes on) |
| 2.00 | Heat lines glow up its suit, steam rises | close insert on the shoulder and visor |
| 2.80 | The frost cracks and falls away; a burst of steam; the head lifts | (holds) |
| 3.40 | It rises from the kneel, steam pouring off | a cut wider, tilting up with it |
| 4.50 | It clenches a fist: the heat pulse (slow motion 0.42x for 0.3 s) | low and close |
| 5.00 | The storm parts: the fog draws back, the outpost, the masts, the aurora | pulling out and up |
| 6.20 | **The name** (assembled from shards). The hero in the thawed ground, steam curling | three-quarter hero |

**Motion:** keyed: the kneel (the left foot planted forward, the right knee down, the head bowed), the shudder, the rise through the
knees with a step, the fist drawn in and the arms thrown open for the pulse, the hero hold. The frost coat is a pale ghost of the
body until the crack.

## Slipstream (7.4 s, the name at 5.6 s)
Underground, at speed. The pilot rides crouched on the roof of a maglev racing through a tunnel, its light strips streaking past.
The train bursts into a station hall; the pilot rises, sprints along the roof and leaps off for the platform, lands in a slide with
sparks off its boots and a hand dragging, and stops on the mark as the train pulls away into the far tunnel, its tail lights fading.

**The set (maglev station):** a ribbed concrete tunnel with light strips at each end; the trackbed and guideway; a tiled platform
with a tactile strip and a lit edge; a tiled far wall with a teal band, lit ad panels, signs and a split-flap departure board; a
barrel vault with ribs and light lines; flared columns; benches; hanging signs; the end walls' tunnel mouths; the maglev (a nose, a
middle car the pilot rides, a nose at the tail), its windows lit.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The tunnel: the pilot crouched on the roof, the strips streaking past | low on the roof behind it |
| 0.80 | (the ride) | beside the train, keeping pace |
| 1.55 | It stands; the train bursts into the hall (1.66) | (rising out of the tunnel with it) |
| 1.78 | It sprints along the roof (the motion bank's sprint) | (keeps pace) |
| 2.70 | The leap: it hangs at the take-off (0.3x) and snaps back to speed | a cut to the platform, wide, the train behind |
| 3.30 | It lands in a slide: sparks off the boots and the dragging hand, a scorch on the floor, the train's wake | low on the platform as it slides at the lens |
| 4.30 | It stops on the mark and stands; a glance after the train (4.45); the tail lights fade into the tunnel | a low orbit |
| 5.00 | The station chime | (holds) |
| 5.60 | **The name** (typed). The departure board clatters over | three-quarter hero, the board and the empty track behind |

**Motion:** keyed: the crouch on the moving roof (the feet planted in the car's frame), the leap, the slide (the feet moving with
it), the glance and the hero hold; the run along the roof is the motion bank's `Unarmed-Sprint` at 1.45x on the model.

## Waygate (8.2 s, the name at 6.4 s)
Dusk in a canyon. An ancient ring gate of carved stone with three fins stands on a stepped dais at the end of a ruined plaza. Its
inner ring turns and locks five times with heavy clunks, each lighting a lamp in the outer ring; light races round its channels; the
gate opens on a turning disc of light that pulls the sand toward it. A figure takes shape in the light and floats out, arms spread,
glides over the dais and settles on the plaza; the disc collapses to a point behind it and the gate goes dark; it steps forward
under the ring's afterglow as the plaza's inlay lights round it.

**The set (canyon plaza):** the ring gate (a carved outer ring with three fins, six lamps and lit channels, a turning inner ring of
glyphs) on its feet and plinth; a three-step half-round dais; rings of worn flagstones with a dark inlay and a lit channel round the
mark; six broken fluted columns and fallen drums; two obelisks with glyph bands; sand and dunes; the canyon's banded cliff walls; the
dusk sky.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The plaza at dusk, the gate dormant, sand blowing | centred and wide, pushing in |
| 0.50 | The inner ring turns and locks (and at 1.1, 1.6, 2.0, 2.3), a lamp lighting each time | (the push) |
| 2.40 | The channels race alight; motes spiral into the ring | (the push) |
| 3.00 | The gate opens: a turning vortex of light, sand pulled toward it | (the push) |
| 3.60 | A figure takes shape in the light and floats out, arms spread, light shed off it | an arc from its left side round to the front |
| 4.65 | It settles on the plaza | a cut low and centred |
| 5.20 | The disc collapses to a point; a white blink; a gust of sand; the inlay lights | (holds) |
| 5.55 | It steps forward | (holds) |
| 6.40 | **The name** (rising). Centred under the ring like a halo, motes rising off the channels | low and centred, closer |

**Motion:** keyed: the float (arms spread, legs together and soft, the head lifting) on a path out of the gate and down to the plaza,
the settle, two steps, the hero hold. Cyan afterimages as it comes through.

## Flashbulb (7.8 s, the name at 6.0 s)
A premiere at night. A red carpet runs from a theatre's doors between velvet ropes; the marquee's bulbs chase round it and two
searchlights sweep the sky. The doors swing open and light spills down the carpet; the pilot strides out and down it as the
photographers' flashes pop from both sides. On the mark it gives three poses, each held for its photograph; a storm of flashes and
confetti; the cover shot under the name.

**The set (premiere):** the pavement, kerb and road; the red carpet with a gold braid; brass stanchions and velvet ropes; press
barriers; an art-deco theatre front (fluted pilasters with stepped caps, teal-and-gold deco bands, dark windows); the marquee canopy
with a letter board of bars (no words) and its bulbs in four runs; glass doors (the middle pair swing open) and the lit foyer; a
blade sign with neon stars and stripes; two lit poster cases; four lamp posts; the night sky.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The dark street; the marquee's bulbs chase on, the searchlights sweep up | wide from the street |
| 0.50 | The doors swing open, light spills down the carpet; a silhouette in the doorway | (holds) |
| 0.75 | It strides down the carpet (the motion bank's walk), flashes popping from both sides | low, dollying back ahead of it |
| 3.32 | On the mark: pose one, a look back over the shoulder, held; a flash floods the lens | photograph one, from its left |
| 3.95 | Pose two, a hand on the hip, chin up, held | photograph two, from its right |
| 4.58 | Pose three, two fingers to the brow, then flicked out | photograph three, centred and low |
| 5.25 | A storm of flashes (Reduced: at most 2.5 a second, softer) and confetti | a crane back to the marquee |
| 6.00 | **The name** (slammed): the cover shot | centred and low, the marquee above |

**Motion:** keyed: the doorway, the three poses and the cover's stance; the stride is the motion bank's `Armed-Walk` at 1.33x on the
model (the walk's own swing; the keyed rig walks under it for the anchors).

## The default
Cloudbreak is the new default (the intro any pilot without an equipped intro gets, and the fallback where an archived pick has no
weapon intro). It reads at a glance on any pilot, it is the brightest of the five, and its arrival needs no setup to understand.
