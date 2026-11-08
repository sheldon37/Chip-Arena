# Expedition bosses: the Backrooms — THE HUM (Level 0) and THE DEEP END (the Poolrooms)

Owner, 2026-11-05: "can you begin development on Level 0 and poolrooms bosses? Give them each a high quality intro (the same kind of
method we use with Siren, so like a player intro but transitions into stage music). Make them backrooms lore accurate based on the
level. Give them high quality designs, high quality effects, beautiful animations and make them difficult. I want to prevent cheese
methods like 'safe' spots."

**Status: designed by Claude while the owner slept and built behind DEV previews (section 9). The owner has not reviewed it; every choice
below is a recommendation and easy to change.** The split is the usual one. Claude builds the model, motion, effects, intro, music and a DEV preview behind a view that
returns None until bound. Astra owns rules, AI, hit volumes, numbers, generator changes, collision and networking (section 8). Every
number here is a proposal for Astra.

The levels' own files: `docs/BACKROOMS.md`, `docs/EXPEDITION-ENEMY-REWORK.md`. The worked examples these follow:
`docs/EXPEDITION-BOSS-CENTRAL-STATION.md`, `docs/EXPEDITION-BOSS-CITY-IRONWORKS.md`, `docs/BOSS-MOTION.md`, `docs/ZONE-BOSS-MUSIC.md`,
`docs/ZONE-BOSS-RULES-HANDOFF.md`.

## 1. The rule both fights are built round: no safe place

The owner's words are "prevent cheese methods like safe spots". So a boss never relies on its pathing or its reach to threaten a
position. Each fight has a **lore reason** why no place in the room is safe, and each kind of hiding has its own named answer:

| The cheese | THE HUM's answer | THE DEEP END's answer |
| --- | --- | --- |
| Standing in a corner, a nook or behind a prop | **Fall Through:** the carpet tears under anyone still for 6 s, wherever they stand | **The Lull:** hands take anyone still for 6 s, wherever they stand |
| Hiding behind walls | Walls don't exist to it. **Noclip** and **Max Hum** go straight through them, and partitions move when nobody is looking (**Peripheral Shift**) | There are no walls to hide behind: the room is one open basin |
| Hugging a wall | **Wall Hand:** an arm comes through the wall within 2.5 m of you | **Seiche:** the wave is tallest at the walls and reflects back, so a wall takes two hits. **Last Lap** runs a rounded square, so the corners are on its lap too |
| Hugging the boss, standing under it | **Stoop:** a 4.5 m slam at its own feet | **Thrash:** contact damage and a 5 m tail sweep while surfaced |
| Sniping from far away | **Noclip** closes any gap, and its reach grows with distance | **Breach** comes up under you, and **Hydro Jet** picks the farthest pilot. It is under water, and immune, unless it is near someone |
| Standing on something high | Nothing is climbable: 4 m ceiling, no pillars or props in the room. Over 1.6 m for 2 s draws **Fall Through** from the ceiling | Nothing is climbable: no chairs or boards with collision. Same 1.6 m rule draws **Breach** |
| Doorway chokes, leaving the room | The room seals as it arrives (doorways papered over). Anyone outside falls back in, through the ceiling | The room seals (the arches flood shut). Anyone outside is washed back in |
| Dry ground | n/a | **The tide** covers every deck by phase 2. In phase 3 the whole room is chest deep |
| Running out the clock, hit and hide | Max Hum-Buzz at 6:00: pulses every 5 s until someone dies | The Flood at 6:00: the tide rises to the ceiling and kills |
| A spawn funnel for the adds | Adds peel out of the walls round each pilot, not from a door | Adds surface round each pilot, not from a door |
| Being out of the map, inside geometry | The authority checks each pilot against the room's volume every second; outside it, they fall back in and take a hit | The same; they're washed back in |
| The boss stuck on geometry | It has no navigation: nothing can stick it | It has no navigation: it swims on a path through the tile |

**A check holds this to account:** `Tests/BackroomsBossSafeSpotChecks.cs` sweeps every cell of each arena, for a pilot standing still,
then for one circling, through the move tables' shapes. A place that nothing threatens within the phase's limit fails the check. Astra
should run the same sweep against the real rules (section 8).

**Difficulty, proposed:** harder than THE 6:47. A one second global gap between moves (it's about three there), telegraphs of the same
fair length, and overlaps allowed between one big and one small move. Everything is still dodgeable by moving: a telegraph must always
say where the danger is, and nothing is hidden from a player who's watching and listening. Punish windows stay, but they are shorter.

## 2. Lore

Sources read 2026-11-05: the Backrooms Wikidot pages for Level 0 and Level 37 ("Sublimity"), and the original post's description of the
place. This is fan fiction with many versions, so these are the common, recognisable ideas, in our own words and our own designs. Nothing
here copies a specific creature from a film, game or web series.

**Level 0, "the Lobby".** Endless segmented yellow rooms, the smell of old wet carpet, and above all the fluorescent lights at their
maximum hum-buzz. The famous warning is that if you hear something wandering nearby, it has certainly heard you. Space there behaves
impossibly:
- You get in by **noclipping**, falling through a floor or wall out of reality.
- The layout shifts whenever nobody is looking (the **Peripheral Shift**).
- **Blackout zones** where the only guide is distant buzzing, **pit grids** that swallow light.
- **Scratching from inside the walls**, and familiar voices.

**Level 37, the Poolrooms, "Sublimity".** Warm tiled halls, always brand new, that can't be broken. Lukewarm blue-green water that
ripples with nothing to disturb it, as if there were tides. Staircases descend straight into deep dark water. Sound fades: echoes die
and voices muffle, until the water grows loud enough to drown out everything. Light comes from odd angles. It is dreamlike and
soothing, and that's the danger: no life has ever been recorded there, and people drown in its depths.

## 3. THE HUM (Level 0)

**The concept.** The thing that has been wandering nearby. It's what the Lobby's light is *for*: the level's fluorescent circuit grown a
body out of wet carpet and wallpaper, walking the rooms and listening. It has no face. Where a face should be, it has one recessed
ceiling fixture, a long horizontal bar of light that buzzes.

**The silhouette.** Very tall and very thin in a room too low for it, stooped so its back scrapes the ceiling tiles. A long black shape
with a bright white horizontal bar for a head, readable at any distance against the mono-yellow, and in the dark as nothing but the bar
and a line of tubes down its spine.

| Measure | Metres |
| --- | --- |
| Back of the stoop, tubes included (the 4 m ceiling scrapes it) | 3.95 |
| Head fixture, centre | 3.3, 1.3 forward of the chest on a long curved neck |
| Head fixture | 1.6 wide x .4 x .3 |
| Arm, shoulder to knuckle | 3.4 (1.62 + 1.62 + .16), plus fingers of about a metre |
| Legs | 2.2 from the hip to the floor, on a raised heel (thigh 1, shin 1.02, a long foot) |
| Armspan | 7.6 |
| Upright (only when it rears: head and shoulders go up through the ceiling) | 5.6 |

**Materials.** All from the level's own palette, which is what makes it look grown from the room:
- **Hide:** soaked black-brown carpet pile with sodden patches, shining wet (not fur: dense matted pile, damp, darker where it's wet).
- **Wallpaper:** long peeling strips of the level's pale chevron paper hanging from shoulders and hips like a torn shroud, with the
  plaster-backed reverse showing as they curl.
- **Ribs:** bent ceiling T-grid, with acoustic ceiling tiles hanging off it, broken and stained.
- **Fingers:** a metre long, jointed lengths of red-brown baseboard and bent T-rail. They scratch. It stands on them: each hand is an arch on four fingertips, the wrist .4 m up, not a knuckle-walk (changed from the first design: the arched hands read far better at game distance and in the dark).
- **Spine:** seven fluorescent tubes along its back in old ballast housings, set in a herringbone that echoes the wallpaper's chevrons, each with a bare wire tail. They buzz and
  flicker, and light the room as it walks.
- **The Ballast:** a transformer the size of a chest in the ribcage, hot amber through the ribs. It's the heart and the sound.
- **The head:** a real recessed fixture: steel housing, a prismatic diffuser, two tubes behind it. When it attacks, the diffuser
  splits along its length into a jaw, packed with broken tube glass.

Lit parts: the head, the seven tubes, the Ballast. In the dark, those are all you can see, and that's deliberate.

**Weak points and breaks.**
- **The head:** 2x while it stoops low (every Reach and Stoop wind-up brings it down in reach).
- **The Ballast:** 1.5x, 2.5x while Max Hum charges (the ribs open).
- **The seven tubes:** each is breakable (2.5% of max health). A broken tube stays dark and sparks. Three broken during a Max Hum charge
  chokes the pulse and staggers it for 2 s.
- **Clipped:** a Noclip lunge that ends within 1 m of a partition or wall leaves it half inside the surface for 2.2 s, glitching, taking
  2x. Players can bait this: stand in front of a wall and sidestep.

### Phases

| Phase | Health | The room | New moves |
| --- | --- | --- | --- |
| **HUM-BUZZ** | 100-65% | Lit, flat yellow. Partitions in their first layout. It walks on the floor | Reach, Stoop, Row Burst, Noclip, Fall Through, Peripheral Shift |
| *change at 65%* | | The hum climbs to a scream, then stops. Every lamp dies, row by row toward the middle | |
| **BLACKOUT** | 65-30% | Dark but for flickering circuits and its own tubes. Facelings in the walls | + Max Hum, Lights Out, Wall Hand, Familiar Voices |
| *change at 30%* | | It rears up through the ceiling, tiles rain, and the room's textures start to slip | |
| **NOCLIP** | 30-0% | Lights strobe back wrong. It walks on the ceiling as often as the floor. The carpet tears in places and stays torn (pit grid) | Everything faster; double Row Burst; Noclip chains twice; Shift every 8 s |

Each change is a 3.5 s scripted window, as the 6:47's are.

### Moves

`Wind` is the telegraph. Times in seconds, damage in the same units as THE 6:47's moves, before party scaling.

| Id | Move | Phase | Wind | Strike | Follow | Recover | Cooldown | Damage | Shape |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `reach` | Reach | 1 | .48 | .16 | .24 | .42 | 3 | 22 | 130° arc, 6 m, through walls |
| `stoop` | Stoop | 1 | .7 | .15 | .4 | .6 | 7 | 30 | 4.5 m circle at its feet |
| `row` | Row Burst | 1 | 1.1 | .1 | .5 | .4 | 10 | 16 | every fixture on alternate rows of one axis bursts: lanes 1.4 m wide, the whole room long |
| `lunge` | Noclip | 1 | .75 | .8 | .4 | .7 | 12 | 34 | a 3 m wide lane, 26 m at 32.5 m/s, through everything; it follows its target through the wind and holds its line .3 s before it goes |
| `tear` | Fall Through | 1 | 1.5 | .2 | 0 | 0 | meter | 12 + moved | 2.4 m circle under a still pilot; see below |
| `shift` | Peripheral Shift | 1 | 0 | 0 | 0 | 0 | 18 / 12 / 8 | 0 | partitions rearrange where no pilot is looking |
| `pulse` | Max Hum | 2 | 1.4 | 1.3 | .3 | .8 | 14 | 14 + .5 s stagger | a ring from its chest, through walls, to the room's edge at 14 m/s; jump it or roll through it |
| `cut` | Lights Out | 2 | 1.2 | .3 | .4 | .6 | 16 | 38 | its tubes and the hum stop dead; 1.2 s later it strikes a 3.5 m arc behind the target |
| `hand` | Wall Hand | 2 | .8 | .25 | 1.5 | .6 | 15 | 8 + 8/s | a grab within 2.5 m of any wall or partition, dragging the pilot to it. 120 team damage or 40 from the victim breaks it |
| `voices` | Familiar Voices | 2 | 1 | 2 | 0 | .5 | 25 | 0 | Facelings peel out of the walls round each pilot |

- **Fall Through.** Each pilot has a meter: within 3 m of the same spot for 6 s (or over 1.6 m up for 2 s) and the carpet goes black under
  them, then tears. They fall through the floor and drop out of the ceiling above the boss, stunned .6 s. It's the anti-camp move, and it
  can't be avoided by standing anywhere, only by moving. In NOCLIP the meter is 4 s.
- **Listening.** Its target weight favours whoever made the most noise lately (shots fired, explosions) and then whoever is farthest. It
  turns its head to a shot before its body.

### The tells are the point

Sound and light are its language, and every tell is learnable:
- The **hum** rises through every wind-up. Its **pitch** says how soon.
- **Silence is the worst thing you can hear.** Lights Out is the only move that cuts the hum: the whole level goes quiet for 1.2 s.
- **Row Burst:** the row's fixtures brighten and buzz for 1.1 s before they burst, so the danger lanes are lit and you stand between
  them. The axis changes each time, so a safe line never stays safe.
- **Noclip:** it sinks into the nearest wall, hand first. Scratching travels inside the walls, the paper bulges along the path, and lamps
  on the lane dip in sequence, then it comes out at speed.

### The arena: the Lobby room

Around the exit. **12 x 12 cells (38.4 m square), the 4.0 m ceiling, no pillars.** Fixtures every other cell, in the level's own grid,
on eight circuits it can kill one by one. **Eight free-standing partition segments** (2 cells long, papered both sides, to the ceiling)
give cover from sight and from Row Burst lanes, never from the boss, and they move. Each has two places in a table in
`Core NativeHumArena`; in their first places a clear hall runs down the middle of the room (the intro looks down it); the checks flood all 256 mixes and none leaves a sealed pocket. Doorways are papered over as it arrives; on its death one door in
the wallpaper opens onto the exit cache.

### The intro: "It has heard you"

The Siren's method: a directed, multi-shot entrance on the 9.0678 s intro hold, in the boss's own time, with the same style of card (a
top line, a progress bar, the title rising letter by letter at the name time), ending as the boss's own song drops into the fight.

| Boss time (s) | Shot | What happens | Sound |
| --- | --- | --- | --- |
| 0-2.2 | Wide at the room's edge, eye height, very slow push | Flat yellow, nothing there. The far row of lights goes out. Then the next, nearer, on the melody's notes | The stage music has stopped dead. Hum-buzz alone, rising. The boss song's opening comes in under it |
| 2.2-4.2 | Close and low on one wall | The paper bulges, and something scratches inside. Four long fingers push through as the paper stretches and tears open onto black | Scratching, tearing |
| 4.2-6.2 | Long lens, side on, its whole height | It walks out through the wall without opening it. Dark but for what's coming: the seven tubes strike up its spine one at a time | Tube starters ticking, each louder |
| 6.2-8.0 | Slow push on the head from below | The head tube flickers and catches, the bar of light steadies, it turns to look. The diffuser splits. **The card rises at 6.43** | The hum at maximum over the song's build |
| 8.0-9.07 | Whip back to the party's view | It straightens, head through the ceiling, then stoops. Every light in the room slams to full on the drop | The song drops at 9.0678, into the fight |

Reduced effects: no strobing; the row-by-row dimming stays.

### Death (fits the 4 s victory hold)

The tubes pop one at a time from the top down. The arms give way and it folds forward, slowly, onto its knuckles. The head fixture
hangs, buzzing, the last light in the room. The hum falls in pitch and stops. **Silence**, for the first time since the level began,
then one by one every lamp in the room comes back, ordinary and quiet. A door in the wallpaper is open.

### The cue keys

Silent until clips exist: they go through the effects bank (Core `NativeHumBossCues`), queued from the record and the rig the picture drew, at the moments the table below says. 2D keys (the hum, the silence, the slam on, the ring, Lights Out, the death, the walls) play flat; the rest at their place.

| Key | Brief |
| --- | --- |
| `boss.hum.hum.loop.rise` | the fluorescent hum-buzz climbing alone in an empty room; loopable, pitched by the stage's Pitch |
| `boss.hum.silence` | a low shelf cut: the room going quiet; used before Lights Out |
| `boss.hum.row.out` | a row of fixtures clicking off, far away, with a sag in the hum |
| `boss.hum.scratch` | fingernails on the back of wallpaper, close and dry |
| `boss.hum.tear` | wallpaper tearing slowly, plaster dust, then a wet rip |
| `boss.hum.starter` | a tube starter ticking: small, each one louder |
| `boss.hum.tube.strike` | a tube striking on, one rising buzz |
| `boss.hum.head.catch` | the head's two tubes catching: a heavy flicker then a steady bar |
| `boss.hum.jaw` | the diffuser splitting: plastic flexing, glass grinding |
| `boss.hum.ceiling` | ceiling grid wrenching and tiles breaking over its shoulders |
| `boss.hum.slam.on` | every lamp slamming on: one huge thunk of relays on the song's drop |
| `boss.hum.reach.windup` | a long arm drawing back through wet carpet |
| `boss.hum.reach` | a long whip of air and a wet heavy slap |
| `boss.hum.stoop.rise` | the back pressing the ceiling tiles, creaking |
| `boss.hum.stoop` | a double impact on wet carpet, the room's lamps jumping |
| `boss.hum.row.buzz` | fixtures brightening and buzzing louder over a second |
| `boss.hum.row.burst` | a row of fixtures exploding in order down the room |
| `boss.hum.glass` | safety glass raining onto carpet |
| `boss.hum.noclip.sink` | something heavy pressing into plaster, scratching inside it |
| `boss.hum.noclip.walls` | scratching running fast along the inside of the walls |
| `boss.hum.noclip` | a torn rush of air and plaster: it comes through at speed |
| `boss.hum.brake` | hands dragging on carpet; a skid |
| `boss.hum.carpet` | carpet backing tearing inward, slow and close |
| `boss.hum.fall` | a body falling a long way, then a drop out of the ceiling |
| `boss.hum.shift` | a flicker's click and, somewhere, a heavy wall set down |
| `boss.hum.charge` | the hum rising to a strain, ribs creaking open, a transformer loading up |
| `boss.hum.pulse` | a ring of noise: the hum's whole spectrum sweeping past, wall to wall |
| `boss.hum.cut` | every sound stopping dead |
| `boss.hum.strike.dark` | a tube flash, a whip, a heavy blow from behind you in the dark |
| `boss.hum.hand.scratch` | nails inside the wall right by the ear |
| `boss.hum.hand` | an arm bursting through wallpaper and gripping |
| `boss.hum.voices` | many muffled familiar voices inside the walls |
| `boss.hum.scream` | the hum climbing to a scream, then stopping |
| `boss.hum.die.row` | one row of fixtures dying with a ballast's thud |
| `boss.hum.rear` | its back going up through the ceiling: grid, plaster, a groan of steel |
| `boss.hum.tile.rain` | tiles raining and breaking on carpet |
| `boss.hum.step` | one heavy wet footfall |
| `boss.hum.knuckle` | a knuckle of baseboard and rail planting on carpet |
| `boss.hum.scratch.idle` | a fingernail drumming now and then |
| `boss.hum.hurt` | a wet thud and the tubes stuttering |
| `boss.hum.tube.break` | a tube bursting and its wires spitting |
| `boss.hum.head.break` | the head's panel cracking |
| `boss.hum.choke` | the Ballast choking: a pitch collapse, a stumble |
| `boss.hum.clip` | stuck in a wall: paper stretching, a glitching buzz |
| `boss.hum.death` | the hum falling in pitch and stopping |
| `boss.hum.tube.pop` | a tube popping, glass |
| `boss.hum.fold` | its arms giving way: wet carpet folding, rails bending |
| `boss.hum.lamp.back` | one lamp coming back on, ordinary and quiet |

## 4. THE DEEP END (the Poolrooms)

**The concept.** The level has no recorded life, and this isn't really alive: it's the pool. A vast, smooth eel-like thing, glazed in
perfect white tile, that moves through the water and through the floor as if the tile were water too. It is blind, silent, unhurried and
beautiful, until it isn't. Its mouth is a drain.

**The silhouette.** A serpent 32 m long and 2.4 m thick, in a room 40 m across, rising in long arcs. A round black mouth ringed in
chrome, with two curved chrome rails like mandibles. Lit from within by a row of round underwater pool lamps along each flank. From
above, in dark water, those lamps make a slowly turning spiral of light.

| Measure | Metres |
| --- | --- |
| Length, mouth to tail | 32 |
| Body diameter | 2.4, tapering to .5 at the tail |
| Head | 2.8 wide; mouth 2.2 across |
| Highest it rises above the water | 5.5 (the room's ceiling is 7.2) |

**Materials.** All from the level. It's the cleanest thing in the game, which is what makes it wrong:
- **Skin:** real geometry, not a texture: thousands of glazed white 15 cm tiles laid on the body, with teal grout, wet and gleaming. Tiles
  shrink and stretch round its bends. The belly is pale turquoise basin tile.
- **Depth markers:** black tile lettering down both upper flanks, mirrored so it reads from either side, counting up toward the head: `3 FT`, `5 FT`, `9 FT`,
  `NO DIVING`; across the crown in two lines `DEEP` `END`, reading from in front of the mouth, so it says it as it comes at you. A black lane line runs down the spine.
- **Flank lamps:** twelve round pool lights, six a side, with chrome bezels, turquoise-white, lighting the water round them, set in a pool's bullnose coping running down each flank, with small running lights (about twenty a side, not breakable) between them: from above, they are what makes the spiral.
- **Head:** blunt and eyeless, smooth tile, no features but the mouth. The mouth is an iris of nine curved chrome grate blades, and behind it a round drain grate turning the other way.
  Behind them, a throat lit blue going back further than the body should allow.
- **Mandibles:** two chrome pool-ladder rails, curved forward, dripping.
- **Fins:** two long side fins and a low dorsal crest of white fibreglass with chrome grab-rail edges.

**Weak points and breaks.**
- **The throat:** 2x while the mouth is open (Hydro Jet, The Drain, Breach's rise).
- **The twelve flank lamps:** each breakable (2% of max health). A broken lamp goes dark and leaks a cloud of dark water. Four broken
  staggers it for 3 s on the surface (one time per phase).
- **Under water it's immune**, for at most 6 s at a time, so it can't stall the fight.
- **Stranded:** a Belly Flop onto dry deck in phase 1 strands it for 2.5 s, thrashing.

### Phases

| Phase | Health | The room | New moves |
| --- | --- | --- | --- |
| **THE SHALLOW END** | 100-65% | Quiet, bright, dry decks. It only surfaces where there's water | Breach, Hydro Jet, Belly Flop, The Drain, The Lull |
| *change at 65%* | | The drain reverses. Water wells up the steps and spreads over every deck | |
| **HIGH TIDE** | 65-30% | Water .5 m over every deck: no dry ground, and it can surface anywhere | + Seiche, Whistle |
| *change at 30%* | | The lights in the ceiling go out, leaving only the ones under water. The tide comes up to the chest | |
| **THE DEEP END** | 30-0% | Chest deep (1.1 m) everywhere, half speed. The pit's current never stops | + Last Lap; everything faster |

The speed rule is Astra's existing one (`NativePoolroomsWater.Speed`): depth slows you, but jumps, rolls and abilities still work. So
the fight gets slower on your feet and those tools matter more.

### Moves

| Id | Move | Phase | Wind | Strike | Follow | Recover | Cooldown | Damage | Shape |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `breach` | Breach | 1 | 1.1 | .25 | .5 | .6 | 5 | 28 + launch | 3.2 m circle at the target's position when it locked, mouth up, then the body arcs over and dives |
| `jet` | Hydro Jet | 1 | 1.0 | 2.2 | .3 | .8 | 12 | 10 per .25 s | a 2 m beam, swept 110° over 30 m, starting on the farthest pilot |
| `flop` | Belly Flop | 1 | 1.8 | .2 | .6 | 1 | 14 | 30 | a line 4 m wide and 24 m long where its body falls; its shadow and a line of bubbles or spray mark it |
| `thrash` | Thrash | 1 | .4 | .3 | .2 | .4 | 6 | 18 | 5 m round the surfaced body, a tail sweep, when someone hugs it |
| `drain` | The Drain | 1 | 1.4 | 4 | .3 | .8 | 20 | 40 at the mouth | the whole room's water runs toward the pit at 2.5 to 4.5 m/s, a current a pilot fights (none in the air or a roll); 3% of its health to the open throat shuts it, a 2 s stagger |
| `lull` | The Lull | 1 | 1.5 | .3 | 1.5 | 0 | meter | 8 + 8/s | hands of water take a pilot still for 6 s, or above 1.6 m for 2 s |
| `seiche` | Seiche | 2 | 1.4 | 7 | 0 | .5 | 16 | 18 + knockback | a wave .9 m tall crosses the room wall to wall, then reflects back. Jump it or roll through |
| `whistle` | Whistle | 2 | 1 | 2 | 0 | .5 | 25 | 0 | a Lifeguard's whistle from nowhere: Drowned surface round each pilot, Tilers peel off the walls |
| `lap` | Last Lap | 3 | 1.5 | 6 | .5 | 1 | 24 | 24 + crush | its body runs the room's edge at the surface on a rounded square, tightening from 19 m to 9 m. It exposes every flank lamp: the fight's best damage window |

- **The Lull.** The lore's own trap: the water soothes you. Stand still and the world softens: colour blooms, sound falls away. That is
  the warning, then the hands come. It's the same meter as THE HUM's Fall Through: 6 s, 4 s in the last phase. Moving clears it.
- **The tide is a real level.** Depth under the feet comes from the tide, not just the pool cells.

### The tells

Sound is the language here too, in the opposite way: **the room goes quiet before anything happens**.
- The song and every sound effect dip and muffle during a wind-up (the lore's fading sound), then the **water roars**.
- **Breach:** the water over the locked spot goes dark, bubbles rise in a ring and the flank lamps pass underneath, and it moves 1.1 s
  before contact.
- **Hydro Jet:** the mouth irises open, the throat brightens and a thin line of mist tracks the start of the sweep.
- **Belly Flop:** a long shadow on the water, its own length, darkening.
- **The Drain:** the surface starts to turn, slowly and silently. The floating debris tells you first.
- **Seiche:** the water draws back from the far wall and piles up, and the wave comes.

### The arena: the natatorium

Around the exit. **20 x 20 cells (40 m square), ceiling 7.2 m, no pillars, no chairs, no boards.** A central deep pool 10 x 10 cells with
steps on all four sides (flights two cells wide); in its middle a 4 x 4 **abyss**, a real shaft of tile going down 38 m into black (`PoolroomsLayout.Abyss`; pilots stand on
an invisible floor at the deep pool's level under dark water). Deck all round, 5 cells wide. Round lights in the ceiling, and lights under
the water that stay on when the rest die: in the pool's walls, down the shaft, and low on the room's walls where the tide reaches them. Four arches, two cells wide at each wall's middle, flood shut as it arrives (a curtain of falling water across each); on its death the water drains away down the abyss and the
exit stairs are there.

### The intro: "Sublimity"

| Boss time (s) | Shot | What happens | Sound |
| --- | --- | --- | --- |
| 0-2.3 | Wide over perfectly still water from the deck's corner, gliding down toward it | Calm, warm light, the ceiling's lights in the mirror. One ripple spreads from nothing | The music has stopped. A far drip, the room's quiet, sound fading |
| 2.3-4.3 | Low at the top of the steps, looking down them into the dark | The lights under the water go out one by one, from the far end, down toward the pit, the room falling dark with them. Small white tiles drift | The boss song's opening, muffled, comes in |
| 4.3-5.9 | Straight down over the abyss, slowly descending | Black water. The body comes up the shaft on a helix, two turns of it: its lamps and running lights a slow spiral turning far below, growing, drawing in to the middle, where the mouth's pale ring rises | The song swells, a low pressure |
| 5.9-8.0 | Low at the water, wide, looking up | It breaks the surface on the song's nearest hit, at the back of the shaft: the head rising and rising on a true, slow parabola, sheets of water falling off perfect tile. **The card rises at 6.43** | The hit, then the song's build, held back |
| 8.0-9.07 | Long lens, close on the mouth at the top of its rise, then pulled back on the wave | The grates iris open and turn over the blue throat. A wave rolls at the camera and over it; every light comes up as it clears | The water roars, and the song drops at 9.0678, into the fight |

### The cue keys

`boss.deepend.*` through the effects bank (Core `NativeDeepEndBossCues`); silent until clips exist. Keys marked 2D play flat.

| Key | The sound |
| --- | --- |
| `boss.deepend.drip` | 2D. One far drip in the silence the stage music leaves |
| `boss.deepend.ripple` | The ripple that spreads from nothing: barely there |
| `boss.deepend.lamp.out` | A lamp under water going out: a soft thunk, far off, each nearer the pit |
| `boss.deepend.pressure` | 2D. A low pressure swelling for the shot over the abyss |
| `boss.deepend.break` | It breaks the surface: a huge, slow rush of water upward |
| `boss.deepend.pour` | Sheets falling back off it for seconds, a long tiled patter |
| `boss.deepend.iris` | The mouth's grates sliding open: metal on wet metal, oiled, unhurried |
| `boss.deepend.roar` | Not a voice: the sound of a pool's drain opened all the way |
| `boss.deepend.wave` | The wave rolling at the camera |
| `boss.deepend.quiet` | 2D. The room goes quiet before a move (a dip, for the mixer) |
| `boss.deepend.bubbles` | Breach's ring of bubbles coming up in the dark patch |
| `boss.deepend.breach` | The mouth through the surface under a pilot: a burst, a chrome clang |
| `boss.deepend.jet.charge` | The throat brightening, water drawn in, rising |
| `boss.deepend.jet` | Hydro Jet: a hard, hissing column; its splash where it lands |
| `boss.deepend.shadow` | The Flop's shadow darkening: a long low groan of water |
| `boss.deepend.flop` | The whole body landing: the biggest impact it has |
| `boss.deepend.thrash` | The tail coming round, tile on air |
| `boss.deepend.drain.turn` | The surface starting to turn, slowly, silently but for a thin whine |
| `boss.deepend.drain.roar` | The Drain open: the room's water going down |
| `boss.deepend.lull` | The warning: everything softens, a warm hum, the song falling away |
| `boss.deepend.hands` | The hands of water closing on the pilot, pulling under |
| `boss.deepend.seiche.draw` | 2D. The water drawing back from the far wall |
| `boss.deepend.seiche` | The wave front crossing the room, its crest breaking |
| `boss.deepend.seiche.wall` | The wave hitting the wall and turning back |
| `boss.deepend.whistle` | 2D. A lifeguard's whistle, from nowhere, echoing once |
| `boss.deepend.lap` | Its body round the room's edge at speed: a tiled rumble and a wake |
| `boss.deepend.lamp.break` | A flank lamp shot out: thick glass, a pop of pressure |
| `boss.deepend.stagger` | The break's stagger: its whole length shuddering |
| `boss.deepend.tide` | 2D. Water welling up the steps and spreading over the deck |
| `boss.deepend.lights.die` | 2D. The ceiling's lights going out, outward from the middle, each with a tick |
| `boss.deepend.hurt` | A hit on the throat or a lamp |
| `boss.deepend.death` | The mouth closing, the last grate turning shut |
| `boss.deepend.drain.away` | 2D. The tide going down the pit, gently, sound coming back |

### Death

The mouth closes. It sinks without a splash, the lamps going dark one by one from the tail as it goes. The surface goes still. Then the
water begins, gently, to drain: the tide falling, the turning surface slowing, sound coming back for the first time. The depth lettering
is the last thing visible, sliding down into the black: `DEEP END`.

## 5. Music: each boss has its own song

Owner, 2026-11-05: "Im gonna give both bosses different music from the stage. They will get boss music". This replaces the first request's
"transitions into stage music": the intro runs into the boss's own song, the way the Siren's does.

- **The files (owner's, 2026-11-05):** `Resources/Audio/music/level0-boss.mp3`, *Failing Light*, and `poolrooms-boss.mp3`, *Main Theme*. `docs/ZONE-BOSS-MUSIC.md` has their maps and edits.
- **The stage's own music** (`backrooms-fluorescent-loop`, `poolrooms-untitled`) plays until the arrival, and stops dead when the boss arrives. That silence is the intro's first beat.
- **The intro's beats come from the song** (`HumBossBeats.FromSong`, `DeepEndBossBeats.FromSong`), and the shots and the card follow them. The tables in sections 3 and 4 give the fallback's times, which only play if a song's file is missing.
- **In a preview:** each DEV preview plays its boss's song on its own clock, so an intro can be judged with its music.

## 6. Animation direction

Both are on the shared motion library (`docs/BOSS-MOTION.md`): a replicated root follow, actions stretched to the authority's `hitAt`, no
hard cuts between poses. What's specific to each:

**THE HUM: fluid, wrong and heavy-limbed.**
- **Gait:** two long legs and two long arms, standing on its arched fingers, a four-limb `BossGait` in a lateral sequence (a 3 m cycle: the arms can't reach further), planted limbs locked by IK; past the arm's reach the shoulder blade
  slides down toward the hand rather than the hand slipping. It takes one step where a pilot takes three.
- **The head is stabilised, like a bird's:** whatever the body does, the bar of light stays level and locked on its target, and the body
  moves around it. It holds its place in the air until the body has left it .62 m behind, then thrusts to catch up. The neck is solved along a curve between them, so the body can turn right round under it. That's the single most unsettling thing about it, so spend the most care here.
- **Second order everywhere:** the shroud strips and wire tails are `BossChain`s, the arms lag and overshoot, and the tubes whip on the
  spine.
- **The flicker hitch:** when the lamps stutter, its pose stutters with them for a frame or two, an additive layer on top of fluid motion,
  never a pop in its root. Reduced effects turns it off.
- **Through surfaces:** where it crosses a wall or ceiling, the surface's paper stretches toward it and tears (Fx), and the part inside
  is clipped.
- **On the ceiling:** in NOCLIP the same gait runs inverted: the pose is computed on a floor and turned a half turn about the line the root was on when it began, so planted limbs stay planted upside down. The turn over takes 1.7 s (a slow roll about its own long axis, as it walks).

**THE DEEP END: slow, huge and beautiful.**
- **The body follows the head:** every joint traces the head's path a fixed arc length behind it, plus a travelling undulation and a slow
  roll, so nothing ever cuts. About 30 joints on a spline.
- **Ballistic arcs:** a rise decelerates under its own weight, hangs, and falls; nothing moves at a constant speed. Everything eases.
- **Water follows it:** sheets pour off it for seconds after it rises, foam collars where it pierces the surface, and wakes on the real
  wave field.
- **The mouth:** a slow iris and a turning grate on separate springs. The mandibles flex on a spring.
- **Fins:** second order, folding along the body when it dives and spreading as it rises.

**Hit timing and network impact:** none. Presentation only: strikes land at the record's `hitAt` and the anticipation is never stretched
away from it. Colliders follow the authority's root and move state, except breakable parts, whose positions come from the same Core
model that the authority reads.

## 7. Effects

**THE HUM:**
- Fixture bursts: glass rain falling in real arcs, a bright lane on the carpet before, a scorch after.
- The wall tear: paper stretched toward the hand, a jagged black opening with streaked wallpaper stretching into it, and paper strips.
- Noclip's travel: bulges moving along wallpaper, lamps dipping in sequence, dust from the ceiling grid.
- Fall Through: the carpet blackens from the middle, tears inward, and the torn hole shows nothing at all.
- Max Hum: a ring of distortion through walls, with lamps flaring as it passes each one, and a faint chromatic shift.
- Blackout: real darkness. Its tubes are the room's lights in phase 2.
- Wet carpet: footprints that darken and fade, drips off the hide.
- Texture slip in NOCLIP: wall and ceiling surfaces that creep and swim, like a game with a missing floor.

**THE DEEP END:**
- The real wave field and spray (`PoolWaves`, `PoolSpray`), driven hard, across the whole room once the tide is up.
- The tide: a real rising water surface over the whole room, not just the pools.
- Whirlpool: a turning, sinking surface, with foam spirals and debris (white tiles, a float) going round.
- Hydro Jet: a solid-looking beam with a mist sheath, a hard splash where it lands, and rainbow mist if it reads.
- Seiche: a real wave front with a foam crest, and spray off the walls where it hits.
- Caustics: moving light on the walls and ceiling from every lamp under water. This is half of what makes the level beautiful.
- The Lull: a screen softening, bloom, a colour drift, sound falling away.
- Water hands: formed from the surface, rising round the pilot, pulling down.

**Audio cue keys** go through the effects bank as the other bosses' do (`boss.hum.*`, `boss.deepend.*`, Core `Native*BossCues`). They stay
silent until clips exist. Local synthesised clips may be added for the review; the keys are listed in each boss's Cues file.

## 8. Astra's contract (proposal)

Base: the zone boss path that THE 6:47 and THE CONVERTER use. Astra owns all of it and can change any of it.

**Spawn and flow.** Today these two levels end at the exit cache with no boss (`TryBackroomsExit`, `level0-exit`, `poolrooms-exit`). Proposal:
quota, then reaching the sealed room's door starts the arrival; killing the boss opens the exit cache, keeping the once-only reward ledger.

1. **Archetypes.** `level0-hum` and `poolrooms-deep-end` in `NativeZoneBossRules` (`Known`, `Archetype(map)`, `Phase` at 65% / 30%,
   `Action` from `NativeHumBoss.Moves` / `NativeDeepEndBoss.Moves`). Keep the 9.0678 s intro hold: the songs' edits depend on it.
2. **Bind the views:** `ArenaPresentation.HumBossViewOf` / `DeepEndBossViewOf` and `ConversionGame`'s banner routing, with `NativeZoneBossViews`
   adapters as the others have. `NativeIntroRules.Staged` already excludes known zone bosses. `NativeZoneBossNames` names them.
3. **The arenas.** `NativeHumArena.Carve(BackroomsLayout)` and `NativeDeepEndArena.Carve(PoolroomsLayout)` are pure Core functions Claude has
   written for the preview. Call them from the generators round the exit, bump the generator version and keep the routes certified. The
   Poolrooms needs the room's ceiling and a new `Abyss` cell handled by collision (solid at the deep pool's floor).
4. **New record fields** beside `NativeZoneBossFrame` (the views name them; finite floats, -100 absent):
   - THE HUM: `shiftAt`, `shiftMask` (a bit per partition segment: its current position in the table), `circuitMask` (dead circuits),
     `parts` widened to 7 tube bits and the head (with `partAt`), `clipAt` (stagger cause `clip`), and hazards `row`, `tear`, `ring`.
   - THE DEEP END: `tideY`, `tideFrom`, `tideAt`, `tideUntil`; `diveAt`, `surfaceAt`, `surfaceX`, `surfaceZ`, `pathSeed` (the body's route is
     derived from them on each client); `parts` widened to 12 lamps (with `partAt`); hazards `breach`, `flop`, `jet`, `seiche`, `whirl`, `lull`.
   - `heard[]` or `lull[]`: a per-seat meter 0-1, so the warning can be drawn. It can be one shared field.
5. **Water depth** from the tide: `NativePoolroomsWater.Depth` takes the tide level, so `Waterline`, `WaterDepth` and the speed rule follow it.
6. **The no-safe-spot rules, section 1, every row:**
   - the stillness meter (3 m radius, 6 s, 4 s in the last phase; movement in that radius doesn't reset it), and the height rule;
   - seal the room on arrival, and return anyone outside it. An out-of-volume check runs every second;
   - walls never block THE HUM's moves, and they do block pilots' shots, so `Noclip`'s time inside a wall counts as immune for at most 1.5 s;
   - THE DEEP END is immune under water for at most 6 s at a time, and must surface near someone;
   - adds spawn round each pilot, never from a fixed place;
   - the 6:00 enrages;
   - no boss navigation: both move on authority-driven paths through geometry.
7. **Numbers.** Everything in the move tables, and health. Proposal: `NativePveRules.BossHp` x1.1 for THE HUM, x1.25 for THE DEEP END.
8. **Acceptance:** run `Tests/BackroomsBossSafeSpotChecks.cs`'s sweep against the real rules, and add a native fixture of a pilot in each
   arena corner, wall, doorway and spawn spot, asserting they take damage within the limit. Then windup, contact, single hit, phase change,
   codec and guest application, as `ValidateZoneBossAuthority` does.

**The prompt for Astra** is in the session's final message and in `docs/DEV_LOG.md`.

## 9. Built

Filled in as the work lands, with what was actually seen and checked.

<!-- THE HUM built -->
### THE HUM (Claude, 2026-11-05): presentation only, seen in Unity through the DEV preview, not played by a person

Nothing here is a rule. `ArenaPresentation.HumBossViewOf` returns `HumBossView.None` until Astra binds the record, so the live level is exactly as it was.

| Part | Where | What it is |
| --- | --- | --- |
| The model | `tools/gear/hum_boss.py`, the local bank `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaPackBossHum.bytes` (not committed; Claude ships it in the Drive zip) | Blender, headless, about 8 s. A smooth skin from ropes of hide joined by a voxel remesh, pile displaced in and tufted, on 131 bones; hardware (fixture, T-grid ribs, broken tiles, baseboard fingers, tube housings, the transformer) skinned rigidly in the same mesh; wallpaper strips and wire tails on their own chain bones; vertex colours with ambient occlusion, cavity, wet patches and the paper's pattern. Lit parts are rigid props with their own light. Three tiers: see the bank's report |
| The numbers | `Core/NativeHumBoss.cs` | Phases, the moves table Astra reads, the meter, weak points, hit volumes, the shots and the card |
| The arena | `Core/NativeHumArena.cs` | `Carve`, the partitions' two places, fixtures and circuits, the wall queries the sweep and the effects use |
| The record | `Core/NativeHumBossView.cs` | `HumBossView` with `None`, and section 8's fields as the view reads them |
| The motion | `Core/NativeHumBossLook.cs`, `...Moves.cs`, `...Rig.cs` | The joint table (held to the script's by the checks), a phrase with key poses per move, the rig on the shared motion library |
| The room | `Core/NativeHumBossStage.cs`, `Core/NativeHumSurface.cs`, `Runtime/NativeHumBossRoom.cs` | One table of intro beats (`HumBossBeats`, to bind to the song later), each lamp's level, partitions, the air; real geometry for bulges, tears, holes, slip and the door. Hook in `BackroomsWorld`: one line (`HumBossLayout`), which returns the level's own layout with no boss |
| The effects | `Core/NativeHumBossFx.cs` | Telegraphs and floor light through `NativeFxDraw`, particles through the shared renderer, each lit part's level, its lights in the practical slots |
| The sounds | `Core/NativeHumBossCues.cs` | The `boss.hum.*` keys in section 3's table, queued, silent until clips exist |
| The preview | `Core/NativeHumBossPreview.cs`, `Runtime/NativeHumBossPresentation.cs`, `Runtime/NativeHumBossBody.cs` | The intro, every move, both phase changes, breaks, a choke, a clip, the ceiling and the death on a loop, in the carved room on the real map, with a directed camera that finds a clear view |
| The checks | `Tests/HumBossChecks.cs` (`--hum-boss`), `Tests/HumBossSweep.cs` (`--backrooms-boss-sweep`) | See below |

**What changed from the first design, and why.**
- It stands on arched fingers, not its knuckles: far stronger silhouette from the game's camera, in the dark too.
- The tubes run along its back in a herringbone (they first read as a line of plain bars, and the chevron is the wallpaper's).
- The walk's cycle is 3 m, not longer: past that the arms can't reach. A hand past its reach pulls the shoulder blade down rather than slipping.
- Partitions were re-placed so a clear hall runs down the middle of the room (the first table had one across every intro shot). Each has two places; 256 mixes checked.
- The neck pays out: its three bones stretch along themselves, so the head can really hold its place while the body goes round it.
- Wall Hand has its own arm on its own bones in the skin (bound where the right arm is), hidden until used, as the boss's own arm can't reach across a room. Its body plunges the right arm into the floor to match.
- Row Burst's lanes, the lunge's width and the rest are as designed.
- In the blackout a stray lamp stutters on over a Wall Hand or a Familiar Voice (a lamp near the point the record names), and a light rakes down that stretch of wall, so what's in it can be seen when nothing else is lit. The rule isn't touched; it's light.
- A Familiar Voice is a face in relief under the paper, a little too large: brow, sockets, nose, cheekbones, chin; its mouth splits the paper as it speaks. The first version was four bumps and a hole, which read as a hole. Surface patches now stop at a partition's end (one hung off the end of a partition in the air), so a voice or a hand should be placed off the end: the preview insets them, and Astra's rule should too (a metre or so).
- The room's dust takes the room's light, and in a blackout it's nearly gone.
- A Row Burst's lamps stay dark for 5.5 s, then stutter back on their own, so the room isn't left dark by the rotation; the intro's tear heals 4 to 6.5 s after the hold, since the level doesn't keep what happens to it.
- NOCLIP's light is wrong but fightable (the first level left whole stretches too dark to read), and the slip of the ceiling and wall grid is gentler than first built, since at full strength it read as a funhouse rather than as a room that's slightly wrong.
- The intro's wall is lit by one lamp raking down it, added because a flat bulge didn't show at all from the shot: with the rake, four finger ridges and a palm read under the paper before anything's through. The tear is a lobed hole with a few long runs, not a star; its rim and the flaps peeled back off it show the paper's pale reverse, and the hand hooks over the lower lip and pulls, which is what opens it. The walk-out shot sits a little further across the wall so that what's lit on the body can be seen coming up through the paper as it nears.
- The death ends on the exit: a door, opened in the wall at `NativeHumArena.ExitDoor`, as the first lamp returns. The door is a picture only; the way out is Astra's.
- The intro's first shots run with the card's shades at .42 of the Siren's (`BossIntroCard.Shades`, the other boss unchanged): the first beat is a flat bright room, and her shades hid it. From the name on they're hers.

**For Astra (section 8 still stands):** `HumBossViewOf` is the one binding point. The record fields are `HumBossView`'s. On binding, the presenter carves a copy of the layout round its exit cell for the room's look (`HumBossLayout`); your generator should call `NativeHumArena.Carve` round the same cell and read the partitions from `HumRoom.Partition`. `Tests/HumBossSweep.cs` plays a stand-in over the table's shapes, not your rules: run the same grid against the real authority.

**The sweep.** A pilot at each cell's centre, hard against each wall and in each corner, still and circling, over 110 s of a stand-in rotation (whatever has been ready longest, near enough to land). A still pilot's limit is the meter plus its wind plus .5 s. A circling pilot's threat counts a shape passing within 1.5 m (it had to be moving to miss it); those limits (16, 20, 20 s) were set just above what the stand-in does, so they hold the design where it is rather than prove it. Removing Fall Through must break the still limit, and does. The stand-in found no hole that needed a design change; it did show the rotation must not be a fixed order, or Max Hum comes round too rarely: Astra's rotation should prefer what has been ready longest.

**Not done or not known.** No person has played it; no rule exists. Performance is not measured in a real match (the skin is about 32k vertices on a `SkinnedMeshRenderer` at tier 0, and the stage load builds the bank and body through `BackroomsBossPrewarm` once `NativeZoneBossRules.Archetype` names the map; that path has not run yet). The song's accents aren't bound: the beats are fixed numbers. Fall Through's fall and return, Facelings and the pilot's side of every move are Astra's. On the ceiling the preview's chains only approximate gravity through the turn.
<!-- /THE HUM built -->

### THE DEEP END (Claude, 2026-11-05): presentation only, seen in Unity through the DEV preview, not played by a person

Presentation only; nobody has played it. `DeepEndBossViewOf` returns None until Astra binds it, and with None nothing here draws or costs anything beyond a branch: the live Poolrooms is what it was (its hooks are the layout swap under the preview flag and two surfaces standing aside under the stage flag).

**The model.** `tools/gear/deep_end_boss.py` (Blender 5.2, headless; `deep_end_boss_sheet.py` renders a review sheet, straight and on a leaping arc) writes the local bank `Resources/ChipArenaPackBossDeepEnd` (not committed; ships in the Drive zip). As drawn: about 60,500 triangles at tier 0, 29,500 at tier 1, 4,800 at tier 2. The skin is one mesh per tier on the spine's 31 joints: real tile geometry, about 4,000 tiles at tier 0, each with its own bevel, grout channel and a tiny tilt of its own, finer tile on the skull, larger basin tile on the belly; the letters are the grid's own cells going black, with the cells on a letter's edge split into small mosaic pieces. A vertex's uv0 is its place on the spine and its gloss; Runtime makes four cubic B-spline weights from the place, so the skin bends as a curve. Rigid parts: the flank lamps (with a broken variant), the nine iris blades, the drain grate, the throat, the rails, the fins, a ring buoy. It's drawn with the gear's toon (`ZombieGearMaterials`) and a glaze over it (`NativeDeepEndGlaze`). Without the bank, a plain loft on the same bones.

**The motion.** `Core DeepEndBossRig`: the head on a route (`DeepEndRoute`: Hermite legs and true parabolas, a pure function of time; `DeepEndBossPaths` writes each move's so its contact lands on the record's `hitAt`). Every joint traces the route a fixed arc length behind the head, on fixed ticks of the clock, so two frame rates give one pose. A visible joint never goes faster than a heavy body's pace or the head's own; a hidden joint (under the tile, deep in the shaft) catches up fast: when the head has rushed ahead under the floor, the strand still in sight pours in at its own speed. On top: a travelling undulation integrated on the ticks, a slow roll, the neck's look on forward kinematics, the Flop's lift (only what's over its line), the Thrash's sweep, a rotation-minimizing frame that rights itself by an angle, second-order fins and rails, the iris and the grate on springs of their own.

**The room.** `Core NativeDeepEndArena.Carve` (pure), `PoolroomsLayout.Abyss` (a new floor kind; the level's mesh builds its shaft), `Core NativeDeepEndBossStage` (the hook: tide, flood front, lights by group, whirlpool, seiche, the arrival's ring, murk, the screen's two moments), `Core DeepEndWaves` (a 43 m wave field over the whole room, the wave equation stepped on fixed ticks, foam; the level's `PoolWaves` is untouched). Runtime: one displaced grid (`NativeDeepEndWater`: the tide, funnel and waves are real geometry; refraction by a named grab, absorption by the scene's depth, a reflection marched against the depth with the room's box behind it, foam from the field and where the water meets anything), caustics on the level's own meshes drawn again (`NativeDeepEndCaustics`, pushed by the field's curvature, so a wave's ring sweeps the ceiling), the water geometry (`NativeDeepEndFlow`), the screen (`NativeDeepEndScreen`).

**The effects**, functions of record and clock: sheets pouring off it for seconds after it rises (`EmergedAt` per joint), foam collars, spray and mist through the shared effects renderer, the Jet's tube with its mist and splash, hands of water, the long shadow, the dark circle and its bubbles, the lamps' glow on the water over them while they pass under the tile, tile and a ring buoy going round the whirlpool, curtains across the arches.

**What changed from the first design, and why.**
- **Belly Flop's wind is 1.8 s, not 1.2:** a 32 m body can't be seen to rise out of the water, hang and fall in 1.2 s, and the long shadow needs the time to read.
- **Seiche's strike is 7 s, not 3.5:** 3.5 s is one crossing at its speed; "a wall takes two hits" needs the crossing back.
- **Last Lap runs a rounded square, not a circle:** the sweep's first pass showed a circle of 19 m never reaches the corners (28 m out).
- **A dry-deck target in the first phase never draws Breach** (it only surfaces where there's water): the rotation has to choose the Jet, the Flop or the Drain instead, or the deck's corners go 26 s without a threat for a moving pilot. Astra's rotation should do the same.
- **The lettering is on both upper flanks, mirrored,** not on the spine alone: from the game's cameras a line down the spine reads upside down half the time. `DEEP END` is two lines on the crown, reading from in front of the mouth.
- **Running lights** between the twelve lamps: twelve points don't make a spiral.
- **It breaks the surface at the back of the shaft,** so its fall comes down in the pool, not on the deck.
- **The mouth is nine iris blades over a turning grate:** concentric rings can't open.
- **Four arches with curtains of water,** rather than plain walls: the room needs its architecture, and it's what "the arches flood shut" looks like.

**The sweep** (`Tests/DeepEndBossSweep.cs`, in `--backrooms-boss-sweep`), every cell, the corners and along the walls, 210 s a run:

```
deepend-sweep phase 1: still worst 7.6 s at (-19.0,-19.0), limit 8.0; circling worst 12.5 s at (-15.0,-19.0), limit 14.0; without the Lull still worst 11.8 s at (-19.0,-19.0); without it and the room-wide moves 11.8 s at (-19.0,-19.0)
deepend-sweep phase 1: with only the Flop and the Jet: 11.8 s at (-19.0,-19.0)
deepend-sweep phase 2: still worst 4.1 s at (-19.0,-19.0), limit 8.0; circling worst 4.9 s at (-15.0,-19.0), limit 10.0; without the Lull still worst 4.2 s at (-19.0,-19.0); without it and the room-wide moves 5.0 s at (-19.0,-19.0)
deepend-sweep phase 2: with only the Flop and the Jet: 11.7 s at (-19.0,-19.0)
deepend-sweep phase 3: still worst 5.6 s at (-19.0,-19.0), limit 6.0; circling worst 12.4 s at (-17.0,-19.0), limit 14.0; without the Lull still worst 12.4 s at (19.0,-19.0); without it and the room-wide moves 5.0 s at (-19.0,-19.0)
deepend-sweep phase 3: with only the Flop and the Jet: 11.7 s at (-19.0,-19.0)
PASS deepend-sweep (9 checks)
```

The Lull is what guarantees the first and last phases' limit for a still pilot; take it out and the corner goes past the limit in the first and last phases. In the second phase the rotation alone keeps up (the room-wide moves come round fast), so its proof of teeth is taking out the Breach too.

**For Astra.** Bind `ArenaPresentation.DeepEndBossViewOf` (Runtime `NativeDeepEndBossPresentation.cs`): the fields are `Core DeepEndBossView`'s. The rig fills its own route from the record as moves start, so a record only needs the move's start, its hit, its places (`targetX/Z`, `aimX/Z`) and `surfaceAt/X/Z` between moves. `NativeDeepEndBoss.Moves` is the table; `NativeDeepEndBoss.Parts` the hit volumes and weak points; `DeepEndBossRig.LampFrame` and `MouthAt` the positions. Collision for `PoolroomsLayout.Abyss` stands at the deep pool's floor. The tide is `tideFrom`, `tideY`, `tideAt`, `tideUntil`: `NativePoolroomsWater.Depth` should read the same.

**Not done.** No audio clips (the keys are silent); no record, rules or hit volumes bound. The mixer's dip for `Muffle` isn't wired (it's on the stage for whoever does the sound), and the stage's `Shake` moves only the preview's camera: hooking it to the live camera touches shared ConversionGame. The Whistle's Drowned and Tilers are not spawned here (no spawn is presentation): the move shows the dark water round each pilot and the rings, and the flat cue, and waits for the rest. The body doesn't reflect in the water when the reflection's ray leaves the screen (it falls back to the room's box). Reduced effects only has less to reduce here: nothing strobes.

## 10. The owner's pass, 2026-11-07 (Claude: presentation; the rules' half is Astra's, below)

The owner, after playing both: THE HUM's charge is easily dodged; its swipe should look like it really has force; its intro doesn't
show it properly; upside down its hit box doesn't work and it's hard to tell it's upside down (raise the room in that phase). THE DEEP
END: weird twists in its body and glitches; swimming where there's no water, coming out of dry tile; no counter to the pull; raise its
room; the water beam doesn't look as if it hits. And: no pilots in the bosses' intros.

What was wrong, measured, and what changed:

- **THE HUM's intro in a real fight** stood frozen. The arena's clock waits through the intro's hold while the song runs; THE 6:47 and
  THE DEEP END already run their presenters on a clock that trails the arena's by what's left of the hold, THE HUM's didn't, so its rig
  got no time and no arrival. It now does the same (Runtime `NativeHumBossPresentation`). Seen in a real Boss Battle: the walk out, the
  tubes, the head catching, the straighten and stoop all play.
- **Its hit box upside down:** the authority placed the colliders with `HumBossRig.ToWorld(Shown...)`, but `Shown` already carries the
  half turn, so the colliders went back to the floor while the body hung from the ceiling. `ToWorld` is now the identity on `Shown`
  (a one-line fix in the rig, so Astra's file is untouched).
- **NOCLIP's room is taller:** as it rears up into the phase it pushes the ceiling from 4 m to 6.4 m (`NativeHumBoss.HighCeiling`, over
  1.3 s from 1.25 s into the change); its walls are carried up in a papered band; its fixtures go up with it. On the ceiling it hangs
  with clear air under it, and whatever it strikes with reaches the rest of the way down (the arms a move drives, the head as a move
  comes in). The rig turns over about half the new height. **Collision is Astra's:** the level's one ceiling block at 4.15 m still
  stops shots above 4 m in that room until it is lifted (below).
- **The swipe (Reach)** is thrown with the whole body: a gather, then a coil (weight back and low, shoulders wound round 66 degrees, the
  arm cocked behind it); the hips unwind first while the chest still faces back, then the chest, then the arm whips across; the arm's
  weight carries the body on round and down after it and it settles back off that. A trail of torn air and dust follows the fingertips,
  dust blows outward on the carpet under the arc, and a ring of it is thrown off at the end. The rule's arc and timing are unchanged.
- **THE DEEP END's body:** the real rules start a move while the last one's path still runs on (its fall, its dive), and the route
  overwrote its own end, so a new move began from where the old plan *would* have ended: the head jumped and the body folded on itself.
  Measured with `--deepend-motion` over the real rules in all three phases: neighbouring joints twisted up to 180 degrees and joints in
  sight moved at over 1,000 m/s. Now a new move (or a phase change, or the death) cuts the plan where its record began and carries on from
  the head as it is (`DeepEndRoute.CutAt`); every way down is a forward arc (`DeepEndBossPaths.Dive`; a body can't go back down itself);
  hidden travel has a top speed; the skull stays on the route so contacts keep their clock; and each joint's back is carried through
  time and leans to the sky where the body lies level (no candy-wrapper twist). After: twist between neighbours mostly under 20 degrees.
  Some single-frame outliers remain in rare overlaps (noted in the measure).
- **Never out of dry tile:** with the decks dry, everything it does stays in or over the water: Breach arcs toward the pool's far
  corner and comes down in it, the Jet and the Thrash rise well inside the pool. **The Flop is a real leap:** it runs up under the water,
  comes out at the line's start, flies the length of the line and comes down along it on the hit (the arc it leaves in the air is laid
  flat, so the whole body lands); on dry deck it lies stranded on the tile, then hauls itself back over the edge into the pool, on top of
  the tile. A cut while it's stranded crawls it back the same way.
- **The beam** is drawn on the rule's own line (JetDir and its flat 2 m by the table's reach), not the mouth's lagging axis: a column to
  where it lands, then the same water tearing along the surface to the line's end (climbing a wall it reaches), and a burst on anyone in
  the line. The neck holds the rule's line tightly through the sweep. JetDir now aims the column at up to 26 m (was 16), so it lands on
  the pilot it was aimed at.
- **The Drain** shows its pull over the whole room: sheets of water racing in over the surface and over the dry deck, spiralling as they
  near the pit, spray dragged along, the throat bright. Its counterplay is a rule (below); the iris closes and the pull's look stops on
  `cancelAt`.
- **Its room is 12 m high** (was 7.2: its rises came up against it). `NativeDeepEndArena.Ceiling`; the generator's ceiling blocks follow
  it; the lights' reach grows with it.
- **No pilots in a boss's intro:** through a zone boss's intro hold (`NativeIntroRules.PilotsOffStage`) no pilot is posed or drawn, no
  spawn beam, no footfall; they come in with the fight (the spawn-in finishes as it starts). Seen in real Boss Battles for THE HUM, THE
  DEEP END and NOCTIEL. A faint ring still shows where NOCTIEL's pilot stands in its intro; not yet traced.

**Animation impact:** THE HUM: presentation only. Its colliders now sit on its drawn body on the ceiling (they were on the floor), so
upside down it can be hit where it is. THE DEEP END: its route is shared by the authority (its root, the Jet's mouth and Last Lap's body
are read by the rules), so the place the Jet fires from, where it surfaces and the Lap's body follow the new paths; every contact stays
on the record's `hitAt`; nothing else changes (`--backrooms-boss-rules` passes).

### For Astra (the rules' half of the owner's list)

1. **THE HUM's charge (Noclip) faster** (the owner: easily dodged). Proposal: wind 1.2 to .75 s, strike 1.2 to .8 s at 32.5 m/s
   (`NativeHumBoss.Moves` `lunge`); `Contacts` and `BackroomHitScale` read the table's strike instead of the literal `1.2f` (and NOCLIP's
   second pass `2.4f` from it); the lane re-aims at the target until .3 s before the hit (rewrite `laneX1/Z1` in `Step` during the wind) so a
   sidestep at the start no longer beats it. Its warning and pose already follow the record.
2. **NOCLIP's raised room:** in phase 3, lift the room's ceiling collision to `HumBossView.Ceiling` (the map has one `ceiling` block at
   4.15 m over the whole level: switch it off over the room or add a room-sized one at 6.55 m), the seals to 6.4 m and `Inside` to 6.4 m.
   Until then shots stop at 4 m in that room.
3. **THE DEEP END's Drain, a counter** (the owner: there's none). Today `ZoneDrag` zeroes the pilot's own movement, so nobody can resist
   for 4 s. Proposal: an added current (the pilot keeps control), 3 to 5 m/s (under a sprint), none while airborne or rolling; and the
   throat closes it: damage to the throat during the Drain of 3% max health cancels it (`cancelAt`, `whirlUntil` now, a 2 s stagger). The
   presentation already closes the mouth and stops the pull's look on `cancelAt`.
4. **The Deep End room is 12 m high now:** `NativeBackroomsBossRules.Inside` and `BackroomGeometry`'s seal height still say 7.2; read
   `NativeDeepEndArena.Ceiling` (the 6:00 flood can rise to it or stay at 7.2).
5. **Boss relics:** docs/BOSS-CHIPS.md.
6. **For a check:** the HUM's colliders follow `Shown` on the ceiling (Core `HumBossRig.ToWorld` is the identity now).

## 11. The rules' half, done (2026-11-07, Claude at the owner's word: "Go ahead and make the changes yourself")

The owner asked Claude to make section 10's "For Astra" list itself. What changed in the rules (Core `NativeBackroomsBossRules`, Runtime
`NativeBackroomsBossAuthority`):

- **Noclip is faster and follows you.** Wind .75 s (was 1.2), strike .8 s (was 1.2) over the same 26 m, so 32.5 m/s; the table is read
  everywhere (the contacts, the second pass in NOCLIP at 2x the strike, the clip check, the authority's immunity while it is inside a wall).
  Through its wind the lane follows its target (its warning, pose and facing follow the lane) and holds its line from
  `NativeHumBoss.LungeLock` (.3 s) before it goes: a sidestep at the start no longer beats it, one in the last .3 s does.
- **NOCLIP's room really is taller.** `NativeBackroomsBossRules.Ceiling(z)`: 4 m until NOCLIP, `HumBossView`'s 6.4 m from it. The authority's
  seals rise to it, `Inside` reads it, and the level's one ceiling block is set aside for the fight: the room gets its own lid at 6.4 m and
  the rest of the level keeps its ceiling at 4 m around it (so nobody climbs onto the maze's walls elsewhere). Shots now carry up to the
  room's new ceiling.
- **The Drain has counters.** It no longer takes a pilot's control: `ZoneCurrent` adds a current toward the pit, 2.5 m/s at the room's
  edge to 4.5 m/s at the pit (a run is 6.8 m/s), with no hold on a pilot in the air or rolling. And its throat shuts it: 3% of its health
  to the open throat while it drains (the throat already takes 2x then) chokes it shut with a 2 s stagger (`staggerCause` "throat"); the
  presentation already closes the iris and stops the pull's look on `cancelAt`. The 40 at the pit is unchanged. THE HUM's Wall Hand
  still drags (a grab).
- **The Deep End room's 12 m** is in the rules: `Inside` and the seals read `NativeDeepEndArena.Ceiling`. The 6:00 flood still rises to 7.2 m
  (it ends the fight either way).

**Animation impact:** Noclip's phrase reads the table, so its sink and its run are shorter (.6 s gather and a .15 s set, .8 s run); its
facing turns with the lane through the wind (the rig's fast turn for the lunge). Contacts stay on `hitAt`. The Drain's stagger poses as
any other. Networking: the lane's re-aim is in the replicated frame (laneX1/Z1, actionYaw, target), as before.

**Later the same day (the owner, after playing): the Drain's pull.** "Deep end's drain counter still pulls while player is airborne, which is fine...
however, make the pull weaker if players are further away." The current holds airborne pilots again (not rolling ones), and falls off steeply:
`NativeDeepEndBoss.DrainPull(d)` = 4.5 m/s x (1 - d/28)^1.5, never under a 0.6 m/s drift (4 m/s at the pit's edge, 2.3 at 10 m, 0.6 at the
room's corners). A pilot the current carries toward the pit without fighting it still counts as standing still (the stillness meter follows
them), so the Lull still finds a camper: the build's no-safe-spot gate (a still pilot in every corner hit within 8.5 s) failed without it
once the pull weakened, as the old drag had carried corner pilots into the pit.

**And the owner's next look (2026-11-07):** THE HUM's swipe ("turn their arm and actually swipe at them, make it a little faster"): Reach is
.48 s wind, .16 s strike, .24 follow, .42 recover (was .6, .2, .3, .5); the arm is cocked high behind it with the elbow up, then turns over as
it comes (the elbow rolling down and through), the hand driven out through the space in front of it where its target stands, the body
lunging after it. Its turn over ("animate actually flipping upside down and when they flip back"): the same flip both ways in its own frame,
`HumBossMoves.TurnKeys` on the turn's clock: a crouch with the arms swung back, a push with the arms thrown toward the surface it's going to,
a tuck as it turns, hands then feet planting with a give, straightening; the rotation (`HumBossView.Turn`) waits through the crouch (none
before .3 of TurnOver) and is over by .76. Its colliders follow the drawn body as before. THE DEEP END's Hydro Jet ("make the water more
realistic, it currently has a unrealistic curve to it"): the jet leaves the mouth dead straight along the rule's aim, its head reaching where it
lands in a tenth of a second, drops peeling off it; a crown of water is thrown up again and again where it lands; beyond, spray skips on along
the line to its end (no sheet of water). The rule's line is unchanged.

**The swipe as a slap (the owner, 2026-11-07: "we need to turn their hand to, think like they're smacking someone"):** the hand used to carry
on along the forearm palm-down, so it came in edge-first. Two hand channels now: `HumCh.PalmL/R` (0..1) turns a hand on a channel about its
forearm until its palm faces where it's going (its own travel, smoothed over 30 ms, full past 8 m/s, none under 2), and `CockL/R` (degrees)
bends it back at the wrist. Reach lays the wrist back 40 degrees as it winds, turns the palm into the stroke as it starts (fingers drawn
together), is flat at the hit and flicks the fingers through after it; mirrored for the left hand. At the hit the palm faces the hand's travel
within 16 degrees and stays on it through the sweep (CoreTests `--hum-boss`, both hands). A fixed roll angle couldn't do it: the forearm
swings so far through the stroke that "palm to the left" in its frame came out palm-down at the hit.

**THE DEEP END, the owner's next look (2026-11-07):** "we dont need hit indicators for the waves. Also, add slam effects when the deep end slams
onto the tiles. (Add slam into water effects [Splashing]) when done in water." Then: "the vortex ability seems too weak now, we need to make the
pull stronger ... we can also remove hit indicators from that one too."
- **No warnings** for Seiche or the Drain (`NativeDeepEndBossFx.Unwarned`): the wave and the whirlpool are their own tells.
- **The Drain stronger:** `DrainPull(d)` = 6.5 m/s x (1 - d/28)^1.2, never under 1.4 m/s (5.8 at the pit's edge, 3.8 at 10 m, 1.4 at the
  corners; was 4.5, ^1.5, 0.6). Still under the 6.8 a run beats; airborne pilots still held, rolls still free, a carried pilot still Lulled.
- **Slams and splashes** where it comes down (`NativeDeepEndBossFx.Slam`), wet or dry by the tide against the floor there (`Wet`): in water a
  splash (spray up and out, mist, a crown of water); on dry tile a white flash, chips of tile and grout thrown up that bounce, dust rolling out
  in a ring and a column of it, and a dark scar that fades. The Belly Flop comes down along its line head first, a slam or splash at each of
  seven places, the water's wall only where most of it is wet; Breach bursts up through dry tile; Thrash hits round itself four times.

**THE DEEP END's body, the owner again (2026-11-07):** "can we make the movement more realistic? A lot of turns (mostly upward) the body clips
into itself". Every joint traced the head's route exactly, so wherever the route turned tighter than the body is thick (over the top of a rise,
out of the water into a leap, a dive that turns back behind the head) the body folded through itself: through the scripted fight two parts of
it came to 0.17 of their radii apart and a joint bent 170 degrees. Now, after the trail, `DeepEndBossRig.Relax` makes a few passes on the
frame's joints alone (two frame rates still give one pose; nothing allocated): joints either side of one held at least as far as a bend of
`MostBend` (0.62 rad) allows, parts three or more joints apart kept their radii apart (`Apart`), each segment at least its own length, the
rest drawn back toward the trail; the skull stays on the route, so every contact is where it was. Through the same fight the closest two
parts come is now 0.95 of their radii and the sharpest bend 72 degrees (CoreTests `--deepend-boss`, "the body never folds into itself").
Stranded on dry tile with the pool behind it, it curls round in a U (`DeepEndBossPaths.UTurn`, 2.6 m) instead of turning back over its neck.
The DEV preview's script planned every move before the flood on the flood's water (its phase times read 0 while it was written), so its
Flop dove through dry deck: fixed. Last Lap's body contact (the rule reads the same rig) follows the body as drawn, a little wider in turns.
