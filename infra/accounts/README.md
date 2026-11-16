# Chip Arena account migration

The Cloudflare account owner administers Workers, D1, secrets, metrics and backups in their own dashboard. The only outbound dependency on the existing Site in the independent account Worker is a public room-code lookup when sending an invitation. Registration, recovery, saved pilots, privileges, XP and sessions use the independent D1 binding. Game packets never pass through this Worker.

See the [hosted staging results and current bindings](STAGING-REPORT.md) before provisioning.

## Source inventory, November 16, 2026

The live Site reported a successful v70 deployment at `51c0401a3d242d67df2ae113098870e6e5442ee9`. Its source repository's deployment branch is `main`. The GitHub game development base was `claude/sleepy-ritchie-g8ruhv` at `4f57b8ae7596325eaaf0ba4b478bb5348b39fc79`. GitHub's default `main` is a different branch. The live D1 overview and column metadata confirmed the 12 tables below. All seven packaged SQL migrations and the deployed schema declaration matched the GitHub base. Physical constraints, row counts and application data on production must be checked again from a consistent export. Neither a Site version nor its Git checkout contains a backup of the live database.

| Source table | Treatment | Data |
| --- | --- | --- |
| player_accounts | Copy every column verbatim | ID, username, scrypt verifier, recovery digest, developer bit, unlocks, flags, profile JSON, both revisions and timestamps |
| account_audit | Copy | IDs, actor, target, action, detail, created |
| account_xp | Copy | Account, total and revision |
| account_xp_awards | Copy | Account/completion key, payload, before/after balance, marker and timestamp |
| player_invites | Copy the snapshot | ID, sender, recipient, code, state and timestamps. Existing expiry still applies. The application ordinarily purges expired invitations. |
| player_sessions | Start empty | Players sign in once with their existing credentials. Original recovery codes continue to work. |
| player_presence, dev_login_rates | Start empty | Presence renews on polling. Authentication throttles restart. |
| online_rooms, party_rooms, party_seats, relay_rooms | Keep on the original service | Directory leases, seat keys, temporary snapshots, mailboxes, reconnection and host state |
| steam_links, steam_ticket_uses | New on destination | Unique account/Steam pair, proof replay fingerprints. Both survive subsequent recovery from an independent backup. |

There is no separate statistics or friends table in the observed schema. The transfer preserves the raw profile blob and every other source field, including unrecognized JSON properties. New profile writes on this branch preserve stored extension fields. The migrator stops for new tables or columns until someone classifies them.

## Prerequisites and isolated staging

Use Node 24, npm, Python 3.10+ with SQLite JSON support, OpenSSL 3, and a POSIX operator machine for snapshot handling. Execute the commands from the repository root unless a block starts in `infra/accounts`. Keep credentials in a secret manager or the protected operator environment. A Workers Paid CPU allowance is appropriate for the existing 32 MiB scrypt with three passes. Benchmark register, login and recovery under concurrent load on the actual plan. Free Workers allow 10 ms of CPU per request.

```sh
npm ci
cd infra/accounts
npm ci
W=./node_modules/.bin/wrangler
"$W" login
"$W" whoami
```

For a noninteractive runner, supply `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` from its secret store. Scope the token to the personal account with D1 Edit and Workers Scripts Edit. D1 dashboard/GraphQL analytics can need Account Analytics Read. Configure a workers.dev subdomain in Workers & Pages if the account has none. No zone or DNS token is required for a workers.dev address. A deploy token has no place in Unity, Electron, browser bundles or database rows.

The following provisions a **fresh** staging environment. For an existing named resource, inspect it and reuse its ID. Creating a second destination or running the test on production would defeat the comparison. Before recovery drills use a separate empty D1 and isolated Worker.

```sh
"$W" d1 create chip-arena-accounts-staging --location enam --update-config=false
export CHIP_D1_STAGING_ID='<returned UUID>'
export CLOUDFLARE_ACCOUNT_ID='<personal account ID>'
export CHIP_ACCOUNT_STATE=maintenance
node cf.mjs staging
C=wrangler.staging.local.json
"$W" d1 migrations apply DB --remote -c "$C"
"$W" deploy -c "$C"
```

Use independent staging-only secrets. `DEV_SIGNING_KEY` is a private P-256 signing JWK. `XP_ISSUER_SECRET` should be at least 32 random bytes encoded as hex or base64url. `DEV_ACCOUNT_VERIFIER` is optional legacy bootstrap data. Only provision a synthetic verifier on staging. Feed secrets with Wrangler's masked prompts, secret-manager pipes or `secret bulk` on stdin.

```sh
"$W" secret put DEV_SIGNING_KEY -c "$C"
"$W" secret put XP_ISSUER_SECRET -c "$C"
export CHIP_ACCOUNT_STATE=active
node cf.mjs staging
"$W" deploy -c "$C"
export CHIP_STAGING_ORIGIN='https://chip-arena-accounts-staging.<account-subdomain>.workers.dev'
export CHIP_STAGING_PUBLIC_JWK='/private/path/staging-public.json'
node staging-smoke.mjs
```

The smoke runner also requires the staging issuer secret in its process environment. It creates one synthetic account, verifies the signed multiplayer claim against the supplied **public** key, and tests password recovery, expiry of the old session, a profile save, privilege denial, presence and idempotent XP. Run migrations and byte comparisons on a fresh database **before** generating smoke-test writes. Local verification needs no Cloudflare identity:

```sh
cd infra/accounts
python -m unittest test_snapshot
python rehearse.py
cd ../..
node --import tsx --test tests/{independent-accounts,accounts,desktop-accounts,account-xp,party,relay,directory}.test.ts
```

The rehearsal creates two throwaway v70 accounts, seals a full SQL export, decrypts it, applies migrations to disposable local D1, imports only the selected tables, re-exports D1, compares fields and signs in through the built Worker on loopback. It also exercises the migrated developer, XP, recovery and sign-out.

## Secure snapshot, backup and import

Sites exposes read-only table/row inspection in the connected interface. It does not expose a point-in-time export, arbitrary SQL, or a personal-account D1 credential. Request an owner-authorized, consistent **full D1-format SQL snapshot** of the Site's `DB` from Sites support/administration. Obtain the provider's capture timestamp or bookmark and a secure delivery channel. If the provider explicitly grants direct source D1 access, use its issued account/DB identifiers and a source-only config to run `wrangler d1 export DB --remote --output <private-path>`. The personal destination token alone cannot export the Sites database. Paginated table views, a code archive or a collection of live player API calls cannot establish a consistent snapshot. Leave production writable until an export route and maintenance appointment are confirmed.

On an encrypted operator volume, establish a private directory outside any repository. Use an organization-controlled backup recipient (RSA 3072+ certificate) and store its private key separately with a tested recovery path. For a new recipient, the interactive OpenSSL command below encrypts the private PEM with an operator passphrase. Use separate encrypted locations for backups and key escrow. Verify the public certificate fingerprint using your trusted key record.

```sh
umask 077
install -d -m 700 "$VAULT" "$KEY_VAULT"
openssl req -x509 -newkey rsa:3072 -days 3650 -subj '/CN=Chip Arena D1 recovery' \
  -keyout "$KEY_VAULT/recovery.pem" -out "$VAULT/recipient.pem"
openssl x509 -in "$VAULT/recipient.pem" -noout -fingerprint -sha256
export CHIP_BACKUP_KEY="$KEY_VAULT/recovery.pem"
```

Provide `CHIP_BACKUP_PASSPHRASE` from the secret manager when the PEM is encrypted. The files and their immediate directories must be owner-only. Use a new output name each time. The following commands run from `infra/accounts`:

```sh
python snapshot.py backup "$VAULT/provider-final.sql" "$VAULT/source-final.cms" "$VAULT/recipient.pem"
python snapshot.py import production "$VAULT/source-final.cms" "$VAULT/destination-final.cms" "$VAULT/recipient.pem" > "$VAULT/report.json"
python snapshot.py verify "$VAULT/source-final.cms" "$VAULT/destination-final.cms"
```

Use `staging` with synthetic inputs for practice. The import checks the account and database UUID against the remote name, exports and inspects the empty target, creates an owner-only temporary import file, imports using Wrangler, exports again, seals the destination and verifies it. Its insert guard rejects existing data, including sessions and prior Steam links. Remote file imports use D1's file interface. The SQL file contains no conflict updates, truncations or deletes of existing records. If the connection drops, inspect the current destination by exporting, sealing and verifying before any retry. For a partial import keep the failed database for investigation and resume against a new empty target. The guard intentionally rejects re-imports into a populated DB.

The report contains counts, missing/extra rows, per-column mismatch counts and relationship failures only. A single mismatch is a cutover blocker. For a restore rehearsal or fresh candidate under a distinct name, set `CHIP_D1_STAGING_NAME` or `CHIP_D1_PRODUCTION_NAME` to the environment database name plus a hyphenated suffix and set the matching UUID before generating configuration. Real row values, usernames, secrets, token digests and fingerprints derived from player data do not belong in reports. Original SQL, import SQL and full backups are credential-bearing. The tooling uses OpenSSL CMS authenticated encryption (AES-256-GCM, RSA-OAEP/SHA-256) and disables Wrangler disk logging during transfers. Keep an encrypted original off the deployment machine. Remove temporary plaintext from the encrypted volume after confirming restoration. SQL snapshots are parsed in memory and sorted in private SQLite memory. Measure memory and duration during rehearsal before committing to a maintenance window.

For Linux/WSL development use `node cf.mjs development`, then `"$W" d1 migrations apply DB --local -c wrangler.development.local.json` and `"$W" dev --local -c wrangler.development.local.json`. Supply a disposable key via untracked `.dev.vars`. The local DB ID is a sentinel, suitable only for local commands.

## Gameplay, XP and Steam trust

The existing username/password, sessions, recovery rotation, administrative grants, revision checks and signed player claims are reused. Account authentication runs on the Worker. The existing 15-minute player claim is what hosts receive. A production Worker requires a key matching the existing client verification coordinates. Stage/development refuse those production coordinates. If the owner lacks the matching production private JWK, prepare a coordinated public-key rotation for all supported clients and hosts before activation. A stage playtest needs an isolated QA build pinned to the staging public key. Never ship a verifier that trusts arbitrary runtime keys. Do not use a production private signing key to make staging clients pass.

Existing Unity and Electron clients submit a completion description at `POST /api/accounts/xp`. On the independent Worker this acknowledges only an already committed receipt owned by the session. If it has not arrived, HTTP 503 lets the client retain and retry its queued award. A **trusted operator-controlled match result producer** must send authenticated terminal results to `POST /internal/match-completions`, with `Authorization: Bearer <XP_ISSUER_SECRET>` and JSON `{"accountId":"<verified account id>","completion":{"version":1,"completionId":"<stable id>","mode":"pvp","won":true,"bots":false,"seconds":90,"cleared":0}}`. The Worker computes the reward and deduplicates the receipt. Never distribute the issuer secret to a game host or participant. Ordinary player hosts and offline solo runs do not currently have a trusted results producer. **Keep production XP on the existing service until a trusted producer is integrated and verified for the modes that must keep awarding XP.** An origin change also changes Unity's per-origin XP outbox. Drain old pending entries before switching or maintain the legacy-origin compatibility route for those clients. Verify pending entries on both old and updated native clients during rehearsal.

For Steam, provision a real `STEAM_APP_ID` and environment-specific `STEAM_TICKET_IDENTITY` before generating config, and install `STEAM_PUBLISHER_KEY` as a Worker secret. A Steam client requests a fresh `GetAuthTicketForWebApi` ticket with the configured identity, waits for its callback, and hex-encodes the bytes. `POST /api/accounts/steam-link` requires an active player bearer session, the current account password, and that ticket. The Worker validates the ticket through Valve's publisher endpoint for its configured AppID/identity. `POST /api/accounts/steam-login` accepts a fresh ticket and optional `remember`, then issues a regular Chip Arena session only for an existing link. Both routes reject repeated tickets. Uniqueness on both sides prevents reassignment, and linking leaves the account ID and all saved state intact. Client SteamIDs confer no authority. Live Valve verification and the native Steam UI require Steamworks credentials, real tickets and the release build.

## Production cutover — owner approval required

1. Complete hosted staging tests, a full-scale snapshot rehearsal, encrypted backup restore and signed two-client Windows tests. Create/join a room, invite and accept, change party seats, start Expedition, disconnect/reconnect, finish a match and verify one trusted award per eligible account. Include an old supported client. Obtain the original production signing key or finish a reviewed rotation. Resolve pending XP issuance first.
2. In the personal account create an empty `chip-arena-accounts-production` D1 if absent. Set `CHIP_D1_PRODUCTION_ID` to its returned UUID. Run `CHIP_ACCOUNT_STATE=maintenance node cf.mjs production`, then `"$W" d1 migrations apply DB --remote -c wrangler.production.local.json` and `"$W" deploy -c wrangler.production.local.json`. Install the matching production signing JWK, trusted issuer secret, a distinct random 32-byte (hex) `ACCOUNT_BRIDGE_SECRET`, and any active legacy verifier via `wrangler secret put ... -c wrangler.production.local.json`. Verify `/healthz` returns `ready:true`, `environment:production`, `state:maintenance`. Ensure private internal submitters and admin jobs are paused. Keep the original database and release artifacts.
3. Prepare the legacy account routing bridge on the **then-current** deployed Site source. Set the same `ACCOUNT_BRIDGE_SECRET` in the Site's server-only environment. The bridge signs the original edge IP for the independent Worker's rate limiter because cross-zone Worker subrequests share an IP. Unauthenticated forwarded headers are ignored. Keep an intact copy of its published archive. From the game checkout, run `node infra/accounts/build-bridge.mjs /verified/live/dist/server/index.js /private/release/bridge.js` with a fresh output path. In a separate release checkout of that exact Site version, replace only `dist/server/index.js` with `bridge.js`. Confirm all other packaged asset and migration hashes match. The bridge delegates non-account requests to the published Worker, including its updater, room directory and transport. Save the previous Site version and verify the new bridge in default `ACCOUNT_ROUTING=local` mode in a private rehearsal before publishing it with approval. Old builds with relative or fixed URLs will use this bridge. Updated Unity builds take `CHIP_ARENA_ACCOUNT_ORIGIN` from their packaging environment into StreamingAssets, with a process override for QA. Explicit custom or disabled account origins retain their settings. Electron reads the variable in the main process, Vite embeds it at build, and the portable auth proxy reads it at runtime. Room/update endpoints are independent.
4. Announce the measured maintenance window. Stop new matches/finish active games and drain acknowledged XP/outboxes where possible. Set `ACCOUNT_ROUTING=maintenance` on the original Site and apply its runtime configuration. Fence **both** account prefixes, including the legacy DEV login. Confirm old/direct clients and trusted jobs receive 503, directory/room requests still work and all in-flight account transactions have finished. Take the final provider snapshot after the fence. Verify its timestamp/bookmark is later than the last committed account write. The final snapshot supersedes every rehearsal copy.
5. Run the three backup/import/verify commands above. Record the source bookmark, destination ID, Worker version and zero-mismatch report, plus independent encrypted backups. Keep both writers fenced throughout verification. A short outage is possible only when the provider can deliver the fenced export within the rehearsed duration. If it cannot, abort before opening the destination.
6. Confirm approval to open writes. Set `CHIP_ACCOUNT_STATE=active`, regenerate the production config and deploy it. Treat this instant as the rollback boundary even if no client has reported a write. Set the original Site's `ACCOUNT_UPSTREAM` to the exact verified HTTPS production origin, then `ACCOUNT_ROUTING=forward`. Probe both URLs. The compatibility route never retries against the old D1. Publish the separately verified client configuration for the independent HTTPS origin. Resume trusted producers and matches. Players sign in again. New clients can authenticate directly if Sites is unavailable.
7. Test registration, a consenting migrated QA user's existing password, recovery on a disposable account, profile reload on another client, DEV grant + audit, signed claims, one authoritative XP receipt, invite/accept, room join and reconnection. Monitor both auth paths, Workers errors/CPU/latency, D1 reads/writes, 409 conflicts, 429 throttling, pending XP and 5xx rates. Configure dashboard notifications for available error/CPU and budget thresholds on the owner's plan. Logs use allowlisted operation/status only. Never enable request body/header capture or Valve upstream URL logging.
8. **Rollback before destination activation:** leave the new Worker in maintenance, put the Site back in `local` mode, verify the original login and resume writes there. **After activation:** put both edges and the issuer back into maintenance; drain requests and take a fresh encrypted destination backup. Roll back Worker code to the tested previous compatible version while retaining the **latest authoritative D1**, and keep old clients pointed at it through the original Site. Do not re-enable local writes on stale Sites data. Restoring the physical Sites DB requires a provider-supported inverse import/reconciliation of every post-cutover change, a zero-mismatch verification, session invalidation, and a plan for new Steam links and replay records that v70 cannot store. Until those conditions are met, keep the latest D1 as authority or stay in maintenance. A code rollback is fast; a lossless reverse database migration may not be.

## Ongoing backups and recovery

Run `python snapshot.py remote-backup production "$VAULT/<unique-date>.cms" "$VAULT/recipient.pem"` from a protected operator backup job, with off-machine retention and independently escrowed keys. D1 Time Travel is an additional recovery option (7 days Free, 30 days Paid). Keep a bookmark before significant changes using `"$W" d1 time-travel info DB -c wrangler.production.local.json`. To practice restoring, provision a fresh isolated destination, apply both migrations and import an independent backup using the same verified importer. It preserves Steam links and used-ticket fingerprints and resets transient sessions/presence/rate limits. No write should be accepted against a partially restored DB. A Time Travel restore to an earlier point also discards later legitimate writes unless separately reconciled. Fence all writers, preserve the latest backup/bookmark and test the chosen recovery point before an owner-approved restore.

Official references: [D1 export/import](https://developers.cloudflare.com/d1/best-practices/import-export-data/), [D1 recovery](https://developers.cloudflare.com/d1/reference/time-travel/), [Workers CPU limits](https://developers.cloudflare.com/workers/platform/limits/), [Workers secrets](https://developers.cloudflare.com/workers/configuration/secrets/), [Steam ticket verification](https://partner.steamgames.com/doc/webapi/ISteamUserAuth#AuthenticateUserTicket).
