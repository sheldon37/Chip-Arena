# Ward Zero revamp simulation contract (draft for Astra)

2026-10-13 · Claude (presentation lane) · for Astra (simulation, collision, spawning, hazards, nav, networking, tests) and the owner.
Design: `docs/arenas/WARDZERO_PROPOSAL.md`. Draft data: `docs/arenas/scripts/wardzero_m1.py` (`--json` writes the map in the
UnityMaps.json shape plus a top-level `pve` block; frozen as `unity/ChipArenaConversion/Tests/wardzero-draft.json`).
Branch `claude/wardzero-rework` (base `258e348`). Nothing here is wired into the game: today's Ward Zero keeps working
unchanged until the import, and the presentation on the branch stays dormant until then. All numbers are proposals; record
agreements and changes at the top of this file (as BRICKFALL- and CYBERCITY-SIMULATION-CONTRACT.md do).

## Simulation agreement — 2026-10-13, Codex (implementation on task branch)

- Import through UnityMaps.json: replace only the existing `siren` map, preserve
  music, rotation, public identity and every authored geometry/entry ID. Keep old hall assets.
- Generic `ramps` / `navHints` drive wedge collision and navigation; corpse treads remain.
- Breach collision returns at `restoreAt + 2.6`, not at restoreAt. Overlapping
  bodies postpone restore in 0.5-second increments; the updated clock is replicated.
- Shutters push only on authority simulation ticks. Their collision interlock stays
  open if a checked push cannot clear a capsule, including a locked pilot; it
  retries while the clock is shut. The visual clock is unchanged, so an obstructed
  shutter may look shut while collision waits. This deliberate no-crush exception
  needs native playtest coverage.
- Navigation uses a supported 1 m graph, wedge links, twelve directed drops and
  hazard-tagged links. Authored safe pockets are marked for evacuation.
- Hulk content adopts the existing VARIANTS row and capsule (.954 radius / 3.06
  height), capped at two and no more than half the normal alive cap. It enters only
  via breaches or the flagged Cradle hatch. Ordinary entries remain data driven.
- Song sides come from `song`, including the draft's last pair (1, 0); index parity
  is no longer assumed. No entry IDs or geometry were changed.
- No presentation geometry, animation, lighting, kit or effect redesign is authorized.
  The only presentation edits will connect the replicated/data-owned simulation fields.
- Actual executed gates and remaining native validation are recorded in DEV_LOG.


## 1. Owners and files
| owner | files |
|---|---|
| Astra | the import (map content, PveContent), `ContentMap` / `PveEntry` / `PvePoint` fields, the wedge collision and the traversal gate, hazard damage and blocking, entry timelines, nav on two floors, bots, Siren rules, PvP, Sandbox, events, replication, tests; the hazard timing once merged (`Core/NativeWardZeroHazards.cs`, change it if needed: the presentation follows) |
| Claude | `MapLooks.json` `siren-quarantine`, the shader's ward bank, `Core/NativeWardZero{Looks,Mood,Kit,Lights,Fx}.cs`, `Runtime/NativeWardZeroPresentation.cs`, `Tests/WardZeroChecks.cs`, the draft script |
| Zombie lane | the Hulk, its landing effect, the breach burst and holographic restore (`docs/SIREN-ZOMBIES.md` sections 7-8, `Core/NativeBreach.cs`, `Runtime/NativeHordeBreachPresentation.cs`) |

## 2. Import: replace Ward Zero in place
- **Keep the id `siren` and the name "Ward Zero"** (PvE, the boss, the music, saves and menus key on it). Draft fields: tag "Meridian Biocontainment, Ward Zero. Where she got out.", theme `siren`, **look `quarantine`** (the presentation's switch: `NativeWardZeroLooks.Active` = id `siren` and look `quarantine`), ambience `wardzero`, sky/floor/accent as the draft; keep today's `music` and `rotation`.
- **Where:** today's map comes from GameContent.json (the Electron export, no look). Proposed: generate the revamp into UnityMaps.json (`unity: true`, validated like the other Unity maps) and let `NativeUnityMaps.Merge` replace a GameContent map of the same id (today it would reject the duplicate), or edit GameContent.json; pick one and record it. The content revision changes as with any map edit.
- **Draft numbers:** 616 blocks (604 static, 12 cover with hp 60, no structural members, no segments), initial prop frame 12; bounds x +-38, z +-30, ceiling 13, killY -6; no pilot blocks needed (the vault is solid over the whole slab, the shell rises to it). Spawns: FFA 8, teams 2 x 4 exactly as the draft (y = floor + 1.1, yaw toward the origin; two FFA points and no team point on the upper floor).
- **Id families** (all `siren-`; the presentation keys on them, so keep them or tell Claude): `slab`, `vault`, `shell-{n,s,e,w}`, `atrium-wall-{n,s,e,w}-*`, `atrium-upper-{e,w}-*`, `atrium-cart-*`, `atrium-screen-*`, `gallery-{n,s,e,w}`, `gallery-*-rail-*` (`-kick`), `bridge-{e,w}-{deck,rail-*,specimen,link}`, `stair-{atrium-e,atrium-w,nw,ne,se,sw}-NN` (treads) with `-landing`, `-rail*`, `ramp-{e,w}-{deck,skirt-*,platform}`, `deck-{n,s}-*`, `hall-{n,s}-{wall-*,gurney-*,cart-*}`, `concourse-{n,s}-{wall-*,bench-*,console}`, `wing-{wall,upper,tall}-{ne,nw,se,sw}-*`, `cells-{n,s}-{row,station,gurney-*}`, `obs-{n,s}-{row,desk}`, `breach-*`, `cradle-shard-{n,s}-*`, `morgue-{nw,se}-*`, `intake-{ne,sw}-*`, `decon-{nw,se}-*`, `control-{ne,sw}-*`, `lab-{e,w}-{tank,bench}-*`, `lane-{e,w}-{wall-*,furnace}`, `gantry-{e,w}-{deck,rail-*}`. The draft's ids avoid today's Ward Zero ids.
- **Today's hall assets** (`map-siren` source model, `WardMotion.json`, `NativeWardPresentation`) stay in the repo; the presentation stops drawing them when the map carries the look (already on the branch). Removing them is the owner's call.
- `MapLooks.json` already holds `siren-quarantine` (its `id` differs from the map id; `NativeLighting.Look` picks it by the map's `look`). If a check counts MapLooks entries or requires one entry per map id, update it.

## 3. Floors and collision
- **Routes between the floors (`ramps[]`, 8):** six stairs (`kind: stair`, 26.6 degrees, 0.25 m risers, 0.5 m treads) and two gurney ramps (`kind: ramp`, 24 degrees). Each entry: `id`, `low` and `high` (the top face's ends, y 0 and 6), `width`, `bottom` (0) and the visual `blocks`. Proposed `ContentMap.ramps` (`MapRamp {id, kind, low, high, width, bottom, blocks[]}`), read like `NativeCyberCityStairs.Flights` but from data instead of ids.
- **Collision (Astra's Cut precedent):** for each route disable the listed blocks' colliders (the treads, the pitched ramp deck; `navHints.collisionOff` also lists the ramp skirts) and add one closed invisible wedge mesh collider from `low` to `high`, `width` wide, down to `bottom`, with no renderer, rebuilt on round reset on host and replica alike; the corpse world keeps the authored treads. A 0.5 m tread is shorter than the 0.84 m capsule, so every flight needs its wedge.
- **Traversal gate on both floors** (as the Cut's 116-route gate, `NativeCyberCityExitValidation`): for every route, host and guest walk and sprint from the foot's floor, from the bottom tread and from mid-flight at centre, left and right (capsule edge 2 cm from the side), to 2 m beyond the top on the upper floor; and down again from the top; plus the bot path (navigator and host motor) from a ground point to an upper point through each route. 8 routes x up and down. Report failures with route, start, side, mode and position. Run it in the Editor menu and in the build's native checks.
- **Rails:** a top bar (0.95..1.1 m) and a kick plate (0..0.15 m) both collide; the 0.8 m gap between them stops a capsule but not shots or sight. Drop gaps in the rails are deliberate (section 8).
- **Breach panels:** solid blocks until a breach (section 7.6).
- **Today's stair rule unchanged elsewhere.** The static flood (0.5 m cells, the capsule's footprint, the wedges) reaches every walkable cell on both floors from the spawns; Unity's PhysX result is the gate's to show.

## 4. Hazard schema (`ContentMap.hazards`, `MapHazard`)
The existing class and fields (`MapHazard : FactoryHazard`: id, kind, center, size, axis, a, b, first, period, offset, warn, danger, speed, length, charge, live, tick, damage, radius, start, rise, travel, dwell, hoist, carry, push, lethal, alternate, zones[]). One existing kind (`grid`) and three new ones:

| id | kind | volume (center; size) | timing | effect |
|---|---|---|---|---|
| `siren-grid-e` / `-w` | `grid` (existing) | (+-6, 6.5, 0); (3, 1, 20) | first 8 / 16, period 16, offset 8 / -8, charge 2.5, live 2 | tick 0.25, damage 13 |
| `siren-burner-e-a` / `-e-b` / `-w-a` / `-w-b` | `burner` | the lane (+-34.3, 1.75, 0); (7.4, 3.5, 28); `zones`: two 2 m rows each | first 6 (a) / 11 (b), period 10, offset 5 / -5, warn 1.5, live 2.5, dwell 1 | tick 0.25, damage 10, only inside the zones |
| `siren-gas-nw` / `-se` | `gas` | (-+28, 9.5, +-22.2); (7.2, 7, 5.6) | first 15 / 27, period 24, offset 12 / -12, warn 2.5, live 4, dwell 3 | tick 0.5, damage 6 |
| `siren-shutter-n` / `-s` / `-e` / `-w` | `shutter` | portals (0, 2.25, +-14.2); (6, 4.5, 0.4) and (+-16.2, 2.25, 0); (0.4, 4.5, 6) | first 20 (n, s) / 38 (e, w), period 36, offset 18 / -18, warn 3, travel 1.2, dwell 9, rise 1.5 | push 6, blocks while shut, no damage |

**Loader validation (proposed):** unique id with the map prefix; kind in {grid, burner, gas, shutter} for this map (anything else rejects it); finite vectors, size > 0; `period > 0`, `first >= 0`, `first + offset >= 0`; burner and gas: `warn > 0`, `live > 0`, `warn + live + dwell <= period`, `tick > 0`, `damage > 0`, `lethal` false, burner zones inside the volume; shutter: `warn + travel + dwell + rise <= period`, `travel > 0`, `rise > 0`, size thin on one horizontal axis; grid as Cyber City's.

## 5. Timing (pure; nothing new replicated)
Reference functions in `Core/NativeWardZeroHazards.cs` (struct results, allocation free, pinned by `Tests/WardZeroChecks.cs`):
```csharp
NativeHazardState NativeWardZeroHazards.Evaluate(MapHazard h, int round, double fightAt, double time); // grid -> NativeCyberCityHazards.Grid; unknown/null -> Idle
NativeHazardState NativeWardZeroHazards.At(MapHazard h, NativePhase phase, int round, double fightAt, double time); // Idle outside Fight/Result
NativeHazardState NativeWardZeroHazards.Burst(...);         // burner and gas: Warn warn, Active live, Clear dwell, Idle
NativeHazardState NativeWardZeroHazards.ShutterState(...);  // Warn warn, Danger closing (travel), Active shut (dwell), Clear opening (rise), Idle
float NativeWardZeroHazards.Closure(in NativeHazardState s); // 0 open .. 1 shut (rises through Danger, 1 in Active, falls through Clear)
int   NativeWardZeroHazards.Tick(MapHazard h, in NativeHazardState s); // tick index while Active, -1 otherwise
bool  NativeHazardState.Clear(double margin);               // Idle with at least `margin` s before the next Warn (nav gate)
```
- `round = NativeStructureFx.RoundIndex(match.Set, match.Round)`; cycle k starts at `fightAt + first + k * period`, plus `offset` on even rounds. Damage applies only in `NativePhase.Fight`; the presentation shows Fight and Result.
- **Round 1, fight at 10 s:** burner e-a warn 16, burn 17.5-20, clear to 21, then every 10 s; e-b 5 s later (warn 21). Gas nw warn 25, gas 27.5-31.5, clear to 34.5, next warn 49; se 12 s later. Grid e warn 28.5, live 31-33, clear to 34, next warn 44.5; w 8 s later. Shutters n and s warn 30, closing 33-34.2, shut to 43.2, opening to 44.7, next warn 66; e and w warn 48. Round 2: each clock moves by its offset (the burner pairs swap, the shutter axes swap, the bridges swap).
- Fairness (checked by the draft and the managed checks over two rounds): the two bridges are never live together, at most two of four portals are shut, the burner clocks never overlap.

## 6. Damage, kill and blocking rules (proposed)
- **Grid (`DamageKind "grid"`):** Cyber City's coupling path unchanged: while Active, at each tick of the hazard's own clock, every living pilot or add whose feet are on the deck volume takes `damage` through the normal mitigation path (`stage: true`), no stagger, one hit per tick per body keyed on the tick index.
- **Burner (`DamageKind "burner"`, Detail = hazard id):** the same tick rule, but only inside one of the hazard's `zones` (the 2 m rows), feet below the zone's top; no damage in Warn or Clear. 2.5 s in a row is 100 (kills a base pilot); a sprinted crossing 10-20. Add `burner` to the stagger `None` list. A burning status is optional (presentation shows fire on the body if the frame says so).
- **Gas (`DamageKind "gas"`):** the tick rule inside the lock's volume while Active (6 every 0.5 s: 48 over the window), mitigation applies, no stagger. Doors stay open; nothing traps a body.
- **Shutter:** a box collider the portal's size, enabled from the start of Danger to the end of Clear (pilot and add collision, sight and shots blocked while Active). Its leading edge descends with `Closure`; a body under the edge in Danger is pushed out along the portal's normal (toward the side its centre is on) at `push` m/s; nothing is crushed or killed; a shutter never closes on a body it cannot push clear (hold the closure while overlapped and replicate nothing new: the presentation reads the clock, so prefer pushing). No damage.
- **Song vents (indices 0..7):** today's rules (`StepVents`, `NativeVentRules.Falls`, the `vent-fall` / `vent-splat` death, the PvP dive schedule, `BossTrack.Vent` for the song) with the vents' positions read from the entries' `from` instead of `NativeArena.StageVents`.
- **Credit:** the existing 7 s last-attacker rule for every kind. `RecordDeath` keeps the kind; `NativeNetworkFrame`'s death validation accepts `grid`, `burner` and `gas` (and today's kinds).

## 7. PvE content (PveContent.json for the map)
Write the draft's `pve` block into the map's PvE content: `entries` (53), `machines` (8), `machineBlocks` (8), `pads` (8), `boss`, `hideSpots` (12). **No coordinates in code:** today's `NativeArena.StageVents` and the boss's place (`PlacePveBoss` / `FoundPveBoss`, (0, .9, 24.2)) become data.

### 7.1 Fields
- `PvePoint` gains `y` (the floor the point stands on; absent reads 0, so today's content still loads). Every entry `from` / `exit`, machine `front` / `dispense` / `chipDrop`, pad and hide spot carries it.
- `PveEntry` gains (absent fields read as defaults): `surface` (`floor`, `wall`, `ceiling`; vents only, absent = floor), `level` (0 ground, 1 upper), `song` (0/1 for the eight song vents, -1 otherwise), `pit` (the vent opens into a shaft a pilot falls into), `height` (a drop's or ceiling vent's fall), `clear` (a drop's clear landing radius), `hulk` (a drop the Hulk may use), and for a breach `block` (the panel block's id), `center`, `size` (the panel's box) and `normal` (outward, into the arena). The presentation reads exactly these names (`Core/NativeWardZeroKit.cs` `NativeWardEntry`).
- **Indices:** `e` is the entry's position; 0..7 are the song vents in today's sides (`song`), so `BossTrack.Vent(t, e % 2)` style lookups keep working if they key on `song` (prefer `song`).
- `machines[]` keep today's ids (`chip-0..3`, `chip-4/5` recruits, `med-0/1`) and gain `y`; the machine blocks stand on their floor (`pos.y = y + 1.1`).
- `boss`: `spawn` (0, 0, 0), `yaw` 180, `rescue` (0, 0, 0).

### 7.2 Entry timelines (proposed; the add animations are today's per kind)
| kind / surface | from -> exit | timeline |
|---|---|---|
| `vent` / `floor` | the grate -> 1.2 m out | today's vent climb (`NativeEntryRules.Climb`) |
| `vent` / `wall` | the grille (0.5 m up the wall) -> 1.4 m in front | a crawl out: the vent timeline with the add starting prone at `from` |
| `vent` / `ceiling` | the grille -> the floor under it | a drop: `NativeHordeDrop` with `height` (5.6 or 7) |
| `cell` | inside the cell row -> 2.2 m into the aisle | today's cell walk |
| `door` | behind the door -> into the room | today's door walk |
| `drop` | the hatch -> the landing (same x, z) | `NativeHordeDrop` with the entry's `height` (13 over the Cradle, 7 over the observation aisles); spawn only if `clear` m round the landing hold no body (or push them as today's drop does) |
| `breach` | 1 m inside the wall face -> 2.4 m in front of the panel | `docs/SIREN-ZOMBIES.md` section 8 (`NativeBreach`: knock 2.1 s, through 1.3 s, restore, rebuild 2.6 s) |

Every entry's exit is on walkable floor at its own level (checked). Use both floors: in a wave, pick entries across both levels and near the living pilots' floors, never one whose exit is inside an Active hazard volume.

### 7.3 Opening amounts
Keep today's `NativeEntryView` (`entry`, `kind`, `mode`, `openAt`, `closeAt`) for every kind; the presentation opens lids, grilles, gates, doors and hatches with `NativeVentRules.Open` on it (as today's hall does) and treats `drop` and `breach` the same way. For `vent` keep `kind == "vent"` for all three surfaces (the surface is the entry's field).

### 7.4 Drops and the Hulk
- `drop-cradle` (e 36): 13 m, clear 4.5 m, `hulk: true`; the Hulk's only drop. `drop-obs-n/s`: 7 m, clear 2.8 m, brutes.
- The Hulk (zombie lane's prompt in `docs/SIREN-ZOMBIES.md`): at most half the alive cap ("1/2 max spawned"), very slow, very high hp, huge knockback; it enters by `drop-cradle` or a breach entry.

### 7.5 Machines, pads, hide spots
Machines on both floors (chips on the specimen hall walls and the concourses, recruits in the halls, med on the galleries); pads four per floor; hide spots six per floor. All on reached floor (checked).

### 7.6 Breach collision
- The panel is the block `entry.block` (`siren-breach-*`, 3.2 x 4.0 x 0.4 m, proud of a solid face, so the opening shows a recess nobody can enter). Name its prop GameObject `breach:<entry id>` (`NativeBreach.ObjectName`) so the breach presentation finds it.
- **Collision off at `breakAt`; back when the rebuild ends** (`solidAt = restoreAt + NativeBreach.Rebuild`, 2.6 s). The coordinator's note says "returns at restoreAt"; the zombie lane's contract defines `restoreAt` as the start of the holographic rebuild and returns collision at `solidAt`: follow docs/SIREN-ZOMBIES.md section 8 and record the choice. If a body overlaps the panel at `restoreAt`, push the restore back (re-check every 0.5 s) or push the body out along `normal` at `solidAt` (pick one, record it).
- Replicate `breaches: [{entry, breakAt, restoreAt}]` on the PvE frame (the zombie lane's proposed fields) so a late joiner and the killcam agree. A breach in progress is not reused by another add until `solidAt`.
- Nav: carve the panel while open (an add or bot may walk through only between `breakAt` and `solidAt`; the recess behind is not walkable).

## 8. Nav on two floors
- **Nodes** on both floors (`layer` 0 ground, 1 upper), stair and ramp links along the wedges (the draft's `ramps[]` give the ends), and **one-way drop links** from `navHints.drops` (12: gallery gaps, bridge gaps, specimen bridge gaps, gantry gaps; edge `a` on the upper floor, landing `b` below). No link off any other edge; the atrium void and the rail gaps are falls, not walkable.
- **Hazard gates** (`MapNavLink.hazards`): bridge links by their grid (`Clear(1.5)`), links through a burner row by that burner (`Clear(1.0)`), links through a decon lock by its gas (`Clear(2.5)`), a portal link by its shutter (usable only while Idle with `Clear(3)`; closed from Warn through Clear). Safe nodes from `navHints.safe` (the lane pockets between rows); a bot inside a hazard volume when it leaves Idle goes to the nearest safe node.
- **Floor choice:** bots path to targets on the other floor through the nearest route; the Siren and adds path on both floors. The managed nav checks should isolate each route and require both ends (as the Cut's per-flight check).

## 9. The Siren on this map
- Spawn and reveal at the Cradle (0, 0, 0), yaw 180 (data). Phases and possession as today; the possessed add drifts toward `hideSpots` on both floors (prefer the floor away from most pilots).
- Song vents on the boss song as today (indices 0..7). The presentation's mood reads the replicated PvE state (`Stage`, `Wave`, `Phase`, `Hidden`, `Holding`, `Cinematic`) and the boss song clock, which guests already receive; nothing new to replicate.

## 10. PvP and the Dev Sandbox
- PvP: the same map; hazards on their clocks in PvP too; the song vents dive on today's PvP schedule; spawns as section 2. Selectable as today.
- Sandbox: every entry kind available to the spawn tools (the presentation animates any entry the data names); hazard clocks as in PvP; a way to force a breach and the Cradle drop for testing.

## 11. Events, replication, protocol
- No new event kinds required (optional: `minion-breach` / `minion-drop` from the zombie lane's prompt). Damage kinds `grid` (exists), `burner`, `gas`.
- No new replicated hazard state: timing derives from the match frame (time, phase, fightAt, round, set). New PvE frame field: `breaches[]` (zombie lane).
- The content revision changes with the map; no protocol bump for the hazards themselves; `PveEntry`/`PvePoint` field additions are JSON-tolerant.

## 12. What the presentation already does (this branch)
- Switches on when the map is `siren` with look `quarantine` (`NativeWardZeroLooks.Active`): the look, the dressing kit, the practical light picker (through `NativeLighting.PickedLights`), the mood, the effects, the pit shafts; and stops drawing today's `map-siren` model and `WardPresentation` (also behind the menu).
- Reads `arena.Map.hazards` by kind (grids by the sign of `center.x`), the entries from `Resources/PveContent.json` (`entries`, the field names of section 7.1; the index is the entry's position), and `arena.Entry(e)`-equivalent open amounts (allocation free, same rules).
- Breach seam: `ArenaPresentation.SetWardBreach(string entryId, bool open)` (and by index) hides the panel's dressing and leaves its block out of the arena dressing while the breach presentation shows it broken (`NativeArenaDressing.Hidden`, cleared on a new live arena). The zombie lane's `NativeHordeBreachPresentation` calls it when it hides and restores the segment.
- If an id family, an entry field or a hazard kind changes, tell Claude: the MapLooks globs and the kit's stems follow.

## 13. Tests for Astra
1. Loader: the revamp loads under id `siren`, name "Ward Zero"; today's content (no look) still loads until replaced; each section 4 rule rejects a broken copy; `ramps[]` and `navHints` read.
2. Collision: every `ramps[]` flight has its wedge and its listed blocks' colliders off; corpse world keeps treads; the native traversal gate (section 3) passes all routes up and down.
3. Timing: the section 5 table; even-round offsets; `At` Idle outside Fight/Result; purity; host and replay agree.
4. Damage: grid, burner (only inside a zone row), gas (only inside the lock) tick counts per window; none in Warn or Clear; mitigation; no stagger; credit; network accepts the kinds.
5. Shutter: blocks while shut, pushes a body out in Danger, never traps or kills; corner doors always passable.
6. Song vents: positions from data; today's PvP schedule and boss song behaviour unchanged; falls at indices 0..7 only where `pit`.
7. Entries: every kind and surface spawns its add at `from` and releases it at `exit` on the right floor; drops respect `clear`; the Hulk uses only `hulk` drops and breaches; breach collision off at `breakAt`, back at `solidAt`, overlap handling; `breaches[]` replicate.
8. Nav: both floors connected through each route on its own; drop links one-way; hazard gates; safe nodes; bots reach targets on the other floor.
9. Spawns: every spawn at least 6 m from every hazard volume on its floor (the draft's minimum is 6.6 m); FFA and team sightlines (the draft's jitter test).
10. Keep `Tests/WardZeroChecks.cs`, `Tests/test_wardzero_draft.py` and the other checks passing; regenerate `Tests/wardzero-draft.json` only from the script.

## 14. Ready-to-paste prompt for Astra
```
Task: import the Ward Zero revamp (map id `siren`, name "Ward Zero") into the Unity Chip Arena
(unity/ChipArenaConversion) and build its simulation, from Claude's design and presentation branch
`claude/wardzero-rework`.

Read first: docs/arenas/WARDZERO-SIMULATION-CONTRACT.md (this contract), docs/arenas/WARDZERO_PROPOSAL.md,
docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md (the hazard schema precedent and the Cut stair wedges and traversal
gate), docs/SIREN-ZOMBIES.md sections 7-8 (the Hulk and the breach contract), and the draft
docs/arenas/scripts/wardzero_m1.py (`--json`, `--ascii`, `--sections`; frozen as
unity/ChipArenaConversion/Tests/wardzero-draft.json; its `pve` block is the map's PvE content).

Owner: "Rework Ward Zero map, support vent spawning / door spawning, add hazards. Make it a lot bigger, 2 floors,
and improve the lighting, effects, and overall design". Hulk: "... They can also break through the walls to enter
(Stage will eventually holographically recover broken pieces)".

Do:
1. Review the contract; record agreements and changes at its top.
2. Import: replace today's Ward Zero in place (id `siren`, name "Ward Zero", look `quarantine`, keep music and
   rotation) from the draft: blocks, roles, spawns, bounds (ceiling 13, killY -6), hazards, ramps, nav. Choose
   UnityMaps.json with a same-id replace in NativeUnityMaps.Merge, or GameContent.json; record it. Keep the id
   families (section 2) or tell Claude. Do not delete today's hall assets without the owner.
3. Floors: ContentMap.ramps (MapRamp); for each route turn off the listed visual blocks' colliders and add a
   closed invisible wedge like NativeCyberCityStairs (data driven, not by id); the corpse world keeps the treads.
   Add the native traversal gate across both floors (all 8 routes, up and down, host/guest walk/sprint, landing
   and mid-flight at centre/left/right, bot path), in the Editor menu and the build checks.
4. Hazards (sections 4-6): kinds burner, gas, shutter beside the existing grid; validation; damage kinds burner and
   gas through the normal tick path (no stagger, credit by the 7 s rule, RecordDeath and NativeNetworkFrame accept
   them); the shutter's collider, push and no-trap rule; take ownership of Core/NativeWardZeroHazards.cs (change
   it if you need to; the presentation follows).
5. PvE content (section 7): PvePoint.y and the PveEntry fields (surface, level, song, pit, height, clear, hulk,
   block, center, size, normal); write the draft's entries, machines, machine blocks, pads, boss and hide spots;
   move NativeArena.StageVents and the boss coordinates into the data; entry timelines per kind and surface,
   spawning on both floors; drops with clear landings; breach entries per docs/SIREN-ZOMBIES.md section 8:
   name the panel prop `breach:<entry id>`, collision off at breakAt and back at solidAt (restoreAt + 2.6) with
   the overlap rule, replicate breaches[] on the PvE frame.
6. Nav on two floors (section 8): per-floor nodes, wedge links, one-way drop links from navHints.drops,
   hazard-gated links and navHints.safe; bots and adds path between floors; the Siren's possession hide spots on
   both floors.
7. PvP and the Dev Sandbox (section 10).
8. Tests (section 13) beside the existing managed checks; keep Tests/WardZeroChecks.cs and
   Tests/test_wardzero_draft.py passing.

Everything in data: no coordinates in code. Do not change the look, kit, lights, mood, FX, telegraphs or
entrance animations (Claude's); report any id, field, kind or geometry change that affects them in DEV_LOG and
the PR. No Unity Editor, PhysX, Windows or multiplayer claims without running them. Run the repo checks in
docs/DEVELOPMENT.md; record actual results, unrun checks and risks in DEV_LOG.
```
