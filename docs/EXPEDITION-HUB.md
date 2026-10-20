# Expedition hub: fenced sanctuary, gates, wilds (exp-layout-3)

Claude, 2026-10-15, branch `claude/expedition-hub` (base shared `claude/sleepy-ritchie-g8ruhv` at `ec7a7eb`).
Status: implemented on the branch, not merged, not released. The layout, fence collision, dressing and gate
presentation are live in the imported town. Gate interaction, the hub spawn director, money and the sanctuary
rules are proposals for Astra (the contract is at the end).

Owner request (2026-10-15, verbatim): "For expedition, lets extend the hub map a little bit. I want zombies to spawn
around the hub, not as much as inside an instance of course~ but still some way to gather money and have some danger.
I think players need to run a bit further to get to their instances. Also, for the safe hub, lets maybe fence off the
area, and use animated wooden / metal gates that players can open. Prevent zombies from entering safe area. Implement
some kind of mechanic so players can't just sit in the safe zone and kill zombies."

## The city grown (exp-layout-7, Claude, 2026-10-20)

Branch `claude/expedition-city-bigger` (base shared `d555363`, merged with `b0e0a12`); on the branch, not merged. Owner
(verbatim): "I will also like to make the expedition city still a little bigger (I want players to walk further still.)
Afterwards, lets begin improving the design quality of the city". This is the first part only; the graphics pass is next.
The numbers in the sections below this one are exp-layout-3's unless they say otherwise.

**How.** `expedition_city.py` ("THE CITY GROWN") moves everything beyond the city's edge (the line |x| = 44 east and west,
z = -44 south) 24 m outward: the three wilds districts, the instance frontages and their doors, the corner masses. A block
that straddles the edge is stretched across the gap; a face on the edge stays with its own block. The sanctuary, the inner
city, the ring road, the forecourts, the final plaza and the north edge do not move. Each 24 m band holds an **outer
quarter**, the city's last lit streets before the old checkpoint (authored once in an arm's frame and laid on all three):

| a (m out from the edge) | What |
| --- | --- |
| 0-5 | a cross street: the forecourt's (or the station square's) axis ends against the landmark's face, so the party turns |
| 5-17.5 | the landmark (c -16..13): the Exchange (east), the Union Hall (west), the Tram Depot (south); the main lane round it on the left (c 13..20, 7 m), the back alley on the right (c -20..-16, 4 m) |
| 17.5-24 | a square before the old checkpoint, where the instance's beacon shows down the wilds again |
| beyond c ±20 | building rows close the band (east and west: two on each side and a back mass; the south band's sides are the stretched station blocks) |

Cover (a wreck in the lane, two on the square, crates, a barrier, a dumpster) and five spawns per quarter: the landmark's
front door on the cross street, a door on each lane, a grate in the main lane, a brute drop off the landmark's roof into the
cross street. Each quarter is its own hub zone (`<arm>-quarter`, ring mid). The hub record's routes now carry `via`, their
turning points down the streets (the presentation's chevrons, metres and wall signs follow them; nothing else reads them).

| | exp-layout-6 (before) | exp-layout-7 (after) |
| --- | --- | --- |
| Town bounds | 192 x 156 m, 29,952 m² | 240 x 180 m, 43,200 m² (+44%) |
| Street (outside the final plaza, 0.5 m pilot floor) | 7,978 m² | 9,386 m² |
| Navigation | 9,505 nodes, 17,602 links | 11,040 nodes, 20,372 links (all reached from slot 0) |
| Blocks / spawns / zones | 203 / 84 / 11 | 240 / 99 / 14 |
| Doors (Ward Zero, station, foundry) | x 84, z -84, x -84 | x 108, z -108, x -108 |
| Join circles | (79, 0), (0, -79), (-79, 0) | (103, 0), (0, -103), (-103, 0); r 2.5, h 4 unchanged |
| Walk, slots to the circle (mean of the 8; nav graph, any-angle) | 81.0 / 79.0 / 80.2 m (11.9 s running) | 111.8 / 109.2 / 111.3 m (16.4 s running, 12.2 s sprinting): x1.38 / 1.38 / 1.39 |
| Walk, route gate to the circle | 67.1 / 65.1 / 66.3 m | 97.9 / 95.2 / 97.4 m: x1.46 / 1.46 / 1.47 |
| Walk, slots to the doorway | 83.6 / 82.9 / 83.6 m | 114.7 / 112.8 / 114.5 m |
| Route record, gate to the doorway (0.5 m grid) | 72.9 / 71.7 / 72.9 m | 107.0 m each |
| Circle to circle (Ward Zero-station, station-foundry) | 135.8 / 136.5 m | 197.3 / 200.2 m |

The 24 m is what lands both walks in the requested +35-50%: 20 m gave x1.33 from the slots on the any-angle measure; the
quarter's turn adds about 6 m over the straight push.

**Per-frame cost.** The kit grew to 15,382 parts (12,783; 8,955 low tier, 7,711; 580 casting, 462). The runtime now leaves
far parts out by size (`NativeExpeditionCityKit.Range`, squared once per map): anything that glows (lit windows, neon, lamps,
fires, beacons) to the fog's end plus 25 m (200 m); other parts to 80 + 25 L m (L their largest extent); detail to
50 + 25 L m, at most 110 m. Drawn per frame, from the east gate, 25/50/75% along the Ward Zero route, its circle and the
sanctuary: 12,524 / 11,850 / 10,367 / 8,327 / 6,888 / 12,519 parts in at most 26 draws (exp-layout-6 drew all 12,783 in 26
from everywhere), 2.71 M vertices at most (2.76 M); low tier at most 8,159 (7,711). Blocks 240 (203). Skyline 1,572 boxes
(1,809), atmosphere at most 662 quads (684), 91 light sources for the picker (79).

**What the authority reads that changed (old → new).** All formats are unchanged; Astra's code is untouched.

| Data | Change |
| --- | --- |
| `UnityMaps.json` `expedition-city` | bounds x ±96 → ±120, z -96..60 → -120..60; blocks 203 → 240 (37 new, the moved and stretched ones); pilot exclusion columns 68 → 81; navigation regenerated |
| Doors (`exp-city:door:*`) | anchors 83.4 → 107.4 out (x or -z); interact, closure, approach and returns moved with them (24 m along the arm) |
| `doors[].join` | centres 79 → 103 m out; r, h unchanged |
| Town spawns | 84 → 99: the 84 keep their `e` and ids (those beyond the edge moved 24 m with their walls); 15 new `*-xq-*`, `*-fq-*`, `*-tq-*` at e 84-98 |
| Hub zones | far zones moved out with the wilds (`far-east` x 44..84 → 68..108 and so on); new `east-quarter`, `west-quarter`, `south-quarter` (mid, 5 spawns each); near and mid unchanged |
| Hub `wilds`, `routes` | wilds moved out 24 m; routes' path 72.9 → 107.0 m, plus the presentation-only `via` |
| Revision | the live manifest exp-layout-6 → exp-layout-7 (fresh runs only; old clients are refused by the fingerprint) |

Unchanged: the sanctuary, its fence, gates, slots, machine and caches, the final gate, plaza and boss records, the instance
areas (byte for byte), every rule. The hub population needs no retuning for the size: it spawns 12-60 m from living pilots
and despawns 8 s past 60 m, so the density round a party is what it was; the three quarters add 15 mid-ring spawn points
in the new streets, so a party crossing them meets the same pressure as on the ring road.

**Regenerating.** `expedition_layout.py --fixtures` (town, base manifest, join draft), then the town doors in
`Tests/expedition-central-station-activation.json` and `Tests/expedition-ironworks-layout-activation.json` (moved 24 m with
the doorways), then the live manifest's town area from `tools/generate_expedition_live.py` (its returns now follow the
circle) with the instance areas kept as they are (the composer no longer reproduces the foundry area edited after it, see
DEV_LOG), then `expedition_city.py --maplooks`, `tools/generate_expedition.py`, `expedition_central_station.py --fixture`
and `expedition_ironworks.py --fixtures`.

**Seams for the graphics pass.**
- The landmarks (`bldg-xq-exchange`, `bldg-fq-union-hall`, `bldg-tq-tram-depot`) are the generic civic front; each wants its
  own face (`NativeExpeditionCityKit.KindOf`, `Building`), as the city hall and Meridian have.
- The quarter rows are generic shops and tenements; the back masses (`bldg-*-back`) and the corner masses show only roofs.
- The squares and cross streets carry only cover; the plan leaves room for a monument, a kiosk or market stalls on each
  square (keep 2.8 m round the drop and clear of the route's `via`).
- The range constants (`PartBase`, `DetailBase`, `PerMetre`, `DetailRange`) are the budget lever: new detail can be paid for
  there, and a part that must read from far (a landmark's lettering) can glow or be made larger.
- The route's `via` points are the street's centre line for wayfinding (markers, signs, light pools).
- The distant ambience cues still sit 150-230 m out; the bigger town's corners reach 170 m.

Checks: `CoreTests --expedition-city` (`Tests/ExpeditionCityChecks.cs`), with the re-measured pins in the layout, join,
station, ironworks, hub, laser-door and dressing checks (DEV_LOG lists them).

## The plan in numbers

| | exp-layout-2 (before) | exp-layout-3 (this branch) |
| --- | --- | --- |
| Town bounds | 120 x 120 m (x, z +-60) | 192 x 156 m (x +-96, z -96..60) |
| Walkable street (outside the final plaza) | 2,901 m2 | 7,978 m2 |
| Sanctuary | 24 m plaza, 1.1 m barricades with four 6 m gaps | the same 24 m plaza (same volume), closed by a 3.2 m fence across its four street mouths, a 4 m gate in each |
| Ward Zero door | x 44 (32 m from the plaza's edge) | x 84, out across the Meridian Approach |
| Reserved foundry / transit doorways | (-44, 0) / (0, -44) | (-84, 0) / (0, -84) |
| Run from the gate to the Ward Zero door | about 31 m (4.6 s running), straight line from the old barricade gap | 72.9 m walked (70.2 m straight): 10.7 s running, 7.9 s sprinting |
| Town spawns | 50 | 84 (34 new in the wilds; 4 moved clear of the gates) |
| Town navigation | 4,057 nodes, 7,367 links | 9,505 nodes, 17,602 links |
| Blocks | 124 | 203 |

Run speed is NativeArena's 6.8 m/s at speed 1.0; sprint is x1.35 (9.18 m/s) and drains 12 stamina a second. The walked
distance is an 8-connected path on the pilot's 0.5 m floor grid from the gate's threshold (the centre of its opening)
to the door's approach. The target is 70 m or more. The reserved routes measure the same: 72.9 m to the foundry plot
and 71.7 m to the station.

`python3 docs/arenas/scripts/expedition_city.py --png <file>` draws the plan offline. The renders are not in the repository.

## Layout

The town is still the ring-and-cross city around the sanctuary. Each of the three instance plots (east, west, south)
now sits at the far end of a district of wilds that opens off the ring road. The north (the final gate, the final plaza
and the city hall) is unchanged.

```
                 final plaza (unchanged)
                 [ final gate z 36 ]
   Ironworks       N gate (wood)        Meridian
   Yard (west)  W gate  SANCT  E gate   Approach (east)  -> Ward Zero door x 84
   foundry x-84 (wood)  24 m  (metal)
                       S gate (metal)
                    Station Road (south) -> transit plot z -84
```

### Sanctuary, fence and gates

- The sanctuary volume is unchanged: centre (0, 4, 0), 24 x 8 x 24. The inner blocks' walls enclose the plaza except at
  its four street mouths. The fence closes each mouth just outside the volume, at |x| or |z| 12.0..12.4. The sanctuary
  therefore ends at the fence's inner face, and a gate's threshold is outside it.
- The fence is 3.2 m tall and 0.4 m thick, in 8 sections (`sanctuary-fence-<kind>-<side>-<k>`). Every section carries a
  pilot exclusion column from 3.1 m up (NativeUnityMaps.ExcludeTops), so a single or air jump (1.57 m and +1.27 m)
  cannot clear it.
  - North and west mouths: a wooden palisade on the civic and residential side.
  - East and south: scrap-metal panels toward the Meridian lab and the station yards.
- The 1.1 m barricade ring is gone.

| Gate | Kind, motion | Centre (fence plane) | Out (yaw) | Opening | Closure (collider) |
| --- | --- | --- | --- | --- | --- |
| `exp-city:hub-gate:north` | wood, swing | (0, 0, 12.2) | 0 | 4.0 x 3.2 m | centre (0, 1.6, 12.2), 4.0 x 3.2 x 0.4 |
| `exp-city:hub-gate:east` | metal, slide | (12.2, 0, 0) | 90 | 4.0 x 3.2 m | centre (12.11, 1.6, 0), 4.0 x 3.2 x 0.62 (reaches the inner track) |
| `exp-city:hub-gate:south` | metal, slide | (0, 0, -12.2) | 180 | 4.0 x 3.2 m | centre (0, 1.6, -12.11), 4.0 x 3.2 x 0.62 |
| `exp-city:hub-gate:west` | wood, swing | (-12.2, 0, 0) | 270 | 4.0 x 3.2 m | centre (-12.2, 1.6, 0), 4.0 x 3.2 x 0.4 |

Each gate record also carries these:
- `interact`: 4.4 x 3 x 4.4, reaching 2.2 m either side of the fence;
- `sensor`: 6 x 3.4 x 8, the auto-close watch volume;
- `inside` and `outside`: threshold points 1.6 m either side;
- `fence`: the two flanking section blocks.

The openings pass a Hulk (2.0 m clear either side of the centre, no lintel; the header beam is at 4.0 m).

### Wilds districts (the far ring)

| District | Volume | Leads to | Cover |
| --- | --- | --- | --- |
| Meridian Approach (east) | x 44..84, z -30..30 | the Meridian Biocontainment frontage and the Ward Zero door at x 84 | evacuation checkpoint chicane at x 47, booth and sandbag nests, military truck and APC, stacked shipping containers, rubble heap, ruined walls, power pylon, floodlights at the forecourt |
| Ironworks Yard (west) | x -84..-44, z -30..30 | the sealed foundry plot (CITY IRONWORKS) | chicane, three rail boxcars on two tracks, slag heaps, coal tank, crates, container, pylon, floodlight |
| Station Road (south) | x -30..30, z -84..-44 | the sealed CENTRAL STATION | chicane, two crashed buses, ticket booth, bus shelters, rubble, cars, floodlight |

Sight lines:
- The east route runs down the z = 0 axis. From the east gate, the Meridian portico's beacon and floodlights are visible
  72 m away through the fog.
- Every piece of cover off the axis is lower than eye height on the axis itself; the one exception is the 1.2 m rubble
  heap, which you step round.
- The chicane at each district's mouth breaks the straight run without blocking the view.
- The north gate still looks straight up the avenue to the final checkpoint, 24 m away.

### Spawn zones

Every town spawn record outside the final plaza lies in exactly one zone. The nine final-plaza spawns stay the final
encounter's. Zones are the hub record's `zones[]`: ring, volume and spawn `e` indices. A spawn on a shared edge belongs
to the first zone listed. The distances are walked from the nearest gate's threshold.

| Ring | Zones | Spawns | Walking distance from a gate |
| --- | --- | --- | --- |
| near | near-north, near-east, near-south, near-west (the inner streets beyond the aprons) | 18 | 10-29 m |
| mid | mid-north (forecourt), mid-east, mid-south, mid-west (the ring road and the city's edge) | 23 | 18-45 m |
| far | far-east, far-south, far-west (the wilds) | 34 | 47-82 m |

Aprons:
- No spawn exit lies within 10 m of a gate; the nearest is 10.2 m (`vent-avenue`).
- No zone volume lies within 8 m of a gate.
- Spawn exits stay 3 m or more apart, and 8 m or more from the Ward Zero door, its approach and its returns.

## Hub danger (proposal for the director: no balance in the manifest)

The town's spawn records and zones are layout data. The numbers below are proposals for Astra's hub spawn director,
anchored to the M1 Ward instance: a cap of 8 live enemies, refilled one a second.

- Live cap (hub): 2 + 2 per connected member, at most 16 (solo 4, half the instance's 8). It refills one at a time, every
  3 s (the instance refills every 1 s).
- Density curve (relative spawn weight per zone, by ring):

| Ring | Distance from a gate | Weight | Kinds | Money (x the add's reward) |
| --- | --- | --- | --- | --- |
| apron | under 10 m | 0 (no spawns) | | |
| near | 10-29 m | 0.35 | shambler, runner | 0.4 |
| mid | 18-45 m | 0.7 | shambler, runner, spitter | 0.6 |
| far (wilds) | 47-82 m | 1.0 | shambler, runner, spitter; brute rare (not before 6 min of the loop) | 0.8 |

- No Hulks in the hub (the town's Hulk breaches and drops stay for later events or the final encounter).
- Spawn choice: pick a zone by weight among zones with a spawn whose exit is 20 m or more from every living pilot, is
  not in any pilot's line of sight within 30 m, and is not occupied. Then pick that spawn and use the existing entrance
  animation by kind.
- Despawn (silent: no money, no death effect; the slot frees):
  - more than 60 m walked from every living pilot for 15 s;
  - alive 180 s without aggro;
  - lost its target to the sanctuary and not re-aggroed within 30 s;
  - found inside the sanctuary volume (should never happen).
- Money per kill: round(add reward x ring factor), through the existing NativePveRules.Money chip bonuses.

| Add (reward) | near | mid | far |
| --- | --- | --- | --- |
| shambler (25) | 10 | 15 | 20 |
| runner (20) | 8 | 12 | 16 |
| spitter (30) | | 18 | 24 |
| brute (80) | | | 64 |

  - Headshot +5 (the instance pays +10).
  - Assist: 40% of the hub reward (the existing ratio).
  - Kills pay only when the credited damage was dealt from outside the sanctuary (see anti-camping).
  - Optional soft cap: hub income above $600 per member per loop pays half, so the instances stay the main income.

## Anti-camping: recommendation

Recommended: holster inside, plus the ward and aggro loss (the owner's suggested combination). A fourth rule, no money
for kills from inside, backs it up.

1. **Weapons and abilities holstered in the sanctuary.**
   - While a member's feet are inside the sanctuary volume, attack, weapon abilities and thrown or placed attacks do not
     fire.
   - Movement, interaction (machines, caches, gates, reprints), menus and non-damaging self abilities stay.
   - Holstering starts 0.25 s after entering (so crossing a threshold mid-swing is fair) and ends at once on leaving.
   - Recruits inside are holstered too.
2. **The ward at the fence line.**
   - It blocks projectiles and area effects that cross the fence or a gate's opening, in both directions: spitter acid
     cannot land inside, and a shot or ability from inside cannot leave.
   - Zombies cannot cross it even when a gate stands open: a horde-only blocker in every gate's closure, always on, and
     horde navigation links through a closure disabled.
3. **Zombies lose aggro on members inside.**
   - 1.5 s after its target enters, a zombie drops it and walks back toward its zone, at least 15 m from the fence.
   - A zombie never targets a member inside, so none gather at the fence for step-out farming.
4. **Belt and braces:** a kill pays nothing if its credited damage came from inside (damage over time and recruits
   included).

Why this rather than the alternatives:
- It is legible. The HUD tag says SANCTUARY · WEAPONS HOLSTERED, the ward shimmers at the fence, and zombies turn away.
  A player learns the rule the first time they try to shoot from inside.
- It removes the farming loop at the root: nothing can be killed from inside, and nothing waits at the fence to be
  killed from the doorway.
- It keeps the sanctuary a calm place to shop, reprint and regroup, with no timer and no penalty for waiting for a
  teammate.

Alternatives considered:
- **No money from inside, alone.** Players could still clear threats from total safety and pull zombies to the fence,
  and the rule is invisible until you notice your wallet.
- **Decaying "sanctuary fatigue".** It punishes legitimate waiting (shopping, reviving, a teammate loading), needs a UI
  to explain it and a balance pass, and still allows a short farm before it bites.

## Gates

- **Opening and closing.**
  - The interact key within a gate's `interact` volume, from either side, opens a shut gate and closes an open one.
  - Zombies never open gates.
  - An open gate closes by itself 2.5 s after its `sensor` holds no living member.
  - A gate never closes onto a member standing in its closure; it re-opens or holds.
- **Motion as drawn** (retimed 2026-10-15 to the authority's collider; see "Hub fixes" below).
  - Opening settles by 0.9 s. Wood: the latch lifts in the first 0.18 s, then the leaves swing inward to 160 degrees with a
    little overshoot (past 90 degrees, clear of the opening, at 0.30 s). Metal: the cane bolt lifts in 0.10 s, then the
    leaves roll apart into pockets behind the fence sections with a judder, in them at 0.48 s, and rock on the end stops.
  - Closing settles by 1.0 s. The leaves accelerate shut from wherever they are, meet and slam at 0.67 s, bounce, and the
    latch drops.
  - A reversal mid-swing eases from where the leaves are (the latch stays up).
- **Collider (proposed).**
  - The closure box is solid while the gate is shut. It releases 0.5 s into an opening (the leaves are clear by then) and blocks
    again 0.65 s into a closing, if the closure is clear.
  - While shut, add a pilot exclusion column over the closure (from 3.1 m), so nobody jumps a shut gate.
  - The horde-only ward blocker in each closure is always on.
- **Without a state** (no hub authority: an older build or a manifest without the record), every gate is drawn resting
  **open**. With Astra's authority (merged) every gate starts shut. Without it the openings carry no collider, and a shut
  leaf that pilots and zombies walk through would fake a barrier. This differs from the
  brief's "draw closed"; one constant flips it (`NativeExpeditionHubGates.RestOpen`).
- **Sound** (existing keys):
  - wood: latch `reload-end`, creak `wood` (slowed), slam `wood` with a `stone` thud;
  - metal: bolt `reload-start`, rumble `stone`, slam `hit`, with the same thud;
  - closing: the latch drops on `reload-end`.
- **Dust:** drag puffs along the leaves' foot while they travel, a burst where they stop, and sparks off a metal gate's
  track.

## Presentation (what is drawn)

- **Fence** (kit, static).
  - Wood: palisade logs with stepped points, battens on the inside, a SAFE board, claw marks and a smear outside.
  - Metal: lapped scrap sheets in mismatched colours, I-beam posts, a top rail, razor wire, KEEP OUT.
  - Both: thin sandbag courses along the inner foot, kept clear of the leaves' travel.
  - At each gate:
    - a heavy post either side, inside the section's footprint, with a lamp arm into the plaza (8 lamps, the bulb
      string between them);
    - a header beam at 4.0-4.35 m: SANCTUARY on the street side, and on the plaza side where the gate leads (WARD
      ZERO 75 M, FINAL GATE, IRONWORKS SEALED, STATION SEALED).
- **Gates** (`NativeExpeditionHubGates`, drawn each frame).
  - Wood: two leaves of five logs on battens with a Z-brace, iron straps and a flip latch.
  - Metal: two welded frames of rust, galvanised and painted sheet, a brace, a handle, wheels, a cane bolt and the top
    track.
  - Each gate's dust and sparks.
- **Ward shimmer.** A pale gold curtain on the fence's outer face, a brighter curtain and floor line across every
  opening, and rising motes. Drawn only when the view says the ward is on.
- **HUD.** SANCTUARY · WEAPONS HOLSTERED under the SAFE chip, only when the view says the holster rule is live and I am
  safe.
- **Wilds atmosphere.**
  - Ground fog: every wilds cell carries a sheet, about 1.4x larger and darker, with a second layer higher up (the city
    leaves a quarter of its cells bare).
  - No steady street lamps (88% dead, the rest flickering), no lit or television windows, and dark dirt and ash over the
    asphalt.
  - The few lights mark the way: the Meridian forecourt's cold floodlights, sodium floods in the yard and station,
    fires and the pylons' red lights.
- **Distance markers.**
  - Painted chevrons every 15 m along the Ward Zero route, and the walked metres left every 30 m (WARD ZERO 55M).
  - Two wall signs per route where it leaves the city (WARD ZERO 45 M >; IRONWORKS SEALED; STATION SEALED).
  - SANCTUARY arrows back the other way in the wilds.
- **DEV preview** (DEV panel · Camera · "Hub gates preview").
  - On the town map, every gate opens and closes on its own 8 s cycle, staggered.
  - The ward shimmers, and the holster tag shows while I stand inside.
  - Its sounds play, labelled "DEV PREVIEW · ... · NOT GAMEPLAY".
  - It changes nothing in the simulation.

### Live today vs waiting on Astra

| Live on this branch (no director change) | Waiting on Astra |
| --- | --- |
| the bigger town, its wilds, cover and 84 spawn records | the hub spawn director (zones, cap, despawn), money |
| fence collision and exclusion columns, gate openings passable | gate interaction, state, colliders, auto-close |
| Ward Zero door at x 84 (M1 enters and returns there) | the ward (projectiles, horde blocker, nav links) |
| gate models resting open, fence dressing, headers, lamps | the holster rule, aggro loss, no-money-from-inside |
| wilds fog, darkness, markers, signs | the holster tag and ward shimmer in play (they bind to the rule) |
| DEV preview of gates, ward, holster tag and sounds | a replicated GateState in the view |

M1 runs the town as an intermission and spawns no enemies there, so in M1 today the wilds are empty and the run to the
door is safe. That is true until the hub spawn director exists.

## Collision, movement and navigation changes (all in `expedition-city`)

- **Removed:** the eight 1.1 m sanctuary barricades.
- **Added:**
  - eight 3.2 m fence sections, with their exclusion columns;
  - 14 building masses and 4 rooftop pieces closing the wilds;
  - 58 wilds cover blocks (39 dressed `wild-*` pieces, plus 19 vehicles, barriers and crates);
  - three breach panels.

  That makes 87 blocks added and 8 removed, for 203.
- **Resized:** five ground slabs, to the new extents.
- **Moved:**
  - the Meridian facility, tower, portico and ambulance (+40 m in x);
  - the foundry hall, stack and shutter (-40 m in x);
  - the transit hall, boards, canopy, pylons and roof vents (-40 m in z);
  - the cars and truck in the avenue and the east and west streets, to clear 3-6 m aprons outside the gates; the bus's
    fire light and the ambience crackle moved with it to (22.6, -3.9).
- **Spawns:**
  - moved: `door-east-n`, `vent-avenue`, `drop-west` and `drop-south` (gate aprons), `door-south-e` (its doorway on the
    building), and `drop-hulk-station` (to the new station);
  - added: 34 in the wilds.
- **Navigation:** regenerated by `tools/generate_expedition.py` (deterministic `--check`).
- **Unchanged:**
  - the Ward Zero instance (its draft and blockHash);
  - Siren;
  - every other map;
  - hit timing, hitboxes, damage, cooldowns, movement rules, networking and saves.
- **Layout revision:** exp-layout-2 becomes exp-layout-3. The compatibility fingerprint changes with the manifest and
  UnityMaps, so the room/layout identity changes as it does for any map change. A durable Expedition save that pins the
  old fingerprint would not resume on the new town: the director's own refusal.

## Checks

Generator (`expedition_city.py`), its PNG plan and `test_expedition_layout_draft.py`:
- fence closed except at gates;
- every crossing of the sanctuary edge inside a gate opening;
- each route through its gate only, and at least 70 m long;
- zones outside the fence and 8 m or more from the gates;
- spawn exits 10 m or more from the gates;
- every non-final spawn in exactly one zone;
- rings ordered by distance;
- all the earlier floor, clearance and route proofs.

Managed:
- `CoreTests --expedition-hub` (`Tests/ExpeditionHubChecks.cs`) repeats the fence, route, zone and apron checks on the
  imported map with its own capsule grid;
- the gate models stay inside their closures when shut, and through their whole travel (to 108%) never meet a block, a
  slot, an interaction or an anchor;
- the motion's ends and reversal, the rest pose, the DEV preview, the cue schedule, the dressing, no per-frame
  allocation, and the hooks;
- `--expedition-layout` (Astra's strict parser and ValidateGeometry with the imported navigation),
  `--expedition-dressing`, `--expedition-acceptance` and `--expedition-states`.

Also: an offline emulation of the M1 runtime startup gate (scratch probe, PhysX-like) found 0 failures on both areas.

## Astra contract (proposal)

1. **Layout reads (additive; the strict loader ignores them).** `areas[exp-city].hub`:
   - `gates[]` (id, kind, motion, side, x/y/z, yaw, width, height, `closure`, `interact`, `sensor`, `inside`, `outside`,
     `fence`);
   - `fence.segments[]` (block, kind, side, gate) and `fence.height` / `thickness`;
   - `zones[]` (id, ring, volume, spawns as `e` indices, gateDistance);
   - `wilds[]`;
   - `routes[]` and `reserved[]` (gate, walked path, run and sprint seconds);
   - `apron` (spawn 10 m, zone 8 m).

   The town's `spawns` keep the PveEntry shape plus `allow`. The final gate stays the only `gates[]` record.
2. **Replicated gate state.** One per hub gate in the public view: `{id, open: bool, changedAt: run seconds}` (the
   closures' clock). Changes only on transitions.
   - Bind: `NativeExpeditionHubLive.Authority` with `AuthorityClock`; the presentation and sound do the rest.
   - A float `open 0..1` is not needed: motion is eased from `changedAt`.
3. **Sanctuary rules in the view:** `holster: bool`, `ward: bool` (on for the run) on the same object.
   `members[].safe` already exists and drives the holster tag.
4. **Gate authority.**
   - Interact opens or closes.
   - Auto-close 2.5 s after the sensor is empty; never closes onto a member.
   - Collider: the closure box, solid while shut, released 0.5 s into an opening, blocking 0.65 s into a closing; a
     pilot exclusion column over a shut closure.
   - The horde-only ward blocker in every closure, always on; horde nav links through closures disabled.
   - Zombies never open gates.
   - The startup geometry gate keeps running with gates open, as today, because spawns must still route to slot 0.
5. **Sanctuary rules.**
   - Holster inside (0.25 s in, instant out).
   - The ward blocks projectiles and area effects across the fence line both ways.
   - Aggro dropped 1.5 s after a target enters; walk back 15 m or more.
   - No kill money for damage credited from inside.
   - A horde actor found inside despawns.
6. **Hub spawn director.** The cap, cadence, ring weights, kinds, line-of-sight and distance rules, despawn rules and
   money in "Hub danger". It uses the zones and the town spawn records. It never uses the nine final-plaza spawns before
   FinalUnlocked, and no Hulks.

## Open questions for the owner

- Are the scale and run right? About 11 s running to Ward Zero, 192 x 156 m.
- Should gates rest open or shut before the gate authority exists? Open is what the code does now (see above).
- Should hub money be soft-capped per loop, and should the hub ever spawn Hulks (for example a late-loop event)?
- Are the district names (Meridian Approach, Ironworks Yard, Station Road) and the palisade on the north and west mouths
  right?


## Authority implementation — 2026-10-15

Owner-approved rules are implemented on `codex/expedition-hub-authority`, based on
Claude's hub `3c13863` (includes shared `f29d7c9`). This section supersedes the
"waiting on Astra" status above for that branch; native release remains gated.

- Four gates start shut, accept F from either side, safely reverse a non-solid closing when a player capsule
  overlaps, and auto-close after 2.5 seconds with a clear sensor. Collision releases
  .5 seconds into opening and returns .65 seconds into closing. Closed gates also
  have pilot anti-vault columns. Occupancy uses enabled member capsules that actually overlap the doorway in 3D.
- Horde-only layer 28 barriers remain solid at every gate regardless of its state.
  Horde navigation rejects crossing links and direct shortcuts; vent travel is
  disabled in the town. Intruding horde actors are removed without rewards.
- Weapons/ability input is cleared and pending attacks cancelled .25 seconds after
  entry; movement and interactions remain. Leaving clears the timer immediately.
  The ward is immediate and rejects cross-boundary projectiles, acid arcs, melee
  projectiles, blasts, fields, and damage application, including outside-to-outside
  paths through sanctuary. Remote placed standard abilities cannot be placed across it.
- Existing targets retain at most 1.5 seconds of aggro inside; no new inside targets
  are selected. Untargeted hub mobs return toward their entrance instead of piling
  against the fence. Inside-origin damage is rejected before payout. Accepted
  outside-origin DoTs can still pay after the shooter retreats; recruits use the
  same damage boundary. No optional $600 soft cap was added.
- Population is `min(16, 2 + 2 * living connected unextracted members)` (zero if none).
  One spawn attempt every 3 seconds, with no catch-up burst; roster/weights follow
  the authored rings and never include Hulks. Existing entrance clearance and
  queue checks apply, with 20 m pilot exclusion and 30 m visible-spawn exclusion.
  Far brutes are rare after six run minutes. Final-plaza spawns are not used.
- The spawn ring fixes its lifetime base reward multiplier (.4/.6/.8), rounded
  before existing chip bonuses. Headshot bonus $5; existing recruit/assist ratios.
  Excess population after deaths/disconnects is culled silently. Far timeout is
  15 seconds beyond **60 m Euclidean distance** from everyone (a conservative
  lower bound on walked distance); idle timeout 180 seconds, lost-target timeout
  30 seconds. Final boss arrival clears hub mobs and restores the boss add cap.
- `ExpeditionView.hub` carries detached serializable `ExpeditionHubView` (Gates,
  Count, Holster, Ward). Existing public/private transport carries it at 20 Hz,
  including source-only changes at the same run revision. ConversionGame binds it
  and `elapsed` to `NativeExpeditionHubLive`. No private rewards enter this field.

Verification: engine-free gate/ward/cap/reward/wire checks plus the existing hub
layout/visual checks; native Unity acceptance now also exercises gate colliders,
occupancy, holster timing, aggro grace, spawning, damage and outside DoTs. The latter
requires the desktop builder/Unity and is not established by a semantic compile.
Full multiplayer Expedition launch remains the existing separate integration task;
this change supplies the public hub state contract, not a new multiplayer launcher.

## Town zombie roaming and perception (owner, 2026-10-15)

Authority implementation: `Core/NativeHubPerception.cs` and
`Runtime/NativeHubPerceptionRuntime.cs`, only for the hub population tracked by
`hubMobs`. Instance/Boss Test hordes and DEV monsters retain their existing AI.

- **Wander / idle:** choose authored navigation points in the zombie's spawn
  zone, normally 3–14 m away. Shamble at `min(1.1 m/s, stats.walk * .55)`, with
  existing crawl/limp, slow and stagger modifiers. Idle for a host-random 2–6 s
  on arrival; a blocked patrol leg times out after 15 s. After chasing outside
  its zone, return toward its spawn before resuming the zone patrol. Horde gate
  links stay excluded; movement still checks support and the sanctuary boundary.
- **Vision:** 18 m, horizontal 110° forward cone; within 3 m, notice in any
  direction. Both require scene line of sight. Range uses three-dimensional
  distance. Sanctuary occupancy or a segment across its fence rejects sight.
- **Hearing:** actual grounded displacement generates steps, at most every .6 s
  walking / .3 s sprinting; a grounded transition generates landing noise.
  Accepted attacks/casts/shots/explosions and successful manual gate openings
  generate their own noises. Merely pressing a blocked action does not count.
  Noise can pass ordinary scenery, but never the sanctuary fence; source and
  listener paths are checked, including two exterior points across the sanctuary.

| Noise | Radius |
| --- | ---: |
| Walking | 5 m |
| Sprinting | 10 m |
| Landing | 12 m |
| Fighting / melee attack | 14 m |
| Firing (including bow) | 28 m |
| Explosion | 36 m |
| Casting | 22 m |
| Opening a gate from outside | 16 m |

- **Alert / chase:** a sight or noise sample starts a .4–.8 s stationary turn
  toward that sample, then chase. Repeated observations do not restart the
  reaction. Existing attacks cannot run during the reaction. Optional screams
  and zombie-to-zombie alert propagation are not enabled.
- **Memory / search:** only sight/noise updates the known position. Without a
  fresh observation for 3 s, investigate that position (up to 10 s), search in
  place for up to 4 s on arrival, then wander again. Memory pursuit and search
  cannot attack using the hidden target's live position. Reacquisition from
  search starts a new alert. Existing sanctuary aggro grace remains 1.5 s;
  entering safety does not expose further positions. Existing far (60 m / 15 s),
  idle (180 s) and lost-aggro (30 s) despawn rules remain active.
- **Cost:** senses run every .2 s per zombie, staggered initially; vision rejects
  range/cone/fence before querying the scene. The host keeps at most 64 noises,
  expires them after .75 s, and each zombie consumes each event once. Each
  zombie owns its random stream, independent of spawn/economy randomness.

### Shared reaction state / Claude handoff

`NativeMinionFrame` adds three scalar fields, copied into `NativePveAdd.Hub` on
host and guest. They also survive `NativeFrameCodec` replay/late-join capture:

| Field | Meaning |
| --- | --- |
| `hubState` | `HubZombieState`: 0 none/non-hub, 1 wander, 2 idle, 3 alert, 4 chase, 5 search |
| `hubAlertAt` | Start of latest alert in **arena simulation seconds**, -100 before first alert |
| `hubLookYaw` | Desired horizontal look direction in degrees, +Z = 0; `V3.Forward` gives the vector |

`NativePveAdd.Hub` exposes `State`, `AlertAt`, `LookYaw`; it is null for non-hub
adds. A default/older frame's state 0 clears the view. The receiver validates
state range and finite time/yaw. No client runs perception, randomness or timers.
Body position/velocity/yaw already replicate, so the real idle, shamble and turn
are visible without a presentation rewrite. Claude may layer a bespoke reaction
using this state; there is no new scream audio or animation asset in this change.
Attack timing/damage, spawn caps/cadence, ring payouts and gate rules are unchanged.

Acceptance: managed checks cover cone/LOS inputs, every noise radius, fence
segments, seeded reaction/idle/memory/search traces and public-state roundtrips.
Native hub acceptance adds real scene LOS, heard rear-target reaction,
JsonUtility and guest/late-join reconstruction. Native execution and Windows
visual/two-client play must be verified by the builder/playtest; semantic compile
alone is not that verification.


### Presentation of the roaming (Claude, 2026-10-15)

Branch `claude/hub-zombie-wander` (presentation only; reads the reaction state above, writes nothing). The owner's
request is met by Astra's behaviour; this layer makes each state read on the body, the same on host, guests and the
killcam.

- **One mapping.** `NativeHordeRoam.FromHub` (`Core/NativeHordeRoam.cs`) maps `NativePveAdd.Hub` one for one to the
  presentation's states and reads `hubAlertAt` and `hubLookYaw`. `NativeHordeRoamRecord.Read`
  (`Runtime/NativeHordeRoamPresentation.cs`) is the only place the presentation reads the record; the pose and the
  voices both go through it.
- **Wander.** The stride's walk part blends to the pack's slow forward walk (`walk-4`; the only take that plants cleanly
  at the host's 0.4-1.1 m/s pace for every hub kind; walk-6 and the chases drag a planted ankle 5-7 cm there). The trunk
  lolls with the stride, the head glances about every few seconds, and an occasional stumble dips the trunk.
- **Idle.** Five variants per add (sway, twitch, look around, slump, rock) on a schedule hashed from the add's seed
  against the clock: 4.5-7.5 s slots, all five in every five slots, never one twice in a row, 0.8 s crossfades. A
  crowd is out of step, and every peer draws the same variant at the same moment.
- **Alert** (from `hubAlertAt`, so a late joiner draws the same moment). The head snaps to `hubLookYaw` in 0.14 s,
  with a small overshoot and an aim correction onto it. The chest follows while the host turns the body. A scream
  starts at +0.2 s and lasts 0.95 s: head back, a tremble, the arms flung on a reel's start, and hinged jaws open.
  The burst throws the trunk forward from the chase's first frame.
- **Chase.** Today's chase look. The head and alert stance follow the nearest pilot within 14 m, but only when that
  pilot lies within 75° of `hubLookYaw`. Otherwise they follow the look direction.
- **Search.** A wide head scan and sniffing (head up, quick nods, chest lifting) over the host's slow turn in place.
- **Blends.** States blend over 0.5 s from the frame each peer first sees the change, since the start is not
  replicated.
- **No record** (instance hordes, DEV monsters, the town's final encounter, an add still entering). The inference
  from motion only tells standing apart from hunting. Such an add shows the idle variants after standing 1.5 s with
  no pilot within 14 m and no attack for 3 s. Slowed by a limp or a frost field, it still reads as a chase.
  Otherwise it looks as before.
- **Head look-at fix (every horde add).** The gait's look-at was mirrored: heads turned away from the pilot and looked
  down at one above. It now turns toward the pilot and looks up at one above.
- **Voices** (the roaming population only; bank-only, silent without LocalAssets).
  - Sparse, low moans while wandering, idling or searching: within 24 m, 7-13 s slots per add with three slots in
    ten silent, at most one a 0.9 s across the horde, attenuated with distance.
  - A shriek with the scream.
  - New bank keys `pve.zombie.moan` / `pve.zombie.shriek`. Until the owner supplies clips, the stand-ins are
    `pve.add.death` pitched down and `siren.rush`.
- **Unchanged.** Hit timing, hitboxes (Balance and KeepHands still hold the trunk and claws to the capsule), damage,
  cooldowns, movement and the network frame. The scream is cosmetic: it alerts no other zombie.

**Gaps for Astra (optional; visuals only, nothing blocks):**

- `hubStateAt` (the current state's start, arena seconds). Blends would then start on the host's frame on every peer.
  A late joiner within 0.5 s of a change would match the host's blend instead of starting its own.
- `hubTarget` (the hunted seat, 0 none). The alert stance and the chase's head would then track the actual hunted pilot
  instead of the nearest within 14 m near the look direction.

Checks: `Tests/HordeRoamChecks.cs` (`CoreTests --horde-roam`). Offline renders are in the scratchpad (DEV_LOG).

## Hub fixes — 2026-10-15 (Claude)

Branch `claude/expedition-hub-fixes` (base shared `425cb2d`, merged with shared `ee46074`). Owner report (verbatim): "Also
on the hub~ it seems like there is Z-fighting on the gates? It also allows us to run through it before it is even open.
Lets also remove the circle outlines like the one on the exit door, the caches, etc."

### Z-fighting on the gates (presentation; fixed)

No collider-only block is drawn in the town: the gate closures, the no-vault columns and the horde ward are Astra's
runtime colliders without renderers, and the fence's exclusion columns are `pilotBlocks`, never drawn. The fights were
drawn faces sharing a plane (found with `NativeSurfaceLayers.Solve` at zero lift; now held to none by
`CoreTests --expedition-hub`):

| What | Shared planes before | Fix |
| --- | --- | --- |
| The sanctuary edge's lit curtain (`NativeExpeditionStateScene.Sanctuary`) stood on the volume's edge: the plaza's building faces, the fence's inner face and the shut wooden leaves' inner faces all lie in that plane | 232 of its 332 quads, all four gates' logs among them | the curtain stands `CurtainInset` 8 cm inside the edge (5 cm still met shopfront trim, 5.7 cm the metal leaves' stiles; 8 cm meets nothing) |
| The metal leaves' rails ran over their stiles with flush front and back faces | 24 per metal gate, at every pose | the rails run between the stiles, 7 mm inside their faces |
| The wooden latch's pivot plate sat 2 mm behind its bar | 1 per wooden gate while the latch is up | 7 mm |
| The ward's fence shimmer stood 1 mm off the outer dressing (claw marks, smears, the metal top rail) | 23 quads (back faces) | `WardOffset` 3.5 cm, one helper for the runtime and the checks (`NativeExpeditionHubGates.WardSegments`) |

### The leaves in step with the collider (presentation; retimed)

The authority's collider (`ExpeditionHubAuthority.Blocking`) is released 0.5 s into an opening and solid again 0.65 s
into a closing. Before, a metal gate was 35% open at the release (its leaves still covered 1.3 m of each side of the
opening until 0.74 s), and every closing was still 55% open when the collider turned solid (the slam was at 0.85 s).

| Moment (s into the state) | Collider | Wood (drawn) | Metal (drawn) |
| --- | --- | --- | --- |
| opening 0 | solid | latch lifts to 0.18 | bolt lifts to 0.10 |
| | | past 90 degrees (clear) at 0.30, fully open at 0.49 | in the pockets at 0.48 (`ClearAt`), bang, rock on the stops |
| opening 0.5 | released | | |
| closing 0 | released | leaves accelerate shut from where they are | same |
| closing 0.65 | solid | | |
| closing 0.67 (`MeetAt`) | | meet, slam, bounce; the latch drops by 0.83 | same |

The cues follow (rumble when the bolt lifts, the metal stop at 0.48, the slam at 0.67). The presentation also carries
the authority clock between the 20 Hz view updates (`ExpeditionHubClock`, at most 0.1 s ahead), so the leaves move
every frame instead of stepping with the projection. Checked against the real authority every 1/240 s: never drawn shut
while walkable, clear whenever released, met whenever solid, still visibly closing 0.15 s before it blocks.

### Run-through report (for Astra; authority, not changed here)

What fails: walking into a shut gate, without pressing F, opens it and drops its collider in the same tick. The pilot
crosses the fence line 0.117 s later at run speed while the gate is (correctly) drawn shut with its latch lifting.

- `NativeArena.HubOccupied` (`Runtime/NativeExpeditionHubAuthority.cs:51-58`, the test at line 56) counts a closure as
  occupied when a capsule's centre is within its radius + 0.15 m of the closure box in plan, at any height. A pilot pressed
  against the solid closure stands its radius + PhysX's 0.01 m contact offset from it, so touching a shut gate, or coming
  within 0.15 m of it, "occupies" it. So does a pilot leaning on the fence within 0.57 m of the opening's edge.
- `ExpeditionHubAuthority.Step` (`Core/NativeExpeditionHubRules.cs:28`) then re-opens any shut gate whose closure is
  occupied, however long it has been shut and solid.
- `StepHubGates` (`Runtime/NativeExpeditionHubAuthority.cs:62`, `solid=Blocking(i,now)&&!HubOccupied(...)`) turns the
  collider and the no-vault column off at once while occupied, instead of 0.5 s into the opening.

Not ours: the presentation reads the right state on the right clock and ids (the view's `hub` and `elapsed`, bound at
`Runtime/NativeExpeditionNetwork.cs:14`), the gates start shut, and the closures span their openings exactly (the fence
flood check passes with every closure solid).

Reproduction: `CoreTests --expedition-hub` prints `PENDING (Astra) Expedition hub: a shut north gate opens itself ...`
(`ExpeditionHubChecks.PendingAuthority`: Core's authority and the runtime's occupancy rule replayed engine-free; a pilot
at 6.8 m/s, no interact). It turns into a PASS line once a shut gate stops the pilot. In the game: walk into any shut hub
gate.

Suggested fix:
1. For "never close onto a member", test a real overlap of the capsule with the closure (radius less a skin, or an
   overlap query against the closure box while its collider is off), not radius + 0.15 m.
2. Re-open on occupancy only while a closing has not blocked yet (`!Open && now-ChangedAt < Block`); a gate that is
   already solid stays shut. (`Tests/ExpeditionHubAuthorityChecks.cs:21` asserts a re-open 0.7 s into a closing, after
   it blocks; it would move under 0.65 s.)
3. In `StepHubGates`, let occupancy hold the collider off only in that same window.

Reversals (same source, `Blocking` at `Core/NativeExpeditionHubRules.cs:35` is a pure function of the newest state's
age): pressing F again during an opening's first 0.5 s releases the collider at once while the leaves are still nearly
shut (walkable while drawn shut, up to 0.65 s); pressing it during a closing's first 0.65 s makes the collider solid for
0.5 s while the leaves are nearly open (an invisible wall). Suggest keeping the collider's state across a reversal: a
close during an opening that has not released stays solid; a re-open during a closing that has not blocked stays
released. The presentation reverses from where the leaves are and clears or meets at the same 0.5 and 0.65 s marks, so
it follows either rule.


### Run-through authority fix (Astra, 2026-10-15)

Implemented on `codex/hub-gate-collision`, based on shared `72aa1858`. The
report above records the pre-fix behavior; the PENDING reproduction was observed
before this change and is now a hard assertion in `CoreTests --expedition-hub`.
It checks all four gates, both the open flag and the collider, and the pilot
remaining outside the closure.

- Occupancy uses actual upright capsule-versus-yaw-box distance in three
  dimensions, with a 1 cm inward skin. Capsule center and world-scaled height /
  radius come from the real collider. Tangency, the physics contact offset,
  expanded rectangular corners and a pilot above the opening are not occupancy.
- Gate authority retains collider solidity. Occupancy can reverse a closing
  only while that collider is still released; a shut/solid gate cannot auto-open
  even if penetration is reported. On the threshold tick, overlap is checked
  before enabling the closing collider, so it cannot close onto a member.
- `StepHubGates` applies the authority collider state directly to both the door
  and no-vault column. There is no independent occupancy override.
- Reversing opening-to-closing before release preserves solidity. Reversing
  closing-to-opening before blocking preserves the released state. Only the
  existing .5 s opening-release / .65 s closing-block markers change solidity.
  Explicit F interaction from either side and 2.5 s clear auto-close remain.

The gate pose/wire fields, animation, sanctuary ward, movement speed, combat,
rewards and zombie perception are unchanged. Claude's separate presentation
changes remain on their own branch. Native acceptance now checks real capsule
contact/penetration/height and both reversal collider states; these require the
Windows builder/PhysX run, not just semantic compilation.

### No floor rings round the Expedition's interactables (presentation; removed)

Removed:
- `Core/NativeExpeditionWorld.cs` `Emit`: the floor ring (radius r-0.05..r) and the actionable spinning outer ring
  (r+0.04..r+0.12) round every world piece: the Ward Zero door and the instance exit (doors, 1.35 m), the reprint machine
  (0.95 m), the chip caches (0.75 m) and reward drops (0.42 m). The light column while actionable, the glyph (diamond,
  tile, pedestal, crate), the label, the HUD door hologram and the interact prompt stay.
- `Runtime/NativeExpeditionScreen.cs`: the same ring in the pieces' greybox preview.
- `Runtime/NativeBeaconPresentation.cs`: the revive beacon's floor ring (with its self-revive arc) on Expedition maps
  (`NativeExpeditionWorld.ExpeditionMap`, ids starting `expedition-`). Wave defense and the Boss Test keep it: there it
  is the self-revive timer, not a generic interact ring. The column and the F · REVIVE / SELF-REVIVE label with its clock
  stay everywhere.

Kept (events, not outlines round an interactable): the final gate's opening cue rings, the sanctuary crossing ripple,
the boss arrival's roar rings, the revive repair's floor ring, combat telegraphs, and the sanctuary edge's square floor
glow. The gates never had rings.

### Impact

Presentation only: no collider, layout, navigation, timing rule, hit timing, hitbox, damage, cooldown, movement or
network change. The layout and its generator are unchanged (the closures span their openings).


## Join circle at the instance doors (owner request 2026-10-15) — contract for Astra

Owner (verbatim): "lets move the 'Join instance' circle a little bit away from the door. We can make the circle bigger too, players
can join by standing inside the circle." Also asked: "can this work if everyone goes into their own instance? Say one player wants to
run around the hub, the other wants to go inside ward zero. Could this still run smooth for both of them?"

**Today** (research at `8355b44`): there is no join circle. The door's floor ring was removed with the other interactable rings
(`03eb9f4`); a cyan light column at the door anchor remains. Entry is the host's F inside `doors[].interact` (3 × 3 × 2 m box,
1 m out from the doorway) with line of sight to `approach` (`Core/NativeExpeditionDirector.cs:39-43`,
`Core/NativeExpeditionDirectorState.cs:110-115`, `Runtime/NativeExpeditionM1.cs:77-82`). Then every living member is placed on the
instance `entries`. The run has one `area`, and the host simulates one arena, so a split party (hub and Ward Zero at once) is not
supported. It contradicts `EXPEDITION.md` ("No split indoor/outdoor simulation") and would need per-member areas, concurrent arenas,
per-peer frames and per-area barriers: a large redesign.

**Owner decision (2026-10-15):** "Option 2 , but ideally were gonna let players individually enter instances", then: "It doesn't
have to pull the whole party in, just whoever is wanting to run the instance". So the circle is a personal entry: whoever stands in
it when the countdown ends goes in, and everyone else stays in the hub. That needs split-area parties (below), which Astra scopes
first.

**Rule (Astra; owner-chosen 2026-10-15):**
1. Each instance door gets a **join circle**: a vertical cylinder `join {x, y, z, r, h}`. For Ward Zero: centre 5 m out from the
   doorway on the approach line (x 79, z 0), r 2.5 m, h 3 m, between the portico pylons (z ±3.8).
2. When a living, connected, unextracted member steps inside (capsule centre in the cylinder), a short **deploy countdown** starts
   (proposed 3 s; tunable). Others may step in and join it before zero; anyone who steps out leaves it. It cancels if nobody is
   left inside. At zero, **only the members inside** enter the instance (`EnterDoor` path, barrier, ready, placement for those
   members). The rest stay in the hub. The F door entry is retired; F stays for caches, machines, rewards and gates. Guests count
   through their pilot on the host.
3. Public view per door: `joinInside` (count), `joinStartedAt` (authority seconds, −1 none), and per member `atJoin` (bool) and
   `area` (where each member is).
4. Town phase rules (quota, closures, exit vote) need restating per area; see below.
5. **Interim, only if the circle ships before split areas:** proposed to start the countdown only when every living member is
   inside, so nobody is pulled in who did not step in. Owner can overrule.

**Split-area parties (owner target; Astra to scope first).** Members may be in the hub and in an instance at the same time. Today
the run has one `area` and the host simulates one arena (`EXPEDITION.md`: "No split indoor/outdoor simulation"). Needed:
per-member areas; the host simulating the hub plus each occupied instance (physics, AI, hazards); frames and events scoped to each
peer's area; per-area load barriers; and rules across areas for death, revive, reprint, loot, quota, extraction, saves and host
migration. Astra to say what changes in the wire, director and save formats, and the host cost of two live areas.

**Claude's part:**
- Layout: `docs/arenas/scripts/expedition_city.py` emits `join` and moves `returns` out of the circle. The approach goes to the
  circle centre. The revision bumps to exp-layout-4 (saves pinned to exp-layout-3 will not resume). This needs Astra's parser to
  accept `join` first.
- Presentation: a ground ring at the circle (an owner-requested exception to the no-rings rule; `ExpeditionPresentationChecks` is
  updated) that lights when someone stands in it, a countdown sweep, and a label: STAND IN THE CIRCLE TO DEPLOY, or DEPLOYING TO
  WARD ZERO 3…1 with the names inside. Members in the hub see a small note when a teammate deploys, and the party list shows
  who is in which area.

**Layout draft exp-layout-4 (Claude, 2026-10-16, branch `claude/join-circle-layout-draft`): NOT LIVE.**

Where it lives:
- `python3 docs/arenas/scripts/expedition_layout.py --draft <file>` (or `--fixtures`) writes it to
  `unity/ChipArenaConversion/Tests/expedition-layout-4-join-draft.json`.
- Nothing under `Assets/` names that file. The game still loads `Resources/ExpeditionLayout.json` (exp-layout-3, byte-identical;
  its SHA-256 is pinned in `Tests/ExpeditionJoinDraftChecks.cs`).
- There is no ring, countdown or HUD.

It differs from exp-layout-3 only in four things:
- `revision`: exp-layout-4.
- The Ward Zero door's `join`: `{x 79, y 0, z 0, r 2.5, h 4}`.
  - The base is the floor. The centre is 5 m out from the doorway on the approach line.
  - The edge is 1.62 m from the portico pylons, 0.5 m short of the F volume and 1.5 m from the rubble heap.
  - h is 4 m, not the proposed 3 m: a ground plus air jump's capsule centre peaks at 3.69 m. The canopy's underside is at 4.4 m,
    so nobody standing on the canopy counts.
- `approach`: the circle's centre (it was 82.4, 0, 0).
- `returns`, in this order: (79.4, ±4.2), (81.1, ±6.0), (82.7, ±6.0), (81.9, ±7.6).
  - exp-layout-3 had 4 of its 8 returns inside the circle.
  - Each return is 1.72 m or more beyond the edge, 8.2 m or more from any spawn exit, and Hulk-clear.
  - The shortest walk from each return to the east gate never needs the circle.

The anchor, the F volume, the closure and the hub record are unchanged, so F entry stays live. The walk from the east gate is
69.7 m to the centre and 67.1 m to the edge. The 70 m target was met by the door (72.9 m).

Checks: the generator's join report, `CoreTests --expedition-layout` and `test_expedition_layout_draft.py`.

To switch it live: first Astra's subset transfer and `joins[]` / `atJoin` / `area` must exist. Then make the draft's records the
default, bump `REVISION` / `PINNED` and the pin, regenerate and import (`tools/generate_expedition.py`). Keep the town dressing
off the circle, and add the ring.

### Join circle presentation (Claude, 2026-10-17, branch `claude/join-circles`)

Replaces Astra's stand-in ring and label (PR #255) on the live exp-layout-5 circles. Presentation only: it reads the public
view (`joins[]`: doorId, area, x/y/z/r/h, joinInside, joinStartedAt, duration, deployedAt/deployedAccounts, closedReason;
`members[].area/pendingArea/atJoin/seat`) and where each member's pilot stands. Who deploys is the authority's `atJoin`,
`joinInside` and deploy record; no collider, spawn, ring, route or timing moves. Bound to Astra's exact join fields (PR #262)
on `claude/join-circles-bind`; see the table below.

- **The pad** (`Core/NativeExpeditionJoinScene.cs`, drawn through the state pieces' NativeBlockLit and NativeSirenFx path):
  - Six waist-high hardlight emitter posts stand 0.3 m outside the radius, off the door and approach lines.
  - A low emitter plate sits at the centre, with a soft energy pool round it (a filled glow, not an outline).
  - A low light curtain stands exactly on the counted radius.
  - A hologram faces the camera with the zone's name and threat. A thin sky beacon rises over the rooftops.
  - The accent is per zone: Ward Zero red, Central Station teal, City Ironworks amber (`NativeExpeditionJoinZones`, keyed on
    the layout's area ids).
- **The states:**
  - Charging: the curtain rises to the counted height `h` and whitens over `duration`, and the posts' bars fill. The
    hologram shows the seconds, 12 segments and a pip per pilot counted.
  - Each counted pilot stands in a lock column under a chevron. A pilot outside has neither.
  - Deploy (a new deploy record): the curtain flashes over the counted column, a beam takes each pilot in
    `deployedAccounts`, and a shock ring runs out. Pilots still held in the hub while their transfer loads keep a hold column.
  - Cancelled (the count stopped with no new record and the circle empty, including a step-out at the deadline): the
    curtain drops and flickers out.
  - Held (still occupied after expiry with no new record: the authority refused): an amber flicker over the restarted count.
  - Closed (`closedReason`; only Open counts, so no curtain, countdown, beacon or lock columns): Cleared, green posts and
    CLEARED; Locked, red-amber posts and a padlock over LOCKED; Reserved, amber posts and ROUTE RESERVED in a stamp frame.
  - A return to the hub: a beam-in where the pilot appears.
- **The HUD** (`Runtime/NativeExpeditionJoinHud.cs`):
  - A party list on the left. Each member's place is HUB / WARD ZERO / CENTRAL STATION / CITY IRONWORKS (where they
    physically are), with their state (deploying with seconds, LOADING · the zone while their transfer loads, back in the
    hub, down, offline, extracted). Mine is marked YOU.
  - In a circle and counted: DEPLOYING TO, the zone, the seconds, the segments, who goes, and "step out to stay".
  - In it but not counted: the reason (down, picking a chip, carrying, loading, waiting for the host). At a closed one:
    CLEARED, LOCKED or ROUTE RESERVED with NO DEPLOY. Near one: a prompt.
  - Notes (who deployed where, who came back), and my flash over the last 0.35 s, so the loading screen's cut lands on light.
- **Sound keys** (the owner generates them; until then the fallback clip plays):

  | Key | Fallback clip |
  | --- | --- |
  | `expedition.join.charge` | shield |
  | `expedition.join.tick` | countdown |
  | `expedition.join.cancel` | reload-start |
  | `expedition.join.deploy` | lightning |
  | `expedition.join.deploy.whoosh` | dash |
  | `expedition.join.held` | ui |
  | `expedition.join.arrive` | dash |

- **Hub entrances** (`Core/NativeExpeditionCityKit.Entrances.cs`), keyed on the live layout's doors and the hub record's routes:
  - The signs name the zones. They read SEALED only while a route is reserved.
  - The station's boarded doorway gives way to the airlock (state pieces) framed in cream tile and teal light.
  - The foundry's welded shutter is shown rolled into its drum over the airlock, with NO 1 GATE and amber beacons.
  - CITY IRONWORKS and the canopy's TRANSIT are lit. A light in the zone's accent sits at each circle.
- **Low graphics:** half the curtain, no motes or beacon glow, plainer posts.
- **Reduced effects:** no shimmer, flicker or bob. The countdown still fills.
- **Fallback:** without the pads (shader missing), the stand-in ring and label stay.

**What each join field drives** (`claude/join-circles-bind`, 2026-10-17):

| Field | Presentation |
| --- | --- |
| `joins[].deployedAt` | The deploy trigger: a changed record (per doorId) plays the deploy (beam, flash, shock ring, cue, note). The first record seen is a baseline: a late joiner, a pilot back from a zone or a cancelled load, and a record already 1.6 s old on the view's clock (a menu) replay nothing. A reset (-1) plays nothing. An empty circle at the deadline is a cancel, never a deploy. |
| `joins[].deployedAccounts` | Who beamed out: the beams, the note's names (mine left out), my flash only if I am in it, and the deployed party rows. |
| `joins[].h` | The curtain rises to `h` and flashes over it; the counted column for "in the circle" (capsule centre, feet + 0.85 m, in `y..y+h`). 4.5 m only when `h <= 0` (an old snapshot). |
| `joins[].closedReason` | Open counts. Cleared: the green CLEARED pad. Locked: red-amber LOCKED with a padlock. Reserved: amber ROUTE RESERVED. Closed: no countdown, curtain, beacon or lock columns. An unknown value reads as locked. `instances[].cleared` closes a circle only in a snapshot without `h`. |
| `members[].pendingArea` | The party row reads LOADING · the zone (its accent, with a slow sweep) for every member, while the place chip stays where they physically are; the pilot keeps a hold column in the hub. A load that ends without a move clears the deployed row. |

## Expedition co-op hosting (owner request 2026-10-15) — for Astra

Owner: "lets make hosting a lobby for this one better (So I can test with other people) Add Expedition as a option for hosting
rooms."

**Implemented on `codex/expedition-coop` (base `d6562d98`, 2026-10-15):** Expedition uses the existing internet room / LAN transport and host combat authority. Create Room has Expedition as its third mode; the existing Expeditions lobby replicates that choice and requires every guest to ready up.

- One to four signed-in, distinct human accounts. No lobby bots or dedicated-server start. Clients advertise Expedition support in the existing hello; old clients cannot strand the load barrier.
- Every player joins the same run. All clients load the current map and dynamic gate collision before reporting the exact run/epoch/layout/load ticket. The authority waits for all connected, unextracted members, then places living pilots in distinct validated slots. Health, resources and death state survive transfers.
- Every pilot's F input goes through host-measured gate, door, drop, cache and machine interaction. J votes and C rewards keep their authenticated commands and private projections. Party members travel together; the proposed join circle remains separate work.
- All deaths are recorded, and surviving guests can continue when the host's character dies. At the sanctuary reprint station, F starts a three-second stationary channel for a connected fallen teammate; movement, damage, disconnect or lost range interrupts it. Payment and resurrection are host-owned.
- Gate collision explicitly replicates the authority's retained solidity, including reversals. Guests do not infer solidity from animation time.
- Join before launch. Existing pilots can reclaim a seat within the transport's 90-second grace using the original account and resume token, including while an area load is frozen. Disconnected pilots do not block the barrier. The hosting application must remain running; endpoint migration is still unavailable, and the existing rules refuse host extraction while guests remain.

Verification: managed party lifecycle, director, integration, hub, menu, pick and acceptance checks; full managed suite through real local relay/LAN transport; Player/Runtime/Editor semantic compilation; source-package integrity. An Editor fixture covers two pilot placements, guest reprinting, gate collision and JsonUtility generation/solidity. **Unity/PhysX, Windows build and two-client gameplay are not exercised here.** Test with two updated signed-in clients: launch, delay one loader, guest F at gates/caches/door, host death, reprint, exit vote, disconnect/reconnect and host return to lobby. A failed load stays frozen instead of silently advancing.

Claude can polish the existing mode/status presentation; this change only wires the existing controls and corrects their capability labels. Waves 2/3 starts, new expedition catalogue entries, join circles and split-area parties are unchanged.

### Safe-zone border hidden in the hub (Claude, 2026-10-15)

Owner: make the safe zone border invisible in the hub, and keep the zone in case it is needed later. The sanctuary volumes and every
rule on them stay. Only the drawn edge (floor glow, curtain, motes, crossing ripple) is off in the fenced hub
(`NativeExpeditionStateScene.HideSanctuaryEdge`, set from `NativeExpeditionHubLive.Layout`). The fence, the gates, the ward and the
SAFE tag and chip still show where safety is.

## Astra split-area implementation scope (2026-10-16)

Status: design recorded before gameplay implementation, inspected at shared `f92a3e3`
(includes handoff `e4e12fb` and co-op validation repair). This supersedes the old
"No split indoor/outdoor simulation" restriction as a target, not as a claim that it works.
Do not ship the all-members interim circle: preserve current entry until the real
subset-transfer path passes the gates below. No unwilling member may be moved.

### Runtime boundary and sequence

1. Introduce an authority-owned area runtime registry, keyed by stable area ID, with
   one arena/combat state, physics scene, navigation state, event sequence and load
   state per occupied area. M1 is one hub plus one shared Ward Zero instance; two
   groups choosing Ward Zero join the same encounter, not private copies. Separate
   copies need an additional instance-run ID and are outside this first scope.
2. Separate the single `ConversionGame.arena` presentation/input binding from the
   registry of host simulations. Render only the local member's area. Remote areas
   retain simulation/collision, not cameras, lights, audio or presentation objects.
   `NativeArena` currently rewrites Match callbacks, uses global Physics queries,
   and ConversionGame calls global Physics.Simulate: merely constructing a second
   arena would cross-contaminate collisions and match state. Give each runtime its
   own match state and PhysicsScene; route EVERY authority query/overlap/cast through
   that scene and simulate each once per fixed step. Audit static state and global
   callbacks before enabling a second arena. Client-only camera queries must use
   the displayed area's scene as well.
3. Tick the shared ExpeditionSession clock exactly once per host tick. Tick each
   occupied runtime independently; loading an arriving cohort cannot freeze the hub
   or an already-running encounter. Retain empty-instance logical encounter state;
   disable its simulation until occupied, without resetting rewards or bosses.
4. Transfer an immutable cohort captured by the host at countdown expiry. Validate
   destination slots/capacity before committing membership; hold only transferring
   members, clear their buffered actions/choosing state, and commit placement once
   their per-cohort barrier succeeds. On failure keep the cohort recoverable at the
   source without charging/rewarding twice. Disconnect removes that member from
   required acknowledgements; reconnect receives a fresh destination ticket.

### Director, wire and layout changes

- Authority members gain `area` and an explicit pending transfer identity. Replace
  global area/phase/loading decisions in NativeExpedition, Director, integration,
  rewards, reprints and encounter scheduling with area-scoped decisions. Global
  run lifecycle/loop/quota stay separate from each instance phase. Never temporarily
  swap snapshot.area around calls; that risks saves, callbacks and duplicated loot.
- Add a negotiated split-area capability/version; refuse incompatible clients at
  room launch. Frames, events, commands and ready acknowledgements carry area,
  authority epoch and area generation/transfer identity. Authenticate sender from
  the peer seat and derive allowed area from host membership, never a claimed area.
  Reject delayed packets after transfer, reconnect or epoch change. Scope entity
  IDs/event cursors by area (or allocate globally unique IDs); clear interpolation,
  prediction, killcam and event queues only for the transferring peer.
- NativeExpeditionNetwork currently broadcasts one public packet and generation
  to everyone. Replace this with common minimal party metadata plus area-specific
  projections/frames per recipient. Other-area combat entities, events, anchors and
  drops must not leak; private offers remain owner-only. `view.area`, `phase`,
  `loading` and `areaBarrier` mean the VIEWER's area rather than the host's area.
- One barrier per transfer cohort/area generation; unrelated occupants are never
  required acknowledgers. Tickets include run/epoch/area/layout/generation and
  cohort identity. An existing instance keeps fighting while a newcomer loads;
  newcomer placement waits for readiness and a validated safe entry slot.
- Optional door `join {x,y,z,r,h}` is a cylinder with **base y** and top y+h;
  membership tests the authoritative capsule centre, inclusive radial/height bounds.
  Missing join preserves legacy layouts. Nonfinite, nonpositive radius/height and
  out-of-map cylinders fail validation. A join is only meaningful at a hub entry.
- Planned public `joins[]` entries: door ID, area, `joinInside`, `joinStartedAt`
  (-1 idle), duration 3 seconds; members add `atJoin` and `area`. Countdown starts
  with one eligible member, cancels when empty, and transfers exactly those eligible
  and inside at expiry. Dead/disconnected/extracted/loading/choosing members do not
  count. Multiple rings must resolve a member to one deterministic door per tick.
  F entry is removed only with this functional path; F exit/interactions remain.
- Claude may author exp-layout-4 after parser/geometry checks exist. Until authoritative
  joins and view fields actually exist, do not activate a deploy ring or countdown.

### Cross-area rule decisions for first implementation

| System | Rule |
| --- | --- |
| Damage, revive, carry | Same-area actors only; no cross-area target IDs or carried transfers. Death stays assigned to its area until reprint. |
| Reprint | Preserve sanctuary remote reprint: a living hub member can pay to reprint a connected dead member from another area into a hub slot. Cancel old corpse/revive ownership atomically; once-only charge and resurrection. Instance-local reprint/revive remains local. |
| Loot and wallets | Keep personal ownership and wallets. Physical pickup/claim requires the owner's actual area and measured reach. Boss credit/reward eligibility uses that encounter's participants, not everybody in the hub. No repeat grants on reentry. |
| Quota and boss clear | Shared run quota, each authenticated kill/clear credited once with area + encounter identity. Encounter scaling uses participants in that encounter; freeze the scaling basis at encounter start to prevent join/leave exploits. |
| Closures | Local encounter closures, never a global hub lock when someone enters Ward Zero. New entrants wait if encounter policy forbids admission; do not teleport them through sealed combat boundaries. |
| Wipe | An instance wipe does not end the run while a connected living member exists elsewhere. Preserve the dead cohort for reprint; retain encounter state until a separately specified reset. |
| Extraction | Personal extraction only at the existing valid hub extraction location, with an in-memory once-only completion record before confirming. Remote members are not extracted by a hub vote. Host extraction remains refused while guests remain because endpoint migration is unavailable. |
| Exit/continue votes | Scope local votes to eligible occupants. A global loop reset/ending requires all remaining members back in hub and no transfers, so no occupied instance is discarded. |
| Saves | Owner update (2026-10-16): fresh run every launch. No run saves, checkpoint writes, resume flow, save-envelope bump or schema-1 reader. Live disconnect/rejoin tickets remain; account/profile persistence is unchanged. |
| Disconnect/rejoin | Preserve area membership and encounter state during existing grace; no input from disconnected pilots. Rejoin loads that area and a new ticket before control returns. |
| Host migration | Existing code advertises candidates incapable/unreachable. Continue fail-closed and do not claim live migration. Future migration needs every area runtime snapshot plus an endpoint handoff and epoch barrier; safe checkpoint resume is not live migration. |

### Fresh runs and cost

Owner update (2026-10-16) supersedes the save/resume requirements in the original
scope. Every live launch creates a new run identity in memory. Do not open or
write old Expedition checkpoints. Extraction completion is recorded in the live
run; reconnect cannot reclaim an extracted member. Keep account/profile persistence
and reconnect to a still-running host. No save schema migration is required.
Layout fingerprints and protocol identities still gate live connections/transfers.
Old checkpoint files may remain on disk but are not loaded or deleted.

No measured two-area benchmark exists yet. Expected CPU cost is approximately
T_hub + T_instance + routing, not max(T_hub,T_instance); collision/navigation/AI
memory is additive. Rendering should remain one area's cost. A 60 Hz fixed step
has 16.67 ms total budget; a 10 ms instance plus a 5 ms hub leaves only 1.67 ms for
other fixed-step work (illustration, not a benchmark). Guests receive only their
area frames; host upload is the sum of recipient streams. Instrument per-area
step, physics, navigation, active counts, packet bytes and resident memory. Do not
promise smoothness from the owner's GPU alone.

### Gates before enabling subset entry

Managed: ring boundary/height/eligibility, leave/reenter/cancel/dead-at-zero,
subset placement, overlapping rings, per-area reward/reprint/clock isolation,
barrier timeout/reconnect/stale ticket, fresh launch isolation and once-only live extraction.
Transport: simultaneous hub/instance frames, no cross-area events/private offers,
stale input rejection, disconnect and host-death cases.
Native Unity: isolated scene collision/raycast/projectiles/hazards, host staying in
hub while guest fights and the reverse, host loading while guest remains active,
instance wipe + hub reprint, entry to an occupied instance, return/extraction, and
one-host/three-guest sustained worst-case performance. Windows host/guest play and
actual two-area timings are release gates, not inferred from managed tests.

### Parser preparation receipt

On `codex/expedition-split-area-handoff`, optional `doors[].join` now parses with
base-y semantics, positive finite dimensions, hub-only use, bounds, sampled floor
clearance and centre navigation checks. Legacy layouts without it still parse.
**No live join view fields or countdown exist in this preparation.** Claude can
prepare a layout draft against this parser, but should not switch the live layout
or show a working deploy countdown until subset transfer and its view are delivered.

### Accepted scope and runtime foundation receipt (2026-10-16)

Owner accepts the scope above: 3-second eligible-subset countdown, one shared Ward
instance, and no all-members interim. Milestone 1 foundation is implemented on
`codex/expedition-area-runtime` from shared `9f18015`; detailed ownership audit and
gates: [EXPEDITION-AREA-RUNTIME.md](EXPEDITION-AREA-RUNTIME.md). The live launch path
still uses one arena. The native two-scene isolation test must execute before
cohort integration. **joins[], members[].atJoin and members[].area are not yet
implemented.** Claude must keep exp-layout-4 and the join presentation disabled.
