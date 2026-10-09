# Native Unity automatic test publication

This is an isolated automation installation on the private game repository's
default branch. It does not merge the Unity game into `main`. Unity continues to
build only `codex/unity-cloud-current`, target `chip-arena`.

Owner requested browser-only setup on 2026-10-09. The existing game Update button
and native test channel remain unchanged. The scripts here retrieve the completed
cloud delivery, validate it and publish to the existing `native/windows-x64`
subtree of `sheldon37/chip-arena-test-updates`. Electron `feed/` is preserved.

## One-time browser setup

1. Merge the automation-only PR into `sheldon37/Chip-Arena` **main**. GitHub
   schedules and `workflow_run` listeners must exist on the default branch.
   The job is inactive until enabled below; a merge alone cannot publish.
2. In Unity Cloud, open **Account menu > Manage organization > Administration >
   Service Accounts > New**. Name it `chip-arena-update-reader`. Grant project
   access only to **Chip Arena**, with permission to read Build Automation build
   status, artifacts and logs and to start builds for the existing target. The
   project-scoped Automation User role was verified for these operations. Use the least-privileged applicable Automation
   role shown in the dashboard; do not grant organization owner/admin. If role
   labels do not expose build-read permissions, inspect them before granting
   broader access. The API check below is the definitive access test.
3. Select **Keys > Add key**. Copy the generated **Key ID** and **Secret key**
   directly into the following repository Actions secrets:
   <https://github.com/sheldon37/Chip-Arena/settings/secrets/actions>

   | Secret name | Value |
   |---|---|
   | `UNITY_SERVICE_KEY_ID` | Unity service-account Key ID |
   | `UNITY_SERVICE_SECRET` | Unity service-account Secret key |
   | `NATIVE_FEED_TOKEN` | Fine-grained GitHub token described next |

   Do not use the legacy Unity Build Automation API key; this job uses the v2
   service-account API. Do not paste credentials into chat or commit them.
4. At <https://github.com/settings/personal-access-tokens/new>, create a token
   named `Chip Arena native publisher`, resource owner `sheldon37`, selected
   repository **chip-arena-test-updates only**, repository permission **Contents:
   Read and write**. Metadata read is implicit. No Actions, workflow-write, admin,
   or access to the private game repository is needed. Choose an expiry and save
   its value as `NATIVE_FEED_TOKEN` above. Renew it before expiry.
5. Open **Actions > Publish native Unity cloud update > Run workflow**, select
   branch **main**, mode **check**. This validates Unity authentication and checks
   the current cloud head/native CI; when a successful build exists it downloads
   and validates the delivery. Check mode never writes the feed. It can report
   "waiting" while Unity or native CI is incomplete; that is not delivery acceptance.
6. Once the summary says **Validated Unity build ... ready**, run mode **publish**.
   The successful summary must say the public manifest and every payload part
   passed verification. A CDN delay can produce "pushed, but ... not refreshed";
   run publish again later to complete verification without rebuilding/re-pushing
   the player. An older installed game can then use Update.
7. At <https://github.com/sheldon37/Chip-Arena/settings/variables/actions>, add
   repository variable **NATIVE_AUTO_PUBLISH_ENABLED** with value **true**.
   This authorizes future verified cloud-head candidates for the existing native
   test channel. Change it to `false` or disable the workflow to pause automation.

No Unity installation, local Git, or personal-computer files are needed. After
configuration, Claude/Astra push completed batches to the existing cloud branch.
First real automatic publication verified 2026-10-09: UBA #7, source `27a7833`,
native version `3686f40e`, with public downloads verified in Actions run
`37966337783` (attempt 2). Unity service authentication and the scoped feed
credential both worked. Auto-publication is enabled. Windows installed-game
update/playtesting is still the owner's final check.

Build-start fallback verified 2026-10-09: automation requested UBA #8 for `603ca76`,
validated Unity/Linear and delivery gates, then published native `80b8edb9`. Run
`37974643037` attempt 2 verified all public downloads at 18:52:39 UTC, receipt
`b4462d36e4cc63625a48487b9ad6eb5a1de5184f`. Existing credentials were sufficient;
no target, role or permission changes were made.

## Timing and costs

The job checks once when native source CI finishes on the cloud branch and has an
hourly backstop at minute 17. Unity may still be building when source CI finishes.
Reviewed publisher changes on `main` also recheck after both hosted tests pass.
Expect publication on the first scheduled run after both are ready, usually
within an hour, plus GitHub scheduling/CDN delay. Run workflow manually for an
immediate check. Authorized publish runs now check for a missing cloud build after
both exact-head native checks pass. If none exists or is active, they request one
build pinned to that SHA using the existing `chip-arena` configuration. Check mode
remains read-only. No configuration, branch, machine or permission changes occur.

The build step waits up to 15 minutes, polling every 30 seconds, so a normal build
can publish in the same run. Longer builds resume on the hourly backstop. Active
builds prevent duplicate requests, failed/cancelled builds are not automatically
retried, and an advancing branch stops the old request. Unity's existing Auto-build
can still trigger first; the API returns 409 if another build is already pending.

The hourly job runs on GitHub's hosted Linux runner in a private repository and
uses Actions minutes. An idle check typically rounds to one billed minute, about
720 checks in a 30-day month, plus CI-triggered checks/publication/tests. Actual
usage depends on GitHub scheduling and execution time; retain the account's
existing spending cap. Unity minutes are consumed by the existing target when a new build is needed;
the wait also consumes hosted Actions minutes. No extra build is requested for an
already successful current candidate.
Successful, verified builds are not repeatedly downloaded on idle checks.
If publication succeeds but the public CDN has not refreshed, the run reports
that explicitly. The next check resumes download verification without rebuilding
or republishing the game. Only pushes integrated into `codex/unity-cloud-current`
are candidates; pushing solely to another Claude/Astra task branch is insufficient.

## Gates and behavior

- Trusted automation code is checked out from `main`, never from a delivery ZIP
  or downloaded game branch. No received player/source is executed with credentials.
- Delivery API requests are GET-only. The separate build step can POST once to
  the fixed build-target endpoint after native CI passes. Credentials are not
  forwarded to redirects or signed storage URLs. The publisher token is available only to the separate feed checkout
  after validation; it is never placed on the Unity builder or in game files.
- Requires the exact current `codex/unity-cloud-current` commit, successful UBA
  build, expected target/platform/editor and both latest native CI jobs
  (`native-source`, `native-updater-windows`) successful on that same commit.
  Known Electron baseline failures are not silently relabeled as passing; this
  native-only pipeline does not use the unrelated Electron Checks workflow.
- Requires all four export/delivery markers, Linear colour space, matching receipt,
  full delivery/starter/payload hashes and identities, safe ZIP inventory, matching
  starter and update player bytes. Rejects reported test/compiler/shader errors.
- Rejects wrong branch, unfinished/failed/deleted builds, missing artifacts,
  partial packaging, older/equal revisions and a branch/CI change before publication.
  There is no "publish despite failure" input.
- Uses one atomic Git commit for native payload objects, manifest and provenance;
  preserves the Electron feed and previous native objects. No force pushes.
- Downloads the public manifest and all parts after push, revalidates the archive,
  then records successful verification in a second small metadata-only commit.
  Interrupted public verification is resumable on the next run.
- Provider/editor logs remain private Actions artifacts for seven days; raw
  provider metadata/signed URLs are not uploaded. Public provenance contains only
  commit/build/version/hash/colour-space and verification information.
- A green automation result is package/update-delivery validation, not a Windows
  playtest, GPU visual review, multiplayer test or proof of gameplay correctness.

The `check` mode does not test feed-token write permission. The first publish run
tests that scope. A token/service failure stops publication and retains the old feed.
Documentation-only cloud pushes with no native CI run are held until exact-commit
native CI is run; an ancestor's passing result is never substituted silently.

## Maintenance and tests

`tools/publish_native_update.py`, `tools/package_cloud_build.py`,
`tools/cloud_post_build.sh`, `Publish-Native.cmd`, and their existing test files
are unchanged copies of the native tooling at cloud commit `c50993c`. They are
vendored here because `main` predates the Unity conversion. Review updates to
these trusted copies explicitly when changing the native packaging protocol;
never download executable publisher scripts from an unverified build artifact.

Run `python -m unittest discover -s automation/native-publish/Tests -p 'test_*.py'`.
Fixtures exercise real archives and local bare Git repositories. CI runs these
checks on Linux and Windows. Network tests mock provider responses, including
UBA's 303 JSON signed-URL response and its redirected logs.

Official references checked 2026-10-09:
- <https://docs.unity.com/en-us/cloud/accounts/create-service-account>
- <https://docs.unity.com/en-us/oas-build-automation-client/2.0.0>
- <https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows>
- <https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets>
- <https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens>
