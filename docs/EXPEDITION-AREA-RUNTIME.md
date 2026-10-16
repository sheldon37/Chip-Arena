# Expedition area runtime foundation — 2026-10-16

Owner accepted the split-area scope in EXPEDITION-HUB.md. This is milestone 1's
**gated foundation**, based on shared `9f18015`; it is not split-party gameplay.
The live launch path still uses the existing single-area director and F entry.
Do not connect this registry to a live run until the native isolation gate passes.
Cohorts, per-area progression, negotiated wire, schema migration and join fields
remain subsequent milestones. No all-party join-circle interim is enabled.

## Ownership and scheduling

`ExpeditionAreaRegistry<T>` permits the hub and one shared Ward identity only.
Acquire reuses an existing runtime, including an empty instance, rather than
resetting an encounter. Occupancy and readiness are separate: a loading destination
is skipped while an occupied ready source keeps stepping. A monotonically numbered
host tick invokes the supplied shared-session clock once, then each occupied ready
runtime once. Duplicate/stale ticks do nothing. A partial-step exception latches
failure; neither the clock nor an earlier area's physics may be retried.

`NativeExpeditionAreaRuntime` creates a fresh NativeMatch and independent local
PhysicsScene before constructing its arena root. Its arena retains its own nav,
RNG, collections, event sequence, physics contacts and load state. Runtime Step
runs arena logic and that scene's physics once, with the existing PvE hold rule.
It records step/physics/nav costs. These are instrumentation hooks, not benchmarks.
The registry shows at most one runtime. Hidden roots keep colliders and simulation
active; their renderers use forceRenderingOff. Newly built props/fragments/machines
inherit that flag. The wrapper creates no cameras, lights, sound or presentation
components. Disposal disables arena colliders and releases its owned scene.

## Audit and changes

| Surface | Finding / boundary |
| --- | --- |
| Arena callbacks | NativeArena binds ResetRound, HoldResolution, RoundDeaths and related callbacks on its supplied match. The new wrapper always creates a distinct match; never pass the host's displayed match to another runtime. Seats must also be independently constructed when cohort transfer is wired. |
| Authority queries | Combat/navigation/projectile queries already use root.scene.GetPhysicsScene, or collider-specific APIs. A managed source gate now refuses default-scene ray/shape/overlap/check/simulate calls in current runtime sources. Validation fixtures and retired ArenaGame are excluded. |
| Simulation | ConversionGame's existing path now simulates arena.PhysicsWorld. The new scheduler must replace that single-area path when activated, not run alongside it. Otherwise the displayed area would step twice. |
| Client view queries | Aim, lock-on, shoulder/chooser cameras, ability preview, bow wall queries and footstep surface queries now use the displayed arena's physics scene. PhysicsScene buffer wrappers retain RaycastAll/check behavior. No sound or camera design change. |
| Gravity / physics settings | Game/dedicated startup owns the common -23 gravity and manual simulation mode. Explicit area-scene arena construction does not write global gravity. Legacy arena construction retains its prior assignment for standalone validation callers. Global SyncTransforms synchronizes transforms but does not advance another scene. |
| Collision layers / contacts | Layer policy is process-wide and idempotent (including the zombie-only hub ward). Per-collider ignore calls use locally owned actors. Prop callbacks target their own Arena and per-arena contact ledger. |
| Shared data / randomness | Content/catalogs are shared read-only during gameplay; per-arena combat RNG and match state are distinct. Editor fixtures that mutate map data run sequentially. Do not mutate shared layout/content to emulate transfers. |
| Rendering statics | NativeLighting/RenderSettings/shader globals, NativeExpeditionHubLive, hazard-kit slots, dressing preview caches, UI and audio are single-view services. Background runtimes must never invoke them. Presentation/killcam/interpolation resets and rebinding are required on future local-member transfers. |
| Global DEV clock | NativeDevNetwork currently sets Time.timeScale from the displayed arena. Before live split-area launch, negotiate/refuse per-area time control or replace that behavior with explicit host-wide policy. The independent runtime wrapper never writes timeScale. |
| Existing global Expedition bindings | Current director, session callbacks, barriers, public projection and save path still refer to one area. They are deliberately not connected to this registry. Milestones 2–5 must replace these bindings before any subset transfer. |

## Gates and next work

Managed `CoreTests --expedition-areas`: clock exactly once with two areas; loading
isolation; one shared Ward; empty retention/resume; display rebinding; duplicate and
stale ticks; fault latching; disposal; default-scene query audit.

Native `NativeBuildChecks.CheckExpeditionAreaRuntimes` runs in the existing import
validation pipeline using two independent Editor preview physics scenes. It checks
one hub pilot plus three Ward pilots, overlapping-coordinate ray/sphere/overlap
isolation, independent gravity probes, one simulation per tick, loading/empty
suspension, renderer suppression and reset callback isolation. It is **not** the
later projectile/hazard/cohort/reprint/network acceptance or the Windows worst-case
72+ zombie benchmark. Compile success does not satisfy its execution gate.

After native foundation PASS: implement immutable cohorts and destination-slot
validation with cohort barriers, rollback/idempotency and fresh reconnect tickets;
then replace director/economy decisions, scope the wire, migrate schema and build
the 3-second join rule. Preserve Claude's proposed joins[]/atJoin/area field names.
Keep exp-layout-4 a draft until those gates pass. Windows host/three-guest gameplay
and measured two-area timings remain mandatory before enabling subset entry.

Unity references used for scene ownership and manual stepping:
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsScene.Simulate.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.LocalPhysicsMode.Physics3D.html
