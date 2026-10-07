# Native Unity conversion status

Source baseline: `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Unity: **6000.3.24f1**.
This project and executable remain separate from Electron. Work is continuing toward the owner's full-conversion request.
The following is implementation status, not a claim of Windows or visual acceptance.

| Area | Native implementation at this checkpoint |
|---|---|
| Combat | Standard gun/magic/melee primaries and Q/E, modifiers, block/reflect, recovery, status fields, summons |
| Chips | Full 236-entry source catalog is eligible through explicit stat/mechanic support catalogs; six PvE money Chips |
| DEV | Signed-access Emperor, Ansatsuken, Leet and Siren, their actions and style Chips |
| PvE | Three wave schedules, horde bodies/limb damage, economy and stations, personal drafts, recruits, revives, boss phases, entry queues, moving doors/grates and vent travel/falls |
| Presentation | 124 original model exports with three procedural textures, 165 original motion samples including 20 entrances, 31 source cosmetic scene tracks and original entrance lighting, existing captured melee clips, combat lights/fields, original night-sky shader port, native death bodies and effect adapters |
| Audio | 83 original source files with menu/map/boss music and combat event routing |
| Accounts | Configured HTTPS service, login/register/recover/profile/logout, signed unlock verification, Windows encrypted session persistence, developer grant management and in-room token refresh |
| Multiplayer | Native TCP LAN and WebSocket relay rooms/discovery, host authority, bounded compressed frames, source/version lock, readiness and reconnect leases; headless Unity authority mode |
| Controls | Saved graphics/audio/sensitivity settings, first person, melee lock-on, spectator switching, isolated read-only kill replay and local developer sandbox |
| Packaging | Separate Windows builder, CA icon, source asset receipts and fail-closed import/build gates |

Still being completed: remaining environment/Siren presentation, living-wound carving and native body refinements;
full native gameplay gates; final packaging and handoff. Player-host migration is not implemented; a separate
headless authority mode is available in source. See Relay/README.md for the networking boundaries.
Some cinematic, rig and PhysX adapters are approximations of the source renderer/physics rather than identical output.
Unity clients cannot join Electron rooms. No service endpoint or credentials are bundled and no live service mutation was performed.

Verified locally: managed/source parity checks, real loopback TCP and WebSocket relay transport, Unity Runtime/Editor/Windows C# compilation.
Unrun: activated Unity import and shader compilation, rendered play, native PhysX acceptance, live accounts/remote rooms and Windows EXE.
See VALIDATION.md and docs/DEV_LOG.md for the detailed checkpoint record.
