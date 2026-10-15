# Expedition: first playable runtime milestone

2026-10-15. Owner priority: start playtesting. Presentation is merged at shared
`f156a21`; PR #218 contains the SAFE and semantic compile follow-up. This document
is the next implementation task, not a claim that a playable build exists.

## Astra: implement one real local run first

Keep the normal Expedition menu closed until acceptance passes. Add a DEV-only
local entry through the existing signed-in account/session path. Reuse imported
`expedition-city`, `expedition-ward-zero`, the layout manifest, town dressing,
door/gate states and all four PR #216 fields. Do not rebuild them.

1. Construct `ExpeditionDirector`, bind the session via
   `ConversionGame.BindExpeditionScene`/`BindExpedition`, and supply real pilot
   positions, alive/connected state, interaction evidence and geometry/nav probes.
   Unimplemented claim/purchase/reprint paths must refuse actions explicitly.
2. Implement the area loader: capture the readiness ticket before loading, switch
   `NativeArena.MapId`, load matching geometry/nav, validate the layout, create
   closure colliders and only then call `ReportExpeditionAreaLoaded`. Authority
   calls `CompleteAreaLoad` with actual atomic party placement. Failed loading or
   placement stays frozen with SAFE false; expose an actionable DEV error.
3. Bind closure collision to the director's blocking state, including the full
   opening interval. Route door interaction through authority distance checks.
4. Spawn actual catalog combat entities for the configured encounter/quota and
   final boss. Call `RecordBossArrival` only after creation, `UpdateBossHealth`
   on real health changes and `BossDefeated` from confirmed combat death. Never
   satisfy completion using timers, fabricated HP or ready acknowledgements.
5. Exercise city -> Ward Zero -> encounter completion -> return -> final boss ->
   Victory -> Continue/new city barrier, plus extraction. Use existing rules;
   do not silently reduce the required quota or change progression for this test.

## Evidence required before inviting the owner to play

- Real Unity assembly compile (player, editor-context Runtime and Editor), affected
  managed regressions, source checks and Windows build succeed on the target commit.
- In Unity/Windows, a local run completes the route above with real movement,
  damage, death and collision. SAFE follows actual sanctuary position and never
  lingers during loading or failed placement. Repeat Continue at least once.
- Record the exact commit/build, launch steps, credentials/setup requirements,
  known limitations, and a capture or concise observed playtest log in DEV_LOG.
- Publish to the existing test channel only after its normal verification gates
  pass. Do not label a managed preview or source package as a playable build.

After the local slice passes, test host + guest with real independent map loads,
late/stale acknowledgements, placement failure and disconnect. Full migration,
durable combat resume and progression/account integration remain separate release
requirements; any unsupported path must be unavailable or clearly blocked in DEV.

## Claude: what is needed

Wait for the runtime build/launch instructions, then inspect the existing visuals
in the real route: transfer overlay, collision/door alignment, SAFE tags, boss
arrival and health, dark-street wayfinding and Continue cleanup. Fix observed
presentation defects only. Offline acceptance already passed; no view field is
missing. Report exact build, observations and captures, separating local from
host/guest results. Astra owns runtime and combat integration.

## M1 runtime branch — 2026-10-15

Implementation branch: `codex/unity-expedition-m1`. No successful native build or
observed playable run is claimed yet. The native builder must pass the new
Expedition geometry/combat acceptance checks before this is a playtest candidate.

Intended launch steps for the resulting Windows test build:
1. Sign in with a DEV account. Stay offline (no hosted/joined room).
2. Open **Test Boss**, select your weapon, then **DEV Expedition M1**.
3. Walk from the city sanctuary to the Ward Zero doorway; press **F** in its
   interaction volume. Failed layout/loading/placement leaves the run frozen and
   reports an error; use Escape to leave or restart.
4. Kill the 12-enemy quota, then the **TEST Expedition Siren**. This provisional
   quota comes from the original Ward Zero preview; these are real catalog enemies.
   Boss HP and damage use the existing Expedition scaling at spawn.
5. Press **J** and vote to leave the cleared instance. Walk through the unlocked
   city gate into the final trigger and defeat the second TEST Siren.
6. Choose Continue in J to start the harder loop, or return to the sanctuary and
   choose extraction. Extraction uses the existing durable local save journal.
7. Escape → Restart Expedition M1 creates a new test run. Return to Main Menu
   clears the runtime binding. Local death requires restart in this milestone.

Scope: single signed-in local pilot; ordinary Expedition access remains closed.
Claim/purchase/reprint deliberately refuse until their evidence and live inventory
adapters exist; no loot or account progression is awarded by this test. The
existing preview remains separate. No host/guest/migration or saved-combat resume
claim. Claude's live visual pass starts only after an exact native build is posted.

Spitter authority range is 18 m (previously 11 m maximum aim, with the content's
shorter attack-start distance). Both attack initiation and the aim clamp use the
same authority range; visual assets and the legacy content export are unchanged.


### Boss-test scene parity and kill rewards (2026-10-15)

Ward Zero retains every boss-test block and ramp, with only the three exit-frame
blocks added. At the shared map-load boundary it now inherits the exact boss-test
hazard array (including IDs used by the lamps and alarms). Its raw draft omits that
array to avoid a second tuning copy. The quarantine look, full dressing, practical
lights, boss-test machine fixtures, song vents and owner-muted alarm sound remain
the same. Expedition publishes the existing wave/boss stage names so horde and boss
lighting actually activate; boss lighting follows the boss's health quarters.

Each boss arrival plays the existing Siren intro scene (9.0678 seconds); combat and
the Expedition clock pause for it. After the intro, Expedition still owns the kill
quota and loop transitions. This does not replace the M1 quota with the boss-test
wave schedule or possession phase sequence.

Confirmed non-DEV zombie kills credit the responsible player's Expedition wallet:
shambler 25, runner 20, spitter 30, brute 80, Hulk 300. Existing headshot (+10 before
bonuses), assist (40% reward after at least 20% damage) and money-chip formulas are
reused. Death callbacks and wallet events are deduplicated. Currency is personal
and persists with the run; buying/claiming/reprinting remains unavailable in M1.
