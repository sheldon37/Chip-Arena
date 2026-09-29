# Releases and existing hosted dependencies

Phase 1 changes development workflow only. No player release, live configuration,
account migration, runtime upgrade, feed cutover or production publish is authorized
by this migration. Preserve the current installation and hosting until phase 2.

## Established source facts

| Component | Existing mechanism | Migration status |
| --- | --- | --- |
| Desktop game | Electron 0.3.6 app shell, main.cjs, bundled arena://game origin; Three.js/Rapier renderer and simulation | Preserved |
| Optional hosted game | Current Chip Arena Site client | No publish or removal |
| Content updater | launcher/update.mjs, desktop/update.cjs; https://chip-arena-prototype.sheldon37.chatgpt.site/updates/latest.json and named arena-*.json.gz | Old feed preserved |
| Update check | /api/update-check served by src/relay/worker.ts | Preserved |
| Accounts/DEV access | desktop/accounts.cjs and shared/dev-auth.ts use the same Site's /api/accounts/* and legacy /api/auth/login | Hosted signing identity preserved |
| Discovery | shared/room-directory.ts and server/directory-lease.ts use the same Site's /api/directory/* | Preserved |
| HTTPS rooms | Site /api/rooms/* and /api/rooms/v2/*; own DB binding in .openai/hosting.json | Preserved; no live records read/exported |
| Desktop online hosting | Bundled Node authority; cloudflared 2026.9.3 quick tunnel; local host stays local | Runtime pins preserved |
| Optional dedicated backend | MULTIPLAYER_URL selects secure WebSockets through /api/network | Existing setting not read or changed |

Source independently establishes Chip Arena's own `DB` binding; this was not
inferred from Embertrail. Local `LocalRoomDatabase` uses SQLite `:memory:` and
migrations; process exit loses local relay rows. The live Site database is separate.

Production secret names: **DEV_SIGNING_KEY**, **DEV_ACCOUNT_VERIFIER**. Keep their
values in the existing provider's secret store. The checked-in DEV_PUBLIC_KEY is
public verification material, not a private key. No key rotation is needed based
on the migration scan's reviewed findings; this is not a security certification.

## Identity and updates

The root manifest's 0.1.0 is not the content update version. Baseline launcher is
0.3.6; game build is `9b6caf05644a73c2cf5929da`; generated stable content identifier
is `0.2.4cad5cbfa762`. `generate-build-info.mjs` hashes src/public/package.json;
`package-update.mjs` hashes packaged paths/content into a 0.2.<hash> identifier.
Build metadata can change after development script edits even with unchanged gameplay.

The available Windows ZIP (SHA256
`ba9981df7e660a0f65caae7a0e9843ed28e5d4d5b59a6fcd01438e2af0673791`)
has content 0.2.44211e218269, the same shell/runtime manifest, and 72 differing
source files versus the baseline. This is an artifact comparison, not evidence
of the version currently installed on the owner's PC.

The existing update format is a gzip JSON file list with SHA256 per file and an
outer SHA256 in HTTPS metadata. The updater validates allowed paths, runtime major,
version and required files, stages changes, journals replacement, and keeps a
backup/recovery path. Hashes are integrity checks, **not publisher signatures**.
It updates server.mjs, dist/client, drizzle and release.json, not Electron, Node,
cloudflared or the desktop shell. The manual UI coordinates stopping sessions.
Desktop 0.3.6 launches the child with CHIP_AUTO_UPDATE=0; the portable launcher
has its own startup update behavior. Preserve both until explicitly migrated.

Private GitHub Releases do not supply an anonymous download feed. No GitHub token
may be embedded in game binaries. Phase 2 must settle private download access,
signing, stable/test channels, compatibility, interruption recovery and the old
installation bridge before changing any endpoint.

## Settings and recovery

- UI preferences: localStorage `chip-arena-preferences`; remember flag
  `chip-arena-remember`. Browser accounts use local/session storage
  `chip-arena-account-session`; legacy DEV claim uses sessionStorage
  `chip-arena-dev-session`.
- Electron: session partition `persist:chip-arena-game`, origin `arena://game`;
  `app.getPath('userData')/account-session.dat`, encrypted via Electron safeStorage.
  The source does not set a fixed userData path. Record the actual Windows path
  on the test machine instead of guessing a particular AppData directory.
- Signed-in pilot customization is stored server-side in player_accounts.profile;
  passwords, sessions, recovery codes and audit rows stay in live storage.
- Updater staging/journal/backup lives under resources/game/runtime. Exclude it
  from source; do not delete or relocate installed settings as part of migration.

## Packaging and import boundaries

See DEVELOPMENT for explicit build commands. Release preparation intermediates
now live in ignored `.cache/release/` rather than a shared global /tmp, and Python
ZIP entries use forward slashes on Windows. Runtime inputs are verified by the
existing SHA256 pins in `.cache/runtimes`; do not commit full runtimes or packages.
No runtime version, updater URL or package schema changed.

All required source media stays in Git (largest current source asset ~2.6 MiB).
LFS is not needed for these assets. Historical generated files remain in the 68
original commits to preserve history; new commits stop tracking dist. The original
history is ~1.3 GiB compressed and contains no blobs >=50 MiB. Its one-time import
cost remains; no history rewrite or LFS conversion was attempted. Future generated
packages belong in controlled Releases/artifacts, not normal source commits.

Retain ASSET-CREDITS and license notices. Supplied commercial-game audio remains
as existing required source; its inclusion does not establish redistribution rights.
Source privacy does not hide readable code in existing desktop/source packages.

No imported GitHub workflow exists at the original baseline. The new workflow
only verifies PRs/manual runs. The selected destination is confirmed private and
empty, but the connector does not expose webhooks, installed publishing Apps or
deployment settings. Ruleset inspection returned a private-plan HTTP 403. Local
Git transport also lacks authentication. These prevent a safe first push; do not
bypass them by pushing to the live Site. Inspect publishing controls first, without changing live
infrastructure. If the destination is empty and safe, push the original baseline
to main once and this migration branch separately, then open a PR. If nonempty,
fetch and reconcile its actual history without force-push; do not assume main
can be replaced. Owner retains merge/release policy.

## Windows validation for phase 2

Use a separate extracted test installation. Record exact SHA, shell/content versions,
actual userData location and OS/GPU. Test startup/offline bots; host/join on two
machines; settings and login persistence; current melee/block/dodge, teams, intros,
spectating and lobby return. Then separately validate old -> bridge -> next test
update, partial downloads, invalid hashes, interrupted install and rollback.
None of this migration's Linux checks proves native Windows/GPU behavior.

External references checked 2026-09-29: [workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax),
[required checks](https://docs.github.com/en/enterprise-cloud%40latest/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks),
[Claude imports](https://code.claude.com/docs/en/memory).
