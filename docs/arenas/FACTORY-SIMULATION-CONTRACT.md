# Factory simulation contract — H0 draft

## Simulation agreement — 2026-10-12, Astra

Task `codex/unity-stage-hazards-20261012`, base shared `aead19c4` (includes
owner-selected map music, PR #146). Both maps are selectable with rotation false.

- Shared `ContentMap.hazards` is `MapHazard[]` beside `MapBounds`. `MapHazard`
  inherits serializable `FactoryHazard`: common fields keep their names and the
  Factory pure-clock signatures remain unchanged. City adds center/first/danger/
  speed/length/charge/live. Legacy maps normalize hazards to null. Strict loader
  validation covers both families, finite values, IDs and nav references.
- `tools/generate_stage_hazards.py` imports both draft scripts without modifying
  them. Block IDs, geometry, segments, rubble and clock definitions remain exactly
  authored. No new visual IDs or geometry changes for Claude. It adds supported
  navigation on all accessible elevations, optional `MapNavLink.hazards[]` (a
  crossing may touch multiple hazards), and `MapNavNode.safe` for cut refuges.
- Damage runs only in Fight. Maglev uses the swept train union and 7-second pilot
  credit; grid uses eight scheduled ticks, mitigation and feet height. Factory
  lethals bypass mitigation and defensive saves, with 6-second credit. Vents keep
  the draft's damage=0; the tick path supports enabling damage later. Existing
  Kernel Panic pair isolation still applies. Adds receive environmental deaths.
- Crane cover is a simulation-owned collider outside the prop frame, evaluated
  on authority, replica and replay clocks through Landed/Regrip. No collider is
  added to the moving rigs. Source content revision changes automatically.
- Factory presentation files are untouched. Cyber City's two hazard-source reads
  use the validated map array; its temporary resource reader is removed. No look,
  kit, backdrop, effects or cue changes. Factory uses `factory-anvil-loop`; Cyber
  City uses `cybercity-neon-riot`, from the owner's supplied recordings.
- Managed and native build gates are separate. Actual executed checks, unrun
  Unity/Windows/multiplayer checks and remaining risks are in DEV_LOG and the PR.


2026-10-12, Claude (presentation) for Astra (simulation). Branch `claude/stage-factory`, base `f575670`. Design:
`docs/arenas/FACTORY_PROPOSAL.md`. Draft map data: `python3 docs/arenas/scripts/factory_m1.py --out DIR` (writes
`factory_m1.json` in the UnityMaps.json shape and a top-down PNG). Nothing here is wired into the game: the map is not in
`UnityMaps.json` and not selectable. Like Brickfall's H0, this is a proposal for Astra to review, change and agree.

## 1. Files and owners

| File | Owner | Content |
|---|---|---|
| `Assets/Conversion/Resources/UnityMaps.json` | **Astra** | The `factory` map: blocks, roles, spawns, bounds, pilot blocks, nav and `hazards[]`. Generated from the draft by an Astra tool (as `tools/generate_brickfall.py`); geometry is sim-owned once agreed |
| `Core/NativeFactoryHazards.cs` | **Astra from H0** (Claude drafted it so the presentation compiles and previews) | `FactoryHazard` schema, the hazard clock, the pure state functions, cue times, validation (§3). Astra may change the internals and add sim-side helpers. Keep the signatures in §3, which the presentation calls |
| `Core/ConversionContent.cs` / `Core/NativeUnityMaps.cs` | Astra | `hazards` on ContentMap (element type `FactoryHazard`, or a renamed shared class agreed with the Cyber City contract), loader validation (`NativeFactoryHazards.Validate`) |
| `Runtime/NativeArena.cs` (hazard damage), bots, PvE nav | Astra | Kill volumes, damage, the landed-load collider, credit, events, nav (§4–§6) |
| `MapLooks.json` `factory` entry, `NativeBlockLooks` look 14 `factory-grating`, `NativeBlockLit.shader`, `Core/NativeFactoryKit.cs`, `Core/NativeFactoryRig.cs`, `Core/NativeFactoryScenery.cs`, `Core/NativeFactoryFx.cs`, `Runtime/NativeFactoryPresentation.cs`, `Runtime/NativeFactoryAudio.cs` | **Claude** | Presentation only: light rig, looks, kit, rigs, telegraphs, backdrop, effects, audio |
| `Tests/FactoryChecks.cs` | Claude (presentation checks); Astra adds sim checks | Gates |

**Id prefixes the presentation keys on** (keep them; geometry may change and the kit follows it). Block ids:
`factory-floor`, `factory-wall-{n,s,w,e}-{base,clad}`, `factory-baywall-{n,s}-*` (incl. `-lintel-*`),
`factory-press-{n,s}-{head,bolster}`, `factory-gallery-{n,s}`, `factory-catwalk-{n,s}`, `factory-stair-*` (a flight is the
id minus its last `-NN`; landings end `-landing`), `factory-column-*`, `factory-runway-*`, `factory-roof-*`,
`factory-lathe-*` (`-head` for the headstock), `factory-mill-*`, `factory-cabinet-*`, `factory-rack-*`,
`factory-bay-rack-*`, `factory-coils-*`, `factory-crate-*`, `factory-crib-*`, `factory-shelf-*`, `factory-screen-*`,
`factory-bins-*`, `factory-dock-{a,b}-{office,container,forklift,pallets}`, `factory-pulpit-{a,b}-{deck,leg-*,parapet-*,roof}`,
`factory-ladle`, `factory-ladle-car`, `factory-bay-{billets,ingots,plates,moulds,drums,beams,hammer}-{a,b}`.
Hazard ids: `factory-saw-slot-{n,s}`, `factory-saw-rail-{n,s}`, `factory-crane`, `factory-press-{n,s}`,
`factory-vent-bay-{a,b}`, `factory-vent-hall-{n,s}`. The kit only dresses `static` blocks with identity rotation.

## 2. Schema additions

`ContentMap.hazards`: an array of `FactoryHazard` (JsonUtility: vectors `{x,y,z}`, no nested arrays, every field written):

```json
{ "id": "factory-saw-slot-n", "kind": "saw-slot",
  "a": {"x": -18, "y": 0, "z": 19.5}, "b": {"x": 18, "y": 0, "z": 19.5}, "axis": {"x": 0, "y": 0, "z": 1}, "size": {"x": 1.6, "y": 1.6, "z": 0.1},
  "radius": 0.8, "start": 2, "period": 14, "offset": 0, "warn": 1.6, "rise": 0.35, "travel": 6.0, "dwell": 0, "hoist": 0, "carry": 0,
  "damage": 0, "tick": 0, "push": 0, "lethal": true, "alternate": true, "zones": [] }
```

| Field | saw-slot | saw-rail | crane | press | vent |
|---|---|---|---|---|---|
| `a`, `b` | blade centre path at floor level | blade centre parked in the wall / fully out | bridge travel ends on the runway top (`a.y` = rail base 13.2) | ram centre open / slammed | grate centre (`a`) |
| `axis` | axle (unit, across the path) | axle | — | — | — |
| `size` | blade box (diameter, diameter, thickness) | blade box | load (2.2, 0.9, 4.0) | ram (2.9, 0.5, 1.3) | steam column |
| `radius` | blade radius | blade radius | — | — | — |
| `warn` / `rise` / `travel` / `dwell` / `hoist` | warning / rise and sink / run / — / — | warning / — / out and back / held / — | telegraph / fall / to the zone / landed / hoist | warning / slam / lift / held / — | hiss / — / — / burst / — |
| `carry` | — | — | load underside when hoisted (9.5) | — | — |
| `zones[]` (`MapVolume`) | — | — | drop zones in visiting order: centre on the floor, `size.y` = lethal height | — | — |
| `alternate` | reverse every other run | — | — | — | — |
| `lethal` / `damage`, `tick`, `push` | instant kill | instant kill | instant kill | instant kill | `damage` per `tick` s (0 = cosmetic) |

Shared: `start` (s after the arm time), `offset` (s), `period` (s). The draft values are in FACTORY_PROPOSAL.md §3.8 and
`factory_m1.py`. `NativeFactoryHazards.Validate` rejects unknown kinds, duplicate ids, a motion longer than its period,
bad blades, presses or crane data. `contentRevision` (the hash of UnityMaps.json) already covers the definitions.

## 3. Pure functions (Core/NativeFactoryHazards.cs; presentation and simulation call the same ones)

```csharp
public static double Clock(NativePhase phase,double fightAt,double now);      // now - fightAt - 4 in Fight and Result; -inf otherwise
public static bool Cycle(FactoryHazard h,double clock,out int n,out double u); // cycle index and time into it (false before the first)
public static bool Reversed(FactoryHazard h,int round,int n);                  // alternate runs; odd rounds start reversed
public static int ZoneOf(FactoryHazard h,int round,int n);                     // crane zone of cycle n (odd rounds start one zone on)
public static FactoryHazardState State(FactoryHazard h,int round,double clock); // the pose, phase and live volume
public static int Cues(FactoryHazard[] hz,int round,double from,double to,FactoryCue[] into); // discrete moments in (from, to]
public static void Validate(FactoryHazard[] hz);
```
- `round` is `NativeStructureFx.RoundIndex(set, round)` (= set·64 + round; only its parity is used).
- `FactoryHazardState`:
  - `Phase` (Parked, Warning, Rising, Running, Sinking, Holding, Returning, Slamming, Lifting, Travelling, Falling, Landed, Regrip, Hoisting, Bursting), `Cycle`, `Zone`, `From`;
  - `Lethal`: the kill volume is live (a vent with damage: the column damages);
  - `Reverse`, `Landed`, `Attached`;
  - `U` (path fraction, press stroke −.06..1, vent level), `Lift` (slot blade exposure), `Spin`, `Rate`, `Warn` (0..1);
  - crane: `X`, `Z`, `HookY`, `LoadY`, `Sway`, `ImpactAt` (hazard clock of this cycle's impact);
  - **`BoxCenter`, `BoxSize`: the live volume.** Saw: the blade box at its centre (the slot box is clipped to y ≥ 0 by you). Press: the ram footprint from the floor to the ram's underside. Crane: the current zone's box. Vent: the column.
- Constants: `ArmDelay` (= NativeStructureRules.ArmDelay, 4 s), `Exposure` 0.15 m (slot blade lethal above the floor), `LethalRam` 1.8 m, `ImpactGrace` 0.06 s, `RegripSeconds` 1.5 s.
- Pure and allocation free. Host, guests and the killcam evaluate them from replicated data, so **no hazard state is sent**.
- The presentation evaluates at the drawn arena's field clock (`ArenaPresentation.FieldClock`). The simulation evaluates at the authoritative match time of the tick.

## 4. Simulation rules (Astra)

1. **Hits.** Each tick, for each hazard whose state is `Lethal`, test every pilot capsule (radius 0.42, half height 0.85) against `BoxCenter`/`BoxSize`, and the slot box clipped to y ≥ 0. Do the same for bots and PvE adds.
2. **Instant kill.** A `lethal` hazard sets health to 0 whatever the armour, block, damage multipliers or i-frames (owner decision 5; recommended) and emits `HazardHit` (Detail = kind: `saw`, `crane`, `press`) and a `Death` with `DamageKind` `hazard` (Detail = hazard id). Kill feed tags: SAW, CRUSHED, PRESSED.
3. **Vents.** `damage` per `tick` while inside the column and `Bursting`. A `HazardHit` with Detail `vent` goes through the normal damage path (multipliers apply), with no push.
4. **Credit.** The last pilot who damaged the victim within 6 s (Brickfall decision 7), else environmental. No self-credit.
5. **The landed load** is a static box (`size`, centre (X, size.y/2, Z)) present while `Landed || Regrip`. It is removed when `Hoisting` begins; pilots on it fall. Pose it from the state on every client and in the killcam (it is not a replicated prop). Bots treat it as cover and an obstacle.
6. **Arming.** Nothing moves or hurts before FightAt + 4 s. Through the countdown, intros and the draft every hazard is `Parked`.
7. **Spawn safety.** No spawn within 3 m of any hazard's swept footprint (the draft's closest is 9.1 m). Sandbox respawns also skip points inside or within 3 m of a live volume.
8. **Collision of the rigs.** The crane bridge, trolley, hook and carried load, the blades, the rail-saw carriages and the rams are **not** colliders. Rams and blades are volumes only. The press tunnel is open space with its kill volume.

## 5. Events and replicated state
- **Events:** the existing `HazardHit` (Detail = hazard kind) and `Death` (`DamageKind` `hazard`). No new event kinds: the presentation derives every visual and audio moment from the states and `Cues`.
- **Replicated state:** none new (phase, FightAt, round, set and time are already in the match frame). The killcam replays the match frames, so its hazards replay exactly.
- **Network:** no protocol change beyond adding the map (contentRevision changes with UnityMaps.json, as Brickfall).

## 6. Bots and PvE nav (Astra)
- Nav graph for the intact layout: floor, the four dock stairs, two gallery stairs from the halls, four catwalk stairs, two pulpit stairs, the bay doors, the press tunnels.
- **Phase-gated links:**
  - slitter lane cells within 4 m of the blade while Warning–Sinking;
  - the catwalk link through the rail saw (x ±8) only while Parked with ≥1 s left;
  - the press tunnel link only while open with ≥1 s left;
  - the crane zone cells from Travelling until the landing, the landed load an obstacle;
  - vent columns while Warning or Bursting (if damaging).
- Bots never path along the slot line while it is live, and never wait inside a zone.

## 7. Tests Astra should add
1. Determinism: `State` and `Cues` give identical results for the same inputs on repeated calls and across a host/guest/killcam evaluation of the same frames.
2. Timing: each hazard's lethal windows match FACTORY_PROPOSAL.md §3.8 (slitter about 6.5 of 14 s, rail 1.25 of 5 s, press about 1.33 of 6 s, crane about 0.17 s per cycle); nothing lethal before FightAt + 4 s or outside the fight and result.
3. Kill volumes: a capsule placed on each path is killed exactly while the blade overlaps it, and one 0.6 m off the slot line never is. The press kills inside the footprint only while the ram is below 1.8 m. The crane kills inside the zone box only during the lethal window and never outside the zone.
4. The landed load collides from landing until the hoist, and riders drop when it lifts.
5. Instant kill ignores multipliers and block; vent damage respects them; credit follows §4.4.
6. Spawn safety (§4.7), jittered spawn LOS (the draft's checks), ground connectivity with every phase-gated link closed.
7. Validation rejects bad definitions (`NativeFactoryHazards.Validate`).
8. Old-client refusal (contentRevision) once the map ships.

## 8. Ready-to-paste prompt for Astra

> **Astra:** the owner asked for "a couple of more stages on the same quality of brick fall. A larger map with stage hazards. One is a factory (with like instant kill buzzsaws or things that can fall on players … I can have astra handle the coding portion.)" Claude designed the **Factory** (map id `factory`) and built its presentation on `claude/stage-factory` (base `f575670`). Read `docs/arenas/FACTORY_PROPOSAL.md` and `docs/arenas/FACTORY-SIMULATION-CONTRACT.md`. The map is an 80×64 m steel works: two roofed halls, each with a floor-slot buzzsaw lane and a rail saw across its catwalk; an open crane bay where an overhead crane drops a steel bundle into one of four painted zones every 20 s; a die press in each bay wall; optional steam vents. Claude owns all presentation (looks, kit, rigs, telegraphs, effects, audio). You own collision, hazard rules, spawns, bots, networking and tests. Work on a `codex/` task branch from the newest Unity integration head (check it first) merged with `claude/stage-factory`.
> 1. Generate the `factory` map into `UnityMaps.json` from `python3 docs/arenas/scripts/factory_m1.py --out DIR` (`factory_m1.json`: 353 blocks with roles, spawns, bounds, 11 pilot blocks, 11 hazards). Keep the id prefixes listed in the contract §1; adjust geometry as the sim needs (the dressing follows). Add `hazards` to ContentMap with the `FactoryHazard` element (or a shared class agreed with the Cyber City contract). Validate with `NativeFactoryHazards.Validate`. Make the map selectable with `rotation:false`.
> 2. Take ownership of `Core/NativeFactoryHazards.cs` (pure clock, states, cue times). Keep the §3 signatures. Implement §4: lethal volumes from `State().Lethal`/`BoxCenter`/`BoxSize` per tick for pilots, bots and adds; instant kill with `HazardHit` (saw/crane/press) and `Death` `DamageKind` `hazard`; vent tick damage; credit to the last damager within 6 s; the crane's landed-load collider while `Landed || Regrip`, posed on every client and in the killcam; no hazard motion or damage before FightAt + 4 s.
> 3. Nav: the §6 phase-gated links for bots and the PvE horde, so they wait for the saws, tunnels and zones.
> 4. Tests: §7. Run the repo checks and record actual results, unrun Unity/Windows/multiplayer checks and risks in DEV_LOG and the PR.
> Do not change visuals, shaders, MapLooks.json or the presentation files. If an id or a hazard field must change, note it in DEV_LOG and the PR so Claude can follow.
