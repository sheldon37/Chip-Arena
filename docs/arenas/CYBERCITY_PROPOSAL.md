# Chip Arena: Cyber City (`cybercity`), a stage with hazards

2026-10-12 · Claude (presentation lane) · for the owner (decides) and Astra (simulation, collision, hazards, spawns, bots, networking, tests)
Base: `f575670` on `claude/sleepy-ritchie-g8ruhv` (= `codex/unity-cloud-current`), Unity port `unity/ChipArenaConversion`.
Status: **design and presentation on the branch `claude/stage-cybercity`; the map is not in the game.** Astra builds the simulation from `docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md`. Layout numbers come from the draft script `docs/arenas/scripts/cybercity_m1.py`; the looks come from offline renders that run the game's own block and sky shader code. Nothing has been run in Unity, on Windows or in multiplayer.

Owner's request (2026-10-12, verbatim): "Finally, add a couple of more stages on the same quality of brick fall. A larger map with stage hazards. One is a factory (with like instant kill buzzsaws or things that can fall on players, the other is a cyber city. I can have astra handle the coding portion.)"

## At a glance
- **Cyber City: Helix Junction.** A rain-soaked neon interchange at 02:00. A sunken maglev cut, **the Helix Line**, splits the district east to west; a station concourse hangs over its central bridge; two three-storey podiums (Nova, NE; Vega, SW) carry 24 m spires; night markets, neon avenues and taxi ranks fill the rest. Megatowers, expressways, flying traffic and searchlights rise beyond the walls.
- **Larger than Brickfall:** an 80 x 64 m slab, 76 x 60 m play space (Brickfall: 60 x 52), with strong verticality: the cut at -4 m, streets at 0, floors at 3.6 and 7.2, the concourse and skybridges at 7.2, roofs at 10.8, spires to 24 (not standable).
- **Hazards, all telegraphed and pure functions of (round, FightAt, time):**
  1. **The Helix Line maglev** (instant kill inside the train's body): a pass every 32 s from fight + 18 s; 6 s amber warning, 3 s red danger with horn, then a 44 m/s, 42 m train. Six alcoves and two stair exits on each wall are safe pockets; no trench point is more than 3 m from one.
  2. **Power couplings** (the two flank bridges over the cut): electrified grating, 2.5 s charging telegraph, 2 s live (13 damage every 0.25 s), one coupling always lags the other by 7 s.
  3. **Falling holo billboards** (player-caused, the §5 segment system): two 14 x 5.4 m screens on yellow-banded struts; blow either strut and the screen swings down across the avenue (1 s telegraph, crush 45, capped like Brickfall).
  4. Optional for G3: a **security sweep** drone over the plaza (designed in §8.4, not in M1).
- **Spawns:** 8 FFA and two 4-point team tables; all 28 FFA pairs and 16 team pairs are blocked under the Brickfall jitter test; the nearest spawn to any hazard volume is 6.0 m.
- **Presentation is built on the branch** and dormant until the map exists: the `neon-rain` look, a Cyber City surface bank for the block shader, a 2,567-part dressing kit, the train rig, a skyline backdrop, rain, steam, wet reflections, neon haze, flying traffic, searchlights, every hazard telegraph, and the sound cues. Offline renders: `.../scratchpad/stage-cybercity/renders/`.

---

## 1. Pitch
02:00 in a megacity ward. Drizzle hangs in the neon. The Helix Line runs through a 6 m cut under the streets; every half minute a maglev screams through it. The **Helix Station** concourse bridges the cut at 7.2 m; from it skybridges reach the **Nova Arcade** (magenta) and the **Vega Arcade** (cyan), three-storey podiums whose glass spires climb out of the play space. North of the cut, the **Lantern Market** sells noodles under strings of paper lanterns and **Neon Avenue** runs past parked hover vans under the **north holo billboard**; south, the **Signal Market** and **Rain Street** mirror them. Two flank bridges, the **West and East Couplings**, are live power grating.

**The rules to learn:** the cut kills when the lights go red; the couplings bite when they glow; yellow-banded struts hold the screens up.

## 2. Footprint
- **Slab:** 80 x 64 m (x ±40, z ±32). An indestructible frontage 2 m deep and 16 m tall closes all four sides (tops not standable). The cut runs through tunnel portals in the west and east frontage.
- **Play space:** 76 x 60 m (x ±38, z ±30), about 4,100 m² of streets and decks plus about 1,600 m² of upper floors, roofs and the concourse. Brickfall walks about 3,600 m².
- **Heights:** cut floor -4.0 (guide beam -3.7); street 0; podium floors 3.6 and 7.2, roofs 10.8 with a 1.0 parapet; concourse and skybridges 7.2 with 1.0 rails; container perch 2.6; spires 24 (tops not standable); frontage 16.
- **Limits:** killY proposed at -8 (the cut floor is at -4); ceiling 24 unchanged (projectiles only). Clamps come from the bounds (Brickfall's rule).
- **Jump reach** (Brickfall §3.2 numbers): a base jump (1.57 m apex) cannot leave the 4 m cut; a rubber build (6.3 m) can. No base build reaches the spire or frontage tops; pilot-only blocking columns cover them, the portal tops, and the tunnel mouths.

## 3. Blockout (top-down)
From `python3 docs/arenas/scripts/cybercity_m1.py --ascii` (1 char = 1 m of x and 2 m of z; north up; point symmetric (x, z) -> (-x, -z)). The PNG version is `--png` (`renders/00-blockout-topdown.png`).
```
          x=-40                                  x=0                                   x=+39
 +30..+32 ################################################################################
 +28..+30 ##...........................ffffffffffffffff...................l.............##
 +26..+28 ##...........................ffffffffffffffff.....tttttt...........ccccc..5...##
 +24..+26 ##.......tttttt........F7.....ffffffffffffffff.....tttttt..kkk......ccccc......##
 +22..+24 ##....l..tttttt.......l......ffffffffffffffff.l...........kkk.......8...ccccc.##
 +20..+22 ##..i....BBBBBBi...KKKKKKK.i....//.........oooHHHuuuHHHHHHHHHHHHH....O..ccccc.##
 +18..+20 ##...F1...BBBBBB....KKKKKKK......//............HHHuuuHHHHHHHHHHHHH.M........7..##
 +16..+18 ##.......BBBBBB..//KKKKKKK.v....//......kkkk..HHHHHHHXXXXXXHHHHHH.M...^^^^^^^^##
 +14..+16 ##.vv......sss...//KKKKKKKss....//......kkkk..HHHHHHHXXXXXXHHHHHH.M.OO^^6^^^^^##
 +12..+14 ##BBBBBs..sss....//..sss....ooo.//............H//HF5HHXXXXXXuuuuHH.M...^^^^^^^^##
 +10..+12 ##BBiBB...sss..i.//........i....//..ooo.......HHHHHHHHHHHHHuuuuHH.oooo........##
  +8..+10 ##..MMMMMMMMMMMMMMMMMMMMMMMMMO..//........O...r=r......pppp.......OOOOO.......##
  +6.. +8 ##.rrrrpppprrrr.....O..pppOOOO.///........O...r=r..rrrrpppprrrr......pppp.....##
  +4.. +6 ##.rrrrrrrraaa^^^^^..aaa.......aaarrrrrrraaarrr=rr.rrrrrrrrrraaa^^^....aaa....##
  +2.. +4 ##~~~~~~~~~aaa^^^^^~~aaa~~~~~~~aaaSSSSSSSaaaSSSSSr~~~~~~~~~~~aaa^^^~~~~aaa~~~~##
  +0.. +2 ##============gggg=============rSSSSSSLLLLSSSSSSSr============gggg============##
  -2.. +0 ##============gggg=============rSSSSSSLLLLSSSSSSSr============gggg============##
  -4.. -2 ##~~~~~aaa~~~^^^^aaarrrrrrrrrr~rSSSSbaaaSSSSSSSaaa~~~~~~~aaa~^^^^^^aaarrrrrrrr##
  -6.. -4 ##................rrrrrrrrrrrr.r==rrrrrrrrrrrrr//r................rrrrrrrrrrrr##
  -8.. -6 ##.....ppOOOOO.......pppp......r==r..O........///.OOOOppp..O.........pppp.....##
 -10.. -8 ##.......OOOOO.................r==r..O........//..OOMMMMMMMMMMMMMMMMMMMMMMMMBB##
 -12..-10 ##..............HuuuuHHHHHHHHHHHHHH......ooo..//.oooi...sss..//.i..sss..sBBiBB##
 -14..-12 ##^^^^^^^^...M..HuuuuXXXXXXHHF6H///H...........//.ooosss.sss..//....sss..sBBBBB##
 -16..-14 ##^^^^^2^^.O.M..HHHHHXXXXXXHHHHH/HH.kkkk......//....sssKKKKKK//...sss......vv.##
 -18..-16 ##^^^^^^^^...M..HHHHHXXXXXXHHHHH/HH.kkkk......//....v..KKKKKK//..BBBBBB.......##
 -20..-18 ##..3........M..HHHHHHHHHHHHuuuHHHH...........//.......KKKKKKK...BBBBBB...F2...##
 -22..-20 ##.ccccc..O.....HHHHHHHHHHHHHHHHHHHoo.........//....i...........iBBBBBB....i..##
 -24..-22 ##.ccccc...4.......kkk............lffffffffffffffff.......l......tttttt...l...##
 -26..-24 ##......ccccc......kkk..tttttt.....ffffffffffffffff.....F8........tttttt.......##
 -28..-26 ##...1..ccccc...........tttttt.....ffffffffffffffff...........................##
 -30..-28 ##.............l...................ffffffffffffffff...........................##
 -32..-30 ################################################################################
```
**Legend** (heights above the street):
- *Perimeter:* `#` frontage, 16 m, indestructible, tops not standable; tunnel portals at x ±38..40 over the cut.
- *The Cut:* `~` cut floor at -4; `=` (rows 0..±2) the guide beam and the street-level bridges (plaza bridge x -7..7); `a` safe alcoves (2.4 x 1.2 x 2.4 m recesses) and stair exits; `|`/`r` the wall band and the pit rails; `/` the four stair pits down to the floor (16 risers of .25 m).
- *Crossings:* `g` the West and East Couplings (electrified grating, x ±22..26); `^` their pylons (3 m) and gate beams (3.0..3.4).
- *Station:* `S` concourse deck at 7.2 (x -9..9, z ±4.5); `L` the lift core on the plaza bridge (holds the deck; 3 x 3 m) and its head; `C` columns; `/` the two stair-bridges through the alleys (open under their high ends); `=` (x ±6..8) skybridges to the podiums' second floors.
- *Podiums:* `H` Nova (NE, x 6..24, z 10..21) and Vega (SW) shells, floors 3.6 / 7.2, roof 10.8; `X` spires (5 x 5 m to 24); `u` rooftop plant (cover); `/` internal stairs (ground to F1 and F2 to roof against the west wall, F1 to F2 beside it).
- *Markets:* `M` the market halls' walls with three arches; `B` noodle bar and game booth; `K` the container perch (2.6, a third box above at 5.2); `s` stalls (wood cover); `v` vending (metal cover); `i` canopy posts (the lattice at 5.4 is presentation only).
- *Avenues and yards:* `t` hover vans (2.6); `k` kiosks; `O` ad pillars, shelters and charging pylons (2.2..2.6); `o` bins, bikes, barriers (low cover); `l` lamp posts; `c` hover cabs (1.6); `^` (yards) the taxi and dock canopies at 3.3..3.6.
- *Collapse zones:* `f` the billboards' painted fell zones (kept free of props).
- *Spawns:* `1-4` Team A (Loading Dock, SW), `5-8` Team B (Cab Rank, NE); `F1..F8` FFA seats.

The draft holds 702 blocks (668 static, 20 cover, 4 structural, 10 carried) and 2 segments; the initial prop frame is 34 blocks.

## 4. Sections
From `python3 docs/arenas/scripts/cybercity_m1.py --sections` (1 column = 1 m, 1 row = 1.2 m).
```
SECTION A-A  x = 7, looking west (S left, N right). The south stair-bridge, the concourse, the NE skybridge into Nova.
 16.8 ##                                                            ##   frontage tops (not standable)
 15.6 ##^^                                                          ##   south billboard frame (beyond the cut, 9.6..15.4)
 12.0 ## ^                                      H         H         ##   Nova parapet
 10.8 ## ^                                      HHHHHHHHHHH         ##   Nova roof
  9.6 ##^^                                        //////  H         ##   Nova F2 -> roof stair
  7.2 ##                     ////SSSSSSSSSS=====HHHHHHHHHHH         ##   concourse 7.2 = skybridge = Nova F2
  6.0 ##                  ////                  H         H         ##
  3.6 ##             ////                       HHHHHHHHHHH         ##   Nova F1
  1.2 ##         /////                          H ////////H         ##   Nova ground -> F1 stair
  0.0 ##........./////...........||======||.....H.////////H.........##   street; plaza bridge over the cut
 -2.4 ............................       ||...........................
 -3.6 ...........................~~~~~~~~~~...........................   cut floor -4, guide beam
      z: -32 Rain St | Signal Market / stair-bridge | prom | CUT | prom | Nova Arcade | Neon Ave +32
SECTION B-B  z = 15.5, looking north (W left, E right). Lantern Market, Kiosk Alley, Nova.
 24.0 ##                                                   XXXXXX                   ##   Nova spire to 24
 10.8 ##                                            H HHHHHXXXXXXHHHHH              ##   Nova roof
  7.2 ##                                            HHHHH HXXXXXXHHHHH              ##   Nova F2 (stair hole)
  3.6 ##                 KKKKKKK      //      kkkk  H HHHHHXXXXXXHHHHH      ^^^^^^^^##   F1; Cab Rank canopy 3.6
  1.2 ## vv      sss   //KKKKKKK              kkkk  H//    XXXXXX       M           ##   container perch 2.6
  0.0 ##.vv......sss...//KKKKKKK..............kkkk..H//....XXXXXX.......M...........##
      x: -40 Lantern Market (booth, stalls, stair, containers) | Kiosk Alley (station stair, kiosk) | Nova | Cab Rank +40
```
Openings are holes in collision (windows 1.6 m tall from a 1.0 m sill, doors 2.7 m); a window, roof edge or bridge side is a free drop (no fall damage), but the cut is a 4 m drop a base jump cannot climb.

## 5. Zones (mirror pairs together)
| Callout | What it is | Plays as |
|---|---|---|
| **The Cut** (Helix Line) | 6 m wide, 4 m deep trench along z = 0; guide beam down the middle; 6 alcoves and 2 stair exits on each wall | A risky low lane and shortcut under every crossing; lethal when the train comes (§8.1) |
| **Helix Plaza bridge** | 14 m wide street-level bridge over the cut (x -7..7), lift core in its middle, planters | The central crossing; the lift blocks every line through the map's centre |
| **Helix Station concourse** | 18 x 9 m deck at 7.2 over the plaza bridge, 1 m rails, lift head; skybridges NE and SW; stair-bridges through the alleys | The central perch over the cut and both promenades; overlooked by both podium roofs |
| **West / East Coupling** | 4 m electrified grating bridges at x ∓24 between gate pylons | Flank crossings on a cycle (§8.2) |
| **North / South Promenade** | 6 m sidewalks along the cut (z ±3..9) with planters, pillars, shelters, the stair pits | Mid-range lanes along the cut, broken every 10-15 m |
| **Nova Arcade / Vega Arcade** | 18 x 11 m podiums, 3 storeys, a 5 x 5 m spire through all floors; lobby, galleries, roof | Lobby close quarters, gallery windows over the promenade and avenue, the 10.8 roof as the power perch (one stair, the spire splits it, parapets) |
| **Lantern Market / Signal Market** | 24 x 11 m halls behind a wall with three arches; noodle bar, game booth, stalls, container perch | Melee and ambush space; the perch (2.6) watches the hall |
| **Kiosk Alley** (N and S) | 16 m alleys between market and podium, the stair-bridge, kiosks, bins | Flank route; the stair up to the concourse |
| **Neon Avenue / Rain Street** | 8 m streets along the frontage: vans, a kiosk, an ad pillar, lamps, the billboard | Outer lanes; the billboard can drop across them |
| **Cab Rank (B) / Loading Dock (A)** | 12 x 21 m spawn yards: canopy, cabs, charging pylons, a wall | Three exits each: avenue, promenade, podium |

## 6. Flow and sightlines
- **Between the yards:** (1) the promenade and a coupling (fast, timed); (2) the promenade, the plaza bridge and the lift's shadow (central, contested); (3) the podium, its skybridge, the concourse and the far skybridge (high route at 7.2); (4) the avenue, an alley and the station stair; (5) down a stair pit and along the cut (a gamble against the Helix Line). Yard to yard is about 80 m: about 12 s walking, 9 s sprinting.
- **Long lines:** the cut floor (80 m) is only visible from inside it and from the bridges; the promenades are broken by pillars, shelters, the station stairs and the pylons every 10-15 m; the avenues by vans, the kiosk and the ad pillar. The lift core blocks every line through the map's centre.
- **Perches:** the podium roofs (eye 12.2) see the promenades, the concourse and their own avenue end, not the far market interiors; the concourse (eye 8.6) sees both promenades and the cut but sits under both roofs; the container perches (eye 4.0) watch their markets.
- **Fit by kit:** ranged on the roofs, concourse and promenades; melee in the markets, lobbies, alleys and the cut; magic at the couplings and the plaza bridge (choke points over the cut).
- **Round arc (base stats, 8 pilots):** 0-15 s the concourse and roofs are contested; first coupling live at about 15 s after the fight starts, first train at 18 s; the billboards come down when someone commits explosives (the struts are explosive-only); overtime from 100 s as everywhere.

## 7. Spawns
Yaw faces the origin (today's behaviour); y = 1.1 on the street.

| Team A, Loading Dock | Position (x, z) | | Team B, Cab Rank | Position (x, z) |
|---|---|---|---|---|
| A1 | (-34.8, -27.2) | | B1 | (34.8, 27.2) |
| A2 | (-32.4, -14.6) | | B2 | (32.4, 14.6) |
| A3 | (-35.6, -18.6) | | B3 | (35.6, 18.6) |
| A4 | (-28.6, -23.2) | | B4 | (28.6, 23.2) |

FFA seat order (spawn `min(seat, 7)`): F1 (-34.5, 19.5) Lantern Market west · F2 (34.5, -19.5) · A1 · B1 · F5 (10.5, 12.5) Nova lobby · F6 (-10.5, -12.5) Vega lobby · F7 (-16.5, 25.5) Neon Avenue · F8 (16.5, -25.5) Rain Street. The two-team tables apply only for exactly two teams of at most four each (Brickfall's rule); otherwise FFA by seat.

**Checks run by the draft script (2D, not Unity):** 28/28 FFA pairs and 16/16 team pairs blocked with ±1 m jitter at both ends and eye heights 1.2/1.6/1.9 (blockers: every element 2 m or taller spanning eye height); min FFA spacing 19.0 m, min A-B 71.1 m; nearest spawn to a hazard or crush volume 6.0 m (F7 to the north billboard's crush box); nearest block a spawn could touch 1.8 m; every spawn within the bounds less 2 m.

## 8. Hazards
All timing is a pure function of (round index, FightAt, match time) and the hazard's definition (`hazards[]` in UnityMaps.json, contract §3). The round index is `NativeStructureFx.RoundIndex(set, round)`. Presentation evaluates the same function every frame; nothing is sent.

### 8.1 The Helix Line maglev (`cybercity-maglev`, instant kill)
| Item | Value |
|---|---|
| Schedule | Pass k's head reaches its near portal at **FightAt + 18 + 32 k**. Direction **west to east when k + round is even**, east to west when odd (each team's end of the cut gets the first pass on alternate rounds). |
| Train | 42 m (three cars), **44 m/s**; the tail clears the far portal (80 + 42) / 44 = 2.77 s after the head arrives. |
| Phases | Warn (amber) from 6 s before arrival; Danger (red) from 3 s before; Active through the pass; Clear 2 s; then Idle. |
| Kill volume | The train's body at that instant, clipped to the portals: x between head and tail, |z| < 2.7, y -4.0..-0.9 (`TrainBox`). Instant kill, `DamageKind=maglev`, kill feed MAGLEV. The alcoves (|z| 3..4.2) and everything above -0.9 (bridge undersides) are outside it. |
| Telegraph (never reduced) | The cut's edge strips and wall wash turn amber then strobe red at 4 Hz; beacons over all 16 pockets spin with red pools on the floor; the coils race from the approaching end; the SAFE lights brighten; the portal signal goes amber then red; the far portal glows as the head nears and its headlight cone reaches out in the last 1.5 s; chime at warn, horn at danger, rush at the pass. |
| Fairness | 6 s of warning; no cut-floor point is more than 3.0 m from a safe pocket on either wall (0.44 s at 6.8 m/s); stair exits at both ends of each wall. Spawns are at least 6 m from the cut. |
| Round 1, fight at 10 s | warn 22, danger 25, head at the east portal 28, clear 30.77 (E->W); then 54/57/60 (W->E), 86/89/92 (E->W). |

### 8.2 Power couplings (`cybercity-grid-w`, `-e`)
| Item | Value |
|---|---|
| Schedule | Cycle k starts at FightAt + first + 14 k (west first = 4, east 11), shifted by `offset` on even rounds (**west +7 s, east -7 s**), so the west leads on odd rounds and the east on even rounds. Each 14 s cycle: Idle 8.5 s, Warn (charging) 2.5 s, Active (live) 2 s, Clear (discharge) 1 s. |
| Damage volume | The deck x ±22..26, z -3..3, from the deck top to 1.0 m above it (a pilot touching the grating). **13 damage every 0.25 s while Active**, on the hazard's own tick clock (8 ticks per live window: a sprinted crossing takes 2-4 ticks, 26-52; standing through the whole window deals 104 and kills a base 100 HP pilot). `DamageKind=grid`, kill feed GRID. Not instant. |
| Telegraph (never reduced) | Charging: the rails crawl brighter from the pylons inward, small arcs at the deck ends, pylon lamps blink amber, a hum; Live: the grating's void glows white-blue (30 Hz broken), 9 arcs across the deck, sparks, lamps red, a crack. |
| Round 1, fight at 10 s | west charge 22.5-25.0, live 25.0-27.0, discharge to 28.0, every 14 s; east 7 s later. Round 2: east at those times, west 7 s later. |

### 8.3 Falling holo billboards (`cybercity-billboard-n`, `-s`; §5 segments, player-caused)
| Item | Value |
|---|---|
| Members | Two struts per billboard (0.8 x 9 x 0.8 m, `structural`, explosives only, 80 HP each, yellow band and DANGER tag). |
| Segment | `failBelow 2`: losing either strut drops it. Mode `tear-hinge`: the screen, truss and frames swing forward about the screen's bottom edge (pivot y 9.3) through 90° and drop 9.05 m over the 0.8 s fall (`duration`), landing flat across the avenue (rubble: a 0.5 m plate, the truss as 0.6 m cover). |
| Crush | Sweep box x -10.5..4.5, z 22.4..29.5, y 0..15.5 (mirror for the south); 45 + 6 m/s push, capped at min(60, 60% max HP) per pilot (Brickfall's rule, after multipliers). |
| Telegraph | 1.0 s (Brickfall minimum 0.8): the destruction kit's crush footprint, grit and groan; the screen's content glitches and splits (stressed), noise while falling, a 3 s dying flicker after it lands; the struts' lamps strobe red; a painted hatched fell zone on the street. |
| Events | `StructureStress`, `StructureFall`, `StructureLand` exactly as Brickfall §5.4. |

### 8.4 Optional (G3, not in M1): security sweep
A patrol drone over the plaza bridge sweeps a red scan fan on a 20 s cycle (5 s scan, 1 s lock telegraph, a 30-damage pulse on pilots inside the lit fan). Presentation would need the fan's angle as a pure function; deferred until the three M1 hazards are playtested.

### 8.5 Bots and PvE navigation
- **The cut:** nav links into and along it require the maglev to be Idle or Clear and at least 6 s from its next Warn; a bot in the cut at Warn heads for the nearest pocket (alcove nodes are safe) or exit. Kerbs at the edges are 6 cm presentation strips, so the obstacle ray (0.45 m) never jumps a bot into the cut; nav must not route bots off an edge except through drop links.
- **Couplings:** links over a deck are disabled from Warn to Clear; bots wait at the pylons.
- **Billboards:** fell zones and crush boxes avoided while a segment is stressed or falling (Brickfall's rule); rubble becomes low cover.
- **PvE:** adds caught by the train or the grating die like pilots (environmental credit); horde entries from the tunnel mouths (vents behind the pilot barrier), the market booths and the yard canopies.

## 9. Destruction plan
- **Only the billboards fall** (two segments on the §5 contract, tear-hinge). Everything else is static: podiums, spires, frontage, station, couplings, kiosks and vans. Cover that breaks: market stalls (wood, 34 HP), vending machines, bins and bikes (metal, 60 HP).
- Member damage filters, arm delay (4 s), caps, credit and replication are Brickfall's (§3.9, §5); the billboards add no new rule. Rubble swaps in under the landing; the kit's dead screen and sparks ride the rubble.

## 10. Visual identity
**Palette** (sRGB): neon cyan `#22E6FF` and magenta `#FF3DCF` (the HUD's, NativeCyberTheme), violet `#8B5CFF`, ice `#C8F4FF`, warm `#FF8A4D`, lime `#7DFF8A` (safe); precast `#4A4E5C`, anodised metal `#2C3140`, wet asphalt `#22232A`, paving `#3B3D47`, curtain glass `#141A2C`; hazard amber `#FFB000` and red `#FF2A3D` are reserved for telegraphs; the 2.3 band yellow/black on the struts. Fog and sky violet-navy.
- **Pilot readability:** the environment is dark in value; neon below 3 m is thin strips and small signs; the lower 1.8 m of every wall keeps the 22% grime ramp; pilots carry the key and the magenta rim. Cyan neon is everywhere, so teal and cyan pilot colours rely on value and the rim, as Brickfall's palette rule does.
- **Surfaces (NativeBlockLit's Cyber City bank):** `cyber-tile` pavers with wet patches, `cyber-panel` precast with rain streaks, `cyber-metal` brushed and bolted, `cyber-grate` floor grating (its void glows when a coupling charges), `cyber-road` wet asphalt with puddles that keep a sheen, `cyber-glass` curtain walls with lit and blinded panes that fade to their average far away, `cyber-holo` screens, `cyber-tube` neon. The bank sits beside the 14 base looks (a per-instance flag), so no old look or map changes.
- **Dressing kit (2,567 parts on the draft):** shopfronts, window bands, floor-line strips, neon fins, wall units, holo ads and a lit cornice on the frontage; the cut's rails, coils, edge strips, cable trays, kerb stripes, framed alcoves with SAFE lights, portal chevrons and the HELIX LINE sign; coupling grating, rails, insulators and lamps; station fascias, under-deck light lines, the lift doors and HELIX signs; podium floor-line strips (Nova magenta, Vega cyan), framed openings, holo ribbons; spire corner fins, crowns, masts and the NOVA / VEGA signs; stair LED nosings; markets' lantern strings, lanterns and canopy lattice, the NOODLE, GAME and CAPSULE signs; vehicle windows, lights and underglow; lamp heads, planter shrubs, pillar screens, sidewalks, lane marks, crosswalks, vents and the hatched fell zones. Below 3 m nothing stands out more than 0.25 m (checked), except floor paint and thin posts.
- **Landmarks:** the two spires; the Helix Station with its lift; the two holo billboards; the tunnel portals.
- **Lighting rig `neon-rain`:** key `#8EA4FF` at .58 from 54° elevation, bearing 315 (perpendicular to the SW-NE team axis), soft shadows .62; rim `#FF50D8` at .42 from 135°; trilight sky `#3E5494`x.6, equator `#5A4D84`x.5, ground `#28164A`x.5; shadows 4 cascades over 70 m (2 over 60 on the low tier).
- **Fog and sky:** linear fog `#222058` from 24 to 260 m; night sky with a violet horizon `#3F1D68`, deep zenith `#070714`, a hazy moon, faint stars, city-lit clouds.
- **Practical lights (8, NativeBlockLit's arrays):** the Nova and Vega signs (magenta, cyan), the two markets (warm lanterns with a slow flicker; violet), the two billboards (stuttering), the Cab Rank and Loading Dock canopies.
- **Backdrop (C#, deterministic, 4,570 instanced boxes in 10 draws):** a near ring of 18-22 m blocks behind the frontage with lit bands, neon and rooftop plant (kept under the menu orbit), the Helix Line's lit tunnels beyond the portals, two expressways, and mid and far towers (36-380 m) with setbacks, neon crowns and fins, holo screens and beaconed masts.
- **Atmosphere (scaled by effects quality and the low tier):** rain streaks round the eye (none under cover), splashes on the topmost surfaces, steam from street vents and rooftop plant, rain haze round the big neon, light pools under every glow source, and the wet ground's reflection streaks toward the eye; flying traffic on six lanes, expressway light streams, three searchlights, blinking beacons and moving holo content (also behind the menu).

## 11. Audio and ambience
- Ambience id `cybercity` (MapLooks): the rain bed (the existing `water` clip, looped quietly if it is long enough). Cues on existing keys until the owner supplies the map's own: maglev warning chime (`countdown`, slowed, heard everywhere), horn at the danger (`spike-rise`, low, heard everywhere), the pass's rush (`dash`, an octave down, at the head); coupling hum (`electric-hook`) and crack (`lightning`) at the deck; the billboards use the destruction kit's structure cues.
- Music: the existing `kinetic-city` track is proposed (the map's `music` field is Astra's).
- **Udio prompt** (for the owner, optional): "Instrumental darksynth / cyberpunk, 118 BPM, 4/4. Driving analog bass arpeggios, gated reverb drums, glassy pads, distant train-horn swells, rain texture, neon hum. Mood: 2 AM in a megacity, dangerous and sleek. No vocals. Seamless loop, no fade-in or fade-out." Delivery spec: ARENA_PROPOSAL.md §8.1.

## 12. Performance budget (ARENA_PROPOSAL.md §2.4)
| Item | Cyber City |
|---|---|
| Arena blocks | 702 draft blocks through NativeArenaDressing: 2 draws of 500 plus shadow passes |
| Dressing | 2,567 kit parts + 68 train parts: 7 draws (casters under 200) |
| Backdrop | 4,570 instanced boxes: 10 draws, no shadows cast |
| Atmosphere and FX | 2 draws (one solid, one effects batch); at most 3,600 quads (a busy frame is about 2,000) |
| Lights | 0 new Unity lights; 8 practical lights through the shader block |
| Shadows | 4 cascades over 70 m; 2 over 60 m on the low tier |
| Sim | 34 replicated props at round start; 2 segments |
| Reduced / low | rain 720 -> 216 at reduced, halved on low; pools, haze, reflections, steam, traffic and streams scale; searchlights off on low; telegraphs never reduced |

Not measured on the owner's PC.

## 13. PvE suitability
Good: the cut and the couplings are crowd-control tools a team can time (lure a pack onto a live coupling, hold an alcove while the train clears the cut); the podiums and concourse give holdable high ground; the markets give close-quarters retreat. Horde entries: the tunnel mouths, the market booths and the yard canopies. Adds need the same hazard gating as bots (§8.5).

## 14. Work split and status
| Step | Astra (sim) | Claude (presentation) |
|---|---|---|
| H0 | Review CYBERCITY-SIMULATION-CONTRACT.md | This proposal, the draft script, the contract, presentation built (this branch) |
| G1 grey-box | UnityMaps.json entry from the draft, spawns, bounds, pilot blocks, nav, hazards schema | Look and kit apply as soon as the map loads |
| G2 hazards | Maglev kill volume, coupling damage, billboard segments, events, bot gating, tests | Telegraphs already driven by the pure timing; tune on Windows |
| G3 art | Tuning | Performance capture on the owner's PC, music, owner review |

**Not verified anywhere:** Unity renders, Windows, multiplayer, bot behaviour, performance, capsule movement on the stairs and stair-bridges, readability on the owner's display. A passing build proves none of these.

## Appendix: draft files
- `docs/arenas/scripts/cybercity_m1.py`: the blockout (draft UnityMaps.json shape: blocks, segments, hazards, spawns, bounds, pilot blocks), its checks (spawn LOS with jitter, spacing, clearances, safe-pocket reach, ids, timing tables), `--ascii`, `--sections`, `--png`, `--json`.
- `unity/ChipArenaConversion/Tests/cybercity-draft.json`: the draft frozen for the managed checks (regenerate with `--json` after layout edits).
- Presentation code: `Core/NativeCyberCityLooks.cs`, `NativeCyberCityHazards.cs`, `NativeCyberCityKit.cs`, `NativeCyberCityScenery.cs`, `NativeCyberCityFx.cs`, `NativeCyberCityAmbience.cs`; `Runtime/NativeCyberCityPresentation.cs`, `NativeCyberCityAudio.cs`; the bank in `Resources/NativeBlockLit.shader`; the `neon-rain` entry in `MapLooks.json`; `Tests/CyberCityChecks.cs`.
