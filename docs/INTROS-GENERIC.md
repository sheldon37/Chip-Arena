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
| **Slipstream** | speed, neon | travel: riding a maglev's roof out of a tunnel, a leap and a sliding stop | an underground maglev station | kinetic: tracking with the train, a whip at the leap, a slow-motion wide, a slide into the lens |
| **Waygate** | ceremony, dusk | a summons: an ancient ring gate wakes, opens, and the pilot floats out of it | a canyon plaza with a carved stone ring gate | formal: a centred push-in, a quarter orbit, a centred hero framed by the ring |
| **Flashbulb** | glamour, swagger | a showstopper: a stride down a red carpet, three poses frozen by photographers' flashes | a theatre's red carpet at night under a chasing marquee | the dolly ahead of a walk, then three "photographs" (held frames from three angles) and a cover shot |

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
Dawn above the clouds. A summit pokes out of a sea of cloud; a relay mast blinks red on it. A white-hot point falls out of the sky
on a contrail, a pilot in a head-first dive, glowing from the re-entry. It flips feet first, its boots' thrusters fire a braking burn
and it settles onto the summit's pad in slow motion; the wash blows grit and snow across the pad and snaps the windsocks out. It
rises as the sun clears the far peaks and floods the pad with gold.

**The set (summit relay):** a craggy rock summit falling away to the cloud sea; an octagonal concrete landing pad with worn
markings, a hazard-striped edge and recessed edge lights; a triangular steel lattice relay mast with two dish antennas and a red
beacon; a corrugated equipment hut with vent fins and a solar array; pipe railings; three windsock poles; far snow peaks out of the
clouds; the cloud sea itself; a dawn sky with the sun low over the peaks.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The pad at dawn, the beacon blinking, high wind | low on the pad, looking up past the mast at the sky, creeping in |
| 0.35 | A boom: a bright point appears high up | (holds) |
| 0.45 | The streak arcs down out of the sky, a contrail behind it | tilting down after it |
| 1.30 | The pilot in a head-first dive, its shoulders glowing, mist peeling off | long lens from the pad's edge, following the fall |
| 2.20 | The summit rushing up | close beside the pilot, falling with it |
| 2.55 | It tucks and flips feet first; the boots' thrusters ignite | (follows) |
| 2.90 | The braking burn bites: slow motion | the fall-follow slows with it (a speed ramp) |
| 3.30 | Touchdown in a crouch on the pad; the wash blows grit and snow out over the pad, the windsocks snap out | wide, low, at the pad's edge |
| 4.30 | It rises; the thrusters sputter and wind down, heat shimmer | a low orbit round it |
| 5.00 | The sun clears the far peaks: gold floods the pad | (orbit ends) |
| 5.70 | **The name.** The hero: chin up, loose hands | low three-quarter, the sun behind its shoulder |
| 6.30 | One last puff of the thrusters; the beacon blinks | (holds) |

**Motion:** a fall clip under a keyed head-first dive (root pitched down, arms swept back), the jump-flip's tuck for the flip, the
fall under a feet-first brake (arms out for balance), the land clip held low through the slow motion, the land's recovery into the
idle, the idle under the hero (chest up, head up).

**Sound:** wind bed, the distant boom, the falling roar, thruster ignition and burn, the touchdown thud and gust, the thrusters
winding down, the sun's warm swell, the name sting.

## Whiteout (8.0 s, the name at 6.2 s)
Night, a blizzard, a polar outpost lost in it. The frame is a wall of snow backlit by the outpost's floodlights. A shape kneels in
it, frosted white. Its visor lights. Heat lines glow up its suit and steam starts to rise; the frost cracks and falls off in plates.
It rises, steam pouring off it, clenches a fist, and a pulse of heat blows the snow off the ground round it in a cloud of steam. The
storm parts: the outpost, the masts and the aurora above.

**The set (polar outpost):** a snowfield with drifts and wind-carved sastrugi; a ribbed half-round hut with a lit door, a caged
lamp and a snow-loaded roof; a floodlight mast with four lamp heads; a radio mast with guy wires and a red beacon; fuel drums;
flag-stake markers; a sled; pressure-ridge ice blocks; far ice ridges; the aurora's curtains in the sky.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | White: blowing snow backlit by floodlights, the wind howling | long lens, low, pushing slowly into the storm |
| 0.30 | A dim kneeling shape emerges, frosted white, head bowed | (the push goes on) |
| 1.40 | Its visor lights | (the push goes on) |
| 2.00 | Heat lines glow up its suit, steam rises | close insert on the shoulder and visor |
| 2.80 | The frost cracks and falls off in plates; a burst of steam; the head lifts | (holds) |
| 3.40 | It rises from the kneel, steam pouring off, snow sliding off its shoulders | tilting up with it |
| 4.50 | It clenches a fist: a pulse of heat blows the snow off the ground round it, the ground left dark and wet | low and wide |
| 5.00 | The storm parts: the outpost, the masts, the aurora | pulling out and down |
| 6.20 | **The name.** The hero in the cleared ground, steam curling off it, the aurora behind | low wide hero |

**Motion:** the crouch clip held as the kneel (head bowed, keyed), lifted head and shoulders as it thaws, the crouch's rise into
the idle, a fist clench and a squared stance keyed over the idle.

**Sound:** the blizzard, the visor's power-up, the heater's hum and sizzle, the frost cracking, the steam's hiss, snow crunching as
it rises, the heat pulse's thump, the wind dropping away to a calm, the name sting.

## Slipstream (7.4 s, the name at 5.6 s)
Underground, at speed. The pilot rides crouched on the roof of a maglev racing through a tunnel, the tunnel's light strips streaking
past. The train bursts out of the tunnel into a station hall; the pilot rises, runs along the roof and leaps off onto the platform,
lands in a slide with sparks off its boots and stops on the mark as the train howls past behind it and away into the far tunnel.

**The set (maglev station):** a ribbed tunnel with light strips and cable trays; the guideway; a long platform with a tactile
edge strip and a lit edge; columns, a vaulted ceiling with light lines, benches, signage and a split-flap departure board; the far
tunnel mouth; the maglev itself (three cars: a sleek nose, windows lit inside).

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The tunnel: light strips streaking past; the pilot crouched on the roof, the station's light ahead | low over the roof behind the pilot |
| 0.80 | (the ride) | beside the train, keeping pace, the tunnel's ribs flicking past |
| 1.60 | Out of the tunnel into the station hall; it stands and runs along the roof | (keeps pace, rising) |
| 2.70 | It leaps off toward the platform: slow motion | a whip to the platform: wide, the train streaking behind |
| 3.30 | It lands in a slide, one hand down, sparks off the boots | low on the platform, it slides toward the lens |
| 4.10 | It stops on the mark; the last car whips past behind, its tail lights fading into the far tunnel | (holds) |
| 4.60 | It stands, glances after the train, back to the lens | (settles) |
| 5.60 | **The name.** The hero on the platform; the departure board clatters over | low, the empty guideway behind |

**Motion:** the crouch clip held on the roof (arms out, keyed for balance), a sprint clip along the roof, the jump clip, the land
clip into a keyed slide (the root slows over the floor, one knee down, a hand on the floor), the land's recovery into the idle, a
keyed glance back.

**Sound:** the tunnel rush, the burst out of the tunnel, footsteps on the roof, the leap's whoosh, the slide's metal skid, the
train passing, the station chime, the departure board's flaps, the name sting.

## Waygate (8.2 s, the name at 6.4 s)
Dusk in a canyon. An ancient ring gate of carved stone stands on a stepped dais at the end of a ruined plaza. Its outer segments
turn and lock one after another with heavy clunks, each lighting the glyph channels in it; light races round the ring; the gate
opens on a turning disc of light that pulls the sand toward it. A figure takes shape in the light and floats out of it, arms
spread, a metre above the dais, and settles onto the plaza. The disc collapses behind it to a point; the gate goes dark.

**The set (canyon plaza):** the ring gate (a carved stone ring with nine turning segments and glowing glyph channels) on a three-step
dais; worn flagstones with a circular inlay; broken columns; two obelisks framing the axis; sand drifts; the canyon's layered cliff
walls; a dusk sky.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The plaza at dusk, the gate dormant, sand blowing | centred, wide, locked off, pushing in slowly |
| 0.50 | The first segment turns and locks; its glyphs light | (the push) |
| 1.10 | The second, the third, the fourth, the fifth, sooner each time | (the push) |
| 2.40 | Light races round the ring; dust shakes off it | (the push) |
| 3.00 | The gate opens: a turning disc of light, sand pulled toward it | (the push) |
| 3.60 | A figure takes shape in the light and floats out, arms spread | a quarter orbit from its side to the front |
| 4.60 | It settles onto the plaza | (orbit ends) |
| 5.20 | The disc collapses to a point; a gust of sand; the gate goes dark | (holds) |
| 5.60 | It steps forward off the last step | (holds) |
| 6.40 | **The name.** Centred, the ring behind it like a halo, the glyphs' afterglow | low and centred |

**Motion:** the dual cast's AOE start and loop (arms spread) under a keyed lift and float, the land clip softened, a step from the
walk clip, the idle under the hero.

**Sound:** the desert wind and a low drone, five stone-and-metal locks, the rising charge, the opening's swirl, the pass-through
shimmer, the touchdown on sand, the collapse, the name sting.

## Flashbulb (7.8 s, the name at 6.0 s)
A premiere at night. A red carpet runs from a theatre's doors between velvet ropes; the marquee's bulbs chase round it and
searchlights sweep the sky. The doors swing open and light spills down the carpet; the pilot strides out and down it as flashes pop
from both sides. On the mark it stops and gives the photographers three poses, each frozen by a flash; a storm of flashes; the cover
shot under the name.

**The set (premiere):** the red carpet; brass stanchions and velvet ropes; an art-deco theatre front with a marquee canopy lined
with bulbs, glass doors, poster frames and a vertical sign; lamp posts; the photographers' barriers; the street.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The dark street; the marquee's bulbs chase on, the searchlights sweep up | wide from the street |
| 0.60 | The doors swing open, light spills down the carpet; a silhouette in the doorway | (holds) |
| 1.20 | It strides down the carpet, flashes popping from both sides | low, dollying back ahead of it |
| 3.20 | On the mark: it stops and turns to the lens; pose one (a look over the shoulder), frozen by a flash | photograph one, from the left |
| 3.90 | Pose two (a hand on the hip, chin up), frozen | photograph two, from the right |
| 4.60 | Pose three (a two-finger salute off the brow), frozen | photograph three, centred |
| 5.30 | A storm of flashes (Reduced: at most 3 a second, softer) | (holds) |
| 6.00 | **The name**: the cover shot | centred, the marquee behind |

**Motion:** the walk clip down the carpet, the idle under three keyed poses (the head and chest turned, one arm keyed to the hip,
the other to the brow), each pose held while its photograph is held.

**Sound:** the crowd's murmur and cheer, the marquee's bulbs clicking on, the doors, shutters and flash pops, the burst of flashes,
the name sting.

## The default
Cloudbreak is the new default (the intro any pilot without an equipped intro gets, and the fallback where an archived pick has no
weapon intro). It reads at a glance on any pilot, it is the brightest of the five, and its arrival needs no setup to understand.
