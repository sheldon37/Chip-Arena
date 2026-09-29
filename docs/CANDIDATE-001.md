# Candidate 001 — BLOCKED, diagnostic Windows testing only

Nothing has been published or merged. Live feed, accounts, multiplayer services,
settings and the working installation were not changed by this task.

## Identity

- Source GitHub commit: dfb90c854e9b7b48e8982819a9fcbe786b1263f1.
- Source tree: 6a4945932030b1ffb854974ac8b9b2e67ceffe6b.
- Tested local tooling commit: d88a4470e200961a01ada680c0866bc0f9cbb897.
- Equivalent GitHub tooling commit: b9147c394e1ba1592b9d2cb0a3c9bf1d42dc1154.
  Both tooling trees were verified identical: 5661734f1480cbaa910233b75f31bebe50116fcd.
  Different commit IDs reflect local one-commit versus connector per-file history.
- Workflow PR: https://github.com/sheldon37/Chip-Arena/pull/3, targeting the unmerged
  migration branch. Later result-documentation commits do not change tested tooling.
- Game build: dee9d7ad42bbf9697cc55e63; update version: 0.2.729db3cf3b99.
- Payload: arena-224bf52f2d9353e5.json.gz, 18,202,795 bytes.
- Payload SHA256: 224bf52f2d9353e5ca60238fbfe4e336440bedf2011fe8e1f3e3a7358cd8c8d3.
- Test ZIP SHA256: 09251d0ed7bff8a186a1cf6a43aaaa1a6ca1a7cd20e78ddf68f3d3ee4c4cb151.
- Environment: Linux, Node 24.19.0, npm 11.9.0, Python 3.12.14.

## Actual results

| Check | Result |
| --- | --- |
| Clean isolated worktree, npm lockfile restore | Passed |
| Typecheck | Passed |
| Unit suite | 406/419 passed; 13 failed |
| Strict server build | Failed: four existing TypeScript errors |
| WebSocket full-match integration | Failed: rematch timeout |
| Local HTTPS relay integration | Passed |
| Focused auth/updater/controller tests | 7/7 passed |
| Downloaded/cached runtime SHA256 pins | Passed |
| Two complete content builds | Passed; manifest and gzip payload byte-identical |
| Actual payload installation | Passed; all 101 installed files checked |
| Current version, offline refusal, bad checksum, settings sentinel | Passed |
| Windows portable and Electron ZIP construction/integrity | Passed |
| Isolated test ZIP construction/integrity | Passed |
| Extracted test manual wrapper: install then current | Passed under Linux Node |
| Packaged bundled server /health | Passed under Linux Node |
| Native Windows EXE/GPU and real two-machine multiplayer | Not run |
| Actual historical installed version -> candidate | Not run |
| GitHub manual candidate workflow execution | Not run |
| Live publication | Not performed; not authorized |

The builder exited 1 and recorded publishable:false / BLOCKED. The explicit
--test-only mode produced the fixture without reclassifying failed tests as passes.
This is a concrete artifact for testing, not an approvable production release yet.

Claude PR #2 proposes fixing the server build configuration. It was open when
inspected and is NOT included in this source SHA. Its reported CI results should
not be applied to Candidate 001. The owner's screenshot reports 407/419 passing
there; the compression test passed in that run but failed here, so timing-sensitive
results vary. Unit issues and WebSocket timeout still require separate resolution.

## Owner Windows results and update-label diagnosis — 2026-09-29

The owner tested the isolated Candidate 001 ZIP on Windows and reported:
- The first update installed and restarted the game.
- Settings survived the update and a subsequent manual reopen.
- Play vs Bots worked.
- A subsequent check displayed "You're up to date (0.2.729db3cf3b99)".
The supplied screenshot shows the running menu and that version, alongside an
"Update Available!" badge. These are owner-reported basic Windows results;
account flows, two-machine multiplayer, failure/recovery, and historical-version
upgrade compatibility remain unverified. Candidate 001 remains BLOCKED.

Cause of the contradictory label: src/client/update-status.ts checks the live
DIRECTORY_ORIGIN + /api/update-check and compares GAME_BUILD by inequality.
The fixture's scripts/release/test-manual-update.mjs instead supplies localFeed
to the real updater using the bundled test-feed. Only the manual update was
redirected for this fixture; the startup label still reads production.
Any different live build (including an older build) yields "available"; it is
not a newer-version comparison.

A targeted offline reproduction executed the existing checkGameUpdate function:
a simulated different live build returned "available", while the real bundled
manifest returned "current" for candidate build dee9d7ad42bbf9697cc55e63.
The exact current live manifest was not retrieved; the external read was
unavailable. No production availability/version claim is made.
This finding explains the fixture's inconsistent display and does not invalidate
the owner's successful local update. No implementation fix or rebuilt package
is included in this documentation update. Follow-up: have the fixture's status
check and updater consult the same bundled feed, or label the two channels
explicitly; do not publish the test candidate to silence the badge.

## Corrected test fixture r2 — 2026-09-29

- Fix: the isolated Electron game session now serves its exact /api/update-check
  request from resources/game/test-feed/latest.json. The manual updater already
  uses that feed. Missing/malformed feed metadata reports unavailable; no live fallback.
- A session-specific handler forwards all other HTTPS requests unchanged via
  bypassCustomProtocolHandlers. Production shell, updater and gameplay source are
  unchanged. This follows Electron's documented protocol/session APIs:
  https://www.electronjs.org/docs/latest/api/session#sesfetchinput-init
- Archive comparison: only resources/app/test-bootstrap.cjs changed, and
  resources/app/test-update-status.cjs was added; every other archive entry is
  byte-identical to original Candidate 001, including the bundled feed/payload.
- The source SHA, content build/version and payload checksum above are unchanged.
  This is a corrected diagnostic fixture, not a new game release.
- Chip-Arena-TEST-ONLY-r2.zip SHA256:
  fb518e3802fa3bcd80a78e2ab706ba326732d0cf56943ebdf26228e836fd99fb
- Chip-Arena-Test-Label-Fix.zip SHA256:
  7946c325bf9d386f15bafa46b973c8b1a91b539c4ea97025299a45b4e4ed1555
  The small patch contains those two files and instructions to merge them into
  an existing TEST-ONLY folder after closing the app. It preserves TEST-PROFILE.
- New regression checks: 3 passed (local/missing/malformed metadata, unrelated
  request forwarding with original method/headers/body, correct Electron partition).
  They run in npm test and as a hard prerequisite in the candidate builder.
- The actual existing renderer check, executed against the packaged handler, reports
  current for the real bundled manifest and unavailable when it is missing, without
  network calls. Extracted manual updater installs then reports current; status checks
  still pass after installation. Linux Node smoke, not native Electron/Windows.
- Broad verification: typecheck passed; 410/422 unit tests passed; all 12 failures
  are in original Candidate 001's 13-failure set. The compression test passed this
  time. The four baseline server TypeScript errors and WebSocket rematch timeout
  remain. This does not clear the release gate.
- Native Windows behavior of r2 still needs owner validation. Original Candidate 001
  owner tests remain evidence for the earlier package; they do not prove this new
  Electron handler has run on Windows.
- A fresh fixture seed already has candidate content, so its build-based badge can
  show Latest build before the artificial version self-install. After an update or
  reopen it must not compare against the live feed.

## Reproduce

Check out GitHub tooling commit b9147c394e1ba1592b9d2cb0a3c9bf1d42dc1154 in an
already authenticated clone with the selected source commit fetched, then run:

```powershell
python scripts/release/candidate.py --commit dfb90c854e9b7b48e8982819a9fcbe786b1263f1 --output F:\ChipArenaCandidates\candidate-001 --test-only
```

Use a new output directory; Node/npm pins are enforced. Full ZIP hashes may differ
because the existing ZIP scripts embed timestamps. The tested content payload was
byte-identical across consecutive builds. The JSON report retains logs and hashes.

## Next

Run WINDOWS-VALIDATION.md against the isolated ZIP. Review PR #3 for workflow and
fixture changes; review PR #2 independently. Resolve baseline failures and merge
only the intended changes with owner authorization. Then build a fresh candidate
from that exact approved SHA, complete real old-to-new and multiplayer validation,
and approve the feed manifest/hash before any publication. No feed cutover is needed.

The existing-feed promotion procedure is in RELEASE-CANDIDATES.md. It requires a
fresh read-only live-version check before publication to avoid replacing newer
backend/service changes or downgrading clients. No live feed availability or version
claim is made by this offline candidate run.

References for the new tooling design: Electron app.setPath/sessionData before ready
(https://www.electronjs.org/docs/latest/api/app) and GitHub manual workflows
(https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).
