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
| Forgeborn | caldera forge: basalt column tops with lava in the cracks (`fire/lava/0..5`, lit in turn), the vent (`fire/vent`), four lava rivers (`fire/river/k`) from two falls on the rim (`fire/falls`), organ-pipe column clusters, a half-ruined forge arch with hot joints (`fire/forge-seams`) and its hearth (`fire/hearth`), a crater rim of crags | basalt black, the fire weapon's orange `#ff6c32`, flame yellow, ember red; smoky red-brown fog | heavy and grounded: a macro along a seam, a crane up and back, low under the eruption, a crane up the column, the walk at the lens, an orbit framing the crown, wide and low for the slam |
| Still Tide | drowned hall: colonnades, pointed arcades, clerestory lancets, a ribbed vault broken over two bays, a rose window, still water (`water/surface`, raised by the tide), the wave's two halves liquid (`water/wave/L,R`) and frozen (`water/frozen/L,R`), the frost's rings (`water/ice/0..4`) | the water weapon's cyan `#63e2ff`, foam `#eaffff`, deep `#2b8bd6`, moonlight `#a9c8ff`; teal-black | gliding: a dolly over the water, a rise with the tide, low under the rearing wave, front and low as it parts, an orbit through the frozen moment, a pull back between the walls |
| Strike Point | storm summit: fractured slate, a cliff falling into a cloud sea, six conductor spires with copper bands, a coil and an iron spike (`lightning/spires`, their seams `lightning/seams/k`), a fulgurite crater (`lightning/crater`), peaks through the clouds | the lightning weapon's violet `#c8aaff`, core `#f3ecff`; storm slate-blue | violent and vertical: low and wide, straight up into the turning vortex, down from the cloud, the smash to the strike, a fast orbit, rising with it, wide between the spires |
| Black Sun | eclipse plain: obsidian plates over a dim violet, ash dunes, six leaning monoliths with rune staves (`darkness/glyphs/k`), a broken gate framing the sun, far mesas, the moon disc (`darkness/moon`, slid over the sun) | the darkness weapon's `#af6eff`, the void orb's rim `#c04dff` and lens `#d9a6ff`; lilac dusk to black | still and symmetrical: centred through the gate, a dolly zoom as the shadows pour, low under the black sun, locked off for the void, a close-up on the eye, a slow pull back |

## How they are built

- **The base** (`NativeIntroElementalScript` on `NativeIntroV2Script`): the keyed rig (the anchors for the camera and effects,
  the Classic pose) with the staff placed by arm IK on a keyed grip track (`StaffKey`, `Arms`); the model's own motion from the
  motion bank over windows of the clock (`Clips`, `INativeIntroBody`); the drawn wrists (`INativeIntroHands`) carry the staff's
  effects; `Beam` lays every streak over 1 m on its head's camera plane and skips one whose head is behind the lens; `Bolt` draws
  a fractal bolt (a slow wander and a fine jitter on short segments, forks); `Star` is a glint whose rays are laid the same way.
- **Motion:** only clips already in the motion bank, mirrored for the staff hand (no rebake): `2Hand-Staff-Idle~mirror`, the
  `2Hand-Staff-Cast-L-Buff1/2`, `-AOE1/2`, `-Summon1/2` and `2Hand-Staff-Cast-Attack1` starts and ends. A model never goes back from
  a clip to the keys (it would pop): Forgeborn walks out of the fire on keys and plays its casts from the crown on; Still Tide plays
  clips throughout and holds on its mark; Strike Point's crouch is keyed, its rise and summon are clips; Black Sun walks out of the
  void on keys and plays its summon and strike. The keyed rig is lowered to the clips' stance so the anchors sit on the model.
- **Sets:** `tools/intros/elemental_set.py` (Blender 5.2.3 headless, the stage kit's geometry, finishes, AO and bank format),
  modelled in the intros' source frame and mirrored into the bank's Unity space at export (so the script and the scripts share
  their numbers; `IntroElementalChecks` reads them from the script). `<prop>` is the Fine tier, `<prop>/low` the Low tier. The bank
  `ChipArenaIntroElemental.bytes` (about 6.1 MB, 125k vertices over both tiers) is local and never committed; without it every
  intro plays on the framework's dark lit void in its own light, fog and effects.
- **Gloss:** every set finish stays at smoothness .35 or less: the set reflects the arena's own probe, and Cyber City's magenta
  neon showed on the water, the ice and the obsidian in the first captures.

## Forgeborn (fire)

The forge of a dead volcano. The caldera wakes, the forge's fire rises as a column, and the pilot is forged in it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Dark caldera lit by its lava, embers rising | macro, low along a seam toward the dais | unseen | `intro2.fire.rumble`; `structure.rumble`, `stone` |
| 1.0 | The cracks light in from the rim to the dais, sparks running down the seams | crane up and back | unseen | `intro2.fire.crack`; `structure.crack`, `stone` |
| 2.15 | **Eruption** (speed ramp): a 9.5 m fire column with a white-hot core, sparks, the forge's joints flaring | low under it, then a crane up it | built in inside the fire | `intro2.fire.erupt`; `blast.fire`, `explosion` / `intro2.fire.column`; `hazard.lava`, `fire` |
| 3.3 | It walks out of the fire, flame peeling off its shoulders and arms | front, pushing in at the lens | keyed walk, the staff upright at its side | `intro2.fire.step` (x2); `impact.stone`, `stone` |
| 4.75 | The column spirals in and gathers into nine licks of flame round its head | orbit framing the crown | `Cast-L-Buff1` | `intro2.fire.crown`; `magic.flame-lance`, `fire` |
| 5.45 | The staff comes down: fire runs out along the floor, the lava rings flare outward | wide and low | `Cast-L-AOE1` | `intro2.fire.slam`; `magic.fire.hit`, `explosion` |
| 6.0 | **Name** (Rise): the hero under the burning crown | hero frame | staff idle | `intro2.fire.name`; `melee.greatsword.hit`, `metal` |

## Still Tide (water)

A drowned hall under the moon. The sea comes in to drown the intruder, and the pilot stops it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Still water between the colonnades, moon shafts and sheens on the water, drips | glide low over the water | staff idle | `intro2.water.drips`; `boss.deepend.drip`, `water` |
| 1.3 | The tide rises, foam along the colonnade's feet | rising with it | staff idle | `intro2.water.rise`; `boss.deepend.tide`, `water` |
| 1.75 | The wave rears behind the pilot (its halves one wall), spray off the crest | low, looking up past it | `Cast-L-Buff2` | `intro2.water.rear`; `magic.tidal-wave`, `water` |
| 2.95 | **The sweep** (ramp): the wave parts down its middle, its halves surging round either side | front and low | `Cast-L-AOE2` | `intro2.water.part`; `boss.deepend.wave`, `water` |
| 3.75 | **Freeze** (ramp): frost races up both walls, they freeze mid-curl, the frost spreads over the water in rings, the spray hangs as glints | orbit through the stopped moment | `Cast-Attack1` (the tap) | `intro2.water.freeze`; `blast.frost`, `shield` |
| 4.85 | The ice settles, frost shivering off the crests | pull back between the walls | staff idle | `intro2.water.settle`; `impact.glass`, `shield` |
| 6.4 | **Name** (Wipe): between the two frozen waves, the breath frosting | centred hero frame | staff idle | `intro2.water.name`; `boss.noctiel.ripple`, `install` |

## Strike Point (lightning)

A summit under a storm. The storm has been waiting for a conductor; the pilot is the strike.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Rain on the slate, St Elmo's fire on the spires, the clouds lit from inside | low and wide, tilting up | unseen | `intro2.storm.bed`; `structure.rumble`, `lightning` |
| 1.1 | Arcs leap spire to spire and crawl in over the stone; the cloud turns into a vortex | straight up into it, turning | unseen | `intro2.storm.charge`; `blast.static`, `lightning` |
| 2.0 | A point of light gathers in the vortex's eye | down from the cloud | unseen | `intro2.storm.gather`; `chip.vacuum`, `dash` |
| 2.45 | **Strike** (ramp): the trunk and its branches come down and are the pilot, white-hot in a crouch (a lag-0 afterimage cooling), arcs run out over the stone | smash to wide and low, then a fast orbit | keyed crouch | `intro2.storm.strike`; `magic.lightning.hit`, `lightning` / `intro2.storm.after`; `blast.shock`, `explosion` |
| 3.7 | It rises, arcs crawling on its limbs | low, rising with it | staff idle | `intro2.storm.crackle`; `status.shock-zap`, `electric-hook` |
| 4.3 | The staff up: a bolt into its head, then to every spire, which fire back into the sky | wide between the spires | `Cast-L-Summon1` | `intro2.storm.call`; `magic.lightning.shot`, `electric-hook-hit` |
| 5.5 | **Name** (Slam): the staff crackling, rain, the storm behind | hero frame | staff idle | `intro2.storm.name`; `blast.quake`, `explosion` |

In Reduced effects the cloud's flashes are held .45 s apart and widened (`Flash`), the bolts re-shape every .42 s, and the
strike's white-out is halved.

## Black Sun (darkness)

An eclipse over a glass plain. The light goes out, the dark comes to one place, and the pilot comes out of it.

| t (s) | Beat | Camera | Pilot | Cue (key; fallback) |
| ---: | --- | --- | --- | --- |
| 0 | Dusk on the obsidian, the sun in the broken gate, the moon's edge touching it | centred through the gate | unseen | `intro2.void.wind`; `boss.noctiel.glide`, `darkness` |
| 1.35 | The moon slides over, the light drains; every monolith's shadow peels off and pours in as ink | a dolly zoom on the centre | unseen | `intro2.void.pour`; `boss.noctiel.eclipse.pool`, `darkness` |
| 2.8 | **Totality** (ramp): the corona's rays, the diamond; the poured dark rises into a void against a violet glare | low, centred, the void under the black sun | unseen | `intro2.void.totality`; `boss.noctiel.eclipse`, `darkness` |
| 3.55 | It steps out of the void, built in as it crosses its face, the dark running off it | locked off | keyed walk, the staff at its side | `intro2.void.emerge`; `blast.implosion`, `dash` |
| 4.55 | One eye of light on the visor | close on the head | head up | `intro2.void.eye`; `boss.noctiel.hush.open`, `install` |
| 5.05 | The staff up, the dark spreading behind its shoulders in feathers edged with violet | slow pull back, centred | `Cast-L-Summon2` | `intro2.void.wings`; `boss.noctiel.wings.open`, `dash` |
| 5.7 | The snap: the dark streaks back to the monoliths, their runes flare | | `Cast-Attack1` | `intro2.void.snap`; `magic.darkness.hit`, `darkness` |
| 6.6 | **Name** (Assemble): centred under the black sun, the eye lit | centred hero frame | staff idle | `intro2.void.name`; `blast.void`, `darkness` |

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
  the staff's anchors finite. `--intro-elemental-probe [id]` prints the anchors and the camera for tuning.

## Choices made overnight (owner asleep)

- The four names (Forgeborn, Still Tide, Strike Point, Black Sun) are new; rename freely in the registry line.
- Still Tide holds on its mark instead of walking out between the walls: its model plays its staff clips throughout (no upper-body
  layer tonight), and a clip never goes back to the keys.
- The water's walls are set pieces of the bank lifted, swung and swapped for their frozen form under the frost front (no live fluid).
- The magic sink's water puffs and the void orb's matte sphere are not on the entrance sink tonight; the water and the void use
  lit puffs, streaks and glows.
