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

Prefer targeted checks for small edits. Changes under shared simulation, networking,
identity, persistence, dependency manifests, launcher, packaging, or CI require
`npm run verify`. Headless tests do not verify animation appearance or GPU speed.
Full verification attempts every stage and reports failures, never substitutes a
passing narrower test for a failed stage. Logs go to ignored `test-results/`.

## CI and agent access

`.github/workflows/checks.yml` runs on PRs or explicit workflow_dispatch, with no
push trigger, no publication jobs, no secrets, no paid AI and read-only contents.
Docs-only PRs run whitespace/scope checks without dependencies/builds. Other edits
run standard checks; cross-cutting edits run broad checks. There is one stable
`Required checks` gate. npm's download cache is keyed by OS/architecture and the
lockfile through setup-node. Logs expire after 7 days. There is no automatic
Windows package workflow; the local procedure above is the phase-1 build path.

Repository settings could not be inspected. No branch rules, required reviews,
visibility, billing or administrative policy were changed. After owner review of
the existing failures, `Required checks` is the suggested merge check, not a
claim that protection has been enabled. A solo owner must not be required to
obtain their own approving PR review. AGENTS.md is guidance, not access control.

Authorize each agent's GitHub integration for this private repository only, with
source/branch/PR access sufficient for its task; workflow edits may need additional
provider permission. Do not grant admin, release credentials, or signing secrets
for routine coding. Astra and Claude use separate task branches/worktrees and one
writer per branch. Claude imports `@AGENTS.md` from CLAUDE.md. Review the exact PR
head when handing off unmerged work. No second AI review is automatically required.

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
