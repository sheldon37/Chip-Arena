# Expedition Deployment Station: two machines and a generated destination

Owner direction (2026-10-31): party starts together at a Deployment Station, rolls instances on the first machine, can optionally roll a second mutator machine, and then deploys. The first roll generates the outdoor exploration map: approaching an Ironworks entrance gradually gives the surrounding world an Ironworks identity, approaching Central Station gives it a transit identity, etc. Once the Expedition begins, neither machine may roll.

## Approved gameplay shape

1. The Deployment Station is separate from the actual Expedition world. All party members are in the same safe staging environment; the active combat clock, kills, rewards and world simulation have not started.
2. INSTANCE MACHINE is required. It selects three distinct authored instances from an eligible catalog and maps them to three entrances. This chooses which three maps exist in this run; their authored interiors and bosses remain unchanged. At present there are only three authored instances (Ward Zero, Central Station, City Ironworks), so the earliest version varies their placements, procedural outdoor roads and themes, not the roster.
3. MUTATOR MACHINE is optional. It rolls up to three IDs from a vetted catalog. Skipping it leaves no mutators. Exact number of reels, effects, rewards, stacking, price and party voting remain proposals.
4. DEPLOY becomes available once the instance/world roll is complete and the existing party system verifies readiness. The host starts. The roll outcomes become immutable for the entire Expedition, including repeat loops, reconnects and authority migration.
5. The final boss is distinct from the three rolled instance entrances; it remains a fixed run requirement.

## Implemented on task branch: dormant rules foundation

- Core/NativeExpeditionDeployment.cs is a deterministic pre-run authority with separate instance and mutator RNG streams. The host can roll instances, independently roll or skip mutators, and deploy; guests are rejected. New rolls overwrite the old pre-run result; no roll changes the state after deployment.
- ExpeditionDeploymentState has detached copies suitable for future networking. The instance seed and optional mutator draw indices are captured.
- A procedural 25 by 19 cell world **blueprint** (10-meter cells, 250 by 190 meter footprint) is generated from the first machine. It contains a safe neutral central plaza, connected road routes from the center to all three selected entrances, and theme influence percentages across all cells. For each cell the four values (neutral, instance 0, instance 1, instance 2) sum to 100. This can drive future district blends of terrain, walls, props, lighting, ambient sound, and enemy populations. Interiors are not procedurally modified.
- Tests/ExpeditionDeploymentChecks.cs exercises host authentication, required-first roll, uniqueness, seed determinism, three reachable themed destinations, normalized influences, independent mutator randomness, optional skipping, readiness and permanent lockout, and snapshot isolation.
- Tests/Program.cs adds targeted --expedition-deployment and full-suite invocation.

**Not implemented or claimed:** actual Deployment Station scene, machine presentation, world block mesh, UnityMaps/ExpeditionLayout generation, new nav mesh/path generation, collision, encounter placement, mutator gameplay effects, co-op replication, room ready/start wiring, save/handoff transport, Windows build or a published test update. Existing M1 game mode remains unchanged.

## Next integration: Astra (simulation/data/network)

- The real host room owns an authenticated deployment authority. Host sends roll intents, not selected data or random outcomes. Guests receive a replicated immutable result. Disconnected/reconnected users see the same roll and world seed.
- Choose the eligible authored instance pool from validated content and the selected Expedition. When more than three instances exist, the first machine yields meaningful combinations; until then it can only vary layout/placement.
- Turn the blueprint into a generated outdoor map entry and ExpeditionLayout manifest while retaining authored instance interiors. Must generate buildings, exterior entrance shells, safe starts, gates, nearby scenery, traversable paths, collision, supported surfaces, accessible caches and spawn points, line-of-sight/nav tests, and final boss arena. All checks that static city generator currently enforces apply here too. Never ship a cell grid as if it were full game geometry.
- Apply a vetted mutator catalog to host-authoritative PvE state at deploy. Define compatible/contradictory effects and explicit chip, spawn, boss and reward interactions. Replicate IDs/effects, never let clients choose arbitrary mutator IDs. No mutator gameplay rules have been agreed.
- Record immutable selection and generated map compatibility in network run handshake and transfer/handoff. Freeze the machine state before starting PvE time and ensure a no-mutator start is valid. Existing in-progress M1 games keep their old fixed map/schema.

## Next integration: Claude (presentation)

- Design a full Deployment Station staging room with all party characters visible, two mechanically distinct slot machines, world/instance map preview, mutator preview and Deploy action.
- Instance machine reels resolve to the authority's three fixed instances and three generated approaches. Progressively blend scenery toward each instance using the cell influence weights, not hard seams between biomes.
- Mutator machine visually communicates optionality, good/bad/chaotic effects and a skip option. A rolled effect must not be visually presented as active until the host applies it.
- When Deploy occurs, the machines go dark/lock, selected map preview freezes, and the party travels to the generated world. The station is not the hub sanctuary; it is a pre-run location.

## Presentation (Claude, 2026-10-31): the station, the machines and the map

Branch `claude/deployment-station`, on top of Astra's foundation (`codex/expedition-deployment-machines-20261031`).
Presentation only. Nothing here rolls, decides or writes state: every client draws the same pictures from the replicated
`ExpeditionDeploymentState`, plus its own clock since it saw each roll.

### The set

The Deployment Station is a staged set, not a walkable map. Like the rooftop menu and the draft stage, the party stands on
marked pads and the station needs no collision or nav. Astra can make it walkable later; the layout below is the contract
either way. Station frame: metres, x right, z away from the default camera, y up.

- **The holo war-table** (centre, `Table` (0, 0, 2), radius 1.6 m) projects the blueprint map: Core `Preview`. It shows
  - district colours blended by the cell influences;
  - brighter roads and the neutral plaza ring;
  - the three entrances as rings in their colours;
  - the boss gate as a red diamond on the north edge.

  It freezes at Deploy.
- **The party pads:** 8 pads on an arc 4.4 m in front of the table, the host on the centre pad and the rest alternating
  outward (`Pad(i)`). Each pilot stands on its pad in its lobby idle, with a ready lamp under it from the room's readiness.
- **The INSTANCE MACHINE** (left, `InstanceMachineAt` (-5.6, 0, 3.6), turned 55 degrees toward the table): a tall
  three-reel "destination engine".
  - **Cabinet and lever:** a graphite and brass cabinet with a big pull lever.
  - **Reels:** three drum reels carrying the eligible instances' emblems, each reel topped by its entrance label (EAST,
    WEST, SOUTH).
  - **Approach strip:** a strip under the reels shows each district's generated approach in its colour.
  - **Theme:** heavy, mechanical, warm industrial light.
- **The MUTATOR MACHINE** (right, mirrored): mechanically different, a "wild card" machine.
  - **Dials:** three card dials spinning under a glass dome.
  - **Polarity colours:** boon green, bane red, chaos violet, unknown grey.
  - **Skip plate:** a SKIP plate and "OPTIONAL" signage with dashed light outlines, so it never reads as required.
  - **Theme:** volatile, flickering, cooler light.
- **The DEPLOY gate** (back wall, `Gate` (0, 0, 9)): a lift platform behind blast doors, with a console in front (the
  Deploy action) and a readiness light per party member.
- **Shots** (`ShotOf`, `Framing`): the overview by default, the spinning machine's close-up while it rolls, the gate on
  Deploy.

### What each piece shows (Core `NativeDeployPresentation`)

- **Reels:** each reel spins forward from where the last roll left it.
  - Timing: a 0.3 s spin-up, then a cruise of at least 11 symbols a second.
  - Stops: the reels stop left to right at 1.6, 2.05 and 2.5 s, overshoot a quarter symbol and settle onto the
    authority's result (instance reel i on district i; mutator reel i on mutator i).
  - Edge cases: a client that joins after a roll sees it landed. An id the presentation does not know is added to the
    strip, so it still lands.
- **Instance machine:** Dormant (the host is invited to pull; a guest sees "waiting for the host"), then Spinning, Landed
  (the host may reroll), then Locked at Deploy. When locked it dims to 20% with the result still legible, and the lever is
  clamped.
- **Mutator machine:**
  - **Before the first roll:** Unavailable until the destinations are rolled, then Optional ("roll, or deploy without").
  - **Rolled:** Spinning, then **Pending**. A rolled mutator is never shown as active before Deploy; its card carries a
    PENDING stamp.
  - **Skipped:** reads "no mutators".
  - **At Deploy:** Active, the cards stamped ACTIVE and still lit while the cabinet darkens, or just Locked when there are
    none.
- **Deploy console:** "roll destinations to deploy", "waiting for the party", DEPLOY (host only; guests see "waiting for the
  host"), then deploying.
- **Deploy sequence:**
  - the machines dim (0.6 s) and clamp (0.2–0.5 s);
  - the gate opens (0.8–2.2 s);
  - the lift carries the party out (2.2–3.6 s);
  - the screen fades (3.4–4 s);
  - the travel hand-off starts at 4 s.
- **Display tables:**
  - **Destinations:** Ward Zero (quarantine green), Central Station (transit cyan), City Ironworks (foundry orange). A new
    instance falls back to its theme's look, then to neutral.
  - **Mutators:** `surge`, `storm`, `darkness` and `brittle` (the ids Astra's checks use), plus `windfall` and
    `second-wind`. Names and one-line flavour only, no numbers. Astra's catalog decides what each id does; an unknown id
    reads UNKNOWN.

### The generated world's look (for the outdoor map, once Astra generates it)

- `Weights(state, x, z)` gives the theme blend [neutral, district 0, 1, 2] at any world point. It is the bilinear,
  smoothstep-weighted blend of the four nearest cells' influences.
  - It equals each cell's influences at the cell centre and sums to 1 everywhere.
  - It changes by at most 0.08 over 25 cm, so dressing driven by it (props, wall kits, lighting, fog, ambient sound, enemy
    look) fades between districts with no seam at cell borders.
- `Road(state, x, z)` is the same blend for roads.
- Interiors stay authored. Only the outdoor approach is blended.

### Checks

`CoreTests --deploy-presentation` (also in the full suite): 436,266 checks.
- Reels land on the result for every strip length 1 to 12, every reel, every start and every result. They spin forward
  only, never faster than 30 symbols a second with no jump, and overshoot and settle.
- Phases are checked for host and guest across a real authority's lifecycle (roll, mutators, skip, reroll, deploy). Mutators
  are never Active before Deploy at any time.
- The deploy timeline is monotone, and travel starts at 4 s.
- Weights equal each cell at its centre, sum to 1, have no seam and are neutral at the plaza, and each district owns its
  entrance.
- The map is deterministic and opaque, with entrances in their colours and the boss gate red.
- The pads are apart and clear of the table and machines; the shots are sane; the display tables have fallbacks.

### Needs from Astra (data the presentation reads; nothing here changes the authority)

- **The state:** the replicated `ExpeditionDeploymentState` on every client.
- **Roll times:** each client's local time when it first saw a new `instanceRolls` or `mutatorRolls`, so reels spin. A
  missing time is fine: the result shows landed.
- **Deploy time:** each client's local time when it saw `deployed`, for the sequence.
- **Readiness:** per-seat readiness for the pad lamps, and the aggregate `partyReady` for the console.
- **Intents:** host intents from the console's buttons: roll destinations, roll mutators, skip, deploy. The runtime only
  asks; the authority answers.

## Runtime presentation (Claude, 2026-10-31): the station drawn, its console and a DEV preview

Branch `claude/deployment-station`. Presentation only, built on the rules above; nothing here rolls or writes state.

### What is built

- **Core `NativeDeployModels`** (engine-free sculpts, `NativeWeaponSculpt` kit, Unity's frame, each model facing -z;
  bones 0 body, 1 lights, 2+ extra lights, -2 glass, -3 screen faces with uv in `WeaponSection.Data`):
  - the INSTANCE MACHINE (graphite and brass cabinet, marquee with two alternating bulb banks, DESTINATIONS sign,
    EAST/WEST/SOUTH plates, approach strip in three district segments, gauges, struts, the lever on its side);
  - one reel drum band (drawn three times, its symbols scrolled so any strip length lands exactly) and its rims (turned);
  - the MUTATOR MACHINE (blue enamel and chrome, three card dials on spindles under a glass half-dome, polarity rings,
    a SKIP plate and an OPTIONAL sign with dashed light outlines, WILD CARD fascia, flickering cool lights);
  - the war-table (projector ring, glass top, the holo map plane, its light volume, entrance and boss beacons), the
    party pad (ready-lamp ring), the DEPLOY gate (frame, two blast-door leaves, the lift and its lit bay), the console
    and readiness lamps, the room (plates, walls, gantries, beams, light bars, two wall map screens);
  - the station's eight practical lights, a key and the ambient (`StationLights`).
  - Footprints are documented on the class (`Footprint`); budgets: about 105,000 triangles for the whole set.
- **Core `NativeDeployArt`**: the reel symbols, mutator cards, wild-card back, PENDING / ACTIVE stamps and a soft glow,
  rasterised by `NativeIconRaster`. The lettering is a chamfered monoline A-Z, 0-9 in the CHIP ARENA wordmark's style.
  The signs and tiles share it.
- **Core `NativeDeployStage`** poses a frame from the replicated state and this client's times:
  - **Reels and dials:** posed by `Reel()` from the last landing, landing on the authority's result.
  - **Machines:** power, banners and the lever from the rules. Dormant reels rest between symbols, and the dials show the
    wild-card back until rolled.
  - **Stamps:** PENDING until Deploy, ACTIVE after.
  - **Deploy:** the doors part, the lift rises, the party turns to the gate and is lifted out in light columns, and the
    machines dim. The map freezes at Deploy. Each light shaft is a soft ribbon with a bright core, fading upward, turned
    to face the camera about its pad, so the close Deploy shot reads as light rather than solid tubes.
  - **Each frame:** the camera eases between `ShotOf` shots. It sets the lights and floor light pools, and the
    console's host controls (`ControlsOf`).
  - **Allocation:** none per frame (checked).
  - **`NativeDeployClock`:** keeps each client's local roll and deploy times; the first state it sees stays landed.
- **Runtime `NativeDeployScene`** draws the stage:
  - **Meshes:** built once.
  - **Lit parts:** NativeSourceLit with the stage light block.
  - **Glass:** NativeSourceTransparent.
  - **Screens:** a new `Resources/NativeDeployScreen.shader` for the strips (scroll, blur), the holo maps (edges, cell
    grid, frame, sweep), the stamps and the light. Strips and the map are rasterised on worker threads; the map changes
    only when `revision` does.
  - **Party:** `NativeDeployCast` poses each pilot on its pad in its lobby pose (hero stance or arms crossed) on the
    visual body, with voxel as the fallback, in its seat's outfit and skins.
  - **Console:** `NativeDeployConsoleUI` draws the destination cards (revealed as each reel lands), the mutator cards
    (PENDING / ACTIVE), readiness, the host's buttons or the guest's status, and the travel fade. It returns a
    `DeployIntent` and never calls the authority.
- **DEV preview (`--ca-deploy-preview`).** It replaces the home screen, like `--ca-expedition-preview`.
  - **Authority:** a local `NativeExpeditionDeployment` (station `preview`, host = the local account id, seed from the
    clock).
  - **Catalogs:** the three authored instances and the display table's six mutator ids.
  - **Party:** one to four stand-in members with readiness toggles; it can also view as a guest.
  - **Deploy:** it plays the whole sequence, says that no generated world exists yet, and returns to the menu.
  - **Scope:** it is the only code that answers the console's intents. It touches no room, network frame, Expedition
    start flow or save.

### Checks

- `CoreTests --deploy-models` (also in the full suite): 1,985 checks.
  - **Models:** deterministic, clean, inside footprints and budgets. Lights are unlit and glass is see-through.
  - **Pivots:** the lever hub, axle stubs, spindle hubs, door leaves and lift sit on their bodies.
  - **Screen UVs:** correct.
  - **Real-authority lifecycle:** every reel and dial lands on the result, the lever follows `Lever()` and the doors
    follow `Gate`. Nothing is ACTIVE before Deploy; skip and late join are covered; the map freezes; eight lights.
  - **Stage allocation:** zero bytes per frame.
  - **Art:** the tile art is checked.
  - **Runtime wiring (text scan):** the reels come from `Reel()` and the map from `Preview()`, and only the DEV preview
    calls the authority. The draw does not allocate, and the metas are present.
  - **Shader text:** `NativeDeployScreen.shader` names no variable after an HLSL reserved word (`line`, `point`,
    `triangle`, `sample` and the adjacency forms). The first draft used `line`, which would not have compiled.
- Offline renders were made with WebGL emulating the shader path; they are not committed.

### What Astra wires for the live room (presentation reads it; nothing here changes the authority)

- **State:** the replicated `ExpeditionDeploymentState` → `DeployInput.State` each frame on every client.
- **Times:** feed `NativeDeployClock.Observe(state, localTime)` each frame. Then pass `SinceInstanceRoll`,
  `SinceMutatorRoll` and `SinceDeploy`.
- **Readiness:** per-seat `Ready[]`, aggregate `PartyReady`, `Seats` (host on pad 0) and `Host` for this client.
- **Catalogs:** the eligible `Instances` (with themes) and `Mutators` the reels carry.
- **Intents:** `NativeDeployConsole.Draw` returns `DeployIntent`. Send it as a host roll, skip or deploy request; the
  authority answers.
- **Looks and travel:** the party's `DeployPilot` looks (pose and outfit). The hand-off starts when
  `Stage.Deploy.Travel` turns true.
- **Hooks:** `ArenaPresentation.RenderDeployStation` / `DeployLeave` and `DeployStage`; the DEV preview shows the
  calls.

**Not done or claimed.** No Unity Editor or Windows run, and no Unity shader compile. Not checked: the pilots on the
real visual body, bloom and fog tuning in Unity, and console layout at every aspect. Also not done: the live room or
network binding, audio, and any test-update publication.

## Open design knobs (safe defaults for foundation only)

- Unlimited pre-launch rerolls with no price, until cost/cooldown is specified.
- Only the host rolls; no voting yet. Party readiness is required to deploy.
- Three unique mutator IDs are drawn per optional roll; no effect catalog or reward multiplier is bundled.
- East, west and south entrances, neutral center, one final boss to the north are temporary generator anchors. Any later world structure must preserve reachability and authored instance entrances.

## Verification and release

The new authority and test file are source-only; the current environment does not have the pinned Unity Editor or .NET SDK to execute managed tests. This work is not integrated into the live scene or published to the builder. First check on a suitable development machine: dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -- --expedition-deployment. Then run the full managed suite and an actual Windows multi-client preview after the UI, geometry and network work.
