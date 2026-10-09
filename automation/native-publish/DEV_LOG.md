# Native publishing automation development log

## 2026-10-09 — Astra — match the actual UBA version identifier

- PR #73 diagnostics (main `32870a0`, run `37964117907`) prove UBA #7 list and
  detail both report `unityVersion=6000_3_24f1`, `localUnityVersion=6000.3.24f1`.
  Only the version spelling was rejected; status, source, branch, target, platform
  and artifact availability all matched. This was a publisher bug, not a Unity failure.
- Accept only the dotted or underscore spelling of the exact pinned Editor;
  reject mismatching installed Editor versions when supplied. Dotted receipt,
  Linear, logs, archive, current-head/CI and public download gates remain intact.
- Regression covers real metadata, incorrect/mixed IDs and mismatched installed
  Editor. End-to-end fixture now uses real UBA version spelling. Local 49 tests:
  48 pass, one Windows-only skip. Hosted and real publication verification next.
- Repair continues on `codex/unity-publish-version`, based on `32870a0`. No game
  source change or new Unity build; successful game source remains `27a7833`.

## 2026-10-09 — Astra — diagnose successful UBA build detection

- Owner requests completion of automated publication. Task `codex/unity-publish-detection`,
  remote base default main `4babc69`. UBA #7 log confirms commit `27a7833`, Linear,
  all four export/delivery markers and final SUCCESS, with no C# or shader errors.
  Publisher run `37958519880` attempt 2 still excludes it before retrieval.
- Added bounded allowlisted list/detail metadata diagnostics and named rejection
  reasons; no acceptance gate changed and credentials/signed links are not logged.
  Trusted main publisher changes now recheck publication after both hosted test
  jobs pass, only with the existing auto-publish variable enabled. This avoids a
  manual dispatch for each reviewed tooling correction; no source auto-merges.
- Local suite: 48 tests collected, 47 passed, one Windows-only skip. Hosted tests
  and real metadata diagnosis are next. Build #7 has not yet reached the feed.
  No game or visual changes; cloud branch stays `27a7833`, no rebuild required.

## 2026-10-09 — Astra — browser-configurable cloud delivery publication

- Owner asked to finish automatic cloud-build-to-Update publication from another
  computer. Task branch `codex/unity-auto-publish`; the remote PR is based only on
  default `main` (`18231698`) so scheduled automation can run without merging the
  game conversion or changing the cloud target. Native tooling copied unchanged
  from the verified `c50993c` cloud candidate into this isolated directory.
- Added GET-only Unity v2 service-account retrieval, exact cloud-branch/commit and
  latest native Linux/Windows CI gates, complete receipt/archive/log validation,
  existing native publisher integration, atomic feed/provenance commit, resumable
  public download verification and a durable verified receipt to avoid redundant
  downloads. No game, presentation, physics, input, network, save or updater changes.
- Workflow tests run without secrets. Production publication executes trusted
  default-branch automation only. Unity credentials stay in preparation; separate
  repository-scoped feed token reaches only the post-validation feed checkout.
  Automatic scheduling is off until `NATIVE_AUTO_PUBLISH_ENABLED=true`; manual
  `check` never publishes. Hourly backstop plus cloud-branch native-CI completion;
  no Unity builds, automatic source merges, AI review/fix loops or paid upgrades.
- Local verification: 47 tests collected, 46 pass, one Windows-only wrapper test
  skipped on Linux; includes two real local-Git end-to-end publication/retry tests.
  Python syntax, YAML parsing, unchanged vendor copies and whitespace checks pass.
  Hosted Linux/Windows CI is run on the pushed PR; its result is recorded in the PR.
- Not yet verified: account authentication/roles, actual UBA API responses/artifact
  retrieval, first unattended cloud feed update, Windows installation/gameplay.
  No credentials are available here. Existing published game remains unchanged.
- Next: install the isolated default-branch workflow after its CI passes; owner
  adds three Actions secrets using the browser-only README, runs check/publish,
  then enables the repository variable. No personal-PC files or Unity installation
  are needed. Keep the dashboard on `codex/unity-cloud-current`.
