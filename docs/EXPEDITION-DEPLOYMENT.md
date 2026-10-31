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
