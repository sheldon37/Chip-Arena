# Chip Arena — current state

2026-10-12 melee Q/E presentation: `claude/melee-qe` (base `fdd1914`, merged to shared `a77edc6`) adds the visuals for Astra's eight melee Q/E abilities, driven only by replicated kit state and events. It adds per-phase pilot poses (third person and first-person arms), per-weapon VFX (telegraphs, dashes, decoy, mark, shield flight/bounces/absorbs, wall stun, fissure strip, super armour, landing waves), audio on existing bank keys, and Q/E HUD cards (recast window, cooldown, stamina). Presentation only. On the branch; not merged or released. Simulation notes for Astra are in DEV_LOG.

2026-10-12 melee/jump handoff: `codex/unity-melee-followup-20261012` on merged death repair `3bb8995f` implements Claude's dagger air-jump simulation/prediction and explicit replicated flip signal, plus S0's corrected native melee sweep direction. Landing resets the one-air-jump budget; locks/lava remain separate; obstructed dagger bots can use it near the apex. Original contact fixtures are reflected into native coordinates. No optional balance tuning before native duels. See MELEE-FEEL, DEV_LOG and task PR for actual checks/integration; Unity/Windows acceptance and compiled publication remain pending.

2026-10-12 death-results repair: `codex/unity-death-results-20261012` on cloud `2c45d052` fixes PR #125's immediate cleanup on the winning kill, which suppressed the death animation/body entirely. PvP bodies now persist through the full five-second results period and clear at the following countdown, draft or match screen. Brickfall floor/stair/rubble support remains in place; summon expiry and PvE revival stay separate. Managed lifecycle regression reproduces the old failure; Unity/Windows visual acceptance and compiled publication remain pending. See DEV_LOG and task PR for checks/integration receipts.

2026-10-12 jump animation: `claude/jump-anim` (base `fdd1914`) adds per-weapon pack jumps (push-off, rise/apex/fall by vertical speed, fall-scaled landing) for every route and the dagger double-jump presentation (flip, air burst, whoosh), detected from replicated state; the double-jump rule itself is handed to Astra in `docs/MELEE-FEEL.md`. Merged into the shared Unity head; not released; see DEV_LOG.

2026-10-12 melee simulation: `codex/unity-melee-abilities-20261012`, base `19c00c7` with shared `c5d9d0a` integrated, implements all eight Q/E abilities, owner's exact primary chain/turn/step values, LMB edge buffering, combo reset, guest swing prediction, bot approach, block Detail/contact and account cosmetics compatibility. Builder v4 already installs LocalAssets before building (verified). Merged as PR #124 (`374964c`); native source and Windows updater CI passed, legacy Electron Checks retains its documented failures. Native duel launch is blocked by absent Unity Editor; Windows/two-client acceptance, remaining Claude Q/E presentation, account deployment and compiled publication are pending. See MELEE-SIMULATION-HANDOFF and DEV_LOG for exact verification and source integration receipts.

2026-10-12 build validator repair: `codex/unity-fix-flash-step-validation-20261012` on cloud `374964c` corrects the gun/magic gate accidentally sending new melee abilities through the mana caster. The eleven gun/magic checks remain; all eight melee Q/E now also exercise their real input dispatcher, stamina commitment and release. Managed/relay (379 PASS), Unity API compilation and source packaging pass; full Editor/Windows rebuild and compiled publication remain pending through local Build-and-Publish. See task PR/DEV_LOG for verification and source integration.

2026-10-12 melee feel: `claude/melee-feel` (base `19c00c7`) adds `docs/MELEE-FEEL.md` (evaluation of the owner's melee fluidity, gap-closing and torso request, and Astra's simulation handoff: LMB buffer, chain points, windup turning, step magnetism, combo reset, guest prediction; the original baseline had no melee Q/E kit; implementation is now in the simulation task above), steadies the melee walk/run torso and makes the captured swings fluid: the sweep plays in the hit window, follows through into the real recovery, a turned swing eases in, and the slash crescent follows the blade (presentation only). It also hands Astra a mirrored hitbox sweep (S0, still open). Merged into the shared Unity head; a follow-up on the branch only adapts the swings to the native feel (chain entry from the follow-through, turn following, anchored lunge/sprint footwork). Unity Editor/Windows play not run. Not released; see DEV_LOG.

2026-10-12 PvE chips: `codex/unity-pve-chip-pickups-20261012` on shared `1627b0c` gives every PvE pick three choices, including rainbow, and collects ready drops automatically only for their living, connected owner. Choices stay queued for C; F remains station/revive interaction. Logic harness, managed/relay, Python, Unity API compilation and source packaging pass. Editor/Windows/two-client play and compiled publication remain pending through local Build-and-Publish. See DEV_LOG and task PR for receipts.

2026-10-12 room start: `codex/unity-host-start-20261012` on shared `19c00c7` removes the hidden all-ready automatic start for player-hosted rooms. Ready now enables the host's Start button; the existing host-side readiness guard remains. Old saved auto-start values are ignored. Source logic checks pass; Unity/Windows and two-client acceptance plus compiled publication require the normal local Build-and-Publish. See DEV_LOG and task PR for source integration and CI receipts.

2026-10-12 entrance pilot motion: `claude/intro-melee` (base `512348d`) drives every weapon signature but the daggers (sword, greatsword, shield, guns and the four magic schools) from the owner's Explosive pack (`tools/export_pack_intros.mjs`, `Core/NativeEntrancePack`); daggers stay baked. Managed/relay 376 PASS, Python 82 (one skip), Unity API compilation and source packaging pass; Unity Editor/Windows play not run. Not pushed, merged or released; see DEV_LOG.

2026-10-12 guest screens/music: `codex/unity-guest-screens-20261011` on shared `b320737` fixes held-clock Siren frame ordering and empty-string PvP choices, and replaces native minion-wave music with the owner's Ward Zero track. Boss music stays unchanged. Hover fix `f288493` is already on both shared source refs. Managed/relay (346 PASS), Python (82 tests / one skip), Unity API compilation, asset decode and source packaging pass; native Editor/two-client/listening checks and compiled publication remain pending through local Build-and-Publish. See DEV_LOG for the causes, compatibility and verification limits.

2026-10-11 Brickfall music: the owner-supplied “Brickfall Run” is added on `codex/unity-brickfall-music-20261011`, based on shared `411c696`, filling Brickfall's existing music slot. The complete MP3 and credits travel with normal builds; no LocalAssets input is required for this song. Asset hashes/source packaging and audio decoding pass. Unity import/listening and compiled publication still require a successful Windows Build-and-Publish; see the task PR for source integration receipts.

2026-10-11 online rooms: owner selected automatic Cloudflare tunnels (free) and requested the test Update release. Task `codex/unity-cloudflare-rooms-20261011` supersedes PR #118's Render/shared-service plan. Hosts launch a bundled, verified connector; guests use six-character codes through the existing directory; gameplay stays on the host PC. No paid service or Site/account deployment. Merged-tree managed/relay (340 PASS), Python (82 / one skip), Unity API compilation and source packaging pass. Windows public-tunnel CI (38176955364) and native/Windows updater CI (38176955384) passed. Shared effects work through `af13686` is included; shared-source push and compiled Windows Build-and-Publish remain pending; see DEV_LOG and Relay/README.md.

2026-10-11 Unity effects audio: `claude/fx-audio` adds the data-driven effects sound bank and its hooks. The licensed clips live in the owner's private licensed-assets repo and install into the git-ignored `Assets/LocalAssets` (`tools/install_local_assets.py`). Without them the game sounds exactly as before. Builder v4's pre-build installer and cache invalidation are verified on 2026-10-12 (191 keys / 18 entrances); put the private LocalAssets.zip beside Build-and-Publish.cmd. Owner listening/gain tuning and compiled publication remain pending.

2026-10-11 actual Unity result: owner's `ec0c058` log passes every native stair/ramp/clearance gate (25 ramp variants, three Brickfall flights, host/guest walk/sprint, idle, descent, jumps, walls, low/passable ceilings and ledge gravity). Build then fails in the DEV combat fixture's held-E recovery check: fighters are placed outside the map with no floor. `codex/unity-combat-fixture-floor-20261011` adds real isolated support and probes it, preserving all ability assertions. Exact editor-only fixture compiles against Unity APIs; managed/relay 323 PASS, source validation pass. Full Unity/Windows rebuild and compiled publication remain pending via the owner's local builder. Separate builder v3 diagnostics work adds fixed logs and automatic error reports; see DEV_LOG/task PRs.

2026-10-11 stair clearance follow-up: the owner's `c877db5` Unity run passed the earlier traversal assertions and reached a low-ceiling failure. `codex/unity-stair-clearance-20261011` makes a refused step constrain host/guest horizontal movement and distinguishes rounded tread-edge hits from true ramps. Existing native gates remain; clearance now checks host/guest walk/sprint refusal and permitted headroom with per-tick diagnostics. Managed/relay 323 PASS, Unity API compilation, source validation and 124 separate PhysX WASM trials pass. Those trials are not Unity native acceptance: the Windows Build-and-Publish run and compiled test update remain pending. Shared builder target: `codex/unity-cloud-current`; latest delivery receipts in the task PR/DEV_LOG.

2026-10-11 stair build follow-up: the owner's actual `07f9d6e` Unity log failed in the new locomotion gate because its one-seat match resolved and reset, destroying the pilot during traversal. `codex/unity-stair-gate-lifetime-20261011` temporarily holds round resolution inside that fixture and restores it afterward; all native traversal checks and gameplay rules remain intact. Managed regression reproduces the reset and verifies 180,000 stable held ticks plus resumed normal resolution. Unity API compilation and source validation pass; real Unity/Windows rebuild and compiled test publication remain pending. Builder target remains `codex/unity-cloud-current`; see the task PR and DEV_LOG for check results and the local Build-and-Publish handoff.

2026-10-11 stairs: `codex/unity-stairs-fix-20261011` on `b48b075` repairs shared host/guest ground movement and the stationary-collision-to-knockback feedback that drove players downhill. Handles the 228 legacy stair ramps and Brickfall treads with slope support and bounded step clearance; keeps maps, movement stats and airborne combat impulses. Managed/relay (321 PASS), Python (74 pass / 1 skipped), Unity API compilation and source packaging pass. A new real Unity stair/clearance gate must run in the Windows builder; that runtime acceptance and compiled publication remain pending. The latest observed cloud build request failed HTTP 500; use the existing owner local Build-and-Publish workflow if it remains unavailable. Details: DEV_LOG.

2026-10-11 owner build repair: `build(5).log` on `2bcb646` failed in `NativeMapRules.Spawn` before player packaging. Branch `codex/unity-spawn-build-fix-20261011` normalizes JsonUtility-created empty supplemental fields on legacy maps, preserving their global spawn/bounds/navigation behavior while keeping Brickfall validation strict. The regression reproduces the original crash before the fix and passes afterward (15 maps / 240 spawn selections); full managed + relay checks have 320 PASS lines, runtime API compilation and 1,140-file source validation pass. A native JsonUtility gate was added; the Windows rebuild remains pending. Builder source target: `codex/unity-cloud-current`. No new compiled update from this repair yet; see DEV_LOG.

2026-10-11 Brickfall integration: owner authorized connecting PR #109's simulation to Claude's current kit and updating the build source. `codex/unity-brickfall-integration-20261011` merges simulation `8c00940` into shared base `beaacc7`; intended shared/build heads are `claude/sleepy-ritchie-g8ruhv` and `codex/unity-cloud-current`. Map, static/carried/rubble dressing, authored hp, shared arm lock, collapse VFX/audio, segment warnings and COLLAPSE announcements are connected. All 14 rotation maps and Ward Zero remain; Brickfall is manually selectable and `rotation:false`. Available source/managed checks are recorded in DEV_LOG; Unity Editor/PhysX, Windows playtest, real packet budgets and G1/G2/G3 still require the owner's local build. This is a source update, not a new published player. Latest observed native test feed before this integration: channel `20b03b8`, build `94469f03347c59a6a4e419d3a4eda5ae4eeceebd3e3805379adc284c7e1eaa25`; older release entries below are historical.

2026-10-10 current verified native release: **UBA #23 / 4c568429**, source `efb4fa4017e68b12dbfda2db166e52f397913539`, published through Update; public downloads verified at 18:15 UTC (receipt commit `7db75dd61f5ce2e5feefba25800644ecb4c62c91`). Includes the Siren music-clock repair; Windows listening acceptance remains unrun. This task branch restores the Siren victim animation and PvE holographic regeneration; native CI, the Unity build and publication are pending. See DEV_LOG and the task PR for the release receipt.

2026-10-09 14:52 EDT current native release: **UBA #8 / 80b8edb9**, source `603ca76b69b2256d58b24abc4ac23166306e1fd4` (account PR #81 + test-isolation #83). Published through Update and all public downloads verified; receipt `b4462d36e4cc63625a48487b9ad6eb5a1de5184f`, publisher run `37974643037` attempt 2. Native CI, real Unity 6000.3.24f1 / Linear and delivery gates passed. Account service configured automatically, old blank URLs migrated once, URL field hidden. All account APIs, profile fields, signed grants and existing records retained. Sheldon already has DEV and the supplied credential matches; Windows sign-in/save/reload remains the owner's runtime check. See UNITY-LOGIC-HANDOFF and DEV_LOG.

Automation main PR #82 (`f6137be`) now starts a missing build pinned to the verified cloud head; real API start produced #8. Push completed game batches to `codex/unity-cloud-current`; task-only branches are not published. Existing target/settings/permissions unchanged; active builds are not duplicated and failed builds are not retried automatically. Main's publisher validates before updating the existing native test feed. Prior #7 / 3686f40e was owner-confirmed working. Legacy Electron CI still has its documented 12 unit failures and TypeScript build errors.

Cloud/native update checkpoint (2026-10-08 23:14 EDT): owner-approved UBA build #4 (`chip-arena`), compiled source `51841828e142c551b521ca6698d7487390cd4880`, is **published** as version `659a376e`, revision `639271111007548341`, channel commit `1d4b4f62762698f0c3e9f01d114dcbc29574f1df`. Build passes 86 Unity checks and complete cloud delivery validation; the public main-branch manifest and all 12 parts were downloaded and passed full hash/CRC/inventory/identity validation. Previous native objects and Electron feed are preserved. At 23:14 EDT the owner reports the installed update worked: the first cloud-build-to-Update workflow is owner-confirmed. Installed identity, retained settings and full Windows gameplay are not independently verified. No further local-PC build or starter install is required. Source branch `codex/unity-cloud-current` / PR #68 and cloud/native handoffs contain full receipts; implementation `82ddcfb` passed native CI `37874907141`. No source merge or Site/live release; cloud triggering/retrieval/publication remain separate actions.

Current Unity Windows/update checkpoint (2026-10-08 20:21 EDT): the owner's `build(4).log` records 86 PASS lines, all 15 maps / 120 spawn floors, successful Windows player build, native feed and starter ZIP preparation, and final exit 0 in Unity 6000.3.24f1. Reported source is shared `5900583` plus draft PR #67's cumulative v3 patch (`9903a66`), branch `codex/unity-startup-build-check`; the uploaded candidate package and matching build identity now pass checksum/inventory validation (version `83e6a566`, revision `639271006350008324`). Source provenance remains owner-reported because the log does not embed the Git SHA. Owner reported the candidate looked good and approved publication. Native test feed is now published at channel commit `c5251fb`; the public manifest and all 12 download parts were fetched and the full player archive passed checksum/CRC/inventory/identity validation. Owner now reports the older installation successfully updated. Installed-identity, settings retention, repeat-update and rollback checks are not independently verified. Future ordinary releases can use this same feed/installation; cloud build #4 now passes export, packaging and downloaded delivery validation as recorded above. A12 build/packaging is passed; rendered gameplay, two-client/services and the remaining acceptance matrix are still open. See DEV_LOG, `docs/UNITY-PARITY-GAPS.md` and `docs/UNITY-LOGIC-HANDOFF.md`.

Claude visual parity batch (2026-10-09): `claude/sleepy-ritchie-g8ruhv` (PR #62, head `4654059`) holds PR #66 plus C1-C5, C6/C7, C9-C11, C14 (live pilot portraits), C17, C18, C20 and C22, each reviewed and merged on the branch; Unity Editor/Windows not run. Paused by the owner to save usage; open items and Astra data requests: `docs/UNITY-PARITY-GAPS.md` (progress list) and `docs/DEV_LOG.md` (2026-10-09 entries). Next: owner Windows build and playtest of the branch; resume C6b/C8/C12/C13/C16/C19 when the owner asks.

Historical migration/runtime context follows.

Migration imported to private GitHub; not merged or released (2026-09-29).

- Destination: `sheldon37/Chip-Arena`, private. Owner pushed the verified baseline
  and migration branch; remote SHAs match the history bundle. Browser audit found
  no publishing hooks, environments or Pages; only the Codex Connector app.
  Ruleset API returns a private-plan restriction; no admin policies changed.
- Source baseline: `18231698c7c79a7a604e567b933eb2d0155d7a3e`, clean `main`,
  68 existing commits; matches latest successfully published Site source (version 64).
  Work branch: `migration/shared-development`; full history retained.
- Runtime: TypeScript/Three.js/Rapier; Node authority; Electron shell
  `desktop/main.cjs`. Launcher 0.3.6; pinned Electron 44.4.5, Node 24.21.0,
  cloudflared 2026.9.3. Migration checks use Node 24.19.0/npm 11.9.0.
- Baseline game build `9b6caf05644a73c2cf5929da`; content `0.2.4cad5cbfa762`.
  Available Windows ZIP contains older content `0.2.44211e218269` with the same
  launcher. The owner's installed, possibly updated, copy is not accessible.
- Existing Site still serves updater downloads, identity, room discovery and relay.
  Its database and endpoints remain unchanged. Production still uses its original
  updater feed; TEST installations use the separate GitHub channel below.
- Settings use `chip-arena-preferences`; desktop uses persistent
  `arena://game` storage and an encrypted account-session file under userData.
- Baseline: typecheck passed; unit tests 406/419 passed (13 failed);
  identical after clean restore. WebSocket rematch/server build remain failing;
  HTTPS integration and Windows ZIP construction passed. Native Windows untested.

- Shared test-development branch: `claude/visual-revision` (owner confirmed
  2026-10-01). Visual revision PR #4 remains separate from the migration to main;
  this branch is already distributed through the test channel.
- Test builds update online from the public test channel
  sheldon37/chip-arena-test-updates (tooling PR #5); live feed untouched. See DEVELOPMENT.md.
  Latest test update (2026-10-06): `0.2.e337f51c003d`, source `a0167be` (PR #57),
  channel `5ddab5b`: wave completion clears surviving enemies and queued arrivals, excluding bosses and friendlies.
  Prior impact-lifetime, added-projectile, fourfold Siren Find-me horde and card UI changes are included.
  Actual network upgrade, installed hashes, settings, same-version skip and menu status pass for both updater schemas.
  Baseline test failures and native Windows/GPU/difficulty acceptance remain; see DEV_LOG.
  Update headroom (owner-approved 2026-10-03, merged with PR #33):
  test schema 1 up to 32 MiB; updater v2 (schema 2, launcher self-update) needs one new
  seed ZIP (no source archive), delivered as an owner-only GitHub Release asset on the private
  game repo, tag `test-seed-<content version>` (procedure in tooling PR #5).
  Standing owner instruction (2026-10-01): publish completed game updates to this
  existing test Update channel unless told otherwise; no repeat publication approval.
- DEV combo-ready holds and Demon Step's 0.20 s i-frame window (PR #6), plus
  individually controlled primary hits (PR #7), merged into the shared branch
  at `66451cd`. One hit per activation, per-hit stamina, 2.4 s continuation memory.
  Included in the current Siren test update below. DEV_LOG records checks and
  the 12 unchanged baseline unit failures.

Next: review the migration PR against `main`; shared policy and CI remain on
`migration/shared-development` until the owner merges. Resolve recorded baseline
failures before enforcing a green merge/release gate. No automatic merge.
Windows validation and desktop distribution cutover remain separate work.

- Siren: PR #8 merged into `claude/visual-revision` at `9f010f5` (2026-10-01),
  combining systems `1a8a5d0`, Claude visuals `2e986f4`, DEV-only `???` category
  and original music. The merge tree matches the tested integration exactly.
  Follow-up PR #11 merged at `fd67313`: Claude's silent smile/Last Laugh cutscene
  `452bbe6`, removed voice hooks, 15,000 base HP, verified-DEV bot weapon choices
  and bot ability use. Test content `0.2.c452eefcbbac` published from `fd67313`
  at channel `79e939f`; existing test installations press Update. Windows
  visual/audio acceptance remains. Publication verification is recorded in DEV_LOG.
  See `docs/SIREN.md`, `docs/SIREN-SYSTEMS.md` and DEV_LOG.
- Owner-authorized integration on `codex/chip-cards-bot-draft`: Claude's card
  redesign/three Siren Chips (`1d16c68`), isolated Siren team preservation
  (`a93d9eb`), and opt-in Skip bot chip choices (bots still receive Chips).
  Targeted local/network checks pass; full units retain exactly 12 known failures.
  Merge into `claude/visual-revision` and existing test-channel publication next.
  See newest DEV_LOG entry for actual checks and unrun Windows/visual acceptance.

- Body damage systems: draft PR #14, `codex/body-damage-systems`, shared base
  `a2480ba`, initial handoff `fdaf42c`. Authority wound outcomes, public default
  impact-ragdoll and collapse timing are implemented on the task branch.
  Claude's wounds, sever/crush and kneel/fall visuals (`afb7561`, from `2f148ad`)
  are integrated into PR #14 with Astra's real-Rapier adapter fixes: no legacy
  impact spin and an exact pose/velocity handoff at .833333 s. Real-Rapier tests
  pass; final units 664/676 vs complete base 646/658, identical 12 failures.
  Types/client/relay builds pass; server build retains four errors. Local lab
  visual acceptance is blocked by Cloud Browser (`ERR_BLOCKED_BY_CLIENT`).
  See newest DEV_LOG and BODY-DAMAGE-HANDOFF for checks and remaining visual review.
  Owner subsequently authorized merge/publication with these recorded limits.
  PR #14 merged at `25599c4052af5f3f50457ea7491dcb01b6216076`; test content
  `0.2.c99e20077a04` published at channel `8bf843b2963b0575e6a13e41c10afcfa0e7fcca8`.
  See the publication DEV_LOG entry for updater verification. Native visual review remains.

- Kill-cam / Siren armor: `codex/killcam-siren-armor` from shared `4dbb1513`.
  Automatic short local death replay, skip/return-to-spectate, and physical
  interruption resistance during Siren's numbered abilities are implemented.
  Focused/network checks pass; broad checks retain baseline failures. With Claude's
  final replay UI/shoulder camera, merged into `claude/visual-revision` at `7d31530`
  (PRs #17, #16). Test content `0.2.1aeb4fad8c90` published at channel `012d37b`
  (owner request); native Windows/GPU and audio review remain.
- Full-screen 3D chip draft (pilot looks at and grabs the chip) and a 1.2 s kill-cam pre-roll merged at
  `a9269ff` (PR #22); test content `0.2.0a02ae6804bc` at channel `bc84529`. Native review remains.
- Ward Zero rebuilt as a sealed, music-synced asylum hall with hooks for future adds and chip pads, merged at
  `5af891c` (PR #24); test content `0.2.58d079f38769` at channel `d5f3714`. Native look/frame-rate review remains.
- ROUNDS-style chest-up chip draft and Siren minion design (zombie Pilots, not spawned yet; docs/SIREN-MINIONS.md)
  merged at `fd2f745` (PR #26); test content `0.2.177d472cf940` at channel `7a9ff3d`. Native review remains.
- Siren PvE TEST BOSS mode (owner request 2026-10-02): all streams integrated and verified on
  `claude/pve-integration` (2026-10-03; see `docs/PVE.md`, DEV_LOG). Not merged or released; next: PR into
  `claude/visual-revision`, merge, test-channel publication. Native Windows review and owner balance playtest remain.
- Siren TEST BOSS PvE mode (docs/PVE.md) merged at `2bec705` (PR #28); test content `0.2.65597ffe7165` at channel
  `de5cc4d`. Next: owner playtest and balance retune; native Windows review remains.
- Ranged/magic Q+E ability overhaul + magic rebalance (docs/ABILITIES.md), PvE downed bodies, chip throw and update
  headroom merged at `dcc7430` (PR #33); test content `0.2.1412350a765a`. Owner balance playtest remains.
- Developer Sandbox (DEV accounts only) and Ward Zero as a PvP map: PR #44 (`accc362`), test `0.2.cb814cb80ece`; follow-up
  (rarity sort, keypress dummy cast, dummy respawn, PvP vent warnings, no machines in PvP) merged at `29d184b` (PR #45);
  test content `0.2.fd485a10e3d0` at channel `b2241e2`. Native review remains.
- Siren PvE: "Find me..." spawns ×5 rate / ×3 alive (cap 48) and 30 s intermissions merged at `d31fa25` (PR #47); test
  content `0.2.57123b899145` at channel `fee293c`. Owner playtest remains.
- Double-tap-W sprint, Tab melee lock-on, roll-cancel of melee attacks and the Escape menu board merged at `6ad1516`
  (PR #48); test content `0.2.6a229a16de6f` at channel `aac8363`. Owner playtest and native Windows feel review remain.
- Melee block riposte (a human's timed melee block holds the guard until the counter and stuns the attacker long enough
  for it) merged at `0aa70dc` (PR #49); test content `0.2.e07d5479254f` at channel `d67f3b6`. Owner playtest remains.
- Captured melee animations (sword, daggers, shield, greatsword) from the owner's Explosive pack, presentation only:
  merged at `ab93263` (PR #50); test content `0.2.00f72f2f7eb8` at channel `72dfa69`. Owner review of the new style and
  native Windows feel remain.

- Isolated native Unity conversion (2026-10-07), `codex/unity-conversion`: broad conversion work is
  continuing under the owner's request to finish before playtest. The current checkpoint adds full
  Chip drafting, DEV/PvE authority, source models/motion/audio, cosmetic bodies, account/profile
  integration, Ward entry/vent behavior, replay/spectator/settings, developer practice and separate
  native LAN/relay rooms with a headless authority mode. Source remains pinned to `05950ea`; no original-game cutover.
  Managed/source fixtures, loopback transport and real Unity API compilation pass. Actual native
  Editor/render/PhysX and Windows build remain unrun. Remaining parity/coverage is documented in
  `unity/ChipArenaConversion/CONVERSION-STATUS.md`. No merge or updater/Site publication.

- 2026-10-07 conversion continuation: source cinematic bank (31 scenes / 124 models), entrance lights, native ragdoll style forces, appearance preview/build inspection/draft controls implemented separately. Full task remains active; see latest DEV_LOG and Unity CONVERSION-STATUS.

## Unity conversion candidate — 2026-10-07 (current; supersedes earlier checkpoint gaps)

`codex/unity-conversion` now contains the full native conversion candidate from original `05950ea`, with separate Unity 6000.3.24f1 project and Windows TEST builder. Wounds/collapse, dynamic scenery, Slender/summons, station motion, combat HUD, sky announcements and animated drafts complete the latest implementation pass. Protocol 3 replaces earlier native frame layouts. Original source has no automatic player-host migration; the native relay/headless design retains host authority.

Source/managed, real loopback relay/TCP, eleven asset/package checks and genuine Unity API compilation pass. Actual Unity import/shaders/native physics/rendering, live service flows and Windows EXE remain unrun in the authoring environment. This is an owner-test candidate, not a tested binary or release. Native adapters and acceptance boundaries are in unity/ChipArenaConversion/CONVERSION-STATUS.md and VALIDATION.md. No merge, test updater publication or production deployment.

Windows follow-up (Oct. 7, 20:27): `codex/unity-build-fix`, draft PR #59. Owner's earlier log confirms texture/account and native asset/glow/melee/E-special repairs; free-ability gate correction passes conversion CI at `a46043a`. Latest screenshot reaches setup UI but shows an `ArenaPresentation` constructor allocation error. Move the Ward graphics property block into explicit initialization; add actual presentation component initialization/reset/teardown coverage to the Windows build gate. Twelve local asset/package tests pass. Rebuilt player, new native gate and successful match start remain unverified here. See newest DEV_LOG and refreshed source ZIP. No merge or publication.

## Native updater — 2026-10-08

`codex/unity-updater` starts from repair `c2cb7fd` (PR #59). It adds an isolated
Unity update button, a stable Windows launcher, verified player replacement and
startup rollback. Packaging prepares a native feed; it does not publish it. The
Electron feed is unchanged. Claude can style `NativeUpdateUI.cs`; the two menu
hooks are the only changes to `ConversionGame.cs`. See `Updater/README.md` inside
the Unity project for integration and release instructions. Source/managed and
Windows helper verification are recorded in DEV_LOG. A real Unity Windows build
and update acceptance remain required before publishing the native test feed.
Players need one starter installation to acquire the updater. Not merged/released.

Owner build follow-up: the October 8 01:55 UTC log exposed a Unity/.NET
`CompressionLevel` name collision in the updater Editor packager. PR #61 now
explicitly selects the .NET ZIP enum; the corrected native build must be rerun.

The next owner log confirms the alias repair, native gates and Windows player
build pass. Launcher packaging then failed with CS1504 source paths. The shared
compiler step now uses validated absolute paths and a response file, with a real
Windows packaging regression. Final starter ZIP/feed and in-game update acceptance
still require the repaired build to run on the owner's Unity installation.

Presentation and updater integration (2026-10-08): `claude/sleepy-ritchie-g8ruhv` carries the
captured-melee runtime, stage/overlay/draft/HUD/menu polish and Astra's updater (`6235a82`) with a
styled update card. Source, managed, Python, updater-transaction and Unity API compile checks pass;
the Windows Unity build, rendered acceptance and an actual in-game update remain. Not merged/released.
The Unity menu is now the source home screen with panel screens and a pause menu (`NativeHomeMenu.cs`);
the update card opens from the home Update Game pill. Offline renders of the real drawing code pass;
a Unity playtest of every screen (including a two-player room) is the next step.
Owner direction (2026-10-08): all future game changes target the Unity Chip Arena; Astra
handles logic and programming, Claude handles visuals (AGENTS.md).
Remaining Unity-vs-Electron gaps, split by owner, are in `docs/UNITY-PARITY-GAPS.md` (online play,
sign-in, hosting, killcam and combat rules are the blockers).
