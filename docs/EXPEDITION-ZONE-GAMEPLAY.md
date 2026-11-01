# Expedition zone gameplay

Implementation branch: `codex/unity-zone-gameplay`, initially based on `dfb8aac`.
This implements batch 3's gameplay bindings against the owner-approved Station and
Ironworks contracts. Publication receipts belong in DEV_LOG and the task PR.

The nine zone kinds are shipped in `NativeHordeZones`. `NativeZoneCombat.Bind`
adds their content rows to the detached instance content, on authority and guests.
The original Siren resource rows and mixes remain the base mode's content.
The director selects weighted mixes by quota progress (0/15/50/85 percent), applies
per-kind caps and introduces a zone enemy at each phase. Cinderlings enter in
packs of up to three, bounded by remaining quota and entry capacity. Boss adds
use the zone mix. The Siren remains the approved placeholder boss; converter tilt
is still reserved for the future boss design.

`exp-layout-6` preserves map blocks and navigation, binds the authored allowed
kinds, imports the eight Rigger hangs and the boss-only converter shutter, and
retains the existing machines and joins. The manifest generator and frozen
activation snapshot reproduce these records. Placement checks now include each
allowed zone kind's actual capsule.

Each arena owns its enemy state, event timers, damage contact ledger and hazard
clock. No gameplay uses static presentation state. `NativeZoneEnemyFrame` carries
the Station/Ironworks event fields with finite -100 sentinels; its presentation
properties convert unset times to NaN. `NativeZoneHazardFrame` carries the local
schedule, express, lever, boss, and bounded glass events. Explicit presence flags
handle Unity's null-object serialization. Frame validation rejects malformed
values, unknown specials, duplicate chain targets and oversized event arrays.
Copies detach arrays for network and replay. Death records retain zone state,
including split children and existing poured metal.

Presentation bindings are `StationViewOf`, `IronworksViewOf`, and
`NativeArena.BindZonePresentation`, called for the arena actually drawn (including
replay). The Ladleman's existing pool draw is shared with its death event so
already-poured metal retains its original three-second lifetime.

Station levers use the normal interact key within 2.2 m of (36, 6, -54.4), cut power
and halt the express for 60 seconds, and become reusable after the resumed express
passes. Resumption always gets the full four-second warning. Initial express is
60 seconds after activation, followed by 70–110 second gaps. The lever is free.
Unspecified hazard tuning: rail 25/contact with one-second cooldown; cabinets 12
per arc; flood and steam 8 DPS; glass 18 per impact. Ironworks contact tuning:
thermal hazards 8 DPS, press/pusher 35, shear 22, with one-second repeat protection
for machinery. Wave changes restart the common warning schedule; boss entry parks
the pour and starts the remaining hazards with their warning windows.

Validation: `--zone-combat` covers bound rows, entry filters, mixes/caps, event
serialization/copy isolation, warning/lever boundaries. Existing Station/Ironworks
model, animation, hazard and layout checks retain their geometry and timing checks;
only pre-shipping pins are updated. The native four-area build gate also executes
actual enemy attacks/interrupts/weak points and passes their frames through the
codec into a separate guest arena. Native build results and human playtest limits
must be reported separately from source compilation.

Batch 3 #17 is unchanged: a deployer's ticket switches to the destination at
acceptance, so their own flash uses the existing final countdown ramp; remaining
hub viewers receive the explicit deployment record.
