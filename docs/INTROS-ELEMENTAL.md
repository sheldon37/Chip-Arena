# The elemental weapon intros (intros v2, 2026-11-20)

Owner (2026-11-20, before sleeping): "I want to move the intros to a much higher quality standard, great animations and effects,
non-block based background and designs, etc. With great camera work as well!" The four magic weapons' signatures are replaced
by four new intros, each a summoning of its element with a story, a modelled set, an authored camera and a name beat. The old
ones (Cinder Crown, Tidal Serpent, Thunderstep, Eventide Gate) are archived by the framework (`docs/INTROS-V2.md`), their code
kept.

Author: Claude (I5), branch `claude/intro-elemental`. Presentation only: no rule, wire, save or timing change. The framework,
its registry, the archive, the name card, the camera rig, the set bank loader and the sound plumbing are I0's
(`claude/intro-framework`); this branch owns `Core/NativeElementalIntros.cs`, its checks, its set script
`tools/intros/elemental_sets.py` and its sound list.

| Weapon | Intro | Length | Name at | One line |
| --- | --- | ---: | ---: | --- |
| Fire | **Forgeborn** | 7.8 s | 6.0 s | In a caldera forge the cracks race in, a fire column erupts, the pilot walks out of it and its flame gathers into a crown. |
| Water | **Still Tide** | 8.2 s | 6.4 s | In a moonlit flooded hall the tide rises into a wave over the pilot, parts round them at a sweep of the staff and freezes mid-curl. |
| Lightning | **Strike Point** | 7.2 s | 5.5 s | On a storm summit the spires charge, the sky opens and the pilot arrives as the strike itself, then calls a second bolt into the staff. |
| Darkness | **Black Sun** | 8.4 s | 6.6 s | On an obsidian plain the moon covers the sun, every shadow pours to the centre into a void, and the pilot steps out of it with one eye of light. |

Each is distinct in set, palette and camera:

| | Set | Palette (game colours) | Camera language |
| --- | --- | --- | --- |
| Forgeborn | caldera forge: basalt plates, lava seams and rivers, basalt column clusters, a crater rim with lava falls, a half-buried forge arch | basalt black, the fire weapon's orange `#ff6c32`, the blasts' flame yellow, ember red; smoke brown sky | heavy and grounded: long lenses low to the floor, slow pushes, one great crane up the column and down onto the crown |
| Still Tide | flooded hall: a drowned colonnade, arches, a ribbed vault with broken skylights, a rose window, still black water | the water weapon's cyan `#63e2ff`, foam `#eaffff`, deep `#2b8bd6`, moonlight `#a9c8ff`; teal-black | gliding: dollies at the water line, a bullet-time orbit inside the frozen moment, a rising pull-back between the frozen walls |
| Strike Point | storm summit: a fractured slate plateau, six conductor spires with copper bands, a fulgurite crater, peaks through the cloud sea | the lightning weapon's violet `#c8aaff`, core `#f3ecff`; storm slate-blue | violent and vertical: looking straight up into the vortex, a top-down from the cloud down the bolt, wide and low at the strike, a fast orbit with a speed ramp |
| Black Sun | eclipse plain: an obsidian floor, ash dunes, seven leaning monoliths with glyph seams, a great broken gate, far mesas | the darkness weapon's `#af6eff`, the void orb's rim `#c04dff` and lens `#d9a6ff`; lilac-grey dusk to black | still and symmetrical: centred frames, a dolly zoom as the eclipse completes, a locked-off frame for the void, an extreme close-up on the eye |

## Rules every one keeps

- **Length and name:** 6-8.5 s each, the name on the live display name through I0's name card at `NameAt`, the hero frame held
  still from the name to the end. The match's hold rules are unchanged.
- **Effects:** rich but readable. Streaks over about 1 m (bolts, the corona's rays, the column's tongues, beams) are laid on their
  head's camera plane (`NativeCacheOpenFx.OntoHeadPlane`). No ring outlines (no `WeaponShape.Ring`, no ring glow profile, no flat
  discs or rings on the floor); a shockwave is puffs and streaks. In Reduced effects nothing flashes faster than 3 Hz (the
  lightning's flickers and the eclipse's diamond flash are held to one slow swell), the camera never shakes, and every beat
  still reads.
- **Motion:** the model's own staff clips from the motion bank (`2Hand-Staff-*` casts, `Armed-Walk-Slow` under them), retimed and
  layered in code; no new pack clip was needed.
- **Sets:** scripted Blender into a local bank (`ChipArenaIntroElemental.bytes`, never committed). Without it each intro plays on
  the framework's dark lit void with its own light, fog and effects; never the old block arena.
- **Sound:** each intro has its own cue timeline on its beats (below). Generation is approved (ElevenLabs); until the clips are
  in, every cue plays the existing sound named beside it.

## Forgeborn (fire)

The forge of a dead volcano. Story: the caldera wakes, the forge's fire rises as a column, and the pilot is forged in it.

| t (s) | Picture | Camera | Pilot | Sound |
| ---: | --- | --- | --- | --- |
| 0 | Dark caldera, embers rising; a lava seam's glow under a basalt plate | macro, 0.25 m off the floor, sliding along the seam toward the centre (30 mm-ish, FOV 30) | unseen | `fire.rumble` (low magma rumble) |
| 1.1 | The seams crack open in sequence, racing in from the rim to the dais | crane up and back to reveal the caldera, the forge arch behind | unseen | `fire.crack` (rock cracking, rising) |
| 2.15 | **Eruption**: a fire column 9 m high bursts from the dais vent, the whole caldera lit; slow-motion beat | low wide, looking up the column | a dark silhouette forms inside it | `fire.erupt` (a deep whoomph and roar) |
| 2.9 | The column roars; flame tongues climb it; embers and heat haze | the great crane: from the feet up the column's length | silhouette clearer | `fire.column` (roaring flame bed) |
| 3.5 | The pilot walks out of the fire, flame peeling off shoulders and arms, embers in its steps | front, medium, slow push in as it walks at the lens | `Armed-Walk-Slow` legs under the staff idle, two steps | `fire.step` x2 (stone footfalls) |
| 4.7 | The column's fire spirals up and gathers above the head into a crown of flame tongues | orbit to a low three-quarter, the crown framed | `Cast-L-Buff1` (the staff raised) | `fire.crown` (a swirling whoosh igniting) |
| 5.45 | The staff comes down on the dais: fire runs out along every seam | wide and low | `Cast-L-AOE1` (staff to the floor) | `fire.slam` (stone strike and fire rush) |
| 6.0 | **Name**: hero under the burning crown, embers, the lava rivers bright | still hero frame, low, the arch behind | staff idle, head up | `fire.name` (a forge hammer ring and flare) |

## Still Tide (water)

A drowned hall under moonlight. Story: the sea comes in to drown the intruder, and the pilot stops it.

| t (s) | Picture | Camera | Pilot | Sound |
| ---: | --- | --- | --- | --- |
| 0 | Still black water between the colonnades, moon shafts from the broken vault, drops falling | glide low over the water (0.35 m) down the hall toward a figure | staff idle, head bowed | `water.drips` (cave drips, echo) |
| 1.4 | The tide rises: the water climbs the columns, a swell runs the hall | rising crane behind the pilot | staff idle | `water.rise` (a deep swelling surge) |
| 2.2 | A wave rears up behind the pilot, 6 m, its lip curling | low, looking up past the pilot at the wave | `Cast-L-Buff2` (the staff raised) | `water.rear` (a wave drawing up, roaring) |
| 2.9 | **The sweep**: the wave comes down and parts round the pilot in two curling walls; slow-motion beat | front and low, the walls splitting either side | `Cast-L-AOE2` (the staff swept) | `water.part` (a crash tearing in two) |
| 3.7 | **Freeze**: the staff taps the water; frost races up both walls, they freeze mid-curl, the spray hangs as glints | bullet-time orbit, 200 degrees round the pilot between the still walls | `Cast-Attack1` (the tap) | `water.freeze` (a crystalline crackle and chime) |
| 4.9 | The pilot walks out between the frozen walls, breath frosting | slow pull back ahead of it | `Armed-Walk-Slow`, two steps | `water.step` (wading steps) |
| 6.4 | **Name**: framed between the two frozen waves in the moonlight, drops still hanging | still hero frame, centred | staff idle | `water.name` (a deep glass bell) |

## Strike Point (lightning)

A summit under a storm. Story: the storm has been waiting for a conductor; the pilot is the strike.

| t (s) | Picture | Camera | Pilot | Sound |
| ---: | --- | --- | --- | --- |
| 0 | Rain on the slate, the clouds lit from inside, the spires humming with St Elmo's fire | ground level, wide, tilting up to the spires | unseen | `storm.bed` (rain and distant thunder) |
| 1.2 | Arcs leap spire to spire and crawl toward the centre; the cloud above turns into a vortex | straight up from the centre into the vortex, turning | unseen | `storm.charge` (rising electric whine) |
| 2.2 | A bright point gathers at the vortex's eye | top-down from the cloud, the summit below | unseen | `storm.gather` (a sucking hiss) |
| 2.45 | **Strike**: the bolt comes down with branching forks and is the pilot, landing in a crouch; slow-motion beat | smash down the bolt to wide and low at the strike | white-hot silhouette cooling into the body, `2Hand-Staff-Land` into the crouch | `storm.strike` (a thunder crack at the lens) |
| 2.7 | The branches hang and die in the air, arcs run out over the stone, rain thrown out in a burst | fast orbit round the crouch, speed ramp | crouch hold | `storm.after` (sizzle and rolling thunder) |
| 3.8 | It rises, arcs crawling over its limbs | low, close, rising with it | crouch to stand | `storm.crackle` (electric crackle) |
| 4.3 | The staff up: a second bolt into its tip, then to every spire, which fire bolts back to the sky | wide, the spires round it | `Cast-L-Summon1` | `storm.call` (bolt, then six snaps) |
| 5.5 | **Name**: hero with the staff crackling, rain, the storm behind | still hero frame, low | staff idle | `storm.name` (a deep rolling thunder) |

## Black Sun (darkness)

An eclipse over a glass plain. Story: the light goes out, the dark comes to one place, and the pilot comes out of it.

| t (s) | Picture | Camera | Pilot | Sound |
| ---: | --- | --- | --- | --- |
| 0 | Dusk on the obsidian plain, long monolith shadows, the sun framed in the broken gate, the moon's edge touching it | wide, symmetrical, through the gate, slow push | unseen | `void.wind` (thin cold wind) |
| 1.3 | The eclipse: the moon slides over, the light drains; the shadows peel off the ground and pour toward the centre like ink | dolly zoom on the centre as the shadows pour in | unseen | `void.pour` (a low draining rush) |
| 2.8 | **Totality**: the corona blooms round the black sun; the poured dark rises into a void at the centre | low, centred, the void and the black sun in one line | unseen | `void.totality` (a deep choral swell, then silence) |
| 3.7 | The void opens; the pilot steps out of it, the dark running off its body | locked-off, front, the void filling the frame | `Armed-Walk-Slow` steps, the body revealed from the front | `void.emerge` (an inverse rush and a step) |
| 4.5 | One eye of light ignites on the visor | extreme close-up on the eye | head up | `void.eye` (a single high glass tone) |
| 5.0 | The staff up; the dark wraps round the shoulders like wings, then snaps back to the monoliths | slow pull back, centred | `Cast-L-Summon2` | `void.wings` (cloth-like whoosh, a snap) |
| 6.6 | **Name**: centred under the black sun, rim lit, the eye shining | still hero frame, symmetrical | staff idle | `void.name` (a low struck tone) |

## Sounds

Generated with ElevenLabs where the batch runs (owner: "Attempt to generate them if possible, if not, use existing sounds and
ensure things sync properly"); every cue has an existing fallback so the intros are never silent. Keys `intro.<element>.<cue>`.

| Key | Fallback (existing) | Prompt |
| --- | --- | --- |
| `intro.fire.rumble` | `boss.ironworks.arrive.rumble` | A deep volcanic rumble under the ground with slow lava bubbling, isolated, no music |
| `intro.fire.crack` | `boss.ironworks.crust.crack` | Hot rock cracking open in a fast rising sequence, crackling and splitting, isolated, no music |
| `intro.fire.erupt` | `blast.fire` | A huge column of fire erupting from the ground, a deep whoomph into a roar, isolated, no music |
| `intro.fire.column` | `field.fire` | A roaring pillar of flame, steady and powerful, isolated, no music |
| `intro.fire.step` | `boss.noctiel.step` | One heavy boot step on hot stone with a crackle of embers, isolated, dry, no music |
| `intro.fire.crown` | `fire` (weapon) | Flames swirling upward and igniting into a crown, a whooshing spiral and a soft flare, isolated, no music |
| `intro.fire.slam` | `boss.ironworks.slam.wave` | A staff striking stone and fire rushing out along cracks, isolated, no music |
| `intro.fire.name` | `metal` | A single forge hammer ring on an anvil with a warm flare of fire, isolated, no music |
| `intro.water.drips` | `boss.deepend.drip` | Slow water drips in a large stone hall, a faint echo, isolated, no music |
| `intro.water.rise` | `boss.deepend.tide` | Deep water rising and surging through a stone hall, swelling, isolated, no music |
| `intro.water.rear` | `boss.deepend.seiche.draw` | A great wave drawing up and rearing, a rising roar of water, isolated, no music |
| `intro.water.part` | `boss.deepend.wave` | A huge wave crashing and tearing in two around a point, isolated, no music |
| `intro.water.freeze` | `blast.frost` | Water freezing instantly, a crystalline crackle racing upward ending in a bright chime, isolated, no music |
| `intro.water.step` | `water` (weapon) | Two wading steps through shallow water, isolated, no music |
| `intro.water.name` | `boss.noctiel.ripple` | A deep glass bell struck once over still water, long soft tail, isolated, no music |
| `intro.storm.bed` | `field.storm` | Heavy rain on stone with distant rolling thunder, isolated, no music |
| `intro.storm.charge` | `boss.station.arc` | A rising electric whine and crackle as static builds, isolated, no music |
| `intro.storm.gather` | `chip.vacuum` | A sucking reverse hiss gathering into a point, isolated, no music |
| `intro.storm.strike` | `blast.shock` | A lightning strike right here, a blinding thunder crack, isolated, no music |
| `intro.storm.after` | `blast.static` | Sizzling electricity and rolling thunder after a strike, isolated, no music |
| `intro.storm.crackle` | `lightning` (weapon) | Electric arcs crawling and snapping over metal, isolated, no music |
| `intro.storm.call` | `electric-hook-hit` | A lightning bolt into a staff then six quick electric snaps, isolated, no music |
| `intro.storm.name` | `blast.quake` | A long deep rolling thunder, isolated, no music |
| `intro.void.wind` | `boss.noctiel.glide` | A thin cold wind over an empty plain, isolated, no music |
| `intro.void.pour` | `boss.noctiel.eclipse.pool` | A low draining rush, like shadows pouring away, isolated, no music |
| `intro.void.totality` | `boss.noctiel.eclipse` | A deep wordless choral swell cut to silence, isolated, no music |
| `intro.void.emerge` | `blast.implosion` | An inverse rush of air into a point and one soft footstep, isolated, no music |
| `intro.void.eye` | `boss.noctiel.hush.open` | A single high glass tone, pure and cold, isolated, no music |
| `intro.void.wings` | `boss.noctiel.wings.open` | A heavy cloth-like whoosh opening and a sharp snap back, isolated, no music |
| `intro.void.name` | `darkness` (weapon) | A low struck metallic tone with a dark shimmer, isolated, no music |

## Choices made overnight (owner asleep)

- The four names (Forgeborn, Still Tide, Strike Point, Black Sun) are new; rename freely in the registry line.
- Motion uses only clips already in the motion bank; nothing was rebaked.
- The water's moving walls are set pieces of the bank lifted and cross-faded into their frozen form under the frost front (no
  live fluid sim): it reads as a wave and stays within budget.
