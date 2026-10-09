# Native publishing automation development log

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
