# Native stall diagnostics and combat follow-ups

## Capturing a Ward Zero session

Start a fresh game process, reproduce a few stalls, then immediately use
Pause → Connection report → Copy. For all sampled hitches, quit and copy
`%USERPROFILE%\AppData\LocalLow\ChipArenaConversion\Chip Arena\Player.log`
before relaunching (the next launch rotates it to Player-prev.log).
Upload the log and copied report with the approximate freeze time, PvE/PvP,
solo/host/guest, wave/phase and whether it coincided with a kill or replay.
The builder's Latest-Build.txt diagnoses compilation, not gameplay frame times.
A short video with the visible freeze is useful if the log reports no hitch.

## Reading a stall report

While a native match exists, frame intervals of at least 100 ms and sustained
slow rendering (20 consecutive frames over 33.33 ms, or a slow rolling average)
produce `PERF_HITCH` JSON lines in Player.log. Open Pause → Connection report →
Copy to include the last report, total hitch count and session worst frame.
The records survive opening the pause menu. There is no hotkey or DEV requirement.

The logger keeps 240 frame durations without per-frame allocations. It emits at
most one report every two seconds and 120 reports per process session; counters
and the worst frame continue updating after the cap. It does not force GC or
write files per frame. Log entries omit stack traces, credentials and usernames.

- `frameMs`: wall-clock interval between completed LateUpdate callbacks, including
  waiting and stalls (not Unity's clamped simulation delta).
- `simulationMs`: all FixedUpdate work in that interval; `fixedTicks` counts callbacks.
- `updateMs`, `presentationMs`: timed Update and LateUpdate CPU work. Replay capture,
  snapshot creation, animation and effects are nested timings, not additional
  costs to add to those totals. Animation is the renderer's most recent sample.
- `arenaStepMs` and `physicsMs`: authority gameplay step versus BeforePhysics / PhysX / AfterPhysics, nested inside `simulationMs`.
- `navigationMs` and `navigationSearches`: structural navigation work and search/tree-build count, nested inside the gameplay step. Both PvP bots and PvE enemies use this search.
- `hordeMs` and `hordeSpawnMs`: horde update and enemy entry selection/creation, nested inside the gameplay step. Spawning and horde navigation can overlap the horde total; do not add these timings together. `pveStage`, `pvePhase` and `hordeCap` identify the active load.
- `navigationNearestMs` and `navigationLinkMs`: finding usable nearby nodes versus evaluating graph links, nested inside navigation time. `navigationLinkChecks` counts link evaluations; these can far outnumber searches.
- `deathBuildMs`, `deathStepMs`, `deathWoundMs`, `deathCreates`: regular corpse/effect setup, corpse stepping (including world refresh), wound-model work during death presentation, and new regular corpses. These narrow the overall death timer; other death effects can remain outside them. Timings can overlap and must not be summed.
- `deathRecordMs`: pilot death-record creation only, nested inside the gameplay step (not every damage/cleanup operation).
- `deathPresentationMs`: corpse/death presentation, including its creation and stepping, nested inside presentation time. This is separate from authority physics; use it to investigate deaths on other PvP maps.
- GC counters are runtime collection-count deltas. Matching generation counters do not establish three separate sets of pauses, and these reports do not measure GC pause duration.
- `unattributedMs`: remaining interval. It includes render/GPU waits, scheduling,
  other callbacks and work outside these timers. It is **not measured GPU time**.
- `gc0/1/2`: GC collection-count deltas, not GC pause durations. `managedBytes` is
  heap size without forcing collection, not total process memory or allocated bytes.
- Scene context: map, weapon, phase, focus/pause, graphics mode/API, resolution,
  pilot/minion/projectile/prop/event/corpse/slash counts and replay-buffer length.

A hard hang that never returns cannot emit a completed-frame record. A recovered
freeze can. Focus loss, startup/phase transitions and paused frames are labelled;
these must not be mistaken for confirmed combat bottlenecks. No GPU/Windows
profiling has been performed in the coding environment.

## Weapon crescent

Pending air attacks use a ten-second simulation safety window. The old crescent
hook captured that window, followed the pilot position, and retained the copied
end time after landing. Only landed air attacks may now create a crescent; their
actual impact window is used. Ground and sprint strokes keep their normal timing.
Pooled slash state now also copies the air/impact and sprint flags. No damage or
movement timing is changed by this trail fix.

## Siren execution and revive location

A completed Last Laugh catch now uses current HP + 1 forced damage in PvE as well
as PvP. Healing, ordinary damage reduction and protective shields cannot save the
caught victim at the crush. Death credit and Siren-crush presentation still use
the regular damage/death path, once. Catch eligibility/cancellation and the
3.2-second crush / 3.8-second finish clocks remain unchanged.

On a rendered authority (solo or player host), the kept ragdoll supplies its
current centre at most 10 times per simulation second. A downward world ray,
walkable-normal test, arena bounds and standing capsule clearance select a safe
revive position beneath it. If unsafe, the previous anchor stays. The dead pilot's
existing replicated body position carries the anchor; the immutable death record
stays at the kill location. The beacon/HELP, teammate interaction and ordinary
revive all use the anchor. Pad/wave respawns retain their explicit destinations.
Repair scanning uses the current ragdoll pose and revive floor height.

Guests do not select authority anchors. Cosmetic ragdolls may differ slightly
between peers. A headless authority has no presentation ragdoll and retains its
existing dead-body anchor; exact ragdoll following there is not implemented.
Claude: no redesign needed; preserve the repair art and use these anchor semantics.

Validation: managed trail checks cover pending air attacks, landed impact and
expiry; hitch checks cover recovered freezes, sustained slow frames, rolling
recovery, throttling and cap. The native interaction gate additionally checks a
healed/resistant/shielded victim, once-only crush, safe anchor, invalid anchor,
replication and respawn. Actual Unity/PhysX and Windows acceptance remain required.

## Ward Zero crowd follow-up (2026-10-13)

Hidden Siren phase two retains its authored 20x spawn rate and 12x cap (up to
192 adds). With 12 or more adds, mobs chasing the same pilot share a reverse
shortest-path tree; individual routes and immediate unsafe-edge checks remain.
Trees refresh on the existing 0.7-second cadence, target movement or topology
revision. Directed drops remain one-way. Small groups and the possessed enemy
retain individual searches. Equal-cost crowd paths may choose different lanes.

The possessed enemy selects its hiding destination before navigation instead of
requesting chase and hide paths that invalidate one another every fixed tick.
Hide selection now runs every 0.75 seconds. Feet-relative support/navigation
queries fix the full-height Hulk missing the floor and its first waypoint.
Actual Hulk capsule, health, attack timing and crowd limits are unchanged.

Managed crowd checks do not include Unity raycasts, physics or rendering. The
native Ward build gate checks real Hulk movement, shared crowd search count and
the possessed route cache. Windows phase-two load testing and a fresh hitch
capture remain necessary before calling the performance problem resolved.

## Owner captures and follow-up (2026-10-13 evening)

Ward Zero local boss wave: 138.62 ms frame, 118.37 ms navigation, four searches,
14 adds; physics .89 ms, death presentation .10 ms, replay inactive. The separate
report-header simulationTickMs is a later tick, not the recorded hitch.
Cyber City local practice Result/replay: 2382.46 ms frame, 2373.76 ms death
presentation; replay prepare/apply zero, GC counters +17. Death presentation is
nested inside replay playback, not another 2.37 seconds to add. The capture
does not separate corpse construction, world refresh/stepping or wound meshes.
No exact GC pause or allocation volume can be inferred from collection counts.

Navigation now precomputes only static hazard-envelope membership for authored
links/nodes during arena setup. Hazard clocks, structural state and collision
linecasts remain live, with original route selection and safety gates. The
index is rebuilt on map change. Managed equivalence and allocation checks cover
all authored hazard maps; actual Windows navigation performance needs another
capture. New death sub-timers diagnose the unresolved replay freeze.

## First-use animation banks (2026-10-13 evening)

New Factory capture: 2431.90 ms frame / 2423.03 ms death presentation, but
only 25.09 ms regular corpse creation, .0007 ms stepping and .0048 ms wounded
models, with 17 GC increments. Ward replay earlier spent 591.24 ms in death
presentation with none of those sub-timers active.
The fallback death animation paths call static bank loaders: SourceMotion
expands to 14,413,209 bytes of JSON; SceneMotion to 94,316,897 bytes. SceneMotion
previously loaded even for a missing cinematic death key before falling back
to a regular corpse. Loading is a strong candidate consistent with the captures,
not a directly measured attribution from these fields. Both banks now prewarm
during client Awake, after the dedicated/duplicate-instance exits and before
presentation setup. Their existing static caches serve subsequent matches and
replays. Player.log emits MOTION_BANKS_READY with clip counts and elapsed time.
This moves decompression/JSON allocation to startup, so startup can take longer
and the bank memory is resident earlier. No explicit GC or animation changes.
A fresh process followed by the same Ward/Factory replay is the acceptance test;
other unmeasured death-effect paths can still stall.
