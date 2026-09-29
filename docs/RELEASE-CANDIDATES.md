# Unpublished release candidates

This is preparation, not permission to merge or publish. Source comes from an
exact 40-character GitHub commit, not a moving branch or an old ZIP. Record the
source and tooling SHAs separately. An owner-selected source SHA is a build input;
it is not evidence of final release approval.

## Reproduce locally

Use an authenticated clone of sheldon37/Chip-Arena; fetch the chosen source and
release-workflow branch. Work from a clean checkout of the reviewed tooling commit.
Use Node from .node-version (24.19.0), npm from packageManager (11.9.0), Git and
Python 3.12. Linux can construct Windows ZIPs but cannot prove Windows execution.

```powershell
# Replace the SHA with the exact commit selected for candidate review.
python scripts/release/candidate.py --commit <40-character-source-SHA> --output F:\ChipArenaCandidates\candidate-001
```

The builder creates a separate temporary Git worktree, restores npm dependencies,
runs full verification and focused updater tests, verifies pinned runtime downloads,
builds twice, compares the update payload byte-for-byte, applies that actual payload
through the source updater in a temporary directory, then packages Windows/Electron.
It never invokes hosting, GitHub Releases, account changes, or a live feed write.
No npm install or Python package installation is required for the wrapper itself.

Normal mode refuses packaging after failed verification. For diagnosis only append
`--test-only`: failures remain visible, candidate.json stays BLOCKED/publishable:false,
and the command exits nonzero even if it successfully produces a test ZIP. This is
not an approval bypass. A known-good runtime cache may be supplied with
`--runtime-cache <directory>`; the existing script rechecks every SHA256 pin.
Output must be a new directory. Failed and partial runs still retain their evidence.

The manual-only GitHub Actions workflow uses the same command, read-only permissions,
no deployment secrets and 7-day artifact retention. It uploads failure evidence with
an always step but does not suppress the failed job. workflow_dispatch may not be
available until this workflow exists on the default branch. No Actions run is implied
by creating a PR. The workflow runner is Linux; native Windows testing is manual.

## Contents and provenance

- candidate.json: source/tree/tooling SHAs, exact tool versions, checks and failures,
  content version, update manifest, artifact SHA256 hashes, outstanding checks.
- feed/latest.json and its named arena-*.json.gz: candidate for the EXISTING feed;
  production schema and updater endpoint remain unchanged. Never copy automatically.
- Chip-Arena-TEST-ONLY.zip: isolated desktop fixture, NOT a distributable release.
- logs/: complete build and check results, including failed verification.

The two builds compare the manifest and compressed content payload, not full desktop
ZIP bytes. Existing ZIP generation includes filesystem timestamps. Reproducible here
means a pinned source/lockfile/toolchain and repeatable commands with identical tested
update payload; do not claim all ZIP archives are byte-for-byte reproducible.

## Isolation and limits

The test ZIP adds test-bootstrap.cjs and points its package.json main to it. Before
normal main.cjs loads, it sets userData/sessionData to TEST-PROFILE inside the extracted
folder. It retains the production app name, protocol and session partition; the
normal installation's settings are not imported. Close the normal game before launch
because the existing single-instance behavior has not been tested on Windows here.
For strongest isolation use a separate Windows account or VM.

The test ZIP replaces only its manual updater wrapper with a local-file fetch adapter,
adds the candidate files under resources/game/test-feed, routes the game session's exact
startup update-check request to that same local manifest, and marks its initial content
version test-seed. The test-only HTTPS handler forwards every other request through the same session
with Electron's bypassCustomProtocolHandlers option; a missing/bad local feed returns
unavailable instead of falling back to live metadata. The handler is registered before
the game window opens and is outside replaceable game payload files. The actual
launcher/update.mjs, desktop update IPC/controller and
candidate runtime/game code are unchanged. Clicking Update installs the real candidate
payload from local files; clicking again should report current. The test-seed content
already contains candidate game files: this validates UI/application mechanics, not
compatibility with every historical installed version. Since the seed already contains
the candidate content build, its badge can correctly say Latest build before the first
diagnostic self-install changes the artificial seed version. Full old-to-new testing remains
a separate release gate. Test-only files are outside replaceable payload units and
must never be placed in a production package or published to the feed.

Account and multiplayer service destinations are unchanged. Offline practice testing
requires no account. Signing in, editing customization or hosting a public room can
still touch those real services; these are manual later tests, not automatically run.
No account/session/saves are copied into the test ZIP. Do not sign in during the initial
offline test. Test profiles can be discarded only after the owner is done with them.

## Promotion through the existing feed (later, separately authorized)

1. Review/merge intended source changes, resolve blocking checks, and pin the exact
   approved source SHA. Rebuild if that SHA changes. Confirm the proposed candidate
   version is not an unintended downgrade of current live content.
2. Complete WINDOWS-VALIDATION.md, including a backed-up old installation upgrade and
   real two-machine multiplayer on explicitly chosen test accounts. Record evidence.
3. Inspect the current live Site version and update manifest read-only. Preserve its
   account keys, DB, discovery and relay configuration. Reconcile any live changes
   since this candidate; never deploy a stale full Site checkout over newer services.
4. Prepare a reviewed Site asset change that adds the immutable named payload first,
   checks its download/hash, and only then switches latest.json. Preserve earlier
   payloads for recovery. If the hosting mechanism deploys assets atomically, verify
   that both are in the same consistent deployment. Do not guess an upload API.
5. Preserve the prior manifest/payload and prepare a tested rollback. The current
   updater compares equality, not version ordering; pointing latest.json backward
   can downgrade clients. Compatibility must be checked before any rollback switch.
6. Obtain owner approval of source SHA, candidate manifest/hash, Windows results,
   publication scope and rollback. Publish using the supported existing Site workflow
   only after that approval. Verify live manifest, payload hash and update-check API.
   Exercise the normal installed in-game button and record release status in DEV_LOG.

This workflow deliberately has no publishing step. Existing Site publishing capability
is preserved, not yet automated from GitHub. A private GitHub asset URL is not a
replacement for the anonymous feed, and no access token belongs in the game.
