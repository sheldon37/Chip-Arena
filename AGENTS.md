# Chip Arena — shared agent instructions

## Scope and authority
Work on the existing Chip Arena game, not Embertrail. Preserve the current
Windows desktop runtime, gameplay, assets, networking, and save compatibility.
Do not rebuild the game or change engines as part of repository migration.
Explicit current owner instructions define the requested change; inspected code
and observed tests establish implementation status. Document conflicts instead
of treating old chat claims or an unverified checklist as working functionality.

## Start small
1. Confirm the repository, branch, working-tree status, and current commit. Never
   discard someone else's uncommitted work. Fetch the remote before starting a
   new task. Base new work on current `origin/main`, unless a named PR/ref is the
   task's target. An existing branch may need a deliberate merge/rebase, not a
   blind pull or reset. Respect any branch prefix required by the agent service.
2. Read `docs/PROJECT_STATE.md` and the task/PR. Read only relevant sections of
   `docs/DEVELOPMENT.md`, `docs/DESIGN_INTENT.md`, and `docs/RELEASES.md` as needed.
   Search relevant symbols before opening files. Do not reread the whole repo,
   all chat history, generated bundles, binaries, or every documentation file.
3. Use a task branch and a separate checkout/worktree. One writer per task branch
   at a time. Do not force-push shared branches or write directly to `main`.

## Implement and verify
Make the smallest complete change that meets the acceptance criteria. Avoid
unrelated refactors, dependency upgrades, reformatting, asset regeneration, or
new frameworks. Reuse verified commands in `docs/DEVELOPMENT.md`; inspect and
repair missing commands rather than guessing that a script exists.
Run affected checks; expand coverage for networking, saves, packaging, or other
cross-cutting changes. A passing build is not proof of Windows gameplay,
multiplayer correctness, visual quality, or a functioning updater. Mark tests
not run and their environment limitations. Never claim invented results.

## Release and cost boundaries
A normal task ends with tested code and a pushed branch/PR, not a player release.
Do not routinely publish the browser Site or generate a full Windows package.
Package when required to validate the task or explicitly requested. Preserve
shared renderer/client code even if it also supports a browser. Keep hosting
that the desktop client, updater, room discovery, or backend still needs.
No production release, public distribution, live-data migration, infrastructure
removal, or automatic AI review/fix loop without explicit owner authorization.
Use existing deterministic scripts for checks/builds; no model calls in routine CI.

## Security and handoff
Never commit secrets, hard-coded developer passwords, signing keys, personal
saves, tokens, or live database exports. Private repositories are not a reason
to ship secrets. Use scoped credentials and documented secret names only.
Before pushing, review the diff for sensitive data and accidental large files.
Update project state only when status, blockers, or the next step changed; update
other docs only when the relevant behavior changed. Keep PROJECT_STATE short.
In the PR/final handoff state the branch, pushed commit, scope, actual checks,
unrun checks, known risks, and release status. The next agent must be able to
continue from GitHub without access to this conversation. Owner controls merges
and releases unless a specific request authorizes otherwise.

## Shared development log (Astra and Claude)
Before each task, read recent entries in `docs/DEV_LOG.md` and inspect relevant
open PRs as well as PROJECT_STATE. Confirm your checkout includes the intended
base commit; logs and pushes do not automatically synchronize another checkout.
Until migration PR #1 merges, use current `migration/shared-development` as the
base for new task branches unless the owner names another ref; afterward use main.
At completion or handoff, add a concise entry to DEV_LOG on your own task branch:
date/agent/task, branch and base, PR or implementation commit if known, changes and
rationale, affected systems, actual checks/results, remaining risks and next step.
Distinguish implemented-on-branch, merged, and released; verify current GitHub
status rather than assuming historical log status is current. Record animation
impacts on hit timing, hitboxes, damage, cooldowns, movement and networking.
Use docs/UPDATE-HISTORY.md for earlier game updates and add a concise entry when
a game update is completed; distinguish source changes from verified player releases.
Keep PROJECT_STATE short. Preserve other agents' entries when resolving conflicts;
do not overwrite their work. Coordinate overlapping files/systems before editing.
Do not append long transcripts or duplicate full diffs. No automatic review loop.
