# CITY IRONWORKS: the sealed foundry (Expedition instance `exp-foundry`): design draft

Claude, 2026-10-16, branch `claude/ironworks-design` (base shared `claude/sleepy-ritchie-g8ruhv` at `bb4b36a`; shared `afac02d`
with Astra's PR #254 merged in since).
**Status: owner-approved design (2026-10-16; decisions in section 12). Nothing is live.** The blockout, the manifest draft and
the look are inert test fixtures. No game code, AI, networking, live data or catalogue state changed. Central Station is a
separate parallel design.

Owner request (2026-10-16): "lets begin the design for the Central Station and City Ironworks~ we can tweak the instances to
have specific zombies / enemies. So lets also begin creating unique enemies for those zones. We can work on visual updates /
bosses after." Owner bar (2026-10-16, via the coordinator): the Ironworks must look clearly better than Ward Zero, the kind of
map players call "a cool map", using our real assets and lighting. Readability comes first. Owner approval (2026-10-16): "Go
with your picks, add the rigger as well." (section 12).

Roles: Claude owns layout, look, dressing, animation, VFX, audio cues and presentation. Astra owns stats, AI, the director,
networking, hazards' authority and tests of those. Everything marked **proposal** below is for Astra or the owner to accept.

## 0. At a glance

| | |
| --- | --- |
| Ids | area `exp-foundry` (reserved), map `expedition-ironworks` (the id PR #254 gave the importer's draft profile, `NativeMapRules.ExpeditionInstance` and the machine rule; `docs/EXPEDITION-LAYOUT.md` first reserved `expedition-foundry`), look `ironworks`; the town door `exp-city:door:exp-foundry` at the reserved doorway (-84, 0, 0), yaw 90; the shutter `exp-foundry:gate:converter` |
| Size | 100 x 68 m inside the shell (Ward Zero: 76 x 60 m); ceiling 18 m; floors at y 0 and y 6 |
| Floor | 4,453 m2 at ground and 987 m2 on the upper ring and walkways, all reached from the entry slots |
| Blockout | 308 blocks, 5 stairs, 31 pilot exclusion columns, 46 spawn records (8 of them Rigger hang points), 3 caches, 3 boss points, 1 instance gate (the converter shutter), 11 proposed hazards with a Wave 1 and boss schedule, 4 machine spots |
| Rooms | Time Office (entry), Casting Floor (arena), Furnace Aisle and Charging Deck, Rolling Mill, Stockhouse, Pattern Shop, Coke Oven Battery, Slag Yard and Drop Forge, Converter Hall (boss arena placeholder) |
| Run from the entry to the boss arena | north loop 101.4 m (14.9 s running); south loop 109.9 m (16.2 s); direct 83.4 m (12.3 s) only once the shutter opens with the boss |
| New enemies (approved designs; stats are proposals) | Cinderling (swarm), Stoker (puller), Slagback (armoured heavy), Ladleman (elite area denial), Rigger (ambusher off the crane rails) |
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

`python3 docs/arenas/scripts/expedition_ironworks.py --png plan.png` draws the plan with every record (renders below). The
`=` between the casting floor and the converter hall at z 0 is the converter shutter (red in the PNG): shut until the boss.

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
   - **Direct:** the Casting Floor's west opening into the Converter Hall (83.4 m). **Owner decision:** the converter shutter
     seals it until the boss. It is a real instance gate in the draft manifest: `exp-foundry:gate:converter`, `opens: "boss"`
     (PR #254's instance shutter: sealed from the start, opening with the instance's boss encounter over the existing one-second
     closure transition, never with the town's final gate). With it shut, the generator proves every boss point (pilot and
     Hulk) and every spawn exit still routed to the entry by the loops, and no way through the opening. The shutter opening is
     the boss encounter's first beat: the party, arriving by a loop, sees the direct way open behind the boss.
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
| Machine spots (owner-approved, not anchors) | chip and med in the Pattern Shop (46.6, 0, -16 / -24); **the second chip machine upstairs on the coke walkway** (-30, 6, 26.6), yaw 180, on the north loop; recruit in the Time Office (39, 0, 7). Fronts pilot-clear on reached floor, 6.2 m or more from any spawn exit. A live machine needs PveContent authoring for the instance (section 10.6) |
| Converter shutter (instance gate) | `exp-foundry:gate:converter`, opens `boss`: closure (-24, 2.5, 0) 8 x 5 x 0.6 m, yaw 90, filling wall-w's west opening between its piers and under its lintel |
| Sanctuary | none (instances have none, as Ward Zero) |
| Town door (draft manifest) | anchor (-83.4, 0, 0) yaw 90; closure against the sealed shutter; approach (-82.4, 0, 0) (the hub's reserved approach); returns x -79.6 and -78.0, z -3, -1, 1, 3, yaw 90 (the Ward Zero door's mirrored); nearest town spawn 16 m |

### 2.6 Spawns (46; the PveEntry shape plus `allow`)

Existing spawn kinds only (door, vent, drop, breach), so the strict parser accepts them, including PR #254's stricter rules
(allow lists per entry kind, Hulk only on drops, breach panels in the area's own map, level 0..1, song -1..1). The additive
`ironworks.spawns[]` gives each record an **entrance style** for its presentation and the **new kinds it would admit**:

| Style | Count | Kind | Where | Proposed kinds |
| --- | --- | --- | --- | --- |
| furnace-mouth | 3 | door, level 1 | charging doors on the deck: they walk out of the fire | cinderling, stoker |
| ash-door | 3 | door | furnace bases under the deck | cinderling, stoker |
| coke-oven-door | 5 | door (one on the walkway) | the oven doors on the bench | cinderling, stoker |
| door | 6 | door | stores, the flue gallery (deck), the stock yard | mixed |
| slag-trench, slag-pit, mill-pit | 11 | vent (floor) | floor trenches and pits: they climb out of the slag | slagback, cinderling |
| gantry | 3 | vent (ceiling: a drop) | under the roof truss: casting floor (2), rolling mill | cinderling, stoker, **rigger** |
| crane-rail | 5 | vent (ceiling: a drop) | under the crane girders: the north runway (2), the south runway, the crane bridge, the converter hall's east runway | **rigger** |
| bunker-chute | 2 | vent (ceiling, level 1) | the ore chutes over the ore bridge | cinderling |
| catwalk-drop, slag-bank | 3 | drop (brute) | off the ring and the slag bank | slagback |
| crane-bay | 2 | drop (Hulk) | lowered from the crane: casting floor, converter hall | ladleman |
| breach | 3 | breach (Hulk) | bricked-up openings: mill, forge, converter hall | ladleman |

Checks: every exit is on reached floor at its own level with its clear radius and routes back to entry 0 with its own capsule
(Hulk where allowed), with the shutter open and shut; exits are 4.0 m apart at least; none within 4 m of a boss point; none
within 0.5 m of a proposed hazard. **The Rigger's 8 hang points** (`ironworks.spawns[].hang`: the girder block, the hook's
height, the drop height) each have a girder overhead within 1.5 m at the hook height (or the roof truss, kit, at 14.4 m), the
chain's fall clear by 0.6 m up to the hook, 1.6 m clear from the floor to 0.5 m over the drop height, and the 1.5 m landing ring
on reached floor and 2 m clear of every hazard. The bridge point rides the crane bridge's parked position: it is live only while
the ladle crane is parked (Astra: gate it on the crane's phase).

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
| Zone tints (owner decision) | the brute's seams cream `0xFFD27A` instead of `NativeHordeKinds.Seam[Brute]` `0xFF6A2C`, in this instance only; recorded in `ironworks.presentation.seams` (MapLooks has no per-kind field; to implement: the horde presentation swaps the seam colour while this instance is loaded) |

### 3.2 Readability rules (gameplay first)

1. **Heat stays local.** Orange light comes only from molten sources and furnaces, in tight pools (ranges 8-13 m). Fill,
   rim and work light are cool. The renders showed a flat orange room when the warm lights had 20 m ranges and the rim was
   warm; that is the failure mode.
2. **Two of the eight practical light slots are cool** (mercury-vapour lamps), whichever furnace is near (picker rule,
   section 9.2). The cool light keeps the floor, the pilots and the enemies' shapes readable.
3. **Enemies are dark silhouettes with non-orange tells.** The new kinds carry white-hot, cyan, cream or cold-white tells
   (the Rigger's head lamp); nothing an enemy does is signalled in plain orange (the environment's colour). Against furnace
   glow they read as black shapes (the lineup render shows it).
4. **Telegraphs:** cream-white decals with a hard edge for area attacks (slam ring, pour arc), cyan dashes for lines (the
   Stoker's pull), a white flare on the body for fast attacks. The hazard warn colour stays the hazard kit's amber, used only by
   hazards. Molten floor glow never exceeds a telegraph's brightness: molten surfaces sit at HDR 1.4-4, decals at full white
   with alpha.
5. **Existing kinds in this light:** the brute's orange seams (0xff6a2c) vanish against the furnace glow. **Owner decision
   (2026-10-16):** in the Ironworks only, the brute's seams draw cream 0xffd27a (presentation: the seam colour feeds its
   landing cracks and impact ink too, so they turn cream with it). Every other zone keeps `NativeHordeKinds.Seam`. The
   shambler, runner, spitter and Hulk seams read as they are.
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
the doc's proposals, not shipped code. The enemy sheet now carries the Rigger (lineup, its ambush at a real hang point, move,
telegraph, strike).

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
| NativeHordeModels and NativeZombieMotion on the ZOMBIE PRO takes (`tools/pack-zombie-provenance.json`) | the five new kinds' bodies and gaits |
| NativeHordeDrop (warn, leap, fall, recover; the landing slam's dust and cracks) | the Rigger's landing (a lighter profile); the existing kinds' drops from its hang points |
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
9. **A chain prop with a verlet solver** for the Rigger (hang, slide, swing, climb), the crane hooks and the converter hall's
   hook; today a chain would be a rigid stick. Medium payoff, needed before the Rigger can ship.

## 5. Hazards (all proposals for Astra; presentation is Claude's)

Ids start with the map id (`expedition-ironworks-`). Volumes, cycles and the schedule are in `ironworks.hazards[]`. Every
record keeps 2 m (spawn exits 0.5 m) clear. **Owner decision (2026-10-16):** keep the runners and the ladle pour; Wave 1 runs
light; the pour stops for the boss encounter. Waves 2 to 4 run every cycle as listed.

| Id | Kind | Reuse | Cycle (warn / live / period, s) | Wave 1 | Boss encounter | Telegraph | Effect |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `-runner-r2` | molten-runner | new | 2 / 6 / 34 | **on** (the one live runner, arena centre: it teaches the read) | on | the taphole sparks and roars, the trough brightens from the furnace end | burn while live inside the trough |
| `-runner-r1`, `-r3` | molten-runner | new | 2 / 6 / 34 (offsets 0, 11, 22 with r2) | **off** (tapholes plugged, a dull red glow; they tap from Wave 2) | on | as r2 | as r2 |
| `-ladle-crane` | crane | Factory crane, pour instead of drop | 2.5 / 3 / 26 | **half rate** (one pour every 52 s, warn 3.5 s) | **off** (the crane parks at the east end, ladle upright) | amber beacon, klaxon, the ladle tilts, a cream decal on the zone | splash zone burns; the stream blocks sight |
| `-drop-hammer` | press | Factory press, vertical | 1.2 / 0.35 / 6 | on (a fixed, readable press guarding cache 2) | on | the ram lifts, a hiss, a white glint on the billet | crush on the anvil |
| `-slag-spill` | slag-spill | new | 3 / 10 / 40 | **off** (from Wave 3, when the slag yard joins) | on | the pot tips and glows | a tongue spreads east over 3 s, burns, cools over 10 s |
| `-hot-slab` | hot-slab | Factory conveyor idea | 1.5 / 9 / 18 | **off** (from Wave 2: the reheat door opens for the first time as the mill joins) | on | the reheat door opens, a white slab emerges | burns and carries anyone on the table |
| `-flying-shear` | saw-rail | Factory saw-rail / kit saw | 1 / 0.6 / 9 | **off** (runs with the slab) | on | blade drop squeal | cut across the table |
| `-quench-steam` | vent | Factory vent | 1.5 / 3 / 22 | **half rate** (every 44 s) | on | hiss, white mist at the grate | push and scald |
| `-pusher` | pusher | new (a travelling press) | 3 / 6 / 30 | **off** (from Wave 3, when the coke battery joins) | on | amber beacon, horn | crush in its path along the bench |
| `-converter-tilt` | converter | new | boss-driven only | off | boss design | (boss design) | (boss design) |

Wave 1 therefore runs one runner and a slow pour on the casting floor, the drop hammer by cache 2 and the quench steam slowed on
the north loop. Molten pools left by enemies (the Ladleman) share the runner's burn. No hazard is lethal on contact; all are
dodgeable. The generator checks the schedule (one runner live in Wave 1, the pour off for the boss).

## 6. Unique enemies

Built on the pilot-model zombie rig and the ZOMBIE PRO gaits, as every horde kind is. Each reads at 30 m as its own silhouette
and keeps a non-orange tell. Tiers: Cinderling (fodder), Stoker (special), Rigger (special), Slagback (heavy), Ladleman
(elite). The owner kept all four and added the Rigger as the fifth (2026-10-16). Stats are in the Astra contract (section 10);
timings here are the presentation's proposal.

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

### 6.5 Rigger: the ambusher (owner addition, 2026-10-16)

- **Role:** an ambusher and disruptor that comes from above. It waits on the crane girders and the roof truss, lowers its hook
  over an isolated or busy pilot, slides down the chain onto them, swings the hook to scatter the group, then climbs back up
  to do it again. It punishes standing still and turtling under the crane; it gives the casting floor a vertical threat.
  Tier: special (as the Stoker), at most two alive.
- **Silhouette against the furnace glow:** a lean figure hanging from a chain under the girders, the crane hook dangling below
  it, one cold white point of light (its head lamp). On the ground: a thin, long-armed figure trailing a hook on a short chain.
  Its tells are cold white (the lamp, the ring's flash) and cream (the ring); nothing orange. The ambush render shows it under
  the north runway with the furnace line behind it.
- **Rig build:** the shambler body on the pilot-model zombie rig at 1.04 scale, skin dead grey; parts: a dirty cream hard hat
  with a head lamp (emissive cold white plus a spot cone), a webbing harness (crossed straps, a belt, two shackles), gloves, a
  chain coil over the chest, and a crane hook (about 0.6 m) on a chain held in the right hand. The chain is a verlet prop
  (gap 9 in section 4): 30 links between the right hand and the hook, pinned to the girder while it hangs.
- **Animation set:**
  - idle `idle-3` with the hook hanging from the hand; move `walk-2`, `chase-1` at the shambler's tempo with the hook dragging
    (the chain prop on the floor, sparks off the hook's point);
  - **hang** (new, procedural): both hands over the head on the chain, hips under the hands, legs loose, a pendulum sway driven
    by the chain (built on the smash wind-up's arms-up frame held, as the renders do);
  - **slide** (new, procedural): the hang pose run down the chain in 0.35 s, legs tucked, sparks at the gloves;
  - **land**: NativeHordeDrop's landing crouch and recover at a lighter profile (no Hulk-class shake);
  - **hook swing**: `swipe-r1` on the right arm, looped twice overhead with the chain prop. **New mocap wanted:** an overhead
    lasso spin and release (about 1.1 s);
  - **throw and climb back up** (yes, it climbs): a hook throw (0.5 s, `swipe-r1` aimed up) and a hand-over-hand climb. **New
    mocap wanted:** a rope-climb loop (about 1.0 s); the stand-in alternates the hang pose's hands while it rises;
  - **knocked off**: `knock-3` in the air, then the landing; hits `knock-*`; deaths `back-*`, `front-*` (the hook clatters away,
    the lamp flickers out).
- **The ambush (hook drop), telegraph timings** (T = the landing):
  - T-2.0 s: a chain rattle high overhead (positional, from the hang point), dust sifts off the girder;
  - T-1.6 s: the target spot is locked (the pilot's position then). The hook drops out of the dark and stops 2.4 m over the
    spot, swaying 0.6 m either side (0.8 s period): **its shadow swings across the spot**. A cream ring (1.5 m) fades in on the
    floor; the head lamp clicks on and its cold cone lights the ring;
  - T-0.35 s: the Rigger slides down the chain (a whip of chain, sparks at its gloves); the ring flashes white;
  - T 0: it lands: 1.5 m disc, 14 damage, 0.5 s knockdown, a dust ring and sparks off the hook.
  A pilot who moves 1.5 m in the 1.6 s after the lock is safe; the rattle buys another 0.4 s. As a horde entrance from a hang
  point it runs the same sequence onto the spawn record's exit.
- **Hook swing** (on the ground): wind-up 0.6 s (the hook spun overhead, a rising whirr, a cream dashed ring at 3.2 m and a
  cold inner ring at 1 m); a 0.5 s sweep through 300 degrees from its front-left, 9 damage and 3 m knockback outward: it breaks
  up a group. Cooldown 5 s. The hook passes over anyone inside 1 m (the dead zone).
- **Climb back up:** after a swing, if no pilot is within 2 m and it has 40% of its health or more, it throws the hook up to the
  nearest hang point within 8 m and climbs (2.0 s), lurks at least 4 s with its lamp off, then ambushes again. At most two
  climbs per Rigger; then it fights on the ground.
- **VFX:** the lamp halo and cone (Halo, a soft shaft), the ring decal (the telegraph family), the hook's swaying shadow decal,
  girder dust (Mist motes), glove sparks on the slide (Spark), the landing dust ring (NativeHordeFx at 0.6 scale), the swing's
  pale trail along the arc, a chain-cut burst (Spark plus steel HotDebris).
- **Audio:** `pve.ironworks.rigger.*` in section 8 (the rattle is the key warning: it must cut through the furnace bed).
- **Counters and weak points:** keep moving when the rattle sounds; step out of the ring; shoot it while it hangs, slides or
  climbs (it takes 1.5x damage there); **the chain** (40 hp while it hangs or climbs): cut it and it falls (20% of its maximum
  health and a 1.5 s stun); 30% of its maximum health while it hangs or climbs knocks it off the same way; the head (lamp) takes
  the usual head multiplier. The swing: back off past 3.2 m or step inside 1 m; a heavy melee blow in the wind-up staggers it.
- **Spawns:** only from hang points: the 5 crane-rail points and the 3 roof-truss gantries (section 2.6); 8 in the draft.

### 6.6 Concept sheet

`enemy-sheet.png` (renders): the lineup with a pilot and a shambler, backlit by the furnace line and front-lit, the Rigger's
ambush at its real hang point, then each kind moving, telegraphing and striking. `rigger-sheet.png` gathers the Rigger's four.
Built from the real models and gaits with the proposed parts as boxes on the bones.

## 7. Encounter plan (proposal)

| Phase | Rooms active | Hazards | Mix |
| --- | --- | --- | --- |
| Entry (quota 0) | Time Office quiet; first trickle on the casting floor | Wave 1's | shambler, cinderling packs of 3 |
| Wave 1 | casting floor, furnace aisle | light: runner r2 only, the pour every 52 s, quench steam every 44 s, the drop hammer; mill, pusher and slag spill off | shambler .55, cinderling .35, stoker .10 |
| Wave 2 | + rolling mill, stockhouse | all three runners, the full pour, the mill wakes (slab, shear) | shambler .33, cinderling .33, stoker .14, slagback .10, rigger .05 (cap 1), spitter .05 |
| Wave 3 | + coke ovens, slag yard | + pusher, slag spill | shambler .22, cinderling .28, stoker .13, slagback .15, rigger .08 (cap 2), brute .05, ladleman .05 (cap 1), hulk .04 (cap 1) |
| Wave 4 | all but the converter hall | all | shambler .18, cinderling .27, stoker .13, slagback .15, rigger .10 (cap 2), brute .05, ladleman .07 (cap 2), hulk .05 (cap 1) |
| Boss (placeholder) | converter hall; **the shutter opens with the encounter** | **the ladle pour stops** (the crane parks); the rest as Wave 4; the converter tilt is the boss's | adds: cinderling .45, stoker .15, slagback .20, rigger .10 (off the converter runway), shambler .10 |

Per room: furnace aisle and coke ovens favour cinderlings and stokers (fire doors); slag yard, trenches and pits favour
slagbacks; breaches and crane bays are the ladleman's and the Hulk's; the crane rails and gantries are the Rigger's (the
casting floor and the mill; the converter rail in the boss). The Rigger arrives in Wave 2 with a scripted first ambush on the
casting floor (its rattle is new to the players). The director picks spawns away from pilots' sight, as today; two Riggers never
lock the same pilot, and their hang points are 6 m apart or more.

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
| `pve.ironworks.rigger.rattle` | sfx | 4 | chains rattling high overhead on a steel girder, a creak of links, echoing in a vast hall (the ambush warning) |
| `pve.ironworks.rigger.hang` | loop | 1 | a chain creaking under a swaying weight, slow pendulum rhythm, faint |
| `pve.ironworks.rigger.lamp` | sfx | 2 | an old miner's head lamp clicking on, a faint electrical buzz |
| `pve.ironworks.rigger.lower` | sfx | 3 | a heavy crane hook lowered fast on a chain, links running through a pulley, stopping with a jolt |
| `pve.ironworks.rigger.slide` | sfx | 3 | gloves sliding fast down a steel chain, a metallic hiss and a whip of chain |
| `pve.ironworks.rigger.land` | sfx | 4 | a body landing hard on concrete from height, a thud and a chain clattering |
| `pve.ironworks.rigger.swing.windup` | sfx | 3 | a heavy hook spun overhead on a chain, a rising whirr |
| `pve.ironworks.rigger.swing` | sfx | 3 | a crane hook swung on a chain, a deep whoosh and a clank |
| `pve.ironworks.rigger.throw` | sfx | 2 | a hook thrown upward on a chain, catching a steel girder with a clang |
| `pve.ironworks.rigger.climb` | loop | 1 | climbing a hanging chain hand over hand, rhythmic clinks |
| `pve.ironworks.rigger.snap` | sfx | 2 | a chain shot through and snapping, links scattering on concrete |
| `pve.ironworks.rigger.groan` | voice | 3 | a wiry zombie's strained groan from high above, echoing |
| `pve.ironworks.rigger.shriek` | voice | 3 | a zombie screech as it drops from height |

Steps: the existing surface pools (`move.step.metal` on the ring, `move.step.concrete` on the floor, `move.step.gravel` on
slag) and zombie steps cover the zone. The Rigger's rattle and lower cues are positional and loud enough to cut through
`ambience.ironworks.casting`; mix them above the furnace bed.

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
390-520 FX quads per view. The Rigger adds no practical light: its lamp is an emissive part, a halo and a cone quad, the ring
and shadow are decals, and its chain is about 30 instanced links (two Riggers: 60 parts, 8 quads).

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
| `rigger` | special | 120 | 9 | swing (+ hook drop) | 1.5 / 3.4 | 3.2 | 3.0 | 0.60 | 1.60 | 0.2 | 260 | 45 | .35 / .45 | .35 |

New fields:
- cinderling: `fireImmune` (paths through live runners), `pop {fuse .45, radius 1.4, damage 6, meleeOnly true}`, core weak point x2.
- stoker: `pull {windup .7, range 4.5, width .6, drag 2.5, dragTime .35, root .4, cooldown 6}`; an interrupt rule (15% of max
  hp or a heavy melee blow in the wind-up).
- slagback: `armour {front 120 deg, reduction .6, breakAfter 120}`, `shed {stagger .8, speedAfter 1.25}`, back weak point x2,
  `turnRate 90 deg/s`.
- ladleman: `pour {windup 1.0, duration 1.2, arc 60 deg, range 4.6, pool 3 s, poolDps 8, cooldown 7}`, `ladle {hp 200, spill
  .35 max hp, stun 2}`, capsule radius .62 height 2.1, `maxAlive` per wave.
- rigger: `hookDrop {rattle .4, lock 1.25, slide .35, hookHeight 2.4, sway .6, swayPeriod .8, radius 1.5, damage 14, knockdown
  .5}` (T-2.0 rattle, T-1.6 lock, T-0.35 slide, T 0 land); `swing {windup .6, sweep .5, arc 300 deg, radius 3.2, inner 1.0,
  damage 9, knockback 3, cooldown 5}`; `climb {throw .5, speed 5, reach 8, minHealth .4, lurk 4, maxClimbs 2}`; `exposed 1.5`
  (damage taken while hanging, sliding or climbing); `chain {hp 40, fall .2 max hp, stun 1.5}`, `knockOff .3 max hp`; `hangs`
  (the area's entries whose ironworks proposal lists the rigger: the ceiling vents of section 2.6); `maxAlive 2`; capsule as
  the shambler's.

### 10.2 AI states

The existing horde states (roam, alert, chase, wind-up, strike, recover, stagger, death) plus: stoker `pull` (chooses the
isolated pilot nearest within 4.5 m, keeps 3-4.5 m otherwise); slagback `shed` (one-shot) and the slow turn; ladleman `pour`
(chooses a cluster), `spilled` (stunned); cinderling `pop` (on death in melee range); cinderling packs offset their chase
angles by +-25 degrees; rigger `lurk` (on a hang point, lamp off; moves between hang points along the rails out of sight at
4 m/s), `lock` (chooses a pilot within 6 m horizontally of a free hang point, preferring one standing still or in a cluster;
never one another Rigger has locked), `slide` and `land` (the hook drop), then chase and the swing as the existing states,
`throw` and `climb` (back to a hang point within 8 m), `knocked` (the chain cut or the knock-off threshold while hanging or
climbing: it falls and is stunned).

### 10.3 Spawn weights

Section 7 (waves, caps, the boss placeholder mix). Spawn records' `allow` gains the new ids once `NativeHordeKinds.Known` and
PveContent know them (`ironworks.spawns[].propose` lists them per record today). The Rigger spawns only from hang points and
needs its own entry timeline (`NativeWardEntries.Timeline` gives ceiling vents the drop profile today).

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
| rigger hang point (entry index or -1), `lockAt`, the locked landing point (x, z) | replicated (new) |
| rigger rattle, hook drop, sway, ring, lamp, slide and landing times | derived from `lockAt` and the stats |
| rigger hook and body positions on the chain while it hangs, slides or climbs | derived from the hang point, `lockAt` or `climbAt` and the stats |
| rigger `climbAt` and its target hang index | replicated (new) |
| rigger `chainCutAt` (or knocked off) | replicated (new); the fall derived from it and the position |
| rigger swing | the existing lastAttack and attack move; the arc derived |
| hazards' phase | derived from the instance's hazard clock, as the Factory's |

### 10.5 Parser finding: fixed by PR #254

The first draft found that `NativeExpeditionLayout.Parse` dropped a spawn's `hulk` flag and breach panel (`block`, `center`,
`size`, `normal`), `song` and `pit`. Astra's PR #254 (merged into shared at `afac02d`) keeps them and validates them strictly:
allow lists per entry kind (`NativeWardEntries.Allows`), the Hulk flag only on drops, breach panels on static blocks of the
area's own map with a finite box and a unit normal, level 0..1, song -1..1. `NativeExpeditionLayout.BindContent` hands the bound
entries to authority and guests alike. The managed check's old note is now a PASS: all 46 parsed spawns keep every authored
field (the 2 Hulk drops their flag, the 3 breaches their panels; pit, song, level, height, clear, allow), and `BindContent` hands
the horde exactly those entries, the first boss point and the eight entry pads, with no Ward machines. The live town's 4 Hulk
drops and 7 breaches are covered by the same fix.

### 10.6 What a live import needs (PR #254 did part of it; nothing live here)

Done by PR #254: the importer's draft profile for this map (`'expedition-ironworks': ('ironworks', 'ei', 2)`: it reads
`Tests/expedition-ironworks-draft.json`, the file this draft already writes, at 0.5 m); `NativeMapRules.ExpeditionInstance` and
`NativeMachineRules.Present` recognise the map; the spawn fields (10.5); instance gates with `opens: "boss"` (the shutter is in
the draft, and the managed check runs the director on it: sealed from the start, still sealed through the waves, opening with
the instance's boss encounter, clear after the transition, the town's final gate untouched). This draft renamed its map from
`expedition-foundry` to `expedition-ironworks` to meet those (the area id `exp-foundry` is unchanged).

Still to do:
1. **Import:** add the area and the town door to `Tests/expedition-layout-draft.json` (a new manifest revision; saves pinned to
   exp-layout-3 will not resume, the director's own refusal) and run `tools/generate_expedition.py`. The importer resolves this
   draft manifest's maps, blockHashes and the Ironworks navigation today (the Python test runs it: about 29,800 nodes and 56,300
   links; the managed check's C# rebuild of the same 0.5 m grid gives 29,848 and 56,329, its probe being the block capsule).
2. Add the look to `Resources/MapLooks.json` (it validates beside today's looks).
3. The hub record's `reserved[]` entry for exp-foundry becomes a route (the IRONWORKS SEALED signs and the shutter's dressing
   change; Claude's side).
4. PvE machines for the instance at the approved spots (section 2.5): machine authoring and table support (Astra's open item;
   `BindContent` gives authored instances no Ward machines).
5. Hazard authority, schedule (section 5) and replication; generic pit vents; the five kinds (10.1) with the Rigger's entry
   timeline; the zone seam tint (3.2). Presentation still reads `StageContent.entries` in `NativeHordeBreachPresentation`: use
   the bound entries for authored maps (Astra's note on PR #254).
6. The catalogue stays `InDevelopment` until it is playable.

## 11. Files, checks and renders

| File | What |
| --- | --- |
| `docs/arenas/scripts/expedition_ironworks.py` | the generator: blockout, records, spawns and Rigger hang points, the shutter gate, the ironworks record (hazard schedule, machine spots, presentation tints), the town door, the manifest draft, the look, its checks, the plan PNG (`--fixtures`, `--png`, `--ascii`) |
| `Tests/expedition-ironworks-draft.json` | the map entry `expedition-ironworks` (UnityMaps shape, nav empty: the importer builds it) |
| `Tests/expedition-ironworks-layout-draft.json` | the live exp-layout-3 manifest plus exp-foundry with its shutter gate (revision `exp-layout-3-ironworks-draft`) |
| `Tests/expedition-ironworks-look-draft.json` | the look (MapLooks shape) |
| `Tests/test_expedition_ironworks_draft.py` | fixtures are the generator's bytes (twice, deterministic) and its checks pass (shutter, Rigger hang points, schedule, machines); Resources untouched by a run; the live manifest is exp-layout-3 without the Ironworks; nothing under Assets names the drafts; the draft is the live manifest plus the foundry only; **the importer** resolves the draft manifest's maps and blockHashes and builds the Ironworks navigation (0.5 m, `ei`), while the manifest it imports names no Ironworks |
| `Tests/ExpeditionIronworksDraftChecks.cs` | run by `ExpeditionLayoutChecks` (`CoreTests --expedition-layout`, the full suite): the live manifest's bytes are exp-layout-3's; the importer's profile and its manifest; the map validates and imports beside today's maps with the importer's 0.5 m navigation; **Astra's `Parse` (PR #254's strict spawn reader) and `ValidateGeometry` accept the draft manifest** (town door, Ward Zero, the foundry's 46 spawns, entries, caches, boss points with pilot and Hulk routes, breach panels in its own map); every spawn field kept and bound by `BindContent`; **the shutter runs in the director** (sealed, opens with the boss, never the final gate); the record (five kinds, schedule, hang points, machine spots, the seam tint); the look validates and covers every block |

The generator's checks: both floors reached 100% from the entries; 0 overlapping blocks; no exclusion column under a deck;
every record with pilot or Hulk clearance; all 46 spawn exits routed to entry 0 with their capsule, with the shutter open and
shut; Hulk routes to every entry, boss point and the approach; the shutter fills its opening, overlaps nothing and leaves the
loops as the only way to the boss points; the Rigger's 8 hang points (girder, chain fall, landing ring); hazard proposals
clear of records with one runner and a slower pour in Wave 1 and no pour in the boss encounter; machine fronts clear, the
second chip machine upstairs; the town door's returns Hulk-clear in the live town.

Static analysis only. Not run: Unity, PhysX, a build on the owner's PC, the director, two clients.

## 12. Owner decisions (2026-10-16)

The owner answered the open questions: "Go with your picks, add the rigger as well." Numbered 8 to 14 as in the decision list
relayed with the approval; they answer this doc's former questions 1 to 8.

| # | Question (old section 12) | Decision | Where it lands |
| --- | --- | --- | --- |
| 8 | Theme and scale (questions 1, 2) | A full steelworks at 100 x 68 m | sections 1 and 2 as drafted; no layout change |
| 9 | The enemies (question 3) | Keep the Cinderling, Stoker, Slagback and Ladleman; **add the Rigger as the fifth** | 6.5 (design), 2.6 (8 hang points with clearance checks), 7 (from Wave 2), 8 (`pve.ironworks.rigger.*`), 10 (contract row, fields, AI, network); renders `enemy-sheet.png`, `rigger-sheet.png` |
| 10 | Hazards during waves (question 4) | Keep the molten runners and the ladle pour; **the pour stops in the boss encounter; Wave 1 runs light** (r1 and r3 off, the pour at half rate, the hot slab, shear, pusher and slag spill off, the quench steam at half rate) | 5 (schedule columns), 7; `ironworks.hazards[].schedule`; generator check |
| 11 | The converter shutter (question 5) | Shut until the boss: **a real instance gate** `exp-foundry:gate:converter`, `opens: "boss"` (PR #254) | 2.3, 2.5; the draft manifest's `gates[]`; generator checks (fills the opening, nothing stranded) and the managed check (parsed, then the director keeps it sealed until the boss encounter) |
| 12 | Machines (question 6) | A second chip machine upstairs on the coke walkway | 2.5; `ironworks.machines[]` (status approved spot, level 1); generator check |
| 13 | Boss (question 7) | Later; keep the converter hall placeholder | 7; `ironworks.arena` (boss none yet) |
| 14 | Brute seams (question 8) | Cream in the Ironworks only (presentation) | 3.1, 3.2; `ironworks.presentation.seams` |

Implementation note (Claude, not an owner decision): the draft's map id is now `expedition-ironworks`, the id PR #254 gave the
importer's draft profile, `NativeMapRules.ExpeditionInstance` and `NativeMachineRules.Present`; `docs/EXPEDITION-LAYOUT.md`'s
reserved row is updated to match. The area id `exp-foundry` and the town door's ids are unchanged.

Open for later: the boss (13); Astra's acceptance of the contract numbers (10.1) and the hazard authority; the new mocap takes
the Rigger and Ladleman want (6.4, 6.5).
