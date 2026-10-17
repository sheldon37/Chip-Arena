## 2026-10-17 — Astra: authoritative first-cohort exit seal; publication verified

- Base shared `bc397907`; optional Claude handoff #20 uses the existing instance `entered` ledger, set only by `MoveMembers` after cohort readiness/commit. Split-instance exits begin open, then `sealing` at authority elapsed and `sealed` after one second. An emptied encounter remains sealed until clear. Non-split entry and boss shutters unchanged.
- Managed director 108 and co-op 41 checks pass, including pending/committed/empty-cohort transitions. Native Windows/two-client rendering of this change unrun.
- #17: `ExpeditionAreaProjection.Build` chooses the pending destination ticket immediately and emits joins only for the world ticket. Deployers do not receive their fresh hub deploy record on successful transfer; hub bystanders do.
- Publication correction: the owner's current desktop log identifies source `bc397907a6a3`, successful Unity build, and the status file reports publication succeeded 2026-10-17 20:33:43 UTC. The public feed was updated at 20:33:02 UTC to version `7b5a3845ab7a`, revision `639278659473702045`, channel `84c1dfc`. This is newer than `5dc860d` and includes the exit laser door. Verified existing publication, not a new publish by this task.
- Cloud run 416 still fails before a build number with HTTP 500. Main PR #270 adds bounded allowlisted request diagnostics (15 focused tests; hosted checks passed); merged as `f19bd67`. Root cause is not yet established; no blind POST retry or target changes.

undefined