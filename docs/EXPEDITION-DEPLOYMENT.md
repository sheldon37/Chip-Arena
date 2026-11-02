# Expedition Deployment Station: two machines and a generated destination

Owner direction (2026-10-31): party starts together at a Deployment Station, rolls instances on the first machine, can optionally roll a second mutator machine, and then deploys. The first roll generates the outdoor exploration map: approaching an Ironworks entrance gradually gives the surrounding world an Ironworks identity, approaching Central Station gives it a transit identity, etc. Once the Expedition begins, neither machine may roll.

## Test-update scope (owner confirmed 2026-11-01)

The owner selected **test update now with active-run host migration unavailable**, authorizing
integration into shared and the existing test builder. This supersedes the earlier draft-only
hold for this limited test scope; it does not claim complete host migration or production readiness.
The generated station/world/district features are included; active-run transfer remains disabled.

`PrototypeBuild.Validate` now runs `CheckDeploymentBuildAcceptance` before Windows packaging
(and the cloud pre-export path uses the same validation). Even under `-nographics`, this runs
eight generated layouts, four 1,800-tick split-area fixtures, native codecs, lobby transfers and
loading/handoff guards. Failure throws before packaging/publication. The headless marker is
`DEPLOYMENT_BUILD_ACCEPTANCE_READY`; it explicitly excludes GPU passes and rendered/human co-op
acceptance. With a graphics device the shader passes run too. The standalone graphics check below
remains available. Native build results, publication and manual playtesting must be reported separately.

## Current integration (Astra, 2026-10-31)

Task branch `codex/deployment-completion` combines continuation `c9039362` with Claude's station presentation `654c4f85`. Earlier foundation/dormant notes below are historical; this section describes the combined source.

- The Expedition catalogue launch opens the real station. Host and guests use the room's authenticated selection; the console shows actual connected party members and readiness. Guests can change their own ready state, not roll or deploy. The DEV preview remains independent.
- A destination roll constructs an `ExpeditionRunBundle`: detached map catalogue, deterministic generated outdoor collision/navigation, composed layout retaining the authored interiors, hub gates/routes/zones, and blueprint. No arena, run clock or rewards start during a roll. The bundle is cached across frames and replaced only for a new roll.
- Deploy requires the current revision, signed-in host and ready guests; it binds the full generated identity into the irreversible ticket before entering the existing stage-loading pipeline. Actual native layout/clearance/navigation validation runs before creating the Expedition session. Validation failure leaves the ticket locked and reports an error; leave the station to create a fresh run.
- Every split-area arena receives that same bundle. The locked staging interval rejects new joins and ready changes; authenticated existing-seat reconnects still work. Guest lobby and combined state envelopes carry the compact ticket, including reconnects; guests reconstruct locally and reject mismatched layout identity before applying a frame. Guest PvE, dynamic gate collision and world/door presentation use the generated layout. The generated map never enters PvP rotation. Existing fixed-map sessions still have the null-ticket fallback.
- Generated runs hold simulation and elapsed time until every connected party member has acknowledged initial loading. Later subset transfers retain their independent behavior. Repeat loops keep the bundle; returning to the lobby clears it and restores the shipped map catalogue.
- Deployment capability is now `2`; capability `1` only implemented the dormant staging descriptor and cannot join this live path. General fixed-M1 networking is unchanged.
- Owner confirmed the optional mutator machine should wait for a separately agreed effect list. Live inputs supply an empty catalogue, so it stays unavailable; skipping is valid. Preview-only effect names are not live gameplay effects.

### Astra follow-up: bounded initial loading

The combined candidate now includes shared stage/Training presentation at `6b5d9e4d`. Generated runs use the existing 60-second loading allowance for the initial ready barrier, measured from the completion of host area construction on a real-time clock. Progress/ready packets cannot extend it. A timeout freezes the run, reports the failure to the host and connected guests, and requires leaving/restarting; it never starts combat with an unready party. A late acknowledgement cannot revive the failed run. Once initial readiness succeeds, later subset loads use their existing independent rules. Readiness acknowledgements also require a current connection epoch and unexpired authentication.

### Astra follow-up: command barrier

Initial party loading also blocks immediate Expedition commands, including inventory and vote changes, from both the local host and authenticated guests. Guest inputs are discarded during this hold. Once the initial barrier succeeds, normal per-area readiness controls apply again. Any stopped split run refuses further commands; blocked guest requests receive a rate-limited, transaction- and area-bound refusal without entering the command journal.

### Astra follow-up: fence lobby transfers at deployment

Lobby host promotion now checks the same eligibility at initiation and at every incoming prepare,
commit and redirect: no arena/session, no requested stage, and no locked deployment (either the
local lock or replicated ticket). A late lobby message cannot switch authority or redirect a
party that has already locked its generated run. Prepared/live acknowledgements and commit
messages also check the 45-second deadline directly, before the periodic timeout handler runs.
The native loopback fixture covers these refusals and retains the successful ordinary lobby
transfer/rejoin path. These guards do not implement active-run migration.

### Migration rules checkpoint (active-run transport still pending)

`PrepareHandoff` now freezes the shared run across all existing and newly obtained area
facades. It waits for area loading and member transfers to finish and runs only through the
world facade. The handoff carries the complete party rules snapshot plus every area's phase,
exit votes and deadline; returned arrays are detached. The runtime command and fixed-step
barriers honor the shared freeze. No candidate is advertised as active-run-migration capable.

`RestorePreparedHandoff(payload, authenticatedNextHost)` validates the schema, identities,
epoch/revision, area membership and votes, then restores a frozen rules authority. The caller
must authenticate and validate the complete package before using this API. Only a matching
world-facade commit releases it and extracts the departing host. Source abort resumes the
old authority; destination abort must discard the restored instance, never resume the old
epoch. A committed destination can become the source of a later transfer.

This is **not a complete active-run handoff**. Remaining work: serialize/restore authoritative
combat and physics in every area (including RNG, AI, cooldowns and projectiles), cohort/session
state and command journals, then implement endpoint reservation, fencing, digest acknowledgement,
commit and authenticated reconnect. Replica frames are not authority checkpoints. The existing
native deployment acceptance entry also includes the rules checkpoint's Unity JSON round trip
and command/fixed-step freeze fixture; it still requires actual Unity execution.

### Run deployment acceptance directly from this draft

`NativeBuildChecks.CheckDeploymentAcceptance` is an Editor menu/CLI entry point. It does not
build or publish a player, move a branch, contact live services, or require merging #303.
Use a separate checkout of `codex/deployment-completion`, the pinned Unity **6000.3.24f1**, and
leave Play mode. In Unity choose **Chip Arena > Check Expedition deployment acceptance**.
For Windows batch execution, from the repository root:

```powershell
$project = (Resolve-Path "unity/ChipArenaConversion").Path
$editor = "$env:ProgramFiles\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
$log = Join-Path $env:TEMP "chip-deployment-acceptance.log"
$run = Start-Process -FilePath $editor -ArgumentList @(
    '-batchmode', '-quit', '-force-d3d11',
    '-projectPath', ('"{0}"' -f $project),
    '-executeMethod', 'ChipArena.UnityConversion.Editor.NativeBuildChecks.CheckDeploymentAcceptance',
    '-logFile', ('"{0}"' -f $log)
) -Wait -PassThru
if ($run.ExitCode -ne 0) { throw "Deployment acceptance failed; see $log" }
if (-not (Select-String -Path $log -SimpleMatch 'DEPLOYMENT_NATIVE_ACCEPTANCE_READY:')) {
    throw "Acceptance did not finish; see $log"
}
git rev-parse HEAD
```

Keep the commit and log together. Do not pass `-nographics`: this entry point refuses a null
graphics device, exercises the station screen's no-fog/linear-fog passes synchronously, and
checks shader errors. The ordinary native build gate also checks the station shader import.

The suite checks seeds `1`, `17`, `0x80000000`, `0xffffffff`, each at rolls 1 and 2:

- All eight layouts: real collision/support and Hulk routes, spawn exits, join circles,
  composed-layout startup, deployment-ticket codec, late/reconnecting receiver reconstruction
  and rejection of an earlier unlocked ticket.
- Each first roll: the existing 1,800-tick split-area fixture, now using the generated bundle
  on authority and replica arenas, four occupied areas, transfers into all three instances,
  serialized state identity, shared clocks, encounter spawning and zone-boss replication.
- Existing loopback lobby promotion/rejoin checks, plus native component loading/command
  guard checks covering the exact timeout boundary and refusal after failure.

Logs identify every seed/roll/layout and include total time, per-area simulation/navigation/
physics timings, worst/p95 tick time and packet bytes. These measurements are for review;
there is no new machine-dependent FPS threshold. Physics mode, gravity, active scene, time
scale and cursor settings are restored on exit. Preview arenas are disposed even on failure.
A success marker proves this automated fixture completed, **not** a human co-op session,
finished generated-district visuals, acceptable real gameplay FPS, or active-run migration.

### Remaining acceptance and presentation work

The owner-authorized test update uses the pre-package native simulation gate above. It is not a fully accepted Windows release: station shader passes on a real GPU, real co-op join/leave/reconnect and initial-load failure handling, multi-seed gameplay performance and rendered acceptance remain manual follow-up. The build gate now includes generated-world native placements/routes, the full composed-layout startup validator and Unity codec round trips for both lobby and combined snapshots.

Claude's generated-district pass through `c08742fe` is now included in this draft, on top of
`3cdaa7b7`. The live adapter remains `Runtime/NativeDeploymentLive.cs`; it uses the actual
party and authority, with `ArenaPresentation.GeneratedRun` exposing each run's area, hub and
blueprint. District dressing is implemented; native rendered/performance acceptance is still
required. Later district fog/ambient colour, audio and plaza props are polish and do not block
this integration. Optional mutator effects remain deferred pending an agreed catalogue.

### Generated districts drawn (Claude, 2026-11-01)

Branch `claude/generated-districts` (from `codex/deployment-completion` at `a0fd8e8a`, shared merged in). Presentation
only (`Core/NativeExpeditionCityKit.Generated.cs`); no collider, record, route, timing or wire changes.

- The town kit now reads the generated map's ids in the town's terms (`NativeExpeditionCityKit.Stem`): buildings get
  the town's facades, fence sections the sanctuary's metal dressing, the gates their SANCTUARY headers and route signs.
  Before this the generated map drew as bare grey boxes (the kit keyed only on the authored town's ids).
- **District looks.** Each building takes one district's look, picked from `NativeDeployPresentation.Weights` at its
  centre (the shares squared, against a stable hash of its id), so a district reads as its own near its entrance and
  mixes toward the plaza with no seam. An unknown theme dresses as the town.
  - `industrial` (City Ironworks): brick, roller doors, hazard hems, rust sheeting, pipes, stencils, burning stacks.
  - `rail` (Central Station): cream tile, a teal band, the metro roundel, departure boards, cold lamps.
  - `quarantine` (Ward Zero): white cladding, cold ribbon lights, taped shutters, notices, red lamps and beacons.
- **Streets:** wall lamps in each district's light, kerbs, centre dashes and district ground marks (rails, a tactile
  strip, decon bands), all flat paint. **Entrances:** each frame clad in its district's look, its zone's name on a dark
  plate both ways, an accent light strip and beacons. **Final court:** blast panels, razor wire, floodlights, CHECKPOINT.
- **Runtime:** each generated run uses its own area, hub and blueprint (the first one used to stick for the session);
  the skyline is cut round the generated footprint (`NativeExpeditionCityScenery.Around`; the town's stood inside its
  north end).
- **Checks:** `Tests/GeneratedDistrictChecks.cs` (`--generated-districts`) on three real rolls: determinism, the town's
  part and caster budgets, every street building dressed, the blend (picks grow with a district's share; at least half
  within 30 m of each entrance; the town's look leads within 55 m of the plaza), the zone names, SANCTUARY and
  CHECKPOINT lettered, and the town's placement rules against the generated records.
- **Not done:** per-district fog or ambient colour as the camera moves, district audio, the camp's brazier and props
  in the generated plaza, mutator presentation (no effect list yet).

### Instance halls and the closed edge (Claude, 2026-11-01, blockout 2)

Owner playtest of the test update: each entrance "looks like just a gate", and pilots walked round it and fell. Human
pilots are not clamped to the bounds each tick (only bots, abilities and teleports use `NativeMapRules.Clamp`), and
every entrance's 3 × 3 forecourt ran to the floor's edge with nothing behind the frame.

- **Collision (`NativeExpeditionGeneratedWorld`, `Version` `deployment-world-blockout-2`):**
  - `hall-<instance>`: one 18 m block filling the forecourt's outer half from the frame (its face touches the posts'
    backs) to the floor's edge, 30 m across. The frame is its doorway; the door records, approach, join circle and
    returns are unchanged (all on the city side).
  - `edge-<x:z>` / `edge-z-<x:z>`: a 6 m wall along the outer side of any other street cell on the floor's edge (a
    seeded branch can reach it).
  - Both are static and carry the generated buildings' exclusion column.
  - The generator version change fences older staging tickets as before.
- **Dressing (`NativeExpeditionCityKit.Generated.cs`, stem `bldg-hall:<instance>`):**
  - The hall gets its district's facade on every face, with the doorway kept clear for the door leaves.
  - Its name stands about 1.7 m tall over the frame, with an accent strip and floodlights on the parapet.
  - Each district has its own crown:
    - Ironworks: two burning stacks over a sawtooth roof.
    - Central Station: a lit glass roof and a roundel mast.
    - Ward Zero: air handlers and four strobing beacons.
  - Edge walls are dressed as the checkpoint walls, unlettered.
- **Checks:** `--generated-districts` now requires, on every roll:
  - a hall per instance, at least 12 m tall, with its face on the door's plane and dressed;
  - every street cell on the floor's edge closed along its outer side.

  `--expedition-deployment` passes unchanged (3,681).

### Mutator catalog (owner request 2026-11-01; cards by Claude, effects for Astra)

Owner: "lets add some modifiers to roll for expedition; things like 'More money per kill, faster move speed, ALL hulk
zombies, etc.' Think fun things, overpowered things, insane difficulty things. Consider like... 30 modifiers?"

- **Drawn:** 30 cards in `NativeDeployPresentation` (`MutatorIds`, `MutatorOf`), 10 Boon, 11 Bane, 9 Chaos. The DEV
  preview rolls from them. Their names use only the card font's letters (checked).
- **Not applied:** the live room's `RollMutators` still returns false, and `NativeDeploymentLive` passes an empty list.
- **Astra to apply:** each effect below for the whole run (city and every instance, host and guests), the roll's
  exclusions, and the reward scaling. Then pass `NativeDeployPresentation.MutatorIds`, or the rules' own list with the
  same ids, to the live roll.
- **Numbers are proposals.** Change them freely; if a line on a card no longer fits, tell Claude.

| id | Card | Polarity | Proposed effect |
|---|---|---|---|
| `jackpot` | JACKPOT | Boon | Kill money ×2. |
| `overclock` | OVERCLOCK | Boon | Pilot move and sprint speed ×1.33. |
| `moon-boots` | MOON BOOTS | Boon | Pilot gravity ×0.5 (the `playerGravity` stat): about twice the jump height. |
| `bottomless` | BOTTOMLESS | Boon | Magazines never empty, no reloads. Stamina and magic costs unchanged. |
| `second-wind` | SECOND WIND | Boon | Reprints cost $0; downed revives take half the time. |
| `windfall` | WINDFALL | Boon | Cache prices ×0.5, restock ×2 as fast. |
| `vampire` | VAMPIRE | Boon | Heal 8% of damage dealt to zombies, at most 15 HP a second. |
| `adrenaline` | ADRENALINE | Boon | No stamina costs (sprint, rolls, melee). |
| `chip-rain` | CHIP RAIN | Boon | Each zombie kill: a 3% chance to drop a random common chip; brutes and Hulks 25%. |
| `headhunter` | HEADHUNTER | Boon | Headshot damage ×3 (pilots on zombies). |
| `hulkamania` | HULKAMANIA | Bane | Every spawn is a Hulk. Live Hulks capped per area (about 6) and the spawn interval ×2.5 for performance; a Hulk kill pays ×3. |
| `sprinters` | SPRINTERS | Bane | Shamblers spawn as runners; zombie move speed ×1.15. |
| `iron-hide` | IRON HIDE | Bane | Zombie health ×2. |
| `swarm` | SWARM | Bane | Spawn budget and live cap ×2, inside the existing crowd-cost bounds. |
| `paper-pilots` | PAPER PILOTS | Bane | Pilot maximum health ×0.5. |
| `permadeath` | PERMADEATH | Bane | No reprints. A downed pilot can still be revived; a dead one stays out until the run ends. |
| `darkness` | DARKNESS | Bane | Presentation (Claude): street and district lights off, ambient −60%, weapon lights only. Rules: none, or zombies sense pilots 20% farther. |
| `whiteout` | WHITEOUT | Bane | Presentation (Claude): fog to about 25 m, markers hidden past it. Rules: none. |
| `broke` | BROKE | Bane | Kill money ×0.5, cache prices ×2. |
| `bloated` | BLOATED | Bane | Every zombie death bursts like a bloater: damage and knockback to pilots in its radius. |
| `brittle` | BRITTLE | Bane | Destructible block durability ×0.25. |
| `surge` | SURGE | Chaos | Pilots ×1.25 and zombies ×1.35 move speed. |
| `storm` | STORM | Chaos | Every 20 s, 3 strikes near random pilots, telegraphed 2 s: 60 damage within 3 m to anyone, zombies too. |
| `glass-cannon` | GLASS CANNON | Chaos | Damage dealt by pilots ×3 and taken by pilots ×3. |
| `high-explosive` | HIGH EXPLOSIVE | Chaos | Every shot's impact makes a 1.5 m blast at 35% of its damage; self-damage at half. |
| `bounce-house` | BOUNCE HOUSE | Chaos | All knockback ×3, pilots and zombies. |
| `friendly-fire` | FRIENDLY FIRE | Chaos | Pilots damage teammates (at 50%); kill money ×1.5. |
| `roulette` | ROULETTE | Chaos | Every 90 s each pilot's weapon becomes a random one from the run's pool, fully loaded. |
| `martyr` | MARTYR | Chaos | A pilot who goes down explodes: 4 m, 150 damage to zombies, 30 to pilots. |
| `big-heads` | BIG HEADS | Chaos | Head hitboxes ×2 radius for pilots and zombies. Presentation (Claude): heads drawn ×2.2. |

- **Roll rules (proposal):**
  - Never draw together: `jackpot` with `broke`; `windfall` with `broke`; `second-wind` with `permadeath`;
    `darkness` with `whiteout` (both dim the view; it reads as a broken screen).
  - Three Banes may land: that is the owner's "insane difficulty".
- **Reward (proposal):** each Bane +25% run payout, each Chaos +10%, Boons none. Show it on the station
  (Claude, once the rules give a number).
- **Claude's follow-ups once the rules apply them:**
  - DARKNESS's and WHITEOUT's lighting and fog;
  - BIG HEADS' head scale;
  - STORM's telegraph and strike, the HIGH EXPLOSIVE and MARTYR blasts, BLOATED's burst (reusing the bloater's);
  - the active mutators on the run HUD.

### Slot sparks and sound cues (Claude, 2026-11-02)

The owner asked for "spark effects when rolling and landing on things", with a different effect when a card helps the players
or screws them. Core `NativeDeploySparks` derives every spark from this client's roll clock (the reels' own), so a late
joiner sees none. `NativeDeployStage` draws them as camera-facing additive quads after the station's own items (at most 160).

| Moment | What it looks like |
|---|---|
| A reel spinning | Grinder sparks: a thin stream at cruise, a spray while the brake bites (StopTime − Decel to StopTime). The instance machine's are hot orange off each window's lower lip; the mutator dials' are short blue-white crackles off the hubs. |
| A destination landing | A burst off its window's rim in the destination's colour, a flash in the window and a thump of the cabinet light. |
| A BOON card | A fountain of gold and green sparks with slow glints rising after it; the cabinet brightens. |
| A BANE card | A short circuit: red sparks spray out and fall hard, arcs crackle round the card, and the flash and cabinet stutter. |
| A CHAOS card | A violet vortex spinning out of the dome (alternate cards turn opposite ways), colours cycling violet, magenta, cyan. |
| All three cards one polarity | The sign over the dome erupts at `SweepAt` (StopTime(2) + .12 s) in that polarity: a jackpot, or a disaster. |
| Reduced effects | About a third of the sparks; no arcs; no stutter (the bane dip eases out instead). |

- **Sound (Astra, owner 2026-11-02: "We can have astra simulate sound effects"):**
  - `NativeDeployStage.Cues[0..CueCount)` lists each frame's roll moments, each heard exactly once.
  - Each cue has a `Kind`:
    - `Spin`: the lever and motor;
    - `Brake`: a reel's brake starts biting, at StopTime − Decel;
    - `Stop`: the reel's clunk;
    - `Card`: the mutator card's sting, carrying its `Polarity`;
    - `Sweep`: three of one polarity, carrying it.
  - Each cue also carries `Machine` (0 instance, 1 mutator), `Reel` (-1 for the whole machine) and `At` (seconds after the
    roll was seen).
  - The schedule is `NativeDeploySparks.Schedule`. Astra's `NativeDeploySounds` / `NativeDeployAudio` now plays it immediately after `NativeDeployScene.Draw`, in live and DEV stations.
- **Sound implementation (Astra, 2026-11-02):** twelve original 48 kHz procedural clips, cached in memory; no new audio assets. Destination lever/motor/brake/clunk are warm metallic tones; mutator versions use higher electric tones and crackle. Boon rises with bell/coin harmonics, Bane descends with a short-circuit buzz, Chaos uses frequency-modulated warble; each polarity has a longer sweep sting. Stops vary slightly by reel, with left/centre/right stereo pan. The motor bed loses one third as each reel brakes and ends at the last stop. No cue time or visual changed.
  - Uses `NativeAudio.Effects`, `Muted`, the existing `NativeSoundLimiter`, shared 24 sources and eight gated-voice cap. Active station voices follow effects-volume changes, stop on mute/leave, and a reroll stops that machine's previous sounds. Unmuting does not replay consumed cues.
  - Keys: `deploy.destination.{spin,brake,stop}`, `deploy.mutator.{spin,brake,stop}`, `deploy.{boon,bane,chaos}.{card,sweep}`. Unknown card polarity falls back to its machine's stop sound; no invented benefit/penalty tone.
  - `--deploy-sounds` checks all cue routes, late-join/new-roll clock behavior, once-only scheduling, stereo pan, deterministic bounded PCM/envelopes and limiter wiring. Full checks and native test publication receipt: newest DEV_LOG entry / audio PR.
  - Live mutator rolling remains gated off in this sound-only release; its sounds are available in the DEV preview. Mutator gameplay is separate draft PR #305.
- **The card faces:** a dial's 31 tiles used to rasterise on one worker per dial (9–14 s in the Editor) and drew white until
  then. `NativeDeployScene` now rasterises each tile once, in parallel. The dials share the jobs, and a strip that isn't
  ready draws a dark blank. All six strips are ready about 2.6 s after the station opens.
- **Checks:** `--deploy-sparks` (`Tests/DeploySparkChecks.cs`).

Active-run endpoint migration remains unsupported by the existing live Expedition transport (candidates remain non-capable); staging host promotion preserves the ticket. This change does not claim or enable active-run migration. The owner-authorized test update may ship with active-run migration disabled; full migration remains separate unfinished work.

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


## Astra room integration — 2026-10-31 (draft, not the completed feature)

Branch `codex/expedition-deployment-integration`, based on foundation `0f23c87b` (PR #302).

Implemented:
- `ExpeditionDeploymentRoom` chooses its pool from the selected, unlocked Expedition catalogue and the loaded layout/maps. Missing interiors, nav nodes, bosses, mismatched area/map IDs and missing compatibility fail closed. The final boss never enters the instance pool.
- `ExpeditionDeploymentTicket` carries schema, station, original host, seed, draw index, revision, expedition and content/generator compatibility. It carries no client-selected instances or cells. Receiver reconstruction is bounded (one deterministic draw, never replay N rolls), rejects rewind/content mismatch and returns detached snapshots.
- Revision-checked host intents reject duplicate rolls. The room wrapper rejects all mutator rolls until an effect catalogue is approved; skipping is valid. Core deployment additionally requires a generated-content digest and trusted readiness. The runtime intentionally exposes no Deploy method yet: a digest string alone is not geometry validation.
- `ConversionGame.BeginDeploymentStation(expeditionId)`, `RollDeploymentInstances(expectedRevision)`, `SkipDeploymentMutators(expectedRevision)` and `DeploymentState` are the presentation integration API. No menu calls Begin yet. Begin creates no arena, PvE clock, kills or rewards. Leaving the network resets this staging state.
- The host broadcasts a compact `deployment-state` envelope with lobby refreshes, including reconnect/late join. The `deploymentProtocol=1` hello capability and verified account are required while staging; old fixed-M1 room protocol/schema are unchanged. Guests have no roll authority or client-supplied selected-ID path.
- Lobby host promotion freezes rolls through the existing PromotionBusy gate, transfers the bounded ticket in its prepare transaction, validates it before preparation succeeds, then rebinds the same draw stream to the new authenticated host at commit. Missing/incompatible handoff aborts. This is lobby promotion, **not active-run endpoint migration**.
- Fixed M1/ordinary match starts cannot bypass an open station. Closing the room clears staging. Existing M1 runs without a station retain their old path.

Still required before activation:
1. A full procedural outdoor generator: real buildings, entrance shells, scenery, sanctuary/gates, supported surfaces/collision, caches/spawns, nav and a separate final arena. Apply all static city geometry/LOS/nav/clearance gates over many seeds. The cell blueprint is still only a blueprint.
2. Generated map/layout loading throughout host split areas and guest reconstruction; replace the hardcoded resource reads with a per-run validated manifest. Bind its digest to the ticket, actual run handshake and active-run transfer. Lock before creating/ticking PvE. Preserve fixed M1 schema fallback.
3. Actual readiness-to-Deploy transaction and generated-content validation. No callable runtime deployment is claimed by this patch.
4. Claude's room, machines, previews and entry/exit controls. No station scene or artwork changed here.
5. Approved mutator effects/compatibility/rewards, native JsonUtility/transport tests and Windows multi-client preview. No guessed effects or multipliers were introduced.

Verification: focused managed deployment checks pass (49); pinned Unity 6000.3.24f1 Player, Runtime and Editor semantic compilation passes. Full managed/relay regression passes: 884 PASS lines, exit 0; receipt is in the latest DEV_LOG entry. Unity execution, PhysX, Windows build, real multi-client staging/promotion and active-run migration are unrun. No merge, builder synchronization or test-feed publication; this is a draft continuation, not a playable release.

## Generated outdoor collision blockout — continuation (2026-10-31)

`NativeExpeditionGeneratedWorld.Build(room)` now consumes the authenticated room's deterministic roll and produces a fresh, engine-free `ContentMap`, world `ExpeditionAreaLayout`, hub gate/zone/route records and theme blueprint. It does not mutate shipped content or start simulation. `ComposeLayout(world, authoredJson)` combines that world with newly parsed copies of exactly the selected authored interiors.

- 250 × 190 m flat supported outdoor footprint; seeded streets, solid building footprints and roof exclusion columns. A clear apron connects the seeded streets to all four sanctuary gates.
- Three entrance shells with eight return slots and a clear joining circle each. The fixed northern final-boss court has a separate gate and does not consume a machine reel.
- Existing host-owned hub gate records, spawn exclusions, street caches and encounter zones. Routes follow the generated graph; zone distances are measured from gate exits. No encounter balance, mutator or reward rules were added.
- A 2 m navigation graph with collision checks for the Hulk-sized capsule; generation rejects isolated graph pockets or unsupported/unreachable gameplay points. These are blockout checks, not proof of native PhysX or the full static-city acceptance suite.
- Stable ordering, invariant numeric formatting, and a versioned geometry/placement/theme fingerprint. The generator version participates in staging compatibility. Reconstructing the same ticket after host reassignment produces the same map; locale cannot change its fingerprint. The fingerprint is **not** a runtime permission to deploy.
- 68 seeded builds, independent entrance/join collision probes, authored-interior preservation, reconstruction and bounded map budgets are covered in `ExpeditionDeploymentChecks`. `CheckGeneratedDeploymentWorld` adds a real-Unity builder gate for native collision, Hulk routes, spawns and join circles; it has only been compiled here.

**Still dormant and deliberately not enabled in menus.** The next Astra step is a per-run map/layout/hub bundle used by every host split-area and guest loader, replacing fixed-resource reads for generated runs while retaining the old M1 path. Bind its full identity into the start/ready/transfer handshake, then implement the atomic readiness-to-Deploy transition before any PvE clock starts. Complete native multi-seed, dynamic-gate, LOS, cache/encounter, performance and Windows co-op acceptance before release. Add physical machine fixtures and presentation with Claude; this blockout currently has cache interaction anchors, not finished city dressing or machine art. Runtime `BeginDeploymentStation` still does not generate/load this blockout automatically. Approved mutator effects remain outstanding.

Verification: managed deployment 3,664 checks; stage/Training regression 5,076 checks; pinned Unity 6000.3.24f1 Player/Runtime/Editor semantic compilation passes. Final full-suite and integration receipts are in DEV_LOG. No live Deployment Station, native Windows build or test-feed publication claimed.
