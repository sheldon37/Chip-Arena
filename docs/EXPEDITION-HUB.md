# Expedition hub: fenced sanctuary, gates, wilds (exp-layout-3)

Claude, 2026-10-15, branch `claude/expedition-hub` (base shared `claude/sleepy-ritchie-g8ruhv` at `ec7a7eb`).
Status: implemented on the branch, not merged, not released. The layout, fence collision, dressing and gate
presentation are live in the imported town. Gate interaction, the hub spawn director, money and the sanctuary
rules are proposals for Astra (the contract is at the end).

Owner request (2026-10-15, verbatim): "For expedition, lets extend the hub map a little bit. I want zombies to spawn
around the hub, not as much as inside an instance of course~ but still some way to gather money and have some danger.
I think players need to run a bit further to get to their instances. Also, for the safe hub, lets maybe fence off the
area, and use animated wooden / metal gates that players can open. Prevent zombies from entering safe area. Implement
some kind of mechanic so players can't just sit in the safe zone and kill zombies."

## The plan in numbers

| | exp-layout-2 (before) | exp-layout-3 (this branch) |
| --- | --- | --- |
| Town bounds | 120 x 120 m (x, z +-60) | 192 x 156 m (x +-96, z -96..60) |
| Walkable street (outside the final plaza) | 2,901 m2 | 7,978 m2 |
| Sanctuary | 24 m plaza, 1.1 m barricades with four 6 m gaps | the same 24 m plaza (same volume), closed by a 3.2 m fence across its four street mouths, a 4 m gate in each |
| Ward Zero door | x 44 (32 m from the plaza's edge) | x 84, out across the Meridian Approach |
| Reserved foundry / transit doorways | (-44, 0) / (0, -44) | (-84, 0) / (0, -84) |
| Run from the gate to the Ward Zero door | about 31 m (4.6 s running), straight line from the old barricade gap | 72.9 m walked (70.2 m straight): 10.7 s running, 7.9 s sprinting |
| Town spawns | 50 | 84 (34 new in the wilds; 4 moved clear of the gates) |
| Town navigation | 4,057 nodes, 7,367 links | 9,505 nodes, 17,602 links |
| Blocks | 124 | 203 |

Run speed is NativeArena's 6.8 m/s at speed 1.0; sprint is x1.35 (9.18 m/s) and drains 12 stamina a second. The walked
distance is an 8-connected path on the pilot's 0.5 m floor grid from the gate's threshold (the centre of its opening)
to the door's approach. The target is 70 m or more. The reserved routes measure the same: 72.9 m to the foundry plot
and 71.7 m to the station.

`python3 docs/arenas/scripts/expedition_city.py --png <file>` draws the plan offline. The renders are not in the repository.

## Layout

The town is still the ring-and-cross city around the sanctuary. Each of the three instance plots (east, west, south)
now sits at the far end of a district of wilds that opens off the ring road. The north (the final gate, the final plaza
and the city hall) is unchanged.

```
                 final plaza (unchanged)
                 [ final gate z 36 ]
   Ironworks       N gate (wood)        Meridian
   Yard (west)  W gate  SANCT  E gate   Approach (east)  -> Ward Zero door x 84
   foundry x-84 (wood)  24 m  (metal)
                       S gate (metal)
                    Station Road (south) -> transit plot z -84
```

### Sanctuary, fence and gates

- The sanctuary volume is unchanged: centre (0, 4, 0), 24 x 8 x 24. The inner blocks' walls enclose the plaza except at
  its four street mouths. The fence closes each mouth just outside the volume, at |x| or |z| 12.0..12.4. The sanctuary
  therefore ends at the fence's inner face, and a gate's threshold is outside it.
- The fence is 3.2 m tall and 0.4 m thick, in 8 sections (`sanctuary-fence-<kind>-<side>-<k>`). Every section carries a
  pilot exclusion column from 3.1 m up (NativeUnityMaps.ExcludeTops), so a single or air jump (1.57 m and +1.27 m)
  cannot clear it.
  - North and west mouths: a wooden palisade on the civic and residential side.
  - East and south: scrap-metal panels toward the Meridian lab and the station yards.
- The 1.1 m barricade ring is gone.

| Gate | Kind, motion | Centre (fence plane) | Out (yaw) | Opening | Closure (collider) |
| --- | --- | --- | --- | --- | --- |
| `exp-city:hub-gate:north` | wood, swing | (0, 0, 12.2) | 0 | 4.0 x 3.2 m | centre (0, 1.6, 12.2), 4.0 x 3.2 x 0.4 |
| `exp-city:hub-gate:east` | metal, slide | (12.2, 0, 0) | 90 | 4.0 x 3.2 m | centre (12.11, 1.6, 0), 4.0 x 3.2 x 0.62 (reaches the inner track) |
| `exp-city:hub-gate:south` | metal, slide | (0, 0, -12.2) | 180 | 4.0 x 3.2 m | centre (0, 1.6, -12.11), 4.0 x 3.2 x 0.62 |
| `exp-city:hub-gate:west` | wood, swing | (-12.2, 0, 0) | 270 | 4.0 x 3.2 m | centre (-12.2, 1.6, 0), 4.0 x 3.2 x 0.4 |

Each gate record also carries these:
- `interact`: 4.4 x 3 x 4.4, reaching 2.2 m either side of the fence;
- `sensor`: 6 x 3.4 x 8, the auto-close watch volume;
- `inside` and `outside`: threshold points 1.6 m either side;
- `fence`: the two flanking section blocks.

The openings pass a Hulk (2.0 m clear either side of the centre, no lintel; the header beam is at 4.0 m).

### Wilds districts (the far ring)

| District | Volume | Leads to | Cover |
| --- | --- | --- | --- |
| Meridian Approach (east) | x 44..84, z -30..30 | the Meridian Biocontainment frontage and the Ward Zero door at x 84 | evacuation checkpoint chicane at x 47, booth and sandbag nests, military truck and APC, stacked shipping containers, rubble heap, ruined walls, power pylon, floodlights at the forecourt |
| Ironworks Yard (west) | x -84..-44, z -30..30 | the sealed foundry plot (CITY IRONWORKS) | chicane, three rail boxcars on two tracks, slag heaps, coal tank, crates, container, pylon, floodlight |
| Station Road (south) | x -30..30, z -84..-44 | the sealed CENTRAL STATION | chicane, two crashed buses, ticket booth, bus shelters, rubble, cars, floodlight |

Sight lines:
- The east route runs down the z = 0 axis. From the east gate, the Meridian portico's beacon and floodlights are visible
  72 m away through the fog.
- Every piece of cover off the axis is lower than eye height on the axis itself; the one exception is the 1.2 m rubble
  heap, which you step round.
- The chicane at each district's mouth breaks the straight run without blocking the view.
- The north gate still looks straight up the avenue to the final checkpoint, 24 m away.

### Spawn zones

Every town spawn record outside the final plaza lies in exactly one zone. The nine final-plaza spawns stay the final
encounter's. Zones are the hub record's `zones[]`: ring, volume and spawn `e` indices. A spawn on a shared edge belongs
to the first zone listed. The distances are walked from the nearest gate's threshold.

| Ring | Zones | Spawns | Walking distance from a gate |
| --- | --- | --- | --- |
| near | near-north, near-east, near-south, near-west (the inner streets beyond the aprons) | 18 | 10-29 m |
| mid | mid-north (forecourt), mid-east, mid-south, mid-west (the ring road and the city's edge) | 23 | 18-45 m |
| far | far-east, far-south, far-west (the wilds) | 34 | 47-82 m |

Aprons:
- No spawn exit lies within 10 m of a gate; the nearest is 10.2 m (`vent-avenue`).
- No zone volume lies within 8 m of a gate.
- Spawn exits stay 3 m or more apart, and 8 m or more from the Ward Zero door, its approach and its returns.

## Hub danger (proposal for the director: no balance in the manifest)

The town's spawn records and zones are layout data. The numbers below are proposals for Astra's hub spawn director,
anchored to the M1 Ward instance: a cap of 8 live enemies, refilled one a second.

- Live cap (hub): 2 + 2 per connected member, at most 16 (solo 4, half the instance's 8). It refills one at a time, every
  3 s (the instance refills every 1 s).
- Density curve (relative spawn weight per zone, by ring):

| Ring | Distance from a gate | Weight | Kinds | Money (x the add's reward) |
| --- | --- | --- | --- | --- |
| apron | under 10 m | 0 (no spawns) | | |
| near | 10-29 m | 0.35 | shambler, runner | 0.4 |
| mid | 18-45 m | 0.7 | shambler, runner, spitter | 0.6 |
| far (wilds) | 47-82 m | 1.0 | shambler, runner, spitter; brute rare (not before 6 min of the loop) | 0.8 |

- No Hulks in the hub (the town's Hulk breaches and drops stay for later events or the final encounter).
- Spawn choice: pick a zone by weight among zones with a spawn whose exit is 20 m or more from every living pilot, is
  not in any pilot's line of sight within 30 m, and is not occupied. Then pick that spawn and use the existing entrance
  animation by kind.
- Despawn (silent: no money, no death effect; the slot frees):
  - more than 60 m walked from every living pilot for 15 s;
  - alive 180 s without aggro;
  - lost its target to the sanctuary and not re-aggroed within 30 s;
  - found inside the sanctuary volume (should never happen).
- Money per kill: round(add reward x ring factor), through the existing NativePveRules.Money chip bonuses.

| Add (reward) | near | mid | far |
| --- | --- | --- | --- |
| shambler (25) | 10 | 15 | 20 |
| runner (20) | 8 | 12 | 16 |
| spitter (30) | | 18 | 24 |
| brute (80) | | | 64 |

  - Headshot +5 (the instance pays +10).
  - Assist: 40% of the hub reward (the existing ratio).
  - Kills pay only when the credited damage was dealt from outside the sanctuary (see anti-camping).
  - Optional soft cap: hub income above $600 per member per loop pays half, so the instances stay the main income.

## Anti-camping: recommendation

Recommended: holster inside, plus the ward and aggro loss (the owner's suggested combination). A fourth rule, no money
for kills from inside, backs it up.

1. **Weapons and abilities holstered in the sanctuary.**
   - While a member's feet are inside the sanctuary volume, attack, weapon abilities and thrown or placed attacks do not
     fire.
   - Movement, interaction (machines, caches, gates, reprints), menus and non-damaging self abilities stay.
   - Holstering starts 0.25 s after entering (so crossing a threshold mid-swing is fair) and ends at once on leaving.
   - Recruits inside are holstered too.
2. **The ward at the fence line.**
   - It blocks projectiles and area effects that cross the fence or a gate's opening, in both directions: spitter acid
     cannot land inside, and a shot or ability from inside cannot leave.
   - Zombies cannot cross it even when a gate stands open: a horde-only blocker in every gate's closure, always on, and
     horde navigation links through a closure disabled.
3. **Zombies lose aggro on members inside.**
   - 1.5 s after its target enters, a zombie drops it and walks back toward its zone, at least 15 m from the fence.
   - A zombie never targets a member inside, so none gather at the fence for step-out farming.
4. **Belt and braces:** a kill pays nothing if its credited damage came from inside (damage over time and recruits
   included).

Why this rather than the alternatives:
- It is legible. The HUD tag says SANCTUARY · WEAPONS HOLSTERED, the ward shimmers at the fence, and zombies turn away.
  A player learns the rule the first time they try to shoot from inside.
- It removes the farming loop at the root: nothing can be killed from inside, and nothing waits at the fence to be
  killed from the doorway.
- It keeps the sanctuary a calm place to shop, reprint and regroup, with no timer and no penalty for waiting for a
  teammate.

Alternatives considered:
- **No money from inside, alone.** Players could still clear threats from total safety and pull zombies to the fence,
  and the rule is invisible until you notice your wallet.
- **Decaying "sanctuary fatigue".** It punishes legitimate waiting (shopping, reviving, a teammate loading), needs a UI
  to explain it and a balance pass, and still allows a short farm before it bites.

## Gates

- **Opening and closing.**
  - The interact key within a gate's `interact` volume, from either side, opens a shut gate and closes an open one.
  - Zombies never open gates.
  - An open gate closes by itself 2.5 s after its `sensor` holds no living member.
  - A gate never closes onto a member standing in its closure; it re-opens or holds.
- **Motion as drawn.**
  - Opening takes 0.9 s. The latch lifts in the first 0.18 s, then the leaves travel.
  - Wood swings inward to 160 degrees with a little overshoot.
  - Metal rolls apart into pockets behind the fence sections, with a judder and an end-stop bounce.
  - Closing takes 1.0 s. The leaves accelerate shut, slam at 0.85 s, bounce, and the latch drops.
  - A reversal mid-swing eases from where the leaves are.
- **Collider (proposed).**
  - The closure box is solid while the gate is shut. It releases 0.5 s into an opening (about 60% open) and blocks
    again 0.65 s into a closing, if the closure is clear.
  - While shut, add a pilot exclusion column over the closure (from 3.1 m), so nobody jumps a shut gate.
  - The horde-only ward blocker in each closure is always on.
- **Without a state** (today), every gate is drawn resting **open**. The openings carry no collider until the director's
  gate authority exists, and a shut leaf that pilots and zombies walk through would fake a barrier. This differs from the
  brief's "draw closed"; one constant flips it (`NativeExpeditionHubGates.RestOpen`).
- **Sound** (existing keys):
  - wood: latch `reload-end`, creak `wood` (slowed), slam `wood` with a `stone` thud;
  - metal: bolt `reload-start`, rumble `stone`, slam `hit`, with the same thud;
  - closing: the latch drops on `reload-end`.
- **Dust:** drag puffs along the leaves' foot while they travel, a burst where they stop, and sparks off a metal gate's
  track.

## Presentation (what is drawn)

- **Fence** (kit, static).
  - Wood: palisade logs with stepped points, battens on the inside, a SAFE board, claw marks and a smear outside.
  - Metal: lapped scrap sheets in mismatched colours, I-beam posts, a top rail, razor wire, KEEP OUT.
  - Both: thin sandbag courses along the inner foot, kept clear of the leaves' travel.
  - At each gate:
    - a heavy post either side, inside the section's footprint, with a lamp arm into the plaza (8 lamps, the bulb
      string between them);
    - a header beam at 4.0-4.35 m: SANCTUARY on the street side, and on the plaza side where the gate leads (WARD
      ZERO 75 M, FINAL GATE, IRONWORKS SEALED, STATION SEALED).
- **Gates** (`NativeExpeditionHubGates`, drawn each frame).
  - Wood: two leaves of five logs on battens with a Z-brace, iron straps and a flip latch.
  - Metal: two welded frames of rust, galvanised and painted sheet, a brace, a handle, wheels, a cane bolt and the top
    track.
  - Each gate's dust and sparks.
- **Ward shimmer.** A pale gold curtain on the fence's outer face, a brighter curtain and floor line across every
  opening, and rising motes. Drawn only when the view says the ward is on.
- **HUD.** SANCTUARY · WEAPONS HOLSTERED under the SAFE chip, only when the view says the holster rule is live and I am
  safe.
- **Wilds atmosphere.**
  - Ground fog: every wilds cell carries a sheet, about 1.4x larger and darker, with a second layer higher up (the city
    leaves a quarter of its cells bare).
  - No steady street lamps (88% dead, the rest flickering), no lit or television windows, and dark dirt and ash over the
    asphalt.
  - The few lights mark the way: the Meridian forecourt's cold floodlights, sodium floods in the yard and station,
    fires and the pylons' red lights.
- **Distance markers.**
  - Painted chevrons every 15 m along the Ward Zero route, and the walked metres left every 30 m (WARD ZERO 55M).
  - Two wall signs per route where it leaves the city (WARD ZERO 45 M >; IRONWORKS SEALED; STATION SEALED).
  - SANCTUARY arrows back the other way in the wilds.
- **DEV preview** (DEV panel · Camera · "Hub gates preview").
  - On the town map, every gate opens and closes on its own 8 s cycle, staggered.
  - The ward shimmers, and the holster tag shows while I stand inside.
  - Its sounds play, labelled "DEV PREVIEW · ... · NOT GAMEPLAY".
  - It changes nothing in the simulation.

### Live today vs waiting on Astra

| Live on this branch (no director change) | Waiting on Astra |
| --- | --- |
| the bigger town, its wilds, cover and 84 spawn records | the hub spawn director (zones, cap, despawn), money |
| fence collision and exclusion columns, gate openings passable | gate interaction, state, colliders, auto-close |
| Ward Zero door at x 84 (M1 enters and returns there) | the ward (projectiles, horde blocker, nav links) |
| gate models resting open, fence dressing, headers, lamps | the holster rule, aggro loss, no-money-from-inside |
| wilds fog, darkness, markers, signs | the holster tag and ward shimmer in play (they bind to the rule) |
| DEV preview of gates, ward, holster tag and sounds | a replicated GateState in the view |

M1 runs the town as an intermission and spawns no enemies there, so in M1 today the wilds are empty and the run to the
door is safe. That is true until the hub spawn director exists.

## Collision, movement and navigation changes (all in `expedition-city`)

- **Removed:** the eight 1.1 m sanctuary barricades.
- **Added:**
  - eight 3.2 m fence sections, with their exclusion columns;
  - 14 building masses and 4 rooftop pieces closing the wilds;
  - 58 wilds cover blocks (39 dressed `wild-*` pieces, plus 19 vehicles, barriers and crates);
  - three breach panels.

  That makes 87 blocks added and 8 removed, for 203.
- **Resized:** five ground slabs, to the new extents.
- **Moved:**
  - the Meridian facility, tower, portico and ambulance (+40 m in x);
  - the foundry hall, stack and shutter (-40 m in x);
  - the transit hall, boards, canopy, pylons and roof vents (-40 m in z);
  - the cars and truck in the avenue and the east and west streets, to clear 3-6 m aprons outside the gates; the bus's
    fire light and the ambience crackle moved with it to (22.6, -3.9).
- **Spawns:**
  - moved: `door-east-n`, `vent-avenue`, `drop-west` and `drop-south` (gate aprons), `door-south-e` (its doorway on the
    building), and `drop-hulk-station` (to the new station);
  - added: 34 in the wilds.
- **Navigation:** regenerated by `tools/generate_expedition.py` (deterministic `--check`).
- **Unchanged:**
  - the Ward Zero instance (its draft and blockHash);
  - Siren;
  - every other map;
  - hit timing, hitboxes, damage, cooldowns, movement rules, networking and saves.
- **Layout revision:** exp-layout-2 becomes exp-layout-3. The compatibility fingerprint changes with the manifest and
  UnityMaps, so the room/layout identity changes as it does for any map change. A durable Expedition save that pins the
  old fingerprint would not resume on the new town: the director's own refusal.

## Checks

Generator (`expedition_city.py`), its PNG plan and `test_expedition_layout_draft.py`:
- fence closed except at gates;
- every crossing of the sanctuary edge inside a gate opening;
- each route through its gate only, and at least 70 m long;
- zones outside the fence and 8 m or more from the gates;
- spawn exits 10 m or more from the gates;
- every non-final spawn in exactly one zone;
- rings ordered by distance;
- all the earlier floor, clearance and route proofs.

Managed:
- `CoreTests --expedition-hub` (`Tests/ExpeditionHubChecks.cs`) repeats the fence, route, zone and apron checks on the
  imported map with its own capsule grid;
- the gate models stay inside their closures when shut, and through their whole travel (to 108%) never meet a block, a
  slot, an interaction or an anchor;
- the motion's ends and reversal, the rest pose, the DEV preview, the cue schedule, the dressing, no per-frame
  allocation, and the hooks;
- `--expedition-layout` (Astra's strict parser and ValidateGeometry with the imported navigation),
  `--expedition-dressing`, `--expedition-acceptance` and `--expedition-states`.

Also: an offline emulation of the M1 runtime startup gate (scratch probe, PhysX-like) found 0 failures on both areas.

## Astra contract (proposal)

1. **Layout reads (additive; the strict loader ignores them).** `areas[exp-city].hub`:
   - `gates[]` (id, kind, motion, side, x/y/z, yaw, width, height, `closure`, `interact`, `sensor`, `inside`, `outside`,
     `fence`);
   - `fence.segments[]` (block, kind, side, gate) and `fence.height` / `thickness`;
   - `zones[]` (id, ring, volume, spawns as `e` indices, gateDistance);
   - `wilds[]`;
   - `routes[]` and `reserved[]` (gate, walked path, run and sprint seconds);
   - `apron` (spawn 10 m, zone 8 m).

   The town's `spawns` keep the PveEntry shape plus `allow`. The final gate stays the only `gates[]` record.
2. **Replicated gate state.** One per hub gate in the public view: `{id, open: bool, changedAt: run seconds}` (the
   closures' clock). Changes only on transitions.
   - Bind: `NativeExpeditionHubLive.Authority` with `AuthorityClock`; the presentation and sound do the rest.
   - A float `open 0..1` is not needed: motion is eased from `changedAt`.
3. **Sanctuary rules in the view:** `holster: bool`, `ward: bool` (on for the run) on the same object.
   `members[].safe` already exists and drives the holster tag.
4. **Gate authority.**
   - Interact opens or closes.
   - Auto-close 2.5 s after the sensor is empty; never closes onto a member.
   - Collider: the closure box, solid while shut, released 0.5 s into an opening, blocking 0.65 s into a closing; a
     pilot exclusion column over a shut closure.
   - The horde-only ward blocker in every closure, always on; horde nav links through closures disabled.
   - Zombies never open gates.
   - The startup geometry gate keeps running with gates open, as today, because spawns must still route to slot 0.
5. **Sanctuary rules.**
   - Holster inside (0.25 s in, instant out).
   - The ward blocks projectiles and area effects across the fence line both ways.
   - Aggro dropped 1.5 s after a target enters; walk back 15 m or more.
   - No kill money for damage credited from inside.
   - A horde actor found inside despawns.
6. **Hub spawn director.** The cap, cadence, ring weights, kinds, line-of-sight and distance rules, despawn rules and
   money in "Hub danger". It uses the zones and the town spawn records. It never uses the nine final-plaza spawns before
   FinalUnlocked, and no Hulks.

## Open questions for the owner

- Are the scale and run right? About 11 s running to Ward Zero, 192 x 156 m.
- Should gates rest open or shut before the gate authority exists? Open is what the code does now (see above).
- Should hub money be soft-capped per loop, and should the hub ever spawn Hulks (for example a late-loop event)?
- Are the district names (Meridian Approach, Ironworks Yard, Station Road) and the palisade on the north and west mouths
  right?


## Authority implementation — 2026-10-15

Owner-approved rules are implemented on `codex/expedition-hub-authority`, based on
Claude's hub `3c13863` (includes shared `f29d7c9`). This section supersedes the
"waiting on Astra" status above for that branch; native release remains gated.

- Four gates start shut, accept F from either side, reopen/hold when a player capsule
  overlaps, and auto-close after 2.5 seconds with a clear sensor. Collision releases
  .5 seconds into opening and returns .65 seconds into closing. Closed gates also
  have pilot anti-vault columns. Occupancy includes downed bodies and overhead pilots.
- Horde-only layer 28 barriers remain solid at every gate regardless of its state.
  Horde navigation rejects crossing links and direct shortcuts; vent travel is
  disabled in the town. Intruding horde actors are removed without rewards.
- Weapons/ability input is cleared and pending attacks cancelled .25 seconds after
  entry; movement and interactions remain. Leaving clears the timer immediately.
  The ward is immediate and rejects cross-boundary projectiles, acid arcs, melee
  projectiles, blasts, fields, and damage application, including outside-to-outside
  paths through sanctuary. Remote placed standard abilities cannot be placed across it.
- Existing targets retain at most 1.5 seconds of aggro inside; no new inside targets
  are selected. Untargeted hub mobs return toward their entrance instead of piling
  against the fence. Inside-origin damage is rejected before payout. Accepted
  outside-origin DoTs can still pay after the shooter retreats; recruits use the
  same damage boundary. No optional $600 soft cap was added.
- Population is `min(16, 2 + 2 * living connected unextracted members)` (zero if none).
  One spawn attempt every 3 seconds, with no catch-up burst; roster/weights follow
  the authored rings and never include Hulks. Existing entrance clearance and
  queue checks apply, with 20 m pilot exclusion and 30 m visible-spawn exclusion.
  Far brutes are rare after six run minutes. Final-plaza spawns are not used.
- The spawn ring fixes its lifetime base reward multiplier (.4/.6/.8), rounded
  before existing chip bonuses. Headshot bonus $5; existing recruit/assist ratios.
  Excess population after deaths/disconnects is culled silently. Far timeout is
  15 seconds beyond **60 m Euclidean distance** from everyone (a conservative
  lower bound on walked distance); idle timeout 180 seconds, lost-target timeout
  30 seconds. Final boss arrival clears hub mobs and restores the boss add cap.
- `ExpeditionView.hub` carries detached serializable `ExpeditionHubView` (Gates,
  Count, Holster, Ward). Existing public/private transport carries it at 20 Hz,
  including source-only changes at the same run revision. ConversionGame binds it
  and `elapsed` to `NativeExpeditionHubLive`. No private rewards enter this field.

Verification: engine-free gate/ward/cap/reward/wire checks plus the existing hub
layout/visual checks; native Unity acceptance now also exercises gate colliders,
occupancy, holster timing, aggro grace, spawning, damage and outside DoTs. The latter
requires the desktop builder/Unity and is not established by a semantic compile.
Full multiplayer Expedition launch remains the existing separate integration task;
this change supplies the public hub state contract, not a new multiplayer launcher.

## Town zombie roaming and perception (owner, 2026-10-15)

Authority implementation: `Core/NativeHubPerception.cs` and
`Runtime/NativeHubPerceptionRuntime.cs`, only for the hub population tracked by
`hubMobs`. Instance/Boss Test hordes and DEV monsters retain their existing AI.

- **Wander / idle:** choose authored navigation points in the zombie's spawn
  zone, normally 3–14 m away. Shamble at `min(1.1 m/s, stats.walk * .55)`, with
  existing crawl/limp, slow and stagger modifiers. Idle for a host-random 2–6 s
  on arrival; a blocked patrol leg times out after 15 s. After chasing outside
  its zone, return toward its spawn before resuming the zone patrol. Horde gate
  links stay excluded; movement still checks support and the sanctuary boundary.
- **Vision:** 18 m, horizontal 110° forward cone; within 3 m, notice in any
  direction. Both require scene line of sight. Range uses three-dimensional
  distance. Sanctuary occupancy or a segment across its fence rejects sight.
- **Hearing:** actual grounded displacement generates steps, at most every .6 s
  walking / .3 s sprinting; a grounded transition generates landing noise.
  Accepted attacks/casts/shots/explosions and successful manual gate openings
  generate their own noises. Merely pressing a blocked action does not count.
  Noise can pass ordinary scenery, but never the sanctuary fence; source and
  listener paths are checked, including two exterior points across the sanctuary.

| Noise | Radius |
| --- | ---: |
| Walking | 5 m |
| Sprinting | 10 m |
| Landing | 12 m |
| Fighting / melee attack | 14 m |
| Firing (including bow) | 28 m |
| Explosion | 36 m |
| Casting | 22 m |
| Opening a gate from outside | 16 m |

- **Alert / chase:** a sight or noise sample starts a .4–.8 s stationary turn
  toward that sample, then chase. Repeated observations do not restart the
  reaction. Existing attacks cannot run during the reaction. Optional screams
  and zombie-to-zombie alert propagation are not enabled.
- **Memory / search:** only sight/noise updates the known position. Without a
  fresh observation for 3 s, investigate that position (up to 10 s), search in
  place for up to 4 s on arrival, then wander again. Memory pursuit and search
  cannot attack using the hidden target's live position. Reacquisition from
  search starts a new alert. Existing sanctuary aggro grace remains 1.5 s;
  entering safety does not expose further positions. Existing far (60 m / 15 s),
  idle (180 s) and lost-aggro (30 s) despawn rules remain active.
- **Cost:** senses run every .2 s per zombie, staggered initially; vision rejects
  range/cone/fence before querying the scene. The host keeps at most 64 noises,
  expires them after .75 s, and each zombie consumes each event once. Each
  zombie owns its random stream, independent of spawn/economy randomness.

### Shared reaction state / Claude handoff

`NativeMinionFrame` adds three scalar fields, copied into `NativePveAdd.Hub` on
host and guest. They also survive `NativeFrameCodec` replay/late-join capture:

| Field | Meaning |
| --- | --- |
| `hubState` | `HubZombieState`: 0 none/non-hub, 1 wander, 2 idle, 3 alert, 4 chase, 5 search |
| `hubAlertAt` | Start of latest alert in **arena simulation seconds**, -100 before first alert |
| `hubLookYaw` | Desired horizontal look direction in degrees, +Z = 0; `V3.Forward` gives the vector |

`NativePveAdd.Hub` exposes `State`, `AlertAt`, `LookYaw`; it is null for non-hub
adds. A default/older frame's state 0 clears the view. The receiver validates
state range and finite time/yaw. No client runs perception, randomness or timers.
Body position/velocity/yaw already replicate, so the real idle, shamble and turn
are visible without a presentation rewrite. Claude may layer a bespoke reaction
using this state; there is no new scream audio or animation asset in this change.
Attack timing/damage, spawn caps/cadence, ring payouts and gate rules are unchanged.

Acceptance: managed checks cover cone/LOS inputs, every noise radius, fence
segments, seeded reaction/idle/memory/search traces and public-state roundtrips.
Native hub acceptance adds real scene LOS, heard rear-target reaction,
JsonUtility and guest/late-join reconstruction. Native execution and Windows
visual/two-client play must be verified by the builder/playtest; semantic compile
alone is not that verification.
