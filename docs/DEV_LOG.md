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

## 2026-09-29 — Claude — server TypeScript build errors

- Branch `claude/chip-arena-typescript-errors-v9ljw8`, PR #2; originally based on
  `dcdcd93`, then merged current `migration/shared-development` (`dfb90c8`).
  Implementation commit `c1d1ef6`.
- Cause: `tsconfig.server.json` used NodeNext resolution, which cannot resolve the
  extensionless `./exports` import in Rapier's `rapier.d.ts`; `skipLibCheck` hid it,
  so RAPIER became `any` and produced the four documented errors. Fix: use the base
  config's ESNext/Bundler resolution. No source edits.
- Compatibility: emitted server JS byte-identical to the previous output (two .d.ts
  files gain Rapier types). Hit timing, hitboxes, damage, cooldowns, movement and
  network behavior unchanged. Tradeoff: NodeNext's `.js` import-extension check is lost.
- Checks: local Node 22.22.2 — server:build and typecheck passed; built server
  /health passed; unit 406/419 before and after with identical failures. PR CI on
  `c1d1ef6` (Node 24.19.0): build:check incl. server:build passed, HTTPS passed;
  unit 407/419 (12 known baseline failures), WebSocket rematch timeout (baseline).
  Windows/GPU, packaging and multiplayer not run.
- Not in Candidate 001 (PR #3), whose table still lists the server build failure.
  PR #2 monitoring and its scheduled check-in were cancelled at owner request;
  verified no remaining scheduled triggers.
- Status: implemented on branch, unmerged, unreleased. Next: owner review of PR #2;
  a later candidate built from a SHA containing it should re-run the strict server build.

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
