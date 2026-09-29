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

## 2026-09-29 — Astra — owner Windows results and mixed-feed label diagnosis

- Branch release/candidate-workflow, PR #3, previous head 467eb23152b1451b5121163c3ae9daab17b2e887.
  Evidence added in commit 686ffeaf26e66f796395c03dd46f7910501efea3.
- Owner reports Candidate 001 update/restart, preference persistence after reopening,
  offline bots, and subsequent up-to-date result passed on Windows. Screenshot
  confirms running menu and candidate version 0.2.729db3cf3b99. Other Windows
  checklist items and actual old-to-new/account/two-machine tests remain unverified.
- Diagnosed contradictory Update Available badge: client startup check reads live
  /api/update-check; test manual updater reads bundled test-feed. Any unequal
  build ID is treated as available, including an older/different live build.
- Verification: targeted offline execution of existing checkGameUpdate reproduced
  available for a simulated different live build and current for the actual bundled
  manifest. Current live manifest was not retrieved. No gameplay/build/package
  rerun needed for this documentation-only update.
- No code fix yet; next fixture revision should use one feed for status/install or
  explicitly distinguish its channels. Do not publish a candidate to clear the badge.
- Handoff context: CLAUDE.md imports AGENTS.md. Read PROJECT_STATE, recent DEV_LOG,
  DEVELOPMENT and DESIGN_INTENT, relevant historical docs and Git history. Inspect
  open PRs #1 (migration), #2 (server types), #3 (candidate tooling) individually:
  all were open/unmerged when checked. PR #2 is excluded from Candidate 001.
  Main does not yet contain the shared migration docs; normal new work uses
  migration/shared-development until PR #1 merges, unless a task names another ref.
- Working preference: explicit PR/commit handoffs; no routine PR polling, scheduled
  hourly checks, or automatic review/fix loop. Cancellation of Claude's previously
  scheduled monitoring has not been independently verified from this environment.
- Status: documentation on branch only, unmerged/unreleased. Existing automated
  failures remain blockers; no accounts/services/live-feed/installation changed.

## 2026-09-29 — Astra — unpublished candidate workflow — validated test fixture

- Branch release/candidate-workflow; source baseline dfb90c854e9b7b48e8982819a9fcbe786b1263f1.
- Added exact-commit isolated builds, a manual-only artifact workflow, repeat-payload
  validation, a local-feed Windows fixture with isolated profile, and release/Windows
  validation instructions. No gameplay, production updater, account or network changes.
- Claude PR #2 is open for server build errors; deliberately not merged into this baseline.
- PR #3; results in docs/CANDIDATE-001.md. Clean restore, 7 updater tests, HTTPS,
  repeat-payload equality, all 101 installed files, ZIP integrity and Linux fixture
  smoke passed. Unit suite 406/419, four server type errors and rematch timeout
  remain failures. Windows/GPU/real old-install upgrade not run. Not merged/released.
- Next: review the workflow PR, resolve baseline blockers,
  complete Windows validation, then obtain separate approval before live publication.


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
