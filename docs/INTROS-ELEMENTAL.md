# The elemental weapon intros (intros v2, 2026-11-20)

Owner (2026-11-20, before sleeping): "I want to move the intros to a much higher quality standard, great animations and effects,
non-block based background and designs, etc. With great camera work as well!" The four magic weapons' signatures are replaced
by four new intros, each a summoning of its element with a story, a modelled set, an authored camera and a name beat. The old
ones (Cinder Crown, Tidal Serpent, Thunderstep, Eventide Gate) are archived by the framework (`docs/INTROS-V2.md`); their code
stays in `Core/NativeEntranceWeapons.cs`.

Author: Claude (I5), branch `claude/intro-elemental`. Presentation only: no rule, wire, save or timing change. The framework (its
registry, the archive, the name card, the camera rig, the set loader, the sound plumbing) is I0's. This branch owns
`Core/NativeIntroElemental.cs`, `Tests/IntroElementalChecks.cs` (`--intro-elemental`), the set script
`tools/intros/elemental_set.py`, the sound rows `docs/audio/intros/I5.json` and their lines on the I5 slot of
`Core/NativeIntroSoundKeys.cs`, and one registry line per intro.

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
| Still Tide | drowned hall: colonnades, pointed arcades, clerestory lancets, a ribbed vault broken over two bays, a rose window, still water (`water/surface`, raised by the tide), the wave's two halves liquid (`water/wave/L,R`: a breaking curl, its barrel and thin lip lit through from teal to white) and frozen (`water/frozen/L,R`: faceted ice whose faces toward the barrel glow cyan to white, the light through it, so it reads as ice in any arena; a bright rim, frost at its foot and crest), the frost over the water (`water/ice/0..4`: broken, irregular patches, no rings, revealed in turn by the frost front) | the water weapon's cyan `#63e2ff`, foam `#eaffff`, deep `#2b8bd6`, moonlight `#a6d4ff`, ice `#d6faff`; teal-black | gliding: a dolly over the water, a rise with the tide, low under the rearing wave, front and low as it parts, an orbit through the frozen moment, a pull back between the walls; it ends still and centred down the corridor of ice, a slow push in |
| Strike Point | storm summit: fractured slabs of wet dark rock, rain pools that catch every flash (`lightning/puddles`), a scorch round the strike with cracks cooling from white-hot (`lightning/scorch`), a cliff falling into a cloud sea, six conductor spires with copper bands, a coil and an iron spike (`lightning/spires`, their seams `lightning/seams/k`), a fulgurite crater (`lightning/crater`), peaks through the clouds | the lightning weapon's violet `#c8aaff`, core `#f3ecff`; storm slate-blue, near-black rock | violent and vertical: low and wide, straight up into the turning vortex, down from the cloud, the smash to the strike, a fast orbit, rising with it, low and close under the call with the spires firing; it ends on a Dutch low orbit |
| Black Sun | eclipse plain: near-black obsidian plates over a dim violet, dark ash dunes, six leaning monoliths with rune staves (`darkness/glyphs/k`), a broken gate framing the sun, far mesas, the moon disc (`darkness/moon`, slid over the sun) | the darkness weapon's `#af6eff`, the void orb's rim `#c04dff` and lens `#d9a6ff`; dim dusk to black, a violet rim | still and symmetrical: centred through the gate, high over the plain as the shadows stream in, low under the black sun, locked off for the void, the eye held close (a slow ramp), low against the eclipse for the wings; it ends pulling back, centred under the black sun |

## How they are built

- **The base** (`NativeIntroElementalScript` on `NativeIntroV2Script`): the keyed rig (the anchors for the camera and effects,
  the Classic pose and the model's pose) with the staff placed by arm IK on a keyed grip track (`StaffKey`, `Arms`) and the right
  hand riding it for two-handed moves (`Support`); the drawn wrists (`INativeIntroHands`) carry the staff's effects; `Beam` lays every streak over 1 m on its head's camera plane and skips one whose head is behind the lens; `Bolt` draws
  a fractal bolt (a slow wander and a fine jitter on short segments, forks); `Star` is a glint whose rays are laid the same way.
- **Motion: bespoke keyed animation only** (owner, 2026-11-20 morning: "we dont need the matches own annimation system. Think like
  the top dog and Bombardier ones, they have unique animations"). No motion-bank clip plays (`Clip` is never true). Every beat is
  keyed (`NativeEntranceKeys`: anticipation, impact keys that ring out, pass keys that flow, planted feet and step plans):
  - Forgeborn: rises out of the fire, walks out heavily, goes up on its toes for the crown, takes the staff high in both hands
    and drives it down.
  - Still Tide: looks back, braces with the staff level overhead, sweeps it across into a lunge, taps the water.
  - Strike Point: lands kneeling with a hand on the stone; rises, dips and thrusts the staff up for the call, is rocked back by
    the bolt, plants the staff.
  - Black Sun: walks low out of the void, hunches, throws the wings open, snaps the staff down.
  `--intro-elemental` fails on any match clip, a non-finite joint or a head turned more than .62 rad from its chest;
  `--intro-elemental-pose id t...` dumps the keyed joints (stick-figure sheets: the animation checked without the Editor).
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
  - Black Sun: a low white key, a violet horizon in the obsidian.
- **Sets:** `tools/intros/elemental_set.py` (Blender 5.2.3 headless, the stage kit's geometry, finishes, AO and bank format),
  modelled in the intros' source frame and mirrored into the bank's Unity space at export (so the script and the scripts share
  their numbers; `IntroElementalChecks` reads them from the script). `<prop>` is the Fine tier, `<prop>/low` the Low tier. The bank
  `ChipArenaIntroElemental.bytes` (about 6.5 MB, 133k vertices over both tiers) is local and never committed; without it every
  intro plays on the framework's dark lit void in its own light, fog and effects.
- **Gloss:** every set finish stays at smoothness .35 or less. Before the intro owned its light, the sets mirrored the arena's
  probe (Cyber City's magenta on the water, the ice and the obsidian), and low baked occlusion hid it. With the intro's own
  reflection the caps are lifted to .8-.85 (`darken`, `ice_colours`, `wave_colours`).
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
| 4.75 | The column spirals in and gathers on its head: a hot ring at the brow, nine flame tongues standing off it, uneven and curling | orbit framing the crown and the whole figure | `Cast-L-Buff1` | `intro2.fire.crown`; `magic.flame-lance`, `fire` |
| 5.45 | The staff comes down: fire runs out along the floor, the lava rings flare outward | wide and low | `Cast-L-AOE1` | `intro2.fire.slam`; `magic.fire.hit`, `explosion` |
| 6.0 | **Name** (Rise): the hero under the burning crown, the forge's mouth glowing behind (heat shimmering off it, sparks, embers) | low, from its right, rising | staff idle | `intro2.fire.name`; `melee.greatsword.hit`, `metal` |

## Still Tide (water)

A drowned hall under the moon. The sea comes in to drown the intruder, and the pilot stops it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Still water between the colonnades, moon shafts and sheens on the water, drips | glide low over the water | staff idle | `intro2.water.drips`; `boss.deepend.drip`, `water` |
| 1.3 | The tide rises, foam along the colonnade's feet | rising with it | staff idle | `intro2.water.rise`; `boss.deepend.tide`, `water` |
| 1.75 | The wave rears behind the pilot (its halves one wall) and rolls forward, its barrel lit through, foam riding its crest, the lip spilling, spray off it | low, looking up past it | `Cast-L-Buff2` | `intro2.water.rear`; `magic.tidal-wave`, `water` |
| 2.95 | **The sweep** (ramp): the wave parts down its middle, its halves surging round either side | front and low | `Cast-L-AOE2` | `intro2.water.part`; `boss.deepend.wave`, `water` |
| 3.75 | **Freeze** (ramp): frost races up both walls, they freeze mid-curl into faceted ice, the frost spreads over the water in broken patches, the spray hangs and glitters | orbit through the stopped moment | `Cast-Attack1` (the tap) | `intro2.water.freeze`; `blast.frost`, `shield` |
| 4.85 | The ice settles, frost shivering off the crests | pull back between the walls | staff idle | `intro2.water.settle`; `impact.glass`, `shield` |
| 6.4 | **Name** (Wipe): between the two frozen waves, the breath frosting | still, centred, down the corridor of ice | staff idle | `intro2.water.name`; `boss.noctiel.ripple`, `install` |

## Strike Point (lightning)

A summit under a storm. The storm has been waiting for a conductor; the pilot is the strike.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Rain on the slate, St Elmo's fire on the spires, the clouds lit from inside | low and wide, tilting up | unseen | `intro2.storm.bed`; `structure.rumble`, `lightning` |
| 1.1 | Arcs leap spire to spire and crawl in over the stone; the cloud turns into a vortex | straight up into it, turning | unseen | `intro2.storm.charge`; `blast.static`, `lightning` |
| 2.0 | A point of light gathers in the vortex's eye | down from the cloud | unseen | `intro2.storm.gather`; `chip.vacuum`, `dash` |
| 2.45 | **Strike** (ramp): the trunk and its branches come down and are the pilot, white-hot in a crouch (a lag-0 afterimage cooling), arcs run out over the stone, the pools flash, the rock is scorched round it | smash to wide and low, then a fast orbit | keyed crouch | `intro2.storm.strike`; `magic.lightning.hit`, `lightning` / `intro2.storm.after`; `blast.shock`, `explosion` |
| 3.7 | It rises, arcs crawling on its limbs | low, rising with it | staff idle | `intro2.storm.crackle`; `status.shock-zap`, `electric-hook` |
| 4.3 | The staff up: a bolt into its head, then to every spire, which fire back into the sky | low and close, looking up, the spires firing round it | `Cast-L-Summon1` | `intro2.storm.call`; `magic.lightning.shot`, `electric-hook-hit` |
| 5.5 | **Name** (Slam): the staff crackling, rain, the storm behind | a Dutch low orbit from its left | staff idle | `intro2.storm.name`; `blast.quake`, `explosion` |

In Reduced effects the cloud's flashes are held .45 s apart and widened (`Flash`), the bolts re-shape every .42 s, and the
strike's white-out is halved.

## Black Sun (darkness)

An eclipse over a glass plain. The light goes out, the dark comes to one place, and the pilot comes out of it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Dusk on the obsidian, the sun in the broken gate, the moon's edge touching it | centred through the gate | unseen | `intro2.void.wind`; `boss.noctiel.glide`, `darkness` |
| 1.35 | The moon slides over, the light drains; the shadows stream in along the ground from all sides, their leading edges violet, converging on the centre | high over the plain, craning down | unseen | `intro2.void.pour`; `boss.noctiel.eclipse.pool`, `darkness` |
| 2.8 | **Totality** (ramp): the corona's rays, the diamond; the poured dark rises into a void against a violet glare | low, centred, the void under the black sun | unseen | `intro2.void.totality`; `boss.noctiel.eclipse`, `darkness` |
| 3.55 | It steps out of the void, built in as it crosses its face, the dark running off it | locked off | keyed walk, the staff at its side | `intro2.void.emerge`; `blast.implosion`, `dash` |
| 4.55 | One eye of light on the visor, a star of glints as it lights (a slow ramp holds it) | close on the head, a slow push | head up | `intro2.void.eye`; `boss.noctiel.hush.open`, `install` |
| 5.05 | The staff up, the dark spreading behind its shoulders in five feathers a side edged with violet, against the eclipse | low in front, looking up past it at the black sun | `Cast-L-Summon2` | `intro2.void.wings`; `boss.noctiel.wings.open`, `dash` |
| 5.7 | The snap: the dark streaks back to the monoliths, their runes flare | | `Cast-Attack1` | `intro2.void.snap`; `magic.darkness.hit`, `darkness` |
| 6.6 | **Name** (Assemble): centred under the black sun, the eye lit | pulling back, centred and low, the black sun over its head | staff idle | `intro2.void.name`; `blast.void`, `darkness` |

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
`docs/audio/intros/I5.json` (category `boss`, two takes each, prompts ending "isolated, no reverb, no music, no background
noise") with their keys on the I5 slot of `Core/NativeIntroSoundKeys.cs`; the framework compiles every author's rows into one
queue job (`docs/audio/intros/README.md`). Generation runs only inside a builder run.

## Checks

- The framework's `--intros-v2` (every registered intro): registry, determinism, no allocation, budgets, Reduced effects (no
  shake, nothing faster than 3 Hz), the streak rule, the cues, the sets with and without a bank.
- `--intro-elemental`: the set script's numbers are the scripts' (the vent, the rings, the rivers, the hall, the spires, the
  monoliths, the facing); every prop drawn is one the script builds; each intro draws its set through a fake bank and the void
  without one; 6-8.5 s; the cue keys are in the contract and requested; long streaks on their head's plane; FX peaks within 600
  (300 Low); the model's clips are known staff clips, played from their first window to the end without returning to the keys;
  the staff's anchors finite. `--intro-elemental-probe [id] [t...]` prints the anchors, the camera and where the model's feet,
  chest and head top land on screen.

## Choices made overnight (owner asleep)

- The four names (Forgeborn, Still Tide, Strike Point, Black Sun) are new; rename freely in the registry line.
- Still Tide holds on its mark instead of walking out between the walls: its model plays its staff clips throughout (no upper-body
  layer tonight), and a clip never goes back to the keys.
- The water's walls are set pieces of the bank lifted, swung, rolled forward and swapped for their frozen form under the frost
  front (no live fluid).
- The polish pass (the integrator's review, 2026-11-20 morning) was filmed in holds 3 and 4 (hold 4 at the integrator's word). Its
  clips replace the earlier ones, which are kept as `*-v1.mp4`.
- Known from hold 4 (Cyber City): Still Tide's ice reads purple. The set takes the arena's own light volume (`ArenaGI`) and probe,
  and with the probe cut the volume's magenta shows. Black Sun's plain is still dark grey rather than near-black. See DEV_LOG for
  candidate fixes.
- The magic sink's water puffs and the void orb's matte sphere are not on the entrance sink tonight; the water and the void use
  lit puffs, streaks and glows.
