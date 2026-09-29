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
