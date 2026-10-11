# Native publishing automation development log

## 2026-10-11 — Astra — Direct download with updates ready on first launch

- Owner screenshot shows `Install the Windows starter ZIP once to enable updates`.
  The raw UBA player is still launchable in the outer export, while the correctly
  packaged updater-ready ZIP is nested inside. Prior download instructions sent
  the owner through that ambiguous export. Owner wants no extra updater setup.
- Branch `codex/native-player-download-20261011`, main base `06d51a6`.
  Publish the clean `Chip-Arena-Windows.zip` directly as a test-release asset in
  the existing `chip-arena-test-updates` repository after verified native feed
  publication. Extract once, open `Chip Arena/Chip Arena.exe`; launcher, marker,
  configured feed and full `app` payload are included. No game/source rebuild or
  bootstrap change is required; current UBA #38 already contains this package.
- Exact current cloud/native-CI, Unity/Linear/compiler, delivery inventory,
  provenance, ZIP/hash, and byte-for-byte update-player checks precede upload.
  Draft prerelease, scoped existing feed token, no overwrite; anonymous public
  ZIP download is size/hash-verified before recording completion. Already verified
  releases skip downloads, incomplete publication can resume. Main still does not
  contain the game source; no Electron, gameplay, visual or save changes.
- Local suite: 88 tests, 87 passed / one platform skip. Hosted Linux/Windows and
  real public download verification remain the final integration gates. Real
  Windows rendered gameplay remains an owner check; this task fixes distribution.


## 2026-10-11 — Astra — Accept the clean Chip Arena player package

- Owner requested one `Chip Arena` folder and `Chip Arena.exe` for distribution.
  Branch `codex/native-simple-package-validator`, main base `9c7d44f`.
- Synchronize the publisher's cloud archive validator with the game packager:
  accept the new single-root seed or the existing flat legacy seed; retain exact
  inventory, checksum and byte-for-byte native-player gates. No relaxed payload
  validation, permission/configuration or feed changes. Shared package helper also
  writes `Chip-Arena-Windows.zip` beside a cloud export for player distribution.
- Local suite: 74 tests, 73 passed / one platform skip. Hosted Linux/Windows gates
  required before merge. This backward-compatible bridge must land before the
  game branch `codex/unity-simple-package-20261011` is promoted to cloud.
- No game source is merged into main. Existing automatic publication remains the
  only feed writer; this bridge does not claim a new player is built/published.


## 2026-10-10 — Astra — retain failed Unity diagnostics

- Branch `codex/unity-failed-build-diagnostics`, base `0cdb0058`. Unity #24
  for grab/revival source `d9ebe850` failed before preparation; publisher run
  `38077039859` stopped correctly but retained no compiler/build-gate log.
- Save the exact-source terminal build's provider log and minimal identity in
  private Actions artifacts. Existing credentials, target, publication gates and
  failed-build no-retry behavior remain unchanged. No game/animation changes.
- Local publisher suite: 71 tests, 70 pass and one expected Windows-only skip.
  Covers safe metadata, unavailable logs and failure propagation. Hosted checks
  and actual #24 diagnosis remain pending.

## 2026-10-10 — Astra — expose deferred publication outcomes

- Owner requested final Unity visual-batch publication and delegated pipeline
  repairs. Task `codex/unity-publish-outcomes`, main base `f92c16eb`.
- Run #44 (`38058241777`) waited on UBA #17 `sentToBuilder` for 15 minutes,
  then deferred with exit 0. Its candidate stayed `64084ad`; branch movement was
  not the cause. It never reached feed publication. Run #45 separately lacked
  exact-head CI on documentation-only `ff81074`; the native workflow repair is
  tracked on `codex/unity-publish-ci` against the cloud game branch.
- Emit explicit ready/status outputs and summary labels. Deferred runs annotate
  `Native update not published`, identifying waiting CI, waiting Unity (with
  active build ID/state), or a superseded head. A pending build gate skips redundant
  prepare/download work. Check mode stays read-only; published-unverified,
  published-verified and already-published states remain distinct.
- Local 68 tests: 67 passed, one expected Windows-only skip. Regression tests cover
  actual workflow outputs/summaries for queued build #17, CI wait, branch advance,
  successful build, and publication warning separation. Real fixture archive and
  local-git publication tests pass; no test contacted a service or live feed.
- Hosted Linux/Windows tests and activation pending. Existing exact-commit/native
  CI, shader/compiler/Linear, receipt, archive/hash and public download gates stay
  intact. No game/animation changes, source merge into main, target configuration,
  credentials, permissions, automatic failed-build retry or new release claimed.

## 2026-10-09 14:52 EDT — Astra — build-start fallback and account update verified

- PR #82 merged as `f6137be` after hosted Linux/Windows checks (66 tests, one
  expected skip). Workflow 37974643037 authenticated using existing service-account
  credentials, observed Auto-build=true, and requested UBA #8 for exact game source
  `603ca76b69b2256d58b24abc4ac23166306e1fd4` at 18:38:17 UTC. No target or permission
  changes. Unity was provisioned then built successfully; detected at 18:50:37.
- Full delivery validated; native version `80b8edb9` pushed at feed commit
  `e19ece73917f3320a0f38a7a1804e2c5ae51955f`. Initial public cache delay correctly
  failed verification. Attempt 2 reused the published candidate, verified every
  public part/manifest and succeeded at 18:52:39. Receipt commit
  `b4462d36e4cc63625a48487b9ad6eb5a1de5184f`, publicDownloadsVerified=true.
- Game PR #81 and fixture isolation #83 hide the account URL, configure the existing
  service automatically, and preserve saved pilots/account APIs/grants. Existing
  Sheldon DEV record and credential verified through authorized owner administration;
  no account reset, database mutation, backend deployment or secrets committed.
  Windows sign-in and save/reload remain owner runtime checks after Update.

## 2026-10-09 — Astra — start a missing verified cloud build

- Task `codex/unity-cloud-trigger`, main base `93e491d`. Owner requested publishing
  account PR #81 to Update. Shared/cloud `ae0a003` passed both native CI jobs, but
  publisher run `37973568012` attempts 1–2 still found no build newer than #7.
  This confirms the remaining gap is build triggering, not delivery publication.
- Add a separate authorized build step before the read-only delivery checks. It
  validates the existing enabled Windows target/branch, requires exact-head native
  CI, avoids any active build, and pins one request to the checked commit. No target
  settings, machine, branch, credentials or permissions are changed. Check mode
  stays read-only. Failed/cancelled builds are not automatically retried; branch
  advancement stops the step. Poll for at most 15 minutes, then let the backstop
  resume. Existing delivery/compiler/shader/receipt/hash gates remain unchanged.
- Local suite: 66 tests, 65 passed / one expected platform skip. Ten added trigger
  tests cover CI/config/branch gates, duplicate prevention, commit pinning, rejected
  requests and safe conflict handling. Hosted Linux/Windows checks and the actual
  service start remain pending. Unity native player is still build #7 until verified.
- Account PR legacy Checks reproduces the documented 12 Electron unit failures and
  TypeScript errors in chaos.ts, leet.ts and standard-melee.ts. Native checks passed;
  those unrelated legacy failures are not relabeled as success. No gameplay changes.

## 2026-10-09 13:29 EDT — Astra — UBA #7 published and publicly verified

- Owner asked to fix the remaining automatic publisher integration. Repairs
  PRs #73–79 are merged on main, latest code `f24f393`. Real UBA responses required
  exact underscore version IDs, JSON-string artifact URLs, the documented Unity
  US East UVCS storage endpoint, the log `redirect_url` field, and gzip decoding.
  The successful run confirms gzip was the final marker-reading problem. No
  source/CI/receipt/Linear/ZIP/checksum/compiler/shader gate was bypassed.
- Every repair passed hosted Linux and Windows suites before merge. Final suite
  has 56 tests (one expected platform-specific skip); source native CI and UBA #7
  passed on `27a7833609b69f3067a7acb8b1c81344e649f5c3`. No new Unity build needed.
- Automatic main-push run `37966337783` validated the complete delivery, used the
  scoped feed credential successfully, and published #7 at channel commit
  `3cf2b0f8caa0d7c498edf6c51d2f0e7bcc63f05d`. Initial public-cache delay was reported
  accurately; verification-only attempt 2 completed SUCCESS. Public manifest and
  all three payload parts passed full download/hash/archive validation. Receipt
  `publicDownloadsVerified=true`, `verifiedAt=2026-10-09T17:29:37.385421+00:00`,
  persisted at channel commit `fc5eb96759a9c946fcff93fd7fdbee096db0a415`.
- Released version `3686f40e5a1c7600d8abcb6e050701af7719aaedd976936efd003e33b9953a55`,
  revision `639271614362672189`, payload SHA256
  `a9bd4f1f729ff7a6aa4d56b7413b89b1028a21a7e39e127829be35836df4ba2e`,
  94,229,097 bytes / 229 files, Linear / Unity 6000.3.24f1. Previous objects and
  Electron feed preserved. No game/visual changes, live feed, Site or paid upgrade.
- Owner next step: use Update in an older native installation, then verify launch,
  retained settings and gameplay (including Siren minion hits). Runtime Windows/GPU
  playtesting is not performed by the publisher. Future completed batches must
  reach `codex/unity-cloud-current`; enabled Unity Auto-build creates candidates,
  native-CI completion/hourly checks publish validated successful current heads.
  Publication automation is now verified; future Unity push-trigger behavior was
  owner-configured, while this accepted Unity build was manually started.

## 2026-10-09 — Astra — decode compressed stored provider logs

- PR #78 passed hosted tests and merged at `890a677`; run `37965946382` retrieves
  the stored log but literal marker search sees only 77,353 decoded characters.
  The owner's real log has 1,527,518 bytes / 9,320 lines; gzipping it reproduces
  the small corrupted-text shape. Add gzip magic detection and bounded decoding
  before existing log checks, with a diagnostic confirming when this occurs.
- Raw and JSON-string logs remain supported. Decompressed logs over 64 MiB fail;
  markers and compiler/shader checks run on decoded text, with regression coverage.
- Local: 56 tests, 55 pass, one Windows-only skip. Task
  `codex/unity-publish-gzip`, base `890a677`; hosted and actual publication next.
  No bypass of any acceptance gate, no source/build change.

## 2026-10-09 — Astra — use the log endpoint's documented redirect field

- PR #77 passed hosted tests and merged at `0971446`. Run `37965640899`
  reaches the explicit manual-log response, whose payload uses `redirect_url`,
  not the artifact endpoint's `url`. Confirmed in Unity's v2 OpenAPI source:
  `GetBuildLogResponse.properties.redirect_url`. Handle both distinct API fields.
- Updated the manual-log regression to the exact schema. Local 54 tests:
  53 pass, one Windows-only skip. Hosted then real publication checks next.
  Task `codex/unity-publish-log-schema`, base `0971446`; all validation remains.

## 2026-10-09 — Astra — retrieve and interpret the complete provider log

- Storage repair PR #76 passed hosted tests and merged at `a8aadb9`. Real run
  `37965171329` downloads/unpacks the game and passes receipt/archive/seed checks,
  then fails a provider-log marker. Owner's complete #7 log contains all markers.
- Use the documented `log?redirect=manual` URL retrieval rather than a default
  progressive response, and accept a JSON-encoded log string as well as raw text.
  Missing-marker errors identify the exact gate and text size, never log contents
  or signed URLs. No gate is skipped. Downloaded GitHub diagnostic artifact was
  unavailable locally (403), so real response validation remains pending.
- Local: 54 tests, 53 pass, one Windows-only skip. Added manual-log redirect and
  escaped JSON-log fixtures. Task `codex/unity-publish-log`, base `a8aadb9`.
  No Unity/game rebuild; public feed is still unchanged pending validation.

## 2026-10-09 — Astra — support this project's Unity artifact storage

- PR #75 passed hosted tests and merged at `03888ad`. Real run `37964788287`
  reached the signed download URL on `prd-azure-eastus-01-cloud.plasticscm.com`.
  The original storage allowlist omitted Unity's own UVCS artifact storage.
- Added only this observed exact host, HTTPS on 443 or its documented 7178 port;
  Unity's official proxy-endpoint list and UVCS Server API confirm the endpoint.
  https://docs.unity.com/en-us/oas-unity-version-control-server/1.0.0
  Service authorization still cannot be forwarded there. Regression covers
  UVCS-to-Azure redirects, credential stripping, lookalike domains and wrong ports.
- Local: 52 tests collected, 51 pass, one Windows-only skip. Task
  `codex/unity-publish-storage`, base `03888ad`; hosted/publication checks next.
  All archive and identity gates remain; no new Unity build or game changes.

## 2026-10-09 — Astra — handle UBA's actual artifact-link response

- Version repair PR #74 passed both hosted jobs and merged at `0975cba`.
  Real run `37964439938` now selects #7 and reaches download; UBA's HTTP redirect
  body decodes to a JSON string, rather than the documented `{url: ...}` object.
- Handle both response shapes. Non-string/malformed link values and untrusted
  hosts still fail before any download. Auth is still removed before storage
  requests; signed URLs are never logged. Added regression and negative cases.
- Local: 51 tests collected, 50 passed, one Windows-only skip; hosted tests and
  actual download/publication next. Task `codex/unity-publish-download` from `0975cba`.
  No Unity rebuild or game change. Build #7 has not been published yet.

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
