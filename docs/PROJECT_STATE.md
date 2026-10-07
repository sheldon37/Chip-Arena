# Chip Arena — current state

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
  branch `claude/sleepy-ritchie-g8ruhv`, PR into `claude/visual-revision`; merge and test publication in DEV_LOG. Owner
  review of the new style and native Windows feel remain.

- Isolated native Unity conversion (2026-10-07), `codex/unity-conversion`: source checkpoint now includes
  first-person stability (base-pose feedback fix, interpolated camera and continuous animation clock), smooth ADS
  and rifle scope, plus the staff/glow and native standard-ability foundations. Original game and accepted Unity lab remain separate; no updater cutover.
  Managed/source checks and Unity API compilation pass; shader import, native gameplay and Windows EXE/visual acceptance
  remain unrun here. Next: build with activated Unity 6000.3.24f1 and check first-person idle/casts/movement, pause/V and rifle ADS/scope at different display rates.
  Exact coverage, native adaptations and remaining full-conversion work: unity/ChipArenaConversion/CONVERSION-STATUS.md.
