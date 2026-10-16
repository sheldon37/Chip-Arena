# Expedition: CENTRAL STATION (design draft)

Claude, 2026-10-16. Branch `claude/central-station-design`, based on shared `claude/sleepy-ritchie-g8ruhv` (`bb4b36a`).
**Status: design phase. Nothing is live.** No runtime file, live resource, catalogue state, rule, AI or network field is
changed. The blockout is a generated draft that nothing at runtime loads. The new enemies, hazards and machines are
proposals. Their stats and behaviour belong to Astra (section 8). Bosses come later, per the owner.

Owner (2026-10-16, verbatim): "lets begin the design for the Central Station and City Ironworks~ we can tweak the instances
to have specific zombies / enemies. So lets also begin creating unique enemies for those zones. We can work on visual updates
/ bosses after." Follow-up: the station must look clearly better than Ward Zero, a map players call "This is a cool map!",
using our real assets and lighting.

City Ironworks is a separate parallel task and is not covered here.

| What | Where |
| --- | --- |
| This design | `docs/EXPEDITION-CENTRAL-STATION.md` |
| Blockout generator and its checks | `docs/arenas/scripts/expedition_central_station.py` (`--fixture`, `--json`, `--png`) |
| Draft (map, area record, hub door record, look, proposals) | `unity/ChipArenaConversion/Tests/expedition-central-station-draft.json` (not live) |
| Managed check (Astra's parser and geometry gate on it) | `unity/ChipArenaConversion/Tests/ExpeditionStationDraftChecks.cs`, run from `ExpeditionLayoutChecks` |
| Python check | `unity/ChipArenaConversion/Tests/test_expedition_station_draft.py` |
| Renders (not in the repo) | listed in section 13 |

## Contents

1. Fantasy and mood
2. Layout
3. Look and set dressing, room by room
4. Asset inventory and gaps
5. The draft look entry
6. Hazards
7. The station's enemies
8. Astra contract
9. Encounter plan
10. Audio cue list (for the owner to generate)
11. Performance budget and low-graphics fallback
12. Integration steps (when the owner says go)
13. Renders, checks and what is not done
14. Open questions for the owner

## 1. Fantasy and mood

The last train left at 6:47. The station was the city's evacuation point. The army sandbagged the ticket hall, set cots under
the concourse arcades and boarded the doors from outside. Then the boards went up from the inside too. The town's facade
already tells this: CENTRAL STATION in teal, STATION CLOSED, EVAC CANCELLED, the clock stopped at 6:47.

Inside it is a grand through station at night with the power failing:
- a low ticket hall gives way to a huge concourse under a broken glass vault, with moonlight falling through the missing panes;
- a departure board still flickers DELAYED and CANCELLED over the information booth;
- beyond the platform gates, four platforms sit under a sooty train shed lit by a few sodium lamps that still work;
- one train never left: it stands at platform 2/3 with its doors open and its windows flickering;
- the express line is still live. Every so often a driverless express howls out of the west tunnel, headlight first;
- the freight line has flooded at its west end. Something moves in the water;
- at the far end, the signal box glows red over the tracks. It still controls the third rail and the signals.

The mood is "abandoned grandeur, still running". Ward Zero is a clinical lab under emergency light. The station is warm
sodium against cold moonlight, iron and glass, wet surfaces, and the sense that the building is still trying to run its
timetable. Its threats are the machine (the express, the third rail) as much as the dead.

## 2. Layout

The instance's own frame: x east, z north, y up, in metres. The town's door is at the reserved transit doorway (0, 0, -84)
on Station Road. Inside, the exit door is in the ticket hall's north wall at (0, 0, 36).

| | Value |
| --- | --- |
| Area / map / look | `exp-transit` / `expedition-transit` / `central-station` (the layout contract's ids) |
| Bounds | x -48..48, z -60..36 (96 x 96 m), ceiling 22, killY -10 |
| Levels | ground y 0 (hall, concourse, platforms, corridor); upper y 6 (balconies, north gallery, offices, footbridges, signal box); track bed y -1.2 (three pits, two throats, tunnel mouths) |
| Walkable floor | ground 4,718 m2, upper 1,880 m2, track bed 1,069 m2: 7,667 m2, about 1.5 times Ward Zero's two floors (4,981 m2 by the same 0.5 m count) |
| Blocks | 458: 226 colliding, 232 visual stair steps; 18 stairs and steps (walkable wedges, as Siren's) |
| Records | 1 exit door with 8 entries; 3 caches; 3 boss points; 47 spawns |
| Walked from the entry slots | concourse 30 m, P1 at the crossing 39 m, P2 54 m, the stalled train 66 m, the east tunnel mouth 66 m, P4 84 m, station master's office 101 m, the corridor 104 m, the signal box 104 m |


```
 z 36 ┌─────────────[EXIT]──────────────┐  N
      │   TICKET HALL  (ceiling 5.5)      │       upper y 6: offices over the hall
 z 20 │═══ gate line (3 gaps) ═══════════ │       north gallery overlooking the concourse
      │ W arcade │ GRAND CONCOURSE │ E arc│       balconies W and E (stairs up)
      │ +balcony │  board, booth   │+balc │
 z -4 │════ platform barrier (3 gates) ═══│
      │ P1 side platform                  │
      │~T1 EXPRESS~~~~~~~~~~~~~~~~~~~~~~~~│ tunnel mouths both ends (x ±44)
      │ P2 island           | footbridges │ W (x -36..-32) and E (x 32..36) at y 6
      │~T2 [car1][car2][car3]~~~~~~~~~~~~~│ the stalled train; car 2 is a walk-through
      │ P3 island                         │
      │~T3 FLOODED (west)~~~~~~~~~~~~~~~~~│
      │ P4 side platform                  │      signal box (y 6) over the east end
 z-54 │ MAINTENANCE CORRIDOR: pump room ↔ generator, back stair up to the signal box │
 z-60 └───────────────────────────────────┘
```

### Entry and the critical path

1. **The exit door** is in the ticket hall's north wall (0, 0, 36) and faces north. As in Ward Zero, its anchor is 0.6 m
   inside, its closure sits between the jambs, and its approach and 8 entries are inside, facing south. The hall is quiet:
   the nearest spawn exit is 17 m away on foot.
2. **Ticket hall → concourse.** The gate line has three gaps, and the centre one (6 m) passes a Hulk. The centre gap opens
   onto the board, the booth and the CENTRAL sign: the central axis.
3. **Concourse → P1** through the platform barrier's three gates (each 6 m).
4. **P1 → P2** by the **barrow crossing** on the central axis: a 3 m ramp into the express pit and up the other side, with
   the third rail gapped there. This is the short way and the dangerous one, because the express uses this pit. Or take the
   **footbridges** (y 6): up from the balconies, then down stairs onto P2, P3 and P4. That way is safe and longer.
5. **P2 → P3 through the stalled train.** Car 2 has doors on both sides and is a 17 m walk-through aisle. Pilots and the
   normal horde fit, a Hulk does not. Or walk round the train's ends in the T2 pit.
6. **P3 → P4** by the second barrow crossing (over the flooded freight line T3), or the west footbridge.
7. **P4 → the maintenance corridor** (two 3 m doors), **→ the signal box** up the corridor's back stair. Or the east
   footbridge leads straight into the box.

### Loops and branches

- **The shed loop** (ground and pits): concourse, P1, crossing, P2, train, P3, crossing, P4, corridor, generator stair,
  signal box, east footbridge, east balcony, concourse.
- **The upper loop** (y 6): the west balcony, the north gallery (over the gate line), the offices (Lost Property, Station
  Master, PA room, linked by inner doors) and the east balcony. It is optional and holds cache 1.
- **The pit network** (y -1.2): the three track pits meet in the throats at both ends (x ±38..44). Steps lead down from P1,
  P2 and P3. The four tunnel mouths are spawn sources, not exits. The pits are the fast, dangerous lanes.
- **Branches:** the pump room (west end of the corridor, flooded floor, a Hulk breach) and the PA room upstairs (a proposed
  interactive: the PA console).

### Sightlines and cover

- The concourse is 80 x 24 m and open. The booth and board make the centre line, with kiosks, benches, trolleys and a
  fallen girder as low and mid cover. The balconies overlook it from 6 m.
- The platforms are 76 m lanes, long sightlines for ranged kits. Lamp posts, benches, kiosks, signal cabinets and the
  footbridge piers break them up. The train cuts the P2/P3 sightline in two.
- The pits are trenches. A pilot in a pit is below the platforms' line of fire but exposed along the track (the express,
  the Linesman's arcs).
- The footbridges are 4 m wide decks 6 m up over the whole shed: firing platforms with chokes at every stair head.

### Choke points

The gate line (three gaps), the barrier gates, the barrow crossings (3 m), the car-2 doors (1.6 m), the stair heads on the
footbridges (3 m), the corridor doors (3 m), the pump-room door (3 m), and the signal box doors (3.2 m north, 1.9 m west).

### Arena rooms

- **The grand concourse** (80 x 24 m, balconies on two sides) is the big wave room and the **boss arena placeholder**.
  The boss points are (0, 0, 3), (-12, 0, 6.5) and (12, 0, 6.5), on the floor under the board, all Hulk-clear. A boss can
  arrive through the glass vault, like the Siren's Spine hatch (boss design later).
- **Platforms P2/P3 and the train** form the second arena: two 10-12 m islands, door spawns on both sides of the train,
  and the express and the flood beside them.
- **The throats** (6 x 37 m at track level, both ends) are where the Linesman and the Undertow fight best.

### Caches, machines, exit and spawns

These follow Ward Zero's conventions: anchors, interaction points 0.8 m in front, entries inside the exit, and spawns in the
`PveEntry` shape plus `allow`.

| Record | Where | Why |
| --- | --- | --- |
| `exp-transit:cache:0` | ticket office counter (-15.2, 0, 29.5) | on the way in, like Ward Zero's arcade cache |
| `exp-transit:cache:1` | station master's office (0, 6, 33.2) | the upper loop's reward |
| `exp-transit:cache:2` | inside the signal box (38.6, 6, -53) | the far objective's reward |
| Machines (proposals, not anchors) | chip (-35, 0, 13.5) under the W arcade; med (-6, 0, -16.2) on P2; recruit (35, 0, 7) under the E arcade; chip (-30, 6, 33) in Lost Property | instance machines come from PveContent in Ward Zero; Astra decides how the station gets its own |

There are 47 spawns, all using today's entrance kinds and existing horde kinds:

| Entrance | Count | Where | Proposed new entrance kind (Astra) |
| --- | --- | --- | --- |
| Train doors | 8 | cars 1 and 3, both sides, onto P2 and P3 | `train`: the door slides open, the add steps across the gap |
| Tunnel mouths | 6 | T1-T3 at both ends, out of the dark along the rails | `tunnel`: a walk-in along the track |
| Staff doors | 11 | concourse side walls 4, hall 2, corridor east 1; upstairs: offices 2, balconies 2 | |
| Grates and drains | 11 | concourse 4, P2 2, P3 2, P4 1, corridor 2 | |
| Sumps | 2 | in the flood (T3 west, the west throat) at y -1.2 | `sump`: surfacing out of water |
| Drops | 6 | brutes off the vault beams (concourse 2) and the shed roof (P2, P3); 2 Hulk drops through the glass | |
| Hulk breaches | 3 | bricked arches in the concourse's W and E walls, the pump room's end wall | |

Each spawn also carries a `station` list naming the station enemies that entrance suits. The parser ignores it; Astra can
use it as data.

### The hub side

Entering needs a door record in the town at the reserved doorway. The draft carries it (`layout.town`):
- the anchor (0, 0, -83.4), yaw 0;
- the interaction, closure and approach volumes;
- eight returns at z -79.6 and -78.0, Hulk-clear.

Today the hub record lists the station as reserved (71.7 m walked from the south gate, 10.5 s running).

**One town change will be needed at import:** the live town spawn `drop-hulk-station` lands at (0, -76), 2.2 m from those
returns. The Ward Zero rule keeps exits 8 m from a door's returns, so it must move or go. Two more import changes: the
facade kit draws the boarded doorway (`STATION CLOSED`) and would draw the open door's airlock, as at Ward Zero; and
`exp-transit` moves from `hub.reserved` to `routes`.

## 3. Look and set dressing, room by room

**Rule zero: readability first.** Enemies, telegraphs, pickups and routes always win over atmosphere:
- the danger lanes are lit red (the LED strips along every pit wall);
- shafts and haze stay at or below body height only where no enemy stands, and haze never covers a telegraph decal;
- every enemy carries its own identity light (section 7), so it reads in any room;
- the route reads by the line colours: P1 red, P2 blue, P3 amber, P4 green, taken from the facade's route map.

Lead-the-eye path: each objective has its own unique light colour:
- the board (amber) at the concourse's centre;
- the CENTRAL sign (teal) over the platform gates;
- the express signal (green or red) at the west mouth;
- the signal box (deep red) at the far south-east, the last thing you see down every platform.

Palette:

| Swatch | Hex | Where |
| --- | --- | --- |
| Station teal | `#7AE8D0` | signs, CENTRAL, the station's identity (the facade's) |
| Cream tile | `#C8BCA0` | hall and platform walls (the facade's dado) |
| Station green | `#3E5A4E` | ironwork: ribs, posts, piers, signal box |
| Sodium | `#FF9E3A` | platform lamps |
| Board amber | `#FFB45A` | departure and destination boards |
| Moonlight | `#8FAEDC` | the key through the glass; shafts `#9ABCE8` |
| Emergency red | `#FF2A22` | pit LED strips, the signal box, corridor strips |
| Fluorescent | `#D8FFF0` | troffers in the hall, the train's interior |
| Line colours | `#D8302A` `#2A8AD8` `#E8B030` `#3AB04A` | P1-P4 signage bands |

Each room below lists its hero shot, palette, lights, atmosphere, surfaces, dressing density and identity.

### Ticket hall (z 20..36, ceiling 5.5)
- **Hero shot:** from the entry slots, looking down the axis through the gate line. The board glows amber beyond and
  CENTRAL glows teal over the platform gates, framed by the low ceiling. (`ticket-hall.png`)
- **Palette:** cream tile, a dark green dado band, a brass and teal terrazzo roundel, burlap sandbags.
- **Lights:**
  - key: none (indoors);
  - fill: three rows of fluorescent troffers, a third dead and a few stuttering (cool white `#D8FFF0`);
  - practicals: the lit ticket window (warm), the red "locked" lamps on every turnstile, the WAY OUT sign (yellow),
    green radio LEDs.
- **Atmosphere:** low haze (0.42 below 2.6 m); no shafts.
- **Surfaces:** dry. Dust over everything.
- **Dressing:** high density, about 700 parts (cots, crates, a field table with a radio, police tape, EVAC CANCELLED, posters,
  the route map, the clock at 6:47).
- **Identity:** the abandoned evacuation checkpoint. The compression before the concourse's release.

### Grand concourse (boss arena)
- **Hero shot:** from the north gallery over the board: DEPARTURES, DELAYED and CANCELLED in amber; moon shafts through the
  broken vault; CENTRAL in teal; the red pit lines and sodium lamps of the platforms beyond. (`hero-concourse.png`)
- **Palette:** warm terrazzo, green iron ribs, dark glass, moonlight blue against board amber.
- **Lights:**
  - key: the moon through the glass (soft shadows from the ribs and purlins);
  - practicals: the board (amber, stutter), the info booth lamp, the cafe's smouldering fire, globe lamps on the balconies
    (one in three lit), the newsagent's fridge, the field hospital's work lamp.
- **Atmosphere:**
  - about 6 moonlight shafts through missing panes;
  - haze;
  - glass grit glinting under the girder;
  - puddles under the broken panes (wet terrazzo reflecting the board).
- **Dressing:** about 2,600 parts:
  - ribs and glazing;
  - the 4-face board with live text;
  - kiosks, benches, trolleys, luggage, a toppled vending machine;
  - cots and a red-cross banner under the W arcade (the field hospital);
  - balusters on every rail;
  - PLATFORM signs over the gates.
- **Identity:** the cathedral of the station. The boss arrives here.

### P1 and the express line (T1)
- **Hero shot:** from P1 looking west down T1 into the mouth. The express's headlights and the green signal are in the
  tunnel, its beam lies on wet sleepers, and a Linesman is silhouetted in the pit. (`express-headlight.png`)
- **Palette:** sodium on concrete, brick-red end wall, a cold white headlight, red pit lines.
- **Lights:** sodium posts every 8 m (about half lit); the signals (red, or green when the express is due); the express
  headlight as a moving light during a pass.
- **Atmosphere:**
  - the headlight beam along the pit;
  - puddles on the ballast;
  - the third rail's blue insulator glints;
  - on a pass: a dust wall, sparks off the rails, a strobing window band.
- **Dressing:** rails, sleepers every 0.7 m, the third rail with its yellow guard board, cables, the edge coping and yellow
  line, PLATFORM 1 signs (red band).
- **Identity:** "mind the gap". The machine still runs.

### P2, P3 and the stalled train (T2)
- **Hero shot:** along P2's south edge with the train on the left: lit and dead windows, NOT IN SERVICE on its destination
  display, the platform running into darkness under sodium posts and skylight shafts, a Conductor's red lantern at a door.
  (`platform-p2.png`)
- **Palette:** stainless steel with a red band, blue line signs on P2, amber on P3.
- **Lights:**
  - the train's windows (fluorescent; some flicker);
  - sodium posts;
  - timetable screens (a few alive);
  - moonlight through the skylight gaps in the shed roof.
- **Atmosphere:** moon shafts, haze, the train's interior glow spilling through open doors.
- **Dressing:**
  - benches, bins, timetable boards, signal cabinets that spark (hazard);
  - the PLATFORM 2/3 hanging signs;
  - the car-2 interior: seats, poles, flickering ceiling tubes, ads.
- **Identity:** the train that never left. The horde comes out of it.

### The flooded freight line (T3 west) and the west throat
- **Hero shot:** from P4's west end over the black water: a rusting wagon off its rails, the P3 steps going down into the
  flood, two aqua eyes and a ripple ring where an Undertow waits. (`flooded-t3.png`)
- **Palette:** black-green water, rust, the aqua of the Undertow, sodium reflections.
- **Lights:** a faint aqua glow under the water, the sodium lamps' reflections streaking on the surface.
- **Atmosphere:** low mist on the water, drips from the roof, ripples (proposal: a water look).
- **Dressing:** the wagon, debris, floating litter, a sunken trolley.
- **Identity:** something in the water.

### P4 and the maintenance corridor
- **Hero shot:** down the corridor: red emergency strips every 6 m, pipes along the wall, steam from a burst main, the
  pump room's flooded floor beyond its door.
- **Palette:** concrete, green and brown pipes, red emergency light, the hazard paint on the generator.
- **Lights:** red strips (stutter), the generator's amber warning lamp.
- **Atmosphere:** steam (hazard), a wet floor in the pump room.
- **Dressing:** pipes, cable trays, pumps, the generator, tool lockers, warning signs.
- **Identity:** the station's guts. Tight, red, loud.

### The signal box
- **Hero shot:** from the east footbridge: the box glows red at the end of the bridge, its name board, its windows over the
  whole track field, the lever frame inside. (`signal-box.png`)
- **Palette:** station green, deep red interior light, the blue track diagram.
- **Lights:** the red interior (the far beacon of the whole map); the track diagram screen.
- **Dressing:** the lever frame (red, blue, yellow and black levers), the illuminated diagram, a desk, the name board
  CENTRAL SIGNAL BOX.
- **Identity:** the brain of the station. A proposed objective: throw the levers to cut the third rail and halt the express
  for a while.

### Footbridges, balconies, north gallery and offices
- **Footbridges:** grating decks, green lattice parapets, a lamp every 14 m (some dead), the best view of the shed. The
  danger is being shot from both platforms.
- **Gallery and offices:** cream panels, desks, filing cabinets, lockers, the PA console with its red ON lamp, windows over
  the concourse. The interiors are dark; this is a quiet loop.

### Dressing density targets

| Room | Kit parts | Notes |
| --- | --- | --- |
| Ticket hall | about 700 | checkpoint clutter, troffers |
| Concourse | about 2,600 | ribs and glazing are about 900 of it |
| Shed (platforms, pits, train) | about 3,200 | rails and sleepers about 1,200; can use long strips in the low tier |
| Corridor and pump room | about 600 | |
| Signal box | about 250 | |
| Upper floor | about 500 | |
| **Total** | **about 7,850** | Ward Zero's kit is 7,124 parts, the town's 7,631 |

The concept renders use about 2,000 dressing parts plus about 530 glyph boxes of text.

## 4. Asset inventory and gaps

Every room maps to pieces we already have. No licensed pack file is committed; packs are named by id only.

| Asset | What it gives the station |
| --- | --- |
| `NativeExpeditionCityKit` and `.Landmarks` (`TransitFace`, `TransitPiece`) | The facade's identity: the cream tile dado, the arched windows, CENTRAL STATION in teal on dark green, the route-map line colours, posters and notices, the 6:47 clock, STATION CLOSED. Its glyph font (`GlyphBoxes`) letters every sign in the renders; the Poster, Message and Notice helpers. |
| Expedition states (door leaves, airlock, seals) | The exit door's leaves and seal (`claude/expedition-states`), as Ward Zero's. |
| Cyber City looks and kit (`NativeCyberCityLooks`, `NativeCyberCityKit`) | `cyber-holo` (departure boards, ticket machines, timetable screens, the signal box diagram), `cyber-tube` (LED strips, glyphs, signals, the headlight), `cyber-glass` (glazing), `cyber-grate` (footbridge decks), `cyber-panel` (stainless carriages), `cyber-metal` (rails, rails' guards, parapets). |
| Factory kit (`NativeFactoryKit`, `NativeFactoryRig`, `NativeFactoryFx`) | Pipes, cable trays, the generator room, sparks and steam effects, catwalk railings for the footbridges. |
| Ward Zero dressing (`NativeWardZeroKit`, `Fx`, `Lights`, `Field`, `Mood`, looks) | `ward-light` troffers (hall, train interior), `ward-tile` (terrazzo, cream tile), `ward-panel` (offices, the signal box), the ceiling light field for the hall's low ceiling, the darker look, and the mood states (calm, horde, boss, emergency) as the station's power states. |
| `NativeLighting` / `MapLooks.json` | Sky (`night`: the moon visible through the broken glass), key (the moon through the vault, soft shadows), rim, ambient, fog, ground haze, dust, 8 practical slots. |
| City light picker (`NativeExpeditionCityLights`) | The pattern for a station picker: about 45 candidates (lamps, boards, signals, fires) settling to 8 slots per camera. The renders use exactly that. |
| `NativeBlockLit` (base looks) | `concrete-board`, `brick-red`/`brick-buff`, `steel-green`, `timber`, `tar` (ballast), `window-lit` (lit train windows, lamps), `neon`, `corrugated`, `demolition` (the generator's hazard paint), and the dust coat (`NativeDustCoat`). |
| `NativeVfxDraw` roles, `NativeStructureFx` | Sparks, embers, drips and steam as sprite roles. The window strobe (structure telegraph) drives the train's flickering windows and the glass vault's collapse warning (boss). |
| Hazard kit (`NativeHazardKit*`, `HazardKit.json`) | Alarm beacons (amber, lockdown) and klaxon for the express warning; strobes in the corridor; lit signs `STAND BACK`, `NO ENTRY`, `EVACUATE`. No turrets or saws: they don't fit a station. |
| Horde (`NativeHordeModels`, `NativeHordeKinds`, `NativeZombieClips`) | The base bodies (brute, shambler and runner rigs) and the exported mocap the new enemies reuse. |
| Licensed packs (by id) | Animation: ZOMBIE PRO MoCap (MocapOnline): 540 files, 39 exported today; section 7 lists the clips to export. Audio: Universal Sound FX (Imphenzia, Asset Store 17256: VEHICLES/Trains, ALARMS, ELECTRICITY, MACHINES as stand-ins); Fantasy Game Sound Effects (94186); Battle Sound Library (176417); Interface and Item Sounds (89646); music: Ultimate Game Music Collection (37351), Badlands Music Pack 3 (184710). |

### Gaps, ranked by visual payoff

| # | Gap | Kind of work | Payoff |
| --- | --- | --- | --- |
| 1 | **Station dressing kit** (`NativeExpeditionStationKit`): laid by block id stem like the city kit. Vault ribs and glazing, the board with live text, platform furniture, rails and sleepers, train body dressing, signage, debris. | code, existing looks | the whole look; the renders are a draft of it |
| 2 | **A proper light-shaft quad** (soft along the length as well as across), for the moon and the headlight. The renders fake it with stacked flat layers. | atmosphere shader mode | the hero shots |
| 3 | **Water look** (a bank slot in `NativeBlockLit`): dark, glossy, ripple normal, reflection streaks of nearby lights. The renders use flat dark plates and additive streaks. | shader | the flood, puddles, wet ballast |
| 4 | **The express** (a moving train presentation): carriage mesh kit, window band strobe, headlight beam, dust and sparks. | code-sculpted meshes (like `NativeMachineModels`) | the signature event |
| 5 | **Station atmosphere** (`NativeExpeditionStationFx`): haze sheets, drips, steam, sparks, embers. | code | depth and life |
| 6 | **Station light picker and power mood** (normal, brownout, emergency red, the express pass, blackout). | code | dynamics |
| 7 | **Wet coat** (a dust-coat variant that darkens and glosses the floor under drips). | shader | surface richness |
| 8 | **The four enemy models** as horde kinds (sculpted on the rig) plus the clip exports below. | code plus pack export | identity |
| 9 | **Carriage interior** (seat moquette, ads, grab rails). | kit | close-up quality |

## 5. The draft look entry

`look` in the draft file, a MapLooks.json entry, not loaded. It validates beside today's looks (`NativeMapLooks.Validate`, in
the managed check), and every station block resolves to a listed look.

| Field | Value |
| --- | --- |
| id / look | `expedition-transit` / `central-station` |
| farClip, background | 260, `#070B0D` |
| key (the moon through the glass) | `#8FAEDC`, intensity 0.42, elevation 62, bearing 205, soft shadows, strength 0.9 |
| rim | `#3E6A7A`, 0.14, elevation 18, bearing 25 |
| ambient | sky `#2A3E58` x 0.075, equator `#24302E` x 0.06, ground `#16120E` x 0.055 (Ward Zero: 0.055 / 0.045 / 0.055) |
| fog | linear `#0B1517`, 14..120 m |
| shadows | 2 cascades, 60 m (fallback 2, 45 m) |
| sky | `night`: horizon `#16222C`, zenith `#05080C`, glow `#6F8AB0`, moon `#DDE6F0` (size 0.035, halo 0.12), stars 0.35, crater 0.3, clouds `#2A3440` at 0.3; elevation 62, bearing 205 |
| blocks | dressed, dust 0.08, haze 0.42 below 2.6 m, haze `#1E2A2A` |
| practical lights (8) | departures `#FFB45A` 1.5/18 m (stutter 7 s); hall tube `#D8FFF0` 0.9/14 (stutter); sodium P2 `#FF9E3A` 1.2/16; sodium P3 1.1/15 (stutter); signal box `#FF2A22` 1.0/12; west tunnel green `#3AFF6A` 0.7/9; corridor emergency `#FF3A2A` 0.8/12 (stutter); cafe fire `#FF7A2E` 1.1/9 (fire) |
| block looks (55) | terrazzo `ward-tile #7A7468`; platforms `concrete-board #5A5852`; ballast `tar`; walls `brick-buff`/`ward-tile #C8BCA0`/`brick-red`; ironwork `steel-green`; decks `cyber-metal`; footbridges `cyber-grate`; carriages `cyber-panel #8A9AA0`; boards `cyber-holo`; kiosks and booth `timber`; the generator `demolition` (full list in the draft) |

`dressing`, `atmosphere` and `ambience` are empty until the station kit exists.

## 6. Hazards

Reused from the hazard kit (presentation only, ships with the kit's rules): alarm beacons (amber) and a klaxon tied to the
express warning, strobes in the corridor, lit signs (STAND BACK at the platform ends, NO ENTRY at the tunnel mouths).

These are **proposals for Astra.** They are volumes in the draft's `proposals.hazards`, all inside the bounds and 2 m clear
of every entry, the door, the caches and the boss points:

| Hazard | Volume | Proposed behaviour | Telegraph (presentation) |
| --- | --- | --- | --- |
| **The express** (`train`) | the T1 pit, end to end | A driverless express on a schedule (first at about 60 s, then every 70-110 s, never during a boss intro). It passes in 2.2 s. Lethal in the pit; it also pushes pilots within 0.6 m of the platform edge (6 m/s). Zombies in the pit die too, a deliberate tactic. | 4 s ahead: the T1 signals go red; the PA chimes "stand back"; the rails sing; the headlight grows in the west mouth; amber beacons. The pass: a strobing window band, a dust wall and sparks. |
| **Third rail** (`third-rail`) | strips along T1 (gapped at the crossing), T2 beyond the train, T3 | Touching it: 25 damage and a 0.6 s stun. Arcs into the flood when live. The signal box levers can cut it, and stop the express, for 60 s (an optional objective). | Blue insulator glints, a hum near it, sparks when stepped near. |
| **Flood** (`flood`) | T3 west and the west throat, 0.35 m deep | Slows pilots by 20%. While the rail arcs or a Linesman is grounded in it: 8 dps to anyone in it. | Ripples; the water lights blue when live. |
| **Spark cabinets** (`spark`) | two signal cabinets on P2 and P3 | Every 9-14 s: a 1 s fizz, then a 2 m arc (12 damage). Reuse the Ward `grid` kind's timing. | Fizz sound, cabinet door glow. |
| **Steam main** (`gas`) | the corridor centre | The Ward `gas` kind: 2 s hiss warning, then a 3 s scald. | Hiss, steam plume. |
| **Falling glass** (`falling-glass`) | the concourse, boss encounter only | Shards fall with a 1.2 s shadow telegraph. | Shadow decal, creak, glass. |

## 7. The station's enemies

There are four new kinds. Each is built on the player-model zombie rig (`CapturedRig`) with the ZOMBIE PRO mocap, like the
Siren minions. Each has its own **identity light** so it reads at 30 m in the dark, and none repeats an existing kind's seam
hue:

| Kind | Seam hue | Existing kinds |
| --- | --- | --- |
| Linesman | electric blue `#6FD8FF` | shambler violet, runner pink, brute orange, spitter green |
| Conductor | cold white `#E8F0FF` plus a red lantern | crawler pale lavender, bloater amber, Hulk red |
| Undertow | aqua `#2EF0C0` | |
| Pileup | none; its lit carriage window is the read | |

The concept sheet (section 13) poses them beside a pilot and a shambler in the station's light.

### 7.1 The Linesman: "the third rail walks"

- **Role:** elite controller (area denial). He makes the pits and the flood deadly and punishes clumping.
- **Silhouette at 30 m:** a single cold headlamp beam sweeping the dark, blue arcs crawling up the right arm, and two
  reflective hi-vis bands catching the light. Hard hat, heavy shoulders.
- **Build:** the brute rig at scale 1.0 (heavy). Hi-vis vest (orange `#FF6A10`) with reflective bands; a hard hat with a
  cracked headlamp; jumper cables coiled round the right forearm; a battery pack on his back (glows blue: the weak point);
  arcs from the coil to the hand.
- **Animation:**

  | State | Clip | Notes |
  | --- | --- | --- |
  | idle | `idle-3` | with an electric twitch: export `Zombie_OnFire_1_SHORT` as an additive writhe |
  | move | `walk-6` (stiff) | chase: `chase-2` at 0.8 speed |
  | melee | `swipe-r2` (wind-up and strike) | |
  | arc wind-up | `Zombie_Reach_1_Loop` | export; arm raised |
  | arc strike | new | 0.25 s whip forward, from the end of `swipe-r1` |
  | ground | `Zombie_Stand to_Crawl_1` | export; 40% then a hold: kneel and grip the rail |
  | hit | `knock-1`..`knock-3` | |
  | death | `Zombie_OnFire_2A_SHORT` | export; an electrocuted collapse, and the battery bursts |

- **Attacks:**
  - Swipe: 14 damage, reach 2.0 m, hit at 0.40 s, cycle 1.4 s.
  - **Arc:** a 1.1 s wind-up. The telegraph: the headlamp flares from white to blue, arcs climb the raised arm, a rising hum,
    and the floor crackles under him. Then a bolt hits the nearest pilot in line of sight within 9 m for 18 damage, and
    chains to up to 2 more pilots within 4 m of each other for 10 each. Each target is jolted: 40% slow for 0.35 s, never
    a stun lock. Cooldown 6 s.
  - **Grounded:** standing on live rail or in the flood, his arc does +30% damage and its cooldown halves. He glows brighter.
- **Weak point:** the battery pack. Hits from his rear 120° deal 1.75x. 30 damage to the back during a wind-up cancels the
  arc (a 0.6 s stagger and a shower of sparks).
- **Counters:** break line of sight (columns, the train), keep out of the water near him, flank for the pack, kill him first
  when he heads for a pit.
- **Spawn:** out of the tunnel mouths, walking in along the rails; out of the corridor's east door (the generator room).
- **Tier:** elite, between the spitter and the brute. 1 alive early, at most 2.
- **VFX:** coil arcs (beam ribbons), a headlamp cone and halo, sparks at his feet when grounded, the arc bolt (a jagged
  ribbon chain), a burst on death, the battery glow.

### 7.2 The Conductor: "all aboard"

- **Role:** support and commander. He buffs the horde and calls surges from the train. Priority target.
- **Silhouette at 30 m:** a red signal lantern bobbing in the dark: a railway's danger light, swinging. Tall and thin, a
  long coat to the knees, a peaked cap, a white flash when the whistle blows.
- **Build:** the shambler rig scaled tall and thin (0.94, 1.13, 0.94). A long dark coat (chest, hips, thighs); a peaked
  cap with a brass band and a black visor; brass buttons; the lantern in the left hand (red, the weak point); a whistle on
  a chain.
- **Animation:**

  | State | Clip | Notes |
  | --- | --- | --- |
  | idle | `Zombie_Idle_1_v2` | export; upright |
  | move | `walk-4` | upright, stiff |
  | keep distance | `walk-4` played backward | or the turn clips |
  | whistle | new | hand to mouth, chest heave, built from `Zombie_Reach_Full_2`'s arm and a head tilt |
  | last call | `Zombie_Reach_1_Loop` on the left arm | export; the lantern raised high |
  | melee | `swipe-l1` | weak |
  | hit | `knock-2` | |
  | death | `death-front-2` | the lantern drops and shatters (red glass sparks) |

  The lantern swing is a procedural secondary motion on the left arm.
- **Attacks:**
  - Swipe: 8 damage, reach 1.8 m.
  - **Whistle, "All aboard":** a 0.8 s wind-up (he inhales, the lantern flares). Then every horde add within 14 m gets +25%
    move speed and +15% attack rate for 6 s; their seams flash red-white once and trail a faint red ribbon. Cooldown 12 s.
  - **Last call:** every 20 s while a stalled-train door is within 20 m, he raises the lantern and rings it (1.2 s
    telegraph). The director then opens 2 train-door spawns at once.
- **Weak point:** the lantern. Hits on it deal 1.5x. 40 damage shatters it: the aura ends, he can no longer whistle, and he
  flees for 4 s.
- **Behaviour:** he keeps 10-14 m from the nearest pilot, backs away from melee, and stays near the horde.
- **Spawn:** steps off the stalled train (train doors); the concourse staff doors.
- **Tier:** special (low health, high priority). 1 alive (2 late).
- **VFX:** the lantern's red halo and its swing trail, the whistle's white ring on the blast, the red buff trail on buffed
  adds, red glass on death.

### 7.3 The Undertow: "something in the water"

- **Role:** ambusher and grabber (displacement). It turns the flood and the pits into hunting ground and pulls pilots toward
  the express lane.
- **Silhouette at 30 m:** two aqua eyes just above black water and a slow ripple ring. On land: long dripping arms, a torn
  raincoat, strands of weed and cable.
- **Build:** the runner rig (lean), waterlogged. A dark green-grey skin; a torn raincoat; strands hanging from the arms;
  aqua eyes and seams; a wet sheen (proposal: a wet coat on the model).
- **Animation:**

  | State | Clip | Notes |
  | --- | --- | --- |
  | submerged swim | `Zombie_Crawl_2_Loop` | export; the root sunk 0.75 m |
  | surface | `crawl-stand` (exists) | 1.3x speed |
  | grab | `lunge-1` (exists) | |
  | drag | `Zombie_Crawl_1_Loop` played backward | export; while holding |
  | release | `knock-4` | |
  | death | `stand-crawl` | then it sinks (the root lowers 1 m over 1.2 s) |

- **Attack: Grab.**
  - The telegraph: 0.6 s of bubbles and ripples converging on the target's feet, a gurgle, the eyes lighting.
  - The lunge reaches 2.6 m, from the pit up over a platform edge 1.2 m above.
  - On a hit the pilot is held and dragged at 2.7 m/s toward the nearest pit or flood, for up to 1.5 s (4 m), at 6 damage
    a second.
  - Breaking free: the victim deals 40 damage, a teammate deals 25, or any stagger.
  - Cooldown 5 s after a release.
- **Habitat:** pits and the flood only. It never stays out of the track bed or the water for more than 3 s. Submerged, only
  its head is a hitbox.
- **Counters:** stay 2.5 m back from the edges in the flooded end, read the ripples, shoot teammates free. The barrow
  crossings are its hunting ground.
- **Spawn:** the sumps (surfacing), the T3 tunnel mouths.
- **Tier:** special. At most 2.
- **VFX:** ripple rings, bubbles, drips, splash on surfacing, eye glow, a water trail while dragging.

### 7.4 The Pileup: "rush hour"

- **Role:** heavy shield-pusher. It walls off a platform or the train aisle, forces flanking, and shoves pilots into the
  pits.
- **Silhouette at 30 m:** a lit carriage-door window, a cool white rectangle, coming at you with arms reaching round it. It
  is wide and low, with a second head at the right shoulder.
- **Build:** three commuters fused, on the brute rig scaled wide (1.28, 1.08, 1.2). A stainless carriage door held in front
  as a shield (its window lit by a dying interior tube); a second head and arm at the right shoulder; a third arm through
  the window; a handrail bent across.
- **Animation:**

  | State | Clip | Notes |
  | --- | --- | --- |
  | walk | `walk-4` at 0.7 | a heavy shuffle |
  | brace | the wind-up of `smash-1` | 0.7 s |
  | shove | `Zombie_Atk_Arms_4_SHORT_Loop` | export; a new two-handed push |
  | crush | `smash-2` | |
  | hit | `knock-5` | small, heavy |
  | split death | `death-front-1` | then 2 crawlers stand out of it (`crawl-stand`) |

- **Attacks:**
  - **Shove:** a 0.7 s wind-up (it leans back and the door scrapes the floor). Then a 2.4 m, 70° cone: 20 damage and a
    6 m/s knockback. Near a platform edge, that is into the pit. Cooldown 4 s.
  - Crush: 24 damage, reach 2.2 m.
- **Defence:**

  | Hit from | Damage | Effect |
  | --- | --- | --- |
  | the front 70° (the door) | 0.25x | bullets spark off it |
  | the back and sides | 1.0x | |
  | the second head | 1.5x | the weak point |

- **On death** it splits into 2 crawlers (a proposed kind already), or 2 shamblers until crawlers ship.
- **Spawn:** the train doors (cars 1 and 3), mid and late phases.
- **Tier:** heavy, between the brute and the Hulk. At most 1 alive.
- **VFX:** the window glow (it flickers when hit), sparks on frontal hits, door scrape sparks, a dust burst on the shove.

### Boss ideas (later; not designed here)

Recorded so they are not lost:
- the **Voice of the Station**: a PA-system Siren echo, screaming through every speaker in the concourse;
- the **Express itself**: a fused locomotive horror coming out of the west tunnel;
- the **Stationmaster**: the station's last official, holding the timetable.

## 8. Astra contract (proposal)

These numbers are starting points in today's `PveAddStats` units. Astra owns every value.

| | Linesman | Conductor | Undertow | Pileup |
| --- | --- | --- | --- | --- |
| id | `linesman` | `conductor` | `undertow` | `pileup` |
| hp | 260 | 140 | 120 | 700 |
| walk / run (m/s) | 1.2 / 2.2 | 1.4 / 2.6 | 1.6 / 3.2 (3.6 submerged) | 0.9 / 1.6 (shove carry) |
| melee (damage, reach, hit, cycle) | 14, 2.0, 0.40, 1.4 | 8, 1.8, 0.34, 1.2 | grab: 2.6 reach, hit 0.6, 6 dps hold | crush 24, 2.2, 0.55, 2.0 |
| special | arc 18 + 2 chains x 10, 9 m, wind-up 1.1, cd 6 (3 grounded) | whistle 14 m aura, wind-up 0.8, cd 12; last call cd 20 | grab drag 2.7 m/s, up to 1.5 s, cd 5 | shove 2.4 m 70° cone, 20 damage, 6 m/s, wind-up 0.7, cd 4 |
| stagger | 0.3 (a back hit of 30 in the wind-up cancels) | 0.45 | 0.4 (any stagger releases) | 0.15 (only the head or the back) |
| limb / head fractions | 0.45 / 0.5 | 0.35 / 0.4 | 0.35 / 0.45 | 0.55 / 0.6 |
| capsule (radius, height) | 0.48, 1.75 | 0.40, 1.9 | 0.42, 1.7 (head only when submerged) | 0.75, 2.1 |
| mass (presentation) | 1.6 | 0.9 | 0.85 | 4 |
| reward | 70 | 90 | 60 | 160 |
| alive cap | 2 | 1 (2 late) | 2 | 1 |
| damage multipliers | back cone 120°: 1.75 | lantern 1.5; shatters at 40 | | front 70°: 0.25; head 1.5 |

### AI states

| Kind | States |
| --- | --- |
| Linesman | wander, hunt, (seek rail), ground (recharge 0.8 s), arc wind-up, arc, melee, stagger |
| Conductor | follow the horde, keep distance (10-14 m), whistle wind-up, whistle, last call (near a train door), flee (lantern broken, or melee within 3 m), melee |
| Undertow | submerged (moves only in pits and water), stalk (the nearest pilot within 2.6 m of an edge or in water), surface, grab, drag, release, retreat (submerge) |
| Pileup | advance (turns slowly at 60°/s, the shield toward the nearest pilot), brace, shove, crush, stagger (only from the back or the head), split (death) |

### Spawn weights

Mix fractions per phase. The phases are the instance's quota, 0-100%. New kinds are added to the existing mix.

| Phase | shambler | runner | spitter | brute | Hulk | Linesman | Conductor | Undertow | Pileup |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Arrival 0-15% (concourse) | 0.7 | 0.25 | 0.05 | | | | 0.03 | | |
| Platforms 15-50% | 0.5 | 0.3 | 0.12 | 0.06 | 0.03 | 0.04 | 0.04 | 0.03 | |
| Deep shed 50-85% | 0.42 | 0.3 | 0.12 | 0.1 | 0.05 | 0.06 | 0.05 | 0.05 | 0.03 |
| Final push 85-100% | 0.4 | 0.3 | 0.1 | 0.12 | 0.06 | 0.06 | 0.06 | 0.04 | 0.04 |

By entrance:

| Entrance | Proposed kinds |
| --- | --- |
| Train doors | shambler, runner, brute, Conductor, Pileup |
| Tunnel mouths | runner, brute, Linesman (Undertow in T3) |
| Sumps | Undertow, shambler |
| Concourse doors | everything but the Undertow |
| Upstairs doors | shambler, runner, spitter |

The draft's `station` list on each spawn records this.

### Networking: what the presentation needs

Replicated fields are authority state. Derived values are computed from replicated state on every peer.

| Field | Replicated or derived | Used for |
| --- | --- | --- |
| `addKind` | replicated, existing | the four new ids must be added to `NativeHordeKinds` (Claude) and `PveContent` (Astra) |
| `LastAttack`, `Attack` (`arc`, `whistle`, `grab`, `shove`) | replicated, existing pattern | wind-ups and strikes timed as today |
| Linesman `arcTargets[]` + `arcAt` | replicated, one event per strike | the bolt and the chains, drawn to the right pilots |
| Linesman `grounded` | replicated | the brighter state; the rail and flood state is authority |
| Conductor `aboardUntil` (per buffed add) | replicated, or derived from `whistleAt` + aura membership if Astra replicates the list | the red buff flash and trail |
| Conductor `lanternBroken`, `lastCallAt` | replicated | the lantern, flee, the bell |
| Undertow `submerged`, `surfaceAt` | replicated | sink and surface, ripples |
| Undertow `grabTarget`, `grabAt`, `releaseAt` | replicated | the drag pose on both bodies, and the victim's camera |
| Pileup facing | derived (yaw) | the shield side |
| Pileup `splitAt` + child ids | replicated (the death event) | the split |
| Express `trainAt` (next pass start), `line` | replicated (schedule) | warning 4 s before, the pass |
| Third rail `live` per section, `changedAt` | replicated | glints, sparks, the hum |
| Flood `live` | replicated | blue water |
| Signal box lever state | replicated | the levers, the signals |

Animation impacts: the new kinds' strikes follow the authority's hit times, as `NativeHordeKinds.Strike` does today. The grab
moves the victim's capsule (authority), and the presentation only follows it. The shove's knockback is authority. Nothing
here changes an existing kind's hit timing, hitboxes, damage, cooldowns, movement or networking.

## 9. Encounter plan

The station runs on the instance's kill quota, like Ward Zero. Phases follow the quota; the director owns the pacing.

| Phase | Rooms | What happens | Kinds |
| --- | --- | --- | --- |
| Arrival (0-15%) | hall to concourse | The concourse doors and grates trickle. Late in the phase, one Conductor appears on the concourse's far side and teaches the whistle. | shambler, runner, spitter; Conductor x1 |
| Platforms (15-50%) | P1, P2, P3, the train | The train doors open (driven by the Conductor's last call). The express makes its first pass, with full warning. The first Linesman walks out of the west tunnel. Spitters on the islands. | + brute, Linesman, Conductor, a Hulk breach (concourse) |
| Deep shed (50-85%) | T3, P4, the corridor, the signal box | The flood wakes (Undertow). A Pileup comes off the train. Brutes drop off the shed roof. A Hulk through the pump-room wall. Optional: throw the levers (cut the rail, halt the express for 60 s, open cache 2). | everything |
| Final push (85-100%) | back to the concourse | The horde converges through the platform gates. Hulk drops through the glass. | everything |
| Boss (placeholder) | the concourse | The boss arrives through the vault (design later). Falling glass. Adds from the platform gates and the balconies' doors. | boss + Conductor, Linesman |

## 10. Audio cue list (for the owner to generate)

The keys follow the bank's style. Space: P = positional 3D, F = 2D, O = loop. Each has a one-line brief and an
ElevenLabs-style prompt. No sound was generated for this design.

### Music states

| Key | Brief / prompt |
| --- | --- |
| `music.station.explore` | Sparse, eerie ambient bed for a dead train station at night: low drone, distant metallic resonance, slow piano notes, 70 BPM, loopable. |
| `music.station.horde` | Driving industrial horror loop, 120 BPM, train-rhythm percussion (wheel clacks as hi-hats), distorted bass, rising tension. |
| `music.station.express` | 6 s stinger for the express warning: a rising horn chord into a rail-roar crescendo, cutting off. |
| `music.station.boss` | Big orchestral-industrial boss loop with a station-bell motif and train brakes as percussion, 140 BPM. |
| `music.station.extract` | Relief cue: warm pad, a single clean station chime, fades out (8 s). |

### Room ambiences (O)

| Key | Brief / prompt |
| --- | --- |
| `amb.station.hall` | Indoor hall room tone: buzzing fluorescent tubes, a distant radio hiss, papers rustling in a draught. |
| `amb.station.concourse` | Huge glass-roofed hall at night: wind through broken panes, glass creaks, water dripping into puddles, a far-off fire crackle, faint echo. |
| `amb.station.shed` | Train shed at night: sodium lamp hum, rain ticking on a metal roof, distant clanks and cable ticks, faint wind from tunnels. |
| `amb.station.tunnel` | Deep tunnel mouth: low wind moan, dripping water, far metallic groans. |
| `amb.station.flood` | Still flood water in a tunnel: slow drips, gentle sloshes, a bubble now and then, hollow echo. |
| `amb.station.corridor` | Service corridor: steam hiss in pipes, a pump thumping, an electrical hum, a buzzing emergency light. |
| `amb.station.signalbox` | Signal box: relay clicks, an old electrical hum, a ticking clock, wind against the windows. |

### The express and the rail

| Key | Space | Brief / prompt |
| --- | --- | --- |
| `station.train.warn` | P | Railway warning: two-tone level-crossing bell ringing urgently, 4 s. |
| `station.train.approach` | P | Express train approaching fast from a tunnel: rising rumble, rails singing, a horn blast, 4 s. |
| `station.train.pass` | P | A high-speed train thundering past at close range: roar, wheel clatter, air rush, 2.5 s. |
| `station.train.recede` | P | The train receding into a tunnel: Doppler fall, rattling fade, 3 s. |
| `station.rail.hum` | O | A live third-rail electrical hum with soft crackle, loopable. |
| `station.rail.arc` | P | A violent electric arc from a rail: a sharp crack and a sizzle, 0.8 s. |
| `station.rail.cut` | P | A heavy breaker cutting power: a clunk and a descending electrical whine, 1.5 s. |
| `station.signal.lever` | P | A heavy mechanical signal lever thrown: a ratchet, then a clunk, 1 s. |
| `station.signal.change` | P | A railway signal relay click with a small lamp buzz, 0.4 s. |

### The PA (F, voice: calm, slightly broken, a female station announcer, echoing)

| Key | Line |
| --- | --- |
| `station.pa.chime` | three-note station chime |
| `station.pa.standback` | "The train now approaching platform one does not stop. Please stand back." |
| `station.pa.delayed` | "We apologise for the delay to the six forty-seven express." |
| `station.pa.evac` | "This station is closed. Please proceed to the evacuation point." (distorted, looping fragment) |
| `station.pa.glitch` | the announcer's voice breaking into feedback and a low wail (the Siren bleeding through) |

### Hazards and props (P)

| Key | Brief |
| --- | --- |
| `station.spark.fizz` | electrical fizzing from an old cabinet, 1 s |
| `station.spark.arc` | a cabinet arc discharge, a crack and a buzz, 0.6 s |
| `station.steam.burst` | a pipe bursting, a loud hiss blast, 2 s |
| `station.glass.creak` | a large glass pane creaking under stress, 1.5 s |
| `station.glass.fall` | a glass pane falling and shattering on stone, 1.5 s |
| `station.water.splash` | a body surfacing from waist-deep water, 1 s |
| `station.board.flip` | a split-flap or LED board flicker tick, 0.3 s |
| `station.door.slide` | a carriage door sliding open with a pneumatic hiss, 1 s |

### Enemy cues (P)

| Key | Brief / prompt |
| --- | --- |
| `pve.station.linesman.idle` | Zombie groan mixed with a crackling electrical buzz, raspy, 2 s. |
| `pve.station.linesman.charge` | Rising electric whine building over 1.1 s with crackling arcs. |
| `pve.station.linesman.arc` | A lightning-bolt crack with an electric zap chain, 0.6 s. |
| `pve.station.linesman.ground` | A heavy hand on a metal rail, then a deep electrical surge, 1 s. |
| `pve.station.linesman.hurt` | A zombie grunt with a static pop. |
| `pve.station.linesman.death` | Electrocution: a buzzing scream, then a battery bursting with a pop and sizzle, 1.6 s. |
| `pve.station.conductor.inhale` | A rattling dead breath drawn in before a whistle, 0.8 s. |
| `pve.station.conductor.whistle` | A shrill railway guard's whistle, distorted and too long, 1 s. |
| `pve.station.conductor.bell` | An old handbell rung urgently, 1.2 s. |
| `pve.station.conductor.mutter` | A hoarse zombie voice muttering a timetable, unintelligible, 2 s. |
| `pve.station.conductor.lantern` | Glass and tin lantern shattering, 0.6 s. |
| `pve.station.conductor.death` | A wheezing collapse and a lantern clatter, 1.4 s. |
| `pve.station.undertow.bubble` | Bubbles rising in still water, ominous, loopable 2 s. |
| `pve.station.undertow.surface` | Something bursting out of water with a wet gurgling snarl, 0.8 s. |
| `pve.station.undertow.grab` | A wet slap and a grip, then a choking gurgle, 0.6 s. |
| `pve.station.undertow.drag` | A body dragged through shallow water, loopable 1.5 s. |
| `pve.station.undertow.death` | A drowning gurgle and a sink splash, 1.4 s. |
| `pve.station.pileup.moan` | Several zombie voices moaning together, layered, muffled behind metal, 2.5 s. |
| `pve.station.pileup.brace` | A heavy steel door scraping across concrete, 0.7 s. |
| `pve.station.pileup.shove` | A big metal slam and a whoosh of a body thrown, 0.6 s. |
| `pve.station.pileup.ricochet` | A bullet ricochet off thick steel, 0.3 s. |
| `pve.station.pileup.split` | Wet tearing flesh and a steel door clanging down, 1.5 s. |

## 11. Performance budget and low-graphics fallback

| Room | Kit parts | Draws (of the arena's 60) | Practical lights (8 slots, picked per camera) | Atmosphere quads |
| --- | --- | --- | --- | --- |
| Ticket hall | 700 | 2 | 3 troffer rows, the ticket window, WAY OUT | 60 |
| Concourse | 2,600 | 6 | the board, booth, fire, balcony globes, fridge, field lamp (about 9 candidates) | 260 (6 shafts, halos, puddle streaks) |
| Shed (platforms, pits, train) | 3,200 | 7 | sodium posts (about 16 lit), signals (6), the express (moving), train windows | 380 |
| Corridor, pump room | 600 | 1 | red strips (2 candidates), the generator lamp | 80 (steam) |
| Signal box | 250 | 1 | the red interior, the diagram | 30 |
| Upper floor | 500 | 1 | the PA lamp, bridge lamps | 20 |
| **Total** | **about 7,850** (Ward Zero's kit 7,124, the town's 7,631) | **18** (blocks 2) | **8 of about 45** | **at most 900** (buffer 2,800) |

Rules, as for the town and Ward Zero:
- one shadow-casting draw (about 600 parts: ribs, beams, the train, piers);
- no point-light shadows;
- nothing allocates per frame;
- the express adds about 300 parts while it passes, in its own draw.

**Low tier and compatibility rendering:**
- detail parts out (balusters, sleepers become one striped strip per track, glazing bars, debris);
- shafts halved and shortened, no reflection streaks;
- haze only (no sheets);
- 4 practical slots;
- moon shadows off: the glass's light pattern becomes a baked lighter floor tint under the vault.

The PvE frame budget stays as the M1 acceptance measures it on Windows; this is not measured here.

## 12. Integration steps (when the owner says go)

1. **Owner:** approve the layout and the enemies (or ask for changes).
2. **Claude:**
   - turn the draft into the live map;
   - add the hub door record and move the town's `drop-hulk-station`;
   - bump the layout revision;
   - write the station dressing kit, atmosphere, lights, look and the enemy models;
   - export the clips listed in section 7 with `tools/export_pack_zombie.mjs`.
3. **Astra:**
   - extend `tools/generate_expedition.py` (navigation for `expedition-transit`: its prefix and the 0.5 m grid, as Ward Zero's);
   - the parser's spawn source (the station has its own spawns, already allowed) and `instances`;
   - the new kinds' rules, the hazards, the director's phases and the networking fields in section 8;
   - the catalogue moves CENTRAL STATION from InDevelopment when it is playable.
4. **Both:** Windows build and live acceptance, as M1.

## 13. Renders, checks and what is not done

Renders are offline, through the real `NativeBlockLit` shader and night sky in the WebGL harness: the draft's look, its 8
light slots, practical lights picked per camera, and posed rig models lit by the actor lights. They are in
`scratchpad/renders-central-station/` (not in the repository):

| Render | What |
| --- | --- |
| `plan-both-levels.png` | the plan: ground and track bed, the upper floor, labels, records, spawns, hazards |
| `hero-concourse.png` | the concourse postcard |
| `ticket-hall.png` | the first view |
| `express-headlight.png` | the express line and the Linesman |
| `platform-p2.png` | P2 and the train |
| `train-car2.png` | car 2's aisle and the Pileup's window |
| `flooded-t3.png` | the flood and the Undertow |
| `signal-box.png` | the box from the east footbridge |
| `overview.png`, `overview-north.png` | cutaway obliques |
| `enemy-sheet.png` | the four kinds beside a pilot and a shambler, in the station's light, plus a close-up of each in its habitat |
| `compare/ward-*.png` | Ward Zero's instance through the same harness, for the step up |

Checks:
- the generator's own checks (listed at the top of the script);
- `ExpeditionStationDraftChecks`: Astra's `NativeExpeditionLayout.Parse` and `ValidateGeometry` accept the live manifest
  plus the station and its hub door;
- the draft look validates;
- `test_expedition_station_draft.py`: the fixture is the script's output, and the live resources are byte-identical.

**Not done:** navigation for the station; the dressing kit, atmosphere, light picker and enemy models in the game (the
renders use a harness-side concept dressing); any rule, AI or network work; audio; Unity, Windows or live play.

## 14. Open questions for the owner

1. **Layout:** a through station with four platforms, a stalled train, a flooded freight line and a signal box at the end.
   Is the size right (about 1.5 times Ward Zero's walkable floor), and is the concourse as the boss arena right?
2. **The express:** a lethal scheduled train is the signature hazard. Should it also kill zombies (a tactic), and should the
   signal box's levers be an optional objective that stops it?
3. **Barrow crossings:** the short route to the islands crosses the live track at rail level. Keep it risky, or add a third
   footbridge?
4. **Enemies:** are the Linesman, Conductor, Undertow and Pileup the right four? Any names to change?
5. **Grabs:** the Undertow drags a pilot. Is a grab (loss of control for up to 1.5 s) acceptable in this game?
6. **Text:** the board's lines (6.47 DELAYED, CANCELLED) and the PA lines. Any you want in particular?
7. **Music:** one station theme with states (explore, horde, express, boss), or a track per room?
