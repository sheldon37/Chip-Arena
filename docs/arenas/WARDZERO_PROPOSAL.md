# Chip Arena: Ward Zero revamp (`siren`), two floors, vents, doors, breaches and hazards

2026-10-13 · Claude (presentation lane) · for the owner (decides) and Astra (simulation, collision, spawning, hazards, nav, networking, tests)
Base: `258e348` on `claude/sleepy-ritchie-g8ruhv`; branch `claude/wardzero-rework`; Unity port `unity/ChipArenaConversion`.
Status: **design, draft data and dormant presentation on the branch; today's Ward Zero is unchanged and still the map in the game.** Astra builds the simulation from `docs/arenas/WARDZERO-SIMULATION-CONTRACT.md` (it ends with a ready prompt). Layout numbers come from the deterministic draft script `docs/arenas/scripts/wardzero_m1.py`; looks come from offline renders that run the game's own block shader code. Nothing has been run in Unity, on Windows or in multiplayer.

Owner's requests (verbatim):
- "Rework Ward Zero map, support vent spawning / door spawning, add hazards. Make it a lot bigger, 2 floors, and improve the lighting, effects, and overall design"
- Earlier: "I am planning to revamp this map, so keep this in mind" (the brute drop was built data driven for this).
- Hulk (2026-10-13): "Add a Hulk zombie too, similar to brute but much bigger model size, huge knockback, very slow, very high hp. 1/2 max spawned. The jump in too, shaking the ground and a even bigger stage effect when landing. They can also break through the walls to enter (Stage will eventually holographically recover broken pieces)". The map's part: breach walls and a Hulk-sized drop (sections 11.5 and 11.6). The Hulk itself and its burst and restore effects are the zombie lane's (`docs/SIREN-ZOMBIES.md` sections 7 and 8, branch `claude/hulk-zombie`).

## At a glance
- **Meridian Biocontainment, Ward Zero: where she got out.** A sealed two-storey containment facility round the **Breach Atrium**, a 24 x 20 m void through both floors with the **Cradle** (the Siren's shattered vessel) at its middle under the **Spine** (the hoist shaft and its hatch in the roof). Cell blocks, observation cells, a morgue, intake, specimen halls, incinerator lanes, decon locks and control rooms ring it.
- **A lot bigger:** an 80 x 64 m slab, 76 x 60 m play space on **two full floors** (ground y 0, upper y 6, roof 13): about **2,930 m² walkable on the ground and 2,100 m² upstairs** (today's hall is 64 x 56 on one floor). Every walkable cell of both floors is reached from the spawns (static flood on 0.5 m cells with the pilot capsule).
- **Floors joined** by six stairs (26.6 degrees, collision as a closed wedge like Astra's Cut fix), two 24-degree gurney ramps, twelve drop-downs and the open atrium. Every route climbs on its own.
- **PvE entrances, all data:** 53 entries over both floors: 10 floor vents (the 8 song vents keep indices 0..7), 8 wall grilles, 4 ceiling vents, 8 cell gates, 12 doors, 3 drop hatches (one Hulk-sized over the Cradle) and 8 breach panels for the Hulk.
- **Five hazards, telegraphed, pure functions of (round, FightAt, time):** live grating bridges (the existing `grid` kind), incinerator burners, decon gas locks, blast shutters at the atrium portals, and today's song vents (pits) carried over.
- **PvP and the Sandbox** keep working on the same map: 8 FFA and two 4-point team spawns, all 28 FFA and 16 team pairs blocked under +-1 m jitter, the closest same-floor spawn to a hazard 6.6 m.
- **Presentation built and dormant** until the import: the `quarantine` look, six ward surfaces in the shared block shader bank, a 7,124-part dressing kit (4,817 on the low tier), eight practical lights picked per camera from 189 candidates, a music-synced mood (calm, horde, boss, emergency) with the Siren's ink spreading, haze, shafts, steam, sparks, drips, every hazard telegraph and every entrance animation. Renders: `.../scratchpad/renders-wardzero/`.

---

## 1. Identity
Meridian Biocontainment kept one thing in Ward Zero. The Cradle cracked from the inside; the containment fluid is on the floor and something violet is growing out of it. The ward still runs on its own: fluorescents hum and fail, the incinerators cycle, the decon locks purge, the blast shutters drill. Clinical white tile and painted steel, teal containment trim, green fluid in the specimen tanks, and red when the alarms go.

**The rules to learn:** red strobes and a klaxon mean a shutter is coming down; a grate that glows orange will burn; amber beacons on a lock mean gas; a bridge whose rails crawl with light will shock; an open floor vent is a pit. Cyan arrows lead up, warm arrows lead down.

## 2. Footprint and heights
- **Slab** 80 x 64 m (x +-40, z +-32); a 2 m shell to the vault on all sides; the vault (13.0..13.6) is solid everywhere: no jump or launch leaves the building.
- **Play space** 76 x 60 m (x +-38, z +-30). Ground floor y 0; upper floor top y 6.0 (decks 5.6..6.0, grating 5.7..6.0); roof underside 13.0.
- **Doorways** 3.4 m high (the Hulk stands about 3.2 m), 2.4 m wide or more; atrium portals 6 m wide and 4.5 m high; rails 1.1 m (a top bar and a kick plate, see-through).
- **Limits** (proposed): bounds x +-38, z +-30, ceiling 13.0, killY -6 (there is no void; it is a safety net under the vent pits).
- **Point symmetric** (x, z) -> (-x, -z) except the Cradle drop (unmirrored, at the centre). Mirror ids swap n/s and e/w.

## 3. Plans
From `python3 docs/arenas/scripts/wardzero_m1.py --ascii` (1 char = 1 m of x and 2 m of z; north up). The PNG of both floors is `--png` (`renders-wardzero/00-plan-both-floors.png`).
Legend: `#` shell, `H` walls, `.` floor, `/` stairs and ramps (`////` in the upper plan: a stair or ramp below rising to it), `C` cell rows (solid), `K` solid fittings (booth, chambers, lockers, racks), `o` low cover (gurneys, carts, benches, slabs, consoles), `O` specimen tanks, `s` triage screens, `B` breach panel, `G` gallery deck, `D` upper deck, `g` live grating bridge, `T` gantry, `P` platform or bridge, `r` rails, `~` burner rows, `F` furnace, `%` decon lock, `=` blast shutter, `X` drop hatch landing. Entries: `v` floor vent, `w` wall grille, `u` ceiling vent, `c` cell gate, `d` door, `b` breach. Machines `$` chips, `R` recruits, `+` med; `p` pads; spawns `1`-`4` team A, `5`-`8` team B, other digits the FFA points.

**Ground floor (y 0)**
```
          x=-40                                  x=0                                   x=+39
 +30..+32 ################################################################################
 +28..+30 ##....////////////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCH.....////////////....##
 +26..+28 ##....////////////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCHKKK..////////////....##
 +24..+26 ##w......KK............HCCcCCCCCCCcCCCCCCCCCCCcCCCbCCCcCHKKK....KKKK.......7..##
 +22..+24 ##KK.1......oo.........H.............KKKKKK.............H....8........ooo.....##
 +20..+22 ##KK..oo.........ooo...H...ooo.......KKKKKK.............H............6......5.##
 +18..+20 ##HHHHHHHH..HHHHRHH..HHHHHHHHHH..HHHHHHHHHHHHHH..HHHHHHHHHH..HHHHHHH..HHHHHHHH##
 +16..+18 ##.d.........ooo...............v....................o...........u..p........d.##
 +14..+16 ##HHH...HHHHHHHHHHHH..HHHHH..HHHHHHHH======HHHHHHHH..HHHHH..HHHHHHH..HHH...HHH##
 +12..+14 ##B......H.............H.....ooo........pooo..ss........H.............H.......##
 +10..+12 #bB......H$ooo.....v.........ooo..........................OOO...7..OO.H.......##
  +8..+10 ##~~~~~~~~....////.....H....///.........................H.............~~~~~~~~##
  +6.. +8 ##.......H....////.OOO.H.oo.///.....v......v............H.............H.......##
  +4.. +6 ##.......H....////.....H.oo.///......cc.ccc.............H.....////....H.......##
  +2.. +4 ##~~~~~~~~.OO.////..o..=....///..................///....=..o..////...w~~~~~~~~##
  +0.. +2 ##F......H....////..o..=....///...cc........cc...///....=..o..////....H......F##
  -2.. +0 ##F......H....////..o..=....///...cc....X...cc...///....=..o..////....H......F##
  -4.. -2 ##~~~~~~~~w...////..o..=....///..................///....=..o..////.OO.~~~~~~~~##
  -6.. -4 ##.......H....////.....H.............ccc.cc......///.oo.H.....////....H.......##
  -8.. -6 ##.......H.............H............v......v.....///.oo.H.OOO.////....H.......##
 -10.. -8 ##~~~~~~~~.............H.........................///....H.....////....~~~~~~~~##
 -12..-10 ##.......H.OO...8..OOO..........................ooo...............ooo$H......B##
 -14..-12 ##.......H.............H........ss..ooo.p.......ooo.....H....v........H......B#b
 -16..-14 ##HHH...HHH..HHHHHHH..HHHHH..HHHHHHHH======HHHHHHHH..HHHHH..HHHHHHHHHHHH...HHH##
 -18..-16 ##.d.........p..u..........o.....................v..............ooo.........d.##
 -20..-18 ##HHHHHHHH..HHHHHHH..HHHHHHHHHH..HHHHHHHHHHHHHH..HHHHHHHHHH..HHHRHHH..HHHHHHHH##
 -22..-20 ##.1......2............H.............KKKKKK.......ooo...H...ooo.........oo..KK##
 -24..-22 ##.....ooo........4....H.............KKKKKK.............H.........oo......2.KK##
 -26..-24 ##..........KKKK....KKKHCCcCCCCCCCcCCCCCCCCCCCcCCCCCCCcCH............KK......w##
 -28..-26 ##..3.////////////..KKKHCCCCCCbCCCCCCCCCCCCCCCCCCCCCCCCCH.....////////////....##
 -30..-28 ##....////////////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCH.....////////////....##
 -32..-30 ################################################################################
```
**Upper floor (y 6)**
```
          x=-40                                  x=0                                   x=+39
 +30..+32 ################################################################################
 +28..+30 ##....      //////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCH.....//////      ....##
 +26..+28 ##....rrrrrr//////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCH.....//////rrrrrr....##
 +24..+26 ##HH..HHHHH%%HHH.......HCCdCCCbCCCCCCCdCCCCCCCdCCCCCCCdCH................oooow##
 +22..+24 ##KK....H%%%%%%H.......H................X....ooo........H....................K##
 +20..+22 ##KK....H%%%%%%H.......H.....................ooo........H...ooo....oooo....5.K##
 +18..+20 ##HHHHHHHHH%%HHHHHHH..HHHHwH$HH..HHHHHHHHHHHHHH..HHHHHHHHH..HHHHHHH..HHHHHHHHH##
 +16..+18 ##............p...u...............................v...........................##
 +14..+16 ##...HHHHHHHHHHHHHHHHHHH................................HHHHHHHHoHHHHHHHHHH...##
 +12..+14 ##....                 H+...............................H                 ....##
 +10..+12 ##....                 H................................H                 ....##
  +8..+10 ##....                 H....    gggg        gggg    ....H                 ....##
  +6.. +8 ##....                 H....    gggg        gggg   .....H.....................##
  +4.. +6 ##....                 H....    gggg        gggg .......H.....................##
  +2.. +4 ##....        ////     H..../// gggg        gggg ///....H     ////        ...B##
  +0.. +2 ##....        ////     H..../// gggg        gggg ///....H     ////        ...B#b
  -2.. +0 ##B...        ////     H.p../// gggg        gggg ///..p.H     ////        ....##
  -4.. -2 #bB...        ////     H..../// gggg        gggg ///....H     ////        ....##
  -6.. -4 ##.....................H....... gggg        gggg    ....H                 ....##
  -8.. -6 ##.....................H.....   gggg        gggg    ....H                 ....##
 -10.. -8 ##....                 H....    gggg        gggg    ....H                 ....##
 -12..-10 ##....                 H................................H                 ....##
 -14..-12 ##....                 H...............................+H                 ....##
 -16..-14 ##...HHHHHHHHHHoHHHHHHHH................................HHHHHHHHHHHHHHHHHHH...##
 -18..-16 ##............................v...............................u...p...........##
 -20..-18 ##HHHHHHHHH..HHHHHHH..HHHHHHHHH..HHHHHHHHHHHHHH..HHH$HwHHH..HHHHHHH%%HHHHHHHHH##
 -22..-20 ##K..6...oooo....ooo...H........ooo.....................H.......H%%%%%%H....KK##
 -24..-22 ##K....................H........ooo.....X...............H.......H%%%%%%H....KK##
 -26..-24 ##.oooo................HCCdCCCCCCCdCCCCCCCdCCCCCCCCCCCdCH.......HHH%%HHHHH..HH##
 -28..-26 ##w...rrrrrr//////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCbCCCCCH.....//////rrrrrr....##
 -30..-28 ##....      //////.....HCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCH.....//////      ....##
 -32..-30 ################################################################################
```

## 4. Sections
```
SECTION A-A  x = 0.5, looking west (S left, N right; 1 column = 1 m of z, 1 row = 0.6 m):
 14.4
 13.8 ##VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV##
 13.2 ##VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV##
 12.6 ##CCCCC     H                                      H     CCCCC##
 12.0 ##CCCCC     H                                      H     CCCCC##
 11.4 ##CCCCC     H                                      H     CCCCC##
 10.8 ##CCCCC     H                                      H     CCCCC##
 10.2 ##CCCCC     H                                      H     CCCCC##
  9.6 ##CCCCC     H                                      H     CCCCC##
  9.0 ##CCCCC     H                                      H     CCCCC##
  8.4 ##CCCCC     H                                      H     CCCCC##
  7.8 ##CCCCC     H                                      H     CCCCC##
  7.2 ##CCCCC     H                                      H     CCCCC##
  6.6 ##CCCCC     H                                      H     CCCCC##
  6.0 ##CCCCCDDDDDHDDDDHGGGG                    GGGGHDDDDHDDDDDCCCCC##
  5.4 ##CCCCC     H    H                            H    H     CCCCC##
  4.8 ##CCCCC     H    H                            H    H     CCCCC##
  4.2 ##CCCCC     H                                      H     CCCCC##
  3.6 ##CCCCC     H                                      H     CCCCC##
  3.0 ##CCCCC     H                                      H     CCCCC##
  2.4 ##CCCCC KKKKH                                      HKKKK CCCCC##
  1.8 ##CCCCC KKKKH                                      HKKKK CCCCC##
  1.2 ##CCCCC KKKKH                                      HKKKK CCCCC##
  0.6 ##CCCCC KKKKH                                      HKKKK CCCCC##
  0.0 ##CCCCC.KKKKH......................................HKKKK.CCCCC##
 -0.6
SECTION B-B  z = 5.5, looking north (W left, E right; 1 column = 1 m of x):
 14.4
 13.8 ##VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV##
 13.2 ##VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV##
 12.6 ##                     H                                H                     ##
 12.0 ##                     H                                H                     ##
 11.4 ##                     H                                H                     ##
 10.8 ##                     H                                H                     ##
 10.2 ##                     H                                H                     ##
  9.6 ##                     H                                H                     ##
  9.0 ##                                                                            ##
  8.4 ##                                                                            ##
  7.8 ##                                                                            ##
  7.2 ##                     H   r    r  r        r  r    r                         ##
  6.6 ##            ////     H   r    r  r        r  r    r                         ##
  6.0 ##TTTT        ////     HGGGG    gggg        gggg    GGGGHPPPPPPPPPPPPPPPPPTTTT##
  5.4 ##            ////     H                                H                     ##
  4.8 ##       H    ////     H                                H             H       ##
  4.2 ##       H    ////     H                                H             H       ##
  3.6 ##       H    ////     H                                H             H       ##
  3.0 ##            ////     H                                H                     ##
  2.4 ##            ////     H    ///                      s  H                     ##
  1.8 ##            ////     H    ///                      s  H                     ##
  1.2 ##            ////     H oo ///                      s  H                     ##
  0.6 ##            ////     H oo ///                      s  H                     ##
  0.0 ##............////.....H.oo.///......................s..H.....................##
 -0.6
```

## 5. Zones (mirror pairs together)
| zone | where | floor | what it is for |
|---|---|---|---|
| Breach Atrium and the Cradle | x +-12, z +-10 void inside walls at x +-16, z +-14 | both | the centre: the Cradle (six vessel shards on a 5 m ring round a clear 4.7 m disc), four song vents, two grand stairs, the arcade under the gallery, four portals with blast shutters and corner doors |
| Gallery ring | x 12..16.4 and z 10..14.4 round the void | upper | the balcony over the fight: rails with drop gaps, two live grating bridges (x +-4.5..7.5) across the void, med machines |
| North and South Halls | z 14.4..19 (mirror) | ground | the long corridors joining wings and atrium; floor vents, a ceiling vent, cover gurneys, ward doors at the ends |
| North and South Concourses | z 14.4..19 (mirror) | upper | the upper corridors over the halls, open to the gallery; floor and wall vents, chip machines |
| Cell Blocks | z 19.4..25 aisle, rows to z 30 | ground | isolation cells (8 cell gates as entries), the nurse booth, a breach panel |
| Observation Cells | over the cell blocks | upper | observation doors (8 entries), the roof hatches (brute drops), a breach panel |
| Morgue NW / SE | x -38..-16.4 (mirror) | ground | slabs and cold chambers; FFA spawns; a wall grille |
| Intake NE / SW | x 16.4..38 (mirror) | ground | team spawns (B north-east, A south-west), lockers, showers |
| Decon Wing NW / SE | x -38..-16.4 (mirror) | upper | the decon lock (gas) with a bypass vestibule, the specimen freezers |
| Control Rooms NE / SW | x 16.4..38 (mirror) | upper | consoles and racks, FFA spawns, a wall grille |
| Specimen Halls E / W | x 16.4..30, z +-14 (mirror) | double height | tanks, lab benches, the gurney ramp up to the specimen bridge, floor and wall vents |
| Incinerator Lanes E / W | x 30.6..38 (mirror) | ground | burner rows, the furnace mouth, a breach panel; under the gantry |
| Gantries E / W | x 34.4..38 over the lanes | upper | the long flank walk, drop gaps to the lane, a breach panel, the link to the specimen bridge |
| Corner stairs | x +-22..34, z +-27.4..30 | both | four straight flights against the shell (intake and morgue to control and decon) |

## 6. Floor connections
- **Six stairs, 26.6 degrees** (0.25 m risers, 0.5 m treads; 24 treads for 6 m): the grand stairs in the atrium (3.0 m wide, x 9..12, z -9 -> 3 to a landing on the east gallery, and the point mirror) and four corner stairs (2.6 m wide, 12 m run, against the north and south shells).
- **Two gurney ramps, 24 degrees** (3.0 m wide, 13.5 m run) in the specimen halls up to the specimen bridge platform (z 4..7.5, upper floor), with stepped skirts under the deck.
- **Collision (Astra's Cut precedent, `NativeCyberCityStairs`):** the treads and the pitched ramp deck are visual blocks; each route's collision is one closed invisible wedge from its foot to its top (the draft's `ramps[]`: `low`, `high`, `width`, `bottom` and the visual block ids, all in `navHints.collisionOff`). A 0.5 m tread is shorter than the 0.84 m capsule, so the wedge is required, not optional. The contract asks for the native traversal gate across both floors (host and guest walk and sprint, landing and mid-flight starts at centre, left and right, the bot path) on all eight routes.
- **Twelve drop-downs** (one-way nav links in `navHints.drops`): the gallery rail gaps (N, S, E, W), an inboard gap on each grating bridge, the specimen bridge gaps and two gaps on each gantry. A fall of 6 m (today's fall rules apply; nothing here kills by falling).
- **The open atrium**: both floors see the Cradle; the gallery overlooks the fight; the grand stairs rise inside the void.

## 7. Flow, lanes, sightlines and cover
- **Rings and spokes.** Ground: the atrium arcade, the two halls, the specimen halls and lanes form a ring; four portals and four corner doors cut into the atrium. Upper: the gallery ring, the concourses and the gantries form a second ring; the bridges cut across the void. Stairs and ramps join the rings at eight points, drops at twelve.
- **Long lanes, broken:** the halls (76 m) are broken by doors, cover gurneys and carts and the nurse booths; the gantries (28 m) by the link and the drop gaps; the atrium by the shards, the stairs and the triage screens. The upper atrium walls have only two observation windows each (a third was closed for the spawn sightline check).
- **Cover** is low and metal (gurneys, carts, consoles, benches at 1.0 m or lower) with tall solids at the nurse booths, chambers, lockers, racks, tanks and shards. Twelve cover blocks are destructible props (`role: cover`, hp 60).
- **Sightlines:** every FFA pair and every cross-team pair is blocked under +-1 m jitter at three eye heights (the Brickfall test): 28 of 28 and 16 of 16.

## 8. The Cradle: the Siren's arena centre
- The Cradle stands at the origin: six vessel shards (2.0..2.6 m wide, 1.3..2.4 m tall, 0.5 m thick) on a 5 m ring round a **clear disc of 4.7 m radius**. The boss spawns at (0, 0, 0) facing 180 (data, `pve.boss`).
- **The Spine** over it: the hoist shaft in the roof with the big drop hatch (5.2 m, the Hulk fits) and the Spine light that pumps on the boss song's beat.
- **Siren phases:** phases 1-2 read as the boss mood (low white light, violet ink rising round the Cradle); phases 3-4 and the "Find me..." possession hunt read as emergency (red strobes, heavy flicker, haze). The ink spreads outward over both floors with her phase.
- **Possession hiding spots on both floors** (`pve.hideSpots`, 12): the far corners of the morgues and intakes, the control rooms and decon wings upstairs, the lanes and gantries, under the grand stairs and the observation aisles. The possessed add prefers to drift there.

## 9. Machines and pads
- Chips (`chip-0..3`): the specimen hall walls (+-29.45, 0, -+11.6) and the north and south concourses (-+12, 6, +-18.45). Recruits (`chip-4/5`): the halls (-+24, 0, +-18.45). Med (`med-0/1`): the east and west galleries (+-15.45, 6, -+12.2). Every front, dispense point and chip drop is on reached floor; the machine blocks are in the draft's `machineBlocks`.
- Eight pads, four per floor: the atrium arcade (0, 0, +-12.3), the galleries (+-14.2, 6, 0), the halls (+-27, 0, +-16.7) and the concourses (+-26, 6, -+16.7).

## 10. Spawns: PvP, Sandbox and fairness
- **Teams:** B in the NE Intake ((36.2, 21), (29.5, 21.4), (35.2, 26), (21.5, 23.6), ground), A in the SW Intake (mirror). Minimum A-B spacing 63.9 m.
- **FFA:** both morgues, both intakes, both control rooms (upper), both specimen halls: minimum spacing 16.0 m on one floor (6.1 m across floors through a deck). Spawn y = floor + 1.1; yaw toward the Cradle, as today.
- **Clearances:** the closest same-floor spawn to a hazard volume is 6.6 m (the east burner b row from (24, 0, 10.6)); the closest spawn to a touchable block 1.6 m; all spawns inside the bounds less 1.5 m.
- **Sandbox:** the same map with the hazards on their clocks and every entry kind available to the Dev Sandbox's spawn tools (the presentation's entrance animations read the entry, so a sandbox spawn at any entry looks right).

## 11. PvE spawning: entries on both floors
All entries are data (`pve.entries` in the draft, PveContent.json's `entries` after the import); each carries `e` (its index), `id`, `kind`, `surface`, `from`, `exit` (both with y), `yaw`, `level` (0 ground, 1 upper), and kind fields. **No coordinates in code** (the presentation reads them; Astra's rules should too).

| kind | count | ground / upper | what happens |
|---|---|---|---|
| `vent` / `floor` | 10 | 8 / 2 | a grate lid slides open and the add climbs out (today's vent climb). Indices 0..7 are today's eight **song vents** (`song` side 0/1, `pit`: they open into the shafts players fall into), round the Cradle, in the halls and the specimen halls. The two upper ones are duct grates (no pit, no song). |
| `vent` / `wall` | 8 | 4 / 4 | a grille 0.5 m up a wall swings open, the add crawls out to a point 1.4 m in front. |
| `vent` / `ceiling` | 4 | 2 / 2 | a ceiling grille drops open, the add drops `height` m to the floor (5.6 under a deck, 7 under the roof). |
| `cell` | 8 | 8 / 0 | a cell gate rises, the add walks out of the cell row. |
| `door` | 12 | 4 / 8 | a door's leaves slide open (the halls' ward doors in the shell, the observation cell doors). |
| `drop` | 3 | 1 / 2 | the brute drop (`NativeHordeDrop`): a roof hatch opens and the add falls `height` m to a landing with `clear` m of clear floor. The **Cradle drop** (13 m, clear 4.5 m, `hulk: true`) takes the Hulk; the observation hatches (7 m, clear 2.8 m) take brutes. |
| `breach` | 8 | 4 / 4 | the Hulk breaks a reinforced panel (section 11.6). |

### 11.1 The song vents (today's eight, kept)
Indices 0..7 stay the music-synced floor vents with today's two sides (`song` 0 or 1) and today's PvP dive schedule, so `NativeBossTrack.Vent` and `NativeVentRules.Ward` keep working; their positions move from `NativeArena.StageVents` (hard-coded today) into the data (`from`). Falling in an open one is today's vent death.

### 11.2 Opening and closing
Every entrance shows its open amount (`NativeVentRules.Open` on the replicated entry view, as today): vent lids slide, grilles swing, cell gates rise, doors slide, hatches part; a lamp blinks while it opens; light and dust spill out of the opening; a floor vent's rim glows and dust lifts; a drop hatch shows a warning shaft and falling grit. The climb, walk-in and drop animations of the adds are today's (`NativeHordeKindPresentation`: vent climb, door walk, drop), read from the entry kind.

### 11.3 Drops
`drop-cradle` (index 36): landing (0, 0, 0), 13 m from the Spine hatch, 4.5 m clear (the Hulk's shockwave; the shards stand at 5 m). `drop-obs-n/s`: (0, 6, +-22.2) on the observation aisles, 7 m, 2.8 m clear. The landing room is checked against every block (nearest obstruction 4.5 and 2.8 m).

### 11.4 Floors
31 entries open on the ground floor, 22 on the upper floor. Every exit stands on reached floor at its own level.

### 11.5 The Hulk's drop
The Cradle drop is the Hulk's: the hatch is 5.2 m across (1.6-2x a brute's), the landing disc 4.5 m clear for the big shockwave (the zombie lane's landing effect), and the boss disc keeps 4.7 m.

### 11.6 Breach panels (the Hulk's way in through the walls)
Eight reinforced containment panels, 3.2 m wide, 4.0 m tall, 0.4 m thick, each a distinct block (`siren-breach-*`) standing proud of a solid face, so a broken panel opens onto a dark recess nobody can enter (no trap on restore): `breach-cells-n/s` (the ground cell rows, x +-10), `breach-obs-n/s` (the observation rows, x -+10, upper), `breach-lane-e/w` (the shell in the incinerator lanes, z -+12, ground), `breach-gantry-e/w` (the shell on the gantries, z +-2, upper). Each breach entry names its panel (`block`), the panel's box (`center`, `size`) and its outward `normal`; `from` is 1 m inside the face (behind the wall), `exit` 2.4 m in front. They read as weaker panels: a hazard-striped frame, a dented inner plate with braces and rivets, a stencil plate and a slow red lamp. Collision goes off at the break and returns after the holographic rebuild (`docs/SIREN-ZOMBIES.md` section 8: `breakAt`, `restoreAt`, solid at `restoreAt + 2.6`); the burst and the hologram restore are the zombie lane's.

## 12. Hazards
All timings are a pure function of (round index, FightAt, time) in Fight and Result only (as Cyber City's); even rounds shift each clock by its `offset`. Numbers are proposals for Astra to tune; the generator prints the timing table and the managed checks pin the same table.

### 12.1 Live grating bridges (`siren-grid-e`, `-w`; the existing `grid` kind)
- **Where:** the two 3 x 20 m bridges across the void at the upper floor (x +-4.5..7.5).
- **Cycle:** 16 s: idle 10.5, charging 2.5 (telegraph), live 2, discharge 1. East first warns at fight + 18.5 s, west 8 s later; the bridges are never live together (checked over two rounds).
- **Telegraph:** the rails' light strips crawl and brighten, the pylon lamps go amber and blink, arcs and sparks build on the grating, an electric hum.
- **Danger:** 13 damage every 0.25 s on the grating while live (Cyber City's coupling numbers).
- **Safe:** the gallery at either end and the inboard drop gap; the farthest point on a bridge is 4.4 m from safety (0.65 s at 6.8 m/s against the 2.5 s charge).
- **Counterplay:** cross on the other bridge, drop through the gap, or use it to cut a chaser off.

### 12.2 Incinerator burners (`siren-burner-e-a/-b`, `-w-a/-b`)
- **Where:** the incinerator lanes (x 30.6..38): four 2 m rows across each lane at z +-3 and +-9, on two clocks (a: rows -9 and 3; b: rows -3 and 9 in the east, mirrored in the west).
- **Cycle:** 10 s: warn 1.5, burn 2.5, clear 1, idle 5. Clock a first warns at fight + 6 s, clock b 5 s later; the two clocks never overlap.
- **Telegraph:** pilot flames along the row, the grate heats from orange to yellow, heat shimmer, the furnace mouth roars.
- **Danger:** 10 damage every 0.25 s inside the burning row (a burn, not an instant kill).
- **Safe pockets:** the 4 m strips between the rows (z -+12.5, -+6, 0); the farthest point inside a row is 1.0 m from its edge (0.15 s).
- **Counterplay:** time the rows, wait in a pocket, take the gantry above, or hold the lane while the other clock burns.

### 12.3 Decon gas locks (`siren-gas-nw`, `-se`)
- **Where:** the decon lock on the upper floor of each decon wing (7.2 x 5.6 m between the concourse and the corner stair hall).
- **Cycle:** 24 s: warn 2.5, gas 4, clearing 3, idle 14.5. NW first warns at fight + 15 s, SE at fight + 27 s.
- **Telegraph:** amber beacons over both doors, "DECON" lit on the lock, nozzles hiss; then green gas jets fill the room and drift out of the doors while it clears.
- **Danger:** 6 damage every 0.5 s inside the lock while gassing (area denial, not a kill).
- **Safe:** step out of either door (the farthest point is 4.3 m away, 0.63 s against the 2.5 s warning); the vestibule door to the east bypasses the lock.
- **Counterplay:** go round through the vestibule, or bait a chaser into the purge.

### 12.4 Blast shutters (`siren-shutter-n/-s`, `-e/-w`)
- **Where:** the four 6 x 4.5 m atrium portals, in pairs (north and south, east and west).
- **Cycle:** 36 s: warn 3, closing 1.2 (danger), shut 9, opening 1.5, idle 21.3. North and south first warn at fight + 20 s, east and west at fight + 38 s; at most two of four portals are ever shut, and the corner doors beside each portal never shut.
- **Telegraph:** rotating amber beacons over the portal, a klaxon (the ward's mood takes a red share), striped thresholds; the slats come down from the lintel; sparks along the closing edge; a red line under a shut portal.
- **Danger:** the closing edge pushes a body out of the portal (`push` 6 m/s); a shut shutter blocks movement and sight (no damage proposed).
- **Counterplay:** the corner doors, the stairs to the gallery, the other axis; use the closure to split a fight.

### 12.5 Song vents (today's, carried over)
- **Where:** the eight pit floor vents (indices 0..7: four round the Cradle, two in the halls, two in the specimen halls).
- **Cycle:** PvP: today's dive schedule (`NativeVentRules.Ward`, every 12 s for 4.5 s, staggered 1.5 s). PvE boss: today's song schedule (`NativeBossTrack.Vent`: the lift and climax sections, one side per bar).
- **Telegraph:** the rim fills with light and black wisps rise for the half second before it opens (today's), the lid rattles; on the song, the vents erupt on the downbeat.
- **Danger:** falling into an open vent is today's vent death (`vent-fall`).
- **Counterplay:** read the rim, keep off the grates on the lift and the climax.

## 13. Presentation (on the branch, dormant until the import)
- **Look `quarantine`** (`MapLooks.json` entry `siren-quarantine`, matched by the map's `look` field, so today's Ward Zero keeps `ward-zero`): cold fluorescent key from above without shadows (indoors the vault would shadow everything), a low trilight ambient, linear fog 10..110 m, low haze to 3.4 m.
- **Surfaces:** six ward slots in the block shader's shared bank (beside Cyber City's): clinical tile (30 cm joints, scuffs, stains), wall panels (seams, rivets, streaks), the burner grate (heats orange, then yellow-white), specimen glass (lit fluid, meniscus, bubbles), the troffer diffuser, the ink veins.
- **Lighting on both floors:** troffers under every deck and over the upper floor, high-bays over the double-height halls and the atrium, the Spine light, tanks, furnaces, emergency beacons, alarms; the eight practical light slots go to the fixtures that light the camera's surroundings, eased so a change never pops.
- **Mood (music synced, upgraded):** calm (clinical white, a few failing tubes), horde (cooler, dimmer, more failures, haze thickening with the wave), boss (low white, violet ink light rising with her phase, the lights pump on the beat), emergency (red strobes, heavy flicker); a hazard alarm takes a share of the room; the boss song's sections shape the pulse as today's hall does.
- **Siren-ink corruption** spreads over the fight: in PvE by wave and then by boss phase (more while she hides), in PvP over the first 150 s; veins on walls and floors, pools round the Cradle, its breath (wisps and rings on the beat).
- **Dressing kit:** cells and gates, observation doors, the nurse booth, morgue drawers and slabs, intake lockers and showers, lab benches and screens, tanks, gurneys and carts, consoles and racks, pipes and cable trays, signage (room names on the door lintels, WARD 0, OBSERVATION, ISOLATION), floor markings, atrium railings and nosings, grating, vents, doors and hatches that open, breach panels.
- **Effects:** light halos, floor pools, shafts under the high-bays and down the Spine, dust motes, steam leaks, sparks from failing fixtures, drips with splash rings, the ink's breath, low haze; every hazard telegraph and every entrance; route guides (light columns and pools at every foot and top, UP and DOWN arrows, chasing stair nosings).
- **Tiers:** light and life scale with the effects quality and the low tier; telegraphs, entrances and route guides never do (ARENA_PROPOSAL.md section 2.1 rule 4); the low tier drops the kit's detail parts (lettering, rivets, posts, paint lines, ink).
- **Budget:** the kit in 15 instanced draws (500 a draw), the effects in 2 (at most 4,200 quads; a busy frame about 800); no shadow casters in the kit; no allocations per frame after the builds (checked).
- **Route readability:** cyan UP arrows at every foot, warm DOWN arrows at every top, chasing nosings, the gallery's edge LEDs, room signs over doors, hazard stripes on thresholds and decks, lit exits.

## 14. Fairness and checks (generator output)
- Ground 11,738 standable cells (2,934 m²), upper 8,394 (2,098 m²), all reached from the spawns; 8 of 8 routes climb on their own; 53 of 53 entry exits, 8 machine fronts, 4 chip drops, 8 pads, 12 hide spots, the boss spawn, 12 drop hint ends and 20 safe pockets on reached floor at their level; entry ids unique.
- FFA 28 of 28 and teams 16 of 16 pairs blocked; spacing 16.0 m (one floor), A-B 63.9 m; closest hazard 6.6 m; spawn clearance 1.6 m.
- At most 2 of 4 portals shut; burner clocks never overlap; bridges never live together (both rounds).
- 616 blocks (604 static, 12 cover), ids unique and clear of today's Ward Zero ids; 438 collide as authored, 178 visual-only (treads, ramp decks and skirts; collision by wedge); 8 rotated (shards and ramp decks).
- Reruns are byte-identical.

## 15. Status, work split and risks
- **Claude (this branch):** this design, the draft and its checks, the contract and prompt, and the presentation (dormant; switches on when the map is `siren` with look `quarantine`). Today's Ward Zero, its presentation and its behaviour are unchanged.
- **Astra:** the import (replace the `siren` map in place, keep id and name), the wedge collision and traversal gate on both floors, the entry kinds and fields, the hazards, nav on two floors, machines and pads, PvP, Sandbox and tests (the contract).
- **Zombie lane:** the Hulk, its landing effect, the breach burst and the holographic restore; it calls `ArenaPresentation.SetWardBreach(entryId, open)` so the panel's dressing and block hide while broken.
- **Risks:** no PhysX traversal, Windows, gameplay or multiplayer run exists; the stairs need the wedges (treads shorter than the capsule); the two-floor nav is new; the gas and burner damage kinds are new; the upper floor's lighting needs an in-engine look; breach collision timing must agree with the zombie lane's contract.

## Appendix: draft files
- `docs/arenas/scripts/wardzero_m1.py` (generator; `--json`, `--png`, `--ascii`, `--sections`)
- `docs/arenas/WARDZERO-SIMULATION-CONTRACT.md` (contract and Astra prompt)
- `unity/ChipArenaConversion/Tests/wardzero-draft.json` (the frozen draft the managed checks read; `Tests/test_wardzero_draft.py` keeps it equal to the generator's output)
- Core: `NativeWardZeroLooks`, `NativeWardZeroHazards`, `NativeWardZeroMood`, `NativeWardZeroKit`, `NativeWardZeroLights`, `NativeWardZeroFx`; Runtime: `NativeWardZeroPresentation`; shader bank in `Resources/NativeBlockLit.shader`; look in `Assets/Conversion/Resources/MapLooks.json`; checks `Tests/WardZeroChecks.cs`.
