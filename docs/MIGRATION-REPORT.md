# Migration evidence — 2026-09-29

## Provenance and recovery

Original source: `/workspace/scratch/6889f4e63071/.sites-checkout`, clean main at
`18231698c7c79a7a604e567b933eb2d0155d7a3e` (68 commits). No uncommitted/untracked
source was present. Latest saved Site source version 64 independently reports
that same SHA, and its deployment status is succeeded. No remote was configured
in the original checkout.

Before edits, created `recovery/chip-arena-original.bundle` in the migration task
workspace; `git bundle verify` passed and confirmed complete history. Created an
independent clone (no hardlinks), branch `migration/shared-development`; retained
the original main and all ancestors. Original checkout and live Site unchanged.
The full bundle is local; do not assume scratch retention. The original baseline
also remains in the existing Site source history. The final source export includes
an incremental migration bundle with the original SHA as its required prerequisite,
not a replacement/squashed history. Restore/apply instructions accompany that export.

Compared the available Windows ZIP, not an inaccessible running installation.
Its SHA256 is `ba9981df7e660a0f65caae7a0e9843ed28e5d4d5b59a6fcd01438e2af0673791`.
Content is 0.2.44211e218269 versus baseline 0.2.4cad5cbfa762. 267 shared source files
were identical, 72 differed, and no files existed only in the old embedded source;
current source also contains newer files. All desktop shell files matched. The
old embedded source predates build-info.generated.ts. Do not migrate from that ZIP.

## Source/history audit

Scanned 1,690 reachable historical blobs (1,784 distinct payloads including nested ZIP
and gzip/base64 update extraction and commit metadata). Checked token patterns,
private PEM/JWK keys, credential URLs, literal credential assignments, JWTs and
SQLite signatures; inspected sensitive filenames, current ignored paths, migration
scripts and SQL migrations. No extraction errors. Eighteen literal matches were
reviewed: synthetic test passwords/forged tokens. Broad AWS-looking matches inside
Rapier's embedded WASM were false positives. No exposed production credential
requiring rotation was identified. Heuristic scans are not proof of absence.

No live provider secrets or database rows were retrieved. DEV_PUBLIC_KEY contains
public verification material only. Source's .env.example has placeholders/names.
Original and imported baseline have no .github/workflows or deployment script.
Existing .openai/hosting.json and Worker/db code remain because desktop depends on
those services. Destination webhooks/apps/branch policies remain uninspected because
`sheldon37/Chip-Arena` is not found by the connected GitHub account/installation.
No push, repository creation, remote replacement, PR, or admin policy change occurred.

Generated dist (104 files, roughly 53 MiB) is removed from the migration branch's
index and ignored; it remains available from original history and local builds.
Dependencies, caches, logs, test results, runtime downloads, release ZIPs, .env
variants, private Claude settings and account-session.dat are excluded. Required
source assets/public media, lockfile, SQL schema/migrations, packaging and licenses
remain. Largest source asset is ~2.6 MiB; LFS not warranted. History is ~1.3 GiB
compressed; no historical blob reaches 50 MiB. No history rewrite was performed.

## Baseline results (before migration edits)

Environment: Linux, Node 24.19.0, npm 11.9.0, Python 3.12.14. Baseline reused its
existing installed dependencies; final clean-checkout restoration is separate.

- `node node_modules/typescript/bin/tsc --noEmit`: passed.
- `npm test`: **406 passed, 13 failed, 419 total**, no skipped/cancelled tests.
- `node tests/run-network.mjs`: failed at the existing rematch timeout after
  reaching match completion/assertions. Disconnect assertion was not reached.
- `npm run server:build`: failed with TS7006 in chaos.ts and standard-melee.ts,
  plus TS2322/TS2345 in leet.ts (four errors); reproduced from untouched baseline.
- HTTP startup: baseline source and migration's bundled authority both returned
  application=chip-arena, served the client HTML and selected /socket transport.
- Native Windows/GPU playtest: not run; Windows and Wine unavailable.

Existing unit failures (not removed, weakened or marked expected failures):

- `tests/chaos-visuals.test.ts`: twenty named music tracks are distinct nonempty MP3 assets.
- `tests/chaos.test.ts`: 50 behavior Chips are distinct and all are consumed by the authoritative engine.
- `tests/core.test.ts`: 182 unique chips, compatibility, caps, and explicit multiplicative stacking.
- `tests/demon-update.test.ts`: Demon Step moves substantially farther, matches prediction, respects cooldown and walls, and needs access.
- `tests/fighter-presentation.test.ts`: reference poses have a straight high kick, turning uppercut, flat landing, and animated broken halo.
- `tests/host-room.test.ts`: guest disconnect releases lobby slot and forfeits active match after timeout.
- `tests/knockback-distance.test.ts`: blocking sword and greatsword swings throws the attacker back; Rebound Engine increases the distance.
- `tests/network-bandwidth.test.ts`: eight fighting clients negotiate compression, receive identical states and keep ping responsive.
- `tests/rarity-combat.test.ts`: 182 catalog entries have a real effect, eight rarities and useful weapon eligibility.
- `tests/rarity-combat.test.ts`: dagger primaries and echoes alternate server sequence and leading-hand poses.
- `tests/stage-hazards.test.ts`: vertical platforms fit default jump height and breakable supports release suspended sections.
- `tests/view-elements-audio.test.ts`: shield real attack applies both elemental DoTs, ticks are nonrecursive and reset clears them.
- `tests/view-elements-audio.test.ts`: all bundled effects are valid audible PCM with peak headroom and distinct samples.

## Migration implementation

Merged every kit repo-files entry into root; preserved older docs. Shared policy
is AGENTS.md, imported by CLAUDE.md. No gameplay, network protocol, accounts,
settings, audio or renderer logic changed. Only generated build identity can
change because the established hash includes package.json.

Added thin setup/typecheck/verification/build/packaging commands. Legacy npm build
and update feed are retained. Release intermediates use .cache/release rather than
shared /tmp; Python ZIP paths are portable. Added checksum-pinned runtime downloads.
Network test launcher now waits for readiness, rejects unknown suite names/busy
ports, and treats child signals/startup failure as failure rather than exit 0.

One read-only PR/manual CI workflow selects docs/standard/full checks, retains logs
7 days and never packages Windows or publishes. Known baseline failures remain red.
No workflow has been executed on GitHub. Repository permissions/check gates are
documented proposals; no review count, admin bypass or paid service was configured.

## Verified migration commands

Code/packaging revision: `958a8d160cd60e363b49265575afb6e1fc862935`.
Generated game build: `dee9d7ad42bbf9697cc55e63`. The game simulation, client
renderer logic, network/auth/save code, runtime manifest, assets and lockfile are
byte-identical to the original baseline; only generated build identity differs.

- Clean detached worktree started with no dependencies/build output; `npm run setup`
  passed, restoring 49 packages from the unchanged lockfile (no fallback installer).
- Default `npm run dev`: blocked by this container's uv_interface_addresses
  restriction when Vite enumerates network interfaces. No permissions bypass used.
- `npm run dev -- --host 127.0.0.1`: passed client HTML and server health readiness
  on :4173/:8788 in the clean checkout. Processes stopped afterward. No graphical
  browser/GPU playtest claimed.
- `npm run verify:fast -- tests/desktop.test.ts tests/desktop-connection.test.ts tests/maps-bots.test.ts tests/selection-settings.test.ts tests/lobby-return-identity.test.ts tests/desktop-accounts.test.ts`:
  types passed, 23/23 tests passed, including bot maps/settings, encrypted mock
  login persistence, lobby return, team identity, intros and spectating logic.
- Untouched baseline `npm run test:https`: passed real local HTTP/SQLite transport,
  five sets/four drafts, 3,587 matching snapshots, retries, rematch and guest leave.
- `npm run release:prepare`: passed existing complete client/relay/update build.
  Validation content version 0.2.729db3cf3b99; not published or promoted.
- `npm run runtime:fetch`: passed all three official downloads and fixed SHA256
  checks (Node, Electron, cloudflared). No downloaded executable was run on Linux.
- `npm run package:windows`: passed, 84,508,205-byte portable ZIP, SHA256
  e88e1f64e4614a116f941cfad5133e6ce0b6140583f32d0b83ba4852678826f1.
- `npm run package:desktop`: passed, 260,375,351-byte desktop ZIP, SHA256
  3a0d4e263dcbb3e36f3ec0967a20c9fcf977835680fa68fe0bb1d519fde8ea32;
  ZIP integrity, required contents, content identity and Windows x64 PE checks passed.
  All 279 core source/asset files in its embedded source matched current inputs;
  no update payload nesting, unsafe ZIP paths or runtime manifest changes found.
  These local test packages were created before the final evidence documentation;
  they are not a release of the final handoff commit and are not exported/published.
- `CHIP_AUTO_UPDATE=0 CHIP_BUNDLE_DIR=<local-portable-folder> node tests/run-portable.mjs`:
  failed after startup on the existing exact /api/network response assertion:
  actual includes inviteOrigin:null; expected omits it. No test assertion weakened.
- YAML syntax, Node/Python script syntax, full-change CI classification and
  `git diff --check`: passed. GitHub Actions execution itself is not run.
- Native Windows install, GPU/animation, real remote multiplayer and the owner's
  installed settings/updater: not run, unavailable here. Required procedure in RELEASES.

## Final clean-checkout comparison

`npm run verify` at 958a8d160cd60e363b49265575afb6e1fc862935: **FAILED, exit 1**,
correctly retaining unsuccessful stages rather than masking them.

| Stage | Baseline | Clean migration checkout |
| --- | --- | --- |
| Typecheck | Passed | Passed |
| Existing unit suite | 406 passed / 13 failed | 406 passed / 13 failed; identical failure names |
| Client/relay compilation | Existing release build available | Passed |
| Standalone server TypeScript compilation | Four errors | Same four errors |
| WebSocket full-match integration | Rematch timeout | Same rematch timeout |
| HTTPS Worker/SQLite match | Passed; 3,587 compared snapshots | Passed; 3,488 compared snapshots |

The clean HTTPS run completed five sets, four drafts, retry, rematch and guest
leave in 179.7 seconds. No new unit failure was introduced.
These results do not classify every old failure as a stale test; combat/performance
failures still need investigation. Full CI should remain red until they are resolved.

## Handoff status

GitHub destination is unconfirmed/inaccessible, so no pushed commit or PR exists.
Final evidence-only commit follows the tested code commit; it changes docs only.
The source-only export records its exact SHA, all file hashes, baseline provenance,
test outputs and an incremental Git bundle. It excludes generated player packages.
The local source branch and unchanged original baseline are recoverable; see the
export's MIGRATION-HANDOFF.md before importing. Owner action: provide/connect the
exact private repository, then inspect its history and hooks before the first push.
Production and the old working installation remain unchanged; phase 2 not started.
