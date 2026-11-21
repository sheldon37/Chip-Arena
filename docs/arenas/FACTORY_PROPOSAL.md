# Chip Arena: new arenas proposal (Arena 2: Factory)

2026-10-12 · Claude (presentation lane) · for the owner (decides) and Astra (implements collision, hazards, spawns, bots, multiplayer)
Base: `f575670` on `claude/sleepy-ritchie-g8ruhv` (= `codex/unity-cloud-current`), Unity port `unity/ChipArenaConversion`.
Status: **design and presentation on the branch `claude/stage-factory`; not merged or released. The map is not in `UnityMaps.json` and is not selectable.** Layout numbers come from the 2D draft script (`docs/arenas/scripts/factory_m1.py`, Appendix A). The simulation contract for Astra is `docs/arenas/FACTORY-SIMULATION-CONTRACT.md`. Nothing here has run in Unity, on Windows or in multiplayer.

Owner request (verbatim): "Finally, add a couple of more stages on the same quality of brick fall. A larger map with stage hazards. One is a factory (with like instant kill buzzsaws or things that can fall on players, the other is a cyber city. I can have astra handle the coding portion.)"

## At a glance

- **Factory (map id `factory`)**: Corvane Works, Line 3, on the first shift. A steel fabrication plant of two roofed machine halls either side of an open crane bay, under a low, smog-hazed morning sun that comes down through the halls' skylights in slanting shafts. The overhead crane, the glowing ladle in the middle of the bay and the floor-slot saws are the landmarks.
- **Larger than Brickfall:** an 80×64 m slab (Brickfall 64×56), a 76×60 m play space (4,560 m² of floor, Brickfall 3,120), about 720 m² of galleries, catwalks and pulpits at 3.6 and 6 m.
- **Three lanes, one hazard family each:**
  - **North and South Halls:** a 36 m **floor-slot buzzsaw** ("slitter") runs each hall's long lane every 14 s, and a **rail saw** sweeps across each hall's catwalk every 5 s.
  - **Crane Bay:** the **overhead crane** carries a 4 m steel beam bundle to one of four painted zones every 20 s and drops it after a 3 s telegraph; the landed bundle is cover for about 6.6 s.
  - **Bay walls:** a **die press** in each wall stamps its tunnel shut every 6 s (the short way between a hall and the bay).
  - Optional: four **steam vents** (light damage, or cosmetic only).
- **The owner's examples are instant kills** (saws, press, crane drop). Every one is a pure function of FightAt and the match time, is idle for the fight's first 4 s, and is telegraphed by lamps that are never reduced: green safe, amber soon, red live. No spawn is within 9 m of a hazard volume.
- **No new network data:** every client, the host and the killcam pose the hazards from the replicated phase, FightAt, round and time. Astra adds the definitions to `UnityMaps.json`, the kill volumes, the crane's landed collider, the nav and the tests (contract).
- **Presentation is built** (this branch): the light rig, sky and fog, block looks with a new `factory-grating` surface, a ~5,500-part dressing kit, the hazard rigs and their telegraphs, a five-sector backdrop, atmosphere, hazard effects and ambience. It is active only for map id `factory` and draws nothing on any other map.

---

## 1. Why a factory, and what it adds to the roster

| Roster need (ARENA_PROPOSAL.md §1) | Factory answer |
|---|---|
| Every old map shares one tower set, footprint and heights | Its own 80×64 footprint, three lanes with three different characters, decks at 3.6 and 6 m, roofs at 11.5 m |
| One lighting rig | Its own: a cool smog-filtered morning sun with warm sodium high-bays, a molten-orange ladle and cold dock floods (§3.10) |
| Few hazards, switched on map id | Five hazard families as data (`hazards[]`), pure functions of the clock, with one shared telegraph language |
| Brickfall's identity is golden-hour brick and collapse | The Factory is steel, concrete and moving machinery: timing and positioning instead of demolition |

The rule to learn: **yellow-and-black moves. Green lamps are safe, amber means soon, red means now.**

---

## 2. Quality bar

The Factory follows ARENA_PROPOSAL.md §2 unchanged (rules 2.1, kit 2.2, colour code 2.3, budget 2.4). Factory additions:

1. **Every lethal volume has a painted floor or wall mark that is at least as large as the volume** (slot danger lines at ±0.68 m, crane zones 0.2 m larger than the kill box, striped tunnel jambs and bolster, striped wall slots for the rail saws).
2. **Every lethal motion is preceded by at least 0.8 s of lamp telegraph plus sound** (slitter 1.95 s, rail saw 1.0 s, press 0.93 s, crane 3.85 s from red), and the lamps keep strobing for as long as the volume is live.
3. **Hazard telegraphs are never reduced** by Reduced effects or the low graphics tier: lamp halos, the slot glow strip, the zone's red wash and the load's contact shadow always draw. Sparks, dust and steam halve.
4. **The sun never lies about a drop:** the hanging load casts no sun shadow (at 36° it would fall about 13 m away from the zone); its telegraph is a vertical contact shadow straight below that darkens and tightens as it falls.

---

## 3. FACTORY

### 3.1 Pitch
08:10 in Corvane Works. The night shift has left Line 3 running. Two long machine halls under sawtooth skylights flank an open-roofed crane bay where a 40 t overhead crane shuttles steel bundles between the ladle and the rolling stock. In the halls the floor-slot slitters run their lanes on a clock, and rail saws swing out of the wall across the catwalks. Presses stamp the shortcuts through the bay walls. Each team holds a loading dock with a control pulpit over its mouth.

### 3.2 Footprint and heights
- **Slab:** 80×64 m (x ±40, z ±32). **Shell:** 2 m deep, a 4.4 m concrete plinth with corrugated cladding to 14 m; tops unstandable.
- **Play space:** x ±38, z ±30 (76×60 m). Bounds for the clamps and the bot clamp: x ±38, z ±30 minus the 0.42 m capsule (the five clamp sites already read map bounds after Brickfall; the Factory only needs its own `bounds`).
- **Area:** about 3,600 m² of open floor in the capsule-width raster (the draft's flood fill reaches all of it), plus the galleries (2 × 73 × 2.4 m), catwalks (2 × 60 × 2.4 m), pulpits (2 × 20 m²) and landings: about 4,300 m² walkable (Brickfall about 3,600 m²).
- **Heights:**
  - floor 0; bay walls 3.4 m with the galleries on top at 3.6 m (cantilevered 1 m over the bay);
  - hall catwalks 3.6 m along the outer walls;
  - control pulpits 6.0 m (parapets to 7.0, roof 8.8);
  - hall roofs 11.5–11.8 m with four 1.5 m skylight slots each (glazed lanterns to 12.8 m are kit);
  - crane runway girders 12.2–13.2 m on columns along both bay walls; the crane rides 13.3–16 m;
  - the ladle 4.0 m; steam hammers 3.4 m; shell 14 m.
- **Limits:** the y 24 projectile ceiling and the −6 kill plane are unchanged. No pits.
- **Jump reach** (ARENA_PROPOSAL.md §3.2 table): base apex 1.57 m. From a 3.6 m deck a rubber build (6.3 m) reaches 9.9 m (under the roof); a moonboots + rubber build (13.4 m) can come up through a skylight slot onto a roof, and rubber + springs from a gallery can reach the runway top. **Unstandable tops** (pilot-only blocking columns in the draft's `pilotBlocks`): the shell tops (13.9), both hall roofs (11.7), both runway girders (13.1), both pulpit roofs (8.7) and the ladle's melt (3.9).
- **Not colliders:** the crane bridge, trolley, hook and the carried load (they sit above 9.5 m, out of reach of all but stacked jump chips; shots pass through them). The landed load is a collider (§3.8).

### 3.3 Blockout (top-down)
Generated by `docs/arenas/scripts/factory_m1.py` (run `python3 docs/arenas/scripts/factory_m1.py [--out DIR]`; `--out` also writes the draft `factory_m1.json` in the UnityMaps.json shape and a top-down `factory_m1.png`). 1 char = 1 m of x and 2 m of z; north (+z) is up. The map is point-symmetric: (x, z) → (−x, −z). Overhead decks are drawn only where the floor is open.

```
         x=-40                                    x=0                                     x=+39
 +30..+32 ################################################################################
 +28..+30 ##////////cccccccccccccccccccccccccccgcccccccccc~ccccccccccccccccccccc////////##
 +26..+28 ##........cccccccccccccccccccccccccccccccccccccc~ccvvccccccccccccccccc........##
 +24..+26 ##.....oo.....RRRRRR.....xx.......RRRRRR.....ooo.....RRRRRR.....xx........OOOO##
 +22..+24 ##.....oo...OO...........xx..................ooo................xx.O..ooo.OOOO##
 +20..+22 ##..........OO.....................................................O..ooo.OOOO##
 +18..+20 ##OOOO......OO.......SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS........O..........##
 +16..+18 ##OOOO.f....OO.....................................................O.....h....##
 +14..+16 ##OOOO......OO.........MMM......................MMM.............MMMO..........##
 +12..+14 ##.OOOOOOO..OO.mmmmm...MMM.......mmmmm..MMMM....MMM....mmmmm////MMMO..OOOOOOO.##
 +10..+12 ##=CCggg====CC=======CC=====ppp=C========C=ggg==CC=======CC///////CC====gggCC=##
  +8..+10 ##===////////===============ppp============ggg=====================////////OOO##
  +6.. +8 ##...////////..xxvv...........OOOoooo......ZZZZ...........OOOO.....////////OOO##
  +4.. +6 ##.4..xx.......xx..oo...ZZZZ..OOO..........ZZZZ....ooo....OOOO.........7..OOOO##
  +2.. +4 ##..OO..2KKK.CPPCP.oo...ZZZZ..OOO..........ZZZZ.......OOO......CPPCPKKK.......##
  +0.. +2 ##..OO...KKK.PPPPP......ZZZZ..ooo....LLLLLL...........OOO......PPPPPKKK....5..##
  -2.. +0 ##..1....KKK.PPPPP.....OOO...........LLLLLL....ooo..ZZZZ.......PPPPPKKK...OO..##
  -4.. -2 ##.......KKK.CPPCP.....OOO.......ZZZZ..........OOO..ZZZZ...oo..CPPCPKKK.6.OO..##
  -6.. -4 ##OOOO..3.........OOOO....ooo....ZZZZ......ooooOOO..ZZZZ...oo..xx.......xx..8.##
  -8.. -6 ##OOO////////.....OOOO...........ZZZZ......ooooOOO............vxx..////////...##
 -10.. -8 ##OOOOgg==========================ggg============ppp====================ggg===##
 -12..-10 ##=CCggg====CC///////CC=======CC==ggg=C========C=ppp=====CC=======CC====gggCC=##
 -14..-12 ##.OOOOOOO..OOMM////mmmmm....MMM....MMMM..mmmmm.......MMM...mmmmm..O..OOOOOOO.##
 -16..-14 ##..........OOMM.............MMM......................MMM..........O......OOOO##
 -18..-16 ##....H.....OO.....................................................O......OOOO##
 -20..-18 ##..........OO.......SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS........O....F.OOOO##
 -22..-20 ##OOOO.ooo..OO.....................................................O..........##
 -24..-22 ##OOOO.ooo....xx................ooo..................xx................oo.....##
 -26..-24 ##OOOO........xx.....RRRRRR.....ooo.....RRRRRR.......xx.....RRRRRR.....oo.....##
 -28..-26 ##........cccccccccccccccccvvccc~ccccccccccccccccccccccccccccccccccccc........##
 -30..-28 ##////////cccccccccccccccccccccc~ccccccccccGcccccccccccccccccccccccccc////////##
 -32..-30 ################################################################################
```
**Legend** (heights above the floor at y 0)
- *Shell and structure:* `#` shell, 14 m, indestructible, top unstandable. `=` bay wall, 3.4 m, with the gallery on top (`g` where it overhangs open floor). `C` runway columns and pulpit legs. `p` die press tunnel (3 m wide, 1.4 m deep, 2.5 m clear when open).
- *Decks:* `c` hall catwalk at 3.6 m. `g` gallery at 3.6 m. `P` control pulpit at 6.0 m. `/` stairs (17 steps of 20 cm; 11 on the pulpit flights).
- *Cover:* `m` lathes (1.1 m bed, 1.9 m headstock). `M` mills, switch cabinets (2.2–2.4 m). `R` cantilever bar racks (2.4 m). `O` tall hard cover: tool cribs, shelving, hall-end screens (3.0 m), site offices, forklifts, billet racks, steam hammers (3.4 m). `o` low hard cover: coils, bins, ingots, plates, moulds, beam bundles (0.9–1.6 m). `x` breakable cover: crates, pallets (wood), oil drums (metal). `K` dock containers (2.6 m). `L` the ladle on its car (4.0 m).
- *Hazards:* `S` floor-slot saw path (blade radius included). `~` rail saw sweep across the catwalk. `Z` painted crane drop zones (3.2×5.0 m). `v` steam vents.
- *Spawns:* `1-4` Team A (West Dock), `5-8` Team B (East Dock); `f g h` / `F G H` extra FFA spawns.

### 3.4 Sections
```
SECTION A-A  x = -10.5 (through the north press), looking west. S left, N right.
 13.2 ..........rrr.........................................rrr...........   runway girders 12.2-13.2 (crane rides on top)
 11.8 RRRRRRRRRR|                                            |RRRRRRRRRR   hall roofs 11.5-11.8, skylight lanterns above
  9.6 ..........|            [load 9.5-10.4 when carried]   |..........
  6.0 ..........|                                            |..........
  3.6 cccc......ggg|                                      |ggg......cccc   catwalks / galleries at 3.6
  2.5 ..........===|                                      |=p=.........    press ram underside when open 2.5
  0.0 ____S_____===|________Z_____________LL______________|=p=_____S___   floor; S slitter slots at z +-19.5
      z: -30 shell | -19.5 slot | -10 bay wall | -4.5 zone | 0 ladle | +9.7 press | +19.5 slot | +30 shell
```

### 3.5 Zones (mirror pairs listed together)
| Callout | What it is | Plays as |
|---|---|---|
| **West Dock (A) / East Dock (B)** | 11×18 m loading yards: site office, a container under the pulpit, forklift, pallets, stairs to both galleries, roller doors in the shell | Spawn yards with three exits: the bay mouth, the two hall doors, two stairs |
| **Crane Bay** | 54×16 m open bay under the crane: the ladle at the centre, steam hammers, billet and bar racks, ingot and plate stacks, four painted zones | Mid range, constantly re-shaped by the crane's landed loads |
| **The Ladle** | 3.4×3.4 m, 4 m tall, melt glowing on top (unstandable) | Splits the bay's centre line; the hottest cover on the map |
| **North / South Gallery** | 2.4 m decks at 3.6 m on the bay walls, the full length of the building | The high flank: sees the bay and, through the colonnade, its hall |
| **West / East Pulpit** | 4×5 m control cabs at 6.0 m over each dock mouth, 1 m parapets, open sides | Team-side perch over the bay; reached only from its gallery |
| **North / South Hall** | 54×19 m machine halls under the roofs: machine row, the slitter lane, storage row, catwalk aisle | Long-lane rifle play with a moving hazard; close play in the rows |
| **Slitter Lanes** | 36 m floor slots at z ±19.5 with 0.68 m danger lines | The rifle lane is safe only between runs |
| **North / South Catwalk** | 2.4 m decks at 3.6 m along the outer walls, rail saw gate at x +8 (N) / −8 (S) | Perch over the lane; the gate forces timing |
| **Hall-end rooms** (NW, NE, SW, SE) | 11×19 m ends behind 3 m blockwork screens: tool cribs, shelving vestibules at the dock doors, the welding bay (NW) | FFA spawns; ambush and rotation |
| **Press Tunnels** (N at x −10.5, S at x +10.5) | 3 m tunnels through the bay walls under the galleries | The short way hall ↔ bay, on a 6 s clock |

### 3.6 Flow and sightlines
- **Routes between the docks** (spawn to spawn about 64 m in a straight line):
  - **Bay:** dock mouth → bay → dock mouth, about 64 m (9.4 s at 6.8 m/s), past the ladle and two of the four zones.
  - **Halls:** dock door → hall-end room → aisle or lane → room → dock door, about 80 m (12 s).
  - **High:** dock stair → gallery (3.6 m) → gallery → stair, about 75 m, with drops into the bay or the hall anywhere.
  - **Shortcuts:** the press tunnels and the two 3.5 m bay doors (x 3..6.5 N, −6.5..−3 S) cross between the bay and a hall in 1.4 m.
- **Sightlines:**
  - the slitter lanes: 54 m between the hall-end screens (the saw sweeps them every 14 s);
  - the catwalks see their lane end to end; the inner aisle under the galleries is screened by the machine row;
  - the bay: at eye height its long axis is broken by the ladle, the hammers and the racks; pulpit to pulpit (eye 7.4 m) is an open 50 m duel line over everything (parapets give cover; G2 can make the pulpits destructible, §3.9);
  - galleries see down into the bay and, through the column line, into their hall at 3.6 m.
- **Spawns:** every FFA and team pair is blocked (§3.7).
- **Fit by kit:**
  - **Ranged:** catwalks over the lanes, pulpits, galleries.
  - **Melee:** hall-end rooms, the machine rows, the dock yards, the press tunnels and the landed loads.
  - **Magic:** the bay's open floor and the choke points at the tunnels and bay doors.
- **Round arc:**
  - 0–4 s: everything parked; the lamps glow green.
  - 4–6 s: rail saws and presses begin.
  - 6 s: the first slitter warning; the crane sets off at fight + 10 s (first impact at fight + 19.9 s).
  - Five crane drops per 100 s of fighting (every 20 s). Overtime (fight + 100 s) is unchanged.

### 3.7 Spawns
Per-map table (Brickfall's contract): yaw in degrees from +z toward +x, facing the origin; y = 1.1.

| Team A, West Dock | Position (x, z) | Yaw | | Team B, East Dock | Position (x, z) | Yaw |
|---|---|---|---|---|---|---|
| A1 | (−35.5, −1.0) | 88 | | B1 | (35.5, 1.0) | 268 |
| A2 | (−32.0, 3.6) | 96 | | B2 | (32.0, −3.6) | 276 |
| A3 | (−31.8, −5.0) | 81 | | B3 | (31.8, 5.0) | 261 |
| A4 | (−36.6, 4.2) | 97 | | B4 | (36.6, −4.2) | 277 |

FFA seat order (`min(seat, 7)`): A1, B1, f (−32.5, 18) NW room, F (32.5, −18) SE room, g (−3, 28.6) north catwalk aisle, G (3, −28.6), h (33.5, 17) NE room, H (−33.5, −17) SW room. The two-team rule (exactly two teams of at most 4, lower id = side A) is Brickfall's.

**Draft checks** (2D, not Unity; `factory_m1.py`):
- 28/28 FFA pairs and 16/16 team pairs blocked with ±1 m jitter at both ends and eye heights 1.2, 1.6 and 1.9 m.
- Closest pair: FFA 16.1 m; team A to B 63.8 m.
- Closest spawn to any hazard volume: 9.1 m (g to the north slitter). Closest spawn to any block below 1.9 m: 1.1 m.
- All spawns inside the bounds minus the capsule; every spawn reachable on the ground raster.
- No prop in the slitter lanes; the closest block to a painted crane zone is 1.08 m (the ladle car).

### 3.8 Hazards
All timings are **seconds on the hazard clock** `c = now − FightAt − 4` during the fight and its result (parked otherwise), as `NativeFactoryHazards.Clock`. Each hazard's cycle `n` starts at `start + offset + n·period`. The round index (`NativeStructureFx.RoundIndex(set, round)`) only flips which end or zone goes first on odd rounds, so each side gets the first run in turn. The states, kill volumes and cue times are the pure functions in `Core/NativeFactoryHazards.cs` (FACTORY-SIMULATION-CONTRACT.md §3), which both the presentation and the simulation use.

**H1. Floor-slot buzzsaws ("slitters"), instant kill** — `factory-saw-slot-n` / `-s`
| Item | Value |
|---|---|
| Path | z ±19.5, x −18 → +18 (36 m) at floor level; blade radius 0.8 m, 0.05 m thick (kill box 1.6×1.6×0.1 m clipped to the floor) |
| Cycle (14 s; N offset 0, S offset 7; first at c = 2) | warning 1.6 s (blade spinning up under the floor) → rise 0.35 s → run 6.0 s (6 m/s) → sink 0.35 s → parked 5.7 s at that end; the next run comes back the other way |
| Lethal | while at least 0.15 m of blade stands above the floor: from about 0.1 s into the rise to about 0.25 s into the sink (about 6.5 s of 14 s, the blade's box only) |
| Telegraph | slot lamps every 2 m on both edges: green parked; amber strobing at 6 Hz through the warning, with an amber glow strip down the slot and sparks spitting from the start end; red while it runs, with a 10 Hz chase 1 m behind to 3.5 m ahead of the blade; red danger lines at ±0.68 m |
| Counterplay | the lethal band is 0.94 m wide (blade plus capsule); step off the line. The blade is visible and audible the whole run. At 6 m/s a pilot on the line has 0.58 s from the chase to the blade |
| Presentation needs | the state (phase, U, lift, spin, reverse); `HazardHit` with Detail `saw` (existing saw-cut routing) |
| Nav | the lane's slot cells cost more and are forbidden while Warning–Sinking within 4 m of the blade; crossing links stay open |

**H2. Rail saws ("sweepers"), instant kill** — `factory-saw-rail-n` / `-s`
| Item | Value |
|---|---|
| Path | from a slot in the outer wall at z ±30.6 to z ±27.0 across the catwalk at x +8 (N) / −8 (S); blade centre 4.65 m (1.05 m above the deck), radius 0.85 m, axle along x |
| Cycle (5 s; N offset 0, S offset 2.5; first at c = 1) | warning 1.0 s → out 0.45 s → held 0.35 s → back 0.45 s → parked 2.75 s |
| Lethal | out, held and back (1.25 s of 5 s), the blade's box (0.1×1.7×1.7 m) |
| Telegraph | the wall lamp: green parked, amber strobing through the warning, red while live; a band across the deck under the sweep (amber pulsing through the warning, red while live) and spin-up sparks from the slot; the striped wall slot; a rising whine; the catwalk rail has a gap at the sweep |
| Counterplay | cross in the 2.75 s window (the crossing takes about 0.4 s); a jump does not clear it (apex 1.57 m) |
| Nav | the catwalk link through x ±8 opens only in the parked phase with at least 1 s left |

**H3. Overhead crane drop, instant kill** — `factory-crane`
| Item | Value |
|---|---|
| Rig | bridge on the runways (x −20..20), trolley across it; the load is a 2.2×0.9×4.0 m beam bundle hanging with its underside at 9.5 m |
| Zones, visiting order | 1 (−14, 3) · 2 (14, −3) · 3 (−5, −4.5) · 4 (5, 4.5); odd rounds start at zone 2. Painted zones 3.2×5.0 m; kill box 2.8×2.2×4.6 m |
| Cycle (20 s; first at c = 6) | travel 6 s to the zone → telegraph 3 s over it → fall 0.85 s (from 9.5 m) → landed 5.15 s → the hook regrips 1.5 s → hoist 2 s → idle 1.5 s |
| Lethal | the end of the fall while the load's underside is at or below 2.2 m, plus 0.06 s after it lands (about 0.17 s) |
| Landed load | a static collider (cover) from the landing until the hoist starts (6.65 s); a pilot overlapping it at landing is already dead; pilots standing on it drop 0.9 m when it lifts |
| Telegraph | zone lamps: amber pulsing while the crane travels there, red strobing at 8 Hz through the telegraph, solid red as it falls; a red floor wash; the load's vertical contact shadow (darker and tighter as it falls); the trolley's beacon sweeping amber/red; two horn blasts (at 0 s and 1.5 s of the telegraph) and the clamp's clank at release |
| Counterplay | 3.85 s from red to impact; the zone is painted |
| Presentation needs | the state (phase, zone, X, Z, hook and load heights, sway, impact time); `HazardHit` Detail `crane` |
| Nav | zone cells forbidden from the travel start to the landing; the landed load is an obstacle and cover |

**H4. Die presses, instant kill** — `factory-press-n` (x −10.5) / `-s` (x +10.5)
| Item | Value |
|---|---|
| Geometry | a 3 m tunnel through the bay wall (z ±9.0..10.4); ram 2.9×0.5×1.3 m, underside 2.5 m open, 0.08 m slammed |
| Cycle (6 s; N offset 0, S offset 3; first at c = 0.5) | open 3.38 s → warning 0.8 s (the ram lifts 15 cm, hissing) → slam 0.22 s → held 0.6 s → lift 1.0 s |
| Lethal | slam, held and lift while the ram's underside is below 1.8 m (about 1.33 s of 6 s): the footprint from the floor to the underside |
| Telegraph | head lamps on both faces and a gallery beacon: green open, amber through the warning, red live; striped jambs, bolster and floor border; light-curtain posts |
| Counterplay | the tunnel takes 0.33 s to cross; the open window is 3.38 s plus the warning |
| Nav | the tunnel link opens only while open with at least 1 s left |

**H5. Steam vents (optional)** — `factory-vent-bay-a` / `-b`, `factory-vent-hall-n` / `-s`
| Item | Value |
|---|---|
| Geometry | 1.4 m grates (bay at (−22.5, 7) and mirror; halls at (12, 27.4) and mirror); steam column 1.4×3.2×1.4 m |
| Cycle (9 s; offsets 0 / 2.25 / 4.5 / 6.75) | quiet 6 s → hiss 0.8 s → burst 2.2 s |
| Damage | 10 per 0.5 s tick inside the column while bursting (at most 50 per burst); not lethal, no push. **Owner choice:** ship as cosmetic first (damage 0) |

**Kill credit, immunity and multipliers** (FACTORY-SIMULATION-CONTRACT.md §4): an instant kill sets the victim's health to 0 whatever their armour, block or damage multipliers; credit goes to the last pilot who damaged the victim within 6 s, otherwise it is environmental (owner decision 7 of Brickfall). Hazards hit bots and PvE adds too.

### 3.9 Destruction plan
The Factory is a steel plant: its shell, walls, columns, roofs, machines and the ladle are `static`. Destruction is local:
- **Breakable cover (today's rules):** crates and pallets (wood, 34 HP) and oil drums (metal, 150 HP).
- **Masonry (G2, optional):** the four hall-end blockwork screens as `masonry` (120 HP; bullets ×0.25, melee ×0.5, explosives ×1.5). Blowing one opens a hall-end room onto its lane.
- **Segments (G2, optional, Brickfall's §5 contract unchanged):** `pulpit-a` / `pulpit-b`. Supports are the four legs (`structural`, explosives only, 80 HP each), `failBelow` 3 (lose two legs). Mode `topple` toward the bay (1.0 s telegraph, 0.7 s fall). Crush 45, capped at min(60, 60% max HP), plus sweeps over the cab's footprint and 2 m beyond. Rubble: the cab on its side in the dock mouth (1.3 m, cover). This removes a team's perch at the cost of explosives.
- The crane's landed load is a hazard (§3.8), not destruction.

### 3.10 Visual identity
**Palette** (sRGB)
| Group | Colours |
|---|---|
| Concrete | floor `#6E6A63`, plinth `#8C877D`, bay walls `#9A9488`, blockwork `#A49D90` |
| Cladding and roofs | shell `#5E6E78`, roof sheet `#5A646C`, trusses `#46525C` |
| Structure paint | columns and brackets `#3C4E5C`, runway `#4A5864` |
| Machines | machine green `#4F7A6A` / `#557F70`, cabinets `#8E9AA0`, racks `#2E5584` |
| Hazard and safety | yellow `#E8B21A` (rails, toe boards), crane `#E3A21A`, press `#C79A22`, stripes `#F4B321`/`#1C1A18`, danger red `#B8322A` |
| Steel stock | coils `#6E6A66`, billets `#7A6E64`, beams `#8A4E32` (red oxide), ladle `#2E2B29` |
| Light | sodium `#FFA14A`, melt `#FF6A1E`, dock floods `#DCE8FF`, welding arc `#B8D4FF`; lamps: safe `#1F7A3A`, amber `#FFB020`, red `#C81E14` |

- **Pilot readability:** the large surfaces are mid-value greys and greens; saturated yellow is thin (rails, stripes) or high (the crane). Bot `#eac558` against the yellow rails is the worst case and stays a value check at G3.
- **Looks** (MapLooks.json `factory` blockLooks): `concrete-board`, `corrugated`, `steel-green` (machines, crane, stock), `plain` (painted structure and rails, so they read clean rather than rusted), `timber`, `demolition` (the press bolster), and the new **`factory-grating`** (NativeBlockLit look 14). It draws pressed bar grating on deck tops (bars along the longer axis, cross rods, a worn walking line), a flat-bar edge on thin sides and welded mesh on tall sides (the tool cribs).
- **Dressing kit** (`Core/NativeFactoryKit.cs`, 54 kinds in `kitLooks`, 5,504 parts on the draft):
  - walkways: rails, posts, knee rails and toe boards on every open deck edge, gaps where stairs land and where the rail saws sweep; stringers, sloped handrails and yellow nosings on every flight; gallery brackets;
  - structure: H-section flanges, base plates, hazard collars and haunches on the columns; rails, stiffeners and striped end stops on the runways; trusses and purlins under the roofs; glazed skylight lanterns (they cast no shadow, so the sun reaches the floor); roof vents;
  - shell: pilasters every 9 m, clerestory windows, steam, water and gas mains with flanges and valve drops, roller doors with striped jambs;
  - bay walls: striped jambs and lintels, kick strips, sign plates; press heads with hydraulic cylinders and light-curtain posts;
  - machines and stock: lathe ways, carriages, tailstocks, chucks and lamps; mill tables, handwheels and DROs; cabinet doors, lamps and cable trays to the roof; rack uprights, arms and bar ends; coil faces and straps; bar-end grids, beam I-sections, plate courses, dunnage;
  - rooms and docks: tool-crib frames, shelves with cartons and bins, screen copings and extinguishers, office windows and roof units, container castings and lock bars, forklift masts, forks and wheels;
  - the ladle's hoops, trunnions and lip, its car's wheels; floor paint (aisle lines, joints, drains);
  - hazard fittings: slot voids, edge plates, danger lines and striped ends; rail-saw tracks, braces and striped wall slots; vent grates; tunnel borders; numbered, hatched crane zones.
- **Hazard rigs** (`Core/NativeFactoryRig.cs`): sixteen-tooth blades (four turned square plates on a hub), the rail-saw carriages with guard hoods and motors, the rams with stripes and guide rods, the crane (end trucks with wheels, two box girders, walkway, hanging cab, trolley with hoist, motor and beacon, ropes, hook block, slings, the three-beam bundle with straps and sway) and every lamp.
- **Saw deaths** (presentation only): a pilot a saw kills is split in two along the blade's plane through the pelvis, with sealed caps, sparks, blood and a bloodied blade; gore off keeps the current death. See `docs/BISECT-GIB.md`.
- **Landmarks:**
  - the crane;
  - the ladle;
  - outside the walls: No. 3 blast furnace, three banded smokestacks, the CORVANE WORKS sign above the east sheds (readable from the bay), the cooling tower and the gas holder.
- **Lighting rig `factory-first-shift`:**
  - Sun: `#FFE8C4` at 1.15, elevation 36°, **bearing 150°** (SSE), soft shadows at .82. It is perpendicular to the team axis (x), so both docks have it to the side. It slants through the skylight slots as stripes and shafts.
  - Rim: `#9DB6E0` at .30 from 330°, 18°.
  - Trilight: sky `#A3B4C4`×.48, equator `#8F877C`×.34, ground `#2E2A26`×.40.
  - Shadows: 4 cascades over 85 m; the low tier falls back to 2 over 65 m.
- **Fog and sky:** linear fog `#8E9AA3` from 46 to 210 m; ground haze 18% to 3 m (`#A8A49A`); the dusk-variant sky drawn as a smoggy morning: horizon `#CDC4B0`, zenith `#5C7690`, a pale sun disc `#FFF4DE` (.04) with a wide warm glow, light clouds at .45×, no stars.
- **Practical lights** (8, NativeBlockLit's own block):
  - four sodium high-bays `#FFA14A` (×1.5, 15 m) at (±13, 9.6, ±20.5);
  - the ladle's melt `#FF6A1E` (×2, 13 m, slow fire flicker);
  - two dock floods `#DCE8FF` (×1.2, 12 m) at (±33, 6.5, 0);
  - the welding bay's arc `#B8D4FF` (fast flicker) in the NW room.
- **Backdrop** (`Core/NativeFactoryScenery.cs`, 5 sectors + the goliath trolley; drawn in the menu too):
  - the concrete apron, road, fence and floodlight masts; three rail sidings with hopper wagons and a shunter;
  - the blast furnace with stoves, downcomer, dust catcher and skip incline, its cast house, three banded stacks;
  - three sawtooth sheds and the sign, the gas holder, a water tower;
  - the stockyard's piles under a goliath crane whose trolley travels every 96 s; the sinter plant;
  - the cooling tower, the substation and a pylon line, the works office.
- **Atmosphere and effects** (`Core/NativeFactoryFx.cs`):
  - sun shafts under every skylight; 200 dust motes that brighten inside the shafts; the ladle's churning melt, embers and heat steam; steam leaks; the welding bay's arcs and sparks;
  - blade sparks and blur discs; the crane's impact dust ring, shock skirt and sparks; press slam puffs; vent steam;
  - stack plumes, the cooling tower plume, aviation lights and the furnace flare.

### 3.11 Audio and ambience
- **Hazard cues** (existing clips, each with a `factory.*` key the owner's effects bank can fill; `Runtime/NativeFactoryAudio.cs`):
  - slitter: spin-up (`saw-cut` low), emerge (`spike-rise`), grinding every 0.55 s along the run (`saw-cut`), sink (`metal`);
  - rail saw: whine (`saw-cut`), swing (`dash` + `saw-cut`), return (`dash`);
  - press: hiss (`water` high), slam (`metal` low + `stone`), lift (`water`);
  - crane: move (`metal`), two horns (`countdown` low), release (`metal`), impact (the synthesized metal landing + `explosion` low), hoist (`metal`);
  - vents: hiss and burst (`water` + `fire`).
- **Ambience `factory`** (one-shot schedule, `NativeFactoryAmbience`): lathe chatter (`metal`), the bay's steam hammers (`stone`), the ladle's crackle (`fire`), the welding arc (`lightning`, on its bursts), steam leaks (`water`), a distant works horn every 47 s. No new audio files.
- **Music** (owner ask): id `factory-line-three`. Udio prompt: "Instrumental industrial big beat, 124 BPM, 4/4, mechanical and driving. Pounding drop-forge kick on the one, clanging anvil and sheet-metal percussion, hydraulic hiss swells, a grinding distorted bass riff, chopped funk breaks, a low brass motif like a works horn, shimmering ride like spinning steel. Mood: a steel mill at full speed, dangerous and precise. No vocals. Consistent energy, mixed loud and clean, seamless loop with no fade-in, fade-out or silence." Delivery spec: ARENA_PROPOSAL.md §8.1.

### 3.12 Performance budget (estimates; not measured on the owner's PC)
| Item | Factory |
|---|---|
| Arena blocks | 353 draft blocks: one stage mesh, 1 draw plus shadow passes |
| Kit + rigs | 5,504 static parts on the draft in batches of 500 split by shadow mode (12 draws), the rig ≤1,400 parts (201 at the hero moment) in ≤5 draws: about 17 draws (budget ≤60) |
| Backdrop | 6 draws, about 53k vertices (budget ≤10 draws) |
| Effects | 2 draws, ≤3,600 quads; telegraphs never reduced; sparks, puffs, motes halved on Reduced, no shafts on the low tier |
| Lights | 0 new Unity lights; 8 practical lights in NativeBlockLit's block |
| Shadows | 4 cascades over 85 m (2 over 65 m on the low tier) |
| Sim | 10 replicated props at round start (crates, pallets, drums); 343 static blocks; 2 landed-load colliders at most; no new frame fields |
| Risk | the kit's geometry (5,504 instances of the 216-vertex, 188-triangle block mesh, about 1.0M triangles per main pass plus the casting share per cascade) is the first thing to measure at G3; the floor joints, shelf cartons and stack courses are the first to cut |

### 3.13 PvE suitability
- **Horde waves fit well:** wide lanes, four spawn-side entries (roller doors and dock mouths) for adds, three lanes to kite through and the bay to hold.
- **The hazards hit adds too.** Luring adds onto a running slot or under a red zone is a skill, and kills credit the luring pilot's last damage.
- **Nav needs hazard awareness** (Astra): the volumes forbidden by phase in §3.8, plus the landed loads as obstacles.
- **The Siren boss fight stays in Ward Zero.** The Factory is a PvP and horde map.
- **Spawn safety for adds:** add spawn points stay 3 m from every hazard volume, as pilots' do.

---

## 4. Owner decisions
1. **Name and id:** map id `factory`, display name "Factory", the plant "Corvane Works" (invented). Keep or rename?
2. **Instant kills:** saws (as asked), the press and the crane drop are instant kills (recommended). Alternative: the crane drop deals 90 plus a push.
3. **Steam vents:** 10 per 0.5 s (recommended for G2) or cosmetic only.
4. **Random pool:** `rotation:false` until G3 (recommended, as Brickfall).
5. **Instant kill and protection:** an instant kill ignores block, armour, damage multipliers and the Demon Step i-frames (recommended), or respects i-frames?
6. **Destructible pulpits** (G2 segment, §3.9): yes or no.
7. **Music:** commission `factory-line-three` (Udio licence terms as ARENA_PROPOSAL.md §8.1).

## 5. Contract and work split
The simulation contract, the hazard schema, the pure functions, events, replicated state, tests and a ready-to-paste prompt for Astra are in `docs/arenas/FACTORY-SIMULATION-CONTRACT.md`.

| Step | Astra (sim) | Claude (presentation) | Gate |
|---|---|---|---|
| 0 | Review the contract | This proposal, the draft, the presentation (this branch) | H0: contract agreed |
| 1 | `factory` in UnityMaps.json from the draft (blocks, spawns, bounds, pilot blocks, `hazards[]`), selectable, `rotation:false` | Re-render on the real map; fix any id the dressing misses | G1 grey-box with hazards |
| 2 | Kill volumes and damage from the shared states, the landed-load collider, credit, HazardHit events | Tune telegraph timing and FX on the real states; audio balance | G2 hazards |
| 3 | Hazard-aware nav (bots and adds), spawn checks, tests | Performance capture, camera fade for high kit if needed, music | G3 art and playtest |

**Animation impact:** none on hit timing, hitboxes or cooldowns. New damage sources: the five hazards. New movement case: pilots standing on a landed load drop 0.9 m when it lifts.

**Not verified anywhere in this document:** Unity renders, Windows runs, multiplayer, frame time, bot behaviour, the kit's real draw cost, audio balance. A passing build is not proof of any of these.

## Appendix A: files
- `docs/arenas/scripts/factory_m1.py`: the blockout painter, hazards, spawns and pilot blocks, the jittered spawn LOS check, spacing, hazard and block clearances, the ground raster, the hazard fairness numbers; `--out` writes the draft JSON and PNG.
- Presentation:
  - `Core/NativeFactoryHazards.cs`: schema and pure states (draft for Astra; contract §3);
  - `Core/NativeFactoryKit.cs`, `Core/NativeFactoryRig.cs`, `Core/NativeFactoryScenery.cs`, `Core/NativeFactoryFx.cs`;
  - `Runtime/NativeFactoryPresentation.cs`, `Runtime/NativeFactoryAudio.cs`;
  - the `factory` entry in `MapLooks.json`, the `factory-grating` look in `NativeBlockLooks` and `NativeBlockLit.shader`;
  - `Tests/FactoryChecks.cs`.
- Offline renders: Claude's session scratchpad (`stage-factory/renders/`), not in the repo.
