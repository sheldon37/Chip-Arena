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
- **Motion as drawn** (retimed 2026-10-15 to the authority's collider; see "Hub fixes" below).
  - Opening settles by 0.9 s. Wood: the latch lifts in the first 0.18 s, then the leaves swing inward to 160 degrees with a
    little overshoot (past 90 degrees, clear of the opening, at 0.30 s). Metal: the cane bolt lifts in 0.10 s, then the
    leaves roll apart into pockets behind the fence sections with a judder, in them at 0.48 s, and rock on the end stops.
  - Closing settles by 1.0 s. The leaves accelerate shut from wherever they are, meet and slam at 0.67 s, bounce, and the
    latch drops.
  - A reversal mid-swing eases from where the leaves are (the latch stays up).
- **Collider (proposed).**
  - The closure box is solid while the gate is shut. It releases 0.5 s into an opening (the leaves are clear by then) and blocks
    again 0.65 s into a closing, if the closure is clear.
  - While shut, add a pilot exclusion column over the closure (from 3.1 m), so nobody jumps a shut gate.
  - The horde-only ward blocker in each closure is always on.
- **Without a state** (no hub authority: an older build or a manifest without the record), every gate is drawn resting
  **open**. With Astra's authority (merged) every gate starts shut. Without it the openings carry no collider, and a shut
  leaf that pilots and zombies walk through would fake a barrier. This differs from the
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

- Four gates start shut, accept F from either side, safely reverse a non-solid closing when a player capsule
  overlaps, and auto-close after 2.5 seconds with a clear sensor. Collision releases
  .5 seconds into opening and returns .65 seconds into closing. Closed gates also
  have pilot anti-vault columns. Occupancy uses enabled member capsules that actually overlap the doorway in 3D.
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


### Presentation of the roaming (Claude, 2026-10-15)

Branch `claude/hub-zombie-wander` (presentation only; reads the reaction state above, writes nothing). The owner's
request is met by Astra's behaviour; this layer makes each state read on the body, the same on host, guests and the
killcam.

- **One mapping.** `NativeHordeRoam.FromHub` (`Core/NativeHordeRoam.cs`) maps `NativePveAdd.Hub` one for one to the
  presentation's states and reads `hubAlertAt` and `hubLookYaw`. `NativeHordeRoamRecord.Read`
  (`Runtime/NativeHordeRoamPresentation.cs`) is the only place the presentation reads the record; the pose and the
  voices both go through it.
- **Wander.** The stride's walk part blends to the pack's slow forward walk (`walk-4`; the only take that plants cleanly
  at the host's 0.4-1.1 m/s pace for every hub kind; walk-6 and the chases drag a planted ankle 5-7 cm there). The trunk
  lolls with the stride, the head glances about every few seconds, and an occasional stumble dips the trunk.
- **Idle.** Five variants per add (sway, twitch, look around, slump, rock) on a schedule hashed from the add's seed
  against the clock: 4.5-7.5 s slots, all five in every five slots, never one twice in a row, 0.8 s crossfades. A
  crowd is out of step, and every peer draws the same variant at the same moment.
- **Alert** (from `hubAlertAt`, so a late joiner draws the same moment). The head snaps to `hubLookYaw` in 0.14 s,
  with a small overshoot and an aim correction onto it. The chest follows while the host turns the body. A scream
  starts at +0.2 s and lasts 0.95 s: head back, a tremble, the arms flung on a reel's start, and hinged jaws open.
  The burst throws the trunk forward from the chase's first frame.
- **Chase.** Today's chase look. The head and alert stance follow the nearest pilot within 14 m, but only when that
  pilot lies within 75° of `hubLookYaw`. Otherwise they follow the look direction.
- **Search.** A wide head scan and sniffing (head up, quick nods, chest lifting) over the host's slow turn in place.
- **Blends.** States blend over 0.5 s from the frame each peer first sees the change, since the start is not
  replicated.
- **No record** (instance hordes, DEV monsters, the town's final encounter, an add still entering). The inference
  from motion only tells standing apart from hunting. Such an add shows the idle variants after standing 1.5 s with
  no pilot within 14 m and no attack for 3 s. Slowed by a limp or a frost field, it still reads as a chase.
  Otherwise it looks as before.
- **Head look-at fix (every horde add).** The gait's look-at was mirrored: heads turned away from the pilot and looked
  down at one above. It now turns toward the pilot and looks up at one above.
- **Voices** (the roaming population only; bank-only, silent without LocalAssets).
  - Sparse, low moans while wandering, idling or searching: within 24 m, 7-13 s slots per add with three slots in
    ten silent, at most one a 0.9 s across the horde, attenuated with distance.
  - A shriek with the scream.
  - New bank keys `pve.zombie.moan` / `pve.zombie.shriek`. Until the owner supplies clips, the stand-ins are
    `pve.add.death` pitched down and `siren.rush`.
- **Unchanged.** Hit timing, hitboxes (Balance and KeepHands still hold the trunk and claws to the capsule), damage,
  cooldowns, movement and the network frame. The scream is cosmetic: it alerts no other zombie.

**Gaps for Astra (optional; visuals only, nothing blocks):**

- `hubStateAt` (the current state's start, arena seconds). Blends would then start on the host's frame on every peer.
  A late joiner within 0.5 s of a change would match the host's blend instead of starting its own.
- `hubTarget` (the hunted seat, 0 none). The alert stance and the chase's head would then track the actual hunted pilot
  instead of the nearest within 14 m near the look direction.

Checks: `Tests/HordeRoamChecks.cs` (`CoreTests --horde-roam`). Offline renders are in the scratchpad (DEV_LOG).

## Hub fixes — 2026-10-15 (Claude)

Branch `claude/expedition-hub-fixes` (base shared `425cb2d`, merged with shared `ee46074`). Owner report (verbatim): "Also
on the hub~ it seems like there is Z-fighting on the gates? It also allows us to run through it before it is even open.
Lets also remove the circle outlines like the one on the exit door, the caches, etc."

### Z-fighting on the gates (presentation; fixed)

No collider-only block is drawn in the town: the gate closures, the no-vault columns and the horde ward are Astra's
runtime colliders without renderers, and the fence's exclusion columns are `pilotBlocks`, never drawn. The fights were
drawn faces sharing a plane (found with `NativeSurfaceLayers.Solve` at zero lift; now held to none by
`CoreTests --expedition-hub`):

| What | Shared planes before | Fix |
| --- | --- | --- |
| The sanctuary edge's lit curtain (`NativeExpeditionStateScene.Sanctuary`) stood on the volume's edge: the plaza's building faces, the fence's inner face and the shut wooden leaves' inner faces all lie in that plane | 232 of its 332 quads, all four gates' logs among them | the curtain stands `CurtainInset` 8 cm inside the edge (5 cm still met shopfront trim, 5.7 cm the metal leaves' stiles; 8 cm meets nothing) |
| The metal leaves' rails ran over their stiles with flush front and back faces | 24 per metal gate, at every pose | the rails run between the stiles, 7 mm inside their faces |
| The wooden latch's pivot plate sat 2 mm behind its bar | 1 per wooden gate while the latch is up | 7 mm |
| The ward's fence shimmer stood 1 mm off the outer dressing (claw marks, smears, the metal top rail) | 23 quads (back faces) | `WardOffset` 3.5 cm, one helper for the runtime and the checks (`NativeExpeditionHubGates.WardSegments`) |

### The leaves in step with the collider (presentation; retimed)

The authority's collider (`ExpeditionHubAuthority.Blocking`) is released 0.5 s into an opening and solid again 0.65 s
into a closing. Before, a metal gate was 35% open at the release (its leaves still covered 1.3 m of each side of the
opening until 0.74 s), and every closing was still 55% open when the collider turned solid (the slam was at 0.85 s).

| Moment (s into the state) | Collider | Wood (drawn) | Metal (drawn) |
| --- | --- | --- | --- |
| opening 0 | solid | latch lifts to 0.18 | bolt lifts to 0.10 |
| | | past 90 degrees (clear) at 0.30, fully open at 0.49 | in the pockets at 0.48 (`ClearAt`), bang, rock on the stops |
| opening 0.5 | released | | |
| closing 0 | released | leaves accelerate shut from where they are | same |
| closing 0.65 | solid | | |
| closing 0.67 (`MeetAt`) | | meet, slam, bounce; the latch drops by 0.83 | same |

The cues follow (rumble when the bolt lifts, the metal stop at 0.48, the slam at 0.67). The presentation also carries
the authority clock between the 20 Hz view updates (`ExpeditionHubClock`, at most 0.1 s ahead), so the leaves move
every frame instead of stepping with the projection. Checked against the real authority every 1/240 s: never drawn shut
while walkable, clear whenever released, met whenever solid, still visibly closing 0.15 s before it blocks.

### Run-through report (for Astra; authority, not changed here)

What fails: walking into a shut gate, without pressing F, opens it and drops its collider in the same tick. The pilot
crosses the fence line 0.117 s later at run speed while the gate is (correctly) drawn shut with its latch lifting.

- `NativeArena.HubOccupied` (`Runtime/NativeExpeditionHubAuthority.cs:51-58`, the test at line 56) counts a closure as
  occupied when a capsule's centre is within its radius + 0.15 m of the closure box in plan, at any height. A pilot pressed
  against the solid closure stands its radius + PhysX's 0.01 m contact offset from it, so touching a shut gate, or coming
  within 0.15 m of it, "occupies" it. So does a pilot leaning on the fence within 0.57 m of the opening's edge.
- `ExpeditionHubAuthority.Step` (`Core/NativeExpeditionHubRules.cs:28`) then re-opens any shut gate whose closure is
  occupied, however long it has been shut and solid.
- `StepHubGates` (`Runtime/NativeExpeditionHubAuthority.cs:62`, `solid=Blocking(i,now)&&!HubOccupied(...)`) turns the
  collider and the no-vault column off at once while occupied, instead of 0.5 s into the opening.

Not ours: the presentation reads the right state on the right clock and ids (the view's `hub` and `elapsed`, bound at
`Runtime/NativeExpeditionNetwork.cs:14`), the gates start shut, and the closures span their openings exactly (the fence
flood check passes with every closure solid).

Reproduction: `CoreTests --expedition-hub` prints `PENDING (Astra) Expedition hub: a shut north gate opens itself ...`
(`ExpeditionHubChecks.PendingAuthority`: Core's authority and the runtime's occupancy rule replayed engine-free; a pilot
at 6.8 m/s, no interact). It turns into a PASS line once a shut gate stops the pilot. In the game: walk into any shut hub
gate.

Suggested fix:
1. For "never close onto a member", test a real overlap of the capsule with the closure (radius less a skin, or an
   overlap query against the closure box while its collider is off), not radius + 0.15 m.
2. Re-open on occupancy only while a closing has not blocked yet (`!Open && now-ChangedAt < Block`); a gate that is
   already solid stays shut. (`Tests/ExpeditionHubAuthorityChecks.cs:21` asserts a re-open 0.7 s into a closing, after
   it blocks; it would move under 0.65 s.)
3. In `StepHubGates`, let occupancy hold the collider off only in that same window.

Reversals (same source, `Blocking` at `Core/NativeExpeditionHubRules.cs:35` is a pure function of the newest state's
age): pressing F again during an opening's first 0.5 s releases the collider at once while the leaves are still nearly
shut (walkable while drawn shut, up to 0.65 s); pressing it during a closing's first 0.65 s makes the collider solid for
0.5 s while the leaves are nearly open (an invisible wall). Suggest keeping the collider's state across a reversal: a
close during an opening that has not released stays solid; a re-open during a closing that has not blocked stays
released. The presentation reverses from where the leaves are and clears or meets at the same 0.5 and 0.65 s marks, so
it follows either rule.


### Run-through authority fix (Astra, 2026-10-15)

Implemented on `codex/hub-gate-collision`, based on shared `72aa1858`. The
report above records the pre-fix behavior; the PENDING reproduction was observed
before this change and is now a hard assertion in `CoreTests --expedition-hub`.
It checks all four gates, both the open flag and the collider, and the pilot
remaining outside the closure.

- Occupancy uses actual upright capsule-versus-yaw-box distance in three
  dimensions, with a 1 cm inward skin. Capsule center and world-scaled height /
  radius come from the real collider. Tangency, the physics contact offset,
  expanded rectangular corners and a pilot above the opening are not occupancy.
- Gate authority retains collider solidity. Occupancy can reverse a closing
  only while that collider is still released; a shut/solid gate cannot auto-open
  even if penetration is reported. On the threshold tick, overlap is checked
  before enabling the closing collider, so it cannot close onto a member.
- `StepHubGates` applies the authority collider state directly to both the door
  and no-vault column. There is no independent occupancy override.
- Reversing opening-to-closing before release preserves solidity. Reversing
  closing-to-opening before blocking preserves the released state. Only the
  existing .5 s opening-release / .65 s closing-block markers change solidity.
  Explicit F interaction from either side and 2.5 s clear auto-close remain.

The gate pose/wire fields, animation, sanctuary ward, movement speed, combat,
rewards and zombie perception are unchanged. Claude's separate presentation
changes remain on their own branch. Native acceptance now checks real capsule
contact/penetration/height and both reversal collider states; these require the
Windows builder/PhysX run, not just semantic compilation.

### No floor rings round the Expedition's interactables (presentation; removed)

Removed:
- `Core/NativeExpeditionWorld.cs` `Emit`: the floor ring (radius r-0.05..r) and the actionable spinning outer ring
  (r+0.04..r+0.12) round every world piece: the Ward Zero door and the instance exit (doors, 1.35 m), the reprint machine
  (0.95 m), the chip caches (0.75 m) and reward drops (0.42 m). The light column while actionable, the glyph (diamond,
  tile, pedestal, crate), the label, the HUD door hologram and the interact prompt stay.
- `Runtime/NativeExpeditionScreen.cs`: the same ring in the pieces' greybox preview.
- `Runtime/NativeBeaconPresentation.cs`: the revive beacon's floor ring (with its self-revive arc) on Expedition maps
  (`NativeExpeditionWorld.ExpeditionMap`, ids starting `expedition-`). Wave defense and the Boss Test keep it: there it
  is the self-revive timer, not a generic interact ring. The column and the F · REVIVE / SELF-REVIVE label with its clock
  stay everywhere.

Kept (events, not outlines round an interactable): the final gate's opening cue rings, the sanctuary crossing ripple,
the boss arrival's roar rings, the revive repair's floor ring, combat telegraphs, and the sanctuary edge's square floor
glow. The gates never had rings.

### Impact

Presentation only: no collider, layout, navigation, timing rule, hit timing, hitbox, damage, cooldown, movement or
network change. The layout and its generator are unchanged (the closures span their openings).


## Join circle at the instance doors (owner request 2026-10-15) — contract for Astra

Owner (verbatim): "lets move the 'Join instance' circle a little bit away from the door. We can make the circle bigger too, players
can join by standing inside the circle." Also asked: "can this work if everyone goes into their own instance? Say one player wants to
run around the hub, the other wants to go inside ward zero. Could this still run smooth for both of them?"

**Today** (research at `8355b44`): there is no join circle. The door's floor ring was removed with the other interactable rings
(`03eb9f4`); a cyan light column at the door anchor remains. Entry is the host's F inside `doors[].interact` (3 × 3 × 2 m box,
1 m out from the doorway) with line of sight to `approach` (`Core/NativeExpeditionDirector.cs:39-43`,
`Core/NativeExpeditionDirectorState.cs:110-115`, `Runtime/NativeExpeditionM1.cs:77-82`). Then every living member is placed on the
instance `entries`. The run has one `area`, and the host simulates one arena, so a split party (hub and Ward Zero at once) is not
supported. It contradicts `EXPEDITION.md` ("No split indoor/outdoor simulation") and would need per-member areas, concurrent arenas,
per-peer frames and per-area barriers: a large redesign. The owner is asked to decide; the proposal below keeps one area.

**Proposed rule (Astra; pending the owner's confirmation):**
1. Each instance door gets a **join circle**: a vertical cylinder `join {x, y, z, r, h}`. For Ward Zero: centre 5 m out from the
   doorway on the approach line (x 79, z 0), r 2.5 m, h 3 m, between the portico pylons (z ±3.8).
2. When every living, connected, unextracted member stands inside it (capsule centre in the cylinder), a 3 s **deploy countdown**
   starts. It cancels if anyone steps out, dies or disconnects. At zero the run enters (`EnterDoor` path, barrier, ready, placement
   unchanged). The F door entry is retired; F stays for caches, machines, rewards and gates. Guests count through their pilot on
   the host.
3. Public view per door: `joinInside` (count), `joinNeeded` (count), `joinStartedAt` (authority seconds, −1 none), and per member
   `atJoin` (bool) for "who is missing".
4. The phase is Town only; quota, closures and the exit vote are unchanged.

**Claude's part:**
- Layout: `docs/arenas/scripts/expedition_city.py` emits `join` and moves `returns` out of the circle. The approach goes to the
  circle centre. The revision bumps to exp-layout-4 (saves pinned to exp-layout-3 will not resume). This needs Astra's parser to
  accept `join` first.
- Presentation: a ground ring at the circle (an owner-requested exception to the no-rings rule; `ExpeditionPresentationChecks` is
  updated), split into `joinNeeded` segments that fill as members step in. A countdown sweep and a label: STAND IN THE CIRCLE TO
  DEPLOY · 2/3, or DEPLOYING 3…2…1. Missing members are named on the HUD.

## Expedition co-op hosting (owner request 2026-10-15) — for Astra

Owner: "lets make hosting a lobby for this one better (So I can test with other people) Add Expedition as a option for hosting
rooms."

**Today:** the Expeditions menu's HOST ROOM makes a real internet (tunnel/directory) or LAN room, but it plays Wave Defense
only. START EXPEDITION is solo, local and DEV-only (`Runtime/NativeExpeditionM1.cs:14,38`). Guests cannot play an Expedition:
- M1 seats only the host;
- placement assumes one pilot (`:65`);
- guests never report their area loaded;
- door and interact intents are host-local;
- only the host's death is recorded;
- new peers are refused once an arena exists (`Runtime/NativeNetwork.cs:92`).

The full gap list is in DEV_LOG (Expedition integration contract) and `EXPEDITIONS-MENU.md`.

**Astra (blocking):**
1. Room fields `expedition` / `mode`.
2. A room-start path that seats every lobby peer and `Run.Join`s them, without the Online/DEV gate, with N-party placement on
   the 8 slots.
3. Guest area load and ready on the existing ticket and stage barrier.
4. Guest intents for doors (or the join circle), caches and rewards; guest death and reprint.
5. Party cap and late-join / host-leave policy.

**Claude, after item 2:**
- "Expedition" in the Create Room and lobby Mode select;
- START EXPEDITION enabled for the host, with guests ready-gated;
- guests shown the host's pick;
- the party cap and status lines.

### Safe-zone border hidden in the hub (Claude, 2026-10-15)

Owner: make the safe zone border invisible in the hub, and keep the zone in case it is needed later. The sanctuary volumes and every
rule on them stay. Only the drawn edge (floor glow, curtain, motes, crossing ripple) is off in the fenced hub
(`NativeExpeditionStateScene.HideSanctuaryEdge`, set from `NativeExpeditionHubLive.Layout`). The fence, the gates, the ward and the
SAFE tag and chip still show where safety is.
