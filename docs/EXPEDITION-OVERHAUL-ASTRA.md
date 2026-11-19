# Expedition overhaul: rules, generation, collision and network (hand-off to Astra, 2026-11-18)

From Claude, passed on by the owner, who divides the work: Claude builds the presentation, and you build the packages below. This hand-off stands on its own. It ends with an END marker; if the marker is missing, ask the owner to forward the saved plan file again. Claude will commit the full plan as `docs/EXPEDITION-OVERHAUL.md` with the first package.

Paths: `Core/`, `Runtime/`, `Editor/` and `Resources/` are under `unity/ChipArenaConversion/Assets/ChipArena/`; `Tests/` and `tools/` are under `unity/ChipArenaConversion/`. Line numbers are at shared `442bcf69`.

## What the owner asked (R1-R12)
- **R1:** a tileset machine. The tileset decides enemy types and instances. Only City exists now, but the machine must be ready.
- **R2 and R5:** move City off the box style, with uniquely modelled buildings, props and gates.
- **R3 and R12:** streets shaped by their instance, buildings and nearby enemies included. Near Level 0, for example, Backrooms-style buildings and some Backrooms enemies.
- **R4:** a darker map lit by street lamps.
- **R6:** a detailed hub, with survivors and interactions.
- **R7:** pods that crash into the hub, spread apart. The owner named this "First".
- **R8:** an animated drop-in.
- **R9:** metal-chest caches, with a loot effect, better cost and rarity text, and new spots every run.
- **R10:** a chip machine and a healing machine in the hub.
- **R11:** a much bigger expedition area on the scale of Risk of Rain levels, with elevation, taller buildings and zombies on the roofs.

## Ground rules
**Base and branches:**
- Fetch first. The base is the shared Unity head, `origin/claude/sleepy-ritchie-g8ruhv`, unless the owner names another.
- One task branch per package, with a PR into shared.

**Checks:**
- Before merging: the full CoreTests, `python tools/check_unity_compile.py` and `git diff --check`.
- Generation changes also run the builder gates: 8 layouts, Hulk routes and the 1,800-tick fixture.

**Files:**
- **Yours:** the generator and rules files. Claude does not edit:
  - `Core/NativeExpeditionGeneratedWorld.cs`;
  - `NativeExpeditionDeployment.cs`;
  - `ExpeditionDeploymentRoom.cs`;
  - `ExpeditionRunBundle.cs`;
  - `NativeExpeditionHubRules.cs`;
  - `NativeMatch.cs`;
  - `NativePodRun.cs`;
  - `Runtime/NativeArena.cs`'s step and probe;
  - the integration, director and rewards files;
  - `NativeNetworkFrame.cs` and `NativeNetwork.cs`;
  - `NativeExpeditionM1Arena.cs`.
- **Claude's:** the presentation files, including:
  - `NativePodDrop.cs` (except LaunchGone and Keys/Normalize);
  - `NativeDeployPresentation.cs`;
  - the city kit;
  - lighting;
  - `NativeZombieLooks.cs`;
  - the machine models.
- **Shared contract files,** and who owns their fields:
  - `NativeExpeditionHubPlan`: you create it in A0 with the landing field and ExitReach 0; Claude proposes the camp rows; you own it all after A1.
  - `NativeCityBleed`: Claude writes it in M2 for presentation; please review its math when it lands; you own it all, thresholds included, from A5a, when it first gates spawns.
  - `NativeCityLayout`: Claude's adapter shapes it in M3; you fill it in A4; Claude draws it.
  - `NativeTilesets`: Claude creates it in M1 with City's display fields; you add the rules fields in A3.

**Coordination:**
- deploymentProtocol and `Runtime/NativeNetwork.cs` with `codex/steam-player-hosted`.
- `Runtime/NativeExpeditionM1.cs` with `codex/thunderstore-content`.
- Log in `docs/DEV_LOG.md` before editing shared records.

**Reach, for everything below.** Six air jumps with Skybound reach about 10.8 m, about 21.6 m with MOON BOOTS, and jump-power and gravity chips go further (`Core/NativeJumpSimulation.cs:7-9`, `Core/NativeMovementContent.cs:12-14`, `Core/NativeExpeditionMutators.cs:27`, `Core/ConversionContent.cs:110`). So the plan assumes a stacked pilot can reach any roof or camp structure, and no rule or check may rely on "out of reach" by height.

**Do not change:**
- **Records and wire:**
  - the pod record's fields;
  - the minion frame;
  - HubZombieBrain view states.
- **Ids:**
  - kind ids, which are append-only;
  - the `black-siren` expedition id.
- **Behaviour:**
  - the F order (except lifting the machine gate for the hub);
  - the REVIVE-O-MATIC's spot and its E key;
  - the privacy of cache buyers and offers.
- **Geometry and timing:**
  - the door contract (closures, interact, approach, join, returns; interiors stay authored);
  - the sanctuary square and its gates' sizes and timings.
- **Saves:** there are no run saves, and the pod look keys are unchanged.

## Owner answers that gate your packages
The owner approved these defaults on 2026-11-18 ("Yes, use defaults"): the 5.0 s hub opening, option A's size, the four cache tiers, the instance-kind shares and caps, and cosmetic survivors.

| Question | Default | Gates |
| --- | --- | --- |
| Q22, clocks | A 5.0 s hub opening for generated runs (3.0 if the owner says no); PvP keeps 3 s | A0's `PodOpening` |
| Q29, camp machine prices | Match the instances; no recruit | A1's machines |
| Q26, cache tiers | STANDARD, RARE, EPIC and LEGENDARY: $100, $175, $275, $400; 60/28/10/2% | A2's tiers |
| Q1, the tileset choice | The host picks (SelectTileset). RollTileset only if the owner asks for a RANDOM lever | A3 |
| Q13, district enemies | Instance kinds only at warp (a 45% share or more, about 60 m out on the layered city) and threshold (70% or more), under the caps in A5a | A5a |
| N2, scale | Option A: 420 × 340 m with 150-190 m routes. Option B: about 600 × 480 m with 250-300 m routes. Both greyboxed for the owner's walk | A4's release |
| Q18, survivors | Cosmetic only (Claude). Trading or quests would be a later feature of yours, only if the owner asks | Nothing now |

## Order
1. **A0,** now. It ships in M1, with Claude's crash.
2. **A4's greybox,** in parallel, at both sizes. It is the long pole; start it during M1.
3. **A1 and A2** in one release (M3), with one world Version bump. A3 joins them if ready, with its protocol bump batched in.
4. **A5a** (M4), with its protocol bump. A3 lands here if it missed M3.
5. **A4's release** (M5).
6. **A5b** (M6), and its last feature-gated kinds (M7).

## A0. The landing field and groundwork (M1)
**Do:**
- **The yaw fix:** `NativePodRun.InReach` reads the record's yaw in degrees (`Core/NativePodRun.cs:34`), with PodRunChecks at yaws of 0, 90 and 225.
- **The landing field:** the area's slots (`Core/NativeExpeditionGeneratedWorld.cs:99`) become per-run landing spots:
  - seats 0-3 draw four spots from `worldSeed`, 11-15 m from the fountain at the origin;
  - each pod centre at least 4.2 m off every gate axis, so the 4 m lanes stay clear with 1 m to spare;
  - at least 12 m apart;
  - each pod's yaw (degrees) toward the origin, ±30°, seeded;
  - each spot's pod hull (r 1.17 m), its 2.5 m skid apron behind the pod (away from the door) and its capsule clear of everything in A1's hub plan table, by the clearances there, including the rows that A1 has not built yet, so the field does not move when the camp lands;
  - a draw that fails a clearance is re-drawn deterministically from the seed;
  - seats 4-7 take further draws;
  - ExitReach is 0 for now: the slot is the pod centre, and pods are not solid.
- **The other slot users**: PvP free-for-all and team spawns (`GeneratedWorld.cs:119-120`) keep today's row points as their own table, so PvP on this map is unchanged. Respawn pads, `CompleteAreaLoad`, field revive, reprint, the loop reset, `ForRecipient` (slots[0]), the final-gate rig (faces slots[0]) and the nav origin (slots[0]) move with the slots. Tell Claude if you choose otherwise.
- **The opening:** `ExpeditionRules.PodOpening`, replacing the literal `Time + 3` at `Core/NativeMatch.cs:84`. PhaseEnd = FightAt = Time + PodOpening. 5.0 s for generated hubs if Q22 approves, otherwise 3.0. Other modes keep 3 s.
- **The GO snap:** SPAWN-IN option 1 (`docs/SPAWN-IN.md:94-98`): ground the spawn centres (floor + `HalfHeight + Skin`), and run `ProbePilotGround` before the non-fight early return in `NativeArena.Step` (`Runtime/NativeArena.cs:117,124`), so `Actor.Grounded` is true through every countdown. It removes the countdown "float" in every mode.
- **A native sim benchmark** on today's map with 4 pilots and 88 zombies: nav ms, crowd-flow ms, perception and physics per tick. Record it in the budget table in `docs/EXPEDITION-DEPLOYMENT.md`, and rerun it on every blockout change.
- **A DEV load harness** (Editor and DEV builds only, never in a release): a DEV lobby-count override for the hub cap (`Core/NativeExpeditionHubRules.cs:55`), and up to three stand-in members that BoZo walks along scripted routes, so a 4-pilot, 88-zombie frame can be measured on one PC. Parties are 1-4 signed-in accounts with no bots (`Core/NativeExpeditionParty.cs:8,15`).
- **Create `NativeExpeditionHubPlan`** (shared Core) with the field's rules and `ExitReach = 0`.

**Contract:**
- Anchor, slot and pod-record yaws are in degrees; PveMachine yaw stays in radians.
- One world Version bump (your string), because the slots are in the digest.
- The pod record is unchanged.

**Checks:**
- **Re-pin:** PodRunChecks:21,23 (the opening) and the yaw cases; `Editor/NativeGeneratedWorldBuildChecks.cs:120` (phaseEnd and fightAt) and :136-141 (Hulk routes from slots[0]); ReviveMachineChecks:195,201 (clearance against the new spots).
- **Add:** a landing-field check: over 2,000 seeds every draw passes Validate and ValidateGeometry, holds the spacing and the clearances, and never stands four pods in a row.

**Claude delivers:** GeneratedFrameChecks and the frame-time captures in the same table; the presentation's own degree fix; the slot keep-out re-pin (`Tests/GeneratedDistrictChecks.cs:99`); and, in the same release, the full crash on these spots (if your grounding fix slips, Claude suppresses the GO Land take on pod runs instead).

**Done when:**
- four pods land at least 12 m apart, in a different figure every run;
- there is no Land take or `move.land` at GO, on host and guest;
- the benchmark and the harness work.

## A1. The camp pass: ExitReach, solid pods, hub blocks, camp machines, the prop pass (M3)
**ExitReach and solid pods:**
- `ExitReach = 2.4` m. slots[i] = podCentre(spot) + forward(yaw) × ExitReach, facing the origin. The spots do not move; the slots move to the ramp foot.
- **Pod colliders:** runtime colliders on host and guests, built from the pod records while a pod stands, removed at launch (LaunchGone), on expedition pod runs only.
  - Shape: an octagon of two crossed 1.96 × 2.85 × 1.96 m boxes, centred on record − forward(yaw°) × ExitReach.
  - They are not generator blocks, so empty spots never hold invisible walls.
- **Boarding:** InReach measured from the door point (record − forward × 1.1 m, which is pod centre + forward × 1.3 m), within 1.5 m, on the ramp side (forward offset from the door point ≥ -0.2 m). A pilot at the door, on the ramp or at its foot can board. Pin it in PodRunChecks at yaws of 0, 90 and 225.

**Hub blocks,** from `NativeExpeditionHubPlan` (x east, z north, y up, in metres; fence inner faces at ±24.8; blocks axis-aligned). Claude delivers the final table, checked in the DEV preview, before you start. The proposal:

| Id | Where (x, z) | Size (x × y × z) | Notes |
| --- | --- | --- | --- |
| hub-fountain | (0, 0) | 3.6 × 0.9 × 3.6 | Square basin |
| hub-canopy-n, -s | (0, ±3.4), y 2.6-2.8 | 9.2 × 0.2 × 2.4 | Ring roof, open over the fire |
| hub-canopy-e, -w | (±3.4, 0), y 2.6-2.8 | 2.4 × 0.2 × 4.4 | |
| hub-canopy-post-0..3 | (±4.4, ±4.4) | 0.3 × 2.6 × 0.3 | |
| hub-post-0..3 | (0, 3.4), (3.4, 0), (0, -3.4), (-3.4, 0) | 0.9 × 1.4 × 0.7, long axis round the ring | Benches with seated survivors; 1.2 m headroom under the canopy |
| REVIVE-O-MATIC | (-13.5, 13.5), yaw 135° | Unchanged | Keeps E |
| hub-kiosk | (16.4, 16.4) | 2.6 × 2.6 × 2.6 | Behind the chip machine |
| hub-awning-post-0, -1 | (14.6, 12.4), (12.4, 14.6) | 0.3 × 3.4 × 0.3 | Flank the chip machine |
| hub-infirmary | (-16.9, -16.9) | 3.0 × 2.6 × 3.0 | Behind the med machine |
| hub-lookout | (13.5, -13.5) | Base 3 × 9 × 3; cabin 3.6 × 2.6 × 3.6 on top (y 9-11.6) | |
| hub-searchlight | On the lookout roof, y 11.6-12.8 | 1.2 × 1.2 × 1.2 | |
| hub-sentry-n, -e, -s, -w | (-11.5, 23.8), (23.8, 11.5), (11.5, -23.8), (-23.8, -11.5) | 2.0 × 5.2 × 2.0 | Just beyond each gate's leaf pocket |
| hub-radio | (13.0, 23.9) | 4.0 × 3.3 × 1.8 | Flush with the north fence |
| hub-supply | (-12.5, -23.8) | 3.0 × 3.3 × 2.0 | Flush with the south fence |
| hub-generator | (24.3, -12.5) | 1.0 × 1.4 × 2.0 | Flush with the east fence |
| hub-sandbag-* | Two per gate, beside the lane, outside the 12 × 12 sensor; for example (±7.5, 20.5) | 2.4 × 1.0 × 0.6, long axis along the fence | |

The war board (x 15.4-19.0) and the MISSING board (x -13.0 to -16.5) are Claude's flat art on the north fence, outside every leaf pocket.

**Clearances** (Validate; edge to edge):
- every pod hull, skid apron and capsule at least 1 m clear of every hub block, the machines' solids and their use, dispense and chip-drop points, and the REVIVE-O-MATIC's solids and mat;
- every block outside the gate interact (10 × 10) and sensor (12 × 12) volumes;
- **every block outside each gate's leaf travel and pockets, with the top track:** 5-10.3 m along each gate, from the fence's inner face (±24.8) to 0.5 m inboard, 0-3.4 m high. The metal leaves roll 5.08 m into pockets 0.21-0.39 m inside the gate's plane (±25), against the fence's inner face, under a track at 3.3 m out to 10.2 m (`Core/NativeExpeditionHub.cs:178-180,300-302`);
- 4 m lanes clear from each gate inward to r 9;
- a walkway at least 2.5 m wide round the mess ring;
- **the fence vault:** within 3 m of the fence, every block is either 1.6 m tall or less, or at least 3.2 m tall, so no hub block lowers what it takes to vault the fence (today two air jumps). Whether the fence itself gets the closed gates' no-vault rule stays your call;
- **survivor headroom:** no post top with a figure on it has 1.7 m or more of headroom (the pilot capsule, `Core/NativeLocomotionRules.cs:8`);
- Hulk clearance on the lanes and round the camp, so the graph stays connected from every slot to every gate.

**Machines:** two PveMachine records in the generated hub area.
- **m 0:** `exp-city:hub-machine:chip`, kind chip, at (13.5, 0, 13.5), yaw 3.927 rad.
- **m 1:** `exp-city:hub-machine:med`, kind med, at (-13.5, 0, -13.5), yaw 0.785 rad.
- **Both:** front 0.7, dispense 1.3, chipDrop 2.2.
- **Gates:** lift the host's build gate (`Runtime/NativeExpeditionM1Arena.cs:110`) and serve gate (`:188`) for the hub.
- **Prices:**
  - chip: CachePrice(mutators, min(350, 175 + 25·bought)), with `bought` counted run-wide, so hub purchases climb the same ladder;
  - med: $75, refused at full HP, 12 s recharge, 4 pulses of 12.5% max HP, 0.5 s apart;
  - no recruit in the hub.
- **Revive clearance** (ReviveMachineChecks:195,201) against the machines, blocks and pods.
- **Transfers:** decide how heal and row state survive transfers and host migration.

**The prop pass on today's grid:**
- static collision blocks with stems `lamp-`, `bollard-`, `car-`, `bus-`, `barrier-`, `bench-`, `shelter-`, `barrel-`: 0.3 m lamp posts about every 20 m along the streets, and a few props per district;
- clear of spawn exits (6 × 6 m), routes, and the door, join and airlock volumes; Hulk clearance and nav kept;
- the block bound at `Tests/ExpeditionDeploymentChecks.cs:150` raised from 500 to 700 by measurement (about 600 expected with the camp).

**Optional:** crash noise the horde hears (HubNoise ignores sanctuary positions today).

**Contract:**
- **Version and digest:** a world Version bump (your string). The digest additionally hashes the area's machines (id, kind, x, y, z, yaw, front, dispense, chipDrop); the new blocks and slots are already hashed as blocks and slots.
- **Records and ids:**
  - the pod record is unchanged;
  - machine ids are distinct from the legacy `exp-city:machine:0`;
  - survivor structures and posts are blocks; their figures are Claude's and never replicated.
- **Release coupling:** ships in the same release as Claude's machine draw flag, labels and guests' reach test. Otherwise guests see machines the host never serves.

**Checks:**
- **Re-pin:**
  - PodRunChecks (boarding from the door and the ramp);
  - `Editor/NativeGeneratedWorldBuildChecks.cs:136-141` (Hulk routes);
  - MachinePlacementChecks:146 (the exact text at `M1Arena:110,:190`);
  - ReviveMachineChecks:169-173,181-201;
  - `NativeExpeditionActivationBuildChecks.cs:171-172` (guest machine parity) and :194 (each area's wire under 1 MiB);
  - ExpeditionDeploymentChecks:150 (the bound).
- **Add:**
  - a hub machine suite modelled on InstanceMachineChecks;
  - a native hub purchase in `NativeExpeditionM1Validation`;
  - activation parity for the hub;
  - HubPlan checks: every row valid and clear, the leaf pockets and the fence-vault rule included.

**Claude delivers:**
- the hub plan table first;
- then, in the same release:
  - the camp art and survivors;
  - the climb-out to the capsule at the ramp foot;
  - the machine dressing, draw flag and labels (the reach test shipped in M2);
  - kit dressers for the prop blocks, with Toon Harbor post heads on the lamp posts;
  - the kit keep-outs;
  - the sanctuary rule widened for exactly these ids.

**Done when:**
- nothing on the plaza can be walked through;
- no ramp, skid apron, machine use point, gate volume or leaf pocket overlaps another;
- host and guests serve the same machines;
- a pilot can board from the door and the ramp;
- a Windows host and guest run passes, including the 2026-11-02 boarding review.

## A2. Caches: seeded spots, a solid chest, instance candidates, tiers (M3)
**Spots:**
- A seeded spot pool from worldSeed, re-picked every run and every reroll (not every restock), replacing `n%7` (`Core/NativeExpeditionGeneratedWorld.cs:110-115`).
- **Where each can stand:**
  - in a facade niche or an alley mouth, back to a wall or kerb;
  - facing the street: the anchor's yaw in degrees is the chest's front;
  - the interaction at anchor + forward × 0.9 m, range 2.0;
  - at least 40 m apart;
  - at least 8 m from spawn exits, doors, door approaches, join circles and the final court;
  - Hulk-supported and reachable.
- **How many:**
  - 4 on today's grid: one per district and one neutral;
  - 5 on blockout-4, including one on a roof plaza reached by a stair-street or ramp (a horde connector).

**Collision:** a solid block of 0.92 × 0.62 × 0.58 m, centred on the anchor and turned by its yaw. It is hashed. Claude's steel locker appears only where this block stands.

**Instance candidates** (exp-layout-8):
- 2-3 designed candidates per cache, for Ward Zero, Central Station and the Ironworks;
- they go in the design scripts (`docs/arenas/scripts/expedition_ironworks.py` CACHES at :468-473, `expedition_central_station.py`, `expedition_layout.py`) and the fixture `Tests/expedition-live-layout.json`;
- regenerated with `tools/generate_expedition.py`;
- one picked per run by seed;
- offers and anchors paired per area (`Core/ExpeditionAreaProjection.cs:23-27`).

**Tiers,** if the owner approves Q26:
- `ExpeditionChestOffer.tier`: standard, rare, epic or legendary; missing reads as standard.
- It is carried through validation, the view, JSON, the host handoff and the legacy savepoint.
- It is rolled at SetWorld and at each restock (60/28/10/2%), from the run's economy RNG on the host.
- Prices: $100, $175, $275, $400. WINDFALL ×0.5 and BROKE ×2.
- Offers come from the matching offerWeights row.

**Also check:** whether boss "EPIC OR BETTER" picks leave out mythic chips (`NativeExpeditionIntegration.cs:96`, `NativeExpeditionPick.cs:46`).

**Contract:**
- The open moment stays "the generation changed with the cooldown still ahead".
- The buyer and the offers stay private (`NativeExpeditionView.Leaks`).
- It ships in A1's Version bump.

**Checks to re-pin:**
- `Runtime/NativeExpeditionM1Validation.cs:39-45`;
- ExpeditionDirectorChecks:40-41, ExpeditionIntegrationChecks:48 and ExpeditionMutatorChecks:56;
- ExpeditionLayoutChecks:130,238, `Tests/test_expedition_layout_draft.py:129`, ExpeditionCityChecks:190,272 and ExpeditionIronworksDraftChecks:132;
- `NativeExpeditionActivationBuildChecks.cs:164` (Level 0 and the Poolrooms keep an exit cache);
- the builder's Hulk routes.

**Claude delivers:**
- in M2, on today's hologram: the text, F prompt, J panel fix and loot moment, STANDARD only;
- in M3: the Civil Defence locker, sculpted in code, on your blocks, with the tier liveries and the livery sweep.

## A3. The tileset registry and its choice (M3, or M4)
**The registry:** `NativeTilesets` (shared Core). You own the rules fields:
- generator and its Version;
- look id;
- bank families;
- pool [{instance, theme}];
- hub enemy table;
- mutator pool (which mutators a tileset can roll);
- finalBosses;
- ambience;
- relief profiles;
- state (live or dev).

Claude owns the display fields (name, place, accent, emblem, board row) and creates the file in M1 with City's. City is `id=city`.

**State and intents:**
- `ExpeditionDeploymentState.tileset`, a string; null reads as city.
- SelectTileset(id): host-only; it clears the instance and mutator rolls. Add RollTileset only if the owner asks for a RANDOM lever.
- The revision stays monotonic across a tileset change. Update the revision rule at:
  - `NativeExpeditionDeployment.cs:154`;
  - `ExpeditionRunBundle.cs:47-49`;
  - `SameDeploymentTicket` (`Runtime/NativeExpeditionDeploymentRoom.cs:22-25`);
  - the receiver (`ExpeditionDeploymentRoom.cs:112-120`).
- **`worldSeed`** (`Core/NativeExpeditionDeployment.cs:101`) mixes in the tileset id only when it is not `city`, so every City seed, the builder's 8 layouts, the seed-pinned suites (GeneratedDistrictChecks and ReviveMachineChecks roll at revision 0) and the captured seeds stay as they are.

**Ticket and compatibility:**
- Ticket schema 2, with `tileset`.
- Compatibility and GeneratorVersion hash every live tileset's pool.
- deploymentProtocol goes from 2 to 3, batched into M3 (or M4) and coordinated with `codex/steam-player-hosted`.

**The bundle:**
- it picks the pool, generator, hub layout, base horde content, hub enemy table and mutator pool by tileset;
- its cache is cleared when the tileset changes;
- DeployInput exposes the tileset and its pool (Catalog is private today).

**The City-only sites:** each keyed by tileset in A3, or deferred with an owner.

| # | Site | Where | Disposition |
| --- | --- | --- | --- |
| 1 | Pool themes, and the appended Backrooms and Poolrooms | `Core/ExpeditionDeploymentRoom.cs:95-96` | A3: from the tileset's pool |
| 2 | The reel list | `Runtime/NativeDeploymentLive.cs:9-11` | A3: from DeployInput's pool (your glue) |
| 3 | The spawn allow list | `Core/NativeExpeditionGeneratedWorld.cs:107` | A3: from the tileset's hub enemy table |
| 4 | The hub kind table | `Core/NativeExpeditionHubRules.cs:65` | A3: keyed by tileset; A5a adds district and band |
| 5 | Base horde content, host and guests | `Runtime/NativeExpeditionM1.cs:36`, `Runtime/NativeNetworkFrame.cs:156` | A3: the tileset's content rows |
| 6 | Map registrations | `Core/NativeUnityMaps.cs:53`, `KnownDestinations`, `NativeExpeditionJoinZones.Known` | Deferred to the first real tileset (you): its map ids register there |
| 7 | The kit's looks | `NativeExpeditionCityKit.LookOf/Active` | Claude: keyed by the tileset's bank families and look |
| 8 | Map id and look | `Core/NativeExpeditionGeneratedWorld.cs:24,117` | A3: from the registry's generator and look |
| 9 | The revive machine's spot, by map id | `Core/NativeReviveMachine.cs:306-311` | Deferred: a new tileset's hub plan names its spot (Claude proposes, you validate) |
| 10 | The final boss's seat and ids | `Runtime/NativeExpeditionSplitRuntime.cs:45`, `Runtime/NativeExpeditionM1Arena.cs:260-264` | A3: from the tileset's finalBosses |
| 11 | Zombie-only mutators (HULKAMANIA, SPRINTERS, BLOATED) | `Core/NativeExpeditionMutators.cs` | A3: from the tileset's mutator pool |

**Proof:** a DEV-only proof tileset that differs from City in its hub enemy table, one mutator (left out of its pool) and its final-boss id (a DEV alias). A check pins that the bundle takes all three from the registry. Players see CITY alone.

**The recipe:** a one-page "Adding a tileset" doc, with Claude: everything a new tileset needs (registry row, generator and Version, look, bank families, pool, hub enemy table, mutator pool, final bosses, ambience, relief profile, board row and emblem), with the proof tileset as the template to copy.

**Checks:**
- ExpeditionDeploymentChecks (revision, ticket JSON, receiver);
- a pin that City's `worldSeed` is unchanged for a given seed and roll count;
- the proof tileset's pin;
- the builder's RunDeploymentAcceptance;
- an old client refused cleanly;
- host and guests see the same flaps.

**Claude delivers:**
- the ATLAS board (live from M1, its rows drawn from the registry's display fields, so a new row needs no new code);
- the pick animation (and RANDOM, if asked), and the reels re-skinned from the chosen pool;
- board sounds from Claude's own table, so `NativeDeploySounds` needs no change.

## A4. Blockout-4: the layered city and the City Layout Contract (greybox from M1; release M5)
**First:** a DEV-only greybox switch, solo, with Claude's kit fallback, at both N2 sizes, so the owner can walk them before any layered art. The generator takes the size as a parameter.

**Solo scale walk (M1 preview):** In the Unity Editor in Play mode or a Development Build, open **Settings → Developer → Walk Meridian greybox** from the offline main menu with your DEV account. The launch argument `--ca-layered-city-walk` opens the same picker. Enter a seed and select A or B. Use normal pilot movement, sprint and jump. Hold Tab for the north-up relief map and gate routes. Escape returns to the picker so the same seed can be walked at the other size. The four gates lead to City Hall and three dealt districts. The viaduct, outer streets, hall roofs and three-deck car park can also be explored. The western haul ramp returns from the Cut to the camp route.

| Preview size | Gate-to-hall acceptance | Collision blocks in 8 checked rolls | Maximum corridor nodes / links |
| --- | --- | --- | --- |
| A, 420 × 340 m | 150–190 m | 558–602 | 408 / 407 |
| B, 600 × 480 m | 250–300 m | 748–790 | 598 / 597 |

**Analytical plan views.** The [Option A sheet](expedition-scale-walk/meridian-scale-a.svg) and [Option B sheet](expedition-scale-walk/meridian-scale-b.svg) show the same eight seeds at a common north-up scale. Each panel traces the actual gate-to-hall routes over the collision footprints and plots each route's elevation against walked distance. Regenerate both with CoreTests `--layered-walk-plans docs/expedition-scale-walk` from the repository root.

The scale walk uses native pilot physics, grey collision massing and the existing map ramp wedges. The trace graph samples the four gate routes at 2 m or less. Run CoreTests with `--layered-walk` for the paired 8-seed sweep, including Hulk-width floor support and headroom. Counts above describe this walk prototype. M5's surface-wide navigation, chunked kit, population, performance budgets and deployment digest still require the release passes below.

**Size** (option A; option B scales it): 420 × 340 m: 42 × 34 cells of 10 m; bounds ±210 × ±170; ceiling 60; killY -25.

**Tiers** (TerraceStep 7 m):

| Tier | y | What is there |
| --- | --- | --- |
| The Crag | +14 | City Hall and the final court, north |
| The Knoll | 0 | The sanctuary, unchanged at the origin, and Belvedere Park |
| The upper town | -7 | Ward Zero's ridge spur, the Slag Terrace, the viaduct deck |
| The lower town | -14 | The main streets, Level 0's flat plain, the Ash Hollow |
| The Basin | -17.5 | The Poolrooms' plaza |
| The Cut | -21 | The station's tracks |

**Relief profiles by theme** (generation data):
- Level 0: dead flat, with uniform lot heights in a perfect grid;
- the Poolrooms: the Basin;
- Ward Zero: a walled ridge spur at the top of a straight boulevard;
- Central Station: the Cut, plus the viaduct;
- City Ironworks: the Hollow, plus a haul ramp up to the Slag Terrace.

**Lots:**
- 1×1 to 3×2 cells, with 0 or 2.5 m setbacks: alleys of 5 m, streets of 10 m, avenues of 15-20 m;
- heights of 4 + 3n m (7-22 m), and towers of 25-60 m;
- along sloped streets, buildings stand on stepped plinths.

**Roofs:**
- walkable: flat, with parapet blocks at least 1.0 m tall;
- pitched: ramp wedges, which Claude's art follows within 0.25 m;
- spires, tanks and signs on reachable roofs are blocks, or rise above the ceiling;
- no crown kind, and no pilot column over a building: retire today's 40 m columns (`GeneratedWorld.cs:122`).

**Pilot columns** only on the map edge: the Cordon's top, and the tops of the halls and the final court, which stand on the Cordon line.

**Connectors:**
- **Horde connectors,** on the horde's graph, Hulk-sized (at least 3 m wide, 3.2 m headroom):
  - sloped streets: MapRamp 'ramp', 1:8 to 1:6, for most tier changes;
  - stair-streets: MapRamp 'stair', 26.6°, a landing every 3.5 m of rise;
  - haul ramps: MapRamp 'ramp', 1:4 or gentler, at least 6 m wide;
  - the Hundred Steps: the north gate to the Crag, a 14 m rise;
  - the car park's ramps and decks (decks 3.5 m apart), and passages under bridges and the viaduct.
- **Pilot routes,** off the horde's graph: fire escapes, ladders, rubble ramps, narrow footbridges, and roof gaps (about 4 m for a running jump, 6 m for a sprint jump, wider only for chips or bridges).
- Per-link size flags, or a second graph for ordinary zombies, are your option later, with their own node and CPU budget.

**Structures,** as blocks:
- the viaduct deck (about 200 m);
- footbridges over the Cut;
- sky-bridges;
- pipe catwalks;
- a three-deck car park;
- the Cordon: a 14 m wall on the bounds, with watchtower blocks every 60 m and a pilot column only above its top.

**Props** as thin and solid blocks, with stems lamp-, bollard-, tree-, car-, bus-, bench-, bin-, barrier-, shelter-, crate- and barrel-:
- clear of the keep-outs: spawn exits 6 × 6 m, routes, doors, joins, airlocks;
- per district: ambulances near Ward Zero, a derailed tram near the Station, slag carts near the Ironworks, filing cabinets near Level 0, loungers near the Poolrooms;
- lamp posts about every 20 m along streets.

**Support and navigation:**
- Supported, Clear and Validate generalised to y: support comes from any block top or ramp surface.
- **A spatial index** (cell buckets) for Supported and Clear. Today both loop over every block for every candidate (`GeneratedWorld.cs:152-158,183-186`); at this size that is about 10^8 box tests per layer, on every peer, because guests rebuild the world from the ticket.
- Multi-level nav: 2 m nodes on walkable surfaces with layer ids, and walk, stair, ramp and one-way drop links.
- **Roof islands:** pilot-only roofs get their own nav, their hatches and one-way drop links to the street.
- **A validation-only pilot graph,** for reachability through fire escapes and jumpable gaps.
- Raise the node bound by measurement. Crowd flow may be local to the pilots.

**Spawn entries,** existing kinds only (door, vent, drop, breach):
- placed on shopfront doors, ground-floor windows, manholes, roof hatches, parapets (drop entries, with `height` above the landing) and breach panels;
- **a roof hatch on every walkable roof without a horde connector,** so no roof is a safe spot;
- `surface` (or a new hashed field) names which; `level` is the nav layer;
- at least 20 m from door approaches;
- each opening reserved in `openings[]`.

**Routes and influences:**
- Rings retuned by walked distance.
- `Hub.Routes` Via points with y. Routes run 150-190 m from the gates to the halls (option A).
- Influences retuned: share at least 0.70 within 30 m of a hall door, about 0.45 at 60 m, about 0.25 at 110 m, under 0.10 at 160 m. The sanctuary stays 100% neutral.

**Records:** features (dark, flood, rail, hang, wall, cut), landmark slots, and the layout below.

**The contract,** `NativeCityLayout` (Core, pure):
- It is versioned with the world Version, attached to the generated world, and every field is hashed.
- Claude draws exactly what it says, as the Backrooms layout works today.
- Claude's `FromBlockout3` adapter (M3) expresses today's world in it until you fill it natively.

```
CityLayout {
  version; cellMetres = 10; terraceStep = 7; minX, minZ, cellsX, cellsZ
  cells[]     { x, z, tier, use(street|plaza|lot|prefab|hub|court|cut|void), lot, influences[4] }
  lots[]      { id, x0, z0, x1, z1, tier, height, roof(walkable|pitched), setback[n,e,s,w], district, hero, seed }
  links[]     { id, kind(slope|stair|ramp|bridge|drop|fire-escape|ladder|rubble), horde, a{x,y,z}, b{x,y,z}, width, headroom, blocks[] }
  prefabs[]   { id, kind(carpark|viaduct|gantry|skybridge|aqueduct|ceiling|cordon-tower), x0, z0, x1, z1, turn, tier }
  landmarks[] { id, district, kind, x, y, z, yaw, sx, sz, height }
  features[]  { id, kind(dark|flood|rail|hang|wall|cut), volume | segment, depth }
  props[]     { block, stem }
  openings[]  { block, face, u0, u1, y0, y1, entry }
}
```

- `horde` is true when the link is on the horde's graph. Slopes, stairs and ramps are also MapRamp records; drops are also navHints.drops.

**Block id prefixes by role:** terrace-, retain-, slope-, lot-, stair-, ramp-, bridge-, viaduct-, cut-, cordon-, crag-, landmark-, prop-, roof-.

**Version and digest:**
- Version `deployment-world-blockout-4`.
- The digest adds MapRamp records, link kinds and horde flags, node layers, pilot columns, every CityLayout record and the new spawn fields. Today it hashes none of these.

**`NativeCityBleed`:** A4 retunes the influences it reads; its thresholds stay versioned with the world Version. You own the whole function from A5a.

**PilotReach:** publish the reach envelope (rise and distance) for the worst live stack, for traversal design only. No rule or check relies on it for honesty.

**Budgets:**
- count blocks, nodes per layer and links on the greybox, and write them into this hand-off's budget rows as the new bounds (about 2,000-3,000 blocks and 20,000-30,000 nodes are expected for option A);
- generation 1.5 s or less on a worker, measured on the slowest supported guest machine;
- the sim benchmark within the A0 baseline + 25%;
- the area wire under 1 MiB.

**Checks:**
- **The sweep:** 2,000 seeds through Validate and ValidateGeometry.
- **The native gates:** 8 layouts, Hulk routes over every horde connector, and the 1,800-tick fixture.
- **Reachability:** every walkable surface, roof plaza and bridge is reachable by a pilot without chips (the pilot graph); every walkable roof without a horde connector has a hatch.
- **Re-pin:** ExpeditionDeploymentChecks:52-61 (the entrance share) and :150 (the measured bounds); the station holo map and the skyline bounds (Claude's, sized from the map).

**Claude delivers:**
- **Before you start:**
  - the world brief: this package, plus landmark footprints;
  - the art keep-outs: reserved openings kept clear, parapets as blocks, the 0.25 m and 3.1 m rules;
  - a DEV top-down roll viewer at the new size;
  - the kit fallback dressing the new block roles, by 32 m chunk.
- **After:**
  - the layered bank;
  - the landmarks, in a far layer that reads through the fog;
  - the light field v2 and the rebuilt light volume;
  - the relief bleed's dressing.

## A5a. District kinds on today's grid (M4)
**Kinds by district:**
- `ExpeditionHubRules.Kind(ring, kind, elapsed)` (`Core/NativeExpeditionHubRules.cs:65`) gains the district and band at the spawn exit, through `NativeCityBleed`, which you now own. Set today's grid's thresholds so instance kinds start about 20-60 m from each hall door (today's street spawns start about 20 m out).
- Instance kinds are allowed only at Warp or above.

**Plain kinds,** each only where it works off its home map (they bind only on their own maps today, `NativeZoneCombat.Bind :29-35`):
- Level 0: Hounds, in packs of 3;
- the Poolrooms: Tilers, if they can anchor on today's building faces; otherwise on blockout-4's wall features, in A5b;
- Ward Zero: a spitter-heavy street mix;
- Central Station: Conductors and Pileups;
- City Ironworks: Cinderlings, Stokers and Slagbacks.

**Caps:**
- at most 25% of spawns in warp, and 40% at threshold;
- at most 6 instance-kind minions alive per district;
- none from a cleared instance (`ExpeditionView.instances[].cleared`).

**Network:**
- content rows bound on host and guests (`M1Arena:108-109`, `NativeNetworkFrame.cs:156-161`);
- caps on the city map (`Runtime/NativeExpeditionZoneEnemies.cs:12`);
- the Zone frame sent on the city map (`NativeNetworkFrame.cs:131`), with a protocol bump batched into M4.

**Also:** rewards by theme, and a difficulty and CPU review with the owner. If the CPU review fails, the fallback is district clothes only (Claude's, already live from M2).

**Contract:**
- Kind ids are unchanged and append-only (`Core/NativeHordeKinds.cs:26-44`).
- The Zone frame's fields are unchanged; only where it is sent changes.
- The minion frame and HubZombieBrain view states are unchanged.

**Checks:**
- HordeKindChecks;
- the zone combat checks;
- HubPerceptionChecks;
- ExpeditionHubAuthorityChecks:65-77, re-pinned;
- the native activation fixture (1,800 ticks, four areas);
- frame sizes within `NativeFrameBudget`;
- a measured four-pilot run, with A0's harness.

**Claude delivers:**
- the instance kinds' looks loaded on the city map when their district is rolled;
- their sounds and their night readability;
- a DEV_LOG note: no change to hit timing or hitboxes.

## A5b. The rooftop horde and feature-gated kinds (M6-M7)
**Rooftops** (on blockout-4):
- roof perches: a percher is Idle on a roof entry;
- roof-hatch door entries;
- parapet drops through `NativeHordeDrop`, with height from the entry, plus a lighter profile for runners if you add one;
- breaches, including Ward Zero's quarantine panels;
- Slagbacks and brutes up the Ironworks haul ramp.

**Feature-gated kinds** (M6 onward; the last in M7):
- the Smiler, in dark features;
- the Drowned, in flood features;
- the Undertow, in the flooded Cut;
- the Linesman, on rails;
- the Rigger, on hangs;
- the Faceling, on walls.

**Contract and checks:** as A5a, plus the horde reaching roof players by horde connectors and hatches, and leaving the roofs by drops.

**Claude delivers:**
- perchers crouched on the parapet, instead of the ink tear;
- perch idles;
- breach panels styled per district;
- the threshold creatures' dressing.

## Shared constants

| Constant | Value | Owner |
| --- | --- | --- |
| ExitReach | 0 until A1, then 2.4 m | Shared, in `NativeExpeditionHubPlan`; yours after A1 |
| The landing field | Four spots per run, 11-15 m out, 4.2 m off the gate axes, 12 m apart, yaw ±30° | Yours (A0) |
| ExpeditionRules.PodOpening | 5.0 s, generated hubs only (3.0 if the owner says no) | Yours (A0) |
| TravelAt | 5.6 s (4.0 today); the host already follows it | Claude |
| Band thresholds | Layered city: 0.10, 0.25, 0.45, 0.70; today's grid: set in A5a; versioned with the world Version | Claude's provisional values in M2; yours from A5a |
| TerraceStep | 7 m | Yours (from Claude's brief) |
| PilotReach | An envelope for the worst live stack, for traversal design only | Yours |
| Look `expedition-night` | Resolved by map id | Claude |

## Release coupling
- **M1:** A0, together with Claude's ATLAS board, eject, cradle card and full crash on the landing field. One world Version bump.
- **M2:** nothing of yours. Claude's night look, light packs, first bleed pass, camp fence and gates, cache text and the machine label and reach fixes ship alone.
- **M3:** A1, A2 and possibly A3, together with Claude's camp, survivors, machine presentation, climb-out to the ramp foot, steel lockers, liveries, hall fronts and prop dressers. One world Version bump, and deploymentProtocol 3 if A3 rides along.
- **M4:** A5a (and A3 if it missed M3), together with Claude's modelled streets, set pieces and the district kinds' looks. The expedition protocol bump for the Zone frame.
- **M5:** A4, together with Claude's layered bank, landmarks, field v2 and light volume.
- **M6:** A5b, together with Claude's perchers, breaches and threshold set pieces. Any protocol bump batched here.
- **M7:** A5b's last feature-gated kinds, together with Claude's final court and the Low-tier pass; a 4-player Windows run.
- **Every release:** log the compatibility break (older test clients are refused) in the DEV_LOG and in the release receipt.

```
===== END HAND-OFF TO ASTRA =====
```

---

## A4 update after the owner's walk (2026-11-18)
The owner walked the greybox and chose size B (600 × 480 m, 250-300 m routes), correcting an earlier reading of A ("Sorry I mean B is a good size"). They also asked for "just like 5 unique set of paths" instead of fully random layouts. That changes A4:

1. **Choose the five.** Propose five layouts from the greybox generator, at size B, with clearly different route shapes, elevation and landmark spots. For example, take the most distinct of the walk seeds and hand-tune them. Each needs four or more gate/hall slots and its rooftop and terrace routes.
2. **Freeze them as data** in the City Layout Contract, versioned, so the host and guests load the same layout by index. A run picks one layout, seeded or rotated so a party doesn't repeat it back to back, and the run's instances fill its slots.
3. **Keep varying per run:** which instance takes which slot, caches (A2), survivors' spots, and enemy spawns and district kinds (A5a).
4. **Developer walk:** extend "Walk Meridian greybox" to list the five frozen layouts by name, so the owner can walk each before any art.
5. **No mirroring.**

Claude then dresses each layout by hand (W8 and W10).

**Size B also changes the budgets.** Rerun the native sim benchmark (4 pilots, 88 zombies) and nav/crowd-flow timing on the B layouts. Report any budget the larger map breaks before the layouts are frozen.

## Amendments from the voxel retirement (owner-approved 2026-11-18; see docs/VOXEL-RETIREMENT.md)
These go to the owner with this plan, are logged in the DEV_LOG with V0, and are amended in `docs/EXPEDITION-OVERHAUL.md` and `docs/EXPEDITION-OVERHAUL-ASTRA.md`:
1. **R8 and W3** (:608, :1068): "so the visual, voxel and Classic bodies all play them" becomes: the model plays the eject. W3's crouch, fall and seat move onto the model's own clips and hand IK in V4 (Stage 2), and the voxel style plays the rig versions without the bake. The cast gains no Classic branch; its voxel branch is chosen by the body predicate (U4, M12, P9).
2. **R6 and W5** (:505, :1167): "a voxel fallback" for survivors is dropped. Without the bake, survivors are not drawn.
3. **W0** (:911): the frame capture adds the 8-pilot PvP pass and the Classic on-and-off pass. It runs in a Development Mono player with `PERF_SAMPLE` and `PERF_HITCH`, beside one Editor run, and adds a low-end run. The checks gain the ratchet, VoxelStyleChecks' first half and the counter (V0).
4. **R2 and W8b** (:190, :1383): modelled dressers in M3 wherever the bank has the stem; the box "kit dressers" only as the public fallback.
5. **W7 and W8** (:156): the Central Station and City Ironworks stage banks do not exist yet. V13 builds them in Stage 4, before W7's M4 set pieces for those zones; until then W7 uses the kits' looks.
6. **W7 in M2** (:1671-1676): the district outfits ship with V7 part 1 (a pool miss hides the add; prewarm by district), so they never pop in as voxel.
7. **W11a** (M4): district kinds in the city need V7 parts 2 and 3 (pinning, prewarm, the LODs) and V15's numbers. Otherwise far Hounds draw as voxel shamblers on all fours, and unprewarmed pinned adds draw nothing for frames. Pinned kinds take Full slots, so Astra's district caps and Claude's tiers are sized together (VA3).
8. **W11b** (M6): perches and drops are authored on the model's gait (V8).
9. **Section 6** (:1755-1790): add the crowd and pilot rows above (3, Budgets). Stages 3 and 4's absolute crowd gates replace the headroom share for the crowd, and the crowd is measured in the player.
10. **M7:** the final court keyed by its boss brings the Siren's own model (O5).
11. **Superseded owner decisions:** add "2026-10-17 'Keep voxel style, just improve it' (zombies)". The Prototype Series' "Keep it voxel" (2026-10-29) and "Model everywhere, classic will be stored as backup" (2026-10-25) stand: this plan fulfils them.
