# Cyber City simulation contract (H0 draft)

2026-10-12, Claude (presentation) for Astra (simulation). Branch `claude/stage-cybercity`, base `f575670`.
Design: `docs/arenas/CYBERCITY_PROPOSAL.md`. Draft layout: `docs/arenas/scripts/cybercity_m1.py` (frozen as
`unity/ChipArenaConversion/Tests/cybercity-draft.json`). This is a proposal for review, in the shape of
`BRICKFALL-SIMULATION-CONTRACT.md`. Astra owns every simulation decision below and may change any of it; the
presentation follows whatever the merged contract says. Nothing here is implemented in the simulation yet.

## 1. Owners and files
| Area | Owner | Files |
|---|---|---|
| Map content: blocks, roles, segments, spawns, bounds, pilot blocks, nav, `hazards[]`, `rotation`, `music` | Astra | `Assets/Conversion/Resources/UnityMaps.json`, a generator like `tools/generate_brickfall.py` |
| Hazard schema and loader validation | Astra | `Core/NativeUnityMaps.cs`, `Core/ConversionContent.cs` (`ContentMap.hazards`) |
| Hazard timing (the reference functions) | shared: written by Claude, owned by Astra once merged | `Core/NativeCyberCityHazards.cs` |
| Kill and damage volumes, damage kinds, credit, events, frame validation | Astra | `Runtime/NativeArena*.cs`, `Runtime/NativeDeaths.cs`, `Runtime/NativeNetworkFrame.cs`, `Core/NativeStagger.cs` |
| Bots and PvE nav gating | Astra | nav generator, bot code |
| Billboard segments | Astra (existing §5 system) | as Brickfall |
| Map selection, rotation, Random | Astra (owner decides) | as Brickfall |
| Look, dressing kit, backdrop, atmosphere, hazard telegraphs, sound cues | Claude (on this branch) | `Core/NativeCyberCity{Looks,Kit,Scenery,Fx,Ambience}.cs`, `Runtime/NativeCyberCity{Presentation,Audio}.cs`, `MapLooks.json` `cybercity`, the bank in `Resources/NativeBlockLit.shader` |
| Kill-feed labels MAGLEV / GRID | Claude, after the damage kinds exist (or Astra while integrating, as COLLAPSE was) | `Core/NativeDeathBroadcast.cs`, `Runtime/NativeDeathBroadcastPresentation.cs` |

## 2. Map content
- Map id `cybercity`, name "Cyber City", tag "A neon megacity interchange at 02:00. Mind the Helix Line.", look `neon-rain`, ambience `cybercity`, music proposed `kinetic-city`. `unity=true` after merge, `rotation` false (selectable, excluded from Random, Brickfall's rule) until the owner decides.
- **Draft numbers:** 702 blocks (668 static, 20 cover, 4 structural, 10 carried), 2 segments, initial prop frame 34 (cover + structural + carried, under the 360 cap). Bounds x ±38, z ±30, ceiling 24, killY -8. Eight pilot blocks: the four frontage tops (bottom 15.9; they cover the portal tops), the two spire tops (23.9), the two tunnel mouths (bottom -4, so pilots cannot walk into the tunnels).
- **Spawns:** FFA 8 and teams 2 x 4 exactly as the draft (`--json`); y 1.1, yaw toward the origin. Two-team tables only for exactly two teams of at most four (Brickfall's rule).
- **Id families** (all prefixed `cybercity-`; the presentation keys on them, so keep them or tell Claude): `frontage-{n,s,e,w}*`, `portal-{e,w}*`, `trench-floor`, `trench-wall-*` (with `-alcove-*`, `-lintel-*`, `-exit-*`), `guideway`, `ground-{n,s}-*`, `pit-{nw,ne,sw,se}-*`, `plaza-*`, `grid-{w,e}-*` (`-deck`, `-pylon-{n,s}-{1,2}`, `-beam-*`), `station-*` (`-deck`, `-col-*`, `-lift`, `-lift-head`, `-rail-*`, `-stair-{n,s}-*`, `-landing-*`, `-bench-*`), `skybridge-{ne,sw}-*`, `nova-*` / `vega-*` (`-s{0,1,2}-{north,south,east,west}-*`, `-floor-*`, `-roof-*`, `-parapet-*`, `-stair-*`, `-spire`, `-roof-unit-*`, `-lobby`), `lantern-*` / `signal-*` (`-wall-*`, `-bar`, `-booth`, `-box-*`, `-counter`, `-post-*`, `-stair-*`, `-stall-*`, `-vend-*`), `alley-{n,s}-*`, `neon-*` / `rain-*` (`-van-*`, `-kiosk`, `-pillar`), `lamp-{n,s}-*`, `prom-{n,s}-*`, `rank-*` / `dock-*` (`-canopy`, `-post-*`, `-cab-*`, `-pylon-*`, `-barrier`, `-wall`), `billboard-{n,s}-*`, `fell-{n,s}`. Rubble: `rubble-cybercity-billboard-{n,s}-{0,1,2}`. Mirror pairs map (x, z) -> (-x, -z) with n<->s, e<->w, nw<->se, ne<->sw, nova<->vega, lantern<->signal, neon<->rain, rank<->dock.
- Simulation may move geometry; if an id family changes, the matching `MapLooks.json` glob and the kit rule in `NativeCyberCityKit.Build` need the same change (Claude's).

## 3. Hazard schema: `ContentMap.hazards`
Add `public MapHazard[] hazards;` to `ContentMap` (beside `segments`, `nav`, `pilotBlocks`) and null it for old maps where `NativeUnityMaps` clears the other Unity fields. The class exists today in `Core/NativeCyberCityHazards.cs`; **move it beside `MapBounds` in `Core/NativeUnityMaps.cs` with the same name and fields** (and delete it from the hazards file in the same commit), so the presentation keeps compiling.

```csharp
[Serializable] public sealed class MapHazard
{ public string id,kind; public V3 center,size,axis;
  public float first,period,offset,warn,danger,speed,length,charge,live,tick,damage; public bool lethal; }
```
JsonUtility, every field written (a missing number reads 0). Draft values:

| id | kind | center | size | timing | damage |
|---|---|---|---|---|---|
| `cybercity-maglev` | `maglev` | (0, -2.45, 0) | (80, 3.1, 5.4) | first 18, period 32, warn 6, danger 3, speed 44, length 42 | `lethal` true |
| `cybercity-grid-w` | `grid` | (-24, 0.5, 0) | (4, 1, 6) | first 4, period 14, offset 7, charge 2.5, live 2 | tick 0.25, damage 13 |
| `cybercity-grid-e` | `grid` | (24, 0.5, 0) | (4, 1, 6) | first 11, period 14, offset -7, charge 2.5, live 2 | tick 0.25, damage 13 |

**Loader validation (proposed `Need` rules):** unique id with the map prefix; kind `maglev` or `grid` (anything else rejects the map); finite vectors, size > 0; `period > 0`, `first >= 0`, `first + offset >= 0`; maglev: `speed > 0`, `length > 0`, `period > warn > danger > 0`, `(size.x + length) / speed + 2 + warn < period` (passes never overlap); grid: `charge > 0`, `live > 0`, `charge + live + 1 <= period`, `tick > 0`, `damage > 0`, `lethal` false.

## 4. Timing (pure; nothing replicated)
Reference functions, `Core/NativeCyberCityHazards.cs` (allocation free, struct results, checked in `Tests/CyberCityChecks.cs`):
```csharp
NativeHazardState NativeCyberCityHazards.Evaluate(MapHazard h, int round, double fightAt, double time);  // any kind; unknown/null -> Idle
NativeHazardState NativeCyberCityHazards.At(MapHazard h, NativePhase phase, int round, double fightAt, double time); // Idle outside Fight/Result
NativeHazardState NativeCyberCityHazards.Maglev(...), Grid(...);   // same arguments as Evaluate
bool NativeCyberCityHazards.TrainBox(MapHazard h, in NativeHazardState s, out V3 center, out V3 size); // the train's body, clipped to the portals
bool NativeHazardState.Clear(double margin);  // Idle with at least `margin` s before the next Warn (nav gate)
// NativeHazardState { Phase (Idle, Warn, Danger, Active, Clear), Cycle, Direction (+1 = west to east), Since, Until, Lead, Head, Tail, Level, Telegraph }
```
- `round = NativeStructureFx.RoundIndex(match.Set, match.Round)` (set * 64 + round, so parity is the round's). `fightAt = match.FightAt`, `time = match.Time` on the host; guests and the killcam use the same match frame values, so all agree with no new state.
- **Maglev:** pass k's head reaches its near portal at `fightAt + first + k * period`; west to east when `k + round` is even. Warn from `warn` s before, Danger from `danger` s before, Active while any of the train is between the portals (`(size.x + length) / speed` = 2.77 s), Clear 2 s, then Idle. Head x = near portal + direction * speed * (time - arrival); tail = head - direction * length.
- **Grid:** cycle k starts at `fightAt + first + k * period`, plus `offset` on even rounds (west +7, east -7, so the couplings swap order). Idle `period - charge - live - 1` s, Warn (charging) `charge`, Active (live) `live`, Clear (discharge) 1 s.
- **Phases:** the simulation applies damage only in `NativePhase.Fight`. Presentation shows Fight and Result (a pass that is running finishes behind the round's end) and Idle everywhere else, because FightAt still holds the last round's start during intro, countdown and draft.
- Round 1 with the fight at 10 s: maglev warn 22, danger 25, head at the east portal 28 (E->W), clear 30.77, next pass warn 54 (W->E); grid-w charge 22.5, live 25-27, every 14 s; grid-e 7 s later. On round 2 the east coupling leads.

## 5. Kill and damage rules (proposed)
**Maglev (`DamageKind = "maglev"`, Detail = hazard id):**
- Each simulation step in Fight, take the train's box at the step's start and end (`TrainBox` at t - dt and t) and use their union (the train moves 2.2 m per 20 Hz step), so nobody passes through between steps.
- A living pilot (or PvE add) whose capsule intersects the union dies: an instant kill on a path that ignores shields, armour and damage caps (as `lethal` says), the same way the overtime damage uses `forced`. Spawn protection does not save a pilot in the train (no spawn is within 6 m of the Cut). Death impulse along the train's direction (about 30 m/s) plus 6 m/s up, for the ragdoll.
- Credit: the existing rule (`NativeDeaths`: the victim's last pilot attacker within 7 s, otherwise environmental), so knocking someone into the train scores. No self-credit.
- The train has no collider: it does not block shots, carry pilots or push. The alcoves (|z| 3..4.2, recessed under 1.6 m lintels) and everything above y -0.9 (the bridges and couplings) are outside the box.

**Couplings (`DamageKind = "grid"`, Detail = hazard id):**
- While a grid is Active, at each tick of its own clock (`floor(Since / tick)` changes; 8 ticks per live window, the first at Since = 0) every living pilot or add whose feet are inside the deck volume (x, z inside `center ± size / 2`, capsule bottom between the deck top and `center.y + size.y / 2`) takes `damage` (13) through the normal mitigation path, as the Siren stage vents do (`NativeSirenStage.cs`, `stage:true`). No stagger (add the kind to `NativeStagger`'s `None` list). A pilot standing through the whole window takes 104 (kills a base 100 HP pilot); a sprinted crossing 26-52.
- One hit per pilot per tick; per-pilot key on the tick index, not on wall time, so host and replay agree.

**Billboards:** two `tear-hinge` segments on the existing §5 contract (`supports` the two `structural` struts, `failBelow 2`, telegraph 1.0, duration 0.8, crush 45 + push 6, cap min(60, .6 max HP), rubble 3 boxes each). Struts take explosive damage only, 80 HP, as Brickfall members. No new segment rule. `NativeStructureRules.Validate` passes on the draft (checked).

**Other:** killY -8 is authored; the Cut floor at -4 is solid, so nothing reaches it. Bounds clamp as Brickfall.

## 6. Bots, nav and PvE (proposed)
- Nav links gain an optional `hazard` (hazard id) and the generator tags every link that enters or runs along the Cut floor with `cybercity-maglev`, and every link across a coupling deck with its grid. A tagged link is usable only while `NativeHazardState.Clear(margin)`: maglev margin 6 s (a full trench run plus slack), grid margin 1.5 s.
- Alcove and stair-exit nodes in the Cut are safe nodes: a bot on the Cut floor when the maglev leaves Idle routes to the nearest one (the farthest floor point is 3.0 m from a pocket) and holds there until Clear.
- Bots wait at the coupling pylons from Warn to Clear. Fell zones and crush boxes are avoided while a billboard segment is stressed or falling (Brickfall's rule); rubble becomes low cover.
- Cut edges have only 6 cm presentation kerbs: the nav must not route a bot off an edge except through authored drop links (the obstacle ray must not treat the 4 m drop as walkable).
- PvE adds die in both hazards like pilots (environmental credit unless a pilot hit them within 7 s). Proposed horde entries: the tunnel mouths (behind the pilot blocks), the market booths, the yard canopies.

## 7. Events, replication and protocol
- **No new event kinds.** Hit and Death carry `DamageKind` `maglev` or `grid`, Detail the hazard id. `RecordDeath` must keep the kind (today it only records `collapse`), and `NativeNetworkFrame`'s death validation must accept `maglev` and `grid` (it accepts only null, empty and `collapse`).
- **No new replicated state:** the timing derives from fields the match frame already carries (time, phase, fightAt, round, set). Segments use the existing `segments[] {id,state,t0}`.
- The `UnityMaps.json` change alters the content revision (SHA of the file), as every map edit does; no protocol bump is needed for the hazards themselves.

## 8. What the presentation already does (this branch)
- Reads hazards from `Resources/UnityMaps.json` by map id today (`NativeCyberCityHazardSource`), so it works as soon as the map entry carries `hazards[]`. When `ContentMap.hazards` exists, switch `MetroRebind` (Runtime/NativeCyberCityPresentation.cs) and `CyberCityCues` (Runtime/NativeCyberCityAudio.cs) to `arena.Map.hazards` (one line each) and delete the source class.
- Binds the maglev and the two grids by `kind` (the grid side by the sign of `center.x`), and the billboards' holo content to each segment's block ending in `-screen`.
- Everything is dormant unless the map id is `cybercity`; without hazards the kit and look still apply and the telegraph rigs stay dark.

## 9. Tests for Astra
1. Loader: the Cyber City entry loads; old maps get `hazards = null`; each §3 validation rule rejects a broken copy.
2. Timing: the §4 round 1 table at fight 10 s; even-round direction and coupling order; `At` is Idle in Countdown/Intro/Draft; purity (same inputs, same state); host and replay match frame give equal states.
3. Maglev kill: a pilot at (20, -3, 0) dies when the head passes (DamageKind `maglev`, Detail `cybercity-maglev`); a pilot in an alcove (x -28, y -3, z 3.6) and one standing on the plaza bridge (y 0) survive; a pilot crossing the box between two steps still dies (swept union); spawn protection does not apply; credit to a pusher within 7 s; nothing outside Fight.
4. Grid: 8 ticks of 13 per live window to a pilot standing on the deck; none in Warn or Clear; none to a pilot 1.1 m above the deck; one per tick per pilot; mitigation applies; no stagger.
5. Billboards: Validate passes; one strut destroyed -> stressed -> falling after the telegraph -> rubble; crush cap; events as Brickfall.
6. Network: a death frame with `maglev` or `grid` validates; an unknown kind still fails.
7. Nav: tagged Cut links are unusable from 6 s before Warn to the end of Clear; bots in the Cut at Warn reach a safe node; coupling links closed Warn to Clear.
8. Spawns: every spawn at least 6 m from every hazard and crush volume (the draft's minimum is 6.0 m).

## 10. Ready-to-paste prompt for Astra
```
Task: build the Cyber City stage simulation (map id `cybercity`) in the Unity Chip Arena
(unity/ChipArenaConversion), from Claude's design and presentation branch `claude/stage-cybercity`.

Read first: docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md (this contract), docs/arenas/CYBERCITY_PROPOSAL.md
(§2-§9), docs/arenas/BRICKFALL-SIMULATION-CONTRACT.md (the precedent), and the draft layout
docs/arenas/scripts/cybercity_m1.py (`--json`, `--ascii`, `--sections`; frozen as
unity/ChipArenaConversion/Tests/cybercity-draft.json).

Owner's request: "add a couple of more stages on the same quality of brick fall. A larger map with stage
hazards. ... the other is a cyber city. I can have astra handle the coding portion."

Do:
1. Review the contract; record agreements and changes at its top (as BRICKFALL-SIMULATION-CONTRACT.md does).
2. Add `ContentMap.hazards` (MapHazard[]): move the MapHazard class from Core/NativeCyberCityHazards.cs to
   Core/NativeUnityMaps.cs beside MapBounds (same name and fields), null it for old maps, validate (§3).
3. Generate the `cybercity` entry in Assets/Conversion/Resources/UnityMaps.json from the draft (a generator like
   tools/generate_brickfall.py): blocks and roles, the two billboard segments with rubble, spawns, bounds,
   pilot blocks, hazards, nav with hazard-tagged links (§6). Keep the id families in §2 or tell Claude.
   Selectable, rotation false, excluded from Random (owner decides later).
4. Simulation: the maglev instant kill from the swept TrainBox union, the coupling tick damage, both only in
   Fight (§5), using NativeCyberCityHazards' pure timing (owned by you once merged; change it if you need to,
   the presentation follows). DamageKind `maglev`/`grid`, Detail = hazard id, RecordDeath keeps the kind,
   NativeNetworkFrame accepts both, NativeStagger None for both, credit by the existing 7 s rule.
5. Bots and PvE: hazard-gated nav links (NativeHazardState.Clear), safe nodes in the alcoves, edge handling.
6. Switch the presentation's hazard source to arena.Map.hazards (§8, one line each in
   Runtime/NativeCyberCityPresentation.cs and Runtime/NativeCyberCityAudio.cs) and delete
   NativeCyberCityHazardSource.
7. Tests (§9) beside the existing managed checks; keep Tests/CyberCityChecks.cs passing (regenerate
   Tests/cybercity-draft.json only if you change the draft script).

Do not change the look, kit, backdrop, FX, telegraphs or sound cues (Claude's); report any id or geometry
change that affects them in DEV_LOG and the PR. No Unity Editor, Windows or multiplayer claims without running
them. Run the repo checks in docs/DEVELOPMENT.md; record actual results, unrun checks and risks in DEV_LOG.
```
