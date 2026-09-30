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

## 2026-09-30 — Claude — visual revision (unfinished handoff)

- Task: owner-requested complete visual revision (animations, intros, model, UI),
  presentation-only. Branch `claude/visual-revision`, base `migration/shared-development`
  `dfb90c8`. Plan, owner direction, invariants and remaining task lists:
  docs/VISUAL-REVISION-PLAN.md. Not merged, not released; no PR yet.
- Done on branch: dev-only visual lab (tools/visual-lab); no-behaviour module split
  (verified identical renders, pose dumps and test results); wave 1 merged: ground-
  anchored locomotion (planted feet, cadence, turn lag, landing spring, roll blending,
  class stances) and reactions (transition policy, continuous knockdown, layered hit
  reactions, ragdoll joints, rewindable cinematics). Gameplay timing, hitboxes, damage,
  cooldowns, movement rules and networking unchanged; comedic death fling kept.
- Checks (Linux, Node 22.22.2): full `node --import tsx --test tests/*.test.ts` fails only
  the 13 pre-existing failures; `npm run check:types` passes. Windows/GPU, multiplayer
  playback and packaging not run.
- In progress on local/backup branches: `claude/visual-revision-melee`,
  `claude/visual-revision-look` (wave 2, under review). Remaining: Souls-feel melee pass
  from owner reference GIFs, detailed voxel model with modular armor slots, ranged/magic,
  intros, final polish/review, UPDATE-HISTORY entry and PR.
- Overlap: PR #3 also edits DEV_LOG (keep both entries). No shared/server files touched.

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
