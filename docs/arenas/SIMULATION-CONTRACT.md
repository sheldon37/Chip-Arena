# Arena simulation contract v1 — 2026-10-11

Status: implementation on `codex/unity-arena-simulation-foundation`, base `40009f1`.
Astra owns this contract/loader and all gameplay data. Claude owns dressing,
lighting, atmosphere and destruction presentation. No concept proposal exists
under docs/arenas at this base. `arena-1-lab` is a neutral simulation test fixture,
not the owner's final arena concept. All 14 old arenas and Ward Zero remain.

## Existing implementation confirmed
NativeDestruction fractures a breakable box into four PhysX chunks (360-prop
soft budget). CheckSupports drops a box if ALL direct support ids are absent or
have zero health. It does not trace support to foundations, telegraph, bound
active physics bodies, or freeze settled rubble. NativePropFrame streams every
prop transform; replay stores these frames. Keep this legacy path for old maps.
Core Rules.ArenaHalf=24 belongs to the isolated prototype, not NativeArena.

## Authoring
New maps load from Resources/ArenaMaps.json, appended to GameContent by the native
loader; the pinned GameContent export stays byte-identical. `ContentMap.gameplay`
has explicit `enabled`; absent/disabled means exact legacy behavior (including
unclamped physical movement), ±31/±27 aim, ±32/±28 shot ceiling footprint,
y=24 ceiling, y=-6 kill, y=-15 projectile/debris cleanup and global FFA spawns.

An enabled map supplies bounds (minX/maxX/minZ/maxZ, ceiling, killY, cleanupY),
ffa spawns (capsule CENTRES, y=.9 for floor y=0), optional teams (team id and
points, all eight team ids when used), spawnProtection radius, groups, hazards
and collapse limits. Each group has unique `id`, `building` id. Structural blocks
carry `group`, `materialClass` (concrete/steel/wood/glass), `anchor` and support[]:
directed edges to load-bearing blocks in the SAME building; ANY surviving path
to an intact anchored block supplies support. Cycles cannot self-support.
The existing material id supplies durability/mass. Structural boxes must be
axis aligned; visual trim/rotations belong in `visual`. Oversize groups are
rejected; author smaller sections. Spawn cylinders must have indestructible
floors and no structural or hazard footprints. Unassigned boxes are immutable
collision on enabled maps. `visual` is an opaque presentation sub-object:
simulation never reads it. Claude should coordinate schema/loader edits here.

## State and wire
A separate `StructureSnapshot` (`structures`, `hasStructures`) in NativeGameFrame is the source of truth. Its `layout` SHA-256 fingerprints gameplay geometry, materials, bounds, spawns, groups, edges and hazards, excluding visual dressing; peers with different arena data reject it. Every
structural block, including destroyed ones, has id, health, damageStage
(0 > 2/3 health, 1 > 1/3, 2 > 0, 3 broken), state
(Intact/Telegraph/Falling/Settled/Destroyed), from/position/settled transform,
size, group, seed, start tick and end tick. Tick rate is 60 Hz. All guest and
killcam worlds restore this snapshot, never simulate structural physics.
The host quantizes final rubble to millimetres, publishes decisions/final poses;
fall motion is derived from start/end ticks. No PhysX falling bodies or network
per-frame structural transforms. Settled boxes become static colliders and block
movement, shots and sightlines. A topology revision invalidates bot routes.
Full snapshots recover skipped events and allow late joining after any collapse.

StructureEvent has monotonic sequence, tick/time, kind and relevant fields:
- BlockDamaged: block id, amount, point, normal, cause, source seat.
- BlockBroken: block id, point, impulse, cause, source seat.
- CollapseTelegraph: group id, start tick/time.
- CollapseStart: group, seed, time, direction.
- CollapseSettled: group, time.
- HazardPhaseChange: hazard id, phase, time.
Events are bounded presentation cues; block state is never dropped for budget.
A broken support is destroyed; its unsupported dependants fall as authored
boxes and become permanent static rubble until the next round. No cosmetic
fragments are simulated. Claude may derive shards/dust from events and seed.

## Fairness / proposed owner rule
Default telegraph .75 s (tunable .5–1 s), at most 16 falling boxes and two active
sections (telegraph or falling). Excess sections wait and retain their collision.
Falling boxes have NO actor collision, NO HP damage and no physics crush.
At landing, an occupied rubble volume defers collider activation until clear;
nearby players receive at most 1.5 m/s horizontal knockback per section (no
upward launch), with spawn cylinders excluded. This conservative rule is the
proposal for owner playtest; damaging debris requires a later explicit tune.
Landing overlap is an authority input; seed does not claim cross-platform PhysX
replay. Determinism checks feed identical damage, ticks and occupancy inputs.

## Hazards and bots
Authored axis-aligned hazard zones have off/warning/active durations, offset,
damage-per-second and interval. Warning is >= .5 s; no spawn overlap. Phase
changes and current phases are replicated. No new hazard is enabled on old maps.
New-map bots use a bounded grid rebuilt on topology revision, with floor/clearance
sampling (four layers per metre column) and 1.5 m jump / 3 m drop connections.
Rebuild work is 256 columns per host tick; bots explicitly evacuate telegraphed sections. Telegraph/fall footprints are avoided.
No navmesh bake; platform/floor connectivity is measured from gameplay collision.

## Presentation handoff / acceptance
Consume snapshot/events for cracks, warning, falling boxes, dust and settling;
keep scenery colliders off the PropLayer and never mutate structure state.
The default box renderer only displays gameplay boxes; no destruction VFX added.
The experimental fixture is explicitly selected with the Experimental room
option (Advanced room settings), never random or between-set rotation. Dedicated testing uses `--ca-experimental --ca-map arena-1-lab`. A minimal option/filter hook is
plumbing; Claude owns its styling. Old saved map ids/defaults remain supported.
Windows gates: play with 2 and 8 pilots, every weapon blast, telegraph visibility,
cover/sightline changes, roof occupants, crowded landings, bots on multiple levels,
host/guest packet delay, late join and killcam, and frame/packet budgets. Managed
checks do not establish rendered quality, PhysX behavior or Windows acceptance.

## Implementation notes and limits

- `NativeMapRules.cs`: schema, catalog append, validation, legacy fallbacks and layout identity.
- `NativeStructure.cs`: pure support graph, bounded phases, deterministic landing and snapshots.
- `NativeMapSimulation.cs`: host damage, static collider binding, protected spawns, soft displacement and hazards.
- `NativeArenaNavigation.cs`: layered grid rebuilt on topology revision; new maps only. The older Ward Zero horde flow grid stays Ward-specific.
- `NativeNetworkFrame.cs`, `NativeFrameCodec.cs`, `NativeReplay.cs`: shared guest/late join/replay state and presence flags. Protocol 5 stays valid for old maps; experimental maps additionally require this catalog and schema.
- `ArenaMaps.json`: neutral test fixture, two structures and steps, eight spawns, 80×36 m footprint, y=28 ceiling, y=-8 kill plane. No active hazards or scenery yet.
- `ArenaFoundationChecks.cs`: managed determinism, strict authoring, legacy spawn/bounds, caps and queued sections, occupied landings, hazard phases, navigation, layout mismatch and real TCP state transfer.
- `NativeArenaFoundationBuildChecks.cs` + runtime validation: next Unity export must exercise real JsonUtility, area damage, host/guest/late collision, raycasts, rewind and frame budget. Added, not executed locally.

Rubble retains the authored X/Z footprint and compresses to at most .9 m high.
Falling is vertical, without tumbling or runtime fracture generation; Claude can
animate cosmetic tilt/dust around that collision contract. It is intentionally
not a general rigid-body demolition solver. Settled rubble is permanent and
indestructible for this milestone. Occupied landings can defer indefinitely;
they keep consuming the hard body/section budget. There is no timed forced crush.
All gameplay boxes in enabled maps are axis aligned; use small authored sections
and stairs rather than rotated ramps. Scenery must not carry gameplay colliders.
The lab has no visual warning, dust or destruction styling yet; Claude must add
these before owner acceptance. The `.75 s` state/event is ready to consume.

`ContentMap` additions and the minimal room toggle/filter are shared-file changes.
`GameContent.json`, source exporters, drawing/shader/lighting/scenery files and all
14 legacy arenas plus Ward Zero are unchanged. Review any future proposal against
this contract before authoring its gameplay blocks; no proposal was available at
implementation time. No old-map deletion/default migration belongs in this PR.
