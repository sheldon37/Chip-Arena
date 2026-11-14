# Chip Arena: new stage proposal (Harbor: Pier 9) and the Expedition docks zone

2026-10-28 · Claude (presentation lane) · for the owner (decides) and Astra (collision, hazards, spawns, bots, network)

Base: `claude/stage-kits` (the kit bank's stage props and harbour yards, docs/arenas/STAGE-KITS.md).

Status: **proposal only. No map, collision or hazard exists; nothing is selectable.** The presentation pieces it needs
already exist on the branch: the kit bank, the block-prop rules, the yards, and the textured block variant.

Owner direction (2026-10-28): "much better environment + stage development"; Toon Harbor's crates and containers; the
Expedition maps, the Factory and Cyber City are the direction, Brickfall the baseline.

## At a glance

- **Pier 9 (map id `harbor`, proposed):** a container terminal's last berth on the night shift.
  - Under sodium floodlights, with a loaded freighter moored along the west quay.
  - Three ship-to-shore cranes stand over the freighter; a rail-mounted gantry crane works the stacks.
- **Size:** an 84×64 m slab, 80×60 m of play (4,800 m² of floor). That is a little larger than the Factory (4,560 m²).
  - Container-top walkways at 2.6 m and 5.2 m.
  - The crane portal's deck at 9 m (reachable by one stair, the high ground).
- **Three lanes, one character each:**
  - **North: the Stacks.** A maze of container rows, two and three high, with alleys 3.2 m wide. There are ladders onto
    the tops: mid range, flanks, verticality.
  - **Middle: the Gantry Lane.** An open apron under the rail-mounted gantry crane (RMG). This is the long sightline
    (about 60 m), broken by the containers the crane drops.
  - **South: the Transit Shed.** An open-sided shed of pallets, crates, cages and two parked forklifts: close quarters.
- **Hazards (pure functions of FightAt and the match time, idle for the fight's first 4 s, telegraphed like the Factory's):**
  - **Container drop.** The gantry travels to one of five painted slots on the lane every 18 s. Its spreader lowers, the slot's
    lamps strobe for 3 s, and it drops a 20 ft container. Under it is an instant kill; the landed container is cover for 8 s,
    then it is lifted away.
  - **The quay edge.** A fall into the basin is a kill (a splash, the water's kill volume below the quay).
  - Optional: a **reach stacker** crossing the shed every 25 s (a knockback, never a kill).
- **No new network data:** every client, the host and the killcam pose the gantry and its container from the replicated
  phase, FightAt, round and time, as the Factory's overhead crane already does.

## Layout sketch (top view, x east →, z north ↑; 1 char ≈ 2 m)

```
   z +32 ┌──────────────────────────────────────────────────────────────────────────────┐
         │≈≈≈│ ▓▓ ▓▓▓  ▓▓▓▓ ▓▓  ▓▓▓  │ THE STACKS (2-3 high, alleys 3.2 m)  ▓▓▓ ▓▓  ▓▓ ║ G │
         │≈≈≈│ ▓▓ ▓▓▓  ▓▓▓▓ ▓▓  ▓▓▓  │   ladders ┼  tops at 2.6 / 5.2 m     ▓▓▓ ▓▓  ▓▓ ║ A │
         │≈≈≈│ ▓▓      ▓▓▓▓      ▓▓▓  ┼                                      ▓▓▓     ▓▓ ║ T │
         │≈≈≈├─[STS portal deck 9 m]─┤ ========== RMG rail (north) =============== ║ E │
     B   │≈≈≈│ bollards  ▢  ▢  ▢     │  ① ② ③ ④ ⑤  drop slots (painted, lamps)          ║   │  A
  spawn  │≈≈≈│ quay apron            │  THE GANTRY LANE: open apron, 60 m sightline      ║ yard│ spawn
  (quay) │≈≈≈│  ▢     ▢   ▢          │                                                    ║   │ (gate)
         │≈≈≈├───────────────────────┤ ========== RMG rail (south) =============== ║   │
         │≈≈≈│ ▒ pallets ▒  ▒ crates  ┌──────── THE TRANSIT SHED (open sides, roof 7 m) ──┐ ║   │
         │≈≈≈│ ▒ forklift ▒  cages    │ racks ▒▒   ▒▒ forklift   ▒▒ crates   ▒▒ racks   │ ║   │
   z -32 └──────────────────────────────────────────────────────────────────────────────┘
        x -42 (quay wall, water ≈ to the west)                                    x +42 (gate, truck yard)
```

- Legend: ≈ water (the freighter's hull beyond), ▓ container stacks, ▢ single containers as cover, ▒ pallets and crates,
  ║ the gate fence, ① to ⑤ drop slots.
- **Spawns:**
  - Team A at the gate (east): truck yard, gatehouse, reefer stacks.
  - Team B on the quay (west): under the STS portal, bollards, the freighter's lit superstructure behind.
  - FFA: eight points spread over the three lanes and both ends.
- **Rotations:**
  - Each lane connects to its neighbours through gaps every 12-16 m: the stacks' cross alleys, the shed's open side bays.
  - The STS portal deck links the north and middle lanes at 9 m (one stair each side). It is the power position, exposed to
    the whole gantry lane.
- **Sightlines:**
  - Long: the gantry lane end to end (about 60 m); dropped containers cut it into thirds.
  - Mid: across the stacks' tops.
  - Short: inside the shed.
  - No sightline crosses the whole map diagonally: the stacks and the shed roof block it.

## Lighting mood (MapLooks `harbor-night`, proposed)

- Night, a low cold moon from the north-west, a key of about 0.35 on cool blue.
  - Sodium floodlights on four 25 m masts (warm, as the Factory's high-bays), with practical slots on the lane and the quay.
  - The freighter's windows and the cranes' aviation lights in the lights atlas at glow 1.2.
- Wet asphalt (the toon sheen up), a sea haze off the water (ground haze 0.3, a cool fog from 25 to 160 m).
- Dust is low, the anime style on, with the outline as on the other Unity-era maps.
- **The water edge:** a quay wall dropping 2.2 m to dark water, a yellow kerb, bollards and black fenders (the yards' quay).
  The basin carries the moored freighter, so the west horizon is the ship's hull and the cranes' booms, never an empty sea.

## Building it from the kit (presentation, Claude)

- **Collision blocks** take the table's rules by id:
  - `harbor-stack-*` draw as 20 ft or 40 ft containers;
  - `harbor-crate-*` as crates on pallets, `harbor-cage-*` as drum cages;
  - `harbor-shed-*` keeps its boxes under the shed kit (corrugated cladding, the roof trusses).
  - Every collider stays a box, so the rules' fit (nothing proud, at most 0.25 m short) holds by construction when the
    stacks are authored on the container grid (2.4 m wide, 2.6 m tall, 6.0 or 12.2 m long).
- **The yards** draw outside the slab:
  - the quay wall and the basin to the west;
  - the freighter, the three STS cranes and two tugs;
  - the terminal's further stacks to the north and east;
  - the gatehouse, trucks and lamps.
- **The gantry rig**: built from the pack's crane elements (gantry structure, trolley, cabin, motors; added to the
  selection when the stage is built), posed each frame from the hazard state (bridge travel, trolley, spreader height, the
  container it carries), with lamps on the slots, a strobe through the telegraph and a dust burst and decal on landing.
  Same pattern as the Factory crane rig (`NativeFactoryRig`). The three cranes over the freighter are the bank's
  ship-to-shore cranes, as in the yards.
- **Effects:** splashes at the quay (a fall), sea spray on the wall, gulls, a horn from the freighter on the round's
  start, rain optional.

## The Expedition docks zone (exp-docks, proposed)

The town's west edge already draws the docks (`PackDressing.json`, the town's yards): the quay, the basin, three STS cranes,
a freighter and the stacks, by the City Ironworks gate.

The zone opens them as a PvE area:

- **Approach:** the foundry road west from the town square, through the docks gate at x -126.
- **The Stacks maze:** container alleys where adds spawn from opened containers (a door-burst entry, the existing entry
  kinds).
- **The Gantry Yard:** the container drop as a zone hazard that also crushes adds (players can lure them under a slot).
- **The Freighter:**
  - A gangway onto the deck, with two holds below (stairs down: a dark interior lit by the lights atlas);
  - the zone's objective in the aft superstructure (the bridge), for example restarting the ship's generator under a horde;
  - the boss arena on the open deck between the container bays.
- **Exit:** back through the gate, or by the tug at the quay's end (a scripted transition).

## Astra handoff (collision, layout and simulation: Astra builds; Claude does not)

1. **`UnityMaps.json` map `harbor`:**
   - Bounds: x ±42, z ±32, ceiling 20, killY −6.
   - Blocks: ids as above, on the container grid; roles `static` for stacks and the shed, `cover` for crates and cages with
     hp.
   - Spawns: FFA 8, teams 4 + 4; `pilotBlocks`; nav and navHints for the stacks maze and the tops.
   - Stairs and ladders onto the stacks and the portal deck as collision steps (the Factory's stair flights pattern).
2. **The gantry hazard:** a new kind, or the Factory crane extended (`NativeFactoryHazards` `crane`), in `hazards[]`.
   - The rail axis and five drop slots; a travel, lower, telegraph and drop schedule from FightAt and time; idle for the
     fight's first 4 s.
   - The kill volume under the container while it falls.
   - The landed container as a temporary collider (as the Factory's landed bundle) for 8 s, then lifted away.
   - Presentation reads the hazard state through the same pure function.
3. **The water:** a kill volume over the basin below the quay edge (y below −1.0, x below −42) and the splash event for
   presentation. Bots treat the quay edge as a cliff.
4. **Optional reach stacker:** a moving knockback volume on a fixed path through the shed every 25 s (never a kill).
5. **Expedition `exp-docks`:**
   - an `ExpeditionLayout.json` area: entries, the gate, rooms (the stacks, the gantry yard, the freighter's deck and
     holds), add spawns from containers, the objective's state machine, the boss;
   - a `UnityMaps.json` map `expedition-docks` with its own blocks (the town's docks yards stay presentation and move out
     of the town's backdrop when the zone exists);
   - doors and the transition back to the town.
6. **Bots:** cover selection on container rows, the portal deck as a contested high point, avoiding drop slots under
   telegraph and the quay edge.

Claude then adds:
- the MapLooks `harbor-night` look;
- the PackDressing rules for the new ids and the map's yards;
- the gantry rig, telegraphs and splash presentation;
- the shed kit and the Expedition zone's dressing;
- before/after renders, with the same checks group (`--stage-kits`) extended to the new map.
