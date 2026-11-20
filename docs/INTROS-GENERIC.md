# The five generic intros (Intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well! Lets generate... 5 new intros". These five replace the archived
generic entrances (Hero Landing, Orbital Drop, Grid Rez, Holo Projection, Blackout, Skyline Zip, Overclock). Any pilot can pick
them: they are weapon-agnostic, the hands stay empty and every move is the intro's own keyed animation.

The contract they are built on is `docs/INTROS-V2.md` (agent I0, `claude/intro-framework`). This page is the authored design:
the story, the set, the camera's language, the beats, the motion, the effects and the sound of each.

| Intro | Mood | Arrival | Set | Camera language |
| --- | --- | --- | --- | --- |
| **Cloudbreak** | awe, dawn gold | from above: a re-entry streak, a flip and a thruster-braked touchdown | a summit relay station above a sea of cloud | vertical: a look up, a long-lens fall-follow, a slow-motion touchdown, a low hero |
| **Whiteout** | cold mystery, then warmth | out of the white: a frosted figure kneeling in a blizzard thaws and rises | a polar outpost at night, its floodlights in the snow | telephoto: one slow push into the storm, a close insert, a reveal as the storm parts |
| **Slipstream** | speed, neon | travel: riding a maglev's roof out of a tunnel, a leap and a sliding stop | an underground maglev station | kinetic: on the roof and beside the train keeping pace, a cut to the platform for the leap, a slide into the lens |
| **Threshold** | ceremony, dusk | a summons: a seam of light splits two leaning standing stones and the pilot steps out through the slit | a canyon plaza with two standing stones meeting in a cleft | formal: a centred push-in on the cleft, an arc in front as it steps out, a centred hero under the stones |
| **Flashbulb** | glamour, swagger | a showstopper: a stride down a red carpet, three poses frozen by photographers' flashes | a theatre's red carpet at night under a chasing marquee | the dolly ahead of a walk, then three photographs (held poses from three angles, each lit by its flash) and a cover shot |

**The default:** Cloudbreak (see "The default" below).

## Shared rules
- **Timing:** 7.4-8.2 s each, the name beat between 5.6 and 6.4 s; the pilot holds a hero pose under the name.
- **The closing frame and the name:** the name sits in a band under the feet (`card.Y` 0.855 of the height, `CardY` in the base),
  never on the legs: each closing shot shows the whole figure above it (the crown near 0.2 of the height, the soles near 0.71,
  settled before the card arrives; `NativeIntroGenericScript.Closing`, a slow push in from a low front three-quarter). The
  name, its kicker and its line stay centred (`card.X` unset), on the floor in front of the pilot.
- **On the body as drawn:** effects and lights sit on the framework's model-aware anchors (`Anchor`, docs/INTROS-V2.md 2a: the
  drawn feet, hands, chest and `Visor`); no hand-tuned head offsets. Whiteout's visor is the model's own (its accent colour) and
  its light sits at `Anchor(Visor)`. Its kneel bows the head gently and its close-up is near frontal: a deep bow seen from above
  the shoulder read as a head turned aside (the framework measured the mapping exact).
- **Weapon-agnostic:** no weapon in the hands (the registry gives them no weapon spec, so nothing is drawn in them).
- **Body:** the intro's own keyed animation (`NativeEntranceKeys` and planned steps, as Top Dog and Last Warning are; owner:
  "think like the top dog and Bombardier ones"), drawn on the model; no motion-bank clips.
- **Feet on the model** (`NativeIntroGenericScript`, 2026-11-20 second pass). The model takes the keyed legs' turns bone for bone
  (docs/INTROS-V2.md 2b) on legs 1.7 times as long (hip to ankle .851 m against the rig's .5), hung from hips set closer in, under hips
  that move 1.7 times as far from rest as the keyed ones, on the shared root, with its feet unplanted. So a keyed foot held still while
  the root travels slid the drawn foot back at 0.7 times the walking speed (the walks moonwalked about a quarter of a metre a step:
  measured 1 m/s), a keyed stride drew 1.7 times as long, and a keyed drop sank the drawn hips 1.7 times as deep (the kneels sat the
  hips on the floor with a knee 30 cm through it). Now:
  - every stance and step is written where the **model's** ankle goes, and each frame the keyed foot is put where the drawn one lands
    there (`OnModel`, from the keyed hips of a first pass); drops are written in the model's metres (`Sink`);
  - a walking foot rolls: the heel rises onto the ball before it leaves (the ball held still, the ankle coming forward over it), the toe
    trails, and the heel strikes with the toe up, flattening as the weight comes on;
  - a kneeling leg (`Kneel`) puts the model's knee on the floor under the hips with the shin along the floor behind and the toes
    tucked, whatever the hips do;
  - walks are a `Track` (the hips' speed rising, steady and falling) with each step landing where the hips will be half way through
    its stance (`Walk`): the foot lands ahead of the hips and leaves behind them, and the strides are the model's (about .7 m);
  - arms that must meet the body (a hand on the hip, two fingers to the brow, a fist on the pad, a hand on the knee) were solved on the
    measured skeleton for where the model's hand lands, then keyed.

  `CoreTests --intro-generic` holds it: on the model, a foot flat on the floor stays put (under .45 m/s) except where the intro slides
  it (`Sliding`), and no knee, ankle or ball goes 4 cm into the floor. (A Classic body draws the keyed rig, whose planted feet now creep
  forward through a walk; the model is what is drawn by default.)
- **Sets:** each modelled in Blender 5.2.3 (headless) by its own script (`tools/intros/generic_<set>.py`, run by
  `tools/intros/generic_sets.py`, shapes from `generic_kit.py` on stagekit), two tiers (Fine, `/low`), into the local bank
  `ChipArenaIntroGeneric.bytes` (7.7 MB; never committed). Nothing in them is a placeholder shape: the sun has a limb, the pad is
  square and painted, the far ranges are ridged, the thaw is wet ground and slush, the aurora is rays. Every far piece (sky domes, peaks, clouds, canyon) lies inside the
  game's 200 m far plane. Without the bank each intro draws its own plain fallback (a floor far wider than its fog, a soft pool,
  a horizon glow) in its own light, never blocks.
- **Effects:** `NativeVfxDraw` (glows, streaks, smoke); a streak longer than 1 m is laid on its head's camera plane
  (`NativeIntroGenericScript.Line`); no ring outlines (shockwaves are dust, snow and steam); nothing flashes faster than 3 Hz in
  Reduced effects and there is no camera shake there.
- **Sound:** each intro has a cue timeline on its beats, every cue with an existing bank key and a built-in clip as stand-ins; a
  stand-in key is always a filled one-shot (the bank will not play a loop key or an empty one as a cue, and the checks refuse both).
  The generated sounds are requested in `docs/audio/intros/I2.json` (49 keys, 82 takes, 128 s of generation), their contract
  lines in `Core/NativeIntroSoundKeys.cs`; the framework compiles every author's rows into the builder's queue.

## Where it lives
| Part | Files |
| --- | --- |
| The intros | `Core/NativeIntroGeneric.cs` (the shared base: the set's frame, long streaks, the plain fallback), `Core/NativeIntroCloudbreak.cs`, `NativeIntroWhiteout.cs`, `NativeIntroSlipstream.cs`, `NativeIntroThreshold.cs`, `NativeIntroFlashbulb.cs` |
| Registry | `Core/NativeIntroCatalog.cs`: Cloudbreak on the default's slot (`intro2-default`), the other four on generic slots 1-4 (`intro2-whiteout`, `intro2-slipstream`, `intro2-threshold`, `intro2-flashbulb`) |
| Sets | `tools/intros/generic_sets.py` and its modules; the bank's master copy in `F:/Chip Arena Development/scratch/intro-generic/bank/` |
| Sound | `docs/audio/intros/I2.json`, `Core/NativeIntroSoundKeys.cs` (the I2 slot) |
| Framing preview | `tools/intros/generic_preview.py` (Blender Workbench, no Editor): renders each intro's camera, set pieces, a mannequin on the keyed anchors and its effects marked, from the views `CoreTests --intro-generic` writes with `INTRO_GENERIC_VIEWS` set |
| The body as drawn | `CoreTests --intro-generic` with `INTRO_GENERIC_MODEL` set to a folder writes each intro's model skeleton (bound on the measured skeleton), keyed anchors and camera every 1/30 s as JSON, for stick-figure reviews from any side |
| Checks | `Tests/IntroGenericChecks.cs` (`CoreTests --intro-generic`), and the framework's `Tests/IntroV2Checks.cs` (`--intros-v2`) |

To rebuild the bank: `blender --background --factory-startup --python unity/ChipArenaConversion/tools/intros/generic_sets.py --
--out DIR [--preview DIR]` (about 25 s with occlusion), then copy `DIR/ChipArenaLocal/Resources/ChipArenaIntroGeneric.bytes`
and its `.meta` into `Assets/LocalAssets/ChipArenaLocal/Resources/`.

## Cloudbreak (7.6 s, the name at 5.7 s)
Dawn above the clouds. A summit pokes out of a sea of cloud; a relay mast blinks red on it. A boom, and a white-hot point falls out
of the sky on a contrail: the pilot in a head-first dive, glowing from the re-entry. It flares, tucks and flips feet first, its boots'
thrusters fire a braking burn and it settles onto the summit's pad in slow motion; the wash blows grit and snow across the pad and
snaps the windsocks out. It rises as the sun breaks the cloud line behind it and floods the pad with gold.

**The set (summit relay):** a craggy faceted rock summit falling away to the cloud sea, snow on its ledges; a square concrete
landing pad (no circles: a painted H, hazard-striped corners, scuffs and thruster scorch, twelve edge lights); a red-and-white lattice relay
mast with two dish antennas, platforms and a red beacon; a corrugated equipment hut with a door, a vent and a stack; a solar array on
the slope; a yellow railing; two windsock poles (their socks turn in the wind); far mountain ranges in three layers either side of the sun (ridged
crests, snow on the heights and steep faces, each farther layer paler into the dawn haze; open cloud toward the sun); the cloud sea
and far cumulus;
the dawn sky and the sun (its face white-hot at the core, darkening to gold at the limb, a halo glow round it).

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The pad at dawn, the beacon blinking | low on the pad, looking up past the mast at the sky and the horizon, creeping in |
| 0.35 | A distant boom | (holds) |
| 0.45 | The streak: a white-hot head, a contrail laid on the camera's plane, smoke hanging along it | (rising) |
| 1.30 | The dive: head first, the shoulders' glow cooling, wind streaming past, mist peeling off | a long lens from the pad's edge, following the fall |
| 2.20 | The summit rushing up | falling alongside it |
| 2.45 | The flare (belly to the pad, arms wide), the tuck, feet first; the thrusters ignite | (follows) |
| 2.95 | The braking burn bites: slow motion (0.35x for 0.6 s) | wide and low at the pad's edge |
| 3.12 | Touchdown in a superhero landing (right knee and fist on the pad, left foot out front, left arm swept back); grit and snow blown out over the pad, the windsocks snap out | (holds, slowing) |
| 4.40 | It rises; the thrusters sputter three times and cool | a low orbit round it |
| 5.05 | The sun breaks the cloud line: gold floods the pad; it looks to the sun | (orbit ends) |
| 5.70 | **The name** (a wipe). The hero, chin up | the closing: the whole figure from a low three-quarter, the sun beside it, the name on the pad |
| 6.45 | One last puff of the thrusters | (holds) |

**Motion:** keyed: the dive (root pitched over, arms back), the flare, the tuck and the feet-first brake (the root on a Hermite path
from the sky to the pad, the arms flung wide as it brakes), then a superhero landing: the left foot planted out in front, the right knee
down on the pad, the torso folded over the front knee, the right fist on the pad and the left arm swept back; a held breath, the head
up to the lens, then it pushes off the pad and drives up over the front foot, the right foot stepping up beside it, chest open, chin
up; it turns to the sunrise, rolls its right shoulder back and sets its left fist on its hip, and on the name its head comes round to
the lens, chin up, its weight on the back leg. Cyan afterimages through the flip.

## Whiteout (8.0 s, the name at 6.2 s)
Night, a blizzard, a polar outpost lost in it. The frame is a wall of snow backlit by the outpost's floodlights (the fog itself is
the storm). A shape kneels in it, frosted white. Its visor lights. Heat lines glow up its suit and steam starts to rise; the frost
cracks and falls away in shards. It rises, steam pouring off it, clenches a fist, and a pulse of heat blows the snow off the ground
round it in a dome of steam, the ground left dark and wet. The storm parts: the outpost, its masts and the aurora above.

**The set (polar outpost):** a snowfield with drifts and wind-carved sastrugi, far plains and ice ridges; a corrugated half-round hut
with red end walls, a door, two lit windows, a caged lamp, snow on its back and drifts at its foot; a floodlight mast with four lamps
aimed at the pilot; a red-and-white radio mast with guy wires and a beacon; fuel drums; flag-stake markers; a loaded sled; ice
blocks; the aurora's three curtains of rays (ragged tops, each fading where its light ends); the night sky. The thaw the pulse grows
is melted ground: dark and wet at the heart, grey slush, a low rim of packed snow fading into the field.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | White: snow driving across, backlit; a dim kneeling shape emerging from the fog, frosted white | telephoto, low, one slow push into the storm |
| 1.40 | Its visor lights | (the push goes on) |
| 2.00 | Heat lines glow up its suit, steam rises | close insert on the shoulders and visor, low (framed by the chest) |
| 2.80 | The frost cracks and falls away; a burst of steam; the head lifts | (holds) |
| 3.40 | It rises from the kneel, steam pouring off | a cut wider, tilting up with it |
| 4.50 | It clenches a fist: the heat pulse (slow motion 0.42x for 0.3 s) | low and close |
| 5.00 | The storm parts: the fog draws back, the outpost, the masts, the aurora | pulling out and up |
| 6.20 | **The name** (assembled from shards). The hero in the thawed ground, steam curling | the closing from its right: the hut behind it, the radio mast beside it |

**Motion:** keyed: frozen in a kneel, huddled over the front knee (the left foot planted, the right knee in the snow, the left forearm
across the knee, the right forearm on the thigh, head bowed); the visor wakes and the head stirs; the shoulders draw up and shiver as the
heat builds; the frost cracks (the head snaps up, the chest opens, the right arm flings out); the weight goes forward over the front
foot, the hand pressing on the knee, and it drives up, the right foot stepping up beside the left; the fists drawn in to the chest and a
dip, then the pulse (a deep sink, the arms thrown down and wide); it looks up at the aurora on a long breath as the storm parts, then
its head comes down to the lens and its right hand comes to rest over its heart, where the heat came from. The frost coat is a pale ghost of the
body until the crack. The visor is the model's own, lit by its accent; its light rides the model's head as drawn.

## Slipstream (7.4 s, the name at 5.6 s)
Underground, at speed. The pilot rides crouched on the roof of a maglev racing through a tunnel, its light strips streaking past.
The train bursts into a station hall; the pilot springs up, sprints along the roof and leaps off for the platform, lands in a slide
with sparks off its boots and a hand dragging, and rises onto the mark as the train pulls away into the far tunnel, its tail lights
fading.

**The set (maglev station):** a ribbed concrete tunnel with light strips at each end; the trackbed and guideway; a tiled platform
with a tactile strip and a lit edge; a tiled far wall with a teal band, lit ad panels, signs and a split-flap departure board; a
barrel vault with ribs and light lines; flared columns; benches; hanging signs; the end walls' tunnel mouths; the maglev (a nose, a
middle car the pilot rides, a nose at the tail), its windows lit.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The tunnel: the pilot crouched on the roof, the strips streaking past | low on the roof behind it |
| 0.80 | (the ride) | beside the train, keeping pace |
| 1.55 | It springs up; the train bursts into the hall (1.66) | (rising out of the tunnel with it) |
| 1.80 | It sprints along the roof (a footfall every .27 s at 9.1 m/s): the right foot, the left, the right | (keeps pace) |
| 2.60 | The take-off, planted on the left foot | |
| 2.70 | The leap: it hangs at the take-off (0.3x) and snaps back to speed | a cut to the platform, wide, the train behind |
| 3.30 | It lands on the right foot into a slide: sparks off the boot, the folded knee and the dragging hand, a scorch on the floor, the train's wake | low on the platform as it slides at the lens |
| 4.30 | It stops short of the mark and rises over its left foot onto it; a glance after the train (4.45); the tail lights fade into the tunnel | a low orbit |
| 5.00 | The station chime | (holds) |
| 5.60 | **The name** (typed). The departure board clatters over | the closing from its right, the hall receding behind it |

**Motion:** the sprint, the take-off and the slide are the owner's Runner Action pack's clips (owner: "The run is very 'cartoon'
like, can we make this more realistic? Could use the running pack I have"), keyed over this intro's own keys from the intros' local
motion bank (`docs/INTROS-V2.md` 9d; `ChipArenaIntroMotion.bytes`, never committed): `Mvm_Dash` for the sprint (its root moving along
the roof at the clip's 9.12 m/s, so the drawn feet hold), `Jmp_FrontAir_A_Start` for the take-off (held on its airborne frame into the
keyed leap), and `Esc_Slide_All` for the slide (the right foot down on the landing, the leg folding under, the left leg out in front,
its feet sliding with it until it stops) and the rise (held feet, the body forward over the left foot onto the mark, held on its
upright frame as it eases into the keyed stand: the clip runs on into a stride, which mixed with the stand jolted the body and the
camera following it).
- **Keyed:** a surfer's crouch on the moving roof, the spring up off the front (left) foot as the right swings through, the split leap
  in the air, and the hero beats: it glances after the train, brushes the slide's dust off its left shoulder and settles loose and
  cocked (the weight on one leg, the shoulders tilted), the head tilting to the lens on the name.
- **The moving roof:** the body is posed in the car's frame until the leap (`Carried`), so its feet, planted or held by a clip, stay
  on the moving roof; `IntroGenericChecks.Roof` holds them there (under .45 m/s against the car).
- **Under the clips:** the keyed lean is on the pelvis, not the body (a clip plays every joint but the body's turn, so a keyed body
  lean tilted the whole clip 20 degrees); the slide's right hand is keyed (in line with its forearm, the fingertips dragging), as the
  clip's wrist, bent back flat on the floor, draws further off its keyed turn than the binding allows a hand; the root rides 2 cm higher
  while the slide plays; and as the rise hands over to the keyed stand both feet are pinned to the keys' plan (`Repin`), the left on the
  hero stance's spot, where the slide stops it (`LeftReach`), the right stepping up beside it.
- **Without the bank** (CI, a build without the local assets) the keys play: the same beats and footfalls, keyed (an accelerating
  sprint at the clip's stride, a take-off off the left foot, a knee slide on the right knee with the left foot out in front and the
  right hand dragging, the right foot planting under the rising body). The sparks follow whatever is down (the folded leg's knee, the
  boot out in front, the dragging hand's fingertips), so both read the same.
- **Checks:** with the bank, its timing is the clips' (`IntroGenericChecks.Clips`: the sprint's speed and stride, the footfalls, the
  take-off's plant, the slide's touchdown), and Slipstream is checked both ways (its feet, the tunnel, the roof, the budgets).

## Threshold (8.2 s, the name at 6.4 s)
Dusk in a canyon. At the end of a ruined plaza two leaning standing stones meet in a cleft. A crack of light runs up the seam between
them in three jolts, dust shaken from it; the seam brightens and the runes cut beside it wake; the stones grind apart and light floods
out of the widening slit in a sheet, dust pouring along the ground; the pilot steps out through the slit and walks down the plaza to
the mark as the stones grind shut behind it, the seam a fading line. (It replaces Waygate, whose ring gate, lamps lit round it and
vortex read as an existing film's gate: no ring, no circles, no vortex.)

**The set (canyon plaza):** two leaning standing stones (rough outer faces, broken slanted tops, worn runes cut up beside the seam,
their tops touching at rest; each its own piece, parted by Core); rubble at their feet; a plaza of flagstones laid in a grid, some
lost to sand; six broken fluted columns and fallen drums; two obelisks with glyph bands framing the stones; sand and dunes; the
canyon's banded cliff walls with a saddle worn into the far one; the dusk sky.

| Time | Beat | Camera |
| --- | --- | --- |
| 0.00 | The plaza at dusk, the stones dark, sand blowing | centred and wide, pushing in on the cleft |
| 0.60 | A crack of light runs a third of the way up the seam, dust shaken off (and at 1.20, 1.75) | (the push) |
| 2.40 | The seam brightens, the runes wake | (the push) |
| 3.00 | The stones grind apart: light floods out of the slit, dust bursts along the ground, rays fan out | (the push) |
| 3.40 | The pilot steps out of the light on its first footfall (it set off unseen at 3.0) | an arc in front, from its left round to its front |
| 3.50 | It walks down the plaza toward the lens | (the arc) |
| 4.90 | The stones grind shut behind it, dust squeezed out, a last flare up the seam | low and centred, the stones towering |
| 5.85 | It reaches the mark | (holds) |
| 6.40 | **The name** (rising). Small and centred under the stones | the closing: wide, low and centred |

**Motion:** keyed: the walk out of the slit, measured and upright (strides of about .7 m every .49 s, the hips lowest at each
footfall and turned with the leg landing, the shoulders countering, the arms swinging opposite with the forward elbow bent, the chin
rising step by step); it steps into sight on its first footfall, glances back over its shoulder as the stones grind shut, settles onto
the mark with its weight, and sweeps its right arm up and out aside, the hand open, presenting the gate it came through, then holds
tall under the stones, chin up. Warm afterimages as it steps out.

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
| 0.80 | It struts down the carpet, flashes popping from both sides | a cut to it halfway down the carpet, low, dollying back ahead of it |
| 3.32 | On the mark: pose one, a look back over the shoulder, held; a flash floods the lens | photograph one, from its left |
| 3.95 | Pose two, a hand on the hip, chin up, held | photograph two, from its right |
| 4.58 | Pose three, two fingers to the brow, then flicked out | photograph three, centred and low |
| 5.25 | A storm of flashes (Reduced: at most 2.5 a second, softer) and confetti | a crane back to the marquee |
| 5.65 | (the storm goes on) | a cut to the cover: low, straight down the carpet |
| 6.00 | **The name** (slammed) on the carpet: the cover shot | (a slow push in) |

**Motion:** keyed: a silhouette in the doorway, weight on one hip, rolling its shoulders back as the doors open; after the cut a
catwalk strut (the feet crossing onto one line, the hips turning well round and dropping hard over the stance leg and swinging out over
it, shoulders back, chin up, loose arms); the three poses, each snapped in on its flash (one: turned away, weight back, the head over
the shoulder to the lens and a hand on the hip behind; two: square to the lens, a hand on the hip, the hip popped; three: two fingers
to the brow, flicked out); arms thrown up into a V in the storm; the cover in a power pose, both hands on the hips, photographed by
one last flash from the lens as the name slams (none in Reduced effects).

## The default
Cloudbreak is the new default (the intro any pilot without an equipped intro gets, and the fallback where an archived pick has no
weapon intro). It reads at a glance on any pilot, it is the brightest of the five, and its arrival needs no setup to understand.
