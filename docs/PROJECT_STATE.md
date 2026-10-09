# Chip Arena — current state

Current Unity Windows checkpoint (2026-10-08 19:58 EDT): the owner's `build(4).log` records 86 PASS lines, all 15 maps / 120 spawn floors, successful Windows player build, native feed and starter ZIP preparation, and final exit 0 in Unity 6000.3.24f1. Reported source is shared `5900583` plus draft PR #67's cumulative v3 patch (`9903a66`), branch `codex/unity-startup-build-check`; the uploaded candidate package and matching build identity now pass checksum/inventory validation (version `83e6a566`, revision `639271006350008324`). Source provenance remains owner-reported because the log does not embed the Git SHA. No feed publication or actual Unity-to-Unity update is verified. Next: owner playtests the separately extracted starter and approves native test publication, then completes the older-to-newer update test. A12 build/packaging is passed; rendered gameplay, two-client/services and the remaining acceptance matrix are still open. See DEV_LOG, `docs/UNITY-PARITY-GAPS.md` and `docs/UNITY-LOGIC-HANDOFF.md`.

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
