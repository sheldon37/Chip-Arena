## 2026-11-08 — Astra: faster invitation inbox polling

- Branch `codex/faster-invites`, base shared `841cc75d`. Owner reports roughly a minute to receive invites.
- Confirmed client delay: background social polling waited 20 seconds. Reduced to four seconds after successful completion, so slow responses cannot produce back-to-back requests. Same signed-in/menu-independent polling, no concurrent social request, cached inbox and five-second failure retry.
- Expected healthy-network delivery: up to four seconds plus request latency; this does not prove the full reported minute was polling. Existing popup suppression during combat remains unchanged. Recipients must update.
- Rate budget checked against current accounts router: at most 75 periodic social requests / five minutes against 100 per action; 15/min against 180/min per IP. Explicit invite actions can trigger additional refreshes as before. No service deployment or database change.
- Verification: Node 24 real-SQLite invitation directory/send/private-inbox/accept/one-use regression PASS; git diff --check PASS. No .NET SDK or Unity reference assemblies available in this session, so semantic compilation and native validation are delegated to the existing Windows builder. Two-player latency measurement not run.
- No visuals, combat/animation timing, hitboxes, networking protocol or saves changed. Shared merge and test builder sync follow; no live/Site release. Publication remains pending until builder receipt.

