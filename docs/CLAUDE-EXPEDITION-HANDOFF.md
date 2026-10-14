# Expedition — current Claude handoff

Verified shared/builder base: `5aa2e0a48cfb7677c8e564f46ba7f1fba7826648`,
`codex/unity-cloud-current` (2026-10-14). Fetch before branching; preserve newer work.
Astra follow-up branch: `codex/expedition-director-lifecycle`.

## Already present — do not rebuild

- Imported town and Ward Zero instance maps/nav and `Resources/ExpeditionLayout.json`
  (`exp-layout-2`); ordinary PvP, Sandbox and DEV map selectors exclude them.
- Town dressing: facades, storefronts, landmarks, vehicles, sanctuary, skyline,
  fires/fog, picked lights and ambience (`NativeExpeditionCity*`).
- Door/gate seals and transitions, sanctuary boundary, transfer overlay and boss
  arrival presentation (`NativeExpeditionStates*`).
- SAFE tags from `members[].seat`, crack/roar at `bossArrivals[].x/y/z`, boss bars
  from `hp/maxHp`, initial layout binding from `areaBarrier.layout` (PR #216;
  Claude binding merged at `5aa2e0a`).
- Hulk v2 presentation binding also exists; it is not an outstanding Expedition task.

## Claude next: presentation acceptance and necessary fixes

Read AGENTS.md, current PROJECT_STATE/DEV_LOG and EXPEDITION-PRESENTATION.md before
editing. Own presentation only; Astra owns runtime scene/combat/network/persistence.

Use the existing presentation and inspect it in Unity/Windows when available:
- Doors and final gate: seals remain visible for the entire authoritative blocking
  interval, including the one-second opening; no apparent opening before collision.
- First load: matching layout fingerprint binds the correct map; mismatches show
  no misplaced world art. Transfer overlay follows loading/ready state.
- SAFE tags: correct other pilot after seat changes, no first-person self tag, no
  misleading invulnerability claim; test wall occlusion and range.
- Boss intro/crack: correct recorded spawn, once per arrival, no replay on late join;
  bars follow damage/healing/max-HP changes and death without restarting the intro.
- Town: visibility in dark streets, Ward airlock clearance, wayfinding, low/reduced
  tiers, effects/lighting cost, ambience and 3440x1440 readability.
- Continue: clear old boss plates/effects for the new loop and follow its new loading
  barrier. Draw only from replicated state.

Fix demonstrated presentation defects. Add no new maps, bosses or mechanics in
this pass. If the runtime adapter is still missing, record that as a blocked live
acceptance case; a preview or offline render is not a playable run or two-client pass.
Do not fabricate ready ACKs, boss HP, SAFE flags, collision or migration success.

## Astra remaining

This follow-up implements the fenced area-ready message and same-area loop reset;
it does not implement the map loader. Remaining work:
- authored scene creation, real geometry/navigation probes and closure collision;
- map loading, authenticated door interaction and real party placement/ready hooks;
- encounter catalog/spawning, combat/death/health callbacks and pilot-state binding;
- pickup/cache/reprint input with authority-observed evidence;
- durable director resume and full endpoint/combat migration with fencing;
- progression/account integration and native host/guest acceptance before release.

The normal Expedition menu remains closed. Source present, managed tests, native
compile, Windows playtest and published update are separate statuses.

## Return handoff

Record exact branch/commit, changed presentation files, actual checks, images or
captures when available, and unrun/blocked acceptance cases in DEV_LOG. Report any
missing authority contract with the exact field/callback; do not reopen the four
already-bound fields above. Preserve shared work and send a concise PR/handoff.
