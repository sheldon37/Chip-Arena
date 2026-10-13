# Native stall diagnostics and combat follow-ups

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
- `navigationMs` and `navigationSearches`: structural navigation work and route rebuild count, nested inside the gameplay step. Both PvP bots and PvE enemies use this search.
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
