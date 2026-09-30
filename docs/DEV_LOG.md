# Chip Arena — development log

Read recent entries and relevant open PRs before starting work. Keep entries concise,
newest first; use Git diffs and tests as implementation evidence. Entries on task
branches are not automatically visible in other checkouts. Check GitHub for current
merge/release status; an entry records status at the time it was written.

For every completed task or unfinished handoff, record:
- Date, agent, task, branch, base commit, and PR or implementation commit when known.
- Changes and rationale; relevant files/systems and compatibility impacts.
- Actual checks with passed/failed/not-run outcomes and environment limitations.
- Remaining issues, overlapping work/dependencies, and the exact next step.
- Status: implemented on branch / merged / released (release version only if verified).
For animation/combat work explicitly state whether hit timing, hitboxes, damage,
cooldowns, movement rules, or network behavior changed. Never invent a commit,
PR, test result, release, or earlier log history. Git history identifies the commit
containing an entry; no self-referential commit hash is required.

## 2026-09-30 — Claude — visual revision TEST-ONLY Windows build

- Owner requested a Windows test build of `claude/visual-revision` at 2557a12. Built with
  PR #3 tooling (release/candidate-workflow 7ff6462) `candidate.py --test-only`, Node
  24.19.0 / npm 11.9.0 (Python 3.11): Chip-Arena-TEST-ONLY.zip, SHA256 0f4df606...71c5,
  content 0.2.754d8ed53b1b, build 8d4cb82a5c68b3f79c2e3b0b. Status BLOCKED/publishable:false.
- Passed: setup, fixture tests, updater tests, runtime pins, two builds with identical
  payload, actual payload install, Windows/desktop/isolated packaging. `verify` failed only
  on known items: 12 known unit failures (portable-update passes on Node 24), the 4 server
  TS errors (PR #2), WebSocket rematch timeout.
- Delivered to the owner as 10 split parts; not published, no live feed change. Native
  Windows run pending owner test.

## 2026-09-30 — Claude — complete visual revision

- Task: owner-requested complete visual revision (animations, intros, model, UI),
  presentation-only. Branch `claude/visual-revision`, base `migration/shared-development`
  `dfb90c8`; PR to migration/shared-development (see GitHub). Plan, owner direction,
  invariants and ownership: docs/VISUAL-REVISION-PLAN.md. Player-facing summary:
  docs/UPDATE-HISTORY.md (copied from PR #3's version 7ff6462 plus a new entry — on
  merge conflict keep both sides' rows/sections).
- Changes: dev-only visual lab (tools/visual-lab, not in the build); no-behaviour module
  split; ground-anchored locomotion; continuous reactions/knockdown/ragdolls; Souls-like
  melee from owner reference GIFs (DEV classes untouched); shouldered guns, reloads,
  casting, first-person view model; detailed voxel model in armor slots with hue-true
  glow; reworked intros; UI polish and in-place HUD updates. Unchanged: hit timing,
  hitboxes, damage, cooldowns, movement rules, network/snapshot formats, saves, access
  control; src/shared, server and relay untouched; no pre-existing test edited.
- Checks (Linux, Node 22.22.2): full `node --import tsx --test tests/*.test.ts` 523 tests,
  only the 13 pre-existing failures; `npm run check:types` passes; independent reviews
  (invariants, animation quality, real-app smoke via Playwright/SwiftShader) passed.
  `npm run build:check` still stops on the 4 known server TS errors (fixed by PR #2).
- Not run: native Windows/GPU, real-hardware performance, online multiplayer, packaging.
- Known open: jump airborne pose barely visible in lab strips; dagger idle guard less low
  than the reference; warm accents pale under lobby/portrait lighting; intro close-ups
  blow the chest glow to white (pre-existing); crossed-arms lobby fist clips (pre-existing);
  Developer Sandbox buttons overflow at 1280x720 (pre-existing); intro scenes render
  slower under software GL, so check frame rate on the owner's PC. Owner-gated stale tests
  (fighter high kick, rarity-combat dagger mirror) still fail as before.
- Next: owner playtest on Windows and review of feel (cadence, melee, glow); then merge
  decisions. Status: implemented on branch; not merged; not released.

## 2026-09-29 — Astra — shared development log

- Branch: `migration/shared-development`; base before this documentation task:
  `dcdcd930565d05ace379e263206e13366b3e4e69`; PR #1.
- Added this log and a shared AGENTS.md handoff requirement. CLAUDE.md already
  imports AGENTS.md, so both agents use the same policy.
- Checks: read current remote AGENTS.md and PR metadata; documentation-only change.
  No gameplay/build/Windows tests run for this task. Existing baseline failures
  remain documented in MIGRATION-REPORT.md; this entry does not revalidate them.
- Runtime, game mechanics, assets and update feed unchanged. No player release.
- Status: implemented on migration branch; PR #1 still draft/unmerged at writing.
- Next: each agent synchronizes this branch before making its own task branch;
  after migration merges, start new tasks from updated main. Review relevant open
  PRs to avoid overlapping work. Resolve baseline failures before release work.
