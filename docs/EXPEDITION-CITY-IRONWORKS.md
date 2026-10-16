# CITY IRONWORKS: the sealed foundry (Expedition instance `exp-foundry`): design draft

Claude, 2026-10-16, branch `claude/ironworks-design` (base shared `claude/sleepy-ritchie-g8ruhv`, `bb4b36a`).
**Status: design phase. Nothing is live.** The blockout, the manifest draft and the look are inert test fixtures. No game
code, AI, networking, live data or catalogue state changed. Central Station is a separate parallel design.

Owner request (2026-10-16): "lets begin the design for the Central Station and City Ironworks~ we can tweak the instances to
have specific zombies / enemies. So lets also begin creating unique enemies for those zones. We can work on visual updates /
bosses after." Owner bar (2026-10-16, via the coordinator): the Ironworks must look clearly better than Ward Zero, the kind of
map players call "a cool map", using our real assets and lighting. Readability comes first.

Roles: Claude owns layout, look, dressing, animation, VFX, audio cues and presentation. Astra owns stats, AI, the director,
networking, hazards' authority and tests of those. Everything marked **proposal** below is for Astra or the owner to accept.

## 0. At a glance

| | |
| --- | --- |
| Ids (reserved, unchanged) | area `exp-foundry`, map `expedition-foundry`, look `ironworks`; the town door `exp-city:door:exp-foundry` at the reserved doorway (-84, 0, 0), yaw 90 |
| Size | 100 x 68 m inside the shell (Ward Zero: 76 x 60 m); ceiling 18 m; floors at y 0 and y 6 |
| Floor | 4,453 m2 at ground and 987 m2 on the upper ring and walkways, all reached from the entry slots |
| Blockout | 308 blocks, 5 stairs, 31 pilot exclusion columns, 41 spawn records, 3 caches, 3 boss points, 11 proposed hazards |
| Rooms | Time Office (entry), Casting Floor (arena), Furnace Aisle and Charging Deck, Rolling Mill, Stockhouse, Pattern Shop, Coke Oven Battery, Slag Yard and Drop Forge, Converter Hall (boss arena placeholder) |
| Run from the entry to the boss arena | direct 83.4 m (12.3 s running); north loop 101.4 m (14.9 s); south loop 109.9 m (16.2 s) |
| New enemies (proposals) | Cinderling (swarm), Stoker (puller), Slagback (armoured heavy), Ladleman (elite area denial) |
| Files | `docs/arenas/scripts/expedition_ironworks.py`; `unity/ChipArenaConversion/Tests/expedition-ironworks-draft.json`, `-layout-draft.json`, `-look-draft.json`; `Tests/test_expedition_ironworks_draft.py`; `Tests/ExpeditionIronworksDraftChecks.cs` |

## 1. Fantasy and mood

The city's steelworks was sealed with the outbreak, but nobody banked the furnaces. Twenty years of habit kept them burning:
the dead still work the shift. Inside, it is night, always. The only light is the metal itself, the furnace mouths, the sodium
lamps that still buzz on the night circuit and the cold mercury lamps in the high roof.

- **Feel:** heat you can see. A cathedral of black iron lit from below in orange. Haze in the roof, sparks falling off the
  gantries, steam hissing from the quench tower, the drone of the furnaces under everything.
- **Contrast with the town and Ward Zero:** the town is dusk blue and burning; Ward Zero is cold teal and violet. The Ironworks
  is the warm one, black and orange with a cold blue counter-light. It must never become a flat orange room (section 3.2).
- **Story beats in the dressing:** the clocking-in rack and the shift board stopped at 6:47 (the city's stopped clock); a
  pour left running into the molds; a coke oven door jammed open; the Bessemer converters waiting tipped, as if for a pour that
  never came (the boss arena).

## 2. Layout

### 2.1 Plan

```
 z 34 +----------------+---------------------------------------------------+--------------+
      | COKE OVEN      |  [F1]          [F2]          [F3]   skip        |  bunkers     |
      | BATTERY        |  FURNACE AISLE: tapholes, runners south          |  STOCKHOUSE  |
      |  (walkway y6)  |  CHARGING DECK (y 6) ========================== ore bridge (y6) |
      |  quench  stair |      |r1          |r2          |r3       stair    |  stair       |
 z 12 +-------+--door--+  W   |            |            |            E     +---door-------+
      |                |  c   CASTING FLOOR (the arena; upper ring at y 6) |              |
      |   CONVERTER    |  a     torpedo car    torpedo car                 =  TIME OFFICE |
      |   HALL         =  t              ladle crane pour                  =  (entry)  >EXIT
      |  (boss arena)  |  w   molds   molds              ladle            |              |
 z-12 +-------+--door--+==== SOUTH GALLERY (y 6) over the mill wall =======+---door-------+
      | SLAG YARD /    |  ROLLING MILL: reheat furnace -> roller table    | PATTERN SHOP |
      | DROP FORGE     |  through four stands and the shear -> cooling bed| (machines)   |
 z-34 +----------------+---------------------------------------------------+--------------+
     x-50            x-24                                                x36           x50
```

`python3 docs/arenas/scripts/expedition_ironworks.py --png plan.png` draws the plan with every record (renders below).

### 2.2 Rooms

| Room | Extent (x, z) | Levels | Role | Identity |
| --- | --- | --- | --- | --- |
| Time Office | 36..50, -10..10 | 0 | the way in and out; cache 0; a breath | clock racks, lockers, the shift board at 6:47, a stuttering sodium tube |
| Casting Floor | -24..36, -14..22 | 0, 6 | the main arena; big waves | the ladle crane pouring into the molds, torpedo cars on their track, the runners arriving from the north |
| Furnace Aisle and Charging Deck | -24..36, 22..30 | 0, 6 | arena edge; furnace-mouth spawns | three open-hearth furnaces, charging doors glowing on the deck, tapholes and runners under it |
| Rolling Mill | -24..36, -34..-14 | 0, 6 | lane fight | a 60 m hall: reheat furnace, roller table through four stands, hot slabs, the gallery above |
| Stockhouse and Ore Bridge | 36..50, 10..34 | 0, 6 | branch up to the ring | ore bunkers, chutes over the ore bridge, a conveyor |
| Pattern Shop | 36..50, -34..-10 | 0 | side room; machines | wooden patterns, mold boxes, the sand muller |
| Coke Oven Battery | -50..-24, 12..34 | 0, 6 | choke; two lanes | the oven doors with glowing seams, one jammed open, the pusher car, the quench tower |
| Slag Yard and Drop Forge | -50..-24, -34..-12 | 0 | risk and reward; cache 2 | the slag bank and its glowing tongue, slag pots, the drop hammer over a hot billet |
| Converter Hall | -50..-24, -12..12 | 0 | **boss arena placeholder** | two Bessemer converters on trunnions, the pouring pit, a crane hook |

### 2.3 Flow

1. **Entry.** The town door is the sealed shutter at the end of the Ironworks Yard. Inside, the party stands in the Time Office
   (8 entry slots facing west). Cache 0 is in its south-west corner. Nothing spawns within 22 m on foot of the entry slots.
2. **The reveal.** The Time Office's west opening (8 m wide) frames the Casting Floor: the pour in the centre, the furnace line
   glowing on the right, the torpedo track leading the eye west. This is the first postcard (section 3).
3. **The arena.** The Casting Floor is open ground ringed by the upper ring at y 6 (north charging deck, east and west catwalks,
   south gallery). Two stairs reach the ring from the floor (north-east corner and west side); open rail gaps let pilots and
   brutes drop back down.
4. **Two loops to the Converter Hall:**
   - **North:** under the charging deck past the tapholes, through the furnace aisle's west door or along the deck to the coke
     battery walkway, down the coke stair, through the battery's south lane to the converter hall's north door (101.4 m).
   - **South:** through the mill wall into the Rolling Mill, along either lane, through the west opening into the Slag Yard,
     north to the converter hall's south door (109.9 m).
   - **Direct:** the Casting Floor's west opening into the Converter Hall (83.4 m). **Proposal:** a sealed shutter here that
     opens with the boss phase, so the loops are the way until then (it needs an instance gate in the schema: section 10.6).
5. **Back loop.** Rolling Mill east door, Pattern Shop, Time Office: the way home without crossing the arena.

### 2.4 Verticality, sightlines, cover, chokes

- **The upper ring (y 6)** is the Ironworks' signature for play: a rectangle of catwalks round the casting floor with branches
  to the ore bridge (east) and the coke walkway (west). It gives kiting loops, sniping lanes over the pour and drop-down
  escapes. Rails are 1.05 m (a pilot can jump them); the gaps are drop points (navigation drop hints).
- **Sightlines:** long east-west lines on the casting floor (60 m), along the charging deck (60 m) and down the mill (the
  stands are windowed: the eye follows the slab line, a pilot cannot walk through). Short lines in the coke lanes and the slag
  yard.
- **Cover:** torpedo cars (3.6 m), ladles on stands (3.4 m), mold rows and the pig bed (0.7-0.9 m, crouch cover), the stair
  undersides on the casting floor, the mill stands, slag pots. Tall masses carry pilot exclusion columns so nobody perches
  where the horde cannot follow.
- **Chokes:** the Time Office opening (8 m), the coke battery's two lanes (4.5 m and 8 m), the converter hall's three doors
  (5 m), the mill's crossing aisles (3-4 m).
- **Arena rooms:** the Casting Floor (main waves), the Converter Hall (the boss), the Rolling Mill (lane waves).

### 2.5 Records (Ward Zero's conventions)

| Record | Value |
| --- | --- |
| `exp-foundry:door:exit` | doorway (50, 0, 0) in the east shell; anchor (49.4, 0, 0) yaw 90 (faces out, toward the town); interaction centre (48.9, 1.5, 0) 3 x 3 x 2; closure (49.75, 1.7, 0) 3.2 x 3.4 x 0.4 between the jambs; approach (48.4, 0, 0) |
| Entries | 8 at x 47.0 and 45.6, z -2.6, -1.1, 1.1, 2.6, yaw 270; Hulk-clear (tightest 2.55 m), 1.40 m apart |
| Caches (`chest`) | 0: Time Office (38.6, 0, -6.4); 1: charging deck (6.5, 6, 24.2), upstairs between furnaces 2 and 3; 2: slag yard (-36, 0, -16.6), by the drop hammer. Interactions 0.8 m in front, range 2.0 |
| Boss points | the pouring pit (-35, 0, 0) and either side (-34, 0, +-7.5), yaw 90 (facing the main entrance). Hulk-clear by 4.3 m |
| Machines (proposal, not anchors) | chip and med in the Pattern Shop (46.6, 0, -16 / -24); chip on the coke walkway (-30, 6, 26.6); recruit in the Time Office (39, 0, 7). Ward Zero's machines live in PveContent; these would follow (section 10) |
| Sanctuary | none (instances have none, as Ward Zero) |
| Town door (draft manifest) | anchor (-83.4, 0, 0) yaw 90; closure against the sealed shutter; approach (-82.4, 0, 0) (the hub's reserved approach); returns x -79.6 and -78.0, z -3, -1, 1, 3, yaw 90 (the Ward Zero door's mirrored); nearest town spawn 16 m |

### 2.6 Spawns (41; the PveEntry shape plus `allow`)

Existing spawn kinds only (door, vent, drop, breach), so the strict parser accepts them. The additive `ironworks.spawns[]` gives
each record an **entrance style** for its presentation and the **new kinds it would admit** (proposal):

| Style | Count | Kind | Where | Proposed kinds |
| --- | --- | --- | --- | --- |
| furnace-mouth | 3 | door, level 1 | charging doors on the deck: they walk out of the fire | cinderling, stoker |
| ash-door | 3 | door | furnace bases under the deck | cinderling, stoker |
| coke-oven-door | 5 | door (one on the walkway) | the oven doors on the bench | cinderling, stoker |
| door | 6 | door | stores, the flue gallery (deck), the stock yard | mixed |
| slag-trench, slag-pit, mill-pit | 11 | vent (floor) | floor trenches and pits: they climb out of the slag | slagback, cinderling |
| gantry, bunker-chute | 5 | vent (ceiling: a drop) | off the crane girders and the ore chutes | cinderling, stoker |
| catwalk-drop, slag-bank | 3 | drop (brute) | off the ring and the slag bank | slagback |
| crane-bay | 2 | drop (Hulk) | lowered from the crane: casting floor, converter hall | ladleman |
| breach | 3 | breach (Hulk) | bricked-up openings: mill, forge, converter hall | ladleman |

Checks: every exit is on reached floor at its own level with its clear radius and routes back to entry 0 with its own capsule
(Hulk where allowed); exits are 4.0 m apart at least; none within 4 m of a boss point; none within 0.5 m of a proposed hazard.

## 3. Look and set dressing

### 3.1 The look (draft entry `ironworks`, `Tests/expedition-ironworks-look-draft.json`)

A sealed interior, as Ward Zero's quarantine look: no sky, no shadowing key (the roof would shadow it). Exact values:

| Field | Value |
| --- | --- |
| Background, fog | 0x0B0807; linear fog 0x120C0A from 12 to 90 m; farClip 240 |
| Key (fill) | 0x8A9CC0 x 0.13, elevation 72, bearing 210, no shadows: the cold roof monitors |
| Rim | 0x6A7FA8 x 0.16, elevation 38, bearing 200 |
| Ambient | sky 0x3C4A60 x 0.075, equator 0x2C2A2C x 0.055, ground 0x2A1A10 x 0.07 (a warm ground bounce) |
| Blocks | dressed; dust 0.12; ground haze 0.25 to 4 m, 0x1E1612 |
| Practical lights (the default 8) | furnace-2 doors (0, 7.4, 26.2) 0xFF6A1C x 1.6, r 12, fire 1.1 Hz; ladle pour (7.3, 2.6, -7) 0xFFB45A x 2.4, r 13, fire 3.1; runner r2 (0, 0.8, 16.5) 0xFF7A26 x 1.2, r 8, fire 2.2; reheat door (28.2, 1.8, -25) 0xFF5A18 x 1.9, r 13, fire 0.9; mill sodium (6, 10.4, -25) 0xFFB347 x 0.7, r 15; casting sodium (18, 12, 4) 0xFFB347 x 0.4, r 14, stutter; mercury west (-14, 11.7, 12) 0xBFD8FF x 0.5, r 16; mercury south (0, 11.7, -11) 0xBFD8FF x 0.45, r 15 |
| Block looks | 62 entries: refractory `brick-buff` furnaces, soot-black `brick-red` partitions, `factory-grating` ring and stairs, `steel-green` iron in black, machine green and worn safety yellow, `asphalt` slag, `timber` patterns; every block is listed |
| Names | dressing `ironworks`, atmosphere `ironworks`, ambience `ironworks` (none exist yet) |

### 3.2 Readability rules (gameplay first)

1. **Heat stays local.** Orange light comes only from molten sources and furnaces, in tight pools (ranges 8-13 m). Fill,
   rim and work light are cool. The renders showed a flat orange room when the warm lights had 20 m ranges and the rim was
   warm; that is the failure mode.
2. **Two of the eight practical light slots are cool** (mercury-vapour lamps), whichever furnace is near (picker rule,
   section 9.2). The cool light keeps the floor, the pilots and the enemies' shapes readable.
3. **Enemies are dark silhouettes with non-orange tells.** The new kinds carry white-hot, cyan or cream tells; nothing an
   enemy does is signalled in plain orange (the environment's colour). Against furnace glow they read as black shapes (the
   lineup render shows it).
4. **Telegraphs:** cream-white decals with a hard edge for area attacks (slam ring, pour arc), cyan dashes for lines (the
   Stoker's pull), a white flare on the body for fast attacks. The hazard warn colour stays the hazard kit's amber, used only by
   hazards. Molten floor glow never exceeds a telegraph's brightness: molten surfaces sit at HDR 1.4-4, decals at full white
   with alpha.
5. **Existing kinds in this light:** the brute's orange seams (0xff6a2c) vanish against the furnace glow. **Proposal:** a
   per-zone seam tint for presentation (brute to 0xffd27a cream in the Ironworks); the others read.
6. **Pickups and caches:** caches keep their existing look; add a cool top light so they never sink into the orange.
7. **Lead the eye:** every room has one brightest thing and it is where the route goes: the pour (arena centre), the furnace
   doors (north loop), the reheat slab line (south loop), the jammed coke door (west lane), the converter mouths (boss).

### 3.3 Per room: hero shot, palette, light, atmosphere, density

| Room | Hero shot (the postcard) | Palette | Lights (key / fill / practical) | Atmosphere | Emissive hot metal | Dressing density |
| --- | --- | --- | --- | --- | --- | --- |
| Casting Floor | from the south gallery: the ladle tipped, a white-orange stream falling into the molds, the furnace line glowing behind, runners leading the eye | black iron, sooted concrete, safety yellow crane, white-to-orange metal | pour 2.4/13, runner ends, 2 sodium high bays, 2 mercury | smoke layer under the roof lit from below, sparks off the splash (70 streaks), ember motes, roof shafts | the stream, the poured molds, the torpedo mouths, the pig bed | high: crane, hook, chains, molds, rails, puddles, stencils |
| Time Office | through the west opening: the pour framed by the brick jambs | warm brown, one stuttering tube, red LED shift board | office tube 0.9/12, the pour beyond | a little dust in the tube light | the 6:47 digits | medium: lockers, clock racks, bench, notices |
| Furnace Aisle / Deck | on the deck facing a charging door: a Cinderling silhouetted against the open mouth | refractory buff, black steel buckstays, white-hot mouths | door lights 1.6/12 per furnace, tapholes 1.4/9, runners 1.2/8 | heat shimmer over the doors, sparks spat out, smoke at the hoods, god rays out of the doors | doors (open: white; closed: orange seams), tapholes, runners hottest at the furnace | high: buckstays, bands, hoods, flues, pipes, charging boxes |
| Rolling Mill | from the south-east corner: the stands as portals, a slab glowing through each window, sodium lamps receding | machine green, black table, red-to-white slabs, sodium yellow | reheat door 1.9/13, slab lights 0.9-1.7/7-10, 4 sodium 0.7/15, mercury west | scale sparks at the stands, heat over the hot end | slabs (white at the furnace, red at the cooling bed), the reheat discharge door, the windows' glow | medium-high: rollers, screwdown motors, coils, billets, the pulpit's lit window |
| Coke Ovens | along the bench: oven doors receding with glowing seams, one jammed open, the pusher car | soot red-brown brick, iron doors, orange seams, blue-white mercury | open door 1.4/9, seams 0.6/7, mercury 1.0/16, sodium | smoke leaking at the doors, steam from the quench tower | door seams, the open oven | high: buckstays every 2.2 m, doors, the pusher car, hoppers |
| Converter Hall | from the east door: the two converters with glowing mouths, the hook over the pit | black iron, banded vessels, pit glow, cool roof light | converter mouths 1.5/13, pit 0.9/12, 2 mercury, sodium | smoke out of the mouths, a red pit haze | the mouths, the pit's slag | medium: bands, seams, trunnions, the hook (room for the boss) |
| Slag Yard / Forge | the slag tongue running off the bank toward the camera, the drop hammer over its billet | glassy black slag, glowing cracks, steel frame | slag tongue 1.7/10, spill 1.6/13, billet 1.1/8, forge 1.0/8, mercury | heat over the tongue, sparks at the anvil | the tongue (white at the bank, red at the tip), cracks, the billet, the hearth | medium |
| Stockhouse | the ore bridge under the chutes | rust, ore brown, sodium | sodium, mercury | ore dust | none | medium |
| Pattern Shop | the patterns on their racks in a warm lamp | timber, sand | one warm lamp | dust | none | medium (the quiet room) |

Budget rule for every room: one brightest emitter, at most three emissive families, warm pools under 13 m, two cool slots.

### 3.4 Renders (session scratchpad; not committed)

Offline renders through the game's own shaders (NativeBlockLit's shared section and NativeNightSky, translated mechanically
in headless Chromium WebGL, as the Ward Zero and Brickfall lighting work did), the draft map and look, the eight practical
lights picked per camera, a scratch dressing pass standing in for the proposed kit, FX quads and the real pilot body and zombie
models posed by NativeHordeGait. Paths are listed in the DEV_LOG entry. They are concept renders: dressing, FX and lights are
the doc's proposals, not shipped code.

## 4. Asset inventory: what we have, per room

| Existing piece (by id) | Use in the Ironworks |
| --- | --- |
| NativeBlockLit looks: `brick-buff`, `brick-red`, `steel-green`, `corrugated`, `factory-grating`, `asphalt`, `timber`, `neon`, `window-lit` | every block (62 entries in the draft look) |
| Cyber looks: `cyber-tube` (emissive strip in the block's colour x level), `cyber-glass`, `cyber-holo`, `cyber-metal`, `cyber-grate` | molten metal surfaces, roof monitors, the pulpit screens, dark plate |
| Ward looks: `ward-burner` (a grate that heats from dull red to white with the light level), `ward-light` | taphole and forge grates, coke seams, the slag cracks (driven by a kit) |
| NativeWardZeroFx roles: halos, pools, shafts, haze, motes, steam, sparks, drips | the atmosphere layer of every room |
| NativeWardZeroField (a baked ceiling light field of many fixtures) | the answer to the 8-light cap: bake dozens of furnace and runner glows into a field |
| NativeWardZeroMood (calm, horde, boss, emergency presets) | Ironworks moods: furnace flicker and roar up with the waves |
| NativeWardZeroLights / NativeExpeditionCityLights (8-slot pickers) | the Ironworks picker (6 warm + 2 cool) |
| NativeFactoryKit (lathes, mills, cabinets, racks, coils, crates, cribs, pulpits, the ladle car and ladle, billets, ingots) | mill, pattern shop, casting floor props |
| NativeFactoryHazards: `saw-slot`, `saw-rail`, `crane` (drop zones), `press`, `vent` (steam) | the hot saw, the flying shear, the ladle crane, the drop hammer, the quench steam |
| Factory look's `ladle-melt` (fire light) and `welding-bay` (flicker) | precedents for the pour and the gantry welding sparks |
| NativeExpeditionCityKit.Landmarks `FoundryFace` / `FoundryShutter` (CITY IRONWORKS lettering, the sealed shutter) and its `Text()` lettering | the town side; in-instance signage (No 2 FURNACE, CAST HOUSE, MILL 1, 6:47) |
| NativeExpeditionCityKit.Hub wilds (rails, boxcars, slag heaps, coal tank) and NativeExpeditionCityFx (flames, embers, smoke columns, falling ash) | stockhouse and slag yard dressing; furnace mouths and smoke |
| Hazard kit (`alarm-beacon` lockdown amber, `alarm-klaxon`, `alarm-sign` up to 14 letters, `saw` industrial) | crane and pusher warning beacons, MOLTEN METAL / CRANE OVERHEAD signs, the shear |
| NativeVfxDraw roles: Fireball, Smoke, Spark, Ember, Flame, HotDebris, Mist, Halo, Lick | pours, splashes, sparks, the Cinderling's pop, steam |
| NativeStructureFx (debris, dust, window strobe) | the Slagback's crust shedding; breaches |
| NativeHordeFx (brute slam dust and cracks), the spitter's acid pool presentation | the Slagback's slam; the Ladleman's molten pool (recoloured) |
| NativeHordeModels and NativeZombieMotion on the ZOMBIE PRO takes (`tools/pack-zombie-provenance.json`) | the four new kinds' bodies and gaits |
| Licensed local packs (by id, never committed): ZOMBIE PRO MoCap Animation Pack (`pack-zombie`), the owner's effects audio bank (`EffectsAudio` via `tools/install_local_assets.py`) | animation; audio until the Ironworks cues exist |

### Gaps, ranked by visual payoff

1. **Molten flow shader** (a `cyber-tube` variant: scrolling hot noise, a dark crust that drifts and cracks, brightness from
   the hazard state). Runners, the pour, slabs, the slag tongue. Biggest single payoff.
2. **Heat distortion** (a screen-space refraction quad over hot sources, low tier off). It sells "heat you can see".
3. **The Ironworks kit** (`NativeIronworksKit`, as NativeWardZeroKit): furnace doors and hoods, buckstays, flues, the ladle
   and crane hook, torpedo mouths, rollers, coke doors, converter vessels, rails as bars, stencils. The renders' scratch pass is
   its sketch (about 650 parts; target 7-8k, as Ward Zero's 7,124).
4. **A baked glow field** (NativeWardZeroField's method) for the dozens of molten emitters beyond the 8 lights.
5. **Volumetric light** (shaft quads exist; a soft-particle depth fade would remove their hard edges against the floor).
6. **Smoke layer under the roof** (large lit sprites, sorted; the town's smoke is close).
7. **Converter, ladle and torpedo meshes** (the box versions read; rounded vessels would read better).
8. **Spark trails with bounce** (the Spark role streaks exist; a floor bounce would help the pour).

## 5. Hazards (all proposals for Astra; presentation is Claude's)

Ids start with the map id. Volumes and cycles are in `ironworks.hazards[]`. Every record keeps 2 m (spawn exits 0.5 m) clear.

| Id | Kind | Reuse | Cycle (warn / live / period, s) | Telegraph | Effect |
| --- | --- | --- | --- | --- | --- |
| `-runner-r1..r3` | molten-runner | new | 2 / 6 / 34 (offsets 0, 11, 22) | the taphole sparks and roars, the trough brightens from the furnace end | burn while live inside the trough |
| `-ladle-crane` | crane | Factory crane, pour instead of drop | 2.5 / 3 / 26 | amber beacon, klaxon, the ladle tilts, a cream decal on the zone | splash zone burns; the stream blocks sight |
| `-drop-hammer` | press | Factory press, vertical | 1.2 / 0.35 / 6 | the ram lifts, a hiss, a white glint on the billet | crush on the anvil |
| `-slag-spill` | slag-spill | new | 3 / 10 / 40 | the pot tips and glows | a tongue spreads east over 3 s, burns, cools over 10 s |
| `-hot-slab` | hot-slab | Factory conveyor idea | 1.5 / 9 / 18 | the reheat door opens, a white slab emerges | burns and carries anyone on the table |
| `-flying-shear` | saw-rail | Factory saw-rail / kit saw | 1 / 0.6 / 9 | blade drop squeal | cut across the table |
| `-quench-steam` | vent | Factory vent | 1.5 / 3 / 22 | hiss, white mist at the grate | push and scald |
| `-pusher` | pusher | new (a travelling press) | 3 / 6 / 30 | amber beacon, horn | crush in its path along the bench |
| `-converter-tilt` | converter | new | boss-driven only | (boss design) | (boss design) |

Molten pools left by enemies (the Ladleman) share the runner's burn. No hazard is lethal on contact; all are dodgeable.

## 6. Unique enemies

Built on the pilot-model zombie rig and the ZOMBIE PRO gaits, as every horde kind is. Each reads at 30 m as its own silhouette
and keeps a non-orange tell. Tiers: Cinderling (fodder), Stoker (special), Slagback (heavy), Ladleman (elite). Stats are in the
Astra contract (section 10); timings here are the presentation's proposal.

### 6.1 Cinderling: the swarm

- **Role:** fast fodder in packs of 3-5; the Ironworks' runner. Fire-immune: it paths through live runners where pilots cannot.
- **Look:** a slight furnace hand (the runner model at 0.93 scale), charred black to the bone, the seams white-hot (not orange),
  a coal burning in its ribs, a smoke plume streaming from its head. Against the furnace glow: a small black shape with a white
  heart.
- **Animation:** idle `idle-3`, `shamble-2`; move `sprint-1`, `sprint-3`; attack `lunge-1`, `lunge-2` (wind-up held as a crouch);
  hits `knock-1..5`; deaths `front-*`, `back-*` then the pop (procedural: it curls and bursts into embers); entrances: the wake
  take out of a furnace mouth or oven door, the vent climb out of a pit. **New:** none; the pop is procedural.
- **Attack:** lunge. Wind-up 0.38 s: crouch, the core flares white, a hiss. Strike at 0.38 s, 1.8 m reach.
- **Death pop:** dies in melee range: a 0.45 s fuse (core strobes white), then a 1.4 m ember burst.
- **VFX:** ember trail (Ember role), the core's halo, the smoke plume; the pop: Fireball and Ember.
- **Counters:** area damage; kill at range; step out of the 1.4 m pop. Weak point: the chest coal (double damage).
- **Spawns:** furnace mouths, ash doors, coke oven doors, gantries, chutes.

### 6.2 Stoker: the puller

- **Role:** a controller that breaks formations: it hooks an isolated pilot and drags them in.
- **Look:** an aluminised heat suit, silver (it catches every light as a bright edge), a square hood with one cyan visor slit,
  a coat to the knees, a 3 m rake with a hooked iron tip. Taller than a shambler (1.07 scale). Silhouette: tall, narrow, a long
  pole.
- **Animation:** idle `idle-3` with the rake planted (procedural hand lock); move `walk-6`, `chase-2` with the pole carried (new
  left-hand IK layer); rake sweep `swipe-r1` (the rake follows the right hand); hits `knock-*`; deaths `back-*`, `front-*` (the
  rake falls, procedural). **New mocap wanted:** a two-handed pole thrust and yank (about 1.2 s) for the pull.
- **Attacks:**
  - Rake sweep: 2.6 m arc in front; wind-up 0.5 s (rake drawn back), strike at 0.5 s.
  - **Hook pull:** wind-up 0.7 s (rake raised over the shoulder, the hook glints, a cyan dashed line on the floor to the
    target, a chain rattle); throw at 0.7 s down a 4.5 m line 0.6 m wide; a hit drags the pilot 2.5 m toward the Stoker over
    0.35 s and roots 0.4 s. Cooldown 6 s.
- **VFX:** the visor glow, the cyan line, the hook's spark trail, a chain streak.
- **Counters:** sidestep the line; a heavy melee blow or 15% of its health during the wind-up staggers it; the visor is its
  weak point.
- **Spawns:** furnace mouths, doors, the flue gallery and the coke walkway (the upper level), gantries.

### 6.3 Slagback: the armoured heavy

- **Role:** a slow tank that refuses frontal fire: it forces flanking.
- **Look:** a brute crusted in black, glassy slag plates (chest, shoulders, forearms, head), dim orange cracks between them, a
  bright fissure down its back (the weak point). Silhouette: lumpy and asymmetric.
- **Animation:** idle `idle-3`; move `walk-4`, `chase-5`; slam `smash-1`, `smash-2` with a longer hold; emerge from slag: the
  vent climb with slag dripping (procedural); hits `knock-*`; deaths the brute's. **New:** the crust shedding (procedural plates
  flying off on `knock-3`).
- **Attacks:** slam: 1.6 m disc 1.2 m ahead (the brute's), wind-up 0.55 s (a cream dashed ring appears on the floor, a
  grinding crackle), strike at 0.55 s, debris and a crack decal.
- **Armour:** 60% less damage from the front 120 degrees while crusted. After 120 damage the crust sheds (0.8 s stagger, plates
  fall), the armour is gone and it moves 25% faster.
- **Counters:** flank it (it turns at 90 degrees a second), shoot the back fissure (double damage), break the crust with
  heavy hits.
- **Spawns:** slag pits and trenches (it climbs out of the slag), catwalk drops, the slag bank.

### 6.4 Ladleman: the elite

- **Role:** area denial and a priority target; one at a time (two in late waves).
- **Look:** a big brute (1.24 scale) in a leather apron and a welder's mask with a cyan lens, carrying a crucible ladle of
  molten iron on a shank in both hands. The glowing bowl is the brightest thing on any enemy: you always know where it is.
- **Animation:** idle `idle-3` with the arms locked to the shank (new procedural two-handed carry); move `walk-4` at tempo 0.7
  with the carry layer; ladle swing `smash-2`; spilled `knock-1` and a fall to the knees; deaths the brute's at 1.24x (the ladle
  drops and spills). **New mocap wanted:** lift, tip and pour (about 2.2 s); a heavy two-handed carry walk.
- **Attacks:**
  - **Pour:** wind-up 1.0 s (raises the ladle, the metal bubbles white, a cream arc 60 degrees by 4.6 m appears on the floor);
    pours for 1.2 s along the arc; leaves a molten pool for 3 s (burn). Cooldown 7 s.
  - Ladle swing: 2.4 m reach, wind-up 0.6 s.
- **Weak point:** the ladle. 200 damage to it spills the metal over him: 35% of his health, a 2 s stun, no more pours.
- **Counters:** stay out of the arc, flank, shoot the ladle.
- **Spawns:** Hulk breaches (he kicks through the bricked openings) and the crane bays (lowered on the hook).

### 6.5 Concept sheet

`enemy-sheet.png` (renders): the lineup with a pilot and a shambler, backlit by the furnace line and front-lit, then each kind
moving, telegraphing and striking. Built from the real models and gaits with the proposed parts as boxes on the bones.

## 7. Encounter plan (proposal)

| Phase | Rooms active | Mix |
| --- | --- | --- |
| Entry (quota 0) | Time Office quiet; first trickle on the casting floor | shambler, cinderling packs of 3 |
| Wave 1 | casting floor, furnace aisle | shambler .55, cinderling .35, stoker .10 |
| Wave 2 | + rolling mill, stockhouse | shambler .35, cinderling .35, stoker .15, slagback .10, spitter .05 |
| Wave 3 | + coke ovens, slag yard | shambler .25, cinderling .30, stoker .15, slagback .15, brute .05, ladleman .04 (cap 1), hulk .03 (cap 1) |
| Wave 4 | all but the converter hall | shambler .20, cinderling .30, stoker .15, slagback .15, brute .05, ladleman .06 (cap 2), hulk .04 (cap 1) |
| Boss (placeholder) | converter hall (+ the direct shutter opens) | adds: cinderling .5, stoker .2, slagback .2, shambler .1 |

Per room: furnace aisle and coke ovens favour cinderlings and stokers (fire doors); slag yard, trenches and pits favour
slagbacks; breaches and crane bays are the ladleman's and the Hulk's. The director picks spawns away from pilots' sight, as
today.

## 8. Audio: every cue the zone needs (for the owner to generate; nothing generated here)

Keys follow the bank's style. Category as in `docs/audio/sound-requests.json` (`voice`, `step`) plus `sfx`, `loop`, `music`.

| Key | Cat. | Count | Brief (ElevenLabs-style prompt) |
| --- | --- | --- | --- |
| `music.ironworks.explore` | music | 1 | slow industrial drone, low brass and metallic pulses, distant hammering, dread, loopable 90 s |
| `music.ironworks.combat` | music | 1 | driving industrial percussion, anvil hits on the beat, distorted low synth, 120 bpm, loopable |
| `music.ironworks.horde` | music | 1 | the combat theme with a choir of grinding metal and a rising siren, loopable |
| `music.ironworks.boss` | music | 1 | placeholder until the boss exists: heavy, slow, furnace roar under it |
| `music.ironworks.clear` | music | 1 | short sting: a furnace exhales, a bell on a crane, quiet |
| `ambience.ironworks.casting` | loop | 1 | a vast steel hall at night: furnace roar, distant clanks, hiss of molten metal, crane motor hum |
| `ambience.ironworks.furnace` | loop | 1 | close furnace roar, flame breathing through a door, crackle |
| `ambience.ironworks.mill` | loop | 1 | rolling mill: rollers turning, motor whine, steel groaning, sodium lamp buzz |
| `ambience.ironworks.coke` | loop | 1 | coke ovens: muffled roar behind iron doors, hissing gas leaks, dripping |
| `ambience.ironworks.slag` | loop | 1 | crackling cooling slag, glassy ticks, distant hammer |
| `ambience.ironworks.office` | loop | 1 | an empty office: a buzzing fluorescent tube, a clock not ticking, far hall rumble |
| `ambience.ironworks.converter` | loop | 1 | huge vessel humming, deep resonance, low wind in the roof |
| `hazard.ironworks.runner.open` | sfx | 3 | a taphole bursts open: roaring gush of molten iron, sparks |
| `hazard.ironworks.runner.flow` | loop | 1 | molten iron flowing in a trough, thick bubbling hiss |
| `hazard.ironworks.ladle.tilt` | sfx | 2 | a giant ladle creaking as it tips on chains, metal groan |
| `hazard.ironworks.ladle.pour` | loop | 1 | heavy molten metal pouring into a mold, roaring hiss and spatter |
| `hazard.ironworks.crane.move` | loop | 1 | an overhead crane traveling, electric motor whine, rattling chains |
| `hazard.ironworks.hammer.lift` | sfx | 2 | a drop hammer rising, hydraulic hiss and chain |
| `hazard.ironworks.hammer.strike` | sfx | 4 | a drop hammer slamming a hot billet, huge metallic boom, short ring |
| `hazard.ironworks.slag.tip` | sfx | 2 | a slag pot tipping, glassy molten slag pouring out and crackling |
| `hazard.ironworks.slab.out` | sfx | 2 | a reheat furnace door opening, a white-hot slab rolling out on rollers |
| `hazard.ironworks.shear` | sfx | 3 | a flying shear cutting hot steel, sharp hydraulic snap and screech |
| `hazard.ironworks.steam` | sfx | 3 | a quench tower venting, violent steam blast |
| `hazard.ironworks.pusher` | sfx | 2 | a coke pusher machine horn, then grinding wheels on rails |
| `pve.ironworks.cinderling.hiss` | voice | 4 | a burning zombie hissing and crackling like embers, raspy breath |
| `pve.ironworks.cinderling.screech` | voice | 4 | a small charred zombie shrieking, dry and crackling |
| `pve.ironworks.cinderling.flare` | sfx | 3 | a coal flaring white-hot, short whoosh |
| `pve.ironworks.cinderling.pop` | sfx | 4 | a charred body bursting into embers, crackling pop |
| `pve.ironworks.stoker.breath` | voice | 3 | a heavy breath through a heat hood's respirator, muffled |
| `pve.ironworks.stoker.rake` | sfx | 4 | a long iron rake sweeping, whoosh and scrape on concrete |
| `pve.ironworks.stoker.hook.windup` | sfx | 3 | a chain rattling as a hook is drawn back, metal ring |
| `pve.ironworks.stoker.hook.throw` | sfx | 3 | an iron hook thrown on a chain, whip crack |
| `pve.ironworks.stoker.hook.yank` | sfx | 3 | a heavy chain yanked tight, clank and drag |
| `pve.ironworks.slagback.grind` | voice | 3 | glassy slag plates grinding as a heavy body moves, groan |
| `pve.ironworks.slagback.slam.windup` | sfx | 3 | a deep crackling rumble, rock about to break |
| `pve.ironworks.slagback.slam` | sfx | 4 | a heavy body slamming the floor, glassy crack and thud |
| `pve.ironworks.slagback.deflect` | sfx | 4 | a bullet glancing off black glassy slag, sharp tink |
| `pve.ironworks.slagback.shed` | sfx | 2 | slag armour shattering and falling off, glass and rock |
| `pve.ironworks.slagback.emerge` | sfx | 2 | something climbing out of bubbling molten slag |
| `pve.ironworks.ladleman.carry` | loop | 1 | molten metal sloshing in an iron ladle, low bubbling |
| `pve.ironworks.ladleman.pour.windup` | sfx | 3 | molten metal bubbling up, a ladle creaking as it is raised |
| `pve.ironworks.ladleman.pour` | sfx | 3 | molten iron poured onto concrete, roaring hiss and spatter |
| `pve.ironworks.ladleman.swing` | sfx | 3 | a heavy iron ladle swung, deep whoosh |
| `pve.ironworks.ladleman.spill` | voice | 2 | molten metal splashing over a huge zombie, agonised roar |
| `pve.ironworks.ladleman.groan` | voice | 3 | a huge zombie groaning behind a welder's mask, muffled |

Steps: the existing surface pools (`move.step.metal` on the ring, `move.step.concrete` on the floor, `move.step.gravel` on
slag) and zombie steps cover the zone.

## 9. Performance

### 9.1 Budget per room (targets; to measure on the owner's PC)

| Room | Kit parts | Draws | Practical lights | FX quads in view | Notes |
| --- | --- | --- | --- | --- | --- |
| Casting Floor | 1,800 | shared | 8 (6 warm, 2 cool) | 500 | the pour's sparks are the peak |
| Furnace Aisle / Deck | 1,400 | shared | 8 | 400 | doors are emissive parts, not lights |
| Rolling Mill | 1,300 | shared | 8 | 300 | rollers instanced |
| Coke Ovens | 900 | shared | 8 | 300 | smoke sprites |
| Converter Hall | 700 | shared | 8 | 200 + boss | room left for the boss |
| Slag Yard / Forge | 600 | shared | 8 | 200 | |
| East wing (3 rooms) | 900 | shared | 8 | 100 | |
| Whole map | about 7,600 | about 16 instanced draws (Ward Zero 7,124 parts, 15 draws) | 8 per camera (the shader's cap) | buffer 3,000 (the town's 2,800) | |

Emissive surfaces cost nothing in lights. The renders used 308 blocks, about 650 dressing parts, 47 light candidates and
390-520 FX quads per view.

### 9.2 Lights

- The shader takes 8 practical lights. The **proposed picker** scores candidates by intensity x range over distance to the
  camera's focus, keeps 6 warm and reserves 2 for the nearest cool lamps. 47 candidates are listed in the harness (to be
  the kit's table).
- Beyond 8: bake the molten glows into a field (NativeWardZeroField's method) rather than adding lights.

### 9.3 Low graphics fallback

Keeps every gameplay read: molten emissives, hazard and enemy telegraphs, the cool fill. Drops: kit detail parts (about 35%),
the smoke layer under the roof, light shafts, ember motes, sparks to 30%, heat distortion, bloom (the low tier keeps the clip,
so molten surfaces stay bright). Two of the eight lights stay cool at every tier.

## 10. Astra contract (proposal)

### 10.1 New kinds (PveAddStats rows plus new fields)

| id | tier | hp | damage | attack | walk / run | reach | startRange | hit (s) | cycle (s) | lunge | impulse | reward | limb / head frac | stagger |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cinderling` | fodder | 30 | 4 | lunge | 2.8 / 5.0 | 1.8 | 2.4 | 0.38 | 0.90 | 1.1 | 50 | 15 | .35 / .4 | .45 |
| `stoker` | special | 90 | 8 | sweep (+ pull) | 1.4 / 2.8 | 2.6 | 2.2 | 0.50 | 1.40 | 0.3 | 80 | 40 | .35 / .45 | .35 |
| `slagback` | heavy | 320 | 16 | smash | 1.2 / 2.2 | 2.1 | 1.7 | 0.55 | 1.90 | 0.3 | 450 | 90 | .45 / .5 | .2 |
| `ladleman` | elite | 700 | 18 | swing (+ pour) | 0.9 / 1.4 | 2.4 | 2.0 | 0.60 | 2.40 | 0.15 | 600 | 220 | .5 / .55 | .2 |

New fields:
- cinderling: `fireImmune` (paths through live runners), `pop {fuse .45, radius 1.4, damage 6, meleeOnly true}`, core weak point x2.
- stoker: `pull {windup .7, range 4.5, width .6, drag 2.5, dragTime .35, root .4, cooldown 6}`; an interrupt rule (15% of max
  hp or a heavy melee blow in the wind-up).
- slagback: `armour {front 120 deg, reduction .6, breakAfter 120}`, `shed {stagger .8, speedAfter 1.25}`, back weak point x2,
  `turnRate 90 deg/s`.
- ladleman: `pour {windup 1.0, duration 1.2, arc 60 deg, range 4.6, pool 3 s, poolDps 8, cooldown 7}`, `ladle {hp 200, spill
  .35 max hp, stun 2}`, capsule radius .62 height 2.1, `maxAlive` per wave.

### 10.2 AI states

The existing horde states (roam, alert, chase, wind-up, strike, recover, stagger, death) plus: stoker `pull` (chooses the
isolated pilot nearest within 4.5 m, keeps 3-4.5 m otherwise); slagback `shed` (one-shot) and the slow turn; ladleman `pour`
(chooses a cluster), `spilled` (stunned); cinderling `pop` (on death in melee range); cinderling packs offset their chase
angles by +-25 degrees.

### 10.3 Spawn weights

Section 7 (waves, caps, the boss placeholder mix). Spawn records' `allow` gains the new ids once `NativeHordeKinds.Known` and
PveContent know them (`ironworks.spawns[].propose` lists them per record today).

### 10.4 Network fields the presentation needs

| Field | Replicated or derived |
| --- | --- |
| kind id (`addKind`), lastAttack, attack move | replicated (existing) |
| telegraph start and strike time | derived (the strike formula, as today) |
| stoker pull: target id, `pullAt` | replicated (new) |
| stoker pull path and drag pose | derived from `pullAt` and the positions |
| slagback crust intact, `shedAt` | replicated (new) |
| ladleman pour: `pourAt`, `pourYaw` | replicated (new; the yaw because he turns) |
| ladleman ladle intact, `spilledAt` | replicated (new) |
| molten pool extent and expiry | derived from `pourAt`, `pourYaw` and the stats |
| cinderling pop time and place | derived from the death event, its position and the fuse |
| hazards' phase | derived from the instance's hazard clock, as the Factory's |

### 10.5 Parser finding (applies to the live town too)

`NativeExpeditionLayout.Parse` reads a spawn's e, id, kind, from, exit, yaw, level, clear, height and surface only.
`NativeExpeditionM1Arena.BindExpeditionCombat` hands those parsed entries to the horde. A manifest-authored Hulk drop or breach
therefore loses `hulk` and its panel (`block`, `center`, `size`, `normal`), and `song` and `pit` default. The live town's 4 Hulk
drops and 7 breaches are affected today; the Ironworks draft has 2 and 3. **Ask:** carry those fields through the parser (the
validator already checks Hulk spawns by `allow`). The managed check reports this as a note, not a failure.

### 10.6 What a live import needs (none done here)

1. Promote the draft map into `Resources/UnityMaps.json` with navigation: `tools/generate_expedition.py` lists two maps today;
   it needs the foundry (its own nav prefix, scale 1). The managed check already builds importer-style navigation for it
   (about 7,400 nodes and 13,100 links).
2. Add the look to `Resources/MapLooks.json` (it validates beside today's looks) and the area and town door to the manifest
   (a new revision; saves pinned to exp-layout-3 will not resume, the director's own refusal).
3. The hub record's `reserved[]` entry for exp-foundry becomes a route (the IRONWORKS SEALED signs and the shutter's dressing
   change; Claude's side).
4. PvE machines for the instance (PveContent, as Ward Zero's) at the proposed spots; Ward-only paths (`NativeMapRules.WardInstance`,
   breaches, vents) need an instance rule or a generalisation.
5. **Instance gates:** the parser refuses `gates` outside the world. The converter shutter (section 2.3) needs instance gates or
   a door-like closure record.
6. Parse's spawn fields (10.5). Hazard authority for section 5. The four kinds (10.1).
7. The catalogue stays `InDevelopment` until it is playable.

## 11. Files, checks and renders

| File | What |
| --- | --- |
| `docs/arenas/scripts/expedition_ironworks.py` | the generator: blockout, records, spawns, the ironworks record, the town door, the manifest draft, the look, its checks, the plan PNG (`--fixtures`, `--png`, `--ascii`) |
| `Tests/expedition-ironworks-draft.json` | the map entry (UnityMaps shape, nav empty) |
| `Tests/expedition-ironworks-layout-draft.json` | the live exp-layout-3 manifest plus exp-foundry (revision `exp-layout-3-ironworks-draft`) |
| `Tests/expedition-ironworks-look-draft.json` | the look (MapLooks shape) |
| `Tests/test_expedition_ironworks_draft.py` | fixtures are the generator's bytes (twice, deterministic); Resources untouched by a run; the live manifest is exp-layout-3 without the foundry; nothing under Assets names the drafts; the draft is the live manifest plus the foundry only |
| `Tests/ExpeditionIronworksDraftChecks.cs` | run by `ExpeditionLayoutChecks` (`CoreTests --expedition-layout`, the full suite): the live manifest's bytes are exp-layout-3's; the map validates and imports beside today's maps with importer-style navigation; **Astra's `Parse` and `ValidateGeometry` accept the draft manifest** (town door, Ward Zero, the foundry's 41 spawns, entries, caches, boss points with pilot and Hulk routes); the look validates and covers every block |

The generator's checks: both floors reached 100% from the entries; 0 overlapping blocks; no exclusion column under a deck;
every record with pilot or Hulk clearance; all 41 spawn exits routed to entry 0 with their capsule; Hulk routes to every entry,
boss point and the approach; hazard proposals clear of records; the town door's returns Hulk-clear in the live town.

Static analysis only. Not run: Unity, PhysX, a build on the owner's PC, the director, two clients.

## 12. Open questions for the owner

1. **Theme and name:** a steelworks (blast/open-hearth furnaces, casting floor, rolling mill, coke ovens, converters) rather
   than a small foundry. Right?
2. **Scale:** 100 x 68 m, about 12-16 s from the entry to the boss hall. Bigger, smaller?
3. **The four enemies:** Cinderling, Stoker, Slagback, Ladleman. Keep, rename, cut? A fifth idea: the Rigger, a crane rigger
   who drops from the gantries on a chain hook.
4. **Hazards:** molten runners on a cycle and the ladle pour in the arena: is that too much going on during waves?
5. **The converter shutter:** shut the direct route until the boss phase (needs instance gates), or keep it open?
6. **Machines:** a chip machine on the coke walkway (upstairs) as well as in the Pattern Shop?
7. **Boss:** later, as you said. The converter hall is sized for one; any early idea (a foreman, a living converter)?
8. **Brute seam colour in this zone:** OK to tint the brute's seams cream here so they read against the glow?
