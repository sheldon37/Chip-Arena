# Expedition bosses: the Backrooms — THE HUM (Level 0) and THE DEEP END (the Poolrooms)

Owner, 2026-11-05: "can you begin development on Level 0 and poolrooms bosses? Give them each a high quality intro (the same kind of
method we use with Siren, so like a player intro but transitions into stage music). Make them backrooms lore accurate based on the
level. Give them high quality designs, high quality effects, beautiful animations and make them difficult. I want to prevent cheese
methods like 'safe' spots."

**Status: design by Claude while the owner slept. The owner has not reviewed it; every choice below is a recommendation and easy to
change.** The split is the usual one. Claude builds the model, motion, effects, intro, music and a DEV preview behind a view that
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
| Hugging a wall | **Wall Hand:** an arm comes through the wall within 2.5 m of you | **Seiche:** the wave is tallest at the walls and reflects back, so a wall takes two hits. **Last Lap** crushes the perimeter |
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
| Back of the stoop (the 4 m ceiling scrapes it) | 3.9 |
| Head fixture, centre | 3.3, forward of the chest |
| Head fixture | 1.6 wide x .4 x .3 |
| Arm, shoulder to knuckle | 3.4 |
| Legs | 2.2 |
| Armspan | 7.6 |
| Upright (only when it rears: head and shoulders go up through the ceiling) | 5.6 |

**Materials.** All from the level's own palette, which is what makes it look grown from the room:
- **Hide:** soaked black-brown carpet pile with sodden patches, shining wet (not fur: dense matted pile, damp, darker where it's wet).
- **Wallpaper:** long peeling strips of the level's pale chevron paper hanging from shoulders and hips like a torn shroud, with the
  plaster-backed reverse showing as they curl.
- **Ribs:** bent ceiling T-grid, with acoustic ceiling tiles hanging off it, broken and stained.
- **Fingers:** a metre long, jointed lengths of red-brown baseboard and bent T-rail. They scratch.
- **Spine:** seven fluorescent tubes through its back in old ballast housings, each with a bare wire tail. They buzz and flicker, and
  light the room as it walks.
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
| `reach` | Reach | 1 | .6 | .2 | .3 | .5 | 3 | 22 | 130° arc, 6 m, through walls |
| `stoop` | Stoop | 1 | .7 | .15 | .4 | .6 | 7 | 30 | 4.5 m circle at its feet |
| `row` | Row Burst | 1 | 1.1 | .1 | .5 | .4 | 10 | 16 | every fixture on alternate rows of one axis bursts: lanes 1.4 m wide, the whole room long |
| `lunge` | Noclip | 1 | 1.2 | 1.2 | .4 | .7 | 12 | 34 | a 3 m wide lane, 26 m at 22 m/s, through everything |
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
give cover from sight and from Row Burst lanes, never from the boss, and they move. Their positions come from a table in
`Core NativeHumArena`, chosen so no layout leaves a sealed pocket. Doorways are papered over as it arrives; on its death one door in
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
- **Depth markers:** black tile lettering laid along its back as at a pool's edge, counting up toward the head: `3 FT`, `5 FT`, `9 FT`,
  `NO DIVING`, and across the crown `DEEP END`. A black lane line runs down the spine.
- **Flank lamps:** twelve round pool lights, six a side, with chrome bezels, turquoise-white, lighting the water round them.
- **Head:** blunt and eyeless, smooth tile, no features but the mouth. The mouth is an iris of concentric chrome drain grates, turning.
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
| `drain` | The Drain | 1 | 1.4 | 4 | .3 | .8 | 20 | 40 at the mouth | the whole room's water pulls toward the pit at 5 to 9 m/s; it's the mouth open at the centre |
| `lull` | The Lull | 1 | 1.5 | .3 | 1.5 | 0 | meter | 8 + 8/s | hands of water take a pilot still for 6 s, or above 1.6 m for 2 s |
| `seiche` | Seiche | 2 | 1.4 | 7 | 0 | .5 | 16 | 18 + knockback | a wave .9 m tall crosses the room wall to wall, then reflects back. Jump it or roll through |
| `whistle` | Whistle | 2 | 1 | 2 | 0 | .5 | 25 | 0 | a Lifeguard's whistle from nowhere: Drowned surface round each pilot, Tilers peel off the walls |
| `lap` | Last Lap | 3 | 1.5 | 6 | .5 | 1 | 24 | 24 + crush | its body circles the room's edge at the surface, tightening from 19 m to 9 m. It exposes every flank lamp: the fight's best damage window |

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
steps on all four sides; in its middle a 4 x 4 **abyss**, where the floor falls away into black (the visible floor stops; pilots stand on
an invisible floor at the deep pool's level under dark water). Deck all round, 5 cells wide. Round lights in the ceiling, and lights under
the water that stay on when the rest die. Arches flood shut as it arrives; on its death the water drains away down the abyss and the
exit stairs are there.

### The intro: "Sublimity"

| Boss time (s) | Shot | What happens | Sound |
| --- | --- | --- | --- |
| 0-2.3 | Wide over perfectly still water, gliding | Calm, warm light. One ripple spreads from nothing | The music has stopped. A far drip, the room's quiet, sound fading |
| 2.3-4.3 | Low at the surface, toward steps going down into the dark | The lights under the water go out one by one, from the far end, down toward the pit. Small white tiles drift | The boss song's opening, muffled, comes in |
| 4.3-5.9 | Straight down over the abyss, slowly descending | Black water. A slow spiral of round lights turning far below, growing. A pale ring in the middle of it | The song swells, a low pressure |
| 5.9-8.0 | Low, wide, looking up | It breaks the surface on the song's nearest hit: the head rising and rising, sheets of water falling off perfect tile, in slow motion. **The card rises at 6.43** | The hit, then the song's build, held back |
| 8.0-9.07 | Close on the mouth, then pulled back on the wave | The grates iris open and turn. A wave rolls at the camera and over it | The water roars, and the song drops at 9.0678, into the fight |

### The cue keys

`boss.deepend.*` through the effects bank (Core `NativeDeepEndBossCues`); silent until clips exist. Keys marked 2D play flat.

| Key | The sound |
| --- | --- |
| `boss.deepend.drip` | 2D. One far drip in the silence the stage music leaves |
| `boss.deepend.ripple` | The ripple that spreads from nothing: barely there |
| `boss.deepend.lamp-out` | A lamp under water going out: a soft thunk, far off, each nearer the pit |
| `boss.deepend.pressure` | 2D. A low pressure swelling for the shot over the abyss |
| `boss.deepend.break` | It breaks the surface: a huge, slow rush of water upward |
| `boss.deepend.pour` | Sheets falling back off it for seconds, a long tiled patter |
| `boss.deepend.iris` | The mouth's grates sliding open: metal on wet metal, oiled, unhurried |
| `boss.deepend.roar` | Not a voice: the sound of a pool's drain opened all the way |
| `boss.deepend.wave` | The wave rolling at the camera |
| `boss.deepend.quiet` | 2D. The room goes quiet before a move (a dip, for the mixer) |
| `boss.deepend.bubbles` | Breach's ring of bubbles coming up in the dark patch |
| `boss.deepend.breach` | The mouth through the surface under a pilot: a burst, a chrome clang |
| `boss.deepend.jet-charge` | The throat brightening, water drawn in, rising |
| `boss.deepend.jet` | Hydro Jet: a hard, hissing column; its splash where it lands |
| `boss.deepend.shadow` | The Flop's shadow darkening: a long low groan of water |
| `boss.deepend.flop` | The whole body landing: the biggest impact it has |
| `boss.deepend.thrash` | The tail coming round, tile on air |
| `boss.deepend.drain-turn` | The surface starting to turn, slowly, silently but for a thin whine |
| `boss.deepend.drain-roar` | The Drain open: the room's water going down |
| `boss.deepend.lull` | The warning: everything softens, a warm hum, the song falling away |
| `boss.deepend.hands` | The hands of water closing on the pilot, pulling under |
| `boss.deepend.seiche-draw` | 2D. The water drawing back from the far wall |
| `boss.deepend.seiche` | The wave front crossing the room, its crest breaking |
| `boss.deepend.seiche-wall` | The wave hitting the wall and turning back |
| `boss.deepend.whistle` | 2D. A lifeguard's whistle, from nowhere, echoing once |
| `boss.deepend.lap` | Its body round the room's edge at speed: a tiled rumble and a wake |
| `boss.deepend.lamp-break` | A flank lamp shot out: thick glass, a pop of pressure |
| `boss.deepend.stagger` | The break's stagger: its whole length shuddering |
| `boss.deepend.tide` | 2D. Water welling up the steps and spreading over the deck |
| `boss.deepend.lights-die` | 2D. The ceiling's lights going out, outward from the middle, each with a tick |
| `boss.deepend.hurt` | A hit on the throat or a lamp |
| `boss.deepend.death` | The mouth closing, the last grate turning shut |
| `boss.deepend.drain-away` | 2D. The tide going down the pit, gently, sound coming back |

### Death

The mouth closes. It sinks without a splash, the lamps going dark one by one from the tail as it goes. The surface goes still. Then the
water begins, gently, to drain: the tide falling, the turning surface slowing, sound coming back for the first time. The depth lettering
is the last thing visible, sliding down into the black: `DEEP END`.

## 5. Music: each boss has its own song

Owner, 2026-11-05: "Im gonna give both bosses different music from the stage. They will get boss music". This replaces the first request's
"transitions into stage music": the intro runs into the boss's own song, the way the Siren's does.

- **The files (owner's):** `Assets/ChipArena/Resources/Audio/music/level0-boss.mp3` and `poolrooms-boss.mp3` (`NativeZoneMusic.Level0Boss`,
  `PoolroomsBoss`).
- **Until they arrive:** the boss plays the Siren's song, as the zone bosses did before theirs. Its drop lands on the same hold, so the
  intro's last beat still works.
- **When a file arrives:** add it to `tools/zone_boss_beatmap.py`, as `docs/ZONE-BOSS-MUSIC.md` describes. That gives it an intro edit whose
  drop lands exactly at 9.0678 s, a bar-aligned loop and its accents. Then bind the shots' timings to the accents, as
  `NativeStationBossBeats.UseSong` does. Each song needs a clear drop; the edit handles where it falls in the file.
- **The stage's own music** (`backrooms-fluorescent-loop`, `poolrooms-untitled`) plays until the arrival, and stops dead when the boss
  arrives. That silence is the intro's first beat.

The intro tables above are written against the fallback beats, the only fixed ones being the name card at 6.43 and the drop at 9.0678.

## 6. Animation direction

Both are on the shared motion library (`docs/BOSS-MOTION.md`): a replicated root follow, actions stretched to the authority's `hitAt`, no
hard cuts between poses. What's specific to each:

**THE HUM: fluid, wrong and heavy-limbed.**
- **Gait:** two long legs and two long arms, walking on its knuckles as often as its feet, a four-limb `BossGait` with very long strides,
  planted hands locked by IK. It takes one step where a pilot takes three.
- **The head is stabilised, like a bird's:** whatever the body does, the bar of light stays level and locked on its target, and the body
  moves around it. That's the single most unsettling thing about it, so spend the most care here.
- **Second order everywhere:** the shroud strips and wire tails are `BossChain`s, the arms lag and overshoot, and the tubes whip on the
  spine.
- **The flicker hitch:** when the lamps stutter, its pose stutters with them for a frame or two, an additive layer on top of fluid motion,
  never a pop in its root. Reduced effects turns it off.
- **Through surfaces:** where it crosses a wall or ceiling, the surface's paper stretches toward it and tears (Fx), and the part inside
  is clipped.
- **On the ceiling:** in NOCLIP the same gait runs inverted, with a slow turn over from floor to ceiling through a wall.

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
