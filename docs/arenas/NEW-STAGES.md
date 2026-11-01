# Chip Arena: five new stages and the Training Room (plan, M1 blockouts)

2026-10-31 · Claude (presentation lane) · for the owner (decides) and Astra (collision, hazards, spawns, bots, network)

Owner request (2026-10-31): "Can we begin development on the new stages utilizing high quality designs? I would like a
smaller, flat, 1v1 map as well. Lets make use of assets if possible. Hazards are good to add too, not needed on each
addition though. Lets also add a training room level. Good for testing things. Lets add, another... 5 stages ?"

Read as: **five new stages, one of them the small flat 1v1 map, plus a Training Room** (a sandbox level, not a sixth PvP
stage).

Status: **M1 simulation integrated on `codex/unity-m1-stages-training` (base `2066db79`); presentation and Windows playtest pending.**
- Each stage has a draft generator in `docs/arenas/scripts/<id>_m1.py` that writes the map in the `UnityMaps.json` shape and
  a top-down plan, and checks spawn sightlines, spacing, hazard clearance, bounds and reachability.
- All six are generated through `unity/ChipArenaConversion/tools/generate_stage_hazards.py`. Five are selectable with
  `rotation: false`; `training` is sandbox-only and rejected by PvP selection. Draft geometry, IDs and timings are preserved.

## At a glance

| # | Stage (id) | Play area | Built for | Light (one identity each, §2.5 of ARENA_PROPOSAL) | Landmark | Hazards |
|---|---|---|---|---|---|---|
| 1 | **Breakwater** (`breakwater`) | 40×26 m, flat | 1v1 duel (2v2 fits) | Blue hour: the sun just down, lamps coming on | The lighthouse at the pier head | **None** (a clean duel) |
| 2 | **Pier 9** (`harbor`) | 80×60 m | 4v4, FFA 8 | Night, sodium floodlights | Ship-to-shore cranes over a moored freighter | Gantry **container drop**; the **quay edge** |
| 3 | **Skyframe** (`skyframe`) | 56×48 m, 3 levels | 3v3 to 4v4 | Clear high noon above a hazy city | The tower crane through the core | Tower-crane **beam drop**; open **edges** |
| 4 | **Deepwell** (`rig`) | 56×44 m, 3 decks | 3v3 to 4v4 | Night storm: rain, lightning, the flare's glow | The flare stack and the derrick | **Flare burners** on the walkways; **sea falls** |
| 5 | **Polaris Station** (`polar`) | 64×52 m | 4v4, FFA 8 | Polar night under an aurora | The radome | **None** in M1 (optional steam vents) |
| T | **Calibration Bay** (`training`) | 96×56 m interior | Solo and co-op testing | Neutral studio light, the grid | The range's distance boards | Hazard bay samples (sandbox clock only) |

- **Assets:** Toon Harbor is the only banked environment kit (`kit-harbor.json`, 255 props). All five stages use it:
  - containers, crates, pallets, concrete blocks, tanks, barrels, pipes, scaffolding, fences, lights, beacons, sirens,
    buoys, forklifts, trucks, cranes, tugs, freighters, metal-structure buildings.
  - Skyframe and Polaris use only its industrial pieces (scaffold, containers as site cabins and station modules, tanks,
    lights, vehicles), so the roster does not read as five harbours.
  - Other owned packs (the drive's Synty, Polyart and Staggart sets, stylized water) can re-dress a stage later. They are
    not banked yet, which needs a PC pass. A stage's collision never depends on which kit draws it.
- **Variety against today's roster:**
  - Brickfall (golden hour street), the Factory (interior, first shift), Cyber City (neon rain), Ward Zero (sealed
    interior).
  - The new ones add a duel size, a night port, a sky-high frame, a storm at sea and polar night.

## Rules every blockout follows

These come from ARENA_PROPOSAL §2.1 and the Factory and Cyber City drafts.

- **Coordinates and ids:**
  - x east, z north, y up, metres (the Unity frame).
  - Ids are `<map id>-...`. Tokens `n s e w a b` swap in a mirrored partner's id (`mirror_id`).
- **Symmetry:**
  - Team maps are point symmetric, (x, z) → (−x, −z), unless the section says otherwise.
  - Pier 9 mirrors across x = 0 so both teams have the same water.
- **What looks like cover is cover:**
  - Collision boxes are the kit's real footprints. Below 3 m, a prop never shows more than 0.25 m beyond its box.
  - Half cover is 1.0 to 1.2 m and full cover at least 2.4 m. Nothing is between 1.3 and 2.2 m unless it is a deliberate
    window or sill: eye heights are 1.2, 1.6 and 1.9 m, and in-between tops make head-glitch ambiguity.
- **Kit grids:**
  - Containers are 2.4 wide, 2.6 tall, 6.0 or 12.2 long (20 and 40 ft).
  - Crates are 1.2 m; a pallet is 1.2 × 1.0 × 0.15 m; a concrete block is 1.6 × 0.8 × 0.8 m.
- **Movement:**
  - The capsule radius is 0.42 m and eyes are about 1.6 m. Stairs have risers of at most 0.2 m and are at least 1.6 m wide.
  - Alleys are at least 2.4 m (container gaps 3.2 m).
  - Bots jump anything their obstacle ray hits (it starts about 0.45 m up), so every parapet or rail of 1.2 m or less
    over a drop gets a `pilotBlocks` column above it. Unstandable tops (walls, roofs, the lighthouse) get one too.
- **Spawns (each draft checks these and exits 1 on a failure):**
  - Every FFA pair and every A–B pair has no line of sight. The check jitters both ends ±1 m and uses eyes at 1.2, 1.6
    and 1.9 m.
  - Spawns keep at least 3 m from any hazard volume. They stay inside the bounds minus the capsule.
  - Every spawn is reachable on foot (the ground raster flood fill, plus the stairs on layered maps).
- **Falls:**
  - The sim clamps a pilot to the bounds minus the capsule (`Core/NativeUnityMaps.cs` `Clamp`). A fall only exists where
    the bounds run past the floor's edge (Pier 9's quay, Skyframe's open sides) or where a notch is cut into a deck inside
    the bounds (Deepwell's landings).
  - The void beyond has no standable top, so falling there reaches killY.
- **Sim data:**
  - Decor never goes into sim data. Every prop is sent in every 20 Hz frame, so blockouts keep each map at about 350
    blocks or fewer, and replicated props at round start stay at 200 or fewer after the static split.
- **Hazards:**
  - Every hazard reuses a kind the sim already has: `crane` (zones), `vent`, `burner`, `saw-*`, `press`, `grid`.
  - Each is idle for the fight's first 4 s, and telegraphed at least as long as the same kind is today:
    - 0.8 s for a vent or the press;
    - 1.5 s for a burner;
    - 1.6 s for a saw;
    - 2.5 s for a grid;
    - 3 s for a drop from above.
  - Telegraphs are never reduced by graphics settings.

## 1. Breakwater (`breakwater`): the 1v1 duel

**Astra M1 integration:** selectable for up to four FFA pilots or teams of at most two (1v1/2v2).
The first four FFA points are used. No geometry or hazard changes. Temporary music: `cybercity-neon-riot`.

**Pitch.** The end of a stone pier at blue hour. A short, flat, symmetric slab with no high ground. Spawns face each other
across 35 m of cover. The lighthouse beam sweeps over the duel.

- **Size:**
  - The slab is x ±21.2, z ±14.2, and the play bounds are x ±20, z ±13.
  - A 1.2 m stone parapet rings it: the breakwater's wall, with a `pilotBlocks` column above so nobody stands on it.
  - Beyond the parapet is decor only: the sea, tetrapods, a moored tug, the lighthouse at the east tip, the harbour
    lights to the west.
- **Flat:**
  - Everything is on y = 0. There are no stairs and no standable tops above 1.2 m.
  - Crates and barriers are the only things to stand on.
- **Cover (point symmetric; A's north is B's south):**
  - **The chicane:** two 20 ft containers staggered across the spawn line, with a 2.4 m slot between them. The middle is
    never a straight shootout.
  - **The booths:** a harbourmaster's booth on each long side, roofed at 3 m and open toward the pier wall. A 2.4 m lane
    runs behind each.
  - **A's north is the container corner** (close quarters): a corner container flush to the parapet, a 2 × 3 crate stack,
    and a barrier line on the lane.
  - **A's south is the open apron** (long duels over half cover):
    - a crate stack flush to the parapet that breaks the lane;
    - a winch house;
    - two jersey-barrier pairs;
    - oil drums at the spawn's south exit.
  - **Mid stack:** a 1 × 2 crate stack in each side's spine band, off the duel line.
  - **Spawn screens:** a container 2 m in front of each spawn (the "spawn box"), exits north and south.
  - **Breakable:** crates (34 hp each) and drums (150 hp); containers, booths, winch houses and barriers never fall.
  - The draft also checks that no 0.6 m-wide sight lane runs longer than 32 m. The play length is 40 m.
- **Spawns:**
  - Duel A (−17.5, 0) and B (17.5, 0), 35 m apart, each behind its screen.
  - Teams: all four team points sit in the spawn pocket.
  - FFA 4: the two duel points plus one inside each booth.
- **Hazards:** none. Duels stay pure. The water is outside the parapet, so there is no fall.
- **Kit:**
  - Toon Harbor containers, crates, concrete blocks, pallets, barrels, bollards and lamps on the parapet.
  - Tetrapods, buoys and the tug are outside the bounds.
  - The lighthouse is a bank metal-structure piece, or a kit tower capped by a beacon.
- **Look (`breakwater-blue-hour`):**
  - A deep blue sky with a last orange band low in the west. A cool key from the west at 0.3.
  - Warm lamp pools on the parapet (practical lights). The lighthouse beam is a slow-sweeping volumetric cone.
  - Wet stone, spray over the parapet on the windward side.

## 2. Pier 9 (`harbor`): the night container terminal

**Astra M1 integration:** no geometry/timing changes; existing `killY = -6` is used for the quay fall.
The proposed `killVolumes` key is omitted from generated runtime data, so death occurs at -6 rather than the proposed
water surface threshold of -1. Bounds remain z +34. Handrails are left as drafted for initial playtest; adding them would
change combat geometry. Temporary music: `factory-anvil-loop`.

Full pitch in `HARBOR_PROPOSAL.md`. **M1 change: the quay moves to the north edge, and the map mirrors across x = 0.**
In the proposal only team B had the water at its back. Now both teams share it, and the three lanes stay:

- **North, the Quay** (z +18..+30):
  - An open apron along the berth. The freighter's hull is beyond the edge.
  - Bollards and the ship-to-shore cranes' sill beams are low cover.
  - The STS portal deck at 9 m over the centre, reached by one stair from each side, is the exposed power position.
  - The edge at z = +30 drops 2.2 m to the water, a kill.
- **Middle, the Gantry Yard** (z −6..+14):
  - Container rows two and three high with 3.2 m alleys, under the rail-mounted gantry crane.
  - The central cross aisle (z ≈ +4) holds the five painted drop slots.
  - Container tops are walkways at 2.6 and 5.2 m, reached by stairs at the row ends.
- **South, the Transit Shed** (z −30..−10):
  - An open-sided shed (roof 7 m, unstandable): pallet racks, crate stacks, drum cages and two parked forklifts.
  - Close quarters, with side bays every 12 to 16 m.
- **Spawns:**
  - Team A in the west truck yard, team B in the east one (each a gatehouse, reefers and a truck).
  - FFA 8 over the three lanes and both ends.
- **Hazards:**
  - **Container drop:** kind `crane` with five zones in the cross aisle. Every 18 s, with a 3 s strobe over the slot,
    it kills under the load; the landed container is cover for 8 s.
  - **The quay edge:** a kill volume over the water (z > +30.2, y < −1). A splash event for presentation.
- **Bounds:** x ±40, z −30..+34 (4 m past the quay edge, so the fall exists), ceiling 20, killY −6.

## 3. Skyframe (`skyframe`): the half-built tower

**Astra M1 integration:** crane fall, regrip, hoist, impact cue, lethal height and landed collider now use each
zone's floor y. Timings and geometry unchanged. Current movement has no general landing-damage rule: the 3.6 m
internal drops remain free of fall damage; external drops still reach killY. Keep the 7.2 m FFA perches for playtest.
Actual draft bounds x ±32 are retained (the original pitch below says ±28). Temporary music: `cybercity-neon-riot`.

The ARENA_PROPOSAL alternative, simplified for M1: **no hinge or drop groups**. Structure falls are a later milestone
built on Brickfall's segment system.

- **Setting:**
  - Floor 31 of a steel high-rise, between two brick party walls (north and south, 12 m tall, indestructible).
  - Open to the sky and the city east and west.
- **Levels:**
  - 0 m: the poured slab, all of the play area.
  - 3.6 m: deck plates on the steel frame over the east and west halves, joined by two beam bridges.
  - 7.2 m: the tower crane's collar ring at the centre and two scaffold perches.
  - Scaffold stairs between levels (risers of 0.2 m or less), at least three routes up on each side.
- **Edges:**
  - The open east and west edges have a 1.1 m guard rail (with pilot blocks) except at the two material-hoist landings
    each side. Those are open drops, kills: knockback matters.
  - Deck plates have holes that drop to the level below, never off the building.
- **Spawns:**
  - Team A at the west hoist core, team B at the east one, each behind the core's blockwork.
  - FFA 8 spread over all three levels.
- **Hazard: the tower-crane beam drop.**
  - Kind `crane`: the trolley runs along the jib (x) at 16 m and lowers a steel beam bundle onto one of four painted
    zones (two on 0 m, two on the 3.6 m decks).
  - Every 20 s, with a 3 s telegraph; it kills under the load; the landed bundle is cover for 6 s.
- **Bounds:** x ±28, z ±24, ceiling 24, killY −6.
- **Kit:**
  - Scaffolding, concrete blocks, pallets of blocks, site containers (as cabins), fences (the edge mesh), lights.
  - Steel columns and beams are block-built (I-section kit parts drawn on the blocks).
- **Look (`skyframe-high-noon`):**
  - A hard sun almost overhead, short shadows and a deep blue zenith.
  - A pale haze over the city far below, rising dust, wind-snapped safety netting, sun glints on the steel.

## 4. Deepwell (`rig`): the offshore platform in a storm

**Astra M1 integration:** geometry and timing unchanged. Navigation samples at 0.5 m here: the existing 1 m
grid isolated some main-deck spawn pockets despite the draft's finer on-foot checks. Burners and vents dispatch by kind,
not Ward Zero map ID. Keep 8 HP per 0.25 s tick: 32 HP/s, 10 ticks / 80 HP per complete 2.5 s burst against 100 base HP
before damage modifiers (Ward Zero is 100 HP per burst). Sea notches retain killY. Temporary music: `factory-anvil-loop`.

- **Setting:**
  - A production platform in the North Sea at night in a gale.
  - Rain, lightning flashes and the flare stack's orange roar at the north end, outside the bounds.
- **Decks:**
  - 0 m: the cellar deck, grating and pipework, over the sea.
  - 4.2 m: the main deck, process modules and pipe racks.
  - 8.4 m: the drill floor around the derrick's base at the centre (about 12 × 12 m; the derrick legs are cover).
  - Stairs between decks at both ends and both sides (risers of 0.2 m or less).
- **Edges:**
  - Handrails of 1.1 m with pilot blocks everywhere, except the two boat landings and the two lifeboat stations. These
    are open drops to the sea, kills.
- **Hazards:**
  - **Flare burners** (kind `burner`, non-lethal damage ticks): on the north and south walkways of the main deck by the
    flare knock-out drums. They fire on a shared clock with a 1.5 s warning: risk on the quick route.
  - **The sea:** killY. Presentation adds splashes and spray.
  - Optional: **blowdown vents** (kind `vent`) on the cellar deck.
- **Spawns:**
  - Team A in the west accommodation module, team B in the east process end.
  - The ends are point symmetric in collision; only the decor differs.
  - FFA 8.
- **Bounds:** x ±28, z ±22, ceiling 22, killY −6.
- **Kit:** tanks, pipes and pipe racks, scaffolding, metal structures, barrels, beacons, sirens, the cranes (the platform's
  pedestal cranes, decor), lights.
- **Look (`rig-storm`):**
  - A near-black sky lit by lightning, a cold key at 0.15 and sodium deck lights.
  - The flare's warm flicker on everything facing north; wet steel sheen; rain streaks; spray over the edges.

## 5. Polaris Station (`polar`): the research station at polar night

**Astra M1 integration:** no geometry or hazard changes. Temporary music: `cybercity-neon-riot`.

- **Setting:**
  - An Antarctic research station on the ice shelf under a green and violet aurora.
  - Snowfield, berms and drifts; station modules on skids; a central radome.
- **Layout (point symmetric):**
  - **Centre:** the radome hall, an octagon about 12 m across with four doors and a mezzanine at 3.2 m. The interior is
    for close quarters, the roof is unstandable, and the dome is the landmark.
  - **Mid ring:** container modules on skids (top 2.9 m; roof walkways reached by stairs) linked by gaps of 3.2 m or more.
    Fuel tank pairs. A drill tower.
  - **Snowfield flanks:** berms 0.9 to 1.2 m (half cover, stepped), a parked snowcat (a kit truck), a sled cargo line.
  - **Bases:** the garage at each end (an open-sided shed, 3.6 m eaves) with a generator hut.
- **Spawns:** team A in the west garage, team B in the east one; FFA 8.
- **Hazards:** none in M1. The station is the clean mid-size team map. Optional later: geothermal steam vents (kind `vent`).
- **Bounds:** x ±32, z ±26, ceiling 18, killY −6.
- **Kit:** containers (station modules), tanks, metal-structure buildings, fences, lights, beacons, trucks and forklifts
  (station vehicles), scaffolding (the drill tower).
- **Look (`polar-aurora`):**
  - A cold moonless key at 0.12. The aurora's moving green and violet bands light the snow from above.
  - Warm window light from the modules, blowing snow, frost on metal, breath in the cold.

## T. Calibration Bay (`training`): the Training Room

**Astra M1 integration:** owner confirmed entry for every player; DEV tools retain the existing verified-DEV
gates. Home has a solo Training Room entry. In a hosted room, enable Training Room (co-op), ready the party, then enter;
up to eight humans plus 28 authored target actors. Human spawn positions/yaws are the authored north-facing pad points.
Training never enters PvP lists or Random, earns no progression, never resolves a competitive round, and has no overtime
damage. Target actors are static except the rail target (smooth ±3 m motion, peak speed 2.5 m/s); melee targets retain
their inward yaw. Dead targets and humans restore after five seconds; a reconnect also restores a dead human.

`ContentMap.trainingPads` carries the draft data. `NativeArena.TrainingReadouts()` and replicated
`NativeGameFrame.trainingReadouts` provide one record per pad: `pad`, `actor`, cumulative actual HP `total`,
`last`, `headshot`, `sourceActor`, `source` (weapon/environment plus hit kind), centre-to-centre `distance`,
last-hit `at`, and `dps` (actual damage in (now-5, now], divided by five). Overkill is excluded; deaths do not clear totals.
The host's pause-menu reset clears all readouts and rebuilds targets and all three destruction samples at authored HP,
along with the rest of the map; the normal frame generation change replicates that reset. No separate dummy combat path.
The five authored hazard samples loop on fight time; existing DEV hazard-trigger/at commands can trigger their previews.
No saw-rail/burner samples are added in M1. Look-only marks remain Claude presentation constants; no trainingMarks field
is introduced. Temporary music: `factory-anvil-loop`. No geometry/timing changes.

A sandbox-only interior: a hangar with a 1 m floor grid, built to test weapons, abilities, movement and hazards. It is not
in PvP rotation or the PvP map list. The menu entry and its mode are Astra's.

- **Hall:** x ±48, z ±28, a 14 m roof (unstandable). The grid is look only.
- **Zones:**
  - **Spawn pad** (0, −20), facing north, with the loadout terminal.
  - **Firing range** (north strip, z +12..+26):
    - A firing line at x = −42 facing +x, with four lanes 3.5 m wide divided by 0.5 m kerbs.
    - Dummy pads at 10, 20, 30, 45, 60 and 80 m, and distance boards over each.
    - One lane carries a strafing-target rail at 20 m (Astra moves the pad).
  - **Melee ring** (centre): a 14 m octagon with a 0.3 m kerb and four dummy pads.
  - **Movement course** (south-west):
    - Steps at 0.2, 0.45, 0.65, 1.0, 1.5, 2.0, 2.5 and 3.0 m.
    - A gap run of 2 m platforms with gaps of 2, 3, 4, 5 and 6 m.
    - A 3-level tower (0, 3.6, 7.2 m) with standard stairs.
    - A sprint straight 30 m long, marked every 5 m.
  - **Cover lab** (south-east):
    - Standard cover pieces: half 1.0 and 1.2 m, full 2.4 m.
    - A wall with a window (sill 1.0 m, head 2.0 m) and a door 2.2 m high.
    - Destruction samples by colour code: a crate (wood, hp 34), a brick panel (masonry) and a chevron column
      (structural).
  - **Hazard bay** (far south-east, behind a 3 m wall with a door): one sample of each hazard kind on the sandbox clock.
    The DEV preview triggers them by hand.
- **Proposed data:**
  - `trainingPads[]` in the draft lists each dummy pad (id, pos, yaw, kind `static`, `strafe` or `melee`, lane distance).
  - Astra decides the real field.
- **Look (`training-studio`):**
  - Even, shadow-soft light; a neutral grey floor with the 1 m grid and the 5 m lines bolder.
  - Cyan lane markers and the destruction colour code on the samples.
  - A DPS and hit readout board over the range (presentation; it reads the sandbox's damage events).

### Training Room presentation (Claude, 2026-11-01; branch `claude/training-room-look`)

Presentation only: no simulation, rule, network, save, damage or timing change. Marks stay presentation constants derived
from the map (`trainingPads`, blocks, hazards, bounds) plus two draft constants the map does not carry (the sprint straight,
the ring's 7 m apothem); no `trainingMarks` field.

- **Look (`training-studio`, MapLooks.json):** a sealed studio (sky `none`), a bright neutral key and rim with no shadows
  (the roof block would shade the whole hall), a high trilight, light fog from 70 m, three soft practicals (hazard bay,
  backstop, spawn), the anime style on with a neutral grade, and block looks for every training id (light grey floor and
  walls, dark kerbs, the ring's orange kerb, the samples by colour code, the banded structural column). Its `kitLooks`
  give the bay's rig parts the Factory kit's looks.
- **Floor and wall paint** (Core `NativeTrainingMarks`, one SourceLit mesh without outlines):
  - a metre grid with bolder 5 m and 10 m lines over the hall, and over the lanes a grid measured from the firing line
    (Resources/NativeTrainingStudio.shader, multiplied over the floor);
  - the firing line, a shooter's box and number per lane, dashed lane edges, a 1 m ruler along the barrier with numbers,
    cyan lane markers along the kerb tops;
  - per pad: a puck in its kind's colour (cyan static, magenta strafe, orange melee) with a crosshair and facing chevron,
    and an ID plate with the authored distance label ("20 M") and the pad ID ("L1-20"); the strafe pad's rail from
    `rail.a` to `rail.b` with lit edges, end stops and ±metre ticks; the melee pads' 1, 2 and 3 m reach arcs;
  - the spawn pad, the sprint straight's 5 m marks and run-out, step heights on tops and risers, gap widths, deck heights,
    cover heights, the lab wall's sill, head and door ticks, the samples' colour plates with their hp, the bay's sign and
    each sample's name, zone labels, the walls' wainscot, seams, titles and height rulers.
- **Fixtures** (outlined): the readout boards and their hangers, the roof's light panels.
- **Readout boards:** one board per authored distance, hung over its distance line (bottom 7 m), a cell over each lane;
  the ring's board north of the ring (bottom 5.8 m). Each cell shows the pad's header, total HP, last hit with a HEAD badge,
  rolling 5 s DPS, source (weapon or hazard and hit kind) and distance. A floating tag over each recently hit target
  (6 s hold, 2 s fade, the 3 most recent only) shows the same cell, camera-facing at a share of the view's height.
  Rows come from `NativeArena.TrainingReadouts()` (the host's meters, a guest's replicated `TrainingRemote`) at 10 Hz; a
  cell's texture is repainted only when a drawn value changes.
- **Hazard bay:** the existing hazard presentation draws these kinds only on the Factory (its crane is the Factory hall's
  bridge), so Core `NativeTrainingBay` draws the five samples from the simulation's pure clocks (`NativeFactoryHazards.State`,
  `NativeCyberCityHazards.At`) with the Factory kit's parts and lamp rules: the slot saw, the press ram, the vent's lamps
  and steam, the grid's lamps, bars and arcs, and a monorail hoist under the runway block with its load and zone lamps.
  It also draws the strafing target's carriage.
- **Hooks:** `TrainingScenery(arena)` in `SourceWorld` and `TrainingPrepare(arena)` in `PrewarmStage` (one line each);
  everything else is Runtime/NativeTrainingPresentation.cs, a component that draws only on the training map.
- **Checks:** `dotnet run --project Tests/CoreTests.csproj -c Release -- --training-look` (also in the full suite):
  placement from the pads, nothing in play space (floor paint at most 3 cm, wall and block-face paint at most 3 cm proud,
  fixtures from 5.5 m, under the roof, clear of blocks and out of reach), determinism, readout formatting and host/guest
  parity, bay capacities, no per-frame allocation, the look, and text scans of the runtime wiring. MapStyleChecks counts the
  training look among the styled looks; TransitionLoadingChecks requires its prewarm.
- **Unverified:** the Unity Editor, a Windows build and the shader compile (only Runtime and Editor C# were compiled
  against the Unity assemblies); the look under Unity's real NativeBlockLit and toon outline (the renders are an offline
  WebGL approximation); readability on screen; guest play. The host's `TrainingReadouts()` allocates its rows when polled
  (10 Hz); a non-allocating accessor would be Astra's change.
- **For Astra (found, not changed):** `TrainingStep` pins each target's body at its pad's `pos`, whose y is 1.1 (the spawn
  convention, floor + 1.1); with the capsule's 0.85 m half height the drawn feet (body − 0.865) stand about 0.24 m above
  the floor and the pad's puck. Pinning at floor + 0.865 (or keeping the settled y) would ground them; it is a sim change.

## Stage looks (presentation)

Claude, 2026-11-01; branch `claude/new-stages-look` from shared `3323310` (shared `0c80fa9` merged in). Step 2 of
"Ownership and order" for the five PvP stages. Presentation only: no collision, navigation, spawn, hazard timing, damage,
network, save or selection change. Every other map draws exactly as before (the code is keyed on the five map ids).

**Where it hooks.**
- Data: `MapLooks.json` gains one entry per stage (the look id each map already names: lights, the anime sky and style,
  fog, practical lights, block and kit looks, `blocks.dressed`, the advanced lighting and weather). `PackDressing.json`
  gains four kits and an entry per stage (rules for solid blocks; yards where the ground is at y 0).
- Core (engine-free; `Tests/CoreTests --new-stage-looks`):
  - `NativeNewStageLooks`: the five ids and looks, the `stage-kit-*` kinds (resolved per look from `kitLooks`, with
    defaults) and the places the scenery, rigs and effects share (sea levels, lighthouse, flare, dome).
  - `NativeNewStageScenery`: the box-built backdrops, baked into three SourceLit sectors per stage. Its builder throws on
    any box inside the bounds grown by 1 m above the floor's base (water, the pier's body, the tower's facade and the jacket
    lie under the floor).
  - `NativeNewStageKit`: detail on the blocks (every part inside its block grown by 0.25 m, the stage rule; nearly all
    within 2 cm of the face) and the overhead structure no block stands for (each at least 2 m over every standable top
    under it, like the Factory's crane bridge).
  - `NativeNewStageRig`: the hazard rigs and two ambient rigs, posed each frame from `NativeFactoryHazards.State` (cranes,
    vents) and `NativeWardZeroHazards.At` (burners) at the round index, phase, FightAt and field clock: the replicated
    state, so host, guests and the killcam agree. Allocation free.
  - `NativeNewStageFx`: the effects quads and, above the low tier, the volumetric beam list. Telegraphs are never reduced.
- Runtime: `NativeNewStagePresentation.cs` (`NativeNewStageDraw`; NativeBlockLit instancing as the Factory's kit,
  SourceLit sectors on the backdrop layer without shadows, one NativeFxDraw batch). One line in `SourceWorld`
  (`NewStageScenery(arena)`) and one in `PrewarmStage` (`PrewarmNewStage`). The kit drops the parts of blocks the bank
  dresses (`NativePackStage.Strip`). No new shader or material asset.

**Per stage.**
- **Breakwater (`breakwater-blue-hour`):** the sun 3° under the western horizon (orange halo, sunset-lit anime clouds,
  first stars), a cool 0.3 key from the west, warm lamps. Granite setts, rendered booths with lit windows, liveried
  containers with corner posts and door ends, the parapet's coping and scuppers, barrier reflectors. Backdrop: a faceted
  blue-hour sea, the pier's stone body and tetrapod armour, the causeway west with its lamps, lamp posts outside the
  parapet, a moored tug, channel buoys, the lighthouse on its plinth past the east parapet (16-sided banded tower, gallery,
  lantern, copper dome), the harbour town with lit windows and its cranes. Effects: the lantern and two sweeping beams,
  spray over the north and west parapets, foam, glints toward the afterglow and the lantern, halos, buoy blinks. Weather:
  a light drizzle (wet stone). Rig: the lighthouse optic turning.
- **Pier 9 (`harbor-night`):** moonlight key 0.32, sodium rim and practical floods, light drizzle with wet asphalt and
  puddles. Containers per id in liveries with tier seams, doors and corner posts; reefer units; the shed's pallet racks
  (uprights, beams, goods); gatehouse and office windows; the quay kerb in hazard stripes; yard lines and the five drop
  slots hatched under the gantry. Overhead: the ship-to-shore crane's portal, A-frame, boom over the basin, machinery
  house and cab over its in-map legs (from 15 m, 6 m over the 9 m deck). Rig: the rail-mounted gantry (bridge between the
  two runways at the hook's x, end trucks, cab, trolley and beacon, ropes, headblock, spreader 1.1 m under it, the 20 ft
  container, the slots' corner lamps). Backdrop: the basin, the quay face and fenders, the freighter alongside past the
  bounds (hull, deck lights, 4 to 7 tiers of containers, lit house, funnel), the terminal beyond the fences (stacks, masts,
  two more ship-to-shore cranes), sheds, the city and a lit bridge. Effects: flood halos and cones, aviation lights, the
  gantry's red cone over the slot (strobing through the warning, solid as it falls), landing dust, reflections, mist.
- **Skyframe (`skyframe-high-noon`):** a hard key at 74°, deep blue zenith, white anime clouds, haze below (height fog
  from the street 112 m down). Red-primer columns, blockwork, yellow edge protection, cabins, bundles with straps, pallets of
  blocks, the drop zones' paint on the slab and decks; the mast painted as a boxed lattice (it is solid: it stops shots).
  Overhead: the tower crane's head over the mast top. Rig: the slewing tower crane (slewing unit, cab, laced jib, counter-jib
  and weights, pendants, trolley, hook, slings, the beam bundle, the zones' lamps on their own floors). The simulation moves
  the hook straight between zones (through the mast for opposite zones); while it travels, high and harmless, the rig
  slews and trollies between the same two points instead, and from the warning to the next travel the drawn hook is the
  simulation's. Backdrop: the open frame of floors 30 and 29 with netting, the glazed tower to the street, the hoists' masts
  and parked cars, the neighbours (a glass tower north, a stone block south), the city on its grid with two more cranes.
  Effects: haze layers, netting snapping, dust motes, jib glints, the crane's telegraph and landing dust.
- **Deepwell (`rig-storm`):** a cold 0.2 key, the flare's warm rim from the north and a flickering practical light at the
  flare, rain with lightning (every 11 s on average), wet steel. White accommodation with lit windows, orange derrick and
  cranes, KO drums with saddles and beacons, wellhead and BOP, pipe racks with pipes, walkway edges, burner lanes hatched
  red, vent grates ringed, muster points. Overhead: the derrick lattice from 3.2 m over the drill floor, the pedestal
  cranes' booms. Rig: the burner grates and nozzle rows (embers, an amber flicker through the 1.5 s warning, white-hot
  burning, cooling red) and each lane's drum beacon; the vents' grates and corner lamps. Backdrop: the storm sea 5 m under
  the cellar deck (a pilot through a landing meets it just before killY), the jacket clear of every landing, lifeboats in
  davits, the flare boom, a standby vessel, two distant platforms. Effects: the flare, burner fire columns and smoke, vent
  hiss, steam column and drift, spray up the deck edges, whitecaps, deck halos, distant flares.
- **Polaris (`polar-aurora`):** a green 0.32 aurora key from the north, a cold blue rim, blue shade, snow. Station modules
  (containers on skids) with lit windows, doors, porch lamps, snow caps and frost; tanks, huts, garages with roof lights,
  snowcats; the drill masts painted as lattice. Overhead: the radome's geodesic dome over the radome block's unstandable
  roof. Rig: a radar turning on the met tower. Backdrop: the ice shelf with sastrugi and drift banks, flag lines, skiway
  lights, a drum line, outbuildings, an antenna field, a wind turbine, nunataks and an ice cliff. Effects: the aurora's
  rayed curtains (green below, violet above, pulses along them), ground drift, halos, aviation lights. Weather: snow.

**The kit bank (Toon Harbor), on the owner's PC only.** Rules (solid props; the plan keeps a box it cannot fill):
Breakwater crates, drums and containers (56 blocks); Pier 9 one-high rows, the quay box, crates and cages (35); Skyframe
cabins, deck containers and pallets of blocks (34); Deepwell crates, drums and cellar tanks (8); Polaris modules, tank
pairs, crates and drums (24; the two-high crate columns and the shallow tanks keep their boxes). Yards: Breakwater's
container quay north of the town (cranes, stacks, a freighter, tugs) and tetrapods along its faces; Pier 9's terminal beyond
the fences (its box-built cranes and stacks stand down while these draw); Polaris's fuel depot, storage row, vehicle line,
hangar, wind fences and skiway lamps. Skyframe and Deepwell have no yards (a yard stands at y 0, which is floor 31's level
and the deck's). Without the bank (CI, cloud builds) every stage draws its own block looks, kit, backdrop, rigs and
effects.

**Checks.** `CoreTests --new-stage-looks` (`Tests/NewStageLookChecks.cs`, also in the full run): every look resolves,
styled and dressed with the advanced lighting; block looks decide real blocks; kit looks are kit kinds; attached kit parts
inside their blocks and overhead parts clear of standable tops; the pack rules dress real blocks inside their colliders
against a synthetic bank of the props' real bounds; the yards and backdrops stay out of the play space; the rigs follow the
replicated state (the same at any FightAt, the hooks and loads the simulation's from the warning to the hoist, parked before
the arm delay, the tower crane never through the mast); telegraphs are never reduced; determinism; no allocation per
frame; the runtime's wiring and the new files' metas; every other map untouched. `StageKitChecks`, `MapStyleChecks`,
`AdvancedLightingChecks` and `TransitionLoadingChecks` list the five new looks.

**Renders.** Offline WebGL renders (the stage-kits harness: NativeBlockLit's SHARED code and the anime sky as GLSL, one
shadow map, its own bloom) from the worktree's Core and the map JSON, without the bank, before (the moonlit fallback and
flat-coloured cubes, as the shared branch draws them) and after, per stage: an overview, a player-height lane and the
hazard rig (Breakwater: the lighthouse).

**Not verified / needs the PC.**
- Not seen in the Unity Editor or a Windows build. The harness has no volumetric beams (their billboard fallback is
  drawn), no light volume, no wet surfaces or puddles and no lightning.
- The kit bank's props and yards are unseen here: a PC pass should look at the liveries the rules pick, the tank and
  brick piles' fit, the yards' placement against the box backdrops (Breakwater's town, Polaris's outbuildings) and whether
  Deepwell wants deck props (beacons, sirens, pipe clusters) through a new yard kind at deck height.
- Practical lights are the look's eight static slots: the burners' fire and the crane telegraphs light nothing but
  themselves (no dynamic light slots are claimed; the hazard kit owns `NativeLighting.KitLights`).
- Not drawn: breath in the cold on Polaris (needs pilot heads), splashes when a pilot falls into the water (Astra's
  event, if wanted), the lighthouse beam's sweep on the volumetric path (its billboards were tuned here).
- Audio ambience (the maps' `ambience` keys) is unchanged.

## M1 drafts (2026-10-31): what is built and checked

Run any draft with `python3 docs/arenas/scripts/<id>_m1.py [--out DIR]`. It prints the plan and checks, exits 1 if a check
fails, and with `--out` writes the map JSON and a top-down plan PNG.

| Stage | Draft | Blocks (+ floor) | Breakable | Hazards | Pilot blocks | Spawn checks |
|---|---|---|---|---|---|---|
| Breakwater | `breakwater_m1.py` | 76 | 50 crates/drums | none | 34 | FFA 6 pairs and A–B 16 pairs blocked; no sight lane over 31.9 m |
| Pier 9 | `harbor_m1.py` | 311 | 28 | gantry drop (`crane`, 5 zones) | 50 | 28 and 16 blocked; no team spawn seen from any raised spot |
| Skyframe | `skyframe_m1.py` | 344 | 12 | beam drop (`crane`, 4 zones: 2 on the slab, 2 on the decks) | 124 | 28 and 16 blocked in 3D (spawns on all three levels) |
| Deepwell | `rig_m1.py` | 323 | 6 | 2 flare burners, 2 blowdown vents | 85 | 28 and 16 blocked in 3D (per-deck eyes) |
| Polaris | `polar_m1.py` | 288 | 12 | none | 61 | 28 and 16 blocked; no team spawn seen from any raised spot |
| Calibration Bay | `training_m1.py` | 151 | 3 samples | 5 samples (saw, vent, press, grid, crane) | 17 | n/a (sandbox); every pad, step, platform and tower level checked |

Every draft also checks:
- spawn spacing and hazard clearance (3 m or more);
- spawns inside the bounds;
- every spawn and every walkable level reachable on foot (a 0.25 m 3D raster on the layered maps);
- no tops in the 1.3 to 2.2 m band (Calibration Bay's 1.5 and 2.0 m step pads are deliberate);
- point symmetry (Pier 9 mirrors across x = 0).

A review pass confirmed that no pilot column cuts a walkable deck above it; pilot columns block from their bottom upward
without limit.

**Notes from the drafts (for Astra and the owner):**

- **Breakwater:**
  - FFA lists the 4 duel and booth points first; entries 5 to 8 are team points, so a duel room uses the first 4.
  - Everything taller than a barrier carries a pilot column, so the map stays flat even with an air jump.
- **Pier 9:**
  - The quay is at z +13.6..+30, the gantry yard at z −5.6..+13.6, a road at z −10..−5.6 and the shed at z −30..−10.
  - The crane portal deck is at 9 m, reached by one 45-riser flight a side. Container terraces are at 2.6 m and tops at
    5.2 m, with bridges to the centre stacks.
  - The gantry `dwell` is 6.5 s: with the sim's fixed 1.5 s regrip, the landed container is cover for exactly 8 s, and the
    motion fills the 18 s cycle.
  - Runway girders on columns at x ±15.4 carry the gantry; the bridge, trolley and load stay non-colliders, as in the
    Factory.
  - The bounds run to z +34 past the quay edge, so the fall exists. `killVolumes` (`harbor-water`) is a proposed key.
  - Open asks: handrails on the crane flights and 5.2 m bridges? FFA spawns f and F on the quay can be seen from their own
    side's yard tops (reported, not enforced).
- **Skyframe:**
  - The middle of the map is the crane well, open to the sky (x ±7.8). It holds the mast, two drop zones and a stair per
    team up to its deck.
  - Under each deck is a 3.4 m lobby split by blockwork into rooms and a colonnade along the well.
  - The decks see each other only across the bridges and the well's middle. The 7.2 m route is each side's perch, a beam
    walk and the collar ring.
  - The bounds are x ±32 and the slab ends at ±28, so the hoist landings (3 m openings in the edge rail, two a side, about
    8 m from the spawns) are real falls.
  - The deck zones' centre y is 3.6. The Factory crane lands at y 0, so the fall, landed collider and lethal band must be
    measured from the zone's y. The hook may move straight between zones; the presentation slews the jib.
  - Long lanes: the longest runs 54.7 m north to south through the well (130 lanes of 40 m or more on the slab, 151 on
    the decks). That is well inside the shipped Factory's numbers on the same 0.6 m lane measure (3,637 lanes of 40 m or
    more, the longest 75.5 m).
  - Open asks:
    - Are 3.6 m drops (deck holes, the unrailed stairwell bay) free of fall damage?
    - Are the FFA spawns on the 7.2 m perches acceptable?
    - The landed bundle is 0.9 m, a little under the half-cover band.
- **Deepwell:**
  - Pilots are clamped inside the bounds, so the boat landings and lifeboat stations are 3 × 2 m notches cut into the
    cellar deck's edge. Each has a beacon post, and every spawn is 10.6 m or more from one.
  - The main deck is x ±23.5, z ±17.5, which leaves a 4.5 m outer ring on the cellar deck. Its straight edge lines run up
    to 54 m; that is the ring lane's character. The longest diagonal line is 35.8 m.
  - Flare burners deal 8 per 0.25 s tick (Ward Zero's are 10); Astra sets the real number against pilot HP.
  - The drill floor has no FFA spawn: it is the exposed power position.
- **Polaris:**
  - The hall walls are 5.6 m, not 4.5 m, so the 3.2 m mezzanine has headroom; the radome box rises above to 11 m.
  - The hall is two ground rooms joined by the mezzanine bridge, so no point inside sees more than 2 doors.
  - Point symmetry pairs everything: two drill towers, four tank pairs, two snowcats.
- **Calibration Bay:**
  - Only the 0.2 m step pad is walk-reachable; the others, and gap platforms 2 to 6, are jump tests.
  - Lane 2's 20 m pad sits 0.45 m north, clear of the strafing rail.
  - The hazard samples copy today's timings, including the Factory's 0.8 s vent and press telegraphs.
  - Five kinds are sampled; saw-rail and burner could be added.
  - The distance boards, firing line, sprint marks and readout board are look only; a `trainingMarks` field could carry
    them.

## Ownership and order

**Claude (presentation)**, in this order, each step a branch merged into the shared branch:

1. M1 blockout drafts with checks and plan images: all six are done (`claude/new-stages`, see "M1 drafts" above).
2. Per stage, after Astra integrates it (built on `claude/new-stages-look`; see "Stage looks (presentation)"):
   - the MapLooks look;
   - the PackDressing rules for its ids and its yards (scenery outside the bounds);
   - the hazard rigs' presentation (gantry, tower crane, flare);
   - water, sea and snow surfaces; atmosphere and ambience;
   - before and after renders.
3. Training Room presentation: the grid, lane markers, distance boards, the readout board, dummy pad visuals.

**Astra (logic):**

- Integration: generator, nav, music and the asset lock.
- Hazard simulation on the existing kinds, and the kill volumes (water, sea, edges).
- Bots on edges and decks.
- Selection: maps stay `rotation: false` until playtested; Breakwater is offered for 1v1 and 2v2 rooms.
- The Training Room mode: sandbox entry, dummy pads, strafing rail, hazard bay triggers, damage readout events.

## Astra handoff (ready to paste)

> New stages M1 (Claude, `docs/arenas/NEW-STAGES.md`). Draft generators:
> `docs/arenas/scripts/{breakwater,training,harbor,skyframe,rig,polar}_m1.py`. Each `unity_json()` returns the map in the
> `UnityMaps.json` shape, and running one prints its checks: spawn sightlines, spacing, hazard clearance, bounds,
> reachability.
>
> 1. Integrate each through `tools/generate_stage_hazards.py`: nav, music, asset lock.
>    - Keep the geometry, ids and hazard timing unless the sim needs a change; note any change in the stage's section.
>    - Set `rotation: false` until playtested.
> 2. Falls: the bounds deliberately run past Pier 9's quay edge and Skyframe's open sides, because `NativeUnityMaps.Clamp`
>    would otherwise hold pilots on the slab. Pier 9's draft proposes a `killVolumes` entry (`harbor-water`); it is a new
>    key, so take it or rely on killY.
> 3. Hazards on existing kinds:
>    - Pier 9's container drop and Skyframe's beam drop are `crane` with zones. Skyframe has zones on two levels, so
>      check the zone's y in `NativeFactoryHazards`.
>    - Deepwell's flares are `burner`.
>    - Add kill volumes for Pier 9's water (z > 30.2, y < −1), and treat Skyframe's open edges and Deepwell's open landings
>      as falls to killY.
>    - Bots treat open edges as cliffs, and knockback can push a pilot off.
> 4. Selection:
>    - Breakwater is a duel map: offer it for 1v1 and 2v2. It has 4 FFA points, so cap FFA at 4 there.
>    - The others take full rooms.
> 5. The Training Room (`training`), sandbox only:
>    - A menu entry, and spawn on the pad.
>    - Dummies on the draft's `trainingPads`, with respawn as the Dev Sandbox does; the strafing pad moves on its rail.
>    - The hazard bay's samples run on the sandbox clock, and the DEV preview triggers them.
>    - Emit damage and hit events for the readout board. Never in PvP lists.
> 6. Tests: each map passes the map and hazard validators, and the nav check covers every spawn.
>
> Claude follows with the looks, dressing, hazard rigs and the Training Room presentation.

## Owner asks

- **Confirm the lineup:** Breakwater (the 1v1), Pier 9, Skyframe, Deepwell, Polaris, and the Training Room as a separate
  sandbox level. Any theme can be swapped before its blockout is integrated.
- **Other packs:** banking a Synty or Polyart environment set on the PC would give Skyframe and Polaris richer kits; until
  then they use Toon Harbor's industrial pieces.
