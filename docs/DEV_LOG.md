## 2026-11-08 — Astra: reduce NOCTIEL health (owner request)

- Branch `codex/noctiel-health-tuning`, base shared `e9ea5aab`.
- Lower `NativeFallenBoss.HealthScale` from 2.5 to 1.4: 44% less HP, 12% above The Deep End (1.25), with existing party/scenario scaling retained. Direct Boss Battle reads this constant through `NativeBossBattle.Health`.
- Health only; phase percentages, damage, lives, attacks, animation timing and networking are unchanged.
- Verification: source call-site/formula inspection and party-size comparison (1–4); whitespace and source package checks recorded in the PR. No .NET SDK or Unity runtime available here; native compilation/build and gameplay balance acceptance remain pending the builder.
- Implemented on task branch; merge and builder synchronization receipts in the PR. No live or Site release.


