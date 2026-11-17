# Independent Cloudflare staging verification

Verified November 16, 2026 against the account owner's Cloudflare account. Staging Worker version `6d5b3e38-a94d-45c6-b356-46844a7fdb9d` served HTTPS and the authenticated checks below. The live game still uses Sites v70. This report covers exclusively synthetic player records.

| Resource | Observed state |
| --- | --- |
| `chip-arena-accounts-staging` Worker | Active. [Health endpoint](https://chip-arena-accounts-staging.chip-arena-sheldon37.workers.dev/healthz) |
| `chip-arena-accounts-staging-restore-01` D1 | Current staging `DB` binding. Set `CHIP_D1_STAGING_NAME` to this name when regenerating staging configuration. Look up its UUID in the owner's D1 dashboard. |
| `chip-arena-accounts-staging` D1 | Earlier isolated migration rehearsal retained. Contains synthetic data. |
| `chip-arena-accounts-production` D1 | Both schema migrations applied. Confirmed zero player accounts. The owner approved live migration and sign-in cutover on November 16, 2026, once the cutover checks pass. |

## Remote transfer report

A full synthetic legacy SQL snapshot was encrypted with AES-256-GCM CMS, decrypted for the allowlisted import, and compared against an independently exported and encrypted remote D1 snapshot. Counts below are from before the HTTP smoke tests created additional synthetic records.

| Table | Source | Hosted destination | Missing | Extra | Field mismatches |
| --- | ---: | ---: | ---: | ---: | ---: |
| player_accounts | 2 | 2 | 0 | 0 | 0 |
| account_audit | 1 | 1 | 0 | 0 | 0 |
| account_xp | 1 | 1 | 0 | 0 | 0 |
| account_xp_awards | 1 | 1 | 0 | 0 | 0 |
| player_invites | 1 | 1 | 0 | 0 | 0 |

Foreign keys and administration references passed. Destination sessions, presence, rate counters, Steam links and replay fingerprints were empty at comparison time. Source room data stayed out of the account database.

## Functional results

- Hosted staging accepted an imported Python-generated scrypt credential, returned the original internal ID, profile revision, unlock and VIP flag, and authorized only the imported DEV account to search players. Read the original 100 XP and verified a 150 XP match award. A second hosted exercise covered registration, login, signature verification, recovery, old-session invalidation, profile persistence, logout, presence, permission denial and duplicate XP submission.
- An earlier staged run repeatedly returned a busy login response with the legacy asynchronous JavaScript KDF. The independent Worker now uses native scrypt with identical parameters, encoding and salts. The same imported fixture succeeded on the deployed version above.
- 50 focused Node tests passed for accounts, authorization, Steam linking, XP, invitations, room directory, relay and party behavior. Eight Python migration tests passed. A separate local workerd/D1 rehearsal passed with encrypted transfer and HTTP verification.
- Unity 6000.3.24f1 input, player, runtime and Editor assemblies compiled. V70 bundle checks confirmed that the compatibility wrapper fences both account prefixes and passes ordinary assets, directory and network responses through.
- The standalone Electron server build has four existing TypeScript errors in `chaos.ts`, `leet.ts` and `standard-melee.ts`, reproduced at the unchanged GitHub base. The project type check with its normal bundler configuration passed.
- Live Steam tickets, Windows host/guest gameplay, Expedition reconnection under load, old-client cutover and production player rows still need verification. Sites marks both production signing and legacy verifier values secret and redacts them. Full production export access, signing-key continuity and a trusted producer for ordinary player-hosted XP remain cutover prerequisites.

The approval also covers opening destination writes after the checks pass. The in-app capture rehearsal and final transfer, production signing-key rotation and compatible client rollout, trusted XP issuance and signed two-client Windows rehearsal remain outstanding. This approval records no live transfer or routing change.

See [account operations and rollback](README.md) for the exact operator procedure.

## Owner backup recipient verification

The owner supplied the public 3072-bit RSA certificate for Chip Arena Backup. Its DER public-key SHA-256 is `eecd03aa28b0b25cdc2e2cb58a4263ec7c5ac0ff0be2870caede46cf70b0c0a0`. The in-app exporter sealed a local two-account synthetic capture for this recipient. The owner reported a successful RSA-OAEP SHA-256 encrypt/decrypt check on their laptop with the matching private key. The owner subsequently re-encrypted the private key under a new passphrase and verified its derived public key still matches the recorded fingerprint. The key remains in the owner's custody. See the hosted synthetic capture and restore result below.

## Hosted synthetic capture and restore, November 16 (Eastern)

Ran the in-app exporter in an isolated Cloudflare Worker against a temporary D1 with the legacy schema and synthetic records. An HTTP attempt returned 403. A scheduled invocation of the exporter succeeded and stored its encrypted envelope for operator retrieval. Decrypted with a disposable rehearsal key and used the guarded import to restore a separate empty remote D1. Read every durable field back through the remote D1 API and compared against the original fixture. Two accounts, one audit record, one XP balance, one XP award and one invitation matched exactly. Sessions, rate limits, presence and both Steam tables were empty. The temporary Worker and databases were removed. This covers the exporter, envelope and database transfer; production Sites routing, owner-key restore of a hosted capture, signed client compatibility and XP issuance are separate checks. No live data or configuration was changed.

## First live-source rehearsal capture, November 16 (Eastern)

Published the temporary, default-disabled account exporter on Sites v71 from the exact live v70 source. The authorized HTTPS request returned an encrypted envelope for the owner's verified RSA recipient. SHA-256 `fd79ce057df631b9d18f649338828f7b64efe065e6cf513297ca27aa0e4907ee`, 41,603 bytes. Removed both exporter environment entries, redeployed, and confirmed the retired credential could not fetch the export. Live accounts remained writable on Sites, so this is a rehearsal capture, not a cutover snapshot. The independent production D1 still had zero players at preflight. Created an isolated, unbound rehearsal D1 with both destination schema migrations. The owner-side Windows restore package is pinned to this envelope and the rehearsal database. Local synthetic import, identical retry and mismatch detection passed. Awaiting owner-side decryption and remote field comparison. No player rows, credentials or backup files are in this report.
