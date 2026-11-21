# The elemental weapon intros (intros v2, 2026-11-20)

Owner (2026-11-20, before sleeping): "I want to move the intros to a much higher quality standard, great animations and effects,
non-block based background and designs, etc. With great camera work as well!" The four magic weapons' signatures are replaced
by four new intros, each a summoning of its element with a story, a modelled set, an authored camera and a name beat. The old
ones (Cinder Crown, Tidal Serpent, Thunderstep, Eventide Gate) are archived by the framework (`docs/INTROS-V2.md`); their code
stays in `Core/NativeEntranceWeapons.cs`.

Author: Claude (I5), branch `claude/intro-elemental`. Presentation only: no rule, wire, save or timing change. The framework (its
registry, the archive, the name card, the camera rig, the set loader, the sound plumbing) is I0's. This branch owns
`Core/NativeIntroElemental.cs`, `Tests/IntroElementalChecks.cs` (`--intro-elemental`), the set script
`tools/intros/elemental_set.py`, the sound rows `docs/audio/intros-b2/I5.json` (batch 2's manifest) and their lines on the I5 slot
of `Core/NativeIntroSoundKeys.cs`, and one registry line per intro.

| Weapon | Id | Intro | Length | Name at | One line |
| --- | --- | --- | ---: | ---: | --- |
| Fire | `intro2-weapon-fire` | **Forgeborn** | 7.8 s | 6.0 s | In a caldera forge the cracks race in, a fire column erupts, the pilot walks out of it and its flame gathers into a crown. |
| Water | `intro2-weapon-water` | **Still Tide** | 8.2 s | 6.4 s | In a moonlit drowned hall the tide rises into a wave behind the pilot, parts round it at a sweep of the staff and freezes mid-curl. |
| Lightning | `intro2-weapon-lightning` | **Strike Point** | 7.2 s | 5.5 s | On a storm summit the spires charge, the sky opens and the pilot arrives as the strike itself, then calls a second bolt into the staff. |
| Darkness | `intro2-weapon-darkness` | **Black Sun** | 8.4 s | 6.6 s | On an obsidian plain the moon covers the sun, every shadow pours to the centre into a void, and the pilot steps out of it with one eye of light. |

Each is distinct in set, palette and camera:

| | Set (bank props) | Palette (game colours) | Camera language |
| --- | --- | --- | --- |
| Forgeborn | caldera forge: basalt column tops with lava in the cracks (`fire/lava/0..5`, lit in turn), a dais of irregular fitted stones (no ring joints) round the vent (`fire/vent`, its round lava throat under jagged rock lips), four lava rivers (`fire/river/k`) from two falls on the rim (`fire/falls`), organ-pipe column clusters, a half-ruined forge arch with hot joints (`fire/forge-seams`), its firebox (`fire/hearth`: a lining graded from a white-yellow core to a deep red lip, on a bed of coals), a crater rim of crags | basalt black, the fire weapon's orange `#ff6c32`, flame yellow, ember red; smoky red-brown fog | heavy and grounded: a macro along a seam, a crane up and back, low under the eruption, a crane up the column, the walk at the lens, an orbit framing the crown, wide and low for the slam; it ends on a low crane rising from the pilot's right, the forge's mouth glowing at the left |
| Still Tide | drowned hall: colonnades, pointed arcades, clerestory lancets, a ribbed vault broken over two bays, a rose window, still water (`water/surface`, raised by the tide), the wave's two halves liquid (`water/wave/L,R`: a plunging breaker's section, `WAVE_SECTION`: a long back, the crest, the lip thrown out and curling down over a hollow barrel; dark glossy water mirroring the hall's moonlit sky, lit through where the lip runs thin from a dark teal to a clear cyan, foam broken over the crest and the lip's edge and raked down the face) and frozen (`water/frozen/L,R`: the same breaker as clear glossy ice with a crystalline skin, pale where it faces the sky and a deep clear blue where it is thick, rime on its crest, the curl lit through, two threads of light frozen in it, icicles off the lip), the frost over the water (`water/ice/0..4`: broken, irregular patches, no rings, revealed in turn by the frost front) | the water weapon's cyan `#63e2ff`, foam `#eaffff`, deep `#2b8bd6`, moonlight `#a6d4ff`, ice `#d6faff`; teal-black | gliding: a dolly over the water, a rise with the tide, low under the rearing wave, front and low as it parts, an orbit through the frozen moment, a pull back between the walls; it ends still and centred down the corridor of ice, a slow push in |
| Strike Point | storm summit: fractured slabs of wet dark rock, rain pools that catch every flash (`lightning/puddles`), a scorch round the strike with cracks cooling from white-hot (`lightning/scorch`), a cliff falling into a cloud sea, six conductor spires with copper bands, a coil and an iron spike (`lightning/spires`, their seams `lightning/seams/k`), a fulgurite crater (`lightning/crater`), peaks through the clouds | the lightning weapon's violet `#c8aaff`, core `#f3ecff`; storm slate-blue, near-black rock | violent and vertical: low and wide, straight up into the turning vortex, down from the cloud, the smash to the strike, a fast orbit, rising with it, low and close under the call with the spires firing; it ends on a Dutch low orbit |
| Black Sun | eclipse plain under a dusk sky (`darkness/sky`: a dome burning amber and rose along the whole horizon, hottest under the sun, through violet to a near-black zenith; Core cools and dims it through the eclipse, leaving totality's sunset all round): polished obsidian plates (mirroring that sky) over a dim violet, dark ash dunes, six leaning monoliths with rune staves (`darkness/glyphs/k`), a broken gate framing the sun, mesas inside the dome, the moon disc (`darkness/moon`, slid over the sun) | the darkness weapon's `#af6eff`, the void orb's rim `#c04dff` and lens `#d9a6ff`; dim dusk to black, a violet rim | still and symmetrical: centred through the gate, high over the plain as the shadows stream in, low under the black sun, locked off for the void, the eye held close (a slow ramp), low against the eclipse for the wings; it ends pulling back, centred under the black sun |

## How they are built

- **The base** (`NativeIntroElementalScript` on `NativeIntroV2Script`, batch 2 on the framework's pose tools, `docs/INTROS-V2.md`
  9a-9c): the staff's left fist keyed in the chest's frame (`Hold`: the wrist in the keyed rig's proportions, the shaft's direction
  in the body's frame so an upright staff stays upright, the hand's back, the elbow's pole; `NativeIntroGrip.Hold` places it, the
  palm's roll in the forearm), held out at the side so the drawn staff never stands across the face (the model's shoulders are
  0.13 m from the spine, the rig's 0.35 m); the right hand on the shaft by the grip IK (`INativeIntroGrip.GripAt` from `TwoHands`
  and `Along`) where both hands hold it; the feet and the drops on the model (`ModelFeet`; Strike Point's kneel `ModelKneel`), the
  walks on `NativeIntroFeet.Track` and `NativeIntroFeet.Walk`. `Beam` lays every streak over 1 m on its head's camera plane and skips
  one whose head is behind the lens; `Bolt` draws a fractal bolt (a slow wander and a fine jitter on short segments, forks); `Star`
  is a glint whose rays are laid the same way.
- **Motion: bespoke keyed animation only** (owner, 2026-11-20 morning: "we dont need the matches own annimation system. Think like
  the top dog and Bombardier ones, they have unique animations"). No motion-bank clip plays (`Clip` is never true). Every beat is
  keyed (`NativeEntranceKeys`: anticipation, impact keys that ring out, pass keys that flow, contacts that plant):
  - Forgeborn: crouched in the column, it rises, chest first; three heavy steps out of the fire (the hips sinking onto each, the
    free arm swinging, the staff upright at its side); straightening under the fire with the free arm rising open up and out at
    its side beneath the crown; both arms up in a V as the staff is swung high at its side; the staff driven down beside its foot in a deep slam, the free arm
    thrown back; up into the hold.
  - Still Tide: head bowed; a look back over its right shoulder; the staff taken low across its body in both hands, the weight on a
    braced foot; coiling lower under the rearing wave; the arms swept wide apart into a wide stance as the wave parts (the staff
    upright out at its left, the open right hand out at its right); the staff's heel struck into the water, the knees giving; a look
    along each frozen wall.
  - Strike Point: a three-point landing on the right knee (the knee on the stone, the fist down, the staff planted forward at its
    side, the head bowed); the head comes up; the push up, the right foot stepping through; the call (the staff raised high out at
    its side, the head back, the free arm flung out); rocked back by the bolt; the staff driven down beside its foot.
  - Black Sun: a low stalking walk out of the void, the head lifting to level as the eye lights; a hunch; the wings, both arms
    thrown up wide (the staff high out at its side); the snap, the staff planted, the free arm whipped in.
  `--intro-elemental` fails on any match clip, a non-finite joint or a head turned more than .62 rad from its chest;
  `--intro-elemental-limits` lists the framework's findings for these four (the limits, the drawn feet, the drawn grip; 0 each);
  `--intro-elemental-model DIR` writes the body as the model draws it (the staff seated, the grip on) every 1/30 s for stick reviews
  from the side, at three-quarters and through the lens; `--intro-elemental-pose id t...` dumps the keyed joints.
- **On the body as drawn** (the framework's model-aware anchors, `docs/INTROS-V2.md` 2a):
  - Forgeborn's crown sits on `Anchor(Head)`, Black Sun's eye on `Anchor(Visor)`, and Still Tide's breath comes from just below
    `Anchor(Eyes)`.
  - The staff's head is `Anchor(LeftMuzzle)` along `WeaponBore`.
  - The old hand-tuned offsets are gone (`HeadShift`, `ModelHeadLift`, the drawn-wrist shift). Only the eye close-up keeps a
    keyed framing (`CameraEyeLift`, camera only), since the camera reads the keyed anchors.
- **Its own light** (`docs/INTROS-V2.md` 3a): the arena's lights, probe, volume and ambient no longer reach an intro, and point
  lights reach only the pilot. So each set is lit by its own directional and ambient, and mirrors its own sky (`Reflect*`):
  - Forgeborn: a warm key from the fire over the dais, lava light from below.
  - Still Tide: the moon through the rose window, a cold sky in the ice and the water.
  - Strike Point: cloud light flashing with the storm, mirrored in the wet rock.
  - Black Sun: the dusk's low warm key from the sun behind the gate, an amber horizon in the obsidian draining to violet under the
    eclipse; its fog held off the sky's dome (120 m out), whose own gradient is the distance.
- **Sets:** `tools/intros/elemental_set.py` (Blender 5.2.3 headless, the stage kit's geometry, finishes, AO and bank format),
  modelled in the intros' source frame and mirrored into the bank's Unity space at export (so the script and the scripts share
  their numbers; `IntroElementalChecks` reads them from the script). `<prop>` is the Fine tier, `<prop>/low` the Low tier. The bank
  `ChipArenaIntroElemental.bytes` (v9: 6,686,905 bytes, 137k vertices over both tiers) is local and never committed; without it every
  intro plays on the framework's dark lit void in its own light, fog and effects.
- **Gloss:** since the intro owns its light (its own reflection, `Reflect*`), the wet and glassy finishes are glossy and mirror the
  intro's own sky: the wave .9 (its foam .2), the ice .86 (its rime .25), the water's face .86, the obsidian .84 (Black Sun's
  reflection a warm amber horizon at dusk, violet through the eclipse). Before batch 2 they stayed at .35 or less, because they
  mirrored the arena's probe (Cyber City's magenta on the water, the ice and the obsidian).
- **The name:** every card sits at `Y` .86, a band under the hero frame's feet, centred (`X` .5); each ending frames the pilot
  above it (the probe puts the feet at .72-.76).

## Forgeborn (fire)

The forge of a dead volcano. The caldera wakes, the forge's fire rises as a column, and the pilot is forged in it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Dark caldera lit by its lava, embers rising | macro, low along a seam toward the dais | unseen | `intro2.fire.rumble`; `structure.rumble`, `stone` |
| 1.0 | The cracks light in from the rim to the dais, sparks running down the seams | crane up and back | unseen | `intro2.fire.crack`; `structure.crack`, `stone` |
| 2.15 | **Eruption** (speed ramp): a 9.5 m column of flame (tongues licking up it in four tiers, white at their feet, orange, red-tipped, round a white-hot heart; a little dark smoke off its head), sparks, the forge's joints flaring | low under it, then a crane up it | built in inside the fire | `intro2.fire.erupt`; `blast.fire`, `explosion` / `intro2.fire.column`; `hazard.lava`, `fire` |
| 3.3 | It walks out of the fire, flame peeling off its shoulders and arms | front, pushing in at the lens | keyed walk, the staff upright at its side | `intro2.fire.step` (x2); `impact.stone`, `stone` |
| 4.75 | The column spirals in and gathers on its head: a hot ring at the brow, nine flame tongues standing off it, uneven and curling | orbit from its right framing the crown and the whole figure | the free arm raised open up and out under it, the chin up | `intro2.fire.crown`; `magic.flame-lance`, `fire` |
| 5.45 | The staff comes down: fire runs out along the floor, the lava rings flare outward | wide and low | both arms up, then the staff driven down, a deep slam | `intro2.fire.slam`; `magic.fire.hit`, `explosion` |
| 6.0 | **Name** (Rise): the hero under the burning crown, the forge's mouth glowing behind (heat shimmering off it, sparks, embers) | low, from its right, rising | the hold, the staff upright at its side | `intro2.fire.name`; `melee.greatsword.hit`, `metal` |

## Still Tide (water)

A drowned hall under the moon. The sea comes in to drown the intruder, and the pilot stops it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Still water between the colonnades, moon shafts and sheens on the water, drips | glide low over the water | head bowed, the staff at its side | `intro2.water.drips`; `boss.deepend.drip`, `water` |
| 1.3 | The tide rises, foam along the colonnade's feet | rising with it | a look back over its shoulder | `intro2.water.rise`; `boss.deepend.tide`, `water` |
| 1.75 | The wave rears behind the pilot (its halves one wall) and rolls forward, its barrel lit through, foam riding its crest, the lip spilling, spray off it | low, looking up past it | the staff low across its body in both hands, braced | `intro2.water.rear`; `magic.tidal-wave`, `water` |
| 2.95 | **The sweep** (ramp): the wave parts down its middle, its halves surging round either side | front and low | the arms swept wide apart, a wide stance | `intro2.water.part`; `boss.deepend.wave`, `water` |
| 3.75 | **Freeze** (ramp): frost races up both walls, they freeze mid-curl into faceted ice, the frost spreads over the water in broken patches, the spray hangs and glitters | orbit through the stopped moment | the tap: the heel into the water, the knees giving | `intro2.water.freeze`; `blast.frost`, `shield` |
| 4.85 | The ice settles, frost shivering off the crests | pull back between the walls | a look along each wall | `intro2.water.settle`; `impact.glass`, `shield` |
| 6.4 | **Name** (Wipe): between the two frozen waves, the breath frosting | still, centred, down the corridor of ice | the hold | `intro2.water.name`; `boss.noctiel.ripple`, `install` |

## Strike Point (lightning)

A summit under a storm. The storm has been waiting for a conductor; the pilot is the strike.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Rain on the slate, St Elmo's fire on the spires, the clouds lit from inside | low and wide, tilting up | unseen | `intro2.storm.bed`; `structure.rumble`, `lightning` |
| 1.1 | Arcs leap spire to spire and crawl in over the stone; the cloud turns into a vortex | straight up into it, turning | unseen | `intro2.storm.charge`; `blast.static`, `lightning` |
| 2.0 | A point of light gathers in the vortex's eye | down from the cloud | unseen | `intro2.storm.gather`; `chip.vacuum`, `dash` |
| 2.45 | **Strike** (ramp): the trunk and its branches come down and are the pilot, white-hot in a crouch (a lag-0 afterimage cooling), arcs run out over the stone, the pools flash, the rock is scorched round it | smash to wide and low, then a fast orbit round its right side to just behind it (it stops there so the staff arm, the left, never crosses the face: `--intro-arms`) | a three-point landing on its right knee, the fist on the stone | `intro2.storm.strike`; `magic.lightning.hit`, `lightning` / `intro2.storm.after`; `blast.shock`, `explosion` |
| 3.7 | It rises, arcs crawling on its limbs | low, rising with it | the push up, the right foot stepping through | `intro2.storm.crackle`; `status.shock-zap`, `electric-hook` |
| 4.3 | The staff up: a bolt into its head, then to every spire, which fire back into the sky | low and close, looking up, the spires firing round it | the staff raised high at its side, rocked back by the bolt, driven down | `intro2.storm.call`; `magic.lightning.shot`, `electric-hook-hit` |
| 5.5 | **Name** (Slam): the staff crackling, rain, the storm behind | a Dutch low orbit from its right | the hold | `intro2.storm.name`; `blast.quake`, `explosion` |

In Reduced effects the cloud's flashes are held .45 s apart and widened (`Flash`), the bolts re-shape every .42 s, and the
strike's white-out is halved.

## Black Sun (darkness)

An eclipse over a glass plain. The light goes out, the dark comes to one place, and the pilot comes out of it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Dusk on the obsidian, the sun in the broken gate, the moon's edge touching it | centred through the gate | unseen | `intro2.void.wind`; `boss.noctiel.glide`, `darkness` |
| 1.35 | The moon slides over, the light drains; the shadows stream in along the ground from all sides, their leading edges violet, converging on the centre | high over the plain, craning down | unseen | `intro2.void.pour`; `boss.noctiel.eclipse.pool`, `darkness` |
| 2.8 | **Totality** (ramp): the corona's rays, the diamond; the poured dark rises into a void against a violet glare | low, centred, the void under the black sun | unseen | `intro2.void.totality`; `boss.noctiel.eclipse`, `darkness` |
| 3.55 | It steps out of the void, built in as it crosses its face, the dark running off it | locked off | a low stalking walk, the staff at its side | `intro2.void.emerge`; `blast.implosion`, `dash` |
| 4.55 | One eye of light on the visor, a star of glints as it lights (a slow ramp holds it) | close on the head, a slow push | the head level | `intro2.void.eye`; `boss.noctiel.hush.open`, `install` |
| 5.05 | The staff up, the dark spreading behind its shoulders in five feathers a side edged with violet, against the eclipse | low in front, looking up past it at the black sun | both arms thrown up wide | `intro2.void.wings`; `boss.noctiel.wings.open`, `dash` |
| 5.7 | The snap: the dark streaks back to the monoliths, their runes flare | | the staff planted, the free arm whipped in | `intro2.void.snap`; `magic.darkness.hit`, `darkness` |
| 6.6 | **Name** (Assemble): centred under the black sun, the eye lit | pulling back, centred and low, the black sun over its head | the hold | `intro2.void.name`; `blast.void`, `darkness` |

## Sounds

Every cue has an existing bank key and a built-in clip, so the intros are synced before anything is generated (the managed
checks hear each cue once at 60 Hz and at a guest's 20 Hz). A cue plays its generated key, else the existing key, else the
built-in clip (`Runtime/NativeIntroAudio.cs`). Each existing key is a one-shot that the installed bank fills, going by
`docs/audio/audio-inventory-2026-11-16.csv`. `BankPlay` refuses a loop entry and finds nothing in an empty key. So the first
mapping's loops (`field.fire`, `field.storm`, `magic.ball-lightning`, `magic.singularity`) and empty keys
(`boss.ironworks.arrive.rumble`, `boss.ironworks.crust.crack`, `boss.station.arc`) would have dropped to the built-in clip.
They are now `hazard.lava`, `structure.rumble`, `status.shock-zap` and `blast.void`, plus the empty keys' listed stand-ins
(`structure.rumble`, `structure.crack`, `blast.static`). `--intro-elemental` checks that no fallback key is a loop. The 31
generated sounds are requested in
`docs/audio/intros-b2/I5.json` (batch 2's rows; category `boss`, two takes each, prompts ending "isolated, no reverb, no music, no
background noise", each `targetSeconds.min` at most 0.45 of its `generateSeconds` since the importer trims silence) with their keys
on the I5 slot of `Core/NativeIntroSoundKeys.cs`. They are compiled into batch 2's own manifest
(`tools/intros/compile_sound_requests.py --batch b2`: `docs/audio/sound-requests-intros-b2.json` and its bundle copy; batch 1's sealed
manifest is untouched); the integrator writes the queue job at release (`docs/audio/intros/README.md` 2a). Generation runs only
inside a builder run.

## Checks

- The framework's `--intros-v2` (every registered intro): registry, determinism, no allocation, budgets, Reduced effects (no
  shake, nothing faster than 3 Hz), the streak rule, the cues, the sets with and without a bank.
- `--intro-elemental`: the set script's numbers are the scripts' (the vent, the rings, the rivers, the hall, the spires, the
  monoliths, the facing); every prop drawn is one the script builds; each intro draws its set through a fake bank and the void
  without one; 6-8.5 s; the cue keys are in the contract and requested; long streaks on their head's plane; FX peaks within 600
  (300 Low); no match clip; the staff's anchors finite. `--intro-elemental-limits`: the framework's limits, drawn feet and drawn
  grip for these four (0 findings; `--intro-limits` holds the same for every intro). `--intro-elemental-probe [id] [t...]` prints the anchors, the camera and where the model's feet,
  chest and head top land on screen.

## Choices made overnight (owner asleep)

- The four names (Forgeborn, Still Tide, Strike Point, Black Sun) are new; rename freely in the registry line.
- Still Tide holds on its mark instead of walking out between the walls (its feet step out for the parting and back).
- The water's walls are set pieces of the bank lifted, swung, rolled forward and swapped for their frozen form under the frost
  front (no live fluid).
- The polish pass (the integrator's review, 2026-11-20 morning) was filmed in holds 3 and 4 (hold 4 at the integrator's word). Its
  clips replace the earlier ones, which are kept as `*-v1.mp4`.
- Batch 2 (the integrator's review: Still Tide's pillow wave and flat magenta polygon ice, Black Sun plain still grey; the owner's
  weird orientations): the wave and the ice rebuilt as a breaker's section with water and ice finishes that mirror the intro's own
  sky; Black Sun's dusk sky dome and glossy obsidian; every body re-keyed on the framework's pose tools (0 findings), the staff out
  at the side, checked on the model from the side and at three-quarters.
- The magic sink's water puffs and the void orb's matte sphere are not on the entrance sink tonight; the water and the void use
  lit puffs, streaks and glows.
