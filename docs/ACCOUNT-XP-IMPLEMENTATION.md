# Permanent account XP — Astra implementation, 2026-10-19

Task branch: `codex/account-xp`, base shared `f1994f1`, subsequently integrated with shared `0a8e6b9` in PR #287. Owner authorized the remaining implementation after Claude's presentation handoff. Initial balance below is an implementation default, not playtested progression tuning.

## Rules

| Completion | Base XP | Victory bonus | Other |
| --- | ---: | ---: | --- |
| PvP full match (all sets, never individual rounds) | 100 | 50 | Both amounts halved when any bot participates |
| Standalone PvE result | 150 | 150 | Solo and cooperative count |
| Expedition loop | 200 | 400 | 100 per cleared instance, at most three |

At least 60 elapsed simulation seconds are required. PvP/PvE duration freezes at the terminal transition, so waiting on results never qualifies a short game. Expedition duration freezes in the authority's per-member receipt at victory, individual extraction or a connected-party wipe. It uses run elapsed time, not active-input time. One award per account/run/loop: extraction after victory does not award again; continuing creates a new loop identity. Disconnected members at the completion are excluded. Ordinary downing with a living party member does not finish the loop.

Signed-in solo, LAN and internet play count. Temporarily unreachable account service queues eligible completions. A signed-in account is required when the client observes the completion; past match history is not reconstructed for XP. Sandbox, previews, replays and DEV-command-assisted games are excluded by the client/host producer. Expedition DEV quarantine persists in the run snapshot across area changes and host handoffs, including later joiners. Merely using a DEV account does not disqualify normal play.

Level 1 starts at zero. Level L starts at `250 * (L-1) * L / 2`; the next level costs `250 * L`. Level cap 100, total cap 1,237,500. Capped line items are trimmed in order so their sum always equals the awarded increase; a zero-XP capped completion is recorded. No unlocks or gameplay perks, chips or currency are granted.

## Authority and storage

`GET /api/accounts/xp` returns the authenticated account's saved summary. `POST /api/accounts/xp` accepts only a version-1 completion description: ID, mode, win flag, bot flag, elapsed seconds and cleared-instance count. The account ID comes from the existing bearer session. Client-supplied totals, XP values and other account IDs are ignored. The service computes all saved totals, levels and bands; client estimates are presentation-only and may change on confirmation. If the saved balance is unknown, pending details are unavailable rather than inventing a saved balance.

Migration `drizzle/0005_account_xp.sql` adds `account_xp` and `account_xp_awards`; existing profile/grant rows and revisions are unchanged. Award uniqueness is `(account, completion)`. A transactional [D1 batch](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch) inserts an immutable result, conditionally updates the balance only for that insertion's random marker, then reads the receipt and current summary. Identical retries return the original before/after totals; a conflicting result with the same ID gets 409. Concurrent distinct completions serialize their balances. Ledger entries must not be pruned without preserving deduplication tombstones.

**Trust limit:** this is casual progression in a player-hosted game, not an anti-cheat service. An authenticated modified client or dishonest host can fabricate completion descriptions. Request validation, rate limits, server reward math and deduplication prevent malformed/cross-account/double writes, but do not prove gameplay. Do not attach competitive ranks, valuable unlocks or purchases without introducing trusted match registration/attestation.

## Client and network

- Match start creates a new UUID; the normal snapshot replicates it, the start clock and frozen terminal duration. Reconnects reuse it; rematches replace it. Older hosts lacking these fields award nothing.
- Expedition authority stores a detached `AccountXpCompletion` on each eligible member. Snapshots, host handoffs and viewer projection carry it. Extracted members retain their original receipt across subsequent loops and cannot receive later loop rewards.
- Each player submits their own result using their own account session. No bearer session goes to the match host or other guests.
- `NativeAccountXpTransport` stores the outbox under the native user-data directory, scoped by a SHA-256 hash of account-service origin and account ID. No credentials are stored in this file. Writes use temporary-file replacement. Pending/failed records are retained, with the latest 200 confirmed records. The server's permanent ledger handles replay of older local records.
- Pending is recorded before the HTTP call. Transient failures retry with backoff, then offer manual retry after five failed submissions. Sign-in/restart resumes failed manual records; expired sessions wait for sign-in. Invalid/rejected completions are terminal failures. Switching accounts or origin invalidates outstanding response handlers.
- Summary refreshes on sign-in, after confirmation, and every five minutes. Pending count never increases the saved total. All three one-line presentation bindings are connected; presentation remains responsible only for drawing and celebration history.
- Retry history requires writable user storage. A disk error is reported; an in-memory reward may still confirm, but restart recovery is not guaranteed if the outbox could not be written.

## Verification and rollout

Maintained gates: `npm run verify:relay` now includes `tests/account-xp.test.ts`; managed `--account-xp` includes `AccountXpProgressionChecks`. The tests cover duplicate/lost responses, concurrent results, account isolation, transaction rollback, cap/band math, rematch IDs, frozen duration, guest identity, Expedition lifecycle and DEV quarantine. Existing presentation tests still require both bindings and prevent authority code from calling visual panel/HUD types; the former blanket `AccountXp` name ban now permits the bound producer contract.

The native client cannot confirm XP until the central account Worker and additive migration are deployed. The repository's AGENTS.md requires explicit owner authorization for live-data migrations/production deployment. Prepare and test the Worker bundle first; apply the additive migration and deploy its matching Worker only after that authorization. Then merge/sync the verified client to the existing test builder and verify Windows publication. No credentials or live account rows are needed for local tests.

Windows play, real two-client disconnect/rejoin, on-device outbox recovery and live D1 deployment need separate verification. Unity assembly compilation is not a player build or multiplayer playtest.
