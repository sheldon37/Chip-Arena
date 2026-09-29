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
