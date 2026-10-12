# Development reference

Run commands from the repository root. Use Node **24.19.0** (`.node-version`)
with npm **11.9.0** for these checks; the shipped Windows runtime remains pinned
separately to 24.21.0. Keep `package-lock.json`; no dependencies were upgraded.
Python 3 is needed only for desktop packaging. Put its executable on PATH as
`python`, or set `CHIP_PYTHON` to its full path.

## Commands

| Purpose | Command | Notes |
| --- | --- | --- |
| Restore | `npm run setup` | Thin `npm ci --no-audit --no-fund`; requires registry access or a complete npm cache. |
| Develop | `npm run dev` | Vite http://localhost:4173 and Node authority :8788; stop with Ctrl+C. |
| Cloud loopback dev | `npm run dev -- --host 127.0.0.1` | Verified here; default all-interface Vite startup is blocked by this container's network-interface enumeration restriction. |
| Types only | `npm run verify:fast` | No packaging. |
| Affected tests | `npm run verify:fast -- tests/desktop.test.ts tests/desktop-connection.test.ts` | Types plus explicit existing test files. |
| Unit regression | `npm test` | Existing suite, known baseline failures; nonzero exit remains a failure. |
| Standard checks | `npm run verify:standard` | Types, all unit tests, client/relay/server compilation. |
| Broad checks | `npm run verify` | Standard checks plus real WebSocket match and local HTTPS relay integration; collects failures and exits nonzero. |
| Compile without release packages | `npm run build:check` | Generates icons/build identity; compiles client, Worker relay and Node server. |
| Serve compiled game | `npm start` | After build:check; http://localhost:8788. |
| WebSocket integration | `npm run test:network:local` | Own isolated server on :8792; override TEST_PORT if occupied. |
| Targeted network | `node tests/run-network.mjs block` | Also accepts maps, party, intros, first-block, chaos, rarity, motion, debris, elements. |
| HTTPS integration | `npm run test:https` | Production Worker handler with temporary in-memory SQLite; does not contact live D1. |
| Prepare legacy release files | `npm run release:prepare` | Explicit existing full build including source/update packages; does not publish. |
| Fetch Windows runtimes | `npm run runtime:fetch` | Existing exact version/hash pins, official URLs, verified cache in .cache/runtimes. |
| Portable Windows ZIP | `npm run package:windows` | Requires release:prepare and the pinned Node ZIP. |
| Electron desktop ZIP | `npm run package:desktop` | Requires portable ZIP, Electron ZIP, cloudflared and Python; outputs releases/Chip-Arena-Desktop.zip. |

On Windows PowerShell, the full local packaging procedure is:

```powershell
npm run setup
npm run verify
# Stop and investigate any failures. Packaging below is for explicit validation.
npm run release:prepare
npm run runtime:fetch
npm run package:windows
npm run package:desktop
Expand-Archive releases/Chip-Arena-Desktop.zip -DestinationPath .cache/windows-validation
& '.\.cache\windows-validation\Chip-Arena-Desktop\Chip Arena.exe'
```

Never run this against the owner's installed folder. `package:windows` replaces
only `Chip-Arena-Windows` inside CHIP_RELEASE_DIR (default `releases/`). Native
Windows execution is unverified in this Linux workspace. A PE header/ZIP check
is not an EXE startup test. Do not publish with failing/unrun release checks.

The existing alternative `python scripts/repackage-desktop.py --base <verified-desktop.zip>
--base-sha256 <audited-sha256> --out <test-output.zip>` reuses a verified desktop
runtime. It also requires release:prepare. It is not an updater-feed cutover.

## Source map

| System | Paths |
| --- | --- |
| UI/rendering and boot | src/client/main.ts, renderer.ts, character-rig.ts, host-worker.ts |
| Physics, bots, combat, Chips, match authority | src/shared/simulation.ts, movement.ts, content.ts, host-room.ts |
| Persistent WebSocket process | src/server/index.ts, socket-output.ts, directory-lease.ts |
| Site Worker: identity/discovery/HTTPS rooms | src/relay/worker.ts, accounts.ts, directory.ts, party.ts |
| Local relay database | src/relay/local.ts; **in-memory**, not persistent SQLite despite older README wording |
| SQL schema/migrations | db/schema.ts, drizzle/*.sql; no player rows exported |
| Electron shell and IPC | desktop/main.cjs, game-preload.cjs, accounts.cjs, policy.cjs, lan-socket.cjs |
| Portable launcher/update | launcher/start.mjs, update.mjs, manual-update.mjs |
| Package scripts | scripts/package-{windows,desktop,update,source}.*, repackage-desktop.py |
| Required media and notices | public/, assets/, scripts/source-audio/, docs/ASSET-CREDITS.md, docs/NOBLE-HASHES-LICENSE.txt |
| Mocap animation (presentation only) | src/client/mocap/ (clip decoder, rig pose, per-weapon sets; the *-clips.ts tables are generated), scripts/mocap/ |

Prefer targeted checks for small edits. Changes under shared simulation, networking,
identity, persistence, dependency manifests, launcher, packaging, or CI require
`npm run verify`. Headless tests do not verify animation appearance or GPU speed.
Full verification attempts every stage and reports failures, never substitutes a
passing narrower test for a failed stage. Logs go to ignored `test-results/`.

Mocap clips: the sword, daggers, shield and greatsword animations come from the owner's Explosive "RPG Character
Mecanim Animation Pack" (Unity asset; its FBX and .meta files are never committed), retargeted offline to the voxel rig.
To regenerate after changing the converter or clip choices, extract the pack outside the repository and run
`node scripts/mocap/convert-explosive.mjs <pack>/Animations` (rewrites src/client/mocap/{sword,daggers,shield,greatsword}-clips.ts
deterministically; the clip choices and their [start, contact start, contact end, settled] marks are its SETS table),
then `node --import tsx --test tests/melee-mocap.test.ts tests/greatsword-mocap.test.ts`. `--preview
tools/visual-lab/mocap-preview.generated.ts [--events <out.json>]` writes the candidate clips (git-ignored; the JSON lists
each clip's authored Hit markers) for the visual lab's `mocap=<key>` raw view (`support=0` for one-handed clips);
`mocapOff=1` renders the procedural layers for before/after strips. Attack clips are retimed onto the shared
windup/active/recovery timing at runtime; shared timing never depends on them. The shield set is the pack's left-arm shield
read onto the game's right hand; the daggers use a reverse grip; the converter keeps the greatsword's two-handed hilt
(fists, pommel, blade root) off the voxel head, pauldrons and torso; captured cuts stand on the procedural phrase's footwork.
The earlier Mixamo greatsword set (`node scripts/mocap/convert-greatsword.mjs <pack dir>`, the owner's "Great Sword Pack")
is superseded: running it writes that set back into greatsword-clips.ts.
The Unity melee hit reactions use the same pack: `node unity/ChipArenaConversion/tools/export_pack_reactions.mjs
<pack>/Animations` rewrites `Assets/ChipArena/Core/NativePackClips.Generated.cs` and `tools/pack-reactions-provenance.json`
deterministically (GetHit, Block-GetHit and Stunned per weapon set; the choices are its SETS table), then run the
Tests project (`Tests/PackReactionChecks.cs` compares each track with the provenance hashes).
The Unity weapon-signature entrances' pilot motion uses the same pack: `node unity/ChipArenaConversion/tools/export_pack_intros.mjs
<pack>/Animations` rewrites `Assets/ChipArena/Core/NativePackIntros.Generated.cs` and `tools/pack-intros-provenance.json`
deterministically (one 4.8 s, 60 Hz track per entrance, sequenced from pack clips with timing keys, cross-fades, root
continuity and foot contacts; the choices are its ENTRANCES table; the daggers keep their baked clip), then run the Tests
project (`Tests/PackIntroChecks.cs` checks the hashes, planted feet, floor, grips, blade and staff clearance, the hits, the
detail shots' framing and the daggers' fingerprint).
The Unity jump animation uses the same pack: `node unity/ChipArenaConversion/tools/export_pack_jumps.mjs <pack>/Animations`
rewrites `Assets/ChipArena/Core/NativePackJumps.Generated.cs` and `tools/pack-jumps-provenance.json` deterministically
(per weapon style the guard frame, the Jump and Land takes split by their .meta frame ranges, the Fall loop and the
Jump-Flip; the choices are its SETS table), then run the Tests project (`Tests/JumpMotionChecks.cs` checks the hashes,
phase tracking, floor contact, grips, the dagger flip and the hooks).

## CI and agent access

`.github/workflows/checks.yml` runs on PRs or explicit workflow_dispatch, with no
push trigger, no publication jobs, no secrets, no paid AI and read-only contents.
Docs-only PRs run whitespace/scope checks without dependencies/builds. Other edits
run standard checks; cross-cutting edits run broad checks. There is one stable
`Required checks` gate. npm's download cache is keyed by OS/architecture and the
lockfile through setup-node. Logs expire after 7 days. There is no automatic
Windows package workflow; the local procedure above is the phase-1 build path.

Destination metadata confirms private visibility and owner push/admin access.
Ruleset inspection returned HTTP 403: the private repository's current plan does
not support that feature. Authorized browser inspection found no webhooks or
environments, Pages unavailable, and only ChatGPT Codex Connector installed.
Actions permits all actions, defaults to read-only contents/packages, disables
fork PR workflows and creating/approving PRs, and denies cross-repository access.
Repository retention is 90 days; the new workflow sets its artifacts to 7 days.
No branch rules, required reviews, visibility, billing or
administrative policy were changed. No upgrade is needed for the source import.
After owner review of
the existing failures, `Required checks` is the suggested merge check, not a
claim that protection has been enabled. A solo owner must not be required to
obtain their own approving PR review. AGENTS.md is guidance, not access control.

Authorize each agent's GitHub integration for this private repository only, with
source/branch/PR access sufficient for its task; workflow edits may need additional
provider permission. Do not grant admin, release credentials, or signing secrets
for routine coding. Astra and Claude use separate task branches/worktrees and one
writer per branch. Claude imports `@AGENTS.md` from CLAUDE.md. Review the exact PR
head when handing off unmerged work. No second AI review is automatically required.

## Test update channel (TEST-ONLY builds)

Testers' TEST-ONLY builds made with `--channel` update from the public channel repo
`sheldon37/chip-arena-test-updates` (feed `https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/`),
never the live feed. Tooling is on `claude/test-update-channel` (PR #5; based on PR #3's
release/candidate-workflow).

The owner confirmed `claude/visual-revision` as the shared test-development branch
on 2026-10-01. Claude and Codex use this same source branch and update channel;
fetch its current head before starting and preserve the other agent's changes.
Standing owner instruction (2026-10-01): publish completed game updates from the
verified, approved shared-branch commit to this existing test Update channel
unless told otherwise; do not ask for publication confirmation each time. This
does not authorize automatic merges or live-feed/Site releases. Keep recording
actual verification results and unrun checks, including known baseline failures
for diagnostic test builds.
Publishing a version:

```sh
python scripts/release/candidate.py --commit <40-char SHA> --output <new dir> --test-only \
  --channel https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/
# then in the channel repo: replace feed/ with the candidate's feed/ files (latest.json + one
# arena-*.json.gz, latest-v2.json + one arena-*.pack), add a PUBLISHED.md row, commit, push;
# log it in DEV_LOG. If the candidate has no latest.json (schema 1 held), follow the channel
# README's "schema 1 hold" step instead of copying latest.json.
```

Needs Node 24.19.0 / npm 11.9.0. Only a new tester needs the ZIP; later versions ship as
feed files. Never put credentials or player data in the channel; live-feed publication
remains a separate owner-approved release.

For an existing channel installation, a new Windows seed ZIP is unnecessary.
Feed-only publication can use a clean worktree at the exact approved source commit:
run the affected/full verification, `npm run release:prepare` twice and compare
the feed bytes; run PR #5's `check-payload.mjs <feed-dir> <source-dir>` and the
remote-channel/status tests. Preserve the actual verification results, including
known failures for diagnostic test builds. Publish only the verified manifest and
payload together, then verify the real network updater from the previous version,
same-version skip, settings preservation and menu status. Record the source SHA,
content version, channel commit and unrun native checks in DEV_LOG.

### Update payload limits and updater v2 (owner-approved 2026-10-03)

`scripts/package-update.mjs` reads `CHIP_UPDATE_PROFILE` (details in `scripts/update-format.mjs`):

| Profile | Schema 1 (`latest.json`) | Schema 2 (`latest-v2.json`) | Schema 1 too large |
| --- | --- | --- | --- |
| `live` (default; Site/live builds) | 25 MiB guard (Cloudflare Pages per-file limit) | not produced | build fails, unchanged |
| `test-channel` (set by `candidate.py --channel`) | up to 32 MiB, the cap every installed launcher enforces | always; default limit 95 MiB (GitHub rejects files over 100 MB) | held back: only v2 files are written |

Overrides: `CHIP_UPDATE_SCHEMA1_MAX_MIB` (test-channel 1-32, live 1-25), `CHIP_UPDATE_SCHEMA2_MAX_MIB`
(test-channel 1-256), `CHIP_UPDATE_SCHEMA2_QUALITY` (Brotli 1-11, default 11, about 85 s).
Schema 2 is test-channel only: the live profile refuses `CHIP_UPDATE_SCHEMA2`,
`CHIP_UPDATE_SCHEMA2_MAX_MIB` and `CHIP_UPDATE_LAUNCHER_OVERLAY` before it builds anything. Never set
`CHIP_UPDATE_PROFILE` in a shell used for a Site build: before any Site publication, check that
`dist/client/updates/` holds `latest.json` and no `latest-v2.json` or `.pack`. `CHIP_UPDATE_LAUNCHER_OVERLAY` names a folder
whose whitelisted files replace `launcher/*` inside schema 2; a test-channel payload must carry the
channel's own `manual-update.mjs`, never the production one that reads the live feed.
Default `npm run build` output is byte-identical to the previous packager. Every packaged path must
be Windows-safe: no device names, also with an extension or trailing spaces (`CON`, `PRN`, `AUX`,
`NUL`, `CONIN$`, `CONOUT$`, `CLOCK$`, `COM0`-`COM9`, `LPT0`-`LPT9`, and COM/LPT with superscript
1-3), and no trailing dot or space. The live
schema-1 payload was 24.84 MiB on 2026-10-03, about 0.17 MiB under the live guard.

Schema 2 is Brotli of `CHIPPACK`, a uint32 BE index length, a JSON index
`{schema:2,version,files:[{path,size,sha256}]}` and the raw file bytes (no base64). The manifest
adds `encoding:"br"`, `bytes`, `unpackedBytes`, `files` and a `launcher` list. `launcher/update.mjs`
reads it only when the caller passes `schema2:true` (the test-channel wrapper does; production
`manual-update.mjs` does not), streams the download to `runtime/update-download.part` with a
256 MiB cap, a byte-count and a SHA-256 check before decompressing (every write is completed even
when the disk accepts only part of it, and the hash covers the bytes written), then unpacks file by file with
size/hash checks, the same `allowedPath` rule, Windows-safe names and a strict launcher whitelist
(`LAUNCHER_FILES`). Only a 404 for `latest-v2.json` falls back to schema 1, and a v2 launcher refuses
the channel's schema-1 hold notice (`hold:true`) instead of downgrading. Downloading, unpacking,
launcher syntax checks and installation all print progress, because the desktop updater stops
after 90 s without output.

Launcher self-update: changed launcher files are syntax-checked and staged in
`runtime/launcher-pending/`. The next launcher start (`startLauncher()` in every entry point,
including hosting) re-verifies them, copies the old files to `runtime/launcher-backup/`, swaps them
under `runtime/launcher-journal.json`, records `runtime/launcher-probation.json` and loads the new
`update.mjs`. `launcher/update.mjs` restores the backup itself after a crash mid-swap or when the
new `update.mjs` cannot load or lacks `LAUNCHER_API>=2`. Everything else is decided by the stable
desktop code, which ships only in the seed ZIP and is never self-updated (`desktop/update.cjs`):

- Update proves `update.mjs`, `manual-update.mjs`, `remote-feed.mjs` and `local-feed.mjs` when a
  later Update installs a version with them. Until then, any failed Update (an error, a non-zero
  exit, no result line, a timeout) restores the previous launcher and drops anything staged.
- Hosting proves `start.mjs` and `update.mjs` when a later Create Room reaches `YOUR GAME:`
  (`desktop/main.cjs`). Until then, a server start that fails or times out before that line restores
  the previous launcher.
- A run that activated the launcher never proves it (the probation token changes). A newer launcher
  activated while one is still unproven keeps the original backup. When every needed kind is
  proven, the backup is deleted.
- An Update that reports `installed` proves only when `release.json`, read by the desktop code, has
  the reported version. `current` never proves. While Update is unproven, a `current` result is
  checked against the menu badge's update check (`feedVersion` in `desktop/main.cjs`; in a TEST-ONLY
  seed the channel handler answers it from `latest-v2.json`). If the feed has a different version,
  the launcher said "up to date" without really checking, so the run counts as failed and the
  previous launcher is restored.
- A restore first writes its plan to `runtime/launcher-restoring.json`, then replaces each file by
  copy-and-rename. If it stops midway (crash, locked file), the desktop code finishes it before the
  next Update or Create Room, and `update.mjs` before its next start. Nothing is proven while the
  marker exists, so a half-restored launcher can never become the proven one.

Not covered: a failure after `YOUR GAME:` (for example in online tunnel code), an `update.mjs`
whose bug only shows after it installed one update, and non-desktop entry points (the Windows
`.bat` package, which never reads schema 2). A launcher that reports `current` without checking is
caught only when the badge check is reachable and the feed has moved on: offline, with the check
unavailable, or while the feed still equals the installed version, it stays on probation (neither
proven nor restored) until a later Update installs or fails. A very short window after a channel
publish (raw GitHub caching) could make the two reads disagree; the cost is one unneeded restore,
and the next Update stages the launcher again. The feed still lists a rolled-back launcher, so the
next Update downloads and stages it again; the fix is a corrected publish, which the restored
launcher installs. Settings, saves, `runtime/` binaries and `test-channel.json` are never update
targets. The desktop update timeout is now 90 s of silence (45 min maximum). Launchers installed
before v2 never read schema 2: a tester needs one new TEST-ONLY ZIP.

## Clean-checkout verification

```sh
git worktree add --detach ../chip-arena-check HEAD
cd ../chip-arena-check
npm run setup
npm run verify
```

Record command exit codes and the tested SHA. Read MIGRATION-REPORT for the actual
results, environment limits and existing failures. Setup/compilation do not require
production secrets; account service tests generate disposable signing keys.
