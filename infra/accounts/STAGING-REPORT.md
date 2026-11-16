# Independent Cloudflare staging verification

Verified November 16, 2026 against the account owner's Cloudflare account. Staging Worker version `6d5b3e38-a94d-45c6-b356-46844a7fdb9d` served HTTPS and the authenticated checks below. The live game still uses Sites v70. This report covers exclusively synthetic player records.

| Resource | Observed state |
| --- | --- |
| `chip-arena-accounts-staging` Worker | Active. [Health endpoint](https://chip-arena-accounts-staging.chip-arena-sheldon37.workers.dev/healthz) |
| `chip-arena-accounts-staging-restore-01` D1 | Current staging `DB` binding. Set `CHIP_D1_STAGING_NAME` to this name when regenerating staging configuration. Look up its UUID in the owner's D1 dashboard. |
| `chip-arena-accounts-staging` D1 | Earlier isolated migration rehearsal retained. Contains synthetic data. |
| `chip-arena-accounts-production` D1 | Both schema migrations applied. Confirmed zero player accounts. Production account Worker activation is pending owner approval. |

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

See [account operations and rollback](README.md) for the exact operator procedure.
